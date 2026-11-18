import bpy, sys, os
from mathutils import Vector
sys.path.insert(0, r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\RoyalLibraryModels")
import rl_giant_model as g
rig = bpy.data.objects["GiantRig"]
out = sys.argv[sys.argv.index("--")+1]
rows = []
for p in ("rest", "reach", "raise"):
    g.pose(rig, p)
    sc, cam = g.setup_render(420, 420)
    paths = []
    bpy.context.view_layer.update()
    for view, d in (("front", Vector((0,-1,.25))), ("tq", Vector((-.8,-1,.45))), ("back", Vector((.6,1,.6)))):
        hb = rig.pose.bones["neck"]; c = rig.matrix_world @ hb.head
        cam.data.ortho_scale = 26
        cam.location = c + d.normalized()*300
        cam.rotation_euler = (-d).to_track_quat("-Z","Y").to_euler()
        sc.render.filepath = os.path.join(out, "neck_%s_%s.png" % (p, view)); bpy.ops.render.render(write_still=True); paths.append(sc.render.filepath)
    rows.append(paths)
print("SHEET", g.sheet(rows, os.path.join(out, "neck_sheet.png")))
for r in rows:
    for f in r: os.remove(f)
