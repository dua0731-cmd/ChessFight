# CR02 · the carriage-yard corridor (2026-11-22): leaves the Royal Carriage Yard for the first gate's mini-game.
# Concept: Docs/PawnRush/ConceptArt/SectionCorridors/02_carriageyard.png, ornaments AssetRefs/CR02_assets_carriageyard.png
# (all modelled here). Materials: the yard's own RCY_ set (Assets/Resources/PawnRushCarriageYard/Materials).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr02_carriageyard.py -- [--export] [--render]
#
# Ends as CR01 (yard exit at y 0 with the gate frame, the mini-game entry at 10, walls stop at 9.2, open sky).
# A rampart passage. West wall, inner face x -6.30, outer face x -7.0, wall top 4.5, battlements to 5.2:
#   piers are round rook towers (centre x -6.64, shaft r 0.56) on a riveted bronze ring, a corbelled crenellated top
#   bays: big ashlar, a small arched barred window (sill 1.6), an oak beam with iron straps at 3.3, an iron lantern
#   the west wall's bays carry a slate lean-to loft over the wall top with crates and sacks (BayWest), the east the
#   plain battlements (Bay). Nothing past x -6.0.
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bpy
import cr13_common as C
import cr13_ornaments as O
import cr13_details as D

ID = "CR02_CarriageYard"
HERE = os.path.dirname(os.path.abspath(__file__))
STONE, SHADE, FLAG, TIMBER = "RCY_Stone", "RCY_Shade", "RCY_Flag", "RCY_Timber"
IRON, BRONZE, GOLD, OAK, SACK, SLATE, ROPE = "RCY_Iron", "RCY_Brass", "RCY_Gold", "RCY_Oak", "RCY_Sack", "RCY_Tile", "RCY_Rope"
PALE = "GC_cream_stone_pale"
FACE, OUTER, TOP = -6.30, -7.0, 4.5
TX, TR = -6.64, .56            # tower centre x, shaft radius
SPAN = C.AUTHORED_SPAN
HB = SPAN / 2 + .05


def ashlar(m, x0, x1, y0, y1, z0, z1, course=.52, block=1.05, phase=0, skip=None, bevel=.03):
    k, z = 0, z0
    while z < z1 - .05:
        zt = min(z1, z + course)
        shift = block / 2 if (k + phase) % 2 else 0
        cuts = [y0]
        yy = y0 - shift + block
        while yy < y1 - .25:
            if yy > y0 + .25:
                cuts.append(yy)
            yy += block
        cuts.append(y1)
        gap = skip(z, zt) if skip else None
        for a, b in zip(cuts[:-1], cuts[1:]):
            parts = [(a, b)]
            if gap:
                parts = [(a, min(b, gap[0]))] if a < gap[0] else []
                if b > gap[1]:
                    parts.append((max(a, gap[1]), b))
            for pa, pb in parts:
                if pb - pa > .08:
                    m.box(x0, x1, pa + .014, pb - .014, z + .014, zt - .014, STONE, bevel=bevel)
        z, k = zt, k + 1


def lantern(m, x_face, y, z_mount):
    """An iron wall lantern: a scroll bracket from the wall, a hexagonal cage with clear panes and an empty cup."""
    # bracket: back plate, arm, scroll
    m.box(x_face, x_face + .04, y - .07, y + .07, z_mount - .28, z_mount + .12, IRON, bevel=.012)
    arm = [(x_face + .03, y, z_mount), (x_face + .12, y, z_mount + .03), (x_face + .18, y, z_mount - .02)]
    m.tube(arm, .022, IRON, segs=6)
    scroll = []
    for i in range(12):
        t = i / 11
        a = math.pi * (1.0 + 1.6 * t)
        r = .06 * (1 - .6 * t)
        scroll.append((x_face + .09 + r * math.cos(a), y, z_mount - .1 + r * math.sin(a)))
    m.tube(scroll, .016, IRON, segs=6)
    cx, cz = x_face + .18, z_mount - .1   # the cage stays behind the bodies' face (x -6): it hangs at head height
    m.tube([(cx, y, z_mount - .02), (cx, y, cz)], .012, IRON, segs=6, cap=True)
    # cage: roof, frame posts, base; the panes are left open (code may light the empty cup)
    m.lathe([(.001, .3), (.11, .2), (.1, .17), (.001, .17)], (cx, y, cz - .25), IRON, segs=6)
    m.lathe([(.1, -.08), (.1, -.04), (.085, -.04), (.085, -.08)], (cx, y, cz - .25), IRON, segs=6, cap_top=False)
    for k in range(6):
        a = math.pi / 6 + k * math.pi / 3
        px, py = cx + .088 * math.cos(a), y + .088 * math.sin(a)
        m.box(px - .012, px + .012, py - .012, py + .012, cz - .33, cz - .08, IRON, bevel=0)
    m.lathe([(.035, 0), (.035, .05), (.028, .05)], (cx, y, cz - .32), IRON, segs=8, cap_top=False)
    return (cx, y, cz - .22)


