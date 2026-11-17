# R686 (seoungtest): the carriage yard's material library into Unity, without the editor.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I textures/rcy_unity_materials.py [names]
#
# Reads textures/library (rcy_textures.py's output: <Name>_Albedo.png with smoothness in alpha, <Name>_Normal.png and
# rcy_textures.json) and writes, with deterministic GUIDs (as export_rcy._guid / Tools/Generators/mkmeta.py):
#   Assets/Art/PawnRushCarriageYard/Textures/<Name>_Albedo.png / _Normal.png + .meta (sRGB / normal map, max size by
#       SIZES: the big surfaces 1024, the small ones 512, mipmaps, trilinear, aniso 4)
#   Assets/Resources/PawnRushCarriageYard/Materials/<Name>.mat + .meta (URP Lit: base map + normal map, smoothness from
#       the albedo's alpha, metallic per material, tiling = 1 / the tile's metres - the models' UVs are in metres)
# RoyalCarriageYard.Dressing.cs finds a slot's material by name ("RCY | stone" -> Materials/RCY_Stone). Never run while
# Unity is in Play (it re-imports what changes). Existing .meta files are kept (their GUIDs stay).

import hashlib, json, os, shutil, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.environ.get("RCY_UNITY_ROOT") or os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))   # RCY_UNITY_ROOT: write elsewhere (Unity in Play)
LIB = os.path.join(HERE, "library")
TEX_REL = "Assets/Art/PawnRushCarriageYard/Textures"
MAT_REL = "Assets/Resources/PawnRushCarriageYard/Materials"
LIT_GUID = "933532a4fcc9baf4fa0491de14d08ed7"   # URP Lit
# R686 "판석마다 짙은 돌 섞기": the stone materials on the yard's tinted Lit (rcy_tint_shader.py: URP Lit times the
# vertex colour rcy_kit writes per stone part).
TINT_GUID = hashlib.md5(b"chessfight:Assets/Art/PawnRushCarriageYard/Shaders/RcyTintLit.shader").hexdigest()
TINTED = {"RCY_Flag", "RCY_Stone", "RCY_Shade"}
BIG = {"RCY_Stone", "RCY_Flag", "RCY_Shade", "RCY_Marble", "RCY_Tile", "RCY_Oak", "RCY_Walnut", "RCY_Timber", "RCY_Soil"}


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def folder_meta(rel):
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                             "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(rel))


def platform(target, size):
    return ("  - serializedVersion: 4\n    buildTarget: %s\n    maxTextureSize: %d\n    resizeAlgorithm: 0\n"
            "    textureFormat: -1\n    textureCompression: 2\n    compressionQuality: 50\n    crunchedCompression: 0\n"
            "    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n"
            "    forceMaximumCompressionQuality_BC6H_BC7: 0\n" % (target, size))


def texture_meta(g, normal, size):
    return ("fileFormatVersion: 2\nguid: %s\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n"
            "  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 1\n    sRGBTexture: %d\n"
            "    linearTexture: 0\n    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n"
            "    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n  bumpmap:\n"
            "    convertToNormalMap: 0\n    externalNormalMap: 0\n    heightScale: 0.25\n    normalMapFilter: 0\n"
            "    flipGreenChannel: 0\n  isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0\n  vTOnly: 0\n"
            "  ignoreMipmapLimit: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  cubemapConvolution: 0\n"
            "  seamlessCubemap: 0\n  textureFormat: 1\n  maxTextureSize: %d\n  textureSettings:\n    serializedVersion: 2\n"
            "    filterMode: 2\n    aniso: 4\n    mipBias: 0\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n  nPOTScale: 1\n"
            "  lightmap: 0\n  compressionQuality: 50\n  spriteMode: 0\n  spriteExtrude: 1\n  spriteMeshType: 1\n"
            "  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n"
            "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 1\n  alphaUsage: 1\n"
            "  alphaIsTransparency: 0\n  spriteTessellationDetail: -1\n  textureType: %d\n  textureShape: 1\n"
            "  singleChannelComponent: 0\n  flipbookRows: 1\n  flipbookColumns: 1\n  maxTextureSizeSet: 0\n"
            "  compressionQualitySet: 0\n  textureFormatSet: 0\n  ignorePngGamma: 0\n  applyGammaDecoding: 0\n"
            "  swizzle: 50462976\n  cookieLightType: 0\n  platformSettings:\n%s%s  spriteSheet:\n    serializedVersion: 2\n"
            "    sprites: []\n    outline: []\n    customData: \n    physicsShape: []\n    bones: []\n    spriteID: \n"
            "    internalID: 0\n    vertices: []\n    indices: \n    edges: []\n    weights: []\n    secondaryTextures: []\n"
            "    spriteCustomMetadata:\n      entries: []\n    nameFileIdTable: {}\n  mipmapLimitGroupName: \n"
            "  pSDRemoveMatte: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
            % (g, 0 if normal else 1, size, 1 if normal else 0, platform("DefaultTexturePlatform", size), platform("Standalone", size)))


