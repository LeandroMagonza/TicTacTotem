import * as THREE from 'three'
import {
  COLLAR_RADIUS, COLLAR_HEIGHT, PIECE_HEIGHT, FOOTPRINT,
  pieceThickness, cellToWorld, traySlotToWorld,
} from './geometry.js'

/**
 * Cuerpo procedural: un cono truncado cuya altura y radio superior crecen con el
 * nivel. Es el placeholder hasta que existan los modelos de animales, y es
 * perfectamente jugable — el collar hace el trabajo informativo igual.
 * @param {number} rank
 */
function proceduralBody(rank) {
  const h = PIECE_HEIGHT[rank] ?? 0.27
  const rBottom = FOOTPRINT / 2
  // Los niveles bajos salen conicos y los altos casi cilindricos: sumado a la
  // altura, un 5 se lee como una mole y un 1 como una punta. Dos señales
  // redundantes para lo mismo.
  const rTop = rBottom * (0.22 + 0.12 * rank)
  return new THREE.CylinderGeometry(rTop, rBottom, h, 24, 1)
}

/**
 * Una pieza: collar + cuerpo, dentro de un Group cuyo origen esta en la BASE.
 * Que el origen este en la base es lo que hace que apilar sea sumar alturas.
 *
 * @param {object} args
 * @param {number} args.rank @param {number} args.owner
 * @param {ReturnType<import('./materials.js').createMaterials>} args.mats
 * @param {THREE.Object3D} [args.model]  prototipo ya normalizado, si hay
 */
export function createPiece({ rank, owner, mats, model }) {
  const g = new THREE.Group()

  // El collar. Materiales por cara: [borde, tapa superior, tapa inferior].
  const collar = new THREE.Mesh(
    new THREE.CylinderGeometry(COLLAR_RADIUS, COLLAR_RADIUS, COLLAR_HEIGHT, 32, 1),
    [mats.collarFor(rank, owner), mats.collarCap[owner], mats.collarCap[owner]],
  )
  collar.position.y = COLLAR_HEIGHT / 2
  collar.castShadow = true
  collar.receiveShadow = true
  g.add(collar)

  const body = model ? model.clone(true) : new THREE.Mesh(proceduralBody(rank), mats.body[owner])
  if (!model) {
    body.position.y = COLLAR_HEIGHT + (PIECE_HEIGHT[rank] ?? 0.26) / 2
    body.castShadow = true
  } else {
    body.position.y = COLLAR_HEIGHT
    body.traverse((o) => { if (o.isMesh) { o.castShadow = true; o.material = mats.body[owner] } })
  }
  g.add(body)

  g.userData = { kind: 'piece', rank, owner, thickness: pieceThickness(rank) }
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
        obj.rotation.y = 0
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
        obj.rotation.y = 0
        obj.scale.setScalar(1)
      })
    }
  }

  /** Altura a la que caeria una pieza si se juega en esta celda. */
  function landingY(snap, cell) {
    let y = 0
    for (const pieceId of snap.stacks[cell]) y += byId.get(pieceId).userData.thickness
    return y
  }

  return { group, byId, applyInstant, landingY }
}
