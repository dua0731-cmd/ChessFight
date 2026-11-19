# The Royal Observatory's structure v0.3 with its new obstacles (R716, R717), dressed as a concept picture (R718).
# A picture only: nothing here goes into Unity. Layout: PAWN_RUSH_ROYAL_OBSERVATORY_STRUCTURE_v0.3.md
# (section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y)).
#
#   blender --background --factory-startup --python observatory_v3_concept.py -- <out.png> [samples] [width] [height]
#
# The materials and the box / cylinder / bar / lamp / pawn helpers are observatory_dress_preview.py's: its head, up to
# its first geometry, is run here (it also reads the arguments and empties the scene).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler

_head = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "observatory_dress_preview.py"), encoding="utf-8").read()
exec(compile(_head.split("# entry, ring court")[0], "observatory_dress_preview.py (head)", "exec"), globals())

HX, HZ, HR, FLOOR, WALLTOP = 0.0, 64.0, 46.0, -18.0, 30.0
D = math.radians


# ---------------------------------------------------------------- more materials
def glass(name, tint):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    mix = nt.nodes.new("ShaderNodeMixShader")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    tr.inputs["Color"].default_value = (*tint, 1)
    gl = nt.nodes.new("ShaderNodeBsdfGlossy")
    gl.inputs["Roughness"].default_value = 0.04
    fr = nt.nodes.new("ShaderNodeFresnel")
    fr.inputs["IOR"].default_value = 1.25
    add = nt.nodes.new("ShaderNodeMath")
    add.operation = "ADD"
    add.inputs[1].default_value = 0.05
    nt.links.new(fr.outputs[0], add.inputs[0])
    nt.links.new(add.outputs[0], mix.inputs[0])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(gl.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    return m


M_GLASS = glass("MAT_GlobeGlass", (0.78, 0.90, 1.0))
M_GLOW = principled("MAT_StarGlow", (0.95, 0.62, 0.18), 0.25, 1.0, (1.0, 0.55, 0.15), 0.9)
M_BEAM = principled("MAT_Beam", (1, 0.8, 0.45), 0.5, 0.0, (1.0, 0.66, 0.28), 2.0)
M_BEAM.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.11
M_DOME = starry("MAT_Dome", (0.016, 0.05, 0.22), (1.0, 0.9, 0.65), 0.11)
M_LINE = principled("MAT_Line", (0.55, 0.75, 0.95), 0.25, 0.0, (0.35, 0.6, 1.0), 0.5)
M_BRONZE = principled("MAT_Bronze", (0.50, 0.27, 0.10), 0.35, 1.0)
M_MARS = principled("MAT_Mars", (0.62, 0.20, 0.08), 0.6)
M_JADE = principled("MAT_Jade", (0.10, 0.42, 0.30), 0.5)
M_SUN = principled("MAT_SunLamp", (1, 0.7, 0.35), 0.3, 0.0, (1.0, 0.55, 0.18), 9.0)
M_MOON = principled("MAT_Moon", (0.78, 0.76, 0.70), 0.7, 0.0, (0.9, 0.9, 1.0), 0.25)


# ---------------------------------------------------------------- more helpers
def frame_obj(o, center, x, y, z):
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = center
    o.matrix_world = m
    return o


def plank(name, a, b, width, thick, mat):
    A, B = U(*a), U(*b)
    d = (B - A)
    y = d.normalized()
    x = y.cross(Vector((0, 0, 1))).normalized()
    z = x.cross(y)
    o = inst(ME_CUBE, name, mat, (0, 0, 0), (1, 1, 1))
    frame_obj(o, (A + B) / 2 - z * (thick / 2), x * width, y * d.length, z * thick)
    return o


def star_mesh(name, n, inner):
    bm = bmesh.new()
    vs = []
    for i in range(n * 2):
        a = math.pi * i / n + math.pi / 2
        r = inner if i % 2 else 1.0
        vs.append(bm.verts.new((r * math.cos(a), r * math.sin(a), 0)))
    face = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)], vec=(0, 0, 0.05))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(None)
    return me


ME_STAR5 = star_mesh("ME_Star5", 5, 0.42)
ME_STAR12 = star_mesh("ME_Star12", 12, 0.55)


