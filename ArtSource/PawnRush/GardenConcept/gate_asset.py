# R483 (승규 11-06, "castle gate 3d model.glb" + "보트 애니메이션을 둘거야 에셋 설치 해주고 그 구간에서 배가 떠 다니는
# 애니메이션 만들어줘", where: "성벽 안 수로 수문"; then "꼼꼼히 텍스쳐 해" with the gate's reference picture): 승규's Tripo
# castle gate - a long arcaded front (a round arch with a banner at its crown that runs on over the roof, two square
# piers with crests and finials, a small round tower with a slate cone at one end, a cypress and planters of bushes at
# the foot, ivy over the parapet, lanterns down the arcade inside) before a big hollow gatehouse with a flat paved roof
# and round bushes on it; the arch goes right through - becomes the water gate of the east channel inside the castle
# wall (watergate_sample_v1.py).
#
# The GLB comes untextured (one grey material, no UVs, one connected mesh), and its leaves are as smooth as its stone,
# so no measure of the surface alone finds them (tried: normal turn, normal spread, spread with erosion). This script
# paints it by hand-measured places instead (on orthographic renders of it: front at 4667 px per unit, top at 1400),
# as the reference picture has it:
#   stone       the castle walls' stone (GC | castle stone), the rest
#   banners     royal blue cloth, its raised hem and crown gold (raised = proud of the surface round it, `relief`);
#               the rods gold; the middle banner's runner over the roof the same
#   crests      the four shields: blue field, the raised rim and crown gold
#   cone        the small tower's slate cone in the towers' gate blue, its finial and raised ornament gold
#   lanterns    the arcade's lanterns gold, their panes glowing (GC | lantern glow)
#   greenery    the ivy, the planters' bushes and the roof's round bushes: leaves in three greens by clump, flowers
#               (pink, purple, yellow, coral, white) in clumps on their tops; the cypress in its own two greens; its trunk
#               wood
# where greenery is what stands proud of its host surface (the wall behind, found by rays round the place; the planter's
# or the roof's top) inside its place and is not flat stone (a cornice standing out with it), cleaned by a majority of
# its neighbours. Then normals recomputed, decimated to about 90k faces, left in its own units (front of the piers at
# y -0.30, base z 0, x -0.49 .. 0.49; the arch's clear opening x -0.03 .. 0.15 up to z 0.13).
#
#   blender -b --python gate_asset.py -- "<castle gate 3d model.glb>"
# Out: GateAsset_v1.blend (object "Gate asset"), render_gate_asset_*.png
import bpy, sys, os, math, random
import numpy as np
from collections import defaultdict
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
GLB = sys.argv[sys.argv.index("--") + 1]
FACES = 90000

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
src = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
src.name = "Gate asset"
me = src.data


def mat(name, rgb, rough, metal=0., emit=0.):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit:
        p.inputs["Emission Color"].default_value = (*rgb, 1)
        p.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*rgb, 1)
    return m


# The garden's materials by name (Unity maps them by name to Resources/PawnRushGardenConcept/Materials/GC_*.mat).
MATS = [
    ("stone", "GC | castle stone", (.88, .76, .62), .74),
    ("blue", "GC | royal blue", (.10, .24, .66), .6),
    ("gold", "GC | gate gold", (.98, .82, .30), .3, .75),
    ("roof", "GC | gate blue", (.18, .38, .78), .55),
    ("leaf", "GC | leaf", (.30, .62, .16), .7),
    ("leaf2", "GC | leaf light", (.45, .74, .22), .7),
    ("leaf3", "GC | leaf dark", (.18, .46, .12), .7),
    ("pink", "GC | flower pink", (1.0, .48, .68), .6),
    ("purple", "GC | flower purple", (.70, .50, .92), .6),
    ("yellow", "GC | flower yellow", (1.0, .86, .22), .6),
    ("coral", "GC | flower coral", (1.0, .55, .40), .6),
    ("white", "GC | flower white", (.98, .97, .93), .6),
    ("wood", "GC | bar wood", (.55, .36, .2), .55),
    ("glow", "GC | lantern glow", (1.0, .84, .55), .3, 0, 4),
]
me.materials.clear()
IDX = {}
for i, (k, name, *rest) in enumerate(MATS):
    me.materials.append(mat(name, *rest))
    IDX[k] = i

