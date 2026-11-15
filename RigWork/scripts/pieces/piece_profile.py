# Front silhouette of a design image that also shows arms, feet and a face: per row the outer edges of everything
# that is not the white page (and separately of the dark parts: hands, feet, eyes), about the body's axis.
# blender -b --factory-startup -P piece_profile.py -- <image> <out.json> [--white 0.972] [--step 4] [--rows a:b]
import bpy, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
IMG, OUT = a[0], a[1]
def opt(name, default):
    return a[a.index(name) + 1] if name in a else default
WHITE = float(opt("--white", "0.972"))
STEP = int(opt("--step", "4"))
x = bi.load(IMG)
h, w = x.shape[:2]
rgb = x[..., :3]
lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
ink = (lum < WHITE) | (rgb.max(axis=2) - rgb.min(axis=2) > 0.05)       # anything but the page
dark = lum < 0.30                                                     # hands, feet, eyes
rows = {}
for r in range(h):
    rs = bi.runs(ink[r], min_len=4, max_gap=6)
    if rs:
        ds = bi.runs(dark[r], min_len=4, max_gap=3)
        rows[r] = (rs, ds)
keys = sorted(rows)
top = keys[0]
# the axis: the middle of the widest light run in the rows of the lower body (between 55 and 80 percent down to the
# last row with ink)
bot = keys[-1]
mids = []
for r in keys:
    if top + 0.55 * (bot - top) < r < top + 0.80 * (bot - top):
        s, e = max(rows[r][0], key=lambda q: q[1] - q[0])
        mids.append((s + e - 1) / 2)
axis = float(np.median(mids))
print("PROFILE %s size %dx%d top %d last %d axis %.1f" % (os.path.basename(IMG)[-14:], w, h, top, bot, axis))
r0, r1 = (int(v) for v in opt("--rows", "%d:%d" % (top, bot)).split(":"))
line = []
for r in range(r0, r1 + 1, STEP):
    if r not in rows:
        continue
    rs, ds = rows[r]
    t = "%d: %s" % (r, " ".join("%+.0f..%+.0f" % (s - axis, e - 1 - axis) for s, e in rs))
    if ds:
        t += "  dark " + " ".join("%+.0f..%+.0f" % (s - axis, e - 1 - axis) for s, e in ds)
    line.append(t)
for t in line:
    print("  " + t)
with open(OUT, "w", encoding="utf-8") as f:
    json.dump(dict(size=[w, h], top=top, last=bot, axis=axis,
                   rows={str(r): dict(ink=rows[r][0], dark=rows[r][1]) for r in keys}), f)
