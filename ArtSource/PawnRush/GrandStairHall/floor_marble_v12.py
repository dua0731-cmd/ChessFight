# The hall's flat walked floors as polished marble paving, modelled (no texture, no colour): 1 m slabs with
# 6 mm joints and softly rounded top edges, a 12 cm band every 4 m (a big grid like a palace hall's), a hair
# of unevenness from slab to slab, and on the king's round dais rings of curved slabs around a centre stone.
# Slab tops are the blockout tops (the box underneath is lowered by the slab thickness), so the play
# doesn't change. One material for all of it: Marble_Clay, a light grey clay with a polished sheen; the
# stair treads (v11) take it too. Curved strips (spiral, dive landings, windy way, last stair) and the
# floating stones keep their shapes.
# Run: blender -b grand_stair_hall_v11.blend -P floor_marble_v12.py   -> grand_stair_hall_v12.blend
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
THICK = 0.03          # slab thickness
JOINT = 0.006
BAND = 0.12           # the band every BAND_EVERY metres
BAND_EVERY = 4
random.seed(12)

sc = bpy.context.scene
MARBLE = bpy.data.materials.new("Marble_Clay")
MARBLE.diffuse_color = (0.86, 0.86, 0.85, 1); MARBLE.roughness = 0.12; MARBLE.metallic = 0.0
col = bpy.data.collections.new("Floors_v12"); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]

BOXES = ["B1_GrandStair/Foyer_floor", "B1_GrandStair/Porch_floor", "B1_GrandStair/Middle_landing",
         "B2_CentralLanding/Central_landing", "B3_Spiral/Spiral_foot", "B3_Spiral/Onto_the_dais",
         "B6_RoyalBalcony/Royal_balcony", "B6_RoyalBalcony/Runout", "B6_RoyalBalcony/Exit_terrace",
         "B6_RoyalBalcony/Step_down_1", "B6_RoyalBalcony/Step_down_2", "B6_RoyalBalcony/Step_down_3",
         "B6_RoyalBalcony/Step_down_4", "B6_RoyalBalcony/Mini-game_entrance_band"]
DAIS = "B5_RoyalDais/Royal_dais"


def retire(ob):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    originals.objects.link(ob)


def corners(ob):
    vs = [ob.matrix_world @ v.co for v in ob.data.vertices]
    return Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)])


def lower(name):
    """The blockout piece stays (hidden with the originals); a copy with its top THICK lower carries it."""
    ob = bpy.data.objects[name]
    lo, hi = corners(ob)
    cp = ob.copy(); cp.data = ob.data.copy(); cp.name = name + " (under marble)"
    mat = cp.data.materials[0].copy(); mat.name = "Section_Floor_under"   # same look, not counted as a walk top
    cp.data.materials[0] = mat
    inv = cp.matrix_world.inverted()
    for v in cp.data.vertices:
        w = cp.matrix_world @ v.co
        if w.z > hi.z - 0.01:
            w.z -= THICK; v.co = inv @ w
    for c in ob.users_collection:
        c.objects.link(cp)
    retire(ob)
    return lo, hi


def finish_slab(bm, top):
    tops = [e for e in bm.edges if all(abs(v.co.z - top) < 1e-5 for v in e.verts)]
    bmesh.ops.bevel(bm, geom=tops, offset=0.004, offset_type="OFFSET", segments=2, profile=0.5,
                    affect="EDGES", clamp_overlap=True)
    c = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    tilt = Matrix.Rotation(math.radians(random.uniform(-.03, .03)), 4, "X") @ Matrix.Rotation(
        math.radians(random.uniform(-.03, .03)), 4, "Y")
    bm.transform(Matrix.Translation(c + Vector((0, 0, random.uniform(-.0012, 0)))) @ tilt @ Matrix.Translation(-c))


def rect_slab(x0, x1, y0, y1, top):
    bm = bmesh.new()
    r = bmesh.ops.create_cube(bm, size=1)
    for v in r["verts"]:
        v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, top - THICK if v.co.z < 0 else top))
    finish_slab(bm, top)
    return bm


ACC = {}


