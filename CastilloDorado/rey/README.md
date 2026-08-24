# El juego del rey

Segunda versión del juego. Es un juego distinto al de `../solver/`: cambian las piezas, el
movimiento y la victoria. Por eso tiene su propio motor y este README se lee solo.

**Estado en una línea:** hay dos versiones vivas, y la elección entre ellas es un
intercambio real, no una duda por falta de datos.

```
# la simple: bien balanceada, pero el taller casi nunca se construye primero
--lado 5 --no-pegado --rey-reino --sacerdote-reubica --sacerdote-vuelve

# la de movimiento: el guerrero corre como torre y el constructor tiene trabajo propio
--lado 5 --no-pegado --rey-reino --sacerdote-reubica --sacerdote-vuelve \
  --desliza --solo-guerrero-corre --sacerdote-diagonal --construye-lejos --alcance-obra 2
```

A 8 plies con quietud: la simple da reparto 49,5 / 12% de empates / 64% de finales en
castillo, con el taller como primer edificio apenas el 3,5% de las veces. La de movimiento
da 52,8 / 16% / 56% con el taller al **15,8%** y el constructor haciendo el doble de
trabajo. Secciones 3.d y 3.e.

> **Aviso sobre los números viejos.** Todo lo medido antes de la sección 3.d salió de un
> buscador que evaluaba en seco en el horizonte. Las cifras de empates de aquella época
> (24%, 34%, 54%) eran en buena parte del buscador, no del juego. Están para mostrar el
> camino, no para decidir nada.

---

## 1. Las reglas

- Cada uno tiene un **rey**, un **constructor**, un **guerrero** y un **sacerdote**, más un
  **taller**, un **cuartel** y una **iglesia**. Hay un **castillo** que no es de nadie.
- **Se arranca con el rey solo**, sin ningún edificio. La primera jugada es construir.
- **El rey hace lo que hacen las otras unidades** mientras no las tenga sueltas. Cuál es
  exactamente la condición está en discusión: sección 2.
- **Cada edificio viene con su unidad adentro**: el taller trae el constructor, el cuartel
  el guerrero, la iglesia el sacerdote. La unidad aparece parada sobre el edificio nuevo.
- **Los edificios son terreno**: se camina por encima. Sólo te traba una unidad, nunca un
  edificio vacío. Por eso nadie se tapia como pasaba en el juego anterior.
- **Cualquier unidad entra a un edificio desocupado** y por estar ahí lo controla. **A uno
  ocupado sólo entra el guerrero**, matando al que estaba adentro y quedándose con el
  control en la misma jugada.
- **El edificio nunca se destruye ni cambia de dueño.** Lo que cambia es quién lo
  **controla**: manda el que lo ocupa, y si está vacío manda el que lo construyó. Un
  edificio tuyo pero ocupado por el otro ya no es tuyo.
- **El castillo lo levanta cualquiera** que tenga el poder de construir y controle uno de
  cada tipo de edificio. Cualquiera puede meterse a defenderlo.
- **Ganás** metiendo tu rey en el castillo controlando los tres edificios, o matándole el
  rey al otro. **Perdés** si te matan el rey o si te toca jugar y no tenés jugadas.
- Repetir la misma posición tres veces es empate.

### Lo que tuve que decidir

1. **El arranque es el rey solo.** Es la única versión donde "el rey construye el taller" es
   la jugada 1.
2. **La unidad que trae el edificio está PARADA SOBRE él**, no guardada adentro. Con los
   edificios convertidos en casillas transitables, "adentro" tiene que querer decir
   "encima". Es lo que hace que se la pueda matar ahí.
3. **El sacerdote no puede convertir a un rey.** Sale de la regla original: convertir pide
   que tu pieza de ese tipo esté fuera del tablero, y tu rey nunca lo está.
4. **Un rey muerto no vuelve.** No tiene edificio de dónde salir.
5. **El guerrero que ocupa un edificio lo controla mientras siga ahí**, no se lo lleva para
   siempre. Tenés un solo guerrero, así que negar cuesta clavarlo en un lugar.

---

## 2. Las banderas, en castellano

