# A Ceremonial Court (R506): the user's three Tripo FBX (made from Docs/PawnRush/ConceptArt/R506_CeremonialCourt/AssetRefs)
# split into single assets, scaled to their in-game sizes and lowered to a per-asset triangle budget
# (Source/R506_A_README.md). The user, 11-07: "Blender로 고쳐" — the four broken pieces are repaired here:
#   - Tower:    the crumbled lower body (holes under the window) is cut off and rebuilt as a straight stone cylinder
#               with a plinth, in the phase-1 stone brick (R502 PR_Early_StoneBrick);
#   - GatePost: the holed base is replaced by a bevelled stone box, same material;
#   - Bench:    Tripo made a one-legged mushroom; a new bench (seat slab on two block legs) in R506 ivory limestone;
#   - Pots:     both came cream; the paint surface and the drips get their own white / black paint material.
#   - small holes Tripo tore in stone faces are closed with mg_holes (facade tower, walls, arch, tower, post).
# Also: the facade's tower windows see through the hollow towers, so a dark core stands inside each tower; the team door
# is stretched to the game's leaf (3.85 x 4.55 x 0.3 m, MiniGamePalaceArt.DressGate); the props FBX faced +Y and is turned.
# Origins: bottom centre (front = Blender -Y); the facade, team arch and walls at their front face (FRONT_ORIGIN); the team
# door at the centre of the leaf, like the game's door box. The facade is 17.6 m wide and the arch piers 0.9 m, so the
# centre block (x +-8.8) and the team arches (x +-12) overlap as little as D's.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python a_props_v1.py -- [--render] [--export]
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mg_holes   # closes Tripo's small face holes (R505, shared with the D scripts)

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

FACADE = "R506_A_Facade_StoneCastleGate"
STRUCTURE = "R506_A_Structure_StoneCastleAssets"
PROPS = "R506_A_Props_StoneFlowerPlanter"


# Assets left to right as seen from the front, split at the empty X gaps between them (a_inspect_v1.py finds the same).
STRUCTURE_ORDER = ["TeamArch", "TeamDoor", "Wall", "Tower", "GardenWall", "GatePost"]
PROPS_ORDER = ["PaintRack", "PotWhite", "PotBlack", "Carpet", "Bench", "Planter", "Flagpole", "Cypress"]   # after the turn


# name: (size axis, metres, verdict, target triangles). Sizes are first estimates; placing them in Unity may adjust.
PLAN = {
    "Facade":     ("x", 17.6, "lower + dark tower cores", 25000),
    "TeamArch":   ("opening", 4.5, "lower + narrow piers", 4000),
    "TeamDoor":   ("door", 0, "lower + stretched", 1500),
    "Wall":       ("z", 5.0, "lower", 1500),
    "Tower":      ("z", 8.0, "lower + rebuilt base", 4000),
    "GardenWall": ("z", 1.1, "lower", 800),
    "GatePost":   ("z", 3.0, "lower + rebuilt base", 1500),
    "Cypress":    ("z", 4.5, "lower", 3000),
    "Flagpole":   ("z", 4.4, "lower", 1000),
    "Planter":    ("x", 2.0, "lower", 5000),
    "Bench":      ("x", 1.8, "new", 0),
    "Carpet":     ("x", 2.4, "lower", 2500),
    "PotWhite":   ("z", .6, "lower + paint", 1500),
    "PotBlack":   ("z", .6, "lower + paint", 1500),
    "PaintRack":  ("x", 1.4, "lower", 3000),
}
DOOR = (3.85, .30, 4.55)
# Placed by their front face (y = 0 at the front-most point, the body behind it): these stand against a line in the
# section (the board's north edge z 24, the walls' inner faces), so nothing pokes into the play area.
FRONT_ORIGIN = {"Facade", "TeamArch", "Wall", "GardenWall"}
# Walls stand by their foot: their upper course juts 0.36 m (wall) / 0.21 m (garden wall) out past the foot, so measured
# by the front-most point the foot stood that far back from the floor's edge and the void showed between (user, 11-07:
# "틈이 있어 아직"). Their y = 0 is the foot's face (lowest 10 %); the upper course overhangs the room a little.
BASE_FRONT = {"Wall", "GardenWall"}
PIER = .9             # team arch piers narrowed to this width (as D's d_team_gate_v1.py), so the arch is ~6.3 m wide
TOWER_CUT = .215      # fraction of the tower's height under which Tripo's body crumbled (just under the window sill)
POST_CUT = .145       # fraction of the gate post's height taken by its holed base
DRIP_OUT = .035       # a drip stands this far (fraction of the pot's radius) out of the body
HOLES = {"Facade": 6., "TeamArch": 3., "Wall": 3., "Tower": 3., "GardenWall": 1., "GatePost": 1.}   # max hole perimeter (m)
TEAR = (.56, .80)                  # height band of the facade towers searched for the black tear
CORE_DARK, CORE_TOP = .56, .76   # facade tower cores: dark below, stone above (fractions of the height)
LIP = .86             # the pot's lip band (fraction of its height) is painted all round


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def load(name):
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"))
    ob = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    ob.name = name
    return ob


