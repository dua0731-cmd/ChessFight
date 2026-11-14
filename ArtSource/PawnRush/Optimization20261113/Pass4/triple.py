# Pass 4: before / 0.6 / 0.7 trees from Unity, one row per view
import sys
from PIL import Image, ImageDraw
d, out = sys.argv[1], sys.argv[2]; W = 600; views = ["front", "close"]; phs = [("before", "before"), ("after06", "0.6"), ("after", "0.7")]
s = Image.new("RGB", (W * 3, W * 2))
for j, v in enumerate(views):
    for i, (ph, label) in enumerate(phs):
        im = Image.open(f"{d}/Garden_tree_W1_{v}_{ph}.png").convert("RGB").resize((W, W))
        ImageDraw.Draw(im).text((8, 8), f"{v} {label}", fill=(255, 255, 255)); s.paste(im, (i * W, j * W))
s.save(out)
