using System;
using System.Collections.Generic;

namespace ReySolver;

/// <summary>Generador xorshift64. Sirve para desempatar entre jugadas igual de buenas.</summary>
public struct Rng {
    private ulong _s;
    public Rng(ulong semilla) { _s = semilla == 0 ? 0x9E3779B97F4A7C15UL : semilla; }
    public ulong Siguiente() {
        _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
        return _s;
    }
    public int Hasta(int n) => n <= 1 ? 0 : (int)(Siguiente() % (ulong)n);
}

/// <summary>
/// Alfa-beta con tabla de transposicion. Dos modos:
///   Practico: evalua material en el horizonte. Sirve para jugar partidas.
///   Exacto:   horizonte cero, solo prueba victorias forzadas. "gana X" es real;
///             "no se" mezcla empate con "mas profundo que el limite".
/// </summary>
public sealed class Busqueda {
    public const int GANA = 1_000_000;

    private readonly Juego _g;
    private readonly int _bits;
    private readonly ulong[] _ttClave;
    private readonly int[] _ttValor;
    private readonly sbyte[] _ttProf;
    private readonly byte[] _ttTipo;    // 0 vacio, 1 exacto, 2 cota inferior, 3 cota superior

    private readonly Pos[] _camino = new Pos[1024];
    private int _caminoLen;

    /// <summary>
    /// Cuantas veces se corto una rama por repeticion. Un valor que salio de una
    /// repeticion depende del CAMINO recorrido, no solo de la posicion, asi que
    /// guardarlo en la tabla contamina cualquier otro contexto que llegue ahi.
    /// Contando antes y despues se sabe si el valor de un nodo se puede guardar.
    /// </summary>
    private int _repeticiones;

    public long Nodos;

    /// <summary>
    /// Cuantos plies extra puede estirarse la busqueda de quietud. 0 la apaga.
    /// Sin ella la evaluacion cae en medio de un intercambio y el resultado depende
    /// enormemente de si la profundidad es par o impar: medido sobre la misma
    /// configuracion, el reparto da 67.4% a 7 plies y 46.8% a 8, y los empates
    /// pasan de 1.8% a 26%. Ese salto es del buscador, no del juego.
    /// </summary>
    public int MaxQuieta = 0;

    /// <summary>
    /// Vacia la tabla antes de cada jugada. La tabla guarda valores que dependen del
    /// CAMINO -YaVista devuelve 0 por repeticion, y ese 0 se propaga al valor del padre,
    /// que si se guarda-, asi que reutilizarla en otro contexto contamina.
    /// </summary>
    public bool LimpiaCadaJugada = false;

    public Busqueda(Juego g, int bits = 22) {
        _g = g;
        _bits = bits;
        int n = 1 << bits;
        _ttClave = new ulong[n];
        _ttValor = new int[n];
        _ttProf = new sbyte[n];
        _ttTipo = new byte[n];
    }

    private int Indice(Pos p) => (int)(p.Clave() & (ulong)((1 << _bits) - 1));

    public void Limpiar() {
        Array.Clear(_ttTipo);
        Nodos = 0;
        _repeticiones = 0;
    }

    // ------------------------------------------------------------ evaluacion

    /// <summary>Material y posicion desde el punto de vista del que tiene que jugar.</summary>
    public int Evaluar(Pos p, int turno) {
        int presU = Juego.PresU(p.Un);
        int s = 0;
        int cc = Juego.CasillaCastillo(p.Ed);

        for (int d = 0; d < 2; d++) {
            int sg = d == turno ? 1 : -1;
            int ed = _g.Edificios(p, d);
            s += sg * 130 * ed;
            if (ed == 3) s += sg * 260;                       // habilitado a levantar el castillo
            for (int t = Juego.Constructor; t <= Juego.Sacerdote; t++)
                if (Juego.Hay(presU, d, t)) s += sg * 35;

            int rc = Juego.CasillaRey(p.Un, d);
            if (rc < 0) continue;

            if (cc >= 0) {
                // Con el castillo puesto lo unico que importa es la carrera hasta ahi.
                int dist = _g.DistRey[rc][cc];
                s += sg * (ed == 3 ? 520 - 90 * dist : 110 - 20 * dist);
            }

            // Un rey pegado a algo que lo puede matar es un rey que ya casi perdio.
            foreach (int a in _g.Ady[rc]) {
                int w = Juego.En(p.Un, a);
                if (w == 0 || Juego.DuenoU(w) == d) continue;
                if (Juego.TipoU(w) == Juego.Guerrero) s -= sg * 200;
            }
        }
        return s;
    }

    // ------------------------------------------------------------- practico

