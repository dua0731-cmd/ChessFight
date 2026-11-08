# Mini-game FBX export (R501), the garden's export_concept.py made a function so one build script can write several
# FBX files (a section's static dressing and the moving parts that ride the game's own transforms).
#
#   export(collection, "Assets/Resources/PawnRushMiniGames/Early/C_Drawbridge_Env.fbx")
#
# The FBX settings and the .meta are the garden's (axis -Z forward / Y up; Unity hangs the model under a pivot turned
# 180 degrees about y, so Blender x, y, z = Unity x, z, y of that pivot's parent). Materials are the phase-1 kit's:
# one URP Lit .mat per Blender material in Assets/Resources/PawnRushGardenConcept/Materials, by name ("GC | c slate"
# -> GC_c_slate). An existing .mat is never rewritten (the garden's are tuned in Unity), nor an existing texture.
import bpy, hashlib, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
GARDEN = os.path.normpath(os.path.join(HERE, "..", "..", "GardenConcept"))
KIT_REL = "Assets/Resources/PawnRushGardenConcept"
LIT = "933532a4fcc9baf4fa0491de14d08ed7"  # URP Lit
FBX_META = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
TEX_META = os.path.join(ROOT, "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_BannerNavy_BaseColor.png.meta")

FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
NATIVE = "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def folder(rel):
    """Makes rel and every missing parent folder, each with a folder .meta."""
    parts = rel.split("/")
    for i in range(2, len(parts) + 1):
        sub = "/".join(parts[:i])
        os.makedirs(os.path.join(ROOT, sub), exist_ok=True)
        if not os.path.exists(os.path.join(ROOT, sub + ".meta")):
            write(sub + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(sub), FOLDER))


def texture(fname):
    rel = KIT_REL + "/Textures/" + fname
    if not os.path.exists(os.path.join(ROOT, rel)):
        shutil.copyfile(os.path.join(GARDEN, "textures", fname), os.path.join(ROOT, rel))
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        with open(TEX_META, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + guid(rel)
        write(rel + ".meta", "\n".join(lines))
    with open(os.path.join(ROOT, rel + ".meta"), encoding="utf-8") as f:
        return f.read().split("\n")[1].split(":")[1].strip()


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


def materials(objects):
    folder(KIT_REL + "/Materials")
    folder(KIT_REL + "/Textures")
    done = set()
    for ob in objects:
        for m in ob.data.materials:
            if m is None or m.name in done:
                continue
            done.add(m.name)
            name = unity_name(m.name)
            rel = KIT_REL + "/Materials/" + name + ".mat"
            if os.path.exists(os.path.join(ROOT, rel)):
                print("MAT kept", name)
                continue
            p = m.node_tree.nodes["Principled BSDF"]
            textured = "gc_texture" in m
            r, g, b = list(m["gc_tint"]) if textured else to_srgb(list(p.inputs["Base Color"].default_value)[:3])
            alpha = p.inputs["Alpha"].default_value
            emit = p.inputs["Emission Strength"].default_value
            er, eg, eb = [v * emit for v in (r, g, b)]
            see_through = alpha < .99
            keywords = (["_EMISSION"] if emit > 0 else []) + (["_SURFACE_TYPE_TRANSPARENT"] if see_through else [])
            write(rel, MAT.format(
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
                er="%.4f" % er, eg="%.4f" % eg, eb="%.4f" % eb))
            if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
                write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(rel), NATIVE))
            print("MAT", name)


def export(collection, fbx_rel):
    folder(os.path.dirname(fbx_rel))
    bpy.ops.object.select_all(action="DESELECT")
    objects = list(collection.objects)
    total = 0
    for ob in objects:
        ob.select_set(True)
        total += sum(len(p.vertices) - 2 for p in ob.data.polygons)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, fbx_rel), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    if not os.path.exists(os.path.join(ROOT, fbx_rel + ".meta")):
        with open(FBX_META, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + guid(fbx_rel)
        write(fbx_rel + ".meta", "\n".join(lines))
    materials(objects)
    print("EXPORT %s  %d objects  %d tris" % (fbx_rel, len(objects), total))
