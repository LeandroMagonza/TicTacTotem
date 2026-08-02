import * as THREE from 'three'
import {
  BOARD_HALF, BOARD_THICKNESS, TILE_SIZE, MAX_STACK_HEIGHT,
  cellToWorld, traySlotToWorld, TRAY_SLOTS, COLLAR_RADIUS,
} from './geometry.js'

/**
 * Tablero, baldosas, contornos de bandeja y las cajas invisibles de picking.
 *
 * Se rayean SOLO las cajas invisibles, nunca el arte. Rayear geometria importada
 * es lento, da un area de toque mezquina en un celular, y cambiar un modelo
 * cambiaria el comportamiento del picking en silencio. Unity ponia un
 * OnMouseOver por objeto (PieceModel.cs:8-18), que ademas es solo-mouse.
 *
 * @param {ReturnType<import('./materials.js').createMaterials>} mats
 */
export function createBoard(mats) {
  const group = new THREE.Group()

  // Losa
  const slab = new THREE.Mesh(
    new THREE.BoxGeometry(BOARD_HALF * 2 + 0.16, BOARD_THICKNESS, BOARD_HALF * 2 + 0.16),
    mats.board,
  )
  slab.position.y = -BOARD_THICKNESS / 2
  slab.receiveShadow = true
  group.add(slab)

  // Nueve baldosas
  const tileGeo = new THREE.BoxGeometry(TILE_SIZE, 0.02, TILE_SIZE)
  /** @type {THREE.Mesh[]} */
  const tiles = []
  for (let i = 0; i < 9; i++) {
    const t = new THREE.Mesh(tileGeo, mats.tile.clone())
    const { x, z } = cellToWorld(i)
    t.position.set(x, 0.011, z)
    t.receiveShadow = true
    t.userData = { kind: 'tile', index: i }
    tiles.push(t)
    group.add(t)
  }

  // Contornos tenues de los slots de bandeja. Los huecos son informacion: se ve
  // de un vistazo que piezas ya uso cada uno.
  const slotGeo = new THREE.RingGeometry(COLLAR_RADIUS * 0.82, COLLAR_RADIUS * 0.92, 24)
  const slotMat = new THREE.MeshBasicMaterial({
    color: 0x6a7382, transparent: true, opacity: 0.35, side: THREE.DoubleSide, depthWrite: false,
  })
  /** @type {THREE.Mesh[][]} */
  const traySlots = [[], []]
  for (let side = 0; side < 2; side++) {
    for (let k = 0; k < TRAY_SLOTS; k++) {
      const m = new THREE.Mesh(slotGeo, slotMat)
      m.rotation.x = -Math.PI / 2
      m.position.y = 0.004
      traySlots[side].push(m)
      group.add(m)
    }
  }

  // --- Cajas de picking, invisibles ---------------------------------------
  //
  // 9 de celda + 10 de slot de mano = 19. La de celda es ALTA (cubre la pila
  // maxima mas aire) a proposito: tocar el aguila arriba de un totem de 4 tiene
  // que seleccionar ESA celda, no la celda vacia que hay detras.
  /** @type {THREE.Mesh[]} */
  const pickables = []
  const pickMat = new THREE.MeshBasicMaterial({ visible: false })

  const cellPickGeo = new THREE.BoxGeometry(0.95, MAX_STACK_HEIGHT + 0.5, 0.95)
  for (let i = 0; i < 9; i++) {
    const box = new THREE.Mesh(cellPickGeo, pickMat)
    const { x, z } = cellToWorld(i)
    box.position.set(x, (MAX_STACK_HEIGHT + 0.5) / 2, z)
    box.visible = false
    box.userData = { kind: 'cell', index: i }
    pickables.push(box)
    group.add(box)
  }

  const handPickGeo = new THREE.BoxGeometry(0.62, 0.62, 0.62)
  /** @type {THREE.Mesh[][]} */
  const handPickers = [[], []]
  for (let side = 0; side < 2; side++) {
    for (let k = 0; k < TRAY_SLOTS; k++) {
      const box = new THREE.Mesh(handPickGeo, pickMat)
      box.visible = false
      box.userData = { kind: 'hand', side, slot: k, pieceId: -1 }
      handPickers[side].push(box)
      pickables.push(box)
      group.add(box)
    }
  }

  /** Reubica bandejas y sus pickers cuando cambia la orientacion de la pantalla. */
  function applyLayout(layout) {
    for (let side = 0; side < 2; side++) {
      for (let k = 0; k < TRAY_SLOTS; k++) {
        const { x, z } = traySlotToWorld(side, k, layout)
        traySlots[side][k].position.set(x, 0.004, z)
        handPickers[side][k].position.set(x, 0.31, z)
      }
    }
  }

  return { group, tiles, traySlots, handPickers, pickables, applyLayout }
}
