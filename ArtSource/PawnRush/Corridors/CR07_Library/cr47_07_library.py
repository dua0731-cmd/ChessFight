# CR07 - the king's library's approach (mini-game 2 -> the royal library), after the GPT concept
# Docs/PawnRush/ConceptArt/SectionCorridors/07_library.png: dark polished walnut panelling, bookshelves recessed into
# the wall between carved piers (books never reach the walk), a brass rail, tall round-headed windows high up with
# warm light, a coffered walnut ceiling with brass rosettes, brass reading lamps on the piers, a long patterned rug.
# Height 7.5. Starts 0.65 m after the mini-game's exit (Header), ends at the library's entry (Portal).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr47_07_library.py -- [--render] [--export]
import math, os, random, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import cr_kit_04_07 as K
import cr47_parts as P
import cr47_ornaments as O
import cr47_run as R

H = 7.5
SPEC = {
    "id": "CR07_Library", "height": H, "z_from": .65, "z_to": 10.0,
    "portal_start": False, "portal_end": True, "header_start": True, "header_end": False,
    "banner": {"width": 0.9, "height": 2.2, "fabric": "PR_Cor_VelvetNavy"},
    "layout": [
        {"name": "CR07_ReadingLamp", "per": "pier", "index": [0, 1, 2, 3], "offset": [0.0, 3.95, 0.0]},
    ],
    "views": [("pawn", (0, -1.0, 3.0), (0, 10, 3.0), 18), ("wall", (3.5, 2.5, 2.0), (-6, 6.5, 3.2), 20),
              ("shelf", (2.0, 4.0, 1.6), (-6, 5.4, 1.8), 30), ("outside", (-14, -4, 6), (-6, 5, 3), 24)],
}
FACE, BACK, MASS = -.38, -.62, -.72
SHELF_BACK = -.66
BOOKS = ["BookRed", "BookGreen", "BookBlue", "BookTan", "BookRed", "BookBlue"]


def outer_face(m, y0, y1):
    m.box(MASS - .06, y0, 0, MASS, y1, H, "CreamStone", 0)
    m.box(MASS - .14, y0, 0, MASS - .06, y1, .7, "SandStone", .02)
    m.box(MASS - .2, y0, H - .05, MASS - .06, y1, H + .4, "SandStone", .03)
    m.box(-.7, y0, H + .3, -.38, y1, H + .85, "CreamStone", .02)


def bookshelf(m, y0, y1, z0, z1, seed):
    """Shelves recessed into the wall from z0 to z1, four rows of books standing back from the wall face."""
    rnd = random.Random(seed)
    m.box(SHELF_BACK - .03, y0, z0, SHELF_BACK, y1, z1, "WalnutDark", 0)
    for s in (y0, y1):
        m.box(SHELF_BACK, s - .03, z0, FACE, s + .03, z1, "Walnut", 0)
    m.box(SHELF_BACK, y0, z1 - .06, FACE, y1, z1, "Walnut", 0)
    rows = 4
    gap = (z1 - z0) / rows
    for r in range(rows):
        zb = z0 + r * gap
        m.box(SHELF_BACK, y0, zb, FACE + .01, y1, zb + .05, "Walnut", .01)
        y = y0 + .04
        while y < y1 - .08:
            w = rnd.uniform(.05, .11)
            h = gap * rnd.uniform(.58, .82)
            lean = rnd.random() < .06
            key = rnd.choice(BOOKS)
            if y + w > y1 - .04:
                break
            d0 = SHELF_BACK + .02
            d1 = FACE - .015 - rnd.uniform(0, .04)
            if lean:
                m.box(d0, y, zb + .05, d1, y + h * .35, zb + .05 + w, key, .006)
                y += h * .35 + .01
            else:
                m.box(d0, y, zb + .05, d1, y + w, zb + .05 + h, key, .006)
                m.box(d1, y + .005, zb + .05 + h * .18, d1 + .003, y + w - .005, zb + .05 + h * .24, "Brass", 0)
                y += w + .004
    m.box(FACE, y0 - .05, z1, FACE + .14, y1 + .05, z1 + .14, "Walnut", .02)          # shelf cornice
    m.box(FACE, y0 - .05, z1 + .14, FACE + .16, y1 + .05, z1 + .18, "Brass", .005)


