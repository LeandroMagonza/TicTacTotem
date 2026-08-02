"""¿Se le puede poner un valor a cada pieza, como en el ajedrez?

En ajedrez los valores (peón=1, caballo=3, ...) funcionan porque son aproximadamente
aditivos y transitivos: si A vale más que B, A le gana a B casi sin importar el resto.
Este script mide si eso pasa acá, en vez de suponerlo.

Tres pruebas, de menos a más exigente:

  1. Sustitución: cambiar una pieza de rango a por una de rango b, ¿cuánto mueve la
     probabilidad de ganar? Promediado sobre todos los rivales posibles.
  2. Aditividad: ¿un modelo lineal "valor del set = suma de sus piezas" explica los
     resultados? Se reporta el R².
  3. Transitividad: ¿existen pares de sets incomparables, donde cada uno le gana al otro
     contra rivales distintos? Si son muchos, ningún número por pieza puede ordenarlos y
     la respuesta a la pregunta es "depende del rival".

Uso:  python valor_piezas.py balance_full.csv
"""
import csv
import sys
from collections import defaultdict
from itertools import combinations

import numpy as np

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RANKS = range(1, 10)


def cargar(path):
    whites, blacks, rows = {}, {}, []
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            w, b = r["white"], r["black"]
            if w not in whites:
                whites[w] = len(whites)
            if b not in blacks:
                blacks[b] = len(blacks)
            v = {"white": 1, "black": 0}.get(r["verdict"], -1)
            rows.append((whites[w], blacks[b], v, int(r["plies"])))

    M = np.full((len(whites), len(blacks)), -1, dtype=np.int8)
    P = np.zeros((len(whites), len(blacks)), dtype=np.int8)
    for wi, bi, v, p in rows:
        M[wi, bi] = v
        P[wi, bi] = p
    return whites, blacks, M, P


def composicion(label):
    return np.array([label.count(str(r)) for r in RANKS], dtype=float)


def seccion(t):
    print()
    print(t)
    print("=" * len(t))


def tasa_de_victoria(M, eje):
    """Fracción de enfrentamientos resueltos que gana cada set de un lado."""
    if eje == "white":
        gana, resuelto = (M == 1), (M >= 0)
        return gana.sum(1) / np.maximum(resuelto.sum(1), 1)
    gana, resuelto = (M == 0), (M >= 0)
    return gana.sum(0) / np.maximum(resuelto.sum(0), 1)


def sustituciones(labels, wr, tam):
    """Efecto medio de cambiar una pieza de rango a por una de rango b."""
    idx = {l: i for i, l in enumerate(labels)}
    delta = defaultdict(list)
    for label, i in idx.items():
        piezas = [int(c) for c in label]
        for pos, a in enumerate(piezas):
            for b in RANKS:
                if b == a:
                    continue
                nuevo = sorted(piezas[:pos] + piezas[pos + 1:] + [b])
                clave = "".join(map(str, nuevo))
                j = idx.get(clave)
                if j is not None:
                    delta[(a, b)].append(wr[j] - wr[i])
    return {k: (np.mean(v), len(v)) for k, v in delta.items()}


def aditividad(labels, wr):
    """Ajusta 'tasa de victoria = suma de valores por pieza' y reporta el R²."""
    X = np.array([composicion(l) for l in labels])
    usados = X.sum(0) > 0
    X = X[:, usados]
    coef, *_ = np.linalg.lstsq(X, wr, rcond=None)
    pred = X @ coef
    ss_res = ((wr - pred) ** 2).sum()
    ss_tot = ((wr - wr.mean()) ** 2).sum()
    return coef, [r for r, u in zip(RANKS, usados) if u], 1 - ss_res / ss_tot


