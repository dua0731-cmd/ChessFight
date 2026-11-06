# Royal water garden - concept-art dressing, sample 2 v2: the geyser canal (beat 2) of the v31 layout (R423, R426).
# v2 (R426): the geysers as real floor fountains (stone rosette, gold lip, basin of water over mosaic, bronze foam-jet
# nozzle; dry-deck on the ramp) - their water is GardenJet's, computed (jet_math.py).
#
# 승규 11-05: "확인 다음 코스" (the plaza v4 seen in Unity). As the plaza, every position and collider stays the v31
# graybox's (RoyalWaterGardenBuilder.Beats.cs GeyserCanal, Geyser; FountainPlaza's north apron); this only draws over
# it, in the kit's look (R419): the peach square tiles, cream block walls down to the water, stone balustrades with
# gold-balled posts and flower urns, a tall block wall with pilasters on the west, the eight geysers as gold-ringed
# grates in a stone ring (their water column and bubbles stay the graybox's moving parts).
#
# Run:  blender -b --python canal_sample_v2.py
# Out:  CanalSample_v2.blend, render_canal_v2_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Canal dressing"
os.environ["GC_SEED"] = "22"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

# ------------------------------------------------------------------ the v31 numbers (section frame, Blender x, y = section x, z)
WEST_Y = 1.8                     # the west landing's top, where the ramp arrives
WALK = (-35.0, 7.0, -18.5, -12.5)  # the canal walk: x0, x1, z0, z1, top 0
APRON = (-7.0, 7.0, -21.0, -18.5)  # the plaza's north apron (beat 1), top 0
RAMP = (-35.0, -27.0, -12.5, -1.0)  # the ramp north: x0, x1, z0, z1, top 0 -> 1.8
GEYSERS = [(-3, -17), (-8, -14), (-13, -17), (-18, -14), (-23, -17), (-28, -14), (-33, -9), (-29, -4.5)]
SLOPE = math.atan2(WEST_Y, RAMP[3] - RAMP[2])
COURSES = [(-2.25, -1.55), (-1.55, -.86), (-.86, -.17)]

M["iron"] = material("GC | iron grate", (.20, .17, .14), .45, .55)


def ramp_y(z):
    return 0.0 if z <= RAMP[2] else min(WEST_Y, WEST_Y * (z - RAMP[2]) / (RAMP[3] - RAMP[2]))


def geyser_y(z):
    return WEST_Y * (z + 12.5) / 11.5 if z > -12.5 else 0.0


# ------------------------------------------------------------------ the context (grey)
ctx = Mesh("Context blockout", CTX)
ctx.lathe([(0, DEEP), (R_PLAZA, DEEP), (R_PLAZA, -.02), (0, -.02)], Matrix.Translation((PLAZA[0], PLAZA[1], 0)), M["clay"], 96, False)
ctx.box((-36, -1, DEEP), (-27, 12, WEST_Y), M["clay"])            # the west landing (beat 3)
ctx.box((-48.5, -63, DEEP), (-46.5, 30, 7), M["clay"])            # the garden's west wall
ctx.box((-35, -18.5, DEEP), (7, -12.5, -.02), M["clay"])
ctx.box((-7, -21, DEEP), (7, -18.5, -.02), M["clay"])
ctx.done()
water = bpy.data.materials.new("Context | water")
water.use_nodes = True
_p = water.node_tree.nodes["Principled BSDF"]
_p.inputs["Base Color"].default_value = (*srgb((.02, .58, .82)), 1)
_p.inputs["Roughness"].default_value = .06
wm = Mesh("Context water", CTX)
wm.box((-46.5, -61.5, WATER - .05), (46.5, 30, WATER), water)
wm.done()

# ------------------------------------------------------------------ the floor: square tiles (4 mm under the plaza's rings)
floor = Mesh("Canal paving")


def tiled_quad(corners, uv_of):
    vs = [floor.bm.verts.new(c) for c in corners]
    f = floor.bm.faces.new(vs)
    f.normal_update()
    if f.normal.z < 0:
        f.normal_flip()
    floor.tag(vs, M["paving"])
    floor.fixed[f] = {v: uv_of(v.co) for v in vs}
    floor.no_recalc.add(f)


