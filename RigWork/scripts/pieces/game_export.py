# Put the rigged Tripo pieces into the game, eyes included, coloured by stripe atlases (no painted textures: JY 11-12).
# The game's baker takes one skinned mesh with one material and one UV set (PieceModelBaker), so:
#   - the eye meshes (RigWork/eyes/mesh/<P>_Eyes.blend: Eye, Glint, Blush, the knight's Brow) are joined into the
#     piece mesh, each vertex weighted like the body face under it (inverse-distance over that face's corners)
#   - every face maps to one colour stripe of the atlas: body, hands and feet, eye, glint, blush, brow
#   - two atlases: <P>_Albedo.png (white team) and <P>_Black_Albedo.png (black team: ivory eyes, glint the eye's colour
#     so it disappears, the black team's blush), colours read from the eye file's materials
#   - the body is triangulated first with quad_method FIXED (Blender's 0-2 split), which the eyes were fitted to
# blender -b -P game_export.py -- <rigged .blend dir> <eye .blend dir> <Assets/Art/Pieces dir, absolute> Rook ...
import bpy, bmesh, sys, os
import numpy as np
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index("--") + 1:]
SRC, EYES, DST, NAMES = a[0], a[1], a[2], a[3:]
CREAM, CHARCOAL, DARK = (0.96, 0.88, 0.76), (0.22, 0.22, 0.24), (0.05, 0.045, 0.045)
STRIPES = 8          # body, limbs, eye, glint, blush, brow, spare, spare
KIND = {"Eye": 2, "Glint": 3, "Blush": 4, "Brow": 5}

def srgb(c):
    return tuple(12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055 for x in c[:3])

def base(mat_name, fallback):
    m = bpy.data.materials.get(mat_name)
    if m is None:
        return fallback
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                # a face part drawn mostly by its emission is its emission colour: the black team's ivory eyes are
                # ivory x0.2 + emission 0.9 (RigWork/README.md section 2), and their base colour alone came out khaki
                if n.inputs["Emission Strength"].default_value >= 0.8:
                    return srgb(n.inputs["Emission Color"].default_value)
                return srgb(n.inputs["Base Color"].default_value)
    return srgb(m.diffuse_color)

def atlas(path, cols):
    n, w = 256, 256 // STRIPES
    px = np.ones((n, n, 4), dtype=np.float32)
    for i in range(STRIPES):
        px[:, i * w:(i + 1) * w, :3] = cols[i] if i < len(cols) else cols[0]
    img = bpy.data.images.new("atlas", n, n, alpha=False)
    img.pixels = px.ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()

def stripe_uv(me, kinds):
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        k = kinds(p)
        for li in p.loop_indices:
            uv.data[li].uv = ((k + 0.5) / STRIPES, 0.5)

for name in NAMES:
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, name + "_Rigged.blend"))
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    body = [o for o in bpy.data.objects if o.type == "MESH"][0]
    me = body.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="FIXED", ngon_method="BEAUTY")
    bm.to_mesh(me); bm.free()
    hf = {g.index for g in body.vertex_groups if "Hand" in g.name or "Foot" in g.name}
    dark = [sum(g.weight for g in v.groups if g.group in hf) > 0.5 for v in me.vertices]
    stripe_uv(me, lambda p: 1 if all(dark[i] for i in p.vertices) else 0)
    names = [g.name for g in body.vertex_groups]
    W8 = np.zeros((len(me.vertices), len(names)))
    for v in me.vertices:
        for g in v.groups:
            W8[v.index, g.group] = g.weight
    P = np.array([v.co[:] for v in me.vertices])
    tree = BVHTree.FromPolygons([v.co for v in me.vertices], [p.vertices[:] for p in me.polygons])
    with bpy.data.libraries.load(os.path.join(EYES, name + "_Eyes.blend")) as (src, dst):
        dst.objects = list(src.objects)
        dst.materials = list(src.materials)
    eyes = [o for o in dst.objects if o and o.type == "MESH"]
    for o in eyes:
        bpy.context.scene.collection.objects.link(o)
        o.data = o.data.copy()
        k = next(v for key, v in KIND.items() if o.name.startswith(key))
        stripe_uv(o.data, lambda p, k=k: k)
        for nm in names:
            o.vertex_groups.new(name=nm)
        for v in o.data.vertices:
            co = o.matrix_world @ v.co
            _, _, fi, _ = tree.find_nearest(co)
            ids = list(me.polygons[fi].vertices)
            wd = 1.0 / (np.linalg.norm(P[ids] - np.array(co[:]), axis=1) + 1e-6)
            w = (W8[ids] * wd[:, None]).sum(0) / wd.sum()
            w = w / max(w.sum(), 1e-9)
            for gi in np.nonzero(w > 1e-4)[0]:
                o.vertex_groups[names[gi]].add([v.index], float(w[gi]), "REPLACE")
        o.parent = None
    white = [CREAM, DARK, base("Eye_WhiteTeam_Black", DARK), base("Glint_WhiteTeam", (1, 1, 1)),
             base("Blush_WhiteTeam", (0.96, 0.6, 0.54)), base("Eye_WhiteTeam_Black", DARK)]
    ivory = base("Eye_BlackTeam_Ivory", (0.93, 0.9, 0.82))
    black = [CHARCOAL, DARK, ivory, ivory, base("Blush_BlackTeam", (0.7, 0.4, 0.4)), ivory]
    bpy.ops.object.select_all(action="DESELECT")
    for o in eyes:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in me.polygons:
        p.material_index = 0
    me.materials.clear()
    me.materials.append(bpy.data.materials.new(name))
    # check: body faces stay flat, eye faces keep their own smooth normals
    cn = np.array([c.vector[:] for c in me.corner_normals])
    uvk = np.array([me.uv_layers[0].data[p.loop_start].uv[0] * STRIPES - 0.5 for p in me.polygons]).round().astype(int)
    dev = np.array([max(np.degrees(np.arccos(np.clip(np.dot(cn[li], p.normal), -1, 1))) for li in p.loop_indices) for p in me.polygons])
    print("NORMALS", name, "body max dev %.1f deg," % dev[uvk <= 1].max(), "eye mean dev %.1f deg" % dev[uvk >= 2].mean(),
          "| eye faces", int((uvk >= 2).sum()), "| groups", len(body.vertex_groups))
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(DST, name, name + "_Rigged.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, mesh_smooth_type="FACE",
                             use_armature_deform_only=False)
    atlas(os.path.join(DST, name, name + "_Albedo.png"), white)
    atlas(os.path.join(DST, name, name + "_Black_Albedo.png"), black)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(os.path.abspath(sys.argv[sys.argv.index("-P") + 1])), "game_" + name + ".blend"))
    print("GAME", name, len(me.vertices), "verts,", len(me.polygons), "faces; white", [tuple(round(x, 2) for x in c) for c in white[2:5]])