def add(bm, key):
    me = bpy.data.meshes.new("_t"); bm.to_mesh(me); bm.free()
    ACC.setdefault(key, bmesh.new()).from_mesh(me); bpy.data.meshes.remove(me)


# ---------------------------------------------------------------- rectangular floors: a global 1 m grid
rects = []                                   # (x0, x1, y0, y1, top) walked tops to pave
for n in BOXES:
    lo, hi = lower(n)
    rects.append((lo.x, hi.x, lo.y, hi.y, hi.z, n.split("/")[0]))
# The stair flights (v11 treads) own their own tops: no paving there.
stairs = [corners(o) for o in originals.all_objects if "_flight_step_" in o.name]
stairs = [(lo.x, hi.x, lo.y, hi.y, hi.z) for lo, hi in stairs]


def covered(x, y, z):
    """The rects (same top) that contain point x, y; None where a stair step owns it."""
    for a in stairs:
        if a[0] - 1e-6 <= x <= a[1] + 1e-6 and a[2] - 1e-6 <= y <= a[3] + 1e-6 and abs(a[4] - z) < .02:
            return None
    return [r for r in rects if r[0] <= x <= r[1] and r[2] <= y <= r[3] and abs(r[4] - z) < .02]


def onband(c):
    return abs(c / BAND_EVERY - round(c / BAND_EVERY)) < 1e-6


done = set()
count = 0
for r in rects:
    x0, x1, y0, y1, z, sect = r
    for ix in range(math.floor(x0), math.ceil(x1)):
        for iy in range(math.floor(y0), math.ceil(y1)):
            key = (ix, iy, round(z, 2))
            if key in done:
                continue
            # The part of this cell inside the paved area (a union of rects, approximated by sampling).
            pts = [(ix + (i + .5) / 8, iy + (j + .5) / 8) for i in range(8) for j in range(8)]
            inside = [p for p in pts if covered(p[0], p[1], z)]
            if not inside:
                continue
            done.add(key)
            owners = set()
            for p in inside:
                owners.update(covered(p[0], p[1], z))
            cx0 = max(ix, min(o[0] for o in owners)); cx1 = min(ix + 1, max(o[1] for o in owners))
            cy0 = max(iy, min(o[2] for o in owners)); cy1 = min(iy + 1, max(o[3] for o in owners))
            # Stair steps that cut into the cell: keep the side away from them.
            for a in stairs:
                if abs(a[4] - z) < .02 and a[0] < cx1 and cx0 < a[1] and a[2] < cy1 and cy0 < a[3]:
                    if a[3] < cy1 - 1e-6:
                        cy0 = max(cy0, a[3] + 0.04)       # under the last tread's nose
                    elif a[2] > cy0 + 1e-6:
                        cy1 = min(cy1, a[2])
            # Joints: half a joint off each inner side, half a band off band lines; flush on the floor's edge.
            def inset(lo_, hi_, a_, b_):
                lo2 = a_ if lo_ > a_ + 1e-6 else (a_ + (BAND / 2 if onband(a_) else JOINT / 2))
                hi2 = b_ if hi_ < b_ - 1e-6 else (b_ - (BAND / 2 if onband(b_) else JOINT / 2))
                return lo2, hi2
            sx0, sx1 = inset(min(o[0] for o in owners), max(o[1] for o in owners), cx0, cx1)
            sy0, sy1 = inset(min(o[2] for o in owners), max(o[3] for o in owners), cy0, cy1)
            if sx1 - sx0 < .05 or sy1 - sy0 < .05:
                continue
            add(rect_slab(sx0, sx1, sy0, sy1, z), sect); count += 1
            # Band pieces on this cell's west and south band lines (and the square where two meet).
            for along_x in (True, False):
                line = cx0 if along_x else cy0
                mx, my = ((line, (sy0 + sy1) / 2) if along_x else ((sx0 + sx1) / 2, line))
                if not onband(line) or not (covered(mx - .01 * along_x, my - .01 * (not along_x), z)
                                            and covered(mx + .01 * along_x, my + .01 * (not along_x), z)):
                    continue                     # a band only between two paved cells, never on the edge
                if along_x:
                    a0, a1 = sy0, sy1
                    add(rect_slab(line - BAND / 2 + JOINT / 2, line + BAND / 2 - JOINT / 2, a0, a1, z), sect)
                else:
                    a0, a1 = sx0, sx1
                    add(rect_slab(a0, a1, line - BAND / 2 + JOINT / 2, line + BAND / 2 - JOINT / 2, z), sect)
            if onband(cx0) and onband(cy0) and covered(cx0 - .01, cy0 - .01, z) and covered(cx0 + .01, cy0 + .01, z):
                h = BAND / 2 - JOINT / 2
                add(rect_slab(cx0 - h, cx0 + h, cy0 - h, cy0 + h, z), sect)
