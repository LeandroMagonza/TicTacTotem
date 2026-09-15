"""Tabla final bajo la regla 'la primera pieza va en un lado'."""
import csv
from collections import defaultdict

def descorrer(s):
    return ''.join(str(int(c) - 1) for c in s)

def leer(ve):
    porpar = defaultdict(list)
    for f, corrido in ((f'lado_sin_ve{ve}.csv', False), (f'lado_con_ve{ve}.csv', True)):
        for r in csv.DictReader(open(f, newline='')):
            if r['casilla'] != 'lado':
                continue
            w, b = (descorrer(r['white']), descorrer(r['black'])) if corrido else (r['white'], r['black'])
            pieza = int(r['pieza']) - (1 if corrido else 0)
            porpar[(w, b)].append(dict(pieza=pieza, g1=float(r['gana1']), g2=float(r['gana2']),
                                       ganan=int(r['ganan']), aguantan=int(r['aguantan'])))
    return porpar

v2, v4, v6 = leer(2), leer(4), leer(6)
print(f"{'primero':>6}    {'segundo':<6}  {'ve2 mejor':>9}  {'ve4 mejor (pieza)':>17}  {'ve4 prom':>8}  {'ve6 mejor':>9}  "
      f"{'gan':>3} {'ag8':>3}   piedra/elef/leon")
for (w, b), fs in sorted(v4.items(), key=lambda kv: max(f['g1'] for f in kv[1])):
    top = max(fs, key=lambda f: f['g1'])
    m2 = max(f['g1'] for f in v2[(w, b)])
    m6 = max(f['g1'] for f in v6[(w, b)])
    todo = w + b
    print(f"{w:>6} vs {b:<6}  {m2:9.1f}  {top['g1']:11.1f} ({top['pieza']})    {sum(f['g1'] for f in fs)/len(fs):8.1f}  {m6:9.1f}  "
          f"{min(f['ganan'] for f in fs):>3} {min(f['aguantan'] for f in fs):>3}   {todo.count('0')}/{todo.count('1')}/{todo.count('2')}")
