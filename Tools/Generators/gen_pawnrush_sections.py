# Generates the Pawn Rush section assets that have to exist before Unity has opened them, as hand-written
# Unity YAML (the same method as gen_pawnrush_course01.py):
#
#   Assets/Materials/Section_*.mat                         the section blockout look: 1 m grid, blue floor, grey
#                                                          unclimbable, orange obstacles, sky-blue rest spots,
#                                                          dark red respawn floor (CoursePattern grid styles)
#   Assets/Settings/PawnRushSectionKit.asset               those materials + the imported obstacle prefabs
#   Assets/Prefabs/PawnRushSections/Mid_2-1_GrandStairHall.prefab
#                                                          the grand stair hall's ROOT only (GrandStairHall +
#                                                          PawnRushSection: id, entry z -12, exit (0, 19, 132))
#   Assets/Settings/PawnRushSections.asset                 the round's seven slots; the hall in slot 3 (중반 장애물)
#   Assets/Scenes/PawnRush_Sections.unity                  camera, light, physics rate, the round (PawnRushRound)
#                                                          and the offline playtest (ragdoll pawn, white team)
#
# The hall itself is NOT in the prefab file: the prefab builds it from code when it wakes (GrandStairHallBuilder),
# and the menu ChessFight > Pawn Rush > Build Mid 2-1 Grand Stair Hall bakes it into the prefab (Unity then saves
# the prefab with the links to the obstacle prefabs). AI tools without Unity do not write nested prefab YAML
# (DECISIONS P1).
#
# !! It OVERWRITES these files. Once Unity has baked the prefab or saved the scene, or anyone has edited the
# !! kit, the set or the materials, running this throws that work away.
# Run from the project root:  python3 Tools/Generators/gen_pawnrush_sections.py --overwrite
import hashlib, os, re, sys

PREFAB = "Assets/Prefabs/PawnRushSections/Mid_2-1_GrandStairHall.prefab"
SCENE = "Assets/Scenes/PawnRush_Sections.unity"
MATERIALS = "Assets/Materials"
if "--overwrite" not in sys.argv and (os.path.exists(SCENE) or os.path.exists(PREFAB)):
    sys.exit("Refusing to run: this overwrites " + SCENE + ", " + PREFAB + " and the sections' data assets. "
             "Read the header, then pass --overwrite.")

HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"

def guid_for(path): return hashlib.md5(("chessfight:" + path).encode()).hexdigest()
def meta_guid(path): return re.search(r'guid: ([0-9a-f]{32})', open(path + ".meta").read()).group(1)
def write(path, text, meta_extra=""):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    folder = os.path.dirname(path)
    if not os.path.exists(folder + ".meta"):
        open(folder + ".meta", "w").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
                                          "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                                          % guid_for(folder))
    open(path, "w").write(text)
    if not os.path.exists(path + ".meta"):
        open(path + ".meta", "w").write("fileFormatVersion: 2\nguid: %s\n%s" % (guid_for(path), meta_extra))
    print("wrote", path)
