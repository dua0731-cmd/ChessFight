# Old-versus-new sheets from two -ragdollFeel recordings of the same scenes (same physics, so the same frame is the
# same moment). Per scene: N frames across the clip; rows: old side camera, new side camera, old player camera,
# new player camera. blender -b -P cmp_feel.py -- <old dir> <new dir> <out dir> [N]
import bpy, sys, os
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
old, new, out = args[:3]
n = int(args[3]) if len(args) > 3 else 7
os.makedirs(out, exist_ok=True)

def load(path, step=2):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::step, ::step]
    bpy.data.images.remove(im)
    return a

for scene in sorted(os.listdir(old)):
    d_old, d_new = os.path.join(old, scene), os.path.join(new, scene)
    if not os.path.isdir(d_old) or not os.path.isdir(d_new):
        continue
    frames = sorted(f for f in os.listdir(d_old) if f.startswith("f_"))
    if not frames:
        continue
    picks = [frames[int(i * (len(frames) - 1) / (n - 1))] for i in range(n)]
    rows = []
    for kind in ("s_", "f_"):
        for d in (d_old, d_new):
            rows.append([load(os.path.join(d, kind + f[2:])) for f in picks])
    h, w = rows[0][0].shape[:2]
    sheet = np.ones((len(rows) * h, n * w, 4), dtype=np.float32)
    for r, imgs in enumerate(rows):
        y0 = (len(rows) - 1 - r) * h
        for c, im in enumerate(imgs):
            sheet[y0:y0 + h, c * w:(c + 1) * w] = im
    img = bpy.data.images.new("s", n * w, len(rows) * h, alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = os.path.join(out, "cmp_%s.png" % scene)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("SHEET", scene, picks[0], picks[-1])
