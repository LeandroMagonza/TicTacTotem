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

function setup(layout = 'portrait') {
  const mats = createMaterials()
  const board = createBoard(mats)
  const pieces = createPieceSet(spec, mats)
  // Igual que hace la app en newGame. Sin esto las 10 cajas de bandeja se quedan
  // en su posicion inicial, todas encimadas en el centro del tablero, y se comen
  // los toques de la celda del medio.
  board.applyLayout(layout)
  return { mats, board, pieces }
}

test('hay exactamente 28 cajas de picking', () => {
  const { board } = setup()
  // 9 losas de celda + 9 columnas de pila + 10 slots de mano. Si este numero
  // cambia, alguien empezo a rayear geometria de arte, que es justo lo que el
  // diseño evita.
  assert.equal(board.pickables.length, 28)
  const kinds = board.pickables.map((p) => p.userData.kind)
  assert.equal(kinds.filter((k) => k === 'cell').length, 18)
  assert.equal(kinds.filter((k) => k === 'hand').length, 10)
  assert.ok(board.pickables.every((p) => p.visible === false), 'los pickers son invisibles')
})

test('la caja de picking de celda sigue la altura real de su pila', () => {
  const { board, pieces } = setup()
  const idOf = (owner, rank) => {
    for (let i = 0; i < spec.pieceCount; i++) if (spec.owner[i] === owner && spec.rank[i] === rank) return i
    throw new Error('sin pieza')
  }
  const snap = { stacks: Array.from({ length: 9 }, () => []), hands: { 0: [], 1: [] } }
  snap.stacks[4] = [idOf(0, 1), idOf(1, 2), idOf(0, 4)]
  board.setCellHeights((c) => pieces.landingY(snap, c))

  const columna = board.stackPickers[4]
  const columnaVacia = board.stackPickers[0]
  const alturaPila = pieces.landingY(snap, 4)

  // La columna tiene que cubrir el totem, para que tocarle la punta seleccione
  // su celda...
  assert.ok(columna.scale.y >= alturaPila, `la columna (${columna.scale.y}) no cubre la pila (${alturaPila})`)
  // ...pero sin pasarse: cada unidad de mas tapa lo que hay detras.
  assert.ok(columna.scale.y < alturaPila + 0.1, 'la columna se pasa de alto')
  // Sin pila no hay columna en absoluto.
  assert.ok(columnaVacia.scale.y < 0.01, `una celda vacia levanta una columna de ${columnaVacia.scale.y}`)
  // Se apoya en el tablero, no flota.
  assert.ok(Math.abs(columna.position.y - columna.scale.y / 2) < 1e-9)

  // Y no es mas ANCHA que la pieza: si lo fuera, un totem taparia mas de lo que
  // se ve que tapa, que es como se sentia el bug de picking.
  assert.ok(columna.scale.x <= geo.COLLAR_RADIUS * 2.2,
    `la columna mide ${columna.scale.x} contra un collar de ${geo.COLLAR_RADIUS * 2}`)
  // La losa, en cambio, cubre toda la baldosa y es chata.
  assert.equal(board.cellPickers[4].scale.x, 0.95)
  assert.ok(board.cellPickers[4].scale.y < 0.2)
})

