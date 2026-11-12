# R548 (11-11): a flat look at the Pawn Rush course board (LoadingStudio.Board.cs) before it goes into Unity: the path's
# squares, stops, start pad, castle and the two pawns through the studio camera, the stop name plates where the screen
# puts them, and the entrance screen's own parts on top (title, team lists, progress line, tip) at 1280 x 720.
# Run: uv run --with numpy --with opencv-python-headless python board_preview.py out.png [aspect]
import cv2, numpy as np, sys, json, math
out = sys.argv[1]
aspect = float(sys.argv[2]) if len(sys.argv) > 2 else 16 / 9
L = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else None
H = 720; W = int(round(H * aspect)); SC = W / 1280.0
# Board.cs BoardEye, BoardAim, BoardFov and TokenTall
cfg = dict(cam=[0, 25.4, -17.3], look=[0, 0, 0.5], fov=26.0, pawn=1.2, tile=1.0)
if L: cfg.update(L.get('cfg', {}))
C = np.array(cfg['cam'], float); T = np.array(cfg['look'], float)
f = T - C; f /= np.linalg.norm(f)
r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r)      # Unity: right = up x forward (left-handed, computed as numpy)
r = -r if r[0] < 0 else r
u = np.cross(f, r); u = u if u[1] > 0 else -u
fov = cfg['fov']
# narrower than 16:9: widen the view so the board keeps its width (LoadingStudio.Board.cs does the same)
if aspect < 16 / 9: fov = 2 * math.degrees(math.atan(math.tan(math.radians(fov / 2)) * (16 / 9) / aspect))
k = (H / 2) / math.tan(math.radians(fov / 2))
def P(p):
    d = np.array(p, float) - C
    x, y, z = d @ r, d @ u, d @ f
    return (W / 2 + x / z * k, H / 2 - y / z * k)
def ipt(q): return (int(round(q[0])), int(round(q[1])))

img = np.zeros((H, W, 3), np.uint8); img[:] = (12, 18, 26)       # BGR walnut dark
# table glow under the lamp
yy, xx = np.mgrid[0:H, 0:W]; c0 = P([0, 0, 0.3])
g = np.exp(-(((xx - c0[0]) / (W * .38)) ** 2 + ((yy - c0[1]) / (H * .42)) ** 2))
img = (img + (g[..., None] * np.array([30, 52, 78]))).clip(0, 255).astype(np.uint8)

ZONE = [((195, 221, 233), (73, 106, 138)), ((122, 165, 201), (36, 59, 90)), ((207, 180, 189), (86, 54, 62)), ((126, 201, 236), (34, 90, 122))]
from board_layout import layout
lay = layout(cfg['tile'])
def quad(cx, cz, half, y, col, edge=None):
    pts = [P([cx - half, y, cz - half]), P([cx + half, y, cz - half]), P([cx + half, y, cz + half]), P([cx - half, y, cz + half])]
    pts = np.array([ipt(q) for q in pts], np.int32)
    cv2.fillConvexPoly(img, pts, col, cv2.LINE_AA)
    if edge: cv2.polylines(img, [pts], True, edge, 2, cv2.LINE_AA)
# squares
for n, (x, z, zone, stop) in enumerate(lay['squares']):
    light, dark = ZONE[zone]
    half = lay['tile'] * (.62 if stop >= 0 else .45)
    quad(x, z, half, .12 if stop >= 0 else .08, light if n % 2 == 0 else dark, (74, 176, 232) if stop >= 0 else None)
sx, sz = lay['start']; quad(sx, sz, .75, .1, (200, 200, 200), (230, 230, 230))
# castle
cx, cz = lay['castle']; cw, ch, cd = lay['castle_size']
for (a, b) in [((cx - cw / 2, 0, cz - cd / 2), (cx + cw / 2, ch + .1, cz - cd / 2))]:
    p0 = ipt(P(a)); p1 = ipt(P(b)); cv2.rectangle(img, (p0[0], p1[1]), (p1[0], p0[1]), (40, 60, 90), -1); cv2.rectangle(img, (p0[0], p1[1]), (p1[0], p0[1]), (74, 176, 232), 2)
# the crown on the keep (KingCrown, .42 over the top)
cr = P([cx, .1 + ch + .42, cz]); cv2.circle(img, ipt(cr), max(3, int(10 * SC)), (102, 184, 227), -1, cv2.LINE_AA)
# pawns at the first stop and at the start
for team, (x, z) in enumerate(lay['tokens_demo']):
    b = P([x, 0, z]); t = P([x, cfg['pawn'], z]); w = abs(P([x + .25, 0, z])[0] - b[0])
    cv2.rectangle(img, (int(b[0] - w), int(t[1])), (int(b[0] + w), int(b[1])), (215, 235, 243) if team == 0 else (63, 48, 40), -1)
    cv2.rectangle(img, (int(b[0] - w), int(t[1])), (int(b[0] + w), int(b[1])), (60, 60, 60), 1)
# plates
def tw(s, px): return sum(px * (.3 if ch == ' ' else 1.0 if ord(ch) > 0x3000 else .55) for ch in s)
for i, (x, z) in enumerate(lay['stops']):
    name = lay['longest'][i]; mg = i in (1, 3, 5); fin = i == 6
    a = P(lay["final_plate"]) if fin else P([x, lay["plate_up"], z])
    pw = (tw(name, 17 if mg or fin else 14) + (28 if mg or fin else 22)) * SC; ph = (46 if mg or fin else 38) * SC
    x0, y0 = int(a[0] - pw / 2), int(a[1] - ph)
    cv2.rectangle(img, (x0, y0), (int(x0 + pw), int(a[1])), (16, 22, 30), -1)
    cv2.rectangle(img, (x0, y0), (int(x0 + pw), int(a[1])), (74, 176, 232) if mg or fin else (150, 170, 190), 1)
    cv2.putText(img, 'S%d' % (i + 1), (x0 + 6, y0 + 16), cv2.FONT_HERSHEY_SIMPLEX, .4, (200, 220, 240), 1, cv2.LINE_AA)
    cv2.line(img, ipt(a), ipt(P(lay["final_tail"]) if fin else P([x, cfg['pawn'] + .12, z])), (74, 176, 232), 1)
# the entrance screen on top: the panel matches the screen width (1280 wide, 1280 / aspect tall); bottom parts sit on its bottom
s = W / 1280.0; PH = 1280.0 / aspect
def box(x0, y0, x1, y1, col, txt=None, bottom=False):
    if bottom: y0, y1 = PH - y0, PH - y1
    a, b = (int(x0 * s), int(y0 * s)), (int(x1 * s), int(y1 * s))
    cv2.rectangle(img, a, b, col, 1)
    if txt: cv2.putText(img, txt, (a[0] + 4, a[1] + 14), cv2.FONT_HERSHEY_SIMPLEX, .4, col, 1, cv2.LINE_AA)
box(440, 22, 840, 128, (90, 200, 255), "title / rule / note")
box(30, 148, 320, 534, (255, 200, 120), "white list"); box(960, 148, 1250, 534, (120, 120, 255), "black list")
box(340, 104, 940, 62, (200, 200, 200), "progress", True); box(340, 40, 940, 16, (200, 200, 200), "tip", True)
box(330, 152, 950, 112, (120, 120, 120), "buttons (matching only)", True)
cv2.imwrite(out, img)
print(json.dumps({'hops': lay['hops'], 'start': [round(v) for v in P([lay['start'][0], 0, lay['start'][1]])], 'castle_right': round(P([lay['castle'][0] + lay['castle_size'][0] / 2, 0, lay['castle'][1]])[0])}))
