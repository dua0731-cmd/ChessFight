# R543 (11-11, second pass, 승규 님: "바닥 타일 간격이 맞지 않아 그거 자세하게 해서 맞춰 주고"): rebuild the floor
# square by square. The floor is a plane seen by a known camera (vanishing point (630, 280), focal 1253), so every
# square is a quad between two seam rows (horizontal in the picture) and two seam columns (lines through the vanishing
# point, constant X on the floor), all read off the picture (the squares beside the strip are narrower: 1.0 against
# 1.18 to 1.2).
#  - a square the pawns, their shadows or the dark line under their bases only touch is mended from its own clean part
#    (the same square shifted along the floor), so its grain and light stay its own;
#  - a square mostly behind a pawn, or one of the squares along the strip whose colour the picture got wrong in the rows
#    the pawns stood in (the picture breaks its own checkerboard there), is replaced whole: clean squares of the right
#    colour, without their outer two pixels, warped onto it (quad to quad) and relit by a smooth light map of that colour;
#  - replaced squares are laid in with their exact (anti-aliased) shape, so every seam runs straight along the grid.
# Run: uv run --with numpy --with opencv-python-headless python clean_plate2.py (needs photo.png, mask_pawns.png and
# the first plate clean.png for the wall, all made by clean_plate.py) -> clean2.png
import cv2, numpy as np
im = cv2.imread('photo.png'); H, W = im.shape[:2]
old = cv2.imread('clean.png')
pawn = cv2.imread('mask_pawns.png', 0) > 0
CX, CY, F = 630.5, 348.0, 1253.0
TH = np.arctan((CY - 280.0) / F); S, C = np.sin(TH), np.cos(TH)

def to_floor(u, v):
    a = (u - CX) / F; b = -(v - CY) / F
    dy = b * C - S; dz = b * S + C; t = -1.0 / dy
    return t * a, t * dz

def to_img(X, Z):
    yc = -C + Z * S; zc = S + Z * C
    return CX + F * X / zc, CY - F * yc / zc

FLOOR = 397
ROWS = [397, 404, 412, 423, 437, 457, 484, 526, 600, 820]
ZR = [float(to_floor(CX, v)[1]) for v in ROWS]
COLS = [-4.65, -3.43, -2.226, -1.05, -0.05, 0.05, 1.053, 2.25, 3.44, 4.65]
NI, NJ = len(COLS) - 1, len(ROWS) - 1
SQUARES = [(i, j) for i in range(NI) for j in range(NJ) if i != 4]
SWAPPED = {(3, j) for j in range(7)} | {(5, j) for j in range(7)}

def logical(i): return i if i < 4 else i - 1
def is_light(i, j): return (logical(i) + j) % 2 == 0

def quad(i, j, flip=False):
    za, zb = ZR[j], ZR[j + 1]
    q = [to_img(COLS[i], za), to_img(COLS[i + 1], za), to_img(COLS[i + 1], zb), to_img(COLS[i], zb)]
    if flip: q = [q[1], q[0], q[3], q[2]]
    return np.array(q, np.float64)

def inset(q, d):
    # move every edge of a convex quad inwards by d pixels
    c = q.mean(0); lines = []
    for k in range(4):
        a, b = q[k], q[(k + 1) % 4]
        n = np.array([-(b - a)[1], (b - a)[0]]); n /= np.linalg.norm(n)
        if np.dot(c - a, n) < 0: n = -n
        lines.append((a + n * d, b - a))
    out = []
    for k in range(4):
        (p1, d1), (p2, d2) = lines[k - 1], lines[k]
        t = np.linalg.solve(np.array([d1, -d2]).T, p2 - p1)
        out.append(p1 + d1 * t[0])
    return np.array(out)

uu, vv = np.meshgrid(np.arange(W, dtype=np.float64), np.arange(H, dtype=np.float64))
Xf, Zf = to_floor(uu, np.maximum(vv, 300.0))
ti = np.searchsorted(COLS, Xf) - 1
tj = np.searchsorted(ROWS, vv, side='right') - 1
floor = vv >= FLOOR
tile = np.where(floor & (ti >= 0) & (ti < NI) & (tj >= 0) & (tj < NJ), ti * 100 + tj, -1)
strip = floor & (ti == 4)

