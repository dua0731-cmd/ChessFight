# Royal water garden - concept-art dressing, sample 7 v1: the gate and the way out (beat 7) of the v31 layout (R456).
#
# 승규 11-05: "다음 작업" (after the boat crossing). As before every position and collider stays the v31 graybox's
# (RoyalWaterGardenBuilder.Beats.cs Gate, the gate terraces of Shell); this only draws over it in the kit's look (R419):
#   the gate yard     kit tiles, block walls down to the reservoir on its south edge, the canal's balustrade on its
#                     west and east rails, a royal blue runner with gold borders and a crown from the water to the exit
#   the gate arch     a triumphal arch on the graybox's piers and lintel: rusticated piers, gold-filleted imposts, a
#                     basket arch of voussoirs with a crowned keystone, royal blue banners, an entablature with a blue
#                     and gold frieze of crowns, an attic and a great gold crown on top
#   the north wall    (x -14.3 .. 14.3) block faces both sides and on the opening's jambs, a coping, gold balls by
#                     the opening
#   the exit terrace  kit tiles, the runner on to a gold and blue finish medallion (CP 끝; the graybox marker), its
#                     balustrades, block walls on its sides
#   the exit arch     a smaller arch the same way (piers to the band below), a pediment with a crown
#   the steps / band  four stone steps with nosings, the entrance band in kit tiles with balustrades, block sides
#   the terraces      either side of the yard (x 14.3 .. 44): kit tiles, a block wall on their north edge, topiaries
# Scenery over the way stays out of it: what stands in reach of a pawn is flush with the graybox (6 cm at most proud).
# All faces outward and no two faces in one plane (PITFALLS 38, 39).
#
# Run:  blender -b --python gate_sample_v1.py
# Out:  GateSample_v1.blend, render_gate_v1_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Gate dressing"
os.environ["GC_SEED"] = "77"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)     # as the platforms'
import water_tiers as wt  # noqa: E402

UPPER, RES_Y = 3.8, wt.LATE
BAND = UPPER - 1.0

# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
ctx.box((-46.5, 60.5, -2), (-14.3, 62, 9), M["clay"])            # the rest of the north wall
ctx.box((14.3, 60.5, -2), (46.5, 62, 9), M["clay"])
ctx.box((-44, 33, -2), (-26, 48.6, UPPER), M["clay"])            # the reservoir's terraces
ctx.box((26, 33, -2), (44, 48.6, UPPER), M["clay"])
ctx.box((-26, 33, -2), (-18, 48.6, 7.3), M["clay"])              # the boat tunnels (R446: roof to 7.3)
ctx.box((18, 33, -2), (26, 48.6, 7.3), M["clay"])
ctx.box((-14, 72.5, -2), (14, 90, BAND - .4), M["clay"])         # the mini-game beyond
ctx.done()
wat = bpy.data.materials.new("Context | reservoir")
wat.use_nodes = True
wat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb((.03, .5, .78)), 1)
wat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .06
wm = Mesh("Context water", CTX)
wm.box((-26, 33, RES_Y - .05), (26, 48.6, RES_Y), wat)
wm.done()


def elev(y0):
    """An elevation frame: local x = world x, local y = height, local z = world y from y0 (into the gate)."""
    return Matrix(((1, 0, 0, 0), (0, 0, 1, y0), (0, 1, 0, 0), (0, 0, 0, 1)))


def strip_solid(m, xs, lo, hi, y0, y1, mat):
    """A solid from height lo(x) to hi(x) over the samples xs, from y0 to y1 (an arch's spandrels)."""
    f = [(m.bm.verts.new((x, y0, lo(x))), m.bm.verts.new((x, y0, hi(x)))) for x in xs]
    b = [(m.bm.verts.new((x, y1, lo(x))), m.bm.verts.new((x, y1, hi(x)))) for x in xs]
    new = m.bm.faces.new
    for i in range(len(xs) - 1):
        new((f[i][0], f[i + 1][0], f[i + 1][1], f[i][1]))
        new((b[i][1], b[i + 1][1], b[i + 1][0], b[i][0]))
        new((f[i][1], f[i + 1][1], b[i + 1][1], b[i][1]))
        new((b[i][0], b[i + 1][0], f[i + 1][0], f[i][0]))
    new((f[0][0], f[0][1], b[0][1], b[0][0]))
    new((f[-1][1], f[-1][0], b[-1][0], b[-1][1]))
    m.tag([v for pair in f + b for v in pair], mat)


