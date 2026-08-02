"""Cruza los dos barridos para separar la ventaja de mover primero de la fuerza del set.

balance_full.csv   resuelve cada par con el set de 6 piezas empezando.
balance_espejo.csv resuelve el mismo par con el set de 5 piezas empezando.

Con los dos se puede clasificar cada par en cuatro casos, que es lo que de verdad importa
para diseñar un juego asimétrico:

  domina el de 6   el set de 6 gana empiece quien empiece: la asimetría de piezas manda
  domina el de 5   ídem al revés
  manda el turno   gana el que empieza, sea cual sea: los sets están parejos y decide el tempo
  manda ir segundo gana el que NO empieza: mover primero es una desventaja (zugzwang)

Uso:  python ambos_sentidos.py
"""
import csv
import os
import sys
from collections import Counter, defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
AQUI = os.path.dirname(os.path.abspath(__file__))

ACTUAL = ("122335", "12246")   # (set de 6, set de 5) del juego hoy


def cargar(path, quien_es_white):
    """Devuelve {(set6, set5): (set_que_gana, plies)}."""
    out = {}
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            if r["verdict"] not in ("white", "black"):
                continue
            w, b = r["white"], r["black"]
            s6, s5 = (w, b) if quien_es_white == 6 else (b, w)
            gana = w if r["verdict"] == "white" else b
            out[(s6, s5)] = ("s6" if gana == s6 else "s5", int(r["plies"]))
    return out


def seccion(t):
    print()
    print(t)
    print("=" * len(t))


