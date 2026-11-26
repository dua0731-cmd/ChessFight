"""Grand banquet hall modelling kit (B0, seoungtest R837, 2026-11-25): what every zone's model script shares.

    import sys; sys.path.insert(0, r"<this folder>"); import bh_kit as k

Made from the observatory's ro_kit.py (the same shapes, UVs, bevel, graybox frames and export). The zones read this;
only B0 (the bh-path session) changes it, and never renames what is here (PLAN.md 2 and 3).

  - SLOTS / slot(): the material slots, names fixed by PLAN.md 2 ("stone", "walnut", "gold" ... plus "olive"). A
    model's faces carry "BH | gold" ...; the Unity side (GrandBanquetHallDressing.cs) draws each with
    Resources/BanquetHall/Materials/BH_<Name>. The colours here only preview them; preview_materials() puts the
    library's textures on them for stills, as Unity will draw them.
  - surface_uvs(obj): metre UVs, one connected part at a time (the carriage yard's rcy_kit.surface_uvs); the library
    textures are seamless tiles in metres. Faces in a KEEP_UVS slot keep the UVs their script gave them.
  - bevel(obj, w): the round bevel every hard edge gets (weighted normals so the faces stay flat).
  - lathe(), star_outline(), prism(), beads(): the shapes the kit (bh_parts.py) and the zones build from.
  - frame(name): the pose of a built graybox object (bh_blockout.json, dumped from Unity), for models that hang under
    a moving object (a waiter, a cloche, a couple ...).
  - export(collection, model, zone, frame=None): FBX to Assets/Resources/BanquetHall/Models/<zone>/<model>.fbx with a
    .meta of a fixed GUID. frame = the graybox object the model hangs under (its name or path, e.g. "BH waiter 1"):
    written in that object's frame (position and rotation, not scale); None = the section's frame (static pieces).

Axes: Unity section frame (x east, y up, z north, metres, the entry's threshold at 0) -> Blender (x, z up, y north):
Blender (x, y, z) = Unity (x, z, y). Build at the blockout's places and sizes (bh_blockout.py); walking faces and
colliders stay the builder's - a model must not stand above a walking face. Never export while Unity is in Play.
"""
import hashlib, json, math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
os.environ.setdefault("RCY_ROOT", ROOT)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
import rcy_kit   # noqa: E402  (UVs only)
rcy_kit.RCY_UVS = True

DUMP = os.path.join(HERE, "bh_blockout.json")
OUT_REL = "Assets/Resources/BanquetHall/Models"
LIB = os.path.join(HERE, "textures", "library")
FBX_META_TEMPLATE = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
FOLDER_META = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # swaps Unity y / z (its own inverse)

