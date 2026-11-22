# The corridor ornaments of CR04..CR07, modelled in Blender after the GPT sheet
# Docs/PawnRush/ConceptArt/SectionCorridors/AssetRefs/CR_ornaments_01.png (user 11-22: "에셋 시트 너가 블렌더에서
# 정밀하게 모델링 해" - no Tripo). All fixtures are empty and unlit: the game adds the light and the flame.
# (CR01..03's own ornaments live in cr13_ornaments.py / cr13_common.py; this file is the CR04..07 set.)
#
# Pivot: wall pieces at the wall-mount point (back plate centre on the wall face), reaching out along +x (a west
# wall piece, as the corridor modules); the chandelier at its ceiling point, hanging down -z.
# Empties: LIGHT_<n> = where the code puts a Light (and a flame), FLAME_<n> = a flame only (a candle cup).
import math
from mathutils import Vector
import cr_kit_04_07 as K


def _marks(col, pts, kind):
    return [K.empty(col, "%s_%d" % (kind, i), p) for i, p in enumerate(pts)]


def cup(m, c, key="Brass", s=1.0):
    """An empty candle cup: a drip pan and a cup, its rim at c.z."""
    x, y, z = c
    m.lathe([(0, -.075 * s), (.012 * s, -.075 * s), (.016 * s, -.06 * s), (.05 * s, -.05 * s), (.055 * s, -.042 * s),
             (.02 * s, -.036 * s), (.022 * s, -.02 * s), (.034 * s, -.008 * s), (.036 * s, 0), (.028 * s, 0),
             (.026 * s, -.012 * s), (0, -.012 * s)], (x, y, z), key, 14)


def sconce3(col, name):
    """Three-arm brass wall sconce (sheet #1): oblong back plate with a raised panel, a central stem, two S arms
    and three empty cups. 0.55 m tall, 0.5 m across, 0.22 m out."""
    m = K.Mesh(name)
    pill = []
    for i in range(10):
        a = math.pi * i / 9
        pill.append((.085 * math.cos(a), .19 + .085 * math.sin(a)))
    for i in range(10):
        a = math.pi + math.pi * i / 9
        pill.append((.085 * math.cos(a), -.19 + .085 * math.sin(a)))
    m.prism(pill, "x", 0, .022, "Brass")
    m.prism([(u * .78, v * .86) for u, v in pill], "x", .022, .036, "Brass")
    m.lathe([(.05, 0), (.045, .02), (.03, .03), (0, .03)], (.036, 0, -.06), "Brass", 14, axis="x")
    m.tube([(.05, 0, -.06), (.12, 0, -.06)], .022, "Brass", 10)
    m.lathe([(0, -.2), (.028, -.2), (.04, -.17), (.025, -.14), (.03, -.06), (.022, .02), (.03, .08), (.02, .12),
             (.024, .14), (0, .14)], (.12, 0, 0), "Brass", 14)
    for side in (-1, 1):
        arm = K.bez((.12, 0, -.1), (.13, side * .14, -.16), (.17, side * .22, -.08), (.17, side * .21, .06), 12)
        m.tube(arm, .017, "Brass", 8)
        m.sphere((.12, side * .06, -.13), .022, "Brass", 10, 6)
        cup(m, (.17, side * .21, .11))
    cup(m, (.12, 0, .21))
    ob = m.finish(col)
    marks = [K.empty(col, "LIGHT_0", (.15, 0, .26))]
    marks += _marks(col, [(.17, -.21, .12), (.12, 0, .22), (.17, .21, .12)], "FLAME")
    return ob, marks


