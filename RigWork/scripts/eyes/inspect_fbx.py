import bpy, sys, math
from mathutils import Vector
for p in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=p)
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            print(" ", o.name, o.type); continue
        me = o.data
        ws = [o.matrix_world @ v.co for v in me.vertices]
        mn = Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws)))
        mx = Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws)))
        print(p.split("/")[-1], o.name, "verts", len(me.vertices), "faces", len(me.polygons), "uv", [u.name for u in me.uv_layers],
              "mats", [m.name for m in me.materials if m], "bbox", tuple(round(c, 3) for c in mn), tuple(round(c, 3) for c in mx),
              "rot", tuple(round(c, 3) for c in o.rotation_euler), "scale", tuple(round(c, 3) for c in o.scale))
