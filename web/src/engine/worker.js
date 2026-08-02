/**
 * El motor corriendo en un Web Worker.
 *
 * Esta es la unica razon de existir del worker: que la busqueda de la IA no
 * compita con el render. Mientras la IA piensa, el tablero sigue interactivo
 * (orbita, inclinacion, explotar una pila). No meter nada mas aca adentro.
 */
import { makeSpecFromLabels } from './spec.js'
import { Searcher } from './searcher.js'
import { Match } from './match.js'
import { DIFFICULTIES, chooseMove } from './ai.js'
import { makeRng } from './rng.js'
import { applyMove } from './position.js'

/** @type {import('./spec.js').GameSpec|null} */
let spec = null
/** @type {Match|null} */
let match = null
/** @type {Searcher|null} */
let searcher = null
let rng = makeRng(1)

/** Info estatica de las piezas: se manda una sola vez, no en cada snapshot. */
function pieceTable() {
  const out = {}
  for (let i = 0; i < spec.pieceCount; i++) out[i] = { owner: spec.owner[i], rank: spec.rank[i] }
  return out
}

const handlers = {
  newGame({ white = '12344', black = '11245', seed = 1 }) {
    spec = makeSpecFromLabels(white, black)
    match = new Match(spec)
    // El Searcher se reusa toda la partida y entre partidas: sus entradas estan
    // indexadas por (canonica, turno, profundidad exacta) y solo se guardan las
    // no contaminadas, asi que valen para cualquier camino. Limpiarla seria
    // tirar trabajo bueno.
    searcher = new Searcher(spec, { ttBits: 18 })
    rng = makeRng(seed >>> 0)
    return {
      pieces: pieceTable(),
      whiteLabel: spec.whiteLabel,
      blackLabel: spec.blackLabel,
      snapshot: match.snapshot(),
    }
  },

  applyMove({ moveId }) {
    match.apply(moveId)
    return { snapshot: match.snapshot() }
  },

  undo({ plies = 1 }) {
    match.undo(plies)
    return { snapshot: match.snapshot() }
  },

  /**
   * Pide la jugada de la IA. `minThinkMs` es un piso deliberado: a ve2 y ve4 la
   * respuesta sale en menos de 2 ms, y una jugada instantanea se lee como glitch
   * en vez de como decision. Ademas filtra la dificultad — un rival que contesta
   * al toque delata que no esta calculando.
   */
  async aiMove({ difficulty = 'dificil', minThinkMs = 450 }) {
    const cfg = DIFFICULTIES[difficulty] ?? DIFFICULTIES.dificil
    const t0 = performance.now()

    const decision = chooseMove({
      spec,
      searcher,
      pos: match.pos,
      turn: match.turn,
      difficulty: cfg,
      rng,
      history: match.history.map((h) => h.pos),
      avoidRepeats: true,
    })
    if (!decision) return { move: null, snapshot: match.snapshot() }

    const elapsed = performance.now() - t0
    if (elapsed < minThinkMs) await new Promise((r) => setTimeout(r, minThinkMs - elapsed))

    match.apply(decision.move)
    return {
      move: decision.move,
      value: decision.value,
      blundered: decision.blundered,
      ms: Math.round(elapsed),
      nodes: searcher.nodes,
      snapshot: match.snapshot(),
    }
  },

  /** Evaluacion de todas las jugadas, para el boton de pista y el analisis final. */
  analyze({ vision = 6 }) {
    const turn = match.turn
    const mio = turn === 0 ? 1 : -1
    const moves = match.legalMoves()
    const scored = moves.map((m) => ({
      ...m,
      value: searcher.solveFrom(applyMove(match.pos, m.id), 1 - turn, vision - 1).result * mio,
    }))
    return { moves: scored }
  },
}

self.onmessage = async (e) => {
  const { id, type, payload } = e.data ?? {}
  try {
    const fn = handlers[type]
    if (!fn) throw new Error(`mensaje desconocido: ${type}`)
    const result = await fn(payload ?? {})
    self.postMessage({ id, ok: true, result })
  } catch (err) {
    self.postMessage({
      id,
      ok: false,
      error: err instanceof Error ? `${err.message}\n${err.stack ?? ''}` : String(err),
    })
  }
}