def chandelier(col, name):
    """Two-tier brass ring chandelier (sheet #2): ceiling canopy, chain, a turned stem, an upper ring of six and a
    lower ring of eight empty cups on curved arms, studs round the lower ring. Top 0 .. bottom -2.5; lower ring
    1.7 m across at -2.0."""
    m = K.Mesh(name)
    m.lathe([(0, -.08), (.06, -.08), (.16, -.05), (.18, -.02), (.18, 0)], (0, 0, 0), "Brass", 18)
    m.sphere((0, 0, -.1), .035, "Brass", 10, 6)
    chain_top, chain_bottom, n = -.14, -.95, 9
    for i in range(n):
        z = chain_top + (chain_bottom - chain_top) * (i + .5) / n
        m.torus((0, 0, z), .045, .013, "Brass", 12, 6, axis="x" if i % 2 else "y")
    zs = -1.0
    m.lathe([(0, 0), (.04, 0), (.06, -.04), (.035, -.08), (.04, -.3), (.085, -.38), (.085, -.43), (.04, -.47),
             (.04, -.85), (.11, -.95), (.12, -1.0), (.06, -1.06), (.06, -1.25), (.035, -1.4), (0, -1.45)],
            (0, 0, zs), "Brass", 16)
    up_z, lo_z = zs - .42, zs - 1.0
    m.torus((0, 0, up_z), .45, .032, "Brass", 32, 8)
    m.torus((0, 0, lo_z), .85, .045, "Brass", 40, 8)
    m.torus((0, 0, lo_z - .07), .85, .022, "Brass", 40, 6)
    for i in range(8):
        a = 2 * math.pi * i / 8
        d = Vector((math.cos(a), math.sin(a), 0))
        if i % 2 == 0:
            arm = K.bez(d * .06 + Vector((0, 0, lo_z + .32)), d * .5 + Vector((0, 0, lo_z + .36)),
                        d * .8 + Vector((0, 0, lo_z + .18)), d * .85 + Vector((0, 0, lo_z + .02)), 12)
            m.tube(arm, .022, "Brass", 8)
        m.tube([d * .85 + Vector((0, 0, lo_z)), d * .85 + Vector((0, 0, lo_z + .06))], .018, "Brass", 8)
        cup(m, tuple(d * .85 + Vector((0, 0, lo_z + .13))), s=1.3)
        m.sphere(tuple(d * .85 + Vector((0, 0, lo_z - .1))), .035, "Brass", 8, 6)
    for i in range(6):
        a = 2 * math.pi * (i + .5) / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        m.tube([d * .05 + Vector((0, 0, up_z + .12)), d * .32 + Vector((0, 0, up_z + .07)), d * .45 + Vector((0, 0, up_z))],
               .016, "Brass", 8)
        cup(m, tuple(d * .45 + Vector((0, 0, up_z + .12))), s=1.15)
    for i in range(16):
        a = 2 * math.pi * (i + .5) / 16
        m.sphere((.85 * math.cos(a), .85 * math.sin(a), lo_z + .045), .03, "Brass", 8, 5)
    m.lathe([(0, 0), (.07, 0), (.05, -.06), (.018, -.12), (0, -.15)], (0, 0, zs - 1.45), "Brass", 12)
    ob = m.finish(col)
    flames = [(.85 * math.cos(2 * math.pi * i / 8), .85 * math.sin(2 * math.pi * i / 8), lo_z + .14) for i in range(8)]
    flames += [(.45 * math.cos(2 * math.pi * (i + .5) / 6), .45 * math.sin(2 * math.pi * (i + .5) / 6), up_z + .13)
               for i in range(6)]
    return ob, [K.empty(col, "LIGHT_0", (0, 0, lo_z + .25))] + _marks(col, flames, "FLAME")


