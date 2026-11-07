# Copies the J Palace Laundry models into Unity (R509), the same layout as B/E (e_install_unity.py):
#   Assets/Resources/PawnRushMiniGames/Mid/J/J_Tub.fbx, J_Nozzle.fbx, J_Leaf.fbx, J_Wheel.fbx   (j_props_v1.py)
#   Assets/Art/PawnRushMiniGames/Mid/Textures/PR_Mid_J_TubSluice_BaseColor/Normal.png             (the Tripo atlas)
#   Assets/Resources/PawnRushMiniGames/Materials/PR_Mid_J_TubSluice/NozzleBrass/NozzleHole.mat
# .meta files are copied from the D gate's (same import settings) with a stable guid from the asset path.
# Re-running overwrites the FBX and textures; an existing .mat is left alone (it may be tuned in Unity).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I j_install_unity.py
import importlib.util, os

HERE = os.path.dirname(os.path.abspath(__file__))
MID = os.path.dirname(HERE)
EXPORT = os.path.join(MID, "Export")
TEX = os.path.join(MID, "Materials", "Textures")

# Reuse E's helpers (guid, folder, copy, material) without running its main(): load it with main() stubbed.
_src = open(os.path.join(MID, "e_install_unity.py"), encoding="utf-8").read().replace("\nmain()\n", "\n")
e = type(os)("e_install")
e.__file__ = os.path.join(MID, "e_install_unity.py")
exec(compile(_src, e.__file__, "exec"), e.__dict__)

FBX_DIR = "Assets/Resources/PawnRushMiniGames/Mid/J"


def main():
    e.folder(FBX_DIR)
    e.folder(e.TEX_DIR)
    for name in ("J_Tub", "J_Nozzle", "J_Leaf", "J_Wheel"):
        e.copy(os.path.join(EXPORT, name + ".fbx"), FBX_DIR + "/" + name + ".fbx", e.FBX_META)
    name = "PR_Mid_J_TubSluice"
    base = e.copy(os.path.join(TEX, name + "_BaseColor.png"), e.TEX_DIR + "/" + name + "_BaseColor.png", e.BASE_META)
    normal = e.copy(os.path.join(TEX, name + "_Normal.png"), e.TEX_DIR + "/" + name + "_Normal.png", e.NORMAL_META)
    e.material(name, base, normal, smooth=.25)
    # the nozzle built in Blender has no UVs: plain matte brass and near-black holes (Blender colours as sRGB)
    e.material("PR_Mid_J_NozzleBrass", color=(.659, .514, .235), smooth=.55, metallic=.8)
    e.material("PR_Mid_J_NozzleHole", color=(.11, .09, .07), smooth=.1)
    # the room (j_hall_v1.py): E's hall without its wall bookcases + a white tile dado; the basin's white tile. The
    # other hall materials are B's / E's, already installed (b_install_unity.py, e_install_unity.py).
    for name in ("J_HallEnv", "J_BasinEnv"):
        e.copy(os.path.join(EXPORT, name + ".fbx"), FBX_DIR + "/" + name + ".fbx", e.FBX_META)
    name = "PR_Mid_WhiteTile"     # R509 swatch, 1 m tile (the models' box UVs repeat every metre), glossy glaze
    base = e.copy(os.path.join(TEX, name + "_BaseColor.png"), e.TEX_DIR + "/" + name + "_BaseColor.png", e.BASE_META)
    normal = e.copy(os.path.join(TEX, name + "_Normal.png"), e.TEX_DIR + "/" + name + "_Normal.png", e.NORMAL_META)
    e.material(name, base, normal, smooth=.7)
    # the hanging laundry (code cloth, MiniGameModels.Laundry LaundryLines): R509's undyed linen swatch, 0.5 m tile
    name = "PR_Mid_DollLinen"
    base = e.copy(os.path.join(TEX, name + "_BaseColor.png"), e.TEX_DIR + "/" + name + "_BaseColor.png", e.BASE_META)
    normal = e.copy(os.path.join(TEX, name + "_Normal.png"), e.TEX_DIR + "/" + name + "_Normal.png", e.NORMAL_META)
    e.material("PR_Mid_J_Linen", base, normal, smooth=.08)
    # the props modelled in Blender (j_laundry_props_v1.py): oak = R509 swatch (1 m tile), plain colours as sRGB
    e.copy(os.path.join(EXPORT, "J_PropsEnv.fbx"), FBX_DIR + "/J_PropsEnv.fbx", e.FBX_META)
    name = "PR_Mid_OakStaves"
    base = e.copy(os.path.join(TEX, name + "_BaseColor.png"), e.TEX_DIR + "/" + name + "_BaseColor.png", e.BASE_META)
    normal = e.copy(os.path.join(TEX, name + "_Normal.png"), e.TEX_DIR + "/" + name + "_Normal.png", e.NORMAL_META)
    e.material(name, base, normal, smooth=.3)
    src = open(os.path.join(HERE, "j_laundry_props_v1.py"), encoding="utf-8").read()
    for m in e.re.finditer(r'"(PR_Mid_J_Prop\w+)": \(([\d.]+), ([\d.]+), ([\d.]+)\)', src):
        c = tuple(round(float(m.group(k)) ** (1 / 2.2), 3) for k in (2, 3, 4))
        J_MATS.append(e.MAT_DIR + "/" + m.group(1) + ".mat")
        e.material(m.group(1), color=c, smooth=.55 if "Zinc" in m.group(1) else .2, metallic=.6 if "Zinc" in m.group(1) else None)
    # the cloth dolls (j_dolls_v1.py); body PR_Mid_J_Linen and trim PR_Mid_J_DollTrim, both near white: Unity tints them
    # per station (MiniGameModels.Laundry LaundryDolls)
    for kind in ("Pawn", "Bishop", "Knight", "Rook", "Queen", "King"):
        e.copy(os.path.join(EXPORT, "J_Doll_" + kind + ".fbx"), FBX_DIR + "/J_Doll_" + kind + ".fbx", e.FBX_META)
    e.material("PR_Mid_J_DollTrim", color=(.94, .93, .91), smooth=.1)
    J_MATS.append(e.MAT_DIR + "/PR_Mid_J_DollTrim.mat")
    soapy_water()
    fix_metas()