def arch(m, half, y0, y1, spring, rise, top, ring_side, keystone_scale):
    """A basket arch between piers at +-half: the spandrels up to `top`, voussoirs 5 cm proud of both faces, a crowned
    keystone."""
    xs = [half * math.cos(math.pi * (1 - k / 48)) for k in range(49)]

    def intrados(x):
        return spring + rise * math.sqrt(max(0., 1 - (x / half) ** 2))
    strip_solid(m, xs, intrados, lambda x: top, y0, y1, M["stone"])
    n = 13
    ao, bo = half + ring_side, top - spring
    for k in range(n):
        t0, t1 = math.pi * k / n + .006, math.pi * (k + 1) / n - .006
        pts = [((half - .03) * math.cos(t), spring + (rise - .03) * math.sin(t)) for t in (t0, t1)]
        pts = [pts[0], (ao * math.cos(t0), spring + bo * math.sin(t0)), (ao * math.cos(t1), spring + bo * math.sin(t1)), pts[1]]
        key = k == n // 2
        m.slab(pts, elev(y0 - .05), 0, (y1 - y0) + .1, M["stone3"] if key else STONES[k % 2])
    for y, out in ((y0 - .052, (0, -1, 0)), (y1 + .052, (0, 1, 0))):
        crown_badge(m, face_frame((0, y, spring + rise + .5 * (top - spring - rise)), out), keystone_scale, M["crown_blue"])


def pier(m, flowers, x0, x1, y0, y1, base, plinth, impost, top, inner, banner_top=None):
    """A rusticated pier dressing the graybox pier x0..x1, y0..y1: a plinth (6 cm proud, flush on the opening's side
    `inner` = x0 or x1), block courses flush and inset in turn over a mortar core, an impost with a gold fillet, a plain
    upper pier with a pilaster on its north face, a royal blue banner on its south face."""
    px0 = x0 if inner == x0 else x0 - .06
    px1 = x1 if inner == x1 else x1 + .06
    m.box((px0, y0 - .06, base), (px1, y1 + .06, plinth), M["stone_dark"], .03)
    m.box((x0 + .05, y0 + .05, plinth), (x1 - .05, y1 - .05, impost), M["grout"])
    n = max(1, round((impost - plinth) / .55))
    h = (impost - plinth) / n
    for ci in range(n):
        ins = .04 if ci % 2 else 0.
        m.box((x0 + ins, y0 + ins, plinth + ci * h + .015), (x1 - ins, y1 - ins, plinth + (ci + 1) * h - .015),
              random.choice(STONES[:2]), .045)
    m.box((x0 - .1, y0 - .1, impost), (x1 + .1, y1 + .1, impost + .28), M["stone3"], .04)
    m.box((x0 - .12, y0 - .12, impost + .28), (x1 + .12, y1 + .12, impost + .33), GOLD, .01)
    m.box((x0, y0, impost + .33), (x1, y1, top), M["stone2"])
    xc, w = (x0 + x1) / 2, (x1 - x0) / 2 - .2
    m.box((xc - w, y1, impost + .33), (xc + w, y1 + .05, top - .3), M["stone3"], .015)
    m.box((xc - w - .07, y1, top - .3), (xc + w + .07, y1 + .09, top - .15), GOLD, .01)
    if banner_top:
        banner_at(m, xc, y0 - .03, banner_top, -1)


def crown3d(m, at, r=.9, h=.5):
    """A great gold crown standing at `at`: a jewelled band, eight points with balls, a blue cap, an orb and cross."""
    t = Matrix.Translation(at)
    m.lathe([(0, 0), (r * .9, 0), (r, .06), (r, h), (r * .9, h), (r * .9, .1), (0, .1)], t, GOLD, 40)
    m.lathe([(0, 0), (r * 1.04, 0), (r * 1.06, .04), (r * 1.04, .1), (0, .1)], t, GOLD, 40)
    m.sphere(Vector(at) + Vector((0, 0, .1)), r * .86, M["blue"], (1, 1, .62), 3)
    for k in range(8):
        a = 2 * math.pi * k / 8
        p = Vector(at) + Vector((math.cos(a) * r * .95, math.sin(a) * r * .95, 0))
        m.lathe([(0, h - .02), (.12, h - .02), (.03, h + .45), (0, h + .5)], Matrix.Translation(p), GOLD, 10)
        m.sphere(p + Vector((0, 0, h + .55)), .08, GOLD, subdiv=2)
        q = Vector(at) + Vector((math.cos(a + math.pi / 8) * r * 1.0, math.sin(a + math.pi / 8) * r * 1.0, h * .5))
        m.sphere(q, .07, M["crown_blue"], subdiv=2)
    top = Vector(at) + Vector((0, 0, .1 + r * .86 * .62))
    m.sphere(top + Vector((0, 0, .16)), .17, GOLD, subdiv=3)
    m.box(top + Vector((-.035, -.035, .3)), top + Vector((.035, .035, .62)), GOLD)
    m.box(top + Vector((-.13, -.035, .45)), top + Vector((.13, .035, .52)), GOLD)


