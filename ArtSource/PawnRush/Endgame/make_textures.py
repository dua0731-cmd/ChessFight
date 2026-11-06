"""Textures for the war room (brief §9.6-9.7), all seamless and packed for URP Lit.

    python make_textures.py [<out dir>]        (numpy + Pillow; default: ./Textures)

Per material family (512 px/m on walked faces and devices; the hall reuses them at 256 px/m by tiling twice as far):
    T_EG_<Name>_BaseColor.png         sRGB, no light baked in
    T_EG_<Name>_MetallicSmoothness.png  R = metallic, A = smoothness (1 - roughness)   -> _MetallicGlossMap
    T_EG_<Name>_Normal.png            tangent space, OpenGL (Y+), like Unity            -> _BumpMap
    T_EG_<Name>_Occlusion.png         G = occlusion                                     -> _OcclusionMap
    T_EG_Lamp_Emission.png            the lamp glass                                    -> _EmissionMap
Trim sheet T_EG_Trim_* (2048, eight 256 px bands: gold, gold worn, teal, teal panel, walnut, stone, iron, map strip)
and the decal atlas T_EG_Decals_BaseColor.png (1024, RGBA: compass, rook crest, double chevron, crown ring, map print).

The patterns are the procedural masters' (eg_bl_materials.py) rebuilt from periodic noise, so every tile wraps with no
seam; the Blender materials stay the look reference for renders.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "Textures")
RNG = np.random.default_rng(7)


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def periodic_noise(n, beta=2.0, seed=0, stretch=(1.0, 1.0)):
    """Tileable fractal noise in [0, 1]: white noise shaped by 1 / f^beta in the Fourier domain."""
    rng = np.random.default_rng(seed)
    w = rng.standard_normal((n, n))
    f = np.fft.fft2(w)
    ky = np.fft.fftfreq(n)[:, None] * stretch[1]
    kx = np.fft.fftfreq(n)[None, :] * stretch[0]
    k = np.sqrt(kx * kx + ky * ky)
    k[0, 0] = 1.0
    f = f / k ** (beta / 2.0)
    f[0, 0] = 0
    r = np.real(np.fft.ifft2(f))
    r -= r.min()
    return r / max(1e-9, r.max())


def grid(n):
    v, u = np.meshgrid(np.arange(n) / n, np.arange(n) / n, indexing="ij")
    return u, v


def normal_from_height(h, strength):
    """Tangent-space normal, OpenGL (green up): rows run down the image, so v up = -row."""
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = -(np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength, -dy * strength, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / ln, ny / ln, nz / ln], axis=-1) * 0.5 + 0.5


def occlusion_from_height(h, strength=0.6):
    blur = h
    for s in (2, 4, 8):
        blur = (blur + np.roll(blur, s, 0) + np.roll(blur, -s, 0) + np.roll(blur, s, 1) + np.roll(blur, -s, 1)) / 5.0
    ao = 1.0 - np.clip((blur - h) * strength * 8.0, 0.0, 0.6)
    return ao


def save_rgb(arr, path, srgb=True):
    a = np.clip(arr, 0.0, 1.0)
    Image.fromarray((a * 255.0 + 0.5).astype(np.uint8), "RGB").save(path, optimize=True)


def save_rgba(arr, path):
    a = np.clip(arr, 0.0, 1.0)
    Image.fromarray((a * 255.0 + 0.5).astype(np.uint8), "RGBA").save(path, optimize=True)


def write_set(name, base, rough, metal, height, nstrength, n):
    os.makedirs(OUT, exist_ok=True)
    save_rgb(base, os.path.join(OUT, f"T_EG_{name}_BaseColor.png"))
    ms = np.zeros((n, n, 4))
    ms[..., 0] = metal
    ms[..., 3] = 1.0 - rough
    save_rgba(ms, os.path.join(OUT, f"T_EG_{name}_MetallicSmoothness.png"))
    save_rgb(normal_from_height(height, nstrength), os.path.join(OUT, f"T_EG_{name}_Normal.png"), srgb=False)
    ao = occlusion_from_height(height)
    occ = np.stack([ao, ao, ao], axis=-1)
    save_rgb(occ, os.path.join(OUT, f"T_EG_{name}_Occlusion.png"), srgb=False)
    return name


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a * (1.0 - t) + b * t


def marble(n=2048):
    """Cream marble, a 4 m tile: soft clouds, thin warm-grey veins, faint gold glints."""
    u, v = grid(n)
    warp = periodic_noise(n, 2.2, 11)
    warp2 = periodic_noise(n, 2.6, 12)
    band = np.sin(2 * math.pi * (3 * u + 2 * v) + 9.0 * (warp - 0.5) + 4.0 * (warp2 - 0.5))
    vein = np.clip(1.0 - np.abs(band) / 0.06, 0, 1) ** 1.5
    fine = periodic_noise(n, 1.4, 13)
    cloud = periodic_noise(n, 2.8, 14)
    base = mix(hexrgb("#E6D9C2")[None, None, :] * np.ones((n, n, 3)), hexrgb("#EFE6D4"), np.clip(cloud * 1.4 - 0.2, 0, 1))
    base = mix(base, hexrgb("#B8AA90"), vein * 0.85)
    base = base * (0.985 + 0.03 * fine[..., None])
    rough = 0.3 + 0.08 * fine + 0.06 * vein
    height = cloud * 0.3 - vein * 0.4 + fine * 0.1
    return write_set("Marble_Cream", base, rough, np.zeros((n, n)), height, 2.0, n)


def walnut(n=1024):
    """Walnut, a 2 m tile, grain along v."""
    u, v = grid(n)
    warp = periodic_noise(n, 2.4, 21, stretch=(1.0, 0.25))
    rings = np.sin(2 * math.pi * (14 * u + 1.5 * (warp - 0.5) * 4.0))
    grain = periodic_noise(n, 1.2, 22, stretch=(6.0, 0.4))
    t = np.clip(rings * 0.5 + 0.5, 0, 1) ** 2.0
    base = mix(hexrgb("#6A4729")[None, None, :] * np.ones((n, n, 3)), hexrgb("#7A5432"), t)
    base = base * (0.92 + 0.12 * grain[..., None])
    rough = 0.45 + 0.1 * grain
    height = t * 0.4 + grain * 0.3
    return write_set("Walnut", base, rough, np.zeros((n, n)), height, 1.5, n)


def teal(n=1024):
    """Teal paint over wood, a 2 m tile: soft brush noise, a few worn specks."""
    noise = periodic_noise(n, 2.0, 31)
    speck = periodic_noise(n, 0.6, 32)
    wear = np.clip((speck - 0.82) * 8.0, 0, 1)
    base = mix(hexrgb("#2F6B70")[None, None, :] * np.ones((n, n, 3)) * (0.92 + 0.12 * noise[..., None]), hexrgb("#6E9A98"), wear * 0.6)
    rough = 0.38 + 0.08 * noise + 0.1 * wear
    height = noise * 0.2 - wear * 0.3
    return write_set("Teal", base, rough, np.zeros((n, n)), height, 1.0, n)


def gold(n=1024):
    """Gold and brass, a 2 m tile: brushed along u, brighter wear specks."""
    brushed = periodic_noise(n, 1.6, 41, stretch=(0.15, 3.0))
    speck = periodic_noise(n, 0.8, 42)
    wear = np.clip((speck - 0.78) * 6.0, 0, 1)
    base = mix(hexrgb("#D2A63E")[None, None, :] * np.ones((n, n, 3)) * (0.94 + 0.1 * brushed[..., None]), hexrgb("#F2D27A"), wear * 0.5)
    rough = 0.22 + 0.08 * brushed - 0.06 * wear
    height = brushed * 0.15
    return write_set("Gold", base, rough, np.ones((n, n)), height, 0.6, n)


def stone(n=1024):
    """Pale stone (smooth: walls nobody climbs), a 2 m tile."""
    noise = periodic_noise(n, 2.2, 51)
    fine = periodic_noise(n, 1.2, 52)
    base = mix(hexrgb("#D4C7AE")[None, None, :] * np.ones((n, n, 3)), hexrgb("#E3D7C0"), np.clip(noise * 1.3 - 0.15, 0, 1))
    base = base * (0.97 + 0.06 * fine[..., None])
    rough = 0.55 + 0.1 * fine
    height = noise * 0.3 + fine * 0.15
    return write_set("Stone", base, rough, np.zeros((n, n)), height, 1.2, n)


def iron(n=1024):
    noise = periodic_noise(n, 1.8, 61)
    base = hexrgb("#2E2C2A")[None, None, :] * np.ones((n, n, 3)) * (0.85 + 0.3 * noise[..., None])
    rough = 0.4 + 0.15 * noise
    return write_set("Iron", base, rough, np.full((n, n), 0.85), noise * 0.2, 0.8, n)


def map_paper(n=2048):
    """The campaign map print (a 4 m tile): parchment, grey-green land, ink coasts, a gold grid every 0.5 m."""
    u, v = grid(n)
    land = periodic_noise(n, 2.6, 71)
    coast = np.clip(1.0 - np.abs(land - 0.55) / 0.006, 0, 1)
    paper = periodic_noise(n, 1.8, 72)
    base = mix(hexrgb("#D8C9A3")[None, None, :] * np.ones((n, n, 3)) * (0.95 + 0.08 * paper[..., None]),
               hexrgb("#A9B9A2"), (land > 0.55).astype(float))
    base = mix(base, hexrgb("#6F8E80"), coast * 0.9)
    gu = np.abs(((u * 8) % 1.0) - 0.5) > 0.494
    gv = np.abs(((v * 8) % 1.0) - 0.5) > 0.494
    base = mix(base, hexrgb("#C9A44E"), (gu | gv).astype(float) * 0.8)
    rough = 0.72 + 0.06 * paper
    return write_set("MapPaper", base, rough, np.zeros((n, n)), paper * 0.1, 0.5, n)


def banner(n=1024):
    weave = (np.sin(np.arange(n)[None, :] * math.pi / 2) * np.sin(np.arange(n)[:, None] * math.pi / 2)) * 0.5 + 0.5
    noise = periodic_noise(n, 2.0, 81)
    base = hexrgb("#34507E")[None, None, :] * np.ones((n, n, 3)) * (0.9 + 0.12 * noise[..., None] + 0.04 * weave[..., None])
    rough = 0.85 + 0.05 * weave
    return write_set("BannerNavy", base, rough, np.zeros((n, n)), weave * 0.15, 0.8, n)


def lamp(n=256):
    u, v = grid(n)
    glow = np.clip(1.0 - np.hypot(u - 0.5, v - 0.5) * 1.6, 0, 1)
    col = mix(hexrgb("#FFB25E")[None, None, :] * np.ones((n, n, 3)), hexrgb("#FFE3B0"), glow)
    os.makedirs(OUT, exist_ok=True)
    save_rgb(col, os.path.join(OUT, "T_EG_Lamp_Emission.png"))
    save_rgb(col * 0.9, os.path.join(OUT, "T_EG_Lamp_Emissive_BaseColor.png"))


def trim_sheet(n=2048):
    """Eight horizontal bands of 256 px (0.5 m at 512 px/m), each wrapping along u."""
    bands = [("gold", "#D2A63E", 1.0, 0.24), ("gold_worn", "#C69A3A", 1.0, 0.32), ("teal", "#2F6B70", 0.0, 0.4),
             ("teal_panel", "#2F6B70", 0.0, 0.38), ("walnut", "#7A5432", 0.0, 0.48), ("stone", "#E3D7C0", 0.0, 0.58),
             ("iron", "#2E2C2A", 0.85, 0.42), ("map", "#D8C9A3", 0.0, 0.72)]
    h = n // len(bands)
    base = np.zeros((n, n, 3))
    rough = np.zeros((n, n))
    metal = np.zeros((n, n))
    height = np.zeros((n, n))
    for i, (name, hx, m, r) in enumerate(bands):
        sl = slice(i * h, (i + 1) * h)
        noise = periodic_noise(n, 1.6, 100 + i, stretch=(0.2, 2.0))[:h, :]
        col = hexrgb(hx)[None, None, :] * (0.92 + 0.12 * noise[..., None])
        prof = np.zeros(h)
        y = np.arange(h) / h
        if name in ("gold", "gold_worn", "teal_panel"):
            prof = np.clip(1.0 - np.abs(y - 0.5) / 0.42, 0, 1) ** 0.6       # a bevelled moulding across the band
        elif name == "walnut":
            prof = 0.5 + 0.5 * np.sin(2 * math.pi * y)
        if name == "teal_panel":
            edge = (np.abs(y - 0.12) < 0.02) | (np.abs(y - 0.88) < 0.02)
            col[edge, :, :] = hexrgb("#D2A63E")
        base[sl] = col
        rough[sl] = r + 0.05 * noise
        metal[sl] = m
        height[sl] = prof[:, None] * 0.8 + noise * 0.1
    return write_set("Trim", base, rough, metal, height, 3.0, n)


def decals(n=1024):
    """RGBA atlas, 4 x 4 cells of 256 px: compass (2 x 2), rook crest (2 x 2), chevrons (2 x 1), crown ring (2 x 1),
    map print (2 x 2)."""
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gold = (210, 166, 62, 255)
    teal = (47, 107, 112, 255)
    navy = (52, 80, 126, 255)
    cream = (239, 230, 212, 255)
    # compass rose, cells (0..1, 0..1)
    cx, cy, R = 256, 256, 240
    d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=gold, width=14)
    d.ellipse([cx - R + 24, cy - R + 24, cx + R - 24, cy + R - 24], fill=(216, 201, 163, 255))
    for k in range(8):
        a = k * math.pi / 4
        L = 220 if k % 2 == 0 else 140
        w = 34 if k % 2 == 0 else 22
        tip = (cx + L * math.cos(a), cy - L * math.sin(a))
        s1 = (cx + w * math.cos(a + math.pi / 2), cy - w * math.sin(a + math.pi / 2))
        s2 = (cx + w * math.cos(a - math.pi / 2), cy - w * math.sin(a - math.pi / 2))
        d.polygon([(cx, cy), s1, tip], fill=gold)
        d.polygon([(cx, cy), s2, tip], fill=teal)
    # rook crest, cells (2..3, 0..1)
    ox = 512
    d.polygon([(ox + 40, 30), (ox + 472, 30), (ox + 472, 330), (ox + 256, 490), (ox + 40, 330)], fill=navy, outline=gold, width=12)
    d.rectangle([ox + 170, 340, ox + 342, 380], fill=gold)
    d.polygon([(ox + 196, 340), (ox + 316, 340), (ox + 300, 170), (ox + 212, 170)], fill=gold)
    d.rectangle([ox + 186, 130, ox + 326, 170], fill=gold)
    for x in (186, 238, 290):
        d.rectangle([ox + x, 90, ox + x + 36, 130], fill=gold)
    # double chevron, cells (0..1, 2)
    oy = 512
    for dx in (60, 230):
        d.polygon([(dx, oy + 40), (dx + 90, oy + 40), (dx + 190, oy + 128), (dx + 90, oy + 216), (dx, oy + 216), (dx + 100, oy + 128)], fill=gold)
    # crown ring segment, cells (0..1, 3)
    oy = 768
    d.rectangle([0, oy + 150, 512, oy + 190], fill=gold)
    for x in range(20, 512, 120):
        d.polygon([(x, oy + 150), (x + 80, oy + 150), (x + 80, oy + 90), (x + 60, oy + 110), (x + 40, oy + 70), (x + 20, oy + 110), (x, oy + 90)], fill=gold)
    # map print, cells (2..3, 2..3): reuse the map texture's look at 512 px
    m = periodic_noise(512, 2.6, 171)
    land = (m > 0.55)
    tile = np.zeros((512, 512, 4), dtype=np.uint8)
    tile[..., :3] = np.where(land[..., None], [169, 185, 162], [216, 201, 163])
    tile[..., 3] = 255
    coast = np.abs(m - 0.55) < 0.006
    tile[coast, :3] = [111, 142, 128]
    tile[::64, :, :3] = [201, 164, 78]
    tile[:, ::64, :3] = [201, 164, 78]
    img.paste(Image.fromarray(tile, "RGBA"), (512, 512))
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, "T_EG_Decals_BaseColor.png"), optimize=True)


def report():
    rows = []
    for f in sorted(os.listdir(OUT)):
        if f.endswith(".png"):
            with Image.open(os.path.join(OUT, f)) as im:
                rows.append((f, im.size, im.mode))
    return rows


if __name__ == "__main__":
    for fn in (marble, walnut, teal, gold, stone, iron, map_paper, banner, lamp, trim_sheet, decals):
        fn()
        print("[textures]", fn.__name__, flush=True)
    for r in report():
        print(r)
