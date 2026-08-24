using System;
using System.Collections.Generic;
using System.Linq;

namespace ReySolver;

/// <summary>
/// Cada test comprueba una frase concreta del enunciado. Si una regla cambia, tiene que
/// cambiar el test que la nombra.
/// </summary>
public static class SelfTest {
    private static int _fallos;

    private static void Check(string nombre, bool ok) {
        Console.WriteLine($"  [{(ok ? "ok " : "FALLA")}] {nombre}");
        if (!ok) _fallos++;
    }

    private static Pos U(Pos p, int dueno, int tipo, int c)
        => new Pos(p.Ed, Juego.Con(p.Un, c, Juego.CodU(dueno, tipo)));

    private static Pos E(Pos p, int dueno, int tipo, int c)
        => new Pos(Juego.Con(p.Ed, c, Juego.CodE(dueno, tipo)), p.Un);

    private static Pos Cast(Pos p, int c)
        => new Pos(Juego.Con(p.Ed, c, Juego.CastilloCod), p.Un);

    private static List<int> Lista(Juego g, Pos p, int turno) {
        var buf = new int[Juego.MaxJugadas];
        int n = g.Jugadas(p, turno, buf.AsSpan());
        return buf.Take(n).ToList();
    }

    private static bool Tiene(Juego g, Pos p, int turno, int tipoJ, int hasta, int extra = -1)
        => Lista(g, p, turno).Any(j => Juego.JTipo(j) == tipoJ && Juego.JHasta(j) == hasta
                                       && (extra < 0 || Juego.JExtra(j) == extra));

    private static bool TieneDesde(Juego g, Pos p, int turno, int tipoJ, int desde, int hasta)
        => Lista(g, p, turno).Any(j => Juego.JTipo(j) == tipoJ && Juego.JDesde(j) == desde
                                       && Juego.JHasta(j) == hasta);

    private static bool TieneTipo(Juego g, Pos p, int turno, int tipoJ)
        => Lista(g, p, turno).Any(j => Juego.JTipo(j) == tipoJ);