    /// <summary>Elige una jugada mirando prof plies. Entre las que empatan elige al azar.</summary>
    public int ElegirPractico(Pos p, int turno, int prof, ref Rng rng, List<Pos>? historia) {
        if (LimpiaCadaJugada) Array.Clear(_ttTipo);
        _caminoLen = 0;
        if (historia != null) foreach (Pos h in historia) _camino[_caminoLen++] = h;

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(p, turno, jugadas);
        if (n == 0) return -1;
        Ordenar(jugadas, n);

        int mejor = int.MinValue;
        Span<int> empatadas = stackalloc int[Juego.MaxJugadas];
        int ne = 0;

        for (int i = 0; i < n; i++) {
            Pos np = _g.Aplicar(p, turno, jugadas[i]);
            _camino[_caminoLen++] = np;
            int v = -Practico(np, 1 - turno, prof - 1, -GANA * 2, GANA * 2);
            _caminoLen--;
            if (v > mejor) { mejor = v; ne = 0; empatadas[ne++] = jugadas[i]; }
            else if (v == mejor) empatadas[ne++] = jugadas[i];
        }
        return empatadas[rng.Hasta(ne)];
    }

    private bool YaVista(Pos p) {
        int veces = 0;
        for (int i = 0; i < _caminoLen; i++) if (_camino[i] == p && ++veces >= 2) return true;
        return false;
    }

    private int Practico(Pos p, int turno, int prof, int alfa, int beta) {
        Nodos++;

        var fin = _g.Terminal(p, turno);
        if (fin.HasValue) {
            int signo = turno == Juego.Blanco ? 1 : -1;
            return (int)fin.Value.res * signo * (GANA - (100 - prof));
        }
        if (YaVista(p)) { _repeticiones++; return 0; }

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(p, turno, jugadas);
        if (n == 0) return -(GANA - (100 - prof));        // ahogado: pierde el que no puede jugar
        if (prof <= 0) return MaxQuieta > 0 ? Quieta(p, turno, alfa, beta, 0) : Evaluar(p, turno);

        ulong clave = p.Clave();
        int idx = Indice(p);
        if (_ttTipo[idx] != 0 && _ttClave[idx] == clave && _ttProf[idx] >= prof) {
            int v = _ttValor[idx];
            if (_ttTipo[idx] == 1) return v;
            if (_ttTipo[idx] == 2 && v > alfa) alfa = v;
            if (_ttTipo[idx] == 3 && v < beta) beta = v;
            if (alfa >= beta) return v;
        }

        Ordenar(jugadas, n);
        int alfa0 = alfa, mejor = int.MinValue;
        int repAntes = _repeticiones;
        for (int i = 0; i < n; i++) {
            Pos np = _g.Aplicar(p, turno, jugadas[i]);
            _camino[_caminoLen++] = np;
            int v = -Practico(np, 1 - turno, prof - 1, -beta, -alfa);
            _caminoLen--;
            if (v > mejor) mejor = v;
            if (mejor > alfa) alfa = mejor;
            if (alfa >= beta) break;
        }

        // Solo se guarda lo que vale para cualquier camino.
        if (_repeticiones == repAntes) {
            _ttClave[idx] = clave;
            _ttValor[idx] = mejor;
            _ttProf[idx] = (sbyte)Math.Min(prof, 127);
            _ttTipo[idx] = (byte)(mejor <= alfa0 ? 3 : mejor >= beta ? 2 : 1);
        }
        return mejor;
    }

    /// <summary>
    /// Valor practico de una posicion suelta, sin historia previa. Es la puerta para
    /// analizar una posicion a mano y ver que le da el motor a cada jugada.
    /// </summary>
    public int RaizPractica(Pos p, int turno, int prof) {
        _caminoLen = 0;
        _camino[_caminoLen++] = p;
        return Practico(p, turno, prof, -GANA * 2, GANA * 2);
    }

    // --------------------------------------------------------------- quietud

    /// <summary>
    /// Sigue solo las jugadas forzantes -matar, convertir, coronar- hasta que la
    /// posicion se calma. El que juega puede "plantarse": no esta obligado a seguir
    /// el intercambio, asi que el valor de quedarse quieto es la cota de abajo.
    ///
    /// Construir queda afuera a proposito: vale tanto como una captura pero esta
    /// disponible casi siempre, asi que incluirla convertiria esto en una busqueda
    /// completa sin horizonte.
    /// </summary>
    private int Quieta(Pos p, int turno, int alfa, int beta, int gastado) {
        Nodos++;

        var fin = _g.Terminal(p, turno);
        if (fin.HasValue) {
            int signo = turno == Juego.Blanco ? 1 : -1;
            return (int)fin.Value.res * signo * (GANA - (100 + gastado));
        }
        if (YaVista(p)) return 0;

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(p, turno, jugadas);
        if (n == 0) return -(GANA - (100 + gastado));

        int mejor = Evaluar(p, turno);
        if (mejor >= beta || gastado >= MaxQuieta) return mejor;
        if (mejor > alfa) alfa = mejor;

        Ordenar(jugadas, n);
        for (int i = 0; i < n; i++) {
            if (!Forzante(jugadas[i])) continue;
            Pos np = _g.Aplicar(p, turno, jugadas[i]);
            _camino[_caminoLen++] = np;
            int v = -Quieta(np, 1 - turno, -beta, -alfa, gastado + 1);
            _caminoLen--;
            if (v > mejor) mejor = v;
            if (mejor > alfa) alfa = mejor;
            if (alfa >= beta) break;
        }
        return mejor;
    }