Sólo cinco están vivas. Las demás son diagnóstico o lecturas descartadas, y las dejo
listadas para que no confundan cuando aparezcan en una tabla.

### Las que importan

| Bandera | Qué hace |
|---|---|
| **`--lado 5`** | Tablero de 5x5 en vez de 4x4. |
| **`--no-pegado`** | Dos edificios no pueden estar pegados. Es la regla del juego anterior. |
| **`--rey-reino`** | El rey pierde el poder de una unidad si tiene **la unidad o el edificio**. Sólo lo recupera cuando no le queda ninguno de los dos: le mataron el guerrero **y** le ocuparon el cuartel. |
| **`--castillo-aguanta`** | Entrar al castillo no gana en el acto: hay que seguir adentro cuando te vuelve a tocar jugar. El rival tiene un turno para desalojarte o robarte un edificio. |
| **`--adelanta-segundo`** | El rey del segundo arranca una fila más adelante. Es una compensación mucho más chica que regalarle un edificio, y es la que mejor equilibra sin romper nada. |
| **`--sacerdote-reubica`** | El sacerdote convierte aunque ya tengas esa pieza: la tuya se muda ahí en vez de aparecer una segunda. |
| **`--sacerdote-vuelve`** | Después de convertir, el sacerdote **siempre deja la casilla**: va a su iglesia si está libre, y si no sale del tablero. |
| `--sacerdote-releva` | Como `--sacerdote-reubica` pero sólo mientras tu pieza esté guarnecida sobre su edificio. Versión intermedia. |
| **`--guerrero-veloz`** | El guerrero carga: se mueve hasta dos casillas en línea recta, atravesando una casilla vacía. Arregla el equilibrio pero se come el juego (sección 3). |

### Las del buscador

No son reglas del juego: son cómo se mide.

| Bandera | Qué hace |
|---|---|
| **`--quieta N`** | Al llegar al horizonte sigue las jugadas forzantes —matar y convertir— hasta N plies más, con derecho a plantarse, en vez de evaluar en seco. **Sin esto las mediciones no sirven**: sección 3.d. Usar siempre `--quieta 4`. |
| `--semilla N` | Cambia las partidas. Es la única forma de repetir una medición de verdad. |

### Las del movimiento (sección 3.e)

| Bandera | Qué hace |
|---|---|
| **`--desliza`** | Se corre en línea recta por terreno abierto y se frena **antes** de cualquier unidad o edificio. Entrar a un edificio es un paso suelto desde al lado. Es lo que hace que los edificios estorben. |
| **`--solo-guerrero-corre`** | Corre sólo el guerrero; el resto camina. Escalona la partida: la apertura es lenta y el tablero se acelera cuando aparece el cuartel. Sin esto, correr convierte el juego en una cacería. |
| **`--sacerdote-diagonal`** | El sacerdote convierte sólo en diagonal. El guerrero mata en cruz, así que cubren casillas disjuntas y el sacerdote roba desde donde no le pueden contestar. |
| **`--construye-lejos`** | Se construye a la vista: cualquier casilla de la línea, frenando en lo primero que la tape. Es lo único que logró que el taller se construya primero. |
| **`--alcance-obra N`** | Tope de casillas para la obra a distancia. Sin tope los dos se amurallan y los empates se duplican. |

### Las que no

| Bandera | Por qué está |
|---|---|
| `--rey-ajedrez` | El rey camina en ocho direcciones en vez de correr. Un rey lento con una torre enemiga es presa: el castillo cae de 56,5% a 32,5%. |
| `--sale-caminando` | Parado sobre un edificio no se corre. Reparto 74,8: castiga al que se defiende, o sea al que va perdiendo. |
| `--guerrero-largo` | Matar al final de la corrida. Rompe la frase que ordena todo el movimiento y borra el aviso de un turno. |
| `--edificio-sin-unidad` | Los edificios no traen su unidad. Probaba que alargar la carrera diluiría el tempo; no lo hace, y sube empates y alarga las partidas un 30%. |
| `--regalo` / `--regalo-donde` / `--regalo-casilla` | El segundo arranca con un edificio puesto. Compensaba un desbalance que en buena parte era del buscador. |

