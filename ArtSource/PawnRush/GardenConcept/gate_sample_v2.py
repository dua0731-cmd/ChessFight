# Royal water garden - concept-art dressing, sample 7 v2: the gate and the way out (beat 7) of the v31 layout (R458).
#
# v1 (R456) had a triumphal arch in the gate yard and a smaller arch at the exit. 승규 11-05, with three pictures of a
# garden gate (a round arch with a crowned gold shield, two towers with blue pyramid roofs, flags and crowned banners,
# urn pillars full of flowers, flowering vines, planters, moss-green footings): "앞쪽 문 지워주고 이걸로 똑같이 모델링 해",
# then "앞문 지우고 뒷문을 이걸로": the yard's arch is gone, and the exit arch (graybox piers x +-6..7 at z 66.1..66.9)
# is that gate, in its proportions measured off the front picture in units of its opening (the exit's 12 m):
#   opening 1, springing 0.85, crown 1.35 (a semicircle); the jambs 0.21 wide; the ring thickening from 0.22 at the
#   springing to 0.42 at the crown (a rose voussoir archivolt inside a cream band); the shield 0.28 x 0.3
#   towers 0.41 square, a gold band over a moss footing (0.18), the shaft to 1.33, a gold band with the banner rod,
#   a wider head and cornice to 1.63, a gold-edged blue pyramid roof to 2.02, a gold pole and ball to 2.32, a flag
#   banners 0.33 wide, 0.92 long to a point, gold edged, a gold crown
#   wings: a wall 0.42 long to 1.02 (vines down it, a planter of flowers before it), an urn pillar 0.35 square to 1.08
#   with a flowering urn to 1.52, a low end block to 0.93
# so the gate is 12 m wide inside, about 31 m high to the flags and 49 m across; it stands on a paved podium (x 6..25
# either side of the band, the band's height) down to the ground. What is in a pawn's reach stays flush with the
# graybox (the jambs' faces are the opening's sides, x +-6); the rest is out past the rails.
# Everything else is v1's: the yard, its balustrades and runner, the terraces, the north wall by the opening, the exit
# terrace and its medallion, the steps and the band (the balustrades now end clear of the gate's jambs).
# All faces outward and no two faces in one plane (PITFALLS 38, 39).
#
# R483 (승규 11-06, "fantasy castle arch 3d model.glb" + its painted picture: "깃발 제거 해주고 성문 쪽 에셋 바꾸고 똑같이
# 텍스쳐 입혀" - all the new gate's flags and banners gone): with ASSET_GATE the exit gate is 승규's Tripo gate as
# gate_arch_asset.py paints it (GateArchAsset_v1.blend: no flags, no banners, the gate's own materials by part), 50.6
# times its size so its wings end where the hand-built one's did (x +-24.8, where the castle wall starts) - its opening
# 14.2 m (x +-7.09, the jambs' faces), the arch's crown 19.4, the roofs' eaves 24.4 and spires 32.5, its towers x
# 9.1 .. 14.1 and 2.4 m either side of its middle plane (z 64.1 .. 68.9). It stands on the podium, which reaches 2.4 m
# further north for the planters on its back. The hand-built gate below is still built but not added (gate.done()).
#
# Run:  blender -b --python gate_sample_v2.py
# Out:  GateSample_v2.blend, render_gate_v2_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Gate dressing"
os.environ["GC_SEED"] = "77"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)     # as the platforms'
M["gate_blue"] = material("GC | gate blue", (.18, .38, .78), .55)      # as the lock's sluice gates
M["moss"] = textured("GC | moss band", STONE_TEX, (.42, .62, .30), .75, .5)   # as the lock's
M["rose_dark"] = material("GC | rose joint", (.78, .48, .42), .8)
M["shield"] = material("GC | shield olive", (.47, .54, .27), .6)
import water_tiers as wt  # noqa: E402


# R460 (승규 "레퍼런스 사진 색이랑 이음새 부분이 다르잖아 ... 우리껀 한국형 바닥 돌 타일 이야 택스쳐도 좀 달라 보여"): the gate's
# stone as the picture's (sampled there: median sRGB 251 222 190, light 253 233 214), one stone for every block, soft
# deeper blotches inside each block, no tone change from block to block; big blocks (a course about 1.7 m, the end
# block one stone a course, the urn pillar two) with rounded edges and thin joints; the picture's sage moss and
# yellower gold.
def gate_stone_texture():
    """2 m of the reference gate's stone (sRGB): pale peach, soft deeper blotches, a fine grain, a few specks."""
    n = 1024
    t = np.clip(.7 + .3 * periodic_noise(n, n, 31, 2.3, 2), 0, 1)
    t = t * t * (3 - 2 * t)
    light, deep = np.array([.99, .905, .82]), np.array([.965, .845, .735])
    rgb = deep * (1 - t[..., None]) + light * t[..., None]
    rgb = rgb * (1 + .012 * periodic_noise(n, n, 32, .4, 80))[..., None]
    pits = np.random.default_rng(33).random((n, n))
    rgb[pits < .0012] *= .87
    return save_image("Stone_Gate", np.clip(rgb, 0, 1))


GATE_TEX = gate_stone_texture()
GS = textured("GC | gate stone", GATE_TEX, (1.0, 1.0, 1.0), .72, .5)
GJ = material("GC | gate joint", (.80, .65, .53), .85)
GM = textured("GC | gate moss", STONE_TEX, (.72, .76, .48), .78, .5)
GG = material("GC | gate gold", (.98, .82, .30), .3, .75)
M["rose"] = textured("GC | rose stone", GATE_TEX, (1.0, .86, .84), .7, .5)    # paler, as the picture's

UPPER, RES_Y = 3.8, wt.LATE
BAND = UPPER - 1.0
U = 12.0            # the gate's opening, its unit
G = BAND            # its ground
YC = 66.5           # its middle plane (the graybox exit arch's)

# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
# R472: the old north wall is gone (walls_sample_v1.py's castle wall runs from the gate's wings instead).
ctx.box((-44, 33, -2), (-26, 48.6, UPPER), M["clay"])            # the reservoir's terraces
ctx.box((26, 33, -2), (44, 48.6, UPPER), M["clay"])
ctx.box((-26, 33, -2), (-18, 48.6, 7.3), M["clay"])              # the boat tunnels (R446: roof to 7.3)
ctx.box((18, 33, -2), (26, 48.6, 7.3), M["clay"])
ctx.box((-14, 72.5, -2), (14, 90, BAND - .4), M["clay"])         # the mini-game beyond
ctx.box((-60, 62, -2.5), (60, 100, -.02), M["clay"])             # the ground outside the north wall
ctx.done()
wat = bpy.data.materials.new("Context | reservoir")
wat.use_nodes = True
wat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb((.03, .5, .78)), 1)
wat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .06
wm = Mesh("Context water", CTX)
wm.box((-26, 33, RES_Y - .05), (26, 48.6, RES_Y), wat)
wm.done()


# ------------------------------------------------------------------ helpers
def frame_on(at, out, up=Vector((0, 0, 1))):
    """An orthonormal frame at `at`: z = out, y as near `up` as it goes, x = y cross z."""
    z = Vector(out).normalized()
    x = Vector(up).cross(z)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    y = z.cross(x)
    return Matrix.Translation(Vector(at)) @ Matrix((x, y, z)).transposed().to_4x4()


def loft2d(m, inner, outer, y0, y1, mat, bevel=0.):
    """A solid between two curves of the elevation ((x, z) lists of one length), from y0 to y1 (its sharp edges
    rounded by `bevel`)."""
    vs = []

    def row(pts, y):
        r = [m.bm.verts.new((x, y, z)) for x, z in pts]
        vs.extend(r)
        return r
    fi, fo, bi, bo = row(inner, y0), row(outer, y0), row(inner, y1), row(outer, y1)
    new = m.bm.faces.new
    for i in range(len(inner) - 1):
        j = i + 1
        new((fi[i], fi[j], fo[j], fo[i]))
        new((bo[i], bo[j], bi[j], bi[i]))
        new((bi[i], bi[j], fi[j], fi[i]))
        new((fo[i], fo[j], bo[j], bo[i]))
    new((fi[0], fo[0], bo[0], bi[0]))
    new((bi[-1], bo[-1], fo[-1], fi[-1]))
    edges = None
    if bevel:
        for v in vs:
            v.select = False
        edges = {e for v in vs for e in v.link_edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > .6}
    m.tag(vs, mat, False, edges, bevel)


def tube(m, pts, r, mat, segs=8):
    """A closed tube along a few points (gold edges, poles)."""
    pts = [Vector(p) for p in pts]
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        n = t.orthogonal().normalized()
        b = t.cross(n)
        rings.append([m.bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * r)
                      for a in (2 * math.pi * k / segs for k in range(segs))])
    for ra, rb in zip(rings, rings[1:]):
        for k in range(segs):
            m.bm.faces.new((ra[k], ra[(k + 1) % segs], rb[(k + 1) % segs], rb[k]))
    m.bm.faces.new(rings[0])
    m.bm.faces.new(list(reversed(rings[-1])))
    m.tag([v for r in rings for v in r], mat, True)


def gate_run(m, axis, fixed, a0, a1, d0, d1, top, bottom, course=1.7, block=2.2):
    """The gate's masonry (R460): courses of about `course`, blocks of about `block` (one a course on a narrow face,
    staggered where two or more fit), rounded edges, 2.4 cm joints over a darker mortar core."""
    span = top - bottom
    n = max(1, round(span / course))
    h = span / n
    lo = (a0 + .03, fixed + d0 + .05, bottom) if axis == "x" else (fixed + d0 + .05, a0 + .03, bottom)
    hi = (a1 - .03, fixed + d1 - .05, top - .03) if axis == "x" else (fixed + d1 - .05, a1 - .03, top - .03)
    m.box(lo, hi, GJ)
    nb = max(1, round((a1 - a0) / block))
    bw = (a1 - a0) / nb
    for ci in range(n):
        zb, zt = bottom + ci * h + .012, bottom + (ci + 1) * h - .012
        shift = .5 * bw if (ci % 2 and nb > 1) else 0.
        a = a0 - shift
        while a < a1 - 1e-6:
            s_, e_ = max(a, a0), min(a + bw, a1)
            a += bw
            if e_ - s_ < .25:
                continue
            lo = (s_ + .012, fixed + d0, zb) if axis == "x" else (fixed + d0, s_ + .012, zb)
            hi = (e_ - .012, fixed + d1, zt) if axis == "x" else (fixed + d1, e_ - .012, zt)
            m.box(lo, hi, GS, .07)


def block_faces(m, x0, x1, y0, y1, bottom, top, faces="snwe", course=1.7, block=2.2):
    """The gate's masonry on the chosen faces of a box (s y0, n y1, w x0, e x1), 0.3 m deep."""
    if "s" in faces:
        gate_run(m, "x", y0, x0, x1, 0, .3, top, bottom, course, block)
    if "n" in faces:
        gate_run(m, "x", y1, x0, x1, -.3, 0, top, bottom, course, block)
    if "w" in faces:
        gate_run(m, "y", x0, y0 + .3, y1 - .3, 0, .3, top, bottom, course, block)
    if "e" in faces:
        gate_run(m, "y", x1, y0 + .3, y1 - .3, -.3, 0, top, bottom, course, block)


def mirror_x(lo, hi, s):
    """A box given for the east side (x > 0), put on side s."""
    xa, xb = sorted((s * lo[0], s * hi[0]))
    return (xa, lo[1], lo[2]), (xb, hi[1], hi[2])


def sbox(m, s, lo, hi, mat, bevel=0.):
    a, b = mirror_x(lo, hi, s)
    m.box(a, b, mat, bevel)


