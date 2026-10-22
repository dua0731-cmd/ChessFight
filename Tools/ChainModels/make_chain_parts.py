"""Blender: turn the Tripo downloads into the rook chain's game parts (R153).
make.py -- <out_dir> <handle.glb> <hook.glb>
Handle: parts 1-5 (part_0, a tapered piece hanging under it, goes): grips (2, 5) -> Wood, band + ring (1) -> Gold, end caps
(3, 4) -> Cap. Grip along X, 0.36 m long, the ring's bottom at the origin, the grip above it (+Z).
Hook: the figure's top only: the ring (+ its plate) -> Gold, the hook -> Steel; the ring's top at the origin, the hook
running away along +Y (the chain's way). 0.3 m long.
Each object decimated to about 2500 faces, smooth shaded, exported as FBX (Y up, metres)."""
import bpy, sys, os, mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir, handle_path, hook_path = argv


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load(path):
    bpy.ops.import_scene.gltf(filepath=path)
    return {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


def decimate(o, faces):
    n = len(o.data.polygons)
    if n > faces:
        m = o.modifiers.new("dec", "DECIMATE")
        m.ratio = faces / n
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="dec")
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.materials.clear()


def bounds(objs):
    lo = mathutils.Vector((1e9,) * 3)
    hi = -lo
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            lo = mathutils.Vector((min(lo[k], w[k]) for k in range(3)))
            hi = mathutils.Vector((max(hi[k], w[k]) for k in range(3)))
    return lo, hi


def place(objs, origin, scale, rot=None):
    for o in objs:
        o.location -= origin
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        o.location *= scale
        o.scale = (scale,) * 3
        if rot is not None:
            o.rotation_euler = rot
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # rotation about the world origin: apply after moving
    if rot is not None:
        pass


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    lo, hi = bounds(objs)
    print("EXPORT", os.path.basename(path), [o.name + ":" + str(len(o.data.polygons)) for o in objs], "size", [round(x, 3) for x in hi - lo])


# ---- handle
reset()
parts = load(handle_path)
bpy.data.objects.remove(parts["tripo_part_0"])
wood = join([parts["tripo_part_2"], parts["tripo_part_5"]], "Wood")
gold = join([parts["tripo_part_1"]], "Gold")
cap = join([parts["tripo_part_3"], parts["tripo_part_4"]], "Cap")
objs = [wood, gold, cap]
for o, f in ((wood, 3000), (gold, 2500), (cap, 1500)):
    decimate(o, f)
lo, hi = bounds(objs)
length = hi.x - lo.x
origin = mathutils.Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))   # the ring's bottom, under the middle
place(objs, origin, 0.36 / length)
export(objs, os.path.join(out_dir, "RookChainHandle.fbx"))

# ---- hook
reset()
parts = load(hook_path)
for name, o in list(parts.items()):
    if name not in ("tripo_part_6", "tripo_part_7"):
        bpy.data.objects.remove(o)
ring = join([parts["tripo_part_6"]], "Gold")
hook = join([parts["tripo_part_7"]], "Steel")
objs = [ring, hook]
for o, f in ((ring, 2500), (hook, 2500)):
    decimate(o, f)
lo, hi = bounds(objs)
height = hi.z - lo.z
rlo, rhi = bounds([ring])
origin = mathutils.Vector(((rlo.x + rhi.x) / 2, (rlo.y + rhi.y) / 2, rhi.z))   # the ring's top
place(objs, origin, 0.3 / height)
# Hanging down (-Z) -> running along +Y (the chain's way): turn -90 degrees about X.
for o in objs:
    o.data.transform(mathutils.Matrix.Rotation(-1.5707963, 4, "X"))
    o.data.update()
export(objs, os.path.join(out_dir, "RookChainHook.fbx"))
