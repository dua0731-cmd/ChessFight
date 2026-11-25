"""B6 put together in the hall (bh-hall session, seoungtest R838, 2026-11-25): the loft and the chandeliers the user
passed one by one (bh_b6_loft_model.py, bh_b6_chandelier_model.py) placed at the builder's numbers inside B7's shell
(bh_b7_hall_assembly.py).

    blender -b --factory-startup --python bh_b6_assembly.py

Saves bh_b6_hall.blend and stills/b6/assembly/*.png. Blender frame as B7's: x = Unity x, y = Unity z, z = Unity y.
The builder (GrandBanquetHallBuilder): the loft x -31 .. 4.6, its floor 19.4 .. 20 over z 0 .. 5.6, its face z 5.6 .. 6
from 16 to 20, the rail 1.1 high in three runs, the ladder at x -27.7 from 15.4; the ropes on lines x +-2.6, pivots
at ScreenZ + RopeAlong[k], PivotY(k) (rope lengths 6.5, 6.5, 6.5, 8, 8), a gilt rod from each pivot to the ceiling;
the great chandelier's floor at (0, GreatY 17.8, GreatZ). The chandeliers hang at rest here (straight down).
"""
import math, os, sys
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h       # noqa: E402
import bh_b7_hall_assembly as a7   # noqa: E402
import bh_b6_loft_model as lm      # noqa: E402
import bh_b6_chandelier_model as cm   # noqa: E402

OUT = os.path.join(HERE, "stills", "b6", "assembly")
SCREEN_Z, LOFT_Y, CEIL, GREAT_Y = 6.0, 20.0, 34.0, 17.8
ROPE_L = [6.5, 6.5, 6.5, 8.0, 8.0]
ROPE_LOW = [LOFT_Y + .55, LOFT_Y + .05, LOFT_Y - .45, GREAT_Y + .55, GREAT_Y + .05]
LINES_X = (-2.6, 2.6)


def pivot_y(k):
    return ROPE_LOW[k] + ROPE_L[k] * math.cos(math.radians(40))


def loft(kit):
    m = h.Mesh()
    lm.fascia(m, -31.0, 4.6, pitch=5.08)
    kit.place("loft face", lambda col: [m.finish("BH loft face", col, bevel=.02)], Matrix.Translation((0, SCREEN_Z, 16)))
    r = h.Mesh()
    for x0, x1 in ((-24.0, -3.6), (-1.6, 1.6), (3.6, 4.6)):
        lm.balustrade(r, x0, x1, pitch=3.2)
    kit.place("loft rail", lambda col: [r.finish("BH loft rail", col, bevel=.012)],
              Matrix.Translation((0, SCREEN_Z, LOFT_Y)))
    ld = h.Mesh()
    lm.ladder(ld)
    kit.place("loft ladder", lambda col: [ld.finish("BH loft ladder", col, bevel=0)],
              Matrix.Translation((-27.7, SCREEN_Z, LOFT_Y - lm.LADDER_H)))
    # the loft's underside (walnut boards between beams) and its floor's top (marble, a gold edge), its end wall
    s = h.Mesh()
    h.box(s, "walnut", -31, 4.6, 0, 5.6, 19.36, 19.4)
    x = -31.0
    while x <= 4.6:
        h.box(s, "walnut", x - .15, x + .15, 0, 5.6, 19.1, 19.4)
        x += 2.54
    h.box(s, "marble", -31, 4.6, 0, 5.6, 19.98, 20.0)
    h.box(s, "walnut", 4.6, 4.9, 0, 6.0, 16.0, 23.0)
    h.box(s, "gold", 4.55, 4.95, 0, 6.05, 23.0, 23.08)
    h.box(s, "gold", 4.55, 4.95, 0, 6.05, 19.95, 20.02)
    kit.place("loft body", lambda col: [s.finish("BH loft body", col, bevel=.02)], Matrix())


def chandeliers(kit, lamps):
    pivots, great = a7.rope_pivots()
    for k, z in enumerate(pivots):
        L = ROPE_L[k]
        y = pivot_y(k)
        for x in LINES_X:
            kit.place(("hanging", L), lambda col, L=L: cm.hanging(col, L), Matrix.Translation((x, z, y)))
            lamps.append(Vector((x, z, y - L + 1.6)))
            # the gilt rod from the pivot to the ceiling, a knop every 2 m, a collar at each end
            rm = h.Mesh()
            h.tube(rm, "gold", [Vector((0, 0, .15)), Vector((0, 0, CEIL - y - .3))], .09, sides=12)
            zz = 1.6
            while zz < CEIL - y - 1:
                h.lathe(rm, "gold", [(0, -.12), (.13, -.1), (.17, 0), (.13, .1), (0, .12)], at=(0, 0, zz), segs=14)
                zz += 2.0
            h.lathe(rm, "gold", [(0, 0), (.2, 0), (.22, .1), (.12, .2), (0, .22)], at=(0, 0, .1), segs=16)
            kit.place(("rod", round(y, 2)), lambda col, rm=rm: [rm.finish("BH chandelier rod", col, bevel=0)],
                      Matrix.Translation((x, z, y)))
    gm = Matrix.Translation((0, great, GREAT_Y))
    kit.place("great", lambda col: cm.great(col, chain_up=CEIL - GREAT_Y - cm.GREAT_TOP), gm)
    for i in range(6):
        t = 2 * math.pi * i / 6
        lamps.append(Vector((4.3 * math.cos(t), great + 4.3 * math.sin(t), GREAT_Y + .8)))


SHOTS = {
    "loft_from_floor": ((14, 34, 3), (-8, 4, 18), 24),
    "along_the_lines": ((-6, 3, 21.5), (0, 50, 20), 28),
    "great": ((9, 26, 22), (0, 36, 18.5), 30),
    "overview": ((26, 70, 30), (-6, 18, 14), 22),
}


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hall, lamps = a7.assemble()
    kit = a7.Kit(hall)
    loft(kit)
    chandeliers(kit, lamps)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b6_hall.blend"))
    a7.stage(lamps)
    a7.SHOTS.clear()
    a7.SHOTS.update(SHOTS)
    a7.OUT = OUT
    a7.stills()
