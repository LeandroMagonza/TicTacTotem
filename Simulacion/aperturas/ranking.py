"""Ranking de sets segun el criterio de aperturas.

Lee los resumenes de `aperturas` (con y sin piedra), vuelve a la notacion con
piedra (0 = piedra) y aplica:
  1. mejor apertura del primero a ve4 <= MAX_APERTURA
  2. promedio a ve4 entre 45 y 55
  3. respuestas ganadoras del segundo tras cada apertura >= MIN_RESP,
     o tablas con juego perfecto
mas el inventario fisico (elefantes = rango 1, leones = rango 2).
"""
import csv, sys

MAX_APERTURA = float(sys.argv[1]) if len(sys.argv) > 1 else 55.0
MIN_RESP = int(sys.argv[2]) if len(sys.argv) > 2 else 6

def descorrer(s):
    return ''.join(str(int(c) - 1) for c in s)

def leer(resumen, filas, corrido):
    out = []
    # aperturas que conservan la victoria del primero, por par (para teoria=primero)
    ganadoras = {}
    with open(filas, newline='') as f:
        for r in csv.DictReader(f):
            k = (r['white'], r['black'])
            if int(r['ganan']) == 0 and int(r['tablas']) == 0:
                ganadoras[k] = ganadoras.get(k, 0) + 1
    with open(resumen, newline='') as f:
        for r in csv.DictReader(f):
            k = (r['white'], r['black'])
            w, b = (descorrer(r['white']), descorrer(r['black'])) if corrido else (r['white'], r['black'])
            ap = r['apertura_max']
            if corrido:
                pieza, cas = ap.split()
                ap = f'{int(pieza) - 1} {cas}'
            todo = w + b
            out.append(dict(
                white=w, black=b, teoria=r['teoria'],
                max=float(r['max_gana1']), apertura=ap, prom=float(r['prom_gana1']),
                min_resp=int(r['min_ganan']), ap_ganadoras=ganadoras.get(k, 0),
                piedra=todo.count('0'), elef=todo.count('1'), leon=todo.count('2')))
    return out

datos = leer('ap_sin_piedra_resumen.csv', 'ap_sin_piedra.csv', False)
try:
    datos += leer('ap_con_piedra_resumen.csv', 'ap_con_piedra.csv', True)
except FileNotFoundError:
    print('(todavia no esta el barrido con piedra)')

def pasa(d):
    if d['max'] > MAX_APERTURA or not (45 <= d['prom'] <= 55):
        return False
    if d['teoria'] == 'tablas':
        return True
    if d['teoria'] == 'segundo':
        return d['min_resp'] >= MIN_RESP
    return d['ap_ganadoras'] >= 3   # el primero gana: que no dependa de una sola apertura

def fila(d):
    return (f"{d['white']:>6} vs {d['black']:<6} {d['teoria']:<8} {d['max']:5.1f}  {d['apertura']:<10} "
            f"{d['prom']:5.1f}  {d['min_resp']:>3}  {d['ap_ganadoras']:>2}   {d['piedra']}/{d['elef']}/{d['leon']}")

cab = f"{'primero':>6}    {'segundo':<6} {'teoria':<8} {'mejor':>5}  {'apertura':<10} {'prom':>5}  {'r2':>3}  {'a1':>2}   piedra/elef/leon"
print(f'{len(datos)} enfrentamientos medidos. Criterio: mejor apertura <= {MAX_APERTURA}, promedio 45-55, '
      f'respuestas del 2o >= {MIN_RESP} (o el 1o con >= 3 aperturas ganadoras).\n')
ok = sorted([d for d in datos if pasa(d)], key=lambda d: (d['max'], abs(d['prom'] - 50)))
print(f'== Pasan el criterio: {len(ok)}')
print(cab)
for d in ok[:40]:
    print(fila(d))

print()
print('== Los 15 con la mejor apertura mas baja, pasen o no (para ver que tan lejos esta lo mejor)')
print(cab)
for d in sorted(datos, key=lambda d: (d['max'], abs(d['prom'] - 50)))[:15]:
    print(fila(d))

print()
print('== Promedio entre 45 y 55, ordenados por la mejor apertura del primero (sin mirar respuestas)')
print(cab)
for d in sorted([d for d in datos if 45 <= d['prom'] <= 55], key=lambda d: (d['max'], abs(d['prom'] - 50)))[:25]:
    print(fila(d))

print()
print(f'== Sin jugada secreta (respuestas del 2o >= {MIN_RESP}, o tablas, o el 1o con >= 3 aperturas), ordenados por promedio cerca de 50')
print(cab)
robustos = [d for d in datos if d['teoria'] == 'tablas' or (d['teoria'] == 'segundo' and d['min_resp'] >= MIN_RESP)
            or (d['teoria'] == 'primero' and d['ap_ganadoras'] >= 3)]
for d in sorted(robustos, key=lambda d: (abs(d['prom'] - 50), d['max']))[:25]:
    print(fila(d))
print(f'   ({len(robustos)} en total; teoria: ' + ', '.join(f"{t} {sum(1 for d in robustos if d['teoria'] == t)}" for t in ('segundo', 'tablas', 'primero')) + ')')

print()
print('== Referencias')
print(cab)
for d in datos:
    if (d['white'], d['black']) in {('12344', '11245'), ('12344', '12245'), ('12344', '01345'), ('12344', '01355'), ('12344', '01245')}:
        print(fila(d))
