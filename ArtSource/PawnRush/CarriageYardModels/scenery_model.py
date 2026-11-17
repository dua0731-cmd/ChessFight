# Royal Carriage Yard models (R680): the scenery the code still drew as blockout inside the map, the battlements on the
# side walls and the lamp posts on the canal's landings and on the platform.
#
#   RCY_BUILD = False; exec(open(r".../rcy_kit.py").read()); exec(open(r".../export_rcy.py").read())
#   exec(open(r".../entrance_model.py").read()); exec(open(r".../sorting_model.py").read())   # lathe, GOLD, lamp_post
#   RCY_BUILD = True;  exec(open(r".../scenery_model.py").read()); scenery_export()
#
# Builds, all grey clay, in the MAP frame (Blender x, y, z = Unity x, z, y), over the pictures RoyalCarriageYard.Scenery.cs
# puts in its two groups (none of them collides but the lamp posts, whose colliders stay the code's):
#   "RCY Scenery"      group "Scenery": the merlons on the yard's and the canal's side walls (x 22 .. 23 either side,
#                      on their tops at 5), the four lamp posts on the canal's landings
#   "RCY Scenery own"  group "3-4 (own frame)/Scenery (own frame)": the merlons on the sorting yard's and the platform's
#                      side walls (x 26 .. 27, on their tops at 11), the platform's two lamp posts, and along the
#                      sorting yard's stretch of those walls (to the cliff) a corbel table under their coping (10.15 ..
#                      10.75, out 0.16 at most: over the heavy berth's gantry, 9.9 at its top, and the banners' rods, 9.6;
#                      none over the platform, where a pawn's jump comes within reach of it)
# Left to the code: the lamps' glow (the lanterns are built round it), the banners, the towers and the palace outside the
# walls (the castle kit covers the towers at the gates).

MERLON_EVERY = 2.0      # the code's: a 1 m tooth every 2 m (Merlons, Scenery.cs)
MERLON_HIGH = .8


def merlon(col, x0, y0, top, s):
    """One merlon where the code's picture stands (x0 .. x0+1 across the wall, y0 .. y0+1 along it, top .. top+0.8): a
    plinth course on the coping, the body with an arrow slit on the face the players see (at x0+1 when s is +1, at x0
    when -1) and a cap."""
    x1, y1, ym = x0 + 1.0, y0 + 1.0, y0 + .5
    face = x1 if s > 0 else x0

    def across(t0, t1):
        """x from depth t0 to depth t1 behind the players' face."""
        return sorted((face - s * t0, face - s * t1))

    parts = [box(col, "Merlon plinth", (x0 - .02, y0 - .03, top), (x1 + .02, y1 + .03, top + .1), STONE, bevel=.02, segments=1)]
    b0, b1 = top + .1, top + MERLON_HIGH - .12
    slit, depth, sill, head = .06, .16, top + .22, top + .58
    a, c = across(depth + .02, .98)
    parts.append(box(col, "Merlon", (a, y0 + .02, b0), (c, y1 - .02, b1), STONE, bevel=.03, segments=1))
    a, c = across(.02, depth + .02)
    parts.append(box(col, "Merlon", (a, y0 + .02, b0), (c, ym - slit, b1), STONE, bevel=.012, segments=1))
    parts.append(box(col, "Merlon", (a, ym + slit, b0), (c, y1 - .02, b1), STONE, bevel=.012, segments=1))
    parts.append(box(col, "Merlon sill", (a, ym - slit, b0), (c, ym + slit, sill), STONE))
    parts.append(box(col, "Merlon head", (a, ym - slit, head), (c, ym + slit, b1), STONE))
    parts.append(box(col, "Merlon cap", (x0 - .04, y0 - .05, b1), (x1 + .04, y1 + .05, top + MERLON_HIGH), STONE, bevel=.035, segments=2))
    return parts


def merlons(col, x0, z0, z1, top, s, skip_below=None):
    """The code's Merlons(x, z0, z1, top): one every 2 m from z0 + 0.5 while short of z1 - 0.5 (map-frame numbers)."""
    parts, y = [], z0 + .5
    while y < z1 - .5:
        if skip_below is None or y >= skip_below:
            parts += merlon(col, x0, y, top, s)
        y += MERLON_EVERY
    return parts


CORBEL_EVERY = 1.0


