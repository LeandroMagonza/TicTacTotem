"""Ranking de los candidatos de una variante de color a partir de apc_<v>.csv.

Uso: python ranking_color.py c [max_apertura] [min_aguantan]
"""
import csv, sys
from collections import defaultdict, Counter

v = sys.argv[1]
MAX_AP = float(sys.argv[2]) if len(sys.argv) > 2 else 58.0
MIN_AG = int(sys.argv[3]) if len(sys.argv) > 3 else 8

porpar = defaultdict(list)
for r in csv.DictReader(open(f'apc_{v}.csv', newline='')):
    porpar[(r['white'], r['black'])].append(dict(
        ap=f"{r['pieza']} {r['casilla']}", resp=int(r['respuestas']), ganan=int(r['ganan']),
        tablas=int(r['tablas']), aguantan=int(r['aguantan']), plies=int(r['plies']), g1=float(r['gana1'])))

datos = []
for (w, b), fs in porpar.items():
    fs.sort(key=lambda f: -f['g1'])
    teoria = 'primero' if any(f['ganan'] == 0 and f['tablas'] == 0 for f in fs) \
             else 'tablas' if any(f['ganan'] == 0 for f in fs) else 'segundo'
    todo = w + b
    datos.append(dict(white=w, black=b, teoria=teoria, max=fs[0]['g1'], ap=fs[0]['ap'],
                      prom=sum(f['g1'] for f in fs) / len(fs), n_ap=len(fs),
                      min_ganan=min(f['ganan'] for f in fs), min_nop=min(f['ganan'] + f['tablas'] for f in fs),
                      min_ag=min(f['aguantan'] for f in fs), resp=min(f['resp'] for f in fs),
                      elef=todo.count('1'), leon=todo.count('2'),
                      var=min(len(set(w)), len(set(b))), vartxt=f"{len(set(w))}/{len(set(b))}"))

cab = (f"{'primero':>6}    {'segundo':<6} {'teoria':<8} {'mejor':>5}  {'apertura':<10} {'prom':>5}  "
       f"{'gan':>3} {'nop':>3} {'ag8':>3} {'resp':>4}  elef/leon  rangos")
def fila(d):
    return (f"{d['white']:>6} vs {d['black']:<6} {d['teoria']:<8} {d['max']:5.1f}  {d['ap']:<10} {d['prom']:5.1f}  "
            f"{d['min_ganan']:>3} {d['min_nop']:>3} {d['min_ag']:>3} {d['resp']:>4}  {d['elef']}/{d['leon']}      {d['vartxt']}")

print(f'variante {v}: {len(datos)} enfrentamientos. teoria: {dict(Counter(d["teoria"] for d in datos))}')
print('ag8 (min sobre aperturas):', sorted(Counter(d['min_ag'] for d in datos).items()))
print()
print(f'== mejor apertura <= {MAX_AP}, promedio 42-58, ag8 >= {MIN_AG}; ordenados por mejor apertura')
print(cab)
sel = sorted([d for d in datos if d['max'] <= MAX_AP and 42 <= d['prom'] <= 58 and d['min_ag'] >= MIN_AG],
             key=lambda d: (d['max'], abs(d['prom'] - 50)))
for d in sel[:30]:
    print(fila(d))
print(f'   ({len(sel)} en total)')
print()
print(f'== con variedad: 4 o 5 rangos distintos en cada lado, mejor apertura <= {MAX_AP}, promedio 42-58, ag8 >= {MIN_AG}')
print(cab)
selv = [d for d in sel if d['var'] >= 4]
for d in sorted(selv, key=lambda d: (-d['min_nop'], d['max']))[:25]:
    print(fila(d))
print(f'   ({len(selv)} en total)')
print()
print('== promedio 42-58, ordenados por mejor apertura, sin mirar ag8')
print(cab)
for d in sorted([d for d in datos if 42 <= d['prom'] <= 58], key=lambda d: (d['max'], abs(d['prom'] - 50)))[:15]:
    print(fila(d))
