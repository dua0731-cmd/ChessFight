"""Close-up test stills of the giant (R6): a few named spots, each in the three test poses, from a few sides.

    blender -b rl_giant_a.blend --python rl_giant_closeup_stills.py -- <spot> <out.png>
spot: hand (the right hand; front, side, below-front) or shoulder (the right shoulder; back, back-side, top).
"""
import os, sys
import bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rl_giant_model as g

argv = sys.argv[sys.argv.index("--") + 1:]
spot, out = argv[0], argv[1]
rig = bpy.data.objects["GiantRig"]
SPOTS = {
    "hand": ("hand.R", .5, 22, (("front", Vector((0, -1, .2))), ("side", Vector((-1, 0, .1))), ("tq", Vector((-.7, -1, .5))))),
    "shoulder": ("upperarm.R", 0, 30, (("back", Vector((-.4, 1, .5))), ("backside", Vector((-1, .6, .3))), ("top", Vector((-.4, .3, 1))))),
}
bone, along, scale, views = SPOTS[spot]
rows = []
for p in ("rest", "reach", "raise"):
    g.pose(rig, p)
    sc, cam = g.setup_render(420, 420)
    bpy.context.view_layer.update()
    pb = rig.pose.bones[bone]
    c = rig.matrix_world @ (pb.head + (pb.tail - pb.head) * along)
    paths = []
    for view, d in views:
        cam.data.ortho_scale = scale
        cam.location = c + d.normalized() * 300
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(os.path.dirname(out), "_cl_%s_%s.png" % (p, view))
        bpy.ops.render.render(write_still=True); paths.append(sc.render.filepath)
    rows.append(paths)
print("SHEET", g.sheet(rows, out))
for r in rows:
    for f in r:
        os.remove(f)
