using System;
using System.Collections.Generic;
using System.Text;

namespace CastilloSolver;

/// <summary>Resultado de una partida, siempre desde el punto de vista del BLANCO (el que arranca).</summary>
public enum Resultado { Negro = -1, Empate = 0, Blanco = 1 }

/// <summary>Por que termino la partida. Se cuenta cada final por separado.</summary>
public enum Final {
    Castillo,        // alguien levanto el castillo dorado
    SinConstructor,  // se quedo sin constructor Y sin taller: no puede construir nunca mas
    Ahogado,         // le tocaba jugar y no tenia ninguna jugada legal
    Repeticion,      // la misma posicion se repitio N veces
    Limite,          // se acabo el limite de plies sin que pasara nada
}

/// <summary>
/// Las lecturas de las reglas que quedaron ambiguas en el enunciado. Cada una es un
/// interruptor porque el juego esta en diseño y hay que poder dar vuelta cualquiera.
/// Los valores por defecto son la lectura literal (ver README, seccion 2).
/// </summary>
public sealed class Reglas {
    /// <summary>El sacerdote tambien puede convertir edificios enemigos, no solo unidades.</summary>
    public bool SacerdoteEdificios = false;

    /// <summary>Al tomar un edificio el guerrero NO entra: se queda donde estaba.</summary>
    public bool GuerreroQueda = false;

    /// <summary>Se cae la regla de que dos edificios no pueden estar pegados.</summary>
    public bool ObraLibre = false;

    /// <summary>La regla del no-pegado sigue valiendo para los tres edificios, pero no para el castillo.</summary>
    public bool CastilloLibre = false;

    /// <summary>Cuantas veces tiene que repetirse una posicion para que sea empate.</summary>
    public int RepeticionesEmpate = 3;

    /// <summary>Tope duro de plies. Pasado eso la partida cuenta como Final.Limite.</summary>
    public int PliesMax = 300;

    /// <summary>Disposicion inicial: esquinas / frentes / diagonal. Ver Juego.Inicial.</summary>
    public string Inicio = "esquinas";

    public Reglas Copia() => (Reglas)MemberwiseClone();

    public string Etiqueta() {
        var sb = new StringBuilder(Inicio);
        if (SacerdoteEdificios) sb.Append("+sacerdote-edificios");
        if (GuerreroQueda) sb.Append("+guerrero-queda");
        if (ObraLibre) sb.Append("+obra-libre");
        if (CastilloLibre) sb.Append("+castillo-libre");
        return sb.ToString();
    }
}

/// <summary>
/// Reglas del juego de los cuatro edificios.
///
/// El tablero es un ulong: 4 bits por casilla, 16 casillas, indice = fila*4+columna.
/// Cada casilla guarda un codigo de pieza. Las unidades fuera del tablero no se
/// guardan en ninguna parte: como cada jugador tiene exactamente una de cada tipo,
/// "esta fuera" es lo mismo que "no aparece en el tablero", y una unidad muerta y una
/// que todavia no salio de su edificio son el mismo estado.
/// </summary>
public sealed class Juego {
    public const int Casillas = 16;
    public const int Blanco = 0;
    public const int Negro = 1;

    // Tipos: 0..2 unidades, 3..5 edificios. El indice del edificio de una unidad es tipo+3.
    public const int Constructor = 0, Guerrero = 1, Sacerdote = 2;
    public const int Taller = 3, Cuartel = 4, Iglesia = 5;

    public const int Vacio = 0;
    public const int Castillo = 13;   // el castillo dorado no es de nadie

    public const int MaxJugadas = 96;

    // Tipos de jugada
    public const int MOVER = 0, MATAR = 1, TOMAR = 2, CONSTRUIR = 3, DESPLEGAR = 4, CONVERTIR = 5, CORONAR = 6;

    public readonly Reglas R;
    public readonly int[][] Ady = new int[Casillas][];
    public readonly int[][] Simetrias = new int[8][];   // las 8 simetrias del cuadrado (D4)

