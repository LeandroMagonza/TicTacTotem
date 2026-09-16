#!/bin/bash
# Mira como vienen todas las corridas largas, de este proyecto y de cualquier otro.
#
# El trato es simple: cualquier proceso largo va escribiendo lineas de progreso a un
# archivo que termine en .prog . Este script muestra la ULTIMA linea de cada uno, con
# cuanto hace que no se actualiza -- que es como se distingue "va lento" de "se colgo".
#
#   uso:  estado.sh [carpeta ...]        una foto
#         estado.sh -s [carpeta ...]     se refresca solo cada 5 segundos
#
# Para el solver del rey, las tandas se lanzan asi:
#   rey.exe practica ... > resultado.txt 2> resultado.prog
# porque el progreso sale por la salida de error y los resultados por la normal.

refresca=0
if [ "$1" = "-s" ]; then refresca=1; shift; fi
carpetas=("$@")
[ ${#carpetas[@]} -eq 0 ] && carpetas=(".")

foto () {
  local ahora hay
  ahora=$(date +%s)
  hay=0
  printf '%s\n' "  ---- $(date '+%H:%M:%S') ----------------------------------------"
  for dir in "${carpetas[@]}"; do
    while IFS= read -r f; do
      [ -f "$f" ] || continue
      hay=1
      local mod edad ultima nombre
      mod=$(stat -c %Y "$f" 2>/dev/null || echo "$ahora")
      edad=$(( ahora - mod ))
      # En Windows el mtime puede venir un segundo adelantado; una edad negativa
      # es ruido del reloj, no informacion.
      [ "$edad" -lt 0 ] && edad=0
      ultima=$(tail -n 1 "$f" 2>/dev/null)
      nombre=$(basename "$f" .prog)
      # Mas de dos minutos sin escribir con este ritmo de reporte es sospechoso.
      if [ "$edad" -gt 120 ]; then
        printf '  %-22s %-52s  QUIETO hace %ss\n' "$nombre" "${ultima:0:52}" "$edad"
      else
        printf '  %-22s %-52s  hace %ss\n' "$nombre" "${ultima:0:52}" "$edad"
      fi
    done < <(find "$dir" -maxdepth 2 -name '*.prog' -newermt '-12 hours' 2>/dev/null | sort)
  done
  if [ "$hay" -eq 0 ]; then
    echo "  (no hay ningun .prog reciente en: ${carpetas[*]})"
  fi
  return 0
}

if [ "$refresca" -eq 1 ]; then
  while true; do clear; foto; sleep 5; done
else
  foto
fi
