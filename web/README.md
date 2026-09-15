# TicTacTotem — juego web

Implementación jugable del juego, en Three.js. Contra la máquina en cinco
niveles, dos jugadores en la misma pantalla, o bot contra bot para mirar.

La configuración es la que salió del análisis de `../Simulacion/` (sección 10):
**Tótem A `12344` arranca, Tótem B `12355` juega segundo, y nadie coloca una
pieza de la mano en el centro.**

```bash
npm install
npm run dev        # servidor de desarrollo; entra desde el celular por la LAN
npm test           # 85 tests, ~30 s, sin browser ni GPU
npm run build      # a dist/
```

---

## 1. Las reglas, en un párrafo

Tablero de 3×3, cada casilla es una **pila**. En tu turno, o colocás una pieza de
tu mano en una casilla **vacía que no sea el centro**, o movés tu pieza
**destapada** a una casilla ortogonalmente adyacente que esté vacía o cuya pieza
visible tenga nivel **estrictamente menor**. Gana quien deje tres piezas
destapadas propias en línea.

Tres reglas más, que la primera vez se leen como bug:

- **Al centro no se coloca desde la mano; sólo se llega moviendo.** Sin esto el
  primero abre 4 al centro y gana dos de cada tres partidas a nivel humano, porque
  al segundo le queda una única respuesta que nadie ve. Con la regla, y con el set
  `12355` para B, ninguna apertura pasa de 54 % y el segundo siempre tiene varias
  respuestas que aguantan (`../Simulacion/README.md`, sección 10).
- **Si tu jugada destapa una línea del rival, perdés** — aunque la línea no sea
  tuya. Es la regla del medio punto: el que acaba de mover pierde los empates.
  El juego te avisa antes con un **anillo ámbar** sobre ese destino.
- **Si te toca mover y no tenés jugada legal, perdés.** Pasa cuando el tablero se
  llena: son 10 piezas para 9 casillas, así que alguien se queda con una en la
  mano.

## 2. Estructura

```
src/engine/     motor puro: sin DOM, sin Three.js, sin dependencias.
                Corre igual en Node (tests) y en un Web Worker (juego).
  spec.js       makeSpec(): tabla de piezas, grupos, adyacencia, simetrías
  position.js   posición empaquetada, loc/withLoc/applyMove
  rules.js      computeTops / winnerAfter / generateMoves / canonical
  searcher.js   alpha-beta con tabla de transposición
  ai.js         chooseMove(): el jugador de visión limitada + la escalera
  match.js      partida: historia, repetición, ahogado, undo
  worker.js     el motor en un hilo aparte

src/scene/      Three.js. geometry.js es el ÚNICO lugar que convierte índices
                de casilla en posiciones del mundo.
src/app/        estado, entrada, HUD, orquestación
test/           node --test; ver §6
tools/          export_models.py, para Blender
```

## 3. La escalera de dificultad

No está inventada: está **medida**. Visión simétrica, 4000 partidas por celda en
C# y 2000 en JS.

| ve | JS | C# | `README` de Simulación |
|---|---|---|---|
| 2 | 52,8 / 46,5 | 54,5 / 44,8 | 54,5 / 45,5 |
| 4 | 51,8 / 47,1 | 51,6 / 47,5 | 51,7 / 48,3 |
| 6 | 37,6 / 60,5 | 39,0 / 59,5 | 38,8 / 61,2 |
| 8 | 22,3 / 76,3 | 20,1 / 78,4 | — |

Todo dentro del intervalo de confianza. Eso es la prueba de punta a punta de que
**el juego que se juega es el juego que se analizó**.

| Nivel | visión | ε (jugada al azar) | ms por jugada |
|---|---|---|---|
| Fácil | 2 | 0,50 | 0,2 |
| Medio | 2 | 0 | 0,2 |
| Difícil | 4 | 0 | 1,9 |
| Muy difícil | 6 | 0 | 11,7 |
| Experto | 8 | 0 | 50 |

`ve6 → ve8` es un salto de 19 puntos: son rivales distintos de verdad. **ve8 está
saturado** — en la sentadura invertida gana 100/0, todas las partidas en
exactamente 8 plies. Nada por encima agrega fuerza, y ve10 hace que 1 de cada 20
partidas termine en shuffle sin resultado. Por eso la escalera corta ahí y el
escalón fácil se hace con ε, no bajando más la visión.

Reproducir la tabla: `node test/practica.js --games 2000 --depths 2,4,6,8`.

## 4. Decisiones del port que no son obvias

