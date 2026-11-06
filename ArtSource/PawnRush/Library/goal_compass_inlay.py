# 승규 11-04 (a shot of the war room's goal platform - three rooks on a near-black panel with thin orange lines - and a
# concept tile: a cream stone rim with gold corner brackets round a teal panel inlaid with a gold compass star: "마지막
# 구간 발판을 2번 째 사진으로 바꿔"): the goal panel's inlay, drawn for PR_LibraryInlay.shader over the whole top of
# Goal24_Star_platform_center (5.45 m square, no UVs: the shader projects this from the panel's middle). A teal ground
# a little mottled; a double gold frame line inside the edge; cardinal and diagonal gold lines out to it; a gold ring
# and a thin inner one; an eight-point star, its four long points to the cardinals and four short ones to the
# diagonals, every point split down its middle into a light and a dark gold facet; a boss in the middle. Edges are
# filtered by their distance (no stair-steps). Gold is worn brass, not a mirror: metallic .6, smoothness .45.
#   LibraryGoalCompass.png       base colour (sRGB)
#   LibraryGoalCompass_MS.png    R metallic, A smoothness (linear)
# Run: blender -b -P goal_compass_inlay.py   -> Assets/Resources/PawnRushLibrary/Goal/
import bpy, math, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushLibrary", "Goal"))
N = 2048
SIZE = 5.45                       # the panel, metres
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
colour *= (1 - .12 * np.clip((np.maximum(np.abs(X), np.abs(Y)) - 2.2) / .5, 0, 1))[..., None]   # darker towards the rim
metal = np.full((N, N), TEAL_MS[0])
gloss = np.full((N, N), TEAL_MS[1])


def lay(alpha, rgb, ms=GOLD_MS):
    global colour, metal, gloss
    a = alpha[..., None]
    colour = colour * (1 - a) + np.asarray(rgb) * a
    metal = metal * (1 - alpha) + ms[0] * alpha
    gloss = gloss * (1 - alpha) + ms[1] * alpha


# Frame, lines and rings (under the star).
F1, F2, W = 2.48, 2.38, .018
lay(np.maximum(square(F1, W), square(F2, W * .6)), GOLD_LINE)
TIP_LONG, TIP_SHORT, WAIST = 2.0, 1.25, .3
lines = np.zeros((N, N))
for k in range(4):
    a = math.pi / 2 * k
    ca, sa = math.cos(a), math.sin(a)
    lines = np.maximum(lines, segment(TIP_LONG * ca, TIP_LONG * sa, F2 * ca, F2 * sa, .012))
    d = a + math.pi / 4
    corner = F2 * math.sqrt(2)
    lines = np.maximum(lines, segment(TIP_SHORT * math.cos(d), TIP_SHORT * math.sin(d), corner * math.cos(d), corner * math.sin(d), .012))
lay(lines, GOLD_LINE)
lay(np.maximum(ring(.98, .028), ring(.86, .009)), GOLD_LINE)

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
lay(cover(.13 - R), GOLD_DARK)
lay(cover(.055 - R), GOLD_LIGHT)

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
save("LibraryGoalCompass", np.concatenate([np.clip(colour, 0, 1), alpha], axis=2), "sRGB")
ms = np.stack([metal, np.zeros_like(metal), np.zeros_like(metal), gloss], axis=2)
save("LibraryGoalCompass_MS", ms, "Non-Color")
