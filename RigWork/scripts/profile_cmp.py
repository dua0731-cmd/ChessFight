# Old pawn body versus the new pawn body (as pawn_match.py scales it), height by height: the silhouette's reach in
# +x, -x, +y, -y per 1 cm slice. blender -b -P profile_cmp.py -- <Pawn.fbx> <parts dir> <out dir>
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pawn_match as pm
from mathutils import Vector, Matrix

bpy.ops.wm.read_factory_settings(use_empty=True)
arm, old_mesh = pm.load_old()
ws, H, comps, kinds = pm.builder_parts(old_mesh)
M = pm.old_measures(ws, H, comps, kinds)
print("islands:", [(c, kinds[c], len(ids)) for c, ids in enumerate(comps)][:20])
old_pts = []
for c, ids in enumerate(comps):
    if kinds[c] in (1, 2):
        continue
    for i in ids:
        p = ws[i]
        if p.z < 0.12 * H and abs(p.x) > 0.035 * H:
            continue
        old_pts.append(p)

old_mesh_polys = [type("P", (), {"vertices": tuple(pl.vertices)}) for pl in old_mesh.data.polygons]
bpy.data.objects.remove(old_mesh, do_unlink=True)
body = pm.drop_hidden(pm.import_parts(os.path.join(pm.PARTS, "pawn.fbx")))
mn, mx = pm.bounds(body)
base = [o.matrix_world @ v.co for o in body for v in o.data.vertices if (o.matrix_world @ v.co).z < mn.z + 0.03]
bmn, bmx = pm.bounds_pts(base)
bc = Vector(((bmn.x + bmx.x) / 2, (bmn.y + bmx.y) / 2, mn.z))
s_body = (M["top"] - M["skirt"]) / (mx.z - mn.z)
T = Matrix.Translation(Vector((0, 0, M["skirt"]))) @ Matrix.Scale(s_body, 4) @ Matrix.Translation(-bc)
new_pts, new_polys = [], []
for o in body:
    base_i = len(new_pts)
    new_pts += [T @ (o.matrix_world @ v.co) for v in o.data.vertices]
    new_polys += [tuple(base_i + v for v in pl.vertices) for pl in o.data.polygons]

from mathutils.bvhtree import BVHTree
keep = set()
for c, ids in enumerate(comps):
    if kinds[c] not in (1, 2):
        keep.update(ids)
old_tree = BVHTree.FromPolygons(ws, [tuple(pl.vertices) for pl in old_mesh_polys if all(v in keep for v in pl.vertices)])
new_tree = BVHTree.FromPolygons(new_pts, new_polys)

def reach(tree, z, ang):
    d = Vector((math.cos(ang), math.sin(ang), 0))
    hit = tree.ray_cast(d * 1.0 + Vector((0, 0, z)), -d)
    return (hit[0] - Vector((0, 0, z))).length if hit[0] is not None else 0.0

DIRS = [0, math.pi, math.pi / 2, -math.pi / 2]   # +x -x +y(back? front?) -y
rows = []
lines = ["z     | old +x  -x  +y  -y | new +x  -x  +y  -y | x ratio  y ratio"]
z = 0.120
while z <= 1.0:
    o = [reach(old_tree, z, a) for a in DIRS]
    n = [reach(new_tree, z, a) for a in DIRS]
    rx = (o[0] + o[1]) / (n[0] + n[1]) if n[0] + n[1] > 1e-6 else 0
    ry = (o[2] + o[3]) / (n[2] + n[3]) if n[2] + n[3] > 1e-6 else 0
    rows.append((z, o, n))
    lines.append("%.3f | %s | %s | %.3f %.3f" % (z, " ".join("%.3f" % v for v in o), " ".join("%.3f" % v for v in n), rx, ry))
    z += 0.01
out = os.path.join(pm.OUT, "profiles_cmp.txt")
open(out, "w").write(chr(10).join(lines))
print(chr(10).join(lines))
