import { CELLS, WHITE, BLACK, MAX_PIECES } from './constants.js'

/**
 * @typedef {object} GameSpec
 * @property {number}    pieceCount
 * @property {Int8Array} owner       WHITE/BLACK por indice de pieza
 * @property {Int8Array} rank
 * @property {Int8Array} groupStart  grupos de piezas identicas (mismo dueño y rango)
 * @property {Int8Array} groupLen
 * @property {Int8Array} groupOwner
 * @property {Int8Array} groupRank
 * @property {number}    groupCount
 * @property {Int8Array} adjStart    adyacencia en CSR: adj[adjStart[c] .. adjEnd[c])
 * @property {Int8Array} adjEnd
 * @property {Int8Array} adj
 * @property {Int8Array} symmetries  8 permutaciones D4, aplanadas (8 * 9)
 * @property {string}    whiteLabel
 * @property {string}    blackLabel
 * @property {boolean}   sinCentro   regla: nadie coloca desde la mano en el centro
 */

/**
 * @typedef {object} Rules
 * @property {boolean} [sinCentro]  Al centro solo se llega moviendo una pieza ya puesta.
 *   Es la regla que saca la apertura dominante del primero (4 al centro, 68 %) sin
 *   quitarle la eleccion de casilla. Port de --sin-centro del solver (Game.cs).
 */

/**
 * Port de GameSpec(..) — Game.cs:47-100.
 *
 * Las piezas blancas ocupan los indices 0..n-1 ordenadas por rango ascendente, y
 * las negras siguen, tambien ordenadas. Esa contiguidad es lo que hace que los
 * grupos de piezas identicas sean rangos contiguos de indices, que es de lo que
 * depende `canonical`.
 *
 * @param {number[]} whitePieces
 * @param {number[]} blackPieces
 * @param {Rules} [rules]
 * @returns {GameSpec}
 */
export function makeSpec(whitePieces, blackPieces, rules = {}) {
  const white = [...whitePieces].sort((a, b) => a - b)
  const black = [...blackPieces].sort((a, b) => a - b)
  const pieceCount = white.length + black.length

  // C# banca 16 piezas (Game.cs:54) porque 16*4 = 64 bits entran en un ulong.
  // JS tiene 53 bits exactos, asi que el techo es 13. Fallar fuerte: un overflow
  // silencioso aca corrompe todas las claves de la tabla de transposicion.
  if (pieceCount > MAX_PIECES) {
    throw new RangeError(
      `Maximo ${MAX_PIECES} piezas en JS (${pieceCount} pedidas): ` +
      `${pieceCount} * 4 = ${pieceCount * 4} bits no entra en los 53 exactos de un Number.`
    )
  }
  if (pieceCount === 0) throw new RangeError('Hacen falta piezas.')

  const owner = new Int8Array(pieceCount)
  const rank = new Int8Array(pieceCount)
  let k = 0
  for (const r of white) { owner[k] = WHITE; rank[k] = r; k++ }
  for (const r of black) { owner[k] = BLACK; rank[k] = r; k++ }

  const gs = [], gl = [], go = [], gr = []
  for (let i = 0; i < pieceCount;) {
    let j = i
    while (j < pieceCount && owner[j] === owner[i] && rank[j] === rank[i]) j++
    gs.push(i); gl.push(j - i); go.push(owner[i]); gr.push(rank[i])
    i = j
  }

  // Adyacencia ortogonal. El orden de los vecinos importa: el test diferencial
  // compara el orden de emision de generateMoves contra el de C#, y ese orden
  // sale de aca. Game.cs:78 recorre (-1,0), (1,0), (0,-1), (0,1).
  const adjStart = new Int8Array(CELLS)
  const adjEnd = new Int8Array(CELLS)
  const adjList = []
  const deltas = [[-1, 0], [1, 0], [0, -1], [0, 1]]
  for (let c = 0; c < CELLS; c++) {
    adjStart[c] = adjList.length
    const row = (c / 3) | 0, col = c % 3
    for (const [dr, dc] of deltas) {
      const nr = row + dr, nc = col + dc
      if (nr >= 0 && nr < 3 && nc >= 0 && nc < 3) adjList.push(nr * 3 + nc)
    }
    adjEnd[c] = adjList.length
  }

  // Las 8 simetrias del cuadrado. Game.cs:85-99.
  const transforms = [
    (r, c) => r * 3 + c,              // identidad
    (r, c) => c * 3 + (2 - r),        // rotacion 90
    (r, c) => (2 - r) * 3 + (2 - c),  // rotacion 180
    (r, c) => (2 - c) * 3 + r,        // rotacion 270
    (r, c) => r * 3 + (2 - c),        // espejo vertical
    (r, c) => (2 - r) * 3 + c,        // espejo horizontal
    (r, c) => c * 3 + r,              // diagonal principal
    (r, c) => (2 - c) * 3 + (2 - r),  // antidiagonal
  ]
  const symmetries = new Int8Array(8 * CELLS)
  transforms.forEach((t, s) => {
    for (let c = 0; c < CELLS; c++) symmetries[s * CELLS + c] = t((c / 3) | 0, c % 3)
  })

  return Object.freeze({
    pieceCount,
    owner, rank,
    groupStart: Int8Array.from(gs),
    groupLen: Int8Array.from(gl),
    groupOwner: Int8Array.from(go),
    groupRank: Int8Array.from(gr),
    groupCount: gs.length,
    adjStart, adjEnd, adj: Int8Array.from(adjList),
    symmetries,
    whiteLabel: white.join(''),
    blackLabel: black.join(''),
    sinCentro: rules.sinCentro === true,
  })
}

/**
 * Atajo: makeSpecFromLabels('12344', '12355', { sinCentro: true })
 * @param {string} white
 * @param {string} black
 * @param {Rules} [rules]
 */
export function makeSpecFromLabels(white, black, rules = {}) {
  const digits = (s) => [...s].map((ch) => {
    const d = Number(ch)
    if (!Number.isInteger(d) || d < 1 || d > 9) throw new RangeError(`Rango invalido: ${ch}`)
    return d
  })
  return makeSpec(digits(white), digits(black), rules)
}
