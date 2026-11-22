# CR05 - the king's antechamber gallery (grand stair hall -> mini-game 2 rooms G / H / I), after the GPT concept
# Docs/PawnRush/ConceptArt/SectionCorridors/05_kingsantechamber.png: cream marble, massive piers with brass bands,
# tall round-headed windows with tied purple velvet curtains and tassels, a marble wainscot, a long skylight between
# marble beams (no chandeliers - stage 3), globe sconces, a purple runner with brass edges. Height 7.5. Starts at the
# hall's exit (Portal), ends 0.8 m before the mini-game's entry (Header).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr47_05_kingsantechamber.py -- [--render] [--export]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import cr_kit_04_07 as K
import cr47_parts as P
import cr47_ornaments as O
import cr47_run as R

H = 7.5
SPEC = {
    "id": "CR05_KingsAntechamber", "height": H, "z_from": 0.0, "z_to": 10 - .8,
    "portal_start": True, "portal_end": False, "header_start": False, "header_end": True,
    "banner": {"width": 1.1, "height": 3.6, "fabric": "PR_Cor_VelvetPurple"},
    "layout": [
        {"name": "CR05_GlobeSconce", "per": "pier", "index": [0, 1, 2, 3], "offset": [0.0, 3.3, 0.0]},
    ],
}
FACE, BACK, MASS = -.38, -.62, -.72


def outer_face(m, y0, y1):
    m.box(MASS - .06, y0, 0, MASS, y1, H, "MarbleCream", 0)
    m.box(MASS - .14, y0, 0, MASS - .06, y1, .7, "MarbleShade", .02)
    m.box(MASS - .2, y0, H - .05, MASS - .06, y1, H + .4, "MarbleShade", .03)
    m.box(MASS - .21, y0, H + .1, MASS - .19, y1, H + .16, "Brass", 0)
    m.box(-.7, y0, H + .3, -.38, y1, H + .9, "MarbleCream", .02)
    m.box(-.74, y0, H + .9, -.34, y1, H + 1.0, "MarbleShade", .02)


def entablature(m, y0, y1):
    P.cornice(m, y0, y1, FACE, H - .9, [(.14, .06, "MarbleShade"), (.05, .09, "Brass"), (.2, .12, "MarbleCream"),
                                         (.08, .18, "MarbleShade"), (.08, .24, "Brass"), (.35, .32, "MarbleCream")], "MarbleCream")


