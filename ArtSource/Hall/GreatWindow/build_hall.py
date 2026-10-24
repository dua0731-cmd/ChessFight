# Lobby backdrop: a palace stair hall in chess materials (ivory / ebony marble, red velvet,
# gold), built around the lobby's stage so the party of pawns stays the subject.
# Blender coords: X = lobby sample x, Y = -sample z... i.e. Y grows AWAY from the camera,
# Z = height. Camera A = the lobby camera today, camera B = lower, looking up a little.
# Usage: blender -b --factory-startup --python build_hall.py -- <out_dir> [A|B|both] [samples]
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
OUT = args[0]; CAMS = args[1] if len(args) > 1 else "both"; SAMPLES = int(args[2]) if len(args) > 2 else 96
HALL = r"C:\Work\ChessFight\ArtSource\Hall"
os.makedirs(OUT, exist_ok=True)
bpy.context.preferences.view.use_translate_new_dataname = False
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
sc = bpy.context.scene
COL = {}
def coll(name):
    if name not in COL:
        c = bpy.data.collections.new(name); sc.collection.children.link(c); COL[name] = c
    return COL[name]

def srgb(h):
    h = h.lstrip("#"); return tuple(((int(h[i:i + 2], 16) / 255) ** 2.2) for i in (0, 2, 4)) + (1.0,)

