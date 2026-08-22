using System;
using System.Collections.Generic;

namespace CastilloSolver;

/// <summary>Generador barato y reproducible: la misma semilla da siempre la misma partida.</summary>
public struct Rng {
    private ulong _s;
    public Rng(ulong semilla) { _s = semilla == 0 ? 0x9E3779B97F4A7C15UL : semilla; }
    public ulong Next() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return _s; }
    public int Entre(int n) => (int)(Next() % (ulong)n);
}

/// <summary>
/// Dos busquedas sobre el mismo generador de jugadas.
///
/// Practico: alfa-beta con una evaluacion de material, que es como juega alguien que mira
/// N plies y despues estima. Sirve para jugar partidas, no prueba nada.
///
/// Exacto: alfa-beta donde el horizonte vale 0 y solo existen ganar / perder / no se.
/// Un "gana blanco" ahi si es una victoria forzada real dentro del limite de plies.
/// </summary>
public sealed class Busqueda {
    private const int GANA = 1_000_000;

    private readonly Juego _g;
    private readonly ulong[] _camino = new ulong[1024];
    private int _caminoLen;

    // Tabla de transposicion: clave canonica, valor, profundidad, tipo de corte.
    private readonly ulong[] _ttClave;
    private readonly int[] _ttValor;
    private readonly sbyte[] _ttProf;
    private readonly byte[] _ttTipo;   // 0 exacto, 1 cota inferior, 2 cota superior
    private readonly int _ttMask;

    public long Nodos;
    public long Cortes;

    public Busqueda(Juego g, int ttBits = 22) {
        _g = g;
        int n = 1 << ttBits;
        _ttClave = new ulong[n];
        _ttValor = new int[n];
        _ttProf = new sbyte[n];
        _ttTipo = new byte[n];
        _ttMask = n - 1;
    }

    public void Limpiar() {
        Array.Clear(_ttClave); Array.Clear(_ttValor); Array.Clear(_ttProf); Array.Clear(_ttTipo);
        Nodos = 0; Cortes = 0;
    }

    private static int Indice(ulong clave, int mask) {
        ulong h = clave * 0x9E3779B97F4A7C15UL;
        return (int)((h >> 40) & (ulong)mask);
    }

    // ------------------------------------------------------------ evaluacion

    /// <summary>Material desde el punto de vista del que tiene que jugar.</summary>
    public int Evaluar(ulong b, int turno) {
        int pres = Juego.Presentes(b);
        int s = 0;
        for (int d = 0; d < 2; d++) {
            int sg = d == turno ? 1 : -1;
            int ed = Juego.Edificios(pres, d);
            s += sg * 120 * ed;
            if (ed == 3) s += sg * 300;                                   // a una jugada de coronar
            s += sg * 40 * Juego.Unidades(pres, d);
            if (Juego.Hay(pres, d, Juego.Taller)) s += sg * 40;           // el taller es la vida
            if (Juego.Hay(pres, d, Juego.Constructor)) s += sg * 30;
        }
        // El reclamo del castillo vale como un cuarto edificio, y mucho mas con los tres.
        if (_g.R.CastilloClaim) {
            int r = Juego.ReclamoCastillo(pres);
            if (r >= 0) {
                int sg = r == turno ? 1 : -1;
                s += sg * (120 + (Juego.Edificios(pres, r) == 3 ? 400 : 0));
            }
        }
        // Un constructor sin donde construir no vale nada: contar los sitios de obra que tiene.
        for (int c = 0; c < Juego.Casillas; c++) {
            int v = Juego.En(b, c);
            if (!Juego.EsPieza(v) || Juego.Tipo(v) != Juego.Constructor) continue;
            int sg = Juego.Dueno(v) == turno ? 1 : -1;
            int sitios = 0;
            foreach (int a in _g.Ady[c]) if (_g.SitioDeObra(b, a)) sitios++;
            s += sg * 12 * sitios;
        }
        return s;
    }

    // ------------------------------------------------------------- practico

    /// <summary>Elige una jugada mirando prof plies. Entre las que empatan elige al azar.</summary>
    public int ElegirPractico(ulong b, int turno, int prof, ref Rng rng, List<ulong> historia) {
        _caminoLen = 0;
        if (historia != null) foreach (ulong h in historia) _camino[_caminoLen++] = h;

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(b, turno, jugadas);
        if (n == 0) return -1;

        int mejor = int.MinValue;
        Span<int> empatadas = stackalloc int[Juego.MaxJugadas];
        int ne = 0;

        for (int i = 0; i < n; i++) {
            ulong nb = _g.Aplicar(b, turno, jugadas[i]);
            int v = -Practico(nb, 1 - turno, prof - 1, -GANA * 2, GANA * 2, 1);
            if (v > mejor) { mejor = v; ne = 0; empatadas[ne++] = jugadas[i]; }
            else if (v == mejor && ne < Juego.MaxJugadas) empatadas[ne++] = jugadas[i];
        }
        return empatadas[rng.Entre(ne)];
    }

