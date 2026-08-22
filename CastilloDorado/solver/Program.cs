using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CastilloSolver;

public static class Program {
    public static int Main(string[] args) {
        if (args.Length == 0) { Usage(); return 1; }
        var o = Opciones(args.Skip(1).ToArray());
        switch (args[0]) {
            case "selftest": return SelfTest.Run() ? 0 : 1;
            case "perft": return Perft(o);
            case "azar": return Azar(o);
            case "practica": return Practica(o);
            case "resolver": return Resolver(o);
            case "partida": return UnaPartida(o);
            case "comparar": return Comparar(o);
            default: Usage(); return 1;
        }
    }

    private static void Usage() {
        Console.WriteLine(@"
Simulador del juego de los cuatro edificios (tablero 4x4).

  selftest
      Comprueba que el motor haga lo que dicen las reglas.

  perft   [--prof 6]
      Cuenta el arbol completo hasta esa profundidad. Da el factor de ramificacion.

  azar    [--partidas 50000] [--semilla 1]
      Los dos jugadores juegan al azar entre sus jugadas legales. Mide la forma
      cruda del juego: cuanto dura, como termina, si hay sesgo de salida.

  practica [--partidas 400] [--plies 4] [--plies-negro N] [--semilla 1]
      Los dos miran N plies con una evaluacion de material y desempatan al azar.

  resolver [--max-prof 14] [--tt-bits 24]
      Profundizacion iterativa buscando victorias forzadas desde la posicion
      inicial. Un veredicto de victoria es real; ""no se"" no dice nada.

  partida [--plies 4] [--semilla 1] [--azar] [--json]
      Juega una sola partida y la imprime jugada por jugada. Con --json saca
      la secuencia entera de posiciones para poder dibujarla en otro lado.

  comparar [--partidas 20000] [--plies 4] [--partidas-practica 300]
      Corre todas las disposiciones iniciales y las variantes de regla y arma
      la tabla comparativa.

Reglas (en cualquier comando):
  --inicio esquinas|frentes|diagonal|centro   disposicion inicial (por defecto esquinas)
  --sacerdote-edificios    el sacerdote tambien convierte edificios enemigos
  --guerrero-queda         al tomar un edificio el guerrero no entra, se queda afuera
  --toma-libre             el guerrero entra a cualquier edificio enemigo, incluso de un tipo
                           que ya tenga: podes terminar con dos iguales, y el taller se toma
  --obra-libre             se cae la regla de que dos edificios no pueden estar pegados
  --castillo-libre         el no-pegado vale para los tres edificios pero no para el castillo
  --castillo-claim         el castillo se levanta neutral y en cualquier momento; gana el que
                           tenga los tres edificios Y una unidad metida adentro del castillo
  --sacerdote-reubica      el sacerdote convierte aunque ya tenga esa pieza: la muda ahi
  --repeticiones 3         cuantas repeticiones de una posicion son empate
  --plies-max 300          tope de plies antes de dar la partida por no resuelta
");
    }

    // ------------------------------------------------------------- opciones

    private static Dictionary<string, string> Opciones(string[] args) {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++) {
            if (!args[i].StartsWith("--")) continue;
            string k = args[i].Substring(2);
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) { d[k] = args[i + 1]; i++; }
            else d[k] = "true";
        }
        return d;
    }

    private static int Int(Dictionary<string, string> o, string k, int def)
        => o.TryGetValue(k, out string v) && int.TryParse(v, out int r) ? r : def;
    private static string Str(Dictionary<string, string> o, string k, string def)
        => o.TryGetValue(k, out string v) ? v : def;
    private static bool Flag(Dictionary<string, string> o, string k) => o.ContainsKey(k);

    private static Reglas Leer(Dictionary<string, string> o) => new Reglas {
        Inicio = Str(o, "inicio", "esquinas"),
        SacerdoteEdificios = Flag(o, "sacerdote-edificios"),
        GuerreroQueda = Flag(o, "guerrero-queda"),
        TomaLibre = Flag(o, "toma-libre"),
        ObraLibre = Flag(o, "obra-libre"),
        CastilloLibre = Flag(o, "castillo-libre"),
        CastilloClaim = Flag(o, "castillo-claim"),
        SacerdoteReubica = Flag(o, "sacerdote-reubica"),
        RepeticionesEmpate = Int(o, "repeticiones", 3),
        PliesMax = Int(o, "plies-max", 300),
    };

    // ---------------------------------------------------------------- perft

    private static int Perft(Dictionary<string, string> o) {
        var g = new Juego(Leer(o));
        int max = Int(o, "prof", 6);
        Console.WriteLine($"Disposicion {g.R.Etiqueta()}");
        Console.WriteLine(Juego.Dibujar(g.Inicial()));
        Console.WriteLine("  prof        nodos     hojas    ramas   terminales      ms");
        for (int d = 1; d <= max; d++) {
            var sw = Stopwatch.StartNew();
            long nodos = 0, hojas = 0, term = 0;
            Contar(g, g.Inicial(), Juego.Blanco, d, ref nodos, ref hojas, ref term);
            sw.Stop();
            double ramas = nodos > 1 ? Math.Pow(hojas, 1.0 / d) : 0;
            Console.WriteLine($"  {d,4} {nodos,12:N0} {hojas,9:N0}  {ramas,7:F2} {term,12:N0} {sw.ElapsedMilliseconds,7:N0}");
        }
        return 0;
    }

    private static void Contar(Juego g, ulong b, int turno, int prof, ref long nodos, ref long hojas, ref long term) {
        nodos++;
        if (g.Terminal(b, turno) != null) { term++; hojas++; return; }
        var buf = new int[Juego.MaxJugadas];
        int n = g.Jugadas(b, turno, buf.AsSpan());
        if (n == 0) { term++; hojas++; return; }
        if (prof == 0) { hojas++; return; }
        for (int i = 0; i < n; i++) Contar(g, g.Aplicar(b, turno, buf[i]), 1 - turno, prof - 1, ref nodos, ref hojas, ref term);
    }

    // ----------------------------------------------------------------- lote

    /// <summary>Corre un lote de partidas en paralelo y junta los resultados.</summary>
    private static Balance Lote(Reglas r, int partidas, ulong semilla, Func<Juego, Politica> blancas,
                                Func<Juego, Politica> negras) {
        int hilos = Math.Max(1, Environment.ProcessorCount - 1);
        var parciales = new Balance[hilos];
        Parallel.For(0, hilos, h => {
            var g = new Juego(r.Copia());
            var mesa = new Mesa(g);
            var pb = blancas(g);
            var pn = negras(g);
            var bal = new Balance();
            for (int i = h; i < partidas; i += hilos) {
                var rng = new Rng(semilla + (ulong)i * 0x9E3779B97F4A7C15UL);
                var reg = mesa.Jugar(pb, pn, ref rng);
                bal.Sumar(reg);
            }
            parciales[h] = bal;
        });
        var total = new Balance();
        foreach (var p in parciales) total.Fusionar(p);
        return total;
    }

    // ----------------------------------------------------------------- azar

    private static int Azar(Dictionary<string, string> o) {
        var r = Leer(o);
        int partidas = Int(o, "partidas", 50000);
        var sw = Stopwatch.StartNew();
        var bal = Lote(r, partidas, (ulong)Int(o, "semilla", 1), _ => new PoliticaAzar(), _ => new PoliticaAzar());
        sw.Stop();
        bal.Imprimir($"Juego al azar   [{r.Etiqueta()}]");
        Console.WriteLine($"  ({sw.ElapsedMilliseconds:N0} ms)");
        return 0;
    }

    // ------------------------------------------------------------- practica

    private static int Practica(Dictionary<string, string> o) {
        var r = Leer(o);
        int partidas = Int(o, "partidas", 400);
        int pb = Int(o, "plies", 4);
        int pn = Int(o, "plies-negro", pb);
        int tt = Int(o, "tt-bits", 20);
        var sw = Stopwatch.StartNew();
        var bal = Lote(r, partidas, (ulong)Int(o, "semilla", 1),
                       g => new PoliticaBusqueda(new Busqueda(g, tt), pb),
                       g => new PoliticaBusqueda(new Busqueda(g, tt), pn));
        sw.Stop();
        bal.Imprimir($"Blanco ve {pb} plies, negro ve {pn}   [{r.Etiqueta()}]");
        Console.WriteLine($"  ({sw.ElapsedMilliseconds:N0} ms)");
        return 0;
    }

    // ------------------------------------------------------------- resolver

    private static int Resolver(Dictionary<string, string> o) {
        var r = Leer(o);
        var g = new Juego(r);
        var bus = new Busqueda(g, Int(o, "tt-bits", 24));
        int max = Int(o, "max-prof", 14);
        Console.WriteLine($"Disposicion {r.Etiqueta()}");
        Console.WriteLine(Juego.Dibujar(g.Inicial()));
        Console.WriteLine("  prof            nodos       ms   veredicto");
        for (int d = 1; d <= max; d++) {
            bus.Limpiar();
            var sw = Stopwatch.StartNew();
            int v = bus.RaizExacta(g.Inicial(), Juego.Blanco, d);
            sw.Stop();
            Console.WriteLine($"  {d,4} {bus.Nodos,16:N0} {sw.ElapsedMilliseconds,8:N0}   {Busqueda.Veredicto(v, Juego.Blanco)}");
            if (Math.Abs(v) > 900000) break;
        }
        return 0;
    }

    // -------------------------------------------------------------- partida

    private static int UnaPartida(Dictionary<string, string> o) {
        var r = Leer(o);
        var g = new Juego(r);
        var mesa = new Mesa(g);
        int prof = Int(o, "plies", 4);
        Politica pol = Flag(o, "azar") ? new PoliticaAzar() : (Politica)new PoliticaBusqueda(new Busqueda(g, 22), prof);
        var rng = new Rng((ulong)Int(o, "semilla", 1));
        bool json = Flag(o, "json");

        var lineas = new List<string> { Juego.Linea(g.Inicial()) };
        var jugadas = new List<string>();

        if (!json) {
            Console.WriteLine($"Disposicion {r.Etiqueta()}, ambos con {pol.Nombre}");
            Console.WriteLine(Juego.Dibujar(g.Inicial()));
        }
        var reg = mesa.Jugar(pol, pol, ref rng, (ply, turno, desc, b) => {
            lineas.Add(Juego.Linea(b));
            jugadas.Add($"{{\"ply\":{ply},\"turno\":\"{(turno == Juego.Blanco ? "blanco" : "negro")}\",\"texto\":\"{desc}\"}}");
            if (json) return;
            Console.WriteLine($"  {ply,3}. {(turno == Juego.Blanco ? "BLANCO" : "negro ")}  {desc}");
            Console.WriteLine(Juego.Dibujar(b));
        });

        if (json) {
            Console.WriteLine("{");
            Console.WriteLine($"  \"inicio\": \"{r.Etiqueta()}\",");
            Console.WriteLine($"  \"politica\": \"{pol.Nombre}\",");
            Console.WriteLine($"  \"semilla\": {Int(o, "semilla", 1)},");
            Console.WriteLine($"  \"resultado\": \"{reg.Res}\",");
            Console.WriteLine($"  \"final\": \"{Balance.NombreFinal[(int)reg.Fin]}\",");
            Console.WriteLine($"  \"plies\": {reg.Plies},");
            Console.WriteLine($"  \"posiciones\": [{string.Join(", ", lineas.ConvertAll(x => $"\"{x}\""))}],");
            Console.WriteLine($"  \"jugadas\": [{string.Join(", ", jugadas)}]");
            Console.WriteLine("}");
        } else {
            Console.WriteLine($"  {reg.Res} por {Balance.NombreFinal[(int)reg.Fin]} en {reg.Plies} plies");
        }
        return 0;
    }

    // ------------------------------------------------------------- comparar

    private static int Comparar(Dictionary<string, string> o) {
        int partidasAzar = Int(o, "partidas", 20000);
        int partidasPract = Int(o, "partidas-practica", 300);
        int prof = Int(o, "plies", 4);
        ulong semilla = (ulong)Int(o, "semilla", 1);

        var variantes = new List<(string nombre, Action<Reglas> ajuste)> {
            ("base", _ => { }),
            ("sacerdote-edificios", x => x.SacerdoteEdificios = true),
            ("guerrero-queda", x => x.GuerreroQueda = true),
            ("toma-libre", x => x.TomaLibre = true),
            ("obra-libre", x => x.ObraLibre = true),
            ("castillo-libre", x => x.CastilloLibre = true),
            ("castillo-claim", x => x.CastilloClaim = true),
            ("sacerdote-reubica", x => x.SacerdoteReubica = true),
        };

        Console.WriteLine($"Al azar, {partidasAzar:N0} partidas por fila");
        Console.WriteLine("  inicio     variante              blanco  negro  empate  reparto   plies  castillo  sinconstr  ahogado  repet  limite");
        foreach (string inicio in Juego.Disposiciones)
            foreach (var (nombre, ajuste) in variantes) {
                var r = new Reglas { Inicio = inicio };
                ajuste(r);
                var bal = Lote(r, partidasAzar, semilla, _ => new PoliticaAzar(), _ => new PoliticaAzar());
                Fila(inicio, nombre, bal);
            }

        Console.WriteLine();
        Console.WriteLine($"Mirando {prof} plies, {partidasPract:N0} partidas por fila");
        Console.WriteLine("  inicio     variante              blanco  negro  empate  reparto   plies  castillo  sinconstr  ahogado  repet  limite");
        foreach (string inicio in Juego.Disposiciones)
            foreach (var (nombre, ajuste) in variantes) {
                var r = new Reglas { Inicio = inicio };
                ajuste(r);
                var bal = Lote(r, partidasPract, semilla,
                               g => new PoliticaBusqueda(new Busqueda(g, 20), prof),
                               g => new PoliticaBusqueda(new Busqueda(g, 20), prof));
                Fila(inicio, nombre, bal);
            }
        return 0;
    }

    private static void Fila(string inicio, string variante, Balance b) {
        double P(int i) => 100.0 * b.Finales[i] / b.Partidas;
        Console.WriteLine($"  {inicio,-10} {variante,-20} {b.PctBlanco,6:F1} {b.PctNegro,6:F1} {b.PctEmpate,7:F1} {b.RepartoBlanco,8:F1} {b.PliesMedio,7:F1} {P(0),9:F1} {P(1),10:F1} {P(2),8:F1} {P(3),6:F1} {P(4),7:F1}");
    }
}
