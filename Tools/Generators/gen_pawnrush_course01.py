# Generates the Pawn Rush course 01 assets that have to exist before Unity has opened them,
# as hand-written Unity YAML (same method as gen_queenhill_scene.py):
#
#   Assets/Scenes/PawnRush_Course01.unity       camera, light, physics rate, the course
#                                                        root (PawnRushCourse) and the offline
#                                                        playtest (ragdoll pawn, white team)
#   Assets/Settings/Course01Kit.asset                    materials + imported obstacle prefabs
#   Assets/Materials/*.mat              the course materials
#
# The course itself (v0.4, Course01v4Builder) is NOT in the scene file: Play builds it from code
# when the scene does not hold it, and the menu ChessFight > Pawn Rush > Build Course01 v4 builds
# it into the scene (Unity then saves the scene).
#
# !! It OVERWRITES these files. Once Unity has saved the scene (Build Course01 v4) or anyone has
# !! edited the kit or the materials, running this throws that work away.
# Run from the project root:  python3 Tools/Generators/gen_pawnrush_course01.py --overwrite
import hashlib, os, re, sys

SCENE = "Assets/Scenes/PawnRush_Course01.unity"
# Since the 10-08 folder cleanup the course has no folder of its own: scripts in
# Assets/Scripts/PawnRush, materials in Assets/Materials, the kit in Assets/Settings.
MATERIALS = "Assets/Materials"
if "--overwrite" not in sys.argv and os.path.exists(SCENE):
    sys.exit("Refusing to run: this overwrites " + SCENE + " and the course's data assets. Read the header, then pass --overwrite.")

HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"

def guid_for(path): return hashlib.md5(("chessfight:" + path).encode()).hexdigest()
def meta_guid(path): return re.search(r'guid: ([0-9a-f]{32})', open(path + ".meta").read()).group(1)
def write(path, text, meta_extra=""):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w").write(text)
    if not os.path.exists(path + ".meta"):
        open(path + ".meta", "w").write("fileFormatVersion: 2\nguid: %s\n%s" % (guid_for(path), meta_extra))
    print("wrote", path)
def fmt(x):
    if isinstance(x, float): return ("%.7f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))
    return str(x)
def v3(v): return "{x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in v)
def q4(q): return "{x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(c) for c in q)

def script(name, folder):
    return meta_guid("%s/%s.cs" % (folder, name))
PR = "Assets/Scripts/PawnRush"
SCRIPT = {
    "PawnRushCourse": script("PawnRushCourse", PR), "Course01Playtest": script("Course01Playtest", PR),
    "Course01Kit": script("Course01Kit", PR),
    "OrbitCamera": script("OrbitCamera", "Assets/Scripts/Game"),
    "PhysicsProfile": script("PhysicsProfile", "Assets/Scripts/Gameplay"),
    "PlaytestSpawner": script("PlaytestSpawner", "Assets/Scripts/Gameplay/Playtest"),
    "GameSceneConfig": script("GameSceneConfig", "Assets/Scripts/Game"),
}

def root_object(prefab):
    """fileID of a prefab's root GameObject."""
    text = open(prefab).read()
    for tid, body in re.findall(r'--- !u!4 &(\d+)\nTransform:(.*?)(?=--- !u!|\Z)', text, re.S):
        if "m_Father: {fileID: 0}" in body:
            return int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1))
    raise SystemExit("no root in " + prefab)
def prefab_ref(prefab): return "{fileID: %d, guid: %s, type: 3}" % (root_object(prefab), meta_guid(prefab))

# ------------------------------------------------------------------ materials
def material(name, shader_file, floats, colors):
    shader = meta_guid(MATERIALS + "/" + shader_file)
    body = HEAD + ("--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_Name: %s\n  m_Shader: {fileID: 4800000, guid: %s, type: 3}\n  m_Parent: {fileID: 0}\n"
            "  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n"
            "  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n"
            "  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n    serializedVersion: 3\n"
            "    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n%s    m_Colors:\n%s  m_BuildTextureStacks: []\n  m_AllowLocked: 1\n") % (
        name, shader,
        "".join("    - %s: %s\n" % (k, fmt(v)) for k, v in floats.items()),
        "".join("    - %s: {r: %s, g: %s, b: %s, a: %s}\n" % ((k,) + tuple(fmt(c) for c in v)) for k, v in colors.items()))
    path = "%s/%s.mat" % (MATERIALS, name)
    write(path, body)
    return "{fileID: 2100000, guid: %s, type: 2}" % meta_guid(path)

