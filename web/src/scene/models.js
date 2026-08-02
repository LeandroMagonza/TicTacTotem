import * as THREE from 'three'
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js'
import { PIECE_HEIGHT, FOOTPRINT } from './geometry.js'

/**
 * Carga y normaliza los modelos de animales.
 *
 * EL TITULAR DE ESTE ARCHIVO: en Blender no hace falta poner el origen, ni
 * preocuparse por la escala, ni ubicar el modelo en el origen del mundo. El
 * loader reorigina y reescala todo. Lo unico que si tiene que estar bien es
 * aplicar las transformaciones (Ctrl+A -> All Transforms), porque una escala NO
 * UNIFORME de objeto hornea una distorsion en los vertices exportados que una
 * escala uniforme al cargar no puede deshacer.
 *
 * La degradacion es POR RANGO, no todo o nada: si falta el manifest, los cinco
 * niveles quedan procedurales; si `lobo.glb` da 404, el nivel 3 queda procedural
 * y los otros cuatro usan modelo. Podes modelar un animal por semana y jugar
 * todo el tiempo.
 */

/**
 * @typedef {object} RankEntry
 * @property {string} file
 * @property {number} [height]    alto final en unidades de celda; por defecto el del nivel
 * @property {string} [label]     nombre del animal, para la UI
 * @property {number} [yawDeg]    giro sobre Y si el modelo mira para otro lado
 * @property {'Y'|'Z'} [upAxis]   escape: solo si se exporto sin "+Y Up"
 * @property {number} [footprint]
 */

const loader = new GLTFLoader()

/**
 * @param {string} baseUrl
 * @returns {Promise<{models: Map<number, THREE.Object3D>, labels: Map<number,string>, avisos: string[]}>}
 */
export async function loadModels(baseUrl = import.meta.env?.BASE_URL ?? './') {
  const models = new Map()
  const labels = new Map()
  const avisos = []

  const root = `${baseUrl}models/`
  let manifest
  try {
    const res = await fetch(`${root}manifest.json`)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    manifest = await res.json()
  } catch (e) {
    // Sin manifest no es un error: el juego arranca con piezas procedurales.
    return { models, labels, avisos: [`sin manifest de modelos (${e.message}); se usan piezas procedurales`] }
  }

  const defaults = manifest.defaults ?? {}
  const entries = Object.entries(manifest.ranks ?? {})

  await Promise.all(entries.map(async ([rankStr, entry]) => {
    const rank = Number(rankStr)
    try {
      const gltf = await loader.loadAsync(`${root}${entry.file}`)
      const proto = normalize(gltf.scene, rank, entry, defaults, avisos)
      models.set(rank, proto)
      if (entry.label) labels.set(rank, entry.label)
    } catch (e) {
      avisos.push(`nivel ${rank}: no se pudo cargar ${entry.file} (${e.message}); queda procedural`)
    }
  }))

  return { models, labels, avisos }
}

/**
 * Normaliza un modelo cargado: escala uniforme al alto del nivel, recorte de
 * huella, y reorigen para que la base quede en y=0 y el centro XZ en el origen.
 *
 * @param {THREE.Object3D} scene
 * @param {number} rank
 * @param {RankEntry} entry
 * @param {object} defaults
 * @param {string[]} avisos
 */
export function normalize(scene, rank, entry, defaults = {}, avisos = []) {
  const inner = scene

  // Escape de eje. Blender es Z-up y glTF esta DEFINIDO Y-up, y la casilla
  // "+Y Up" del exportador (que viene marcada) hace la conversion. Asi que en el
  // camino normal esto es un no-op y no hay que rotar nada.
  const upAxis = entry.upAxis ?? defaults.upAxis ?? 'Y'
  if (upAxis === 'Z') inner.rotateX(-Math.PI / 2)
  if (entry.yawDeg) inner.rotateY((entry.yawDeg * Math.PI) / 180)

  inner.updateWorldMatrix(true, true)
  const box = new THREE.Box3().setFromObject(inner)
  const size = box.getSize(new THREE.Vector3())
  const center = box.getCenter(new THREE.Vector3())

  const alto = entry.height ?? PIECE_HEIGHT[rank] ?? 0.27
  const huella = entry.footprint ?? defaults.footprint ?? FOOTPRINT

  if (size.y <= 1e-6) {
    avisos.push(`nivel ${rank}: el modelo no tiene alto; queda procedural`)
    throw new Error('modelo plano')
  }

  let s = alto / size.y
  const anchoMax = Math.max(size.x, size.z) * s
  if (anchoMax > huella) {
    // Un aguila ancha no puede invadir la celda vecina. Se achica uniforme —
    // NUNCA no uniforme, que deformaria al animal — y se acepta que quede mas
    // baja que el resto de su nivel.
    const antes = s
    s *= huella / anchoMax
    avisos.push(
      `nivel ${rank}: ${entry.file} es muy ancho, se achico un ${((1 - s / antes) * 100).toFixed(0)}% ` +
      `para que entre en la huella (queda ${(size.y * s).toFixed(3)} de alto en vez de ${alto})`)
  }

  const grupo = new THREE.Group()
  inner.scale.setScalar(s)
  // Base en y=0 y centro XZ en el origen: apilar pasa a ser sumar alturas.
  inner.position.set(-center.x * s, -box.min.y * s, -center.z * s)
  grupo.add(inner)
  return grupo
}
