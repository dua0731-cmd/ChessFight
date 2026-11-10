# Images in a grid (rows separated by ';', columns by ','), each scaled by k (box filter for k < 1), on white.
# blender -b --factory-startup -P grid_sheet.py -- <out.png> <k> "<a.png,b.png;c.png,d.png>" [x0 y0 w h crop in px]
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
OUT, K, spec = a[0], float(a[1]), a[2]
crop = list(map(int, a[3:7])) if len(a) >= 7 else None
def load(p):
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]; bpy.data.images.remove(im)
    al = x[..., 3:4]
    return np.clip(x[..., :3] * al + (1 - al), 0, 1)
def scale(x, k):
    if crop:
        x0, y0, w, h = crop
        x = x[y0:y0 + h, x0:x0 + w]
    if k == 1:
        return x
    if k < 1:
        n = int(round(1 / k))
        h, w = x.shape[0] // n * n, x.shape[1] // n * n
        return x[:h, :w].reshape(h // n, n, w // n, n, 3).mean(axis=(1, 3))
    n = int(round(k))
    return np.repeat(np.repeat(x, n, axis=0), n, axis=1)
grey = np.array([0.8, 0.8, 0.8], dtype=np.float32)
rows = []
for r in spec.split(";"):
    tiles = [scale(load(p), K) for p in r.split(",")]
    parts = []
    for t in tiles:
        parts += [t, np.ones((t.shape[0], 6, 3), dtype=np.float32) * grey]
    rows.append(np.concatenate(parts[:-1], axis=1))
wmax = max(r.shape[1] for r in rows)
stack = []
for r in rows:
    if r.shape[1] < wmax:
        r = np.concatenate([r, np.ones((r.shape[0], wmax - r.shape[1], 3), dtype=np.float32)], axis=1)
    stack += [r, np.ones((6, wmax, 3), dtype=np.float32) * grey]
s = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("g", s.shape[1], s.shape[0], alpha=True)
img.pixels = np.dstack([s, np.ones(s.shape[:2], dtype=np.float32)])[::-1].ravel()
img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("GRID", OUT, s.shape)
