# Royal water garden - concept-art dressing, sample 3 v3: the bobbing platforms and the shortcut (beat 3) of the v31
# layout (R428).
#
# 승규 11-05: "다음 작업" (after the canal's fountains). As before every position and collider stays the v31 graybox's
# (RoyalWaterGardenBuilder.Beats.cs Platforms, Shortcut); this only draws over it in the kit's look (R419):
#   the west landing   kit tiles, block walls down to the water, the canal's balustrades (gold balls, urns of flowers)
#   P1..P8             floating round stone islands: rings of curved tiles, a coping of rounded blocks over a gold band,
#                      block sides; each rides a bronze piston rod that slides into its pier - why it rises and sinks
#   the piers          round stone columns (static) with gold bands and a bronze collar the rod disappears into
#   K1..K3             small round stepping stones on the same pistons (K1 with a gold coping: the way across)
#   K4                 a tall round block pedestal with the plaza's spin bar in small (hub, gold sprig, two logs)
#   L1, L2             giant lotus leaves (notched, ribbed, a lotus flower at the rim) on green stalks
# The moving pieces are separate objects named as the graybox movers they ride (Bobbing platform P1.., Shortcut post
# K1.., Shortcut post K4 spin bar, Shortcut lotus L1..): Unity hangs each on its mover at rest.
#
# Run:  blender -b --python platforms_sample_v3.py
# Out:  PlatformsSample_v3.blend, render_platforms_v3_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Platforms dressing"
os.environ["GC_SEED"] = "33"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

M["bronze"] = material("GC | bronze", (.62, .42, .2), .35, .85)   # as the canal's nozzles
M["lotus_pink"] = material("GC | lotus pink", (1.0, .62, .78), .55)
M["lotus_white"] = material("GC | lotus white", (1.0, .93, .95), .55)
M["stalk"] = material("GC | lotus stalk", (.26, .52, .2), .6)

import water_tiers as wt  # noqa: E402
WEST_Y, UPPER, WATER_Z = 1.8, 3.8, wt.MIDDLE      # R441: the lake's level (the floating leaves, the stems' bands)


def platform_top(i):
    """PlatformTop(i): the middle of platform i's top, half way up its bob (Blender x, y = section x, z; z up)."""
    return Vector((-21 + 6 * i, 2.5 if i % 2 == 0 else 6.5, 2.35 + .25 * i))


FROM = platform_top(3)
LANDING = Vector((19.4, 24.0))
ALONG = (LANDING - FROM.xy).normalized()


def shortcut_at(s):
    return FROM.xy + ALONG * s


# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
ctx.box((-35.6, -18.8, DEEP), (-35, 8.3, WEST_Y + 1.4), M["clay"])         # the canal's west wall
ctx.box((-35, -12.5, DEEP), (-27, -1, 0), M["clay"])                         # the ramp (as a block)
ctx.box((25.2, 0, DEEP), (35, 10, UPPER), M["clay"])                         # the east landing (beat 4)
ctx.box((8, 24, DEEP), (35, 30, UPPER), M["clay"])                           # the robot dyke (beat 5)
ctx.done()
water = bpy.data.materials.new("Context | water")
water.use_nodes = True
_p = water.node_tree.nodes["Principled BSDF"]
_p.inputs["Base Color"].default_value = (*srgb((.02, .58, .82)), 1)
_p.inputs["Roughness"].default_value = .06
wm = Mesh("Context water", CTX)
wm.box((-46.5, -20, WATER_Z - .05), (46.5, 40, WATER_Z), water)
wm.done()

# ------------------------------------------------------------------ the west landing (x -35..-25.2, z -1..8, top 1.8)
land = Mesh("West landing dressing")
flowers = Mesh("Landing flowers")
vs = [land.bm.verts.new(p) for p in ((-35, -1, WEST_Y), (-25.2, -1, WEST_Y), (-25.2, 8, WEST_Y), (-35, 8, WEST_Y))]
f = land.bm.faces.new(vs)
land.tag(vs, M["paving"])
land.fixed[f] = {v: (v.co.x / (TILE_SIZE * 4), v.co.y / (TILE_SIZE * 4)) for v in vs}
land.no_recalc.add(f)


