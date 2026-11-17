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
KERB_BACK = 144.35   # the platform kerb's back (the paving starts there)
PLATFORM_HALF, PLATFORM_Y1 = 26.0, 159.0
LANDING_HALF = 3.5                      # the lifting wheel's landing meets the platform between the railings
SLUICE_X, SLUICE_HALF = 15.5, 3.1       # the basin docks' sluice arches in the cliff (sorting_model.py)
COURT_HALF, COURT_Y0, COURT_Y1, COURT_WALL = 10.0, 159.0, 178.0, 5.0
PEDESTAL = ((-1.2, 174.0), (1.2, 176.4), 1.4)
RUNOUT_Y1, TERRACE_Y1, BAND_Y0, BAND_Y1 = 169.0, 175.0, 177.0, 181.0
# The cliff's pilasters on the +x side (mirrored for -x); R682: the railing's posts stand over them.
CLIFF_PIERS = [(3.5, 4.4), (7.75, 8.65), (11.55, 12.4), (18.6, 19.4), (22.15, 23.05), (25.4, 26.0)]


# ------------------------------------------------------------------ the cliff


def cliff(col):
    """The marble face over the basin, x +-26, from the basin's bed (z -3) to the platform (8). Nothing stands in front of
    y 143 (the collider's face, where the lifting wheel's riders and the swimmers touch it)."""
    x0, x1, f, b = -PLATFORM_HALF, PLATFORM_HALF, CLIFF_Y, CLIFF_BACK
    parts = [box(col, "Cliff core", (x0, f, -3.0), (x1, b, -.4), SHADE)]
    # A rusticated base from just under the water, big blocks.
    parts.append(ashlar(col, "Cliff base", (x0, f, -.4), (x1, b, 1.3), "-y", course=.85, length=2.4, mat=MARBLE))
    parts.append(box(col, "Cliff plinth", (x0, f, 1.3), (x1, b, 1.52), MARBLE, bevel=.06))
    # The panelled storey: pilasters flush with the face, sunk panels between them (5 cm), a raised field in each.
    piers = [(-c, -a) for a, c in reversed(CLIFF_PIERS)] + CLIFF_PIERS
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
    parts = [box(col, "Platform core", (-PLATFORM_HALF, CLIFF_BACK, -3.0), (PLATFORM_HALF, PLATFORM_Y1, TOP - 1.0), SHADE)]
    # R686 (rcy-textures' z-fighting check): the paving starts behind the kerb - it used to run under it from 143.9, the
    # kerb's top over the first slabs' tops and its face over the paving base's face (12 m2 drawn twice) - and the kerb
    # goes down to the paving base's depth, filling what the paving no longer does under it.
    parts.append(flagstones(col, "Platform paving", -PLATFORM_HALF, PLATFORM_HALF, KERB_BACK, PLATFORM_Y1, TOP, size=2.0))
    # A marble kerb along the coping.
    parts.append(box(col, "Platform kerb", (-PLATFORM_HALF, 143.9, TOP - 1.0), (PLATFORM_HALF, KERB_BACK, TOP), MARBLE, bevel=.03))
    parts += edge_railings(col)
    return parts


RAIL_Y0, RAIL_Y1, RAIL_HIGH = CLIFF_Y, 143.4, 1.0   # the railings' colliders: y 143 .. 143.4, 1 m over the platform


def edge_railings(col):
    """R682 (environment-artist, prop-artist): the cliff-top balustrade in bays between pedestal posts set over the
    cliff's pilasters (the landing's gap edge at x +-3.5 is one of them), instead of one run with a newel at each end.
    Each post: a moulded base, a shaft with a sunk panel on both faces, a cap 3 cm over (the rail's collider is the
    1 m box; nothing past it into the landing's gap). Each bay: plinth, turned balusters, a handrail."""
    parts = []
    piers = [p for p in CLIFF_PIERS if p[0] >= LANDING_HALF - 1e-6]
    for s in (-1, 1):
        for a, b in piers:
            lo, hi = sorted((s * a, s * b))
            parts += rail_post(col, lo, hi)
        for (_, a), (b, _) in zip(piers[:-1], piers[1:]):
            lo, hi = sorted((s * a, s * b))
            parts += rail_bay(col, lo, hi)
    return parts


