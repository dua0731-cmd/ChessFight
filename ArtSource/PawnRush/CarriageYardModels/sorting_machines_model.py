# Royal Carriage Yard models (R678): zone 3's machines, each in the frame of the transform the code builds it on.
#
#   RCY_BUILD = False; exec(rcy_kit.py); exec(entrance_model.py); exec(sorting_model.py)   # gear, crate, crown ...
#   RCY_BUILD_MOVERS = False; exec(movers_model.py)                                       # ring_x, ellipse_ring, dome
#   exec(open(r".../sorting_machines_model.py").read())
#
# Every number is the code's (CargoConveyor, StackRow, StackCrate, CargoSlam, SlamChest, SlewCrane, SlewJib, PalletHoist,
# HoistPallet, VerticalWaterwheel; read off the Play dump), written as Unity (x, y, z) in the machine's own frame and
# turned to Blender by U(). Where a pawn touches a part - a collider - the model keeps to its box (승규 11-15: box
# colliders only, models fitted to them). Collections, each one FBX (export_rcy.py), all grey clay:
#   "RCY Conveyor"         the conveyor's frame on its root: the belt bed, its oak slats flush with the belt's top, the
#                          side rails with their brackets, bolts and iron feet (the gold trim on the rails stays the code's)
#   "RCY Conveyor crate"   a single load (ConveyorCrate's pivot in the crate's middle); "RCY Conveyor stack" the two-high
#   "RCY Stack row"        a storage row's frame: the posts, knee braces, the beam, the trolleys' rail, the stock stacks at
#                          both ends (the beam's gold trim, the trolleys' lamps, the ropes, the gap marks stay the code's)
#   "RCY Stack crate"      a moving stack (StackCrate's pivot on the floor): two crates, iron eyes on top
#   "RCY Row trolley"      a row's trolley, full size (the code scales its box: Unity hangs this under a holder that
#                          undoes that): the carriage, its wheels and cheeks, the rope drum, the lamp's hood
#   "RCY Slam gantry"      the heavy berth's gantry: four posts on stone feet, ties, braces, knees, the winch wheels, the
#                          two beams and the trolley's track (the beams' gold bands stay the code's)
#   "RCY Slam chest"       the giant chest (SlamChest's pivot in its middle): planked body, lid, iron bands with studs,
#                          corner irons, lock and hasp, end handles, the cushion and the crown
#   "RCY Slam trolley"     the chest's trolley, full size (held as the row's): the frame, wheels, the chain drum
#   "RCY Slew crane"       a timber crane's standing part: the stone drum, its band, the mast, the rest boards and chocks
#   "RCY Slew jib"         what turns (SlewJib): the house and its roof, the treadwheel, the jib and its head, the
#                          counterweight, the brace, the slings and the timber load (the jib's gold trim, the lamp stay)
#   "RCY Hoist frame"      a loft hoist's standing part: the framed partition between the shafts, the lucam post, arm and
#                          roof, the pulleys (the arm's gold trim, the lamps, ropes and the well's boards stay the code's)
#   "RCY Hoist pallet P" / "N"   a pallet (HoistPallet's pivot on the plate's top) with its two sacks at local +x / -x
#   "RCY Lift wheel"       the lifting waterwheel (it turns about x): hub, caps, rims, eight spoke pairs and paddles (the
#                          gold handles a pawn holds stay the code's)

LIFT_R, LIFT_W = 5.2, 3.0          # VerticalWaterwheel: wheelRadius, wheelWidth
ROPE = "RCY | rope"


def U(x, y, z):
    """Unity (x, y, z) in the machine's frame -> Blender."""
    return Vector((x, z, y))


def ubox(col, name, c, s, mat, bevel=0.0, seg=1):
    """A box from the code's centre and size (Unity)."""
    c, s = U(*c), Vector((s[0], s[2], s[1]))
    return box(col, name, c - s / 2, c + s / 2, mat, bevel=bevel, segments=seg)


def urange(a, b, step):
    out, x = [], a
    while x < b - 1e-6:
        out.append(x)
        x += step
    return out


def bolt(col, at, normal, r=.025, mat=IRON):
    """A bolt head on a face: `at` and `normal` Blender."""
    n = Vector(normal).normalized()
    return cylinder(col, "Bolt", Vector(at) + n * .006, n, r, .012, mat, segments=6)


def rope(col, name, a, b, r=.035):
    a, b = Vector(a), Vector(b)
    return cylinder(col, name, (a + b) / 2, b - a, r, (b - a).length, ROPE, segments=8)


