# Rig the new low-poly bodies (bishop of the final design, king, queen) on the pawn's own Mixamo skeleton, with the
# rules rig_tripo.py uses for the rook, knight and the earlier bishop:
#   body - by height along Hips > Spine > Spine1 > Spine2 > Head (the pawn's bone heights); everything above the
#          Head bone follows the head
#   feet - the low-poly pawn's own, where the pawn has them (the game's physics is the pawn's for every piece)
#   arms - where each design has them: the game turns an arm about the piece's own shoulder (PieceModelBaker marks
#          it where the stub leaves the body). The bishop keeps its own arm (bishop arm.fbx) on the middle of its
#          neck column; the king and queen take the pawn's arms, lifted and moved to their sheets' place. A stub's
#          root is pushed 12 mm inside the narrowest body round it.
# The body comes from a .blend made by make_bishop.py / make_royals.py instead of a Tripo fbx.
# blender -b --factory-startup -P rig_lowpoly.py -- <pawn rig .blend> <out dir> <piece>=<body .blend> ...
import bpy, bmesh, sys, os, math, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index("--") + 1:]
PAWN, OUT = a[:2]
JOBS = [v.split("=", 1) for v in a[2:]]
os.makedirs(OUT, exist_ok=True)
P = "mixamorig:"
DL = "C:/Users/trews/Downloads/"

def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

# The arms. z: the arm's height; ball_x: the hand ball's centre from the axis (both off each piece's sheet).
ARMS = {
    # the neck column of the final design: between the ring band (z 0.56-0.60) and the disc (0.69-0.71), 0.091 m
    # round; its middle. The ball as far out as before (the bishop's sheet), the spindle thinned as before.
    "bishop": dict(model="bishop arm.fbx", z=0.6346, ball_x=0.335, thin=0.61),
    # the king's arms leave the body just under the shoulder disc; the queen's cross her waist ring
    "king": dict(z=0.7283, ball_x=0.339),
    "queen": dict(z=0.8031, ball_x=0.273),
}
SMOOTH_PARTS = {"bishop": {3}}                     # the bishop's top ball is round on its sheet

def bvh(o):
    return BVHTree.FromPolygons([o.matrix_world @ v.co for v in o.data.vertices], [tuple(p.vertices) for p in o.data.polygons])

def inside_parity(tree, p):
    votes = 0
    for d in (Vector((1, 0.013, 0.007)), Vector((-0.011, 1, 0.017)), Vector((0.009, -0.015, 1))):
        d.normalize()
        n, q = 0, p.copy()
        for _ in range(64):
            hit = tree.ray_cast(q, d)
            if hit[0] is None:
                break
            n += 1
            q = hit[0] + d * 1e-5
        votes += n % 2
    return votes >= 2

def body_radius(tree, z, side):
    d = Vector((side, 0, 0))
    h = tree.ray_cast(Vector((0, 0, z)) + d * 2, -d)
    return abs(h[0].x) if h[0] else 0.0

def limb_verts(limbs, ends):
    groups = {g.index for g in limbs.vertex_groups if g.name.endswith(ends)}
    return [v for v in limbs.data.vertices if sum(g.weight for g in v.groups if g.group in groups) >= 0.5]

def ball_of(limbs):
    """Centre x, centre z and diameter of the pawn's left hand ball."""
    hv = [v.co for v in limb_verts(limbs, ("Hand",)) if v.co.x > 0]
    zs, xs = [p.z for p in hv], [p.x for p in hv]
    d = max(zs) - min(zs)
    return max(xs) - d / 2, (max(zs) + min(zs)) / 2, d

# The stub's root. The game turns an arm about the piece's shoulder mark, which PieceModelBaker puts 12 mm out from
# the innermost vertex that is mostly on the arm bone - so that vertex ring sits 12 mm inside the body and the mark
# lands on the body's surface. A stub that ends there (as the pawn's does) shows its cut end when the arm turns far
# or when the body under it leans the other way (tall pieces: the body up there follows the head bone, the arm the
# chest). So every stub goes on, deep into the body, and that hidden part blends from the arm bone to the body's own
# bones: whatever the arm does, its root stays in the body and the skin between bends instead of pulling out.
ROOT_IN = 0.012                                    # the arm bone's innermost ring, inside the surface
SLEEVE = [(0.022, 0.45), (0.050, 0.15)]            # further in: (depth beyond that ring, share left on the arm bone)
DEEP_X = 0.012                                     # the root's end, next to the axis
SIDES = 16

