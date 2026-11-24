# First-run tutorial map, palace drill yard look (R834, concept Docs/Player/ConceptArt/R834_Tutorial/TU_1_palace_drill_yard.png).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_map_v1.py -- [--inspect] [--render] [--save]
#
# Run tu_textures_v1.py once first (Materials/Textures/TU_*.png).
#
# Frame: Blender (x, y, z) = Unity (x, z, y). Every number that the game checks comes from LabLayout / TutorialLayout /
# RagdollLabBuilder.BuildGeometry and is used unchanged: each collider box gets a dressed mesh of exactly its size.
# Nothing here adds a collider or changes one; railings, statues and castle walls stand outside the play floors.
#
# Parts
#   Tripo (Source/*.glb, the user's models from AssetRefs sheets 01-03), split, repaired, sized:
#     A1 sheet 01: CastleWall, CastleCorner, CastleTower, CastlePilaster
#     A2 sheet 02: Gatehouse, WallNiche, KnightStatue, BishopStatue, StoneSteps
#     A3 sheet 03: ClimbBlock, Ramp, Tunnel   (A3 came without the low ledge and the limbo station)
#   Made here (exact game sizes): training walls 2/3/4 m, hurdles, limbo station, tunnel, sweeper, beam, landing,
#     finish arch, balustrade, balustrade post, moat walls, floors, platform and slopes, climbing faces.
#   Reused: D_FlowerPot, A_Cypress (ArtSource/PawnRush/MiniGames/Early/Export).
import bpy, bmesh, sys, os, math, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib, tu_kit_common as K
importlib.reload(K)
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
EARLY = os.path.join(K.HERE, "..", "PawnRush", "MiniGames", "Early")

# ------------------------------------------------------------------ game numbers (LabLayout, TutorialLayout, builder)
HALF = 15.
BAR_C = (0., 24.); BAR_LEN = 13.; BAR_H = .45; BAR_R = .2
WALL_FRONT_X = 20.; WALL_DEPTH = 5.; WALL_W = 6.; WALL_H = (2., 3., 4.); WALL_Y = (-9., 0., 9.)
CLIMB_Y = 13.5; CLIMB_H = 5.; CLIMB_W = 3.; CLIMB_X = (-9., 0., 9.)
LEDGE_W = 6.; LEDGE_STEP_H = 3.; LEDGE_BACK = 1.5; LEDGE_N = 4
PLAT_H = 5.; PLAT_EDGE = -35.; PLAT_BACK = -40.; SLOPE_ANG = (15., 30., 45.); SLOPE_Y = (-9., 0., 9.); SLOPE_W = 6.
BEAM_W = .6; BEAM_Y0 = -15.; BEAM_Y1 = -31.
LIMBO = (9., -7.); LIMBO_CLEAR = .55; TUNNEL = (9., -11.5); TUNNEL_CLEAR = .65; TUNNEL_LEN = 3.; SLAB = .25
HURDLE_X = (3.2, 5.4); HURDLE_Y = -1.; HURDLE_LEN = 5.; HURDLE_H = .45; HURDLE_T = .3
GOAL_Y = BEAM_Y1 - 3.
KILL_Z = -8.            # top of the kill box (KillCenter y -12, size 8): anything below this is already a fall

FLOORS = [  # (x0, y0, x1, y1, kind) top at z 0
    (-15., -15., 15., 15., "chess"),                 # [1] 30 x 30
    (15., -15., 35., 15., "paving"),                 # wall floor
    (-40., -15., -15., 15., "paving"),               # slope floor
    (-15., 15., 15., 16., "paving"),                 # climb back strip
    (8., 16., 15., 24., "paving"),                   # climb back east
    (-15., 16., -8., 24., "paving"),                 # climb back west
    (-8., 16., 8., 32., "board"),                    # bar floor 16 x 16
    (-3., -37., 3., -31., "board3"),                 # beam landing 6 x 6
]
MOAT_Z = -10.           # dry moat floor, below the kill height so nobody lands on it alive
RIM_IN = (-52., -47., 47., 42.)    # inner edge of the courtyard rim (10-12 m from any play floor)
RIM_W = 6.

# ------------------------------------------------------------------ materials
def mats():
    T = K.TEX
    M = {}
    M["block"] = K.image_material("TU_CreamBlock", os.path.join(T, "TU_CreamBlock_BaseColor.png"), rough=.7)
    M["paving"] = K.image_material("TU_Paving", os.path.join(T, "TU_Paving_BaseColor.png"), rough=.55)
    M["cream"] = K.image_material("TU_MarbleCream", os.path.join(T, "TU_MarbleCream_BaseColor.png"), rough=.25)
    M["walnut_m"] = K.image_material("TU_MarbleWalnut", os.path.join(T, "TU_MarbleWalnut_BaseColor.png"), rough=.25)
    M["wood"] = K.image_material("TU_Walnut", os.path.join(T, "TU_Walnut_BaseColor.png"), rough=.4)
    M["moat"] = K.image_material("TU_MoatFloor", os.path.join(T, "TU_MoatFloor_BaseColor.png"), rough=.9)
    M["gold"] = K.principled("TU_Gold", (.83, .37, .01, 1), rough=.32, metal=.6)
    M["purple"] = K.principled("TU_Purple", (.12, .006, .16, 1), rough=.45)
    M["paint"] = K.principled("TU_CreamPaint", (.86, .70, .54, 1), rough=.45)
    M["grass"] = K.principled("TU_Grass", (.06, .20, .03, 1), rough=.9)
    return M


# ------------------------------------------------------------------ mesh builders
def new_obj(name, bm, mlist, coll):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mlist: me.materials.append(m)
    o = bpy.data.objects.new(name, me); coll.objects.link(o)
    return o


def boxuv(o, tile, offset=(0., 0., 0.)):
    """World-metre box projection: every face takes the two axes across its normal, `tile` metres per texture repeat."""
    me = o.data
    if not me.uv_layers: me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    co = [o.matrix_world @ v.co for v in me.vertices]
    for p in me.polygons:
        n = o.matrix_world.to_3x3() @ p.normal
        a = max(range(3), key=lambda k: abs(n[k]))
        ax = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[a]
        sgn = 1 if n[a] >= 0 else -1
        for li in p.loop_indices:
            c = co[me.loops[li].vertex_index]
            u = (c[ax[0]] + offset[ax[0]]) / tile; v = (c[ax[1]] + offset[ax[1]]) / tile
            if a == 1: u = u * -sgn          # read left to right from outside
            if a == 0: u = u * sgn
            uv[li].uv = (u, v)


def cuboid(bm, corners, mi=0):
    """8 corners in the order (---, +--, ++-, -+-, --+, +-+, +++, -++)."""
    v = [bm.verts.new(c) for c in corners]
    F = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    out = []
    for f in F:
        face = bm.faces.new([v[i] for i in f]); face.material_index = mi; out.append(face)
    return out


def box_corners(lo, hi):
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    return [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]


def unity_box_corners(c, s, rot=None):
    """A Unity BoxCollider (centre, size, Euler degrees) as 8 Blender points (Unity x, z, y -> Blender x, y, z)."""
    R = np.eye(3)
    if rot:
        ax, ay, az = [math.radians(a) for a in rot]
        Rx = np.array([[1, 0, 0], [0, math.cos(ax), -math.sin(ax)], [0, math.sin(ax), math.cos(ax)]])
        Ry = np.array([[math.cos(ay), 0, math.sin(ay)], [0, 1, 0], [-math.sin(ay), 0, math.cos(ay)]])
        Rz = np.array([[math.cos(az), -math.sin(az), 0], [math.sin(az), math.cos(az), 0], [0, 0, 1]])
        R = Ry @ Rx @ Rz                                   # Unity applies z, then x, then y
    h = np.array(s) / 2
    out = []
    for sx, sy, sz in [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]:
        p = np.array(c) + R @ (h * np.array([sx, sz, sy]))   # corner order is in Blender terms: (x, y=Unity z, z=Unity y)
        out.append((p[0], p[2], p[1]))
    return out


