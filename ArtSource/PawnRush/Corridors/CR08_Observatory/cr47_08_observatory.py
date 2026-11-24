# CR08 - the royal observatory's approach (mini-game 2 -> the observatory's entry tunnel), after the GPT concept
# Docs/PawnRush/ConceptArt/SectionCorridors/08_observatory.png (user 11-22 "천문대 쪽도 코덱스에게 사진 받아서 정밀하게
# 모델링 해"): cream marble piers with gold capitals and navy star plaques, a navy wainscot of gold-starred panels,
# round gold-framed oculus windows onto a starry dusk, a gold star frieze, a deep navy barrel vault with gold stars
# and constellation lines between gold-edged marble ribs, pearl globe sconces, a purple runner with gold stars, and at
# the end a gate frame of marble posts with pearl globes and a navy starred arch band. The observatory's own textures
# (RO_Marble / RO_NavyEnamel / RO_Gold / RO_Pearl) dress it. Height 7.5. Starts 0.65 m after the mini-game's exit
# (Header), ends at the observatory's entry tunnel (Portal).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr47_08_observatory.py -- [--render] [--export]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import cr_kit_04_07 as K
import cr47_parts as P
import cr47_ornaments as O
import cr47_run as R

H = 7.5
SPEC = {
    "id": "CR08_Observatory", "height": H, "z_from": .65, "z_to": 10.0,
    "portal_start": False, "portal_end": True, "header_start": True, "header_end": False,
    "banner": {"width": 1.0, "height": 2.6, "fabric": "PR_Cor_VelvetPurple"},
    "layout": [
        {"name": "CR08_GlobeSconce", "per": "pier", "index": [0, 1, 2, 3], "offset": [0.0, 3.4, 0.0]},
    ],
    "views": [("pawn", (0, -1.0, 3.0), (0, 10, 3.2), 18), ("wall", (3.5, 2.5, 2.0), (-6, 6.5, 3.4), 20),
              ("vault", (0, 1.0, 1.6), (0, 6.0, 7.2), 22), ("outside", (-14, -4, 6), (-6, 5, 3), 24)],
}
FACE, BACK, MASS = -.38, -.62, -.72
GOLD, NAVY, MARBLE, PEARL = "ObsGold", "ObsNavy", "ObsMarble", "ObsPearl"
SPRING, RISE = -1.4, 1.4          # the vault springs 1.4 m under the ceiling line and rises to it


def star_outline(cu, cv, r, inner=.38, points=8, turn=math.pi / 2):
    pts = []
    for i in range(points * 2):
        a = turn + math.pi * i / points
        rr = r if i % 2 == 0 else r * inner
        pts.append((cu + rr * math.cos(a), cv + rr * math.sin(a)))
    return pts


def star(m, axis, a0, a1, cu, cv, r, key=GOLD, inner=.38, points=8):
    """An eight-point star in relief: triangles about its centre (each convex), extruded a0..a1 along `axis`."""
    pts = star_outline(cu, cv, r, inner, points)
    n = len(pts)
    for i in range(n):
        m.prism([(cu, cv), pts[i], pts[(i + 1) % n]], axis, a0, a1, key)


def star_panel(m, y0, y1, z0, z1, x_face, frame=.06):
    """A navy panel in a gold frame with a gold star in the middle, standing on a wall face (west frame)."""
    m.ring([(y0, z0), (y1, z0), (y1, z1), (y0, z1)], [(y0 + frame, z0 + frame), (y1 - frame, z0 + frame), (y1 - frame, z1 - frame),
                                                     (y0 + frame, z1 - frame)], "x", x_face, x_face + .045, GOLD)
    m.box(x_face, y0 + frame, z0 + frame, x_face + .02, y1 - frame, z1 - frame, NAVY, 0)
    star(m, "x", x_face + .02, x_face + .04, (y0 + y1) * .5, (z0 + z1) * .5, min(y1 - y0, z1 - z0) * .3)


def outer_face(m, y0, y1):
    m.box(MASS - .06, y0, 0, MASS, y1, H, MARBLE, 0)
    m.box(MASS - .14, y0, 0, MASS - .06, y1, .7, MARBLE, .02)
    m.box(MASS - .2, y0, H - .05, MASS - .06, y1, H + .4, MARBLE, .03)
    m.box(MASS - .21, y0, H + .1, MASS - .19, y1, H + .16, GOLD, 0)
    m.box(-.7, y0, H + .3, -.38, y1, H + .85, MARBLE, .02)


