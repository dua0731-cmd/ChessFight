# Royal Observatory material library into Unity without the editor (R0, seoungtest R725, 2026-11-19).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ro_unity_materials.py [names]
#
# Reads textures/library (ro_textures.py) and writes, with GUIDs fixed by the path, through the carriage yard's writer
# (as the library's rl_unity_materials.py does):
#   Assets/Art/RoyalObservatory/Textures/RO_<Name>_Albedo.png / _Normal.png (+ RO_Motif_MetallicSmoothness.png) + .meta
#   Assets/Resources/RoyalObservatory/Materials/RO_<Name>.mat + .meta (URP Lit; tiling 1 / the tile's metres)
#   RO_Motif: the floor-motif atlas, tiling 1 (the models' motif faces carry atlas UVs), a metallic map (R metallic,
#   A smoothness) so only the gold is metal, not resized to a power of two (4096 x 5120).
#   RO_Glass: clear, transparent, no textures (rl_unity_materials.write_glass).
# The zones' dressing (RoyalObservatoryDressing) draws each slot with RO_<Name>. Never run while Unity is in Play.
# Existing .meta files are kept (their GUIDs stay).

import json, os, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels", "textures"))
sys.path.insert(0, os.path.join(HERE, "..", "RoyalLibraryModels"))
import rcy_unity_materials as ru   # noqa: E402
import rl_unity_materials as rlm   # noqa: E402  (write_glass)

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ru.ROOT = ROOT
LIB = os.path.join(HERE, "textures", "library")
ART_REL = "Assets/Art/RoyalObservatory"
TEX_REL = ART_REL + "/Textures"
RES_REL = "Assets/Resources/RoyalObservatory"
MAT_REL = RES_REL + "/Materials"
SMALL = {"RO_Velvet"}
GLASS = {"RO_Glass": (.86, .92, 1.0, .18, .96, False)}
rlm.MAT_REL = MAT_REL
rlm.GLASS = GLASS


def mat_meta(rel):
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        ru.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % ru.guid(rel))


def texture(name, kind, size, linear=False, normal=False, npot=False):
    rel = "%s/%s_%s.png" % (TEX_REL, name, kind)
    shutil.copyfile(os.path.join(LIB, "%s_%s.png" % (name, kind)), os.path.join(ROOT, rel))
    meta = os.path.join(ROOT, rel + ".meta")
    if os.path.exists(meta):
        with open(meta, encoding="utf-8") as f:
            return f.read().split("\n")[1].split()[1]
    g = ru.guid(rel)
    text = ru.texture_meta(g, normal, size)
    if linear:
        text = text.replace("    sRGBTexture: 1\n", "    sRGBTexture: 0\n", 1)
    if npot:
        text = text.replace("  nPOTScale: 1\n", "  nPOTScale: 0\n", 1)
    ru.write(rel + ".meta", text)
    return g


def write_motif(spec):
    ga = texture("RO_Motif", "Albedo", 8192, npot=True)
    gn = texture("RO_Motif", "Normal", 8192, normal=True, npot=True)
    gm = texture("RO_Motif", "MetallicSmoothness", 8192, linear=True, npot=True)
    mat = ru.material("RO_Motif", ga, gn, 1.0, 1.0)
    old = "    - _MetallicGlossMap:\n        m_Texture: {fileID: 0}\n"
    assert mat.count(old) == 1
    mat = mat.replace(old, "    - _MetallicGlossMap:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n" % gm)
    for a, b in (("  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n", "  - _METALLICSPECGLOSSMAP\n"),
                 ("    - _SmoothnessTextureChannel: 1\n", "    - _SmoothnessTextureChannel: 0\n")):
        assert mat.count(a) == 1, a
        mat = mat.replace(a, b)
    rel = "%s/RO_Motif.mat" % MAT_REL
    ru.write(rel, mat)
    mat_meta(rel)
    print("RO_MAT RO_Motif atlas", spec.get("atlas"), "metallic map")