def crate(m, x, y, z, s=.5, turn=0.):
    m.box(x - s / 2, x + s / 2, y - s / 2, y + s / 2, z, z + s, OAK, bevel=.02)
    for dz in (.04, s - .04):
        m.box(x - s / 2 - .006, x + s / 2 + .006, y - s / 2 - .006, y + s / 2 + .006, z + dz - .03, z + dz + .03, TIMBER, bevel=.008)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box(x + sx * s / 2 - .07, x + sx * s / 2 + .008 * sx, y + sy * s / 2 - .07 * sy, y + sy * s / 2 + .008 * sy,
                  z + .003, z + .1, IRON, bevel=.006)
            m.box(x + sx * s / 2 - .07, x + sx * s / 2 + .008 * sx, y + sy * s / 2 - .07 * sy, y + sy * s / 2 + .008 * sy,
                  z + s - .1, z + s + .003, IRON, bevel=.006)


def sack(m, x, y, z, h=.55, seed=1):
    m.blob((x, y, z + h * .42), h * .5, SACK, subdiv=2, squash=(.8, .8, 1.0), noise=.05, seed=seed)
    m.lathe([(.08 * h / .55, h * .78), (.06 * h / .55, h * .86), (.09 * h / .55, h * .98), (.001, h)], (x, y, z), SACK, segs=8)
    m.lathe([(.075 * h / .55, h * .8), (.075 * h / .55, h * .86)], (x, y, z), ROPE, segs=8, cap_bottom=False, cap_top=False)


