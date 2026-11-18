# Royal Library material library into Unity without the editor (R0, seoungtest R687, 2026-11-18, 승규 "Unity 재질 만들어").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I rl_unity_materials.py [names]
#
# Reads textures/library (rl_textures.py: <Name>_Albedo.png with smoothness in alpha, <Name>_Normal.png, rl_textures.json)
# and writes, with deterministic GUIDs, through the carriage yard's writer (CarriageYardModels/textures/rcy_unity_materials.py):
#   Assets/Art/RoyalLibrary/Textures/<Name>_Albedo.png / _Normal.png + .meta (sRGB / normal map, mipmaps, trilinear,
#       aniso 4, max size by SMALL)
#   Assets/Resources/RoyalLibrary/Materials/<Name>.mat + .meta (URP Lit: base + normal map, smoothness from the albedo's
#       alpha, metallic per material, tiling = 1 / the tile's metres - the models' UVs are in metres as in the yard)
# Nothing uses these yet: the zones' dressing (PLAN.md R0 ~ R7) will pick them by name. Never run while Unity is in Play.
# Existing .meta files are kept (their GUIDs stay).

import json, os, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels", "textures"))
import rcy_unity_materials as ru   # noqa: E402  (its writer functions; its own paths are not used)

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ru.ROOT = ROOT
LIB = os.path.join(HERE, "textures", "library")
ART_REL = "Assets/Art/RoyalLibrary"
TEX_REL = ART_REL + "/Textures"
RES_REL = "Assets/Resources/RoyalLibrary"
MAT_REL = RES_REL + "/Materials"
SMALL = {"RL_InkGlass", "RL_CreamCloth", "RL_RedWax"}   # nearly flat or small parts: 512 (wax 256 source) in Unity, the rest 1024


def detail_texture(name, kind):
    """<Name>_Detail<kind>.png into Unity: the albedo as a LINEAR texture (URP's detail albedo is read as numbers,
    0.5 = no change; the material editor warns when it is sRGB), the normal as a normal map. 512 px."""
    rel = "%s/%s_Detail%s.png" % (TEX_REL, name, kind)
    shutil.copyfile(os.path.join(LIB, "%s_Detail%s.png" % (name, kind)), os.path.join(ROOT, rel))
    meta = os.path.join(ROOT, rel + ".meta")
    if os.path.exists(meta):
        with open(meta, encoding="utf-8") as f:
            return f.read().split("\n")[1].split()[1]
    g = ru.guid(rel)
    text = ru.texture_meta(g, kind == "Normal", 512)
    if kind == "Albedo":
        text = text.replace("    sRGBTexture: 1\n", "    sRGBTexture: 0\n", 1)
    ru.write(rel + ".meta", text)
    return g


def with_detail(mat, name, spec):
    """The detail slot on the URP Lit material: maps, tiling (relative to the base map's: the shader multiplies the
    base-transformed UV by _DetailAlbedoMap_ST), scales 1 and the _DETAIL_MULX2 keyword the Lit editor would set."""
    ga, gn = detail_texture(name, "Albedo"), detail_texture(name, "Normal")
    k = spec["tile"] / spec["detail"]["tile"]
    for prop, g in (("_DetailAlbedoMap", ga), ("_DetailNormalMap", gn)):
        old = "    - %s:\n        m_Texture: {fileID: 0}\n        m_Scale: {x: 1, y: 1}\n" % prop
        new = "    - %s:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n        m_Scale: {x: %g, y: %g}\n" % (prop, g, k, k)
        assert mat.count(old) == 1, prop
        mat = mat.replace(old, new)
    mat = mat.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _DETAIL_MULX2\n", 1)
    mat = mat.replace("    m_Floats:\n", "    m_Floats:\n    - _DetailAlbedoMapScale: 1\n    - _DetailNormalMapScale: 1\n", 1)
    return mat


# Transparent, untextured materials: (r, g, b, alpha, smoothness, double-sided)
GLASS = {
    "RL_Glass": (1.0, .97, .90, .2, .95, False),   # R7 11-18: the giant inkwell, the magnifier's lens
    "RL_Veil": (.81, .89, .91, .45, .90, True),    # R1 11-18: the dimensional entrance's veil (#CFE3E8), seen both ways
}