    public static bool Run() {
        _fallos = 0;
        int B = Juego.Blanco, N = Juego.Negro;
        var g = new Juego(new Reglas());                                   // guarnicion prendida
        var gp = new Juego(new Reglas { ReyGuarnicion = false });          // el rey pierde el poder
        var vacio = Pos.Vacia;

        // Casillas del 4x4:   0  1  2  3
        //                     4  5  6  7
        //                     8  9 10 11
        //                    12 13 14 15

        Console.WriteLine("Arranque");

        Pos ini = g.Inicial();
        Check("cada uno arranca con el rey solo y sin ningun edificio",
              ini.Ed == UInt128.Zero &&
              Juego.CasillaRey(ini.Un, B) >= 0 && Juego.CasillaRey(ini.Un, N) >= 0 &&
              !Juego.Hay(Juego.PresU(ini.Un), B, Juego.Constructor));
        Check("la posicion inicial no es terminal", g.Terminal(ini, B) == null);
        Check("la primera jugada del blanco ya puede ser levantar un taller",
              TieneTipo(g, ini, B, Juego.CONSTRUIR));
        Check("las cuatro disposiciones son simetricas por giro de 180",
              Juego.Disposiciones.All(d => {
                  Pos a = Juego.Inicial(d);
                  UInt128 un = UInt128.Zero;
                  for (int c = 0; c < Juego.Casillas; c++) {
                      int v = Juego.En(a.Un, c);
                      if (v == 0) continue;
                      un = Juego.Con(un, Juego.Casillas - 1 - c, Juego.CodU(1 - Juego.DuenoU(v), Juego.TipoU(v)));
                  }
                  return un == a.Un;
              }));

        Console.WriteLine("El rey hace de todo mientras no tenga a quien delegarle");

        Pos solo = U(U(vacio, B, Juego.Rey, 5), N, Juego.Constructor, 6);
        solo = U(solo, N, Juego.Rey, 15);
        Check("sin edificios, el rey puede levantar los tres tipos",
              new[] { Juego.Taller, Juego.Cuartel, Juego.Iglesia }
                  .All(t => Tiene(g, solo, B, Juego.CONSTRUIR, 4, t)));
        Check("sin guerrero propio, el rey mata como un guerrero",
              Tiene(g, solo, B, Juego.MATAR, 6));
        Check("sin sacerdote propio, el rey convierte como un sacerdote",
              Tiene(g, solo, B, Juego.CONVERTIR, 6));
        Check("con --rey-no-mata el rey convierte pero no mata",
              !Tiene(new Juego(new Reglas { ReyNoMata = true }), solo, B, Juego.MATAR, 6) &&
              Tiene(new Juego(new Reglas { ReyNoMata = true }), solo, B, Juego.CONVERTIR, 6));

        Console.WriteLine("El edificio viene con su unidad adentro");

        Pos conTaller = g.Aplicar(solo, B, Juego.Jug(Juego.CONSTRUIR, 5, 4, Juego.Taller));
        Check("levantar el taller deja un constructor parado sobre el taller",
              Juego.En(conTaller.Ed, 4) == Juego.CodE(B, Juego.Taller) &&
              Juego.En(conTaller.Un, 4) == Juego.CodU(B, Juego.Constructor));
        Pos conCuartel = g.Aplicar(solo, B, Juego.Jug(Juego.CONSTRUIR, 5, 4, Juego.Cuartel));
        Check("y el cuartel deja un guerrero, y la iglesia un sacerdote",
              Juego.En(conCuartel.Un, 4) == Juego.CodU(B, Juego.Guerrero) &&
              Juego.En(g.Aplicar(solo, B, Juego.Jug(Juego.CONSTRUIR, 5, 4, Juego.Iglesia)).Un, 4)
                  == Juego.CodU(B, Juego.Sacerdote));
        Check("el rey se queda donde estaba: no entra al edificio que levanta",
              Juego.En(conTaller.Un, 5) == Juego.CodU(B, Juego.Rey));
        Check("no se puede levantar un segundo taller",
              !Lista(g, conTaller, B).Any(j => Juego.JTipo(j) == Juego.CONSTRUIR
                                               && Juego.JExtra(j) == Juego.Taller));

        var gsu2 = new Juego(new Reglas { EdificioSinUnidad = true });
        Check("con --edificio-sin-unidad el taller nace vacio",
              Juego.En(gsu2.Aplicar(solo, B, Juego.Jug(Juego.CONSTRUIR, 5, 4, Juego.Taller)).Un, 4) == 0);
        Check("y el constructor hay que desplegarlo aparte, gastando un turno",
              TieneTipo(gsu2, gsu2.Aplicar(solo, B, Juego.Jug(Juego.CONSTRUIR, 5, 4, Juego.Taller)), B, Juego.DESPLEGAR));

        Console.WriteLine("El poder prestado del rey");

        // Constructor guarnecido: parado sobre su propio taller.
        Pos guarnecido = E(U(U(vacio, B, Juego.Rey, 5), B, Juego.Constructor, 0), B, Juego.Taller, 0);
        guarnecido = U(guarnecido, N, Juego.Rey, 15);
        Check("con el constructor guarnecido en el taller, el rey sigue pudiendo construir",
              TieneDesde(g, guarnecido, B, Juego.CONSTRUIR, 5, 6));
        Check("con --rey-pierde-poder, no: alcanza con que el constructor exista",
              !TieneDesde(gp, guarnecido, B, Juego.CONSTRUIR, 5, 6));

        Pos suelto = E(U(U(vacio, B, Juego.Rey, 5), B, Juego.Constructor, 1), B, Juego.Taller, 0);
        suelto = U(suelto, N, Juego.Rey, 15);
        Check("si el constructor sale del taller, el rey pierde el poder en las dos lecturas",
              !TieneDesde(g, suelto, B, Juego.CONSTRUIR, 5, 6) &&
              !TieneDesde(gp, suelto, B, Juego.CONSTRUIR, 5, 6));
        Check("pero el constructor suelto si construye",
              TieneDesde(g, suelto, B, Juego.CONSTRUIR, 1, 2));

        // El caso que planteaste: sin cuartel, el guerrero no puede existir, asi que el rey
        // conserva su poder para siempre.
        Pos sinCuartel = U(U(vacio, B, Juego.Rey, 1), N, Juego.Constructor, 5);
        sinCuartel = U(sinCuartel, N, Juego.Rey, 15);
        Check("sin cuartel el rey siempre tiene el poder del guerrero",
              Tiene(g, sinCuartel, B, Juego.MATAR, 5) && Tiene(gp, sinCuartel, B, Juego.MATAR, 5));

        Pos conGuerrero = E(U(sinCuartel, B, Juego.Guerrero, 10), B, Juego.Cuartel, 10);
        Check("con el guerrero guarnecido en el cuartel, el rey mata",
              Tiene(g, conGuerrero, B, Juego.MATAR, 5));
        Pos guerreroFuera = E(U(sinCuartel, B, Juego.Guerrero, 9), B, Juego.Cuartel, 10);
        Check("con el guerrero fuera del cuartel, el rey ya no mata",
              !TieneDesde(g, guerreroFuera, B, Juego.MATAR, 1, 5));

        var ge = new Juego(new Reglas { ReyPorEdificio = true });
        Check("con --rey-por-edificio, tener el taller le saca el poder aunque el constructor este adentro",
              !TieneDesde(ge, guarnecido, B, Juego.CONSTRUIR, 5, 6));
        Check("y matarle el constructor no se lo devuelve: sigue controlando el taller",
              !TieneDesde(ge, new Pos(guarnecido.Ed, Juego.Con(guarnecido.Un, 0, 0)), B, Juego.CONSTRUIR, 5, 6));
        Check("pero si le toman el taller, el rey vuelve a construir",
              TieneDesde(ge, U(guarnecido, N, Juego.Guerrero, 0), B, Juego.CONSTRUIR, 5, 6));
        Check("y sin cuartel sigue teniendo el poder del guerrero, como pediste",
              Tiene(ge, sinCuartel, B, Juego.MATAR, 5));

        // Cuarta lectura: hace falta no tener NI la unidad NI el edificio.
        var gr = new Juego(new Reglas { ReyReino = true });
        Check("con --rey-reino, teniendo taller y constructor el rey no construye",
              !TieneDesde(gr, guarnecido, B, Juego.CONSTRUIR, 5, 6));
        Pos sinCon = new Pos(guarnecido.Ed, Juego.Con(guarnecido.Un, 0, 0));
        Check("le matan el constructor pero le queda el taller: sigue sin poder, desplegara otro",
              !TieneDesde(gr, sinCon, B, Juego.CONSTRUIR, 5, 6) &&
              TieneTipo(gr, sinCon, B, Juego.DESPLEGAR));
        Pos sinConNiTaller = U(sinCon, N, Juego.Guerrero, 0);
        Check("le matan el constructor Y le ocupan el taller: ahi si vuelve a construir",
              TieneDesde(gr, sinConNiTaller, B, Juego.CONSTRUIR, 5, 6));
        Check("y con el constructor vivo pero el taller ocupado, sigue sin poder",
              !TieneDesde(gr, U(guarnecido, N, Juego.Guerrero, 1), B, Juego.CONSTRUIR, 5, 6));
        Check("con --rey-por-edificio, en cambio, alcanza con perder el taller",
              TieneDesde(ge, U(guarnecido, N, Juego.Guerrero, 0), B, Juego.CONSTRUIR, 5, 6));

        Console.WriteLine("Los edificios son terreno");

        Pos terreno = U(U(vacio, B, Juego.Constructor, 4), B, Juego.Rey, 12);
        terreno = E(terreno, N, Juego.Taller, 5);
        terreno = U(terreno, N, Juego.Rey, 15);
        Check("cualquier unidad entra a un edificio vacio, sea de quien sea",
              TieneDesde(g, terreno, B, Juego.MOVER, 4, 5));
        Pos ocupado = U(terreno, N, Juego.Sacerdote, 5);
        Check("a un edificio ocupado el constructor no entra",
              !TieneDesde(g, ocupado, B, Juego.MOVER, 4, 5) &&
              !TieneDesde(g, ocupado, B, Juego.MATAR, 4, 5));
        Pos conGue = U(ocupado, B, Juego.Guerrero, 1);
        Check("a un edificio ocupado solo entra el guerrero, matando",
              TieneDesde(g, conGue, B, Juego.MATAR, 1, 5));
        Pos entro = g.Aplicar(terreno, B, Juego.Jug(Juego.MOVER, 4, 5, 0));
        Check("y despues puede seguir de largo: el edificio no atrapa a nadie",
              TieneDesde(g, entro, B, Juego.MOVER, 5, 6));
        Check("el edificio sigue siendo del que lo construyo aunque lo ocupe el otro",
              Juego.En(entro.Ed, 5) == Juego.CodE(N, Juego.Taller));

        // La carga: dos casillas en linea recta, nunca en L ni saltando.
        var gv = new Juego(new Reglas { GuerreroVeloz = true });
        Pos carga = U(U(U(vacio, B, Juego.Guerrero, 12), B, Juego.Rey, 0), N, Juego.Rey, 3);
        Check("el guerrero veloz llega a dos casillas en linea recta",
              TieneDesde(gv, carga, B, Juego.MOVER, 12, 14) && TieneDesde(gv, carga, B, Juego.MOVER, 12, 4));
        Check("y sin la regla no llega", !TieneDesde(g, carga, B, Juego.MOVER, 12, 14));
        Check("no se mueve en L: dos casillas es en linea",
              !TieneDesde(gv, carga, B, Juego.MOVER, 12, 9));
        Check("no salta por encima de nadie",
              !TieneDesde(gv, U(carga, N, Juego.Sacerdote, 13), B, Juego.MOVER, 12, 14));
        Check("pero carga hasta matar al del fondo",
              TieneDesde(gv, U(carga, N, Juego.Sacerdote, 14), B, Juego.MATAR, 12, 14));
        Check("el guerrero veloz sigue matando de al lado",
              TieneDesde(gv, U(carga, N, Juego.Sacerdote, 13), B, Juego.MATAR, 12, 13));
        Check("y solo carga el guerrero, no el constructor",
              !TieneDesde(gv, U(U(U(vacio, B, Juego.Constructor, 12), B, Juego.Rey, 0), N, Juego.Rey, 3),
                          B, Juego.MOVER, 12, 14));

        Console.WriteLine("Quien controla que");

        Check("edificio propio y vacio: lo controlas vos",
              g.Controla(E(vacio, B, Juego.Taller, 5), 5) == B);
        Check("edificio propio con una unidad enemiga encima: lo controla el enemigo",
              g.Controla(U(E(vacio, B, Juego.Taller, 5), N, Juego.Guerrero, 5), 5) == N);
        Check("edificio enemigo ocupado por tu unidad: lo controlas vos",
              g.Controla(entro, 5) == B);
        Check("el castillo vacio no es de nadie", g.Controla(Cast(vacio, 5), 5) == -1);
        Check("controlar cuenta TIPOS: dos edificios del mismo tipo son uno solo",
              g.Edificios(E(E(vacio, B, Juego.Taller, 0), B, Juego.Taller, 3), B) == 1);

        // Solo el guerrero da vuelta el control de un edificio.
        var gcg = new Juego(new Reglas { ControlGuerrero = true });
        Pos miTaller = E(vacio, B, Juego.Taller, 5);
        Check("con --control-guerrero, un constructor enemigo encima no te saca el control",
              gcg.Controla(U(miTaller, N, Juego.Constructor, 5), 5) == B);
        Check("un sacerdote enemigo tampoco",
              gcg.Controla(U(miTaller, N, Juego.Sacerdote, 5), 5) == B);
        Check("ni el rey enemigo",
              gcg.Controla(U(miTaller, N, Juego.Rey, 5), 5) == B);
        Check("el guerrero enemigo si",
              gcg.Controla(U(miTaller, N, Juego.Guerrero, 5), 5) == N);
        Check("pero el que pisa igual bloquea el despliegue, aunque no mande",
              !TieneTipo(gcg, U(E(U(vacio, B, Juego.Rey, 9), B, Juego.Taller, 5),
                                N, Juego.Constructor, 5), B, Juego.DESPLEGAR));
        Pos matando = E(U(U(vacio, B, Juego.Guerrero, 1), N, Juego.Constructor, 5), N, Juego.Taller, 5);
        matando = U(U(matando, B, Juego.Rey, 12), N, Juego.Rey, 15);
        Check("el guerrero mata al de adentro y se queda con el control en la misma jugada",
              gcg.Controla(gcg.Aplicar(matando, B, Juego.Jug(Juego.MATAR, 1, 5, 0)), 5) == B);

        Console.WriteLine("El castillo");

        // Blanco controla taller, cuartel e iglesia; su constructor esta muerto, asi que el rey construye.
        Pos tres = E(E(E(vacio, B, Juego.Taller, 0), B, Juego.Cuartel, 3), B, Juego.Iglesia, 12);
        tres = U(U(tres, B, Juego.Rey, 5), N, Juego.Rey, 15);
        Check("con los tres edificios bajo control aparece la jugada de levantar el castillo",
              Tiene(g, tres, B, Juego.CORONAR, 6));
        Check("el castillo lo puede levantar cualquiera con el poder de construir, el rey incluido",
              Lista(g, tres, B).Any(j => Juego.JTipo(j) == Juego.CORONAR && Juego.JDesde(j) == 5));
        Pos dos = new Pos(Juego.Con(tres.Ed, 12, 0), tres.Un);
        Check("con dos edificios no aparece", !TieneTipo(g, dos, B, Juego.CORONAR));
        Check("si el enemigo te ocupa un edificio, perdes el control y se cae la jugada",
              !TieneTipo(g, U(tres, N, Juego.Guerrero, 3), B, Juego.CORONAR));
        Check("no se puede levantar un segundo castillo",
              !TieneTipo(g, Cast(tres, 9), B, Juego.CORONAR));

        Pos enCastillo = Cast(new Pos(tres.Ed, Juego.Con(tres.Un, 5, 0)), 6);
        enCastillo = U(enCastillo, B, Juego.Rey, 6);
        Check("el rey parado en el castillo con los tres edificios gana",
              g.Terminal(enCastillo, N) is (Resultado.Blanco, Final.Castillo));
        Check("cualquiera puede meterse a defenderlo, y entonces el rey no puede entrar",
              !TieneDesde(g, U(Cast(tres, 6), N, Juego.Sacerdote, 6), B, Juego.MOVER, 5, 6));

        var ga = new Juego(new Reglas { CastilloAguanta = true });
        Check("con --castillo-aguanta entrar no gana: el rival contesta primero",
              ga.Terminal(enCastillo, N) == null);
        Check("y se cobra cuando al de adentro le vuelve a tocar jugar",
              ga.Terminal(enCastillo, B) is (Resultado.Blanco, Final.Castillo));
        Check("si en el medio le roban un edificio, no se cobra nada",
              ga.Terminal(U(enCastillo, N, Juego.Guerrero, 0), B) == null);

        Console.WriteLine("Entrar al castillo sin tener los tres todavia");

        // Tu caso: sin cuartel, el rey conserva el poder del guerrero, desaloja el castillo,
        // y gana al turno siguiente cuando el constructor levanta el cuartel que falta.
        Pos truco = E(E(vacio, B, Juego.Taller, 0), B, Juego.Iglesia, 12);
        truco = Cast(truco, 5);
        truco = U(U(truco, B, Juego.Rey, 1), B, Juego.Constructor, 13);
        truco = U(U(truco, N, Juego.Constructor, 5), N, Juego.Rey, 3);
        Check("el rey sin cuartel desaloja el castillo defendido", Tiene(g, truco, B, Juego.MATAR, 5));
        Pos dentro = g.Aplicar(truco, B, Juego.Jug(Juego.MATAR, 1, 5, 0));
        Check("queda adentro del castillo pero todavia no gana: le falta un edificio",
              Juego.En(dentro.Un, 5) == Juego.CodU(B, Juego.Rey) &&
              g.Edificios(dentro, B) == 2 && g.Terminal(dentro, N) == null);
        Pos gana = g.Aplicar(dentro, B, Juego.Jug(Juego.CONSTRUIR, 13, 9, Juego.Cuartel));
        Check("y gana en cuanto el constructor levanta el cuartel que faltaba",
              g.Terminal(gana, N) is (Resultado.Blanco, Final.Castillo));

        Console.WriteLine("La muerte del rey");

        Pos amenaza = U(U(vacio, B, Juego.Rey, 5), N, Juego.Guerrero, 6);
        amenaza = E(U(amenaza, N, Juego.Rey, 15), N, Juego.Cuartel, 11);
        Check("el guerrero enemigo mata al rey", Tiene(g, amenaza, N, Juego.MATAR, 5));
        Check("y quedarse sin rey es perder",
              g.Terminal(g.Aplicar(amenaza, N, Juego.Jug(Juego.MATAR, 6, 5, 0)), B)
                  is (Resultado.Negro, Final.ReyMuerto));
        Check("el sacerdote no puede convertir un rey",
              !Tiene(g, U(U(U(vacio, B, Juego.Sacerdote, 4), B, Juego.Rey, 12), N, Juego.Rey, 5),
                     B, Juego.CONVERTIR, 5));

        // El releve del sacerdote: convertir la pieza que ya tenes, pero solo si la tuya
        // esta guarnecida. El precio del poder es dejarla en casa.
        var gsr = new Juego(new Reglas { SacerdoteReleva = true });
        var gsu = new Juego(new Reglas { SacerdoteReubica = true });

        // Sacerdote blanco en 5, constructor negro pegado en 6. Taller blanco en 10.
        Pos rel = U(U(vacio, B, Juego.Sacerdote, 5), N, Juego.Constructor, 6);
        rel = E(U(U(rel, B, Juego.Rey, 0), N, Juego.Rey, 15), B, Juego.Taller, 10);

        Pos suelto2 = U(rel, B, Juego.Constructor, 9);          // el propio, fuera del taller
        Pos enCasa = U(rel, B, Juego.Constructor, 10);          // el propio, guarnecido
        Check("con la regla base, teniendo constructor propio no se convierte al enemigo",
              !Tiene(g, suelto2, B, Juego.CONVERTIR, 6) && !Tiene(g, enCasa, B, Juego.CONVERTIR, 6));
        Check("con --sacerdote-releva tampoco, si el constructor propio anda suelto",
              !Tiene(gsr, suelto2, B, Juego.CONVERTIR, 6));
        Check("pero si el constructor propio esta guarnecido en el taller, si",
              Tiene(gsr, enCasa, B, Juego.CONVERTIR, 6));
        Check("con --sacerdote-reubica vale igual, guarnecido o no: por eso es mas fuerte",
              Tiene(gsu, suelto2, B, Juego.CONVERTIR, 6) && Tiene(gsu, enCasa, B, Juego.CONVERTIR, 6));

        Pos releva = gsr.Aplicar(enCasa, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("el enemigo sale del tablero y el propio se muda del taller a esa casilla",
              Juego.En(releva.Un, 6) == Juego.CodU(B, Juego.Constructor) &&
              Juego.En(releva.Un, 10) == 0 &&
              !Juego.Hay(Juego.PresU(releva.Un), N, Juego.Constructor));
        Check("nunca quedan dos constructores del mismo bando",
              Enumerable.Range(0, Juego.Casillas)
                        .Count(c => Juego.En(releva.Un, c) == Juego.CodU(B, Juego.Constructor)) == 1);
        Check("el taller sigue en pie: el que se fue es el constructor, no el edificio",
              Juego.En(releva.Ed, 10) == Juego.CodE(B, Juego.Taller));
        Check("si el propio ya estaba fuera del tablero, se comporta como la regla base",
              g.Aplicar(rel, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0)) ==
              gsr.Aplicar(rel, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0)));
        Check("y el rey sigue siendo inconvertible con cualquiera de las dos",
              !Tiene(gsr, U(U(U(vacio, B, Juego.Sacerdote, 5), B, Juego.Rey, 0), N, Juego.Rey, 6),
                     B, Juego.CONVERTIR, 6) &&
              !Tiene(gsu, U(U(U(vacio, B, Juego.Sacerdote, 5), B, Juego.Rey, 0), N, Juego.Rey, 6),
                     B, Juego.CONVERTIR, 6));

