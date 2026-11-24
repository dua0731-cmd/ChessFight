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
#   "RCY Lift wheel"       the lifting waterwheel (it turns about x): hub, caps, rims, eight spoke pairs and paddles, and
#                          (11-23) the handles a pawn holds: rope-wound walnut rungs on iron brackets (lift_handle)

LIFT_R, LIFT_W = 5.2, 3.0          # VerticalWaterwheel: wheelRadius, wheelWidth
ROPE = "RCY | rope"
# R686 (textures): footings, plinths and stone under water are the concept's darker "shade stone" slot (rcy_kit
# names it SHADE; the fallback keeps this file running on an rcy_kit without it).
SHADE = globals().get("SHADE") or "RCY | shade"


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
    return cylinder(col, "Bolt", Vector(at) + n * .006, n, r, .012, mat, segments=6, smooth=False)


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
    """sorting_model.crate_pale with iron corner caps: `foot` the middle of its base, `size` (Blender x, y, z)."""
    parts = crate_pale(col, foot, size, dark)
    x, y, z = foot
    sx, sy, sz = size
    c, o = .15, PROUD
    for ex in (-1, 1):
        for ey in (-1, 1):
            xa, ya = x + ex * sx / 2, y + ey * sy / 2
            x0, x1 = sorted((xa + ex * o, xa - ex * c))
            y0, y1 = sorted((ya + ey * o, ya - ey * c))
            for z0, z1 in ((z, z + c), (z + sz - c, z + sz + o)):
                parts.append(box(col, "Crate cap", (x0, y0, z0), (x1, y1, z1), IRON, bevel=.01, segments=1))
    return parts


# ------------------------------------------------------------------ the conveyor


