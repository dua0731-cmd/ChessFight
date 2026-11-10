# The eye's outline shape and where it sits on the visible head, for design images and our renders alike.
# Shape: half-width profile w(y)/a against t = |y - yc| / b (rows) and half-height h(x)/b against s = |x - xc| / a
# (columns), sub-pixel from the 50% crossing of the eye's darkness; fitted to a superellipse |x/a|^p + |y/b|^q = 1
# (p sets how flat the top and bottom are, q how flat the left and right sides are; an ellipse is 2, 2), upper and
# lower halves separately. Placement: the eye's box against the head's top row and its junction with the collar.
# blender -b --factory-startup -P eye_profile.py -- <measure.json>:<index> ...
import bpy, sys, os, json, math
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
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
def crossings(v):
    """Sub-pixel first and last index where v (darkness 0..1) crosses 0.5."""
    idx = np.nonzero(v >= 0.5)[0]
    if len(idx) == 0:
        return None
    i0, i1 = idx[0], idx[-1]
    def interp(i, j):
        if j < 0 or j >= len(v):
            return float(i)
        return i + (0.5 - v[i]) / (v[j] - v[i]) * (j - i) if v[j] != v[i] else float(i)
    return interp(i0, i0 - 1), interp(i1, i1 + 1)
def fit(ts, rs):
    """Best (p, q) for r = (1 - t^q)^(1/p) over a grid (rows: t = |y|/b, r = w/a)."""
    best = None
    for p in np.arange(1.5, 3.51, 0.02):
        for q in np.arange(1.5, 4.01, 0.02):
            pred = np.clip(1 - ts ** q, 0, 1) ** (1 / p)
            e = np.mean((pred - rs) ** 2)
            if best is None or e < best[0]:
                best = (e, p, q)
    return best
for item in a:
    js, idx = item.rsplit(":", 1)
    meas = json.load(open(js))[int(idx)]
    img = load(find(js, meas["image"]))
    lum = img @ np.array([0.2126, 0.7152, 0.0722])
    hd = meas["head"]; R = hd["R"]
    # darkness: 0 on the skin around the eye, 1 on the eye; the glint is filled in row by row below
    print("IMG", meas["image"], "head top %d, collar junction %d (span %d px), R %.1f" % (hd["top"], hd["neck"], hd["neck"] - hd["top"], R))
    for k, e in enumerate(meas["eyes"]):
        ex, ey = e["px"]
        bw, bh = e["box"][0] * R, e["box"][1] * R
        x0, x1 = int(ex - 0.75 * bw), int(ex + 0.75 * bw)
        y0, y1 = int(ey - 0.7 * bh), int(ey + 0.7 * bh)
        L = lum[y0:y1, x0:x1]
        skin = np.median(np.concatenate([L[:3].ravel(), L[-3:].ravel(), L[:, :3].ravel(), L[:, -3:].ravel()]))
        dark = np.clip((skin - L) / max(skin - 0.06, 1e-3), 0, 1)
        # rows
        rows = []
        for r in range(dark.shape[0]):
            c = crossings(dark[r])
            if c: rows.append((r, c[0], c[1]))
        cols = []
        for c_ in range(dark.shape[1]):
            c = crossings(dark[:, c_])
            if c: cols.append((c_, c[0], c[1]))
        rows = np.array(rows); cols = np.array(cols)
        left, right = rows[:, 1].min(), rows[:, 2].max()
        top, bot = cols[:, 1].min(), cols[:, 2].max()
        xc, yc = (left + right) / 2, (top + bot) / 2
        A, Bh = (right - left) / 2, (bot - top) / 2
        out = "  eye %d: box %.1f x %.1f px (%.3f x %.3f R), h/w %.3f" % (k, 2 * A, 2 * Bh, 2 * A / R, 2 * Bh / R, Bh / A)
        for half, sel in (("upper", rows[:, 0] < yc), ("lower", rows[:, 0] >= yc)):
            r_ = rows[sel]
            t = np.abs(r_[:, 0] + 0.5 - yc) / Bh
            w = (r_[:, 2] - r_[:, 1]) / 2 / A
            ok = t < 0.97
            e_, p, q = fit(t[ok], w[ok])
            out += " | %s p %.2f q %.2f (rms %.3f)" % (half, p, q, math.sqrt(e_))
        # half-width at a few heights (fraction of the max), both halves together
        t = np.abs(rows[:, 0] + 0.5 - yc) / Bh; w = (rows[:, 2] - rows[:, 1]) / 2 / A
        prof = []
        for tt in (0.3, 0.5, 0.7, 0.85, 0.95):
            m = np.abs(t - tt) < 0.03
            prof.append(np.mean(w[m]) if m.any() else float("nan"))
        out += "\n    half-width at t=0.3/0.5/0.7/0.85/0.95: " + " ".join("%.3f" % v for v in prof)
        for half, sgn in (("up", -1), ("down", 1)):
            pr = []
            for tt in (0.2, 0.4, 0.6, 0.75, 0.85, 0.93):
                yy_ = yc + sgn * tt * Bh
                m = np.abs(rows[:, 0] + 0.5 - yy_) < 0.6
                pr.append(np.mean(w[m]) if m.any() else float("nan"))
            out += "\n      %-4s t=0.2/0.4/0.6/0.75/0.85/0.93: " % half + " ".join("%.3f" % v for v in pr)
        iw = np.argmax(rows[:, 2] - rows[:, 1])
        out += "\n      widest row at %+.3f of the half-height from the middle (+ = down)" % ((rows[iw, 0] + 0.5 - yc) / Bh)
        out += "  (ellipse: 0.954 0.866 0.714 0.527 0.312)"
        # placement on the visible head
        cy_abs = y0 + yc; top_abs = y0 + top; bot_abs = y0 + bot
        span = hd["neck"] - hd["top"]
        out += "\n    eye middle at %.3f of the head span from the top; eye top %.3f, bottom %.3f; eye height %.3f of the span; width %.3f of 2R" % (
            (cy_abs - hd["top"]) / span, (top_abs - hd["top"]) / span, (bot_abs - hd["top"]) / span, 2 * Bh / span, 2 * A / (2 * R))
        print(out)
