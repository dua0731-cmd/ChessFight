"""Pawn Rush library (작전 회의실): the balcony balustrades stand on the balconies, not half over the drop (R422).

    blender --background --factory-startup --python rail_onto_floor.py                       (writes the FBX)
    blender --background --factory-startup --python rail_onto_floor.py -- --preview D:/check (renders, writes nothing)

승규 님 (11-05, a shot from below of the teal balustrade's base sticking out past the balcony's edge): "난간이 바닥 보다
나와 있어 수정해". The side balconies' floor (GEO_fitted_stone_Side_balcony, .001) ends at x -8.01 / +8.01, but the
balustrades along them (GAME_Railing_sector_+-1_1 .. 4: posts, rails, balusters, the cross run at the start and the
run up the goal stairs) were centred on x +-8.0 with a 0.34 m base, so their inner half hung over the drop all the way
(a ray down from x +-7.86 finds nothing from y -12.5 to 12.5). Each side's four pieces move 0.21 m outward together
(their joints stay as they were): the base now spans x +-8.03 .. 8.37, on the floor. The cannon openings (R421) move
with them. Running it again changes nothing (it checks the inner edge first).
"""
import os
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryEnvironment.fbx"))
SHIFT, FLOOR_EDGE = .21, 8.01


def sectors(side):
    return [o for o in bpy.data.objects if o.name.startswith(f"GAME_Railing_sector_{side}_") and o.type == "MESH"]


def edge_run(side):
    """The inner edge (nearest the hall's middle) of the long run along the balcony."""
    o = bpy.data.objects[f"GAME_Railing_sector_{side}_2"]
    xs = [(o.matrix_world @ v.co).x for v in o.data.vertices if (o.matrix_world @ v.co).y > -11]
    return min(xs) if side > 0 else max(xs)


def shift_all():
    moved = 0
    for side in (-1, 1):
        inner = edge_run(side)
        if abs(inner) >= FLOOR_EDGE:
            print(f"side {side}: inner edge {inner:.3f}, already on the floor")
            continue
        for o in sectors(side):
            if o.parent is not None:
                raise RuntimeError(o.name + " has a parent")
            o.matrix_world.translation += Vector((side * SHIFT, 0, 0))
            moved += 1
        bpy.context.view_layer.update()
        print(f"side {side}: inner edge {inner:.3f} -> {edge_run(side):.3f}, {moved} pieces moved")
    return moved


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    return col


def check():
    """Rays down beside the railing's inner face: all on the floor now."""
    rails = [o for o in bpy.data.objects if "Railing" in o.name]
    for o in rails:
        o.hide_viewport = True
    dg = bpy.context.evaluated_depsgraph_get()
    sc = bpy.context.scene
    for side in (-1, 1):
        inner = abs(edge_run(side)) + .02
        miss = [y for y in [-12 + k * .5 for k in range(42)]
                if not sc.ray_cast(dg, Vector((side * inner, y, .6)), Vector((0, 0, -1)), distance=1.0)[0]]
        print(f"side {side}: rays at x {side * inner:.2f} with no floor under them: {miss}")
    for o in rails:
        o.hide_viewport = False


def preview(out):
    import cannon_conveyor_look as look
    os.makedirs(out, exist_ok=True)
    load()
    shift_all()
    check()
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
        elif o.type == "MESH":
            cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
            o.hide_render = max(c.z for c in cs) > 4
    sun, cam = look.stage(sc)
    look.shoot(sc, sun, cam, out, (
        ("rail_edge_left_below.png", (-5.5, -6.5, -.6), (-8.0, -1.0, .3), 30, 90),
        ("rail_edge_right_below.png", (5.5, 2.5, -.6), (8.0, 8.0, .3), 30, 270),
    ))


def apply():
    from export_library_fbx import SETTINGS
    col = load()
    if not shift_all():
        return
    check()
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {FBX}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        preview(argv[argv.index("--preview") + 1])
    else:
        apply()
