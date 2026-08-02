import * as THREE from 'three'

/**
 * Escena, renderer y loop. El loop es "render on demand": un juego de mesa esta
 * visualmente quieto el ~95% del tiempo, y renderizar frames identicos a 60fps
 * quema bateria y presupuesto termico en un celular. Se conserva el rAF (para
 * poder medir y para avanzar tweens) pero se saltea el render.
 *
 * @param {HTMLCanvasElement} canvas
 */
export function createWorld(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true })
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
  renderer.outputColorSpace = THREE.SRGBColorSpace
  renderer.toneMapping = THREE.ACESFilmicToneMapping
  renderer.shadowMap.enabled = true
  renderer.shadowMap.type = THREE.PCFSoftShadowMap

  const scene = new THREE.Scene()
  scene.background = new THREE.Color(0x1b1d21)

  const camera = new THREE.PerspectiveCamera(35, 1, 0.1, 100)
  camera.position.set(0, 6, 8)
  camera.lookAt(0, 0, 0)

  let dirty = true
  /** Marca que hay algo nuevo que dibujar. Todo lo que cambie el mundo la llama. */
  const invalidate = () => { dirty = true }

  /** @type {Set<(tMs: number) => boolean>} cada uno devuelve true si sigue activo */
  const steppers = new Set()

  function resize() {
    // visualViewport ademas de window: en iOS la barra de URL cambia el alto
    // sin disparar un resize de window.
    const w = window.visualViewport?.width ?? window.innerWidth
    const h = window.visualViewport?.height ?? window.innerHeight
    renderer.setSize(w, h, false)
    camera.aspect = w / h
    camera.updateProjectionMatrix()
    invalidate()
  }

  let resizeTimer = 0
  const onResize = () => {
    clearTimeout(resizeTimer)
    resizeTimer = setTimeout(resize, 100)
  }
  window.addEventListener('resize', onResize)
  window.visualViewport?.addEventListener('resize', onResize)
  resize()

  function loop(tMs) {
    requestAnimationFrame(loop)
    let running = false
    for (const step of steppers) {
      if (step(tMs)) running = true
      else steppers.delete(step)
    }
    if (dirty || running) {
      renderer.render(scene, camera)
      dirty = false
    }
  }
  requestAnimationFrame(loop)

  return { renderer, scene, camera, invalidate, steppers }
}
