# Grand banquet hall material library into Unity without the editor (B0, seoungtest R837, 2026-11-25).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I bh_unity_materials.py [names]
#
# Reads textures/library (bh_textures.py) and writes, with GUIDs fixed by the path, through the carriage yard's writer
# (as the observatory's ro_unity_materials.py does):
#   Assets/Art/BanquetHall/Textures/BH_<Name>_Albedo.png / _Normal.png + .meta
#   Assets/Resources/BanquetHall/Materials/BH_<Name>.mat + .meta (URP Lit; tiling 1 / the tile's metres)
#   BH_Crystal: clear, transparent, no textures. BH_China, BH_Emerald: colour only (no textures).
# GrandBanquetHallDressing draws each bh_kit slot "BH | <slot>" with BH_<Name>. Never run while Unity is in Play.
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
ART_REL = "Assets/Art/BanquetHall"
TEX_REL = ART_REL + "/Textures"
RES_REL = "Assets/Resources/BanquetHall"
MAT_REL = RES_REL + "/Materials"
SMALL = {"BH_Wax", "BH_Ebony", "BH_Ivory", "BH_Chocolate"}
GLASS = {"BH_Crystal": (.9, .95, 1.0, .25, .96, False)}
# colour-only opaque: name -> (r, g, b (linear), smoothness, metallic)
COLOURED = {"BH_China": (.86, .85, .82, .88, 0.0), "BH_Emerald": (.035, .10, .012, .93, 0.0)}
rlm.MAT_REL = MAT_REL
rlm.GLASS = GLASS


def mat_meta(rel):
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        ru.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % ru.guid(rel))


def texture(name, kind, size, normal=False):
    rel = "%s/%s_%s.png" % (TEX_REL, name, kind)
    shutil.copyfile(os.path.join(LIB, "%s_%s.png" % (name, kind)), os.path.join(ROOT, rel))
    meta = os.path.join(ROOT, rel + ".meta")
    if os.path.exists(meta):
        with open(meta, encoding="utf-8") as f:
            return f.read().split("\n")[1].split()[1]
    g = ru.guid(rel)
    ru.write(rel + ".meta", ru.texture_meta(g, normal, size))
    return g


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
    print("BH_MAT", name, "colour", (r, g, b), "smoothness", smooth)


def main(names):
    with open(os.path.join(LIB, "bh_textures.json"), encoding="utf-8") as f:
        info = json.load(f)
    for rel in (ART_REL, TEX_REL, RES_REL, MAT_REL):
        os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
        ru.folder_meta(rel)
    for name in names or sorted(info) + sorted(GLASS) + sorted(COLOURED):
        if name in GLASS:
            rlm.write_glass(name)
            continue
        if name in COLOURED:
            write_coloured(name)
            continue
        spec = info[name]
        size = 512 if name in SMALL else 1024
        ga = texture(name, "Albedo", size)
        gn = texture(name, "Normal", size, normal=True)
        rel = "%s/%s.mat" % (MAT_REL, name)
        ru.write(rel, ru.material(name, ga, gn, spec["tile"], spec["metallic"]))
        mat_meta(rel)
        print("BH_MAT", name, "tile", spec["tile"], "metallic", spec["metallic"], "max", size)


if __name__ == "__main__":
    main([a for a in sys.argv[1].split(",") if a] if len(sys.argv) > 1 else [])
