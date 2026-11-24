# CR03 · the hall-porch corridor (2026-11-22): leaves the first gate's mini-game for the Grand Stair Hall's porch.
# Concept: Docs/PawnRush/ConceptArt/SectionCorridors/03_hallporch.png, ornaments AssetRefs/CR03_assets_hallporch.png
# (all modelled here). Materials: the hall's (PR_PalaceStone, PR_StatueMarble, PR_Travertine, PR_CalacattaOro - world
# space stone, no UV needed) plus a hall gold and a few plain ones of this kit.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr03_hallporch.py -- [--export] [--render]
#
# Ends: the mini-game's exit at y 0 (the walls start at 0.65, a header closes the vault's end up to 10.5 over the
# mini-game's arch), the hall's porch at y 10 (it has its own arch: no gate frame; the walls run to the end).
# Piers at 1.1, 3.817, 6.533, 9.55 - a bay's clear span 1.917 (authored 1.9).
# Indoors under a coffered segmental vault (springing 6.4 on the cornice, crown 7.6; the bodies' lid is at 7.5 and the
# early corridor's lid and upper fence let the camera through). West wall, inner face x -6.30:
#   0 .. 1.06 marble dado with a gold rail; 1.06 .. 5.55 limestone; a frieze and a cornice to 6.4
#   Bay (the end bays): an arched niche (1.1 wide, sill 1.25, springing 4.4) with a crown keystone, a banner (code)
#   BayMid (the middle bay): a tall arched window with gold glazing bars
#   Pier: a marble pilaster on a travertine base, gold panel lines, a gilded acanthus capital, a two-arm sconce
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bpy
from mathutils import Matrix as Mx
import cr13_common as C
import cr13_ornaments as O
import cr13_details as D

ID = "CR03_HallPorch"
HERE = os.path.dirname(os.path.abspath(__file__))
LIME, MARBLE, TRAV, FLOORM = "PR_PalaceStone", "PR_StatueMarble", "PR_Travertine", "PR_CalacattaOro"
GOLD, GREY, NICHE, GLASS, RED = "PR_Cor13_HallGold", "PR_Cor13_DadoMarble", "PR_Cor13_NicheBlue", "PR_Cor13_FrostGlass", "GC_velvet_crimson"
C.new_material(GOLD, (.86, .64, .28), .18, .75)
C.new_material(GREY, (.60, .60, .62), .35, 0.)
C.new_material(NICHE, (.36, .43, .56), .6, 0.)
C.new_material(GLASS, (.88, .93, .98), .12, 0.)
C.new_material("PR_Cor13_Wax", (.96, .93, .86), .5, 0.)

FACE, OUTER = -6.30, -7.0
SPRING_V, CROWN_V = 6.4, 7.6           # the vault
HALF_V = 6.3
SPAN = C.AUTHORED_SPAN
HB = SPAN / 2 + .05
OPEN_A, SILL, SPRING = .55, 1.25, 4.4  # niche / window


def vault_arc(n=16, inset=0.0):
    rise = CROWN_V - SPRING_V
    R = (HALF_V ** 2 + rise ** 2) / (2 * rise)
    cz = CROWN_V - R
    a0 = math.asin(HALF_V / R)
    pts = []
    for i in range(n + 1):
        a = -a0 + 2 * a0 * i / n
        pts.append(((R - inset) * math.sin(a), cz + (R - inset) * math.cos(a), a))
    return pts, R, cz


