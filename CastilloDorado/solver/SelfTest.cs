using System;
using System.Collections.Generic;
using System.Linq;

namespace CastilloSolver;

/// <summary>
/// Cada test comprueba una frase concreta del enunciado de las reglas. Si una regla se
/// cambia, el test que la nombra tiene que cambiar con ella.
/// </summary>
public static class SelfTest {
    private static int _fallos;

    private static void Check(string nombre, bool ok) {
        Console.WriteLine($"  [{(ok ? "ok " : "FALLA")}] {nombre}");
        if (!ok) _fallos++;
    }

    private static ulong Poner(ulong b, int dueno, int tipo, int casilla)
        => Juego.Con(b, casilla, Juego.Cod(dueno, tipo));

    private static List<int> Lista(Juego g, ulong b, int turno) {
        var buf = new int[Juego.MaxJugadas];
        int n = g.Jugadas(b, turno, buf.AsSpan());
        return buf.Take(n).ToList();
    }

    private static bool Tiene(Juego g, ulong b, int turno, int tipoJugada, int hasta, int extra = -1)
        => Lista(g, b, turno).Any(j => Juego.JTipo(j) == tipoJugada && Juego.JHasta(j) == hasta
                                       && (extra < 0 || Juego.JExtra(j) == extra));

    public static bool Run() {
        _fallos = 0;
        int B = Juego.Blanco, N = Juego.Negro;
        var g = new Juego(new Reglas());

        // Casillas del 4x4:   0  1  2  3
        //                     4  5  6  7
        //                     8  9 10 11
        //                    12 13 14 15

        Console.WriteLine("Arranque");

        ulong ini = g.Inicial();
        int pres0 = Juego.Presentes(ini);
        Check("cada uno arranca con taller y constructor",
              Juego.Hay(pres0, B, Juego.Taller) && Juego.Hay(pres0, B, Juego.Constructor) &&
              Juego.Hay(pres0, N, Juego.Taller) && Juego.Hay(pres0, N, Juego.Constructor));
        Check("nadie arranca con guerrero ni sacerdote en el tablero",
              !Juego.Hay(pres0, B, Juego.Guerrero) && !Juego.Hay(pres0, N, Juego.Sacerdote));
        Check("la posicion inicial no es terminal", g.Terminal(ini, B) == null);
        Check("las cuatro disposiciones son simetricas por giro de 180",
              Juego.Disposiciones.All(d => {
                  ulong a = Juego.Inicial(d);
                  ulong r = 0;
                  for (int c = 0; c < 16; c++) {
                      int v = Juego.En(a, c);
                      if (v == 0) continue;
                      int nv = Juego.Cod(1 - Juego.Dueno(v), Juego.Tipo(v));
                      r = Juego.Con(r, 15 - c, nv);
                  }
                  return r == a;
              }));

        Console.WriteLine("Mover: una casilla vacia adyacente, sin diagonales");

        ulong b1 = Poner(Poner(0UL, B, Juego.Constructor, 5), B, Juego.Taller, 0);
        b1 = Poner(Poner(b1, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        var m5 = Lista(g, b1, B).Where(j => Juego.JTipo(j) == Juego.MOVER && Juego.JDesde(j) == 5)
                                .Select(Juego.JHasta).OrderBy(x => x).ToList();
        Check("el constructor en 5 se mueve a 1, 4, 6 y 9", m5.SequenceEqual(new[] { 1, 4, 6, 9 }));
        Check("no hay diagonales", !m5.Contains(0) && !m5.Contains(10));

        ulong tapado = Poner(b1, N, Juego.Sacerdote, 6);
        Check("no se mueve a una casilla ocupada",
              !Lista(g, tapado, B).Any(j => Juego.JTipo(j) == Juego.MOVER && Juego.JHasta(j) == 6));

        Console.WriteLine("Construir");

        // Constructor en 5, taller propio en 0. La casilla 1 y la 4 tocan el taller.
        Check("construye en una casilla vacia adyacente al constructor",
              Tiene(g, b1, B, Juego.CONSTRUIR, 9, Juego.Cuartel));
        Check("no construye pegado a un edificio (1 y 4 tocan el taller de 0)",
              !Tiene(g, b1, B, Juego.CONSTRUIR, 1) && !Tiene(g, b1, B, Juego.CONSTRUIR, 4));
        Check("no construye un taller si ya tiene uno",
              !Tiene(g, b1, B, Juego.CONSTRUIR, 9, Juego.Taller));
        Check("el edificio enemigo tambien bloquea la obra",
              !Tiene(g, Poner(b1, N, Juego.Iglesia, 13), B, Juego.CONSTRUIR, 9));

        ulong construido = g.Aplicar(b1, B, Lista(g, b1, B).First(j => Juego.JTipo(j) == Juego.CONSTRUIR
                                                                      && Juego.JHasta(j) == 9
                                                                      && Juego.JExtra(j) == Juego.Cuartel));
        Check("al construir el constructor se queda donde estaba",
              Juego.En(construido, 5) == Juego.Cod(B, Juego.Constructor) &&
              Juego.En(construido, 9) == Juego.Cod(B, Juego.Cuartel));

        Console.WriteLine("Guerrero");

        ulong gb = Poner(Poner(0UL, B, Juego.Guerrero, 5), B, Juego.Taller, 0);
        gb = Poner(gb, B, Juego.Constructor, 2);
        gb = Poner(Poner(gb, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        ulong conVictima = Poner(gb, N, Juego.Sacerdote, 6);
        Check("el guerrero mata una unidad enemiga adyacente", Tiene(g, conVictima, B, Juego.MATAR, 6));
        ulong matado = g.Aplicar(conVictima, B, Lista(g, conVictima, B).First(j => Juego.JTipo(j) == Juego.MATAR));
        Check("al matar ocupa la casilla y la victima sale del tablero",
              Juego.En(matado, 6) == Juego.Cod(B, Juego.Guerrero) && Juego.En(matado, 5) == Juego.Vacio &&
              !Juego.Hay(Juego.Presentes(matado), N, Juego.Sacerdote));

        ulong conIglesiaEnemiga = Poner(gb, N, Juego.Iglesia, 6);
        Check("el guerrero toma un edificio enemigo que no tiene", Tiene(g, conIglesiaEnemiga, B, Juego.TOMAR, 6));
        ulong tomado = g.Aplicar(conIglesiaEnemiga, B, Lista(g, conIglesiaEnemiga, B).First(j => Juego.JTipo(j) == Juego.TOMAR));
        Check("el edificio tomado cambia de bando",
              Juego.En(tomado, 6) == Juego.Cod(B, Juego.Iglesia));
        Check("el guerrero que entra queda adentro, o sea fuera del tablero",
              Juego.En(tomado, 5) == Juego.Vacio && !Juego.Hay(Juego.Presentes(tomado), B, Juego.Guerrero));

        ulong yaTengoIglesia = Poner(conIglesiaEnemiga, B, Juego.Iglesia, 8);
        Check("no puede entrar a un edificio que ya tiene",
              !Tiene(g, yaTengoIglesia, B, Juego.TOMAR, 6));

        var gQueda = new Juego(new Reglas { GuerreroQueda = true });
        ulong tomado2 = gQueda.Aplicar(conIglesiaEnemiga, B,
                                       Lista(gQueda, conIglesiaEnemiga, B).First(j => Juego.JTipo(j) == Juego.TOMAR));
        Check("con --guerrero-queda el guerrero no entra",
              Juego.En(tomado2, 5) == Juego.Cod(B, Juego.Guerrero) &&
              Juego.En(tomado2, 6) == Juego.Cod(B, Juego.Iglesia));

        Console.WriteLine("Sacerdote");

        ulong sb = Poner(Poner(0UL, B, Juego.Sacerdote, 5), B, Juego.Taller, 0);
        sb = Poner(sb, B, Juego.Constructor, 2);
        sb = Poner(Poner(sb, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        ulong conGuerreroEnemigo = Poner(sb, N, Juego.Guerrero, 6);
        Check("convierte al guerrero enemigo si no tiene guerrero",
              Tiene(g, conGuerreroEnemigo, B, Juego.CONVERTIR, 6));
        ulong convertido = g.Aplicar(conGuerreroEnemigo, B,
                                     Lista(g, conGuerreroEnemigo, B).First(j => Juego.JTipo(j) == Juego.CONVERTIR));
        Check("el convertido cambia de bando y se queda en su casilla",
              Juego.En(convertido, 6) == Juego.Cod(B, Juego.Guerrero) &&
              Juego.En(convertido, 5) == Juego.Cod(B, Juego.Sacerdote));

        ulong yaTengoGuerrero = Poner(conGuerreroEnemigo, B, Juego.Guerrero, 8);
        Check("no convierte si ya tiene esa pieza en el tablero",
              !Tiene(g, yaTengoGuerrero, B, Juego.CONVERTIR, 6));

        ulong conCuartelEnemigo = Poner(sb, N, Juego.Cuartel, 6);
        Check("por defecto el sacerdote no convierte edificios",
              !Tiene(g, conCuartelEnemigo, B, Juego.CONVERTIR, 6));
        var gSac = new Juego(new Reglas { SacerdoteEdificios = true });
        Check("con --sacerdote-edificios si los convierte",
              Tiene(gSac, conCuartelEnemigo, B, Juego.CONVERTIR, 6));

        Console.WriteLine("Los edificios son el punto de aparicion");

        ulong solo = Poner(0UL, B, Juego.Taller, 5);
        solo = Poner(Poner(solo, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        var salidas = Lista(g, solo, B).Where(j => Juego.JTipo(j) == Juego.DESPLEGAR)
                                       .Select(Juego.JHasta).OrderBy(x => x).ToList();
        Check("con el constructor muerto vuelve a salir del taller, a cualquier casilla adyacente",
              salidas.SequenceEqual(new[] { 1, 4, 6, 9 }));
        Check("del taller sale el constructor, no otra cosa",
              Lista(g, solo, B).Where(j => Juego.JTipo(j) == Juego.DESPLEGAR)
                               .All(j => Juego.JExtra(j) == Juego.Constructor));
        Check("no despliega si la unidad ya esta en el tablero",
              !Lista(g, Poner(solo, B, Juego.Constructor, 1), B).Any(j => Juego.JTipo(j) == Juego.DESPLEGAR));

        ulong conCuartel = Poner(solo, B, Juego.Cuartel, 7);
        Check("del cuartel sale el guerrero",
              Lista(g, conCuartel, B).Any(j => Juego.JTipo(j) == Juego.DESPLEGAR && Juego.JExtra(j) == Juego.Guerrero));

        Console.WriteLine("Derrotas");

        ulong sinNada = Poner(0UL, B, Juego.Cuartel, 5);      // blanco tiene cuartel pero ni taller ni constructor
        sinNada = Poner(Poner(sinNada, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        var fin = g.Terminal(sinNada, B);
        Check("sin constructor y sin taller se pierde",
              fin != null && fin.Value.res == Resultado.Negro && fin.Value.fin == Final.SinConstructor);
        Check("con el taller en pie no se pierde aunque el constructor este muerto",
              g.Terminal(solo, B) == null);
        Check("con el constructor vivo no se pierde aunque no haya taller",
              g.Terminal(Poner(sinNada, B, Juego.Constructor, 1), B) == null);

        // Blanco: taller en 0 y nada mas; las dos salidas tapadas por piezas negras.
        ulong ahogado = Poner(0UL, B, Juego.Taller, 0);
        ahogado = Poner(Poner(ahogado, N, Juego.Guerrero, 1), N, Juego.Sacerdote, 4);
        ahogado = Poner(Poner(ahogado, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        Check("posicion sin jugadas legales: el que no puede jugar pierde",
              g.Terminal(ahogado, B) == null && Lista(g, ahogado, B).Count == 0);

        Console.WriteLine("Castillo dorado");

        // Taller 0, cuartel 3, iglesia 12, constructor en 6. De sus cuatro vecinos, la 2 y
        // la 7 tocan el cuartel de 3; quedan la 5 y la 10.
        ulong tres = Poner(Poner(0UL, B, Juego.Taller, 0), B, Juego.Cuartel, 3);
        tres = Poner(Poner(tres, B, Juego.Iglesia, 12), B, Juego.Constructor, 6);
        tres = Poner(Poner(tres, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        Check("con los tres edificios aparece la jugada de coronar",
              Lista(g, tres, B).Any(j => Juego.JTipo(j) == Juego.CORONAR));
        Check("sin los tres edificios no aparece",
              !Lista(g, b1, B).Any(j => Juego.JTipo(j) == Juego.CORONAR));
        var sitiosCastillo = Lista(g, tres, B).Where(j => Juego.JTipo(j) == Juego.CORONAR)
                                              .Select(Juego.JHasta).OrderBy(x => x).ToList();
        Check("el castillo tambien respeta el no-pegado (la 2 y la 7 tocan el cuartel)",
              sitiosCastillo.SequenceEqual(new[] { 5, 10 }));

        // Con los tres edificios pegados al constructor no hay donde coronar: se puede
        // ganar la carrera de edificios y quedarse sin sitio de obra.
        ulong encerrado = Poner(Poner(0UL, B, Juego.Taller, 0), B, Juego.Cuartel, 2);
        encerrado = Poner(Poner(encerrado, B, Juego.Iglesia, 8), B, Juego.Constructor, 5);
        encerrado = Poner(Poner(encerrado, N, Juego.Taller, 15), N, Juego.Constructor, 14);
        Check("con los tres edificios rodeando al constructor no hay donde coronar",
              !Lista(g, encerrado, B).Any(j => Juego.JTipo(j) == Juego.CORONAR));
        ulong coronado = g.Aplicar(tres, B, Lista(g, tres, B).First(j => Juego.JTipo(j) == Juego.CORONAR));
        var res = g.Terminal(coronado, N);
        Check("levantar el castillo gana en el acto",
              res != null && res.Value.res == Resultado.Blanco && res.Value.fin == Final.Castillo);

        Console.WriteLine("El candado del taller");

        // Para tomar un edificio de tipo T hay que NO tener uno propio de tipo T. Los dos
        // arrancan con taller, ninguno puede perderlo de otra forma, asi que ninguno puede
        // tomar el del otro nunca. El taller es inmortal y la derrota por eliminacion no existe.
        ulong dosTalleres = Poner(Poner(0UL, B, Juego.Taller, 5), B, Juego.Guerrero, 6);
        dosTalleres = Poner(Poner(dosTalleres, N, Juego.Taller, 7), N, Juego.Constructor, 11);
        Check("con taller propio, el guerrero no puede tomar el taller enemigo pegado",
              !Tiene(g, dosTalleres, B, Juego.TOMAR, 7));
        ulong sinTallerPropio = Juego.Con(dosTalleres, 5, Juego.Vacio);
        sinTallerPropio = Poner(sinTallerPropio, B, Juego.Constructor, 4);
        Check("sin taller propio si puede tomarlo", Tiene(g, sinTallerPropio, B, Juego.TOMAR, 7));
        Check("con --sacerdote-edificios pasa lo mismo: con taller propio no lo convierte",
              !Tiene(gSac, Poner(Juego.Con(dosTalleres, 6, Juego.Vacio), B, Juego.Sacerdote, 6),
                     B, Juego.CONVERTIR, 7));

        // Ningun tipo de jugada saca un edificio del tablero: solo cambian de dueño.
        var problemasTaller = new List<string>();
        BarrerTalleres(g, g.Inicial(), B, 8, problemasTaller);
        Check("en 8 plies desde el inicio nadie pierde nunca su taller", problemasTaller.Count == 0);

        // ------------------------------------------------------------------------
        // Variante: el castillo hay que tomarlo, no alcanza con levantarlo.
        // Se levanta neutral y en cualquier momento; cualquier unidad puede meterse
        // adentro; gana el que tenga los tres edificios Y el reclamo al mismo tiempo.
        // ------------------------------------------------------------------------
        // ------------------------------------------------------------------------
        // Variante: el guerrero entra a cualquier edificio, tenga o no uno igual.
        // Es la unica palanca que abre el candado del taller de la seccion 5.
        // ------------------------------------------------------------------------
        Console.WriteLine("La toma libre (--toma-libre)");

        var gt = new Juego(new Reglas { TomaLibre = true });

        ulong tl = 0;
        tl = Poner(tl, B, Juego.Taller, 0);
        tl = Poner(tl, B, Juego.Guerrero, 5);
        tl = Poner(tl, N, Juego.Taller, 6);
        tl = Poner(tl, N, Juego.Constructor, 15);
        Check("con la regla clasica, con taller propio no se toma el taller enemigo",
              !Tiene(g, tl, B, Juego.TOMAR, 6));
        Check("con la toma libre si se toma", Tiene(gt, tl, B, Juego.TOMAR, 6));
        ulong tl2 = gt.Aplicar(tl, B, Juego.Jug(Juego.TOMAR, 5, 6, 0));
        Check("quedan dos talleres blancos en el tablero",
              Enumerable.Range(0, 16).Count(c => Juego.En(tl2, c) == Juego.Cod(B, Juego.Taller)) == 2);
        Check("pero para ganar siguen contando tipos, no piezas: sigue siendo un edificio",
              Juego.Edificios(Juego.Presentes(tl2), B) == 1);
        Check("y el negro se quedo sin taller",
              !Juego.Hay(Juego.Presentes(tl2), N, Juego.Taller));

        // La regla de eliminacion, que en el juego base es inalcanzable, aca se alcanza:
        // el negro se queda sin taller y sin constructor en el tablero.
        ulong el = 0;
        el = Poner(el, B, Juego.Taller, 0);
        el = Poner(el, B, Juego.Guerrero, 5);
        el = Poner(el, N, Juego.Taller, 6);
        Check("antes de la toma el negro esta vivo", gt.Terminal(el, N) == null);
        Check("le toman el ultimo taller sin constructor en el tablero y pierde",
              gt.Terminal(gt.Aplicar(el, B, Juego.Jug(Juego.TOMAR, 5, 6, 0)), N)
                  is (Resultado.Blanco, Final.SinConstructor));

        Console.WriteLine("Barrido exhaustivo de 6 plies con --toma-libre");
        var probToma = new List<string>();
        long nodosToma = Barrer(gt, gt.Inicial(), B, 6, probToma);
        Check($"ninguna posicion rompe una invariante ({nodosToma:N0} nodos)", probToma.Count == 0);
        foreach (string x in probToma.Take(5)) Console.WriteLine($"        {x}");
        // Desde el arranque el guerrero ni siquiera llega a desplegarse en 8 plies, asi que
        // el barrido se hace desde una posicion donde ya esta a dos pasos del taller enemigo.
        ulong cerca = 0;
        cerca = Poner(cerca, B, Juego.Taller, 0);
        cerca = Poner(cerca, B, Juego.Guerrero, 4);
        cerca = Poner(cerca, N, Juego.Taller, 6);
        cerca = Poner(cerca, N, Juego.Constructor, 15);
        var noPierde = new List<string>();
        BarrerTalleres(g, cerca, B, 4, noPierde);
        Check("con la regla clasica ni con el guerrero a dos pasos se pierde un taller",
              noPierde.Count == 0);
        var siPierde = new List<string>();
        BarrerTalleres(gt, cerca, B, 4, siPierde);
        Check("con la toma libre si: el candado de la seccion 5 queda abierto",
              siPierde.Count > 0);

        Console.WriteLine("El castillo que hay que tomar (--castillo-claim)");

        var gc = new Juego(new Reglas { CastilloClaim = true });

        // Blanco con los tres edificios y el constructor en 6: sitios de obra 5 y 10.
        ulong cc = 0;
        cc = Poner(cc, B, Juego.Taller, 0);
        cc = Poner(cc, B, Juego.Cuartel, 3);
        cc = Poner(cc, B, Juego.Iglesia, 12);
        cc = Poner(cc, B, Juego.Constructor, 6);
        cc = Poner(cc, N, Juego.Taller, 15);
        Check("con el claim, levantar el castillo ya no gana la partida",
              gc.Terminal(gc.Aplicar(cc, B, Juego.Jug(Juego.CORONAR, 6, 5, 0)), N) == null);
        Check("y el castillo queda neutral, sin dueno",
              Juego.En(gc.Aplicar(cc, B, Juego.Jug(Juego.CORONAR, 6, 5, 0)), 5) == Juego.Castillo);

        // Sin los tres edificios: con la regla clasica no se puede levantar, con el claim si.
        ulong cs = 0;
        cs = Poner(cs, B, Juego.Taller, 0);
        cs = Poner(cs, B, Juego.Constructor, 5);
        cs = Poner(cs, N, Juego.Taller, 15);
        Check("con el claim se puede levantar sin tener los tres edificios",
              Tiene(gc, cs, B, Juego.CORONAR, 9));
        Check("con la regla clasica eso sigue prohibido",
              !Tiene(g, cs, B, Juego.CORONAR, 9));
        Check("no se puede levantar un segundo castillo",
              !Lista(gc, Juego.Con(cs, 9, Juego.Castillo), B).Any(j => Juego.JTipo(j) == Juego.CORONAR));

        // Blanco con los tres edificios, castillo neutral en 9 y el GUERRERO al lado en 13.
        ulong ce = 0;
        ce = Poner(ce, B, Juego.Taller, 0);
        ce = Poner(ce, B, Juego.Cuartel, 3);
        ce = Poner(ce, B, Juego.Iglesia, 12);
        ce = Poner(ce, B, Juego.Guerrero, 13);
        ce = Juego.Con(ce, 9, Juego.Castillo);
        ce = Poner(ce, N, Juego.Taller, 15);
        ce = Poner(ce, N, Juego.Constructor, 11);
        Check("entra cualquier unidad, no solo el constructor", Tiene(gc, ce, B, Juego.ENTRAR, 9));
        ulong ce2 = gc.Aplicar(ce, B, Juego.Jug(Juego.ENTRAR, 13, 9, 0));
        Check("la unidad que entra sale del tablero y el castillo pasa a ser suyo",
              Juego.En(ce2, 13) == Juego.Vacio && Juego.En(ce2, 9) == Juego.CastilloBlanco);
        Check("con los tres edificios, entrar al castillo gana",
              gc.Terminal(ce2, N) is (Resultado.Blanco, Final.Castillo));
        Check("sin el claim nadie puede entrar al castillo",
              !Lista(g, ce, B).Any(j => Juego.JTipo(j) == Juego.ENTRAR));

        // El mismo reclamo pero con dos edificios: no gana hasta que llega el tercero.
        ulong cp = 0;
        cp = Poner(cp, B, Juego.Taller, 0);
        cp = Poner(cp, B, Juego.Cuartel, 3);
        cp = Poner(cp, B, Juego.Constructor, 10);
        cp = Juego.Con(cp, 5, Juego.CastilloBlanco);
        cp = Poner(cp, N, Juego.Taller, 12);
        cp = Poner(cp, N, Juego.Constructor, 13);
        Check("el reclamo solo no gana: faltan los tres edificios", gc.Terminal(cp, B) == null);
        Check("con el reclamo puesto, el tercer edificio gana",
              gc.Terminal(gc.Aplicar(cp, B, Juego.Jug(Juego.CONSTRUIR, 10, 11, Juego.Iglesia)), N)
                  is (Resultado.Blanco, Final.Castillo));

        // Y el rival se lo puede robar entrando el, que es lo que hace la regla interesante.
        ulong cr = Poner(cp, N, Juego.Constructor, 9);
        cr = Juego.Con(cr, 13, Juego.Vacio);
        Check("el rival roba el reclamo metiendose adentro", Tiene(gc, cr, N, Juego.ENTRAR, 5));
        Check("y despues de robarlo el castillo es suyo",
              Juego.ReclamoCastillo(Juego.Presentes(gc.Aplicar(cr, N, Juego.Jug(Juego.ENTRAR, 9, 5, 0)))) == N);

        Console.WriteLine("Barrido exhaustivo de 6 plies con --castillo-claim");
        var probClaim = new List<string>();
        long nodosClaim = Barrer(gc, gc.Inicial(), B, 6, probClaim);
        Check($"ninguna posicion rompe una invariante ({nodosClaim:N0} nodos)", probClaim.Count == 0);
        foreach (string x in probClaim.Take(5)) Console.WriteLine($"        {x}");

        // ------------------------------------------------------------------------
        // Variante: el sacerdote convierte aunque ya tenga esa pieza, mudandola.
        // ------------------------------------------------------------------------
        Console.WriteLine("El sacerdote que reubica (--sacerdote-reubica)");

        var gs = new Juego(new Reglas { SacerdoteReubica = true });

        ulong sr = 0;
        sr = Poner(sr, B, Juego.Taller, 12);
        sr = Poner(sr, B, Juego.Sacerdote, 5);
        sr = Poner(sr, B, Juego.Guerrero, 0);
        sr = Poner(sr, N, Juego.Taller, 15);
        sr = Poner(sr, N, Juego.Guerrero, 6);
        Check("con la regla clasica, teniendo guerrero propio no se convierte al enemigo",
              !Tiene(g, sr, B, Juego.CONVERTIR, 6));
        Check("con la reubicacion si se puede", Tiene(gs, sr, B, Juego.CONVERTIR, 6));
        ulong sr2 = gs.Aplicar(sr, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("el guerrero propio se muda: deja de estar donde estaba",
              Juego.En(sr2, 0) == Juego.Vacio && Juego.En(sr2, 6) == Juego.Cod(B, Juego.Guerrero));
        Check("nunca quedan dos guerreros del mismo bando",
              Enumerable.Range(0, 16).Count(c => Juego.En(sr2, c) == Juego.Cod(B, Juego.Guerrero)) == 1);
        Check("el guerrero enemigo desaparece del tablero",
              !Juego.Hay(Juego.Presentes(sr2), N, Juego.Guerrero));

        ulong sf = Juego.Con(sr, 0, Juego.Vacio);   // sin guerrero blanco en el tablero
        Check("si la pieza propia estaba afuera, la regla no cambia nada",
              g.Aplicar(sf, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0)) ==
              gs.Aplicar(sf, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0)));
        Check("sin --sacerdote-edificios sigue sin tocar edificios",
              !Tiene(gs, Poner(sr, N, Juego.Cuartel, 9), B, Juego.CONVERTIR, 9));

        // Lo que mas importa: el sacerdote es la unica jugada que saca al constructor
        // de un tapiado, porque lo mueve sin que camine.
        ulong tap = 0;
        tap = Poner(tap, B, Juego.Taller, 0);
        tap = Poner(tap, B, Juego.Cuartel, 2);
        tap = Poner(tap, B, Juego.Iglesia, 8);
        tap = Poner(tap, B, Juego.Constructor, 5);
        tap = Poner(tap, B, Juego.Sacerdote, 13);
        tap = Poner(tap, N, Juego.Taller, 15);
        tap = Poner(tap, N, Juego.Constructor, 14);
        Check("constructor tapiado: con los tres edificios no hay donde coronar",
              !Lista(gs, tap, B).Any(j => Juego.JTipo(j) == Juego.CORONAR));
        ulong des = gs.Aplicar(tap, B, Juego.Jug(Juego.CONVERTIR, 13, 14, 0));
        Check("el sacerdote lo reubica sobre el constructor enemigo",
              Juego.En(des, 5) == Juego.Vacio && Juego.En(des, 14) == Juego.Cod(B, Juego.Constructor));
        Check("y desde ahi el castillo vuelve a ser posible",
              Lista(gs, des, B).Any(j => Juego.JTipo(j) == Juego.CORONAR));

        Console.WriteLine("Barrido exhaustivo de 6 plies con --sacerdote-reubica");
        var probSac = new List<string>();
        long nodosSac = Barrer(gs, gs.Inicial(), B, 6, probSac);
        Check($"ninguna posicion rompe una invariante ({nodosSac:N0} nodos)", probSac.Count == 0);
        foreach (string x in probSac.Take(5)) Console.WriteLine($"        {x}");

        Console.WriteLine("Simetrias y coherencia");

        ulong giro = g.Transformar(b1, 3);   // 180 grados
        Check("una posicion y su giro tienen la misma cantidad de jugadas",
              Lista(g, b1, B).Count == Lista(g, giro, B).Count);
        Check("una posicion y su giro tienen la misma canonica", g.Canonica(b1) == g.Canonica(giro));
        Check("las 8 simetrias son permutaciones distintas",
              Enumerable.Range(0, 8).Select(k => string.Join(",", g.Simetrias[k])).Distinct().Count() == 8);

        Console.WriteLine("Barrido exhaustivo de 6 plies");
        var problemas = new List<string>();
        long nodos = Barrer(g, g.Inicial(), B, 6, problemas);
        Check($"ninguna posicion rompe una invariante ({nodos:N0} nodos)", problemas.Count == 0);
        foreach (string p in problemas.Take(5)) Console.WriteLine($"        {p}");

        Console.WriteLine();
        Console.WriteLine(_fallos == 0 ? "Todo en orden." : $"{_fallos} fallas.");
        return _fallos == 0;
    }

    /// <summary>Comprueba que en todo el arbol los dos jugadores conserven su taller.</summary>
    private static void BarrerTalleres(Juego g, ulong b, int turno, int prof, List<string> problemas) {
        if (prof == 0 || problemas.Count > 0) return;
        int pres = Juego.Presentes(b);
        if (!Juego.Hay(pres, Juego.Blanco, Juego.Taller)) { problemas.Add("el blanco perdio el taller"); return; }
        if (!Juego.Hay(pres, Juego.Negro, Juego.Taller)) { problemas.Add("el negro perdio el taller"); return; }
        if (g.Terminal(b, turno) != null) return;
        var buf = new int[Juego.MaxJugadas];
        int m = g.Jugadas(b, turno, buf.AsSpan());
        for (int i = 0; i < m; i++)
            BarrerTalleres(g, g.Aplicar(b, turno, buf[i]), 1 - turno, prof - 1, problemas);
    }

    /// <summary>
    /// Recorre todo el arbol hasta prof plies comprobando que ninguna jugada rompa las
    /// invariantes estructurales: nadie tiene dos piezas del mismo tipo, no aparecen
    /// codigos inventados, y el castillo solo aparece con los tres edificios puestos.
    /// </summary>
    private static long Barrer(Juego g, ulong b, int turno, int prof, List<string> problemas) {
        long n = 1;
        if (prof == 0) return n;
        if (g.Terminal(b, turno) != null) return n;

        var buf = new int[Juego.MaxJugadas];
        int m = g.Jugadas(b, turno, buf.AsSpan());
        for (int i = 0; i < m; i++) {
            ulong nb = g.Aplicar(b, turno, buf[i]);

            var cuenta = new int[16];
            for (int c = 0; c < 16; c++) cuenta[Juego.En(nb, c)]++;
            for (int cod = 1; cod <= 15; cod++) {
                // Con --toma-libre un bando puede juntar dos edificios del mismo tipo; las
                // unidades siguen siendo unicas siempre.
                int tope = g.R.TomaLibre && cod <= 12 && Juego.Tipo(cod) >= Juego.Taller ? 2 : 1;
                if (cuenta[cod] > tope) { problemas.Add($"la pieza {cod} aparece {cuenta[cod]} veces"); return n; }
            }

            int castillos = cuenta[Juego.Castillo] + cuenta[Juego.CastilloBlanco] + cuenta[Juego.CastilloNegro];
            if (castillos > 1) { problemas.Add("hay mas de un castillo"); return n; }
            if (castillos == 1 && !g.R.CastilloClaim) {
                int pres = Juego.Presentes(nb);
                if (Juego.Edificios(pres, turno) != 3)
                    problemas.Add("castillo levantado sin los tres edificios");
            }

            // Dos edificios pegados solo pueden venir de una toma, nunca de una obra nueva.
            if (Juego.JTipo(buf[i]) == Juego.CONSTRUIR || Juego.JTipo(buf[i]) == Juego.CORONAR) {
                int donde = Juego.JHasta(buf[i]);
                foreach (int a in g.Ady[donde])
                    if (Juego.EsEdificio(Juego.En(b, a))) problemas.Add("obra pegada a otro edificio");
            }

            n += Barrer(g, nb, 1 - turno, prof - 1, problemas);
            if (problemas.Count > 20) return n;
        }
        return n;
    }
}