    public Juego(Reglas reglas) {
        R = reglas ?? new Reglas();

        for (int c = 0; c < Casillas; c++) {
            int f = c / 4, col = c % 4;
            var v = new List<int>(4);
            if (f > 0) v.Add(c - 4);
            if (f < 3) v.Add(c + 4);
            if (col > 0) v.Add(c - 1);
            if (col < 3) v.Add(c + 1);
            Ady[c] = v.ToArray();
        }

        for (int k = 0; k < 8; k++) {
            var m = new int[Casillas];
            for (int c = 0; c < Casillas; c++) {
                int f = c / 4, col = c % 4;
                int nf = f, nc = col;
                if ((k & 4) != 0) { int t = nf; nf = nc; nc = t; }   // transponer
                if ((k & 1) != 0) nf = 3 - nf;
                if ((k & 2) != 0) nc = 3 - nc;
                m[c] = nf * 4 + nc;
            }
            Simetrias[k] = m;
        }
    }

    // ---------------------------------------------------------------- codigos

    public static int Cod(int dueno, int tipo) => 1 + dueno * 6 + tipo;
    public static int Tipo(int cod) => (cod - 1) % 6;
    public static int Dueno(int cod) => (cod - 1) / 6;
    public static bool EsPieza(int cod) => cod != Vacio && cod != Castillo;
    public static bool EsUnidad(int cod) => EsPieza(cod) && Tipo(cod) <= Sacerdote;
    public static bool EsEdificio(int cod) => cod == Castillo || (EsPieza(cod) && Tipo(cod) >= Taller);

    public static int En(ulong b, int c) => (int)((b >> (c * 4)) & 0xF);
    public static ulong Con(ulong b, int c, int v) => (b & ~(0xFUL << (c * 4))) | ((ulong)v << (c * 4));

    // Mascara de codigos presentes en el tablero: bit i = hay al menos una pieza de codigo i.
    public static int Presentes(ulong b) {
        int m = 0;
        for (int c = 0; c < Casillas; c++) m |= 1 << En(b, c);
        return m;
    }

    public static bool Hay(int pres, int dueno, int tipo) => (pres & (1 << Cod(dueno, tipo))) != 0;

    /// <summary>Sin constructor en el tablero y sin taller propio no hay forma de volver a construir.</summary>
    public static bool Muerto(int pres, int dueno) =>
        !Hay(pres, dueno, Constructor) && !Hay(pres, dueno, Taller);

    public static int Edificios(int pres, int dueno) {
        int n = 0;
        for (int t = Taller; t <= Iglesia; t++) if (Hay(pres, dueno, t)) n++;
        return n;
    }

    public static int Unidades(int pres, int dueno) {
        int n = 0;
        for (int t = Constructor; t <= Sacerdote; t++) if (Hay(pres, dueno, t)) n++;
        return n;
    }

    // --------------------------------------------------------------- jugadas

    public static int Jug(int tipo, int desde, int hasta, int extra) =>
        tipo | (desde << 3) | (hasta << 7) | (extra << 11);

    public static int JTipo(int j) => j & 7;
    public static int JDesde(int j) => (j >> 3) & 15;
    public static int JHasta(int j) => (j >> 7) & 15;
    public static int JExtra(int j) => (j >> 11) & 15;

    /// <summary>Una casilla sirve para construir si esta vacia y no toca ningun edificio.</summary>
    public bool SitioDeObra(ulong b, int c) => SitioDeObra(b, c, false);

    public bool SitioDeObra(ulong b, int c, bool esCastillo) {
        if (En(b, c) != Vacio) return false;
        if (R.ObraLibre) return true;
        if (esCastillo && R.CastilloLibre) return true;
        foreach (int a in Ady[c]) if (EsEdificio(En(b, a))) return false;
        return true;
    }

