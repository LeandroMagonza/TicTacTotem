import { createWorld } from './scene/world.js'

/**
 * Muestra un error fatal como cartel DOM. Existe porque los fallos mas comunes
 * de este stack (worker que no importa, contexto WebGL que no se crea) no dejan
 * nada visible: el tablero simplemente no responde.
 * @param {unknown} err
 */
export function fatal(err) {
  const msg = err instanceof Error ? `${err.message}\n\n${err.stack ?? ''}` : String(err)
  let el = document.getElementById('fatal')
  if (!el) {
    el = document.createElement('pre')
    el.id = 'fatal'
    document.body.appendChild(el)
  }
  el.textContent = msg
  console.error(err)
}

window.addEventListener('error', (e) => fatal(e.error ?? e.message))
window.addEventListener('unhandledrejection', (e) => fatal(e.reason))

try {
  const canvas = /** @type {HTMLCanvasElement} */ (document.getElementById('scene'))
  createWorld(canvas)
} catch (err) {
  fatal(err)
}