def wall_common(m):
    """Dado, limestone, frieze, cornice and the wall's core for a bay (the opening is cut by the caller's pieces)."""
    m.box(FACE - .3, OUTER + .02, -HB + .01, HB - .01, 0, SPRING_V - .01, LIME, bevel=0)              # core and outer face (under the cornice)
    m.box(FACE + .08, FACE - .05, -HB, HB, 0, .18, TRAV, bevel=.015)                        # skirting
    m.box(FACE + .04, FACE - .05, -HB, HB, .18, 1.0, GREY, bevel=.01)                       # dado
    m.box(FACE + .065, FACE - .05, -HB, HB, 1.0, 1.06, GOLD, bevel=.008)                    # dado rail
    for yy in (-.48, .48):                                                                  # dado panels' gold lines
        for z0, z1, y0, y1 in ((.3, .33, yy - .36, yy + .36), (.86, .89, yy - .36, yy + .36),
                               (.33, .86, yy - .36, yy - .33), (.33, .86, yy + .33, yy + .36)):
            m.box(FACE + .055, FACE + .03, y0, y1, z0, z1, GOLD, bevel=0)
    m.box(FACE + .02, FACE - .05, -HB, HB, 5.55, 5.61, GOLD, bevel=.006)                    # frieze
    m.box(FACE + .01, FACE - .05, -HB, HB, 5.61, 5.92, TRAV, bevel=.01)
    for k in range(5):
        y = -SPAN / 2 + SPAN * (k + .5) / 5
        m.blob((FACE + .025, y, 5.765), .045, GOLD, subdiv=1)
    m.prism([(FACE + .02, 5.92), (FACE + .12, 6.02), (FACE + .2, 6.18), (FACE + .25, 6.3), (FACE + .25, 6.4),
             (OUTER - .05, 6.4), (OUTER - .05, 5.92)], "y", -HB, HB, LIME, bevel=.008)
    m.box(FACE + .26, FACE + .19, -HB, HB, 6.3, 6.36, GOLD, bevel=.005)


def opening_frame(m, keystone=True):
    """The marble frame and gold architrave round the arched opening, with a crown keystone (sheet item 4)."""
    a, b = OPEN_A, OPEN_A + .14
    inner = [(a, SILL)] + [(a * math.cos(math.pi * i / 14), SPRING + a * math.sin(math.pi * i / 14)) for i in range(15)] + [(-a, SILL)]
    outer = [(b, SILL - .1)] + [(b * math.cos(math.pi * i / 14), SPRING + b * math.sin(math.pi * i / 14)) for i in range(15)] + [(-b, SILL - .1)]
    for i in range(len(inner) - 1):
        m.prism([inner[i], outer[i], outer[i + 1], inner[i + 1]], "x", FACE + .05, FACE - .26, MARBLE, bevel=0)
    gi = [(b, SILL - .1)] + [(b * math.cos(math.pi * i / 14), SPRING + b * math.sin(math.pi * i / 14)) for i in range(15)] + [(-b, SILL - .1)]
    go = [(b + .05, SILL - .1)] + [((b + .05) * math.cos(math.pi * i / 14), SPRING + (b + .05) * math.sin(math.pi * i / 14)) for i in range(15)] + [(-b - .05, SILL - .1)]
    for i in range(len(gi) - 1):
        m.prism([gi[i], go[i], go[i + 1], gi[i + 1]], "x", FACE + .07, FACE, GOLD, bevel=0)
    # the limestone round the frame (left, right, over the arch up to the frieze)
    for s in (-1, 1):
        m.box(FACE + .005, FACE - .05, s * (b + .05), s * HB, 1.06, 5.55, LIME, bevel=0)
        arc = [p for p in go[1:-1] if p[0] * s >= -1e-6]
        arc.sort(key=lambda p: -abs(p[0]))
        m.prism([(s * (b + .05), SPRING), (s * (b + .05), 5.55), (0, 5.55)] + list(reversed(arc)), "x", FACE + .005, FACE - .05, LIME, bevel=0)
    m.box(FACE + .04, FACE - .05, -b, b, 1.06, SILL - .1, LIME, bevel=0)
    m.box(FACE + .09, FACE - .05, -b - .08, b + .08, SILL - .1, SILL, MARBLE, bevel=.015)    # sill
    if keystone:
        top = SPRING + b + .05
        m.prism([(-.17, top - .2), (.17, top - .2), (.22, top + .38), (-.22, top + .38)], "x", FACE + .1, FACE - .05, MARBLE, bevel=.01)
        # gold scroll frame and crown on its face
        scroll = []
        for i in range(9):
            t = i / 8
            scroll.append((FACE + .11, .16 * math.cos(math.pi * t) * (1.1 - .3 * t), top + .1 + .2 * math.sin(math.pi * t)))
        m.tube(scroll, .018, GOLD, segs=6)
        crown = [(-.13, 0), (.13, 0), (.15, .09), (.09, .05), (.05, .12), (0, .06), (-.05, .12), (-.09, .05), (-.15, .09)]
        m.prism([(u, top + .02 + v) for u, v in crown], "x", FACE + .1, FACE + .13, GOLD, bevel=0)
    return inner