def transitividad(M, eje):
    """
    Cuenta pares de sets incomparables: cada uno gana contra rivales que el otro pierde.
    Si son frecuentes, no existe un orden total y por lo tanto ningún valor escalar.
    """
    G = (M == 1).astype(np.float32) if eje == "white" else (M == 0).astype(np.float32).T
    # gana[i,k] = contra cuántos rivales gana i y no gana k
    gana = G @ (1 - G).T
    a, b = gana > 0, gana.T > 0
    n = G.shape[0]
    iu = np.triu_indices(n, 1)
    incomp = (a & b)[iu].sum()
    domina = (a & ~b)[iu].sum() + (~a & b)[iu].sum()
    iguales = (~a & ~b)[iu].sum()
    return incomp, domina, iguales, len(iu[0])


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "balance_full.csv"
    whites, blacks, M, P = cargar(path)
    wl = list(whites)
    bl = list(blacks)
    print(f"{path}: {len(wl)} sets del que empieza x {len(bl)} sets del segundo")
    resueltos = (M >= 0).sum()
    print(f"Resueltos: {resueltos:,} de {M.size:,} ({100*resueltos/M.size:.1f}%)")

    wr_w = tasa_de_victoria(M, "white")
    wr_b = tasa_de_victoria(M, "black")

    for eje, labels, wr, tam in (("EL QUE EMPIEZA", wl, wr_w, 6), ("EL SEGUNDO", bl, wr_b, 5)):
        seccion(f"{eje}: valor de cambiar una pieza por otra")
        subs = sustituciones(labels, wr, tam)
        print("  filas = pieza que sacás, columnas = pieza que ponés")
        print("  el número es el cambio en puntos porcentuales de victoria")
        presentes = sorted({a for a, _ in subs} | {b for _, b in subs})
        print("      " + "".join(f"{b:>8}" for b in presentes))
        for a in presentes:
            fila = f"  {a:>3} "
            for b in presentes:
                if a == b:
                    fila += f"{'·':>8}"
                elif (a, b) in subs:
                    fila += f"{100*subs[(a, b)][0]:>+8.1f}"
                else:
                    fila += f"{'-':>8}"
            print(fila)

        seccion(f"{eje}: ¿los valores son aditivos?")
        coef, ranks, r2 = aditividad(labels, wr)
        print("  valor ajustado por pieza (aporte a la tasa de victoria del set):")
        for r, c in zip(ranks, coef):
            print(f"    pieza {r}: {100*c:>+7.2f} pp")
        print(f"  R² del modelo aditivo: {r2:.3f}", end="  ")
        print("(cerca de 1 = los valores sirven; bajo = depende del rival)")

        seccion(f"{eje}: ¿el orden entre sets es transitivo?")
        incomp, domina, iguales, tot = transitividad(M, "white" if tam == 6 else "black")
        print(f"  pares de sets comparados: {tot:,}")
        print(f"    uno domina al otro : {domina:>9,}  ({100*domina/tot:5.1f}%)")
        print(f"    incomparables      : {incomp:>9,}  ({100*incomp/tot:5.1f}%)")
        print(f"    identicos          : {iguales:>9,}  ({100*iguales/tot:5.1f}%)")

    seccion("Preguntas concretas")
    idx_w = {l: i for i, l in enumerate(wl)}

    def wrate(label):
        i = idx_w.get(label)
        return None if i is None else 100 * wr_w[i]

    print("  ¿cuánto sirve un 1 extra? (sets del que empieza, agrupados por cantidad de 1s)")
    por_unos = defaultdict(list)
    for l, i in idx_w.items():
        por_unos[l.count("1")].append(wr_w[i])
    for n in sorted(por_unos):
        v = por_unos[n]
        print(f"    {n} pieza(s) de rango 1:  {100*np.mean(v):5.1f}% de victorias   (n={len(v)})")

    print()
    print("  un 6 contra dos 4s, mismo resto del set:")
    for base in ("1223", "1233", "1123", "2233", "1122"):
        for extra6 in RANKS:
            a = "".join(sorted(base + str(extra6) + "6"))
            b = "".join(sorted(base + "44"))
            wa, wb = wrate(a), wrate(b)
            if wa is not None and wb is not None and extra6 <= 6:
                print(f"    {a} ({wa:5.1f}%)  vs  {b} ({wb:5.1f}%)   diferencia {wa-wb:+5.1f} pp")
                break


if __name__ == "__main__":
    main()
