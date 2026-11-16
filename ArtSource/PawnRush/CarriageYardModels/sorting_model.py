# Royal Carriage Yard models (R678): zone 3, the royal loading and sorting yard and the lifting wheel's basin.
#
#   RCY_BUILD = False; exec(open(r".../rcy_kit.py").read()); exec(open(r".../entrance_model.py").read())
#   RCY_BUILD = True;  exec(open(r".../sorting_model.py").read())
#
# Builds, all grey clay over the code's blockout (whose colliders stay), in the MAP frame (the code builds zones 3-4 in
# their own frame under "3-4 (own frame)", moved LaterShift -24; every number below is already the map's):
#   "RCY Sorting"  the yard's paving and its walls, the sorting stalls along both walls (cloth awnings on posts, a bay
#                  of goods each, the bay numbers, the scales, the conveyor's two hatches), the timber stores beside the
#                  crane field, the loft and its stacked ends, the heavy berth's four boards, the lamp posts, the
#                  basin's edge and bed, the lifting wheel's piers, bearings and axle, its landing, and both basin docks
#                  (sluice arch and raised gate, gear, moored barge and its load, mooring ropes, jib crane and sack net,
#                  banners on the cliff).
# Left to the code: every moving obstacle (conveyor, storage rows, timber cranes, loft hoists, heavy chest, lifting
# wheel), the water, the falls and the foam, the two dark hatch and sluice tunnels, the lamps' glow, the gold edges that
# tell a player where to stand or drop (the basin's edge, the landing's, the loft's), the signs and labels.

STALL_FRONT, SORT_HALF, STALL_Y0, STALL_Y1 = 23.5, 26.0, 82.0, 129.5
CONVEYOR_Y, HATCH_HALF, HATCH_TOP = 88.0, 1.15, 3.2
STORE_FRONT, STORE_Y0, STORE_Y1 = 20.11, 97.8, 113.4
LOFT_Y0, LOFT_Y1, LOFT_HALF, LOFT_TOP, LOFT_END_TOP = 113.4, 116.6, 12.0, 2.4, 4.4
BERTH_Y, BERTH_XS = 125.4, (-13.5, -4.5, 4.5, 13.5)
POOL_Y0, CLIFF_Y, TOP, WATER = 132.85, 143.0, 8.0, -.5
LIFT_Y, LIFT_AXLE, LANDING_Y = 137.0, 4.6, 141.4


# ------------------------------------------------------------------ the yard's floor and walls


def yard_and_walls(col):
    parts = [flagstones(col, "Sorting paving", -SORT_HALF, SORT_HALF, STALL_Y0, POOL_Y0, 0.0, size=2.0)]
    for s in (-1, 1):
        x0, x1 = sorted((s * SORT_HALF, s * (SORT_HALF + 1)))
        face = "-x" if s > 0 else "+x"
        # Beside the yard from its floor (one plain core behind the stalls' goods and awnings), beside the basin from its
        # bed, beside the platform over its top; big courses on so tall a wall.
        parts.append(ashlar(col, "Side wall", (x0, 81.0, 0), (x1, STALL_Y1, 10.75), face, course=.75, length=1.6, below=4.4))
        parts.append(ashlar(col, "Side wall", (x0, STALL_Y1, 0), (x1, POOL_Y0, 10.75), face, course=.75, length=1.6))
        parts.append(ashlar(col, "Side wall", (x0, POOL_Y0, -3.0), (x1, CLIFF_Y, 10.75), face, course=.75, length=1.6, below=WATER))
        parts.append(ashlar(col, "Side wall", (x0, CLIFF_Y, TOP), (x1, 159.0, 10.75), face, course=.75, length=1.6))
        g = -s * .05
        parts.append(box(col, "Side wall coping", (min(x0, x0 + g), 81.0, 10.75), (max(x1, x1 + g), 159.0, 11.0), STONE, bevel=.04))
        parts.append(box(col, "Side wall plinth", (min(x0, x0 + g * 1.2), 81.0, 0), (max(x1, x1 + g * 1.2), POOL_Y0, .35), STONE, bevel=.03))
        # The yard's south corners where the canal's landing opens 20 m wide.
        a0, a1 = sorted((s * 22.0, s * 27.0))
        parts.append(ashlar(col, "Arena wall", (a0, 81.0, 0), (a1, 82.0, 5.8), "+y"))
        parts.append(box(col, "Arena wall coping", (a0 - .04, 80.96, 5.8), (a1 + .04, 82.04, 6.0), STONE, bevel=.04))
        # The wall's end pier, inside the wall's own box (it faces the canal's landing where pawns walk).
        end = s * 22.0
        p0, p1 = sorted((end, end + s * .6))
        parts.append(box(col, "Arena wall pier", (p0, 80.95, 0), (p1, 82.05, 6.3), STONE, bevel=.04))
        parts.append(lathe(col, "Pier finial", [(0, 6.3), (.3, 6.3), (.3, 6.42), (.18, 6.5), (.24, 6.75), (.1, 6.95), (0, 7.0)],
                           (end + s * .3, 81.5, 0), seg=12, mat=STONE))
    return parts


