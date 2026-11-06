"""Pawn Rush library: give the two blower cannons a separate fan so Unity can spin it (R291).

    blender --background --factory-startup --python air_cannons.py
    blender --background --factory-startup --python air_cannons.py -- --out D:/check   (to another folder)

The cannons (BLOWER_A_left, BLOWER_B_right) came as one mesh each, so the fan inside the muzzle (a hub and ten
blades) could not move. This script reads Assets/Resources/PawnRushLibrary/PawnRushLibraryEnvironment.fbx, moves
each cannon's hub and blades into its own object BLOWER_<x>_Fan (a child of the BLOWER_<x> empty, origin on the
hub centre so it spins in place) and writes the FBX back with export_library_fbx.SETTINGS. The muzzle rim, the
back plate and the rim bolts stay on the body. Running it again on the edited file changes nothing.
Unity: LibraryAirCannon.cs spins the fan clockwise (seen from the front) and fires the air.
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
                                    "PawnRushLibraryEnvironment.fbx"))
CANNONS = ("BLOWER_A_left", "BLOWER_B_right")
# Measured along the muzzle in the cannon's own space: hub and blades start 0.74-0.79 m out from the cannon origin;
# the back plate starts at 0.70, the rim at 0.30 and the rim bolts at 1.0, so this window picks only the fan.
FAN_START = (.72, .85)
FAN_PARTS = 11  # hub + 10 blades


def islands(bm):
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, part = [v], []
        while stack:
            a = stack.pop()
            if a.index in seen:
                continue
            seen.add(a.index)
            part.append(a)
            stack.extend(e.other_vert(a) for e in a.link_edges)
        out.append(part)
    return out


def split_fan(col, name, done):
    """done: body mesh name -> (fan mesh, hub centre). B is A turned 180 degrees and shares A's mesh, so the
    second cannon only gets its own fan object on the fan mesh the first one made."""
    empty = bpy.data.objects.get(name)
    if empty is None:
        raise SystemExit(f"{name} not found")
    if any(c.name == name + "_Fan" for c in empty.children):
        print(f"{name}: fan already separate")
        return False
    body = next(c for c in empty.children if c.type == "MESH")
    if body.data.name in done:
        fan_mesh, centre = done[body.data.name]
        add_fan(col, name, empty, body, fan_mesh, centre)
        return True
    to_cannon = empty.matrix_world.inverted() @ body.matrix_world  # body mesh space -> cannon space
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.verts.ensure_lookup_table()
    fan = [p for p in islands(bm) if FAN_START[0] <= min((to_cannon @ v.co).x for v in p) <= FAN_START[1]]
    if len(fan) != FAN_PARTS:
        raise SystemExit(f"{name}: expected {FAN_PARTS} fan parts, found {len(fan)}; stopped")
    fan_verts = {v for p in fan for v in p}
    hub = max(fan, key=len)
    centre = sum((v.co for v in hub), Vector()) / len(hub)

    # The fan: a copy of the body mesh with everything else deleted, moved so its origin is the hub centre.
    fan_bm = bm.copy()
    fan_bm.verts.ensure_lookup_table()
    keep = {v.index for v in fan_verts}
    bmesh.ops.delete(fan_bm, geom=[v for v in fan_bm.verts if v.index not in keep], context="VERTS")
    bmesh.ops.translate(fan_bm, verts=fan_bm.verts, vec=-centre)
    fan_mesh = bpy.data.meshes.new(name + "_Fan")
    fan_bm.to_mesh(fan_mesh)
    fan_bm.free()
    for m in body.data.materials:
        fan_mesh.materials.append(m)
    done[body.data.name] = (fan_mesh, centre)
    add_fan(col, name, empty, body, fan_mesh, centre)

    bmesh.ops.delete(bm, geom=list(fan_verts), context="VERTS")
    bm.to_mesh(body.data)
    bm.free()
    print(f"{name}: fan of {len(fan_verts)} vertices separated")
    return True


def add_fan(col, name, empty, body, fan_mesh, centre):
    fan_ob = bpy.data.objects.new(name + "_Fan", fan_mesh)
    col.objects.link(fan_ob)
    fan_ob.parent = empty
    fan_ob.matrix_world = body.matrix_world @ Matrix.Translation(centre)
    print(f"{name}: fan at {tuple(round(x, 3) for x in fan_ob.matrix_world.translation)}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = argv[argv.index("--out") + 1] if "--out" in argv else os.path.dirname(FBX)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 24, 1.0
    col = bpy.data.collections.new("PawnRushLibraryEnvironment")
    scene.collection.children.link(col)
    view = bpy.context.view_layer
    view.active_layer_collection = view.layer_collection.children[col.name]
    bpy.ops.import_scene.fbx(filepath=FBX, use_custom_normals=True, use_anim=True)
    done = {}
    changed = [split_fan(col, n, done) for n in CANNONS]
    if not any(changed):
        return
    view.active_layer_collection = view.layer_collection.children[col.name]
    path = os.path.join(out_dir, "PawnRushLibraryEnvironment.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_active_collection=True, bake_anim=False, **SETTINGS)
    print(f"exported {len(col.all_objects)} objects -> {path}")


main()