def corbel_table(col, face, s, y0, y1):
    """Under the coping of the wall whose face is at x `face` (the yard on the side of -s): stepped corbels every metre
    carrying a moulded band (10.5 .. 10.75)."""

    def out(d):
        return sorted((face, face - s * d))

    parts = []
    a, b = out(.1)
    parts.append(box(col, "Corbel fillet", (a, y0, 10.5), (b, y1, 10.56), STONE, bevel=.015, segments=1))
    a, b = out(.16)
    parts.append(box(col, "Corbel band", (a, y0, 10.56), (b, y1, 10.75), STONE, bevel=.03, segments=2))
    # R682: the sorting yard's wall has pilasters now (sorting_model.wall_rhythm, their caps up to 10.15, 9 cm proud, 1.1
    # wide): over each a console as wide as the cap carries the band, and the small corbels are spaced evenly in the
    # bays between (they had run on at 1 m regardless, one straddling every second pilaster's cap off centre).
    pil = [y for y in globals().get("PILASTER_YS", PILASTER_YS) if y0 < y < y1]
    for y in pil:
        a, b = out(.1)
        parts.append(box(col, "Corbel console", (a, y - .5, 10.15), (b, y + .5, 10.27), STONE, bevel=.03, segments=2))
        a, b = out(.13)
        parts.append(box(col, "Corbel console", (a, y - .42, 10.27), (b, y + .42, 10.4), STONE, bevel=.03, segments=2))
        a, b = out(.155)
        parts.append(box(col, "Corbel console", (a, y - .34, 10.4), (b, y + .34, 10.5), STONE, bevel=.025, segments=2))
    edges = [y0] + [e for y in pil for e in (y - PILASTER_HALF, y + PILASTER_HALF)] + [y1]
    for g0, g1 in zip(edges[0::2], edges[1::2]):
        n = max(1, int(round((g1 - g0) / CORBEL_EVERY)))
        step = (g1 - g0) / n
        for k in range(n):
            y = g0 + step * (k + .5)
            a, b = out(.08)
            parts.append(box(col, "Corbel", (a, y - .11, 10.15), (b, y + .11, 10.33), STONE, bevel=.02, segments=1))
            a, b = out(.14)
            parts.append(box(col, "Corbel", (a, y - .13, 10.33), (b, y + .13, 10.5), STONE, bevel=.025, segments=1))
    return parts


PILASTER_YS = (91.5, 101.0, 110.5, 120.0, 129.5, 139.0)   # sorting_model.py's (used when it was not run first)
PILASTER_HALF = .55                                       # its pilaster cap's half width


def scenery(col):
    # The yard's and the canal's walls, z 0 .. 82; the two teeth at z 0.5 stand on no wall (the outer gate's towers are
    # there), so they go (their pictures with the rest).
    parts = merlons(col, -23.0, 0.0, 82.0, 5.0, 1, skip_below=2.0) + merlons(col, 22.0, 0.0, 82.0, 5.0, -1, skip_below=2.0)
    for x, y in ((-9.0, 51.0), (9.0, 51.0), (-9.0, 81.0), (9.0, 81.0)):
        parts += lamp_post(col, x, y)
    return parts


def scenery_own(col):
    # The sorting yard's and the platform's walls: own frame z 106 .. GateZ + 3 (183), map 82 .. 159.
    parts = merlons(col, -27.0, 82.0, 159.0, 11.0, 1) + merlons(col, 26.0, 82.0, 159.0, 11.0, -1)
    # The corbel table from the yard's corner (82) to the cliff (143).
    parts += corbel_table(col, -26.0, -1, 82.0, 143.0) + corbel_table(col, 26.0, 1, 82.0, 143.0)
    for x in (-8.0, 8.0):
        parts += lamp_post(col, x, 144.5, 8.0)
    return parts


def build_scenery():
    out = {}
    for cname, make in (("RCY Scenery", scenery), ("RCY Scenery own", scenery_own)):
        col = collection(cname)
        clear(col)
        out[cname] = join(col, cname[4:], make(col))
    print("scenery tris", {k: tris(bpy.data.collections[k]) for k in out})
    return out


def scenery_export():
    for cname, name in (("RCY Scenery", "RcyScenery"), ("RCY Scenery own", "RcySceneryOwn")):
        export(cname, name)


if globals().get("RCY_BUILD", True):
    scenery_parts = build_scenery()