def build_bay(coll, mid):
    m = C.Mesh(ID + ("_BayMid" if mid else "_Bay"))
    wall_common(m)
    inner = opening_frame(m, keystone=True)
    # gilded garlands from the capitals to the keystone, either side
    for s in (-1, 1):
        D.garland(m, GOLD, [GOLD], GOLD, GOLD, (FACE + .07, s * (SPAN / 2 - .04), 5.32), (FACE + .07, s * .26, 5.28), .16,
                  seed=40 + int(s), roses=1)
    depth = FACE - .26
    if not mid:
        # the niche: a blue back under a shell-less half dome (a simple quarter-sphere approximated by rings)
        m.prism(inner, "x", depth - .02, depth, NICHE, bevel=0)
        # a gilt urn of white and pink hydrangeas on the niche's sill, under the banner
        uz = SILL + .02
        m.lathe([(.1, 0), (.12, .03), (.07, .07), (.06, .12), (.14, .2), (.2, .3), (.21, .38), (.18, .4)], (depth + .26, 0, uz), GOLD, segs=16, cap_top=False)
        m.lathe([(.17, uz + .37)], (depth + .26, 0, 0), "GC_planter_soil", segs=16, cap_bottom=False)
        import random as _r
        rr = _r.Random(3)
        for i in range(9):
            a = 2 * math.pi * i / 9
            px, py = depth + .26 + .14 * math.cos(a), .14 * math.sin(a)
            m.blob((px, py, uz + .46 + rr.uniform(0, .08)), .09, "GC_flower_white" if i % 3 else "GC_flower_pink", subdiv=2, noise=.18, seed=i)
            D.leaf2(m, "GC_leaf", (px, py, uz + .4), (math.cos(a), math.sin(a), -.3), length=.14, width=.06)
        m.blob((depth + .26, 0, uz + .56), .1, "GC_flower_white", subdiv=2, noise=.18, seed=20)
        ob = m.finish(coll, (-6.0, 0, 0))
        C.empty(coll, "BANNER", (depth + .03, 0, SPRING + .2), parent=ob)
        return ob
    # the window: frosted glass, a gold frame, glazing bars and a transom at the springing
    m.prism(inner, "x", depth - .02, depth, GLASS, bevel=0)
    for yb in (-.18, .18):
        h = SPRING + math.sqrt(OPEN_A ** 2 - yb ** 2) - .02
        m.box(depth + .05, depth, yb - .018, yb + .018, SILL, h, GOLD, bevel=0)
    m.box(depth + .05, depth, -OPEN_A, OPEN_A, SPRING - .02, SPRING + .02, GOLD, bevel=0)
    for zz in (2.05, 2.85, 3.62):
        m.box(depth + .05, depth, -OPEN_A, OPEN_A, zz - .015, zz + .015, GOLD, bevel=0)
    for k in range(1, 6):
        a = math.pi * k / 6
        p = [(depth + .04, .05 * math.cos(a), SPRING + .05 * math.sin(a)), (depth + .04, (OPEN_A - .02) * math.cos(a), SPRING + (OPEN_A - .02) * math.sin(a))]
        m.tube(p, .014, GOLD, segs=5)
    return m.finish(coll, (-6.0, 0, 0))


def acanthus(m, x_face, y, z0, h, w):
    """A gilded acanthus leaf (sheet item 3) on a capital's face, toward +x."""
    pts = [(0, 0)]
    for i in range(1, 8):
        t = i / 7
        lobe = abs(math.sin(t * math.pi * 3.5))
        pts.append((w * .5 * (1 - t) ** .55 * (.72 + .28 * lobe), h * t))
    pts.append((0, h * 1.05))
    left = [(-u, v) for u, v in reversed(pts[1:-1])]
    poly = [(y + u, z0 + v) for u, v in pts + left]
    m.prism(poly, "x", x_face, x_face + .04, GOLD, bevel=0)
    tip = [(y - .02, z0 + h * .55), (y + .02, z0 + h * .55), (y + .05, z0 + h * 1.1), (y, z0 + h * 1.2), (y - .05, z0 + h * 1.1)]
    m.prism(tip, "x", x_face + .03, x_face + .06, GOLD, bevel=0)