    private int Practico(ulong b, int turno, int prof, int alfa, int beta, int ply) {
        Nodos++;

        var t = _g.Terminal(b, turno);
        if (t != null) {
            if (t.Value.res == Resultado.Empate) return 0;
            bool gano = (t.Value.res == Resultado.Blanco) == (turno == Juego.Blanco);
            return gano ? GANA - ply : -GANA + ply;
        }

        // Repeticion en el camino: cuenta como tablas.
        for (int i = 0; i < _caminoLen; i++) if (_camino[i] == b) return 0;

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(b, turno, jugadas);
        if (n == 0) return -GANA + ply;             // ahogado: pierde el que no puede jugar
        if (prof <= 0) return Evaluar(b, turno);

        ulong clave = _g.Canonica(b) ^ (turno == Juego.Negro ? 0xD1B54A32D192ED03UL : 0UL);
        int idx = Indice(clave, _ttMask);
        if (_ttClave[idx] == clave && _ttProf[idx] >= prof) {
            int v = _ttValor[idx];
            if (_ttTipo[idx] == 0) return v;
            if (_ttTipo[idx] == 1 && v > alfa) alfa = v;
            else if (_ttTipo[idx] == 2 && v < beta) beta = v;
            if (alfa >= beta) { Cortes++; return v; }
        }

        Ordenar(jugadas, n);

        int alfa0 = alfa, mejor = -GANA * 2;
        _camino[_caminoLen++] = b;
        for (int i = 0; i < n; i++) {
            ulong nb = _g.Aplicar(b, turno, jugadas[i]);
            int v = -Practico(nb, 1 - turno, prof - 1, -beta, -alfa, ply + 1);
            if (v > mejor) mejor = v;
            if (mejor > alfa) alfa = mejor;
            if (alfa >= beta) { Cortes++; break; }
        }
        _caminoLen--;

        _ttClave[idx] = clave;
        _ttValor[idx] = mejor;
        _ttProf[idx] = (sbyte)Math.Min(prof, 127);
        _ttTipo[idx] = (byte)(mejor <= alfa0 ? 2 : mejor >= beta ? 1 : 0);
        return mejor;
    }

    /// <summary>Primero lo que decide: coronar, tomar, matar, construir. Despues el resto.</summary>
    private static void Ordenar(Span<int> jugadas, int n) {
        for (int i = 1; i < n; i++) {
            int j = jugadas[i], p = Prioridad(j), k = i - 1;
            while (k >= 0 && Prioridad(jugadas[k]) < p) { jugadas[k + 1] = jugadas[k]; k--; }
            jugadas[k + 1] = j;
        }
    }

    private static int Prioridad(int j) {
        switch (Juego.JTipo(j)) {
            case Juego.ENTRAR: return 7;
            case Juego.CORONAR: return 6;
            case Juego.TOMAR: return 5;
            case Juego.CONVERTIR: return 4;
            case Juego.MATAR: return 3;
            case Juego.CONSTRUIR: return 2;
            case Juego.DESPLEGAR: return 1;
            default: return 0;
        }
    }

    // --------------------------------------------------------------- exacto

    /// <summary>
    /// Busca victorias forzadas dentro de prof plies. El horizonte vale 0, asi que el
    /// resultado solo distingue "gana en k" de "no se". Un veredicto de victoria es real.
    /// </summary>
    public int Exacto(ulong b, int turno, int prof, int alfa, int beta, int ply) {
        Nodos++;

        var t = _g.Terminal(b, turno);
        if (t != null) {
            if (t.Value.res == Resultado.Empate) return 0;
            bool gano = (t.Value.res == Resultado.Blanco) == (turno == Juego.Blanco);
            return gano ? GANA - ply : -GANA + ply;
        }

        for (int i = 0; i < _caminoLen; i++) if (_camino[i] == b) return 0;

        Span<int> jugadas = stackalloc int[Juego.MaxJugadas];
        int n = _g.Jugadas(b, turno, jugadas);
        if (n == 0) return -GANA + ply;
        if (prof <= 0) return 0;

        ulong clave = _g.Canonica(b) ^ (turno == Juego.Negro ? 0xD1B54A32D192ED03UL : 0UL);
        int idx = Indice(clave, _ttMask);
        if (_ttClave[idx] == clave && _ttProf[idx] >= prof) {
            int v = _ttValor[idx];
            if (_ttTipo[idx] == 0) return v;
            if (_ttTipo[idx] == 1 && v > alfa) alfa = v;
            else if (_ttTipo[idx] == 2 && v < beta) beta = v;
            if (alfa >= beta) { Cortes++; return v; }
        }

        Ordenar(jugadas, n);

        int alfa0 = alfa, mejor = -GANA * 2;
        _camino[_caminoLen++] = b;
        for (int i = 0; i < n; i++) {
            ulong nb = _g.Aplicar(b, turno, jugadas[i]);
            int v = -Exacto(nb, 1 - turno, prof - 1, -beta, -alfa, ply + 1);
            if (v > mejor) mejor = v;
            if (mejor > alfa) alfa = mejor;
            if (alfa >= beta) { Cortes++; break; }
        }
        _caminoLen--;

        _ttClave[idx] = clave;
        _ttValor[idx] = mejor;
        _ttProf[idx] = (sbyte)Math.Min(prof, 127);
        _ttTipo[idx] = (byte)(mejor <= alfa0 ? 2 : mejor >= beta ? 1 : 0);
        return mejor;
    }

    public int RaizExacta(ulong b, int turno, int prof) {
        _caminoLen = 0;
        return Exacto(b, turno, prof, -GANA * 2, GANA * 2, 0);
    }

    public static string Veredicto(int v, int turno) {
        if (v >= GANA - 1000) return $"gana {(turno == Juego.Blanco ? "BLANCO" : "NEGRO")} en {GANA - v} plies";
        if (v <= -GANA + 1000) return $"gana {(turno == Juego.Blanco ? "NEGRO" : "BLANCO")} en {GANA + v} plies";
        return "no se (empate o mas profundo que el limite)";
    }
}