def solid(name, corner_sets, mlist, coll, bevel=.04, tile=2., mi=0, segs=2):
    bm = bmesh.new()
    for cs in corner_sets:
        cuboid(bm, cs, mi)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj(name, bm, mlist, coll)
    if bevel:
        b = o.modifiers.new("bev", 'BEVEL'); b.width = bevel; b.segments = segs; b.limit_method = 'ANGLE'
        b.harden_normals = False
        K.apply_mod(o, b)
    boxuv(o, tile)
    smooth_by_angle(o)
    return o


def smooth_by_angle(o, deg=35):
    for p in o.data.polygons: p.use_smooth = False
    try:
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o], selected_editable_objects=[o]):
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(deg))
    except Exception:
        pass


def cylinder(bm, c0, c1, r, seg=24, mi=0, caps=True):
    c0 = Vector(c0); c1 = Vector(c1); d = (c1 - c0)
    q = d.to_track_quat('Z', 'Y')
    ring0 = []; ring1 = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        p = q @ Vector((math.cos(a) * r, math.sin(a) * r, 0))
        ring0.append(bm.verts.new(c0 + p)); ring1.append(bm.verts.new(c1 + p))
    for i in range(seg):
        j = (i + 1) % seg
        f = bm.faces.new([ring0[i], ring0[j], ring1[j], ring1[i]]); f.material_index = mi; f.smooth = True
    if caps:
        f = bm.faces.new(list(reversed(ring0))); f.material_index = mi
        f = bm.faces.new(ring1); f.material_index = mi


def sphere(bm, c, r, mi=0, seg=16):
    res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2 + 2, radius=r)
    for v in res["verts"]: v.co += Vector(c)
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.material_index = mi; f.smooth = True


def crown(bm, c, r, h, mi=0):
    """A small sculpted crown: a band, five points, a ball on each point."""
    cx, cy, cz = c
    cylinder(bm, (cx, cy, cz), (cx, cy, cz + h * .45), r, 24, mi)
    for i in range(5):
        a = 2 * math.pi * i / 5
        px, py = cx + math.cos(a) * r * .92, cy + math.sin(a) * r * .92
        cylinder(bm, (px, py, cz + h * .3), (px, py, cz + h * .85), r * .16, 10, mi)
        sphere(bm, (px, py, cz + h * .9), r * .2, mi, 10)
    sphere(bm, (cx, cy, cz + h * .35), r * .55, mi, 14)


def striped_bar(bm, c0, c1, r, n, mi_a, mi_b, seg=20):
    c0 = Vector(c0); c1 = Vector(c1)
    for i in range(n):
        a = c0.lerp(c1, i / n); b = c0.lerp(c1, (i + 1) / n)
        cylinder(bm, a, b, r, seg, mi_a if i % 2 == 0 else mi_b, caps=True)


# ------------------------------------------------------------------ Tripo kit
A1_TOWER = (.283, -.031)


def split_rule_a1(c):
    if c.x < -.17: return 0
    if c.x < .14: return 1
    if c.x > .40 and c.y < -.095: return 3
    return 2


def split_islands(ob, rule, n):
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    groups = [[] for _ in range(n)]
    for idx in K.islands(bm):
        c = Vector()
        for i in idx[:200]: c += bm.faces[i].calc_center_median()
        c /= min(len(idx), 200)
        groups[rule(c)].extend(idx)
    out = []
    for idx in groups:
        keep = set(idx)
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new("part"); b2.to_mesh(me); b2.free()
        for m in ob.data.materials: me.materials.append(m)
        out.append(K.link(bpy.data.objects.new("part", me)))
    bm.free(); bpy.data.objects.remove(ob)
    return out


TRIPO = {   # name: (set, index, sizing, target tris, use in map)
    "CastleWall":     ("A1", 0, ("x", 4.0), 1400, "courtyard wall, 4 m module"),
    "CastleCorner":   ("A1", 1, ("same", "CastleWall"), 3000, "spare (towers take the corners)"),
    "CastleTower":    ("A1", 2, ("z", 10.0), 6000, "corner and mid-side towers"),
    "CastlePilaster": ("A1", 3, ("same", "CastleWall"), 900, "every third wall module"),
    "Gatehouse":      ("A2", 0, ("x", 10.0), 9000, "north gate behind the bar pad"),
    "WallNiche":      ("A2", 1, ("z", 3.6), 2400, "east and west walls, statue inside"),
    "KnightStatue":   ("A2", 2, ("z", 2.6), 4500, "in the niches, flanking the gate"),
    "BishopStatue":   ("A2", 3, ("z", 2.6), 3500, "in the niches, flanking the gate"),
    "StoneSteps":     ("A2", 4, ("x", 6.0), 2400, "not placed: no room in front of the gate (the rim ends 1.3 m in front of it)"),
    "ClimbBlock":     ("A3", 0, ("x", 6.0), 3000, "not placed: the 2/3/4 m walls must be 5 x 6 m exactly"),
    "Ramp":           ("A3", 1, ("x", 6.0), 3000, "not placed: slopes are 7-19 m long"),
    "Tunnel":         ("A3", 2, ("x", 4.6), 2400, "not placed: the game's tunnel has a flat 0.65 m ceiling"),
}
SETS = {"A1": "TU_A1_CastleWallTower", "A2": "TU_A2_CastleProps", "A3": "TU_A3_AssetPack"}
KIT_MAT = {"A1": "TU_Kit_CastleKit", "A2": "TU_Kit_GateKit", "A3": "TU_Kit_TrainingKit"}


def kit_material(o, name):
    mat = o.data.materials[0]
    if mat.name == name: return mat
    os.makedirs(K.TEX, exist_ok=True)
    for n in mat.node_tree.nodes:
        if n.type != 'TEX_IMAGE' or not n.image: continue
        role = "BaseColor"
        for l in n.outputs["Color"].links:
            if l.to_node.type == 'NORMAL_MAP': role = "Normal"
            elif l.to_node.type == 'SEPARATE_COLOR' or 'Rough' in l.to_socket.name or 'Metal' in l.to_socket.name: role = "ORM"
        img = n.image.copy(); img.name = "%s_%s" % (name, role)
        if img.size[0] > 2048: img.scale(2048, 2048)
        img.filepath_raw = os.path.join(K.TEX, "%s_%s.png" % (name, role)); img.file_format = 'PNG'; img.save()
        n.image = img
    mat.name = name
    return mat


def tripo_kit(coll, report):
    raw = {}
    for key, src in SETS.items():
        ob = K.load_glb(src)
        if key == "A1":
            parts = split_islands(ob, split_rule_a1, 4)
        else:
            parts = K.split_by_footprint(ob)
        for p in parts:
            kit_material(p, KIT_MAT[key])
        raw[key] = parts
    kit = {}
    scale_of = {}
    for name, (key, idx, sizing, target, use) in TRIPO.items():
        o = raw[key][idx]; o.name = o.data.name = "TU_" + name
        for c in o.users_collection: c.objects.unlink(o)
        coll.objects.link(o)
        before = K.inspect(o)
        if name in ("CastleWall", "Gatehouse", "StoneSteps", "ClimbBlock"):
            K.straighten(o)
        else:
            K.ground_center(o)
        lo, hi = K.bounds(o); d = hi - lo
        if sizing[0] == "same":
            s = scale_of[sizing[1]]
        else:
            axis = "xyz".index(sizing[0]); s = sizing[1] / d[axis]
        scale_of[name] = s
        K.xform(o, Matrix.Scale(s, 4))
        K.repair(o, weld=.004)
        K.decimate(o, target)
        K.ground_center(o)
        after = K.inspect(o)
        lo, hi = K.bounds(o)
        report[name] = dict(source=SETS[key], before=before, after=after, tris=K.tris(o),
                            size=[round(float(v), 2) for v in hi - lo], use=use, target=target)
        smooth_by_angle(o, 40)
        kit[name] = o
    return kit


