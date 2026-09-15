"""De cada barrido teorico de una variante de color saca los candidatos para
`aperturas`: veredicto a >= 10 plies (o empate hasta 14) y filtro fisico laxo
(hasta tres elefantes y tres leones entre los dos sets)."""
import csv, sys
from collections import Counter

for nombre in sys.argv[1:]:
    filas = list(csv.DictReader(open(f'sw_{nombre}.csv', newline='')))
    ver = Counter(r['verdict'] for r in filas)
    plies = Counter((r['verdict'], int(r['plies'])) for r in filas)
    print(f'== {nombre}: {len(filas)} pares  {dict(ver)}')
    print('   plies:', ' '.join(f"{v[0][0]}{v[1]}:{n}" for v, n in sorted(plies.items())))
    cand = []
    for r in filas:
        w, b = r['white'], r['black']
        if (w + b).count('1') > 2 or (w + b).count('2') > 2:
            continue
        if r['verdict'] == 'draw' or (r['verdict'] in ('white', 'black') and int(r['plies']) >= 10):
            cand.append((w, b))
    open(f'cand_{nombre}.txt', 'w').write('\n'.join(f'{w} {b}' for w, b in cand) + '\n')
    print(f'   candidatos (>= 10 plies o empate, filtro fisico): {len(cand)}')
