# Small numbered thumbnails of several images in one row or grid, to pick one by eye.
# blender -b --factory-startup -P contact_sheet.py -- <out.png> <tile height> <cols> <image> ...
import bpy, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
OUT, TH, COLS = a[0], int(a[1]), int(a[2])
tiles = []
for i, p in enumerate(a[3:]):
    x = bi.load(p)
    h, w = x.shape[:2]
    t = bi.resize(bi.on_white(x), int(round(w * TH / h)), TH)
    bi.text(t, i + 1, 6, 6, 4)
    tiles.append(t)
W = max(t.shape[1] for t in tiles)
rows = []
for i in range(0, len(tiles), COLS):
    ts = tiles[i:i + COLS]
    row = []
    for t in ts:
        pad = np.ones((TH, W - t.shape[1] + 4, 4), dtype=np.float32)
        row += [t, pad]
    r = np.concatenate(row, axis=1)
    rows.append(r)
Wmax = max(r.shape[1] for r in rows)
rows = [np.concatenate([r, np.ones((TH, Wmax - r.shape[1], 4), dtype=np.float32)], axis=1) for r in rows]
bi.save(OUT, np.concatenate(rows, axis=0))
print("SHEET", OUT)
