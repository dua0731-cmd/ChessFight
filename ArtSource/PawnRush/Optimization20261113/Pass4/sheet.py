# Pass 4 comparison sheet: rows = views, columns = source and candidates, plus a 2x crop of the middle (detail).
import sys
from PIL import Image, ImageDraw
d, out = sys.argv[1], sys.argv[2]; tags = sys.argv[3].split(","); views = sys.argv[4].split(",")
crop = sys.argv[5].split(",") if len(sys.argv) > 5 else None   # x0,y0,x1,y1 in the 1200 image
W = 600
rows = []
for v in views:
    row = []
    for t in tags:
        im = Image.open(f"{d}/{v}_{t}.png").convert("RGBA")
        bg = Image.new("RGBA", im.size, (20, 22, 28, 255)); bg.alpha_composite(im); im = bg.convert("RGB")
        if crop: im = im.crop(tuple(int(c) for c in crop))
        im = im.resize((W, int(W * im.height / im.width)))
        ImageDraw.Draw(im).text((8, 8), f"{v} {t}", fill=(255, 255, 255))
        row.append(im)
    rows.append(row)
H = rows[0][0].height
sheet = Image.new("RGB", (W * len(tags), H * len(views)), (0, 0, 0))
for j, row in enumerate(rows):
    for i, im in enumerate(row): sheet.paste(im, (i * W, j * H))
sheet.save(out)
