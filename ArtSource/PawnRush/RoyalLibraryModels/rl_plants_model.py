"""Royal Library plants (seoungtest R687, 2026-11-18): the houseplants of the front gallery and the chess room, modelled
in Blender instead of Tripo (Seunggyu, 11-18: "화단 꽃 모델링 시작").

    blender -b --factory-startup --python rl_plants_model.py -- <plants> [--no-export] [--stills] [--preview <dir>]

plants: comma list of fiddle, palm, bloom, monstera, fern (or all). Every listed plant is built (the .blend keeps what
was built), exported unless --no-export, and photographed into stills/plants/ with --stills (the concept sheet crop
beside it). --preview <dir> writes the FBX under <dir> instead of the repo (rl_kit.ROOT swapped).

Reference: Docs/PawnRush/ConceptArt/R687_GiantChessLibrary/AssetRefs/GC_assets_02_plants.png
  1 fiddle-leaf fig (cream fluted pot)   2 monstera (low cream bowl, gold rim)   3 kentia palm (brass tub)
  4 fern (cream footed pot)              5 flowering shrub (cream and pink blossoms, cream gold-rim bowl)
Glossy dark-green leaves in soft masses; every leaf is a thick, bent mesh (top, underside and rim) with its midrib
fold, lateral-vein quilting and a wavy margin modelled, not a flat card. Stems are swept tubes, soil a lumpy disc
mostly hidden under the leaves.

Frames: each plant is built about its pot's base centre (Blender z up, metres), at the blockout pot's scale s (the
blockout pot is a unit r 3, h 4 pot scaled by s), then placed on its prefab host and exported in that host's frame
(rl_kit.export(..., "Plants", host)). The front gallery's pots are R4's (rl_r4_gallery_model.py PLANTERS: brass lip
inner radius 2.88 s, lid at 4.15 s): there only the soil and the plant are ours. The chess room's four come with pots.
"""
import math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rl_kit as k   # noqa: E402

ZONE = "Plants"
BLEND = os.path.join(HERE, "rl_plants.blend")
STILLS = os.path.join(HERE, "stills", "plants")
SHEET = os.path.join(k.ROOT, "Docs/PawnRush/ConceptArt/R687_GiantChessLibrary/AssetRefs/GC_assets_02_plants.png")
SMOOTH_ANGLE = math.radians(55)

# slots this script uses that rl_kit may not know yet: preview colour + roughness (the library material wins once
# rl_kit.SLOTS has the slot)
PREVIEW = {
    "leaf": ((.035, .13, .025, 1), .32), "leaf_light": ((.12, .32, .03, 1), .4), "stem": ((.07, .18, .03, 1), .55),
    "bark": ((.12, .06, .025, 1), .75), "soil": ((.035, .02, .012, 1), .95),
    "petal_cream": ((.85, .78, .6, 1), .5), "petal_pink": ((.9, .42, .42, 1), .5), "pollen": ((.85, .6, .1, 1), .6),
    "ceramic": ((.8, .66, .42, 1), .2),
}


def slot_material(name):
    key = "RL | " + name
    if key in k.SLOTS:
        return k.slot(name)
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        col, rough = PREVIEW[name]
        p.inputs["Base Color"].default_value = col
        p.inputs["Roughness"].default_value = rough
        m.diffuse_color = col
    return m


# ------------------------------------------------------------------ mesh builder (R4's, with this script's slots)
class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.slots = []

    def merge(self, src, slot, M=Matrix.Identity(4)):
        if slot not in self.slots:
            self.slots.append(slot)
        mi = self.slots.index(slot)
        vm = {v: self.bm.verts.new(M @ v.co) for v in src.verts}
        for f in src.faces:
            try:
                nf = self.bm.faces.new([vm[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = mi
            nf.smooth = f.smooth
        src.free()

    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)

    def obj(self, name, col):
        me = bpy.data.meshes.new(name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for s in self.slots:
            me.materials.append(slot_material(s))
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=SMOOTH_ANGLE)
        o = bpy.data.objects.new(name, me)
        col.objects.link(o)
        return o


def lathe_bm(prof, seg=32, wobble=None):
    """(r, z) profile turned about Z; r = 0 points close the ends. wobble(theta) scales r (flutes)."""
    bm = bmesh.new()
    rings = []
    for r, z in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
            continue
        rings.append([bm.verts.new((r * (wobble(t) if wobble else 1) * math.cos(t),
                                    r * (wobble(t) if wobble else 1) * math.sin(t), z))
                      for t in (2 * math.pi * i / seg for i in range(seg))])
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(seg):
            j = (i + 1) % seg
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], b[0], a[j]))
            else:
                bm.faces.new((a[i], b[i], b[j], a[j]))
    return bm


def tube_bm(pts, radii, sides=6, cap0=False, cap1=True):
    """A tube swept along a polyline (parallel-transport frames); radii per point."""
    bm = bmesh.new()
    pts = [Vector(p) for p in pts]
    tang = []
    for i in range(len(pts)):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        tang.append((b - a).normalized())
    ref = Vector((0, 0, 1)) if abs(tang[0].z) < .9 else Vector((1, 0, 0))
    n = tang[0].cross(ref).normalized()
    rings = []
    for i, p in enumerate(pts):
        if i:
            n = (n - tang[i] * n.dot(tang[i])).normalized()
        b = tang[i].cross(n)
        rings.append([bm.verts.new(p + radii[i] * (math.cos(2 * math.pi * j / sides) * n +
                                                    math.sin(2 * math.pi * j / sides) * b)) for j in range(sides)])
    for a, c in zip(rings, rings[1:]):
        for j in range(sides):
            bm.faces.new((a[j], a[(j + 1) % sides], c[(j + 1) % sides], c[j]))
    if cap0:
        bm.faces.new(list(reversed(rings[0])))
    if cap1:
        tip = bm.verts.new(pts[-1] + tang[-1] * radii[-1] * .6)
        for j in range(sides):
            bm.faces.new((rings[-1][j], rings[-1][(j + 1) % sides], tip))
    for f in bm.faces:
        f.smooth = True
    return bm


