# Eye outlines of two images on top of each other, both scaled to the same head radius and centred on the chosen
# eye's centroid: only design = red, only ours = blue, both = grey; plus left/right and top/bottom half-widths.
# blender -b --factory-startup -P eye_overlay.py -- <out.png> <ppr> <half box R> <design.json:idx:eK> <ours.json:idx:eK> [...pairs]
import bpy, sys, os, json, math
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
OUT, PPR, HB = a[0], float(a[1]), float(a[2])
pairs = list(zip(a[3::2], a[4::2]))
DIRS = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "image_dirs.json")))
def find(js, name):
    for d in [os.path.dirname(js)] + DIRS:
        p = os.path.join(d, name)
        if os.path.exists(p):
            return p
def load(p):
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]; bpy.data.images.remove(im)
    al = x[..., 3:4]
    return np.clip(x[..., :3] * al + (1 - al), 0, 1)
def eye_mask(item):
    js, idx, e = item.rsplit(":", 2)
    m = json.load(open(js))[int(idx)]
    img = load(find(js, m["image"]))
    h, w = img.shape[:2]
    cx, cy, R = m["head"]["cx"], m["head"]["cy"], m["head"]["R"]
    ex, ey = m["eyes"][int(e[1:])]["px"]
    N = int(2 * HB * PPR)
    g = (np.arange(N) + 0.5) / PPR - HB
    X, Y = np.meshgrid(ex + g * R, ey + g * R)
    xi = np.clip(np.round(X).astype(int), 0, w - 1); yi = np.clip(np.round(Y).astype(int), 0, h - 1)
    # bilinear luminance
    x0 = np.clip(np.floor(X).astype(int), 0, w - 2); y0 = np.clip(np.floor(Y).astype(int), 0, h - 2)
    fx, fy = X - x0, Y - y0
    lum = img @ np.array([0.2126, 0.7152, 0.0722])
    sat = img.max(axis=2) - img.min(axis=2)
    L = lum[y0, x0] * (1 - fx) * (1 - fy) + lum[y0, x0 + 1] * fx * (1 - fy) + lum[y0 + 1, x0] * (1 - fx) * fy + lum[y0 + 1, x0 + 1] * fx * fy
    S = sat[yi, xi]
    dark = (L < 0.4) & (S < 0.2)
    white = (L > 0.72) & (S < 0.16)
    # fill: the eye is the dark blob plus the glint inside it (holes)
    rows = []
    filled = np.zeros_like(dark)
    for r in range(N):
        c = np.nonzero(dark[r])[0]
        if len(c):
            filled[r, c.min():c.max() + 1] = True
    return filled, m["image"], R
tiles = []
for d_item, o_item in pairs:
    md, nd, Rd = eye_mask(d_item)
    mo, no, Ro = eye_mask(o_item)
    t = np.ones(md.shape + (3,), dtype=np.float32) * 0.97
    t[md & mo] = 0.6
    t[md & ~mo] = (0.9, 0.2, 0.2)
    t[~md & mo] = (0.2, 0.35, 0.95)
    N = md.shape[0]
    t[N // 2, :] = t[N // 2, :] * 0.7
    t[:, N // 2] = t[:, N // 2] * 0.7
    tiles.append(t)
    for nm, mk in (("design", md), ("ours", mo)):
        ys, xs = np.nonzero(mk)
        cxm, cym = xs.mean(), ys.mean()
        lw = (cxm - xs.min()) / PPR; rw = (xs.max() - cxm) / PPR
        th = (cym - ys.min()) / PPR; bh = (ys.max() - cym) / PPR
        print("%-7s %-34s left %.3f right %.3f (r/l %.3f)  top %.3f bottom %.3f (b/t %.3f)  area %.4f" % (
            nm, (nd if nm == "design" else no), lw, rw, rw / lw, th, bh, bh / th, mk.sum() / PPR ** 2))
    print("  overlap IoU %.3f" % ((md & mo).sum() / max(1, (md | mo).sum())))
sheet = np.concatenate([np.concatenate([t, np.ones((t.shape[0], 6, 3), dtype=np.float32) * 0.8], axis=1) for t in tiles], axis=1)
img = bpy.data.images.new("ov", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = np.dstack([sheet, np.ones(sheet.shape[:2], dtype=np.float32)])[::-1].ravel()
img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("OVERLAY", OUT)
