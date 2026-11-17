# Royal Carriage Yard surface textures (R686, seoungtest, 11-17 승규 "재질 텍스쳐 컨셉 아트대로 재질 그래픽 극대화로 나오게 구워").
#
#   blender -b --factory-startup --python rcy_textures.py -- <out folder> [name,name...] [size]
#
# One seamless tile per material slot ("RCY | stone" -> RCY_Stone): <Name>_Albedo.png (sRGB colour, alpha = smoothness)
# and <Name>_Normal.png (OpenGL +Y, Unity's and Blender's convention), drawn procedurally with numpy (band-limited FFT
# noise, periodic Worley cells, painted wood grain) so every tile wraps exactly. The look is the concept's
# (ArtSource/PawnRush/CarriageYardConcept/R677_ModelingRefs, RCY_08's swatch row and 승규's paintings): soft painterly
# mottling, warm cream stone, honey oak and red walnut with flowing grain, dark warm iron, matte brass, grey slate,
# sand canvas. Crevices are darkened in the albedo (the hand-painted way), so no separate AO map: two textures a material.
#
# The blocks, slabs, boards and tiles are modelled, so the tiles carry no joints: only the surface of one stone, one
# board. The models' UVs are in metres (rcy_kit.surface_uvs, a random offset a part), and SPECS' tile is how many metres
# one tile covers (Unity: the material's tiling = 1 / tile).

import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
N = 1024

# ------------------------------------------------------------------ noise (all periodic over the tile)


def _k(n=None, stretch=1.0):
    n = n or N
    ky = np.fft.fftfreq(n) * n
    kx = np.fft.rfftfreq(n) * n
    return np.sqrt((kx[None, :] * stretch) ** 2 + ky[:, None] ** 2)


def fbm(seed, fmin, fmax, beta=1.0, stretch=1.0, n=None):
    """Band-limited noise between fmin and fmax cycles a tile (amplitude ~ f^-beta), std 1. stretch > 1 draws it out
    along u (the image's x): wood grain, slate flakes, brush strokes."""
    n = n or N
    rng = np.random.default_rng(seed)
    F = np.fft.rfft2(rng.standard_normal((n, n)).astype(np.float32))
    K = _k(n, stretch)
    K[0, 0] = 1e-6
    lo = np.clip((K - fmin * .7) / (fmin * .6 + 1e-6), 0, 1)
    hi = np.clip((fmax * 1.3 - K) / (fmax * .6 + 1e-6), 0, 1)
    out = np.fft.irfft2(F * lo * hi * K ** (-beta), s=(n, n)).astype(np.float32)
    out -= out.mean()
    return out / (out.std() + 1e-8)


def blur(a, sigma):
    """A periodic Gaussian blur (sigma in pixels)."""
    n = a.shape[0]
    K = _k(n)
    return np.fft.irfft2(np.fft.rfft2(a) * np.exp(-.5 * (K * 2 * math.pi * sigma / n) ** 2), s=a.shape).astype(np.float32)


def worley(seed, count, n=None, jitter=1.0):
    """Distance (pixels) to the nearest and second nearest of `count` random points, wrapped round the tile."""
    n = n or N
    rng = np.random.default_rng(seed)
    pts = rng.random((count, 2)) * n
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    d1 = np.full((n, n), 1e9, np.float32)
    d2 = np.full((n, n), 1e9, np.float32)
    for px, py in pts:
        dx = np.abs(xx - px)
        dx = np.minimum(dx, n - dx)
        dy = np.abs(yy - py)
        dy = np.minimum(dy, n - dy)
        d = np.sqrt(dx * dx + dy * dy)
        d2 = np.where(d < d1, d1, np.minimum(d2, d))
        d1 = np.minimum(d1, d)
    return d1, d2


def sample(a, x, y):
    """Bilinear, wrapped: a at fractional pixel (x, y)."""
    n = a.shape[0]
    x0 = np.floor(x).astype(np.int64)
    y0 = np.floor(y).astype(np.int64)
    tx, ty = x - x0, y - y0
    x0 %= n
    y0 %= n
    x1, y1 = (x0 + 1) % n, (y0 + 1) % n
    if a.ndim == 3:
        tx, ty = tx[..., None], ty[..., None]
    return (a[y0, x0] * (1 - tx) + a[y0, x1] * tx) * (1 - ty) + (a[y1, x0] * (1 - tx) + a[y1, x1] * tx) * ty


