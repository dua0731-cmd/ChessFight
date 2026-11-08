# Copies the G Royal Procession centre gate into Unity (R504), the same layout as the D (R505) and B (R503) gates:
#   Assets/Resources/PawnRushMiniGames/Late/G/G_CenterGate.fbx + the props G_*.fbx (MiniGameModels loads the gate)
#   Assets/Art/PawnRushMiniGames/Late/Textures/PR_Late_CenterGate_BaseColor/Normal.png
#   Assets/Resources/PawnRushMiniGames/Materials/PR_Late_CenterGate.mat
# .meta files are copied from the D gate's (same import settings) with a stable guid from the asset path.
# Re-running overwrites the FBX and textures; an existing .mat is left alone (it may be tuned in Unity).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_install_unity.py
import hashlib, os, re, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
EXPORT = os.path.join(HERE, "Export")
TEX = os.path.join(HERE, "Materials", "Textures")

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Late/G"
TEX_DIR = "Assets/Art/PawnRushMiniGames/Late/Textures"
MAT_DIR = "Assets/Resources/PawnRushMiniGames/Materials"
FBX_META = "Assets/Resources/PawnRushMiniGames/Early/D/D_CenterGate.fbx.meta"
BASE_META = "Assets/Art/PawnRushMiniGames/Early/Textures/PR_Early_CenterGate_BaseColor.png.meta"
NORMAL_META = "Assets/Art/PawnRushMiniGames/Early/Textures/PR_Early_CenterGate_Normal.png.meta"
MAT_TEMPLATE = MAT_DIR + "/PR_Early_CenterGate.mat"
BASE_GUID = "e0e48d248c6e1ecc97c0d5788c6b6b2c"     # the template's textures
NORMAL_GUID = "c40387daff0912d46f0d348904f17273"
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def p(rel):
    return os.path.join(ROOT, rel)


def read(rel):
    with open(p(rel), encoding="utf-8") as f:
        return f.read()


def write(rel, text):
    os.makedirs(os.path.dirname(p(rel)), exist_ok=True)
    with open(p(rel), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def folder(rel):
    parts = rel.split("/")
    for i in range(2, len(parts) + 1):
        sub = "/".join(parts[:i])
        os.makedirs(p(sub), exist_ok=True)
        if not os.path.exists(p(sub + ".meta")):
            write(sub + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(sub), FOLDER))


def meta_from(template, rel):
    text = re.sub(r"^guid: \w+", "guid: " + guid(rel), read(template), count=1, flags=re.M)
    write(rel + ".meta", text)
    return guid(rel)


def copy(src, rel, template):
    shutil.copyfile(src, p(rel))
    if not os.path.exists(p(rel + ".meta")):
        meta_from(template, rel)
    print("copied", rel)
    return guid(rel)


def material(name, base, normal, smooth):
    rel = MAT_DIR + "/" + name + ".mat"
    if os.path.exists(p(rel)):
        print("kept", rel)
        return
    t = read(MAT_TEMPLATE).replace("m_Name: PR_Early_CenterGate", "m_Name: " + name)
    t = t.replace(BASE_GUID, base).replace(NORMAL_GUID, normal)
    t = t.replace("- _Smoothness: 0.15", "- _Smoothness: %g" % smooth)
    write(rel, t)
    meta_from(MAT_DIR + "/PR_Early_CenterGate.mat", rel)
    print("made", rel)


