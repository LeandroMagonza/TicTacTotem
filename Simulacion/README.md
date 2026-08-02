# Análisis de balance — TaTeTi con Esteroides

Documento de cierre. Contiene la configuración elegida, las alternativas descartadas y por
qué, todo lo que se descubrió en el camino, y cómo volver a correr cualquier medición.

---

## 1. La conclusión

**El que arranca lleva `1, 2, 3, 4, 4` — el otro lleva `1, 1, 2, 4, 5`.**

Cinco piezas por lado, cinco niveles de rango, diez piezas en total. El que arranca **no**
tiene la pieza más alta, igual que en el diseño original.

| | |
|---|---|
| Con juego perfecto | gana el **segundo**, en 12 plies |
| Si arranca el otro | gana `11245` en 7 plies — **no alternar quién empieza** |
| Aperturas viables | **12 de 12 (100 %)** — centro, esquina y lado, todas |
| Libertad efectiva | 33 % |
| Reparto real, ve2 | 54,5 % / 45,5 % |
| **Reparto real, ve4** | **51,7 % / 48,3 %** |
| Reparto real, ve6 | 38,8 % / 61,2 % |
| Duración típica | 10,2 turnos |
| Sumas | 14 contra 13 |
| Disimilitud de los sets | 0,57 |
| Moldes | 5 — nivel 1 ×3, nivel 2 ×2, nivel 3 ×1, nivel 4 ×3, nivel 5 ×1 |

**Contra el diseño original** (`122335` vs `12246`), a ve4 el reparto pasa de 62,6 / 37,4 a
51,7 / 48,3. Once puntos. Además: un molde menos, una pieza menos, sumas casi iguales entre
los dos lados, los dos jugadores con la misma cantidad de piezas, y todas las aperturas
jugables en vez de 5 de 12.

Tiene una propiedad que conviene entender porque es la que lo sostiene: **la ventaja teórica
y la práctica apuntan en direcciones opuestas y se cancelan.** El segundo jugador tiene la
victoria forzada, pero está a 12 plies y nadie la ve; el primero tiene el tempo, que sí se
siente. Es más robusto que buscar un empate exacto — que además es imposible.

---

## 2. Las otras opciones que quedaron sobre la mesa

| Configuración | Niv | ve4 | ve6 | A favor | En contra |
|---|---|---|---|---|---|
| **`12344` vs `11245`** | 5 | **51,7** | 38,8 | la elegida: equilibrio general | ve6 se va al segundo |
| `13334` vs `11245` | 5 | 50,1 | 35,8 | el ve4 más parejo, 39 % de libertad | triple de `3`, estéticamente pesado |
| `12334` vs `11245` | 5 | 48,4 | 33,4 | los dos sets son escaleras con un par | ve6 peor (33,4) |
| `12333` vs `11234` | **4** | 53,1 | **42,9** | el más estable entre niveles, 4 moldes | pierde el quinto nivel |
| `13444` vs `22345` | 5 | 51,0 | 40,2 | disimilitud 0,75, la más asimétrica | triple de `4` |
| `122335` vs `12246` | 6 | 62,6 | 58,8 | el diseño actual | muy desbalanceado a todo nivel |

Descartadas por razones que vale la pena recordar:

- **`113444` vs `12246`** — 23 plies de profundidad, el doble que cualquier otra. Pero **una
  sola apertura viable de nueve**: es un acertijo con solución única, no una partida.
- **`2224` vs `11126`** — empate real con juego perfecto (aguanta 39 plies sin resolverse).
  Pero formato 4v5 y sólo 33 % de aperturas viables.
- **`133344` vs `11125`** — 89 % de aperturas y 72 % de libertad, parecía lo mejor de todo.
  En la práctica reparte 84 / 16. La libertad alta significa que el que va ganando no puede
  errarle, que es exactamente lo contrario de un juego parejo.

---

## 3. Lo que descubrimos

### Sobre las reglas del juego

**Sólo importan los rangos relativos, no sus valores.** Cualquier renombre que preserve el
orden da un juego idéntico. `2224` vs `11126` es *exactamente* `2223` vs `11124`, verificado
al ply. Consecuencia práctica: la cantidad de moldes a producir es la cantidad de niveles
*distintos que aparecen en la partida*, no el rango máximo.

