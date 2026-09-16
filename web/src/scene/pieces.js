import * as THREE from 'three'
import {
  COIN_RADIUS, COIN_THICKNESS, PIECE_HEIGHT,
  pieceThickness, cellToWorld, traySlotToWorld,
} from './geometry.js'

/**
 * Una pieza: una moneda, dentro de un Group cuyo origen esta en la BASE.
 * Que el origen este en la base es lo que hace que apilar sea sumar alturas.
 *
 * La moneda es un solo cilindro con tres materiales, uno por cara:
 * [canto con los numerales alrededor, cara de arriba con el numeral grande,
 * cara de abajo lisa].
 *
 * Si hay un modelo de animal para el nivel, se para encima de la moneda (que
 * entonces hace de collar) y la pieza suma su alto.
 *
 * @param {object} args
 * @param {number} args.rank @param {number} args.owner
 * @param {ReturnType<import('./materials.js').createMaterials>} args.mats
 * @param {THREE.Object3D} [args.model]  prototipo ya normalizado, si hay
 */
export function createPiece({ rank, owner, mats, model }) {
  const g = new THREE.Group()
  const grosor = COIN_THICKNESS[rank] ?? 0.18

  const coin = new THREE.Mesh(
    new THREE.CylinderGeometry(COIN_RADIUS, COIN_RADIUS, grosor, 48, 1),
    [mats.coinEdgeFor(rank, owner), mats.coinFaceFor(rank, owner), mats.coinBottom[owner]],
  )
  coin.position.y = grosor / 2
  coin.castShadow = true
  coin.receiveShadow = true
  g.add(coin)

  let thickness = pieceThickness(rank)
  if (model) {
    const body = model.clone(true)
    body.position.y = grosor
    body.traverse((o) => { if (o.isMesh) { o.castShadow = true; o.material = mats.body[owner] } })
    g.add(body)
    thickness += PIECE_HEIGHT[rank] ?? 0.27
  }

  g.userData = { kind: 'piece', rank, owner, thickness }
  return g
}

/**
 * Registro de piezas y sincronizacion escena <- snapshot.
 *
 * La regla de arquitectura de todo el proyecto: la escena tiene que poder
 * reconstruirse desde cualquier snapshot SIN animacion. Las animaciones son
 * decoracion opcional entre dos estados instantaneos. Asi el undo, el restart,
 * el cambio de bando, la carga de posiciones para debug y "se desincronizo algo"
 * colapsan todos a un solo camino de codigo.
 *
 * @param {import('../engine/spec.js').GameSpec} spec
 * @param {ReturnType<import('./materials.js').createMaterials>} mats
 * @param {Map<number, THREE.Object3D>} [models]  rank -> prototipo normalizado
 */
export function createPieceSet(spec, mats, models = new Map()) {
  const group = new THREE.Group()
  /** @type {Map<number, THREE.Group>} */
  const byId = new Map()

  /**
   * Giro comun de todas las piezas sobre Y. El canto es simetrico, asi que lo
   * unico que gira de verdad es el numeral de la cara: se lo mantiene alineado
   * con el azimut de la camara para que se lea derecho desde donde se mire.
   */
  let facing = 0

  for (let i = 0; i < spec.pieceCount; i++) {
    const obj = createPiece({
      rank: spec.rank[i],
      owner: spec.owner[i],
      mats,
      model: models.get(spec.rank[i]),
    })
    obj.userData.pieceId = i
    byId.set(i, obj)
    group.add(obj)
  }

  /**
   * Coloca cada pieza donde diga el snapshot, sin animar.
   * @param {{stacks: number[][], hands: Record<number, number[]>}} snap
   * @param {'portrait'|'landscape'} layout
   */
  function applyInstant(snap, layout) {
    // Piezas en el tablero: la pila viene de abajo hacia arriba, asi que la
    // altura sale de caminar el array. Nunca se ordena ni se comparan niveles
    // aca: eso ya lo garantiza el motor (los niveles crecen hacia arriba).
    for (let cell = 0; cell < 9; cell++) {
      const stack = snap.stacks[cell]
      const { x, z } = cellToWorld(cell)
      let y = 0
      for (const pieceId of stack) {
        const obj = byId.get(pieceId)
        obj.visible = true
        obj.position.set(x, y, z)
        obj.rotation.y = facing
        obj.scale.setScalar(1)
        y += obj.userData.thickness
      }
    }

    // Piezas en la mano, en su bandeja.
    for (const side of [0, 1]) {
      const hand = snap.hands[side] ?? []
      hand.forEach((pieceId, slot) => {
        const obj = byId.get(pieceId)
        const { x, z } = traySlotToWorld(side, slot, layout)
        obj.visible = true
        obj.position.set(x, 0, z)
        obj.rotation.y = facing
        obj.scale.setScalar(1)
      })
    }
  }

  /**
   * Alinea el numeral de la cara con la camara. Devuelve true si algo cambio,
   * para que el loop sepa que hay que volver a dibujar.
   * @param {number} azimuth  el de la camara, en radianes
   */
  function setFacing(azimuth) {
    if (azimuth === facing) return false
    facing = azimuth
    for (const obj of byId.values()) obj.rotation.y = facing
    return true
  }

  /** Altura a la que caeria una pieza si se juega en esta celda. */
  function landingY(snap, cell) {
    let y = 0
    for (const pieceId of snap.stacks[cell]) y += byId.get(pieceId).userData.thickness
    return y
  }

  return { group, byId, applyInstant, setFacing, landingY }
}
