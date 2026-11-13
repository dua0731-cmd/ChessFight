# Royal water garden - concept-art dressing, sample 2 v1: the geyser canal (beat 2) of the v31 layout (R423).
#
# 승규 11-05: "확인 다음 코스" (the plaza v4 seen in Unity). As the plaza, every position and collider stays the v31
# graybox's (RoyalWaterGardenBuilder.Beats.cs GeyserCanal, Geyser; FountainPlaza's north apron); this only draws over
# it, in the kit's look (R419): the peach square tiles, cream block walls down to the water, stone balustrades with
# gold-balled posts and flower urns, a tall block wall with pilasters on the west, the eight geysers as gold-ringed
# grates in a stone ring (their water column and bubbles stay the graybox's moving parts).
#
# Run:  blender -b --python canal_sample_v1.py
# Out:  CanalSample_v1.blend, render_canal_v1_*.png (next to this file)

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
tiled_quad([(x0, z0, -.004), (x1, z0, -.004), (x1, z1, -.004), (x0, z1, -.004)], flat_uv)
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
    for ci, (zb, zt) in enumerate(COURSES):
        a = a0 - (.75 if ci % 2 else 0)
        while a < a1:
            s, e = max(a, a0), min(a + 1.5, a1)
            a += 1.5
            if e - s < .3:
                continue
            lo = [s + .06, fixed + d0, zb + .03] if axis == "x" else [fixed + d0, s + .06, zb + .03]
            hi = [e - .06, fixed + d1, zt - .03] if axis == "x" else [fixed + d1, e - .06, zt - .03]
            wall.box(lo, hi, random.choice(STONES), .06)
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

# ------------------------------------------------------------------ the eight geysers: a stone ring, a gold ring, a dark grate
geys = Mesh("Canal geysers")
for gx, gz in GEYSERS:
    y = geyser_y(gz)
    frame = Matrix.Translation((gx, gz, y + .002))
    if gz > -12.5:
        frame = frame @ Matrix.Rotation(SLOPE, 4, "X")  # lie on the ramp
    geys.lathe([(1.05, 0), (1.32, 0), (1.32, .012), (1.27, .03), (1.08, .03), (1.05, .015)], frame, M["stone3"], 48, False, True)
    geys.lathe([(.96, -.005), (1.07, -.005), (1.07, .03), (1.04, .05), (.98, .05), (.96, .03)], frame, GOLD, 48, True, True)
    geys.lathe([(0, -.01), (.97, -.01), (.97, .006), (0, .006)], frame, M["iron"], 48, False)
    for k in range(12):
        rot = frame @ Matrix.Rotation(2 * math.pi * k / 12, 4, "Z")
        v = geys.box((.18, -.025, .006), (.96, .025, .03), GOLD, .008)
        for vv in v:
            vv.co = rot @ vv.co
    geys.lathe([(.32, .006), (.38, .006), (.38, .03), (.32, .03)], frame, GOLD, 32, False, True)
    geys.lathe([(.62, .006), (.68, .006), (.68, .03), (.62, .03)], frame, GOLD, 32, False, True)
    geys.lathe([(0, .006), (.17, .006), (.15, .05), (.08, .07), (0, .075)], frame, GOLD, 24)
geys.done()
flowers.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, pz in ((-2, -15.5), (-15, -15.5), (-31, -8)):
    pawn(scale, px, pz, ramp_y(pz) if px < -27 else 0)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "CanalSample_v1.blend"))
shoot("canal_v1", "overview", (8, -32, 13), (-15, -12, 0), 26)
shoot("canal_v1", "player", (2, -21.5, 2.3), (-18, -15.5, .5), 26)
shoot("canal_v1", "geyser_close", (-4.5, -20.5, 2.2), (-8, -15, 0), 32)
shoot("canal_v1", "ramp", (-20, -17, 4.5), (-31, -6, 1), 28)
print("CANAL SAMPLE DONE")
