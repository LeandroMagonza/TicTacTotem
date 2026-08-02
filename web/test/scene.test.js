import test from 'node:test'
import assert from 'node:assert/strict'

/**
 * three.js corre perfecto en Node para TODO menos WebGLRenderer. Asi que se
 * puede armar el grafo de escena entero, sincronizarlo contra un snapshot y
 * asertar posiciones — sin GPU, sin browser, sin canvas, en milisegundos.
 *
 * Aca va la mayoria de la cobertura automatica de la escena, porque "la pieza
 * quedo a la altura equivocada en una pila de 3" es exactamente el bug que este
 * juego va a tener, y una captura de pantalla no lo agarra.
 */

// Stub minimo de canvas: lo unico que lo necesita es la textura del numeral del
// collar (un canvas 2D de 512x64).
globalThis.document = {
  createElement: () => ({
    width: 0, height: 0,
    getContext: () => ({
      fillStyle: '', font: '', textAlign: '', textBaseline: '',
      fillRect() {}, fillText() {},
    }),
  }),
}

const { makeSpecFromLabels } = await import('../src/engine/spec.js')
const { Match } = await import('../src/engine/match.js')
const { createMaterials } = await import('../src/scene/materials.js')
const { createBoard } = await import('../src/scene/board.js')
const { createPieceSet } = await import('../src/scene/pieces.js')
const geo = await import('../src/scene/geometry.js')

const spec = makeSpecFromLabels('12344', '11245')

function setup() {
  const mats = createMaterials()
  const board = createBoard(mats)
  const pieces = createPieceSet(spec, mats)
  return { mats, board, pieces }
}

test('hay exactamente 19 cajas de picking', () => {
  const { board } = setup()
  // 9 de celda + 10 de slot de mano. Si este numero cambia, alguien empezo a
  // rayear geometria de arte, que es justo lo que el diseño evita.
  assert.equal(board.pickables.length, 19)
  const kinds = board.pickables.map((p) => p.userData.kind)
  assert.equal(kinds.filter((k) => k === 'cell').length, 9)
  assert.equal(kinds.filter((k) => k === 'hand').length, 10)
  assert.ok(board.pickables.every((p) => p.visible === false), 'los pickers son invisibles')
})

test('la caja de picking de celda cubre la pila maxima', () => {
  const { board } = setup()
  const cellBox = board.pickables.find((p) => p.userData.kind === 'cell')
  const h = cellBox.geometry.parameters.height
  // Tocar la pieza de arriba de un totem alto tiene que seleccionar ESA celda,
  // no la celda vacia que hay detras.
  assert.ok(h > geo.MAX_STACK_HEIGHT, `la caja (${h}) no cubre la pila maxima (${geo.MAX_STACK_HEIGHT})`)
})

test('las piezas se apilan a la altura correcta', () => {
  const { pieces } = setup()
  const match = new Match(spec)

  // Armar a mano una pila de 3 en la celda 4: niveles 1, 2, 4.
  const idOf = (owner, rank, skip = 0) => {
    let seen = 0
    for (let i = 0; i < spec.pieceCount; i++) {
      if (spec.owner[i] === owner && spec.rank[i] === rank) { if (seen++ === skip) return i }
    }
    throw new Error(`sin pieza ${owner}/${rank}`)
  }
  const snap = {
    stacks: Array.from({ length: 9 }, () => []),
    hands: { 0: [], 1: [] },
  }
  snap.stacks[4] = [idOf(0, 1), idOf(1, 2), idOf(0, 4)]
  pieces.applyInstant(snap, 'portrait')

  const centro = geo.cellToWorld(4)
  const [a, b, c] = snap.stacks[4].map((id) => pieces.byId.get(id))

  assert.equal(a.position.y, 0, 'la de abajo se apoya en el tablero')
  assert.equal(b.position.y, geo.pieceThickness(1))
  assert.equal(c.position.y, geo.pieceThickness(1) + geo.pieceThickness(2))

  for (const obj of [a, b, c]) {
    assert.equal(obj.position.x, centro.x)
    assert.equal(obj.position.z, centro.z)
  }

  // Y la altura de aterrizaje de una cuarta pieza es la suma de las tres.
  const esperado = geo.pieceThickness(1) + geo.pieceThickness(2) + geo.pieceThickness(4)
  assert.ok(Math.abs(pieces.landingY(snap, 4) - esperado) < 1e-9)

  void match
})