def finial(m, at, ball=.32):
    t = Matrix.Translation(at)
    m.lathe([(0, 0), (.42, 0), (.42, .12), (.3, .2), (.24, .4), (.32, .5), (0, .5)], t, M["stone3"], 24)
    m.lathe([(0, .5), (.16, .5), (.1, .58), (0, .58)], t, GOLD, 16)
    m.sphere(Vector(at) + Vector((0, 0, .58 + ball)), ball, GOLD, subdiv=3)


stone = Mesh("Gate stonework")
flowers = Mesh("Gate flowers")
gate = Mesh("Gate arch")
wall = Mesh("North wall")

# ------------------------------------------------------------------ the gate yard (x -14..14, z 48.6..60.5) and the terraces
kit_paving(stone, -14, 14, 48.6, 60.5, UPPER)
kit_wall_run(stone, "x", 48.6, -18, 18, 0, .32, UPPER, RES_Y - .35)          # its face over the reservoir
for s in (-1, 1):
    kit_balustrade(stone, flowers, (s * 14.15, 48.95, 0), (s * 14.15, 60.15, 0), lambda p: UPPER)
    xa, xb = sorted((s * 14.3, s * 44))
    kit_paving(stone, xa, xb, 48.6, 59, UPPER)
    kit_wall_run(stone, "x", 59, xa, xb, -.3, 0, UPPER, 0)                     # the terraces' north edge
    for y in (50.6, 53.3, 56.0, 58.4):                                         # topiaries in gold-rimmed pots
        t = Matrix.Translation((s * 16.6, y, UPPER))
        stone.lathe([(0, 0), (.3, 0), (.38, .5), (.42, .55), (0, .55)], t, M["stone3"], 20)
        stone.lathe([(0, .5), (.43, .5), (.43, .58), (0, .58)], t, GOLD, 20)
        flowers.lathe([(0, .55), (.36, .62), (.3, .95), (.2, 1.35), (.08, 1.65), (0, 1.75)], t, M["leaf3"], 14)
        flowers.sphere((s * 16.6, y, UPPER + 1.85), .1, GOLD, subdiv=1)

# The runner: royal blue, gold borders, from the water's edge through both arches to the finish medallion.
for y0, y1, z in ((49.0, 60.5, UPPER), (60.5, 62.3, UPPER)):
    stone.box((-1.4, y0, z), (1.4, y1, z + .006), M["blue"])
    for s in (-1, 1):
        xa, xb = sorted((s * 1.4, s * 1.56))
        stone.box((xa, y0, z), (xb, y1, z + .008), GOLD)
crown_badge(stone, face_frame((0, 51.6, UPPER + .006), (0, 0, 1), (0, 1, 0)), .75, M["crown_blue"])

# ------------------------------------------------------------------ the gate arch (piers x +-6..7.5, z 54.5..56, to 11; lintel 11..12.6)
GY0, GY1 = 54.5, 56.0
for s in (-1, 1):
    x0, x1 = sorted((s * 6, s * 7.5))
    pier(gate, flowers, x0, x1, GY0, GY1, UPPER, 4.5, 8.3, 11.0, s * 6, 10.6)
