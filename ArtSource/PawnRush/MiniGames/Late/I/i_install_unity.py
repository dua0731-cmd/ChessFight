# Copies the I King's Bedchamber models and materials into Unity (R511), the same layout as H / J (helpers from
# Mid/e_install_unity.py, loaded with its main() stubbed, as j_install_unity.py does):
#   Assets/Resources/PawnRushMiniGames/Late/I/I_*.fbx        (i_props_v1.py, i_devices_v1.py -> Late/Export/I)
#   Assets/Art/PawnRushMiniGames/Late/Textures/<name>_*.png
#   Assets/Resources/PawnRushMiniGames/Materials/<name>.mat   the I swatches (carpets, plum velvet, furniture walnut),
#                                                            the bed linen (J's linen swatch) and the plain colours
# The G / H set (PR_Late_MarbleCream / Brass / Walnut / Ermine / Ebony) is already installed and is reused as it is.
# Re-running overwrites FBX and textures; an existing .mat is left alone (it may be tuned in Unity).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I i_install_unity.py
import os, re

HERE = os.path.dirname(os.path.abspath(__file__))
LATE = os.path.dirname(HERE)
MID = os.path.join(os.path.dirname(LATE), "Mid")
EXPORT = os.path.join(LATE, "Export", "I")
LTEX = os.path.join(LATE, "Materials", "Textures")

_src = open(os.path.join(MID, "e_install_unity.py"), encoding="utf-8").read().replace("\nmain()\n", "\n")
e = type(os)("e_install")
e.__file__ = os.path.join(MID, "e_install_unity.py")
exec(compile(_src, e.__file__, "exec"), e.__dict__)

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Late/I"
TEX_DIR = "Assets/Art/PawnRushMiniGames/Late/Textures"
MID_TEX_DIR = "Assets/Art/PawnRushMiniGames/Mid/Textures"
MAT_META = "Assets/Resources/PawnRushMiniGames/Early/R502/PR_Early_MoatWater.mat.meta"   # a well-formed .mat.meta
MADE = []


def tex(name, kind, meta):
    return e.copy(os.path.join(LTEX, "%s_%s.png" % (name, kind)), "%s/%s_%s.png" % (TEX_DIR, name, kind), meta)


def mat(name, **kw):
    e.material(name, **kw)
    MADE.append(e.MAT_DIR + "/" + name + ".mat")


def main():
    e.folder(FBX_DIR)
    e.folder(TEX_DIR)
    for fbx in sorted(os.listdir(EXPORT)):
        if fbx.endswith(".fbx"):
            e.copy(os.path.join(EXPORT, fbx), FBX_DIR + "/" + fbx, e.FBX_META)
    # the two carpet swatches (the board squares) and the plum velvet: BaseColor + Normal, 1 m tiles
    for name, smooth in (("PR_Late_I_CarpetLight", .05), ("PR_Late_I_CarpetDark", .05), ("PR_Late_I_Velvet", .12)):
        mat(name, base=tex(name, "BaseColor", e.BASE_META), normal=tex(name, "Normal", e.NORMAL_META), smooth=smooth)
    # the furniture walnut (i_props_v1.py walnut_texture: colour only, u along the grain)
    mat("PR_Late_I_PropWalnut", base=tex("PR_Late_I_PropWalnut", "BaseColor", e.BASE_META), smooth=.35)
    # bed linen: J's undyed linen swatch, already installed under Mid
    mat("PR_Late_I_Linen", base=e.guid(MID_TEX_DIR + "/PR_Mid_DollLinen_BaseColor.png"),
        normal=e.guid(MID_TEX_DIR + "/PR_Mid_DollLinen_Normal.png"), smooth=.08)
    # bright functional gold (bed base band, cart grip stripe) = the game's Arena.Gold look, a little glow for bloom
    mat("PR_Late_I_GripGold", color=(.95, .74, .30), emission=(.35, .25, .05), smooth=.75, metallic=1.)
    mat("PR_Late_Iron", color=(.27, .27, .29), smooth=.45, metallic=.7)
    mat("PR_Late_I_Seam", color=(.38, .29, .21), smooth=.1)
    # the plain colours of the props (i_props_v1.py COLORS, linear -> sRGB)
    src = open(os.path.join(HERE, "i_props_v1.py"), encoding="utf-8").read()
    for m in re.finditer(r'"(PR_Late_I_Prop\w+)": \(([\d.]+), ([\d.]+), ([\d.]+)\)', src):
        c = tuple(round(float(m.group(k)) ** (1 / 2.2), 3) for k in (2, 3, 4))
        mat(m.group(1), color=c, smooth=.7 if "Ceramic" in m.group(1) else .3 if "Wax" in m.group(1) else .1)
    # the room shell (i_hall_v1.py): window lattice and skylight ribs in a warm ivory paint
    mat("PR_Late_I_Lattice", color=(.93, .91, .87), smooth=.35)
    # the code banners' cloth (i_hall_v1.py banner_texture: king over a crescent, V hem cut by alpha), alpha-clipped
    # like G's royal banner
    base = tex("PR_Late_I_Banner", "BaseColor", e.BASE_META)
    mat("PR_Late_I_Banner", base=base, smooth=.1)
    rel = e.MAT_DIR + "/PR_Late_I_Banner.mat"
    t = e.read(rel)
    if "_ALPHATEST_ON" not in t:
        if "  m_ValidKeywords: []\n" in t: t = t.replace("  m_ValidKeywords: []\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n")
        else: t = t.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _ALPHATEST_ON\n", 1)
        t = t.replace("- _AlphaClip: 0", "- _AlphaClip: 1").replace("m_CustomRenderQueue: -1", "m_CustomRenderQueue: 2450")
        t = t.replace("RenderType: Opaque", "RenderType: TransparentCutout")
        t = t.replace("- _Cull: 2", "- _Cull: 0")            # both faces (cloth seen from behind too)
        e.write(rel, t)
        print("alpha clip", rel)
    # e.material copies its .meta from a template that is sometimes not a .meta: make sure every new one is well formed
    for rel in MADE:
        if not os.path.exists(e.p(rel)):
            continue
        text = e.read(rel + ".meta") if os.path.exists(e.p(rel + ".meta")) else ""
        if not text.startswith("fileFormatVersion"):
            e.meta_from(MAT_META, rel)
            print("meta", rel)


main()
