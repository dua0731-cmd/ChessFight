# Removes the listed objects (never drawn in Play) from an FBX and writes it back with stair_pass.py's export settings.
import bpy, sys, os
a = sys.argv[sys.argv.index("--") + 1:]
src, listing, out = a[0], a[1], a[2]
names = [l.strip() for l in open(listing, encoding="utf8") if l.strip()]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src, use_custom_normals=True)
def tri(o):
    if o.type != "MESH": return 0
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)
before = sum(tri(o) for o in bpy.data.objects); n0 = len(bpy.data.objects)
gone, missing = 0, []
for n in names:
    o = bpy.data.objects.get(n)
    if o is None: missing.append(n); continue
    for c in list(o.children): c.parent = None   # nothing hangs off these, but keep any child where it is
    bpy.data.objects.remove(o); gone += 1
after = sum(tri(o) for o in bpy.data.objects)
bpy.ops.object.select_all(action="DESELECT")
for o in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active = next(o for o in bpy.data.objects if o.type == "MESH")
kinds = {o.type for o in bpy.data.objects} & {"MESH", "EMPTY", "ARMATURE", "LIGHT", "CAMERA", "OTHER"}
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types=kinds, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                         use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="STRIP",
                         embed_textures=False, colors_type="LINEAR")
print("PRUNE objects %d -> %d (removed %d), triangles %d -> %d, missing %s" % (n0, len(bpy.data.objects), gone, before, after, missing))
