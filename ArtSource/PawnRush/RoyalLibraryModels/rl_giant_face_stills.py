"""The giant's face next to the reference drawings, close up (R6, 11-18, 승규 "얼굴을 좀 더 컨셉 아트 처럼").

    blender -b rl_giant_a.blend --python rl_giant_face_stills.py -- [A|B]

Writes stills/giant/giant_<a|b>_face.png: reference front | model front | reference side | model side, the same
crop and scale (the drawings' pixel scale F).
"""
import os, sys
import bpy
import numpy as np
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rl_giant_model as g

which = (sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "A").upper()
g.pose(bpy.data.objects["GiantRig"], "rest")
N = 380                                  # crop size in reference pixels (rows 115..495)
row0, fc, sc_ = 115, 512, 575
sc, cam = g.setup_render(N, N)
cam.data.ortho_scale = N * g.F
zc = g.zr(row0 + N / 2)
tiles = []
for view, d, centre, col in (("front", Vector((0, -1, 0)), Vector((0, 0, zc)), fc),
                             ("side", Vector((1, 0, 0)), Vector((0, g.ys(sc_), zc)), sc_)):
    ref = g.load_px(os.path.join(g.REFS, "GC_rig_%s_%s.png" % (which, view)))
    H = ref.shape[0]
    tiles.append(ref[H - (row0 + N):H - row0, col - N // 2:col + N // 2])
    cam.location = centre + d * 400
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(g.STILLS, "_face_%s.png" % view)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    tiles.append(g.load_px(path)); os.remove(path)
gap = np.ones((N, 8, 4), np.float32)
row = np.concatenate([tiles[0], gap, tiles[1], gap, tiles[2], gap, tiles[3]], 1)
print("[rl_giant] face", g.save_px(row, os.path.join(g.STILLS, "giant_%s_face.png" % which.lower())))
