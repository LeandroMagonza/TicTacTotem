import test from 'node:test'
import assert from 'node:assert/strict'

import { makeSpecFromLabels } from '../src/engine/spec.js'
import { WHITE, ONGOING, DRAW, WHITE_WIN, BLACK_WIN } from '../src/engine/constants.js'
import { initialPosition, unpack, applyMove, loc } from '../src/engine/position.js'
import { winnerAfter, generateMoves, canonical, legalMoves, stacksOf } from '../src/engine/rules.js'
import { Searcher } from '../src/engine/searcher.js'

/**
 * Minimax pelado: sin poda, sin tabla, sin ordenamiento. Port de Verify.cs:16-39.
 * Es demasiado lento para resolver el juego, pero a poca profundidad da una
 * referencia INDEPENDIENTE contra la cual contrastar el buscador optimizado. Si
 * los dos coinciden en todas las profundidades, la poda y la tabla no estan
 * cambiando el resultado.
 */
let naiveNodes = 0
function naive(spec, locs, turn, depth, path) {
  naiveNodes++
  if (depth <= 0) return DRAW

  const key = canonical(spec, locs)
  for (const [k, t] of path) if (k === key && t === turn) return DRAW

  const buf = new Int32Array(128)
  const n = generateMoves(spec, locs, turn, buf)
  if (n === 0) return turn === WHITE ? -1 : 1   // ahogado: pierde el que no puede mover

  path.push([key, turn])
  const maximizing = turn === WHITE
  let best = maximizing ? -2 : 2
  for (let i = 0; i < n; i++) {
    const mv = buf[i]
    const piece = mv >> 4, to = mv & 0xf
    const from = locs[piece]
    locs[piece] = to
    const w = winnerAfter(spec, locs, turn)
    const v = w !== ONGOING ? w : naive(spec, locs, 1 - turn, depth - 1, path)
    locs[piece] = from
    if (maximizing) { if (v > best) best = v } else { if (v < best) best = v }
  }
  path.pop()
  return best === 2 || best === -2 ? DRAW : best
}

test('minimax pelado: mismo veredicto y MISMOS NODOS que C#', () => {
  const spec = makeSpecFromLabels('12344', '11245')

  // El minimax pelado no tiene tabla, ni poda, ni ordenamiento: su recorrido es
  // completamente determinista. Asi que los conteos de C# se pueden asertar
  // EXACTOS, no con tolerancia. Cualquier diferencia en generacion de jugadas,
  // en su orden de emision, o en la deteccion terminal, mueve estos numeros.
  //
  // Referencia, de `tateti-solver verify --white 12344 --black 11245 --max-depth 6`:
  //   1:37  2:1.189  3:30.205  4:673.373  5:11.367.485  6:170.596.385
  // El 6 se deja afuera: son 170 M de nodos, ~45 s hasta en C#.
  const nodosCSharp = [37, 1189, 30205, 673373, 11367485]

  for (let depth = 1; depth <= 5; depth++) {
    naiveNodes = 0
    const slow = naive(spec, unpack(spec, initialPosition(spec)), WHITE, depth, [])
    assert.equal(naiveNodes, nodosCSharp[depth - 1],
      `nodos del pelado a ${depth} plies: JS ${naiveNodes} vs C# ${nodosCSharp[depth - 1]}`)

    const fast = new Searcher(spec, { ttBits: 20 }).solve(depth)
    assert.equal(fast, slow, `discrepancia a ${depth} plies: pelado=${slow} optimizado=${fast}`)
  }
})

test('ancla contra el solver C#: 12344 vs 11245', async (t) => {
  const spec = makeSpecFromLabels('12344', '11245')

  await t.test('gana NEGRO, decidido en 12 plies', () => {
    const s = new Searcher(spec, { ttBits: 22 })
    let decided = 0
    for (let d = 1; d <= 14; d++) {
      if (s.solve(d) !== DRAW) { decided = d; break }
    }
    assert.equal(decided, 12)
    assert.equal(new Searcher(spec, { ttBits: 22 }).solve(12), BLACK_WIN)
  })

  await t.test('conteo de nodos dentro del 1% del solver C#', () => {
    // C#, tabla 2^25: 37 104 418 1151 5817 10289 57391 83558 169557 203167
    //                 332285 534927, total 1.398.701, 2 ahogados.
    // Las profundidades 1-8 tienen que dar EXACTO: a esa altura la tabla no
    // colisiona ni con 2^20, asi que cualquier diferencia seria un bug real de
    // reglas o de poda, no ruido de hashing.
    const exactos = [37, 104, 418, 1151, 5817, 10289, 57391, 83558]
    for (let d = 1; d <= 8; d++) {
      const s = new Searcher(spec, { ttBits: 22 }).resetCounters()
      s.solve(d)
      assert.equal(s.nodes, exactos[d - 1], `nodos a ${d} plies`)
    }

    // A 12 plies la tabla si influye, asi que se compara con tolerancia. Nunca
    // asertar igualdad exacta aca: C# usa 2^25 y otro hash.
    const s12 = new Searcher(spec, { ttBits: 22 }).resetCounters()
    s12.solve(12)
    const ref = 534_927
    assert.ok(Math.abs(s12.nodes - ref) / ref < 0.01,
      `nodos a 12 plies fuera del 1%: ${s12.nodes} vs ${ref}`)
  })

  await t.test('encuentra 2 posiciones sin jugada legal', () => {
    // Simulacion/README.md:173. Los ahogados son rarisimos (0 en 5,5 M de
    // estados enumerados a 8 plies), asi que este numero es la unica evidencia
    // en todo el suite de que la regla se ejerce.
    const s = new Searcher(spec, { ttBits: 22 }).resetCounters()
    s.solve(12)
    assert.equal(s.stalemates, 2)
  })
})

