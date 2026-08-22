# El juego del rey

Segunda versión del juego, y es un juego distinto: cambian las piezas, cambia el
movimiento y cambia la victoria. Por eso vive en su propio motor (`rey/`) y no toca el
anterior, que sigue funcionando con sus 80 tests en `../solver/`.

**Lo que hay que saber en una línea: arregla todo lo que estaba roto y rompe una cosa
nueva.** El empate se derrumbó de 48% a **0,83%** y los ahogados de 15,3% a **0,01%**,
pero el primer jugador gana entre el 74% y el 92%.

Y lo segundo que hay que saber: **eso se arregla**, y con dos banderas que salen de cosas
que ya habías dicho vos. `--rey-por-edificio --castillo-aguanta` deja el reparto en 53,5
con 34% de empates, sin perder ni la decisión ni la profundidad. Sección 5.

---

## 1. Lo que salió

Al azar, 200.000 partidas, comparado con el juego anterior:

| | juego anterior | juego del rey |
|---|---|---|
| empate | 48,0% | **0,83%** |
| ahogado (nadie puede jugar) | 15,3% | **0,01%** |
| termina en castillo | 36,7% | 38,1% |
| termina matando al rey | — | 61,1% |
| duración | 45 plies | 47 plies |
| reparto del primero | 50,76 | 50,34 |

Las tres cosas que estaban rotas se arreglaron solas, y cada una por un motivo concreto:

- **El tapiado desapareció** porque los edificios ahora son terreno. Nadie se encierra
  detrás de sus propias obras: se camina por encima de ellas.
- **El empate desapareció** porque hay dos maneras de ganar y las dos son activas. Ya no
  existe la posición donde los dos tienen todo y ninguno puede rematar.
- **La carrera de edificios dejó de sabotearse sola.** Construir ya no te quita casillas:
  te da una unidad.

Y hay dos cosas más que salieron bien:

- **La habilidad decide.** El que mira 8 plies le gana al que mira 2 el **96,5%** jugando
  primero y el **87,0%** jugando segundo.
- **Nadie gana por la fuerza en 17 plies** de búsqueda exacta, 41,6 millones de nodos.

---

## 2. Las reglas tal como las simulé

### Lo que estaba dicho y entró tal cual

- Cada uno tiene un **rey**, un **constructor**, un **guerrero** y un **sacerdote**, más un
  **taller**, un **cuartel** y una **iglesia**. Hay un **castillo** que no es de nadie.
- **El rey hace lo que hacen las otras unidades** mientras no las tenga sueltas por el
  tablero.
- **Cada edificio viene con su unidad adentro**: el taller trae el constructor, el cuartel
  el guerrero, la iglesia el sacerdote. La unidad aparece parada sobre el edificio nuevo.
- **Cualquier unidad entra a un edificio vacío**, sea de quien sea. **A uno ocupado sólo
  entra el guerrero**, matando al que estaba adentro.
- **Los edificios son terreno**: se pasa por arriba. Sólo te traba una unidad, tuya o del
  otro, nunca un edificio vacío.
- **El edificio nunca cambia de dueño ni se destruye.** Lo que cambia es quién lo
  **controla**: manda el que lo ocupa, y si está vacío manda el que lo construyó.
- **El castillo lo levanta cualquiera** que tenga el poder de construir y controle uno de
  cada tipo de edificio. **Cualquiera puede meterse a defenderlo.**
- **Ganás** metiendo tu rey en el castillo controlando los tres edificios, o matándole el
  rey al otro. **Perdés** si te matan el rey o si te toca jugar y no tenés jugadas.

### Lo que tuve que decidir

1. **El arranque es el rey solo, sin ningún edificio.** No dijiste con qué se empieza, y
   ésta es la única versión que hace que "el rey construye el taller" sea la jugada 1.
2. **La unidad que viene con el edificio está PARADA SOBRE él, no guardada adentro.** Con
   el modelo nuevo, donde los edificios son casillas por las que se pasa, "adentro" tiene
   que querer decir "encima". Es lo que hace que se la pueda matar ahí.
