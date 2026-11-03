# The grand stair's side walls (both sides of the lower flight, the middle landing and the upper flight,
# down to the hall floor at -4) dressed as a palace stair's stone flank: a projecting plinth, rusticated
# ashlar courses with chamfered joints, pilasters at the ends and where the pitch changes, and a moulded
# string course running up the stair's rake just under the balustrade. Everything stands outside the
# blockout's side planes (x = +-8), so the walked stair is untouched. No paint: v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v13.blend -P stair_walls_v14.py   -> grand_stair_hall_v14.blend
import bpy, bmesh, math, os, random
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
X = 8.0                    # the stair's side plane (east; the west is its mirror)
Y0, Y1 = 14.0, 51.0        # from the foyer to the central landing
FLOOR = -4.0               # the hall floor (respawn)
PLINTH_TOP, PLINTH_OUT = FLOOR + 0.6, 0.42
COURSE, BLOCK, PROUD, JOINT = 0.55, 1.1, 0.18, 0.012
STRING_H = 0.36
PILASTERS = [Y0 + 0.45, 21.5, 29.0, 35.0, 43.0, Y1 - 0.45]   # ends and the pitch changes
PIL_W, PIL_OUT = 0.9, 0.30
random.seed(14)

CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Stair_walls_v14"); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]


def nose(y):
    """The line through the step noses (the stair's rake): lower flight, middle landing, upper flight."""
    if y <= 29:
        return 0.25 * (y - 13)
    if y <= 35:
        return 4.0
    return 4.0 + 0.25 * (y - 35)


def top(y):
    return nose(y) + 0.1          # up to the balustrade's foot


def under_string(y):
    return top(y) - STRING_H


KINKS = [29.0, 35.0]


def box(bm, x0, x1, ya, yb, z0, ztop, chamfer):
    """A block whose top follows ztop(y) (straight between ya and yb), chamfered all round."""
    vs = []
    for x in (x0, x1):
        for y in (ya, yb):
            vs.append(bm.verts.new((x, y, z0)))
            vs.append(bm.verts.new((x, y, ztop(y))))
    # order: (x0,ya,b) (x0,ya,t) (x0,yb,b) (x0,yb,t) (x1,ya,b) (x1,ya,t) (x1,yb,b) (x1,yb,t)
    f = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (1, 3, 7, 5), (0, 4, 6, 2)]
    faces = [bm.faces.new([vs[i] for i in q]) for q in f]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    if chamfer:
        edges = list({e for fc in faces for e in fc.edges})
        bmesh.ops.bevel(bm, geom=edges, offset=chamfer, offset_type="OFFSET", segments=1, affect="EDGES",
                        clamp_overlap=True)


