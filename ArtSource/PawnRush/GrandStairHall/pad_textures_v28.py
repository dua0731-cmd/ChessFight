# The two pads' textures (승규 11-03 "발판도 텍스쳐 입혀", after the assets' own previews), drawn from their relief
# (pads_v28.py -- raster: textures/<Pad>_height.npy, 1024 square, row 0 = the pad's -y, so flipped on saving). For each
# pad, into Assets/Resources/PawnRushGrandStairHall:
#   <Pad>_Top.png       albedo of the top (UV straight down)        <Pad>_Side.png       albedo round the sides
#   <Pad>_Top_Emit.png  what glows (orange), black elsewhere         <Pad>_Side_Emit.png
#   <Pad>_Top_MS.png    R metallic, A smoothness (Lit's metallic map) <Pad>_Side_MS.png
# The heights, measured (m, the deck/panel top at about 0):
#   floating pad  outer lip -0.30 (d < 15 px) | border groove -0.008 .. -0.011 (d 15 .. 55) | panel rim -0.002 |
#                 panel field -0.005 | the cross seams between the four panels -0.013 (about 20 px)
#   jump pad      flange -0.099 (d < 60) | rim frame +0.007 (60 .. 88) | groove -0.099 (88 .. 112) | flagstones
#                 0 .. +0.003 with their joints a few mm lower | the arrow +0.036 .. +0.047
# Run: python pad_textures_v28.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
TEX = os.path.join(HERE, "textures")
N = 1024
rng = np.random.default_rng(328)

CREAM, GROUT = np.array([238, 224, 198.]), np.array([150, 104, 62.])
GOLD, GOLD_DK = np.array([222, 168, 78.]), np.array([150, 102, 40.])
SLATE, SLATE_RIM = np.array([58, 50, 72.]), np.array([196, 186, 172.])
GLOW, GLOW_EMIT = np.array([255, 168, 70.]), np.array([255, 128, 30.])
BASE = np.array([46, 38, 34.])


def blur(a, r):
    """A box blur of radius r (NaN-safe: NaN read as the mean)."""
    a = np.where(np.isnan(a), np.nanmean(a), a)
    for axis in (0, 1):
        c = np.cumsum(np.pad(a, [(r + 1, r) if i == axis else (0, 0) for i in range(2)], mode="edge"), axis=axis)
        a = (np.take(c, range(2 * r + 1, c.shape[axis]), axis=axis) - np.take(c, range(0, c.shape[axis] - 2 * r - 1), axis=axis)) / (2 * r + 1)
    return a


def tile_noise(scale):
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    m = np.exp(-((fx ** 2 + fy ** 2) * scale ** 2))
    n = np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((N, N))) * m))
    return (n - n.mean()) / (n.std() + 1e-9)


def save(name, rgb, alpha=None, flip=True):
    a = np.clip(rgb, 0, 255).astype(np.uint8)
    if flip:
        a = np.flipud(a)
    if alpha is not None:
        al = np.clip(alpha * 255, 0, 255).astype(np.uint8)
        a = np.dstack([a, np.flipud(al) if flip else al])
        Image.fromarray(a, "RGBA").save(os.path.join(RES, name + ".png"))
    else:
        Image.fromarray(a, "RGB").save(os.path.join(RES, name + ".png"))
    print("PNG", name, a.shape)


def paint(masks, layers, shape):
    out = np.zeros(shape + (3,))
    for m, c in zip(masks, layers):
        out = np.where(m[..., None], c if np.ndim(c) == 3 else np.broadcast_to(c, shape + (3,)), out)
    return out


yy, xx = np.mgrid[0:N, 0:N]
d = np.minimum(np.minimum(xx, yy), np.minimum(N - 1 - xx, N - 1 - yy))
grain = tile_noise(3)                       # a little speckle so nothing reads flat
cloud = tile_noise(120)

# ------------------------------------------------------------------ floating pad (solar panel cube), top
# The four panels as measured on the height map (its seams and border groove; the panels' own heights differ by a few
# mm, so the regions are drawn as squares rather than read off the heights): x 59 .. 511 and 531 .. 975, y 58 .. 519
# and 540 .. 985, each with a raised rim 22 px wide.
PX, PY, RIM = ((59, 511), (531, 975)), ((58, 519), (540, 985)), 22
lip = d < 15
panel = np.zeros((N, N), bool); inner = np.zeros((N, N), bool)
u = np.zeros((N, N)); v = np.zeros((N, N))
for x0, x1 in PX:
    for y0, y1 in PY:
        inside = (xx >= x0) & (xx <= x1) & (yy >= y0) & (yy <= y1)
        panel |= inside
        inner |= inside & (xx >= x0 + RIM) & (xx <= x1 - RIM) & (yy >= y0 + RIM) & (yy <= y1 - RIM)
        u = np.where(inside, (xx - x0) / (x1 - x0), u); v = np.where(inside, (yy - y0) / (y1 - y0), v)
