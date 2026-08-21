using System;
using System.Collections.Generic;

namespace CastilloSolver;

public struct Registro {
    public Resultado Res;
    public Final Fin;
    public int Plies;
    public int MaxEdifBlanco;    // el maximo de edificios que llego a tener cada uno
    public int MaxEdifNegro;
    public int Ramas;            // suma de jugadas legales vistas, para el factor de ramificacion

    // Diagnostico: por que la partida no llega a ningun lado.
    public int TurnosConTresSinCoronar;  // tenia los tres edificios y no tenia donde coronar
    public int TurnosConTresYCoronar;    // los tenia y si podia coronar
    public int TurnosTapiado;            // su constructor estaba en el tablero sin ninguna casilla libre al lado
    public int TurnosSinConstructor;     // su constructor estaba fuera del tablero
    public int Matanzas;                 // jugadas MATAR
    public int Tomas;                    // jugadas TOMAR
    public int TomasTaller;              // de esas, cuantas fueron de un taller
    public int Conversiones;             // jugadas CONVERTIR
    public int Obras;                    // jugadas CONSTRUIR
}

/// <summary>Como elige una jugada un jugador. La mesa ya genero la lista y descarto el ahogado.</summary>
public abstract class Politica {
    public abstract string Nombre { get; }
    public abstract int Elegir(Juego g, ulong b, int turno, ReadOnlySpan<int> jugadas, List<ulong> historia, ref Rng rng);
}

public sealed class PoliticaAzar : Politica {
    public override string Nombre => "azar";
    public override int Elegir(Juego g, ulong b, int turno, ReadOnlySpan<int> jugadas, List<ulong> historia, ref Rng rng)
        => jugadas[rng.Entre(jugadas.Length)];
}

/// <summary>Mira prof plies con evaluacion de material y desempata al azar.</summary>
public sealed class PoliticaBusqueda : Politica {
    private readonly Busqueda _bus;
    private readonly int _prof;
    public PoliticaBusqueda(Busqueda bus, int prof) { _bus = bus; _prof = prof; }
    public override string Nombre => $"ve{_prof}";
    public override int Elegir(Juego g, ulong b, int turno, ReadOnlySpan<int> jugadas, List<ulong> historia, ref Rng rng)
        => _bus.ElegirPractico(b, turno, _prof, ref rng, historia);
}

/// <summary>
/// Juega una partida entera. Reutiliza las estructuras entre partidas porque la idea es
/// correr decenas de miles.
/// </summary>
public sealed class Mesa {
    private readonly Juego _g;
    private readonly Dictionary<ulong, int> _vistas = new Dictionary<ulong, int>(512);
    private readonly List<ulong> _historia = new List<ulong>(512);
    private const ulong Sal = 0xD1B54A32D192ED03UL;

    public Mesa(Juego g) { _g = g; }