def conveyor(col):
    half, w = 23.5, .8
    parts = [ubox(col, "Belt bed", (0, .215, 0), (2 * half - .02, .43, 2 * w - .02), LEATHER)]
    # R685 (승규 11-16: the loads moved over a belt standing still): the oak slats (0.2 x 0.02 x 1.5 m, 0.45 m apart, top at
    # 0.45) are no longer in the model; ConveyorSlats draws them and runs them along with the loads at the belt's speed.
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
        parts.append(ubox(col, "Row post foot", (x, .2, 0), (.76, .4, .76), SHADE, bevel=.04, seg=2))
        for y in (1.7, 3.9, 5.9):
            parts.append(ubox(col, "Row post band", (x, y, 0), (.56 + 2 * PROUD, .14, .56 + 2 * PROUD), IRON, bevel=.01))
        parts.append(beam(col, "Row knee", U(x - s * .26, 5.0, 0), U(x - s * 1.75, 6.5, 0), w=.22, h=.22, mat=WALNUT))
        # The stock at the row's end (the code's "Row stock (collider)", x 19.8..23.5, 2.4 high, 1.8 deep): two stacks.
        for cx, w0 in ((s * 20.885, 2.13), (s * 22.74, 1.48)):
            parts += slatted_crate(col, (cx, 0, .02), (w0, 1.8, 1.16))
            parts += slatted_crate(col, (cx, 0, 1.22), (w0 - .1, 1.7, 1.16), dark=True)
    # The beam, its gold trim the code's (z .30..32), iron fish plates on its back face and top.
    parts.append(ubox(col, "Row beam", (0, 6.75, 0), (53.8, .5, .6), WALNUT, bevel=.02, seg=2))
    for x in urange(-24, 24.01, 6.0):
        parts.append(ubox(col, "Beam plate", (x, 6.75, -.3 - PROUD / 2), (.6, .4, PROUD), IRON, bevel=.005))
        parts.append(ubox(col, "Beam plate", (x, 7.0 + PROUD / 2, 0), (.6, PROUD, .5), IRON, bevel=.005))
        for y in (6.62, 6.88):
            for dx in (-.2, .2):
                parts.append(bolt(col, U(x + dx, y, -.3 - PROUD), U(0, 0, -1)))
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
            parts.append(ubox(col, "Gantry foot", (x, .15, z), (1.0, .3, 1.0), SHADE, bevel=.04, seg=2))
            for y in (2.2, 5.0, 8.0):
                parts.append(ubox(col, "Gantry band", (x, y, z), (.8 + 2 * PROUD, .16, .8 + 2 * PROUD), IRON, bevel=.01))
            parts.append(beam(col, "Gantry knee", U(x - sx * .4, 7.2, z), U(x - sx * 2.2, 9.0, z), w=.26, h=.26, mat=WALNUT))
        for y in (3.0, 6.8):
            parts.append(ubox(col, "Gantry tie", (x, y, 0), (.4, .4, 1.62), WALNUT, bevel=.02))
        # R686: the X's two members on the posts' two faces (in one plane they flickered where they cross).
        parts.append(beam(col, "Gantry cross", U(x - .12, 3.3, -.8), U(x - .12, 6.5, .8), w=.2, h=.2, mat=WALNUT))
        parts.append(beam(col, "Gantry cross", U(x + .12, 3.3, .8), U(x + .12, 6.5, -.8), w=.2, h=.2, mat=WALNUT))
        # The winch on the south posts' faces: a toothed wheel, its axle into the post, the crank.
        parts += gear(col, (sx * 25.9, -1.7, 5.8), 1.1, .1, teeth=20)
        parts.append(cylinder(col, "Winch axle", U(sx * 25.9, 5.8, -1.45), U(0, 0, 1), .08, .5, IRON, segments=10))
        parts.append(ubox(col, "Winch crank", (sx * 25.9, 5.45, -1.85), (.06, .7, .05), IRON))
        parts.append(cylinder(col, "Crank grip", U(sx * 25.9, 5.1, -1.95), U(0, 0, 1), .035, .2, WALNUT, segments=8))
    for sz in (-1, 1):
        parts.append(ubox(col, "Gantry beam", (0, 9.4, sz * 1.2), (54.2, .8, .7), WALNUT, bevel=.025, seg=2))
        for x in urange(-24, 24.01, 6.0):
            parts.append(ubox(col, "Beam plate", (x, 9.4, sz * (.85 - PROUD / 2)), (.7, .6, PROUD), IRON, bevel=.005))
            for y in (9.2, 9.6):
                for dx in (-.25, .25):
                    parts.append(bolt(col, U(x + dx, y, sz * (.85 - PROUD)), U(0, 0, -sz)))
        # The track under each beam's inner edge.
        parts.append(ubox(col, "Track", (0, 8.95, sz * .9), (52.0, .1, .3), IRON))
    for x in urange(-24, 24.01, 4.0):
        # R686: under the tracks (their tops were 1 cm apart and flickered: rcy-textures' 1.5 cm check).
        parts.append(ubox(col, "Track tie", (x, 8.87, 0), (.15, .06, 1.9), IRON))
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
    # R686: the bands round the chest 2 cm proud of the upright ones front and back (flush, they flickered where they cross).
    parts.append(ubox(col, "Chest band", (0, -.672, 0), (3.28, .16, 2.52), IRON, bevel=.01))
    parts.append(ubox(col, "Chest band", (0, .5, 0), (3.28, .06, 2.52), IRON))
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
    # R686 (승규 11-17 "왕관 지워"): no crown (nor its cushion) on the lid.
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
    # R686 (승규 "줄이 감겨 보이도록 해서 디테일하게"; the concept's coffer winch, RCY_08 8 · RCY_09 8): a walnut drum with
    # hemp rope wound on it in two layers, bolted iron flanges at both ends and a middle one, iron journals into the cheeks.
    parts.append(cylinder(col, "Winch drum", U(0, -.38, 0), U(1, 0, 0), .15, 3.0, WALNUT, segments=20))
    for a, b in ((-1.3, -.06), (.06, 1.3)):
        parts.append(rope_coil(col, "Wound rope", a, b, 0, -.38, .184))
        parts.append(rope_coil(col, "Wound rope", a + .07, b - .21, 0, -.38, .25))
    for x in (-1.36, 0, 1.36):
        parts.append(cylinder(col, "Drum flange", U(x, -.38, 0), U(1, 0, 0), .33, .06, IRON, segments=24, bevel=.008))
        for k in range(8):
            t = 2 * math.pi * k / 8
            parts.append(bolt(col, (x + .035, .26 * math.cos(t), -.38 + .26 * math.sin(t)), (1, 0, 0), r=.022))
            parts.append(bolt(col, (x - .035, .26 * math.cos(t), -.38 + .26 * math.sin(t)), (-1, 0, 0), r=.022))
    for sx in (-1, 1):
        parts.append(cylinder(col, "Drum journal", U(sx * 1.47, -.38, 0), U(1, 0, 0), .07, .16, IRON, segments=12))
    parts += wheel(col, "Drum wheel", U(1.42, -.38, 0), U(1, 0, 0), .3, .05, spokes=6)
    return parts


