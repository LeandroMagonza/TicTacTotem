/**
 * Lista de tweens minima. Nada de GSAP ni tween.js: el juego anima exactamente
 * tres cosas (posicion, escala, color/opacidad) y una dependencia para eso es
 * mas codigo del que reemplaza.
 *
 * Lo unico importante es avanzar por reloj de pared y no por frames, asi un
 * frame perdido no ralentiza la animacion.
 */

export const easeOut = (t) => 1 - (1 - t) ** 3
export const easeIn = (t) => t * t * t
export const easeInOut = (t) => (t < 0.5 ? 4 * t ** 3 : 1 - (-2 * t + 2) ** 3 / 2)

export function createTweens() {
  /** @type {{start:number, dur:number, ease:Function, step:Function, done?:Function, dead?:boolean}[]} */
  let list = []

  /**
   * @param {object} t
   * @param {number} t.dur   duracion en ms
   * @param {(k:number)=>void} t.step  recibe el progreso ya suavizado, 0..1
   * @param {Function} [t.done]
   * @param {Function} [t.ease]
   * @param {number} [t.delay]
   */
  function add({ dur, step, done, ease = easeInOut, delay = 0 }) {
    const tw = { start: performance.now() + delay, dur, ease, step, done, dead: false }
    list.push(tw)
    return tw
  }

  /** Encadena tweens; devuelve una promesa que resuelve al terminar el ultimo. */
  function sequence(steps) {
    return steps.reduce(
      (prev, s) => prev.then(() => new Promise((res) => add({ ...s, done: res }))),
      Promise.resolve(),
    )
  }

  /** Avanza todo. Devuelve true si queda algo vivo (el loop tiene que seguir dibujando). */
  function update(now) {
    if (list.length === 0) return false
    let alive = false
    for (const tw of list) {
      if (tw.dead) continue
      const k = tw.dur <= 0 ? 1 : (now - tw.start) / tw.dur
      if (k < 0) { alive = true; continue }   // todavia en el delay
      if (k >= 1) {
        tw.step(1)
        tw.dead = true
        tw.done?.()
      } else {
        tw.step(tw.ease(k))
        alive = true
      }
    }
    if (!alive) list = []
    else if (list.length > 32) list = list.filter((t) => !t.dead)
    return alive
  }

  /** Lleva todo al final de golpe. Un toque durante una animacion la adelanta. */
  function finishAll() {
    for (const tw of list) {
      if (tw.dead) continue
      tw.step(1); tw.dead = true; tw.done?.()
    }
    list = []
  }

  return { add, sequence, update, finishAll, get pending() { return list.some((t) => !t.dead) } }
}
