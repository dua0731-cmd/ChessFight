# R502: a piece of the drawbridge's rope (user 11-07: "로프를 다시 만들어"): three twisted strands, 1 unit long along
# Blender y, 0.15 thick at 1 unit (Unity sets the size). The strands turn 120 degrees over the piece, so pieces laid end
# to end join seamlessly (the three-fold twist repeats). MiniGameDrawbridgeRope lays these along the rope's run.
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_rope_segment.py
import os, math
import bpy, bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "C_RopeSegment.fbx")
STRAND_R, LAY_R, ROWS, TUBE = .042, .033, 24, 10

bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()
for k in range(3):
    rows = []
    for i in range(ROWS + 1):
        y = i / ROWS
        a = 2 * math.pi * k / 3 + (2 * math.pi / 3) * y
        c = Vector((LAY_R * math.cos(a), y, LAY_R * math.sin(a)))
        t = Vector((-LAY_R * math.sin(a) * 2 * math.pi / 3, 1, LAY_R * math.cos(a) * 2 * math.pi / 3)).normalized()
        n1 = Vector((math.cos(a), 0, math.sin(a)))
        n1 = (n1 - t * n1.dot(t)).normalized()
        n2 = t.cross(n1)
        rows.append([bm.verts.new(c + (n1 * math.cos(2 * math.pi * j / TUBE) + n2 * math.sin(2 * math.pi * j / TUBE)) * STRAND_R)
                     for j in range(TUBE)])
    for i in range(ROWS):
        for j in range(TUBE):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % TUBE], rows[i + 1][(j + 1) % TUBE], rows[i + 1][j]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("C_RopeSegment")
bm.to_mesh(me)
bm.free()
me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
mat = bpy.data.materials.new("PR_Early_C_Rope")
me.materials.append(mat)
ob = bpy.data.objects.new("C_RopeSegment", me)
bpy.context.scene.collection.objects.link(ob)
ob.select_set(True)
bpy.context.view_layer.objects.active = ob
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                         mesh_smooth_type="OFF", add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
me.calc_loop_triangles()
print("ROPE segment triangles", len(me.loop_triangles), "->", OUT)
