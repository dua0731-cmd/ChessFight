# CR06 - the war room's approach (mini-game 2 -> the king's war room), after the GPT concept
# Docs/PawnRush/ConceptArt/SectionCorridors/06_warroom.png: warm sandstone ashlar, round-headed windows with iron
# tracery, a teal riveted iron band over a blind arcade, teal riveted tie beams on curved brackets under a ribbed
# sandstone vault, compass medallions on the ceiling and the floor, iron lanterns on the piers. Height 6.5. Starts
# 0.65 m after the mini-game's exit (Header), ends at the war room's entry (Portal) and the war room's wide south
# edge (EndWall, |x| 6.6..14.6).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr47_06_warroom.py -- [--render] [--export]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import cr_kit_04_07 as K
import cr47_parts as P
import cr47_ornaments as O
import cr47_run as R

H = 6.5
SPEC = {
    "id": "CR06_WarRoom", "height": H, "z_from": .65, "z_to": 10.0,
    "portal_start": False, "portal_end": True, "header_start": True, "header_end": False,
    "banner": {"width": 1.0, "height": 3.4, "fabric": "PR_Cor_IronTeal"},
    "banner_room": {"width": 1.3, "height": 3.0, "fabric": "PR_Cor_IronTeal"},
    "layout": [
        {"name": "CR06_Lantern", "per": "pier", "index": [0, 1, 2, 3], "offset": [0.0, 3.7, 0.0]},
        {"name": "CR06_CeilingCompass", "per": "bay", "index": [1], "mirror": False, "offset": [0.0, H - .14, 0.0]},
    ],
    "views": [("pawn", (0, -1.0, 3.0), (0, 10, 2.8), 18), ("wall", (3.5, 2.5, 2.0), (-6, 6.5, 3.0), 20),
              ("room", (4, 17, 3.5), (0, 8, 4), 22), ("outside", (-14, -4, 6), (-6, 5, 3), 24)],
}
FACE, BACK, MASS = -.38, -.62, -.72


def rivets(m, pts, r=.022, key="IronTeal"):
    for p in pts:
        m.sphere(p, r, key, 6, 4)


def iron_band(m, y0, y1, z0, z1, x_face):
    m.box(x_face, y0, z0, x_face + .05, y1, z1, "IronTeal", .01)
    n = max(2, int((y1 - y0) / .22))
    rivets(m, [(x_face + .05, y0 + (y1 - y0) * (i + .5) / n, (z0 + z1) * .5) for i in range(n)])


def outer_face(m, y0, y1):
    m.box(MASS - .06, y0, 0, MASS, y1, H, "SandStone", 0)
    m.box(MASS - .14, y0, 0, MASS - .06, y1, .6, "SandStone", .03)
    m.box(MASS - .2, y0, H - .05, MASS - .06, y1, H + .4, "SandStone", .03)
    m.box(MASS - .21, y0, H - .25, MASS - .19, y1, H - .1, "IronTeal", 0)
    m.box(-.7, y0, H + .3, -.38, y1, H + .8, "SandStone", .02)


def dado(m, y0, y1):
    """Blind arcade under a riveted teal iron band, then a sandstone string course."""
    m.box(FACE, y0, 0, FACE + .06, y1, .15, "SandStone", .02)
    n = 3
    w = (y1 - y0) / n
    for i in range(n):
        c = y0 + w * (i + .5)
        inner = K.lift(K.arch(c, w - .2, .45, 8), .2)
        outer = K.lift(K.grow(K.arch(c, w - .2, .45, 8), c, .45, .05), .2)
        m.ring(outer, inner, "x", FACE, FACE + .1, "SandStone")
        m.prism(inner, "x", FACE - .06, FACE, "SandStone")                       # the blind arch's sunk back
    iron_band(m, y0, y1, 1.0, 1.18, FACE)
    m.box(FACE, y0, 1.18, FACE + .09, y1, 1.28, "SandStone", .02)


