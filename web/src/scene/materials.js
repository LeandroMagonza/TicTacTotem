import * as THREE from 'three'
import { COIN_RADIUS, COIN_THICKNESS } from './geometry.js'

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

/** Tinta de los numerales. Los dos colores de equipo son claros y la bancan. */
const INK = 'rgba(0,0,0,0.78)'

/**
 * Textura del canto de la moneda: el numeral del nivel repetido alrededor del
 * borde, para que se lea desde cualquier angulo.
 *
 * El canvas se dimensiona con la proporcion REAL del canto (circunferencia por
 * grosor), asi el numeral sale con su forma y no estirado a lo ancho: una
 * moneda de nivel 5 tiene el canto el doble de alto que una de nivel 1, y su
 * textura tiene el doble de filas.
 * @param {number} rank @param {number} owner
 */
function edgeTexture(rank, owner) {
  const W = 1024
  const circunferencia = 2 * Math.PI * COIN_RADIUS
  const H = Math.round(W * COIN_THICKNESS[rank] / circunferencia)
  const cv = document.createElement('canvas')
  cv.width = W; cv.height = H
  const g = cv.getContext('2d')

  g.fillStyle = TEAM_COLOR[owner]
  g.fillRect(0, 0, W, H)

  // Franja mas oscura arriba y abajo, para que el borde tenga definicion.
  const franja = Math.max(2, Math.round(H * 0.09))
  g.fillStyle = 'rgba(0,0,0,0.22)'
  g.fillRect(0, 0, W, franja)
  g.fillRect(0, H - franja, W, franja)

  // El numeral, 10 veces alrededor.
  const N = 10
  g.fillStyle = INK
  g.font = `bold ${Math.round(H * 0.72)}px system-ui, sans-serif`
  g.textAlign = 'center'
  g.textBaseline = 'middle'
  for (let i = 0; i < N; i++) g.fillText(String(rank), (W / N) * (i + 0.5), H / 2 + 1)

  const tex = new THREE.CanvasTexture(cv)
  tex.colorSpace = THREE.SRGBColorSpace
  tex.anisotropy = 4
  return tex
}

/**
 * Textura de la cara de la moneda: un reborde y el numeral grande.
 *
 * Las tapas de CylinderGeometry mapean el canvas asi: el eje V (arriba del
 * canvas) cae sobre +X del mundo y el eje U (derecha del canvas) sobre +Z. La
 * camara por defecto (azimut 0) esta en +Z mirando a -Z, asi que "arriba en
 * pantalla" es -Z y "derecha en pantalla" es +X. Para que el numeral se lea
 * derecho hay que dibujarlo girado 90 grados antihorario. Las piezas ademas
 * giran con el azimut de la camara (ver pieces.js), asi que el numeral queda
 * derecho tambien despues de un cuarto de vuelta o del giro de hotseat.
 * @param {number} rank @param {number} owner
 */
function faceTexture(rank, owner) {
  const S = 256
  const cv = document.createElement('canvas')
  cv.width = S; cv.height = S
  const g = cv.getContext('2d')

  g.fillStyle = TEAM_COLOR[owner]
  g.fillRect(0, 0, S, S)

  // Reborde, como el listel de una moneda de verdad: un anillo mas oscuro cerca
  // del canto y un filete claro adentro para que la cara tenga relieve.
  g.beginPath()
  g.arc(S / 2, S / 2, S * 0.46, 0, Math.PI * 2)
  g.lineWidth = S * 0.05
  g.strokeStyle = 'rgba(0,0,0,0.22)'
  g.stroke()
  g.beginPath()
  g.arc(S / 2, S / 2, S * 0.415, 0, Math.PI * 2)
  g.lineWidth = S * 0.012
  g.strokeStyle = 'rgba(255,255,255,0.28)'
  g.stroke()

  // El numeral, grande y girado para la convencion de UV de la tapa.
  g.save()
  g.translate(S / 2, S / 2)
  g.rotate(-Math.PI / 2)
  g.fillStyle = INK
  g.font = `bold ${Math.round(S * 0.6)}px system-ui, sans-serif`
  g.textAlign = 'center'
  g.textBaseline = 'middle'
  g.fillText(String(rank), 0, S * 0.03)
  g.restore()

  const tex = new THREE.CanvasTexture(cv)
  tex.colorSpace = THREE.SRGBColorSpace
  tex.anisotropy = 4
  return tex
}

/**
 * Materiales compartidos. Los de moneda se cachean por nivel y dueño: diez
 * combinaciones como mucho, aplicadas a todas las piezas.
 *
 * El resaltado de seleccion NO cambia materiales. Unity reemplazaba el material
 * de la pieza por un tercero "seleccionado" (Piece.cs:28-34), lo que destruye el
 * color de equipo justo cuando mas necesitas saber de quien es la pieza. Aca la
 * seleccion es un anillo en el piso mas un rebote.
 */
export function createMaterials() {
  // Un toque de metal para que la moneda agarre el mapa de entorno y no se vea
  // como plastico, pero poco: con mas, el color de equipo se lava en los brillos.
  const coinLook = { roughness: 0.5, metalness: 0.18 }

  const body = TEAM_COLOR.map((c) => new THREE.MeshStandardMaterial({
    color: new THREE.Color(c),
    roughness: 0.55,
    metalness: 0.0,
  }))

  /** @type {Map<string, THREE.MeshStandardMaterial>} */
  const cache = new Map()
  const cached = (key, make) => {
    let m = cache.get(key)
    if (!m) { m = make(); cache.set(key, m) }
    return m
  }

  const coinEdgeFor = (rank, owner) => cached(`edge:${rank}:${owner}`, () =>
    new THREE.MeshStandardMaterial({ map: edgeTexture(rank, owner), ...coinLook }))

  const coinFaceFor = (rank, owner) => cached(`face:${rank}:${owner}`, () =>
    new THREE.MeshStandardMaterial({ map: faceTexture(rank, owner), ...coinLook }))

  // Cara de abajo, lisa y un poco mas oscura: nunca se ve salvo en el vuelo de
  // una pieza que cae.
  const coinBottom = TEAM_COLOR.map((c) => new THREE.MeshStandardMaterial({
    color: new THREE.Color(c).multiplyScalar(0.88),
    ...coinLook,
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
    coinEdgeFor,
    coinFaceFor,
    coinBottom,
    board,
    tile,
    ringLegal: ring(HL_LEGAL, 0.85),
    ringLoss: ring(HL_LOSS, 0.9),
    ringWin: ring(HL_WIN, 0.95),
    ringSelect: ring('#ffffff', 0.95),
    ringLast: ring('#ffffff', 0.35),
    dispose() {
      body.forEach((m) => m.dispose())
      coinBottom.forEach((m) => m.dispose())
      cache.forEach((m) => { m.map?.dispose(); m.dispose() })
      board.dispose(); tile.dispose()
    },
  }
}
