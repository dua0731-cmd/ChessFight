# Royal water garden - concept-art dressing, sample 5 v1: the boat crossing (beat 6) of the v31 layout (R436).
#
# 승규 11-05: "다음 작업" (after the lock and the dyke). As before every position and collider stays the v31 graybox's
# (RoyalWaterGardenBuilder.Beats.cs BoatCrossing); this only draws over it in the kit's look (R419), the concept's
# "곤돌라 보트" and falls (PAWN_RUSH_GARDEN v04):
#   the boarding quay   kit tiles, block walls down to the moat (west, south) and over the reservoir (north), the
#                       canal's balustrade on its west and south rails, gold mooring rings on its north face
#   the reservoir wall  west of the quay: R439 moved to water_sample_v1.py (the spillway)
#   the two tunnels     (|x| 18..26) boathouses: rusticated piers on the lane walls, a stone eave with gold trims and a
#                       crown over each lane, a corniced roof with a balustrade, block-lined lanes, a dark back; the
#                       graybox's glass falls in each mouth stay
#   Boat 1-1 .. 4-2     royal gondolas: a royal blue hull with a cream upper strake between gold bands, a gold
#                       gunwale, a planked wood deck (the walking surface, 3.8), gold finials at both ends, a crown on
#                       each side (ride their movers)
#   the lanes           floating ropes with gold and white buoys between them
# All faces outward and no two faces in one plane (PITFALLS 38, 39).
#
# Run:  blender -b --python boats_sample_v1.py
# Out:  BoatsSample_v1.blend, render_boats_v1_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Boats dressing"
os.environ["GC_SEED"] = "55"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)     # as the platforms'
M["shade"] = material("GC | tunnel shade", (.05, .06, .09), .9)
M["rope"] = material("GC | rope", (.93, .88, .76), .8)

import water_tiers as wt  # noqa: E402
UPPER, RES_Y, RES_TOP, MOAT = 3.8, wt.LATE, wt.LATE + .35, wt.MIDDLE    # R441: the reservoir and the lake from water_tiers.py
LANES = (35.4, 39.0, 42.6, 46.2)
WALLS = ((33.0, 34.2), (36.6, 37.8), (40.2, 41.4), (43.8, 45.0), (47.4, 48.6))
BOATS = [("%d-%d" % (lane + 1, n + 1), -9.0 + 18.0 * n, z) for lane, z in enumerate(LANES) for n in range(2)]

# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
ctx.box((8, 24, DEEP), (35, 32.6, UPPER), M["clay"])            # the robot dyke (beat 5)
ctx.box((-14, 48.6, DEEP), (14, 60.5, UPPER), M["clay"])        # the gate yard (beat 7)
ctx.done()


def water_mat(name, rgb, alpha=1.0):
    w = bpy.data.materials.new(name)
    w.use_nodes = True
    p = w.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*srgb(rgb), 1)
    p.inputs["Roughness"].default_value = .06
    if alpha < 1:
        p.inputs["Alpha"].default_value = alpha
        for attr, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
            try:
                setattr(w, attr, value)
            except Exception:
                pass
    return w


wm = Mesh("Context water", CTX)
wm.box((-40, 10, MOAT - .05), (40, 33, MOAT), water_mat("Context | water", (.02, .58, .82)))
wm.box((-26, 33, RES_Y - .05), (26, 48.6, RES_Y), water_mat("Context | reservoir", (.03, .5, .78)))
falls = water_mat("Context | falls", (.75, .92, 1.0), .45)
for s in (-1, 1):
    for z in LANES:
        x0, x1 = (18.65, 18.85) if s > 0 else (-18.85, -18.65)
        wm.box((x0, z - 1.2, RES_Y), (x1, z + 1.2, 4.4), falls)
wm.done()

stone = Mesh("Quay stonework")
flowers = Mesh("Quay flowers")

# ------------------------------------------------------------------ the boarding quay (x -10..10, z 27..33)
kit_paving(stone, -10, 10, 27, 33, UPPER)
kit_wall_run(stone, "y", -10.0, 26.65, 33, -.35, 0, UPPER)                 # the west face, down to the moat
kit_wall_run(stone, "x", 27.0, -10.35, 8, -.35, 0, UPPER)                  # the south face
kit_wall_run(stone, "x", 33.0, -10, 10, -.32, 0, UPPER, RES_Y - .35)       # the north face over the reservoir
kit_balustrade(stone, flowers, (-10.15, 27, 0), (-10.15, 33, 0), lambda p: UPPER, False)
kit_balustrade(stone, flowers, (-10, 26.85, 0), (8, 26.85, 0), lambda p: UPPER, True, True)
# Gold mooring rings on the north face, a lion-less boss behind each.
for x in (-8, -4, 0, 4, 8):
    dome(stone, (x, 33.0, RES_Y + .5), (0, 1, 0), .13)          # R441: over the reservoir's new level
    stone.lathe([(.11, -.022), (.15, 0), (.11, .022), (.07, 0)],
                Matrix.Translation((x, 33.06, RES_Y + .27)) @ Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 20, True, True)