# ---------------- materials ----------------
MATS = {}
def principled(name, color, rough=.4, metal=0.0, sheen=0.0, emit=None, emit_k=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = srgb(color); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    if sheen: b.inputs['Sheen Weight'].default_value = sheen; b.inputs['Sheen Roughness'].default_value = .4
    if emit: b.inputs['Emission Color'].default_value = srgb(emit); b.inputs['Emission Strength'].default_value = emit_k
    MATS[name] = m; return m

def marble(name, base, vein, rough=.12, scale=.35, checker=None):
    """Polished marble: soft clouds + two vein networks from object position. checker=(c2, vein2, size)
    makes a two-tone checkerboard floor."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); b.inputs['Roughness'].default_value = rough
    L = nt.links.new
    tc = nt.nodes.new('ShaderNodeTexCoord')
    def tone(basec, veinc, seed):
        mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (scale, scale, scale * .6); mp.inputs['Location'].default_value = (seed, seed * .7, 0)
        L(tc.outputs['Object'], mp.inputs['Vector'])
        n1 = nt.nodes.new('ShaderNodeTexNoise'); n1.inputs['Scale'].default_value = 1.4; n1.inputs['Detail'].default_value = 8; n1.inputs['Distortion'].default_value = 1.3
        L(mp.outputs['Vector'], n1.inputs['Vector'])
        r = nt.nodes.new('ShaderNodeValToRGB'); e = r.color_ramp.elements
        e[0].position, e[0].color = .47, (0, 0, 0, 1); e[1].position, e[1].color = .5, (1, 1, 1, 1)
        e2 = e.new(.53); e2.color = (0, 0, 0, 1)
        L(n1.outputs['Fac'], r.inputs['Fac'])
        cl = nt.nodes.new('ShaderNodeTexNoise'); cl.inputs['Scale'].default_value = .5; cl.inputs['Detail'].default_value = 3
        L(mp.outputs['Vector'], cl.inputs['Vector'])
        c0 = nt.nodes.new('ShaderNodeMix'); c0.data_type = 'RGBA'; L(cl.outputs['Fac'], c0.inputs[0])
        c0.inputs[6].default_value = srgb(basec); bc = srgb(basec); c0.inputs[7].default_value = tuple(min(1, x * 1.12) for x in bc[:3]) + (1,)
        mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'
        mul = nt.nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = .8
        L(r.outputs['Color'], mul.inputs[0]); L(mul.outputs[0], mx.inputs[0]); L(c0.outputs[2], mx.inputs[6]); mx.inputs[7].default_value = srgb(veinc)
        return mx.outputs[2]
    a = tone(base, vein, 0.0)
    if checker:
        c2, v2, size = checker
        bb = tone(c2, v2, 7.3)
        ck = nt.nodes.new('ShaderNodeTexChecker'); ck.inputs['Scale'].default_value = 1.0
        mp2 = nt.nodes.new('ShaderNodeMapping'); mp2.inputs['Scale'].default_value = (1 / size, 1 / size, 1 / size)
        L(tc.outputs['Object'], mp2.inputs['Vector']); L(mp2.outputs['Vector'], ck.inputs['Vector'])
        ck.inputs['Color1'].default_value = (1, 1, 1, 1); ck.inputs['Color2'].default_value = (0, 0, 0, 1)
        f = nt.nodes.new('ShaderNodeMix'); f.data_type = 'RGBA'
        L(ck.outputs['Fac'], f.inputs[0]); L(bb, f.inputs[6]); L(a, f.inputs[7]); a = f.outputs[2]
    L(a, b.inputs['Base Color'])
    MATS[name] = m; return m

IVORY = marble("Ivory Marble", "efe6d4", "b39b7c")
WHITE = marble("White Marble", "f4f1ea", "c8c0b2", rough=.18)
FLOOR = marble("Floor Marble", "efe6d4", "b39b7c", rough=.08, scale=.25, checker=("1c1916", "6a5a46", 2.0))
EBONY = principled("Ebony", "15110e", rough=.22)
GOLD = principled("Gold", "d9ab4a", rough=.28, metal=1.0)
RED = principled("Red Velvet", "6a0a10", rough=.75, sheen=.8)
CARPET = principled("Carpet", "760c12", rough=.8, sheen=.6)
WALNUT = principled("Walnut", "3a2416", rough=.45)
PLASTER = marble("Wall", "e9dfcc", "d3c6ae", rough=.5, scale=.12)
GLOW = principled("Window Glow", "ffd9a0", rough=1, emit="ffc884", emit_k=3.0)
SHADOW = principled("Arch Shadow", "2a1d14", rough=.9)
PAWN_W = principled("Pawn Ivory", "efe7d7", rough=.35)
PAWN_B = principled("Pawn Charcoal", "2b2724", rough=.35)
STAGE_WOOD = principled("Stage Wood", "2a1a10", rough=.5)
MAHOGANY = principled("Mahogany", "4a1d12", rough=.35)

# ---------------- mesh helpers ----------------
def obj(name, me, mat, coll_name="Hall", loc=(0, 0, 0)):
    o = bpy.data.objects.new(name, me); coll(coll_name).objects.link(o); o.location = loc
    if mat is not None: me.materials.append(mat)
    return o

def box(name, center, size, mat, coll_name="Hall", bevel=0.0):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect='EDGES', profile=.5)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = bevel > 0
    return obj(name, me, mat, coll_name, center)

def lathe(name, profile, mat, segs=32, coll_name="Hall", loc=(0, 0, 0), scale=(1, 1, 1), caps=True):
    """profile: [(radius, z)] bottom to top; closed with caps where radius is 0."""
    bm = bmesh.new(); rings = []
    for r, z in profile:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring.append(bm.verts.new((r * math.cos(a) * scale[0], r * math.sin(a) * scale[1], z * scale[2])))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if caps: bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    return obj(name, me, mat, coll_name, loc)

def linked(src, name, loc, rot=(0, 0, 0), scale=(1, 1, 1), coll_name="Hall"):
    o = bpy.data.objects.new(name, src.data); coll(coll_name).objects.link(o)
    o.location = loc; o.rotation_euler = rot; o.scale = scale; return o

def beam_between(name, a, b, thick, mat, coll_name="Hall"):
    a, b = Vector(a), Vector(b); d = b - a
    o = box(name, (a + b) / 2, (d.length, thick[0], thick[1]), mat, coll_name, bevel=.01)
    o.rotation_euler = d.to_track_quat('X', 'Z').to_euler(); return o

# ---------------- pieces ----------------
PAWN = [(.42, 0), (.42, .07), (.36, .1), (.34, .14), (.24, .2), (.17, .32), (.14, .5), (.24, .55), (.26, .58), (.16, .62),
        (.2, .68), (.25, .76), (.26, .83), (.22, .92), (.13, .98), (.0, 1.0)]
baluster = lathe("Pawn Baluster", PAWN, WHITE, segs=20, scale=(.42, .42, .78))
baluster.location = (0, 0, -100)

def balustrade(p0, p1, z0, z1, spacing=.34, coll_name="Hall"):
    p0, p1 = Vector(p0), Vector(p1); L = (p1 - p0).length; n = max(2, int(L / spacing))
    for i in range(n + 1):
        t = i / n; p = p0.lerp(p1, t); z = z0 + (z1 - z0) * t
        linked(baluster, "Baluster", (p.x, p.y, z + .06), coll_name=coll_name)
    rail_h = .78 + .12
    beam_between("Rail", (p0.x, p0.y, z0 + rail_h), (p1.x, p1.y, z1 + rail_h), (.16, .1), EBONY, coll_name)
    beam_between("Rail Gold", (p0.x, p0.y, z0 + rail_h + .055), (p1.x, p1.y, z1 + rail_h + .055), (.08, .015), GOLD, coll_name)
    beam_between("Plinth", (p0.x, p0.y, z0 + .03), (p1.x, p1.y, z1 + .03), (.2, .06), IVORY, coll_name)

def flight(start, direction, width, steps, rise=.2, run=.36, coll_name="Stairs", carpet=.62):
    """Solid marble steps climbing from `start` (x, y, z of the first riser's foot) along `direction`."""
    d = Vector(direction).normalized(); side = Vector((-d.y, d.x, 0)); s = Vector(start)
    for i in range(steps):
        top = s.z + rise * (i + 1)
        c = s + d * (run * (i + .5)); c.z = (s.z + top) / 2 - .001
        sz = (run if abs(d.x) > .5 else width, width if abs(d.x) > .5 else run, top - s.z)
        box("Step", (c.x, c.y, c.z + .0), sz, WHITE, coll_name)
        # nosing in gold-free ivory, carpet with gold edges on top and on the riser
        cw = width * carpet
        csz = (run + .002 if abs(d.x) > .5 else cw, cw if abs(d.x) > .5 else run + .002, .025)
        box("Carpet", (c.x, c.y, top + .012), csz, CARPET, coll_name)
        rf = s + d * (run * i) + d * -.012; rf.z = top - rise / 2
        box("Carpet Riser", (rf.x, rf.y, rf.z), (.025 if abs(d.x) > .5 else cw, cw if abs(d.x) > .5 else .025, rise), CARPET, coll_name)
        for k in (-1, 1):
            g = c + side * k * (cw / 2 + .03)
            box("Carpet Edge", (g.x, g.y, top + .016), (run if abs(d.x) > .5 else .05, .05 if abs(d.x) > .5 else run, .03), GOLD, coll_name)
    end = s + d * (run * steps); end.z = s.z + rise * steps
    return end

# ================= layout =================
# Lobby stage (Unity draws it; here only as a stand-in so the hall is judged around it)
STAGE, BASE, FRAME, BOARD = 10.8, .9, .34, 9.6
TOP = BASE + FRAME + .005
box("Stage", (0, 0, BASE / 2), (STAGE, STAGE, BASE), STAGE_WOOD, "Stand-in")
box("Frame", (0, 0, BASE + FRAME / 2), (10.4, 10.4, FRAME), MAHOGANY, "Stand-in")
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=8, y_segments=8, size=BOARD / 2)
me = bpy.data.meshes.new("Board"); bm.to_mesh(me); bm.free()
board = obj("Board", me, principled("Maple", "d9b27a", .35), "Stand-in", (0, 0, TOP)); me.materials.append(principled("Walnut Sq", "5a321c", .35))
for i, p in enumerate(me.polygons):
    c = p.center; p.material_index = (int((c.x + 4.8) // 1.2) + int((c.y + 4.8) // 1.2)) % 2
PLACES = [(0, .5), (-2.4, .5), (2.4, .5), (-1.2, -1.5), (1.2, -1.5), (-3.6, -1.5)]
pawn_big = [(r * 1.0, z * 1.0) for r, z in PAWN]
for i, (x, zz) in enumerate(PLACES):
    s = 1.55 if i == 0 else 1.35
    lathe("Party Pawn", PAWN, PAWN_B if i == 5 else PAWN_W, segs=32, coll_name="Stand-in", loc=(x, -zz, TOP), scale=(s * .55, s * .55, s))

# Floor: big ivory / ebony checker marble
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40)
me = bpy.data.meshes.new("Floor"); bm.to_mesh(me); bm.free()
obj("Floor", me, FLOOR, "Hall", (0, 10, 0))

# Grand stair: central flight up to a landing, then left and right flights to the gallery
Y0 = 11.0                       # foot of the central flight (behind the stage, a clear gap)
end = flight((0, Y0, 0), (0, 1, 0), 7.0, 15)          # rises 3.0 m over 5.4 m
LAND_Y0, LAND_Y1, LAND_Z = end.y, end.y + 3.6, end.z
box("Landing", (0, (LAND_Y0 + LAND_Y1) / 2, LAND_Z / 2), (16.0, LAND_Y1 - LAND_Y0, LAND_Z), WHITE, "Stairs")
box("Landing Carpet", (0, (LAND_Y0 + LAND_Y1) / 2, LAND_Z + .012), (7.0 * .62, LAND_Y1 - LAND_Y0, .025), CARPET, "Stairs")
for k in (-1, 1):
    # side flights climb outward along x from the landing's ends to the gallery
    flight((k * 8.0, (LAND_Y0 + LAND_Y1) / 2, LAND_Z), (k, 0, 0), 3.6, 15)
GAL_Z = LAND_Z + 3.0
GAL_Y0, GAL_Y1 = LAND_Y0 - .4, LAND_Y1 + 4.0
for k in (-1, 1):
    box("Gallery Wing", (k * 17.5, (GAL_Y0 + GAL_Y1) / 2, GAL_Z / 2), (5.0, GAL_Y1 - GAL_Y0, GAL_Z), WHITE, "Stairs")
box("Gallery Back", (0, LAND_Y1 + 2.0, GAL_Z / 2), (40.0, 4.0, GAL_Z), WHITE, "Stairs")
box("Gallery Carpet", (0, LAND_Y1 + 2.0, GAL_Z + .012), (30.0, 2.2, .025), CARPET, "Stairs")
# under the landing: a dark arched passage (depth reads as a doorway)
box("Under Landing", (0, LAND_Y0 + .02, LAND_Z / 2 - .3), (5.0, .1, LAND_Z - .6), SHADOW, "Stairs")

# balustrades
balustrade((-3.75, Y0, 0), (-3.75, LAND_Y0, 0), 0, LAND_Z) if False else None
for k in (-1, 1):
    x = k * 3.75
    balustrade((x, Y0 + .2, 0), (x, LAND_Y0, 0), 0.0, LAND_Z)                                    # central flight sides
    balustrade((k * 4.0, LAND_Y0 + .15, 0), (k * 8.0, LAND_Y0 + .15, 0), LAND_Z, LAND_Z)            # landing front
    balustrade((k * 8.0, LAND_Y0 + .15, 0), (k * 13.4, LAND_Y0 + .15, 0), LAND_Z, GAL_Z)            # side flight outer rail
    balustrade((k * 4.0, LAND_Y1 + .1, 0), (k * 13.4, LAND_Y1 + .1, 0), GAL_Z, GAL_Z)               # gallery front
# newel posts at the foot: the Tripo king posts
NEWELS = [(-3.75, Y0 - .1), (3.75, Y0 - .1)]

# Back wall with three tall arches over the gallery, red drapes, the throne in the middle
WALL_Y = GAL_Y1
box("Back Wall", (0, WALL_Y + .5, 10), (44, 1.0, 20), PLASTER, "Walls")
for x, w, h in ((0, 5.2, 8.0), (-9.5, 3.6, 6.6), (9.5, 3.6, 6.6)):
    zb = GAL_Z
    box("Arch Recess", (x, WALL_Y - .02, zb + h / 2 - w / 4), (w, .05, h - w / 2), SHADOW, "Walls")
    bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, radius=w / 2, segments=48)
    me = bpy.data.meshes.new("ArchTop"); bm.to_mesh(me); bm.free()
    o = obj("Arch Top", me, SHADOW, "Walls", (x, WALL_Y - .02, zb + h - w / 2)); o.rotation_euler = (math.pi / 2, 0, 0)
    # glowing window high in the arch
    wo = box("Arch Window", (x, WALL_Y - .04, zb + h - w / 2), (w * .45, .04, w * .7), GLOW, "Walls")
    # ivory surround + gold keystone line
    for k in (-1, 1):
        box("Arch Pier", (x + k * (w / 2 + .3), WALL_Y - .25, zb + (h - w / 2) / 2), (.6, .5, h - w / 2), IVORY, "Walls", bevel=.03)
    ring = lathe("Arch Ring", [(w / 2 + .6, 0), (w / 2 + .6, .5), (w / 2, .5), (w / 2, 0)], IVORY, segs=48, coll_name="Walls")
    ring.location = (x, WALL_Y - .02, zb + h - w / 2); ring.rotation_euler = (math.pi / 2, 0, 0)
    box("Keystone", (x, WALL_Y - .35, zb + h + .25), (.6, .4, .8), GOLD, "Walls", bevel=.04)
    # red drapes either side of the opening
    for k in (-1, 1):
        box("Drape", (x + k * (w / 2 - .35), WALL_Y - .45, zb + (h - .3) / 2), (.7, .25, h - .3), RED, "Walls", bevel=.1)
        box("Drape Tie", (x + k * (w / 2 - .35), WALL_Y - .6, zb + 1.4), (.78, .1, .12), GOLD, "Walls")
# Cornice and frieze
box("Cornice", (0, WALL_Y - .3, GAL_Z + 9.4), (44, .6, .5), IVORY, "Walls", bevel=.05)
box("Frieze Gold", (0, WALL_Y - .62, GAL_Z + 9.1), (44, .05, .12), GOLD, "Walls")
# lower wall under the gallery: walnut arch doors, ivory pilasters, a red banner over the landing
FACE = LAND_Y1 - .02
for x in (-10.6, 10.6, -14.6, 14.6):
    box("Door", (x, FACE - .04, 1.8), (2.0, .08, 3.6), WALNUT, "Walls", bevel=.02)
    bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, radius=1.0, segments=32)
    me = bpy.data.meshes.new("DoorTop"); bm.to_mesh(me); bm.free()
    o = obj("Door Top", me, WALNUT, "Walls", (x, FACE - .05, 3.6)); o.rotation_euler = (math.pi / 2, 0, 0)
    ring = lathe("Door Ring", [(1.25, 0), (1.25, .12), (1.0, .12), (1.0, 0)], GOLD, segs=40, coll_name="Walls")
    ring.location = (x, FACE - .03, 3.6); ring.rotation_euler = (math.pi / 2, 0, 0)
    for k in (-1, 1):
        box("Door Frame", (x + k * 1.12, FACE - .07, 1.8), (.25, .14, 3.6), GOLD, "Walls", bevel=.02)
        box("Pilaster", (x + k * 1.9, FACE - .15, GAL_Z / 2), (.5, .3, GAL_Z), IVORY, "Walls", bevel=.03)
    box("Door Handle", (x - .15, FACE - .12, 1.7), (.08, .08, .5), GOLD, "Walls")
    box("Door Handle", (x + .15, FACE - .12, 1.7), (.08, .08, .5), GOLD, "Walls")
