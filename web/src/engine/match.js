import { HAND, CELLS, WHITE, BLACK, ONGOING } from './constants.js'
import { initialPosition, applyMove, loc, cellName } from './position.js'
import { legalMoves, winnerAfterPacked, canonicalPacked, stacksOf, lineReport } from './rules.js'

/** Tope duro de plies, como red de seguridad. Ninguna partida real se acerca. */
const PLY_CAP = 60

/**
 * @typedef {object} MoveInfo
 * @property {number} id            el codigo de jugada, (pieza << 4) | celda
 * @property {number} pieceId
 * @property {number} rank
 * @property {'hand'|number} from
 * @property {number} to
 * @property {null|'win'|'loss'} immediateResult  desde el punto de vista del que mueve
 * @property {number} [twinOf]      colocacion de una pieza identica a la de esta otra jugada
 */

/**
 * Estado de una partida: historia, repeticion, ahogado, undo.
 *
 * No tiene equivalente en C#. El solver nunca necesito contestar "¿esta partida
 * termino por repeticion?" ni "¿de quien es el turno despues de un undo?".
 */
export class Match {
  /**
   * @param {import('./spec.js').GameSpec} spec
   */
  constructor(spec) {
    this.spec = spec
    this.reset()
  }

  reset() {
    const p = initialPosition(this.spec)
    /** @type {{pos: number, turn: number, canon: number}[]} */
    this.history = [{ pos: p, turn: WHITE, canon: canonicalPacked(this.spec, p) }]
    /** @type {number[]} jugadas aplicadas, para el log */
    this.moves = []
    this.seq = 0
    /** @type {null | {winner: number|null, reason: string, lines: number[][]}} */
    this.result = null
    return this
  }

  /**
   * Salta a una posicion arbitraria, descartando la historia.
   *
   * Es la unica forma de llegar a posiciones que jugando no se alcanzan nunca.
   * El caso que importa es el ahogado: ocurre 2 veces en los 1,4 millones de
   * nodos que resuelven el juego, y 0 veces en los 5,5 millones de estados
   * alcanzables en 8 plies. Sin esto, la pantalla de "te quedaste sin jugadas"
   * se enviaria sin haberla visto nunca.
   *
   * @param {number} pos @param {number} turn
   */
  loadPosition(pos, turn) {
    const canon = canonicalPacked(this.spec, pos)
    this.history = [{ pos, turn, canon }]
    this.moves = []
    this.seq++
    // Se evalua con el rival como ultimo en mover, que es lo que asume el resto
    // del motor para una posicion recien alcanzada.
    this.result = this._evaluate(pos, 1 - turn, turn, canon)
    return this.result
  }

  get pos() { return this.history[this.history.length - 1].pos }
  get turn() { return this.history[this.history.length - 1].turn }
  get ply() { return this.history.length - 1 }

  /**
   * Jugadas legales enriquecidas para la UI.
   *
   * El generador emite UNA colocacion por grupo de piezas identicas (la primera
   * libre): para la busqueda las otras son la misma jugada. Para la persona
   * que toca la pantalla no: si tiene dos 4 en la mano y toca el segundo, tiene
   * que poder ponerlo. Aca se agregan esas gemelas, marcadas con `twinOf` para
   * que el analisis pueda seguir mostrando cada jugada una sola vez.
   */
  legalMoves() {
    if (this.result) return []
    const spec = this.spec, p = this.pos, turn = this.turn
    const { pieceCount, owner, rank } = spec
    const out = /** @type {MoveInfo[]} */ ([])
    for (const mv of legalMoves(spec, p, turn)) {
      const pieceId = mv >> 4, to = mv & 0xf
      const from = loc(p, pieceId)
      const w = winnerAfterPacked(spec, applyMove(p, mv), turn)
      // Esto es lo que permite el anillo ambar: avisar antes de mover que la
      // jugada destapa una linea rival, sin que la UI reimplemente ni una regla.
      const immediateResult = w === ONGOING ? null : (w === (turn === WHITE ? 1 : -1) ? 'win' : 'loss')
      out.push({ id: mv, pieceId, rank: rank[pieceId], from: from === HAND ? 'hand' : from, to, immediateResult })
      if (from !== HAND) continue
      // Las gemelas siguen a la pieza en el indice (spec.js: los grupos son
      // contiguos) y dan exactamente la misma posicion salvo permutacion.
      for (let j = pieceId + 1; j < pieceCount && owner[j] === owner[pieceId] && rank[j] === rank[pieceId]; j++) {
        if (loc(p, j) !== HAND) continue
        out.push({ id: (j << 4) | to, pieceId: j, rank: rank[j], from: 'hand', to, immediateResult, twinOf: pieceId })
      }
    }
    return out
  }