# ------------------------------------------------------------------ the mesh as arrays (normals made consistent first)
import bmesh
_bm = bmesh.new()
_bm.from_mesh(me)
bmesh.ops.recalc_face_normals(_bm, faces=_bm.faces)
_bm.to_mesh(me)
_bm.free()
me.update()
nv, nf = len(me.vertices), len(me.polygons)
co = np.zeros(nv * 3)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
vn = np.zeros(nv * 3)
me.vertices.foreach_get("normal", vn)
vn = vn.reshape(-1, 3)
edges = np.zeros(len(me.edges) * 2, dtype=np.int64)
me.edges.foreach_get("vertices", edges)
edges = edges.reshape(-1, 2)
deg = np.bincount(edges.ravel(), minlength=nv).astype(float)
fc = np.zeros(nf * 3)
me.polygons.foreach_get("center", fc)
fc = fc.reshape(-1, 3)
fn = np.zeros(nf * 3)
me.polygons.foreach_get("normal", fn)
fn = fn.reshape(-1, 3)
loops = np.zeros(len(me.loops), dtype=np.int64)
me.loops.foreach_get("vertex_index", loops)
fv = loops.reshape(-1, 3)                     # the GLB is all triangles


def smooth(field, times):
    f = field.copy()
    for _ in range(times):
        s = f.copy()
        np.add.at(s, edges[:, 0], f[edges[:, 1]])
        np.add.at(s, edges[:, 1], f[edges[:, 0]])
        f = s / (1 + deg)[:, None] if f.ndim == 2 else s / (1 + deg)
    return f


def relief_of(times):
    """How far each face stands out along its normal from the surface round it (asset units; > 0 raised)."""
    r = np.einsum("ij,ij->i", co - smooth(co, times), vn)
    return r[fv].mean(axis=1)


relief = relief_of(10)          # thin raised lines: hems, rims
plateau = relief_of(60)         # raised shapes a few centimetres across: the crowns
spread_v = 1 - np.linalg.norm(smooth(vn, 6), axis=1)
spread = spread_v[fv].mean(axis=1)

bvh = BVHTree.FromPolygons([tuple(v) for v in co], [tuple(f) for f in fv])


def wall_plane(x0, x1, z0, z1, ring=.016, step=.0025, side=-1):
    """The wall behind a place, as a plane y = a + b x (+ 0 z): rays from the front in a band `ring` wide round the
    place, fitted, then fitted again on the hits not more than 4 mm before it (the piers, the tower and the cornices
    standing out round a place drop out; the walls here turn up to 23 degrees from the front). `side` -1 looks at the
    front (from -y), +1 at the back (from +y)."""
    pts = []
    for xa, xb, za, zb in ((x0 - ring, x0, z0, z1), (x1, x1 + ring, z0, z1), (x0, x1, z0 - ring, z0), (x0, x1, z1, z1 + ring)):
        for x in np.arange(xa, xb, step):
            for z in np.arange(za, zb, step):
                h = bvh.ray_cast(Vector((x, side, z)), Vector((0, -side, 0)), 3)
                if h[0] and h[1].y * side > .75 and h[0].y * side > (-.1 if side < 0 else -.3):   # (the back of the tower's wall is at y -0.17)
                    pts.append((x, z, h[0].y))
    if len(pts) < 6:
        return None                                        # nothing there to stand before (open to the sky)
    p = np.array(pts)
    keep = np.ones(len(p), bool)
    for _ in range(4):
        A = np.c_[np.ones(keep.sum()), p[keep, 0]]          # the walls stand plumb: y = a + b x
        a, b = np.linalg.lstsq(A, p[keep, 2], rcond=None)[0]
        res = p[:, 2] - (a + b * p[:, 0])
        keep = res * side < .004
    return (a, b, 0.)


def before(coef, gap, side=-1):
    """Faces at least `gap` before the plane (towards the viewer: -y at the front, +y at the back); all, with no plane."""
    if coef is None:
        return np.ones(nf, bool)
    return (fc[:, 1] - (coef[0] + coef[1] * fc[:, 0] + coef[2] * fc[:, 2])) * side > gap


def in_box(x0, x1, z0, z1, y0=-1., y1=1.):
    return ((fc[:, 0] >= x0) & (fc[:, 0] <= x1) & (fc[:, 2] >= z0) & (fc[:, 2] <= z1)
            & (fc[:, 1] >= y0) & (fc[:, 1] <= y1))


