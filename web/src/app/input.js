import * as THREE from 'three'

/**
 * Entrada por Pointer Events, unica y para todo: mouse, touch y lapiz por el
 * mismo camino. Nunca eventos de mouse ni de touch por separado.
 *
 * INVARIANTE, y va en serio: este archivo y todo el renderer NO conocen ni una
 * regla del juego. Consumen la lista de jugadas legales que da el motor y nada
 * mas. Si en algun momento aparece un `Math.abs(a - b) === 1` por aca, o una
 * comparacion de niveles, o un chequeo de "es mi pieza de arriba", el diseño se
 * rompio: eso vive en engine/rules.js.
 *
 * @param {object} args
 * @param {HTMLCanvasElement} args.canvas
 * @param {THREE.Camera} args.camera
 * @param {() => THREE.Object3D[]} args.pickables
 * @param {(hit: {kind:string, index?:number, side?:number, slot?:number, pieceId?:number}|null) => void} args.onTap
 * @param {(dx:number, dy:number) => void} args.onDrag
 */
export function createInput({ canvas, camera, pickables, onTap, onDrag }) {
  const raycaster = new THREE.Raycaster()
  const ndc = new THREE.Vector2()

  const DRAG_PX = 8        // umbral tap/drag
  const TAP_MS = 700

  let active = null        // { id, x0, y0, x, y, t0, dragging }

  const toNdc = (ev) => {
    const r = canvas.getBoundingClientRect()
    ndc.x = ((ev.clientX - r.left) / r.width) * 2 - 1
    ndc.y = -((ev.clientY - r.top) / r.height) * 2 + 1
  }

  function pick(ev) {
    toNdc(ev)
    raycaster.setFromCamera(ndc, camera)
    // recursivo en false: son 19 cajas planas, sub-microsegundo.
    const hits = raycaster.intersectObjects(pickables(), false)
    return hits.length ? hits[0].object.userData : null
  }

  canvas.addEventListener('pointerdown', (ev) => {
    if (active) return                        // se ignoran punteros secundarios
    active = { id: ev.pointerId, x0: ev.clientX, y0: ev.clientY, x: ev.clientX, y: ev.clientY, t0: performance.now(), dragging: false }
    canvas.setPointerCapture(ev.pointerId)
  })

  canvas.addEventListener('pointermove', (ev) => {
    if (!active || ev.pointerId !== active.id) return
    const dx = ev.clientX - active.x
    const dy = ev.clientY - active.y
    if (!active.dragging) {
      const far = Math.hypot(ev.clientX - active.x0, ev.clientY - active.y0)
      if (far > DRAG_PX) active.dragging = true
    }
    if (active.dragging) onDrag(dx, dy)
    active.x = ev.clientX
    active.y = ev.clientY
  })

  const end = (ev) => {
    if (!active || ev.pointerId !== active.id) return
    const wasTap = !active.dragging && performance.now() - active.t0 < TAP_MS
    if (canvas.hasPointerCapture(ev.pointerId)) canvas.releasePointerCapture(ev.pointerId)
    active = null
    if (wasTap) onTap(pick(ev))
  }
  canvas.addEventListener('pointerup', end)
  canvas.addEventListener('pointercancel', () => { active = null })

  return {
    get dragging() { return !!active?.dragging },
    /** Expuesto para diagnostico headless: que hay bajo un punto de pantalla. */
    pickAt(clientX, clientY) {
      const r = canvas.getBoundingClientRect()
      ndc.x = ((clientX - r.left) / r.width) * 2 - 1
      ndc.y = -((clientY - r.top) / r.height) * 2 + 1
      raycaster.setFromCamera(ndc, camera)
      return raycaster.intersectObjects(pickables(), false)
        .map((h) => ({ ...h.object.userData, dist: h.distance, y: h.point.y }))
    },
  }
}

/**
 * Teclado. Son ~40 lineas y compran tres cosas: gente que en escritorio lo
 * prefiere, accesibilidad basica, y —lo mas importante— una forma determinista
 * e independiente de pixeles de que un test headless juegue una partida entera.
 *
 * @param {object} args
 * @param {() => void} args.onUndo
 * @param {() => void} args.onCancel
 * @param {(q:number) => void} args.onRotate
 * @param {() => void} args.onTilt
 * @param {(cell:number) => void} args.onCell
 * @param {(slot:number) => void} args.onHandSlot
 */
export function createKeyboard({ onUndo, onCancel, onRotate, onTilt, onCell, onHandSlot }) {
  let cursor = 4
  const handler = (ev) => {
    if (ev.target instanceof HTMLInputElement) return
    const k = ev.key
    let handled = true
    if (k === 'ArrowLeft') cursor = cursor % 3 === 0 ? cursor + 2 : cursor - 1
    else if (k === 'ArrowRight') cursor = cursor % 3 === 2 ? cursor - 2 : cursor + 1
    else if (k === 'ArrowUp') cursor = (cursor + 6) % 9
    else if (k === 'ArrowDown') cursor = (cursor + 3) % 9
    else if (k === 'Enter' || k === ' ') onCell(cursor)
    else if (k === 'Escape') onCancel()
    else if (k === 'u' || k === 'U') onUndo()
    else if (k === 'r' || k === 'R') onRotate(1)
    else if (k === 't' || k === 'T') onTilt()
    else if (k >= '1' && k <= '5') onHandSlot(Number(k) - 1)
    else handled = false
    if (handled) ev.preventDefault()
  }
  window.addEventListener('keydown', handler)
  return {
    get cursor() { return cursor },
    destroy: () => window.removeEventListener('keydown', handler),
  }
}
