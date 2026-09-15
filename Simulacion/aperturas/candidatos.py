"""Arma la lista de candidatos para el barrido de aperturas.

Parte de los 338 viables (rangos 1-5) y aplica el filtro fisico: hasta dos
elefantes (rango 1) y hasta dos leones (rango 2) entre los dos sets. Ademas
genera las variantes con piedra: a cada viable con hasta tres elefantes se le
reemplaza un 1 por un 0 en uno u otro set, y se escribe con los rangos corridos
uno (0->1, 1->2, ... 5->6) que es como los entiende el solver.
"""
import sys

viables = [l.split() for l in open(sys.argv[1]) if l.strip() and not l.startswith('#')]

def cuenta(a, b, d):
    return (a + b).count(d)

sin_piedra = []
for a, b in viables:
    if cuenta(a, b, '1') <= 2 and cuenta(a, b, '2') <= 2:
        sin_piedra.append((a, b))

def corrido(s):
    return ''.join(str(int(c) + 1) for c in s)

con_piedra = set()
for a, b in viables:
    if cuenta(a, b, '1') > 3 or cuenta(a, b, '2') > 2:
        continue
    for lado in (0, 1):
        s = (a, b)[lado]
        if '1' not in s:
            continue
        nuevo = ''.join(sorted(s.replace('1', '0', 1)))
        par = (nuevo, b) if lado == 0 else (a, nuevo)
        if cuenta(par[0], par[1], '1') <= 2:
            con_piedra.add((corrido(par[0]), corrido(par[1])))

with open('candidatos_sin_piedra.txt', 'w') as f:
    for a, b in sin_piedra:
        f.write(f'{a} {b}\n')
with open('candidatos_con_piedra.txt', 'w') as f:
    for a, b in sorted(con_piedra):
        f.write(f'{a} {b}\n')
print(f'sin piedra: {len(sin_piedra)}   con piedra (rangos corridos): {len(con_piedra)}')
