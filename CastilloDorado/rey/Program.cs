using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReySolver;

public static class Program {
    public static int Main(string[] args) {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length == 0) { Ayuda(); return 0; }

        var o = new Dictionary<string, string>();
        var sueltos = new List<string>();
        for (int i = 0; i < args.Length; i++) {
            if (!args[i].StartsWith("--")) { sueltos.Add(args[i]); continue; }
            string k = args[i].Substring(2);
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) o[k] = args[++i];
            else o[k] = "1";
        }

        string cmd = sueltos.Count > 0 ? sueltos[0] : "ayuda";
        var reglas = LeerReglas(o);
        // El tamaño del tablero es global y se fija una sola vez, antes de todo lo demas.
        Juego.Configurar(reglas.Lado);

        switch (cmd) {
            case "selftest": return SelfTest.Run() ? 0 : 1;
            case "perft": Perft(reglas, Ent(o, "prof", 6)); return 0;
            case "azar": Azar(reglas, Ent(o, "partidas", 50000), Ent(o, "semilla", 1)); return 0;
            case "practica":
                Practica(reglas, Ent(o, "partidas", 300), Ent(o, "plies", 4),
                         Ent(o, "plies-negro", -1), Ent(o, "semilla", 1), Ent(o, "quieta", 0));
                return 0;
            case "resolver": Resolver(reglas, Ent(o, "max-prof", 15), Ent(o, "tt-bits", 22)); return 0;
            case "analiza": Analiza(reglas, o); return 0;
            case "partida":
                UnaPartida(reglas, Ent(o, "plies", 4), Ent(o, "semilla", 1),
                           o.ContainsKey("azar"), o.ContainsKey("json"),
                           Ent(o, "quieta", 0), Flag(o, "limpia-tt"));
                return 0;
            case "comparar": Comparar(reglas, Ent(o, "partidas", 20000), Ent(o, "plies", 6), Ent(o, "partidas-practica", 200)); return 0;
            default: Ayuda(); return 0;
        }
    }

    private static int Ent(Dictionary<string, string> o, string k, int def)
        => o.TryGetValue(k, out string? v) && int.TryParse(v, out int n) ? n : def;

    private static bool Flag(Dictionary<string, string> o, string k) => o.ContainsKey(k);

    private static Reglas LeerReglas(Dictionary<string, string> o) => new Reglas {
        Inicio = o.TryGetValue("inicio", out string? i) ? i : "esquinas",
        Lado = Ent(o, "lado", 4),
        AdelantaSegundo = Ent(o, "adelanta-segundo", 0),
        CorreSegundo = Ent(o, "corre-segundo", 0),
        AdelantaPrimero = Ent(o, "adelanta-primero", 0),
        CorrePrimero = Ent(o, "corre-primero", 0),
        Regalo = o.TryGetValue("regalo", out string? rg)
            ? Array.IndexOf(new[] { "taller", "cuartel", "iglesia" }, rg) : -1,
        RegaloDonde = o.TryGetValue("regalo-donde", out string? rd) ? rd : "fondo-esquina",
        RegaloConUnidad = Flag(o, "regalo-con-unidad"),
        RegaloCasilla = Ent(o, "regalo-casilla", -1),
        EdificioSinUnidad = Flag(o, "edificio-sin-unidad"),
        Desliza = Flag(o, "desliza"),
        ReyAjedrez = Flag(o, "rey-ajedrez"),
        SoloGuerreroCorre = Flag(o, "solo-guerrero-corre"),
        SaleCaminando = Flag(o, "sale-caminando"),
        SacerdoteDiagonal = Flag(o, "sacerdote-diagonal"),
        GuerreroLargo = Flag(o, "guerrero-largo"),
        ConstruyeLejos = Flag(o, "construye-lejos"),
        AlcanceObra = Ent(o, "alcance-obra", 0),
        ConstructorDiagonal = Flag(o, "constructor-diagonal"),
        ObraEnElLugar = Flag(o, "obra-en-el-lugar"),
        ObraAlLlegar = Flag(o, "obra-al-llegar"),
        DespliegaAlLado = Flag(o, "despliega-al-lado"),
        NoCercaDelRey = Flag(o, "no-cerca-del-rey"),
        VetoReyLinea = Flag(o, "veto-rey-linea"),
        FcTipo = Flag(o, "fc-tipo") || Flag(o, "fila-columna"),
        FcPropio = Flag(o, "fc-propio") || Flag(o, "fila-columna"),
        CastilloLibre = Flag(o, "castillo-libre"),
        ReyNoRecupera = Flag(o, "rey-no-recupera"),
        ReyGuarnicion = !Flag(o, "rey-pierde-poder"),
        ReyPorEdificio = Flag(o, "rey-por-edificio"),
        ReyReino = Flag(o, "rey-reino"),
        ControlGuerrero = Flag(o, "control-guerrero"),
        GuerreroVeloz = Flag(o, "guerrero-veloz"),
        NoPegado = Flag(o, "no-pegado"),
        SacerdoteReubica = Flag(o, "sacerdote-reubica"),
        SacerdoteReleva = Flag(o, "sacerdote-releva"),
        SacerdoteVuelve = Flag(o, "sacerdote-vuelve"),
        VuelveAlEdificio = Flag(o, "vuelve-al-edificio"),
        ReyNoMata = Flag(o, "rey-no-mata"),
        Compensa = Flag(o, "compensa"),
        CastilloAguanta = Flag(o, "castillo-aguanta"),
        RepeticionesEmpate = Ent(o, "repeticiones", 3),
        PliesMax = Ent(o, "plies-max", 300),
    };

    private static void Ayuda() {
        Console.WriteLine(@"Simulador del juego del REY (tablero 4x4).

Cada uno empieza con el rey solo. El rey hace de todo hasta que construye: cada
edificio viene con su unidad adentro. Se gana metiendo al rey en el castillo con
los tres edificios bajo control, o matandole el rey al otro.

  selftest
      Comprueba que el motor haga lo que dicen las reglas.

  perft   [--prof 6]
      Cuenta el arbol completo. Da el factor de ramificacion.

  azar    [--partidas 50000] [--semilla 1]
      Los dos al azar. Mide la forma cruda del juego.

  practica [--partidas 300] [--plies 6] [--plies-negro N] [--semilla 1]
      Los dos miran N plies con evaluacion de material.

  analiza [--pos <linea de 50>] [--turno 0|1] [--plies 8] [--quieta 4]
      Que vale cada jugada legal en una posicion, de mejor a peor. La linea es la
      misma que exporta -partida --json- en el campo posiciones.

  resolver [--max-prof 15] [--tt-bits 22]
      Profundizacion iterativa buscando victorias forzadas desde el arranque.
      Un veredicto de victoria es real; 'no se' no dice nada.

  partida [--plies 6] [--semilla 1] [--azar] [--json]
      Una sola partida, jugada por jugada.

  comparar [--partidas 20000] [--plies 6] [--partidas-practica 200]
      Todas las disposiciones y las variantes de regla, en una tabla.

Reglas (en cualquier comando):
  --lado 4|5            tamaño del tablero
  --inicio esquinas|frentes|adelantados|lados|centro   disposicion inicial
  --adelanta-segundo N  el rey del segundo arranca N filas adelantado (sin numero, 1)
  --corre-segundo N     y N columnas de costado; con -1 y --adelanta-segundo 1 queda en diagonal
  --adelanta-primero N  lo mismo para el rey del primero
  --corre-primero N
  --regalo taller|cuartel|iglesia    el segundo arranca con ese edificio ya levantado
  --regalo-donde pegado|fondo-centro|fondo-esquina|fila2-borde|fila2-centro
  --regalo-con-unidad   el edificio regalado viene con su unidad adentro
  --regalo-casilla N    casilla exacta del regalo, pisa a --regalo-donde
  --edificio-sin-unidad los edificios no traen su unidad: hay que desplegarla aparte
  --quieta N            plies extra de busqueda de quietud al llegar al horizonte (0 la apaga)

  --desliza             las unidades corren en linea recta y frenan ante unidad o edificio
  --rey-ajedrez         el rey no corre: un paso en las ocho direcciones
  --solo-guerrero-corre corre solo el guerrero; el resto camina
  --sale-caminando      parado sobre un edificio no se corre: salir cuesta un paso
  --sacerdote-diagonal  el sacerdote convierte solo en diagonal
  --guerrero-largo      el guerrero mata a la primera unidad de la fila
  --construye-lejos     se construye en cualquier casilla del alcance, no solo al lado
  --alcance-obra N      tope de casillas para la obra a distancia (0 = toda la linea)
  --constructor-diagonal  el constructor levanta tambien en diagonal; el rey no
  --obra-en-el-lugar    se construye bajo los pies: hay que caminar hasta el sitio
  --obra-al-llegar      el que construye se muda al sitio: mover y construir es una jugada
  --despliega-al-lado   la unidad sale al lado del edificio, no encima
  --no-cerca-del-rey    no se puede construir pegado al rey enemigo
  --veto-rey-linea      ni en la fila o la columna del rey enemigo
  --fila-columna        prende las dos de abajo
  --fc-tipo             no dos edificios del mismo TIPO en la misma fila o columna
  --fc-propio           no dos edificios del mismo JUGADOR en la misma fila o columna
  --castillo-libre      el castillo no juega a --no-pegado, en las dos direcciones
  --rey-no-recupera     el rey pierde el poder al levantar el edificio y no vuelve nunca
  --rey-pierde-poder    el rey pierde el poder apenas la unidad existe en el tablero,
                        en vez de conservarlo mientras la unidad este en su edificio
  --rey-por-edificio    el rey pierde el poder por CONTROLAR el edificio, no por tener la
                        unidad: matarsela al otro ya no se lo devuelve
  --rey-reino           el rey pierde el poder si tiene la unidad O el edificio; solo lo
                        recupera cuando no le queda ninguno de los dos
  --control-guerrero    solo el guerrero toma el control de un edificio parandose encima
  --guerrero-veloz      el guerrero carga: se mueve hasta dos casillas en linea recta
  --no-pegado           vuelve la regla de que dos edificios no pueden tocarse
  --sacerdote-reubica   el sacerdote convierte aunque ya tenga esa pieza: la muda ahi
  --sacerdote-releva    lo mismo, pero solo si tu pieza esta guarnecida en su edificio
  --sacerdote-vuelve    despues de convertir el sacerdote deja la casilla: va a su iglesia si
                        esta libre, y si no sale del tablero. No aplica al convertir un sacerdote
  --vuelve-al-edificio  al morir o ser convertida, la unidad vuelve parada sobre su edificio si
                        esta libre, en vez de salir del tablero
  --rey-no-mata         el rey nunca mata, aunque tenga el poder del guerrero
  --compensa            el segundo arranca con el taller levantado y el constructor adentro
  --castillo-aguanta    no alcanza con entrar al castillo: hay que aguantar adentro un turno
  --repeticiones 3      cuantas repeticiones son empate
  --plies-max 300       tope de plies");
    }

    // ------------------------------------------------------------------ perft

    private static void Perft(Reglas r, int prof) {
        var g = new Juego(r);
        Console.WriteLine($"Disposicion {r.Etiqueta()}");
        Console.WriteLine(Juego.Dibujar(g.Inicial()));
        long anterior = 1;
        for (int d = 1; d <= prof; d++) {
            var sw = Stopwatch.StartNew();
            long n = Contar(g, g.Inicial(), Juego.Blanco, d);
            sw.Stop();
            Console.WriteLine($"  prof {d,2}   {n,14:N0} nodos   ramificacion {(double)n / Math.Max(1, anterior),5:F2}   {sw.ElapsedMilliseconds,6} ms");
            anterior = n;
        }
    }

    private static long Contar(Juego g, Pos p, int turno, int prof) {
        if (prof == 0) return 1;
        if (g.Terminal(p, turno) != null) return 1;
        Span<int> buf = stackalloc int[Juego.MaxJugadas];
        int n = g.Jugadas(p, turno, buf);
        if (n == 0) return 1;
        long t = 0;
        for (int i = 0; i < n; i++) t += Contar(g, g.Aplicar(p, turno, buf[i]), 1 - turno, prof - 1);
        return t;
    }

    // ------------------------------------------------------------------- azar

    private static void Azar(Reglas r, int partidas, int semilla) {
        var b = CorrerLote(r, partidas, semilla, _ => new PoliticaAzar(), _ => new PoliticaAzar());
        b.Imprimir($"Juego al azar   [{r.Etiqueta()}]");
    }

    // --------------------------------------------------------------- practica

    private static void Practica(Reglas r, int partidas, int plies, int pliesNegro, int semilla, int quieta) {
        if (pliesNegro < 0) pliesNegro = plies;
        var sw = Stopwatch.StartNew();
        var b = CorrerLote(r, partidas, semilla,
            g => new PoliticaBusqueda(new Busqueda(g, 20) { MaxQuieta = quieta }, plies),
            g => new PoliticaBusqueda(new Busqueda(g, 20) { MaxQuieta = quieta }, pliesNegro));
        sw.Stop();
        b.Imprimir($"Blanco ve {plies} plies, negro ve {pliesNegro}   [{r.Etiqueta()}]");
        Console.WriteLine($"  ({sw.ElapsedMilliseconds:N0} ms)");
    }

    /// <summary>Segundos a algo que se lee de un vistazo: 3m12s, 1h04m.</summary>
    private static string Reloj(double seg) {
        if (seg < 90) return $"{seg,4:F0}s";
        if (seg < 3600) return $"{(int)(seg / 60),3}m{(int)(seg % 60):00}s";
        return $"{(int)(seg / 3600)}h{(int)(seg % 3600 / 60):00}m";
    }

    /// <summary>
    /// Cuantas partidas van, cuanto tardaron y cuanto falta. Va a la salida de ERROR a
    /// proposito: asi el archivo de resultados queda limpio y el progreso se puede
    /// redirigir aparte y mirar con tail -f mientras corre.
    /// </summary>
    private static int _hechas;

    private static Balance CorrerLote(Reglas r, int partidas, int semilla,
                                      Func<Juego, Politica> hacerB, Func<Juego, Politica> hacerN) {
        int hilos = Math.Max(1, Environment.ProcessorCount - 1);
        var parciales = new Balance[hilos];
        int porHilo = (partidas + hilos - 1) / hilos;

        _hechas = 0;
        int paso = Math.Max(1, partidas / 20);
        var swProg = Stopwatch.StartNew();
        Console.Error.WriteLine($"[{r.Etiqueta()}]  {partidas} partidas en {hilos} hilos");

        Parallel.For(0, hilos, h => {
            var bal = new Balance();
            var g = new Juego(r.Copia());
            var mesa = new Mesa(g);
            var polB = hacerB(g);
            var polN = hacerN(g);
            var rng = new Rng((ulong)(semilla * 1000003 + h * 7919 + 17));
            int desde = h * porHilo, hasta = Math.Min(partidas, desde + porHilo);
            for (int i = desde; i < hasta; i++) {
                bal.Sumar(mesa.Jugar(polB, polN, ref rng));
                int n = Interlocked.Increment(ref _hechas);
                if (n % paso != 0 && n != partidas) continue;
                double seg = swProg.Elapsed.TotalSeconds;
                Console.Error.WriteLine(
                    $"  {n,5}/{partidas}  {100.0 * n / partidas,5:F1}%   " +
                    $"{Reloj(seg)} corridos, faltan ~{Reloj(seg / n * (partidas - n))}");
            }
            parciales[h] = bal;
        });

        var total = new Balance();
        foreach (var p in parciales) if (p != null) total.Fusionar(p);
        return total;
    }

    // --------------------------------------------------------------- resolver

    private static void Resolver(Reglas r, int maxProf, int ttBits) {
        var g = new Juego(r);
        var bus = new Busqueda(g, ttBits);
        Console.WriteLine($"Disposicion {r.Etiqueta()}");
        Console.WriteLine(Juego.Dibujar(g.Inicial()));
        Console.WriteLine("  prof            nodos       ms   veredicto");
        for (int d = 1; d <= maxProf; d++) {
            bus.Limpiar();
            var sw = Stopwatch.StartNew();
            int v = bus.RaizExacta(g.Inicial(), Juego.Blanco, d);
            sw.Stop();
            Console.WriteLine($"  {d,4}   {bus.Nodos,14:N0}   {sw.ElapsedMilliseconds,6}   {Busqueda.Veredicto(v, Juego.Blanco)}");
            if (Math.Abs(v) > Busqueda.GANA / 2) break;
        }
    }

    // ---------------------------------------------------------------- analiza

    /// <summary>
    /// Le pregunta al motor que vale cada jugada legal en una posicion. Sin esto, cuando
    /// el motor juega algo raro no queda mas que adivinar por que.
    /// </summary>
    private static void Analiza(Reglas r, Dictionary<string, string> o) {
        var g = new Juego(r);
        Pos p = o.TryGetValue("pos", out string? l) ? Juego.DesdeLinea(l) : g.Inicial();
        int turno = Ent(o, "turno", 0);
        int plies = Ent(o, "plies", 8);
        var bus = new Busqueda(g, 22) { MaxQuieta = Ent(o, "quieta", 4) };

        Console.Write(Juego.Dibujar(p));
        Console.WriteLine($"Juega {(turno == Juego.Blanco ? "el PRIMERO" : "el segundo")}, " +
                          $"mirando {plies} plies   [{r.Etiqueta()}]");

        var fin = g.Terminal(p, turno);
        if (fin.HasValue) {
            Console.WriteLine($"  la posicion ya termino: {fin.Value.res} por {fin.Value.fin}");
            return;
        }

        Span<int> buf = stackalloc int[Juego.MaxJugadas];
        int n = g.Jugadas(p, turno, buf);
        if (n == 0) { Console.WriteLine("  ahogado: no hay jugadas"); return; }

        var filas = new List<(int v, string t)>();
        for (int i = 0; i < n; i++) {
            Pos np = g.Aplicar(p, turno, buf[i]);
            filas.Add((-bus.RaizPractica(np, 1 - turno, plies - 1), g.Describir(p, turno, buf[i])));
        }
        filas.Sort((a, b) => b.v.CompareTo(a.v));

        Console.WriteLine($"  {n} jugadas legales, de mejor a peor:");
        foreach (var (v, t) in filas) Console.WriteLine($"  {Veredicto(v),14}  {t}");
    }

    private static string Veredicto(int v) {
        if (v > Busqueda.GANA / 2) return $"GANA ({v})";
        if (v < -Busqueda.GANA / 2) return $"PIERDE ({v})";
        return v.ToString();
    }

    // ---------------------------------------------------------------- partida

    private static void UnaPartida(Reglas r, int plies, int semilla, bool azar, bool json,
                                   int quieta, bool limpiaTT) {
        var g = new Juego(r);
        var bus = new Busqueda(g, 22) { MaxQuieta = quieta, LimpiaCadaJugada = limpiaTT };
        var rng = new Rng((ulong)(semilla * 1000003 + 17));
        Pos p = g.Inicial();
        int turno = Juego.Blanco;
        var historia = new List<Pos> { p };
        var vistas = new Dictionary<(UInt128, UInt128, int), int> { [(p.Ed, p.Un, turno)] = 1 };

        var jsPos = new List<string> { Juego.Linea(p) };
        var jsJug = new List<string>();

        if (!json) {
            Console.WriteLine($"Disposicion {r.Etiqueta()}, ambos {(azar ? "al azar" : $"con ve{plies}")}");
            Console.WriteLine(Juego.Dibujar(p));
        }

        string desenlace = "sigue";
        Span<int> buf = stackalloc int[Juego.MaxJugadas];
        for (int ply = 1; ply <= r.PliesMax; ply++) {
            var fin = g.Terminal(p, turno);
            if (fin.HasValue) { desenlace = Texto(fin.Value, ply - 1); break; }

            int n = g.Jugadas(p, turno, buf);
            if (n == 0) {
                desenlace = $"{(turno == Juego.Blanco ? "el PRIMERO" : "el segundo")} se quedo sin jugadas y pierde, en {ply - 1} plies";
                break;
            }

            int jugada = azar ? buf[rng.Hasta(n)] : bus.ElegirPractico(p, turno, plies, ref rng, historia);
            string texto = g.Describir(p, turno, jugada);
            p = g.Aplicar(p, turno, jugada);

            if (json) {
                jsPos.Add(Juego.Linea(p));
                jsJug.Add($"{{\"ply\":{ply},\"turno\":{turno},\"tipo\":\"{Juego.NombreJugada[Juego.JTipo(jugada)]}\"," +
                          $"\"desde\":\"{Juego.Casilla(Juego.JDesde(jugada))}\",\"hasta\":\"{Juego.Casilla(Juego.JHasta(jugada))}\"," +
                          $"\"texto\":\"{texto}\"}}");
            } else {
                Console.WriteLine($"  {ply,3}. {(turno == Juego.Blanco ? "BLANCO " : "negro  ")} {texto}");
                Console.WriteLine(Juego.Dibujar(p));
            }

            turno = 1 - turno;
            historia.Add(p);
            var clave = (p.Ed, p.Un, turno);
            vistas.TryGetValue(clave, out int veces);
            vistas[clave] = veces + 1;
            if (veces + 1 >= r.RepeticionesEmpate) { desenlace = $"empate por repeticion en {ply} plies"; break; }
        }

        if (json) {
            var sb = new StringBuilder();
            sb.Append("{\"reglas\":\"").Append(r.Etiqueta()).Append("\",\"desenlace\":\"").Append(desenlace).Append("\",");
            sb.Append("\"posiciones\":[").Append(string.Join(",", jsPos.Select(x => $"\"{x}\""))).Append("],");
            sb.Append("\"jugadas\":[").Append(string.Join(",", jsJug)).Append("]}");
            Console.WriteLine(sb.ToString());
        } else {
            Console.WriteLine($"  {desenlace}");
        }
    }

    private static string Texto((Resultado res, Final fin) f, int plies) {
        string quien = f.res == Resultado.Blanco ? "gana el PRIMERO" : f.res == Resultado.Negro ? "gana el segundo" : "empate";
        string por = f.fin switch {
            Final.Castillo => "por meter el rey en el castillo",
            Final.ReyMuerto => "por matarle el rey al otro",
            Final.Ahogado => "por ahogado",
            _ => f.fin.ToString().ToLowerInvariant(),
        };
        return $"{quien} {por}, en {plies} plies";
    }

    // --------------------------------------------------------------- comparar

    private static void Comparar(Reglas baseR, int partidas, int plies, int partidasPractica) {
        var variantes = new (string nombre, Action<Reglas> set)[] {
            ("base", _ => { }),
            ("rey-pierde-poder", x => x.ReyGuarnicion = false),
            ("rey-por-edificio", x => x.ReyPorEdificio = true),
            ("rey-reino", x => x.ReyReino = true),
            ("control-guerrero", x => x.ControlGuerrero = true),
            ("guerrero-veloz", x => x.GuerreroVeloz = true),
            ("adelanta-segundo", x => x.AdelantaSegundo = 1),
            ("no-pegado", x => x.NoPegado = true),
            ("sacerdote-reubica", x => x.SacerdoteReubica = true),
            ("sacerdote-releva", x => x.SacerdoteReleva = true),
            ("sacerdote-vuelve", x => x.SacerdoteVuelve = true),
            ("vuelve-al-edificio", x => x.VuelveAlEdificio = true),
            ("rey-no-mata", x => x.ReyNoMata = true),
            ("compensa", x => x.Compensa = true),
            ("castillo-aguanta", x => x.CastilloAguanta = true),
        };

        Console.WriteLine();
        Console.WriteLine($"Al azar, {partidas:N0} partidas por fila");
        Console.WriteLine("  inicio     variante              empate   reparto   castillo  reymuerto  ahogado   plies");
        foreach (string ini in Juego.Disposiciones) {
            foreach (var (nombre, set) in variantes) {
                var r = baseR.Copia(); r.Inicio = ini; set(r);
                var b = CorrerLote(r, partidas, 1, _ => new PoliticaAzar(), _ => new PoliticaAzar());
                Console.WriteLine($"  {ini,-10} {nombre,-20} {100.0 * b.Empate / b.Partidas,6:F1}%  {b.Reparto,6:F1}   " +
                                  $"{100.0 * b.PorFinal[(int)Final.Castillo] / b.Partidas,6:F1}%   " +
                                  $"{100.0 * b.PorFinal[(int)Final.ReyMuerto] / b.Partidas,6:F1}%   " +
                                  $"{100.0 * b.PorFinal[(int)Final.Ahogado] / b.Partidas,5:F1}%  {(double)b.PliesTotal / b.Partidas,5:F1}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Con los dos mirando {plies} plies, {partidasPractica} partidas por fila");
        Console.WriteLine("  inicio     variante              empate   reparto   castillo  reymuerto  ahogado   plies");
        foreach (string ini in Juego.Disposiciones) {
            foreach (var (nombre, set) in variantes) {
                var r = baseR.Copia(); r.Inicio = ini; set(r);
                var b = CorrerLote(r, partidasPractica, 1,
                    g => new PoliticaBusqueda(new Busqueda(g, 20), plies),
                    g => new PoliticaBusqueda(new Busqueda(g, 20), plies));
                Console.WriteLine($"  {ini,-10} {nombre,-20} {100.0 * b.Empate / b.Partidas,6:F1}%  {b.Reparto,6:F1}   " +
                                  $"{100.0 * b.PorFinal[(int)Final.Castillo] / b.Partidas,6:F1}%   " +
                                  $"{100.0 * b.PorFinal[(int)Final.ReyMuerto] / b.Partidas,6:F1}%   " +
                                  $"{100.0 * b.PorFinal[(int)Final.Ahogado] / b.Partidas,5:F1}%  {(double)b.PliesTotal / b.Partidas,5:F1}");
            }
        }
    }
}
