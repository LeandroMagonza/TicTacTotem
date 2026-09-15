/**
 * Traza de estado del juego en headless: fase, ply, largo del historial y
 * resultado, muestreados cada 100 ms mientras el bot juega contra si mismo.
 *
 * Contesta dos preguntas que a ojo no se contestan:
 *   - el historial acompaña a la partida (record.length == ply en todo momento)
 *   - los segundos entre jugadas son el ritmo, no un recargo sobre el pensar:
 *     con pausa de N s el ciclo mide N s + lo que tarda la animacion, y no
 *     N s + pensar + animacion.
 *
 * Uso:  node test/traza.mjs [segundos] [dificultad] [urlBase]
 */
import { spawn } from 'node:child_process'

const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
const PORT = 9335
const segundos = Number(process.argv[2] ?? 0)
const dificultad = process.argv[3] ?? 'dificil'
const base = process.argv[4] ?? 'http://localhost:4173/'
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const browser = spawn(EDGE, [
  '--headless=new', '--disable-gpu', '--enable-unsafe-swiftshader', '--disable-extensions',
  `--remote-debugging-port=${PORT}`, '--window-size=900,1400',
  '--no-first-run', '--no-default-browser-check',
  '--user-data-dir=' + (process.env.TEMP || '/tmp') + '/tictactotem-traza',
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
await send('Page.navigate', { url: base })
for (let i = 0; i < 300; i++) {
  const listo = await evaluar("!!(window.__game && window.__game.state.snapshot && window.__game.state.phase === 'playing')").catch(() => false)
  if (listo) break
  await sleep(100)
}
// El historial se abre YA, con la partida arrancando: es el caso en que antes
// se quedaba clavado en las jugadas que habia al abrirlo.
await evaluar(`(() => { const g = window.__game; g.state.mode = 'auto'; g.state.difficulty = '${dificultad}'; g.setSeconds(${segundos}); g.openHistory(); g.playPause(); return true })()`)

let ultimo = ''
let plyPrevio = 0
let tPly = Date.now()
const desfasajes = []
const ciclos = []
const t0 = Date.now()
while (Date.now() - t0 < 120000) {
  const e = await evaluar("(() => { const s = window.__game.state; return { fase: s.phase, ply: s.snapshot.ply, hist: s.record.length, fin: s.snapshot.result ? s.snapshot.result.reason : '-', play: s.auto.playing, panel: document.querySelectorAll('.hist-line').length, turno: document.querySelector('.turn-text').textContent } })()")
  const k = `${e.fase}|ply ${e.ply}|historial ${e.hist}|panel ${e.panel}|${e.fin}|${e.play ? 'play' : 'pausa'}|${e.turno}`
  if (k !== ultimo) { console.log(`${String(Date.now() - t0).padStart(6)} ms  ${k}`); ultimo = k }
  // El historial solo puede ir atrasado MIENTRAS anima; en reposo tiene que
  // tener una entrada por ply, y el panel abierto tiene que mostrarlas todas.
  if (e.fase !== 'animating' && (e.hist !== e.ply || e.panel !== e.ply)) {
    desfasajes.push(`${Date.now() - t0} ms: ply ${e.ply}, record ${e.hist}, panel ${e.panel} (${e.fase})`)
  }
  if (e.ply > plyPrevio) { ciclos.push(Date.now() - tPly); tPly = Date.now(); plyPrevio = e.ply }
  if (e.fin !== '-') break
  await sleep(100)
}
console.log('historial final:', await evaluar("JSON.stringify(window.__game.state.record.map(r => r.ply + ':' + r.text + (r.ai ? ' [' + r.ai.value + '/' + r.ai.plies + ']' : '')))"))
console.log('link:', await evaluar('window.__game.linkDePartida()'))
console.log(`ciclo por jugada con pausa de ${segundos} s:`,
  ciclos.map((m) => (m / 1000).toFixed(1)).join(' '), 's')
if (desfasajes.length) {
  console.log(`DESFASAJES (${desfasajes.length}):`)
  for (const d of desfasajes) console.log('  ' + d)
} else {
  console.log('historial y panel al dia en todos los muestreos')
}

ws.close()
browser.kill()
