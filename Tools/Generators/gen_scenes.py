# Generates Intro/KingRush/RagdollTest scenes, the obstacle/character prefabs and
# course materials as hand-written Unity YAML, with deterministic GUIDs.
#
# !! It OVERWRITES those files. Once anyone has saved them from the Unity Editor,
# !! running this throws their edits away. It is kept to show how the 2026-09-24
# !! assets were made and to rebuild them only when nobody has touched them.
# Run from the project root:  python3 Tools/Generators/gen_scenes.py --overwrite
import hashlib, os, re, math, sys

if "--overwrite" not in sys.argv:
    sys.exit("Refusing to run: this overwrites scenes and prefabs. Read the header, then pass --overwrite.")

HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
BUILTIN = "0000000000000000e000000000000000"
MESH = {"Cube": 10202, "Sphere": 10207, "Capsule": 10208}

def guid_for(path): return hashlib.md5(("chessfight:"+path).encode()).hexdigest()
def meta_guid(path): return re.search(r'guid: ([0-9a-f]{32})', open(path + ".meta").read()).group(1)
def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w").write(text)
    if not os.path.exists(path + ".meta"):
        open(path + ".meta", "w").write("fileFormatVersion: 2\nguid: %s\n" % guid_for(path))
    print("wrote", path)
def folder(path):
    os.makedirs(path, exist_ok=True)
    if not os.path.exists(path + ".meta"):
        open(path + ".meta", "w").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid_for(path))

SCRIPT = {n: meta_guid(p) for n, p in {
    "CameraRig": "Assets/Scripts/Game/CameraRig.cs",
    "GameSceneConfig": "Assets/Scripts/Game/GameSceneConfig.cs",
    "PawnAvatar": "Assets/Scripts/Game/PawnAvatar.cs",
    "PhysicsProfile": "Assets/Scripts/Gameplay/PhysicsProfile.cs",
    "PlaytestSpawner": "Assets/Scripts/Gameplay/Playtest/PlaytestSpawner.cs",
    "PlaytestCharacter": "Assets/Scripts/Gameplay/Playtest/PlaytestCharacter.cs",
    "SpawnPoint": "Assets/Scripts/Gameplay/Course/SpawnPoint.cs",
    "Checkpoint": "Assets/Scripts/Gameplay/Course/Checkpoint.cs",
    "FinishZone": "Assets/Scripts/Gameplay/Course/FinishZone.cs",
    "Spinner": "Assets/Scripts/Gameplay/Obstacles/Spinner.cs",
    "Oscillator": "Assets/Scripts/Gameplay/Obstacles/Oscillator.cs",
    "Pendulum": "Assets/Scripts/Gameplay/Obstacles/Pendulum.cs",
}.items()}
SHADER = meta_guid("Assets/Materials/NetworkColor.shader")