def bay(col, name, mid=False, seed=3):
    m = K.Mesh(name)
    y0, y1 = -.985, .985        # ends 1.5 cm short, inside the piers (no shared planes with them)
    sy0, sy1 = -.85, .85
    sh0, sh1 = .3, 3.55
    m.box(BACK, y0, 0, FACE, y1, sh0, "Walnut", 0)                     # under the shelves
    m.box(BACK, y0, 0, FACE, sy0, 3.75, "Walnut", 0)
    m.box(BACK, sy1, 0, FACE, y1, 3.75, "Walnut", 0)
    m.box(FACE, y0, 0, FACE + .08, y1, .22, "WalnutDark", .015)       # skirting
    bookshelf(m, sy0, sy1, sh0, sh1, seed)
    m.box(SHELF_BACK - .03, y0, 0, MASS, y1, 3.75, "Walnut", 0)       # behind the shelves
    if mid:
        m.box(BACK, y0, 3.75, FACE, y1, H, "Walnut", 0)
        m.ring([(-.7, 3.95), (.7, 3.95), (.7, H - .9), (-.7, H - .9)], [(-.62, 4.03), (.62, 4.03), (.62, H - .98), (-.62, H - .98)],
               "x", FACE, FACE + .05, "Brass")
        m.box(FACE, -.62, 4.03, FACE + .02, .62, H - .98, "WalnutDark", 0)
        m.tube([(-.3, -.66, H - 1.15), (-.3, .66, H - 1.15)], .022, "Brass", 8)
        for s in (-1, 1):
            m.sphere((-.3, s * .68, H - 1.15), .04, "Brass", 8, 6)
    else:
        K.wall_with_arch(m, y0, y1, 3.75, H, 0, 1.1, 4.2, 5.95, FACE, BACK, "Walnut")
        P.window(m, 0, 1.1, 4.2, 5.95, FACE, BACK, "Walnut", "Brass", "GlassWarm", "Brass", .035, keystone=False, bars=(2, 2))
        P.voussoirs(m, 0, 1.1, 4.2, 5.95, .2, FACE, FACE + .08, "WalnutDark", "Brass", n=9)
    m.box(BACK - .001, y0, 3.75, MASS, y1, H, "Walnut", 0)
    # The brass rail in front of the shelves, on posts from the shelf sides.
    m.tube([(-.13, sy0 + .05, 1.0), (-.13, sy1 - .05, 1.0)], .022, "Brass", 8)
    for y in (sy0 + .05, sy1 - .05):
        m.tube([(FACE, y, 1.0), (-.13, y, 1.0)], .018, "Brass", 8)
        m.sphere((-.13, y, 1.0), .035, "Brass", 8, 6)
    P.cornice(m, y0, y1, FACE, H - .85, [(.12, .06, "WalnutDark"), (.05, .1, "Brass"), (.2, .14, "Walnut"),
                                          (.1, .2, "WalnutDark"), (.38, .3, "Walnut")], "Walnut")
    outer_face(m, y0, y1)
    ob = m.finish(col)
    # The code hangs a banner from this point (the rod) in the banner bay.
    return ob, ([K.empty(col, "BANNER", (-.3, 0, H - 1.18))] if mid else [])