# R465 (승규 "꽃들 더 다듬어 주고"): rounder leaves, a stem through the vines, smaller layered blossoms in clusters, buds.
def round_leaf(m, base, direction, length, width, mat, up):
    """A rounded leaf (an egg-shaped blade, its tip drawn out, folded a little along the midrib), facing `up`."""
    d = Vector(direction).normalized()
    side = d.cross(Vector(up))
    if side.length < 1e-4:
        side = d.orthogonal()
    side.normalize()
    normal = side.cross(d).normalized()
    b = Vector(base)
    ts = (.0, .18, .4, .62, .82, 1.0)
    rows = []
    for t in ts:
        w = width * .5 * (math.sin(math.pi * min(1., t * 1.08)) ** .75 if t < 1 else 0)
        mid = b + d * length * t + normal * width * .1 * math.sin(math.pi * t)
        rows.append((mid - side * w - normal * w * .18, mid, mid + side * w - normal * w * .18))
    vs = []
    faces = []
    prev = None
    for l, c, r in rows:
        cur = [m.bm.verts.new(p) for p in (l, c, r)]
        vs += cur
        if prev:
            for q in ((prev[0], cur[0], cur[1], prev[1]), (prev[1], cur[1], cur[2], prev[2])):
                if len({v.co.to_tuple(6) for v in q}) == 4:
                    faces.append(m.bm.faces.new(q))
                else:
                    faces.append(m.bm.faces.new([v for i, v in enumerate(q) if v.co.to_tuple(6) not in
                                                 {w.co.to_tuple(6) for w in q[:i]}]))
        prev = cur
    i = m.mi(mat)
    for fc in faces:
        fc.normal_update()
        if fc.normal.dot(normal) < 0:
            fc.normal_flip()
        fc.material_index = i
        fc.smooth = True
    m.no_recalc.update(faces)


def blossom(m, at, size, mat, facing, rnd):
    """A five-petal flower; often a second, smaller ring over it (fuller, as the picture's)."""
    m.flower(at, size, mat, facing)
    if rnd.random() < .42:
        m.flower(at + Vector(facing).normalized() * size * .12, size * .62, rnd.choice([mat, M["white"]]), facing)


def vine(m, top, length, face_y, out, seed):
    """Flowering ivy hanging from `top` down a face (its plane y = face_y, `out` -1 south / +1 north): a green stem
    swaying down, rounded leaves both sides, blossoms in twos and threes, buds."""
    rnd = random.Random(seed)
    pts = []
    z = top.z
    while z > top.z - length:
        pts.append(Vector((top.x + .3 * math.sin((top.z - z) * 1.2 + seed) + rnd.uniform(-.05, .05), face_y + out * .08, z)))
        z -= .22
    tube(m, pts, .035, M["leaf3"], 6)
    for k, p in enumerate(pts):
        for side in (-1, 1):
            if rnd.random() < .85:
                d = Vector((side * rnd.uniform(.6, 1.0), out * rnd.uniform(.15, .4), -rnd.uniform(.2, .7)))
                round_leaf(m, p, d, rnd.uniform(.32, .46), rnd.uniform(.26, .34), rnd.choice([M["leaf"], M["leaf2"], M["leaf3"]]),
                           Vector((0, out, .35)))
        if k % 3 == 1 or rnd.random() < .12:
            for j in range(rnd.choice((1, 2, 2, 3))):
                q = p + Vector((rnd.uniform(-.2, .2), out * (.12 + .03 * j), rnd.uniform(-.15, .15)))
                blossom(m, q, rnd.uniform(.1, .15), rnd.choice([M["pink"], M["white"], M["pink"], M["yellow"], M["coral"]]),
                        Vector((rnd.uniform(-.3, .3), out, rnd.uniform(.1, .5))), rnd)
        elif rnd.random() < .25:
            m.sphere(p + Vector((rnd.uniform(-.15, .15), out * .1, 0)), .045, rnd.choice([M["pink"], M["white"]]), subdiv=1)


def planter(m, x0, x1, y0, y1, z0, h):
    """A stone planter box, its rim round a bed of soil."""
    m.box((x0, y0, z0), (x1, y1, z0 + h - .15), M["stone3"], .04)
    m.box((x0, y0, z0 + h - .15), (x1, y0 + .12, z0 + h), M["stone3"], .02)
    m.box((x0, y1 - .12, z0 + h - .15), (x1, y1, z0 + h), M["stone3"], .02)
    m.box((x0, y0 + .12, z0 + h - .15), (x0 + .12, y1 - .12, z0 + h), M["stone3"], .02)
    m.box((x1 - .12, y0 + .12, z0 + h - .15), (x1, y1 - .12, z0 + h), M["stone3"], .02)
    m.box((x0 + .12, y0 + .12, z0 + h - .15), (x1 - .12, y1 - .12, z0 + h - .06), M["soil"])