# ------------------------------------------------------------------ the reservoir's south wall west of the quay (x -26..-10.4)
# R439: now the spillway the reservoir pours over into the lake (water_sample_v1.py, water_tiers.py).


# ------------------------------------------------------------------ the two boathouses (|x| 18..26)
tun = Mesh("Boat tunnels")


def sbox(m, s, d0, d1, z0, z1, y0, y1, mat, bevel=0.):
    """A box at depth d0..d1 into the tunnel on side s (x = s * (18 + d)), z0..z1 across the lanes, y0..y1 up."""
    xa, xb = sorted((s * (18 + d0), s * (18 + d1)))
    m.box((xa, z0, y0), (xb, z1, y1), mat, bevel)


for s in (-1, 1):
    out = (-s, 0, 0)          # the facade faces the crossing
    # The roof (5 .. 6.2) as a stone body with a corniced front and tiles on top, a balustrade along its front.
    sbox(tun, s, .02, 8, 33, 48.6, 5.0, 6.18, M["stone2"])
    sbox(tun, s, -.22, .3, 32.9, 48.7, 4.98, 5.24, M["stone3"], .04)        # the cornice under the roof's face
    sbox(tun, s, -.1, .1, 32.97, 48.63, 5.24, 5.96, M["stone"], .03)   # R438: its ends off the roof's
    sbox(tun, s, -.25, .35, 32.85, 48.75, 5.96, 6.215, M["stone3"], .04)     # the top cornice
    xa, xb = sorted((s * 18.35, s * 26))
    kit_paving(tun, xa, xb, 33, 48.6, 6.22)
    kit_balustrade(tun, flowers, (s * 18.15, 33.3, 0), (s * 18.15, 48.3, 0), lambda p: 6.215)
    # The eave (graybox x 18..18.6, 4.4..5): a stone lintel just proud of it, gold trims off its faces, a crown over
    # each lane.
    sbox(tun, s, -.06, .62, 33, 48.6, 4.43, 4.98, M["stone"], .04)
    sbox(tun, s, -.09, .64, 32.95, 48.65, 4.38, 4.45, GOLD, .015)
    sbox(tun, s, -.09, .0, 32.95, 48.65, 4.9, 4.96, GOLD, .01)
    for z in LANES:
        crown_badge(tun, face_frame((s * (18 - .06), z, 4.68), out), .22, M["crown_blue"])
    # The lane walls: rusticated piers at the front, block-lined sides to the back, a dark back wall per lane.
    for w0, w1 in WALLS:
        # R438 (승규 "깜빡깜박"): the mortar core 5 cm inside the pier's blocks (it was their size: its faces and theirs
        # fought in the same planes).
        sbox(tun, s, -.1, .57, w0 - .05, w1 + .05, 1.65, 4.33, M["grout"])
        n = 5
        for ci in range(n):
            y0 = 1.6 + (4.38 - 1.6) * ci / n
            y1 = 1.6 + (4.38 - 1.6) * (ci + 1) / n
            inset = .04 if ci % 2 else 0
            sbox(tun, s, -.15 + inset, .62, w0 - .1 + inset, w1 + .1 - inset, y0 + .015, y1 - .015, random.choice(STONES[:2]), .05)
        sbox(tun, s, -.19, .58, w0 - .02, w1 + .02, 4.2, 4.3, GOLD, .015)              # a gold band (R438: ends before the falls)
        for zz, sgn in ((w0, -1), (w1, 1)):
            if 33.5 < zz < 48.1:
                # The wall's side along the lane, in blocks set into the wall (the boats run flush past it).
                a0, a1 = sorted((s * 18.62, s * 26))
                # R438: 2 cm into the wall, off the plane of the graybox falls' sides (z = the wall's face) they flickered with.
                kit_wall_run(tun, "x", zz, a0, a1, .02 if sgn < 0 else -.14, .14 if sgn < 0 else -.02, 4.98, 1.6, False,
                             .62, 1.6)
    for z in LANES:
        sbox(tun, s, 7.85, 7.98, z - 1.25, z + 1.25, 1.6, 4.965, M["shade"])
        sbox(tun, s, .62, 7.9, z - 1.2, z + 1.2, 4.95, 4.975, M["shade"])           # the lane's ceiling
tun.done()

# ------------------------------------------------------------------ the gondolas (hull 6 x 2.4, deck 3.8, keel 1.55)
HALF_L, HALF_W, KEEL = 3.3, 1.2, 1.55     # the keel 0.4 m under the reservoir (1.95): the boats sit in it


def half_width(x):
    return max(.06, HALF_W * max(0., 1 - (abs(x) / HALF_L) ** 3) ** .5)


def sheer(x):
    return UPPER + .12 * (abs(x) / HALF_L) ** 2


def keel(x):
    return KEEL + .95 * (abs(x) / HALF_L) ** 3