def gap_segments(me, bins=2000):
    """X ranges of the assets standing in a row: runs of X covered by an edge, separated by empty gaps."""
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get("co", co); x = co.reshape(-1, 3)[:, 0]
    lo, hi = float(x.min()), float(x.max())
    ev = np.empty(len(me.edges) * 2, np.int64); me.edges.foreach_get("vertices", ev); ev = ev.reshape(-1, 2)
    i0 = ((x[ev[:, 0]] - lo) / (hi - lo) * (bins - 1)).astype(int); i1 = ((x[ev[:, 1]] - lo) / (hi - lo) * (bins - 1)).astype(int)
    occ = np.zeros(bins, bool)
    for a, b in zip(np.minimum(i0, i1), np.maximum(i0, i1)): occ[a:b + 1] = True
    segs, start = [], None
    for i, o in enumerate(occ):
        if o and start is None: start = i
        if not o and start is not None: segs.append((start, i - 1)); start = None
    if start is not None: segs.append((start, bins - 1))
    to_x = lambda i: lo + (hi - lo) * i / (bins - 1)
    return [(to_x(a) - 1e-6, to_x(b) + 1e-6) for a, b in segs]


def split(ob, names, turn=False):
    if turn:                                   # front +Y -> -Y (a half turn about Z keeps it right-handed)
        ob.data.transform(Matrix.Rotation(math.pi, 4, 'Z'))
    segs = gap_segments(ob.data)
    if len(segs) != len(names):
        raise RuntimeError("%s: %d assets found, %d expected" % (ob.name, len(segs), len(names)))
    def rule(c):
        return names[min(range(len(segs)), key=lambda i: 0 if segs[i][0] <= c.x <= segs[i][1] else min(abs(c.x - segs[i][0]), abs(c.x - segs[i][1])))]
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    seen = np.zeros(len(bm.faces), bool); groups = {}
    for f in bm.faces:
        if seen[f.index]: continue
        stack = [f]; seen[f.index] = True; isl = []
        while stack:
            g = stack.pop(); isl.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if not seen[h.index]: seen[h.index] = True; stack.append(h)
        c = sum((bm.faces[i].calc_center_median() for i in isl), Vector()) / len(isl)
        groups.setdefault(rule(c), []).extend(isl)
    out = {}
    for key, idx in groups.items():
        keep = set(idx)
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new("A_" + key); b2.to_mesh(me); b2.free()
        for m in ob.data.materials: me.materials.append(m)
        o = bpy.data.objects.new("A_" + key, me); bpy.context.scene.collection.objects.link(o)
        out[key] = o
    bm.free(); bpy.data.objects.remove(ob)
    return out


def verts(o):
    a = np.empty(len(o.data.vertices) * 3, np.float32)
    o.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def bounds(o):
    v = verts(o)
    return Vector(v.min(0)), Vector(v.max(0))


