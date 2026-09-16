/**
 * Todas las medidas del juego, en unidades de celda (paso = 1).
 *
 * Es el UNICO lugar que convierte indices de celda en posiciones del mundo. Si
 * aparece aritmetica de posiciones en otro archivo, algo se duplico.
 */

export const CELL_PITCH = 1.0
export const BOARD_HALF = 1.5          // el tablero va de -1,5 a +1,5 en x y z
export const TILE_SIZE = 0.92
export const TILE_THICKNESS = 0.02
export const BOARD_THICKNESS = 0.12

/**
 * Cara superior de la baldosa. Todo anillo que vaya "en el piso" tiene que
 * dibujarse por ENCIMA de esto o queda enterrado dentro de la baldosa y no se
 * ve — que es exactamente lo que pasaba con los anillos de destino.
 */
export const TILE_TOP = 0.001 + TILE_THICKNESS
/** Altura a la que se dibujan los anillos apoyados en el piso. */
export const GROUND_RING_Y = TILE_TOP + 0.012

/**
 * La MONEDA es la pieza entera y el dispositivo central de legibilidad del juego.
 *
 * Cada pieza es un cilindro chato coloreado por dueño: el numeral del nivel
 * grande en la cara, y repetido alrededor del borde. Todas tienen el mismo
 * radio, asi que una pila es un rollo de monedas: desde cualquier angulo por
 * encima del horizonte se ve una franja numerada por pieza, y la cara de la
 * de arriba dice de un vistazo que nivel esta destapado.
 *
 * Hace tres trabajos a la vez:
 *   1. Resuelve "que hay enterrado" sin interaccion (el borde).
 *   2. Dice que hay destapado sin leer letra chica (la cara).
 *   3. Es la geometria fija sobre la que se apoya un modelo, si algun dia hay.
 */
export const COIN_RADIUS = 0.34

/**
 * Grosor de la moneda por nivel. El grosor codifica el nivel, asi que la
 * silueta de la pila es informacion en si misma, redundante con los numerales.
 *
 * Va de 0,12 a 0,24 (2x): mas chato que el cono anterior, pero con un borde
 * mas alto que el collar viejo (0,085), asi que los numerales del canto se leen
 * mejor. Son numeros de ajuste: si una pila de 4 no se lee, se tocan estos y
 * nada mas.
 */
export const COIN_THICKNESS = Object.freeze({ 1: 0.12, 2: 0.15, 3: 0.18, 4: 0.21, 5: 0.24 })

/**
 * Huella maxima de un modelo apoyado sobre la moneda. Bastante MAS ANGOSTA que
 * la moneda a proposito: la moneda tiene que sobresalir un labio visible de
 * 0,10 por todos lados, si no a 45 grados queda escorzada a nada y la pila
 * deja de leerse.
 */
export const FOOTPRINT = 0.48

/**
 * Alto de un modelo de animal parado sobre la moneda, por nivel. Solo aplica si
 * hay modelo cargado; la pieza por defecto es la moneda sola.
 */
export const PIECE_HEIGHT = Object.freeze({ 1: 0.15, 2: 0.21, 3: 0.27, 4: 0.34, 5: 0.42 })

/** Altura total que ocupa una pieza de nivel r apilada (la moneda). */
export const pieceThickness = (rank) => COIN_THICKNESS[rank] ?? 0.18

/**
 * Pila maxima teorica: los niveles crecen ESTRICTAMENTE hacia arriba, asi que
 * nunca hay mas de 5 piezas en una celda. 0,90 unidades.
 */
export const MAX_STACK_HEIGHT =
  [1, 2, 3, 4, 5].reduce((a, r) => a + pieceThickness(r), 0)

/** Centro de la celda i, en el plano del tablero (y = 0 es la cara de arriba). */
export function cellToWorld(i) {
  return { x: (i % 3) - 1, y: 0, z: Math.floor(i / 3) - 1 }
}

/** Indice de celda desde coordenadas de mundo, o -1 si cae afuera. */
export function worldToCell(x, z) {
  const col = Math.round(x) + 1
  const row = Math.round(z) + 1
  if (col < 0 || col > 2 || row < 0 || row > 2) return -1
  return row * 3 + col
}

/**
 * Altura a la que se apoya la pieza que ocupa el indice `depth` de una pila
 * (0 = abajo de todo), dados los niveles de las que tiene debajo.
 * @param {number[]} ranksBelow
 */
