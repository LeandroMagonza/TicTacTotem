/**
 * Verificacion end-to-end del picking, con clicks REALES sobre el canvas.
 *
 * Proyecta el centro de cada casilla a coordenadas de pantalla, dispara
 * pointerdown/pointerup ahi, y comprueba que la pieza haya caido en la casilla
 * apuntada. Ejercita el camino entero: Pointer Events, umbral tap/drag,
 * raycast contra las cajas de picking y aplicacion de la jugada.
 *
 * Es la unica forma de agarrar el bug que se reporto —"apunte a la fila de
 * atras y me la puso adelante"—, porque depende del angulo de camara y de la
 * geometria de las cajas al mismo tiempo.
 *
 * Uso:  node test/picking.mjs [urlBase] [elevaciones...]
 */
import { spawn } from 'node:child_process'

const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
const PORT = 9334
const base = process.argv[2] ?? 'http://localhost:4173/'
const elevaciones = process.argv.slice(3).map(Number)
const ANGULOS = elevaciones.length ? elevaciones : [26, 30, 38, 45, 55, 62, 75]

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const browser = spawn(EDGE, [
  '--headless=new', '--disable-gpu', '--enable-unsafe-swiftshader',
  `--remote-debugging-port=${PORT}`, '--window-size=900,1400',
  '--no-first-run', '--no-default-browser-check',
  '--user-data-dir=' + (process.env.TEMP || '/tmp') + '/tictactotem-pick',
  'about:blank',
], { stdio: 'ignore' })

async function page() {
  for (let i = 0; i < 60; i++) {
    try {
      const list = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json()
      const p = list.find((t) => t.type === 'page')
      if (p?.webSocketDebuggerUrl) return p
    } catch { /* todavia no levanto */ }
    await sleep(250)
  }
  throw new Error('el browser no expuso el puerto de debug')
}

const target = await page()
const ws = new WebSocket(target.webSocketDebuggerUrl)
await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej })

let nextId = 1
const pending = new Map()
ws.onmessage = (ev) => {
  const m = JSON.parse(ev.data)
  const p = pending.get(m.id)
  if (!p) return
  pending.delete(m.id)
  m.error ? p.reject(new Error(JSON.stringify(m.error))) : p.resolve(m.result)
}
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = nextId++
  pending.set(id, { resolve, reject })
  ws.send(JSON.stringify({ id, method, params }))
})

const evaluar = async (expr) => {
  const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true })
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text)
  return r.result?.value
}

await send('Runtime.enable')
await send('Page.enable')

/** Coordenadas de pantalla del centro de una casilla, en CSS px. */
const PROYECTAR = (cell) => `(() => {
  const g = window.__game
  const THREE_V = g.camera
  const x = (${cell} % 3) - 1, z = Math.floor(${cell} / 3) - 1
  const v = { x, y: 0, z }
  const p = new (Object.getPrototypeOf(g.camera.position).constructor)(v.x, v.y, v.z)
  p.project(g.camera)
  const r = g.canvas.getBoundingClientRect()
  void THREE_V
  return { x: r.left + (p.x + 1) / 2 * r.width, y: r.top + (-p.y + 1) / 2 * r.height }
})()`

/**
 * Toque real sobre el canvas: se despachan PointerEvent de verdad desde la
 * pagina. Ejercita input.js entero — umbral tap/drag, captura de puntero y
 * raycast — que es justo lo que se quiere verificar.
 *
 * (Input.dispatchMouseEvent del CDP no sirve: en headless no llega a sintetizar
 * los eventos de puntero que escucha el canvas.)
 */
async function click(pt) {
  await evaluar(`(() => {
    const c = window.__game.canvas
    const o = { pointerId: 1, isPrimary: true, bubbles: true, cancelable: true,
                clientX: ${pt.x}, clientY: ${pt.y}, pointerType: 'mouse', button: 0 }
    c.dispatchEvent(new PointerEvent('pointerdown', o))
    c.dispatchEvent(new PointerEvent('pointerup', o))
    return true
  })()`)
  // Aplicar una jugada es asincrono: roundtrip al worker mas ~600 ms de
  // animacion. Hay que esperar a que termine, no un tiempo fijo.
  for (let i = 0; i < 40; i++) {
    await sleep(100)
    const quieto = await evaluar(
      "window.__game.state.phase === 'playing' || window.__game.state.phase === 'over'")
    if (quieto && await evaluar('!window.__game.tweens.pending')) return
  }
}

let fallos = 0
console.log('Click real sobre cada casilla, a distintas elevaciones de camara.')
console.log('Se apunta al centro de la casilla y se comprueba donde cayo la pieza.\n')
console.log(`${'elev'.padStart(5)}  ${'aciertos'.padStart(9)}   errores`)

for (const elev of ANGULOS) {
  let ok = 0
  const errores = []

  // La casilla 4 (centro) no se prueba: con la regla del centro no se puede
  // colocar ahi desde la mano, asi que un click correcto no mueve nada.
  for (const cell of [0, 1, 2, 3, 5, 6, 7, 8]) {
    await send('Page.navigate', { url: `${base}?elev=${elev}` })
    // Esperar a que el juego este LISTO PARA JUGAR, no solo cargado. Contra una
    // URL remota la carga tarda mucho mas que en localhost, y con una espera
    // corta el harness reportaba fallos de picking que no existian.
    let listo = false
    for (let i = 0; i < 300; i++) {
      listo = await evaluar(
        "!!(window.__game && window.__game.state.snapshot && window.__game.state.phase === 'playing')"
      ).catch(() => false) === true
      if (listo) break
      await sleep(100)
    }
    if (!listo) { errores.push(`celda ${cell}: el juego no cargo a tiempo`); continue }
    await evaluar("window.__game.state.mode = 'hotseat'")

    // Seleccionar la primera pieza de la mano. Si no quedo seleccionada, el
    // click siguiente no probaria nada: mejor decirlo que contarlo como fallo
    // de picking.
    await evaluar('window.__game.tap({kind:"hand", pieceId: window.__game.state.snapshot.hands[0][0]})')
    if (await evaluar('!!window.__game.state.selection') !== true) {
      errores.push(`celda ${cell}: no se pudo seleccionar la pieza de la mano`)
      continue
    }
    const pt = await evaluar(PROYECTAR(cell))
    await click(pt)

    const stacks = await evaluar('JSON.stringify(window.__game.state.snapshot.stacks.map(s=>s.length))')
    const ocupadas = JSON.parse(stacks).map((n, i) => (n ? i : -1)).filter((i) => i >= 0)
    if (ocupadas.length === 1 && ocupadas[0] === cell) ok++
    else errores.push(`apunte a ${cell} -> cayo en ${ocupadas.length ? ocupadas.join(',') : 'ningun lado'}`)
  }

  if (errores.length) fallos++
  console.log(`${String(elev).padStart(5)}  ${String(ok).padStart(6)}/8   ${errores.slice(0, 3).join(' · ') || '—'}`)
}

console.log()
console.log(fallos === 0
  ? 'Todas las casillas se aciertan a todos los angulos.'
  : `${fallos} elevacion(es) con errores de picking.`)

ws.close()
browser.kill()
process.exit(fallos ? 1 : 0)
