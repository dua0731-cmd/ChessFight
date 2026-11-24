# CR01 · the garden corridor (2026-11-22): the corridor that leaves the Royal Water Garden (or, with CR02's look, the
# carriage yard) for the first gate's mini-game. Concept: Docs/PawnRush/ConceptArt/SectionCorridors/01_garden.png,
# ornaments: AssetRefs/CR01_assets_garden.png (all modelled here).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python cr01_garden.py -- [--export] [--render]
#
# Its ends never change: the garden's exit at y 0 (an obstacle end: the gate frame CR01_Portal), the first gate's entry
# at y 10 (the walls stop 0.8 m short, at 9.2: the mini-game's wing walls and arch stand there; open sky, no header).
# So the piers stand at 0.45, 3.217, 5.983, 8.75 and a bay's clear span is 1.867 (authored 1.9, scaled 0.983).
#
# Open to the sky. West wall, inner face at x -6.30 (0.30 behind the bodies' face), outer face at x -7.0:
#   0 .. 0.32   a fountain basin in front of the wall (x -6.0 .. -6.3), fed by a scallop-shell spout at 0.85
#   0 .. 4.2    ashlar in 0.48 m courses, an arched niche (1.2 wide, sill 2.0, springing 3.1) with a rose planter
#   4.2 .. 4.5  cornice; 4.5 .. 5.42 a balustrade (plinth, six balusters a bay, rail)
#   piers 0.9 along the way: plinth with a gold fleur-de-lis medallion, gold band at 4.05, pedestal and urn finial on top
# Nothing reaches past x -6.0. The bodies' wall is 5.2 high with an unseen fence to 7.5 (SectionCorridor).
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bpy
import cr13_common as C
import cr13_ornaments as O
import cr13_details as D

ID = "CR01_Garden"
HERE = os.path.dirname(os.path.abspath(__file__))

# Materials (Unity names). The garden concept kit's, plus the herringbone paving (new: the garden's plaza texture).
STONE, WARM, PALE, BAND = "GC_cream_stone", "GC_cream_stone_warm", "GC_cream_stone_pale", "GC_stone_band"
GOLD, WATER, JET, POOL = "GC_gold", "GC_basin_water", "GC_fountain_jet", "GC_channel_stone"
LEAF, LEAF2, LEAF3, SOIL = "GC_leaf", "GC_leaf_light", "GC_leaf_dark", "GC_planter_soil"
RED, PINK = "GC_pennant_red", "GC_flower_pink"
FLOOR = "PR_Cor_GardenHerringbone"
C.new_material(FLOOR, (1, 1, 1), .78, 0., os.path.join(C.ROOT, "ArtSource/PawnRush/GardenConcept/textures/Herringbone_PinkBeige.png"),
               tiling=1 / 4.5)

FACE, BACK, OUTER = -6.30, -6.62, -7.0     # inner face, niche back, outer face
COURSE = .48
CORNICE, TOP = 4.2, 4.5
NICHE_A, SILL, SPRING = .6, 2.0, 3.1
SPAN = C.AUTHORED_SPAN
HB = SPAN / 2 + .05                         # a bay's masonry runs 5 cm into the piers


def ashlar_rows(m, mat, x0, x1, y0, y1, z0, z1, skip=None, block=.95, phase=0, bevel=.026):
    """Ashlar blocks between y0 and y1, courses from z0 to z1; skip(z_lo, z_hi) -> (ya, yb) or None leaves a gap."""
    course = 0
    z = z0
    while z < z1 - .05:
        zt = min(z1, z + COURSE)
        shift = (block / 2) if (course + phase) % 2 else 0
        edges = [y0]
        yy = y0 - shift + block
        while yy < y1 - .2:
            if yy > y0 + .2:
                edges.append(yy)
            yy += block
        edges.append(y1)
        gap = skip(z, zt) if skip else None
        for a, b in zip(edges[:-1], edges[1:]):
            parts = [(a, b)]
            if gap:
                ga, gb = gap
                parts = []
                if a < ga:
                    parts.append((a, min(b, ga)))
                if b > gb:
                    parts.append((max(a, gb), b))
            for pa, pb in parts:
                if pb - pa > .06:
                    m.box(x0, x1, pa + .011, pb - .011, z + .011, zt - .011, mat, bevel=bevel)
        z = zt
        course += 1