def make_stub(side, sg, y, z, r, x_root, x_out, body_w):
    """A round stub along x for one side (sg +1 left, -1 right): from next to the axis out to x_out (or only its hidden
    root, up to x_root, when x_out is None), weighted as described above. body_w(z) gives the body's weights there."""
    rings = [(DEEP_X, 0.0)] + [(x_root - d, a) for d, a in reversed(SLEEVE) if x_root - d > DEEP_X + 0.008] + [(x_root, 1.0)]
    if x_out is not None:
        rings += [(x_root + ROOT_IN + 0.004, 1.0), ((x_root + ROOT_IN + x_out) / 2, 1.0), (x_out, 1.0)]
    bm = bmesh.new()
    loops = []
    for x, _ in rings:
        loops.append([bm.verts.new((sg * x, y + r * math.cos(2 * math.pi * k / SIDES), z + r * math.sin(2 * math.pi * k / SIDES)))
                      for k in range(SIDES)])
    for lo, hi in zip(loops, loops[1:]):
        for k in range(SIDES):
            bm.faces.new((lo[k], lo[(k + 1) % SIDES], hi[(k + 1) % SIDES], hi[k]))
    bm.faces.new(loops[0])
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("Stub")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("Stub_" + side, me)
    bpy.context.scene.collection.objects.link(o)
    bw = body_w(z)
    for n in [side + "Arm"] + list(bw):
        o.vertex_groups.new(name=P + n)
    for i, (x, a) in enumerate(rings):
        ids = list(range(i * SIDES, (i + 1) * SIDES))
        if a > 1e-4:
            o.vertex_groups[P + side + "Arm"].add(ids, a, "REPLACE")
        for b_, w in bw.items():
            if (1 - a) * w > 1e-4:
                o.vertex_groups[P + b_].add(ids, (1 - a) * w, "REPLACE")
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    return o

def join_into(limbs, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    limbs.select_set(True)
    bpy.context.view_layer.objects.active = limbs
    bpy.ops.object.join()

def pawn_arms(limbs, cfg, tree, body_w):
    """The pawn's cuff and hand ball moved to the piece's place; its stub replaced by the piece's own (same thickness,
    as long as this body needs, its root deep inside)."""
    bx, bz, d = ball_of(limbs)
    dx, dz = cfg["ball_x"] - bx, cfg["z"] - bz
    arm_v = [v for v in limb_verts(limbs, ("Arm",)) if v.co.x > 0]
    ys, zs_ = [v.co.y for v in arm_v], [v.co.z for v in arm_v]
    y0, r = (min(ys) + max(ys)) / 2, (max(zs_) - min(zs_)) / 2
    x_out = max(v.co.x for v in arm_v) + dx
    for v in limb_verts(limbs, ("Hand",)):
        v.co.x += dx if v.co.x > 0 else -dx
        v.co.z += dz
    gone = {v.index for v in limb_verts(limbs, ("Arm",))}
    bm = bmesh.new()
    bm.from_mesh(limbs.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in gone], context="VERTS")
    bm.to_mesh(limbs.data)
    bm.free()
    made, info = [], {}
    for side, sg in (("Left", 1), ("Right", -1)):
        rmin = min(body_radius(tree, cfg["z"] - r + 2 * r * j / 8, sg) for j in range(9))
        made.append(make_stub(side, sg, y0, cfg["z"], r, rmin - ROOT_IN, x_out, body_w))
        info[side] = round(rmin, 4)
    join_into(limbs, made)
    print("ARMS own stubs r %.3f from the axis to x %.3f, body there %s; cuff and ball moved out %.3f, up %.3f (ball %.3f at x %.3f z %.3f)"
          % (r, x_out, info, dx, dz, d, cfg["ball_x"], cfg["z"]))
    return info

