"""Candidatos para `aperturas` de cada formato, con la regla del centro.

Mismo criterio que en color/ y orden/: veredicto a 10 plies o mas, o sin decidir al tope
de 14, y filtro fisico de hasta tres elefantes (1) y tres leones (2) entre los dos sets.
A diferencia de antes hay un TOPE por formato, porque 6v6 tiene 44.100 pares: se toman
primero los no decididos y despues los de teoria mas larga, que es lo que el criterio
premia de todos modos (una teoria corta se resuelve en la mesa).

Tambien imprime lo que el README de la seccion 3 medía para las reglas viejas: que
fraccion de pares gana el que arranca.

Uso: python candidatos.py [--tope 1500] 4v4 4v5 5v4 5v5 5v6 6v5 6v6
"""
import csv
import gzip
import os
import sys
from collections import Counter

args = sys.argv[1:]
tope = 1500
if '--tope' in args:
    i = args.index('--tope')
    tope = int(args[i + 1])
    del args[i:i + 2]

for fmt in args:
    ruta = f'sw_{fmt}.csv'
    fh = open(ruta, newline='') if os.path.exists(ruta) else gzip.open(ruta + '.gz', 'rt', newline='')
    filas = list(csv.DictReader(fh))
    ver = Counter(r['verdict'] for r in filas)
    decididos = ver['white'] + ver['black']
    hondos = Counter((r['verdict'], int(r['plies'])) for r in filas if r['verdict'] in ('white', 'black'))
    print(f'== {fmt}: {len(filas)} pares  {dict(ver)}   '
          f'gana el que arranca en {100 * ver["white"] / max(1, decididos):.1f} % de los decididos')
    print('   plies:', ' '.join(f"{k[0][0]}{k[1]}:{n}" for k, n in sorted(hondos.items())))

    cand = []
    for r in filas:
        w, b = r['white'], r['black']
        if (w + b).count('1') > 3 or (w + b).count('2') > 3:
            continue
        v = r['verdict']
        if v in ('draw', 'unknown'):
            cand.append((0, 0, w, b, v))
        elif int(r['plies']) >= 10:
            cand.append((1, -int(r['plies']), w, b, v))
    cand.sort()
    total = len(cand)
    cand = cand[:tope]
    open(f'cand_{fmt}.txt', 'w', newline='\n').write(''.join(f'{w} {b}\n' for _, _, w, b, _ in cand))
    print(f'   candidatos: {total}, se toman {len(cand)}  {dict(Counter(c[4] for c in cand))}'
          + (f'  (profundidad minima tomada: {-cand[-1][1]} plies)' if cand and cand[-1][0] == 1 else ''))