CREAM, BROWN = (0.93, 0.88, 0.76, 1.0), (0.55, 0.38, 0.24, 1.0)
def pattern(name, top, side, side_color, line_color, gloss=0.2, metal=0.0, top_colors=(CREAM, BROWN)):
    return material(name, "CoursePattern.shader",
                    {"_TopStyle": float(top), "_SideStyle": float(side), "_Cell": 2.0, "_Glossiness": gloss, "_Metallic": metal},
                    {"_Color": top_colors[0], "_Color2": top_colors[1], "_SideColor": side_color, "_LineColor": line_color})

MAT = {
    "floorChecker": pattern("Floor_Checker", 0, 0, (0.86, 0.79, 0.66, 1.0), (0.5, 0.42, 0.33, 1.0)),
    "wallClimbable": pattern("Wall_Climbable", 0, 1, (0.74, 0.66, 0.56, 1.0), (0.36, 0.31, 0.27, 1.0), 0.1),
    "wallNoClimb": pattern("Wall_NoClimb", 0, 2, (0.9, 0.91, 0.93, 1.0), (0.62, 0.64, 0.7, 1.0), 0.75),
    "glass": material("Glass_TeamDivider", "CourseGlass.shader", {"_Glossiness": 0.9},
                      {"_Color": (0.6, 0.82, 0.95, 0.18), "_BandColor": (0.85, 0.95, 1.0, 0.45)}),
    "trimWhite": pattern("Trim_White", 1, 0, (0.96, 0.96, 0.94, 1.0), (0.7, 0.7, 0.7, 1.0), 0.4),
    "trimBlack": pattern("Trim_Black", 1, 0, (0.12, 0.12, 0.14, 1.0), (0.3, 0.3, 0.32, 1.0), 0.4),
    "rankGold": pattern("Rank_Gold", 1, 0, (0.95, 0.74, 0.26, 1.0), (0.6, 0.45, 0.1, 1.0), 0.75, 0.7),
    "hazard": pattern("Hazard_Course", 1, 0, (0.86, 0.27, 0.2, 1.0), (0.4, 0.1, 0.1, 1.0), 0.35),
    # Mini-game boards and planks (A's squares, B's planks, C's drawbridge): plain cream, plain brown.
    "boardLight": pattern("Board_Light", 1, 0, CREAM, (0.7, 0.64, 0.52, 1.0), 0.25),
    "boardDark": pattern("Board_Dark", 1, 0, BROWN, (0.36, 0.24, 0.15, 1.0), 0.25),
}

# ------------------------------------------------------------------ data assets
def scriptable(path, script_name, name, fields):
    write(path, HEAD + ("--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
          "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n"
          "  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: \n%s"
          % (SCRIPT[script_name], name, fields)))
    return "{fileID: 11400000, guid: %s, type: 2}" % meta_guid(path)


OBS = "Assets/Prefabs/Obstacles/"
PREFABS = {
    "spinningDisc": "01_SpinningDisc", "jumpPad": "02_JumpPad", "pusher": "03_ReciprocatingPusher",
    "hammer": "04_RotatingHammer", "risingTiles": "05_RisingFallingTiles", "slidingWalls": "06_SlidingWalls",
    "conveyor": "08_ConveyorChessboard", "clockGates": "09_ChessClockGates", "fallingPiece": "10_FallingChessPiece",
    "pendulum": "11_PendulumBall", "foldingBridge": "12_FoldingBridge", "airVent": "15_AirVent",
    "knightCavalry": "17_KnightCavalryCharge",
}
kit_fields = "".join("  %s: %s\n" % (k, MAT[k]) for k in
                     ["floorChecker", "wallClimbable", "wallNoClimb", "glass", "trimWhite", "trimBlack", "rankGold",
                      "boardLight", "boardDark", "hazard"])
kit_fields += "".join("  %s: %s\n" % (k, prefab_ref(OBS + v + ".prefab")) for k, v in PREFABS.items())
kit = scriptable("Assets/Settings/Course01Kit.asset", "Course01Kit", "Course01Kit", kit_fields)

