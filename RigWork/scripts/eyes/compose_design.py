# design images (top) over our renders (bottom), same order
import bpy, sys, os
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]
OUT, D = a[0], a[1]
pairs = list(zip(a[2::2], a[3::2]))
def load(p):
    im = bpy.data.images.load(p); w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4); bpy.data.images.remove(im); return x
def half(x):
    return x[::2, ::2]
def white(t):
    al = t[..., 3:4]
    return np.concatenate([np.clip(t[..., :3] + (1 - al), 0, 1), np.ones_like(al)], axis=2)
top, bot = [], []
for ref, ours in pairs:
    r = load(ref)
    if r.shape[0] != 627:
        r = half(r)[:627, :627]
    top.append(r)
    bot.append(white(load(os.path.join(D, ours))))
grey = np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
def row(ts):
    parts = []
    for t in ts:
        parts += [t, np.ones((t.shape[0], 8, 4), dtype=np.float32) * grey]
    return np.concatenate(parts[:-1], axis=1)
s = np.concatenate([row(bot), np.ones((8, row(top).shape[1], 4), dtype=np.float32) * grey, row(top)], axis=0)
img = bpy.data.images.new("c", s.shape[1], s.shape[0], alpha=True); img.pixels = s.ravel()
img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