def wheel(col, name, centre, axis, r, thick, spokes=6, mat=IRON, hub=None):
    """A spoked wheel (a sheave, a trolley wheel): rim, spokes, hub, round `axis` (Blender)."""
    c, t = Vector(centre), Vector(axis).normalized()
    side = Vector((1, 0, 0)) if abs(t.x) < .9 else Vector((0, 0, 1))
    u = (side - t * side.dot(t)).normalized()
    v = t.cross(u)
    band = min(.08, r * .3)
    parts = [ellipse_ring(col, name + " rim", c, t, r - band / 2, r - band / 2, band, thick, mat, seg=24, side=side)]
    for k in range(spokes):
        a = 2 * math.pi * k / spokes
        d = u * math.cos(a) + v * math.sin(a)
        parts.append(beam(col, name + " spoke", c + d * (hub or r * .2), c + d * (r - band * .8), w=max(.025, r * .12), h=thick * .6, mat=mat, segments=1))
    parts.append(cylinder(col, name + " hub", c, t, hub or r * .22, thick * 1.3, mat, segments=10))
    return parts


def place(objs, matrix):
    """Moves freshly built parts (identity objects) by `matrix` (Blender)."""
    for o in objs:
        o.matrix_world = matrix @ o.matrix_world
    return objs


def slatted_crate(col, foot, size, dark=False):
    """entrance_model.crate with iron corner caps: `foot` the middle of its base, `size` (Blender x, y, z)."""
    parts = crate(col, foot, size, dark)
    x, y, z = foot
    sx, sy, sz = size
    c, o = .13, .006
    for ex in (-1, 1):
        for ey in (-1, 1):
            xa, ya = x + ex * sx / 2, y + ey * sy / 2
            x0, x1 = sorted((xa + ex * o, xa - ex * c))
            y0, y1 = sorted((ya + ey * o, ya - ey * c))
            for z0 in (z, z + sz - c):
                parts.append(box(col, "Crate cap", (x0, y0, z0), (x1, y1, z0 + c), IRON, bevel=.008, segments=1))
    return parts


# ------------------------------------------------------------------ the conveyor


def conveyor(col):
    half, w = 23.5, .8
    parts = [ubox(col, "Belt bed", (0, .215, 0), (2 * half - .02, .43, 2 * w - .02), LEATHER)]
    for x in urange(-half + .25, half, .45):
        parts.append(ubox(col, "Belt slat", (x, .44, 0), (.2, .02, 2 * w - .1), OAK, bevel=.005))
    for s in (-1, 1):
        z = s * (w + .04)
        parts.append(ubox(col, "Belt rail", (0, .245, z), (2 * half, .39, .08), WALNUT, bevel=.012))
        for x in urange(-half + .8, half, 2.4):
            parts.append(ubox(col, "Rail bracket", (x, .24, z + s * .046), (.16, .34, .012), IRON))
            for y in (.13, .35):
                parts.append(bolt(col, U(x, y, z + s * .052), U(0, 0, s)))
            parts.append(ubox(col, "Rail foot", (x, .025, s * (w + .05)), (.22, .05, .12), IRON, bevel=.01))
    return parts


def conveyor_crate(col):
    return slatted_crate(col, (0, 0, -.58), (1.3, 1.3, 1.16))


def conveyor_stack(col):
    parts = slatted_crate(col, (0, 0, -1.18), (1.3, 1.3, 1.16))
    parts += slatted_crate(col, (0, 0, .02), (1.18, 1.18, 1.16), dark=True)
    # Two ropes lashing the top crate down.
    for y in (-.3, .3):
        parts.append(box(col, "Lashing", (-.61, y - .025, 1.18), (.61, y + .025, 1.2), ROPE))
        for x in (-1, 1):
            parts.append(box(col, "Lashing", (x * .6 - .01, y - .025, .02), (x * .6 + .01, y + .025, 1.2), ROPE))
    return parts


# ------------------------------------------------------------------ the storage rows


