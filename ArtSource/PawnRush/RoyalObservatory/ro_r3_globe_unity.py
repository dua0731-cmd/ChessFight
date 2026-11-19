# R3 the great globe's own materials into Unity without the editor (ro-hall session, R725, 2026-11-19).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ro_r3_globe_unity.py
#
# Saturn's ring and the moon: single pictures, not tiles (11-19: the seven planets are ro-path's library now -
# RO_Planet<Name> from ro_textures.py / ro_unity_materials.py, same .mat paths and GUIDs). Reads textures/r3/RO_Planet<Name>.png (made by
# ro_r3_globe_model.py from the Codex sheets AssetRefs/Planets/RO_P1 .. P9) and writes, GUIDs fixed by the path:
#   Assets/Art/RoyalObservatory/Globe/RO_Planet<Name>_Albedo.png + .meta
#   Assets/Resources/RoyalObservatory/Materials/RO_Planet<Name>.mat + .meta (URP Lit, one painted texture over the
#   model's UVs, tiling 1, smoothness from the number - the texture's alpha is not smoothness)
# RO_PlanetSaturnRing is transparent (its texture's alpha is the ring's gaps). RoyalObservatoryDressing draws a model's
# "RO | planet_<name>" slot with RO_Planet<Name>. Never run while Unity is in Play; existing .meta files are kept.

import os, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels", "textures"))
import rcy_unity_materials as ru   # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ru.ROOT = ROOT
SRC = os.path.join(HERE, "textures", "r3")
TEX_REL = "Assets/Art/RoyalObservatory/Globe"
MAT_REL = "Assets/Resources/RoyalObservatory/Materials"
# name -> (smoothness, max texture size, transparent)
PLANETS = {
    "RO_PlanetSaturnRing": (.3, 2048, True),
    "RO_PlanetSun": (.2, 2048, False),
}

# The observatory's glass (11-20, the user on Unity Play: the glass skin looked odd, "좀 더 그래픽이 좋았으면"): the
# shader Assets/Art/RoyalObservatory/Shaders/RO_Glass.shader (the project's crystal glass - what is behind bent and a
# little frosted, a night tint, reflections, a cool rim - with the globe's grid engraved from its texture's alpha).
# RO_PlanetGlobeGlass keeps its path and GUID (the globe's "planet_globe_glass" slot); RO_TubeGlass is put on the
# telescope tube's glass by RoyalObservatoryDressing.Telescope.cs.
GLASS_SHADER_REL = "Assets/Art/RoyalObservatory/Shaders/RO_Glass.shader"
GLASS = {
    # name: (texture (None: plain), floats, colours)
    "RO_PlanetGlobeGlass": ("RO_PlanetGlobeGlass", {
        "_Cull": 0, "_LineStrength": .9, "_LineFloor": .1, "_FresnelPower": 3.2, "_FresnelStrength": 2.0, "_Frost": .06,
        "_FrostRadius": .01, "_Distortion": .012, "_NoiseScale": .5, "_NoiseSpeed": .03, "_Reflection": .16,
        "_Smoothness": .96, "_Highlight": 1.6},
        {"_BaseColor": (.55, .62, 1.0, .05), "_LineColor": (.02, .02, .04, 1), "_FresnelColor": (.7, .78, 1.0, 1)}),
    "RO_TubeGlass": (None, {
        "_Cull": 2, "_LineStrength": 0, "_LineFloor": .1, "_FresnelPower": 3.0, "_FresnelStrength": 2.6, "_Frost": .12,
        "_FrostRadius": .012, "_Distortion": .02, "_NoiseScale": 1.2, "_NoiseSpeed": .04, "_Reflection": .2,
        "_Smoothness": .96, "_Highlight": 1.8},
        {"_BaseColor": (.58, .6, 1.0, .07), "_LineColor": (.02, .02, .04, 1), "_FresnelColor": (.78, .8, 1.0, 1)}),
}


