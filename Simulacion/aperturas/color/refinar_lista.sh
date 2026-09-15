#!/bin/bash
# Uso: bash refinar.sh <variante> <flags del solver...>
# Elige finalistas de apc_<v>.csv y corre ve4/ve2/ve6 + teoria a 20 plies + practica + invertido.
v=$1; shift; F="$*"
S="S:/unityProyects/TaTeTi con Esteroides/Simulacion/solver/bin/Release/net9.0/tateti-solver.exe"
cd "$(dirname "$0")"
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