def stack_row(col):
    parts = []
    for s in (-1, 1):
        x = s * 26.45
        parts.append(ubox(col, "Row post", (x, 3.5, 0), (.56, 7.0, .56), WALNUT, bevel=.03, seg=2))
        parts.append(ubox(col, "Row post foot", (x, .2, 0), (.76, .4, .76), STONE, bevel=.04, seg=2))
        for y in (1.7, 3.9, 5.9):
            parts.append(ubox(col, "Row post band", (x, y, 0), (.6, .12, .6), IRON, bevel=.01))
        parts.append(beam(col, "Row knee", U(x - s * .26, 5.0, 0), U(x - s * 1.75, 6.5, 0), w=.22, h=.22, mat=WALNUT))
        # The stock at the row's end (the code's "Row stock (collider)", x 19.8..23.5, 2.4 high, 1.8 deep): two stacks.
        for cx, w0 in ((s * 20.885, 2.13), (s * 22.74, 1.48)):
            parts += slatted_crate(col, (cx, 0, .02), (w0, 1.8, 1.16))
            parts += slatted_crate(col, (cx, 0, 1.22), (w0 - .1, 1.7, 1.16), dark=True)
    # The beam, its gold trim the code's (z .30..32), iron fish plates on its back face and top.
    parts.append(ubox(col, "Row beam", (0, 6.75, 0), (53.8, .5, .6), WALNUT, bevel=.02, seg=2))
    for x in urange(-24, 24.01, 6.0):
        parts.append(ubox(col, "Beam plate", (x, 6.75, -.305), (.6, .4, .012), IRON))
        parts.append(ubox(col, "Beam plate", (x, 7.005, 0), (.6, .012, .5), IRON))
        for y in (6.62, 6.88):
            for dx in (-.2, .2):
                parts.append(bolt(col, U(x + dx, y, -.311), U(0, 0, -1)))
    # The trolleys' rail under the beam: a flange and a web, hung every 3 m.
    parts.append(ubox(col, "Rail flange", (0, 6.435, 0), (52.1, .03, .3), IRON))
    parts.append(ubox(col, "Rail web", (0, 6.475, 0), (52.1, .05, .05), IRON))
    for x in urange(-24.6, 24.61, 3.0):
        parts.append(ubox(col, "Rail hanger", (x, 6.47, 0), (.1, .06, .34), IRON))
    return parts


def stack_crate(col):
    parts = slatted_crate(col, (0, 0, .02), (2.0, 1.8, 1.16))
    parts += slatted_crate(col, (0, 0, 1.22), (1.9, 1.7, 1.16), dark=True)
    for x in (-.55, .55):
        parts.append(ellipse_ring(col, "Lifting eye", (x, 0, 2.47), (0, 1, 0), .07, .07, .025, .025, IRON, seg=12))
        parts.append(box(col, "Eye plate", (x - .1, -.06, 2.38), (x + .1, .06, 2.4), IRON))
    return parts


def row_trolley(col):
    parts = [ubox(col, "Trolley body", (0, -.07, 0), (1.0, .3, .8), IRON, bevel=.02, seg=2)]
    for s in (-1, 1):
        parts.append(ubox(col, "Trolley cheek", (0, .17, s * .19), (.9, .2, .03), IRON))
        for x in (-.3, .3):
            parts += wheel(col, "Trolley wheel", U(x, .2, s * .165), U(0, 0, 1), .06, .03, spokes=4)
    for x in urange(-.4, .41, .2):
        for s in (-1, 1):
            parts.append(bolt(col, U(x, .03, s * .4), U(0, 0, s), r=.02))
    parts.append(cylinder(col, "Rope drum", U(0, -.27, 0), U(1, 0, 0), .07, .8, WALNUT, segments=12))
    parts.append(ubox(col, "Lamp hood", (0, -.065, -.45), (.33, .03, .12), IRON))
    return parts


# ------------------------------------------------------------------ the heavy berth


