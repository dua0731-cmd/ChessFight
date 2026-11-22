# Renders the six corridor ornaments one by one (3/4 view from the corridor side, white ground) and stitches them
# into previews/ornaments_sheet.png (3 x 2) to compare with the GPT sheet.
#   blender -b --factory-startup --python cr47_ornaments_preview.py
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from mathutils import Vector
import cr_kit_04_07 as K
import cr47_ornaments as O

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "previews")
os.makedirs(OUT, exist_ok=True)
K.reset()
scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
S = 600
scene.render.resolution_x = scene.render.resolution_y = S
scene.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("W")
scene.world = world
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (1, 1, 1, 1)
for rot, e in (((math.radians(40), 0, math.radians(60)), 3.5), ((math.radians(70), 0, math.radians(-120)), 1.5)):
    s = bpy.data.objects.new("S", bpy.data.lights.new("S", "SUN"))
    s.data.energy = e
    s.rotation_euler = rot
    scene.collection.objects.link(s)
cam = bpy.data.objects.new("C", bpy.data.cameras.new("C"))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = "ORTHO"
tiles = []
for name, fn in O.ALL:
    col = K.collection(name)
    ob, em = fn(col, "CR_" + name)
    for c in bpy.data.collections:
        for o in c.objects:
            o.hide_render = c != col
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mid = (lo + hi) * .5
    size = max((hi - lo).length, .3)
    cam.data.ortho_scale = size * 1.05
    d = Vector((1.0, .75, .45)).normalized()
    cam.location = mid + d * 6
    cam.rotation_euler = (mid - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, "cr47_orn_%s.png" % name)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    tiles.append(path)
    print("TRIS", name, K.tris(ob), "size %.2f x %.2f x %.2f" % tuple(hi - lo))
sheet = np.ones((S * 2, S * 3, 4), dtype=np.float32)
for i, path in enumerate(tiles):
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(S, S, 4)
    r, c = 1 - i // 3, i % 3
    sheet[r * S:(r + 1) * S, c * S:(c + 1) * S] = px
out = bpy.data.images.new("sheet", S * 3, S * 2, alpha=True)
out.pixels = sheet.ravel()
out.filepath_raw = os.path.join(OUT, "cr47_ornaments_sheet.png")
out.file_format = "PNG"
out.save()
for p in tiles:
    os.remove(p)
