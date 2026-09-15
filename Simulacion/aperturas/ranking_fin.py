"""Tabla final de los finalistas: ve4 (2000 partidas por apertura) y ve2 (1000)."""
import csv

def descorrer(s):
    return ''.join(str(int(c) - 1) for c in s)

def leer(resumen, corrido):
    out = {}
    for r in csv.DictReader(open(resumen, newline='')):
        w, b = (descorrer(r['white']), descorrer(r['black'])) if corrido else (r['white'], r['black'])
        ap = r['apertura_max']
        if corrido:
            pieza, cas = ap.split()
            ap = f'{int(pieza) - 1} {cas}'
        out[(w, b)] = dict(teoria=r['teoria'], max=float(r['max_gana1']), ap=ap,
                           prom=float(r['prom_gana1']), min_resp=int(r['min_ganan']))
    return out

ve4 = leer('fin_sin_ve4_resumen.csv', False) | leer('fin_con_ve4_resumen.csv', True)
ve2 = leer('fin_sin_ve2_resumen.csv', False) | leer('fin_con_ve2_resumen.csv', True)

print(f"{'primero':>6}    {'segundo':<6} {'teoria':<8} {'ve4 mejor':>9}  {'apertura':<10} {'ve4 prom':>8}  "
      f"{'ve2 mejor':>9} {'ve2 prom':>8}  {'r2':>2}  piedra/elef/leon")
for (w, b), d in sorted(ve4.items(), key=lambda kv: (kv[1]['max'], abs(kv[1]['prom'] - 50))):
    e = ve2.get((w, b), {})
    todo = w + b
    print(f"{w:>6} vs {b:<6} {d['teoria']:<8} {d['max']:9.1f}  {d['ap']:<10} {d['prom']:8.1f}  "
          f"{e.get('max', float('nan')):9.1f} {e.get('prom', float('nan')):8.1f}  {d['min_resp']:>2}  "
          f"{todo.count('0')}/{todo.count('1')}/{todo.count('2')}")