def stem(x, y, z):
    cyl("Star stem", x, FLOOR, y - 0.9, z, 0.5, M_GOLD, ME_CYL8)
    cyl("Star stem foot", x, FLOOR, FLOOR + 0.6, z, 3.2, M_GOLD)
    torus("Star knuckle", x, y - 2.4, z, 1.2, 0.1, M_GOLD)
    torus("Star knuckle", x, y - 2.4, z, 1.0, 0.08, M_GOLD, (D(90), 0, D(30)))


def starplat(p, r, lit=True):
    x, y, z = p
    cyl("Star platform", x, y - 0.5, y, z, r * 2, M_PANEL)
    cyl("Star platform skirt", x, y - 0.95, y - 0.5, z, r * 2 + 0.5, M_GOLD)
    torus("Star platform rim", x, y + 0.02, z, r - 0.1, 0.09, M_GOLD)
    inst(ME_STAR5, "Star inlay", M_GLOW, U(x, y + 0.01, z), (r * 0.8, r * 0.8, 1), (0, 0, random.random() * 1.2))
    stem(x, y, z)
    if lit:
        lamps.append((x, y + 1.6, z))


def pulsing(p, r, folds):
    # A variable star: its middle, and five petals at their folds (degrees).
    x, y, z = p
    cyl("Variable star core", x, y - 0.5, y, z, 3.2, M_PANEL)
    inst(ME_STAR5, "Variable star inlay", M_GLOW, U(x, y + 0.01, z), (1.3, 1.3, 1))
    torus("Variable star core rim", x, y + 0.02, z, 1.6, 0.08, M_GOLD)
    stem(x, y, z)
    length = r - 1.5
    for k in range(5):
        a, f = D(18 + 72 * k), D(folds[k])
        d = Vector((math.cos(a), math.sin(a), 0))
        tg = Vector((-math.sin(a), math.cos(a), 0))
        up = Vector((0, 0, 1))
        d2 = d * math.cos(f) - up * math.sin(f)
        n2 = d * math.sin(f) + up * math.cos(f)
        hinge = U(x, y, z) + d * 1.55
        o = inst(ME_CUBE, "Variable star petal", M_GLOW if folds[k] > 5 else M_PANEL, (0, 0, 0), (1, 1, 1))
        frame_obj(o, hinge + d2 * (length / 2) - n2 * 0.22, d2 * length, tg * 2.3, n2 * 0.44)
        e = inst(ME_CUBE, "Variable star petal edge", M_GOLD, (0, 0, 0), (1, 1, 1))
        frame_obj(e, hinge + d2 * (length - 0.06) - n2 * 0.22, d2 * 0.12, tg * 2.4, n2 * 0.5)


def cline(a, ra, b, rb, width=3.0):
    # A constellation line between two stars' edges: a pale glass plank, brass edges, a line of light down it.
    dx, dz = b[0] - a[0], b[2] - a[2]
    h = math.hypot(dx, dz)
    ux, uz = dx / h, dz / h
    A = (a[0] + ux * ra, a[1], a[2] + uz * ra)
    B = (b[0] - ux * rb, b[1], b[2] - uz * rb)
    plank("Constellation line", A, B, width, 0.3, M_LINE)
    px, pz = -uz * (width / 2), ux * (width / 2)
    for s in (-1, 1):
        bar("Line edge", (A[0] + px * s, A[1] - 0.05, A[2] + pz * s), (B[0] + px * s, B[1] - 0.05, B[2] + pz * s), 0.22, M_GOLD)
    bar("Line light", (A[0], A[1] + 0.03, A[2]), (B[0], B[1] + 0.03, B[2]), 0.16, M_GLOW)
    return A, B


def along(A, B, t):
    return tuple(A[i] + (B[i] - A[i]) * t for i in range(3))


def meteor(at, warn):
    x, y, z = at
    torus("Meteor ring", x, y + 0.06, z, 1.4, 0.1, M_LAMP)
    if not warn:
        head, top = U(x + 1.2, y + 3.2, z + 0.8), U(x + 6.2, y + 16.5, z + 4.1)
        trail = inst(ME_CONE, "Meteor trail", M_BEAM, (head + top) / 2, (1.5, 1.5, (top - head).length))
        trail.rotation_euler = (top - head).to_track_quat("Z", "Y").to_euler()
        bar("Meteor trail core", (x + 1.2, y + 3.2, z + 0.8), (x + 2.6, y + 6.9, z + 1.7), 0.2, M_LAMP)
        ball("Meteor", x + 1.0, y + 2.7, z + 0.7, 1.1, M_LAMP)
        lamps.append((x + 1.0, y + 3.0, z + 0.7))