# ------------------------------------------------------------------ the sorting stalls


def cloth_awning(col, back, eave, y0, y1, high=4.6, low=3.4):
    """A cloth awning a bay long from the wall (x `back`, `high`) down to its eave (x `eave`, `low`): sagging a little
    between its two end rafters, a gold-trimmed eave pole, the scalloped valance."""
    parts = []
    nx, ny = 6, 10
    bm = bmesh.new()
    grid = []
    for i in range(nx + 1):
        t = i / nx
        row = []
        for j in range(ny + 1):
            u = j / ny
            x = back + (eave - back) * t
            z = high + (low - high) * t - .1 * math.sin(math.pi * u) * math.sin(math.pi * min(1.0, t * 1.4))
            row.append(bm.verts.new((x, y0 + (y1 - y0) * u, z + .07)))
        grid.append(row)
    for i in range(nx):
        for j in range(ny):
            f = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
            f.smooth = True
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    ob = mesh_object("Stall awning", bm, col, (CLOTH,))
    sol = ob.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness, sol.offset = .05, -1
    parts.append(ob)
    for y in (y0 + .06, y1 - .06):
        parts.append(beam(col, "Awning rafter", (back, y, high), (eave, y, low), w=.1, h=.12, mat=WALNUT))
    parts.append(cylinder(col, "Awning pole", (eave, (y0 + y1) / 2, low + .02), (0, 1, 0), .06, y1 - y0, GOLD, segments=10))
    parts += valance(col, eave + (.04 if back > eave else -.04), y0, y1, low - .04, .3, width=.68)
    return parts


def bay_number(col, x, y, side, n):
    """The gold board on a bay's post with its number cut in it (pips, one to five, then a bar for five)."""
    parts = [box(col, "Bay number", (x - .03, y - .25, 2.73), (x + .03, y + .25, 3.07), GOLD, bevel=.012, segments=1)]
    fx = x - side * .035
    bars, pips = divmod(n, 5)
    k = 0
    for _ in range(bars):
        parts.append(box(col, "Bay number mark", (fx - .01, y - .18, 2.83 + k * .1), (fx + .01, y + .18, 2.88 + k * .1), WALNUT))
        k += 1
    for p in range(pips):
        cy = y - .16 + p * .08
        parts.append(box(col, "Bay number mark", (fx - .01, cy - .025, 2.95), (fx + .01, cy + .025, 3.0), WALNUT))
    return parts


def hatch(col, side, front, back, y0, y1):
    """The conveyor's hatch in its bay: iron posts, a gold lintel, a cloth valance over the loads, the board over it
    (its 입고/분류 label is the code's), crates either side of it in the rest of the bay."""
    parts = []
    a, b = CONVEYOR_Y - HATCH_HALF, CONVEYOR_Y + HATCH_HALF
    # The posts stand at the belt's end beside its loads: back in the goods' face to within 7 cm of it (the code's
    # pictures stood 19 cm proud).
    px = front + side * .05
    for y in (a - .14, b + .14):
        parts += square_post(col, "Hatch post", px, y, 0, HATCH_TOP + .25, w=.24, mat=IRON, bands=(.4, HATCH_TOP - .5))
    parts.append(box(col, "Hatch lintel", (px - .13, a - .42, HATCH_TOP - .01), (px + .13, b + .42, HATCH_TOP + .25), GOLD, bevel=.03))
    parts += valance(col, front - side * .03, a, b, HATCH_TOP - .02, .24, width=.58)
    bx = front - side * .2
    parts.append(box(col, "Hatch board", (bx - .03, CONVEYOR_Y - .8, HATCH_TOP + .27), (bx + .03, CONVEYOR_Y + .8, HATCH_TOP + .97), WALNUT, bevel=.02))
    parts.append(box(col, "Hatch board frame", (bx - .045, CONVEYOR_Y - .84, HATCH_TOP + .25), (bx + .045, CONVEYOR_Y + .84, HATCH_TOP + .3), GOLD, bevel=.01, segments=1))
    parts.append(box(col, "Hatch board frame", (bx - .045, CONVEYOR_Y - .84, HATCH_TOP + .94), (bx + .045, CONVEYOR_Y + .84, HATCH_TOP + .99), GOLD, bevel=.01, segments=1))
    x = front + side * 1.25
    for lo_, hi_ in ((y0 + .2, a - .35), (b + .35, y1 - .2)):
        if hi_ - lo_ < .8:
            continue
        w = min(2.2, hi_ - lo_)
        mid = (lo_ + hi_) / 2
        parts += crate(col, (x, mid, 0), (2.2, w, 1.2))
        if w > 1.2:
            parts += crate(col, (x + side * .2, mid, 1.2), (1.4, w - .3, 1.2), dark=True)
    return parts