def flower_mound(m, centre, rx, ry, height, count, seed):
    """A heaped bed of leaves and flowers (a planter's or an urn's): a dark leafy core rx x ry x height, rounded leaves
    standing out all over it, blossoms in small clusters (some layered), buds between."""
    rnd = random.Random(seed)
    c = Vector(centre)
    dome = [(0, 0), (1.0, 0)] + [(math.cos(t * math.pi / 16), math.sin(t * math.pi / 16)) for t in range(1, 8)] + [(0, 1.0)]
    m.lathe(dome, Matrix.Translation(c) @ Matrix.Diagonal((rx * .96, ry * .96, height * .96, 1)), M["leaf3"], 24)

    def on_dome(a, r, lift):
        z = height * math.sqrt(max(0., 1 - r * r))
        p = c + Vector((math.cos(a) * r * rx, math.sin(a) * r * ry, z))
        n = Vector((math.cos(a) * r / rx, math.sin(a) * r / ry, z / (height * height) if height else 1)).normalized()
        return p + n * lift, n
    golden = math.pi * (3 - math.sqrt(5))
    n_l = int(rx * ry * 44)
    for i in range(n_l):
        r = math.sqrt((i + .5) / n_l) * .99
        p, n = on_dome(golden * i, r, rnd.uniform(-.04, .06))
        side = Vector((0, 0, 1)).cross(n)
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
        d = (n * .7 + side.normalized() * rnd.uniform(-1, 1) + n.cross(side).normalized() * rnd.uniform(-1, 1)).normalized()
        round_leaf(m, p, d, rnd.uniform(.3, .44), rnd.uniform(.24, .32), rnd.choice([M["leaf2"], M["leaf"], M["leaf"], M["leaf3"]]),
                   n + Vector((0, 0, .3)))
    colours = [M["pink"], M["white"], M["yellow"], M["pink"], M["coral"], M["white"], M["pink"]]
    for i in range(count):
        a, r = rnd.uniform(0, 2 * math.pi), math.sqrt(rnd.uniform(0, 1)) * .97
        col = rnd.choice(colours)
        for j in range(rnd.choice((1, 2, 2, 3))):
            p, n = on_dome(a + rnd.uniform(-.12, .12) / max(r, .2), min(.99, r + rnd.uniform(-.05, .05)), .08 + .03 * j)
            blossom(m, p, rnd.uniform(.12, .2), col if rnd.random() < .7 else rnd.choice(colours), n + Vector((0, 0, .5)), rnd)
    for i in range(count):
        a, r = rnd.uniform(0, 2 * math.pi), math.sqrt(rnd.uniform(0, 1)) * .95
        p, n = on_dome(a, r, .06)
        m.sphere(p, rnd.uniform(.04, .06), rnd.choice([M["pink"], M["white"], M["yellow"]]), subdiv=1)


GATE_FLOWERS = False   # R467 (승규 "지워", the exit gate's flowers): no vines, no flowers in the urns and planters
ASSET_GATE = True      # R483: the exit gate is 승규's Tripo gate (gate_arch_asset.py), the hand-built one is not added
GATE_ASSET = os.path.join(os.path.dirname(os.path.abspath(__file__)), "GateArchAsset_v1.blend")
GATE_K = 24.8 / .49    # the asset's wings end at x +-0.49 -> the hand-built wings' ends (x +-24.8)
PODIUM_N = 5.8 if ASSET_GATE else 3.4   # the podium's north edge from the gate's middle (the asset's planters 5.3)
stone = Mesh("Gate stonework")
flowers = Mesh("Gate flowers")
gate = Mesh("Exit gate")
NORTH_WALL = False      # R472: the north wall between the yard and the gate goes (the castle wall takes the gate in)
wall = Mesh("North wall") if NORTH_WALL else None

# ------------------------------------------------------------------ the gate yard (x -14..14, z 48.6..60.5) and the terraces (v1)
kit_paving(stone, -14, 14, 48.6, 60.5, UPPER)
kit_wall_run(stone, "x", 48.6, -18, 18, 0, .32, UPPER, RES_Y - .35)          # its face over the reservoir
# R474 (the castle wall's ring, walls_sample_v2.py, cuts the terraces' outer corners): the terraces are dressed there
# now, clipped to the ring; here only the yard's balustrades.
TERRACES_HERE = False
for s in (-1, 1):
    kit_balustrade(stone, flowers, (s * 14.15, 48.95, 0), (s * 14.15, 60.15, 0), lambda p: UPPER)
    if not TERRACES_HERE:
        continue
    xa, xb = sorted((s * 14.3, s * 44))
    kit_paving(stone, xa, xb, 48.6, 59, UPPER)
    kit_wall_run(stone, "x", 59, xa, xb, -.3, 0, UPPER, 0)                     # the terraces' north edge
    d0, d1 = (-.3, 0) if s > 0 else (0, .3)
    kit_wall_run(stone, "y", s * 44, 48.6, 58.7, d0, d1, UPPER, 0)              # R462: their outer ends (by the trench)
    for y in (50.6, 53.3, 56.0, 58.4):                                         # topiaries in gold-rimmed pots
        t = Matrix.Translation((s * 16.6, y, UPPER))
        stone.lathe([(0, 0), (.3, 0), (.38, .5), (.42, .55), (0, .55)], t, M["stone3"], 20)
        stone.lathe([(0, .5), (.43, .5), (.43, .58), (0, .58)], t, GOLD, 20)
        flowers.lathe([(0, .55), (.36, .62), (.3, .95), (.2, 1.35), (.08, 1.65), (0, 1.75)], t, M["leaf3"], 14)
        flowers.sphere((s * 16.6, y, UPPER + 1.85), .1, GOLD, subdiv=1)
# The runner: royal blue, gold borders, from the water's edge through the wall to the finish medallion.
for y0, y1, z in ((49.0, 60.5, UPPER), (60.5, 62.3, UPPER)):
    stone.box((-1.4, y0, z), (1.4, y1, z + .006), M["blue"])
    for s in (-1, 1):
        xa, xb = sorted((s * 1.4, s * 1.56))
        stone.box((xa, y0, z), (xb, y1, z + .008), GOLD)
crown_badge(stone, face_frame((0, 51.6, UPPER + .006), (0, 0, 1), (0, 1, 0)), .75, M["crown_blue"])


def finial(m, at, ball=.32):
    t = Matrix.Translation(at)
    m.lathe([(0, 0), (.42, 0), (.42, .12), (.3, .2), (.24, .4), (.32, .5), (0, .5)], t, M["stone3"], 24)
    m.lathe([(0, .5), (.16, .5), (.1, .58), (0, .58)], t, GOLD, 16)
    m.sphere(Vector(at) + Vector((0, 0, .58 + ball)), ball, GOLD, subdiv=3)