def sconce(m, x_face, y, z):
    """The two-arm sconce (sheet item 1): an ornate back plate with a fleur, two scrolled arms, empty cups.
    Returns the cups' tops (code lights the candles)."""
    plate = []
    for i in range(17):
        t = i / 16
        a = math.pi * t
        r = .16 + .05 * math.sin(3 * a) ** 2
        plate.append((y + r * .7 * math.sin(a), z - .26 + .55 * t + .04 * math.sin(2 * a)))
    plate = [(y, z - .3)] + plate[1:-1] + [(y, z + .32)]
    plate = plate + [(2 * y - u, v) for u, v in reversed(plate[1:-1])]
    m.prism(plate, "x", x_face, x_face + .035, GOLD, bevel=0)
    O.fleur(m, GOLD, .16, "x", (y, z + .02), x_face + .035, x_face + .055)
    m.blob((x_face + .05, y, z - .12), .045, GOLD, subdiv=1)
    cups = []
    for s in (-1, 1):
        arm = []
        for i in range(9):
            t = i / 8
            arm.append((x_face + .04 + .16 * t, y + s * (.05 + .19 * math.sin(math.pi * .5 * t)), z - .12 + .22 * t ** 2 - .06 * math.sin(math.pi * t)))
        m.tube(arm, .017, GOLD, segs=6)
        cx, cy, cz = arm[-1]
        m.lathe([(.022, 0), (.03, .02), (.052, .07), (.056, .11), (.046, .11)], (cx, cy, cz), GOLD, segs=10, cap_top=False)
        cups.append(D.candle(m, "PR_Cor13_Wax", cx, cy, cz + .06, h=.17, r=.02))
    return cups


def build_pier(coll):
    m = C.Mesh(ID + "_Pier")
    m.box(-6.0, -6.62, -.45, .45, 0, .55, TRAV, bevel=.025)                                 # base
    m.box(-6.03, -6.6, -.42, .42, .55, .64, MARBLE, bevel=.015)
    m.box(-6.08, -6.56, -.36, .36, .64, 5.22, MARBLE, bevel=.02)                            # shaft
    for z0, z1, y0, y1 in ((.8, .83, -.26, .26), (5.0, 5.03, -.26, .26), (.83, 5.0, -.26, -.23), (.83, 5.0, .23, .26)):
        m.box(-6.07, -6.1, y0, y1, z0, z1, GOLD, bevel=0)                                   # panel lines
    # capital (sheet item 3): a marble block widening upward, gilded leaves, a small fleur, an abacus
    m.prism([(-.36, 5.22), (.36, 5.22), (.43, 5.86), (-.43, 5.86)], "x", -6.06, -6.58, MARBLE, bevel=.01)
    m.box(-6.0, -6.62, -.46, .46, 5.86, 5.97, MARBLE, bevel=.015)
    m.box(-6.02, -6.6, -.42, .42, 5.25, 5.3, GOLD, bevel=.005)
    for yy, hh in ((-.2, .5), (.2, .5), (-.36, .42), (.36, .42)):
        acanthus(m, -6.075 + (.02 if abs(yy) > .3 else 0), yy, 5.31, hh, .2)
    O.fleur(m, GOLD, .14, "x", (0, 5.62), -6.07, -6.045)
    # the cornice carried over the pier (the bays' cornices stop at its faces): 3 mm proud of theirs, a hair lower on
    # top, so where they meet no two faces share a plane
    d = .003
    m.prism([(FACE + .02 + d, 5.92), (FACE + .12 + d, 6.02), (FACE + .2 + d, 6.18), (FACE + .25 + d, 6.3), (FACE + .25 + d, 6.397),
             (OUTER - .05 - d, 6.397), (OUTER - .05 - d, 5.92)], "y", -.42, .42, LIME, bevel=.008)
    m.box(FACE + .26 + d, FACE + .19, -.418, .418, 6.298, 6.362, GOLD, bevel=.005)
    # the wall behind the pilaster, closing the gap between the bays' cores (3 mm off their faces)
    m.box(FACE - .3 - d, OUTER + .02 - d, -.415, .415, 0, SPRING_V - .013, LIME, bevel=0)
    # the sconce on the shaft's front
    cups = sconce(m, -6.08, 0, 3.2)
    ob = m.finish(coll, (-6.0, 0, 0))
    for i, c in enumerate(cups):
        C.empty(coll, "FLAME_%d" % i, c, parent=ob)
    return ob


