# 승규 11-03, three fixes on v17:
#   1. the red hall floor (Hall/Hall_floor_(respawn)) widened sideways to x +-38.1 so it runs in under the v17 walls'
#      recesses (their back faces at x +-38.04) and no slot shows at the walls' foot
#   2. the eight floating stones (B3_Spiral/Floating_stone_N) drawn as 승규's solar panel cube
#      (source/solar_panel_cube.glb): 2.6 m square (the stones' 2.6 m round), 0.5 m thick, an edge along the radius
#   3. the jump pad (B3_Spiral/02_Jump_pad) drawn as 승규's glowing square platform (source/glowing_platform.glb):
#      3 m square (the kit pad's base), its arrow along the launch, standing on the landing in full 3D (승규: "점프 발판은
#      3D로", R311 v2): its deck 0.41 m up where the kit pad's plate was, the arrow and tiles in their own relief over it
# "flat" is what a foot feels: Unity gives each pad one box collider top (the deck plane), so WASD on it meets no
# edge. The stones' panel relief is pressed to 30 % of its height as well, so their top reads as that plane.
# The pads move (the stones bob), so they are not in the hall FBX: each goes out alone (origin = top centre) and Unity
# hangs it on its stone / pad. Here they stand where the stones and pad stand, only for the renders.
# The v9 "Floating stone keels" under the round stones go. No paint: Arch_Clay.
# Run: blender -b grand_stair_hall_v17.blend -P pads_v18.py   -> grand_stair_hall_v18.blend, FloatingPad.fbx, JumpPadModel.fbx
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TARGET = r"C:\Work\ChessFight-SeoungTest\Assets\Resources\PawnRushGrandStairHall"
CLAY = bpy.data.materials["Arch_Clay"]
HUB = Vector((0, 92, 0))
FLOOR_HALF = 38.1
sc = bpy.context.scene
originals = bpy.data.collections["_Blockout originals"]
col = bpy.data.collections.new("Pads_v18 (Unity hangs these on the moving pieces)"); sc.collection.children.link(col)
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)


def retire(o):
    for c in list(o.users_collection):
        c.objects.unlink(o)
    originals.objects.link(o)


def load(name, ratio):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", name))
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == "MESH"][0]
    me = src.data.copy(); me.transform(src.matrix_world)
    for o in new:
        bpy.data.objects.remove(o)
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.materials.clear(); me.materials.append(CLAY)
    o = bpy.data.objects.new("_d", me); sc.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("d", "DECIMATE"); m.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(o)
    return me


def fit(me, relief_from, side, height):
    """Press the relief above `relief_from` (share of the height) to 30 %, then size it side x side x height with its
    origin at the top centre."""
    vs = [v.co for v in me.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    t0 = lo.z + relief_from * (hi.z - lo.z)
    for v in me.vertices:
        if v.co.z > t0:
            v.co.z = t0 + (v.co.z - t0) * .3
    top = max(v.co.z for v in me.vertices)
    sx, sy, sz = side / (hi.x - lo.x), side / (hi.y - lo.y), height / (top - lo.z)
    me.transform(Matrix.Diagonal((sx, sy, sz, 1)) @ Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -top)))
    for p in me.polygons:
        p.use_smooth = False
    return me


def fit_standing(me, side, deck):
    """Full relief, side x side, the deck (the widest upward face level in the upper half) `deck` over the bottom and
    the origin on the deck's middle: what stands above it is the asset's own relief."""
    vs = [v.co for v in me.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    bins = {}
    for p in me.polygons:
        if p.normal.z > .95 and p.center.z > lo.z + .5 * (hi.z - lo.z):
            k = round(p.center.z, 3)
            bins[k] = bins.get(k, 0) + p.area
    level = max(bins, key=bins.get)
    s = deck / (level - lo.z)
    me.transform(Matrix.Diagonal((side / (hi.x - lo.x), side / (hi.y - lo.y), s, 1))
                 @ Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -level)))
    for p in me.polygons:
        p.use_smooth = False
    print("DECK at", round((level - lo.z) / (hi.z - lo.z), 3), "of the height, relief over it", round((hi.z - level) * s, 3), "m")
    return me


