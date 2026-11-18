"""Royal Library modelling kit (R0, seoungtest R687, 2026-11-18): what every zone's model script shares.

    import sys; sys.path.insert(0, r"<this folder>"); import rl_kit as k

What it gives (PLAN.md 3: the zones read this, only R0 changes it):
  - SLOTS / slot(): the material slot names. A model's faces carry "RL | walnut", "RL | brass" ... ; the Unity side
    (RoyalLibraryDressing.cs) draws each in Resources/RoyalLibrary/Materials/RL_<Name>. Colours here only preview them.
  - surface_uvs(obj): metre UVs, one part (connected piece) at a time, U along the part's longest side, a random offset
    a part - the carriage yard's rcy_kit.surface_uvs, the textures being seamless tiles in metres (tile in rl_textures).
  - frame(name): the blockout object's pose (from the prefab), for models that ride a moving piece.
  - export(collection, model, zone, frame=None): FBX to Assets/Resources/RoyalLibrary/Models/<zone>/<model>.fbx with
    the carriage yard's import settings and a .meta with a fixed GUID. frame = the prefab object the model hangs on
    (its name, e.g. "Horizontal closing cover"): the model is written in that object's frame (position and rotation,
    not scale), so it follows the piece; None = the course root's frame (static pieces).
Build in the blockout's frame (rl_blockout.py: Blender x, y, z = Unity x, z, y, metres), at the blockout objects'
places and sizes; walking surfaces and colliders stay the prefab's (a model must not stick out above a walking face).
Never export while Unity is in Play.
"""
import hashlib, os, sys
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
os.environ.setdefault("RCY_ROOT", ROOT)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
import rcy_kit   # noqa: E402  (UVs; its other parts belong to the carriage yard)
rcy_kit.RCY_UVS = True
import rl_blockout   # noqa: E402  (reads the prefab; builds nothing when imported)

OUT_REL = "Assets/Resources/RoyalLibrary/Models"
FBX_META_TEMPLATE = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
FOLDER_META = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

# slot -> (library material in Unity, preview colour in Blender: the swatch colours, linear-ish)
SLOTS = {
    "RL | stone": ("RL_CreamStone", (.80, .55, .31, 1)),
    "RL | walnut": ("RL_Walnut", (.20, .057, .017, 1)),
    "RL | varnished": ("RL_VarnishedWalnut", (.14, .038, .012, 1)),
    "RL | brass": ("RL_Brass", (.56, .26, .045, 1)),
    "RL | leather": ("RL_RedLeather", (.23, .024, .014, 1)),
    "RL | parchment": ("RL_Parchment", (.91, .64, .34, 1)),
    "RL | ink": ("RL_InkGlass", (.017, .011, .011, 1)),
    "RL | cloth": ("RL_CreamCloth", (.72, .48, .26, 1)),
    "RL | wax": ("RL_RedWax", (.42, .020, .018, 1)),
    "RL | marble": ("RL_Marble", (.83, .72, .62, 1)),     # R1 11-18: the entrance arch and floor, white marble
    "RL | ivory": ("RL_Ivory", (.79, .65, .44, 1)),       # R6 11-18: the white chess pieces
    "RL | ebony": ("RL_Ebony", (.024, .013, .009, 1)),    # R6 11-18: the black chess pieces
    "RL | glass": ("RL_Glass", (1.0, .97, .90, 1)),       # R7 11-18: clear glass (transparent in Unity, no texture)
    # the plants session 11-18 (GC_assets_02_plants)
    "RL | leaf": ("RL_Leaf", (0.035, 0.13, 0.025, 1)),
    "RL | leaf_light": ("RL_LeafLight", (0.12, 0.32, 0.03, 1)),
    "RL | stem": ("RL_Stem", (0.07, 0.18, 0.03, 1)),
    "RL | soil": ("RL_Soil", (0.035, 0.02, 0.012, 1)),
    "RL | petal_cream": ("RL_PetalCream", (0.85, 0.78, 0.6, 1)),
    "RL | petal_pink": ("RL_PetalPink", (0.9, 0.42, 0.42, 1)),
    "RL | ceramic": ("RL_Ceramic", (0.8, 0.66, 0.42, 1)),
    "RL | pollen": ("RL_Pollen", (0.85, 0.6, 0.1, 1)),
    "RL | bark": ("RL_Bark", (.12, .06, .025, 1)),
    # 11-18 the chess room's portrait canvas: a whole picture, map the canvas face 0..1 (NOT surface_uvs)
    "RL | painting": ("RL_Portrait", (.35, .17, .06, 1)),
    "RL | tea": ("RL_Tea", (.15, .03, .005, 1)),           # 11-18 R7: the tea in the giant teacup
    "RL | veil": ("RL_Veil", (.62, .78, .82, 1)),          # 11-18 R1: the dimensional veil (transparent, two-sided in Unity)      # R1 11-18: the stamp's face, the red seal marks
    # R4 11-18: book spines - the red leather recoloured (rl_textures TINTS); slate is a near-neutral dark grey
    "RL | leather_brown": ("RL_LeatherBrown", (.14, .045, .015, 1)),
    "RL | leather_dark": ("RL_LeatherDark", (.068, .011, .007, 1)),
    "RL | leather_tan": ("RL_LeatherTan", (.39, .16, .045, 1)),
    "RL | leather_slate": ("RL_LeatherSlate", (.07, .066, .072, 1)),
}


