"""B5 put together in the hall (bh-hall session, seoungtest R838, 2026-11-25): the stage, the curtain, the pelmet, the
double basses, the dessert buffet, the cake stands and the chocolate fountain the user passed one by one
(bh_b5_stage_model.py), placed at the builder's numbers inside B7's shell with B6's loft and chandeliers.

    blender -b --factory-startup --python bh_b5_assembly.py

Saves bh_b5_hall.blend and stills/b5/assembly/*.png. Blender frame as B7's: x = Unity x, y = Unity z, z = Unity y.
The builder (GrandBanquetHallBuilder.StageAndCurtain / Buffet): the stage x -32 .. -20, top 7.4, z 29.05 .. 49.05; the
curtain on x -24.5, its 20 pleats shut at CurtainZ +- (0.55 + 1.05 k), 7.7 .. 18; the rail 18 .. 18.3 and the pelmet
18.3 .. 19.9 (x -25 .. -24) over z 12.5 .. 51.05; the basses at x -23.95, z 19 and 27.05; the buffet x -32 .. -23.5,
z 3 .. 17, top 7.4; the fountain at (-27.7, 9.2). The curtain is drawn shut. The cake stands are pictures (no
collider), set off the route over the buffet's top. The musicians are not modelled here (bh-path's figures).
"""
import math, os, sys
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h       # noqa: E402
import bh_b7_hall_assembly as a7   # noqa: E402
import bh_b6_assembly as b6        # noqa: E402
import bh_b5_stage_model as st     # noqa: E402

OUT = os.path.join(HERE, "stills", "b5", "assembly")
STAGE_X0, STAGE_X1, STAGE_Z0, STAGE_Z1, STAGE_Y = -32.0, -20.0, 29.05, 49.05, 7.4
CURTAIN_X, CURTAIN_Z, PLEAT_W, PLEATS, PLEAT_BOTTOM, RAIL_Y = -24.5, 40.05, 1.05, 10, 7.7, 18.0
RUN_Z0, RUN_Z1 = 12.5, 51.05
BASS_X, BASS_Z = -23.95, (19.0, 27.05)
FOUNTAIN = (-27.7, 9.2)
CAKES = ((-27.7, 3.95), (-30.85, 15.9))


def facing(out, origin):
    """The part frame (x along the face, y out of it, z up) for a face looking along `out` (a unit x/y vector), its
    local origin at `origin`."""
    o = Vector((out[0], out[1], 0))
    x = Vector((o.y, -o.x, 0))                    # x = y cross z: along the face, right-handed with y out, z up
    return Matrix(((x.x, o.x, 0, origin[0]), (x.y, o.y, 0, origin[1]), (0, 0, 1, origin[2]), (0, 0, 0, 1)))


def stage(kit):
    # the front (east, at x -20) and the two ends (south at z 29.05, north at z 49.05); the top's boards
    kit.place("stage front E", lambda col: (lambda m: (st.stage_front(m, 0, STAGE_Z1 - STAGE_Z0),
                                                       [m.finish("BH stage front E", col, bevel=.02)])[1])(h.Mesh()),
              facing((1, 0), (STAGE_X1, STAGE_Z1, 0)))
    for key, out, org in (("S", (0, -1), (STAGE_X1, STAGE_Z0, 0)), ("N", (0, 1), (STAGE_X0, STAGE_Z1, 0))):
        kit.place("stage front " + key, lambda col, key=key: (lambda m: (st.stage_front(m, 0, STAGE_X1 - STAGE_X0),
                                                                         [m.finish("BH stage front " + key, col, bevel=.02)])[1])(h.Mesh()),
                  facing(out, org))
    m = h.Mesh()
    h.box(m, "floor_dark", STAGE_X0, STAGE_X1 - .3, STAGE_Z0 + .3, STAGE_Z1 - .3, STAGE_Y - .1, STAGE_Y)
    kit.place("stage top", lambda col: [m.finish("BH stage top", col, bevel=0)], Matrix())


def curtain(kit):
    for side in (-1, 1):
        for k in range(PLEATS):
            z = CURTAIN_Z + side * (.55 + PLEAT_W * k)
            kit.place("pleat", lambda col: (lambda m: (st.pleat(m), [m.finish("BH curtain pleat", col, bevel=0, merge=False)])[1])(h.Mesh()),
                      facing((1, 0), (CURTAIN_X, z, PLEAT_BOTTOM)))
    # the rail: a brass rod on brackets, a finial at each end
    r = h.Mesh()
    h.tube(r, "gold", [Vector((CURTAIN_X, RUN_Z0, RAIL_Y + .15)), Vector((CURTAIN_X, RUN_Z1, RAIL_Y + .15))], .1, sides=14)
    z = RUN_Z0 + 1
    while z < RUN_Z1:
        h.box(r, "gold", CURTAIN_X - .04, CURTAIN_X + .04, z - .04, z + .04, RAIL_Y + .15, RAIL_Y + .3)
        z += 3.0
    for z, sg in ((RUN_Z0, -1), (RUN_Z1, 1)):
        h.ball(r, "gold", Vector((CURTAIN_X, z + sg * .12, RAIL_Y + .15)), .15, 12, 8)
    kit.place("rail", lambda col: [r.finish("BH curtain rail", col, bevel=0)], Matrix())
    kit.place("pelmet", lambda col: (lambda m: (st.pelmet(m, 0, RUN_Z1 - RUN_Z0, span=2.4),
                                               [m.finish("BH stage pelmet", col, bevel=0)])[1])(h.Mesh()),
              facing((1, 0), (CURTAIN_X + .5, RUN_Z1, RAIL_Y + .3)))


def basses(kit):
    for z in BASS_Z:
        kit.place("bass", lambda col: (lambda m: (st.bass(m), [m.finish("BH double bass", col, bevel=.008, merge=False)])[1])(h.Mesh()),
                  facing((1, 0), (BASS_X, z, 0)))


def buffet(kit, lamps):
    kit.place("buffet", lambda col: (lambda m: (st.buffet(m), [m.finish("BH dessert buffet", col, bevel=0, merge=False)])[1])(h.Mesh()),
              Matrix())
    kit.place("fountain", lambda col: (lambda m: (st.fountain(m), [m.finish("BH chocolate fountain", col, bevel=0, merge=False)])[1])(h.Mesh()),
              Matrix.Translation((FOUNTAIN[0], FOUNTAIN[1], 0)))
    for x, z in CAKES:
        kit.place("cake", lambda col: (lambda m: (st.cake_stand(m), [m.finish("BH cake stand", col, bevel=0, merge=False)])[1])(h.Mesh()),
                  Matrix.Translation((x, z, STAGE_Y)))
    lamps.append(Vector((-22, 10, 12)))
    lamps.append(Vector((-18, 38, 12)))


SHOTS = {
    "stage": ((-2, 40, 11), (-26, 40, 12), 24),
    "buffet": ((-13, 24, 17), (-28, 9, 11), 26),
    "pit": ((-15, 33, 5), (-24, 22, 8), 26),
    "overview": ((12, 58, 28), (-20, 22, 10), 22),
}


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hall, lamps = a7.assemble()
    kit = a7.Kit(hall)
    b6.loft(kit)
    b6.chandeliers(kit, lamps)
    stage(kit)
    curtain(kit)
    basses(kit)
    buffet(kit, lamps)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b5_hall.blend"))
    a7.stage(lamps)
    a7.SHOTS.clear()
    a7.SHOTS.update(SHOTS)
    a7.OUT = OUT
    a7.stills()
