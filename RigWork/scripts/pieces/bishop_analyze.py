# Reads the measured runs (bishop_measure.py) and prints the numbers the model is built from, in pixels about each
# view's axis: the front half width per row, the side views' edges (head front, head back, petal front, petal back),
# the seam under the V between head and petal (darkest line), and the petal's outline in the back view.
# blender -b --factory-startup -P bishop_analyze.py -- <measure dir>
import bpy, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
D = a[0]
rep = json.load(open(os.path.join(D, "measure.json"), encoding="utf-8"))
DL = "C:/Users/trews/Downloads/ChatGPT 이미지 2026년 11월 9일 오후 "
VIEWS = {"front": DL + "10_39_12.png", "left": DL + "10_31_49.png", "right": DL + "10_31_51.png",
         "back": DL + "10_38_40.png"}

def lum(x):
    return x[..., :3] @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

def smooth(v, k=5):
    return np.convolve(v, np.ones(k) / k, mode="same")

# ---- front and back: half width per row (the wider side is not used: both sides, and their mean)
for name in ("front", "back"):
    v = rep[name]
    ax = v["axis"]
    print("HALF %s axis %.1f top %d" % (name, ax, v["top"]))
    out = []
    for r in range(v["top"], v["bottom"] + 1, 4):
        rs = v["runs"].get(str(r))
        if not rs:
            continue
        l, rr = rs[0][0], rs[-1][1] - 1
        out.append("%d:%.0f/%.0f" % (r, ax - l, rr - ax))
    for i in range(0, len(out), 12):
        print("  " + "  ".join(out[i:i + 12]))

# ---- side views: runs about the axis, sign so that + is towards the back
for name, sgn in (("left", 1), ("right", -1)):
    v = rep[name]
    ax = v["axis"]
    print("SIDE %s axis %.1f top %d (+ = back)" % (name, ax, v["top"]))
    for r in range(v["top"], v["top"] + 600, 6):
        rs = v["runs"].get(str(r))
        if not rs:
            continue
        ed = []
        for s, e in rs:
            p, q = sgn * (s - ax), sgn * (e - 1 - ax)
            ed.append("%+.0f..%+.0f" % (min(p, q), max(p, q)))
        print("  %d  %s" % (r, "  ".join(ed if sgn > 0 else ed[::-1])))

# ---- the seam under the V in the side views: the darkest pixel in a window behind the axis
for name, sgn in (("left", 1), ("right", -1)):
    v = rep[name]
    ax = v["axis"]
    L = lum(bi.load(VIEWS[name]))
    print("SEAM %s (row: offset behind the axis, luminance there)" % name)
    out = []
    for r in range(v["top"] + 380, v["top"] + 575, 6):
        band = L[r - 1:r + 2].mean(axis=0)
        xs = np.arange(int(ax + sgn * 10), int(ax + sgn * 95), sgn)
        seg = smooth(band[xs], 3)
        i = int(np.argmin(seg[2:-2])) + 2
        out.append("%d:%+.0f(%.2f)" % (r, sgn * (xs[i] - ax), seg[i]))
    for i in range(0, len(out), 8):
        print("  " + "  ".join(out[i:i + 8]))

# ---- the petal's outline in the back view: the darkest line between the silhouette and the axis, per side
v = rep["back"]
ax = v["axis"]
L = lum(bi.load(VIEWS["back"]))
print("PETAL back view (row: left offset / right offset from the axis; silhouette half widths)")
out = []
for r in range(v["top"] + 186, v["top"] + 420, 6):
    rs = v["runs"].get(str(r))
    l, rr = rs[0][0], rs[-1][1] - 1
    band = L[r - 1:r + 2].mean(axis=0)
    res = []
    for lo, hi in ((l + 5, int(ax) - 4), (int(ax) + 4, rr - 5)):
        seg = smooth(band[lo:hi], 3)
        # the strongest dark dip: the minimum of (value - local mean over 25 px)
        base = smooth(band[lo:hi], 25)
        d = seg - base
        i = int(np.argmin(d[3:-3])) + 3
        res.append((lo + i - ax, d[i]))
    out.append("%d:%+.0f(%.2f)/%+.0f(%.2f) sil %.0f/%.0f" % (r, res[0][0], res[0][1], res[1][0], res[1][1], ax - l, rr - ax))
for i in range(0, len(out), 4):
    print("  " + "  ".join(out[i:i + 4]))
print("DONE")