test('el veredicto no depende de la tabla de transposicion', () => {
  const spec = makeSpecFromLabels('12344', '11245')
  // 2^10 fuerza colisiones masivas; 2^22 casi ninguna. El veredicto tiene que
  // ser el mismo: la tabla es una optimizacion, no parte de las reglas.
  const chico = new Searcher(spec, { ttBits: 10 }).resetCounters()
  const grande = new Searcher(spec, { ttBits: 22 }).resetCounters()
  assert.equal(chico.solve(12), BLACK_WIN)
  assert.equal(grande.solve(12), BLACK_WIN)
  assert.notEqual(chico.nodes, grande.nodes, 'si los nodos coinciden, la tabla no esta viva')
})

test('repeticion', async (t) => {
  const spec = makeSpecFromLabels('12344', '11245')

  await t.test('a 12 plies no dispara nunca, y por eso da identico prendida o apagada', () => {
    // Verificado contra el solver C#: `solve --max-depth 14 --tt-bits 22` da
    // 1.399.053 nodos con y sin --no-repetition, byte por byte. Es que a 12
    // plies desde el inicio ninguna linea sobreviviente al alpha-beta llega a
    // repetir: hacen falta 6 plies de ir y volver con piezas ya en el tablero,
    // y las colocaciones desde la mano son irreversibles.
    //
    // Este test fija ese hecho. Si algun dia dispara, algo cambio en la poda o
    // en la generacion de jugadas y hay que entender que.
    const con = new Searcher(spec, { ttBits: 22, useRepetition: true }).resetCounters()
    const sin = new Searcher(spec, { ttBits: 22, useRepetition: false }).resetCounters()
    assert.equal(con.solve(12), BLACK_WIN)
    assert.equal(sin.solve(12), BLACK_WIN)
    assert.equal(con.repetitions, 0)
    assert.equal(con.nodes, sin.nodes)
  })

  await t.test('a 14 plies si dispara, y ahi se nota que el flag esta cableado', () => {
    const con = new Searcher(spec, { ttBits: 22, useRepetition: true }).resetCounters()
    const sin = new Searcher(spec, { ttBits: 22, useRepetition: false }).resetCounters()
    con.solve(14)
    sin.solve(14)
    assert.ok(con.repetitions > 0, 'la deteccion de repeticion no se ejercita')
    assert.notEqual(con.nodes, sin.nodes, 'si los nodos coinciden, el flag no esta cableado')
  })

  await t.test('una victoria forzada real no depende del camino', () => {
    const con = new Searcher(spec, { ttBits: 20, useRepetition: true })
    const sin = new Searcher(spec, { ttBits: 20, useRepetition: false })
    assert.equal(con.solve(12), BLACK_WIN)
    assert.equal(sin.solve(12), BLACK_WIN)
  })
})

