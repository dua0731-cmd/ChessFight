"""R1 part 2 (seoungtest R725, 2026-11-19): the constellation lines - the bridges between the Big Dipper's star platforms
(ro_parts.line_bridge; RO_A1's bridge piece, RO_2's lines), with the floor-motif panels the user asked for (PLAN 1-2).

    blender -b --factory-startup --python ro_r1_lines_model.py -- [--stills] [--export]

Graybox (ro_blockout.json): "Dipper line 1-2", "2-3", "3-4" and "Pointer line 7-west gate" - each a box 3 m wide and
0.4 deep under its top, the top a straight slope between the two line ends (RoyalObservatoryBuilder.Line), with a gold
"<name> mark" bar along its middle. Each bridge is built on that top and cut round the star platforms at its ends
(plain star: its rim's outer edge; variable star: its petals' tips), so it meets them instead of running into them.
-> ro_r1_lines.blend, stills/r1/ (line 1-2 beside star 1, its concept crops), --export
Assets/Resources/RoyalObservatory/Models/Dipper/RoDipperLines.fbx (all four, the section's frame) - hung by
RoyalObservatoryDressing.Dipper.cs over "Dipper line *" / "Pointer line *" (their colliders stay).
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402

MODEL, ZONE = "RoDipperLines", "Dipper"
# RoyalObservatoryBuilder.Dipper (Unity x, y, z, radius) and which are variable stars (petals to radius - 0.02)
STARS = [(-2, 0, 18, 4), (-10, .8, 26, 3.2), (-19, 1.8, 33, 3.2), (-27, 2.4, 43, 3.6), (-38, 1.2, 49, 3.2), (-37, 2.2, 62, 3.6),
         (-25, 3.2, 60, 4)]
VARIABLE = {1, 2}
LINES = [("Dipper line 1-2", 0, 1), ("Dipper line 2-3", 1, 2), ("Dipper line 3-4", 2, 3), ("Pointer line 7-west gate", 6, None)]


def cut_of(i):
    x, y, z, r = STARS[i]
    return (k.at((x, y, z)), r + (.03 if i in VARIABLE else .12))


def ends(name):
    """A line's two top ends (Blender), as RoyalObservatoryBuilder works them out (Ends / the pointer): from the star
    before, Reach inside its rim (a variable star 0.05, a plain one 0.3), to the star after likewise; the pointer runs
    from star 7 toward the west gate to x -18.3 at the gate's height. (Since the lines became arched groups of eight
    boxes, the dump holds no single box to read them from.)"""
    def reach(i):
        return STARS[i][3] - (.05 if i in VARIABLE else .3)
    for n, i, j in LINES:
        if n != name:
            continue
        a = Vector(STARS[i][:3])
        if j is not None:
            c = Vector(STARS[j][:3])
            flat = Vector((c.x - a.x, 0, c.z - a.z)).normalized()
            f, t = a + flat * reach(i), c - flat * reach(j)
        else:
            gate = Vector((-12.65, 4.0, 64.0))
            to_gate = Vector((gate.x - a.x, 0, gate.z - a.z)).normalized()
            f = a + to_gate * (STARS[i][3] - .3)
            t = a + to_gate * ((-18.3 - a.x) / to_gate.x)
            t.y = gate.y
        return k.at(f), k.at(t)
    raise KeyError(name)


def build(only=None):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    k.motif_reset()
    col = bpy.data.collections.new("RO R1 lines")
    bpy.context.scene.collection.children.link(col)
    objs = []
    for name, i, j in LINES:
        if only and name not in only:
            continue
        a, b = ends(name)
        ca, cb = cut_of(i), (cut_of(j) if j is not None else None)
        # the end nearer star i is a
        if (a - ca[0]).length > (b - ca[0]).length:
            a, b = b, a
        got = P.line_bridge(col, name, a, b, 3.0, .4, cut_a=ca, cut_b=cb)
        objs += got
        print("[r1 lines]", name, "%.2f m" % (b - a).length, k.tris(got), "tris")
    print("[r1 lines] total", k.tris(objs), "tris")
    return col, objs


def stills(out):
    import ro_r0_star_model as r0
    # the first star platform beside the line, for the look of the meeting
    star_col = bpy.data.collections.new("context star")
    bpy.context.scene.collection.children.link(star_col)
    _, lamps = P.star_platform(star_col, "Dipper star 1", k.at(STARS[0][:3]), 4.0, -18.0,
                               lamps=[105, 165, 204, 336])
    r0.LAMPS[:] = lamps
    s1 = k.at(STARS[0][:3])
    shots = {
        "r1_line_12_along": (Vector((-1.5, -3.0, 2.2)), Vector((-6.5, 5.0, .2)), 30),
        "r1_line_12_side": (Vector((-1.0, 11.0, 2.5)), Vector((-4.4, 4.2, -.3)), 30),
        "r1_line_12_top": (Vector((-4.4, 4.2, 12.0)), Vector((-4.4, 4.21, 0)), 35),
        "r1_line_12_under": (Vector((2.5, 9.5, -2.8)), Vector((-4.4, 4.2, -.4)), 32),
        "r1_lines_handle_overview": (Vector((12.0, -14.0, 22.0)), Vector((-13.0, 13.0, 0.0)), 30),
        # line 2-3 from its side (it climbs 1 m): the curve
        "r1_line_23_curve_side": (Vector((-6.0, 17.5, 1.6)), Vector((-12.5, 11.5, 1.0)), 30),
    }
    r0.stills([], out, shots=shots, centre=s1, floor_at=(-6, 22, -18), fill_offset=(4, 8, 8))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    col, objs = build(["Dipper line 1-2"] if "--stills" in argv and "--export" not in argv and "--all" not in argv else None)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r1_lines.blend"))
    if "--export" in argv:
        k.export(col.name, MODEL, ZONE)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r1"))
