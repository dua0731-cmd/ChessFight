# The Royal Observatory's graybox layout dressed in the concept art's look, as one still (R714).
# A preview picture only: nothing here goes into Unity. The layout is RoyalObservatoryBuilder.cs's (section frame:
# x east, y up, z north); in Blender that is (X, Y, Z) = (x, z, y).
#
#   blender --background --factory-startup --python observatory_dress_preview.py -- <out.png> [samples] [width] [height]
import bpy, bmesh, math, random, sys
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if len(argv) > 0 else "//observatory_dressed.png"
SAMPLES = int(argv[1]) if len(argv) > 1 else 128
WIDTH = int(argv[2]) if len(argv) > 2 else 1920
HEIGHT = int(argv[3]) if len(argv) > 3 else 1080
random.seed(7)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = bpy.data.collections.new("COL_Observatory")
scene.collection.children.link(col)


def U(x, y, z):
    return Vector((x, z, y))


# ---------------------------------------------------------------- materials
def principled(name, color, rough=0.5, metal=0.0, emit=None, strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = strength
    return m


def marble(name, a, c, scale):
    # Two creams in 2 m tiles with thin dark joints, and a faint vein.
    m = principled(name, a, 0.32)
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexCoord")
    brick = nt.nodes.new("ShaderNodeTexBrick")
    brick.inputs["Color1"].default_value = (*a, 1)
    brick.inputs["Color2"].default_value = (*c, 1)
    brick.inputs["Mortar"].default_value = (a[0] * .55, a[1] * .55, a[2] * .55, 1)
    brick.inputs["Scale"].default_value = scale
    brick.inputs["Mortar Size"].default_value = 0.012
    brick.inputs["Brick Width"].default_value = 1.0
    brick.inputs["Row Height"].default_value = 1.0
    brick.offset = 0.0
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 0.35
    noise.inputs["Detail"].default_value = 6
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 0.22
    nt.links.new(tex.outputs["Object"], brick.inputs["Vector"])
    nt.links.new(tex.outputs["Object"], noise.inputs["Vector"])
    nt.links.new(brick.outputs[0], mix.inputs[6])
    nt.links.new(noise.outputs[1], mix.inputs[7])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    return m


def starry(name, base, star, density):
    # A navy panel sprinkled with gold stars.
    m = principled(name, base, 0.35)
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexCoord")
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.inputs["Scale"].default_value = density
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.035
    ramp.color_ramp.elements[0].color = (1, 1, 1, 1)
    ramp.color_ramp.elements[1].position = 0.07
    ramp.color_ramp.elements[1].color = (0, 0, 0, 1)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs[6].default_value = (*base, 1)
    mix.inputs[7].default_value = (*star, 1)
    nt.links.new(tex.outputs["Object"], vor.inputs["Vector"])
    nt.links.new(vor.outputs["Distance"], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], mix.inputs[0])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    nt.links.new(mix.outputs[2], b.inputs["Emission Color"])
    nt.links.new(ramp.outputs[0], b.inputs["Emission Strength"])
    return m


CREAM, CREAM2 = (0.70, 0.63, 0.49), (0.58, 0.52, 0.40)
M_MARBLE = marble("MAT_Marble", CREAM, CREAM2, 0.5)
M_STONE = principled("MAT_Stone", (0.62, 0.56, 0.45), 0.5)
M_GOLD = principled("MAT_Gold", (0.92, 0.62, 0.18), 0.2, 1.0)
M_BRASS = principled("MAT_Brass", (0.55, 0.40, 0.17), 0.38, 1.0)
M_NAVY = principled("MAT_NavyWall", (0.018, 0.036, 0.12), 0.5)
M_NAVY2 = principled("MAT_NavyDeep", (0.008, 0.014, 0.05), 0.7)
M_PANEL = starry("MAT_StarPanel", (0.022, 0.04, 0.15), (1.0, 0.78, 0.35), 3.2)
M_FLOOR = starry("MAT_HallFloor", (0.010, 0.018, 0.055), (0.9, 0.72, 0.4), 0.9)
M_PURPLE = principled("MAT_Velvet", (0.16, 0.035, 0.34), 0.8)
M_RUNNER = starry("MAT_Runner", (0.15, 0.04, 0.32), (1.0, 0.8, 0.4), 1.6)
M_LAMP = principled("MAT_Lamp", (1, 0.8, 0.5), 0.3, 0.0, (1.0, 0.50, 0.16), 32.0)
M_DOOR = principled("MAT_Doorway", (1, 0.9, 0.7), 0.5, 0.0, (1.0, 0.82, 0.55), 6.0)
M_GRATE = principled("MAT_Grate", (0.10, 0.11, 0.14), 0.45, 0.9)
M_PLANET = marble("MAT_Planet", (0.10, 0.20, 0.42), (0.30, 0.38, 0.46), 0.6)
M_WHITE = principled("MAT_PawnBody", (0.86, 0.85, 0.80), 0.4)
M_RED = principled("MAT_PawnRed", (0.70, 0.05, 0.05), 0.5)
M_BLUE = principled("MAT_PawnBlue", (0.06, 0.20, 0.70), 0.5)
M_WIND = principled("MAT_Wind", (0.8, 0.9, 1.0), 0.5, 0.0, (0.7, 0.85, 1.0), 0.8)
M_WIND.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.16

