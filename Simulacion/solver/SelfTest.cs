using System;
using System.Collections.Generic;
using System.Linq;

namespace TatetiSolver;

/// <summary>
/// Verifica que el motor reproduzca exactamente las reglas del juego en Unity.
/// Cada test cita la regla de Cell.cs / GameManager.cs que comprueba.
/// </summary>
public static class SelfTest {
    private static int _failed;

    private static void Check(string name, bool ok) {
        Console.WriteLine($"  [{(ok ? "ok " : "FALLA")}] {name}");
        if (!ok) _failed++;
    }

    /// <summary>Coloca la primera pieza libre del dueño y rango pedidos en una celda.</summary>
    private static ulong Put(GameSpec s, ulong p, int owner, int rank, int cell) {
        for (int i = 0; i < s.PieceCount; i++)
            if (s.Owner[i] == owner && s.Rank[i] == rank && GameSpec.Loc(p, i) == GameSpec.Hand)
                return GameSpec.WithLoc(p, i, cell);
        throw new InvalidOperationException($"no queda pieza libre {owner}/{rank}");
    }

    private static List<(int piece, int from, int to)> Moves(GameSpec s, ulong p, int turn) {
        var buf = new int[128];
        int n = s.GenerateMoves(p, turn, buf.AsSpan());
        return Enumerable.Range(0, n)
            .Select(i => (buf[i] >> 4, GameSpec.Loc(p, buf[i] >> 4), buf[i] & 0xF))
            .ToList();
    }