def slam_gantry(col):
    parts = []
    for sx in (-1, 1):
        x = sx * 26.5
        for sz in (-1, 1):
            z = sz * 1.2
            parts.append(ubox(col, "Gantry post", (x, 4.95, z), (.8, 9.9, .8), WALNUT, bevel=.03, seg=2))
            parts.append(ubox(col, "Gantry foot", (x, .15, z), (1.0, .3, 1.0), STONE, bevel=.04, seg=2))
            for y in (2.2, 5.0, 8.0):
                parts.append(ubox(col, "Gantry band", (x, y, z), (.84, .14, .84), IRON, bevel=.01))
            parts.append(beam(col, "Gantry knee", U(x - sx * .4, 7.2, z), U(x - sx * 2.2, 9.0, z), w=.26, h=.26, mat=WALNUT))
        for y in (3.0, 6.8):
            parts.append(ubox(col, "Gantry tie", (x, y, 0), (.4, .4, 1.62), WALNUT, bevel=.02))
        parts.append(beam(col, "Gantry cross", U(x, 3.3, -.8), U(x, 6.5, .8), w=.2, h=.2, mat=WALNUT))
        parts.append(beam(col, "Gantry cross", U(x, 3.3, .8), U(x, 6.5, -.8), w=.2, h=.2, mat=WALNUT))
        # The winch on the south posts' faces: a toothed wheel, its axle into the post, the crank.
        parts += gear(col, (sx * 25.9, -1.7, 5.8), 1.1, .1, teeth=20)
        parts.append(cylinder(col, "Winch axle", U(sx * 25.9, 5.8, -1.45), U(0, 0, 1), .08, .5, IRON, segments=10))
        parts.append(ubox(col, "Winch crank", (sx * 25.9, 5.45, -1.85), (.06, .7, .05), IRON))
        parts.append(cylinder(col, "Crank grip", U(sx * 25.9, 5.1, -1.95), U(0, 0, 1), .035, .2, WALNUT, segments=8))
    for sz in (-1, 1):
        parts.append(ubox(col, "Gantry beam", (0, 9.4, sz * 1.2), (54.2, .8, .7), WALNUT, bevel=.025, seg=2))
        for x in urange(-24, 24.01, 6.0):
            parts.append(ubox(col, "Beam plate", (x, 9.4, sz * .845), (.7, .6, .012), IRON))
            for y in (9.2, 9.6):
                for dx in (-.25, .25):
                    parts.append(bolt(col, U(x + dx, y, sz * .839), U(0, 0, -sz)))
        # The track under each beam's inner edge.
        parts.append(ubox(col, "Track", (0, 8.95, sz * .9), (52.0, .1, .3), IRON))
    for x in urange(-24, 24.01, 4.0):
        parts.append(ubox(col, "Track tie", (x, 8.96, 0), (.15, .06, 1.9), IRON))
    return parts


def slam_chest(col):
    parts = [ubox(col, "Chest body", (0, -.35, 0), (3.14, 1.7, 2.34), WALNUT)]
    # Horizontal boards, a little proud, on all four faces.
    for k in range(5):
        yc, hh = -1.2 + k * .34 + .17, .33
        for sz in (-1, 1):
            parts.append(ubox(col, "Chest board", (0, yc, sz * 1.18), (3.16, hh, .04), OAK, bevel=.008))
        for sx in (-1, 1):
            parts.append(ubox(col, "Chest board", (sx * 1.58, yc, 0), (.04, hh, 2.3), OAK, bevel=.008))
    parts.append(ubox(col, "Chest lid", (0, .862, 0), (3.26, .72, 2.46), WALNUT, bevel=.04, seg=2))
    parts.append(ubox(col, "Lid panel", (0, 1.235, 0), (2.6, .03, 1.8), OAK, bevel=.01))
    # Iron: two upright bands round body and lid, one round the foot, one at the lid's seam, studs on them.
    for sx in (-1, 1):
        parts.append(ubox(col, "Chest band", (sx * .96, 0, 0), (.2, 2.45, 2.48), IRON, bevel=.01))
        for y in urange(-1.05, 1.1, .3):
            for sz in (-1, 1):
                parts.append(dome(col, "Stud", U(sx * .96, y, sz * 1.24), "y", .035, sgn=sz))
    parts.append(ubox(col, "Chest band", (0, -.672, 0), (3.28, .16, 2.48), IRON, bevel=.01))
    parts.append(ubox(col, "Chest band", (0, .5, 0), (3.28, .06, 2.48), IRON))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(ubox(col, "Corner iron", (sx * 1.55, 0, sz * 1.15), (.2, 2.44, .2), IRON, bevel=.02))
    # The lock on the south face (-z) and the hasp from the lid.
    parts.append(ubox(col, "Lock plate", (0, .288, -1.225), (.5, .6, .05), BRASS, bevel=.01))
    parts.append(ubox(col, "Keyhole", (0, .23, -1.255), (.06, .16, .01), IRON))
    parts.append(ubox(col, "Hasp", (0, .62, -1.245), (.16, .4, .03), IRON))
    # Handles on the ends.
    for sx in (-1, 1):
        parts.append(cylinder(col, "Handle", U(sx * 1.65, .1, 0), U(0, 0, 1), .04, .7, IRON, segments=8))
        for sz in (-1, 1):
            parts.append(ubox(col, "Handle eye", (sx * 1.62, .1, sz * .32), (.08, .1, .06), IRON))
    # The crown on its cushion.
    parts.append(ubox(col, "Cushion", (0, 1.31, 0), (.9, .2, .9), CLOTH, bevel=.06, seg=2))
    parts += crown(col, (0, 0, 1.41), .62)
    return parts


