using System;
using System.Diagnostics;

namespace TatetiSolver;

public sealed class SearchAborted : Exception { }

/// <summary>
/// Alpha-beta exacto sobre los tres valores posibles (gana blanco / tablas / gana negro),
/// con tabla de transposicion y repeticion = tablas.
///
/// Sobre la solidez del resultado:
///  - La tabla se indexa por (posicion canonica, turno, plies restantes) con coincidencia
///    exacta de profundidad. Como cada jugada consume exactamente un ply, una posicion
///    alcanzada a distinta altura del arbol nunca reusa una entrada de otra altura.
///  - La regla de repeticion depende del camino recorrido, no solo de la posicion (el
///    problema clasico de Graph History Interaction). Por eso todo nodo cuyo valor haya
///    sido influido por un corte por repeticion queda marcado como "contaminado" y no se
///    guarda en la tabla. Lo que se guarda vale para cualquier camino.
///  - Un veredicto "gana X" siempre es una victoria forzada real dentro del limite de
///    plies. Un veredicto "tablas" significa "no hay victoria forzada dentro del limite",
///    que es una afirmacion mas debil y se reporta como tal.
/// </summary>
public sealed class Searcher {
    private struct TtEntry {
        public ulong Key;
        public sbyte Value;
        public sbyte Flag;    // 0 exacto, 1 cota inferior, 2 cota superior
        public byte Depth;
        public byte Turn;
        public bool Used;
    }

    private const int MaxPly = 64;
    private const int MaxMoves = 128;

    private readonly GameSpec _spec;
    private readonly TtEntry[] _tt;
    private readonly ulong _mask;
    private readonly bool _useRepetition;

    private readonly ulong[] _path = new ulong[MaxPly + 2];
    private readonly int[] _pathTurn = new int[MaxPly + 2];
    private int _pathLen;

    private readonly int[][] _moveBuf;
    private readonly int[][] _quietBuf;

    private readonly Stopwatch _clock = new();
    private long _budgetMs;
    private long _tick;

    public long Nodes;
    public long TtHits;
    public long Stalemates;

    public Searcher(GameSpec spec, int ttBits = 24, bool useRepetition = true) {
        _spec = spec;
        _useRepetition = useRepetition;
        int size = 1 << ttBits;
        _tt = new TtEntry[size];
        _mask = (ulong)(size - 1);
        _moveBuf = new int[MaxPly + 2][];
        _quietBuf = new int[MaxPly + 2][];
        for (int i = 0; i < _moveBuf.Length; i++) {
            _moveBuf[i] = new int[MaxMoves];
            _quietBuf[i] = new int[MaxMoves];
        }
    }

    private ulong Index(ulong key, int turn, int depth) {
        ulong h = key * 0x9E3779B97F4A7C15UL;
        h ^= (ulong)depth * 0x632BE59BD9B4E019UL;
        h ^= (ulong)turn * 0xD6E8FEB86659FD93UL;
        h ^= h >> 29;
        return h & _mask;
    }

    /// <summary>Resuelve la posicion inicial con un limite de plies. Lanza SearchAborted si se agota el tiempo.</summary>
    public Outcome Solve(int maxDepth, long budgetMs = 0) {
        _budgetMs = budgetMs;
        _tick = 0;
        _clock.Restart();
        _pathLen = 0;
        ulong start = _spec.InitialPosition();
        int v = Search(start, GameSpec.White, maxDepth, -1, 1, out _);
        return (Outcome)v;
    }