| Bandera | Por qué está |
|---|---|
| `--rey-pierde-poder` | Otra lectura del poder del rey: lo pierde apenas la unidad existe. Peor que `--rey-reino`. |
| `--rey-por-edificio` | Otra más: lo pierde por controlar el edificio. Casi tan buena, un poco más frágil. |
| `--control-guerrero` | Que sólo el guerrero tome control de un edificio. **Contradice la regla base** y la empeora: queda sólo por si hay que volver a mirarla. |
| `--compensa` | El segundo arranca con el taller puesto. **No es una regla propuesta**: sirvió para probar que un tempo decide la partida. |
| `--rey-no-mata` | El rey nunca mata. No mueve casi nada. |

---

## 3. Dónde estábamos (medido con el buscador roto)

⚠ **Esta tabla y las dos subsecciones que le siguen se midieron sin búsqueda de quietud.**
Las conclusiones de forma siguen valiendo —el 5x5 con no-pegado produce mejor juego que el
4x4, `--guerrero-veloz` arregla el número equivocado— pero **las cifras de empates están
infladas** y el desvío del reparto es poco confiable. La sección 3.d explica por qué y
la 3.e trae las mediciones buenas.

Cuatro configuraciones, todas sobre `--rey-reino --castillo-aguanta`. Los números con
búsqueda son el promedio de 4, 6 y 8 plies; `desvío` es cuánto se aparta de 50 el reparto
del primero.

| | 4x4 | 4x4 +no-pegado | **5x5 +no-pegado** | 5x5 +no-pegado +veloz |
|---|---|---|---|---|
| **equilibrio** (desvío) | 3,3 | **2,1** | 10,5 ⚠ | **1,6** |
| **empates** | 34,9% | 54,8% ⚠ | **24,2%** | 36,5% |
| **duración** | 33 plies | 54 | 51 | 40 |
| **orden de construcción** más jugado | 69,6% ⚠ | 64,3% ⚠ | **22,9%** | 30,1% |
| **primer edificio** (taller/cuartel/iglesia) | 14/17/**69** ⚠ | 4/**79**/17 ⚠ | **29/38/33** | 27/45/27 |
| **termina en castillo** | 29,6% | 4,0% ⚠ | **67,2%** | 11,2% ⚠ |
| **uso**: rey/constr/guerr/sacerd | 30/25/26/19 | 39/12/29/19 | 37/22/26/14 | 36/14/**45**/6 ⚠ |
| ahogados | 0,02% | 0,02% | **0,00%** | 0,00% |

Para tener la referencia de dónde veníamos: el juego anterior empataba el **48%** al azar y
el **99,7%** con búsqueda, con **15,3%** de ahogados. Todo eso está resuelto en las cuatro
columnas.

### Lo que dice la tabla

**El 5x5 con el no-pegado es el que mejor juego produce, y falla en una sola cosa.**

Gana en tres de los cuatro ejes que importan:

- **El orden de construcción es táctico.** En el 4x4 casi siete de cada diez partidas juegan
  **el mismo orden** y la iglesia va primera el 69% de las veces. En el 5x5 el orden más
  jugado es el 22,9% —de seis posibles, o sea apenas por encima del azar— y el primer
  edificio reparte **29/38/33**. La elección pasó a ser una decisión.
- **Empata menos**: 24,2%, contra 34,9% del 4x4.
- **El castillo pasa a ser la forma de ganar**: 67,2% de los finales, contra 29,6%. En el
  4x4 con no-pegado el castillo está prácticamente muerto, 4%.

Y falla en el equilibrio: **desvío 10,5, y creciendo con la profundidad** (52,6 → 61,9 →
67,0 a 4, 6 y 8 plies). Esa forma —que empeore cuanto mejor se juega— es la peor que puede
tener un desbalance.

### Por qué el 5x5 desequilibra

