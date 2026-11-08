# D courtyard paving (R505): a tileable cream flagstone texture made with numpy (the Tripo paving tile came as an
# upright lump and is not used). One 2 m tile = 2048 px: four 0.5 m courses of flagstones of random lengths
# (0.45-1.0 m), each course wrapping round so the tile repeats seamlessly, 1.8 cm joints, rounded stone edges, a
# little colour difference per stone and periodic (tiling) grain. Writes PR_Early_Paving_BaseColor / _Normal.png.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r505_paving.py
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Textures")
N, TILE = 2048, 2.0                      # pixels, metres
PX = N / TILE
COURSES, JOINT, BEVEL = 4, .018, .03     # rows per tile, joint width (m), rounded edge (m)
CREAM = np.array([.84, .76, .62])        # sRGB, the phase-1 cream stone
MORTAR = np.array([.62, .56, .47])
rng = np.random.default_rng(505)

y, x = np.mgrid[0:N, 0:N].astype(np.float32)


def periodic_noise(octaves):
    """Sum of waves with whole numbers of periods across the tile: repeats exactly at the edges."""
    out = np.zeros((N, N), np.float32)
    for amp, kmax in octaves:
        for _ in range(10):
            kx, ky = rng.integers(-kmax, kmax + 1, 2)
            if kx == 0 and ky == 0: continue
            ph = rng.uniform(0, 2 * np.pi)
            out += amp * np.sin(2 * np.pi * (kx * x + ky * y) / N + ph)
    return out / max(1e-6, np.abs(out).max())


stone_id = np.zeros((N, N), np.int32)
edge = np.zeros((N, N), np.float32)      # distance to the nearest joint (px)
tint = np.zeros((N, N), np.float32)
row_h = N // COURSES
sid = 0
for r in range(COURSES):
    widths = rng.uniform(.45, 1.0, 6)
    widths = widths / widths.sum() * TILE
    while widths.min() < .4:             # keep stones readable after the normalising
        widths = rng.uniform(.45, 1.0, len(widths) - 1); widths = widths / widths.sum() * TILE
    offset = rng.uniform(0, TILE)
    cuts = (np.concatenate([[0], np.cumsum(widths)]) + offset) % TILE * PX
    y0, y1 = r * row_h, (r + 1) * row_h
    xs = x[y0:y1]
    # which stone along the course: position relative to the (wrapping) cuts
    rel = (xs - cuts[0]) % N
    bounds = np.sort((cuts - cuts[0]) % N)
    k = np.searchsorted(bounds, rel, side='right') - 1
    left = bounds[np.clip(k, 0, len(bounds) - 1)]
    right = np.where(k + 1 < len(bounds), bounds[np.clip(k + 1, 0, len(bounds) - 1)], N)
    dx = np.minimum(rel - left, right - rel)
    dy = np.minimum(y[y0:y1] - y0, y1 - 1 - y[y0:y1])
    edge[y0:y1] = np.minimum(dx, dy)
    stone_id[y0:y1] = sid + k
    for j in range(len(widths) + 1):
        tint[y0:y1][k == j] = rng.uniform(-.06, .06)
    sid += len(widths) + 1

joint = edge < JOINT * PX / 2
height = np.clip((edge - JOINT * PX / 2) / (BEVEL * PX), 0, 1)
height = height * (2 - height)                                    # rounded shoulder
grain = periodic_noise([(1, 3), (.5, 9), (.25, 23)])
height = height * (1 + .04 * grain)

colour = CREAM[None, None, :] * (1 + tint[..., None]) * (1 + .05 * grain[..., None])
speck = rng.uniform(-1, 1, (N, N)).astype(np.float32)
colour = colour * (1 + .025 * speck[..., None])                  # fine grain
colour = colour * (.93 + .07 * height[..., None])                 # a soft darkening into the edge
colour[joint] = MORTAR * (1 + .05 * grain[joint][:, None])
colour = np.clip(colour, 0, 1)

# normal (OpenGL, +y up) from the height map, wrapping at the edges
s = 6.0
gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * s
gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * s
n = np.dstack([-gx, -gy, np.ones_like(gx)])
n /= np.linalg.norm(n, axis=2, keepdims=True)


def save(name, rgb, linear):
    img = bpy.data.images.new(name, N, N)
    if not linear: img.colorspace_settings.name = 'sRGB'
    else: img.colorspace_settings.name = 'Non-Color'
    data = np.dstack([rgb, np.ones((N, N))]).astype(np.float32)
    # a byte image's pixels go to the PNG as they are: the authored sRGB colour is written directly
    img.pixels.foreach_set(data[::-1].ravel())
    img.filepath_raw = os.path.join(OUT, name + ".png"); img.file_format = 'PNG'; img.save()
    print("saved", name)


save("PR_Early_Paving_BaseColor", colour, False)
save("PR_Early_Paving_Normal", n * .5 + .5, True)