def bay(col, name, mid=False):
    m = K.Mesh(name)
    y0, y1 = -.985, .985        # ends 1.5 cm short, inside the piers (no shared planes with them)
    if mid:
        # Where the code hangs its purple banner: a recessed panel with a brass frame.
        m.box(-.4, y0, 0, FACE, -.75, H, "MarbleCream", 0)
        m.box(-.4, .75, 0, FACE, y1, H, "MarbleCream", 0)
        m.box(-.4, -.75, 0, FACE, .75, 2.2, "MarbleCream", 0)
        m.box(-.4, -.75, 6.4, FACE, .75, H, "MarbleCream", 0)
        m.box(-.42, -.75, 2.2, -.4, .75, 6.4, "MarbleShade", 0)
        m.ring([(-.82, 2.13), (.82, 2.13), (.82, 6.47), (-.82, 6.47)], [(-.75, 2.2), (.75, 2.2), (.75, 6.4), (-.75, 6.4)],
               "x", FACE - .01, FACE + .05, "Brass")
        m.tube([(-.3, -.7, 6.25), (-.3, .7, 6.25)], .022, "Brass", 8)
        for s in (-1, 1):
            m.sphere((-.3, s * .72, 6.25), .045, "Brass", 8, 6)
    else:
        K.wall_with_arch(m, y0, y1, 0, H, 0, 1.05, 3.0, 5.55, FACE, BACK, "MarbleCream")
        P.window(m, 0, 1.05, 3.0, 5.55, FACE, BACK, "MarbleCream", "Brass", "Glass", "Brass", .04, keystone=False, bars=(2, 3))
        P.voussoirs(m, 0, 1.05, 3.0, 5.55, .2, FACE, FACE + .08, "MarbleShade", "Brass", n=9)
        m.box(FACE - .02, -.8, 2.88, FACE + .16, .8, 3.0, "MarbleShade", .02)
        for s in (-1, 1):
            P.stack(m, FACE, s * .55, .07, FACE, [(2.55, 2.68, 0, "MarbleCream"), (2.68, 2.8, .06, None), (2.8, 2.88, .12, None)],
                    "MarbleCream")
        m.ring([(-.75, 1.95), (.75, 1.95), (.75, 2.45), (-.75, 2.45)], [(-.65, 2.03), (.65, 2.03), (.65, 2.37), (-.65, 2.37)],
               "x", FACE, FACE + .05, "MarbleShade")
        m.tube([(-.13, -.92, 6.55), (-.13, .92, 6.55)], .022, "Brass", 8)
        for s in (-1, 1):
            m.sphere((-.13, s * .94, 6.55), .04, "Brass", 8, 6)
            P.curtain(m, s * .67, 6.52, 4.2, 2.45, -.3, -.08, "VelvetPurple", folds=5, spread=.2)
    m.box(BACK - .001, y0, 0, MASS, y1, H, "MarbleCream", 0)
    m.box(FACE, y0, 0, FACE + .12, y1, 1.65, "MarbleCream", 0)
    P.wainscot(m, y0, y1, FACE + .12, 1.65, "MarbleShade", "MarbleCream", "MarbleShade", "MarbleShade", panels=2, fillet_key="Brass")
    entablature(m, y0, y1)
    outer_face(m, y0, y1)
    ob = m.finish(col)
    # The code hangs a banner from this point (the rod) in the banner bay.
    return ob, ([K.empty(col, "BANNER", (-.3, 0, 6.22))] if mid else [])


def pier(col, name):
    """A massive marble pier standing 0.38 m proud: a stepped base, brass bands wrapping it, a stepped capital."""
    m = K.Mesh(name)
    m.box(MASS, -.46, 0, FACE, .46, H, "MarbleCream", 0)
    P.stack(m, FACE, 0, .42, -.04, [(0, .3, .08, "MarbleShade"), (.3, .45, .05, "MarbleCream"), (.45, .55, .02, "Brass"),
                                     (.55, .7, .0, "MarbleShade")], "MarbleShade")
    m.box(FACE, -.42, .7, -.04, .42, H - 1.45, "MarbleCream", .04)            # shaft
    for z in (1.75, 4.65):                                                       # the brass bands
        P.stack(m, FACE, 0, .42, -.04, [(z - .12, z - .06, .02, "Brass"), (z - .06, z + .06, .035, "Brass"),
                                         (z + .06, z + .12, .02, "Brass")], "Brass")
    m.ring([(-.34, 2.05), (.34, 2.05), (.34, 4.3), (-.34, 4.3)], [(-.26, 2.13), (.26, 2.13), (.26, 4.22), (-.26, 4.22)],
           "x", -.04, -.01, "MarbleShade")
    P.stack(m, FACE, 0, .42, -.04, [(H - 1.45, H - 1.38, .02, "Brass"), (H - 1.38, H - 1.2, .05, "MarbleShade"),
                                     (H - 1.2, H - 1.08, .1, "MarbleCream"), (H - 1.08, H - .98, .14, "Brass"),
                                     (H - .98, H - .8, .16, "MarbleShade")], "MarbleShade")
    m.box(FACE, -.45, H - .8, -.05, .45, H, "MarbleCream", .02)
    m.box(MASS - .28, -.46, 0, MASS, .46, H + .3, "MarbleCream", .02)            # outer pier
    m.box(MASS - .32, -.5, 0, MASS, .5, .8, "MarbleShade", .03)
    for z in (1.75, 4.65):
        m.box(MASS - .3, -.48, z - .08, MASS, .48, z + .08, "Brass", .015)
    return m.finish(col), []


