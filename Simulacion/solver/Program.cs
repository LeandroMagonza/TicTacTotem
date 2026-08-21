using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TatetiSolver;

public static class Program {
    public static int Main(string[] args) {
        if (args.Length == 0) { Usage(); return 1; }
        var opts = ParseOptions(args.Skip(1).ToArray());
        switch (args[0]) {
            case "selftest":
                return (Flag(opts, "libre") ? SelfTestLibre.Run() : SelfTest.Run()) ? 0 : 1;
            case "verify":
                return Verify.Run(ParseSet(Str(opts, "white", "122335")),
                                  ParseSet(Str(opts, "black", "12246")),
                                  Int(opts, "max-depth", 9),
                                  Flag(opts, "libre"), Flag(opts, "pegado"),
                                  Flag(opts, "pegado-orto"), Flag(opts, "pegado-siempre")) ? 0 : 1;
            case "solve": return Solve(opts);
            case "openings": return Openings(opts);
            case "libertad": return Libertad(opts);
            case "practica": return Practica(opts);
            case "practicas": return Practicas(opts);
            case "trampas": return Trampas(opts);
            case "sweep": return Sweep(opts);
            default: Usage(); return 1;
        }
    }

    private static void Usage() {
        Console.WriteLine(@"
Solver exacto para el TaTeTi con Esteroides.

  selftest
      Verifica que el motor reproduzca las reglas de Cell.cs / GameManager.cs.

  solve  --white 122335 --black 12246 [--max-depth 20] [--tt-bits 25]
         [--budget-ms 0] [--no-repetition] [--pv]
      Resuelve un enfrentamiento con profundizacion iterativa. Un veredicto
      'gana X' es una victoria forzada real dentro del limite de plies.

  sweep  [--white-sets a,b,c --black-sets d,e,f]
         [--gen-white-size 6 --gen-black-size 5 --max-rank 6]
         [--sum-window 4] [--limit 0] [--max-depth 14] [--budget-ms 60000]
         [--tt-bits 22] [--out balance.csv]
      Barre combinaciones de sets y escribe un CSV comparativo.

  --libre   (en cualquier comando)
      Variante sin tablero: el 3x3 no existe de antemano, lo delimitan las
      piezas. Todo lo puesto tiene que entrar en algun 3x3, y esa caja se
      puede redefinir durante la partida. El resto de las reglas no cambia.

  --pegado [--pegado-orto] [--pegado-siempre]   (con --libre)
      Sub-variante: la pieza que se coloca desde la mano tiene que tocar a
      alguna ya puesta. Por defecto vale tocar en diagonal y la exigencia
      se levanta apenas la caja llega a 3x3; --pegado-orto exige tocar por
      un lado y --pegado-siempre la mantiene toda la partida.

Los sets se escriben como digitos: 122335 = piezas 1,2,2,3,3,5.
");
    }

    // ---------------------------------------------------------------- opciones

    private static Dictionary<string, string> ParseOptions(string[] args) {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++) {
            if (!args[i].StartsWith("--")) continue;
            string k = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) { d[k] = args[++i]; }
            else d[k] = "true";
        }
        return d;
    }

    private static string Str(Dictionary<string, string> o, string k, string dflt) =>
        o.TryGetValue(k, out var v) ? v : dflt;

    private static int Int(Dictionary<string, string> o, string k, int dflt) =>
        o.TryGetValue(k, out var v) && int.TryParse(v, out int r) ? r : dflt;

    private static bool Flag(Dictionary<string, string> o, string k) => o.ContainsKey(k);

    /// <summary>Construye el juego con la variante y sub-variantes que pidio la linea de comandos.</summary>
    private static GameSpec Spec(Dictionary<string, string> o, int[] white, int[] black) =>
        new GameSpec(white, black,
                     libre: Flag(o, "libre"),
                     pegado: Flag(o, "pegado"),
                     pegadoOrto: Flag(o, "pegado-orto"),
                     pegadoSiempre: Flag(o, "pegado-siempre"));

    private static int[] ParseSet(string s) =>
        s.Where(char.IsDigit).Select(c => c - '0').OrderBy(x => x).ToArray();

    // ------------------------------------------------------------------ solve

    private static int Solve(Dictionary<string, string> o) {
        var white = ParseSet(Str(o, "white", "122335"));
        var black = ParseSet(Str(o, "black", "12246"));
        int maxDepth = Math.Min(Int(o, "max-depth", 20), 60);
        int ttBits = Int(o, "tt-bits", 25);
        long budget = Int(o, "budget-ms", 0);
        bool repetition = !Flag(o, "no-repetition");

        bool libre = Flag(o, "libre");

        var spec = Spec(o, white, black);
        Console.WriteLine($"Blancas (mueven primero): {spec.WhiteLabel}   suma {white.Sum()}");
        Console.WriteLine($"Negras:                   {spec.BlackLabel}   suma {black.Sum()}");
        Console.WriteLine($"Repeticion = tablas: {(repetition ? "si" : "no")}   tabla: 2^{ttBits} entradas");
        if (libre) Console.WriteLine("Variante SIN TABLERO: el 3x3 lo delimitan las piezas (caja de 3x3 como maximo).");
        Console.WriteLine();
        Console.WriteLine($"{"plies",6} {"veredicto",-14} {"nodos",16} {"seg",8}");

        var searcher = new Searcher(spec, ttBits, repetition);
        var clock = Stopwatch.StartNew();
        Outcome result = Outcome.Draw;
        int reached = 0;

        for (int depth = 1; depth <= maxDepth; depth++) {
            long before = searcher.Nodes;
            var t0 = clock.Elapsed;
            try {
                result = searcher.Solve(depth, budget);
            } catch (SearchAborted) {
                Console.WriteLine($"{depth,6} {"(sin tiempo)",-14} {searcher.Nodes - before,16:N0} {(clock.Elapsed - t0).TotalSeconds,8:F1}");
                break;
            }
            reached = depth;
            Console.WriteLine($"{depth,6} {Verdict(result),-14} {searcher.Nodes - before,16:N0} {(clock.Elapsed - t0).TotalSeconds,8:F1}");
            if (result != Outcome.Draw) break;
        }

        Console.WriteLine();
        Console.WriteLine(result != Outcome.Draw
            ? $"VEREDICTO: {Verdict(result)} tiene victoria forzada en {reached} plies."
            : $"VEREDICTO: ninguno tiene victoria forzada en {reached} plies o menos.");
        Console.WriteLine($"Nodos: {searcher.Nodes:N0}   aciertos de tabla: {searcher.TtHits:N0}   posiciones sin jugada legal: {searcher.Stalemates:N0}");

        if (Flag(o, "pv") && reached > 0) {
            Console.WriteLine();
            Console.WriteLine("Linea principal:");
            if (Flag(o, "trace")) {
                Console.WriteLine("  " + searcher.PrincipalVariation(reached, (ply, pos, mover) => {
                    var (alto, ancho) = spec.Caja(pos);
                    Console.WriteLine();
                    Console.WriteLine($"  ply {ply}  ({(mover == GameSpec.White ? "arranca" : "segundo")})   caja {alto}x{ancho}");
                    foreach (var linea in spec.Describe(pos).TrimEnd().Split('\n'))
                        Console.WriteLine("    " + linea.TrimEnd());
                }));
            } else {
                Console.WriteLine("  " + searcher.PrincipalVariation(reached));
            }
        }
        return 0;
    }

    // --------------------------------------------------------------- openings

    /// <summary>
    /// Donde cae la primera pieza. En la variante sin tablero no hay donde: todavia no hay
    /// casillas, asi que todas las colocaciones son la misma jugada y la unica eleccion real
    /// es con que pieza abrir.
    /// </summary>
    private static string TipoDeCasilla(GameSpec spec, int cell) =>
        spec.Libre ? "cualquiera"
        : cell == 4 ? "centro" : (cell % 2 == 0 ? "esquina" : "lado");

    /// <summary>
    /// Evalua todas las jugadas iniciales por separado. Sirve para medir si el juego tiene
    /// una apertura obligada — si una sola jugada gana y el resto pierde, la partida es un
    /// acertijo con solucion unica por mas larga que sea.
    /// </summary>
    private static int Openings(Dictionary<string, string> o) {
        var white = ParseSet(Str(o, "white", "122335"));
        var black = ParseSet(Str(o, "black", "12246"));
        int maxDepth = Math.Min(Int(o, "max-depth", 25), 60);
        int ttBits = Int(o, "tt-bits", 24);
        long budget = Int(o, "budget-ms", 0);

        var spec = Spec(o, white, black);
        ulong start = spec.InitialPosition();
        Console.WriteLine($"Aperturas de {spec.WhiteLabel} (arranca) contra {spec.BlackLabel}");
        Console.WriteLine($"Hasta {maxDepth} plies. Las jugadas equivalentes por simetria se cuentan una vez.");
        Console.WriteLine();

        var buf = new int[256];
        int n = spec.GenerateMoves(start, GameSpec.White, buf.AsSpan());
        var vistos = new HashSet<ulong>();
        var unicos = new List<int>();
        for (int i = 0; i < n; i++) {
            ulong child = spec.Apply(start, buf[i]);
            if (vistos.Add(spec.Canonical(child))) unicos.Add(buf[i]);
        }

        var filas = new (int rank, int cell, Outcome res, int plies)[unicos.Count];
        Parallel.For(0, unicos.Count, i => {
            int mv = unicos[i];
            ulong child = spec.Apply(start, mv);
            var searcher = new Searcher(spec, ttBits);
            Outcome res; int plies;
            try {
                (res, plies) = searcher.SolveFrom(child, GameSpec.Black, maxDepth - 1, budget);
            } catch (SearchAborted) { res = Outcome.Draw; plies = maxDepth - 1; }
            filas[i] = (spec.Rank[spec.PieceOf(mv)], mv & 0xF, res, plies + 1);
        });

        var mejor = filas.Any(f => f.res == Outcome.WhiteWin) ? Outcome.WhiteWin
                  : filas.Any(f => f.res == Outcome.Draw) ? Outcome.Draw : Outcome.BlackWin;
        int buenas = filas.Count(f => f.res == mejor);

        Console.WriteLine($"{"pieza",6} {"casilla",10} {"resultado",-16} {"plies",6}");
        Console.WriteLine(new string('-', 42));
        foreach (var f in filas.OrderBy(f => f.res == mejor ? 0 : 1)
                               .ThenByDescending(f => f.plies).ThenBy(f => f.rank)) {
            string marca = f.res == mejor ? "  <-- conserva lo mejor" : "";
            Console.WriteLine($"{f.rank,6} {TipoDeCasilla(spec, f.cell),10} {Verdict(f.res),-16} {f.plies,6}{marca}");
        }

        Console.WriteLine();
        Console.WriteLine($"Aperturas distintas (sin simetrias): {filas.Length}");
        Console.WriteLine($"Conservan el mejor resultado ({Verdict(mejor)}): {buenas}  ({100.0 * buenas / filas.Length:F0} %)");
        Console.WriteLine();
        foreach (var g in filas.GroupBy(f => TipoDeCasilla(spec, f.cell)).OrderBy(g => g.Key)) {
            int ok = g.Count(f => f.res == mejor);
            Console.WriteLine($"  {g.Key,-8} {ok}/{g.Count()} conservan lo mejor" +
                              (ok > 0 ? $"   (mejor profundidad {g.Where(f => f.res == mejor).Max(f => f.plies)} plies)" : ""));
        }
        return 0;
    }

    // -------------------------------------------------------------- libertad

    /// <summary>
    /// Recorre la partida con juego optimo de los dos lados y mide, turno por turno, cuantas
    /// jugadas distintas conservan lo mejor a lo que ese jugador puede aspirar. Para el que va
    /// ganando son las que mantienen la victoria; para el que va perdiendo, las que mas
    /// estiran la derrota — su unica decision real. Un juego donde ese numero cae a uno
    /// enseguida es un acertijo, por mas turnos que dure.
    /// </summary>
    private static int Libertad(Dictionary<string, string> o) {
        var white = ParseSet(Str(o, "white", "122335"));
        var black = ParseSet(Str(o, "black", "12246"));
        int maxDepth = Math.Min(Int(o, "max-depth", 19), 60);
        int maxPlies = Int(o, "plies", 9);
        int ttBits = Int(o, "tt-bits", 23);

        var spec = Spec(o, white, black);
        ulong p = spec.InitialPosition();
        int turn = GameSpec.White;

        Console.WriteLine($"{spec.WhiteLabel} (arranca) contra {spec.BlackLabel}");
        Console.WriteLine($"{"ply",4} {"juega",-9} {"aspira a",-10} {"opciones",12} {"descuidos",4}  {"elige",-22}");
        Console.WriteLine(new string('-', 64));

        var perfil = new List<double>();
        for (int ply = 1; ply <= maxPlies; ply++) {
            int restante = maxDepth - (ply - 1);
            if (restante < 3) break;

            var buf = new int[256];
            int n = spec.GenerateMoves(p, turn, buf.AsSpan());
            if (n == 0) break;

            var vistos = new HashSet<ulong>();
            var unicos = new List<int>();
            for (int i = 0; i < n; i++) {
                ulong c = spec.Apply(p, buf[i]);
                if (vistos.Add(spec.Canonical(c))) unicos.Add(buf[i]);
            }

            var res = new (Outcome r, int d)[unicos.Count];
            ulong pp = p; int tt = turn, rr = restante;
            Parallel.For(0, unicos.Count, i => {
                ulong c = spec.Apply(pp, unicos[i]);
                Outcome? term = spec.WinnerAfter(c, tt);
                if (term != null) { res[i] = (term.Value, 1); return; }
                var s = new Searcher(spec, ttBits);
                var (a, b) = s.SolveFrom(c, 1 - tt, rr - 1);
                res[i] = (a, b + 1);
            });

            int mio = turn == GameSpec.White ? 1 : -1;
            int mejor = Enumerable.Range(0, unicos.Count).Max(i => (int)res[i].r * mio);
            var conMejor = Enumerable.Range(0, unicos.Count)
                                     .Where(i => (int)res[i].r * mio == mejor).ToList();

            List<int> viables;
            string aspira;
            int elegido;
            if (mejor > 0) {
                viables = conMejor;
                aspira = "ganar";
                elegido = conMejor.OrderBy(i => res[i].d).First();
            } else if (mejor == 0) {
                viables = conMejor;
                aspira = "empatar";
                elegido = conMejor.OrderByDescending(i => res[i].d).First();
            } else {
                int dmax = conMejor.Max(i => res[i].d);
                viables = conMejor.Where(i => res[i].d == dmax).ToList();
                aspira = "resistir";
                elegido = viables.First();
            }

            // Una jugada que pierde en dos plies es un descuido a la vista: el rival cierra
            // linea en el acto. Evitarlas es juego basico, no profundidad. La libertad que
            // importa es la que queda DESPUES de descartarlas.
            var consideradas = Enumerable.Range(0, unicos.Count)
                .Where(i => !((int)res[i].r * mio < 0 && res[i].d <= 2)).ToList();
            int descuidos = unicos.Count - consideradas.Count;
            double frac = consideradas.Count == 0 ? 0
                        : (double)viables.Count(i => consideradas.Contains(i)) / consideradas.Count;
            perfil.Add(frac);

            string quien = turn == GameSpec.White ? "arranca" : "segundo";
            Console.WriteLine($"{ply,4} {quien,-9} {aspira,-10} " +
                              $"{viables.Count + "/" + consideradas.Count,8} {frac,4:P0}  " +
                              $"{descuidos,3}  {spec.DescribeMove(p, unicos[elegido], turn),-22}");

            // El numero de arriba resume; esto muestra a que lleva cada jugada disponible,
            // que es lo que de verdad determina si el jugador tiene decisiones o no.
            if (Flag(o, "detalle")) {
                var hist = Enumerable.Range(0, unicos.Count)
                    .GroupBy(i => (res[i].r, res[i].d))
                    .OrderByDescending(g => (int)g.Key.r * mio)
                    .ThenByDescending(g => (int)g.Key.r * mio > 0 ? -g.Key.d : g.Key.d)
                    .Select(g => {
                        int signo = (int)g.Key.r * mio;
                        string et = signo > 0 ? "gana" : signo < 0 ? "pierde" : "tablas";
                        return $"{et} en {g.Key.d} ({g.Count()})";
                    });
                Console.WriteLine($"       {string.Join("   ", hist)}");
            }

            p = spec.Apply(p, unicos[elegido]);
            if (spec.WinnerAfter(p, turn) != null) {
                Console.WriteLine($"     -> termina en el ply {ply}");
                break;
            }
            turn = 1 - turn;
        }

        Console.WriteLine();
        Console.WriteLine($"Libertad efectiva promedio en los primeros {perfil.Count} plies: {perfil.Average(),3:P0}");
        var apretados = perfil.Select((v, i) => (v, i)).Where(x => x.v <= 0.2).ToList();
        Console.WriteLine(apretados.Count == 0
            ? "Ningun turno con 20 % o menos de opciones: no hay jugada forzada temprana."
            : $"Turnos con 20 % o menos de opciones: {string.Join(", ", apretados.Select(x => x.i + 1))}");
        return 0;
    }

    // -------------------------------------------------------------- practica

    /// <summary>
    /// Tasa de victoria con jugadores de vision limitada. Con juego perfecto un par fijo es
    /// 100/0 y no existe ninguna tasa; la pregunta util es otra: si los dos jugadores calculan
    /// solo unos pocos turnos hacia adelante — el alcance de una persona en una mesa — y
    /// eligen al azar entre las jugadas que les parecen equivalentes, ¿quien gana y cuanto?
    ///
    /// El modelo de jugador es deliberadamente simple y sin heuristica posicional: ve
    /// exactamente N plies, y entre lo que a esa distancia se ve igual, elige al azar. Es una
    /// caricatura de un humano, no un humano. Sirve para comparar configuraciones entre si,
    /// no para predecir el resultado de una partida concreta.
    /// </summary>
    private static int Practica(Dictionary<string, string> o) {
        var white = ParseSet(Str(o, "white", "122335"));
        var black = ParseSet(Str(o, "black", "12246"));
        int games = Int(o, "games", 400);
        int maxPlies = Int(o, "max-plies", 40);
        int seed = Int(o, "seed", 20260727);
        var profundidades = Str(o, "depths", "2,4,6")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

        var spec = Spec(o, white, black);
        Console.WriteLine($"{spec.WhiteLabel} (arranca) contra {spec.BlackLabel}"
                          + (spec.Libre ? "   [variante sin tablero]" : ""));
        Console.WriteLine($"{games} partidas por nivel de vision, eligiendo al azar entre jugadas equivalentes.");
        Console.WriteLine();
        Console.WriteLine($"{"ve",4} {"gana el 1o",12} {"gana el 2o",12} {"sin definir",12} {"turnos",8}");
        Console.WriteLine(new string('-', 52));

        // En la variante interesa ademas si el tablero flotante se nota durante la partida o
        // se clava enseguida y el resto se juega como el de siempre: cuando queda definido el
        // 3x3 por primera vez, cuantos turnos pasan sin definir, y si vuelve a soltarse.
        var pinPrimero = new List<int>();
        var pliesSinDefinir = new List<int>();
        int redefiniciones = 0, partidasQueRedefinen = 0;

        foreach (int vision in profundidades) {
            int w = 0, b = 0, nada = 0;
            long turnos = 0;
            object candado = new();

            Parallel.For(0, games, g => {
                var rng = new Random(seed + g * 7919 + vision * 104729);
                var searcher = new Searcher(spec, 18);
                ulong p = spec.InitialPosition();
                int turn = GameSpec.White;
                int ply = 0;
                int? ganador = null;
                int pin = -1, sinDefinir = 0, sueltas = 0;
                bool estuvoPinchado = false;

                while (ply < maxPlies) {
                    var buf = new int[256];
                    int n = spec.GenerateMoves(p, turn, buf.AsSpan());
                    if (n == 0) {   // ahogado: pierde el que no puede mover
                        ganador = turn == GameSpec.White ? -1 : 1;
                        break;
                    }

                    int mio = turn == GameSpec.White ? 1 : -1;
                    var valores = new int[n];
                    for (int i = 0; i < n; i++) {
                        ulong c = spec.Apply(p, buf[i]);
                        Outcome? term = spec.WinnerAfter(c, turn);
                        valores[i] = term != null ? (int)term.Value * mio
                                   : (int)searcher.SolveFrom(c, 1 - turn, vision - 1).result * mio;
                    }
                    int mejor = valores.Max();
                    var opciones = Enumerable.Range(0, n).Where(i => valores[i] == mejor).ToList();
                    int mv = buf[opciones[rng.Next(opciones.Count)]];

                    p = spec.Apply(p, mv);
                    ply++;

                    if (spec.Libre) {
                        var (alto, ancho) = spec.Caja(p);
                        bool definido = alto == 3 && ancho == 3;
                        if (!definido) sinDefinir++;
                        if (definido && pin < 0) pin = ply;
                        if (definido) estuvoPinchado = true;
                        else if (estuvoPinchado) { sueltas++; estuvoPinchado = false; }
                    }

                    Outcome? fin = spec.WinnerAfter(p, turn);
                    if (fin != null) { ganador = (int)fin.Value; break; }
                    turn = 1 - turn;
                }

                lock (candado) {
                    if (ganador == 1) w++;
                    else if (ganador == -1) b++;
                    else nada++;
                    turnos += ply;
                    if (spec.Libre) {
                        if (pin > 0) pinPrimero.Add(pin);
                        pliesSinDefinir.Add(sinDefinir);
                        redefiniciones += sueltas;
                        if (sueltas > 0) partidasQueRedefinen++;
                    }
                }
            });

            Console.WriteLine($"{vision,4} {100.0 * w / games,11:F1}% {100.0 * b / games,11:F1}% " +
                              $"{100.0 * nada / games,11:F1}% {(double)turnos / games,8:F1}");
        }
        Console.WriteLine();
        Console.WriteLine("'ve' = cuantos plies calcula cada jugador hacia adelante.");
        Console.WriteLine("Con vision infinita el resultado seria 100/0 para uno de los dos.");

        if (spec.Libre && pliesSinDefinir.Count > 0) {
            int total = pliesSinDefinir.Count;
            Console.WriteLine();
            Console.WriteLine("El tablero flotante, medido sobre esas mismas partidas:");
            Console.WriteLine($"  queda definido el 3x3 en el ply         {(pinPrimero.Count > 0 ? pinPrimero.Average() : 0),5:F1}   " +
                              $"(nunca en el {100.0 * (total - pinPrimero.Count) / total:F0} % de las partidas)");
            Console.WriteLine($"  turnos jugados con el 3x3 sin definir   {pliesSinDefinir.Average(),5:F1}");
            Console.WriteLine($"  partidas donde vuelve a soltarse        {100.0 * partidasQueRedefinen / total,5:F1} %   " +
                              $"({(double)redefiniciones / total:F2} veces por partida)");
        }
        return 0;
    }

    // ------------------------------------------------------------- practicas

    /// <summary>
    /// Una partida simulada con jugadores de vision limitada. Es el nucleo de `practica`,
    /// extraido para poder correr muchos enfrentamientos y muchas reglas en un solo proceso.
    /// Devuelve +1 gana el primero, -1 gana el segundo, 0 sin definir, y los plies jugados.
    /// </summary>
    private static (int ganador, int plies) UnaPartida(GameSpec spec, Searcher searcher, Random rng,
                                                       int vision, int maxPlies) {
        ulong p = spec.InitialPosition();
        int turn = GameSpec.White, ply = 0;
        var buf = new int[256];

        while (ply < maxPlies) {
            int n = spec.GenerateMoves(p, turn, buf.AsSpan());
            if (n == 0) return (turn == GameSpec.White ? -1 : 1, ply);   // ahogado

            int mio = turn == GameSpec.White ? 1 : -1;
            var valores = new int[n];
            for (int i = 0; i < n; i++) {
                ulong c = spec.Apply(p, buf[i]);
                Outcome? term = spec.WinnerAfter(c, turn);
                valores[i] = term != null ? (int)term.Value * mio
                           : (int)searcher.SolveFrom(c, 1 - turn, vision - 1).result * mio;
            }
            int mejor = valores.Max();
            var opciones = Enumerable.Range(0, n).Where(i => valores[i] == mejor).ToList();

            p = spec.Apply(p, buf[opciones[rng.Next(opciones.Count)]]);
            ply++;
            Outcome? fin = spec.WinnerAfter(p, turn);
            if (fin != null) return ((int)fin.Value, ply);
            turn = 1 - turn;
        }
        return (0, ply);
    }

    /// <summary>
    /// El reparto real de muchos enfrentamientos y de las dos reglas en una sola corrida, para
    /// poder buscar una configuracion que sirva a las dos: que con tablero se incline al
    /// segundo y sin tablero al primero. Lee pares "blancas negras" de un archivo.
    /// </summary>
    private static int Practicas(Dictionary<string, string> o) {
        string pairsFile = Str(o, "pairs-file", "");
        int games = Int(o, "games", 2000);
        int maxPlies = Int(o, "max-plies", 40);
        int seed = Int(o, "seed", 20260727);
        string outPath = Str(o, "out", "practico.csv");
        var visiones = Str(o, "depths", "4")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

        var pares = new List<(int[] w, int[] b)>();
        foreach (var linea in File.ReadAllLines(pairsFile)) {
            var t = linea.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 2 || linea.TrimStart().StartsWith("#")) continue;
            pares.Add((ParseSet(t[0]), ParseSet(t[1])));
        }

        // Las reglas a comparar: el juego de siempre y la variante tal como la pidan las banderas.
        var reglas = new List<(string nombre, bool libre)> { ("fijo", false), ("libre", true) };
        var tareas = (from par in pares from reg in reglas from v in visiones
                      select (par, reg, v)).ToList();

        Console.WriteLine($"{pares.Count} enfrentamientos x {reglas.Count} reglas x {visiones.Length} visiones " +
                          $"= {tareas.Count} corridas de {games} partidas.");

        var filas = new ConcurrentBag<(string w, string b, string regla, int v, double g1, double g2, double nd, double t)>();
        int hechas = 0;
        var clock = Stopwatch.StartNew();

        Parallel.ForEach(tareas, tarea => {
            var (par, reg, vision) = tarea;
            var spec = new GameSpec(par.w, par.b, reg.libre,
                                    pegado: Flag(o, "pegado"), pegadoOrto: Flag(o, "pegado-orto"),
                                    pegadoSiempre: Flag(o, "pegado-siempre"));
            var searcher = new Searcher(spec, 18);
            int w = 0, b = 0, nada = 0; long turnos = 0;
            for (int g = 0; g < games; g++) {
                var rng = new Random(seed + g * 7919 + vision * 104729);
                var (ganador, plies) = UnaPartida(spec, searcher, rng, vision, maxPlies);
                if (ganador == 1) w++; else if (ganador == -1) b++; else nada++;
                turnos += plies;
            }
            filas.Add((spec.WhiteLabel, spec.BlackLabel, reg.nombre, vision,
                       100.0 * w / games, 100.0 * b / games, 100.0 * nada / games, (double)turnos / games));

            int d = System.Threading.Interlocked.Increment(ref hechas);
            if (d % 20 == 0 || d == tareas.Count)
                Console.WriteLine($"  {d,5}/{tareas.Count}  ({clock.Elapsed.TotalSeconds:F0}s)");
        });

        using (var f = new StreamWriter(outPath)) {
            f.WriteLine("white,black,regla,ve,gana1,gana2,sindef,turnos");
            foreach (var r in filas.OrderBy(r => r.w).ThenBy(r => r.b).ThenBy(r => r.regla).ThenBy(r => r.v))
                f.WriteLine(string.Join(",", r.w, r.b, r.regla, r.v,
                    r.g1.ToString("F2", CultureInfo.InvariantCulture),
                    r.g2.ToString("F2", CultureInfo.InvariantCulture),
                    r.nd.ToString("F2", CultureInfo.InvariantCulture),
                    r.t.ToString("F2", CultureInfo.InvariantCulture)));
        }
        Console.WriteLine($"Escrito {outPath} ({filas.Count} filas, {clock.Elapsed.TotalSeconds:F0}s)");
        return 0;
    }

    // --------------------------------------------------------------- trampas

    /// <summary>
    /// Mide lo que `libertad` no ve. `libertad` recorre la linea optima y cuenta opciones; esto
    /// juega partidas de verdad y, en cada turno, compara lo que el jugador ve con lo que pasa
    /// en realidad. Distingue tres cosas que no son lo mismo:
    ///
    ///  - **forzado**: en la posicion hay una sola jugada que conserva lo mejor. Puede estar a
    ///    la vista o no.
    ///  - **trampa**: ademas de forzada, esa jugada le parece *igual* a otras que pierden, con
    ///    lo cual el jugador la puede errar sin enterarse. Es el caso que preocupa.
    ///  - **riesgo**: la probabilidad de errarle en ese turno, o sea que fraccion de lo que al
    ///    jugador le parece optimo en realidad pierde.
    ///
    /// Solo se cuentan los turnos en que el jugador todavia no esta perdido: si ya perdio con
    /// juego perfecto, que haya una sola jugada "mejor" no significa nada.
    /// </summary>
    private static int Trampas(Dictionary<string, string> o) {
        var white = ParseSet(Str(o, "white", "13344"));
        var black = ParseSet(Str(o, "black", "12345"));
        int games = Int(o, "games", 300);
        int vision = Int(o, "vision", 4);
        int verdad = Int(o, "verdad", 8);
        int maxPlies = Int(o, "max-plies", 40);
        int seed = Int(o, "seed", 20260727);

        var spec = Spec(o, white, black);
        Console.WriteLine($"{spec.WhiteLabel} (arranca) contra {spec.BlackLabel}" +
                          (spec.Libre ? "   [sin tablero]" : "   [con tablero]"));
        Console.WriteLine($"{games} partidas. El jugador ve {vision} plies; la verdad se mide a {verdad}.");

        var totales = new long[2];
        var forzados = new long[2];
        var trampas = new long[2];
        var riesgo = new double[2];
        var perdidos = new long[2];
        long plies = 0;
        object candado = new();

        Parallel.For(0, games, g => {
            var rng = new Random(seed + g * 7919);
            var corto = new Searcher(spec, 18);
            var largo = new Searcher(spec, 20);
            ulong p = spec.InitialPosition();
            int turn = GameSpec.White, ply = 0;
            var buf = new int[256];
            var t = new long[2]; var f = new long[2]; var tr = new long[2]; var ri = new double[2];
            var pe = new long[2];

            while (ply < maxPlies) {
                int n = spec.GenerateMoves(p, turn, buf.AsSpan());
                if (n == 0) break;
                int mio = turn == GameSpec.White ? 1 : -1;

                var vCorto = new int[n];
                var vLargo = new int[n];
                for (int i = 0; i < n; i++) {
                    ulong c = spec.Apply(p, buf[i]);
                    Outcome? term = spec.WinnerAfter(c, turn);
                    if (term != null) { vCorto[i] = vLargo[i] = (int)term.Value * mio; continue; }
                    vCorto[i] = (int)corto.SolveFrom(c, 1 - turn, vision - 1).result * mio;
                    vLargo[i] = (int)largo.SolveFrom(c, 1 - turn, verdad - 1).result * mio;
                }

                int mejorLargo = vLargo.Max();
                int mejorCorto = vCorto.Max();
                var creeOptimas = Enumerable.Range(0, n).Where(i => vCorto[i] == mejorCorto).ToList();

                // Solo tiene sentido hablar de jugada forzada si todavia hay algo que salvar.
                if (mejorLargo > -1) {
                    int salvan = Enumerable.Range(0, n).Count(i => vLargo[i] == mejorLargo);
                    int malasQueCreeBuenas = creeOptimas.Count(i => vLargo[i] != mejorLargo);
                    t[turn]++;
                    if (salvan == 1) f[turn]++;
                    if (salvan == 1 && malasQueCreeBuenas > 0) tr[turn]++;
                    ri[turn] += (double)malasQueCreeBuenas / creeOptimas.Count;
                } else pe[turn]++;   // ya perdido a la profundidad de la verdad

                p = spec.Apply(p, buf[creeOptimas[rng.Next(creeOptimas.Count)]]);
                ply++;
                if (spec.WinnerAfter(p, turn) != null) break;
                turn = 1 - turn;
            }

            lock (candado) {
                for (int k = 0; k < 2; k++) {
                    totales[k] += t[k]; forzados[k] += f[k]; trampas[k] += tr[k];
                    riesgo[k] += ri[k]; perdidos[k] += pe[k];
                }
                plies += ply;
            }
        });

        Console.WriteLine();
        Console.WriteLine($"Plies por partida: {(double)plies / games:F1}");
        Console.WriteLine();
        Console.WriteLine($"{"",-10} {"turnos",8} {"ya perdido",11} {"forzados",12} {"trampas",12} {"riesgo/turno",14}");
        Console.WriteLine(new string('-', 72));
        for (int k = 0; k < 2; k++) {
            string quien = k == GameSpec.White ? "arranca" : "segundo";
            Console.WriteLine($"{quien,-10} {totales[k],8:N0} " +
                              $"{100.0 * perdidos[k] / (totales[k] + perdidos[k]),10:F1}% " +
                              $"{100.0 * forzados[k] / totales[k],11:F1}% " +
                              $"{100.0 * trampas[k] / totales[k],11:F1}% " +
                              $"{100.0 * riesgo[k] / totales[k],13:F1}%");
        }
        long tt = totales[0] + totales[1];
        long pp = perdidos[0] + perdidos[1];
        Console.WriteLine($"{"los dos",-10} {tt,8:N0} " +
                          $"{100.0 * pp / (tt + pp),10:F1}% " +
                          $"{100.0 * (forzados[0] + forzados[1]) / tt,11:F1}% " +
                          $"{100.0 * (trampas[0] + trampas[1]) / tt,11:F1}% " +
                          $"{100.0 * (riesgo[0] + riesgo[1]) / tt,13:F1}%");
        Console.WriteLine();
        Console.WriteLine("'ya perdido' = turnos en que no queda nada que salvar, excluidos del resto.");
        Console.WriteLine("forzados = una sola jugada conserva lo mejor.  trampas = ademas es indistinguible");
        Console.WriteLine("de otra que pierde, para el que ve " + vision + " plies.  riesgo = chance de errarle ese turno.");
        return 0;
    }

    private static string Verdict(Outcome o) => o switch {
        Outcome.WhiteWin => "gana BLANCO",
        Outcome.BlackWin => "gana NEGRO",
        _ => "tablas",
    };

    // ------------------------------------------------------------------ sweep

    private static IEnumerable<int[]> Multisets(int size, int maxRank, int min = 1) {
        if (size == 0) { yield return Array.Empty<int>(); yield break; }
        for (int r = min; r <= maxRank; r++)
            foreach (var rest in Multisets(size - 1, maxRank, r))
                yield return new[] { r }.Concat(rest).ToArray();
    }

    private static int Sweep(Dictionary<string, string> o) {
        int maxDepth = Math.Min(Int(o, "max-depth", 14), 60);
        int ttBits = Int(o, "tt-bits", 22);
        long budget = Int(o, "budget-ms", 60000);
        int limit = Int(o, "limit", 0);
        int sumWindow = Int(o, "sum-window", 4);
        string outPath = Str(o, "out", "balance.csv");
        bool libre = Flag(o, "libre");
        bool pegadoS = Flag(o, "pegado"), pegadoOrtoS = Flag(o, "pegado-orto"), pegadoSiempreS = Flag(o, "pegado-siempre");

        List<int[]> whites, blacks;
        if (o.ContainsKey("white-sets") && o.ContainsKey("black-sets")) {
            whites = Str(o, "white-sets", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(ParseSet).ToList();
            blacks = Str(o, "black-sets", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(ParseSet).ToList();
        } else {
            int wSize = Int(o, "gen-white-size", 6);
            int bSize = Int(o, "gen-black-size", 5);
            int maxRank = Int(o, "max-rank", 6);
            whites = Multisets(wSize, maxRank).ToList();
            blacks = Multisets(bSize, maxRank).ToList();
        }

        var pairs = new List<(int[] w, int[] b)>();
        foreach (var w in whites)
            foreach (var b in blacks) {
                if (w.Length + b.Length > 16) continue;
                if (sumWindow > 0 && Math.Abs(w.Sum() - b.Sum()) > sumWindow) continue;
                pairs.Add((w, b));
            }
        if (limit > 0 && pairs.Count > limit) pairs = pairs.Take(limit).ToList();

        Console.WriteLine($"Barriendo {pairs.Count:N0} enfrentamientos, hasta {maxDepth} plies, {budget} ms cada uno."
                          + (libre ? "  [variante sin tablero]" : ""));
        Console.WriteLine($"Hilos: {Environment.ProcessorCount}");

        var rows = new ConcurrentBag<string>();
        int done = 0;
        var clock = Stopwatch.StartNew();

        Parallel.ForEach(pairs, pair => {
            var spec = new GameSpec(pair.w, pair.b, libre,
                                    pegado: pegadoS, pegadoOrto: pegadoOrtoS, pegadoSiempre: pegadoSiempreS);
            var searcher = new Searcher(spec, ttBits);
            Outcome result = Outcome.Draw;
            int reached = 0;
            var t0 = Stopwatch.StartNew();
            for (int depth = 1; depth <= maxDepth; depth++) {
                try { result = searcher.Solve(depth, budget); }
                catch (SearchAborted) { break; }
                reached = depth;
                if (result != Outcome.Draw) break;
            }
            string verdict = result == Outcome.WhiteWin ? "white"
                           : result == Outcome.BlackWin ? "black"
                           : reached >= maxDepth ? "draw" : "unknown";
            rows.Add(string.Join(",",
                spec.WhiteLabel, spec.BlackLabel,
                pair.w.Sum().ToString(CultureInfo.InvariantCulture),
                pair.b.Sum().ToString(CultureInfo.InvariantCulture),
                verdict, reached.ToString(),
                searcher.Nodes.ToString(),
                t0.ElapsedMilliseconds.ToString()));

            int d = System.Threading.Interlocked.Increment(ref done);
            if (d % 25 == 0 || d == pairs.Count)
                Console.WriteLine($"  {d,6}/{pairs.Count}  ({clock.Elapsed.TotalSeconds:F0}s)");
        });

        var sorted = rows.OrderBy(r => r).ToList();
        using (var f = new StreamWriter(outPath)) {
            f.WriteLine("white,black,white_sum,black_sum,verdict,plies,nodes,ms");
            foreach (var r in sorted) f.WriteLine(r);
        }

        var counts = sorted.GroupBy(r => r.Split(',')[4]).ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine();
        Console.WriteLine($"Escrito {outPath} ({sorted.Count:N0} filas, {clock.Elapsed.TotalSeconds:F0}s)");
        foreach (var kv in counts.OrderByDescending(k => k.Value))
            Console.WriteLine($"  {kv.Key,-8} {kv.Value,6:N0}  ({100.0 * kv.Value / sorted.Count:F1}%)");
        return 0;
    }
}
