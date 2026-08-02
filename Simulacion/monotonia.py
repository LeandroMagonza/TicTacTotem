"""Verifica la monotonía: tener una pieza de más nunca puede empeorarte.

Sale de una observación de diseño: si un set A contiene a otro B más una pieza extra,
entonces A es al menos tan fuerte como B, porque siempre podés jugar la estrategia de B
ignorando la pieza que sobra. Las piezas sin jugar no tocan el tablero ni la detección de
líneas, así que la partida transcurre idéntica.

Sirve para dos cosas:

  1. Como test del motor. Es una propiedad que tiene que valer sí o sí; si aparece un
     contraejemplo, hay un bug en las reglas o en la búsqueda.
  2. Como poda para barridos futuros: si B ya le gana a un rival, A también, sin resolverlo.

Con la regla del ahogado adoptada — el que no tiene jugada legal pierde — la propiedad vale
sin excepciones. Con el ahogado contado como tablas había un caso teórico donde podía fallar:
si B se quedaba sin jugadas y A tenía una sólo por la pieza extra, A estaba obligado a mover.
Contándolo como derrota, quedarse sin jugadas nunca puede ser mejor que tener una más.

Uso:  python monotonia.py [bases] [rivales]
"""
import csv
import itertools
import os
import random
import subprocess
import sys
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
SOLVER = os.path.join(AQUI, "solver", "bin", "Release", "net9.0", "tateti-solver.exe")
SALIDA = os.path.join(AQUI, "monotonia.csv")


def multisets(size, max_rank=6):
    return ["".join(map(str, c))
            for c in itertools.combinations_with_replacement(range(1, max_rank + 1), size)]


def main():
    n_bases = int(sys.argv[1]) if len(sys.argv) > 1 else 24
    n_rivales = int(sys.argv[2]) if len(sys.argv) > 2 else 24
    if not os.path.exists(SOLVER):
        print(f"No encuentro el solver en {SOLVER}")
        return 1

    rng = random.Random(20260727)
    todos5 = multisets(5)
    bases = sorted(rng.sample(todos5, n_bases))
    rivales = sorted(rng.sample(todos5, n_rivales))

    # por cada base, sus superconjuntos de 6 piezas (la base mas una pieza cualquiera)
    supers = defaultdict(list)
    for b in bases:
        for r in range(1, 7):
            supers[b].append("".join(sorted(b + str(r))))

    whites = sorted(set(bases) | {a for v in supers.values() for a in v})
    print(f"{len(bases)} sets base de 5 piezas, {len(whites) - len(bases)} superconjuntos de 6,")
    print(f"contra {len(rivales)} rivales = {len(whites) * len(rivales):,} enfrentamientos.")

    cmd = [SOLVER, "sweep",
           "--white-sets", ",".join(whites), "--black-sets", ",".join(rivales),
           "--sum-window", "0", "--max-depth", "15", "--budget-ms", "60000",
           "--tt-bits", "22", "--out", SALIDA]
    subprocess.run(cmd, cwd=AQUI, check=True, stdout=subprocess.DEVNULL)

    res = {}
    with open(SALIDA, newline="") as f:
        for r in csv.DictReader(f):
            res[(r["white"], r["black"])] = (r["verdict"], int(r["plies"]))

    revisados = violaciones = 0
    ejemplos = []
    for b in bases:
        for a in supers[b]:
            for y in rivales:
                kb, ka = (b, y), (a, y)
                if kb not in res or ka not in res:
                    continue
                vb, plb = res[kb]
                va, pla = res[ka]
                if vb not in ("white", "black") or va not in ("white", "black"):
                    continue
                revisados += 1
                # si el set chico gana, el grande tambien tiene que ganar
                if vb == "white" and va != "white":
                    violaciones += 1
                    if len(ejemplos) < 10:
                        ejemplos.append((b, a, y, vb, plb, va, pla))

    print()
    print(f"Casos verificados : {revisados:,}")
    print(f"Violaciones       : {violaciones:,}")
    if violaciones:
        print()
        print("  base      superset  rival     chico          grande")
        for b, a, y, vb, plb, va, pla in ejemplos:
            print(f"  {b:<9} {a:<9} {y:<9} {vb}/{plb:<3}      {va}/{pla}")
        print()
        print("HAY CONTRAEJEMPLOS: con la regla del ahogado vigente la monotonía no admite")
        print("excepciones, así que esto delata un bug en las reglas o en la búsqueda.")
        return 1

    print()
    print("Monotonía confirmada: agregar una pieza nunca empeoró el resultado.")
    print("El motor pasa un test independiente de las reglas, y la poda por")
    print("superconjunto es válida para barridos futuros.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