def warp(a, wx, wy, amount):
    n = a.shape[0]
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    return sample(a, xx + wx * amount, yy + wy * amount)


def s01(a, lo=-2.0, hi=2.0):
    return np.clip((a - lo) / (hi - lo), 0, 1)


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def mix(a, b, t):
    t = np.asarray(t, np.float32)
    if t.ndim == 2:
        t = t[..., None]
    return a * (1 - t) + b * t


def ramp(t, stops):
    """t in 0..1 through colour stops [(pos, '#hex'), ...]."""
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,), np.float32)
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        m = (t >= p0) & (t <= p1)
        u = ((t - p0) / max(p1 - p0, 1e-6))[m]
        out[m] = hexc(c0) * (1 - u[:, None]) + hexc(c1) * u[:, None]
    return out


def normal_map(h, strength):
    """Height (in 'pixels' of rise) to an OpenGL normal map (rows top first, v up)."""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5 * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5 * strength
    n = np.stack([-dx, dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * .5 + .5


def cavity(h, sigma, k):
    """Darkening where the surface is below its surroundings (painted-in occlusion)."""
    return np.clip(1 - k * np.clip(blur(h, sigma) - h, 0, None), 0, 1)


def brush(seed, scale=1.0):
    """Soft painterly strokes: stretched noise in a few directions, blended (the concept's brushwork)."""
    out = np.zeros((N, N), np.float32)
    for i, st in enumerate((3.0, 1.0)):
        out += fbm(seed + i, 3 * scale, 40 * scale, 1.2, stretch=st)
    return out / 2


# ------------------------------------------------------------------ materials
# Each returns (albedo rgb 0..1 sRGB, smoothness 0..1, height); the specs below set the tile, metal and normal strength.


def stone(seed, base, light, dark, warm, pits=70, chalk=.5, grain=1.0, cracks=0.0, warmth=.4):
    """Cream limestone (walls, copings, footings) / flagstone / shade stone, the concept's clean painted stone: big soft
    colour clouds (each block shows a different part of the tile, so blocks differ in tone), soft brushwork, a few
    shallow pits, a faint chalky bloom; `cracks` adds rare hairlines (the flags)."""
    big = fbm(seed, 1, 3, 1.2)
    mid = brush(seed + 10, .6)
    fine = fbm(seed + 20, 60, 260, .5)
    t = s01(.75 * big + .25 * mid, -2.0, 2.0)
    col = ramp(t, [(0, dark), (.3, base), (.72, light), (1, light)])
    col = mix(col, hexc(warm), warmth * smooth(s01(fbm(seed + 30, 1, 5, 1.2), 0.0, 1.8)))
    rng = np.random.default_rng(seed + 40)
    d1, _ = worley(seed + 40, pits)
    size = 3.0 + 4.0 * s01(fbm(seed + 41, 4, 12), -1.5, 1.5)
    pit = smooth(1 - d1 / size) * (rng.random() * 0 + 1)
    bloom = smooth(s01(fbm(seed + 43, 4, 20, 1.0), .8, 2.2))
    h = .9 * big + .5 * mid + .08 * fine * grain - 1.6 * pit
    crack = np.zeros_like(h)
    if cracks > 0:
        e1, e2 = worley(seed + 44, 24)
        crack = smooth(1 - (e2 - e1) / 1.8) * smooth(s01(fbm(seed + 45, 2, 8), 1.0, 1.8)) * cracks
        h -= 1.5 * crack
    col = col * (1 + .02 * fine[..., None] * grain)
    col = mix(col, hexc(dark) * .85, .45 * pit + .5 * crack)
    col = mix(col, np.minimum(hexc(light) * 1.06, 1), chalk * .3 * bloom)
    col *= cavity(h, 8, .03)[..., None]
    sm = .2 + .06 * s01(big) - .08 * pit
    return col, sm, h


def marble(seed):
    """White marble (no climb): soft grey veins running on a diagonal, a few fine branches (the concept's swatch)."""
    n = N
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    base = fbm(seed, 1, 6, 1.0)
    turb = fbm(seed + 1, 1, 8, 1.1)
    v1 = xx / n * 2 + yy / n * 3 + turb * .3          # whole cycles each way: wraps
    main = smooth(1 - np.abs(np.sin(math.pi * v1)) / .1)
    v2 = xx / n * 5 - yy / n * 2 + fbm(seed + 2, 2, 12, 1.1) * .25
    branch = smooth(1 - np.abs(np.sin(math.pi * v2)) / .025) * smooth(s01(fbm(seed + 3, 2, 8), .3, 1.5))
    veins = np.clip(main * (.55 + .45 * s01(fbm(seed + 4, 2, 10), -1, 1)) + .6 * branch, 0, 1)
    veins = blur(veins, 1.2)
    col = ramp(s01(base, -2, 2), [(0, "#DCD3CE"), (.5, "#EAE3DF"), (1, "#F4EFEB")])
    col = mix(col, hexc("#A39A96"), veins * .6)
    fine = fbm(seed + 5, 60, 250, .5)
    col *= (1 + .012 * fine)[..., None]
    h = .25 * base - .4 * veins + .05 * fine
    return col, .58 + .04 * s01(base) - .08 * veins, h


def slate(seed):
    """Grey roof slate (no blue: 승규's colour rule): layered flakes along the course, lighter worn flake edges."""
    layers = fbm(seed, 10, 120, .8, stretch=2.0)
    steps = np.floor(layers * 1.6) / 1.6
    edge = smooth(1 - np.abs(layers * 1.6 - np.round(layers * 1.6)) / .1)
    big = fbm(seed + 1, 1, 5, 1.0)
    fine = fbm(seed + 2, 60, 300, .5)
    t = s01(.65 * big + .35 * steps, -2, 2)
    col = ramp(t, [(0, "#61574F"), (.4, "#736860"), (.75, "#83776C"), (1, "#908478")])
    col = mix(col, hexc("#7A6A62"), .3 * s01(fbm(seed + 3, 2, 8), 0, 2))   # a warm brown cast in places
    col = mix(col, hexc("#9C948D"), .3 * edge)
    col *= (1 + .03 * fine)[..., None]
    h = .5 * steps + .3 * big + .1 * fine
    col *= cavity(h, 5, .05)[..., None]
    return col, .2 + .06 * s01(big) + .04 * edge, h


def wood(seed, stops, line, rings=46, knots=3, figure=1.0):
    """Painted plank wood, the grain along u: rings (v * rings + a stretched warp) drawn as soft dark strokes, a long
    colour drift, a few knots the rings swirl round, fine fibre streaks."""
    n = N
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    w = fbm(seed, 1, 6, 1.0, stretch=5.0) * .9 + fbm(seed + 1, 4, 20, 1.0, stretch=8.0) * .25
    g = yy / n * rings + w * 1.6 * figure
    rng = np.random.default_rng(seed + 2)
    for _ in range(knots):   # the rings swirl round each knot (its dark heart is drawn below)
        kx, ky, r = rng.random() * n, rng.random() * n, (14 + rng.random() * 16)
        dx = np.abs(xx - kx)
        dx = np.minimum(dx, n - dx)
        dy = np.abs(yy - ky)
        dy = np.minimum(dy, n - dy)
        g += 3.0 * np.exp(-(np.sqrt((dx / 2.5) ** 2 + dy ** 2) / (r * 2.2)) ** 2)
    f = g - np.floor(g)
    stroke = smooth(1 - np.abs(f - .5) / .2) * (.45 + .55 * s01(fbm(seed + 3, 8, 40, 1.0, stretch=10.0), -1.5, 1.5))
    early = smooth(s01(f, .1, .9))
    fibre = fbm(seed + 4, 40, 300, .4, stretch=14.0)
    drift = fbm(seed + 5, 1, 4, 1.0, stretch=3.0)
    col = ramp(s01(.6 * drift + .4 * (early * 2 - 1), -1.6, 1.6), stops)
    col = mix(col, hexc(line), np.clip(stroke * .75, 0, 1))
    hearts = np.zeros((n, n), np.float32)
    rng = np.random.default_rng(seed + 2)
    for _ in range(knots):
        kx, ky, r = rng.random() * n, rng.random() * n, (14 + rng.random() * 16)
        dx = np.abs(xx - kx)
        dx = np.minimum(dx, n - dx)
        dy = np.abs(yy - ky)
        dy = np.minimum(dy, n - dy)
        hearts = np.maximum(hearts, smooth(1 - np.sqrt((dx / 1.6) ** 2 + dy ** 2) / (r * .45)))
    col = mix(col, hexc(line) * .7, hearts * .85)
    col *= (1 + .06 * fibre)[..., None]
    h = -1.2 * stroke + .35 * fibre + .4 * drift - 1.5 * hearts
    col *= cavity(h, 3, .05)[..., None]
    return col, .3 + .06 * s01(drift) - .08 * stroke, h


def metal(seed, stops, dents=180, rust=None, scratch=.5):
    big = fbm(seed, 1, 6, 1.0)
    mid = fbm(seed + 1, 6, 40, 1.0)
    d1, _ = worley(seed + 2, dents)
    dent = smooth(1 - d1 / 22.0)
    scr = smooth(s01(fbm(seed + 3, 30, 200, .4, stretch=12.0), 1.6, 3.0))
    col = ramp(s01(.6 * big + .4 * mid, -2, 2), stops)
    if rust:
        col = mix(col, hexc(rust), .45 * smooth(s01(fbm(seed + 4, 2, 12, 1.0), .9, 2.4)))
    col = mix(col, hexc(stops[-1][1]) * 1.1, scratch * .4 * scr)
    h = .4 * big + .3 * mid - 1.4 * dent + .2 * scr
    col *= cavity(h, 5, .05)[..., None]
    return np.clip(col, 0, 1), .38 + .1 * s01(mid) + .1 * scr - .08 * dent, h


def weave(seed, threads, stops, coarse=0.0, slub=.5):
    """Canvas / burlap: over-under threads, `threads` a tile each way, slubs and soft folds."""
    n = N
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    jit = fbm(seed, 4, 30, 1.0) * coarse * 2.0
    u = xx / n * threads + jit * .15
    v = yy / n * threads + fbm(seed + 1, 4, 30, 1.0) * coarse * .3
    tu = .5 + .5 * np.cos(2 * math.pi * v)          # threads along u (their crests at whole v)
    tv = .5 + .5 * np.cos(2 * math.pi * u)
    over = (np.floor(u) + np.floor(v)) % 2
    hw = np.where(over > 0, tu * .8 + tv * .4, tv * .8 + tu * .4)
    sl = fbm(seed + 2, 10, 80, .6, stretch=6.0) * slub
    folds = fbm(seed + 3, 1, 5, 1.2)
    col = ramp(s01(.5 * folds + .3 * sl + .4 * (hw - .6), -1.8, 1.8), stops)
    h = hw * (1 + .3 * coarse) + .2 * sl + .6 * folds
    col *= cavity(h, 2, .25)[..., None]
    return col, .12 + .04 * s01(folds) + 0 * h, h


def straw(seed):
    n = N
    st = fbm(seed, 6, 200, .7, stretch=30.0)
    st2 = fbm(seed + 1, 6, 200, .7, stretch=24.0)
    col = ramp(s01(st, -2.2, 2.2), [(0, "#8E6A2C"), (.35, "#C49A45"), (.7, "#DDB862"), (1, "#F0D488")])
    col = mix(col, hexc("#A88A50"), .35 * smooth(s01(st2, 1, 2.5)))
    h = st + .6 * st2
    col *= cavity(h, 2, .08)[..., None]
    return col, .16 + 0 * h, h


def rope(seed):
    n = N
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    twist = (xx / n * 24 + yy / n * 48) % 1.0                          # strands round a rope along u
    strand = smooth(1 - np.abs(twist - .5) / .5)
    fib = fbm(seed, 30, 300, .5, stretch=4.0)
    col = ramp(s01(.7 * (strand * 2 - 1) + .3 * fib, -1.2, 1.2), [(0, "#5E4528"), (.45, "#9C7A4C"), (1, "#C9A675")])
    h = strand * 2 + .3 * fib
    return col, .14 + 0 * h, h


def leather(seed, stops):
    d1, d2 = worley(seed, 1600)
    cell = smooth((d2 - d1) / 2.0) * .6 + .4
    big = fbm(seed + 1, 1, 8, 1.0)
    col = ramp(s01(big, -2, 2), stops)
    col *= (.9 + .1 * cell)[..., None]
    h = cell * .6 + .4 * big
    return col, .34 + .1 * s01(big) + .06 * cell, h


def paint(seed, stops, wear="#5A3B22"):
    """Lacquered coach paint over wood: brush streaks along u, a little wear to the wood beneath."""
    br = fbm(seed, 2, 120, .8, stretch=10.0)
    big = fbm(seed + 1, 1, 5, 1.0)
    col = ramp(s01(.4 * br + .6 * big, -2, 2), stops)
    wr = smooth(s01(fbm(seed + 2, 6, 60, .8) + .5 * big, 2.2, 3.0))
    col = mix(col, hexc(wear), wr * .8)
    h = .15 * br + .2 * big - .8 * wr
    return col, .55 + .08 * s01(big) - .3 * wr, h


def velvet(seed, stops):
    crush = fbm(seed, 2, 30, 1.0, stretch=2.0)
    fine = fbm(seed + 1, 80, 300, .4)
    col = ramp(s01(crush, -2, 2), stops)
    col *= (1 + .04 * fine)[..., None]
    return col, .22 + .1 * s01(crush), .4 * crush + .1 * fine


# name: (slot, tile m, metallic, normal strength, builder) - the swatch each matches is RCY_08's bottom row
SPECS = {
    "RCY_Stone": ("RCY | stone", 2.0, 0.0, 2.2, lambda: stone(101, "#D7B68C", "#E8CDA6", "#C4A27A", "#E2AE7E", warmth=.25)),
    "RCY_Flag": ("RCY | flag", 2.4, 0.0, 2.0, lambda: stone(202, "#D3B08A", "#E2C6A2", "#C2A07C", "#DDA983", pits=60, chalk=.25, cracks=1.0, warmth=.15)),
    "RCY_Shade": ("RCY | shade", 2.0, 0.0, 2.2, lambda: stone(303, "#9C9089", "#AEA39B", "#7E736D", "#A28F84", chalk=.2)),
    "RCY_Marble": ("RCY | marble", 3.0, 0.0, 1.0, lambda: marble(404)),
    "RCY_Tile": ("RCY | tile", 1.5, 0.0, 2.5, lambda: slate(505)),
    "RCY_Oak": ("RCY | oak", 2.0, 0.0, 2.0, lambda: wood(606, [(0, "#BE7E44"), (.5, "#D29758"), (1, "#E4B172")], "#9A5E2E", rings=28)),
    "RCY_Walnut": ("RCY | walnut", 2.0, 0.0, 2.0, lambda: wood(707, [(0, "#683519"), (.5, "#844827"), (1, "#A05D36")], "#45210F", rings=26)),
    "RCY_Iron": ("RCY | iron", 1.0, .75, 1.8, lambda: metal(808, [(0, "#3D3532"), (.5, "#544A45"), (1, "#6E625B")], rust="#6E4A33")),
    "RCY_Brass": ("RCY | brass", 1.0, .9, 1.2, lambda: metal(909, [(0, "#7A5426"), (.5, "#A07234"), (1, "#C29450")], dents=120, rust="#5E3E1E", scratch=.6)),
    "RCY_Gold": ("RCY | gold", 1.0, 1.0, .8, lambda: metal(1010, [(0, "#A27628"), (.5, "#B98B36"), (1, "#CFA24C")], dents=60, scratch=.4)),
    "RCY_Cloth": ("RCY | cloth", 1.0, 0.0, 1.0, lambda: weave(1111, 160, [(0, "#A47A4A"), (.5, "#BC9262"), (1, "#D2AD7E")])),
    "RCY_Sack": ("RCY | sack", .8, 0.0, 1.6, lambda: weave(1212, 96, [(0, "#7E5E36"), (.5, "#9E7A4C"), (1, "#BC9868")], coarse=.4, slub=1.0)),
    "RCY_Straw": ("RCY | straw", 1.0, 0.0, 2.0, lambda: straw(1313)),
    "RCY_Rope": ("RCY | rope", .4, 0.0, 2.5, lambda: rope(1414)),
    "RCY_Leather": ("RCY | leather", .6, 0.0, 1.2, lambda: leather(1515, [(0, "#3E2214"), (.5, "#5B3320"), (1, "#73452B")])),
    "RCY_Velvet": ("RCY | velvet", .6, 0.0, .8, lambda: velvet(1616, [(0, "#5E1019"), (.5, "#801A26"), (1, "#9B2733")])),
    "RCY_Coach": ("RCY | coach", 1.5, 0.0, 1.0, lambda: paint(1717, [(0, "#2A3E24"), (.5, "#375231"), (1, "#486A3F")])),
}


# ------------------------------------------------------------------ the library from Codex's albedos (R686 승규 11-17
# "코덱스에게 모든 재질 텍스쳐를 받아 내"): textures/codex/b*/rcy_*.png, one square painted albedo a material. Each is made
# seamless (Moisan's periodic + smooth split: the smooth part that carries the edge mismatch is dropped), its broad
# lighting drift flattened, its mean colour pulled half-way to the plan's colour, then a height from its own detail
# gives the normal map and the smoothness. The procedural SPECS builders above are the fallback when a file is missing.

CODEX = os.path.join(HERE, "textures", "codex")

# name: (slot, codex file, tile m, metallic, normal strength, smoothness, smoothness from detail, target colour)
LIBRARY = {
    "RCY_Stone": ("RCY | stone", "rcy_cream_limestone.png", 2.0, 0.0, 2.0, .22, .06, "#D6B68C"),
    "RCY_Flag": ("RCY | flag", "rcy_flagstone.png", 2.4, 0.0, 1.8, .24, .06, "#C8A27C"),
    "RCY_Shade": ("RCY | shade", "rcy_shade_stone.png", 2.0, 0.0, 2.0, .2, .06, "#A1958D"),
    "RCY_Marble": ("RCY | marble", "rcy_white_marble.png", 3.0, 0.0, .8, .6, .05, "#E8E0DB"),
    "RCY_Tile": ("RCY | tile", "rcy_roof_slate.png", 1.5, 0.0, 2.2, .22, .08, "#766C65"),
    "RCY_Soil": ("RCY | soil", "rcy_soil.png", 2.0, 0.0, 2.0, .1, .04, "#A58A66"),
    "RCY_Oak": ("RCY | oak", "rcy_light_oak.png", 2.0, 0.0, 1.8, .3, .08, "#CA9054"),
    "RCY_Walnut": ("RCY | walnut", "rcy_walnut.png", 2.0, 0.0, 1.8, .32, .08, "#7C4424"),
    "RCY_Timber": ("RCY | timber", "rcy_pale_timber.png", 2.0, 0.0, 1.8, .2, .06, "#C4A374"),
    "RCY_Iron": ("RCY | iron", "rcy_dark_iron.png", 1.0, .75, 1.6, .42, .12, "#554A45"),
    "RCY_Brass": ("RCY | brass", "rcy_matte_brass.png", 1.0, .9, 1.0, .45, .1, "#9E7135"),
    "RCY_Gold": ("RCY | gold", "rcy_gold.png", 1.0, 1.0, .7, .5, .1, "#BE8F38"),
    "RCY_Coach": ("RCY | coach", "rcy_coach_lacquer.png", 1.5, 0.0, .8, .58, .06, "#375231"),
    "RCY_Cloth": ("RCY | cloth", "rcy_sand_canvas.png", 1.0, 0.0, 1.2, .14, .03, "#B68E60"),
    "RCY_Sack": ("RCY | sack", "rcy_burlap.png", .8, 0.0, 1.8, .12, .03, "#9A784B"),
    "RCY_Straw": ("RCY | straw", "rcy_straw.png", 1.0, 0.0, 2.0, .16, .04, "#C9A352"),
    "RCY_Rope": ("RCY | rope", "rcy_rope.png", .4, 0.0, 2.2, .14, .03, "#9C7B4F"),
    "RCY_Leather": ("RCY | leather", "rcy_leather.png", .6, 0.0, 1.2, .42, .08, "#59331F"),
    "RCY_Velvet": ("RCY | velvet", "rcy_velvet.png", .6, 0.0, .8, .25, .06, "#24361F"),
    # The horse: coat and hair neutral grey, Dressing.cs multiplies each coat's colour in (bay, grey, black).
    "RCY_Coat": ("RCY | coat", "rcy_horse_coat.png", .6, 0.0, .8, .35, .06, "#BDBDBD"),
    "RCY_Hair": ("RCY | hair", "rcy_horse_mane.png", .6, 0.0, 1.2, .3, .06, "#9A9A9A"),
    "RCY_Hoof": ("RCY | hoof", "rcy_hoof.png", .3, 0.0, 1.0, .5, .06, "#3A302A"),
    "RCY_Plume": ("RCY | plume", "rcy_plume.png", .4, 0.0, 1.0, .2, .03, "#F1E8D6"),
}


MID_CALM = {"RCY_Flag": .3}
TONE_SWING = {}   # see from_codex (tried on the paving: one tile is smaller than a slab, so the swing showed as a spot in every slab)
ISOTROPIC = {"RCY_Brass", "RCY_Gold"}   # no brushing direction (see from_codex)


def load_png(path):
    import bpy
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)[::-1, :, :3].copy()


def resample(a, n):
    """Square crop (centre) and area/bilinear resample to n x n."""
    h, w = a.shape[:2]
    s = min(h, w)
    a = a[(h - s) // 2:(h - s) // 2 + s, (w - s) // 2:(w - s) // 2 + s]
    if s == n:
        return a
    x = (np.arange(n) + .5) * s / n - .5
    x0 = np.clip(np.floor(x).astype(int), 0, s - 1)
    x1 = np.clip(x0 + 1, 0, s - 1)
    t = np.clip(x - x0, 0, 1)
    if s > 2 * n:   # pre-blur so the shrink does not alias
        k = s / n / 2
        a = np.stack([blur_any(a[..., c], k) for c in range(a.shape[2])], -1)
    rows = a[x0] * (1 - t)[:, None, None] + a[x1] * t[:, None, None]
    return rows[:, x0] * (1 - t)[None, :, None] + rows[:, x1] * t[None, :, None]


def blur_any(a, sigma):
    h, w = a.shape
    ky = np.fft.fftfreq(h)[:, None]
    kx = np.fft.rfftfreq(w)[None, :]
    g = np.exp(-2 * (math.pi * sigma) ** 2 * (kx ** 2 + ky ** 2))
    return np.fft.irfft2(np.fft.rfft2(a) * g, s=a.shape).astype(np.float32)


def periodic(u):
    """Moisan's periodic component of one channel: the image minus the smooth image that holds its edge jumps."""
    M, N = u.shape
    v = np.zeros_like(u)
    v[0, :] += u[-1, :] - u[0, :]
    v[-1, :] += u[0, :] - u[-1, :]
    v[:, 0] += u[:, -1] - u[:, 0]
    v[:, -1] += u[:, 0] - u[:, -1]
    q = np.arange(M)[:, None]
    r = np.arange(N)[None, :]
    den = 2 * np.cos(2 * math.pi * q / M) + 2 * np.cos(2 * math.pi * r / N) - 4
    den[0, 0] = 1
    s = np.fft.fft2(v) / den
    s[0, 0] = 0
    return (u - np.real(np.fft.ifft2(s))).astype(np.float32)


def edge_blend(col, band=.16):
    """Hides what the periodic split leaves of a pattern's seam (a weave or a strand out of phase at the edges): per axis,
    the edge strips take the tile shifted by half (whose own seam then lies in the middle, where the original shows), so
    the edges join the opposite edge's neighbours. Along x then y; each mask depends on one axis only."""
    n = col.shape[0]
    d = np.minimum(np.arange(n), n - 1 - np.arange(n)) / n
    m = smooth(d / band).astype(np.float32)
    col = col * m[None, :, None] + np.roll(col, n // 2, 1) * (1 - m)[None, :, None]
    return col * m[:, None, None] + np.roll(col, n // 2, 0) * (1 - m)[:, None, None]


def from_codex(name, size=N):
    slot, fname, tile, metal_, strength, smooth0, smooth_var, target = LIBRARY[name]
    path = None
    for b in sorted(os.listdir(CODEX)) if os.path.isdir(CODEX) else []:
        p = os.path.join(CODEX, b, fname)
        if os.path.exists(p):
            path = p
    if path is None:
        return None
    src = resample(load_png(path), size)
    col = edge_blend(np.stack([periodic(src[..., c]) for c in range(3)], -1))
    # Metals Codex brushed in one direction read as wood grain on a part whose U runs across (rcy-tex-movers-castle,
    # 11-17: the coach's brass medallions): the brushing is blurred out and an even hammered mottle put back.
    if name in ISOTROPIC:
        col = np.stack([blur(col[..., c], 5.0) for c in range(3)], -1)
        mottle = .6 * fbm(sum(map(ord, name)), 6, 40, 1.0) + .4 * fbm(sum(map(ord, name)) + 1, 40, 160, .6)
        col *= (1 + .07 * mottle)[..., None]
    # Flatten broad light drift (a gradient across the tile would repeat as stripes): divide by the luminance blurred
    # to a third of the tile, re-multiply by the mean.
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    broad = blur(lum, size / 6.0)
    col *= (lum.mean() / np.clip(broad, 1e-3, None))[..., None] ** .7
    # 70 % of the way to the plan's colour (Codex paints brighter and more saturated than the concept's swatches).
    tgt = hexc(target)
    col *= (1 + .7 * (tgt / np.clip(col.reshape(-1, 3).mean(0), 1e-3, None) - 1))[None, None, :]
    # Slab-to-slab tone (11-17, rcy-tex-canal-sorting: the paving read white and flat beside the concept's warm beige
    # with darker stones mixed in): a broad light-dark swing; each slab's random UV offset lands on another part of it.
    if name in MID_CALM:   # Codex's half-metre mottle read as dirt smudges on the paving: that band only, softened
        mid = np.stack([blur(col[..., c], 4.0) - blur(col[..., c], 120.0) for c in range(3)], -1)
        col = col - MID_CALM[name] * mid
    if name in TONE_SWING:
        swing = fbm(sum(map(ord, name)) + 7, 1, 1.4, 1.0)   # about one swing a tile: slabs differ, a slab stays even
        col *= (1 + TONE_SWING[name] * np.clip(swing, -2, 2))[..., None]
        col = mix(col, col * hexc("#B98E6A") / hexc("#D0A880"), .3 * smooth(s01(-swing, .8, 2.0)))
    col = np.clip(col, 0, 1)
    # Height from the albedo's own detail (the painted lights and darks), broad tones left out.
    lum = col @ np.array([.2126, .7152, .0722], np.float32)
    h = (lum - blur(lum, 12.0)) * 8 + (blur(lum, 3.0) - blur(lum, 24.0)) * 6
    detail = np.clip(.5 + (lum - blur(lum, 8.0)) * 4, 0, 1)
    sm = np.clip(smooth0 + smooth_var * (detail - .5) * 2, 0, 1)
    return col, sm, h, (slot, tile, metal_, strength, path)


def save_png(arr, path):
    """Writes the numbers as they are (rows top first) through a Blender image."""
    import bpy
    h, w = arr.shape[:2]
    if arr.shape[2] == 3:
        arr = np.concatenate([arr, np.ones((h, w, 1), np.float32)], 2)
    img = bpy.data.images.new("rcy_tex", w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def half(a):
    return a.reshape(a.shape[0] // 2, 2, a.shape[1] // 2, 2, -1).mean((1, 3))


def build(name, out, size=N):
    got = from_codex(name, size) if name in LIBRARY else None
    if got is not None:
        col, sm, h, (slot, tile, metal_, strength, source) = got
    elif name in SPECS:
        slot, tile, metal_, strength, fn = SPECS[name]
        col, sm, h = fn()
        source = "procedural"
    else:
        return None   # a Codex-only material whose image has not come yet
    alb = np.concatenate([np.clip(col, 0, 1), np.clip(sm, 0, 1)[..., None]], -1).astype(np.float32)
    nrm = normal_map(h, strength)
    while alb.shape[0] > size:
        alb, nrm = half(alb), half(nrm)
    save_png(alb, os.path.join(out, name + "_Albedo.png"))
    save_png(nrm, os.path.join(out, name + "_Normal.png"))
    rgb = alb[..., :3].reshape(-1, 3).mean(0)
    return {"slot": slot, "tile": tile, "metallic": metal_, "smoothness": float(alb[..., 3].mean()),
            "mean": "#%02X%02X%02X" % tuple(int(round(c * 255)) for c in rgb),
            "source": os.path.relpath(source, HERE).replace("\\", "/") if source != "procedural" else source}


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv and argv[0] else os.path.join(HERE, "textures", "library")
    names = argv[1].split(",") if len(argv) > 1 and argv[1] else list(dict.fromkeys(list(LIBRARY) + list(SPECS)))
    os.makedirs(out, exist_ok=True)
    info = {}
    for nm in names:
        got = build(nm, out)
        print("RCY_TEX", nm, got)
        if got:
            info[nm] = got
    path = os.path.join(out, "rcy_textures.json")
    old = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            old = json.load(f)
    old.update(info)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(old, f, indent=1)
