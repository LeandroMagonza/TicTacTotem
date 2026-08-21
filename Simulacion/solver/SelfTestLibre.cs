using System;
using System.Collections.Generic;
using System.Linq;

namespace TatetiSolver;

/// <summary>
/// Tests de la variante sin tablero (Libre.cs). Son aparte de SelfTest.cs a proposito: aquel
/// verifica el juego que ya esta analizado y no se toca, este verifica la regla nueva.
///
/// Lo que hay que probar es que la caja de 3x3 se respete y que se pueda redefinir, que el
/// encuadre no invente ni pierda posiciones, y que sacando la regla nueva del medio el juego
/// siga siendo el mismo de siempre.
/// </summary>
public static class SelfTestLibre {
    private static int _failed;

    private static void Check(string name, bool ok) {
        Console.WriteLine($"  [{(ok ? "ok " : "FALLA")}] {name}");
        if (!ok) _failed++;
    }

    private static ulong Put(GameSpec s, ulong p, int owner, int rank, int cell) {
        for (int i = 0; i < s.PieceCount; i++)
            if (s.Owner[i] == owner && s.Rank[i] == rank && GameSpec.Loc(p, i) == GameSpec.Hand)
                return GameSpec.WithLoc(p, i, cell);
        throw new InvalidOperationException($"no queda pieza libre {owner}/{rank}");
    }

    /// <summary>Las posiciones distintas a las que puede llevar el turno de un jugador.</summary>
    private static HashSet<ulong> Hijos(GameSpec s, ulong p, int turn) {
        var buf = new int[256];
        int n = s.GenerateMoves(p, turn, buf.AsSpan());
        return Enumerable.Range(0, n).Select(i => s.Canonical(s.Apply(p, buf[i]))).ToHashSet();
    }

    private static int Jugadas(GameSpec s, ulong p, int turn) {
        var buf = new int[256];
        return s.GenerateMoves(p, turn, buf.AsSpan());
    }

    /// <summary>Alto x ancho de la caja de cada posicion alcanzable en `plies` turnos.</summary>
    private static void Recorrer(GameSpec s, ulong p, int turn, int plies, Action<ulong> visita) {
        visita(p);
        if (plies == 0) return;
        var buf = new int[256];
        int n = s.GenerateMoves(p, turn, buf.AsSpan());
        for (int i = 0; i < n; i++) {
            ulong c = s.Apply(p, buf[i]);
            if (s.WinnerAfter(c, turn) != null) { visita(c); continue; }
            Recorrer(s, c, 1 - turn, plies - 1, visita);
        }
    }