def to_origin(o, k=(1., 1., 1.)):
    """Bottom centre to the origin, then scale by k (per axis)."""
    lo, hi = bounds(o)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    o.data.transform(Matrix.Diagonal((*k, 1.)) @ Matrix.Translation(-c))


def front_of(o, foot):
    """Blender y of the front face: the front-most point, or for a wall the face of its foot (lowest 10 %)."""
    v = verts(o)
    if foot:
        lo, hi = v[:, 2].min(), v[:, 2].max()
        v = v[v[:, 2] < lo + .1 * (hi - lo)]
    return float(v[:, 1].min())


def opening_edges(o):
    """X of the opening's two jambs: the widest empty gap between vertices in a band at 20-35 % of the height."""
    v = verts(o); lo, hi = v.min(0), v.max(0); h = hi[2] - lo[2]; w = hi[0] - lo[0]
    band = v[(v[:, 2] > lo[2] + .2 * h) & (v[:, 2] < lo[2] + .35 * h)][:, 0]
    band = np.sort(band[(band > lo[0] + .1 * w) & (band < hi[0] - .1 * w)])
    i = int(np.argmax(np.diff(band)))
    return float(band[i]), float(band[i + 1])


def narrow_piers(o):
    """Squeeze everything outside the opening toward it so each pier is PIER wide; the opening keeps its 4.5 m."""
    a, b = opening_edges(o)
    lo, hi = bounds(o)
    sl, sr = PIER / (a - lo.x), PIER / (hi.x - b)
    for v in o.data.vertices:
        if v.co.x < a: v.co.x = a - (a - v.co.x) * sl
        elif v.co.x > b: v.co.x = b + (v.co.x - b) * sr
    for v in o.data.vertices: v.co.x -= (a + b) / 2
    lo, hi = bounds(o)
    print("ARCH piers -> %.2f m each, width %.2f m, opening %.2f m" % (PIER, hi.x - lo.x, b - a))


def opening_width(o):
    """Widest empty X gap between vertices in a band at 20-35 % of the height, inside the middle 80 % of the width."""
    v = verts(o); lo, hi = v.min(0), v.max(0); h = hi[2] - lo[2]; w = hi[0] - lo[0]
    band = v[(v[:, 2] > lo[2] + .2 * h) & (v[:, 2] < lo[2] + .35 * h)][:, 0]
    band = np.sort(band[(band > lo[0] + .1 * w) & (band < hi[0] - .1 * w)])
    gaps = np.diff(band)
    return float(gaps.max()) if len(gaps) else w


def fit(o, name):
    axis, size, _, _ = PLAN[name]
    lo, hi = bounds(o); e = hi - lo
    if axis == "door":
        k = (DOOR[0] / e.x, DOOR[1] / e.y, DOOR[2] / e.z)
    elif axis == "opening":
        s = size / opening_width(o); k = (s, s, s)
    else:
        s = size / (e.x if axis == "x" else e.z); k = (s, s, s)
    to_origin(o, k)
    return k


def modifier(o, kind, **kw):
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new(kind, kind)
    for a, b in kw.items(): setattr(m, a, b)
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier=m.name)


def lower_to(o, target):
    modifier(o, 'WELD', merge_threshold=.002)
    if tris(o) > target:
        modifier(o, 'DECIMATE', ratio=target / tris(o), use_collapse_triangulate=True)