    private static bool Forzante(int j) {
        // Solo intercambios de verdad. Coronar tambien es un salto grande de
        // evaluacion, pero NO es una captura: el rival no tiene con que contestarla
        // dentro de la quietud, asi que la linea termina con uno coronado y el otro
        // sin jugar. Incluirla devolvia justo el sesgo optimista que esto viene a
        // sacar, y de hecho lo empeoraba: el reparto daba 80.8% a 6 plies.
        int t = Juego.JTipo(j);
        return t == Juego.MATAR || t == Juego.CONVERTIR;
    }

    // --------------------------------------------------------------- exacto

    /// <summary>
    /// Busca victorias forzadas. Sin evaluacion: al llegar al horizonte devuelve 0, que aca
    /// significa "no se". Por eso un veredicto de victoria es real y uno de empate no.
    /// </summary>
    public int Exacto(Pos p, int turno, int prof, int alfa, int beta) {
        Nodos++;

        var fin = _g.Terminal(p, turno);
        if (fin.HasValue) {
            int signo = turno == Juego.Blanco ? 1 : -1;
            return (int)fin.Value.res * signo * (GANA - (100 - prof));
        }
        if (YaVista(p)) return 0;

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(p, turno, jugadas);
        if (n == 0) return -(GANA - (100 - prof));
        if (prof <= 0) return 0;

        Pos can = _g.Canonica(p);
        ulong clave = can.Clave();
        int idx = Indice(can);
        if (_ttTipo[idx] != 0 && _ttClave[idx] == clave && _ttProf[idx] >= prof) {
            int v = _ttValor[idx];
            if (_ttTipo[idx] == 1) return v;
            if (_ttTipo[idx] == 2 && v > alfa) alfa = v;
            if (_ttTipo[idx] == 3 && v < beta) beta = v;
            if (alfa >= beta) return v;
        }

        Ordenar(jugadas, n);
        int alfa0 = alfa, mejor = int.MinValue;
        for (int i = 0; i < n; i++) {
            Pos np = _g.Aplicar(p, turno, jugadas[i]);
            _camino[_caminoLen++] = np;
            int v = -Exacto(np, 1 - turno, prof - 1, -beta, -alfa);
            _caminoLen--;
            if (v > mejor) mejor = v;
            if (mejor > alfa) alfa = mejor;
            if (alfa >= beta) break;
        }

        _ttClave[idx] = clave;
        _ttValor[idx] = mejor;
        _ttProf[idx] = (sbyte)Math.Min(prof, 127);
        _ttTipo[idx] = (byte)(mejor <= alfa0 ? 3 : mejor >= beta ? 2 : 1);
        return mejor;
    }

    public int RaizExacta(Pos p, int turno, int prof) {
        _caminoLen = 0;
        _camino[_caminoLen++] = p;
        return Exacto(p, turno, prof, -GANA * 2, GANA * 2);
    }

    public static string Veredicto(int v, int turno) {
        if (v > GANA / 2) return turno == Juego.Blanco ? "gana el PRIMERO por la fuerza" : "gana el SEGUNDO por la fuerza";
        if (v < -GANA / 2) return turno == Juego.Blanco ? "gana el SEGUNDO por la fuerza" : "gana el PRIMERO por la fuerza";
        return "no se (empate o mas profundo que el limite)";
    }

    // ------------------------------------------------------------- ordenado

    private static void Ordenar(Span<int> jugadas, int n) {
        for (int i = 1; i < n; i++) {
            int j = jugadas[i], p = Prioridad(j), k = i - 1;
            while (k >= 0 && Prioridad(jugadas[k]) < p) { jugadas[k + 1] = jugadas[k]; k--; }
            jugadas[k + 1] = j;
        }
    }

    private static int Prioridad(int j) {
        switch (Juego.JTipo(j)) {
            case Juego.MATAR: return 5;
            case Juego.CORONAR: return 4;
            case Juego.CONVERTIR: return 3;
            case Juego.CONSTRUIR: return 2;
            case Juego.DESPLEGAR: return 1;
            default: return 0;
        }
    }
}