def scale(col, x, y):
    """The big scale over a stall's goods: an iron post, the gold beam, two pans on chains."""
    parts = [box(col, "Scale foot", (x - .25, y - .25, 0), (x + .25, y + .25, .12), IRON, bevel=.02)]
    parts.append(cylinder(col, "Scale post", (x, y, 1.6), (0, 0, 1), .07, 3.2, IRON, segments=10))
    parts.append(box(col, "Scale beam", (x - .06, y - 1.1, 3.1), (x + .06, y + 1.1, 3.2), GOLD, bevel=.02))
    parts.append(lathe(col, "Scale pivot", [(0, 3.2), (.1, 3.2), (.06, 3.32), (.03, 3.4), (0, 3.42)], (x, y, 0), seg=8, mat=GOLD))
    for dy in (-1, 1):
        cy = y + dy * 1.0
        parts.append(lathe(col, "Scale pan", [(0, 2.47), (.38, 2.47), (.4, 2.53), (.36, 2.53), (0, 2.5)], (x, cy, 0), seg=16, mat=GOLD))
        for k in range(3):
            a = 2 * math.pi * k / 3
            parts.append(beam(col, "Scale chain", (x, cy, 3.1), (x + .32 * math.cos(a), cy + .32 * math.sin(a), 2.53), w=.02, h=.02, mat=IRON, segments=1))
    return parts


def stalls(col, side):
    parts = []
    front, back = side * STALL_FRONT, side * SORT_HALF
    x0, x1 = min(front, back), max(front, back)
    y = STALL_Y0
    while y < STALL_Y1 - 1e-3:
        parts.append(box(col, "Stall board", (x0 + .02, y + .01, 0), (x1 - .02, min(y + .3, STALL_Y1) - .01, .03), WALNUT))
        y += .3
    eave = front - side * .35
    bays = round((STALL_Y1 - STALL_Y0) / 4.6)
    step = (STALL_Y1 - STALL_Y0) / bays
    for i in range(bays):
        y0, y1 = STALL_Y0 + i * step, STALL_Y0 + (i + 1) * step
        parts += cloth_awning(col, back, eave, y0 + .1, y1 - .1)
        if y0 < CONVEYOR_Y < y1:
            parts += hatch(col, side, front, back, y0, y1)
        else:
            parts += load(col, side, front - side * .25, (y0 + y1) / 2, step, (i * 3 + (1 if side < 0 else 4)) % 5, 40 + i + (0 if side < 0 else 20))
        parts += bay_number(col, front - side * .05, y0 + .3, side, i + 1)
    # The stalls' counter on the goods' solid face (x +-23.5): upright boards 1.75 m high between a kick rail and a top
    # rail, so a pawn pushed against the stalls meets wood, not the air between the goods; open at the hatch, and none
    # where the timber store and the loft's end stand in front of the stalls.
    c0, c1 = sorted((front, front + side * .07))
    a, b = CONVEYOR_Y - HATCH_HALF - .3, CONVEYOR_Y + HATCH_HALF + .3
    for lo_y, hi_y in ((STALL_Y0, a), (b, STORE_Y0), (LOFT_Y1, STALL_Y1)):
        y = lo_y
        while y < hi_y - 1e-3:
            parts.append(box(col, "Counter board", (c0, y + .01, .12), (c1, min(y + .3, hi_y) - .01, 1.65), OAK, bevel=.01, segments=1))
            y += .3
        for z0, z1 in ((0, .14), (1.63, 1.75)):
            parts.append(box(col, "Counter rail", (c0, lo_y, z0), (c1, hi_y, z1), WALNUT, bevel=.015, segments=1))
    px = front + side * .14
    for i in range(bays + 1):
        py = STALL_Y0 + i * step
        parts += square_post(col, "Stall post", px, py, .2, 3.36, w=.24, mat=WALNUT)
        parts.append(box(col, "Post footing", (px - .19, py - .19, 0), (px + .19, py + .19, .2), STONE, bevel=.03))
        for d in (-1, 1):
            if (i == 0 and d < 0) or (i == bays and d > 0):
                continue
            parts.append(beam(col, "Knee brace", (px, py + d * .1, 2.75), (px, py + d * .7, 3.34), w=.1, h=.1, mat=WALNUT))
    parts.append(box(col, "Eave plate", (px - .13, STALL_Y0 - .15, 3.34), (px + .13, STALL_Y1 + .15, 3.54), WALNUT, bevel=.025))
    parts += scale(col, back - side * .8, (STALL_Y0 + STALL_Y1) / 2)
    return parts


# ------------------------------------------------------------------ the timber stores, the loft, the berths, the lamps