# ---------------------------------------------------------------- shared meshes
def mesh_from(op, **kw):
    op(**kw)
    o = bpy.context.active_object
    me = o.data
    bpy.data.objects.remove(o)
    return me


ME_CUBE = mesh_from(bpy.ops.mesh.primitive_cube_add, size=1)
ME_CYL = mesh_from(bpy.ops.mesh.primitive_cylinder_add, vertices=40, radius=0.5, depth=1)
ME_CYL8 = mesh_from(bpy.ops.mesh.primitive_cylinder_add, vertices=10, radius=0.5, depth=1)
ME_SPH = mesh_from(bpy.ops.mesh.primitive_uv_sphere_add, segments=32, ring_count=16, radius=0.5)
ME_CONE = mesh_from(bpy.ops.mesh.primitive_cone_add, vertices=24, radius1=0.5, radius2=0.0, depth=1)
for me in (ME_CYL, ME_CYL8, ME_SPH, ME_CONE):
    for p in me.polygons:
        p.use_smooth = True
for me in (ME_CUBE, ME_CYL, ME_CYL8, ME_SPH, ME_CONE):
    me.materials.append(None)


def inst(me, name, mat, loc, scale, rot=(0, 0, 0)):
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    o.location = loc
    o.scale = scale
    o.rotation_euler = Euler(rot)
    o.material_slots[0].link = "OBJECT"
    o.material_slots[0].material = mat
    return o