# ------------------------------------------------------------------ parts made here
def part_training_wall(M, coll, h):
    """One of the east walls, exactly WALL_DEPTH x WALL_W x h, front face at x = 0 looking -x (placed at x 20)."""
    bm = bmesh.new()
    cuboid(bm, box_corners((0, -WALL_W / 2, 0), (WALL_DEPTH, WALL_W / 2, h)), 0)
    o = new_obj("TU_TrainingWall_%dm" % h, bm, [M["block"], M["gold"], M["paving"]], coll)
    for p in o.data.polygons:
        if p.normal.z > .9: p.material_index = 2
    b = o.modifiers.new("bev", 'BEVEL'); b.width = .05; b.segments = 2; K.apply_mod(o, b)
    boxuv(o, 2.)
    # gold band round the top, handholds on the climbing face
    bm = bmesh.new()
    e = .05
    cuboid(bm, box_corners((-e, -WALL_W / 2 - e, h - .22), (WALL_DEPTH + e, WALL_W / 2 + e, h - .04)), 0)
    rows = int((h - .5) / .6) + 1
    for r in range(rows):
        z = .45 + r * .6
        if z > h - .35: break
        for k, yy in enumerate((-2.1, -.7, .7, 2.1)):
            yy += (.35 if r % 2 else -.35)
            if abs(yy) > 2.5: continue
            cuboid(bm, box_corners((-.09, yy - .28, z - .05), (.01, yy + .28, z + .05)), 0)
    g = new_obj("tw_gold", bm, [M["gold"]], coll)
    b = g.modifiers.new("bev", 'BEVEL'); b.width = .03; b.segments = 2; K.apply_mod(g, b)
    o = K.join([o, g], "TU_TrainingWall_%dm" % h)
    smooth_by_angle(o)
    return o


def part_hurdle(M, coll):
    bm = bmesh.new()
    t, L, h = HURDLE_T, HURDLE_LEN, HURDLE_H
    cuboid(bm, box_corners((-t / 2, -L / 2, 0), (t / 2, L / 2, h - .08)), 0)
    cuboid(bm, box_corners((-t / 2 - .02, -L / 2 - .02, h - .08), (t / 2 + .02, L / 2 + .02, h)), 1)
    o = new_obj("TU_Hurdle", bm, [M["block"], M["gold"]], coll)
    b = o.modifiers.new("bev", 'BEVEL'); b.width = .025; b.segments = 2; K.apply_mod(o, b)
    boxuv(o, 2.); smooth_by_angle(o)
    return o


def part_limbo(M, coll):
    """Posts and bar exactly where the colliders are (posts 0.3 x 0.6 at x +-2.15, bar 4 x 0.6 x 0.25 at 0.55-0.80)."""
    bm = bmesh.new()
    top = LIMBO_CLEAR + SLAB
    for sx in (-1, 1):
        x = 2.15 * sx
        # striped post: purple and cream bands round a 0.3 x 0.6 core
        n = 4
        for i in range(n):
            z0 = top * i / n; z1 = top * (i + 1) / n
            cuboid(bm, box_corners((x - .15, -.3, z0), (x + .15, .3, z1)), 2 if i % 2 else 1)
        cuboid(bm, box_corners((x - .19, -.34, 0), (x + .19, .34, .08)), 0)            # gold foot
        crown(bm, (x, 0, top), .15, .3, 0)
    # the bar: a rounded slab in purple and cream stripes, gold caps at both ends
    n = 8
    for i in range(n):
        x0 = -2. + 4. * i / n; x1 = -2. + 4. * (i + 1) / n
        cuboid(bm, box_corners((x0, -.3, LIMBO_CLEAR), (x1, .3, top)), 1 if i % 2 else 2)
    o = new_obj("TU_LimboStation", bm, [M["gold"], M["purple"], M["paint"]], coll)
    b = o.modifiers.new("bev", 'BEVEL'); b.width = .02; b.segments = 2; b.limit_method = 'ANGLE'; K.apply_mod(o, b)
    smooth_by_angle(o)
    return o


def part_tunnel(M, coll):
    """Walls 0.3 x 0.9 x 3 at x +-2.15, roof 4 x 0.25 x 3 at 0.65-0.90: a flat ceiling inside, a low vault on top,
    a purple band round the middle and gold rims at both mouths (concept sheet 03, fitted to the game's box)."""
    L = TUNNEL_LEN; top = TUNNEL_CLEAR + SLAB
    bm = bmesh.new()
    for sx in (-1, 1):
        cuboid(bm, box_corners((2.15 * sx - .15, -L / 2, 0), (2.15 * sx + .15, L / 2, top)), 0)
    cuboid(bm, box_corners((-2.3, -L / 2, TUNNEL_CLEAR), (2.3, L / 2, top)), 0)
    # vault on top: a shallow half-cylinder from x -2.3 to 2.3 rising 0.45 m
    seg = 16
    for part, (y0, y1, mi) in enumerate(((-L / 2, -.5, 0), (-.5, .5, 1), (.5, L / 2, 0))):
        prev = None
        for i in range(seg + 1):
            a = math.pi * i / seg
            x = -2.3 * math.cos(a); z = top + .45 * math.sin(a)
            v0 = bm.verts.new((x, y0, z)); v1 = bm.verts.new((x, y1, z))
            if prev:
                f = bm.faces.new([prev[0], v0, v1, prev[1]]); f.material_index = mi; f.smooth = True
            prev = (v0, v1)
        # end caps of the vault
        for yy, rev in ((y0, True), (y1, False)):
            ring = [bm.verts.new((-2.3 * math.cos(math.pi * i / seg), yy, top + .45 * math.sin(math.pi * i / seg))) for i in range(seg + 1)]
            f = bm.faces.new(ring if not rev else list(reversed(ring))); f.material_index = mi
    # gold rims round both mouths
    for yy in (-L / 2, L / 2):
        cuboid(bm, box_corners((-2.38, yy - .08, top - .02), (2.38, yy + .08, top + .06)), 2)
        for sx in (-1, 1):
            cuboid(bm, box_corners((2.15 * sx - .2, yy - .08, 0), (2.15 * sx + .2, yy + .08, top)), 2)
        prev = None
        for i in range(seg + 1):
            a = math.pi * i / seg
            ps = [(-(2.3 + .08) * math.cos(a), yy + s, top + (.45 + .08) * math.sin(a)) for s in (-.09, .09)]
            vs = [bm.verts.new(p) for p in ps]
            if prev:
                f = bm.faces.new([prev[0], vs[0], vs[1], prev[1]]); f.material_index = 2; f.smooth = True
            prev = vs
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("TU_Tunnel", bm, [M["block"], M["purple"], M["gold"]], coll)
    boxuv(o, 2.)
    return o