def slam_trolley(col):
    parts = []
    for sz in (-1, 1):
        parts.append(ubox(col, "Trolley girder", (0, 0, sz * 1.15), (3.8, .5, .3), WALNUT, bevel=.02))
        for x in (-1.5, 1.5):
            parts += wheel(col, "Trolley wheel", U(x, .24, sz * 1.2), U(0, 0, 1), .11, .08, spokes=5)
        for x in urange(-1.6, 1.61, .4):
            parts.append(bolt(col, U(x, 0, sz * 1.3), U(0, 0, sz)))
    for sx in (-1, 1):
        parts.append(ubox(col, "Trolley girder", (sx * 1.75, 0, 0), (.3, .5, 2.0), WALNUT, bevel=.02))
        parts.append(ubox(col, "Drum cheek", (sx * 1.55, -.42, 0), (.06, .46, .7), IRON, bevel=.01))
    parts.append(ubox(col, "Trolley deck", (0, .22, 0), (3.2, .05, 2.0), OAK))
    parts.append(cylinder(col, "Chain drum", U(0, -.38, 0), U(1, 0, 0), .17, 3.0, IRON, segments=16))
    for x in urange(-1.2, 1.21, .3):
        wrap = ring_x(col, "Chain wrap", x, .17, .2, -.04, .04, IRON, seg=16)
        wrap.location = (x, 0, -.38)
        parts.append(wrap)
    parts += wheel(col, "Drum wheel", U(1.42, -.38, 0), U(1, 0, 0), .3, .05, spokes=6)
    return parts


# ------------------------------------------------------------------ the timber cranes


def drum_profile():
    prof = [(0, 0), (1.215, 0), (1.215, .22), (1.2, .23)]
    for y in (.8, 1.4, 1.95):
        prof += [(1.2, y - .02), (1.185, y - .01), (1.185, y + .01), (1.2, y + .02)]
    prof += [(1.2, 2.38), (1.19, 2.4), (0, 2.4)]
    return prof


def slew_crane(col):
    parts = [lathe(col, "Crane drum", drum_profile(), (0, 0, 0), seg=32, mat=STONE, smooth=True)]
    parts.append(arc_ring(col, "Drum band", 0, 0, 1.17, 1.23, 2.215, 2.285, IRON, seg=40, bevel=.005))
    parts.append(arc_ring(col, "Turntable", 0, 0, .3, 1.15, 2.4, 2.43, WALNUT, seg=32, bevel=.005))
    parts.append(cylinder(col, "Crane mast", (0, 0, 4.9), (0, 0, 1), .2, 5.0, WALNUT, segments=12))
    for y in (2.6, 4.6, 7.2):
        parts.append(arc_ring(col, "Mast collar", 0, 0, .19, .23, y, y + .1, IRON, seg=16, bevel=0))
    # The rest boards each side (the load lies on them between slews) and their chocks.
    for s in (-1, 1):
        for k in range(4):
            parts.append(ubox(col, "Rest board", (s * 6, .01, -.345 + k * .23), (8.4, .02, .22), OAK, bevel=.004))
        for x in (2.6, 4.6, 6.6, 8.6):
            parts.append(ubox(col, "Rest chock", (s * x, .06, 0), (.25, .1, .72), WALNUT, bevel=.02))
    return parts


