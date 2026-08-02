"""Analiza el CSV que produce `tateti-solver sweep`.

Uso:  python analizar.py balance_full.csv [--set 122335/12246]
"""
import csv
import sys
from collections import Counter, defaultdict

JUEGO_ACTUAL = ("122335", "12246")


def cargar(path):
    with open(path, newline="") as f:
        return [
            {
                "white": r["white"],
                "black": r["black"],
                "wsum": int(r["white_sum"]),
                "bsum": int(r["black_sum"]),
                "verdict": r["verdict"],
                "plies": int(r["plies"]),
                "ms": int(r["ms"]),
            }
            for r in csv.DictReader(f)
        ]


def barra(frac, ancho=28):
    n = int(round(frac * ancho))
    return "#" * n + "." * (ancho - n)


def seccion(titulo):
    print()
    print(titulo)
    print("-" * len(titulo))


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "balance_full.csv"
    filas = cargar(path)
    total = len(filas)
    print(f"{path}: {total:,} enfrentamientos resueltos")

    seccion("Resultado con juego perfecto")
    cuenta = Counter(f["verdict"] for f in filas)
    etiqueta = {
        "white": "gana el que empieza",
        "black": "gana el segundo",
        "draw": "sin victoria forzada",
        "unknown": "sin resolver (tiempo)",
    }
    for v, n in cuenta.most_common():
        print(f"  {etiqueta.get(v, v):<24} {n:>7,}  {100*n/total:5.1f}%  {barra(n/total)}")

    seccion("Profundidad a la que se decide la partida")
    print("  (plies hasta la victoria forzada; mas alto = mas dificil de encontrar jugando)")
    dec = [f for f in filas if f["verdict"] in ("white", "black")]
    por_plies = Counter(f["plies"] for f in dec)
    for p in sorted(por_plies):
        n = por_plies[p]
        w = sum(1 for f in dec if f["plies"] == p and f["verdict"] == "white")
        print(f"  {p:>3} plies  {n:>7,}  {100*n/len(dec):5.1f}%   "
              f"empieza gana {100*w/n:5.1f}%   {barra(n/len(dec))}")

    seccion("Efecto de la ventaja material")
    print("  diferencia de suma (empieza - segundo) -> quien gana")
    por_dif = defaultdict(Counter)
    for f in dec:
        por_dif[f["wsum"] - f["bsum"]][f["verdict"]] += 1
    for d in sorted(por_dif):
        c = por_dif[d]
        n = sum(c.values())
        if n < 20:
            continue
        print(f"  {d:>+4}  n={n:>6,}   empieza gana {100*c['white']/n:5.1f}%   {barra(c['white']/n)}")

    seccion("Sets mas parejos (victoria forzada mas profunda)")
    print("  los que mas se acercan a un juego jugable de verdad")
    hondos = sorted(dec, key=lambda f: -f["plies"])[:15]
    for f in hondos:
        quien = "empieza" if f["verdict"] == "white" else "segundo"
        print(f"  W={f['white']:<7} B={f['black']:<7}  {f['plies']:>3} plies  gana el {quien}")

    seccion("El set que usa el juego hoy")
    match = [f for f in filas if (f["white"], f["black"]) == JUEGO_ACTUAL]
    if match:
        f = match[0]
        quien = "el que empieza (sin el 6)" if f["verdict"] == "white" else "el segundo (con el 6)"
        print(f"  W={f['white']} vs B={f['black']}: gana {quien} en {f['plies']} plies")
        peor = sum(1 for g in dec if g["plies"] < f["plies"])
        print(f"  Mas profundo que el {100*peor/len(dec):.0f}% de los enfrentamientos resueltos.")
    else:
        print(f"  {JUEGO_ACTUAL[0]} vs {JUEGO_ACTUAL[1]} no esta en este barrido.")


if __name__ == "__main__":
    main()
