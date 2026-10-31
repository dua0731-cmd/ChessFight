"""Pawn Rush library conveyor map: rebuild the Blender work file from the two FBX files Unity uses.

The map was handed over as FBX only (commit 4945866 on seoungtest). This script imports both FBX files into one
.blend so the map can be edited in Blender and exported back with export_library_fbx.py.

    blender --background --factory-startup --python build_library_blend.py
    blender --background --factory-startup --python build_library_blend.py -- --out D:/other.blend

What it builds (collection names = FBX file names, so the exporter knows where each part goes):
  PawnRushLibraryEnvironment  the hall, floors, stairs, lanterns, markers (static)
  PawnRushLibraryShuttle      the moving conveyor; its 16 s round trip sits on frames 1..385 at 24 fps
  UNITY_Collisions_ref        wire boxes for the colliders Unity adds in code (not exported, see README)

Coordinates: the FBX importer gives Blender Z-up metres, the same space the map was authored in.
"""
import os
import sys

import bpy

sys.dont_write_bytecode = True  # keep __pycache__ out of the repository
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from export_library_fbx import SETTINGS  # noqa: E402  (same settings for the in-Blender export button)

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
FBX_DIR = os.path.join(REPO, "Assets", "Resources", "PawnRushLibrary")
PARTS = ["PawnRushLibraryEnvironment", "PawnRushLibraryShuttle"]
COLLIDERS = "UNITY_Collisions_ref"
FPS = 24
# The shuttle take was exported over frames 1..385 (FBX time 1/24 .. 385/24 s). Blender's FBX importer puts FBX
# time t on frame t * fps + 1, so the keys arrive one frame late; shift them back so an export writes the same times.
IMPORT_FRAME_SHIFT = -1.0


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.join(HERE, "PawnRushLibrary.blend")
    if "--out" in argv:
        out = argv[argv.index("--out") + 1]
    return out


def import_part(name):
    path = os.path.join(FBX_DIR, name + ".fbx")
    if not os.path.isfile(path) or os.path.getsize(path) < 1024:
        raise SystemExit(f"{path} is missing or is still a Git LFS pointer (run: git lfs pull)")
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    layer = bpy.context.view_layer.layer_collection.children[name]
    bpy.context.view_layer.active_layer_collection = layer
    before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True, use_anim=True)
    for action in set(bpy.data.actions) - before:
        shift_action(action, IMPORT_FRAME_SHIFT)
    add_exporter(col)
    print(f"{name}: {len(col.all_objects)} objects")
    return col


def add_exporter(col):
    # Collection Properties -> Exporters -> Export: writes this collection to its Unity FBX in one click.
    with bpy.context.temp_override(collection=col):
        bpy.ops.collection.exporter_add(name="IO_FH_fbx")
    props = col.exporters[-1].export_properties
    props.filepath = f"//../../../Assets/Resources/PawnRushLibrary/{col.name}.fbx"
    for key, value in SETTINGS.items():
        setattr(props, key, value)
    props.bake_anim = col.name == "PawnRushLibraryShuttle"


def merge_duplicate_materials():
    # Both FBX files carry the same material; the second import comes in as "<name>.001". Unity matches materials
    # by name, so point every user back at the first one and drop the copy.
    for mat in list(bpy.data.materials):
        base, dot, num = mat.name.rpartition(".")
        if dot and num.isdigit() and base in bpy.data.materials:
            mat.user_remap(bpy.data.materials[base])
            bpy.data.materials.remove(mat)


def shift_action(action, delta):
    # Blender 5 layered actions keep their curves in channelbags; older files keep action.fcurves.
    curves = []
    if hasattr(action, "layers") and action.layers:
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    curves.extend(bag.fcurves)
    else:
        curves = list(action.fcurves)
    for fc in curves:
        for k in fc.keyframe_points:
            k.co.x += delta
            k.handle_left.x += delta
            k.handle_right.x += delta
    action.use_fake_user = False


# --- Unity colliders --------------------------------------------------------------------------------------------
# Course01v4Builder.LibraryReplacement.cs adds these boxes under a pivot turned 180 degrees about Y. In that
# pivot's space (= the FBX model space Unity sees) a Unity point (x, y, z) is Blender (-x, -z, y), and a box size
# (sx, sy, sz) is Blender (sx, sz, sy). Keep this list in step with the C# file when either changes.
def unity_boxes():
    boxes = [
        ("Conveyor deck (moves with CONVEYOR_CARRIAGE)", (0, .24, 4.91593), (3.36, .28, 12.6)),
        ("Start floor", (0, -.12, 17.5), (29.9, .24, 10)),
        ("Left balcony", (-11.45, -.12, 2), (7.1, .24, 21)),
        ("Right balcony", (11.45, -.12, 2), (7.1, .24, 21)),
        ("Goal floor", (0, 1.08, -20), (29.9, .24, 10)),
        ("Lower library floor", (0, -8.35, 0), (30, .7, 45)),
        ("Return lower landing", (-16, -8.15, 3.6), (6, .3, 3)),
        ("Return upper landing", (-16, -.15, 16), (6, .3, 3)),
    ]
    for i in range(32):
        h = .25 * (i + 1)
        z = 3.8 + (i + .5) * .375
        boxes.append((f"Return stair {i}", (-17.6, -8 + h / 2, z), (3, h, .38)))
    for i in range(8):
        h = .15 * (i + 1)
        z = -8.75 - .5 * i
        boxes.append((f"Left stairs {i}", (-10.5, h / 2, z), (5, h, .5)))
        boxes.append((f"Right stairs {i}", (10.5, h / 2, z), (5, h, .5)))
    return boxes


def add_colliders():
    col = bpy.data.collections.new(COLLIDERS)
    bpy.context.scene.collection.children.link(col)
    col.hide_render = True
    col.color_tag = "COLOR_04"
    mat = bpy.data.materials.new("UNITY_Collision_ref")
    mat.diffuse_color = (0.1, 1.0, 0.3, 0.35)
    for name, (x, y, z), (sx, sy, sz) in unity_boxes():
        mesh = bpy.data.meshes.new("COL " + name)
        hx, hy, hz = sx / 2, sz / 2, sy / 2
        verts = [(a * hx, b * hy, c * hz) for a in (-1, 1) for b in (-1, 1) for c in (-1, 1)]
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(mat)
        ob = bpy.data.objects.new("COL " + name, mesh)
        ob.location = (-x, -z, y)
        ob.display_type = "WIRE"
        ob.show_in_front = True
        ob.hide_select = True
        col.objects.link(ob)
    print(f"{COLLIDERS}: {len(col.objects)} boxes")


def main():
    out = args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    for name in PARTS:
        import_part(name)
    merge_duplicate_materials()
    add_colliders()
    scene.frame_start, scene.frame_end = 1, 385
    scene.frame_current = 1
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection
    # Viewport: solid view far enough to see the 45 m hall.
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                for space in area.spaces:
                    if space.type == "VIEW_3D":
                        space.clip_end = 1000.0
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    print("saved", out)


main()
