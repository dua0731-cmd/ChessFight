# The lineup (pawn, rook, knight, bishop on their feet, one scale): each piece's cream body - top, skirt bottom,
# widest base, and its hands' height - in pixels and relative to the pawn.
import bpy, sys
import numpy as np
p = sys.argv[-1]
im = bpy.data.images.load(p); w, h = im.size
x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]
lum = x @ np.array([0.2126, 0.7152, 0.0722])
cream = (lum > 0.55) & (lum < 0.97) & (x[..., 0] - x[..., 2] > 0.08)
dark = lum < 0.2
cols = cream.any(axis=0)
# split into pieces by empty columns
runs, inrun = [], False
for c in range(w):
    if cols[c] and not inrun:
        start = c; inrun = True
    elif not cols[c] and inrun:
        if c - start > 40: runs.append((start, c))
        inrun = False
names = ["pawn", "rook", "knight", "bishop"]
res = {}
for name, (a, b) in zip(names, runs):
    m = cream[:, a:b]
    ys, xs = np.nonzero(m)
    top, bot = ys.min(), ys.max()
    widths = [(r, m[r].nonzero()[0]) for r in range(top, bot + 1)]
    base_w = max((xx.max() - xx.min()) for r, xx in widths if len(xx) and r > top + 0.6 * (bot - top))
    d = dark[:, max(0, a - 120):b + 120]
    dy = np.nonzero(d.any(axis=1))[0]
    dd = dark[:, a:b]
    fy = [r for r in range(top + int(0.7 * (bot - top)), h) if dd[r].sum() > 20]
    feet_top = min(fy) if fy else bot
    # the base width just above the feet
    base_w = max((xx.max() - xx.min()) for r, xx in widths if len(xx) and feet_top - 40 < r < feet_top - 5)
    # hands: dark blobs beside the body, their middle height
    side = dark[:, max(0, a - 140):a + 20]
    hy = np.nonzero(side.any(axis=1))[0]
    hands = (hy[(hy > top) & (hy < feet_top - 20)].mean() if len(hy) else -1)
    bot = feet_top
    res[name] = (top, bot, base_w, hands)
    print("PIECE %-6s top %d  feet top %d  height %d  base width %d  hands at %.0f" % (name, top, bot, bot - top, base_w, hands))
pt, pb, pw, ph = res["pawn"]
for n, (t, b, bw, hh) in res.items():
    print("REL %-6s height %.3f  base width %.3f  hands above feet %.3f of the pawn height" % (n, (b - t) / (pb - pt), bw / pw, (b - hh) / (pb - pt)))