        // El regreso del sacerdote: despues de convertir siempre deja la casilla.
        var gv2 = new Juego(new Reglas { SacerdoteVuelve = true });
        Pos vue = U(U(vacio, B, Juego.Sacerdote, 5), N, Juego.Guerrero, 6);
        vue = E(U(U(vue, B, Juego.Rey, 0), N, Juego.Rey, 15), B, Juego.Iglesia, 13);

        Pos tras = gv2.Aplicar(vue, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("con la iglesia libre, el sacerdote aterriza ahi",
              Juego.En(tras.Un, 13) == Juego.CodU(B, Juego.Sacerdote) && Juego.En(tras.Un, 5) == 0);
        Check("y la conversion se hizo igual: el guerrero enemigo paso a ser tuyo",
              Juego.En(tras.Un, 6) == Juego.CodU(B, Juego.Guerrero));

        Pos tapada = U(vue, N, Juego.Constructor, 13);
        Pos tras2 = gv2.Aplicar(tapada, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("con la iglesia tapada por el enemigo, el sacerdote sale del tablero",
              !Juego.Hay(Juego.PresU(tras2.Un), B, Juego.Sacerdote) &&
              Juego.En(tras2.Un, 6) == Juego.CodU(B, Juego.Guerrero));
        Check("y la iglesia sigue en pie, con el enemigo encima",
              Juego.En(tras2.Ed, 13) == Juego.CodE(B, Juego.Iglesia) &&
              Juego.En(tras2.Un, 13) == Juego.CodU(N, Juego.Constructor));

        Pos propia = U(vue, B, Juego.Constructor, 13);
        Check("tambien sale del tablero si la tapa una unidad propia",
              !Juego.Hay(Juego.PresU(gv2.Aplicar(propia, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0)).Un),
                         B, Juego.Sacerdote));

        Pos sinIglesia = new Pos(Juego.Con(vue.Ed, 13, 0), vue.Un);
        Check("y si no tenes iglesia, tambien: siempre deja la casilla",
              !Juego.Hay(Juego.PresU(gv2.Aplicar(sinIglesia, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0)).Un),
                         B, Juego.Sacerdote));

