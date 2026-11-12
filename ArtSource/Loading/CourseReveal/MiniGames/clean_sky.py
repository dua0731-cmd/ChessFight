# Real-map mini-game pictures for the course reveal (R562).
# raw/<prefab key>.png are 1920 x 1080 shots of PawnRush_MiniGames.unity from the top right corner
# (pos (16.6, 9.6, 2.35), Euler(20.3, -36.5, 0), FOV 60) with signs, text, effects and pieces hidden.
# For the outdoor games the flat test-scene sky is swapped for a soft sky (light blue overhead, warm ivory haze at
# the horizon), small blocks floating in the sky go with it, and the shot is cropped to 16:9.
# Usage: clean_sky.py <raw folder> <out folder>  ->  <out>/<prefab key>.png (full size, read by reveal_pictures.py)
import sys, pathlib, numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage
src, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
out.mkdir(parents=True, exist_ok=True)
crops = {  # (x0, y0, x1); the height follows 16:9
    'Early_MG_C_MoatDrawbridge': (0, 0, 1920),
    'Early_MG_D_PortcullisCourt': (0, 0, 1920),
    'Early_MG_A_ParadeCourt': (176, 0, 1920),  # leaves out the back of the entry banner at the bottom left
}
H, W = 1080, 1920
yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)
u, v = (xx - 960) / 960, yy / 1080
B = np.stack([np.ones_like(u), u, v, u * u, v * v, u * v], -1)
t = np.clip(yy / 560, 0, 1) ** 1.35
top, low = np.array([158, 196, 230.]), np.array([250, 239, 219.])
sky_new = top * (1 - t[..., None]) + low * t[..., None]
glow = np.exp(-(((xx - 980) / 900) ** 2 + ((yy - 470) / 380) ** 2))
sky_new = sky_new + glow[..., None] * np.array([10, 8, 2.])
for name, (x0, y0, x1) in crops.items():
    a = np.asarray(Image.open(src / (name + '.png')).convert('RGB')).astype(np.float64)
    # the old sky is a smooth gradient: fit it, then take every pixel close to the fit as sky
    guess = np.linalg.norm(a - np.array([176, 208, 232.]), axis=-1) < 16
    coef = np.linalg.lstsq(B[guess], a[guess], rcond=None)[0]
    sky_old = B @ coef
    m = np.clip((14 - np.linalg.norm(a - sky_old, axis=-1)) / 8, 0, 1)
    # anti-aliased rims next to the sky take the new colour too
    soft = np.asarray(Image.fromarray((m > .5).astype(np.uint8) * 255).filter(ImageFilter.GaussianBlur(1.2)))
    m = np.maximum(m, soft.astype(np.float64) / 255 * .85)
    lab, n = ndimage.label(m < .5)
    for i, size in enumerate(ndimage.sum(np.ones_like(m), lab, range(1, n + 1)), 1):
        if size > 2000:
            continue
        blob = lab == i
        edge = ndimage.binary_dilation(blob, iterations=3) & ~blob
        if edge.any() and (m[edge] > .5).mean() > .9:
            grow = ndimage.binary_dilation(blob, iterations=2)
            a[grow], m[grow] = sky_old[grow], 1
    res = np.clip(a + m[..., None] * (sky_new - sky_old), 0, 255).astype(np.uint8)
    h = round((x1 - x0) * 9 / 16)
    Image.fromarray(res).crop((x0, y0, x1, y0 + h)).save(out / (name + '.png'))
    print(name, 'sky %.1f%%' % (100 * (m > .5).mean()))
# the indoor games (halls, palace rooms) keep their shot as it is
for f in sorted(src.glob('*.png')):
    if f.stem not in crops:
        Image.open(f).convert('RGB').save(out / f.name)
        print(f.stem, 'as shot')
