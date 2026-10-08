# Generates Assets/Scenes/PawnRushVictory.unity and PawnRushLose.unity, the Pawn Rush result scenes (R76), as
# hand-written Unity YAML (same method as gen_queenhill_scene.py).
#
# The scene holds almost nothing: a camera and the "Last Scene" root with its
# LastSceneDirector, which BUILDS THE FINISH AREA, THE PIECES, THE LIGHTS AND THE
# HUD FROM CODE when Play starts (Assets/Scripts/Game/PawnRushResult*.cs). Look changes
# are code changes; this file only needs running again when the scene's two
# objects change.
#
# !! It OVERWRITES the scene. Once anyone has saved it from the Unity Editor,
# !! running this throws their edits away.
# Run from the project root:  python3 Tools/Generators/gen_pawnrush_result.py --overwrite
import hashlib, os, re, math, sys

TARGETS = ["Assets/Scenes/PawnRushVictory.unity", "Assets/Scenes/PawnRushLose.unity"]
if "--overwrite" not in sys.argv and any(os.path.exists(t) for t in TARGETS):
    sys.exit("Refusing to run: this overwrites the result scenes. Read the header, then pass --overwrite.")

HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"

def guid_for(path): return hashlib.md5(("chessfight:"+path).encode()).hexdigest()
def meta_guid(path): return re.search(r'guid: ([0-9a-f]{32})', open(path + ".meta").read()).group(1)
def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "w", newline="\n").write(text)
    if not os.path.exists(path + ".meta"):
        open(path + ".meta", "w", newline="\n").write("fileFormatVersion: 2\nguid: %s\n" % guid_for(path))
    print("wrote", path)

SCRIPT = {n: meta_guid(p) for n, p in {
    "PawnRushResultDirector": "Assets/Scripts/Game/PawnRushResultDirector.cs",
}.items()}

def fmt(x):
    if isinstance(x, float): return ("%.7f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))
    return str(x)
def v3(v): return "{x: %s, y: %s, z: %s}" % tuple(fmt(c) for c in v)
def q4(q): return "{x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(c) for c in q)
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

# The camera block and the scene settings come from scenes Unity wrote.
kingrush = open("Tools/Generators/templates/KingRush.unity").read()
camera_body = re.search(r'--- !u!20 &\d+\nCamera:\n.*?m_GameObject: \{fileID: \d+\}\n(.*?)(?=--- !u!)', kingrush, re.S).group(1)
camera_body = re.sub(r'm_BackGroundColor: \{[^}]*\}', 'm_BackGroundColor: {r: 0.078, g: 0.051, b: 0.031, a: 1}', camera_body)
camera_body = re.sub(r'field of view: [0-9.]+', 'field of view: 30', camera_body)
camera_body = re.sub(r'far clip plane: [0-9.]+', 'far clip plane: 600', camera_body)
sample = open("Tools/Generators/templates/SampleScene.unity").read()
PREAMBLE = sample[sample.index("--- !u!29 &1"):sample.index("--- !u!1 &330585543")]

def result_scene(target, victory):
    d = Doc(4000)
    cam = d.go("Main Camera", pos=(0, 3, -17), rot=euler_x(2), tag="MainCamera")
    d.comp(cam, "20", "Camera", camera_body)
    d.comp(cam, "81", "AudioListener", "  m_Enabled: 1\n")
    root = d.go("Pawn Rush Result")
    d.script(root, "PawnRushResultDirector", "  victory: %d\n  preview: 1\n" % (1 if victory else 0))
    write(target, d.render(PREAMBLE))

result_scene(TARGETS[0], True)
result_scene(TARGETS[1], False)