def build_ceiling_bay(coll):
    """The vault between two ribs: a shell, two rows of coffers across it with gold frames and fleur rosettes (sheet
    item 2). Authored on y -0.95 .. 0.95 (scaled with the bays); one light in its middle."""
    m = C.Mesh(ID + "_CeilingBay")
    arc, R, cz = vault_arc(16)
    for i in range(16):
        x0, z0, _ = arc[i]
        x1, z1, _ = arc[i + 1]
        # the shell: 0.15 thick, from the arc up
        m.prism([(x0, z0), (x1, z1), (x1, z1 + .15), (x0, z0 + .15)], "y", -HB, HB, LIME, bevel=0)
    # coffers: 8 across (two shell segments each), 2 along
    ncol, nrow = 8, 2
    for c in range(ncol):
        a0 = arc[2 * c][2]
        a1 = arc[2 * c + 2][2]
        am = (a0 + a1) / 2
        cell_w = R * (a1 - a0)
        for r in range(nrow):
            yc = -SPAN / 2 + SPAN * (r + .5) / nrow
            cell_l = SPAN / nrow
            before = set(m.bm.faces)
            # local: x across the cell, y along, z down from the shell (negative = into the room)
            w, l = cell_w / 2 - .05, cell_l / 2 - .05
            # the frame's side bars stop at the end bars and stand 2 mm lower, so no two faces share a plane
            for x0, x1, y0, y1, d in ((-w, w, -l, -l + .07, -.1), (-w, w, l - .07, l, -.1), (-w, -w + .07, -l + .07, l - .07, -.098),
                                      (w - .07, w, -l + .07, l - .07, -.098)):
                m.box(x0, x1, y0, y1, d, -.002, GOLD, bevel=0)
            for x0, x1, y0, y1 in ((-w + .07, w - .07, -l + .07, -l + .14), (-w + .07, w - .07, l - .14, l - .07),
                                   (-w + .07, -w + .14, -l + .14, l - .14), (w - .14, w - .07, -l + .14, l - .14)):
                m.box(x0, x1, y0, y1, -.06, -.002, LIME, bevel=0)
            m.box(-.17, .17, -.17, .17, -.05, -.002, MARBLE, bevel=0)
            for k, poly in enumerate(O.fleur_polygons()):
                pts = [(u * .3, (v - .55) * .3) for u, v in poly]
                m.prism(pts, "z", -.09 - .0015 * k, -.051 - .0015 * k, GOLD, bevel=0)   # staggered: no shared planes
            m.transform_new(before, Mx.Translation((R * math.sin(am), yc, cz + R * math.cos(am))) @ Mx.Rotation(am, 4, "Y"))
    ob = m.finish(coll, (0, 0, 0))
    C.empty(coll, "LIGHT_0", (0, 0, 5.6), parent=ob)
    return ob


def build_ceiling_beam(coll):
    """A transverse rib over a pier line (as deep as the pier, 0.9, so the vault closes over the end piers): a band hanging 0.18 below the vault, gold edges."""
    m = C.Mesh(ID + "_CeilingBeam")
    arc, R, cz = vault_arc(16, inset=0)
    inner, _, _ = vault_arc(16, inset=.18)
    for i in range(16):
        q = [(arc[i][0], arc[i][1] + .02), (arc[i + 1][0], arc[i + 1][1] + .02), (inner[i + 1][0], inner[i + 1][1]), (inner[i][0], inner[i][1])]
        m.prism(q, "y", -.45, .45, MARBLE, bevel=0)
        for s in (-1, 1):
            g = [(inner[i][0], inner[i][1] + .03), (inner[i + 1][0], inner[i + 1][1] + .03), (inner[i + 1][0], inner[i + 1][1] - .02), (inner[i][0], inner[i][1] - .02)]
            m.prism(g, "y", s * .45, s * .47, GOLD, bevel=0)
    m.box(-.35, .35, -.28, .28, CROWN_V - .3, CROWN_V - .17, GOLD, bevel=.01)           # the crown boss
    return m.finish(coll, (0, 0, 0))


