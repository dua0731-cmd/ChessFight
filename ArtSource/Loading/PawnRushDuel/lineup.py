# R543 (11-11): a flat look at a lineup over the plate before it goes into LoadingStudio.Duel.cs (DuelSize, DuelFeet):
# rough pawn outlines (.79 of their height across, arms out) at their spots, black mirrored about the strip, the screen's
# team lists, title, VS and buttons on top (16:9, in the picture's pixels), and how much of each white pawn and its head
# no nearer pawn hides.
# Run: uv run --with numpy --with opencv-python-headless python lineup.py 0.4 "[[551,548],[462,548],[506,474],[373,548],[417,474],[462,426]]" out.png
# (needs clean2.png from clean_plate2.py beside it)
import cv2, numpy as np, sys, json
im = cv2.imread('clean2.png'); H, W = im.shape[:2]
CX, CY, F = 630.5, 348.0, 1253.0
TH = np.arctan((CY - 280.0) / F); S, C = np.sin(TH), np.cos(TH)
def to_floor(u, v):
    a = (u - CX) / F; b = -(v - CY) / F
    dy = b * C - S; dz = b * S + C; t = -1.0 / dy
    return t * a, t * dz
def proj(X, y, Z):     # y: height above the floor, camera 1 up
    yy = y - 1.0
    yc = yy * C + Z * S; zc = -yy * S + Z * C
    return CX + F * X / zc, CY - F * yc / zc
k = float(sys.argv[1]); spots = json.loads(sys.argv[2]); out = sys.argv[3]
Hh = 1.208 * k                                        # pawn height in camera heights (the picture's pawn = 1.208)
vis = im.copy()
pawns = []
for team in (0, 1):
    for n, (u, v) in enumerate(spots):
        if team == 1: u = W - u
        X, Z = to_floor(u, v)
        pawns.append((Z, X, team, n))
masks = []
for Z, X, team, n in sorted(pawns, reverse=True):
    def P(dx, y): return tuple(int(round(c)) for c in proj(X + dx * Hh, y * Hh, Z))
    body = (238, 232, 220) if team == 0 else (58, 50, 44)
    edge = (60, 60, 60) if team == 0 else (150, 150, 150)
    m = np.zeros((H, W), np.uint8); head = np.zeros((H, W), np.uint8)
    poly = np.array([P(-.15, .62), P(.15, .62), P(.28, .14), P(-.28, .14)], np.int32)
    cv2.fillPoly(vis, [poly], body); cv2.polylines(vis, [poly], True, edge, 1); cv2.fillPoly(m, [poly], 1)
    for sx in (-1, 1):
        c = P(sx * .33, .5); r = max(2, int(abs(P(.065, 0)[0] - P(0, 0)[0])))
        cv2.circle(vis, c, r, body, -1); cv2.circle(vis, c, r, edge, 1); cv2.circle(m, c, r, 1, -1)
        c = P(sx * .13, .06); cv2.circle(vis, c, r, body, -1); cv2.circle(vis, c, r, edge, 1); cv2.circle(m, c, r, 1, -1)
    c = P(0, .8); r = int(abs(P(.2, 0)[0] - P(0, 0)[0]))
    cv2.circle(vis, c, r, body, -1); cv2.circle(vis, c, r, edge, 1); cv2.circle(m, c, r, 1, -1); cv2.circle(head, c, r, 1, -1)
    cv2.putText(vis, str(n + 1), (c[0] - 4, c[1] + 4), cv2.FONT_HERSHEY_SIMPLEX, .4, (0, 0, 255), 1)
    xs = np.where(m.any(0))[0]; ys = np.where(m.any(1))[0]
    masks.append((team, n, m > 0, head > 0, (xs.min(), xs.max(), ys.min(), ys.max())))
# how much of each pawn (and of its head) no nearer pawn hides; masks run far to near
for i, (team, n, m, hd, box) in enumerate(masks):
    cover = np.zeros((H, W), bool)
    for j in range(i + 1, len(masks)): cover |= masks[j][2]
    if team == 0:
        print('white %d: body %3d%%  head %3d%%  x %d-%d  y %d-%d' % (n + 1, 100 * (m & ~cover).sum() / m.sum(), 100 * (hd & ~cover).sum() / hd.sum(), *box))
# UI (photo pixels on a 16:9 screen): rosters, title, VS, the buttons and status below
ov = vis.copy()
for (x0, y0, x1, y1) in [(41, 143, 321, 491), (940, 143, 1220, 491), (420, 20, 840, 110), (470, 150, 790, 215), (330, 555, 930, 690)]:
    cv2.rectangle(ov, (x0, y0), (x1, y1), (40, 30, 20), -1)
vis = cv2.addWeighted(ov, .45, vis, .55, 0)
for (x0, y0, x1, y1) in [(41, 143, 321, 491), (940, 143, 1220, 491), (420, 20, 840, 110), (470, 150, 790, 215), (330, 555, 930, 690)]:
    cv2.rectangle(vis, (x0, y0), (x1, y1), (120, 200, 255), 1)
cv2.imwrite(out, vis)
