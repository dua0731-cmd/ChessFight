# The foyer (the start) without its six columns: four giant chess statues instead, two a side, facing the
# way in from the porch to the grand stair - bishop and queen on the west, knight and king on the east
# (king and queen nearest the stair). Each about 11 m on its own pedestal, the foyer walls being 12 m.
# Sources: source/statue_*.glb (one fused mesh each, ~320k triangles, no UV/texture), decimated to a
# quarter. No paint: v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v12.blend -P statues_v13.py   -> grand_stair_hall_v13.blend
import bpy, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
HEIGHT = 11.0
FRONT, BACK, SIDE_X = 3.6, 9.9, 14.0
# (source, x, y, yaw so it faces the aisle). The king, queen and bishop face -Y as modelled, the knight -X.
PLACES = [("statue_bishop", -SIDE_X, FRONT, 90), ("statue_queen", -SIDE_X, BACK, 90),
          ("statue_knight", SIDE_X, FRONT, 0), ("statue_king", SIDE_X, BACK, -90)]

sc = bpy.context.scene
CLAY = bpy.data.materials["Arch_Clay"]
col = bpy.data.collections.new("Foyer_statues_v13"); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]

cols = bpy.data.objects.get("Foyer columns")
if cols:
    for c in list(cols.users_collection):
        c.objects.unlink(cols)
    originals.objects.link(cols)

for src, x, y, yaw in PLACES:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", src + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    mesh = [o for o in new if o.type == "MESH"][0]
    me = mesh.data.copy(); me.transform(mesh.matrix_world)
    for o in new:
        bpy.data.objects.remove(o)
    vs = [v.co for v in me.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    s = HEIGHT / (hi.z - lo.z)
    me.transform(Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Scale(s, 4)
                 @ Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)))
    me.materials.clear(); me.materials.append(CLAY)
    name = "Foyer_statue_" + src.split("_")[1].capitalize()
    me.name = name
    ob = bpy.data.objects.new(name, me); ob.location = (x, y, 0); col.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new("d", "DECIMATE"); mod.ratio = 0.25
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    print("STATUE", name, "tris", sum(len(p.vertices) - 2 for p in me.polygons),
          [round(min(v[i] for v in bb), 2) for i in range(3)], [round(max(v[i] for v in bb), 2) for i in range(3)])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v13.blend"))