    public int Jugadas(ulong b, int turno, Span<int> buf) {
        int n = 0;
        int pres = Presentes(b);
        bool tieneLosTres = Edificios(pres, turno) == 3;

        for (int c = 0; c < Casillas; c++) {
            int v = En(b, c);
            if (!EsPieza(v) || Dueno(v) != turno) continue;
            int t = Tipo(v);

            if (t <= Sacerdote) {
                foreach (int a in Ady[c]) {
                    int w = En(b, a);
                    if (w == Vacio) { buf[n++] = Jug(MOVER, c, a, 0); continue; }
                    if (w == Castillo) continue;
                    if (Dueno(w) == turno) continue;
                    int tw = Tipo(w);

                    if (t == Guerrero) {
                        // El guerrero mata unidades pisandolas, y toma edificios metiendose
                        // adentro, pero solo si no tiene ya uno de ese tipo.
                        if (tw <= Sacerdote) buf[n++] = Jug(MATAR, c, a, 0);
                        else if (!Hay(pres, turno, tw)) buf[n++] = Jug(TOMAR, c, a, 0);
                    } else if (t == Sacerdote) {
                        // El sacerdote reemplaza la pieza enemiga por la propia del mismo
                        // tipo, que tiene que estar fuera del tablero.
                        bool edificio = tw >= Taller;
                        if (edificio && !R.SacerdoteEdificios) continue;
                        if (!Hay(pres, turno, tw)) buf[n++] = Jug(CONVERTIR, c, a, 0);
                    }
                }

                if (t == Constructor) {
                    foreach (int a in Ady[c]) {
                        if (SitioDeObra(b, a, false))
                            for (int tb = Taller; tb <= Iglesia; tb++)
                                if (!Hay(pres, turno, tb)) buf[n++] = Jug(CONSTRUIR, c, a, tb);
                        if (tieneLosTres && SitioDeObra(b, a, true)) buf[n++] = Jug(CORONAR, c, a, 0);
                    }
                }
            } else {
                // Edificio propio: es el punto de aparicion de su unidad, si esta afuera.
                int tu = t - 3;
                if (Hay(pres, turno, tu)) continue;
                foreach (int a in Ady[c])
                    if (En(b, a) == Vacio) buf[n++] = Jug(DESPLEGAR, c, a, tu);
            }
        }
        return n;
    }

    public ulong Aplicar(ulong b, int turno, int j) {
        int tipo = JTipo(j), desde = JDesde(j), hasta = JHasta(j), extra = JExtra(j);
        switch (tipo) {
            case MOVER:
            case MATAR: {
                int v = En(b, desde);
                return Con(Con(b, desde, Vacio), hasta, v);
            }
            case TOMAR: {
                // El edificio cambia de bando. El guerrero queda adentro (o sea, fuera del
                // tablero) salvo que se juegue con la lectura de que no entra.
                ulong nb = Con(b, hasta, Cod(turno, Tipo(En(b, hasta))));
                return R.GuerreroQueda ? nb : Con(nb, desde, Vacio);
            }
            case CONSTRUIR:
                return Con(b, hasta, Cod(turno, extra));
            case CORONAR:
                return Con(b, hasta, Castillo);
            case DESPLEGAR:
                return Con(b, hasta, Cod(turno, extra));
            case CONVERTIR:
                return Con(b, hasta, Cod(turno, Tipo(En(b, hasta))));
            default:
                throw new InvalidOperationException("tipo de jugada desconocido");
        }
    }

    // -------------------------------------------------------------- finales

    /// <summary>
    /// Estado terminal de una posicion mirada por el que TIENE que jugar. Devuelve null si
    /// la partida sigue. No mira ahogado: eso lo decide quien genere las jugadas.
    /// </summary>
    public (Resultado res, Final fin)? Terminal(ulong b, int turno) {
        int pres = Presentes(b);
        if ((pres & (1 << Castillo)) != 0) {
            // El castillo lo levanto el que acaba de jugar, o sea el rival del turno.
            int ganador = 1 - turno;
            return (ganador == Blanco ? Resultado.Blanco : Resultado.Negro, Final.Castillo);
        }
        bool muertoYo = Muerto(pres, turno), muertoEl = Muerto(pres, 1 - turno);
        if (muertoYo && !muertoEl) return (turno == Blanco ? Resultado.Negro : Resultado.Blanco, Final.SinConstructor);
        if (muertoEl && !muertoYo) return (turno == Blanco ? Resultado.Blanco : Resultado.Negro, Final.SinConstructor);
        if (muertoYo && muertoEl) return (Resultado.Empate, Final.SinConstructor);
        return null;
    }

    // ------------------------------------------------------------ simetrias

    public ulong Transformar(ulong b, int k) {
        int[] m = Simetrias[k];
        ulong r = 0;
        for (int c = 0; c < Casillas; c++) {
            int v = En(b, c);
            if (v != Vacio) r |= (ulong)v << (m[c] * 4);
        }
        return r;
    }

    /// <summary>La menor de las 8 simetrias. Las reglas no distinguen orientacion, asi que
    /// dos posiciones con la misma forma son la misma posicion.</summary>
    public ulong Canonica(ulong b) {
        ulong mejor = b;
        for (int k = 1; k < 8; k++) {
            ulong t = Transformar(b, k);
            if (t < mejor) mejor = t;
        }
        return mejor;
    }

    // -------------------------------------------------------------- inicial

