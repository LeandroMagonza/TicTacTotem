"""Ranking con la columna `aguantan`: respuestas del segundo que no pierden dentro
de 8 plies, aunque pierdan en teoria. Para un humano esas no son jugadas forzadas.

Lee ap2_sin.csv (rangos 1-5), ap2_con.csv (rangos corridos, 1 = piedra) y
ap2_emp.csv (empates teoricos, rangos 1-5).
"""
import csv, sys
from collections import defaultdict

MIN_AGUANTAN = int(sys.argv[1]) if len(sys.argv) > 1 else 8

def descorrer(s):
    return ''.join(str(int(c) - 1) for c in s)

def leer(filas, corrido):
    porpar = defaultdict(list)
    for r in csv.DictReader(open(filas, newline='')):
        w, b = (descorrer(r['white']), descorrer(r['black'])) if corrido else (r['white'], r['black'])
        pieza = int(r['pieza']) - (1 if corrido else 0)
        porpar[(w, b)].append(dict(ap=f"{pieza} {r['casilla']}", resp=int(r['respuestas']), ganan=int(r['ganan']),
                                   tablas=int(r['tablas']), pierden=int(r['pierden']), aguantan=int(r['aguantan']),
                                   plies=int(r['plies']), g1=float(r['gana1']), g2=float(r['gana2'])))
    out = []
    for (w, b), fs in porpar.items():
        fs.sort(key=lambda f: -f['g1'])
        teoria = 'primero' if any(f['ganan'] == 0 and f['tablas'] == 0 for f in fs) \
                 else 'tablas' if any(f['ganan'] == 0 for f in fs) else 'segundo'
        todo = w + b
        out.append(dict(white=w, black=b, teoria=teoria, max=fs[0]['g1'], ap=fs[0]['ap'],
                        prom=sum(f['g1'] for f in fs) / len(fs),
                        min_ganan=min(f['ganan'] for f in fs),
                        min_nopierden=min(f['ganan'] + f['tablas'] for f in fs),
                        min_aguantan=min(f['aguantan'] for f in fs),
                        resp=min(f['resp'] for f in fs),
                        piedra=todo.count('0'), elef=todo.count('1'), leon=todo.count('2')))
    return out

datos = []
for f, corrido in (('ap2_sin.csv', False), ('ap2_con.csv', True), ('ap2_emp.csv', False)):
    try:
        datos += leer(f, corrido)
    except FileNotFoundError:
        print(f'(falta {f})')

cab = (f"{'primero':>6}    {'segundo':<6} {'teoria':<8} {'mejor':>5}  {'apertura':<10} {'prom':>5}  "
       f"{'gan':>3} {'nop':>3} {'ag8':>3}   piedra/elef/leon")
def fila(d):
    return (f"{d['white']:>6} vs {d['black']:<6} {d['teoria']:<8} {d['max']:5.1f}  {d['ap']:<10} {d['prom']:5.1f}  "
            f"{d['min_ganan']:>3} {d['min_nopierden']:>3} {d['min_aguantan']:>3}   {d['piedra']}/{d['elef']}/{d['leon']}")

print(f'{len(datos)} enfrentamientos. Columnas: minimo sobre aperturas de respuestas del 2o que ganan (gan), '
      f'que no pierden en teoria (nop), que aguantan 8 plies (ag8).\n')

from collections import Counter
print('== distribucion de ag8:', sorted(Counter(d['min_aguantan'] for d in datos).items()))
print('== teoria:', dict(Counter(d['teoria'] for d in datos)))

print()
print(f'== ag8 >= {MIN_AGUANTAN} y promedio 42-58, ordenados por mejor apertura')
print(cab)
sel = sorted([d for d in datos if d['min_aguantan'] >= MIN_AGUANTAN and 42 <= d['prom'] <= 58],
             key=lambda d: (d['max'], abs(d['prom'] - 50)))
for d in sel[:30]:
    print(fila(d))
print(f'   ({len(sel)} en total)')

print()
print('== los 20 con mas ag8, cualquier reparto')
print(cab)
for d in sorted(datos, key=lambda d: (-d['min_aguantan'], d['max']))[:20]:
    print(fila(d))

print()
print('== empates teoricos (todos)')
print(cab)
for d in sorted([d for d in datos if d['teoria'] == 'tablas'], key=lambda d: (d['max'], abs(d['prom'] - 50))):
    print(fila(d))

print()
print('== referencias')
print(cab)
for d in datos:
    if (d['white'], d['black']) in {('12344', '11245'), ('13344', '12355'), ('03344', '12355'), ('12344', '02355'), ('12344', '01355')}:
        print(fila(d))