# The hull's rows from the deck edge (u 0) to the keel (u 1); each band of colour is whole rows, so its edges follow the
# sheer instead of the triangles: the cream upper strake, a gold band, the royal blue body.
ROWS = [(0, "stone3"), (.08, "stone3"), (.17, "gold"), (.25, "blue"), (.36, "blue"), (.48, "blue"), (.6, "blue"),
        (.72, "blue"), (.84, "blue"), (.93, "blue"), (1.0, None)]


def gondola(name, bx, bz):
    m = Mesh("Boat " + name)
    xs = [HALF_L * math.sin(math.pi / 2 * t / 14) for t in range(-14, 15)]

    def ring(x):
        w, top, bot = half_width(x), sheer(x), keel(x)
        pts = [(w * max(0., 1 - u ** 2.5) ** .5, top - (top - bot) * u) for u, _ in ROWS]
        side_l = [(bx + x, bz - lat, y) for lat, y in pts]
        side_r = [(bx + x, bz + lat, y) for lat, y in reversed(pts[:-1])]
        return [m.bm.verts.new(p) for p in side_l + side_r]

    rings = [ring(x) for x in xs]
    n_rows = len(ROWS) - 1
    for ra, rb in zip(rings, rings[1:]):
        for j in range(len(ra) - 1):
            f = m.bm.faces.new((ra[j], rb[j], rb[j + 1], ra[j + 1]))
            row = j if j < n_rows else 2 * n_rows - 1 - j
            f.material_index = m.mi(M[ROWS[row][1]])
            f.smooth = True
    for r in (rings[0], rings[-1]):
        f = m.bm.faces.new(r)
        f.material_index = m.mi(M["blue"])
    # The deck, the walking surface (3.8): planks across the boat (the wood's grain runs across them), each cut to the
    # hull's outline inside the gunwale.
    x = -HALF_L + .12
    while x < HALF_L - .12:
        x1 = min(x + .3, HALF_L - .12)
        w0, w1 = half_width(x) - .1, half_width(x1) - .1
        if min(w0, w1) > .08:
            m.slab([(x + .006, -w0), (x1 - .006, -w1), (x1 - .006, w1), (x + .006, w0)], Matrix.Translation((bx, bz, 0)),
                   UPPER - .05, UPPER + .012, M["wood"])
        x = x1
    # The gunwale: a gold rail round the deck edge, just over the hull's top.
    for side in (-1, 1):
        prev = None
        for x in xs:
            p = Vector((bx + x, bz + side * (half_width(x) - .04), sheer(x) - .1))
            if prev is not None:
                beam(m, prev, p, .07, 0, .17, GOLD, 0)
            prev = p
    # Finials at both ends: a gold vase and ball, a small crown.
    for e in (-1, 1):
        at = Vector((bx + e * (HALF_L - .12), bz, sheer(HALF_L) - .02))
        m.lathe([(0, 0), (.16, 0), (.12, .06), (.07, .14), (.09, .24), (.13, .3), (.06, .36), (0, .36)], Matrix.Translation(at), GOLD, 20)
        m.sphere(at + Vector((0, 0, .44)), .09, GOLD, subdiv=2)
    # A crown on each side, mid-hull, on the cream strake (upright there).
    for side in (-1, 1):
        crown_badge(m, face_frame((bx, bz + side * 1.205, 3.56), (0, side, 0)), .18, M["crown_blue"])
    m.done()


for name, x, z in BOATS:
    gondola(name, x, z)

# ------------------------------------------------------------------ the lane ropes and buoys
ropes = Mesh("Lane ropes")
for zr in (37.2, 40.8, 44.4):
    ropes.lathe([(.035, -18), (.035, 18)], Matrix.Translation((0, zr, RES_Y + .03)) @ Matrix.Rotation(math.pi / 2, 4, "Y"), M["rope"], 8, True)
    for i, x in enumerate(range(-17, 18, 3)):
        ropes.sphere((x, zr, RES_Y + .04), .17, GOLD if i % 2 else M["white"], (1, 1, .8), 2)
ropes.done()
stone.done()
flowers.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((-4, 30, UPPER), (-9.5, 35.4, UPPER + .012), (8, 39.2, UPPER + .012), (0, 31.5, UPPER)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "BoatsSample_v1.blend"))
shoot("boats_v1", "overview", (30, 18, 24), (0, 41, 2.5), 24)
shoot("boats_v1", "quay", (-3, 28.5, 5.8), (2, 42, 3.2), 26)
shoot("boats_v1", "boat_close", (-4.5, 31.5, 5.2), (-9, 35.4, 3.4), 30)
shoot("boats_v1", "tunnel", (6, 41, 6.5), (18, 41, 4.2), 26)
shoot("boats_v1", "tunnel_lane", (12, 39, 4.6), (22, 39, 3.6), 30)
shoot("boats_v1", "on_boat", (-9.5, 34.4, 4.9), (8, 42, 3.5), 28)
print("BOATS SAMPLE DONE")
