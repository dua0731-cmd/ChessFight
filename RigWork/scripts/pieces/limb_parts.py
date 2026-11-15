# Lists the loose parts of a rigged piece's limbs (arm stub, cuff, hand ball, feet): bounds, vertex count and the
# bone each is weighted to, and the arm bones' places. blender -b --factory-startup -P limb_parts.py -- <blend> ...
import bpy, sys
from mathutils import Vector

P = "mixamorig:"
for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.open_mainfile(filepath=path)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    ob = [o for o in bpy.data.objects if o.type == "MESH"][0]
    me = ob.data
    print("FILE", path.replace("\\", "/").split("/")[-1])
    for b in ("LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "Spine2", "Neck", "Head"):
        if P + b in arm.data.bones:
            print("  BONE %-12s head %s" % (b, [round(v, 3) for v in (arm.matrix_world @ arm.data.bones[P + b].head_local)]))
    gname = {g.index: g.name.replace(P, "") for g in ob.vertex_groups}
    par = list(range(len(me.vertices)))
    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    for e in me.edges:
        a, b = find(e.vertices[0]), find(e.vertices[1])
        if a != b:
            par[a] = b
    isl = {}
    for v in me.vertices:
        isl.setdefault(find(v.index), []).append(v.index)
    for ids in sorted(isl.values(), key=lambda q: -len(q)):
        ws = {}
        for i in ids:
            for g in me.vertices[i].groups:
                ws[gname[g.group]] = ws.get(gname[g.group], 0.0) + g.weight
        top = max(ws.items(), key=lambda kv: kv[1])[0] if ws else "-"
        if not any(k in top for k in ("Arm", "Hand", "Foot")):
            continue
        cs = [ob.matrix_world @ me.vertices[i].co for i in ids]
        if sum(c.x for c in cs) < 0:
            continue                                   # the left side only (x > 0)
        lo = [round(min(c[k] for c in cs), 4) for k in range(3)]
        hi = [round(max(c[k] for c in cs), 4) for k in range(3)]
        print("  PART %-10s verts %4d  min %s  max %s  weights %s" % (
            top, len(ids), lo, hi, {k: round(v / len(ids), 2) for k, v in ws.items()}))