def box(name, x0, x1, y0, y1, z0, z1, mat):
    return inst(ME_CUBE, name, mat, U((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (abs(x1 - x0), abs(z1 - z0), abs(y1 - y0)))


def cyl(name, x, y0, y1, z, d, mat, me=None):
    return inst(me or ME_CYL, name, mat, U(x, (y0 + y1) / 2, z), (d, d, abs(y1 - y0)))


def ball(name, x, y, z, d, mat):
    return inst(ME_SPH, name, mat, U(x, y, z), (d, d, d))


def torus(name, x, y, z, R, r, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=72, minor_segments=10)
    o = bpy.context.active_object
    o.name = name
    for c in list(o.users_collection):
        c.objects.unlink(o)
    col.objects.link(o)
    o.location = U(x, y, z)
    o.rotation_euler = Euler(rot)
    o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def bar(name, a, b_, thick, mat, me=None):
    # A round bar from section point a to b.
    A, B = U(*a), U(*b_)
    d = B - A
    o = inst(me or ME_CYL8, name, mat, (A + B) / 2, (thick, thick, d.length))
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return o


lamps = []


def lamp(x, y, z, light=False):
    cyl("Lamp post", x, y, y + 1.9, z, 0.14, M_GOLD, ME_CYL8)
    ball("Lamp globe", x, y + 2.15, z, 0.62, M_LAMP)
    cyl("Lamp cap", x, y + 2.42, y + 2.6, z, 0.2, M_GOLD, ME_CYL8)
    if light:
        lamps.append((x, y + 2.2, z))


# ---------------------------------------------------------------- walkways
def slab(name, x0, x1, z0, z1, y=0.0, mat=None, piers=True):
    box(name, x0, x1, y - 0.5, y, z0, z1, mat or M_MARBLE)
    box(name + " fascia", x0 - 0.12, x1 + 0.12, y - 0.95, y - 0.5, z0 - 0.12, z1 + 0.12, M_GOLD)
    box(name + " under", x0 + 0.4, x1 - 0.4, y - 1.6, y - 0.95, z0 + 0.4, z1 - 0.4, M_NAVY)
    if piers:
        for px in (x0 + 1.2, x1 - 1.2):
            for pz in (z0 + 1.2, z1 - 1.2):
                pier(px, pz, y - 1.6)


def pier(x, z, top):
    cyl("Pier", x, -18, top, z, 1.1, M_NAVY)
    for h in (top - 0.5, top - 6, -12, -17.4):
        if h > -18:
            cyl("Pier band", x, h - 0.25, h + 0.25, z, 1.3, M_GOLD)


def rail(x0, z0, x1, z1, y, lit=True):
    # A brass balustrade along a straight edge, a marble post and a lamp at each end.
    L = math.hypot(x1 - x0, z1 - z0)
    if L < 0.2:
        return
    bar("Rail top", (x0, y + 1.05, z0), (x1, y + 1.05, z1), 0.13, M_GOLD)
    bar("Rail foot", (x0, y + 0.12, z0), (x1, y + 0.12, z1), 0.09, M_GOLD)
    n = max(1, int(L / 0.9))
    for i in range(1, n):
        t = i / n
        cyl("Baluster", x0 + (x1 - x0) * t, y + 0.12, y + 1.05, z0 + (z1 - z0) * t, 0.07, M_GOLD, ME_CYL8)
    for i, (px, pz) in enumerate(((x0, z0), (x1, z1))):
        box("Rail post", px - 0.22, px + 0.22, y, y + 1.25, pz - 0.22, pz + 0.22, M_STONE)
        if lit:
            lamp(px, y + 1.25, pz, light=(len(lamps) < 26 and random.random() < 0.5))
    k = int(L / 7)
    for i in range(1, k + 1):
        t = i / (k + 1)
        px, pz = x0 + (x1 - x0) * t, z0 + (z1 - z0) * t
        box("Rail post", px - 0.2, px + 0.2, y, y + 1.25, pz - 0.2, pz + 0.2, M_STONE)
        if lit:
            lamp(px, y + 1.25, pz)


def inlay(x0, x1, z0, z1, y, mat=None):
    box("Inlay", x0, x1, y, y + 0.03, z0, z1, mat or M_PANEL)
    for a in ((x0, x1, z0 - 0.08, z0), (x0, x1, z1, z1 + 0.08), (x0 - 0.08, x0, z0, z1), (x1, x1 + 0.08, z0, z1)):
        box("Inlay rim", a[0], a[1], y, y + 0.045, a[2], a[3], M_GOLD)


def armillary(x, y, z, R, mat=M_GOLD, thick=None):
    t = thick or R * 0.035
    torus("Armillary ring", x, y, z, R, t, mat, (0, 0, 0))
    torus("Armillary ring", x, y, z, R * 0.93, t, mat, (math.radians(90), 0, math.radians(20)))
    torus("Armillary ring", x, y, z, R * 0.86, t, mat, (math.radians(62), math.radians(25), 0))
    ball("Armillary core", x, y, z, R * 0.42, M_PLANET)


def pawn(x, y, z, team):
    cyl("Pawn skirt", x, y, y + 0.55, z, 0.62, M_WHITE)
    inst(ME_CONE, "Pawn body", M_WHITE, U(x, y + 0.6, z), (0.6, 0.6, 0.7))
    ball("Pawn head", x, y + 0.92, z, 0.42, M_WHITE)
    cyl("Pawn scarf", x, y + 0.66, y + 0.8, z, 0.4, M_RED if team else M_BLUE)


# entry, ring court
box("Entry floor", -6, 6, -0.5, 0, 0, 12, M_MARBLE)
inlay(-2.2, 2.2, 0.5, 11.5, 0, M_RUNNER)
for s in (-1, 1):
    box("Gate post", s * 6.2, s * 7.6, 0, 9, 10.6, 12, M_NAVY)
    box("Gate cap", s * 6.0, s * 7.8, 9, 9.5, 10.4, 12.2, M_GOLD)
    box("Gate banner", s * 6.4, s * 7.4, 3.2, 8.4, 10.5, 10.58, M_PURPLE)
    ball("Gate emblem", s * 6.9, 6.6, 10.45, 0.7, M_GOLD)
box("Gate lintel", -7.8, 7.8, 9.5, 11.2, 10.6, 12, M_NAVY)
torus("Gate arch", 0, 6.2, 11.0, 6.0, 0.28, M_GOLD, (math.radians(90), 0, 0))
slab("Ring court", -6.2, 6.2, 12, 38)
rail(-6.2, 12, -6.2, 50, 0)
rail(6.2, 12, 6.2, 38, 0)
for z, spin in ((18.4, 28), (31.6, -42)):
    cyl("Ring inlay", 0, 0, 0.03, z, 12.2, M_PANEL)
    torus("Ring rim", 0, 0.04, z, 6.1, 0.09, M_GOLD)
    torus("Ring hoop", 0, 0.75, z, 4.6, 0.16, M_GOLD)
    torus("Ring hoop", 0, 0.75, z, 2.6, 0.12, M_GOLD)
    a = math.radians(spin)
    bar("Ring bar", (-6.1 * math.cos(a), 0.36, z - 6.1 * math.sin(a)), (6.1 * math.cos(a), 0.36, z + 6.1 * math.sin(a)), 0.34, M_GOLD)
    for k in range(4):
        b2 = a + k * math.pi / 2
        bar("Ring spoke", (0, 0.75, z), (4.6 * math.cos(b2), 0.75, z + 4.6 * math.sin(b2)), 0.1, M_GOLD)
    cyl("Ring hub", 0, 0, 1.5, z, 0.9, M_GOLD)
    armillary(0, 2.2, z, 0.8)
bar("High bar", (0.3, 1.52, 31.6), (5.4, 1.52, 35.0), 0.3, M_BRASS)
cyl("High mast", 0, 1.5, 2.0, 31.6, 0.3, M_BRASS)

# SW landing, belts and shields, SE landing
slab("SW landing", -6.2, 12, 38, 50)
inlay(-3.5, 9, 40.5, 47.5, 0)
rail(-6.2, 50, 12, 50, 0)
rail(6.2, 38, 12, 38, 0)
slab("Belt floor a", 12, 13.5, 38, 50, piers=False)
slab("Belt floor b", 21.5, 28.7, 38, 50)
slab("Belt floor c", 36.7, 44, 38, 50)
for bx in (17.5, 32.7):
    box("Belt bed", bx - 4, bx + 4, -1.5, -0.45, 37.9, 50.1, M_NAVY)
    box("Belt frame", bx - 4.1, bx + 4.1, -0.45, -0.05, 37.85, 50.15, M_GOLD)
    for lane in (41, 47):
        box("Belt", bx - 4, bx + 4, -0.3, 0.0, lane - 2.85, lane + 2.85, M_PANEL)
        for k in range(5):
            cyl("Belt roller", bx - 3.4 + 1.7 * k, -0.02, 0.04, lane, 0.5, M_GOLD)
    box("Belt seam", bx - 4, bx + 4, -0.05, 0.06, 43.85, 44.15, M_GOLD)
for (x0, x1, zc) in ((21.8, 24.8, 47), (25.4, 28.4, 35), (37.05, 40.05, 41), (40.65, 43.65, 53)):
    xm = (x0 + x1) / 2
    o = inst(ME_CYL, "Shutter shield", M_GOLD, U(xm, 0.2, zc), (5.8, 5.8, 0.5), (0, math.radians(90), 0))
    o2 = inst(ME_CYL, "Shield boss", M_BRASS, U(xm, 1.3, zc), (2.0, 2.0, 0.7), (0, math.radians(90), 0))
    box("Shield foot", x0, x1, 0, 0.35, zc - 3, zc + 3, M_BRASS)
for (a, b2) in ((12, 21.8), (24.8, 40.65), (43.65, 44)):
    rail(a, 50, b2, 50, 0, lit=(b2 - a > 3))
for (a, b2) in ((12, 25.4), (28.4, 37.05), (40.05, 44)):
    rail(a, 38, b2, 38, 0, lit=(b2 - a > 3))
slab("SE landing", 44, 56, 38, 50)
inlay(46.5, 53.5, 40.5, 47.5, 0)
rail(44, 38, 56, 38, 0)
rail(56, 38, 56, 50, 0)
rail(44, 50, 47, 50, 0, False)
rail(53, 50, 56, 50, 0, False)

# vent grating bridge
box("Grating", 47, 53, -0.25, 0, 50, 71, M_GRATE)
for z in range(50, 72, 1):
    box("Grating bar", 47, 53, 0, 0.04, z - 0.05, z + 0.05, M_BRASS)
for x in (47, 53):
    box("Grating girder", x - 0.2, x + 0.2, -0.9, 0.05, 50, 71, M_GOLD)
for zc in (55, 62, 69):
    cyl("Fan pier", 55.2, -18, 0.2, zc, 1.3, M_NAVY)
    cyl("Fan pier band", 55.2, -0.3, 0.2, zc, 1.6, M_GOLD)
    cyl("Fan stem", 55.2, 0.2, 2.2, zc, 0.35, M_GOLD)
    inst(ME_CYL, "Fan hub", M_GOLD, U(54.9, 2.4, zc), (0.9, 0.9, 0.8), (0, math.radians(90), 0))
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        o = inst(ME_CUBE, "Fan blade", M_GOLD, U(54.7, 2.4 + 1.2 * math.sin(a), zc + 1.2 * math.cos(a)), (0.08, 0.85, 1.9), (a - math.pi / 2, 0, 0))
    for k in range(3):
        inst(ME_CUBE, "Wind streak", M_WIND, U(50.3 - k * 0.4, 1.0 + 0.5 * k, zc - 1.2 + 1.2 * k), (7.0, 0.5, 0.03), (0.15 * (k - 1), 0, 0))
slab("NE landing", 44, 56, 71, 83)
inlay(46.5, 53.5, 73.5, 80.5, 0)
rail(56, 71, 56, 83, 0)
rail(44, 83, 56, 83, 0)
rail(44, 71, 47, 71, 0, False)
rail(53, 71, 56, 71, 0, False)
rail(44, 80, 44, 83, 0, False)
rail(44, 71, 44, 74, 0, False)
slab("Orrery approach", 31, 44, 74, 80, piers=False)
rail(32, 80, 44, 80, 0)
rail(32, 74, 44, 74, 0)

# the great orrery
HX, HZ = 22.0, 77.0
cyl("Turntable", HX, -0.5, 0, HZ, 18, M_PANEL)
torus("Turntable rim", HX, -0.05, HZ, 9.0, 0.22, M_GOLD)
for R in (3.0, 5.5, 7.6):
    torus("Chart ring", HX, 0.02, HZ, R, 0.05, M_GOLD)
for k in range(12):
    a = k * math.pi / 6
    bar("Chart spoke", (HX + 1.6 * math.cos(a), 0.03, HZ + 1.6 * math.sin(a)), (HX + 8.8 * math.cos(a), 0.03, HZ + 8.8 * math.sin(a)), 0.06, M_GOLD)
cyl("Orrery pedestal", HX, -18, -0.5, HZ, 6.5, M_NAVY)
for h in (-17.5, -9, -1.2):
    cyl("Pedestal band", HX, h - 0.4, h + 0.4, HZ, 7.2, M_GOLD)
cyl("Orrery hub", HX, 0, 3.2, HZ, 3.0, M_STONE)
cyl("Orrery hub cap", HX, 3.2, 3.6, HZ, 3.4, M_GOLD)
cyl("Orrery spire", HX, 3.6, 27, HZ, 0.7, M_GOLD)
armillary(HX, 16, HZ, 11.0, M_GOLD, 0.26)
torus("Orrery ring", HX, 16, HZ, 9.4, 0.2, M_GOLD, (math.radians(35), math.radians(40), 0))
ball("Orrery sun", HX, 16, HZ, 4.2, M_LAMP)
a = math.radians(200)
bar("Low arm", (HX - 9.2 * math.cos(a), 0.36, HZ - 9.2 * math.sin(a)), (HX + 9.2 * math.cos(a), 0.36, HZ + 9.2 * math.sin(a)), 0.34, M_GOLD)
a = math.radians(-35)
gx, gz = HX + 7.5 * math.cos(a), HZ + 7.5 * math.sin(a)
bar("Globe arm", (HX, 1.95, HZ), (gx, 1.95, gz), 0.26, M_GOLD)
armillary(gx, 1.3, gz, 1.05)
slab("W landing", 1, 13, 71, 83)
inlay(3.5, 10.5, 73.5, 80.5, 0)
rail(1, 71, 13, 71, 0)
rail(1, 71, 1, 83, 0)
rail(1, 83, 4, 83, 0, False)
rail(10, 83, 13, 83, 0, False)

# planet lifts and the ledge
for (z0, z1, top) in ((83.2, 88.2, 1.3), (92.6, 97.6, 4.7)):
    zc = (z0 + z1) / 2
    box("Lift deck", 4, 10, top - 0.35, top, z0, z1, M_PANEL)
    box("Lift rim", 3.9, 10.1, top - 0.6, top - 0.3, z0 - 0.1, z1 + 0.1, M_GOLD)
    for px in (5.2, 8.8):
        for pz in (z0 + 1.1, z1 - 1.1):
            cyl("Lift piston", px, -18, top - 0.6, pz, 0.45, M_GOLD)
    cyl("Lift cylinder", 7, -18, top - 4, zc, 2.2, M_NAVY)
    cyl("Lift cylinder band", 7, top - 4.6, top - 4, zc, 2.5, M_GOLD)
slab("Lift ledge", 4, 10, 88.4, 92.4, 3)
for px in (0.5, 13.5):
    cyl("Weight post", px, -18, 14.3, 90.4, 0.7, M_NAVY)
    cyl("Weight post cap", px, 14.3, 14.9, 90.4, 0.95, M_GOLD)
bar("Weight beam", (0.5, 14.3, 90.4), (13.5, 14.3, 90.4), 0.5, M_GOLD)
sw = math.radians(32)
bx, by = 7 + 6 * math.sin(sw), 10.3 - 6 * math.cos(sw)
bar("Weight chain", (7, 10.3, 90.4), (bx, by, 90.4), 0.12, M_BRASS)
bar("Weight hanger", (7, 14.3, 90.4), (7, 10.3, 90.4), 0.3, M_GOLD)
ball("Planet weight", bx, by, 90.4, 2.2, M_PLANET)
torus("Planet weight ring", bx, by, 90.4, 1.5, 0.07, M_GOLD, (math.radians(20), 0, 0))

# upper level: NW landing, telescope bridge, observation platform
slab("NW landing", 1, 13, 97.8, 108, 6)
inlay(3.5, 10.5, 99.8, 106, 6)
rail(1, 97.8, 1, 108, 6)
rail(1, 108, 13, 108, 6)
rail(1, 97.8, 4, 97.8, 6, False)
rail(10, 97.8, 13, 97.8, 6, False)
slab("Telescope bridge", 13, 43, 100, 106, 6, piers=False)
for px in (16, 24, 32, 40):
    pier(px, 103, 4.4)
inlay(13.5, 42.5, 101.6, 104.4, 6, M_RUNNER)
for (a, b2) in ((13, 17), (21, 25), (29, 33), (37, 43)):
    rail(a, 106, b2, 106, 6, lit=True)
for i, x in enumerate((19, 27, 35)):
    out = (0.0, 4.6, 1.2)[i]
    zf = 106.2 - out
    box("Ram cradle", x - 2.4, x + 2.4, 4.6, 6.0, 106.4, 113.5, M_NAVY)
    box("Ram cradle trim", x - 2.6, x + 2.6, 6.0, 6.25, 106.3, 113.6, M_GOLD)
    for zz in (108.0, 111.5):
        torus("Ram yoke", x, 7.2, zz, 1.25, 0.14, M_GOLD, (math.radians(90), 0, 0))
        box("Ram yoke foot", x - 1.5, x + 1.5, 6.2, 6.6, zz - 0.3, zz + 0.3, M_GOLD)
    box("Ram head", x - 2, x + 2, 6.05, 7.65, zf, zf + 0.5, M_GOLD)
    o = inst(ME_CYL, "Telescope barrel", M_PANEL, U(x, 7.2, zf + 4.2), (1.6, 1.6, 8.0), (math.radians(90), 0, 0))
    for k in range(4):
        inst(ME_CYL, "Barrel band", M_GOLD, U(x, 7.2, zf + 0.9 + 2.1 * k), (1.75, 1.75, 0.3), (math.radians(90), 0, 0))
    inst(ME_CYL, "Lens", M_LAMP if out > 0 else M_BRASS, U(x, 6.85, zf - 0.03), (1.3, 1.3, 0.06), (math.radians(90), 0, 0))
slab("Observation platform", 43, 57, 96, 109.4, 6)
cyl("Platform inlay", 50, 6, 6.03, 102.5, 9.0, M_PANEL)
torus("Platform inlay rim", 50, 6.04, 102.5, 4.5, 0.08, M_GOLD)
rail(43, 96, 57, 96, 6)
rail(57, 96, 57, 109.4, 6)
rail(43, 96, 43, 100, 6, False)
rail(43, 106, 43, 109.4, 6, False)

# the constellation wall, terrace and exit
for (y1, z0, z1) in ((7.5, 109.4, 110.6), (9.0, 110.6, 111.8), (10.5, 111.8, 118)):
    box("Tier", 44, 56, 4.4, y1, z0, z1, M_MARBLE)
    box("Tier lip", 44, 56, y1 - 0.12, y1 + 0.02, z0 - 0.08, z0 + 0.1, M_GOLD)
    for k in range(13):
        for r in range(2):
            ball("Constellation stud", 44.6 + 0.9 * k + 0.45 * r, y1 - 0.45 - 0.55 * r, z0 - 0.04, 0.2, M_GOLD)
for (yy, zz) in ((7.5, 110.6), (9.0, 111.8)):
    box("Wind slot", 45, 55, yy + 0.25, yy + 0.7, zz - 0.06, zz + 0.02, M_GRATE)
for x in (43, 56):
    box("Cheek", x, x + 1, 4.4, 12.6, 109.4, 120, M_NAVY)
    box("Cheek cap", x - 0.1, x + 1.1, 12.6, 13.0, 109.3, 120.1, M_GOLD)
    lamp(x + 0.5, 13.0, 110.2, True)
    lamp(x + 0.5, 13.0, 119.2)
for i in range(4):
    box("Exit step", 44, 56, 9.5, 10.75 + 0.25 * i, 118 + 0.5 * i, 118.5 + 0.5 * i, M_MARBLE)
    box("Step carpet", 47.5, 52.5, 10.75 + 0.25 * i, 10.78 + 0.25 * i, 118 + 0.5 * i, 118.5 + 0.5 * i, M_PURPLE)
box("Exit run-out", 44, 56, 10.9, 11.5, 120, 136, M_MARBLE)
inlay(47.5, 52.5, 120.2, 135.8, 11.5, M_RUNNER)
box("Terrace carpet", 47.5, 52.5, 10.5, 10.53, 112.4, 118, M_RUNNER)
for x in (45.2, 54.8):
    for (dx, dz) in ((0.5, 0.4), (-0.5, 0.4), (0, -0.6)):
        bar("Tripod leg", (x, 12.0, 114.5), (x + dx, 10.5, 114.5 + dz), 0.07, M_GOLD)
    bar("Terrace telescope", (x - 0.3, 11.9, 113.6), (x + 0.3, 12.5, 115.6), 0.34, M_PANEL)
for s in (44, 56):
    box("Arch post", s - 0.9, s + 0.9, 11.5, 19.5, 131, 133, M_STONE)
    box("Curtain", s + (0.9 if s < 50 else -3.2), s + (3.2 if s < 50 else -0.9), 12.2, 19.2, 131.4, 131.7, M_PURPLE)
box("Arch lintel", 43.1, 56.9, 19.5, 21.2, 131, 133, M_STONE)
ball("Arch sun", 50, 20.4, 130.9, 1.6, M_GOLD)
box("Doorway glow", 44.9, 55.1, 11.5, 19.5, 135.6, 135.8, M_DOOR)
box("Exit gatehouse back", 43.1, 56.9, 10.9, 21.2, 135.8, 137.0, M_NAVY)
for s2 in (43.2, 56.8):
    cyl("Run-out pier", s2, -18, 10.9, 126, 1.1, M_NAVY)
rail(44, 120, 44, 131, 11.5)
rail(56, 120, 56, 131, 11.5)
pier(47, 128, 10.9)
pier(53, 128, 10.9)

# ---------------------------------------------------------------- the hall
WEST, EAST, SOUTH, NORTH, FLOOR, TOP = -18.0, 68.0, 6.0, 114.0, -18.0, 34.0
box("Hall floor", WEST, 190, FLOOR - 1, FLOOR, SOUTH - 120, NORTH + 40, M_FLOOR)
for R in (13, 22, 31):
    torus("Floor ring", HX, FLOOR + 0.05, HZ, R, 0.14, M_BRASS)
for k in range(16):
    a = k * math.pi / 8
    bar("Floor ray", (HX + 7.5 * math.cos(a), FLOOR + 0.05, HZ + 7.5 * math.sin(a)), (HX + 31 * math.cos(a), FLOOR + 0.05, HZ + 31 * math.sin(a)), 0.16 if k % 2 == 0 else 0.08, M_BRASS)
box("Hall wall W", WEST - 1.5, WEST, FLOOR, TOP, SOUTH - 20, NORTH + 1.5, M_NAVY)
box("Hall wall N", WEST, 42, FLOOR, TOP, NORTH, NORTH + 1.5, M_NAVY)


def arch_mesh(name, w, h, depth):
    # A panel with a round head, in its own XZ plane (width along X), `depth` thick along -Y.
    bm = bmesh.new()
    r = w / 2
    pts = [(-r, 0), (r, 0), (r, h - r)]
    for i in range(1, 16):
        a = math.pi * i / 16
        pts.append((r * math.cos(a), h - r + r * math.sin(a)))
    pts.append((-r, h - r))
    verts = [bm.verts.new((p[0], 0, p[1])) for p in pts]
    face = bm.faces.new(verts)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)], vec=(0, -depth, 0))
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(None)
    return me