def ceiling_beam(col, name):
    m = K.Mesh(name)
    # The ceiling over the pier line (the ceiling bays stop short of it): 1 m wide, it also closes the ends.
    m.box(-6.6, -.5, 0, 6.6, .5, .3, "MarbleCream", 0)
    m.box(-6.25, -.26, -.62, 6.25, .26, 0, "MarbleCream", .03)
    m.box(-6.25, -.2, -.66, 6.25, .2, -.62, "Brass", .01)
    for s in (-1, 1):
        m.box(s * 6.25, -.3, -.95, s * 5.95, .3, -.55, "MarbleShade", .03)
    return m.finish(col), []


def ceiling_bay(col, name):
    """Between two beams: the slab with a long skylight - a well 0.9 m deep lined in marble, brass-framed glazing
    in a grid - and a stepped cove either side."""
    m = K.Mesh(name)
    hw = 3.3
    for s in (-1, 1):
        m.box(s * 6.6, -1.38, 0, s * hw, 1.38, .3, "MarbleCream", 0)
        m.box(s * (hw + .35), -1.0, -.18, s * hw, 1.0, 0, "MarbleShade", .02)        # stepped edge of the well
        m.box(s * (hw + .6), -1.0, -.1, s * (hw + .35), 1.0, 0, "MarbleCream", .02)
        for c0, c1 in ((-.95, -.08), (.08, .95)):                                      # coffers in the flat ceiling
            m.ring([(s * 6.2, c0), (s * (hw + .7), c0), (s * (hw + .7), c1), (s * 6.2, c1)],
                   [(s * 6.08, c0 + .1), (s * (hw + .82), c0 + .1), (s * (hw + .82), c1 - .1), (s * 6.08, c1 - .1)],
                   "z", -.14, 0, "MarbleShade")
            m.box(s * 6.08, c0 + .1, -.02, s * (hw + .82), c1 - .1, 0, "Brass", 0)
        m.box(s * (hw + .1), -1.0, -.24, s * hw, 1.0, -.18, "Brass", .005)
        m.box(s * (hw + .1), -1.0, 0, s * hw, 1.0, .9, "MarbleCream", 0)              # the well's side
    m.box(-hw, -1.0, .9, hw, 1.0, .96, "Glass", 0)
    for t in (-1, 1):                                                                 # the well's ends
        m.box(-hw, t * .97, .02, hw, t * 1.38, .94, "MarbleCream", 0)
    for i in range(1, 6):
        x = -hw + 2 * hw * i / 6
        m.box(x - .03, -1.0, .84, x + .03, 1.0, .9, "Brass", .005)
    m.box(-hw, -.03, .84, hw, .03, .9, "Brass", .005)
    for y in (-.98, .98):
        m.box(-hw, y - .03, .82, hw, y + .03, .9, "Brass", .005)
    return m.finish(col), []


def crown(m, c, s, key="Brass", gem="VelvetPurple", axis_y=True):
    """A small royal crown (relief-free, round): band, five points with balls, a cross-orb on top."""
    x, y, z = c
    m.torus((x, y, z), .32 * s, .05 * s, key, 20, 6)
    m.lathe([(.34 * s, 0), (.34 * s, .16 * s), (.3 * s, .2 * s), (0, .2 * s)], (x, y, z), gem, 16)
    for i in range(5):
        a = 2 * math.pi * i / 5
        px, py = x + .3 * s * math.cos(a), y + .3 * s * math.sin(a)
        m.tube([(px, py, z), (px * .9 + x * .1, py * .9 + y * .1, z + .38 * s)], .035 * s, key, 6)
        m.sphere((px * .9 + x * .1, py * .9 + y * .1, z + .42 * s), .06 * s, key, 8, 5)
    m.sphere((x, y, z + .36 * s), .1 * s, key, 10, 6)
    m.box(x - .02 * s, y - .02 * s, z + .44 * s, x + .02 * s, y + .02 * s, z + .66 * s, key, 0)
    m.box(x - .08 * s, y - .02 * s, z + .55 * s, x + .08 * s, y + .02 * s, z + .59 * s, key, 0)