def side():
    bm = bmesh.new()
    # Back plate: closes the joints and anything behind the blocks.
    pts = [Y0] + [k for k in KINKS] + [Y1]
    back = [bm.verts.new((X + 0.002, y, FLOOR)) for y in pts] + \
           [bm.verts.new((X + 0.002, y, top(y))) for y in reversed(pts)]
    bm.faces.new(back)
    # Plinth.
    box(bm, X, X + PLINTH_OUT, Y0 - 0.1, Y1 + 0.0, FLOOR, lambda y: PLINTH_TOP, 0.05)
    # Pilasters: base, shaft, capital (their tops follow the rake under the string course).
    zones = []
    for yc in PILASTERS:
        a, b = yc - PIL_W / 2, yc + PIL_W / 2
        zones.append((a, b))
        ys = sorted({a, b} | {k for k in KINKS if a < k < b})
        for ya, yb in zip(ys, ys[1:]):
            box(bm, X, X + PIL_OUT + 0.06, ya - 0.06 * (ya == a), yb + 0.06 * (yb == b), PLINTH_TOP,
                lambda y: PLINTH_TOP + 0.45, 0.03)
            box(bm, X, X + PIL_OUT, ya, yb, PLINTH_TOP + 0.45, lambda y: under_string(y) - 0.22, 0.02)
            box(bm, X, X + PIL_OUT + 0.08, ya - 0.08 * (ya == a), yb + 0.08 * (yb == b),
                min(under_string(ya), under_string(yb)) - 0.22, lambda y: under_string(y), 0.02)
    # Ashlar courses between the pilasters, staggered, each block cut to the rake.
    bays = []
    edges = [Y0] + [z for p in zones for z in p] + [Y1]
    for a, b in zip(edges[::2], edges[1::2]):
        if b - a > 0.05:
            bays.append((a, b))
    n_blocks = 0
    z, row = PLINTH_TOP, 0
    while z < top(Y1):
        for a, b in bays:
            cuts = [a]
            y = a + (BLOCK / 2 if row % 2 else 0) + random.uniform(-.1, .1)
            while True:
                y += BLOCK + random.uniform(-.12, .12)
                if y >= b - 0.35:
                    break
                cuts.append(y)
            cuts += [k for k in KINKS if a < k < b]
            cuts = sorted(cuts) + [b]
            for ya, yb in zip(cuts, cuts[1:]):
                ya2, yb2 = ya + JOINT / 2, yb - JOINT / 2
                zt = lambda y, z=z: min(z + COURSE - JOINT / 2, under_string(y) - JOINT)
                # Cut the block back to where it is at least 0.1 m tall.
                if zt(ya2) - z < 0.1 and zt(yb2) - z < 0.1:
                    continue
                for _ in range(2):
                    if zt(ya2) - z < 0.1:
                        lo_, hi_ = ya2, yb2
                        for _ in range(30):
                            m = (lo_ + hi_) / 2
                            lo_, hi_ = (m, hi_) if zt(m) - z < 0.1 else (lo_, m)
                        ya2 = hi_
                    if zt(yb2) - z < 0.1:
                        lo_, hi_ = ya2, yb2
                        for _ in range(30):
                            m = (lo_ + hi_) / 2
                            lo_, hi_ = (lo_, m) if zt(m) - z < 0.1 else (m, hi_)
                        yb2 = lo_
                if yb2 - ya2 < 0.15:
                    continue
                box(bm, X, X + PROUD + random.uniform(-.012, .012), ya2, yb2, z + JOINT / 2, zt, 0.022)
                n_blocks += 1
        z += COURSE; row += 1
    # String course: a moulding swept up the rake.
    prof = [(0, 0), (0.30, 0), (0.30, -0.05), (0.24, -0.09), (0.19, -0.17), (0.23, -0.23), (0.23, -0.29),
            (0.12, -STRING_H), (0, -STRING_H)]
    path = [Y0 - 0.12] + KINKS + [Y1]
    rings = [[bm.verts.new((X + dx, y, top(min(max(y, Y0), Y1)) + dz)) for dx, dz in prof] for y in path]
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(len(prof) - 1):
            bm.faces.new((r0[i], r0[i + 1], r1[i + 1], r1[i]))
    bm.faces.new(rings[0]); bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    print("SIDE blocks", n_blocks)
    return bm


east = side()
west = east.copy()
for v in west.verts:
    v.co.x = -v.co.x
bmesh.ops.reverse_faces(west, faces=west.faces[:])
for bm, name in ((east, "Stair_wall_E"), (west, "Stair_wall_W")):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
    print("WALL", name, len(me.polygons), "faces")

# v9's stair balustrades kept the blockout rails' solid panels down to -5.8 (in front of the new wall):
# cut what hangs below the balustrade's foot.
bal = bpy.data.objects["Balustrades"]
bm = bmesh.new(); bm.from_mesh(bal.data)
mw = bal.matrix_world
dead = []
for f in bm.faces:
    c = mw @ f.calc_center_median()
    if 7.9 <= abs(c.x) <= 8.45 and Y0 <= c.y <= Y1 + 2 and c.z < nose(c.y) - 0.2:
        dead.append(f)
bmesh.ops.delete(bm, geom=dead, context="FACES")
bm.to_mesh(bal.data); bm.free()
print("BALUSTRADE panel faces cut", len(dead))

# v9's moulding round the middle landing ran where the string course now runs.
m = bpy.data.objects.get("Middle_landing moulding")
if m:
    for c in list(m.users_collection):
        c.objects.unlink(m)
    originals.objects.link(m)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v14.blend"))
