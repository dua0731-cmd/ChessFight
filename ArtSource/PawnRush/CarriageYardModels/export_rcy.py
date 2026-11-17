# Royal Carriage Yard models (R678): writes one Blender collection to Unity as an FBX.
#
#   In the live Blender (after rcy_kit.py):  exec(open(r".../export_rcy.py").read()); export("RCY Canal", "RcyCanal")
#
# Writes Assets/Resources/PawnRushCarriageYard/<name>.fbx (every mesh of the collection, modifiers applied) with the
# garden FBX's import settings (axis -Z forward / Y up: Unity hangs it under a pivot turned 180 degrees about y, so
# Blender x, y, z = Unity x, z, y) and a .meta with a deterministic GUID (as Tools/Generators/mkmeta.py), plus the
# folder's .meta. No materials are written: RoyalCarriageYard.Dressing.cs draws every slot in clay for now (the slot
# names - "RCY | walnut" ... - are kept for the colour pass).

import bpy, hashlib, os

RCY_ROOT = globals().get("RCY_ROOT") or os.environ.get("RCY_ROOT") or r"C:/Work/ChessFight-SeoungTest"   # as rcy_kit.py
RCY_OUT_REL = "Assets/Resources/PawnRushCarriageYard"
RCY_FBX_TEMPLATE = os.path.join(RCY_ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
RCY_FOLDER_META = ("folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n"
                   "  assetBundleVariant: \n")


def _guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def _write(rel, text):
    path = os.path.join(RCY_ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def export(collection_name, name):
    os.makedirs(os.path.join(RCY_ROOT, RCY_OUT_REL), exist_ok=True)
    if not os.path.exists(os.path.join(RCY_ROOT, RCY_OUT_REL + ".meta")):
        _write(RCY_OUT_REL + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (_guid(RCY_OUT_REL), RCY_FOLDER_META))
    col = bpy.data.collections[collection_name]
    meshes = [o for o in col.all_objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    total = 0
    for o in meshes:
        o.hide_set(False)
        o.hide_viewport = False
        o.select_set(True)
        total += sum(len(p.vertices) - 2 for p in o.data.polygons)
    # A collection hidden in the view layer cannot be selected, and the FBX came out empty (R680): stop instead.
    missed = [o.name for o in meshes if not o.select_get()]
    if missed:
        raise RuntimeError("export %s: not selectable (its collection hidden in the view layer?): %s" % (name, missed))
    bpy.context.view_layer.objects.active = meshes[0]
    rel = RCY_OUT_REL + "/" + name + ".fbx"
    bpy.ops.export_scene.fbx(filepath=os.path.join(RCY_ROOT, rel), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    if not os.path.exists(os.path.join(RCY_ROOT, rel + ".meta")):
        with open(RCY_FBX_TEMPLATE, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + _guid(rel)
        _write(rel + ".meta", "\n".join(lines))
    print("exported", rel, len(meshes), "objects", total, "tris")
    return rel
