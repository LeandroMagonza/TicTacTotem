import * as THREE from 'three'
import { createWorld } from '../scene/world.js'
import { createMaterials, TEAM_NAME } from '../scene/materials.js'
import { createBoard } from '../scene/board.js'
import { createPieceSet } from '../scene/pieces.js'
import { loadModels } from '../scene/models.js'
import { createRig, DEG } from '../scene/camera.js'
import { createHighlights } from '../scene/highlights.js'
import { createTweens, easeOut, easeIn, easeInOut } from '../scene/tween.js'
import { cellToWorld, traySlotToWorld, ELEVATION_LOW, ELEVATION_HIGH } from '../scene/geometry.js'
import { createEngineClient } from './engineClient.js'
import { createInput, createKeyboard } from './input.js'
import { createHud } from './hud.js'
import { DIFFICULTIES } from '../engine/ai.js'

const WHITE_SET = '12344'
const BLACK_SET = '12355'
/**
 * Nadie coloca desde la mano en el centro; al centro solo se llega moviendo.
 * Con esta regla y este set, B tiene la victoria forzada a 14 jugadas, el
 * primero no pasa de 54 % con su mejor apertura, y el segundo tiene siempre
 * varias respuestas que aguantan (Simulacion/aperturas/color/).
 */
const RULES = Object.freeze({ sinCentro: true })