def bay(col, name, mid=False):
    m = K.Mesh(name)
    y0, y1 = -.985, .985        # ends 1.5 cm short, inside the piers (no shared planes with them)
    if mid:
        m.box(-.4, y0, 0, FACE, -.72, H, "SandStone", 0)
        m.box(-.4, .72, 0, FACE, y1, H, "SandStone", 0)
        m.box(-.4, -.72, 0, FACE, .72, 1.6, "SandStone", 0)
        m.box(-.4, -.72, 5.7, FACE, .72, H, "SandStone", 0)
        m.box(-.42, -.72, 1.6, -.4, .72, 5.7, "SandStone", 0)
        m.ring([(-.78, 1.54), (.78, 1.54), (.78, 5.76), (-.78, 5.76)], [(-.72, 1.6), (.72, 1.6), (.72, 5.7), (-.72, 5.7)],
               "x", FACE - .01, FACE + .04, "IronTeal")
        rivets(m, [(FACE + .04, s * .75, 1.75 + i * .5) for s in (-1, 1) for i in range(8)])
        m.tube([(-.3, -.66, 5.5), (-.3, .66, 5.5)], .022, "IronDark", 8)
        for s in (-1, 1):
            m.sphere((-.3, s * .68, 5.5), .04, "IronDark", 8, 6)
    else:
        K.wall_with_arch(m, y0, y1, 0, H, 0, .95, 2.6, 4.6, FACE, BACK, "SandStone")
        P.window(m, 0, .95, 2.6, 4.6, FACE, BACK, "SandStone", "SandStone", "Glass", "IronDark", .04, keystone=False, bars=(1, 2))
        P.voussoirs(m, 0, .95, 2.6, 4.6, .24, FACE, FACE + .09, "SandStone", "SandStone", n=9)
        m.box(FACE - .02, -.75, 2.48, FACE + .16, .75, 2.6, "SandStone", .02)
        iron_band(m, -.75, .75, 2.4, 2.48, FACE + .1)
        m.torus((BACK + .05, 0, 4.6 + .2), .2, .022, "IronDark", 16, 6, axis="x")     # tracery ring in the head
        m.box(BACK + .03, -.012, 4.6, BACK + .065, .012, 4.6 + .47, "IronDark", .004)
    m.box(BACK - .001, y0, 0, MASS, y1, H, "SandStone", 0)
    holes = [(-.92, .92, 1.5, H - 1.0)] if mid else [(-.8, .8, 2.3, 4.6 + .475 + .4)]
    P.ashlar(m, y0 + .02, y1 - .02, 1.3, H - 1.27, FACE, "SandStone", course=.5, unit=.85, depth=.05, holes=holes,
             seed=7 if mid else 5)
    dado(m, y0, y1)
    P.cornice(m, y0, y1, FACE, H - 1.27, [(.08, .06, None), (.08, .12, None), (.06, .16, None)], "SandStone")
    iron_band(m, y0, y1, H - 1.05, H - .92, FACE)
    outer_face(m, y0, y1)
    ob = m.finish(col)
    # The code hangs a banner from this point (the rod) in the banner bay.
    return ob, ([K.empty(col, "BANNER", (-.3, 0, 5.47))] if mid else [])


def pier(col, name):
    m = K.Mesh(name)
    m.box(MASS, -.46, 0, FACE, .46, H, "SandStone", 0)
    P.stack(m, FACE, 0, .42, -.04, [(0, .3, .08, None), (.3, .45, .05, None), (.45, .55, .02, None)], "SandStone")
    m.box(FACE, -.42, .55, -.04, .42, H - 1.3, "SandStone", .03)
    for s in (-1, 1):                                                              # brass corner guards
        m.box(-.05, s * .44, .55, 0, s * .4, H - 1.3, "Brass", .005)
    for z in (1.0, 2.9, 4.6):                                                      # chevron reliefs
        for s in (-1, 1):
            m.prism([(0, z), (s * .3, z + .18), (s * .3, z + .26), (0, z + .08)], "x", -.03, -.0, "SandStone")
    for z in (1.0, 4.9):
        m.box(FACE, -.47, z - .08, 0, .47, z + .08, "IronTeal", .01)
        rivets(m, [(0.0, -.3 + .2 * i, z) for i in range(4)])
    P.stack(m, FACE, 0, .42, -.04, [(H - 1.3, H - 1.2, .03, "IronTeal"), (H - 1.2, H - 1.1, .07, None),
                                     (H - 1.1, H - 1.0, .13, None)], "SandStone")
    m.box(FACE, -.44, H - 1.0, -.04, .44, H, "SandStone", .02)
    m.box(MASS - .26, -.45, 0, MASS, .45, H + .3, "SandStone", .02)
    m.box(MASS - .3, -.5, 0, MASS, .5, .7, "SandStone", .03)
    return m.finish(col), []


