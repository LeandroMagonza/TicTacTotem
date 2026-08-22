# El juego de los cuatro edificios

Simulación de un abstracto de dos jugadores en un tablero de 4x4. Es un juego distinto al
TaTeTi con Esteroides: vive en esta carpeta aparte y no comparte nada de código con
`Simulacion/`.

El motor está en `solver/`. Es C# sobre .NET 9, con generación de jugadas exacta,
búsqueda alfa-beta y tests que comprueban regla por regla.

---

## 1. Lo que salió

**El juego es un empate, y el empate se profundiza cuanto mejor juegan los dos.**

| Los dos miran | Empate | Gana alguien | Obras por partida | Duración |
|---|---|---|---|---|
| al azar | 48,1% | 51,9% | 3,67 | 45 plies |
| 2 plies | 100,0% | 0,0% | 4,00 | 31 plies |
| 4 plies | 87,3% | 12,7% | 3,83 | 35 plies |
| 6 plies | 80,4% | 19,6% | 3,81 | 35 plies |
| 8 plies | 54,3% | 45,7% | 3,49 | 31 plies |
| 10 plies | 93,2% | 6,8% | 2,56 | 58 plies |
| 12 plies | 87,9% | 12,1% | 2,45 | 62 plies |
| **14 plies** | **99,7%** | **0,3%** | **2,42** | **91 plies** |

Mirá la columna de obras: cuanto más adelante ve un jugador, **menos construye**. A 14 plies
los dos juntos levantan 2,42 edificios en toda la partida, o sea que cada uno agrega
apenas uno al taller con el que arrancó. Ninguno se anima al tercero. Y sin el tercero
no hay castillo, así que la partida se va a repetición.

Tres cosas más, todas medidas:

- **No hay sesgo de salida de fondo.** Al azar el reparto para el primero es 50,7, y con los
  dos mirando 14 plies queda en 48-50 en las cuatro disposiciones. En el medio hay baches
  raros que valen la pena y están en la sección 7.
- **Nadie gana por la fuerza en 27 plies.** Búsqueda exacta desde las cuatro aperturas,
  18 millones de nodos en la peor. Ni el primero ni el segundo.
- **Hay una sola palanca que revive la regla de eliminación**: `--toma-libre`, dejar que el
  guerrero entre a cualquier edificio aunque ya tenga uno de ese tipo. Con eso el 7,5% de
  las partidas se ganan dejando al otro sin constructor y sin taller, un final que en el
  juego base es literalmente inalcanzable (secciones 5 y 9.1).
- **La habilidad decide todo.** El que mira 12 plies le gana al que mira 2 el **99,25%**
  jugando primero y el **100%** jugando segundo. El juego tiene profundidad; el problema
  no es que sea plano, es que entre iguales no se puede cerrar.

Y una regla entera que no se puede usar: **la derrota por quedarse sin constructor y sin
taller es inalcanzable**, y no por poco probable sino por imposible. Sólo podés entrar a un
edificio que no tenés, los dos arrancan con taller, y nada saca un edificio del tablero: el
taller no puede cambiar de manos nunca. Cero derrotas así en 2.000.000 de partidas, que es
lo que la prueba anticipa. Está en la sección 5.

---

## 2. Las reglas tal como las simulé

Esto importa leerlo, porque el enunciado dejó varias cosas sin definir y tuve que elegir.
Cada elección discutible es un interruptor del simulador, así que dar vuelta cualquiera
cuesta una bandera y no tocar código.

### Lo que estaba dicho y entró tal cual

- Tablero de 4x4. Cada jugador tiene **constructor, guerrero, sacerdote** (unidades) y
  **taller, cuartel, iglesia** (edificios).
- En tu turno, **o movés una unidad o hacés una acción**. Nunca las dos.
- Mover es **una casilla vacía adyacente, sin diagonales**.
- **Construir**: el constructor levanta un edificio en una casilla vacía adyacente a él.
  El constructor no se mueve. No podés construir un tipo que ya tenés.
- **Convertir**: el sacerdote reemplaza una pieza enemiga adyacente por la propia del mismo
  tipo, que tiene que estar fuera del tablero.
- **El guerrero mata** pisando una unidad enemiga adyacente.
- **El guerrero toma edificios** metiéndose adentro, sólo si no tiene ya uno de ese tipo.
- **Los edificios son el punto de aparición.** Del taller sale el constructor, del cuartel
  el guerrero, de la iglesia el sacerdote. Sale a cualquier casilla vacía adyacente y
  cuesta el turno entero.
- **Se pierde sin constructor y sin taller.**
- **El que no puede jugar pierde.**
- **Repetición: empate.**
- Con los tres edificios propios, el constructor levanta el **castillo dorado** y gana.

### Lo que tuve que decidir

**1. Cómo empieza la partida.** El enunciado no lo dice. Lo único que hace coherente al
resto es arrancar con **taller y constructor de cada lado**: sin taller no hay de dónde
sacar unidades, y sin constructor no hay quien levante nada. Probé cuatro disposiciones,
todas simétricas por giro de 180 grados para que ninguna le dé ventaja posicional a nadie:

```
  esquinas        frentes         diagonal        centro          solo-taller
  T C . .         . T C .         T . . .         . . . .         T . . .
  . . . .         . . . .         . C . .         C T . .         . . . .
  . . . .         . . . .         . . c .         . . t c         . . . .
  . . c t         . c t .         . . . t         . . . .         . . . t
```

`solo-taller` es la lectura de que se arranca sólo con el taller y el primer turno se gasta
en desplegar al constructor. Da casi lo mismo que `esquinas`: 50,9 de reparto contra 50,8
al azar, y 49,9 contra 49,9 con los dos mirando 14 plies. Traba un poco menos (42,7% de
empates al azar contra 48,0%), pero no cambia ninguna conclusión.

Mayúscula es el primer jugador, minúscula el segundo. `T`=taller, `C`=constructor,
`Q`=cuartel, `I`=iglesia, `G`=guerrero, `S`=sacerdote, `*`=castillo.

**2. La regla de la obra.** Dijiste que el espacio vacío "también tiene que tener los
lugares adyacentes vacíos, o sea que no puede haber 2 edificios uno al lado del otro".
La primera mitad, leída al pie de la letra, hace la obra imposible siempre: el constructor
está parado justo al lado del sitio, así que ese vecino nunca está vacío. Me quedé con la
segunda mitad, que es la que da la razón: **la casilla de obra no puede tocar ningún
edificio**, propio o enemigo, pero sí puede tener unidades al lado. Si querías la
lectura estricta, avisá, aunque esa versión no deja construir nada.

**3. Qué pasa con el guerrero que toma un edificio.** "Meterse en un edificio" lo leí
literal, y encaja con que las unidades vivan adentro de los edificios: el edificio cambia
de bando y **el guerrero queda adentro, o sea fuera del tablero**, y vuelve a salir después
por el cuartel. Tomar un edificio te cuesta el guerrero por un rato, que es un precio
razonable. La otra lectura —el guerrero se queda en su casilla y el edificio cambia de
bando a distancia— es `--guerrero-queda`, y en las mediciones no cambia casi nada.

