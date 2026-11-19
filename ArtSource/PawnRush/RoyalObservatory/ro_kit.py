"""Royal Observatory modelling kit (R0, seoungtest R725, 2026-11-19): what every zone's model script shares.

    import sys; sys.path.insert(0, r"<this folder>"); import ro_kit as k

The zones read this; only R0 (the ro-path session) changes it, and never renames what is here (PLAN.md 2 and 3).

  - SLOTS / slot(): the material slots. A model's faces carry "RO | marble", "RO | gold" ...; the Unity side
    (RoyalObservatoryDressing.cs) draws each with Resources/RoyalObservatory/Materials/RO_<Name>. The colours here only
    preview them. Planet surfaces: slot("planet_<name>") makes "RO | planet_<name>" -> RO_Planet<Name> on demand.
  - surface_uvs(obj): metre UVs, one connected part at a time (the carriage yard's rcy_kit.surface_uvs: U along the
    part's longest side, a random offset a part); the library textures are seamless tiles in metres. Faces in the
    "motif" slot keep the atlas UVs motif_* gave them.
  - motif_panels / motif_disc / motif_planar: the floor-motif atlas (PLAN 1-2: every panel a different motif, chosen
    by a hash of the Unity object's name, never the same as a neighbour, turned 0/90/180/270 where the motif allows).
  - bevel(obj, w): the round bevel every hard edge gets (weighted normals so the faces stay flat).
  - lathe(), star_outline(), prism(), beads(): the shapes the kit (ro_parts.py) and the zones build from.
  - frame(name): the pose of a built graybox object (ro_blockout.json, dumped from Unity), for models that hang
    under a moving Device_*.
  - export(collection, model, zone, frame=None): FBX to Assets/Resources/RoyalObservatory/Models/<zone>/<model>.fbx
    with a .meta of a fixed GUID. frame = the graybox object the model hangs under (its name or path, e.g.
    "Device_OrbitRing 1"): written in that object's frame (position and rotation, not scale); None = the section's
    frame (static pieces).

Axes: Unity section frame (x east, y up, z north, metres, the entry's threshold at 0) -> Blender (x, z up, y north):
Blender (x, y, z) = Unity (x, z, y). Build at the blockout's places and sizes (ro_blockout.py); walking faces and
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

DUMP = os.path.join(HERE, "ro_blockout.json")
OUT_REL = "Assets/Resources/RoyalObservatory/Models"
FBX_META_TEMPLATE = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
FOLDER_META = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # swaps Unity y / z (its own inverse)

# ------------------------------------------------------------------ material slots (names fixed: PLAN.md 2)
# slot -> (library material in Unity, preview colour (linear) in Blender, metallic, roughness)
SLOTS = {
    "RO | marble": ("RO_Marble", (.78, .70, .58, 1), 0.0, .25),        # cream marble, soft grey veins
    "RO | gold": ("RO_Gold", (.83, .55, .16, 1), 1.0, .22),            # polished gold, engraved filigree
    "RO | brass": ("RO_Brass", (.55, .34, .10, 1), 1.0, .38),          # aged brushed brass, darker
    "RO | navy": ("RO_NavyEnamel", (.012, .022, .085, 1), 0.0, .18),   # deep navy enamel, gold flecks
    "RO | violet": ("RO_VioletEnamel", (.10, .03, .22, 1), 0.0, .18),  # royal violet enamel, gold flecks
    "RO | pearl": ("RO_Pearl", (.86, .82, .74, 1), 0.0, .15),          # pearl / ivory polished stone: lamp globes' bodies, trims
    "RO | glass": ("RO_Glass", (.85, .90, 1.0, 1), 0.0, .03),          # clear glass (transparent in Unity, no texture)
    "RO | hallstone": ("RO_HallStone", (.035, .035, .05, 1), 0.0, .45),  # dark slate hall floor stone, gold inlay lines
    "RO | velvet": ("RO_Velvet", (.09, .015, .16, 1), 0.0, .85),       # royal violet velvet (banners, held still)
    "RO | motif": ("RO_Motif", (.03, .04, .14, 1), 0.0, .2),           # the floor-motif atlas (enamel + gold inlay)
    # R6 11-19 (ro-shell): the dome's sky, one painted texture (textures/r6/RO_DomeSky_Albedo.png) over the shell,
    # UVs made by the shell's script (u = azimuth / 360, v = altitude / 90) - kept by surface_uvs like the motif's
    "RO | sky": ("RO_DomeSky", (.02, .05, .16, 1), 0.0, .6),
    # R6 11-19 (ro-shell): the night sky beyond the dome's slit, the same painted texture, unlit in Unity (RO_NightSky)
    "RO | night": ("RO_NightSky", (.02, .05, .16, 1), 0.0, 1.0),
    # R3 11-19 (ro-hall, the user on the globe's grid: "좌표(###)를 검은색으로 해"): glossy black enamel, colour only
    "RO | black": ("RO_BlackEnamel", (.012, .012, .014, 1), 0.0, .3),
}
KEEP_UVS = ("RO | motif", "RO | sky", "RO | night")   # slots whose faces carry their own UVs (surface_uvs leaves them)
PLANET_PREFIX = "RO | planet_"


def slot_key(name):
    return name if name.startswith("RO | ") else "RO | " + name


def unity_material(key):
    """The Unity material a slot draws with."""
    if key.startswith(PLANET_PREFIX):
        return "RO_Planet" + "".join(w.capitalize() for w in key[len(PLANET_PREFIX):].split("_"))
    return SLOTS[key][0]


def slot(name):
    """The Blender material for a slot ("gold" or "RO | gold"; "planet_<name>" for a planet surface)."""
    key = slot_key(name)
    if key not in SLOTS and not key.startswith(PLANET_PREFIX):
        raise KeyError("unknown slot %s (ro_kit.SLOTS: %s, or planet_<name>)" % (key, ", ".join(SLOTS)))
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        colour, metal, rough = SLOTS[key][1:] if key in SLOTS else ((.4, .3, .2, 1), 0.0, .5)
        p.inputs["Base Color"].default_value = colour
        p.inputs["Metallic"].default_value = metal
        p.inputs["Roughness"].default_value = rough
        if key == "RO | glass":
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


# ------------------------------------------------------------------ UVs

def _motif_index(obj):
    for i, m in enumerate(obj.data.materials):
        if m is not None and m.name == "RO | motif":
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


# ------------------------------------------------------------------ the floor-motif atlas (PLAN.md 1-2)
# One texture (ro_textures.py builds it: RO_Motif_Albedo / _Normal / _MetallicSmoothness, the gold alone metallic and
# bright). ATLAS_COLS x ATLAS_ROWS cells of the same size, cell 0 top left, row by row. A UV rectangle a cell, inset by
# CELL_INSET of a cell so mip levels do not bleed the neighbour in.
ATLAS_COLS, ATLAS_ROWS = 4, 5
CELL_INSET = .012
# Cells by kind. SQUARE: the twelve walkway panels (RO_T1, in its order); ROUND: the six star-platform tops (RO_T2, a
# gold five-pointed star in each middle); STAGE: the observing stage's sun (RO_T2's wide disc); PLAIN: enamel with
# flecks only, for filler where a motif would be cut too small.
SQUARE = list(range(0, 12))
ROUND = list(range(12, 18))
STAGE = 18
PLAIN = 19
# The square motifs that may be turned a quarter (a compass, a scatter, a lattice ...). The constellations, the comet
# and the crown have an up; they turn only half (180) or not at all.
TURN_ANY = {0, 1, 5, 6, 7, 9, 10}
TURN_HALF = {4, 8}           # crescent moon, star scatter: upside down reads as well
# the rest (2 Big Dipper, 3 Cassiopeia, 11 crown) keep their up along the walk

_registry = []   # (kind, cell, centre Vector, radius) of the panels given cells this run: the neighbours to avoid


def motif_reset():
    """Forget the panels placed so far (a new model script run)."""
    _registry.clear()


def _hash(text):
    return int(hashlib.md5(text.encode("utf-8")).hexdigest()[:12], 16)


def cell_rect(cell):
    """(u0, v0, u1, v1) of a cell, inset. UV v grows up; cell 0 is the image's top left."""
    col, row = cell % ATLAS_COLS, cell // ATLAS_COLS
    du, dv = 1.0 / ATLAS_COLS, 1.0 / ATLAS_ROWS
    u0, v1 = col * du, 1.0 - row * dv
    iu, iv = CELL_INSET * du, CELL_INSET * dv
    return u0 + iu, v1 - dv + iv, u0 + du - iu, v1 - iv


