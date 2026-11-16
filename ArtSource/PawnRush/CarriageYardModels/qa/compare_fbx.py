# blender -b --factory-startup --python qa/compare_fbx.py -- <old dir> <new dir> <name1,name2,...>
# Compares two exports of the same FBX: triangles, zero-area faces, bounds, surface area, material slots, and the
# largest distance from any new vertex to the old surface (and back), so a cleanup can be shown to change no shape.
import sys, os
import bpy, bmesh
from mathutils.bvhtree import BVHTree

old_dir, new_dir, names = sys.argv[sys.argv.index("--") + 1:]


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    bm = bmesh.new()
    mats = []
    for ob in bpy.data.objects:
        if ob.type != "MESH":
            continue
        mats += [s.material.name if s.material else "" for s in ob.material_slots]
        tmp = bmesh.new()
        tmp.from_mesh(ob.data)
        tmp.transform(ob.matrix_world)
        me = bpy.data.meshes.new("t")
        tmp.to_mesh(me)
        tmp.free()
        bm.from_mesh(me)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    zero = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    area = sum(f.calc_area() for f in bm.faces)
    cos = [v.co.copy() for v in bm.verts]
    lo = [min(c[i] for c in cos) for i in range(3)]
    hi = [max(c[i] for c in cos) for i in range(3)]
    bvh = BVHTree.FromBMesh(bm)
    return {"tris": tris, "zero": zero, "area": area, "lo": lo, "hi": hi, "mats": sorted(set(mats)), "cos": cos, "bvh": bvh}


def far(a, b):
    worst = 0.0
    for c in a["cos"]:
        hit = b["bvh"].find_nearest(c)
        if hit[0] is not None:
            worst = max(worst, hit[3])
    return worst


for n in names.split(","):
    o = load(os.path.join(old_dir, n + ".fbx"))
    w = load(os.path.join(new_dir, n + ".fbx"))
    bounds = max(max(abs(o["lo"][i] - w["lo"][i]), abs(o["hi"][i] - w["hi"][i])) for i in range(3))
    print("RCY_CMP %-18s tris %6d -> %6d  zero %4d -> %d  area %.4f -> %.4f  bounds d %.5f  new->old %.5f  old->new %.5f  mats %s"
          % (n, o["tris"], w["tris"], o["zero"], w["zero"], o["area"], w["area"], bounds, far(w, o), far(o, w),
             "same" if o["mats"] == w["mats"] else "%s -> %s" % (o["mats"], w["mats"])))
