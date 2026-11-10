# Eye shapes on any piece's front design image: per eye its box, area ratio (area / box), the corner radius a
# rounded rectangle of that box and area would have, the glint / dot, and the body's width at the eye's row.
# blender -b --factory-startup -P measure_piece_eyes.py -- <team white|black> img ...
import bpy, sys, math
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
TEAM, paths = a[0], a[1:]
exec(open(__file__.replace("measure_piece_eyes.py", "face_measure_lib.py")).read()) if False else None
def load(p):
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]; bpy.data.images.remove(im)
    al = x[..., 3:4]
    return np.clip(x[..., :3] * al + (1 - al), 0, 1)
def components(mask, min_px=40):
    h, w = mask.shape
    lab = np.zeros((h, w), dtype=np.int32); out = []; nxt = 1
    for y0, x0 in zip(*np.nonzero(mask)):
        if lab[y0, x0]: continue
        st = [(y0, x0)]; lab[y0, x0] = nxt; pix = []
        while st:
            y, x = st.pop(); pix.append((y, x))
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = nxt; st.append((yy, xx))
        nxt += 1
        if len(pix) >= min_px: out.append(np.array(pix))
    out.sort(key=len, reverse=True)
    return out
for p in paths:
    x = load(p); h, w = x.shape[:2]
    lum = x @ np.array([0.2126, 0.7152, 0.0722]); sat = x.max(axis=2) - x.min(axis=2)
    fg = lum < 0.93
    if TEAM == "white":
        eye = (lum < 0.4) & (sat < 0.2)
    else:
        eye = (lum > 0.72) & (x[..., 0] > 0.85) & (x[..., 0] - x[..., 2] > 0.12) & (x[..., 0] - x[..., 2] < 0.35)
    eye[int(h * 0.8):] = False
    comps = [c for c in components(eye) if len(c) > 300][:2]
    comps.sort(key=lambda c: c[:, 1].mean())
    rows = np.nonzero(fg.any(axis=1))[0]
    ftop, fbot = rows.min(), rows.max()
    # the lowest rows can be the floor shadow: the figure's bottom is the last row at least 30% as wide as the widest
    widths = np.array([(np.ptp(np.nonzero(fg[r])[0]) + 1) if fg[r].any() else 0 for r in range(h)])
    fbot = max(r for r in range(h) if widths[r] > 0.3 * widths.max())
    print("IMG", p.split("/")[-1], "figure rows %d..%d" % (ftop, fbot))
    if TEAM == "white":
        pink = (x[..., 0] - x[..., 1] > 0.22) & (x[..., 0] > 0.75)
    else:
        pink = (x[..., 0] - x[..., 1] > 0.2) & (x[..., 0] > 0.55) & (lum < 0.62)
    pink[int(h * 0.8):] = False
    for c in sorted([c for c in components(pink) if len(c) > 300][:2], key=lambda c: c[:, 1].mean()):
        y0, x0 = c.min(axis=0); y1, x1 = c.max(axis=0)
        row = int(round((y0 + y1) / 2)); cols = np.nonzero(fg[row])[0]
        bw_ = cols.max() - cols.min() + 1; bc = (cols.max() + cols.min()) / 2
        print("  blush box %dx%d px centre (%.1f, %.1f): x %.3f of half-width, w %.3f h %.3f of body w at its row, height %.3f of the figure from the bottom" % (
            x1 - x0 + 1, y1 - y0 + 1, (x0 + x1) / 2, (y0 + y1) / 2, ((x0 + x1) / 2 - bc) / (bw_ / 2), (x1 - x0 + 1) / bw_, (y1 - y0 + 1) / bw_,
            (fbot - (y0 + y1) / 2) / (fbot - ftop)))
    for c in comps:
        y0, x0 = c.min(axis=0); y1, x1 = c.max(axis=0)
        bw, bh = x1 - x0 + 1, y1 - y0 + 1
        # fill holes row by row (the glint)
        filled = 0
        for r in range(y0, y1 + 1):
            cols = c[c[:, 0] == r][:, 1]
            if len(cols): filled += cols.max() - cols.min() + 1
        ratio = filled / (bw * bh)
        rr = math.sqrt(max(0.0, (bw * bh - filled) / (4 - math.pi)))
        cy, cx = c[:, 0].mean(), c[:, 1].mean()
        row = int(round((y0 + y1) / 2))
        cols = np.nonzero(fg[row])[0]
        body_w = cols.max() - cols.min() + 1; body_c = (cols.max() + cols.min()) / 2
        s = "  eye box %dx%d px (h/w %.3f) centre (%.1f, %.1f) area/box %.3f  rounded-rect corner %.3f W" % (
            bw, bh, bh / bw, (x0 + x1) / 2, (y0 + y1) / 2, ratio, rr / bw)
        s += " | body width at its row %d px, eye centre %.3f of half-width from the middle, eye w %.3f of body w, height %.3f of the figure" % (
            body_w, ((x0 + x1) / 2 - body_c) / (body_w / 2), bw / body_w, (fbot - (y0 + y1) / 2) / (fbot - ftop))
        if TEAM == "white":
            gl = (lum > 0.72) & (sat < 0.16)
            gl[:y0] = False; gl[y1 + 1:] = False; gl[:, :x0] = False; gl[:, x1 + 1:] = False
            g = components(gl, 4)
            # the glint is the bright piece whose box lies inside the eye's box
            g = [q for q in g if q[:, 1].min() > x0 + 2 and q[:, 1].max() < x1 - 2 and q[:, 0].min() > y0 + 2 and q[:, 0].max() < y1 - 2]
            if g:
                q = g[0]
                gy, gx = q[:, 0].mean(), q[:, 1].mean()
                s += "\n    glint at (%.3f, %.3f) of the box from its top-left, i.e. u %+.3f W, v %+.3f H from the centre, diameter %.3f W" % (
                    (gx - x0) / bw, (gy - y0) / bh, (gx - (x0 + x1) / 2) / bw, ((y0 + y1) / 2 - gy) / bh, 2 * math.sqrt(len(q) / math.pi) / bw)
        # corner radius from the outline: the half-width profile near the top and bottom against a rounded rectangle
        prof = []
        for r in range(y0, y1 + 1):
            cols = c[c[:, 0] == r][:, 1]
            if len(cols): prof.append((r - y0 + 0.5, cols.max() - cols.min() + 1))
        prof = np.array(prof, dtype=np.float64)
        best = None
        for rad in np.linspace(0.05, 0.5, 46) * bw:
            yy_ = np.minimum(prof[:, 0], bh - prof[:, 0])
            k = np.clip(rad - yy_, 0, rad)
            pred = bw - 2 * (rad - np.sqrt(np.maximum(rad * rad - k * k, 0)))
            err = np.mean((pred - prof[:, 1]) ** 2)
            if best is None or err < best[0]: best = (err, rad)
        s += "\n    outline fit: rounded-rect corner %.3f W (rms %.2f px)" % (best[1] / bw, math.sqrt(best[0]))
        print(s)