def bezier(p0, p1, p2, n):
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    return [(1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2 for t in (i / (n - 1) for i in range(n))]


# ------------------------------------------------------------------ the thick leaf
def leaf_bm(L, W, outline, nu=13, vs=(0, .24, .6, 1), thick=.035, fold=.10, cup=.12, droop=.25, veins=6,
            vein_slope=.55, quilt=.035, wave=.03, wave_n=4.0, curl=0.0, rib=0.0, rng=None):
    """A leaf along +Y from its base at the origin, face up +Z. outline(u) = half-width / W along u (0 base .. 1 tip).
    vs: column positions across one half (0 = midrib .. 1 = margin). fold: V of the midrib (x W), cup: margins up
    (x W), droop: tip down (x L), veins: lateral vein pairs, quilt: the blade bulging between them (x W), wave: margin
    ripple (x W). Thickness is real: an underside and a rim, the leaf thinner at the margin."""
    rng = rng or random.Random(0)
    ph = rng.uniform(0, 6.28)
    cols = [-v for v in reversed(vs[1:])] + list(vs)
    grid = []
    for i in range(nu):
        u = .4 * i / (nu - 1) + .6 * (.5 - .5 * math.cos(math.pi * i / (nu - 1)))   # rows closer at base and tip
        hw = W * max(outline(u), .015) * (1 + .035 * math.sin(u * 17 + ph))
        row = []
        for v in cols:
            av = abs(v)
            x = v * hw
            env = math.sin(math.pi * min(u * 1.05, 1)) ** .7
            z = fold * W * av * env + cup * W * av * av * env
            # quilting between lateral veins (veins sweep tipward as they run out)
            phase = u * veins - av * vein_slope
            z += quilt * W * (.5 - .5 * math.cos(2 * math.pi * phase)) * min(1, av * 3) * (1 - av ** 4) * env
            z += wave * W * av ** 3 * math.sin(2 * math.pi * (u * wave_n) + ph + (1.3 if v < 0 else 0))
            y = u * L
            z -= droop * L * u * u
            if curl:   # roll the tip back under (palm and fern tips)
                z -= curl * L * u ** 4
            row.append(Vector((x, y, z)))
        grid.append(row)
    nv = len(cols)
    # normals from the grid
    def nrm(i, j):
        a = grid[min(i + 1, nu - 1)][j] - grid[max(i - 1, 0)][j]
        b = grid[i][min(j + 1, nv - 1)] - grid[i][max(j - 1, 0)]
        n = b.cross(a)
        return n.normalized() if n.length > 1e-9 else Vector((0, 0, 1))
    bm = bmesh.new()
    top = [[bm.verts.new(grid[i][j]) for j in range(nv)] for i in range(nu)]
    bot = []
    for i in range(nu):
        row = []
        for j in range(nv):
            av = abs(cols[j])
            t = thick * W * (1 - .65 * av * av) * (1.4 - .6 * i / (nu - 1))
            row.append(bm.verts.new(grid[i][j] - nrm(i, j) * t))
        bot.append(row)
    for i in range(nu - 1):
        for j in range(nv - 1):
            bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j]))
            bm.faces.new((bot[i][j], bot[i + 1][j], bot[i + 1][j + 1], bot[i][j + 1]))
    ring_t = [top[0][j] for j in range(nv)] + [top[i][nv - 1] for i in range(1, nu)] + \
             [top[nu - 1][j] for j in range(nv - 2, -1, -1)] + [top[i][0] for i in range(nu - 2, 0, -1)]
    ring_b = [bot[0][j] for j in range(nv)] + [bot[i][nv - 1] for i in range(1, nu)] + \
             [bot[nu - 1][j] for j in range(nv - 2, -1, -1)] + [bot[i][0] for i in range(nu - 2, 0, -1)]
    for a in range(len(ring_t)):
        b = (a + 1) % len(ring_t)
        bm.faces.new((ring_t[a], ring_b[a], ring_b[b], ring_t[b]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    if not rib:
        return bm, None
    # the midrib: a low tented ridge on the top face, base to near the tip, tapering (its own slot, lighter)
    rb = bmesh.new()
    mid = nv // 2
    rows = []
    for i in range(nu - 1):
        u = .4 * i / (nu - 1) + .6 * (.5 - .5 * math.cos(math.pi * i / (nu - 1)))
        c, n = grid[i][mid], nrm(i, mid)
        w = rib * W * (1 - .8 * u)
        side = Vector((1, 0, 0))
        rows.append([rb.verts.new(c - side * w + n * .002 * W), rb.verts.new(c + n * (.012 * W * (1 - .7 * u))),
                     rb.verts.new(c + side * w + n * .002 * W)])
    for a, b in zip(rows, rows[1:]):
        rb.faces.new((a[0], b[0], b[1], a[1]))
        rb.faces.new((a[1], b[1], b[2], a[2]))
    for f in rb.faces:
        f.smooth = True
    return bm, rb


def aim(base, d, roll=0.0, up=Vector((0, 0, 1))):
    """Matrix taking a leaf (+Y along, +Z face) to `base`, pointing along d, its face as near `up` as it can be."""
    y = Vector(d).normalized()
    x = y.cross(up)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    z = x.cross(y)
    R = Matrix((x, y, z)).transposed().to_4x4()
    return Matrix.Translation(base) @ R @ Matrix.Rotation(roll, 4, "Y")


def soil_bm(r, z, rng, rings=3, seg=28, lump=.12):
    """A lumpy soil disc of radius r, top about z, its edge tucked down under the pot lip."""
    prof = [(r * (i / rings), z + lump * .5 * (1 - (i / rings) ** 2)) for i in range(rings, 0, -1)]
    bm = bmesh.new()
    rs = []
    for rr, zz in [(r, z - .25)] + prof:
        rs.append([bm.verts.new((rr * math.cos(t), rr * math.sin(t),
                                 zz + (rng.uniform(-lump, lump) if rr < r * .99 else 0)))
                   for t in (2 * math.pi * i / seg for i in range(seg))])
    c = bm.verts.new((0, 0, z + lump * .5))
    for a, b in zip(rs, rs[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b[j], b[i]))
    for i in range(seg):
        bm.faces.new((rs[-1][i], rs[-1][(i + 1) % seg], c))
    for f in bm.faces:
        f.smooth = True
    return bm


# ------------------------------------------------------------------ 1 fiddle-leaf fig
def fiddle_outline(u):
    """Violin leaf (obovate): narrow wedge base, a faint waist, widest at 0.7, a broad blunt round tip."""
    if u < .7:
        w = .5 * (u / .7) ** .7
    else:
        w = .5 * math.sqrt(max(0, 1 - ((u - .7) / .3) ** 2)) ** .85
    return max(w - .045 * math.exp(-((u - .38) / .1) ** 2), 0)


def polyline_at(pts, t):
    f = min(max(t, 0), 1) * (len(pts) - 1)
    i = min(int(f), len(pts) - 2)
    return pts[i].lerp(pts[i + 1], f - i)


def build_fiddle(s, soil_r, soil_z, seed=3):
    """A brown trunk, slightly S-bent, forking into two branches in its upper half; big violin leaves crowd the top
    two thirds on short petioles, tilted up and out (the lower ones flatter), the newest at the tips lighter."""
    rng = random.Random(seed)
    B = Builder()
    S = Matrix.Scale(s, 4)
    B.merge(soil_bm(soil_r, soil_z, rng), "soil", S)
    z0 = soil_z - .25
    trunk = [Vector((.22 * math.sin(t * 3.0), .14 * math.sin(t * 2.1 + 1), z0 + t * (14.6 - z0)))
             for t in (i / 13 for i in range(14))]
    B.merge(tube_bm(trunk, [.44 if i == 0 else .40 - .27 * (i / 13) ** .9 for i in range(14)], 10), "bark", S)
    branches = []
    for t0, end, mid in ((.42, (-2.6, .9, 12.6), (-1.0, .4, 10.4)), (.52, (2.4, -1.0, 13.0), (1.1, -.3, 11.0))):
        p0 = polyline_at(trunk, t0)
        br = bezier(p0, Vector(mid), Vector(end), 8)
        B.merge(tube_bm(br, [.2 - .12 * i / 7 for i in range(8)], 8), "bark", S)
        branches.append(br)
    centre = Vector((0, 0, 10.5))
    # (stem, from t, to t, leaves)
    plan = [(trunk, .28, 1.0, 9), (branches[0], .2, 1.0, 5), (branches[1], .2, 1.0, 5)]
    k_i = 0
    for stem, ta, tb, n in plan:
        for i in range(n):
            t = ta + (tb - ta) * (i / (n - 1))
            p0 = polyline_at(stem, t)
            tip = i == n - 1
            hgt = (p0.z - 6) / 9
            out = Vector((p0.x - centre.x, p0.y - centre.y, 0))
            if out.length < .6:   # on the trunk: the golden-angle spiral
                yaw = k_i * math.radians(137.5) + rng.uniform(-.15, .15)
            else:                 # on a branch: alternate sides of its outward line
                yaw = math.atan2(out.y, out.x) + (.75 if i % 2 else -.75) + rng.uniform(-.15, .15)
            k_i += 1
            elev = math.radians(20 + 48 * min(max(hgt, 0), 1) ** 1.2 + rng.uniform(-8, 8) + (12 if tip else 0))
            d = Vector((math.cos(yaw) * math.cos(elev), math.sin(yaw) * math.cos(elev), math.sin(elev)))
            young = tip or (stem is trunk and t > .9)
            L = rng.uniform(4.0, 4.8) * (.72 if young else 1) * (.85 if hgt < .1 else 1)
            pet = .5 + .2 * rng.random()
            dh = Vector((d.x, d.y, 0)).normalized()
            p2 = p0 + dh * pet + Vector((0, 0, pet * .45))
            B.merge(tube_bm(bezier(p0, p0 + dh * pet * .25 + Vector((0, 0, pet * .45)), p2, 5),
                            [.09, .08, .07, .06, .055], 6), "stem" if young else "bark", S)
            lf, rb = leaf_bm(L, L * .86, fiddle_outline, nu=13, vs=(0, .2, .55, 1), thick=.028, fold=.035,
                             cup=-.13, droop=.07 + .22 * (1 - min(max(hgt, 0), 1)) ** 1.5, veins=6, vein_slope=.5,
                             quilt=.04, wave=.04, wave_n=2.4, rib=.012, rng=rng)
            # blades turn on their petioles: a strong roll, alternating, so faces show from every side
            M = S @ aim(p2, d, roll=(1 if k_i % 2 else -1) * rng.uniform(.45, .95))
            B.merge(lf, "leaf_light" if young else "leaf", M)
            B.merge(rb, "stem" if young else "leaf_light", M)
    return B


# ------------------------------------------------------------------ 3 kentia palm
def strap_bm(L, W, rows=5, fold=.35, droop=.35, outline=None):
    """A narrow solid leaflet along +Y, face +Z: a diamond cross-section (thin margins, V-folded along its midrib,
    real thickness in the middle), drooping toward its pointed tip. Ends close to points."""
    outline = outline or (lambda u: .5 * min(1, (u / .1) ** .5) * (1 - u ** 1.8))
    bm = bmesh.new()
    pts = []
    for i in range(rows + 1):
        u = i / rows
        y, z = u * L, -droop * L * u * u
        if i in (0, rows):
            pts.append([bm.verts.new((0, y, z))])
            continue
        hw = W * outline(u)
        zm = z - fold * hw
        t = .07 * W * (1 - .5 * u)
        pts.append([bm.verts.new((-hw, y, z)), bm.verts.new((0, y, zm + t)), bm.verts.new((hw, y, z)),
                    bm.verts.new((0, y, zm - t))])
    for a, b in zip(pts, pts[1:]):
        for j in range(4):
            jj = (j + 1) % 4
            if len(a) == 1:
                bm.faces.new((a[0], b[jj], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[j], a[jj], b[0]))
            else:
                bm.faces.new((a[j], a[jj], b[jj], b[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    return bm


def frond(B, S, base, yaw, elev, R, rng, leaflets=9, slot="leaf", lmax=.42, angle=48):
    """A pinnate frond: a green rachis arching out from `base` (yaw, elevation of its first stretch, reach R) with
    `leaflets` drooping straps a side, longest in the middle, the newest (upright) ones lighter."""
    dh = Vector((math.cos(yaw), math.sin(yaw), 0))
    up = Vector((0, 0, 1))
    p0 = Vector(base)
    # rises steeply, then bows out: the stalk goes up, the leafy end leans over
    p1 = p0 + up * R * .8 * math.sin(elev) + dh * R * .14
    p2 = p0 + dh * R * (.38 + .6 * math.cos(elev)) + up * R * (.66 * math.sin(elev) + .08)
    rach = bezier(p0, p1, p2, 9)
    B.merge(tube_bm(rach, [.1 - .07 * i / 8 for i in range(9)], 5), "stem", S)
    for i in range(leaflets):
        t = .38 + .61 * i / (leaflets - 1)
        f = t * 8
        j = min(int(f), 7)
        pos = rach[j].lerp(rach[j + 1], f - j)
        T = (rach[j + 1] - rach[j]).normalized()
        side = T.cross(up)
        if side.length < 1e-4:
            side = dh.cross(up)
        side.normalize()
        Lf = R * lmax * math.sin(math.pi * (.1 + .75 * (t - .3) / .7 + .1)) ** .5 * rng.uniform(.9, 1.08)
        for sgn in (1, -1):
            a = math.radians(angle + rng.uniform(-6, 6))
            d = (T * math.cos(a) + side * sgn * math.sin(a) + up * .1).normalized()
            lf = strap_bm(Lf, Lf * .25, rows=5, fold=.3, droop=.5 + .15 * (1 - math.sin(elev)) + rng.uniform(0, .1))
            B.merge(lf, slot, S @ aim(pos, d, roll=sgn * rng.uniform(.05, .2)))


def build_palm(s, soil_r, soil_z, seed=7):
    """Kentia: five slender green canes from the soil, fronds from their tops - the central ones tall, upright and
    lighter, the outer ones arching out and drooping."""
    rng = random.Random(seed)
    B = Builder()
    S = Matrix.Scale(s, 4)
    B.merge(soil_bm(soil_r, soil_z, rng), "soil", S)
    # three tiers of fronds, each from its own short green cane: (count, cane height, elevation, reach, slot, size)
    tiers = [(5, 2.4, (50, 60), 6.0, "leaf", .40), (5, 4.2, (66, 74), 6.8, "leaf", .38),
             (3, 5.8, (80, 86), 7.4, "leaf_light", .33)]
    n = 0
    for count, ch, (e0, e1), R, slot, lmax in tiers:
        for i in range(count):
            a = (i + .5 * (n % 2)) * 2 * math.pi / count + n * .37 + rng.uniform(-.2, .2)
            r = rng.uniform(.2, .8)
            b0 = Vector((r * math.cos(a), r * math.sin(a), soil_z - .25))
            top = b0 + Vector((math.cos(a), math.sin(a), 0)) * .25 + Vector((0, 0, ch * rng.uniform(.85, 1.1)))
            B.merge(tube_bm(bezier(b0, b0.lerp(top, .5), top, 4), [.15, .14, .13, .12], 6, cap1=False), "stem", S)
            frond(B, S, top, a, math.radians(rng.uniform(e0, e1)), R * rng.uniform(.93, 1.05), rng,
                  leaflets=8, slot=slot, lmax=lmax * 1.4, angle=42)
        n += 1
    return B


def soft_limit(B, axis_sign, a, b):
    """Squeeze everything past a (metres, along +x * axis_sign) so nothing reaches past b: keeps a plant off the wall
    it stands by."""
    for v in B.bm.verts:
        x = v.co.x * axis_sign
        if x > a:
            v.co.x = axis_sign * (a + (b - a) * math.tanh((x - a) / (b - a)))


# ------------------------------------------------------------------ 5 flowering shrub
def oval_outline(u):
    """Camellia / gardenia leaf: an even oval, a short pointed tip."""
    return .5 * math.sin(math.pi * min(u, .999)) ** .6 * (1 - .25 * max(0, u - .75) / .25)


def petal_outline(u):
    """A rounded petal: narrow claw, broad round top."""
    return .5 * math.sin(math.pi * min(u, .999) * .92 + .08) ** .45 * (.55 + .5 * u)


def frame_at(n):
    """Two unit vectors across a normal."""
    a = Vector((0, 0, 1)) if abs(n.z) < .9 else Vector((1, 0, 0))
    e1 = n.cross(a).normalized()
    return e1, n.cross(e1)


def flower(B, S, c, n, size, slot, rng, open_=1.0):
    """Five cupped petals round a pollen boss, facing n."""
    e1, e2 = frame_at(n)
    ph = rng.uniform(0, 6.28)
    for i in range(5):
        a = ph + i * 2 * math.pi / 5 + rng.uniform(-.1, .1)
        d = (math.cos(a) * e1 + math.sin(a) * e2 + n * (.55 - .35 * open_)).normalized()
        pb, _ = leaf_bm(size * rng.uniform(.92, 1.05), size * .95, petal_outline, nu=5, vs=(0, 1),
                        thick=.04, fold=0, cup=.22, droop=-.06, veins=3, quilt=.02, wave=.03, wave_n=1.5, rng=rng)
        B.merge(pb, slot, S @ aim(c + d * size * .08, d, up=n))
    boss = lathe_bm([(0, -.05), (.2, 0), (.17, .09), (.07, .14), (0, .15)], 8)
    e1, e2 = frame_at(n)
    R = Matrix((e1, e2, n)).transposed().to_4x4()
    B.merge(boss, "pollen", S @ Matrix.Translation(c) @ R @ Matrix.Scale(size * 1.0, 4))


def bud(B, S, c, n, size, slot):
    e1, e2 = frame_at(n)
    R = Matrix((e1, e2, n)).transposed().to_4x4()
    prof = [(0, -.05), (.3, .05), (.42, .3), (.38, .6), (.22, .85), (0, 1.0)]
    B.merge(lathe_bm(prof, 8), slot, S @ Matrix.Translation(c) @ R @ Matrix.Scale(size, 4))
    # the green calyx cupping its base
    B.merge(lathe_bm([(0, -.08), (.34, .02), (.44, .22), (.3, .2), (0, .05)], 6), "stem",
            S @ Matrix.Translation(c) @ R @ Matrix.Scale(size, 4))


def fib_dir(i, n, zmin):
    """The i-th of n directions spread evenly over the part of the unit sphere above z = zmin."""
    z = 1 - (1 - zmin) * (i + .5) / n
    r = math.sqrt(max(0, 1 - z * z))
    a = i * math.radians(137.508)
    return Vector((r * math.cos(a), r * math.sin(a), z))


def build_bloom(s, soil_r, soil_z, seed=11):
    """A rounded bush of small glossy oval leaves over a few green stems, cream and pink five-petalled blossoms and
    pink buds sitting on its surface (the sheet's flowering pot: camellia-like)."""
    rng = random.Random(seed)
    B = Builder()
    S = Matrix.Scale(s, 4)
    B.merge(soil_bm(soil_r, soil_z, rng), "soil", S)
    C = Vector((0, 0, soil_z + 1.3))
    RX, RZ = 5.6, 4.1
    def on_dome(d, k_=1.0):
        return C + Vector((d.x * RX, d.y * RX, d.z * RZ)) * k_
    for i in range(8):   # stems fanning out under the dome
        d = fib_dir(i * 5 + 2, 40, -.2)
        b0 = Vector((rng.uniform(-.4, .4), rng.uniform(-.4, .4), soil_z - .25))
        end = on_dome(d, .62)
        B.merge(tube_bm(bezier(b0, b0.lerp(end, .5) + Vector((0, 0, .9)), end, 5), [.13, .11, .09, .07, .05], 5),
                "stem", S)
    for nl, kr in ((56, .8), (18, .55)):   # an outer shell of leaves and an inner one, so the bush reads solid
        for i in range(nl):  # leaves laid like shingles over the dome: base inside, leaning out and up, face outward
            d = fib_dir(i, nl, -.4)
            d = (d + Vector((rng.uniform(-.12, .12), rng.uniform(-.12, .12), rng.uniform(-.08, .08)))).normalized()
            base = on_dome(d, kr * rng.uniform(.93, 1.07))
            out = Vector((d.x * RZ, d.y * RZ, d.z * RX)).normalized()   # the dome's normal there
            up_t = Vector((0, 0, 1)) - out * out.z                       # along the surface, upward
            if up_t.length < .2:
                up_t = Vector((math.cos(i * 2.4), math.sin(i * 2.4), 0))
            up_t.normalize()
            side = out.cross(up_t)
            L = rng.uniform(2.5, 3.0) * (.8 if d.z > .85 else 1)
            # upper leaves lean up the dome, the lower ones hang down over the pot lip
            dirn = (out * .6 + up_t * (.8 if d.z > .15 else .55) + side * rng.uniform(-.5, .5)).normalized()
            lf, _ = leaf_bm(L, L * .52, oval_outline, nu=6, vs=(0, 1), thick=.04, fold=.1, cup=-.06, droop=.16,
                            veins=0, quilt=0, wave=.03, wave_n=1.5, rng=rng)
            B.merge(lf, "leaf", S @ aim(base, dirn, roll=rng.uniform(-.25, .25), up=out))
    # blossoms on the upper surface, most of them pink, a few cream; buds between them
    blooms = [(fib_dir(i, 11, -.25), "petal_pink" if i % 3 else "petal_cream") for i in range(11)]
    for d, slot in blooms:
        d = (d + Vector((rng.uniform(-.08, .08), rng.uniform(-.08, .08), 0))).normalized()
        n = Vector((d.x * RZ, d.y * RZ, d.z * RX)).normalized()
        n = (n + Vector((0, 0, .15))).normalized()
        flower(B, S, on_dome(d, 1.03), n, rng.uniform(1.1, 1.25), slot, rng)
    for i in range(7):
        d = fib_dir(i * 3 + 1, 28, -.05)
        d = (d + Vector((.12, -.1, 0))).normalized()
        n = (Vector((d.x * RZ, d.y * RZ, d.z * RX)).normalized() + Vector((0, 0, .5))).normalized()
        bud(B, S, on_dome(d, 1.0), n, rng.uniform(.5, .6), "petal_pink")
    return B


# ------------------------------------------------------------------ 2 monstera
def patch_bm(fn, u0, u1, v0, v1, nu, nv, thick):
    """A thick patch of a leaf surface fn(u, v) -> Vector over [u0, u1] x [v0, v1]: top, underside (offset along the
    surface normal by thick(u, v)) and a rim all round."""
    us = [u0 + (u1 - u0) * i / (nu - 1) for i in range(nu)]
    vs = [v0 + (v1 - v0) * j / (nv - 1) for j in range(nv)]
    P = [[fn(u, v) for v in vs] for u in us]

    def nrm(i, j):
        a = P[min(i + 1, nu - 1)][j] - P[max(i - 1, 0)][j]
        b = P[i][min(j + 1, nv - 1)] - P[i][max(j - 1, 0)]
        n = b.cross(a)
        return n.normalized() if n.length > 1e-9 else Vector((0, 0, 1))
    bm = bmesh.new()
    top = [[bm.verts.new(P[i][j]) for j in range(nv)] for i in range(nu)]
    bot = [[bm.verts.new(P[i][j] - nrm(i, j) * thick(us[i], vs[j])) for j in range(nv)] for i in range(nu)]
    for i in range(nu - 1):
        for j in range(nv - 1):
            bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j]))
            bm.faces.new((bot[i][j], bot[i + 1][j], bot[i + 1][j + 1], bot[i][j + 1]))

    def ring(g):
        return [g[0][j] for j in range(nv)] + [g[i][nv - 1] for i in range(1, nu)] + \
               [g[nu - 1][j] for j in range(nv - 2, -1, -1)] + [g[i][0] for i in range(nu - 2, 0, -1)]
    rt, rb = ring(top), ring(bot)
    for a in range(len(rt)):
        b = (a + 1) % len(rt)
        bm.faces.new((rt[a], rb[a], rb[b], rt[b]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    return bm


def monstera_leaf(B, slot, M, L, rng, slits=4, young=False):
    """A split leaf: heart-shaped blade (basal lobes behind the petiole), a solid band along the midrib and, each
    side, fingers between slits that run from the margin toward the midrib, slanting tipward with the veins; the
    blade cupped a little, drooping at the tip, its margin rippled. The pieces overlap under the band."""
    W = L * 1.08
    ph = rng.uniform(0, 6.28)

    def half_w(u):   # u in [-.2, 1]: -.2 .. 0 are the basal lobes
        t = (u + .2) / 1.2
        return W * .5 * math.sin(math.pi * min(t, .999)) ** .5 * (1.15 - .55 * t)

    def fn(u, v):
        av = abs(v)
        z = .1 * W * av * av - .04 * W * av - .16 * L * max(u, 0) ** 2
        z += .025 * W * av ** 2 * math.sin(u * 9 + ph + (1.5 if v < 0 else 0))
        return Vector((v * half_w(u), u * L, z))

    B.merge(patch_bm(fn, -.02, .985, -.42, .42, 10, 3, lambda u, v: .02 * W * (1 - .5 * abs(v))), slot, M)
    gap = .028
    cuts = [-.2, 1.0] if young else [-.2] + [.08 + (.82 / slits) * i for i in range(1, slits)] + [1.0]
    for sgn in (1, -1):
        for a, b in zip(cuts, cuts[1:]):
            g0 = gap if a > -.2 else 0
            g1 = gap if b < 1 else 0

            # a finger between two slits: its sides slant tipward toward the margin (u shifts with |v|)
            def f2(u, v, a=a, b=b, sgn=sgn, g0=g0, g1=g1):
                av = .3 + .7 * v
                uu = a + g0 + (b - a - g0 - g1) * u
                uu += .1 * (av - .3) * (1 - abs(2 * u - 1) ** 4) * (1 if b < 1 else 0)
                return fn(min(uu, 1.0), sgn * av)
            nu = 7 if young else (4 if b - a < .3 else 6)
            B.merge(patch_bm(f2, 0, 1, 0, 1, nu, 3, lambda u, v: .016 * W), slot, M)


def build_monstera(s, soil_r, soil_z, seed=5):
    """Long green petioles from the soil, arching up and out, each ending in a big split leaf held at an angle; two
    young unsplit lighter leaves in the middle."""
    rng = random.Random(seed)
    B = Builder()
    S = Matrix.Scale(s, 4)
    B.merge(soil_bm(soil_r, soil_z, rng), "soil", S)
    for i in range(9):
        young = i in (2, 6)
        yaw = i * math.radians(137.5) + rng.uniform(-.15, .15)
        tall = .9 if young else rng.uniform(.55, 1.0)
        dh = Vector((math.cos(yaw), math.sin(yaw), 0))
        b0 = Vector((rng.uniform(-.5, .5), rng.uniform(-.5, .5), soil_z - .2))
        reach = 2.6 + 3.4 * (1 - tall)
        top = b0 + dh * reach + Vector((0, 0, 3.2 + 5.2 * tall))
        pts = bezier(b0, b0 + dh * reach * .15 + Vector((0, 0, (top.z - b0.z) * .9)), top, 8)
        B.merge(tube_bm(pts, [.24 - .1 * j / 7 for j in range(8)], 6), "stem", S)
        L = rng.uniform(5.4, 6.2) * (.6 if young else 1)
        # the blade leans out from the petiole tip, pointing outward and a little up or down, face up
        elev = math.radians(-rng.uniform(18, 38) + (50 if young else 0))   # blades hang from the petiole tip
        d = (dh * math.cos(elev) + Vector((0, 0, math.sin(elev)))).normalized()
        M = S @ aim(top - d * L * .12, d, roll=rng.uniform(-.35, .35))
        monstera_leaf(B, "leaf_light" if young else "leaf", M, L, rng, slits=rng.choice((4, 5)), young=young)
    return B


# ------------------------------------------------------------------ 4 fern
def build_fern(s, soil_r, soil_z, seed=17):
    """A Boston-fern fountain: twenty fronds from the crown, the middle ones upright, the outer ones arching out and
    hanging over the pot lip; each a thin rachis with small solid oblong pinnae either side, shrinking to its tip."""
    rng = random.Random(seed)
    B = Builder()
    S = Matrix.Scale(s, 4)
    B.merge(soil_bm(soil_r, soil_z, rng), "soil", S)
    up = Vector((0, 0, 1))
    pin_outline = lambda u: .5 * math.sin(math.pi * min(u, .999)) ** .3   # noqa: E731
    n = 22
    for i in range(n):
        t_out = i / (n - 1)                       # 0 = the middle of the crown .. 1 = the outer ring
        yaw = i * math.radians(137.5) + rng.uniform(-.1, .1)
        dh = Vector((math.cos(yaw), math.sin(yaw), 0))
        elev = math.radians(86 - 60 * t_out ** .8 + rng.uniform(-5, 5))
        R = rng.uniform(7.6, 8.6) * (1.05 - .1 * t_out)
        p0 = Vector((0, 0, soil_z)) + dh * rng.uniform(.1, .6)
        p1 = p0 + up * R * .75 * math.sin(elev) + dh * R * .3
        se = math.sin(elev)   # the outer fronds arch up and then fall below the pot lip
        p2 = p0 + dh * R * (.42 + .28 * math.cos(elev)) + up * R * (1.0 * se - 1.1 * (1 - se) - .1)
        rach = bezier(p0, p1, p2, 9)
        B.merge(tube_bm(rach, [.07 - .045 * j / 8 for j in range(9)], 4), "stem", S)
        npin = 16
        for j in range(npin):
            t = .14 + .84 * j / (npin - 1)
            f = t * 8
            k_ = min(int(f), 7)
            pos = rach[k_].lerp(rach[k_ + 1], f - k_)
            T = (rach[k_ + 1] - rach[k_]).normalized()
            side = T.cross(up)
            if side.length < 1e-4:
                side = dh.cross(up)
            side.normalize()
            Lp = R * .24 * (1 - .6 * t) ** .6 * (.75 + .25 * math.sin(math.pi * min(t * 2.5, 1)))
            for sgn in (1, -1):
                d = (T * .25 + side * sgn - up * .1).normalized()
                pb = strap_bm(Lp, Lp * .62, rows=2, fold=.2, droop=.1, outline=pin_outline)
                N = side.cross(T) if side.cross(T).z > 0 else T.cross(side)   # the frond's face
                B.merge(pb, "leaf_light", S @ aim(pos, d, roll=sgn * .2, up=N))
    return B


# ------------------------------------------------------------------ the chess room's pots (sheet 1, 2, 3/7, 4)
def build_pot(B, S, kind):
    """A unit pot like the blockout's (r about 3, top about 4.3), scaled by S; returns (soil r, soil z) unscaled."""
    if kind == "bowl":       # 2: low cream bowl, gold rim
        body = [(0, 0), (2.35, 0), (2.5, .12), (2.48, .3), (2.8, .9), (3.12, 1.9), (3.14, 2.8), (3.02, 3.6),
                (2.9, 3.95), (2.72, 3.95), (2.75, 3.4), (0, 3.4)]
        rim = [(2.7, 3.82), (3.0, 3.86), (3.12, 4.0), (3.08, 4.2), (2.94, 4.28), (2.74, 4.24), (2.68, 4.0), (2.7, 3.82)]
        B.merge(lathe_bm(body, 64), "ceramic", S)
        B.merge(lathe_bm(rim, 64), "brass", S)
        return 2.72, 3.75
    if kind == "fluted":     # 1 / 6: tall cream fluted pot, gold rim
        body = [(0, 0), (2.55, 0), (2.68, .1), (2.7, .3), (2.82, 1.2), (2.92, 2.4), (2.95, 3.6), (2.9, 4.0),
                (2.72, 4.0), (2.72, 3.6), (0, 3.6)]
        rim = [(2.72, 3.9), (2.98, 3.95), (3.1, 4.1), (3.08, 4.32), (2.92, 4.4), (2.72, 4.34), (2.68, 4.1), (2.72, 3.9)]
        B.merge(lathe_bm(body, 96, wobble=lambda t: 1.0 - .05 * abs(math.sin(8 * t)) ** .6), "ceramic", S)
        B.merge(lathe_bm(rim, 64), "brass", S)
        return 2.72, 3.95
    if kind == "tub":        # 3 / 7: brass tub, base band and lip roll
        body = [(0, 0), (2.62, 0), (2.8, .06), (2.86, .26), (2.86, .55), (2.74, .62), (2.78, 1.2), (2.92, 2.6),
                (3.02, 3.9), (2.86, 3.9), (2.8, 3.4), (0, 3.4)]
        rim = [(2.86, 3.8), (3.08, 3.84), (3.18, 4.02), (3.14, 4.2), (3.0, 4.26), (2.86, 4.2), (2.84, 4.0), (2.86, 3.8)]
        B.merge(lathe_bm(body, 64), "brass", S)
        B.merge(lathe_bm(rim, 48), "brass", S)
        return 2.86, 3.85
    if kind == "footed":     # 4: cream urn on a turned foot
        body = [(0, 0), (1.9, 0), (2.0, .12), (1.95, .3), (1.35, .55), (1.25, .9), (1.6, 1.2), (2.5, 1.7),
                (3.0, 2.5), (3.15, 3.3), (3.1, 3.95), (2.9, 4.1), (2.78, 3.6), (0, 3.6)]
        rim = [(2.82, 3.98), (3.12, 4.0), (3.24, 4.15), (3.2, 4.34), (3.02, 4.42), (2.84, 4.36), (2.78, 4.14),
               (2.82, 3.98)]
        B.merge(lathe_bm(body, 48), "ceramic", S)
        B.merge(lathe_bm(rim, 64), "brass", S)
        return 2.82, 4.0
    raise KeyError(kind)


def soft_limit_dir(B, axis, a, b):
    """Squeeze everything past a (metres along the horizontal unit vector axis) so nothing goes past b: keeps a
    plant off the wall or furniture beside it."""
    axis = Vector(axis).normalized()
    for v in B.bm.verts:
        x = v.co.dot(axis)
        if x > a:
            v.co += axis * ((a + (b - a) * math.tanh((x - a) / (b - a))) - x)


# ------------------------------------------------------------------ places
BUILDERS = {"fiddle": build_fiddle, "palm": build_palm, "bloom": build_bloom, "monstera": build_monstera,
            "fern": build_fern}
# key -> model, builder, host, scale s, pot (an R4 FBX shown with it in the still, or a kind of our own), turn about z
# (degrees: the fiddle's two branches run along the wall), limits ((horizontal axis in the host frame, metres the
# plant may reach that way), ...).
# Gallery: R4's pots (s = rl_r4_gallery_model PLANTERS), the walls at x +-37.25 (leaves 1 m off). Chess room: our own
# pots, s from the blockout pot (r 3 s); limits from R5's measures (11-18: pilaster, sofa, bookcases 1.3 .. 2.5 m
# beyond an 11 m reach).
PLANTS = {
    "fiddle": ("RlPlantFiddle112", "fiddle", "Library gallery houseplant (28, -22.5, -112)", 1.5,
               "Gallery/RlPlanterFluted112", 90, [((1, 0, 0), 37.25 - 28 - 1)]),
    "palm": ("RlPlantPalm53", "palm", "Library gallery houseplant (29, -22.5, -53)", 1.8,
             "Gallery/RlBrassTub53", 0, [((1, 0, 0), 37.25 - 29 - 1)]),
    "bloom": ("RlPlantBloom35", "bloom", "Library gallery houseplant (-28, -22.5, -35)", 1.2,
              "Gallery/RlPlanterFluted35", 0, [((-1, 0, 0), 37.25 - 28 - 1)]),
    "monstera": ("RlConservatoryMonstera5", "monstera", "Giant chess conservatory plant (104, -22, 5)", 1.8,
                 "bowl", 0, [((0, -1, 0), 11.0), ((1, 0, 0), 15.0)]),
    "fern": ("RlConservatoryFern12", "fern", "Giant chess conservatory plant (-104, -22, 12)", 1.6,
             "footed", 0, [((-1, 0, 0), 11.0)]),
    # the two big ones: the plant at a smaller scale than its pot (8th: plant scale), so the fiddle stays under
    # y 15 as R5 measured it (the blockout plant's top 14.9)
    "fiddle90": ("RlConservatoryFiddle90", "fiddle", "Giant chess conservatory plant (107, -22, 90)", 2.3,
                 "fluted", 0, [((0, -1, 0), 11.0), ((1, 0, 0), 13.3)], 1.95),
    "palm142": ("RlConservatoryPalm142", "palm", "Giant chess conservatory plant (-105, -22, 142)", 2.0,
                "tub", 0, [((-1, 0, 0), 11.0)]),
}
# x0, y0, x1, y1 in the sheet (top-left origin)
SHEET_CROP = {"fiddle": (0, 0, 345, 565), "palm": (745, 0, 1175, 565), "bloom": (100, 580, 540, 990),
              "monstera": (330, 90, 775, 565), "fern": (1130, 100, 1536, 565),
              "fiddle90": (0, 0, 345, 565), "palm142": (745, 0, 1175, 565)}


def new_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    return col


def build(key):
    model, kind, host, s, pot, yaw, limits = PLANTS[key][:7]
    ps = PLANTS[key][7] if len(PLANTS[key]) > 7 else s   # the plant's own scale (its pot's is s)
    pot_tris = 0
    if "/" in pot:   # in R4's pot: inside its lip (inner r 2.88, lid 4.15): soil r 2.86, top ~4.3
        B = BUILDERS[kind](s, 2.86, 4.30)
    else:            # our own pot round the plant
        B = Builder()
        sr, sz = build_pot(B, Matrix.Scale(s, 4), pot)
        pot_tris = B.tris()
        B.bm.free()
        B = BUILDERS[kind](ps, sr * s / ps, (sz + .1) * s / ps)
        build_pot(B, Matrix.Scale(s, 4), pot)
    if yaw:
        bmesh.ops.rotate(B.bm, verts=B.bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(yaw), 3, "Z"))
    for axis, reach in limits:
        soft_limit_dir(B, axis, reach - 2.0, reach)
    xs, ys, zs = ([v.co[i] for v in B.bm.verts] for i in range(3))
    xs, ys, zs = list(xs), list(ys), list(zs)
    print("[plants] %s %d tris (pot %d), x %.1f .. %.1f, y %.1f .. %.1f, top %.1f m above the floor"
          % (model, B.tris(), pot_tris, min(xs), max(xs), min(ys), max(ys), max(zs)))
    col = new_collection("Plants " + model)
    o = B.obj(model, col)
    k.surface_uvs(o)
    return col, o, model, host, (pot if "/" in pot else None)


# ------------------------------------------------------------------ stills
def _lookdev_materials():
    """rl_lookdev's library materials (its main() left out), for the slots the library already has."""
    src = open(os.path.join(HERE, "rl_lookdev.py"), encoding="utf-8").read()
    src = src[:src.rindex("\nmain()")]
    g = {"__file__": os.path.join(HERE, "rl_lookdev.py"), "__name__": "rl_lookdev_lib"}
    exec(compile(src, "rl_lookdev.py", "exec"), g)
    return g


def still(o, model, pot, key, out_dir):
    import numpy as np
    sc = bpy.context.scene
    keep = set(bpy.data.objects)
    # the pot it stands in (R4's FBX, written in the same host frame) at the model's place
    extra = []
    if pot:
        fbx = os.path.join(k.ROOT, "Assets/Resources/RoyalLibrary/Models", pot + ".fbx")
        bpy.ops.import_scene.fbx(filepath=fbx)
        extra = [x for x in bpy.data.objects if x not in keep]
    lib = _lookdev_materials()
    names = {v: kk for kk, v in lib["SLOT"].items()}
    cache = {}

    def paint(ob):
        for sl in ob.material_slots:
            n = sl.material.name if sl.material else ""
            kk = n[2:].lstrip(" _|").split(".")[0].strip().lower() if n.startswith("RL") else ""
            libname = lib["SLOT"].get(kk) or ("RL_" + "".join(p.capitalize() for p in kk.split("_")))
            if libname in lib["INFO"] or libname == "RL_Glass":
                cache.setdefault(libname, lib["library_material"](libname))
                sl.material = cache[libname]
    saved = [sl.material for sl in o.material_slots]
    for ob in [o] + extra:
        paint(ob)
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = 900, 1200
    sc.render.film_transparent = False
    w = bpy.data.worlds.new("plants still"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (1, 1, 1, 1); bg.inputs["Strength"].default_value = .85
    sun = bpy.data.objects.new("still sun", bpy.data.lights.new("still sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.data.angle = math.radians(4)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    objs = [o] + extra
    lo = Vector((min(min((x.matrix_world @ Vector(c))[i] for c in x.bound_box) for x in objs) for i in range(3)))
    hi = Vector((max(max((x.matrix_world @ Vector(c))[i] for c in x.bound_box) for x in objs) for i in range(3)))
    lo.z = min(lo.z, o.matrix_world.translation.z)
    centre, size = (lo + hi) / 2, hi - lo
    cam = bpy.data.objects.new("still cam", bpy.data.cameras.new("still cam")); sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 85
    view = Vector((-.35, -1, .22)).normalized()
    dist = max(size.z * 1.25, size.x * 1.7) / (36 / 85) * 1.0
    cam.location = centre + view * dist
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.sensor_fit = "VERTICAL"; cam.data.sensor_height = 36
    cam.data.clip_end = dist * 4
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, key + ".png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    # side by side with the sheet crop (same height)
    if key in SHEET_CROP and os.path.exists(SHEET):
        r = bpy.data.images.load(path); ref = bpy.data.images.load(SHEET)
        rw, rh = r.size; sw, sh = ref.size
        a = np.empty(rw * rh * 4, np.float32); r.pixels.foreach_get(a); a = a.reshape(rh, rw, 4)
        b = np.empty(sw * sh * 4, np.float32); ref.pixels.foreach_get(b); b = b.reshape(sh, sw, 4)
        x0, y0, x1, y1 = SHEET_CROP[key]
        crop = b[sh - y1:sh - y0, x0:x1]
        f = rh / crop.shape[0]
        cw = int(crop.shape[1] * f)
        yi = np.clip((np.arange(rh) / f).astype(int), 0, crop.shape[0] - 1)
        xi = np.clip((np.arange(cw) / f).astype(int), 0, crop.shape[1] - 1)
        crop = crop[yi][:, xi]
        both = np.concatenate([crop, np.ones((rh, 20, 4), np.float32), a], axis=1)
        img = bpy.data.images.new("cmp", both.shape[1], rh)
        img.pixels.foreach_set(both.ravel())
        img.filepath_raw = os.path.join(out_dir, key + "_vs_sheet.png"); img.file_format = "PNG"; img.save()
    for sl, m in zip(o.material_slots, saved):
        sl.material = m
    for x in extra + [sun, cam]:
        bpy.data.objects.remove(x, do_unlink=True)
    print("[plants] still", path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["fiddle"]
    keys = list(PLANTS) if argv[0] == "all" else argv[0].split(",")
    if "--preview" in argv:
        k.ROOT = argv[argv.index("--preview") + 1]
    if os.path.exists(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    else:
        for x in list(bpy.data.objects):
            bpy.data.objects.remove(x, do_unlink=True)
    for key in keys:
        col, o, model, host, pot = build(key)
        # in the host's place (rl_kit.export writes it back in the host frame)
        o.matrix_world = k.frame(host)
        if "--no-export" not in argv:
            missing = [m.name for m in o.data.materials if m.name not in k.SLOTS]
            if missing:
                print("[plants] %s NOT exported: slots not in rl_kit yet: %s" % (model, missing))
            else:
                k.export(col.name, model, ZONE, host)
        if "--stills" in argv:
            o.matrix_world = Matrix.Identity(4)
            still(o, model, pot, key, STILLS)
            o.matrix_world = k.frame(host)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


main()
