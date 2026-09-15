/**
 * Que le queda al primero despues de la respuesta correcta del segundo.
 *
 * Fija un prefijo de jugadas (por descripcion: rango + casilla) y para cada
 * jugada legal del que sigue da el veredicto teorico y el reparto a vision fija.
 *
 * Uso: node despues.mjs "1@A1,5@C3" [ve] [partidas] [profundidad]
 */
const E = new URL('../../../web/src/engine/', import.meta.url).href
const { makeSpecFromLabels } = await import(E + 'spec.js')
const { WHITE, ONGOING, DRAW } = await import(E + 'constants.js')
const { applyMove, loc, HAND, cellName, initialPosition } = await import(E + 'position.js')
const { legalMoves, winnerAfterPacked, canonicalPacked } = await import(E + 'rules.js')
const { Searcher } = await import(E + 'searcher.js')
const { Match } = await import(E + 'match.js')
const { makeRng } = await import(E + 'rng.js')

const prefijo = (process.argv[2] ?? '1@A1,5@C3').split(',').filter(Boolean)
const vision = Number(process.argv[3] ?? 4)
const partidas = Number(process.argv[4] ?? 1000)
const prof = Number(process.argv[5] ?? 12)
const spec = makeSpecFromLabels('12344', '12355', { sinCentro: true })
const celda = (n) => 'ABC'.indexOf(n[0]) * 3 + Number(n[1]) - 1

/** Busca la jugada "rango@casilla" entre las legales. */
function buscar(pos, turn, texto) {
  const [r, c] = texto.split('@')
  const destino = celda(c)
  for (const m of legalMoves(spec, pos, turn)) {
    if ((m & 0xF) === destino && spec.rank[m >> 4] === Number(r) && loc(pos, m >> 4) === HAND) return m
  }
  throw new Error(`no existe la jugada ${texto}`)
}

// Armar el prefijo.
const forzadas = []
{
  let pos = initialPosition(spec), turn = WHITE
  for (const t of prefijo) {
    const m = buscar(pos, turn, t)
    forzadas.push(m)
    pos = applyMove(pos, m)
    turn = 1 - turn
  }
}

let pos = initialPosition(spec), turn = WHITE
for (const m of forzadas) { pos = applyMove(pos, m); turn = 1 - turn }

const quien = turn === WHITE ? 'el primero' : 'el segundo'
const mio = turn === WHITE ? 1 : -1
console.log(`Despues de ${prefijo.join(' ')}, le toca a ${quien}.`)
console.log(`Teoria a ${prof} plies, ${partidas} partidas a ve${vision} con esas jugadas forzadas.\n`)

const hondo = new Searcher(spec, { ttBits: 24 })
const vistos = new Set()
const filas = []
for (const m of legalMoves(spec, pos, turn)) {
  const child = applyMove(pos, m)
  const k = canonicalPacked(spec, child)
  if (vistos.has(k)) continue
  vistos.add(k)
  const term = winnerAfterPacked(spec, child, turn)
  let res, plies
  if (term !== ONGOING) { res = term; plies = 0 }
  else { const r = hondo.solveFrom(child, 1 - turn, prof - 1); res = r.result; plies = r.plies + 1 }
  filas.push({ m, res, plies, rango: spec.rank[m >> 4], celda: m & 0xF, mano: loc(pos, m >> 4) === HAND })
}

/** practica con el prefijo + esta jugada forzados. */
function practica(extra) {
  const searcher = new Searcher(spec, { ttBits: 20 })
  let g = [0, 0], nada = 0
  for (let k = 0; k < partidas; k++) {
    const rng = makeRng(31000 + k)
    const match = new Match(spec)
    for (const m of [...forzadas, extra]) match.apply(m)
    while (!match.snapshot().result && match.snapshot().ply < 40) {
      const t = match.turn, sg = t === WHITE ? 1 : -1
      const moves = Array.from(legalMoves(spec, match.pos, t))
      if (!moves.length) break
      const evs = moves.map((m) => {
        const c = applyMove(match.pos, m)
        const term = winnerAfterPacked(spec, c, t)
        if (term !== ONGOING) return { m, v: term * sg, d: 0 }
        const r = searcher.solveFrom(c, 1 - t, vision - 1)
        return { m, v: r.result * sg, d: r.plies + 1 }
      })
      const mejor = Math.max(...evs.map((e) => e.v))
      let pool = evs.filter((e) => e.v === mejor)
      if (mejor === 1 || mejor === -1) {
        const obj = mejor === 1 ? Math.min(...pool.map((e) => e.d)) : Math.max(...pool.map((e) => e.d))
        pool = pool.filter((e) => e.d === obj)
      }
      match.apply(rng.pick(pool).m)
    }
    const r = match.snapshot().result
    if (r?.winner == null) nada++; else g[r.winner]++
  }
  return [100 * g[0] / partidas, 100 * g[1] / partidas, 100 * nada / partidas]
}

for (const f of filas) {
  const [a, b] = practica(f.m)
  f.g1 = a; f.g2 = b
}
filas.sort((x, y) => (turn === WHITE ? y.g1 - x.g1 : y.g2 - x.g2))

console.log(`${'jugada'.padEnd(26)} ${'teoria'.padEnd(24)} ve${vision} 1o/2o`)
console.log('-'.repeat(66))
for (const f of filas) {
  const et = f.res === DRAW ? `tablas a ${prof}`
           : f.res * mio === 1 ? `gana ${quien} en ${f.plies}`
           : `pierde en ${f.plies}`
  const desc = `${f.mano ? '' : 'mueve '}${f.rango} a ${cellName(f.celda)}`
  console.log(`${desc.padEnd(26)} ${et.padEnd(24)} ${f.g1.toFixed(1)} / ${f.g2.toFixed(1)}`)
}
const mejores = filas.filter((f) => (f.res * mio) >= 0)
console.log(`\nJugadas que no pierden en teoria a ${prof} plies: ${mejores.length} de ${filas.length}.`)
