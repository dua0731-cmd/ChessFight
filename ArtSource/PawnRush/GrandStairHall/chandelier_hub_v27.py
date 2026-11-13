# 승규 11-03 "좀 더 크고 밝게 … 흔들리는 애니메이션만 넣어": the hub chandelier (v26's, dressed in gold, crystal, wax and bulbs)
# half as big again - three times v10's size - its hook raised from 33 to 36 so it still hangs about 8 m over the dais,
# and its chain re-hung from the oculus' boss (45.6) down to the new hook. Its bulb markers go with it. The sway is
# Unity's (HallChandelierSway), about the chain's top.
# Run: blender -b grand_stair_hall_v26C.blend -P chandelier_hub_v27.py -> grand_stair_hall_v27C.blend, renders_v34/*.png
import bpy, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
HY, RIG = 92.0, "Chandelier_6"
S, OLD, NEW, CHAIN_TOP = 1.5, 33.0, 36.0, 45.6
sc = bpy.context.scene
old, new = Vector((0, HY, OLD)), Vector((0, HY, NEW))

parts = [o for o in bpy.data.objects if o.name.startswith(RIG + "/Part_")]
for o in parts:
    o.location = new + (o.location - old) * S
    o.scale = o.scale * S
marks = [o for o in bpy.data.objects if o.name.startswith("Hub_chandelier_light_")]
for o in marks:
    o.data.transform(Matrix.Translation(new) @ Matrix.Scale(S, 4) @ Matrix.Translation(-old))
chains = [o for o in bpy.data.objects if o.name.startswith(RIG + "/Chain_")]
col, cme = chains[0].users_collection[0], chains[0].data
for o in chains:
    bpy.data.objects.remove(o)
zs = [v.co.z for v in cme.vertices]
k = 2.0                                                # links a little heavier for the bigger chandelier
pitch = (max(zs) - min(zs)) * .9 * k
z, i = CHAIN_TOP, 0
while z - pitch * .5 > NEW:
    o = bpy.data.objects.new("%s/Chain_%02d" % (RIG, i), cme); col.objects.link(o)
    o.location = (0, HY, z); o.rotation_euler = (0, 0, math.pi / 2 * (i % 2)); o.scale = (k,) * 3
    z -= pitch; i += 1
bpy.context.view_layer.update()
bb = [o.matrix_world @ Vector(c) for o in parts for c in o.bound_box]
lo = [min(v[j] for v in bb) for j in range(3)]; hi = [max(v[j] for v in bb) for j in range(3)]
print("HUB x%.1f: %d parts, %d chain links, %d bulbs; width %.2f m, z %.2f .. %.2f (%.2f m over the dais)"
      % (S * 2, len(parts), i, len(marks), max(hi[0] - lo[0], hi[1] - lo[1]), lo[2], hi[2], lo[2] - 16))

out = os.path.join(HERE, "renders_v34"); os.makedirs(out, exist_ok=True)
for name in ("Cam_dome_up", "Cam_from_balcony", "Cam_chandelier_under"):
    cam = bpy.data.objects.get(name)
    if cam:
        sc.camera = cam
        sc.render.filepath = os.path.join(out, "%s.png" % name[4:])
        bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v27C.blend"))
