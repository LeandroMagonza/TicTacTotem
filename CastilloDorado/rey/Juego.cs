using System;
using System.Collections.Generic;
using System.Text;

namespace ReySolver;

/// <summary>Resultado de una partida, siempre desde el punto de vista del BLANCO (el que arranca).</summary>
public enum Resultado { Negro = -1, Empate = 0, Blanco = 1 }

/// <summary>Por que termino la partida.</summary>
public enum Final {
    Castillo,     // un rey entro al castillo controlando los tres edificios
    ReyMuerto,    // le mataron el rey
    Ahogado,      // le tocaba jugar y no tenia ninguna jugada legal
    Repeticion,   // la misma posicion se repitio N veces
    Limite,       // se acabo el tope de plies
}

/// <summary>
/// Las lecturas que quedaron ambiguas en el enunciado del juego del rey. Cada una es un
/// interruptor porque el juego esta en diseño. Ver README-rey, seccion 2.
/// </summary>
public sealed class Reglas {
    /// <summary>
    /// El rey usa el poder de una unidad mientras esa unidad este PARADA SOBRE su edificio
    /// (ademas de cuando no esta en el tablero). Apagado, el rey pierde el poder apenas la
    /// unidad existe en el tablero, este donde este.
    /// </summary>
    public bool ReyGuarnicion = true;

    /// <summary>
    /// Tercera lectura del poder del rey: lo pierde por tener el EDIFICIO, no la unidad.
    /// Levantaste el taller, el rey no construye mas mientras controles un taller, muera
    /// quien muera. Es la unica que hace que el rey delegue de verdad, y ademas es la que
    /// cumple las dos frases: "ya el rey no puede construir mas" y "podes meter el rey en
    /// un castillo defendido si no tenes cuartel". Pisa a ReyGuarnicion.
    /// </summary>
    public bool ReyPorEdificio = false;

    /// <summary>
    /// Cuarta lectura, la mas estricta: el rey pierde el poder de una unidad si tiene la
    /// unidad O el edificio en su reino. Solo lo recupera cuando no le queda ninguno de los
    /// dos, o sea cuando le mataron la unidad Y le ocuparon el edificio. Pisa a las otras.
    /// </summary>
    public bool ReyReino = false;

    /// <summary>
    /// Solo el guerrero toma el control de un edificio parandose encima. Las demas unidades
    /// pueden pisarlo y estorbar, pero el edificio lo sigue mandando el que lo construyo.
    /// </summary>
    public bool ControlGuerrero = false;

    /// <summary>
    /// El guerrero carga: se mueve hasta DOS casillas en linea recta, atravesando una casilla
    /// vacia. Es lo unico que probe que hace mas rapido estorbar en vez de mas lenta la
    /// carrera, que es de donde sale la ventaja del que sale primero.
    /// </summary>
    public bool GuerreroVeloz = false;

    /// <summary>Vuelve la regla de que dos edificios no pueden estar pegados.</summary>
    public bool NoPegado = false;

    /// <summary>
    /// El sacerdote convierte aunque ya tengas esa pieza: en vez de aparecer una segunda, la
    /// tuya se muda a esa casilla. Le saca la pieza al otro y reposiciona la tuya de un saque.
    /// </summary>
    public bool SacerdoteReubica = false;

    /// <summary>
    /// Lo mismo, pero solo si tu pieza de ese tipo esta GUARNECIDA, o sea parada sobre un
    /// edificio de su tipo. Es el mismo poder con un precio: para tenerlo disponible hay que
    /// dejar la pieza en casa, sin usarla en el tablero.
    /// </summary>
    public bool SacerdoteReleva = false;

    /// <summary>El rey no puede matar nunca, aunque tenga el poder del guerrero.</summary>
    public bool ReyNoMata = false;

    /// <summary>
    /// El segundo arranca con el taller ya levantado y su constructor adentro. Es un tempo
    /// exacto de compensacion por la ventaja de salida, que en este juego es enorme.
    /// </summary>
    public bool Compensa = false;

    /// <summary>
    /// No alcanza con entrar al castillo: hay que seguir adentro cuando te vuelve a tocar
    /// jugar. Le da al rival exactamente un turno para desalojarte o para robarte un
    /// edificio, que es el tempo que en este juego decide la partida entera.
    /// </summary>
    public bool CastilloAguanta = false;

    /// <summary>Lado del tablero: 4 o 5. Ver Juego.Configurar.</summary>
    public int Lado = 4;