# ------------------------------------------------------------------ scene
class Doc:
    """One YAML file. Allocates file IDs and keeps parent/child links consistent."""
    def __init__(self, base): self.next = base; self.blocks = []; self.roots = []; self.all = []
    def id(self): self.next += 1; return self.next
    def go(self, name, pos=(0, 0, 0), rot=(0, 0, 0, 1), tag="Untagged"):
        g = {"go": self.id(), "tr": self.id(), "name": name, "comps": [], "tag": tag, "pos": pos, "rot": rot}
        g["comps"].append(g["tr"]); self.roots.append(g); self.all.append(g)
        return g
    def comp(self, g, cls_id, cls, body):
        cid = self.id(); g["comps"].append(cid); self.blocks.append((cid, g, cls_id, cls, body)); return cid
    def script(self, g, name, fields=""):
        return self.comp(g, "114", "MonoBehaviour",
            "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n%s" % (SCRIPT[name], fields))
    def render(self, preamble):
        out = HEAD + preamble
        for g in self.all:
            comps = "\n".join("  - component: {fileID: %d}" % c for c in g["comps"])
            out += ("--- !u!1 &%d\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                    "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n%s\n"
                    "  m_Layer: 0\n  m_Name: %s\n  m_TagString: %s\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
                    "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n") % (g["go"], comps, g["name"], g["tag"])
            out += ("--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                    "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n"
                    "  serializedVersion: 2\n  m_LocalRotation: %s\n  m_LocalPosition: %s\n  m_LocalScale: {x: 1, y: 1, z: 1}\n"
                    "  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n"
                    "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n") % (g["tr"], g["go"], q4(g["rot"]), v3(g["pos"]))
            for cid, owner, cls_id, cls, body in [b for b in self.blocks if b[1] is g]:
                out += ("--- !u!%s &%d\n%s:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n%s") \
                       % (cls_id, cid, cls, g["go"], body)
        out += "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n" + \
               "".join("  - {fileID: %d}\n" % r["tr"] for r in self.roots)
        return out

# The camera block, the light block and the scene settings come from the scenes Unity wrote.
kingrush = open("Tools/Generators/templates/KingRush.unity").read()
camera_body = re.search(r'--- !u!20 &\d+\nCamera:\n.*?m_GameObject: \{fileID: \d+\}\n(.*?)(?=--- !u!)', kingrush, re.S).group(1)
camera_body = re.sub(r'm_BackGroundColor: \{[^}]*\}', 'm_BackGroundColor: {r: 0.64, g: 0.79, b: 0.93, a: 1}', camera_body)
camera_body = camera_body.replace("far clip plane: 1000", "far clip plane: 1500")
sample = open("Tools/Generators/templates/SampleScene.unity").read()
light_body = re.search(r'--- !u!108 &410087040\nLight:\n(.*?)(?=--- !u!)', sample, re.S).group(1)
light_body = light_body.split("  m_GameObject: {fileID: 410087039}\n", 1)[1].replace("  m_Intensity: 2\n", "  m_Intensity: 1.2\n")
PREAMBLE = sample[sample.index("--- !u!29 &1"):sample.index("--- !u!1 &330585543")]

PAWN_PREFAB = "Assets/Prefabs/RagdollPawn.prefab"

d = Doc(3000)
cam = d.go("Main Camera", pos=(0, 6, -20), tag="MainCamera")
d.comp(cam, "20", "Camera", camera_body)
d.comp(cam, "81", "AudioListener", "  m_Enabled: 1\n")
orbit = d.script(cam, "OrbitCamera",
    "  distance: 6\n  minDistance: 1.5\n  maxDistance: 16\n  minPitch: -40\n  maxPitch: 75\n  mouseSensitivity: 2.2\n"
    "  lookHeight: 0.6\n  shoulder: 0.45\n  followTime: 0.1\n  heightTime: 0.25\n  collisionRadius: 0.2\n"
    "  flySpeed: 12\n  flyFastSpeed: 45\n")
light = d.go("Directional Light", pos=(0, 30, 0), rot=(0.40821788, -0.23456968, 0.10938163, 0.8754261))
d.comp(light, "108", "Light", light_body)
physics = d.go("Physics Profile")
d.script(physics, "PhysicsProfile", "  physicsRate: 120\n  solverIterations: 24\n")
course = d.go("Pawn Rush Course01")
course_id = d.script(course, "PawnRushCourse", "  kit: %s\n  buildOnPlayIfEmpty: 1\n" % kit)
play = d.go("Playtest", pos=(0, 0, -7))
spawner = d.script(play, "PlaytestSpawner",
    "  characterPrefab: %s\n  spawnPoint: {fileID: 0}\n  cameraRig: {fileID: 0}\n"
    "  orbitCamera: {fileID: %d}\n  team: 0\n  fallLimit: -40\n  fallCatchSpeed: 0\n  showHelp: 1\n"
    % (prefab_ref(PAWN_PREFAB), orbit))
d.script(play, "Course01Playtest", "  spawner: {fileID: %d}\n  course: {fileID: %d}\n  teamSize: 6\n  writeCsv: 1\n" % (spawner, course_id))
# The lobby's 폰 러쉬 match opens this scene: the Steam link (SteamPawnRushLink, R92) runs it, Esc leaves.
match = d.go("ChessFight Game Root")
d.script(match, "GameSceneConfig", "  customMatchSimulation: 1\n  escapeLeavesMatch: 1\n")
write(SCENE, d.render(PREAMBLE))
