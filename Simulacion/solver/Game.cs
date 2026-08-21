using System;
using System.Collections.Generic;
using System.Linq;

namespace TatetiSolver;

/// <summary>Resultado de una partida, siempre desde el punto de vista de las blancas.</summary>
public enum Outcome { BlackWin = -1, Draw = 0, WhiteWin = 1 }

/// <summary>
/// Reglas del juego, portadas de Assets/Scripts/Cell.cs y Assets/Scripts/GameManager.cs.
///
/// Una posicion es un ulong: 4 bits por pieza indicando su casilla (0..8) o Hand (15).
/// El orden dentro de una pila queda implicito en el rango: solo se puede tapar con un
/// rango estrictamente mayor, asi que una celda nunca tiene dos piezas del mismo rango
/// y la pila esta siempre ordenada de menor a mayor de abajo hacia arriba.
/// </summary>
public sealed partial class GameSpec {
    public const int Hand = 15;
    public const int Cells = 9;
    public const int White = 0;
    public const int Black = 1;

    public readonly int PieceCount;
    public readonly int[] Owner;    // White / Black por indice de pieza
    public readonly int[] Rank;

    // Las piezas identicas (mismo dueño y mismo rango) son intercambiables: se agrupan
    // en rangos contiguos de indices para poder canonicalizar ordenando el subrango.
    public readonly int[] GroupStart;
    public readonly int[] GroupLen;
    public readonly int[] GroupOwner;
    public readonly int[] GroupRank;

    public readonly int[][] Adjacent;
    public readonly int[][] Symmetries;   // las 8 simetrias del tablero (D4)

    public static readonly int[][] Lines = {
        new[]{0,1,2}, new[]{3,4,5}, new[]{6,7,8},
        new[]{0,3,6}, new[]{1,4,7}, new[]{2,5,8},
        new[]{0,4,8}, new[]{2,4,6},
    };

    public readonly string WhiteLabel;
    public readonly string BlackLabel;

    /// <summary>Variante sin tablero fijo: el 3x3 lo delimitan las piezas. Ver Libre.cs.</summary>
    public readonly bool Libre;

    /// <summary>Sub-variante: la pieza que se coloca tiene que tocar a alguna ya puesta.</summary>
    public readonly bool Pegado;
    /// <summary>Tocar sólo en ortogonal (por defecto tambien vale en diagonal).</summary>
    public readonly bool PegadoOrto;
    /// <summary>Exigir el pegado tambien despues de que el 3x3 quedo delimitado.</summary>
    public readonly bool PegadoSiempre;

    public GameSpec(IEnumerable<int> whitePieces, IEnumerable<int> blackPieces, bool libre = false,
                    bool pegado = false, bool pegadoOrto = false, bool pegadoSiempre = false) {
        Libre = libre;
        Pegado = pegado; PegadoOrto = pegadoOrto; PegadoSiempre = pegadoSiempre;
        var white = whitePieces.OrderBy(r => r).ToArray();
        var black = blackPieces.OrderBy(r => r).ToArray();
        WhiteLabel = string.Concat(white);
        BlackLabel = string.Concat(black);

        PieceCount = white.Length + black.Length;
        if (PieceCount > 16)
            throw new ArgumentException("Maximo 16 piezas en total (4 bits por pieza en un ulong).");

        Owner = new int[PieceCount];
        Rank = new int[PieceCount];
        int k = 0;
        foreach (int r in white) { Owner[k] = White; Rank[k] = r; k++; }
        foreach (int r in black) { Owner[k] = Black; Rank[k] = r; k++; }

        var gs = new List<int>(); var gl = new List<int>();
        var go = new List<int>(); var gr = new List<int>();
        for (int i = 0; i < PieceCount;) {
            int j = i;
            while (j < PieceCount && Owner[j] == Owner[i] && Rank[j] == Rank[i]) j++;
            gs.Add(i); gl.Add(j - i); go.Add(Owner[i]); gr.Add(Rank[i]);
            i = j;
        }
        GroupStart = gs.ToArray(); GroupLen = gl.ToArray();
        GroupOwner = go.ToArray(); GroupRank = gr.ToArray();

        Adjacent = new int[Cells][];
        for (int c = 0; c < Cells; c++) {
            int row = c / 3, col = c % 3;
            var a = new List<int>();
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) {
                int nr = row + dr, nc = col + dc;
                if (nr >= 0 && nr < 3 && nc >= 0 && nc < 3) a.Add(nr * 3 + nc);
            }
            Adjacent[c] = a.ToArray();
        }

