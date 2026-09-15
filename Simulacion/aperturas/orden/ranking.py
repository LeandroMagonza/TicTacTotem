"""Tabla final de los finalistas de una variante de orden.

Uso: python ranking.py asc   (lee finasc_ve{2,4,6}.csv, finasc_solve.txt y finasc_inv.txt)

Columnas: mejor apertura del primero y promedio sobre aperturas a ve2 / ve4 / ve6;
nop = minimo, sobre aperturas, de respuestas del segundo que no pierden en teoria;
ag8 = minimo de la FRACCION de respuestas que aguantan 8 plies (con la regla de orden
el segundo tiene pocas respuestas, asi que la cantidad fija de color/ no compara).
Ordena por cercania al 50 % a ve2 y ve4, penalizando una apertura por encima de 55.
"""
import csv
import re
import sys
from collections import defaultdict

v = sys.argv[1]


def leer(ve):
    porpar = defaultdict(list)
    for r in csv.DictReader(open(f'fin{v}_ve{ve}.csv', newline='')):
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
    return (t.replace('VEREDICTO: ', '').replace(' tiene victoria forzada', '')
             .replace('gana BLANCO', 'gana 1o').replace('gana NEGRO', 'gana 2o').strip())


v2, v4, v6 = leer(2), leer(4), leer(6)
teoria, inv = {}, {}
for linea in lineas(f'fin{v}_solve.txt'):
    m = re.match(r'(\d+) (\d+)\s+(.*)', linea.strip())
    if m:
        teoria[(m.group(1), m.group(2))] = corto(m.group(3))
for linea in lineas(f'fin{v}_inv.txt'):
    m = re.match(r'(\d+) (\d+)\s+invertido:\s*(.*)', linea.strip())
    if m:
        inv[(m.group(1), m.group(2))] = corto(m.group(3))


def resumen(fs):
    top = max(fs, key=lambda f: f['g1'])
    return top['g1'], sum(f['g1'] for f in fs) / len(fs), top['ap']


filas = []
for par, fs4 in v4.items():
    m4, p4, ap4 = resumen(fs4)
    m2, p2, _ = resumen(v2.get(par, fs4))
    m6, p6, _ = resumen(v6.get(par, fs4))
    puntaje = max(abs(p2 - 50), abs(p4 - 50)) + max(0, m4 - 55) + max(0, m2 - 55)
    filas.append((puntaje, par, m2, p2, m4, p4, ap4, m6, p6,
                  min(f['nop'] for f in fs4), min(f['ag'] for f in fs4)))
filas.sort()

print(f"{'primero':>7}    {'segundo':<6} {'teoria a 20 plies':<30} {'ve2 mejor/prom':>14} "
      f"{'ve4 mejor (apertura)/prom':>27} {'ve6 mejor/prom':>14} {'nop':>3} {'ag8':>4}  elef/leon rangos  invertido")
for (pt, (w, b), m2, p2, m4, p4, ap4, m6, p6, nop, ag) in filas:
    todo = w + b
    print(f"{w:>7} vs {b:<6} {teoria.get((w, b), '?')[:30]:<30} {m2:6.1f}/{p2:5.1f}   "
          f"{m4:6.1f} ({ap4:<9})/{p4:5.1f}   {m6:6.1f}/{p6:5.1f} {nop:>3} {ag:4.0%}  "
          f"{todo.count('1')}/{todo.count('2')}       {len(set(w))}/{len(set(b))}    {inv.get((w, b), '')[:30]}")