    public static bool Run() {
        var s = new GameSpec(new[] { 1, 2, 2, 3, 3, 5 }, new[] { 1, 2, 2, 4, 6 });
        int W = GameSpec.White, B = GameSpec.Black;
        ulong empty = s.InitialPosition();

        Console.WriteLine("Reglas de victoria (GameManager.CheckForWinner)");

        // Tres piezas destapadas del mismo dueño en linea ganan.
        ulong p = Put(s, Put(s, Put(s, empty, W, 1, 0), W, 2, 1), W, 3, 2);
        Check("linea de blancas gana", s.WinnerAfter(p, W) == Outcome.WhiteWin);

        // Solo cuenta la pieza visible: una linea tapada no vale.
        ulong covered = Put(s, p, B, 6, 1);   // B6 tapa a W2 en la celda 1
        Check("linea tapada no vale", s.WinnerAfter(covered, B) == null);

        // GameManager.cs:209-211, wins[whoseTurn] -= 0.5f
        // Destapar una linea del rival te hace perder aunque la linea no sea tuya.
        ulong buried = Put(s, Put(s, Put(s, empty, B, 1, 0), B, 2, 1), B, 4, 2);
        buried = Put(s, buried, W, 5, 0);     // W5 tapa a B1, no hay linea negra
        Check("con la linea negra tapada no hay ganador", s.WinnerAfter(buried, W) == null);
        int w5 = Enumerable.Range(0, s.PieceCount).First(i => s.Owner[i] == W && s.Rank[i] == 5);
        ulong revealed = GameSpec.WithLoc(buried, w5, 3);  // mueve W5 de la celda 0 a la 3
        Check("destapar linea rival hace perder al que mueve",
              s.WinnerAfter(revealed, W) == Outcome.BlackWin);

        // Si ambos quedan en linea a la vez, pierde el que acaba de mover.
        ulong both = Put(s, Put(s, Put(s, empty, W, 1, 0), W, 2, 1), W, 3, 2);
        both = Put(s, Put(s, Put(s, both, B, 1, 6), B, 2, 7), B, 4, 8);
        Check("doble linea: mueve blanco, gana negro", s.WinnerAfter(both, W) == Outcome.BlackWin);
        Check("doble linea: mueve negro, gana blanco", s.WinnerAfter(both, B) == Outcome.WhiteWin);

        Console.WriteLine("Reglas de jugada (Cell.CanPieceBePushed / Piece.CanPieceBeMoved)");

        // Cell.cs:92-97 - desde la mano solo se puede colocar en celda vacia.
        ulong occupied = Put(s, empty, W, 1, 4);
        bool placesOnOccupied = Moves(s, occupied, W)
            .Any(m => m.from == GameSpec.Hand && m.to == 4);
        Check("no se coloca desde la mano en celda ocupada", !placesOnOccupied);

        // Cell.cs:103 - solo se tapa con rango estrictamente mayor.
        ulong equalRank = Put(s, Put(s, empty, W, 2, 0), B, 2, 1);
        bool coversEqual = Moves(s, equalRank, B).Any(m => m.from == 1 && m.to == 0);
        Check("no se tapa una pieza de rango igual", !coversEqual);

        ulong higherRank = Put(s, Put(s, empty, W, 2, 0), B, 4, 1);
        bool coversLower = Moves(s, higherRank, B).Any(m => m.from == 1 && m.to == 0);
        Check("si se tapa una pieza de rango menor", coversLower);

        // Piece.cs:41 - solo se mueve la pieza de arriba de la pila.
        ulong stacked = Put(s, Put(s, empty, W, 1, 0), B, 2, 0);
        bool buriedMoves = Moves(s, stacked, W).Any(m => m.from == 0);
        Check("una pieza tapada no se puede mover", !buriedMoves);

        // Cell.cs:86-90 - distancia Manhattan exactamente 1.
        ulong lone = Put(s, empty, W, 1, 0);
        var targets = Moves(s, lone, W).Where(m => m.from == 0).Select(m => m.to).OrderBy(x => x).ToList();
        Check("movimiento solo ortogonal y adyacente", targets.SequenceEqual(new[] { 1, 3 }));

        // La pieza de abajo sigue ahi y se destapa al irse la de arriba.
        ulong under = Put(s, Put(s, empty, B, 1, 0), W, 5, 0);
        Span<int> tops = stackalloc int[9];
        s.ComputeTops(under, tops);
        bool w5OnTop = tops[0] >= 0 && s.Rank[tops[0]] == 5 && s.Owner[tops[0]] == W;
        ulong moved = GameSpec.WithLoc(under, tops[0], 3);
        s.ComputeTops(moved, tops);
        bool b1Revealed = tops[0] >= 0 && s.Rank[tops[0]] == 1 && s.Owner[tops[0]] == B;
        Check("la pila conserva la pieza de abajo y la destapa", w5OnTop && b1Revealed);

        Console.WriteLine("Generacion y canonicalizacion");

        // 4 rangos distintos en la mano x 9 celdas vacias.
        Check("36 jugadas iniciales para blanco", Moves(s, empty, W).Count == 36);

        // El tablero tiene las 8 simetrias del cuadrado.
        ulong a = Put(s, Put(s, empty, W, 1, 0), B, 6, 4);
        ulong rotated = Put(s, Put(s, empty, W, 1, 2), B, 6, 4);   // rotacion de 90 grados
        Check("posiciones simetricas comparten canonico", s.Canonical(a) == s.Canonical(rotated));

        // Las dos piezas de rango 2 de blanco son intercambiables.
        var w2 = Enumerable.Range(0, s.PieceCount).Where(i => s.Owner[i] == W && s.Rank[i] == 2).ToArray();
        ulong x = GameSpec.WithLoc(GameSpec.WithLoc(empty, w2[0], 0), w2[1], 5);
        ulong y = GameSpec.WithLoc(GameSpec.WithLoc(empty, w2[0], 5), w2[1], 0);
        Check("piezas identicas intercambiadas dan el mismo canonico", s.Canonical(x) == s.Canonical(y));

        // Distintas posiciones no deben colisionar.
        ulong z = Put(s, empty, W, 1, 1);
        Check("posiciones distintas dan canonicos distintos", s.Canonical(lone) != s.Canonical(z));

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "Todo en orden." : $"{_failed} test(s) fallaron.");
        return _failed == 0;
    }
}