def slot(name):
    """The Blender material for a slot ("walnut" or "RL | walnut")."""
    key = name if name.startswith("RL | ") else "RL | " + name
    if key not in SLOTS:
        raise KeyError("unknown slot %s (rl_kit.SLOTS: %s)" % (key, ", ".join(SLOTS)))
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        p.inputs["Base Color"].default_value = SLOTS[key][1]
        p.inputs["Roughness"].default_value = .5
        if key == "RL | brass":
            p.inputs["Metallic"].default_value = .95
        if key in ("RL | glass", "RL | veil"):
            p.inputs["Roughness"].default_value = .03
            for s in ("Transmission Weight", "Transmission"):
                if s in p.inputs:
                    p.inputs[s].default_value = 1.0
        m.diffuse_color = SLOTS[key][1]
    return m


def surface_uvs(obj, salt=0):
    """Metre UVs on a mesh object (modifiers are not applied: call it on the final mesh, or after applying)."""
    import bmesh
    # one UV channel only (Unity reads the first): primitives' own "UVMap" would otherwise stay first after a join
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.data.uv_layers.new(name="UVMap")
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)   # in the frame the model is exported in, as rcy_kit does
    parts = rcy_kit.surface_uvs(bm, salt)
    bm.transform(obj.matrix_world.inverted())
    bm.to_mesh(obj.data)
    bm.free()
    return parts


_poses = None


def frame(name, index=0):
    """Blender-space matrix (location + rotation, no scale) of the prefab object named `name` (the index-th of that
    name, in prefab order)."""
    global _poses
    if _poses is None:
        gos, tfs, _, _ = rl_blockout.read_prefab(rl_blockout.PREFAB)
        kids = {}
        for k, t in tfs.items():
            kids.setdefault(t["parent"], []).append(k)
        _poses = {}

        def walk(k, parent):
            t = tfs[k]
            w = parent @ rl_blockout.unity_trs(t["pos"], t["rot"], t["scl"])
            _poses.setdefault(gos[t["go"]]["name"], []).append(w)
            for c in sorted(kids.get(k, []), key=lambda c: t["order"].index(c) if c in t["order"] else 0):
                walk(c, w)
        for r in kids["0"]:
            walk(r, Matrix.Identity(4))
    w = _poses[name][index]
    loc, rot, _ = w.decompose()
    return rl_blockout.C @ Matrix.LocRotScale(loc, rot, None) @ rl_blockout.C


def _guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def _write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def export(collection_name, model, zone, frame_name=None, frame_index=0):
    """Every mesh of the collection (modifiers applied) to Models/<zone>/<model>.fbx; see the module text."""
    for rel in ("Assets/Resources/RoyalLibrary", OUT_REL, OUT_REL + "/" + zone):
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
            _write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (_guid(rel), FOLDER_META))
    col = bpy.data.collections[collection_name]
    meshes = [o for o in col.all_objects if o.type == "MESH"]
    bad = [o.name for o in meshes for m in o.data.materials if m is None or m.name not in SLOTS]
    if bad:
        raise RuntimeError("export %s: faces outside rl_kit.SLOTS on %s" % (model, sorted(set(bad))[:8]))
    to_local = frame(frame_name, frame_index).inverted() if frame_name else Matrix.Identity(4)
    saved = {o: o.matrix_world.copy() for o in meshes}
    try:
        for o in meshes:
            if o.parent is None or o.parent not in saved:
                o.matrix_world = to_local @ saved[o]
        bpy.ops.object.select_all(action="DESELECT")
        for o in meshes:
            o.hide_set(False); o.hide_viewport = False; o.select_set(True)
        missed = [o.name for o in meshes if not o.select_get()]
        if missed:
            raise RuntimeError("export %s: not selectable (collection hidden in the view layer?): %s" % (model, missed))
        bpy.context.view_layer.objects.active = meshes[0]
        rel = "%s/%s/%s.fbx" % (OUT_REL, zone, model)
        bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, rel), use_selection=True, object_types={"MESH"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                                 bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                                 path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    finally:
        for o, m in saved.items():
            o.matrix_world = m
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        with open(FBX_META_TEMPLATE, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + _guid(rel)
        _write(rel + ".meta", "\n".join(lines))
    tris = sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.polygons)
               for o in meshes)
    print("[rl_kit] exported", rel, len(meshes), "objects", tris, "tris", "frame", frame_name or "root")
    return rel