# ------------------------------------------------------------------ materials
def tex_material(name, stem, rough=.75):
    """URP-like material from a baked swatch: base colour + normal (OpenGL)."""
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, stem + "_BaseColor.png"), check_existing=True)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(os.path.join(TEX, stem + "_Normal.png"), check_existing=True)
    n.image.colorspace_settings.name = 'Non-Color'
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(n.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = rough
    return m


def flat_material(name, hexcol, rough=.7):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    c = [int(hexcol[i:i + 2], 16) / 255. for i in (1, 3, 5)]
    c = [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = rough
    return m


def save_atlas(o, stem):
    """Tripo's embedded maps go to Materials/Textures under a stable name (resolution kept)."""
    mat = o.data.materials[0]
    for n in mat.node_tree.nodes:
        if n.type != 'TEX_IMAGE' or not n.image: continue
        kind = "BaseColor" if "base" in n.image.name.lower() else "Normal"
        img = n.image.copy(); img.name = "%s_%s" % (stem, kind)
        if kind == "Normal": img.colorspace_settings.name = 'Non-Color'
        img.filepath_raw = os.path.join(TEX, "%s_%s.png" % (stem, kind)); img.file_format = 'PNG'; img.save()
        n.image = img
    mat.name = stem
    return mat


def slot(o, mat):
    if mat.name not in [m.name for m in o.data.materials if m]:
        o.data.materials.append(mat)
    return [m.name for m in o.data.materials].index(mat.name)


def merge_bmesh(o, nb, mat):
    """Append the faces of bmesh nb to o, wearing mat."""
    s = slot(o, mat)
    for f in nb.faces: f.material_index = s
    bmesh.ops.recalc_face_normals(nb, faces=nb.faces)
    # the new UVs must sit in o's own UV layer (same name), or the merge leaves o's faces on an empty second layer
    want = o.data.uv_layers.active.name if o.data.uv_layers else "UVMap"
    src = nb.loops.layers.uv.active
    if src is not None and src.name != want:
        dst = nb.loops.layers.uv.new(want)
        for f in nb.faces:
            for l in f.loops: l[dst].uv = l[src].uv
        nb.loops.layers.uv.remove(src)
    me = bpy.data.meshes.new("part"); nb.to_mesh(me); nb.free()
    bm = bmesh.new(); bm.from_mesh(o.data); bm.from_mesh(me); bm.to_mesh(o.data); bm.free()
    bpy.data.meshes.remove(me)


def box_uv(bm, tile=2.):
    """World-metre box mapping: each face takes the two axes across its main normal, one tile per `tile` metres."""
    uvl = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for l in f.loops:
            l[uvl].uv = (l.vert.co[a] / tile, l.vert.co[b] / tile)


def bevel_box(lo, hi, width=.04, segments=2):
    me = bpy.data.meshes.new("box"); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co = Vector([lo[i] + (v.co[i] + .5) * (hi[i] - lo[i]) for i in range(3)])
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=width, segments=segments, affect='EDGES', profile=.5)
    return bm


def delete_faces(o, test):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if test(f.calc_center_median())], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()


# ------------------------------------------------------------------ repairs
def rebuild_tower_base(o, stone):
    """Cut the crumbled body under TOWER_CUT and put a straight cylinder + plinth there, reaching 6 cm into what stays."""
    lo, hi = bounds(o); h = hi.z - lo.z; cut = lo.z + TOWER_CUT * h
    v = verts(o)
    band = v[(v[:, 2] > cut) & (v[:, 2] < cut + .08 * h)]
    cx, cy = float(np.median(band[:, 0])), float(np.median(band[:, 1]))
    r = float(np.percentile(np.hypot(band[:, 0] - cx, band[:, 1] - cy), 90))
    delete_faces(o, lambda c: c.z < cut)
    nb = bmesh.new(); seg = 40
    for z0, z1, rr in ((lo.z, lo.z + .45, r * 1.08), (lo.z + .45, cut + .06, r * .985)):
        ring = bmesh.ops.create_cone(nb, cap_ends=True, segments=seg, radius1=rr, radius2=rr, depth=z1 - z0,
                                     matrix=Matrix.Translation((cx, cy, (z0 + z1) / 2)))
    # cylinder UVs: around in metres, up in metres, one tile per 2 m
    uvl = nb.loops.layers.uv.new("UVMap")
    for f in nb.faces:
        for l in f.loops:
            p = l.vert.co; a = math.atan2(p.y - cy, p.x - cx)
            if abs(f.normal.z) > .7: l[uvl].uv = (p.x / 2, p.y / 2)
            else: l[uvl].uv = (a * r / 2, p.z / 2)
    merge_bmesh(o, nb, stone)
    print("TOWER base: cut at %.2f m, radius %.2f, plinth %.2f" % (cut - lo.z, r, r * 1.08))


