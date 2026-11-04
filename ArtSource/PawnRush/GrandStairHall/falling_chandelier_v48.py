# 승규 11-04: "계단 쪽 샹들리에도 텍스쳐 입혀줘 대형 샹들리에랑 똑같으면 안돼". The falling chandeliers over the
# stair's central landing (ChandelierRain, FallingChandelier.fbx: one v10 chandelier in 129 parts and its chain, still
# clay) dressed - not as the hub chandelier (gold, clear glowing crystal, cream wax, bright bulbs, R329/R330) but as its
# darker sibling: an antique silver frame, deep ruby crystals that pick up the crimson carpet, ivory candles with a dim
# flame. Its faces are sorted as chandelier_hub_v26.py sorted the hub's (the same design at half the hub's size there,
# so every distance is halved): candle tips found as spikes off the axis, followed down to their cups; drops and bead
# strings = small parts taller than they are wide, off the middle; the rest frame. Four materials Unity dresses by name
# (ChandelierRain): FC_Silver, FC_Ruby, FC_Wax, FC_Flame.
# The FBX is read back in and written out again with the same parts, names, origins and chain (round trip, checked).
# Run: blender -b -P falling_chandelier_v48.py -- [render]
#   -> Assets/Resources/PawnRushGrandStairHall/FallingChandelier.fbx, falling_chandelier_v48.blend, renders_fall_v48/
import bpy, math, os, sys
from mathutils import Vector, kdtree

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
SRC = os.path.join(RES, "FallingChandelier.fbx")
RENDER = "render" in sys.argv
K = .5                     # this chandelier against the hub's size when chandelier_hub_v26.py ran
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath=SRC, axis_forward="-Z", axis_up="Y")
obs = [o for o in bpy.data.objects if o.type == "MESH"]
parts = sorted([o for o in obs if o.name.startswith("Part_")], key=lambda o: o.name)
chain = [o for o in obs if o.name.startswith("Chain")]
before = {o.name: (tuple(round(v, 4) for v in o.matrix_world.translation), len(o.data.vertices)) for o in obs}
print("READ", len(parts), "parts,", len(chain), "chain")


def mat(name, rgb, metal, rough, emit=0.):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*rgb, 1)
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*rgb, 1)
    return m


SILVER = mat("FC_Silver", (.66, .66, .7), .9, .32)
RUBY = mat("FC_Ruby", (.55, .03, .08), .1, .08, .6)
WAX = mat("FC_Wax", (.95, .92, .85), 0, .5)
FLAME = mat("FC_Flame", (1, .62, .3), 0, .4, 3.)
SLOTS = [SILVER, RUBY, WAX, FLAME]

# ---- the candles' tips (chandelier_hub_v26.py with every distance halved; the axis is x = y = 0 here)
pts = []
for o in parts:
    M = o.matrix_world
    pts += [M @ v.co for v in o.data.vertices]
kd = kdtree.KDTree(len(pts))
for i, p in enumerate(pts):
    kd.insert(p, i)
kd.balance()
hr = lambda a, b: math.hypot(a.x - b.x, a.y - b.y)
axis_r = lambda p: math.hypot(p.x, p.y)
tips = []
for p in sorted((p for p in pts if axis_r(p) > .9 * K), key=lambda p: -p.z):
    if any(hr(p, t) < .4 * K for t, _ in tips):
        continue
    near = [q for q, _, _ in kd.find_range(p, 1.2 * K)]
    if any(q.z > p.z + 1e-4 and hr(q, p) < .4 * K for q in near):
        continue
    shaft = [q for q in near if p.z - .35 * K <= q.z <= p.z and hr(q, p) < .5 * K]
    if not shaft or max(hr(q, p) for q in shaft) > .21 * K:
        continue
    bottom, z = p.z - .35 * K, p.z - .35 * K
    while z > p.z - 1.6 * K:
        ring = [q for q in kd.find_range(Vector((p.x, p.y, z)), .45 * K) if abs(q[0].z - z) < .03 * K]
        if ring and max(hr(q[0], p) for q in ring) > .21 * K:
            break
        bottom, z = z, z - .04 * K
    tips.append((p, bottom))
print("CANDLES", len(tips), "tips at", sorted(round(t.z, 2) for t, _ in tips))


def zone(c):
    for t, b in tips:
        if hr(c, t) < .24 * K and b - .02 * K <= c.z <= t.z + .02 * K:
            return 3 if c.z >= (t.z + b) / 2 else 2
        if hr(c, t) < .17 * K and t.z - 1.3 * K <= c.z < b:
            return 2
    return None


count = [0, 0, 0, 0]
for o in parts:
    me = o.data
    me.materials.clear()
    for m in SLOTS:
        me.materials.append(m)
    M = o.matrix_world
    zones = [zone(M @ p.center) for p in me.polygons]
    candle = sum(1 for z in zones if z) / max(1, len(zones))
    vs = [M @ v.co for v in me.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    d, c = hi - lo, (hi + lo) / 2
    crystal = candle < .2 and max(d.x, d.y) <= .75 * K and d.z >= .35 * K and d.z >= .9 * max(d.x, d.y) and axis_r(c) > .2 * K
    for p, z in zip(me.polygons, zones):
        p.material_index = z if z else (1 if crystal else 0)
        count[p.material_index] += 1
for o in chain:
    o.data.materials.clear(); o.data.materials.append(SILVER)
print("FACES silver %d ruby %d wax %d flame %d" % tuple(count))

# ---- write it back (same objects, names and placement)
for o in sc.objects:
    o.select_set(o in obs)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.export_scene.fbx(filepath=SRC, **FBX)
print("EXPORT FallingChandelier.fbx")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "falling_chandelier_v48.blend"))

# ---- round trip check: read the new file and compare every object's place and size
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=SRC, axis_forward="-Z", axis_up="Y")
after = {o.name: (tuple(round(v, 4) for v in o.matrix_world.translation), len(o.data.vertices)) for o in bpy.data.objects if o.type == "MESH"}
moved = [n for n in before if n not in after or before[n] != after[n]]
print("ROUNDTRIP", len(before), "objects,", len(moved), "changed", moved[:5])

if RENDER:
    out = os.path.join(HERE, "renders_fall_v48"); os.makedirs(out, exist_ok=True)
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (.04, .035, .03, 1)
    for o in bpy.data.objects:
        if o.name.startswith("Chain"):
            o.hide_render = True
    for name, loc, e in (("Key", (-3, -4, 3.5), 700), ("Rim", (3, 3, 2.5), 400)):
        ld = bpy.data.lights.new(name, "AREA"); ld.energy = e; ld.size = 2.5; ld.color = (1, .93, .85)
        lo_ = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo_); lo_.location = loc
        lo_.rotation_euler = (Vector((0, 0, 1.3)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("cam"); cd.lens = 50
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam)
    cam.location = (1.2, -6.3, 2.3)
    cam.rotation_euler = (Vector((0, 0, 1.45)) - Vector(cam.location)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(out, "falling_chandelier.png")
    bpy.ops.render.render(write_still=True)
    print("RENDER", sc.render.filepath)