def arch_points(a, spring, n=14, grow=0.0):
    r = a + grow
    return [(r * math.cos(math.pi * i / n), spring + r * math.sin(math.pi * i / n)) for i in range(n + 1)]


def build_bay(coll):
    m = C.Mesh(ID + "_Bay")
    # the wall's core behind everything, and the niche's back
    m.box(BACK - .02, OUTER + .03, -HB + .01, HB - .01, 0, TOP - .005, BAND, bevel=0)   # a hair inside the faces round it (flicker)
    # inner ashlar, leaving the niche (and its surround) open
    def gap(z0, z1):
        if z1 > SILL - .02 and z0 < SPRING + NICHE_A + .14:
            return (-NICHE_A - .1, NICHE_A + .1)
        return None
    ashlar_rows(m, STONE, FACE, BACK, -HB, HB, 0, CORNICE, skip=gap)
    # over the arch: the spandrel up to the cornice, one stone either side of a keystone
    outer = arch_points(NICHE_A + .1, SPRING)
    poly = [(NICHE_A + .1, SPRING), (NICHE_A + .1, CORNICE - .006)] + [(-NICHE_A - .1, CORNICE - .006), (-NICHE_A - .1, SPRING)]
    poly = poly + list(reversed(outer))[1:-1]
    m.prism([(u, v) for u, v in poly], "x", FACE, BACK, STONE, bevel=0)
    # the niche: back panel, reveals and soffit, a moulded surround, a sill and a keystone
    inner = arch_points(NICHE_A, SPRING, 16)
    back_outline = [(NICHE_A, SILL), ] + inner + [(-NICHE_A, SILL)]
    m.prism(back_outline, "x", BACK - .01, BACK + .02, WARM, bevel=0)
    ring_in = [(NICHE_A, SILL)] + inner + [(-NICHE_A, SILL)]
    ring_out = [(NICHE_A + .1, SILL)] + arch_points(NICHE_A + .1, SPRING, 16) + [(-NICHE_A - .1, SILL)]
    for i in range(len(ring_in) - 1):
        quad = [ring_in[i], ring_out[i], ring_out[i + 1], ring_in[i + 1]]
        m.prism(quad, "x", FACE + .045, BACK + .01, PALE, bevel=0)      # reveal and surround, one stone deep
    for i in range(len(ring_in) - 1):
        a, b = ring_out[i], ring_out[i + 1]
        quad = [ring_out[i], (a[0] * 1.13 if abs(a[0]) > .01 else a[0], a[1] + (a[1] - SPRING) * .13 if a[1] > SPRING else a[1]),
                (b[0] * 1.13 if abs(b[0]) > .01 else b[0], b[1] + (b[1] - SPRING) * .13 if b[1] > SPRING else b[1]), ring_out[i + 1]]
        m.prism(quad, "x", FACE + .065, FACE - .01, WARM, bevel=0)      # a raised architrave round it
    m.prism([(-.12, SPRING + NICHE_A + .02), (.12, SPRING + NICHE_A + .02), (.16, SPRING + NICHE_A + .34),
             (-.16, SPRING + NICHE_A + .34)], "x", FACE + .09, BACK + .05, WARM, bevel=.012)
    m.box(FACE + .13, BACK + .1, -NICHE_A - .18, NICHE_A + .18, SILL - .1, SILL + .02, WARM, bevel=.015)
    # the planter in the niche
    D.planter2(m, PALE, SOIL, LEAF3, LEAF2, [RED, PINK], LEAF, "GC_flower_white", "GC_flower_heart", LEAF3, FACE - .16, 0, SILL + .02, r=.4, seed=7)
    # a rose garland swagged over the niche from pier to pier, across the keystone
    D.garland(m, LEAF3, [RED, PINK], "GC_flower_white", "GC_flower_heart", (FACE + .1, -SPAN / 2 + .02, 4.1), (FACE + .1, SPAN / 2 - .02, 4.1), .3, seed=5, roses=3)
    # the basin at the wall's foot and the shell spout feeding it
    m.box(-6.0, -6.07, -.82, .82, 0, .32, PALE, bevel=.02)               # front kerb
    for s in (-1, 1):
        m.box(-6.0, FACE + .01, s * .82 - .035, s * .82 + .035, 0, .32, PALE, bevel=.015)
    m.box(-6.07, FACE + .01, -.79, .79, 0, .1, POOL, bevel=0)             # the basin's bed
    m.box(-6.07, FACE + .005, -.79, .79, .1, .24, WATER, bevel=0)         # its water, up to 0.24
    O.shell_spout(m, PALE, GOLD, JET, FACE + .01, 0, .78, r=.3)
    D.lily(m, "GC_lily_pad", "GC_lotus_pink", -6.17, -.45, .24, r=.12)
    D.lily(m, "GC_lily_pad", "GC_lotus_pink", -6.16, .42, .24, r=.1, bloom=False)
    # cornice: a gold fillet, a cyma and a coping over the whole wall
    m.box(FACE + .03, BACK + .02, -HB, HB, CORNICE - .08, CORNICE, GOLD, bevel=.008)
    m.prism([(FACE, CORNICE), (FACE + .1, CORNICE + .1), (FACE + .14, CORNICE + .22), (FACE + .18, CORNICE + .295),
             (OUTER - .1, CORNICE + .295), (OUTER - .1, CORNICE)], "y", -HB, HB, WARM, bevel=.01)
    # balustrade: plinth, balusters, rail
    bx = -6.62
    m.box(bx - .27, bx + .27, -HB, HB, TOP, TOP + .15, PALE, bevel=.02)
    n = 6
    for i in range(n):
        y = -SPAN / 2 + SPAN * (i + .5) / n
        O.baluster(m, PALE, bx, y, TOP + .15, .6, block=.15)
    m.box(bx - .25, bx + .25, -HB, HB, TOP + .75, TOP + .92, PALE, bevel=.025)
    # outer face: ashlar on the core, a plinth, the cornice already runs over it
    ashlar_rows(m, STONE, OUTER + .02, OUTER - .06, -HB, HB, .35, CORNICE, phase=1, bevel=0)
    m.box(OUTER + .02, OUTER - .12, -HB, HB, 0, .35, WARM, bevel=.02)
    return m.finish(coll, (-6.0, 0, 0))