def tex_env(name, g=None, scale=1.0):
    t = "{fileID: 2800000, guid: %s, type: 3}" % g if g else "{fileID: 0}"
    return "    - %s:\n        m_Texture: %s\n        m_Scale: {x: %g, y: %g}\n        m_Offset: {x: 0, y: 0}\n" % (name, t, scale, scale)


def material(name, albedo, normal, tile, metallic, shader=LIT_GUID):
    s = 1.0 / tile
    envs = "".join([tex_env("_BaseMap", albedo, s), tex_env("_BumpMap", normal, s), tex_env("_DetailAlbedoMap"),
                    tex_env("_DetailMask"), tex_env("_DetailNormalMap"), tex_env("_EmissionMap"), tex_env("_MainTex", albedo, s),
                    tex_env("_MetallicGlossMap"), tex_env("_OcclusionMap"), tex_env("_ParallaxMap"), tex_env("_SpecGlossMap")])
    floats = {"_AlphaClip": 0, "_Blend": 0, "_BlendModePreserveSpecular": 1, "_BumpScale": 1, "_Cull": 2, "_Cutoff": .5,
              "_DstBlend": 0, "_DstBlendAlpha": 0, "_EnvironmentReflections": 1, "_GlossyReflections": 1, "_Metallic": metallic,
              "_OcclusionStrength": 1, "_QueueOffset": 0, "_ReceiveShadows": 1, "_Smoothness": 1, "_SmoothnessTextureChannel": 1,
              "_SpecularHighlights": 1, "_SrcBlend": 1, "_SrcBlendAlpha": 1, "_Surface": 0, "_WorkflowMode": 1, "_ZWrite": 1}
    return ("%%YAML 1.1\n%%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_Name: %s\n  m_Shader: {fileID: 4800000, guid: %s, type: 3}\n  m_Parent: {fileID: 0}\n"
            "  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords:\n  - _NORMALMAP\n  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n"
            "  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n"
            "  m_CustomRenderQueue: -1\n  stringTagMap:\n    RenderType: Opaque\n  disabledShaderPasses:\n  - MOTIONVECTORS\n"
            "  m_LockedProperties: \n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n%s    m_Ints: []\n"
            "    m_Floats:\n%s    m_Colors:\n    - _BaseColor: {r: 1, g: 1, b: 1, a: 1}\n    - _Color: {r: 1, g: 1, b: 1, a: 1}\n"
            "    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}\n    - _SpecColor: {r: 0.2, g: 0.2, b: 0.2, a: 1}\n"
            "  m_BuildTextureStacks: []\n  m_AllowLocking: 1\n"
            % (name, shader, envs, "".join("    - %s: %g\n" % kv for kv in sorted(floats.items()))))


def main(names):
    with open(os.path.join(LIB, "rcy_textures.json"), encoding="utf-8") as f:
        info = json.load(f)
    for rel in ("Assets/Art/PawnRushCarriageYard", TEX_REL, MAT_REL):
        os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
        folder_meta(rel)
    for name in names or sorted(info):
        spec = info[name]
        size = 1024 if name in BIG else 512
        gs = {}
        for kind in ("Albedo", "Normal"):
            rel = "%s/%s_%s.png" % (TEX_REL, name, kind)
            shutil.copyfile(os.path.join(LIB, "%s_%s.png" % (name, kind)), os.path.join(ROOT, rel))
            gs[kind] = guid(rel)
            if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
                write(rel + ".meta", texture_meta(gs[kind], kind == "Normal", size))
            else:
                with open(os.path.join(ROOT, rel + ".meta"), encoding="utf-8") as f:
                    gs[kind] = f.read().split("\n")[1].split()[1]
        rel = "%s/%s.mat" % (MAT_REL, name)
        write(rel, material(name, gs["Albedo"], gs["Normal"], spec["tile"], spec["metallic"],
                            TINT_GUID if name in TINTED else LIT_GUID))
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                 "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid(rel))
        print("RCY_MAT", name, spec["slot"], "tile", spec["tile"], "max", size)


if __name__ == "__main__":
    main([a for a in sys.argv[1].split(",") if a] if len(sys.argv) > 1 else [])
