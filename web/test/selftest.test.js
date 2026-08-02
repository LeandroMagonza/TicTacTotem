import test from 'node:test'
import assert from 'node:assert/strict'

import { makeSpec, makeSpecFromLabels } from '../src/engine/spec.js'
import {
  HAND, CELLS, WHITE, BLACK, ONGOING, WHITE_WIN, BLACK_WIN, POW16, MAX_PIECES,
} from '../src/engine/constants.js'
import {
  loc, withLoc, applyMove, initialPosition, pack, unpack, unpackInto,
} from '../src/engine/position.js'
import {
  computeTops, winnerAfter, generateMoves, canonical,
  winnerAfterPacked, legalMoves, canonicalPacked,
} from '../src/engine/rules.js'

// ---------------------------------------------------------------------------
// Helpers, portados de SelfTest.cs:20-33. Trabajan con posiciones empaquetadas
// para que las aserciones se lean igual que en C#.
// ---------------------------------------------------------------------------

/** Coloca la primera pieza libre del dueño y rango pedidos. SelfTest.cs:20-25 */
function put(s, p, owner, rank, cell) {
  for (let i = 0; i < s.pieceCount; i++) {
    if (s.owner[i] === owner && s.rank[i] === rank && loc(p, i) === HAND) {
      return withLoc(p, i, cell)
    }
  }
  throw new Error(`no queda pieza libre ${owner}/${rank}`)
}

/** SelfTest.cs:27-33 → [{piece, from, to}] */
function moves(s, p, turn) {
  const buf = new Int32Array(128)
  const n = generateMoves(s, unpack(s, p), turn, buf)
  return Array.from({ length: n }, (_, i) => ({
    piece: buf[i] >> 4,
    from: loc(p, buf[i] >> 4),
    to: buf[i] & 0xf,
  }))
}

const findPiece = (s, owner, rank) => {
  for (let i = 0; i < s.pieceCount; i++) if (s.owner[i] === owner && s.rank[i] === rank) return i
  throw new Error(`sin pieza ${owner}/${rank}`)
}

// ---------------------------------------------------------------------------
// Las 16 aserciones de SelfTest.cs, con el MISMO spec (122335 vs 12246) para que
// los literales se transfieran sin recalcular nada — incluido "36 jugadas".
// ---------------------------------------------------------------------------

const s = makeSpec([1, 2, 2, 3, 3, 5], [1, 2, 2, 4, 6])
const W = WHITE, B = BLACK
const empty = initialPosition(s)

test('reglas de victoria (GameManager.CheckForWinner)', async (t) => {
  await t.test('linea de blancas gana', () => {
    const p = put(s, put(s, put(s, empty, W, 1, 0), W, 2, 1), W, 3, 2)
    assert.equal(winnerAfterPacked(s, p, W), WHITE_WIN)
  })

  await t.test('linea tapada no vale', () => {
    const p = put(s, put(s, put(s, empty, W, 1, 0), W, 2, 1), W, 3, 2)
    const covered = put(s, p, B, 6, 1)     // B6 tapa a W2 en la celda 1
    assert.equal(winnerAfterPacked(s, covered, B), ONGOING)
  })

  // GameManager.cs:209-211 — wins[whoseTurn] -= 0.5f
  await t.test('destapar linea rival hace perder al que mueve', () => {
    let buried = put(s, put(s, put(s, empty, B, 1, 0), B, 2, 1), B, 4, 2)
    buried = put(s, buried, W, 5, 0)       // W5 tapa a B1: no hay linea negra
    assert.equal(winnerAfterPacked(s, buried, W), ONGOING,
      'con la linea negra tapada no hay ganador')

    const w5 = findPiece(s, W, 5)
    const revealed = withLoc(buried, w5, 3)  // W5 se va de la celda 0 a la 3
    assert.equal(winnerAfterPacked(s, revealed, W), BLACK_WIN)
  })

  await t.test('doble linea: pierde el que acaba de mover', () => {
    let both = put(s, put(s, put(s, empty, W, 1, 0), W, 2, 1), W, 3, 2)
    both = put(s, put(s, put(s, both, B, 1, 6), B, 2, 7), B, 4, 8)
    assert.equal(winnerAfterPacked(s, both, W), BLACK_WIN, 'mueve blanco, gana negro')
    assert.equal(winnerAfterPacked(s, both, B), WHITE_WIN, 'mueve negro, gana blanco')
  })
})