def in_pennant(x0, x1, z0, z1, square):
    """Inside a banner's or a crest's outline (front view): square from the top down to `square` of its height, then
    a point at the middle of its foot."""
    x, z = fc[:, 0], fc[:, 2]
    zs = z1 - square * (z1 - z0)
    mid, half = (x0 + x1) / 2, (x1 - x0) / 2
    v = np.where(z >= zs, half, half * np.clip((z - z0) / max(1e-6, zs - z0), 0, 1))
    return (z >= z0) & (z <= z1) & (np.abs(x - mid) <= v + .0015)


cls = np.full(nf, IDX["stone"], dtype=np.int32)
leafy = np.zeros(nf, bool)        # greenery (gets leaves and flowers below)
FRONT = (-.46, -.13)              # the front's items lie here in y
BACK = (.13, .46)                 # the back's

# ------------------------------------------------------------------ greenery
# (x0, x1, z0, z1, gap, top, behind, flowers, flat, side, y0, y1): leaves are what stands `gap` before the wall behind
# them (its plane fitted round the place, seen from the front, side -1, or the back, +1) or over the `top` they grow on
# (a wall's coping, a planter's rim) - and then, where `behind` is set, still before the wall above the place (a
# planter's back) - and are not flat (spread over `flat`: the copings and cornices stand out of the wall as much as
# the ivy). `flowers` is their share of blossom; y0 .. y1 where the place lies.
F, B = (-1,) + FRONT, (1,) + BACK
GREEN = [
    (-.402, -.316, .125, .240, .0025, .2095, False, .25, .05) + F,   # the ivy over the wall between the tower and the
                                                                      # left pier, its bush on the coping
    (-.300, -.232, .105, .300, .0015, .2560, False, .25, .03) + F,   # the bush on the left pier's shoulder, its ivy
    (-.307, -.205, .061, .099, None, .0645, True, .55, .03) + F,     # the left planter's bush
    (-.298, -.262, .026, .067, .0025, None, False, 0., .03) + F,     # its ivy over the planter's front
    (.160, .259, .215, .292, .0040, .2570, False, .3, .05) + F,      # the ivy over the parapet right of the arch
    (.350, .477, .236, .292, None, .2500, False, .45, .03) + F,      # the bushes on the right end's coping
    (.368, .420, .165, .292, .0025, None, False, .15, .03) + F,      # their ivy down its face
    (.309, .429, .061, .098, None, .0650, True, .55, .03) + F,       # the right planter's bush
    (.393, .427, .032, .067, .0025, None, False, 0., .03) + F,       # its ivy over the planter's front
    # the back (look_gate back renders): the planter in the recess left of the arch, the ivy by the tower
    (-.270, -.184, .062, .099, None, .0660, True, .55, .03, 1, .15, .36),
    (-.385, -.333, .140, .228, .0025, .2070, False, .25, .05, 1, -.22, .05),
]
# stone that stands out with the leaves: the step block on the right pier's shoulder; the string course across the
# wall by the tower, under its ivy (only its flat faces)
NOT_GREEN = [(.200, .259, .283, .310, 1.), (-.402, -.316, .157, .169, .08)]
ROOF_BUSHES = ((-.270, -.084, .205, .318), (-.080, .001, .282, .396), (.168, .273, .218, .318))
region = np.zeros(nf, bool)
bloom = np.zeros(nf)              # each leaf face's share of blossom
for x0, x1, z0, z1, gap, top, behind, flowers, flat, side, y0, y1 in GREEN:
    box = in_box(x0, x1, z0, z1, y0, y1)
    region |= box
    sel = np.zeros(nf, bool)
    if gap is not None:
        coef = wall_plane(x0, x1, z0, z1, side=side)
        sel |= before(coef, gap, side) & (spread > flat)
        print("GREEN wall", coef, "at x %.3f..%.3f side %d" % (x0, x1, side))
    if top is not None:
        over = fc[:, 2] > top + .002
        if behind:
            over &= before(wall_plane(x0, x1, z1, z1 + .03, .03, side=side), .003, side)
        sel |= over                                      # (a bush's round top is as smooth as stone: no spread test)
    sel &= box
    leafy |= sel
    bloom[sel] = np.maximum(bloom[sel], flowers)
for x0, x1, z0, z1, flat in NOT_GREEN:
    leafy &= ~(in_box(x0, x1, z0, z1) & (spread < flat))
for x0, x1, y0, y1 in ROOF_BUSHES:
    box = in_box(x0, x1, .252, .40, y0, y1)
    region |= box
    sel = box
    leafy |= sel
    bloom[sel] = .5
