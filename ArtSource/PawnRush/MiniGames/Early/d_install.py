# D portcullis court (R505): copies the exported FBX + textures into Assets and writes their .meta and URP Lit materials.
# Plain Python (no Blender needed). On this PC `python` is a Store stub, so run it with Blender's interpreter:
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I d_install.py
#
# Models  -> Assets/Resources/PawnRushMiniGames/Early/D/<name>.fbx   (MiniGameModels loads them by name)
# Mats    -> Assets/Resources/PawnRushMiniGames/Materials/<name>.mat (MiniGameModels swaps the FBX's imported material)
# Texture -> Assets/Art/PawnRushMiniGames/Early/Textures/<name>.png  (referenced by the materials only)
# GUIDs are md5("chessfight:" + path), as Tools/Generators/mkmeta.py. Files are overwritten; a .meta is kept if present.
import hashlib, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
MODELS = "Assets/Resources/PawnRushMiniGames/Early/D"
MATS = "Assets/Resources/PawnRushMiniGames/Materials"
TEXS = "Assets/Art/PawnRushMiniGames/Early/Textures"
FBX_META = "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta"
BASE_META = "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_BannerNavy_BaseColor.png.meta"
NORMAL_META = "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_Iron_Normal.png.meta"
MAT_TEMPLATE = "Assets/Art/PawnRushMap/Endgame/Materials/EG_Iron.mat"
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

# material name -> (texture stem, smoothness = 1 - Blender roughness)
MATERIALS = {"PR_Early_Capstan": ("PR_Early_Capstan", .28), "PR_Early_CenterGate": ("PR_Early_CenterGate", .15),
             "PR_Early_TeamGate": ("PR_Early_TeamGate", .20),
             # d_props_v1.py: the two Tripo atlases of the remaining parts
             "PR_Early_TripoStructure": ("PR_Early_TripoStructure", .15), "PR_Early_TripoProps": ("PR_Early_TripoProps", .2),
             # the wall module's core (d_props_v1.py fill_wall): R502 tileable stone brick tinted to the wall's stone
             "PR_Early_WallCore": ("PR_Early_StoneBrick", .1, (.91, .90, .87)),
             # the courtyard floor (Materials/r505_paving.py, 2 m a tile; MiniGameModels.Paving sets the repeat)
             "PR_Early_Paving": ("PR_Early_Paving", .08),
             # d_cypress_v1.py: the painted leaf ramp (u = light, v = hue row; the last row is bark)
             "PR_Early_Cypress": ("PR_Early_Cypress", .12),
             # d_tower_v1.py: the Tripo corner tower, tinted from its paler stone (.77 .69 .56) to the wall's (.72 .60 .47)
             "PR_Early_CornerTower": ("PR_Early_CornerTower", .12, (.93, .87, .84))}
# flat-colour materials of the parts d_props_v1.py built (sRGB colour, smoothness, metallic)
FLAT = {"PR_Early_DarkIron": ((.39, .36, .32), .45, .6), "PR_Early_Soil": ((.35, .27, .21), .05, 0),
        "PR_Early_Leaf": ((.35, .56, .29), .4, 0), "PR_Early_Petal": ((.94, .93, .91), .4, 0),
        "PR_Early_FlowerHeart": ((.91, .79, .35), .4, 0)}
FBX = ["D_Capstan_Static", "D_Capstan_Bars", "D_Capstan_Bar", "D_CenterGate", "D_TeamArch", "D_Portcullis",
       "D_PulleyFrame", "D_PulleyWheel", "D_Wall", "D_GatePost", "D_Lantern", "D_BarRack", "D_ChainPile",
       "D_ChainStraight", "D_HelmetBench", "D_FlowerPot", "D_Cypress", "D_CornerTower"]


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
    """Existing .meta wins (keeps the GUID Unity may already use); otherwise the template with a new GUID."""
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
    ref = lambda g: tex(g) if g else "{fileID: 0}"           # no texture: a flat-colour material
    text = text.replace(tex("c3ab225f3cefaf4438aac342c2c78091") + "\n        m_Scale: {x: 2, y: 2}",
                        ref(base_guid) + "\n        m_Scale: {x: 1, y: 1}")
    text = text.replace(tex("82c0c28f6ba245f4b9f4beabddb5ef94"), ref(normal_guid))
    text = text.replace(tex("2c4d268c4cbe99c4eaf75ccc5003f83a"), "{fileID: 0}")
    text = text.replace(tex("e092c39bbcd81d846b88a9cd58903d1c"), "{fileID: 0}")
    text = text.replace("    - _Smoothness: 1\n", "    - _Smoothness: %.2f\n" % smooth)
    text = text.replace("    - _Metallic: 0\n", "    - _Metallic: %.2f\n" % metallic)
    text = text.replace("    - _GlossyReflections: 0\n", "    - _GlossyReflections: 1\n")
    if tint:
        rgb = "{r: %.2f, g: %.2f, b: %.2f, a: 1}" % tint
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
    for name, (stem, smooth, *tint) in MATERIALS.items():
        b = copy(os.path.join(HERE, "Materials", "Textures", stem + "_BaseColor.png"), TEXS + "/" + stem + "_BaseColor.png", BASE_META)
        n = copy(os.path.join(HERE, "Materials", "Textures", stem + "_Normal.png"), TEXS + "/" + stem + "_Normal.png", NORMAL_META)
        material(name, b, n, smooth, tint[0] if tint else None)
    for name, (rgb, smooth, metal) in FLAT.items():
        material(name, None, None, smooth, rgb, metal)


main()