def portrait(col, name, w=.62, h=.8):
    """Ornate brass portrait frame (sheet #3): a moulded frame with corner bosses and scrolls, a scrolled crest with
    a shell on top, a flat dark canvas (no picture). Hangs on its back centre (x 0); 0.62 x 0.8 m plus the crest."""
    m = K.Mesh(name)
    hw, hh = w * .5, h * .5
    fw = .075
    rect = lambda a, b: [(-a, -b), (a, -b), (a, b), (-a, b)]
    m.ring(rect(hw, hh), rect(hw - fw, hh - fw), "x", 0, .035, "Brass")
    m.ring(rect(hw - .012, hh - .012), rect(hw - fw + .012, hh - fw + .012), "x", .035, .055, "Brass")
    m.ring(rect(hw - fw + .004, hh - fw + .004), rect(hw - fw - .012, hh - fw - .012), "x", .01, .03, "Brass")
    m.box(.005, -(hw - fw), -(hh - fw), .012, hw - fw, hh - fw, "Canvas", 0)
    for sy in (-1, 1):
        for sz in (-1, 1):
            m.lathe([(0, 0), (.03, 0), (.034, .015), (.02, .035), (0, .04)], (.03, sy * (hw - .02), sz * (hh - .02)),
                    "Brass", 12, axis="x")
            m.tube(K.spiral((.05, sy * (hw - .07), sz * (hh + .005)), .006, .035, 1.0, "yz",
                            a0=math.pi if sy > 0 else 0, flip=sy * sz, n=16), .009, "Brass", 6)
    for sy in (-1, 1):
        m.tube(K.spiral((.04, sy * .085, hh + .06), .006, .05, 1.1, "yz", a0=0 if sy > 0 else math.pi, flip=-sy, n=20),
               .012, "Brass", 6)
        m.tube(K.bez((.04, sy * .03, hh), (.04, sy * .12, hh + .02), (.04, sy * .19, hh + .005), (.04, sy * .22, hh - .04), 8),
               .012, "Brass", 6)
    fan = [(0, hh - .01)]
    for i in range(9):
        a = math.pi * i / 8
        fan.append((.06 * math.cos(a), hh + .03 + .07 * math.sin(a)))
    m.prism(fan, "x", .02, .05, "Brass")
    for i in range(5):
        a = math.pi * (i + .5) / 5
        m.tube([(.05, .0, hh + .01), (.055, .06 * math.cos(a), hh + .03 + .065 * math.sin(a))], .006, "Brass", 6)
    m.sphere((.045, 0, hh + .13), .02, "Brass", 10, 6)
    return m.finish(col), []


def globe_sconce(col, name, glass="GlassClear", metal="Brass"):
    """Brass wall sconce with one clear glass globe (sheet #4): round back plate, a curved arm, a cup, the globe and
    a finial. 0.53 m tall, 0.38 m out."""
    m = K.Mesh(name)
    m.lathe([(0, 0), (.1, 0), (.1, .014), (.085, .026), (.05, .034), (0, .034)], (0, 0, -.08), metal, 20, axis="x")
    m.torus((.026, 0, -.08), .085, .008, metal, 20, 6, axis="x")
    m.tube(K.bez((.03, 0, -.08), (.14, 0, -.12), (.25, 0, -.12), (.26, 0, .0), 12), .021, metal, 10)
    m.sphere((.09, 0, -.1), .026, metal, 10, 6)
    m.lathe([(0, -.03), (.02, -.03), (.03, 0), (.06, .02), (.065, .035), (.05, .04), (0, .04)], (.26, 0, 0), metal, 16)
    m.sphere((.26, 0, .15), .115, glass, 20, 12)
    m.lathe([(0, 0), (.045, 0), (.05, .02), (.03, .03), (.032, .05), (.018, .06), (.02, .08), (0, .1)],
            (.26, 0, .255), metal, 14)
    return m.finish(col), [K.empty(col, "LIGHT_0", (.26, 0, .15))]


