# 승규 11-03 "여기 튀어나온 부분 지워주고 유니티에 넣어" (the lantern over the oculus, "랜턴 전체"): v23C's lantern - eight
# columns, ring, cap, ball and cross - gone, so the star vault ends in the glass oculus in its crown ring (top 48.2).
# Run: blender -b grand_stair_hall_v23C.blend -P round_hall_v24.py -> grand_stair_hall_v24C.blend, renders_v31/*.png
#      then: blender -b grand_stair_hall_v24C.blend -P export_unity_v15.py
import bpy, os
HERE = os.path.dirname(os.path.abspath(__file__))
sc = bpy.context.scene
gone = [o for o in bpy.data.objects if o.name.startswith("Lantern_")]
for o in gone:
    bpy.data.objects.remove(o)
dome = next(c for c in bpy.data.collections if c.name.startswith("Dome_1"))
top = max((o.matrix_world @ v.co).z for o in dome.objects if o.type == "MESH" and not o.name.startswith("Chandelier")
          for v in o.data.vertices)
print("LANTERN removed", len(gone), "objects; the dome's top now", round(top, 2))
out = os.path.join(HERE, "renders_v31"); os.makedirs(out, exist_ok=True)
for name in ("Cam_overview", "Cam_low", "Cam_from_balcony", "Cam_dome_up"):
    sc.camera = bpy.data.objects[name]
    sc.render.filepath = os.path.join(out, "%s.png" % name[4:])
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects["Cam_overview"]
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v24C.blend"))
