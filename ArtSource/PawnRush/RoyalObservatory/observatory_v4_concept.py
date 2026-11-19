# The Royal Observatory as its graybox stands after R724, dressed as a concept picture: two renders, the whole hall
# and the Big Dipper's bowl from near. A picture only: nothing here goes into Unity. The layout is
# RoyalObservatoryBuilder's (section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y)):
#   the handle's variable stars (a hub and five petals), the bowl's two ways - three crossing orbits of turning
#   platforms round the west, one planet rolling to and fro straight across - the globe's tilted floor with the sun
#   lamp, the planets and their shadows, two moons on the ecliptic ring, Cassiopeia's standing star and four meshed
#   gears, the stage, the telescope with four irises and two focus beams.
#
#   blender --background --factory-startup --python observatory_v4_concept.py -- <out.png> [samples] [width] [height]
#   (the second picture is written beside the first as <out>_bowl.png)
#
# The materials and helpers are observatory_v3_concept.py's (and through it observatory_dress_preview.py's): its
# head, up to its first geometry, is run here; so are its telescope, balcony, hall and lights, a few numbers changed.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler

_v3 = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "observatory_v3_concept.py"), encoding="utf-8").read()
_CUT = "# ---------------------------------------------------------------- "
exec(compile(_v3.split(_CUT + "the entry and the Big Dipper")[0], "observatory_v3_concept.py (head)", "exec"), globals())


def v3_part(start, end, swaps=()):
    text = _v3.split(start)[1].split(end)[0]
    for old, new in swaps:
        assert old in text, old
        text = text.replace(old, new)
    exec(compile(start + text, "observatory_v3_concept.py (" + start.strip("# -") + ")", "exec"), globals())


M_SHADE = principled("MAT_PlanetShadow", (0.012, 0.016, 0.05), 0.9)
M_SHADE.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.72
PLANET_MATS = (M_PLANET, M_MARS, M_JADE, M_BRONZE)


def unit(v):
    return Vector(v).normalized()


def heading(deg):
    # A level direction by its heading (0 north, 90 east), in Blender's axes.
    return Vector((math.sin(D(deg)), math.cos(D(deg)), 0))


def yaw(dx, dz):
    return math.degrees(math.atan2(dx, dz))


def touching(a, fa, b, fb, toward):
    # The point fa from a and fb from b (x, z), on the side `toward` points to.
    ab = (b[0] - a[0], b[1] - a[1])
    d = math.hypot(*ab)
    u = (ab[0] / d, ab[1] / d)
    al = (fa * fa - fb * fb + d * d) / (2 * d)
    off = math.sqrt(max(0.0, fa * fa - al * al))
    side = (-u[1], u[0])
    if side[0] * toward[0] + side[1] * toward[1] < 0:
        side = (-side[0], -side[1])
    return (a[0] + u[0] * al + side[0] * off, a[1] + u[1] * al + side[1] * off)