def ceiling_beam(col, name):
    """A teal riveted iron tie beam across the spring of the vault (z -1.0), with curved brackets to the piers."""
    m = K.Mesh(name)
    # The ceiling over the pier line (the ceiling bays stop short of it): 1 m wide, it also closes the ends.
    m.box(-6.6, -.5, 0, 6.6, .5, .3, "SandStone", 0)
    z0 = -1.02
    m.box(-6.25, -.16, z0 - .04, 6.25, .16, z0, "IronTeal", .01)               # bottom flange
    m.box(-6.25, -.05, z0, 6.25, .05, z0 + .3, "IronTeal", .005)               # web
    m.box(-6.25, -.16, z0 + .3, 6.25, .16, z0 + .34, "IronTeal", .01)          # top flange
    rivets(m, [(x, s * .16, z0 - .02) for x in [-6 + .5 * i for i in range(25)] for s in (-1, 1)], .018)
    for s in (-1, 1):
        arc = K.bez((s * 6.25, 0, z0 - .9), (s * 5.95, 0, z0 - .2), (s * 5.6, 0, z0 - .06), (s * 5.0, 0, z0 - .04), 10)
        m.tube(arc, .05, "IronTeal", 8)
        m.tube(K.spiral((s * 5.85, 0, z0 - .32), .02, .14, .9, "xz", a0=math.pi, flip=-s, n=16), .03, "IronTeal", 6)
        m.box(s * 6.25, -.12, z0 - 1.0, s * 6.17, .12, z0 + .34, "IronTeal", .01)     # wall plate
    return m.finish(col), []


def ceiling_bay(col, name):
    """A sandstone barrel vault from the spring (-1.0) to its crown (0), filled to the slab, with a ridge rib and two
    crossing diagonal ribs."""
    m = K.Mesh(name)
    K.arc_fill(m, -6.25, 6.25, -1.0, 1.0, 0.0, "y", -1.38, 1.38, "SandStone", 20)   # up to the slab (not into it)
    m.box(-6.6, -1.38, 0, 6.6, 1.38, .3, "SandStone", 0)
    half, rise = 6.25, 1.0
    R = (half * half + rise * rise) / (2 * rise)
    vz = lambda x: -1.0 + rise - R + math.sqrt(max(0, R * R - x * x))
    m.tube([(-0.0, -1.0, vz(0) - .05), (0, 1.0, vz(0) - .05)], .08, "SandStone", 8)
    for s in (-1, 1):
        pts = [(-half + 2 * half * t, s * (-.95 + 1.9 * t), vz(-half + 2 * half * t) - .04) for t in [i / 20 for i in range(21)]]
        m.tube(pts, .06, "SandStone", 6)
    return m.finish(col), []


def compass(m, c, r, axis_up=1, key_disc="IronTeal", key_rim="Brass", t=.04):
    """A compass rose medallion in the xy plane at c (z = its face): a teal disc, a brass ring, an eight-point
    star. axis_up -1 faces down (a ceiling medallion)."""
    x, y, z = c
    z0, z1 = sorted((z, z - axis_up * t))
    m.cylinder((x, y), r, z0, z1, key_disc, 32)
    circ = lambda rr: [(x + rr * math.cos(2 * math.pi * i / 32), y + rr * math.sin(2 * math.pi * i / 32)) for i in range(32)]
    r0, r1 = sorted((z, z + axis_up * .004))
    m.ring(circ(r), circ(r - .08), "z", r0, r1, key_rim)          # flat brass rims (a floor inlay stays flush)
    m.ring(circ(r * .74), circ(r * .7), "z", r0, r1, key_rim)
    star = []
    for i in range(16):
        a = math.pi * 2 * i / 16 + math.pi / 2
        rr = r * (.92 if i % 4 == 0 else .55 if i % 2 == 0 else .16)
        star.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    s0, s1 = r0, r1
    for i in range(16):                    # the star as triangles about the centre (each convex)
        j = (i + 1) % 16
        m.prism([(x, y), star[i], star[j]], "z", s0, s1, key_rim)


