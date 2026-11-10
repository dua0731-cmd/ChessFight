# contact sheet: images in a row, each scaled to H px tall
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
out, H = a[0], int(a[1])
tiles = []
for p in a[2:]:
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    ys = (np.arange(H) * h / H).astype(int); W = int(w * H / h); xs = (np.arange(W) * w / W).astype(int)
    tiles.append(x[ys][:, xs])
row = []
for t in tiles:
    row += [t, np.ones((H, 6, 4), dtype=np.float32)]
s = np.concatenate(row[:-1], axis=1)
img = bpy.data.images.new("c", s.shape[1], s.shape[0], alpha=True); img.pixels = s.ravel()
img.filepath_raw = out; img.file_format = "PNG"; img.save()