def prism(name, outline, z0, z1):
    # A flat piece: an outline (x, y) between two heights.
    bm = bmesh.new()
    face = bm.faces.new([bm.verts.new((x, y, z0)) for (x, y) in outline])
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)], vec=(0, 0, z1 - z0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(None)
    return me


def ring_mesh(name, r0, r1, thick, segments=72):
    # A flat ring: its top at z 0.
    bm = bmesh.new()
    rows = []
    for (r, z) in ((r0, 0), (r1, 0), (r1, -thick), (r0, -thick)):
        rows.append([bm.verts.new((r * math.cos(2 * math.pi * i / segments), r * math.sin(2 * math.pi * i / segments), z)) for i in range(segments)])
    for j in range(4):
        a, b = rows[j], rows[(j + 1) % 4]
        for i in range(segments):
            bm.faces.new((a[i], a[(i + 1) % segments], b[(i + 1) % segments], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(None)
    return me


def put(o, center, x, y, z):
    return frame_obj(o, Vector(center), Vector(x), Vector(y), Vector(z))


# ---------------------------------------------------------------- the entry and the handle
box("Entry floor", -6, 6, -0.5, 0, 0, 14, M_MARBLE)
inlay(-2.2, 2.2, 0.5, 13.5, 0, M_RUNNER)
for s in (-1, 1):
    box("Gate post", s * 6.2, s * 7.6, 0, 9, 0, 1.6, M_NAVY)
    box("Gate cap", s * 6.0, s * 7.8, 9, 9.5, -0.2, 1.8, M_GOLD)
    box("Gate banner", s * 6.4, s * 7.4, 3.2, 8.4, 1.62, 1.7, M_PURPLE)
    rail(s * 6, 2, s * 6, 14, 0)
box("Gate lintel", -7.8, 7.8, 9.5, 11.2, 0, 1.6, M_NAVY)
for px in (-4, 4):
    pier(px, 6, -0.5)

DIP = [(-2, 0, 18, 4), (-10, 0.8, 26, 3.2), (-19, 1.8, 33, 3.2), (-27, 2.4, 43, 3.6), None, None, (-25, 3.2, 60, 4)]
HUB = 0.35
PETAL = math.cos(D(36)) * HUB


def petal_mesh(radius):
    half = D(35.3)
    outline = [(HUB * math.cos(half) - PETAL, -HUB * math.sin(half))]
    for i in range(9):
        a = -half + 2 * half * i / 8
        outline.append((radius * math.cos(a) - PETAL, radius * math.sin(a)))
    outline.append((HUB * math.cos(half) - PETAL, HUB * math.sin(half)))
    return prism("ME_Petal", outline, -0.3, 0)


ME_PETAL = petal_mesh(3.18)


def variable_star(p, toward, folds):
    # A hub 0.7 m across and five petals, the first under the line in (its heading), each at its fold (degrees).
    x, y, z = p
    cyl("Variable star hub", x, y - 0.6, y + 0.02, z, HUB * 2, M_GOLD)
    stem(x, y, z)
    for k in range(5):
        d, f = heading(toward + 72 * k), D(folds[k])
        tg, up = Vector((0, 0, 1)).cross(d), Vector((0, 0, 1))
        d2, n2 = d * math.cos(f) - up * math.sin(f), d * math.sin(f) + up * math.cos(f)
        o = inst(ME_PETAL, "Variable star petal", M_GLOW if folds[k] > 5 else M_PANEL, (0, 0, 0), (1, 1, 1))
        put(o, U(x, y - 0.01, z) + d * PETAL, d2, tg, n2)
        if folds[k] <= 5:
            bar("Petal seam", (x + d.x * 0.5, y + 0.01, z + d.y * 0.5), (x + d.x * 3.0, y + 0.01, z + d.y * 3.0), 0.07, M_GOLD)


starplat(DIP[0][:3], DIP[0][3])
starplat(DIP[3][:3], DIP[3][3])
starplat(DIP[6][:3], DIP[6][3])
# star 2 level, star 3 with the wave coming back from the way out
variable_star(DIP[1][:3], yaw(DIP[0][0] - DIP[1][0], DIP[0][2] - DIP[1][2]), (0, 0, 0, 0, 0))
variable_star(DIP[2][:3], yaw(DIP[1][0] - DIP[2][0], DIP[1][2] - DIP[2][2]), (0, 35, 84, 84, 8))
segs = []
for i in range(3):
    segs.append(cline(DIP[i][:3], DIP[i][3] - 0.2, DIP[i + 1][:3], DIP[i + 1][3] - 0.2))
cline((0, 0, 13), 0, DIP[0][:3], DIP[0][3] - 0.2, 4.0)
cline(DIP[6][:3], DIP[6][3] - 0.2, (-18.3, 4, 62.2), 0)
meteor(along(*segs[1], 0.5), False)
meteor(along(*segs[2], 0.45), True)

# ---------------------------------------------------------------- the bowl's long way: three crossing orbits
RHO, DECK, APART, TIME = 3.6, 1.45, 6.0, 1.15
S4, S7 = DIP[3], DIP[6]
o4, o7 = unit((-0.92, 0.39)), unit((-0.96, -0.28))
dock4 = (S4[0] + o4.x * (S4[3] + 0.15 + DECK), S4[2] + o4.y * (S4[3] + 0.15 + DECK))
dock7 = (S7[0] + o7.x * (S7[3] + 0.15 + DECK), S7[2] + o7.y * (S7[3] + 0.15 + DECK))
rise = Vector((dock7[0] - dock4[0], dock7[1] - dock4[1]))
SLOPE = (S7[1] - S4[1]) / rise.length


def bowl_y(x, z):
    return S4[1] + SLOPE * (Vector((x - dock4[0], z - dock4[1])).dot(rise.normalized()))


first = (dock4[0] + o4.x * RHO, dock4[1] + o4.y * RHO)
last = (dock7[0] + o7.x * RHO, dock7[1] + o7.y * RHO)
ORBITS = [first, touching(first, APART, last, APART, (-1, 0)), last]
TURN = [-1, 1, -1]
heads = [0.0]
for i in (1, 2):
    p, q = ORBITS[i - 1], ORBITS[i]
    x = touching(p, RHO, q, RHO, (0, 1))
    heads.append(yaw(x[0] - q[0], x[1] - q[1]) - TURN[i] * 60 + yaw(x[0] - p[0], x[1] - p[1]) - heads[i - 1])
RIDERS = []
for i, (cx, cz) in enumerate(ORBITS):
    cy = bowl_y(cx, cz)
    stem(cx, cy - 0.4, cz)
    ball("Orbit sun", cx, cy - 0.3, cz, 1.5, M_SUN)
    lamps.append((cx, cy + 1.4, cz))
    chain([(cx + RHO * math.sin(D(a)), bowl_y(cx + RHO * math.sin(D(a)), cz + RHO * math.cos(D(a))) - 0.45, cz + RHO * math.cos(D(a))) for a in range(0, 361, 8)], 0.12, M_GOLD)
    for k in range(3):
        h = heads[i] + 120 * k + TURN[i] * 30 * TIME
        px, pz = cx + RHO * math.sin(D(h)), cz + RHO * math.cos(D(h))
        py = bowl_y(px, pz)
        cyl("Orbit platform", px, py - 0.4, py, pz, DECK * 2, M_PANEL)
        cyl("Orbit platform skirt", px, py - 0.75, py - 0.4, pz, DECK * 2 + 0.35, M_GOLD)
        torus("Orbit platform rim", px, py + 0.02, pz, DECK - 0.08, 0.07, M_GOLD)
        inst(ME_STAR5, "Orbit platform inlay", M_GLOW, U(px, py + 0.01, pz), (1.05, 1.05, 1), (0, 0, D(-h)))
        ball("Orbit planet", px, py - 1.1, pz, 2.3, PLANET_MATS[(i + k) % 4])
        bar("Orbit arm", (cx, cy - 0.85, cz), (px, py - 0.85, pz), 0.16, M_GOLD)
        RIDERS.append((px, py, pz))

# ---------------------------------------------------------------- the bowl's short way: one rolling planet
RR, RDOCK, ACROSS = 2.2, 0.6, 0.44
fl = unit((S7[0] - S4[0], S7[2] - S4[2]))
near = U(S4[0] + fl.x * (S4[3] + RDOCK), S4[1] + 0.05 - RR, S4[2] + fl.y * (S4[3] + RDOCK))
far = U(S7[0] - fl.x * (S7[3] + RDOCK), S7[1] + 0.05 - RR, S7[2] - fl.y * (S7[3] + RDOCK))
r_along = (far - near).normalized()
r_east = r_along.cross(Vector((0, 0, 1))).normalized()
r_up = r_east.cross(r_along)
ball_at = near + (far - near) * ACROSS


def sec(v):
    return (v.x, v.z, v.y)


def rolled(v):
    # A direction fixed in the ball, after it has rolled this far: about the axis that carries its top the way it goes.
    turn = Matrix.Rotation((far - near).length * ACROSS / RR, 3, -r_east)
    return turn @ v


inst(ME_SPH, "Rolling planet", M_MARS, ball_at, (RR * 2, RR * 2, RR * 2))
for start in (r_up, r_along):
    n = rolled(start)
    put(torus("Rolling planet band", 0, 0, 0, RR + 0.02, 0.1, M_GOLD), ball_at, r_east, n.cross(r_east), n)
spot = rolled((r_east * 0.5 + r_up * 0.7 + r_along * 0.5).normalized())
inst(ME_SPH, "Rolling planet spot", M_GLOW, ball_at + spot * (RR - 0.12), (1.0, 1.0, 0.6))
# the rails it rolls on, from under the one star's rim to under the other's
low = r_up * (RR + 0.1)
r0, r1 = near - r_along * 1.2 - low, far + r_along * 1.2 - low
for s_ in (-0.8, 0.8):
    bar("Planet rail", sec(r0 + r_east * s_), sec(r1 + r_east * s_), 0.16, M_GOLD)
for k in range(3):
    at = r0 + (r1 - r0) * (k / 2)
    bar("Planet rail tie", sec(at - r_east * 0.8), sec(at + r_east * 0.8), 0.14, M_GOLD)
    inst(ME_CYL8, "Planet rail post", M_GOLD, Vector((at.x, at.y, (at.z + FLOOR) / 2)), (0.35, 0.35, at.z - FLOOR))
# where it has been: three streaks behind it, and where it stands at either star
for h in (-0.9, 0.2, 1.2):
    a = ball_at - r_along * (RR + 0.5) + r_up * h
    bar("Planet streak", sec(a), sec(a - r_along * (2.2 - abs(h) * 0.6)), 0.07, M_GLOW)
for dock in (near, far):
    put(torus("Planet dock", 0, 0, 0, 1.3, 0.05, M_GOLD), dock + r_up * (RR - 0.02), r_east, r_along, r_up)
lamps.append(sec(ball_at + r_up * (RR + 1.6)))
BALL_TOP = ball_at + r_up * RR - r_along * 0.3

# ---------------------------------------------------------------- the great globe: its floor, the sun and the shadows
GY, GR = 10.0, 14.0
WEST, EAST = (-12.65, 4.0), (13.86, 12.0)
TILT = math.atan2(EAST[1] - WEST[1], EAST[0] - WEST[0])
FX, FN, FZ = Vector((math.cos(TILT), 0, math.sin(TILT))), Vector((-math.sin(TILT), 0, math.cos(TILT))), Vector((0, 1, 0))
depth = Vector((0 - WEST[0], 0, GY - WEST[1])).dot(FN)
FMID = U(0, GY, HZ) - FN * depth
FRAD, SUNH = math.sqrt(GR * GR - depth * depth), 8.0
SUNP = FMID + FN * SUNH


def on_floor(r, deg, lift=0.0):
    return FMID + (FX * math.cos(D(deg)) + FZ * math.sin(D(deg))) * r + FN * lift


inst(ME_SPH, "Celestial globe", M_GLASS, U(0, GY, HZ), (GR * 2, GR * 2, GR * 2))
for lo in range(0, 180, 30):
    torus("Globe meridian", 0, GY, HZ, GR + 0.02, 0.05, M_GOLD, (D(90), 0, D(lo)))
for la in (-60, -30, 0, 30, 60):
    torus("Globe parallel", 0, GY + GR * math.sin(D(la)), HZ, GR * math.cos(D(la)) + 0.02, 0.05, M_GOLD)
torus("Meridian ring", 0, GY, HZ, 17.4, 0.5, M_GOLD, (0, D(90), 0))
hr = torus("Horizon ring", 0, GY, HZ, 18.2, 1.0, M_GOLD)
hr.scale = (1, 1, 0.3)
for (r, y0, y1, m) in ((26, FLOOR, -12, M_NAVY), (21, -12, -8, M_STONE), (14, -8, -5.5, M_NAVY), (9, -5.5, -3.7, M_STONE)):
    cyl("Globe base", 0, y0, y1, HZ, r, m)
    cyl("Globe base band", 0, y1 - 0.35, y1 + 0.05, HZ, r + 0.6, M_GOLD)
for k in range(8):
    a = D(22.5 + 45 * k)
    lamp(11.6 * math.cos(a), -12, HZ + 11.6 * math.sin(a), k % 2 == 0)
for (gx, gy) in ((-13.0, 6.2), (14.2, 14.2)):
    torus("Globe gate", gx, gy, HZ, 3.6, 0.32, M_GOLD, (0, D(90), 0))
box("West gate threshold", -18.5, -12.4, 3.5, 4, 60.5, 67.5, M_MARBLE)
box("East gate threshold", 13.6, 17.2, 11.5, 12, 60.5, 67.5, M_MARBLE)

put(inst(ME_CYL, "Ecliptic floor", M_MARBLE, (0, 0, 0), (1, 1, 1)), FMID - FN * 0.25, FX * (FRAD * 2), FZ * (FRAD * 2), FN * 0.5)
put(torus("Ecliptic floor rim", 0, 0, 0, FRAD - 0.1, 0.14, M_GOLD), FMID, FX, FZ, FN)
put(torus("Ecliptic ring", 0, 0, 0, 15.6, 0.3, M_BRASS), FMID, FX, FZ, FN)
for (deg, name) in ((158, "Moon A"), (338, "Moon B")):
    out = FX * math.cos(D(deg)) + FZ * math.sin(D(deg))
    put(inst(ME_CYL, name, M_MOON, (0, 0, 0), (1, 1, 1)), FMID + out * 15.6, FN.cross(out) * 6.8, FN * 6.8, out * 0.5)

sun = inst(ME_SPH, "Sun lamp", M_SUN, SUNP, (4.4, 4.4, 4.4))
sun.visible_shadow = False
bar("Sun rod", (SUNP.x, SUNP.z + 2.2, SUNP.y), (SUNP.x + FN.x * 6, SUNP.z + 8, SUNP.y), 0.3, M_GOLD)
put(torus("Sun corona", 0, 0, 0, 3.0, 0.1, M_GOLD), SUNP, FX, FN, FZ)
put(torus("Sun flare ring", 0, 0, 0, 3.6, 0.06, M_LAMP), SUNP, FX, FZ, FN)
SHADES = []
for i, (rho, h, body, angles) in enumerate(((6.0, 3.4, 1.1, (20, 140, 260)), (3.5, 3.9, 0.95, (95, 275)), (1.45, 4.5, 0.75, (40, 220)))):
    cast = SUNH / (SUNH - h)
    put(torus("Planet orbit", 0, 0, 0, rho, 0.05, M_GOLD), FMID + FN * h, FX, FZ, FN)
    put(torus("Shadow track", 0, 0, 0, rho * cast, 0.07, M_GOLD), FMID + FN * 0.03, FX, FZ, FN)
    for k, deg in enumerate(angles):
        at = on_floor(rho, deg, h)
        inst(ME_SPH, "Planet", PLANET_MATS[(i + k) % 4], at, (body * 2, body * 2, body * 2))
        arm = FMID + FN * h
        bar("Planet arm", (arm.x, arm.z, arm.y), (at.x, at.z, at.y), 0.07, M_GOLD)
        shade = on_floor(rho * cast, deg, 0.04)
        put(inst(ME_CYL, "Planet shadow", M_SHADE, (0, 0, 0), (1, 1, 1)), shade, FX * (body * cast * 2), FZ * (body * cast * 2), FN * 0.02)
        SHADES.append(shade)

# ---------------------------------------------------------------- Cassiopeia: a star that stands and four gears
CAS1 = (19.5, 12, 66.5, 3)
starplat(CAS1[:3], CAS1[3])
ne, nw = (0.6, 0.8), (-0.6, 0.8)
g1 = (CAS1[0] + ne[0] * 7, CAS1[2] + ne[1] * 7)
g2 = (g1[0] + nw[0] * 9, g1[1] + nw[1] * 9)
g3 = (g2[0] + ne[0] * 9, g2[1] + ne[1] * 9)
g4 = touching(g3, 7, (9, 90), 11, (0, 1))
GEARS = [(g1, 16), (g2, 20), (g3, 16), (g4, 12)]
ON_GEARS = []
gyaw = 0.0
for i, ((cx, cz), teeth) in enumerate(GEARS):
    if i > 0:
        (px, pz), pt = GEARS[i - 1]
        mesh = yaw(cx - px, cz - pz)
        gyaw = mesh + 180 + (360 / teeth) * (0.5 - (gyaw - mesh) / (360 / pt))
    rad = teeth / 4
    outer, width = rad - 0.08, min(1.8, rad * 0.45)
    inner, y = outer - width, 12.0
    inst(ring_mesh("ME_GearRim", inner, outer, 0.5), "Gear rim", M_PANEL, U(cx, y, cz), (1, 1, 1))
    put(torus("Gear rim edge", 0, 0, 0, outer - 0.05, 0.06, M_GOLD), U(cx, y + 0.02, cz), (1, 0, 0), (0, 1, 0), (0, 0, 1))
    put(torus("Gear rim edge", 0, 0, 0, inner + 0.05, 0.06, M_GOLD), U(cx, y + 0.02, cz), (1, 0, 0), (0, 1, 0), (0, 0, 1))
    for k in range(teeth):
        d = heading(gyaw + k * 360 / teeth)
        tg = Vector((0, 0, 1)).cross(d)
        cog = inst(ME_CUBE, "Gear cog", M_GOLD, (0, 0, 0), (1, 1, 1))
        put(cog, U(cx, y - 0.62, cz) + d * (rad - 0.05), d * 0.7, tg * 0.66, Vector((0, 0, 0.65)))
        e = heading(gyaw + (k + 0.5) * 360 / teeth)
        bar("Gear seam", (cx + e.x * (inner + 0.1), y + 0.02, cz + e.y * (inner + 0.1)), (cx + e.x * (outer - 0.1), y + 0.02, cz + e.y * (outer - 0.1)), 0.06, M_GOLD)
    cyl("Gear hub", cx, y - 0.5, y, cz, 1.8, M_GOLD)
    for k in range(4):
        d = heading(gyaw + 360 / teeth / 2 + 90 * k)
        plank("Gear spoke", (cx + d.x * 0.7, y, cz + d.y * 0.7), (cx + d.x * (inner + 0.1), y, cz + d.y * (inner + 0.1)), 0.9, 0.4, M_NAVY)
    stem(cx, y - 0.3, cz)
    lamps.append((cx, y + 1.8, cz))
    side = heading(270 if i % 2 == 0 else 90) * (outer - width / 2)
    ON_GEARS.append((cx + side.x, y, cz + side.y))
mx, my, mz = ON_GEARS[1]
meteor((mx - 0.3, my, mz + 1.6), False)

# ---------------------------------------------------------------- the observing stage
TX, TZ = 9.0, 90.0
cyl("Observing stage", TX, 11.4, 12, TZ, 16, M_MARBLE)
cyl("Observing stage skirt", TX, 10.9, 11.4, TZ, 16.6, M_GOLD)
cyl("Stage inlay", TX, 12, 12.03, TZ, 13.4, M_PANEL)
inst(ME_STAR12, "Sun motif", M_GLOW, U(TX, 12.03, TZ), (6.2, 6.2, 1))
cyl("Stage pier", TX, FLOOR, 10.9, TZ, 5.2, M_NAVY)
for h in (-17.4, -6, 4, 9.6):
    cyl("Stage pier band", TX, h - 0.4, h + 0.4, TZ, 6.0, M_GOLD)
for a in range(0, 360, 12):
    # open where the tube's deck leaves (north) and where the last gear meets the rim (east-north-east)
    if 66 < a < 114 or a < 30:
        continue
    px, pz = TX + 7.8 * math.cos(D(a)), TZ + 7.8 * math.sin(D(a))
    cyl("Stage baluster", px, 12, 13.05, pz, 0.09, M_GOLD, ME_CYL8)
    nx, nz = TX + 7.8 * math.cos(D(a + 12)), TZ + 7.8 * math.sin(D(a + 12))
    if not (66 < a + 12 < 114 or a + 12 >= 360):
        bar("Stage rail", (px, 13.05, pz), (nx, 13.05, nz), 0.1, M_GOLD)
    if a % 48 == 0:
        lamp(px, 12, pz, a % 96 == 0)

# ---------------------------------------------------------------- the telescope, the balcony, the hall (v3's)
v3_part("DK0, DK1 = (TX, 12, 93), (TX, 20, 118)", "# the balcony outside the dome, and the way on",
        (("((0.28, 0.0), (0.54, 0.55), (0.8, 1.0))", "((0.2, 0.0), (0.4, 0.55), (0.6, 1.0), (0.8, 0.25))"),
         ("((0.41, 1.1), (0.67, -1.3))", "((0.3, 1.0), (0.7, -1.1))"),
         ("(2.6, 2.6, 0.05)", "(2.4, 2.4, 0.05)")))
v3_part("# the balcony outside the dome, and the way on", _CUT + "the round hall and its star-chart dome")
v3_part(_CUT + "the round hall and its star-chart dome", _CUT + "the pieces")

# ---------------------------------------------------------------- the pieces
def floor_y(x):
    return WEST[1] + (x - WEST[0]) * (EAST[1] - WEST[1]) / (EAST[0] - WEST[0])


PAWNS = [(0, 0, 5), (-2, 0, 10), (-3, 0, 17), (-1, 0, 20), (-10.6, 0.8, 26.8), (-26, 2.4, 42), (-28.2, 2.4, 44.2), (-27.4, 2.4, 45.4),
         (-25, 3.2, 59), (-23.5, 3.2, 61.5), (-19.6, 3.7, 61.8), (-16.6, 4, 64.6), (15.4, 12, 63.4), (19.5, 12, 66.2), (18.4, 12, 67.6),
         (12, 12, 88), (7, 12, 86), (TX - 1, 13.3, 97), (TX + 1.2, 15.5, 104), (TX - 0.8, 18.1, 112), (TX, 20, 122), (TX + 2, 20, 127)]
PAWNS += [RIDERS[1], RIDERS[4], (RIDERS[4][0] + 0.7, RIDERS[4][1], RIDERS[4][2] - 0.5), RIDERS[8]]
PAWNS += [sec(BALL_TOP)]
for i in (0, 0, 3, 5):
    s = SHADES[i] + FX * random.uniform(-0.7, 0.7) + FZ * random.uniform(-0.7, 0.7)
    PAWNS.append((s.x, s.z, s.y))
for (x, z) in ((-6.5, 61.8), (4.8, 66.2)):
    PAWNS.append((x, floor_y(x), z))
PAWNS += ON_GEARS
for i, (x, y, z) in enumerate(PAWNS):
    pawn(x, y, z, i % 2)

# ---------------------------------------------------------------- light (v3's, the sun lamp moved and sharpened)
v3_part(_CUT + "light", _CUT + "camera and render",
        (('light("LGT_Sun", "POINT", U(0, 16, HZ), 26000, (1.0, 0.76, 0.42), shadow_soft_size=2.2)',
          'light("LGT_Sun", "POINT", SUNP, 120000, (1.0, 0.80, 0.50), shadow_soft_size=0.4)'),))
bowl = light("LGT_Bowl", "AREA", U(-30, 34, 50), 42000, (1.0, 0.86, 0.66), size=26)
aim(bowl, U(-30, 2, 51))

# ---------------------------------------------------------------- cameras and render
scene.render.engine = "CYCLES"
prefs = bpy.context.preferences.addons["cycles"].preferences
prefs.compute_device_type = "OPTIX"
prefs.get_devices()
for d in prefs.devices:
    d.use = d.type in ("OPTIX",)
scene.cycles.device = "GPU"
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 8
scene.cycles.transparent_max_bounces = 12
scene.cycles.volume_bounces = 0
scene.render.resolution_x = WIDTH
scene.render.resolution_y = HEIGHT
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
    try:
        scene.view_settings.look = look
        break
    except Exception:
        pass
scene.render.image_settings.file_format = "PNG"
print("OBJECTS", len(col.objects), "LAMPS", len(lamps))
SHOTS = (("", U(88, 72, -30), U(-6, 8.5, 61.5), 40, 0.32),
         ("_bowl", U(-7, 27, 29), U(-31.5, 1.5, 51.5), 32, 0.2))
ONLY = os.environ.get("OBSERVATORY_SHOT")
for (suffix, eye, target, lens, exposure) in SHOTS:
    if ONLY is not None and ONLY != suffix:
        continue
    cam_d = bpy.data.cameras.new("CAM_ObservatoryV4" + suffix)
    cam_d.lens = lens
    cam_d.clip_end = 900
    cam = bpy.data.objects.new("CAM_ObservatoryV4" + suffix, cam_d)
    col.objects.link(cam)
    cam.location = eye
    aim(cam, target)
    scene.camera = cam
    scene.view_settings.exposure = exposure
    scene.render.filepath = OUT[:-4] + suffix + ".png" if OUT.lower().endswith(".png") else OUT + suffix
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
