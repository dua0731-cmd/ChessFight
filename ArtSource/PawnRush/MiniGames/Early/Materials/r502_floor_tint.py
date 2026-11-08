# R502: the floor's own colour (user, 11-07, a Unity screenshot of the stone: "바닥 색을 이 색으로 맞추고").
# The stone block's baked base colour is recoloured, per channel in linear light, so its stone median becomes the
# screenshot's stone median (sRGB 0.929, 0.741, 0.533); the walls keep the module's own colour.
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r502_floor_tint.py
# Writes Textures/PR_Early_C_Floor_BaseColor.png and _Normal.png (the module's normal map, unchanged).
import bpy, os, shutil
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Textures")
TARGET = np.array([.929, .741, .533])


def lin(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= .0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - .055)


im = bpy.data.images.load(os.path.join(TEX, "PR_Early_StoneBlock_BaseColor.png"))
w, h = im.size
a = np.empty(w * h * 4, np.float32)
im.pixels.foreach_get(a)
a = a.reshape(h, w, 4)
rgb = a[..., :3]
mx = rgb.max(-1)
s = (mx - rgb.min(-1)) / np.maximum(mx, 1e-4)
stone = (mx > .55) & (s > .1) & (s < .6) & (rgb[..., 0] > rgb[..., 2])
med = np.median(rgb[stone], 0)
gain = lin(TARGET) / lin(med)
print("FLOOR stone median", np.round(med, 3), "-> gain (linear)", np.round(gain, 3))
a[..., :3] = srgb(lin(rgb) * gain)
out = bpy.data.images.new("floor", w, h, alpha=False)
out.pixels.foreach_set(a.ravel())
out.filepath_raw = os.path.join(TEX, "PR_Early_C_Floor_BaseColor.png")
out.file_format = "PNG"
out.save()
shutil.copyfile(os.path.join(TEX, "PR_Early_StoneBlock_Normal.png"), os.path.join(TEX, "PR_Early_C_Floor_Normal.png"))
print("wrote PR_Early_C_Floor_BaseColor.png")
