/**
 * Envoltorio de promesas sobre el worker del motor.
 *
 * Dos cosas que se olvidan siempre y que aca son obligatorias:
 *
 *   1. onerror y onmessageerror TIENEN que mostrarse. Un worker que explota al
 *      importar falla en silencio: queda un tablero que no responde a los toques
 *      y nada evidente en la consola.
 *   2. Timeout por request. Sin el, un error dentro del worker deja la UI colgada
 *      en "pensando" para siempre.
 *
 * @param {(msg: string) => void} onFatal
 */
export function createEngineClient(onFatal) {
  const worker = new Worker(new URL('../engine/worker.js', import.meta.url), { type: 'module' })

  let nextId = 1
  /** @type {Map<number, {resolve: Function, reject: Function, timer: number}>} */
  const pending = new Map()

  worker.onmessage = (e) => {
    const { id, ok, result, error } = e.data ?? {}
    const p = pending.get(id)
    if (!p) return
    clearTimeout(p.timer)
    pending.delete(id)
    if (ok) p.resolve(result)
    else p.reject(new Error(error))
  }

  const fail = (what) => (e) => {
    const msg = `El motor fallo (${what}): ${e?.message ?? e?.type ?? 'sin detalle'}`
    onFatal(msg)
    for (const [, p] of pending) { clearTimeout(p.timer); p.reject(new Error(msg)) }
    pending.clear()
  }
  worker.onerror = fail('error')
  worker.onmessageerror = fail('mensaje ilegible')

  /**
   * @param {string} type @param {object} [payload] @param {number} [timeoutMs]
   */
  function call(type, payload = {}, timeoutMs = 20000) {
    const id = nextId++
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        pending.delete(id)
        reject(new Error(`El motor no contesto a "${type}" en ${timeoutMs / 1000}s.`))
      }, timeoutMs)
      pending.set(id, { resolve, reject, timer })
      worker.postMessage({ id, type, payload })
    })
  }

  return {
    newGame: (opts) => call('newGame', opts),
    applyMove: (moveId) => call('applyMove', { moveId }),
    undo: (plies) => call('undo', { plies }),
    loadPosition: (pos, turn) => call('loadPosition', { pos, turn }),
    aiMove: (opts) => call('aiMove', opts, 60000),
    analyze: (opts) => call('analyze', opts, 60000),
    terminate: () => worker.terminate(),
  }
}