ME_NICHE = arch_mesh("ME_Niche", 5.0, 11.0, 0.3)
ME_FRAME = arch_mesh("ME_NicheFrame", 5.9, 11.7, 0.15)


def statue(kind, p, h, facing):
    # p: the foot, in Blender space. A chess piece or an armillary on a plinth.
    x, y, z = p
    inst(ME_CUBE, "Plinth", M_STONE, Vector((x, y, z + 0.6)), (2.2, 2.2, 1.2))
    if kind == 0:
        inst(ME_CONE, "Pawn statue", M_STONE, Vector((x, y, z + 1.2 + h * 0.3)), (h * 0.42, h * 0.42, h * 0.6))
        inst(ME_SPH, "Pawn statue head", M_STONE, Vector((x, y, z + 1.2 + h * 0.72)), (h * 0.3,) * 3)
    elif kind == 1:
        inst(ME_CYL, "Rook statue", M_STONE, Vector((x, y, z + 1.2 + h * 0.36)), (h * 0.32, h * 0.32, h * 0.72))
        inst(ME_CYL, "Rook statue top", M_STONE, Vector((x, y, z + 1.2 + h * 0.8)), (h * 0.42, h * 0.42, h * 0.18))
        inst(ME_CYL, "Rook statue band", M_GOLD, Vector((x, y, z + 1.2 + h * 0.68)), (h * 0.36, h * 0.36, h * 0.05))
    else:
        inst(ME_CYL8, "Armillary stand", M_GOLD, Vector((x, y, z + 1.2 + h * 0.2)), (0.25, 0.25, h * 0.4))
        for rot in ((0, 0, 0), (math.radians(90), 0, facing), (math.radians(60), math.radians(30), 0)):
            bpy.ops.mesh.primitive_torus_add(major_radius=h * 0.28, minor_radius=0.07, major_segments=40, minor_segments=8)
            o = bpy.context.active_object
            for c in list(o.users_collection):
                c.objects.unlink(o)
            col.objects.link(o)
            o.location = Vector((x, y, z + 1.2 + h * 0.62))
            o.rotation_euler = Euler(rot)
            o.data.materials.append(M_GOLD)
        inst(ME_SPH, "Armillary globe", M_PLANET, Vector((x, y, z + 1.2 + h * 0.62)), (h * 0.22,) * 3)