def rail_post(col, x0, x1):
    y0, y1, z0, z1 = RAIL_Y0, RAIL_Y1, TOP, TOP + RAIL_HIGH
    # the cap's overhang stops at the landing's gap edge (x +-3.5): nothing out into the gap
    o0 = 0.0 if abs(abs(x0) - LANDING_HALF) < 1e-6 else .03
    o1 = 0.0 if abs(abs(x1) - LANDING_HALF) < 1e-6 else .03
    parts = [box(col, "Rail post base", (x0, y0 - .03, z0), (x1, y1 + .03, z0 + .14), MARBLE, bevel=.03, segments=2),
             box(col, "Rail post torus", (x0 + .02, y0 - .015, z0 + .14), (x1 - .02, y1 + .015, z0 + .22), MARBLE, bevel=.035, segments=3),
             box(col, "Rail post shaft", (x0 + .04, y0 + .02, z0 + .22), (x1 - .04, y1 - .02, z1 - .16), MARBLE)]
    # the shaft's faces: a frame flush with them round a panel 2 cm in, on the basin's and the platform's side
    for ya, yb in ((y0, y0 + .02), (y1 - .02, y1)):
        for xa, xb in ((x0 + .04, x0 + .14), (x1 - .14, x1 - .04)):
            parts.append(box(col, "Rail post frame", (xa, ya, z0 + .22), (xb, yb, z1 - .16), MARBLE, bevel=.012, segments=1))
        for za, zb in ((z0 + .22, z0 + .32), (z1 - .26, z1 - .16)):
            parts.append(box(col, "Rail post frame", (x0 + .04, ya, za), (x1 - .04, yb, zb), MARBLE, bevel=.012, segments=1))
    parts.append(box(col, "Rail post cap bed", (x0 + .01, y0 - .01, z1 - .16), (x1 - .01, y1 + .01, z1 - .08), MARBLE, bevel=.025, segments=2))
    parts.append(box(col, "Rail post cap", (x0 - o0, y0 - .03, z1 - .08), (x1 + o1, y1 + .03, z1), MARBLE, bevel=.035, segments=3))
    return parts


def rail_bay(col, x0, x1):
    """A bay between two posts: a plinth and a handrail running into them, balusters about every 0.3 m."""
    y0, y1, z0, z1 = RAIL_Y0, RAIL_Y1, TOP, TOP + RAIL_HIGH
    plinth, rail = .2, .14
    parts = [box(col, "Rail plinth", (x0 - .01, y0 + .03, z0), (x1 + .01, y1 - .03, z0 + plinth), MARBLE, bevel=.03, segments=2),
             box(col, "Rail handrail bed", (x0 - .01, y0 + .04, z1 - rail - .03), (x1 + .01, y1 - .04, z1 - rail), MARBLE, bevel=.012, segments=1),
             box(col, "Rail handrail", (x0 - .01, y0 - .01, z1 - rail), (x1 + .01, y1 + .01, z1 - .01), MARBLE, bevel=.045, segments=3)]
    span = x1 - x0 - .2
    count = max(1, int(round(span / .3)))
    step = span / count
    for k in range(count):
        parts.append(baluster(col, (x0 + .1 + step * (k + .5), (y0 + y1) / 2, z0 + plinth), z1 - rail - .03 - z0 - plinth))
    return parts


# ------------------------------------------------------------------ the finish court (the whole map only)


