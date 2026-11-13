# 승규 11-03 (a reference of a red-carpeted palace hall): "우리 맵에 빨간색으로 된 부분 카펫 재질 텍스쳐 입히고". The hall's red
# respawn floor gets a carpet: this writes its tiling albedo, one tile = 2 x 2 m of crimson pile - fine woven noise
# (tileable: made in frequency space), a soft nap that lightens and darkens in broad patches, and a faint
# diamond-and-rosette damask in a slightly deeper red, as a palace runner. The gold border lines are Unity's
# (Course01v4Builder.GrandStairHallModel.cs, CarpetHallFloor).
# Run: python carpet_texture_v26.py -> Assets/Resources/PawnRushGrandStairHall/HallCarpet.png
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall", "HallCarpet.png"))
N = 1024
rng = np.random.default_rng(326)


def tile_noise(scale_px, power=1.0):
    """Tileable noise with features about `scale_px` across (white noise low-passed by an FFT mask)."""
    f = np.fft.fftfreq(N)
    fx, fy = np.meshgrid(f, f)
    r = np.sqrt(fx ** 2 + fy ** 2)
    mask = np.exp(-(r * scale_px) ** 2 * power)
    n = np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((N, N))) * mask))
    return (n - n.mean()) / (n.std() + 1e-9)


u, v = np.meshgrid(np.arange(N) / N, np.arange(N) / N)
fibre = tile_noise(1.5)                     # the pile, a pixel or two
weave = np.sin(2 * np.pi * u * 256) * np.sin(2 * np.pi * v * 256) * .5   # the backing's weave, 4 px
nap = tile_noise(90)                        # broad nap: where the pile lies one way or the other
# damask: a diamond lattice (two per tile) and a rosette at each lattice centre
a, b = (u + v) * 2 % 1.0, (u - v) * 2 % 1.0
line = np.minimum(np.minimum(a, 1 - a), np.minimum(b, 1 - b))
lattice = np.clip(1 - line / .012, 0, 1)
cu, cv = (u * 2 + .5) % 1.0 - .5, (v * 2 + .5) % 1.0 - .5     # centres of the diamonds
rr, th = np.sqrt(cu ** 2 + cv ** 2), np.arctan2(cv, cu)
rosette = np.clip(1 - np.abs(rr - (.11 + .035 * np.cos(4 * th))) / .014, 0, 1) + np.clip(1 - rr / .03, 0, 1)
motif = np.clip(lattice * .8 + rosette, 0, 1)

base = np.array([150, 26, 34], float)        # crimson (sRGB)
deep = np.array([112, 14, 24], float)
shade = 1 + .06 * fibre + .025 * weave + .025 * nap
rgb = (base[None, None, :] * (1 - .55 * motif[..., None]) + deep[None, None, :] * .55 * motif[..., None]) * shade[..., None]
img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
img.save(OUT)
print("CARPET", OUT, img.size, "mean", np.round(rgb.reshape(-1, 3).mean(0), 1))
