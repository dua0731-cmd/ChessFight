# CR04 - the lower-palace rooms' corridor (grand stair hall -> mini-game 2 rooms B / E / F / J), after the GPT concept
# Docs/PawnRush/ConceptArt/SectionCorridors/04_lowerrooms.png: cream stone over a walnut wainscot, round-headed
# windows with tied navy curtains, cream pilasters, curved walnut ceiling ribs with joists, two brass chandeliers,
# three-arm sconces and portraits on the piers. Height 6.5. Starts at the hall's exit (Portal), ends 0.8 m before
# the mini-game's entry (Header).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr47_04_lowerrooms.py -- [--render] [--export]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import cr_kit_04_07 as K
import cr47_parts as P
import cr47_ornaments as O
import cr47_run as R

H = 6.5
SPEC = {
    "id": "CR04_LowerRooms", "height": H, "z_from": 0.0, "z_to": 10 - .8,
    "portal_start": True, "portal_end": False, "header_start": False, "header_end": True,
    "banner": {"width": 1.0, "height": 2.6, "fabric": "PR_Cor_VelvetNavy"},
    "layout": [
        {"name": "CR04_Sconce", "per": "pier", "index": [0, 1, 2, 3], "offset": [0.0, 3.0, 0.0]},
        {"name": "CR04_Portrait", "per": "pier", "index": [1, 2], "offset": [0.0, 4.4, 0.0]},
        {"name": "CR04_Chandelier", "per": "bay", "index": [0, 2], "mirror": False, "offset": [0.0, H, 0.0]},
    ],
}
FACE, BACK, MASS = -.38, -.62, -.72          # wall face, the window's back, the outer face (west frame)


def outer_face(m, y0, y1):
    """What a neighbour section sees: ashlar face, plinth, cornice over the ceiling slab, a roof parapet."""
    m.box(MASS - .06, y0, 0, MASS, y1, H, "CreamStone", 0)
    m.box(MASS - .14, y0, 0, MASS - .06, y1, .6, "MarbleShade", .02)
    m.box(MASS - .2, y0, H - .05, MASS - .06, y1, H + .4, "MarbleCream", .03)
    m.box(-.7, y0, H + .3, -.38, y1, H + .85, "CreamStone", .02)
    m.box(-.74, y0, H + .85, -.34, y1, H + .95, "MarbleCream", .02)


def bay(col, name, mid=False):
    """One bay between two piers: raised ashlar over a walnut wainscot, a deep round-headed window in dressed stone
    (voussoirs, keystone, quoins) on a bracketed sill, tied navy curtains under a swag, a stepped cornice."""
    m = K.Mesh(name)
    y0, y1 = -.985, .985        # ends 1.5 cm short, inside the piers (no shared planes with them)
    if mid:
        # The banner bay: a deep round-headed niche with a navy back where the code hangs its banner, a brass rod.
        K.wall_with_arch(m, y0, y1, 0, H, 0, 1.4, 2.5, 5.0, FACE, -.6, "CreamStone")
        m.prism(K.lift(K.arch(0, 1.4, 5.0 - 2.5, 12), 2.5), "x", -.62, -.6, "NavyPaint")
        P.voussoirs(m, 0, 1.4, 2.5, 5.0, .2, FACE, FACE + .07, "MarbleCream", "MarbleShade", n=11)
        m.box(FACE - .02, -.82, 2.38, FACE + .1, .82, 2.5, "MarbleShade", .02)
        holes = [(-.92, .92, 2.4, 5.0 + .7 + .3)]
        m.tube([(-.3, -.72, 5.45), (-.3, .72, 5.45)], .022, "Brass", 8)
        for s in (-1, 1):
            m.sphere((-.3, s * .74, 5.45), .045, "Brass", 8, 6)
            m.box(-.6, s * .66 - .025, 5.4, -.28, s * .66 + .025, 5.5, "Brass", .005)
    else:
        r = .5
        K.wall_with_arch(m, y0, y1, 0, H, 0, 2 * r, 3.05, 4.85, FACE, BACK, "CreamStone")
        P.window(m, 0, 2 * r, 3.05, 4.85, FACE, BACK, "CreamStone", "MarbleShade", "Glass", "MarbleShade", .05,
                 keystone=False, bars=(1, 2))
        P.voussoirs(m, 0, 2 * r, 3.05, 4.85, .22, FACE, FACE + .08, "MarbleCream", "MarbleShade", n=9)
        m.box(FACE - .02, -.78, 2.93, FACE + .16, .78, 3.05, "MarbleShade", .02)           # sill
        for s in (-1, 1):                                                                 # its brackets
            P.stack(m, FACE, s * .55, .07, FACE, [(2.62, 2.74, .0, "MarbleCream"), (2.74, 2.86, .06, None),
                                                   (2.86, 2.93, .12, None)], "MarbleCream")
        holes = [(-.8, .8, 2.6, 4.85 + r + .35)]
        m.tube([(-.12, -.92, 5.82), (-.12, .92, 5.82)], .022, "Brass", 8)
        for s in (-1, 1):
            m.sphere((-.12, s * .94, 5.82), .04, "Brass", 8, 6)
            P.curtain(m, s * .66, 5.8, 3.95, 2.5, -.34, -.08, "VelvetNavy", folds=5, spread=.19)
    m.box(BACK - .001, y0, 0, MASS, y1, H, "CreamStone", 0)
    P.ashlar(m, y0 + .02, y1 - .02, 2.42, H - .78, FACE, "CreamStone", holes=holes, seed=4 if mid else 2)
    m.box(FACE, y0, 0, FACE + .14, y1, 2.25, "Walnut", 0)                                  # the wainscot stands proud
    P.wainscot(m, y0, y1, FACE + .14, 2.25, "Walnut", "WalnutDark", "Walnut", "WalnutDark", panels=2, fillet_key="Brass")
    P.cornice(m, y0, y1, FACE, H - .78, [(.1, .05, "MarbleShade"), (.06, .09, "Brass"), (.18, .14, "MarbleCream"),
                                          (.1, .2, "MarbleShade"), (.34, .3, "CreamStone")], "CreamStone")
    outer_face(m, y0, y1)
    ob = m.finish(col)
    # The code hangs a banner from this point (the rod) in the banner bay.
    return ob, ([K.empty(col, "BANNER", (-.3, 0, 5.42))] if mid else [])