/** @param {HTMLCanvasElement} canvas @param {HTMLElement} uiRoot @param {(m:string)=>void} onFatal */
export async function startGame(canvas, uiRoot, onFatal) {
  const world = createWorld(canvas)
  const mats = createMaterials()
  const board = createBoard(mats)
  const highlights = createHighlights(mats)
  const rig = createRig(world.camera)
  const tweens = createTweens()

  world.scene.add(rig.pivot, board.group, highlights.group)
  world.steppers.push((now) => tweens.update(now))
  // El numeral de la cara de cada moneda sigue al azimut de la camara, para
  // leerse derecho tras un cuarto de vuelta, el giro de hotseat o la orbita de
  // celebracion. Un stepper y no un hook en cada sitio que toca el azimut: son
  // cinco sitios hoy y seran mas.
  world.steppers.push(() => (pieceSet?.setFacing(rig.state.azimuth) ?? false))

  const state = {
    phase: /** @type {'menu'|'playing'|'thinking'|'animating'|'over'} */ ('playing'),
    mode: /** @type {'ai'|'hotseat'|'auto'} */ ('ai'),
    difficulty: /** @type {keyof typeof DIFFICULTIES} */ ('dificil'),
    humanSide: 0,
    /** Bot contra bot: play/pausa y segundos (entero) entre jugadas. `due` es
     *  cuando vence la pausa, para mostrar la cuenta regresiva. */
    auto: { playing: false, seconds: 2, timer: 0, ticker: 0, due: 0 },
    /**
     * Historial completo de la partida, una entrada por ply, con la evaluacion
     * del bot cuando la jugada fue suya. Es lo que permite discutir "por que hizo
     * eso" mirando la pantalla y no adivinando.
     * @type {{ply:number, owner:number, text:string, moveId:number, ai:null|{value:number, plies:number}}[]}
     */
    record: [],
    snapshot: null,
    pieces: {},
    selection: /** @type {null|{key:string, moves:any[]}} */ (null),
    layout: /** @type {'portrait'|'landscape'} */ ('portrait'),
    exploded: /** @type {number|null} */ (null),
  }

  let pieceSet = null
  const engine = createEngineClient(onFatal)

  // Los modelos se cargan una sola vez. Si no hay ninguno, el juego arranca
  // igual con las piezas procedurales — la degradacion es por nivel.
  const { models, avisos } = await loadModels()
  for (const a of avisos) console.warn('[modelos]', a)

  // --- layout ------------------------------------------------------------
  function pickLayout() {
    const w = window.visualViewport?.width ?? window.innerWidth
    const h = window.visualViewport?.height ?? window.innerHeight
    return w >= h ? 'landscape' : 'portrait'
  }

  function applyLayout(force = false) {
    const next = pickLayout()
    if (!force && next === state.layout) return false
    state.layout = next
    rig.setLayout(next)
    board.applyLayout(next)
    return true
  }

  world.setResizeHandler((w, h) => {
    applyLayout()
    rig.fit(w / h)
    if (state.snapshot) syncInstant()
  })

  // --- sincronizacion escena <- snapshot ---------------------------------
  //
  // La regla que hace facil todo lo demas: la escena se reconstruye desde
  // cualquier snapshot sin animar. Undo, restart, cambio de bando y "algo se
  // desincronizo" colapsan a este unico camino.
  function syncInstant() {
    pieceSet.applyInstant(state.snapshot, state.layout)
    state.exploded = null
    // Las cajas de picking de cada celda se ajustan a la altura REAL de su pila.
    // Es lo que hace que tocar la fila del fondo con la camara baja no termine
    // jugando en la fila de adelante.
    board.setCellHeights((cell) => pieceSet.landingY(state.snapshot, cell))
    board.setHandHeights((side, slot) => {
      const id = (state.snapshot.hands[side] ?? [])[slot]
      return id == null ? 0 : pieceSet.byId.get(id).userData.thickness
    })
    // Los pickers de mano se reasignan en cada sync: los slots se reordenan a
    // medida que se gastan las piezas, asi que el slot k no es siempre la misma
    // pieza.
    for (const side of [0, 1]) {
      const hand = state.snapshot.hands[side] ?? []
      board.handPickers[side].forEach((p, k) => {
        p.userData.pieceId = hand[k] ?? -1
        const { x, z } = traySlotToWorld(side, k, state.layout)
        p.position.x = x
        p.position.z = z
      })
    }
    refreshHighlights()
    world.invalidate()
  }

  const landingY = (cell) => pieceSet.landingY(state.snapshot, cell)

  function sourceKey(m) {
    return m.from === 'hand' ? `hand:${m.pieceId}` : `cell:${m.from}`
  }

  function refreshHighlights() {
    const s = state.snapshot
    if (!s) return
    if (state.selection) {
      const anchor = state.selection.moves[0]
      const obj = pieceSet.byId.get(anchor.pieceId)
      highlights.showSelection(obj.position)
      highlights.showDestinations(state.selection.moves, landingY)
    } else {
      highlights.showSelection(null)
      highlights.showDestinations([], landingY)
    }
    highlights.showLastMove(s.lastMove?.from ?? null, s.lastMove?.to ?? null, landingY)
    highlights.showWinningLine(s.result?.lines?.[0] ?? null)
    world.invalidate()
  }

  // --- animacion de jugada ------------------------------------------------
  function animateMove(mv, prevSnapshot) {
    const obj = pieceSet.byId.get(mv.pieceId)
    const dest = cellToWorld(mv.to)
    // La altura de destino sale del snapshot ANTERIOR: es la pila sobre la que
    // se apoya, sin contar la pieza que esta llegando.
    let destY = 0
    for (const id of prevSnapshot.stacks[mv.to]) {
      if (id !== mv.pieceId) destY += pieceSet.byId.get(id).userData.thickness
    }
    const from = obj.position.clone()
    const peak = Math.max(from.y, destY) + 0.6

    const lift = from.clone(); lift.y = peak
    const over = new THREE.Vector3(dest.x, peak, dest.z)
    const land = new THREE.Vector3(dest.x, destY, dest.z)
    const step = (a, b) => (k) => { obj.position.lerpVectors(a, b, k); world.invalidate() }

    return tweens.sequence([
      { dur: 140, ease: easeOut, step: step(from, lift) },
      { dur: mv.from === 'hand' ? 320 : 240, ease: easeInOut, step: step(lift, over) },
      { dur: 160, ease: easeIn, step: step(over, land) },
      {
        dur: 60,
        step: (k) => { obj.scale.y = 1 - 0.06 * Math.sin(k * Math.PI); world.invalidate() },
        done: () => { obj.scale.y = 1 },
      },
    ])
  }

  /**
   * La pieza recien destapada pulsa. Destapar es COMO SE PIERDE en este juego
   * (GameManager.cs:206-211); si el juego no lleva el ojo ahi, la derrota se
   * vive como aleatoria.
   */
  function pulseUncovered(prevSnapshot, mv) {
    if (mv.from === 'hand') return
    const stack = prevSnapshot.stacks[mv.from]
    const below = stack[stack.length - 2]
    if (below == null) return
    const obj = pieceSet.byId.get(below)
    // La moneda lleva un material por cara (canto, arriba, abajo), asi que se
    // clonan todos: los compartidos no se pueden tocar sin encender a las demas
    // piezas del mismo nivel y dueño.
    const meshes = []
    obj.traverse((o) => { if (o.isMesh) meshes.push(o) })
    const originales = meshes.map((m) => m.material)
    const clones = originales.map((m) => Array.isArray(m) ? m.map((x) => x.clone()) : m.clone())
    meshes.forEach((m, i) => { m.material = clones[i] })
    const emisivos = clones.flat().filter((m) => m.emissive)
    tweens.add({
      dur: 250,
      step: (k) => {
        const a = Math.sin(k * Math.PI)
        for (const m of emisivos) m.emissive.setRGB(a * 0.5, a * 0.45, a * 0.25)
        world.invalidate()
      },
      done: () => {
        meshes.forEach((m, i) => { m.material = originales[i] })
        clones.flat().forEach((m) => m.dispose())
        world.invalidate()
      },
    })
  }

  // --- explotar una pila --------------------------------------------------
  function explode(cell) {
    const stack = state.snapshot.stacks[cell]
    if (stack.length < 2) return
    state.exploded = cell
    const { x, z } = cellToWorld(cell)
    stack.forEach((id, i) => {
      const obj = pieceSet.byId.get(id)
      const y0 = obj.position.y
      const y1 = i * 0.5
      tweens.add({
        dur: 250,
        step: (k) => { obj.position.set(x, y0 + (y1 - y0) * k, z); world.invalidate() },
      })
    })
  }

  function collapse() {
    if (state.exploded == null) return
    syncInstant()
  }

  // --- flujo de juego -----------------------------------------------------
  function clearSelection() {
    state.selection = null
    refreshHighlights()
    hud.render(state)
  }

  async function commit(moveId) {
    const prev = state.snapshot
    const mv = prev.legalMoves.find((m) => m.id === moveId)
    state.selection = null
    state.phase = 'animating'
    highlights.showSelection(null)
    highlights.showDestinations([], landingY)

    const { snapshot } = await engine.applyMove(moveId)
    pulseUncovered(prev, mv)
    await animateMove(mv, prev)
    state.snapshot = snapshot
    recordMove(prev.turn, moveId, snapshot, null)
    syncInstant()
    hud.render(state)

    if (snapshot.result) return finish()
    state.phase = 'playing'
    if (state.mode === 'ai' && snapshot.turn !== state.humanSide) await aiTurn()
    else if (state.mode === 'auto') scheduleAuto()
  }

  /** Contra la maquina: pensar y jugar seguido, con el piso de "Pensando…". */
  async function aiTurn() {
    state.phase = 'thinking'
    hud.render(state)
    const res = await engine.aiPick({ difficulty: state.difficulty })
    await mostrarJugadaDeLaIA(res)
  }

  /**
   * Juega en pantalla una decision ya tomada. La jugada se aplica al motor recien
   * aca: pensar no mueve nada, asi que una decision que quedo vieja (pausa, undo,
   * nueva partida) se tira sin dejar al motor adelantado.
   */
  async function mostrarJugadaDeLaIA(res) {
    const prev = state.snapshot
    if (!res.move) { state.snapshot = res.snapshot ?? prev; return finish() }

    const mv = prev.legalMoves.find((m) => m.id === res.move)
    // Pre-resaltar origen y destino ANTES de que la pieza se mueva. Es el truco
    // de legibilidad que importa: te dice donde mirar antes de que arranque el
    // movimiento, asi ves la jugada en vez de enterarte despues.
    highlights.showLastMove(mv.from, mv.to, landingY)
    world.invalidate()
    // Repintar aca saca el "Pensando…" en cuanto hay decision: en bot contra bot
    // con pausa 0 el cartel quedaba puesto toda la jugada.
    hud.render(state)
    await new Promise((r) => setTimeout(r, 350))

    state.phase = 'animating'
    const { snapshot } = await engine.applyMove(res.move)
    pulseUncovered(prev, mv)
    await animateMove(mv, prev)
    state.snapshot = snapshot
    recordMove(prev.turn, res.move, snapshot, { value: res.value, plies: res.plies })
    syncInstant()
    state.phase = snapshot.result ? 'over' : 'playing'
    hud.render(state)
    if (snapshot.result) return finish()
    if (state.mode === 'auto') scheduleAuto()
  }

  /** Una entrada de historial por ply; la evaluacion solo cuando jugo el bot. */
  function recordMove(owner, moveId, snapshot, ai) {
    state.record.length = Math.max(0, snapshot.ply - 1)
    state.record.push({ ply: snapshot.ply, owner, text: snapshot.lastMove?.text ?? '', moveId, ai })
  }

  // --- bot contra bot -------------------------------------------------------
  //
  // El bot juega los dos bandos con la misma dificultad. Los segundos entre
  // jugadas son el RITMO de la partida, no un recargo sobre lo que tarda en
  // pensar: la cuenta regresiva y la busqueda arrancan juntas, y al llegar a cero
  // la jugada sale ya pensada. "Pensando…" solo aparece si la busqueda tardo mas
  // que la pausa. Se para solo cuando la partida termina.
  //
  // `autoSeq` es el numero de ciclo: pausar, deshacer o empezar otra partida lo
  // incrementan, y una decision que vuelve con un numero viejo se descarta.
  let autoSeq = 0

  function stopAutoTimers() {
    autoSeq++
    clearTimeout(state.auto.timer)
    clearInterval(state.auto.ticker)
    state.auto.timer = 0
    state.auto.ticker = 0
    state.auto.due = 0
  }

  function scheduleAuto() {
    stopAutoTimers()
    if (state.mode !== 'auto' || !state.auto.playing || state.phase !== 'playing') return
    if (state.snapshot?.result) { state.auto.playing = false; hud.render(state); return }
    const ciclo = autoSeq
    const ms = Math.max(0, state.auto.seconds) * 1000

    // Pensar ya, en paralelo con la espera. Sin piso artificial: el piso es la pausa.
    let listo = false
    const pensando = engine.aiPick({ difficulty: state.difficulty, minThinkMs: 0 })
      .then((r) => { listo = true; return r })

    state.auto.due = Date.now() + ms
    // La cuenta regresiva se redibuja sola; el HUD lee `auto.due`.
    if (ms > 0) state.auto.ticker = setInterval(() => hud.render(state), 250)
    state.auto.timer = setTimeout(async () => {
      clearInterval(state.auto.ticker)
      state.auto.ticker = 0
      state.auto.timer = 0
      state.auto.due = 0
      if (!listo) { state.phase = 'thinking'; hud.render(state) }
      const res = await pensando
      if (ciclo !== autoSeq || state.mode !== 'auto' || !state.auto.playing) return
      state.phase = 'playing'
      await mostrarJugadaDeLaIA(res)
    }, ms)
    hud.render(state)
  }

  function playPause() {
    if (state.mode !== 'auto' || state.snapshot?.result) return
    state.auto.playing = !state.auto.playing
    if (state.auto.playing) scheduleAuto()
    else stopAutoTimers()
    hud.render(state)
  }

  // --- historial --------------------------------------------------------------
  function linkDePartida() {
    const ids = state.record.map((r) => r.moveId).join(',')
    return `${location.origin}${location.pathname}?jugadas=${ids}`
  }

  function openHistory() {
    hud.showHistory(state, { link: linkDePartida })
  }

  function setSeconds(n) {
    const v = Math.floor(Number(n))
    state.auto.seconds = Number.isFinite(v) ? Math.min(60, Math.max(0, v)) : 2
    hud.render(state)
    if (state.auto.playing) scheduleAuto()
  }

  /** Azimut de antes de la orbita de celebracion, para poder volver. */
  let azimuthAntesDeCelebrar = null

  /**
   * Devuelve la camara a donde estaba antes de la celebracion, por el camino
   * corto. Sin esto la revancha arranca con el tablero girado a cualquier lado,
   * que era el estado en que la orbita quedo cuando apretaste el boton.
   */
  function devolverCamara() {
    if (azimuthAntesDeCelebrar == null) return
    const destino = azimuthAntesDeCelebrar
    azimuthAntesDeCelebrar = null
    const desde = rig.state.azimuth
    // Camino corto: normalizar la diferencia a (-PI, PI].
    let d = (destino - desde) % (Math.PI * 2)
    if (d > Math.PI) d -= Math.PI * 2
    if (d < -Math.PI) d += Math.PI * 2
    if (Math.abs(d) < 1e-3) { rig.state.azimuth = destino; rig.fit(world.camera.aspect); return }
    tweens.add({
      dur: 600,
      step: (k) => {
        rig.state.azimuth = desde + d * k
        rig.fit(world.camera.aspect)
        world.invalidate()
      },
    })
  }

  function finish() {
    state.phase = 'over'
    stopAutoTimers()
    state.auto.playing = false
    refreshHighlights()
    hud.render(state)
    hud.showResult(state.snapshot.result, () => newGame({}), () => newGame({ swapSide: true }), openMenu)
    // Orbita lenta de celebracion: deja leer el arreglo final de totems.
    // Se reencuadra en cada paso porque la caja de contenido NO es simetrica a
    // la rotacion (2,35 x 1,75): girando 90 grados cambia lo que entra, y sin
    // reencuadrar las bandejas se salen de pantalla.
    const t0 = performance.now()
    azimuthAntesDeCelebrar = rig.state.azimuth
    const az0 = azimuthAntesDeCelebrar
    world.steppers.push((now) => {
      if (state.phase !== 'over') return false
      rig.state.azimuth = az0 + ((now - t0) / 24000) * Math.PI * 2
      rig.fit(world.camera.aspect)
      world.invalidate()
      return true
    })
  }

  // --- toques -------------------------------------------------------------
  function onTap(hit) {
    if (tweens.pending) { tweens.finishAll(); return }
    if (state.phase !== 'playing') return
    if (state.exploded != null) { collapse(); return }
    if (!hit) { clearSelection(); return }

    const s = state.snapshot
    if (state.mode === 'ai' && s.turn !== state.humanSide) return
    // En bot contra bot el tablero se mira, no se toca: solo explotar pilas.
    if (state.mode === 'auto') {
      if (hit.kind === 'cell' && s.stacks[hit.index].length >= 2) explode(hit.index)
      return
    }

    // Destino de una jugada seleccionada
    if (state.selection && hit.kind === 'cell') {
      const mv = state.selection.moves.find((m) => m.to === hit.index)
      if (mv) { commit(mv.id); return }
    }

    const key = hit.kind === 'hand' ? `hand:${hit.pieceId}` : `cell:${hit.index}`

    // Volver a tocar la fuente ya seleccionada: explota la pila si es del tablero
    if (state.selection?.key === key) {
      if (hit.kind === 'cell') explode(hit.index)
      else clearSelection()
      return
    }

    const moves = s.legalMoves.filter((m) => sourceKey(m) === key)
    if (moves.length === 0) {
      // Si no se puede mover pero hay algo apilado, se explota igual para poder
      // MIRAR. Atarlo a que la pila sea seleccionable dejaba afuera justo el caso
      // en que mas queres saber que hay debajo: la pila del rival.
      clearSelection()
      if (hit.kind === 'cell' && s.stacks[hit.index].length >= 2) explode(hit.index)
      // Ni modal ni cartel: un temblor de 120 ms sobre el objeto y listo.
      else shake(hit)
      return
    }
    state.selection = { key, moves }
    refreshHighlights()
    hud.render(state)
  }

  function shake(hit) {
    const s = state.snapshot
    let obj = null
    if (hit.kind === 'hand') obj = pieceSet.byId.get(hit.pieceId)
    else {
      const stack = s.stacks[hit.index]
      if (stack.length) obj = pieceSet.byId.get(stack[stack.length - 1])
    }
    if (!obj) return
    const x0 = obj.position.x
    tweens.add({
      dur: 120,
      step: (k) => { obj.position.x = x0 + Math.sin(k * Math.PI * 3) * 0.05; world.invalidate() },
      done: () => { obj.position.x = x0; world.invalidate() },
    })
  }

  // --- undo ---------------------------------------------------------------
  async function undo() {
    if (state.phase === 'thinking' || state.phase === 'animating') return
    // Deshacer en bot contra bot pausa: si no, el bot vuelve a jugar lo mismo.
    stopAutoTimers()
    state.auto.playing = false
    // En modo IA se deshacen dos: la de la IA y la tuya.
    const plies = state.mode === 'ai' ? 2 : 1
    const { snapshot } = await engine.undo(plies)
    state.snapshot = snapshot
    state.record.length = Math.max(0, snapshot.ply)
    state.selection = null
    state.phase = 'playing'
    hud.hideResult()
    syncInstant()
    hud.render(state)
  }

  // --- partidas -----------------------------------------------------------
  async function newGame({ swapSide = false } = {}) {
    if (swapSide) state.humanSide = 1 - state.humanSide
    hud.hideResult()
    hud.showMenu(false)
    state.selection = null
    state.phase = 'playing'      // corta la orbita de celebracion
    stopAutoTimers()
    state.record = []
    devolverCamara()

    const res = await engine.newGame({
      white: WHITE_SET,
      black: BLACK_SET,
      rules: RULES,
      seed: (Math.random() * 2 ** 31) >>> 0,
    })
    state.pieces = res.pieces
    state.snapshot = res.snapshot

    if (pieceSet) world.scene.remove(pieceSet.group)
    // Se rearman arrays indexados por id de pieza en vez de confiar en el orden
    // de Object.values: el motor manda una tabla, no una lista.
    const ids = Object.keys(res.pieces).map(Number).sort((a, b) => a - b)
    pieceSet = createPieceSet({
      pieceCount: ids.length,
      owner: Int8Array.from(ids, (i) => res.pieces[i].owner),
      rank: Int8Array.from(ids, (i) => res.pieces[i].rank),
    }, mats, models)
    world.scene.add(pieceSet.group)

    applyLayout(true)
    rig.fit(canvas.clientWidth / Math.max(1, canvas.clientHeight))
    syncInstant()
    hud.render(state)

    if (state.mode === 'ai' && state.snapshot.turn !== state.humanSide) await aiTurn()
    else if (state.mode === 'auto') scheduleAuto()
  }

  /**
   * Salta a una posicion arbitraria aplicando jugadas sin animar.
   *
   * Es la unica forma de llegar a proposito a posiciones que jugando no se
   * alcanzan nunca: un ahogado ocurre 2 veces en 1,4 millones de nodos, y 0
   * veces en los 5,5 millones de estados alcanzables en 8 plies. Tambien sirve
   * para reproducir cualquier bug desde una URL.
   *
   * ?jugadas=12,45,...   ids de jugada exactos
   * ?demo=6              6 jugadas legales al azar, con semilla fija
   */
  async function jumpTo({ moves, demo, seed = 7 }) {
    const wasMode = state.mode
    state.mode = 'hotseat'          // que no conteste la IA mientras se arma
    let rnd = seed >>> 0
    const nextRnd = (n) => { rnd = (rnd * 1103515245 + 12345) & 0x7fffffff; return rnd % n }

    const ids = moves ?? []
    const count = demo ?? ids.length
    for (let i = 0; i < count; i++) {
      // Sin las gemelas de la mano, para que ?demo=N con la misma semilla siga
      // llegando a la misma posicion de siempre.
      const legal = state.snapshot.legalMoves.filter((m) => m.twinOf == null)
      if (legal.length === 0 || state.snapshot.result) break
      const id = moves ? ids[i] : legal[nextRnd(legal.length)].id
      const prevTurn = state.snapshot.turn
      const { snapshot } = await engine.applyMove(id)
      state.snapshot = snapshot
      recordMove(prevTurn, id, snapshot, null)
    }
    state.mode = wasMode
    syncInstant()
    hud.render(state)
    // Si la posicion cargada ya termino, pasar por el MISMO cierre que una
    // partida jugada. Si no, el camino de debug no ejercitaria la pantalla final,
    // que es justo lo que se quiere revisar cargando una posicion a mano.
    if (state.snapshot.result) finish()
  }

  // --- menu ---------------------------------------------------------------
  function openMenu() { buildMenu(); hud.showMenu(true) }

  function buildMenu() {
    const { el, btn } = hud
    const root = hud.menuRoot
    root.innerHTML = ''
    root.append(el('h2', '', 'TicTacTotem'))

    const group = (title) => { root.append(el('h3', '', title)); const d = el('div', 'options'); root.append(d); return d }

    const modes = group('Modo')
    for (const [id, label] of [['ai', 'Contra la máquina'], ['hotseat', 'Dos jugadores'], ['auto', 'Bot contra bot']]) {
      const b = btn(label, label, () => { state.mode = /** @type {any} */ (id); buildMenu() },
        state.mode === id ? 'sel' : 'ghost')
      modes.append(b)
    }

    if (state.mode === 'ai' || state.mode === 'auto') {
      const dif = group(state.mode === 'auto' ? 'Dificultad de los dos bots' : 'Dificultad')
      for (const [id, cfg] of Object.entries(DIFFICULTIES)) {
        const b = btn(cfg.label, `Calcula ${cfg.vision} jugadas hacia adelante`,
          () => { state.difficulty = /** @type {any} */ (id); buildMenu() },
          state.difficulty === id ? 'sel' : 'ghost')
        dif.append(b)
      }
    }

    if (state.mode === 'auto') {
      root.append(el('p', 'note',
        'El bot juega los dos bandos. Apretá ▶ en la barra de abajo; el número son los ' +
        'segundos entre jugadas (0 = seguido). Tocar una pila la abre para mirar.'))
    }

    if (state.mode === 'ai') {
      const bando = group('Tu bando')
      for (const side of [0, 1]) {
        const set = side === 0 ? WHITE_SET : BLACK_SET
        const b = btn(`${TEAM_NAME[side]} · ${[...set].join(' ')}`,
          side === 0 ? 'Arranca' : 'Juega segundo',
          () => { state.humanSide = side; buildMenu() },
          state.humanSide === side ? 'sel' : 'ghost')
        bando.append(b)
      }

      // Decir la verdad sobre el desbalance, en vez de dejar que alguien
      // concluya que el juego esta roto.
      const nota = el('p', 'note',
        'B tiene victoria forzada con juego perfecto, pero está a 14 jugadas y nadie la ve. ' +
        'A profundidad humana los bandos están parejos (50 / 50 con apertura al azar; ' +
        'A llega a 54 con su mejor apertura). A se lleva el tempo; B se lleva la teoría.')
      root.append(nota)
    }

    root.append(el('p', 'note',
      'Regla del centro: nadie coloca una pieza de la mano en el centro. Al centro sólo se llega ' +
      'moviendo una pieza que ya está en el tablero.'))

    const acciones = el('div', 'panel-btns')
    acciones.append(
      btn('Nueva partida', 'Empezar', () => newGame({})),
      btn('Cerrar', 'Volver al tablero', () => hud.showMenu(false), 'ghost'),
    )
    root.append(acciones)
  }

  // --- cableado -----------------------------------------------------------
  const hud = createHud(uiRoot, {
    undo,
    cancel: clearSelection,
    playPause,
    setSeconds,
    openHistory,
    rotate: (q) => { rig.rotateQuarters(q); world.invalidate() },
    tilt: () => {
      const target = rig.toggleTilt()
      const from = rig.state.elevation / DEG
      tweens.add({
        dur: 400,
        step: (k) => { rig.setElevationDeg(from + (target - from) * k); world.invalidate() },
      })
    },
    openMenu,
  })

  const input = createInput({
    canvas,
    camera: world.camera,
    pickables: () => board.pickables,
    onTap,
    onDrag: (dx, dy) => {
      rig.orbit(-dx * 0.006, -dy * 0.004)
      world.invalidate()
    },
  })

  createKeyboard({
    onUndo: undo,
    onCancel: clearSelection,
    onRotate: (q) => { rig.rotateQuarters(q); world.invalidate() },
    onTilt: () => { rig.setElevationDeg(rig.toggleTilt()); world.invalidate() },
    onCell: (cell) => onTap({ kind: 'cell', index: cell }),
    onHandSlot: (slot) => {
      const hand = state.snapshot?.hands[state.snapshot.turn] ?? []
      if (hand[slot] != null) onTap({ kind: 'hand', pieceId: hand[slot] })
    },
  })

  world.resize()
  await newGame({})

  const params = new URLSearchParams(location.search)
  if (params.has('pos')) {
    // ?pos=<entero>&turno=0|1 carga una posicion cruda. Es como se prueba el
    // ahogado, que jugando no se alcanza nunca.
    const { snapshot } = await engine.loadPosition(
      Number(params.get('pos')), Number(params.get('turno') ?? 0))
    state.snapshot = snapshot
    state.record = []
    state.mode = 'hotseat'
    syncInstant()
    hud.render(state)
    if (snapshot.result) finish()
  }
  if (params.has('demo') || params.has('jugadas')) {
    await jumpTo({
      demo: params.has('demo') ? Number(params.get('demo')) : undefined,
      moves: params.has('jugadas') ? params.get('jugadas').split(',').map(Number) : undefined,
      seed: Number(params.get('seed') ?? 7),
    })
  }
  if (params.has('elev')) { rig.setElevationDeg(Number(params.get('elev'))); world.invalidate() }

  // `tap` va expuesto para que un test headless pueda jugar una partida entera
  // de forma determinista y sin depender de pixeles.
  return {
    state, newGame, openMenu, engine, jumpTo, rig, tweens, tap: onTap, undo, highlights,
    playPause, setSeconds, openHistory, linkDePartida, RULES,
    // Para que un test headless pueda proyectar una celda a coordenadas de
    // pantalla y disparar un click DE VERDAD, ejercitando el raycast completo.
    camera: world.camera, canvas, board, input,
  }
}

export { ELEVATION_LOW, ELEVATION_HIGH }
