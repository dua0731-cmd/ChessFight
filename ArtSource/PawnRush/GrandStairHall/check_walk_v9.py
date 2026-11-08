# Walked tops in v9 against the blockout: from 3 m above each original floor piece's top, the first visible
# surface straight down must be that top (the runner is 1.5 cm, the dais inlay 1 cm). Lists what sits higher.
# Run: blender -b grand_stair_hall_v9.blend -P check_walk_v9.py
import bpy
from collections import Counter
from mathutils import Vector
dg = bpy.context.evaluated_depsgraph_get()
sc = bpy.context.scene
orig = bpy.data.collections["_Blockout originals"]
floors = [o for o in bpy.data.objects if o.type == "MESH" and o.data.materials and o.data.materials[0].name == "Section_Floor"
          and not o.name.startswith("B5_RoyalDais/Glass")]
hidden = set(o.name for o in orig.all_objects) | set(o.name for o in bpy.data.objects if o.name.startswith("H "))
for o in bpy.data.objects:
    o.hide_set(o.name in hidden)
worst, n, bad = 0.0, 0, Counter()
for o in floors:
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    zt = max(v.z for v in vs)
    top = [v for v in vs if v.z > zt - 0.02] if "(smooth" not in o.name else None
    if top is None:
        continue                                        # curved strips are kept as they were
    x0, x1 = min(v.x for v in top) + 0.3, max(v.x for v in top) - 0.3
    y0, y1 = min(v.y for v in top) + 0.3, max(v.y for v in top) - 0.3
    if x1 <= x0 or y1 <= y0:
        continue
    for i in range(7):
        for j in range(7):
            p = Vector((x0 + (x1 - x0) * i / 6, y0 + (y1 - y0) * j / 6, zt + 3))
            hit, loc, _, _, ob, _ = sc.ray_cast(dg, p, Vector((0, 0, -1)), distance=6)
            n += 1
            if not hit:
                bad["(nothing) under " + o.name] += 1; continue
            d = loc.z - zt
            if d > 0.03 or d < -0.03:
                bad[f"{ob.name} over {o.name.split('/')[-1]} ({d:+.2f})"] += 1
            worst = max(worst, abs(d)) if abs(d) < 0.5 else worst
print("WALK samples", n, "max diff under 0.5 m: %.3f" % worst)
for k, c in bad.most_common(40):
    print("  ", c, k)
