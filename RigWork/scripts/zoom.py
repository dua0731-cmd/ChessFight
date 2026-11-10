# Upscaled crops of the same region from several frames: blender -b -P zoom.py -- out.png x0 y0 w h k img1 img2 ...
# (x0, y0 from the image's top-left)
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
out = a[0]; x0, y0, w, h, k = map(int, a[1:6]); paths = a[6:]
tiles = []
for p in paths:
    im = bpy.data.images.load(p); W, H = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4); bpy.data.images.remove(im)
    yb = H - y0 - h
    t = x[yb:yb + h, x0:x0 + w]
    tiles.append(np.repeat(np.repeat(t, k, axis=0), k, axis=1))
sheet = np.concatenate(tiles, axis=1)
img = bpy.data.images.new("z", sheet.shape[1], sheet.shape[0], alpha=True); img.pixels = sheet.ravel()
img.filepath_raw = out; img.file_format = "PNG"; img.save()
