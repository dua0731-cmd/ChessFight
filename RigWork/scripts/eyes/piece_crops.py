# Face crops aligned on the eyes: each image is cut around the midpoint of its two eyes and scaled so the eyes are the
# same distance apart; tiles in rows (';') and columns (','), each item "team@path".
# blender -b --factory-startup -P piece_crops.py -- <out.png> <tile w> <tile h> <px per eye distance> "<rows>"
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
OUT, TW, TH, PPD, spec = a[0], int(a[1]), int(a[2]), float(a[3]), a[4]
def load(p):
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]; bpy.data.images.remove(im)
    al = x[..., 3:4]
    return np.clip(x[..., :3] * al + (1 - al), 0, 1)
def comps(mask, min_px=200):
    h, w = mask.shape; lab = np.zeros((h, w), dtype=np.int32); out = []; n = 1
    for y0, x0 in zip(*np.nonzero(mask)):
        if lab[y0, x0]: continue
        st = [(y0, x0)]; lab[y0, x0] = n; pix = []
        while st:
            y, x = st.pop(); pix.append((y, x))
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = n; st.append((yy, xx))
        n += 1
        if len(pix) >= min_px: out.append(np.array(pix))
    out.sort(key=len, reverse=True); return out
def tile(item):
    team, p = item.split("@", 1)
    x = load(p); h, w = x.shape[:2]
    lum = x @ np.array([0.2126, 0.7152, 0.0722]); sat = x.max(axis=2) - x.min(axis=2)
    if team == "white":
        m = (lum < 0.4) & (sat < 0.2)
    else:
        m = (lum > 0.72) & (x[..., 0] > 0.85) & (x[..., 0] - x[..., 2] > 0.12) & (x[..., 0] - x[..., 2] < 0.35)
    m[int(h * 0.8):] = False
    cs = comps(m)[:2]
    c0, c1 = [(c[:, 1].mean(), (c[:, 0].min() + c[:, 0].max()) / 2) for c in cs]
    mx, my = (c0[0] + c1[0]) / 2, (c0[1] + c1[1]) / 2
    D = abs(c1[0] - c0[0])
    k = D / PPD
    xs = mx + (np.arange(TW) + 0.5 - TW / 2) * k
    ys = my + (np.arange(TH) + 0.5 - TH * 0.42) * k
    X, Y = np.meshgrid(xs, ys)
    x0 = np.clip(np.floor(X).astype(int), 0, w - 2); y0 = np.clip(np.floor(Y).astype(int), 0, h - 2)
    fx = np.clip(X - x0, 0, 1)[..., None]; fy = np.clip(Y - y0, 0, 1)[..., None]
    return (x[y0, x0] * (1 - fx) * (1 - fy) + x[y0, x0 + 1] * fx * (1 - fy) + x[y0 + 1, x0] * (1 - fx) * fy + x[y0 + 1, x0 + 1] * fx * fy)
grey = np.array([0.82, 0.82, 0.82], dtype=np.float32)
rows = []
for r in spec.split(";"):
    ts = [tile(i) for i in r.split(",")]
    parts = []
    for t in ts:
        parts += [t, np.ones((TH, 6, 3), dtype=np.float32) * grey]
    rows.append(np.concatenate(parts[:-1], axis=1))
stack = []
for r in rows:
    stack += [r, np.ones((6, r.shape[1], 3), dtype=np.float32) * grey]
s = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("c", s.shape[1], s.shape[0], alpha=True)
img.pixels = np.dstack([s, np.ones(s.shape[:2], dtype=np.float32)])[::-1].ravel()
img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("CROPS", OUT, s.shape)
