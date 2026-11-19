"""Royal Library course (pr/2 WarRoomComplete) -> Blender blockout, the graphics bench of seoungtest R687.

Reads the Unity prefab as it is (Assets/Prefabs/Obstacles/WarRoomComplete.prefab) and rebuilds every object
with its real world transform, primitive shape and material colour, grouped in collections per course section.
It also adds what GiantChessCourse.cs builds only in Play (the 8x8 board, its entry/exit platforms and arch,
the two placeholder giants) for both tile sizes: 10 m (the scene's value) and 16 m (what the stored decor
was laid out for), and the R687 concept art as reference images beside the course.

Run in Blender 5.2 (live, through MCP or the Text Editor):   exec(open(r"<this file>").read())
or in the background:  blender -b --python rl_blockout.py   (saves RoyalLibrary_blockout.blend next to it)

Axes: Unity (x, y up, z forward) -> Blender (x, z forward = +Y, y up = +Z). Units are metres in both.
Nothing here writes to Unity; the prefab is only read.
"""
import os, re, collections
import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\RoyalLibraryModels"
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PREFAB = os.environ.get("RL_PREFAB") or os.path.join(REPO, "Assets", "Prefabs", "Obstacles", "WarRoomComplete.prefab")
CONCEPT = os.path.join(REPO, "Docs", "PawnRush", "ConceptArt", "R687_GiantChessLibrary")
OUT = os.path.join(HERE, "RoyalLibrary_blockout.blend")

# ---------------------------------------------------------------- read the prefab

def read_prefab(path):
    text = open(path, encoding="utf-8").read()
    parts = re.split(r"^--- !u!(\d+) &(-?\d+).*$", text, flags=re.M)
    objs = {parts[i + 1]: (int(parts[i]), parts[i + 2]) for i in range(1, len(parts), 3)}
    gos, tfs, mesh, mat = {}, {}, {}, {}

    def vec(body, key, n):
        m = re.search(key + r": \{" + ", ".join(c + r": ([-\d.eE]+)" for c in "xyzw"[:n]), body)
        return tuple(float(v) for v in m.groups()) if m else None

    for fid, (cls, body) in objs.items():
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)", body)
        if cls == 1:
            gos[fid] = dict(name=re.search(r"m_Name: (.*)", body).group(1).strip(),
                            active=re.search(r"m_IsActive: (\d)", body).group(1) == "1")
        elif cls == 4:
            tfs[fid] = dict(go=go.group(1), parent=re.search(r"m_Father: \{fileID: (-?\d+)", body).group(1),
                            pos=vec(body, "m_LocalPosition", 3), rot=vec(body, "m_LocalRotation", 4),
                            scl=vec(body, "m_LocalScale", 3),
                            order=[c for c in re.findall(r"- \{fileID: (-?\d+)\}", body.split("m_Father")[0])])
        elif cls == 33:
            m = re.search(r"m_Mesh: \{fileID: (\d+)", body)
            mesh[go.group(1)] = int(m.group(1))
        elif cls == 23:
            m = re.search(r"m_Materials:\s*\n\s*- \{fileID: \d+, guid: ([0-9a-f]+)", body)
            if m: mat[go.group(1)] = m.group(1)
    return gos, tfs, mesh, mat


def material_colours():
    """guid -> (name, rgba) for the course's materials (Resources/RoyalPalace/Materials)."""
    out = {}
    folder = os.path.join(REPO, "Assets", "Resources", "RoyalPalace", "Materials")
    for f in os.listdir(folder):
        if not f.endswith(".mat"): continue
        guid = re.search(r"guid: ([0-9a-f]+)", open(os.path.join(folder, f + ".meta")).read()).group(1)
        m = re.search(r"_Color: \{r: ([-\d.]+), g: ([-\d.]+), b: ([-\d.]+), a: ([-\d.]+)",
                      open(os.path.join(folder, f), encoding="utf-8").read())
        out[guid] = (f[:-4], tuple(float(v) for v in m.groups()) if m else (.6, .6, .6, 1))
    return out

# ---------------------------------------------------------------- Blender helpers

C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # swaps Unity y/z


def unity_trs(pos, rot, scl):
    q = Quaternion((rot[3], rot[0], rot[1], rot[2]))
    return Matrix.LocRotScale(Vector(pos), q, Vector(scl))


def collection(name, parent=None, hide=False):
    col = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    par = parent or bpy.context.scene.collection
    if col.name not in par.children: par.children.link(col)
    if hide:
        lc = find_layer_collection(bpy.context.view_layer.layer_collection, col.name)
        if lc: lc.hide_viewport = True
    return col


def find_layer_collection(lc, name):
    if lc.collection.name == name: return lc
    for c in lc.children:
        r = find_layer_collection(c, name)
        if r: return r
    return None