**Con el mismo set de los dos lados gana siempre el que arranca**, en 9 plies (196 de 252
sets), 10 (los 6 de un solo rango) u 11 (los 50 restantes). No hay un solo mirror match
empatado. El juego no tiene forma de terminar en tablas.

**El tapado es lo que impide que el tablero se trabe.** Colocar desde la mano exige casilla
vacía, así que con nueve casillas sólo entran nueve piezas por colocación. Con diez piezas en
juego alguien se queda con una en la mano y el tablero lleno, y ahí:

- *sin tapado posible* (todas las piezas del mismo rango) no hay ningún movimiento legal —
  todos los destinos están ocupados por piezas de rango igual — así que el que le toca queda
  ahogado y pierde. Es lo que pasa con `11111` contra `11111`, que gana el primero en 10 plies
  sin hacer una sola línea;
- *con tapado* siempre podés mover una pieza tuya encima de una más baja del rival, así que
  hay jugada y la partida sigue.

Por eso el tapado no es sólo lo que da profundidad: es lo que evita que el juego se decida por
bloqueo en vez de por línea.

**Monotonía**: agregar una pieza a un set nunca empeora su resultado. Verificado en 3.456
casos sin una sola excepción. Es lo que uno espera por robo de estrategia — con una pieza de
más siempre podés jugar la estrategia del set chico e ignorarla.

### Sobre qué hace fuerte a un set

**La pieza más alta es un acantilado, no una pendiente.**

| Tu pieza más alta contra la del rival | Gana el que arranca |
|---|---|
| Una o más arriba | **100 %** (sin excepciones en 40.116 cruces) |
| Empatan | 73–86 % |
| Una abajo | 16–25 % |
| Dos o más abajo | menos de 12 % |

No existe punto intermedio: el rango es un entero. **El único lugar habitable del diseño es
"una abajo"**, que es donde el juego original ya estaba.

**La suma total de las piezas es el peor predictor de fuerza que existe** — peor que mirar
sólo tu pieza más alta. Dos sets con la misma suma pueden separarse 81 puntos de victoria. El
modelo de puntos del ajedrez no funciona acá.

**Lo que sí predice: tus tres piezas más altas** (0,5 puntos de dispersión dentro del grupo),
*siempre que compares sets del mismo tamaño*. Y **la cantidad de piezas satura alrededor de
cinco**: con las mismas tres altas, 5, 6 y 7 piezas dan resultados idénticos. En el diseño
original la sexta pieza no hacía nada.

### Sobre el formato

Cuatro barridos exhaustivos, 328.104 enfrentamientos:

| Formato | Gana el que arranca |
|---|---|
| 4 vs 5 piezas | 52,0 % |
| 5 vs 6 | 59,8 % |
| 5 vs 5 | 66,0 % |
| 6 vs 5 | 71,7 % |

Menos piezas en total = más parejo. La razón conecta con todo lo anterior: once piezas en
nueve casillas **obligan** a apilar, y apilar es lo que le da la ventaja al primero. Forzar el
overlap amplifica el desbalance que se quería eliminar.

### Sobre las métricas mismas

**Profundidad y libertad se pelean.** Las configuraciones de 23–25 plies tienen 11 % de
aperturas viables; las de 11 plies llegan al 92 %. Duran más porque hay menos para decidir.

**Y la libertad alta no es buena por sí sola.** Mide cuántas jugadas conservan la victoria
*para el que va ganando*: si son muchas, el ganador no puede errarle y gana siempre. La
correlación con el desbalance práctico es fuerte y directa.

**La única métrica que responde "¿es parejo?" es el reparto real con jugadores de visión
limitada.** Todo lo demás son insumos.

---

## 4. Las métricas, qué miden y qué no