def chain(points, thick, mat):
    for i in range(len(points) - 1):
        bar("Rib", points[i], points[i + 1], thick, mat)


# ---------------------------------------------------------------- the entry and the Big Dipper
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

DIP = [(-2, 0, 18, 4), (-10, 0.8, 26, 3.2), (-19, 1.8, 33, 3.2), (-27, 2.4, 43, 3.6), (-38, 1.2, 49, 3.2), (-37, 2.2, 62, 3.6), (-25, 3.2, 60, 4)]
FOLDS = {1: (0, 0, 0, 22, 60), 2: (75, 75, 75, 75, 40), 4: (0, 0, 0, 0, 0)}
for i, s in enumerate(DIP):
    if i in FOLDS:
        pulsing(s[:3], s[3], FOLDS[i])
    else:
        starplat(s[:3], s[3])
segs = []
for i in range(6):
    segs.append(cline(DIP[i][:3], DIP[i][3] - 0.2, DIP[i + 1][:3], DIP[i + 1][3] - 0.2))
cline((0, 0, 13), 0, DIP[0][:3], DIP[0][3] - 0.2, 4.0)
cline(DIP[6][:3], DIP[6][3] - 0.2, (-12.4, 4, 63.7), 0)
meteor(along(*segs[4], 0.45), False)
meteor(along(*segs[3], 0.6), True)

# the comet across the bowl
ca, cb = DIP[3], DIP[6]
cpos = (ca[0] + (cb[0] - ca[0]) * 0.55, ca[1] + (cb[1] - ca[1]) * 0.55, ca[2] + (cb[2] - ca[2]) * 0.55)
for t in range(1, 12):
    q = along(ca[:3], cb[:3], 0.2 + 0.6 * t / 12)
    ball("Comet path dot", q[0], q[1] - 0.2, q[2], 0.22, M_GOLD)
cyl("Comet", cpos[0], cpos[1] - 0.4, cpos[1], cpos[2], 4.4, M_PANEL)
torus("Comet rim", cpos[0], cpos[1] + 0.02, cpos[2], 2.15, 0.1, M_GOLD)
inst(ME_STAR5, "Comet inlay", M_GLOW, U(cpos[0], cpos[1] + 0.01, cpos[2]), (1.5, 1.5, 1))
back = Vector((ca[0] - cb[0], 0, ca[2] - cb[2])).normalized()
tail = inst(ME_CONE, "Comet tail", M_BEAM, U(cpos[0] + back.x * 6.5, cpos[1] - 0.1, cpos[2] + back.z * 6.5), (3.4, 3.4, 9.0))
tail.rotation_euler = Vector((back.x, back.z, 0.02)).to_track_quat("Z", "Y").to_euler()
lamps.append((cpos[0], cpos[1] + 1.2, cpos[2]))

# ---------------------------------------------------------------- the great celestial globe
GY, GR = 10.0, 14.0
gl = inst(ME_SPH, "Celestial globe", M_GLASS, U(0, GY, HZ), (GR * 2, GR * 2, GR * 2))
for lo in range(0, 180, 30):
    torus("Globe meridian", 0, GY, HZ, GR + 0.02, 0.05, M_GOLD, (D(90), 0, D(lo)))
for la in (-60, -30, 0, 30, 60):
    torus("Globe parallel", 0, GY + GR * math.sin(D(la)), HZ, GR * math.cos(D(la)) + 0.02, 0.05, M_GOLD)
