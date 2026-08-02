"""Compara familias de diseño: 6v5, 5v4, rangos 1-5, rangos 1-7, simétrico.

Para cada familia toma una muestra aleatoria (semilla fija) de sets de cada lado, resuelve
el producto cruzado con el solver exacto y reporta cuán profundo se decide la partida.

La profundidad es la métrica que importa: en este juego casi no hay tablas con juego
perfecto, así que "balanceado" no puede significar empate. Lo que sí se puede diseñar es
que la victoria forzada esté lo bastante lejos como para que ningún humano la encuentre.

Uso:  python variantes.py [muestra]
"""
import csv
import itertools
import os
import random
import subprocess
import sys
from collections import Counter

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
SOLVER = os.path.join(AQUI, "solver", "bin", "Release", "net9.0", "tateti-solver.exe")

FAMILIAS = [
    # nombre,                    piezas del que empieza, del segundo, rango maximo
    ("6v5 rangos 1-6 (actual)",  6, 5, 6),
    ("5v4 rangos 1-6",           5, 4, 6),
    ("6v5 rangos 1-5",           6, 5, 5),
    ("6v5 rangos 1-7",           6, 5, 7),
    ("5v5 rangos 1-6 (simetrico)", 5, 5, 6),
    ("7v6 rangos 1-6",           7, 6, 6),
]

FIJOS = {("6v5 rangos 1-6 (actual)"): (["122335"], ["12246"])}


def multisets(size, max_rank):
    return ["".join(map(str, c))
            for c in itertools.combinations_with_replacement(range(1, max_rank + 1), size)]


def correr(nombre, w_size, b_size, max_rank, muestra, max_depth=15, budget=30000):
    rng = random.Random(20260727)
    todos_w = multisets(w_size, max_rank)
    todos_b = multisets(b_size, max_rank)
    ws = sorted(rng.sample(todos_w, min(muestra, len(todos_w))))
    bs = sorted(rng.sample(todos_b, min(muestra, len(todos_b))))

    # el set real del juego siempre entra, para poder ubicarlo en su familia
    extra = FIJOS.get(nombre)
    if extra:
        for s in extra[0]:
            if s not in ws:
                ws.append(s)
        for s in extra[1]:
            if s not in bs:
                bs.append(s)

    salida = os.path.join(AQUI, f"variante_{nombre.split()[0]}_{max_rank}.csv")
    cmd = [SOLVER, "sweep",
           "--white-sets", ",".join(ws), "--black-sets", ",".join(bs),
           "--sum-window", "0", "--max-depth", str(max_depth),
           "--budget-ms", str(budget), "--tt-bits", "22", "--out", salida]
    print(f"  {nombre}: {len(ws)}x{len(bs)} = {len(ws)*len(bs):,} enfrentamientos...", flush=True)
    subprocess.run(cmd, cwd=AQUI, check=True, stdout=subprocess.DEVNULL)

    with open(salida, newline="") as f:
        return list(csv.DictReader(f))


def resumir(nombre, filas):
    total = len(filas)
    c = Counter(r["verdict"] for r in filas)
    dec = [r for r in filas if r["verdict"] in ("white", "black")]
    plies = [int(r["plies"]) for r in dec]
    prof = sum(1 for p in plies if p >= 13)
    return {
        "familia": nombre,
        "total": total,
        "empieza": 100 * c["white"] / total,
        "segundo": 100 * c["black"] / total,
        "sin_forzar": 100 * (c["draw"] + c["unknown"]) / total,
        "plies_medio": sum(plies) / len(plies) if plies else 0,
        "plies_max": max(plies) if plies else 0,
        "profundas": 100 * prof / total,
    }


def main():
    muestra = int(sys.argv[1]) if len(sys.argv) > 1 else 40
    if not os.path.exists(SOLVER):
        print(f"No encuentro el solver en {SOLVER}\nCompilalo con: cd solver && dotnet build -c Release")
        return 1

    print(f"Muestra de {muestra} sets por lado en cada familia.\n")
    res = []
    for nombre, w, b, r in FAMILIAS:
        try:
            res.append(resumir(nombre, correr(nombre, w, b, r, muestra)))
        except subprocess.CalledProcessError as e:
            print(f"  {nombre}: fallo ({e})")

    print()
    cab = f"{'familia':<28}{'empieza':>9}{'segundo':>9}{'sin forzar':>12}{'plies med':>11}{'plies max':>11}{'>=13 plies':>12}"
    print(cab)
    print("-" * len(cab))
    for r in sorted(res, key=lambda x: -x["plies_medio"]):
        print(f"{r['familia']:<28}{r['empieza']:>8.1f}%{r['segundo']:>8.1f}%"
              f"{r['sin_forzar']:>11.1f}%{r['plies_medio']:>11.1f}{r['plies_max']:>11}{r['profundas']:>11.1f}%")
    print()
    print("plies med  = a que profundidad se decide en promedio (mas alto = mas jugable)")
    print(">=13 plies = fraccion de enfrentamientos donde la victoria forzada es")
    print("             lo bastante profunda como para que un humano no la vea")
    return 0


if __name__ == "__main__":
    sys.exit(main())
