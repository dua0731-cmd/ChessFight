# Rig the low-poly pieces on the pawn's own Mixamo skeleton, as main's Tools/PieceRig/rig_piece.py does:
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

a = sys.argv[sys.argv.index("--") + 1:]
PAWN, PDIR, OUT = a[:3]
NAMES = a[3:]
os.makedirs(OUT, exist_ok=True)
P = "mixamorig:"

def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

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

    # the piece's body
    with bpy.data.libraries.load(os.path.join(PDIR, Name + "_LowPoly.blend")) as (src, dst):
        dst.objects = [n for n in src.objects]
    body = [o for o in dst.objects if o.type == "MESH"][0]
    bpy.context.scene.collection.objects.link(body)
    body.name = Name
    bpy.context.view_layer.objects.active = body
    tree = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [tuple(p.vertices) for p in body.data.polygons])

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
