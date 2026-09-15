import { startGame } from './app/game.js'

/**
 * Muestra un error fatal como cartel DOM. Existe porque los fallos mas comunes
 * de este stack (un worker que no importa, un contexto WebGL que no se crea) no
 * dejan nada visible: el tablero simplemente no responde a los toques.
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

/**
 * Los errores de extensiones del navegador (MetaMask y similares inyectan un
 * script en toda pagina y fallan solos) llegan por los mismos eventos globales.
 * No son nuestros y no hay que taparle el juego al usuario por ellos.
 * @param {unknown} err @param {string} [origen]
 */
function esDeExtension(err, origen = '') {
  const texto = `${origen}\n${err instanceof Error ? `${err.message}\n${err.stack ?? ''}` : String(err ?? '')}`
  return /(chrome|moz|safari-web)-extension:\/\//.test(texto) || /MetaMask/i.test(texto)
}

window.addEventListener('error', (e) => {
  const err = e.error ?? e.message
  if (!esDeExtension(err, e.filename)) fatal(err)
})
window.addEventListener('unhandledrejection', (e) => {
  if (!esDeExtension(e.reason)) fatal(e.reason)
})

const canvas = /** @type {HTMLCanvasElement} */ (document.getElementById('scene'))
const uiRoot = /** @type {HTMLElement} */ (document.getElementById('ui'))

startGame(canvas, uiRoot, fatal)
  .then((game) => {
    // Mango de debug: permite que un test headless juegue una partida entera y
    // que cualquier bug se reproduzca desde una URL.
    // @ts-ignore
    window.__game = game
  })
  .catch(fatal)