def slew_jib(col):
    parts = []
    hx, top = -.2, 2.4
    # The house: posts, sill and plates, boarded walls, a door on the south (-z) side.
    for ex in (-1, 1):
        for ez in (-1, 1):
            parts.append(ubox(col, "House post", (hx + ex * .92, top + .9, ez * .82), (.16, 1.8, .16), WALNUT, bevel=.015))
    for y in (top + .06, top + 1.74):
        for ez in (-1, 1):
            parts.append(ubox(col, "House plate", (hx, y, ez * .82), (2.0, .12, .16), WALNUT, bevel=.01))
        for ex in (-1, 1):
            parts.append(ubox(col, "House plate", (hx + ex * .92, y, 0), (.16, .12, 1.8), WALNUT, bevel=.01))
    for x in urange(hx - .84, hx + .84, .24):
        for ez in (-1, 1):
            if ez < 0 and abs(x + .12 - hx) < .3:
                continue
            parts.append(ubox(col, "House board", (x + .12, top + .9, ez * .87), (.23, 1.56, .04), OAK))
    for z in urange(-.74, .74, .24):
        for ex in (-1, 1):
            parts.append(ubox(col, "House board", (hx + ex * .97, top + .9, z + .12), (.04, 1.56, .23), OAK))
    parts.append(ubox(col, "House door", (hx, top + .9, -.865), (.72, 1.56, .05), WALNUT, bevel=.01))
    parts.append(ubox(col, "Door strap", (hx, top + .5, -.895), (.6, .06, .012), IRON))
    parts.append(ubox(col, "Door strap", (hx, top + 1.3, -.895), (.6, .06, .012), IRON))
    # The roof: a gable along x over the code's roof box (4.2..4.4), tiled.
    eave, ridge, half = top + 1.8, top + 2.35, 1.07
    parts.append(prism(col, "House roof", [(-half, eave), (half, eave), (half, eave + .08), (0, ridge + .08), (-half, eave + .08)],
                       hx - 1.15, hx + 1.15, TILE, bevel=.01, axis="x"))
    for k in range(1, 4):
        f = k / 4
        for sz in (-1, 1):
            z0, y0 = sz * half * (1 - f), eave + .08 + (ridge - eave) * f
            parts.append(beam(col, "Roof course", U(hx - 1.15, y0 + .02, z0), U(hx + 1.15, y0 + .02, z0), w=.04, h=.05, mat=TILE, segments=1))
    parts.append(beam(col, "Roof ridge", U(hx - 1.17, ridge + .1, 0), U(hx + 1.17, ridge + .1, 0), w=.1, h=.1, mat=TILE, segments=1))
    # The treadwheel on the house's north side.
    parts += gear(col, (hx, 1.0, top + 1.05), .9, .1, teeth=16)
    # The jib (its gold trim the code's), its head, the counterweight, the brace and the back stay.
    parts.append(ubox(col, "Jib", (3.9, 7.6, 0), (13.0, .4, .4), WALNUT, bevel=.02, seg=2))
    for x in urange(-2.0, 10.0, 1.5):
        parts.append(ubox(col, "Jib strap", (x, 7.6, 0), (.08, .42, .42), IRON))
    for sz in (-1, 1):
        parts.append(ubox(col, "Jib cheek", (10.15, 7.5, sz * .22), (.6, .5, .03), IRON, bevel=.005))
    parts += wheel(col, "Jib sheave", U(10.15, 7.45, 0), U(0, 0, 1), .2, .06, spokes=4)
    parts.append(ubox(col, "Counterweight", (-2.4, 6.95, 0), (1.0, .9, 1.0), STONE, bevel=.05, seg=2))
    for x in (-2.75, -2.05):
        parts.append(ubox(col, "Weight strap", (x, 7.0, 0), (.08, 1.0, 1.04), IRON))
    parts.append(beam(col, "Jib brace", U(.8, top + 2.0, 0), U(.8 + 3.15, top + 2.0 + 3.15, 0), w=.22, h=.22, mat=WALNUT))
    parts.append(beam(col, "Back stay", U(hx - .9, top + 1.8, 0), U(-2.4, 7.4, 0), w=.06, h=.06, mat=IRON, segments=1))
    # The slings, their blocks under the jib and the hooks over the load.
    load_top = .13 + .47
    for x in (3.6, 8.4):
        parts.append(rope(col, "Sling", U(x, load_top + .25, 0), U(x, 7.35, 0), r=.03))
        parts.append(ubox(col, "Sling block", (x, 7.28, 0), (.18, .16, .14), IRON, bevel=.02))
        parts.append(ubox(col, "Sling hook", (x, load_top + .17, 0), (.1, .16, .06), IRON, bevel=.01))
        parts.append(ubox(col, "Sling wrap", (x, .37, 0), (.07, .5, .46), ROPE, bevel=.02))
    # The timber: two beams under one, iron bands round the pair, a strap over the ridge beam, darker ends. The ridge
    # beam runs the load's full length: the code's roof collider does (the blockout's shorter one left 15 cm of air).
    lx, ly = 6.0, .13
    for sz, mat in ((-1, OAK), (1, WALNUT)):
        parts.append(ubox(col, "Timber", (lx, ly + .085, sz * .105), (7.98, .16, .2), mat, bevel=.015))
    parts.append(ubox(col, "Timber", (lx, ly + .295, 0), (7.98, .25, .21), OAK, bevel=.015))
    for x in (2.5, 4.0, 5.5, 7.0, 8.5):
        parts.append(ubox(col, "Timber band", (x, ly + .085, 0), (.08, .2, .45), IRON))
        parts.append(ubox(col, "Timber strap", (x, ly + .3, 0), (.08, .27, .23), IRON))
    for e in (-1, 1):
        for sz in (-1, 1):
            parts.append(ubox(col, "Timber end", (lx + e * 3.995, ly + .085, sz * .105), (.012, .14, .18), WALNUT))
        parts.append(ubox(col, "Timber end", (lx + e * 3.995, ly + .295, 0), (.012, .23, .19), WALNUT))
    return parts