arch(gate, 6, GY0, GY1, 8.63, 1.85, 11.0, .62, .32)
# The entablature over the lintel: architrave, a frieze of crowns on royal blue in gold, a cornice, an attic and the crown.
gate.box((-7.58, GY0 - .08, 11.0), (7.58, GY1 + .08, 11.35), M["stone3"], .03)
gate.box((-7.5, GY0, 11.35), (7.5, GY1, 12.15), M["stone2"])
for y, out, sgn in ((GY0, (0, -1, 0), -1), (GY1, (0, 1, 0), 1)):
    ya, yb = sorted((y, y + sgn * .02))
    gate.box((-7.06, ya, 11.42), (7.06, yb, 12.08), GOLD, .005)
    ya, yb = sorted((y, y + sgn * .03))
    gate.box((-7.0, ya, 11.48), (7.0, yb, 12.02), M["blue"])
    for x, sc in ((0, .3), (-4.6, .2), (4.6, .2)):
        crown_badge(gate, face_frame((x, y + sgn * .03, 11.75), out), sc, M["crown_blue"])
gate.box((-7.8, GY0 - .3, 12.15), (7.8, GY1 + .3, 12.55), M["stone3"], .05)
gate.box((-7.82, GY0 - .32, 12.2), (7.82, GY1 + .32, 12.26), GOLD, .01)
gate.box((-7.7, GY0 - .22, 12.55), (7.7, GY1 + .22, 12.62), M["stone2"], .02)
gate.box((-5.0, GY0 + .1, 12.62), (5.0, GY1 - .1, 13.5), M["stone"], .02)
for y, sgn in ((GY0 + .1, -1), (GY1 - .1, 1)):
    ya, yb = sorted((y, y + sgn * .025))
    gate.box((-4.46, ya, 12.76), (4.46, yb, 13.36), GOLD, .005)
    ya, yb = sorted((y, y + sgn * .035))
    gate.box((-4.4, ya, 12.82), (4.4, yb, 13.3), M["blue"])
gate.box((-5.15, GY0 - .05, 13.5), (5.15, GY1 + .05, 13.62), M["stone3"], .02)
crown3d(gate, (0, (GY0 + GY1) / 2, 13.62), .95, .5)
for s in (-1, 1):
    finial(gate, (s * 6.9, (GY0 + GY1) / 2, 12.62), .3)

# ------------------------------------------------------------------ the north wall by the gate (x +-6.3..14.3, z 60.5..62, to 9)
for s in (-1, 1):
    xa, xb = sorted((s * 6.3, s * 14.3))
    kit_wall_run(wall, "x", 60.5, xa, xb, 0, .3, 8.83, UPPER, False)            # the yard's side
    kit_wall_run(wall, "x", 62.0, xa, xb, -.3, 0, 8.83, 0, False)              # the outside
    d0, d1 = (0, .3) if s > 0 else (-.3, 0)
    kit_wall_run(wall, "y", s * 6.3, 60.5, 62, d0, d1, 8.83, 0, False)         # the opening's jamb
    wall.box((min(xa, xb) - (.05 if s < 0 else 0), 60.45, 8.83), (max(xa, xb) + (.05 if s > 0 else 0), 62.05, 9.012),
             M["stone3"], .04)
    xj = s * 6.9
    wall.box((xj - .6, 60.47, 3.8), (xj + .6, 60.5, 8.83), M["stone3"])          # pilasters by the opening (3 cm proud)
    wall.box((xj - .66, 60.44, 8.4), (xj + .66, 60.5, 8.6), GOLD, .01)
    finial(wall, (xj, 61.25, 9.012), .36)
    for x in (s * 10.3, s * 13.7):
        wall.lathe([(0, 0), (.16, 0), (.1, .1), (0, .1)], Matrix.Translation((x, 61.25, 9.012)), GOLD, 12)
        wall.sphere((x, 61.25, 9.012 + .26), .2, GOLD, subdiv=2)

# ------------------------------------------------------------------ the exit terrace (x -6..6, z 60.5..66.5), its arch, steps, band
kit_paving(stone, -6, 6, 60.5, 66.3, UPPER)
stone.box((-6, 66.3, UPPER - .25), (6, 66.5, UPPER + .012), M["stone3"], .02)   # its edge over the first step
for s in (-1, 1):
    kit_balustrade(stone, flowers, (s * 6.15, 62.15, 0), (s * 6.15, 65.75, 0), lambda p: UPPER, False)
    d0, d1 = (-.3, 0) if s > 0 else (0, .3)
    kit_wall_run(stone, "y", s * 6, 60.5, 66.5, d0, d1, UPPER, 0, False)     # no coping: the tiles lap it
    kit_wall_run(stone, "y", s * 6, 66.5, 72.5, d0, d1, BAND, 0)
    kit_balustrade(stone, flowers, (s * 6.15, 67.25, 0), (s * 6.15, 72.4, 0), lambda p: BAND, False)
