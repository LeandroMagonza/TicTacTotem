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

            var cuenta = new int[14];
            for (int c = 0; c < 16; c++) {
                int v = Juego.En(nb, c);
                if (v > 13) { problemas.Add($"codigo {v} invalido"); return n; }
                cuenta[v]++;
            }
            for (int cod = 1; cod <= 13; cod++)
                if (cuenta[cod] > 1) { problemas.Add($"la pieza {cod} aparece {cuenta[cod]} veces"); return n; }

            if (cuenta[Juego.Castillo] == 1) {
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
