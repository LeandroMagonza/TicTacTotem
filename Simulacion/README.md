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
| Duración típica | 10,2 plies (unas 5 jugadas de cada uno) |
| Sumas | 14 contra 13 |
| Disimilitud de los sets | 0,57 |
| Moldes | 5 — nivel 1 ×3, nivel 2 ×2, nivel 3 ×1, nivel 4 ×3, nivel 5 ×1 |

**Contra el diseño original** (`122335` vs `12246`), a ve4 el reparto pasa de 62,6 / 37,4 a
51,7 / 48,3. Once puntos. Además: un molde menos, una pieza menos, sumas casi iguales entre
los dos lados, los dos jugadores con la misma cantidad de piezas, y todas las aperturas
jugables en vez de 5 de 12.

Esta conclusión es la del juego **con tablero**, que es para el que se la eligió, y se sostiene:
de las 338 configuraciones viables que se barrieron después (sección 6), sólo 10 le ganan acá.
Si el juego además se va a jugar **sin tablero**, la recomendación cambia a `11344` contra
`12245` — el mismo diseño con una pieza intercambiada entre los dos jugadores, que sirve para
las dos reglas. Está en la sección 6.

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
| **forzados / trampas / riesgo** | sobre partidas jugadas, no sobre la línea óptima: en cuántos turnos hay una sola jugada que conserva lo mejor (*forzados*), en cuántos ésa además es indistinguible de una que pierde para el que ve 4 plies (*trampas*), y la probabilidad de errarle en un turno (*riesgo*). Comando `trampas` | depende de a qué profundidad se defina "la verdad"; se usa 8 plies. Los turnos en que el jugador ya está perdido se excluyen, así que hay que mirarlos junto con la columna *ya perdido* |

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

## 6. La variante sin tablero

Idea: que el 3x3 no esté dibujado de antemano. La primera pieza se pone en cualquier lado, la
segunda se pone *a una distancia relativa* de la primera, y entre las dos empiezan a delimitar
el tablero. Si la segunda cae en la columna de al lado, quedan dos columnas fijas y la tercera
puede terminar de cualquiera de los dos lados. Si cae en diagonal salteando una casilla, el
3x3 ya quedó definido entero. Y si en el medio de la partida las piezas se juntan y la caja se
achica, el 3x3 se suelta y puede rearmarse en otra disposición.

Todo eso es una sola regla: **lo que está puesto tiene que entrar en algún 3x3.** O sea que la
caja que envuelve a las casillas ocupadas nunca puede medir más de 3 de alto ni más de 3 de
ancho, y se recalcula después de cada jugada. Nada más cambia: colocar sigue exigiendo casilla
vacía, se sigue moviendo una casilla en ortogonal, se sigue tapando sólo con rango
estrictamente mayor, la línea de tres destapadas sigue ganando y el que acaba de mover sigue
perdiendo los empates.

### Qué le hace al juego

**Le da la ventaja al primero, siempre, y en una sola dirección.** Se rehízo el barrido completo
de 5 contra 5 con la regla nueva — los mismos 63.504 cruces de `balance_5v5_ahogado.csv`, en
`balance_5v5_libre.csv`:

| | con tablero | sin tablero |
|---|---|---|
| Gana el que arranca | 66,0 % | **78,7 %** |
| Gana el segundo | 33,9 % | 21,3 % |
| Sin victoria forzada | 52 cruces | **ninguno** |
| Plies promedio | 8,8 | 8,7 |

Lo contundente no es el 78,7 sino cómo se llega: **8.021 cruces (12,6 %) pasan de ser victoria
del segundo a ser victoria del primero, y ninguno pasa al revés.** Ni uno en 63.504. La regla
no reacomoda el balance, lo transfiere.

Y lo transfiere justo en la franja donde vive el diseño. El único lugar habitable era "el
segundo tiene la pieza más alta por uno" (sección 3):

| El segundo tiene la más alta por... | con tablero | sin tablero |
|---|---|---|
| **+1 de rango** (la franja del diseño) | gana el segundo en **76,7 %** | **43,5 %** |
| se empatan la más alta | 19,6 % | 8,3 % |

El acantilado del rango, en cambio, sigue intacto: `11111` arrancando contra `66666` pierde
igual, en 10 plies. Lo que desaparece es la discriminación fina entre sets parecidos.

### Por qué gana el primero

La línea principal de `11344` contra `12245`, con la mejor defensa de los dos lados. El `*`
marca la jugada que cae afuera del encuadre anterior, o sea la que corre el tablero:

```
WC1(0,0); BC2(-1,0)*; WC1(0,-1)*; BM2(0,1)-(1,1); WC3(2,2);
BC1(0,1); WC4(2,1); BC2(0,2); WC4(2,0)   -> gana BLANCO

ply 5  caja 3x3          ply 9  caja 3x3
  W1   .    .              W1   B1   B2
  .  W1/B2  .              .  W1/B2  .
  .    .    W3             W4   W4   W3
```

En el tablero fijo, la respuesta del segundo ocupa una casilla que al primero le servía: hay
nueve casillas y son de los dos. Acá no. Cuando el segundo pone su pieza **todavía no hay
casillas**, así que no le saca ninguna: sólo fija una distancia, y encima deja la tercera fila
y la tercera columna abiertas para cualquiera de los dos lados. Es una jugada que construye
pero no bloquea.

El primero, que coloca en los plies 1, 3, 5, 7 y 9 contra los 2, 4, 6 y 8 del segundo, ya venía
medio movimiento adelante. Sacarle al segundo su primer bloqueo es lo que desnivela. Se ve en
el ply 9: las negras arriba, mirando cómo se cierra la fila de abajo que cuando las pusieron
no existía.

**Definir el tablero no es lo mismo que ocupar el tablero.** Es la intuición que más cuesta,
porque parece que el que delimita manda.

### Cuánto se siente el tablero flotante

Sobre 6.000 partidas a ve4:

| | |
|---|---|
| El 3x3 queda definido en el ply | **3,7** en promedio |
| Plies jugados sin el 3x3 definido | 3,0 de 9,9 (30 %) |
| Partidas que terminan sin definirlo | 0 % |
| Partidas donde vuelve a soltarse | **16,0 %** (0,18 veces por partida) |

Es, en los hechos, **una regla de apertura**. Después de la segunda jugada de cada uno el
tablero ya está clavado y lo que queda es el juego de siempre. La parte más linda de la idea
—que el 3x3 se suelte en el medio y se rearme en otro lado— pasa en una partida de cada seis.

### La sub-variante de la pieza pegada

Si la pieza que se coloca tiene que **tocar** a alguna ya puesta, el juego cambia mucho, y de
manera distinta según qué se acepte como "tocar". Medido sobre `12344` vs `11245`:

| | ve2 | ve4 | ve6 |
|---|---|---|---|
| suelta (la variante de arriba) | 57,1 | 56,1 | 66,9 |
| pegada, vale la diagonal | 55,4 | 54,8 | 62,7 |
| **pegada, sólo ortogonal** | 47,9 | **41,5** | **31,4** |
| pegada ortogonal, toda la partida | 46,8 | 40,5 | 27,9 |

Con la diagonal permitida no cambia nada: la pieza del segundo en la diagonal inmediata sigue
dejando la geometría abierta. Obligada a tocar **por un lado**, sí clava una fila o una
columna, y ahí el sesgo se da vuelta al segundo — colocar vuelve a ser ocupar. Con juego
perfecto el primero igual gana, en 11 plies en vez de 9.

Dos cosas para tener en cuenta si se la quiere usar. La primera es que **contradice el ejemplo
que define la regla**: "el segundo pone en diagonal dejando un lugar en el medio" es un salto
de (2,2), que con pieza pegada es ilegal, así que desaparece la jugada que delimita el 3x3 de
una. La segunda es que exigirla toda la partida o sólo hasta que el 3x3 quede definido **casi
no cambia nada** (ve4 40,5 contra 41,5), porque el tablero se clava en el ply 3,7 y después ya
no hay sobre qué actuar. Conviene la segunda: no toca el ta-te-ti normal.

Se prende con `--pegado`, `--pegado-orto` y `--pegado-siempre`.

### Una configuración para las dos reglas

Si el juego se va a poder jugar con tablero y sin tablero, conviene un solo set de piezas que
sirva para los dos. Y como la regla suelta corre el reparto **una cantidad casi fija** —mediana
de +3,8 puntos al primero al ve4, y sólo 3 de 181 configuraciones se mueven al revés— alcanza
con elegir una configuración que con tablero se incline al segundo lo suficiente.

Para buscarla en serio hubo que reconocer un hueco del análisis anterior (ver sección 9): los
barridos exhaustivos miden el veredicto con juego perfecto, pero el número que decide es el
reparto real, y ése sólo se había corrido sobre unas 18 configuraciones elegidas a mano. Se
barrió entonces el espacio filtrado por los criterios del propio diseño:

| criterio | quedan |
|---|---|
| todos los 5v5 con rangos 1-5 | 15.876 |
| + el que arranca no tiene la pieza más alta | 3.055 |
| + se usan los cinco niveles (cinco moldes) | 1.095 |
| + cada set con al menos 3 piezas distintas | 770 |
| + sin triples | **338** |

Las 338 se midieron con ve2 y ve4 en las dos reglas (`viables_ve2.csv`, `viables_ve4.csv`), y
las mejores con ve6 y 12.000 partidas por número.

Lo primero que salió de ahí es una tranquilidad: **de las 338, sólo 10 le ganan a la
configuración de la sección 1 en el juego con tablero al ve4.** La búsqueda hecha a mano cayó
en el 3 % superior, y su ve6 con tablero (38,8) es el más parejo de todos los finalistas. Para
el juego con tablero solo, esa elección no necesita cambiarse.

Lo segundo es que esa misma configuración es **la peor de los finalistas sin tablero**, que es
lo esperable: se la eligió mirando una sola de las dos reglas.

Buscando las que sirven para las dos, **sólo cinco se dan vuelta limpio en los dos niveles** —
al segundo con tablero, al primero sin tablero — y de ésas la mejor es:

> ### `11344` contra `12245`

Es **el diseño de la sección 1 con una pieza intercambiada**:

| | primero | segundo |
|---|---|---|
| sección 1 | `1,2,3,4,4` | `1,1,2,4,5` |
| para las dos reglas | `1,`**`1`**`,3,4,4` | `1,`**`2`**`,2,4,5` |

El primero le da su `2` al segundo y el segundo le da un `1` al primero. El reparto de moldes
queda **idéntico** —nivel 1 ×3, nivel 2 ×2, nivel 3 ×1, nivel 4 ×3, nivel 5 ×1—, así que se
fabrica exactamente lo mismo: cambia quién se lleva cuál.

| | con tablero | sin tablero |
|---|---|---|
| Con juego perfecto | gana el **segundo**, en 12 plies | gana el **primero**, en 9 plies |
| Si arranca `12245` | gana `12245` en 7 plies | gana `12245` en 7 plies |
| Aperturas distintas | 9 | 3 (una por pieza) |
| Aperturas viables | 9 de 9 (100 %) | 3 de 3 (100 %) |
| Libertad efectiva | 36 % | 43 % |
| Reparto ve2 | **49,2** / 50,8 | **52,7** / 47,3 |
| **Reparto ve4** | **47,5** / 52,5 | **51,0** / 49,0 |
| Reparto ve6 | 34,0 / 66,0 | 63,0 / 37,0 |
| Plies por partida | 10,2 | 9,9 |
| Sumas | 13 contra 14 |
| Disimilitud | 0,75 |

Los cuatro números de ve2 y ve4 quedan **todos dentro de 2,7 puntos de 50 y del lado correcto**:
con tablero manda el segundo, sin tablero manda el primero. Ninguna de las otras 337 lo logra.

Las que quedaron cerca, por si hace falta ajustar:

| | con tablero (ve2/4/6) | sin tablero (ve2/4/6) | |
|---|---|---|---|
| **`11344` vs `12245`** | 49,2 / 47,5 / 34,0 | 52,7 / 51,0 / 63,0 | **6 de 6 del lado correcto** |
| `11234` vs `12335` | 48,4 / 48,5 / 32,1 | 51,1 / 53,9 / 62,8 | 6 de 6 |
| `11224` vs `11235` | 47,5 / 46,3 / 34,5 | 51,0 / 52,4 / 64,8 | 6 de 6 |
| `13344` vs `12345` | 51,9 / 48,7 / 33,9 | 55,0 / 52,2 / 56,4 | 5 de 6 — la más linda: el segundo lleva la escalera entera |
| `12344` vs `11245` | 54,5 / 51,7 / 38,8 | 57,1 / 56,1 / 66,9 | 4 de 6 — la de la sección 1 |

Las dos últimas ordenan bien el compromiso. `13344` vs `12345` falla sólo en el ve2 con
tablero, donde el primero queda 1,9 puntos arriba en vez de abajo — en la mesa no se nota —, y
a cambio es la más linda de todas: el segundo lleva la escalera entera y las sumas quedan 15 y
15. La de la sección 1 falla en el ve2 y el ve4 con tablero, que es donde más importa, y es
además la más desbalanceada de las cinco sin tablero.

### La jugada única, que es lo que de verdad arruina una partida