# The finish medallion (CP 끝): a royal blue disc in a gold ring, a crown.
stone.arc(1.9, 2.15, 0, 360, UPPER + .006, UPPER + .016, GOLD, (0, 63.5))
pts = [(math.cos(2 * math.pi * k / 48) * 1.9, math.sin(2 * math.pi * k / 48) * 1.9) for k in range(48)]
stone.slab(pts, Matrix.Translation((0, 63.5, 0)), UPPER + .006, UPPER + .012, M["blue"])
crown_badge(stone, face_frame((0, 63.4, UPPER + .012), (0, 0, 1), (0, 1, 0)), .9, M["crown_blue"])
for i in range(1, 5):
    y0, y1, t = 66.5 + (i - 1) * .5, 66.5 + i * .5, UPPER - .25 * i
    stone.box((-6, y0, t - .25), (6, y1, t), M["stone3"] if i % 2 else M["stone2"], .02)
kit_paving(stone, -6, 6, 68.5, 72.5, BAND)

EY0, EY1 = 66.1, 66.9
for s in (-1, 1):
    x0, x1 = sorted((s * 6, s * 7))
    pier(gate, flowers, x0, x1, EY0, EY1, BAND, BAND + .45, 6.75, 8.8, s * 6)
arch(gate, 6, EY0, EY1, 7.08, 1.32, 8.8, .45, .24)
gate.box((-7.08, EY0 - .08, 8.8), (7.08, EY1 + .08, 9.05), M["stone3"], .03)
gate.box((-7.0, EY0, 9.05), (7.0, EY1, 9.5), M["stone2"])
for y, out, sgn in ((EY0, (0, -1, 0), -1), (EY1, (0, 1, 0), 1)):
    ya, yb = sorted((y, y + sgn * .02))
    gate.box((-6.6, ya, 9.09), (6.6, yb, 9.46), GOLD, .005)
    ya, yb = sorted((y, y + sgn * .03))
    gate.box((-6.54, ya, 9.13), (6.54, yb, 9.42), M["blue"])
gate.box((-7.3, EY0 - .22, 9.5), (7.3, EY1 + .22, 9.8), M["stone3"], .04)
gate.box((-7.32, EY0 - .24, 9.55), (7.32, EY1 + .24, 9.6), GOLD, .01)
gate.slab([(-7.1, 9.8), (7.1, 9.8), (0, 11.2)], elev(EY0), 0, EY1 - EY0, M["stone2"])
for y, sgn in ((EY0, -1), (EY1, 1)):
    gate.slab([(-6.2, 9.93), (6.2, 9.93), (0, 10.98)], elev(y + sgn * .025 if sgn < 0 else y), 0, .02, GOLD)
    gate.slab([(-5.9, 9.97), (5.9, 9.97), (0, 10.9)], elev(y - .035 if sgn < 0 else y + .015), 0, .02, M["blue"])
    crown_badge(gate, face_frame((0, y + sgn * .036, 10.3), (0, sgn, 0)), .3, M["crown_blue"])
finial(gate, (0, (EY0 + EY1) / 2, 11.12), .26)
for s in (-1, 1):
    finial(gate, (s * 6.6, (EY0 + EY1) / 2, 9.8), .22)

stone.done()
flowers.done()
gate.done()
wall.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((0, 52.5, UPPER), (-2.2, 57.5, UPPER), (2, 64.0, UPPER + .016), (-1.5, 70.5, BAND)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "GateSample_v1.blend"))
shoot("gate_v1", "overview", (32, 30, 30), (0, 58, 5), 24)
shoot("gate_v1", "yard", (0, 41.5, 7.2), (0, 56, 7.4), 26)
shoot("gate_v1", "arch_close", (-9.5, 50.2, 6.2), (0, 55.2, 9), 26)
shoot("gate_v1", "exit", (0, 57.4, 5.7), (0, 67, 5.6), 28)
shoot("gate_v1", "band", (3.2, 72.2, 4.4), (0, 61, 6), 26)
shoot("gate_v1", "terrace", (-27, 50, 7.5), (-12, 56, 5), 26)
print("GATE SAMPLE DONE")
