import { WHITE, ONGOING, DRAW } from './constants.js'
import { applyMove } from './position.js'
import { legalMoves, canonicalPacked, winnerAfterPacked } from './rules.js'

/**
 * La escalera de dificultad, medida — no inventada.
 *
 * Con visión simétrica, 4000 partidas por celda, sobre 12344 vs 11245
 * (reproduce los numeros publicados en Simulacion/README.md, asi que el port
 * es fiel):
 *
 *   vision | 12344 1º / 11245 2º | 11245 1º / 12344 2º | sin definir
 *   -------+---------------------+---------------------+------------
 *   ve2    | 54,5 / 44,8         | 63,1 / 36,4         | 0,8 %
 *   ve4    | 51,6 / 47,5         | 77,6 / 21,8         | 0,9 %
 *   ve6    | 39,0 / 59,5         | 87,8 / 11,7         | 1,4 %
 *   ve8    | 20,1 / 78,4         | 100,0 / 0,0         | 1,4 %
 *   ve10   |  5,1 / 90,1         | 100,0 / 0,0         | 4,8 %
 *
 * ve6 -> ve8 es un salto de 19 puntos: son rivales distintos de verdad.
 * ve8 esta SATURADO: en la sentadura invertida gana 100/0, todas las partidas
 * en exactamente 8 plies, juego determinista. Nada por encima agrega fuerza, y
 * ve10 empeora las cosas — 1 de cada 20 partidas termina en shuffle sin
 * resultado.
 *
 * Por eso la escalera corta en ve8, y el escalon facil se hace con epsilon
 * (jugadas al azar) en vez de bajar mas la vision.
 */
export const DIFFICULTIES = Object.freeze({
  facil: { label: 'Fácil', vision: 2, epsilon: 0.5, neverBlunderIntoLoss: false },
  medio: { label: 'Medio', vision: 2, epsilon: 0, neverBlunderIntoLoss: true },
  dificil: { label: 'Difícil', vision: 4, epsilon: 0, neverBlunderIntoLoss: true },
  muyDificil: { label: 'Muy difícil', vision: 6, epsilon: 0, neverBlunderIntoLoss: true },
  experto: { label: 'Experto', vision: 8, epsilon: 0, neverBlunderIntoLoss: true },
})

/** @typedef {keyof typeof DIFFICULTIES} DifficultyId */

/**
 * Elige una jugada. Port de Program.cs:358-383 (`Practica`), mas tres perillas.
 *
 * El jugador base es deliberadamente simple y SIN heuristica posicional: ve
 * exactamente N plies, y entre lo que a esa distancia se ve igual, elige al azar.
 *
 * @param {object} args
 * @param {import('./spec.js').GameSpec} args.spec
 * @param {import('./searcher.js').Searcher} args.searcher
 * @param {number} args.pos
 * @param {number} args.turn
 * @param {{vision: number, epsilon?: number, neverBlunderIntoLoss?: boolean}} args.difficulty
 * @param {ReturnType<import('./rng.js').makeRng>} args.rng
 * @param {number[]} [args.history]  posiciones ya jugadas, para no repetir
 * @param {boolean} [args.avoidRepeats]
 * @param {boolean} [args.historyAware]
 */
export function chooseMove({
  spec, searcher, pos, turn, difficulty, rng,
  history = [], avoidRepeats = true, historyAware = false,
}) {
  const moves = legalMoves(spec, pos, turn)
  const n = moves.length
  if (n === 0) return null   // ahogado: lo resuelve match.js, no la IA

  const { vision, epsilon = 0, neverBlunderIntoLoss = true } = difficulty
  const mio = turn === WHITE ? 1 : -1

  // Historia como claves canonicas, para el modo historyAware y el desempate.
  const histKey = new Float64Array(history.length)
  const histTurn = new Int8Array(history.length)
  for (let i = 0; i < history.length; i++) {
    histKey[i] = canonicalPacked(spec, history[i])
    histTurn[i] = i % 2 === 0 ? WHITE : 1 - WHITE
  }

  const valores = new Int8Array(n)
  for (let i = 0; i < n; i++) {
    const child = applyMove(pos, moves[i])
    if (historyAware) {
      // Ruta consciente de la historia: Search.cs:212-223. El SolveFrom de C#
      // resetea el camino en cada pasada (Search.cs:204), asi que la IA portada
      // literal NO ve que esta repitiendo una posicion de la partida real.
      valores[i] = searcher.valueWithHistory(
        pos, moves[i], turn, vision, histKey, histTurn, history.length) * mio
    } else {
      // Program.cs:369-373, literal: si la jugada ya termina la partida vale
      // eso; si no, vale lo que diga una busqueda de `vision - 1` plies desde
      // la respuesta del rival.
      const term = winnerAfterPacked(spec, child, turn)
      valores[i] = (term !== ONGOING ? term : searcher.solveFrom(child, 1 - turn, vision - 1).result) * mio
    }
  }

  let mejor = -2
  for (let i = 0; i < n; i++) if (valores[i] > mejor) mejor = valores[i]

  /** @type {number[]} */
  const empatadas = []
  for (let i = 0; i < n; i++) if (valores[i] === mejor) empatadas.push(i)

  // Desempate anti-shuffle: entre las jugadas de igual valor, preferir las que
  // no vuelven a una posicion ya vista en la partida real. Cuesta un canonical()
  // por jugada empatada y no puede debilitar a la IA, porque solo descarta
  // opciones cuando queda alguna del mismo valor.
  let pool = empatadas
  if (avoidRepeats && history.length > 0) {
    const vistas = new Set(histKey)
    const frescas = empatadas.filter((i) => !vistas.has(canonicalPacked(spec, applyMove(pos, moves[i]))))
    if (frescas.length > 0) pool = frescas
  }

  // Epsilon: con probabilidad epsilon, jugar cualquier cosa. Es como se hace el
  // escalon facil sin bajar mas la vision.
  if (epsilon > 0 && rng.next() < epsilon) {
    let candidatas = Array.from({ length: n }, (_, i) => i)
    if (neverBlunderIntoLoss) {
      // Una jugada al azar que regala una linea en un ply se lee como bug, no
      // como "facil". Se excluyen del sorteo.
      const noSuicidas = candidatas.filter((i) => valores[i] > -1)
      if (noSuicidas.length > 0) candidatas = noSuicidas
    }
    const i = rng.pick(candidatas)
    return { move: moves[i], value: valores[i], blundered: valores[i] !== mejor, evaluated: n }
  }

  const i = rng.pick(pool)
  return { move: moves[i], value: mejor, blundered: false, evaluated: n }
}

export { ONGOING }