def timber_store(col, side):
    """Squared timber stacked along one wall beside the crane field: layers lying along the wall (their joints
    staggered) between layers laid across it (their ends to the yard), iron straps, a gold trim on the front edge."""
    front, back = side * STORE_FRONT, side * STALL_FRONT
    x0, x1 = min(front, back), max(front, back)
    lo_x, hi_x = sorted((front + side * .06, back))
    parts = [box(col, "Timber core", (lo_x, STORE_Y0 + .06, 0), (hi_x, STORE_Y1 - .06, 2.3), WALNUT)]
    h = .4
    for k in range(6):
        z = k * h
        if k % 2 == 0:
            # Along the wall: the front row and the back row, cut in lengths with staggered joints.
            for fx in (front, back - side * .42):
                y = STORE_Y0 - (.0 if k % 4 == 0 else 1.32)
                while y < STORE_Y1 - 1e-3:
                    a, b = max(y, STORE_Y0), min(y + 2.64, STORE_Y1)
                    if b - a > .2:
                        lo_x, hi_x = sorted((fx, fx + side * .42))
                        parts.append(box(col, "Stored beam", (lo_x, a + .01, z + .01), (hi_x, b - .01, z + h - .01), OAK if k % 4 == 0 else WALNUT, bevel=.035, segments=1))
                    y += 2.64
            parts.append(box(col, "Stored beam", (x0 + .42, STORE_Y0 + .02, z + .02), (x1 - .42, STORE_Y1 - .02, z + h - .02), OAK))
        else:
            # Across: their ends to the yard.
            y = STORE_Y0
            while y < STORE_Y1 - .3:
                parts.append(box(col, "Stored beam end", (x0 + .01, y + .01, z + .01), (x1 - .01, y + .41, z + h - .01), OAK if (int(y * 3) + k) % 3 else WALNUT, bevel=.04, segments=1))
                y += .42
    y = STORE_Y0 + 1.5
    while y < STORE_Y1:
        parts.append(box(col, "Store strap", (front - .03, y - .05, 0), (front + .03, y + .05, 2.44), IRON, bevel=.01, segments=1))
        parts.append(box(col, "Store strap", (x0, y - .05, 2.4), (x1, y + .05, 2.44), IRON, bevel=.01, segments=1))
        y += 3.5
    parts.append(box(col, "Store trim", (front - .05, STORE_Y0, 2.37), (front + .05, STORE_Y1, 2.43), GOLD, bevel=.01, segments=1))
    # Chocks on the top where the cranes' outer rests lay their loads.
    for y in (STORE_Y0 + 3.2, STORE_Y1 - 3.2):
        cx = (x0 + x1) / 2
        parts.append(box(col, "Chock", (cx - 1.2, y - .3, 2.4), (cx + 1.2, y + .3, 2.52), WALNUT, bevel=.02))
    return parts


def board_face(col, name, x0, x1, y, z0, z1, side_y, pitch=.24):
    """Upright boards across x0..x1 on a face at y (its front towards side_y), between z0 and z1."""
    parts = []
    x = x0
    while x < x1 - 1e-3:
        b = min(x + pitch, x1)
        lo = (x + .01, min(y, y + side_y * .05), z0)
        hi = (b - .01, max(y, y + side_y * .05), z1)
        parts.append(box(col, name, lo, hi, OAK, bevel=.012, segments=1))
        x += pitch
    return parts