| Métrica | Qué es | Límite |
|---|---|---|
| **plies** | turnos hasta la victoria forzada. Un ply es medio movimiento: el turno de un jugador | es el mínimo con juego perfecto de los dos lados |
| **régimen** | si gana el que arranca sea cual sea el set (sets parejos) o si un set gana siempre | — |
| **aperturas** | de las primeras jugadas distintas, cuántas conservan el resultado | sólo el ply 1 |
| **libertad efectiva** | entre las jugadas que no pierden en 2 plies, cuántas conservan lo mejor, promediado en 8 turnos | medida sólo sobre la línea óptima; para el que pierde, "lo mejor" es una definición inventada (demorar la derrota), y un humano en desventaja busca complicar, no demorar |
| **ve2 / ve4 / ve6** | reparto real con jugadores que calculan 2, 4 o 6 plies y eligen al azar entre lo que les parece igual | el modelo no tiene intuición posicional ni aprende. **Los valores impares (ve3, ve5) son un artefacto** y hay que ignorarlos: con visión impar el jugador termina su cálculo en su propia jugada sin ver la respuesta, lo que le infla el resultado |
| **fuerza vs el pool** | contra cuántos de *todos* los sets posibles gana ese set | dice si el equilibrio de un par es robusto o casual |
| **disimilitud** | 1 = los sets no comparten ninguna pieza, 0 = idénticos | estética, no juego |

**ve4 es el número que importa** para un juego de mesa: son 4 medios turnos, o sea dos jugadas
propias. Es lo que calcula una persona que no le va a dedicar la vida al juego.

---

## 5. La regla del ahogado

Con `12344` vs `11245` aparecen **posiciones sin jugada legal**: un jugador con todas sus
piezas en el tablero, todas tapadas o bloqueadas, sin nada que mover ni colocar.

**Regla adoptada: el que no tiene jugada legal pierde.** Está implementada en el solver
(`Search.cs`, `Verify.cs` y la simulación de partidas). Falta implementarla en
`GameManager.cs`, que hoy no contempla el caso.

Son raras — 2 posiciones en 1,4 millones de nodos para la configuración elegida — y el impacto
está medido: se rehizo el barrido 5v5 completo con la regla nueva y se comparó celda por celda
contra el anterior.

| | |
|---|---|
| Cruces comparados | 63.504 |
| Cambia el ganador | **6 (0,01 %)** |
| Cambia sólo la profundidad | 15 (0,02 %) |
| Idénticos | 63.483 (99,97 %) |

Los 6 que cambian son **exactamente** los mirror match de un solo rango (`11111` contra
`11111` y sus cinco hermanos): de empate en 17 plies a victoria del primero en 10, por ahogado.
Ninguno de los cambios involucra sets con cuatro o más niveles distintos, y el reparto global
del formato no se mueve (66,0 / 33,9 antes y después).

Los archivos `balance_full.csv`, `balance_espejo.csv` y `balance_4v5.csv` se calcularon con la
regla anterior. Por lo medido en el 5v5, la diferencia se concentra en los sets planos de rango
bajo y no afecta ninguna conclusión — pero si se necesita exactitud en esa esquina del espacio,
hay que rehacerlos. `balance_5v5_ahogado.csv` ya está con la regla vigente.

Como efecto secundario, la monotonía queda **incondicional**. Con el ahogado contado como
tablas había una excepción teórica: una pieza de más podía sacarte de un ahogado y obligarte a
mover hacia una derrota. Contándolo como derrota, tener más piezas sólo puede ayudar.

---

## 6. Cómo correr todo

```bash
cd solver
dotnet build -c Release
```

Después, desde `solver/`:

```bash
# Verificar que el motor reproduce las reglas de Cell.cs / GameManager.cs (16 tests)
dotnet run -c Release -- selftest

# Contrastar el buscador optimizado contra un minimax pelado sin poda ni tabla
dotnet run -c Release -- verify --white 12344 --black 11245 --max-depth 7

# Resolver un enfrentamiento y ver la línea principal
dotnet run -c Release -- solve --white 12344 --black 11245 --max-depth 25 --pv

# Evaluar cada jugada inicial por separado
dotnet run -c Release -- openings --white 12344 --black 11245 --max-depth 21

# Libertad turno por turno (--detalle muestra a qué lleva cada jugada disponible)
dotnet run -c Release -- libertad --white 12344 --black 11245 --plies 8 --detalle

# Reparto real con jugadores de visión limitada
dotnet run -c Release -- practica --white 12344 --black 11245 --games 12000 --depths 2,4,6

# Barrer combinaciones y escribir un CSV
dotnet run -c Release -- sweep --gen-white-size 5 --gen-black-size 5 --max-rank 6 \
    --max-depth 17 --out ../balance_5v5.csv
```

Los sets se escriben como dígitos: `12344` son las piezas 1, 2, 3, 4 y 4. **El primer set
siempre arranca**, así que para probar los dos sentidos hay que correrlo dos veces invirtiendo.

