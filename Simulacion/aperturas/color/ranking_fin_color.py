"""Tabla final de los finalistas de una variante de color.
Uso: python ranking_fin_color.py c   (lee finc_ve{2,4,6}.csv y finc_solve.txt)"""
import csv, sys, re
from collections import defaultdict

v = sys.argv[1]

def leer(ve):
    porpar = defaultdict(list)
    for r in csv.DictReader(open(f'fin{v}_ve{ve}.csv', newline='')):
        porpar[(r['white'], r['black'])].append(dict(ap=f"{r['pieza']} {r['casilla']}", g1=float(r['gana1']),
                                                    ganan=int(r['ganan']), tablas=int(r['tablas']),
                                                    aguantan=int(r['aguantan']), resp=int(r['respuestas'])))
    return porpar

v4, v2, v6 = leer(4), leer(2), leer(6)
teoria = {}
# Los archivos de solve traen \r sueltos (los pares vienen de un txt con CRLF): se leen sin
# traducir saltos de linea y se limpian a mano.
def lineas(path):
    return open(path, encoding='utf-8', errors='replace', newline='').read().replace('\r', ' ').split('\n')
for linea in lineas(f'fin{v}_solve.txt'):
    m = re.match(r'(\d+) (\d+)\s+(.*)', linea.strip())
    if m:
        t = m.group(3)
        t = t.replace('VEREDICTO: ', '').replace(' tiene victoria forzada', '')
        teoria[(m.group(1), m.group(2))] = t[:28]

inv = {}
try:
    for linea in lineas(f'fin{v}_inv.txt'):
        m = re.match(r'(\d+) (\d+)\s+invertido:\s*(.*)', linea.strip())
        if m:
            inv[(m.group(1), m.group(2))] = m.group(3).replace('VEREDICTO: ', '').replace(' tiene victoria forzada', '')[:22]
except FileNotFoundError:
    pass

print(f"{'primero':>6}    {'segundo':<6}  {'teoria a 20 plies':<28} {'ve2':>5} {'ve4 mejor (ap)':>16} {'ve4 prom':>8} {'ve6':>5}  "
      f"{'nop':>3} {'ag8':>3} {'resp':>4}  elef/leon  rangos  invertido")
for (w, b), fs in sorted(v4.items(), key=lambda kv: max(f['g1'] for f in kv[1])):
    top = max(fs, key=lambda f: f['g1'])
    m2 = max(f['g1'] for f in v2.get((w, b), fs))
    m6 = max(f['g1'] for f in v6.get((w, b), fs))
    todo = w + b
    print(f"{w:>6} vs {b:<6}  {teoria.get((w, b), '?'):<28} {m2:5.1f} {top['g1']:9.1f} ({top['ap']:<6}) "
          f"{sum(f['g1'] for f in fs)/len(fs):8.1f} {m6:5.1f}  "
          f"{min(f['ganan'] + f['tablas'] for f in fs):>3} {min(f['aguantan'] for f in fs):>3} {min(f['resp'] for f in fs):>4}  "
          f"{todo.count('1')}/{todo.count('2')}      {len(set(w))}/{len(set(b))}     {inv.get((w, b), '')}")
