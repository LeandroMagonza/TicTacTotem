#!/bin/bash
# Uso: bash refinar.sh <variante> <flags del solver...>
#
# Paso 1: `aperturas` a ve4 sobre cand_<v>.txt.
# Paso 2: finalistas. Con la regla de orden el primero tiene 2 o 3 aperturas y el
#   segundo pocas respuestas, asi que "aguantan 8 plies" se pide como FRACCION de
#   las respuestas, no como cantidad fija como en color/.
# Paso 3: ve2 / ve4 / ve6 con mas partidas, teoria a 20 plies y el par invertido.
v=$1; shift; F="$*"
S="S:/unityProyects/TaTeTi con Esteroides/Simulacion/solver/bin/Release/net9.0/tateti-solver.exe"
cd "$(dirname "$0")"

"$S" aperturas --pairs-file cand_$v.txt --games 300 --ve 4 --horizonte 8 $F --out apc_$v.csv > apc_$v.log 2>&1

python - "$v" <<'EOF'
import csv, sys
from collections import defaultdict
v = sys.argv[1]
porpar = defaultdict(list)
for r in csv.DictReader(open(f'apc_{v}.csv', newline='')):
    porpar[(r['white'], r['black'])].append(
        (float(r['gana1']), int(r['aguantan']) / max(1, int(r['respuestas'])), int(r['ganan']) + int(r['tablas'])))
fin = []
for (w, b), fs in porpar.items():
    mx = max(g for g, _, _ in fs)
    prom = sum(g for g, _, _ in fs) / len(fs)
    frac = min(a for _, a, _ in fs)
    nop = min(n for _, _, n in fs)
    if mx <= 58 and 42 <= prom <= 58 and frac >= 0.5:
        var = min(len(set(w)), len(set(b)))
        fin.append((abs(prom - 50) + max(0, mx - 55), -frac, w, b, var, mx, prom))
fin.sort()
con_var = [f for f in fin if f[4] >= 4][:24]
resto = [f for f in fin if f[4] < 4][:36 - len(con_var)]
elegidos = con_var + resto
open(f'fin_{v}.txt', 'w', newline='\n').write(''.join(f'{f[2]} {f[3]}\n' for f in elegidos))
print(v, 'finalistas:', len(elegidos), 'de', len(fin), 'que pasan el filtro (con variedad:', len(con_var), ')')
EOF

"$S" aperturas --pairs-file fin_$v.txt --games 1500 --ve 4 --horizonte 8 $F --out fin${v}_ve4.csv > fin$v.log 2>&1
"$S" aperturas --pairs-file fin_$v.txt --games 1000 --ve 2 --horizonte 8 $F --out fin${v}_ve2.csv >> fin$v.log 2>&1
"$S" aperturas --pairs-file fin_$v.txt --games 600 --ve 6 --horizonte 8 $F --out fin${v}_ve6.csv >> fin$v.log 2>&1
: > fin${v}_solve.txt
: > fin${v}_inv.txt
while read w b; do
  echo -n "$w $b  " >> fin${v}_solve.txt
  "$S" solve --white $w --black $b --max-depth 20 --budget-ms 30000 --tt-bits 22 $F 2>&1 | grep VEREDICTO | head -1 >> fin${v}_solve.txt
  echo -n "$w $b  invertido: " >> fin${v}_inv.txt
  "$S" solve --white $b --black $w --max-depth 20 --budget-ms 20000 --tt-bits 22 $F 2>&1 | grep VEREDICTO | head -1 >> fin${v}_inv.txt
done < <(tr -d '\r' < fin_$v.txt)
echo fin > refin_${v}_done.txt
