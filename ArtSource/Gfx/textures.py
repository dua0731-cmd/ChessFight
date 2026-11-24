# ChessFight look rebuild: the surface sets. Realistic material detail for stylized shapes (the Mario Kart World
# recipe: deformed forms, real surfaces). Every set is made here by numpy, seamless, and written as two images that
# Blender (the look-up renders) and Unity (ChessFight/Look Lit) both read:
#   <set>_mask.png     R roughness (as is)   G cavity (1 open, 0 deep)   B height (0.5 mid)
#                      A albedo variation: the surface colour is multiplied by 1 + (A - 0.5) * 0.5
#   <set>_normal.png   tangent-space normal, +Y up (OpenGL, Unity), from the height at the set's size and depth
# The colour itself is not in the set: it comes from the mesh (a colour per face corner), so one limestone serves
# every block of every tint. Sizes and depths are in SETS below; kit.py and the Unity build read the same table
# (tex/sets.json).
#
#   blender -b --factory-startup --python textures.py -- --out C:/cf-gfx/tex [--only limestone,wood] [--n 1024]
import bpy, sys, os, json, math
import numpy as np

def cli(name, fallback=None):
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return argv[argv.index(name) + 1] if name in argv else fallback

N = int(cli('--n', '1024'))
OUT = cli('--out', os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tex'))

# set: tile = metres one image covers, depth = metres between height 0 and 1, grain = the set has a direction (image V)
SETS = {
    'limestone': dict(tile=2.4, depth=0.007),
    'sand': dict(tile=1.6, depth=0.010),
    'wood': dict(tile=1.0, depth=0.0028, grain=True),
    'iron': dict(tile=0.5, depth=0.0012),
    'brass': dict(tile=0.3, depth=0.0003),
    'paint': dict(tile=0.8, depth=0.0006),
    'canvas': dict(tile=0.2, depth=0.0007, grain=True),
    'leaf': dict(tile=1.2, depth=0.014),
    'slate': dict(tile=1.0, depth=0.0035),
}

# ---------------------------------------------------------------- seamless building blocks
def rng(seed):
    return np.random.default_rng(seed)

def spectral(seed, beta=2.0, lo=1.0, hi=None, ax=1.0, ay=1.0, turn=0.0):
    """Seamless noise, mean 0 and deviation 1: power falls as 1/f^beta between lo and hi cycles a tile.
    ay > 1 draws the features out along the image's V (ax along U); turn rotates that direction (radians)."""
    white = rng(seed).standard_normal((N, N))
    F = np.fft.rfft2(white)
    fy = np.fft.fftfreq(N)[:, None] * N
    fx = np.fft.rfftfreq(N)[None, :] * N
    if turn:
        c, s = math.cos(turn), math.sin(turn)
        fx, fy = fx * c + fy * s, -fx * s + fy * c
    # a feature drawn out along V changes slowly along V: its V frequencies count for more
    f = np.sqrt((fx * ax) ** 2 + (fy * ay) ** 2)
    f[0, 0] = 1.0
    amp = f ** (-beta / 2.0)
    amp *= 1.0 / (1.0 + (lo / np.maximum(f, 1e-6)) ** 8)
    if hi:
        amp *= 1.0 / (1.0 + (f / hi) ** 8)
    amp[0, 0] = 0.0
    out = np.fft.irfft2(F * amp, s=(N, N))
    out -= out.mean()
    return (out / (out.std() + 1e-9)).astype(np.float32)

def blur(a, sigma):
    """A Gaussian blur of sigma pixels that wraps."""
    fy = np.fft.fftfreq(N)[:, None]
    fx = np.fft.rfftfreq(N)[None, :]
    g = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.fft.irfft2(np.fft.rfft2(a) * g, s=(N, N)).astype(np.float32)

V, U = np.mgrid[0:N, 0:N].astype(np.float32) / N        # V up the image (row 0 is the bottom row), U across

def worley(cells, seed, jitter=1.0):
    """Seamless cells. Returns d1, d2 (distance to the nearest and second point, in cells), the nearest cell's random
    value, and the vector from the pixel to the nearest point (in cells)."""
    r = rng(seed)
    px = (np.arange(cells)[None, :] + 0.5 + (r.random((cells, cells)) - 0.5) * jitter) / cells
    py = (np.arange(cells)[:, None] + 0.5 + (r.random((cells, cells)) - 0.5) * jitter) / cells
    val = r.random((cells, cells)).astype(np.float32)
    cx, cy = np.floor(U * cells).astype(np.int32), np.floor(V * cells).astype(np.int32)
    d1 = np.full((N, N), 9.0, np.float32)
    d2 = np.full((N, N), 9.0, np.float32)
    v1 = np.zeros((N, N), np.float32)
    ox1 = np.zeros((N, N), np.float32)
    oy1 = np.zeros((N, N), np.float32)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            ix, iy = (cx + ox) % cells, (cy + oy) % cells
            qx = px[iy, ix] + ((cx + ox) - ix) / cells
            qy = py[iy, ix] + ((cy + oy) - iy) / cells
            ddx, ddy = (qx - U) * cells, (qy - V) * cells
            d = np.hypot(ddx, ddy).astype(np.float32)
            closer = d < d1
            d2 = np.where(closer, d1, np.minimum(d2, d))
            v1 = np.where(closer, val[iy, ix], v1)
            ox1 = np.where(closer, ddx, ox1)
            oy1 = np.where(closer, ddy, oy1)
            d1 = np.where(closer, d, d1)
    return d1, d2, v1, (ox1, oy1)

def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

def unit(h, spread=3.0):
    """A height field to 0..1 about 0.5: spread deviations either way fill the range."""
    h = h - h.mean()
    return np.clip(0.5 + h / (2.0 * spread * (h.std() + 1e-9)), 0.0, 1.0).astype(np.float32)

def cavity(h, radius, gain):
    """1 where the surface is open, toward 0 where it lies under its surroundings."""
    return np.clip(1.0 - gain * np.maximum(blur(h, radius) - h, 0.0), 0.0, 1.0).astype(np.float32)

def normal_map(h, tile, depth):
    px = tile / N
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * depth / (2.0 * px)
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * depth / (2.0 * px)
    n = np.stack([-dx, -dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)

def var(a, amount):
    """An albedo variation of mean 0 and deviation 1, at this many parts either way, as the mask's alpha."""
    return np.clip(0.5 + a * amount * 2.0, 0.0, 1.0).astype(np.float32)

# ---------------------------------------------------------------- the sets: each returns height (0..1), roughness, albedo variation (alpha), cavity
def limestone():
    broad = spectral(11, beta=2.6, lo=1, hi=10)
    medium = spectral(12, beta=1.6, lo=8, hi=70)
    grain = spectral(13, beta=0.4, lo=60, hi=420)
    strata = spectral(14, beta=2.2, lo=2, hi=60, ax=7.0)             # faint bedding, drawn out along U
    tool = spectral(18, beta=0.6, lo=24, hi=140, ay=16.0, turn=0.85)  # the mason's chisel, slanting
    patch = smooth(0.3, 1.5, spectral(19, beta=2.6, lo=1, hi=5))      # pores come in patches, most of a face has none
    d1, d2, v, _ = worley(96, 15)
    pits = smooth(0.14 + 0.30 * v, 0.04, d1) * smooth(0.90, 0.985, v) * patch   # few, small, in patches (dense ones read as dirt in play)
    d1b, _, vb, _ = worley(22, 16)
    holes = smooth(0.24, 0.05, d1b) * smooth(0.965, 0.997, vb)        # a very few larger ones
    h = broad * 0.46 + medium * 0.26 + strata * 0.18 + grain * 0.05 + tool * 0.08 - pits * 0.7 - holes * 1.6
    h = unit(h, 3.2)
    tone = spectral(17, beta=2.4, lo=1, hi=8) * 0.75 + medium * 0.40 + strata * 0.45 + grain * 0.22
    tone = tone / tone.std() - pits * 0.6 - holes * 1.4
    rough = 0.80 + 0.05 * medium - 0.03 * broad + 0.10 * pits
    return h, rough, var(tone, 0.034), cavity(h, 5.0, 6.0)

def sand():
    grain = spectral(21, beta=0.0, lo=110, hi=500)
    medium = spectral(22, beta=2.0, lo=3, hi=40)
    warp = spectral(23, beta=3.0, lo=1, hi=5)
    phase = 3.0 * U + 14.0 * V + warp * 0.9
    ripple = (0.5 + 0.5 * np.sin(2.0 * np.pi * phase)) ** 1.6
    where = smooth(-0.6, 0.7, spectral(24, beta=3.0, lo=1, hi=4))    # ripples in patches
    d1, _, v, _ = worley(40, 25)
    pebble = smooth(0.12 + 0.30 * v, 0.03, d1) * smooth(0.955, 0.995, v)     # a pebble here and there
    h = unit(ripple * where * 2.2 + medium * 0.6 + grain * 0.16 + pebble * 2.6, 3.0)
    tone = grain * 0.55 + medium * 0.55 + (ripple - 0.5) * where * 2.0 + pebble * (v - 0.975) * 120.0
    rough = 0.93 - 0.05 * pebble + 0.02 * grain
    return h, rough, var(tone / tone.std(), 0.036), cavity(h, 4.0, 7.0)

def wood():
    # image V runs along the grain
    drift = spectral(31, beta=3.0, lo=1, hi=7, ay=7.0)                # the rings wander along the board
    rings = np.sin(2.0 * np.pi * (22.0 * U + drift * 1.7))
    rings2 = np.sin(2.0 * np.pi * (55.0 * U + drift * 3.6 + 0.3))
    fibre = spectral(32, beta=1.0, lo=24, hi=460, ay=46.0)
    pore = smooth(1.1, 2.2, spectral(33, beta=0.3, lo=90, hi=500, ay=9.0))   # short open pores
    board = spectral(34, beta=3.0, lo=1, hi=5, ay=5.0)                # broad lighter and darker runs
    streak = smooth(1.0, 2.0, spectral(35, beta=1.6, lo=3, hi=40, ay=70.0))  # a dark streak now and then
    late = smooth(-0.2, 0.9, rings)                                   # the dark late wood of a ring
    h = unit(-late * 0.75 + rings2 * 0.14 + fibre * 0.32 - pore * 1.2 + board * 0.25, 3.0)
    tone = -late * 1.5 + rings2 * 0.25 + fibre * 0.42 + board * 1.0 - pore * 1.2 - streak * 1.6
    rough = 0.56 + 0.07 * late + 0.10 * pore - 0.03 * board
    return h, rough, var(tone / tone.std(), 0.085), cavity(h, 3.0, 5.0)

def iron():
    d1, d2, v, _ = worley(13, 41)
    dent = blur(-np.exp(-(d1 * 1.9) ** 2) * (0.4 + v), 2.5)           # hammer marks: each cell a shallow dish
    medium = spectral(42, beta=2.0, lo=3, hi=40)
    pit = smooth(1.9, 3.0, spectral(43, beta=0.6, lo=40, hi=300))
    scratch = spectral(44, beta=0.8, lo=30, hi=420, ay=28.0, turn=0.5)
    h = unit(dent * 3.2 + medium * 0.22 - pit * 0.8 + scratch * 0.05, 3.0)
    tone = medium * 0.9 + blur(v - 0.5, 6.0) * 2.0 - pit * 1.2
    rough = 0.46 + 0.08 * medium + 0.16 * pit - 0.05 * dent
    return h, rough, var(tone / tone.std(), 0.035), cavity(h, 6.0, 3.0)

def brass():
    a = spectral(51, beta=0.5, lo=40, hi=480, ay=34.0, turn=0.3)
    b = spectral(52, beta=0.5, lo=40, hi=480, ay=34.0, turn=1.9)
    c = spectral(53, beta=0.5, lo=40, hi=480, ay=34.0, turn=2.7)
    medium = spectral(54, beta=2.2, lo=2, hi=24)
    h = unit(a * 0.5 + b * 0.35 + c * 0.25 + medium * 0.5, 3.2)
    rough = 0.25 + 0.05 * medium + 0.04 * np.abs(a)
    return h, rough, var(medium, 0.03), cavity(h, 3.0, 2.0)

def paint():
    peel = spectral(61, beta=0.2, lo=50, hi=150)
    brush = spectral(62, beta=1.0, lo=10, hi=160, ay=24.0)
    medium = spectral(63, beta=2.4, lo=1, hi=12)
    h = unit(peel * 0.5 + brush * 0.55 + medium * 0.35, 3.2)
    rough = 0.36 + 0.04 * medium + 0.03 * brush
    return h, rough, var(medium * 0.8 + brush * 0.5, 0.02), cavity(h, 3.0, 2.5)

def canvas():
    T = 56.0                                                          # threads a tile (3.6 mm)
    wob = spectral(71, beta=2.4, lo=1, hi=10) * 0.012
    u, v = U + wob, V + np.roll(wob, 97, 0)
    warp = 0.5 + 0.5 * np.cos(2.0 * np.pi * T * u)                    # threads running along V
    weft = 0.5 + 0.5 * np.cos(2.0 * np.pi * T * v)
    over = 0.5 + 0.5 * np.tanh(3.0 * np.sin(np.pi * T * u) * np.sin(np.pi * T * v))
    fuzz = spectral(72, beta=0.2, lo=120, hi=500)
    thread = spectral(73, beta=1.2, lo=6, hi=T, ay=30.0) * 0.5 + spectral(74, beta=1.2, lo=6, hi=T, ax=30.0) * 0.5
    h = unit(warp * over + weft * (1.0 - over) + fuzz * 0.06 + thread * 0.10, 2.6)
    tone = (h - 0.5) * 4.0 + thread * 1.0 + spectral(75, beta=2.6, lo=1, hi=6) * 0.8
    rough = 0.90 + 0.03 * fuzz
    return h, rough, var(tone / tone.std(), 0.05), cavity(h, 2.0, 3.0)

def leaf():
    # leaves laid one over another, the later ones on top: each a pointed blade with a dome across and a rib down its middle
    r = rng(81)
    z = np.zeros((N, N), np.float32)
    tone = np.full((N, N), -2.2, np.float32)                          # what shows between the leaves is dark
    count = 3400
    for i in range(count):
        cx, cy, ang = r.random() * N, r.random() * N, r.random() * 2.0 * np.pi
        long = (0.045 + 0.035 * r.random()) * N / SETS['leaf']['tile']
        wide = long * (0.42 + 0.2 * r.random())
        level = 0.25 + 0.75 * (i / count) + 0.1 * r.random()
        light = r.standard_normal() * 0.9 + (i / count - 0.5) * 1.6   # the upper leaves catch more light
        lean = (r.random() - 0.5) * 0.5
        R = int(long * 0.5) + 2
        ys = (np.arange(-R, R + 1) + int(cy)) % N
        xs = (np.arange(-R, R + 1) + int(cx)) % N
        dy, dx = np.mgrid[-R:R + 1, -R:R + 1].astype(np.float32)
        a = (dx * np.cos(ang) + dy * np.sin(ang)) / (long * 0.5)      # -1 at the stem, 1 at the tip
        b = -dx * np.sin(ang) + dy * np.cos(ang)
        half = wide * 0.5 * np.sqrt(np.clip(1.0 - a * a, 0.0, 1.0)) * (1.0 - 0.35 * a)
        inside = (np.abs(a) < 1.0) & (np.abs(b) < half)
        across = b / np.maximum(half, 1e-3)
        surf = level + 0.10 * (1.0 - across * across) + lean * a * 0.12 - 0.05 * np.exp(-(across * 7.0) ** 2)
        win = z[np.ix_(ys, xs)]
        put = inside & (surf > win)
        win[put] = surf[put]
        z[np.ix_(ys, xs)] = win
        tw = tone[np.ix_(ys, xs)]
        tw[put] = (light + 0.5 * a - 0.9 * np.exp(-(across * 7.0) ** 2) - 0.5 * across * across)[put]
        tone[np.ix_(ys, xs)] = tw
    h = unit(z, 2.6)
    rough = 0.48 + 0.05 * np.clip(tone, -2, 2)
    return h, rough, var(tone / tone.std(), 0.10), cavity(h, 5.0, 7.0)

def slate():
    cleave = spectral(91, beta=2.4, lo=2, hi=60, ay=5.0)              # riven faces: long shallow steps
    step = np.round(cleave * 2.2) / 2.2
    medium = spectral(92, beta=1.8, lo=6, hi=80)
    grain = spectral(93, beta=0.4, lo=70, hi=420)
    h = unit(blur(step, 1.6) * 0.9 + medium * 0.25 + grain * 0.06, 3.0)
    tone = cleave * 0.7 + medium * 0.5 + grain * 0.3
    rough = 0.52 + 0.06 * medium - 0.04 * cleave
    return h, rough, var(tone / tone.std(), 0.045), cavity(h, 4.0, 5.0)

MAKERS = dict(limestone=limestone, sand=sand, wood=wood, iron=iron, brass=brass, paint=paint, canvas=canvas, leaf=leaf, slate=slate)

# ---------------------------------------------------------------- write
def save(path, rgba):
    img = bpy.data.images.new(os.path.basename(path), N, N, alpha=rgba.shape[2] == 4, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color'
    if rgba.shape[2] == 3:
        rgba = np.concatenate([rgba, np.ones((N, N, 1), np.float32)], -1)
    img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(np.ascontiguousarray(rgba, np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)

def preview(name, h, rough, alpha, cav, nrm, base):
    """The set on a flat face in a raking light: for the eye, not for the game."""
    n = nrm * 2.0 - 1.0
    light = np.array([-0.55, 0.45, 0.70], np.float32)
    light /= np.linalg.norm(light)
    lit = np.clip(n @ light, 0.0, 1.0)
    half = light + np.array([0, 0, 1], np.float32)
    half /= np.linalg.norm(half)
    spec = np.clip(n @ half, 0.0, 1.0) ** (2.0 / np.maximum(rough, 0.05) ** 2) * (1.0 - rough) * 0.5
    col = np.array(base, np.float32)[None, None, :] * (1.0 + (alpha - 0.5) * 0.5)[:, :, None] * (0.55 + 0.45 * cav)[:, :, None]
    out = col * (0.25 + 0.95 * lit)[:, :, None] + spec[:, :, None]
    return np.clip(out, 0.0, 1.0) ** (1.0 / 2.2)

BASES = dict(limestone=(0.80, 0.70, 0.52), sand=(0.80, 0.56, 0.26), wood=(0.42, 0.22, 0.09), iron=(0.10, 0.10, 0.11), brass=(0.9, 0.6, 0.2),
             paint=(0.6, 0.06, 0.05), canvas=(0.85, 0.78, 0.6), leaf=(0.10, 0.30, 0.06), slate=(0.06, 0.12, 0.42))

os.makedirs(OUT, exist_ok=True)
only = cli('--only')
for name, maker in MAKERS.items():
    if only and name not in only.split(','):
        continue
    h, rough, alpha, cav = maker()
    rough = np.clip(rough, 0.03, 1.0).astype(np.float32)
    spec = SETS[name]
    nrm = normal_map(h, spec['tile'], spec['depth'])
    save(os.path.join(OUT, name + '_mask.png'), np.stack([rough, cav, h, alpha], -1))
    save(os.path.join(OUT, name + '_normal.png'), nrm)
    save(os.path.join(OUT, name + '_preview.png'), preview(name, h, rough, alpha, cav, nrm, BASES[name]).astype(np.float32))
    print('SET %-10s rough %.2f..%.2f  variation %.3f  slope %.2f' % (name, rough.min(), rough.max(), (alpha - 0.5).std() * 0.5,
                                                                       np.abs(nrm[:, :, :2] * 2 - 1).mean()))
with open(os.path.join(OUT, 'sets.json'), 'w') as f:
    json.dump(SETS, f, indent=1)
print('WROTE', OUT)