**Posiciones empaquetadas en un `Number`, con aritmética y no bitwise.**
10 piezas × 4 bits = 40 bits, muy por debajo de los 53 exactos de un `Number`; no
hace falta `BigInt`. Pero `<<`/`&`/`|` truncan a 32 bits y darían mal desde la
pieza 8, así que se usa `Math.floor(p / 16**i) % 16`. El entero que sale es **el
mismo entero que el `ulong` de C#**, que es lo que permite comparar contra el
solver sin traducir nada.

**`ONGOING = 3`, nunca `null` ni un chequeo de verdad.** `winnerAfter` devuelve
`DRAW = 0`, que es un resultado válido *y falsy*. Y `2`/`-2` ya están tomados por
el alpha-beta. Regla sin excepciones: `w !== ONGOING`, jamás `if (w)`.

**El taint va codificado en el valor de retorno.** En C# viaja por un `out bool`,
que es un local fresco por invocación; una bandera a nivel módulo en JS la
pisaría la recursión en silencio y la tabla se ensuciaría con valores dependientes
del camino. Se devuelve `(valor + 1) | (contaminado ? 4 : 0)`, y el error pasa a
ser mecánicamente imposible.

**Tres rarezas de `Search.cs` se preservan a propósito**, cada una con su test:
profundidad 0 devuelve 0 sin test terminal; el ahogado se testea *después* del
probe de tabla; nada se guarda por debajo de profundidad 2. Un refactor "prolijo"
borra las tres y cambia los resultados.

**La UI no conoce ni una regla.** Consume `legalMoves` del motor y nada más. Si
aparece un `Math.abs(a - b) === 1` en `src/scene/` o `src/app/`, el diseño se
rompió.

**Pensar y jugar están separados.** El worker expone `aiPick`, que elige la jugada
y la devuelve **sin aplicarla**; quien la muestra la aplica con `applyMove`. Con
un solo `aiMove` que hiciera las dos cosas, el bot contra bot no podría pensar
durante la pausa entre jugadas sin adelantar la partida respecto de la pantalla,
y pausar o deshacer mientras piensa dejaría al motor una jugada adelante.

**Los segundos del bot contra bot son ritmo, no recargo.** La cuenta regresiva y
la búsqueda arrancan juntas, así que el ciclo mide *pausa + animación* y no
*pausa + pensar + animación*. `Pensando…` sólo aparece si la búsqueda tardó más
que la pausa. Medido con `test/traza.mjs`: a Experto, 1,2-1,5 s por jugada con
pausa 0 y 3,1-3,3 s con pausa 2.

## 5. El collar

Cada pieza se apoya en un cilindro coloreado por dueño con el numeral del nivel
en el borde. Es **más ancho que el cuerpo** (0,68 contra 0,48), así que desde
cualquier ángulo por encima del horizonte se ve una franja limpia de collares,
uno por pieza: un tótem de 3 se lee como tres franjas numeradas sin tocar nada.

El primer intento lo tenía invisible — el cuerpo medía casi lo mismo que el
collar, así que a 45° se escorzaba a nada. Las alturas también se re-repartieron
(0,15 a 0,42 en vez de 0,18 a 0,34) porque los niveles 1, 2 y 3 se veían iguales.

Todos esos números están en `src/scene/geometry.js` y son de ajuste: **si una pila
de 4 no se lee, se tocan esos y nada más.**

La cámara sale de una ecuación: `celdas ocultas = altura de la pila / tan(elevación)`.

| elevación | celdas ocultas detrás de una pila máxima |
|---|---|
| 30° | 3,1 — inusable |
| **45° (por defecto)** | **1,8; una pila realista de 3 oculta 0,89** |
| 62° (el otro preset) | 0,97 — sin oclusión real |

## 6. Verificación

```bash
npm test                                   # todo
node --test test/searcher.test.js          # solo el motor
node test/practica.js --games 2000          # reproducir la escalera
node test/shot.mjs <url> <salida.png> [w] [h] [condicionJS]
node test/traza.mjs [segundos] [dificultad] [url]   # bot contra bot, trazado
```