Un juego puede dar 50/50 y ser malo igual: si en cada turno hay una sola jugada que no pierde,
y encima no es fácil de ver, se pierde sin enterarse. `libertad` no lo contesta porque recorre
sólo la línea óptima. El comando `trampas` juega partidas de verdad y en cada turno compara
**lo que el jugador ve a 4 plies contra lo que pasa a 8**:

- **forzado**: una sola jugada conserva lo mejor
- **trampa**: además es indistinguible de otra que pierde — el caso que importa
- **riesgo**: probabilidad de errarle ese turno
- **ya perdido**: turnos sin nada que salvar, que se excluyen del resto

`11344` contra `12245`, 800 partidas:

| | ya perdido | forzados | trampas | riesgo/turno |
|---|---|---|---|---|
| con tablero — arranca | 29,5 % | 15,2 % | 7,3 % | 32,6 % |
| con tablero — segundo | 24,1 % | 28,1 % | **17,3 %** | 38,3 % |
| sin tablero — arranca | 25,0 % | 13,4 % | 5,9 % | 25,4 % |
| sin tablero — segundo | **50,9 %** | 28,8 % | 10,9 % | 18,9 % |

Tres cosas:

**El problema existe y es del segundo.** Uno de cada seis turnos del segundo es una trampa,
contra uno de cada catorce del primero. No es culpa de la configuración —`12344` vs `11245` da
24,0 % contra 6,5 %, peor— sino la contracara de que el segundo tenga la victoria teórica: la
tiene, pero caminando por el filo.

**Sin tablero el juego es más indulgente**: las trampas bajan de 12,3 % a 7,8 % y el riesgo por
turno de 35,4 % a 23,0 %. Con el tablero flotando hay más lugares donde rehacer la amenaza, así
que menos posiciones dependen de una única casilla.

**Pero parte de eso es que no queda nada que salvar**: sin tablero el segundo está ya perdido
en la mitad de sus turnos, contra un cuarto con tablero.

### Lo que ninguna configuración arregla

**Al ve6 con tablero el juego se va al segundo, siempre.** De las 338 viables la mejor da 36,1
y la mayoría cae entre 29 y 34. No es un problema de piezas: en la franja habitable el segundo
tiene la victoria forzada, y a tres jugadas de cálculo por cabeza los jugadores empiezan a
encontrarla.

Eso reencuadra la conclusión de la sección 1. No es que exista una configuración equilibrada:
es que existe una **equilibrada hasta donde llega un jugador que calcula dos jugadas propias**,
y ninguna hace mejor que eso. Dicho lo cual, es también el mejor argumento a favor de la regla
sin tablero: al ve6 devuelve el fiel para el otro lado (63,0), que es exactamente la
compensación que faltaba.

### Cómo está implementada y por qué no toca nada

Vive en `Libre.cs`, aparte, y se prende con `--libre` en cualquier comando. `GameSpec` la
enciende con una bandera que por defecto está apagada. Tres cosas la sostienen:

- **El juego de siempre quedó idéntico al bit.** `solve --white 12344 --black 11245` sigue
  dando gana negro en 12 plies con los mismos 1.398.701 nodos y la misma línea principal que
  antes del cambio, y los 16 tests de `selftest` siguen pasando sin tocarse.
- **Tiene sus propios 22 tests** (`selftest --libre`): que ninguna posición alcanzable se salga
  del 3x3, que la caja crezca de a poco y se pueda volver a soltar, que el encuadre no pierda
  piezas, que una posición y su traslado sean la misma, que con el 3x3 ya clavado las dos
  variantes generen exactamente las mismas jugadas, y los de la sub-variante pegada.
- **Se contrastó contra una implementación escrita desde cero**, en Python, con coordenadas
  absolutas en un plano infinito, sin encuadre, sin simetrías y sin poda — o sea sin compartir
  ninguna idea de implementación con el solver. Coincide nodo por nodo en las primeras
  profundidades (4 · 220 · 6.064 · 122.104) y llega al mismo veredicto de 9 plies. Se hizo
  justamente porque el resultado era contraintuitivo.

Dos detalles del modelo que la descripción de la regla no fija y hubo que decidir, y que
cambiarían los números si se decidieran al revés: **las piezas no tienen que estar pegadas
entre sí** (alcanza con que entren en la caja; para la lectura contraria está `--pegado`), y
**la caja se recalcula después de cada jugada**, que es lo que permite que el 3x3 se suelte y
se rearme.

---