# The basin's soapy water (user 11-08 "비눗물 셰이더", then "색은 좋은데 뭔가 물 처럼 안보여"): J's own indoor water shader
# (Assets/Art/PawnRushMap/Shaders/PR_SoapyWater.shader, a copy of C's moat water that keeps its caustics and glints
# under a ceiling) with C's wave and caustic maps. Milky pale teal kept (the user liked the colour), clearer so the
# tiles show through, stronger waves and refraction (the tile grid wobbles), the room lights and a window sheen glinting,
# white foam along the tiles and round every tub. Re-running rewrites it (these values are the source).
SOAPY = "Assets/Resources/PawnRushMiniGames/Mid/J/PR_Mid_J_SoapyWater.mat"
MOAT = "Assets/Resources/PawnRushMiniGames/Early/R502/PR_Early_MoatWater.mat"
MOAT_SHADER = "f8c857c7c9b02da4b8507dbc39986e4f"
SOAPY_SHADER = "5c775af55b2319d6a53382149bb2e2ce"     # PR_SoapyWater.shader.meta
# user 11-08 "좋다 여기서 살짝만 손 봐 좀 더 물처럼": a little more of what reads as water - clearer (1.3 -> 1.6 m),
# livelier waves (0.9 -> 1.1, drift 0.1 -> 0.14 m/s) bending the tiles more (refraction 0.035 -> 0.045), a stronger
# fresnel reflection (0.5 -> 0.62, power 4 -> 3.5), sharper and brighter light glints, a little more caustic net
SOAPY_FLOATS = {"_Clarity": 1.6, "_WaveTile": 2.2, "_WaveStrength": 1.1, "_WaveSpeed": .14, "_CausticTile": 1.4,
                "_CausticStrength": 1.3, "_CausticSurface": .22, "_CausticFade": 1.6, "_FoamWidth": .4,
                "_FoamBreakup": .45, "_Refraction": .045, "_FresnelPower": 3.5, "_Reflection": .62, "_Glint": 3,
                "_GlintSharpness": 400, "_WindowGlint": 1.5, "_WindowSharpness": 110, "_PointGlint": 4,
                "_PointSharpness": 320}
SOAPY_COLORS = {"_ShallowColor": (.82, .96, .95), "_DeepColor": (.45, .78, .80), "_SkyColor": (.93, .90, .84),
                "_FoamColor": (1., 1., 1.), "_IndoorLight": (1., .95, .85), "_WindowColor": (1., .97, .9),
                "_WindowDir": (.6, .75, .25)}


def soapy_water():
    t = e.read(MOAT).replace("m_Name: PR_Early_MoatWater", "m_Name: PR_Mid_J_SoapyWater").replace(MOAT_SHADER, SOAPY_SHADER)
    floats = "".join("    - %s: %g\n" % kv for kv in SOAPY_FLOATS.items())
    colors = "".join("    - %s: {r: %g, g: %g, b: %g, a: %d}\n" % (k, *c, 0 if k == "_WindowDir" else 1) for k, c in SOAPY_COLORS.items())
    t = t.replace("    m_Floats: []\n", "    m_Floats:\n" + floats).replace("    m_Colors: []\n", "    m_Colors:\n" + colors)
    e.write(SOAPY, t)
    print("made", SOAPY)


# e_install_unity.material() writes a .mat's .meta from the template .mat itself (not its .meta), so the .meta held the
# material's YAML. J's materials get a real NativeFormatImporter .meta (the moat water's) with the stable path guid.
J_MATS = [e.MAT_DIR + "/" + n + ".mat" for n in ("PR_Mid_J_TubSluice", "PR_Mid_J_NozzleBrass", "PR_Mid_J_NozzleHole",
                                                  "PR_Mid_WhiteTile", "PR_Mid_J_Linen", "PR_Mid_OakStaves")] + [SOAPY]


def fix_metas():
    for rel in J_MATS:
        if not os.path.exists(e.p(rel)):
            continue
        text = e.read(rel + ".meta") if os.path.exists(e.p(rel + ".meta")) else ""
        if text.startswith("fileFormatVersion"):
            continue
        e.meta_from(MOAT + ".meta", rel)
        print("meta", rel)


main()
