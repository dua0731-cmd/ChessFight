"""The night behind the study's arched window (10-28, user "달 배경 만들어"): textures/T_SF_Night.png.

blender -b --factory-startup --python make_night_texture.py -- <textures_dir>
A sky darkening upward (the old pale blue-grey #6f82a6 at the bottom, deep night blue at the top), a full moon in
the arch (cream, faint seas, darker toward its rim) in a soft cool halo, and sparse small stars. Drawn unlit in the
game on the sheet behind the frame (SwordFightArena.Room), so the fog still dims it; its brightest pixel is 1,
under the game's bloom threshold, so it glows only as much as painted and never outshines the skill effects.
The sheet is 0.78 of the frame wide and 0.9 high (21.8 x 26.1 m): the texture keeps that shape.
"""
import bpy
import math
import os
import sys
import numpy as np

OUT = sys.argv[sys.argv.index("--") + 1]
W, H = 1024, 1224                       # the sheet's 0.837 width to height
MOON_U, MOON_V, MOON_R = 0.63, 0.81, 0.085   # in the arch (above the transom, v > 0.66), radius as a share of the width
rng = np.random.default_rng(28)


def hexc(h):
    return np.array([int(h[k:k + 2], 16) / 255 for k in (0, 2, 4)], dtype=np.float32)


def smooth_noise(h, w, cells, seed):
    """Value noise: a coarse random grid blown up smoothly."""
    g = np.random.default_rng(seed).random((cells + 2, cells + 2)).astype(np.float32)
    ys, xs = np.linspace(0, cells, h), np.linspace(0, cells, w)
    y0, x0 = np.floor(ys).astype(int), np.floor(xs).astype(int)
    fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a, b = g[y0][:, x0], g[y0][:, x0 + 1]
    c, d = g[y0 + 1][:, x0], g[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


v = (np.arange(H, dtype=np.float32) + 0.5)[:, None] / H     # 0 bottom .. 1 top (Blender's pixel rows go up)
u = (np.arange(W, dtype=np.float32) + 0.5)[None, :] / W

# The sky: pale at the bottom, deep at the top, a faint uneven haze so it is not a flat gradient.
low, high = hexc("6f82a6"), hexc("1b2645")
t = np.clip(v, 0, 1) ** 0.8
sky = low[None, None, :] * (1 - t[..., None]) + high[None, None, :] * t[..., None]
haze = smooth_noise(H, W, 6, 3)[..., None]
sky = sky * (0.93 + 0.12 * haze)

# The halo round the moon: a soft cool glow, wide and faint.
aspect = W / H
dx, dy = (u - MOON_U), (v - MOON_V) / aspect
d = np.sqrt(dx * dx + dy * dy) / MOON_R                        # 1 at the moon's rim
halo = np.exp(-np.maximum(d - 1, 0) * 1.6) * 0.35 + np.exp(-np.maximum(d - 1, 0) * 0.45) * 0.12
sky = sky + hexc("b9c8ec")[None, None, :] * halo[..., None]

# The stars: sparse, small, mostly up high, none in the halo.
img = sky
stars = 90
sy = rng.random(stars) ** 0.6 * 0.95 + 0.04
sx = rng.random(stars)
for k in range(stars):
    cx, cy = sx[k] * W, sy[k] * H
    if math.hypot((sx[k] - MOON_U), (sy[k] - MOON_V) / aspect) < MOON_R * 3.2:
        continue
    r = rng.uniform(0.8, 1.8)
    b = rng.uniform(0.35, 0.9) * (0.6 + 0.4 * sy[k])
    x0, x1, y0, y1 = int(cx - 4), int(cx + 5), int(cy - 4), int(cy + 5)
    yy, xx = np.mgrid[max(y0, 0):min(y1, H), max(x0, 0):min(x1, W)]
    f = np.exp(-((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2) / (r * r)) * b
    img[max(y0, 0):min(y1, H), max(x0, 0):min(x1, W)] += f[..., None] * hexc("eef2ff")[None, None, :]

# The moon: a cream disc, its seas faint grey-blue blotches, darker toward the rim, a soft edge.
rim = np.clip((1 - d) / 0.02, 0, 1)                            # anti-aliased edge
inside = np.clip(1 - d, 0, 1)
limb = 0.78 + 0.22 * np.sqrt(np.clip(1 - d * d, 0, 1))
seas = smooth_noise(H, W, 40, 9)
seas = np.clip((seas - 0.55) * 3.0, 0, 1) * (inside > 0)
moon = hexc("f3eedd")[None, None, :] * limb[..., None] * (1 - 0.18 * seas[..., None])
moon = moon * 1.0
img = img * (1 - rim[..., None]) + moon * rim[..., None]

img = np.clip(img, 0, 1)
rgba = np.concatenate([img, np.ones((H, W, 1), np.float32)], axis=2)
os.makedirs(OUT, exist_ok=True)
im = bpy.data.images.new("T_SF_Night", W, H, alpha=False)
im.pixels.foreach_set(rgba.astype(np.float32).ravel())
im.filepath_raw = os.path.join(OUT, "T_SF_Night.png")
im.file_format = "PNG"
im.save()
print(f"[night] T_SF_Night.png {W}x{H}, moon at ({MOON_U}, {MOON_V}) r {MOON_R}")