GLASS_MAT = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: NAME
  m_Shader: {fileID: 4800000, guid: SHADER, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
TEXENVS    m_Ints: []
    m_Floats:
FLOATS    m_Colors:
COLOURS  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
MAT_META = """fileFormatVersion: 2
guid: GUID
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def glass_material(name, tex, floats, colours):
    with open(os.path.join(ROOT, GLASS_SHADER_REL + ".meta"), encoding="utf-8") as f:
        shader_guid = f.read().splitlines()[1].split()[1]
    if tex:
        g = texture(tex, 4096)
        texenvs = ("    m_TexEnvs:\n    - _BaseMap:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n"
                   "        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n" % g)
    else:
        texenvs = "    m_TexEnvs: []\n"
    fl = "".join("    - %s: %g\n" % (k_, v) for k_, v in sorted(floats.items()))
    co = "".join("    - %s: {r: %g, g: %g, b: %g, a: %g}\n" % ((k_,) + tuple(v)) for k_, v in sorted(colours.items()))
    mat = (GLASS_MAT.replace("NAME", name).replace("SHADER", shader_guid).replace("TEXENVS", texenvs)
           .replace("FLOATS", fl).replace("COLOURS", co))
    rel = "%s/%s.mat" % (MAT_REL, name)
    ru.write(rel, mat)
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        ru.write(rel + ".meta", MAT_META.replace("GUID", ru.guid(rel)))
    print("RO_GLASS", name)
# glowing: its texture is also its emission, tinted amber and bright (HDR), for the bloom to take (the sun lamp,
# the user 11-19: "이렇게 태양 처럼")
EMISSIVE = {"RO_PlanetSun": (1.0 * 3.2, .62 * 3.2, .22 * 3.2)}


def texture(name, size):
    rel = "%s/%s_Albedo.png" % (TEX_REL, name)
    os.makedirs(os.path.join(ROOT, TEX_REL), exist_ok=True)
    shutil.copyfile(os.path.join(SRC, name + ".png"), os.path.join(ROOT, rel))
    meta = os.path.join(ROOT, rel + ".meta")
    if os.path.exists(meta):
        with open(meta, encoding="utf-8") as f:
            return f.read().split("\n")[1].split()[1]
    g = ru.guid(rel)
    ru.write(rel + ".meta", ru.texture_meta(g, False, size).replace("  nPOTScale: 1\n", "  nPOTScale: 0\n", 1))
    return g


def material(name, smooth, size, transparent):
    ga = texture(name, size)
    mat = ru.material(name, ga, None, 1.0, 0.0)
    swaps = [("  - _NORMALMAP\n  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n", "  - _SURFACE_TYPE_TRANSPARENT\n" if transparent else ""),
             ("    - _SmoothnessTextureChannel: 1\n", "    - _SmoothnessTextureChannel: 0\n"),
             ("    - _Smoothness: 1\n", "    - _Smoothness: %g\n" % smooth)]
    if transparent:
        swaps += [("    - _Cull: 2\n", "    - _Cull: 0\n"),
                  ("  m_CustomRenderQueue: -1\n", "  m_CustomRenderQueue: 3000\n"),
                  ("    RenderType: Opaque\n", "    RenderType: Transparent\n"),
                  ("  disabledShaderPasses:\n  - MOTIONVECTORS\n",
                   "  disabledShaderPasses:\n  - MOTIONVECTORS\n  - DepthOnly\n  - SHADOWCASTER\n"),
                  ("    - _DstBlend: 0\n", "    - _DstBlend: 10\n"), ("    - _DstBlendAlpha: 0\n", "    - _DstBlendAlpha: 10\n"),
                  ("    - _SrcBlend: 1\n", "    - _SrcBlend: 5\n"), ("    - _Surface: 0\n", "    - _Surface: 1\n"),
                  ("    - _ZWrite: 1\n", "    - _ZWrite: 0\n")]
    for a, b in swaps:
        assert mat.count(a) == 1, (name, a)
        mat = mat.replace(a, b)
    if name in EMISSIVE:
        r, g, b_ = EMISSIVE[name]
        for old, new in (("    - _EmissionMap:\n        m_Texture: {fileID: 0}\n",
                          "    - _EmissionMap:\n        m_Texture: {fileID: 2800000, guid: %s, type: 3}\n" % ga),
                         ("    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}\n",
                          "    - _EmissionColor: {r: %g, g: %g, b: %g, a: 1}\n" % (r, g, b_)),
                         ("  m_LightmapFlags: 4\n", "  m_LightmapFlags: 2\n")):
            assert mat.count(old) == 1, (name, old)
            mat = mat.replace(old, new)
        if "  m_ValidKeywords: []\n" in mat:
            mat = mat.replace("  m_ValidKeywords: []\n", "  m_ValidKeywords:\n  - _EMISSION\n")
        else:
            mat = mat.replace("  m_ValidKeywords:\n", "  m_ValidKeywords:\n  - _EMISSION\n", 1)
    rel = "%s/%s.mat" % (MAT_REL, name)
    ru.write(rel, mat)
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        ru.write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                 % ru.guid(rel))
    print("RO_MAT", name, "smoothness", smooth, "transparent" if transparent else "")


if __name__ == "__main__":
    ru.folder_meta("Assets/Art/RoyalObservatory")
    ru.folder_meta(TEX_REL)
    for n, (sm, size, tr) in PLANETS.items():
        material(n, sm, size, tr)
    for n, (tex, fl, co) in GLASS.items():
        glass_material(n, tex, fl, co)
