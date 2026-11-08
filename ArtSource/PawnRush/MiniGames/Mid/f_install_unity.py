# Copies the F Clockwork Ballroom models into Unity (R508), the same layout as E (e_install_unity.py):
#   Assets/Resources/PawnRushMiniGames/Mid/F/F_*.fbx
#   Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_FramesKit / PropsKit / Parquet / PlumVelvet / BallroomMarble _BaseColor/_Normal.png
#   Assets/Resources/PawnRushMiniGames/Materials/<the materials below>.mat
# F_HallEnv (e_hall_v1.py --ballroom) uses E's and B's installed materials (LibraryWallKit, LibraryGateKit, AshlarFill,
# WalnutFloor, CreamFloor, SlateFloor, CheckerMarble); F_Bench B's WalnutFloor and MatteBrass.
# .meta files are copied from the D gate's (same import settings) with a stable guid from the asset path.
# Re-running overwrites the FBX and textures; an existing .mat is left alone (it may be tuned in Unity).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I f_install_unity.py
import hashlib, os, re, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
EXPORT = os.path.join(HERE, "Export")
TEX = os.path.join(HERE, "Materials", "Textures")

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Mid/F"
TEX_DIR = "Assets/Art/PawnRushMiniGames/Mid/Textures"
MAT_DIR = "Assets/Resources/PawnRushMiniGames/Materials"
FBX_META = "Assets/Resources/PawnRushMiniGames/Early/D/D_CenterGate.fbx.meta"
BASE_META = "Assets/Art/PawnRushMiniGames/Early/Textures/PR_Early_CenterGate_BaseColor.png.meta"
NORMAL_META = "Assets/Art/PawnRushMiniGames/Early/Textures/PR_Early_CenterGate_Normal.png.meta"
MAT_TEMPLATE = MAT_DIR + "/PR_Early_CenterGate.mat"
BASE_GUID = "e0e48d248c6e1ecc97c0d5788c6b6b2c"     # the template's textures
NORMAL_GUID = "c40387daff0912d46f0d348904f17273"
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

MODELS = ["F_HallEnv", "F_Bench", "F_DancerGown", "F_DancerTailcoat", "F_WindKey", "F_MirrorDoor", "F_WallMirror",
          "F_Spotlight", "F_MusicStand", "F_CoatStand", "F_DrinksTable", "F_Topiary", "F_Harp"]


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


def meta_guid(rel):
    return re.search(r"^guid: (\w+)", read(rel + ".meta"), re.M).group(1)


def copy(src, rel, template):
    shutil.copyfile(src, p(rel))
    if not os.path.exists(p(rel + ".meta")):
        meta_from(template, rel)
    print("copied", rel)
    return meta_guid(rel)


def material(name, base=None, normal=None, color=None, smooth=None, metallic=None, tile=None):
    rel = MAT_DIR + "/" + name + ".mat"
    if os.path.exists(p(rel)):
        print("kept", rel)
        return
    t = read(MAT_TEMPLATE).replace("m_Name: PR_Early_CenterGate", "m_Name: " + name)
    t = t.replace(BASE_GUID, base) if base else t.replace("{fileID: 2800000, guid: %s, type: 3}" % BASE_GUID, "{fileID: 0}")
    if normal:
        t = t.replace(NORMAL_GUID, normal)
    else:
        t = t.replace("{fileID: 2800000, guid: %s, type: 3}" % NORMAL_GUID, "{fileID: 0}")
        t = t.replace("  m_ValidKeywords:\n  - _NORMALMAP\n", "  m_ValidKeywords: []\n")
    if color:
        t = t.replace("_BaseColor: {r: 1, g: 1, b: 1, a: 1}", "_BaseColor: {r: %g, g: %g, b: %g, a: 1}" % color)
        t = t.replace("_Color: {r: 1, g: 1, b: 1, a: 1}", "_Color: {r: %g, g: %g, b: %g, a: 1}" % color)
    if smooth is not None:
        t = t.replace("- _Smoothness: 0.15", "- _Smoothness: %g" % smooth)
    if metallic is not None:
        t = t.replace("- _Metallic: 0.00", "- _Metallic: %g" % metallic)
    if tile:
        t = t.replace("m_Scale: {x: 1, y: 1}", "m_Scale: {x: %g, y: %g}" % (tile, tile))
    write(rel, t)
    meta_from(MAT_DIR + "/PR_Early_CenterGate.mat", rel)
    print("made", rel)


def srgb(lin):
    return tuple(round(c ** (1 / 2.2), 3) for c in lin)


def main():
    folder(FBX_DIR)
    folder(TEX_DIR)
    for name in MODELS:
        copy(os.path.join(EXPORT, name + ".fbx"), FBX_DIR + "/" + name + ".fbx", FBX_META)
    g = {}
    for name in ("PR_Mid_FramesKit", "PR_Mid_PropsKit", "PR_Mid_Parquet", "PR_Mid_PlumVelvet", "PR_Mid_BallroomMarble"):
        g[name] = (copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META),
                   copy(os.path.join(TEX, name + "_Normal.png"), TEX_DIR + "/" + name + "_Normal.png", NORMAL_META))
    # the user's Tripo sets (f_props_v1.py): mirror frames, spotlight, props
    material("PR_Mid_FramesKit", *g["PR_Mid_FramesKit"], smooth=.3)
    material("PR_Mid_PropsKit", *g["PR_Mid_PropsKit"], smooth=.3)
    # R508 swatches: one lane square = one parquet tile (the game's square box, UV 0..1); velvet 0.6 m (bench UVs
    # are already in tiles); the ballroom's own cream marble
    material("PR_Mid_Parquet", *g["PR_Mid_Parquet"], smooth=.5)
    material("PR_Mid_PlumVelvet", *g["PR_Mid_PlumVelvet"], smooth=.12)
    material("PR_Mid_BallroomMarble", *g["PR_Mid_BallroomMarble"], smooth=.65)
    # the mirror face (MIRROR_Surface): no picture of its own; MiniGameMirrorProbe gives the room's reflection
    material("PR_Mid_Mirror", color=(.86, .88, .9), smooth=.96, metallic=1.)
    # f_dancers_v1.py: porcelain body, white costume cloth (tinted per station in code to the other team's colour),
    # cream lace trim, cheek blush. Cloth uses B's canvas normal, 5x (lace 8x) as in the Blender preview.
    canvas = meta_guid(TEX_DIR + "/PR_Mid_Canvas_Normal.png")
    material("F_DancerBody", color=srgb((.80, .70, .56)), smooth=.68)
    material("F_DancerCostume", None, canvas, color=srgb((.86, .86, .84)), smooth=.15, tile=5)
    material("F_DancerTrim", None, canvas, color=srgb((.86, .80, .66)), smooth=.25, tile=8)
    material("F_DancerBlush", color=srgb((.82, .36, .33)), smooth=.45)


main()
