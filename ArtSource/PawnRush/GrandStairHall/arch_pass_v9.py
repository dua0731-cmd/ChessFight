# Architecture pass on the grand stair hall (grand_stair_hall_v2.blend -> grand_stair_hall_v9.blend).
# Starts again from the v2 shape pass (our blockout); the Tripo hall (v3~v8) is withdrawn.
# Look: ref_hall_look.webp for shapes only. Modelling only: no textures and no painted colours or patterns
# (승규 11-03) — every new piece is one plain clay material, the blockout keeps its own colours.
# Walked tops keep their heights; what a piece replaces goes to the hidden "_Blockout originals".
#   - the hall's outer walls (E, W, south) only: wall_module.blend (castle arch module, banner cut off), one row,
#     each module stretched to the wall's full height; porch, foyer and the round wall stay blockout
# Run: blender -b grand_stair_hall_v2.blend -P arch_pass_v9.py
import bpy, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(bpy.data.filepath)
HUB = Vector((0.0, 92.0))
root = bpy.data.collections["Mid_2-1_GrandStairHall"]
originals = bpy.data.collections["_Blockout originals"]
arch = bpy.data.collections.new("Arch_v9")
root.children.link(arch)


CLAY = bpy.data.materials.new("Arch_Clay")       # plain untextured grey, the only material of the new pieces
CLAY.diffuse_color = (0.72, 0.72, 0.72, 1)
STONE = CLAY


def retire(ob):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    originals.objects.link(ob)


def corners(ob):
    vs = [ob.matrix_world @ v.co for v in ob.data.vertices]
    return Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)])


# ---------------------------------------------------------------- outer walls
with bpy.data.libraries.load(os.path.join(HERE, "wall_module.blend")) as (src, dst):
    dst.meshes = ["WallModule"]
module = dst.meshes[0]
MV = [v.co.copy() for v in module.vertices]
MX0, MX1 = min(v.x for v in MV), max(v.x for v in MV)
MY0, MY1 = min(v.y for v in MV), max(v.y for v in MV)          # front (inner face) = MY0
MZ1 = max(v.z for v in MV)
MW, MD = MX1 - MX0, MY1 - MY0


def wall_mesh(name, place, length, f, t, depth_of):
    """One row of modules along the wall, each stretched up to the wall's full height (승규 11-03: no stacking);
    `place(u, d, z)` -> world (u along, d outward from the inner face)."""
    verts, faces = [], []
    polys = [list(p.vertices) for p in module.polygons]
    h = t - f
    s = (h / 2 if h > 16 else h) / MZ1          # across and in depth: sized as for a half-height module
    n = max(1, round(length / (MW * s)))
    w = length / n
    d_scale = depth_of(s) / MD
    for k in range(n):
        base = len(verts)
        for v in MV:
            u = k * w + (v.x - MX0) / MW * w
            verts.append(place(u, (v.y - MY0) * d_scale, f + v.z / MZ1 * h))
        faces += [[base + i for i in p] for p in polys]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.materials.append(STONE)
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    arch.objects.link(ob)
    return ob


# Only the hall's outer walls take the module (승규 11-03): the porch and foyer walls in front and the
# round wall inside stay blockout.
# name -> (inner normal into the hall, floor z at its foot)
STRAIGHT = {
    "Hall/Hall_wall_E": ((-1, 0), -4.0),
    "Hall/Hall_wall_W": ((1, 0), -4.0),
    "Hall/Hall_wall_S_(east)": ((0, 1), -4.0),
    "Hall/Hall_wall_S_(west)": ((0, 1), -4.0),
}
walls = 0
for name, (nrm, f) in STRAIGHT.items():
    ob = bpy.data.objects[name]
    lo, hi = corners(ob)
    N = Vector((nrm[0], nrm[1], 0))
    out = -N
    T = out.cross(Vector((0, 0, 1)))                   # right-handed: along x out = up
    inner = hi if (nrm[0] + nrm[1]) > 0 else lo          # the inner face's coordinate
    if nrm[0]:
        a, b = (lo.y, hi.y) if T.y > 0 else (hi.y, lo.y)
        start = Vector((inner.x, a, 0))
        length = hi.y - lo.y
    else:
        a, b = (lo.x, hi.x) if T.x > 0 else (hi.x, lo.x)
        start = Vector((a, inner.y, 0))
        length = hi.x - lo.x
    place = lambda u, d, z, S=start, T=T, O=out: S + T * u + O * d + Vector((0, 0, z))
    # Outside the hall nobody walks: the module keeps most of its depth, growing outwards.
    wall_mesh(name.split("/")[1] + " (arch)", place, length, f, hi.z, lambda s: MD * s * 0.75)
    retire(ob)
    walls += 1