def ceiling_compass(col, name):
    m = K.Mesh(name)
    compass(m, (0, 0, -.0), .6, axis_up=-1)
    return m.finish(col), []


def rook_relief(m, axis, a0, a1, cu, cv, h, key):
    s = h
    m.prism([(cu - .3 * s, cv), (cu + .3 * s, cv), (cu + .3 * s, cv + .12 * s), (cu - .3 * s, cv + .12 * s)], axis, a0, a1, key)
    m.prism([(cu - .2 * s, cv + .12 * s), (cu + .2 * s, cv + .12 * s), (cu + .16 * s, cv + .7 * s), (cu - .16 * s, cv + .7 * s)],
            axis, a0, a1, key)
    m.prism([(cu - .26 * s, cv + .7 * s), (cu + .26 * s, cv + .7 * s), (cu + .26 * s, cv + .82 * s), (cu - .26 * s, cv + .82 * s)],
            axis, a0, a1, key)
    for k in (-1, 0, 1):
        m.prism([(cu + k * .18 * s - .06 * s, cv + .82 * s), (cu + k * .18 * s + .06 * s, cv + .82 * s),
                 (cu + k * .18 * s + .06 * s, cv + .98 * s), (cu + k * .18 * s - .06 * s, cv + .98 * s)], axis, a0, a1, key)


def portal(col, name):
    """The gate frame at the war room's entry - authored as an entry end (frame on y 0..0.8), turned 180 by the
    loader - sandstone piers with teal riveted bands, a lintel with a teal shield and a brass rook."""
    m = K.Mesh(name)
    for s in (-1, 1):
        m.box(s * 6, 0, 0, s * 8, .8, H, "SandStone", .02)
        for yf, d in ((0, -1), (.8, 1)):
            m.box(s * 5.95, yf, 0, s * 8.05, yf + d * .06, .6, "SandStone", .02)
            for z in (1.1, 4.9):
                a0, a1 = sorted((yf, yf + d * .05))
                m.box(s * 6.05, a0, z - .1, s * 7.95, a1, z + .1, "IronTeal", .01)
                rivets(m, [(s * (6.3 + .35 * i), yf + d * .05, z) for i in range(5)])
    m.box(-8.2, -.05, H, 8.2, .85, H + 1.0, "SandStone", .02)
    m.box(-8.4, -.15, H + .85, 8.4, .95, H + 1.0, "SandStone", .03)
    sh = P.shield_outline(.8, .86)
    for yf, d in ((-.05, -1), (.85, 1)):
        a0, a1 = sorted((yf, yf + d * .05))
        m.prism([(u, H + .5 + v) for u, v in sh], "y", a0, a1, "IronTeal")
        b0, b1 = sorted((yf, yf + d * .07))
        m.ring([(u * 1.12, H + .5 + v * 1.1) for u, v in sh], [(u, H + .5 + v) for u, v in sh], "y", b0, b1, "Brass")
        c0, c1 = sorted((yf + d * .05, yf + d * .08))
        rook_relief(m, "y", c0, c1, 0, H + .2, .52, "Brass")
    return m.finish(col), []