def part_sweeper(M, coll):
    """The spinning bar's look (child of the game's Spinning Bar): hub r 0.35 from 0 to 1.4 m, lance r 0.2 at 0.45 m,
    13 m tip to tip. Gold hub and crown, purple and cream stripes, gold balls inside the tips."""
    bm = bmesh.new()
    cylinder(bm, (0, 0, 0), (0, 0, .1), .55, 32, 0)
    cylinder(bm, (0, 0, .1), (0, 0, 1.4), .35, 32, 0)
    cylinder(bm, (0, 0, 1.25), (0, 0, 1.4), .45, 32, 0)
    crown(bm, (0, 0, 1.4), .3, .5, 0)
    half = BAR_LEN / 2
    striped_bar(bm, (-half + .25, 0, BAR_H), (half - .25, 0, BAR_H), BAR_R, 22, 1, 2)
    for sx in (-1, 1):
        sphere(bm, (sx * (half - .2), 0, BAR_H), .2, 0, 16)
    o = new_obj("TU_Sweeper", bm, [M["gold"], M["purple"], M["paint"]], coll)
    return o


def part_beam(M, coll):
    L = BEAM_Y0 - BEAM_Y1
    bm = bmesh.new()
    cuboid(bm, box_corners((-BEAM_W / 2, -L / 2, -.5), (BEAM_W / 2, L / 2, 0)), 0)
    for yy in (-L / 2 + .2, L / 2 - .2):
        cuboid(bm, box_corners((-BEAM_W / 2 - .06, yy - .18, -.56), (BEAM_W / 2 + .06, yy + .18, -.04)), 1)
    o = new_obj("TU_Beam", bm, [M["wood"], M["gold"]], coll)
    b = o.modifiers.new("bev", 'BEVEL'); b.width = .02; b.segments = 2; K.apply_mod(o, b)
    # wood grain along the beam: u across (0.6 m of the 1 m texture), v along (4 m per repeat)
    me = o.data
    if not me.uv_layers: me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            c = me.vertices[me.loops[li].vertex_index].co
            n = p.normal
            uv[li].uv = ((c.x if abs(n.x) < .7 else c.z) + .5, c.y / 4.)
    smooth_by_angle(o)
    return o


def part_finish_arch(M, coll):
    """Graduation gate on the landing: cream marble columns, gold arch and crown, open (the curtain is Unity's)."""
    bm = bmesh.new()
    span = 2.6; H = 3.4
    for sx in (-1, 1):
        x = span * sx
        cylinder(bm, (x, 0, 0), (x, 0, .3), .42, 24, 0)
        cylinder(bm, (x, 0, .3), (x, 0, H), .28, 24, 1)
        cuboid(bm, box_corners((x - .38, -.38, H), (x + .38, .38, H + .3)), 0)
        sphere(bm, (x, 0, H + .55), .26, 0, 16)
    seg = 24; R = span; r = .16
    pts = [Vector((-R * math.cos(math.pi * i / seg), 0, H + .3 + 1.2 * math.sin(math.pi * i / seg))) for i in range(seg + 1)]
    for a, b in zip(pts, pts[1:]):
        cylinder(bm, a, b, r, 14, 0)
    for p in pts[1:-1]:
        sphere(bm, p, r, 0, 10)
    crown(bm, (0, 0, H + 1.45), .55, .9, 0)
    o = new_obj("TU_FinishArch", bm, [M["gold"], M["cream"]], coll)
    boxuv(o, 2.5)
    return o


def part_balustrade(M, coll, L=3.):
    """Sheet 05 balustrade, 3 m, ends flat: base and top rails, eleven turned balusters."""
    bm = bmesh.new()
    cuboid(bm, box_corners((-L / 2, -.22, 0), (L / 2, .22, .16)), 0)
    cuboid(bm, box_corners((-L / 2, -.2, .86), (L / 2, .2, 1.0)), 0)
    n = 9
    for i in range(n):
        x = -L / 2 + L * (i + .5) / n
        prof = [(.26, .08), (.42, .13), (.62, .06), (.86, .09)]
        z0, r0 = .16, .07
        for z1, r1 in prof:
            cone(bm, (x, 0, z0), (x, 0, z1), r0, r1, 8, 0); z0, r0 = z1, r1
    o = new_obj("TU_Balustrade", bm, [M["cream"]], coll)
    boxuv(o, 2.5); smooth_by_angle(o, 50)
    return o


def cone(bm, c0, c1, r0, r1, seg, mi):
    ring0 = []; ring1 = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring0.append(bm.verts.new((c0[0] + math.cos(a) * r0, c0[1] + math.sin(a) * r0, c0[2])))
        ring1.append(bm.verts.new((c1[0] + math.cos(a) * r1, c1[1] + math.sin(a) * r1, c1[2])))
    for i in range(seg):
        j = (i + 1) % seg
        f = bm.faces.new([ring0[i], ring0[j], ring1[j], ring1[i]]); f.material_index = mi; f.smooth = True


def part_post(M, coll):
    bm = bmesh.new()
    cuboid(bm, box_corners((-.3, -.3, 0), (.3, .3, .2)), 0)
    cuboid(bm, box_corners((-.24, -.24, .2), (.24, .24, 1.0)), 0)
    cuboid(bm, box_corners((-.3, -.3, 1.0), (.3, .3, 1.14)), 1)
    cylinder(bm, (0, 0, 1.14), (0, 0, 1.22), .14, 20, 1)
    sphere(bm, (0, 0, 1.42), .22, 1, 20)
    o = new_obj("TU_BalustradePost", bm, [M["cream"], M["gold"]], coll)
    b = o.modifiers.new("bev", 'BEVEL'); b.width = .025; b.segments = 2; b.limit_method = 'ANGLE'; K.apply_mod(o, b)
    boxuv(o, 2.5); smooth_by_angle(o, 50)
    return o


# ------------------------------------------------------------------ floors and moat
def floor_cells():
    occ = {}
    for x0, y0, x1, y1, kind in FLOORS:
        for x in range(int(x0), int(x1)):
            for y in range(int(y0), int(y1)):
                occ[(x, y)] = kind
    return occ


def boundary_runs(occ):
    """Outer edges of the floor union as merged straight runs: (p0, p1, outward normal)."""
    edges = []
    for (x, y) in occ:
        for dx, dy, a, b in ((1, 0, (x + 1, y), (x + 1, y + 1)), (-1, 0, (x, y + 1), (x, y)),
                             (0, 1, (x + 1, y + 1), (x, y + 1)), (0, -1, (x, y), (x + 1, y))):
            if (x + dx, y + dy) not in occ:
                edges.append((a, b, (dx, dy)))
    # merge collinear neighbours
    runs = []
    by = {}
    for a, b, n in edges:
        by.setdefault(n, []).append((a, b))
    for n, es in by.items():
        if n[0] != 0:   # vertical edges, group by x
            keyf = lambda e: e[0][0]; along = 1
        else:
            keyf = lambda e: e[0][1]; along = 0
        groups = {}
        for e in es: groups.setdefault(keyf(e), []).append(e)
        for k, g in groups.items():
            segs = sorted([tuple(sorted((e[0][along], e[1][along]))) for e in g])
            s0, s1 = segs[0]
            for a, b in segs[1:]:
                if a == s1: s1 = b
                else:
                    runs.append((k, s0, s1, n)); s0, s1 = a, b
            runs.append((k, s0, s1, n))
    out = []
    for k, s0, s1, n in runs:
        if n[0] != 0: p0, p1 = (k, s0), (k, s1)
        else: p0, p1 = (s0, k), (s1, k)
        out.append((p0, p1, n))
    return out