    /// <summary>
    /// El rey del segundo arranca una fila mas adelante que el del primero. Es una
    /// compensacion mucho mas chica que regalarle un edificio: vale media jugada, no una.
    /// </summary>
    public bool AdelantaSegundo = false;

    public int RepeticionesEmpate = 3;
    public int PliesMax = 300;
    public string Inicio = "esquinas";

    public Reglas Copia() => (Reglas)MemberwiseClone();

    public string Etiqueta() {
        var sb = new StringBuilder($"{Lado}x{Lado}/{Inicio}");
        if (ReyReino) sb.Append("+rey-reino");
        else if (ReyPorEdificio) sb.Append("+rey-por-edificio");
        else if (!ReyGuarnicion) sb.Append("+rey-pierde-poder");
        if (ControlGuerrero) sb.Append("+control-guerrero");
        if (GuerreroVeloz) sb.Append("+guerrero-veloz");
        if (NoPegado) sb.Append("+no-pegado");
        if (SacerdoteReubica) sb.Append("+sacerdote-reubica");
        if (SacerdoteReleva) sb.Append("+sacerdote-releva");
        if (ReyNoMata) sb.Append("+rey-no-mata");
        if (Compensa) sb.Append("+compensa");
        if (AdelantaSegundo) sb.Append("+adelanta-segundo");
        if (CastilloAguanta) sb.Append("+castillo-aguanta");
        return sb.ToString();
    }
}