test('las tres rarezas de Search.cs siguen vivas', async (t) => {
  const spec = makeSpecFromLabels('12344', '11245')

  await t.test('profundidad 0 devuelve 0 aun sobre una posicion ya ganada', () => {
    // Search.cs:97. La terminalidad se testea SOLO en la primera pasada del
    // padre, nunca en la hoja. Agregar el test terminal aca cambia resultados.
    const s = new Searcher(spec, { ttBits: 12 })
    let p = initialPosition(spec)
    // Armar una linea blanca destapada: W1,W2,W3 en la fila de arriba.
    const idx = (o, r) => { for (let i = 0; i < spec.pieceCount; i++) if (spec.owner[i] === o && spec.rank[i] === r) return i; throw 0 }
    p = applyMove(p, (idx(0, 1) << 4) | 0)
    p = applyMove(p, (idx(0, 2) << 4) | 1)
    p = applyMove(p, (idx(0, 3) << 4) | 2)
    assert.equal(winnerAfter(spec, unpack(spec, p), WHITE), WHITE_WIN, 'la linea existe')

    s.setPosition(p)
    assert.equal((s._search(1, 0, -1, 1) & 3) - 1, DRAW,
      'a profundidad 0 tiene que devolver 0, no la victoria')
  })

  await t.test('nada se guarda en la tabla con profundidad < 2', () => {
    // Search.cs:178. Guardar entradas de profundidad 1 no sirve de nada (estan
    // a una generacion de distancia) y desperdicia slots.
    const s = new Searcher(spec, { ttBits: 12 })
    s.solve(1)
    const usados = [...s._ttKey].filter((k) => k !== -1).length
    assert.equal(usados, 0)
  })

  await t.test('solveFrom detecta la posicion terminal con el mover anterior', () => {
    // Search.cs:197 usa `1 - turn`: el que llego a esta posicion es el que movio.
    const s = new Searcher(spec, { ttBits: 12 })
    let p = initialPosition(spec)
    const idx = (o, r) => { for (let i = 0; i < spec.pieceCount; i++) if (spec.owner[i] === o && spec.rank[i] === r) return i; throw 0 }
    p = applyMove(p, (idx(0, 1) << 4) | 0)
    p = applyMove(p, (idx(0, 2) << 4) | 1)
    p = applyMove(p, (idx(0, 3) << 4) | 2)
    const r = s.solveFrom(p, 1, 6)   // le toca a negro; blanco acaba de mover
    assert.equal(r.result, WHITE_WIN)
    assert.equal(r.plies, 0, 'terminal se reconoce sin buscar')
  })
})

test('la regla del ahogado, sobre las posiciones reales', async (t) => {
  const { readFileSync } = await import('node:fs')
  const fixture = JSON.parse(
    readFileSync(new URL('./fixtures/ahogados.json', import.meta.url), 'utf8'))
  const spec = makeSpecFromLabels(fixture.spec.white, fixture.spec.black)

  await t.test('el solve sigue encontrando exactamente estas dos', () => {
    const s = new Searcher(spec, { ttBits: 22 }).resetCounters()
    s.stalemateLog = []
    s.solve(12)
    const vistas = new Set(s.stalemateLog.map((e) => `${e.pos}:${e.turn}`))
    const esperadas = new Set(fixture.posiciones.map((p) => `${p.pos}:${p.turn}`))
    assert.deepEqual([...vistas].sort(), [...esperadas].sort())
  })

  for (const caso of fixture.posiciones) {
    await t.test(`pos ${caso.pos}: sin jugadas y derrota para el que mueve`, () => {
      assert.equal(legalMoves(spec, caso.pos, caso.turn).length, 0)

      // Y el rival SI tiene jugadas: si no, seria una posicion muerta y no un
      // ahogado de verdad.
      assert.ok(legalMoves(spec, caso.pos, 1 - caso.turn).length >= 0)

      // Nadie hizo linea: se pierde por no poder mover, no por el tablero.
      assert.equal(winnerAfter(spec, unpack(spec, caso.pos), 1 - caso.turn), ONGOING)

      // A cualquier profundidad >= 1 el buscador la puntua como derrota del que
      // no puede mover. A profundidad 0 devuelve 0, que es la rareza de
      // Search.cs:97 y esta fijada aparte.
      const s = new Searcher(spec, { ttBits: 14 })
      for (const d of [1, 2, 5]) {
        s.setPosition(caso.pos)
        const v = (s._search(caso.turn, d, -1, 1) & 3) - 1
        assert.equal(v, caso.turn === WHITE ? -1 : 1, `a profundidad ${d}`)
      }
      s.setPosition(caso.pos)
      assert.equal((s._search(caso.turn, 0, -1, 1) & 3) - 1, DRAW,
        'a profundidad 0 tiene que dar 0, no derrota')
    })
  }

  await t.test('el tablero esta lleno y al que se ahoga le sobra una pieza', () => {
    for (const caso of fixture.posiciones) {
      const pilas = stacksOf(spec, caso.pos)
      assert.ok(pilas.every((p) => p.length === 1), 'el tablero tendria que estar lleno y plano')

      let enMano = 0
      for (let i = 0; i < spec.pieceCount; i++) {
        if (loc(caso.pos, i) === 15) { enMano++; assert.equal(spec.owner[i], caso.turn) }
      }
      // 10 piezas, 9 casillas: siempre sobra exactamente una.
      assert.equal(enMano, 1)
    }
  })
})

test('solveFrom sobre la posicion inicial coincide con solve', () => {
  const spec = makeSpecFromLabels('12344', '11245')
  const s = new Searcher(spec, { ttBits: 20 })
  const r = s.solveFrom(initialPosition(spec), WHITE, 12)
  assert.equal(r.result, BLACK_WIN)
  assert.equal(r.plies, 12)
})
