"""Pawn Rush library (작전 회의실): put the hand-modelled cannon and the textured conveyor into the Unity FBX files (R371).

    blender --background --factory-startup --python cannon_conveyor_unity.py
    blender --background --factory-startup --python cannon_conveyor_unity.py -- --check D:/check   (also renders)

승규 님 approved the cannon v2 (cannon_model.py, R370) and the conveyor look (cannon_conveyor_look.py, R368) with
"Unity에 적용해".

Environment FBX: both cannons' bodies (BLOWER_<x>|Blower_GameReady_Arrowless|Dupli|, one shared mesh) get the
hand-built cannon, both fans (BLOWER_<x>_Fan, one shared mesh) the new fan; object names, places and the fans' hub
origins stay, so LibraryAirCannon spins and recoils them as before. Bevels are applied. Their material slots are the
flat parts "Library gold / teal / cream / steel / dark / grout".

Shuttle FBX (imported and written back as rail_shoes.py does: FBX time t lands on frame t * fps + 1, shifted back
so the 16 s round trip stays on frames 1 .. 385): the belt strips get UVs in the source panel's space and the deck
texture; the chassis gets a smart UV unwrap and its per-texel painted maps; the small parts flat "Library <part>".
Textures go to Assets/Resources/PawnRushLibrary/Conveyor: LibraryConveyorDeck, LibraryConveyorChassis (+ _MS).
Course01v4Builder.LibraryLooks.cs builds the Unity materials from these material names.
Running it again stops at once for a file already done.
"""
import math
import os
import sys

import bpy

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cannon_conveyor_look as look  # noqa: E402
import cannon_model  # noqa: E402
from export_library_fbx import SETTINGS  # noqa: E402

OUT = os.path.join(look.LIB, "Conveyor")
IMPORT_FRAME_SHIFT = -1.0


def fresh(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    return col


def bevelled(mesh, width):
    """The mesh with a bevel applied (on a throwaway object, as the cannons share their meshes)."""
    ob = bpy.data.objects.new("bevel tmp", mesh)
    bpy.context.scene.collection.objects.link(ob)
    cannon_model.bevel(ob, width)
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    ob.select_set(True)
    view.objects.active = ob
    bpy.ops.object.modifier_apply(modifier="Bevel")
    bpy.data.objects.remove(ob)
    clean(mesh)
    return mesh


def clean(mesh):
    """Dissolves the zero-area faces a bevel leaves where pieces meet (Unity reads their vertices as NaNs)."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges)
    flat = [f for f in bm.faces if f.calc_area() < 1e-9]
    bmesh.ops.delete(bm, geom=flat, context="FACES_ONLY")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def cannons():
    col = fresh("PawnRushLibraryEnvironment")
    bpy.ops.import_scene.fbx(filepath=look.ENV, use_custom_normals=True, use_anim=True)
    if bpy.data.meshes.get("Library cannon") is not None:
        print("cannons: already the hand-built cannon")
        return False
    body = bevelled(cannon_model.build_body(), .012)
    fan = bevelled(cannon_model.build_fan(), .003)
    swapped, old = 0, set()
    for o in list(col.all_objects):
        if o.type == "MESH" and o.name.startswith("BLOWER_"):
            old.add(o.data)
            o.data = fan if o.name.endswith("_Fan") else body
            swapped += 1
    for mesh in old:   # after the loop: removing data while walking the collection crashes Blender
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    if swapped != 4:
        raise SystemExit(f"expected 2 cannon bodies and 2 fans, swapped {swapped}; stopped")
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=look.ENV, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"cannons: body {len(body.polygons)} faces, fan {len(fan.polygons)} faces, "
          f"{len(col.all_objects)} objects -> {look.ENV}")
    return True


def conveyor():
    col = fresh("PawnRushLibraryShuttle")
    before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=look.SHUTTLE, use_custom_normals=True, use_anim=True)
    for action in set(bpy.data.actions) - before:
        look_shift(action, IMPORT_FRAME_SHIFT)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 1, 385
    scene.frame_set(1)
    if bpy.data.materials.get("Library conveyor parts") is not None:   # R376: the worn look with its parts atlas
        print("conveyor: already in its worn look")
        return False
    os.makedirs(OUT, exist_ok=True)
    look.dress_conveyor(OUT)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=look.SHUTTLE, use_active_collection=True, bake_anim=True, **SETTINGS)
    print(f"conveyor: {len(col.all_objects)} objects -> {look.SHUTTLE}")
    return True


def look_shift(action, delta):
    """Moves every key of action by delta frames (rail_shoes.py's shift_action; that script runs on import)."""
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


def check(out):
    """Renders the written files back (what Unity reads)."""
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=look.ENV)
    sc = bpy.context.scene
    for o in bpy.data.objects:
        if o.type == "LIGHT" or (o.type == "MESH" and "BLOWER" not in o.name
                                 and not o.name.startswith("GEO_fitted_stone")):
            o.hide_render = True
    for name, part in (("Library " + k, k) for k in cannon_model.PARTS):
        m = bpy.data.materials.get(name)
        if m is not None and m.use_nodes:
            c, mt, sm = cannon_model.PARTS[part]
            b = m.node_tree.nodes.get("Principled BSDF")
            b.inputs["Base Color"].default_value = (*c, 1)
            b.inputs["Metallic"].default_value = mt
            b.inputs["Roughness"].default_value = 1 - sm
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (("written_cannon.png", (-6.0, -9.2, 2.6), (-10.9, -4.0, 1.25), 40, 40),))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=look.SHUTTLE, use_anim=True)
    sc = bpy.context.scene
    deck = look.mapped("deck", os.path.join(OUT, "LibraryConveyorDeck"))
    chassis = look.mapped("chassis", os.path.join(OUT, "LibraryConveyorChassis"))
    parts = look.mapped("parts", os.path.join(OUT, "LibraryConveyorParts"))
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        if o.type == "MESH" and o.data.materials:
            n = o.data.materials[0].name
            if n.startswith("Library conveyor deck"):
                o.data.materials[0] = deck
            elif n.startswith("Library conveyor chassis"):
                o.data.materials[0] = chassis
            elif n.startswith("Library conveyor parts"):
                o.data.materials[0] = parts
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (("written_conveyor.png", (6.5, -17.5, 3.6), (0, -6.0, -1.0), 30, 30),))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    cannons()
    conveyor()
    if "--check" in argv:
        check(argv[argv.index("--check") + 1])


main()