def portal(col, name):
    """The gate frame at the hall's exit (z 0..0.8), both faces: marble piers with brass bands, an entablature, a
    purple panel with a crown over the way."""
    m = K.Mesh(name)
    for s in (-1, 1):
        m.box(s * 6, 0, 0, s * 8, .8, H, "MarbleCream", .02)
        for yf, d in ((0, -1), (.8, 1)):
            m.box(s * 5.95, yf, 0, s * 8.05, yf + d * .07, .8, "MarbleShade", .02)
            for z in (1.75, 4.65):
                m.box(s * 6.3, yf, z - .1, s * 7.7, yf + d * .05, z + .1, "Brass", .015)
            m.box(s * 6.2, yf, H - .4, s * 7.8, yf + d * .08, H, "MarbleShade", .02)
    m.box(-8.2, -.05, H, 8.2, .85, H + 1.1, "MarbleCream", .02)
    m.box(-8.2, -.08, H, 8.2, .88, H + .25, "MarbleShade", .02)
    m.box(-8.25, -.1, H + .25, 8.25, .9, H + .3, "Brass", .005)
    m.box(-8.4, -.18, H + .95, 8.4, .98, H + 1.12, "MarbleShade", .03)
    for yf, d in ((-.08, -1), (.88, 1)):
        a0, a1 = sorted((yf, yf + d * .04))
        m.box(-1.3, a0, H + .35, 1.3, a1, H + .9, "VelvetPurple", 0)
        b0, b1 = sorted((yf, yf + d * .06))
        m.ring([(-1.38, H + .3), (1.38, H + .3), (1.38, H + .95), (-1.38, H + .95)],
               [(-1.3, H + .35), (1.3, H + .35), (1.3, H + .9), (-1.3, H + .9)], "y", b0, b1, "Brass")
    crown(m, (0, .4, H + 1.12), 1.3)
    return m.finish(col), []


def header(col, name):
    m = K.Mesh(name)
    top = 10.5 - H
    m.box(-6.6, 0, 0, 6.6, .3, top, "MarbleCream", .02)
    for yf, d in ((0, -1), (.3, 1)):
        m.box(-6.6, yf, 0, 6.6, yf + d * .08, .3, "MarbleShade", .02)
        m.box(-6.6, yf, .3, 6.6, yf + d * .1, .36, "Brass", .005)
        m.box(-6.6, yf, top - .3, 6.6, yf + d * .1, top, "MarbleShade", .02)
    return m.finish(col), []


def floor_inlay(col, name):
    """The purple velvet runner with brass edges down the middle, the whole 10 m (it meets the room's runner)."""
    m = K.Mesh(name)
    m.box(-1.2, 0, .024, 1.2, 10, .042, "VelvetPurple", .006)
    for s in (-1, 1):
        m.box(s * 1.2, 0, .024, s * 1.27, 10, .045, "Brass", .004)
        m.box(s * .95, 0, .042, s * 1.0, 10, .044, "Brass", 0)
    return m.finish(col), []


def build(col):
    return {
        "Bay": bay(col, "CR05_Bay"),
        "BayMid": bay(col, "CR05_BayMid", mid=True),
        "Pier": pier(col, "CR05_Pier"),
        "CeilingBeam": ceiling_beam(col, "CR05_CeilingBeam"),
        "CeilingBay": ceiling_bay(col, "CR05_CeilingBay"),
        "Portal": portal(col, "CR05_Portal"),
        "Header": header(col, "CR05_Header"),
        "FloorInlay": floor_inlay(col, "CR05_FloorInlay"),
        "GlobeSconce": O.globe_sconce(col, "CR05_GlobeSconce"),
    }


R.run(SPEC, build)