    /// <summary>
    /// El enunciado no dice como empieza la partida. Lo unico que la hace coherente es
    /// arrancar con taller + constructor de cada lado: sin taller no hay de donde sacar
    /// unidades y sin constructor no hay quien levante nada. Las tres disposiciones son
    /// simetricas por giro de 180 grados, asi que ninguna le da ventaja posicional a nadie.
    /// </summary>
    public ulong Inicial() => Inicial(R.Inicio);

    public static ulong Inicial(string nombre) {
        ulong b = 0;
        switch (nombre) {
            case "esquinas":   // talleres en esquinas opuestas, constructor al lado
                b = Con(b, 0, Cod(Blanco, Taller));
                b = Con(b, 1, Cod(Blanco, Constructor));
                b = Con(b, 15, Cod(Negro, Taller));
                b = Con(b, 14, Cod(Negro, Constructor));
                return b;
            case "frentes":    // cada uno en su fila, enfrentados
                b = Con(b, 1, Cod(Blanco, Taller));
                b = Con(b, 2, Cod(Blanco, Constructor));
                b = Con(b, 14, Cod(Negro, Taller));
                b = Con(b, 13, Cod(Negro, Constructor));
                return b;
            case "diagonal":   // taller en la esquina, constructor en diagonal hacia adentro
                b = Con(b, 0, Cod(Blanco, Taller));
                b = Con(b, 5, Cod(Blanco, Constructor));
                b = Con(b, 15, Cod(Negro, Taller));
                b = Con(b, 10, Cod(Negro, Constructor));
                return b;
            case "solo-taller":  // se arranca solo con el taller y el primer turno es desplegar
                b = Con(b, 0, Cod(Blanco, Taller));
                b = Con(b, 15, Cod(Negro, Taller));
                return b;
            case "centro":     // talleres pegados al centro, mirando hacia afuera
                b = Con(b, 5, Cod(Blanco, Taller));
                b = Con(b, 4, Cod(Blanco, Constructor));
                b = Con(b, 10, Cod(Negro, Taller));
                b = Con(b, 11, Cod(Negro, Constructor));
                return b;
            default:
                throw new ArgumentException($"disposicion inicial desconocida: {nombre}");
        }
    }

    public static readonly string[] Disposiciones = { "esquinas", "frentes", "diagonal", "centro", "solo-taller" };

    // ---------------------------------------------------------------- texto

    private static readonly char[] Letra = { 'C', 'G', 'S', 'T', 'Q', 'I' };

    public static char Simbolo(int cod) {
        if (cod == Vacio) return '.';
        if (cod == Castillo) return '*';
        char l = Letra[Tipo(cod)];
        return Dueno(cod) == Blanco ? l : char.ToLowerInvariant(l);
    }

    public static string Dibujar(ulong b) {
        var sb = new StringBuilder();
        for (int f = 0; f < 4; f++) {
            sb.Append("    ");
            for (int c = 0; c < 4; c++) { sb.Append(Simbolo(En(b, f * 4 + c))); sb.Append(' '); }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string Casilla(int c) => $"{(char)('a' + c % 4)}{4 - c / 4}";

    private static readonly string[] NombreTipo = { "constructor", "guerrero", "sacerdote", "taller", "cuartel", "iglesia" };

    public string Describir(ulong b, int turno, int j) {
        int tipo = JTipo(j), desde = JDesde(j), hasta = JHasta(j), extra = JExtra(j);
        string quien = EsPieza(En(b, desde)) ? NombreTipo[Tipo(En(b, desde))] : "?";
        switch (tipo) {
            case MOVER: return $"{quien} {Casilla(desde)}-{Casilla(hasta)}";
            case MATAR: return $"{quien} {Casilla(desde)}x{Casilla(hasta)} mata {NombreTipo[Tipo(En(b, hasta))]}";
            case TOMAR: return $"{quien} {Casilla(desde)} toma {NombreTipo[Tipo(En(b, hasta))]} en {Casilla(hasta)}";
            case CONSTRUIR: return $"construye {NombreTipo[extra]} en {Casilla(hasta)}";
            case CORONAR: return $"CASTILLO DORADO en {Casilla(hasta)}";
            case DESPLEGAR: return $"despliega {NombreTipo[extra]} de {Casilla(desde)} a {Casilla(hasta)}";
            case CONVERTIR: return $"sacerdote {Casilla(desde)} convierte {NombreTipo[Tipo(En(b, hasta))]} en {Casilla(hasta)}";
            default: return "?";
        }
    }
}