Ya estaba diagnosticado y el 5x5 lo confirma. Ganar es una **carrera de cinco jugadas**
—taller, cuartel, iglesia, castillo, meter el rey— y esas cinco son las mismas se juegue en
el tablero que se juegue, porque construís pegado a vos mismo. Lo que sí crece con el
tablero es **la distancia que hay que cruzar para estorbar**. Un tablero más grande deja la
carrera igual de corta y el estorbo más lento: el que sale primero llega antes y el otro no
llega a molestarlo.

La prueba está en la última columna. `--guerrero-veloz` no toca la carrera, sólo hace que el
estorbo llegue a tiempo, y **el desvío cae de 10,5 a 1,6 y deja de crecer con la
profundidad** (50,6 / 51,8 / 52,2 / 50,8). El diagnóstico era exacto.

**Pero la cura sale cara.** El guerrero pasa a hacer el **45%** de las jugadas, el sacerdote
cae al **5,7%**, y los finales por castillo se derrumban del 67% al 11%: el juego deja de ser
una carrera con estorbo y pasa a ser una cacería del rey. Se arregló el número equivocado.

---

## 3.b Dónde arrancan los reyes: importa más que casi todo

En el 5x5 sólo había medido esquina contra esquina, que es **la distancia máxima posible** y
por lo tanto el peor caso para que el estorbo llegue a tiempo. Medido en serio, todas sobre
`--lado 5 --no-pegado --rey-reino --castillo-aguanta`:

| Inicio | Distancia entre reyes | Desvío | Empate | Castillo | Primer edificio T/C/I | Orden más jugado |
|---|---|---|---|---|---|---|
| `esquinas` | 8 | 10,5 | 24,2% | 67,2% | 29/38/33 | 22,9% |
| **`esquinas --adelanta-segundo`** | **7** | **4,6** | **25,0%** | **65,6%** | **35/30/35** | **28,0%** |
| `lados` | 6 | 5,4 | 27,3% | 48,4% | 10/43/46 | 29,4% |
| `centro` | 6 | 5,1 | 35,1% | 50,8% | 4/55/41 | 50,0% |
| `frentes` | 4 | 3,3 | 31,3% | 43,6% | 4/43/53 | 38,3% |
| `frentes --adelanta-segundo` | 3 | 5,3 | 49,8% | 40,0% | 3/79/18 | 73,0% |
| `adelantados` | 2 | — | **100%** ⚠ | 0% | — | — |

Salen dos patrones limpios y opuestos:

1. **Cuanto más cerca arrancan, mejor el equilibrio.** De 10,5 a 3,3. Confirma el
   diagnóstico: la ventaja del primero es la distancia que tiene que cruzar el otro para
   estorbar.
2. **Pero cuanto más cerca, peor todo lo demás.** El orden de construcción se vuelve a
   forzar (el taller pasa de primero el 29% de las veces al 4%) y el castillo deja de ser la
   forma de ganar (67% → 44%). Acercarlos hace que la partida se resuelva peleando, no
   construyendo.

Y **acercar a los dos rompe el juego**: con los reyes a dos casillas, el 100% de las partidas
empatan por el ciclo de conversión del hallazgo 3. Es el mismo agujero que aparecía en el
4x4 con `--inicio centro`.

**La que gana es adelantar sólo al segundo desde las esquinas.** Baja el desvío de 10,5 a
4,6 sin tocar nada de lo bueno: los empates quedan igual (25,0%), el castillo sigue siendo la
forma de ganar (65,6%) y el primer edificio queda **35/30/35**, el reparto más uniforme de
toda la tabla. Es media jugada de compensación en vez de una entera, que es lo que
`--compensa` demostró que sobra.

*(Las corridas son de 250 a 400 partidas por celda: los números por profundidad tienen
bastante ruido y hay que mirar los promedios, no cada punto.)*

---

## 3.c El sacerdote, y por qué la métrica de uso engañaba

La cuenta de "uso por unidad" siempre contó **toda** jugada de la pieza, caminar incluido.
Eso escondía lo importante:

| | jugadas del sacerdote | de esas, sólo caminar | conversiones por partida |
|---|---|---|---|
| sin regla | 14,4% | **92,2%** | 0,43 |
| `--sacerdote-releva` (con guarnición) | 17,1% | 88,4% | **0,86** |
| `--sacerdote-reubica` (sin requisito) | 20,4% | 75,6% | **2,12** |

