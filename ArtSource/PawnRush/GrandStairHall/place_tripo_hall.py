# Places the Tripo "royal circular hall" model (source/royal_circular_hall.glb) over the grand stair hall blockout
# at the blockout's size: one uniform scale so its width matches the hall walls (x -36 .. 36, 72 m), its north edge on
# the round wall's outside (Blender Y 125 = Unity z 125: hub 92 + inner radius 31 + wall), its floor at the hall's
# deep floor (Z -5). The scale and place stay on the object (not applied), so they can be nudged by hand.
# Run: blender -b grand_stair_hall_v2.blend -P place_tripo_hall.py   -> grand_stair_hall_v3.blend
import bpy, os
from mathutils import Vector

HERE = os.path.dirname(bpy.data.filepath)
WIDTH, NORTH, FLOOR = 72.0, 125.0, -5.0

before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "royal_circular_hall.glb"))
new = [o for o in bpy.data.objects if o not in before]
mesh = next(o for o in new if o.type == "MESH")

col = bpy.data.collections.new("Tripo royal circular hall")
bpy.data.collections["Mid_2-1_GrandStairHall"].children.link(col)
for o in new:
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o)
mesh.name = "Tripo royal circular hall"

bpy.context.view_layer.update()
pts = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
s = WIDTH / (mx.x - mn.x)
mesh.scale = mesh.scale * s
bpy.context.view_layer.update()
pts = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
mesh.location += Vector((-(mn.x + mx.x) / 2, NORTH - mx.y, FLOOR - mn.z))
bpy.context.view_layer.update()
pts = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
mesh["fit"] = f"scale {s:.2f}, x {mn.x:.1f}..{mx.x:.1f}, y {mn.y:.1f}..{mx.y:.1f}, z {mn.z:.1f}..{mx.z:.1f}"
print("PLACED", mesh["fit"])

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v3.blend"))
print("SAVED")
