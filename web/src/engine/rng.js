/**
 * mulberry32: generador chico, rapido y sembrable.
 *
 * NO intenta reproducir System.Random de .NET (Program.cs:351). Eso seria una
 * trampa: .NET no garantiza la misma secuencia entre plataformas, asi que una
 * partida de `practica` en C# no se puede repetir jugada por jugada en JS. Por
 * eso el test diferencial compara VALORES, no jugadas elegidas.
 *
 * Para repeticiones dentro del juego alcanza con guardar la semilla.
 *
 * @param {number} seed
 */
export function makeRng(seed) {
  let a = seed >>> 0
  const next = () => {
    a = (a + 0x6d2b79f5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), 1 | t)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
  return {
    /** [0,1) */
    next,
    /** Entero en [0, n) */
    int: (n) => Math.floor(next() * n),
    /** Elemento al azar de un array no vacio */
    pick: (arr) => arr[Math.floor(next() * arr.length)],
  }
}
