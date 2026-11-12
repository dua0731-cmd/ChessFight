# R543: paints the two pawns out of the user's picture (photo.png) for the Pawn Rush entrance screen.
# Run in this folder: uv run --with numpy --with opencv-python-headless python clean_plate.py -> clean.png,
# then copy clean.png to Assets/Resources/Loading/PawnRushDuel.png. Walls: straight across each row; floor: each
# square's colour from both sides of the pawn, copied from the same row, seams through the vanishing point (630, 280).
# --- 1. the pawns' outline (GrabCut in two boxes)
import cv2, numpy as np
im = cv2.imread('photo.png')
H, W = im.shape[:2]
final = np.zeros((H, W), np.uint8)
for (x0, y0, x1, y1) in [(370, 222, 585, 530), (675, 222, 890, 530)]:
    m = np.zeros((H, W), np.uint8)
    bg, fg = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
    cv2.grabCut(im, m, (x0, y0, x1 - x0, y1 - y0), bg, fg, 8, cv2.GC_INIT_WITH_RECT)
    pawn = np.where((m == 1) | (m == 3), 255, 0).astype(np.uint8)
    # keep the biggest blob
    n, lab, st, _ = cv2.connectedComponentsWithStats(pawn)
    k = 1 + np.argmax(st[1:, cv2.CC_STAT_AREA])
    pawn = np.where(lab == k, 255, 0).astype(np.uint8)
    # fill holes
    ff = pawn.copy(); h2 = np.zeros((H + 2, W + 2), np.uint8); cv2.floodFill(ff, h2, (0, 0), 255)
    pawn = pawn | cv2.bitwise_not(ff)
    final |= pawn
cv2.imwrite('mask_pawns.png', final)
vis = im.copy(); vis[final > 0] = (vis[final > 0] * .3 + np.array([0, 0, 255]) * .7).astype(np.uint8)
cv2.imwrite('mask_vis.png', vis)

# --- 2. wider on the floor, plus the white pawn's cast shadow and the dark line under both bases
m = cv2.imread('mask_pawns.png', 0)
wall = cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (13, 13)))
floor = cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (23, 23)))
out = wall.copy(); out[400:] = floor[400:]
cv2.fillPoly(out, [np.array([[226, 494], [392, 494], [392, 542], [292, 542]], np.int32)], 255)
for x0, x1 in [(370, 592), (676, 890)]:
    cv2.rectangle(out, (x0, 512), (x1, 541), 255, -1)
cv2.imwrite('mask_fill.png', out)
# --- 3. fill
import cv2, numpy as np
im = cv2.imread('photo.png'); H, W = im.shape[:2]
mask = cv2.imread('mask_fill.png', 0) > 0
F = im.astype(np.float32); L = F.mean(2)
nosrc = mask.copy(); nosrc[:, 608:652] = True          # never copy the brass strip
w = (~mask).astype(np.float32)
illum = cv2.GaussianBlur(L * w, (0, 0), 30) / np.maximum(cv2.GaussianBlur(w, (0, 0), 30), 1e-3)
light = (L / (illum + 1e-3)) > 1.0
FLOOR = 405
VP = (630.0, 280.0)
# seams read off the photo for the band the bases stand on (y from 527): x(y)
FIXED = {False: lambda y: 330 - (y - 527) * .41, True: lambda y: 892 + (y - 530) * .8}
out = F.copy()

def spans(y):
    xs = np.flatnonzero(mask[y])
    if len(xs) == 0: return []
    cuts = np.flatnonzero(np.diff(xs) > 1)
    return list(zip(np.r_[xs[0], xs[cuts + 1]], np.r_[xs[cuts], xs[-1]]))

def side_color(y, x, d):
    xs = [x + d * k for k in range(3, 16) if 0 <= x + d * k < W and not nosrc[y, x + d * k]]
    return bool(np.mean(light[y, xs]) > .5) if xs else None

def run_from(y, start, step, color):
    run, k = [], start
    while 0 <= k < W and not nosrc[y, k] and light[y, k] == color and len(run) < 400:
        run.append(k); k += step
    return run