def flat_uv(co):
    return (co.x / (TILE_SIZE * 4), co.y / (TILE_SIZE * 4))


x0, x1, z0, z1 = WALK
# v2: the walk's tiles with a round hole (r 1 m, under the fountains' gold lips) over each basin, so its water shows.
HOLE_R, HOLE_N = 1.0, 64
outer = [floor.bm.verts.new(p) for p in ((x0, z0, -.004), (x1, z0, -.004), (x1, z1, -.004), (x0, z1, -.004))]
loops = [outer]
for gx, gz in GEYSERS:
    if gz <= -12.5:
        loops.append([floor.bm.verts.new((gx + HOLE_R * math.cos(2 * math.pi * k / HOLE_N),
                                          gz + HOLE_R * math.sin(2 * math.pi * k / HOLE_N), -.004)) for k in range(HOLE_N)])
edges = [floor.bm.edges.new((lp[i], lp[(i + 1) % len(lp)])) for lp in loops for i in range(len(lp))]
filled = bmesh.ops.triangle_fill(floor.bm, use_beauty=True, use_dissolve=False, edges=edges)["geom"]
for f in [g for g in filled if isinstance(g, bmesh.types.BMFace)]:
    f.normal_update()
    if f.normal.z < 0:
        f.normal_flip()
    floor.fixed[f] = {v: flat_uv(v.co) for v in f.verts}
    floor.no_recalc.add(f)
floor.tag([v for lp in loops for v in lp], M["paving"])
x0, x1, z0, z1 = APRON
tiled_quad([(x0, z0, -.004), (x1, z0, -.004), (x1, z1, -.004), (x0, z1, -.004)], flat_uv)
# The ramp: tiles laid along it (v = distance up the slope), so they keep their 0.6 m.
x0, x1, z0, z1 = RAMP
run = math.hypot(z1 - z0, WEST_Y)
tiled_quad([(x0, z0, 0), (x1, z0, 0), (x1, z1, WEST_Y), (x0, z1, WEST_Y)],
           lambda co: (co.x / (TILE_SIZE * 4), (co.y - z0) / (z1 - z0) * run / (TILE_SIZE * 4)))
floor.done()

# ------------------------------------------------------------------ the walls down to the water: block courses and a coping
wall = Mesh("Canal wall stones")


def block_run(axis, fixed, a0, a1, d0, d1, top_at=None):
    """Courses of blocks along `axis` ("x" or "y") from a0 to a1, d0..d1 thick across it at `fixed`; the coping's top
    follows top_at(along) (a slope), else 0."""
    # R434 (승규 11-05, "돌벽 사이사이 틈이 넓어"): 4 cm / 3 cm joints over a mortar core, nothing showing through them.
    lo = [a0, fixed + d0 + .05, COURSES[0][0]] if axis == "x" else [fixed + d0 + .05, a0, COURSES[0][0]]
    hi = [a1, fixed + d1 - .05, COURSES[-1][1] - .02] if axis == "x" else [fixed + d1 - .05, a1, COURSES[-1][1] - .02]
    wall.box(lo, hi, M["grout"])
    for ci, (zb, zt) in enumerate(COURSES):
        a = a0 - (.75 if ci % 2 else 0)
        while a < a1:
            s, e = max(a, a0), min(a + 1.5, a1)
            a += 1.5
            if e - s < .3:
                continue
            lo = [s + .02, fixed + d0, zb + .015] if axis == "x" else [fixed + d0, s + .02, zb + .015]
            hi = [e - .02, fixed + d1, zt - .015] if axis == "x" else [fixed + d1, e - .02, zt - .015]
            wall.box(lo, hi, random.choice(STONES), .035)
    # The coping (0.75 m pieces); on a slope each piece's top follows it.
    a = a0
    while a < a1 - 1e-3:
        s, e = a, min(a + .75, a1)
        a = e
        t0 = top_at(s) if top_at else 0.0
        t1 = top_at(e) if top_at else 0.0
        c0, c1 = fixed + d0 - .05, fixed + d1 + .08
        if axis == "x":
            pts = [(s + .02, c0), (e - .02, c0), (e - .02, c1), (s + .02, c1)]
            tops = [t0, t1, t1, t0]
        else:
            pts = [(c0, s + .02), (c0, e - .02), (c1, e - .02), (c1, s + .02)]
            tops = [t0, t1, t1, t0]
        lo = [wall.bm.verts.new((p[0], p[1], top - .17)) for p, top in zip(pts, tops)]
        hi = [wall.bm.verts.new((p[0], p[1], top + .012)) for p, top in zip(pts, tops)]
        wall.bm.faces.new(list(reversed(lo)))
        wall.bm.faces.new(hi)
        for i in range(4):
            j = (i + 1) % 4
            wall.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        edges = {e2 for v in hi for e2 in v.link_edges}
        wall.tag(lo + hi, M["stone3"], False, edges, .04)


