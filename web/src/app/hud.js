import { TEAM_COLOR, TEAM_NAME } from '../scene/materials.js'
import { DIFFICULTIES } from '../engine/ai.js'

const el = (tag, cls, text) => {
  const n = document.createElement(tag)
  if (cls) n.className = cls
  if (text != null) n.textContent = text
  return n
}

/**
 * Overlay DOM sobre el canvas.
 *
 * DOM y no texto 3D en escena: da texto nitido a cualquier devicePixelRatio
 * gratis, mas lectores de pantalla, foco de teclado y navegacion, cuesta cero
 * draw calls, y el layout responsive son cuatro lineas de CSS en vez de una
 * pasada de layout a mano. El texto en escena solo se gana su costo cuando tiene
 * que quedar ocluido por geometria o anclado al mundo; aca nada lo necesita.
 *
 * El unico "texto 3D" que queda es el numeral en el borde del collar, que es una
 * textura de canvas de 512x64, no un motor de fuentes.
 */
export function createHud(root, actions) {
  root.innerHTML = ''

  // --- barra superior ---
  const top = el('div', 'bar bar-top')
  const turnDot = el('span', 'dot')
  const turnText = el('span', 'turn-text', 'Cargando…')
  const turnBox = el('div', 'turn')
  turnBox.append(turnDot, turnText)

  const hands = [el('div', 'hand'), el('div', 'hand')]
  top.append(hands[0], turnBox, hands[1])

  // --- barra inferior ---
  const bottom = el('div', 'bar bar-bottom')
  const btn = (label, title, fn, cls = '') => {
    const b = el('button', `btn ${cls}`, label)
    b.title = title
    b.setAttribute('aria-label', title)
    b.onclick = fn
    return b
  }
  const undoBtn = btn('Deshacer', 'Deshacer la última jugada (U)', actions.undo)
  const cancelBtn = btn('Cancelar', 'Cancelar la selección (Esc)', actions.cancel, 'ghost')
  const histBtn = btn('Historial', 'Todas las jugadas, con la evaluación del bot', actions.openHistory, 'ghost')
  const rotL = btn('↺', 'Girar la vista un cuarto (R)', () => actions.rotate(-1), 'icon')
  const rotR = btn('↻', 'Girar la vista un cuarto', () => actions.rotate(1), 'icon')
  const tiltBtn = btn('⌂', 'Cambiar el ángulo de cámara (T)', actions.tilt, 'icon')
  const menuBtn = btn('☰', 'Menú', actions.openMenu, 'icon')

  // --- bot contra bot: play/pausa y segundos entre jugadas (entero) ---
  const playBtn = btn('▶', 'Reproducir o pausar el bot contra bot', actions.playPause, 'icon')
  const secs = /** @type {HTMLInputElement} */ (el('input', 'num'))
  secs.type = 'number'
  secs.min = '0'; secs.max = '60'; secs.step = '1'; secs.value = '2'
  secs.inputMode = 'numeric'
  secs.title = 'Segundos entre jugadas'
  secs.setAttribute('aria-label', 'Segundos entre jugadas')
  secs.onchange = () => actions.setSeconds(secs.value)
  const autoBox = el('span', 'auto')
  autoBox.append(playBtn, secs, el('span', 'auto-label', 's'))
  autoBox.style.display = 'none'

  bottom.append(undoBtn, cancelBtn, histBtn, autoBox, el('span', 'spacer'), rotL, rotR, tiltBtn, menuBtn)

  // --- log de jugadas ---
  const log = el('div', 'log')

  // --- panel de resultado ---
  const overlay = el('div', 'overlay hidden')
  const panel = el('div', 'panel')
  const panelTitle = el('h2', '', '')
  const panelWhy = el('p', 'why', '')
  const panelExplain = el('details')
  panelExplain.append(el('summary', '', '¿por qué?'), el('p', 'rule', ''))
  const panelBtns = el('div', 'panel-btns')
  panel.append(panelTitle, panelWhy, panelExplain, panelBtns)
  overlay.append(panel)

  root.append(top, log, bottom, overlay)

  // --- menu ---
  const menu = el('div', 'overlay modal hidden')
  const menuPanel = el('div', 'panel wide')
  menu.append(menuPanel)
  root.append(menu)

  // --- historial ---
  const hist = el('div', 'overlay modal hidden')
  const histPanel = el('div', 'panel wide')
  hist.append(histPanel)
  root.append(hist)

  /**
   * Como se lee la evaluacion del bot: "gana en 3" es que vio una victoria forzada
   * a 3 plies contando su propia jugada; 0 es que la jugada termina la partida.
   */
  const evalText = (ai) => {
    if (!ai) return ''
    if (ai.value === 1) return ai.plies === 0 ? 'gana ya' : `gana en ${ai.plies}`
    if (ai.value === -1) return ai.plies === 0 ? 'pierde ya' : `pierde en ${ai.plies}`
    return 'sin definir a la vista'
  }

  // El historial abierto sigue a la partida. Mirando al bot contra bot el panel
  // se abre CON la partida en curso; si se pintara una sola vez, se quedaria
  // clavado en las jugadas que habia al abrirlo y contradiria al log de arriba.
  let histVivo = null

  /** Repinta la lista y el link del panel abierto, si esta abierto. */
  function pintarHistorial() {
    if (!histVivo || hist.classList.contains('hidden')) return
    const { state, link, lista, linkBox } = histVivo
    lista.innerHTML = ''
    if (state.record.length === 0) lista.append(el('p', 'note', 'Todavía no hay jugadas.'))
    for (const r of state.record) {
      const line = el('div', 'hist-line')
      line.style.borderLeftColor = TEAM_COLOR[r.owner]
      line.append(el('span', 'hist-ply', `${r.ply}.`), el('span', '', `${TEAM_NAME[r.owner]}: ${r.text}`))
      if (r.ai) line.append(el('span', 'hist-eval', evalText(r.ai)))
      lista.append(line)
    }
    const url = link()
    if (linkBox.value !== url && document.activeElement !== linkBox) linkBox.value = url
    lista.scrollTop = lista.scrollHeight
  }

  /** @param {object} state @param {{link: () => string}} opts */
  function showHistory(state, { link }) {
    histPanel.innerHTML = ''
    histPanel.append(el('h2', '', 'Historial'))
    const lista = el('div', 'hist')
    histPanel.append(lista)
    histPanel.append(el('p', 'note',
      'La evaluación es lo que el bot vio al jugar, en plies (medio movimiento cada uno), ' +
      'contando su propia jugada.'))

    // El link reproduce la partida exacta desde la URL (?jugadas=...). Va en un
    // campo de texto ademas del boton porque el portapapeles no siempre esta
    // disponible (http sin https, permisos).
    const linkBox = /** @type {HTMLInputElement} */ (el('input', 'link'))
    linkBox.readOnly = true
    linkBox.onclick = () => linkBox.select()
    histPanel.append(el('h3', '', 'Link de esta partida'), linkBox)

    const acciones = el('div', 'panel-btns')
    const copiar = btn('Copiar link', 'Copiar el link al portapapeles', async () => {
      try { await navigator.clipboard.writeText(linkBox.value); copiar.textContent = 'Copiado' }
      catch { linkBox.select(); copiar.textContent = 'Seleccionalo y copialo' }
    })
    acciones.append(copiar, btn('Cerrar', 'Volver al tablero', () => {
      hist.classList.add('hidden')
      histVivo = null
    }, 'ghost'))
    histPanel.append(acciones)

    histVivo = { state, link, lista, linkBox }
    hist.classList.remove('hidden')
    pintarHistorial()
  }

  const RAZON = {
    line: (w) => `${TEAM_NAME[w]} hizo tres en línea.`,
    uncovered: (w) => `${TEAM_NAME[1 - w]} se movió y destapó la línea de ${TEAM_NAME[w]}.`,
    double: (w) => `Quedaron las dos líneas a la vez, y pierde el que acaba de mover.`,
    stalemate: (w) => `${TEAM_NAME[1 - w]} se quedó sin jugadas legales.`,
    repetition: () => 'Se repitió la misma posición dos veces.',
    cap: () => 'Se llegó al tope de jugadas.',
  }
  const REGLA = {
    line: 'Gana quien deje tres piezas destapadas propias en línea.',
    uncovered: 'Si tu jugada deja al descubierto una línea del rival, perdés — aunque la línea no sea tuya. ' +
      'Es la regla del medio punto: el que acaba de mover pierde los empates.',
    double: 'Si una jugada completa líneas para los dos, pierde el que la hizo.',
    stalemate: 'Si te toca mover y no tenés ninguna jugada legal, perdés.',
    repetition: 'Repetir la misma posición con el mismo jugador en turno es tablas.',
    cap: 'Tope de seguridad de jugadas.',
  }

  function showResult(result, onRematch, onSwap, onMenu) {
    if (!result) { overlay.classList.add('hidden'); return }
    const w = result.winner
    panelTitle.textContent = w == null ? 'Tablas' : `Gana ${TEAM_NAME[w]}`
    panelTitle.style.color = w == null ? 'var(--ink)' : TEAM_COLOR[w]
    panelWhy.textContent = (RAZON[result.reason] ?? (() => ''))(w)
    panelExplain.querySelector('.rule').textContent = REGLA[result.reason] ?? ''
    panelBtns.innerHTML = ''
    panelBtns.append(
      btn('Revancha', 'Jugar otra con los mismos bandos', onRematch),
      btn('Cambiar bando', 'Jugar otra cambiando de bando', onSwap, 'ghost'),
      btn('Menú', 'Volver al menú', onMenu, 'ghost'),
    )
    overlay.classList.remove('hidden')
  }

  let lastLoggedPly = 0

  function render(state) {
    const { snapshot: s, phase, mode, humanSide, pieces } = state
    if (!s) return

    // Indicador de turno
    const esHumano = mode === 'hotseat' || s.turn === humanSide
    const auto = state.auto ?? { playing: false, seconds: 2, due: 0 }
    // En bot contra bot la pausa se muestra como cuenta regresiva. El bot piensa
    // DURANTE esa cuenta, asi que al llegar a cero juega; "Pensando…" solo aparece
    // si la busqueda tardo mas que la pausa.
    const faltan = auto.due ? Math.max(0, Math.ceil((auto.due - Date.now()) / 1000)) : 0
    turnDot.style.background = TEAM_COLOR[s.turn]
    turnText.textContent = phase === 'thinking' ? 'Pensando…'
      : s.result ? '—'
      : mode === 'auto' ? (!auto.playing ? `En pausa · ${TEAM_NAME[s.turn]}`
                           : faltan > 0 ? `${TEAM_NAME[s.turn]} juega en ${faltan} s` : `Turno de ${TEAM_NAME[s.turn]}`)
      : mode === 'hotseat' ? `Turno de ${TEAM_NAME[s.turn]}`
      : esHumano ? 'Tu turno' : `Turno de ${TEAM_NAME[s.turn]}`

    // Controles de bot contra bot: solo en ese modo. El input no se pisa mientras
    // se esta escribiendo en el.
    autoBox.style.display = mode === 'auto' ? '' : 'none'
    if (mode === 'auto') {
      playBtn.textContent = auto.playing ? '⏸' : '▶'
      playBtn.disabled = !!s.result
      if (document.activeElement !== secs) secs.value = String(auto.seconds)
    }

    // Fichas de mano: el resumen numerico preciso que el 3D no da.
    for (const side of [0, 1]) {
      hands[side].innerHTML = ''
      const label = el('span', 'hand-label', TEAM_NAME[side].replace('Tótem ', ''))
      label.style.color = TEAM_COLOR[side]
      hands[side].append(label)
      const restantes = (s.hands[side] ?? []).map((id) => pieces[id].rank).sort((a, b) => a - b)
      for (const r of restantes) {
        const chip = el('span', 'chip', String(r))
        chip.style.borderColor = TEAM_COLOR[side]
        hands[side].append(chip)
      }
      if (restantes.length === 0) hands[side].append(el('span', 'chip empty', '—'))
    }

    undoBtn.disabled = !s.canUndo || phase === 'thinking'
    cancelBtn.style.display = state.selection ? '' : 'none'
    pintarHistorial()

    // Solo se agrega al log cuando el ply es NUEVO. render() se llama varias
    // veces por jugada (al seleccionar, al terminar, al redimensionar) y sin
    // esto la misma jugada aparecia repetida.
    if (s.ply === 0) {
      log.innerHTML = ''
      lastLoggedPly = 0
    } else if (s.lastMove && s.ply > lastLoggedPly) {
      lastLoggedPly = s.ply
      const line = el('div', 'log-line', `${TEAM_NAME[s.lastMove.owner]}: ${s.lastMove.text}`)
      line.style.borderLeftColor = TEAM_COLOR[s.lastMove.owner]
      log.prepend(line)
      while (log.childElementCount > 3) log.lastElementChild.remove()
    } else if (s.ply < lastLoggedPly) {
      // Undo: se recorta el log para que no muestre jugadas que ya no pasaron.
      lastLoggedPly = s.ply
      log.innerHTML = ''
    }
  }

  return {
    render,
    showResult,
    showHistory,
    menuRoot: menuPanel,
    showMenu: (v) => menu.classList.toggle('hidden', !v),
    hideResult: () => overlay.classList.add('hidden'),
    el,
    btn,
    DIFFICULTIES,
  }
}