export function stackY(ranksBelow) {
  let y = 0
  for (const r of ranksBelow) y += pieceThickness(r)
  return y
}

// --- Bandejas de mano ---------------------------------------------------------

export const TRAY_SLOTS = 5
// El paso tiene que superar el diametro de la moneda (0,68) DESPUES del escorzo.
// En horizontal la bandeja corre a lo largo de z, que a 45 grados se proyecta
// por sin(45) = 0,707, asi que hace falta 0,68 / 0,707 = 0,96 para que no se
// pisen. Se redondea a 0,95.
export const TRAY_PITCH = 0.95
export const TRAY_OFFSET = 2.35    // distancia del centro del tablero al eje de la bandeja

/**
 * Posicion de un slot de bandeja.
 *
 * El EJE DE LA BANDEJA SIGUE EL EJE LARGO DE LA PANTALLA: filas horizontales
 * adelante y atras en vertical, columnas a los lados en horizontal. Cinco slots
 * a 0,7 de paso son 3,5 unidades, mas ancho que el tablero de 3, asi que sin
 * esto la caja de contenido queda con el aspect ratio equivocado y la camara se
 * aleja de mas. Es la diferencia entre "anda en celulares" y "tecnicamente
 * renderiza en celulares".
 *
 * @param {number} side      0 = blancas (cerca), 1 = negras (lejos)
 * @param {number} slot      0..4
 * @param {'portrait'|'landscape'} layout
 */
export function traySlotToWorld(side, slot, layout) {
  const along = (slot - (TRAY_SLOTS - 1) / 2) * TRAY_PITCH
  const away = side === 0 ? TRAY_OFFSET : -TRAY_OFFSET
  return layout === 'portrait'
    ? { x: along, y: 0, z: away }
    : { x: away, y: 0, z: along }
}

/**
 * Cajas de contenido a encuadrar. Son DOS y no una a proposito.
 *
 * Solo el tablero tiene altura: las bandejas son siempre planas, nunca se apila
 * nada ahi. Meter todo en una sola caja obliga a la camara a encuadrar esquinas
 * que no existen (los extremos de las bandejas a la altura de una pila de 5) y
 * la aleja bastante de mas.
 *
 * @returns {{halfX:number, halfZ:number, yLow:number, yHigh:number}[]}
 */
export function contentBoxes(layout) {
  const long = TRAY_OFFSET + 0.45
  const across = Math.max(BOARD_HALF + 0.25, (TRAY_SLOTS * TRAY_PITCH) / 2)
  const tablero = { halfX: BOARD_HALF + 0.25, halfZ: BOARD_HALF + 0.25, yLow: -0.15, yHigh: MAX_STACK_HEIGHT + 0.15 }
  const bandejas = layout === 'portrait'
    ? { halfX: across, halfZ: long, yLow: -0.15, yHigh: 0.55 }
    : { halfX: long, halfZ: across, yLow: -0.15, yHigh: 0.55 }
  return [tablero, bandejas]
}

/** Extension total, para tests y para el layout. */
export function contentExtent(layout) {
  const boxes = contentBoxes(layout)
  return {
    halfX: Math.max(...boxes.map((b) => b.halfX)),
    halfZ: Math.max(...boxes.map((b) => b.halfZ)),
  }
}

// --- Camara -------------------------------------------------------------------

export const FOV = 35

/**
 * La restriccion de la camara es una sola ecuacion:
 *
 *     celdas ocultas detras de una pila = altura de la pila / tan(elevacion)
 *
 * Con la pila maxima de 0,90 unidades:
 *
 *     30 grados -> 1,56 celdas ocultas   tapa la fila de atras
 *     45 grados -> 0,90 celdas           los cantos de atras todavia asoman
 *     62 grados -> 0,48 celdas           sin oclusion real
 *     78 grados -> 0,19 celdas           casi cenital, el grosor no se lee
 *
 * Por eso dos presets y no un slider: arrastrar un slider en un celular es
 * molesto, y nadie quiere un tercer eje de control de camara en un juego de mesa.
 */
export const ELEVATION_LOW = 45     // se lee la altura
export const ELEVATION_HIGH = 62    // se ve todo el tablero
export const ELEVATION_MIN = 25
export const ELEVATION_MAX = 78

/** Cuantas celdas quedan ocultas detras de una pila maxima, a esta elevacion. */
export const occludedCells = (elevationDeg) =>
  MAX_STACK_HEIGHT / Math.tan((elevationDeg * Math.PI) / 180)