# ------------------------------------------------------------------ material slots (names fixed: PLAN.md 2)
# slot -> (library material in Unity, preview colour (linear) in Blender, metallic, roughness)
SLOTS = {
    "BH | stone": ("BH_Stone", (.75, .60, .37, 1), 0.0, .7),          # cream ashlar (walls)
    "BH | walnut": ("BH_Walnut", (.10, .035, .016, 1), 0.0, .45),    # walnut posts, beams, panels, chairs
    "BH | gold": ("BH_Gold", (.66, .38, .07, 1), 1.0, .15),          # polished gilt: trims, candlesticks, rims, ware
    "BH | silver": ("BH_Silver", (.53, .54, .56, 1), 1.0, .2),       # brushed silver: cloches, platters
    "BH | linen": ("BH_Linen", (.84, .81, .73, 1), 0.0, .82),        # white tablecloths, the plates' china
    "BH | runner": ("BH_Runner", (.09, .12, .023, 1), 0.0, .78),     # olive runner with gold crowns
    "BH | velvet": ("BH_Velvet", (.15, .005, .009, 1), 0.0, .8),     # red velvet (the stage curtain)
    "BH | marble": ("BH_Marble", (.86, .76, .61, 1), 0.0, .22),      # cream marble (the dais, the balcony)
    "BH | floor_light": ("BH_FloorLight", (.86, .74, .59, 1), 0.0, .18),   # the dance floor's light squares
    "BH | floor_dark": ("BH_FloorDark", (.10, .03, .012, 1), 0.0, .3),     # its dark squares
    "BH | chocolate": ("BH_Chocolate", (.068, .02, .009, 1), 0.0, .14),
    "BH | crystal": ("BH_Crystal", (.85, .90, 1.0, 1), 0.0, .03),    # clear glass, transparent in Unity (no texture)
    "BH | wax": ("BH_Wax", (.89, .80, .58, 1), 0.0, .55),            # candle wax (the flame is Unity's)
    "BH | piece_white": ("BH_Ivory", (.84, .77, .61, 1), 0.0, .38),  # the white pieces' ivory
    "BH | piece_black": ("BH_Ebony", (.008, .005, .004, 1), 0.0, .3),  # the black pieces' ebony
    # R837 (B0) added: BH_T1's olive velvet - the chairs' seats and backs, a banner's ground
    "BH | olive": ("BH_Olive", (.07, .10, .012, 1), 0.0, .8),
    # R837 (B0) added, colour-only in Unity (no texture): the plates' white china, the goblets' cabochons
    "BH | damask": ("BH_GoldDamask", (.66, .38, .07, 1), 1.0, .22),  # gilt with BH_T1's damask chased in (panels, bands)
    "BH | china": ("BH_China", (.86, .85, .82, 1), 0.0, .12),
    "BH | emerald": ("BH_Emerald", (.035, .10, .012, 1), 0.0, .05),
    # R837: the people - the user's Tripo figurines (bh_figures.py), each set one atlas (their UVs kept)
    "BH | figures_royal": ("BH_FiguresRoyal", (.5, .4, .3, 1), 0.0, .5),
    "BH | figures_staff": ("BH_FiguresStaff", (.5, .4, .3, 1), 0.0, .5),
}
KEEP_UVS = ("BH | figures_royal", "BH | figures_staff")   # slots whose faces carry their own UVs (surface_uvs leaves them)


def slot_key(name):
    return name if name.startswith("BH | ") else "BH | " + name


def unity_material(key):
    """The Unity material a slot draws with."""
    return SLOTS[slot_key(key)][0]


def slot(name):
    """The Blender material for a slot ("gold" or "BH | gold")."""
    key = slot_key(name)
    if key not in SLOTS:
        raise KeyError("unknown slot %s (bh_kit.SLOTS: %s)" % (key, ", ".join(SLOTS)))
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        colour, metal, rough = SLOTS[key][1:]
        p.inputs["Base Color"].default_value = colour
        p.inputs["Metallic"].default_value = metal
        p.inputs["Roughness"].default_value = rough
        if key == "BH | crystal":
            for s in ("Transmission Weight", "Transmission"):
                if s in p.inputs:
                    p.inputs[s].default_value = 1.0
        m.diffuse_color = colour
    return m


def set_slots(obj, *names):
    """Give a mesh object these slots in this order (material index i = names[i])."""
    obj.data.materials.clear()
    for n in names:
        obj.data.materials.append(slot(n))
    return obj


