// Constantes del juego. Portadas de Simulacion/solver/Game.cs:19-42.

export const HAND = 15
export const CELLS = 9
export const WHITE = 0
export const BLACK = 1

/** Resultado, siempre desde el punto de vista de las BLANCAS. Game.cs:8 */
export const BLACK_WIN = -1
export const DRAW = 0
export const WHITE_WIN = 1

/**
 * "La partida sigue". En C# esto es `Outcome?` con null (Game.cs:130), pero en JS
 * no se puede usar null ni un chequeo de verdad: DRAW es 0, que es un resultado
 * VALIDO y falsy. Y 2/-2 ya estan tomados como centinelas del alpha-beta
 * (Search.cs:143). Queda 3.
 *
 * Regla sin excepciones: comparar siempre `w !== ONGOING`, jamas `if (w)`.
 * Hay un test que hace grep del codigo buscando violaciones.
 */
export const ONGOING = 3

/** Las 8 lineas del tablero. Game.cs:38-42 */
export const LINES = Object.freeze([
  [0, 1, 2], [3, 4, 5], [6, 7, 8],
  [0, 3, 6], [1, 4, 7], [2, 5, 8],
  [0, 4, 8], [2, 4, 6],
])

/**
 * 16**i, exacto para todo i <= 13 porque 16**i = 2**(4i) y 4*13 = 52 < 53.
 * Es lo que permite hacer el desempaquetado con aritmetica en vez de bitwise:
 * los operadores << & | de JS truncan a 32 bits y darian mal desde la pieza 8.
 */
export const POW16 = Float64Array.from({ length: 14 }, (_, i) => 16 ** i)

/** Maximo de piezas que entra en un Number exacto: 13 * 4 = 52 bits. */
export const MAX_PIECES = 13
