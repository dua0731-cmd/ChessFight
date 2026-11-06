# Royal water garden - concept-art dressing, sample 4 v1: the east lock (beat 4) and the robot dyke (beat 5) of the v31
# layout (R432).
#
# 승규 11-05: "다음 코스 작업해주고 에셋 이야 레퍼런스 처럼 텍스쳐 입혀줘" with source/ball_and_chain.glb and two reference
# renders of it painted. As before every position and collider stays the v31 graybox's (RoyalWaterGardenBuilder.Beats.cs
# EastLock, RobotDyke, Robot); this only draws over it in the kit's look (R419):
#   the east landing   kit tiles, block walls down to the water, the canal's balustrade on its south edge and its short
#                      north rail
#   the lock           a bed of blue-green channel stone under the graybox's water and current arrows (kept), block walls
#                      either side with a coping; at each gate two stone pilasters on the walls, a lintel across with a
#                      gold crown on both faces, gold slots down the walls the gate runs in
#   Sluice gate 1..3   teal planks in a riveted gold frame, a crown on both faces (ride their movers)
#   the dyke           kit tiles, block walls down to the water on its open south and west edges and over the reservoir
#                      wall on its north edge, the canal's balustrade on the wall at z 30
#   the two robots     승규's ball-and-chain (hammer_asset.py): Robot pillar 1, 2 stand north of the wall over the
#                      gantry's posts; Robot arm 1, 2 (hub, handle, ball) ride the graybox arms
#
# Run:  blender -b --python lock_dyke_sample_v1.py
# Out:  LockDykeSample_v1.blend, render_lockdyke_v1_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Lock dressing"
os.environ["GC_SEED"] = "44"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403
import hammer_asset  # noqa: E402

# Shared with the platforms (same names and values: one Unity material each).
M["teal"] = material("GC | teal paint", (.3, .6, .56), .55)
M["teal_dark"] = material("GC | teal dark", (.1, .28, .28), .75)
M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)
# New here.
# R435 (승규 11-05, "파란색으로 맞춰"): the sluice gates in blue planks (their own material: the platforms keep teal).
M["gate_blue"] = material("GC | gate blue", (.18, .38, .78), .55)
M["gate_blue_dark"] = material("GC | gate blue dark", (.05, .12, .32), .75)
M["moss"] = textured("GC | moss band", STONE_TEX, (.42, .62, .30), .75, .5)
M["channel"] = textured("GC | channel stone", STONE_TEX, (.55, .80, .84), .6, .5)
M["channel_dark"] = material("GC | channel joint", (.16, .36, .40), .8)

import water_tiers as wt  # noqa: E402
UPPER, WATER_Z, RES_TOP = 3.8, wt.MIDDLE, 2.3     # R441: the lake's level from water_tiers.py (context only)
GATES = (13.0, 17.5, 22.0)
ROBOTS = (("1", 26.0), ("2", 13.0))

# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
ctx.box((-10, 27, DEEP), (8, 33, UPPER), M["clay"])                       # the boarding quay (beat 6)
ctx.box((-26, 32.6, DEEP), (26, 33, RES_TOP), M["clay"])                   # the reservoir's south wall
ctx.box((15, 4.5, DEEP), (21, 8.5, 4.1), M["clay"])                        # P8 (as a block)
ctx.done()
water = bpy.data.materials.new("Context | water")
water.use_nodes = True
_p = water.node_tree.nodes["Principled BSDF"]
_p.inputs["Base Color"].default_value = (*srgb((.02, .58, .82)), 1)
_p.inputs["Roughness"].default_value = .06
wm = Mesh("Context water", CTX)
wm.box((-10, -10, WATER_Z - .05), (46.5, 40, WATER_Z), water)
wm.box((-26, 33, 1.9), (26, 48.6, 1.95), water)
wm.done()
# The graybox's lock water and current arrows stay in Unity (drawn here as they are, to see the bed through them).
glass = bpy.data.materials.new("Context | lock water")
glass.use_nodes = True
_g = glass.node_tree.nodes["Principled BSDF"]
_g.inputs["Base Color"].default_value = (*srgb((.55, .85, 1.0)), 1)
_g.inputs["Roughness"].default_value = .05
_g.inputs["Alpha"].default_value = .35
for attr, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
    try:
        setattr(glass, attr, value)
    except Exception:
        pass
lw = Mesh("Context lock water", CTX)
lw.box((27, 10, UPPER + .005), (35, 24, UPPER + .02), glass)
for z in (11.5, 15.2, 19.8, 23.2):
    for x in (29, 31, 33):
        for dx in (-.7, .7):
            beam(lw, (x + dx, z + .7, UPPER + .03), (x, z, UPPER + .03), .075, 0, .01, M["pawn"], 0)