# ------------------------------------------------------------------ the north wall by the opening (v1)
# R462: in Unity the garden's north wall stays graybox (it runs on to x +-46.5, undressed), so its faces here stand
# 2 cm proud of the graybox's (60.5, 62, the opening's sides at x +-6.3), a joint-coloured plate between, not in its
# planes (PITFALLS 39).
for s in ((-1, 1) if NORTH_WALL else ()):
    xa, xb = sorted((s * 6.3, s * 14.3))
    kit_wall_run(wall, "x", 60.48, xa, xb, 0, .3, 8.83, UPPER, False)           # the yard's side
    wall.box((xa, 60.488, UPPER), (xb, 60.496, 8.83), M["grout"])
    kit_wall_run(wall, "x", 62.02, xa, xb, -.3, 0, 8.83, 0, False)             # the outside
    wall.box((xa, 62.004, 0), (xb, 62.012, 8.83), M["grout"])
    d0, d1 = (0, .3) if s > 0 else (-.3, 0)
    kit_wall_run(wall, "y", s * 6.28, 60.5, 62, d0, d1, 8.83, 0, False)        # the opening's jamb
    ja, jb = sorted((s * 6.288, s * 6.296))
    wall.box((ja, 60.5, 0), (jb, 62, 8.83), M["grout"])
    ca, cb = sorted((s * 6.28, s * 14.35))                                       # the coping, to the jamb's face
    wall.box((ca, 60.45, 8.83), (cb, 62.05, 9.012), M["stone3"], .04)
    xj = s * 6.9
    wall.box((xj - .6, 60.45, 3.8), (xj + .6, 60.48, 8.83), M["stone3"])          # pilasters by the opening (5 cm proud)
    wall.box((xj - .66, 60.42, 8.4), (xj + .66, 60.48, 8.6), GOLD, .01)
    finial(wall, (xj, 61.25, 9.012), .36)
    for x in (s * 10.3, s * 13.7):
        wall.lathe([(0, 0), (.16, 0), (.1, .1), (0, .1)], Matrix.Translation((x, 61.25, 9.012)), GOLD, 12)
        wall.sphere((x, 61.25, 9.012 + .26), .2, GOLD, subdiv=2)

if not NORTH_WALL:
    # R472: without the wall the yard's north edge (x +-6 .. 14.3, z 60.5) shows: a block face down to the strip behind
    # it (2.8, walls_sample_v1.py) and the canal's balustrade along it.
    for s in (-1, 1):
        xa, xb = sorted((s * 6.0, s * 14.3))
        kit_wall_run(stone, "x", 60.5, xa, xb, -.3, 0, UPPER, 2.4)
        kit_balustrade(stone, flowers, (s * 13.85, 60.35, 0), (s * 7.0, 60.35, 0), lambda p: UPPER, True, True)

# ------------------------------------------------------------------ the exit terrace (x -6..6, z 60.5..66.5), steps, band (v1)
kit_paving(stone, -6, 6, 60.5, 66.3, UPPER)
stone.box((-6, 66.3, UPPER - .25), (6, 66.5, UPPER + .012), M["stone3"], .02)   # its edge over the first step
for s in (-1, 1):
    # v2: the balustrades stop clear of the gate's jambs (z 64.7 .. 68.3).
    kit_balustrade(stone, flowers, (s * 6.15, 60.75 if not NORTH_WALL else 62.15, 0), (s * 6.15, 64.3, 0), lambda p: UPPER, False)
    d0, d1 = (-.3, 0) if s > 0 else (0, .3)
    kit_wall_run(stone, "y", s * 6, 60.5, 66.5, d0, d1, UPPER, 0, False)       # no coping: the tiles lap it
    kit_wall_run(stone, "y", s * 6, 66.5, 72.5, d0, d1, BAND, 0)
    kit_balustrade(stone, flowers, (s * 6.15, 68.7, 0), (s * 6.15, 72.4, 0), lambda p: BAND, False)
# The finish medallion (CP 끝): a royal blue disc in a gold ring, a crown.
stone.arc(1.9, 2.15, 0, 360, UPPER + .006, UPPER + .016, GOLD, (0, 63.5))
pts = [(math.cos(2 * math.pi * k / 48) * 1.9, math.sin(2 * math.pi * k / 48) * 1.9) for k in range(48)]
stone.slab(pts, Matrix.Translation((0, 63.5, 0)), UPPER + .006, UPPER + .012, M["blue"])
crown_badge(stone, face_frame((0, 63.4, UPPER + .012), (0, 0, 1), (0, 1, 0)), .9, M["crown_blue"])
for i in range(1, 5):
    y0, y1, t = 66.5 + (i - 1) * .5, 66.5 + i * .5, UPPER - .25 * i
    stone.box((-6, y0, t - .25), (6, y1, t), M["stone3"] if i % 2 else M["stone2"], .02)
kit_paving(stone, -6, 6, 68.5, 72.5, BAND)

# ------------------------------------------------------------------ the exit gate (v2, the reference)
# Sizes (east side; the west mirrors): the opening x +-6; jambs 6 .. 8.6; towers 8.6 .. 13.5; the wall 13.5 .. 18.5;
# the urn pillar 18.5 .. 22.6; the low end block 22.6 .. 24.8.
JX1, TX0, TX1 = 8.6, 8.6, 13.5
WX1, PX1, LX1 = 18.5, 22.6, 24.8
JY, TY, WY, PY, LY = 1.8, 2.45, 1.2, 2.1, 1.2       # half depths
FOOT = .18 * U                                       # the moss footing (2.16)
SPRING, R_IN = G + .85 * U, .5 * U                   # 13.0, 6.0
R_MID = R_IN + 2.0
E_C, E_R = G + 1.028 * U, .742 * U                   # the extrados circle (thickens to the crown)
SHAFT_TOP = G + 1.33 * U                             # 18.76
HEAD0, HEAD1 = SHAFT_TOP + .5, G + 1.55 * U          # 19.26 .. 21.4
CORNICE1 = G + 1.63 * U                              # 22.36
ROOF0, APEX = CORNICE1 + .55, G + 2.02 * U           # 22.91 .. 27.04