def rebuild_post_base(o, stone):
    lo, hi = bounds(o); h = hi.z - lo.z; cut = lo.z + POST_CUT * h
    v = verts(o)
    base = v[v[:, 2] < cut]
    shaft = v[(v[:, 2] > cut + .05 * h) & (v[:, 2] < cut + .25 * h)]
    sx = (shaft[:, 0].max() - shaft[:, 0].min()); sy = (shaft[:, 1].max() - shaft[:, 1].min())
    bx = min(base[:, 0].max() - base[:, 0].min(), sx * 1.35); by = min(base[:, 1].max() - base[:, 1].min(), sy * 1.35)
    cx, cy = float(np.median(shaft[:, 0])), float(np.median(shaft[:, 1]))
    delete_faces(o, lambda c: c.z < cut)
    nb = bevel_box((cx - bx / 2, cy - by / 2, lo.z), (cx + bx / 2, cy + by / 2, cut + .04), .04)
    box_uv(nb)
    merge_bmesh(o, nb, stone)
    print("POST base: %.2f x %.2f x %.2f m (shaft %.2f x %.2f)" % (bx, by, cut + .04 - lo.z, sx, sy))


def new_bench(o, stone):
    """Replace Tripo's one-legged bench by a seat slab on two block legs (same footprint, 0.45 m seat)."""
    lo, hi = bounds(o)
    w = hi.x - lo.x; d = min(hi.y - lo.y, .62)
    me = bpy.data.meshes.new("A_Bench"); o.data = me
    nb = bmesh.new()
    parts = [((-w / 2, -d / 2, .30), (w / 2, d / 2, .46), .035)] + \
            [((sx * w * .32 - .14, -d * .40, 0.), (sx * w * .32 + .14, d * .40, .31), .025) for sx in (-1, 1)]
    for a, b, bev in parts:
        part = bevel_box(a, b, bev)
        tmp = bpy.data.meshes.new("t"); part.to_mesh(tmp); part.free(); nb.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    box_uv(nb)
    merge_bmesh(o, nb, stone)


def paint_pot(o, paint):
    """Paint = the drips (runs standing out of the pot's body), the lip band and the paint surface inside the rim.
    Tripo painted both pots cream, so this goes by shape: in each height slice the body radius is the median radius of
    the sideways faces; a face more than DRIP_OUT of the pot's radius further out is a drip. Two majority passes over
    edge neighbours smooth the edge of the painted area."""
    lo, hi = bounds(o); hgt = hi.z - lo.z
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    cen = np.array([f.calc_center_median()[:] for f in bm.faces]); nor = np.array([f.normal[:] for f in bm.faces])
    rad = np.hypot(cen[:, 0] - cx, cen[:, 1] - cy); rmax = rad.max()
    zf = (cen[:, 2] - lo.z) / hgt
    side = np.abs(nor[:, 2]) < .5
    body = np.zeros(len(rad))
    for a in np.arange(0, 1, .05):
        m = (zf >= a) & (zf < a + .05)
        ref = rad[m & side]
        body[m] = np.median(ref) if len(ref) else rad[m].mean() if m.any() else 0
    drip = (zf > .40) & (rad > body + DRIP_OUT * rmax) & side
    lip = zf > LIP
    surface = (nor[:, 2] > .6) & (rad < rmax * .8) & (zf > .6)
    paint_f = drip | lip | surface
    for _ in range(2):
        nxt = paint_f.copy()
        for f in bm.faces:
            nb = [g.index for e in f.edges for g in e.link_faces if g is not f]
            if nb: nxt[f.index] = (paint_f[nb].sum() * 2 > len(nb)) if not (lip[f.index] or surface[f.index]) else True
        paint_f = nxt
    s = slot(o, paint)
    for f in bm.faces:
        if paint_f[f.index]: f.material_index = s
    bm.to_mesh(o.data); bm.free()
    print("POT %s: %d paint faces of %d (drip %d, lip %d, surface %d)" % (o.name, paint_f.sum(), len(rad), drip.sum(), lip.sum(), surface.sum()))


