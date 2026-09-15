#!/bin/bash
# Barrido teorico de cada formato con la regla del centro, rangos 1 a 5 (los cinco animales).
# Mismo tope que el 5v5 de color/sw_sc: 14 plies. Retomable: saltea los formatos terminados.
#
# Uso: nohup bash barrer.sh > barrer.log 2>&1 &
S="S:/unityProyects/TaTeTi con Esteroides/Simulacion/solver/bin/Release/net9.0/tateti-solver.exe"
cd "$(dirname "$0")"
for fmt in 4v4 4v5 5v4 5v6 6v5 6v6; do
  w=${fmt%v*}
  b=${fmt#*v}
  if [ -f "sw_$fmt.done" ]; then continue; fi
  echo "$(date +%H:%M:%S) empieza $fmt"
  "$S" sweep --gen-white-size "$w" --gen-black-size "$b" --max-rank 5 --sum-window 0 \
      --max-depth 14 --budget-ms 20000 --tt-bits 20 --sin-centro \
      --out "sw_$fmt.csv" > "sw_$fmt.log" 2>&1 && touch "sw_$fmt.done"
  echo "$(date +%H:%M:%S) termina $fmt"
done
touch barrido.done