test('la mano se acomoda en la bandeja y el eje sigue la orientacion', () => {
  const { pieces } = setup()
  const snap = {
    stacks: Array.from({ length: 9 }, () => []),
    hands: { 0: [0, 1, 2], 1: [5, 6] },
  }

  pieces.applyInstant(snap, 'portrait')
  const p0 = pieces.byId.get(0).position.clone()
  // En vertical las bandejas son filas: varia x, z es constante.
  assert.equal(p0.z, geo.TRAY_OFFSET)
  assert.notEqual(pieces.byId.get(1).position.x, p0.x)
  assert.equal(pieces.byId.get(1).position.z, p0.z)

  pieces.applyInstant(snap, 'landscape')
  // En horizontal son columnas: varia z, x es constante.
  assert.equal(pieces.byId.get(0).position.x, geo.TRAY_OFFSET)
  assert.equal(pieces.byId.get(1).position.x, geo.TRAY_OFFSET)
  assert.notEqual(pieces.byId.get(1).position.z, pieces.byId.get(0).position.z)
})

test('el layout cambia la caja de contenido de vertical a horizontal', () => {
  const v = geo.contentExtent('portrait')
  const h = geo.contentExtent('landscape')
  assert.ok(v.halfZ > v.halfX, 'vertical tiene que ser mas alto que ancho')
  assert.ok(h.halfX > h.halfZ, 'horizontal tiene que ser mas ancho que alto')
  assert.equal(v.halfX, h.halfZ)
  assert.equal(v.halfZ, h.halfX)
})

test('la aritmetica de oclusion de la camara', () => {
  // La restriccion de diseño de la camara, fijada como test para que nadie
  // "mejore" la elevacion por defecto —ni las alturas de las piezas— sin ver el
  // costo en celdas ocultas.
  assert.ok(Math.abs(geo.occludedCells(45) - geo.MAX_STACK_HEIGHT) < 0.01, 'a 45 grados tan=1')
  assert.ok(geo.occludedCells(30) > geo.occludedCells(45) * 1.6, '30 grados es mucho peor')
  assert.ok(geo.ELEVATION_LOW === 45 && geo.ELEVATION_HIGH === 62)

  // El caso teorico peor son 5 piezas apiladas (los niveles crecen estrictamente,
  // asi que nunca hay mas). Es posible con estos sets —A aporta 1,2,3,4 y B el 5—
  // pero rarisimo.
  assert.ok(geo.MAX_STACK_HEIGHT < 2.0, `pila maxima ${geo.MAX_STACK_HEIGHT} demasiado alta`)

  // Lo que importa de verdad es el caso REALISTA: una pila de 3 tiene que
  // ocultar menos de una celda al angulo por defecto, si no el tablero deja de
  // leerse en el juego normal.
  const pilaDe3 = geo.pieceThickness(1) + geo.pieceThickness(2) + geo.pieceThickness(3)
  const ocultaA45 = pilaDe3 / Math.tan(45 * Math.PI / 180)
  assert.ok(ocultaA45 < 1, `una pila de 3 oculta ${ocultaA45.toFixed(2)} celdas a 45 grados`)
  assert.ok(geo.occludedCells(geo.ELEVATION_HIGH) < geo.occludedCells(geo.ELEVATION_LOW) * 0.6,
    'el preset alto tiene que reducir la oclusion de forma notoria')
})

test('los anillos del piso quedan por encima de la baldosa', async (t) => {
  // Bug real: los anillos de destino se dibujaban a y=0,02 y la cara superior de
  // la baldosa esta a 0,021, asi que quedaban ENTERRADOS adentro. El anillo de
  // seleccion si se veia, porque va sobre la pieza y no sobre el piso, y eso
  // hacia que el bug pareciera "no hay jugadas legales" en vez de un problema
  // de dibujo.
  const { board, mats } = setup()
  const tile = board.tiles[0]
  const caraSuperior = tile.position.y + tile.geometry.parameters.height / 2
  assert.ok(Math.abs(caraSuperior - geo.TILE_TOP) < 1e-9,
    `TILE_TOP (${geo.TILE_TOP}) no coincide con la baldosa real (${caraSuperior})`)
  assert.ok(geo.GROUND_RING_Y > caraSuperior,
    `los anillos a ${geo.GROUND_RING_Y} quedan dentro de la baldosa (${caraSuperior})`)

  await t.test('y los destinos sobre celda vacia usan esa altura', async () => {
    const { createHighlights } = await import('../src/scene/highlights.js')
    const hl = createHighlights(mats)
    hl.showDestinations([{ to: 4, immediateResult: null }], () => 0)
    const visibles = hl.group.children.filter((c) => c.visible)
    assert.ok(visibles.length >= 1, 'no se dibujo ningun anillo de destino')
    assert.ok(visibles.every((c) => c.position.y > caraSuperior),
      'algun anillo quedo por debajo de la cara de la baldosa')
  })
})

