"""Tabla final de los finalistas de un formato, con la regla del centro.

Uso: python ranking.py 4v5   (lee fin4v5_ve{2,4,6}.csv, fin4v5_solve.txt y fin4v5_inv.txt)

Columnas: mejor apertura del primero / promedio sobre aperturas a ve2, ve4 y ve6;
rango = diferencia entre la mejor y la peor apertura a ve4; nop = minimo de respuestas del
segundo que no pierden en teoria; ag8 = minimo de la fraccion de respuestas que aguantan 8
plies; tope = quien tiene la pieza de rango mas alto (1o, 2o o los dos); inventario = cuantas
piezas de cada rango hacen falta entre los dos sets, del 1 al 5.
Ordena por cercania al 50 % a ve2 y ve4, penalizando una apertura por encima de 54.
"""
import csv
import re
import sys
from collections import defaultdict

fmt = sys.argv[1]
limite = int(sys.argv[2]) if len(sys.argv) > 2 else 30


def leer(ve):
    porpar = defaultdict(list)
    for r in csv.DictReader(open(f'fin{fmt}_ve{ve}.csv', newline='')):
        porpar[(r['white'], r['black'])].append(dict(
            ap=f"{r['pieza']} {r['casilla']}", g1=float(r['gana1']),
            nop=int(r['ganan']) + int(r['tablas']),
            ag=int(r['aguantan']) / max(1, int(r['respuestas']))))
    return porpar


def lineas(path):
    try:
        return open(path, encoding='utf-8', errors='replace', newline='').read().replace('\r', ' ').split('\n')
    except FileNotFoundError:
        return []


def corto(t):
    t = (t.replace('VEREDICTO: ', '').replace(' tiene victoria forzada', '')
          .replace('gana BLANCO', 'gana 1o').replace('gana NEGRO', 'gana 2o').strip())
    return t.replace('ninguno tiene victoria forzada en', 'nadie a').replace(' plies o menos.', '')


v2, v4, v6 = leer(2), leer(4), leer(6)
teoria, inv = {}, {}
for linea in lineas(f'fin{fmt}_solve.txt'):
    m = re.match(r'(\d+) (\d+)\s+(.*)', linea.strip())
    if m:
        teoria[(m.group(1), m.group(2))] = corto(m.group(3))
for linea in lineas(f'fin{fmt}_inv.txt'):
    m = re.match(r'(\d+) (\d+)\s+invertido:\s*(.*)', linea.strip())
    if m:
        inv[(m.group(1), m.group(2))] = corto(m.group(3))


def resumen(fs):
    top = max(fs, key=lambda f: f['g1'])
    return top['g1'], sum(f['g1'] for f in fs) / len(fs), top['ap'], top['g1'] - min(f['g1'] for f in fs)


filas = []
for par, fs4 in v4.items():
    m4, p4, ap4, rango = resumen(fs4)
    m2, p2, _, _ = resumen(v2.get(par, fs4))
    m6, p6, _, _ = resumen(v6.get(par, fs4))
    puntaje = max(abs(p2 - 50), abs(p4 - 50)) + max(0, m4 - 54) + max(0, m2 - 54)
    filas.append((puntaje, par, m2, p2, m4, p4, ap4, rango, m6, p6,
                  min(f['nop'] for f in fs4), min(f['ag'] for f in fs4)))
filas.sort()

print(f"{'primero':>7}    {'segundo':<6} {'teoria a 20':<20} {'ve2':>11} {'ve4 mejor (apertura)/prom':>27} "
      f"{'rango':>5} {'ve6':>11} {'nop':>3} {'ag8':>4} tope  inventario 1-5  invertido")
for (pt, (w, b), m2, p2, m4, p4, ap4, rango, m6, p6, nop, ag) in filas[:limite]:
    todo = w + b
    tope = '2o' if max(b) > max(w) else '1o' if max(w) > max(b) else '=='
    inventario = '-'.join(str(todo.count(str(k))) for k in range(1, 6))
    print(f"{w:>7} vs {b:<6} {teoria.get((w, b), '?')[:20]:<20} {m2:5.1f}/{p2:5.1f} "
          f"{m4:6.1f} ({ap4:<9})/{p4:5.1f}   {rango:5.1f} {m6:5.1f}/{p6:5.1f} {nop:>3} {ag:4.0%} {tope:>4}  "
          f"{inventario:<14}  {inv.get((w, b), '')[:24]}")