def smooth(o):
    for s in bpy.context.selected_objects:
        s.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))


def export(name, me, fbx):
    """Alone, for Unity: Blender (X, Y, Z) arrives as Unity (-x, y, -z) without the hall's 180-degree pivot, so the
    mesh turns half round to keep its +Y (the arrow) on the pad's +z."""
    m = me.copy(); m.transform(Matrix.Rotation(math.pi, 4, "Z"))
    o = bpy.data.objects.new(name, m); sc.collection.objects.link(o)
    smooth(o)
    for s in sc.objects:
        s.select_set(False)
    o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(TARGET, fbx), **FBX)
    print("EXPORT", fbx, len(m.polygons), "faces")
    bpy.data.objects.remove(o)


# 1. The red floor
floor = bpy.data.objects["Hall/Hall_floor_(respawn)"]
xs = [v.co.x for v in floor.data.vertices]
k = FLOOR_HALF / max(abs(x) for x in xs)
for v in floor.data.vertices:
    v.co.x *= k
print("FLOOR x", [round(min(v.co.x for v in floor.data.vertices) + floor.location.x, 2), round(max(v.co.x for v in floor.data.vertices) + floor.location.x, 2)])

# 2. The floating stones
pad = fit(load("solar_panel_cube.glb", .05), .85, 2.6, .5); pad.name = "Floating_pad"
export("Floating_pad", pad, "FloatingPad.fbx")
for i in range(1, 9):
    stone = bpy.data.objects["B3_Spiral/Floating_stone_%d" % i]
    vs = [stone.matrix_world @ v.co for v in stone.data.vertices]
    c = sum(vs, Vector()) / len(vs); top = max(v.z for v in vs)
    o = bpy.data.objects.new("Pad view/Floating_stone_%d" % i, pad); col.objects.link(o)
    o["unity_path"] = "(pad view)"      # not in the hall FBX: Unity hangs FloatingPad.fbx on the stone itself
    o.location = (c.x, c.y, top)
    o.rotation_euler.z = math.atan2(c.y - HUB.y, c.x - HUB.x)
    retire(stone)
smooth(bpy.data.objects["Pad view/Floating_stone_1"])
keels = bpy.data.objects.get("Floating stone keels")
if keels:
    retire(keels)

# 3. The jump pad
jump = fit_standing(load("glowing_platform.glb", .1), 3.0, .41); jump.name = "Jump_pad_model"
export("Jump_pad_model", jump, "JumpPadModel.fbx")
base = bpy.data.objects["B3_Spiral/02_Jump_pad/Jump_pad_base"]
def world(name):
    ob = bpy.data.objects[name]
    return [ob.matrix_world @ v.co for v in ob.data.vertices]
bv = world(base.name)
landing = min(v.z for v in bv)                                         # the landing's top under the pad
centre = sum(bv, Vector()) / len(bv)
shaft = world("B3_Spiral/02_Jump_pad/Direction_arrow/Shaft")
heads = world("B3_Spiral/02_Jump_pad/Direction_arrow/Arrowhead") + world("B3_Spiral/02_Jump_pad/Direction_arrow/Arrowhead#2")
ahead = sum(heads, Vector()) / len(heads) - sum(shaft, Vector()) / len(shaft)   # the kit arrow: heads ahead of the shaft
o = bpy.data.objects.new("Pad view/02_Jump_pad", jump); col.objects.link(o)
o["unity_path"] = "(pad view)"
o.location = (centre.x, centre.y, landing + .41)
o.rotation_euler.z = math.atan2(ahead.y, ahead.x) - math.pi / 2     # the model's arrow (+Y) along the launch
smooth(o)
print("JUMP landing", round(landing, 3), "yaw", round(math.degrees(o.rotation_euler.z), 1))
for p in [p for p in bpy.data.objects if p.name.startswith("B3_Spiral/02_Jump_pad/")]:
    retire(p)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v18.blend"))
