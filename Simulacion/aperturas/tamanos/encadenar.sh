#!/bin/bash
# Encadena candidatos y refinado de cada formato apenas termina su barrido teorico.
# Retomable: saltea los formatos con refin_<fmt>.done.
# Uso: nohup bash encadenar.sh > encadenar.log 2>&1 &
cd "$(dirname "$0")"
for fmt in 5v5 4v4 4v5 5v4 5v6 6v5 6v6; do
  while [ ! -f "sw_$fmt.done" ] && [ ! -f barrido.done ]; do sleep 30; done
  if [ ! -f "sw_$fmt.done" ]; then echo "$(date +%H:%M:%S) falta el barrido de $fmt, se saltea"; continue; fi
  if [ -f "refin_$fmt.done" ]; then continue; fi
  echo "$(date +%H:%M:%S) candidatos $fmt"
  python candidatos.py "$fmt" >> candidatos.log 2>&1
  echo "$(date +%H:%M:%S) refinar $fmt"
  bash refinar.sh "$fmt" >> refinar.log 2>&1
  echo "$(date +%H:%M:%S) listo $fmt"
done
touch todo.done