groove = (~lip) & (~panel)
rim = panel & ~inner
field = inner
dark_quad = ((xx < N / 2) & (yy >= N / 2)) | ((xx >= N / 2) & (yy < N / 2))
cells = (np.abs(((u - .08) / .84 * 4) % 1 - .5) > .47) | (np.abs(((v - .08) / .84 * 4) % 1 - .5) > .47)
tile_shade = 1 + .03 * grain + .04 * cloud
cream_field = np.where(cells[..., None], GROUT * 1.15, CREAM * tile_shade[..., None])
slate_field = SLATE * (1 + .05 * cloud + .03 * grain)[..., None]
albedo = paint([lip, groove, rim & dark_quad, rim & ~dark_quad, field & dark_quad, field & ~dark_quad],
               [GOLD, GLOW, SLATE_RIM, GOLD, slate_field, cream_field], (N, N))
emit = np.where(groove[..., None], GLOW_EMIT, 0) + np.where((field & ~dark_quad & cells)[..., None], GLOW_EMIT * .35, 0)
metal = np.where(lip | (rim & ~dark_quad), 1.0, 0.0)
smooth = np.select([lip | rim, groove, field & dark_quad], [.75, .55, .7], .45)
save("FloatingPad_Top", albedo)
save("FloatingPad_Top_Emit", emit)
save("FloatingPad_Top_MS", np.dstack([metal * 255] * 3), smooth)

# ------------------------------------------------------------------ jump pad (glowing platform), top
H = np.load(os.path.join(TEX, "JumpPad_height.npy"))
h = blur(H, 1)
low = h < -.05
flange = low & (d < 62)
ring = low & (d >= 62)
frame = (~low) & (d < 95)
arrow = (~low) & (d >= 95) & (h > .02)
stone = (~low) & (d >= 95) & (~arrow)
joint = stone & (h < blur(H, 7) - .0025)
stone_tone = 1 + .05 * cloud + .03 * grain
albedo = paint([flange, ring, frame, arrow, stone, joint],
               [GOLD_DK, GLOW, GOLD, GLOW, CREAM * stone_tone[..., None], GROUT], (N, N))
edge = arrow & (h > .042)                   # the arrow's raised edge, brighter
emit = (np.where(ring[..., None], GLOW_EMIT, 0) + np.where(arrow[..., None], GLOW_EMIT * .8, 0)
        + np.where(edge[..., None], GLOW_EMIT * .4, 0) + np.where(joint[..., None], GLOW_EMIT * .25, 0))
metal = np.where(flange | frame, 1.0, 0.0)
smooth = np.select([flange | frame, ring | arrow], [.75, .6], .45)
save("JumpPad_Top", albedo)
save("JumpPad_Top_Emit", emit)
save("JumpPad_Top_MS", np.dstack([metal * 255] * 3), smooth)
for k, m in (("FloatingPad", (lip, groove, rim, field)), ("JumpPad", (flange, ring, frame, arrow, stone, joint))):
    print("COVER", k, " ".join("%.3f" % x.mean() for x in m))

# ------------------------------------------------------------------ sides (u round one side, v up), 512 x 256
W, Hh = 512, 256
sv, su = np.mgrid[0:Hh, 0:W]
v, u = sv / (Hh - 1), su / (W - 1)            # row 0 = v 0 (the bottom); flipped on saving
for key, line in (("FloatingPad", (.80, .87)), ("JumpPad", (.70, .79))):
    base, trim, band, glow, lipz = v < .12, (v >= .12) & (v < .17), (v >= .17) & (v < line[0]), \
        (v >= line[0]) & (v < line[1]), v >= line[1]
    inset = band & (np.abs(((u * 3) % 1) - .5) < .4) & (v > .25) & (v < line[0] - .07)
    side = paint([base, trim, band, inset, glow, lipz], [BASE, GOLD, GOLD, BASE * 1.4, GLOW, GOLD], (Hh, W))
    side *= (1 + .04 * rng.standard_normal((Hh, W)))[..., None]
    emit = np.where(glow[..., None], GLOW_EMIT, 0) + np.where(inset[..., None], GLOW_EMIT * .08, 0)
    metal = np.where(trim | band | lipz, 1.0, 0.0)
    smooth = np.where(metal > 0, .75, .5)
    save(key + "_Side", side)
    save(key + "_Side_Emit", emit)
    save(key + "_Side_MS", np.dstack([metal * 255] * 3), smooth)
