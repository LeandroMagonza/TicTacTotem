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

Las dos variantes que vos ya tenías en mente (sacerdote y guerrero) no mueven la aguja.
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

## 9. Qué probaría después

En orden de cuánto cambian el juego, de menos a más:

1. **Que el castillo se pueda levantar a distancia 2 del constructor**, o en cualquier
   sitio de obra válido del tablero. Ataca exactamente el cuello de botella —el constructor
   tapiado— sin tocar la carrera de edificios. Es la palanca más quirúrgica de todas.
2. **Que construir mueva al constructor a la casilla del edificio nuevo**, o que lo empuje
   para atrás. Así no se auto-encierra.
3. **Bajar el tercer edificio a dos**: ganás con dos edificios propios más el castillo.
   Menos piezas en el tablero, más aire.
4. **Tablero de 5x5.** Es lo que más limpio lo dejaría y lo digo con números: para meter 7
   edificios que no se toquen hacen falta 7 casillas de un conjunto independiente. En un
   4x4 el máximo es 8 (el damero), o sea que entran justo, sin margen. En un 5x5 el máximo
   es 13. El 4x4 no está apretado, está exactamente en el límite. **Ojo que esto no está
   implementado**: el tablero entra en un `ulong` de 64 bits justo porque son 16 casillas
   de 4 bits, y 25 casillas no entran.
5. **Que el castillo haya que tomarlo, no sólo levantarlo.** Idea tuya, anotada acá para no
   perderla: se construye el castillo, y ganás si al turno siguiente entrás en él, y sólo
   podés entrar teniendo los otros tres edificios. Le da al rival una ventana de un turno
   para romper la jugada, y de paso le devuelve trabajo al sacerdote y al guerrero, que hoy
   no tienen forma de tocar la condición de victoria. **Sin implementar**: antes de medirla
   hay que decidir quién entra (¿cualquier unidad?, ¿sólo el constructor?) y qué pasa si le
   toman un edificio con el castillo ya puesto.
6. **Arreglar el candado del taller y el redespliegue** para que la eliminación exista
   (sección 5).

Lo que **no** recomiendo es sacar el no-pegado: es lo único que evita que el primer jugador
gane forzado en 5 o 7 plies.

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
| `selftest` | 45 tests, uno por regla. Si cambiás una regla, cambiá el test que la nombra. |
| `perft --prof 8` | Árbol completo hasta esa profundidad. Da el factor de ramificación. |
| `azar --partidas 200000` | Los dos al azar. La forma cruda del juego. |
| `practica --plies 12 --partidas 1200` | Los dos miran N plies con evaluación de material. |
| `practica --plies 12 --plies-negro 2` | Duelo desparejo: mide si la habilidad decide. |
| `resolver --max-prof 27` | Busca victorias forzadas. Un veredicto de victoria es real. |
| `partida --plies 4 --semilla 7` | Una sola partida, dibujada jugada por jugada. |
| `comparar` | La tabla de 4 disposiciones x 5 variantes. |

Banderas de regla, válidas en cualquier comando:

```
--inicio esquinas|frentes|diagonal|centro|solo-taller
--sacerdote-edificios     el sacerdote tambien convierte edificios
--guerrero-queda          el guerrero no entra al edificio que toma
--obra-libre              se cae la regla del no-pegado
--castillo-libre          el no-pegado vale para los tres edificios pero no para el castillo
--repeticiones 3          cuantas repeticiones son empate
--plies-max 300           tope de plies
```

### Qué hay en cada archivo

| Archivo | Qué tiene |
|---|---|
| `Juego.cs` | Codificación, generación de jugadas, aplicación, finales, simetrías, disposiciones. |
| `Busqueda.cs` | Alfa-beta con tabla de transposición. Dos modos: práctico (evalúa) y exacto (sólo prueba victorias). |
| `Partida.cs` | Corre partidas enteras, cuenta repeticiones, acumula el diagnóstico. |
| `SelfTest.cs` | Los 45 tests de reglas más un barrido exhaustivo de 6 plies. |
| `Program.cs` | La línea de comandos. |

### Cómo está guardada una posición

Un `ulong`: 16 casillas de 4 bits, índice = `fila*4 + columna`. Cada casilla guarda un
código de pieza (0 vacía, 1-6 del primero, 7-12 del segundo, 13 el castillo).

Las unidades fuera del tablero **no se guardan en ningún lado**. Como cada jugador tiene
exactamente una de cada tipo, "está afuera" es lo mismo que "no aparece en el tablero", y
una unidad muerta y una que todavía no salió de su edificio son el mismo estado. Eso hace
que toda la posición entren en 64 bits y que la tabla de transposición use la posición
misma como clave, sin hash.

Las ocho simetrías del cuadrado se usan para canonicalizar en la tabla de transposición,
pero **no** para la repetición: si la posición actual es la rotación de una anterior, las
piezas de verdad se movieron y eso no es una repetición.

### Qué garantiza el selftest

Cada test cita una frase de las reglas. Además del barrido exhaustivo de 6 plies (28.408
nodos) que comprueba que ninguna jugada rompa las invariantes: nadie tiene dos piezas del
mismo tipo, no aparecen códigos inventados, el castillo no se levanta sin los tres
edificios, y ninguna obra nueva queda pegada a otro edificio.

Lo que el selftest **no** garantiza es que las reglas sean las que vos tenías en la cabeza.
Eso es la sección 2.
