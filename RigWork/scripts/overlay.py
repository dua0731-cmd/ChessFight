# Silhouette overlay of two renders on the same camera: both = grey, only the first = red, only the second = blue.
# blender -b -P overlay.py -- out.png a1.png b1.png [a2.png b2.png ...]
import bpy, sys
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
out, pairs = a[0], list(zip(a[1::2], a[2::2]))

def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    return x

def mask(x):
    bg = x[2, 2, :3]
    return np.abs(x[..., :3] - bg).sum(axis=2) > 0.04

tiles = []
for pa, pb in pairs:
    A, B = load(pa), load(pb)
    ma, mb = mask(A), mask(B)
    t = np.ones(A.shape, dtype=np.float32)
    t[..., :3] = 0.97
    t[ma & mb, :3] = 0.62
    t[ma & ~mb, :3] = (0.90, 0.15, 0.15)
    t[~ma & mb, :3] = (0.15, 0.35, 0.95)
    tiles.append(t)
    print("pixels both %d, only old %d, only new %d, match %.1f%%" % ((ma & mb).sum(), (ma & ~mb).sum(), (~ma & mb).sum(),
                                                                    100.0 * (ma & mb).sum() / max(1, (ma | mb).sum())))
sheet = np.concatenate(tiles, axis=1)
h, w = sheet.shape[:2]
img = bpy.data.images.new("o", w, h, alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = out
img.file_format = "PNG"
img.save()
