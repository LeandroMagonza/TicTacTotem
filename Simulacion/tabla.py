"""Tabla consolidada: todas las métricas de todos los candidatos, en un solo lugar.

Las métricas son ejes independientes, no se resumen unas a otras:

  plies ida / vuelta  cuántos turnos hasta la victoria forzada, empezando cada lado
  régimen            si gana el que arranca (sets parejos) o si un set domina siempre
  libertad efectiva  entre las jugadas que no son un descuido obvio, cuántas conservan
                     lo mejor. Promedio sobre los primeros 8 turnos
  apretados          turnos donde esa libertad cae al 20 % o menos
  apertura           jugadas iniciales distintas que conservan lo mejor
  niveles/piezas     costo de producción
  disimilitud        1 = los sets no comparten ninguna pieza, 0 = son idénticos

Uso:  python tabla.py
"""
import os
import re
import subprocess
import sys
from collections import Counter

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
AQUI = os.path.dirname(os.path.abspath(__file__))
S = os.path.join(AQUI, "solver", "bin", "Release", "net9.0", "tateti-solver.exe")

CANDIDATOS = [
    ("122335", "12246", "el juego actual"),
    ("125555", "44456", "parejo a ve4"),
    ("124444", "23345", "parejo a ve4 (2)"),
    ("113444", "22345", "parejo a ve4 (3)"),
    ("111145", "22246", "parejo a ve4 (4)"),
    ("111234", "12335", "parejo a ve4 (5)"),
    ("123334", "23334", "simétrico 4 niveles"),
    ("133344", "11125", "libre pero desparejo"),
    ("233345", "22336", "libre pero desparejo 2"),
    ("113444", "12246", "profundo (23)"),
    ("13444",  "12245", "profundo (25)"),
    ("2224",   "11126", "4v5 empatado"),
]

JUEGOS = 2000


def correr(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, cwd=AQUI).stdout


def plies(w, b, prof=27):
    out = correr([S, "solve", "--white", w, "--black", b, "--max-depth", str(prof), "--tt-bits", "25"])
    m = re.search(r"VEREDICTO: gana (BLANCO|NEGRO) tiene victoria forzada en (\d+)", out)
    if m:
        return ("arranca" if m.group(1) == "BLANCO" else "segundo"), int(m.group(2))
    return "nadie", prof


def apertura(w, b):
    out = correr([S, "openings", "--white", w, "--black", b, "--max-depth", "19", "--tt-bits", "23"])
    m = re.search(r"Conservan.*: (\d+) *\((\d+) %\)", out)
    t = re.search(r"distintas.*: (\d+)", out)
    return (int(m.group(2)), f"{m.group(1)}/{t.group(1)}") if m and t else (0, "?")


def libertad(w, b):
    out = correr([S, "libertad", "--white", w, "--black", b, "--max-depth", "19",
                  "--plies", "8", "--tt-bits", "23"])
    m = re.search(r"promedio en los primeros \d+ plies: (\d+) *%", out)
    ap = re.search(r"20 % o menos de opciones: (.+)", out)
    n = len(ap.group(1).split(",")) if ap else 0
    return (int(m.group(1)) if m else 0), n


def practica(w, b):
    """Tasa de victoria del que arranca con jugadores que ven pocos plies."""
    out = correr([S, "practica", "--white", w, "--black", b,
                  "--games", str(JUEGOS), "--depths", "2,4,6"])
    v = {}
    for ln in out.splitlines():
        m = re.match(r"\s*(\d+)\s+([\d.]+)%", ln)
        if m:
            v[int(m.group(1))] = float(m.group(2))
    return v


def dissim(a, b):
    ca, cb = Counter(a), Counter(b)
    return 1 - sum((ca & cb).values()) / sum((ca | cb).values())


def main():
    if not os.path.exists(S):
        print(f"Falta el solver en {S}")
        return 1
    filas = []
    for w, b, nota in CANDIDATOS:
        g1, p1 = plies(w, b)
        g2, p2 = plies(b, w)
        if g1 == "nadie" or g2 == "nadie":
            reg = "sin resolver"
        elif g1 == "arranca" and g2 == "arranca":
            reg = "manda el turno"
        elif g1 == "arranca":
            reg = f"domina {w}"
        else:
            reg = f"domina {b}"
        pct, frac = apertura(w, b)
        lib, apr = libertad(w, b)
        pr = practica(w, b)
        filas.append(dict(
            w=w, b=b, nota=nota,
            piezas=f"{len(w)}v{len(b)}",
            niveles=len(set(w) | set(b)),
            ida=p1 if g1 != "nadie" else ">27",
            vuelta=p2 if g2 != "nadie" else ">27",
            reg=reg, lib=lib, apr=apr, ap=pct,
            ve2=pr.get(2, 0), ve4=pr.get(4, 0), ve6=pr.get(6, 0),
            dis=dissim(w, b),
        ))
        print(f"  ...{w} vs {b} listo", flush=True)

    print()
    print(f"Tasa practica sobre {JUEGOS} partidas por nivel; margen de error ~2 puntos.")
    print("ve4 = cada jugador calcula 4 plies (2 jugadas propias) hacia adelante.")
    print()
    cab = (f"{'arranca':<8}{'contra':<8}{'niv':>4}{'ida':>5}{'vta':>5}{'aper':>6}{'libert':>7}"
           f"{'ve2':>7}{'ve4':>7}{'ve6':>7}{'dis':>6}  nota")
    print(cab)
    print("-" * len(cab))
    for f in sorted(filas, key=lambda x: abs(x["ve4"] - 50)):
        print(f"{f['w']:<8}{f['b']:<8}{f['niveles']:>4}{f['ida']:>5}{f['vuelta']:>5}"
              f"{str(f['ap'])+'%':>6}{str(f['lib'])+'%':>7}"
              f"{f['ve2']:>6.1f}%{f['ve4']:>6.1f}%{f['ve6']:>6.1f}%{f['dis']:>6.2f}  {f['nota']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