lw.done()

# ------------------------------------------------------------------ shared pieces
stone = Mesh("Lock and dyke stonework")
flowers = Mesh("Lock flowers")


def paving(m, x0, x1, y0, y1, z=UPPER):
    """A flat run of the kit's floor tiles (Tile_Peach, 4 tiles a texture)."""
    vs = [m.bm.verts.new(p) for p in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    f = m.bm.faces.new(vs)
    m.tag(vs, M["paving"])
    m.fixed[f] = {v: (v.co.x / (TILE_SIZE * 4), v.co.y / (TILE_SIZE * 4)) for v in vs}
    m.no_recalc.add(f)


def wall_run(axis, fixed, a0, a1, d0, d1, top, bottom=-2.25, coping=True):
    """Courses of rounded blocks from `bottom` up to `top` (a coping on it), along x or y; d0..d1 across."""
    span = top - (.17 if coping else 0) - bottom
    n = max(1, round(span / .68))
    h = span / n
    # R434 (승규 11-05, "돌벽 사이사이 틈이 넓어"): thin joints (4 cm, 3 cm) over a mortar core, so nothing behind the
    # wall shows through them.
    lo = (a0, fixed + d0 + .05, bottom) if axis == "x" else (fixed + d0 + .05, a0, bottom)
    hi = (a1, fixed + d1 - .05, top - .1) if axis == "x" else (fixed + d1 - .05, a1, top - .1)
    stone.box(lo, hi, M["grout"])
    for ci in range(n):
        zb, zt = bottom + ci * h, bottom + (ci + 1) * h
        a = a0 - (.75 if ci % 2 else 0)
        while a < a1:
            s, e = max(a, a0), min(a + 1.5, a1)
            a += 1.5
            if e - s < .3:
                continue
            lo = (s + .02, fixed + d0, zb + .015) if axis == "x" else (fixed + d0, s + .02, zb + .015)
            hi = (e - .02, fixed + d1, zt - .015) if axis == "x" else (fixed + d1, e - .02, zt - .015)
            stone.box(lo, hi, random.choice(STONES), .035)
    if coping:
        lo = (a0, fixed + d0 - .05, top - .17) if axis == "x" else (fixed + d0 - .05, a0, top - .17)
        hi = (a1, fixed + d1 + .05, top + .012) if axis == "x" else (fixed + d1 + .05, a1, top + .012)
        stone.box(lo, hi, M["stone3"], .04)


# ------------------------------------------------------------------ beat 4: the east landing (x 25.2..35, z 0..10)
paving(stone, 25.2, 35, -.3, 10)
wall_run("x", 0.0, 25.2, 35, -.35, 0, UPPER)          # the south face (under the balustrade, z -.3..0)
wall_run("y", 25.2, -.35, 10.35, -.35, 0, UPPER)      # the west face, towards the platforms
wall_run("x", 10.0, 25.2, 26.4, 0, .35, UPPER)        # the north face west of the lock (water behind)
kit_balustrade(stone, flowers, (25.2, -.15, 0), (35, -.15, 0), lambda p: UPPER)
kit_balustrade(stone, flowers, (25.35, 9.85, 0), (26.4, 9.85, 0), lambda p: UPPER, False)

# ------------------------------------------------------------------ the lock (x 27..35, z 10..24)
# Its bed: blue-green channel slabs (1.6 x 1.0 m, joints staggered) on a dark joint layer.
bed = Mesh("Lock bed")
# R435: the bed 3 cm under the graybox's water picture (UPPER + 0.005; 5 mm apart they flickered).
bed.box((27, 10, UPPER - .15), (35, 24, UPPER - .07), M["channel_dark"])
y = 10.0
row = 0
while y < 24 - 1e-3:
    y1 = min(y + 1.0, 24)
    x = 27.0 - (.8 if row % 2 else 0)
    while x < 35 - 1e-3:
        x1 = min(x + 1.6, 35)
        xs = max(x, 27.0)
        if x1 - xs > .2:
            bed.box((xs + .025, y + .025, UPPER - .13), (x1 - .025, y1 - .025, UPPER - .03), M["channel"], .02)
        x += 1.6
    y, row = y1, row + 1
bed.done()

# The walls, Upper + 1.8 = 5.6 high: W (x 26.4..27, z 10..24), E (x 35..35.6, z -0.3..32.6).
WALL_TOP = UPPER + 1.8
wall_run("y", 26.4, 10, 24, 0, .6, WALL_TOP)
wall_run("y", 35.0, -.3, 32.6, 0, .6, WALL_TOP)

# The gates' frames: a stone pilaster on each wall (z +-0.45, 5.6..9.6), a lintel across (9.6..10.2) with a gold
# crown on both faces, gold ball finials, and gold slots down the walls' inner faces either side of the gate.
frames = Mesh("Lock gate frames")
for z in GATES:
    for x0, x1, inner, sign in ((26.35, 27.05, 27.0, 1), (34.95, 35.65, 35.0, -1)):
        frames.box((x0 - .05, z - .55, WALL_TOP), (x1 + .05, z + .55, WALL_TOP + .22), M["stone2"], .04)       # base
        frames.box((x0 - .03, z - .53, WALL_TOP + .22), (x1 + .03, z + .53, WALL_TOP + .3), GOLD, .02)
        n = 5
        frames.box((x0 + .05, z - .45, WALL_TOP + .3), (x1 - .05, z + .45, 9.4), M["grout"])   # the mortar core (R434)
        for ci in range(n):
            zb = WALL_TOP + .3 + (9.4 - WALL_TOP - .3) * ci / n
            zt = WALL_TOP + .3 + (9.4 - WALL_TOP - .3) * (ci + 1) / n
            frames.box((x0, z - .5, zb + .015), (x1, z + .5, zt - .015), random.choice(STONES[:2]), .035)
        frames.box((x0 - .06, z - .56, 9.4), (x1 + .06, z + .56, 9.6), M["stone3"], .04)                      # capital
        # The slot: two gold strips down the wall's inner face, the gate between them.
        for s in (-1, 1):
            a, b = sorted((inner, inner + sign * .08))   # R435: proud of the pilaster's face (was flush: flickered)
            frames.box((a, z + s * .32, UPPER), (b, z + s * .44, 9.6), GOLD, .01)
    # The lintel and its trims, the crown on both faces, finials over the pilasters.
    frames.box((26.3, z - .5, 9.6), (35.7, z + .5, 10.2), M["stone"], .05)
    for zz in (9.585, 10.125):   # R435: just outside the lintel's top and bottom (coplanar faces flickered)
        frames.box((26.28, z - .53, zz), (35.72, z + .53, zz + .09), GOLD, .02)
    for face in (-1, 1):
        crown_badge(frames, face_frame((31, z + face * .5, 9.92), (0, face, 0)), .3, M["crown_blue"])
        for k in range(-3, 4):
            if k:
                dome(frames, (31 + k * 1.3, z + face * .53, 9.645), (0, face, 0), .035)
    for x in (26.7, 35.3):
        frames.lathe([(0, 10.21), (.3, 10.21), (.3, 10.3), (.12, 10.36), (.1, 10.45), (0, 10.45)], Matrix.Translation((x, z, 0)), GOLD, 20)
        frames.sphere((x, z, 10.62), .2, GOLD, subdiv=3)
frames.done()

# The sluice gates (movers at x 31, z, rest y 3.8; panel x -4..4, 0..2.6 high, 0.6 thick): teal planks between a riveted
# gold frame, a gold band across the middle, the crown on both faces. One object each, named as its mover.
for i, z in enumerate(GATES):
    g = Mesh("Sluice gate %d" % (i + 1))
    g.box((27.15, z - .16, UPPER + .05), (34.85, z + .16, UPPER + 2.55), M["gate_blue_dark"])
    n_planks = 18
    w = (34.75 - 27.25) / n_planks
    for k in range(n_planks):
        xa = 27.25 + k * w
        g.box((xa + .015, z - .24, UPPER + .18), (xa + w - .015, z + .24, UPPER + 2.42), M["gate_blue"], .018)
    for za, zb in ((UPPER, UPPER + .2), (UPPER + 2.4, UPPER + 2.6)):
        g.box((27.0, z - .3, za), (35.0, z + .3, zb), GOLD, .03)
    for xa, xb in ((27.0, 27.28), (34.72, 35.0)):
        g.box((xa, z - .3, UPPER), (xb, z + .3, UPPER + 2.6), GOLD, .03)
    g.box((27.25, z - .27, UPPER + 1.22), (34.75, z + .27, UPPER + 1.36), GOLD, .02)
    for face in (-1, 1):
        crown_badge(g, face_frame((31, z + face * .27, UPPER + 1.62), (0, face, 0)), .42, M["blue"])
        for k in range(10):
            xr = 27.6 + k * (34.4 - 27.6) / 9
            for zr in (UPPER + .1, UPPER + 2.5):
                dome(g, (xr, z + face * .3, zr), (0, face, 0), .04)
        for zr in (UPPER + .5, UPPER + 1.3, UPPER + 2.1):
            for xr in (27.14, 34.86):
                dome(g, (xr, z + face * .3, zr), (0, face, 0), .04)
    g.done()

# ------------------------------------------------------------------ beat 5: the robot dyke (x 8..35, z 24..32.6)
paving(stone, 8, 35, 24, 27)
paving(stone, 10, 35, 27, 32.6)       # x 8..10 north of z 27 is the boarding quay's (its own graybox)
wall_run("x", 24.0, 8, 27, -.35, 0, UPPER)                          # the open south edge over the water
wall_run("y", 8.0, 24, 27, -.35, 0, UPPER)                          # the west end, south of the quay
# R441: out to the reservoir's edge (z 33; it stopped 0.1 short, a crack down to the lake), over the reservoir wall's
# collider (now flush with the dyke, x -10.4 .. 35).
wall_run("x", 32.6, 10, 35, 0, .4, UPPER, RES_TOP - .4)
kit_balustrade(stone, flowers, (10, 30.25, 0), (35, 30.25, 0), lambda p: UPPER)

# The two robots: 승규's ball-and-chain, painted as the reference.
mats = {k: M[k] for k in ("stone", "stone2", "stone3", "moss", "gold", "wood", "blue")}
made = hammer_asset.make_hammers(list(ROBOTS), mats, DRESS)


def pillar_banner(m, x_face, side, yc, top, width=1.3, length=2.4):
    """The reference's banner on a pillar's east (side +1) or west (-1) face: royal blue, swallowtailed, gold rod and
    bands, a gold crown."""
    z0 = top - length
    tail = length * .2

    def piece(y0, y1, d0, d1, za, zb, mat, bev=0.):
        xa, xb = sorted((x_face + side * d0, x_face + side * d1))
        m.box((xa, yc + y0, za), (xb, yc + y1, zb), mat, bev)

    hw = width / 2
    piece(-hw, hw, 0, .04, z0, top, M["blue"])
    piece(-hw, -.05, 0, .04, z0 - tail, z0, M["blue"])
    piece(.05, hw, 0, .04, z0 - tail, z0, M["blue"])
    piece(-hw - .1, hw + .1, -.02, .12, top - .1, top + .04, GOLD, .02)          # the rod
    for k in (-1, 1):
        m.sphere((x_face + side * .05, yc + k * (hw + .16), top - .03), .09, GOLD, subdiv=2)
    piece(-hw, hw, .04, .065, top - .36, top - .29, GOLD)
    piece(-hw, hw, .04, .065, z0 + .06, z0 + .13, GOLD)
    crown_badge(m, face_frame((x_face + side * .04, yc, z0 + length * .45), (side, 0, 0)), .42, M["blue"], depth=.02)


banners = Mesh("Robot banners")
for (suffix, x0), ob in zip(ROBOTS, [o for o in made if o.name.startswith("Robot pillar")]):
    co = np.array([v.co[:] for v in ob.data.vertices])
    body = co[(co[:, 2] > 5) & (co[:, 2] < 8.5) & (co[:, 1] > 29.5)]
    yc = (body[:, 1].min() + body[:, 1].max()) / 2
    print("ROBOT %s pillar x %.2f..%.2f y %.2f..%.2f (all %s..%s)" % (suffix, body[:, 0].min(), body[:, 0].max(),
          body[:, 1].min(), body[:, 1].max(), co.min(0).round(2), co.max(0).round(2)))
    pillar_banner(banners, body[:, 0].max(), 1, yc, 8.75)
    pillar_banner(banners, body[:, 0].min(), -1, yc, 8.75)
banners.done()
stone.done()
flowers.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py in ((30, 4.5), (31, 11.2), (31.5, 19.5), (20, 27), (29.5, 25.5)):
    pawn(scale, px, py, UPPER)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "LockDykeSample_v1.blend"))
shoot("lockdyke_v1", "overview", (48, -8, 24), (24, 18, 4), 26)
shoot("lockdyke_v1", "landing", (18, -3, 8), (31, 12, 5.5), 28)
shoot("lockdyke_v1", "lock_pawn", (31, 7.5, 5.3), (31, 20, 6.3), 26)
shoot("lockdyke_v1", "gate_close", (28.5, 9.0, 5.2), (31.5, 13, 6.2), 30)
shoot("lockdyke_v1", "dyke", (34, 25.2, 7.5), (14, 29, 6), 24)
shoot("lockdyke_v1", "hammer_close", (21, 21, 6.5), (26, 28.5, 6.8), 28)
shoot("lockdyke_v1", "from_shortcut", (14, 16, 6.5), (20, 28, 6.5), 26)
print("LOCK DYKE SAMPLE DONE")