def loft(col):
    """The loft across the yard: a timber-framed deck 2.4 m up over |x| <= 12 and its ends 4.4 m up to the stalls,
    boarded on both faces, crates stacked on the ends."""
    parts = []
    # The deck's planks (its gold lip and edge are the code's).
    y = LOFT_Y0 + .1
    while y < LOFT_Y1 - .1:
        parts.append(box(col, "Loft plank", (-LOFT_HALF + .02, y + .015, LOFT_TOP - .06), (LOFT_HALF - .02, min(y + .4, LOFT_Y1 - .1) - .015, LOFT_TOP), OAK, bevel=.01, segments=1))
        y += .4
    parts.append(box(col, "Loft core", (-LOFT_HALF, LOFT_Y0 + .12, 0), (LOFT_HALF, LOFT_Y1 - .12, LOFT_TOP - .06), WALNUT))
    for fy, sy in ((LOFT_Y0, -1), (LOFT_Y1, 1)):
        inner = fy - sy * .12
        parts += board_face(col, "Loft board", -LOFT_HALF, LOFT_HALF, inner, .1, LOFT_TOP - .3, sy)
        parts.append(box(col, "Loft beam", (-LOFT_HALF, min(fy, inner), LOFT_TOP - .3), (LOFT_HALF, max(fy, inner), LOFT_TOP - .06), WALNUT, bevel=.025))
        parts.append(box(col, "Loft sill", (-LOFT_HALF, min(fy, inner), 0), (LOFT_HALF, max(fy, inner), .1), WALNUT, bevel=.02))
        x = -LOFT_HALF
        while x <= LOFT_HALF + 1e-3:
            parts += square_post(col, "Loft post", x, fy - sy * .13, 0, LOFT_TOP - .06, w=.26, mat=WALNUT, bands=(.5,))
            x += 2.4
    for s in (-1, 1):
        a, b = sorted((s * LOFT_HALF, s * STALL_FRONT))
        parts.append(box(col, "Loft end core", (a + .12, LOFT_Y0 + .12, 0), (b, LOFT_Y1 - .12, LOFT_END_TOP - .06), WALNUT))
        y = LOFT_Y0
        while y < LOFT_Y1 - 1e-3:
            parts.append(box(col, "Loft end plank", (a, y + .015, LOFT_END_TOP - .06), (b, min(y + .4, LOFT_Y1) - .015, LOFT_END_TOP), OAK, bevel=.01, segments=1))
            y += .4
        for fy, sy in ((LOFT_Y0, -1), (LOFT_Y1, 1)):
            inner = fy - sy * .12
            parts += board_face(col, "Loft end board", a, b, inner, .1, LOFT_END_TOP - .3, sy)
            parts.append(box(col, "Loft end beam", (a, min(fy, inner), LOFT_END_TOP - .3), (b, max(fy, inner), LOFT_END_TOP - .06), WALNUT, bevel=.025))
            parts.append(box(col, "Loft end sill", (a, min(fy, inner), 0), (b, max(fy, inner), .1), WALNUT, bevel=.02))
            for x in (a + .13, (a + b) / 2, b - .13):
                parts += square_post(col, "Loft end post", x, fy - sy * .13, 0, LOFT_END_TOP - .06, w=.26, mat=WALNUT, bands=(.5, 2.4))
        # The end's face over the deck, boarded too; a gold trim along the end's south top.
        inner_x = s * LOFT_HALF
        for k in range(int((LOFT_Y1 - LOFT_Y0) / .24)):
            yy = LOFT_Y0 + k * .24
            lo_x, hi_x = sorted((inner_x, inner_x - s * .05))
            parts.append(box(col, "Loft end board", (lo_x, yy + .01, LOFT_TOP), (hi_x, yy + .23, LOFT_END_TOP - .06), OAK, bevel=.012, segments=1))
        parts.append(box(col, "Loft end trim", (a, LOFT_Y0 - .06, LOFT_END_TOP - .1), (b, LOFT_Y0, LOFT_END_TOP), GOLD, bevel=.01, segments=1))
        # Two-high crate stacks on the end (the code's StackPicture: 2 x 1.6 x 2.6, from 1.1 in, every 2.3 m).
        mid = (LOFT_Y0 + LOFT_Y1) / 2
        x = a + 1.1
        i = 0
        while x < b - 1.0:
            parts += crate(col, (x, mid, LOFT_END_TOP), (2.0, 2.6, .78), dark=(i % 3 == 0))
            parts += crate(col, (x, mid, LOFT_END_TOP + .8), (1.9, 2.5, .76), dark=(i % 3 == 2))
            x += 2.3
            i += 1
    return parts


def berth(col, x):
    """A berth's place on the floor: worn boards a little bigger than the chest, iron plates at the corners."""
    parts = []
    hx, hy = 1.95, 1.55
    y = BERTH_Y - hy
    k = 0
    while y < BERTH_Y + hy - 1e-3:
        parts.append(box(col, "Berth board", (x - hx + .01, y + .01, -.02), (x + hx - .01, min(y + .31, BERTH_Y + hy) - .01, .012 + (.004 if k % 2 else 0)), WALNUT, bevel=.008, segments=1))
        y += .31
        k += 1
    for a in (-1, 1):
        for b in (-1, 1):
            parts.append(box(col, "Berth corner", (x + a * hx - .28, BERTH_Y + b * hy - .28, .0), (x + a * hx + .28, BERTH_Y + b * hy + .28, .024), IRON, bevel=.01, segments=1))
    return parts


def lamp_post(col, x, y, z0=0.0):
    """An iron lamp post round the code's glowing lamp (0.4 x 0.5 x 0.4 at 3.35 over the post's foot at z0): a stepped
    base, the shaft with two rings, a lantern frame and its roof. (z0, R680: the platform's two stand on its top.)"""
    # The base within 7 cm of the post's collider (0.09) where a pawn's feet meet it.
    parts = [lathe(col, "Lamp base", [(0, 0), (.16, 0), (.16, .1), (.13, .18), (.11, .42), (.085, .55), (0, .55)], (x, y, z0), seg=12, mat=IRON)]
    parts.append(cylinder(col, "Lamp post", (x, y, z0 + 1.85), (0, 0, 1), .08, 2.6, IRON, segments=10))
    for z in (1.0, 2.9):
        parts.append(lathe(col, "Lamp ring", [(0, z), (.11, z), (.11, z + .06), (0, z + .06)], (x, y, z0), seg=10, mat=GOLD))
    parts.append(box(col, "Lantern base", (x - .25, y - .25, z0 + 3.05), (x + .25, y + .25, z0 + 3.11), IRON, bevel=.01, segments=1))
    for ex in (-1, 1):
        for ey in (-1, 1):
            parts.append(box(col, "Lantern bar", (x + ex * .225 - .02, y + ey * .225 - .02, z0 + 3.11),
                             (x + ex * .225 + .02, y + ey * .225 + .02, z0 + 3.6), IRON))
    parts.append(lathe(col, "Lantern roof", [(0, 3.6), (.34, 3.6), (.12, 3.86), (.06, 3.9), (.08, 3.96), (0, 4.02)], (x, y, z0), seg=4, mat=IRON))
    return parts


