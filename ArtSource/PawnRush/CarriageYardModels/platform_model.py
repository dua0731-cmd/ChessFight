# Royal Carriage Yard models (R680): zone 4's statics, the platform over the basin and what stands on it.
#
#   RCY_BUILD = False; exec(open(r".../rcy_kit.py").read())
#   RCY_BUILD = True;  exec(open(r".../platform_model.py").read())
#   exec(open(r".../export_rcy.py").read()); platform_export()
#
# Builds, all grey clay over the code's blockout (whose colliders stay), in the MAP frame (the code builds zone 4 in its
# own frame under "3-4 (own frame)/4 Final gate", moved LaterShift -24; every number below is already the map's):
#   "RCY Platform"        both ways the map is built: the cliff's marble face over the basin (a rusticated base, panels
#                         between pilasters, a dentil frieze and the cornice, all flush with or set back from the
#                         collider's face at y 143), its coping, the platform's paving and the two edge balustrades
#   "RCY Finish court"    the whole map only: the court's paving, its three walls, the crown's pedestal and cushion
#   "RCY Section floors"  as a section only: the runout's, the terrace's and the entrance band's paving
# Left to the code: the final gate's leaves and push bar (PushGate), the finish line, the crown, the signs. The gate wall,
# its towers and the section end's arch, railings and steps are the castle kit's (castle_model.py).

CLIFF_Y, CLIFF_BACK, TOP, WATER = 143.0, 143.6, 8.0, -.5
PLATFORM_HALF, PLATFORM_Y1 = 26.0, 159.0
LANDING_HALF = 3.5                      # the lifting wheel's landing meets the platform between the railings
SLUICE_X, SLUICE_HALF = 15.5, 3.1       # the basin docks' sluice arches in the cliff (sorting_model.py)
COURT_HALF, COURT_Y0, COURT_Y1, COURT_WALL = 10.0, 159.0, 178.0, 5.0
PEDESTAL = ((-1.2, 174.0), (1.2, 176.4), 1.4)
RUNOUT_Y1, TERRACE_Y1, BAND_Y0, BAND_Y1 = 169.0, 175.0, 177.0, 181.0


# ------------------------------------------------------------------ the cliff


def cliff(col):
    """The marble face over the basin, x +-26, from the basin's bed (z -3) to the platform (8). Nothing stands in front of
    y 143 (the collider's face, where the lifting wheel's riders and the swimmers touch it)."""
    x0, x1, f, b = -PLATFORM_HALF, PLATFORM_HALF, CLIFF_Y, CLIFF_BACK
    parts = [box(col, "Cliff core", (x0, f, -3.0), (x1, b, -.4), MARBLE)]
    # A rusticated base from just under the water, big blocks.
    parts.append(ashlar(col, "Cliff base", (x0, f, -.4), (x1, b, 1.3), "-y", course=.85, length=2.4, mat=MARBLE))
    parts.append(box(col, "Cliff plinth", (x0, f, 1.3), (x1, b, 1.52), MARBLE, bevel=.06))
    # The panelled storey: pilasters flush with the face, sunk panels between them (5 cm), a raised field in each.
    piers = [(-26.0, -25.4), (-23.05, -22.15), (-19.4, -18.6), (-12.4, -11.55), (-8.65, -7.75), (-4.4, -3.5),
             (3.5, 4.4), (7.75, 8.65), (11.55, 12.4), (18.6, 19.4), (22.15, 23.05), (25.4, 26.0)]
    z0, z1 = 1.52, 6.6
    parts.append(box(col, "Cliff panel back", (x0, f + .05, z0), (x1, b, z1), MARBLE))
    for a, c in piers:
        parts.append(box(col, "Cliff pilaster", (a, f, z0), (c, b, z1), MARBLE, bevel=.04))
        parts.append(box(col, "Cliff pilaster base", (a, f, z0), (c, b, z0 + .3), MARBLE, bevel=.05))
        parts.append(box(col, "Cliff pilaster cap", (a, f, z1 - .28), (c, b, z1), MARBLE, bevel=.05))
    for (_, lo), (hi, _) in zip(piers[:-1], piers[1:]):
        mid = abs((lo + hi) / 2)
        if SLUICE_X - SLUICE_HALF - .1 < mid < SLUICE_X + SLUICE_HALF + .1:
            # Behind the sluice arch the face is plain, so the arch and its tunnel read.
            parts.append(box(col, "Cliff face", (lo, f, z0), (hi, b, z1), MARBLE))
            continue
        parts.append(box(col, "Cliff panel field", (lo + .38, f + .025, z0 + .55), (hi - .38, b, z1 - .55), MARBLE, bevel=.03))
        parts.append(box(col, "Cliff panel frame", (lo + .2, f + .035, z0 + .37), (hi - .2, b, z1 - .37), MARBLE, bevel=.015, segments=1))
    # The band, the dentil frieze and the cornice under the coping.
    parts.append(box(col, "Cliff band", (x0, f, z1), (x1, b, 6.92), MARBLE, bevel=.05))
    parts.append(box(col, "Cliff frieze", (x0, f + .05, 6.92), (x1, b, 7.55), MARBLE))
    x = x0 + .2
    while x < x1 - .3:
        parts.append(box(col, "Cliff dentil", (x, f, 7.2), (x + .22, b, 7.55), MARBLE, bevel=.02, segments=1))
        x += .46
    parts.append(box(col, "Cliff cornice", (x0, f, 7.55), (x1, b, 7.8), MARBLE, bevel=.07))
    # The coping: the cliff's top, the railings' foot and, between them, the step off the wheel's landing.
    parts.append(box(col, "Cliff coping", (x0, f, 7.8), (x1, 143.9, TOP), MARBLE, bevel=.03))
    return parts