def write_glass(name):
    """A clear URP Lit Transparent material, no textures (R7): the yard writer's opaque Lit turned transparent the way
    the garden's water is (GC_basin_water.mat): alpha blend, no depth write, queue 3000, no shadow; smoothness .95 from
    the number (a transparent albedo's alpha is opacity, not smoothness)."""
    mat = ru.material(name, None, None, 1.0, 0.0)
    r, g, b, a, smooth, two_sided = GLASS[name]
    swaps = [
        ("    - _Cull: 2\n", "    - _Cull: %d\n" % (0 if two_sided else 2)),
        ("  m_ValidKeywords:\n  - _NORMALMAP\n  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n", "  m_ValidKeywords:\n  - _SURFACE_TYPE_TRANSPARENT\n"),
        ("  m_CustomRenderQueue: -1\n", "  m_CustomRenderQueue: 3000\n"),
        ("    RenderType: Opaque\n", "    RenderType: Transparent\n"),
        ("  disabledShaderPasses:\n  - MOTIONVECTORS\n", "  disabledShaderPasses:\n  - MOTIONVECTORS\n  - DepthOnly\n  - SHADOWCASTER\n"),
        ("    - _DstBlend: 0\n", "    - _DstBlend: 10\n"), ("    - _DstBlendAlpha: 0\n", "    - _DstBlendAlpha: 10\n"),
        ("    - _SrcBlend: 1\n", "    - _SrcBlend: 5\n"), ("    - _Surface: 0\n", "    - _Surface: 1\n"),
        ("    - _ZWrite: 1\n", "    - _ZWrite: 0\n"), ("    - _Smoothness: 1\n", "    - _Smoothness: %g\n" % smooth),
        ("    - _SmoothnessTextureChannel: 1\n", "    - _SmoothnessTextureChannel: 0\n"),
        ("    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}\n", "    - _BaseColor: {r: %g, g: %g, b: %g, a: %g}\n" % (r, g, b, a)),
    ]
    for old, new in swaps:
        assert mat.count(old) == 1, old
        mat = mat.replace(old, new)
    rel = "%s/%s.mat" % (MAT_REL, name)
    ru.write(rel, mat)
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        ru.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                 % ru.guid(rel))
    print("RL_MAT", name, "transparent, alpha", a)


def main(names):
    with open(os.path.join(LIB, "rl_textures.json"), encoding="utf-8") as f:
        info = json.load(f)
    for rel in (ART_REL, TEX_REL, RES_REL, MAT_REL):
        os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
        ru.folder_meta(rel)
    for name in names or sorted(info) + sorted(GLASS):
        if name in GLASS:
            write_glass(name)
            continue
        spec = info[name]
        size = 512 if name in SMALL else 1024
        # a colour variant (rl_textures TINTS, "share": its source): its own albedo, the source's normal and detail maps
        src = spec.get("share", name)
        gs = {}
        for kind in ("Albedo", "Normal"):
            own = name if kind == "Albedo" else src
            rel = "%s/%s_%s.png" % (TEX_REL, own, kind)
            if own != name:
                meta = os.path.join(ROOT, rel + ".meta")
                if not os.path.exists(meta):
                    raise RuntimeError("%s shares %s's %s: run %s first" % (name, src, kind, src))
                with open(meta, encoding="utf-8") as f:
                    gs[kind] = f.read().split("\n")[1].split()[1]
                continue
            shutil.copyfile(os.path.join(LIB, "%s_%s.png" % (name, kind)), os.path.join(ROOT, rel))
            meta = os.path.join(ROOT, rel + ".meta")
            if os.path.exists(meta):
                with open(meta, encoding="utf-8") as f:
                    gs[kind] = f.read().split("\n")[1].split()[1]
            else:
                gs[kind] = ru.guid(rel)
                ru.write(rel + ".meta", ru.texture_meta(gs[kind], kind == "Normal", size))
        mat = ru.material(name, gs["Albedo"], gs["Normal"], spec["tile"], spec["metallic"])
        if "detail" in spec:
            mat = with_detail(mat, src, spec)
        rel = "%s/%s.mat" % (MAT_REL, name)
        ru.write(rel, mat)
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            ru.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                    "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                     % ru.guid(rel))
        print("RL_MAT", name, "tile", spec["tile"], "metallic", spec["metallic"], "max", size)


if __name__ == "__main__":
    main([a for a in sys.argv[1].split(",") if a] if len(sys.argv) > 1 else [])