def end_wall(col, name):
    """The war room's start floor is 28.5 m wide: its south edge either side of the corridor (|x| 6.6..14.6) and
    over the opening, seen from the room (+y). Pivot on the line (corridor z 10); it stands 0.03 behind the line so
    the portal's face is proud of it."""
    m = K.Mesh(name)
    top = 10.5
    f = -.03
    banners = []
    for s in (-1, 1):
        x0, x1 = s * 6.6, s * 14.6
        m.box(x0, -.4, -3, x1, f, top, "SandStone", 0)
        m.box(x0, f, 0, x1, f + .06, .6, "SandStone", .02)
        m.box(x0, f, 1.0, x1, f + .05, 1.18, "IronTeal", .01)
        m.box(x0, f, top - .5, x1, f + .12, top - .2, "SandStone", .02)
        m.box(x0, f, top - .2, x1, f + .2, top, "SandStone", .02)
        for k in range(3):
            xc = s * (8.6 + k * 2.8)
            m.box(xc - .45, f, 0, xc + .45, f + .25, top - .5, "SandStone", .03)
            m.box(xc - .5, f, 0, xc + .5, f + .3, .55, "SandStone", .03)
            m.box(xc - .5, f, 4.9 - .08, xc + .5, f + .29, 4.9 + .08, "IronTeal", .01)
            m.box(xc - .5, f, top - 1.3, xc + .5, f + .3, top - .95, "SandStone", .03)
        for k in range(2):                                                    # banner rods (the code hangs the cloth)
            xc = s * (8.6 + (k + .5) * 2.8)
            m.tube([(xc - .8, f + .2, H + .9), (xc + .8, f + .2, H + .9)], .025, "IronDark", 8)
            banners.append((xc, f + .25, H + .85))
    m.box(-6.6, -.4, H + 1.0, 6.6, f, top, "SandStone", 0)
    m.box(-6.6, f, top - .5, 6.6, f + .12, top - .2, "SandStone", .02)
    m.box(-6.6, f, top - .2, 6.6, f + .2, top, "SandStone", .02)
    sh = P.shield_outline(1.1, 1.2)
    m.prism([(u, H + 2.1 + v) for u, v in sh], "y", f, f + .05, "IronTeal")
    m.ring([(u * 1.1, H + 2.1 + v * 1.08) for u, v in sh], [(u, H + 2.1 + v) for u, v in sh], "y", f, f + .08, "Brass")
    rook_relief(m, "y", f + .05, f + .09, 0, H + 1.7, .75, "Brass")
    ob = m.finish(col)
    # Facing the war room (+y here): the loader must hang these toward the room, not toward a side wall.
    return ob, [K.empty(col, "BANNER_ROOM_%d" % i, p) for i, p in enumerate(banners)]


def header(col, name):
    m = K.Mesh(name)
    top = 10.5 - H
    m.box(-6.6, 0, 0, 6.6, .3, top, "SandStone", .02)
    for yf, d in ((0, -1), (.3, 1)):
        a0, a1 = sorted((yf, yf + d * .05))
        m.box(-6.6, a0, .1, 6.6, a1, .28, "IronTeal", .01)
        rivets(m, [(-6.3 + .45 * i, yf + d * .05, .19) for i in range(29)])
        m.box(-6.6, yf, top - .3, 6.6, yf + d * .1, top, "SandStone", .02)
    return m.finish(col), []


def floor_inlay(col, name):
    m = K.Mesh(name)
    compass(m, (0, 5.0, .04), 1.1, axis_up=1, t=.016)
    return m.finish(col), []


def build(col):
    return {
        "Bay": bay(col, "CR06_Bay"),
        "BayMid": bay(col, "CR06_BayMid", mid=True),
        "Pier": pier(col, "CR06_Pier"),
        "CeilingBeam": ceiling_beam(col, "CR06_CeilingBeam"),
        "CeilingBay": ceiling_bay(col, "CR06_CeilingBay"),
        "Portal": portal(col, "CR06_Portal"),
        "EndWall": end_wall(col, "CR06_EndWall"),
        "Header": header(col, "CR06_Header"),
        # FloorInlay (the compass rose on the floor) removed: user 11-22 "이거 제거".
        "Lantern": O.iron_lantern(col, "CR06_Lantern"),
        "CeilingCompass": ceiling_compass(col, "CR06_CeilingCompass"),
    }


R.run(SPEC, build)
