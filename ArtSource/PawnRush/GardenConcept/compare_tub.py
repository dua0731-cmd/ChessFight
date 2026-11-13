# Renders one B3 tub (PlatformsSample_v3.blend, P2 alone) at the reference render's angle and puts the two side by side
# (R430): compare_tub_v3.png. The reference path is the chat attachment it was checked against.
import bpy, os, numpy as np
from mathutils import Vector
HERE = r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\GardenConcept"
bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "PlatformsSample_v3.blend"))
sc = bpy.context.scene
# Only P2 and its world: hide the other tubs, the shortcut and the landing so the tub stands alone as in the reference.
for ob in bpy.data.objects:
    if ob.type == "MESH" and ob.name not in ("Bobbing platform P2", "Context water"):
        ob.hide_render = True
cam = sc.camera
target = Vector((-15.0, 6.5, 1.2))
cam.location = Vector((-15.0 + 9.5 * 0.42, 6.5 - 9.5 * 0.9, 2.6 + 5.6))
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 50
sc.render.resolution_x, sc.render.resolution_y = 640, 554
sc.render.filepath = os.path.join(HERE, "render_platforms_v3_single.png")
bpy.ops.render.render(write_still=True)
# Side by side with the reference (scaled to the same height).
ref = bpy.data.images.load(r"C:/Users/Makebot2/AppData/Local/Temp/claude/d958ff63a7cd4535/d3af50c5-d455-49a8-ae8f-063fb75ce0e8/images/8.png")
rw, rh = ref.size
rp = np.array(ref.pixels[:], dtype=np.float32).reshape(rh, rw, 4)
mine = bpy.data.images.load(sc.render.filepath)
mw, mh = mine.size
mp = np.array(mine.pixels[:], dtype=np.float32).reshape(mh, mw, 4)
yi = (np.arange(mh) * rh / mh).astype(int)
xi = (np.arange(int(rw * mh / rh)) * rh / mh).astype(int).clip(0, rw - 1)
rs = rp[yi][:, xi]
sheet = np.concatenate([rs, np.ones((mh, 12, 4), np.float32), mp], axis=1)
out = bpy.data.images.new("cmp", sheet.shape[1], sheet.shape[0], alpha=True)
out.pixels.foreach_set(sheet.ravel())
out.filepath_raw = os.path.join(HERE, "compare_tub_v3.png")
out.file_format = "PNG"
out.save()
print("COMPARE DONE")
