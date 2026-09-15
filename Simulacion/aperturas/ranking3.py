"""Que pasa si se restringe la apertura del primero por regla.

Para cada set y cada regla (todas las aperturas / sin centro / solo lados)
calcula sobre las aperturas permitidas: la mejor para el primero, el promedio,
y el minimo de respuestas del segundo que ganan y que aguantan 8 plies.
"""
import csv, sys
from collections import defaultdict

REGLAS = {'todas': {'centro', 'esquina', 'lado'}, 'sin centro': {'esquina', 'lado'}, 'solo lado': {'lado'}}
regla = sys.argv[1] if len(sys.argv) > 1 else 'solo lado'
permitidas = REGLAS[regla]

def descorrer(s):
    return ''.join(str(int(c) - 1) for c in s)

filas = defaultdict(list)
for f, corrido in (('ap2_sin.csv', False), ('ap2_con.csv', True), ('ap2_emp.csv', False), ('ap2_ref.csv', False)):
    try:
        for r in csv.DictReader(open(f, newline='')):
            w, b = (descorrer(r['white']), descorrer(r['black'])) if corrido else (r['white'], r['black'])
            pieza = int(r['pieza']) - (1 if corrido else 0)
            filas[(w, b)].append(dict(ap=f"{pieza} {r['casilla']}", cas=r['casilla'], ganan=int(r['ganan']),
                                      tablas=int(r['tablas']), aguantan=int(r['aguantan']), g1=float(r['gana1'])))
    except FileNotFoundError:
        print(f'(falta {f})')

datos = []
for (w, b), fs in filas.items():
    teoria = 'primero' if any(f['ganan'] == 0 and f['tablas'] == 0 for f in fs) \
             else 'tablas' if any(f['ganan'] == 0 for f in fs) else 'segundo'
    ok = [f for f in fs if f['cas'] in permitidas]
    ok.sort(key=lambda f: -f['g1'])
    todo = w + b
    datos.append(dict(white=w, black=b, teoria=teoria, max=ok[0]['g1'], ap=ok[0]['ap'],
                      prom=sum(f['g1'] for f in ok) / len(ok),
                      min_ganan=min(f['ganan'] for f in ok), min_ag=min(f['aguantan'] for f in ok),
                      piedra=todo.count('0'), elef=todo.count('1'), leon=todo.count('2')))

cab = (f"{'primero':>6}    {'segundo':<6} {'teoria':<8} {'mejor':>5}  {'apertura':<10} {'prom':>5}  "
       f"{'gan':>3} {'ag8':>3}   piedra/elef/leon")
def fila(d):
    return (f"{d['white']:>6} vs {d['black']:<6} {d['teoria']:<8} {d['max']:5.1f}  {d['ap']:<10} {d['prom']:5.1f}  "
            f"{d['min_ganan']:>3} {d['min_ag']:>3}   {d['piedra']}/{d['elef']}/{d['leon']}")

print(f'Regla: apertura {regla}. {len(datos)} enfrentamientos.\n')
print('== promedio 42-58, ordenados por mejor apertura del primero; luego por ag8')
print(cab)
sel = sorted([d for d in datos if 42 <= d['prom'] <= 58], key=lambda d: (d['max'], -d['min_ag'], abs(d['prom'] - 50)))
for d in sel[:25]:
    print(fila(d))
print()
print('== ag8 >= 10 y promedio 42-58, ordenados por mejor apertura')
print(cab)
for d in sorted([d for d in sel if d['min_ag'] >= 10], key=lambda d: (d['max'], abs(d['prom'] - 50)))[:20]:
    print(fila(d))
print()
print('== referencias')
print(cab)
for d in datos:
    if (d['white'], d['black']) in {('12344', '11245'), ('12344', '12245'), ('12344', '12345'), ('13344', '12355'),
                                    ('03344', '12355'), ('12344', '02355'), ('22334', '01345'), ('11334', '02245')}:
        print(fila(d))