    private int Search(ulong p, int turn, int depth, int alpha, int beta, out bool tainted) {
        tainted = false;
        Nodes++;

        if (_budgetMs > 0 && (++_tick & 0xFFFF) == 0 && _clock.ElapsedMilliseconds > _budgetMs)
            throw new SearchAborted();

        if (depth <= 0) return 0;

        ulong key = _spec.Canonical(p);

        // Repeticion en el camino actual: tablas. Contamina el nodo.
        if (_useRepetition) {
            for (int i = 0; i < _pathLen; i++) {
                if (_path[i] == key && _pathTurn[i] == turn) { tainted = true; return 0; }
            }
        }

        ulong slot = Index(key, turn, depth);
        ref TtEntry e = ref _tt[slot];
        if (e.Used && e.Key == key && e.Depth == (byte)depth && e.Turn == (byte)turn) {
            TtHits++;
            if (e.Flag == 0) return e.Value;
            if (e.Flag == 1 && e.Value >= beta) return e.Value;
            if (e.Flag == 2 && e.Value <= alpha) return e.Value;
        }

        int[] moves = _moveBuf[_pathLen];
        int n = _spec.GenerateMoves(p, turn, moves.AsSpan());
        if (n == 0) {
            // Ahogado: el que no tiene jugada legal pierde.
            Stalemates++;
            return turn == GameSpec.White ? -1 : 1;
        }

        bool maximizing = turn == GameSpec.White;
        int winValue = maximizing ? 1 : -1;
        int lossValue = -winValue;

        // Primera pasada: si alguna jugada gana ya, no hace falta buscar nada mas.
        // El resto se separa en "tranquilas" (siguen la partida) y perdedoras inmediatas
        // (destapan una linea rival y por la regla del medio punto pierden en el acto).
        int[] quiet = _quietBuf[_pathLen];
        int qn = 0;
        bool anyLoss = false;
        for (int i = 0; i < n; i++) {
            ulong child = GameSpec.ApplyMove(p, moves[i]);
            Outcome? w = _spec.WinnerAfter(child, turn);
            if (w == null) quiet[qn++] = moves[i];
            else if ((int)w.Value == winValue) return winValue;
            else anyLoss = true;
        }

        int best = maximizing ? -2 : 2;
        if (anyLoss) best = lossValue;

        int originalAlpha = alpha, originalBeta = beta;

        _path[_pathLen] = key;
        _pathTurn[_pathLen] = turn;
        _pathLen++;

        try {
            for (int i = 0; i < qn; i++) {
                ulong child = GameSpec.ApplyMove(p, quiet[i]);
                int v = Search(child, 1 - turn, depth - 1, alpha, beta, out bool childTainted);
                tainted |= childTainted;

                if (maximizing) {
                    if (v > best) best = v;
                    if (best > alpha) alpha = best;
                } else {
                    if (v < best) best = v;
                    if (best < beta) beta = best;
                }
                if (alpha >= beta) {
                    // Una sola jugada limpia alcanza para probar la cota: si esa jugada no
                    // estaba contaminada, el corte vale para cualquier camino.
                    if (!childTainted) tainted = false;
                    break;
                }
            }
        } finally {
            _pathLen--;
        }

        if (best == 2 || best == -2) best = 0;   // no habia jugadas tranquilas ni perdedoras

        if (!tainted && depth >= 2) {
            // Con ventana recortada por el padre el valor puede ser solo una cota:
            // si quedo en el borde de la ventana original no es exacto.
            sbyte flag = 0;
            if (best <= originalAlpha) flag = 2;        // cota superior (fallo bajo)
            else if (best >= originalBeta) flag = 1;    // cota inferior (fallo alto)
            ref TtEntry w2 = ref _tt[slot];
            w2.Key = key; w2.Value = (sbyte)best; w2.Flag = flag;
            w2.Depth = (byte)depth; w2.Turn = (byte)turn; w2.Used = true;
        }
        return best;
    }

    /// <summary>
    /// Resuelve una posicion cualquiera con profundizacion iterativa y devuelve el resultado
    /// junto con la profundidad minima a la que queda decidido. Se usa para evaluar jugada por
    /// jugada: cuantas opciones conservan el resultado y cuantas lo tiran por la borda.
    /// </summary>
    public (Outcome result, int plies) SolveFrom(ulong p, int turn, int maxDepth, long budgetMs = 0) {
        Outcome? terminal = _spec.WinnerAfter(p, 1 - turn);
        if (terminal != null) return (terminal.Value, 0);

        _budgetMs = budgetMs;
        _tick = 0;
        _clock.Restart();
        for (int depth = 1; depth <= maxDepth; depth++) {
            _pathLen = 0;
            int v = Search(p, turn, depth, -1, 1, out _);
            if (v != 0) return ((Outcome)v, depth);
        }
        return (Outcome.Draw, maxDepth);
    }

