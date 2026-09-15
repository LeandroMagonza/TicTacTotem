/**
 * Dos preguntas que la tasa de victorias no contesta:
 *
 *  1. Cuando entrar al centro es LEGAL, ¿la mejor jugada al centro vale lo mismo
 *     que la mejor jugada fuera del centro? Comparado con busqueda profunda, no
 *     con la vision del bot, para separar "es malo" de "no se ve".
 *  2. ¿Como terminan las partidas? Linea propia, destape, o doble linea.
 *
 * Uso: node centro_teoria.mjs [ve] [partidas] [profundidad]
 */
const E = new URL('../../../web/src/engine/', import.meta.url).href
const { makeSpecFromLabels } = await import(E + 'spec.js')
const { WHITE, ONGOING } = await import(E + 'constants.js')
const { applyMove, loc, HAND } = await import(E + 'position.js')
const { legalMoves, winnerAfterPacked } = await import(E + 'rules.js')
const { Searcher } = await import(E + 'searcher.js')
const { Match } = await import(E + 'match.js')
const { makeRng } = await import(E + 'rng.js')

const vision = Number(process.argv[2] ?? 6)
const partidas = Number(process.argv[3] ?? 80)
const prof = Number(process.argv[4] ?? 10)
const CENTRO = 4
const spec = makeSpecFromLabels('12344', '12355', { sinCentro: true })
const searcher = new Searcher(spec, { ttBits: 22 })
const hondo = new Searcher(spec, { ttBits: 24 })

/** Valor de cada jugada a la profundidad que se pida. */
function valores(s, pos, turn, d) {
  // legalMoves devuelve una vista tipada: mapearla directo convierte los objetos a NaN.
  const moves = Array.from(legalMoves(spec, pos, turn))
  const mio = turn === WHITE ? 1 : -1
  return moves.map((m) => {
    const child = applyMove(pos, m)
    const term = winnerAfterPacked(spec, child, turn)
    if (term !== ONGOING) return { m, v: term * mio, plies: 0 }
    const r = s.solveFrom(child, 1 - turn, d - 1)
    return { m, v: r.result * mio, plies: r.plies + 1 }
  })
}

function elegir(evs, rng) {
  const mejor = Math.max(...evs.map((e) => e.v))
  let pool = evs.filter((e) => e.v === mejor)
  if (mejor === 1 || mejor === -1) {
    const obj = mejor === 1 ? Math.min(...pool.map((e) => e.plies)) : Math.max(...pool.map((e) => e.plies))
    pool = pool.filter((e) => e.plies === obj)
  }
  return rng.pick(pool).m
}

const cmp = { peor: 0, igual: 0, mejor: 0 }
// Control: mover una pieza ya puesta contra colocar una de la mano, sin mirar
// a donde. Separa "el centro es malo" de "gastar el turno moviendo es malo".
const ctl = { peor: 0, igual: 0, mejor: 0 }
const ctlPorPly = new Map()
const porPly = new Map()
const razones = new Map()
let oportunidades = 0, turnos = 0

