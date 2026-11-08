# C · 해자 도개교 v2 (R502): copies c_drawbridge_v2_export.py's FBX + textures into Assets and writes their .meta and
# URP Lit materials, as d_install.py does for D (same templates, same GUID rule). Plain Python:
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I c_install.py
#
# Models  -> Assets/Resources/PawnRushMiniGames/Early/C/<name>.fbx   (MiniGameModels.Drawbridge.cs hangs them)
# Mats    -> Assets/Resources/PawnRushMiniGames/Materials/<name>.mat (swapped for the FBX's imported materials by name)
# Texture -> Assets/Art/PawnRushMiniGames/Early/Textures/<name>.png
# Materials another set already wrote (PR_Early_DarkIron, PR_Early_Leaf, PR_Early_Petal: D) are reused, not rewritten.
import hashlib, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
MODELS = "Assets/Resources/PawnRushMiniGames/Early/C"
MATS = "Assets/Resources/PawnRushMiniGames/Materials"
TEXS = "Assets/Art/PawnRushMiniGames/Early/Textures"
FBX_META = "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta"
BASE_META = "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_BannerNavy_BaseColor.png.meta"
NORMAL_META = "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_Iron_Normal.png.meta"
MAT_TEMPLATE = "Assets/Art/PawnRushMap/Endgame/Materials/EG_Iron.mat"
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

FBX = ["C_Drawbridge_EnvV2", "C_Drawbridge_DeckV2", "C_Drawbridge_DrumV2", "C_ChainLink", "C_RopeSegment", "C_KnotA", "C_KnotC"]
# material -> (texture stem in Materials/Textures, smoothness, has a normal map)
TEXTURED = {"PR_Early_C_Gate": ("PR_Early_C_Gate", .15, True), "PR_Early_C_Tower": ("PR_Early_C_Tower", .2, True),
            "PR_Early_C_Winch": ("PR_Early_C_Winch", .25, True), "PR_Early_C_Railing": ("PR_Early_C_Railing", .15, True),
            "PR_Early_C_Kit": ("PR_Early_C_Kit", .2, True), "PR_Early_StoneBlock": ("PR_Early_StoneBlock", .25, True),
            "PR_Early_C_Floor": ("PR_Early_C_Floor", .25, True),
            "PR_Early_C_Wood": ("PR_Early_WoodPlank", .25, True), "PR_Early_C_TowerStone": ("PR_Early_TowerStone_Surface", .3, False)}
# flat colours (sRGB, smoothness, metallic); the asset gold is the gate texture's own (c_drawbridge_v2.asset_gold)
FLAT = {"PR_Early_C_Gold": ((.686, .451, .224), .65, .85), "PR_Early_C_Tick": ((.886, .698, .235), .55, .3),
        "PR_Early_C_Cypress": ((.235, .416, .204), .2, 0), "PR_Early_C_Rope": ((.79, .65, .48), .1, 0),
        "PR_Early_C_Trunk": ((.35, .25, .19), .1, 0), "PR_Early_C_Buoy": ((.94, .92, .89), .35, 0),
        "PR_Early_C_Leather": ((.30, .16, .08), .35, 0)}
REUSED = ["PR_Early_DarkIron", "PR_Early_Leaf", "PR_Early_Petal"]


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def path(rel):
    return os.path.join(ROOT, rel)


def read(rel):
    with open(path(rel), encoding="utf-8", newline="") as f:
        return f.read()


def write(rel, text):
    os.makedirs(os.path.dirname(path(rel)), exist_ok=True)
    with open(path(rel), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def folder(rel):
    parts = rel.split("/")
    for i in range(2, len(parts) + 1):
        sub = "/".join(parts[:i])
        os.makedirs(path(sub), exist_ok=True)
        if not os.path.exists(path(sub + ".meta")):
            write(sub + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(sub), FOLDER))


def meta_from(template, rel):
    if os.path.exists(path(rel + ".meta")):
        return read(rel + ".meta").replace("\r\n", "\n").split("\n")[1].split(":")[1].strip()
    lines = read(template).replace("\r\n", "\n").split("\n")
    assert lines[1].startswith("guid:"), template
    lines[1] = "guid: " + guid(rel)
    write(rel + ".meta", "\n".join(lines))
    return guid(rel)


def copy(src, rel, template):
    folder(os.path.dirname(rel).replace("\\", "/"))
    shutil.copyfile(src, path(rel))
    g = meta_from(template, rel)
    print("copied", rel, g)
    return g


def material(name, base_guid, normal_guid, smooth, tint=None, metallic=0.0):
    text = read(MAT_TEMPLATE).replace("\r\n", "\n")
    text = text.replace("m_Name: EG_Iron", "m_Name: " + name)
    text = text.replace("  m_ValidKeywords:\n  - _METALLICSPECGLOSSMAP\n  - _NORMALMAP\n  - _OCCLUSIONMAP\n",
                        "  m_ValidKeywords:\n  - _NORMALMAP\n" if normal_guid else "  m_ValidKeywords: []\n")
    tex = lambda g: "{fileID: 2800000, guid: %s, type: 3}" % g
    ref = lambda g: tex(g) if g else "{fileID: 0}"
    text = text.replace(tex("c3ab225f3cefaf4438aac342c2c78091") + "\n        m_Scale: {x: 2, y: 2}",
                        ref(base_guid) + "\n        m_Scale: {x: 1, y: 1}")
    text = text.replace(tex("82c0c28f6ba245f4b9f4beabddb5ef94"), ref(normal_guid))
    text = text.replace(tex("2c4d268c4cbe99c4eaf75ccc5003f83a"), "{fileID: 0}")
    text = text.replace(tex("e092c39bbcd81d846b88a9cd58903d1c"), "{fileID: 0}")
    text = text.replace("    - _Smoothness: 1\n", "    - _Smoothness: %.2f\n" % smooth)
    text = text.replace("    - _Metallic: 0\n", "    - _Metallic: %.2f\n" % metallic)
    text = text.replace("    - _GlossyReflections: 0\n", "    - _GlossyReflections: 1\n")
    if tint:
        rgb = "{r: %.3f, g: %.3f, b: %.3f, a: 1}" % tint
        text = text.replace("    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}", "    - _BaseColor: " + rgb)
        text = text.replace("    - _Color: {r: 1, g: 1, b: 1, a: 1}", "    - _Color: " + rgb)
    for old in ("c3ab225f", "82c0c28f", "2c4d268c", "e092c39b", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP"):
        assert old not in text, (name, old)
    rel = MATS + "/" + name + ".mat"
    folder(MATS)
    write(rel, text)
    if not os.path.exists(path(rel + ".meta")):
        write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(rel))
    print("material", rel)


def main():
    for name in FBX:
        copy(os.path.join(HERE, "Export", name + ".fbx"), MODELS + "/" + name + ".fbx", FBX_META)
    for name, (stem, smooth, has_normal) in TEXTURED.items():
        b = copy(os.path.join(HERE, "Materials", "Textures", stem + "_BaseColor.png" if not stem.endswith("_Surface") else stem + ".png"),
                 TEXS + "/" + stem + "_BaseColor.png", BASE_META)
        n = copy(os.path.join(HERE, "Materials", "Textures", stem + "_Normal.png"), TEXS + "/" + stem + "_Normal.png", NORMAL_META) if has_normal else None
        material(name, b, n, smooth)
    for name, (rgb, smooth, metal) in FLAT.items():
        material(name, None, None, smooth, rgb, metal)
    for name in REUSED:
        assert os.path.exists(path(MATS + "/" + name + ".mat")), "missing shared material " + name


main()