def pick_cell(key, kind, centre, reach=4.5, avoid=()):
    """The cell for one panel: the order of the kind's cells shuffled by the hash of `key` (the Unity object's name
    and the panel's number), the first one no registered panel within `reach` metres already shows and not in
    `avoid`. Registers the choice. centre: Blender Vector."""
    cells = {"square": SQUARE, "round": ROUND}.get(kind)
    if cells is None:
        cell = STAGE if kind == "stage" else PLAIN
        _registry.append((kind, cell, Vector(centre), 0.0))
        return cell
    h = _hash(key)
    order = sorted(cells, key=lambda c: _hash("%d:%d" % (h, c)))
    near = {c for (k, c, at, r) in _registry if (Vector(at) - Vector(centre)).length < reach + r}
    for c in order:
        if c not in near and c not in avoid:
            break
    else:
        c = order[0]
    _registry.append((kind, c, Vector(centre), 0.0))
    return c


def pick_turn(key, cell, kind):
    """Degrees the motif is turned: any angle on a round top, quarters / halves / none on a square one."""
    h = _hash("turn:" + key)
    if kind == "round":
        return (h % 3600) / 10.0
    if cell in TURN_ANY:
        return 90.0 * (h % 4)
    if cell in TURN_HALF:
        return 180.0 * (h % 2)
    return 0.0


