import * as THREE from 'three'
import { COLLAR_RADIUS } from './geometry.js'

/**
 * Colores de equipo.
 *
 * Ambar contra azul pizarra: separables bajo deuteranopia y protanopia, cosa que
 * ningun par rojo/verde logra. Unity usaba blanco contra naranja (Piece.cs:48-50);
 * el blanco tiene poco contraste contra los resaltados y contra un tablero claro.
 */
export const TEAM_COLOR = Object.freeze(['#e8a33d', '#4a7fb5'])
export const TEAM_NAME = Object.freeze(['Tótem A', 'Tótem B'])

/** Colores de estado. Separados del acento: nunca se reusan para "equipo 3". */
export const HL_LEGAL = '#5fb87a'
export const HL_LOSS = '#e0a020'    // ambar: esta jugada destapa una linea rival
export const HL_WIN = '#f2d16b'

/**
 * Textura del collar: el numeral del nivel repetido alrededor del borde, para
 * que se lea desde cualquier angulo.
 * @param {number} rank @param {number} owner
 */
function collarTexture(rank, owner) {
  const W = 512, H = 64
  const cv = document.createElement('canvas')
  cv.width = W; cv.height = H
  const g = cv.getContext('2d')

  g.fillStyle = TEAM_COLOR[owner]
  g.fillRect(0, 0, W, H)

  // Franja mas oscura arriba y abajo, para que el borde tenga definicion.
  g.fillStyle = 'rgba(0,0,0,0.22)'
  g.fillRect(0, 0, W, 7)
  g.fillRect(0, H - 7, W, 7)

  // El numeral, 8 veces alrededor. Tinta oscura sobre el color de equipo: los
  // dos colores de equipo son suficientemente claros para bancar texto negro.
  g.fillStyle = 'rgba(0,0,0,0.78)'
  g.font = `bold ${H * 0.62}px system-ui, sans-serif`
  g.textAlign = 'center'
  g.textBaseline = 'middle'
  for (let i = 0; i < 8; i++) g.fillText(String(rank), (W / 8) * (i + 0.5), H / 2 + 1)

  const tex = new THREE.CanvasTexture(cv)
  tex.colorSpace = THREE.SRGBColorSpace
  tex.anisotropy = 4
  return tex
}

/**
 * Materiales compartidos. Exactamente DOS materiales de cuerpo para todo el
 * juego, uno por equipo, aplicados a todas las mallas de todas las piezas.
 *
 * El resaltado de seleccion NO cambia materiales. Unity reemplazaba el material
 * de la pieza por un tercero "seleccionado" (Piece.cs:28-34), lo que destruye el
 * color de equipo justo cuando mas necesitas saber de quien es la pieza. Aca la
 * seleccion es un anillo en el piso mas un rebote.
 */
export function createMaterials() {
  const body = TEAM_COLOR.map((c) => new THREE.MeshStandardMaterial({
    color: new THREE.Color(c),
    roughness: 0.55,
    metalness: 0.0,
  }))

  /** @type {Map<string, THREE.MeshStandardMaterial>} */
  const collars = new Map()
  const collarFor = (rank, owner) => {
    const k = `${rank}:${owner}`
    let m = collars.get(k)
    if (!m) {
      m = new THREE.MeshStandardMaterial({
        map: collarTexture(rank, owner),
        roughness: 0.6,
        metalness: 0.0,
      })
      collars.set(k, m)
    }
    return m
  }

  // Tapas del collar, lisas: solo el borde lleva el numeral.
  const collarCap = TEAM_COLOR.map((c) => new THREE.MeshStandardMaterial({
    color: new THREE.Color(c).multiplyScalar(0.88),
    roughness: 0.6,
  }))

  const board = new THREE.MeshStandardMaterial({ color: 0x2c3038, roughness: 0.85 })
  const tile = new THREE.MeshStandardMaterial({ color: 0x373c46, roughness: 0.8 })

  const ring = (color, opacity = 0.9) => new THREE.MeshBasicMaterial({
    color: new THREE.Color(color),
    transparent: true,
    opacity,
    depthWrite: false,
  })

  return {
    body,
    collarFor,
    collarCap,
    board,
    tile,
    ringLegal: ring(HL_LEGAL, 0.85),
    ringLoss: ring(HL_LOSS, 0.9),
    ringWin: ring(HL_WIN, 0.95),
    ringSelect: ring('#ffffff', 0.95),
    ringLast: ring('#ffffff', 0.35),
    dispose() {
      body.forEach((m) => m.dispose())
      collarCap.forEach((m) => m.dispose())
      collars.forEach((m) => { m.map?.dispose(); m.dispose() })
      board.dispose(); tile.dispose()
    },
  }
}

export { COLLAR_RADIUS }
