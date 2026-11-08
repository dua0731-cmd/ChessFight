"""Knight check: the same knight from the side, three-quarter front, front and three-quarter back.

blender -b --factory-startup --python preview_knight.py -- <pieces_dir> <out_png>
"""
import math
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = args[0], args[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
for i, yaw in enumerate((0, 40, 90, 150)):
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "PR_Piece_Knight.fbx"))
    o = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.location = (i * 0.85 - 1.3, 0, 0)
    o.rotation_euler = (0, 0, math.radians(yaw))
    for m in o.data.materials:
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (0.9, 0.86, 0.77, 1)
        b.inputs["Roughness"].default_value = 0.35
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1600, 520
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.55, 0.68, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5
sc.world = w
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.2
sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
sc.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.lens = 60
sc.collection.objects.link(cam)
cam.location = (0, -5.4, 1.05)
cam.rotation_euler = (Vector((0, 0, 0.52)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("DONE")
