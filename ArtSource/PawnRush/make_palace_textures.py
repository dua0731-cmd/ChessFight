"""Textures for the royal palace (10-22 v2, 승규 "그래픽이 아쉬워"): an ashlar wall and a stratified rock.

blender --background --factory-startup --python make_palace_textures.py -- <out_dir>
PR_Ashlar_*: one tile = 4 x 4 m of wall (the palace's stone UVs: one unit = 4 m), courses 0.5 m high, blocks of
0.7..1.4 m in running bond, recessed joints, worn arrises with small chips, each block its own shade and a
weathered foot. PR_PalaceRock_*: one tile = 24 m of crag, warped strata with ledges and vertical fractures.
Albedo maps are near-white multipliers (the URP material colour sets the hue), all seamless, 1024 px.
"""
import os
import sys

import bpy
import numpy as np

OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
N = 1024
rng = np.random.default_rng(1022)


def tile_noise(cells):
    g = rng.random((cells, cells))
    x = np.linspace(0, cells, N, endpoint=False)
    i0 = np.floor(x).astype(int)
    f = x - i0
    f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = g[np.ix_(i0, i0)]
    b = g[np.ix_(i0, i1)]
    c = g[np.ix_(i1, i0)]
    d = g[np.ix_(i1, i1)]
    fx = f[None, :]
    fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(base, octaves):
    v = np.zeros((N, N))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        v += tile_noise(base * 2 ** o) * amp
        total += amp
        amp *= 0.5
    return v / total


def aniso(cx, cy):
    """Seamless value noise with its own cell counts across and down (cy small: streaks running down)."""
    g = rng.random((cy, cx))
    x = np.linspace(0, cx, N, endpoint=False)
    y = np.linspace(0, cy, N, endpoint=False)
    xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - xi, y - yi
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = g[np.ix_(yi, xi)]
    b = g[np.ix_(yi, (xi + 1) % cx)]
    c = g[np.ix_((yi + 1) % cy, xi)]
    d = g[np.ix_((yi + 1) % cy, (xi + 1) % cx)]
    fx, fy = fx[None, :], fy[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def save(name, rgb):
    img = bpy.data.images.new(name, N, N, alpha=False)
    rgba = np.ones((N, N, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(OUT, name + ".png")
    img.file_format = "PNG"
    img.save()
    print("SAVED", img.filepath_raw)


def normal_from(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, -dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


# ---------------------------------------------------------------- ashlar
PX_M = N / 4.0                       # pixels per metre
course = int(0.5 * PX_M)             # 128 px
rows = N // course
block_id = np.zeros((N, N), dtype=np.int32)
edge_d = np.zeros((N, N))            # distance to the block's nearest edge, px
local_v = np.zeros((N, N))           # 0 at a block's foot .. 1 at its top
bid = 0
ys = np.arange(N)
for r in range(rows):
    y0 = r * course
    # lengths in px for this course summing to N, at a random offset (running bond)
    lens = []
    total = 0
    while total < N:
        L = int(rng.uniform(0.7, 1.4) * PX_M)
        if N - total - L < 0.6 * PX_M:
            L = N - total
        lens.append(L)
        total += L
    off = int(rng.uniform(0, N))
    x = 0
    for L in lens:
        xs = (np.arange(x, x + L) + off) % N
        yy = np.arange(y0, y0 + course)
        X, Y = np.meshgrid(xs, yy)
        lx = np.arange(L)[None, :].repeat(course, 0)
        ly = np.arange(course)[:, None].repeat(L, 1)
        block_id[Y, X] = bid
        edge_d[Y, X] = np.minimum(np.minimum(lx, L - 1 - lx), np.minimum(ly, course - 1 - ly))
        local_v[Y, X] = 1 - ly / (course - 1)          # image rows run top-down in numpy; flipped on save below
        bid += 1
        x += L

shade = rng.uniform(0.82, 1.0, bid)[block_id]
tint = rng.uniform(0.0, 0.045, bid)[block_id]        # warm only (cream to honey)
n_lo = fbm(6, 4)
n_hi = fbm(48, 3)
chips = fbm(24, 3)
mortar = 5.0
# worn edge: the mortar line wanders and bites into the arrises where the chip noise is high
edge = edge_d - (chips - 0.5) * 4.0 - np.clip(chips - 0.7, 0, 1) * 18.0
# 10-22 (승규 녹화 "깜빡깜빡"): soft joints (a wide ramp, low contrast) so the far mip levels do not shimmer
inside = np.clip((edge - mortar) / 16.0, 0, 1)           # 0 in the joint .. 1 on the face
bevel = np.sqrt(inside)
h = bevel * (0.75 + 0.25 * n_lo) + (n_hi - 0.5) * 0.12 * inside
pits = np.clip(fbm(96, 2) - 0.66, 0, 1) * 3.0
h -= pits * 0.25 * inside
grime = np.clip(1 - local_v * 3.0, 0, 1) * 0.08          # a darker weathered foot on each block
face = shade * (0.92 + 0.1 * n_lo + 0.06 * (n_hi - 0.5)) - grime - pits * 0.08
alb = np.where(inside > 0.01, face, 0.7 + 0.06 * n_hi)
alb = alb * (1 - (1 - inside) * 0.15)
rgb = np.stack([alb * (1 + tint * 0.5), alb, alb * (1 - tint * 1.5)], -1)
save("PR_Ashlar_Albedo", rgb[::-1])
save("PR_Ashlar_Normal", normal_from(h, 5.0)[::-1])

# ---------------------------------------------------------------- rock
warp = fbm(3, 4)
yy = (np.arange(N)[:, None] / N + (warp - 0.5) * 0.25) % 1.0
bands = 6
t = (yy * bands) % 1.0
ledge = (1 - t) ** 2.2                                    # each stratum bulges at its top, recedes below
layer = np.floor(yy * bands).astype(int) % bands
layer_shade = rng.uniform(0.78, 1.02, bands)[layer]
rough = fbm(8, 5)
fine = fbm(64, 3)
# vertical fractures: thin dark lines where a stretched noise crosses its middle
frac_n = aniso(18, 2) * 0.7 + aniso(40, 3) * 0.3
fract = np.exp(-((frac_n - 0.5) / 0.01) ** 2) * np.clip((aniso(6, 3) - 0.45) * 6, 0, 1)
rough = rough * 0.7 + aniso(30, 4) * 0.3                   # a vertical grain from rain wash
hr = ledge * 0.6 + rough * 0.5 + fine * 0.15 - fract * 0.4
albr = layer_shade * (0.82 + 0.25 * rough + 0.08 * (fine - 0.5)) - fract * 0.35 - (1 - ledge) * 0.06
save("PR_PalaceRock_Albedo", np.stack([albr * 1.02, albr, albr * 0.95], -1)[::-1])
save("PR_PalaceRock_Normal", normal_from(hr, 7.0)[::-1])
print("DONE")
