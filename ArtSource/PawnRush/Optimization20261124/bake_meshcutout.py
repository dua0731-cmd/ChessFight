# R823 (user 11-24, the central landing's wooden railing and the dais tower's pilasters back in the grand stair hall:
# "계단을 올라가면 샹들리에 쪽 나무 난간을 지워주고 원형 기둥에 기둥으로 된 것들 지워"): bakes MeshCutOut's cuts into
# GrandStairHallV15.fbx. R340 / R348 / R350 / R410 cut them in Play (MeshCutOut), but since R309 the hall's art is put on
# in Play on an active object and the component woke before it had its boxes, and a player build cannot read the
# model's mesh (Read/Write off) - so nothing was cut. Here the same rule as MeshCutOut.cs runs on the Blender mesh:
#
#   pieces = triangles joined through corners at the same place (Unity mesh units rounded to 1/1000), a piece whose
#   middle lies in a box goes whole, and any other triangle whose own middle lies in a box goes too.
#
# Positions go into the cut's frame as in Play: Unity mesh space = (SIGN_X * x / 100, y / 100, z / 100) of the Blender
# mesh (the FBX in centimetres; the importer mirrors x), then the frame matrix the 11-24 Play probe read off each
# MeshCutOut (frame.worldToLocal * transform.localToWorld) - written below with the boxes, as Course01v4Builder gives them.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python bake_meshcutout.py -- <in.fbx> <out.fbx> [sign]

import bpy, bmesh, sys
from mathutils import Matrix, Vector

CUTS = {
    "HallV15_Balustrades": (
        [-100, 8.742278e-06, 3.12648215e-12, 0, 0, -3.57627869e-05, 100, 0, 8.742278e-06, 100, 3.57627869e-05, 0, 0, 0, 0, 1],
        [((-5.3, 9, 65.675), (18.6, 3, 0.75)), ((-13.95, 9, 58.45), (1.3, 3, 15.7)), ((13.95, 9, 57.3), (1.3, 3, 13.4)),
         ((-11.48, 9, 51.15), (6.24, 3, 1.1)), ((11.48, 9, 51.15), (6.24, 3, 1.1)), ((4.15, 9, 68.8), (0.9, 3, 6.4)),
         ((7.45, 9, 71.35), (7.1, 3, 1.3)), ((-8.05, 2.25, 32.85), (0.7, 16.5, 39.1)), ((8.05, 2.25, 32.85), (0.7, 16.5, 39.1))]),
    "HallV15_Royal dais tower (arch)": (
        [100, -5.684342e-14, -2.032879e-20, 5.684342e-14, 0, -3.57627869e-05, 100, 0, -5.684342e-14, -100, -3.57627869e-05, 0, 0, 0, 0, 1],
        [((0, 5.79996443, -91.99999), (20, 6, 20))]),
}

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = args[0], args[1]
SIGN_X = float(args[2]) if len(args) > 2 else -1.0


def inside(p, boxes):
    for c, s in boxes:
        if all(abs(p[i] - c[i]) <= s[i] * .5 for i in range(3)):
            return True
    return False


def find(parent, i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC, use_custom_normals=True)
for name, (flat, boxes) in CUTS.items():
    ob = bpy.data.objects[name]
    me = ob.data
    M = Matrix([flat[0:4], flat[4:8], flat[8:12], flat[12:16]])
    unity = [Vector((SIGN_X * v.co.x / 100., v.co.y / 100., v.co.z / 100.)) for v in me.vertices]
    frame = [(M @ u.to_4d()).to_3d() for u in unity]
    n = len(unity)
    parent = list(range(n))
    at = {}
    for i, u in enumerate(unity):
        key = (round(u.x * 1000), round(u.y * 1000), round(u.z * 1000))
        if key in at:
            parent[i] = at[key]
        else:
            at[key] = i
    me.calc_loop_triangles()
    for t in me.loop_triangles:
        a, b, c = t.vertices
        ra, rb, rc = find(parent, a), find(parent, b), find(parent, c)
        parent[rb] = ra
        parent[find(parent, rc)] = find(parent, ra)
    lo, hi = {}, {}
    for i in range(n):
        r = find(parent, i)
        p = frame[i]
        if r in lo:
            lo[r] = Vector((min(lo[r].x, p.x), min(lo[r].y, p.y), min(lo[r].z, p.z)))
            hi[r] = Vector((max(hi[r].x, p.x), max(hi[r].y, p.y), max(hi[r].z, p.z)))
        else:
            lo[r] = p.copy()
            hi[r] = p.copy()
    gone = {r for r in lo if inside((lo[r] + hi[r]) * .5, boxes)}
    drop = set()
    alone = 0
    for t in me.loop_triangles:
        if find(parent, t.vertices[0]) in gone:
            drop.add(t.polygon_index)
        elif inside(sum((frame[v] for v in t.vertices), Vector()) / 3., boxes):
            drop.add(t.polygon_index)
            alone += 1
    per_box = [sum(1 for r in gone if inside((lo[r] + hi[r]) * .5, [bx])) for bx in boxes]
    before = len(me.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in drop], context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    me.calc_loop_triangles()
    print("CUT %s sign %+.0f: pieces %d of %d, lone faces %d, polygons dropped %d, triangles %d -> %d, pieces per box %s" % (
        name, SIGN_X, len(gone), len(lo), alone, len(drop), before, len(me.loop_triangles), per_box), flush=True)

bpy.ops.object.select_all(action="DESELECT")
for o in bpy.data.objects:
    o.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["HallV15_Balustrades"]
kinds = {o.type for o in bpy.data.objects} & {"MESH", "EMPTY", "ARMATURE", "LIGHT", "CAMERA", "OTHER"}
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types=kinds, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                         use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="STRIP",
                         embed_textures=False, colors_type="LINEAR")
