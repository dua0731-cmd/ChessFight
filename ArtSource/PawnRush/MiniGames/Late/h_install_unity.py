# Copies the H King's Chess Hall models and materials into Unity (R510), the same layout and helpers as
# g_install_unity.py:
#   Assets/Resources/PawnRushMiniGames/Late/H/H_*.fbx          (h_props_v1.py, h_board_kit_v1.py -> Export/H)
#   Assets/Art/PawnRushMiniGames/Late/Textures/<name>_*.png
#   Assets/Resources/PawnRushMiniGames/Materials/<name>.mat     PR_Late_H_Props (Tripo atlas), PR_Late_Ebony,
#                                                              PR_Late_Bronze, PR_Late_FrozenStone (gold cracks glow)
# The G set (PR_Late_MarbleCream / Brass / Walnut / Velvet / Maple / G_Pieces) is already installed by g_install_unity.py
# and is reused as it is. Re-running overwrites FBX and textures; an existing .mat is left alone.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python h_install_unity.py
import hashlib, os, re, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
EXPORT = os.path.join(HERE, "Export", "H")
TEX = os.path.join(HERE, "Materials", "Textures")

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Late/H"
TEX_DIR = "Assets/Art/PawnRushMiniGames/Late/Textures"
MAT_DIR = "Assets/Resources/PawnRushMiniGames/Materials"
FBX_META = "Assets/Resources/PawnRushMiniGames/Early/D/D_CenterGate.fbx.meta"
BASE_META = "Assets/Art/PawnRushMiniGames/Early/Textures/PR_Early_CenterGate_BaseColor.png.meta"
NORMAL_META = "Assets/Art/PawnRushMiniGames/Early/Textures/PR_Early_CenterGate_Normal.png.meta"
MAT_TEMPLATE = MAT_DIR + "/PR_Early_CenterGate.mat"
BASE_GUID = "e0e48d248c6e1ecc97c0d5788c6b6b2c"
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


def material(name, base, normal, smooth, metallic=0.):
    rel = MAT_DIR + "/" + name + ".mat"
    if os.path.exists(p(rel)):
        print("kept", rel)
        return rel
    t = read(MAT_TEMPLATE).replace("m_Name: PR_Early_CenterGate", "m_Name: " + name)
    t = t.replace(BASE_GUID, base).replace(NORMAL_GUID, normal)
    t = t.replace("- _Smoothness: 0.15", "- _Smoothness: %g" % smooth)
    t = t.replace("- _Metallic: 0.00", "- _Metallic: %g" % metallic)
    write(rel, t)
    meta_from(MAT_DIR + "/PR_Early_CenterGate.mat", rel)
    print("made", rel)
    return rel


def textures(name, emission=False):
    out = []
    for kind, meta in (("BaseColor", BASE_META), ("Normal", NORMAL_META)) + ((("Emission", BASE_META),) if emission else ()):
        out.append(copy(os.path.join(TEX, "%s_%s.png" % (name, kind)), "%s/%s_%s.png" % (TEX_DIR, name, kind), meta))
    return out


def main():
    folder(FBX_DIR)
    folder(TEX_DIR)
    for fbx in sorted(os.listdir(EXPORT)):
        if fbx.endswith(".fbx"):
            copy(os.path.join(EXPORT, fbx), FBX_DIR + "/" + fbx, FBX_META)
    base, normal = textures("PR_Late_H_Props")
    material("PR_Late_H_Props", base, normal, .45)
    base, normal = textures("PR_Late_Ebony")
    material("PR_Late_Ebony", base, normal, .65)                # polished
    base, normal = textures("PR_Late_Bronze")
    material("PR_Late_Bronze", base, normal, .55, .85)
    # writing plates (h_decals_v1.py): RGBA ink on a transparent ground, alpha-clipped like G's royal banner; the
    # marble's normal map gives the ink the stone's grain
    marble_normal = guid(TEX_DIR + "/PR_Late_MarbleCream_Normal.png")
    for name in ("PR_Late_H_Notation", "PR_Late_H_ClockDial"):
        base = copy(os.path.join(TEX, name + "_BaseColor.png"), "%s/%s_BaseColor.png" % (TEX_DIR, name), BASE_META)
        rel = material(name, base, marble_normal, .3)
        t = read(rel)
        if "_ALPHATEST_ON" not in t:
            t = t.replace("  m_ValidKeywords:\n  - _NORMALMAP\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n  - _NORMALMAP\n")
            t = t.replace("- _AlphaClip: 0", "- _AlphaClip: 1").replace("m_CustomRenderQueue: -1", "m_CustomRenderQueue: 2450")
            t = t.replace("RenderType: Opaque", "RenderType: TransparentCutout")
            write(rel, t)
    # frozen king: grey stone, the cracks' mask lights them in functional gold (#E2B23C, HDR so bloom picks it up).
    # The game swaps the king to this material while frozen (MiniGameKingLook).
    base, normal, glow = textures("PR_Late_FrozenStone", emission=True)
    rel = material("PR_Late_FrozenStone", base, normal, .25)
    t = read(rel)
    if "_EMISSION" not in t:
        t = t.replace("  m_ValidKeywords:\n  - _NORMALMAP\n", "  m_ValidKeywords:\n  - _EMISSION\n  - _NORMALMAP\n")
        t = t.replace("    - _EmissionMap:\n        m_Texture: {fileID: 0}",
                      "    - _EmissionMap:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}" % glow)
        t = t.replace("- _EmissionColor: {r: 0, g: 0, b: 0, a: 1}", "- _EmissionColor: {r: 3.2, g: 2.2, b: 0.6, a: 1}")
        t = t.replace("  m_LightmapFlags: 4\n", "  m_LightmapFlags: 2\n")
        write(rel, t)
        print("emission on", rel)


main()