def build_bay(coll, loft):
    m = C.Mesh(ID + ("_BayWest" if loft else "_Bay"))
    m.box(FACE - .3, OUTER + .03, -HB, HB, 0, TOP, SHADE, bevel=0)            # the core behind the joints
    win_a, sill, spring = .32, 1.6, 2.32
    def gap(z0, z1):
        return (-win_a - .12, win_a + .12) if z1 > sill - .14 and z0 < spring + win_a + .16 else None
    ashlar(m, FACE, FACE - .34, -HB, HB, 0, 3.25, skip=gap)
    ashlar(m, FACE, FACE - .34, -HB, HB, 3.6, TOP - .05, phase=1)
    # the window: a stone surround, iron bars, a dark reveal behind
    ring_in = [(win_a, sill)] + [(win_a * math.cos(math.pi * i / 10), spring + win_a * math.sin(math.pi * i / 10)) for i in range(11)] + [(-win_a, sill)]
    ring_out = [(win_a + .12, sill - .1)] + [((win_a + .12) * math.cos(math.pi * i / 10), spring + (win_a + .12) * math.sin(math.pi * i / 10)) for i in range(11)] + [(-win_a - .12, sill - .1)]
    for i in range(len(ring_in) - 1):
        m.prism([ring_in[i], ring_out[i], ring_out[i + 1], ring_in[i + 1]], "x", FACE + .03, FACE - .34, STONE, bevel=0)
    m.box(FACE + .05, FACE - .02, -win_a - .2, win_a + .2, sill - .16, sill - .02, STONE, bevel=.02)   # sill
    # the stones the window's courses leave: under the sill, and over the arch up to the beam
    m.box(FACE, FACE - .34, -win_a - .12 + .014, win_a + .12 - .014, 1.04 + .014, sill - .16, STONE, bevel=.03)
    for s in (-1, 1):
        arc = [p for p in ring_out[1:-1] if p[0] * s >= -1e-6]
        arc.sort(key=lambda p: -abs(p[0]))
        fill = [(s * (win_a + .12 - .014), spring), (s * (win_a + .12 - .014), 3.12 - .014), (0, 3.12 - .014)]   # up to the course over it, not into it + list(reversed(arc))
        m.prism(fill, "x", FACE, FACE - .34, STONE, bevel=0)
    m.prism(ring_in, "x", FACE - .285, FACE - .295, SHADE, bevel=0)                  # the dark inside, 5 mm before the core
    for yb in (-.16, 0, .16):
        m.box(FACE - .1, FACE - .14, yb - .018, yb + .018, sill, spring + math.sqrt(max(0, win_a ** 2 - yb ** 2)) - .02, IRON, bevel=.006)
    m.box(FACE - .095, FACE - .145, -win_a, win_a, sill + .45, sill + .49, IRON, bevel=.006)   # proud of the bars it crosses
    # the oak beam on the wall with iron straps and knees down to the towers
    m.box(FACE, FACE + .2, -HB, HB, 3.25, 3.6, TIMBER, bevel=.025)
    for yy in (-.6, 0, .6):
        m.box(FACE + .005, FACE + .215, yy - .05, yy + .05, 3.22, 3.63, IRON, bevel=.008)
        for zz in (3.3, 3.55):
            m.lathe([(.022, 0), (.016, .02), (.001, .025)], (FACE + .21, yy, zz), IRON, segs=6, axis="x")
    for s in (-1, 1):
        knee = [(s * (HB - .05), 2.45), (s * (HB - .05), 2.6), (s * (HB - .62), 3.25), (s * (HB - .45), 3.25)]
        m.prism(knee, "x", FACE + .02, FACE + .16, TIMBER, bevel=.012)
    # a flower box under the window and ivy climbing the wall beside it
    D.window_box(m, OAK, IRON, "GC_planter_soil", "GC_leaf", ["GC_flower_white", "GC_flower_pink", "GC_flower_yellow"],
                 FACE + .005, 0, sill - .44, w=.86, seed=7 if loft else 9)
    ivy_y = -.78
    D.ivy(m, "GC_leaf_dark", "GC_leaf_dark", [(FACE + .012, ivy_y + .05 * math.sin(i), .25 + i * .3) for i in range(10)],
          seed=11 if loft else 13, every=.05, size=1.3, facing=(1, 0, 0))
    # the lantern by the window
    light = lantern(m, FACE + .02, .62, 2.95)
    # wall top: a walk, coping, battlements on the corridor side (east wall) or the loft (west wall)
    m.box(FACE + .04, OUTER - .04, -HB, HB, TOP - .05, TOP + .1, STONE, bevel=.025)
    if not loft:
        for i, yy in enumerate((-.62, .62)):
            m.box(FACE + .02, FACE - .42, yy - .3, yy + .3, TOP + .103, TOP + .78, STONE, bevel=.03)
        m.box(OUTER - .02, OUTER + .38, -HB, HB, TOP + .101, TOP + .55, STONE, bevel=.03)       # the outer parapet
    else:
        # the lean-to loft: posts on the outer parapet, a slate roof sloping down toward the way, cargo underneath
        m.box(OUTER - .02, OUTER + .3, -HB, HB, TOP + .101, TOP + 1.55, STONE, bevel=.03)
        for yy in (-.8, .8):
            m.box(FACE + .02, FACE - .14, yy - .07, yy + .07, TOP + .1, 5.95, TIMBER, bevel=.015)
        m.box(FACE + .03, FACE - .16, -HB, HB, 5.95, 6.1, TIMBER, bevel=.015)                 # front plate
        roof = [(-6.02, 5.92), (-7.25, 6.75), (-7.25, 6.88), (-6.02, 6.05)]
        m.prism([(x, z) for x, z in roof], "y", -HB - .02, HB + .02, SLATE, bevel=.012)
        for k in range(7):                                                                     # slate courses
            t = (k + .5) / 7
            x = -6.02 + (-7.25 + 6.02) * t
            z = 6.05 + (6.88 - 6.05) * t
            m.box(x - .09, x + .09, -HB - .01, HB + .01, z - .01, z + .015, SLATE, bevel=.004)
        for yy in (-.62, .62):
            m.box(FACE + .02, FACE - .18, yy - .06, yy + .06, 6.05, 6.75, TIMBER, bevel=.01)
        crate(m, -6.62, -.45, TOP + .104, .55)
        crate(m, -6.62, -.45, TOP + .657, .42)
        sack(m, -6.6, .2, TOP + .104, .58, seed=3)
        sack(m, -6.75, .55, TOP + .104, .5, seed=5)
        crate(m, -6.66, .62, TOP + .104, .4)
        D.rope_coil(m, ROPE, -6.4, .05, TOP + .104, r=.15)
        m.lathe([(.11, 0), (.13, .26), (.12, .27), (.1, .02)], (-6.42, -.05, TOP + .104), OAK, segs=12, cap_top=False)   # a bucket
        m.lathe([(.125, .08), (.13, .1)], (-6.42, -.05, TOP + .104), IRON, segs=12, cap_top=False, cap_bottom=False)
    # outer face
    ashlar(m, OUTER + .02, OUTER - .08, -HB, HB, 0, TOP - .05, phase=1, bevel=0)
    ob = m.finish(coll, (-6.0, 0, 0))
    C.empty(coll, "LIGHT_0", light, parent=ob)
    return ob