torus("Meridian ring", 0, GY, HZ, 17.4, 0.5, M_GOLD, (0, D(90), 0))
hr = torus("Horizon ring", 0, GY, HZ, 18.2, 1.0, M_GOLD)
hr.scale = (1, 1, 0.3)
torus("Ecliptic ring", 0, GY, HZ, 15.6, 0.3, M_BRASS, (0, D(-12.8), 0))
ma = D(205)
mo = Vector((15.6 * math.cos(ma), 15.6 * math.sin(ma), 0))
mo.rotate(Euler((0, D(-12.8), 0)))
moon = inst(ME_CYL, "Moon disc", M_MOON, U(0, GY, HZ) + mo, (6.8, 6.8, 0.6))
moon.rotation_euler = Vector((mo.x, mo.y, 0)).to_track_quat("Z", "Y").to_euler()
for (r, y0, y1, m) in ((26, FLOOR, -12, M_NAVY), (21, -12, -8, M_STONE), (14, -8, -5.5, M_NAVY), (9, -5.5, -3.7, M_STONE)):
    cyl("Globe base", 0, y0, y1, HZ, r, m)
    cyl("Globe base band", 0, y1 - 0.35, y1 + 0.05, HZ, r + 0.6, M_GOLD)
for k in range(8):
    a = D(22.5 + 45 * k)
    lamp(11.6 * math.cos(a), -12, HZ + 11.6 * math.sin(a), k % 2 == 0)
for (gx, gy) in ((-13.0, 6.6), (14.2, 14.6)):
    torus("Globe gate", gx, gy, HZ, 3.4, 0.32, M_GOLD, (0, D(90), 0))
plank("Globe climb", (-12.65, 4, HZ), (13.86, 12, HZ), 6.0, 0.5, M_MARBLE)
plank("Globe climb runner", (-12.4, 4.08, HZ), (13.6, 12.04, HZ), 2.6, 0.04, M_RUNNER)
for s in (-1, 1):
    bar("Globe climb edge", (-12.65, 3.9, HZ + 3 * s), (13.86, 11.9, HZ + 3 * s), 0.26, M_GOLD)
ball("Sun lamp", 0, 16, HZ, 4.4, M_SUN)
bar("Sun rod", (0, 18.2, HZ), (0, 24, HZ), 0.3, M_GOLD)
torus("Sun corona", 0, 16, HZ, 3.0, 0.1, M_GOLD, (D(90), 0, D(40)))
for (c, r, R, ang, m, ring) in ((-9, 9, 1.2, 150, M_PLANET, False), (-4.5, 4.5, 0.9, 20, M_MARS, False), (4.5, 4.5, 1.0, 215, M_JADE, False), (9, 9, 1.6, 320, M_BRONZE, True)):
    y = 8 + 0.302 * c + R + 0.2
    torus("Orbit", 0, y, HZ, r, 0.05, M_GOLD)
    px, pz = r * math.cos(D(ang)), HZ + r * math.sin(D(ang))
    ball("Planet", px, y, pz, R * 2, m)
    bar("Planet arm", (0, y, HZ), (px, y, pz), 0.08, M_GOLD)
    if ring:
        pr = torus("Planet ring", px, y - R + 0.5, pz, R + 1.0, 0.5, M_GOLD)
        pr.scale = (1, 1, 0.12)

# ---------------------------------------------------------------- Cassiopeia and the binary star
CAS = [(19, 12, 68, 3), (27, 11.2, 73, 3.4), (19, 12.6, 79, 3), (27, 11.6, 85, 3.4), (20, 12.2, 89.5, 3)]
for i in (0, 1, 3, 4):
    starplat(CAS[i][:3], CAS[i][3])
cline((14.3, 12, 64.2), 0, CAS[0][:3], CAS[0][3] - 0.2)
w0 = cline(CAS[0][:3], CAS[0][3] - 0.2, CAS[1][:3], CAS[1][3] - 0.2)
cline(CAS[1][:3], CAS[1][3] - 0.2, CAS[2][:3], 5.9)
w2 = cline(CAS[2][:3], 5.9, CAS[3][:3], CAS[3][3] - 0.2)
cline(CAS[3][:3], CAS[3][3] - 0.2, CAS[4][:3], CAS[4][3] - 0.2)
bx, by, bz = CAS[2][:3]
stem(bx, by, bz)
ba = D(35)
for s in (-1, 1):
    qx, qz = bx + s * 3.4 * math.cos(ba), bz + s * 3.4 * math.sin(ba)
    cyl("Binary star", qx, by - 0.5, by, qz, 4.6, M_PANEL)
    torus("Binary star rim", qx, by + 0.02, qz, 2.25, 0.09, M_GOLD)
    inst(ME_STAR5, "Binary star inlay", M_GLOW, U(qx, by + 0.01, qz), (1.7, 1.7, 1))