def build_floors(M, coll):
    """Floor tops at z 0 (the game's boxes are 1 m thick; their sides are covered by the moat walls)."""
    objs = []
    for x0, y0, x1, y1, kind in FLOORS:
        bm = bmesh.new()
        if kind in ("chess", "board", "board3"):
            sq = 2.5 if kind == "chess" else 2.
            nx = int(round((x1 - x0) / sq)); ny = int(round((y1 - y0) / sq))
            for i in range(nx):
                for j in range(ny):
                    a = (x0 + i * sq, y0 + j * sq); b = (x0 + (i + 1) * sq, y0 + (j + 1) * sq)
                    vs = [bm.verts.new((a[0], a[1], 0)), bm.verts.new((b[0], a[1], 0)), bm.verts.new((b[0], b[1], 0)), bm.verts.new((a[0], b[1], 0))]
                    f = bm.faces.new(vs); f.material_index = (i + j) % 2
            o = new_obj("TU_Floor_%s_%d_%d" % (kind, x0, y0), bm, [M["walnut_m"], M["cream"]], coll)
            # one marble square per texture repeat, each square a different window of the texture
            uv = o.data.uv_layers.new(name="UVMap").data
            for p in o.data.polygons:
                c = p.center; ii = math.floor((c.x - x0) / sq); jj = math.floor((c.y - y0) / sq)
                off = ((ii * 37 + jj * 11) % 7) / 7.
                for li in p.loop_indices:
                    v = o.data.vertices[o.data.loops[li].vertex_index].co
                    uv[li].uv = ((v.x - x0) / sq - ii + off, (v.y - y0) / sq - jj + off * .5)
        else:
            vs = [bm.verts.new((x0, y0, 0)), bm.verts.new((x1, y0, 0)), bm.verts.new((x1, y1, 0)), bm.verts.new((x0, y1, 0))]
            bm.faces.new(vs)
            o = new_obj("TU_Floor_%s_%d_%d" % (kind, x0, y0), bm, [M["paving"]], coll)
            boxuv(o, 2.)
        objs.append(o)
    return objs


def build_trims_and_moat(M, coll, occ):
    """Gold trim flush along every outer floor edge, the moat wall under it down to the moat floor, buttresses."""
    runs = boundary_runs(occ)
    bm_w = bmesh.new(); bm_g = bmesh.new()
    for (ax, ay), (bx, by), (nx, ny) in runs:
        L = math.hypot(bx - ax, by - ay)
        if L < .01: continue
        # beam opening: no wall face where the beam leaves the main floor / meets the landing is still fine (beam is
        # 0.6 m wide and hangs 0.5 m deep), the wall just continues behind it.
        t = Vector((bx - ax, by - ay, 0)).normalized(); n = Vector((nx, ny, 0))
        a = Vector((ax, ay, 0)); b = Vector((bx, by, 0))
        # wall face (slightly proud of the edge so it covers the game box's 1 m side)
        o = n * .02
        cuboid(bm_w, [a + o - n * .4 + Vector((0, 0, MOAT_Z)), b + o - n * .4 + Vector((0, 0, MOAT_Z)),
                      b + o + Vector((0, 0, MOAT_Z)), a + o + Vector((0, 0, MOAT_Z)),
                      a + o - n * .4 + Vector((0, 0, -.28)), b + o - n * .4 + Vector((0, 0, -.28)),
                      b + o + Vector((0, 0, -.28)), a + o + Vector((0, 0, -.28))])
        # gold lip: top 0.28 m of the wall, 6 cm proud, plus a 0.2 m flush strip on the floor top
        g0 = n * .08
        cuboid(bm_g, [a - n * .2 + Vector((0, 0, -.28)), b - n * .2 + Vector((0, 0, -.28)), b + g0 + Vector((0, 0, -.28)), a + g0 + Vector((0, 0, -.28)),
                      a - n * .2 + Vector((0, 0, .004)), b - n * .2 + Vector((0, 0, .004)), b + g0 + Vector((0, 0, .004)), a + g0 + Vector((0, 0, .004))])
        # buttresses every ~6 m
        k = int(L // 6)
        for i in range(1, k + 1):
            c = a + t * (L * i / (k + 1))
            w = .45
            cuboid(bm_w, [c - t * w + Vector((0, 0, MOAT_Z)), c + t * w + Vector((0, 0, MOAT_Z)), c + t * w + n * .5 + Vector((0, 0, MOAT_Z)), c - t * w + n * .5 + Vector((0, 0, MOAT_Z)),
                          c - t * w + Vector((0, 0, -.6)), c + t * w + Vector((0, 0, -.6)), c + t * w + n * .5 + Vector((0, 0, -.9)), c - t * w + n * .5 + Vector((0, 0, -.9))])
    for bm in (bm_w, bm_g):
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    w = new_obj("TU_MoatWalls", bm_w, [M["block"]], coll); boxuv(w, 2.)
    g = new_obj("TU_FloorTrim", bm_g, [M["gold"]], coll)
    return [w, g]


def build_rim(M, coll):
    """The courtyard rim: paving ring at z 0, its inner moat wall, the moat floor, grass outside."""
    x0, y0, x1, y1 = RIM_IN; W = RIM_W
    bm = bmesh.new()
    outer = [(x0 - W, y0 - W), (x1 + W, y0 - W), (x1 + W, y1 + W), (x0 - W, y1 + W)]
    inner = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    vo = [bm.verts.new((x, y, 0)) for x, y in outer]; vi = [bm.verts.new((x, y, 0)) for x, y in inner]
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new([vo[i], vo[j], vi[j], vi[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0: f.normal_flip()
    rim = new_obj("TU_Rim", bm, [M["paving"]], coll); boxuv(rim, 2.)
    # inner moat wall facing the moat, gold lip
    bm = bmesh.new(); bg = bmesh.new()
    c = [Vector((x, y, 0)) for x, y in inner]
    ctr = Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0))
    for i in range(4):
        a, b = c[i], c[(i + 1) % 4]
        t = (b - a).normalized(); n = Vector((-t.y, t.x, 0))
        if n.dot(ctr - a) < 0: n = -n                    # n points into the moat
        Z = Vector((0, 0, 1))
        cuboid(bm, [a - n * .4 + Z * MOAT_Z, b - n * .4 + Z * MOAT_Z, b + Z * MOAT_Z, a + Z * MOAT_Z,
                    a - n * .4 - Z * .28, b - n * .4 - Z * .28, b - Z * .28, a - Z * .28])
        cuboid(bg, [a - n * .25 - Z * .28, b - n * .25 - Z * .28, b + n * .08 - Z * .28, a + n * .08 - Z * .28,
                    a - n * .25 + Z * .004, b - n * .25 + Z * .004, b + n * .08 + Z * .004, a + n * .08 + Z * .004])
        L = (b - a).length; k = int(L // 6)
        for j in range(1, k + 1):
            p = a + t * (L * j / (k + 1)); w = .45
            cuboid(bm, [p - t * w + Z * MOAT_Z, p + t * w + Z * MOAT_Z, p + t * w + n * .5 + Z * MOAT_Z, p - t * w + n * .5 + Z * MOAT_Z,
                        p - t * w - Z * .6, p + t * w - Z * .6, p + t * w + n * .5 - Z * .9, p - t * w + n * .5 - Z * .9])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bmesh.ops.recalc_face_normals(bg, faces=bg.faces)
    wall = new_obj("TU_RimMoatWall", bm, [M["block"]], coll); boxuv(wall, 2.)
    gold = new_obj("TU_RimTrim", bg, [M["gold"]], coll)
    # moat floor
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, MOAT_Z)) for x, y in inner]; bm.faces.new(vs)
    moat = new_obj("TU_MoatFloor", bm, [M["moat"]], coll); boxuv(moat, 4.)
    # grass beyond the castle
    bm = bmesh.new(); G = 400
    go = [bm.verts.new(p) for p in ((-G, -G, -.02), (G, -G, -.02), (G, G, -.02), (-G, G, -.02))]
    gi = [bm.verts.new((x, y, -.02)) for x, y in outer]
    for i in range(4):
        j = (i + 1) % 4
        f = bm.faces.new([go[i], go[j], gi[j], gi[i]])
    for f in bm.faces:
        if f.normal.z < 0: f.normal_flip()
    grass = new_obj("TU_Grass", bm, [M["grass"]], coll)
    return [rim, wall, gold, moat, grass]