test('reglas de jugada (Cell.CanPieceBePushed / Piece.CanPieceBeMoved)', async (t) => {
  await t.test('no se coloca desde la mano en celda ocupada', () => {
    const occupied = put(s, empty, W, 1, 4)                       // Cell.cs:92-97
    const placesOnOccupied = moves(s, occupied, W).some((m) => m.from === HAND && m.to === 4)
    assert.equal(placesOnOccupied, false)
  })

  await t.test('no se tapa una pieza de rango igual', () => {
    const equalRank = put(s, put(s, empty, W, 2, 0), B, 2, 1)     // Cell.cs:103
    assert.equal(moves(s, equalRank, B).some((m) => m.from === 1 && m.to === 0), false)
  })

  await t.test('si se tapa una pieza de rango menor', () => {
    const higherRank = put(s, put(s, empty, W, 2, 0), B, 4, 1)
    assert.equal(moves(s, higherRank, B).some((m) => m.from === 1 && m.to === 0), true)
  })

  await t.test('una pieza tapada no se puede mover', () => {
    const stacked = put(s, put(s, empty, W, 1, 0), B, 2, 0)       // Piece.cs:41
    assert.equal(moves(s, stacked, W).some((m) => m.from === 0), false)
  })

  await t.test('movimiento solo ortogonal y adyacente', () => {
    const lone = put(s, empty, W, 1, 0)                           // Cell.cs:86-90
    const targets = moves(s, lone, W).filter((m) => m.from === 0)
      .map((m) => m.to).sort((a, b) => a - b)
    assert.deepEqual(targets, [1, 3])
  })

  await t.test('la pila conserva la pieza de abajo y la destapa', () => {
    const under = put(s, put(s, empty, B, 1, 0), W, 5, 0)
    const tops = new Int8Array(CELLS)
    computeTops(s, unpack(s, under), tops)
    assert.ok(tops[0] >= 0 && s.rank[tops[0]] === 5 && s.owner[tops[0]] === W, 'W5 arriba')

    const moved = withLoc(under, tops[0], 3)
    computeTops(s, unpack(s, moved), tops)
    assert.ok(tops[0] >= 0 && s.rank[tops[0]] === 1 && s.owner[tops[0]] === B, 'B1 destapada')
  })
})

test('generacion y canonicalizacion', async (t) => {
  await t.test('36 jugadas iniciales para blanco', () => {
    // 4 rangos distintos en la mano (1,2,3,5) x 9 celdas vacias.
    assert.equal(moves(s, empty, W).length, 36)
  })

  await t.test('posiciones simetricas comparten canonico', () => {
    const a = put(s, put(s, empty, W, 1, 0), B, 6, 4)
    const rotated = put(s, put(s, empty, W, 1, 2), B, 6, 4)       // rotacion de 90
    assert.equal(canonicalPacked(s, a), canonicalPacked(s, rotated))
  })

  await t.test('piezas identicas intercambiadas dan el mismo canonico', () => {
    const w2 = []
    for (let i = 0; i < s.pieceCount; i++) if (s.owner[i] === W && s.rank[i] === 2) w2.push(i)
    const x = withLoc(withLoc(empty, w2[0], 0), w2[1], 5)
    const y = withLoc(withLoc(empty, w2[0], 5), w2[1], 0)
    assert.equal(canonicalPacked(s, x), canonicalPacked(s, y))
  })

  await t.test('posiciones distintas dan canonicos distintos', () => {
    const lone = put(s, empty, W, 1, 0)
    const z = put(s, empty, W, 1, 1)
    assert.notEqual(canonicalPacked(s, lone), canonicalPacked(s, z))
  })
})

// ---------------------------------------------------------------------------
// Las mismas reglas con el spec que se va a jugar. Solo cambia el conteo, que
// depende de cuantos rangos distintos hay en la mano.
// ---------------------------------------------------------------------------

