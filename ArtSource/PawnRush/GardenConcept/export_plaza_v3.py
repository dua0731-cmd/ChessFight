# Exports the plaza sample v3's dressing for Unity (R412, R415: stone and wood textures, tints and tiling from the gc_ keys).
#
#   blender -b --python export_plaza_v3.py
#
# Writes, under Assets/Resources/PawnRushGardenConcept/:
#   GardenPlaza.fbx                 every object of the "Plaza dressing" collection (no colliders: the v31 blockout keeps
#                                   colliding), with the garden FBX's own settings (axis -Z forward / Y up; Unity hangs it
#                                   under a pivot turned 180 degrees about y, so Blender x, y, z = section x, z, y)
#   Materials/GC_*.mat              one URP Lit material per Blender material, by name ("GC | cream stone" -> GC_cream_stone)
#   Textures/Herringbone_PinkBeige.png, Stone_Cream.png, Wood_Grain.png
# and a .meta for each new file and folder (deterministic GUIDs, as Tools/Generators/mkmeta.py).

import bpy, hashlib, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT_REL = "Assets/Resources/PawnRushGardenConcept"
OUT = os.path.join(ROOT, OUT_REL)
LIT = "933532a4fcc9baf4fa0491de14d08ed7"  # URP Lit
GARDEN_FBX_META = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")

bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "PlazaSample_v3.blend"))


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def meta(rel, body=""):
    path = os.path.join(ROOT, rel + ".meta")
    if os.path.exists(path):
        return
    write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(rel), body))


FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
NATIVE = "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

for rel in (OUT_REL, OUT_REL + "/Materials", OUT_REL + "/Textures"):
    os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
    meta(rel, FOLDER)

# ------------------------------------------------------------------ the textures (v3: herringbone, stone, wood)
TEMPLATE_META = os.path.join(ROOT, "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_BannerNavy_BaseColor.png.meta")
TEX_GUIDS = {}


def texture(fname):
    """Copies textures/<fname> into Unity once per run, with a 2D colour importer .meta (a .meta with only a GUID
    imports as a cube map: PITFALLS 35, R330, R412)."""
    if fname in TEX_GUIDS:
        return TEX_GUIDS[fname]
    rel = OUT_REL + "/Textures/" + fname
    shutil.copyfile(os.path.join(HERE, "textures", fname), os.path.join(ROOT, rel))
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        with open(TEMPLATE_META, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + guid(rel)
        lines = ["    aniso: 8" if l.startswith("    aniso: ") else l for l in lines]
        write(rel + ".meta", "\n".join(lines))
    TEX_GUIDS[fname] = guid(rel)
    return TEX_GUIDS[fname]

# ------------------------------------------------------------------ the mesh
dress = bpy.data.collections["Plaza dressing"]
bpy.ops.object.select_all(action="DESELECT")
total = 0
for ob in dress.objects:
    ob.select_set(True)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    total += tris
    print("OBJ %-28s %8d tris  %s" % (ob.name, tris, [m.name for m in ob.data.materials]))
print("TOTAL TRIS", total)
bpy.context.view_layer.objects.active = dress.objects[0]
fbx_rel = OUT_REL + "/GardenPlaza.fbx"
bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, fbx_rel), use_selection=True, object_types={"MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                         bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                         path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
if not os.path.exists(os.path.join(ROOT, fbx_rel + ".meta")):
    with open(GARDEN_FBX_META, encoding="utf-8") as f:
        body = f.read()
    lines = body.split("\n")
    lines[1] = "guid: " + guid(fbx_rel)
    write(fbx_rel + ".meta", "\n".join(lines))

# ------------------------------------------------------------------ the materials


def to_srgb(c):
    return [(12.92 * v) if v <= .0031308 else (1.055 * v ** (1 / 2.4) - .055) for v in c]


def unity_name(name):
    return name.replace(" | ", "_").replace(" ", "_")



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
  m_Shader: {{fileID: 4800000, guid: {lit}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:{keywords}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap:
    RenderType: {rtype}
  disabledShaderPasses:
  - MOTIONVECTORS{extra_pass}
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {tex}
        m_Scale: {{x: {tile}, y: {tile}}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {tex}
        m_Scale: {{x: {tile}, y: {tile}}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _Cull: 2
    - _DstBlend: {dst}
    - _DstBlendAlpha: {dst}
    - _EnvironmentReflections: 1
    - _Metallic: {metal}
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {smooth}
    - _SpecularHighlights: 1
    - _SrcBlend: {src}
    - _SrcBlendAlpha: 1
    - _Surface: {surface}
    - _WorkflowMode: 1
    - _ZWrite: {zwrite}
    m_Colors:
    - _BaseColor: {{r: {r}, g: {g}, b: {b}, a: {a}}}
    - _Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
    - _EmissionColor: {{r: {er}, g: {eg}, b: {eb}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
--- !u!114 &7772292899409006985
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
"""

done = set()
for ob in dress.objects:
    for m in ob.data.materials:
        if m is None or m.name in done:
            continue
        done.add(m.name)
        p = m.node_tree.nodes["Principled BSDF"]
        textured = "gc_texture" in m
        r, g, b = list(m["gc_tint"]) if textured else to_srgb(list(p.inputs["Base Color"].default_value)[:3])
        alpha = p.inputs["Alpha"].default_value
        emit = p.inputs["Emission Strength"].default_value
        er, eg, eb = [v * emit for v in (r, g, b)]
        see_through = alpha < .99
        keywords = []
        if emit > 0:
            keywords.append("_EMISSION")
        if see_through:
            keywords.append("_SURFACE_TYPE_TRANSPARENT")
        name = unity_name(m.name)
        text = MAT.format(
            name=name, lit=LIT,
            keywords="".join("\n  - " + k for k in keywords) if keywords else " []",
            queue=3000 if see_through else -1, rtype="Transparent" if see_through else "Opaque",
            extra_pass="\n  - DepthOnly\n  - SHADOWCASTER" if see_through else "",
            tex=("{fileID: 2800000, guid: %s, type: 3}" % texture(m["gc_texture"])) if textured else "{fileID: 0}",
            tile="%.3f" % (m["gc_tiling"] if textured else 1.0),
            dst=10 if see_through else 0, src=5 if see_through else 1, surface=1 if see_through else 0,
            zwrite=0 if see_through else 1,
            metal="%.3f" % p.inputs["Metallic"].default_value,
            smooth="%.3f" % (1 - p.inputs["Roughness"].default_value),
            r="%.4f" % r, g="%.4f" % g, b="%.4f" % b, a="%.3f" % alpha,
            er="%.4f" % er, eg="%.4f" % eg, eb="%.4f" % eb)
        rel = OUT_REL + "/Materials/" + name + ".mat"
        write(rel, text)
        meta(rel, NATIVE)
        print("MAT", name)
print("EXPORT DONE", fbx_rel)