box("Landing Banner", (0, FACE - .05, LAND_Z + 1.55), (3.4, .06, 2.6), RED, "Walls", bevel=.02)
for k in (-1, 1): box("Banner Trim", (k * 1.72, FACE - .09, LAND_Z + 1.55), (.1, .05, 2.6), GOLD, "Walls")
box("Banner Trim", (0, FACE - .09, LAND_Z + 2.85), (3.5, .05, .1), GOLD, "Walls")
box("Banner Rod", (0, FACE - .15, LAND_Z + 2.9), (4.0, .08, .08), GOLD, "Walls")
box("Gallery Band", (0, FACE - .1, GAL_Z - .15), (40, .2, .3), IVORY, "Walls", bevel=.03)

# Columns: ivory shafts with gold bases and capitals, two pairs framing the stair
COLUMN = [(.75, 0), (.75, .35), (.62, .45), (.55, .6), (.5, .7), (.46, 9.0), (.55, 9.15), (.68, 9.35), (.8, 9.6), (.8, 9.9)]
for x, y in ((-6.2, Y0 - 1.0), (6.2, Y0 - 1.0), (-14.0, Y0 + 1.0), (14.0, Y0 + 1.0)):
    c = lathe("Column", COLUMN, IVORY, segs=40, coll_name="Columns", loc=(x, y, 0))
    lathe("Column Base Gold", [(.78, .34), (.78, .46), (.6, .5), (.6, .34)], GOLD, segs=40, coll_name="Columns", loc=(x, y, 0))
    lathe("Column Capital Gold", [(.56, 9.1), (.72, 9.3), (.84, 9.6), (.84, 9.75), (.56, 9.75)], GOLD, segs=40, coll_name="Columns", loc=(x, y, 0))