def main():
    p1 = os.path.join(AQUI, "balance_full.csv")
    p2 = os.path.join(AQUI, "balance_espejo.csv")
    if not os.path.exists(p2):
        print(f"Falta {p2}. Corré primero el barrido espejo:")
        print("  solver/bin/Release/net9.0/tateti-solver.exe sweep --gen-white-size 5 "
              "--gen-black-size 6 --max-rank 6 --sum-window 0 --max-depth 15 "
              "--budget-ms 60000 --tt-bits 22 --out ../balance_espejo.csv")
        return 1

    empieza6 = cargar(p1, 6)
    empieza5 = cargar(p2, 5)
    pares = sorted(set(empieza6) & set(empieza5))
    print(f"Pares resueltos en los dos sentidos: {len(pares):,}")

    casos = Counter()
    detalle = {}
    for k in pares:
        g6, pl6 = empieza6[k]      # empieza el de 6 piezas
        g5, pl5 = empieza5[k]      # empieza el de 5 piezas
        if g6 == "s6" and g5 == "s6":
            c = "domina el de 6"
        elif g6 == "s5" and g5 == "s5":
            c = "domina el de 5"
        elif g6 == "s6" and g5 == "s5":
            c = "manda el turno"
        else:
            c = "manda ir segundo"
        casos[c] += 1
        detalle[k] = (c, g6, pl6, g5, pl5)

    seccion("¿Qué decide la partida: las piezas o el turno?")
    for c, n in casos.most_common():
        print(f"  {c:<20} {n:>8,}  {100*n/len(pares):5.1f}%  {'#'*int(28*n/len(pares))}")

    seccion("¿Sirve darle el set más débil al que empieza?")
    print("  Para cada par, el mejor reparto posible es el que empuja la victoria forzada")
    print("  más lejos. Esto mide cuál de los dos repartos conviene.")
    mejor6 = sum(1 for k in pares if detalle[k][2] > detalle[k][4])
    mejor5 = sum(1 for k in pares if detalle[k][4] > detalle[k][2])
    igual = len(pares) - mejor6 - mejor5
    print(f"    conviene que empiece el de 6 piezas : {mejor6:>8,}  ({100*mejor6/len(pares):5.1f}%)")
    print(f"    conviene que empiece el de 5 piezas : {mejor5:>8,}  ({100*mejor5/len(pares):5.1f}%)")
    print(f"    da igual                            : {igual:>8,}  ({100*igual/len(pares):5.1f}%)")

    seccion("Los repartos más profundos que existen")
    print("  (el mejor de los dos sentidos para cada par; más plies = menos encontrable)")
    ranking = []
    for k in pares:
        c, g6, pl6, g5, pl5 = detalle[k]
        if pl6 >= pl5:
            ranking.append((pl6, k[0], k[1], "el de 6 empieza", "6" if g6 == "s6" else "5"))
        else:
            ranking.append((pl5, k[1], k[0], "el de 5 empieza", "5" if g5 == "s5" else "6"))
    ranking.sort(key=lambda x: -x[0])
    print(f"  {'plies':>6}  {'empieza':<9} {'contra':<9} {'gana':<10}")
    vistos = set()
    for pl, a, b, _, gana in ranking:
        if (a, b) in vistos:
            continue
        vistos.add((a, b))
        print(f"  {pl:>6}  {a:<9} {b:<9} el de {gana} piezas")
        if len(vistos) >= 18:
            break

    seccion("Los pares más parejos: manda el turno Y la partida es profunda")
    print("  Si gana el que empieza en los dos sentidos, los sets están igualados y sólo")
    print("  decide el tempo. Sumado a mucha profundidad, es lo más cerca de 50/50 que")
    print("  este juego permite. Se ordena por la profundidad del sentido más corto,")
    print("  que es el que un jugador rompería primero.")
    parejos = []
    for k in pares:
        c, g6, pl6, g5, pl5 = detalle[k]
        if c == "manda el turno":
            parejos.append((min(pl6, pl5), pl6, pl5, k[0], k[1]))
    parejos.sort(key=lambda x: (-x[0], -(x[1] + x[2])))
    print()
    print(f"  {'peor':>5} {'6 emp.':>7} {'5 emp.':>7}  {'set de 6':<9} {'set de 5':<9}")
    for peor, pl6, pl5, s6, s5 in parejos[:15]:
        print(f"  {peor:>5} {pl6:>7} {pl5:>7}  {s6:<9} {s5:<9}")
    if not parejos:
        print("  (ninguno)")

    seccion("Tu idea del match a 3 partidas, arranca el que perdió")
    print("  Se simula con los resultados exactos, sin handicap.")
    marcadores = Counter()
    for k in pares:
        c, g6, pl6, g5, pl5 = detalle[k]
        for primero in ("s6", "s5"):
            gana = {"s6": g6, "s5": g5}
            puntos = Counter()
            arranca = primero
            for _ in range(3):
                g = gana[arranca]
                puntos[g] += 1
                arranca = "s5" if g == "s6" else "s6"   # arranca el que perdió
            a, b = puntos["s6"], puntos["s5"]
            marcadores[f"{max(a,b)}-{min(a,b)}"] += 1
    tot_m = sum(marcadores.values())
    for m, n in sorted(marcadores.items(), reverse=True):
        print(f"  match {m}: {n:>9,}  ({100*n/tot_m:5.1f}%)")
    print()
    print("  Un 2-1 significa que la regla convirtió una paliza en una serie disputada.")
    print("  Un 3-0 significa que un set domina y la alternancia no alcanza.")

    seccion("El juego hoy")
    if ACTUAL in detalle:
        c, g6, pl6, g5, pl5 = detalle[ACTUAL]
        n6 = "el de 6 (122335)" if g6 == "s6" else "el de 5 (12246)"
        n5 = "el de 6 (122335)" if g5 == "s6" else "el de 5 (12246)"
        print(f"  clasificación: {c}")
        print(f"    empieza 122335 (6 piezas) -> gana {n6} en {pl6} plies   <- reparto actual")
        print(f"    empieza 12246  (5 piezas) -> gana {n5} en {pl5} plies")
        mejor = "el actual" if pl6 >= pl5 else "invertirlo"
        print(f"  el reparto más profundo de los dos es: {mejor}")
    else:
        print("  el par del juego no está en la intersección de los dos barridos")
    return 0


if __name__ == "__main__":
    sys.exit(main())
