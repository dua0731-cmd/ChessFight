# Generates Assets/Scenes/QueenOfTheHill.unity, the Queen of the Hill graybox
# playtest scene, as hand-written Unity YAML (same method as gen_scenes.py).
#
# The scene holds almost nothing: a camera with an OrbitCamera, a light, the game
# root, the QueenHillLevel that BUILDS THE WHOLE MAP FROM CODE when Play starts
# (Assets/Scripts/Gameplay/QueenHill/QueenHillLevel.cs), and the offline playtest
# (the ragdoll pawn, white team, split-time panel). Map changes are code changes;
# this file only needs running again when the scene's few objects change.
#
# !! It OVERWRITES the scene. Once anyone has saved it from the Unity Editor,
# !! running this throws their edits away.
# Run from the project root:  python3 Tools/Generators/gen_queenhill_scene.py --overwrite
import hashlib, os, re, math, sys

if "--overwrite" not in sys.argv and os.path.exists("Assets/Scenes/QueenOfTheHill.unity"):
    sys.exit("Refusing to run: this overwrites Assets/Scenes/QueenOfTheHill.unity. Read the header, then pass --overwrite.")

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

SCRIPT = {n: meta_guid(p) for n, p in {
    "GameSceneConfig": "Assets/Scripts/Game/GameSceneConfig.cs",
    "OrbitCamera": "Assets/Scripts/Game/OrbitCamera.cs",
    "PhysicsProfile": "Assets/Scripts/Gameplay/PhysicsProfile.cs",
    "PlaytestSpawner": "Assets/Scripts/Gameplay/Playtest/PlaytestSpawner.cs",
    "QueenHillLevel": "Assets/Scripts/Gameplay/QueenHill/QueenHillLevel.cs",
    "QueenHillPlaytestPanel": "Assets/Scripts/Gameplay/QueenHill/QueenHillPlaytestPanel.cs",
}.items()}

def v3(v): return "{x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in v)
def q4(q): return "{x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(c) for c in q)
def fmt(x):
    if isinstance(x, float): return ("%.7f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))
    return str(x)
def euler_x(deg): r = math.radians(deg) / 2; return (math.sin(r), 0.0, 0.0, math.cos(r))

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
kingrush = open("Assets/Scenes/KingRush.unity").read()
camera_body = re.search(r'--- !u!20 &\d+\nCamera:\n.*?m_GameObject: \{fileID: \d+\}\n(.*?)(?=--- !u!)', kingrush, re.S).group(1)
camera_body = re.sub(r'm_BackGroundColor: \{[^}]*\}', 'm_BackGroundColor: {r: 0.64, g: 0.79, b: 0.93, a: 1}', camera_body)
camera_body = camera_body.replace("far clip plane: 1000", "far clip plane: 1500")
config_body = re.search(r'm_Script: \{fileID: 11500000, guid: %s, type: 3\}\n  m_Name: \n  m_EditorClassIdentifier: \n(.*?)(?=--- !u!)'
                        % SCRIPT["GameSceneConfig"], kingrush, re.S).group(1)
sample = open("Assets/Scenes/SampleScene.unity").read()
light_body = re.search(r'--- !u!108 &410087040\nLight:\n(.*?)(?=--- !u!)', sample, re.S).group(1)
light_body = light_body.split("  m_GameObject: {fileID: 410087039}\n", 1)[1].replace("  m_Intensity: 2\n", "  m_Intensity: 1.2\n")
PREAMBLE = sample[sample.index("--- !u!29 &1"):sample.index("--- !u!1 &330585543")]

# The ragdoll pawn prefab (Assets/ChessFight/RagdollLab/Prefabs), by its root GameObject.
PAWN_PREFAB = "Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab"
pawn_text = open(PAWN_PREFAB).read()
pawn_root = None
for tid, body in re.findall(r'--- !u!4 &(\d+)\nTransform:(.*?)(?=--- !u!)', pawn_text, re.S):
    if "m_Father: {fileID: 0}" in body:
        pawn_root = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1))
assert pawn_root, "no root in " + PAWN_PREFAB

def queen_of_the_hill():
    d = Doc(2000)
    cam = d.go("Main Camera", pos=(0, 22, -75), rot=euler_x(10), tag="MainCamera")
    d.comp(cam, "20", "Camera", camera_body)
    d.comp(cam, "81", "AudioListener", "  m_Enabled: 1\n")
    orbit = d.script(cam, "OrbitCamera",
        "  distance: 5\n  minDistance: 1.5\n  maxDistance: 14\n  minPitch: -40\n  maxPitch: 75\n  mouseSensitivity: 2.2\n"
        "  lookHeight: 0.6\n  shoulder: 0.45\n  followTime: 0.1\n  heightTime: 0.25\n  collisionRadius: 0.2\n"
        "  flySpeed: 12\n  flyFastSpeed: 45\n")

    light = d.go("Directional Light", pos=(0, 3, 0), rot=(0.40821788, -0.23456968, 0.10938163, 0.8754261))
    d.comp(light, "108", "Light", light_body)

    root = d.go("ChessFight Game Root")
    d.script(root, "GameSceneConfig", config_body)
    d.script(root, "PhysicsProfile", "  physicsRate: 120\n  solverIterations: 24\n")

    level = d.go("Queen of the Hill Level")
    d.script(level, "QueenHillLevel", "  exclusiveSeconds: 20\n  signs: 1\n")

    play = d.go("Playtest")
    spawner = d.script(play, "PlaytestSpawner",
        "  characterPrefab: {fileID: %d, guid: %s, type: 3}\n  spawnPoint: {fileID: 0}\n  cameraRig: {fileID: 0}\n"
        "  orbitCamera: {fileID: %d}\n  team: 0\n  fallLimit: -20\n  fallCatchSpeed: 15\n  showHelp: 1\n"
        % (pawn_root, meta_guid(PAWN_PREFAB), orbit))
    d.script(play, "QueenHillPlaytestPanel", "  spawner: {fileID: %d}\n  show: 1\n" % spawner)
    write("Assets/Scenes/QueenOfTheHill.unity", d.render(PREAMBLE))

queen_of_the_hill()
