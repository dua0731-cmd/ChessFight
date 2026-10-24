"""Tileable surface textures for the Pawn Rush kit (item 6): a stone grain (albedo multiplier + normal map)
and a faint polish streak for the glossy tiles. Seamless value noise, 512 px.

blender --background --factory-startup --python make_textures.py -- <out_dir>
Albedo maps are near-white (0.82..1.0) so the URP material colour still sets the hue; they only add grain.
"""
import os
import sys

import bpy
import numpy as np

OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
N = 512
rng = np.random.default_rng(17)


def tile_noise(cells):
    """Seamless value noise: random grid (wrapping) with smooth interpolation."""
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


def fbm(octaves):
    v = np.zeros((N, N))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        v += tile_noise(4 * 2 ** o) * amp
        total += amp
        amp *= 0.5
    return v / total


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


# stone: soft mottling + fine grit + a few pits
h = fbm(6)
grit = rng.random((N, N))
pits = (grit > 0.985).astype(float)
stone = 0.86 + 0.12 * h + 0.03 * (grit - 0.5) - 0.08 * pits
save("PR_StoneGrain_Albedo", np.repeat(stone[..., None], 3, -1))
save("PR_StoneGrain_Normal", normal_from(h * 0.6 + grit * 0.15 - pits * 0.6, 6.0))

# tiles: very faint cloudy polish so the gloss is not a flat plastic sheet
p = fbm(5)
tile = 0.93 + 0.07 * p
save("PR_TilePolish_Albedo", np.repeat(tile[..., None], 3, -1))
save("PR_TilePolish_Normal", normal_from(p * 0.3, 3.0))
print("DONE")


# ---- 10-22: mini-game surfaces (승규 "물 그래픽 등 사실적으로")
def stretched(cells_x, cells_y):
    g = rng.random((cells_y, cells_x))
    xs = np.linspace(0, cells_x, N, endpoint=False)
    ys = np.linspace(0, cells_y, N, endpoint=False)
    xi0 = np.floor(xs).astype(int); fx = xs - xi0; fx = fx * fx * (3 - 2 * fx); xi1 = (xi0 + 1) % cells_x
    yi0 = np.floor(ys).astype(int); fy = ys - yi0; fy = fy * fy * (3 - 2 * fy); yi1 = (yi0 + 1) % cells_y
    a = g[np.ix_(yi0, xi0)]; b = g[np.ix_(yi0, xi1)]; c = g[np.ix_(yi1, xi0)]; d = g[np.ix_(yi1, xi1)]
    return (a * (1 - fx) + b * fx) * (1 - fy[:, None]) + (c * (1 - fx) + d * fx) * fy[:, None]


# sand: fine grains + soft wind ripples
yy = np.linspace(0, 2 * np.pi * 6, N, endpoint=False)[:, None]
ripple = 0.5 + 0.5 * np.sin(yy + 2.0 * fbm(3))
grain = rng.random((N, N))
sand = 0.84 + 0.08 * ripple + 0.08 * (grain - 0.5) + 0.05 * fbm(5)
save("PR_Sand_Albedo", np.repeat(sand[..., None], 3, -1))
save("PR_Sand_Normal", normal_from(ripple * 0.35 + grain * 0.12, 5.0))

# wood: long grain along v with growth rings and a few knots
grainv = stretched(48, 3)
rings = 0.5 + 0.5 * np.sin(grainv * 40.0)
wood = 0.80 + 0.12 * rings + 0.08 * stretched(96, 6)
save("PR_Wood_Albedo", np.repeat(wood[..., None], 3, -1))
save("PR_Wood_Normal", normal_from(rings * 0.25 + grainv * 0.2, 4.0))

# water: smooth rolling waves (normal only, the shader scrolls two copies)
w = fbm(4) * 0.8 + tile_noise(8) * 0.4
save("PR_Water_Normal", normal_from(w, 9.0))
print("DONE2")


# ---- 10-22: real wooden chess pieces (u round the piece, v up it: the grain runs up)
streaks = stretched(96, 3) * 0.6 + stretched(24, 2) * 0.4
fibres = stretched(256, 8)
box = 0.9 + 0.07 * streaks + 0.03 * fibres
save("PR_Boxwood_Albedo", np.repeat(box[..., None], 3, -1))
save("PR_Boxwood_Normal", normal_from(fibres * 0.12, 2.0))
eb = 0.72 + 0.22 * streaks + 0.06 * fibres
save("PR_Ebony_Albedo", np.repeat(eb[..., None], 3, -1))
save("PR_Ebony_Normal", normal_from(fibres * 0.12, 2.0))
print("DONE3")