def fmt(x):
    if isinstance(x, float): return ("%.7f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))
    return str(x)
def v3(v): return "{x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in v)
def q4(q): return "{x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(c) for c in q)
def text(s):
    """A YAML string as Unity writes non-ASCII text: double-quoted with \\u escapes."""
    return '"' + "".join(c if ord(c) < 128 and c not in '"\\' else "\\u%04X" % ord(c) for c in s) + '"'

def script(name, folder): return meta_guid("%s/%s.cs" % (folder, name))
PR = "Assets/Scripts/PawnRush"
SCRIPT = {
    "SectionKit": script("SectionKit", PR), "PawnRushSectionSet": script("PawnRushSectionSet", PR),
    "PawnRushSection": script("PawnRushSection", PR), "GrandStairHall": script("GrandStairHall", PR),
    "PawnRushRound": script("PawnRushRound", PR), "SectionPlaytest": script("SectionPlaytest", PR),
    "OrbitCamera": script("OrbitCamera", "Assets/Scripts/Game"),
    "PhysicsProfile": script("PhysicsProfile", "Assets/Scripts/Gameplay"),
    "PlaytestSpawner": script("PlaytestSpawner", "Assets/Scripts/Gameplay/Playtest"),
}

def root_object(prefab):
    """fileID of a prefab's root GameObject."""
    body = open(prefab).read()
    for tid, block in re.findall(r'--- !u!4 &(\d+)\nTransform:(.*?)(?=--- !u!|\Z)', body, re.S):
        if "m_Father: {fileID: 0}" in block:
            return int(re.search(r'm_GameObject: \{fileID: (\d+)\}', block).group(1))
    raise SystemExit("no root in " + prefab)
def prefab_ref(prefab): return "{fileID: %d, guid: %s, type: 3}" % (root_object(prefab), meta_guid(prefab))
def mat_ref(name): return "{fileID: 2100000, guid: %s, type: 2}" % meta_guid("%s/%s.mat" % (MATERIALS, name))

# ------------------------------------------------------------------ materials
def material(name, floats, colors):
    shader = meta_guid(MATERIALS + "/CoursePattern.shader")
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

def grid(name, top, side, line, gloss=0.2):
    """1 m grid on every face: `top` under the lines on top faces, `side` on the sides."""
    return material(name, {"_TopStyle": 2.0, "_SideStyle": 3.0, "_Cell": 1.0, "_Glossiness": gloss, "_Metallic": 0.0},
                    {"_Color": top, "_Color2": top, "_SideColor": side, "_LineColor": line})

GREY = (0.62, 0.63, 0.67, 1.0)
MAT = {
    "floorChecker": grid("Section_Floor", (0.40, 0.60, 0.88, 1.0), GREY, (0.22, 0.34, 0.56, 1.0)),
    "wallNoClimb": grid("Section_NoClimb", (0.68, 0.69, 0.72, 1.0), GREY, (0.42, 0.43, 0.47, 1.0), 0.3),
    "hazard": grid("Section_Hazard", (0.97, 0.55, 0.16, 1.0), (0.93, 0.5, 0.14, 1.0), (0.62, 0.3, 0.07, 1.0), 0.35),
    "marker": grid("Section_Marker", (0.50, 0.84, 0.97, 1.0), (0.45, 0.78, 0.92, 1.0), (0.26, 0.56, 0.74, 1.0)),
    "respawnFloor": grid("Section_Respawn", (0.40, 0.13, 0.15, 1.0), (0.34, 0.11, 0.13, 1.0), (0.22, 0.06, 0.08, 1.0), 0.1),
    # Shared with course 01.
    "wallClimbable": mat_ref("Wall_Climbable"), "glass": mat_ref("Glass_TeamDivider"),
    "trimWhite": mat_ref("Trim_White"), "trimBlack": mat_ref("Trim_Black"), "rankGold": mat_ref("Rank_Gold"),
    "boardLight": mat_ref("Board_Light"), "boardDark": mat_ref("Board_Dark"),
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
                      "boardLight", "boardDark", "hazard", "marker", "respawnFloor"])
kit_fields += "".join("  %s: %s\n" % (k, prefab_ref(OBS + v + ".prefab")) for k, v in PREFABS.items())
kit = scriptable("Assets/Settings/PawnRushSectionKit.asset", "SectionKit", "PawnRushSectionKit", kit_fields)

# ------------------------------------------------------------------ the grand stair hall prefab (root only)
GO, TR, SECTION, HALL = 1000, 1001, 1002, 1003
def mono(fid, script_name, fields):
    return ("--- !u!114 &%d\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n%s"
            % (fid, GO, SCRIPT[script_name], fields))
prefab = HEAD + (
    "--- !u!1 &%d\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
    "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
    "  - component: {fileID: %d}\n  - component: {fileID: %d}\n  - component: {fileID: %d}\n"
    "  m_Layer: 0\n  m_Name: Mid_2-1_GrandStairHall\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n"
    "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n") % (GO, TR, SECTION, HALL)
