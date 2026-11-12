# R618 (승규 picked mockup "D 모카 석회암"): recolour only the cream-stone pixels of the baked colour maps.
# Same mapping as the mockup: hue -> 28 deg, saturation x1.45, value x0.76. Gold, red, wood and glass fall outside
# the mask (hue or saturation). Hall_Floor (cream marble checker) and the gate leaves are left alone.
# Usage: python stone_tint.py <src Bake dir> <dst Bake dir>
import sys, os, numpy as np
from PIL import Image
HUE, SAT, VAL = 28.0, 1.45, 0.76
SKIP = ("Hall_Floor", "GateLeaf_")

def hsv(a):
    mx = a.max(-1); mn = a.min(-1); d = np.maximum(mx - mn, 1e-6)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return h, s, mx

def rgb(H, S, V):
    H = (H / 60) % 6; i = np.floor(H).astype(int) % 6; f = H - np.floor(H)
    p, q, t = V * (1 - S), V * (1 - S * f), V * (1 - S * (1 - f))
    return np.stack([np.choose(i, [V, q, p, p, t, V]), np.choose(i, [t, V, V, q, p, p]),
                     np.choose(i, [p, p, t, V, V, q])], -1)

def tint(a):
    h, s, v = hsv(a)
    m = ((h > 15) & (h < 58)) * np.clip((0.40 - s) / 0.08, 0, 1) * np.clip((v - 0.30) / 0.1, 0, 1)
    out = rgb(np.full_like(h, HUE), np.clip(s * SAT, 0, 1), v * VAL)
    return a * (1 - m[..., None]) + out * m[..., None]

if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    for n in sorted(os.listdir(src)):
        if not n.endswith(".png") or n.endswith("_Metal.png") or n.startswith(SKIP): continue
        im = Image.open(os.path.join(src, n)); mode = im.mode
        a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
        a[..., :3] = tint(a[..., :3])
        Image.fromarray((np.clip(a, 0, 1) * 255 + .5).astype(np.uint8), "RGBA").convert(mode).save(os.path.join(dst, n))
        print("tinted", n)
    for c in (0xEADCC5, 0xF4EBDC, 0xD9C7AA):   # hall_paint.py Stone a / b / dark
        a = np.array([[[(c >> 16) / 255, (c >> 8 & 255) / 255, (c & 255) / 255]]], np.float32)
        o = (np.clip(tint(a), 0, 1)[0, 0] * 255 + .5).astype(int)
        print("%06X -> %02X%02X%02X" % (c, *o))
