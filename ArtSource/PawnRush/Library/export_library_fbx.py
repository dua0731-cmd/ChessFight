"""Pawn Rush library conveyor map: export PawnRushLibrary.blend back to the FBX files Unity loads.

    blender --background PawnRushLibrary.blend --python export_library_fbx.py
    blender --background PawnRushLibrary.blend --python export_library_fbx.py -- --out D:/check   (to another folder)

Or inside Blender: Scripting tab -> open this file -> Run Script (saves nothing, only writes the FBX files).

Each collection named like an FBX file is written to Assets/Resources/PawnRushLibrary/<name>.fbx with the same
settings the map was first exported with (Blender's FBX defaults: -Z forward, Y up, scale 1, baked animation),
so Unity keeps the same .meta, the same node names and the same axes. UNITY_Collisions_ref is never exported.
"""
import os
import sys

import bpy

PARTS = ["PawnRushLibraryEnvironment", "PawnRushLibraryShuttle"]
# Names the Unity code looks up in the shuttle (Course01v4Builder.LibraryReplacement.cs).
REQUIRED = {"PawnRushLibraryShuttle": ["CONVEYOR_CARRIAGE"]}


# The settings the map was first exported with (Blender 5.2 FBX defaults, read back from the original files' header
# and transforms). build_library_blend.py copies them onto each collection's FBX exporter as well.
SETTINGS = dict(
    use_selection=False,
    use_visible=False,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_NONE",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=False,
    object_types={"EMPTY", "MESH", "LIGHT", "OTHER"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_custom_props=False,
    add_leaf_bones=False,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    # 0 = a key on every frame: the imported curves are already simplified once, so do not thin them again.
    bake_anim_simplify_factor=0.0,
    path_mode="AUTO",
    embed_textures=False,
)


def target_dir():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in argv:
        return argv[argv.index("--out") + 1]
    blend_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.path.dirname(os.path.abspath(__file__))
    return os.path.normpath(os.path.join(blend_dir, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary"))


def find_layer(layer, name):
    if layer.name == name:
        return layer
    for child in layer.children:
        found = find_layer(child, name)
        if found:
            return found
    return None


def check(name, col):
    names = [o.name for o in col.all_objects]
    for prefix in REQUIRED.get(name, []):
        if not any(n.startswith(prefix) for n in names):
            raise SystemExit(f"{name}: no object named {prefix}... (Unity needs it); export stopped")


def export(name, out_dir):
    col = bpy.data.collections.get(name)
    if col is None:
        raise SystemExit(f"collection {name} not found")
    check(name, col)
    view = bpy.context.view_layer
    layer = find_layer(view.layer_collection, name)
    was_excluded = layer.exclude
    layer.exclude = False
    view.active_layer_collection = layer
    path = os.path.join(out_dir, name + ".fbx")
    animated = any(o.animation_data and o.animation_data.action for o in col.all_objects)
    bpy.ops.export_scene.fbx(filepath=path, use_active_collection=True, bake_anim=animated, **SETTINGS)
    layer.exclude = was_excluded
    print(f"exported {name}: {len(col.all_objects)} objects{' + animation' if animated else ''} -> {path}")


def main():
    scene = bpy.context.scene
    if scene.render.fps != 24 or scene.render.fps_base != 1.0:
        raise SystemExit("scene must stay at 24 fps: the shuttle round trip is 384 frames = 16 s")
    out_dir = target_dir()
    os.makedirs(out_dir, exist_ok=True)
    active = bpy.context.view_layer.active_layer_collection
    for name in PARTS:
        export(name, out_dir)
    bpy.context.view_layer.active_layer_collection = active


if __name__ == "__main__":
    main()
