# 승규 11-04 (a picture of the grand stair's obstacle: a rolled red carpet with gold ends rolling down the steps):
# "계단 장애물 모델링 해야 하는데 일단 1개만 하자". The rolling carpet (RollingSpawner: the small roll you jump, a capsule
# 0.5 m across and 3.4 m long lying across its lane, turning about its length) as a model that fits that capsule:
#   RollingCarpet_Roll  the rolled carpet, 3.2 m, r 0.24, in the hall's carpet texture (HallCarpet.png, 2 m a tile), its
#                       loose end lapping over it, woven gold borders near both ends (the carpet's edges), and on each
#                       end face the roll's spiral (RollingCarpet_End.png: crimson turns, darker gaps, gold edge threads)
#   RollingCarpet_Hubs  a gold hub on each end: rim, dished face, dome, eight bolts, axle boss
# Along Blender x, centred on the origin (Unity: along x, the roll's axis; symmetric, so the FBX x flip changes nothing).
# Run: blender -b -P carpet_roll_v46.py -- [render]
#   -> Assets/Resources/PawnRushGrandStairHall/RollingCarpet.fbx, RollingCarpet_End.png, carpet_roll_v46.blend,
#      renders_roll_v46/
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
RENDER = "render" in sys.argv
PI = math.pi
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

R, HALF = .24, 1.6            # the carpet's radius and half length
SEG = 64

# ---------------------------------------------------------------- the end face's spiral texture
n = 512
u = (np.arange(n) + .5) / n * 2 - 1
U, V = np.meshgrid(u, u)
rad, th = np.hypot(U, V), np.arctan2(V, U)
turns = 14
phase = rad * turns - th / (2 * PI)
f = phase - np.floor(phase)
gap = np.exp(-((f - .5) / .06) ** 2)                       # the dark seam between turns
thread = np.exp(-((f - .5) / .16) ** 2) * (np.floor(phase) % 3 == 0)
rgb = np.zeros((n, n, 3))
rgb[...] = (.55, .06, .08)
rgb *= (1 - .55 * gap)[..., None]
rgb = rgb * (1 - .5 * thread[..., None]) + np.array((.8, .6, .25)) * (.5 * thread[..., None])
rgb[rad < .3] = (.25, .03, .04)                            # the core (under the hub)
img = bpy.data.images.new("RollingCarpet_End", n, n, alpha=False)
px = np.ones((n, n, 4), np.float32); px[..., :3] = np.clip(rgb, 0, 1)
img.pixels.foreach_set(px.ravel())
img.filepath_raw = os.path.join(RES, "RollingCarpet_End.png"); img.file_format = "PNG"; img.save()
carpet_img = bpy.data.images.load(os.path.join(RES, "HallCarpet.png"))
gold_img = bpy.data.images.load(os.path.join(RES, "RoyalFan_Gold.png"))


