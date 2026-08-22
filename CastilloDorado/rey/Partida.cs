using System;
using System.Collections.Generic;

namespace ReySolver;

/// <summary>Lo que quedo de una partida, incluidos los contadores de diagnostico.</summary>
public struct Registro {
    public Resultado Res;
    public Final Fin;
    public int Plies;

    public int Matanzas;          // jugadas MATAR
    public int MatanzasDelRey;    // de esas, cuantas las hizo un rey usando el poder prestado
    public int Obras;             // jugadas CONSTRUIR
    public int ObrasDelRey;       // de esas, cuantas las hizo el rey en vez del constructor
    public int Coronaciones;      // jugadas CORONAR
    public int Conversiones;
    public int Despliegues;
    public int Ocupaciones;       // MOVER que termina sobre un edificio ajeno o el castillo
    public int TurnosConTres;     // turnos en que el que jugaba controlaba los tres edificios
    public int TurnosReyEnCastillo;
}

public abstract class Politica {
    public abstract int Elegir(Juego g, Pos p, int turno, ref Rng rng, List<Pos> historia);
    public abstract string Nombre { get; }
}

public sealed class PoliticaAzar : Politica {
    public override string Nombre => "azar";
    public override int Elegir(Juego g, Pos p, int turno, ref Rng rng, List<Pos> historia) {
        Span<int> buf = stackalloc int[Juego.MaxJugadas];
        int n = g.Jugadas(p, turno, buf);
        return n == 0 ? -1 : buf[rng.Hasta(n)];
    }
}

public sealed class PoliticaBusqueda : Politica {
    private readonly Busqueda _b;
    private readonly int _prof;
    public PoliticaBusqueda(Busqueda b, int prof) { _b = b; _prof = prof; }
    public override string Nombre => $"ve{_prof}";
    public override int Elegir(Juego g, Pos p, int turno, ref Rng rng, List<Pos> historia)
        => _b.ElegirPractico(p, turno, _prof, ref rng, historia);
}

/// <summary>Corre partidas enteras y lleva la cuenta de las repeticiones.</summary>
public sealed class Mesa {
    private readonly Juego _g;
    private readonly Dictionary<(ulong, ulong, int), int> _vistas = new();
    private readonly List<Pos> _historia = new();

    public Mesa(Juego g) { _g = g; }

    public Registro Jugar(Politica blanco, Politica negro, ref Rng rng) {
        var reg = new Registro();
        Pos p = _g.Inicial();
        int turno = Juego.Blanco;

        _vistas.Clear();
        _historia.Clear();
        _historia.Add(p);
        _vistas[(p.Ed, p.Un, turno)] = 1;

        Span<int> buf = stackalloc int[Juego.MaxJugadas];

        while (true) {
            var fin = _g.Terminal(p, turno);
            if (fin.HasValue) { reg.Res = fin.Value.res; reg.Fin = fin.Value.fin; return reg; }

            if (reg.Plies >= _g.R.PliesMax) { reg.Res = Resultado.Empate; reg.Fin = Final.Limite; return reg; }

            if (Juego.Edificios(p, turno) == 3) reg.TurnosConTres++;
            int cc = Juego.CasillaCastillo(p.Ed);
            if (cc >= 0 && Juego.En(p.Un, cc) != 0 && Juego.TipoU(Juego.En(p.Un, cc)) == Juego.Rey)
                reg.TurnosReyEnCastillo++;

            var pol = turno == Juego.Blanco ? blanco : negro;
            int jugada = pol.Elegir(_g, p, turno, ref rng, _historia);
            if (jugada < 0) {
                // Ahogado: pierde el que no puede jugar.
                reg.Res = turno == Juego.Blanco ? Resultado.Negro : Resultado.Blanco;
                reg.Fin = Final.Ahogado;
                return reg;
            }

            switch (Juego.JTipo(jugada)) {
                case Juego.MATAR:
                    reg.Matanzas++;
                    if (Juego.TipoU(Juego.En(p.Un, Juego.JDesde(jugada))) == Juego.Rey) reg.MatanzasDelRey++;
                    break;
                case Juego.CONSTRUIR:
                    reg.Obras++;
                    if (Juego.TipoU(Juego.En(p.Un, Juego.JDesde(jugada))) == Juego.Rey) reg.ObrasDelRey++;
                    break;
                case Juego.CORONAR: reg.Coronaciones++; break;
                case Juego.CONVERTIR: reg.Conversiones++; break;
                case Juego.DESPLEGAR: reg.Despliegues++; break;
                case Juego.MOVER: {
                    int e = Juego.En(p.Ed, Juego.JHasta(jugada));
                    if (e != 0 && (Juego.EsCastillo(e) || Juego.DuenoE(e) != turno)) reg.Ocupaciones++;
                    break;
                }
            }

            p = _g.Aplicar(p, turno, jugada);
            turno = 1 - turno;
            reg.Plies++;
            _historia.Add(p);

            var clave = (p.Ed, p.Un, turno);
            _vistas.TryGetValue(clave, out int veces);
            _vistas[clave] = veces + 1;
            if (veces + 1 >= _g.R.RepeticionesEmpate) {
                reg.Res = Resultado.Empate; reg.Fin = Final.Repeticion; return reg;
            }
        }
    }
}

