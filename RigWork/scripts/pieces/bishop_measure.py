# Measures the final bishop design (four body-only views in Downloads): per-row silhouette runs of each view, the
# axis, top and bottom rows, and writes labelled grid crops to look at the parts a silhouette cannot give (the petal
# behind the head, the seam, the facets).
# blender -b --factory-startup -P bishop_measure.py -- <out dir>
import bpy, sys, os, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
os.makedirs(OUT, exist_ok=True)
DL = "C:/Users/trews/Downloads/ChatGPT 이미지 2026년 11월 9일 오후 "
VIEWS = {"front": DL + "10_39_12.png", "left": DL + "10_31_49.png", "right": DL + "10_31_51.png",
         "back": DL + "10_38_40.png"}

report = {}
for name, path in VIEWS.items():
    x = bi.load(path)
    h, w = x.shape[:2]
    m = bi.body_mask(x)
    rr = {}
    for r in range(h):
        rs = bi.runs(m[r])
        if rs:
            rr[r] = rs
    rows = sorted(rr)
    top, bot = rows[0], rows[-1]
    H = bot - top
    mids = [(rr[r][0][0] + rr[r][-1][1] - 1) / 2 for r in rows if top + 0.62 * H < r < top + 0.88 * H]
    axis = float(np.median(mids))
    report[name] = dict(size=[w, h], top=top, bottom=bot, axis=axis, runs={str(r): rr[r] for r in rows})
    print("VIEW %s size %dx%d top %d bottom %d axis %.1f" % (name, w, h, top, bot, axis))
    for r in range(top, bot + 1, 20):
        if r in rr:
            print("  row %4d  %s  width %d" % (r, " ".join("%d-%d" % (s, e - 1) for s, e in rr[r]),
                                                  rr[r][-1][1] - rr[r][0][0]))
    # the last rows, one by one (the floor shadow)
    for r in range(bot - 12, bot + 1):
        if r in rr:
            print("  end %4d  %s" % (r, " ".join("%d-%d" % (s, e - 1) for s, e in rr[r])))
    np.save(os.path.join(OUT, name + "_mask.npy"), m)

with open(os.path.join(OUT, "measure.json"), "w", encoding="utf-8") as f:
    json.dump(report, f)
print("DONE")