# ------------------------------------------------------------------ the basin, the lifting wheel's piers and landing


def basin(col):
    parts = []
    # The basin's near wall under the yard's edge (its gold edge is the code's), the bed under the water.
    parts.append(ashlar(col, "Basin wall", (-SORT_HALF, POOL_Y0 - .5, -3.0), (SORT_HALF, POOL_Y0, -.12), "+y", course=.5, below=-1.0))
    parts.append(box(col, "Basin coping", (-SORT_HALF, POOL_Y0 - .55, -.12), (SORT_HALF, POOL_Y0 - .25, 0), STONE, bevel=.02))
    parts.append(flagstones(col, "Basin bed", -SORT_HALF, SORT_HALF, POOL_Y0, CLIFF_Y, -3.0, size=2.5))
    # The wheel's piers, their bearings and the axle (the wheel itself is the code's VerticalWaterwheel).
    for s in (-1, 1):
        x = s * 2.5
        parts.append(ashlar(col, "Wheel pier", (x - .5, LIFT_Y - .7, -3.0), (x + .5, LIFT_Y + .7, 3.95), "+y", course=.45, length=.9, below=WATER))
        outer = x + s * .5
        lo_x, hi_x = sorted((x - s * .5, outer + s * .08))
        parts.append(box(col, "Pier cap", (lo_x, LIFT_Y - .78, 3.95), (hi_x, LIFT_Y + .78, 4.3), STONE, bevel=.05))
        parts.append(box(col, "Bearing block", (x - .5, LIFT_Y - .42, 4.3), (x + .5, LIFT_Y + .42, 4.5), IRON, bevel=.03))
        parts.append(cylinder(col, "Bearing", (x, LIFT_Y, LIFT_AXLE), (1, 0, 0), .38, 1.0, IRON, segments=16, bevel=.02))
        parts.append(box(col, "Bearing strap", (x - .3, LIFT_Y - .4, 4.82), (x + .3, LIFT_Y + .4, 4.94), IRON, bevel=.02))
        for dy in (-.32, .32):
            parts.append(cylinder(col, "Bearing bolt", (x, LIFT_Y + dy, 4.96), (0, 0, 1), .05, .1, BRASS, segments=8))
        parts.append(cylinder(col, "Axle collar", (s * 1.85, LIFT_Y, LIFT_AXLE), (1, 0, 0), .33, .14, BRASS, segments=16))
    parts.append(cylinder(col, "Axle", (0, LIFT_Y, LIFT_AXLE), (1, 0, 0), .25, 6.0, IRON, segments=16))
    # The landing the wheel lets its riders off onto: planks on joists (its gold trim is the code's).
    y0, y1 = LANDING_Y, CLIFF_Y
    for x in (-3.3, 0.0, 3.3):
        parts.append(box(col, "Landing joist", (x - .1, y0 + .05, TOP - .3), (x + .1, y1, TOP - .08), WALNUT, bevel=.02))
    parts.append(box(col, "Landing subfloor", (-3.48, y0 + .02, TOP - .11), (3.48, y1, TOP - .08), WALNUT))
    x = -3.5
    while x < 3.5 - 1e-3:
        parts.append(box(col, "Landing plank", (x + .015, y0, TOP - .08), (min(x + .35, 3.5) - .015, y1, TOP), OAK, bevel=.01, segments=1))
        x += .35
    for x in (-3.4, 3.4):
        parts.append(beam(col, "Landing brace", (x, y1 - .05, 5.4), (x, y0 + .25, TOP - .3), w=.16, h=.16, mat=WALNUT))
    return parts


def banner(col, x, y, top):
    """A royal banner hung on the cliff face (y its face, the cloth to the south): a gold rod with finials, the cloth
    with a swallowtail foot, its gold stripe and crown."""
    parts = [cylinder(col, "Banner rod", (x, y - .1, top + .1), (1, 0, 0), .04, 1.7, GOLD, segments=8)]
    for d in (-1, 1):
        parts.append(lathe(col, "Rod finial", [(0, 0), (.07, 0), (.08, .06), (.04, .14), (0, .18)], (x + d * .85, y - .1, top + .1), axis="x", seg=8, mat=GOLD))
    outline = [(x - .7, top), (x + .7, top), (x + .7, top - 2.6), (x, top - 2.15), (x - .7, top - 2.6)]
    parts.append(prism(col, "Banner cloth", outline, y - .09, y - .04, CLOTH))
    parts.append(prism(col, "Banner stripe", [(x - .12, top), (x + .12, top), (x + .12, top - 2.25), (x - .12, top - 2.25)], y - .1, y - .085, GOLD))
    parts += crown(col, (x, y - .11, top - .9), .5)
    return parts