bar("Binary arm", (bx - 3.4 * math.cos(ba), by - 0.8, bz - 3.4 * math.sin(ba)), (bx + 3.4 * math.cos(ba), by - 0.8, bz + 3.4 * math.sin(ba)), 0.5, M_GOLD)
torus("Binary orbit", bx, by - 0.3, bz, 3.4, 0.07, M_GOLD)
lamps.append((bx, by + 1.6, bz))
meteor(along(*w2, 0.5), True)
meteor(along(*w0, 0.5), False)

# ---------------------------------------------------------------- the observing stage and the great telescope
TX, TZ = 9.0, 90.0
cyl("Observing stage", TX, 11.4, 12, TZ, 16, M_MARBLE)
cyl("Observing stage skirt", TX, 10.9, 11.4, TZ, 16.6, M_GOLD)
cyl("Stage inlay", TX, 12, 12.03, TZ, 13.4, M_PANEL)
inst(ME_STAR12, "Sun motif", M_GLOW, U(TX, 12.03, TZ), (6.2, 6.2, 1))
cyl("Stage pier", TX, FLOOR, 10.9, TZ, 5.2, M_NAVY)
for h in (-17.4, -6, 4, 9.6):
    cyl("Stage pier band", TX, h - 0.4, h + 0.4, TZ, 6.0, M_GOLD)
torus("Stage rail", TX, 13.05, TZ, 7.8, 0.1, M_GOLD)
for a in range(0, 360, 12):
    if 66 < a < 114:
        continue
    px, pz = TX + 7.8 * math.cos(D(a)), TZ + 7.8 * math.sin(D(a))
    cyl("Stage baluster", px, 12, 13.05, pz, 0.09, M_GOLD, ME_CYL8)
    if a % 48 == 0:
        lamp(px, 12, pz, a % 96 == 0)
cline(CAS[4][:3], 2.8, (TX + 7.7, 12, TZ - 0.4), 0)

DK0, DK1 = (TX, 12, 93), (TX, 20, 118)
axis = (U(*DK1) - U(*DK0)).normalized()
ux = Vector((1, 0, 0))
vy = axis.cross(ux).normalized() * -1.0   # the deck's up


def tube(t, lift=2.6):
    return U(*DK0) + (U(*DK1) - U(*DK0)) * t + vy * lift


plank("Telescope deck", DK0, DK1, 5.0, 0.4, M_STONE)
plank("Telescope deck runner", (TX, 12.03, 93), (TX, 20.03, 118), 2.2, 0.04, M_RUNNER)
quat = axis.to_track_quat("Z", "Y").to_euler()
for i in range(7):
    t = i / 6 * 1.06 - 0.03
    R, r = (4.5, 0.42) if i == 6 else (3.7, 0.26)
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=64, minor_segments=10)
    o = bpy.context.active_object
    for c in list(o.users_collection):
        c.objects.unlink(o)
    col.objects.link(o)
    o.location = tube(t)
    o.rotation_euler = quat
    o.data.materials.append(M_GOLD)
    for p in o.data.polygons:
        p.use_smooth = True
for k in range(8):
    a = D(22.5 + 45 * k)
    off = ux * (3.7 * math.cos(a)) + vy * (3.7 * math.sin(a))
    p0, p1 = tube(-0.03) + off, tube(1.03) + off
    o = inst(ME_CYL8, "Tube strut", M_GOLD, (p0 + p1) / 2, (0.16, 0.16, (p1 - p0).length))
    o.rotation_euler = quat
lens = inst(ME_CYL, "Objective lens", M_GLASS, tube(1.04), (8.6, 8.6, 0.25))
lens.rotation_euler = quat
eye = inst(ME_CONE, "Eyepiece", M_GOLD, tube(-0.16), (4.6, 4.6, 5.0))
eye.rotation_euler = (axis * -1).to_track_quat("Z", "Y").to_euler()
fo = ux * 5.0 + vy * 1.2
finder = inst(ME_CYL, "Finder scope", M_PANEL, tube(0.45) + fo, (1.3, 1.3, 9.0))
finder.rotation_euler = quat
for q in (0.3, 0.6):
    band = inst(ME_CYL, "Finder band", M_GOLD, tube(q) + fo, (1.45, 1.45, 0.3))
    band.rotation_euler = quat
