using System;

namespace TatetiSolver;

/// <summary>
/// Variante "sin tablero": el 3x3 no esta dibujado de antemano, lo delimitan las piezas.
///
/// La regla es una sola: <b>todo lo que esta puesto tiene que entrar en algun 3x3</b>. O sea
/// que la caja que envuelve a las casillas ocupadas nunca puede medir mas de 3 de alto ni mas
/// de 3 de ancho. Todo lo demas del juego queda igual — colocar exige casilla vacia, se mueve
/// una casilla en ortogonal, se tapa solo con rango estrictamente mayor, gana la linea de tres
/// destapadas y el que acaba de mover pierde los empates.
///
/// Consecuencias de esa regla, que son las que el jugador siente:
///  - La primera pieza no elige lugar: no hay lugares todavia. Elige pieza y nada mas.
///  - La segunda recien empieza a atar el tablero. Si cae en la columna de al lado, quedan dos
///    columnas fijas y la tercera puede terminar de cualquiera de los dos lados. Si cae en
///    diagonal salteando una casilla, la caja ya mide 3x3 y el tablero queda definido entero.
///  - Y no queda definido para siempre: si las piezas se corren y la caja se achica, el 3x3
///    vuelve a estar sin decidir y puede rearmarse en otra disposicion.
///
/// Como se representa: una posicion se guarda igual que en el juego normal — cuatro bits por
/// pieza con su casilla 0..8 — pero <b>encuadrada</b>, es decir corrida hasta que la fila y la
/// columna mas chicas ocupadas sean la 0. Dos posiciones que difieren en un desplazamiento son
/// la misma posicion, y esta forma lo garantiza sin trabajo extra. Como toda configuracion
/// legal entra en 3x3, el encuadre siempre da casillas 0..8: el ulong, la tabla de
/// transposicion, la deteccion de repeticion y las lineas de victoria funcionan sin tocarse.
///
/// Los destinos, en cambio, no entran en 0..8: una pieza puede colocarse o moverse afuera del
/// encuadre actual. Por eso una jugada de esta variante apunta a un marco extendido de 5x5
/// (filas y columnas -2..2, que es todo lo que la regla de la caja permite alcanzar) y se
/// codifica como (pieza &lt;&lt; 5) | casillaExtendida en vez de (pieza &lt;&lt; 4) | casilla.
/// Aplicarla vuelve a encuadrar.
/// </summary>
public sealed partial class GameSpec {
    private static readonly (int dr, int dc)[] Dirs = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    /// <summary>Casilla del marco extendido 5x5: filas y columnas de -2 a 2.</summary>
    private static int ECell(int r, int c) => (r + 2) * 5 + (c + 2);

    private static bool Dentro(int r, int c) => r >= 0 && r < 3 && c >= 0 && c < 3;

    /// <summary>
    /// Si la casilla (r,c) del marco extendido toca alguna casilla ocupada. Por defecto cuenta
    /// la diagonal; con PegadoOrto sólo los cuatro lados. Las casillas de afuera del encuadre
    /// están vacías por definición, así que sólo hay que mirar las de adentro.
    /// </summary>
    private bool Toca(int r, int c, Span<int> count) {
        for (int dr = -1; dr <= 1; dr++) {
            for (int dc = -1; dc <= 1; dc++) {
                if (dr == 0 && dc == 0) continue;
                if (PegadoOrto && dr != 0 && dc != 0) continue;
                int vr = r + dr, vc = c + dc;
                if (Dentro(vr, vc) && count[vr * 3 + vc] > 0) return true;
            }
        }
        return false;
    }

    /// <summary>Piezas por casilla y pieza visible de cada casilla, en el encuadre actual.</summary>
    private void Ocupacion(ulong p, Span<int> top, Span<int> count) {
        for (int c = 0; c < Cells; c++) { top[c] = -1; count[c] = 0; }
        for (int i = 0; i < PieceCount; i++) {
            int c = Loc(p, i);
            if (c >= Cells) continue;
            count[c]++;
            if (top[c] < 0 || Rank[i] > Rank[top[c]]) top[c] = i;
        }
    }