def wall_run(axis, fixed, a0, a1, d0, d1, top):
    """Courses of rounded blocks from under the water up to `top` (a coping on it), along x or y."""
    n = max(1, round((top - .17 + 2.25) / .68))
    h = (top - .17 + 2.25) / n
    # R434 (승규 11-05, "돌벽 사이사이 틈이 넓어"): 4 cm / 3 cm joints over a mortar core, nothing showing through them.
    lo = (a0, fixed + d0 + .05, -2.25) if axis == "x" else (fixed + d0 + .05, a0, -2.25)
    hi = (a1, fixed + d1 - .05, top - .19) if axis == "x" else (fixed + d1 - .05, a1, top - .19)
    land.box(lo, hi, M["grout"])
    for ci in range(n):
        zb, zt = -2.25 + ci * h, -2.25 + (ci + 1) * h
        a = a0 - (.75 if ci % 2 else 0)
        while a < a1:
            s, e = max(a, a0), min(a + 1.5, a1)
            a += 1.5
            if e - s < .3:
                continue
            lo = (s + .02, fixed + d0, zb + .015) if axis == "x" else (fixed + d0, s + .02, zb + .015)
            hi = (e - .02, fixed + d1, zt - .015) if axis == "x" else (fixed + d1, e - .02, zt - .015)
            land.box(lo, hi, random.choice(STONES), .035)
    lo = (a0, fixed + d0 - .05, top - .17) if axis == "x" else (fixed + d0 - .05, a0, top - .17)
    hi = (a1, fixed + d1 + .08, top + .012) if axis == "x" else (fixed + d1 + .08, a1, top + .012)
    land.box(lo, hi, M["stone3"], .04)


wall_run("y", -25.2, -1.3, 8.3, 0, .35, WEST_Y)     # the east face, towards the platforms
wall_run("x", 8.0, -35, -25.2, 0, .35, WEST_Y)      # the north face, under its rail (z 8..8.3)
wall_run("x", -1.0, -27, -25.2, -.35, 0, WEST_Y)    # the south face, under its rail (z -1.3..-1)
kit_balustrade(land, flowers, (-35, 8.15, 0), (-25.2, 8.15, 0), lambda p: WEST_Y)
kit_balustrade(land, flowers, (-27.15, -1.15, 0), (-25.2, -1.15, 0), lambda p: WEST_Y, False, True)
land.done()

# ------------------------------------------------------------------ P1..P8: floating stone islands on bronze pistons
piers = Mesh("Platform piers")


def pier(cx, cy, r, top, z_bottom=-2.0):
    """A round stone column from under the water to `top`, gold bands, a bronze collar at the top."""
    m = Matrix.Translation((cx, cy, 0))
    piers.lathe([(0, z_bottom), (r, z_bottom), (r, top - .12), (0, top - .12)], m, M["stone2"], 32, True)
    for zb in (WATER_Z + .5, top - .5):
        if zb < top - .2:
            piers.lathe([(r + .01, zb), (r + .035, zb + .02), (r + .035, zb + .08), (r + .01, zb + .1)], m, GOLD, 32, True, True)
    piers.lathe([(0, top - .14), (r + .07, top - .14), (r + .07, top - .02), (r + .03, top), (0, top)], m, M["bronze"], 32, True)


# v2 (R429, 승규 11-05, a reference render: "지름 큰 건 이렇게 모델링 해서 교체 해"): each big platform is a gold-bound teal
# tub - a stone top of soft tiles round a royal-blue crown inlaid in gold, a broad gold rim, teal wooden staves between
# a gold top band and a gold foot band, eight riveted gold clamps over the rim down to gold feet. Colours measured off
# the reference: top stone (239, 212, 191), crown (99, 127, 186), staves (72, 135, 127), gold (211, 155, 66).
M["tub_stone"] = textured("GC | platform stone", STONE_TEX, (1.0, .92, .84), .7, .5)
M["teal"] = material("GC | teal paint", (.3, .6, .56), .55)
M["teal_dark"] = material("GC | teal dark", (.1, .28, .28), .75)
M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)
CROWN = [(-.8, -.45), (.8, -.45), (.86, .45), (.42, .06), (0, .62), (-.42, .06), (-.86, .45)]   # x across, y up, metres
CROWN_BALLS = [(-.86, .55), (0, .74), (.86, .55)]