def bay(pos, facing, storey_z, kind, banner):
    # One wall bay: a gold-framed niche with a statue, a pilaster either side, a banner over some.
    # pos: the niche's foot middle on the wall face (Blender space); facing: rotation about Z.
    rot = (0, 0, facing)
    out = Vector((-math.sin(facing), math.cos(facing), 0)) * -1.0
    base = Vector((pos[0], pos[1], storey_z))
    inst(ME_FRAME, "Niche frame", M_GOLD, base + out * 0.05, (1, 1, 1), rot)
    inst(ME_NICHE, "Niche", M_NAVY2, base + out * 0.14, (1, 1, 1), rot)
    statue(kind, base + out * 1.5, 6.0, facing)
    side = Vector((math.cos(facing), math.sin(facing), 0))
    for s in (-1, 1):
        c = base + side * (4.1 * s) + out * 0.35
        inst(ME_CUBE, "Pilaster", M_NAVY, c + Vector((0, 0, 7.5)), (1.0, 1.0, 15.0), rot)
        inst(ME_CUBE, "Pilaster cap", M_GOLD, c + Vector((0, 0, 15.2)), (1.3, 1.3, 0.5), rot)
        inst(ME_CUBE, "Pilaster foot", M_GOLD, c + Vector((0, 0, 0.3)), (1.3, 1.3, 0.6), rot)
    if banner:
        c = base + side * 4.1 + out * 0.95
        inst(ME_CUBE, "Banner", M_PURPLE, c + Vector((0, 0, 9.5)), (2.3, 0.08, 7.0), rot)
        inst(ME_CUBE, "Banner tip", M_PURPLE, c + Vector((0, 0, 5.4)), (1.6, 0.08, 1.6), (0, math.radians(45), facing))
        inst(ME_CYL, "Banner emblem", M_GOLD, c + Vector((0, 0, 10.6)) + out * 0.06, (1.3, 1.3, 0.06), (math.radians(90), 0, facing))
        inst(ME_CUBE, "Banner rod", M_GOLD, c + Vector((0, 0, 13.1)), (2.8, 0.14, 0.14), rot)