for s in (-1, 1):
    box("Fork arm", TX + s * 4.4 - 0.7, TX + s * 4.4 + 0.7, 12, 20.5, 95.6, 98.4, M_NAVY)
    box("Fork arm cap", TX + s * 4.4 - 0.85, TX + s * 4.4 + 0.85, 20.5, 21.0, 95.4, 98.6, M_GOLD)
    cyl("Fork arm foot", TX + s * 4.4, 12, 12.6, 97, 3.0, M_GOLD)
for j, (t, closed) in enumerate(((0.28, 0.0), (0.54, 0.55), (0.8, 1.0))):
    c = tube(t)
    for k in range(8):
        a = D(45 * k)
        pivot = c + ux * (3.5 * math.cos(a)) + vy * (3.5 * math.sin(a))
        b2 = a + math.pi / 2 + D(58) * closed
        d = ux * math.cos(b2) + vy * math.sin(b2)
        w = axis.cross(d)
        o = inst(ME_CUBE, "Iris blade", M_BRONZE, (0, 0, 0), (1, 1, 1))
        frame_obj(o, pivot + d * 2.1 + axis * (0.03 * k), d * 4.2, w * 2.5, axis * 0.06)
for (t, side) in ((0.41, 1.1), (0.67, -1.3)):
    spot = tube(t, 0.06) + ux * side
    o = inst(ME_CYL, "Focus spot", M_LAMP, spot, (2.6, 2.6, 0.05))
    o.rotation_euler = vy.to_track_quat("Z", "Y").to_euler()
    top = tube(1.02)
    cone = inst(ME_CONE, "Focus beam", M_BEAM, (spot + top) / 2, (5.2, 5.2, (top - spot).length))
    cone.rotation_euler = (spot - top).to_track_quat("Z", "Y").to_euler()
    lamps.append((spot.x, spot.z + 0.6, spot.y))

# the balcony outside the dome, and the way on
box("Balcony", TX - 6, TX + 6, 19.4, 20, 118, 132, M_MARBLE)
inlay(TX - 2.4, TX + 2.4, 119, 131.5, 20, M_RUNNER)
rail(TX - 6, 118.5, TX - 6, 132, 20)
rail(TX + 6, 118.5, TX + 6, 132, 20)
for s in (TX - 6, TX + 6):
    box("Arch post", s - 0.9, s + 0.9, 20, 28, 131, 133, M_STONE)
box("Arch lintel", TX - 6.9, TX + 6.9, 28, 29.6, 131, 133, M_STONE)
box("Doorway glow", TX - 5.1, TX + 5.1, 20, 28, 133, 133.2, M_DOOR)
pier(TX - 4, 125, 19.4)
pier(TX + 4, 125, 19.4)

# ---------------------------------------------------------------- the round hall and its star-chart dome
cyl("Hall floor", HX, FLOOR - 1, FLOOR, HZ, 400, M_FLOOR)
for R in (30, 38, 45):
    torus("Floor ring", HX, FLOOR + 0.05, HZ, R, 0.16, M_BRASS)
for k in range(24):
    a = D(15 * k)
    bar("Floor ray", (HX + 27 * math.cos(a), FLOOR + 0.05, HZ + 27 * math.sin(a)), (HX + 45 * math.cos(a), FLOOR + 0.05, HZ + 45 * math.sin(a)), 0.2 if k % 2 == 0 else 0.1, M_BRASS)

SLIT, AZ0, AZ1 = 78.7, 52.0, 262.0