def build_pier(coll):
    m = C.Mesh(ID + "_Pier")
    h = PIER_HALF = .45
    m.box(-6.0, -7.18, -h, h, 0, .42, WARM, bevel=.03)                      # plinth
    m.box(-6.0, -7.18, -h + .02, h - .02, .42, .5, PALE, bevel=.015)
    m.box(-6.06, -7.12, -.4, .4, .5, 4.02, STONE, bevel=.03)                # shaft
    m.box(-6.04, -7.14, -.42, .42, 4.02, 4.1, GOLD, bevel=.008)             # gold band
    m.box(-6.05, -7.13, -.42, .42, 4.1, 4.24, WARM, bevel=.015)
    m.box(-6.0, -7.18, -h, h, 4.24, 4.5, WARM, bevel=.03)                   # capital
    # pedestal on the balustrade line and the urn finial
    bx = -6.62
    m.box(bx - .32, bx + .32, -.32, .32, 4.5, 4.62, PALE, bevel=.02)
    m.box(bx - .27, bx + .27, -.27, .27, 4.62, 5.34, PALE, bevel=.025)
    m.box(bx - .28, bx + .28, -.28, .28, 5.18, 5.24, GOLD, bevel=.006)
    m.box(bx - .33, bx + .33, -.33, .33, 5.34, 5.46, PALE, bevel=.02)
    O.urn(m, PALE, GOLD, bx, 0, 5.46, h=.8)
    # a fleur-de-lis medallion on the plinth's front (toward the way) and on the outer face
    # panel proud of the shaft by 1.5 cm in a 3 cm frame, the relief standing 3.5 cm off the panel (all behind x -6.0)
    m.box(-6.07, -6.045, -.28, .28, .62, 1.18, WARM, bevel=.008)
    for z0, z1, y0, y1 in ((.56, .62, -.34, .34), (1.18, 1.24, -.34, .34), (.62, 1.18, -.34, -.28), (.62, 1.18, .28, .34)):
        m.box(-6.07, -6.03, y0, y1, z0, z1, PALE, bevel=.01)
    O.fleur(m, GOLD, .44, "x", (0, .9), -6.048, -6.012)
    # gold strips up the shaft's front corners, as the concept's piers
    for s in (-1, 1):
        m.box(-6.05, -6.1, s * .4 - .03, s * .4 + .03, .5, 4.02, GOLD, bevel=.006)
    ob = m.finish(coll, (-6.0, 0, 0))
    # where code hangs the pier's banner (top centre of its front face, facing +x) - blue and crimson alternate
    C.empty(coll, "BANNER", (-6.06, 0, 3.9), parent=ob)
    return ob


