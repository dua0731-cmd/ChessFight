# Stack images top to bottom (same width): blender -b -P stack.py -- out.png top.png ... bottom.png
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
ims = []
for p in a[1:]:
    im = bpy.data.images.load(p); w, h = im.size
    ims.append(np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)); bpy.data.images.remove(im)
gap = np.ones((6, ims[0].shape[1], 4), dtype=np.float32)
parts = []
for x in reversed(ims):
    parts += [x, gap]
sheet = np.concatenate(parts[:-1], axis=0)
img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True); img.pixels = sheet.ravel()
img.filepath_raw = a[0]; img.file_format = "PNG"; img.save()
