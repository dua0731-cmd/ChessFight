# R809 (the 11-23 build test: the carriage yard drew at 52 fps on an RTX 3060 where the grand stair hall drew at 77 - the
# yard is open, nearly all of it is in view at once; Docs/Network/BUILD_TEST_2026-11-23.md section 12): a far LOD for
# the yard's big still models. The user: "마차뜰 LOD 부터 해줘 너무 많이는 줄이지 말고" - one level, half the triangles,
# nothing culled.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup
#       --python ArtSource/PawnRush/CarriageYardModels/lod_rcy.py -- <repository root> [model ...]
#
# For each model: imports Assets/Resources/PawnRushCarriageYard/<model>.fbx as Unity has it, decimates every mesh
# (collapse, RATIO of its triangles, triangulated), gives the result the shading of its angles (edges sharper than
# SHARP_DEGREES stay hard: the kit's stones and planks), and writes Assets/Resources/PawnRushCarriageYard/LOD/
# <model>_LOD1.fbx with export_rcy.py's settings - the same object names, the same material slot names - and a .meta
# copied from the model's own (a deterministic GUID). The exported file is read back and each object's box compared with
# the original's (the round trip must not move anything). RoyalCarriageYard.Dressing.cs (AttachLods) hangs the LOD1
# next to each piece and puts both in a LODGroup.

import bpy, hashlib, math, os, sys

MODELS = ["RcyEntrance", "RcyCanal", "RcySorting", "RcyPlatform", "RcyFinishCourt", "RcyScenery", "RcySceneryOwn",
          "RcyCastleOuterGate", "RcyCastleFinalGate", "RcyCastleSectionEnd"]
RATIO = 0.5
SHARP_DEGREES = 30.0
FOLDER = "Assets/Resources/PawnRushCarriageYard"
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
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        mod = o.modifiers.new("LOD1", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = RATIO
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # The decimated corners lose the imported custom normals: shade by angle instead.
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
        lines = f.read().split("\n")
    lines[1] = "guid: " + guid(rel)
    write(rel + ".meta", "\n".join(lines))
    # The round trip: the written file read back, every object's box against the original's.
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
    print(report[-1])

out = os.path.join(ROOT, "ArtSource/PawnRush/CarriageYardModels/qa/lod_rcy_report.txt")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("lod_rcy.py (R809) ratio %.2f, sharp %.0f deg\n" % (RATIO, SHARP_DEGREES) + "\n".join(report) + "\n")