    public Registro Jugar(Politica blancas, Politica negras, ref Rng rng,
                          Action<int, int, string, ulong> traza = null) {
        _vistas.Clear();
        _historia.Clear();

        ulong b = _g.Inicial();
        int turno = Juego.Blanco;
        var reg = new Registro { MaxEdifBlanco = 1, MaxEdifNegro = 1 };
        Span<int> buf = stackalloc int[Juego.MaxJugadas];

        while (true) {
            var t = _g.Terminal(b, turno);
            if (t != null) { reg.Res = t.Value.res; reg.Fin = t.Value.fin; return reg; }

            ulong clave = b ^ (turno == Juego.Negro ? Sal : 0UL);
            _vistas.TryGetValue(clave, out int veces);
            _vistas[clave] = veces + 1;
            if (veces + 1 >= _g.R.RepeticionesEmpate) {
                reg.Res = Resultado.Empate; reg.Fin = Final.Repeticion; return reg;
            }

            int n = _g.Jugadas(b, turno, buf);
            if (n == 0) {
                reg.Res = turno == Juego.Blanco ? Resultado.Negro : Resultado.Blanco;
                reg.Fin = Final.Ahogado;
                return reg;
            }
            reg.Ramas += n;

            // Diagnostico del turno: el jugador que tiene que mover, que puede hacer.
            int presAhora = Juego.Presentes(b);
            if (Juego.Edificios(presAhora, turno) == 3) {
                bool puede = false;
                for (int i = 0; i < n; i++) if (Juego.JTipo(buf[i]) == Juego.CORONAR) { puede = true; break; }
                if (puede) reg.TurnosConTresYCoronar++; else reg.TurnosConTresSinCoronar++;
            }
            if (!Juego.Hay(presAhora, turno, Juego.Constructor)) reg.TurnosSinConstructor++;
            else {
                for (int c = 0; c < Juego.Casillas; c++) {
                    int v = Juego.En(b, c);
                    if (v == Juego.Cod(turno, Juego.Constructor)) {
                        bool libre = false;
                        foreach (int a in _g.Ady[c]) if (Juego.En(b, a) == Juego.Vacio) { libre = true; break; }
                        if (!libre) reg.TurnosTapiado++;
                        break;
                    }
                }
            }

            var politica = turno == Juego.Blanco ? blancas : negras;
            int jugada = politica.Elegir(_g, b, turno, buf.Slice(0, n), _historia, ref rng);

            switch (Juego.JTipo(jugada)) {
                case Juego.MATAR: reg.Matanzas++; break;
                case Juego.TOMAR:
                    reg.Tomas++;
                    if (Juego.Tipo(Juego.En(b, Juego.JHasta(jugada))) == Juego.Taller) reg.TomasTaller++;
                    break;
                case Juego.CONVERTIR: reg.Conversiones++; break;
                case Juego.CONSTRUIR: reg.Obras++; break;
            }

            _historia.Add(b);
            string desc = traza != null ? _g.Describir(b, turno, jugada) : null;
            b = _g.Aplicar(b, turno, jugada);
            reg.Plies++;
            traza?.Invoke(reg.Plies, turno, desc, b);

            int pres = Juego.Presentes(b);
            reg.MaxEdifBlanco = Math.Max(reg.MaxEdifBlanco, Juego.Edificios(pres, Juego.Blanco));
            reg.MaxEdifNegro = Math.Max(reg.MaxEdifNegro, Juego.Edificios(pres, Juego.Negro));

            turno = 1 - turno;
            if (reg.Plies >= _g.R.PliesMax) {
                reg.Res = Resultado.Empate; reg.Fin = Final.Limite; return reg;
            }
        }
    }
}

/// <summary>Acumula los resultados de un lote de partidas.</summary>
public sealed class Balance {
    public int Partidas;
    public int Blanco, Negro, Empate;
    public readonly int[] Finales = new int[5];
    public readonly int[] FinalGanaBlanco = new int[5];
    public long SumaPlies;
    public long SumaRamas;
    public int MasCorta = int.MaxValue, MasLarga;
    public readonly int[] EdificiosMax = new int[4];   // histograma del mejor de los dos
    public long TresSinCoronar, TresYCoronar, Tapiado, SinConstructor;
    public long Matanzas, Tomas, TomasTaller, Conversiones, Obras;
    public int ConAlgunaMatanza, ConAlgunaToma, ConAlgunaConversion;

    public void Sumar(in Registro r) {
        Partidas++;
        if (r.Res == Resultado.Blanco) Blanco++;
        else if (r.Res == Resultado.Negro) Negro++;
        else Empate++;
        Finales[(int)r.Fin]++;
        if (r.Res == Resultado.Blanco) FinalGanaBlanco[(int)r.Fin]++;
        SumaPlies += r.Plies;
        SumaRamas += r.Ramas;
        if (r.Plies < MasCorta) MasCorta = r.Plies;
        if (r.Plies > MasLarga) MasLarga = r.Plies;
        EdificiosMax[Math.Max(r.MaxEdifBlanco, r.MaxEdifNegro)]++;
        TresSinCoronar += r.TurnosConTresSinCoronar;
        TresYCoronar += r.TurnosConTresYCoronar;
        Tapiado += r.TurnosTapiado;
        SinConstructor += r.TurnosSinConstructor;
        Matanzas += r.Matanzas; Tomas += r.Tomas; TomasTaller += r.TomasTaller;
        Conversiones += r.Conversiones; Obras += r.Obras;
        if (r.Matanzas > 0) ConAlgunaMatanza++;
        if (r.Tomas > 0) ConAlgunaToma++;
        if (r.Conversiones > 0) ConAlgunaConversion++;
    }