# The podium the gate stands on: paved at the band's height, block walls down to the ground.
for s in (-1, 1):
    xa, xb = sorted((s * 6.0, s * 25.2))
    kit_paving(stone, xa, xb, YC - 3.4, YC + PODIUM_N, G)
    gate_run(stone, "x", YC - 3.4, xa, xb, 0, .3, G - .2, 0, 1.4)
    gate_run(stone, "x", YC + PODIUM_N, xa, xb, -.3, 0, G - .2, 0, 1.4)
    d0, d1 = (-.3, 0) if s > 0 else (0, .3)
    gate_run(stone, "y", s * 25.2, YC - 3.1, YC + PODIUM_N - .3, d0, d1, G - .2, 0, 1.4)
    stone.box((xa - (.06 if s < 0 else 0), YC - 3.46, G - .2), (xb + (.06 if s > 0 else 0), YC + PODIUM_N + .06, G - .012),
              GS, .04)

for s in (-1, 1):
    # The jamb: flush with the opening (x 6), a moss footing, a gold band, block courses, an impost.
    sbox(gate, s, (6.0, YC - JY - .1, G), (JX1, YC + JY + .1, G + FOOT), GM, .04)
    sbox(gate, s, (6.0, YC - JY - .06, G + FOOT), (JX1, YC + JY + .06, G + FOOT + .3), GG, .015)
    xa, xb = sorted((s * 6.0, s * JX1))
    block_faces(gate, xa, xb, YC - JY, YC + JY, G + FOOT + .3, SPRING - .45, "sn" + ("w" if s > 0 else "e"))
    sbox(gate, s, (5.84, YC - JY - .16, SPRING - .45), (JX1, YC + JY + .16, SPRING), GS, .05)
    sbox(gate, s, (5.8, YC - JY - .2, SPRING - .52), (JX1, YC + JY + .2, SPRING - .45), GG, .01)

    # The tower: moss footing, gold band, block shaft, the banner band, a wider head and cornice, the roof, pole and flag.
    sbox(gate, s, (TX0, YC - TY - .1, G), (TX1 + .1, YC + TY + .1, G + FOOT), GM, .05)
    sbox(gate, s, (TX0, YC - TY - .14, G + FOOT), (TX1 + .14, YC + TY + .14, G + FOOT + .3), GG, .015)
    xa, xb = sorted((s * TX0, s * TX1))
    block_faces(gate, xa, xb, YC - TY, YC + TY, G + FOOT + .3, SHAFT_TOP, "sn" + ("e" if s > 0 else "w"))
    # its side toward the opening (the jamb, shallower, covers its middle low down; the ring keeps clear of it)
    gate_run(gate, "y", s * TX0, YC - TY + .3, YC + TY - .3, *((0, .3) if s > 0 else (-.3, 0)), SHAFT_TOP,
             G + FOOT + .3)
    sbox(gate, s, (TX0 - .1, YC - TY - .1, SHAFT_TOP), (TX1 + .1, YC + TY + .1, HEAD0), GG, .02)
    sbox(gate, s, (TX0 - .3, YC - TY - .3, HEAD0), (TX1 + .3, YC + TY + .3, HEAD0 + .4), GS, .05)
    sbox(gate, s, (TX0 - .2, YC - TY - .2, HEAD0 + .4), (TX1 + .2, YC + TY + .2, HEAD1), GS, .04)
    sbox(gate, s, (TX0 - .55, YC - TY - .55, HEAD1), (TX1 + .55, YC + TY + .55, CORNICE1 - .2), GS, .08)
    sbox(gate, s, (TX0 - .4, YC - TY - .4, CORNICE1 - .2), (TX1 + .4, YC + TY + .4, CORNICE1), GS, .04)
    sbox(gate, s, (TX0 - .1, YC - TY - .1, CORNICE1), (TX1 + .1, YC + TY + .1, ROOF0), GG, .02)
    xc = s * (TX0 + TX1) / 2
    half = (TX1 - TX0) / 2 + .2
    roof = Matrix.Translation((xc, YC, 0)) @ Matrix.Rotation(math.pi / 4, 4, "Z")
    gate.lathe([(0, ROOF0), (half * 1.414 + .25, ROOF0), (half * 1.414, ROOF0 + .35), (0, APEX)], roof, M["gate_blue"], 4, False)
    h = APEX - ROOF0 - .35
    for cx, cy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        corner = Vector((xc + cx * (half + .17), YC + cy * (half + .17), ROOF0 + .02))
        tube(gate, [corner, Vector((xc + cx * half, YC + cy * half, ROOF0 + .35)), Vector((xc, YC, APEX - .05))], .1, GG, 8)
    for out in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0)):
        o = Vector(out)
        n = (o * h + Vector((0, 0, half))).normalized()
        at = Vector((xc, YC, ROOF0 + .35)) + o * half * .5 + Vector((0, 0, h * .5)) + n * .03
        gate.slab([(0, .45), (.32, 0), (0, -.45), (-.32, 0)], frame_on(at, n), 0, .07, GG)
    t = Matrix.Translation((xc, YC, 0))
    gate.lathe([(0, APEX - .55), (.55, APEX - .55), (.7, APEX - .25), (.45, APEX + .1), (.32, APEX + .45), (.42, APEX + .6),
                (0, APEX + .6)], t, GG, 16)
    gate.lathe([(0, APEX + .6), (.14, APEX + .6), (.14, APEX + 3.4), (0, APEX + 3.4)], t, GG, 10)
    gate.lathe([(0, APEX + 2.0), (.24, APEX + 2.0), (.24, APEX + 2.2), (0, APEX + 2.2)], t, GG, 12)
    gate.sphere((xc, YC, APEX + 3.75), .4, GG, subdiv=3)
    flag = [(0, 1.3), (2.6, 1.15), (3.9, 1.35), (3.1, .7), (3.9, .02), (2.6, .2), (0, 0)]
    f = Matrix.Translation((xc + .1, YC, APEX + 1.75)) @ Matrix.Rotation(math.radians(-12), 4, "Z") \
        @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    gate.slab(flag, f, -.04, .04, M["gate_blue"])

    # The banners, south and north: a gold rod in blue loops on the gold band, the cloth to a point, gold edged, a crown.
    for side in (-1, 1):
        face = YC + side * TY
        n = Vector((0, side, 0))
        rod_y = face + side * .32
        x0, x1 = sorted((s * (TX0 + .35), s * (TX1 - .35)))
        tube(gate, [(x0, rod_y, SHAFT_TOP + .25), (x1, rod_y, SHAFT_TOP + .25)], .11, GG, 10)
        for x in (x0, x1):
            gate.sphere((x, rod_y, SHAFT_TOP + .25), .2, GG, subdiv=2)
        for k in range(5):
            x = xc + (k - 2) * .85
            gate.box((x - .17, rod_y - .17, SHAFT_TOP + .08), (x + .17, rod_y + .17, SHAFT_TOP + .42), M["gate_blue"], .04)
        cloth = [(-2.0, 0), (2.0, 0), (2.0, -9.4), (0, -11.0), (-2.0, -9.4)]
        top = Vector((xc, rod_y - side * .05, SHAFT_TOP + .1))
        gate.slab(offset_polygon(list(reversed(cloth)), .2), frame_on(top - n * .04, n), 0, .06, GG)
        gate.slab(cloth, frame_on(top, n), 0, .08, M["gate_blue"])
        crown_badge(gate, frame_on(top + n * .081 + Vector((0, 0, -6.0)), n), 1.15, GG, GG)

    # The wing: a wall with vines and a planter, an urn pillar, a low end block, all on moss footings.
    sbox(gate, s, (TX1 + .1, YC - WY - .1, G), (WX1, YC + WY + .1, G + FOOT), GM, .04)
    xa, xb = sorted((s * TX1, s * WX1))
    block_faces(gate, xa, xb, YC - WY, YC + WY, G + FOOT, G + 1.02 * U - .4, "sn")
    sbox(gate, s, (TX1, YC - WY - .2, G + 1.02 * U - .4), (WX1 + .1, YC + WY + .2, G + 1.02 * U), GS, .04)
    for i, vx in enumerate((14.6, 17.3) if GATE_FLOWERS else ()):
        for side in (-1, 1):
            vine(flowers, Vector((s * vx, 0, G + 1.02 * U - .3)), 5.8 if i else 4.6, YC + side * (WY + .2), side,
                 int(vx * 10) + side + (3 if s > 0 else 0))
    xa, xb = sorted((s * 14.2, s * 17.9))
    planter(gate, xa, xb, YC - WY - 1.75, YC - WY - .08, G, 1.15)
    if GATE_FLOWERS:
        flower_mound(flowers, (s * 16.05, YC - WY - .92, G + 1.05), 1.85, .82, .85, 40, 11 + (s > 0))

    sbox(gate, s, (WX1, YC - PY - .1, G), (PX1 + .1, YC + PY + .1, G + FOOT), GM, .05)
    sbox(gate, s, (WX1, YC - PY - .14, G + FOOT), (PX1 + .14, YC + PY + .14, G + FOOT + .25), GG, .015)
    xa, xb = sorted((s * WX1, s * PX1))
    block_faces(gate, xa, xb, YC - PY, YC + PY, G + FOOT + .25, G + 1.03 * U, "snwe")
    sbox(gate, s, (WX1 - .3, YC - PY - .3, G + 1.03 * U), (PX1 + .3, YC + PY + .3, G + 1.08 * U - .2), GS, .05)
    sbox(gate, s, (WX1 - .1, YC - PY - .1, G + 1.08 * U - .2), (PX1 + .1, YC + PY + .1, G + 1.08 * U), GS, .03)
    ux = s * (WX1 + PX1) / 2
    urn = Matrix.Translation((ux, YC, G + 1.08 * U))
    gate.lathe([(0, 0), (1.0, 0), (1.0, .25), (.62, .42), (.55, .95), (.85, 1.15), (1.75, 1.65), (2.1, 2.2), (2.2, 2.5),
                (0, 2.5)], urn, GS, 28)
    gate.lathe([(0, 2.42), (2.27, 2.42), (2.27, 2.58), (0, 2.58)], urn, GG, 28)
    if GATE_FLOWERS:
        flower_mound(flowers, (ux, YC, G + 1.08 * U + 2.58), 2.25, 2.25, 2.0, 46, 21 + (s > 0))
    else:
        gate.lathe([(0, 2.58), (2.05, 2.58), (2.05, 2.63), (0, 2.63)], urn, M["soil"], 28)  # soil inside the gold rim

    sbox(gate, s, (PX1 + .1, YC - LY - .1, G), (LX1 + .1, YC + LY + .1, G + FOOT), GM, .04)
    xa, xb = sorted((s * PX1, s * LX1))
    block_faces(gate, xa, xb, YC - LY, YC + LY, G + FOOT, G + .93 * U - .45, "sn" + ("e" if s > 0 else "w"))
    sbox(gate, s, (PX1, YC - LY - .25, G + .93 * U - .45), (LX1 + .25, YC + LY + .25, G + .93 * U - .1), GS, .05)
    sbox(gate, s, (PX1, YC - LY - .1, G + .93 * U - .1), (LX1 + .1, YC + LY + .1, G + .93 * U), GS, .03)