# South edge of the walk (its rail stands outside it, z -18.8..-18.5), the north edge (rail z -12.5..-12.2), the east end.
block_run("x", -18.5, -35, -7, -.35, 0)
block_run("x", -12.5, -27, 7, 0, .35)
block_run("y", 7, -18.5, -12.15, 0, .35)
# The ramp's east side: the courses up to the water line's top course, a sloped coping on the slope.
block_run("y", -27, -12.15, -1, -.3, .05, ramp_y)
# Under the ramp's sloped coping, a stone face from the top course (0) up to it.
x0 = -27.0
z = -12.15
while z < -1:
    e = min(z + 1.5, -1)
    t0, t1 = ramp_y(z) - .17, ramp_y(e) - .17
    if t1 > .02:
        pts = [(x0 - .3, z + .05), (x0 - .3, e - .05), (x0 + .05, e - .05), (x0 + .05, z + .05)]
        lo = [wall.bm.verts.new((p[0], p[1], -.15)) for p in pts]
        hi = [wall.bm.verts.new((p[0], p[1], max(-.14, top))) for p, top in zip(pts, (t0, t1, t1, t0))]
        wall.bm.faces.new(list(reversed(lo)))
        wall.bm.faces.new(hi)
        for i in range(4):
            j = (i + 1) % 4
            wall.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        wall.tag(lo + hi, random.choice(STONES), False, {e2 for v in lo + hi for e2 in v.link_edges}, .04)
    z = e
wall.done()

# ------------------------------------------------------------------ the tall west wall (x -35.6..-35, z -18.8..8.3, up to 3.2)
tall = Mesh("Canal west wall")
W0, W1 = -35.6, -35.0
TOP = WEST_Y + 1.4
rows = [(-2.25 + k * .5, -2.25 + (k + 1) * .5) for k in range(int((TOP - .25 + 2.25) / .5))]
tall.box((W0 + .05, -18.8, -2.25), (W1 - .05, 8.3, TOP - .25), M["grout"])   # R434: the mortar core behind the joints
for ri, (zb, zt) in enumerate(rows):
    y = -18.8 - (.6 if ri % 2 else 0)
    while y < 8.3:
        s, e = max(y, -18.8), min(y + 1.2, 8.3)
        y += 1.2
        if e - s > .3:
            tall.box((W0 - .02, s + .025, zb + .025), (W1 + .02, e - .025, min(zt, TOP - .25) - .025), random.choice(STONES[:2]), .045)
tall.box((W0 - .1, -18.8, TOP - .25), (W1 + .1, 8.3, TOP - .12), M["stone3"], .04)
tall.box((W0 - .06, -18.8, TOP - .12), (W1 + .06, 8.3, TOP), M["stone3"], .05)
for yp in [-18.8 + k * 4.5 for k in range(7)]:
    # A pilaster on the walk's side with a gold ball on top.
    tall.box((W1, yp - .3, -.17), (W1 + .16, yp + .3, TOP - .25), M["stone"], .04)
    tall.box((W0 - .05, yp - .36, TOP), (W1 + .2, yp + .36, TOP + .12), M["stone3"], .04)
    tall.lathe([(0, 0), (.16, 0), (.12, .06), (0, .06)], Matrix.Translation(((W0 + W1) / 2 + .08, yp, TOP + .12)), GOLD, 16, False)
    tall.sphere(((W0 + W1) / 2 + .08, yp, TOP + .32), .15, GOLD, subdiv=3)
