/**
 * Driver headless minimo sobre el DevTools Protocol.
 *
 * Existe porque `--screenshot` con `--virtual-time-budget` NO sirve para esta
 * app: el tiempo virtual adelanta los timers del hilo principal pero no espera
 * los roundtrips del Web Worker, asi que la foto sale antes de que el motor
 * conteste. Aca se espera una condicion de JS de verdad.
 *
 * Ademas junta los errores de consola, que es lo que un `--screenshot` pelado
 * nunca te va a dar.
 *
 * Uso:
 *   node test/shot.mjs <url> <salida.png> [ancho] [alto] [condicionJS]
 */
import { spawn } from 'node:child_process'
import { writeFileSync } from 'node:fs'

const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
const PORT = 9333

const [url, out, w = '900', h = '1400', cond = 'true'] = process.argv.slice(2)
if (!url || !out) { console.error('uso: node test/shot.mjs <url> <salida.png> [w] [h] [cond]'); process.exit(2) }

const browser = spawn(EDGE, [
  '--headless=new', '--disable-gpu', '--enable-unsafe-swiftshader',
  `--remote-debugging-port=${PORT}`,
  `--window-size=${w},${h}`,
  '--no-first-run', '--no-default-browser-check',
  '--user-data-dir=' + (process.env.TEMP || '/tmp') + '/tictactotem-cdp',
  'about:blank',
], { stdio: 'ignore' })

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

async function targets() {
  for (let i = 0; i < 60; i++) {
    try {
      const r = await fetch(`http://127.0.0.1:${PORT}/json/list`)
      const list = await r.json()
      const page = list.find((t) => t.type === 'page')
      if (page?.webSocketDebuggerUrl) return page
    } catch { /* todavia no levanto */ }
    await sleep(250)
  }
  throw new Error('el browser no expuso el puerto de debug')
}

const page = await targets()
const ws = new WebSocket(page.webSocketDebuggerUrl)
await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej })

let nextId = 1
const pending = new Map()
const consoleErrors = []
const pageErrors = []

ws.onmessage = (ev) => {
  const msg = JSON.parse(ev.data)
  if (msg.id && pending.has(msg.id)) {
    const { resolve, reject } = pending.get(msg.id)
    pending.delete(msg.id)
    msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result)
    return
  }
  if (msg.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(msg.params.type)) {
    consoleErrors.push(`[${msg.params.type}] ` +
      msg.params.args.map((a) => a.value ?? a.description ?? a.type).join(' '))
  }
  if (msg.method === 'Runtime.exceptionThrown') {
    const d = msg.params.exceptionDetails
    pageErrors.push(d.exception?.description ?? d.text)
  }
}

const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = nextId++
  pending.set(id, { resolve, reject })
  ws.send(JSON.stringify({ id, method, params }))
})

await send('Runtime.enable')
await send('Page.enable')
await send('Page.navigate', { url })

// Esperar la condicion de verdad, no un tiempo fijo.
let ok = false
for (let i = 0; i < 120; i++) {
  await sleep(250)
  try {
    const r = await send('Runtime.evaluate', { expression: `!!(${cond})`, returnByValue: true })
    if (r.result?.value === true) { ok = true; break }
  } catch { /* la pagina todavia no evalua */ }
}

await sleep(400)   // un frame extra para que termine de dibujar
const shot = await send('Page.captureScreenshot', { format: 'png' })
writeFileSync(out, Buffer.from(shot.data, 'base64'))

const estado = await send('Runtime.evaluate', {
  expression: `JSON.stringify({
    ply: window.__game?.state?.snapshot?.ply ?? null,
    turno: window.__game?.state?.snapshot?.turn ?? null,
    pilas: (window.__game?.state?.snapshot?.stacks ?? []).map(s => s.length),
    resultado: window.__game?.state?.snapshot?.result ?? null,
    drawCalls: window.__game?.info ?? null
  })`,
  returnByValue: true,
})

console.log(ok ? 'condicion cumplida' : 'TIMEOUT esperando la condicion')
console.log('estado:', estado.result?.value ?? '(sin __game)')
if (pageErrors.length) console.log('EXCEPCIONES:\n  ' + pageErrors.join('\n  '))
if (consoleErrors.length) console.log('CONSOLA:\n  ' + consoleErrors.join('\n  '))
console.log(`escrito ${out}`)

ws.close()
browser.kill()
process.exit(pageErrors.length ? 1 : 0)