def coverage(q, scale=4):
    # the share of each pixel inside quad q (anti-aliased), floor rows only
    m = np.zeros((H * scale, W * scale), np.uint8)
    cv2.fillPoly(m, [np.round(q * scale + (scale - 1) / 2.0).astype(np.int32)], 255)
    a = cv2.resize(m.astype(np.float32) / 255, (W, H), interpolation=cv2.INTER_AREA)
    a[:FLOOR] = 0
    return a

# --- what must go
d8 = (cv2.dilate(pawn.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21))) > 0).astype(np.uint8)
cv2.fillPoly(d8, [np.array([[226, 490], [400, 490], [400, 535], [292, 535]], np.int32)], 1)   # white's cast shadow
cv2.fillPoly(d8, [np.array([[650, 484], [700, 484], [700, 532], [650, 532]], np.int32)], 1)   # black's, by the strip
cv2.rectangle(d8, (392, 512), (614, 534), 1, -1)                                               # the dark line under
cv2.rectangle(d8, (646, 512), (896, 534), 1, -1)                                               # each base, to the strip
dirty = (d8 > 0) & floor & ~strip
img = im.astype(np.float32)

inner = np.zeros((H, W), bool)
for (i, j) in SQUARES:
    sel = (tile == i * 100 + j).astype(np.uint8)
    if sel.any(): inner |= cv2.erode(sel, np.ones((7, 7), np.uint8)) > 0

def sample(src, mx, my):
    return cv2.remap(src, np.asarray(mx, np.float32).reshape(-1, 1), np.asarray(my, np.float32).reshape(-1, 1),
                     cv2.INTER_LINEAR)[:, 0]

def smooth(src, m):
    # the light changes fast with depth (far rows are dim) and slowly across: wide along a row, narrow down the picture
    w = m.astype(np.float32); num = np.zeros_like(src); den = np.zeros((H, W), np.float32)
    for (sx, sy), a in [((10, 3), 1.0), ((30, 5), .4), ((80, 8), .15), ((220, 12), .05)]:
        den += a * cv2.GaussianBlur(w, (0, 0), sigmaX=sx, sigmaY=sy)
        num += a * cv2.GaussianBlur(src * w[..., None], (0, 0), sigmaX=sx, sigmaY=sy)
    return num / np.maximum(den, 1e-6)[..., None]

def lightmaps(src, light_px, dark_px):
    # brightness from the squares of each colour; the tint of the light (teal by the left wall, red by the right, warm
    # in the middle) from every clean pixel, so a square brought from beside a wall takes the tint of where it goes
    lum = np.repeat(src.mean(2, keepdims=True), 3, axis=2)
    tint = smooth(src / np.maximum(lum, 1.0), light_px | dark_px)
    return {light: smooth(lum, m) * tint for light, m in ((True, light_px), (False, dark_px))}

# light samples: clean pixels of every square, by its colour; the swapped squares' own visible parts count as the colour
# they really show (nearer the light map of that colour), which keeps the warm light in the middle of the room
clean = floor & ~dirty & ~strip & inner
light_px = np.zeros((H, W), bool); dark_px = np.zeros((H, W), bool)
for (i, j) in SQUARES:
    if (i, j) in SWAPPED: continue
    sel = (tile == i * 100 + j) & clean
    (light_px if is_light(i, j) else dark_px)[sel] = True
LM = lightmaps(img, light_px, dark_px)
for (i, j) in SWAPPED:
    sel = (tile == i * 100 + j) & clean
    if sel.sum() < 30: continue
    lum = img[sel].mean(); dl = abs(lum - LM[True][sel].mean()); dd = abs(lum - LM[False][sel].mean())
    (light_px if dl < dd else dark_px)[sel] = True
    print('swapped', (i, j), 'shows', 'light' if dl < dd else 'dark', 'should be', 'light' if is_light(i, j) else 'dark')
LM = lightmaps(img, light_px, dark_px)