def preview_materials():
    """For stills: every slot's Blender material draws the library's tile (albedo, alpha = smoothness, normal) on the
    faces' metre UVs (UV x 1 / tile), as Unity will. Slots without a texture (crystal) keep their colour."""
    info = json.load(open(os.path.join(LIB, "bh_textures.json"), encoding="utf-8"))
    for key, spec_row in SLOTS.items():
        unity = spec_row[0]
        spec = info.get(unity)
        if spec is None:
            continue
        m = slot(key)
        nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        for n in [n for n in nt.nodes if n.type in ("TEX_IMAGE", "MAPPING", "TEX_COORD", "NORMAL_MAP", "MATH")]:
            nt.nodes.remove(n)
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1.0 / spec["tile"],) * 3
        nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])

        def img(kind, colour):
            path = os.path.join(LIB, "%s_%s.png" % (unity, kind))
            im = bpy.data.images.get(os.path.basename(path)) or bpy.data.images.load(path)
            if not colour:
                im.colorspace_settings.name = "Non-Color"
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = im
            nt.links.new(mp.outputs["Vector"], t.inputs["Vector"])
            return t
        a = img("Albedo", True)
        nt.links.new(a.outputs["Color"], p.inputs["Base Color"])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(a.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], p.inputs["Roughness"])
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(img("Normal", False).outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
        p.inputs["Metallic"].default_value = spec["metallic"]


# ------------------------------------------------------------------ UVs

def _motif_index(obj):
    for i, m in enumerate(obj.data.materials):
        if False:
            return i
    return -1


def _kept_indices(obj):
    return {i for i, m in enumerate(obj.data.materials) if m is not None and m.name in KEEP_UVS}


def surface_uvs(obj, salt=0):
    """Metre UVs on a mesh object (call it on the final mesh: modifiers are not applied). Faces in the motif slot
    keep the UVs they have (motif_* gave them)."""
    keep = _kept_indices(obj)
    kept = {}
    if keep and obj.data.uv_layers:
        uv = obj.data.uv_layers[0].data
        for p in obj.data.polygons:
            if p.material_index in keep:
                kept[p.index] = [tuple(uv[li].uv) for li in p.loop_indices]
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.data.uv_layers.new(name="UVMap")
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    parts = rcy_kit.surface_uvs(bm, salt)
    bm.transform(obj.matrix_world.inverted())
    bm.to_mesh(obj.data)
    bm.free()
    if kept:
        uv = obj.data.uv_layers[0].data
        for pi, uvs in kept.items():
            for li, u in zip(obj.data.polygons[pi].loop_indices, uvs):
                uv[li].uv = u
    return parts




# ------------------------------------------------------------------ shapes

def new_object(name, bm_or_mesh, collection=None, slots=()):
    if isinstance(bm_or_mesh, bmesh.types.BMesh):
        me = bpy.data.meshes.new(name)
        bm_or_mesh.to_mesh(me)
        bm_or_mesh.free()
    else:
        me = bm_or_mesh
    ob = bpy.data.objects.new(name, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    for s in slots:
        me.materials.append(slot(s))
    return ob


def lathe(profile, segments=48, close_top=False, close_bottom=False, mat=0, angle=360.0, start=0.0, smooth=True):
    """A turned shape round Blender z: profile = [(radius, height), ...] from the bottom up (a moulding's section).
    mat: one slot index, or a list (one per profile span). Returns a bmesh."""
    bm = bmesh.new()
    full = abs(angle - 360.0) < 1e-6
    count = segments if full else segments + 1
    rings = []
    for i in range(count):
        t = math.radians(start + angle * i / segments)
        c, s = math.cos(t), math.sin(t)
        rings.append([bm.verts.new((r * c, r * s, z)) for r, z in profile])
    span = len(profile) - 1
    for i in range(segments):
        r0, r1 = rings[i], rings[(i + 1) % count]
        for j in range(span):
            if profile[j][0] < 1e-6 and profile[j + 1][0] < 1e-6:
                continue
            vs = [r0[j], r1[j], r1[j + 1], r0[j + 1]]
            if profile[j][0] < 1e-6:
                vs = [r0[j], r1[j + 1], r0[j + 1]]
            elif profile[j + 1][0] < 1e-6:
                vs = [r0[j], r1[j], r0[j + 1]]
            try:
                f = bm.faces.new(vs)
            except ValueError:
                continue
            f.material_index = mat[j] if isinstance(mat, (list, tuple)) else mat
            f.smooth = smooth
    if full:
        for close, j in ((close_bottom, 0), (close_top, span)):
            if close and profile[j][0] > 1e-6:
                ring = [rg[j] for rg in rings]
                f = bm.faces.new(ring if j == span else list(reversed(ring)))
                f.material_index = mat[min(j, span - 1)] if isinstance(mat, (list, tuple)) else mat
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.normal_update()
    return bm


def star_outline(points=5, outer=1.0, inner=.4, turn=90.0):
    """A star's outline (x, y) round the origin, its first point at `turn` degrees (90: along +y)."""
    pts = []
    for i in range(points * 2):
        r = outer if i % 2 == 0 else inner
        t = math.radians(turn + 180.0 * i / points)
        pts.append((r * math.cos(t), r * math.sin(t)))
    return pts


def prism(outline, z0, z1, mat=0, bm=None, cap=True):
    """Extrude an outline (x, y; any winding) from z0 to z1 into `bm` (new if None). Returns the bmesh."""
    bm = bm or bmesh.new()
    area = sum(outline[i][0] * outline[(i + 1) % len(outline)][1] - outline[(i + 1) % len(outline)][0] * outline[i][1]
               for i in range(len(outline)))
    pts = outline if area > 0 else list(reversed(outline))
    lo = [bm.verts.new((x, y, z0)) for x, y in pts]
    hi = [bm.verts.new((x, y, z1)) for x, y in pts]
    n = len(pts)
    for i in range(n):
        f = bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
        f.material_index = mat
    if cap:
        bm.faces.new(hi).material_index = mat
        bm.faces.new(list(reversed(lo))).material_index = mat
    bm.normal_update()
    return bm


def beads(radius, count, bead, z, mat=0, bm=None, rings=6, segs=8):
    """A string of `count` beads (UV spheres `bead` across) on a circle of `radius` at height z: the beaded borders of
    the concept's rims. Returns the bmesh."""
    bm = bm or bmesh.new()
    for i in range(count):
        t = 2 * math.pi * i / count
        m = Matrix.Translation((radius * math.cos(t), radius * math.sin(t), z))
        got = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=bead * .5, matrix=m)
        for f in {f for v in got["verts"] for f in v.link_faces}:
            f.material_index = mat
            f.smooth = True
    return bm


def bevel(obj, width=.02, segments=3, angle=30.0, harden=True):
    """The round bevel on edges sharper than `angle`, with weighted normals so flat faces stay flat. A modifier:
    export applies it."""
    m = obj.modifiers.new("Bevel", "BEVEL")
    m.width = width
    m.segments = segments
    m.limit_method = "ANGLE"
    m.angle_limit = math.radians(angle)
    m.harden_normals = harden
    m.miter_outer = "MITER_ARC"
    try:
        obj.data.shade_smooth()
    except AttributeError:
        for p in obj.data.polygons:
            p.smooth = True
    w = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    w.keep_sharp = True
    return m


def apply_modifiers(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = obj.data
    obj.modifiers.clear()
    obj.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def join(objs, name):
    """Join mesh objects into one (modifiers applied first), slots merged by name."""
    objs = [apply_modifiers(o) for o in objs]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    mats = ob.data.materials
    first = {}
    for i, m in enumerate(mats):
        first.setdefault(m.name, i)
    for p in ob.data.polygons:
        p.material_index = first[mats[p.material_index].name]
    for i in reversed(range(len(mats))):
        if first[mats[i].name] != i:
            mats.pop(index=i)
    return ob


def tris(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    return sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons) for o in objs if o.type == "MESH")


# ------------------------------------------------------------------ the graybox (bh_blockout.json from Unity)

_dump = None


def dump():
    """The graybox as Unity built it (BanquetBlockoutDump.cs): {"nodes": [...], "meshes": [...]}."""
    global _dump
    if _dump is None:
        if not os.path.exists(DUMP):
            raise RuntimeError("no %s: open Unity once (it writes it after a compile) or use ChessFight > Pawn Rush > "
                               "Grand Banquet Hall > Dump Blockout For Blender" % DUMP)
        with open(DUMP, encoding="utf-8") as f:
            _dump = json.load(f)
    return _dump


def unity_matrix(node):
    m = node["matrix"]
    return Matrix([m[0:4], m[4:8], m[8:12], m[12:16]])


def blender_matrix(node):
    """A node's section-frame matrix in Blender's axes (scale kept)."""
    return C @ unity_matrix(node) @ C


def node(name, index=0):
    """A graybox node by its name, or by its path when the name has a '/'. index: the index-th of that name."""
    key = "path" if "/" in name else "name"
    hits = [n for n in dump()["nodes"] if n[key] == name]
    if not hits:
        raise KeyError("no graybox object %r in bh_blockout.json" % name)
    return hits[index]


def frame(name, index=0):
    """Blender matrix (location + rotation, no scale) of a graybox object."""
    loc, rot, _ = blender_matrix(node(name, index)).decompose()
    return Matrix.LocRotScale(loc, rot, None)


def at(unity_xyz):
    """A Unity section point as a Blender Vector."""
    x, y, z = unity_xyz
    return Vector((x, z, y))


# ------------------------------------------------------------------ export

def _guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def _write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def check_slots(meshes, model):
    bad = sorted({o.name for o in meshes for m in o.data.materials
                  if m is None or (m.name not in SLOTS)})
    if bad:
        raise RuntimeError("export %s: faces outside bh_kit.SLOTS on %s" % (model, bad[:8]))


def export(collection_name, model, zone, frame_name=None, frame_index=0):
    """Every mesh of the collection (modifiers applied) to Models/<zone>/<model>.fbx; see the module text."""
    for rel in ("Assets/Resources/BanquetHall", OUT_REL, OUT_REL + "/" + zone):
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
            _write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (_guid(rel), FOLDER_META))
    col = bpy.data.collections[collection_name]
    meshes = [o for o in col.all_objects if o.type == "MESH"]
    check_slots(meshes, model)
    to_local = frame(frame_name, frame_index).inverted() if frame_name else Matrix.Identity(4)
    saved = {o: o.matrix_world.copy() for o in meshes}
    rel = "%s/%s/%s.fbx" % (OUT_REL, zone, model)
    # Every face goes out as triangles (11-19: Unity dropped 20 of the entry gate's concave n-gons - a cap of an
    # arched or starred outline - as "self-intersecting"; Blender's own triangulation cuts them right). A modifier at
    # the end of each stack for the export only: the .blend keeps its n-gons.
    tri_mods = []
    for o in meshes:
        tm = o.modifiers.new("bh_kit export triangulate", "TRIANGULATE")
        tm.quad_method = "BEAUTY"
        tm.ngon_method = "BEAUTY"
        tm.keep_custom_normals = True
        tri_mods.append((o, tm))
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
        bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, rel), use_selection=True, object_types={"MESH"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                                 bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                                 path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    finally:
        for o, m in saved.items():
            o.matrix_world = m
        for o, tm in tri_mods:
            o.modifiers.remove(tm)
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        with open(FBX_META_TEMPLATE, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + _guid(rel)
        _write(rel + ".meta", "\n".join(lines))
    print("[bh_kit] exported", rel, len(meshes), "objects", tris(meshes), "tris", "frame", frame_name or "section")
    return rel


def write_lamps(zone, model, points, frame_name=None, frame_index=0):
    """The lamp globes' centres (Blender points) beside the FBX: Models/<zone>/<model>_lamps.txt, one "x y z" a line in
    Unity axes, in the frame the model was exported in. GrandBanquetHallDressing puts a light at each (light is Unity's,
    never baked into a model)."""
    to_local = frame(frame_name, frame_index).inverted() if frame_name else Matrix.Identity(4)
    lines = []
    for p in points:
        q = to_local @ Vector(p)
        lines.append("%.4f %.4f %.4f" % (q.x, q.z, q.y))
    rel = "%s/%s/%s_lamps.txt" % (OUT_REL, zone, model)
    _write(rel, "\n".join(lines) + "\n")
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        _write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n"
                              "  assetBundleName: \n  assetBundleVariant: \n" % _guid(rel))
    return rel