def offset_polygon(pts, d):
    """Each corner pushed out by d along its bisector (mitred), for an outline round a polygon."""
    out = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2 = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[(i + 1) % n])
        e0, e1 = (p1 - p0).normalized(), (p2 - p1).normalized()
        n0, n1 = Vector((e0.y, -e0.x)), Vector((e1.y, -e1.x))   # outward for a counter-clockwise polygon
        b = (n0 + n1)
        if b.length < 1e-6:
            b = n0
        b.normalize()
        cos_half = max(.35, b.dot(n0))
        q = p1 + b * (d / cos_half)
        out.append((q.x, q.y))
    return out


def crown(m, cx, cy, z, scale, heading):
    """The crown inlaid flush in the top: gold outline, royal blue fill, gold balls on its points."""
    frame = Matrix.Translation((cx, cy, z)) @ Matrix.Rotation(heading, 4, "Z")
    pts = [(x * scale, y * scale) for x, y in CROWN]
    m.slab(offset_polygon(pts, .07), frame, -.004, .006, GOLD)
    m.slab(pts, frame, .006, .011, M["crown_blue"])
    for bx, by in CROWN_BALLS:
        c = frame @ Vector((bx * scale, by * scale, 0))
        m.lathe([(0, -.004), (.15 * scale, -.004), (.15 * scale, .013), (0, .013)], Matrix.Translation(c), GOLD, 24, False)
    # A gold band across the crown's foot.
    m.slab([(-.8 * scale, -.33 * scale), (.8 * scale, -.33 * scale), (.8 * scale, -.24 * scale), (-.8 * scale, -.24 * scale)],
           frame, .006, .013, GOLD)