# --- step 1: mend lightly touched squares from their own clean part (up to two shifts), else replace them
full, mend = set(SWAPPED), {}
for (i, j) in SQUARES:
    if (i, j) in full: continue
    sel = tile == i * 100 + j
    bad = sel & dirty
    if not bad.any(): continue
    # the far rows are thin bands: a shifted copy inside one would carry its seams along, so they are replaced
    if j <= 4 or bad.sum() / max(sel.sum(), 1) > .45: full.add((i, j)); continue
    ys, xs = np.nonzero(bad)
    X, Z = Xf[ys, xs], Zf[ys, xs]
    w, dz = COLS[i + 1] - COLS[i], ZR[j] - ZR[j + 1]
    todo = np.ones(len(ys), bool); shifts = []
    for _ in range(2):
        best = None
        for fx in np.linspace(-.9, .9, 19):
            for fz in np.linspace(-.9, .9, 19):
                u2, v2 = to_img(X + fx * w, Z + fz * dz)
                ui, vi = np.clip(np.round(u2).astype(int), 0, W - 1), np.clip(np.round(v2).astype(int), 0, H - 1)
                ok = (u2 >= 1) & (u2 < W - 2) & (v2 >= FLOOR) & (v2 < H - 2) & sel[vi, ui] & clean[vi, ui]
                gain = (ok & todo).mean() - .03 * (abs(fx) + abs(fz))
                if best is None or gain > best[0]: best = (gain, fx, fz, ok)
        if best[0] <= 0: break
        shifts.append((best[1], best[2], best[3] & todo)); todo &= ~best[3]
        if todo.mean() < .01: break
    if todo.mean() > .1: full.add((i, j)); continue
    mend[(i, j)] = (ys, xs, shifts)

out = img.copy()
for (i, j), (ys, xs, shifts) in mend.items():
    sel = tile == i * 100 + j
    lm = LM[is_light(i, j)]
    a8 = np.zeros((H, W), np.uint8); a8[ys, xs] = 255
    ring = cv2.dilate(a8, np.ones((7, 7), np.uint8)) > 0
    alpha = np.maximum(cv2.GaussianBlur(ring.astype(np.float32), (0, 0), 1.6), a8 / 255.0) * sel
    ry, rx = np.nonzero(alpha > 0)
    patch = np.zeros((len(ry), 3), np.float32); done = np.zeros(len(ry), bool)
    RX, RZ = Xf[ry, rx], Zf[ry, rx]
    w, dz = COLS[i + 1] - COLS[i], ZR[j] - ZR[j + 1]
    for fx, fz, _ in shifts:
        u2, v2 = to_img(RX + fx * w, RZ + fz * dz)
        ui, vi = np.clip(np.round(u2).astype(int), 0, W - 1), np.clip(np.round(v2).astype(int), 0, H - 1)
        ok = ~done & sel[vi, ui] & clean[vi, ui]
        patch[ok] = sample(img, u2[ok], v2[ok]) * np.clip(lm[ry[ok], rx[ok]] / np.maximum(sample(lm, u2[ok], v2[ok]), 1.0), .3, 3)
        done |= ok
    patch[~done] = img[ry[~done], rx[~done]]
    a = alpha[ry, rx][:, None]
    out[ry, rx] = out[ry, rx] * (1 - a) + patch * a
    print('mend', (i, j), 'shifts', [(round(fx, 2), round(fz, 2)) for fx, fz, _ in shifts])

