import { CELLS, WHITE, ONGOING, DRAW, MAX_PIECES } from './constants.js'
import { unpackInto, pack, initialPosition } from './position.js'
import { winnerAfter, generateMoves, canonical } from './rules.js'

const MAX_PLY = 64
const MAX_MOVES = 128

/** Se tira cuando se agota el presupuesto de tiempo. Search.cs:6, 94-95. */
export class SearchAborted extends Error {
  constructor() { super('busqueda abortada por presupuesto'); this.name = 'SearchAborted' }
}

/**
 * Alpha-beta con tabla de transposicion. Port de Simulacion/solver/Search.cs.
 *
 * El dominio de valores tiene exactamente tres elementos (-1 gana negro, 0 sin
 * decidir, +1 gana blanco), siempre desde el punto de vista de las blancas.
 * Blanco maximiza, negro minimiza.
 *
 * Un veredicto de "gana X" es una victoria forzada real dentro del limite de
 * plies. Un veredicto de 0 solo significa "no hay victoria forzada dentro del
 * limite", nunca "tablas" en sentido teorico.
 */
export class Searcher {
  /**
   * @param {import('./spec.js').GameSpec} spec
   * @param {{ttBits?: number, useRepetition?: boolean}} [opts]
   */
  constructor(spec, opts = {}) {
    const { ttBits = 18, useRepetition = true } = opts
    this.spec = spec
    this.useRepetition = useRepetition

    const size = 1 << ttBits
    this._ttMask = size - 1
    // Float64Array para las claves: son enteros exactos < 2^53, se comparan
    // exacto y no hay que partirlas en dos words. -1 = slot vacio, que reemplaza
    // el TtEntry.Used de C# (Search.cs:31).
    this._ttKey = new Float64Array(size).fill(-1)
    this._ttValue = new Int8Array(size)
    this._ttFlag = new Uint8Array(size)   // 0 exacto, 1 cota inferior, 2 cota superior
    this._ttDepth = new Uint8Array(size)
    this._ttTurn = new Uint8Array(size)

    // Camino actual, para detectar repeticion.
    this._pathKey = new Float64Array(MAX_PLY + 2)
    this._pathTurn = new Int8Array(MAX_PLY + 2)
    this._pathLen = 0

    // Buffers por ply, preasignados: nada de allocar por nodo.
    this._moveBuf = Array.from({ length: MAX_PLY + 2 }, () => new Int32Array(MAX_MOVES))
    this._quietBuf = Array.from({ length: MAX_PLY + 2 }, () => new Int32Array(MAX_MOVES))

    // El estado del tablero vive desempacado y se muta con make/unmake.
    this._locs = new Int8Array(MAX_PIECES)

    this.nodes = 0
    this.ttHits = 0
    this.stalemates = 0
    this.repetitions = 0
    this._budgetMs = 0
    this._tick = 0
    this._t0 = 0
  }

  /** Slot de la tabla. Cualquier hash es correcto: el probe revalida la clave entera. */
  _index(key, turn, depth) {
    const lo = key % 4294967296
    const hi = (key - lo) / 4294967296
    let h = Math.imul(lo, 0x9e3779b1) ^ Math.imul(hi, 0x85ebca6b)
    h = Math.imul(h ^ (h >>> 15), 0xc2b2ae35)
    h ^= h >>> 16
    h ^= Math.imul(depth, 0x27d4eb2f) ^ Math.imul(turn, 0x165667b1)
    return h & this._ttMask
  }