tall.done()

# ------------------------------------------------------------------ the balustrades (the graybox's rails: 1.1 m, 0.3 m wide)
rail = Mesh("Canal balustrade")
flowers = Mesh("Canal flowers")


def beam(m, a, b, half_w, z_lo, z_hi, mat, bevel=.03):
    """A straight piece from a to b (x, y, base z), half_w either side, z_lo..z_hi above the base line."""
    a, b = Vector(a), Vector(b)
    d = Vector((b.x - a.x, b.y - a.y, 0)).normalized()
    s = Vector((-d.y, d.x, 0)) * half_w
    pts = [a - s, b - s, b + s, a + s]
    lo = [m.bm.verts.new((p.x, p.y, p.z + z_lo)) for p in pts]
    hi = [m.bm.verts.new((p.x, p.y, p.z + z_hi)) for p in pts]
    m.bm.faces.new(list(reversed(lo)))
    m.bm.faces.new(hi)
    for i in range(4):
        j = (i + 1) % 4
        m.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    return m.tag(lo + hi, mat, False, {e for v in lo + hi for e in v.link_edges}, bevel)


def urn(m, at):
    """A small gold urn of flowers on a post."""
    m.lathe([(0, 0), (.09, 0), (.1, .03), (.06, .06), (.06, .1), (.13, .14), (.22, .22), (.24, .27), (.21, .29), (0, .29)],
            Matrix.Translation(at), GOLD, 24)
    c = Vector(at) + Vector((0, 0, .34))
    flowers.bush(c, .24, 18, [M["leaf2"], M["leaf"]], .7)
    for _ in range(7):
        t = random.uniform(0, 2 * math.pi)
        up = random.uniform(.45, 1.0)
        out = Vector((math.cos(t) * math.sqrt(1 - up * up), math.sin(t) * math.sqrt(1 - up * up), up))
        p = c + Vector((out.x * .23, out.y * .23, out.z * .23 * .7)) + out * .02
        flowers.flower(p, random.uniform(.07, .09), random.choice(FLOWERS[:4]), out + Vector((0, 0, .4)))


def balustrade(a, b, height_at, urns=True, skip_first=False):
    """Posts every ~3.3 m (a gold ball, or every other one a gold urn of flowers), turned balusters every 0.3 m, a
    plinth and a handrail - all following height_at(point) along a -> b."""
    a, b = Vector(a), Vector(b)
    length = (b - a).length
    n_posts = max(1, round(length / 3.3))
    pts = [a + (b - a) * (k / n_posts) for k in range(n_posts + 1)]
    for p in pts:
        p.z = height_at(p)
    for k, (p, q) in enumerate(zip(pts, pts[1:])):
        beam(rail, p, q, .2, 0, .2, M["stone2"])
        beam(rail, p, q, .2, .86, .98, M["stone"])
        beam(rail, p, q, .23, .98, 1.06, M["stone3"])
        seg = (q - p).length
        n = max(1, int((seg - .5) / .3))
        for i in range(n):
            c = p + (q - p) * ((.25 + (seg - .5) * (i + .5) / n) / seg)
            rail.lathe([(.07, .2), (.08, .24), (.05, .3), (.06, .38), (.11, .52), (.09, .66), (.05, .74), (.06, .8), (.09, .86)],
                       Matrix.Translation((c.x, c.y, c.z)), M["stone"], 12)
    for k, p in enumerate(pts):
        if skip_first and k == 0:
            continue  # the post at a corner is the other run's
        rail.box((p.x - .3, p.y - .3, p.z), (p.x + .3, p.y + .3, p.z + .22), M["stone2"], .04)
        rail.box((p.x - .24, p.y - .24, p.z + .22), (p.x + .24, p.y + .24, p.z + 1.2), M["stone"], .04)
        rail.box((p.x - .3, p.y - .3, p.z + 1.2), (p.x + .3, p.y + .3, p.z + 1.3), M["stone3"], .04)
        if urns and k % 2 == 1:
            urn(rail, (p.x, p.y, p.z + 1.3))
        else:
            rail.lathe([(0, 1.3), (.09, 1.3), (.05, 1.37), (0, 1.37)], Matrix.Translation((p.x, p.y, p.z)), GOLD, 16)
            rail.sphere((p.x, p.y, p.z + 1.52), .16, GOLD, subdiv=3)