n = 0
for storey in (-17.0, 0.5, 17.5):
    for i in range(13):
        zc = SOUTH - 14 + 4.6 + i * 9.6
        bay(U(WEST, 0, zc), math.radians(90), storey, (n + i) % 3, i % 2 == 0)
    for i in range(6):
        xc = WEST + 6 + i * 9.6
        bay(U(xc, 0, NORTH), 0.0, storey, (n + i + 1) % 3, i % 2 == 1)
    n += 1
for storey in (-0.3, 16.7):
    box("Wall cornice W", WEST, WEST + 0.9, storey - 0.35, storey + 0.2, SOUTH - 20, NORTH, M_GOLD)
    box("Wall cornice N", WEST, 42, storey - 0.35, storey + 0.2, NORTH - 0.9, NORTH, M_GOLD)

# planet globes on stands in the drop
for (x, y, z, d) in ((34, -7, 60, 4.2), (10, -9, 58, 3.2), (37, -5, 93, 3.0), (-6, -6, 62, 3.6), (30, -9, 30, 4.6), (62, -8, 62, 3.4), (-9, -8, 90, 2.8), (58, -4, 24, 3.8), (78, -9, 44, 4.4), (70, -6, 84, 3.6), (84, -8, 8, 3.2), (48, -9, -6, 4.0), (22, -8, -14, 3.4), (96, -9, 70, 4.8)):
    cyl("Globe stand", x, -18, y - d * 0.55, z, 0.45, M_GOLD)
    cyl("Globe stand foot", x, -18, -17.5, z, 2.2, M_GOLD)
    ball("Planet globe", x, y, z, d, M_PLANET)
    torus("Planet ring", x, y, z, d * 0.72, 0.08, M_GOLD, (math.radians(18), math.radians(10), 0))
    torus("Planet meridian", x, y, z, d * 0.56, 0.06, M_GOLD, (math.radians(90), 0, math.radians(30)))

