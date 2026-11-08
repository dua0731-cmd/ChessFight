# E Royal Library bell-ring effect textures (R507, user 11-07: "이런 vfx 말고" -> sound waves and musical notes).
# White shapes on black, premultiplied (colour = alpha = the shape with a soft glow); MiniGameBellRingFx tints them HDR
# gold on the additive particle material, so the bloom picks them up.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python e_bell_fx_textures.py
#
# Out: Assets/Resources/PawnRushMiniGames/Fx/T_Bell_Note1.png (an eighth note), T_Bell_Note2.png (two beamed eighths),
#      T_Bell_Wave.png (a sound-wave ring), and for the bookcase door: T_Door_Dust.png (alpha-blended puff, white colour),
#      T_Door_Mote.png (sparkle), T_Door_Glow.png (doorway light); 256 x 256, .meta from the King VFX textures (clamped).
import hashlib, os, re
import numpy as np
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
FOLDER = "Assets/Resources/PawnRushMiniGames/Fx"
META = "Assets/VFX/Textures/T_King_SmokePuff.png.meta"
N = 256
SS = 2                                     # supersampling


def grid():
    n = N * SS
    c = (np.arange(n) + .5) / n
    return np.meshgrid(c, c)               # x right, y up (row 0 = bottom, Blender's pixel order)


def ellipse(x, y, cx, cy, rx, ry, ang):
    a = np.radians(ang); dx, dy = x - cx, y - cy
    u = dx * np.cos(a) + dy * np.sin(a); v = -dx * np.sin(a) + dy * np.cos(a)
    return (np.hypot(u / rx, v / ry) - 1) * min(rx, ry)          # ~signed distance


def rect(x, y, x0, x1, y0, y1):
    dx = np.maximum(x0 - x, x - x1); dy = np.maximum(y0 - y, y - y1)
    return np.where((dx < 0) & (dy < 0), np.maximum(dx, dy), np.hypot(np.maximum(dx, 0), np.maximum(dy, 0)))


def stroke(x, y, pts, w0, w1):
    """Distance to a tapered stroke along a quadratic Bezier (p0, p1, p2), width w0 -> w1."""
    p0, p1, p2 = [np.array(p) for p in pts]
    d = np.full(x.shape, 9.)
    for t in np.linspace(0, 1, 80):
        q = (1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2
        d = np.minimum(d, np.hypot(x - q[0], y - q[1]) - (w0 + (w1 - w0) * t) / 2)
    return d


def quad(x, y, a, b, c, d):
    """Inside a convex quad a-b-c-d (counter-clockwise), as a distance-ish value (<0 inside)."""
    out = np.full(x.shape, -9.)
    for p, q in ((a, b), (b, c), (c, d), (d, a)):
        ex, ey = q[0] - p[0], q[1] - p[1]; L = np.hypot(ex, ey)
        out = np.maximum(out, ((x - p[0]) * ey - (y - p[1]) * ex) / L)
    return out


def shape_to_alpha(d, glow=.035):
    aa = 1.5 / (N * SS)
    core = np.clip(.5 - d / aa, 0, 1)
    halo = .35 * np.exp(-np.maximum(d, 0) / glow)
    return np.maximum(core, halo)


def note1(x, y):
    head = ellipse(x, y, .40, .24, .15, .105, 25)
    stem = rect(x, y, .505, .55, .26, .86)
    flag = stroke(x, y, ((.53, .84), (.80, .70), (.72, .46)), .09, .03)
    return np.minimum(np.minimum(head, stem), flag)


def note2(x, y):
    h1 = ellipse(x, y, .25, .22, .12, .085, 25); h2 = ellipse(x, y, .66, .30, .12, .085, 25)
    s1 = rect(x, y, .335, .37, .23, .80); s2 = rect(x, y, .745, .78, .31, .88)
    beam = quad(x, y, (.335, .73), (.78, .81), (.78, .91), (.335, .83))
    return np.minimum.reduce([h1, h2, s1, s2, beam])


def wave(x, y):
    r = np.hypot(x - .5, y - .5)
    ring = np.abs(r - .43) - .022
    return ring


def dust(x, y):
    """A soft dust puff for the alpha-blended material: lumpy round cloud (sum of soft blobs), soft edge."""
    rng = np.random.default_rng(7)
    a = np.zeros_like(x)
    for _ in range(14):
        cx, cy = rng.normal(.5, .1, 2); r = rng.uniform(.1, .2)
        a += np.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (2 * r * r)) * rng.uniform(.4, .8)
    edge = np.clip(1 - np.hypot(x - .5, y - .5) / .5, 0, 1) ** 1.5
    return np.clip(a * edge, 0, 1)


