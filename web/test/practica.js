/**
 * Equivalente en JS de `tateti-solver practica` (Program.cs:329-400).
 *
 * Es la prueba end-to-end de que el juego que se juega es el juego que se
 * analizo: si las tasas de victoria caen dentro del intervalo de confianza de
 * las medidas en C#, el port de reglas + busqueda + eleccion de jugada es fiel
 * de punta a punta.
 *
 * Reproduce el bucle de C# EXACTAMENTE: sin regla de repeticion, sin desempate
 * anti-shuffle, tope de 40 plies. No usa Match a proposito — Match tiene reglas
 * de partida real que C# no tenia.
 *
 * Uso:  node test/practica.js [--games 2000] [--depths 2,4,6] [--swap]
 */
import { makeSpecFromLabels } from '../src/engine/spec.js'
import { WHITE, ONGOING, DRAW } from '../src/engine/constants.js'
import { initialPosition, applyMove } from '../src/engine/position.js'
import { legalMoves, winnerAfterPacked } from '../src/engine/rules.js'
import { Searcher } from '../src/engine/searcher.js'
import { makeRng } from '../src/engine/rng.js'

const arg = (name, def) => {
  const i = process.argv.indexOf(`--${name}`)
  return i >= 0 ? process.argv[i + 1] : def
}
const swap = process.argv.includes('--swap')

const games = Number(arg('games', 2000))
const depths = String(arg('depths', '2,4,6')).split(',').map(Number)
const maxPlies = Number(arg('max-plies', 40))
const white = swap ? '11245' : '12344'
const black = swap ? '12344' : '11245'

const spec = makeSpecFromLabels(white, black)

console.log(`Blancas (mueven primero): ${spec.whiteLabel}   Negras: ${spec.blackLabel}`)
console.log(`${games} partidas por nivel, tope ${maxPlies} plies\n`)
console.log(`${'ve'.padStart(3)} ${'blancas'.padStart(9)} ${'negras'.padStart(9)} ` +
            `${'sin def.'.padStart(9)} ${'turnos'.padStart(7)} ${'ms/jugada'.padStart(10)}`)

for (const vision of depths) {
  let w = 0, b = 0, nada = 0, turnos = 0, jugadas = 0
  const t0 = performance.now()

  for (let g = 0; g < games; g++) {
    const rng = makeRng(20260727 + g * 7919 + vision * 104729)
    const searcher = new Searcher(spec, { ttBits: 18 })
    let p = initialPosition(spec)
    let turn = WHITE
    let ply = 0
    let ganador = null

    while (ply < maxPlies) {
      const moves = legalMoves(spec, p, turn)
      const n = moves.length
      if (n === 0) { ganador = turn === WHITE ? -1 : 1; break }   // ahogado

      const mio = turn === WHITE ? 1 : -1
      const valores = new Int8Array(n)
      for (let i = 0; i < n; i++) {
        const c = applyMove(p, moves[i])
        const term = winnerAfterPacked(spec, c, turn)
        valores[i] = (term !== ONGOING ? term : searcher.solveFrom(c, 1 - turn, vision - 1).result) * mio
      }
      let mejor = -2
      for (let i = 0; i < n; i++) if (valores[i] > mejor) mejor = valores[i]
      const opciones = []
      for (let i = 0; i < n; i++) if (valores[i] === mejor) opciones.push(i)

      p = applyMove(p, moves[rng.pick(opciones)])
      ply++
      jugadas++
      const fin = winnerAfterPacked(spec, p, turn)
      if (fin !== ONGOING) { ganador = fin; break }
      turn = 1 - turn
    }

    if (ganador === 1) w++
    else if (ganador === -1) b++
    else nada++
    turnos += ply
  }

  const ms = performance.now() - t0
  const pct = (x) => ((100 * x) / games).toFixed(1)
  console.log(`${String(vision).padStart(3)} ${pct(w).padStart(8)}% ${pct(b).padStart(8)}% ` +
              `${pct(nada).padStart(8)}% ${(turnos / games).toFixed(1).padStart(7)} ` +
              `${(ms / jugadas).toFixed(2).padStart(10)}`)
}

void DRAW
