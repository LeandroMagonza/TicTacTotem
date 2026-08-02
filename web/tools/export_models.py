"""Exporta cada objeto de malla de un .blend a un .glb listo para el juego.

    blender --background animales.blend --python tools/export_models.py -- <carpeta_salida>

Si no se pasa carpeta, escribe en web/public/models/ relativo a este script.

Que hace por cada objeto de malla:
  - lo selecciona solo a el
  - exporta glTF binario, +Y Up, con modificadores aplicados
  - sin camaras, ni luces, ni animaciones, ni UVs, ni tangentes, ni Draco

Por que estas opciones:

  +Y Up          Blender es Z-up y glTF esta DEFINIDO Y-up. La casilla viene
                 marcada por defecto; el juego asume que se respeto. Si se
                 exporta sin ella, hay que poner "upAxis": "Z" en el manifest.

  Draco apagado  Exige embarcar ~200 KB de decoder WASM mas un paso async de
                 decodificacion, para animales low-poly sin textura de 150-250 KB
                 cada uno. El decoder cuesta mas que lo que ahorra.

  Sin materiales El juego pinta todo con DOS materiales, uno por equipo. Cualquier
  utiles         color horneado se multiplicaria con el tinte y lo ensuciaria.

Lo que SI tiene que estar bien en el .blend, porque el loader no lo puede arreglar:
  - Ctrl+A -> All Transforms. Una escala NO UNIFORME de objeto hornea una
    distorsion en los vertices que una escala uniforme al cargar no deshace.
  - Un solo material por animal, sin textura (o una textura gris neutra).
  - Sin vertex colors, o blancos.
  - Unir en un solo objeto (Ctrl+J), asi es una malla y un draw call.
  - Normales para afuera: el sombreado sin textura de un solo material es
    implacable con las normales dadas vuelta.

Lo que NO hace falta cuidar, porque el loader lo normaliza:
  - el origen del objeto
  - la escala
  - la posicion en el mundo
"""
import os
import sys

import bpy

argv = sys.argv
argv = argv[argv.index("--") + 1:] if "--" in argv else []

if argv:
    salida = argv[0]
else:
    aqui = os.path.dirname(os.path.abspath(bpy.context.space_data.text.filepath
                                            if bpy.context.space_data else __file__))
    salida = os.path.normpath(os.path.join(aqui, "..", "public", "models"))

os.makedirs(salida, exist_ok=True)

# Los nombres de los argumentos del exportador cambiaron entre Blender 3.x y 4.x.
# Se pasan solo los que existen en esta version, y los demas se ignoran: todos
# menos use_selection y export_apply son el valor por defecto igual.
import inspect  # noqa: E402

try:
    admitidos = set(inspect.signature(bpy.ops.export_scene.gltf).parameters)
except (TypeError, ValueError):
    admitidos = None


def exportar(ruta):
    opciones = {
        "filepath": ruta,
        "export_format": "GLB",
        "use_selection": True,
        "export_apply": True,       # aplica modificadores
        "export_yup": True,         # Z-up de Blender -> Y-up de glTF
        "export_animations": False,
        "export_cameras": False,
        "export_lights": False,
        "export_texcoords": False,
        "export_tangents": False,
        "export_normals": True,
    }
    if admitidos is not None:
        opciones = {k: v for k, v in opciones.items() if k in admitidos or k == "filepath"}
    bpy.ops.export_scene.gltf(**opciones)


mallas = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not mallas:
    print("No hay objetos de malla en la escena.")
    sys.exit(1)

for obj in mallas:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    ruta = os.path.join(salida, f"{obj.name}.glb")
    exportar(ruta)

    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    kb = os.path.getsize(ruta) / 1024
    aviso = "  <-- pesado, revisar" if kb > 500 else ""
    print(f"  {obj.name:24s} {tris:7d} tris  {kb:8.1f} KB{aviso}")

print(f"\n{len(mallas)} modelo(s) en {salida}")
print("Falta mapear nivel -> archivo en public/models/manifest.json:")
print('  "ranks": { "1": { "file": "%s.glb", "label": "..." }, ... }' % mallas[0].name)
