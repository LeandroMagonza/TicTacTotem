import * as THREE from 'three'
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js'
import { MAX_STACK_HEIGHT } from './geometry.js'

/**
 * Escena, renderer y loop.
 *
 * El loop es "render on demand": un juego de mesa esta visualmente quieto el
 * ~95% del tiempo, y renderizar frames identicos a 60fps quema bateria y
 * presupuesto termico en un celular. Se conserva el rAF (para poder medir y para
 * avanzar tweens) pero se saltea la llamada a render.
 *
 * @param {HTMLCanvasElement} canvas
 */
export function createWorld(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true })
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
  renderer.outputColorSpace = THREE.SRGBColorSpace
  renderer.toneMapping = THREE.ACESFilmicToneMapping
  renderer.toneMappingExposure = 1.05
  renderer.shadowMap.enabled = true
  renderer.shadowMap.type = THREE.PCFSoftShadowMap

  const scene = new THREE.Scene()
  scene.background = new THREE.Color(0x1b1d21)

  // Mapa de entorno procedural: cero bytes de assets, y es la mejora visual mas
  // grande que existe para modelos sin textura de un solo material, que si no
  // se ven como plastico plano.
  const pmrem = new THREE.PMREMGenerator(renderer)
  scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture

  const key = new THREE.DirectionalLight(0xffffff, 2.1)
  key.position.set(-3.4, 5.2, 3.0)
  key.castShadow = true
  key.shadow.mapSize.set(1024, 1024)
  // Ajustar la camara de sombra bien pegada al contenido es lo que hace que un
  // mapa de 1024 se vea nitido; con la caja por defecto se ve como barro.
  const sc = key.shadow.camera
  sc.left = -3.6; sc.right = 3.6; sc.top = 3.6; sc.bottom = -3.6
  sc.near = 0.5; sc.far = 14
  sc.updateProjectionMatrix()
  scene.add(key)

  scene.add(new THREE.HemisphereLight(0xdfe6f2, 0x2a2118, 0.55))

  const camera = new THREE.PerspectiveCamera(35, 1, 0.1, 100)

  let dirty = true
  const invalidate = () => { dirty = true }

  /** @type {((now:number)=>boolean)[]} */
  const steppers = []

  let onResize = null
  const setResizeHandler = (fn) => { onResize = fn }

  function resize() {
    const w = window.visualViewport?.width ?? window.innerWidth
    const h = window.visualViewport?.height ?? window.innerHeight
    renderer.setSize(w, h, false)
    camera.aspect = w / h
    camera.updateProjectionMatrix()
    onResize?.(w, h)
    invalidate()
  }

  let timer = 0
  const debouncedResize = () => { clearTimeout(timer); timer = setTimeout(resize, 100) }
  window.addEventListener('resize', debouncedResize)
  window.visualViewport?.addEventListener('resize', debouncedResize)

  function loop(now) {
    requestAnimationFrame(loop)
    let running = false
    for (const step of steppers) if (step(now)) running = true
    if (dirty || running) {
      renderer.render(scene, camera)
      dirty = false
    }
  }
  requestAnimationFrame(loop)

  const setQuality = (high) => {
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, high ? 2 : 1.5))
    renderer.shadowMap.enabled = high
    scene.traverse((o) => { if (o.isMesh && o.material) o.material.needsUpdate = true })
    resize()
  }

  return { renderer, scene, camera, invalidate, steppers, resize, setResizeHandler, setQuality }
}

export { MAX_STACK_HEIGHT }