# ------------------------------------------------------------------ the loft hoists


def hoist_frame(col):
    parts = []
    # The partition between the shafts (the code's collider box: x .2, y 3.6, z 1.2 each way), framed and boarded.
    for sz in (-1, 1):
        parts.append(ubox(col, "Shaft post", (0, 1.8, sz * 1.1), (.4, 3.6, .2), WALNUT, bevel=.015))
    for y in (.08, 1.8, 3.52):
        parts.append(ubox(col, "Shaft rail", (0, y, 0), (.4, .16, 2.0), WALNUT, bevel=.015))
    for z in urange(-.98, .98, .245):
        parts.append(ubox(col, "Shaft board", (0, 1.8, z + .1225), (.36, 3.44, .235), OAK))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(ubox(col, "Guide", (sx * .205, 1.8, sz * .9), (.012, 3.5, .06), IRON))
    # The lucam: its post, the arm (gold trim the code's), the arm's tie back to the post, braces, the pulleys.
    parts.append(ubox(col, "Lucam post", (0, 5.0, 1.0), (.4, 2.8, .4), WALNUT, bevel=.02))
    parts.append(ubox(col, "Lucam arm", (0, 6.0, 0), (5.5, .3, .3), WALNUT, bevel=.015))
    parts.append(ubox(col, "Lucam tie", (0, 6.0, .475), (.28, .28, .65), WALNUT, bevel=.015))
    parts.append(beam(col, "Lucam brace", U(0, 4.4, .82), U(0, 5.86, .12), w=.16, h=.16, mat=WALNUT))
    for sx in (-1, 1):
        parts.append(beam(col, "Arm brace", U(sx * .2, 4.9, .82), U(sx * 1.0, 5.86, .1), w=.12, h=.12, mat=WALNUT))
        x = sx * 1.415
        parts += wheel(col, "Pulley", U(x, 5.65, 0), U(0, 0, 1), .25, .06, spokes=6)
        for sz in (-1, 1):
            parts.append(ubox(col, "Pulley cheek", (x, 5.77, sz * .05), (.12, .2, .015), IRON))
    # The roof: boards and tile courses on the code's slab, which leans 12 degrees (high to the post).
    roof = [box(col, "Lucam roof", (-2.9, -.6, -.06), (2.9, .6, 0), OAK, bevel=.01, segments=1)]
    for k in range(6):
        y = -.6 + k * .2
        roof.append(box(col, "Roof tile course", (-2.92, y - .02, 0), (2.92, y + .22, .07), TILE, bevel=.012, segments=1))
    place(roof, Matrix.Translation(U(0, 6.55, .2)) @ Matrix.Rotation(math.radians(12), 4, "X"))
    return parts + roof


def hoist_pallet(col, sign):
    parts = []
    # The plate (the code's: 2.37 across, .22 thick, 2.4 deep, its top at the pivot): edge frame, bearers, top boards.
    for sz in (-1, 1):
        parts.append(ubox(col, "Pallet edge", (0, -.135, sz * 1.15), (2.37, .17, .1), WALNUT, bevel=.01))
    for sx in (-1, 1):
        parts.append(ubox(col, "Pallet edge", (sx * 1.135, -.135, 0), (.1, .17, 2.2), WALNUT, bevel=.01))
    for x in (-.55, .55):
        parts.append(ubox(col, "Pallet bearer", (x, -.135, 0), (.16, .17, 2.2), WALNUT))
    for k in range(5):
        parts.append(ubox(col, "Pallet board", (0, -.025, -1.2 + .24 + k * .48), (2.36, .05, .47), OAK, bevel=.006))
    for a in (-1, 1):
        for b in (-1, 1):
            parts.append(ubox(col, "Pallet corner", (a * 1.105, -.11, b * 1.12), (.18, .24, .18), IRON, bevel=.01))
    # Two sacks at the outer side, tied, a cord round each.
    for k, z in enumerate((-.85, .85)):
        bottom = U(sign * .885, 0, z)
        parts.append(lumpy(col, "Sack", bottom, (.23, .23, .37), SACK, 31 + k * 7 + (3 if sign > 0 else 0)))
        parts.append(lathe(col, "Sack neck", [(0, 0), (.09, 0), (.05, .05), (.045, .09), (.08, .14), (0, .16)],
                           U(sign * .885, .56, z), seg=8, mat=SACK, smooth=True))
        parts.append(arc_ring(col, "Sack cord", bottom.x, bottom.y, .2, .235, .3, .34, ROPE, seg=16, bevel=0))
    return parts


