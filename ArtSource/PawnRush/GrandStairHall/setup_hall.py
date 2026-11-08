# Builds grand_stair_hall.blend from the Unity export (hall.obj + helpers.csv).
# Axes: Blender X = Unity x, Blender Y = Unity z (north, the way forward), Blender Z = Unity y (up). 1 unit = 1 m.
# Run: blender -b -P setup_hall.py
import bpy, csv, os
from mathutils import Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "unity_export")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=os.path.join(SRC, "hall.obj"))

root = bpy.data.collections.new("Mid_2-1_GrandStairHall")
bpy.context.scene.collection.children.link(root)
groups = {}
for ob in list(bpy.context.scene.collection.objects):
    key = ob.name.split("/")[0]
    col = groups.get(key)
    if col is None:
        col = groups[key] = bpy.data.collections.new(key)
        root.children.link(col)
    bpy.context.scene.collection.objects.unlink(ob)
    col.objects.link(ob)
    ob["unity_path"] = ob.name

# Origins to each piece's own centre so pieces move and rotate in place.
bpy.ops.object.select_all(action="DESELECT")
for ob in bpy.data.objects:
    ob.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects[0]
bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
bpy.ops.object.select_all(action="DESELECT")

for m in bpy.data.materials:
    if m.node_tree:
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                m.diffuse_color = n.inputs["Base Color"].default_value

# Invisible Unity helpers (walking slopes, kill volumes, checkpoints, spans) as wire boxes, hidden.
helpers = bpy.data.collections.new("_Helpers (Unity, invisible)")
root.children.link(helpers)
with open(os.path.join(SRC, "helpers.csv"), newline="") as f:
    for row in csv.DictReader(f):
        bpy.ops.mesh.primitive_cube_add(size=1)
        ob = bpy.context.active_object
        for c in ob.users_collection:
            c.objects.unlink(ob)
        helpers.objects.link(ob)
        ob.name = "H " + row["path"]
        ob.location = (float(row["cx"]), float(row["cz"]), float(row["cy"]))
        ob.scale = (float(row["sx"]), float(row["sz"]), float(row["sy"]))
        q = Quaternion((float(row["rw"]), float(row["rx"]), float(row["ry"]), float(row["rz"])))
        # Unity (x,y,z) -> Blender (x,z,y) is a mirror: axis (x,z,y), angle negated.
        ax, ang = q.to_axis_angle()
        ob.rotation_mode = "AXIS_ANGLE"
        ob.rotation_axis_angle = (-ang, ax.x, ax.z, ax.y)
        ob.display_type = "WIRE"
        ob.hide_render = True
        ob["unity_kind"] = row["kind"]
helpers.hide_viewport = False
bpy.context.view_layer.layer_collection.children[root.name].children[helpers.name].hide_viewport = True

# Markers: section entry / exit (Unity entry (0,0,-12), exit (0,15,158.3)) and the hall hub (0,92).
for name, loc in (("ENTRY (Unity 0,0,-12)", (0, -12, 0)), ("EXIT (Unity 0,15,158.3)", (0, 158.3, 15)), ("HUB (Unity 0,y,92)", (0, 92, 0))):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 3
    e.location = loc
    root.objects.link(e)

sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.rotation_euler = (0.87, 0.0, 0.6)
bpy.context.scene.collection.objects.link(sun)
bpy.context.scene.unit_settings.system = "METRIC"

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall.blend"))
print("objects", len(bpy.data.objects), "collections", [c.name for c in root.children])