def tub(i):
    """v3 (R430, 승규: "기둥 없애주고 레퍼런스 사진 대로"): no pier and no piston - the tub floats and bobs on its own,
    built as the reference, side by side (compare_tub_v3.png), 2.4 m tall for its 6 m: a thick cream stone lid (0.7 m,
    its side in blocks under a thin gold rim) over a gold band, teal staves set in under it (1.2 m), six riveted gold
    clamps over the lid's edge (a teal stud on each) and six gold feet below them (0.35 m). On top the crown's disc
    and one ring of twelve soft tiles."""
    c = platform_top(i)
    top = c.z
    m = Mesh("Bobbing platform P%d" % (i + 1))
    at = Matrix.Translation((c.x, c.y, 0))
    z_lid, z_band, z_floor, z_feet = top - .7, top - .85, top - 2.05, top - 2.4
    # The top: the crown's disc, a ring of twelve soft tiles, light joints under them, a thin raised gold rim.
    m.lathe([(0, top - .06), (1.6, top - .06), (1.6, top - .02), (1.58, top), (0, top)], at, M["tub_stone"], 64, False)
    for k in range(12):
        a0 = 30.0 * k
        m.arc(1.63, 2.8, a0 + .45, a0 + 30 - .45, top - .06, top, M["tub_stone"], center=(c.x, c.y), bevel=.03, segs=8)
    m.lathe([(0, top - .07), (2.82, top - .07), (2.82, top - .05), (0, top - .05)], at, M["grout"], 48, False)
    crown(m, c.x, c.y, top, 1.5, -math.pi / 2)   # upright to a pawn going east (its points to the east)
    m.lathe([(2.8, top - .03), (2.82, top + .018), (3.03, top + .018), (3.07, top - .01), (3.07, top - .07), (2.95, top - .07)],
            at, GOLD, 72, False, True)
    # The lid's side: twelve stone blocks under the rim (their joints under the top tiles' joints).
    for k in range(12):
        a0 = 30.0 * k
        m.arc(2.85, 3.05, a0 + .25, a0 + 30 - .25, z_lid + .02, top - .07, M["tub_stone"], center=(c.x, c.y), bevel=.035, segs=6)
    # The gold band under the lid.
    m.lathe([(2.9, z_band), (3.06, z_band), (3.1, z_band + .03), (3.1, z_lid - .01), (3.06, z_lid + .02), (2.9, z_lid + .02)],
            at, GOLD, 72, False, True)
    # Teal staves set in under the band, a dark core behind their joints, the tub's floor.
    m.lathe([(0, z_band), (2.86, z_band), (2.86, z_floor), (0, z_floor)], at, M["teal_dark"], 48, False)
    n_staves = 40
    for k in range(n_staves):
        a0 = 360.0 * k / n_staves
        m.arc(2.86, 2.98, a0 + .4, a0 + 360.0 / n_staves - .4, z_floor + .01, z_band - .01, M["teal"], center=(c.x, c.y),
              bevel=.016, segs=1)
    # Six clamps over the lid's edge and six feet under them.
    for k in range(6):
        ph = math.radians(30 + 60 * k)
        rot = at @ Matrix.Rotation(ph, 4, "Z")

        def part(lo, hi, bevel, mat=GOLD):
            v = m.box(lo, hi, mat, bevel)
            for vv in v:
                vv.co = rot @ vv.co

        part((2.7, -.42, top - .01), (3.16, .42, top + .06), .022)          # the clamp's cap on the rim
        part((3.05, -.42, z_band - .04), (3.17, .42, top + .055), .028)     # its face down the lid's side, over the band
        # A teal stud ringed in gold on the face, a gold rivet on the cap.
        face = rot @ Vector((3.16, 0, (top + z_lid) / 2))
        frame = Matrix.Translation(face) @ (rot.to_3x3() @ Matrix.Rotation(math.pi / 2, 3, "Y")).to_4x4()
        m.lathe([(0, 0), (.11, 0), (.11, .02), (.09, .035), (0, .035)], frame, GOLD, 20)
        m.lathe([(0, .03), (.07, .03), (.055, .06), (0, .07)], frame, M["teal"], 16)
        for dy in (-.2, .2):
            m.lathe([(0, 0), (.05, 0), (.04, .028), (0, .04)], Matrix.Translation(rot @ Vector((2.95, dy, top + .055))), GOLD, 12)
        # The foot: a gold boot under the staves, wider at the ground, lipped at the top.
        pts = [(2.8, -.44), (3.13, -.44), (3.13, .44), (2.8, .44)]
        lo = [m.bm.verts.new(rot @ Vector((x * 1.01, y * 1.12, z_feet))) for x, y in pts]
        hi = [m.bm.verts.new(rot @ Vector((x, y, z_floor + .42))) for x, y in pts]
        m.bm.faces.new(list(reversed(lo)))
        m.bm.faces.new(hi)
        for q in range(4):
            r = (q + 1) % 4
            m.bm.faces.new((lo[q], lo[r], hi[r], hi[q]))
        m.tag(lo + hi, GOLD, False, {e for v in lo + hi for e in v.link_edges}, .035)
        part((2.82, -.48, z_floor + .36), (3.17, .48, z_floor + .46), .025)  # the boot's lip
    m.done()


for i in range(8):
    tub(i)

# ------------------------------------------------------------------ the shortcut: K1..K3 stepping stones, K4, L1, L2
POSTS = [(5.8, 3.25), (9.3, 3.35), (12.8, 3.45)]
for k, (s, base) in enumerate(POSTS):
    xy = shortcut_at(s)
    top = base + .4
    st = Mesh("Shortcut post K%d" % (k + 1))
    tile_disc(st, xy.x, xy.y, top, .5, .5)
    m = Matrix.Translation((xy.x, xy.y, 0))
    if k == 0:
        # K1 wears a gold coping so the way across is seen from the platforms.
        st.lathe([(.48, top - .1), (.66, top - .1), (.67, top - .02), (.64, top + .015), (.5, top + .015), (.48, top)], m, GOLD, 40, True, True)
    else:
        ring_blocks(st, xy.x, xy.y, .48, .66, top - .1, top + .012, .7, [M["stone3"]], .025)
        st.lathe([(.6, top - .17), (.64, top - .16), (.64, top - .11), (.6, top - .1)], m, GOLD, 40, True, True)
    ring_blocks(st, xy.x, xy.y, .5, .6, top - .6, top - .1, .8, STONES[:2], .03)
    st.lathe([(0, top - .62), (.58, top - .62), (.58, top - .6), (0, top - .6)], m, M["stone_dark"], 24, False)
    st.lathe([(0, top - 1.7), (.18, top - 1.7), (.18, top - .62), (0, top - .62)], m, M["bronze"], 20, True)
    st.done()
    pier(xy.x, xy.y, .25, base - .7)

