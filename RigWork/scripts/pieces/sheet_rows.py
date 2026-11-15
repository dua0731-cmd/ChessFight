# Top row, axis column and base rows of each figure on the bishop's sheet (front, side, back thirds), cream body only.
# blender -b --factory-startup -P sheet_rows.py -- <sheet image>
import bpy, sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

x = bi.load(sys.argv[sys.argv.index("--") + 1])
h, w = x.shape[:2]
m = bi.body_mask(x)
for i, name in enumerate(("front", "side", "back")):
    x0, x1 = i * w // 3, (i + 1) * w // 3
    sub = m[:, x0:x1]
    ext = {}
    for r in range(h):
        rs = bi.runs(sub[r], min_len=6)
        if rs:
            ext[r] = (rs[0][0] + x0, rs[-1][1] - 1 + x0)
    rows = sorted(ext)
    top, last = rows[0], rows[-1]
    H = last - top
    axis = float(np.median([(ext[r][0] + ext[r][1]) / 2 for r in rows if top + 0.62 * H < r < top + 0.88 * H]))
    wid = {r: ext[r][1] - ext[r][0] + 1 for r in rows}
    base_row = max((r for r in rows if r > top + 0.8 * H), key=lambda r: wid[r])
    print("SHEET %s top %d last %d axis %.1f base widest row %d (width %d); head widest %d" % (
        name, top, last, axis, base_row, wid[base_row], max(wid[r] for r in rows if r < top + 0.5 * H)))
    print("   rows near the base:", " ".join("%d:%d" % (r, wid[r]) for r in range(base_row - 6, min(last, base_row + 40) + 1, 3) if r in wid))