/// <summary>Acumula muchas partidas y las imprime.</summary>
public sealed class Balance {
    public long Partidas, GanaBlanco, GanaNegro, Empate;
    public long PliesTotal;
    public int PliesMin = int.MaxValue, PliesMax;
    public readonly long[] PorFinal = new long[5];
    public readonly long[] GanaBlancoPorFinal = new long[5];
    public long Matanzas, MatanzasDelRey, Obras, ObrasDelRey, Coronaciones, Conversiones, Despliegues, Ocupaciones;
    public long TurnosConTres, TurnosReyEnCastillo, Turnos;
    public long ConCastillo, ConAlgunaMatanza, ConAlgunaConversion;

    public void Sumar(in Registro r) {
        Partidas++;
        if (r.Res == Resultado.Blanco) GanaBlanco++;
        else if (r.Res == Resultado.Negro) GanaNegro++;
        else Empate++;
        PliesTotal += r.Plies;
        if (r.Plies < PliesMin) PliesMin = r.Plies;
        if (r.Plies > PliesMax) PliesMax = r.Plies;
        PorFinal[(int)r.Fin]++;
        if (r.Res == Resultado.Blanco) GanaBlancoPorFinal[(int)r.Fin]++;

        Matanzas += r.Matanzas; MatanzasDelRey += r.MatanzasDelRey;
        Obras += r.Obras; ObrasDelRey += r.ObrasDelRey;
        Coronaciones += r.Coronaciones; Conversiones += r.Conversiones;
        Despliegues += r.Despliegues; Ocupaciones += r.Ocupaciones;
        TurnosConTres += r.TurnosConTres; TurnosReyEnCastillo += r.TurnosReyEnCastillo;
        Turnos += r.Plies;
        if (r.Coronaciones > 0) ConCastillo++;
        if (r.Matanzas > 0) ConAlgunaMatanza++;
        if (r.Conversiones > 0) ConAlgunaConversion++;
    }

    public void Fusionar(Balance o) {
        Partidas += o.Partidas; GanaBlanco += o.GanaBlanco; GanaNegro += o.GanaNegro; Empate += o.Empate;
        PliesTotal += o.PliesTotal;
        PliesMin = Math.Min(PliesMin, o.PliesMin);
        PliesMax = Math.Max(PliesMax, o.PliesMax);
        for (int i = 0; i < PorFinal.Length; i++) { PorFinal[i] += o.PorFinal[i]; GanaBlancoPorFinal[i] += o.GanaBlancoPorFinal[i]; }
        Matanzas += o.Matanzas; MatanzasDelRey += o.MatanzasDelRey;
        Obras += o.Obras; ObrasDelRey += o.ObrasDelRey;
        Coronaciones += o.Coronaciones; Conversiones += o.Conversiones;
        Despliegues += o.Despliegues; Ocupaciones += o.Ocupaciones;
        TurnosConTres += o.TurnosConTres; TurnosReyEnCastillo += o.TurnosReyEnCastillo; Turnos += o.Turnos;
        ConCastillo += o.ConCastillo; ConAlgunaMatanza += o.ConAlgunaMatanza; ConAlgunaConversion += o.ConAlgunaConversion;
    }

