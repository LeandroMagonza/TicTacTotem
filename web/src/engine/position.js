import { HAND, CELLS, WHITE, POW16 } from './constants.js'

/**
 * Una posicion empaquetada: 4 bits por pieza, con la casilla (0..8) o HAND (15).
 * Game.cs:13-16. El orden dentro de una pila queda implicito en el rango, porque
 * solo se puede tapar con un rango estrictamente mayor: una celda nunca tiene dos
 * piezas del mismo rango y la pila esta siempre ordenada de menor a mayor.
 *
 * Con 10 piezas son 40 bits, muy por debajo de los 53 exactos de un Number. Y el
 * entero que sale de aca es EL MISMO entero que el ulong de C#, que es lo que
 * permite que el test diferencial compare decimales y nada mas.
 *
 * @typedef {number} Position
 */

/**
 * Casilla de la pieza i. Aritmetica, no bitwise: `p >> (4*i)` truncaria a 32 bits
 * y daria mal desde la pieza 8 en adelante.
 * @param {Position} p @param {number} i
 */
export const loc = (p, i) => Math.floor(p / POW16[i]) % 16

/**
 * Devuelve p con la pieza i movida a `cell`. Suma la diferencia en vez de
 * enmascarar; es exacto porque todo es entero y 16**i es potencia de 2.
 * @param {Position} p @param {number} i @param {number} cell
 */
export const withLoc = (p, i, cell) => p + (cell - loc(p, i)) * POW16[i]

/** Jugada = (indiceDePieza << 4) | celdaDestino. Cabe en 8 bits, bitwise va bien. */
export const movePiece = (move) => move >> 4
export const moveTo = (move) => move & 0xf
export const makeMove = (piece, cell) => (piece << 4) | cell

/** @param {Position} p @param {number} move */
export const applyMove = (p, move) => withLoc(p, move >> 4, move & 0xf)

/** Todas las piezas en la mano. Game.cs:102-106. @param {import('./spec.js').GameSpec} spec */
export function initialPosition(spec) {
  let p = 0
  for (let i = 0; i < spec.pieceCount; i++) p += HAND * POW16[i]
  return p
}

/**
 * Empaqueta un array de casillas. Recorre de atras hacia adelante para que la
 * pieza 0 quede en el nibble mas bajo, igual que Game.cs:212.
 * @param {ArrayLike<number>} locs @param {number} pieceCount
 */
export function pack(locs, pieceCount) {
  let v = 0
  for (let i = pieceCount - 1; i >= 0; i--) v = v * 16 + locs[i]
  return v
}

/** @param {import('./spec.js').GameSpec} spec @param {Position} p @param {Int8Array} out */
export function unpackInto(spec, p, out) {
  for (let i = 0; i < spec.pieceCount; i++) out[i] = Math.floor(p / POW16[i]) % 16
  return out
}

/** @param {import('./spec.js').GameSpec} spec @param {Position} p */
export const unpack = (spec, p) => unpackInto(spec, p, new Int8Array(spec.pieceCount))

/** Tablero en texto, para debug y para el diff cuando falla el test diferencial. Game.cs:218-235 */
export function describe(spec, p) {
  let out = ''
  for (let r = 0; r < 3; r++) {
    for (let c = 0; c < 3; c++) {
      const cell = r * 3 + c
      const stack = []
      for (let i = 0; i < spec.pieceCount; i++) if (loc(p, i) === cell) stack.push(i)
      stack.sort((a, b) => spec.rank[a] - spec.rank[b])
      const s = stack.map((i) => (spec.owner[i] === WHITE ? 'W' : 'B') + spec.rank[i]).join('/')
      out += (s.length === 0 ? '.' : s).padEnd(9)
    }
    out += '\n'
  }
  return out
}

/** Game.cs:237-244. Mismo formato que el solver, asi los logs son comparables. */
export function describeMove(spec, p, move, turn) {
  const piece = move >> 4, to = move & 0xf
  const from = loc(p, piece)
  const who = turn === WHITE ? 'W' : 'B'
  const cell = (x) => `(${(x / 3) | 0},${x % 3})`
  return from === HAND
    ? `${who}C${spec.rank[piece]}${cell(to)}`
    : `${who}M${spec.rank[piece]}${cell(from)}-${cell(to)}`
}

/** Nombres de celda tipo A1..C3 para la UI. Fila 0 = A. */
export const cellName = (c) => 'ABC'[(c / 3) | 0] + (1 + (c % 3))

export { CELLS, HAND }