def v3(v): return "{x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in v)
def q4(q): return "{x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(c) for c in q)
def fmt(x):
    if isinstance(x, float): return ("%.7f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))
    return str(x)
def euler_x(deg): r = math.radians(deg) / 2; return (math.sin(r), 0.0, 0.0, math.cos(r))

# ----------------------------------------------------------- materials
def material(name, rgb):
    path = "Assets/Materials/%s.mat" % name
    write(path, HEAD + """--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %s
  m_Shader: {fileID: 4800000, guid: %s, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats: []
    m_Colors:
    - _Color: {r: %s, g: %s, b: %s, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocked: 1
""" % (name, SHADER, *rgb))
    return meta_guid(path)

MAT = {n: meta_guid("Assets/Materials/%s.mat" % n) for n in ("TeamBlue", "TeamOrange", "BoardDark", "BoardLight")}
MAT["Course"] = material("Course", (0.34, 0.40, 0.50))
MAT["CourseEdge"] = material("CourseEdge", (0.26, 0.31, 0.40))
MAT["Obstacle"] = material("Obstacle", (0.92, 0.36, 0.28))
MAT["Finish"] = material("Finish", (1.0, 0.80, 0.25))
MAT["Prop"] = material("Prop", (0.66, 0.54, 0.36))

# ----------------------------------------------------------- object graph
class Doc:
    """One YAML file. Allocates file IDs and keeps parent/child links consistent."""
    def __init__(self, base): self.next = base; self.blocks = []; self.roots = []
    def id(self): self.next += 1; return self.next
    def emit(self, text): self.blocks.append(text)

    def go(self, name, pos=(0, 0, 0), rot=(0, 0, 0, 1), scale=(1, 1, 1), parent=None, tag="Untagged"):
        g = {"go": self.id(), "tr": self.id(), "name": name, "comps": [], "kids": [], "tag": tag,
             "pos": pos, "rot": rot, "scale": scale, "parent": parent}
        g["comps"].append(g["tr"])
        if parent: parent["kids"].append(g)
        else: self.roots.append(g)
        self.all = getattr(self, "all", []) + [g]
        return g

    def comp(self, g, body):
        cid = self.id(); g["comps"].append(cid); self.emit((cid, g, body)); return cid

    def mesh(self, g, shape, mat):
        self.comp(g, ("33", "MeshFilter", "  m_Mesh: {fileID: %d, guid: %s, type: 0}\n" % (MESH[shape], BUILTIN)))
        self.comp(g, ("23", "MeshRenderer", RENDERER % mat))

    def box(self, g, size=(1, 1, 1), center=(0, 0, 0), trigger=False):
        return self.comp(g, ("65", "BoxCollider", COLLIDER_COMMON % (1 if trigger else 0) +
                             "  serializedVersion: 3\n  m_Size: %s\n  m_Center: %s\n" % (v3(size), v3(center))))

    def rigidbody(self, g, kinematic, mass=1):
        return self.comp(g, ("54", "Rigidbody", RIGIDBODY % (fmt(mass), 1, 1 if kinematic else 0, 1 if kinematic else 0)))

    def script(self, g, name, fields=""):
        return self.comp(g, ("114", "MonoBehaviour",
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n%s" % (SCRIPT[name], fields)))

    def raw(self, g, cls_id, cls, body):
        return self.comp(g, (cls_id, cls, body))

    def render(self, preamble="", scene=True):
        out = HEAD + preamble
        for g in self.all:
            comps = "\n".join("  - component: {fileID: %d}" % c for c in g["comps"])
            out += """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
%s
  m_Layer: 0
  m_Name: %s
  m_TagString: %s
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""" % (g["go"], comps, g["name"], g["tag"])
            kids = ("\n" + "\n".join("  - {fileID: %d}" % k["tr"] for k in g["kids"])) if g["kids"] else " []"
            out += """--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: %s
  m_LocalPosition: %s
  m_LocalScale: %s
  m_ConstrainProportionsScale: 0
  m_Children:%s
  m_Father: {fileID: %d}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
""" % (g["tr"], g["go"], q4(g["rot"]), v3(g["pos"]), v3(g["scale"]), kids,
       g["parent"]["tr"] if g["parent"] else 0)
            for cid, owner, (cls_id, cls, body) in [b for b in self.blocks if b[1] is g]:
                out += "--- !u!%s &%d\n%s:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" \
                       "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n%s" \
                       % (cls_id, cid, cls, g["go"], body)
        if scene:
            out += "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n" + \
                   "".join("  - {fileID: %d}\n" % r["tr"] for r in self.roots)
        return out.replace("  m_Children: []", "  m_Children: []")

RENDERER = """  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: %s, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {fileID: 0}
"""
COLLIDER_COMMON = """  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: %d
  m_ProvidesContacts: 0
  m_Enabled: 1
"""
RIGIDBODY = """  serializedVersion: 5
  m_Mass: %s
  m_LinearDamping: 0
  m_AngularDamping: 0.05
  m_CenterOfMass: {x: 0, y: 0, z: 0}
  m_InertiaTensor: {x: 1, y: 1, z: 1}
  m_InertiaRotation: {x: 0, y: 0, z: 0, w: 1}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ImplicitCom: 1
  m_ImplicitTensor: 1
  m_UseGravity: %d
  m_IsKinematic: %d
  m_Interpolate: %d
  m_Constraints: 0
  m_CollisionDetection: 0
"""
CHARACTER_CONTROLLER = COLLIDER_COMMON % 0 + """  serializedVersion: 3
  m_Height: 2
  m_Radius: 0.5
  m_SlopeLimit: 45
  m_StepOffset: 0.3
  m_SkinWidth: 0.08
  m_MinMoveDistance: 0.001
  m_Center: {x: 0, y: 0, z: 0}
"""
CAMERA = """  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: %s, g: %s, b: %s, a: 1}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {x: 2, y: 11}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 0
  orthographic size: 5
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
"""
# Light: Unity 6.3's own block from SampleScene, intensity toned down to the old arena's.
sample = open("Assets/Scenes/SampleScene.unity").read()
light_body = re.search(r'--- !u!108 &410087040\nLight:\n(.*?)(?=--- !u!)', sample, re.S).group(1)
light_body = light_body.split("  m_GameObject: {fileID: 410087039}\n", 1)[1].replace("  m_Intensity: 2\n", "  m_Intensity: 1.1\n")
PREAMBLE = sample[sample.index("--- !u!29 &1"):sample.index("--- !u!1 &330585543")]
AUDIO = "  m_Enabled: 1\n"

def camera_rig(doc, pos, overview, follow, pitch, bg=(0.07, 0.10, 0.17)):
    cam = doc.go("Main Camera", pos=pos, rot=euler_x(pitch), tag="MainCamera")
    cam_comp = doc.raw(cam, "20", "Camera", CAMERA % bg)
    doc.raw(cam, "81", "AudioListener", AUDIO)
    rig = doc.script(cam, "CameraRig",
        "  target: {fileID: %d}\n  overviewPosition: %s\n  followOffset: %s\n  pitch: %s\n  sharpness: 6\n"
        "  background: {r: %s, g: %s, b: %s, a: 1}\n" % (cam_comp, v3(overview), v3(follow), fmt(pitch), *bg))
    return cam, rig

def light(doc):
    g = doc.go("Directional Light", pos=(0, 3, 0), rot=(0.40821788, -0.23456968, 0.10938163, 0.8754261))
    doc.raw(g, "108", "Light", light_body)

def solid(doc, name, pos, scale, mat, parent=None, rot=(0, 0, 0, 1)):
    g = doc.go(name, pos=pos, rot=rot, scale=scale, parent=parent)
    doc.mesh(g, "Cube", MAT[mat]); doc.box(g)
    return g

def prefab_ref(path): return "{fileID: %d, guid: %s, type: 3}" % (PREFAB_ROOT[path], meta_guid(path))

# ----------------------------------------------------------- prefabs
PREFAB_ROOT = {}
folder("Assets/Prefabs/Characters"); folder("Assets/Prefabs/Obstacles")

def playtest_character():
    d = Doc(8100)
    root = d.go("PlaytestCharacter")
    d.mesh(root, "Capsule", MAT["TeamBlue"])
    d.raw(root, "143", "CharacterController", CHARACTER_CONTROLLER)
    d.script(root, "PlaytestCharacter")
    accent = d.go("Accent", pos=(0, 0.4, 0.45), scale=(0.3, 0.3, 0.3), parent=root)
    d.mesh(accent, "Sphere", MAT["TeamOrange"])
    path = "Assets/Prefabs/Characters/PlaytestCharacter.prefab"
    write(path, d.render(scene=False)); PREFAB_ROOT[path] = root["go"]

def spinner(d, name, pos, length, dps, phase=0.0, parent=None, prefab=False):
    g = d.go(name, pos=pos, scale=(length, 0.6, 0.6), parent=parent)
    d.mesh(g, "Cube", MAT["Obstacle"]); d.box(g); d.rigidbody(g, kinematic=True)
    d.script(g, "Spinner", "  phase: %s\n  axis: {x: 0, y: 1, z: 0}\n  degreesPerSecond: %s\n" % (fmt(phase), fmt(dps)))
    return g

def sliding_wall(d, name, pos, travel, period, phase=0.0, parent=None):
    g = d.go(name, pos=pos, scale=(4, 2, 1), parent=parent)
    d.mesh(g, "Cube", MAT["Obstacle"]); d.box(g); d.rigidbody(g, kinematic=True)
    d.script(g, "Oscillator", "  phase: %s\n  travel: %s\n  period: %s\n" % (fmt(phase), v3(travel), fmt(period)))
    return g

def pendulum(d, name, pos, axis, amplitude, period, arm, head, phase=0.0, parent=None):
    pivot = d.go(name, pos=pos, parent=parent)
    d.rigidbody(pivot, kinematic=True)
    d.script(pivot, "Pendulum", "  phase: %s\n  axis: %s\n  amplitude: %s\n  period: %s\n"
             % (fmt(phase), v3(axis), fmt(amplitude), fmt(period)))
    a = d.go("Arm", pos=(0, -arm / 2, 0), scale=(0.4, arm, 0.4), parent=pivot)
    d.mesh(a, "Cube", MAT["CourseEdge"]); d.box(a)
    h = d.go("Head", pos=(0, -(arm + head / 2), 0), scale=(head, head, head), parent=pivot)
    d.mesh(h, "Cube", MAT["Obstacle"]); d.box(h)
    return pivot

def obstacle_prefabs():
    for fname, build in [
        ("Spinner", lambda d: spinner(d, "Spinner", (0, 0, 0), 14, 90)),
        ("SlidingWall", lambda d: sliding_wall(d, "SlidingWall", (0, 0, 0), (5, 0, 0), 3)),
        ("Pendulum", lambda d: pendulum(d, "Pendulum", (0, 0, 0), (0, 0, 1), 55, 2.6, 7, 3)),
    ]:
        d = Doc(9100); g = build(d)
        path = "Assets/Prefabs/Obstacles/%s.prefab" % fname
        write(path, d.render(scene=False)); PREFAB_ROOT[path] = g["go"]

playtest_character()
obstacle_prefabs()
CHARACTER = "Assets/Prefabs/Characters/PlaytestCharacter.prefab"

# ----------------------------------------------------------- scenes
def intro():
    d = Doc(1000)
    camera_rig(d, (0, 1, -10), (0, 1, -10), (0, 1, -10), 0, bg=(0.035, 0.055, 0.094))
    write("Assets/Scenes/Intro.unity", d.render(PREAMBLE))

def king_rush():
    d = Doc(1000)
    cam, rig = camera_rig(d, (0, 16, -30), (0, 16, -30), (0, 6, -10), 25)
    light(d)
    root = d.go("ChessFight Game Root")
    pawn = "Assets/Prefabs/PawnAvatar.prefab"
    pawn_mb = int(re.search(r'--- !u!114 &(\d+)', open(pawn).read()).group(1))
    d.script(root, "GameSceneConfig",
        "  arenaPrefab: {fileID: 0}\n"
        "  pawnPrefab: {fileID: %d, guid: %s, type: 3}\n"
        "  blueTeamMaterial: {fileID: 2100000, guid: %s, type: 2}\n"
        "  orangeTeamMaterial: {fileID: 2100000, guid: %s, type: 2}\n"
        "  hudLayout: {fileID: 0}\n  hudTheme: {fileID: 0}\n  hudPanelSettings: {fileID: 0}\n"
        "  hudReferenceResolution: {x: 1280, y: 720}\n  ambientLight: {r: 0.55, g: 0.58, b: 0.65, a: 1}\n"
        % (pawn_mb, meta_guid(pawn), MAT["TeamBlue"], MAT["TeamOrange"]))
    d.script(root, "PhysicsProfile", "  physicsRate: 120\n  solverIterations: 24\n")

    course = d.go("Course")
    solid(d, "Start Platform", (0, -0.5, 0), (40, 1, 40), "BoardDark", course)
    solid(d, "Runway", (0, -0.5, 110), (16, 1, 180), "Course", course)
    solid(d, "Finish Platform", (0, -0.5, 215), (30, 1, 30), "Finish", course)

    spawns = d.go("Spawn Points", parent=course)
    first = None
    for i in range(12):
        team, col = divmod(i, 6)
        sp = d.go("Spawn %02d" % i, pos=(-10 + col * 4, 0, -12 - team * 4), parent=spawns)
        d.script(sp, "SpawnPoint", "  index: %d\n  team: %d\n" % (i, team))
        first = first or sp

    for order, z in ((1, 80), (2, 150)):
        cp = d.go("Checkpoint %d" % order, pos=(0, 2, z), parent=course)
        d.box(cp, size=(16, 4, 2), trigger=True)
        d.script(cp, "Checkpoint", "  order: %d\n" % order)
    fin = d.go("Finish Zone", pos=(0, 2, 215), parent=course)
    d.box(fin, size=(28, 4, 28), trigger=True)
    d.script(fin, "FinishZone")

    obstacles = d.go("Obstacles")
    spinner(d, "Spinner A", (0, 0.5, 50), 14, 90, parent=obstacles)
    sliding_wall(d, "Sliding Wall", (0, 1, 100), (5, 0, 0), 3, parent=obstacles)
    pendulum(d, "Pendulum", (0, 10, 130), (0, 0, 1), 55, 2.6, 7, 3, parent=obstacles)
    spinner(d, "Spinner B", (0, 0.5, 180), 14, -120, phase=0.5, parent=obstacles)

    play = d.go("Playtest")
    d.script(play, "PlaytestSpawner",
        "  characterPrefab: %s\n  spawnPoint: {fileID: %d}\n  cameraRig: {fileID: %d}\n  fallLimit: -20\n  showHelp: 1\n"
        % (prefab_ref(CHARACTER), first["tr"], rig))
    write("Assets/Scenes/KingRush.unity", d.render(PREAMBLE))

def ragdoll_test():
    d = Doc(1000)
    cam, rig = camera_rig(d, (0, 10, -24), (0, 10, -24), (0, 5, -8), 28)
    light(d)
    root = d.go("Physics")
    d.script(root, "PhysicsProfile", "  physicsRate: 120\n  solverIterations: 24\n")

    env = d.go("Environment")
    solid(d, "Floor", (0, -0.5, 0), (60, 1, 60), "BoardDark", env)
    solid(d, "Ramp 15deg", (10, 1.2, 6), (6, 0.4, 10), "Course", env, rot=euler_x(-15))
    solid(d, "Ledge 1m", (-10, 0.5, 8), (8, 1, 4), "Course", env)
    solid(d, "Wall 2m (solo climb)", (-10, 1, 14), (8, 2, 4), "Course", env)
    solid(d, "Wall 3m (needs a partner)", (-10, 1.5, 20), (8, 3, 1), "CourseEdge", env)
    solid(d, "Side Wall", (22, 1.5, 0), (0.5, 3, 20), "CourseEdge", env)

    props = d.go("Pushable Boxes")
    for i, p in enumerate([(4, 0.5, -4), (5.2, 0.5, -4), (4.6, 1.5, -4)]):
        b = d.go("Box %d" % (i + 1), pos=p, parent=props)
        d.mesh(b, "Cube", MAT["Prop"]); d.box(b); d.rigidbody(b, kinematic=False, mass=5)

    obstacles = d.go("Obstacles")
    spinner(d, "Spinner", (12, 0.5, -14), 8, 90, parent=obstacles)
    pendulum(d, "Pendulum", (-14, 7, -14), (1, 0, 0), 50, 2.4, 5, 2, parent=obstacles)

    spawn = d.go("Spawn", pos=(0, 0, -10))
    d.script(spawn, "SpawnPoint", "  index: 0\n  team: 0\n")
    play = d.go("Playtest")
    d.script(play, "PlaytestSpawner",
        "  characterPrefab: %s\n  spawnPoint: {fileID: %d}\n  cameraRig: {fileID: %d}\n  fallLimit: -20\n  showHelp: 1\n"
        % (prefab_ref(CHARACTER), spawn["tr"], rig))
    write("Assets/Scenes/RagdollTest.unity", d.render(PREAMBLE))

intro(); king_rush(); ragdoll_test()
