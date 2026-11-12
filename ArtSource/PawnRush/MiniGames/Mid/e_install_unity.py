# Copies the E Royal Library models into Unity (R507), the same layout as B (b_install_unity.py):
#   Assets/Resources/PawnRushMiniGames/Mid/E/E_Tower_9.fbx, E_Bell.fbx, E_ShelfDoor.fbx, E_DoorArch.fbx
#   Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_LibraryTowerKit/PropKit_BaseColor/Normal.png
#   Assets/Resources/PawnRushMiniGames/Materials/PR_Mid_LibraryTowerKit/PropKit/BellRung/MatteBrass.mat
# The ledge decks use B's PR_Mid_WalnutFloor (already installed by b_install_unity.py).
# .meta files are copied from the D gate's (same import settings) with a stable guid from the asset path.
# Re-running overwrites the FBX and textures; an existing .mat is left alone (it may be tuned in Unity).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I e_install_unity.py
import hashlib, os, re, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
EXPORT = os.path.join(HERE, "Export")
TEX = os.path.join(HERE, "Materials", "Textures")

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Mid/E"
TEX_DIR = "Assets/Art/PawnRushMiniGames/Mid/Textures"
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


def material(name, base=None, normal=None, color=None, emission=None, smooth=None, metallic=None):
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
    if emission:
        t = t.replace("_EmissionColor: {r: 0, g: 0, b: 0, a: 1}", "_EmissionColor: {r: %g, g: %g, b: %g, a: 1}" % emission)
        if "  m_ValidKeywords: []\n" in t:
            t = t.replace("  m_ValidKeywords: []\n", "  m_ValidKeywords:\n  - _EMISSION\n")
        else:
            t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _EMISSION\n", 1)
        t = t.replace("m_LightmapFlags: 4", "m_LightmapFlags: 2")
    if smooth is not None:
        t = t.replace("- _Smoothness: 0.15", "- _Smoothness: %g" % smooth)
    if metallic is not None:
        t = t.replace("- _Metallic: 0.00", "- _Metallic: %g" % metallic)
    write(rel, t)
    meta_from(MAT_DIR + "/PR_Early_CenterGate.mat", rel)
    print("made", rel)


def main():
    folder(FBX_DIR)
    folder(TEX_DIR)
    # E_HallEnv: the room's walls, gate, arches, beams, fill and ceiling (e_hall_v1.py)
    # E_LightsEnv: B's chandelier and sconce bodies round the room, SOCKET_ empties for code flames (e_lights_v1.py;
    # material PR_Mid_HallKit is B's, b_install_unity.py)
    # E_PropsEnv: the props modelled in Blender (e_props_v1.py), placed off the pawns' ways
    # R543: E_Tower_9 (one tower, hung on each of the game's towers) replaced E_StationEnv (six towers in one mesh)
    for name in ("E_Tower_9", "E_Bell", "E_ShelfDoor", "E_DoorArch", "E_HallEnv", "E_LightsEnv", "E_PropsEnv"):
        copy(os.path.join(EXPORT, name + ".fbx"), FBX_DIR + "/" + name + ".fbx", FBX_META)
    g = {}
    for name in ("PR_Mid_LibraryTowerKit", "PR_Mid_LibraryPropKit", "PR_Mid_MatteBrass",
                 "PR_Mid_LibraryWallKit", "PR_Mid_LibraryGateKit"):
        g[name] = (copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META),
                   copy(os.path.join(TEX, name + "_Normal.png"), TEX_DIR + "/" + name + "_Normal.png", NORMAL_META))
    material("PR_Mid_LibraryTowerKit", *g["PR_Mid_LibraryTowerKit"], smooth=.2)
    material("PR_Mid_LibraryPropKit", *g["PR_Mid_LibraryPropKit"], smooth=.3)
    material("PR_Mid_LibraryWallKit", *g["PR_Mid_LibraryWallKit"], smooth=.2)
    material("PR_Mid_LibraryGateKit", *g["PR_Mid_LibraryGateKit"], smooth=.2)
    # E_HallEnv's fill and ceiling use B's PR_Mid_AshlarFill and PR_Mid_WalnutFloor (b_install_unity.py)
    # e_props_v1.py: the seamless walnut grain and the globe map (base colour only), and the plain colours (Blender's
    # linear values written as sRGB, as Unity's material colour)
    for name in ("PR_Mid_PropWalnut", "PR_Mid_PropGlobe"):
        base = copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META)
        material(name, base, None, smooth=.35)
    sys.path.insert(0, HERE)
    colors = {}
    with open(os.path.join(HERE, "e_props_v1.py"), encoding="utf-8") as f:
        src = f.read()
    for m in re.finditer(r'"(PR_Mid_Prop\w+)": \(([\d.]+), ([\d.]+), ([\d.]+)\)', src):
        colors[m.group(1)] = tuple(round(float(m.group(k)) ** (1 / 2.2), 3) for k in (2, 3, 4))
    for name, c in colors.items():
        material(name, color=c, smooth=.15 if ("Cloth" in name or "Page" in name) else .35,
                 metallic=.6 if "Iron" in name else None)
    # the tower tops' built rail (e_library_v1.py rail()): the phase-2 matte brass swatch (R503), 1 m tile
    material("PR_Mid_MatteBrass", *g["PR_Mid_MatteBrass"], smooth=.55, metallic=.8)
    # A rung bell (MiniGameBellLook): the same bell, warmed to the functional gold (#E2B23C) and glowing 0.6
    # (R499 art standard: functional gold only where the game's state shows, mid-game emission 0.6).
    material("PR_Mid_BellRung", *g["PR_Mid_LibraryPropKit"], color=(1., .86, .5), emission=(.53, .42, .14), smooth=.55)


main()