**Nueve de cada diez jugadas del sacerdote eran caminar.** Metía presión sin usar nunca la
habilidad, exactamente como estaba la sospecha. Ahora el reporte lo muestra partido.

El requisito de guarnición **duplica** el uso real de la habilidad, y quitarlo lo
quintuplica.

### El regreso a la iglesia

`--sacerdote-vuelve`: después de convertir, el sacerdote **siempre deja la casilla**. Si su
iglesia está libre aterriza ahí; si no, sale del tablero y cuesta un turno volver a
desplegarlo, con la iglesia sin nadie mientras tanto. Una regla, sin ramas. Y le da a las
unidades que no son el guerrero un trabajo que no tenían: **pararse en la iglesia enemiga
convierte cada conversión del rival en un sacrificio**.

Vale sólo para el sacerdote de verdad, no para el rey usando el poder prestado: mandar al
rey adentro de un edificio sería un recurso de seguridad enorme y gratis.

Todo sobre `--lado 5 --no-pegado --rey-reino --castillo-aguanta --adelanta-segundo`,
promedio de 4 a 8 plies:

| | Desvío | Empate | Castillo | Sacerdote: % jugadas / % sólo caminar / habilidades por partida |
|---|---|---|---|---|
| sin regla | **5,0** | 25,1% | 61,3% | 15,8% / 91,0% / 0,53 |
| `+vuelve` sola | 9,4 | 23,8% | 62,2% | 14,0% / **92,8%** / **0,34** ⚠ |
| `+releva` | 10,0 | 22,9% | 63,4% | 19,2% / 83,3% / 1,12 |
| `+releva +vuelve` | 11,7 | 19,9% | 66,8% | 17,5% / 84,8% / 0,94 |
| `+reubica` | 12,0 | 18,4% | 72,2% | 18,1% / 73,1% / 1,45 |
| **`+reubica +vuelve`** | 10,3 | **13,6%** | **75,6%** | **19,7% / 76,8% / 1,46** |

**Sola, la regla del regreso es contraproducente.** Baja las habilidades del sacerdote de
0,53 a 0,34 por partida y le sube el caminar al 92,8%. La razón es la que se podía sospechar
y ganó: mandar al sacerdote a casa hace que **cada conversión cueste un viaje de ida y
vuelta**, y con búsqueda eso no paga. Sobrevivir importa menos que el tempo.

**Pero encima de `--sacerdote-reubica` es exactamente el contrapeso que faltaba.** La
reubicación sola hace la conversión muy fuerte —robás la pieza y reposicionás la tuya de un
saque— y el regreso le pone el precio de tener que volver a salir. El resultado es el mejor
punto medido del proyecto en dos de los cinco criterios: **13,6% de empates**, el número más
bajo de todo, y **75,6% de finales por castillo**, contra 11% por matar al rey. Y el
sacerdote pasa a ser la segunda pieza más usada del tablero.

El orden de construcción tampoco se rompe, que era el riesgo: el orden más jugado queda en
30,3% de seis, y el primer edificio reparte **35/33/32**, el reparto más parejo de todo el
proyecto.

**Lo que empeora es el equilibrio**: desvío 10,3 contra 5,0. Y el mecanismo es el mismo de
siempre —un sacerdote fuerte acelera la partida, de 52 plies a 45-48, y una partida más
rápida favorece al que sale primero—. O sea que la media jugada de `--adelanta-segundo`
alcanzaba para el juego lento y no alcanza para éste.

---

## 3.d El buscador medía mal, y buena parte de lo que arreglamos no estaba roto

El defecto: al llegar al horizonte, `Practico` hacía `return Evaluar(...)`. Evaluaba **en el
medio de un intercambio**. Con profundidad impar la última jugada que ve cada jugador es la
propia —ve su captura y no ve la respuesta— y con profundidad par ve la respuesta. El mismo
juego, medido a 5, 6, 7 y 8 plies:

| plies | 5 | 6 | 7 | 8 |
|---|---|---|---|---|
| reparto | 65,5 | 55,6 | 67,4 | 46,8 |
| **empates** | **0,6%** | **26,3%** | **1,8%** | **26,3%** |

No es gradual: es un interruptor. Lo agrava que la evaluación tiene un término de −200 por
rey pegado a un guerrero enemigo, así que con horizonte impar acercar el guerrero vale +200
y la huida del rey no se ve nunca.

**Con `--quieta 4`** —sigue sólo las jugadas forzantes, matar y convertir, hasta que la
posición se calma, con derecho a plantarse— el interruptor desaparece: empates 2,5 / 10,8 /
7,6 / 12,0 y el reparto se mueve en 7 puntos en vez de 20.

Dos advertencias que salieron de ahí y valen para cualquier medición futura:

1. **Coronar no va en la quietud.** Lo puse al principio razonando "es un salto grande de
   evaluación". Está mal: no es una captura, el rival no tiene con qué contestarla dentro de
   la quietud, y la línea termina con uno coronado y el otro sin jugar. El reparto daba
   80,8% a 6 plies.
2. **Dos corridas con la misma semilla y distinto `--partidas` NO son réplicas.** Los hilos
   arrancan del mismo estado del generador, así que comparten la mayoría de las partidas.
   Para repetir de verdad hay que cambiar `--semilla`. Con 300 partidas a 8 plies el error de
   una corrida es de **±2,5 puntos** de reparto: por debajo de ~7 puntos de diferencia, dos
   configuraciones son indistinguibles.

### Qué regla se gana el lugar

Con el buscador derecho, sacando de a una (8 plies, 200 partidas):

| se saca | reparto | empates | castillo | 1er edificio |
|---|---|---|---|---|
| nada | 49,5 | 12,0% | 64,0% | taller 3,5 |
| `--castillo-aguanta` | 45,8 | 14,5% | 61,5% | taller 4,5 |
| sacerdote (reubica+vuelve) | 38,8 | 19,5% | 44,5% | taller 3,8 |
| `--rey-reino` | 62,0 | 25,0% | 58,5% | **taller 32** |
| `--no-pegado` | **93,5** | 5,0% | 90,5% | — |

- **`--no-pegado` sostiene el juego.** Sin ella el primero gana el 93,5%: amontona los tres
  edificios pegados al rey y corona en 27 plies.
- **`--castillo-aguanta` ya no hace nada** y se puede borrar. Se agregó para cortar
  repeticiones que resultaron ser del buscador.
- **`--rey-reino` es la causa de que el taller no se construya nunca.** Construir el taller es
  controlar un taller, y eso le apaga al rey el poder de construir — y el rey hace el 86%
  de las obras. Sin la regla el taller sale primero el 32% de las veces, pero el reparto se
  va a 62 y los empates al 25%.

---

## 3.e El movimiento: correr, y qué pasa cuando los edificios estorban

Hasta acá **sólo estorbaban las unidades**: un edificio vacío lo pisa cualquiera, así que los
7 edificios eran decoración transitable y los únicos obstáculos eran las 8 unidades. Con
movimiento de un paso, además, "estorbar" y "no se puede entrar" son lo mismo — y no
entrar rompe el control, que es por ocupación. Así que para que los edificios estorben hace
falta alcance.

`--desliza`: se corre en línea recta por terreno abierto y se frena **antes** de cualquier
unidad o edificio. Entrar a un edificio pasa a ser un paso suelto desde al lado, o sea que
tomar uno cuesta dos turnos si no estabas pegado.

**Correr solo desbalancea el juego hacia la cacería.** Con todos corriendo, el castillo cae
del 64% al 25-35% y matar al rey pasa a ser el final normal. Tres explicaciones mías
fallaron antes de dar con la buena: no era la velocidad de construir, no eran los reyes
cazándose temprano (`--rey-no-mata` no movió el castillo ni medio punto), y no era el rey
lento. Era que **un guerrero tipo torre amenaza toda su cruz** y no hay dónde esconderse.