def build_pier(coll):
    """A round rook tower standing out of the wall."""
    m = C.Mesh(ID + "_Pier")
    m.lathe([(.001, 0), (.63, 0), (.63, .42), (.58, .45)], (TX, 0, 0), BRONZE, segs=20, cap_top=False)
    for k in range(12):                                                 # rivets round the ring
        a = 2 * math.pi * k / 12
        if math.cos(a) < -.2:
            continue
        m.blob((TX + .635 * math.cos(a), .635 * math.sin(a), .21), .035, BRONZE, subdiv=1)
    shaft = lambda a, h: 1.0
    m.lathe([(.56, .42), (.56, 4.25), (.6, 4.32), (.64, 4.42), (.64, 4.6)], (TX, 0, 0), STONE, segs=20, cap_top=False, cap_bottom=False)
    for zc in [.95 + .52 * i for i in range(7)]:                        # course lines on the shaft
        m.lathe([(.565, zc - .012), (.57, zc), (.565, zc + .012)], (TX, 0, 0), SHADE, segs=20, cap_top=False, cap_bottom=False)
    m.lathe([(.645, 4.6), (.645, 4.68), (.62, 4.7)], (TX, 0, 0), BRONZE, segs=20, cap_top=False, cap_bottom=False)
    m.lathe([(.62, 4.7), (.62, 5.32), (.001, 5.32)], (TX, 0, 0), STONE, segs=20, cap_bottom=False)
    for k in range(6):                                                  # merlons
        a = 2 * math.pi * (k + .5) / 6
        cx, cy = TX + .5 * math.cos(a), .5 * math.sin(a)
        rot = math.atan2(cy, cx - TX)
        before = set(m.bm.faces)
        m.box(-.12, .12, -.17, .17, 5.32, 5.82, STONE, bevel=.025)
        from mathutils import Matrix as Mx
        m.transform_new(before, Mx.Translation((cx, cy, 0)) @ Mx.Rotation(rot, 4, "Z"))
    # two arrow slits up the tower, one on each side of the banner
    for a, z in ((.55, 2.3), (-.55, 3.3)):
        cx, cy = TX + .57 * math.cos(a), .57 * math.sin(a)
        before = set(m.bm.faces)
        m.box(-.04, .03, -.045, .045, -.32, .32, SHADE, bevel=0)
        m.box(-.02, .05, -.11, .11, -.36, -.3, STONE, bevel=.01)
        m.transform_new(before, Mx.Translation((cx, cy, z)) @ Mx.Rotation(a, 4, "Z"))
    # the shield plaque on the tower's front, under the banner (code hangs the banner at BANNER)
    ob = m.finish(coll, (-6.0, 0, 0))
    C.empty(coll, "BANNER", (TX + .57, 0, 4.15), parent=ob)
    return ob