# the pieces along the way
for i, (x, y, z) in enumerate(((0.5, 0, 4), (-2, 0, 9), (2.5, 0, 14), (-3, 0, 22), (3.5, 0, 26), (-1, 0, 36), (4, 0, 44), (9, 0, 46), (15, 0, 47), (23, 0, 42),
                                (34, 0, 41), (48, 0, 43), (52, 0, 46), (51, 0, 51.5), (49, 0, 58.5), (51, 0, 65.5), (50, 0, 76), (40, 0, 77), (26, 0, 72),
                                (18, 0, 73), (8, 0, 78), (6, 0, 81.5), (7, 1.3, 85.5), (6, 3, 89.3), (8, 4.7, 95), (7, 6, 101), (15, 6, 104), (23, 6, 104.6),
                                (31, 6, 104.5), (39, 6, 103), (47, 6, 101), (52, 6, 106), (48, 7.5, 110), (51, 9, 111.2), (50, 10.5, 114), (49, 11.5, 124))):
    pawn(x, y, z, i % 2)

# ---------------------------------------------------------------- light
world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.014, 0.04, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0


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


key = light("LGT_Shaft", "SPOT", U(-8, 84, 116), 3400000, (1.0, 0.84, 0.58), spot_size=math.radians(24), spot_blend=0.4, shadow_soft_size=1.2)
aim(key, U(24, 2, 86))
fill = light("LGT_Fill", "SUN", U(60, 90, 0), 0.55, (0.50, 0.62, 1.0), angle=math.radians(40))
aim(fill, U(20, 0, 70))
warm = light("LGT_Warm", "AREA", U(70, 40, 20), 95000, (1.0, 0.80, 0.55), size=60)
aim(warm, U(25, 0, 65))
for (x, y, z) in lamps:
    light("LGT_Lamp", "POINT", U(x, y + 0.5, z), 6500, (1.0, 0.58, 0.22), shadow_soft_size=0.4)