def shell(name, az0, az1, y0, y1, mat, radius=HR + 0.6):
    bm = bmesh.new()
    n = max(2, int((az1 - az0) / 3))
    lo, hi = [], []
    for i in range(n + 1):
        a = D(az0 + (az1 - az0) * i / n)
        lo.append(bm.verts.new((HX + radius * math.cos(a), HZ + radius * math.sin(a), y0)))
        hi.append(bm.verts.new((HX + radius * math.cos(a), HZ + radius * math.sin(a), y1)))
    for i in range(n):
        bm.faces.new((lo[i], lo[i + 1], hi[i + 1], hi[i]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


def dome_part(name, az0, az1, e0, e1):
    bm = bmesh.new()
    na, ne = max(2, int((az1 - az0) / 4)), max(2, int((e1 - e0) / 4))
    grid = []
    for j in range(ne + 1):
        e = D(e0 + (e1 - e0) * j / ne)
        row = []
        for i in range(na + 1):
            a = D(az0 + (az1 - az0) * i / na)
            row.append(bm.verts.new((HX + HR * math.cos(e) * math.cos(a), HZ + HR * math.cos(e) * math.sin(a), WALLTOP + HR * math.sin(e))))
        grid.append(row)
    for j in range(ne):
        for i in range(na):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            f.smooth = True
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(M_DOME)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    return o


shell("Hall wall a", AZ0, SLIT - 8, FLOOR, WALLTOP, M_NAVY)
shell("Hall wall b", SLIT + 8, AZ1, FLOOR, WALLTOP, M_NAVY)
shell("Hall wall under the slit", SLIT - 8, SLIT + 8, FLOOR, 11, M_NAVY)
dome_part("Dome a", AZ0, SLIT - 8, 0, 70)
dome_part("Dome b", SLIT + 8, AZ1, 0, 70)


def dp(az, e, r=HR - 0.25):
    return (HX + r * math.cos(D(e)) * math.cos(D(az)), WALLTOP + r * math.sin(D(e)), HZ + r * math.cos(D(e)) * math.sin(D(az)))


for az in range(60, 262, 15):
    if abs(az - SLIT) < 9:
        continue
    chain([dp(az, e) for e in range(0, 71, 7)], 0.14, M_GOLD)
for e in (18, 36, 54):
    chain([dp(az, e) for az in range(int(SLIT + 9), 262, 5)], 0.12, M_GOLD)
for s in (-8, 8):
    chain([dp(SLIT + s, e) for e in range(0, 71, 7)], 0.5, M_GOLD)
    bar("Slit jamb", (HX + HR * math.cos(D(SLIT + s)), 11, HZ + HR * math.sin(D(SLIT + s))), (HX + HR * math.cos(D(SLIT + s)), WALLTOP, HZ + HR * math.sin(D(SLIT + s))), 0.6, M_GOLD)
chain([(HX + (HR - 0.2) * math.cos(D(a)), WALLTOP, HZ + (HR - 0.2) * math.sin(D(a))) for a in range(int(SLIT + 9), 263, 4)], 0.7, M_GOLD)

# constellations drawn on the dome's lower band (the part the picture sees): the Dipper, the W, a hunter, a crown
for figure in ([(128, 9), (135, 12), (142, 13), (149, 17), (150, 24), (159, 25), (160, 18), (149, 17)],
               [(96, 12), (101, 22), (107, 13), (113, 23), (119, 14)],
               [(176, 8), (181, 16), (186, 9), (181, 16), (183, 24), (178, 30), (183, 24), (189, 29)],
               [(212, 14), (217, 9), (224, 8), (231, 10), (236, 15)]):
    pts = [dp(az, e, HR - 0.6) for (az, e) in figure]
    chain(pts, 0.16, M_GLOW)
    for q in pts:
        ball("Dome star", q[0], q[1], q[2], 1.0, M_LAMP)

# the wall's bays (the arcade of observatory_dress_preview.py, laid round the arc)
ME_NICHE_OK = True
_tail = _head.split("def arch_mesh(name, w, h, depth):")[1].split("\nn = 0\n")[0]
exec(compile("def arch_mesh(name, w, h, depth):" + _tail, "observatory_dress_preview.py (bays)", "exec"), globals())
n = 0
for storey in (-17.0, 0.5, 15.5):
    for az in range(60, 258, 12):
        if abs(az - SLIT) < 14 and storey > -5:
            continue
        a = D(az)
        bay(Vector((HX + HR * math.cos(a), HZ + HR * math.sin(a), 0)), a - math.pi / 2, storey, (n + az // 12) % 3, (az // 12) % 2 == 0)
    n += 1

# ---------------------------------------------------------------- the pieces
PAWNS = [(0, 0, 5), (-2, 0, 10), (-3, 0, 17), (-1, 0, 20), (-9, 0.8, 26), (-14, 1.3, 29.6), (-19, 1.8, 33.4), (-26, 2.4, 42), (-28, 2.4, 44.5),
         (-37, 2.2, 61), (-36, 2.2, 63.5), (-25, 3.2, 59), (-23.5, 3.2, 61.5), (-18, 3.6, 62), (cpos[0] + 0.5, cpos[1], cpos[2]),
         (-8, 5.5, 64.8), (-2, 7.3, 63.2), (6, 9.7, 64.6), (11, 11.2, 63.5), (19, 12, 67.4), (27, 11.2, 73.5), (27, 11.6, 85),
         (20, 12.2, 89), (12, 12, 88), (7, 12, 86), (TX - 1, 13.3, 97), (TX + 1.2, 15.5, 104), (TX - 0.8, 18.1, 112), (TX, 20, 122), (TX + 2, 20, 127)]
for i, (x, y, z) in enumerate(PAWNS):
    pawn(x, y, z, i % 2)
bq = (bx + 3.4 * math.cos(ba), by, bz + 3.4 * math.sin(ba))
pawn(bq[0], bq[1], bq[2], 1)

# ---------------------------------------------------------------- light
world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.014, 0.04, 1)


def light(name, kind, loc, energy, color, **kw):
    d = bpy.data.lights.new(name, kind)
    d.energy = energy
    d.color = color
    for k, v in kw.items():
        setattr(d, k, v)
    o = bpy.data.objects.new(name, d)
    col.objects.link(o)
    o.location = loc
    return o


def aim(o, target):
    o.rotation_euler = (Vector(target) - o.location).to_track_quat("-Z", "Y").to_euler()


key = light("LGT_SlitShaft", "SPOT", U(30, 86, 176), 4200000, (1.0, 0.86, 0.62), spot_size=D(19), spot_blend=0.35, shadow_soft_size=1.5)
aim(key, U(2, 11, 68))
fill = light("LGT_Fill", "SUN", U(60, 90, 0), 0.72, (0.46, 0.60, 1.0), angle=D(40))
aim(fill, U(0, 0, 64))
warm = light("LGT_Warm", "AREA", U(84, 60, -8), 210000, (1.0, 0.80, 0.55), size=70)
aim(warm, U(0, 4, 62))
for (x, y, z) in lamps:
    light("LGT_Lamp", "POINT", U(x, y, z), 3600, (1.0, 0.62, 0.26), shadow_soft_size=0.4)
light("LGT_Sun", "POINT", U(0, 16, HZ), 26000, (1.0, 0.76, 0.42), shadow_soft_size=2.2)
light("LGT_Door", "AREA", U(TX, 25, 131), 9000, (1.0, 0.8, 0.5), size=10).rotation_euler = Euler((D(-90), 0, 0))

bpy.ops.mesh.primitive_cube_add(size=1)
haze = bpy.context.active_object
haze.name = "FX_Haze"
for c in list(haze.users_collection):
    c.objects.unlink(haze)
col.objects.link(haze)
haze.location = U(0, 20, 64)
haze.scale = (110, 150, 80)
hm = bpy.data.materials.new("MAT_Haze")
hm.use_nodes = True
nt = hm.node_tree
for nd in list(nt.nodes):
    nt.nodes.remove(nd)
vol = nt.nodes.new("ShaderNodeVolumeScatter")
vol.inputs["Density"].default_value = 0.0012
vol.inputs["Anisotropy"].default_value = 0.35
outn = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(vol.outputs["Volume"], outn.inputs["Volume"])
haze.data.materials.append(hm)

# ---------------------------------------------------------------- camera and render
cam_d = bpy.data.cameras.new("CAM_ObservatoryV3_Hero")
cam_d.lens = 40
cam_d.clip_end = 900
cam = bpy.data.objects.new("CAM_ObservatoryV3_Hero", cam_d)
col.objects.link(cam)
cam.location = U(88, 72, -30)
aim(cam, U(-6, 8.5, 61.5))
scene.camera = cam

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
scene.view_settings.exposure = 0.32
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = OUT
print("OBJECTS", len(col.objects), "LAMPS", len(lamps))
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
