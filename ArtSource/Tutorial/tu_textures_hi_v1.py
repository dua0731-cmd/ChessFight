# Tutorial map, high-resolution maps for the outside of the castle and the trees (R834, 승규 11-25 "성 외곽을 더 디테일
# 하게 ... 고해상도로 나무는 특히 아주 정밀 해야 해"). numpy only, fixed seeds.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_textures_hi_v1.py
#
# Materials/Textures/
#   TU_Leaves_BaseColor.png    1024 RGBA: a spray of ~70 pointed leaves with midribs on transparent (leaf cards)
#   TU_Bark_BaseColor.png      512 x 1024, TU_Bark_Normal.png: furrowed bark running along v (2 m per repeat)
#   TU_Grass_BaseColor.png     2048, 4 x 4 m: meadow with blade streaks, clover, sun-dried patches (replaces the 1024 one)
#   TU_Road_BaseColor.png      1024, 3 x 3 m: rounded cobbles in sandy joints
#   TU_Rock_BaseColor.png      1024, 2 x 2 m: weathered limestone
import bpy, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tu_textures_v1 import value_noise, fbm, TEX   # noqa: E402  (tu_textures_v1's main() runs on import: guarded below)


def save(name, img):
    img = np.clip(img, 0, 1)
    h, w, c = img.shape
    if c == 3: img = np.concatenate([img, np.ones((h, w, 1))], axis=2)
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.pixels.foreach_set(img[::-1].astype(np.float32).ravel())
    im.filepath_raw = os.path.join(TEX, name + ".png"); im.file_format = 'PNG'
    im.save()
    print("TEX", name, w, h)


