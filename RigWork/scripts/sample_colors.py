# The old pawn's texture colours by part: median sRGB of the texels under each island / height band, and how much
# the painted patches vary (luminance spread). blender -b -P sample_colors.py -- <Pawn.fbx> <albedo.jpg>
import bpy, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
a = sys.argv[sys.argv.index("--") + 1:]
sys.argv = sys.argv[:sys.argv.index("--") + 1] + [a[0], "C:/Users/trews/Downloads", os.path.dirname(os.path.abspath(__file__))]
import pawn_match as pm

bpy.ops.wm.read_factory_settings(use_empty=True)
arm, mesh = pm.load_old()
ws, H, comps, kinds = pm.builder_parts(mesh)
im = bpy.data.images.load(a[1])
w, h = im.size
px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3]   # linear? jpg loads as sRGB bytes -> floats
me = mesh.data
uv = me.uv_layers.active.data
compOf = {}
for c, ids in enumerate(comps):
    for i in ids:
        compOf[i] = c
groups = {}
for poly in me.polygons:
    for li in poly.loop_indices:
        vi = me.loops[li].vertex_index
        p = ws[vi]
        c = compOf[vi]
        if c == 0:
            if p.z < 0.115:
                g = "feet"
            elif p.z < 0.24:
                g = "skirt"
            elif p.z < 0.555:
                g = "body"
            elif p.z < 0.625:
                g = "collar"
            else:
                g = "head"
        else:
            g = {1: "hand", 2: "stub", 3: "face%d" % c}[kinds[c]]
        u, v = uv[li].uv
        x = min(w - 1, max(0, int(u * w)))
        y = min(h - 1, max(0, int(v * h)))
        groups.setdefault(g, []).append(px[y, x])
        if g == "collar" and px[y, x][2] > px[y, x][0] + 0.12:
            groups.setdefault("collar_blue", []).append(px[y, x])
            groups.setdefault("_bandz", []).append(p.z)
        if g == "collar":
            groups.setdefault("_collarz", []).append(p.z)
out = {}
for g in ("_bandz", "_collarz"):
    z = np.array(groups.pop(g, [0]))
    print("BAND", g, "p2 %.4f p10 %.4f p50 %.4f p90 %.4f p98 %.4f" % tuple(np.percentile(z, q) for q in (2, 10, 50, 90, 98)))
for g, cols in sorted(groups.items()):
    arr = np.array(cols)
    med = np.median(arr, axis=0)
    lum = arr @ np.array([0.2126, 0.7152, 0.0722])
    out[g] = dict(n=len(cols), median=[round(float(c), 3) for c in med],
                  hex="#%02X%02X%02X" % tuple(int(round(float(c) * 255)) for c in med),
                  lum_p10_p90=[round(float(np.percentile(lum, 10)), 3), round(float(np.percentile(lum, 90)), 3)])
    print("COL", g, json.dumps(out[g]))