# K4: a tall round pedestal (static, r 1.3, top 3.4) and its small spin bar (riding the mover).
k4 = shortcut_at(16.9)
ped = Mesh("Shortcut K4 pedestal")
tile_disc(ped, k4.x, k4.y, 3.4, 1.14, .6)
ring_blocks(ped, k4.x, k4.y, 1.12, 1.36, 3.28, 3.412, .9, [M["stone3"]], .03)
ped.lathe([(1.27, 3.2), (1.34, 3.21), (1.34, 3.26), (1.27, 3.27)], Matrix.Translation((k4.x, k4.y, 0)), GOLD, 48, True, True)
n_courses = round((3.2 + 2.25) / .6)
for ci in range(n_courses):
    z0 = -2.25 + (3.2 + 2.25) * ci / n_courses
    z1 = -2.25 + (3.2 + 2.25) * (ci + 1) / n_courses
    ring_blocks(ped, k4.x, k4.y, 1.16, 1.3, z0, z1, 1.0, STONES[:2], .04, .5 * (ci % 2))
ped.done()
bar = Mesh("Shortcut post K4 spin bar")
at = Vector((k4.x, k4.y, 3.4))
face_r = hub(bar, at, .44, 8, .13, .92, [90, 270], .58)
for deg in (0, 180):
    frame = Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, "Z") @ Matrix.Translation((0, 0, .40)) \
        @ Matrix.Rotation(math.pi / 2, 4, "Y")
    log(bar, frame, .175, .2, 1.23, face_r)
bar.done()


def lotus_flower(m, at, size):
    """A pink lotus: two rings of pointed petals round a yellow seed head."""
    for ring, (count, tilt, length, mat) in enumerate(((8, 55, size, M["lotus_pink"]), (8, 25, size * .8, M["lotus_white"]))):
        for p in range(count):
            ph = 2 * math.pi * (p + .5 * ring) / count
            frame = Matrix.Translation(at) @ Matrix.Rotation(ph, 4, "Z") @ Matrix.Rotation(math.radians(90 - tilt), 4, "Y")
            pts = [(0, 0), (length * .35, length * .2), (length * .8, length * .16), (length, 0), (length * .8, -length * .16),
                   (length * .35, -length * .2)]
            m.slab(pts, frame, -.006, .006, mat)
    m.lathe([(0, 0), (size * .22, 0), (size * .2, size * .16), (0, size * .18)], Matrix.Translation(at), M["heart"], 16)


