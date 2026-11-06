# R488: deletes the gate terraces' eight potted cone topiaries (pot, gold rim, cone, gold ball) from WallsSample_v2.blend
# as it stands (after the hand-made pass), so nothing else moves. --dry lists what it would take.
import bpy, bmesh, sys, math
from collections import defaultdict

args = sys.argv[sys.argv.index("--") + 1:]
BLEND, DRY = args[0], "--dry" in args
bpy.ops.wm.open_mainfile(filepath=BLEND)
UPPER = 3.8
CENTRES = [(s * 16.6, y) for s in (1, -1) for y in (50.6, 53.3, 56.0, 58.4)]
REACH = 1.0                    # step 2 moved freestanding topiaries up to 0.3 m

total = 0
for ob in bpy.data.objects:
    if ob.type != "MESH" or not ob.users_collection or "Walls dressing" not in [c.name for c in ob.users_collection]:
        continue
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    mw = ob.matrix_world
    comp, cid = {}, 0
    for v in bm.verts:
        if v in comp:
            continue
        st = [v]
        comp[v] = cid
        while st:
            a = st.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if b not in comp:
                    comp[b] = cid
                    st.append(b)
        cid += 1
    parts = defaultdict(list)
    for v, c in comp.items():
        parts[c].append(v)
    doomed = []
    hits = defaultdict(int)
    for c, vs in parts.items():
        pts = [mw @ v.co for v in vs]
        zs = [p.z for p in pts]
        if min(zs) < UPPER - .1 or max(zs) > UPPER + 2.2:
            continue
        for k, (cx, cy) in enumerate(CENTRES):
            if all(math.hypot(p.x - cx, p.y - cy) < REACH for p in pts):
                doomed.extend(vs)
                hits[k] += 1
                break
    if doomed:
        print("TOPIARY %s: %d parts, %d verts, per place %s" % (ob.name, sum(hits.values()), len(doomed), dict(hits)))
        total += sum(hits.values())
        if not DRY:
            bmesh.ops.delete(bm, geom=doomed, context="VERTS")
            bm.to_mesh(ob.data)
            ob.data.update()
    bm.free()
print("TOPIARY parts", total)
if not DRY:
    bpy.ops.wm.save_mainfile(filepath=BLEND)
    print("TOPIARY saved")
