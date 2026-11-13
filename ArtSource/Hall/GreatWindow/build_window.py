# The title hall's great window (10-17), modelled for the concept (cream castle stone, gold leaf):
# a deep moulded surround, a moulded sill on a corbel table with a gold edge, six bays split by
# octagonal mullions on moulded bases, a row of short lights, a transom with gold rosettes, a row of
# pointed gothic lights with round openings in the spandrels (all in the band the title camera sees,
# 3.0 to 4.4 m), then tall clear lights above.
# Local frame: origin at the bottom centre of the opening on the wall face; X = Unity x, Y = Unity z
# (toward the camera is +Y), Z = up. In Unity it stands at (GreatCentre, GreatBottom, WallZ) turned 180.
# Usage: blender -b --factory-startup --python build_window.py -- <out_dir> [preview]
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:]
OUT = args[0]; PREVIEW = len(args) > 1 and args[1] == "preview"
_src = open(os.path.join(HERE, "build_hall.py"), encoding="utf-8").read().split("# ================= layout")[0]
sys.argv = sys.argv[:sys.argv.index("--") + 1] + [OUT, "A", "16"]
exec(_src)

def dressed(name, c1, c2, rough=.6, bump=.25):
    """Fine dressed sandstone: soft mottling, a fine grain and a light tooled bump."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; L = nt.links.new
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); b.inputs['Roughness'].default_value = rough
    tc = nt.nodes.new('ShaderNodeTexCoord')
    n1 = nt.nodes.new('ShaderNodeTexNoise'); n1.inputs['Scale'].default_value = 2.2; n1.inputs['Detail'].default_value = 8; L(tc.outputs['Object'], n1.inputs['Vector'])
    r1 = nt.nodes.new('ShaderNodeValToRGB'); e = r1.color_ramp.elements
    e[0].position, e[0].color = .35, srgb(c2); e[1].position, e[1].color = .7, srgb(c1); L(n1.outputs['Fac'], r1.inputs['Fac'])
    L(r1.outputs['Color'], b.inputs['Base Color'])
    n2 = nt.nodes.new('ShaderNodeTexNoise'); n2.inputs['Scale'].default_value = 40; n2.inputs['Detail'].default_value = 6; L(tc.outputs['Object'], n2.inputs['Vector'])
    bp = nt.nodes.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = bump; bp.inputs['Distance'].default_value = .01
    L(n2.outputs['Fac'], bp.inputs['Height']); L(bp.outputs['Normal'], b.inputs['Normal'])
    MATS[name] = m; return m

STONE = dressed("Window Stone", "eadcbf", "d3c19c")
SHADE = dressed("Window Reveal", "a8916c", "8c7656", rough=.75)
GILT = principled("Window Gold", "d9a441", rough=.3, metal=1.0)

W, H = 6.9, 2.8                 # opening
BAYS = 6
BW = W / BAYS
C = "Window"

def bx(name, center, size, mat, bevel=.0):
    return box(name, center, size, mat, C, bevel=bevel)

def prism(name, outline, y0, y1, mat):
    """Extrude a closed XZ outline (list of (x, z)) from y0 to y1."""
    bm = bmesh.new()
    vs0 = [bm.verts.new((x, y0, z)) for x, z in outline]; vs1 = [bm.verts.new((x, y1, z)) for x, z in outline]
    bm.faces.new(vs0[::-1]); bm.faces.new(vs1)
    n = len(outline)
    for i in range(n):
        bm.faces.new((vs0[i], vs0[(i + 1) % n], vs1[(i + 1) % n], vs1[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return obj(name, me, mat, C)

def cut(target, cutter):
    m = target.modifiers.new("Cut", 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = cutter; m.solver = 'EXACT'
    bpy.context.view_layer.objects.active = target
    with bpy.context.temp_override(object=target, active_object=target): bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cutter, do_unlink=True)

# ---------- v2 (10-17, 승규 님: the window did not match the polygon count of the block wall and the
# pillars): every part is laid in real stones like the wall's, each with rounded arrises (3-step bevel),
# a face that is not quite flat and a little tilt, the jambs coursed at the wall's .375 m courses,
# round mouldings smooth, the tracery's edges rounded.
import random
from mathutils import noise as mnoise
RNG = random.Random(1017)

def stone(name, center, size, mat, bevel=.022, bump=.006, cell=.09, tilt=.6):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * .3), segments=3, affect='EDGES', profile=.5)
    long = [e for e in bm.edges if e.calc_length() > cell * 1.5]
    if long:
        bmesh.ops.subdivide_edges(bm, edges=long, cuts=max(1, min(6, int(max(size) / cell))), use_grid_fill=True)
    bm.normal_update()
    seed = RNG.random() * 100
    half = Vector(size) / 2
    for v in bm.verts:
        n = v.normal
        # only the flat middles of faces move; arrises stay crisp
        inner = all(abs(v.co[i]) < half[i] - bevel * 1.2 or abs(n[i]) > .98 for i in range(3))
        if inner and max(abs(n.x), abs(n.y), abs(n.z)) > .98:
            v.co += n * mnoise.noise(v.co * 7 + Vector((seed, seed, seed))) * bump
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = obj(name, me, mat, C, center)
    o.rotation_euler = tuple(math.radians(RNG.uniform(-tilt, tilt)) for _ in range(3))
    return o

def course(name, x0, x1, z0, z1, y0, y1, mat, length=.62, offset=0.0):
    """A run of stones from x0 to x1, joints every `length` starting `offset` in."""
    xs = [x0]; x = x0 + (offset if offset > 0 else length)
    while x < x1 - .12: xs.append(x); x += length
    xs.append(x1)
    for a, b in zip(xs, xs[1:]):
        stone(name, ((a + b) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (b - a - .012, y1 - y0, z1 - z0 - .012), mat)

def bevel_mod(o, width=.012, segs=2):
    m = o.modifiers.new("Round", 'BEVEL'); m.width = width; m.segments = segs; m.limit_method = 'ANGLE'; m.angle_limit = math.radians(35)
    bpy.context.view_layer.objects.active = o
    with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons: p.use_smooth = True

COURSE = .375
# ---------- surround: jambs coursed like the wall (long and short stones in turn), two orders
for s in (-1, 1):
    for k in range(int(math.ceil((H + .4) / COURSE))):
        z0, z1 = k * COURSE - .2, (k + 1) * COURSE - .2
        if z0 > H + .2: break
        long = k % 2 == 0
        w = .5 if long else .34
        xo = s * (W / 2 + .14 + w / 2)
        stone("Jamb Stone", (xo, -.12, (z0 + z1) / 2), (w - .012, .66, COURSE - .012), STONE)
        # the inner order, stepped back
        stone("Jamb Order", (s * (W / 2 + .08), -.2, (z0 + z1) / 2), (.16, .5, COURSE - .012), STONE, bevel=.018)
    lathe("Jamb Roll", [(.055, 0), (.055, H), (0, H)], STONE, segs=20, coll_name=C, loc=(s * (W / 2 + .02), .06, 0))

# ---------- sill: stones a bay long, a drip moulding with a gold edge, a corbel table under it
course("Sill Stone", -W / 2 - .5, W / 2 + .5, -.2, 0, -.24, .28, STONE, length=1.15, offset=.5)
course("Sill Drip", -W / 2 - .5, W / 2 + .5, -.24, -.16, .26, .34, STONE, length=1.15, offset=.9)
bx("Sill Gilt", (0, .345, -.155), (W + 1.0, .012, .03), GILT)
n = int((W + .8) / .38)
for i in range(n):
    x = -(W + .8) / 2 + (i + .5) * (W + .8) / n
    # a carved corbel: three stepped stones, smaller towards the foot
    stone("Corbel", (x, .12, -.29), (.18, .24, .1), STONE, bevel=.018, cell=.05)
    stone("Corbel", (x, .1, -.37), (.15, .2, .08), STONE, bevel=.018, cell=.05)
    stone("Corbel", (x, .08, -.45), (.11, .15, .09), SHADE, bevel=.02, cell=.05)

# ---------- mullions: round shafts on moulded bases, capitals under the transom
T1, T2 = .55, 1.5
for i in range(1, BAYS):
    x = -W / 2 + i * BW
    lathe("Mullion Base", [(.135, 0), (.135, .04), (.12, .06), (.1, .075), (.105, .09), (.115, .1), (.09, .13), (.072, .16), (0, .16)],
          STONE, segs=24, coll_name=C, loc=(x, -.08, 0))
    lathe("Mullion", [(.07, 0), (.07, H), (0, H)], STONE, segs=24, coll_name=C, loc=(x, -.08, 0))
    lathe("Mullion Capital", [(.07, 0), (.08, .02), (.1, .05), (.115, .08), (.12, .1), (0, .1)], STONE, segs=24, coll_name=C, loc=(x, -.08, T1 - .1))
    lathe("Mullion Capital", [(.07, 0), (.08, .02), (.1, .05), (.115, .08), (.12, .1), (0, .1)], STONE, segs=24, coll_name=C, loc=(x, -.08, T2 - .1))
    # a gold rosette where mullion meets transom: a boss and eight petals
    lathe("Rosette", [(.055, 0), (.055, .015), (.04, .035), (0, .045)], GILT, segs=24, coll_name=C, loc=(x, .07, T1 + .06)).rotation_euler = (math.radians(-90), 0, 0)
    for k in range(8):
        a = k * math.pi / 4
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=.022, location=(x + math.cos(a) * .06, .07, T1 + .06 + math.sin(a) * .06))
        p = bpy.context.active_object; p.name = "Rosette Petal"; p.data.materials.append(GILT)
        for f in p.data.polygons: f.use_smooth = True
for s in (-1, 1):
    lathe("Jamb Shaft", [(.07, 0), (.07, H), (0, H)], STONE, segs=24, coll_name=C, loc=(s * W / 2, -.08, 0))

# ---------- transoms in stones a bay long, with a roll and a gold line
course("Transom Stone", -W / 2, W / 2, T1, T1 + .12, -.21, .05, STONE, length=BW)
lathe("Transom Roll", [(.025, -W / 2), (.025, W / 2), (0, W / 2)], STONE, segs=16, coll_name=C, loc=(0, .07, T1 + .06)).rotation_euler = (0, math.radians(90), 0)
bx("Transom Gilt", (0, .09, T1 + .005), (W, .01, .015), GILT)

# ---------- the gothic row: each bay its own pierced stone panel with rounded edges
spring, half = T1 + .3, BW / 2 - .085
r = half * 1.25
for i in range(BAYS):
    cx = -W / 2 + (i + .5) * BW
    panel = prism("Tracery", [(cx - BW / 2 + .004, T1 + .12), (cx + BW / 2 - .004, T1 + .12), (cx + BW / 2 - .004, T2), (cx - BW / 2 + .004, T2)], -.14, .02, STONE)
    pts = []
    cl, cr = cx + (r - half), cx - (r - half)
    apex = spring + math.sqrt(r * r - (r - half) ** 2)
    pts.append((cx - half, T1 + .1)); pts.append((cx + half, T1 + .1))
    for k in range(25):
        a1 = math.atan2(apex - spring, cx - cr); a = a1 * k / 24
        pts.append((cr + r * math.cos(a), spring + r * math.sin(a)))
    for k in range(1, 25):
        a0 = math.atan2(apex - spring, cx - cl); a = a0 + (math.pi - a0) * k / 24
        pts.append((cl + r * math.cos(a), spring + r * math.sin(a)))
    cut(panel, prism("Light Cut", pts, -.3, .2, SHADE))
    # half eyes at the bay's edges (a whole round opening over each mullion between two panels)
    for ex in (cx - BW / 2, cx + BW / 2):
        if -W / 2 + .01 < ex < W / 2 - .01:
            c = lathe("Eye Cut", [(.085, -.3), (.085, .3), (0, .3)], SHADE, segs=32, coll_name=C, loc=(ex, 0, T2 - .17))
            c.rotation_euler = (math.radians(90), 0, 0); bpy.context.view_layer.update()
            cut(panel, c)
    bevel_mod(panel)
course("Upper Transom Stone", -W / 2, W / 2, T2, T2 + .12, -.21, .05, STONE, length=BW, offset=BW / 2)
bx("Upper Gilt", (0, .055, T2 + .005), (W, .01, .015), GILT)
# head of the opening (out of the title frame, closes the shape)
course("Head Stone", -W / 2 - .65, W / 2 + .65, H, H + .3, -.5, .2, STONE, length=.62)

log_n = len([o for o in bpy.data.objects if o.type == 'MESH'])
print("WINDOW objects", log_n, flush=True)
os.makedirs(r"C:\Work\ChessFight\ArtSource\Hall\GreatWindow", exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=r"C:\Work\ChessFight\ArtSource\Hall\GreatWindow\GreatWindow.blend", compress=True)

if PREVIEW:
    # the title camera's view: camera at Unity (0, 2.2, 9), the opening's origin at (-1.95, 3.0, -5)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -.55, 1.4)); wall = bpy.context.active_object
    wall.scale = (30, 1, 1); wall.rotation_euler = (math.radians(90), 0, 0); wall.data.materials.append(dressed("Wall Prev", "e9dcc0", "cdbb98"))
    sky = principled("Sky Prev", "f2a88f", emit="f6b9a2", emit_k=2.0)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, -30, 5)); s = bpy.context.active_object; s.rotation_euler = (math.radians(90), 0, 0); s.data.materials.append(sky)
    cam = bpy.data.cameras.new("Cam"); cam.lens_unit = 'FOV'; cam.angle_y = math.radians(30); cam.sensor_fit = 'VERTICAL'
    co = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co)
    co.location = (1.95, 14, -.8)
    d = Vector((0, 0, .8)) - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    sc.camera = co
    sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 4; so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), 0, math.radians(200))
    fill = bpy.data.lights.new("Fill", 'SUN'); fill.energy = 1.2; fo = bpy.data.objects.new("Fill", fill); sc.collection.objects.link(fo)
    fo.rotation_euler = (math.radians(60), 0, math.radians(10))
    sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.render.filepath = os.path.join(OUT, "preview.png")
    bpy.ops.render.render(write_still=True)
    # a close look at the band the title shows
    co.location = (1.2, 5.5, .7); d = Vector((0, 0, .75)) - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, "preview_close.png")
    bpy.ops.render.render(write_still=True)
print("WINDOW DONE", flush=True)