prefab += ("--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
           "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n"
           "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
           "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n"
           "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n") % (TR, GO)
prefab += mono(SECTION, "PawnRushSection",
               "  id: Mid_2-1_GrandStairHall\n  displayName: %s\n  kind: 0\n  entry: {x: 0, y: 0, z: -12}\n"
               "  exit: {x: 0, y: 19, z: 132}\n  exitYaw: 0\n  beats: []\n  slot: 0\n  seed: 1\n  teamSize: 6\n" % text("대계단 현관홀"))
prefab += mono(HALL, "GrandStairHall",
               "  kit: %s\n  buildIfEmpty: 1\n  wings:\n  - {fileID: 0}\n  - {fileID: 0}\n  portcullis:\n  - {fileID: 0}\n"
               "  - {fileID: 0}\n  eastLift: {fileID: 0}\n  eastLiftWall: {fileID: 0}\n" % kit)
write(PREFAB, prefab)
hall_ref = "{fileID: %d, guid: %s, type: 3}" % (GO, meta_guid(PREFAB))

# ------------------------------------------------------------------ the round's seven slots
SLOTS = [("장애물 1 (초반 · 바깥뜰)", 0, []), ("미니게임 1", 1, []), ("장애물 2 (중반 · 아랫궁)", 0, [hall_ref]),
         ("미니게임 2", 1, []), ("장애물 3 (후반 · 윗궁)", 0, []), ("미니게임 3", 1, []), ("마지막 구간", 2, [])]
slots = "  slots:\n" + "".join(
    "  - name: %s\n    kind: %d\n    prefabs:%s\n" % (text(name), kind, "".join("\n    - " + p for p in prefabs) if prefabs else " []")
    for name, kind, prefabs in SLOTS)
section_set = scriptable("Assets/Settings/PawnRushSections.asset", "PawnRushSectionSet", "PawnRushSections", slots)

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

d = Doc(5000)
cam = d.go("Main Camera", pos=(0, 6, -26), tag="MainCamera")
d.comp(cam, "20", "Camera", camera_body)
d.comp(cam, "81", "AudioListener", "  m_Enabled: 1\n")
orbit = d.script(cam, "OrbitCamera",
    "  distance: 6\n  minDistance: 1.5\n  maxDistance: 16\n  minPitch: -40\n  maxPitch: 75\n  mouseSensitivity: 2.2\n"
    "  lookHeight: 0.6\n  shoulder: 0.45\n  followTime: 0.1\n  heightTime: 0.25\n  collisionRadius: 0.2\n"
    "  flySpeed: 12\n  flyFastSpeed: 45\n")
light = d.go("Directional Light", pos=(0, 40, 0), rot=(0.40821788, -0.23456968, 0.10938163, 0.8754261))
d.comp(light, "108", "Light", light_body)
physics = d.go("Physics Profile")
d.script(physics, "PhysicsProfile", "  physicsRate: 120\n  solverIterations: 24\n")
# The round: its first section's entry at the world origin (the grand stair hall's porch arch at z 0).
rnd = d.go("Pawn Rush Round")
round_id = d.script(rnd, "PawnRushRound", "  set: %s\n  teamSize: 6\n  seed: 0\n  buildOnAwake: 1\n  startSpots: 1\n" % section_set)
play = d.go("Playtest", pos=(0, 0, 4))
spawner = d.script(play, "PlaytestSpawner",
    "  characterPrefab: %s\n  spawnPoint: {fileID: 0}\n  cameraRig: {fileID: 0}\n"
    "  orbitCamera: {fileID: %d}\n  team: 0\n  fallLimit: -60\n  fallCatchSpeed: 0\n  showHelp: 1\n"
    % (prefab_ref(PAWN_PREFAB), orbit))
d.script(play, "SectionPlaytest", "  spawner: {fileID: %d}\n  round: {fileID: %d}\n  writeCsv: 1\n" % (spawner, round_id))
write(SCENE, d.render(PREAMBLE))