    public void Fusionar(Balance o) {
        if (o == null) return;
        Partidas += o.Partidas; Blanco += o.Blanco; Negro += o.Negro; Empate += o.Empate;
        for (int i = 0; i < 5; i++) { Finales[i] += o.Finales[i]; FinalGanaBlanco[i] += o.FinalGanaBlanco[i]; }
        for (int i = 0; i < 4; i++) EdificiosMax[i] += o.EdificiosMax[i];
        SumaPlies += o.SumaPlies; SumaRamas += o.SumaRamas;
        TresSinCoronar += o.TresSinCoronar; TresYCoronar += o.TresYCoronar;
        Tapiado += o.Tapiado; SinConstructor += o.SinConstructor;
        Matanzas += o.Matanzas; Tomas += o.Tomas; TomasTaller += o.TomasTaller;
        Conversiones += o.Conversiones; Obras += o.Obras;
        ConAlgunaMatanza += o.ConAlgunaMatanza; ConAlgunaToma += o.ConAlgunaToma;
        ConAlgunaConversion += o.ConAlgunaConversion;
        if (o.Partidas > 0) {
            MasCorta = Math.Min(MasCorta, o.MasCorta);
            MasLarga = Math.Max(MasLarga, o.MasLarga);
        }
    }

    public double PctBlanco => 100.0 * Blanco / Partidas;
    public double PctNegro => 100.0 * Negro / Partidas;
    public double PctEmpate => 100.0 * Empate / Partidas;
    public double PliesMedio => (double)SumaPlies / Partidas;
    public double RamasMedio => SumaPlies == 0 ? 0 : (double)SumaRamas / SumaPlies;

    /// <summary>Reparto contando el empate como medio punto para cada uno.</summary>
    public double RepartoBlanco => 100.0 * (Blanco + 0.5 * Empate) / Partidas;

    public static readonly string[] NombreFinal = { "castillo", "sin constructor", "ahogado", "repeticion", "limite" };

    public void Imprimir(string titulo) {
        Console.WriteLine();
        Console.WriteLine(titulo);
        Console.WriteLine($"  partidas          {Partidas:N0}");
        Console.WriteLine($"  gana blanco       {Blanco,8:N0}  {PctBlanco,6:F2}%");
        Console.WriteLine($"  gana negro        {Negro,8:N0}  {PctNegro,6:F2}%");
        Console.WriteLine($"  empate            {Empate,8:N0}  {PctEmpate,6:F2}%");
        Console.WriteLine($"  reparto blanco    {RepartoBlanco,6:F2}%   (empate = medio punto)");
        Console.WriteLine($"  plies             media {PliesMedio:F1}   min {MasCorta}   max {MasLarga}");
        Console.WriteLine($"  jugadas por turno {RamasMedio:F1}");
        Console.WriteLine("  como termino:");
        for (int i = 0; i < 5; i++) {
            if (Finales[i] == 0) continue;
            double pct = 100.0 * Finales[i] / Partidas;
            string quien = Finales[i] > 0 ? $"   (gana blanco en {100.0 * FinalGanaBlanco[i] / Finales[i]:F1}%)" : "";
            if (i >= 3) quien = "";
            Console.WriteLine($"    {NombreFinal[i],-16} {Finales[i],8:N0}  {pct,6:F2}%{quien}");
        }
        Console.Write("  edificios que llego a juntar el que mas tuvo:");
        for (int e = 1; e <= 3; e++) Console.Write($"   {e}: {100.0 * EdificiosMax[e] / Partidas:F1}%");
        Console.WriteLine();

        double t = SumaPlies == 0 ? 1 : SumaPlies;
        Console.WriteLine("  diagnostico (sobre el total de turnos jugados):");
        Console.WriteLine($"    con los tres edificios y SIN donde coronar   {100.0 * TresSinCoronar / t,6:F2}%");
        Console.WriteLine($"    con los tres edificios y pudiendo coronar    {100.0 * TresYCoronar / t,6:F2}%");
        Console.WriteLine($"    constructor tapiado (sin casilla libre)      {100.0 * Tapiado / t,6:F2}%");
        Console.WriteLine($"    constructor fuera del tablero               {100.0 * SinConstructor / t,6:F2}%");
        Console.WriteLine($"  uso de las piezas (jugadas por partida):");
        Console.WriteLine($"    obras {(double)Obras / Partidas,5:F2}   matanzas {(double)Matanzas / Partidas,5:F2}   tomas {(double)Tomas / Partidas,5:F2}   conversiones {(double)Conversiones / Partidas,5:F2}");
        Console.WriteLine($"    partidas con al menos una:  matanza {100.0 * ConAlgunaMatanza / Partidas,5:F1}%   toma {100.0 * ConAlgunaToma / Partidas,5:F1}%   conversion {100.0 * ConAlgunaConversion / Partidas,5:F1}%");
        Console.WriteLine($"    de las {Tomas:N0} tomas de edificio, tomas de un TALLER: {TomasTaller:N0}");
    }
}
