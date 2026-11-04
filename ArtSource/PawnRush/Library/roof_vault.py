"""Pawn Rush library (작전 회의실): give the vault webs an inner side so the roof shows from inside the map (R380).

    blender --background --factory-startup --python roof_vault.py                       (writes the FBX)
    blender --background --factory-startup --python roof_vault.py -- --preview D:/check (renders before / after, writes nothing)

승규 님 (a shot up at the roof from inside, only the ribs and beams against the sky, and one of the roof from outside,
stone everywhere): "맵 아래에서 보면 지붕이 첫번 째 사진 처럼 보이는데 밖에서 보면 이렇게 보여 맵 안에 있을 때도 지붕 표현
보이게 수정해줘".

The eight vault webs (Vault web coursed stone bay 00 .. 07) are single-sided: every face looks up and out, none
down, the coursed stones open-edged sheets. Unity culls back faces, so from below they vanish and only the ribs
(closed solids) show. Each stone of each web gets a twin face facing down in the same place: an inner face under every
stone, so the vault reads from the hall floor as from outside. Running it again changes nothing (it checks for
down-facing faces first). The preview renders with back-face culling on, as Unity draws.
"""
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

FBX = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary",
                                    "PawnRushLibraryEnvironment.fbx"))
WEB = "Vault web coursed stone bay"



def downward(ob):
    m = ob.matrix_world.to_3x3()
    return sum(1 for p in ob.data.polygons if (m @ p.normal).z < -.2)


def thicken(col):
    view = bpy.context.view_layer
    done = 0
    for ob in [o for o in col.all_objects if o.type == "MESH" and o.name.startswith(WEB)]:
        if downward(ob) > 0:
            print(f"{ob.name}: already two-sided")
            continue
        if ob.data.users > 1:
            ob.data = ob.data.copy()
        before = len(ob.data.polygons)
        # Every stone gets a twin face in the same place facing down (a solidify spiked out at the groins' acute
        # stones): with back faces culled each side shows one face, no z-fighting, no gaps added.
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        twin = bmesh.ops.duplicate(bm, geom=list(bm.faces))
        bmesh.ops.reverse_faces(bm, faces=[g for g in twin["geom"] if isinstance(g, bmesh.types.BMFace)])
        bm.to_mesh(ob.data)
        bm.free()
        ob.data.update()
        print(f"THICK {ob.name}: {before} -> {len(ob.data.polygons)} faces, down-facing {downward(ob)}")
        done += 1
    return done


def load(collection_name="PawnRushLibraryEnvironment"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new(collection_name)
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    return col


def look_up(out, name):
    """A view up at the vault from the start floor, back faces culled as in Unity."""
    from cannon_conveyor_look import shoot, stage
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_backface_culling = True
    clay = bpy.data.materials.get("CLAY | uniform neutral gray")
    if clay is not None and clay.use_nodes:
        clay.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.46, .36, .27, 1)
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
    sun, cam = stage(sc)
    world = sc.world.node_tree.nodes["Background"]
    world.inputs["Color"].default_value = (.35, .55, .95, 1)        # a sky, so holes show
    shoot(sc, sun, cam, out, ((name, (2.0, -14.0, 2.0), (0.0, 2.0, 15.0), 22, 30),))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        out = argv[argv.index("--preview") + 1]
        os.makedirs(out, exist_ok=True)
        load()
        look_up(out, "roof_before.png")
        col = load()
        thicken(col)
        look_up(out, "roof_after.png")
        return
    from export_library_fbx import SETTINGS
    col = load()
    if thicken(col) == 0:
        return
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.export_scene.fbx(filepath=FBX, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {FBX}")


main()