print("GREEN", int(leafy.sum()))
cypress = in_box(.394, .490, .085, .275, -.37, -.228)
trunk = in_box(.428, .451, .062, .087, -.33, -.26) & ~cypress

# a majority of the edge neighbours decides, twice (cleans single faces and holes)
fe = defaultdict(list)
for f, tri in enumerate(fv):
    for a, b in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
        fe[(min(a, b), max(a, b))].append(f)
nb = [[] for _ in range(nf)]
for fs in fe.values():
    if len(fs) == 2:
        nb[fs[0]].append(fs[1])
        nb[fs[1]].append(fs[0])
for _ in range(2):
    nxt = leafy.copy()
    for f in np.nonzero(region)[0]:
        n = nb[f]
        if n:
            k = sum(leafy[g] for g in n)
            if k * 3 >= 2 * len(n):
                nxt[f] = True
            elif k * 3 <= len(n):
                nxt[f] = False
    leafy = nxt
bloom[~leafy] = 0.

# ------------------------------------------------------------------ banners, crests, cone
# (x0, x1, z0, z1, crown x0, x1, z0, z1)
# (x0, x1, z0, z1, crown x0, x1, z0, z1, side): the front's three, the back's one (the middle one's other end)
BANNERS = [(-.237, -.180, .124, .236, -.223, -.193, .177, .201, -1),
           (-.001, .077, .133, .262, .014, .061, .189, .222, -1),
           (.309, .363, .124, .243, .321, .352, .177, .201, -1),
           (.002, .074, .134, .261, .014, .059, .189, .225, 1)]
CRESTS = [(-.204, -.174, .246, .280, -1), (-.142, -.108, .172, .212, -1), (.199, .236, .171, .216, -1),
          (.273, .302, .246, .280, -1),
          (.230, .256, .247, .279, 1), (.164, .198, .172, .221, 1), (-.069, -.035, .165, .219, 1), (-.132, -.103, .247, .279, 1)]
RAISED = .0009
for x0, x1, z0, z1, cx0, cx1, cz0, cz1, side in BANNERS:
    box = in_box(x0 - .004, x1 + .004, z0, z1 + .012, *(FRONT if side < 0 else BACK))
    coef = wall_plane(x0, x1, z0, z1, side=side)
    hung = box & before(coef, .0012, side)
    cloth = hung & in_pennant(x0, x1, z0, z1, .7)
    rods = hung & (fc[:, 2] > z1 - .014) & ((fc[:, 0] < x0 + .007) | (fc[:, 0] > x1 - .007))
    crown = cloth & in_box(cx0, cx1, cz0, cz1) & (plateau > .0004)
    hem = cloth & ((relief > RAISED) | (np.abs(fc[:, 0] - (x0 + x1) / 2) > (x1 - x0) / 2 - .0045))
    cls[cloth] = IDX["blue"]
    cls[hem | crown | rods] = IDX["gold"]
    print("BANNER wall", coef, "cloth %d crown %d hem %d" % (cloth.sum(), crown.sum(), hem.sum()))
# the middle banner's runner over the roof, front to back (the strip x 0 .. 0.075 of the top view), its hems gold
run = in_box(0., .0755, .240, .30, -.33, .42) & (fn[:, 2] > .3)
cls[run] = np.where((relief[run] > RAISED) | (np.abs(fc[run, 0] - .0378) > .0335), IDX["gold"], IDX["blue"])
print("RUNNER", int(run.sum()))
for x0, x1, z0, z1, side in CRESTS:
    face = (in_box(x0, x1, z0, z1, *(FRONT if side < 0 else BACK)) & in_pennant(x0, x1, z0, z1, .55)
            & (fn[:, 1] * side > .2))
    inner = in_pennant(x0 + .0045, x1 - .0045, z0 + .006, z1 - .0045, .5)
    crown = face & inner & (plateau > .0003) & (fc[:, 2] > z0 + .35 * (z1 - z0))
    cls[face] = IDX["blue"]
    cls[face & (~inner | (relief > RAISED * .8) | crown)] = IDX["gold"]
    print("CREST faces %d" % face.sum())
cone = in_box(-.497, -.383, .229, .40)
cls[cone] = IDX["roof"]
cls[cone & ((fc[:, 2] > .343) | (relief > .0018))] = IDX["gold"]