def unit_meshes():
    """Unity's built-in primitives at Unity size, in Blender axes: cube 1, cylinder r .5 h 2, sphere d 1, capsule h 2."""
    import bmesh
    out = {}
    def make(key, build):
        name = "RL_unit_" + key
        me = bpy.data.meshes.get(name)
        if me is None:
            me = bpy.data.meshes.new(name); bm = bmesh.new(); build(bm); bm.to_mesh(me); bm.free()
            for p in me.polygons: p.use_smooth = key != "cube"
            me.materials.append(flat_material("Unassigned", (.6, .6, .6, 1)))   # one slot, coloured per object
        out[key] = me
    make("cube", lambda bm: bmesh.ops.create_cube(bm, size=1.0))
    make("cylinder", lambda bm: bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=.5, radius2=.5, depth=2.0))
    make("sphere", lambda bm: bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.5))
    def capsule(bm):
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.5)
        for v in bm.verts: v.co.z += .5 if v.co.z > 0 else -.5
    make("capsule", capsule)
    return out


MESH_KEY = {10202: "cube", 10206: "cylinder", 10207: "sphere", 10208: "capsule"}


def flat_material(name, rgba):
    mat = bpy.data.materials.get("RL_" + name)
    if mat is None:
        mat = bpy.data.materials.new("RL_" + name)
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = .6
        mat.diffuse_color = rgba
    return mat


def set_material(obj, mat):
    """The unit meshes are shared, so each object carries its own colour in an object-linked slot."""
    obj.material_slots[0].link = "OBJECT"
    obj.material_slots[0].material = mat

# ---------------------------------------------------------------- sections

SECTIONS = [  # (collection, prefixes of top-level prefab objects)
    ("00 Shell (외벽 · 천장 · 서재 벽)", ("Square perimeter", "Entrance header", "Operations ceiling", "North perimeter",
                                    "Royal library gallery wall", "Distant operations room floor")),
    ("01 Entrance (차원문 · 출발 탁자)", ("Dimensional entry vestibule", "Approach floor -146")),
    ("02 Stamp (도장 탁자)", ("Dispatch table", "Command stamp press")),
    ("03 Books (덮히는 책 · 인장 휴식)", ("Closing royal folio", "Royal folio desk", "Fixed seal island", "Royal seal rest")),
    ("04 Typewriter (타자기)", ("Typewriter", "Approach floor -60", "Approach floor -27")),
    ("05 Library decor (서재 장식)", ("Royal library bookcase -35", "Royal library bookcase 35", "Library gallery houseplant",
                                  "Random GM replay preview")),
    ("06 Chess room (거대 체스 방)", ("Headquarters", "Royal library bookcase -119", "Royal library bookcase 119")),
    ("07 Exit (출구)", ("Final exit walkway", "Complete course exit")),
    ("08 Markers (체크포인트 · 경로)", ("Entry - travel", "Checkpoint", "Approach exit", "Forward waypoint")),
]


def section_of(name):
    for col, prefixes in SECTIONS:
        if any(name.startswith(p) for p in prefixes): return col
    return "99 Other"

# ---------------------------------------------------------------- build


def build():
    for c in [c for c in bpy.data.collections if c.name.startswith("RL ")]:
        for o in list(c.all_objects): bpy.data.objects.remove(o, do_unlink=True)
    for c in [c for c in bpy.data.collections if c.name.startswith("RL ")]:
        for ch in list(c.children_recursive): bpy.data.collections.remove(ch)
        bpy.data.collections.remove(c)

    gos, tfs, meshes, mats = read_prefab(PREFAB)
    colours = material_colours()
    units = unit_meshes()
    root_col = collection("RL Royal Library course (WarRoomComplete)")
    section_cols = {}
    go_tf = {t["go"]: k for k, t in tfs.items()}
    children = collections.defaultdict(list)
    for k, t in tfs.items(): children[t["parent"]].append(k)
    made = 0

    def place(tf_id, parent_obj, parent_world, col, active):
        nonlocal made
        t = tfs[tf_id]; g = gos[t["go"]]
        world = parent_world @ unity_trs(t["pos"], t["rot"], t["scl"])
        key = MESH_KEY.get(meshes.get(t["go"]))
        obj = bpy.data.objects.new(g["name"], units[key] if key else None)
        if key is None:
            obj.empty_display_type = "PLAIN_AXES"; obj.empty_display_size = 1.0
        else:
            guid = mats.get(t["go"])
            name, rgba = colours.get(guid, ("Missing", (1, 0, 1, 1)))
            set_material(obj, flat_material(name, rgba))
        col.objects.link(obj)
        obj.parent = parent_obj
        obj.matrix_world = C @ world @ C
        is_active = active and g["active"]
        obj.hide_viewport = obj.hide_render = not is_active
        obj["unity_name"] = g["name"]
        made += 1
        for c in sorted(children[tf_id], key=lambda c: t["order"].index(c) if c in t["order"] else 0):
            place(c, obj, world, col, is_active)

    root_tf = children["0"][0]
    root = tfs[root_tf]
    root_world = unity_trs(root["pos"], root["rot"], root["scl"])
    top = sorted(children[root_tf], key=lambda c: root["order"].index(c) if c in root["order"] else 0)
    for tf_id in top:
        name = gos[tfs[tf_id]["go"]]["name"]
        sec = section_of(name)
        if sec not in section_cols: section_cols[sec] = collection("RL " + sec, root_col)
        place(tf_id, None, root_world, section_cols[sec], True)

    # Ceilings and their beams hidden in the viewport (render keeps them) so the course reads from above.
    for o in bpy.data.objects:
        if "ceiling" in o.get("unity_name", "").lower(): o.hide_viewport = True

    for tile, hide in ((10, False), (16, True)):
        runtime_chess(tile, collection(f"RL 09 Play-time chess, tile {tile} m" + (" (장면 값)" if tile == 10 else " (장식 기준)"),
                                       root_col, hide=hide), units)
    concept_refs(collection("RL 10 Concept art (R687 D)", root_col))
    scene_setup()
    return made