def mote(x, y):
    """A small four-point sparkle with a round core (additive)."""
    dx, dy = np.abs(x - .5), np.abs(y - .5); r = np.hypot(dx, dy)
    core = np.exp(-(r / .06) ** 2)
    rays = np.exp(-dx / .012) * np.exp(-dy / .22) + np.exp(-dy / .012) * np.exp(-dx / .22)
    return np.clip(core + .8 * rays, 0, 1) * np.clip(1 - r / .5, 0, 1)


def glow(x, y):
    """Light spilling from a doorway: bright at the bottom middle, fading up and to the sides (additive)."""
    return np.exp(-((x - .5) / .32) ** 2) * (1 - y) ** 1.4 * np.clip(1 - np.abs(x - .5) / .5, 0, 1)


def save(name, alpha, premultiplied=True):
    a = alpha.reshape(N, SS, N, SS).mean((1, 3))
    # premultiplied: the shape in the colour too (black around it). The additive particle material adds the colour
    # as is, so a white RGB with the shape only in alpha drew a white square (user, 11-07: "이렇게 나오는데").
    # The dust is for the alpha-blended material: white colour, the shape in alpha only.
    px = np.ones((N, N, 4), np.float32); px[..., 3] = a
    if premultiplied: px[..., 0] = px[..., 1] = px[..., 2] = a
    img = bpy.data.images.new(name, N, N, alpha=True)
    img.pixels.foreach_set(px.ravel())
    rel = FOLDER + "/" + name + ".png"
    img.filepath_raw = os.path.join(ROOT, rel); img.file_format = 'PNG'; img.save()
    meta = os.path.join(ROOT, rel + ".meta")
    if not os.path.exists(meta):
        t = open(os.path.join(ROOT, META), encoding="utf-8").read()
        g = hashlib.md5(("chessfight:" + rel).encode()).hexdigest()
        t = re.sub(r"^guid: \w+", "guid: " + g, t, count=1, flags=re.M)
        t = t.replace("  alphaUsage: 0", "  alphaUsage: 1").replace("  alphaIsTransparency: 0", "  alphaIsTransparency: 1")
        open(meta, "w", encoding="utf-8", newline="\n").write(t)    # the shape is the alpha
    print("FX TEX", rel)


def main():
    os.makedirs(os.path.join(ROOT, FOLDER), exist_ok=True)
    fm = os.path.join(ROOT, FOLDER + ".meta")
    if not os.path.exists(fm):
        g = hashlib.md5(("chessfight:" + FOLDER).encode()).hexdigest()
        open(fm, "w", encoding="utf-8", newline="\n").write(
            "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % g)
    x, y = grid()
    save("T_Bell_Note1", shape_to_alpha(note1(x, y)))
    save("T_Bell_Note2", shape_to_alpha(note2(x, y)))
    save("T_Bell_Wave", shape_to_alpha(wave(x, y), .05))
    # the bookcase door's opening (MiniGameShelfDoorFx)
    save("T_Door_Dust", dust(x, y), premultiplied=False)
    save("T_Door_Mote", mote(x, y))
    save("T_Door_Glow", glow(x, y))


main()
