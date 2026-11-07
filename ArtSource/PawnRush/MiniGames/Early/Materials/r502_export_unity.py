# R502 step 2: the stone block module and the phase-1 water maps into Unity.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b StoneBlock_Module.blend --python r502_export_unity.py
#
# Writes under Assets/Resources/PawnRushMiniGames/Early/R502/ (with .meta files, GUIDs from mg_export.guid):
#   PR_Early_StoneBlock.fbx                      the 1 m game module (6k triangles; Blender x, y, z = Unity x, z, y
#                                                under a pivot turned 180 degrees about y, as every mini-game FBX)
#   Textures/PR_Early_StoneBlock_BaseColor.png   sRGB
#   Textures/PR_Early_StoneBlock_Normal.png      normal map (OpenGL, as Unity)
#   Textures/PR_Early_StoneBlock_AO.png          linear
#   Textures/PR_Early_StoneBlock_MetallicSmoothness.png  linear, R = metallic 0, A = 1 - roughness (URP Lit)
#   Textures/PR_Early_Water_Normal.png           normal map, periodic 2 m
#   Textures/PR_Early_Water_Caustic.png          linear, periodic 2 m
# The materials, the phase-1 look profile and the test scene are made in Unity by
# ChessFight > Pawn Rush > Development > R502 Early Look Test (EarlyLookTestMenu.cs). Existing files are rewritten
# (they are generated); existing .meta files are kept so references survive.
import bpy, os, sys, shutil
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..")))
import mg_export as mg  # noqa: E402  (guid, write, folder, FBX_META, TEX_META, ROOT)

TEX = os.path.join(HERE, "Textures")
OUT = "Assets/Resources/PawnRushMiniGames/Early/R502"


def tex_meta(rel, kind):
    """kind: 'color' (sRGB), 'linear', 'normal'."""
    if os.path.exists(os.path.join(mg.ROOT, rel + ".meta")):
        return
    with open(mg.TEX_META, encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines[1] = "guid: " + mg.guid(rel)
    text = "\n".join(lines)
    if kind != "color":
        text = text.replace("sRGBTexture: 1", "sRGBTexture: 0")
    if kind == "normal":
        text = text.replace("textureType: 0", "textureType: 1")
    mg.write(rel + ".meta", text)


def copy_tex(src_name, dst_name, kind):
    rel = OUT + "/Textures/" + dst_name
    shutil.copyfile(os.path.join(TEX, src_name), os.path.join(mg.ROOT, rel))
    tex_meta(rel, kind)
    print("TEX", rel, kind)


def metallic_smoothness():
    img = bpy.data.images.load(os.path.join(TEX, "PR_Early_StoneBlock_Roughness.png"))
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    a = a.reshape(h, w, 4)
    out = np.zeros_like(a)
    out[:, :, 3] = 1. - a[:, :, 0]
    o = bpy.data.images.new("ms", w, h, alpha=True)
    o.colorspace_settings.name = "Non-Color"
    o.alpha_mode = "STRAIGHT"
    o.pixels.foreach_set(out.ravel())
    rel = OUT + "/Textures/PR_Early_StoneBlock_MetallicSmoothness.png"
    o.filepath_raw = os.path.join(mg.ROOT, rel)
    o.file_format = "PNG"
    o.save()
    tex_meta(rel, "linear")
    print("TEX", rel, "linear (A = smoothness)")


mg.folder(OUT + "/Textures")
copy_tex("PR_Early_StoneBlock_BaseColor.png", "PR_Early_StoneBlock_BaseColor.png", "color")
copy_tex("PR_Early_StoneBlock_Normal.png", "PR_Early_StoneBlock_Normal.png", "normal")
copy_tex("PR_Early_StoneBlock_AO.png", "PR_Early_StoneBlock_AO.png", "linear")
metallic_smoothness()
copy_tex("PR_Early_Water_Normal.png", "PR_Early_Water_Normal.png", "normal")
copy_tex("PR_Early_Water_Caustic.png", "PR_Early_Water_Caustic.png", "linear")

# the module FBX: only the game copy, smooth normals as baked
ob = bpy.data.objects["PR_Early_StoneBlock"]
bpy.ops.object.select_all(action="DESELECT")
ob.hide_set(False)
ob.select_set(True)
bpy.context.view_layer.objects.active = ob
fbx_rel = OUT + "/PR_Early_StoneBlock.fbx"
bpy.ops.export_scene.fbx(filepath=os.path.join(mg.ROOT, fbx_rel), use_selection=True, object_types={"MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                         bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="OFF", add_leaf_bones=False,
                         path_mode="STRIP", embed_textures=False)
if not os.path.exists(os.path.join(mg.ROOT, fbx_rel + ".meta")):
    with open(mg.FBX_META, encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines[1] = "guid: " + mg.guid(fbx_rel)
    mg.write(fbx_rel + ".meta", "\n".join(lines))
me = ob.data
me.calc_loop_triangles()
print("FBX", fbx_rel, "tris", len(me.loop_triangles))
