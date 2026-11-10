# Head crops at one scale: each image is cut around its measured head circle (face_measure.py json) and resampled so
# the head radius is the same number of pixels; rows of images, gaps between, white background.
# blender -b --factory-startup -P face_crops.py -- <out.png> <px per R> <x0 x1 y0 y1 (in R)> <row> [<row> ...]
#   row = "measure.json:index,measure.json:index,..."   (image paths are taken from the json's directory or a dir map)
import bpy, sys, os, json, math
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
OUT, PPR = a[0], float(a[1])
X0, X1, Y0, Y1 = map(float, a[2:6])
rows = a[6:]
SEARCH = [os.path.dirname(OUT)]
cache = {}
def load(p):
    if p not in cache:
        im = bpy.data.images.load(p); w, h = im.size
        x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]; bpy.data.images.remove(im)
        al = x[..., 3:4]
        cache[p] = np.clip(x[..., :3] * al + (1 - al), 0, 1)
    return cache[p]
DIRS = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "image_dirs.json")))
def find(js, name):
    for d in [os.path.dirname(js)] + DIRS:
        p = os.path.join(d, name)
        if os.path.exists(p):
            return p
    raise FileNotFoundError(name)
def crop(js, idx, eye=None, off=(0.0, 0.0)):
    """Around the head's centre, or (eye = k) around the k-th eye's centre (eyes sorted left to right), shifted by
    off (head radii)."""
    m = json.load(open(js))[idx]
    img = load(find(js, m["image"]))
    h, w = img.shape[:2]
    cx, cy, R = m["head"]["cx"], m["head"]["cy"], m["head"]["R"]
    if eye is not None:
        ex, ey = m["eyes"][eye]["centre"]
        cx, cy = cx + ex * R, cy + ey * R
    cx, cy = cx + off[0] * R, cy + off[1] * R
    W = int(round((X1 - X0) * PPR)); H = int(round((Y1 - Y0) * PPR))
    xs = cx + (X0 + (np.arange(W) + 0.5) / PPR) * R - 0.5
    ys = cy + (Y0 + (np.arange(H) + 0.5) / PPR) * R - 0.5
    X, Y = np.meshgrid(xs, ys)
    x0 = np.clip(np.floor(X).astype(int), 0, w - 2); y0 = np.clip(np.floor(Y).astype(int), 0, h - 2)
    fx = np.clip(X - x0, 0, 1)[..., None]; fy = np.clip(Y - y0, 0, 1)[..., None]
    out = (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) +
           img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)
    inside = (X >= 0) & (X < w - 1) & (Y >= 0) & (Y < h - 1)
    out[~inside] = 1.0
    return out
grey = np.array([0.82, 0.82, 0.82], dtype=np.float32)
row_imgs = []
for r in rows:
    tiles = []
    for item in r.split(","):
        eye = None
        off = (0.0, 0.0)
        if "@" in item:
            item, o = item.split("@")
            off = tuple(float(v) for v in o.split("_"))
        if item.rsplit(":", 1)[1].startswith("e"):
            item, e = item.rsplit(":", 1)
            eye = int(e[1:])
        js, idx = item.rsplit(":", 1)
        tiles.append(crop(js, int(idx), eye, off))
    parts = []
    for t in tiles:
        parts += [t, np.ones((t.shape[0], 6, 3), dtype=np.float32) * grey]
    row_imgs.append(np.concatenate(parts[:-1], axis=1))
wmax = max(r.shape[1] for r in row_imgs)
stack = []
for r in row_imgs:
    if r.shape[1] < wmax:
        r = np.concatenate([r, np.ones((r.shape[0], wmax - r.shape[1], 3), dtype=np.float32)], axis=1)
    stack += [r, np.ones((6, wmax, 3), dtype=np.float32) * grey]
sheet = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("crops", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = np.dstack([sheet, np.ones(sheet.shape[:2], dtype=np.float32)])[::-1].ravel()
img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("CROPS", OUT, sheet.shape)
