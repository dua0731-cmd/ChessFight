# Rig the user's Tripo pieces on the pawn's own Mixamo skeleton, as main's Tools/PieceRig/rig_piece.py does:
#   body - by height along Hips > Spine > Spine1 > Spine2 > Head (the pawn's bone heights; the rook bends lower,
#          0.30 / 0.34 / 0.38 / 0.42, as main has it); everything above the Head bone follows the head
#   arms, hands, feet - the low-poly pawn's own, where the pawn has them (the game's physics is the pawn's for every
#          piece), their weights as they are; an arm stub's root is pushed deeper where a body is too narrow to hide it
# Exports per piece: <Piece>_Rigged.fbx/.blend, <Piece>_Body.glb (body only, uv unwrapped, for Tripo's textures),
# and rest / posed renders. blender -b -P rig_pieces.py -- <pawn rig .blend> <pieces dir> <out dir> <piece>...
import bpy, bmesh, sys, os, math, json
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tripo_clean as tc

a = sys.argv[sys.argv.index("--") + 1:]
PAWN, PDIR, OUT = a[:3]
NAMES = a[3:]
os.makedirs(OUT, exist_ok=True)
P = "mixamorig:"

def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

DL = "C:/Users/trews/Downloads/"
HEIGHT = {"bishop": 1.508 * 0.88, "rook": 1.150 * 0.88, "knight": 1.332 * 0.88}
# pieces whose eyes Tripo carved into the body itself (the others have them as loose shells, or none)
CARVED = {"knight"}
# The arms where the user's lineup sheet (11-09, every piece side by side at one scale) and the bishop's own sheet
# (11-12) have them. The game draws each arm about the piece's own shoulder (PieceModelBaker marks it where the stub
# leaves the body, RagdollVisualSync.PoseAsPiece turns the arm there); only the physics stays the pawn's. So the arms
# go where each body needs them, not where the pawn's are. The knight's already match its sheet.
ARMS = {
    # the rook is 0.247 m round at the arms, wider than where the pawn's cuff starts (0.222): its stub and cuff were
    # inside the body and only the balls showed. Out 6.4 cm: the cuff starts 3.9 cm off the body, as on the sheet.
    "rook": dict(dx=0.064),
    # the bishop's arms leave the neck column between the ring band (z 0.60-0.62) and the collar disc (0.71-0.75),
    # 0.087 m round, on both sheets; the pawn's arms sat 16 cm lower, on the skirt. Its own arm (bishop arm.fbx: a
    # turned spindle that flares into the ball), the ball as big as the pawn's and 7.5 percent further out (the
    # sheet), the spindle thinned to 61 percent about its axis so its narrow end (5.6 cm) fits the 7 cm column.
    "bishop": dict(model="bishop arm.fbx", z=0.665, ball_x=0.335, thin=0.61),
}

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

def tripo_body(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=DL + name + ".fbx")
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
    o = bpy.context.view_layer.objects.active
    # split into loose parts; drop the ones lying (90 percent of their points) inside another part
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [p for p in bpy.context.selected_objects if p.type == "MESH"]
    # the faces Tripo sculpted go (the user's call, 2026-11-10): every piece takes the same mesh eyes later
    body0 = max(parts, key=lambda p: np.ptp([v.co.z for v in p.data.vertices]))
    face = tc.face_shells(parts, body0)
    parts = [p for p in parts if p not in face]
    for p in face:
        bpy.data.objects.remove(p, do_unlink=True)
    refilled = 0
    if name in CARVED:
        boxes, nnm = tc.carved_boxes(body0)
        refilled, left = tc.refill(body0, boxes)
    print("FACE", name, len(face), "face shells removed,", refilled, "verts faired where eyes were carved")
    trees = {p.name: bvh(p) for p in parts}
    keep = []
    for p in parts:
        pts = [p.matrix_world @ v.co for v in p.data.vertices]
        sample = pts[:: max(1, len(pts) // 80)]
        others = [trees[q.name] for q in parts if q is not p]
        frac = sum(1 for x in sample if any(inside_parity(t, x) for t in others)) / len(sample)
        (keep.append(p) if not (frac >= 0.9 and others) else bpy.data.objects.remove(p, do_unlink=True))
    bpy.ops.object.select_all(action="DESELECT")
    for p in keep:
        p.select_set(True)
    bpy.context.view_layer.objects.active = keep[0]
    if len(keep) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    print("TRIPO", name, "parts kept", len(keep), "of", len(parts))
    vs = [v.co for v in o.data.vertices]
    mn, mx = min(p.z for p in vs), max(p.z for p in vs)
    low = [p for p in vs if p.z < mn + 0.05 * (mx - mn)]
    bx = (min(p.x for p in low) + max(p.x for p in low)) / 2
    by = (min(p.y for p in low) + max(p.y for p in low)) / 2
    k = HEIGHT[name] / (mx - mn)
    o.data.transform(Matrix.Translation(Vector((0, 0, 0.12))) @ Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-bx, -by, -mn))))
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    if o.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for p in o.data.polygons:
        p.use_smooth = False
    o.vertex_groups.clear()
    return o

def body_radius(tree, z, side):
    d = Vector((side, 0, 0))
    h = tree.ray_cast(Vector((0, 0, z)) + d * 2, -d)
    return abs(h[0].x) if h[0] else 0.0