Lo que sí funciona es **escalonar**: `--solo-guerrero-corre`. Mientras no hay cuartel nadie
corre y la apertura es la carrera de obra de siempre; recién cuando aparece el guerrero el
tablero se vuelve rápido — y en esa misma jugada el rey deja de matar, o sea que pasa de
cazador a presa justo cuando el tablero acelera.

| 8 plies, 250 partidas | reparto | empates | castillo | rey muerto | 1er edificio |
|---|---|---|---|---|---|
| sin correr (la simple) | 49,5 | 12,0% | 64,0% | 24,0% | taller 3,5% |
| `--solo-guerrero-corre --sacerdote-diagonal` | 56,4 | 13,6% | **67,2%** | 19,2% | taller **0%** |
|  + `--construye-lejos --alcance-obra 2` | 52,8 | 16,0% | 56,0% | 28,0% | taller **15,8%** |

**`--construye-lejos` es lo único que destrabó el taller**, y no por un incentivo: porque le
dio al constructor un trabajo que el rey no hace igual de bien. Las obras del rey caen del
93% al 76% y el constructor pasa de 1,26 a 2,04 habilidades por partida. El costo son
empates: sin tope de alcance, tapar un carril desde lejos es tan bueno defendiendo que **los
dos se amurallan** y los empates saltan de 10% a 22%. De ahí `--alcance-obra 2`.

Lo que se probó y no va:

- **`--rey-ajedrez`** (el rey camina en ocho direcciones). Un rey lento con una torre enemiga
  en el tablero es presa: el castillo cae de 56,5% a 32,5%.
- **`--sale-caminando`** (guarnecido no se corre). Reparto 74,8: castiga al que se defiende,
  que es siempre el que va perdiendo, así que amplifica la ventaja.
- **`--guerrero-largo`** (matar al final de la corrida). Rompe la única frase que ordena todo
  el movimiento —correr es por terreno abierto, entrar a algo ocupado es siempre el último
  paso desde al lado— y borra el aviso de un turno.
- **El 4x4 con guerrero torre está muerto**: castillo 13,6%, rey muerto 67,2%. En 16 casillas
  una torre alcanza todo.

**`--sacerdote-diagonal`** sí va, y es casi gratis. Si el guerrero mata en cruz y el sacerdote
convierte en aspa, cubren casillas disjuntas: el sacerdote roba desde la diagonal a un
guerrero que no puede contestarle. Eso le da un trabajo concreto —castigar al que sitia un
cuartel para impedir el despliegue— y es la primera vez que las tres unidades se necesitan.

---

## 4. Los tres hallazgos

1. **Un tempo es la partida entera.** Regalarle al segundo exactamente una jugada
   (`--compensa`) da vuelta el 4x4 pelado de 92-8 a 1-99. No hay margen: la carrera es tan
   ajustada que la mano decide.
2. **El orden de construcción lo fija la regla que apaga al rey.** Bajo `--rey-reino`,
   levantar el taller es lo que le saca al rey el poder de construir, así que conviene
   postergarlo y hacer la iglesia primero. En un 4x4 eso alcanza para forzar el orden; en un
   5x5 hay lugar para que la decisión vuelva a ser táctica.
3. **Lo reversible cicla.** Con los reyes arrancando pegados, los dos se convierten el mismo
   guerrero para siempre y el 100% de las partidas empatan en 6 plies. Es la misma
   enfermedad que en el juego anterior producía el ciclo del cuartel: una acción que cuesta
   una jugada y se deshace en una jugada.

---

## 5. Qué probaría después

1. **Más compensación para el segundo.** `--adelanta-segundo` da media jugada y alcanzaba
   para el juego lento; con el sacerdote fuerte la partida se acorta y hace falta más.
   Adelantarlo dos filas en vez de una es lo más barato de probar, y es el único eje donde
   está claro qué hay que mover y en qué dirección.
2. **Arreglar el ciclo de conversión**, que ahora es lo que más bloquea. Es lo que hace
   inservible cualquier arranque cercano —y los arranques cercanos son lo que mejor
   equilibra— así que resolverlo desbloquea toda una familia de posiciones iniciales.
   Candidatos: que una pieza recién convertida sea inmune un turno, o que convertir cueste
   algo más que el turno.
