# Traces the piece icons for the Sword Fight skill head marks (R102) into outlines for C#.
# Usage: python Tools/Generators/trace_skill_icons.py <dir with King.png Queen.png Rook.png Bishop.png Knight.png> <out_dir>
# The icons are dark shapes with light lines on a light ground (승규 님 sent them in chat on 2026-10-10; they are not in
# the repository). Each is cut to its dark pixels, scaled up 8 times (cubic), blurred a little and cut at the middle
# grey, so the outline comes out smooth; then every contour with its holes (RETR_TREE: depth 0 an outline, 1 a light
# line cut out of it, 2 dark again inside that) is simplified and normalised: the icon's height is 1, x from its middle,
# y up from its foot. Writes <out_dir>/icons.cs.txt (the arrays SwordFightSkillFx.Icons.cs holds, in thousandths)
# and <Name>_trace.png previews (dark by even-odd, outlines red, holes green). Needs numpy and opencv-python.
import cv2, sys, os, numpy as np

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
S = 8
blocks = []
for name in ["King", "Queen", "Rook", "Bishop", "Knight"]:
    im = cv2.imread(os.path.join(src, name + ".png"), cv2.IMREAD_COLOR).astype(np.float32)
    g = cv2.cvtColor(im, cv2.COLOR_BGR2GRAY)
    lo, hi = 40.0, 245.0
    ink = np.clip((hi - g) / (hi - lo), 0, 1)   # 1 dark, 0 light
    ys, xs = np.where(ink > 0.5)
    y0, y1, x0, x1 = ys.min() - 2, ys.max() + 3, xs.min() - 2, xs.max() + 3
    ink = ink[max(0, y0):y1, max(0, x0):x1]
    big = cv2.resize(ink, (ink.shape[1] * S, ink.shape[0] * S), interpolation=cv2.INTER_CUBIC)
    big = cv2.GaussianBlur(big, (0, 0), S * 0.45)
    mask = (big > 0.5).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] < (S * S) * 2.5: mask[lab == i] = 0   # specks
    contours, hier = cv2.findContours(mask, cv2.RETR_TREE, cv2.CHAIN_APPROX_NONE)
    hier = hier[0]
    ys, xs = np.where(mask > 0)
    bottom, left, right = ys.max() + 1, xs.min(), xs.max() + 1
    h = bottom - ys.min()
    cx = (left + right) / 2.0
    polys = []
    for i, c in enumerate(contours):
        depth, p = 0, hier[i][3]
        while p >= 0:
            depth += 1
            p = hier[p][3]
        if cv2.contourArea(c) < (S * S) * 1.5: continue
        a = cv2.approxPolyDP(c, S * 0.11, True)[:, 0, :].astype(np.float64)
        polys.append((depth, [((x - cx) / h, (bottom - y) / h) for x, y in a]))
    sc, prev, even = 360, np.full((420, 420, 3), 255, np.uint8), np.zeros((420, 420), np.int32)
    for d, pts in polys:
        m = np.zeros((420, 420), np.uint8)
        cv2.fillPoly(m, [np.array([[210 + x * sc, 400 - y * sc] for x, y in pts], np.int32)], 1)
        even += m
    prev[(even % 2) == 1] = (64, 35, 27)
    for d, pts in polys:
        cv2.polylines(prev, [np.array([[210 + x * sc, 400 - y * sc] for x, y in pts], np.int32)], True, (0, 0, 255) if d == 0 else (0, 160, 0), 1)
    cv2.imwrite(os.path.join(out, name + "_trace.png"), prev)
    print(name, "contours", [(d, len(p)) for d, p in polys])
    rows = []
    for d, pts in polys:
        flat = ", ".join(f"{int(round(x * 1000))}, {int(round(y * 1000))}" for x, y in pts)
        rows.append(f"            new short[] {{ {d}, {flat} }},")
    blocks.append(f"        static readonly short[][] {name}Icon =\n        {{\n" + "\n".join(rows) + "\n        };")
open(os.path.join(out, "icons.cs.txt"), "w", encoding="utf-8").write("\n\n".join(blocks) + "\n")
