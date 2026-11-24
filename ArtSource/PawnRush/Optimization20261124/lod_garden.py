# R816 (user 11-24 "정원 LOD 진행"): a far LOD for the Royal Water Garden's big still models, the way R809 gave the
# carriage yard one (ArtSource/PawnRush/CarriageYardModels/lod_rcy.py): one level, half the triangles, nothing culled.
# The garden is open like the yard; its models were cut into ~20 m pieces on 11-13 (Optimization20261113/LOD), so
# each piece switches on its own distance.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup
#       --python ArtSource/PawnRush/Optimization20261124/lod_garden.py -- <repository root> [model ...]
#
# For each model: imports Assets/Resources/PawnRushGardenConcept/<model>.fbx as Unity has it, decimates every mesh
# (collapse, RATIO of its triangles, triangulated), shades it by its angles (edges sharper than SHARP_DEGREES stay
# hard), and writes Assets/Resources/PawnRushGardenConcept/LOD/<model>_LOD1.fbx with lod_rcy.py's settings - the same
# object names and material slot names - and a .meta copied from the model's own (a deterministic GUID, Unity's own
# Mesh LOD off). The written file is read back and each object's box compared with the original's.
# RoyalWaterGardenBuilder.Concept.cs (AttachLods) hangs each LOD1 piece under its piece in a LODGroup.
# Not here: the trees and flowers (11-13: thinning their leaves showed), the water and falls (shader sheets), the boats
# (the water shader draws their wake from their shape, 11-13 §11-1), the birds and the finish.

import bpy, hashlib, math, os, sys

MODELS = ["GardenPlaza", "GardenCanal", "GardenLockDyke", "GardenGate", "GardenWalls", "GardenPlatforms",
          "GardenWatergate"]
RATIO = 0.5
SHARP_DEGREES = 30.0
FOLDER = "Assets/Resources/PawnRushGardenConcept"
LOD_FOLDER = FOLDER + "/LOD"

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else os.getcwd()
if len(args) > 1:
    MODELS = args[1:]


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def boxes():
    out = {}
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        if not pts:
            continue
        lo = [min(p[i] for p in pts) for i in range(3)]
        hi = [max(p[i] for p in pts) for i in range(3)]
        out[o.name] = (lo, hi)
    return out


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)


report = []
os.makedirs(os.path.join(ROOT, LOD_FOLDER), exist_ok=True)
if not os.path.exists(os.path.join(ROOT, LOD_FOLDER + ".meta")):
    write(LOD_FOLDER + ".meta", "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
          "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(LOD_FOLDER))
for name in MODELS:
    src = os.path.join(ROOT, FOLDER, name + ".fbx")
    load(src)
    original = boxes()
    before = after = 0
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        t0 = tris(o)
        before += t0
        if o.data.users > 1:
            o.data = o.data.copy()
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        mod = o.modifiers.new("LOD1", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = RATIO
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if o.data.has_custom_normals:
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(SHARP_DEGREES))
        after += tris(o)
    rel = LOD_FOLDER + "/" + name + "_LOD1.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, rel), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    with open(src + ".meta", encoding="utf-8") as f:
        lines = f.read().replace("\r\n", "\n").split("\n")
    lines[1] = "guid: " + guid(rel)
    lines = [l.replace("generateMeshLods: 1", "generateMeshLods: 0") for l in lines]
    write(rel + ".meta", "\n".join(lines))
    load(os.path.join(ROOT, rel))
    back = boxes()
    worst = 0.0
    missing = [n for n in original if n not in back]
    for n, (lo, hi) in original.items():
        if n in back:
            b = back[n]
            worst = max(worst, max(abs(lo[i] - b[0][i]) for i in range(3)), max(abs(hi[i] - b[1][i]) for i in range(3)))
    report.append("%s: %d objects, %d -> %d triangles (%.0f%%), box drift %.3f m%s" % (
        name, len(original), before, after, 100.0 * after / max(1, before), worst,
        (", MISSING " + ", ".join(missing)) if missing else ""))
    print(report[-1], flush=True)

out = os.path.join(ROOT, "ArtSource/PawnRush/Optimization20261124/lod_garden_report.txt")
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("lod_garden.py (R816) ratio %.2f, sharp %.0f deg\n" % (RATIO, SHARP_DEGREES) + "\n".join(report) + "\n")
