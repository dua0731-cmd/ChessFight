# The grand stair's side walls and the blockout faces at its two ends, in the library conveyor map's wall
# (PawnRushLibraryEnvironment.fbx: Side_wall_staggered_limestone + Continuous_side_wall_molding): 0.84 m
# square stones on a 0.86 m pitch (2 cm joints), 0.18 m thick with 1.5 cm chamfers, every row half a
# stone along; plain moulding bands standing 8 cm proud of the stones - 0.30 m at the floor, 0.13 m 4 m up,
# and a double band (0.18 + 0.14 m) at the top, here running up the stair's rake under the balustrade.
# Replaces v14's ashlar (Stair_wall_E/W). Faces dressed:
#   stair sides x = +-8 (y 14..51, hall floor -4 up to the balustrade's foot),
#   the foyer floor's north face y = 14 (|x| 8..20, up to its moulding at -0.48),
#   the central landing's south face y = 51 (|x| 8..14) and its sides x = +-14 (up to its moulding at 7.52).
# Everything stands outside the blockout planes, so the walked surfaces are untouched. No paint: Arch_Clay.
# Run: blender -b grand_stair_hall_v14.blend -P library_walls_v15.py   -> grand_stair_hall_v15.blend
import bpy, bmesh, math, os
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
STONE, PITCH, THICK, CHAMFER = 0.84, 0.86, 0.18, 0.015
BAND_BACK, BAND_FRONT = 0.10, 0.26          # bands from inside the stones to 8 cm proud of their face
FLOOR = -4.0

CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Library_walls_v15"); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]


def nose(y):
    if y <= 29:
        return 0.25 * (y - 13)
    if y <= 35:
        return 4.0
    return 4.0 + 0.25 * (y - 35)


class Face:
    """A wall face: world point = base + u*U + w*N + z*Z, N pointing out of the blockout."""
    def __init__(self, base, U, N, u0, u1, top, kinks=()):
        self.base, self.U, self.N = Vector(base), Vector(U), Vector(N)
        self.u0, self.u1, self.top, self.kinks = u0, u1, top, list(kinks)

    def p(self, u, w, z):
        return self.base + self.U * u + self.N * w + Vector((0, 0, z))


def box(bm, f, ua, ub, wa, wb, zbot, ztop, chamfer):
    """A block on face f, its bottom and top following zbot(u) and ztop(u) (straight between ua and ub)."""
    vs = {}
    for i, u in enumerate((ua, ub)):
        for j, w in enumerate((wa, wb)):
            vs[i, j, 0] = bm.verts.new(f.p(u, w, zbot(u)))
            vs[i, j, 1] = bm.verts.new(f.p(u, w, ztop(u)))
    quads = [[(0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)], [(1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0)],
             [(0, 0, 0), (0, 0, 1), (1, 0, 1), (1, 0, 0)], [(0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)],
             [(0, 0, 1), (0, 1, 1), (1, 1, 1), (1, 0, 1)], [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)]]
    faces = [bm.faces.new([vs[k] for k in q]) for q in quads]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    if chamfer:
        bmesh.ops.bevel(bm, geom=list({e for fc in faces for e in fc.edges}), offset=chamfer,
                        offset_type="OFFSET", segments=1, affect="EDGES", clamp_overlap=True)


def clip(f, ua, ub, ok):
    """Shrink [ua, ub] to where ok(u) holds (ok is monotonic across a kink-free span); None if nowhere."""
    if not ok(ua) and not ok(ub):
        return None
    for side in (0, 1):
        if not ok(ua if side == 0 else ub):
            lo_, hi_ = ua, ub
            for _ in range(30):
                m = (lo_ + hi_) / 2
                if side == 0:
                    lo_, hi_ = (m, hi_) if not ok(m) else (lo_, m)
                else:
                    lo_, hi_ = (lo_, m) if not ok(m) else (m, hi_)
            if side == 0:
                ua = hi_
            else:
                ub = lo_
    return (ua, ub) if ub - ua > 0.12 else None


