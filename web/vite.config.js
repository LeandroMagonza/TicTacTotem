// base: './' a proposito. Hace que el mismo dist/ funcione en la raiz, en
// /play/ y en /legacy/ sin rebuild. El precio es que todo asset que se busque
// en runtime (el manifest de modelos, los .glb) tiene que usar
// import.meta.env.BASE_URL + 'models/...', nunca una barra inicial.
export default {
  base: './',
  build: {
    target: 'es2022',
    assetsInlineLimit: 4096,
    sourcemap: true,
  },
  worker: { format: 'es' },
  // host: true para poder entrar desde el celular por la LAN. No es polish:
  // es como se testea touch en M5, y testear touch tarde es el riesgo #1.
  server: { host: true },
}
