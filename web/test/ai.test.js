import test from 'node:test'
import assert from 'node:assert/strict'

import { makeSpecFromLabels } from '../src/engine/spec.js'
import { WHITE } from '../src/engine/constants.js'
import { applyMove } from '../src/engine/position.js'
import { legalMoves, winnerAfterPacked } from '../src/engine/rules.js'
import { Searcher } from '../src/engine/searcher.js'
import { Match } from '../src/engine/match.js'
import { DIFFICULTIES, chooseMove } from '../src/engine/ai.js'
import { makeRng } from '../src/engine/rng.js'

const spec = makeSpecFromLabels('12344', '12355', { sinCentro: true })

/**
 * Juega al azar hasta que el que mueve tenga una linea disponible, y devuelve
 * esa posicion. Es como se fabrican posiciones "con linea" sin escribirlas a mano.
 */
function posicionConLinea(seed) {
  const rng = makeRng(seed)
  const match = new Match(spec)
  while (!match.snapshot().result) {
    const turn = match.turn, mio = turn === WHITE ? 1 : -1
    const moves = legalMoves(spec, match.pos, turn)
    const inmediatas = moves.filter((m) => winnerAfterPacked(spec, applyMove(match.pos, m), turn) * mio === 1)
    if (inmediatas.length) return { match, inmediatas, rng }
    match.apply(rng.pick(moves))
  }
  return null
}

test('la IA cobra la victoria inmediata en vez de diferirla', async (t) => {
  // Reporte real: en Experto "puede hacer 3 en linea y hace otra jugada". Una
  // linea inmediata y una victoria forzada a 7 plies valen lo mismo por signo,
  // y el sorteo entre iguales la dejaba pasar la mitad de las veces (175 de 357
  // turnos en 200 partidas), con 5 partidas terminadas en tablas por repeticion.
  const searcher = new Searcher(spec, { ttBits: 18 })
  let casos = 0
  for (let seed = 1; seed <= 80 && casos < 25; seed++) {
    const p = posicionConLinea(500 + seed)
    if (!p) continue
    casos++
    for (const dif of ['medio', 'dificil', 'experto']) {
      const d = chooseMove({
        spec, searcher, pos: p.match.pos, turn: p.match.turn,
        difficulty: DIFFICULTIES[dif], rng: p.rng, history: p.match.history.map((h) => h.pos),
      })
      assert.ok(p.inmediatas.includes(d.move), `${dif}: con linea disponible la juega (semilla ${500 + seed})`)
      assert.equal(d.plies, 0)
      assert.equal(d.value, 1)
    }
  }
  assert.ok(casos >= 15, `pocas posiciones con linea para probar: ${casos}`)

  await t.test('la distancia al resultado viaja con la decision', () => {
    const rng = makeRng(3)
    const match = new Match(spec)
    const d = chooseMove({
      spec, searcher, pos: match.pos, turn: match.turn,
      difficulty: DIFFICULTIES.dificil, rng, history: [match.pos],
    })
    assert.ok(Number.isInteger(d.plies) && d.plies >= 1 && d.plies <= 4)
  })
})