def finish_court(col):
    h = COURT_HALF
    parts = [box(col, "Court core", (-h, COURT_Y0, -3.0), (h, COURT_Y1, TOP - 1.0), SHADE)]
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
        # R682: the base goes down into the court's core: its bevel and the border's met in a groove with nothing under
        # the wall, and the sky showed through it along every wall's foot.
        parts.append(box(col, name + " base", (lo[0], lo[1], TOP - 1.0), (hi[0], hi[1], TOP + .4), MARBLE, bevel=.04))
        parts.append(box(col, name + " coping", (lo[0], lo[1], top - .3), (hi[0], hi[1], top), MARBLE, bevel=.05))
    parts += court_crown(col, top)
    parts += pedestal(col)
    return parts


def court_crown(col, top):
    """R682 (environment-artist, fantasy-worlds: "every wall top has chunky rook merlons"): the court's three walls,
    flat-topped to now, get a corbel table under their coping on the court's side (out .16 at most, 3.6 m and more
    over the floor, out of any jump's reach) and a ring of rook merlons on it, a tooth every 2 m with a slit to the
    court, whose corners carry a wider tooth each."""
    h = COURT_HALF
    parts = []
    walls = (  # (face x/y, along axis, from, to, side the court is on, wall's outer bound)
        ("x", -h, COURT_Y0 - 2.0, COURT_Y1 + 1.0, 1),
        ("x", h, COURT_Y0 - 2.0, COURT_Y1 + 1.0, -1),
        ("y", COURT_Y1, -h, h, -1),   # between the side walls' teeth
    )
    for axis, face, a0, a1, s in walls:
        def rect(d0, d1, b0, b1):
            """across the wall from depth d0 to d1 off its court face (+ = out into the court), along b0 .. b1."""
            p, q = sorted((face + s * d0, face + s * d1))
            return ((p, b0), (q, b1)) if axis == "x" else ((b0, p), (b1, q))

        def put(name, d0, d1, b0, b1, z0, z1, bevel, seg=1):
            (x0, y0), (x1, y1) = rect(d0, d1, b0, b1)
            parts.append(box(col, name, (x0, y0, z0), (x1, y1, z1), STONE, bevel=bevel, segments=seg))
        # the corbel table under the coping (over the court's own floor only)
        f0, f1 = (COURT_Y0, COURT_Y1) if axis == "x" else (-h, h)
        put("Court corbel band", -.01, .16, f0, f1, top - .5, top - .3, .03, 2)
        # R682 (environment-artist: break a long wall with piers): pilasters split each wall into bays, 5 cm proud of
        # the stone (the wall's box stands 3 cm behind it: 8 cm, inside the 10 cm a pawn may meet; 9 cm was 13), from the wall's base to the corbel table, where a
        # stepped console as wide as the cap carries the band; the small corbels spaced evenly in the bays between.
        pil = COURT_PILASTERS[axis]
        for p in pil:
            put("Court pilaster base", -.01, .05, p - .5, p + .5, TOP, TOP + .4, .03, 2)
            put("Court pilaster", -.01, .04, p - .42, p + .42, TOP + .4, top - 1.05, .04, 2)
            put("Court pilaster cap", -.01, .06, p - .5, p + .5, top - 1.05, top - .9, .03, 2)
            put("Court console", -.01, .11, p - .46, p + .46, top - .9, top - .76, .03, 2)
            put("Court console", -.01, .135, p - .38, p + .38, top - .76, top - .62, .03, 2)
            put("Court console", -.01, .155, p - .3, p + .3, top - .62, top - .5, .025, 2)
        edges = [f0] + [e for p in pil for e in (p - .5, p + .5)] + [f1]
        for g0, g1 in zip(edges[0::2], edges[1::2]):
            n = max(1, int(round((g1 - g0) / 1.0)))
            step = (g1 - g0) / n
            for k in range(n):
                b = g0 + step * (k + .5)
                put("Court corbel", -.01, .09, b - .11, b + .11, top - .9, top - .7, .02)
                put("Court corbel", -.01, .14, b - .13, b + .13, top - .7, top - .5, .025)
        # the merlons, standing on the coping's back two thirds (the wall is 1 m thick): a slit faces the court
        b = a0 + .5
        while b + 1.0 <= a1 + 1e-6:
            put("Court merlon plinth", .02, -.98, b - .03, b + 1.03, top, top + .1, .02)
            put("Court merlon", -.02, -.96, b + .02, b + .45, top + .1, top + .78, .03)
            put("Court merlon", -.02, -.96, b + .55, b + .98, top + .1, top + .78, .03)
            put("Court merlon", -.18, -.96, b + .45, b + .55, top + .1, top + .78, 0.0)
            put("Court merlon sill", -.02, -.18, b + .45, b + .55, top + .1, top + .3, 0.0)
            put("Court merlon head", -.02, -.18, b + .45, b + .55, top + .62, top + .78, 0.0)
            put("Court merlon cap", .02, -.98, b - .05, b + 1.05, top + .78, top + .9, .035, 2)
            b += MERLON_STEP
    return parts


