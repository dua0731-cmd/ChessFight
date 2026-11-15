# The same crop of several images side by side, numbered. blender -b --factory-startup -P crop_row.py --
#   <out.png> <x0>,<y0>,<x1>,<y1> <scale percent> <image> ...
import bpy, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
x0, y0, x1, y1 = [int(v) for v in a[1].split(",")]
sc = int(a[2]) / 100.0
tiles = []
for i, p in enumerate(a[3:]):
    c = bi.on_white(bi.load(p))[y0:y1, x0:x1]
    c = bi.resize(c, int((x1 - x0) * sc), int((y1 - y0) * sc))
    bi.text(c, i + 1, 4, 4, 3)
    tiles += [c, np.full((c.shape[0], 4, 4), 0.6, dtype=np.float32)]
bi.save(OUT, np.concatenate(tiles[:-1], axis=1))
print("ROW", OUT)