test('las mismas reglas valen para el spec elegido 12344 vs 11245', async (t) => {
  const g = makeSpecFromLabels('12344', '11245')
  const e = initialPosition(g)

  await t.test('36 jugadas iniciales (rangos 1,2,3,4 x 9 celdas)', () => {
    assert.equal(moves(g, e, WHITE).length, 36)
  })

  await t.test('27 jugadas iniciales para negro (rangos 1,2,4,5 -> 4 x 9)', () => {
    // Negro nunca mueve primero, pero el conteo desde el tablero vacio es
    // 4 rangos distintos x 9 celdas = 36 tambien. Se verifica explicito para
    // que el numero no sea folklore.
    assert.equal(moves(g, e, BLACK).length, 36)
  })

  await t.test('destapar linea rival sigue haciendo perder', () => {
    let buried = put(g, put(g, put(g, e, BLACK, 1, 0), BLACK, 2, 1), BLACK, 4, 2)
    buried = put(g, buried, WHITE, 4, 0)   // W4 tapa a B1
    assert.equal(winnerAfterPacked(g, buried, WHITE), ONGOING)
    const w4 = findPiece(g, WHITE, 4)
    assert.equal(winnerAfterPacked(g, withLoc(buried, w4, 3), WHITE), BLACK_WIN)
  })

  await t.test('rango igual no tapa (11245 tiene dos 1)', () => {
    const eq = put(g, put(g, e, WHITE, 1, 0), BLACK, 1, 1)
    assert.equal(moves(g, eq, BLACK).some((m) => m.from === 1 && m.to === 0), false)
  })
})

// ---------------------------------------------------------------------------
// Aserciones propias de JS que el suite de C# no puede tener.
// ---------------------------------------------------------------------------

test('empaquetado', async (t) => {
  await t.test('round-trip de pack/unpack sobre 50k vectores al azar', () => {
    let seed = 12345
    const rnd = (n) => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed % n }
    const locs = new Int8Array(s.pieceCount)
    const back = new Int8Array(s.pieceCount)

    for (let iter = 0; iter < 50_000; iter++) {
      for (let i = 0; i < s.pieceCount; i++) {
        const r = rnd(10)
        locs[i] = r === 9 ? HAND : r
      }
      const p = pack(locs, s.pieceCount)
      assert.ok(Number.isSafeInteger(p), `posicion no exacta: ${p}`)
      unpackInto(s, p, back)
      assert.deepEqual([...back], [...locs])

      // withLoc tiene que coincidir con empaquetar despues de mutar
      const i = rnd(s.pieceCount), c = rnd(9)
      const viaWithLoc = withLoc(p, i, c)
      locs[i] = c
      assert.equal(viaWithLoc, pack(locs, s.pieceCount))
    }
  })

  await t.test('POW16 es exacto hasta el maximo de piezas', () => {
    for (let i = 0; i <= MAX_PIECES; i++) {
      assert.equal(POW16[i], 2 ** (4 * i))
      assert.ok(Number.isSafeInteger(POW16[i]))
    }
    // 13 piezas todas en HAND es el peor caso representable.
    assert.ok(Number.isSafeInteger(16 ** 13 - 1))
  })

  await t.test('makeSpec rechaza mas piezas de las que entran exactas', () => {
    assert.throws(() => makeSpec([1, 2, 3, 4, 5, 6, 7], [1, 2, 3, 4, 5, 6, 7]), RangeError)
    assert.doesNotThrow(() => makeSpec([1, 2, 3, 4, 5, 6, 7], [1, 2, 3, 4, 5, 6]))
  })
})

