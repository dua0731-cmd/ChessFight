# Copies the B East Wing models into Unity (R503), the same layout as the D centre gate (R505):
#   Assets/Resources/PawnRushMiniGames/Mid/B/B_CenterGate.fbx, B_WallArch.fbx       (MiniGameModels loads these)
#   Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_*_BaseColor/Normal.png
#   Assets/Resources/PawnRushMiniGames/Materials/PR_Mid_CenterGate/WallArch/WalnutFloor/WindowGlass.mat
# .meta files are copied from the D gate's (same import settings) with a stable guid from the asset path.
# Re-running overwrites the FBX and textures; an existing .mat is left alone (it may be tuned in Unity).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_install_unity.py
import hashlib, os, re, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
EXPORT = os.path.join(HERE, "Export")
TEX = os.path.join(HERE, "Materials", "Textures")

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Mid/B"
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


def material(name, base=None, normal=None, color=None, emission=None, smooth=None, two_sided=False):
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
        t = t.replace("  m_ValidKeywords: []\n", "  m_ValidKeywords:\n  - _EMISSION\n")
        t = t.replace("m_LightmapFlags: 4", "m_LightmapFlags: 2")
    if smooth is not None:
        t = t.replace("- _Smoothness: 0.15", "- _Smoothness: %g" % smooth)
    if two_sided:
        t = t.replace("- _Cull: 2", "- _Cull: 0").replace("m_DoubleSidedGI: 0", "m_DoubleSidedGI: 1")
    write(rel, t)
    meta_from(MAT_DIR + "/PR_Early_CenterGate.mat", rel)
    print("made", rel)


def main():
    folder(FBX_DIR)
    folder(TEX_DIR)
    # B_Plank / B_StationEnv: the core scene (b_core_scene_v1.py)
    # B_TeamArch / B_HallEnv: the hall (b_hall_v1.py)
    for name in ("B_CenterGate", "B_WallArch", "B_Plank", "B_StationEnv", "B_TeamArch", "B_HallEnv", "B_UpperEnv", "B_ShellEnv"):
        copy(os.path.join(EXPORT, name + ".fbx"), FBX_DIR + "/" + name + ".fbx", FBX_META)
    g = {}
    for name in ("PR_Mid_CenterGate", "PR_Mid_WallArch", "PR_Mid_WalnutFloor", "PR_Mid_BasementStone",
                 "PR_Mid_RusticKit", "PR_Mid_WoodKit", "PR_Mid_CarpentryKit", "PR_Mid_GateKit", "PR_Mid_HallKit",
                 "PR_Mid_Canvas", "PR_Mid_JoistWood", "PR_Mid_UpperKit", "PR_Mid_CeilingKit",
                 "PR_Mid_SlateFloor", "PR_Mid_CheckerMarble", "PR_Mid_CreamFloor", "PR_Mid_MatteBrass"):
        g[name] = (copy(os.path.join(TEX, name + "_BaseColor.png"), TEX_DIR + "/" + name + "_BaseColor.png", BASE_META),
                   copy(os.path.join(TEX, name + "_Normal.png"), TEX_DIR + "/" + name + "_Normal.png", NORMAL_META))
    # B_ShellEnv's fill walls: the phase-1 cream stone brick tile (2 m), one continuous ashlar for every gap
    early = os.path.join(HERE, "..", "Early", "Materials", "Textures", "PR_Early_StoneBrick_%s.png")
    g["PR_Mid_AshlarFill"] = (copy(early % "BaseColor", TEX_DIR + "/PR_Mid_AshlarFill_BaseColor.png", BASE_META),
                              copy(early % "Normal", TEX_DIR + "/PR_Mid_AshlarFill_Normal.png", NORMAL_META))
    material("PR_Mid_AshlarFill", *g["PR_Mid_AshlarFill"], smooth=.1)
    material("PR_Mid_CenterGate", *g["PR_Mid_CenterGate"])
    material("PR_Mid_WallArch", *g["PR_Mid_WallArch"])
    material("PR_Mid_WalnutFloor", *g["PR_Mid_WalnutFloor"], smooth=.3)
    material("PR_Mid_BasementStone", *g["PR_Mid_BasementStone"], smooth=.08)
    # the game's floor boxes (MiniGameModels.RepairWingFloors): station slate, entry chequer, rear corridor cream
    material("PR_Mid_SlateFloor", *g["PR_Mid_SlateFloor"], smooth=.18)
    material("PR_Mid_CheckerMarble", *g["PR_Mid_CheckerMarble"], smooth=.5)
    material("PR_Mid_CreamFloor", *g["PR_Mid_CreamFloor"], smooth=.4)
    material("PR_Mid_MatteBrass", *g["PR_Mid_MatteBrass"], smooth=.45)   # B_ShellEnv aisle edge strips
    for kit in ("PR_Mid_RusticKit", "PR_Mid_WoodKit", "PR_Mid_CarpentryKit", "PR_Mid_GateKit", "PR_Mid_HallKit",
                "PR_Mid_UpperKit", "PR_Mid_CeilingKit"):
        material(kit, *g[kit], smooth=.2)
    # code-made pieces (MiniGameFixtures): the swaying team-gate cloth (seen from both sides), its battens, the flames
    material("PR_Mid_Canvas", *g["PR_Mid_Canvas"], smooth=.05, two_sided=True)
    material("PR_Mid_JoistWood", *g["PR_Mid_JoistWood"], smooth=.1)
    material("PR_Mid_Flame", color=(1., .78, .4), emission=(4., 2.2, .7), smooth=.0)
    # warm daylight in the window, as the concept: bright, a little emissive, no texture
    material("PR_Mid_WindowGlass", color=(1., .9, .72), emission=(.9, .72, .45), smooth=.6)


main()
