# Bundles GPT asset sheets into one white sheet for Tripo (R505, user 11-07: "분리 한거랑 1번 쨰 사진 같이 줘야지 묶어서").
# Cuts each sheet into its assets by the white columns between them, rescales the pulley sheet so its frame matches
# sheet 1's pulley height, and lays the chosen assets in one row.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python bundle_sheets.py
import bpy, os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "D_assets_04_structure_bundle.png")
GAP, PAD, WHITE = 40, 60, .94


def load(name):
    im = bpy.data.images.load(os.path.join(HERE, name))
    w, h = im.size
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]   # top row first


def objects(px):
    """Column spans with anything darker than WHITE (shadows are lighter and are dropped)."""
    ink = (px[..., :3].min(-1) < WHITE)
    cols = ink.any(0)
    spans, start = [], None
    for x, c in enumerate(cols):
        if c and start is None: start = x
        if not c and start is not None:
            if x - start > 30: spans.append((start, x))
            start = None
    if start is not None: spans.append((start, len(cols)))
    out = []
    for x0, x1 in spans:
        rows = ink[:, x0:x1].any(1).nonzero()[0]
        out.append((x0, x1, rows[0], rows[-1] + 1))
    return out


def crop(px, box, pad=12):
    x0, x1, y0, y1 = box
    h, w = px.shape[:2]
    return px[max(0, y0 - pad):min(h, y1 + pad * 3), max(0, x0 - pad):min(w, x1 + pad)]   # keep some contact shadow


def scale(img, k):
    h, w = img.shape[:2]
    im = bpy.data.images.new("t", w, h, alpha=True)
    im.pixels.foreach_set(img[::-1].ravel())
    im.scale(max(1, int(w * k)), max(1, int(h * k)))
    nw, nh = im.size
    return np.array(im.pixels[:], dtype=np.float32).reshape(nh, nw, 4)[::-1]


s1, s3 = load("D_assets_01_structure.png"), load("D_assets_03_pulley.png")
o1, o3 = objects(s1), objects(s3)
print("SHEET1", o1); print("SHEET3", o3)
assert len(o1) == 5 and len(o3) == 3, "asset count"
k = (o1[0][3] - o1[0][2]) / (o3[0][3] - o3[0][2])      # sheet-1 pulley height / sheet-3 frame height
parts = [scale(crop(s3, o3[0]), k), scale(crop(s3, o3[1]), k)] + [crop(s1, b) for b in o1[1:]]
H = max(p.shape[0] for p in parts) + PAD * 2
W = sum(p.shape[1] for p in parts) + GAP * (len(parts) - 1) + PAD * 2
sheet = np.ones((H, W, 4), dtype=np.float32)
x = PAD
base = H - PAD                                          # bottoms on one line, like the source sheets
for p in parts:
    # The sheets' backgrounds are off-white by a hair (and differ): near-white goes pure white so no cut edge shows.
    near = p[..., :3].min(-1) > .975
    p[near, :3] = 1.0
    h, w = p.shape[:2]
    sheet[base - h:base, x:x + w] = p
    x += w + GAP
im = bpy.data.images.new("bundle", W, H, alpha=True)
im.pixels.foreach_set(sheet[::-1].ravel())
im.filepath_raw = OUT; im.file_format = 'PNG'; im.save()
print("SAVED", OUT, W, H, "scale", round(k, 3))