    /// <summary>Valor de una jugada concreta, respetando el historial ya jugado.</summary>
    private int ChildValue(ulong p, int move, int turn, int depth, ulong[] histKey, int[] histTurn, int histLen) {
        ulong child = GameSpec.ApplyMove(p, move);
        Outcome? w = _spec.WinnerAfter(child, turn);
        if (w != null) return (int)w.Value;
        if (depth <= 1) return 0;
        Array.Copy(histKey, _path, histLen);
        Array.Copy(histTurn, _pathTurn, histLen);
        _pathLen = histLen;
        int v = Search(child, 1 - turn, depth - 1, -1, 1, out _);
        _pathLen = 0;
        return v;
    }

    /// <summary>
    /// Reconstruye la linea principal. Con solo tres valores posibles todas las derrotas
    /// valen igual, asi que el bando perdedor elegiria cualquiera y la linea terminaria
    /// antes de tiempo. Para que la linea muestre la mejor defensa real se mide, entre las
    /// jugadas de valor optimo, a que profundidad minima se decide cada una: el que gana
    /// elige la mas corta y el que pierde la mas larga.
    /// </summary>
    public string PrincipalVariation(int maxDepth) {
        var sb = new System.Text.StringBuilder();
        ulong p = _spec.InitialPosition();
        int turn = GameSpec.White;

        var histKey = new ulong[MaxPly + 2];
        var histTurn = new int[MaxPly + 2];
        int histLen = 0;

        for (int ply = 0; ply < maxDepth; ply++) {
            int remaining = maxDepth - ply;
            var moves = new int[MaxMoves];
            int n = _spec.GenerateMoves(p, turn, moves.AsSpan());
            if (n == 0) break;

            bool maximizing = turn == GameSpec.White;
            int bestVal = maximizing ? -2 : 2;
            var values = new int[n];
            for (int i = 0; i < n; i++) {
                values[i] = ChildValue(p, moves[i], turn, remaining, histKey, histTurn, histLen);
                if (maximizing ? values[i] > bestVal : values[i] < bestVal) bestVal = values[i];
            }

            // Entre las jugadas de valor optimo, la distancia a la que se decide el resultado.
            int bestMove = -1, bestDist = -1;
            bool winning = bestVal != 0 && (bestVal == 1) == maximizing;
            for (int i = 0; i < n; i++) {
                if (values[i] != bestVal) continue;
                int dist = remaining;
                if (bestVal != 0) {
                    for (int d = 1; d <= remaining; d++) {
                        if (ChildValue(p, moves[i], turn, d, histKey, histTurn, histLen) == bestVal) { dist = d; break; }
                    }
                }
                if (bestMove < 0 || (winning ? dist < bestDist : dist > bestDist)) {
                    bestMove = moves[i]; bestDist = dist;
                }
                if (bestVal == 0) break;   // sin resultado decidido no hay distancia que comparar
            }
            if (bestMove < 0) break;

            sb.Append(_spec.DescribeMove(p, bestMove, turn));
            histKey[histLen] = _spec.Canonical(p);
            histTurn[histLen] = turn;
            histLen++;

            p = GameSpec.ApplyMove(p, bestMove);
            Outcome? end = _spec.WinnerAfter(p, turn);
            if (end != null) {
                sb.Append(end == Outcome.WhiteWin ? "  -> gana BLANCO" : "  -> gana NEGRO");
                break;
            }
            sb.Append("; ");
            turn = 1 - turn;
        }
        return sb.ToString();
    }
}
