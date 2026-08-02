/**
 * Todas las medidas del juego, en unidades de celda (paso = 1).
 *
 * Es el UNICO lugar que convierte indices de celda en posiciones del mundo. Si
 * aparece aritmetica de posiciones en otro archivo, algo se duplico.
 */

export const CELL_PITCH = 1.0
export const BOARD_HALF = 1.5          // el tablero va de -1,5 a +1,5 en x y z
export const TILE_SIZE = 0.92
export const BOARD_THICKNESS = 0.12

/**
 * El COLLAR es el dispositivo central de legibilidad del juego.
 *
 * Cada pieza se apoya en un cilindro coloreado por dueño con el numeral del
 * nivel en el borde. Es mas ancho que la huella de cualquier animal, asi que
 * desde cualquier angulo por encima del horizonte se ve una franja limpia de
 * collares, uno por pieza: un totem de 3 se lee como tres franjas numeradas sin
 * tocar nada.
 *
 * Hace cuatro trabajos a la vez:
 *   1. Resuelve "que hay enterrado" sin interaccion.
 *   2. Codifica el dueño de forma redundante con el tinte del animal.
 *   3. ES el placeholder: collar + cono procedural ya es una pieza jugable.
 *   4. Es la geometria fija sobre la que se normalizan los modelos.
 */
export const COLLAR_RADIUS = 0.34
export const COLLAR_HEIGHT = 0.085

/**
 * Huella maxima del cuerpo. Bastante MAS ANGOSTA que el collar a proposito: el
 * collar tiene que sobresalir un labio visible de 0,10 por todos lados, si no a
 * 45 grados queda escorzado a nada y la pila deja de leerse. Es la medida de la
 * que depende toda la legibilidad del juego.
 */
export const FOOTPRINT = 0.48

/**
 * Altura por nivel. La altura fisica codifica el nivel, asi que la silueta del
 * totem es informacion en si misma.
 *
 * El rango va de 0,15 a 0,42 — casi 3x — porque con el reparto anterior
 * (0,18 a 0,34) los niveles 1, 2 y 3 se veian iguales en pantalla. Son numeros
 * de ajuste: si una pila de 4 no se lee, se tocan estos y nada mas.
 */
export const PIECE_HEIGHT = Object.freeze({ 1: 0.15, 2: 0.21, 3: 0.27, 4: 0.34, 5: 0.42 })

/** Altura total que ocupa una pieza de nivel r apilada (collar + cuerpo). */
export const pieceThickness = (rank) => COLLAR_HEIGHT + (PIECE_HEIGHT[rank] ?? 0.26)

/**
 * Pila maxima teorica: los niveles crecen ESTRICTAMENTE hacia arriba, asi que
 * nunca hay mas de 5 piezas en una celda. 1,58 unidades.
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
// El paso tiene que superar el diametro del collar (0,68) DESPUES del escorzo.
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
 * Con la pila maxima de 1,58 unidades:
 *
 *     30 grados -> 2,74 celdas ocultas   inusable
 *     45 grados -> 1,58 celdas           los collares de atras todavia asoman
 *     62 grados -> 0,84 celdas           sin oclusion real
 *     78 grados -> 0,34 celdas           casi cenital, la altura no se lee
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