def pier(col, name):
    """A cream stone pilaster standing 0.38 m proud: a stepped marble base, a panelled shaft with flutes, a stepped
    capital with a brass astragal that the walnut rib springs from."""
    m = K.Mesh(name)
    m.box(MASS, -.46, 0, FACE, .46, H, "CreamStone", 0)
    P.stack(m, FACE, 0, .4, -.04, [(0, .28, .08, "MarbleShade"), (.28, .4, .05, "MarbleCream"), (.4, .5, .02, "MarbleShade")],
            "MarbleShade")
    m.box(FACE, -.4, .5, -.04, .4, H - 1.45, "CreamStone", .03)                   # shaft
    m.ring([(-.36, .75), (.36, .75), (.36, H - 1.7), (-.36, H - 1.7)], [(-.29, .82), (.29, .82), (.29, H - 1.77), (-.29, H - 1.77)],
           "x", -.04, -.0, "MarbleShade")
    for y in (-.18, -.06, .06, .18):                                               # flutes' fillets
        m.box(-.05, y - .022, .9, -.02, y + .022, H - 1.9, "MarbleShade", .008)
    P.stack(m, FACE, 0, .4, -.04, [(H - 1.45, H - 1.37, .02, "Brass"), (H - 1.37, H - 1.22, .05, "MarbleShade"),
                                    (H - 1.22, H - 1.1, .09, "MarbleCream"), (H - 1.1, H - 1.0, .13, "MarbleShade")], "MarbleShade")
    m.box(FACE, -.42, H - 1.0, -.06, .42, H, "CreamStone", .02)
    m.box(MASS - .26, -.45, 0, MASS, .45, H + .3, "CreamStone", .02)         # outer pier
    m.box(MASS - .3, -.5, 0, MASS, .5, .7, "MarbleShade", .03)
    m.box(MASS - .3, -.5, H - .5, MASS, .5, H - .2, "MarbleShade", .03)
    return m.finish(col), []


def ceiling_beam(col, name):
    """A curved walnut rib (a deep segmental arch) with a stepped soffit, springing from stepped corbels."""
    m = K.Mesh(name)
    # The ceiling over the pier line (the ceiling bays stop short of it): 1 m wide, it also closes the ends.
    m.box(-6.6, -.5, 0, 6.6, .5, .3, "Plaster", 0)
    K.arc_fill(m, -6.2, 6.2, -1.0, .72, 0.0, "y", -.22, .22, "Walnut", 18)
    K.arc_fill(m, -6.1, 6.1, -1.12, .72, -.3, "y", -.13, .13, "WalnutDark", 18)
    for s in (-1, 1):
        for i, (z0, z1, d) in enumerate(((-1.45, -1.32, .16), (-1.32, -1.2, .28), (-1.2, -1.0, .42))):
            m.box(s * 6.3, -.2 - i * .02, z0, s * (6.3 - d), .2 + i * .02, z1, "WalnutDark", .02)
    return m.finish(col), []