test('picking: con la camara baja, apuntar a la fila del fondo NO pega adelante', async (t) => {
  const THREE = await import('three')
  const { board, pieces } = setup()

  /** Rayo desde una camara en orbita hacia el centro de una celda. */
  const apuntarA = (cell, elevacionGrados, distancia = 8) => {
    // El raycaster usa matrixWorld, que queda vieja si nadie renderizo. En la
    // app la actualiza el renderer en cada frame; aca hay que pedirla.
    board.group.updateMatrixWorld(true)
    const e = (elevacionGrados * Math.PI) / 180
    const origen = new THREE.Vector3(0, distancia * Math.sin(e), distancia * Math.cos(e))
    const destino = geo.cellToWorld(cell)
    const dir = new THREE.Vector3(destino.x, 0, destino.z).sub(origen).normalize()
    const rc = new THREE.Raycaster(origen, dir)
    const hits = rc.intersectObjects(board.pickables, false)
    return hits.length ? hits[0].object.userData : null
  }

  const snapVacio = { stacks: Array.from({ length: 9 }, () => []), hands: { 0: [], 1: [] } }

  await t.test('tablero vacio: cada celda de la fila del fondo se acierta a 30 grados', () => {
    board.setCellHeights((c) => pieces.landingY(snapVacio, c))
    // Las celdas 0, 1 y 2 son la fila mas lejana con la camara en azimut 0.
    for (const cell of [0, 1, 2]) {
      const hit = apuntarA(cell, 30)
      assert.ok(hit, `no se toco nada apuntando a la celda ${cell}`)
      assert.equal(hit.kind, 'cell')
      assert.equal(hit.index, cell,
        `apuntando a la celda ${cell} se toco la ${hit.index}: la caja de una celda cercana se interpuso`)
    }
  })

  await t.test('con un totem de 3 adelante, el fondo se sigue acertando', () => {
    const idOf = (owner, rank) => {
      for (let i = 0; i < spec.pieceCount; i++) if (spec.owner[i] === owner && spec.rank[i] === rank) return i
      throw new Error('sin pieza')
    }
    const snap = { stacks: Array.from({ length: 9 }, () => []), hands: { 0: [], 1: [] } }
    snap.stacks[7] = [idOf(0, 1), idOf(1, 2), idOf(0, 4)]   // pila de 3 en la fila de adelante
    board.setCellHeights((c) => pieces.landingY(snap, c))

    // Al angulo por defecto (45) un totem de 3 no llega a tapar la fila del fondo.
    assert.equal(apuntarA(1, 45)?.index, 1, 'la pila de la celda 7 tapo la celda 1 a 45 grados')

    // A 30 grados SI la tapa — pero eso es honesto: a ese angulo el totem
    // realmente se interpone en la linea de vision, y para eso esta el boton de
    // inclinacion. Lo que no puede pasar es que tape MAS de lo que se ve: la
    // columna no puede superar al totem real por mas de un pelo.
    const real = pieces.landingY(snap, 7)
    const columna = board.stackPickers[7]
    assert.ok(columna.scale.y - real < 0.05,
      `la columna sobresale ${(columna.scale.y - real).toFixed(3)} sobre el totem real`)
    assert.ok(columna.scale.x <= geo.COLLAR_RADIUS * 2.1,
      'la columna es mas ancha que la pieza y tapa de mas')
  })

  await t.test('y tocar la punta del totem si selecciona SU celda', () => {
    const idOf = (owner, rank) => {
      for (let i = 0; i < spec.pieceCount; i++) if (spec.owner[i] === owner && spec.rank[i] === rank) return i
      throw new Error('sin pieza')
    }
    const snap = { stacks: Array.from({ length: 9 }, () => []), hands: { 0: [], 1: [] } }
    snap.stacks[4] = [idOf(0, 1), idOf(1, 2), idOf(0, 4)]
    board.setCellHeights((c) => pieces.landingY(snap, c))

    board.group.updateMatrixWorld(true)
    const alto = pieces.landingY(snap, 4)
    const e = (30 * Math.PI) / 180
    const origen = new THREE.Vector3(0, 8 * Math.sin(e), 8 * Math.cos(e))
    // Apuntar a media altura del totem, no a su base.
    const dir = new THREE.Vector3(0, alto * 0.6, 0).sub(origen).normalize()
    const hits = new THREE.Raycaster(origen, dir).intersectObjects(board.pickables, false)
    assert.equal(hits[0]?.object.userData.index, 4, 'tocar el totem no selecciono su celda')
  })

  await t.test('una celda vacia no levanta una columna invisible', () => {
    board.setCellHeights((c) => pieces.landingY(snapVacio, c))
    for (const p of board.stackPickers) {
      assert.ok(p.scale.y < 0.01, `una celda vacia levanta una columna de ${p.scale.y}`)
    }
    for (const p of board.cellPickers) {
      assert.ok(p.scale.y < 0.2, `la losa mide ${p.scale.y} y tapa lo que hay detras`)
    }
  })
})

test('las cajas de la mano: sin huecos a lo largo, ceñidas a lo ancho', async (t) => {
  for (const layout of ['portrait', 'landscape']) {
    await t.test(layout, () => {
      const { board, pieces } = setup(layout)
      board.setHandHeights(() => geo.pieceThickness(5))
      const caja = board.handPickers[0][0]
      // El eje de la bandeja: en vertical corre a lo largo de x, en horizontal de z.
      const aLoLargo = layout === 'portrait' ? caja.scale.x : caja.scale.z
      const aLoAncho = layout === 'portrait' ? caja.scale.z : caja.scale.x

      assert.ok(aLoLargo >= geo.TRAY_PITCH - 0.1,
        `${aLoLargo} con un paso de ${geo.TRAY_PITCH}: queda un hueco de ` +
        `${(geo.TRAY_PITCH - aLoLargo).toFixed(2)} donde el toque no registra, y esa pieza ` +
        'se siente como que "no se deja seleccionar"')
      assert.ok(aLoLargo < geo.TRAY_PITCH, 'pero no se pueden superponer entre si')

      // A lo ancho y en alto tiene que ceñirse a la pieza: si sobresale, con la
      // camara baja la bandeja se mete en la linea de vision de la fila de
      // casillas mas cercana y le roba los toques.
      assert.ok(aLoAncho <= geo.COLLAR_RADIUS * 2.1,
        `la caja mide ${aLoAncho} de ancho contra un collar de ${geo.COLLAR_RADIUS * 2}`)
      assert.ok(caja.scale.y <= geo.pieceThickness(5) + 0.05,
        `la caja mide ${caja.scale.y} de alto contra una pieza de ${geo.pieceThickness(5)}`)
      assert.ok(Math.abs(caja.position.y - caja.scale.y / 2) < 1e-9, 'la caja flota')

      // Un slot vacio no roba toques.
      board.setHandHeights(() => 0)
      assert.ok(board.handPickers[0][0].scale.y < 0.01)
      void pieces
    })
  }
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