    private int GenerateMovesLibre(ulong p, int turn, Span<int> moves) {
        Span<int> top = stackalloc int[Cells];
        Span<int> count = stackalloc int[Cells];
        Ocupacion(p, top, count);

        // Caja actual. Por el encuadre minR y minC son 0 salvo que no haya nada puesto.
        int minR = 3, maxR = -1, minC = 3, maxC = -1;
        for (int c = 0; c < Cells; c++) {
            if (count[c] == 0) continue;
            int r = c / 3, k = c % 3;
            if (r < minR) minR = r;
            if (r > maxR) maxR = r;
            if (k < minC) minC = k;
            if (k > maxC) maxC = k;
        }
        bool vacio = maxR < 0;
        int n = 0;

        // ---- colocar desde la mano
        for (int g = 0; g < GroupStart.Length; g++) {
            if (GroupOwner[g] != turn) continue;
            int piece = -1;
            for (int i = GroupStart[g]; i < GroupStart[g] + GroupLen[g]; i++) {
                if (Loc(p, i) == Hand) { piece = i; break; }
            }
            if (piece < 0) continue;

            // Sin nada en el tablero todos los lugares son el mismo lugar: una sola jugada.
            if (vacio) { moves[n++] = (piece << 5) | ECell(0, 0); continue; }

            // Sub-variante "pegado": la pieza nueva tiene que tocar a alguna ya puesta. Si se
            // pidio solo mientras el tablero no esta delimitado, deja de exigirse en cuanto la
            // caja llega a 3x3 y de ahi en mas se coloca como en el juego de siempre.
            bool definido = (maxR - minR == 2) && (maxC - minC == 2);
            bool exigirPegado = Pegado && (PegadoSiempre || !definido);

            for (int r = -2; r <= 2; r++) {
                if (Math.Max(maxR, r) - Math.Min(minR, r) > 2) continue;
                for (int c = -2; c <= 2; c++) {
                    if (Math.Max(maxC, c) - Math.Min(minC, c) > 2) continue;
                    if (Dentro(r, c) && count[r * 3 + c] > 0) continue;   // casilla ocupada
                    if (exigirPegado && !Toca(r, c, count)) continue;
                    moves[n++] = (piece << 5) | ECell(r, c);
                }
            }
        }

        // ---- mover una pieza destapada
        for (int cell = 0; cell < Cells; cell++) {
            int i = top[cell];
            if (i < 0 || Owner[i] != turn) continue;
            int r0 = cell / 3, c0 = cell % 3;

            // La casilla de origen queda libre solo si la pieza no estaba tapando a otra.
            bool desocupa = count[cell] == 1;
            int mr = 3, xr = -1, mc = 3, xc = -1;
            for (int c2 = 0; c2 < Cells; c2++) {
                if (count[c2] == 0 || (c2 == cell && desocupa)) continue;
                int r = c2 / 3, k = c2 % 3;
                if (r < mr) mr = r;
                if (r > xr) xr = r;
                if (k < mc) mc = k;
                if (k > xc) xc = k;
            }

            foreach (var (dr, dc) in Dirs) {
                int tr = r0 + dr, tc = c0 + dc;
                if (tr < -2 || tr > 2 || tc < -2 || tc > 2) continue;
                if (Dentro(tr, tc)) {
                    int t = top[tr * 3 + tc];
                    if (t >= 0 && Rank[t] >= Rank[i]) continue;   // solo se tapa hacia abajo
                }
                // La caja resultante: lo que queda mas el destino.
                int nmr = xr < 0 ? tr : Math.Min(mr, tr);
                int nxr = xr < 0 ? tr : Math.Max(xr, tr);
                int nmc = xc < 0 ? tc : Math.Min(mc, tc);
                int nxc = xc < 0 ? tc : Math.Max(xc, tc);
                if (nxr - nmr > 2 || nxc - nmc > 2) continue;
                moves[n++] = (i << 5) | ECell(tr, tc);
            }
        }
        return n;
    }