# ------------------------------------------------------------------ the course
def build_course(M, coll, P):
    out = []
    # [3] training walls
    for h, y in zip(WALL_H, WALL_Y):
        o = place(P["wall%d" % h], "TU_TrainingWall_%dm_at_%d" % (h, y), (WALL_FRONT_X, y, 0), 0, coll); out.append(o)
    # hurdles, limbo, tunnel
    for x in HURDLE_X:
        out.append(place(P["hurdle"], "TU_Hurdle_%.1f" % x, (x, HURDLE_Y, 0), 0, coll))
    out.append(place(P["limbo"], "TU_LimboStation_at", (LIMBO[0], LIMBO[1], 0), 0, coll))
    out.append(place(P["tunnel"], "TU_Tunnel_at", (TUNNEL[0], TUNNEL[1], 0), 0, coll))
    # sweeper and its pad ring (gold inlay 4 mm above the board)
    out.append(place(P["sweeper"], "TU_Sweeper_at", (BAR_C[0], BAR_C[1], 0), 0, coll))
    bm = bmesh.new()
    for r0, r1 in ((6.9, 7.25), (1.0, 1.25)):
        ring_flat(bm, (BAR_C[0], BAR_C[1], .004), r0, r1, 96)
    out.append(new_obj("TU_BarPadInlay", bm, [M["gold"]], coll))
    # beam, landing gate
    out.append(place(P["beam"], "TU_Beam_at", (0, (BEAM_Y0 + BEAM_Y1) / 2, 0), 0, coll))
    out.append(place(P["arch"], "TU_FinishArch_at", (0, GOAL_Y - .6, 0), 0, coll))
    # [3b] climbing faces
    out.append(climb_staircase(M, coll))
    out.append(solid("TU_ClimbOverhang", [unity_box_corners((CLIMB_X[1], CLIMB_H / 2, CLIMB_Y + .7), (CLIMB_W, CLIMB_H, 1.2), (20, 0, 0))],
                     [M["block"]], coll, bevel=.04))
    slabs = int(math.floor(CLIMB_H / .35))
    cs = []
    for i in range(slabs):
        y = .175 + i * .35; t = i / max(1, slabs - 1)
        lean = -12 + 30 * t; bulge = math.sin(t * math.pi) * .28
        cs.append(unity_box_corners((CLIMB_X[2], y, CLIMB_Y + .5 - bulge), (CLIMB_W, .34, 1.), (lean, 0, 0)))
    out.append(solid("TU_ClimbCurve", cs, [M["block"]], coll, bevel=.03))
    # [4] platform and slopes
    bm = bmesh.new()
    cuboid(bm, box_corners((PLAT_BACK, -HALF, MOAT_Z), (PLAT_EDGE, HALF, PLAT_H)), 0)
    plat = new_obj("TU_Platform", bm, [M["block"], M["paving"]], coll)
    for p in plat.data.polygons:
        if p.normal.z > .9: p.material_index = 1
    b = plat.modifiers.new("bev", 'BEVEL'); b.width = .06; b.segments = 2; K.apply_mod(plat, b)
    boxuv(plat, 2.); out.append(plat)
    bm = bmesh.new()   # gold band under the platform's top edge, on the three faces the player sees
    for lo, hi in (((PLAT_EDGE - .02, -HALF, PLAT_H - .3), (PLAT_EDGE + .06, HALF, PLAT_H - .08)),
                   ((PLAT_BACK, -HALF - .06, PLAT_H - .3), (PLAT_EDGE + .06, -HALF + .02, PLAT_H - .08)),
                   ((PLAT_BACK, HALF - .02, PLAT_H - .3), (PLAT_EDGE + .06, HALF + .06, PLAT_H - .08))):
        cuboid(bm, box_corners(lo, hi))
    out.append(new_obj("TU_PlatformTrim", bm, [M["gold"]], coll))
    for ang, y in zip(SLOPE_ANG, SLOPE_Y):
        r = math.radians(ang); L = PLAT_H / math.sin(r); th = .6
        down = np.array([math.cos(r), -math.sin(r), 0]); nrm = np.array([math.sin(r), math.cos(r), 0])
        top = np.array([PLAT_EDGE, PLAT_H, y])
        c = top + down * (L / 2) - nrm * (th / 2)
        s = solid("TU_Slope_%d" % ang, [unity_box_corners(tuple(c), (L, th, SLOPE_W), (0, 0, -ang))], [M["paving"]], coll, bevel=.04)
        out.append(s)
        # gold rails just outside the slope's sides
        rails = []
        for sy in (-1, 1):
            cc = top + down * (L / 2) + nrm * .05
            rails.append(unity_box_corners((cc[0], cc[1], y + sy * (SLOPE_W / 2 + .12)), (L, .26, .24), (0, 0, -ang)))
        out.append(solid("TU_SlopeRails_%d" % ang, rails, [M["gold"]], coll, bevel=.06, segs=3))
    return out


def ring_flat(bm, c, r0, r1, seg):
    cx, cy, cz = c
    v0 = [bm.verts.new((cx + math.cos(2 * math.pi * i / seg) * r0, cy + math.sin(2 * math.pi * i / seg) * r0, cz)) for i in range(seg)]
    v1 = [bm.verts.new((cx + math.cos(2 * math.pi * i / seg) * r1, cy + math.sin(2 * math.pi * i / seg) * r1, cz)) for i in range(seg)]
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new([v0[i], v0[j], v1[j], v1[i]])