def own_arms(limbs, cfg, tree, body_w):
    # the pawn's ball size and front-to-back place, then its arms and hands go (its feet stay)
    arm_v, hand_v = limb_verts(limbs, ("Arm",)), limb_verts(limbs, ("Hand",))
    hz = [v.co.z for v in hand_v if v.co.x > 0]
    ball_d = max(hz) - min(hz)
    ay = [v.co.y for v in arm_v]
    arm_y = (min(ay) + max(ay)) / 2
    gone = {v.index for v in arm_v + hand_v}
    bm = bmesh.new()
    bm.from_mesh(limbs.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in gone], context="VERTS")
    bm.to_mesh(limbs.data)
    bm.free()
    # the user's arm: along y with the ball at -y, in loose parts; a part lying inside another goes
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=DL + cfg["model"])
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    ms = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in ms:
        o.parent = None
        o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(ms) > 1:
        bpy.ops.object.join()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [p for p in bpy.context.selected_objects if p.type == "MESH"]
    trees = [bvh(p) for p in parts]
    inside = []
    for i, p in enumerate(parts):
        pts = [p.matrix_world @ v.co for v in p.data.vertices]
        sample = pts[:: max(1, len(pts) // 80)]
        others = [t for j, t in enumerate(trees) if j != i]
        inside.append(bool(others) and sum(1 for x in sample if any(inside_parity(t, x) for t in others)) / len(sample) >= 0.9)
    keep = [p for p, gone_ in zip(parts, inside) if not gone_]
    for p, gone_ in zip(parts, inside):
        if gone_:
            bpy.data.objects.remove(p, do_unlink=True)
    print("ARM model parts", len(parts), "kept", len(keep))
    ball = min(keep, key=lambda p: min(v.co.y for v in p.data.vertices))
    spindle = max(keep, key=lambda p: max(v.co.y for v in p.data.vertices))
    sv = [v.co for v in spindle.data.vertices]
    ax, az = (min(p.x for p in sv) + max(p.x for p in sv)) / 2, (min(p.z for p in sv) + max(p.z for p in sv)) / 2
    bv = [v.co for v in ball.data.vertices]
    bmn = Vector((min(p.x for p in bv), min(p.y for p in bv), min(p.z for p in bv)))
    bmx = Vector((max(p.x for p in bv), max(p.y for p in bv), max(p.z for p in bv)))
    s = ball_d / (sum(bmx - bmn) / 3)
    right = []
    for p in keep:
        c = p.copy()
        c.data = p.data.copy()
        bpy.context.scene.collection.objects.link(c)
        right.append(c)
    made, roots, info = [], [], {}
    for side, sg, objs in (("Left", 1, keep), ("Right", -1, right)):
        for p, src in zip(objs, keep):
            k = cfg["thin"] if src is spindle else 1.0
            m = (Matrix.Translation(Vector((sg * cfg["ball_x"], arm_y, cfg["z"]))) @ Matrix.Rotation(math.radians(90 * sg), 4, "Z")
                 @ Matrix.Scale(s, 4) @ Matrix.Translation(Vector((0, -(bmn.y + bmx.y) / 2, 0)))
                 @ Matrix.Diagonal((k, 1, k, 1)) @ Matrix.Translation(Vector((-ax, 0, -az))))
            p.data.transform(m)
            p.data.materials.clear()
            p.vertex_groups.clear()
            g = p.vertex_groups.new(name=P + side + ("Arm" if src is spindle else "Hand"))
            g.add(list(range(len(p.data.vertices))), 1.0, "REPLACE")
            if src is spindle:
                # the root 12 mm inside the narrowest body round it, capped
                pz = [v.co.z for v in p.data.vertices if 0 < sg * v.co.x < cfg["ball_x"] - 0.1]
                rmin = min(body_radius(tree, min(pz) + (max(pz) - min(pz)) * j / 8, sg) for j in range(9))
                cut = rmin - 0.012
                bm = bmesh.new()
                bm.from_mesh(p.data)
                bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6,
                                       plane_co=Vector((sg * cut, 0, 0)), plane_no=Vector((sg, 0, 0)), clear_inner=True)
                edges = [e for e in bm.edges if e.is_boundary]
                if edges:
                    filled = bmesh.ops.holes_fill(bm, edges=edges, sides=0)
                    bmesh.ops.triangulate(bm, faces=filled["faces"])
                bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
                bm.to_mesh(p.data)
                bm.free()
                # its hidden root: as thick as the spindle is at the cut, on into the body (see make_stub)
                ring = [v.co for v in p.data.vertices if abs(abs(v.co.x) - cut) < 1e-4]
                r_cut = max(math.hypot(c.y - arm_y, c.z - cfg["z"]) for c in ring)
                roots.append(make_stub(side, sg, arm_y, cfg["z"], r_cut, cut, None, body_w))
                info[side] = round(rmin, 4)
                print("ARM", side, "ball %.3f (scale %.3f), root cut at %.3f (r %.3f), narrowest body there %.3f" % (ball_d, s, sg * cut, r_cut, rmin))
            bpy.ops.object.select_all(action="DESELECT")
            p.select_set(True)
            bpy.context.view_layer.objects.active = p
            if p.data.has_custom_normals:
                bpy.ops.mesh.customdata_custom_splitnormals_clear()
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
            made.append(p)
    join_into(limbs, made + roots)
    return info

report = {}
for name, src in JOBS:
    Name = name.capitalize()
    bpy.ops.wm.open_mainfile(filepath=PAWN)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    pawn = bpy.data.objects["Pawn"]
    W = arm.matrix_world
    bone = {n: W @ arm.data.bones[P + n].head_local for n in ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head")}
    # the pawn's limbs: vertices mostly on an arm, hand or foot bone; everything else goes
    limb_groups = {g.index for g in pawn.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Foot"))}
    me = pawn.data
    keepv = {v.index for v in me.vertices if sum(g.weight for g in v.groups if g.group in limb_groups) >= 0.5}
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keepv], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    limbs = pawn
    limbs.name = Name + "_Limbs"

    # the piece's body: the low-poly model, already at the game's scale, standing on z 0.12, facing -y, flat faces
    with bpy.data.libraries.load(src) as (data_from, data_to):
        data_to.objects = [n for n in data_from.objects]
    body = [o for o in data_to.objects if o is not None and o.type == "MESH"][0]
    bpy.context.scene.collection.objects.link(body)
    body.name = Name
    body.data.materials.clear()
    body.vertex_groups.clear()
    part = body.data.attributes.get("part")
    smooth_parts = SMOOTH_PARTS.get(name, set())
    for p in body.data.polygons:
        p.use_smooth = bool(part and part.data[p.index].value in smooth_parts)
    bpy.context.view_layer.objects.active = body
    tree = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [tuple(p.vertices) for p in body.data.polygons])
    assert limbs.matrix_world == Matrix.Identity(4) and body.matrix_world == Matrix.Identity(4)
    # body weights: by height along the chain, the head bone's height and above on the head
    chain = ["Hips", "Spine", "Spine1", "Spine2", "Head"]
    zs = [bone[c].z for c in chain]
    def body_w(z):
        if z <= zs[0]:
            return {"Hips": 1.0}
        if z >= zs[-1]:
            return {"Head": 1.0}
        for k in range(4):
            if z <= zs[k + 1]:
                t = smooth((z - zs[k]) / (zs[k + 1] - zs[k]))
                return {chain[k]: 1 - t, chain[k + 1]: t}
    for c in chain:
        body.vertex_groups.new(name=P + c)
    for v in body.data.vertices:
        for b_, x in body_w(v.co.z).items():
            if x > 1e-4:
                body.vertex_groups[P + b_].add([v.index], x, "REPLACE")
    body_verts = len(body.data.vertices)

    # the arms, each stub's root deep in the body and blended into it (make_stub)
    cfg = ARMS[name]
    gap = own_arms(limbs, cfg, tree, body_w) if "model" in cfg else pawn_arms(limbs, cfg, tree, body_w)
    moved = 0
    bpy.context.view_layer.objects.active = body

    # the body alone, unrigged, uv unwrapped
    alone = body.copy()
    alone.data = body.data.copy()
    alone.vertex_groups.clear()
    bpy.context.scene.collection.objects.link(alone)
    bpy.ops.object.select_all(action="DESELECT")
    alone.select_set(True)
    bpy.context.view_layer.objects.active = alone
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, Name + "_Body.glb"), use_selection=True, export_format="GLB")
    bpy.data.objects.remove(alone, do_unlink=True)

    # join body + limbs, on the armature
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    limbs.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    piece = bpy.context.view_layer.objects.active
    piece.name = Name
    for m in list(piece.modifiers):
        piece.modifiers.remove(m)
    mod = piece.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    piece.parent = arm
    piece.matrix_parent_inverse = arm.matrix_world.inverted()
    vs = [v.co for v in piece.data.vertices]
    unweighted = sum(1 for v in piece.data.vertices if not v.groups)
    report[name] = dict(vertices=len(vs), body_vertices=body_verts, faces=len(piece.data.polygons), stub_root_moved=moved,
                        body_radius_at_arm=gap, top=round(max(v.z for v in vs), 4), chain=[round(z, 3) for z in zs],
                        arms=cfg, unweighted=unweighted)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    piece.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, Name + "_Rigged.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, mesh_smooth_type="FACE",
                             use_armature_deform_only=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, Name + "_Rigged.blend"))
    print("RIGGED", name, json.dumps(report[name]))

with open(os.path.join(OUT, "rig_report.json"), "w") as f:
    json.dump(report, f, indent=1)
print("DONE")
