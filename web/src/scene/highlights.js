import * as THREE from 'three'
import { COLLAR_RADIUS, cellToWorld } from './geometry.js'

/**
 * Anillos de resaltado: seleccion, destinos legales y ultima jugada.
 *
 * Los anillos de destino se dibujan A LA ALTURA A LA QUE CAERIA LA PIEZA, asi
 * que el anillo previsualiza la altura resultante de la pila. Es informacion
 * gratis: se ve de antemano si una jugada arma un totem alto.
 *
 * @param {ReturnType<import('./materials.js').createMaterials>} mats
 */
export function createHighlights(mats) {
  const group = new THREE.Group()

  const ringGeo = new THREE.RingGeometry(COLLAR_RADIUS * 1.06, COLLAR_RADIUS * 1.3, 32)
  const discGeo = new THREE.CircleGeometry(COLLAR_RADIUS * 1.3, 32)

  const mk = (geo, mat) => {
    const m = new THREE.Mesh(geo, mat)
    m.rotation.x = -Math.PI / 2
    m.visible = false
    m.renderOrder = 2
    group.add(m)
    return m
  }

  const select = mk(ringGeo, mats.ringSelect)
  const lastFrom = mk(discGeo, mats.ringLast)
  const lastTo = mk(ringGeo, mats.ringLast)
  /** @type {THREE.Mesh[]} */
  const dests = Array.from({ length: 9 }, () => mk(ringGeo, mats.ringLegal))
  // Anillos anchos de linea ganadora, a nivel de BALDOSA. No discos encima de la
  // pila: eso tapaba justo las piezas que hay que mirar. Y en blanco, no en
  // dorado: el dorado se confunde con el ambar del equipo A, asi que cuando
  // ganaba B la linea se leia como si fuera de A.
  const winGeo = new THREE.RingGeometry(COLLAR_RADIUS * 1.15, COLLAR_RADIUS * 1.45, 32)
  /** @type {THREE.Mesh[]} */
  const winCells = Array.from({ length: 3 }, () => mk(winGeo, mats.ringSelect))

  /** Anillo de seleccion bajo una posicion de mundo. */
  function showSelection(world) {
    if (!world) { select.visible = false; return }
    select.position.set(world.x, world.y + 0.02, world.z)
    select.visible = true
  }

  /**
   * @param {{to: number, immediateResult: null|'win'|'loss'}[]} moves
   * @param {(cell:number)=>number} landingY
   */
  function showDestinations(moves, landingY) {
    dests.forEach((d) => { d.visible = false })
    moves.forEach((m, i) => {
      if (i >= dests.length) return
      const d = dests[i]
      const { x, z } = cellToWorld(m.to)
      d.position.set(x, landingY(m.to) + 0.02, z)
      // Ambar avisa que la jugada destapa una linea rival y pierde en el acto.
      // Es la regla mas contraintuitiva del juego (GameManager.cs:206-211) y
      // sale gratis: el motor ya calcula ese valor.
      d.material = m.immediateResult === 'loss' ? mats.ringLoss
        : m.immediateResult === 'win' ? mats.ringWin
        : mats.ringLegal
      d.visible = true
    })
  }

  function showLastMove(from, to, landingY) {
    if (from == null || to == null) {
      lastFrom.visible = lastTo.visible = false
      return
    }
    if (typeof from === 'number') {
      const a = cellToWorld(from)
      lastFrom.position.set(a.x, 0.015, a.z)
      lastFrom.visible = true
    } else {
      lastFrom.visible = false
    }
    const b = cellToWorld(to)
    lastTo.position.set(b.x, landingY(to) + 0.015, b.z)
    lastTo.visible = true
  }

  function showWinningLine(cells) {
    winCells.forEach((c) => { c.visible = false })
    if (!cells) return
    cells.slice(0, 3).forEach((cell, i) => {
      const { x, z } = cellToWorld(cell)
      winCells[i].position.set(x, 0.025, z)
      winCells[i].visible = true
    })
  }

  function clear() {
    select.visible = false
    dests.forEach((d) => { d.visible = false })
    winCells.forEach((c) => { c.visible = false })
  }

  return { group, showSelection, showDestinations, showLastMove, showWinningLine, clear }
}