flat = lambda p: 0.0  # noqa: E731
balustrade((-35, -18.65, 0), (-7, -18.65, 0), flat)
balustrade((-27, -12.35, 0), (7, -12.35, 0), flat)
balustrade((7.15, -18.5, 0), (7.15, -12.2, 0), flat, False)
balustrade((-27.15, -12.35, 0), (-27.15, -1, 0), lambda p: ramp_y(p.y), True, True)
rail.done()

# ------------------------------------------------------------------ v2: the eight geysers as real floor fountains (R426)
# 승규 11-05 (the v1 grate in Unity): "디자인을 다른 걸로 하고 실제 물이 나오는 건축 그걸로 해주고". A floor fountain as
# built in a plaza: a stone rosette kerb flush with the paving, a gold lip, a shallow basin of water over a teal
# mosaic, a bronze foam-jet nozzle in its middle (air holes round a dark mouth: the jet entrains air, which is why it
# stands white). On the ramp the water could not lie level, so those two are dry-deck fountains: the nozzle in a
# slotted bronze drain plate. The jet itself is GardenJet's (jet_math.py): only the fixture is modelled here.
M["bronze"] = material("GC | bronze", (.62, .42, .2), .35, .85)
M["mouth"] = material("GC | nozzle dark", (.04, .05, .06), .5)
M["mosaic_teal"] = material("GC | mosaic teal", (.2, .62, .7), .4)
M["mosaic_deep"] = material("GC | mosaic deep", (.1, .38, .55), .4)
M["mosaic_light"] = material("GC | mosaic light", (.6, .85, .88), .4)
M["basin_water"] = material("GC | basin water", (.55, .85, .95), .05)
_bw = M["basin_water"].node_tree.nodes["Principled BSDF"]
_bw.inputs["Alpha"].default_value = .45
for attr, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
    try:
        setattr(M["basin_water"], attr, value)
    except Exception:
        pass

FLOOR_TOP = .002     # the fixture's frame sits 2 mm over the floor
POOL_TOP = -.012     # the basin's water, under the floor
BASIN_FLOOR = -.07
NOZZLE_TOP = .045    # the nozzle's mouth over the floor (= GardenJet.JetZ0 + the group's 2 cm)
geys = Mesh("Canal geysers")


def rosette(frame):
    """A kerb of 16 rounded lobes round a 1.04 m opening, its top flush with the paving."""
    n = 128
    rows = []
    for i in range(n + 1):
        ph = 2 * math.pi * i / n
        r_out = 1.18 + .12 * (.5 + .5 * math.cos(16 * ph)) ** .7
        c, s = math.cos(ph), math.sin(ph)
        rows.append([frame @ Vector((r * c, r * s, z)) for r, z in
                     ((1.04, -.02), (1.04, .016), (r_out - .035, .018), (r_out, .004), (r_out, -.03))])
    verts = [[geys.bm.verts.new(p) for p in row] for row in rows]
    for i in range(n):
        for j in range(4):
            geys.bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
    geys.tag([v for row in verts for v in row], M["stone3"], True)
    # A gold stud at every other lobe's tip.
    for k in range(8):
        ph = 2 * math.pi * k / 8
        geys.sphere(frame @ Vector((1.22 * math.cos(ph), 1.22 * math.sin(ph), .018)), .035, GOLD, (1, 1, .55), 1)


