import * as THREE from 'three'
import {
  BOARD_HALF, BOARD_THICKNESS, TILE_SIZE, TILE_THICKNESS,
  cellToWorld, traySlotToWorld, TRAY_SLOTS, TRAY_PITCH, COLLAR_RADIUS,
} from './geometry.js'

/** Alto de la caja de una celda vacia: algo de cuerpo para tocar, sin tapar. */
const EMPTY_CELL_PICK = 0.14

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
  const tileGeo = new THREE.BoxGeometry(TILE_SIZE, TILE_THICKNESS, TILE_SIZE)
  /** @type {THREE.Mesh[]} */
  const tiles = []
  for (let i = 0; i < 9; i++) {
    const t = new THREE.Mesh(tileGeo, mats.tile.clone())
    const { x, z } = cellToWorld(i)
    t.position.set(x, 0.001 + TILE_THICKNESS / 2, z)
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
  // 9 losas de celda + 9 columnas de pila + 10 slots de mano = 28.
  //
  // LA REGLA DE TODAS: ninguna caja puede ocupar mas espacio del que ocupa lo
  // que representa. Toda geometria invisible de mas es oclusion que el jugador
  // no ve y no puede anticipar, y se siente como que el juego no registra donde
  // tocaste. Por eso las alturas se ajustan en cada sync a la pila y a la pieza
  // reales, en vez de usar un maximo teorico.
  /** @type {THREE.Mesh[]} */
  const pickables = []
  const pickMat = new THREE.MeshBasicMaterial({ visible: false })

  // DOS cajas por celda:
  //
  //   - una LOSA plana del ancho de la baldosa, siempre. Da un objetivo comodo
  //     para tocar la casilla, y es tan baja que no tapa nada de lo que hay
  //     detras.
  //   - una COLUMNA angosta, del ancho de la pieza y del alto REAL de la pila.
  //     Es lo que hace que tocar la punta de un totem seleccione su casilla.
  //
  // La columna se dimensiona con el collar y no con la baldosa a proposito: asi
  // un totem tapa exactamente lo que se ve que tapa. Con una sola caja ancha y
  // alta —que era el diseño anterior— la geometria invisible tapaba mucho mas
  // que las piezas, y con la camara baja tocabas la fila del fondo pero jugaba
  // en la de adelante.
  const cellPickGeo = new THREE.BoxGeometry(1, 1, 1)
  /** @type {THREE.Mesh[]} */
  const cellPickers = []
  /** @type {THREE.Mesh[]} */
  const stackPickers = []
  for (let i = 0; i < 9; i++) {
    const { x, z } = cellToWorld(i)

    const losa = new THREE.Mesh(cellPickGeo, pickMat)
    losa.position.set(x, EMPTY_CELL_PICK / 2, z)
    losa.scale.set(0.95, EMPTY_CELL_PICK, 0.95)
    losa.visible = false
    losa.userData = { kind: 'cell', index: i }
    cellPickers.push(losa)
    pickables.push(losa)
    group.add(losa)

    const columna = new THREE.Mesh(cellPickGeo, pickMat)
    columna.position.set(x, 0, z)
    columna.scale.set(COLLAR_RADIUS * 2.05, 0.0001, COLLAR_RADIUS * 2.05)
    columna.visible = false
    columna.userData = { kind: 'cell', index: i }
    stackPickers.push(columna)
    pickables.push(columna)
    group.add(columna)
  }

  /**
   * Ajusta la columna de cada celda al alto real de su pila. Sin pila, la
   * columna se aplasta a cero y solo queda la losa.
   * @param {(cell:number)=>number} stackHeight
   */
  function setCellHeights(stackHeight) {
    for (let i = 0; i < 9; i++) {
      // Margen chico y a proposito: cada milimetro de mas es oclusion invisible
      // que el jugador no puede ver ni anticipar.
      const h = stackHeight(i)
      const alto = h > 0 ? h + 0.015 : 0.0001
      stackPickers[i].scale.y = alto
      stackPickers[i].position.y = alto / 2
    }
  }

  // Cajas de mano, tambien unitarias y escaladas (ver applyLayout / setHandHeights):
  //
  //   - A LO LARGO de la bandeja son casi tan anchas como el paso (0,95). Con
  //     cubos de 0,62 quedaba un hueco de 0,33 entre pieza y pieza donde el toque
  //     no registraba, y se sentia como que esa pieza "no se dejaba seleccionar".
  //   - A LO ANCHO y en ALTO se ciñen a la pieza. Si sobresalen, con la camara
  //     baja la bandeja se mete en la linea de vision de la fila de casillas mas
  //     cercana y te roba esos toques.
  const handPickGeo = new THREE.BoxGeometry(1, 1, 1)
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
    // El lado largo de la caja sigue el eje de la bandeja: en vertical corre a lo
    // largo de x, en horizontal a lo largo de z.
    const largo = TRAY_PITCH - 0.06
    const ancho = COLLAR_RADIUS * 2.05
    for (let side = 0; side < 2; side++) {
      for (let k = 0; k < TRAY_SLOTS; k++) {
        const { x, z } = traySlotToWorld(side, k, layout)
        traySlots[side][k].position.set(x, 0.004, z)
        const p = handPickers[side][k]
        p.position.x = x
        p.position.z = z
        if (layout === 'portrait') p.scale.x = largo, p.scale.z = ancho
        else p.scale.x = ancho, p.scale.z = largo
      }
    }
  }

  /**
   * Ajusta el alto de cada caja de mano a la pieza que representa. Un slot vacio
   * se aplasta a cero para que no robe toques.
   * @param {(side:number, slot:number) => number} pieceHeight  0 si el slot esta vacio
   */
  function setHandHeights(pieceHeight) {
    for (let side = 0; side < 2; side++) {
      for (let k = 0; k < TRAY_SLOTS; k++) {
        const h = pieceHeight(side, k)
        const alto = h > 0 ? h + 0.03 : 0.0001
        handPickers[side][k].scale.y = alto
        handPickers[side][k].position.y = alto / 2
      }
    }
  }

  return {
    group, tiles, traySlots, handPickers, cellPickers, stackPickers,
    pickables, applyLayout, setCellHeights, setHandHeights,
  }
}
