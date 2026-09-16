import test from 'node:test'
import assert from 'node:assert/strict'
import { makeSpecFromLabels } from '../src/engine/spec.js'
import { Match } from '../src/engine/match.js'
import { legalMoves } from '../src/engine/rules.js'
import { loc, HAND } from '../src/engine/position.js'

const spec = makeSpecFromLabels('12344', '12355', { sinCentro: true })

test('las piezas identicas de la mano se pueden colocar las dos, no solo la primera', async (t) => {
  // Bug real: el generador emite una colocacion por grupo de piezas identicas
  // (la primera libre), asi que tocar el segundo 4 no hacia nada hasta que se
  // jugaba el primero. Para la busqueda son la misma jugada; para la persona
  // que toca la pantalla, no.
  const match = new Match(spec)
  const ui = match.legalMoves()
  const crudas = legalMoves(spec, match.pos, match.turn)

  await t.test('la UI recibe jugadas para los dos 4 (piezas 3 y 4)', () => {
    const del3 = ui.filter((m) => m.pieceId === 3)
    const del4 = ui.filter((m) => m.pieceId === 4)
    assert.equal(del3.length, 8, 'el primer 4 va a las 8 casillas que no son el centro')
    assert.equal(del4.length, 8, 'el segundo 4 tiene que poder ir a las mismas')
    assert.deepEqual(del4.map((m) => m.to), del3.map((m) => m.to))
    assert.ok(del4.every((m) => m.twinOf === 3), 'las gemelas quedan marcadas')
    assert.ok(del3.every((m) => m.twinOf == null))
    // Y sin las gemelas la lista es la del generador, sin mas ni menos.
    assert.deepEqual(ui.filter((m) => m.twinOf == null).map((m) => m.id), Array.from(crudas))
  })

  await t.test('el generador crudo no cambia: el conteo contra C# sigue exacto', () => {
    assert.equal(crudas.length, 32)
    assert.ok(!crudas.some((mv) => (mv >> 4) === 4), 'el generador sigue emitiendo solo el primer 4')
  })

  await t.test('jugar la gemela pone ESA pieza y deja la otra en la mano', () => {
    const mv = ui.find((m) => m.pieceId === 4 && m.to === 0)
    assert.equal(match.apply(mv.id), null)
    assert.equal(loc(match.pos, 4), 0, 'la pieza 4 quedo en la casilla 0')
    assert.equal(loc(match.pos, 3), HAND, 'la pieza 3 sigue en la mano')
    const snap = match.snapshot()
    assert.deepEqual(snap.stacks[0], [4])
    assert.ok(snap.hands[0].includes(3) && !snap.hands[0].includes(4))
    assert.equal(snap.lastMove.pieceId, 4)
  })

  await t.test('y una jugada inventada sigue rechazada', () => {
    assert.throws(() => match.apply((9 << 4) | 4), /ilegal/)
  })
})