**4. Qué convierte el sacerdote.** Tu ejemplo es una unidad ("puedo convertir al guerrero
enemigo si no tengo un guerrero ya"), y el trabajo de dar vuelta edificios ya lo tiene el
guerrero, así que por defecto **el sacerdote sólo convierte unidades**. La versión que
también convierte edificios es `--sacerdote-edificios`.

**5. El castillo dorado.** Lo levanta el constructor en un sitio de obra válido a su lado,
y gana en el acto. Nunca es una pieza que esté en el tablero y se pueda disputar. Si la
idea era que el castillo estuviera puesto desde el principio como edificio neutral y se
tomara con el guerrero, eso es otro juego y no está implementado.

**6. Detalles chicos.** La derrota por eliminación se chequea todo el tiempo, no sólo al
principio del turno. La repetición es la misma posición tres veces. Hay un tope de 300
plies por las dudas, que casi nunca se toca.

---

## 3. La forma cruda del juego

Con los dos jugadores eligiendo al azar entre sus jugadas legales, 200.000 partidas desde
`esquinas`:

```
  gana el primero     26,63%
  gana el segundo     25,25%
  empate              48,11%
  reparto primero     50,69%    (empate = medio punto)
  plies               media 45,2   min 7   max 300
  jugadas por turno   3,7

  como termino:
    castillo          36,65%
    ahogado           15,24%
    repeticion        47,99%
    limite             0,12%
    sin constructor    0,00%
```

El factor de ramificación es **5,2 en la raíz y 3,7 de media** durante la partida. Es un
juego chico: hasta 8 plies el árbol entero son 631.443 nodos. Comparado con el TaTeTi con
Esteroides, donde había 20 y pico de jugadas por turno, acá tenés menos de cuatro opciones
por turno, y eso ya adelanta que va a haber muchas posiciones trabadas.

El **15% de ahogados** al azar es alto y no es ruido: en un tablero de 16 casillas con
hasta 13 piezas encima, quedarse sin ninguna jugada legal es fácil.

---

## 4. Por qué no se puede ganar: el constructor se tapia solo

Este es el hallazgo central y es puramente geométrico.

Para construir, el constructor tiene que estar **al lado** del sitio. O sea que cada
edificio que levanta **se come uno de sus propios vecinos**. Y como dos edificios no pueden
tocarse, cada edificio nuevo además inutiliza las casillas alrededor.

Una casilla del borde tiene 3 vecinos. Una esquina, 2. Sólo las cuatro del centro tienen 4.
Entonces un constructor parado en el borde que levanta sus tres edificios **se queda
literalmente sin ninguna casilla libre al lado**: ni puede moverse, ni puede coronar.

Esta es una posición real, el final de una partida a 2 plies (semilla 7):

```
    T C I .      El primero tiene los tres edificios: taller a4, iglesia c4, cuartel b3.
    . Q S g      Su constructor esta en b4. Los tres vecinos de b4 son a4, c4 y b3,
    G s q .      o sea sus tres propios edificios. No tiene donde poner el castillo
    . i c t      y tampoco se puede mover. Gano la carrera y perdio la partida.
```

Medido sobre el total de turnos jugados:

| Los dos miran | Turnos con el constructor tapiado | Turnos con los 3 edificios y sin dónde coronar |
|---|---|---|
| al azar | 42,9% | 56,3% |
| 2 plies | 87,1% | 87,1% |
| 4 plies | 81,5% | 76,4% |
| 6 plies | 76,5% | 76,9% |
| 8 plies | 41,7% | 52,7% |
| 10 plies | 14,0% | 16,9% |
| 12 plies | 9,4% | 10,6% |
| 14 plies | 6,0% | 14,9% |

Y acá está el punto: **la cifra baja no porque el problema se resuelva, sino porque los
jugadores buenos aprenden a no construir**. A 14 plies el constructor casi nunca está
tapiado, y también casi nunca hay tercer edificio. Son dos maneras distintas de empatar:

- **Jugadores flojos** construyen todo, se tapian, y arrastran los pies hasta la repetición.
- **Jugadores buenos** ven la trampa, se frenan en dos edificios, y maniobran para siempre.
  Por eso las partidas a 14 plies duran 91 plies en vez de 31.

La condición de victoria se sabotea a sí misma: **ganar la carrera de edificios es lo que
te impide ganar la partida.**

### La cuenta exacta

Se puede decir bastante más fuerte que "está apretado". Conté por fuerza bruta cuántas
maneras hay de acomodar edificios en un tablero sin que dos queden pegados:

| | 4x4 | 5x5 |
|---|---|---|
| máximo de edificios que entran sin tocarse | **8** | 13 |
| formas de acomodar los 6 edificios de los dos jugadores | 114 | — |
| formas de acomodar esos 6 **más el castillo** | **20** | 12.798 |

De las 11.440 maneras de elegir 7 casillas entre 16, sólo **20** son legales. Y 16 de esas
20 son "un damero al que le falta una casilla": el 4x4 tiene exactamente dos dameros y no
existe ningún otro conjunto de 8 casillas sueltas.

Peor: esas disposiciones son casi la posición de bloqueo total. Las casillas de un color
sólo tienen vecinos del otro color, así que con 7 de las 8 casillas de un damero ocupadas
queda **una sola** casilla libre de ese color, y de las 8 casillas del otro color sólo entre
**2 y 4** están pegadas a ella. Todas las demás piezas están congeladas.

Ahí está el juego entero: hay que navegar hasta una de 20 posiciones, con el constructor
parado al lado de cada edificio en el momento exacto en que lo levanta, y sin que el paso
por esa posición congele a todo el mundo. No es que el 4x4 esté chico: está **justo en el
borde** de lo que la regla del no-pegado permite. Un 5x5 tiene 640 veces más finales
legales.

Una prueba de que la regla del no-pegado es exactamente la que aguanta todo esto: si la
sacás (`--obra-libre`), el primer jugador construye taller, cuartel, iglesia y castillo en
sus primeros cuatro turnos y **gana por la fuerza en 7 plies** —en 5 desde `diagonal`,
donde el constructor arranca con las cuatro casillas libres alrededor—, verificado con
búsqueda exacta. Esa regla no es un detalle de sabor, es lo único que evita que el juego se termine
antes de empezar. El problema es que hoy pasa de largo y también evita que se termine
alguna vez.

---

## 5. La regla de eliminación está muerta

**Cero partidas de 2.000.000** terminaron con alguien perdiendo por quedarse sin
constructor y sin taller. Ni al azar ni con búsqueda, en ninguna disposición, con ninguna
variante de regla activada.

Y no es mala suerte: es imposible, y se demuestra en tres renglones.

Para perder hace falta quedarse sin constructor **y** sin taller. El constructor vuelve a
salir del taller al turno siguiente de morir, gratis, así que la única derrota real pasa
por perder el taller. Ahora bien:

1. Al taller sólo se le puede entrar con el guerrero, y **sólo podés entrar a un edificio
   que no tenés**.
2. Los dos arrancan con taller.
3. Ninguna otra jugada saca un edificio del tablero: los edificios sólo cambian de dueño.

O sea que nadie carece nunca de taller, así que nadie puede entrar al del otro, así que
nadie pierde nunca el suyo. **El taller es inmortal por inducción desde la primera jugada**,
y la derrota por eliminación es inalcanzable, no rara.

La medición coincide con la prueba: en 200.000 partidas al azar hubo **49.120 tomas de
edificio y ninguna fue de un taller**. El selftest lo comprueba en las dos direcciones (con
taller propio no se puede entrar, sin taller propio sí) y barre los primeros 8 plies desde
la posición inicial verificando que nadie pierde nunca el suyo.

El guerrero igual se usa: hay al menos una matanza en el 64% de las partidas al azar y en
el 86% a 8 plies. Lo que no existe es la **victoria** por eliminación. Matar sirve para
ganar tiempo, no para ganar.

Si querés que esa vía exista hay que romper el candado por algún lado: que el guerrero
pueda entrar a cualquier edificio aunque ya tenga uno de ese tipo, que los edificios se
puedan demoler, o que arranque sin taller alguno. Y aparte hay que encarecer el
redespliegue, porque si no matar al constructor sigue deshaciéndose en un turno.

### La única llave que abre el candado

`--toma-libre` (sección 9.1): si se puede entrar a un edificio del tipo que ya tenés, el
paso 3 de la inducción se cae, el taller se vuelve tomable y la regla revive. Medido: 67.229
tomas de taller en 100.000 partidas al azar, contra cero en el juego base, y **el 7,5% de
las partidas terminan por eliminación**, un final que hasta ahora nunca había aparecido.

---

## 6. Las palancas que probé

Cinco lecturas de regla, cuatro disposiciones, 50.000 partidas al azar y 4.000 a 8 plies
por fila. La tabla completa sale de `castillo comparar`. Lo que importa:

| Variante | Qué hace | Efecto medido |
|---|---|---|
| base | — | empate 48% al azar, 54-100% con búsqueda |
| `--sacerdote-edificios` | el sacerdote también da vuelta edificios | casi nada al azar (+2% de castillos) |
| `--guerrero-queda` | el guerrero no entra al edificio que toma | casi nada, menos de 1% en todo |
| `--castillo-libre` | el no-pegado no vale para el castillo | empates de 48% a 29%; **rompe `centro`: gana el primero forzado en 7 plies** |
| `--obra-libre` | se cae el no-pegado entero | **el primero gana forzado: 5 plies desde `diagonal`, 7 desde las otras tres** |
| `--toma-libre` | el guerrero entra a cualquier edificio, tenga o no uno igual | empates de 48% a 35% y **revive la eliminación (7,5%)**; con búsqueda cicla en 10 plies (9.1) |
| `--castillo-claim` | el castillo se reclama metiéndose adentro, no se gana levantándolo | empates de 48% a **16%** al azar; con búsqueda nadie lo construye (9.2) |
| `--sacerdote-reubica` | el sacerdote muda su propia pieza en vez de duplicarla | el sacerdote pasa de jugarse en el 15% de las partidas al **66%** (9.3) |

Las tres últimas son las que propusiste después y están desarrolladas en la sección 9.
Las dos primeras que ya tenías en mente (sacerdote y guerrero) no mueven la aguja.
Las dos que tocan la geometría la mueven demasiado: pasan de "nunca se puede ganar" a "el
primero gana antes de que el segundo llegue a jugar tres veces". El punto justo está en el medio y
no lo encontré con estas dos.

Las disposiciones iniciales sí cambian bastante el carácter, con reglas base a 12 plies:

| Inicio | Empate | Reparto primero | Castillos |
|---|---|---|---|
| esquinas | 87,7% | 54,0 | 11,9% |
| frentes | 49,8% | 57,3 | 46,3% |
| diagonal | **99,8%** | 50,1 | 0,2% |
| centro | 66,9% | 53,5 | 25,1% |

`frentes` es la menos trabada de las cuatro y `diagonal` es directamente un empate. Si hay
que elegir una para seguir probando, es `frentes`.

---

## 7. El balance, y los baches del medio

Reparto para el primer jugador con los dos mirando la misma cantidad de plies, empate
contado como medio punto, 800 partidas por celda:

| Los dos miran | esquinas | frentes | diagonal | centro |
|---|---|---|---|---|
| 2 plies | 50,0 | 53,9 | 48,8 | 56,2 |
| 4 plies | 48,3 | 49,6 | 49,8 | 53,2 |
| 6 plies | 52,8 | 50,7 | **64,0** | 53,9 |
| 8 plies | 48,8 | **24,3** | 50,5 | 45,1 |
| 10 plies | 49,6 | **23,6** | 50,0 | **33,3** |
| 12 plies | 54,1 | 56,7 | 50,0 | 53,1 |
| 14 plies | 49,9 | 50,3 | 49,8 | 48,9 |

En los extremos el juego es parejo: al azar da 50,7 y a 14 plies queda entre 48,9 y 50,3
en las cuatro disposiciones. Pero en el medio hay tres baches grandes. `frentes` a 8 y 10
plies le da **24 al primero**, o sea que el segundo gana tres de cada cuatro. `centro` a 10
plies le da 33. `diagonal` a 6 se va para el otro lado y le da 64.

Estos baches no son propiedades del árbol —la búsqueda exacta dice que nadie gana por la
fuerza en 27 plies, y a 14 todo vuelve a 50—, son **niveles de juego en los que un bando
se equivoca sistemáticamente**. Para un juego de mesa eso no es un detalle técnico: es
exactamente la forma que tiene "en mi grupo el segundo siempre gana" de aparecer en la
mesa. Si lo probás con gente y sale un sesgo fuerte para un lado, mirá primero acá antes
de tocar las reglas, porque puede irse solo cuando los dos juegan mejor.

## 8. Lo que sí funciona

Vale decirlo porque es la parte que no hay que romper al arreglar el resto:

- **No hay una apertura ganada.** Ni el primero ni el segundo fuerzan nada en 27 plies.
- **La habilidad se paga sola.** 99-100% para el que ve más lejos, de los dos lados. Las
  jugadas tienen consecuencia; el jugador que mira dos movidas adelante no está adivinando.
- **Las piezas se usan.** Matanzas en el 64-86% de las partidas, tomas de edificio en el
  10-22%, conversiones en el 4-15%. Ninguna pieza es decorativa salvo por el problema de
  la sección 5.

El juego tiene todo menos una forma de terminar.

---

## 9. El banco de variantes

Tres de las ideas nuevas están implementadas, medidas y con tests. El resto están anotadas
con lo que habría que decidir antes de poder medirlas.

Las tres, con `esquinas` y 100.000 partidas al azar:

| | base | `--toma-libre` | `--castillo-claim` | `--sacerdote-reubica` |
|---|---|---|---|---|
| empate | 48,0% | **35,4%** | **15,9%** | 46,1% |
| gana por eliminación | 0,0% | **7,5%** | 0,0% | 0,0% |
| partidas con alguna toma | 16,1% | **77,4%** | 18,6% | 16,4% |
| partidas con alguna conversión | 15,2% | 20,1% | 11,5% | **65,7%** |
| ahogados | 15,3% | 32,9% | 15,8% | **9,6%** |
| duración | 45 plies | 48 | 26 | 51 |
| reparto del primero | 50,76 | 51,16 | 50,48 | 50,88 |

Ninguna de las tres le da victoria forzada a nadie en 21 plies de búsqueda exacta.

### 9.1 La toma libre — `--toma-libre`

Esta no la propusiste como regla sino como queja: *"el guerrero sólo captura iglesias
básicamente"*. Tenías razón, y la causa es la restricción de no poder entrar a un edificio
del tipo que ya tenés. Sacarla es **una línea** del generador de jugadas. Podés terminar con
dos edificios iguales; el segundo no suma para ganar —la condición cuenta tipos, no piezas—
pero se lo negás al otro.

**Es la palanca más grande de todas las que probé**, y hace dos cosas que ninguna otra hace:

- **Le da trabajo al guerrero.** De jugarse en el 16% de las partidas pasa al **77%**.
- **Revive la regla de eliminación de la sección 5.** El candado del taller se abre: 67.229
  tomas de taller en 100.000 partidas contra cero, y por primera vez aparece un final que
  nunca había aparecido: **el 7,5% de las partidas las gana alguien por dejar al otro sin
  constructor y sin taller.**

**Pero con búsqueda se rompe, y se rompe feo.** Esta es la partida entera a 8 plies:

```
 1. B construye cuartel b3     5. B guerrero c3 toma cuartel c2     9. B guerrero c3 toma cuartel c2
 2. n construye cuartel c2     6. n guerrero b2 toma cuartel c2    10. n guerrero b2 toma cuartel c2
 3. B despliega guerrero c3    7. B despliega guerrero c3              empate por repeticion
 4. n despliega guerrero b2    8. n despliega guerrero b2
```

Los dos se pasan el mismo cuartel de mano en mano para siempre. **El ciclo existe porque el
edificio es el punto de aparición de la unidad que lo recaptura**: el guerrero entra al
cuartel y sale del tablero, y el cuartel recién perdido es justo de donde vuelve a salir el
guerrero del otro. Tomar no cuesta nada y se deshace en una jugada. A 8, 10 y 12 plies las
300 partidas duran exactamente 10 plies y terminan las 300 en empate.

Con `--guerrero-queda` el ciclo cambia de forma pero no desaparece: 6,86 tomas por partida y
86-100% de empates. El problema no es dónde queda el guerrero, es que **la toma es
reversible**.

### 9.2 El castillo que hay que tomar — `--castillo-claim`

Tal cual lo escribiste: se levanta, cualquier unidad puede meterse adentro, estar adentro le
hace el reclamo, y gana el que tenga los tres edificios **y** el reclamo. La unidad que entra
sale del tablero, igual que el guerrero cuando toma un edificio. Como es una conjunción que
se chequea todos los turnos, el rival la puede romper de dos maneras: tomándote un edificio o
metiéndose él al castillo para robarte el reclamo. Las dos están cubiertas por tests.

**Lo que tuve que decidir**: que el castillo se pueda levantar **en cualquier momento**, sin
tener los tres edificios. Con el requisito puesto el castillo casi nunca llegaba a existir y
la regla no se probaba nunca.

Al azar es otro juego: los empates caen de 48,0% a **15,9%**, el 68% de las partidas
terminan en castillo contra el 37% de antes, y duran 26 plies en vez de 45.

**Con los dos jugando bien vuelve a empatar, y el motivo es nuevo:**

| Los dos miran | Empate | Obras por partida | Entradas al castillo |
|---|---|---|---|
| al azar | 15,9% | 3,01 | 1,49 |
| 8 plies | 89,7% | 3,54 | 0,53 |
| 10 plies | 67,7% | 3,26 | 0,75 |
| 12 plies | **98,5%** | 2,02 | **0,05** |
| 14 plies | **99,0%** | 2,12 | **0,05** |

A 12 plies se levantan **menos** edificios que en el juego base (2,02 contra 2,46) y el
castillo se reclama 5 veces cada 100 partidas. O sea: **nadie construye el castillo.**
Hacerlo disputable les dio a los dos un motivo para no abrirlo, porque el que lo levanta
pone la casilla ganadora al alcance del rival tanto como al suyo. Es un equilibrio de
"nadie abre la caja".

### 9.3 El sacerdote que reubica — `--sacerdote-reubica`

Tal cual lo escribiste: convierte aunque ya tengas esa pieza, y en vez de aparecer una
segunda, la que ya tenías **se muda** a esa casilla.

**Arregla la pieza, y bien.** El sacerdote pasa de jugarse en el 15% de las partidas al
**66%**; las conversiones, de 0,20 a 2,22 por partida.

Y hay una razón estructural para preferirla que va más allá de darle un uso: **es la única
jugada del juego que mueve una pieza sin que camine.** Es el único antídoto contra el tapiado
de la sección 4, y hay un test que lo muestra: constructor con sus tres edificios encima y
cero sitios de obra, el sacerdote toca al constructor enemigo, y el constructor propio
reaparece en una casilla desde donde el castillo vuelve a ser posible. Medido: los ahogados
caen de 15,3% a 9,6%.

**Lo que no arregla es el empate**: a 14 plies sigue en 98,5%. La enfermedad de la sección 4
no es que las piezas no se puedan mover, es que la carrera de edificios se sabotea sola.

### 9.4 Lo que las tres juntas dejan claro

Las tres mejoran el juego al azar y las tres terminan empatando con búsqueda, siempre por el
mismo motivo de fondo. **En este juego casi nada es irreversible:**

- Un edificio tomado se vuelve a tomar. Nunca sale del tablero, sólo cambia de dueño.
- Una unidad muerta vuelve a salir de su edificio al turno siguiente.
- Una conversión se deshace con otra conversión.
- El reclamo del castillo se roba metiéndose adentro.

El único recurso monótono del juego es **construir**: los edificios sólo se agregan, nunca se
sacan. Y ahí está la trampa completa, en una línea:

> **El único recurso irreversible del juego es el que te mata cuando lo gastás.**

Construir el tercer edificio te tapia el constructor y te saca el castillo. Por eso todo
jugador que mira ocho jugadas adelante llega a la misma conclusión —no construyas el
tercero— y por eso todas las variantes terminan en el mismo lugar: ninguna toca esa
asimetría, sólo cambian lo que pasa alrededor.

Y por eso, de todo lo que propusiste, **lo de los escombros es lo único que ataca el problema
de raíz**, aunque no sea por el motivo que parece.

### 9.5 Demoler desde adentro, y los escombros (sin implementar)

Tu intuición de la asimetría es buena: **construir es fácil porque se hace desde afuera,
demoler es caro porque hay que meterse adentro.** Y acertaste solo el peligro: sacar
edificios del tablero alargaría el juego y podría dejarlo indeterminado. Los números de 9.1
son exactamente eso pasando —`--toma-libre` es "sacarle el edificio al otro" sin sacarlo del
tablero, y ya cicla en 10 plies.

**El escombro arregla justo eso.** Si demoler deja una marca que no se levanta, la cantidad
de casillas muertas **sólo puede subir**. Un juego donde algo sólo sube no puede ciclar para
siempre: hay un tope duro y se llega. Es la primera mecánica propuesta que agrega un segundo
recurso monótono, y a diferencia de construir, éste no te tapia solo —o te tapia si lo dejás
mal puesto, que es exactamente la clase de decisión que uno quiere que haya.

**Pero en un 4x4 gasta justo lo que no sobra.** La cuenta de la sección 4: hay **20** maneras
legales de acomodar los 7 edificios y todas usan 7 de las 8 casillas de un damero. Cada
escombro que caiga en el color equivocado borra finales legales de a montones. Mi apuesta
—apuesta, no medición— es que en un 4x4 los escombros cambian el empate por repetición por un
empate por ahogo, que es peor: más largo y más frustrante. En un 5x5, con 12.798 finales
legales, sería otra cosa.

**Lo que hay que decidir antes de medirlo:**

1. **¿Quién demuele?** El constructor metiéndose adentro es lo más limpio: le da un segundo
   trabajo a una pieza que ya existe en vez de agregar una cuarta, y conserva la simetría
   linda de construir-afuera / demoler-adentro. Un demoledor nuevo es una unidad más para
   desplegar y un cuarto edificio para construirle; en 16 casillas eso es carísimo.
2. **¿Se puede demoler lo propio?** Yo diría que sí, y que es el uso más interesante: es cómo
   te destapiás el constructor. Si sólo se demuele lo ajeno, la regla es puramente agresiva y
   el tapiado sigue sin salida.
3. **¿El que demuele queda adentro (o sea, afuera del tablero) o sobre el escombro?** Si
   queda afuera y el edificio era tuyo, acabás de perder el punto de aparición del que iba a
   salir. Ojo con eso: es una manera nueva de suicidarse.
4. **¿El escombro estorba sólo su casilla o también bloquea construir al lado?** Yo
   arrancaría con **sólo su casilla**. Si además bloquea la adyacencia, con tres o cuatro
   escombros el tablero se cierra entero.
5. **¿Se pueden sacar los escombros?** Si se pueden, se cae la propiedad de que sólo suben,
   que es lo único que garantiza que la partida termine. Yo los dejaría para siempre.

**Costo de implementarlo**: real pero acotado. La codificación de 4 bits por casilla quedó
**exactamente llena** con la variante del castillo reclamado (0 vacío, 1-12 piezas, 13-15
castillo). Un escombro necesita un código decimosexto que ya no existe, así que habría que
llevar una máscara de 16 bits aparte. Eso rompe el truco de que la posición sea su propia
clave de tabla de transposición y obliga a hashear. Un rato de trabajo, no un rediseño.

### 9.6 El castillo neutral desde el arranque (idea mía, sin implementar)

Ésta la agrego yo, y sale de tu primer mensaje: dijiste *"hay una pieza extra que no es de
ningún jugador"*, y yo la implementé como algo que se construye. Si en cambio **el castillo
ya está en el tablero desde el arranque**, pasan tres cosas buenas de golpe:

- El tapiado deja de bloquear la victoria: no hace falta una casilla libre al final.
- El tablero tiene un centro, un lugar por el que pelear desde la jugada 1.
- Bajan de 7 a 6 los edificios que hay que acomodar: **de 20 disposiciones legales a 114**.

Con `--castillo-claim` encima es directamente un rey de la colina, con la carrera de
edificios como lo que te habilita a ganarlo.

**Hay un problema geométrico, y tiene solución.** En un 4x4 **ninguna casilla queda fija al
girar el tablero 180 grados** (`c → 15-c` necesitaría `2c = 15`), así que un castillo neutral
solo no se puede poner de forma justa con las cuatro disposiciones actuales, que son todas
simétricas por giro. Pero la **transposición** por la diagonal principal sí deja cuatro
casillas fijas: a4, b3, c2 y d1. Alcanza con espejar el arranque por esa diagonal en vez de
girarlo:

```
    . . c t      Blanco: taller a1, constructor a2
    . * . .      Negro:  taller d4, constructor c4
    C . . .      Castillo: b3, que es su propio espejo por la diagonal
    T . . .      Ninguna pieza arranca pegada al castillo.
```

Un 5x5 no tiene este problema: la casilla del medio es fija en las 8 simetrías.

### 9.7 Las que ya estaban anotadas

1. **Que el castillo se pueda levantar a distancia 2 del constructor**, o en cualquier sitio
   de obra válido del tablero. Ataca el cuello de botella —el constructor tapiado— sin tocar
   la carrera de edificios. Sigue siendo la palanca más quirúrgica de todas.
2. **Que construir mueva al constructor a la casilla del edificio nuevo**, o que lo empuje
   para atrás, para que no se auto-encierre.
3. **Bajar el tercer edificio a dos**: ganás con dos edificios propios más el castillo.
4. **Tablero de 5x5**, por la cuenta de la sección 4. **Ojo**: el tablero entra en un `ulong`
   de 64 bits justo porque son 16 casillas de 4 bits, y 25 casillas no entran.

Lo que **no** recomiendo sigue siendo sacar el no-pegado: es lo único que evita que el primer
jugador gane forzado en 5 o 7 plies.

### 9.8 Si tuviera que elegir dos

`--toma-libre` **más** algo que haga la toma irreversible. La toma libre resuelve dos
problemas de un saque (el guerrero sin trabajo y la regla de eliminación muerta) y su único
defecto es el ciclo de 9.1, que existe porque tomar se deshace gratis. Los escombros son
exactamente lo que lo haría no deshacerse. Son las dos mitades de la misma idea.

---

## 10. Ver una partida

Hay dos partidas completas anotadas jugada por jugada, con el tablero paso a paso, acá:

**https://claude.ai/code/artifact/271c812f-4da6-491a-9c3a-d758f4827622**

- *La que se gana*: 19 plies, gana el primero con el castillo. Tiene una toma de edificio,
  una muerte del constructor con su reaparición inmediata al turno siguiente, y una jugada
  ganadora que no es un ataque sino correr al guerrero para desocupar la única casilla donde
  entraba el castillo.
- *La que se traba*: 18 plies, empate. El primero se sella el constructor con su propio
  tercer edificio en la jugada 5, y los trece plies que siguen no cambian nada.

Las dos salen de `castillo partida --json`, así que las posiciones son exactamente las que
calcula el motor.

---

## 11. Cómo correrlo

```bash
cd CastilloDorado/solver
dotnet build -c Release
./bin/Release/net9.0/castillo.exe <comando>
```

| Comando | Qué hace |
|---|---|
| `selftest` | 80 tests, uno por regla. Si cambiás una regla, cambiá el test que la nombra. |
| `perft --prof 8` | Árbol completo hasta esa profundidad. Da el factor de ramificación. |
| `azar --partidas 200000` | Los dos al azar. La forma cruda del juego. |
| `practica --plies 12 --partidas 1200` | Los dos miran N plies con evaluación de material. |
| `practica --plies 12 --plies-negro 2` | Duelo desparejo: mide si la habilidad decide. |
| `resolver --max-prof 27` | Busca victorias forzadas. Un veredicto de victoria es real. |
| `partida --plies 4 --semilla 7` | Una sola partida, dibujada jugada por jugada. |
| `comparar` | La tabla de 4 disposiciones x 5 variantes. |

Las tres banderas de la sección 9 se combinan entre sí y con todo lo demás. Por ejemplo,
la partida degenerada de 9.1 se reproduce con:

```bash
./bin/Release/net9.0/castillo.exe partida --plies 8 --semilla 5 --toma-libre
```

Banderas de regla, válidas en cualquier comando:

```
--inicio esquinas|frentes|diagonal|centro|solo-taller
--sacerdote-edificios     el sacerdote tambien convierte edificios
--guerrero-queda          el guerrero no entra al edificio que toma
--obra-libre              se cae la regla del no-pegado
--castillo-libre          el no-pegado vale para los tres edificios pero no para el castillo
--toma-libre              el guerrero entra a cualquier edificio, tenga o no uno igual
--castillo-claim          el castillo se reclama metiendose adentro, no se gana levantandolo
--sacerdote-reubica       el sacerdote muda su propia pieza en vez de duplicarla
--repeticiones 3          cuantas repeticiones son empate
--plies-max 300           tope de plies
```

### Qué hay en cada archivo

| Archivo | Qué tiene |
|---|---|
| `Juego.cs` | Codificación, generación de jugadas, aplicación, finales, simetrías, disposiciones. |
| `Busqueda.cs` | Alfa-beta con tabla de transposición. Dos modos: práctico (evalúa) y exacto (sólo prueba victorias). |
| `Partida.cs` | Corre partidas enteras, cuenta repeticiones, acumula el diagnóstico. |
| `SelfTest.cs` | Los 80 tests de reglas más cuatro barridos exhaustivos de 6 plies. |
| `Program.cs` | La línea de comandos. |

### Cómo está guardada una posición

Un `ulong`: 16 casillas de 4 bits, índice = `fila*4 + columna`. Cada casilla guarda un
código de pieza (0 vacía, 1-6 del primero, 7-12 del segundo, 13 el castillo sin reclamar,
14 y 15 el castillo reclamado por uno o por el otro). Los 16 códigos están usados: agregar
una pieza nueva —un escombro, por ejemplo— ya no entra, ver la sección 9.5.

Las unidades fuera del tablero **no se guardan en ningún lado**. Como cada jugador tiene
exactamente una de cada tipo, "está afuera" es lo mismo que "no aparece en el tablero", y
una unidad muerta y una que todavía no salió de su edificio son el mismo estado. Eso hace
que toda la posición entren en 64 bits y que la tabla de transposición use la posición
misma como clave, sin hash.

Las ocho simetrías del cuadrado se usan para canonicalizar en la tabla de transposición,
pero **no** para la repetición: si la posición actual es la rotación de una anterior, las
piezas de verdad se movieron y eso no es una repetición.

### Qué garantiza el selftest

Cada test cita una frase de las reglas. Además hay cuatro barridos exhaustivos de 6 plies
—uno por juego de reglas: base, `--toma-libre`, `--castillo-claim` y `--sacerdote-reubica`,
143.000 nodos entre todos— que comprueban que ninguna jugada rompa las invariantes: nadie
junta dos piezas del mismo tipo (salvo edificios con `--toma-libre`, que es el punto de esa
regla), nunca hay más de un castillo, el castillo no se levanta sin los tres edificios
cuando la regla lo pide, y ninguna obra nueva queda pegada a otro edificio.

Dos barridos más miran una cosa cada uno: que en 8 plies desde el arranque nadie pierda
nunca su taller (la prueba de la sección 5), y que con `--toma-libre` sí exista una línea
donde lo pierde (la sección 9.1).

Lo que el selftest **no** garantiza es que las reglas sean las que vos tenías en la cabeza.
Eso es la sección 2.