def pier(col, name):
    m = K.Mesh(name)
    m.box(MASS, -.46, 0, FACE, .46, H, "Walnut", 0)
    P.stack(m, FACE, 0, .42, -.04, [(0, .25, .08, "WalnutDark"), (.25, .37, .05, "Walnut"), (.37, .45, .02, "Brass")], "Walnut")
    m.box(FACE, -.42, .45, -.04, .42, H - 1.0, "Walnut", .03)
    for y in (-.3, .3):                                                       # sunk panel mouldings
        m.box(-.06, y - .035, .8, -.0, y + .035, H - 1.4, "WalnutDark", .008)
    m.box(-.04, -.3, .77, 0, .3, .83, "WalnutDark", .006)
    m.box(-.04, -.3, H - 1.43, 0, .3, H - 1.37, "WalnutDark", .006)
    # The crown plaque high on the pier: a brass-rimmed quatrefoil with a crown.
    zc = 5.6
    q = []
    for i in range(32):
        a = 2 * math.pi * i / 32
        rr = .26 + .05 * math.cos(4 * a)
        q.append((rr * math.cos(a), zc + rr * math.sin(a)))
    m.prism(q, "x", -.04, .0, "WalnutDark")
    m.ring([(u * 1.12, zc + (v - zc) * 1.12) for u, v in q], q, "x", -.04, .01, "Brass")
    m.prism([(-.14, zc - .1), (.14, zc - .1), (.14, zc - .04), (-.14, zc - .04)], "x", 0, .02, "Brass")
    for k in (-1, 0, 1):
        tip = .14 if k == 0 else .1
        m.prism([(k * .1 - .04, zc - .04), (k * .1 + .04, zc - .04), (k * .1, zc + tip)], "x", 0, .02, "Brass")
    P.stack(m, FACE, 0, .42, -.04, [(H - 1.0, H - .93, .02, "Brass"), (H - .93, H - .8, .06, "WalnutDark"),
                                     (H - .8, H - .68, .1, "Walnut"), (H - .68, H - .55, .14, "WalnutDark")], "Walnut")
    m.box(FACE, -.45, H - .55, -.03, .45, H, "Walnut", .02)
    m.box(MASS - .26, -.45, 0, MASS, .45, H + .3, "CreamStone", .02)
    m.box(MASS - .3, -.5, 0, MASS, .5, .7, "SandStone", .03)
    return m.finish(col), []


def ceiling_beam(col, name):
    m = K.Mesh(name)
    # The ceiling over the pier line (the ceiling bays stop short of it): 1 m wide, it also closes the ends.
    m.box(-6.6, -.5, 0, 6.6, .5, .3, "WalnutDark", 0)
    m.box(-6.25, -.25, -.6, 6.25, .25, 0, "Walnut", .03)
    m.box(-6.25, -.18, -.64, 6.25, .18, -.6, "Brass", .01)
    for s in (-1, 1):
        m.box(s * 6.25, -.3, -1.0, s * 5.95, .3, -.55, "WalnutDark", .03)
    return m.finish(col), []


def ceiling_bay(col, name):
    """A coffered walnut ceiling: three coffers across each half, brass quatrefoil rosettes in them."""
    m = K.Mesh(name)
    m.box(-6.6, -1.38, 0, 6.6, 1.38, .3, "WalnutDark", 0)
    xs = [-6.25, -4.15, -2.05, 0.0, 2.05, 4.15, 6.25]
    for x in xs[1:-1]:
        m.box(x - .12, -1.0, -.32, x + .12, 1.0, 0, "Walnut", .02)
    m.box(-6.25, -.12, -.32, 6.25, .12, 0, "Walnut", .02)
    for i in range(len(xs) - 1):
        cx = (xs[i] + xs[i + 1]) * .5
        for cy in (-.5, .5):
            m.ring([(cx - .9, cy - .38), (cx + .9, cy - .38), (cx + .9, cy + .38), (cx - .9, cy + .38)],
                   [(cx - .78, cy - .27), (cx + .78, cy - .27), (cx + .78, cy + .27), (cx - .78, cy + .27)], "z", -.16, 0, "Walnut")
            m.ring([(cx - .78, cy - .27), (cx + .78, cy - .27), (cx + .78, cy + .27), (cx - .78, cy + .27)],
                   [(cx - .74, cy - .23), (cx + .74, cy - .23), (cx + .74, cy + .23), (cx - .74, cy + .23)], "z", -.18, -.14, "Brass")
            for k in range(4):
                a = math.pi * .5 * k
                m.sphere((cx + .08 * math.cos(a), cy + .08 * math.sin(a), -.1), .065, "Brass", 8, 5)
            m.sphere((cx, cy, -.12), .045, "Brass", 8, 5)
    return m.finish(col), []


def crown(m, c, s, key="Brass"):
    x, y, z = c
    m.torus((x, y, z), .3 * s, .05 * s, key, 20, 6)
    for i in range(5):
        a = 2 * math.pi * i / 5
        px, py = x + .28 * s * math.cos(a), y + .28 * s * math.sin(a)
        m.tube([(px, py, z), (px, py, z + .34 * s)], .035 * s, key, 6)
        m.sphere((px, py, z + .38 * s), .06 * s, key, 8, 5)
    m.sphere((x, y, z + .3 * s), .09 * s, key, 10, 6)