/// <summary>
/// Una posicion son DOS planos de 4 bits por casilla: uno de edificios y uno de unidades.
/// Hacen falta los dos porque una unidad puede estar parada sobre un edificio, que es de lo
/// que se trata el juego. Cada plano es un UInt128 para que entre tambien el 5x5: 25
/// casillas por 4 bits son 100 bits, que en un ulong no entran.
/// </summary>
public readonly struct Pos : IEquatable<Pos> {
    public readonly UInt128 Ed;   // 0 vacio, 1-3 edificios del blanco, 4-6 del negro, 7 castillo
    public readonly UInt128 Un;   // 0 vacio, 1-4 unidades del blanco, 5-8 del negro

    public Pos(UInt128 ed, UInt128 un) { Ed = ed; Un = un; }

    public static readonly Pos Vacia = new Pos(UInt128.Zero, UInt128.Zero);

    public bool Equals(Pos o) => Ed == o.Ed && Un == o.Un;
    public override bool Equals(object? o) => o is Pos p && Equals(p);
    public static bool operator ==(Pos a, Pos b) => a.Equals(b);
    public static bool operator !=(Pos a, Pos b) => !a.Equals(b);
    public override int GetHashCode() => (int)Clave();

    private static ulong Mezclar(ulong x) {
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    /// <summary>
    /// Mezcla de los cuatro ulong que forman los dos planos. La tabla de transposicion
    /// guarda esta clave entera para verificar, asi que una colision de indice no ensucia
    /// el resultado; una colision de los 64 bits enteros es despreciable.
    /// </summary>
    public ulong Clave() {
        ulong h = Mezclar((ulong)Ed + 0x9E3779B97F4A7C15UL);
        h ^= Mezclar((ulong)(Ed >> 64) + 0x165667B19E3779F9UL);
        h = (h << 7) | (h >> 57);
        h ^= Mezclar((ulong)Un + 0xC2B2AE3D27D4EB4FUL);
        h ^= Mezclar((ulong)(Un >> 64) + 0x27D4EB2F165667C5UL);
        return Mezclar(h);
    }
}

/// <summary>
/// Reglas del juego del rey: el rey empieza solo, construye, y cada edificio le trae la
/// unidad que le corresponde. Se gana metiendo al rey en el castillo con los tres edificios
/// bajo control, o matandole el rey al otro.
/// </summary>
public sealed class Juego {
    /// <summary>
    /// Lado y casillas del tablero. Son estaticos y se fijan UNA vez por proceso con
    /// Configurar, antes de crear cualquier Juego y antes de arrancar los hilos: todo el
    /// proceso corre siempre un solo tamaño de tablero.
    /// </summary>
    public static int Lado { get; private set; } = 4;
    public static int Casillas { get; private set; } = 16;

    public static void Configurar(int lado) {
        if (lado != 4 && lado != 5) throw new ArgumentException("el tablero es 4x4 o 5x5");
        Lado = lado;
        Casillas = lado * lado;
    }

    public const int Blanco = 0;
    public const int Negro = 1;

    // Unidades. El rey no tiene edificio propio; las otras tres si.
    public const int Rey = 0, Constructor = 1, Guerrero = 2, Sacerdote = 3;
    // Edificios. El edificio de la unidad t (t>=1) es t-1, y al reves.
    public const int Taller = 0, Cuartel = 1, Iglesia = 2;
    public const int CastilloCod = 7;

    public const int MaxJugadas = 256;

    // Tipos de jugada
    public const int MOVER = 0, MATAR = 1, CONSTRUIR = 2, CORONAR = 3, DESPLEGAR = 4, CONVERTIR = 5;

    public readonly Reglas R;
    public readonly int[][] Ady;
    public readonly int[][] Simetrias = new int[8][];
    public readonly int[][] Dist;
    /// <summary>Por casilla y direccion, las hasta dos casillas en linea recta. Para la carga.</summary>
    public readonly int[][][] Rayos;

    public Juego(Reglas? reglas) {
        R = reglas ?? new Reglas();
        if (R.Lado != Lado)
            throw new InvalidOperationException(
                $"Juego.Configurar({R.Lado}) tiene que llamarse antes de crear el Juego");

        int L = Lado;
        Ady = new int[Casillas][];
        Dist = new int[Casillas][];

        for (int c = 0; c < Casillas; c++) {
            int f = c / L, col = c % L;
            var v = new List<int>(4);
            if (f > 0) v.Add(c - L);
            if (f < L - 1) v.Add(c + L);
            if (col > 0) v.Add(c - 1);
            if (col < L - 1) v.Add(c + 1);
            Ady[c] = v.ToArray();

            Dist[c] = new int[Casillas];
            for (int d = 0; d < Casillas; d++)
                Dist[c][d] = Math.Abs(f - d / L) + Math.Abs(col - d % L);
        }

        Rayos = new int[Casillas][][];
        var pasos = new[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
        for (int c = 0; c < Casillas; c++) {
            int f = c / L, col = c % L;
            var dirs = new List<int[]>(4);
            foreach (var (df, dc) in pasos) {
                var linea = new List<int>(2);
                for (int n = 1; n <= 2; n++) {
                    int nf = f + df * n, nc = col + dc * n;
                    if (nf < 0 || nf >= L || nc < 0 || nc >= L) break;
                    linea.Add(nf * L + nc);
                }
                if (linea.Count > 0) dirs.Add(linea.ToArray());
            }
            Rayos[c] = dirs.ToArray();
        }

        for (int k = 0; k < 8; k++) {
            var m = new int[Casillas];
            for (int c = 0; c < Casillas; c++) {
                int f = c / L, col = c % L;
                int nf = f, nc = col;
                if ((k & 4) != 0) { int t = nf; nf = nc; nc = t; }
                if ((k & 1) != 0) nf = L - 1 - nf;
                if ((k & 2) != 0) nc = L - 1 - nc;
                m[c] = nf * L + nc;
            }
            Simetrias[k] = m;
        }
    }

    // ---------------------------------------------------------------- codigos

    public static int CodU(int dueno, int tipo) => 1 + dueno * 4 + tipo;
    public static int TipoU(int cod) => (cod - 1) % 4;
    public static int DuenoU(int cod) => (cod - 1) / 4;

    public static int CodE(int dueno, int tipo) => 1 + dueno * 3 + tipo;
    public static int TipoE(int cod) => (cod - 1) % 3;
    public static int DuenoE(int cod) => (cod - 1) / 3;
    public static bool EsCastillo(int cod) => cod == CastilloCod;

    private static readonly UInt128 Nibble = 0xF;

    public static int En(UInt128 plano, int c) => (int)((plano >> (c * 4)) & Nibble);
    public static UInt128 Con(UInt128 plano, int c, int v) =>
        (plano & ~(Nibble << (c * 4))) | ((UInt128)(uint)v << (c * 4));

    /// <summary>Mascara de unidades presentes: bit i = hay una unidad de codigo i.</summary>
    public static int PresU(UInt128 un) {
        int m = 0;
        for (int c = 0; c < Casillas; c++) m |= 1 << En(un, c);
        return m;
    }

    /// <summary>Mascara de edificios LEVANTADOS, por quien los construyo (no por quien los controla).</summary>
    public static int PresE(UInt128 ed) {
        int m = 0;
        for (int c = 0; c < Casillas; c++) m |= 1 << En(ed, c);
        return m;
    }

    public static bool Hay(int presU, int dueno, int tipo) => (presU & (1 << CodU(dueno, tipo))) != 0;
    public static bool Levantado(int presE, int dueno, int tipo) => (presE & (1 << CodE(dueno, tipo))) != 0;
    public static bool HayCastillo(UInt128 ed) => (PresE(ed) & (1 << CastilloCod)) != 0;

    public static int CasillaDe(UInt128 plano, int cod) {
        for (int c = 0; c < Casillas; c++) if (En(plano, c) == cod) return c;
        return -1;
    }

    public static int CasillaCastillo(UInt128 ed) => CasillaDe(ed, CastilloCod);
    public static int CasillaRey(UInt128 un, int dueno) => CasillaDe(un, CodU(dueno, Rey));

    // ---------------------------------------------------------------- control

    /// <summary>
    /// Quien controla el edificio de esa casilla, o -1 si no hay edificio o no lo controla
    /// nadie. Manda quien lo ocupa; si esta vacio, manda quien lo construyo. El castillo no
    /// lo construye nadie, asi que vacio no es de nadie.
    /// </summary>
    public int Controla(Pos p, int c) {
        int e = En(p.Ed, c);
        if (e == 0) return -1;
        int u = En(p.Un, c);
        // Con ControlGuerrero solo el guerrero da vuelta el control: las demas unidades
        // pisan el edificio y estorban, pero no lo mandan.
        if (u != 0 && (!R.ControlGuerrero || TipoU(u) == Guerrero)) return DuenoU(u);
        return EsCastillo(e) ? -1 : DuenoE(e);
    }

    public bool ControlaTipo(Pos p, int dueno, int tipoEd) {
        for (int c = 0; c < Casillas; c++) {
            int e = En(p.Ed, c);
            if (e == 0 || EsCastillo(e) || TipoE(e) != tipoEd) continue;
            if (Controla(p, c) == dueno) return true;
        }
        return false;
    }

    /// <summary>Cuantos TIPOS distintos de edificio controla. Es lo que habilita el castillo.</summary>
    public int Edificios(Pos p, int dueno) {
        int n = 0;
        for (int t = Taller; t <= Iglesia; t++) if (ControlaTipo(p, dueno, t)) n++;
        return n;
    }

    // --------------------------------------------------------- poderes del rey

    /// <summary>
    /// El rey puede usar el poder de la unidad t si esa unidad no anda suelta por el tablero:
    /// o esta fuera (muerta o sin edificio todavia), o esta guarnecida sobre un edificio de su
    /// tipo. Con ReyGuarnicion apagado solo vale el primer caso.
    /// </summary>
    /// <summary>
    /// La unidad t de ese bando esta parada sobre un edificio de su propio tipo. Sirve para
    /// el poder prestado del rey y para el releve del sacerdote.
    /// </summary>
    public static bool Guarnecida(Pos p, int dueno, int t) {
        if (t < Constructor || t > Sacerdote) return false;
        int c = CasillaDe(p.Un, CodU(dueno, t));
        if (c < 0) return false;
        int e = En(p.Ed, c);
        return e != 0 && !EsCastillo(e) && TipoE(e) == t - 1;
    }

    public bool PoderDelRey(Pos p, int presU, int turno, int t) {
        // Por edificio: el rey conserva el poder solo mientras no controle el edificio que
        // lo delega. Matarle la unidad al otro ya no le devuelve el poder al rey.
        // Reino: hace falta no tener NINGUNO de los dos. Que te conviertan la unidad no le
        // devuelve el poder al rey si todavia controlas el edificio, porque desplegas otra.
        if (R.ReyReino) return !Hay(presU, turno, t) && !ControlaTipo(p, turno, t - 1);
        if (R.ReyPorEdificio) return !ControlaTipo(p, turno, t - 1);
        if (!Hay(presU, turno, t)) return true;
        if (!R.ReyGuarnicion) return false;
        return Guarnecida(p, turno, t);
    }

    /// <summary>El sacerdote muda la pieza propia en vez de traerla de afuera.</summary>
    public bool SacerdoteMuda => R.SacerdoteReubica || R.SacerdoteReleva;

    // --------------------------------------------------------------- jugadas

    // 3 bits de tipo, 5 de origen, 5 de destino, 4 de extra: entran las 25 casillas del 5x5.
    public static int Jug(int tipo, int desde, int hasta, int extra) =>
        tipo | (desde << 3) | (hasta << 8) | (extra << 13);

    public static int JTipo(int j) => j & 7;
    public static int JDesde(int j) => (j >> 3) & 31;
    public static int JHasta(int j) => (j >> 8) & 31;
    public static int JExtra(int j) => (j >> 13) & 15;

    public bool SitioDeObra(Pos p, int c) {
        if (En(p.Un, c) != 0 || En(p.Ed, c) != 0) return false;
        if (!R.NoPegado) return true;
        foreach (int a in Ady[c]) if (En(p.Ed, a) != 0) return false;
        return true;
    }

    public int Jugadas(Pos p, int turno, Span<int> buf) {
        int n = 0;
        int presU = PresU(p.Un), presE = PresE(p.Ed);
        bool tresEd = Edificios(p, turno) == 3;
        bool hayCastillo = HayCastillo(p.Ed);

        bool reyConstruye = PoderDelRey(p, presU, turno, Constructor);
        bool reyMata = !R.ReyNoMata && PoderDelRey(p, presU, turno, Guerrero);
        bool reyConvierte = PoderDelRey(p, presU, turno, Sacerdote);

        for (int c = 0; c < Casillas; c++) {
            // Punto de aparicion: un edificio propio vacio del que puede salir su unidad.
            int e = En(p.Ed, c);
            if (e != 0 && !EsCastillo(e) && DuenoE(e) == turno && En(p.Un, c) == 0) {
                int tu = TipoE(e) + 1;
                if (!Hay(presU, turno, tu)) buf[n++] = Jug(DESPLEGAR, c, c, tu);
            }

            int u = En(p.Un, c);
            if (u == 0 || DuenoU(u) != turno) continue;
            int t = TipoU(u);
            bool esRey = t == Rey;

            bool puedeMatar = t == Guerrero || (esRey && reyMata);
            bool puedeConstruir = t == Constructor || (esRey && reyConstruye);
            bool puedeConvertir = t == Sacerdote || (esRey && reyConvierte);

            // La carga del guerrero: hasta dos casillas en linea recta, atravesando vacio.
            if (R.GuerreroVeloz && t == Guerrero) {
                foreach (int[] linea in Rayos[c]) {
                    if (linea.Length < 2) continue;
                    if (En(p.Un, linea[0]) != 0) continue;   // la primera tiene que estar libre
                    int destino = linea[1], wd = En(p.Un, destino);
                    if (wd == 0) buf[n++] = Jug(MOVER, c, destino, 0);
                    else if (DuenoU(wd) != turno) buf[n++] = Jug(MATAR, c, destino, 0);
                }
            }

            foreach (int a in Ady[c]) {
                int w = En(p.Un, a);
                if (w == 0) {
                    // Los edificios son terreno: cualquiera entra a uno que no tenga nadie
                    // adentro, sea de quien sea, y ocuparlo es controlarlo.
                    buf[n++] = Jug(MOVER, c, a, 0);
                    continue;
                }
                if (DuenoU(w) == turno) continue;
                int tw = TipoU(w);
                // A un edificio ocupado (y a una unidad suelta) solo se entra matando.
                if (puedeMatar) buf[n++] = Jug(MATAR, c, a, 0);
                if (puedeConvertir && tw != Rey &&
                    (!Hay(presU, turno, tw) || R.SacerdoteReubica ||
                     (R.SacerdoteReleva && Guarnecida(p, turno, tw))))
                    buf[n++] = Jug(CONVERTIR, c, a, 0);
            }

            if (puedeConstruir) {
                foreach (int a in Ady[c]) {
                    if (!SitioDeObra(p, a)) continue;
                    for (int b = Taller; b <= Iglesia; b++)
                        if (!Levantado(presE, turno, b)) buf[n++] = Jug(CONSTRUIR, c, a, b);
                    if (tresEd && !hayCastillo) buf[n++] = Jug(CORONAR, c, a, 0);
                }
            }
        }
        return n;
    }

    public Pos Aplicar(Pos p, int turno, int j) {
        int tipo = JTipo(j), desde = JDesde(j), hasta = JHasta(j), extra = JExtra(j);
        UInt128 ed = p.Ed, un = p.Un;
        switch (tipo) {
            case MOVER:
            case MATAR: {
                int v = En(un, desde);
                un = Con(Con(un, desde, 0), hasta, v);
                return new Pos(ed, un);
            }
            case CONSTRUIR: {
                // El edificio viene con su unidad adentro, si esa unidad no estaba ya en el
                // tablero (puede estarlo si el sacerdote se la robo al otro).
                ed = Con(ed, hasta, CodE(turno, extra));
                int tu = extra + 1;
                if (!Hay(PresU(un), turno, tu)) un = Con(un, hasta, CodU(turno, tu));
                return new Pos(ed, un);
            }
            case CORONAR:
                return new Pos(Con(ed, hasta, CastilloCod), un);
            case DESPLEGAR:
                return new Pos(ed, Con(un, hasta, CodU(turno, extra)));
            case CONVERTIR: {
                int mio = CodU(turno, TipoU(En(un, hasta)));
                un = Con(un, hasta, mio);
                if (SacerdoteMuda)
                    for (int c = 0; c < Casillas; c++)
                        if (c != hasta && En(un, c) == mio) { un = Con(un, c, 0); break; }
                return new Pos(ed, un);
            }
            default:
                throw new InvalidOperationException("tipo de jugada desconocido");
        }
    }

    // -------------------------------------------------------------- finales

    /// <summary>
    /// Estado terminal mirado por el que TIENE que jugar. Devuelve null si la partida sigue.
    /// No mira ahogado: eso lo decide quien genere las jugadas.
    /// </summary>
    public (Resultado res, Final fin)? Terminal(Pos p, int turno) {
        int presU = PresU(p.Un);
        bool rb = Hay(presU, Blanco, Rey), rn = Hay(presU, Negro, Rey);
        if (!rb && !rn) return (Resultado.Empate, Final.ReyMuerto);
        if (!rb) return (Resultado.Negro, Final.ReyMuerto);
        if (!rn) return (Resultado.Blanco, Final.ReyMuerto);

        int cc = CasillaCastillo(p.Ed);
        if (cc >= 0) {
            int u = En(p.Un, cc);
            if (u != 0 && TipoU(u) == Rey) {
                int d = DuenoU(u);
                // Con CastilloAguanta la victoria se cobra recien cuando le vuelve a tocar
                // jugar al que esta adentro, o sea despues de la respuesta del rival.
                if ((!R.CastilloAguanta || turno == d) && Edificios(p, d) == 3)
                    return (d == Blanco ? Resultado.Blanco : Resultado.Negro, Final.Castillo);
            }
        }
        return null;
    }

    // ------------------------------------------------------------ simetrias

    public Pos Transformar(Pos p, int k) {
        int[] m = Simetrias[k];
        UInt128 ed = UInt128.Zero, un = UInt128.Zero;
        for (int c = 0; c < Casillas; c++) {
            int e = En(p.Ed, c), u = En(p.Un, c);
            if (e != 0) ed |= (UInt128)(uint)e << (m[c] * 4);
            if (u != 0) un |= (UInt128)(uint)u << (m[c] * 4);
        }
        return new Pos(ed, un);
    }

    public Pos Canonica(Pos p) {
        Pos mejor = p;
        for (int k = 1; k < 8; k++) {
            Pos t = Transformar(p, k);
            if (t.Ed < mejor.Ed || (t.Ed == mejor.Ed && t.Un < mejor.Un)) mejor = t;
        }
        return mejor;
    }

    // -------------------------------------------------------------- inicial

    /// <summary>
    /// Cada uno arranca con el rey solo y nada mas: no hay edificios en el tablero. La
    /// primera jugada obligada de los dos es levantar algo. Las disposiciones son simetricas
    /// por giro de 180 grados, asi que ninguna le da ventaja posicional a nadie.
    /// </summary>
    public Pos Inicial() {
        Pos p = Inicial(R.Inicio);

        if (R.AdelantaSegundo) {
            // El rey negro sube una fila hacia el centro, si hay lugar y esta libre.
            int rn = CasillaRey(p.Un, Negro);
            int destino = rn - Lado;
            if (destino >= 0 && En(p.Un, destino) == 0)
                p = new Pos(p.Ed, Con(Con(p.Un, rn, 0), destino, CodU(Negro, Rey)));
        }

        if (!R.Compensa) return p;
        // El taller regalado va pegado al rey negro, en la primera casilla libre.
        int rey = CasillaRey(p.Un, Negro);
        foreach (int a in Ady[rey])
            if (En(p.Un, a) == 0 && En(p.Ed, a) == 0)
                return new Pos(Con(p.Ed, a, CodE(Negro, Taller)), Con(p.Un, a, CodU(Negro, Constructor)));
        return p;
    }

    public static Pos Inicial(string nombre) {
        // Cada casilla y su opuesta por giro de 180 grados: c y Casillas-1-c.
        int donde = nombre switch {
            "esquinas"    => 0,                    // esquina contra esquina
            "frentes"     => Lado / 2,             // en el medio de la fila del fondo
            "adelantados" => Lado + Lado / 2,      // en el medio, pero una fila adentro
            "lados"       => Lado,                 // en el medio de la columna de la izquierda
            "centro"      => Lado + 1,             // pegado al centro, en diagonal
            _ => throw new ArgumentException($"disposicion inicial desconocida: {nombre}"),
        };
        UInt128 un = Con(Con(UInt128.Zero, donde, CodU(Blanco, Rey)),
                         Casillas - 1 - donde, CodU(Negro, Rey));
        return new Pos(UInt128.Zero, un);
    }

    public static readonly string[] Disposiciones = { "esquinas", "frentes", "adelantados", "lados", "centro" };

    // ---------------------------------------------------------------- texto

    private static readonly char[] LetraU = { 'R', 'C', 'G', 'S' };
    private static readonly char[] LetraE = { 'T', 'Q', 'I' };

    public static char SimboloU(int cod) {
        if (cod == 0) return '.';
        char l = LetraU[TipoU(cod)];
        return DuenoU(cod) == Blanco ? l : char.ToLowerInvariant(l);
    }

    public static char SimboloE(int cod) {
        if (cod == 0) return '.';
        if (EsCastillo(cod)) return '*';
        char l = LetraE[TipoE(cod)];
        return DuenoE(cod) == Blanco ? l : char.ToLowerInvariant(l);
    }

    /// <summary>Las 16 casillas en 32 caracteres: edificio y unidad de cada una.</summary>
    public static string Linea(Pos p) {
        var sb = new StringBuilder(Casillas * 2);
        for (int c = 0; c < Casillas; c++) { sb.Append(SimboloE(En(p.Ed, c))); sb.Append(SimboloU(En(p.Un, c))); }
        return sb.ToString();
    }

    public static string Dibujar(Pos p) {
        var sb = new StringBuilder();
        for (int f = 0; f < Lado; f++) {
            sb.Append("    ");
            for (int c = 0; c < Lado; c++) {
                int i = f * Lado + c;
                sb.Append(SimboloE(En(p.Ed, i)));
                sb.Append(SimboloU(En(p.Un, i)));
                sb.Append(' ');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static string Casilla(int c) => $"{(char)('a' + c % Lado)}{Lado - c / Lado}";

    public static readonly string[] NombreJugada = { "mover", "matar", "construir", "coronar", "desplegar", "convertir" };
    private static readonly string[] NombreU = { "rey", "constructor", "guerrero", "sacerdote" };
    private static readonly string[] NombreE = { "taller", "cuartel", "iglesia" };

    public string Describir(Pos p, int turno, int j) {
        int tipo = JTipo(j), desde = JDesde(j), hasta = JHasta(j), extra = JExtra(j);
        int u = En(p.Un, desde);
        string quien = u != 0 ? NombreU[TipoU(u)] : "?";
        switch (tipo) {
            case MOVER: {
                int e = En(p.Ed, hasta);
                string donde = e == 0 ? Casilla(hasta)
                    : EsCastillo(e) ? $"el CASTILLO en {Casilla(hasta)}"
                    : $"{NombreE[TipoE(e)]} en {Casilla(hasta)}";
                return $"{quien} {Casilla(desde)}-{donde}";
            }
            case MATAR:
                return $"{quien} {Casilla(desde)}x{Casilla(hasta)} mata {NombreU[TipoU(En(p.Un, hasta))]}";
            case CONSTRUIR:
                return $"{quien} construye {NombreE[extra]} en {Casilla(hasta)}";
            case CORONAR:
                return $"{quien} levanta el CASTILLO en {Casilla(hasta)}";
            case DESPLEGAR:
                return $"sale el {NombreU[extra]} en {Casilla(hasta)}";
            case CONVERTIR:
                return $"{quien} {Casilla(desde)} convierte {NombreU[TipoU(En(p.Un, hasta))]} en {Casilla(hasta)}";
            default: return "?";
        }
    }
}
