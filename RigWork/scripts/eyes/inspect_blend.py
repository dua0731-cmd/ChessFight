import bpy, sys
from mathutils import Vector
for p in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.open_mainfile(filepath=p)
    print("FILE", p.split("/")[-1])
    for o in bpy.data.objects:
        s = "  %s %s parent=%s loc=%s rot=%s scale=%s" % (o.name, o.type, o.parent.name if o.parent else None,
            tuple(round(v, 3) for v in o.location), tuple(round(v, 3) for v in o.rotation_euler), tuple(round(v, 3) for v in o.scale))
        if o.type == "MESH":
            ws = [o.matrix_world @ v.co for v in o.data.vertices]
            mn = Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws)))
            mx = Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws)))
            s += " verts %d faces %d bbox %s..%s mats %s uv %s smooth %d/%d" % (len(o.data.vertices), len(o.data.polygons),
                tuple(round(c, 3) for c in mn), tuple(round(c, 3) for c in mx), [m.name for m in o.data.materials if m],
                [u.name for u in o.data.uv_layers], sum(pl.use_smooth for pl in o.data.polygons), len(o.data.polygons))
        print(s)