print("PAVED", count, "slabs on", len(rects), "floors")


def bed(verts2d, z, key):
    """The mortar bed the slabs sit in: seen only down the joints (grey there, not the blockout's colour)."""
    bm = bmesh.new()
    bm.faces.new([bm.verts.new((x, y, z - THICK + 0.012)) for x, y in verts2d])
    add(bm, key)


for x0, x1, y0, y1, z, sect in rects:
    bed([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z, sect)

# ---------------------------------------------------------------- the round dais: rings of curved slabs
lo, hi = lower(DAIS)
cx, cy, R, z = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2, (hi.x - lo.x) / 2, hi.z
rings = [0, 1.4, 1.6, 2.8, 4.0, 4.2, 5.4, 6.6, 6.8, R]       # thin ones are bands
n_ring = 0
bed([(cx + R * math.cos(t), cy + R * math.sin(t)) for t in [2 * math.pi * i / 96 for i in range(96)]], z, "B5_RoyalDais")
for r0, r1 in zip(rings, rings[1:]):
    a0g, a1g = r0 + (JOINT / 2 if r0 else 0), r1 - (JOINT / 2 if r1 < R else 0)
    if r0 == 0:
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=48, radius1=a1g, radius2=a1g, depth=THICK)
        bm.transform(Matrix.Translation((cx, cy, z - THICK / 2)))
        finish_slab(bm, z); add(bm, "B5_RoyalDais"); continue
    pieces = max(8, round(2 * math.pi * (r0 + r1) / 2 / (1.3 if r1 - r0 > .5 else 3.0)))
    for k in range(pieces):
        t0 = 2 * math.pi * k / pieces + JOINT / 2 / ((r0 + r1) / 2)
        t1 = 2 * math.pi * (k + 1) / pieces - JOINT / 2 / ((r0 + r1) / 2)
        seg = max(2, round((t1 - t0) * r1 / 0.15))
        bm = bmesh.new()
        ring = []
        for s in range(seg + 1):
            t = t0 + (t1 - t0) * s / seg
            ring.append((math.cos(t), math.sin(t)))
        outline = [(a1g * c, a1g * s_) for c, s_ in ring] + [(a0g * c, a0g * s_) for c, s_ in reversed(ring)]
        bot = [bm.verts.new((cx + x, cy + y, z - THICK)) for x, y in outline]
        top = [bm.verts.new((cx + x, cy + y, z)) for x, y in outline]
        bm.faces.new(top); bm.faces.new(list(reversed(bot)))
        for i in range(len(outline)):
            j = (i + 1) % len(outline)
            bm.faces.new((bot[i], bot[j], top[j], top[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        finish_slab(bm, z); add(bm, "B5_RoyalDais"); n_ring += 1
print("DAIS", n_ring, "ring slabs, radius", round(R, 2))

for key, bm in ACC.items():
    me = bpy.data.meshes.new(f"{key}/Marble_floor"); bm.to_mesh(me); bm.free()
    me.materials.append(MARBLE)
    ob = bpy.data.objects.new(f"{key}/Marble_floor", me); col.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = False
    print("FLOOR", ob.name, len(me.polygons), "faces")

# The stair treads in the same marble.
for o in bpy.data.collections["Stairs_v11"].objects:
    if o.name.endswith("_treads"):
        o.data.materials[0] = MARBLE
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v12.blend"))