    /// <summary>Aplica la jugada y vuelve a encuadrar contra la esquina de la caja nueva.</summary>
    private ulong ApplyLibre(ulong p, int move) {
        int piece = move >> 5, e = move & 31;
        int tr = e / 5 - 2, tc = e % 5 - 2;

        Span<int> rs = stackalloc int[PieceCount];
        Span<int> cs = stackalloc int[PieceCount];
        int minR = int.MaxValue, minC = int.MaxValue;
        for (int i = 0; i < PieceCount; i++) {
            if (i == piece) { rs[i] = tr; cs[i] = tc; }
            else {
                int l = Loc(p, i);
                if (l >= Cells) { rs[i] = int.MinValue; continue; }
                rs[i] = l / 3; cs[i] = l % 3;
            }
            if (rs[i] < minR) minR = rs[i];
            if (cs[i] < minC) minC = cs[i];
        }

        ulong q = 0;
        for (int i = 0; i < PieceCount; i++) {
            int loc = rs[i] == int.MinValue ? Hand : (rs[i] - minR) * 3 + (cs[i] - minC);
            q |= (ulong)loc << (4 * i);
        }
        return q;
    }

    /// <summary>
    /// Representante canonico de la variante. Igual que el del juego normal — las 8 simetrias
    /// del cuadrado y el intercambio de piezas identicas — pero reencuadrando despues de cada
    /// simetria, porque girar una caja de 2x2 la deja pegada a la otra esquina y es la misma
    /// posicion. Con el reencuadre, las 8 simetrias del 3x3 son las del plano infinito.
    /// </summary>
    private ulong CanonicalLibre(ulong p) {
        Span<int> loc = stackalloc int[PieceCount];
        for (int i = 0; i < PieceCount; i++) loc[i] = Loc(p, i);

        Span<int> t = stackalloc int[PieceCount];
        ulong best = ulong.MaxValue;

        foreach (var perm in Symmetries) {
            int minR = int.MaxValue, minC = int.MaxValue;
            for (int i = 0; i < PieceCount; i++) {
                int l = loc[i];
                if (l >= Cells) { t[i] = int.MinValue; continue; }
                int m = perm[l];
                t[i] = m;
                int r = m / 3, c = m % 3;
                if (r < minR) minR = r;
                if (c < minC) minC = c;
            }
            for (int i = 0; i < PieceCount; i++) {
                if (t[i] == int.MinValue) { t[i] = Hand; continue; }
                t[i] = (t[i] / 3 - minR) * 3 + (t[i] % 3 - minC);
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

    /// <summary>
    /// En esta variante las coordenadas absolutas no significan nada — el encuadre se corre
    /// solo. Se anota el destino relativo al encuadre previo, con * cuando cae afuera y por lo
    /// tanto redefine el tablero.
    /// </summary>
    private string DescribeMoveLibre(ulong p, int move, int turn) {
        int piece = move >> 5, e = move & 31;
        int tr = e / 5 - 2, tc = e % 5 - 2;
        int from = Loc(p, piece);
        string who = turn == White ? "W" : "B";
        string dest = $"({tr},{tc})" + (Dentro(tr, tc) ? "" : "*");
        return from == Hand
            ? $"{who}C{Rank[piece]}{dest}"
            : $"{who}M{Rank[piece]}({from / 3},{from % 3})-{dest}";
    }

    /// <summary>Alto y ancho de la caja que envuelve lo puesto. 0x0 si no hay nada.</summary>
    public (int alto, int ancho) Caja(ulong p) {
        int minR = 3, maxR = -1, minC = 3, maxC = -1;
        for (int i = 0; i < PieceCount; i++) {
            int l = Loc(p, i);
            if (l >= Cells) continue;
            int r = l / 3, c = l % 3;
            if (r < minR) minR = r;
            if (r > maxR) maxR = r;
            if (c < minC) minC = c;
            if (c > maxC) maxC = c;
        }
        return maxR < 0 ? (0, 0) : (maxR - minR + 1, maxC - minC + 1);
    }
}
