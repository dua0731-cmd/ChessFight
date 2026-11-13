"""Pawn Rush library conveyor: take the carriage off its wheels and let it slide on the guide rails (R288).

    blender --background --factory-startup --python rail_shoes.py
    blender --background --factory-startup --python rail_shoes.py -- --out D:/check   (to another folder)

Reads Assets/Resources/PawnRushLibrary/PawnRushLibraryShuttle.fbx, deletes the four Carriage_guide_wheel and the
four Carriage_bogie_axle_bracket meshes, and puts a Carriage_rail_shoe where each wheel was: a slide block bolted
under the chassis foot that wraps the fixed I-beam's top flange (top plate on the flange, cheeks down both sides,
lips hooked under it). The shoes are children of CONVEYOR_CARRIAGE, so they ride its 16 s round trip unchanged.
Writes the FBX back with export_library_fbx.SETTINGS. Running it again on the edited file changes nothing.
"""
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from export_library_fbx import SETTINGS  # noqa: E402

FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryShuttle.fbx"))
# Same import shift as build_library_blend.py: Blender puts FBX time t on frame t * fps + 1.
IMPORT_FRAME_SHIFT = -1.0
REMOVE = ("Carriage_guide_wheel", "Carriage_bogie_axle_bracket")

# Fixed guide rail (Fixed_guide_I_*), model space at frame 1: web centred on x = +-2.10, top flange 0.38 m wide
# from z -2.87 to -2.80. The chassis feet end at z -2.53.
RAIL_X = 2.10
FLANGE_TOP, FLANGE_BOTTOM, FLANGE_HALF = -2.80, -2.87, .19
WEB_HALF = .06
FOOT_BOTTOM = -2.53
SHOE_HALF_WIDTH = .30   # across the rail
SHOE_LENGTH = .90       # along the rail
CLEAR = .012            # sliding gap between shoe and flange
SINK = .04              # how far the top plate goes up into the foot so no seam shows


def shift_action(action, delta):
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


def box(bm, x0, x1, y0, y1, z0, z1):
    geom = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.transform(bm, verts=geom, matrix=Matrix.Diagonal((x1 - x0, y1 - y0, z1 - z0, 1)))
    bmesh.ops.translate(bm, verts=geom, vec=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def shoe_mesh(name):
    """One slide shoe in its own space: origin on the rail centre line at the flange top, x across, y along."""
    bm = bmesh.new()
    w, h = SHOE_HALF_WIDTH, SHOE_LENGTH / 2
    top = FOOT_BOTTOM + SINK - FLANGE_TOP
    under = FLANGE_BOTTOM - FLANGE_TOP - CLEAR - .05
    # Top plate riding on the flange, up into the chassis foot.
    box(bm, -w, w, -h, h, CLEAR, top)
    # A wider saddle under the foot so the block reads as bolted on, not glued.
    box(bm, -w - .05, w + .05, -h + .08, h - .08, top - .07, top)
    for s in (-1, 1):
        # Cheeks down past the flange edge on both sides.
        lo, hi = sorted((s * (FLANGE_HALF + CLEAR), s * w))
        box(bm, lo, hi, -h, h, under, CLEAR + .001)
        # Lips hooked under the flange, short of the web.
        lo, hi = sorted((s * (WEB_HALF + .04), s * w))
        box(bm, lo, hi, -h + .05, h - .05, under, FLANGE_BOTTOM - FLANGE_TOP - CLEAR)
        # Bolt heads on the saddle.
        for y in (-h + .16, h - .16):
            box(bm, s * (w + .02) - .025, s * (w + .02) + .025, y - .025, y + .025, top - .1, top - .07)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = argv[argv.index("--out") + 1] if "--out" in argv else os.path.dirname(FBX)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryShuttle")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    for action in set(bpy.data.actions) - before:
        shift_action(action, IMPORT_FRAME_SHIFT)
    scene.frame_start, scene.frame_end = 1, 385
    scene.frame_set(1)

    carriage = next(o for o in col.all_objects if o.name.startswith("CONVEYOR_CARRIAGE"))
    wheels = [o for o in col.all_objects if o.name.startswith(REMOVE[0])]
    if not wheels:
        print("no wheels left; the shuttle already slides on shoes")
        return
    spots = [o.matrix_world.translation.copy() for o in wheels]
    material = wheels[0].data.materials[0]
    for o in [o for o in col.all_objects if o.name.startswith(REMOVE)]:
        bpy.data.objects.remove(o, do_unlink=True)
    for mesh in [m for m in bpy.data.meshes if m.users == 0]:
        bpy.data.meshes.remove(mesh)

    for i, spot in enumerate(sorted(spots, key=lambda v: (v.x, v.y))):
        name = "Carriage_rail_shoe" + ("" if i == 0 else f".{i:03d}")
        mesh = shoe_mesh(name)
        mesh.materials.append(material)
        ob = bpy.data.objects.new(name, mesh)
        col.objects.link(ob)
        ob.parent = carriage
        side = 1 if spot.x > 0 else -1
        world = Matrix.Translation(Vector((side * RAIL_X, spot.y, FLANGE_TOP)))
        ob.matrix_parent_inverse = carriage.matrix_world.inverted()
        ob.matrix_world = world
        bevel = ob.modifiers.new("Bevel", "BEVEL")
        bevel.width, bevel.segments, bevel.limit_method = .012, 2, "ANGLE"

    # Export only this collection, exactly like export_library_fbx.py.
    view.active_layer_collection = view.layer_collection.children[col.name]
    path = os.path.join(out_dir, "PawnRushLibraryShuttle.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_active_collection=True, bake_anim=True, **SETTINGS)
    print(f"removed {len(wheels)} wheels + brackets, added {len(spots)} rail shoes -> {path}")


main()
