# Median colours of the design face parts: eye body (inner 70% of the eye, glint excluded), glint core, blush core,
# the head skin next to the eye. blender -b --factory-startup -P sample_design_colors.py -- <measure.json> <img dir> ...
import bpy, sys, json, math, os
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
res = json.load(open(a[0]))
dirs = a[1:]
def find(name):
    for d in dirs:
        p = os.path.join(d, name)
        if os.path.exists(p):
            return p
def load(p):
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]; bpy.data.images.remove(im); return x
for r in res:
    p = find(r["image"])
    x = load(p)
    h, w = x.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    hd = r["head"]; R = hd["R"]
    lum = x @ np.array([0.2126, 0.7152, 0.0722])
    sat = x.max(axis=2) - x.min(axis=2)
    out = []
    for e in r["eyes"]:
        mx, my = e["px"]
        bw, bh = e["box"][0] * R, e["box"][1] * R
        ell = ((xx - mx) / (bw / 2)) ** 2 + ((yy - my) / (bh / 2)) ** 2
        inner = ell < 0.7 ** 2
        ring = (ell > 1.25 ** 2) & (ell < 1.6 ** 2)
        bright = inner & (lum > 0.8) & (sat < 0.15)
        body = inner & ~bright & (lum < 0.5) if lum[inner].mean() < 0.5 else inner & ~bright
        med = lambda m: tuple(round(float(v), 3) for v in np.median(x[m], axis=0)) if m.sum() else None
        pct = lambda m: tuple(round(float(np.percentile(lum[m], q)), 3) for q in (5, 50, 95)) if m.sum() else None
        out.append("eye body %s lum p5/50/95 %s | glint %s n=%d | skin ring %s" % (med(body), pct(body), med(bright), bright.sum(), med(ring)))
    for b in r["blush"]:
        mx, my = b["px"]
        bw, bh = b["box"][0] * R, b["box"][1] * R
        ell = ((xx - mx) / (bw / 2)) ** 2 + ((yy - my) / (bh / 2)) ** 2
        core = ell < 0.5 ** 2
        out.append("blush core %s" % (tuple(round(float(v), 3) for v in np.median(x[core], axis=0)),))
    print("IMG", r["image"]); [print("  " + s) for s in out]
