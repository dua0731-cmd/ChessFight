# Exports the fitted Tripo hall (v7) for Unity: its mesh in the hall's own frame (Blender X/Y/Z = Unity x/z/y, metres)
# plus the clean central landing (LandingTop, LandingWalls), the garden's FBX settings (Unity shows it through a 180°
# pivot, as the garden does), the textures copied beside it.
# Run: blender -b grand_stair_hall_v7.blend -P export_tripo_unity.py -- <Unity Assets/Resources/PawnRushGrandStairHall>
import bpy, os, sys
import numpy as np

target = sys.argv[sys.argv.index("--") + 1]
os.makedirs(target, exist_ok=True)
src = bpy.data.objects["Tripo royal circular hall"]

# A copy with the placement baked into the vertices, so the FBX node is plain.
ob = src.copy(); ob.data = src.data.copy(); ob.name = "GrandStairHallTripo"
bpy.context.scene.collection.objects.link(ob)
ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
n = len(ob.data.vertices); co = np.zeros(n * 3); ob.data.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
print("BOUNDS x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f  mean x %.3f" % (co[:, 0].min(), co[:, 0].max(), co[:, 1].min(), co[:, 1].max(), co[:, 2].min(), co[:, 2].max(), co[:, 0].mean()))

bpy.ops.object.select_all(action="DESELECT")
ob.select_set(True); bpy.context.view_layer.objects.active = ob
# v7: the clean central landing (already in hall metres) goes in the same file, its captured textures beside it.
for name, tex in (("LandingTop", "landing_stone.png"), ("LandingWalls", "wall_panel.png")):
    part = bpy.data.objects.get(name)
    if part is None: continue
    part.select_set(True)
    import shutil
    shutil.copyfile(os.path.join(os.path.dirname(bpy.data.filepath), "textures", tex),
                    os.path.join(target, "GrandStairHallTripo_" + ("LandingStone" if "stone" in tex else "WallPanel") + ".png"))
    print("PART", name)
bpy.ops.export_scene.fbx(filepath=os.path.join(target, "GrandStairHallTripo.fbx"), use_selection=True, object_types={"MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                         bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False,
                         path_mode="STRIP", embed_textures=False)
# The textures under fixed names the hall code loads.
for node in src.data.materials[0].node_tree.nodes:
    if node.type != "TEX_IMAGE": continue
    name = node.image.name.lower()
    key = "BaseColor" if "basecolor" in name else "Normal" if "normal" in name else "RoughMetal" if "_rm" in name else None
    if key is None: continue
    img = node.image
    ext = os.path.splitext(img.filepath or img.name)[1] or ".png"
    path = os.path.join(target, "GrandStairHallTripo_" + key + ext)
    if img.packed_file is not None:
        with open(path, "wb") as f: f.write(img.packed_file.data)
    else:
        import shutil; shutil.copyfile(bpy.path.abspath(img.filepath), path)
    print("TEXTURE", key, path, os.path.getsize(path))
print("EXPORTED", os.path.getsize(os.path.join(target, "GrandStairHallTripo.fbx")))
