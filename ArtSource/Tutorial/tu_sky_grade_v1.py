# Tutorial sky, grade pass (R834): tu_sky_v1.py's raw panorama (Renders/sky_raw.exr) with its highlights rolled off, so
# in Unity the horizon and the cloud tops keep their blue and their shading instead of going flat white (11-25: the
# first try read washed out near the horizon). Per channel x' = x / (1 + k x) (k = KNEE), the sky's blue lifted a
# little toward the zenith, rolled as tu_sky_v1 rolled it, written over Materials/Textures/TU_SkyClouds.exr.
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tu_sky_grade_v1.py
import bpy, os, math
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
KNEE = .55
SUN_UNITY = np.array([.548, .743, -.384])
img = bpy.data.images.load(os.path.join(HERE, "Renders", "sky_raw.exr"))
w, h = img.size
px = np.array(img.pixels[:], np.float32).reshape(h, w, 4)
rgb = px[..., :3]
print("GRADE before: max %.2f, horizon row mean %s" % (rgb.max(), np.round(rgb[h // 2 + 8].mean(0), 2)))
rgb = rgb / (1. + KNEE * rgb)
v = (np.arange(h) + .5) / h                                # 0 bottom, 1 top
up = np.clip((v - .5) * 2, 0, 1)[:, None, None]
rgb = rgb * (1 - .25 * up * np.array([.6, .3, 0.]))       # deeper blue overhead
rgb *= 1.35                                                # back to the brightness the knee took
lum = (px[..., :3].sum(-1))
r, c = np.unravel_index(np.argmax(lum[h // 2:, :]), (h - h // 2, w))
target = .5 - math.atan2(SUN_UNITY[2], SUN_UNITY[0]) / (2 * math.pi)
shift = int(round((target - (c + .5) / w) * w)) % w
px[..., :3] = rgb
px = np.roll(px, shift, axis=1)
print("GRADE after: max %.2f, horizon row mean %s, rolled %d" % (px[..., :3].max(), np.round(px[h // 2 + 8, :, :3].mean(0), 2), shift))
out = os.path.join(HERE, "Materials", "Textures", "TU_SkyClouds.exr")
o = bpy.data.images.new("TU_SkyClouds", w, h, alpha=False, float_buffer=True)
o.pixels.foreach_set(px.ravel())
sc = bpy.context.scene
sc.render.image_settings.file_format = 'OPEN_EXR'; sc.render.image_settings.color_depth = '16'
o.save_render(out, scene=sc)
p = bpy.data.images.new("prev", w, h, alpha=False)
pp = px.copy(); pp[..., :3] = np.clip(pp[..., :3], 0, 1) ** (1 / 2.2)
p.pixels.foreach_set(pp.ravel()); p.filepath_raw = os.path.join(HERE, "Renders", "sky_preview.png"); p.file_format = 'PNG'; p.save()
