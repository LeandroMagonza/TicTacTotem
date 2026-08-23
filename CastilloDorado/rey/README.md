# El juego del rey

Segunda versión del juego. Es un juego distinto al de `../solver/`: cambian las piezas, el
movimiento y la victoria. Por eso tiene su propio motor y este README se lee solo.

**Estado en una línea:** funciona, y hay un canje sin resolver entre **equilibrio** y **que
el orden de construcción sea táctico**. El 4x4 está parejo pero el orden viene forzado; el
5x5 tiene el orden más lindo pero gana el primero. Sección 3.

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
| **`--guerrero-veloz`** | El guerrero carga: se mueve hasta dos casillas en línea recta, atravesando una casilla vacía. Arregla el equilibrio pero se come el juego (sección 3). |

### Las que no

| Bandera | Por qué está |
|---|---|
| `--rey-pierde-poder` | Otra lectura del poder del rey: lo pierde apenas la unidad existe. Peor que `--rey-reino`. |
| `--rey-por-edificio` | Otra más: lo pierde por controlar el edificio. Casi tan buena, un poco más frágil. |
| `--control-guerrero` | Que sólo el guerrero tome control de un edificio. **Contradice la regla base** y la empeora: queda sólo por si hay que volver a mirarla. |
| `--compensa` | El segundo arranca con el taller puesto. **No es una regla propuesta**: sirvió para probar que un tempo decide la partida. |
| `--rey-no-mata` | El rey nunca mata. No mueve casi nada. |
| `--sacerdote-reubica` | El sacerdote muda su propia pieza en vez de duplicarla. Traída del juego anterior. |

---

## 3. Dónde estamos

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

1. **La carga sin golpe.** El guerrero se mueve dos casillas pero **no puede matar al final
   de una carga**: cierra distancia rápido y necesita un turno más para pegar. Debería
   quedarse con lo bueno de `--guerrero-veloz` —que la amenaza llegue a tiempo— sin
   convertir el juego en una cacería. Es la que más ganas tengo de medir y son dos líneas.
2. **Aguantar dos turnos en el castillo** en vez de uno, sobre el 5x5. Es el otro lado del
   mismo problema: en vez de acelerar el estorbo, darle al defensor un tempo más justo donde
   se decide.
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
