"""Reúne todos los números de los análisis en un JSON para la página de resultados.

Uso:  python datos_web.py > datos.json
"""
import csv
import glob
import json
import os
import sys
from collections import Counter, defaultdict

sys.stdout.reconfigure(encoding="utf-8")

AQUI = os.path.dirname(os.path.abspath(__file__))
RANKS = range(1, 8)


def leer(path):
    with open(path, newline="") as f:
        return list(csv.DictReader(f))


def tasas(filas, lado="white"):
    tot, win = defaultdict(int), defaultdict(int)
    for r in filas:
        if r["verdict"] not in ("white", "black"):
            continue
        k = r[lado]
        tot[k] += 1
        win[k] += r["verdict"] == lado
    return {k: 100 * win[k] / tot[k] for k in tot}


def dispersion(wr, clave):
    g = defaultdict(list)
    for s, v in wr.items():
        g[clave(s)].append(v)
    d = [max(v) - min(v) for v in g.values() if len(v) > 1]
    return {"grupos": len(g), "media": sum(d) / len(d) if d else 0.0,
            "peor": max(d) if d else 0.0}


def sustituciones(wr):
    idx = set(wr)
    out = {}
    for a in RANKS:
        for b in RANKS:
            if a == b:
                continue
            deltas = []
            for s in idx:
                if str(a) not in s:
                    continue
                nuevo = "".join(sorted(s.replace(str(a), str(b), 1)))
                if nuevo in wr:
                    deltas.append(wr[nuevo] - wr[s])
            if deltas:
                out[f"{a}->{b}"] = sum(deltas) / len(deltas)
    return out


def main():
    full = leer(os.path.join(AQUI, "balance_full.csv"))
    datos = {}

    c = Counter(r["verdict"] for r in full)
    n = len(full)
    datos["global"] = {"total": n,
                       "empieza": 100 * c["white"] / n,
                       "segundo": 100 * c["black"] / n,
                       "sin_forzar": 100 * (c["draw"] + c["unknown"]) / n}

    dec = [r for r in full if r["verdict"] in ("white", "black")]
    porp = defaultdict(lambda: {"empieza": 0, "segundo": 0})
    for r in dec:
        porp[int(r["plies"])]["empieza" if r["verdict"] == "white" else "segundo"] += 1
    datos["profundidad"] = [{"plies": p, **porp[p]} for p in sorted(porp)]

    wr_w = tasas(full, "white")
    wr_b = tasas(full, "black")
    datos["set_actual"] = {"white": "122335", "black": "12246",
                           "plies": int(next(r for r in full if r["white"] == "122335"
                                             and r["black"] == "12246")["plies"]),
                           "wr_white": wr_w["122335"], "wr_black": wr_b["12246"]}

    datos["predictores"] = []
    for k in (1, 2, 3):
        d = dispersion(wr_w, lambda s, k=k: "".join(sorted(s)[-k:]))
        datos["predictores"].append({"nombre": f"las {k} piezas más altas", **d})
    datos["predictores"].append({"nombre": "los rangos distintos que tenés",
                                 **dispersion(wr_w, lambda s: "".join(sorted(set(s))))})
    datos["predictores"].append({"nombre": "la suma total (estilo ajedrez)",
                                 **dispersion(wr_w, lambda s: sum(int(x) for x in s))})

    datos["sustitucion"] = {"empieza": sustituciones(wr_w), "segundo": sustituciones(wr_b)}

    unos = defaultdict(list)
    for s, v in wr_w.items():
        unos[s.count("1")].append(v)
    datos["unos"] = [{"n": k, "wr": sum(v) / len(v), "sets": len(v)}
                     for k, v in sorted(unos.items())]

    # un 6 frente a dos 4s, con el resto del set igual
    pares = []
    for base in ("1223", "1233", "1123", "2233", "1122", "1112", "2223"):
        a = "".join(sorted(base + "16"))
        b = "".join(sorted(base + "44"))
        if a in wr_w and b in wr_w:
            pares.append({"con6": a, "wr6": wr_w[a], "con44": b, "wr44": wr_w[b]})
    datos["seis_vs_cuatros"] = pares

    por_dif = defaultdict(Counter)
    for r in dec:
        por_dif[int(r["white_sum"]) - int(r["black_sum"])][r["verdict"]] += 1
    datos["material"] = [{"dif": d, "n": sum(por_dif[d].values()),
                          "empieza": 100 * por_dif[d]["white"] / sum(por_dif[d].values())}
                         for d in sorted(por_dif) if sum(por_dif[d].values()) >= 100]

    # familias de diseño
    NOMBRES = {
        "variante_6v5_6.csv": "6 vs 5 piezas, rangos 1-6",
        "variante_5v4_6.csv": "5 vs 4 piezas, rangos 1-6",
        "variante_6v5_5.csv": "6 vs 5 piezas, rangos 1-5",
        "variante_6v5_7.csv": "6 vs 5 piezas, rangos 1-7",
        "variante_5v5_6.csv": "5 vs 5 piezas, rangos 1-6",
        "variante_7v6_6.csv": "7 vs 6 piezas, rangos 1-6",
    }
    fams = []
    for path in sorted(glob.glob(os.path.join(AQUI, "variante_*.csv"))):
        filas = leer(path)
        if not filas:
            continue
        base = os.path.basename(path)
        cc = Counter(r["verdict"] for r in filas)
        pl = [int(r["plies"]) for r in filas if r["verdict"] in ("white", "black")]
        fams.append({"nombre": NOMBRES.get(base, base),
                     "actual": base == "variante_6v5_6.csv", "total": len(filas),
                     "empieza": 100 * cc["white"] / len(filas),
                     "segundo": 100 * cc["black"] / len(filas),
                     "sin_forzar": 100 * (cc["draw"] + cc["unknown"]) / len(filas),
                     "plies_medio": sum(pl) / len(pl) if pl else 0,
                     "profundas": 100 * sum(1 for p in pl if p >= 13) / len(filas)})
    datos["familias"] = fams

    json.dump(datos, sys.stdout, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