# ------------------------------------------------------------------ the timber cranes


def drum_block(col, name, r0, r1, a0, a1, z0, z1, mat=STONE, bevel=.018, step=math.radians(6)):
    """R686 (the concept's crane tower): entrance_model.arc_block with 6-degree steps and round shading (its 15-degree
    facets showed on a 1.2 m drum), the block's corners kept sharp."""
    steps = max(2, int(abs(a1 - a0) / step) + 1)
    bm = bmesh.new()
    rows = []
    for z in (z0, z1):
        row = [bm.verts.new((r1 * math.cos(a0 + (a1 - a0) * i / steps), r1 * math.sin(a0 + (a1 - a0) * i / steps), z))
               for i in range(steps + 1)]
        row += [bm.verts.new((r0 * math.cos(a0 + (a1 - a0) * i / steps), r0 * math.sin(a0 + (a1 - a0) * i / steps), z))
                for i in range(steps, -1, -1)]
        rows.append(row)
    n = len(rows[0])
    bm.faces.new(rows[0])
    bm.faces.new(list(reversed(rows[1])))
    for k in range(n):
        bm.faces.new((rows[0][k], rows[0][(k + 1) % n], rows[1][(k + 1) % n], rows[1][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    sharp_creases(bm)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        bevel_mod(ob, bevel, 1)
    return ob


def crane_tower(col):
    """R686 (승규 "목재 크레인 ... 시작"; the concept's timber crane stands on a crenellated round stone tower, RCY_08 5 and
    RCY_09 4, its knight crest to the yard): the drum the code collides with (radius 1.2, top 2.4) as a shade-stone
    plinth, four staggered courses of cream ashlar on a darker core (the joints read as mortar), a corbel band and a
    parapet of eight merlons whose tops stay at 2.4 (the jib's house turns on them), and a cream shield with a brass
    knight on the south face (Unity -z, the way players come). Every face a pawn can touch within 5 cm of the drum."""
    import random
    rnd = random.Random(686)
    R = 1.2
    parts = [lathe(col, "Drum core", [(0, 0), (1.175, 0), (1.175, 2.25), (0, 2.25)], (0, 0, 0), seg=40, mat=SHADE,
                   smooth=True, sharp=True)]
    # R686 (승규 "하단부가 두껍게 컨셉 아트랑 똑같게"): a two-step base (r 1.30 to .12 up, r 1.255 to .38) - at most 10 cm
    # past the drum's collider, on the floor.
    parts.append(lathe(col, "Drum plinth", [(0, 0), (1.3, 0), (1.3, .1), (1.285, .12), (1.255, .12), (1.255, .36),
                                            (1.235, .38), (0, .38)], (0, 0, 0), seg=60, mat=SHADE, smooth=True, sharp=True))
    n = 10
    for i, (z0, z1) in enumerate(((.38, .80), (.80, 1.22), (1.22, 1.63), (1.63, 2.04))):
        phase = (i % 2) * math.pi / n + .05
        for k in range(n):
            a0 = phase + 2 * math.pi * k / n + .012
            a1 = phase + 2 * math.pi * (k + 1) / n - .012
            parts.append(drum_block(col, "Drum stone", .98, R + rnd.uniform(-.004, .012), a0, a1, z0 + .011, z1 - .011))
    parts.append(lathe(col, "Drum corbel", [(0, 2.04), (1.2, 2.04), (1.24, 2.08), (1.24, 2.135), (1.215, 2.15), (0, 2.15)],
                       (0, 0, 0), seg=60, mat=STONE, smooth=True, sharp=True))
    m = 8
    w = 2 * math.pi / m
    for k in range(m):
        a = w * k + w / 4
        parts.append(drum_block(col, "Merlon", .96, R + .005, a + .015, a + w * .58, 2.15, 2.4))
        parts.append(drum_block(col, "Crenel sill", .96, R, a + w * .58 + .015, a + w - .015, 2.15, 2.26))
    return parts


def slew_crane(col):
    parts = crane_tower(col)
    parts.append(arc_ring(col, "Turntable", 0, 0, .3, 1.15, 2.4, 2.43, WALNUT, seg=32, bevel=.005))
    # R686: the mast is the jib's now (slew_jib: a square post turns with it, as the concept's).
    # The rest boards each side (the load lies on them between slews) and their chocks.
    for s in (-1, 1):
        for k in range(4):
            parts.append(ubox(col, "Rest board", (s * 6, .01, -.345 + k * .23), (8.4, .02, .22), OAK, bevel=.004))
        for x in (2.6, 4.6, 6.6, 8.6):
            parts.append(ubox(col, "Rest chock", (s * x, .06, 0), (.25, .1, .72), WALNUT, bevel=.02))
    return parts


# R686 (승규 11-17 "디테일이 부족하고 깜빡깜빡 거려"): iron that wraps a timber stands 3 cm proud of it on every side. At
# 2 cm, rcy_kit.unfight (2 cm) took the band for a fighting face and pushed the whole band 2 cm to one side, so its
# far side sat flush on the timber and flickered (the crane's jib and mast, the gantry and row posts).
PROUD = .03


def iron_band(col, name, c, size, bolts_on=("y", "z"), r=.03):
    """R686 (the concept's cranes): an iron strap round a timber (Unity centre `c`, size), a bolt head on the middle of
    each side named in `bolts_on` ('x' / 'y' / 'z', both faces)."""
    parts = [ubox(col, name, c, size, IRON, bevel=.006)]
    for ax in bolts_on:
        k = "xyz".index(ax)
        for sgn in (-1, 1):
            at = list(c)
            at[k] += sgn * size[k] / 2
            nrm = [0, 0, 0]
            nrm[k] = sgn
            parts.append(bolt(col, U(*at), U(*nrm), r=r))
    return parts


MAST_W, JIB_W = .6, .5


def crane_mast(col, top):
    """R686 (승규 "컨셉 아트처럼 전체적인 부분이랑 하단부가 두껍게"; RCY_08 5 · RCY_09 4): a heavy squared oak mast (0.6;
    the code's box is 0.45, all of it above 2.4 m where no pawn reaches) turning with the jib on its iron shoe, a broad
    collar block where it rises out of the house roof (the concept's thick foot over the tower), bolted iron bands, and
    the jib clamped on its head between two bolted iron cheek plates under a cap."""
    w = MAST_W
    parts = [ubox(col, "Crane mast", (0, (top + 7.6) / 2, 0), (w, 7.6 - top, w), OAK, bevel=.035, seg=2)]
    parts.append(ubox(col, "Mast shoe", (0, top + .11, 0), (w + .16, .22, w + .16), IRON, bevel=.012))
    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(bolt(col, U(sx * (w / 2 + .08), top + .11, sz * .2), U(sx, 0, 0)))
            parts.append(bolt(col, U(sz * .2, top + .11, sx * (w / 2 + .08)), U(0, 0, sx)))
    # The collar: a broad oak block round the mast's foot over the roof, iron-banded top and bottom.
    c0, c1, cw = 4.45, 5.35, .9
    parts.append(ubox(col, "Mast collar", (0, (c0 + c1) / 2, 0), (cw, c1 - c0, cw), OAK, bevel=.045, seg=2))
    for y in (c0 + .1, c1 - .1):
        parts += iron_band(col, "Collar band", (0, y, 0), (cw + 2 * PROUD, .14, cw + 2 * PROUD), ("x", "z"))
    for y in (6.05, 6.8):
        parts += iron_band(col, "Mast band", (0, y, 0), (w + 2 * PROUD, .16, w + 2 * PROUD), ("x", "z"))
    # The head clamp: cheek plates either side of mast and jib, four bolts each, a cap over the jib.
    for sz in (-1, 1):
        parts.append(ubox(col, "Mast cheek", (0, 7.45, sz * (w / 2 + .018)), (w + .3, 1.1, .035), IRON, bevel=.008))
        for x in (-w / 2, w / 2):
            for y in (7.08, 7.72):
                parts.append(bolt(col, U(x, y, sz * (w / 2 + .035)), U(0, 0, sz), r=.032))
    parts.append(ubox(col, "Mast cap", (0, 7.6 + JIB_W / 2 + .02, 0), (w + .16, .04, JIB_W + .12), IRON, bevel=.008))
    return parts


def timber_brace(col, name, a, b, w=.26):
    """R686: a diagonal oak brace between Unity points a and b (y up, x along the jib), bolted iron plates at both
    ends on both faces."""
    parts = [beam(col, name, U(a[0], a[1], 0), U(b[0], b[1], 0), w=w, h=w, mat=OAK)]
    for (x, y) in (a, b):
        for sz in (-1, 1):
            parts.append(ubox(col, "Brace plate", (x, y, sz * (w / 2 + .015)), (.5, .5, .03), IRON, bevel=.006))
            for dx, dy in ((-.14, -.14), (.14, .14)):
                parts.append(bolt(col, U(x + dx, y + dy, sz * (w / 2 + .03)), U(0, 0, sz)))
    return parts


def rope_coil(col, name, x0, x1, cy, cz, R, r=.034, pitch=.07, per_turn=12, sides=6, mat=ROPE):
    """R686 (승규 "줄이 감겨 보이도록"): a rope wound round a drum along Blender x (axis at y = cy, z = cz) - one tube on a
    helix of radius R, its turns `pitch` apart (2r: touching), round shaded, its ends capped."""
    turns = (x1 - x0) / pitch
    n = max(2, int(turns * per_turn))
    bm = bmesh.new()
    rings = []
    for i in range(n + 1):
        t = 2 * math.pi * turns * i / n
        c = Vector((x0 + (x1 - x0) * i / n, cy + R * math.cos(t), cz + R * math.sin(t)))
        radial = Vector((0, math.cos(t), math.sin(t)))
        tangent = Vector((pitch / (2 * math.pi), -R * math.sin(t), R * math.cos(t))).normalized()
        side = tangent.cross(radial).normalized()
        rings.append([bm.verts.new(c + r * (math.cos(2 * math.pi * k / sides) * radial + math.sin(2 * math.pi * k / sides) * side))
                      for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            f = bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
            f.smooth = True
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_object(name, bm, col, (mat,))


def slew_jib(col):
    parts = []
    hx, top = -.2, 2.4
    # The house: posts, sill and plates, boarded walls, a door on the south (-z) side.
    for ex in (-1, 1):
        for ez in (-1, 1):
            # R686: 0.2 square, so its faces stand 3 cm out of the boards' (0.16 left them 1 cm apart: they flickered).
            parts.append(ubox(col, "House post", (hx + ex * .92, top + .9, ez * .82), (.2, 1.8, .2), WALNUT, bevel=.015))
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
    parts += crane_mast(col, top)
    # The treadwheel on the house's north side.
    parts += gear(col, (hx, 1.0, top + 1.05), .9, .1, teeth=16)
    # The jib (its gold trim the code's), its head, the counterweight, the brace and the back stay.
    # R686: the jib in honey oak as the concept's, with bolted iron bands (were thin bare straps every 1.5 m).
    # R686 (승규 "컨셉 아트처럼 전체적인 부분"): 0.5 thick (the code's box is 0.4, 7.6 m up).
    parts.append(ubox(col, "Jib", (3.9, 7.6, 0), (13.0, JIB_W, JIB_W), OAK, bevel=.035, seg=2))
    for x in (-2.0, -.85, 1.05, 3.6, 6.4, 8.9):
        parts += iron_band(col, "Jib band", (x, 7.6, 0), (.16, JIB_W + 2 * PROUD, JIB_W + 2 * PROUD), ("y", "z"))
    for sz in (-1, 1):
        parts.append(ubox(col, "Jib cheek", (10.15, 7.5, sz * (JIB_W / 2 + .02)), (.6, .55, .03), IRON, bevel=.005))
    parts.append(ubox(col, "Jib end cap", (10.4, 7.6, 0), (.06, JIB_W + 2 * PROUD, JIB_W + 2 * PROUD), IRON, bevel=.008))
    parts += wheel(col, "Jib sheave", U(10.15, 7.45, 0), U(0, 0, 1), .2, .06, spokes=4)
    # R686 (승규 "나이트 장식은 하지 말고"): no knight on the jib's end (an iron cap above). The counterweight as the
    # concept's: a dark iron block with a crenellated top hanging from the jib on two iron hangers (was a grey box).
    wx, wy0, wy1 = -2.4, 6.45, 6.95
    parts.append(ubox(col, "Counterweight", (wx, (wy0 + wy1) / 2, 0), (1.0, wy1 - wy0, 1.0), IRON, bevel=.03, seg=2))
    for dx in (-.35, .35):
        for dz in (-.35, .35):
            parts.append(ubox(col, "Weight merlon", (wx + dx, wy1 + .1, dz), (.28, .2, .28), IRON, bevel=.02))
    for dx in (-.35, .35):
        for sz in (-1, 1):
            parts.append(ubox(col, "Weight hanger", (wx + dx, (wy1 + 7.4) / 2, sz * .3), (.08, 7.4 - wy1 + .1, .04), IRON, bevel=.005))
        parts.append(ubox(col, "Weight hanger", (wx + dx, 7.87, 0), (.08, .04, .66), IRON, bevel=.005))
        parts.append(bolt(col, U(wx + dx, wy0 + .2, -.5), U(0, 0, -1), r=.035))
        parts.append(bolt(col, U(wx + dx, wy0 + .2, .5), U(0, 0, 1), r=.035))
    # Two diagonal oak braces from the collar to the jib, one each side of the mast (the concept's), bolted plates.
    parts += timber_brace(col, "Jib brace", (.62, 5.05), (3.15, 7.32))
    parts += timber_brace(col, "Back brace", (-.62, 5.05), (-1.7, 7.32))
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
    for sz, mat in ((-1, TIMBER), (1, OAK)):   # R686 (textures): raw sawn timber, an oak beam beside it; the ends stay dark
        parts.append(ubox(col, "Timber", (lx, ly + .085, sz * .105), (7.98, .16, .2), mat, bevel=.015))
    parts.append(ubox(col, "Timber", (lx, ly + .295, 0), (7.98, .25, .21), TIMBER, bevel=.015))
    for x in (2.5, 4.0, 5.5, 7.0, 8.5):
        parts.append(ubox(col, "Timber band", (x, ly + .085, 0), (.1, .26, .5), IRON, bevel=.006))
        parts.append(ubox(col, "Timber strap", (x, ly + .32, 0), (.1, .32, .27), IRON, bevel=.006))
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
    # R682 (the concept): a carved knight's head on each end of the arm, under the roof's edge (its lamps are inboard).
    for sx in (-1, 1):
        parts.append(knight_finial(col, "Arm knight", U(sx * 2.75, 5.66, 0), sx, .8, .28))   # its ears just past the roof
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


def lift_handle(col):
    """11-23 (승규 "마지막 구간 물레방아 손잡이 부분도 노란색 말고 다른 걸로 모델링 해줘"): the bar a pawn holds, which was
    the code's gold cylinder and its two gold posts (VerticalWaterwheel: centre (0, 4.65, .32) in the paddle's frame, 0.16
    thick, 2.5 long across x; posts at x +-1.2). Now a worn walnut rung of the same size, rope wound round where hands go
    (two grips, turn by turn), iron ferrules at its ends and an iron bracket each side bolted to the paddle's face."""
    c, R, L = (0.0, 4.65, .32), .08, 2.5
    parts = [cylinder(col, "Handle rung", U(*c), U(1, 0, 0), R, L, WALNUT, segments=12)]
    for s in (-1, 1):
        # A grip of rope: eight turns from 0.25 to 0.85 out, each a short drum with a hair of gap.
        for t in range(8):
            x = s * (.25 + .075 * t + .0375)
            parts.append(cylinder(col, "Handle rope", U(x, c[1], c[2]), U(1, 0, 0), R + .018, .068, ROPE, segments=12))
        parts.append(cylinder(col, "Handle ferrule", U(s * (L / 2 - .04), c[1], c[2]), U(1, 0, 0), R + .012, .08, IRON, segments=12))
        # The bracket: a strap round the rung and a plate down to the paddle's face (z .18), two bolts on it.
        parts.append(cylinder(col, "Handle strap", U(s * 1.2, c[1], c[2]), U(1, 0, 0), R + .02, .07, IRON, segments=12))
        parts.append(ubox(col, "Handle bracket", (s * 1.2, c[1], (c[2] + .18) / 2), (.07, .1, c[2] - .18), IRON, bevel=.006))
        parts.append(ubox(col, "Handle plate", (s * 1.2, c[1], .18 + .012), (.16, .26, .024), IRON, bevel=.004))
        for dy in (-.08, .08):
            parts.append(bolt(col, U(s * 1.2, c[1] + dy, .18 + .024), U(0, 0, 1), r=.022))
    return parts


def lift_wheel(col):
    """R686 (승규 11-17 "2번째 사진도 컨셉아트에 맞춰서"; RCY_09 3 "Upright paddle wheel", RCY_06): the concept's chunky
    paddle wheel - two thick rims studded with iron bolts, eight broad spokes a face, deep stepped paddles of three
    planks standing out past the rims with an iron edge and battens behind, a wooden hub with an iron face plate, a brass
    collar and boss. Every paddle and spoke where VerticalWaterwheel's are (its grab boxes); since 11-23 the handles
    too (lift_handle: rope-wound walnut rungs on iron brackets, the code's gold bars no longer drawn). No crown (승규 11-17 "왕관 지워"). The rims and spokes in oak (a darker override per model, as the giant
    wheel's), the paddles in pale sawn timber, the hub walnut."""
    half, rim = LIFT_W * .5, LIFT_R - .35
    r_in, r_out, rim_d = rim - .22, rim + .2, .28
    parts = [lathe(col, "Hub", [(0, -half - .06), (.72, -half - .06), (.76, -half - .02), (.76, half + .02), (.72, half + .06), (0, half + .06)],
                   (0, 0, 0), axis="x", seg=28, mat=WALNUT, smooth=True)]
    for x in (-1.0, -.35, .35, 1.0):
        parts.append(ring_x(col, "Hub hoop", x, .74, .79, -.05, .05, IRON, seg=28))
    for s in (-1, 1):
        # The hub's face: an iron plate with a ring of bolts, a brass collar and a brass boss on it.
        fx = s * (half + .1)
        parts.append(cylinder(col, "Hub plate", (fx, 0, 0), (1, 0, 0), .98, .08, IRON, 40, .01))
        for k in range(10):
            t = 2 * math.pi * k / 10 + math.pi / 10
            parts.append(bolt(col, (fx + s * .04, .82 * math.cos(t), .82 * math.sin(t)), (s, 0, 0), r=.04))
        x0, x1 = sorted((fx + s * .04, fx + s * .1))
        parts.append(ring_x(col, "Hub collar", 0, .5, .68, x0, x1, BRASS, seg=32, bevel=.008))
        cap = [(0, 0), (.42, 0), (.42, .05), (.33, .1), (.18, .13), (0, .14)]
        parts.append(lathe(col, "Hub boss", [(r, s * (half + .14 + h)) for r, h in cap], (0, 0, 0), axis="x", seg=24, mat=BRASS, smooth=True))
        # The rims: thick oak rings with iron bolt heads round both faces.
        x0, x1 = sorted((s * (half - rim_d), s * half))
        parts.append(ring_x(col, "Rim", 0, r_in, r_out, x0, x1, OAK, seg=48, bevel=.015))
        for k in range(24):
            t = 2 * math.pi * k / 24
            for face in (x0, x1):
                n = -1 if face == x0 else 1
                parts.append(bolt(col, (face + n * .004, (rim - .01) * math.cos(t), (rim - .01) * math.sin(t)), (n, 0, 0), r=.045))
    for k in range(8):
        blade = []
        for s in (-1, 1):
            # Broad spokes, their faces 4 cm inside the rim's (no flicker), an iron strap near the hub.
            sx = s * (half - rim_d / 2)
            blade.append(ubox(col, "Spoke", (sx, (r_in + .7) * .5, 0), (rim_d - .08, r_in - .7 + .05, .34), OAK, bevel=.025, seg=2))
            blade.append(ubox(col, "Spoke strap", (sx, 1.25, 0), (rim_d - .08 + 2 * PROUD, .14, .34 + 2 * PROUD), IRON, bevel=.006))
        # The paddle: three deep planks from 4.15 to 5.35 out (the code's paddle 4.2 .. 5.2), 0.36 thick, past the rims.
        for j in range(3):
            y0 = 4.15 + j * .4
            blade.append(ubox(col, "Paddle plank", (0, y0 + .195, 0), (LIFT_W - .1, .39, .36), TIMBER, bevel=.02, seg=2))
        blade.append(ubox(col, "Paddle edge", (0, 5.35 - .03, 0), (LIFT_W - .04, .1, .36 + 2 * PROUD), IRON, bevel=.008))
        for s in (-1, 1):
            blade.append(ubox(col, "Paddle batten", (s * .75, 4.75, -.18 - .025), (.14, 1.15, .05), WALNUT, bevel=.008))
        blade += lift_handle(col)
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