def gear(col, centre, r, thick, teeth=16):
    """The sluice gate's winch wheel standing in the cliff's plane (axis along y): a toothed rim, six spokes, the hub."""
    cx, cy, cz = centre
    rim = arc_ring(col, "Gear rim", 0, 0, r - .16, r - .04, -thick / 2, thick / 2, WALNUT, seg=32)
    rim.rotation_euler = (math.radians(90), 0, 0)
    rim.location = (cx, cy, cz)
    parts = [rim]
    for k in range(teeth):
        a = 2 * math.pi * k / teeth
        d = Vector((math.cos(a), 0, math.sin(a)))
        p = Vector((cx, cy, cz)) + d * (r - .02)
        parts.append(beam(col, "Gear tooth", p - d * .06, p + d * .06, w=.12, h=thick, mat=WALNUT, segments=1))
    for k in range(6):
        a = 2 * math.pi * k / 6
        parts.append(beam(col, "Gear spoke", (cx, cy, cz), (cx + (r - .14) * math.cos(a), cy, cz + (r - .14) * math.sin(a)), w=.09, h=thick * .8, mat=WALNUT, segments=1))
    parts.append(cylinder(col, "Gear hub", (cx, cy - .05, cz), (0, 1, 0), .22, thick + .1, GOLD, segments=14))
    return parts


