# Numbers for the round hall (승규 11-03 "원형으로 올라가는 코스랑 원형 벽 구조물이랑 회전 반경이랑 규격도 딱 맞아 떨어지는 지
# 튀어나오는 구조물이 없는지 수치상 검토"): the bent wall and the dome against the spiral course and Unity's own numbers
# (GrandStairHallBuilder: hub (0, 92), wall face r 31, collision 31 .. 33, the curving stair out to r 31, the chandelier
# beams to r 31.5). Read-only; prints AUDIT lines.
# Run: blender -b grand_stair_hall_v21C.blend -P round_hall_audit.py [-- <collection name>]
import bpy, math, sys
from mathutils import Vector
HY, R0 = 92.0, 31.0
ARCS = ((108.0, 234.0), (306.0, 432.0))
name = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else next(c.name for c in bpy.data.collections if c.name.startswith("Round_hall"))
ring = bpy.data.collections[name]
for lc in bpy.context.view_layer.layer_collection.children:      # every dome sample evaluated, not only the shown one
    if lc.name.startswith("Dome_"):
        lc.exclude = False
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()


def pts(o):
    e = o.evaluated_get(dg)
    me = e.to_mesh()
    out = [e.matrix_world @ v.co for v in me.vertices]
    e.to_mesh_clear()
    return out


def rphi(p):
    dx, dy = p.x, p.y - HY
    return math.hypot(dx, dy), math.degrees(math.atan2(dy, dx)) % 360


def in_arc(phi):
    return any(a0 <= phi <= a1 or a0 <= phi + 360 <= a1 for a0, a1 in ARCS)


def spiral_r(phi):                       # SpiralRadius, phi 297 .. 717
    return 28.75 + (10 - 28.75) * (phi - 297) / 420


def stair_y(phi):                        # the curving stair's top, phi 297 .. 372, y 8 -> 10
    return 8 + 2 * (phi - 297) / 75


# ---- 1. the ring's own geometry: anything inside r 31 (into the hall) below the wall top, by kind
walls = [o for o in ring.objects if o.type == "MESH" and o.name.startswith("Round_wall") and "tracery" not in o.name]
other = [o for o in ring.objects if o.type == "MESH" and o not in walls]
wp = [p for o in walls for p in pts(o)]
rs = [rphi(p)[0] for p in wp]
print("AUDIT wall r %.3f .. %.3f, z %.2f .. %.2f, %d verts" % (min(rs), max(rs), min(p.z for p in wp), max(p.z for p in wp), len(wp)))
inside = [p for p, r in zip(wp, rs) if r < R0 - .005]
print("AUDIT wall verts inside r 31: %d (deepest %.3f)" % (len(inside), min(rs)))
# the face the stair meets: frontmost wall per 1 deg at the stair's height (its top .. +2 m), phi 306 .. 372
bins = {}
for p, r in zip(wp, rs):
    phi = rphi(p)[1]
    ph = phi if phi >= 297 else phi + 360
    if 306 <= ph <= 372:
        y = stair_y(ph)
        if y - .3 <= p.z <= y + 2.0:
            k = int(ph)
            bins[k] = min(bins.get(k, 99), r)
gaps = sorted(bins.items())
# anything of the wall inside r 31 where a pawn on the curving stair can be (its top .. +2.4 m, a jump)
hit = []
for p, r in zip(wp, rs):
    if r >= R0 - .005:
        continue
    ph = rphi(p)[1]
    ph = ph if ph >= 200 else ph + 360
    if 297 <= ph <= 372 and stair_y(ph) <= p.z <= stair_y(ph) + 2.4:
        hit.append((r, ph, p.z))
print("AUDIT wall inside r 31 within a pawn's reach over the curving stair: %d verts" % len(hit)
      + ("" if not hit else " (deepest r %.2f at phi %.1f z %.2f)" % min(hit)))
