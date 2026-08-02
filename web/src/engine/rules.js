import {
  HAND, CELLS, WHITE, LINES, POW16, MAX_PIECES,
  ONGOING, WHITE_WIN, BLACK_WIN,
} from './constants.js'
import { unpackInto, pack } from './position.js'

// Buffers de scratch a nivel modulo, uno por funcion, para no allocar por nodo.
// Cada funcion tiene el suyo a proposito: compartir uno solo abriria la puerta a
// que una llamada anidada le pise los tops a otra.
const _topsGen = new Int8Array(CELLS)
const _topsWin = new Int8Array(CELLS)
const _canonSrc = new Int8Array(MAX_PIECES)
const _canonTmp = new Int8Array(MAX_PIECES)
const _locsScratch = new Int8Array(MAX_PIECES)

/**
 * Pieza visible (la de mayor rango) de cada celda, o -1 si esta vacia.
 * Game.cs:114-122.
 * @param {import('./spec.js').GameSpec} spec
 * @param {ArrayLike<number>} locs
 * @param {Int8Array} out
 */
export function computeTops(spec, locs, out) {
  const { pieceCount, rank } = spec
  out.fill(-1)
  for (let i = 0; i < pieceCount; i++) {
    const c = locs[i]
    if (c >= CELLS) continue
    const cur = out[c]
    if (cur < 0 || rank[i] > rank[cur]) out[c] = i
  }
  return out
}

/**
 * Quien gano despues de que `mover` jugo, o ONGOING si la partida sigue.
 *
 * Port de GameManager.CheckForWinner (GameManager.cs:189-221) via Game.cs:130-150:
 * cuenta lineas de piezas DESTAPADAS, y el que acaba de mover pierde los empates
 * (`wins[whoseTurn] -= 0.5f`, GameManager.cs:210). Se trabaja con medios puntos
 * duplicados para quedarse en enteros.
 *
 * La consecuencia es la regla trampa del juego: destapar una linea del rival te
 * hace perder, aunque la linea no sea tuya.
 *
 * @param {import('./spec.js').GameSpec} spec
 * @param {ArrayLike<number>} locs
 * @param {number} mover
 * @returns {number} WHITE_WIN | BLACK_WIN | ONGOING
 */
export function winnerAfter(spec, locs, mover) {
  const top = computeTops(spec, locs, _topsWin)
  const owner = spec.owner

  let whiteLines = 0, blackLines = 0
  for (let k = 0; k < 8; k++) {
    const line = LINES[k]
    const a = top[line[0]]
    if (a < 0) continue
    const o = owner[a]
    const b = top[line[1]]; if (b < 0 || owner[b] !== o) continue
    const c = top[line[2]]; if (c < 0 || owner[c] !== o) continue
    if (o === WHITE) whiteLines++; else blackLines++
  }
  if (whiteLines === 0 && blackLines === 0) return ONGOING

  let ws = 2 * whiteLines, bs = 2 * blackLines
  if (mover === WHITE) ws -= 1; else bs -= 1
  // El medio punto rompe todo empate, asi que uno de los dos siempre gana.
  return ws > bs ? WHITE_WIN : BLACK_WIN
}

/**
 * Todas las jugadas legales. Devuelve cuantas escribio en `out`.
 * Game.cs:152-181, que a su vez viene de Cell.CanPieceBePushed (Cell.cs:78-108)
 * y Piece.CanPieceBeMoved (Piece.cs:36-45).
 *
 * El ORDEN de emision es parte del contrato: el test diferencial lo compara
 * contra el de C#, lo que agarra toda una clase de bugs de transposicion de
 * bucles gratis. No reordenar.
 *
 * @param {import('./spec.js').GameSpec} spec
 * @param {ArrayLike<number>} locs
 * @param {number} turn
 * @param {Int32Array|number[]} out
 * @returns {number}
 */
export function generateMoves(spec, locs, turn, out) {
  const { groupCount, groupStart, groupLen, groupOwner, owner, rank, adjStart, adjEnd, adj } = spec
  const top = computeTops(spec, locs, _topsGen)
  let n = 0

  // Colocar desde la mano, solo en celda vacia. Las piezas identicas dan jugadas
  // identicas, asi que alcanza con la primera libre del grupo.
  for (let g = 0; g < groupCount; g++) {
    if (groupOwner[g] !== turn) continue
    let piece = -1
    const start = groupStart[g], end = start + groupLen[g]
    for (let i = start; i < end; i++) {
      if (locs[i] === HAND) { piece = i; break }
    }
    if (piece < 0) continue
    for (let c = 0; c < CELLS; c++) if (top[c] < 0) out[n++] = (piece << 4) | c
  }

  // Mover una pieza propia destapada a una celda ortogonal vacia, o sobre una
  // pila cuya pieza visible tenga rango ESTRICTAMENTE menor (igual no se puede).
  for (let c = 0; c < CELLS; c++) {
    const i = top[c]
    if (i < 0 || owner[i] !== turn) continue
    const from = adjStart[c], to = adjEnd[c]
    for (let k = from; k < to; k++) {
      const d = adj[k]
      const t = top[d]
      if (t < 0 || rank[t] < rank[i]) out[n++] = (i << 4) | d
    }
  }
  return n
}