def build_header(coll):
    """Closes the vault's end over the mini-game's exit arch: limestone from the vault up to 10.5, a gold edge on
    the arc. Authored at the line y 0 on the corridor's side (y 0 .. 0.3)."""
    m = C.Mesh(ID + "_Header")
    arc, R, cz = vault_arc(16)
    pts = [(-6.6, SPRING_V), (-6.6, 10.5), (6.6, 10.5), (6.6, SPRING_V)] + [(x, z) for x, z, _ in reversed(arc)][::1]
    pts = [(-6.6, SPRING_V), (-6.6, 10.5), (6.6, 10.5), (6.6, SPRING_V)] + [(x, z) for x, z, _ in arc][::-1]
    pts = [(-6.6, SPRING_V), (-6.6, 10.5), (6.6, 10.5), (6.6, SPRING_V), (HALF_V, SPRING_V)] + [(x, z) for x, z, _ in arc][::-1][1:-1] + [(-HALF_V, SPRING_V)]
    m.prism(pts, "y", -.012, .3, LIME, bevel=0)   # 1.2 cm before the first rib's end face (no shared plane)
    inner, _, _ = vault_arc(16, inset=.12)
    for i in range(16):
        q = [(arc[i][0], arc[i][1]), (arc[i + 1][0], arc[i + 1][1]), (inner[i + 1][0], inner[i + 1][1]), (inner[i][0], inner[i][1])]
        m.prism(q, "y", .3, .36, GOLD, bevel=0)
    return m.finish(coll, (0, 0, 0))


def build_floor(coll):
    """Calacatta squares with gold diamond insets, a crimson runner with gold borders; top +0.024 from the mini-game's
    paving, down to +0.004 over the last 0.6 m (the hall's porch floor is at 0)."""
    m = C.Mesh(ID + "_Floor")
    def top(y):
        return .024 - .02 * max(0, (y - 9.4) / .6)
    m.box(-6.3, 6.3, 0, 10, -.12, -.01, TRAV, bevel=0)
    xs = [-6.3, -5.25, -4.2, -3.15, -2.1, -1.4]
    for s in (-1, 1):
        for xa, xb in zip(xs[:-1], xs[1:]):
            for r in range(10):
                ya, yb = r * 1.0, r * 1.0 + 1.0
                m.prism([(ya + .006, -.06), (yb - .006, -.06), (yb - .006, top(yb)), (ya + .006, top(ya))], "x",
                        (xa + .006) if s < 0 else -(xb - .006), (xb - .006) if s < 0 else -(xa + .006), FLOORM, bevel=0)
        for xd in xs[1:-1]:
            for r in range(1, 10):
                cx, cy = s * xd, r * 1.0
                d = .09
                m.prism([(cx - d, cy), (cx, cy - d), (cx + d, cy), (cx, cy + d)], "z", -.02, top(cy) + .003, GOLD, bevel=0)
    # runner |x| < 1.4: gold borders, crimson field
    m.prism([(.004, -.06), (9.996, -.06), (9.996, top(10) + .012), (9.4, .036), (.004, .036)], "x", -1.2, 1.2, RED, bevel=0)
    for s in (-1, 1):
        m.prism([(.002, -.06), (9.998, -.06), (9.998, top(10) + .014), (9.4, .038), (.002, .038)], "x", s * 1.2, s * 1.38, GOLD, bevel=0)
        m.prism([(.002, -.06), (9.998, -.06), (9.998, top(10) + .015), (9.4, .039), (.002, .039)], "x", s * 1.0, s * 1.06, GOLD, bevel=0)
    return m.finish(coll, (0, 0, 0))