# Side walls
for k in (-1, 1):
    box("Side Wall", (k * 21, 10, 10), (1.0, 40, 20), PLASTER, "Walls")
# Ceiling (barrel vault, coffered with gold ribs) – mostly for camera B
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=32, radius=1.0)
for v in list(bm.verts):
    if v.co.z < 0: bm.verts.remove(v)
me = bpy.data.meshes.new("Dome"); bm.to_mesh(me); bm.free()
dome = obj("Dome", me, PLASTER, "Walls", (0, 10, 18.0)); dome.scale = (21.5, 26, 10)
for p in me.polygons: p.use_smooth = True
for i in range(12):
    a = 2 * math.pi * i / 12
    rib = lathe("Rib", [(.0, 0), (.0, 1)], GOLD, segs=4)  # placeholder removed below
    bpy.data.objects.remove(rib, do_unlink=True)

# ---------------- Tripo parts ----------------
def tripo(name, folder, loc, rot=0.0, scale=1.0, coll_name="Props"):
    path = os.path.join(HALL, folder, folder + ".fbx")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    m = bpy.data.materials.get(folder + "_Baked")
    if m is None:
        m = bpy.data.materials.new(folder + "_Baked"); m.use_nodes = True; nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        def img(suffix, color):
            p = os.path.join(HALL, folder, folder + suffix + ".png")
            if not os.path.exists(p): return None
            t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(p)
            t.image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'; return t
        a = img("_Albedo", True); nt.links.new(a.outputs['Color'], b.inputs['Base Color'])
        ms = img("_MetalSmooth", False)
        if ms:
            sp = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(ms.outputs['Color'], sp.inputs['Color']); nt.links.new(sp.outputs['Red'], b.inputs['Metallic'])
            inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
            nt.links.new(ms.outputs['Alpha'], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs['Roughness'])
        n = img("_Normal", False)
        if n:
            nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(n.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
        e = img("_Emission", True)
        if e: nt.links.new(e.outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 4.0
    out = []
    for o in new:
        for c in o.users_collection: c.objects.unlink(o)
        coll(coll_name).objects.link(o)
        if o.type == 'MESH':
            o.data.materials.clear(); o.data.materials.append(m)
            o.location = loc; o.rotation_euler.z += rot; o.scale = tuple(v * scale for v in o.scale); o.name = name
            out.append(o)
    return out

for x, y in NEWELS:
    tripo("King Newel", "KingPost", (x, y, 0), 0, 2.8)
for k in (-1, 1):
    tripo("Brazier", "Brazier", (k * 9.3, Y0 - 2.6, 0), 0, 1.5)
tripo("Throne", "Throne", (0, WALL_Y - 1.4, GAL_Z), 0.0, 3.0)
for x in (-12.3, 12.3, -6.8, 6.8):
    tripo("Wall Lantern", "WallLantern", (x, WALL_Y - .2, GAL_Z + 4.2), -math.pi / 2, 1.4)
for x, y, z, s in ((0, 7.5, 11.5, 3.2), (-9.0, 12.0, 12.5, 2.4), (9.0, 12.0, 12.5, 2.4)):
    tripo("Chandelier", "Chandelier", (x, y, z), 0, s)
    box("Chandelier Chain", (x, y, z + s + 3), (.05, .05, 6), EBONY, "Props")

# ---------------- lights ----------------
w = bpy.data.worlds.new("Hall World"); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (.05, .035, .025, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .4
def light(name, kind, loc, energy, color="ffe2b8", size=1.0, rot=(0, 0, 0), spot=None):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = srgb(color)[:3]
    if kind == 'AREA': l.size = size
    if kind == 'POINT': l.shadow_soft_size = size
    if kind == 'SPOT': l.spot_size = spot or math.radians(40); l.spot_blend = .5; l.shadow_soft_size = size
    o = bpy.data.objects.new(name, l); coll("Lights").objects.link(o); o.location = loc; o.rotation_euler = rot; return o
light("Sky Fill", 'AREA', (0, 10, 17), 6000, "fff1dc", size=22)
light("Front Fill", 'AREA', (0, -24, 9), 9000, "ffe9d0", size=16, rot=(math.radians(80), 0, 0))
for x in (-10.6, 10.6, -14.6, 14.6):
    light("Door Glow", 'POINT', (x, LAND_Y1 - 1.5, 4.6), 250, "ffc27a", size=.3)
light("Key on stage", 'SPOT', (0, -5, 15), 9000, "ffe6c2", size=1.5, rot=(math.radians(18), 0, 0), spot=math.radians(36))
for x, y, z, s in ((0, 7.5, 11.5, 3.2), (-9.0, 12.0, 12.5, 2.4), (9.0, 12.0, 12.5, 2.4)):
    light("Chandelier Light", 'POINT', (x, y, z + s * .3), 2200 * s, "ffc27a", size=1.2)
for x in (-12.3, 12.3, -6.8, 6.8):
    light("Lantern Light", 'POINT', (x, WALL_Y - .9, GAL_Z + 4.6), 400, "ffb45e", size=.3)
for k in (-1, 1):
    light("Brazier Light", 'POINT', (k * 9.3, Y0 - 2.6, 1.2), 500, "ff7a2a", size=.4)
for x in (0, -9.5, 9.5):
    light("Arch Glow", 'AREA', (x, WALL_Y - 1.2, GAL_Z + 6), 900, "ffcf90", size=3, rot=(math.radians(90), 0, 0))

# ---------------- cameras + render ----------------
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.render.resolution_x = 1920; sc.render.resolution_y = 1080
sc.view_settings.view_transform = "AgX"
try: sc.view_settings.look = "AgX - Punchy"
except Exception as e: print("LOOK", e)
sc.view_settings.exposure = -.35
def camera(name, loc, target, vfov):
    cam = bpy.data.cameras.new(name); cam.sensor_fit = 'VERTICAL'; cam.angle_y = math.radians(vfov)
    o = bpy.data.objects.new(name, cam); coll("Cameras").objects.link(o); o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); return o
camA = camera("Cam A (lobby today)", (.6, -18.6, 7.0), (.6, 0, 1.75), 30)
camB = camera("Cam B (lower)", (.6, -19.5, 2.9), (.6, 0, 3.6), 38)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LobbyHall.blend"))
for tag, cam in (("A", camA), ("B", camB)):
    if CAMS not in ("both", tag): continue
    sc.camera = cam; sc.render.filepath = os.path.join(OUT, "lobby_%s.png" % tag)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", tag, flush=True)
