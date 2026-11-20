"""The Big Dipper's plain star platforms after the first (seoungtest R725, 2026-11-19): star 4 (the handle's end, where
the bowl's two ways start) and star 7 (the bowl's end, where the pointer line leaves for the globe's west gate). The R0
star platform (ro_parts.star_platform, the user's "확인" on star 1), each with its own round motif and lamps placed for
what meets it.

    blender -b --factory-startup --python ro_stars_model.py -- [4,7] [--stills] [--export]

Graybox (RoyalObservatoryBuilder.Dipper): "Dipper star 4" (-27, 2.4, 43) radius 3.6 and "Dipper star 7" (-25, 3.2, 60)
radius 4 (Unity), each a deck 0.6 deep, a brass rim, a five-ray mark and a pillar from the hall floor (y -18). Lamps
stand outside the rim either side of a line coming in, and away from where the bowl's rides pass the rim (the first /
last orbit's platforms 0.15 m off it, the rolling planet 0.6 m off it).
-> ro_stars.blend, stills/r1/, --export Models/Dipper/RoDipperStar4.fbx (R1) and Models/Bowl/RoDipperStar7.fbx (R2), each with
its _lamps.txt, in the section's frame.
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402


def heading(a, b):
    """Blender heading (degrees, 0 = +x, counter-clockwise) from Unity point a to b (x, z)."""
    return math.degrees(math.atan2(b[2] - a[2], b[0] - a[0]))


S3, S4, S7 = (-19, 1.8, 33), (-27, 2.4, 43), (-25, 3.2, 60)
WEST_GATE = (-12.65, 4, 64)
# RoyalObservatoryBuilder.OrbitCrossing: which way each end star's orbit dock lies (Unity x, z)
OUT4 = math.degrees(math.atan2(.39, -.92))
OUT7 = math.degrees(math.atan2(-.28, -.96))

STARS = {
    # n: (centre, radius, zone, lamps (headings), keep clear of)
    4: (S4, 3.6, "Dipper", None, [heading(S4, S3), OUT4, heading(S4, S7)]),
    7: (S7, 4.0, "Bowl", None, [heading(S7, WEST_GATE), OUT7, heading(S7, S4)]),
}


def lamp_headings(line_in, clear, radius):
    """Two lamps either side of the line coming in (just outside its 3 m width), and two more on the rim's widest
    stretches clear of the rides."""
    half = math.degrees(math.asin(1.5 / radius)) + 8
    got = [line_in - half, line_in + half]
    marks = sorted(h % 360 for h in clear)
    gaps = []
    for i, h in enumerate(marks):
        nxt = marks[(i + 1) % len(marks)] + (360 if i == len(marks) - 1 else 0)
        gaps.append((nxt - h, (h + nxt) / 2))
    for _, mid in sorted(gaps, reverse=True)[:2]:
        if all(abs((mid - g + 180) % 360 - 180) > 25 for g in got):
            got.append(mid)
    return got


def build(which):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    k.motif_reset()
    out = {}
    # the round cells already on the course's star platforms: star 1 (ro_r0_star_model) and the ones made here - a star
    # takes a top no other star has while one is left (six round cells, so few repeat)
    used = [13]
    for n in which:
        centre, radius, zone, _, clear = STARS[n]
        col = bpy.data.collections.new("RO star %d" % n)
        bpy.context.scene.collection.children.link(col)
        lamps = lamp_headings(clear[0], clear, radius)
        objs, at = P.star_platform(col, "Dipper star %d" % n, k.at(centre), radius, -18.0, lamps=lamps,
                                   motif_avoid=used if len(used) < len(k.ROUND) else ())
        used.append(k._registry[-1][1] if k._registry[-1][0] == "round" else
                    next(c for kind, c, _, _ in reversed(k._registry) if kind == "round"))
        print("[stars] star %d: %d tris, lamps at %s" % (n, k.tris(objs), ["%.0f" % h for h in lamps]))
        out[n] = (col, objs, at, zone)
    return out


def stills(built):
    import ro_r0_star_model as r0
    for n, (col, objs, at, zone) in built.items():
        centre = k.at(STARS[n][0])
        r0.LAMPS[:] = at
        shots = {"r1_star%d_three_quarter" % n: (Vector((9.5, -8.5, 5.5)), Vector((0, 0, -1.2)), 35),
                 "r1_star%d_top" % n: (Vector((0, 0, 14)), Vector((0, .01, 0)), 40)}
        r0.stills([], os.path.join(HERE, "stills", "r1"), shots=shots, centre=centre,
                  floor_at=(centre.x, centre.y, -18), fill_offset=(6, -8, 7))
        for o in list(bpy.data.objects):
            if o.type in {"LIGHT", "CAMERA"} or o.name.startswith("Plane"):
                bpy.data.objects.remove(o, do_unlink=True)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = [int(a) for a in argv[0].split(",")] if argv and argv[0][0].isdigit() else [4, 7]
    built = build(which)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_stars.blend"))
    if "--export" in argv:
        for n, (col, objs, at, zone) in built.items():
            k.export(col.name, "RoDipperStar%d" % n, zone)
            k.write_lamps(zone, "RoDipperStar%d" % n, at)
    if "--stills" in argv:
        stills(built)