def build_portal(coll):
    """The gate frame at the garden's exit (y 0 .. 0.95): two big piers out to |x| 8, an elliptical arch over the way,
    an entablature, a balustrade and urns; dressed on both faces."""
    m = C.Mesh(ID + "_Portal")
    y0, y1 = 0.0, .95
    for s in (-1, 1):
        xa, xb = s * 5.985, s * 8.0   # 1.5 cm proud of the wall pier buried in it (no shared faces)
        m.box(xa, xb, y0 - .03, y1 + .03, 0, .5, WARM, bevel=.03)
        m.box(xa + s * .06, xb - s * .06, y0 - .015, y1 + .015, .5, 5.0, STONE, bevel=.03)
        ashlar = lambda ya, yb, z0, z1: None
        for k, zb in enumerate([.5 + i * .5 for i in range(9)]):
            m.box(xa + s * .03, xb - s * .03, y0 - .02, y1 + .02, zb - .01, zb + .01, WARM, bevel=0)
        m.box(xa, xb, y0 - .03, y1 + .03, 4.92, 5.08, GOLD, bevel=.01)
        m.box(xa - s * .0, xb + s * .05, y0 - .05, y1 + .05, 5.08, 5.4, WARM, bevel=.03)
        for f, yy in ((-1, y0 - .02), (1, y1 + .02)):
            m.box(s * 6.55, s * 7.45, yy + f * .002, yy + f * .04, 1.0, 1.9, PALE, bevel=.012)   # the plaque, outward
            O.fleur(m, GOLD, .62, "y", (s * 7.0, 1.45), yy + f * .041, yy + f * .07, flip_u=False)   # on its face
    # ivy climbing both faces of each gate pier, a few roses in it
    for s in (-1, 1):
        for yy, f in ((y0 - .04, -1), (y1 + .04, 1)):
            path = [(s * (6.25 + .06 * math.sin(i * 1.3)), yy, .5 + i * .32) for i in range(13)]
            path += [(s * (6.45 + .25 * i), yy, 4.4 + .12 * math.sin(i)) for i in range(1, 5)]
            D.ivy(m, LEAF3, LEAF3, path, seed=int(20 + s * 3 + f), every=.04, size=1.7, facing=(0, f, 0))
            for z in (1.6, 2.9, 3.9):
                D.rose2(m, RED if z != 2.9 else PINK, s * 6.3, yy + f * .05, z, s=1.0)
    # the arch: a ring of voussoirs, half-ellipse over 12 m, springing at 5.4, rise 1.5
    n = 15
    ring_in = [(6.0 * math.cos(math.pi * i / n), 5.4 + 1.5 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    ring_out = [(6.55 * math.cos(math.pi * i / n), 5.4 + 2.05 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    for i in range(n):
        q = [ring_in[i], ring_out[i], ring_out[i + 1], ring_in[i + 1]]
        q = [(u * (1 if j in (0, 3) else 1), v) for j, (u, v) in enumerate(q)]
        m.prism([(u, v) for u, v in q], "y", y0 + .02, y1 - .02, PALE if i != n // 2 else WARM, bevel=.018)
    # spandrels up to the entablature
    for s in (-1, 1):
        pts = [(s * 6.55, 5.4), (s * 8.0, 5.4), (s * 8.0, 7.6), (0, 7.6)]
        arc = [(u, v) for u, v in ring_out if u * s >= -1e-6]
        arc = sorted(arc, key=lambda p: -abs(p[0]))
        pts = [(s * 8.0, 5.4), (s * 8.0, 7.6), (0, 7.6)] + list(reversed(arc))
        m.prism(pts, "y", y0 + .04, y1 - .04, STONE, bevel=0)
    m.prism([(-.28, 7.35), (.28, 7.35), (.36, 7.75), (-.36, 7.75)], "y", y0 - .04, y1 + .04, WARM, bevel=.015)
    m.box(-8.1, 8.1, y0 - .06, y1 + .06, 7.6, 7.72, GOLD, bevel=.01)
    m.box(-8.25, 8.25, y0 - .12, y1 + .12, 7.72, 8.05, WARM, bevel=.03)
    for f, yy in ((-1, y0 + .02), (1, y1 - .02)):     # on the crown voussoirs' faces
        O.fleur(m, GOLD, .7, "y", (0, 7.05), yy + f * .001, yy + f * .036)
    # balustrade and urns on top
    m.box(-8.0, 8.0, y0 + .2, y1 - .2, 8.056, 8.2, PALE, bevel=.02)
    for i in range(28):
        x = -7.6 + 15.2 * (i + .5) / 28
        if abs(abs(x) - 7.2) < .5 or abs(x) < .4:
            continue
        O.baluster(m, PALE, x, (y0 + y1) / 2, 8.2, .55, block=.14, low=True)
    m.box(-8.0, 8.0, y0 + .22, y1 - .22, 8.75, 8.9, PALE, bevel=.02)
    for x in (-7.2, 0, 7.2):
        m.box(x - .32, x + .32, y0 + .15, y1 - .15, 8.05, 8.95, PALE, bevel=.025)
        O.urn(m, PALE, GOLD, x, (y0 + y1) / 2, 8.95, h=.85)
    return m.finish(coll, (0, 0, 0))


def build_floor(coll):
    """The way: herringbone between cream kerb slabs, a cream threshold before the mini-game's entry. Top +0.024 as the
    mini-games' paving; it rises from +0.004 over the first 0.6 m so the garden's exit floor (top 0) meets no lip."""
    m = C.Mesh(ID + "_Floor")
    prof = [(0, -.12), (9.4, -.12), (9.4, .024), (.6, .024), (0, .004)]
    m.prism(prof, "x", -5.3, 5.3, FLOOR, bevel=0)
    for s in (-1, 1):
        y = 0.0
        while y < 9.4 - .01:
            yb = min(9.4, y + .98)
            top0 = .004 + .02 * min(1, y / .6)
            top1 = .004 + .02 * min(1, yb / .6)
            m.prism([(y + .008, -.12), (yb - .008, -.12), (yb - .008, top1), (y + .008, top0)], "x",
                    s * 5.31, s * 6.3, STONE, bevel=.006)
            y = yb
    for i in range(12):
        xa = -6.3 + 12.6 * i / 12
        m.box(xa + .008, xa + 12.6 / 12 - .008, 9.408, 10.0, -.12, .024, WARM, bevel=.006)
    ob = m.finish(coll, (0, 0, 0))
    return ob


def herring_uv(ob):
    """45 degrees to the way on the herringbone's faces (the plaza's look)."""
    me = ob.data
    uv = me.uv_layers.active
    idx = [i for i, mm in enumerate(me.materials) if mm.name == FLOOR]
    if not idx:
        return
    s = 1 / math.sqrt(2)
    for p in me.polygons:
        if p.material_index not in idx:
            continue
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co + ob.location
            uv.data[li].uv = ((co.x + co.y) * s, (co.y - co.x) * s)


def main():
    C.reset()
    coll = C.collection(ID)
    bay, pier, portal, floor = build_bay(coll), build_pier(coll), build_portal(coll), build_floor(coll)
    herring_uv(floor)
    for ob in (bay, pier, portal, floor):
        print("%-22s %7d tris" % (ob.name, C.tris(ob)))
    frm, to = 0.0, 9.2
    ps = C.piers(frm, to)
    span = C.bay_span(frm, to)
    total = C.tris(floor) + C.tris(portal) + 2 * (4 * C.tris(pier) + 3 * C.tris(bay))
    print("corridor total %d tris (span %.3f, scale %.3f)" % (total, span, span / C.AUTHORED_SPAN))
    if "--export" in C.ARGS:
        out = C.OUT_REL + "/" + ID + "/CR01_"
        C.export([bay], out + "Bay.fbx")
        C.export([pier] + [o for o in coll.objects if o.parent == pier], out + "Pier.fbx")
        C.export([portal], out + "Portal.fbx")
        C.export([floor], out + "Floor.fbx")
        C.write_layout(ID, 4.5, frm, to, ["Bay", "Pier", "Portal", "Floor"], open_sky=True)
    if "--render" in C.ARGS:
        for ob in (bay, pier, portal, floor):
            ob.hide_render = True
        for side in (-1, 1):
            for p in ps:
                C.place(pier, side * 6.0, p, side)
            for i in range(3):
                C.place(bay, side * 6.0, (ps[i] + ps[i + 1]) / 2, side, span / C.AUTHORED_SPAN)
        C.place(portal, 0, 0)
        C.place(floor, 0, 0)
        cfl = bpy.data.objects.new("ctx", None)
        # context: the garden's exit floor behind, the mini-game's entry band ahead (grey slabs)
        ctx = C.Mesh("context")
        ctx.box(-12, 12, -14, 0, -.2, 0, "GC_floor_tile", bevel=0)
        ctx.box(-14, 14, 10, 30, -.2, .02, "PR_Early_Paving", bevel=0)
        ctx.box(-14, -6.6, 10, 30, 0, 7, "PR_Early_WallCore", bevel=0)
        ctx.box(6.6, 14, 10, 30, 0, 7, "PR_Early_WallCore", bevel=0)
        ctx.finish(bpy.context.scene.collection)
        for x in (-1.2, 0, 1.2):
            C.pawn(bpy.context.scene.collection, x, -1.0, "GC_royal_blue" if x else "GC_pennant_red")
        C.render_setup()
        prev = os.path.join(HERE, "previews")
        os.makedirs(prev, exist_ok=True)
        C.shoot(os.path.join(prev, "cr01_concept_view.png"), (0, -4.2, 2.6), (0, 8, 2.2), 22)
        C.shoot(os.path.join(prev, "cr01_wall_close.png"), (2.5, 2.2, 1.8), (-6.2, 4.6, 2.2), 26)
        C.shoot(os.path.join(prev, "cr01_from_minigame.png"), (0, 15, 3.0), (0, 2, 2.6), 22)
        C.shoot(os.path.join(prev, "cr01_outside.png"), (-16, -6, 7), (-6, 5, 2.5), 26)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "cr01_garden.blend"))


main()