light("LGT_Sun", "POINT", U(HX, 16, HZ), 40000, (1.0, 0.78, 0.45), shadow_soft_size=2.2)
light("LGT_Door", "AREA", U(50, 16, 130), 9000, (1.0, 0.8, 0.5), size=10).rotation_euler = Euler((math.radians(-90), 0, 0))

# The shaft, seen: a thin haze through the hall.
bpy.ops.mesh.primitive_cube_add(size=1)
haze = bpy.context.active_object
haze.name = "FX_Haze"
for c in list(haze.users_collection):
    c.objects.unlink(haze)
col.objects.link(haze)
haze.location = U(25, 12, 60)
haze.scale = (100, 150, 70)
hm = bpy.data.materials.new("MAT_Haze")
hm.use_nodes = True
nt = hm.node_tree
for nd in list(nt.nodes):
    nt.nodes.remove(nd)
vol = nt.nodes.new("ShaderNodeVolumeScatter")
vol.inputs["Density"].default_value = 0.0015
vol.inputs["Anisotropy"].default_value = 0.35
outn = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(vol.outputs["Volume"], outn.inputs["Volume"])
haze.data.materials.append(hm)

# ---------------------------------------------------------------- camera and render
cam_d = bpy.data.cameras.new("CAM_Observatory_Hero")
cam_d.lens = 38
cam_d.clip_end = 800
cam = bpy.data.objects.new("CAM_Observatory_Hero", cam_d)
col.objects.link(cam)
cam.location = U(116, 90, -22)
aim(cam, U(23, 1, 65))
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
scene.cycles.max_bounces = 6
scene.cycles.volume_bounces = 0
try:
    scene.cycles.volume_step_rate = 4
except Exception:
    pass
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
print("LOOK", scene.view_settings.look)
scene.view_settings.exposure = 0.35
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = OUT
print("OBJECTS", len(col.objects), "LAMPS", len(lamps))
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