**Contra el solver C#.** El minimax pelado no tiene tabla ni poda, así que su
recorrido es determinista y sus conteos de nodos se asertan **exactos** contra
C#: 37 · 1.189 · 30.205 · 673.373 · 11.367.485. Cualquier diferencia en generación
de jugadas, en su orden de emisión o en la detección terminal movería esos
números. El alpha-beta coincide exacto hasta profundidad 8 y difiere <0,05% de 9
a 12, sólo por el tamaño de tabla (C# usa 2²⁵, JS 2²²).

**Grafo de escena sin GPU.** Three.js corre en Node para todo menos
`WebGLRenderer`, así que se arma la escena entera y se assertan posiciones en
milisegundos. Ahí va la mayor parte de la cobertura, porque "la pieza quedó a la
altura equivocada en una pila de 3" es el bug que este juego va a tener.

**`test/traza.mjs`** mira una partida de bot contra bot desde afuera del juego y
muestrea fase, ply, largo del historial, filas del panel abierto y el texto de
turno cada 100 ms. Contesta las dos cosas que a ojo no se contestan: que el
historial no se atrasa respecto del tablero (`record.length == ply` fuera de la
animación) y cuánto mide de verdad el ciclo por jugada.

**`test/shot.mjs`** es un driver headless sobre el DevTools Protocol. Existe
porque `--screenshot` con `--virtual-time-budget` no sirve acá: el tiempo virtual
adelanta los timers del hilo principal pero **no espera los roundtrips del Web
Worker**, así que la foto sale antes de que el motor conteste. Éste espera una
condición de JS de verdad y junta los errores de consola.

**Parámetros de debug** (`?...` en la URL):

| parámetro | qué hace |
|---|---|
| `?demo=N&seed=S` | juega N jugadas legales al azar, con semilla |
| `?jugadas=a,b,c` | aplica esos ids de jugada exactos |
| `?pos=<entero>&turno=0\|1` | carga una posición cruda |
| `?elev=<grados>` | fija la elevación de cámara |

`?pos=` es la única forma de ver la pantalla de ahogado: hay **2 posiciones sin
jugada legal en los 1,4 millones de nodos** que resuelven el juego, y **0 en los
5,5 millones de estados alcanzables en 8 plies**. Jugando no se llega nunca.
Están fijadas en `test/fixtures/ahogados.json`; para verla:

```
?pos=1058730636576&turno=1
```

## 7. Meter los modelos de animales

El juego arranca y es jugable con piezas procedurales. Los modelos entran después
y **de a uno**: la degradación es por nivel, así que si sólo existe el ratón, el
ratón usa modelo y los otros cuatro siguen procedurales.

1. En Blender, por cada animal: unir en un solo objeto (`Ctrl+J`), un solo
   material sin textura, sin vertex colors, y **aplicar todas las
   transformaciones** (`Ctrl+A → All Transforms`).
2. Exportar:
   ```
   blender --background animales.blend --python tools/export_models.py
   ```
   Escribe un `.glb` por objeto en `public/models/`.
3. Mapear nivel → archivo en `public/models/manifest.json`:
   ```json
   "ranks": { "1": { "file": "raton.glb", "label": "Ratón" } }
   ```

**Lo único que el loader no puede arreglar es una escala NO UNIFORME de objeto**,
porque hornea una distorsión en los vértices exportados. Todo lo demás sí: el
origen, la escala y la posición en el mundo se normalizan al cargar. No hace falta
tocarlos en Blender.

Nada de STL: no tiene materiales, ni convención de eje, ni suavizado, ni unidades,
ni grafo de escena. glTF binario y listo.

## 8. Deploy

`.github/workflows/pages.yml` publica el juego en la raíz del sitio y conserva el
build de Unity de 2022 en `/legacy/`. No borra ningún archivo del repo: el build
viejo se copia dentro del artefacto desde los archivos que siguen trackeados.

Requiere **un paso manual, una sola vez**, en la web de GitHub:
*Settings → Pages → Source: **GitHub Actions***.

Para volver atrás: esa misma opción a *Deploy from a branch: master / (root)*, y
el sitio queda exactamente como estaba.

Antes de pushear conviene probar el subpath, que es la falla de deploy más común:

```bash
npm run build && npm run preview:subpath
```

Nunca por `file://`: los module workers no cargan.

## 9. Lo que queda afuera a propósito

Multiplayer online (la versión Unity usaba Photon sin autoridad de servidor; no
hay nada que rescatar). Analytics. React o cualquier store: el HUD son ocho
elementos. TypeScript. `InstancedMesh`, LOD, pooling: son ~45 objetos y 50-70 draw
calls, e instancing empieza a pagar en los cientos. Post-processing. Librerías de
tweens. Pinch-zoom. Alternar automáticamente quién empieza — con estos sets
`12355` gana en 7 plies si arranca, así que alternar empeora
sistemáticamente la partida para un lado. Cambiar de bando es un botón.