print("WALLS", walls)

MARBLE = MARBLE_D = TRIM = GOLD = CLAY          # names kept to say what each part would be made of later


# ---------------------------------------------------------------- mesh helpers
class Mesh:
    def __init__(self):
        self.v, self.f, self.m, self.mats = [], [], [], []

    def mat(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def face(self, pts, m):
        b = len(self.v)
        self.v += [tuple(p) for p in pts]
        self.f.append(list(range(b, b + len(pts))))
        self.m.append(self.mat(m))

    def rings(self, rings, m, cap=True, closed=False):
        """Closed polygons (same count) joined one after the other; caps at both ends."""
        b = len(self.v)
        n = len(rings[0])
        for r in rings:
            self.v += [tuple(p) for p in r]
        k = self.mat(m)
        last = len(rings) if closed else len(rings) - 1
        for i in range(last):
            i2 = (i + 1) % len(rings)
            for j in range(n):
                self.f.append([b + i * n + j, b + i2 * n + j, b + i2 * n + (j + 1) % n, b + i * n + (j + 1) % n])
                self.m.append(k)
        if cap and not closed:
            self.f.append([b + j for j in reversed(range(n))]); self.m.append(k)
            self.f.append([b + (len(rings) - 1) * n + j for j in range(n)]); self.m.append(k)

    def box(self, lo, hi, m):
        lo, hi = Vector(lo), Vector(hi)
        self.rings([[(lo.x, lo.y, z), (hi.x, lo.y, z), (hi.x, hi.y, z), (lo.x, hi.y, z)] for z in (lo.z, hi.z)], m)

    def lathe(self, centre, profile, m, seg=24, phase=0.0):
        """profile: (r, z) from bottom to top; r = 0 ends make points."""
        c = Vector(centre)
        rings = [[(c.x + r * math.cos(phase + 2 * math.pi * j / seg), c.y + r * math.sin(phase + 2 * math.pi * j / seg), c.z + z)
                  for j in range(seg)] for r, z in profile]
        self.rings(rings, m, cap=True)

    def sweep(self, frames, profile, m):
        """frames: (origin, side, up); profile: closed (s, t) polygon -> origin + side*s + up*t."""
        self.rings([[o + sd * s + up * t for s, t in profile] for o, sd, up in frames], m)

    def build(self, name, col=None, smooth=35):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        for mm in self.mats:
            me.materials.append(mm)
        for p, k in zip(me.polygons, self.m):
            p.material_index = k
        me.validate()
        ob = bpy.data.objects.new(name, me)
        (col or arch).objects.link(ob)
        if smooth:
            me.shade_smooth()
            me.set_sharp_from_angle(angle=math.radians(smooth))
        return ob


def flip_fix(ob):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(ob.data); bm.free()


def finish(mesh, name, col=None, smooth=35):
    ob = mesh.build(name, col, smooth)
    flip_fix(ob)
    return ob


# ---------------------------------------------------------------- platform edges: a moulded band under the walked edge
def slab(name):
    """The blockout box stays as it is (its own colour); only the moulding is added around its sides."""
    ob = bpy.data.objects[name]
    lo, hi = corners(ob)
    mesh = Mesh()
    z0, z1 = lo.z, hi.z
    if z1 - z0 > 1.5:
        # a moulded band just under the walked edge (its top 0.12 below, so the edge stays where it was)
        prof = [(0, 0), (0.06, 0.02), (0.1, 0.1), (0.14, 0.2), (0.14, 0.32), (0, 0.36)]
        prof = [(s, z1 - 0.12 - 0.36 + t) for s, t in prof]
        pts = [Vector((lo.x, lo.y, 0)), Vector((hi.x, lo.y, 0)), Vector((hi.x, hi.y, 0)), Vector((lo.x, hi.y, 0))]
        for i in range(4):
            p, q = pts[i], pts[(i + 1) % 4]
            d = (q - p).normalized()
            out = Vector((d.y, -d.x, 0))
            mesh.sweep([(p - d * 0.14, out, Vector((0, 0, 1))), (q + d * 0.14, out, Vector((0, 0, 1)))],
                       prof, TRIM)
    return finish(mesh, ob.name.split("/")[1] + " moulding")


for n in ("B1_GrandStair/Foyer_floor", "B2_CentralLanding/Central_landing", "B6_RoyalBalcony/Royal_balcony",
          "B1_GrandStair/Middle_landing", "B3_Spiral/Spiral_foot", "B6_RoyalBalcony/Runout",
          "B6_RoyalBalcony/Exit_terrace", "B6_RoyalBalcony/Mini-game_entrance_band"):
    slab(n)


# ---------------------------------------------------------------- foyer columns
def column(c, h, r, m=STONE):
    col = Mesh()
    sq = r * 1.18
    col.box((c.x - sq, c.y - sq, c.z), (c.x + sq, c.y + sq, c.z + 0.35), TRIM)                        # plinth
    col.lathe(c, [(r * 1.08, 0.35), (r * 1.1, 0.45), (r * 0.98, 0.55), (r * 1.02, 0.62), (r * 0.92, 0.7)], TRIM, 32)
    flutes, seg = 16, 64
    rings = []
    for z in [0.7 + (h - 1.6) * t / 8 for t in range(9)]:
        ent = 1 - 0.08 * ((z - 0.7) / (h - 1.6)) ** 1.5                                                 # entasis
        rings.append([(c.x + rr * math.cos(a), c.y + rr * math.sin(a), c.z + z) for a, rr in
                      ((2 * math.pi * j / seg, r * 0.92 * ent * (0.94 if j % (seg // flutes) in (1, 2, 3) else 1.0))
                       for j in range(seg))])
    col.rings(rings, m)
    top = h - 0.9
    col.lathe(c, [(r * 0.86, top), (r * 0.9, top + 0.08), (r * 0.86, top + 0.14), (r * 0.9, top + 0.3),
                  (r * 1.15, top + 0.55), (r * 1.2, top + 0.6)], GOLD, 32)
    col.box((c.x - sq * 1.1, c.y - sq * 1.1, c.z + h - 0.3), (c.x + sq * 1.1, c.y + sq * 1.1, c.z + h), TRIM)    # abacus
    return col


cols = Mesh()
for o in [o for o in bpy.data.objects if o.name.startswith("B1_GrandStair/Foyer_pillar")]:
    lo, hi = corners(o)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    m = column(c, hi.z - lo.z, (hi.x - lo.x) / 2 * 0.85)
    off = len(cols.v)
    cols.v += m.v
    for f, k in zip(m.f, m.m):
        cols.f.append([i + off for i in f]); cols.m.append(cols.mat(m.mats[k]))
    retire(o)
finish(cols, "Foyer columns")


# ---------------------------------------------------------------- balustrades
def plan_axis(lo, hi):
    return 0 if hi.x - lo.x > hi.y - lo.y else 1


def balustrade(ob, mesh, height=1.1):
    lo, hi = corners(ob)
    ax = plan_axis(lo, hi)
    other = 1 - ax
    mid = (lo[other] + hi[other]) / 2
    half = (hi[other] - lo[other]) / 2
    pts = {(round(p[ax], 3), round(p.z, 3)) for p in (ob.matrix_world @ v.co for v in ob.data.vertices)}
    pts = list(pts)
    cu = sum(p[0] for p in pts) / len(pts); cz = sum(p[1] for p in pts) / len(pts)
    pts.sort(key=lambda p: math.atan2(p[1] - cz, p[0] - cu))
    edges = [(pts[i], pts[(i + 1) % len(pts)]) for i in range(len(pts))]
    top = max((e for e in edges if abs(e[1][0] - e[0][0]) > abs(e[1][1] - e[0][1])), key=lambda e: e[0][1] + e[1][1])
    (ua, za), (ub, zb) = sorted(top)
    bot = min((e for e in edges if abs(e[1][0] - e[0][0]) > abs(e[1][1] - e[0][1])), key=lambda e: e[0][1] + e[1][1])
    (va, wa), (vb, wb) = sorted(bot)

    def P(u, z, s=0.0):
        p = [0.0, 0.0, z]
        p[ax] = u
        p[other] = mid + s
        return Vector(p)

    def zt(u):
        return za + (zb - za) * (u - ua) / (ub - ua)

    zb_ = lambda u: zt(u) - height
    # solid below the balustrade where the blockout rail reached further down (stair sides, landing sides)
    if min(wa, wb) < min(za, zb) - height - 0.3:
        quad = [(ua, zb_(ua)), (ub, zb_(ub)), (vb, wb), (va, wa)]
        mesh.rings([[P(u, z, -half) for u, z in quad], [P(u, z, half) for u, z in quad]], STONE)
    up = Vector((0, 0, 1))
    side = P(0, 0, 1) - P(0, 0, 0)
    a, b = P(ua, za), P(ub, zb)
    # base rail, hand rail
    base = [(-half - 0.02, 0), (half + 0.02, 0), (half + 0.02, 0.12), (half - 0.03, 0.18), (-half + 0.03, 0.18), (-half - 0.02, 0.12)]
    mesh.sweep([(a - up * height, side, up), (b - up * height, side, up)], base, STONE)
    hand = [(-half - 0.05, -0.16), (half + 0.05, -0.16), (half + 0.05, -0.06), (half - 0.02, 0.0), (-half + 0.02, 0.0), (-half - 0.05, -0.06)]
    mesh.sweep([(a, side, up), (b, side, up)], hand, STONE)
    mesh.sweep([(a - up * 0.155, side, up), (b - up * 0.155, side, up)],
               [(-half - 0.06, -0.025), (half + 0.06, -0.025), (half + 0.06, 0.0), (-half - 0.06, 0.0)], GOLD)
    # balusters (upright) and posts
    L = ub - ua
    n = max(1, int(L / 0.32))
    hb = height - 0.18 - 0.16
    r = min(half, 0.14)
    for i in range(n):
        u = ua + L * (i + 0.5) / n
        z0 = zb_(u) + 0.18
        k = hb
        mesh.lathe(P(u, z0), [(r * 0.55, 0), (r * 0.75, k * 0.06), (r * 0.5, k * 0.14), (r * 0.62, k * 0.3), (r, k * 0.5),
                              (r * 0.6, k * 0.72), (r * 0.42, k * 0.82), (r * 0.62, k * 0.92), (r * 0.55, k)], STONE, 10)
    posts = max(1, round(L / 4.0))
    for i in range(posts + 1):
        u = ua + L * i / posts
        u = min(max(u, ua + 0.22), ub - 0.22)
        z0, z1 = zb_(u), zt(u) + 0.12
        w = half + 0.08
        p, q = P(u - 0.22, z0, -w), P(u + 0.22, z1, w)
        mesh.box([min(x, y) for x, y in zip(p, q)], [max(x, y) for x, y in zip(p, q)], STONE)
        mesh.lathe(P(u, z1), [(0.12, 0), (0.17, 0.08), (0.12, 0.22), (0.02, 0.3)], GOLD, 16)


bal = Mesh()
rails = [o for o in list(bpy.data.objects) if o.type == "MESH" and "_rail" in o.name.split("/")[-1]
         and o.name.split("/")[0] in ("B1_GrandStair", "B2_CentralLanding", "B3_Spiral", "B6_RoyalBalcony")
         and originals not in o.users_collection]
for o in rails:
    balustrade(o, bal)
    retire(o)
finish(bal, "Balustrades")
print("RAILS", len(rails))


# ---------------------------------------------------------------- portals (entrance and balcony arches)
def portal(name, pier_names, lintel_name, extra_depth=0.3):
    lin = bpy.data.objects[lintel_name]
    lo, hi = corners(lin)
    piers = [corners(bpy.data.objects[p]) for p in pier_names] if pier_names else []
    yc, depth = (lo.y + hi.y) / 2, (hi.y - lo.y) + extra_depth
    w_in = 6.0                                   # the opening's half width (both doors are 12 m)
    zl, zt = lo.z, hi.z
    z0 = min([p[0].z for p in piers] + [0.0 if not piers else 99])
    if not piers:
        z0 = 0.0
    pm = Mesh()
    ya, yb = yc - depth / 2, yc + depth / 2
    for sgn in (-1, 1):                                                                 # piers with base and capital
        x0, x1 = sorted((sgn * w_in, sgn * (w_in + 1.1)))
        pm.box((x0, ya, z0), (x1, yb, zl), STONE)
        pm.box((x0 - 0.1, ya - 0.1, z0), (x1 + 0.1, yb + 0.1, z0 + 0.6), TRIM)
        pm.box((x0 - 0.12, ya - 0.12, zl - 0.45), (x1 + 0.12, yb + 0.12, zl), GOLD)
    # arched head: a segmental intrados from (+-w, zl) to (0, zl + rise), solid up to the entablature
    rise, top = 1.6, zt + 1.4
    R = (w_in ** 2 + rise ** 2) / (2 * rise)
    cz = zl + rise - R
    xs = [w_in * (2 * i / 24 - 1) for i in range(25)]
    intra = [(x, cz + math.sqrt(R * R - x * x)) for x in xs]
    for y0, y1 in ((ya, yb),):
        # column strips between the intrados and the top (each a small closed block)
        for (xa, za), (xb, zb) in zip(intra, intra[1:]):
            pm.rings([[(xa, y, za), (xb, y, zb), (xb, y, top), (xa, y, top)] for y in (y0, y1)], STONE)
    pm.box((-w_in - 1.1, ya, zl), (-w_in, yb, top), STONE)
    pm.box((w_in, ya, zl), (w_in + 1.1, yb, top), STONE)
    # archivolt: gold band along the intrados on both faces, keystone
    for y in (ya, yb):
        s = -1 if y == ya else 1
        for (xa, za), (xb, zb) in zip(intra, intra[1:]):
            pm.rings([[(xa, y, za), (xb, y, zb), (xb, y, zb + 0.35), (xa, y, za + 0.35)],
                      [(xa, y + s * 0.08, za), (xb, y + s * 0.08, zb), (xb, y + s * 0.08, zb + 0.35), (xa, y + s * 0.08, za + 0.35)]], GOLD)
    pm.box((-0.45, ya - 0.12, zl + rise - 0.1), (0.45, yb + 0.12, zl + rise + 0.9), GOLD)
    # entablature and cornice
    pm.box((-w_in - 1.3, ya - 0.15, top), (w_in + 1.3, yb + 0.15, top + 0.5), TRIM)
    pm.box((-w_in - 1.5, ya - 0.3, top + 0.5), (w_in + 1.5, yb + 0.3, top + 0.75), STONE)
    finish(pm, name)
    for n in pier_names + [lintel_name]:
        retire(bpy.data.objects[n])


portal("Entrance portal", [], "B1_GrandStair/Entrance_arch_lintel")
portal("Balcony portal", ["B6_RoyalBalcony/Arch_pillar_E", "B6_RoyalBalcony/Arch_pillar_W"], "B6_RoyalBalcony/Arch_lintel")


# ---------------------------------------------------------------- the royal dais tower
dais = bpy.data.objects["B5_RoyalDais/Royal_dais"]
lo, hi = corners(dais)
R0, c = (hi.x - lo.x) / 2, Vector((0, 92, 0))
dm = Mesh()
dm.lathe(Vector((0, 92, -4.0)), [(R0 + 0.3, 0), (R0 + 0.3, 0.8), (R0 + 0.15, 1.0), (R0 + 0.15, 1.2), (R0, 1.25)], TRIM, 96)
top = hi.z
dm.lathe(Vector((0, 92, 0)), [(R0, top - 1.6), (R0 + 0.12, top - 1.5), (R0 + 0.12, top - 1.1), (R0 + 0.3, top - 0.8),
                              (R0 + 0.3, top - 0.55), (R0 + 0.12, top - 0.45), (R0, top - 0.12)], GOLD, 96)
for k in range(16):                                                  # ribs between base and crown
    a = 2 * math.pi * k / 16
    d, t = Vector((math.cos(a), math.sin(a), 0)), Vector((-math.sin(a), math.cos(a), 0))
    p0 = Vector((0, 92, -2.75)) + d * R0
    dm.sweep([(p0, t, d), (p0 + Vector((0, 0, top - 1.6 + 2.75)), t, d)],
             [(-0.35, -0.1), (0.35, -0.1), (0.35, 0.12), (-0.35, 0.12)], STONE)
finish(dm, "Royal dais tower (arch)")


# ---------------------------------------------------------------- king statue on the central landing
base = corners(bpy.data.objects["B2_CentralLanding/King_statue_base"])[0].z
km = Mesh()
k0 = Vector((0, 63, base))
km.lathe(k0, [(0.01, 0), (2.0, 0), (2.0, 0.35), (1.85, 0.45), (1.85, 0.7), (1.6, 0.85), (1.5, 1.15), (1.25, 1.25),
              (1.05, 1.6), (0.88, 2.4), (0.78, 3.2), (0.82, 3.35), (1.35, 3.6), (1.45, 3.75), (1.05, 3.9),
              (0.85, 4.0), (0.95, 4.6), (1.05, 4.95), (0.9, 5.05), (0.55, 5.25), (0.2, 5.35), (0.01, 5.4)], MARBLE, 48)
km.lathe(k0, [(1.48, 3.6), (1.52, 3.68), (1.48, 3.76)], GOLD, 48)
km.box((-0.14, 62.86, base + 5.3), (0.14, 63.14, base + 6.1), GOLD)
km.box((-0.42, 62.86, base + 5.62), (0.42, 63.14, base + 5.88), GOLD)
finish(km, "King statue (arch)")
for o in [o for o in bpy.data.objects if o.name.startswith("B2_CentralLanding/King_statue")]:
    retire(o)


# ---------------------------------------------------------------- chandeliers
chm = Mesh()
for o in [o for o in bpy.data.objects if o.name.startswith("Hall/Chandelier_ring")]:
    lo, hi = corners(o)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    chm.lathe(c, [(0.01, -0.9), (0.12, -0.8), (0.22, -0.5), (0.12, -0.2), (0.3, 0.0), (0.12, 0.3), (0.1, 1.0), (0.01, 1.05)], GOLD, 16)
    for r, z in ((1.25, 0.0), (0.75, 0.55)):
        seg = 48
        frames = []
        for j in range(seg + 1):
            a = 2 * math.pi * j / seg
            d = Vector((math.cos(a), math.sin(a), 0))
            frames.append((c + d * r + Vector((0, 0, z)), d, Vector((0, 0, 1))))
        chm.rings([[o_ + sd * (0.05 * math.cos(t)) + up * (0.05 * math.sin(t)) for t in [i * math.pi / 4 for i in range(8)]]
                   for o_, sd, up in frames[:-1]], GOLD, closed=True)
        for k in range(8 if r > 1 else 6):
            a = 2 * math.pi * (k + 0.5) / (8 if r > 1 else 6)
            p = c + Vector((math.cos(a) * r, math.sin(a) * r, z))
            chm.lathe(p, [(0.01, 0), (0.12, 0.02), (0.13, 0.09), (0.07, 0.1)], GOLD, 10)
            chm.lathe(p + Vector((0, 0, 0.1)), [(0.045, 0), (0.045, 0.32), (0.01, 0.34)], MARBLE, 8)
    retire(o)
for o in [o for o in bpy.data.objects if o.name.startswith("Hall/Chandelier_column")]:
    retire(o)
finish(chm, "Chandeliers")

# ---------------------------------------------------------------- floating stones: carved undersides
fs = Mesh()
for o in [o for o in bpy.data.objects if o.name.startswith("B3_Spiral/Floating_stone_")]:
    lo, hi = corners(o)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    r = (hi.x - lo.x) / 2
    fs.lathe(c, [(0.02, -1.3), (0.25, -1.1), (0.45, -0.75), (r * 0.8, -0.25), (r * 0.98, 0.0)], TRIM, 24)
finish(fs, "Floating stone keels")

lc = bpy.context.view_layer.layer_collection.children[root.name].children[originals.name]
lc.hide_viewport = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v9.blend"))
print("SAVED")