def limb_verts(limbs, ends):
    groups = {g.index for g in limbs.vertex_groups if g.name.endswith(ends)}
    return [v for v in limbs.data.vertices if sum(g.weight for g in v.groups if g.group in groups) >= 0.5]

def arms_out(limbs, dx):
    for v in limb_verts(limbs, ("Arm", "Hand")):
        v.co.x += dx if v.co.x > 0 else -dx
    limbs.data.update()

def own_arms(limbs, cfg, tree):
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
    keep = [p for p, gone in zip(parts, inside) if not gone]
    for p, gone in zip(parts, inside):
        if gone:
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
    made = []
    for side, sg, objs in (("Left", 1, keep), ("Right", -1, right)):
        for p, src in zip(objs, keep):
            k = cfg["thin"] if src is spindle else 1.0
            # the arm's axis to the origin, the spindle thinned about it, the ball's centre to y 0, scaled, turned to
            # point out along +x / -x, onto the ball's place
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
                print("ARM", side, "ball %.3f (scale %.3f), root cut at %.3f, narrowest body there %.3f" % (ball_d, s, sg * cut, rmin))
            bpy.ops.object.select_all(action="DESELECT")
            p.select_set(True)
            bpy.context.view_layer.objects.active = p
            if p.data.has_custom_normals:
                bpy.ops.mesh.customdata_custom_splitnormals_clear()
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
            made.append(p)
    bpy.ops.object.select_all(action="DESELECT")
    for p in made:
        p.select_set(True)
    limbs.select_set(True)
    bpy.context.view_layer.objects.active = limbs
    bpy.ops.object.join()

report = {}
for name in NAMES:
    Name = name.capitalize()
    bpy.ops.wm.open_mainfile(filepath=PAWN)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    pawn = bpy.data.objects["Pawn"]
    W = arm.matrix_world
    bone = {n: W @ arm.data.bones[P + n].head_local for n in ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head")}
    # the pawn's limbs: vertices mostly on an arm, hand or foot bone; everything else goes
    limb_groups = {g.index for g in pawn.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Foot"))}
    me = pawn.data
    keepv = set()
    for v in me.vertices:
        if sum(g.weight for g in v.groups if g.group in limb_groups) >= 0.5:
            keepv.add(v.index)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keepv], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    limbs = pawn
    limbs.name = Name + "_Limbs"

    # the piece's body: the user's Tripo model, hidden inner shells dropped, stood on the base (z 0.12) at the
    # lineup's height, centred on its base, facing -y as Tripo exports, flat faces
    body = tripo_body(name)
    body.name = Name
    bpy.context.view_layer.objects.active = body
    tree = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [tuple(p.vertices) for p in body.data.polygons])
    assert limbs.matrix_world == Matrix.Identity(4) and body.matrix_world == Matrix.Identity(4)
    cfg = ARMS.get(name, {})
    if "dx" in cfg:
        arms_out(limbs, cfg["dx"])
    if "model" in cfg:
        own_arms(limbs, cfg, tree)

    # an arm stub's root must stay inside the body over the stub's whole height: where the body is narrower than the
    # pawn's (whose stubs start 0.104 m out), push the root's cap inward to 12 mm inside the narrowest body there
    arm_groups = {g.index: g.name for g in limbs.vertex_groups if g.name.endswith("Arm")}
    stub = [v for v in limbs.data.vertices if any(g.group in arm_groups and g.weight > 0.5 for g in v.groups)]
    zlo, zhi = min(v.co.z for v in stub), max(v.co.z for v in stub)
    def body_r(z, side):
        d = Vector((side, 0, 0))
        h = tree.ray_cast(Vector((0, 0, z)) + d * 2, -d)
        return abs(h[0].x) if h[0] else 0.0
    moved = 0
    for side in (1, -1):
        rmin = min(body_r(zlo + (zhi - zlo) * k / 8, side) for k in range(9))
        inner = min(abs(v.co.x) for v in stub if v.co.x * side > 0)
        want = rmin - 0.012
        if inner > want:
            for v in stub:
                if v.co.x * side > 0 and abs(v.co.x) < inner + 0.004:
                    v.co.x = side * want
                    moved += 1
    limbs.data.update()

    # body weights: by height along the chain, the head bone's height and above on the head
    chain = ["Hips", "Spine", "Spine1", "Spine2", "Head"]
    zs = [bone[c].z for c in chain]
    if name == "rook":
        zs = [bone["Hips"].z, 0.30, 0.34, 0.38, 0.42]
    for c in chain:
        body.vertex_groups.new(name=P + c)
    for v in body.data.vertices:
        z = v.co.z
        if z <= zs[0]:
            w = {"Hips": 1.0}
        elif z >= zs[-1]:
            w = {"Head": 1.0}
        else:
            for k in range(4):
                if z <= zs[k + 1]:
                    t = smooth((z - zs[k]) / (zs[k + 1] - zs[k]))
                    w = {chain[k]: 1 - t, chain[k + 1]: t}
                    break
        for b, x in w.items():
            if x > 1e-4:
                body.vertex_groups[P + b].add([v.index], x, "REPLACE")

    # the body alone, unrigged, uv unwrapped, for Tripo's texture generator
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
    report[name] = dict(vertices=len(vs), faces=len(piece.data.polygons), stub_root_moved=moved,
                        top=round(max(v.z for v in vs), 4), chain=[round(z, 3) for z in zs])
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