test('el collar es mas ancho que la huella de cualquier animal', () => {
  // Es de lo que depende toda la legibilidad: si la huella supera al collar,
  // el animal tapa la franja de abajo y las pilas dejan de leerse.
  assert.ok(geo.COLLAR_RADIUS * 2 > geo.FOOTPRINT * 0.95,
    `collar ${geo.COLLAR_RADIUS * 2} vs huella ${geo.FOOTPRINT}`)
})

test('normalizacion de modelos', async (t) => {
  const THREE = await import('three')
  const { normalize } = await import('../src/scene/models.js')

  /** Malla descentrada, sin apoyar en y=0 y de tamaño arbitrario, como sale de Blender. */
  const sujeto = (sx, sy, sz, cx, cy, cz) => {
    const g = new THREE.BoxGeometry(sx, sy, sz)
    g.translate(cx, cy, cz)
    return new THREE.Object3D().add(new THREE.Mesh(g))
  }

  await t.test('escala uniforme al alto del nivel y apoya la base en y=0', () => {
    // 8 unidades de alto, centrada en (5, 30, -2): nada de eso deberia importar.
    const obj = sujeto(3, 8, 3, 5, 30, -2)
    const out = normalize(obj, 3, { file: 'x.glb' })
    const box = new THREE.Box3().setFromObject(out)

    assert.ok(Math.abs(box.min.y) < 1e-6, `la base quedo en ${box.min.y}, no en 0`)
    assert.ok(Math.abs(box.max.y - geo.PIECE_HEIGHT[3]) < 1e-6, 'el alto no es el del nivel')
    const centro = box.getCenter(new THREE.Vector3())
    assert.ok(Math.abs(centro.x) < 1e-6 && Math.abs(centro.z) < 1e-6, 'no quedo centrada en XZ')
  })

  await t.test('la escala es UNIFORME: no deforma al animal', () => {
    const obj = sujeto(2, 8, 4, 0, 0, 0)
    const out = normalize(obj, 3, { file: 'x.glb' })
    const s = new THREE.Box3().setFromObject(out).getSize(new THREE.Vector3())
    // Las proporciones originales 2:8:4 tienen que sobrevivir.
    assert.ok(Math.abs(s.x / s.y - 2 / 8) < 1e-6)
    assert.ok(Math.abs(s.z / s.y - 4 / 8) < 1e-6)
  })

  await t.test('recorta la huella y avisa, aceptando quedar mas bajo', () => {
    // Un animal muy ancho y bajo: escalarlo al alto del nivel lo haria invadir
    // la celda vecina.
    const obj = sujeto(20, 2, 20, 0, 0, 0)
    const avisos = []
    const out = normalize(obj, 5, { file: 'aguila.glb' }, {}, avisos)
    const s = new THREE.Box3().setFromObject(out).getSize(new THREE.Vector3())

    assert.ok(Math.max(s.x, s.z) <= geo.FOOTPRINT + 1e-6,
      `la huella ${Math.max(s.x, s.z)} supera el maximo ${geo.FOOTPRINT}`)
    assert.ok(s.y < geo.PIECE_HEIGHT[5], 'tendria que haber quedado mas bajo que su nivel')
    assert.equal(avisos.length, 1, 'el recorte tiene que avisar, no ser silencioso')
    assert.match(avisos[0], /aguila\.glb/)
  })

  await t.test('el escape de eje Z rota, y el camino normal no', () => {
    const zUp = sujeto(2, 4, 8, 0, 0, 0)     // "alto" a lo largo de z, como Blender
    const out = normalize(zUp, 3, { file: 'x.glb', upAxis: 'Z' })
    const s = new THREE.Box3().setFromObject(out).getSize(new THREE.Vector3())
    // Tras rotar -90 en X, lo que medía 8 en z pasa a ser el alto.
    assert.ok(Math.abs(s.x / s.y - 2 / 8) < 1e-6, 'no se roto el eje')
  })

  await t.test('un modelo plano se rechaza en vez de dividir por cero', () => {
    assert.throws(() => normalize(sujeto(4, 0, 4, 0, 0, 0), 2, { file: 'plano.glb' }))
  })
})

test('la celda 0 esta arriba a la izquierda y la 8 abajo a la derecha', () => {
  assert.deepEqual(geo.cellToWorld(0), { x: -1, y: 0, z: -1 })
  assert.deepEqual(geo.cellToWorld(4), { x: 0, y: 0, z: 0 })
  assert.deepEqual(geo.cellToWorld(8), { x: 1, y: 0, z: 1 })
  for (let i = 0; i < 9; i++) {
    const { x, z } = geo.cellToWorld(i)
    assert.equal(geo.worldToCell(x, z), i, `ida y vuelta de la celda ${i}`)
  }
})
