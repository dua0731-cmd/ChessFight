# The palace wall banner (승규 11-03, a reference wall: cream ashlar, gold trim, a crimson banner with a gold crown):
# a crimson field, a gold border inset from the edge that follows the swallowtail, a thin inner line and a gold crown
# with five pearled points. The banner mesh (walls_v29.py) is cut to the same swallowtail, its UVs straight over it
# (u across, v up, the notch's top 0.12 of the height above the bottom corners).
# Run: python banner_texture_v29.py -> Assets/Resources/PawnRushGrandStairHall/PalaceBanner.png
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall", "PalaceBanner.png"))
W, H, NOTCH = 512, 1024, .12            # NOTCH: as walls_v29.py
CRIMSON, DEEP, GOLD, GOLD_DK, PEARL = (158, 27, 42), (112, 14, 28), (226, 172, 72), (138, 90, 22), (255, 236, 196)

img = Image.new("RGB", (W, H), CRIMSON)
d = ImageDraw.Draw(img)


def bottom(u):
    """The swallowtail's lower edge (image y, 0 at the top) at u 0 .. 1."""
    return H - 1 - NOTCH * H * (1 - abs(2 * u - 1))


def outline(inset):
    pts = [(inset, inset), (W - 1 - inset, inset), (W - 1 - inset, bottom(1) - inset)]
    pts += [(W / 2, bottom(.5) - inset), (inset, bottom(0) - inset)]
    return pts


# a soft vertical shading in the cloth (folds), darker toward the edges
rng = np.random.default_rng(29)
a = np.asarray(img).astype(float)
u = np.linspace(0, 1, W)[None, :, None]
fold = 1 + .06 * np.sin(u * np.pi * 6) - .12 * (np.abs(u - .5) * 2) ** 3
a = a * fold * (1 + .025 * rng.standard_normal((H, W, 1)))
img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(img)

d.line(outline(14) + [outline(14)[0]], fill=GOLD, width=16, joint="curve")
d.line(outline(40) + [outline(40)[0]], fill=GOLD, width=4, joint="curve")
d.line(outline(4) + [outline(4)[0]], fill=GOLD_DK, width=6, joint="curve")
d.rectangle([0, 0, W, 60], fill=GOLD_DK)                   # the hem round the rod
d.rectangle([0, 52, W, 64], fill=GOLD)

# the crown
cx, cy, s = W / 2, 330, 1.25
band = [(cx - 120 * s, cy + 60 * s), (cx + 120 * s, cy + 60 * s), (cx + 120 * s, cy + 95 * s), (cx - 120 * s, cy + 95 * s)]
body = [(cx - 120 * s, cy + 60 * s), (cx - 135 * s, cy - 40 * s), (cx - 70 * s, cy + 10 * s), (cx - 35 * s, cy - 70 * s),
        (cx, cy - 10 * s), (cx + 35 * s, cy - 70 * s), (cx + 70 * s, cy + 10 * s), (cx + 135 * s, cy - 40 * s),
        (cx + 120 * s, cy + 60 * s)]
d.polygon(body, fill=GOLD, outline=GOLD_DK)
d.line(body + [body[0]], fill=GOLD_DK, width=5)
d.polygon(band, fill=GOLD, outline=GOLD_DK)
d.line(band + [band[0]], fill=GOLD_DK, width=5)
for px, py in ((-135, -40), (-35, -70), (0, -10), (35, -70), (135, -40)):
    r = 15 * s
    d.ellipse([cx + px * s - r, cy + py * s - r - 8, cx + px * s + r, cy + py * s + r - 8], fill=PEARL, outline=GOLD_DK, width=4)
for jx, col in ((-70, (60, 110, 200)), (0, (200, 30, 40)), (70, (40, 150, 90))):
    r = 13 * s
    d.ellipse([cx + jx * s - r, cy + 77 * s - r, cx + jx * s + r, cy + 77 * s + r], fill=col, outline=GOLD_DK, width=3)
# a small fleur under the crown
d.polygon([(cx, cy + 160 * s), (cx - 30 * s, cy + 210 * s), (cx, cy + 270 * s), (cx + 30 * s, cy + 210 * s)], fill=GOLD, outline=GOLD_DK)

# clear what lies below the swallowtail (the mesh does not reach it; transparent for safety)
alpha = Image.new("L", (W, H), 255)
ImageDraw.Draw(alpha).polygon([(0, bottom(0)), (W / 2, bottom(.5)), (W, bottom(1)), (W, H), (0, H)], fill=0)
img.putalpha(alpha)
img.save(OUT)
print("BANNER", OUT, img.size)