def main():
    C.reset()
    coll = C.collection(ID)
    bay, mid, pier = build_bay(coll, False), build_bay(coll, True), build_pier(coll)
    cbay, beam, header, floor = build_ceiling_bay(coll), build_ceiling_beam(coll), build_header(coll), build_floor(coll)
    mods = (bay, mid, pier, cbay, beam, header, floor)
    for ob in mods:
        print("%-24s %7d tris" % (ob.name, C.tris(ob)))
    frm, to = .65, 10.0
    ps = C.piers(frm, to)
    span = C.bay_span(frm, to)
    total = 2 * (2 * C.tris(bay) + C.tris(mid) + 4 * C.tris(pier)) + 3 * C.tris(cbay) + 4 * C.tris(beam) + C.tris(header) + C.tris(floor)
    print("corridor total %d tris (span %.3f)" % (total, span))
    if "--export" in C.ARGS:
        out = C.OUT_REL + "/" + ID + "/CR03_"
        kids = lambda o: [o] + [k for k in coll.objects if k.parent == o]
        C.export(kids(bay), out + "Bay.fbx")
        C.export(kids(mid), out + "BayMid.fbx")
        C.export(kids(pier), out + "Pier.fbx")
        C.export(kids(cbay), out + "CeilingBay.fbx")
        C.export(kids(beam), out + "CeilingBeam.fbx")
        C.export(kids(header), out + "Header.fbx")
        C.export(kids(floor), out + "Floor.fbx")
        C.write_layout(ID, 7.6, frm, to, ["Bay", "BayMid", "Pier", "CeilingBay", "CeilingBeam", "Header", "Floor"], ceiling_absolute=True)
    if "--render" in C.ARGS:
        for ob in mods:
            ob.hide_render = True
        sc = span / C.AUTHORED_SPAN
        for side in (-1, 1):
            for p in ps:
                C.place(pier, side * 6.0, p, side)
            for i in range(3):
                C.place(mid if i == 1 else bay, side * 6.0, (ps[i] + ps[i + 1]) / 2, side, sc)
        for i in range(3):
            C.place(cbay, 0, (ps[i] + ps[i + 1]) / 2, -1, sc)
        for p in ps:
            C.place(beam, 0, p)
        C.place(header, 0, frm)
        C.place(floor, 0, 0)
        ctx = C.Mesh("context")
        ctx.box(-14, 14, -14, 0, -.2, .024, "PR_Early_Paving", bevel=0)
        ctx.box(-14, -6.6, -14, .65, 0, 9, "PR_Early_WallCore", bevel=0)
        ctx.box(6.6, 14, -14, .65, 0, 9, "PR_Early_WallCore", bevel=0)
        ctx.box(-14, 14, 10, 30, -.2, 0, FLOORM, bevel=0)
        ctx.finish(bpy.context.scene.collection)
        for x in (-1.2, 0, 1.2):
            C.pawn(bpy.context.scene.collection, x, 1.6, "GC_royal_blue" if x else "GC_pennant_red")
        C.render_setup(sun=True, strength=.6)
        for i in range(3):
            l = bpy.data.lights.new("bay%d" % i, "POINT")
            l.energy = 300
            l.color = (1, .82, .6)
            lo = bpy.data.objects.new("bay%d" % i, l)
            lo.location = (0, (ps[i] + ps[i + 1]) / 2, 5.6)
            bpy.context.scene.collection.objects.link(lo)
        prev = os.path.join(HERE, "previews")
        os.makedirs(prev, exist_ok=True)
        C.shoot(os.path.join(prev, "cr03_concept_view.png"), (0, -1.6, 2.6), (0, 10, 3.0), 20)
        C.shoot(os.path.join(prev, "cr03_wall_close.png"), (3.0, 2.6, 2.4), (-6.2, 5.6, 3.3), 24)
        C.shoot(os.path.join(prev, "cr03_ceiling.png"), (0, 1.2, 2.0), (0, 6, 7.4), 20)
        C.shoot(os.path.join(prev, "cr03_back.png"), (0, 9.4, 2.4), (0, 0, 4.5), 22)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "cr03_hallporch.blend"))


main()
