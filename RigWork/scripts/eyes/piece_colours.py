# Median colours of a piece's design front image: eye core, blush core (pink blobs), body next to the eyes.
import bpy, sys
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
for p in a:
    team = "black" if ("black_" in p or "_black" in p) else "white"
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]; bpy.data.images.remove(im)
    lum = x @ np.array([0.2126, 0.7152, 0.0722]); sat = x.max(axis=2) - x.min(axis=2)
    top = slice(int(h * 0.25), int(h * 0.62))
    xs = x[top]; ls = lum[top]; ss = sat[top]
    if team == "white":
        eye = (ls < 0.25) & (ss < 0.15)
        pink = (xs[..., 0] - xs[..., 1] > 0.3) & (xs[..., 0] > 0.8)
        body = (ls > 0.6) & (ls < 0.93) & (xs[..., 0] - xs[..., 1] < 0.2)
    else:
        eye = (ls > 0.8) & (xs[..., 0] - xs[..., 2] > 0.12)
        pink = (xs[..., 0] - xs[..., 1] > 0.25) & (xs[..., 0] > 0.6) & (ls < 0.62)
        body = (ls > 0.15) & (ls < 0.45) & (ss < 0.05)
    med = lambda m: tuple(round(float(v), 3) for v in np.median(xs[m], axis=0)) if m.sum() else None
    print(p.split("/")[-1], "eye", med(eye), "blush", med(pink), "body", med(body))
