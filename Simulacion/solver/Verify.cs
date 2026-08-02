using System;
using System.Collections.Generic;
using System.Linq;

namespace TatetiSolver;

/// <summary>
/// Minimax pelado: sin poda alpha-beta, sin tabla de transposicion, sin ordenamiento de
/// jugadas. Es demasiado lento para resolver el juego, pero a poca profundidad da una
/// referencia independiente contra la cual contrastar el buscador optimizado. Si los dos
/// coinciden en todas las profundidades, la poda y la tabla no estan cambiando el resultado.
/// </summary>
public static class Verify {
    public static long Nodes;

    private static int Naive(GameSpec s, ulong p, int turn, int depth, List<(ulong, int)> path) {
        Nodes++;
        if (depth <= 0) return 0;

        ulong key = s.Canonical(p);
        if (path.Contains((key, turn))) return 0;

        var buf = new int[128];
        int n = s.GenerateMoves(p, turn, buf.AsSpan());
        if (n == 0) return turn == GameSpec.White ? -1 : 1;   // ahogado: el que no puede mover pierde

        path.Add((key, turn));
        bool maximizing = turn == GameSpec.White;
        int best = maximizing ? -2 : 2;
        for (int i = 0; i < n; i++) {
            ulong child = GameSpec.ApplyMove(p, buf[i]);
            Outcome? w = s.WinnerAfter(child, turn);
            int v = w != null ? (int)w.Value : Naive(s, child, 1 - turn, depth - 1, path);
            if (maximizing) { if (v > best) best = v; }
            else { if (v < best) best = v; }
        }
        path.RemoveAt(path.Count - 1);
        return best == 2 || best == -2 ? 0 : best;
    }

    public static bool Run(int[] white, int[] black, int maxDepth) {
        var spec = new GameSpec(white, black);
        Console.WriteLine($"Contraste minimax pelado vs buscador optimizado  W={spec.WhiteLabel} B={spec.BlackLabel}");
        Console.WriteLine($"{"plies",6} {"pelado",-14} {"optimizado",-14} {"nodos pelado",16} {"nodos opt",14}");

        bool ok = true;
        for (int depth = 1; depth <= maxDepth; depth++) {
            Nodes = 0;
            int naive = Naive(spec, spec.InitialPosition(), GameSpec.White, depth, new List<(ulong, int)>());

            var searcher = new Searcher(spec, 22);
            int fast = (int)searcher.Solve(depth);

            bool match = naive == fast;
            ok &= match;
            Console.WriteLine($"{depth,6} {Name(naive),-14} {Name(fast),-14} {Nodes,16:N0} {searcher.Nodes,14:N0}" +
                              (match ? "" : "   <-- DISCREPANCIA"));
        }
        Console.WriteLine();
        Console.WriteLine(ok ? "Los dos buscadores coinciden en todas las profundidades."
                             : "HAY DISCREPANCIAS: el buscador optimizado no es fiable.");
        return ok;
    }

    private static string Name(int v) => v > 0 ? "gana BLANCO" : v < 0 ? "gana NEGRO" : "tablas";
}
