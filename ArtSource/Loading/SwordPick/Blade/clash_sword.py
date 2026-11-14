# R636 (11-13): draws the longsword that cuts the Sword Fight clash screen's diagonal (사용자 "여기 노란색 대각선으로
# 슬라이스 된 부분 실제 칼로 자르는 연출을 넣어줘") into Assets/Resources/Loading/SwordPick/ClashSword.png, 2048 x 256,
# tip to the right. A steel blade with a diamond section (the upper facet lit, the lower in shade, a bright ridge,
# bright sharpened bevels, a fuller groove lit the other way round), a brass crossguard and pommel shaded as metal,
# a leather-wrapped grip, a thin dark outline so it reads over the gold cut. Drawn at 3x and averaged down.
#   uv run --no-project --managed-python --python 3.12 --with numpy --with pillow python -I clash_sword.py <project root>
import os, sys, numpy as np
from PIL import Image

ROOT = sys.argv[1]
OUT = os.path.join(ROOT, 'Assets/Resources/Loading/SwordPick/ClashSword.png')
W, H, SS = 2048, 256, 3
CY = H / 2
ys, xs = np.mgrid[0:H * SS, 0:W * SS].astype(np.float32)
X = (xs + .5) / SS; Y = (ys + .5) / SS
rgb = np.zeros((H * SS, W * SS, 3), np.float32); a = np.zeros((H * SS, W * SS), np.float32)

def put(mask, col):
    global rgb, a
    rgb[mask] = col[mask] if col.ndim == 3 else col
    a[mask] = 1.0

def mix(c0, c1, t):
    t = np.clip(t, 0, 1)[..., None]; return np.array(c0, np.float32) * (1 - t) + np.array(c1, np.float32) * t

L = np.array([-.35, -.62, .70], np.float32); L /= np.linalg.norm(L)

def metal(nx, ny, base, shade, hi):
    nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
    d = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    c = mix(shade, base, d)
    spec = d ** 28
    return c + (np.array(hi, np.float32) - c) * spec[..., None]

BRASS, BRASS_D, BRASS_H = (.80, .61, .29), (.30, .20, .08), (1., .93, .70)

# ---- blade
x0, xt, xb = 338., 2040., 1760.
hw = np.where(X < xb, 36 - 9 * (X - x0) / (xb - x0), 27 * np.clip((xt - X) / (xt - xb), 0, 1) ** .75)
blade = (X >= x0) & (X <= xt) & (np.abs(Y - CY) <= hw)
v = (Y - CY) / np.maximum(hw, 1e-3)
along = (X - x0) / (xt - x0)
sky = .93 + .05 * np.sin(along * 9.1 + 1.3) + .03 * np.sin(along * 23.7)  # what the steel reflects, along its length
upper = mix((.66, .70, .75), (.86, .89, .92), (-v - .05) / .8) * sky[..., None]
lower = mix((.40, .44, .49), (.27, .30, .35), (v - .05) / .8) * sky[..., None]
c = np.where((v < 0)[..., None], upper, lower)
band = np.exp(-((X - 760) / 70) ** 2) + .7 * np.exp(-((X - 1420) / 55) ** 2)  # soft sheen on the lit facet
c = np.where((v < 0)[..., None], c + (1 - c) * (.55 * band)[..., None], c)
fuller = (X > 372) & (X < 1300) & (np.abs(v) < .2)
fz = np.clip((X - 372) / 30, 0, 1) * np.clip((1300 - X) / 60, 0, 1)
fc = np.where((v < 0)[..., None], mix((.36, .39, .44), (.50, .53, .58), (v + .2) / .2), mix((.62, .66, .71), (.80, .83, .87), v / .2))
c = np.where(fuller[..., None], c * (1 - fz[..., None]) + fc * fz[..., None], c)
c = np.where((np.abs(v) < .045)[..., None] & ~fuller[..., None], np.array((.97, .98, .99), np.float32), c)
c = np.where((v <= -.80)[..., None], np.array((.95, .96, .98), np.float32), c)
c = np.where((v >= .80)[..., None], np.array((.62, .66, .71), np.float32), c)
c = np.where((v <= -.93)[..., None], np.array((1., 1., 1.), np.float32), c)
put(blade, c)

# ---- grip: leather wrapped round the core, cylindrical, a brass ferrule at each end
hh = 22 - 3 * np.abs((X - 188) / 112)
grip = (X >= 76) & (X <= 300) & (np.abs(Y - CY) <= hh)
w = (Y - CY) / np.maximum(hh, 1e-3)
g = mix((.13, .08, .05), (.30, .19, .11), np.sqrt(np.clip(1 - w * w, 0, 1)))
g = g + (np.array((.52, .36, .24), np.float32) - g) * (np.exp(-((w + .38) / .18) ** 2) * .7)[..., None]
phase = (X + (Y - CY) * .65) / 15
groove = (phase - np.floor(phase)) < .16
g = np.where(groove[..., None], g * .45, g)
put(grip, g)
for f0, f1 in ((76, 92), (284, 300)):
    m = (X >= f0) & (X <= f1) & (np.abs(Y - CY) <= hh + 3)
    put(m, metal(np.zeros_like(X), (Y - CY) / (hh + 3), BRASS, BRASS_D, BRASS_H))

# ---- crossguard: a round bar, its arms curving a little toward the tip, a knob at each end, a diamond in the middle
gc = 320 + 12 * ((Y - CY) / 114) ** 2
gx = (X - gc) / 18
guard = (np.abs(gx) <= 1) & (np.abs(Y - CY) <= 106)
put(guard, metal(gx * .95, np.zeros_like(X), BRASS, BRASS_D, BRASS_H))
for ky in (CY - 108, CY + 108):
    kx = 320 + 12 * ((ky - CY) / 114) ** 2
    nx = (X - kx) / 19; ny = (Y - ky) / 19
    put(nx * nx + ny * ny <= 1, metal(nx, ny, BRASS, BRASS_D, BRASS_H))
dia = np.abs(X - 322) / 30 + np.abs(Y - CY) / 48
put(dia <= 1, metal(np.sign(X - 322) * .55 * np.clip(dia, 0, 1), np.sign(Y - CY) * .55 * np.clip(dia, 0, 1), BRASS, BRASS_D, BRASS_H))

# ---- pommel
nx = (X - 46) / 36; ny = (Y - CY) / 40
put(nx * nx + ny * ny <= 1, metal(nx, ny, BRASS, BRASS_D, BRASS_H))

# ---- thin dark outline (1.5 px) so the steel reads over the gold cut
r = int(1.5 * SS); grow = a.copy()
for dy in range(-r, r + 1):
    for dx in range(-r, r + 1):
        if dx * dx + dy * dy <= r * r: grow = np.maximum(grow, np.roll(np.roll(a, dy, 0), dx, 1))
edge = (grow > 0) & (a == 0)
rgb[edge] = (.07, .06, .06); a[edge] = .85

# ---- average down, premultiplied
pre = rgb * a[..., None]
pre = pre.reshape(H, SS, W, SS, 3).mean((1, 3)); al = a.reshape(H, SS, W, SS).mean((1, 3))
col = np.where(al[..., None] > 0, pre / np.maximum(al[..., None], 1e-6), 0)
img = np.dstack([np.clip(col, 0, 1) ** (1 / 1.0), al])
os.makedirs(os.path.dirname(OUT), exist_ok=True)
Image.fromarray((img * 255 + .5).astype(np.uint8), 'RGBA').save(OUT)
print('wrote', OUT, img.shape)