for k, s in enumerate((21.2, 25.1)):
    xy = shortcut_at(s)
    top = 3.3
    pad = Mesh("Shortcut lotus L%d" % (k + 1))
    m = Matrix.Translation((xy.x, xy.y, 0))
    # A lotus leaf (it stands on its stalk, round, no notch): a shallow cup of 0.9 m, its rim wavy and a little raised,
    # its underside an umbrella of ribs down to the boss the stalk holds - the pad's 0.4 m depth under it.
    n, rings = 64, 5
    centre = pad.bm.verts.new((xy.x, xy.y, top + .015))
    grid = []
    for i in range(1, rings + 1):
        f = i / rings
        row = []
        for j in range(n):
            ph = 2 * math.pi * j / n
            r = .9 * f * (1 + .035 * math.sin(9 * ph + k) * f)
            row.append(pad.bm.verts.new((xy.x + r * math.cos(ph), xy.y + r * math.sin(ph), top + .015 + .07 * f ** 2.2)))
        grid.append(row)
    for j in range(n):
        pad.bm.faces.new((centre, grid[0][j], grid[0][(j + 1) % n]))
    for i in range(rings - 1):
        for j in range(n):
            a, b = grid[i][j], grid[i][(j + 1) % n]
            pad.bm.faces.new((a, grid[i + 1][j], grid[i + 1][(j + 1) % n], b))
    pad.tag([centre] + [v for row in grid for v in row], M["lily"], True)
    # The rim rolls under, then the underside runs in and down to the boss.
    under = [pad.bm.verts.new((v.co.x, v.co.y, v.co.z - .035)) for v in grid[-1]]
    boss = [pad.bm.verts.new((xy.x + .16 * math.cos(2 * math.pi * j / n), xy.y + .16 * math.sin(2 * math.pi * j / n), top - .36))
            for j in range(n)]
    for j in range(n):
        jj = (j + 1) % n
        pad.bm.faces.new((grid[-1][j], grid[-1][jj], under[jj], under[j]))
        pad.bm.faces.new((under[j], under[jj], boss[jj], boss[j]))
    pad.tag(under + boss, M["leaf3"], True)
    pad.lathe([(.16, top - .36), (.2, top - .42), (.2, top - .6), (0, top - .6)], m, M["stalk"], 16, True)
    # Ribs radiating over the top.
    for j in range(0, n, 4):
        tip = grid[-1][j].co
        mid = grid[rings // 2][j].co
        beam(pad, Vector((xy.x, xy.y, top + .02)), Vector((mid.x, mid.y, mid.z + .005)), .016, 0, .014, M["leaf2"], .005)
        beam(pad, Vector((mid.x, mid.y, mid.z + .005)), Vector((tip.x, tip.y, tip.z + .005)), .013, 0, .012, M["leaf2"], .005)
    pad.done()
    # The stalk the leaf stands on (static: the leaf tips on it), and a lotus flower on its own stalk beside it.
    piers.lathe([(0, -2.0), (.24, -2.0), (.2, top - .62), (.24, top - .6), (0, top - .6)], m, M["stalk"], 16, True)
    side = math.atan2(ALONG.y, ALONG.x) + (1.9 if k == 0 else -1.9)
    fx, fy = xy.x + 1.25 * math.cos(side), xy.y + 1.25 * math.sin(side)
    bloom_z = 1.4 + .35 * k
    piers.lathe([(0, -2.0), (.06, -2.0), (.045, bloom_z), (0, bloom_z)], Matrix.Translation((fx, fy, 0)), M["stalk"], 10, True)
    lotus_flower(piers, Vector((fx, fy, bloom_z)), .38)
    # Two small leaves floating round the stalk's foot.
    for q in range(2):
        ph = side + 1.3 + q * 2.6
        cx, cy = xy.x + .7 * math.cos(ph), xy.y + .7 * math.sin(ph)
        piers.lathe([(0, WATER_Z + .01), (.32, WATER_Z + .01), (.32, WATER_Z + .03), (0, WATER_Z + .03)],
                    Matrix.Translation((cx, cy, 0)), M["lily"], 20, False)
piers.done()
flowers.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((-30, 3.5, WEST_Y), (-15, 6.5, platform_top(1).z), (k4.x + .7, k4.y - .5, 3.4)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "PlatformsSample_v3.blend"))
shoot("platforms_v3", "overview", (-2, -16, 15), (-4, 8, 1.5), 26)
shoot("platforms_v3", "landing", (-23, -6, 5.5), (-18, 4, 2.2), 26)
shoot("platforms_v3", "platform_close", (-11.2, -1.2, 4.2), (-15, 6.5, 2.0), 30)
shoot("platforms_v3", "tub_close", (-17.2, -2.6, 3.4), (-21, 2.5, 1.7), 34)
shoot("platforms_v3", "pawn_view", (-20.2, 5.2, 4.3), (-15, 6.5, 2.4), 30)
shoot("platforms_v3", "shortcut", (-2, 4, 7), (12, 18, 3.2), 28)
shoot("platforms_v3", "lotus_close", (11.6, 16.6, 4.8), (14.4, 20.0, 3.3), 32)
print("PLATFORMS SAMPLE DONE")
