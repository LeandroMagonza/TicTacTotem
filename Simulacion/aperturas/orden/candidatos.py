"""Candidatos para `aperturas` de cada barrido teorico con la regla de orden.

Mismo criterio que en color/: veredicto a 10 plies o mas, o tablas hasta el tope,
y un filtro fisico laxo de hasta tres elefantes (1) y tres leones (2) entre los dos
sets, porque son las piezas que agrandan la caja.

Uso: python candidatos.py asc ascsc desc descsc
"""
import csv
import gzip
import os
import sys
from collections import Counter

for v in sys.argv[1:]:
    # Los barridos se guardan comprimidos; se lee el que haya.
    ruta = f'sw_{v}.csv'
    fh = open(ruta, newline='') if os.path.exists(ruta) else gzip.open(ruta + '.gz', 'rt', newline='')
    filas = list(csv.DictReader(fh))
    ver = Counter(r['verdict'] for r in filas)
    hondos = Counter((r['verdict'], int(r['plies'])) for r in filas if r['verdict'] in ('white', 'black'))
    print(f'== {v}: {len(filas)} pares  {dict(ver)}')
    print('   plies:', ' '.join(f"{k[0][0]}{k[1]}:{n}" for k, n in sorted(hondos.items())))
    cand = []
    for r in filas:
        w, b = r['white'], r['black']
        if (w + b).count('1') > 3 or (w + b).count('2') > 3:
            continue
        if r['verdict'] == 'draw' or (r['verdict'] in ('white', 'black') and int(r['plies']) >= 10):
            cand.append((w, b, r['verdict'], r['plies']))
    por_ver = Counter(c[2] for c in cand)
    open(f'cand_{v}.txt', 'w', newline='\n').write(''.join(f'{w} {b}\n' for w, b, _, _ in cand))
    print(f'   candidatos (>= 10 plies o tablas, filtro fisico): {len(cand)}  {dict(por_ver)}')