for (let g = 0; g < partidas; g++) {
  const rng = makeRng(9000 + g)
  const match = new Match(spec)
  while (!match.snapshot().result && match.snapshot().ply < 40) {
    const turn = match.turn
    const evs = valores(searcher, match.pos, turn, vision)
    if (!evs.length) break
    turnos++

    const alCentro = evs.filter((e) => (e.m & 0xF) === CENTRO)
    if (alCentro.length) {
      oportunidades++
      // Comparacion HONDA: el mejor centro contra el mejor no-centro.
      const hondos = valores(hondo, match.pos, turn, prof)
      const c = Math.max(...hondos.filter((e) => (e.m & 0xF) === CENTRO).map((e) => e.v))
      const f = Math.max(...hondos.filter((e) => (e.m & 0xF) !== CENTRO).map((e) => e.v))
      const k = c < f ? 'peor' : c > f ? 'mejor' : 'igual'
      cmp[k]++
      const ply = match.snapshot().ply + 1
      const acc = porPly.get(ply) ?? { peor: 0, igual: 0, mejor: 0 }
      acc[k]++
      porPly.set(ply, acc)
    }
    const mueven = evs.filter((e) => loc(match.pos, e.m >> 4) !== HAND)
    if (mueven.length && mueven.length < evs.length) {
      const hondos = valores(hondo, match.pos, turn, prof)
      const mv = Math.max(...hondos.filter((e) => loc(match.pos, e.m >> 4) !== HAND).map((e) => e.v))
      const cl = Math.max(...hondos.filter((e) => loc(match.pos, e.m >> 4) === HAND).map((e) => e.v))
      const k = mv < cl ? 'peor' : mv > cl ? 'mejor' : 'igual'
      ctl[k]++
      const ply = match.snapshot().ply + 1
      const acc = ctlPorPly.get(ply) ?? { peor: 0, igual: 0, mejor: 0 }
      acc[k]++
      ctlPorPly.set(ply, acc)
    }
    match.apply(elegir(evs, rng))
  }
  const res = match.snapshot().result
  if (res) {
    const k = `${res.reason} / gana ${res.winner == null ? 'nadie' : res.winner === 0 ? 'el 1o' : 'el 2o'}`
    razones.set(k, (razones.get(k) ?? 0) + 1)
  }
}

console.log(`12344 vs 12355 con la regla, ve${vision}, ${partidas} partidas, comparacion a ${prof} plies.\n`)
console.log(`Turnos jugados: ${turnos}. Turnos en los que entrar al centro era legal: ${oportunidades} ` +
            `(${(100 * oportunidades / turnos).toFixed(0)} %).`)
const tot = cmp.peor + cmp.igual + cmp.mejor
console.log(`De esos, la mejor jugada al centro es, contra la mejor de afuera:`)
console.log(`  peor   ${cmp.peor.toString().padStart(4)}  ${(100 * cmp.peor / tot).toFixed(0)} %`)
console.log(`  igual  ${cmp.igual.toString().padStart(4)}  ${(100 * cmp.igual / tot).toFixed(0)} %`)
console.log(`  mejor  ${cmp.mejor.toString().padStart(4)}  ${(100 * cmp.mejor / tot).toFixed(0)} %`)
console.log(`\npor ply:`)
for (const ply of [...porPly.keys()].sort((a, b) => a - b)) {
  const a = porPly.get(ply)
  const t = a.peor + a.igual + a.mejor
  console.log(`  ply ${String(ply).padStart(2)}  ${String(t).padStart(4)} casos   ` +
              `peor ${(100 * a.peor / t).toFixed(0).padStart(3)} %   igual ${(100 * a.igual / t).toFixed(0).padStart(3)} %   ` +
              `mejor ${(100 * a.mejor / t).toFixed(0).padStart(3)} %`)
}
const ctlTot = ctl.peor + ctl.igual + ctl.mejor
console.log(`\ncontrol: mover una pieza ya puesta contra colocar una de la mano (${ctlTot} turnos):`)
console.log(`  mover es peor ${(100 * ctl.peor / ctlTot).toFixed(0)} %   igual ${(100 * ctl.igual / ctlTot).toFixed(0)} %   mejor ${(100 * ctl.mejor / ctlTot).toFixed(0)} %`)
for (const ply of [...ctlPorPly.keys()].sort((a, b) => a - b).slice(0, 10)) {
  const a = ctlPorPly.get(ply)
  const t = a.peor + a.igual + a.mejor
  console.log(`  ply ${String(ply).padStart(2)}  ${String(t).padStart(4)} casos   ` +
              `peor ${(100 * a.peor / t).toFixed(0).padStart(3)} %   igual ${(100 * a.igual / t).toFixed(0).padStart(3)} %   ` +
              `mejor ${(100 * a.mejor / t).toFixed(0).padStart(3)} %`)
}

console.log(`\ncomo terminan:`)
for (const [k, v] of [...razones.entries()].sort((a, b) => b[1] - a[1])) {
  console.log(`  ${k.padEnd(28)} ${String(v).padStart(4)}  ${(100 * v / partidas).toFixed(0)} %`)
}