# The arch: a rose voussoir archivolt (6 .. 8 m round the springing's middle) with a joint line, inside a cream band out
# to the extrados (a circle centred higher, so the ring thickens to the crown), the soffit's depth the jambs'.


def ray_extrados(phi):
    d = Vector((math.cos(phi), math.sin(phi)))
    b = -(E_C - SPRING) * d.y
    c = (E_C - SPRING) ** 2 - E_R ** 2
    return -b + math.sqrt(b * b - c)


n_v, steps = 17, 5
for k in range(n_v):
    p0, p1 = math.pi * k / n_v + .004, math.pi * (k + 1) / n_v - .004
    phis = [p0 + (p1 - p0) * i / steps for i in range(steps + 1)]
    inner = [(R_IN * math.cos(p), SPRING + R_IN * math.sin(p)) for p in phis]
    outer = [(R_MID * math.cos(p), SPRING + R_MID * math.sin(p)) for p in phis]
    loft2d(gate, inner, outer, YC - JY, YC + JY, M["rose"], .04)
phis = [math.pi * i / 64 for i in range(65)]
loft2d(gate, [((R_IN + .04) * math.cos(p), SPRING + (R_IN + .04) * math.sin(p)) for p in phis],
       [((R_MID - .04) * math.cos(p), SPRING + (R_MID - .04) * math.sin(p)) for p in phis], YC - JY + .04, YC + JY - .04,
       M["rose_dark"])