/**
 * Representante canonico bajo las 8 simetrias del tablero y el intercambio de
 * piezas identicas. Reduce la tabla de transposicion cerca de 8x. Game.cs:189-216.
 *
 * Devuelve una posicion EMPAQUETADA, comparable directamente con cualquier otra.
 *
 * @param {import('./spec.js').GameSpec} spec
 * @param {ArrayLike<number>} locs
 * @returns {number}
 */
export function canonical(spec, locs) {
  const { pieceCount, groupCount, groupStart, groupLen, symmetries } = spec
  const t = _canonTmp
  let best = Infinity

  for (let s = 0; s < 8; s++) {
    const base = s * CELLS
    for (let i = 0; i < pieceCount; i++) {
      const l = locs[i]
      t[i] = l >= CELLS ? HAND : symmetries[base + l]
    }
    // Ordenar dentro de cada grupo de piezas identicas. Insercion: los grupos
    // son de 2-3 elementos.
    for (let g = 0; g < groupCount; g++) {
      const len = groupLen[g]
      if (len < 2) continue
      const st = groupStart[g]
      for (let i = st + 1; i < st + len; i++) {
        const v = t[i]
        let j = i - 1
        while (j >= st && t[j] > v) { t[j + 1] = t[j]; j-- }
        t[j + 1] = v
      }
    }
    let v2 = 0
    for (let i = pieceCount - 1; i >= 0; i--) v2 = v2 * 16 + t[i]
    if (v2 < best) best = v2
  }
  return best
}

// ---------------------------------------------------------------------------
// Envoltorios sobre posiciones empaquetadas, para la UI y los tests. Desempacan
// a un scratch y delegan. No usar dentro de la busqueda: ahi conviene mantener
// el array desempacado vivo con make/unmake.
// ---------------------------------------------------------------------------

/** @param {import('./spec.js').GameSpec} spec @param {number} p @param {number} mover */
export function winnerAfterPacked(spec, p, mover) {
  return winnerAfter(spec, unpackInto(spec, p, _locsScratch), mover)
}

/** @param {import('./spec.js').GameSpec} spec @param {number} p @param {number} turn */
export function legalMoves(spec, p, turn) {
  const buf = new Int32Array(128)
  const n = generateMoves(spec, unpackInto(spec, p, _locsScratch), turn, buf)
  return buf.subarray(0, n)
}

/** @param {import('./spec.js').GameSpec} spec @param {number} p */
export function canonicalPacked(spec, p) {
  return canonical(spec, unpackInto(spec, p, _locsScratch))
}

/**
 * Que lineas hay completas y de quien, para poder explicar POR QUE se gano.
 * winnerAfter solo devuelve quien; la UI necesita ademas iluminar las celdas y
 * decir si fue "hiciste linea" o "destapaste la del rival", que son dos cosas
 * muy distintas para el jugador.
 *
 * @param {import('./spec.js').GameSpec} spec @param {number} p
 * @returns {{whiteLines: number[][], blackLines: number[][]}}
 */
export function lineReport(spec, p) {
  const top = computeTops(spec, unpackInto(spec, p, _locsScratch), new Int8Array(CELLS))
  const whiteLines = [], blackLines = []
  for (const line of LINES) {
    const a = top[line[0]]
    if (a < 0) continue
    const o = spec.owner[a]
    const b = top[line[1]]; if (b < 0 || spec.owner[b] !== o) continue
    const c = top[line[2]]; if (c < 0 || spec.owner[c] !== o) continue
    ;(o === WHITE ? whiteLines : blackLines).push(line)
  }
  return { whiteLines, blackLines }
}

/**
 * Pilas por celda, de abajo hacia arriba. La UI las consume para saber a que
 * altura dibujar cada pieza; nunca ordena ni compara rangos por su cuenta.
 * Los rangos crecen estrictamente hacia arriba (Game.cs:14-16).
 * @param {import('./spec.js').GameSpec} spec @param {number} p
 * @returns {number[][]}
 */
export function stacksOf(spec, p) {
  const out = Array.from({ length: CELLS }, () => /** @type {number[]} */ ([]))
  const locs = unpackInto(spec, p, _locsScratch)
  for (let i = 0; i < spec.pieceCount; i++) {
    const c = locs[i]
    if (c < CELLS) out[c].push(i)
  }
  for (const st of out) st.sort((a, b) => spec.rank[a] - spec.rank[b])
  return out
}

export { pack, POW16 }