  /**
   * Devuelve `(valor + 1) | (contaminado ? 4 : 0)`.
   *
   * El taint va CODIFICADO EN EL RETORNO a proposito. En C# viaja por un
   * `out bool` (Search.cs:90), que es un local fresco por invocacion; una
   * bandera a nivel modulo en JS la pisaria la recursion en silencio, y el bug
   * seria invisible (la tabla se ensuciaria con valores dependientes del camino).
   * Asi el error es mecanicamente imposible.
   */
  _search(turn, depth, alpha, beta) {
    this.nodes++

    if (this._budgetMs > 0 && (++this._tick & 0xffff) === 0 &&
        performance.now() - this._t0 > this._budgetMs) {
      throw new SearchAborted()
    }

    // Rareza 1 de 3: profundidad 0 devuelve 0 SIN test terminal. La terminalidad
    // siempre se testea en la primera pasada del padre, nunca en la hoja. Un
    // refactor "prolijo" que agregue el test aca cambia los resultados.
    if (depth <= 0) return 1   // (0 + 1), sin contaminar

    const spec = this.spec
    const locs = this._locs
    const key = canonical(spec, locs)

    // Repeticion en el camino actual: tablas, y contamina el nodo.
    if (this.useRepetition) {
      for (let i = 0; i < this._pathLen; i++) {
        if (this._pathKey[i] === key && this._pathTurn[i] === turn) {
          this.repetitions++
          return 1 | 4
        }
      }
    }

    const slot = this._index(key, turn, depth)
    if (this._ttKey[slot] === key && this._ttDepth[slot] === depth && this._ttTurn[slot] === turn) {
      this.ttHits++
      const v = this._ttValue[slot], flag = this._ttFlag[slot]
      if (flag === 0) return v + 1
      if (flag === 1 && v >= beta) return v + 1
      if (flag === 2 && v <= alpha) return v + 1
    }

    const moves = this._moveBuf[this._pathLen]
    const n = generateMoves(spec, locs, turn, moves)
    // Rareza 2 de 3: el ahogado se testea DESPUES del probe de tabla, asi que un
    // ahogado justo en el horizonte puntua 0 y no derrota.
    if (n === 0) {
      this.stalemates++
      return (turn === WHITE ? -1 : 1) + 1
    }

    const maximizing = turn === WHITE
    const winValue = maximizing ? 1 : -1
    const lossValue = -winValue

    // Primera pasada: si alguna jugada gana ya, no hace falta buscar nada mas.
    // El resto se parte en "tranquilas" (siguen la partida) y perdedoras
    // inmediatas (destapan una linea rival y por el medio punto pierden en el acto).
    const quiet = this._quietBuf[this._pathLen]
    let qn = 0
    let anyLoss = false
    for (let i = 0; i < n; i++) {
      const mv = moves[i]
      const piece = mv >> 4, to = mv & 0xf
      const from = locs[piece]
      locs[piece] = to
      const w = winnerAfter(spec, locs, turn)
      locs[piece] = from
      if (w === ONGOING) quiet[qn++] = mv
      else if (w === winValue) return winValue + 1
      else anyLoss = true
    }

    let best = maximizing ? -2 : 2
    if (anyLoss) best = lossValue

    const originalAlpha = alpha, originalBeta = beta
    let tainted = false

    this._pathKey[this._pathLen] = key
    this._pathTurn[this._pathLen] = turn
    this._pathLen++

    try {
      for (let i = 0; i < qn; i++) {
        const mv = quiet[i]
        const piece = mv >> 4, to = mv & 0xf
        const from = locs[piece]
        locs[piece] = to
        const r = this._search(1 - turn, depth - 1, alpha, beta)
        locs[piece] = from

        const v = (r & 3) - 1
        const childTainted = (r & 4) !== 0
        tainted = tainted || childTainted

        if (maximizing) {
          if (v > best) best = v
          if (best > alpha) alpha = best
        } else {
          if (v < best) best = v
          if (best < beta) beta = best
        }
        if (alpha >= beta) {
          // Una sola jugada limpia alcanza para probar la cota: si esa jugada no
          // estaba contaminada, el corte vale para cualquier camino.
          if (!childTainted) tainted = false
          break
        }
      }
    } finally {
      this._pathLen--
    }

    if (best === 2 || best === -2) best = 0

    // Rareza 3 de 3: nada se guarda con depth < 2.
    if (!tainted && depth >= 2) {
      // Con la ventana recortada por el padre el valor puede ser solo una cota.
      let flag = 0
      if (best <= originalAlpha) flag = 2         // cota superior (fallo bajo)
      else if (best >= originalBeta) flag = 1     // cota inferior (fallo alto)
      this._ttKey[slot] = key
      this._ttValue[slot] = best
      this._ttFlag[slot] = flag
      this._ttDepth[slot] = depth
      this._ttTurn[slot] = turn
    }
    return (best + 1) | (tainted ? 4 : 0)
  }

  /** Carga una posicion empaquetada en el estado interno. */
  setPosition(p) {
    unpackInto(this.spec, p, this._locs)
    return this
  }

  getPosition() {
    return pack(this._locs, this.spec.pieceCount)
  }

  /**
   * Resuelve desde la posicion inicial a profundidad fija. Search.cs:79-88.
   * @returns {number} -1 | 0 | 1
   */
  solve(maxDepth, budgetMs = 0) {
    this._budgetMs = budgetMs
    this._tick = 0
    this._t0 = performance.now()
    this._pathLen = 0
    this.setPosition(initialPosition(this.spec))
    return (this._search(WHITE, maxDepth, -1, 1) & 3) - 1
  }

  /**
   * Profundizacion iterativa desde una posicion cualquiera; devuelve el resultado
   * y la profundidad minima a la que queda decidido. Search.cs:196-209.
   *
   * Ojo: resetea _pathLen en cada pasada, asi que NO ve la historia real de la
   * partida. Para eso esta valueWithHistory.
   *
   * @param {number} p @param {number} turn @param {number} maxDepth
   * @returns {{result: number, plies: number}}
   */
  solveFrom(p, turn, maxDepth, budgetMs = 0) {
    this.setPosition(p)
    const terminal = winnerAfter(this.spec, this._locs, 1 - turn)
    if (terminal !== ONGOING) return { result: terminal, plies: 0 }

    this._budgetMs = budgetMs
    this._tick = 0
    this._t0 = performance.now()

    for (let depth = 1; depth <= maxDepth; depth++) {
      this._pathLen = 0
      this.setPosition(p)
      const v = (this._search(turn, depth, -1, 1) & 3) - 1
      if (v !== DRAW) return { result: v, plies: depth }
    }
    return { result: DRAW, plies: maxDepth }
  }

  /**
   * Valor de una jugada concreta respetando la historia ya jugada. Search.cs:212-223.
   * Es lo que necesita la IA del juego para no repetir posiciones sin darse cuenta.
   *
   * @param {number} p @param {number} move @param {number} turn @param {number} depth
   * @param {Float64Array|number[]} histKey @param {Int8Array|number[]} histTurn @param {number} histLen
   */
  valueWithHistory(p, move, turn, depth, histKey, histTurn, histLen) {
    this.setPosition(p)
    const piece = move >> 4, to = move & 0xf
    this._locs[piece] = to

    const w = winnerAfter(this.spec, this._locs, turn)
    if (w !== ONGOING) return w
    if (depth <= 1) return DRAW

    for (let i = 0; i < histLen; i++) {
      this._pathKey[i] = histKey[i]
      this._pathTurn[i] = histTurn[i]
    }
    this._pathLen = histLen
    const v = (this._search(1 - turn, depth - 1, -1, 1) & 3) - 1
    this._pathLen = 0
    return v
  }

  resetCounters() {
    this.nodes = 0
    this.ttHits = 0
    this.stalemates = 0
    this.repetitions = 0
    return this
  }
}

export { CELLS }
