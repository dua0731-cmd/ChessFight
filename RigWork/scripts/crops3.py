# Full-resolution crops of chosen frames from several recordings of the same scenes, one row per recording (top row
# first). blender -b -P crops3.py -- out.png "dirA;dirB;dirC" [cw ch] scene:frame ...
import bpy, sys, os
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
out, dirs = a[0], a[1].split(";")
rest = a[2:]
cw, ch = 420, 330
if rest and rest[0].isdigit():
    cw, ch = int(rest[0]), int(rest[1])
    rest = rest[2:]
picks = rest

def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    return x

rows = len(dirs)
sheet = np.ones((rows * ch, len(picks) * cw, 4), dtype=np.float32)
for i, pk in enumerate(picks):
    scene, f = pk.split(":")
    kind = "s_"
    if f.startswith("f"):
        kind, f = "f_", f[1:]
    for r, d in enumerate(dirs):
        im = load(os.path.join(d, scene, "%s%04d.png" % (kind, int(f))))
        h, w = im.shape[:2]
        x0, y0 = (w - cw) // 2, (h - ch) // 2
        y = (rows - 1 - r) * ch          # Blender images start at the bottom
        sheet[y:y + ch, i * cw:(i + 1) * cw] = im[y0:y0 + ch, x0:x0 + cw]
img = bpy.data.images.new("s", len(picks) * cw, rows * ch, alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = out
img.file_format = "PNG"
img.save()
print("SHEET", out)
