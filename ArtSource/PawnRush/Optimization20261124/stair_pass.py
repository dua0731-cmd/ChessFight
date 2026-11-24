# Grand Stair Hall lighter models (R813, user 11-24 "대계단 경량화 해 보기에 달라지기 없을 때까지").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python stair_pass.py -- <out dir> [search|apply]
#
# For every drawn heavy part (the 11-24 Play probe: what the hall actually draws), tries collapse ratios from the
# lowest up and keeps the lowest one whose close-up renders (two views, same light) still look the same as the
# original: fewer than LIMIT_PIXELS of the pixels off by more than 6/255 and a mean difference under LIMIT_MEAN.
# Kept as the original: names, transforms, material slots and UVs; the edges between materials and sharp edges are
# locked; the shading is the original's (custom normals transferred back from an untouched copy).
#   search  writes <out>/search.json (the ratio per part) and <out>/renders/<part>_<ratio>_<view>.png
#   apply   reads search.json and writes <out>/candidate/<file>.fbx (the same export settings as the 11-13 pass)
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector

ROOT = os.environ.get("CHESSFIGHT_ROOT", "C:/Users/Makebot2/Documents/GitHub/ChessFight")
# R815: STAIR_SET=warroom runs the same pass on the King's war room (Final_WarRoom); default: the Grand Stair Hall
SET = os.environ.get("STAIR_SET", "stair")
SRC = os.path.join(ROOT, {"stair": "Assets/Resources/PawnRushGrandStairHall",
                          "warroom": "Assets/Resources/PawnRushLibrary",
                          "observatory": "Assets/Resources/RoyalObservatory/Models/Hall"}[SET])
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
MODE = args[1] if len(args) > 1 else "search"
RATIOS = [.12, .18, .25, .35, .5, .7, .8, .9]   # .8 / .9: the statues and the falling knight (11-24)
LIMIT_PIXELS, LIMIT_MEAN, OFF = .006, .0025, 6 / 255.
RES = 900

# file -> parts (object-name prefixes); only what the hall draws (StairDrawnProbe 11-24)
TARGETS = {
    "GrandStairHallV15": ["HallV15_Round_wall_", "HallV15_Balustrades", "HallV15_Outer_wall_", "HallV15_Corner_column_",
                          "HallV15_Chandelier_6"],
    "StairStatuesV36": ["StairStatue_King", "StairStatue_Queen", "StairStatue_Knight", "StairStatue_Bishop"],
    "FallingKnight": ["Falling_knight"],
    "RoyalStair": ["RoyalStair_Balustrade", "RoyalStair_Newels", "RoyalStair_Carpet"],
    "FloatingPad": ["Floating_pad"],
    "EntranceV46": ["EntranceV46_walls_", "EntranceV46_arch"],
    "RoyalFan": ["RoyalFan_Frame", "RoyalFan_Rotor"],
}
if SET == "warroom":
    # what the war room draws (WarDrawnProbe 11-24); object or mesh-name prefixes; many share one mesh (Dupli)
    TARGETS = {"PawnRushLibraryEnvironment": ["BLOWER_A", "Bookshelf_", "Return_stair", "War_map_frame",
                                              "Side_wall_staggered", "Array real chain links", "V21_Inner_gallery_pier",
                                              "Vault web"]}
if SET == "observatory":
    # R815: the hall's three heaviest files (ObsDrawnProbe 11-24), their LOD0 only ("*" = name ends with). The banners
    # are skinned (a rig each) and stay as they are.
    TARGETS = {"RoHallArcadeBay": ["*_LOD0"], "RoHallNicheBay": ["*_LOD0"], "RoHallStatues": ["*_LOD0"]}
SKIP = ("_plinth",)


def matches(name, p):
    return name.endswith(p[1:]) if p.startswith("*") else name.startswith(p)
if os.environ.get("STAIR_ONLY"):   # e.g. STAIR_ONLY=FloatingPad,RoyalFan
    TARGETS = {k: v for k, v in TARGETS.items() if k in os.environ["STAIR_ONLY"].split(",")}