KNIGHT = [(-.28, 0), (.3, 0), (.3, .1), (.2, .14), (.2, .22), (.12, .26), (.1, .4), (.22, .56), (.26, .66), (.22, .74),
          (.06, .8), (-.02, .92), (-.1, .86), (-.16, .9), (-.2, .84), (-.3, .72), (-.4, .62), (-.44, .52), (-.38, .46),
          (-.24, .5), (-.14, .44), (-.18, .3), (-.2, .24), (-.16, .2), (-.22, .14), (-.28, .1)]
CROWN = [(-.3, 0), (.3, 0), (.34, .2), (.2, .12), (.12, .28), (0, .14), (-.12, .28), (-.2, .12), (-.34, .2)]


def shield(m, at_y, cx, cz, size, face_dir):
    """The knight shield plaque on a portal face (plane y = at_y, standing toward face_dir)."""
    outline = []
    for i in range(13):
        t = i / 12
        outline.append((.5 * size * (1 - t ** 2.2), cz - size * .62 * t ** 1.2))
    outline = [(cx + u, v) for u, v in [(.5 * size, cz + .5 * size)] + [(p[0], p[1]) for p in outline]]
    full = outline + [(cx - (u - cx), v) for u, v in reversed(outline)][1:-1]
    d0 = at_y
    m.prism(full, "y", d0, d0 + face_dir * .05, PALE, bevel=0)
    inner = [(cx + (u - cx) * .86, cz + (v - cz) * .86 + .02 * size) for u, v in full]
    m.prism(full, "y", d0, d0 + face_dir * .03, GOLD, bevel=0)
    m.prism(inner, "y", d0 + face_dir * .03, d0 + face_dir * .06, PALE, bevel=0)
    for u, v in KNIGHT:
        pass
    m.prism([(cx + u * size * .55, cz - .4 * size + v * size * .62) for u, v in KNIGHT], "y", d0 + face_dir * .06, d0 + face_dir * .1, GOLD, bevel=0)
    m.prism([(cx + u * size * .45, cz + .28 * size + v * size * .5) for u, v in CROWN], "y", d0 + face_dir * .06, d0 + face_dir * .1, GOLD, bevel=0)


