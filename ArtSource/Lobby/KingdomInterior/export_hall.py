# Export the kingdom hall blockout for Unity: blender -b KingdomHall_blockout.blend --python export_hall.py
# Writes Assets/Resources/Lobby/KingdomHall/Hall.fbx. The stand-in stage and pawns are left out (Unity
# draws them). Every collection is joined into one mesh, except the parts the lobby code drives:
#   GateLeaf_L / GateLeaf_R  - empties on the hinges, each with one joined child (the leaf + its shield)
#   Gate Passage End         - the wall behind the gate that glows when it opens
# Axes as the lobby castle's FBX: Unity turns the root half round (LobbyStage.Hall.cs).
import bpy, os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "Lobby", "KingdomHall", "Hall.fbx"))
os.makedirs(os.path.dirname(OUT), exist_ok=True)

KEEP_APART = {"Gate Passage End"}
DROP = {"Stand-in Stage", "Stand-in Pawns", "Cameras"}

for c in list(bpy.data.collections):
    if c.name in DROP:
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)

def make_real(objs):
    """Curves -> meshes and every modifier applied."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_viewport = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target="MESH")
    return [o for o in bpy.context.selected_objects]

def join(objs, name):
    objs = make_real(objs)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    j.name = name
    j.data.name = name
    return j

# the gate leaves: join the children of each hinge empty
for side in ("L", "R"):
    pivot = bpy.data.objects[f"GateLeaf_{side}"]
    kids = [k for k in pivot.children if k.type in ("MESH", "CURVE")]
    leaf = join(kids, f"GateLeaf_{side}_Door")
    m = leaf.matrix_world.copy()
    leaf.parent = pivot
    leaf.matrix_world = m

leaf_parts = {o for o in bpy.data.objects if o.parent is not None and o.parent.name.startswith("GateLeaf_")}
for c in list(bpy.data.collections):
    objs = [o for o in c.objects if o.type in ("MESH", "CURVE") and o.name not in KEEP_APART and o not in leaf_parts]
    if objs:
        join(objs, "Hall_" + c.name.replace(" ", "_"))
for name in KEEP_APART:
    make_real([bpy.data.objects[name]])
    bpy.data.objects[name].name = name.replace(" ", "_")

# metres, as the castle's FBX (UnitScaleFactor 100); the blockout scene starts with no unit system
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0

tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
print("OBJECTS", sorted(o.name for o in bpy.data.objects))
print("TRIS", tris)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=False, object_types={"EMPTY", "MESH"},
                         apply_scale_options="FBX_SCALE_UNITS",   # metres + UnitScaleFactor 100, as Castle.fbx
                         use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                         path_mode="AUTO", embed_textures=False)
print("WROTE", OUT)