def main():
    folder(FBX_DIR)
    folder(TEX_DIR)
    copy(os.path.join(EXPORT, "G_CenterGate.fbx"), FBX_DIR + "/G_CenterGate.fbx", FBX_META)
    name = "PR_Late_CenterGate"
    base = copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META)
    normal = copy(os.path.join(TEX, name + "_Normal.png"), TEX_DIR + "/" + name + "_Normal.png", NORMAL_META)
    material(name, base, normal, .5)      # phase 3 is the cleanest, polished marble
    # the props cut by g_props_v1.py (Export/G/*.fbx), one atlas material per source model
    for fbx in sorted(os.listdir(os.path.join(EXPORT, "G"))):
        if fbx.endswith(".fbx"):
            copy(os.path.join(EXPORT, "G", fbx), FBX_DIR + "/" + fbx, FBX_META)
    for key in ("Pieces", "Decor", "Fixes", "Modules", "Skylight", "Walls", "Gate", "Stairs"):
        name = "PR_Late_G_" + key
        base = copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META)
        normal = copy(os.path.join(TEX, name + "_Normal.png"), TEX_DIR + "/" + name + "_Normal.png", NORMAL_META)
        material(name, base, normal, .4)
        if key == "Gate":   # the team gate door's painted wood: the same atlas, tinted with the team colour in the game
            material("PR_Late_G_GatePaint", base, normal, .3)
    # the phase-3 swatch set (Materials/r504_material_swatches.py) for code-built surfaces: tiling is in the mesh UVs
    for name, smooth in (("PR_Late_MarbleCream", .6), ("PR_Late_MarbleNavy", .6), ("PR_Late_Brass", .5),
                         ("PR_Late_Maple", .35), ("PR_Late_Walnut", .35), ("PR_Late_Ermine", .05), ("PR_Late_Velvet", .1)):
        base = copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META)
        normal = copy(os.path.join(TEX, name + "_Normal.png"), TEX_DIR + "/" + name + "_Normal.png", NORMAL_META)
        material(name, base, normal, smooth)
    # ermine and velvet are 1 m swatches; the floor code's UVs are metres / 2, so they repeat twice per UV unit
    for name in ("PR_Late_Ermine", "PR_Late_Velvet"):
        rel = MAT_DIR + "/" + name + ".mat"
        t = read(rel)
        for slot in ("_BaseMap", "_BumpMap", "_MainTex"):
            t = re.sub(r"(- " + slot + r":\n        m_Texture: [^\n]*\n        m_Scale: )\{x: 1, y: 1\}", r"\g<1>{x: 2, y: 2}", t)
        write(rel, t)
    # the royal banner (g_banner_tex_v1.py): crown and V hem on velvet, alpha-clipped (the hem's corners are cut away)
    name = "PR_Late_G_RoyalBanner"
    base = copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META)
    material(name, base, guid(TEX_DIR + "/PR_Late_Velvet_Normal.png"), .1)
    rel = MAT_DIR + "/" + name + ".mat"
    t = read(rel)
    if "_ALPHATEST_ON" not in t:
        t = t.replace("  m_ValidKeywords:\n  - _NORMALMAP\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n  - _NORMALMAP\n")
        t = t.replace("- _AlphaClip: 0", "- _AlphaClip: 1").replace("m_CustomRenderQueue: -1", "m_CustomRenderQueue: 2450")
        t = t.replace("RenderType: Opaque", "RenderType: TransparentCutout")
        write(rel, t)
    # the functional gold (grips, collars, stops: PAWN_RUSH_ART_PHASES #E2B23C, emission 0.6), a plain material for the
    # Blender-modelled props (g_props_blender_v1.py): the brass material without its texture, gold coloured, glowing
    rel = MAT_DIR + "/PR_Late_GoldFunctional.mat"
    if not os.path.exists(p(rel)):
        t = read(MAT_DIR + "/PR_Late_Brass.mat").replace("m_Name: PR_Late_Brass", "m_Name: PR_Late_GoldFunctional")
        t = re.sub(r"m_Texture: \{fileID: 2800000, guid: \w+, type: 3\}", "m_Texture: {fileID: 0}", t)
        t = t.replace("  m_ValidKeywords:\n  - _NORMALMAP\n", "  m_ValidKeywords:\n  - _EMISSION\n")
        t = t.replace("_BaseColor: {r: 1, g: 1, b: 1, a: 1}", "_BaseColor: {r: 0.886, g: 0.698, b: 0.235, a: 1}")
        t = t.replace("_Color: {r: 1, g: 1, b: 1, a: 1}", "_Color: {r: 0.886, g: 0.698, b: 0.235, a: 1}")
        t = t.replace("_EmissionColor: {r: 0, g: 0, b: 0, a: 1}", "_EmissionColor: {r: 0.53, g: 0.42, b: 0.14, a: 1}")
        t = t.replace("m_LightmapFlags: 4", "m_LightmapFlags: 2")
        write(rel, t)
        meta_from(MAT_DIR + "/PR_Early_CenterGate.mat", rel)
        print("made", rel)
    # matte brass is a metal
    rel = MAT_DIR + "/PR_Late_Brass.mat"
    t = read(rel)
    if "- _Metallic: 0.00" in t:
        write(rel, t.replace("- _Metallic: 0.00", "- _Metallic: 0.8"))


main()
