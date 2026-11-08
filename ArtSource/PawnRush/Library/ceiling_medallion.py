# 승규 11-04 (the war room ceiling concept - teal steel beams with brass joints over a cream ribbed vault, a teal compass
# medallion with a gold star in its crown: "천장도 이런식으로 블렌더 해서 텍스쳐 채워"): the vault crown's 4 m medallion
# (4m central medallion, no UVs) drawn as goal_compass_inlay.py draws the goal panel, round: a teal disc a little
# mottled, a gold rim band and a thin ring inside it, cardinal and diagonal gold lines out to it, a gold ring round the
# middle, an eight-point star in light and dark gold facets, a boss. For PR_LibraryInlay.shader (its faces looking
# down too). Gold is worn brass: metallic .6, smoothness .45.
#   LibraryCeilingCompass.png       base colour (sRGB)
#   LibraryCeilingCompass_MS.png    R metallic, A smoothness (linear)
# Run: blender -b -P ceiling_medallion.py   -> Assets/Resources/PawnRushLibrary/Goal/
import bpy, math, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary", "Goal"))
N = 1024
SIZE = 4.0                        # the medallion, metres
PX = SIZE / N                     # a texel, metres

TEAL = np.array([.22, .40, .47])
GOLD_LIGHT = np.array([.95, .80, .46])
GOLD_DARK = np.array([.66, .48, .21])
GOLD_LINE = np.array([.86, .69, .35])
GOLD_MS, TEAL_MS = (.6, .45), (.05, .3)

c = (np.arange(N) + .5) * PX - SIZE / 2
X, Y = np.meshgrid(c, c)          # metres from the middle, Y up the picture
R = np.hypot(X, Y)


def cover(signed):
    """How much of a texel lies inside, from its signed distance (metres, inside positive)."""
    return np.clip(signed / PX + .5, 0., 1.)


def segment(ax, ay, bx, by, half):
    dx, dy = bx - ax, by - ay
    t = np.clip(((X - ax) * dx + (Y - ay) * dy) / (dx * dx + dy * dy), 0., 1.)
    return cover(half - np.hypot(X - (ax + t * dx), Y - (ay + t * dy)))


def ring(radius, half):
    return cover(half - np.abs(R - radius))


def square(half_side, half):
    return cover(half - np.abs(np.maximum(np.abs(X), np.abs(Y)) - half_side))


def triangle(p0, p1, p2):
    """Coverage of a triangle (counter-clockwise or not)."""
    pts = [p0, p1, p2]
    area = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p1[1] - p0[1]) * (p2[0] - p0[0])
    s = None
    for i in range(3):
        a, b = pts[i], pts[(i + 1) % 3]
        ex, ey = b[0] - a[0], b[1] - a[1]
        length = math.hypot(ex, ey)
        d = ((X - a[0]) * ey - (Y - a[1]) * ex) / length   # right of the edge positive
        d = -d if area > 0 else d
        s = d if s is None else np.minimum(s, d)
    return cover(s)


def noise(scale, seed):
    """Smooth value noise on the panel (metres per cell `scale`)."""
    rng = np.random.default_rng(seed)
    cells = int(SIZE / scale) + 3
    grid = rng.random((cells, cells))
    gx, gy = (X + SIZE / 2) / scale, (Y + SIZE / 2) / scale
    ix, iy = np.floor(gx).astype(int), np.floor(gy).astype(int)
    fx, fy = gx - ix, gy - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = grid[iy, ix], grid[iy, ix + 1]
    cc, d = grid[iy + 1, ix], grid[iy + 1, ix + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (cc * (1 - fx) + d * fx) * fy


colour = np.empty((N, N, 3))
colour[:] = TEAL
mottle = .5 * noise(.9, 1) + .3 * noise(.35, 2) + .2 * noise(.12, 3)
colour *= (.9 + .18 * mottle)[..., None]
colour *= (1 - .12 * np.clip((R - 1.5) / .4, 0, 1))[..., None]   # darker towards the rim
metal = np.full((N, N), TEAL_MS[0])
gloss = np.full((N, N), TEAL_MS[1])


def lay(alpha, rgb, ms=GOLD_MS):
    global colour, metal, gloss
    a = alpha[..., None]
    colour = colour * (1 - a) + np.asarray(rgb) * a
    metal = metal * (1 - alpha) + ms[0] * alpha
    gloss = gloss * (1 - alpha) + ms[1] * alpha


# Rim, lines and rings (under the star).
lay(np.maximum(ring(1.93, .055), ring(1.8, .012)), GOLD_LINE)
TIP_LONG, TIP_SHORT, WAIST = 1.55, .95, .24
lines = np.zeros((N, N))
for k in range(8):
    a = math.pi / 4 * k
    tip = TIP_LONG if k % 2 == 0 else TIP_SHORT
    lines = np.maximum(lines, segment(tip * math.cos(a), tip * math.sin(a), 1.8 * math.cos(a), 1.8 * math.sin(a), .01))
lay(lines, GOLD_LINE)
lay(np.maximum(ring(.72, .024), ring(.63, .008)), GOLD_LINE)

# The star: eight points, each a light and a dark facet.
for k in range(8):
    a = math.pi / 4 * k
    tip = (TIP_LONG if k % 2 == 0 else TIP_SHORT)
    t = (tip * math.cos(a), tip * math.sin(a))
    left = (WAIST * math.cos(a + math.pi / 8), WAIST * math.sin(a + math.pi / 8))
    right = (WAIST * math.cos(a - math.pi / 8), WAIST * math.sin(a - math.pi / 8))
    shade = .96 + .08 * noise(.5, 10 + k)
    lay(triangle((0., 0.), left, t), GOLD_LIGHT * shade[..., None])
    lay(triangle((0., 0.), t, right), GOLD_DARK * shade[..., None])
# The boss in the middle: a dark gold disc, a light dot.
lay(cover(.11 - R), GOLD_DARK)
lay(cover(.045 - R), GOLD_LIGHT)

os.makedirs(OUT, exist_ok=True)


def save(name, rgba, colourspace):
    img = bpy.data.images.new(name, N, N, alpha=True)
    img.colorspace_settings.name = colourspace
    img.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    path = os.path.join(OUT, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("SAVED", path)


alpha = np.ones((N, N, 1))
save("LibraryCeilingCompass", np.concatenate([np.clip(colour, 0, 1), alpha], axis=2), "sRGB")
ms = np.stack([metal, np.zeros_like(metal), np.zeros_like(metal), gloss], axis=2)
save("LibraryCeilingCompass_MS", ms, "Non-Color")
