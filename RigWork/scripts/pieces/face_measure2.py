# Measures the face on a design image that shows it (the bishop's sheet, the king's and queen's fronts): the dark
# eye blobs, the white glint inside each, the pink blush, with the body's top, bottom and half width at the eyes.
# blender -b --factory-startup -P face_measure2.py -- <image> <x0>,<y0>,<x1>,<y1> (the figure) <y0>:<y1> (rows to
#   look for the face in) [--white] (a white body: the page is the only white)
import bpy, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
x = bi.load(a[0])
X0, Y0, X1, Y1 = [int(v) for v in a[1].split(",")]
FY0, FY1 = [int(v) for v in a[2].split(":")]
WHITE = "--white" in a
x = x[Y0:Y1, X0:X1]
rgb = x[..., :3]
lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
chroma = rgb.max(axis=2) - rgb.min(axis=2)
if WHITE:
    body = (lum < 0.972) & (lum > 0.3)
else:
    body = (rgb[..., 0] - rgb[..., 2] > 0.05) & (lum > 0.3)
dark = lum < 0.22
pink = (rgb[..., 0] > 0.75) & (rgb[..., 0] - rgb[..., 1] > 0.18) & (rgb[..., 0] - rgb[..., 2] > 0.18)
rows = np.nonzero(body.any(axis=1))[0]
top = rows.min()
wid = np.array([(np.nonzero(body[r])[0].max() - np.nonzero(body[r])[0].min()) if body[r].any() else 0 for r in range(body.shape[0])])
# the axis: the middle of the body in the rows of the face
mids = [(np.nonzero(body[r])[0].max() + np.nonzero(body[r])[0].min()) / 2 for r in range(FY0 - Y0, FY1 - Y0) if body[r].any()]
axis = float(np.median(mids))
print("FIGURE top row %d axis %.1f (crop coordinates; add %d, %d)" % (top + Y0, axis + X0, X0, Y0))

def blobs(mask, y0, y1, min_px=30):
    """Connected blobs (4-neighbour flood) of mask within rows y0..y1."""
    m = mask.copy()
    m[:y0] = False; m[y1:] = False
    out = []
    seen = np.zeros_like(m)
    ys, xs = np.nonzero(m)
    for y, x_ in zip(ys, xs):
        if seen[y, x_]:
            continue
        stack = [(y, x_)]
        seen[y, x_] = True
        pts = []
        while stack:
            cy, cx = stack.pop()
            pts.append((cy, cx))
            for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                if 0 <= ny < m.shape[0] and 0 <= nx < m.shape[1] and m[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(pts) >= min_px:
            p = np.array(pts)
            out.append(dict(n=len(pts), y0=int(p[:, 0].min()), y1=int(p[:, 0].max()), x0=int(p[:, 1].min()), x1=int(p[:, 1].max())))
    return out

def show(kind, b):
    cx, cy = (b["x0"] + b["x1"]) / 2, (b["y0"] + b["y1"]) / 2
    w, h = b["x1"] - b["x0"] + 1, b["y1"] - b["y0"] + 1
    r = int(round(cy))
    cols = np.nonzero(body[r] | dark[r] | pink[r])[0]
    half = (cols.max() - cols.min() + 1) / 2
    print("%s centre x %+.1f (of half width %.1f = %.3f) row %.1f, w %d h %d (h/w %.2f, w/body width %.4f), %d px" % (
        kind, cx - axis, half, abs(cx - axis) / half, cy + Y0, w, h, h / w, w / (2 * half), b["n"]))
    return cx, cy, w, h

for b in sorted(blobs(dark, FY0 - Y0, FY1 - Y0), key=lambda q: q["x0"]):
    cx, cy, w, h = show("EYE", b)
    # the glint: bright pixels inside the eye's box
    sub = lum[b["y0"]:b["y1"] + 1, b["x0"]:b["x1"] + 1] > 0.75
    hull = dark[b["y0"]:b["y1"] + 1, b["x0"]:b["x1"] + 1]
    # only bright pixels with dark on both sides in their row
    inside = np.zeros_like(sub)
    for r in range(sub.shape[0]):
        d = np.nonzero(hull[r])[0]
        if len(d) > 1:
            inside[r, d.min():d.max()] = sub[r, d.min():d.max()]
    if inside.sum() > 4:
        ys, xs = np.nonzero(inside)
        print("   GLINT centre (%+.3f, %+.3f) eye widths from the eye centre (x + = right, y + = up), w %d h %d px, %d px" % (
            (b["x0"] + xs.mean() - cx) / w, (cy - (b["y0"] + ys.mean())) / w, xs.max() - xs.min() + 1, ys.max() - ys.min() + 1, int(inside.sum())))
for b in sorted(blobs(pink, FY0 - Y0, FY1 - Y0 + 60), key=lambda q: q["x0"]):
    show("BLUSH", b)