MERLON_STEP = 2.0
# R682: the court walls' pilasters (along the side walls' y, along the far wall's x: one behind the crown).
COURT_PILASTERS = {"x": (163.75, 168.5, 173.25), "y": (-5.0, 0.0, 5.0)}


def pedestal(col):
    """The crown's pedestal (R682, prop-artist / lookdev): within the same footprint and height as before (the
    collider's), a stepped base with a rounded torus, a die with a sunk panel on each face (2.5 cm, inside the box) and
    corner fillets, a moulded cap, and the velvet cushion plumped with a deeper bevel and four buttons."""
    (a, b), (c, d), ph = PEDESTAL
    z = TOP
    parts = [box(col, "Pedestal base", (a, b, z), (c, d, z + .14), MARBLE, bevel=.04, segments=2),
             box(col, "Pedestal torus", (a + .04, b + .04, z + .14), (c - .04, d - .04, z + .24), MARBLE, bevel=.045, segments=4)]
    d0, d1 = z + .24, z + ph - .3
    i = .08
    parts.append(box(col, "Pedestal die", (a + i + .025, b + i + .025, d0), (c - i - .025, d - i - .025, d1), MARBLE))
    # each face: two side fillets and a top and bottom rail flush with the die's face, the panel sunk between them
    for lo, hi in (((a + i, b + i), (a + i + .16, d - i)), ((c - i - .16, b + i), (c - i, d - i)),
                   ((a + i, b + i), (c - i, b + i + .16)), ((a + i, d - i - .16), (c - i, d - i))):
        parts.append(box(col, "Pedestal fillet", (lo[0], lo[1], d0), (hi[0], hi[1], d1), MARBLE, bevel=.02, segments=2))
    for z0, z1 in ((d0, d0 + .12), (d1 - .12, d1)):
        parts.append(box(col, "Pedestal rail", (a + i, b + i, z0), (c - i, d - i, z1), MARBLE, bevel=.02, segments=2))
    parts.append(box(col, "Pedestal cap bed", (a + .05, b + .05, d1), (c - .05, d - .05, d1 + .1), MARBLE, bevel=.035, segments=3))
    parts.append(box(col, "Pedestal cap", (a, b, d1 + .1), (c, d, z + ph), MARBLE, bevel=.06, segments=3))
    top = z + ph
    parts.append(box(col, "Pedestal cushion", (-.75, 174.45, top), (.75, 175.95, top + .1), "RCY | velvet", bevel=.048, segments=4))
    for x in (-.35, .35):
        for y in (174.85, 175.55):
            parts.append(cylinder(col, "Cushion button", (x, y, top + .1), (0, 0, 1), .035, .016, "RCY | velvet", segments=10, bevel=.006))
    return parts


# ------------------------------------------------------------------ the section's floors (as a section only)


def section_floors(col):
    parts = []
    for name, y0, y1, top in (("Runout paving", PLATFORM_Y1, RUNOUT_Y1, TOP), ("Terrace paving", RUNOUT_Y1, TERRACE_Y1, TOP),
                              ("Band paving", BAND_Y0, BAND_Y1, TOP - 1.0)):
        parts.append(box(col, name + " core", (-6.0, y0, -3.0), (6.0, y1, top - 1.0), SHADE))
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