        Check("una vez afuera, se lo redespliega desde la iglesia como a cualquiera",
              Tiene(gv2, tras2 == tras ? tras2 : new Pos(tras2.Ed, Juego.Con(tras2.Un, 13, 0)),
                    B, Juego.DESPLEGAR, 13, Juego.Sacerdote));

        // El rey usando el poder prestado no se teletransporta: es la jugada del sacerdote.
        Pos reyConv = E(U(U(vacio, B, Juego.Rey, 5), N, Juego.Guerrero, 6), B, Juego.Iglesia, 13);
        reyConv = U(reyConv, N, Juego.Rey, 15);
        Pos trasRey = gv2.Aplicar(reyConv, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("el rey usando el poder del sacerdote no se mueve a la iglesia",
              Juego.En(trasRey.Un, 5) == Juego.CodU(B, Juego.Rey) && Juego.En(trasRey.Un, 13) == 0);

        // Tu ejemplo exacto: constructor propio parado en la iglesia, convierto al constructor
        // enemigo. El enemigo se va, el mio se muda a esa casilla, y el sacerdote entra a la
        // iglesia que acaba de quedar vacia. Primero la conversion, despues el retroceso.
        var gvr = new Juego(new Reglas { SacerdoteVuelve = true, SacerdoteReubica = true });
        Pos cad = U(U(vacio, B, Juego.Sacerdote, 5), N, Juego.Constructor, 6);
        cad = E(U(U(cad, B, Juego.Rey, 0), N, Juego.Rey, 15), B, Juego.Iglesia, 13);
        cad = U(cad, B, Juego.Constructor, 13);          // el propio, parado en la iglesia
        Pos cad2 = gvr.Aplicar(cad, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("el constructor enemigo sale del tablero",
              !Juego.Hay(Juego.PresU(cad2.Un), N, Juego.Constructor));
        Check("el constructor propio se muda a la casilla donde estaba el del oponente",
              Juego.En(cad2.Un, 6) == Juego.CodU(B, Juego.Constructor));
        Check("y el sacerdote entra a la iglesia recien vaciada",
              Juego.En(cad2.Un, 13) == Juego.CodU(B, Juego.Sacerdote) && Juego.En(cad2.Un, 5) == 0);

        // Convertir un sacerdote no retrocede: si no, seria la unica conversion que no adelanta.
        Pos csac = U(U(vacio, B, Juego.Sacerdote, 5), N, Juego.Sacerdote, 6);
        csac = E(U(U(csac, B, Juego.Rey, 0), N, Juego.Rey, 15), B, Juego.Iglesia, 13);
        Pos csac2 = gvr.Aplicar(csac, B, Juego.Jug(Juego.CONVERTIR, 5, 6, 0));
        Check("convertir al sacerdote enemigo deja al tuyo en esa casilla, sin retroceso",
              Juego.En(csac2.Un, 6) == Juego.CodU(B, Juego.Sacerdote) &&
              Juego.En(csac2.Un, 13) == 0 && Juego.En(csac2.Un, 5) == 0);
        Check("y el sacerdote enemigo desaparece: nunca hay dos",
              !Juego.Hay(Juego.PresU(csac2.Un), N, Juego.Sacerdote));

        // --vuelve-al-edificio: matar o convertir no saca del tablero, devuelve a casa.
        var gve = new Juego(new Reglas { VuelveAlEdificio = true });
        Pos mat = U(U(vacio, B, Juego.Guerrero, 5), N, Juego.Constructor, 6);
        mat = E(U(U(mat, B, Juego.Rey, 0), N, Juego.Rey, 15), N, Juego.Taller, 11);
        Pos mat2 = gve.Aplicar(mat, B, Juego.Jug(Juego.MATAR, 5, 6, 0));
        Check("con --vuelve-al-edificio, el muerto reaparece parado sobre su taller",
              Juego.En(mat2.Un, 11) == Juego.CodU(N, Juego.Constructor) &&
              Juego.En(mat2.Un, 6) == Juego.CodU(B, Juego.Guerrero));
        Check("sin la regla, el muerto queda afuera del tablero",
              !Juego.Hay(Juego.PresU(g.Aplicar(mat, B, Juego.Jug(Juego.MATAR, 5, 6, 0)).Un),
                         N, Juego.Constructor));
        Check("si el edificio esta ocupado, el muerto si sale del tablero",
              !Juego.Hay(Juego.PresU(gve.Aplicar(U(mat, N, Juego.Sacerdote, 11), B,
                                                 Juego.Jug(Juego.MATAR, 5, 6, 0)).Un),
                         N, Juego.Constructor));
        Check("el rey nunca vuelve: matarlo sigue siendo ganar",
              gve.Terminal(gve.Aplicar(U(U(U(vacio, B, Juego.Guerrero, 5), N, Juego.Rey, 6),
                                         B, Juego.Rey, 0), B, Juego.Jug(Juego.MATAR, 5, 6, 0)), N)
                  is (Resultado.Blanco, Final.ReyMuerto));

        // Bloquearle la iglesia al rival le devuelve el poder del sacerdote a su rey.
        var grr = new Juego(new Reglas { ReyReino = true });
        Pos blo = E(U(U(vacio, N, Juego.Rey, 6), B, Juego.Rey, 0), N, Juego.Iglesia, 11);
        blo = U(blo, B, Juego.Constructor, 5);          // victima adyacente al rey negro
        Check("con su iglesia libre y sin sacerdote, el rey negro NO tiene el poder",
              !Tiene(grr, blo, N, Juego.CONVERTIR, 5));
        Check("pero si le bloqueas la iglesia, el rey negro recupera el poder",
              Tiene(grr, U(blo, B, Juego.Guerrero, 11), N, Juego.CONVERTIR, 5));

        Console.WriteLine("El punto de aparicion");

        Pos muerto = E(U(vacio, B, Juego.Rey, 10), B, Juego.Taller, 0);
        muerto = U(muerto, N, Juego.Rey, 15);
        Check("una unidad muerta vuelve a salir sobre su propio edificio",
              Tiene(g, muerto, B, Juego.DESPLEGAR, 0, Juego.Constructor));
        Check("no si el edificio tiene a alguien encima",
              !TieneTipo(g, U(muerto, N, Juego.Guerrero, 0), B, Juego.DESPLEGAR));
        Check("ni si la unidad ya esta en el tablero",
              !TieneTipo(g, U(muerto, B, Juego.Constructor, 9), B, Juego.DESPLEGAR));

        Console.WriteLine("Ahogado");

        // Todo blanco trabado: el rey no tiene ningun poder prestado porque sus tres unidades
        // estan sueltas, y ninguna de las tres tiene a donde ir ni a quien convertir.
        Pos trabado = U(U(U(U(vacio, B, Juego.Guerrero, 0), B, Juego.Rey, 1), B, Juego.Sacerdote, 2), B, Juego.Constructor, 4);
        trabado = U(U(U(U(trabado, N, Juego.Constructor, 3), N, Juego.Guerrero, 5), N, Juego.Sacerdote, 6), N, Juego.Rey, 8);
        Check("posicion sin ninguna jugada legal", Lista(g, trabado, B).Count == 0);
        Check("y no es terminal por otra cosa: el que no puede jugar pierde ahi",
              g.Terminal(trabado, B) == null);

        Console.WriteLine("Simetrias y coherencia");

        Pos giro = g.Transformar(tres, 3);
        Check("una posicion y su giro tienen la misma cantidad de jugadas",
              Lista(g, tres, B).Count == Lista(g, giro, B).Count);
        Check("una posicion y su giro tienen la misma canonica", g.Canonica(tres) == g.Canonica(giro));
        Check("los dos planos giran juntos",
              Juego.En(giro.Ed, 15) == Juego.En(tres.Ed, 0) && Juego.En(giro.Un, 10) == Juego.En(tres.Un, 5));

        Console.WriteLine("Barrido exhaustivo de 5 plies");
        foreach (var (nombre, juego) in new[] { ("base", g), ("rey-pierde-poder", gp),
                                                ("no-pegado", new Juego(new Reglas { NoPegado = true })) }) {
            var problemas = new List<string>();
            long nodos = Barrer(juego, juego.Inicial(), B, 5, problemas);
            Check($"{nombre}: ninguna posicion rompe una invariante ({nodos:N0} nodos)", problemas.Count == 0);
            foreach (string x in problemas.Take(3)) Console.WriteLine($"        {x}");
        }

        // --------------------------------------------------------------------
        // El 5x5. A partir de aca el tablero cambia de tamaño para todo el proceso,
        // asi que este bloque va ultimo y no se mezcla con nada de arriba.
        // --------------------------------------------------------------------
        Console.WriteLine("La compensacion de arranque");
        var g1 = new Juego(new Reglas { AdelantaSegundo = 1 });
        var g2 = new Juego(new Reglas { AdelantaSegundo = 2 });
        var gd = new Juego(new Reglas { AdelantaSegundo = 1, CorreSegundo = -1 });
        Check("sin compensacion los reyes estan en las esquinas opuestas",
              Juego.CasillaRey(new Juego(new Reglas()).Inicial().Un, N) == 15);
        Check("adelantando una fila, el rey negro sube a la fila de arriba",
              Juego.CasillaRey(g1.Inicial().Un, N) == 11);
        Check("adelantando dos, sube dos",
              Juego.CasillaRey(g2.Inicial().Un, N) == 7);
        Check("con una columna de corrimiento queda en diagonal",
              Juego.CasillaRey(gd.Inicial().Un, N) == 10);
        Check("y el rey blanco no se mueve nunca",
              Juego.CasillaRey(g2.Inicial().Un, B) == 0 && Juego.CasillaRey(gd.Inicial().Un, B) == 0);
        Check("la compensacion rompe la simetria a proposito: ya no es un giro de 180",
              g1.Inicial() != new Juego(new Reglas()).Inicial());

        Console.WriteLine("Tablero de 5x5");
        Juego.Configurar(5);
        var g5 = new Juego(new Reglas { Lado = 5 });
        var v5 = Pos.Vacia;

        // Casillas del 5x5:   0  1  2  3  4
        //                     5  6  7  8  9
        //                    10 11 12 13 14
        //                    15 16 17 18 19
        //                    20 21 22 23 24

        Check("el tablero tiene 25 casillas", Juego.Casillas == 25 && Juego.Lado == 5);
        Check("la casilla 24 entra y se lee bien: 25x4 bits no cabrian en un ulong",
              Juego.En(Juego.Con(v5.Un, 24, Juego.CodU(N, Juego.Rey)), 24) == Juego.CodU(N, Juego.Rey));
        Check("la del medio tiene 4 vecinos y la esquina 2",
              g5.Ady[12].Length == 4 && g5.Ady[0].Length == 2 && g5.Ady[24].Length == 2);
        Check("las coordenadas siguen el tablero: 12 es c3 y 24 es e1",
              Juego.Casilla(12) == "c3" && Juego.Casilla(24) == "e1" && Juego.Casilla(0) == "a5");
        Check("las cuatro disposiciones siguen siendo simetricas por giro de 180",
              Juego.Disposiciones.All(d => {
                  Pos a5 = Juego.Inicial(d);
                  UInt128 un = UInt128.Zero;
                  for (int c = 0; c < Juego.Casillas; c++) {
                      int v = Juego.En(a5.Un, c);
                      if (v == 0) continue;
                      un = Juego.Con(un, Juego.Casillas - 1 - c, Juego.CodU(1 - Juego.DuenoU(v), Juego.TipoU(v)));
                  }
                  return un == a5.Un;
              }));
        Check("el giro de 180 lleva la esquina 0 a la 24",
              Juego.En(g5.Transformar(U(v5, B, Juego.Rey, 0), 3).Un, 24) == Juego.CodU(B, Juego.Rey));

        // Rey en el medio (12) con su taller en 6. Los vecinos del taller son 1, 5, 7 y 11,
        // asi que de las cuatro casillas donde el rey podria construir -7, 11, 13 y 17- las
        // dos primeras tocan el taller y las dos ultimas no.
        var g5np = new Juego(new Reglas { Lado = 5, NoPegado = true });
        Pos obra = E(U(U(v5, B, Juego.Rey, 12), N, Juego.Rey, 24), B, Juego.Taller, 6);
        Check("con --no-pegado no se puede construir pegado al taller",
              !TieneDesde(g5np, obra, B, Juego.CONSTRUIR, 12, 7) &&
              !TieneDesde(g5np, obra, B, Juego.CONSTRUIR, 12, 11));
        Check("pero si se puede lejos, que en un 5x5 sigue habiendo lugar",
              TieneDesde(g5np, obra, B, Juego.CONSTRUIR, 12, 13) &&
              TieneDesde(g5np, obra, B, Juego.CONSTRUIR, 12, 17));
        Check("y sin la regla las cuatro valen",
              TieneDesde(g5, obra, B, Juego.CONSTRUIR, 12, 7) &&
              TieneDesde(g5, obra, B, Juego.CONSTRUIR, 12, 11));

        Pos gana5 = E(E(E(v5, B, Juego.Taller, 0), B, Juego.Cuartel, 4), B, Juego.Iglesia, 20);
        gana5 = U(U(Cast(gana5, 12), B, Juego.Rey, 12), N, Juego.Rey, 24);
        Check("el rey en el castillo del medio con los tres edificios gana igual",
              g5.Terminal(gana5, N) is (Resultado.Blanco, Final.Castillo));

        Console.WriteLine("Correr, y los edificios como estorbo");

        var gsl = new Juego(new Reglas { Lado = 5, Desliza = true });

        // Rey blanco en 10 (fila del medio, borde izquierdo). La fila 10..14 esta abierta.
        Pos pista = U(U(v5, B, Juego.Rey, 10), N, Juego.Rey, 4);
        Check("corriendo, una unidad cruza la fila entera de un saque",
              TieneDesde(gsl, pista, B, Juego.MOVER, 10, 11) &&
              TieneDesde(gsl, pista, B, Juego.MOVER, 10, 13) &&
              TieneDesde(gsl, pista, B, Juego.MOVER, 10, 14));

        // Un edificio en 12 parte la fila al medio.
        Pos corte = E(pista, N, Juego.Taller, 12);
        Check("un edificio frena la corrida: se llega hasta la casilla de antes",
              TieneDesde(gsl, corte, B, Juego.MOVER, 10, 11));
        Check("y no se pasa de largo ni se aterriza encima desde lejos",
              !TieneDesde(gsl, corte, B, Juego.MOVER, 10, 12) &&
              !TieneDesde(gsl, corte, B, Juego.MOVER, 10, 13));
        Check("tomar ese edificio cuesta dos turnos: primero al lado, despues adentro",
              TieneDesde(gsl, U(new Pos(corte.Ed, Juego.Con(corte.Un, 10, 0)), B, Juego.Rey, 11),
                         B, Juego.MOVER, 11, 12));
        Check("sin correr, en cambio, el edificio es terreno y se entra de una",
              TieneDesde(g5, E(U(U(v5, B, Juego.Rey, 11), N, Juego.Rey, 4), N, Juego.Taller, 12),
                         B, Juego.MOVER, 11, 12));

        // Una unidad tambien frena la corrida, y ahi sigue valiendo matar de al lado.
        Pos tapon = U(pista, N, Juego.Guerrero, 12);
        Check("una unidad frena igual que un edificio",
              TieneDesde(gsl, tapon, B, Juego.MOVER, 10, 11) &&
              !TieneDesde(gsl, tapon, B, Juego.MOVER, 10, 13));
        Check("y de lejos no se la mata: matar es siempre el ultimo paso desde al lado",
              !TieneDesde(gsl, tapon, B, Juego.MATAR, 10, 12));

        var glg = new Juego(new Reglas { Lado = 5, Desliza = true, GuerreroLargo = true });
        Check("con --guerrero-largo si alcanza a la primera unidad de la fila",
              TieneDesde(glg, tapon, B, Juego.MATAR, 10, 12));
        Check("pero no dispara a traves de un edificio",
              !TieneDesde(glg, U(E(pista, N, Juego.Taller, 12), N, Juego.Guerrero, 13),
                          B, Juego.MATAR, 10, 13));

        var gsg = new Juego(new Reglas { Lado = 5, Desliza = true, SoloGuerreroCorre = true });
        Pos parJ = U(U(U(v5, N, Juego.Rey, 4), B, Juego.Guerrero, 10), B, Juego.Sacerdote, 0);
        Check("con --solo-guerrero-corre el guerrero cruza la fila",
              TieneDesde(gsg, parJ, B, Juego.MOVER, 10, 14));
        Check("y el sacerdote no: camina de a un paso",
              TieneDesde(gsg, parJ, B, Juego.MOVER, 0, 1) &&
              !TieneDesde(gsg, parJ, B, Juego.MOVER, 0, 2));

        var gsc = new Juego(new Reglas { Lado = 5, Desliza = true, SaleCaminando = true });
        // El mismo guerrero, pero parado sobre un cuartel propio en 10.
        Pos guarn = E(parJ, B, Juego.Cuartel, 10);
        Check("con --sale-caminando, guarnecido no corre: sale de a un paso",
              TieneDesde(gsc, guarn, B, Juego.MOVER, 10, 11) &&
              !TieneDesde(gsc, guarn, B, Juego.MOVER, 10, 13));
        Check("pero afuera del edificio corre como siempre",
              TieneDesde(gsc, parJ, B, Juego.MOVER, 10, 13));

        var glj = new Juego(new Reglas { Lado = 5, Desliza = true, ConstruyeLejos = true });
        Check("con --construye-lejos se levanta al fondo de la linea, no solo al lado",
              TieneDesde(glj, pista, B, Juego.CONSTRUIR, 10, 13) &&
              TieneDesde(glj, pista, B, Juego.CONSTRUIR, 10, 14));
        Check("pero no del otro lado de lo que tape la linea",
              TieneDesde(glj, corte, B, Juego.CONSTRUIR, 10, 11) &&
              !TieneDesde(glj, corte, B, Juego.CONSTRUIR, 10, 13));
        Check("y sin la regla sigue siendo solo la casilla de al lado",
              TieneDesde(gsl, pista, B, Juego.CONSTRUIR, 10, 11) &&
              !TieneDesde(gsl, pista, B, Juego.CONSTRUIR, 10, 13));

        var glj2 = new Juego(new Reglas { Lado = 5, Desliza = true, SoloGuerreroCorre = true,
                                          ConstruyeLejos = true });
        Check("construir a la vista no depende de correr: el rey lo hace igual",
              TieneDesde(glj2, pista, B, Juego.CONSTRUIR, 10, 13));

        var gal = new Juego(new Reglas { Lado = 5, Desliza = true, ConstruyeLejos = true, AlcanceObra = 2 });
        Check("con --alcance-obra 2 la obra llega a dos casillas",
              TieneDesde(gal, pista, B, Juego.CONSTRUIR, 10, 11) &&
              TieneDesde(gal, pista, B, Juego.CONSTRUIR, 10, 12));
        Check("y no mas alla",
              !TieneDesde(gal, pista, B, Juego.CONSTRUIR, 10, 13));

        var gaj = new Juego(new Reglas { Lado = 5, Desliza = true, ReyAjedrez = true });
        Check("el rey de ajedrez pisa la diagonal",
              TieneDesde(gaj, pista, B, Juego.MOVER, 10, 16));
        Check("pero no corre: la fila entera ya no es suya",
              TieneDesde(gaj, pista, B, Juego.MOVER, 10, 11) &&
              !TieneDesde(gaj, pista, B, Juego.MOVER, 10, 13));
        Check("y construye tambien en diagonal, porque su vecindario son ocho casillas",
              TieneDesde(gaj, pista, B, Juego.CONSTRUIR, 10, 16));

        var gdi = new Juego(new Reglas { Lado = 5, SacerdoteDiagonal = true, SacerdoteReubica = true });
        // Sacerdote blanco en 12, guerrero negro en 13 (ortogonal) y otro en 18 (diagonal).
        Pos cruz = U(U(U(U(v5, B, Juego.Rey, 0), N, Juego.Rey, 4), B, Juego.Sacerdote, 12), N, Juego.Guerrero, 13);
        Check("con --sacerdote-diagonal no convierte a quien tiene al lado",
              !TieneDesde(gdi, cruz, B, Juego.CONVERTIR, 12, 13));
        Check("pero si a quien tiene en diagonal, donde el guerrero no llega a contestar",
              TieneDesde(gdi, U(U(U(U(v5, B, Juego.Rey, 0), N, Juego.Rey, 4), B, Juego.Sacerdote, 12),
                                N, Juego.Guerrero, 18), B, Juego.CONVERTIR, 12, 18));

        // Jugadas repetidas serian un sesgo silencioso: el desempate al azar de
        // ElegirPractico le daria mas peso a la jugada que aparece dos veces.
        foreach (var (nombre, juego, pos) in new[] {
                     ("corriendo", gsl, corte), ("rey de ajedrez", gaj, corte),
                     ("guerrero largo", glg, tapon), ("construye lejos", glj, corte),
                     ("solo el guerrero corre", gsg, parJ), ("sale caminando", gsc, guarn) }) {
            var l = Lista(juego, pos, B);
            Check($"{nombre}: ninguna jugada sale duplicada", l.Count == l.Distinct().Count());
        }

        Console.WriteLine("Barrido exhaustivo de 4 plies en el 5x5");
        var g5sl = new Juego(new Reglas { Lado = 5, NoPegado = true, Desliza = true,
                                          ReyAjedrez = true, SacerdoteDiagonal = true });
        var g5lj = new Juego(new Reglas { Lado = 5, NoPegado = true, Desliza = true,
                                          SacerdoteDiagonal = true, ConstruyeLejos = true });
        foreach (var (nombre, juego) in new[] { ("base", g5), ("no-pegado", g5np), ("desliza", g5sl), ("construye-lejos", g5lj) }) {
            var problemas5 = new List<string>();
            long nodos5 = Barrer(juego, juego.Inicial(), B, 4, problemas5);
            Check($"{nombre}: ninguna posicion rompe una invariante ({nodos5:N0} nodos)", problemas5.Count == 0);
            foreach (string x in problemas5.Take(3)) Console.WriteLine($"        {x}");
        }

        Console.WriteLine();
        Console.WriteLine(_fallos == 0 ? "Todo en orden." : $"{_fallos} fallas.");
        return _fallos == 0;
    }

    /// <summary>
    /// Recorre el arbol comprobando que ninguna jugada rompa las invariantes estructurales:
    /// una sola unidad de cada codigo, un solo edificio de cada codigo, un solo castillo, y
    /// ningun codigo inventado. Los reyes pueden faltar (los mataron) pero nunca duplicarse.
    /// </summary>
    private static long Barrer(Juego g, Pos p, int turno, int prof, List<string> problemas) {
        long n = 1;
        if (prof == 0 || problemas.Count > 10) return n;
        if (g.Terminal(p, turno) != null) return n;

        var buf = new int[Juego.MaxJugadas];
        int m = g.Jugadas(p, turno, buf.AsSpan());
        for (int i = 0; i < m; i++) {
            Pos np = g.Aplicar(p, turno, buf[i]);

            var cu = new int[16];
            var ce = new int[16];
            for (int c = 0; c < Juego.Casillas; c++) { cu[Juego.En(np.Un, c)]++; ce[Juego.En(np.Ed, c)]++; }

            for (int cod = 9; cod < 16; cod++)
                if (cu[cod] > 0) { problemas.Add($"codigo de unidad {cod} invalido"); return n; }
            for (int cod = 8; cod < 16; cod++)
                if (ce[cod] > 0) { problemas.Add($"codigo de edificio {cod} invalido"); return n; }
            for (int cod = 1; cod <= 8; cod++)
                if (cu[cod] > 1) { problemas.Add($"la unidad {cod} aparece {cu[cod]} veces"); return n; }
            for (int cod = 1; cod <= 7; cod++)
                if (ce[cod] > 1) { problemas.Add($"el edificio {cod} aparece {ce[cod]} veces"); return n; }

            // Una unidad solo puede estar parada sobre un edificio, nunca en el aire.
            if (Juego.JTipo(buf[i]) == Juego.CONSTRUIR) {
                int donde = Juego.JHasta(buf[i]);
                if (Juego.En(p.Ed, donde) != 0 || Juego.En(p.Un, donde) != 0)
                    problemas.Add("obra encima de algo");
                if (g.R.NoPegado)
                    foreach (int a in g.Ady[donde])
                        if (Juego.En(p.Ed, a) != 0) problemas.Add("obra pegada a otro edificio");
            }

            n += Barrer(g, np, 1 - turno, prof - 1, problemas);
        }
        return n;
    }
}