def iron_lantern(col, name):
    """Wrought-iron wall lantern (sheet #5): a bracket of a back bar, an arm and scroll curls, a hook, and a
    hexagonal empty cage with clear panes, a pointed cap and a drop. Arm 0.5 m out, 0.83 m tall overall."""
    m = K.Mesh(name)
    m.box(0, -.035, -.3, .03, .035, .12, "IronDark", .008)
    for z in (-.25, .07):
        m.box(.03, -.012, z - .012, .045, .012, z + .012, "IronDark", .004)   # rivet heads
    m.box(.03, -.018, .02, .5, .018, .06, "IronDark", .006)
    m.tube(K.spiral((.17, 0, -.07), .01, .085, 1.15, "xz", a0=math.pi * .5, flip=1, n=26), .011, "IronDark", 6)
    m.tube(K.spiral((.36, 0, -.01), .006, .045, 1.0, "xz", a0=math.pi * .5, flip=-1, n=20), .01, "IronDark", 6)
    m.tube(K.bez((.04, 0, -.25), (.1, 0, -.2), (.15, 0, -.16), (.17, 0, -.155), 8), .011, "IronDark", 6)
    m.tube(K.spiral((.5, 0, .02), .005, .022, .8, "xz", a0=0, flip=-1, n=10), .008, "IronDark", 6)
    cx, top = .42, -.07
    m.torus((cx, 0, top + .015), .025, .007, "IronDark", 12, 6, axis="y")
    m.tube([(cx, 0, top - .005), (cx, 0, -.0)], .007, "IronDark", 6)
    m.lathe([(0, 0), (.13, 0), (.15, .02), (.13, .035), (0, .2)], (cx, 0, top - .21), "IronDark", 6)
    body_top, body_bot = top - .21, top - .5
    for i in range(6):
        a = 2 * math.pi * (i + .5) / 6
        m.box(cx + .11 * math.cos(a) - .012, .11 * math.sin(a) - .012, body_bot, cx + .11 * math.cos(a) + .012,
              .11 * math.sin(a) + .012, body_top, "IronDark", .004)
    m.lathe([(.1, 0), (.1, .29)], (cx, 0, body_bot), "GlassClear", 6)
    m.torus((cx, 0, body_bot + .145), .11, .006, "IronDark", 6, 4)
    m.lathe([(0, 0), (.06, 0), (.13, .03), (.13, .055), (0, .06)], (cx, 0, body_bot - .05), "IronDark", 6)
    m.lathe([(0, 0), (.03, 0), (.04, .03), (0, .05)], (cx, 0, body_bot - .1), "IronDark", 6)
    m.sphere((cx, 0, body_bot - .12), .022, "IronDark", 8, 6)
    return m.finish(col), [K.empty(col, "LIGHT_0", (cx, 0, (body_top + body_bot) * .5))]


def reading_lamp(col, name):
    """Brass gooseneck reading lamp (sheet #6): round back plate with four rivets, an arm up and over, a deep brown
    dome shade with a brass rim, mouth down. 0.32 m tall, 0.58 m out."""
    m = K.Mesh(name)
    m.lathe([(0, 0), (.08, 0), (.08, .012), (.065, .024), (.035, .03), (0, .03)], (0, 0, 0), "Brass", 18, axis="x")
    for i in range(4):
        a = 2 * math.pi * (i + .5) / 4
        m.sphere((.014, .058 * math.cos(a), .058 * math.sin(a)), .009, "Brass", 6, 4)
    arm = K.bez((.03, 0, 0), (.2, 0, 0), (.22, 0, .2), (.34, 0, .19), 10) + \
        K.bez((.34, 0, .19), (.42, 0, .185), (.42, 0, .14), (.42, 0, .1), 6)[1:]
    m.tube(arm, .019, "Brass", 10)
    m.lathe([(0, .0), (.03, 0), (.03, .03), (0, .03)], (.42, 0, .1), "Brass", 12)
    m.lathe([(.16, -.2), (.155, -.17), (.13, -.08), (.08, -.02), (.035, 0), (0, .005)], (.42, 0, .1), "ShadeBrown", 20)
    m.torus((.42, 0, -.1), .16, .012, "Brass", 24, 6)
    return m.finish(col), [K.empty(col, "LIGHT_0", (.42, 0, -.08))]


ALL = [("Sconce", sconce3), ("Chandelier", chandelier), ("Portrait", portrait), ("GlobeSconce", globe_sconce),
       ("Lantern", iron_lantern), ("ReadingLamp", reading_lamp)]