def build_portal(coll):
    """The gate at the yard's exit (y 0 .. 0.95): two round towers at |x| 7.2, a segmental stone arch with an oak
    lintel frame and iron straps, a crenellated wall over it and the knight shield on both faces."""
    m = C.Mesh(ID + "_Portal")
    y0, y1, yc = 0.0, .95, .475
    from mathutils import Matrix as Mx
    for s in (-1, 1):
        tx = s * 7.18
        tower = set(m.bm.faces)
        m.lathe([(.001, 0), (1.12, 0), (1.12, .5), (1.06, .55)], (tx, yc, 0), BRONZE, segs=24, cap_top=False)
        m.lathe([(1.06, .5), (1.06, 6.2), (1.16, 6.35), (1.16, 6.55)], (tx, yc, 0), STONE, segs=24, cap_top=False, cap_bottom=False)
        for zc in [1.05 + .55 * i for i in range(10)]:
            m.lathe([(1.065, zc - .014), (1.072, zc), (1.065, zc + .014)], (tx, yc, 0), SHADE, segs=24, cap_top=False, cap_bottom=False)
        m.lathe([(1.165, 6.55), (1.165, 6.65), (1.12, 6.68)], (tx, yc, 0), BRONZE, segs=24, cap_top=False, cap_bottom=False)
        m.lathe([(1.12, 6.65), (1.12, 7.3), (.001, 7.3)], (tx, yc, 0), STONE, segs=24, cap_bottom=False)
        for k in range(8):
            a = 2 * math.pi * (k + .5) / 8
            before = set(m.bm.faces)
            m.box(-.16, .16, -.24, .24, 7.3, 7.95, STONE, bevel=.03)
            from mathutils import Matrix as Mx
            m.transform_new(before, Mx.Translation((tx + .92 * math.cos(a), yc + .92 * math.sin(a), 0)) @ Mx.Rotation(a, 4, "Z"))
        # flattened along the way so the tower stays within the frame's 0 .. 0.95 (the yard's exit band is behind y 0)
        m.transform_new(tower, Mx.Translation((tx, yc, 0)) @ Mx.Diagonal((1, .45, 1, 1)) @ Mx.Translation((-tx, -yc, 0)))
    # the arch: segmental, chord 12.1, rise 1.7, springing 4.4; voussoirs
    n, R0 = 13, None
    half, rise, spring = 6.05, 1.7, 4.4
    R = (half ** 2 + rise ** 2) / (2 * rise)
    cz = spring + rise - R
    a0 = math.asin(half / R)
    pin = [(R * math.sin(-a0 + 2 * a0 * i / n), cz + R * math.cos(-a0 + 2 * a0 * i / n)) for i in range(n + 1)]
    pout = [((R + .7) * math.sin(-a0 + 2 * a0 * i / n), cz + (R + .7) * math.cos(-a0 + 2 * a0 * i / n)) for i in range(n + 1)]
    for i in range(n):
        m.prism([pin[i], pout[i], pout[i + 1], pin[i + 1]], "y", y0 + .03, y1 - .03, STONE if i != n // 2 else PALE, bevel=.03)
    # the wall over the arch up to 7.0 and its battlements
    for s in (-1, 1):
        side = [p for p in pout if p[0] * s >= -1e-6]
        side.sort(key=lambda p: -abs(p[0]))
        pts = [(s * 6.3, spring), (s * 6.3, 7.0), (0, 7.0)] + list(reversed(side))
        m.prism(pts, "y", y0 + .06, y1 - .06, STONE, bevel=0)
        m.box(s * 6.0, s * 6.35, y0 + .02, y1 - .02, 0, spring, STONE, bevel=.03)      # the jambs under the springing
    m.box(-6.4, 6.4, y0 - .04, y1 + .04, 6.95, 7.15, STONE, bevel=.03)
    for i in range(9):
        x = -5.6 + 11.2 * i / 8
        m.box(x - .32, x + .32, y0 + .02, y1 - .02, 7.15, 7.85, STONE, bevel=.03)
    # oak frame under the arch's ends: posts and knee braces with iron straps
    for s in (-1, 1):
        for yy in (y0 - .02, y1 + .02):
            dy = -1 if yy < .5 else 1
            m.box(s * 6.0, s * 6.27, yy, yy + dy * .18, 0, 4.2, TIMBER, bevel=.03)   # posts outside the walk (|x| >= 6)
            m.prism([(s * 6.0, 3.3), (s * 6.0, 3.55), (s * 4.75, 4.62), (s * 4.45, 4.62)], "y", yy, yy + dy * .16, TIMBER, bevel=.02)
            for z in (.6, 2.0, 3.4):
                m.box(s * 5.99, s * 6.29, yy - dy * .01, yy + dy * .2, z, z + .1, IRON, bevel=.01)   # wraps the post: 1 cm proud
    for yy, f in ((y0 - .06, -1), (y1 + .06, 1)):
        shield(m, yy, 0, 5.85, 1.25, f)
    return m.finish(coll, (0, 0, 0))


def build_floor(coll):
    """Worn flag paving (RCY_Flag) 1.2 x 1.0 in running bond over a dark bed, two smoother cart tracks; top +0.024,
    rising from +0.004 over the first 0.6 m (the yard's exit floor is at 0)."""
    m = C.Mesh(ID + "_Floor")
    m.box(-6.3, 6.3, 0, 10, -.12, -.01, SHADE, bevel=0)
    def top(y):
        return .004 + .02 * min(1, y / .6)
    rows = 10
    for r in range(rows):
        ya, yb = r * 1.0, (r + 1) * 1.0
        shift = .6 if r % 2 else 0
        xs = [-6.3] + [x for x in [-6.3 + shift + 1.2 * k for k in range(1, 12)] if -6.2 < x < 6.2] + [6.3]
        for xa, xb in zip(xs[:-1], xs[1:]):
            mat = FLAG
            m.prism([(ya + .012, -.06), (yb - .012, -.06), (yb - .012, top(yb)), (ya + .012, top(ya))], "x",
                    xa + .012, xb - .012, mat, bevel=0)
    return m.finish(coll, (0, 0, 0))


def main():
    C.reset()
    coll = C.collection(ID)
    bay, bay_w, pier, portal, floor = build_bay(coll, False), build_bay(coll, True), build_pier(coll), build_portal(coll), build_floor(coll)
    for ob in (bay, bay_w, pier, portal, floor):
        print("%-26s %7d tris" % (ob.name, C.tris(ob)))
    frm, to = 0.0, 9.2
    ps = C.piers(frm, to)
    span = C.bay_span(frm, to)
    total = C.tris(floor) + C.tris(portal) + 8 * C.tris(pier) + 3 * (C.tris(bay) + C.tris(bay_w))
    print("corridor total %d tris" % total)
    if "--export" in C.ARGS:
        out = C.OUT_REL + "/" + ID + "/CR02_"
        C.export([bay] + [o for o in coll.objects if o.parent == bay], out + "Bay.fbx")
        C.export([bay_w] + [o for o in coll.objects if o.parent == bay_w], out + "BayWest.fbx")
        C.export([pier] + [o for o in coll.objects if o.parent == pier], out + "Pier.fbx")
        C.export([portal], out + "Portal.fbx")
        C.export([floor], out + "Floor.fbx")
        C.write_layout(ID, 4.5, frm, to, ["Bay", "BayWest", "Pier", "Portal", "Floor"], open_sky=True)
    if "--render" in C.ARGS:
        for ob in (bay, bay_w, pier, portal, floor):
            ob.hide_render = True
        for side in (-1, 1):
            for p in ps:
                C.place(pier, side * 6.0, p, side)
            for i in range(3):
                C.place(bay_w if side < 0 else bay, side * 6.0, (ps[i] + ps[i + 1]) / 2, side, span / C.AUTHORED_SPAN)
        C.place(portal, 0, 0)
        C.place(floor, 0, 0)
        ctx = C.Mesh("context")
        ctx.box(-14, 14, -14, 0, -.2, 0, FLAG, bevel=0)
        ctx.box(-14, 14, 10, 30, -.2, .02, "PR_Early_Paving", bevel=0)
        ctx.box(-14, -6.6, 10, 30, 0, 7, "PR_Early_WallCore", bevel=0)
        ctx.box(6.6, 14, 10, 30, 0, 7, "PR_Early_WallCore", bevel=0)
        ctx.finish(bpy.context.scene.collection)
        for x in (-1.2, 0, 1.2):
            C.pawn(bpy.context.scene.collection, x, -1.0, "GC_royal_blue" if x else "GC_pennant_red")
        C.render_setup()
        prev = os.path.join(HERE, "previews")
        os.makedirs(prev, exist_ok=True)
        C.shoot(os.path.join(prev, "cr02_concept_view.png"), (0, -4.2, 2.6), (0, 8, 2.2), 22)
        C.shoot(os.path.join(prev, "cr02_wall_close.png"), (-2.5, 2.2, 1.8), (6.2, 4.6, 2.4), 26)
        C.shoot(os.path.join(prev, "cr02_loft_close.png"), (2.5, 2.4, 3.2), (-6.4, 4.6, 4.6), 26)
        C.shoot(os.path.join(prev, "cr02_from_minigame.png"), (0, 15, 3.0), (0, 2, 3.4), 22)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "cr02_carriageyard.blend"))


main()
