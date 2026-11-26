"""B6 the loft over the screens passage (bh-hall session, seoungtest R838, 2026-11-25): its balustrade, its face to the
hall with corbels under it, the lamplighter's gold ladder (PLAN.md 1 "B6 회랑 · 샹들리에"; BH_A02 cells 3 - 5).

    blender -b --factory-startup --python bh_b6_loft_model.py -- --part loft_rail

One part at a time, as B7 (bh_b7_hall_model.py, whose helpers this uses): --part <name> builds it alone and writes
stills/b6/parts/<name>_compare.png (the sheet's cell, renders) and bh_b6_loft_<name>.blend.

The builder (GrandBanquetHallBuilder.Loft; Unity x east, y up, z north): the loft's floor x -31 .. 4.6, y 19.4 .. 20,
z 0 .. 5.6; its face x -31 .. 4.6, y 16 .. 20, z 5.6 .. 6 (not climbed); the rail 1.1 m high, z 5.75 .. 6, in three
runs x -24 .. -3.6, -1.6 .. 1.6, 3.6 .. 4.6 (open over the ladder and the two rope lanes, x +-2.6); the ladder at x
-27.7 on the face, y 15.4 .. 20, a board 1.6 wide standing 0.3 out, its rails and rungs gilt pictures. The models
stay inside / on those: the rail's top is the collider's top, the face's cap is flush with the floor, the ladder's
rails and rungs where the builder draws them.
Part frame: x along the face (east), y out toward the hall (north), z up from the part's foot.
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h   # noqa: E402

h.STILLS = os.path.join(HERE, "stills", "b6")
RAIL_H, RAIL_T = 1.1, .25
FACE_H = 4.0
LADDER_W, LADDER_H, LADDER_OUT = 1.6, 4.6, .3


def balustrade(m, x0, x1, pitch=3.2):
    """A01 cell 3 along x0 .. x1 (the rail's line at y 0 .. -RAIL_T, i.e. its front at y 0): a marble plinth, marble
    posts with gold-framed panels and gold caps at the ends and every `pitch`, turned marble balusters on little
    plinths, a walnut handrail whose top is the collider's top (1.1)."""
    t = RAIL_T
    yc = -t / 2
    n = max(1, round((x1 - x0) / pitch))
    posts = [x0 + (x1 - x0) * i / n for i in range(n + 1)]
    ph = .46                                      # the post's width
    h.box(m, "marble", x0, x1, -t - .03, .03, 0, .2)
    h.box(m, "marble", x0, x1, -t - .01, .01, .2, .24)
    rail_z0 = RAIL_H - .17
    for xa, xb in zip(posts, posts[1:]):
        a, b = xa + ph / 2, xb - ph / 2
        # the handrail: a rounded walnut profile
        h.prism_yz(m, "walnut", [(-t - .03, rail_z0), (.03, rail_z0), (.04, rail_z0 + .05), (.02, RAIL_H - .04),
                                 (-.05, RAIL_H), (-t + .05, RAIL_H), (-t - .02, RAIL_H - .04), (-t - .04, rail_z0 + .05)],
                   a - .02, b + .02)
        h.box(m, "marble", a, b, -t + .02, -.02, rail_z0 - .06, rail_z0)     # the bottom rail under it
        nb = max(2, int((b - a) / .34))
        for k in range(nb):
            x = a + (b - a) * (k + .5) / nb
            h.box(m, "marble", x - .11, x + .11, yc - .11, yc + .11, .24, .32)
            # the sheet's chunky vase: a full belly low, a short neck, a collar under the rail
            prof = [(0, 0), (.08, 0), (.08, .03), (.06, .05), (.075, .08), (.12, .15), (.135, .24), (.12, .33),
                    (.08, .41), (.05, .46), (.05, .5), (.075, .53), (.085, .56), (.085, .6), (0, .6)]
            hh = rail_z0 - .06 - .32
            h.lathe(m, "marble", [(r, z * hh / .6) for r, z in prof], at=(x, yc, .32), segs=20)
    for x in posts:
        h.box(m, "marble", x - ph / 2, x + ph / 2, yc - ph / 2, yc + ph / 2, 0, RAIL_H + .02)
        # gold-framed sunk panels on its front and back
        for y, sgn in ((yc + ph / 2, 1), (yc - ph / 2, -1)):
            for xa, xb, za, zb in ((x - .13, x + .13, .3, .33), (x - .13, x + .13, .87, .9), (x - .13, x - .1, .3, .9),
                                   (x + .1, x + .13, .3, .9)):
                h.box(m, "gold", xa, xb, min(y, y + sgn * .015), max(y, y + sgn * .015), za, zb)
        h.box(m, "gold", x - ph / 2 - .03, x + ph / 2 + .03, yc - ph / 2 - .03, yc + ph / 2 + .03, RAIL_H + .02, RAIL_H + .07)
        # the gold cap: a square plinth, then a rounded bun (the sheet's)
        h.box(m, "gold", x - .2, x + .2, yc - .2, yc + .2, RAIL_H + .07, RAIL_H + .12)
        h.lathe(m, "gold", [(0, 0), (.19, 0), (.2, .04), (.18, .1), (.12, .15), (0, .17)], at=(x, yc, RAIL_H + .12), segs=24)


def fascia(m, x0, x1, pitch=5.0):
    """A02 cell 4 along x0 .. x1: the loft's face, a walnut beam FACE_H tall (its front at y 0, 0.4 deep), a gold cap
    flush with the loft's floor overhanging a little, gold-framed sunk walnut panels between the corbels, the corbels
    under it with gold scrolls, walnut base moulding."""
    d = .4
    H = FACE_H
    rec = .1                                         # the panels' depth
    cap = .5                                         # the gold cap's height (the sheet's thick gilt slab)
    h.box(m, "walnut", x0, x1, -d, -rec, 0, H - cap)                                    # the body (the panels' floor)
    h.prism_yz(m, "gold", [(-d, H - cap), (.1, H - cap), (.16, H - cap + .06), (.16, H - .05), (.12, H), (-d, H)], x0, x1)
    h.prism_yz(m, "walnut", [(-d, -.12), (.1, -.12), (.14, -.05), (.14, .16), (.06, .24), (-d, .24)], x0, x1)
    n = max(1, round((x1 - x0) / pitch))
    cs = [x0 + (x1 - x0) * i / n for i in range(n + 1)]
    za, zb = .6, H - cap - .3
    # the frame: rails along the top and the foot, stiles over each corbel, standing rec out of the panels
    h.box(m, "walnut", x0, x1, -rec, 0, .24, za)
    h.box(m, "walnut", x0, x1, -rec, 0, zb, H - cap)
    for x in cs:
        a, b = max(x0, x - .45), min(x1, x + .45)
        h.box(m, "walnut", a, b, -rec, 0, za, zb)
    for xa, xb in zip(cs, cs[1:]):
        a, b = xa + .45, xb - .45
        # a gold bead round the sunk panel's edge, and a gold fillet frame inside it
        bead = [Vector((a + .05, -.05, za + .05)), Vector((b - .05, -.05, za + .05)), Vector((b - .05, -.05, zb - .05)),
                Vector((a + .05, -.05, zb - .05)), Vector((a + .05, -.05, za + .05))]
        h.tube(m, "gold", bead, .045, sides=8, cap=False)
        for p0, p1, q0, q1 in ((a + .3, b - .3, za + .3, za + .36), (a + .3, b - .3, zb - .36, zb - .3),
                               (a + .3, a + .36, za + .3, zb - .3), (b - .36, b - .3, za + .3, zb - .3)):
            h.box(m, "gold", p0, p1, -rec - .005, -rec + .035, q0, q1)
    for x in cs:
        cx = min(max(x, x0 + .4), x1 - .4)
        # the corbel: a walnut block under the face, its front an S that swells out at the top and tucks in toward
        # its foot, on a little base block; gold scrolls on both sides following the S, a gold leaf on its front
        prof = []
        n2 = 20
        for i in range(n2 + 1):
            t = i / n2                                     # 0 at the top, 1 at the foot
            y = .3 + .65 * (1 - t) ** 1.6 - .1 * math.sin(math.pi * t)
            prof.append((y, -1.9 * t))
        poly = [(-d + .05, 0)] + prof + [(-d + .05, -1.9)]
        h.prism_yz(m, "walnut", poly, cx - .5, cx + .5)
        h.box(m, "walnut", cx - .55, cx + .55, -d + .05, .38, -2.12, -1.9)
        h.box(m, "walnut", cx - .6, cx + .6, -d + .05, .42, -2.2, -2.12)
        for sgn in (-1, 1):
            x = cx + sgn * .51
            h.spiral(m, "gold", Vector((x, .3, -.32)), (0, -1, 0), (0, 0, 1), .2, .035, 1.5, .04, .022, start=math.pi)
            h.spiral(m, "gold", Vector((x, .08, -1.6)), (0, 1, 0), (0, 0, 1), .15, .03, 1.3, .035, .02)
            s_curve = [Vector((x, .3 + .2 * math.cos(math.pi) * 0 - .25 * t + .0, -.52 - 1.0 * t)) for t in [i / 10 for i in range(11)]]
            h.tube(m, "gold", s_curve, .035, sides=8)
        h.box(m, "gold", cx - .06, cx + .06, .55, .74, -.45, -.08)
        h.lathe(m, "gold", [(0, 0), (.09, .03), (.1, .1), (.06, .18), (0, .22)], at=(cx, .7, -.6), segs=12)


def ladder(m, H=LADDER_H, W=LADDER_W, out=LADDER_OUT):
    """A02 cell 5: the lamplighter's gold ladder on the board the builder stands out of the face: two rails with
    collars, rungs every 0.4 m (the builder's), the rails' tops bent over in hooks onto the loft's edge."""
    y = out + .03
    rx = W / 2 - .06
    for sgn in (-1, 1):
        x = sgn * rx
        h.tube(m, "gold", [Vector((x, y, 0)), Vector((x, y, H))], .07, sides=16)
        h.lathe(m, "gold", [(0, 0), (.08, 0), (.08, .04), (.065, .07), (0, .07)], at=(x, y, -.02), segs=14)
        hook = [Vector((x, y - .55 * (1 - math.cos(a)) * .5 - 0, H + .55 * math.sin(a) * .9))
                for a in [math.pi * i / 16 for i in range(17)]]
        hook = [Vector((x, y - .3 + .3 * math.cos(math.pi * i / 16), H + .35 * math.sin(math.pi * i / 16))) for i in range(17)]
        hook.append(hook[-1] + Vector((0, 0, -.18)))
        h.tube(m, "gold", hook, .07, sides=16)
    z = .3
    while z < H - .1:
        h.tube(m, "gold", [Vector((-rx, y, z)), Vector((rx, y, z))], .05, sides=12)
        for sgn in (-1, 1):
            h.lathe(m, "gold", [(0, -.05), (.075, -.05), (.085, 0), (.075, .05), (0, .05)], at=(sgn * rx, y, z), segs=14)
        z += .4


def rail_part(col):
    m = h.Mesh()
    balustrade(m, -1.6, 1.6)       # the middle run (x -1.6 .. 1.6), the sheet's module
    return [m.finish("BH loft rail", col, bevel=.012)]


def fascia_part(col):
    m = h.Mesh()
    fascia(m, -5.0, 5.0)
    return [m.finish("BH loft face", col, bevel=.02)]


def ladder_part(col):
    m = h.Mesh()
    ladder(m)
    return [m.finish("BH loft ladder", col, bevel=0)]


h.CLOSE.update({
    "loft_rail": ((-1.3, -.1, .6), (-.5, 1, .3), 2.4, 40),
    "loft_face": ((-5.0, 0, -.8), (-.6, 1, -.1), 4.5, 40),
    "loft_ladder": ((0, .3, 4.4), (-.6, 1, .3), 2.6, 40),
})
h.PARTS.update({
    "loft_rail": (rail_part, "BH_A02_ceiling_loft.png", (600, 400, 990, 630), None),
    "loft_face": (fascia_part, "BH_A02_ceiling_loft.png", (1010, 380, 1380, 620), None),
    "loft_ladder": (ladder_part, "BH_A02_ceiling_loft.png", (1395, 290, 1520, 690), None),
})


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    name = args[args.index("--part") + 1] if "--part" in args else "loft_rail"
    col = bpy.data.collections.new("BH B6 " + name)
    bpy.context.scene.collection.children.link(col)
    objs = h.PARTS[name][0](col)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b6_loft_%s.blend" % name))
    h.part_stills(name, objs)
