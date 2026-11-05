# 승규 11-03 on R336: "전으로 되돌리고 깃발은 여전히 지워". Back to R335's gold (walls_v29.py: the height bands, the level
# faces, the faces looking along the wall, the window bars) from grand_stair_hall_v29C.blend, without its banners.
# walls_v30.py and its v30C stay on disk for the moulding-line way, unused.
# Run: blender -b grand_stair_hall_v29C.blend -P walls_v31.py -> grand_stair_hall_v31C.blend
import bpy, os

HERE = os.path.dirname(os.path.abspath(__file__))
bc = bpy.data.collections.get("Palace_banners_v29")
gone = 0
if bc:
    for o in list(bc.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o); gone += 1
print("BANNERS removed", gone)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v31C.blend"))
