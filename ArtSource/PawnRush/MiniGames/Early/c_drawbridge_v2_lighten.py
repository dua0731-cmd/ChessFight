# C · 해자 도개교 v2: lighter section model (R811, user 11-24 "c 해자 도개교 부터 하고 모양이 너무 달라지지 않게").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b C_Drawbridge_v2_export.blend --python c_drawbridge_v2_lighten.py
#   then copy Export/C_Drawbridge_EnvV2.fbx over Assets/Resources/PawnRushMiniGames/Early/C/C_Drawbridge_EnvV2.fbx
#   (keep its .meta), or run c_install.py.
#
# The 11-24 triangle audit (Docs/PawnRush/COURSE_POLYGON_AUDIT_2026-11-24.md) found the section model at 3,314,060
# triangles, 81 % of it the stone block module (6,000 triangles) placed 450 times on the walls and floors. Its surface
# detail is in the module's baked normal map, so the module itself is collapsed to MODULE_TRIS (the walls-and-modules
# budget in Materials/README.md is 1,000 ~ 1,500); nothing else changes. Runs on the blend c_drawbridge_v2_export.py
# saved (the section objects as exported), exports the section model with that script's settings and saves the blend.
import os
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
EXPORT = os.path.join(HERE, "Export", "C_Drawbridge_EnvV2.fbx")
MODULE_TRIS = 1500
MODULES = ("meshes[0].001", "meshes[0].001_floor")   # the wall module and its floor copy (c_drawbridge_v2.floor_mesh)


def tris(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def collapse(name, target):
    me = bpy.data.meshes.get(name)
    if me is None:
        raise SystemExit("no mesh " + name)
    n = tris(me)
    if n <= target:
        print("LIGHTEN %s already %d triangles" % (name, n))
        return
    tmp = bpy.data.objects.new("tmp", me)
    bpy.context.scene.collection.objects.link(tmp)
    m = tmp.modifiers.new("dec", "DECIMATE")
    m.ratio = target / n
    m.use_collapse_triangulate = True
    new = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp)
    users = [o for o in bpy.data.objects if o.data == me]
    for o in users:
        o.data = new
    bpy.data.meshes.remove(me)
    new.name = name
    print("LIGHTEN %s %d -> %d triangles (%d placements)" % (name, n, tris(new), len(users)))


def main():
    for name in MODULES:
        collapse(name, MODULE_TRIS)
    in_scene = set(bpy.context.view_layer.objects)
    env = [o for o in bpy.data.objects
           if o.type in ("MESH", "EMPTY") and not o.hide_get() and not o.hide_render and o in in_scene]
    for o in bpy.data.objects:
        o.select_set(False)
    for o in env:
        o.select_set(True)
    bpy.context.view_layer.objects.active = env[0]
    # the same settings as c_drawbridge_v2_export.py's export()
    bpy.ops.export_scene.fbx(filepath=EXPORT, use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z",
                             axis_up="Y", bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="OFF",
                             add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
    dg = bpy.context.evaluated_depsgraph_get()
    n = sum(tris(o.evaluated_get(dg).to_mesh()) for o in env if o.type == "MESH")
    print("EXPORT %s: %d objects, %d triangles" % (os.path.basename(EXPORT), len(env), n))
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)


main()