def tower_cores(o, dark, stone):
    """The facade's towers are hollow shells: their windows look through, and Tripo tore the right tower's wall above
    the window. A core stands inside each tower: dark up to just over the windows (a window reads as a dark room),
    stone brick above (the tear shows stone, not a black gap)."""
    lo, hi = bounds(o); w = hi.x - lo.x; h = hi.z - lo.z
    v = verts(o)
    dark_b, stone_b = bmesh.new(), bmesh.new()
    for side in (-1, 1):
        t = v[(side * v[:, 0] > w * .33) & (v[:, 2] < lo.z + .55 * h)]
        cx = (t[:, 0].max() + t[:, 0].min()) / 2; cy = (t[:, 1].max() + t[:, 1].min()) / 2
        r = min(t[:, 0].max() - t[:, 0].min(), t[:, 1].max() - t[:, 1].min()) / 2 * .72
        # the stone part sits just inside the tower's outer face, so a gap between Tripo's stones shows lit stone
        band = v[(side * v[:, 0] > w * .33) & (v[:, 2] > lo.z + CORE_DARK * h) & (v[:, 2] < lo.z + (CORE_DARK + .1) * h)]
        r_out = float(np.median(np.hypot(band[:, 0] - cx, band[:, 1] - cy)))
        for bm, z0, z1, rr in ((dark_b, 0., CORE_DARK, r), (stone_b, CORE_DARK, CORE_TOP, r_out * .94)):
            bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=rr, radius2=rr, depth=(z1 - z0) * h,
                                  matrix=Matrix.Translation((cx, cy, lo.z + (z0 + z1) / 2 * h)))
        print("FACADE tower core x %.2f y %.2f r %.2f, stone ring r %.2f (outer face %.2f)" % (cx, cy, r, r_out * .94, r_out))
    uvl = stone_b.loops.layers.uv.new("UVMap")
    for f in stone_b.faces:
        for l in f.loops:
            p = l.vert.co; l[uvl].uv = ((p.x + p.y) / 2, p.z / 2)
    merge_bmesh(o, dark_b, dark)
    merge_bmesh(o, stone_b, stone)


def patch_tear(o, stone):
    """Tripo painted the right facade tower's tear black into the texture. Faces of the tower bands above the windows
    whose texel is near black wear the stone brick instead (UVs in metres, like the cores)."""
    lo, hi = bounds(o); w = hi.x - lo.x; h = hi.z - lo.z
    mat = o.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "basecolor" in n.image.name.lower())
    iw, ih = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(ih, iw, 4)
    s_ = slot(o, stone)
    bm = bmesh.new(); bm.from_mesh(o.data); uvl = bm.loops.layers.uv.active
    n = 0
    for f in bm.faces:
        c = f.calc_center_median()
        if f.material_index != 0 or abs(c.x - (lo.x + hi.x) / 2) < w * .33 or not (lo.z + TEAR[0] * h < c.z < lo.z + TEAR[1] * h):
            continue
        u = sum(l[uvl].uv[0] for l in f.loops) / len(f.loops); v = sum(l[uvl].uv[1] for l in f.loops) / len(f.loops)
        col = px[int(min(.999, max(0, v)) * (ih - 1)), int(min(.999, max(0, u)) * (iw - 1)), :3]
        if col @ np.array([.2126, .7152, .0722]) < .08:
            f.material_index = s_; n += 1
            for l in f.loops: l[uvl].uv = ((l.vert.co.x + l.vert.co.y) / 2, l.vert.co.z / 2)
    bm.to_mesh(o.data); bm.free()
    print("FACADE tear: %d black faces now stone" % n)


# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = split(load(FACADE), ["Facade"])
    parts.update(split(load(STRUCTURE), STRUCTURE_ORDER))
    parts.update(split(load(PROPS), PROPS_ORDER, turn=True))
    save_atlas(parts["Facade"], "PR_A_TripoFacade")
    save_atlas(parts["Wall"], "PR_A_TripoStructure")
    save_atlas(parts["Cypress"], "PR_A_TripoProps")
    brick = tex_material("PR_A_StoneBrick", "PR_Early_StoneBrick")
    lime = tex_material("PR_A_IvoryLimestone", "PR_A_IvoryLimestone", .6)
    dark = flat_material("PR_A_TowerDark", "#1E1A17", .95)
    paint = {"PotWhite": flat_material("PR_A_PaintWhite", "#F3EADB", .25), "PotBlack": flat_material("PR_A_PaintBlack", "#231E1B", .25)}
    report = []
    for name, (axis, size, verdict, target) in PLAN.items():
        o = parts[name]; before = tris(o)
        k = fit(o, name)
        if name == "Tower": rebuild_tower_base(o, brick)
        if name == "GatePost": rebuild_post_base(o, brick)
        if name == "Bench": new_bench(o, lime)
        if name in paint: paint_pot(o, paint[name])
        if name == "Facade": tower_cores(o, dark, brick); patch_tear(o, brick)
        if name in HOLES: mg_holes.close_small_holes(o, HOLES[name])
        if target: lower_to(o, target)
        if name == "TeamArch": narrow_piers(o)
        if name == "TeamDoor":                      # the game's door box is centred on the gate
            lo, hi = bounds(o); o.data.transform(Matrix.Translation(-(lo + hi) / 2))
        if name in FRONT_ORIGIN:
            o.data.transform(Matrix.Translation((0., -front_of(o, name in BASE_FRONT), 0.)))
        report.append([name, before, k, verdict])
    for name, before, k, verdict in report:
        o = parts[name]; lo, hi = bounds(o); e = hi - lo
        print("ASSET %-11s %-26s tris %6d -> %6d  size %.2f x %.2f x %.2f  (x%.1f)" % (name, verdict, before, tris(o), e.x, e.y, e.z, k[0]))
    print("ARCH opening %.2f m" % opening_width(parts["TeamArch"]))
    if "--export" in ARGS:
        os.makedirs(OUT, exist_ok=True)
        for name, o in parts.items():
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "A_%s.fbx" % name), use_selection=True,
                                     axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                     mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    x = 0
    order = list(PLAN)
    for name in order:
        o = parts[name]; lo, hi = bounds(o)
        o.location = (x - lo.x, 0, -lo.z)
        x += (hi.x - lo.x) + 1.2
    pawn = flat_material("pawn", "#F2EEE6", .5)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(-1.2, 0, .31)); bpy.context.object.data.materials.append(pawn)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(-1.2, 0, .84)); bpy.context.object.data.materials.append(pawn)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "A_Props_v1.blend"))
    if "--render" in ARGS:
        render(parts, order)


def render(parts, order):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1000, 800
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.6, .66, .74, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .8
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-25))); bpy.context.object.data.energy = 3.4
    cd = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    cd.lens = 50; cd.clip_end = 500
    pawn = [o for o in sc.objects if o.type == 'MESH' and o.name.startswith(("Cylinder", "Sphere"))]
    for name in order:
        o = parts[name]; lo, hi = bounds(o)
        for p in pawn: p.location.x = o.location.x + lo.x - .6        # a 1 m pawn next to each asset
        c = o.location + (lo + hi) / 2
        d = max(hi - lo) * 2.0 + .8
        cam.location = c + Vector((d * .5, -d * .8, d * .45))
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_a_prop_%s.png" % name)
        bpy.ops.render.render(write_still=True)


main()
