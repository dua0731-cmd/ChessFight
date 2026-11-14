# Pass 4: Unity before/after pairs side by side, one row per view; argv: folder, object file prefix, out, views
import sys
from PIL import Image, ImageDraw
d, name, out, views = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4].split(",")
W = 700; rows = []
for v in views:
    pair = []
    for ph in ("before", "after"):
        im = Image.open(f"{d}/{name}_{v}_{ph}.png").convert("RGB").resize((W, W))
        ImageDraw.Draw(im).text((8, 8), f"{v} {ph}", fill=(255, 255, 255)); pair.append(im)
    rows.append(pair)
s = Image.new("RGB", (W * 2, W * len(rows)))
for j, r in enumerate(rows):
    for i, im in enumerate(r): s.paste(im, (i * W, j * W))
s.save(out)