  /**
   * Aplica una jugada. Devuelve el resultado si la partida termino.
   * @param {number} moveId
   */
  apply(moveId) {
    if (this.result) throw new Error('la partida ya termino')
    const spec = this.spec, p = this.pos, mover = this.turn

    // Validar contra la lista real (con las gemelas de la mano, que son jugadas
    // tan legales como su representante): la UI no puede inventar jugadas.
    if (!this.legalMoves().some((m) => m.id === moveId)) {
      throw new Error(`jugada ilegal ${moveId} en el ply ${this.ply}`)
    }

    const next = applyMove(p, moveId)
    const turn = 1 - mover
    const canon = canonicalPacked(spec, next)
    this.history.push({ pos: next, turn, canon })
    this.moves.push(moveId)
    this.seq++

    this.result = this._evaluate(next, mover, turn, canon)
    return this.result
  }

  /** @private */
  _evaluate(pos, mover, turn, canon) {
    const spec = this.spec

    // 1. Linea. La regla del medio punto ya esta adentro de winnerAfter.
    const w = winnerAfterPacked(spec, pos, mover)
    if (w !== ONGOING) {
      const { whiteLines, blackLines } = lineReport(spec, pos)
      const winner = w === 1 ? WHITE : BLACK
      const propias = mover === WHITE ? whiteLines : blackLines
      const ajenas = mover === WHITE ? blackLines : whiteLines
      // Tres formas distintas de terminar, y al jugador le importan las tres:
      //   line       hiciste tu linea
      //   uncovered  te moviste y destapaste la del rival
      //   double     quedaron las dos, y por el medio punto pierde el que movio
      const reason = ajenas.length > 0 ? (propias.length > 0 ? 'double' : 'uncovered') : 'line'
      return { winner, reason, lines: winner === WHITE ? whiteLines : blackLines }
    }

    // 2. Ahogado: el que no tiene jugada legal pierde. Se chequea ANTES de
    //    pedirle jugada al proximo jugador. GameManager.cs no hace esto en
    //    ningun lado; era el unico pendiente de codigo del README.
    if (legalMoves(spec, pos, turn).length === 0) {
      return { winner: mover, reason: 'stalemate', lines: [] }
    }

    // 3. Repeticion DOBLE de (posicion canonica, turno) = tablas.
    //    Es exactamente lo que hace el buscador (Search.cs:102-105), asi que el
    //    juego que se juega sigue siendo el juego que se analizo. Si el juego
    //    usara triple repeticion y el motor doble, la IA estaria optimizando
    //    para una regla que el juego no tiene.
    let repeticiones = 0
    for (const h of this.history) if (h.canon === canon && h.turn === turn) repeticiones++
    if (repeticiones >= 2) return { winner: null, reason: 'repetition', lines: [] }

    if (this.ply >= PLY_CAP) return { winner: null, reason: 'cap', lines: [] }
    return null
  }

  /**
   * Deshace `plies` jugadas. En modo vs IA se pasan 2 para volver al turno del
   * humano. Gratis con posiciones empaquetadas, y la tabla de transposicion no
   * necesita invalidarse: sus entradas valen para cualquier camino.
   * @param {number} plies
   */
  undo(plies = 1) {
    const n = Math.min(plies, this.history.length - 1)
    if (n <= 0) return false
    this.history.length -= n
    this.moves.length -= n
    this.result = null
    this.seq++
    return true
  }

  get canUndo() { return this.history.length > 1 }

  /** Estado completo para la UI. La escena tiene que poder reconstruirse de esto. */
  snapshot() {
    const spec = this.spec, p = this.pos
    const hands = { [WHITE]: /** @type {number[]} */ ([]), [BLACK]: /** @type {number[]} */ ([]) }
    for (let i = 0; i < spec.pieceCount; i++) {
      if (loc(p, i) === HAND) hands[spec.owner[i]].push(i)
    }
    return {
      seq: this.seq,
      pos: p,
      turn: this.turn,
      ply: this.ply,
      hands,
      stacks: stacksOf(spec, p),
      legalMoves: this.legalMoves(),
      result: this.result,
      canUndo: this.canUndo,
      lastMove: this.moves.length ? this._describeLast() : null,
    }
  }

  /** @private */
  _describeLast() {
    const mv = this.moves[this.moves.length - 1]
    const before = this.history[this.history.length - 2]
    const pieceId = mv >> 4, to = mv & 0xf
    const from = loc(before.pos, pieceId)
    const spec = this.spec
    const covered = stacksOf(spec, before.pos)[to]
    const top = covered.length ? spec.rank[covered[covered.length - 1]] : null
    return {
      id: mv,
      pieceId,
      rank: spec.rank[pieceId],
      owner: spec.owner[pieceId],
      from: from === HAND ? 'hand' : from,
      to,
      coveredRank: top,
      text: from === HAND
        ? `nivel ${spec.rank[pieceId]} a ${cellName(to)}`
        : `nivel ${spec.rank[pieceId]} de ${cellName(from)} a ${cellName(to)}` +
          (top !== null ? `, tapando un ${top}` : ''),
    }
  }
}

export { CELLS }