def nozzle(frame, seat):
    """The bronze foam-jet nozzle standing on `seat` (the basin floor or the drain plate)."""
    geys.lathe([(0, seat), (.42, seat), (.42, seat + .02), (.35, seat + .03), (.34, NOZZLE_TOP - .03), (.36, NOZZLE_TOP - .015),
                (.34, NOZZLE_TOP), (.27, NOZZLE_TOP), (.24, NOZZLE_TOP - .006), (0, NOZZLE_TOP - .006)], frame, M["bronze"], 40, True)
    geys.lathe([(0, NOZZLE_TOP - .05), (.225, NOZZLE_TOP - .05), (.225, NOZZLE_TOP - .004), (0, NOZZLE_TOP - .004)], frame, M["mouth"], 32, False)
    geys.lathe([(.225, NOZZLE_TOP - .004), (.26, NOZZLE_TOP - .004), (.26, NOZZLE_TOP + .004), (.225, NOZZLE_TOP + .004)],
               frame, GOLD, 32, False, True)
    for k in range(12):
        ph = 2 * math.pi * (k + .5) / 12
        geys.lathe([(0, NOZZLE_TOP - .01), (.018, NOZZLE_TOP - .01), (.018, NOZZLE_TOP + .001), (0, NOZZLE_TOP + .001)],
                   frame @ Matrix.Translation((.305 * math.cos(ph), .305 * math.sin(ph), 0)), M["mouth"], 8, False)


for gx, gz in GEYSERS:
    on_ramp = gz > -12.5
    y = geyser_y(gz)
    frame = Matrix.Translation((gx, gz, y + FLOOR_TOP))
    if on_ramp:
        frame = frame @ Matrix.Rotation(SLOPE, 4, "X")  # lie on the ramp
    rosette(frame)
    geys.lathe([(.97, -.005), (1.05, -.005), (1.05, .025), (1.03, .036), (.99, .036), (.97, .025)], frame, GOLD, 64, True, True)
    if not on_ramp:
        # The basin: a stone wall, a mosaic floor of three rings of small tiles, the water over it.
        geys.lathe([(.96, BASIN_FLOOR), (.975, BASIN_FLOOR), (.975, 0), (.96, 0)], frame, M["stone2"], 64, False, True)
        for r0, r1, n, mat in ((0, .45, 1, M["mosaic_deep"]), (.45, .75, 28, M["mosaic_light"]), (.75, .96, 40, M["mosaic_teal"])):
            if n == 1:
                geys.lathe([(0, BASIN_FLOOR - .01), (r1, BASIN_FLOOR - .01), (r1, BASIN_FLOOR), (0, BASIN_FLOOR)], frame, mat, 40, False)
                continue
            for k in range(n):
                a0, a1 = 360 * k / n, 360 * (k + 1) / n
                v = geys.arc(r0 + .012, r1 - .012, a0 + .9, a1 - .9, BASIN_FLOOR - .01, BASIN_FLOOR + .006, mat, center=(0, 0), segs=2)
                for vv in v:
                    vv.co = frame @ vv.co
        geys.lathe([(0, POOL_TOP), (.965, POOL_TOP), (.965, POOL_TOP + .001), (0, POOL_TOP + .001)], frame, M["basin_water"], 64, False)
        nozzle(frame, BASIN_FLOOR)
    else:
        # A dry-deck fountain: a bronze drain plate with radial slots, the nozzle in its middle.
        geys.lathe([(0, -.01), (.97, -.01), (.97, .006), (0, .006)], frame, M["bronze"], 64, False)
        for k in range(24):
            rot = frame @ Matrix.Rotation(2 * math.pi * (k + .5) / 24, 4, "Z")
            v = geys.box((.5, -.018, .004), (.9, .018, .008), M["mouth"], .004)
            for vv in v:
                vv.co = rot @ vv.co
        nozzle(frame, 0.0)
geys.done()
flowers.done()


# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, pz in ((-2, -15.5), (-15, -15.5), (-31, -8)):
    pawn(scale, px, pz, ramp_y(pz) if px < -27 else 0)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "CanalSample_v2.blend"))
shoot("canal_v2", "overview", (8, -32, 13), (-15, -12, 0), 26)
shoot("canal_v2", "player", (2, -21.5, 2.3), (-18, -15.5, .5), 26)
shoot("canal_v2", "geyser_close", (-5.6, -17.6, 1.5), (-8, -14, -.1), 34)
shoot("canal_v2", "ramp_fountain", (-31.5, -11.5, 2.2), (-33, -9, .4), 34)
shoot("canal_v2", "ramp", (-20, -17, 4.5), (-31, -6, 1), 28)
print("CANAL SAMPLE DONE")