# ------------------------------------------------------------------ the platform


def platform(col):
    parts = [box(col, "Platform core", (-PLATFORM_HALF, CLIFF_BACK, -3.0), (PLATFORM_HALF, PLATFORM_Y1, TOP - 1.0), STONE)]
    parts.append(flagstones(col, "Platform paving", -PLATFORM_HALF, PLATFORM_HALF, 143.9, PLATFORM_Y1, TOP, size=2.0))
    # A marble kerb along the coping.
    parts.append(box(col, "Platform kerb", (-PLATFORM_HALF, 143.9, TOP - .3), (PLATFORM_HALF, 144.35, TOP), MARBLE, bevel=.03))
    for s in (-1, 1):
        lo, hi = sorted((s * LANDING_HALF, s * PLATFORM_HALF))
        parts.append(balustrade(col, "Edge railing", (lo, CLIFF_Y, TOP), (hi, 143.4, TOP + 1.0)))
    return parts


# ------------------------------------------------------------------ the finish court (the whole map only)


def finish_court(col):
    h = COURT_HALF
    parts = [box(col, "Court core", (-h, COURT_Y0, -3.0), (h, COURT_Y1, TOP - 1.0), STONE)]
    parts.append(flagstones(col, "Court paving", -h + .6, h - .6, COURT_Y0, COURT_Y1 - .6, TOP, size=1.6))
    # A marble border round the paving, along the walls.
    for lo, hi in (((-h, COURT_Y0), (-h + .6, COURT_Y1)), ((h - .6, COURT_Y0), (h, COURT_Y1)),
                   ((-h + .6, COURT_Y1 - .6), (h - .6, COURT_Y1))):
        parts.append(box(col, "Court border", (lo[0], lo[1], TOP - .25), (hi[0], hi[1], TOP), MARBLE, bevel=.025, segments=1))
    top = TOP + COURT_WALL
    for name, lo, hi, face in (("Court wall W", (-h - 1, 157.0), (-h, 179.0), "+x"),
                               ("Court wall E", (h, 157.0), (h + 1, 179.0), "-x"),
                               ("Court wall N", (-h - 1, COURT_Y1), (h + 1, 179.0), "-y")):
        parts.append(ashlar(col, name, (lo[0], lo[1], TOP + .4), (hi[0], hi[1], top - .3), face, course=.6, length=1.3))
        parts.append(box(col, name + " base", (lo[0], lo[1], TOP), (hi[0], hi[1], TOP + .4), MARBLE, bevel=.04))
        parts.append(box(col, name + " coping", (lo[0], lo[1], top - .3), (hi[0], hi[1], top), MARBLE, bevel=.05))
    # The crown's pedestal: base, a die set 7 cm in, its cap, a cushion under the crown.
    (a, b), (c, d), ph = PEDESTAL
    parts.append(box(col, "Pedestal base", (a, b, TOP), (c, d, TOP + .24), MARBLE, bevel=.05))
    parts.append(box(col, "Pedestal die", (a + .07, b + .07, TOP + .24), (c - .07, d - .07, TOP + ph - .26), MARBLE, bevel=.03))
    parts.append(box(col, "Pedestal cap", (a, b, TOP + ph - .26), (c, d, TOP + ph), MARBLE, bevel=.06))
    parts.append(box(col, "Pedestal cushion", (-.75, 174.45, TOP + ph), (.75, 175.95, TOP + ph + .1), "RCY | velvet", bevel=.045, segments=3))
    return parts


# ------------------------------------------------------------------ the section's floors (as a section only)


def section_floors(col):
    parts = []
    for name, y0, y1, top in (("Runout paving", PLATFORM_Y1, RUNOUT_Y1, TOP), ("Terrace paving", RUNOUT_Y1, TERRACE_Y1, TOP),
                              ("Band paving", BAND_Y0, BAND_Y1, TOP - 1.0)):
        parts.append(box(col, name + " core", (-6.0, y0, -3.0), (6.0, y1, top - 1.0), STONE))
        parts.append(flagstones(col, name, -6.0, 6.0, y0, y1, top, size=1.5))
    # The terrace's edge over the steps in marble.
    parts.append(box(col, "Terrace edge", (-6.0, TERRACE_Y1 - .45, TOP - .3), (6.0, TERRACE_Y1, TOP), MARBLE, bevel=.03))
    # Under the railings (the castle kit's, x 6 .. 6.6 out): a wall each side for them to stand on, the floors' sides
    # (no collider; out of the way beside the 12 m walk).
    for s in (-1, 1):
        for y0, y1, top in ((PLATFORM_Y1, 175.4, TOP), (175.4, BAND_Y1, TOP - 1.0)):
            lo, hi = sorted((s * 6.0, s * 6.75))
            parts.append(ashlar(col, "Floor side", (lo, y0, -3.0), (hi, y1, top - .2), "+x" if s > 0 else "-x", course=.7, length=1.5,
                                below=TOP - 4.0))
            parts.append(box(col, "Floor side coping", (lo, y0, top - .2), (hi, y1, top), MARBLE, bevel=.03))
    return parts


def build_platform():
    out = {}
    for cname, make in (("RCY Platform", lambda c: cliff(c) + platform(c)), ("RCY Finish court", finish_court),
                        ("RCY Section floors", section_floors)):
        col = collection(cname)
        clear(col)
        out[cname] = join(col, cname[4:], make(col))
    print("platform tris", {k: tris(bpy.data.collections[k]) for k in out})
    return out


def platform_export():
    for cname, name in (("RCY Platform", "RcyPlatform"), ("RCY Finish court", "RcyFinishCourt"),
                        ("RCY Section floors", "RcySectionFloors")):
        export(cname, name)


if globals().get("RCY_BUILD", True):
    platform_parts = build_platform()
