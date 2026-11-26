"""Debug stills of the pilaster's capital alone (bh-hall, B7): front and side, sheet light. Not part of the model.

    blender -b --factory-startup --python bh_b7_debug_capital.py -- <out folder>
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h   # noqa: E402

out = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
col = bpy.data.collections.new("dbg")
bpy.context.scene.collection.children.link(col)
objs = h.pilaster(col)
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.view_settings.view_transform = "Standard"
w = bpy.data.worlds.new("w")
sc.world = w
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (.8, .8, .8, 1)
sun = h._light("key", "SUN", (0, 0, 0), 3.0, (1, 1, 1), 5)
sun.rotation_euler = (math.radians(45), 0, math.radians(-35))
sc.render.resolution_x, sc.render.resolution_y = 900, 600
look = Vector((0, 1.2, 13.7))
for nm, d in (("front", Vector((0, 1, .05))), ("side", Vector((1, .15, .05))), ("q", Vector((-.6, 1, .3)))):
    cam = h.camera(nm, look + d.normalized() * 9, look, 60)
    sc.camera = cam
    sc.render.filepath = os.path.join(out, "cap_%s.png" % nm)
    bpy.ops.render.render(write_still=True)