def ceiling_bay(col, name):
    """Plaster between the ribs, two walnut joists and a ridge beam, each panel framed in a walnut moulding, a
    brass rose for the chandelier."""
    m = K.Mesh(name)
    m.box(-6.6, -1.38, 0, 6.6, 1.38, .3, "Plaster", 0)     # to the beam strip (no hole over the piers)
    for x in (-3.0, 3.0):
        m.box(x - .16, -1.0, -.34, x + .16, 1.0, 0, "Walnut", .02)
        m.box(x - .1, -1.0, -.4, x + .1, 1.0, -.34, "WalnutDark", .015)
    m.box(-.13, -1.0, -.26, .13, 1.0, 0, "WalnutDark", .02)
    m.box(-6.25, -.1, -.2, 6.25, .1, 0, "WalnutDark", .015)
    for a, b in ((-6.25, -3.16), (-2.84, -.13), (.13, 2.84), (3.16, 6.25)):
        for c0, c1 in ((-.95, -.1), (.1, .95)):
            m.ring([(a + .08, c0 + .04), (b - .08, c0 + .04), (b - .08, c1 - .04), (a + .08, c1 - .04)],
                   [(a + .16, c0 + .12), (b - .16, c0 + .12), (b - .16, c1 - .12), (a + .16, c1 - .12)], "z", -.07, 0, "Walnut")
    m.lathe([(0, 0), (.24, 0), (.22, -.05), (.1, -.09), (0, -.1)], (0, 0, -.26), "Brass", 16)   # chandelier rose
    return m.finish(col), []


def portal(col, name):
    """The gate frame at the hall's exit (z 0..0.8), dressed on both faces."""
    m = K.Mesh(name)
    for s in (-1, 1):
        m.box(s * 6, 0, 0, s * 8, .8, H, "CreamStone", .02)
        for yf, d in ((0, -1), (.8, 1)):
            m.box(s * 5.95, yf, 0, s * 8.05, yf + d * .06, .6, "MarbleShade", .02)
            m.box(s * 6.35, yf, .6, s * 7.65, yf + d * .05, H - .35, "MarbleCream", .02)
            m.box(s * 6.25, yf, H - .35, s * 7.75, yf + d * .08, H, "MarbleShade", .02)
    m.box(-8.2, -.05, H, 8.2, .85, H + .95, "CreamStone", .02)
    m.box(-8.2, -.08, H, 8.2, .88, H + .25, "MarbleShade", .02)
    m.box(-8.4, -.15, H + .82, 8.4, .95, H + .98, "MarbleCream", .03)
    sh = P.shield_outline(.7, .78)
    for yf, d in ((-.08, -1), (.88, 1)):
        m.prism([(u, H + .52 + v) for u, v in sh], "y", min(yf, yf + d * .05), max(yf, yf + d * .05), "NavyPaint")
        rim = [(u * 1.12, H + .52 + v * 1.1) for u, v in sh]
        m.ring(rim, [(u, H + .52 + v) for u, v in sh], "y", min(yf, yf + d * .07), max(yf, yf + d * .07), "Brass")
        a0, a1 = sorted((yf + d * .05, yf + d * .08))
        P.pawn_relief(m, "y", a0, a1, 0, H + .27, .48, "Plaster")
    for s in (-1, 1):                                                         # pediment scrolls over the cornice
        m.tube(K.bez((s * .25, .4, H + .98), (s * .5, .4, H + 1.45), (s * 1.0, .4, H + 1.35), (s * 1.3, .4, H + 1.0), 10),
               .08, "MarbleCream", 10)
        m.tube(K.spiral((s * 1.3, .4, H + 1.12), .02, .11, 1.0, "xz", a0=-math.pi * .5, flip=-s, n=14), .05, "MarbleCream", 8)
    m.sphere((0, .4, H + 1.25), .16, "Brass", 12, 8)
    return m.finish(col), []


def header(col, name):
    """Over the mini-game's arch, from the ceiling (0) to y 10.5: a wall 0.3 thick on the corridor side of the line,
    a walnut moulding at the ceiling and a cornice on top, both faces."""
    m = K.Mesh(name)
    top = 10.5 - H
    m.box(-6.6, 0, 0, 6.6, .3, top, "CreamStone", .02)
    for yf, d in ((0, -1), (.3, 1)):
        m.box(-6.6, yf, 0, 6.6, yf + d * .08, .3, "Walnut", .02)
        m.box(-6.6, yf, top - .3, 6.6, yf + d * .1, top, "MarbleCream", .02)
    return m.finish(col), []


def build(col):
    mods = {
        "Bay": bay(col, "CR04_Bay"),
        "BayMid": bay(col, "CR04_BayMid", mid=True),
        "Pier": pier(col, "CR04_Pier"),
        "CeilingBeam": ceiling_beam(col, "CR04_CeilingBeam"),
        "CeilingBay": ceiling_bay(col, "CR04_CeilingBay"),
        "Portal": portal(col, "CR04_Portal"),
        "Header": header(col, "CR04_Header"),
        "Sconce": O.sconce3(col, "CR04_Sconce"),
        "Portrait": O.portrait(col, "CR04_Portrait"),
        "Chandelier": O.chandelier(col, "CR04_Chandelier"),
    }
    return mods


R.run(SPEC, build)