3. **Tus dos frases sobre el poder del rey se contradicen**, así que implementé las dos y
   una tercera que las concilia:
   - *"si con el rey construís un taller, el rey ya no puede construir más"*
   - *"quizás mientras la unidad está en su edificio, el rey sí puede usar su poder"*

   | Bandera | El rey tiene el poder de la unidad X mientras... |
   |---|---|
   | (por defecto) | X no esté suelta: o está muerta, o está parada sobre su edificio |
   | `--rey-pierde-poder` | X no exista en el tablero |
   | `--rey-por-edificio` | **no controles el edificio de X** |

   La tercera es mía y sale de notar que las dos tuyas se cumplen a la vez con ella:
   levantás el taller y el rey no construye más (frase 1), y sin cuartel el rey conserva
   para siempre el poder del guerrero, que es tu ejemplo del castillo defendido (frase 3).
   La que no cumple es la frase 2. **Es además la que da el juego más parejo, y por bastante
   (sección 5).**
4. **La regla del no-pegado se cayó.** En el juego anterior existía para que los edificios
   no bloquearan; ahora no bloquean nada. Está en `--no-pegado` y resulta que es la palanca
   de equilibrio más fuerte que hay (sección 5).
5. **El sacerdote no puede convertir a un rey.** Sale solo de la regla original: convertir
   pide que tu pieza de ese tipo esté fuera del tablero, y tu rey nunca lo está. Si lo
   estuviera, ya perdiste.
6. **Un rey muerto no vuelve.** No tiene edificio de dónde salir, y perder el rey es
   perder la partida.
7. **La victoria se cobra en el momento**, no al turno siguiente. La versión "hay que
   aguantar un turno" está en `--castillo-aguanta` y es media solución al desbalance.

---

## 3. La forma cruda del juego

Ramificación 8,65 (el anterior tenía 3,7): el rey solo ya tiene 8 jugadas en la primera
posición, porque puede construir tres cosas distintas en cada casilla libre de al lado.

Una partida a 6 plies, para ver el tono:

```
 24. negro   guerrero c3xb3 mata constructor      El guerrero negro entra al cuartel
 25. BLANCO  sacerdote a3 convierte guerrero      blanco y le mata el constructor adentro.
 26. negro   sacerdote c2-c3                      El blanco no tiene guerrero propio -porque
 27. BLANCO  rey b4-el CASTILLO en c4             le tomaron el cuartel- asi que el sacerdote
                                                  puede convertir al invasor. Y el rey entra.
```

Eso es una secuencia con causa y efecto, que es exactamente lo que el juego anterior no
tenía. La conversión funciona **porque** le habían tomado el cuartel: las reglas se hablan
entre ellas.

---

## 4. Lo que está roto: el primero gana

Es un problema de **tempo**, y es grande:

| Los dos miran | Reparto del primero | Empate |
|---|---|---|
| al azar | 50,3 | 0,8% |
| 2 plies | 63,4 | 7,2% |
| 4 plies | 76,1 | 11,2% |
| 6 plies | 74,8 | 14,0% |
| 8 plies | 80,4 | 11,2% |
| **10 plies** | **92,0** | 12,0% |

**Cuanto mejor juegan los dos, más gana el primero.** Ésa es la peor forma posible que
puede tener un desbalance: no se corrige con habilidad, se agrava.

### Por qué

La partida es una carrera de cinco jugadas y las dos manos corren igual: taller, cuartel,
iglesia, castillo, meter el rey. El primero llega un tempo antes, y **estorbar es más lento
que correr**: para matarle el rey al otro necesitás un cuartel (dos jugadas) y después
cruzar el tablero (hasta seis). Nadie va a gastar ocho jugadas en estorbar una carrera de
cinco.

### La prueba de que es exactamente un tempo

Con `--compensa` el segundo arranca con el taller ya levantado y su constructor adentro:
exactamente una jugada regalada. El resultado se da vuelta entero.

| Los dos miran | base | con `--compensa` |
|---|---|---|
| 4 plies | 76,1 | **11,0** |
| 8 plies | 80,4 | **3,5** |
| 10 plies | 92,0 | **0,8** |