def frieze(m, y0, y1):
    """The navy star frieze under the vault's spring, gold mouldings above and below."""
    z0 = H + SPRING - .55
    m.box(FACE, y0, z0 - .08, FACE + .14, y1, z0, GOLD, .01)
    m.box(FACE, y0, z0, FACE + .06, y1, z0 + .45, NAVY, 0)
    n = 4
    for i in range(n):
        y = y0 + (y1 - y0) * (i + .5) / n
        star(m, "x", FACE + .06, FACE + .08, y, z0 + .225, .13 if i % 2 == 0 else .08)
    P.cornice(m, y0, y1, FACE, z0 + .45, [(.06, .08, GOLD), (.04, .12, MARBLE), (.1, .16, MARBLE)], MARBLE)


def bay(col, name, mid=False):
    m = K.Mesh(name)
    y0, y1 = -.985, .985          # ends inside the piers (no shared planes with them)
    m.box(BACK, y0, 0, MASS, y1, H, NAVY, 0)
    zc, r = 3.75, .5
    if mid:
        m.box(BACK, y0, 0, FACE, y1, H, NAVY, 0)
        star_panel(m, -.72, .72, 2.3, 5.4, FACE)                  # behind the code's banner
        m.tube([(-.3, -.66, 5.55), (-.3, .66, 5.55)], .022, GOLD, 8)
        for s in (-1, 1):
            m.sphere((-.3, s * .7, 5.55), .045, GOLD, 8, 6)
    else:
        # The wall round a round oculus: boxes and strips round the circle, a gold ring frame, dusk glass with stars.
        m.box(BACK, y0, 0, FACE, y1, zc - r, NAVY, 0)
        m.box(BACK, y0, zc + r, FACE, y1, H, NAVY, 0)
        seg = 16
        for side in (-1, 1):
            for i in range(seg // 2):
                a0, a1 = -math.pi / 2 + math.pi * i / (seg // 2), -math.pi / 2 + math.pi * (i + 1) / (seg // 2)
                ya, yb = side * r * math.cos(a0), side * r * math.cos(a1)
                za, zb = zc + r * math.sin(a0), zc + r * math.sin(a1)
                edge = y1 if side > 0 else y0
                m.prism([(ya, za), (edge, za), (edge, zb), (yb, zb)] if side > 0 else [(edge, za), (ya, za), (yb, zb), (edge, zb)],
                        "x", BACK, FACE, NAVY)
        m.cylinder((0, zc), r + .02, BACK - .02, BACK, "ObsSkyGlass", 24, axis="x")
        for (dy, dz, rr) in ((-.2, .15, .05), (.15, .25, .035), (.05, -.2, .045), (-.15, -.25, .03), (.25, -.05, .03)):
            star(m, "x", BACK, BACK + .01, dy, zc + dz, rr, GOLD, .4, 4)
        m.torus((FACE + .05, 0, zc), r + .06, .07, GOLD, 32, 8, axis="x")
        m.torus((FACE + .03, 0, zc), r + .17, .035, GOLD, 32, 6, axis="x")
        m.torus((BACK + .04, 0, zc), r - .02, .03, GOLD, 32, 6, axis="x")
        m.box(BACK + .015, -.012, zc - r, BACK + .04, .012, zc + r, GOLD, .004)            # the glazing cross
        m.box(BACK + .015, -r, zc - .012, BACK + .04, r, zc + .012, GOLD, .004)
        star(m, "x", FACE + .02, FACE + .06, 0, zc + r + .32, .13)                          # a star over it
    # A navy wainscot of gold-framed star panels, a gold rail on it.
    m.box(FACE, y0, 0, FACE + .1, y1, 1.55, NAVY, 0)
    star_panel(m, y0 + .05, -.02, .25, 1.4, FACE + .1)
    star_panel(m, .02, y1 - .05, .25, 1.4, FACE + .1)
    m.box(FACE, y0, 1.55, FACE + .16, y1, 1.68, GOLD, .015)
    m.box(FACE, y0, 0, FACE + .14, y1, .2, MARBLE, .015)
    frieze(m, y0, y1)
    m.box(FACE, y0, H + SPRING - .1, FACE + .24, y1, H, MARBLE, 0)                           # the wall up to the vault
    outer_face(m, y0, y1)
    ob = m.finish(col)
    # The code hangs a banner from this point (the rod) in the banner bay.
    return ob, ([K.empty(col, "BANNER", (-.3, 0, 5.52))] if mid else [])


def pier(col, name):
    """A cream marble pier with a gold stepped base and capital and a navy star plaque."""
    m = K.Mesh(name)
    m.box(MASS, -.46, 0, FACE, .46, H, MARBLE, 0)
    P.stack(m, FACE, 0, .42, -.04, [(0, .25, .08, MARBLE), (.25, .33, .06, GOLD), (.33, .45, .03, MARBLE)], MARBLE)
    m.box(FACE, -.42, .45, -.04, .42, H + SPRING - .3, MARBLE, .03)
    star_panel(m, -.28, .28, 4.35, 5.05, -.04, .05)
    m.box(-.05, -.3, 1.68, -.0, .3, 1.74, GOLD, .006)
    P.stack(m, FACE, 0, .42, -.04, [(H + SPRING - .3, H + SPRING - .22, .02, GOLD), (H + SPRING - .22, H + SPRING - .1, .07, MARBLE),
                                     (H + SPRING - .1, H + SPRING, .12, GOLD)], MARBLE)
    m.box(FACE, -.45, H + SPRING, -.06, .45, H, MARBLE, .02)
    m.box(MASS - .28, -.46, 0, MASS, .46, H + .3, MARBLE, .02)
    m.box(MASS - .32, -.5, 0, MASS, .5, .8, MARBLE, .03)
    return m.finish(col), []


def ceiling_beam(col, name):
    """A cream marble arch rib with gold edges and a gold star at its crown, over the pier line."""
    m = K.Mesh(name)
    # The ceiling over the pier line (the ceiling bays stop short of it): 1 m wide, it also closes the ends.
    m.box(-6.6, -.5, 0, 6.6, .5, .3, NAVY, 0)
    # The rib stands 15 cm below the vault (never in its surface).
    K.arc_fill(m, -6.2, 6.2, SPRING - .15, RISE, -.05, "y", -.25, .25, MARBLE, 19)   # 19 strips: no joint in a vault joint
    half, rise = 6.25, RISE
    Rr = (half * half + rise * rise) / (2 * rise)
    vz = lambda x: SPRING - .15 + rise - Rr + math.sqrt(max(0, Rr * Rr - x * x))
    for s in (-1, 1):                         # gold edges along both faces of the rib's soffit
        pts = [(-6.2 + 12.4 * t, s * .25, vz(-6.2 + 12.4 * t) - .02) for t in [i / 24 for i in range(25)]]
        m.tube(pts, .035, GOLD, 6)
    star(m, "y", -.29, -.25, 0, vz(0) - .2, .16)
    star(m, "y", .25, .29, 0, vz(0) - .2, .16)
    return m.finish(col), []


def ceiling_bay(col, name):
    """A deep navy barrel vault between two ribs, scattered with gold stars and two constellations of gold dots
    joined by thin gold lines, a gold-banded marble ridge strip."""
    m = K.Mesh(name)
    K.arc_fill(m, -6.25, 6.25, SPRING, RISE, 0.0, "y", -1.38, 1.38, NAVY, 24)
    m.box(-6.6, -1.38, 0, 6.6, 1.38, .3, NAVY, 0)
    half, rise = 6.25, RISE
    Rr = (half * half + rise * rise) / (2 * rise)
    vz = lambda x: SPRING + rise - Rr + math.sqrt(max(0, Rr * Rr - x * x))
    m.box(-.18, -1.0, vz(0) - .06, .18, 1.0, 0, MARBLE, .01)
    m.box(-.22, -1.0, vz(0) - .08, -.18, 1.0, vz(0) - .02, GOLD, 0)
    m.box(.18, -1.0, vz(0) - .08, .22, 1.0, vz(0) - .02, GOLD, 0)
    import random
    rnd = random.Random(8)
    for i in range(22):
        x = rnd.uniform(-5.6, 5.6)
        if abs(x) < .5:
            continue
        y = rnd.uniform(-.85, .85)
        r = rnd.choice((.05, .07, .1, .14))
        z = vz(x) - .006
        pts = star_outline(x, y, r, .38, 4 if r < .08 else 8)
        for k in range(len(pts)):
            m.prism([(x, y), pts[k], pts[(k + 1) % len(pts)]], "z", z - .012, z, GOLD)
    for xs, ys in (((-4.6, -3.9, -3.1, -2.6, -1.9), (-.6, -.2, -.45, .1, .5)), ((1.8, 2.5, 3.4, 4.0), (.55, .1, .35, -.4))):
        dots = [(x, y, vz(x) - .02) for x, y in zip(xs, ys)]
        for d in dots:
            m.sphere(d, .045, GOLD, 8, 5)
        for a, b in zip(dots, dots[1:]):
            m.tube([a, b], .009, GOLD, 4)
    return m.finish(col), []


def portal(col, name):
    """The gate frame at the observatory's entry (authored as an entry end on y 0..0.8, turned by the loader): marble
    posts with gold capitals and pearl globes on top, a navy arch band of gold stars over the way with a big gold star."""
    m = K.Mesh(name)
    for s in (-1, 1):
        m.box(s * 6, 0, 0, s * 8, .8, H + .3, MARBLE, .02)
        for yf, d in ((0, -1), (.8, 1)):
            a0, a1 = sorted((yf, yf + d * .06))
            m.box(s * 5.95, a0, 0, s * 8.05, a1, .5, MARBLE, .02)
            m.box(s * 5.95, a0, .5, s * 8.05, a1, .58, GOLD, .01)
            star_panel_y = (s * 6.35, s * 7.65)
            y0, y1 = sorted(star_panel_y)
            m.ring([(y0, 2.4), (y1, 2.4), (y1, 4.4), (y0, 4.4)], [(y0 + .07, 2.47), (y1 - .07, 2.47), (y1 - .07, 4.33), (y0 + .07, 4.33)],
                   "y", a0, a1, GOLD)
            b0, b1 = sorted((yf, yf + d * .02))
            m.box(y0 + .07, b0, 2.47, y1 - .07, b1, 4.33, NAVY, 0)
            c0, c1 = sorted((yf + d * .02, yf + d * .045))
            star(m, "y", c0, c1, s * 7.0, 3.4, .36)
        m.box(s * 5.9, -.1, H + .3, s * 8.1, .9, H + .45, GOLD, .02)
        m.box(s * 6.3, .1, H + .45, s * 7.7, .7, H + .7, MARBLE, .02)
        m.lathe([(0, 0), (.22, 0), (.25, .06), (.12, .14), (.1, .28), (.2, .36), (0, .38)], (s * 7.0, .4, H + .7), GOLD, 16)
        m.sphere((s * 7.0, .4, H + 1.3), .27, PEARL, 20, 12)
    # The arch band over the way: a navy segmental arch with gold edges, gold stars on it, a big star on its crown.
    rise = 1.1
    K.arc_fill(m, -6.0, 6.0, H - .2, rise, H + 1.2, "y", -.05, .85, NAVY, 24)
    half = 6.0
    Rr = (half * half + rise * rise) / (2 * rise)
    vz = lambda x: H - .2 + rise - Rr + math.sqrt(max(0, Rr * Rr - x * x))
    for yf in (-.08, .88):
        pts = [(-6 + 12 * t, yf, vz(-6 + 12 * t) - .03) for t in [i / 24 for i in range(25)]]
        m.tube(pts, .05, GOLD, 6)
        for i in range(1, 8):
            x = -6 + 12 * i / 8
            a0, a1 = sorted((yf, yf + (-.03 if yf < 0 else .03)))
            star(m, "y", a0, a1, x, (vz(x) + H + 1.2) * .5, .12 if i % 2 else .08)
    m.box(-6.0, -.1, H + 1.2, 6.0, .9, H + 1.32, GOLD, .02)
    star(m, "y", .35, .45, 0, H + 1.75, .55)
    m.sphere((0, .4, H + 1.75), .1, GOLD, 10, 6)
    return m.finish(col), []


def header(col, name):
    m = K.Mesh(name)
    top = 10.5 - H
    m.box(-6.6, 0, 0, 6.6, .3, top, NAVY, 0)
    for yf, d in ((0, -1), (.3, 1)):
        a0, a1 = sorted((yf, yf + d * .05))
        m.box(-6.6, a0, 0, 6.6, a1, .25, GOLD, .01)
        m.box(-6.6, a0, top - .3, 6.6, a1, top, MARBLE, .01)
        for i in range(7):
            b0, b1 = sorted((yf, yf + d * .03))
            star(m, "y", b0, b1, -5.4 + i * 1.8, top * .5 + .1, .22 if i % 2 == 0 else .14)
    return m.finish(col), []


def floor_inlay(col, name):
    """A purple runner with a gold border of small stars and big gold stars down the middle (flush, top <= +0.045)."""
    m = K.Mesh(name)
    m.box(-1.3, 0, .024, 1.3, 10, .038, "VelvetPurple", .004)
    for s in (-1, 1):
        m.box(s * 1.22, 0, .038, s * 1.27, 10, .042, GOLD, 0)
        for i in range(14):
            star(m, "z", .038, .041, s * 1.1, .35 + i * .7, .06, GOLD, .4, 4)
    for i, (y, r) in enumerate(((2.0, .45), (4.0, .22), (6.0, .45), (8.0, .22))):
        star(m, "z", .038, .042, 0, y, r)
    return m.finish(col), []


def build(col):
    return {
        "Bay": bay(col, "CR08_Bay"),
        "BayMid": bay(col, "CR08_BayMid", mid=True),
        "Pier": pier(col, "CR08_Pier"),
        "CeilingBeam": ceiling_beam(col, "CR08_CeilingBeam"),
        "CeilingBay": ceiling_bay(col, "CR08_CeilingBay"),
        "Portal": portal(col, "CR08_Portal"),
        "Header": header(col, "CR08_Header"),
        "FloorInlay": floor_inlay(col, "CR08_FloorInlay"),
        "GlobeSconce": O.globe_sconce(col, "CR08_GlobeSconce", glass=PEARL, metal=GOLD),
    }


R.run(SPEC, build)