def dress(bm, f, mid_band=True):
    n = 0
    # Backing: closes the joints and hides the blockout behind.
    pts = sorted({f.u0, f.u1, *[k for k in f.kinks if f.u0 < k < f.u1]})
    back = [bm.verts.new(f.p(u, 0.01, FLOOR)) for u in pts] + [bm.verts.new(f.p(u, 0.01, f.top(u))) for u in reversed(pts)]
    face = bm.faces.new(back)
    face.normal_update()
    if face.normal.dot(f.N) < 0:
        face.normal_flip()
    # Stones, row by row, each row half a stone along from the last.
    z, row = FLOOR, 0
    while z < max(f.top(u) for u in pts):
        u = f.u0 - (PITCH / 2 if row % 2 else 0)
        cuts = []
        while u < f.u1:
            cuts.append(u); u += PITCH
        for ua in cuts:
            a, b = max(ua + (0.01 if ua > f.u0 else 0), f.u0), min(ua + STONE + 0.01, f.u1)
            spans = sorted({a, b, *[k for k in f.kinks if a < k < b]})
            for sa, sb in zip(spans, spans[1:]):
                zt = lambda u, z=z: min(z + STONE, f.top(u) - 0.01)
                span = clip(f, sa, sb, lambda u, z=z: zt(u) - z > 0.12)
                if span is None:
                    continue
                box(bm, f, span[0], span[1], 0, THICK, lambda u, z=z: z, zt, CHAMFER)
                n += 1
        z += PITCH; row += 1
    # Moulding bands (plain, like the library's): floor, 4 m up (the foyer's floor level), top double band.
    segs = list(zip(pts, pts[1:]))
    for sa, sb in segs:
        box(bm, f, sa, sb, BAND_BACK, BAND_FRONT, lambda u: FLOOR, lambda u: FLOOR + 0.30, 0)
        if mid_band:
            span = clip(f, sa, sb, lambda u: f.top(u) - 0.32 > 0.03 + 0.05)
            if span:
                box(bm, f, span[0], span[1], BAND_BACK, BAND_FRONT, lambda u: -0.105, lambda u: 0.025, 0)
        box(bm, f, sa, sb, BAND_BACK, BAND_FRONT, lambda u: f.top(u) - 0.32, lambda u: f.top(u) - 0.14, 0)
        box(bm, f, sa, sb, BAND_BACK, BAND_FRONT - 0.02, lambda u: f.top(u) - 0.14, lambda u: f.top(u), 0)
    return n


T_STAIR = lambda y: nose(y) + 0.1
FACES = {
    # Stair sides (u = y), ending inside the foyer's and the landing's dressed faces.
    "Stair_side_E": (Face((8, 0, 0), (0, 1, 0), (1, 0, 0), 14 + THICK, 51 - THICK, T_STAIR, (29, 35)), True),
    "Stair_side_W": (Face((-8, 0, 0), (0, 1, 0), (-1, 0, 0), 14 + THICK, 51 - THICK, T_STAIR, (29, 35)), True),
    # The foyer floor's north face, either side of the stair (u = x); its top is under the foyer moulding.
    "Foyer_north_E": (Face((0, 14, 0), (1, 0, 0), (0, 1, 0), 8, 20, lambda u: -0.48), False),
    "Foyer_north_W": (Face((0, 14, 0), (1, 0, 0), (0, 1, 0), -20, -8, lambda u: -0.48), False),
    # The central landing: its south face either side of the stair, and its sides up to the spiral's foot.
    "Landing_south_E": (Face((0, 51, 0), (1, 0, 0), (0, -1, 0), 8, 14 + THICK, lambda u: 7.52), True),
    "Landing_south_W": (Face((0, 51, 0), (1, 0, 0), (0, -1, 0), -14 - THICK, -8, lambda u: 7.52), True),
    "Landing_side_E": (Face((14, 0, 0), (0, 1, 0), (1, 0, 0), 51, 63.5, lambda u: 7.52), True),
    "Landing_side_W": (Face((-14, 0, 0), (0, 1, 0), (-1, 0, 0), 51, 66, lambda u: 7.52), True),
}

for name in ("Stair_wall_E", "Stair_wall_W"):
    o = bpy.data.objects.get(name)
    if o:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        originals.objects.link(o)

for name, (f, mid) in FACES.items():
    bm = bmesh.new()
    n = dress(bm, f, mid)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    print("FACE", name, n, "stones,", len(me.polygons), "faces")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v15.blend"))