## 7. Cómo correr todo

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

# Lo mismo para muchos enfrentamientos y las dos reglas de una, a CSV
# (el archivo de pares tiene una línea "blancas negras" por enfrentamiento)
dotnet run -c Release -- practicas --pairs-file ../viables.txt --games 3000 --depths 2,4     --out ../viables_ve4.csv

# Jugadas forzadas y trampas: cuántos turnos tienen una sola salida y cuántas
# de ésas no se ven a 4 plies
dotnet run -c Release -- trampas --white 11344 --black 12245 --games 800 --vision 4 --verdad 8

# Barrer combinaciones y escribir un CSV
dotnet run -c Release -- sweep --gen-white-size 5 --gen-black-size 5 --max-rank 6 \
    --max-depth 17 --out ../balance_5v5.csv
```

Cualquiera de esos comandos acepta `--libre`, que los corre con la variante sin tablero de la
sección 6 en vez de con el juego normal, y `--pegado` / `--pegado-orto` / `--pegado-siempre`
para la sub-variante de la pieza pegada. `solve --pv --trace` dibuja el tablero ply por ply,
que con el tablero flotante es la única forma de leer la línea principal.

Los dos selftest son independientes: `selftest` son los 16 tests del juego de siempre y
`selftest --libre` los 22 de la variante.

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

## 8. Cómo leer los CSV

Los archivos de reparto real (`viables_*.csv`, `podio.csv`, `doblevuelta.csv`, `seccion2.csv`)
salen de `practicas` y tienen otras columnas: `white,black,regla,ve,gana1,gana2,sindef,turnos`,
donde `regla` es `fijo` o `libre`, `gana1` es el porcentaje del que arranca y `turnos` son en
realidad **plies** por partida.

Todos los barridos de veredicto tienen las mismas ocho columnas:

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
| `balance_5v5_libre.csv` | 63.504 — el mismo 5 contra 5, con la variante sin tablero de la sección 6 |
| `variante_*.csv` | familias de diseño, muestreadas |
| `profundos*.csv` | los cruces sin resolver, re-resueltos a 27 plies |
| `caja*.csv` | subconjuntos de un master, para la caja de dos versiones |
| `monotonia.csv` | el test de monotonía |
| `viables_ve2.csv`, `viables_ve4.csv` | las 338 configuraciones viables (sección 6) con el reparto real a ve2 y ve4, en las dos reglas |
| `podio.csv`, `doblevuelta.csv` | los finalistas con ve2/ve4/ve6 y 12.000 partidas por número |
| `seccion2.csv` | los candidatos de la sección 2 remedidos con el mismo criterio |

`simulacion.py`, `simulation_*.csv` y `old/` son de la simulación vieja. **Sus resultados no
sirven** — el famoso 68 % / 32 % salía de un modelo que no tenía pilas, no tenía la regla del
medio punto, tenía la detección de amenazas rota y jugaba con una IA codiciosa y aleatoria. Se
dejan sólo como registro.

---

## 9. Qué falta

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

**La variante sin tablero no está implementada en ningún lado todavía.** Está sólo en el
solver, detrás de `--libre`. Para probarla con gente hay que portarla a la web, y es poco
código: la regla entera es la caja de 3x3 y el encuadre, que en `Libre.cs` son la generación de
jugadas y `ApplyLibre`. La condición de victoria, el tapado y el medio punto no se tocan.

**Sobre cómo se buscó, que conviene tener presente si se vuelve a buscar.** Los barridos
exhaustivos —los CSV de 63.504 cruces— miden el veredicto con **juego perfecto**, que según la
sección 3 no es la métrica que contesta si el juego es parejo. La que contesta, el reparto real
con visión limitada, es cara, y durante casi todo el análisis se corrió sobre listas de
candidatos escritas a mano: doce en `tabla.py` y seis en la sección 2, cuatro de las cuales
comparten el mismo set del segundo. O sea que se decidió comparando contra el vecindario de un
set que gustó temprano, no contra el espacio.

El barrido de las 338 de la sección 6 cerró ese hueco para el formato 5v5 con rangos 1-5, y el
resultado es tranquilizador —la configuración de la sección 1 está entre las diez mejores de
338 para el juego con tablero— pero fue suerte de una búsqueda estrecha, no consecuencia del
método. **Quedan sin barrer así los otros formatos**: 4v5, 5v6 y 6v5 sólo tienen veredicto de
juego perfecto, nunca reparto real sistemático. Si alguna vez se reabre el formato, ahí está el
trabajo.
