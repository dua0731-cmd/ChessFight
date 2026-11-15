# Measures the face in design images and in our renders the same way: the head's circle (fitted to the outline above
# the collar), every eye blob and blush blob (connected pieces, not left/right halves, so a 3/4 view works), their
# centres, boxes and moment ellipses, and each eye's glint. Positions are in head radii from the head's centre
# (x to the right, y down, as in the image).
# blender -b --factory-startup -P face_measure.py -- <out.json> [--team white|black] [--piece pawn|rook|bishop] img ...
# A render with a transparent background is put on white first.
import bpy, sys, json, math
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a.pop(0)
TEAM, PIECE = "white", "pawn"
paths = []
i = 0
while i < len(a):
    if a[i] == "--team":
        TEAM = a[i + 1]; i += 2
    elif a[i] == "--piece":
        PIECE = a[i + 1]; i += 2
    else:
        paths.append(a[i]); i += 1


def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]      # rows from the top
    bpy.data.images.remove(im)
    al = x[..., 3:4]
    return np.clip(x[..., :3] * al + (1 - al), 0, 1)                         # onto white


def components(mask, min_px=40):
    """4-connected pieces of a boolean mask, biggest first: arrays of (row, col)."""
    h, w = mask.shape
    lab = np.zeros((h, w), dtype=np.int32)
    out = []
    ys, xs = np.nonzero(mask)
    nxt = 1
    for y0, x0 in zip(ys, xs):
        if lab[y0, x0]:
            continue
        stack = [(y0, x0)]
        lab[y0, x0] = nxt
        pix = []
        while stack:
            y, x = stack.pop()
            pix.append((y, x))
            for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= yy < h and 0 <= xx < w and mask[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = nxt
                    stack.append((yy, xx))
        nxt += 1
        if len(pix) >= min_px:
            out.append(np.array(pix))
    out.sort(key=len, reverse=True)
    return out


def head_circle(x):
    lum = x @ np.array([0.2126, 0.7152, 0.0722])
    fg = lum < 0.93
    h, w = fg.shape
    widths = []
    for r in range(h):
        cols = np.nonzero(fg[r])[0]
        widths.append((cols.min(), cols.max()) if len(cols) else None)
    top = next(r for r in range(h) if widths[r])
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
    return cx, cy, math.sqrt(c + cx * cx + cy * cy), top, neck


def ellipse_of(pix):
    """Centre, full axes (major, minor) and the major axis' angle from the image's vertical (degrees, + = top leans
    right) of a filled blob, from its second moments."""
    y = pix[:, 0].astype(np.float64); x = pix[:, 1].astype(np.float64)
    my, mx = y.mean(), x.mean()
    cov = np.cov(np.vstack([x - mx, y - my]))
    ev, evec = np.linalg.eigh(cov)
    big = evec[:, 1]
    ang = math.degrees(math.atan2(big[0], -big[1]))
    if ang > 90: ang -= 180
    if ang < -90: ang += 180
    return mx, my, 4 * math.sqrt(ev[1]), 4 * math.sqrt(ev[0]), ang


def measure(p):
    x = load(p)
    h, w = x.shape[:2]
    cx, cy, R, top, neck = head_circle(x)
    lum = x @ np.array([0.2126, 0.7152, 0.0722])
    yy, xx = np.mgrid[0:h, 0:w]
    if PIECE == "pawn":
        region = (xx - cx) ** 2 + (yy - cy) ** 2 < (R * 0.995) ** 2
    else:
        region = (yy > top) & (yy < h * 0.75)
    r, g, bl = x[..., 0], x[..., 1], x[..., 2]
    sat_ = x.max(axis=2) - x.min(axis=2)
    low = yy < cy + 0.8 * R                                   # not the neck's shadow
    if TEAM == "white":
        eye = (lum < 0.4) & (sat_ < 0.2) & region
        blush = (r - g > 0.22) & (r > 0.75) & region & low
    else:
        eye = (lum > 0.62) & (r > 0.8) & (bl > 0.45) & (r - bl < 0.35) & region
        blush = (r - g > 0.2) & (r > 0.55) & (lum < 0.62) & region & low
    res = dict(image=p.replace("\\", "/").split("/")[-1], size=[w, h], head=dict(cx=cx, cy=cy, R=R, top=top, neck=neck),
               eyes=[], blush=[])
    for name, mask in (("eyes", eye), ("blush", blush)):
        for pix in [c for c in components(mask) if len(c) > 0.01 * R * R][:2]:
            mx, my, maj, mnr, ang = ellipse_of(pix)
            y0, x0 = pix.min(axis=0); y1, x1 = pix.max(axis=0)
            item = dict(centre=[(mx - cx) / R, (my - cy) / R], box=[(x1 - x0 + 1) / R, (y1 - y0 + 1) / R],
                        box_centre=[((x0 + x1) / 2 - cx) / R, ((y0 + y1) / 2 - cy) / R],
                        axes=[maj / R, mnr / R], tilt=ang, area=len(pix) / (R * R), px=[float(mx), float(my)])
            if name == "eyes" and TEAM == "white":
                # the glint: white (not skin) pixels inside the eye's moment ellipse
                sat = x.max(axis=2) - x.min(axis=2)
                big = math.radians(ang)
                du, dv = xx - mx, yy - my
                along = -du * math.sin(big) + dv * math.cos(big)        # along the major axis
                across = du * math.cos(big) + dv * math.sin(big)
                inside = (along / (maj / 2)) ** 2 + (across / (mnr / 2)) ** 2 < 0.97
                gl = (lum > 0.72) & (sat < 0.16) & inside
                gc = components(gl, 4)
                if gc:
                    gp = gc[0]
                    gx, gy = gp[:, 1].mean(), gp[:, 0].mean()
                    gy0, gx0 = gp.min(axis=0); gy1, gx1 = gp.max(axis=0)
                    item["glint"] = dict(at_box=[(gx - x0) / (x1 - x0 + 1), (gy - y0) / (y1 - y0 + 1)],
                                         from_centre=[(gx - mx) / R, (gy - my) / R],
                                         diam_eq=2 * math.sqrt(len(gp) / math.pi) / R,
                                         box=[(gx1 - gx0 + 1) / R, (gy1 - gy0 + 1) / R])
            res[name].append(item)
        res[name].sort(key=lambda it: it["centre"][0])
    return res


def clean(o):
    if isinstance(o, dict):
        return {k: clean(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [clean(v) for v in o]
    if isinstance(o, (np.floating, np.integer)):
        return o.item()
    return o


allres = [measure(p) for p in paths]
for r in allres:
    hd = r["head"]
    print("IMG %s head (%.1f, %.1f) R %.1f" % (r["image"], hd["cx"], hd["cy"], hd["R"]))
    for e in r["eyes"]:
        s = "  eye   c(%+.3f, %+.3f) box %.3f x %.3f  axes %.3f x %.3f tilt %+.1f area %.4f" % (
            *e["centre"], *e["box"], *e["axes"], e["tilt"], e["area"])
        if "glint" in e:
            gl = e["glint"]
            s += "  glint box-rel (%.2f, %.2f) d %.3f (%.2f of eye w)" % (*gl["at_box"], gl["diam_eq"], gl["diam_eq"] / e["box"][0])
        print(s)
    for e in r["blush"]:
        print("  blush c(%+.3f, %+.3f) box %.3f x %.3f  axes %.3f x %.3f tilt %+.1f" % (*e["centre"], *e["box"], *e["axes"], e["tilt"]))
json.dump(clean(allres), open(OUT, "w"), indent=1)