def _faces_of(obj, faces):
    if faces is None:
        return [p.index for p in obj.data.polygons]
    if callable(faces):
        return [p.index for p in obj.data.polygons if faces(obj, p)]
    return list(faces)


def top_faces(min_up=.7):
    """A face selector: faces looking up (world) at least `min_up`."""
    def sel(obj, p):
        n = obj.matrix_world.to_3x3() @ p.normal
        return n.normalized().z >= min_up
    return sel


def motif_planar(obj, faces, cell, centre, u_axis, v_axis, size_u, size_v, turn=0.0):
    """Project faces (world space) onto a cell: the rectangle size_u x size_v around `centre` (along u_axis, v_axis,
    Blender vectors) fills the cell, turned `turn` degrees about the cell's middle. The faces go into the motif slot."""
    mi = _motif_index(obj)
    if mi < 0:
        obj.data.materials.append(slot("motif"))
        mi = len(obj.data.materials) - 1
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    uv = obj.data.uv_layers[0].data
    u0, v0, u1, v1 = cell_rect(cell)
    cu, cv = (u0 + u1) * .5, (v0 + v1) * .5
    hu, hv = (u1 - u0) * .5, (v1 - v0) * .5
    ca, sa = math.cos(math.radians(turn)), math.sin(math.radians(turn))
    ua, va = Vector(u_axis).normalized(), Vector(v_axis).normalized()
    mw = obj.matrix_world
    verts = obj.data.vertices
    for pi in _faces_of(obj, faces):
        p = obj.data.polygons[pi]
        p.material_index = mi
        for li in p.loop_indices:
            w = mw @ verts[obj.data.loops[li].vertex_index].co - Vector(centre)
            x, y = w.dot(ua) / (size_u * .5), w.dot(va) / (size_v * .5)   # -1..1 across the panel
            x, y = x * ca - y * sa, x * sa + y * ca
            uv[li].uv = (cu + hu * max(-1.0, min(1.0, x)), cv + hv * max(-1.0, min(1.0, y)))


