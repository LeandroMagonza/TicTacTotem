#!/bin/bash
# Uso: bash refinar.sh <variante> <flags del solver...>
# Elige finalistas de apc_<v>.csv y corre ve4/ve2/ve6 + teoria a 20 plies + practica + invertido.
v=$1; shift; F="$*"
S="S:/unityProyects/TaTeTi con Esteroides/Simulacion/solver/bin/Release/net9.0/tateti-solver.exe"
cd "$(dirname "$0")"
python - "$v" <<'EOF'
import csv, sys
from collections import defaultdict
v = sys.argv[1]
porpar = defaultdict(list)
for r in csv.DictReader(open(f'apc_{v}.csv', newline='')):
    porpar[(r['white'], r['black'])].append((float(r['gana1']), int(r['aguantan']), int(r['ganan']) + int(r['tablas'])))
fin = []
for (w, b), fs in porpar.items():
    mx = max(g for g, _, _ in fs); prom = sum(g for g, _, _ in fs) / len(fs)
    ag = min(a for _, a, _ in fs); nop = min(n for _, _, n in fs)
    if mx <= 55 and 42 <= prom <= 58 and ag >= 8:
        var = min(len(set(w)), len(set(b)))
        fin.append((-nop, mx, w, b, var))
fin.sort()
# Primero los que conservan variedad (4 o 5 rangos distintos en cada lado), despues el resto.
con_var = [f for f in fin if f[4] >= 4][:24]
resto = [f for f in fin if f[4] < 4][:36 - len(con_var)]
elegidos = con_var + resto
open(f'fin_{v}.txt', 'w').write('\n'.join(f'{w} {b}' for _, _, w, b, _ in elegidos) + '\n')
print(v, 'finalistas:', len(elegidos), 'de', len(fin), '(con variedad:', len(con_var), ')')
EOF
"$S" aperturas --pairs-file fin_$v.txt --games 1000 --ve 4 --horizonte 8 $F --out fin${v}_ve4.csv > fin$v.log 2>&1
"$S" aperturas --pairs-file fin_$v.txt --games 500 --ve 2 --horizonte 8 $F --out fin${v}_ve2.csv >> fin$v.log 2>&1
"$S" aperturas --pairs-file fin_$v.txt --games 300 --ve 6 --horizonte 8 $F --out fin${v}_ve6.csv >> fin$v.log 2>&1
: > fin${v}_solve.txt
while read w b; do
  echo -n "$w $b  " >> fin${v}_solve.txt
  "$S" solve --white $w --black $b --max-depth 20 --budget-ms 30000 --tt-bits 22 $F 2>&1 | grep VEREDICTO | head -1 >> fin${v}_solve.txt
  echo -n "$w $b  invertido: " >> fin${v}_inv.txt
  "$S" solve --white $b --black $w --max-depth 20 --budget-ms 20000 --tt-bits 22 $F 2>&1 | grep VEREDICTO | head -1 >> fin${v}_inv.txt
done < <(tr -d '\r' < fin_$v.txt)
echo fin > refin_${v}_done.txt