    public double Reparto => Partidas == 0 ? 0 : 100.0 * (GanaBlanco + 0.5 * Empate) / Partidas;

    private static readonly string[] NombreFinal = { "castillo", "rey muerto", "ahogado", "repeticion", "limite" };

    public void Imprimir(string titulo) {
        Console.WriteLine();
        Console.WriteLine($"{titulo}");
        Console.WriteLine($"  partidas          {Partidas,10:N0}");
        Console.WriteLine($"  gana blanco       {GanaBlanco,10:N0}   {100.0 * GanaBlanco / Partidas,5:F2}%");
        Console.WriteLine($"  gana negro        {GanaNegro,10:N0}   {100.0 * GanaNegro / Partidas,5:F2}%");
        Console.WriteLine($"  empate            {Empate,10:N0}   {100.0 * Empate / Partidas,5:F2}%");
        Console.WriteLine($"  reparto blanco    {Reparto,6:F2}%   (empate = medio punto)");
        Console.WriteLine($"  plies             media {(double)PliesTotal / Partidas,4:F1}   min {PliesMin}   max {PliesMax}");
        Console.WriteLine("  como termino:");
        for (int i = 0; i < PorFinal.Length; i++) {
            if (PorFinal[i] == 0) continue;
            double pb = 100.0 * GanaBlancoPorFinal[i] / PorFinal[i];
            string cola = i == (int)Final.Repeticion || i == (int)Final.Limite ? "" : $"   (gana blanco en {pb:F1}%)";
            Console.WriteLine($"    {NombreFinal[i],-12}   {PorFinal[i],8:N0}   {100.0 * PorFinal[i] / Partidas,5:F2}%{cola}");
        }
        Console.WriteLine($"  partidas en que llego a levantarse el castillo: {100.0 * ConCastillo / Partidas,5:F2}%");
        Console.WriteLine("  uso de las piezas (jugadas por partida):");
        Console.WriteLine($"    obras {(double)Obras / Partidas,5:F2}   matanzas {(double)Matanzas / Partidas,5:F2}   conversiones {(double)Conversiones / Partidas,5:F2}   despliegues {(double)Despliegues / Partidas,5:F2}");
        Console.WriteLine($"    ocupaciones de edificio ajeno {(double)Ocupaciones / Partidas,5:F2}");
        if (Obras > 0)
            Console.WriteLine($"    de las {Obras:N0} obras, las hizo el REY: {ObrasDelRey:N0}   ({100.0 * ObrasDelRey / Obras:F1}%)");
        if (Matanzas > 0)
            Console.WriteLine($"    de las {Matanzas:N0} matanzas, las hizo el REY: {MatanzasDelRey:N0}   ({100.0 * MatanzasDelRey / Matanzas:F1}%)");
        Console.WriteLine($"    partidas con al menos una:  matanza {100.0 * ConAlgunaMatanza / Partidas,5:F1}%   conversion {100.0 * ConAlgunaConversion / Partidas,5:F1}%");
        Console.WriteLine("  diagnostico (sobre el total de turnos jugados):");
        Console.WriteLine($"    el que jugaba controlaba los tres edificios   {100.0 * TurnosConTres / Math.Max(1, Turnos),5:F2}%");
        Console.WriteLine($"    habia un rey parado en el castillo            {100.0 * TurnosReyEnCastillo / Math.Max(1, Turnos),5:F2}%");
    }
}
