#!/bin/bash
# Uso: bash refinar.sh <formato>        (lee cand_<formato>.txt; regla del centro siempre)
#
# Paso 1: `aperturas` a ve4 con 200 partidas sobre los candidatos (se saltea si ya esta).
# Paso 2: finalistas: mejor apertura del primero <= 56, promedio sobre aperturas 44-56, y la
#   fraccion de respuestas del segundo que aguantan 8 plies >= 0,3 en toda apertura (el set
#   vigente, 12344 vs 12355, da 10 de 28 = 0,36). Fraccion y no cantidad, porque la cantidad
#   de respuestas depende del tamano de los sets.
# Paso 3: ve2 / ve4 / ve6 con mas partidas, teoria a 20 plies y el par invertido.
fmt=$1
F="--sin-centro"
S="S:/unityProyects/TaTeTi con Esteroides/Simulacion/solver/bin/Release/net9.0/tateti-solver.exe"
cd "$(dirname "$0")"

if [ ! -f "apc_$fmt.done" ]; then
  "$S" aperturas --pairs-file "cand_$fmt.txt" --games 200 --ve 4 --horizonte 8 --max-depth 14 $F \
      --out "apc_$fmt.csv" > "apc_$fmt.log" 2>&1 && touch "apc_$fmt.done"
fi

python - "$fmt" <<'EOF'
import csv, sys, os, gzip
from collections import defaultdict
fmt = sys.argv[1]
porpar = defaultdict(list)
for r in csv.DictReader(open(f'apc_{fmt}.csv', newline='') if os.path.exists(f'apc_{fmt}.csv') else gzip.open(f'apc_{fmt}.csv.gz', 'rt', newline='')):
    porpar[(r['white'], r['black'])].append(
        (float(r['gana1']), int(r['aguantan']) / max(1, int(r['respuestas']))))
fin = []
for (w, b), fs in porpar.items():
    mx = max(g for g, _ in fs)
    prom = sum(g for g, _ in fs) / len(fs)
    frac = min(a for _, a in fs)
    if mx <= 56 and 44 <= prom <= 56 and frac >= 0.3:
        var = min(len(set(w)), len(set(b)))
        fin.append((abs(prom - 50) + max(0, mx - 54), w, b, var))
fin.sort()
con_var = [f for f in fin if f[3] >= 3][:24]
resto = [f for f in fin if f[3] < 3][:30 - len(con_var)]
elegidos = con_var + resto
open(f'fin_{fmt}.txt', 'w', newline='\n').write(''.join(f'{f[1]} {f[2]}\n' for f in elegidos))
print(fmt, 'pasan el filtro:', len(fin), ' finalistas:', len(elegidos), '(con 3 rangos o mas por lado:', len(con_var), ')')
EOF

"$S" aperturas --pairs-file "fin_$fmt.txt" --games 1000 --ve 4 --horizonte 8 --max-depth 14 $F --out "fin${fmt}_ve4.csv" > "fin$fmt.log" 2>&1
"$S" aperturas --pairs-file "fin_$fmt.txt" --games 800 --ve 2 --horizonte 8 --max-depth 14 $F --out "fin${fmt}_ve2.csv" >> "fin$fmt.log" 2>&1
"$S" aperturas --pairs-file "fin_$fmt.txt" --games 400 --ve 6 --horizonte 8 --max-depth 14 $F --out "fin${fmt}_ve6.csv" >> "fin$fmt.log" 2>&1
: > "fin${fmt}_solve.txt"
: > "fin${fmt}_inv.txt"
while read w b; do
  echo -n "$w $b  " >> "fin${fmt}_solve.txt"
  "$S" solve --white $w --black $b --max-depth 20 --budget-ms 30000 --tt-bits 22 $F 2>&1 | grep VEREDICTO | head -1 >> "fin${fmt}_solve.txt"
  echo -n "$w $b  invertido: " >> "fin${fmt}_inv.txt"
  "$S" solve --white $b --black $w --max-depth 20 --budget-ms 20000 --tt-bits 22 $F 2>&1 | grep VEREDICTO | head -1 >> "fin${fmt}_inv.txt"
done < <(tr -d '\r' < "fin_$fmt.txt")
touch "refin_$fmt.done"