def portal(col, name):
    """The gate frame at the library's entry (authored as an entry end, turned 180 by the loader): carved walnut
    piers with brass bands, a lintel with a brass-framed crown cartouche."""
    m = K.Mesh(name)
    for s in (-1, 1):
        m.box(s * 6, 0, 0, s * 8, .8, H, "Walnut", .02)
        for yf, d in ((0, -1), (.8, 1)):
            a0, a1 = sorted((yf, yf + d * .07))
            m.box(s * 5.95, a0, 0, s * 8.05, a1, .5, "WalnutDark", .02)
            b0, b1 = sorted((yf, yf + d * .04))
            m.ring([(s * 6.3, .8), (s * 7.7, .8), (s * 7.7, H - .6), (s * 6.3, H - .6)],
                   [(s * 6.42, .92), (s * 7.58, .92), (s * 7.58, H - .72), (s * 6.42, H - .72)], "y", b0, b1, "WalnutDark")
            m.box(s * 6.15, a0, H - .45, s * 7.85, a1, H - .38, "Brass", .005)
    m.box(-8.2, -.05, H, 8.2, .85, H + 1.0, "Walnut", .02)
    m.box(-8.25, -.1, H + .1, 8.25, .9, H + .16, "Brass", .005)
    m.box(-8.4, -.15, H + .85, 8.4, .95, H + 1.02, "WalnutDark", .03)
    for yf, d in ((-.05, -1), (.85, 1)):
        a0, a1 = sorted((yf, yf + d * .04))
        m.box(-1.0, a0, H + .25, 1.0, a1, H + .8, "WalnutDark", .01)
        b0, b1 = sorted((yf, yf + d * .06))
        m.ring([(-1.08, H + .2), (1.08, H + .2), (1.08, H + .85), (-1.08, H + .85)],
               [(-1.0, H + .25), (1.0, H + .25), (1.0, H + .8), (-1.0, H + .8)], "y", b0, b1, "Brass")
    crown(m, (0, .4, H + 1.02), 1.1)
    return m.finish(col), []


def header(col, name):
    m = K.Mesh(name)
    top = 10.5 - H
    m.box(-6.6, 0, 0, 6.6, .3, top, "Walnut", .02)
    for yf, d in ((0, -1), (.3, 1)):
        a0, a1 = sorted((yf, yf + d * .04))
        for i in range(6):
            x = -6.4 + i * 2.2
            m.ring([(x, .5), (x + 1.8, .5), (x + 1.8, top - .5), (x, top - .5)],
                   [(x + .1, .6), (x + 1.7, .6), (x + 1.7, top - .6), (x + .1, top - .6)], "y", a0, a1, "WalnutDark")
        m.box(-6.6, yf, 0, 6.6, yf + d * .08, .3, "WalnutDark", .02)
        m.box(-6.6, yf, top - .3, 6.6, yf + d * .1, top, "WalnutDark", .02)
    return m.finish(col), []


def floor_inlay(col, name):
    """A long patterned rug with a navy border, flush on the paving (top +0.04)."""
    m = K.Mesh(name)
    m.box(-3.6, 1.6, .024, 3.6, 9.0, .036, "RugBorder", .004)
    m.box(-3.3, 1.9, .03, 3.3, 8.7, .04, "Rug", .003)
    for x in (-3.15, 3.15):
        m.box(x - .03, 1.75, .036, x + .03, 8.85, .041, "Brass", 0)
    return m.finish(col), []


def build(col):
    return {
        "Bay": bay(col, "CR07_Bay"),
        "BayMid": bay(col, "CR07_BayMid", mid=True, seed=11),
        "Pier": pier(col, "CR07_Pier"),
        "CeilingBeam": ceiling_beam(col, "CR07_CeilingBeam"),
        "CeilingBay": ceiling_bay(col, "CR07_CeilingBay"),
        "Portal": portal(col, "CR07_Portal"),
        "Header": header(col, "CR07_Header"),
        "FloorInlay": floor_inlay(col, "CR07_FloorInlay"),
        "ReadingLamp": O.reading_lamp(col, "CR07_ReadingLamp"),
    }


R.run(SPEC, build)