Scripts de análisis, desde `Simulacion/`:

| Script | Qué hace |
|---|---|
| `tabla.py` | tabla consolidada de candidatos con todas las métricas |
| `analizar.py` | distribución de resultados y profundidades de un barrido |
| `valor_piezas.py` | matriz de sustitución, aditividad y transitividad |
| `ambos_sentidos.py` | cruza dos barridos: régimen turno vs piezas, simulación del match a 3 |
| `variantes.py` | compara familias de diseño con muestreo |
| `monotonia.py` | test de monotonía |
| `datos_web.py` | vuelca los números a JSON |

---

## 7. Cómo leer los CSV

Todos los barridos tienen las mismas ocho columnas:

```
white,black,white_sum,black_sum,verdict,plies,nodes,ms
12344,11245,14,13,black,12,1398701,412
```

| Columna | Significado |
|---|---|
| `white` | el set que **arranca** |
| `black` | el set que juega segundo |
| `white_sum` / `black_sum` | suma de los rangos de cada set |
| `verdict` | `white` = gana el que arranca · `black` = gana el segundo · `draw` = sin victoria forzada dentro del límite · `unknown` = se acabó el tiempo |
| `plies` | turnos hasta la victoria forzada, o el límite alcanzado |
| `nodes` | posiciones visitadas |
| `ms` | milisegundos |

**Cuidado con `draw`**: significa "no hay victoria forzada dentro de los plies que buscamos",
no que el empate esté demostrado. Varios cruces que figuran como `draw` a 15 plies resultaron
victorias forzadas a 23 o 25 cuando se los resolvió más profundo.

Archivos disponibles:

| Archivo | Contenido |
|---|---|
| `balance_full.csv` | 116.424 — todos los 6 piezas (arranca) contra todos los de 5 |
| `balance_espejo.csv` | 116.424 — lo mismo con el de 5 arrancando |
| `balance_5v5.csv` | 63.504 — todos los de 5 contra todos los de 5 |
| `balance_4v5.csv` | 31.752 — los de 4 arrancando contra los de 5 |
| `variante_*.csv` | familias de diseño, muestreadas |
| `profundos*.csv` | los cruces sin resolver, re-resueltos a 27 plies |
| `caja*.csv` | subconjuntos de un master, para la caja de dos versiones |
| `monotonia.csv` | el test de monotonía |

`simulacion.py`, `simulation_*.csv` y `old/` son de la simulación vieja. **Sus resultados no
sirven** — el famoso 68 % / 32 % salía de un modelo que no tenía pilas, no tenía la regla del
medio punto, tenía la detección de amenazas rota y jugaba con una IA codiciosa y aleatoria. Se
dejan sólo como registro.

---

## 8. Qué falta

**Probarlo con personas.** Todo el reparto real sale de un modelo de jugador que ve N plies
exactos y elige al azar entre lo que le parece igual. No arma trampas, no tiene intuición
posicional, no aprende. Diez partidas entre dos personas van a decir más sobre el
comportamiento a ve6 que seis mil de la simulación.

Ya hay con qué: **[`../web/`](../web/) es el juego jugable**, en Three.js, contra la máquina en
cinco niveles o dos personas en la misma pantalla. Se comparte con un link y anda en el
celular, que era la traba para probarlo con gente.

Ese juego **implementa la regla del ahogado**, así que el pendiente que decía esta sección ya
no aplica a la versión que se va a jugar. `GameManager.cs` (la versión Unity con Photon) sigue
sin tenerla, pero queda superada por la web.

El motor de la web es un port de `solver/` a JavaScript, contrastado contra este mismo solver:
los conteos del minimax pelado coinciden **exactos** hasta 5 plies (37 · 1.189 · 30.205 ·
673.373 · 11.367.485) y el alpha-beta hasta 8, con menos de 0,05 % de diferencia de 9 a 12 por
el tamaño de tabla. Y las tasas de victoria de la IA reproducen las de `practica` dentro del
intervalo de confianza, que es lo que prueba de punta a punta que el juego que se juega es el
que se analizó acá.

**Si al probarlo se siente inclinado hacia el segundo jugador**, el ajuste ya está medido:
`12333` vs `11234` le devuelve unos dos puntos al primero y baja a cuatro moldes, a costa del
quinto nivel.