De +92 a +1 con una sola jugada de diferencia. **Un tempo es la partida entera.** No es que
el primero tenga una ventajita: es que el juego está tan ajustado que la mano decide.

### Y no es cuestión de distancia

Probé si la ventaja venía de que los reyes arrancan lejos y estorbar cuesta caminar. No:

| Inicio | Distancia entre reyes | ve4 | ve6 | ve8 |
|---|---|---|---|---|
| esquinas | 6 | 76,1 | 74,8 | 80,4 |
| frentes | 4 | 65,9 | 80,4 | **100,0** |
| lados | 4 | 74,6 | 77,9 | 95,8 |
| centro | 2 | *(degenerado, ver 6)* | | |

Arrancar cerca es **peor**, no mejor. La ventaja no sale de la distancia, sale de la
carrera misma. **Ojo con esto si vas a un 5x5**: el 5x5 no la arregla por sí solo, porque
la carrera sigue siendo de cinco jugadas se juegue donde se juegue.

---

## 5. Las palancas, y el canje que hay entre ellas

Promedio de 4 a 10 plies. `desvío` es cuánto se aparta el reparto de 50: cuanto más chico,
más parejo.

| Variante | Reparto | Desvío | Empate |
|---|---|---|---|
| base | 80,8 | 30,8 | **12,1%** |
| `--castillo-aguanta` | 62,0 | 12,0 | 40,3% |
| `--rey-por-edificio` | 57,2 | 7,2 | 32,9% |
| `--castillo-aguanta --rey-pierde-poder` | 55,6 | 5,6 | 51,5% |
| **`--rey-por-edificio --castillo-aguanta`** | **53,5** | **3,5** | **34,1%** |
| `--no-pegado` | 48,7 | 1,3 | 58,0% |
| `--castillo-aguanta --no-pegado` | 48,0 | **2,0** | 73,2% |
| `--compensa --rey-pierde-poder` | 37,2 | 12,8 | 34,2% |
| `--compensa --castillo-aguanta` | 23,5 | 26,5 | 33,2% |

**La mejor combinación que encontré es `--rey-por-edificio --castillo-aguanta`**: reparto
53,5 con 34% de empates. Le gana en las dos columnas a la otra candidata, que empareja
parecido (55,6) pero empata el 51,5%. Y sigue teniendo todo lo bueno: la habilidad decide
igual (el que ve 8 plies le gana al que ve 2 el **96,5%** de primero y el **89,0%** de
segundo) y nadie gana por la fuerza en 17 plies.

Fuera de esa, el canje es limpio y va en una sola dirección: todo lo demás que empareja el
juego lo hace alargando o ensuciando la carrera, y una carrera más larga es más empate.

- **`--castillo-aguanta`** (entrar al castillo no gana: hay que seguir adentro cuando te
  vuelve a tocar jugar) es tu idea vieja y es la más elegante: le devuelve al defensor un
  tempo justo en el momento en que el tempo decide. Baja el desvío de 31 a 12 sola, y a
  **5,6** junto con `--rey-pierde-poder`. Cuesta subir el empate al 40-51%.
- **`--no-pegado`** es la que más empareja, igual que en el juego anterior. Y por la misma
  razón: hace la construcción difícil, así que la carrera se alarga y estorbar se vuelve
  rentable. Cuesta 58% de empates, y las partidas terminan matando al rey (88%) en vez de
  con el castillo (10%), que es otro juego.
- **`--rey-por-edificio`** empareja porque el rey deja de poder rematar la carrera solo: en
  cuanto tenés el taller, construir es trabajo del constructor, y el constructor camina.
  Baja el desvío de 31 a 7 y sube el empate apenas al 33%, que es de lejos el mejor canje.
- **`--compensa`** pasa de largo: da vuelta el desbalance en vez de arreglarlo.

Y una que no mueve casi nada: **`--rey-no-mata`** (reparto 60-77, empate 17-28%). Sacarle
al rey el poder de matar no cambia la carrera, que es lo que decide.