loft2d(gate, [((R_MID + .02) * math.cos(p), SPRING + (R_MID + .02) * math.sin(p)) for p in phis],
       [((ray_extrados(p) - .04) * math.cos(p), SPRING + (ray_extrados(p) - .04) * math.sin(p)) for p in phis],
       YC - JY - .1, YC + JY + .1, GJ)
for y0, y1 in ((YC - JY - .012, YC - JY + .01), (YC + JY - .01, YC + JY + .012)):
    loft2d(gate, [((R_IN + .96) * math.cos(p), SPRING + (R_IN + .96) * math.sin(p)) for p in phis],
           [((R_IN + 1.04) * math.cos(p), SPRING + (R_IN + 1.04) * math.sin(p)) for p in phis], y0, y1, M["rose_dark"])
n_o = 11
for k in range(n_o):
    p0, p1 = math.pi * k / n_o + .003, math.pi * (k + 1) / n_o - .003
    phis = [p0 + (p1 - p0) * i / steps for i in range(steps + 1)]
    inner = [((R_MID - .02) * math.cos(p), SPRING + (R_MID - .02) * math.sin(p)) for p in phis]
    outer = [(ray_extrados(p) * math.cos(p), SPRING + ray_extrados(p) * math.sin(p)) for p in phis]
    loft2d(gate, inner, outer, YC - JY - .14, YC + JY + .14, GS, .06)

# The shield on the keystone, both faces: a gold shield with a crested top and balls, an olive field, a gold crown.
SHIELD = [(-1.75, 1.05), (-1.2, 1.45), (-.62, 1.15), (0, 1.8), (.62, 1.15), (1.2, 1.45), (1.75, 1.05), (1.75, -.25),
          (1.25, -1.25), (0, -1.9), (-1.25, -1.25), (-1.75, -.25)]
SHIELD_Z = SPRING + R_IN + 2.2
for side in (-1, 1):
    n = Vector((0, side, 0))
    base = Vector((0, YC + side * (JY + .14), SHIELD_Z))
    gate.slab(SHIELD, frame_on(base, n), 0, .45, GG)
    gate.slab([(x * .74, y * .74 - .1) for x, y in SHIELD], frame_on(base, n), .45, .5, M["shield"])
    crown_badge(gate, frame_on(base + n * .5 + Vector((0, 0, -.2)), n), 1.05, GG, GG)
    for bx, bz in ((-1.2, 1.45), (0, 1.8), (1.2, 1.45)):
        gate.sphere(base + n * .22 + Vector((bx, 0, bz + .12)), .2, GG, subdiv=2)

stone.done()
flowers.done()
if not ASSET_GATE:
    gate.done()
if wall:
    wall.done()


def asset_gate():
    """R483: 승규's gate (GateArchAsset_v1.blend) at the exit, its front (the shield, the vines) toward the yard, its
    materials the ones made here of the same names, box UVs in metres as Mesh.done's, shaded smooth but for edges sharper
    than 40 degrees (the decimated surface reads as facets otherwise)."""
    with bpy.data.libraries.load(GATE_ASSET, link=False) as (src, dst):
        dst.meshes = list(src.meshes)
    me = max(dst.meshes, key=lambda m: len(m.polygons))
    for i, m in enumerate(me.materials):
        base = m.name.rsplit(".", 1)[0] if m.name[-4:-3] == "." else m.name
        if base != m.name and base in bpy.data.materials:
            me.materials[i] = bpy.data.materials[base]
    me.transform(Matrix.Translation((0, YC, G)) @ Matrix.Scale(GATE_K, 4))
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(40))
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        n = p.normal
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co.x, co.y) if abs(n.z) > .7 else ((co.y, co.z) if abs(n.x) > abs(n.y) else (co.x, co.z))
    me.name = "Exit gate"
    ob = bpy.data.objects.new("Exit gate", me)
    DRESS.objects.link(ob)
    print("GATE ASSET", len(me.polygons), "faces", [m.name for m in me.materials])


if ASSET_GATE:
    asset_gate()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((0, 52.5, UPPER), (-2.2, 57.5, UPPER), (2, 64.0, UPPER + .016), (-1.5, 70.5, BAND)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "GateSample_v2.blend"))
shoot("gate_v2", "overview", (34, 34, 36), (0, 62, 10), 24)
shoot("gate_v2", "front", (0, 30, 16), (0, 66.5, 15.5), 24)            # straight on, from over the reservoir
shoot("gate_v2", "reference", (-30, 38, 16), (-1, 66.5, 14.5), 26)    # as the reference picture, three-quarter left
shoot("gate_v2", "yard", (0, 46, 6.5), (0, 66, 12), 22)
shoot("gate_v2", "through", (0, 61.2, 6.0), (0, 70, 13), 20)
shoot("gate_v2", "north", (10, 86, 12), (0, 66.5, 14), 24)
shoot("gate_v2", "wing", (24, 50, 9), (16, 66.5, 11), 28)
print("GATE SAMPLE DONE")