def source(y, x, edge, d, color, other_edge):
    run = run_from(y, edge, d, color)
    if len(run) < 8:
        alt = run_from(y, other_edge, -d, color)
        if len(alt) > len(run): run = alt
    if len(run) < 8:
        cand = [k for k in range(W) if not nosrc[y, k] and light[y, k] == color]
        if cand:
            c = min(cand, key=lambda k: abs(k - x)); run = run_from(y, c, -1 if c < x else 1, color) or [c]
    if not run: run = [edge]
    n = len(run); period = 2 * n - 2 if n > 1 else 1
    t = abs(x - edge) % period
    return run[t if t < n else period - t]

# wall: straight across each row, plus the wall's own grain from beside
grain = F - cv2.GaussianBlur(F, (0, 0), 3)
for y in range(FLOOR):
    for a, b in spans(y):
        l = F[y, max(0, a - 6):a - 1].mean(0); r = F[y, b + 2:min(W, b + 7)].mean(0)
        for x in range(a, b + 1):
            t = (x - a + 1) / (b - a + 2)
            sx = x - 240 if not mask[y, x - 240] else min(W - 1, x + 240)
            out[y, x] = l * (1 - t) + r * t + grain[y, sx] * .9

# floor: the two sides' colours, and where they differ a seam through the vanishing point per band
rows = {}
for y in range(FLOOR, H):
    for a, b in spans(y):
        side = (a + b) / 2 > 630
        rows.setdefault(side, []).append((y, a, b, side_color(y, a, -1), side_color(y, b, 1)))
for side, lst in rows.items():
    # bands: runs of rows whose sides differ
    k_of = {}
    groups, cur = [], []
    for (y, a, b, cl, cr) in lst:
        if cl is not None and cr is not None and cl != cr and y < 527:
            if cur and y - cur[-1][0] > 3: groups.append(cur); cur = []
            cur.append((y, (a + b) / 2))
    if cur: groups.append(cur)
    for g in groups:
        ym, xm = g[len(g) // 2]
        k = (xm - VP[0]) / (ym - VP[1])
        for (y, _) in g: k_of[y] = k
    for (y, a, b, cl, cr) in lst:
        if cl is None: cl = cr
        if cr is None: cr = cl
        if y >= 527:
            seam = FIXED[side](y); cl = side_color(y, int(seam) - 1, -1) if False else False if not side else False
            cl, cr = (False, True) if not side else (False, True)
        elif cl != cr and y in k_of:
            seam = VP[0] + k_of[y] * (y - VP[1])
        else:
            seam = (a + b) / 2
        for x in range(a, b + 1):
            if x < seam: sx = source(y, x, a - 1, -1, cl, b + 1)
            else: sx = source(y, x, b + 1, 1, cr, a - 1)
            out[y, x] = F[y, sx]
# the line the bases stand on: the band above carried down, the band below carried up (no seams cross them)
for y in range(512, 527):
    for a, b in spans(y):
        out[y, a:b + 1] = out[y - 14, a:b + 1]
for y in range(546, 526, -1):
    for a, b in spans(y):
        for x in range(a, b + 1):
            yy = y + 20
            out[y, x] = F[yy, x] if not mask[yy, x] else out[yy, x]
out = np.clip(out, 0, 255).astype(np.uint8)
cv2.imwrite('clean_raw.png', out)
# thin horizontal slivers where the two halves of a row met a seam line at different heights: a tall, thin median
med = cv2.medianBlur(cv2.medianBlur(out, 5), 5)
vert = out.copy()
for c in range(3):
    ch = out[:, :, c]
    stack = np.stack([np.roll(ch, s, axis=0) for s in range(-4, 5)])
    vert[:, :, c] = np.median(stack, axis=0).astype(np.uint8)
sel = np.zeros_like(mask); sel[FLOOR:] = mask[FLOOR:]
out[sel] = vert[sel]

fm = np.zeros((H, W), np.uint8); fm[FLOOR:][mask[FLOOR:]] = 255
fm = cv2.dilate(fm, np.ones((5, 5), np.uint8))
res = out.copy()
n, lab, st, _ = cv2.connectedComponentsWithStats(fm)
for i in range(1, n):
    x, y, w_, h_ = st[i, :4]
    res = cv2.seamlessClone(out, res, np.where(lab == i, 255, 0).astype(np.uint8), (int(x + w_ // 2), int(y + h_ // 2)), cv2.NORMAL_CLONE)
cv2.imwrite('clean.png', res)
cv2.imwrite('cmp4.png', np.vstack([res[360:620, 160:1100], im[360:620, 160:1100]]))