---

## 6. Los dos agujeros que encontré

### El rey construye todo y el constructor mira

Con la lectura de la guarnición —el rey conserva el poder mientras la unidad esté parada
en su edificio— **el 76% de las obras las hace el rey**, no el constructor. Levanta el
taller, el constructor aparece adentro y se queda ahí quieto haciendo de batería, y el rey
sigue construyendo todo lo demás. El constructor nunca juega.

**Y no lo arregla ninguna de las tres lecturas.** Medido:

| Lectura | Obras del rey al azar | Con búsqueda (4-10 plies) |
|---|---|---|
| guarnición (por defecto) | 76,3% | 54% |
| `--rey-pierde-poder` | 68,4% | 56% |
| `--rey-por-edificio` | 68,2% | 57% |

Las tres quedan en 54-57% con búsqueda: **son indistinguibles**. La razón es que el poder
del rey no se pierde, se presta: vuelve cada vez que se corta el vínculo. Con la guarnición
vuelve cuando matan al constructor —y se mata mucho, 2,7 muertes por partida—. Con
`--rey-por-edificio` vuelve cuando te ocupan el taller, y ocupar edificios ajenos pasa 4,08
veces por partida.

Visto así no es tan un agujero como una consecuencia: **los poderes del rey parpadean con el
control del mapa.** Si el otro te quiebra el taller, tu rey vuelve a ser albañil. Eso es
bastante lindo, en realidad. Lo que no vas a tener nunca es la progresión limpia de "el rey
empieza haciendo todo y termina sólo caminando": para eso el poder tendría que perderse de
una vez y para siempre, y ninguna de tus dos frases dice eso.

### Los dos reyes se pasan el mismo guerrero para siempre

Con `--inicio centro`, donde los reyes arrancan a dos casillas, el **100%** de las partidas
con búsqueda terminan en empate por repetición **en 6,2 plies**, con 4,00 conversiones por
partida:

```
5. BLANCO  rey b3 convierte guerrero en c3
6. negro   rey c2 convierte guerrero en c3
7. BLANCO  rey b3 convierte guerrero en c3     empate por repeticion
```

Ninguno de los dos tiene sacerdote propio, así que los dos reyes tienen el poder de
convertir, y se pasan el mismo guerrero de un bando al otro sin que pase nada más. **Es
exactamente la misma enfermedad que `--toma-libre` en el juego anterior**: una acción que
cuesta una jugada y se deshace en una jugada produce un ciclo de dos plies.

Sale caro sólo cuando los reyes arrancan cerca, pero está siempre. Un arreglo posible:
**que convertir no se pueda deshacer en el turno siguiente** —la pieza recién convertida es
inmune un turno— o que convertir cueste algo (que el sacerdote quede expuesto, que sólo se
pueda una vez por pieza).

---

## 7. Lo que probaría después

1. **Hacer que estorbar sea rápido, en vez de hacer que correr sea lento.** Es la única
   familia de arreglos que no probé y la que sale del diagnóstico: si la carrera son cinco
   jugadas y cruzar el tablero son seis, el problema no es la carrera, es la velocidad de
   las piezas. **Que el guerrero se mueva dos casillas** deja la carrera intacta y hace
   que la amenaza llegue a tiempo. Es la que más ganas tengo de medir.
2. **`--castillo-aguanta` con dos turnos de aguante** en vez de uno, sobre
   `--rey-por-edificio`. Si un tempo vale la partida, el segundo tempo de defensa debería
   llevar el 53,5 a rozar el 50 sin subir el empate como lo sube el no-pegado.
3. **La regla del pastel**: uno arma la posición inicial y el otro elige bando. No cambia
   ninguna mecánica y arregla desbalances de tempo por definición. Es lo que hacen los
   abstractos con este problema exacto (el Hex vive de esto). No se puede simular sin
   modelar a los dos jugadores negociando, pero en la mesa funciona.