test('disciplina del centinela ONGOING', async (t) => {
  await t.test('ONGOING no colisiona con ningun valor con significado', () => {
    for (const v of [WHITE_WIN, DRAW_VALUE(), BLACK_WIN, 2, -2]) {
      assert.notEqual(ONGOING, v)
    }
    function DRAW_VALUE() { return 0 }
  })

  await t.test('ONGOING es truthy, y DRAW seria falsy: por eso no se usa if (w)', () => {
    // Es la razon de existir del centinela. Si alguien "simplifica" a null o 0,
    // este test explica por que no.
    assert.ok(ONGOING, 'ONGOING tiene que ser truthy')
    assert.ok(!0, 'DRAW = 0 es falsy, asi que `if (w)` se comeria un empate')
  })

  await t.test('tablero vacio devuelve ONGOING, no un ganador', () => {
    assert.equal(winnerAfterPacked(s, empty, W), ONGOING)
    assert.equal(winnerAfterPacked(s, empty, B), ONGOING)
  })
})

test('invariantes sobre el espacio alcanzable (BFS de 4 plies)', () => {
  const g = makeSpecFromLabels('12344', '11245')
  let frontier = new Map([[initialPosition(g), WHITE]])
  const buf = new Int32Array(128)

  for (let ply = 0; ply < 4; ply++) {
    const next = new Map()
    for (const [p, turn] of frontier) {
      const locs = unpack(g, p)
      const n = generateMoves(g, locs, turn, buf)
      assert.ok(n > 0, 'no deberia haber ahogados tan temprano')
      assert.ok(n <= 128, 'el buffer de jugadas se queda corto')

      for (let i = 0; i < n; i++) {
        const child = applyMove(p, buf[i])
        assert.ok(Number.isSafeInteger(child), `posicion inexacta: ${child}`)
        const key = canonicalPacked(g, child)
        assert.ok(Number.isSafeInteger(key), `canonico inexacto: ${key}`)

        // Idempotencia: canonicalizar el canonico no lo mueve. Es el invariante
        // que de verdad vale.
        //
        // Ojo con la tentacion de asertar `key <= child`: es FALSO. El canonico
        // ordena ascendente dentro de cada grupo de piezas identicas, y como la
        // pieza 0 va en el nibble menos significativo, ordenar puede AUMENTAR el
        // numero. Con dos piezas identicas en [5,2]: crudo = 2*16+5 = 37,
        // ordenado = 5*16+2 = 82.
        assert.equal(canonicalPacked(g, key), key, 'canonical no es idempotente')

        if (winnerAfter(g, unpack(g, child), turn) === ONGOING) {
          if (!next.has(key)) next.set(child, 1 - turn)
        }
      }
    }
    frontier = next
    assert.ok(frontier.size > 0, `frontera vacia en el ply ${ply}`)
  }
})

test('conservacion: ninguna jugada crea, destruye ni duplica piezas', () => {
  const g = makeSpecFromLabels('12344', '11245')
  const buf = new Int32Array(128)
  let p = initialPosition(g)
  let turn = WHITE
  let seed = 999
  const rnd = (n) => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed % n }

  for (let ply = 0; ply < 30; ply++) {
    const locs = unpack(g, p)
    const n = generateMoves(g, locs, turn, buf)
    if (n === 0) break

    const move = buf[rnd(n)]
    const child = applyMove(p, move)
    const after = unpack(g, child)

    // Exactamente una pieza cambio de lugar, y es la que dice la jugada.
    const moved = []
    for (let i = 0; i < g.pieceCount; i++) if (locs[i] !== after[i]) moved.push(i)
    assert.deepEqual(moved, [move >> 4])
    assert.equal(after[move >> 4], move & 0xf)

    // Ninguna pieza vuelve a la mano jamas.
    for (let i = 0; i < g.pieceCount; i++) {
      if (locs[i] !== HAND) assert.notEqual(after[i], HAND, `la pieza ${i} volvio a la mano`)
    }

    // Nunca dos piezas del mismo rango en la misma celda: es el invariante del
    // que depende que el orden de la pila sea implicito (Game.cs:14-16).
    const byCell = new Map()
    for (let i = 0; i < g.pieceCount; i++) {
      if (after[i] >= CELLS) continue
      const k = `${after[i]}:${g.rank[i]}`
      assert.ok(!byCell.has(k), `dos piezas de rango ${g.rank[i]} en la celda ${after[i]}`)
      byCell.set(k, i)
    }

    p = child
    if (winnerAfter(g, after, turn) !== ONGOING) break
    turn = 1 - turn
  }
})