def basin_dock(col, side):
    parts = []
    cx = side * 15.5
    # The sluice arch: stone jambs, lintel and gold keystone round the code's dark tunnel; the raised gate in it.
    for j in (-2.6, 2.6):
        parts.append(ashlar(col, "Sluice jamb", (cx + j - .45, CLIFF_Y - .45, WATER), (cx + j + .45, CLIFF_Y - .02, 4.6), "-y", course=.5, length=.9))
    parts.append(box(col, "Sluice lintel", (cx - 3.1, CLIFF_Y - .5, 4.6), (cx + 3.1, CLIFF_Y - .02, 5.6), STONE, bevel=.06))
    parts.append(prism(col, "Sluice keystone", [(cx - .35, 5.5), (cx + .35, 5.5), (cx + .25, 4.7), (cx - .25, 4.7)], CLIFF_Y - .55, CLIFF_Y - .45, GOLD))
    y0, y1 = CLIFF_Y - .3, CLIFF_Y - .2
    x = cx - 2.1
    while x < cx + 2.1 - 1e-3:
        parts.append(box(col, "Sluice gate board", (x + .01, y0, 3.42), (min(x + .3, cx + 2.1) - .01, y1, 4.55), WALNUT, bevel=.012, segments=1))
        x += .3
    for gx in (-1.8, -1.2, -.6, 0, .6, 1.2, 1.8):
        parts.append(box(col, "Gate bar", (cx + gx - .05, y0 - .04, 3.3), (cx + gx + .05, y0, 4.55), IRON, bevel=.01, segments=1))
    for gz in (3.5, 4.4):
        parts.append(box(col, "Gate rail", (cx - 2.1, y0 - .06, gz - .05), (cx + 2.1, y0 - .02, gz + .05), IRON, bevel=.01, segments=1))
    parts += gear(col, (cx + side * 3.4, CLIFF_Y - .3, 3.2), .8, .14)
    # The barge moored 1.4 m off the cliff, its length along it.
    y0, y1, deck = LANDING_Y - 3.2, LANDING_Y, .15
    bx0, bx1, mid = cx - 4.6, cx + 4.6, LANDING_Y - 1.6
    hull = [(y0 + .25, WATER - .5), (y1 - .25, WATER - .5), (y1, deck - .2), (y1, deck), (y0, deck), (y0, deck - .2)]
    parts.append(prism(col, "Barge hull", hull, bx0 + .5, bx1 - .5, WALNUT, axis="x"))
    for end, d in ((bx0, 1), (bx1, -1)):
        a = end + d * .5
        tip = [(y0 + .4, WATER - .2), (y1 - .4, WATER - .2), (y1 - .2, deck), (y0 + .2, deck)]
        lo_x, hi_x = sorted((a, end))
        parts.append(prism(col, "Barge end", tip, lo_x, hi_x, WALNUT, axis="x"))
    for k in range(3):
        z = WATER - .35 + k * .22
        for yy, dy in ((y0, -1), (y1, 1)):
            lo_y, hi_y = sorted((yy - dy * .02, yy + dy * .025))
            parts.append(box(col, "Barge strake", (bx0 + .5, lo_y, z), (bx1 - .5, hi_y, z + .05), OAK, bevel=.01, segments=1))
    parts.append(box(col, "Barge deck", (bx0 + .3, y0 + .1, deck - .04), (bx1 - .3, y1 - .1, deck), OAK))
    for yy in (y0, y1 - .12):
        parts.append(box(col, "Barge rail", (bx0 + .4, yy, deck), (bx1 - .4, yy + .12, deck + .3), OAK, bevel=.02))
        for k in range(7):
            px = bx0 + .5 + k * (bx1 - bx0 - 1.0) / 6
            parts.append(box(col, "Rail post", (px - .05, yy, deck + .3), (px + .05, yy + .12, deck + .42), WALNUT, bevel=.01, segments=1))
    parts.append(box(col, "Barge trim", (bx0 + .4, y0 - .02, deck + .3), (bx1 - .4, y0 + .02, deck + .36), GOLD, bevel=.008, segments=1))
    for i in range(3):
        lx = cx - side * (2.6 - i * 1.5)
        parts += crate(col, (lx, mid, deck), (1.2, 1.2, 1.2), dark=(i == 1))
    parts += crate(col, (cx - side * 2.1, mid, deck + 1.2), (.9, .9, .9))
    for dy in (-.8, .8):
        parts += barrel(col, (cx + side * 1.9, mid + dy, deck), d=.8, h=1.1)
    parts += covered_load(col, (cx + side * 3.1 - .7, mid - .9, deck), (cx + side * 3.1 + .7, mid + .9, deck + 1.3), 7 + side)
    # Mooring ropes to rings on the cliff.
    for rx in (bx0 + 1.0, bx1 - 1.0):
        parts.append(beam(col, "Mooring rope", (rx, y1 - .05, deck + .25), (rx, CLIFF_Y - .1, deck + .32), w=.07, h=.07, mat=LEATHER, segments=1))
        ring = arc_ring(col, "Mooring ring", 0, 0, .1, .15, -.025, .025, IRON, seg=16, bevel=0)
        ring.rotation_euler = (math.radians(90), 0, 0)
        ring.location = (rx, CLIFF_Y - .06, deck + .25)
        parts.append(ring)
    # The jib crane on the platform over the barge, a net of sacks hanging from it.
    jx = cx + side * 2.2
    parts += square_post(col, "Jib post", jx, CLIFF_Y + 1.4, TOP, TOP + 4.8, w=.5, mat=WALNUT, bands=(TOP + .6, TOP + 4.2))
    parts.append(box(col, "Jib post foot", (jx - .3, CLIFF_Y + 1.1, TOP), (jx + .3, CLIFF_Y + 1.7, TOP + .2), STONE, bevel=.03))
    parts.append(beam(col, "Jib arm", (jx, CLIFF_Y + 1.6, TOP + 4.6), (jx, CLIFF_Y - 4.8, TOP + 4.6), w=.36, h=.36, mat=WALNUT))
    parts.append(beam(col, "Jib brace", (jx, CLIFF_Y + 1.25, TOP + 2.3), (jx, CLIFF_Y - 1.45, TOP + 4.45), w=.22, h=.22, mat=WALNUT))
    parts.append(box(col, "Jib band", (jx - .2, CLIFF_Y - 4.7, TOP + 4.4), (jx + .2, CLIFF_Y - 4.5, TOP + 4.8), GOLD, bevel=.02))
    parts.append(lathe(col, "Jib pulley", [(0, -.06), (.16, -.06), (.12, 0), (.16, .06), (0, .06)], (jx, CLIFF_Y - 4.7, TOP + 4.3), axis="x", seg=12, mat=IRON))
    parts.append(beam(col, "Jib rope", (jx, CLIFF_Y - 4.7, TOP + 4.25), (jx, CLIFF_Y - 4.7, deck + 3.25), w=.05, h=.05, mat=LEATHER, segments=1))
    parts.append(lathe(col, "Jib hook", [(0, 3.15), (.08, 3.15), (.1, 3.25), (.05, 3.33), (0, 3.35)], (jx, CLIFF_Y - 4.7, deck), seg=8, mat=IRON))
    parts.append(lumpy(col, "Sack net", (jx, CLIFF_Y - 4.7, deck + 2.15), (.62, .62, .55), SACK, 31 + side))
    for k in range(4):
        a = math.pi * k / 4
        parts.append(beam(col, "Net cord", (jx + .66 * math.cos(a), CLIFF_Y - 4.7 + .66 * math.sin(a), deck + 2.5), (jx, CLIFF_Y - 4.7, deck + 3.15), w=.03, h=.03, mat=LEATHER, segments=1))
    for bx in (cx - 4.4, cx + 4.4):
        parts += banner(col, bx, CLIFF_Y - .02, 7.7)
    return parts


def build_sorting():
    col = collection("RCY Sorting")
    clear(col)
    pieces = yard_and_walls(col)
    for side in (-1, 1):
        pieces += stalls(col, side)
        pieces += timber_store(col, side)
        pieces += basin_dock(col, side)
    pieces += loft(col)
    for x in BERTH_XS:
        pieces += berth(col, x)
    for x, y in ((-11.2, 82.8), (11.2, 82.8), (-8.5, 131.95), (8.5, 131.95), (-21.5, 131.95), (21.5, 131.95)):
        pieces += lamp_post(col, x, y)
    pieces += basin(col)
    return join(col, "Sorting", pieces)


if globals().get("RCY_BUILD", True):
    sorting = build_sorting()
    print("sorting tris", tris(bpy.data.collections["RCY Sorting"]))
