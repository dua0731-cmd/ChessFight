# Writes what Unity needs of the water tiers besides the meshes (R440), all from water_tiers.py:
#   Assets/Art/PawnRushMap/Resources/PR_GardenFallUpper.mat, PR_GardenFallLower.mat
#       the fall shader (ChessFight/Garden Fall Sheet) for the spillway's and the weirs' falling water: _Speed = V_REF
#       (the meshes' UV v is V_REF x seconds of travel, so the streaks move with the water) and _FallHeight = the drop
#       from the brink to the pool (the shader finds the foot from the vertex data, not from a world height)
#   Assets/Art/PawnRushMap/Resources/PR_GardenSplash.mat (R442)
#       the splashes' water (GardenSplashFx), URP Lit see-through, both sides
#   Assets/Resources/PawnRushGardenConcept/GardenWater_Landings.json
#       each bay's landing line (section frame) and its pool's height, for the water shader's churn (GardenFallLines)
# and a .meta for each new file (deterministic GUIDs, as export_concept.py).
#
#   "<Blender 5.2>/5.2/python/bin/python.exe" export_water.py      (or blender -b --python export_water.py)
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import water_tiers as wt  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
EDGE = .05     # the crest coping's overhang past each face (as water_sample_v1.py)


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def meta(rel, body):
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(rel), body))


NATIVE = "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
TEXT = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
FALL_SHADER = "fbbfe418cfaab19f56fb96fb3fb842fd"   # PR_GardenFallSheet.shader

MAT = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Aeration: {aeration}
    - _FallHeight: {fall_height}
    - _FootHeight: {foot_height}
    - _Refraction: 0.03
    - _Speed: {speed}
    - _Wobble: {wobble}
    m_Colors:
    - _FoamColor: {{r: 0.92, g: 0.97, b: 1, a: 1}}
    - _PoolTint: {{r: 0.66, g: 0.86, b: 1, a: 1}}
    - _Tint: {{r: 0.8, g: 0.95, b: 1, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""

Q, FALLS = wt.solve()
# The spillway's 2.2 m fall takes in more air and frays more than the weirs' 0.8 m; its foot blends over 0.55 m, the
# weirs' over 0.3 m (most of their short drop stays clear).
for name, fall, aeration, foot, wobble in (("PR_GardenFallUpper", FALLS["spillway"], .6, .55, .025),
                                          ("PR_GardenFallLower", FALLS["weir E"], .42, .3, .015)):
    rel = "Assets/Art/PawnRushMap/Resources/%s.mat" % name
    write(rel, MAT.format(name=name, shader=FALL_SHADER, aeration=aeration, fall_height="%.4f" % fall["H"],
                          foot_height=foot, speed=wt.V_REF, wobble=wobble))
    meta(rel, NATIVE)
    print("MAT", name, "drop %.4f" % fall["H"])

# R442: the splashes' water (GardenSplashFx: the crown, the drops, the jet) - URP Lit, see-through, glossy, both sides
# (a crown's inside shows through it); GardenSplashFx sets each part's alpha through _BaseColor.
SPLASH = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: PR_GardenSplash
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - _RECEIVE_SHADOWS_OFF
  - _SURFACE_TYPE_TRANSPARENT
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap:
    RenderType: Transparent
  disabledShaderPasses:
  - MOTIONVECTORS
  - DepthOnly
  - SHADOWCASTER
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _Cull: 0
    - _DstBlend: 10
    - _DstBlendAlpha: 10
    - _EnvironmentReflections: 1
    - _Metallic: 0
    - _QueueOffset: 1
    - _ReceiveShadows: 0
    - _Smoothness: 0.92
    - _SpecularHighlights: 1
    - _SrcBlend: 5
    - _SrcBlendAlpha: 1
    - _Surface: 1
    - _WorkflowMode: 1
    - _ZWrite: 0
    m_Colors:
    - _BaseColor: {r: 0.86, g: 0.95, b: 1, a: 0.7}
    - _Color: {r: 0.86, g: 0.95, b: 1, a: 0.7}
    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
--- !u!114 &7772292899409006985
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}
  m_Name:
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
"""
rel = "Assets/Art/PawnRushMap/Resources/PR_GardenSplash.mat"
write(rel, SPLASH)
meta(rel, NATIVE)
print("MAT PR_GardenSplash")

lines = []
for name, f in FALLS.items():
    z = f["z_down"] - EDGE - f["throw"] if f["way"] < 0 else f["z_down"] + EDGE + f["throw"]
    for a, b in f["spans"]:
        lines.append({"fall": name, "x0": round(a, 4), "z0": round(z, 4), "x1": round(b, 4), "z1": round(z, 4),
                      "y": f["lower"], "strength": round(min(1.0, .45 + .25 * f["H"]), 3)})
data = {"source": "ArtSource/PawnRush/GardenConcept/water_tiers.py (R439, R440)", "early": wt.EARLY, "middle": wt.MIDDLE,
        "late": wt.LATE, "discharge": round(Q, 4), "lines": lines}
rel = "Assets/Resources/PawnRushGardenConcept/GardenWater_Landings.json"
write(rel, json.dumps(data, indent=2) + "\n")
meta(rel, TEXT)
print("LANDINGS", len(lines), "lines, Q %.3f" % Q)