def material(name, base, metal=0., rough=.5, image=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*base, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if image:
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = image
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m


M_CARPET = material("Roll_Carpet", (.5, .05, .06), 0, .85, carpet_img)
M_END = material("Roll_End", (.5, .05, .06), 0, .9, img)
M_GOLD = material("Stair_Gold", (.86, .64, .28), 1, .28, gold_img)


def ring_shell(bm, uv, x0, x1, r0, r1, a0=0., a1=2 * PI, segs=SEG, mi=0, tile=2.0):
    """A shell (r0 .. r1) round x from x0 to x1 over angles a0 .. a1; outside UVs u round (metres / tile), v along."""
    full = abs(a1 - a0 - 2 * PI) < 1e-6
    k = segs if full else max(2, int(segs * (a1 - a0) / (2 * PI)))
    ang = [a0 + (a1 - a0) * i / k for i in range(k + (0 if full else 1))]
    def ring(x, r):
        return [bm.verts.new((x, r * math.cos(a), r * math.sin(a))) for a in ang]
    o0, o1, i0, i1 = ring(x0, r1), ring(x1, r1), ring(x0, r0), ring(x1, r0)
    m = len(ang)
    faces = []
    for j in range(m if full else m - 1):
        q = (j + 1) % m
        f = bm.faces.new((o0[j], o0[q], o1[q], o1[j])); f.material_index = mi
        for l, (aa, xx) in zip(f.loops, ((ang[j], x0), (ang[q] if q else (2 * PI if full else ang[q]), x0),
                                         (ang[q] if q else (2 * PI if full else ang[q]), x1), (ang[j], x1))):
            l[uv].uv = (aa * r1 / tile, xx / tile)
        faces.append(f)
        for quad in ((i0[j], i1[j], i1[q], i0[q]), (o0[j], i0[j], i0[q], o0[q]), (o1[j], o1[q], i1[q], i1[j])):
            g = bm.faces.new(quad); g.material_index = mi
    if not full:
        for quad in ((o0[0], o1[0], i1[0], i0[0]), (o0[-1], i0[-1], i1[-1], o1[-1])):
            g = bm.faces.new(quad); g.material_index = mi
    return faces


def lathe_x(bm, prof, segs, mi):
    """Profile (r, x) turned about the x axis."""
    rings = [[bm.verts.new((x, r * math.cos(2 * PI * i / segs), r * math.sin(2 * PI * i / segs))) for r, x in prof] for i in range(segs)]
    for i in range(segs):
        a, b = rings[i], rings[(i + 1) % segs]
        for j in range(len(prof) - 1):
            f = bm.faces.new((a[j], b[j], b[j + 1], a[j + 1])); f.material_index = mi


# ---------------------------------------------------------------- the roll
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
ring_shell(bm, uv, -HALF, HALF, .05, R, mi=0)                       # the rolled body (a thick shell, the core hidden)
ring_shell(bm, uv, -HALF, HALF, R - .002, R + .007, 0., 1.15, mi=0)  # the loose end lapping over
for sx in (-1, 1):                                                  # woven gold borders: a wide band and a thin line
    a, b = sorted((sx * (HALF - .2), sx * (HALF - .05)))
    ring_shell(bm, uv, a, b, R + .0005, R + .0095, mi=2, tile=.6)
    a, b = sorted((sx * (HALF - .29), sx * (HALF - .26)))
    ring_shell(bm, uv, a, b, R + .0005, R + .0095, mi=2, tile=.6)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
# the spiral end faces (planar UVs over the end texture): 2 mm clear of the body's end ring and wound outwards by
# hand (on the ring they flickered and the normal recalculation turned them in - the first render showed them black)
for sx in (-1, 1):
    x = sx * (HALF + .002)
    centre = bm.verts.new((x, 0, 0))
    edge = [bm.verts.new((x, R * math.cos(2 * PI * i / SEG), R * math.sin(2 * PI * i / SEG))) for i in range(SEG)]
    for i in range(SEG):
        tri = (centre, edge[i], edge[(i + 1) % SEG]) if sx > 0 else (centre, edge[(i + 1) % SEG], edge[i])
        f = bm.faces.new(tri); f.material_index = 1
        for l in f.loops:
            l[uv].uv = (.5 + l.vert.co.y / (2 * R) * .98, .5 + l.vert.co.z / (2 * R) * .98)
for f in bm.faces:
    f.smooth = abs(f.normal.x) < .5
me = bpy.data.meshes.new("RollingCarpet_Roll"); bm.to_mesh(me); bm.free()
for m in (M_CARPET, M_END, M_GOLD):
    me.materials.append(m)
roll = bpy.data.objects.new("RollingCarpet_Roll", me); sc.collection.objects.link(roll)

# ---------------------------------------------------------------- the gold hubs
bm = bmesh.new()
bm.loops.layers.uv.new("UVMap")
for sx in (-1, 1):
    x = lambda d: sx * (HALF + d)
    prof = [(0, x(-.001)), (.175, x(-.001)), (.18, x(.012)), (.172, x(.03)), (.15, x(.036)), (.12, x(.03)),
            (.09, x(.04)), (.07, x(.06)), (.04, x(.075)), (0, x(.08))]
    lathe_x(bm, prof, 48, 0)
    for k in range(8):                                              # bolts on the dished face
        a = 2 * PI * k / 8 + PI / 8
        c = Vector((x(.035), .125 * math.cos(a), .125 * math.sin(a)))
        b2 = bmesh.new()
        bmesh.ops.create_uvsphere(b2, u_segments=10, v_segments=6, radius=.016)
        bmesh.ops.translate(b2, vec=c, verts=b2.verts)
        tmp = bpy.data.meshes.new("t"); b2.to_mesh(tmp); b2.free(); bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    for k in range(24):                                             # a knurled rim
        a = 2 * PI * k / 24
        b2 = bmesh.new()
        bmesh.ops.create_cube(b2, size=1)
        bmesh.ops.scale(b2, vec=(.03, .03, .022), verts=b2.verts)
        bmesh.ops.rotate(b2, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(a, 3, "X"), verts=b2.verts)
        bmesh.ops.translate(b2, vec=(x(.006), .172 * math.cos(a), .172 * math.sin(a)), verts=b2.verts)
        tmp = bpy.data.meshes.new("t"); b2.to_mesh(tmp); b2.free(); bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
uvl = bm.loops.layers.uv.verify()
for f in bm.faces:
    f.smooth = True
    for l in f.loops:
        l[uvl].uv = (l.vert.co.y / .6, l.vert.co.z / .6)
me = bpy.data.meshes.new("RollingCarpet_Hubs"); bm.to_mesh(me); bm.free()
me.materials.append(M_GOLD)
hubs = bpy.data.objects.new("RollingCarpet_Hubs", me); sc.collection.objects.link(hubs)
print("ROLL", len(roll.data.polygons) + len(hubs.data.polygons), "faces")

for o in sc.objects:
    o.select_set(o in (roll, hubs))
bpy.context.view_layer.objects.active = roll
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "RollingCarpet.fbx"), **FBX)
print("EXPORT RollingCarpet.fbx")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "carpet_roll_v46.blend"))

if RENDER:
    out = os.path.join(HERE, "renders_roll_v46"); os.makedirs(out, exist_ok=True)
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 1200, 700
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (.03, .03, .035, 1)
    for name, loc, e in (("Key", (-2, -3, 3), 500), ("Rim", (3, 2, 2), 300)):
        ld = bpy.data.lights.new(name, "AREA"); ld.energy = e; ld.size = 2
        lo = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    roll.rotation_euler.x = hubs.rotation_euler.x = .6
    for name, loc, at in (("roll", (1.2, -3.2, 1.0), (0, 0, 0)), ("end", (2.4, -.9, .35), (1.6, 0, 0))):
        cd = bpy.data.cameras.new(name); cd.lens = 45; cd.clip_start = .02
        cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
        cam.location = loc
        cam.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        sc.render.filepath = os.path.join(out, "roll_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)