def climb_staircase(M, coll):
    """The big climbing wall: four 3 m blocks, each set back 0.75 m (the union of the builder's four boxes)."""
    x0 = CLIMB_X[0] - LEDGE_W / 2; x1 = CLIMB_X[0] + LEDGE_W / 2
    prof = [(CLIMB_Y, 0.)]
    for i in range(LEDGE_N):
        front = CLIMB_Y + .75 * i
        prof += [(front, LEDGE_STEP_H * i), (front, LEDGE_STEP_H * (i + 1))] if i else [(front, LEDGE_STEP_H)]
        if i < LEDGE_N - 1:
            prof.append((CLIMB_Y + .75 * (i + 1), LEDGE_STEP_H * (i + 1)))
    back = CLIMB_Y + .5 + (LEDGE_N - 1) * LEDGE_BACK + (1 + (LEDGE_N - 1) * LEDGE_BACK) / 2
    prof += [(back, LEDGE_STEP_H * LEDGE_N), (back, 0.)]
    clean = []
    for p in prof:
        if not clean or (abs(clean[-1][0] - p[0]) > 1e-6 or abs(clean[-1][1] - p[1]) > 1e-6): clean.append(p)
    bm = bmesh.new()
    L = [bm.verts.new((x0, y, z)) for y, z in clean]; R = [bm.verts.new((x1, y, z)) for y, z in clean]
    n = len(clean)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([L[i], L[j], R[j], R[i]])
    bm.faces.new(L); bm.faces.new(list(reversed(R)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("TU_ClimbStaircase", bm, [M["block"], M["paving"]], coll)
    for p in o.data.polygons:
        if p.normal.z > .9: p.material_index = 1
    b = o.modifiers.new("bev", 'BEVEL'); b.width = .05; b.segments = 2; b.limit_method = 'ANGLE'; K.apply_mod(o, b)
    boxuv(o, 2.)
    bm = bmesh.new()   # gold band under each ledge's edge
    for i in range(LEDGE_N):
        front = CLIMB_Y + .75 * i; z = LEDGE_STEP_H * (i + 1)
        cuboid(bm, box_corners((x0 - .04, front - .06, z - .24), (x1 + .04, front + .02, z - .06)))
    g = new_obj("tu_stair_gold", bm, [M["gold"]], coll)
    o = K.join([o, g], "TU_ClimbStaircase")
    smooth_by_angle(o)
    return o


def place(src, name, loc, yaw, coll, scale=None):
    o = bpy.data.objects.new(name, src.data)
    o.location = loc; o.rotation_euler = (0, 0, math.radians(yaw))
    if scale: o.scale = scale
    coll.objects.link(o)
    return o


# ------------------------------------------------------------------ castle dressing
def build_castle(M, coll, kit, reuse):
    out = []
    x0, y0, x1, y1 = RIM_IN; W = RIM_W
    X0, Y0, X1, Y1 = x0 - W, y0 - W, x1 + W, y1 + W
    wall = kit["CastleWall"]; tower = kit["CastleTower"]; pil = kit["CastlePilaster"]
    wl, wh = K.bounds(wall); wlen = wh[0] - wl[0]; wdep = wh[1] - wl[1]
    tl, th = K.bounds(tower); trad = (th[0] - tl[0]) / 2
    # sides: (start, end, inward normal) along the outer edge, the wall's back on the edge
    sides = [((X0, Y0), (X1, Y0), (0, 1)), ((X1, Y0), (X1, Y1), (-1, 0)), ((X1, Y1), (X0, Y1), (0, -1)), ((X0, Y1), (X0, Y0), (1, 0))]
    towers = [(X0, Y0), (X1, Y0), (X1, Y1), (X0, Y1), ((X0 + X1) / 2, Y0), (X0, (Y0 + Y1) / 2), (X1, (Y0 + Y1) / 2)]
    gate_x = 0.; gate_half = (K.bounds(kit["Gatehouse"])[1][0] - K.bounds(kit["Gatehouse"])[0][0]) / 2
    for i, (tx, ty) in enumerate(towers):
        out.append(place(tower, "TU_Tower_%d" % i, (tx + (trad * .35 if tx == X0 else -trad * .35 if tx == X1 else 0),
                                                       ty + (trad * .35 if ty == Y0 else -trad * .35 if ty == Y1 else 0), 0), 0, coll))
    n_wall = 0
    for (ax, ay), (bx, by), (nx, ny) in sides:
        a = Vector((ax, ay, 0)); b = Vector((bx, by, 0)); t = (b - a).normalized(); n = Vector((nx, ny, 0))
        L = (b - a).length
        # gaps: towers on this side (corners and the mid tower), the gate on the north side
        stops = [0., L]
        for tx, ty in towers:
            p = Vector((tx, ty, 0)) - a
            d = p.dot(t)
            if 0 < d < L and abs(p.dot(n)) < 1: stops.append(d)
        stops.sort()
        cuts = []
        for s0, s1 in zip(stops, stops[1:]):
            cuts.append((s0 + trad * .8, s1 - trad * .8))
        if ny == -1:                                     # north side: cut the gate out
            g0 = (a.x - (gate_x + gate_half)); g1 = (a.x - (gate_x - gate_half))
            new = []
            for s0, s1 in cuts:
                if s0 < g0 < s1: new.append((s0, g0 + .3))
                if s0 < g1 < s1: new.append((g1 - .3, s1))
                if not (s0 < g0 < s1 or s0 < g1 < s1): new.append((s0, s1))
            cuts = new
        yaw = math.degrees(math.atan2(n.y, n.x)) - 90 + 180   # the kit wall's front looks -y; turn it to look along n
        for s0, s1 in cuts:
            span = s1 - s0
            k = max(1, int(round(span / wlen)))
            sx = span / (k * wlen)
            for j in range(k):
                c = a + t * (s0 + (j + .5) * span / k) + n * (wdep / 2)
                o = place(wall, "TU_Wall_%03d" % n_wall, tuple(c), yaw, coll, scale=(sx, 1, 1)); out.append(o)
                if j % 3 == 1:
                    pc = c + n * (wdep / 2 + .45) + t * (span / k / 2)
                    out.append(place(pil, "TU_Pilaster_%03d" % n_wall, tuple(pc), yaw, coll))
                n_wall += 1
    # gatehouse on the north side, steps in front, statues flanking
    gate = place(kit["Gatehouse"], "TU_Gatehouse_at", (gate_x, Y1 - (K.bounds(kit["Gatehouse"])[1][1] - K.bounds(kit["Gatehouse"])[0][1]) / 2, 0), 0, coll)
    out.append(gate)
    gl, gh = K.bounds(kit["Gatehouse"])
    front_y = Y1 - (gh[1] - gl[1])
    for sx, piece in ((-1, "KnightStatue"), (1, "BishopStatue")):
        out.append(place(kit[piece], "TU_Gate_%s" % piece, (gate_x + sx * (gate_half + 2.2), front_y - .75, 0), 0, coll))
    # niches with statues on the east and west walls
    nl, nh = K.bounds(kit["WallNiche"]); nd = nh[1] - nl[1]
    for side, (xx, yaw) in (("W", (X0 + wdep + nd / 2, 90)), ("E", (X1 - wdep - nd / 2, -90))):
        for k, yy in enumerate((Y0 + (Y1 - Y0) * .27, Y0 + (Y1 - Y0) * .73)):
            out.append(place(kit["WallNiche"], "TU_Niche_%s%d" % (side, k), (xx, yy, 0), yaw, coll))
            piece = "KnightStatue" if (k + (side == "E")) % 2 else "BishopStatue"
            out.append(place(kit[piece], "TU_NicheStatue_%s%d" % (side, k), (xx, yy, .12), yaw, coll))
    # flower urns and cypresses along the rim, inner side
    pot, cyp = reuse.get("pot"), reuse.get("cyp")
    k = 0
    for (ax, ay), (bx, by) in (((x0 + 3, y0 - 2), (x1 - 3, y0 - 2)), ((x1 + 2, y0 + 4), (x1 + 2, y1 - 4)), ((x0 - 2, y0 + 4), (x0 - 2, y1 - 4))):
        a = Vector((ax, ay, 0)); b = Vector((bx, by, 0)); L = (b - a).length; m = int(L // 9)
        for i in range(m + 1):
            p = a.lerp(b, i / max(m, 1))
            src = cyp if i % 2 else pot
            if src: out.append(place(src, "TU_Green_%03d" % k, tuple(p), 0, coll, scale=(1.6, 1.6, 1.6) if src is pot else None))
            k += 1
    return out


def reuse_parts(coll):
    got = {}
    for key, name in (("pot", "D_FlowerPot"), ("cyp", "A_Cypress")):
        path = os.path.join(EARLY, "Export", name + ".fbx")
        if not os.path.exists(path): continue
        bpy.ops.import_scene.fbx(filepath=path)
        o = [x for x in bpy.context.selected_objects if x.type == 'MESH'][0]
        o.data.transform(o.matrix_world); o.parent = None; o.matrix_world.identity()
        for x in list(bpy.context.selected_objects):
            if x is not o: bpy.data.objects.remove(x)
        # FBX exports strip texture paths: point them at the Early textures
        for m in o.data.materials:
            for nd in m.node_tree.nodes:
                if nd.type == 'TEX_IMAGE' and nd.image:
                    fn = os.path.basename(nd.image.filepath)
                    p = os.path.join(EARLY, "Materials", "Textures", fn)
                    if os.path.exists(p): nd.image.filepath = p; nd.image.reload()
        K.ground_center(o)
        for c in o.users_collection: c.objects.unlink(o)
        coll.objects.link(o); o.name = "TU_Reuse_" + name
        got[key] = o
    return got


# ------------------------------------------------------------------ inspection sheets
def inspection(parts, tag, report_only=False):
    """All given parts in a row, back faces magenta, from front-left and back-right."""
    scene = bpy.context.scene
    coll = bpy.data.collections.new("Inspect_" + tag); scene.collection.children.link(coll)
    x = 0.; copies = []
    for o in parts:
        lo, hi = K.bounds(o)
        c = bpy.data.objects.new("insp_" + o.name, o.data); coll.objects.link(c)
        c.location = (x - lo[0], -(lo[1] + hi[1]) / 2, -lo[2])
        copies.append(c); x += hi[0] - lo[0] + 1.
    hide = [ob for ob in scene.objects if ob.users_collection[0] is not coll and ob.type == 'MESH']
    for ob in hide: ob.hide_render = True
    K.mark_backfaces()
    hmax = max(K.bounds(o)[1][2] - K.bounds(o)[0][2] for o in parts)
    cam = K.camera("insp_cam")
    for view, (dx, dy) in (("front", (-.55, -1)), ("back", (.55, 1))):
        dist = x * 1.2
        K.look_at(cam, (x / 2 + dx * dist * .5, dy * dist, hmax * .5 + dist * .45), (x / 2, 0, hmax * .45), ortho=max(x, hmax * 2.4) * 1.05)
        K.render(os.path.join(K.RENDERS, "inspect_%s_%s.png" % (tag, view)))
    for ob in hide: ob.hide_render = False
    for c in copies: bpy.data.objects.remove(c)
    bpy.data.collections.remove(coll)


def unmark_backfaces():
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        nt = m.node_tree; mix = nt.nodes.get("_bf_mix")
        if not mix: continue
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
        src = mix.inputs[1].links[0].from_socket
        nt.links.new(src, out.inputs["Surface"])
        for l in list(mix.inputs[0].links): nt.nodes.remove(l.from_node)
        for l in list(mix.inputs[2].links): nt.nodes.remove(l.from_node)
        nt.nodes.remove(mix)


# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = K.setup_render(1920, 1080)
    M = mats()
    kit_coll = bpy.data.collections.new("Kit"); sc.collection.children.link(kit_coll)
    map_coll = bpy.data.collections.new("Map"); sc.collection.children.link(map_coll)
    report = {}
    kit = tripo_kit(kit_coll, report)
    reuse = reuse_parts(kit_coll)
    P = {}
    for h in WALL_H: P["wall%d" % h] = part_training_wall(M, kit_coll, h)
    P["hurdle"] = part_hurdle(M, kit_coll)
    P["limbo"] = part_limbo(M, kit_coll)
    P["tunnel"] = part_tunnel(M, kit_coll)
    P["sweeper"] = part_sweeper(M, kit_coll)
    P["beam"] = part_beam(M, kit_coll)
    P["arch"] = part_finish_arch(M, kit_coll)
    P["balustrade"] = part_balustrade(M, kit_coll)
    P["post"] = part_post(M, kit_coll)
    for name, o in P.items():
        report.setdefault("made:" + o.name, dict(source="Blender", after=K.inspect(o), tris=K.tris(o),
                                                 size=[round(float(v), 2) for v in (K.bounds(o)[1] - K.bounds(o)[0])]))
    if "--inspect" in ARGS:
        inspection([kit[n] for n in ("CastleWall", "CastleCorner", "CastleTower", "CastlePilaster")], "A1")
        inspection([kit[n] for n in ("Gatehouse", "WallNiche", "KnightStatue", "BishopStatue", "StoneSteps")], "A2")
        inspection([kit[n] for n in ("ClimbBlock", "Ramp", "Tunnel")], "A3")
        inspection([P[k] for k in ("wall2", "wall3", "wall4", "hurdle", "limbo", "tunnel")], "made_course")
        inspection([P[k] for k in ("sweeper", "beam", "arch", "balustrade", "post")], "made_props")
        unmark_backfaces()
    # map
    occ = floor_cells()
    build_floors(M, map_coll)
    build_trims_and_moat(M, map_coll, occ)
    build_rim(M, map_coll)
    build_course(M, map_coll, P)
    # balustrades: on the rim's inner edge (outside the play floors, 10+ m from them), with posts every 3 modules
    x0, y0, x1, y1 = RIM_IN
    bl, bh = K.bounds(P["balustrade"]); blen = bh[0] - bl[0]
    nb = 0
    for (ax, ay), (bx, by), yaw in (((x0, y0 - .5), (x1, y0 - .5), 0), ((x1 + .5, y0), (x1 + .5, y1), 90),
                                    ((x1, y1 + .5), (x0, y1 + .5), 0), ((x0 - .5, y1), (x0 - .5, y0), 90)):
        a = Vector((ax, ay, 0)); b = Vector((bx, by, 0)); L = (b - a).length; k = int(L // blen)
        for i in range(k):
            p = a.lerp(b, (i + .5) / k)
            place(P["balustrade"], "TU_Rail_%03d" % nb, tuple(p), yaw, map_coll, scale=(L / k / blen, 1, 1))
            if i % 3 == 0:
                q = a.lerp(b, i / k); place(P["post"], "TU_RailPost_%03d" % nb, tuple(q), yaw, map_coll)
            nb += 1
    build_castle(M, map_coll, kit, reuse)
    kit_coll.hide_render = True
    for o in kit_coll.objects: o.hide_set(True)
    print("REPORT", json.dumps(report))
    with open(os.path.join(K.HERE, "tu_kit_report.json"), "w") as f:
        json.dump(report, f, indent=1)
    if "--render" in ARGS:
        views = next((a.split("=")[1].split(",") for a in ARGS if a.startswith("--views=")), None)
        render_views(views)
    if "--save" in ARGS:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(K.HERE, "TU_Map_v1.blend"), compress=True)


VIEWS = {
    "overview":   ((-70, -95, 72), (-3, -2, -2), 30),
    "concept":    ((-58, -62, 34), (2, 2, 0), 28),
    "walls":      ((3, -24, 9), (22, -2, 1.5), 32),
    "low":        ((-2, -20, 4.5), (7, -6, .3), 30),
    "beam":       ((9, -6, 6), (0, -30, -.5), 32),
    "bar":        ((14, 8, 8), (0, 24, 0), 30),
    "slopes":     ((-8, -26, 12), (-32, 0, 2), 30),
    "climb":      ((3, -2, 7), (-4, 16, 5), 30),
    "gate":       ((0, 18, 6), (0, 46, 4), 32),
}


def render_views(only=None):
    K.use_cycles(int(next((a.split("=")[1] for a in ARGS if a.startswith("--samples=")), 48)))
    cam = K.camera("cam")
    for name, (loc, tgt, lens) in VIEWS.items():
        if only and name not in only: continue
        K.look_at(cam, loc, tgt, lens=lens)
        K.render(os.path.join(K.RENDERS, "map_%s.png" % name))


if __name__ == "__main__":
    main()