# ------------------------------------------------------------------ the lifting waterwheel


def lift_wheel(col):
    half, rim = LIFT_W * .5, LIFT_R - .35
    parts = [lathe(col, "Hub", [(0, -half - .1), (.6, -half - .1), (.64, -half - .06), (.64, half + .06), (.6, half + .1), (0, half + .1)],
                   (0, 0, 0), axis="x", seg=24, mat=WALNUT, smooth=True)]
    for x in (-1.15, -.45, .45, 1.15):
        parts.append(ring_x(col, "Hub hoop", x, .62, .67, -.04, .04, IRON, seg=24))
    for s in (-1, 1):
        cap = [(0, 0), (.45, 0), (.45, .05), (.36, .1), (.2, .13), (0, .14)]
        parts.append(lathe(col, "Hub cap", [(r, s * (half + .09 + h)) for r, h in cap], (0, 0, 0), axis="x", seg=20, mat=GOLD, smooth=True))
        x0, x1 = sorted((s * (half - .2), s * half))
        parts.append(ring_x(col, "Rim", 0, rim - .15, rim + .15, x0, x1, WALNUT, seg=40, bevel=.01))
        parts.append(ring_x(col, "Rim tyre", 0, rim + .14, rim + .17, x0 + .02, x1 - .02, IRON, seg=40, bevel=0))
        parts.append(ring_x(col, "Hub flange", 0, .6, .7, s * (half - .1) - .1, s * (half - .1) + .1, IRON, seg=16, bevel=0))
    for k in range(8):
        blade = []
        for s in (-1, 1):
            blade.append(ubox(col, "Spoke", (s * (half - .1), (rim + .65) * .5, 0), (.2, rim - .65, .22), WALNUT, bevel=.02))
            blade.append(ubox(col, "Spoke strap", (s * (half - .1), 1.2, 0), (.22, .1, .24), IRON))
        for j in range(3):
            blade.append(ubox(col, "Paddle board", (0, LIFT_R - 1.0 + j / 3 + 1 / 6, 0), (LIFT_W - .1, .328, .16), OAK, bevel=.008))
        blade.append(ubox(col, "Paddle edge", (0, LIFT_R - .03, 0), (LIFT_W - .1, .06, .17), IRON))
        for s in (-1, 1):
            blade.append(ubox(col, "Paddle cleat", (s * .7, LIFT_R - .5, -.1), (.1, .9, .04), WALNUT))
        parts += place(blade, Matrix.Rotation(-math.radians(k * 45), 4, "X"))
    return parts


# ------------------------------------------------------------------ build


SORTING_MACHINES = [
    ("RCY Conveyor", "RcyConveyor", conveyor),
    ("RCY Conveyor crate", "RcyConveyorCrate", conveyor_crate),
    ("RCY Conveyor stack", "RcyConveyorStack", conveyor_stack),
    ("RCY Stack row", "RcyStackRow", stack_row),
    ("RCY Stack crate", "RcyStackCrate", stack_crate),
    ("RCY Row trolley", "RcyRowTrolley", row_trolley),
    ("RCY Slam gantry", "RcySlamGantry", slam_gantry),
    ("RCY Slam chest", "RcySlamChest", slam_chest),
    ("RCY Slam trolley", "RcySlamTrolley", slam_trolley),
    ("RCY Slew crane", "RcySlewCrane", slew_crane),
    ("RCY Slew jib", "RcySlewJib", slew_jib),
    ("RCY Hoist frame", "RcyHoistFrame", hoist_frame),
    ("RCY Hoist pallet P", "RcyHoistPalletP", lambda c: hoist_pallet(c, 1)),
    ("RCY Hoist pallet N", "RcyHoistPalletN", lambda c: hoist_pallet(c, -1)),
    ("RCY Lift wheel", "RcyLiftWheel", lift_wheel),
]


def build_sorting_machines(only=None):
    parent = collection("RCY Sorting machines")
    made = {}
    for name, fbx, builder in SORTING_MACHINES:
        if only and name not in only:
            continue
        col = collection(name, parent)
        clear(col)
        join(col, name.replace("RCY ", ""), builder(col))
        made[name] = tris(col)
    return made


if globals().get("RCY_BUILD_MACHINES", True):
    print(build_sorting_machines())