4. **Un 5x5.** Con lo de arriba en la mano: **no arregla el desbalance solo**, porque la
   carrera es de cinco jugadas en cualquier tablero. Lo que sí hace es darle aire a todo lo
   demás, y combinado con el punto 1 es probablemente donde vive la versión buena. Ojo con
   el costo: la posición son dos planos de 16 casillas de 4 bits, y 25 casillas no entran
   en un `ulong` — habría que pasar a cuatro.
5. **Arreglar el ciclo de conversión** de la sección 6.

---

## 8. Cómo correrlo

```bash
cd CastilloDorado/rey
dotnet build -c Release
./bin/Release/net9.0/rey.exe <comando>
```

| Comando | Qué hace |
|---|---|
| `selftest` | 60 tests, uno por regla, más tres barridos exhaustivos de 5 plies. |
| `perft --prof 7` | Árbol completo. Da la ramificación. |
| `azar --partidas 200000` | Los dos al azar. La forma cruda del juego. |
| `practica --plies 8 --partidas 400` | Los dos miran N plies con evaluación de material. |
| `practica --plies 8 --plies-negro 2` | Duelo desparejo: mide si la habilidad decide. |
| `resolver --max-prof 17` | Busca victorias forzadas. Un veredicto de victoria es real. |
| `partida --plies 6 --semilla 3` | Una sola partida, dibujada jugada por jugada. |
| `comparar` | Las cuatro disposiciones por las variantes, en una tabla. |

Banderas de regla, válidas en cualquier comando:

```
--inicio esquinas|frentes|lados|centro
--rey-por-edificio    el rey pierde el poder por CONTROLAR el edificio, no por tener la
                      unidad: matarsela al otro ya no se lo devuelve  (RECOMENDADA)
--rey-pierde-poder    el rey pierde el poder apenas la unidad existe en el tablero
--no-pegado           vuelve la regla de que dos edificios no pueden tocarse
--castillo-aguanta    entrar al castillo no gana: hay que seguir adentro un turno despues
--compensa            el segundo arranca con el taller levantado y el constructor adentro
--rey-no-mata         el rey nunca mata, aunque tenga el poder del guerrero
--sacerdote-reubica   el sacerdote convierte aunque ya tenga esa pieza: la muda ahi
--repeticiones 3      cuantas repeticiones son empate
--plies-max 300       tope de plies
```

La mejor versión medida, la de la sección 5:

```bash
./bin/Release/net9.0/rey.exe practica --plies 8 --partidas 400 --rey-por-edificio --castillo-aguanta
```

Para reproducir el ciclo de conversión de la sección 6:

```bash
./bin/Release/net9.0/rey.exe partida --plies 6 --inicio centro --semilla 2
```

### Cómo está guardada una posición

**Dos** `ulong`, no uno: un plano de edificios y un plano de unidades, 16 casillas de 4 bits
cada uno. Hacen falta los dos porque ahora una unidad puede estar parada sobre un edificio,
que es de lo que se trata el juego entero.

- edificios: 0 vacío, 1-3 del primero, 4-6 del segundo, 7 el castillo
- unidades: 0 vacío, 1-4 del primero (rey, constructor, guerrero, sacerdote), 5-8 del segundo

Una unidad fuera del tablero no se guarda en ningún lado: como cada jugador tiene
exactamente una de cada tipo, "está afuera" es lo mismo que "no aparece". La tabla de
transposición ya no puede usar la posición como clave —son 128 bits— así que hashea los dos
planos y además guarda los dos `ulong` enteros para verificar, de manera que una colisión de
clave no ensucia el resultado.

### Qué garantiza el selftest

Cada test cita una frase de las reglas. Además hay tres barridos exhaustivos de 5 plies
—base, `--rey-pierde-poder` y `--no-pegado`, 94.887 nodos entre los tres— que comprueban
que ninguna jugada rompa las invariantes: nadie junta dos unidades del mismo tipo, nadie
levanta dos edificios del mismo tipo, nunca hay más de un castillo, no aparecen códigos
inventados, y ninguna obra cae encima de algo.

Lo que el selftest **no** garantiza es que las reglas sean las que vos tenías en la cabeza.
Eso es la sección 2.