# ------------------------------------------------------------------ lanterns (the arcade, both sides)
for x0, x1 in ((-.096, -.024), (.142, .194)):
    box = in_box(x0, x1, .075, .1165, -.30, .345)          # inside the arcade (not the back arch's imposts)
    lamp = box & (spread > .02) & (np.abs(fn[:, 1]) < .92)    # not the arches' faces round them
    cls[lamp] = IDX["gold"]
    pane = lamp & (fc[:, 2] > .0835) & (fc[:, 2] < .1025) & (np.abs(fn[:, 2]) < .5)
    cls[pane] = IDX["glow"]

# ------------------------------------------------------------------ leaves and flowers by clump
rnd = random.Random(483)
clump = {}


def pick(key, choices):
    if key not in clump:
        clump[key] = rnd.choice(choices)
    return clump[key]


for f in np.nonzero(leafy | cypress)[0]:
    c = fc[f]
    key = (round(c[0] / .011), round(c[1] / .011), round(c[2] / .011))
    if cypress[f]:
        cls[f] = IDX[pick(("c",) + key, ("leaf3", "leaf3", "leaf"))]
        continue
    k = pick(key, ("leaf", "leaf", "leaf2", "leaf3"))
    if bloom[f] > 0 and fn[f, 2] > .2:
        # flowers in small clumps on the tops of the leaves, as many as the place has (planters most, ivy few)
        fk = (round(c[0] / .006), round(c[1] / .006), round(c[2] / .006))
        if fk not in clump:
            clump[fk] = rnd.choice(("pink", "purple", "yellow", "coral", "white", "pink", "purple")) if rnd.random() < bloom[f] else None
        k = clump[fk] or k
    cls[f] = IDX[k]
cls[trunk] = IDX["wood"]

me.polygons.foreach_set("material_index", cls)
me.update()
counts = defaultdict(int)
for k, i in IDX.items():
    counts[k] = int((cls == i).sum())
print("SEGMENTS", dict(counts))

# Decimate without merging across the paint: the vertices on a border between two materials are kept (weight 0 in
# the collapse's vertex group), so no big flat stone triangle takes a leaf's or a banner's material (the first try did).
border = np.zeros(nv, bool)
for (a, b), fs in fe.items():
    if len(fs) == 2 and cls[fs[0]] != cls[fs[1]]:
        border[a] = border[b] = True
vg = src.vertex_groups.new(name="decimate")
vg.add([int(i) for i in np.nonzero(~border)[0]], 1.0, "REPLACE")
vg.add([int(i) for i in np.nonzero(border)[0]], 0.0, "REPLACE")
dec = src.modifiers.new("Decimate", "DECIMATE")
dec.ratio = min(1., FACES / max(1, nf))
dec.vertex_group = "decimate"
dec.vertex_group_factor = 1000.
bpy.context.view_layer.objects.active = src
bpy.ops.object.modifier_apply(modifier=dec.name)
src.vertex_groups.clear()
for p in src.data.polygons:
    p.use_smooth = False
print("FACES", len(src.data.polygons), "border verts", int(border.sum()))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "GateAsset_v1.blend"))

# ------------------------------------------------------------------ previews in the materials' colours
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "MATERIAL"
sc.display.shading.show_cavity = True
sc.render.resolution_x, sc.render.resolution_y = 1400, 800
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
SHOTS = (("front", (0, -2, .17), (0, 0, .17), 1.0), ("front_left", (-.25, -2, .19), (-.25, 0, .19), .5),
         ("front_right", (.25, -2, .19), (.25, 0, .19), .5), ("quarter", (1.1, -1.1, .8), (0, 0, .12), None),
         ("back", (-1.0, 1.2, .7), (0, 0, .12), None), ("arcade", (.06, -.9, .09), (.06, 0, .09), None),
         ("back_ortho", (0, 2, .17), (0, 0, .17), 1.0), ("top", (0, -.0001, 2), (0, 0, 0), 1.0))
for name, at, look, ortho in SHOTS:
    cam.location = Vector(at)
    cam.rotation_euler = (Vector(look) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    if ortho:
        cam.data.type, cam.data.ortho_scale = "ORTHO", ortho
    else:
        cam.data.type, cam.data.lens = "PERSP", 35
    sc.render.filepath = os.path.join(HERE, "render_gate_asset_%s.png" % name)
    bpy.ops.render.render(write_still=True)
print("GATE ASSET DONE")