# Painted single textures (not tiles, no normal map): name -> (source png relative to this folder, smoothness, max size).
# R6 11-19 (ro-shell, user "확인"): the dome's sky, its UVs made by the shell's script.
PAINTED = {"RO_DomeSky": ("textures/r6/RO_DomeSky_Albedo.png", .4, 4096)}
# RO_NightSky (R6 11-19, the night beyond the dome's slit, user "이대로 확인"): written once by hand from URP's Unlit
# (Assets/Resources/URPVariants/Unlit.mat) with RO_DomeSky_Albedo.png on _BaseMap, tiling 1, both sides (_Cull 0); the
# texture's import repeats (wrap 0), so UVs past 0..1 carry on. Not regenerated here.


def write_painted(name):
    src, smooth, size = PAINTED[name]
    rel = "%s/%s_Albedo.png" % (TEX_REL, name)
    shutil.copyfile(os.path.join(HERE, src), os.path.join(ROOT, rel))
    meta = os.path.join(ROOT, rel + ".meta")
    if os.path.exists(meta):
        with open(meta, encoding="utf-8") as f:
            ga = f.read().split("\n")[1].split()[1]
    else:
        ga = ru.guid(rel)
        ru.write(rel + ".meta", ru.texture_meta(ga, False, size).replace("  nPOTScale: 1\n", "  nPOTScale: 0\n", 1))
    mat = ru.material(name, ga, None, 1.0, 0.0)
    for a, b in (("  - _NORMALMAP\n  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n", ""),
                 ("    - _SmoothnessTextureChannel: 1\n", "    - _SmoothnessTextureChannel: 0\n"),
                 ("    - _Smoothness: 1\n", "    - _Smoothness: %g\n" % smooth)):
        assert mat.count(a) == 1, a
        mat = mat.replace(a, b)
    rel = "%s/%s.mat" % (MAT_REL, name)
    ru.write(rel, mat)
    mat_meta(rel)
    print("RO_MAT", name, "painted, smoothness", smooth)


# Colour-only opaque materials (no textures): name -> (r, g, b, smoothness, metallic).
# R3 11-19 (ro-hall): the globe's black grid lines, the user "좌표(###)를 검은색으로 해".
COLOURED = {"RO_BlackEnamel": (.012, .012, .014, .7, 0.0)}


def write_coloured(name):
    r, g, b, smooth, metal = COLOURED[name]
    mat = ru.material(name, None, None, 1.0, metal)
    for a, c in (("  - _NORMALMAP\n  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n", ""),
                 ("    - _SmoothnessTextureChannel: 1\n", "    - _SmoothnessTextureChannel: 0\n"),
                 ("    - _Smoothness: 1\n", "    - _Smoothness: %g\n" % smooth),
                 ("    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}\n", "    - _BaseColor: {r: %g, g: %g, b: %g, a: 1}\n" % (r, g, b)),
                 ("    - _Color: {r: 1, g: 1, b: 1, a: 1}\n", "    - _Color: {r: %g, g: %g, b: %g, a: 1}\n" % (r, g, b))):
        assert mat.count(a) == 1, a
        mat = mat.replace(a, c)
    rel = "%s/%s.mat" % (MAT_REL, name)
    ru.write(rel, mat)
    mat_meta(rel)
    print("RO_MAT", name, "colour", (r, g, b), "smoothness", smooth)


def main(names):
    with open(os.path.join(LIB, "ro_textures.json"), encoding="utf-8") as f:
        info = json.load(f)
    for rel in (ART_REL, TEX_REL, RES_REL, MAT_REL):
        os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
        ru.folder_meta(rel)
    for name in names or sorted(info) + sorted(GLASS) + sorted(PAINTED) + sorted(COLOURED):
        if name in GLASS:
            rlm.write_glass(name)
            continue
        if name in PAINTED:
            write_painted(name)
            continue
        if name in COLOURED:
            write_coloured(name)
            continue
        spec = info[name]
        if name == "RO_Motif":
            write_motif(spec)
            continue
        size = 512 if name in SMALL else 2048 if name.startswith("RO_Planet") else 1024
        ga = texture(name, "Albedo", size)
        gn = texture(name, "Normal", size, normal=True)
        rel = "%s/%s.mat" % (MAT_REL, name)
        ru.write(rel, ru.material(name, ga, gn, spec["tile"], spec["metallic"]))
        mat_meta(rel)
        print("RO_MAT", name, "tile", spec["tile"], "metallic", spec["metallic"], "max", size)


if __name__ == "__main__":
    main([a for a in sys.argv[1].split(",") if a] if len(sys.argv) > 1 else [])
