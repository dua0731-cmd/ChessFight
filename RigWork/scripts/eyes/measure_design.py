# The pawn design images: the head's circle (fitted to its outline above the collar), the eyes (dark), their glints
# (white inside them) and the blush (pink), all relative to the head's centre and radius.
import bpy, sys, math
import numpy as np
for p in sys.argv[sys.argv.index("--") + 1:]:
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]   # rows from the top
    lum = x @ np.array([0.2126, 0.7152, 0.0722])
    fg = lum < 0.93                                                          # not the white page
    # the collar: the first row from the top where the figure suddenly widens below the head
    widths = []
    for r in range(h):
        cols = np.nonzero(fg[r])[0]
        widths.append((cols.min(), cols.max()) if len(cols) else None)
    top = next(r for r in range(h) if widths[r])
    # head rows: from the top down to where the width stops shrinking after the widest (the neck/collar)
    ws = [(widths[r][1] - widths[r][0]) if widths[r] else 0 for r in range(h)]
    widest = top + int(np.argmax(ws[top:top + h // 2]))
    neck = widest + int(np.argmin(ws[widest:widest + h // 5]))
    pts = []
    for r in range(top, neck - 4):
        if widths[r]:
            pts.append((widths[r][0], r)); pts.append((widths[r][1], r))
    P = np.array(pts, dtype=np.float64)
    A = np.c_[2 * P[:, 0], 2 * P[:, 1], np.ones(len(P))]
    b = (P ** 2).sum(axis=1)
    cx, cy, c = np.linalg.lstsq(A, b, rcond=None)[0]
    R = math.sqrt(c + cx * cx + cy * cy)
    print("IMG %s  %dx%d  head centre (%.1f, %.1f) R %.1f  top %d neck %d" % (p.split("\\")[-1], w, h, cx, cy, R, top, neck))
    yy, xx = np.mgrid[0:h, 0:w]
    inhead = (xx - cx) ** 2 + (yy - cy) ** 2 < (R * 0.98) ** 2
    dark = (lum < 0.18) & inhead
    pink = (x[..., 0] - x[..., 1] > 0.24) & (x[..., 1] < 0.72) & (x[..., 0] > 0.75) & inhead
    for name, mask in (("eye", dark), ("blush", pink)):
        for side, sel in (("L", xx < cx), ("R", xx >= cx)):
            m = mask & sel
            n = m.sum()
            if n < 30:
                continue
            ys, xs = np.nonzero(m)
            bx0, bx1, by0, by1 = xs.min(), xs.max(), ys.min(), ys.max()
            mx, my = xs.mean(), ys.mean()
            line = "  %s %s centre (%+.3f R, %+.3f R)  size %.3f R x %.3f R  aspect %.2f" % (
                name, side, (mx - cx) / R, (my - cy) / R, (bx1 - bx0 + 1) / R, (by1 - by0 + 1) / R, (by1 - by0 + 1) / (bx1 - bx0 + 1))
            if name == "eye":
                wm = (lum > 0.85) & (xx >= bx0) & (xx <= bx1) & (yy >= by0) & (yy <= by1)
                if wm.sum() > 5:
                    gy, gx = np.nonzero(wm)
                    line += "  glint at (%.2f, %.2f) of the box, diameter %.2f of the eye width" % (
                        (gx.mean() - bx0) / (bx1 - bx0 + 1), (gy.mean() - by0) / (by1 - by0 + 1), 2 * math.sqrt(wm.sum() / math.pi) / (bx1 - bx0 + 1))
            print(line)
    bpy.data.images.remove(im)