def runtime_chess(tile, col, units):
    """What GiantChessCourse.cs builds in Play, in Unity coordinates (course root at the origin)."""
    light = flat_material("ChessLight", (.88, .85, .74, 1)); dark = flat_material("ChessDark", (.10, .13, .18, 1))
    gold = flat_material("ChessGold", (.7, .46, .16, 1))

    def shape(name, pos, size, mat, kind="cube"):
        obj = bpy.data.objects.new(name, units[kind])
        col.objects.link(obj)
        set_material(obj, mat)
        obj.matrix_world = C @ Matrix.LocRotScale(Vector(pos), None, Vector(size)) @ C
        return obj

    for s in range(64):
        x, z = (s // 8 - 3.5) * tile, (7 - s % 8 + .5) * tile
        shape(f"Board {'abcdefgh'[s // 8]}{s % 8 + 1}", (x, -.4, z), (tile, .8, tile), light if (s // 8 + s % 8) % 2 else dark)
    shape("Entry platform", (0, -.4, -4), (tile * 8, .8, 8), gold)
    shape("Exit platform", (0, -.4, tile * 8 + 10), (tile * 8, .8, 20), gold)
    for side in (-1, 1): shape("Exit arch pillar", (side * 12, 5, tile * 8 + 12), (2, 10, 2), dark)
    shape("Exit arch header", (0, 10, tile * 8 + 12), (26, 2, 2), gold)
    for side in (0, 1):
        x, z = (-1 if side == 0 else 1) * (tile * 4 + 22), tile * 4
        suit = dark if side == 0 else light
        shape("GM seated torso", (x, 16, z), (18, 26, 22), suit, "capsule")
        shape("GM head", (x, 44, z), (14, 17, 14), light, "sphere")


def concept_refs(col):
    """The chosen concept art as image empties west of the course, readable from the front view."""
    if not os.path.isdir(CONCEPT): return
    files = sorted(f for f in os.listdir(CONCEPT) if f.startswith(("GC_D", "GC_E")) and f.endswith(".png"))
    for i, f in enumerate(files):
        img = bpy.data.images.load(os.path.join(CONCEPT, f), check_existing=True)
        img.filepath = bpy.path.relpath(img.filepath) if bpy.data.filepath else img.filepath
        e = bpy.data.objects.new("Concept " + f[:-4], None)
        e.empty_display_type = "IMAGE"; e.data = img; e.empty_display_size = 60
        e.empty_image_depth = "DEFAULT"; e.use_empty_image_alpha = False
        e.location = (-230, -150 + (i % 6) * 64, 20 + (i // 6) * 44)
        e.rotation_euler = (1.5708, 0, -1.5708)
        col.objects.link(e)


def scene_setup():
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"; sc.unit_settings.scale_length = 1.0
    if "RL Sun" not in bpy.data.objects:
        sun = bpy.data.objects.new("RL Sun", bpy.data.lights.new("RL Sun", "SUN"))
        sun.data.energy = 3.0; sun.rotation_euler = (0.9, 0.2, 0.6)
        bpy.context.scene.collection.objects.link(sun)
    for area in getattr(bpy.context, "screen", None).areas if getattr(bpy.context, "screen", None) else []:
        if area.type == "VIEW_3D":
            for sp in area.spaces:
                if sp.type == "VIEW_3D": sp.clip_end = 5000; sp.clip_start = .1


if __name__ != "rl_blockout":   # imported as a module (rl_kit.py reads the prefab through it): build nothing
  made = build()
  print(f"[rl_blockout] {made} prefab objects from {os.path.relpath(PREFAB, REPO)}")
  if bpy.app.background:
      bpy.ops.wm.save_as_mainfile(filepath=OUT, relative_remap=True)
      print("[rl_blockout] saved", OUT)