3. **Edificios que bloqueen**, para que la partida termine sola. Como los edificios sólo se
   agregan, un edificio que además tape la casilla achica el tablero de manera monótona y
   fuerza un final: es la única familia de reglas que puede matar el empate sin agregar
   una regla de ida y vuelta. Hay que decidir cuál bloquea y a quién.
3. **Que el primer edificio cueste dos turnos**, o que el rey no pueda construir en el
   primer turno. Ataca la carrera de frente en vez de por los costados.
4. **Arreglar el ciclo de conversión** del hallazgo 3: que una pieza recién convertida sea
   inmune un turno, o que convertir cueste algo.
5. **6x6.** Con el diagnóstico en la mano hay que esperar que el equilibrio empeore todavía
   más, no que mejore. Sólo tiene sentido con el punto 1 resuelto.

---

## 6. Cómo correrlo

```bash
cd CastilloDorado/rey
dotnet build -c Release
./bin/Release/net9.0/rey.exe <comando>
```

La versión que recomiendo mirar:

```bash
./bin/Release/net9.0/rey.exe practica --partidas 400 --plies 8 --lado 5 --no-pegado --rey-reino --castillo-aguanta
```

| Comando | Qué hace |
|---|---|
| `selftest` | 90 tests, uno por regla, más cinco barridos exhaustivos. |
| `perft --prof 7` | Árbol completo. Da la ramificación. |
| `azar --partidas 200000` | Los dos al azar. La forma cruda del juego. |
| `practica --plies 8 --partidas 400` | Los dos miran N plies con evaluación de material. |
| `practica --plies 8 --plies-negro 2` | Duelo desparejo: mide si la habilidad decide. |
| `resolver --max-prof 17` | Busca victorias forzadas. Un veredicto de victoria es real; "no sé" no dice nada. |
| `partida --plies 6 --semilla 3` | Una sola partida, dibujada jugada por jugada. |
| `comparar` | Las cuatro disposiciones por las variantes, en una tabla. |

Todas las banderas de la sección 2 valen en cualquier comando. Para ver el ciclo de
conversión del hallazgo 3:

```bash
./bin/Release/net9.0/rey.exe partida --plies 6 --inicio centro --semilla 2
```

---

## 7. Cómo está hecho

### La posición

**Dos planos** de 4 bits por casilla: uno de edificios y uno de unidades. Hacen falta los dos
porque una unidad puede estar parada sobre un edificio, que es de lo que se trata el juego.

- edificios: 0 vacío, 1-3 del primero, 4-6 del segundo, 7 el castillo
- unidades: 0 vacío, 1-4 del primero (rey, constructor, guerrero, sacerdote), 5-8 del segundo

Cada plano es un `UInt128`, no un `ulong`: **el 5x5 son 25 casillas por 4 bits, o sea 100
bits, que en 64 no entran.** El tamaño del tablero es global y se fija una sola vez por
proceso con `Juego.Configurar`, antes de crear nada y antes de arrancar los hilos.

Una unidad fuera del tablero no se guarda en ningún lado: como cada jugador tiene
exactamente una de cada tipo, "está afuera" es lo mismo que "no aparece". La tabla de
transposición mezcla los cuatro `ulong` en una clave de 64 bits y la guarda entera para
verificar, así que una colisión de índice no ensucia el resultado.

### Qué garantiza el selftest

Cada test cita una frase de las reglas. Además hay cinco barridos exhaustivos —tres de 5
plies en 4x4 y dos de 4 plies en 5x5, 102.721 nodos entre todos— que comprueban que ninguna
jugada rompa las invariantes: nadie junta dos unidades del mismo tipo, nadie levanta dos
edificios del mismo tipo, nunca hay más de un castillo, no aparecen códigos inventados, y
ninguna obra cae encima de algo ni pegada a otro edificio cuando la regla lo prohíbe.

Lo que el selftest **no** garantiza es que las reglas sean las que vos tenías en la cabeza.
Eso es la sección 1.