def tris(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"), use_custom_normals=True)
    return [o for o in bpy.data.objects if o.type == "MESH"]


def parts(name, obs):
    """One object per target mesh (a mesh several objects share is reduced once, for all of them)."""
    out, seen = [], set()
    for o in obs:
        if any(s in o.name for s in SKIP) or o.data.name in seen:
            continue
        if any(matches(o.name, p) or matches(o.data.name, p) for p in TARGETS[name]) and tris(o.data) >= 3000:
            out.append(o)
            seen.add(o.data.name)
    return out


def part_key(name, ob):
    return name + "|" + ob.data.name


def lock_group(ob):
    """Vertices on material boundaries, open edges and sharp edges (>20 degrees) stay; the rest may collapse."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    locked = set()
    for e in bm.edges:
        f = e.link_faces
        if len(f) != 2 or f[0].material_index != f[1].material_index or e.calc_face_angle(0) > math.radians(20):
            locked.update(v.index for v in e.verts)
    bm.free()
    vg = ob.vertex_groups.new(name="keep")
    free = [v.index for v in ob.data.vertices if v.index not in locked]
    if free:
        vg.add(free, 1., "REPLACE")
    if locked:
        vg.add(list(locked), 0., "REPLACE")
    return vg


def corner_table(me):
    """Every corner of me: its vertex position, its face's normal and its shading normal."""
    me.calc_loop_triangles()
    pos = [me.vertices[l.vertex_index].co.copy() for l in me.loops]
    face = [None] * len(me.loops)
    for p in me.polygons:
        for li in p.loop_indices:
            face[li] = p.normal.copy()
    normal = [n.vector.copy() for n in me.corner_normals]
    return pos, face, normal


def restore_normals(me, table):
    """R813 fix (11-24): the shading normals put back corner by corner from the original. A corner whose vertex did
    not move takes the original corner at the same place on the most alike face (so an untouched part is exactly the
    original, sharp edges and all); a moved one takes the nearest original corner on the most alike face. The 11-24
    first pass used a DATA_TRANSFER modifier here, which by itself shifted the shading of faceted models (2.4 % of the
    pixels on the hall's balustrade with nothing collapsed) and failed them at every ratio."""
    from mathutils import kdtree
    pos, face, normal = table
    tree = kdtree.KDTree(len(pos))
    for i, p in enumerate(pos):
        tree.insert(p, i)
    tree.balance()
    fn = {}
    for p in me.polygons:
        for li in p.loop_indices:
            fn[li] = p.normal
    out = []
    for l in me.loops:
        co = me.vertices[l.vertex_index].co
        near = tree.find_n(co, 24)
        d0 = near[0][2]
        same = [n for n in near if n[2] <= d0 + 1e-6]
        pick = max(same if d0 < 1e-6 else near, key=lambda n: face[n[1]].dot(fn[l.index]) - (0. if d0 < 1e-6 else n[2]))
        out.append(normal[pick[1]])
    me.normals_split_custom_set(out)


def reduce(ob, ratio):
    """Collapses ob to about ratio of its triangles, the original's shading put back (restore_normals)."""
    table = corner_table(ob.data)
    vg = lock_group(ob)
    m = ob.modifiers.new("dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = ratio
    m.use_collapse_triangulate = True
    m.vertex_group = vg.name
    m.vertex_group_factor = 1000.
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=m.name)
    ob.vertex_groups.remove(ob.vertex_groups["keep"])
    restore_normals(ob.data, table)


def stage():
    sc = bpy.context.scene
    items = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        if e in items:
            sc.render.engine = e
            break
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("w")
    sc.world.use_nodes = True
    bg = next(n for n in sc.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (.5, .52, .56, 1)
    bg.inputs[1].default_value = .6
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(55), math.radians(10), math.radians(40))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    # flat grey clay so the shape (not the texture) is what is compared
    clay = bpy.data.materials.new("clay")
    clay.use_nodes = True
    p = next(n for n in clay.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (.7, .68, .64, 1)
    p.inputs["Roughness"].default_value = .55
    return cam, clay


def frame(cam, ob, view):
    ws = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    mn = Vector([min(w[i] for w in ws) for i in range(3)])
    mx = Vector([max(w[i] for w in ws) for i in range(3)])
    c = (mn + mx) / 2
    r = max((mx - mn).length / 2, .05)
    d = Vector(((1, -1.4, .7), (-1.2, -.6, .35))[view]).normalized()
    cam.data.lens = 50
    cam.data.clip_end = r * 20
    cam.location = c + d * r * 2.4
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()


def shot(cam, ob, view, path):
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o != ob
    frame(cam, ob, view)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)[:, :3]
    bpy.data.images.remove(img)
    return px


def search():
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    result = {}
    path = os.path.join(OUT, "search.json")
    if os.path.exists(path):
        result = json.load(open(path, encoding="utf8"))
    for name in TARGETS:
        obs = load(name)
        cam, clay = stage()
        for ob in parts(name, obs):
            key = part_key(name, ob)
            if key in result or name + "|" + ob.name in result:
                continue
            n0 = tris(ob.data)
            slots = [s.material for s in ob.material_slots]
            for s in ob.material_slots:
                s.link = "OBJECT"
                s.material = clay
            safe = lambda s: s.replace("|", "_").replace("/", "_")
            base = [shot(cam, ob, v, os.path.join(OUT, "renders", "%s_orig_%d.png" % (safe(ob.name), v))) for v in (0, 1)]
            keep_me = ob.data.copy()
            chosen = None
            for ratio in RATIOS:
                ob.data = keep_me.copy()
                reduce(ob, ratio)
                ok, scores = True, []
                for v in (0, 1):
                    px = shot(cam, ob, v, os.path.join(OUT, "renders", "%s_%.2f_%d.png" % (safe(ob.name), ratio, v)))
                    d = np.abs(px - base[v]).max(axis=1)
                    frac, mean = float((d > OFF).mean()), float(d.mean())
                    scores.append((round(frac, 5), round(mean, 5)))
                    ok = ok and frac < LIMIT_PIXELS and mean < LIMIT_MEAN
                print("TRY %-45s %.2f %s %s" % (ob.name, ratio, scores, "OK" if ok else "-"), flush=True)
                if ok:
                    chosen = dict(ratio=ratio, before=n0, after=tris(ob.data), scores=scores)
                    break
            result[key] = chosen or dict(ratio=None, before=n0, after=n0)
            print("PICK %-45s %s" % (ob.name, result[key]), flush=True)
            ob.data = keep_me
            for s, m in zip(ob.material_slots, slots):
                s.material = m
                s.link = "DATA"
            with open(path, "w", encoding="utf8") as f:
                json.dump(result, f, indent=1)


def apply():
    picks = json.load(open(os.path.join(OUT, "search.json"), encoding="utf8"))
    os.makedirs(os.path.join(OUT, "candidate"), exist_ok=True)
    for name in TARGETS:
        obs = load(name)
        before = sum(tris(o.data) for o in obs)
        done = set()
        for ob in obs:
            mesh = ob.data
            if mesh.name in done:
                continue
            # keyed by mesh (R815, shared meshes); the 11-24 stair runs keyed by object
            p = picks.get(name + "|" + mesh.name) or picks.get(name + "|" + ob.name)
            if not (p and p.get("ratio")):
                continue
            users = [o for o in obs if o.data == mesh]
            done.add(mesh.name)
            ob.data = mesh.copy()
            reduce(ob, p["ratio"])
            for o in users:
                o.data = ob.data
            old = mesh.name
            bpy.data.meshes.remove(mesh)
            ob.data.name = old   # the FBX geometry keeps its name (Unity's mesh name)
        after = sum(tris(o.data) for o in obs)
        # R815: every object the file had, not only the meshes (the war room's 124 empties - sockets and markers)
        bpy.ops.object.select_all(action="DESELECT")
        for o in bpy.data.objects:
            o.select_set(True)
        bpy.context.view_layer.objects.active = obs[0]
        kinds = {o.type for o in bpy.data.objects} & {"MESH", "EMPTY", "ARMATURE", "LIGHT", "CAMERA", "OTHER"}
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "candidate", name + ".fbx"), use_selection=True,
                                 object_types=kinds, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
                                 axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False,
                                 mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="STRIP", embed_textures=False,
                                 colors_type="LINEAR")
        print("APPLY %s %d -> %d" % (name, before, after), flush=True)


search() if MODE == "search" else apply()
