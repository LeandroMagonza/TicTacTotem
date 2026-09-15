/**
 * ¿Conviene correr al centro con la regla puesta?
 *
 * Tres politicas sobre el MISMO evaluador (el de `practica`: valor por signo,
 * desempate por distancia, sorteo entre iguales):
 *   normal  - la del juego
 *   blando  - entre jugadas de igual valor, prefiere las que van al centro.
 *             No puede debilitar: solo desempata.
 *   duro    - si puede ir al centro, va, cueste lo que cueste.
 *
 * Y sobre partidas normales: en que ply se ocupa el centro, quien lo ocupa
 * primero, y si eso se correlaciona con ganar.
 *
 * Uso: node centro.mjs [ve] [partidas]
 */
const E = new URL('../../../web/src/engine/', import.meta.url).href
const { makeSpecFromLabels } = await import(E + 'spec.js')
const { WHITE, ONGOING } = await import(E + 'constants.js')
const { applyMove } = await import(E + 'position.js')
const { legalMoves, winnerAfterPacked } = await import(E + 'rules.js')
const { Searcher } = await import(E + 'searcher.js')
const { Match } = await import(E + 'match.js')
const { makeRng } = await import(E + 'rng.js')

const vision = Number(process.argv[2] ?? 4)
const partidas = Number(process.argv[3] ?? 400)
const CENTRO = 4
const spec = makeSpecFromLabels('12344', '12355', { sinCentro: true })

/** Valor y distancia de cada jugada, igual que el bot del juego. */
function evaluar(searcher, pos, turn) {
  const moves = legalMoves(spec, pos, turn)
  const mio = turn === WHITE ? 1 : -1
  const vals = new Int8Array(moves.length)
  const dist = new Int16Array(moves.length)
  for (let i = 0; i < moves.length; i++) {
    const child = applyMove(pos, moves[i])
    const term = winnerAfterPacked(spec, child, turn)
    if (term !== ONGOING) { vals[i] = term * mio; dist[i] = 0 }
    else {
      const r = searcher.solveFrom(child, 1 - turn, vision - 1)
      vals[i] = r.result * mio
      dist[i] = r.plies + 1
    }
  }
  return { moves, vals, dist }
}

/** Entre las de mejor valor, la victoria mas corta o la derrota mas larga. */
function mejores({ moves, vals, dist }) {
  let mejor = -2
  for (const v of vals) if (v > mejor) mejor = v
  let pool = []
  for (let i = 0; i < moves.length; i++) if (vals[i] === mejor) pool.push(i)
  if (mejor === 1 || mejor === -1) {
    let obj = mejor === 1 ? Infinity : -Infinity
    for (const i of pool) obj = mejor === 1 ? Math.min(obj, dist[i]) : Math.max(obj, dist[i])
    pool = pool.filter((i) => dist[i] === obj)
  }
  return { pool, mejor }
}

function elegir(politica, ev, rng) {
  const { moves } = ev
  const alCentro = []
  for (let i = 0; i < moves.length; i++) if ((moves[i] & 0xF) === CENTRO) alCentro.push(i)

  if (politica === 'duro' && alCentro.length) {
    // Toma el centro igual: entre las que van al centro, la mejor que haya.
    let mejor = -2
    for (const i of alCentro) if (ev.vals[i] > mejor) mejor = ev.vals[i]
    const cand = alCentro.filter((i) => ev.vals[i] === mejor)
    return moves[rng.pick(cand)]
  }
  const { pool } = mejores(ev)
  if (politica === 'blando') {
    const conCentro = pool.filter((i) => (moves[i] & 0xF) === CENTRO)
    if (conCentro.length) return moves[rng.pick(conCentro)]
  }
  return moves[rng.pick(pool)]
}

/** Una partida. `pol[lado]` es la politica de cada bando. */
function jugar(pol, semilla, searcher) {
  const rng = makeRng(semilla)
  const match = new Match(spec)
  let plyCentro = 0, duenoCentro = null, cambios = 0, ultimoDueno = null
  while (!match.snapshot().result) {
    const turn = match.turn
    const ev = evaluar(searcher, match.pos, turn)
    if (!ev.moves.length) break
    match.apply(elegir(pol[turn], ev, rng))
    const s = match.snapshot()
    const pila = s.stacks[CENTRO]
    if (pila.length) {
      const dueno = spec.owner[pila[pila.length - 1]]
      if (plyCentro === 0) { plyCentro = s.ply; duenoCentro = dueno }
      if (ultimoDueno !== null && dueno !== ultimoDueno) cambios++
      ultimoDueno = dueno
    }
    if (s.ply > 40) break
  }
  const res = match.snapshot().result
  return { ganador: res?.winner ?? null, plies: match.snapshot().ply, plyCentro, duenoCentro, cambios,
           finalCentro: ultimoDueno }
}

function serie(pol, etiqueta) {
  const searcher = new Searcher(spec, { ttBits: 20 })
  let g = [0, 0], tablas = 0, conCentro = 0, sumaPly = 0, tomaYgana = 0, tomaTotal = 0
  let sumaCambios = 0
  for (let k = 0; k < partidas; k++) {
    const r = jugar(pol, 5000 + k, searcher)
    if (r.ganador == null) tablas++; else g[r.ganador]++
    if (r.plyCentro) { conCentro++; sumaPly += r.plyCentro; sumaCambios += r.cambios }
    if (r.plyCentro && r.ganador != null) { tomaTotal++; if (r.ganador === r.duenoCentro) tomaYgana++ }
  }
  const pct = (x) => (100 * x / partidas).toFixed(1)
  console.log(
    `${etiqueta.padEnd(30)} 1o ${pct(g[0]).padStart(5)} %   2o ${pct(g[1]).padStart(5)} %   ` +
    `tablas ${pct(tablas).padStart(4)} %   ` +
    `centro ocupado en ${pct(conCentro).padStart(5)} % de las partidas` +
    (conCentro ? `, ply ${(sumaPly / conCentro).toFixed(1)}, cambia ${(sumaCambios / conCentro).toFixed(1)} veces` : ''))
  return { g, tablas, tomaYgana, tomaTotal, conCentro }
}

console.log(`12344 vs 12355, regla del centro, ve${vision}, ${partidas} partidas por serie.\n`)
const base = serie(['normal', 'normal'], 'los dos normales')
console.log()
serie(['blando', 'normal'], '1o prefiere el centro')
serie(['normal', 'blando'], '2o prefiere el centro')
serie(['duro', 'normal'], '1o corre al centro')
serie(['normal', 'duro'], '2o corre al centro')
console.log()
console.log(`En partidas normales, el que ocupa primero el centro gana ` +
            `${(100 * base.tomaYgana / Math.max(1, base.tomaTotal)).toFixed(1)} % de las veces ` +
            `(${base.tomaTotal} partidas decididas con el centro ocupado).`)