    public static bool Run() {
        var s = new GameSpec(new[] { 1, 2, 3, 4, 4 }, new[] { 1, 1, 2, 4, 5 }, libre: true);
        int W = GameSpec.White, B = GameSpec.Black;
        ulong empty = s.InitialPosition();

        Console.WriteLine("La caja de 3x3");

        // Todo lo puesto tiene que entrar en algun 3x3. Es la unica regla nueva.
        {
            bool ok = true;
            var cajas = new HashSet<(int, int)>();
            Recorrer(s, empty, W, 5, p => {
                var (alto, ancho) = s.Caja(p);
                cajas.Add((alto, ancho));
                if (alto > 3 || ancho > 3) ok = false;
                for (int i = 0; i < s.PieceCount; i++) {
                    int l = GameSpec.Loc(p, i);
                    if (l != GameSpec.Hand && (l < 0 || l >= 9)) ok = false;
                }
            });
            Check("ninguna posicion alcanzable se sale del 3x3 (5 plies exhaustivos)", ok);
            // Y la caja crece de verdad: si siempre fuera 3x3 la regla no estaria haciendo nada.
            Check("aparecen cajas de varios tamaños, no solo la de 3x3",
                  cajas.Contains((1, 1)) && cajas.Contains((1, 2)) && cajas.Contains((3, 3)) && cajas.Count >= 6);
        }

        Console.WriteLine("La primera pieza y la segunda");

        // Sin nada puesto no hay lugares: elegir donde no es una decision, solo elegir pieza.
        Check("la primera jugada es solo elegir pieza (4 rangos distintos, 4 jugadas)",
              Jugadas(s, empty, W) == 4 && Hijos(s, empty, W).Count == 4);

        // La segunda pieza puede caer en cualquier casilla a distancia <= 2 en las dos
        // direcciones: es todo lo que la caja de 3x3 tolera. 5x5 menos la casilla ocupada.
        ulong una = s.Apply(empty, FirstMove(s, empty, W));
        {
            var buf = new int[256];
            int n = s.GenerateMoves(una, B, buf.AsSpan());
            int colocaciones = Enumerable.Range(0, n)
                .Count(i => GameSpec.Loc(una, s.PieceOf(buf[i])) == GameSpec.Hand);
            // 3 rangos distintos en la mano de negro (1,2,4,5 -> 4 grupos) x 24 casillas.
            Check("la segunda pieza tiene 24 destinos por rango (5x5 menos la ocupada)",
                  colocaciones == 4 * 24);
            // Modulo simetria del plano son 5: al lado, salteando una, y las tres diagonales.
            Check("modulo simetria hay 5 segundas jugadas por rango",
                  Hijos(s, una, B).Count == 4 * 5);
        }

        Console.WriteLine("El tablero se define de a poco y se puede redefinir");

        // Dos piezas en columnas contiguas: la tercera columna todavia puede caer de los dos
        // lados. Dos piezas en diagonal salteando una: el 3x3 ya quedo definido entero.
        {
            var s2 = new GameSpec(new[] { 1, 2 }, new[] { 3, 4 }, libre: true);
            ulong contiguas = Put(s2, Put(s2, s2.InitialPosition(), W, 1, 0), B, 3, 1);  // (0,0) y (0,1)
            var (a1, n1) = s2.Caja(contiguas);
            Check("dos piezas al lado dejan la caja en 1x2: falta definir filas y una columna",
                  a1 == 1 && n1 == 2);

            ulong diagonal = Put(s2, Put(s2, s2.InitialPosition(), W, 1, 0), B, 3, 8);   // (0,0) y (2,2)
            var (a2, n2) = s2.Caja(diagonal);
            Check("dos piezas en diagonal salteando una definen el 3x3 entero",
                  a2 == 3 && n2 == 3);

            // Con el 3x3 definido no se puede colocar afuera: solo quedan las 7 casillas libres.
            var buf = new int[256];
            int n = s2.GenerateMoves(diagonal, W, buf.AsSpan());
            int colocaciones = Enumerable.Range(0, n)
                .Count(i => GameSpec.Loc(diagonal, s2.PieceOf(buf[i])) == GameSpec.Hand);
            Check("con el 3x3 definido solo se coloca adentro (7 casillas libres)", colocaciones == 7);

            // Y si las piezas se juntan, el 3x3 se suelta y vuelve a poder definirse en otro
            // lado: la de (2,2) sube a (1,2), la caja pasa a 2x3 y reaparecen destinos afuera.
            ulong suelto = Mover(s2, diagonal, B, 3, 8, 5);   // (2,2) -> (1,2)
            var (a3, n3) = s2.Caja(suelto);
            Check("al juntarse las piezas la caja se achica y el 3x3 queda sin definir",
                  a3 == 2 && n3 == 3);
            bool afuera = Enumerable.Range(0, s2.GenerateMoves(suelto, W, buf.AsSpan()))
                .Any(i => {
                    ulong hijo = s2.Apply(suelto, buf[i]);
                    var (h, w) = s2.Caja(hijo);
                    return h == 3 && w == 3;
                });
            Check("y el 3x3 puede rearmarse en una disposicion distinta", afuera);
        }

        Console.WriteLine("El encuadre");

        // Encuadrar es solo mirar la misma posicion desde otro lado: no puede cambiar quien gana.
        {
            var s3 = new GameSpec(new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, libre: true);
            ulong linea = Put(s3, Put(s3, Put(s3, s3.InitialPosition(), W, 1, 0), W, 2, 1), W, 3, 2);
            Check("la linea de tres sigue ganando en el encuadre", s3.WinnerAfter(linea, W) == Outcome.WhiteWin);

            // La misma linea corrida una fila abajo es la misma posicion.
            ulong corrida = Put(s3, Put(s3, Put(s3, s3.InitialPosition(), W, 1, 3), W, 2, 4), W, 3, 5);
            Check("una posicion y su traslado tienen el mismo canonico",
                  s3.Canonical(linea) == s3.Canonical(corrida));

            // Pero en el juego normal son posiciones distintas: el tablero esta clavado.
            var s3fijo = new GameSpec(new[] { 1, 2, 3 }, new[] { 4, 5, 6 });
            Check("en el juego normal ese mismo traslado es otra posicion",
                  s3fijo.Canonical(linea) != s3fijo.Canonical(corrida));

            // El encuadre no puede perder piezas ni apilar dos donde habia una.
            // El encuadre corre la posicion contra la esquina: las casillas ocupadas tienen
            // que entrar en la caja, y la caja tiene que estar pegada a la fila 0 y la
            // columna 0. Si alguna de las dos cosas fallara, el ulong estaria guardando una
            // posicion que no es la que se jugo.
            bool sano = true;
            Recorrer(s3, s3.InitialPosition(), W, 4, p => {
                var (alto, ancho) = s3.Caja(p);
                if (CasillasOcupadas(s3, p) > alto * ancho) sano = false;
                int minR = 3, minC = 3;
                for (int i = 0; i < s3.PieceCount; i++) {
                    int l = GameSpec.Loc(p, i);
                    if (l == GameSpec.Hand) continue;
                    minR = Math.Min(minR, l / 3);
                    minC = Math.Min(minC, l % 3);
                }
                if (alto > 0 && (minR != 0 || minC != 0)) sano = false;
            });
            Check("el encuadre deja todo pegado a la esquina y dentro de la caja (4 plies)", sano);
        }

        Console.WriteLine("Lo que no cambia");

        // Con el 3x3 ya definido y lleno de bordes, la variante tiene que jugarse igual que el
        // juego normal: las mismas piezas, las mismas casillas, las mismas jugadas.
        {
            var fijo = new GameSpec(new[] { 1, 2, 3, 4, 4 }, new[] { 1, 1, 2, 4, 5 });
            var libre = new GameSpec(new[] { 1, 2, 3, 4, 4 }, new[] { 1, 1, 2, 4, 5 }, libre: true);
            // Caja 3x3 clavada por las cuatro esquinas ocupadas.
            ulong p = fijo.InitialPosition();
            p = Put(fijo, p, W, 1, 0); p = Put(fijo, p, B, 1, 2);
            p = Put(fijo, p, W, 2, 6); p = Put(fijo, p, B, 1, 8);
            Check("con el 3x3 clavado las dos variantes generan las mismas jugadas",
                  Hijos(fijo, p, W).SetEquals(Hijos(libre, p, W)));

            // Tapar, destapar y el medio punto son los mismos de siempre.
            ulong tapada = Put(libre, Put(libre, Put(libre, libre.InitialPosition(), B, 1, 0), B, 1, 1), B, 2, 2);
            tapada = Put(libre, tapada, W, 4, 0);
            Check("destapar linea rival sigue haciendo perder al que mueve",
                  libre.WinnerAfter(tapada, W) == null &&
                  libre.WinnerAfter(Mover(libre, tapada, W, 4, 0, 3), W) == Outcome.BlackWin);
        }

        Console.WriteLine("Sub-variante: la pieza nueva tiene que tocar a alguna puesta");

        {
            var pegado = new GameSpec(new[] { 1, 2, 3, 4, 4 }, new[] { 1, 1, 2, 4, 5 },
                                      libre: true, pegado: true);
            var orto = new GameSpec(new[] { 1, 2, 3, 4, 4 }, new[] { 1, 1, 2, 4, 5 },
                                    libre: true, pegado: true, pegadoOrto: true);
            ulong unaP = pegado.Apply(pegado.InitialPosition(), FirstMove(pegado, pegado.InitialPosition(), W));

            var buf = new int[256];
            int Colocaciones(GameSpec sp, ulong p, int turno) {
                int n = sp.GenerateMoves(p, turno, buf.AsSpan());
                return Enumerable.Range(0, n).Count(i => GameSpec.Loc(p, sp.PieceOf(buf[i])) == GameSpec.Hand);
            }
            // 4 grupos en la mano del segundo x las 8 casillas que rodean a la primera pieza.
            Check("pegado: la segunda pieza tiene 8 destinos por rango, no 24",
                  Colocaciones(pegado, unaP, B) == 4 * 8);
            Check("pegado-orto: tiene 4", Colocaciones(orto, unaP, B) == 4 * 4);

            // La jugada con la que se explicaba la regla —diagonal salteando una— deja de existir.
            bool hayDiagonalLarga = Enumerable.Range(0, pegado.GenerateMoves(unaP, B, buf.AsSpan()))
                .Any(i => { var (a, an) = pegado.Caja(pegado.Apply(unaP, buf[i])); return a == 3 && an == 3; });
            bool hayDiagonalLargaSuelta = Enumerable.Range(0, s.GenerateMoves(una, B, buf.AsSpan()))
                .Any(i => { var (a, an) = s.Caja(s.Apply(una, buf[i])); return a == 3 && an == 3; });
            Check("pegado: la segunda pieza ya no puede definir el 3x3 de una",
                  !hayDiagonalLarga && hayDiagonalLargaSuelta);

            // Con la caja ya en 3x3 la exigencia se levanta y se juega como la variante suelta,
            // salvo que se pida --pegado-siempre.
            var siempre = new GameSpec(new[] { 1, 2, 3, 4, 4 }, new[] { 1, 1, 2, 4, 5 },
                                       libre: true, pegado: true, pegadoSiempre: true);
            ulong esquinas = s.InitialPosition();
            esquinas = Put(s, esquinas, W, 1, 0);      // (0,0)
            esquinas = Put(s, esquinas, B, 1, 8);      // (2,2) -> caja 3x3, y (0,2)/(2,0) aislados
            Check("pegado hasta definir: con el 3x3 hecho se coloca como en la variante suelta",
                  Hijos(pegado, esquinas, W).SetEquals(Hijos(s, esquinas, W)));
            Check("pegado-siempre: con el 3x3 hecho sigue exigiendo tocar",
                  Colocaciones(siempre, esquinas, W) < Colocaciones(pegado, esquinas, W));
        }

        Console.WriteLine("El buscador sobre la variante");

        // El minimax pelado y el optimizado tienen que coincidir tambien con la regla nueva:
        // es lo que prueba que la poda, la tabla y el encuadre no se estan comiendo nada.
        {
            var chico = new[] { 1, 2, 3 };
            var chico2 = new[] { 1, 2, 4 };
            bool ok = Verify.Run(chico, chico2, 5, libre: true);
            Check("el minimax pelado y el optimizado coinciden en la variante", ok);
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "Todo en orden." : $"{_failed} test(s) fallaron.");
        return _failed == 0;
    }

    private static int CasillasOcupadas(GameSpec s, ulong p) {
        var vistas = new HashSet<int>();
        for (int i = 0; i < s.PieceCount; i++) {
            int l = GameSpec.Loc(p, i);
            if (l != GameSpec.Hand) vistas.Add(l);
        }
        return vistas.Count;
    }

    private static int FirstMove(GameSpec s, ulong p, int turn) {
        var buf = new int[256];
        s.GenerateMoves(p, turn, buf.AsSpan());
        return buf[0];
    }

    /// <summary>Mueve la pieza de ese dueño y rango que esta en `from` hacia `to`, encuadrando.</summary>
    private static ulong Mover(GameSpec s, ulong p, int owner, int rank, int from, int to) {
        var buf = new int[256];
        int n = s.GenerateMoves(p, owner, buf.AsSpan());
        for (int i = 0; i < n; i++) {
            int piece = s.PieceOf(buf[i]);
            if (s.Rank[piece] != rank || GameSpec.Loc(p, piece) != from) continue;
            ulong hijo = s.Apply(p, buf[i]);
            if (GameSpec.Loc(hijo, piece) == to) return hijo;
        }
        throw new InvalidOperationException($"no hay jugada {rank} de {from} a {to}");
    }
}