def motif_disc(obj, key, centre, radius, faces=None, kind="round", avoid=()):
    """A round top: the faces (default: all that look up) seen from above, the disc of `radius` round `centre`
    filling a round cell (or kind="stage" / "plain"), turned at the hash's angle. key: the Unity object's name.
    Returns the cell."""
    centre = Vector(centre)
    cell = pick_cell(key, kind, centre, reach=radius + 3.0, avoid=avoid)
    _registry[-1] = (kind, cell, centre, radius)
    motif_planar(obj, faces if faces is not None else top_faces(), cell, centre, (1, 0, 0), (0, 1, 0),
                 radius * 2.0, radius * 2.0, pick_turn(key, cell, kind) if kind == "round" else 0.0)
    return cell


def motif_panels(obj, key, start, end, width, panel=3.0, faces=None, up=(0, 0, 1)):
    """A long walking floor (a constellation line, a threshold, the balcony) cut into panels of about `panel` metres
    from `start` to `end` (Blender points on the floor's middle line, its top), each panel its own square motif, no
    two neighbours alike, each turned as its motif allows. The mesh is cut across at every panel's edge (bisect), so
    call it on the floor's top before bevelling. faces: which faces take the motif (default: those looking up).
    Returns the cut positions (Blender points on the middle line, the inner ones) for the gold seam strips, and the
    cells chosen."""
    start, end = Vector(start), Vector(end)
    along = end - start
    length = along.length
    a = along.normalized()
    side = a.cross(Vector(up)).normalized()
    n = max(1, int(round(length / panel)))
    step = length / n
    # cut the mesh across at the inner edges
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    inv = obj.matrix_world.inverted()
    cuts = []
    for i in range(1, n):
        at = start + a * (step * i)
        cuts.append(at)
        co = inv @ at
        no = (inv.to_3x3() @ a).normalized()
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    sel = faces if faces is not None else top_faces()
    chosen = []
    mw = obj.matrix_world
    picked = _faces_of(obj, sel)
    for i in range(n):
        mid = start + a * (step * (i + .5))
        k = "%s#%d" % (key, i)
        cell = pick_cell(k, "square", mid, reach=step * 1.05, avoid=chosen[-1:])
        chosen.append(cell)
        mine = [pi for pi in picked
                if -1e-4 <= (mw @ obj.data.polygons[pi].center - start).dot(a) - step * i <= step + 1e-4]
        motif_planar(obj, mine, cell, mid, a, side, step, max(width, .01), pick_turn(k, cell, "square"))
    return cuts, chosen


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


# ------------------------------------------------------------------ the graybox (ro_blockout.json from Unity)

_dump = None


def dump():
    """The graybox as Unity built it (RoyalObservatoryBlockoutDump.cs): {"nodes": [...], "meshes": [...]}."""
    global _dump
    if _dump is None:
        if not os.path.exists(DUMP):
            raise RuntimeError("no %s: open Unity once (it writes it after a compile) or use ChessFight > Pawn Rush > "
                               "Royal Observatory > Dump Blockout For Blender" % DUMP)
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
        raise KeyError("no graybox object %r in ro_blockout.json" % name)
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
                  if m is None or (m.name not in SLOTS and not m.name.startswith(PLANET_PREFIX))})
    if bad:
        raise RuntimeError("export %s: faces outside ro_kit.SLOTS on %s" % (model, bad[:8]))


def export(collection_name, model, zone, frame_name=None, frame_index=0):
    """Every mesh of the collection (modifiers applied) to Models/<zone>/<model>.fbx; see the module text."""
    for rel in ("Assets/Resources/RoyalObservatory", OUT_REL, OUT_REL + "/" + zone):
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
        tm = o.modifiers.new("ro_kit export triangulate", "TRIANGULATE")
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
    print("[ro_kit] exported", rel, len(meshes), "objects", tris(meshes), "tris", "frame", frame_name or "section")
    return rel


def write_lamps(zone, model, points, frame_name=None, frame_index=0):
    """The lamp globes' centres (Blender points) beside the FBX: Models/<zone>/<model>_lamps.txt, one "x y z" a line in
    Unity axes, in the frame the model was exported in. RoyalObservatoryDressing puts a light at each (light is Unity's,
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