def normal_from_height(hgt, strength):
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * strength
    gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * strength
    n = np.stack([-gx, gy, np.ones_like(hgt)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * .5 + .5


def voronoi(n, cells, rng):
    """Tileable jittered-grid Voronoi: distance to nearest and second nearest site, and the nearest site's id."""
    jit = rng.random((cells, cells, 2))
    y, x = np.mgrid[0:n, 0:n] / n * cells
    ix = np.floor(x).astype(int); iy = np.floor(y).astype(int)
    d1 = np.full((n, n), 9.); d2 = np.full((n, n), 9.); idx = np.zeros((n, n), int)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx = ix + dx; cy = iy + dy
            j = jit[cy % cells, cx % cells]
            px = cx + j[..., 0]; py = cy + j[..., 1]
            d = np.hypot(x - px, y - py)
            closer = d < d1
            d2 = np.where(closer, d1, np.minimum(d2, d))
            idx = np.where(closer, (cy % cells) * cells + (cx % cells), idx)
            d1 = np.where(closer, d, d1)
    return d1, d2, idx


def leaves(rng, n=1024):
    img = np.zeros((n, n, 4))
    y, x = np.mgrid[0:n, 0:n] / n
    # leaves laid from the back of the spray to the front, pointing out from a stem low in the middle
    for k in range(110):
        ang = rng.uniform(-2.9, 2.9) * (.35 + .65 * k / 110)
        dist = rng.uniform(.04, .30)
        cx = .5 + np.sin(ang) * dist; cy = .58 - np.cos(ang) * dist
        L = rng.uniform(.10, .16); W = L * rng.uniform(.38, .5)
        a = ang + rng.uniform(-.35, .35)
        tx, ty = cx + np.sin(a) * L, cy - np.cos(a) * L
        if not (.03 < tx < .97 and .03 < ty < .97 and .03 < cx < .97 and .03 < cy < .97): continue
        u = (x - cx) * np.sin(a) - (y - cy) * np.cos(a)    # along the leaf, from its base outward
        v = (x - cx) * np.cos(a) + (y - cy) * np.sin(a)    # across
        t = u / L
        inside = (t > 0) & (t < 1)
        half = W * .5 * np.sin(np.clip(t, 0, 1) * np.pi) ** .8 * (1 - .25 * t)
        mask = inside & (np.abs(v) < half)
        if not mask.any(): continue
        shade = rng.uniform(.75, 1.15)
        base = np.array([.16, .36, .07]) * shade + rng.uniform(-.03, .03, 3)
        tip = base * np.array([1.25, 1.2, 1.0]) + np.array([.04, .05, .0])
        col = base[None, None, :] * (1 - t[..., None]) + tip[None, None, :] * t[..., None]
        side = np.clip(v / np.maximum(half, 1e-4), -1, 1)
        col = col * (1 - .18 * side[..., None])                      # one half catches more light
        rib = np.abs(v) < W * .035
        col = np.where(rib[..., None], col * 1.35 + .03, col)
        veins = np.abs(np.sin((t * 7 + np.abs(v) / W * 4) * np.pi)) < .12
        col = np.where(veins[..., None] & ~rib[..., None], col * 1.12, col)
        img[mask, :3] = col[mask]; img[mask, 3] = 1.
    # the twig
    # transparent texels carry the leaves' green, so mipmaps and filtering never fringe the cards in black
    empty = img[..., 3] < .5
    img[empty, :3] = (.17, .34, .08)
    for s in np.linspace(0, 1, 400):
        px = int((.5 + .01 * np.sin(s * 6)) * n); py = int((.99 - s * .45) * n)
        img[max(0, py - 3):py + 3, max(0, px - 3):px + 3] = (.22, .14, .07, 1.)
    return img


def main():
    rng = np.random.default_rng(83420)
    save("TU_Leaves_BaseColor", leaves(rng))

    # bark: long furrows round the trunk (u) running along it (v), 512 x 1024
    w, h = 512, 1024
    f1 = fbm(1024, rng, 4, 5)[:h, :w]
    y, x = np.mgrid[0:h, 0:w] / np.array([h, w])[:, None, None]
    ridges = np.abs(np.sin((x * 14 + f1 * 2.2 + np.sin(y * 9) * .15) * np.pi))
    hgt = ridges ** .6 * (.75 + .25 * fbm(1024, rng, 16, 4)[:h, :w])
    col = np.array([.30, .21, .14])[None, None, :] * (.55 + .6 * hgt[..., None])
    col += (fbm(1024, rng, 6, 4)[:h, :w, None] - .5) * np.array([.06, .07, .04])
    save("TU_Bark_BaseColor", col)
    save("TU_Bark_Normal", normal_from_height(hgt, 3.))

    # meadow 2048 over 4 m: streaky blades in four directions, clover, dry patches
    n = 2048
    g = np.zeros((n, n))
    for k in range(4):
        sx, sy = [(96, 12), (12, 96), (70, 70), (70, 70)][k]
        nn = fbm(n, rng, 8, 3)
        a = value_noise(n, sx, rng) if k < 2 else value_noise(n, 64, rng)
        g += a * .25 + nn * .1
    blades = value_noise(n, 512, rng) * .6 + value_noise(n, 256, rng) * .4
    big = fbm(n, rng, 3, 5)
    col = np.array([.25, .45, .14])[None, None, :] * (.7 + .5 * g[..., None] * .6 + .35 * blades[..., None])
    col *= (.85 + .3 * big[..., None])
    dry = np.clip((big - .6) * 3, 0, 1)
    col = col * (1 - .45 * dry[..., None]) + np.array([.55, .52, .25]) * .45 * dry[..., None]
    d1, d2, _ = voronoi(n, 96, rng)
    clover = (d1 < .16) & (value_noise(n, 12, rng) > .6)
    col = np.where(clover[..., None], col * np.array([.85, 1.1, .85]), col)
    flowers = (d1 < .05) & (value_noise(n, 20, rng) > .72)
    col = np.where(flowers[..., None], np.array([.95, .93, .85]), col)
    save("TU_Grass_BaseColor", col)

    # cobbles 1024 over 3 m
    n = 1024
    d1, d2, idx = voronoi(n, 18, rng)
    edge = d2 - d1
    stone = np.clip(edge * 6, 0, 1) ** .5
    tint = rng.uniform(.82, 1.12, 18 * 18)[idx]
    col = np.array([.70, .63, .52])[None, None, :] * (tint * (.75 + .25 * stone))[..., None]
    col *= (.9 + .2 * fbm(n, rng, 10, 4))[..., None]
    joint = edge < .06
    col = np.where(joint[..., None], np.array([.55, .47, .34]) * (.8 + .4 * fbm(n, rng, 20, 3))[..., None], col)
    save("TU_Road_BaseColor", col)

    # limestone rock 1024 over 2 m
    f = fbm(n, rng, 4, 6)
    d1, d2, _ = voronoi(n, 7, rng)
    crack = np.clip(1 - (d2 - d1) * 14, 0, 1)
    col = np.array([.66, .62, .55])[None, None, :] * (.7 + .45 * f[..., None])
    col *= (1 - .45 * crack[..., None])
    moss = np.clip((fbm(n, rng, 5, 4) - .58) * 4, 0, 1)
    col = col * (1 - .5 * moss[..., None]) + np.array([.22, .32, .12]) * .5 * moss[..., None]
    save("TU_Rock_BaseColor", col)

    meadow_extras(np.random.default_rng(83430))


def blades(rng, n=512):
    """Grass tuft card: ~140 curved blades from the bottom edge, darker at the foot, some seed heads and small flowers."""
    img = np.zeros((n, n, 4))
    img[..., :3] = (.24, .40, .12)           # transparent texels keep the green (no dark fringes)
    for k in range(140):
        x0 = rng.uniform(.06, .94); hgt = rng.uniform(.35, .95); lean = rng.uniform(-.25, .25)
        w0 = rng.uniform(.010, .022)
        tone = rng.uniform(.8, 1.2); dry = rng.random() < .18
        base = np.array([.16, .30, .07]) * tone
        tip = (np.array([.55, .55, .25]) if dry else np.array([.40, .62, .20])) * tone
        steps = int(hgt * n * 1.2)
        for s in range(steps):
            t = s / steps
            y = 1 - t * hgt
            x = x0 + lean * t * t
            w = w0 * (1 - t) ** .7 + .0015
            px0 = int((x - w) * n); px1 = int((x + w) * n) + 1; py = int(y * n) - 1
            if py < 0 or py >= n: continue
            px0 = max(px0, 0); px1 = min(px1, n)
            if px1 <= px0: continue
            c = base * (1 - t) + tip * t
            img[py, px0:px1, :3] = c; img[py, px0:px1, 3] = 1.
        if rng.random() < .12:               # a flower or a seed head at the tip
            cx = int((x0 + lean) * n); cy = int((1 - hgt) * n)
            col = [(.95, .93, .85), (.95, .82, .25), (.62, .45, .85), (.75, .62, .35)][int(rng.integers(0, 4))]
            r = int(rng.uniform(4, 8))
            yy, xx = np.mgrid[-r:r + 1, -r:r + 1]
            m = xx * xx + yy * yy <= r * r
            ys = np.clip(cy + yy, 0, n - 1); xs = np.clip(cx + xx, 0, n - 1)
            img[ys[m], xs[m], :3] = col; img[ys[m], xs[m], 3] = 1.
    return img


def meadow_extras(rng):
    save("TU_GrassBlades_BaseColor", blades(rng))
    # macro variation, 512 over 96 m (the meadow shader tints by it: lush, yellowed, shaded)
    n = 512
    m = np.stack([fbm(n, rng, 3, 5), fbm(n, rng, 5, 4), fbm(n, rng, 2, 3)], -1)
    save("TU_MeadowMacro", m)
    # a normal map for the detail grass (4 m): fine blade bumps
    h = value_noise(1024, 256, rng) * .5 + value_noise(1024, 128, rng) * .3 + fbm(1024, rng, 32, 3) * .2
    save("TU_Grass_Normal", normal_from_height(h, 2.5))


if __name__ == "__main__":
    main()