# --- step 2: replace whole squares from clean ones of the same colour (mended squares count as clean)
print('replace', sorted(full))
ok_src = floor & ~strip
for (i, j) in full: ok_src &= tile != i * 100 + j
ok_src &= ~(dirty & ~np.isin(tile, [i * 100 + j for (i, j) in mend]))
src_img = out.copy()
num = np.zeros_like(img); den = np.zeros((H, W), np.float32)
used = {}
for (i, j) in sorted(full, key=lambda t: (-t[1], abs(logical(t[0]) - 3.5))):
    cov = coverage(quad(i, j))
    ys, xs = np.nonzero(cov > 0)
    pts = np.stack([xs, ys], 1).astype(np.float64)[None]
    th = ROWS[j + 1] - ROWS[j]
    want = is_light(i, j)
    cands = []
    for (si, sj) in SQUARES:
        if (si, sj) in full or is_light(si, sj) != want: continue
        sh = (ROWS[sj + 1] if sj < NJ - 1 else 700) - ROWS[sj]
        for flip in (False, True):
            Hm = cv2.getPerspectiveTransform(quad(i, j).astype(np.float32), inset(quad(si, sj, flip), 2.0).astype(np.float32))
            m = cv2.perspectiveTransform(pts, Hm)[0]
            ui, vi = np.round(m[:, 0]).astype(int), np.round(m[:, 1]).astype(int)
            ok = (ui >= 1) & (ui < W - 1) & (vi >= FLOOR) & (vi < H - 1)
            ok[ok] = ok_src[vi[ok], ui[ok]]
            cover = ok.mean()
            if cover < .25: continue
            mag = max(th / max(sh, 1), 1.0)
            # the picture's left and right edges are darkened and tinted by the walls: keep away from them
            edge = np.mean((m[:, 0] < 90) | (m[:, 0] > W - 90))
            cost = (1 - cover) * 6 + 2.5 * np.log(mag) + .5 * abs(logical(si) - logical(i)) + .7 * abs(sj - j) + 4 * edge
            cost += 2.0 * ((si < 4) != (i < 4))                 # the other side has the other wall's tint
            for (a, b), s2 in used.items():
                if s2 == (si, sj) and abs(logical(a) - logical(i)) + abs(b - j) <= 2: cost += 1.5
            cands.append((cost, si, sj, flip, m, ok))
    cands.sort(key=lambda c: c[0])
    todo = np.ones(len(ys), bool); val = np.zeros((len(ys), 3), np.float32)
    lm = LM[want]; chosen = []
    for cost, si, sj, flip, m, ok in cands:
        take = todo & ok
        if not take.any(): continue
        mx, my = m[take, 0], m[take, 1]
        val[take] = sample(src_img, mx, my) * np.clip(lm[ys[take], xs[take]] / np.maximum(sample(lm, mx, my), 1.0), .3, 3)
        todo &= ~take; chosen.append(((si, sj), flip, round(float(ok.mean()), 2)))
        if not todo.any(): break
    if todo.any():
        # what no candidate covers cleanly (a pixel or two at the picture's edge): the first one anyway
        print('  !!', (i, j), 'left', int(todo.sum()))
        m = cands[0][4]
        mx, my = np.clip(m[todo, 0], 1, W - 2), np.clip(m[todo, 1], FLOOR, H - 2)
        val[todo] = sample(src_img, mx, my) * np.clip(lm[ys[todo], xs[todo]] / np.maximum(sample(lm, mx, my), 1.0), .3, 3)
    used[(i, j)] = chosen[0][0]
    a = cov[ys, xs]
    num[ys, xs] += val * a[:, None]; den[ys, xs] += a
    print('replace', (i, j), 'light' if want else 'dark', '<-', chosen[:3])

lay = den > 0
den_c = np.clip(den, 0, 1)
den_c[dirty] = 1.0          # at a square's rim, a pawn's pixel never shows through
out[lay] = (num[lay] / np.maximum(den[lay], 1e-6)[:, None]) * den_c[lay][:, None] + out[lay] * (1 - den_c[lay][:, None])
res = np.clip(out, 0, 255).astype(np.uint8)
res[:FLOOR] = old[:FLOOR]
# the foot of the wall behind the pawns' necks: straight across from both sides (the first plate left a bright line)
wide = cv2.dilate(pawn.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21))) > 0
for y in range(FLOOR - 9, FLOOR + 1):
    xs = np.flatnonzero(wide[y])
    if len(xs) == 0: continue
    cuts = np.flatnonzero(np.diff(xs) > 1)
    for a, b in zip(np.r_[xs[0], xs[cuts + 1]], np.r_[xs[cuts], xs[-1]]):
        l = im[y, max(0, a - 6):a - 1].astype(np.float32).mean(0); r = im[y, b + 2:min(W, b + 7)].astype(np.float32).mean(0)
        t = (np.arange(a, b + 1) - a + 1) / (b - a + 2)
        res[y, a:b + 1] = np.clip(l[None] * (1 - t[:, None]) + r[None] * t[:, None], 0, 255).astype(np.uint8)
cv2.imwrite('clean2.png', res)
vis = res.copy()
for v in ROWS: cv2.line(vis, (0, v), (W, v), (0, 255, 0), 1)
for X in COLS:
    a = to_img(X, ZR[0]); b = to_img(X, ZR[-1])
    cv2.line(vis, (int(a[0]), int(a[1])), (int(b[0]), int(b[1])), (0, 255, 0), 1)
for (i, j) in full: cv2.polylines(vis, [np.round(quad(i, j)).astype(np.int32)], True, (0, 0, 255), 1)
for (i, j) in mend: cv2.polylines(vis, [np.round(quad(i, j)).astype(np.int32)], True, (255, 0, 255), 1)
cv2.imwrite('clean2_grid.png', vis)
cv2.imwrite('clean2_cmp.png', np.vstack([res[370:696, 100:1160], im[370:696, 100:1160]]))
cv2.imwrite('z2a.png', cv2.resize(res[385:545, 200:660], None, fx=2, fy=2, interpolation=cv2.INTER_NEAREST))
cv2.imwrite('z2b.png', cv2.resize(res[385:545, 600:1060], None, fx=2, fy=2, interpolation=cv2.INTER_NEAREST))
