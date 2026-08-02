import * as THREE from 'three'
import {
  FOV, ELEVATION_LOW, ELEVATION_HIGH, ELEVATION_MIN, ELEVATION_MAX,
  MAX_STACK_HEIGHT, contentBoxes,
} from './geometry.js'

const DEG = Math.PI / 180

/** Altura a la que apunta la camara. Cerca del plano del tablero. */
const LOOK_Y = 0.18

/**
 * Rig orbital: un pivot en el centro del tablero con la camara como hijo.
 *
 * Rig y no camara libre porque asi los cuartos de vuelta, los presets de
 * inclinacion, el giro de 180 grados del hotseat y la orbita de la pantalla de
 * victoria son todos one-liners, y `lookAt` desaparece del codigo.
 *
 * @param {THREE.PerspectiveCamera} camera
 */
export function createRig(camera) {
  const pivot = new THREE.Object3D()
  camera.fov = FOV
  pivot.add(camera)

  const state = {
    azimuth: 0,           // radianes
    elevation: ELEVATION_LOW * DEG,
    distance: 12,
    layout: /** @type {'portrait'|'landscape'} */ ('portrait'),
  }

  function apply() {
    const { azimuth, elevation, distance } = state
    const cosE = Math.cos(elevation)
    camera.position.set(
      distance * cosE * Math.sin(azimuth),
      distance * Math.sin(elevation),
      distance * cosE * Math.cos(azimuth),
    )
    // Mirar cerca del plano del tablero. Apuntar mas arriba empuja el tablero
    // hacia abajo en pantalla y deja un hueco grande arriba.
    camera.lookAt(0, LOOK_Y, 0)
    camera.updateProjectionMatrix()
  }

  const _p = new THREE.Vector3()
  const _right = new THREE.Vector3()
  const _up = new THREE.Vector3()
  const _u = new THREE.Vector3()

  /**
   * Distancia para que entre todo el contenido.
   *
   * Proyecta las 8 esquinas de la caja al espacio de camara y resuelve cual es
   * la que aprieta. La version por esfera envolvente es mas corta pero
   * desperdicia un monton de margen en una escena con forma de caja plana como
   * esta: el tablero terminaba ocupando un tercio del alto de la pantalla.
   *
   * La cuenta sale exacta porque x e y en espacio de camara NO dependen de la
   * distancia (right y up son perpendiculares al eje camara-centro), asi que
   * solo z se mueve:
   *
   *     z_cam = d - proyeccion de (P - centro) sobre u
   *     hace falta |x_cam| <= tan(hFov/2) * z_cam
   *     => d >= |x_cam| / tan(hFov/2) + proyeccion
   *
   * y la distancia buena es el maximo sobre las 8 esquinas y los dos ejes.
   */
  function fit(aspect) {
    const boxes = contentBoxes(state.layout)

    const vFov = FOV * DEG
    const tanV = Math.tan(vFov / 2)
    const tanH = tanV * aspect

    // Base de camara para la orientacion actual, tomada de three para no
    // pelearse con sus convenciones de signo.
    const { azimuth, elevation } = state
    const cosE = Math.cos(elevation)
    _u.set(cosE * Math.sin(azimuth), Math.sin(elevation), cosE * Math.cos(azimuth))
    const target = new THREE.Vector3(0, LOOK_Y, 0)
    camera.position.copy(target).addScaledVector(_u, 1)
    camera.lookAt(target)
    camera.updateMatrixWorld(true)
    _right.setFromMatrixColumn(camera.matrixWorld, 0)
    _up.setFromMatrixColumn(camera.matrixWorld, 1)

    let d = 0
    for (const b of boxes) {
      for (const sx of [-b.halfX, b.halfX]) {
        for (const sy of [b.yLow, b.yHigh]) {
          for (const sz of [-b.halfZ, b.halfZ]) {
            _p.set(sx, sy, sz).sub(target)
            const along = _p.dot(_u)
            const x = Math.abs(_p.dot(_right))
            const y = Math.abs(_p.dot(_up))
            d = Math.max(d, x / tanH + along, y / tanV + along)
          }
        }
      }
    }

    state.distance = d * 1.06        // un pelin de aire
    apply()
    return state.distance
  }

  return {
    pivot,
    state,
    apply,
    fit,
    setLayout(layout) { state.layout = layout },
    setElevationDeg(deg) {
      state.elevation = Math.min(ELEVATION_MAX, Math.max(ELEVATION_MIN, deg)) * DEG
      apply()
    },
    /** Alterna entre 45 (se lee la altura) y 62 (se ve todo el tablero). */
    toggleTilt() {
      const cur = state.elevation / DEG
      const target = Math.abs(cur - ELEVATION_LOW) < 1 ? ELEVATION_HIGH : ELEVATION_LOW
      return target
    },
    /** @param {number} quarters */
    rotateQuarters(quarters) {
      state.azimuth += (quarters * Math.PI) / 2
      apply()
    },
    orbit(dAzimuth, dElevation) {
      state.azimuth += dAzimuth
      state.elevation = Math.min(ELEVATION_MAX * DEG,
        Math.max(ELEVATION_MIN * DEG, state.elevation + dElevation))
      apply()
    },
  }
}

export { DEG }