bz = {}
for p, r in zip(wp, rs):
    if r < R0 - .005:
        k = int((p.z + 4) // 2) * 2 - 4
        bz[k] = min(bz.get(k, 99), r)
print("AUDIT wall inside r 31 by z band (z:deepest r):", " ".join("%d:%.2f" % kv for kv in sorted(bz.items())))
print("AUDIT stair-height wall face r by deg (306..372): min %.3f max %.3f" % (min(v for _, v in gaps), max(v for _, v in gaps)))
print("AUDIT   per 3 deg:", " ".join("%d:%.2f" % (k, v) for k, v in gaps if k % 3 == 0))
for o in other:
    ps = pts(o)
    rr = [rphi(p)[0] for p in ps]
    lowz = min(p.z for p in ps)
    print("AUDIT part %-28s r %.2f .. %.2f  z %.2f .. %.2f" % (o.name[:28], min(rr), max(rr), lowz, max(p.z for p in ps)))

# ---- 2. the course and everything else in the round part: what crosses r 31 (into the wall), and where
print("AUDIT ---- course pieces reaching past r 30.5 inside the wall's arcs")
for o in bpy.data.objects:
    if o.type != "MESH" or o.users_collection[0] == ring or o.hide_render:
        continue
    if any(c.hide_render for c in o.users_collection):
        continue
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    if max(p.y for p in bb) < 60 or min(p.y for p in bb) > 125:
        continue
    far = max(bb, key=lambda p: rphi(p)[0])
    r, phi = rphi(far)
    if r < 30.5 or not in_arc(phi):
        continue
    ps = pts(o)
    worst = max(ps, key=lambda p: rphi(p)[0] if in_arc(rphi(p)[1]) else 0)
    r, phi = rphi(worst)
    if r > 30.5 and in_arc(phi):
        print("AUDIT %-55s r %.2f at phi %.1f z %.2f  %s" % (o.name[:55], r, phi, worst.z,
                                                          "INTO WALL %.2f m" % (r - R0) if r > R0 + .01 else "ok"))

# ---- 3. the spiral's clearance under the dome parts: lowest dome part above each spiral radius band
groups = [(name, other)] + [(c.name, [o for o in c.objects if o.type == "MESH"]) for c in bpy.data.collections
                             if c.name.startswith("Dome_")]
for gname, objs in groups:
    print("AUDIT ---- headroom under %s: lowest part above the course (course top + 2.4 m jump)" % gname)
    cloud = [(rphi(p)[0], p.z) for o in objs for p in pts(o)]
    if not cloud:
        continue
    print("AUDIT   %s r %.2f .. %.2f, z %.2f .. %.2f" % (gname, min(c[0] for c in cloud), max(c[0] for c in cloud),
                                                       min(c[1] for c in cloud), max(c[1] for c in cloud)))
    for phi0, phi1, y in ((297, 372, 10), (372, 440, 11.5), (440, 575, 14), (575, 717, 16), (717, 717, 16)):
        rin, rout = (spiral_r(phi1) - 2.5, min(R0, spiral_r(phi0) + 2.5)) if phi0 != phi1 else (0, 8.0)
        low = min((z for r, z in cloud if rin <= r <= rout), default=99)
        what = "dais" if phi0 == phi1 else "phi %d..%d" % (phi0, phi1)
        print("AUDIT   %-12s r %4.1f..%4.1f top <= %4.1f: lowest above %.2f (clear %.2f m)" % (what, rin, rout, y, low, low - y - 2.4))
hub = [o for o in bpy.data.objects if o.name.startswith("Chandelier_6/Part_")]
if hub:
    bot = min((o.matrix_world @ Vector(c)).z for o in hub for c in o.bound_box)
    print("AUDIT hub chandelier bottom %.2f: %.2f m over the dais top 16 (clear %.2f m after a 2.4 m jump)" % (bot, bot - 16, bot - 18.4))