        Func<int, int, int>[] transforms = {
            (r, c) => r * 3 + c,             // identidad
            (r, c) => c * 3 + (2 - r),       // rotacion 90
            (r, c) => (2 - r) * 3 + (2 - c), // rotacion 180
            (r, c) => (2 - c) * 3 + r,       // rotacion 270
            (r, c) => r * 3 + (2 - c),       // espejo vertical
            (r, c) => (2 - r) * 3 + c,       // espejo horizontal
            (r, c) => c * 3 + r,             // diagonal principal
            (r, c) => (2 - c) * 3 + (2 - r), // antidiagonal
        };
        Symmetries = transforms.Select(t => {
            var perm = new int[Cells];
            for (int c = 0; c < Cells; c++) perm[c] = t(c / 3, c % 3);
            return perm;
        }).ToArray();
    }

    public ulong InitialPosition() {
        ulong p = 0;
        for (int i = 0; i < PieceCount; i++) p |= (ulong)Hand << (4 * i);
        return p;
    }

    public static int Loc(ulong p, int i) => (int)((p >> (4 * i)) & 0xF);

    public static ulong WithLoc(ulong p, int i, int cell) =>
        (p & ~(0xFUL << (4 * i))) | ((ulong)cell << (4 * i));

    /// <summary>Pieza visible (la de mayor rango) de cada celda, o -1 si esta vacia.</summary>
    public void ComputeTops(ulong p, Span<int> topPiece) {
        for (int c = 0; c < Cells; c++) topPiece[c] = -1;
        for (int i = 0; i < PieceCount; i++) {
            int c = Loc(p, i);
            if (c >= Cells) continue;
            int cur = topPiece[c];
            if (cur < 0 || Rank[i] > Rank[cur]) topPiece[c] = i;
        }
    }

    /// <summary>
    /// GameManager.CheckForWinner: cuenta lineas de piezas destapadas, y el jugador que
    /// acaba de mover pierde los empates (wins[whoseTurn] -= 0.5f). Consecuencia: destapar
    /// una linea del rival te hace perder, aunque la linea no sea tuya.
    /// Se usan medios puntos duplicados para trabajar con enteros.
    /// </summary>
    public Outcome? WinnerAfter(ulong p, int mover) {
        Span<int> top = stackalloc int[Cells];
        ComputeTops(p, top);

        int whiteLines = 0, blackLines = 0;
        foreach (var line in Lines) {
            int a = top[line[0]];
            if (a < 0) continue;
            int o = Owner[a];
            int b = top[line[1]]; if (b < 0 || Owner[b] != o) continue;
            int c = top[line[2]]; if (c < 0 || Owner[c] != o) continue;
            if (o == White) whiteLines++; else blackLines++;
        }
        if (whiteLines == 0 && blackLines == 0) return null;

        int ws = 2 * whiteLines, bs = 2 * blackLines;
        if (mover == White) ws -= 1; else bs -= 1;
        if (ws > bs) return Outcome.WhiteWin;
        if (bs > ws) return Outcome.BlackWin;
        return null;   // inalcanzable: el medio punto rompe todo empate
    }

    /// <summary>Una jugada codificada como (indiceDePieza &lt;&lt; 4) | celdaDestino.</summary>
    public int GenerateMoves(ulong p, int turn, Span<int> moves) {
        if (Libre) return GenerateMovesLibre(p, turn, moves);
        Span<int> top = stackalloc int[Cells];
        ComputeTops(p, top);
        int n = 0;

        // Colocar desde la mano, solo en celda vacia (Cell.CanPieceBePushed).
        // Las piezas identicas dan jugadas identicas: alcanza con la primera del grupo.
        for (int g = 0; g < GroupStart.Length; g++) {
            if (GroupOwner[g] != turn) continue;
            int piece = -1;
            for (int i = GroupStart[g]; i < GroupStart[g] + GroupLen[g]; i++) {
                if (Loc(p, i) == Hand) { piece = i; break; }
            }
            if (piece < 0) continue;
            for (int c = 0; c < Cells; c++) if (top[c] < 0) moves[n++] = (piece << 4) | c;
        }

        // Mover una pieza propia destapada a una celda ortogonal vacia, o sobre una pila
        // cuya pieza visible tenga rango estrictamente menor.
        for (int c = 0; c < Cells; c++) {
            int i = top[c];
            if (i < 0 || Owner[i] != turn) continue;
            foreach (int d in Adjacent[c]) {
                int t = top[d];
                if (t < 0 || Rank[t] < Rank[i]) moves[n++] = (i << 4) | d;
            }
        }
        return n;
    }

    public static ulong ApplyMove(ulong p, int move) => WithLoc(p, move >> 4, move & 0xF);

    /// <summary>Aplica una jugada segun la variante. En el juego normal es ApplyMove.</summary>
    public ulong Apply(ulong p, int move) => Libre ? ApplyLibre(p, move) : ApplyMove(p, move);

    /// <summary>Indice de pieza y casilla destino de una jugada, segun la codificacion de la variante.</summary>
    public int PieceOf(int move) => Libre ? move >> 5 : move >> 4;

    /// <summary>
    /// Representante canonico bajo las 8 simetrias del tablero y el intercambio de piezas
    /// identicas. Reduce la tabla de transposicion cerca de 8x.
    /// </summary>
    public ulong Canonical(ulong p) {
        if (Libre) return CanonicalLibre(p);
        Span<int> loc = stackalloc int[PieceCount];
        for (int i = 0; i < PieceCount; i++) loc[i] = Loc(p, i);

        Span<int> t = stackalloc int[PieceCount];
        ulong best = ulong.MaxValue;

        foreach (var perm in Symmetries) {
            for (int i = 0; i < PieceCount; i++) {
                int l = loc[i];
                t[i] = l >= Cells ? Hand : perm[l];
            }
            for (int g = 0; g < GroupStart.Length; g++) {
                int len = GroupLen[g];
                if (len < 2) continue;
                int s = GroupStart[g];
                for (int i = s + 1; i < s + len; i++) {   // insercion, grupos de 2-3
                    int v = t[i], j = i - 1;
                    while (j >= s && t[j] > v) { t[j + 1] = t[j]; j--; }
                    t[j + 1] = v;
                }
            }
            ulong v2 = 0;
            for (int i = PieceCount - 1; i >= 0; i--) v2 = (v2 << 4) | (uint)t[i];
            if (v2 < best) best = v2;
        }
        return best;
    }

    public string Describe(ulong p) {
        Span<int> top = stackalloc int[Cells];
        ComputeTops(p, top);
        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < 3; r++) {
            for (int c = 0; c < 3; c++) {
                int cell = r * 3 + c;
                var stack = Enumerable.Range(0, PieceCount)
                    .Where(i => Loc(p, i) == cell)
                    .OrderBy(i => Rank[i])
                    .Select(i => (Owner[i] == White ? "W" : "B") + Rank[i]);
                string s = string.Join("/", stack);
                sb.Append((s.Length == 0 ? "." : s).PadRight(9));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string DescribeMove(ulong p, int move, int turn) {
        if (Libre) return DescribeMoveLibre(p, move, turn);
        int piece = move >> 4, to = move & 0xF;
        int from = Loc(p, piece);
        string who = turn == White ? "W" : "B";
        return from == Hand
            ? $"{who}C{Rank[piece]}({to / 3},{to % 3})"
            : $"{who}M{Rank[piece]}({from / 3},{from % 3})-({to / 3},{to % 3})";
    }
}
