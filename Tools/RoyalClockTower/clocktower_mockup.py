# Royal Clock Tower (course 2 draft) - the layout drawings, from one coordinate table.
# Usage: python clocktower_mockup.py <out_dir> [R-number]
# Writes <R>_RoyalClockTower.svg (plan + side view), <R>_RoyalClockTower_Iso.svg and <R>_RoyalClockTower_Devices.svg.
import math
import os
import sys

OUT = sys.argv[1]
RN = sys.argv[2] if len(sys.argv) > 2 else "R775"

FONT = "'Malgun Gothic','Noto Sans KR','Apple SD Gothic Neo',sans-serif"
BG, INK, SUB, PIT, EDGE = "#171a22", "#f4ead2", "#a39a85", "#0f1320", "#3b4257"
REST = "#5DCAA5"      # floors and ledges: stamina comes back
CLIMB = "#EF9F27"     # faces to climb
RIDE = "#AFA9EC"      # things that move and carry a climber
DOWN = "#85B7EB"      # ways down
STONE = "#2c3344"     # solid, not part of the road
WARN = "#F0997B"      # the combs
YEL = "#FAC775"
GREY = "#8b93a8"

# ---------------------------------------------------------------- the table (metres, section local: x east, y up, z north)
Y_YARD, Y_GAL, Y_F, Y_DRUM, Y_COURT, Y_BAL, Y_BELL, Y_EXIT = 3.5, 12.0, 5.0, 10.1, 15.0, 23.0, 28.0, 22.0
WEIGHT_X = [-15.5, -12.0, -6.0, -2.5]
WEIGHT_Z = (20.9, 23.9)
WEIGHT_PHASE = [0, 6, 3, 9]
PIERS = [(8.0, 10.4), (13.6, 16.0)]            # x ranges; z 37 .. 39.4, top 9
PIER_Z = (37.0, 39.4)
ISLAND = (5.0, 19.0, 38.6, 44.0)               # x0, x1, z0, z1
ROPE_X = [6.0, 9.0, 12.0, 15.0, 18.0]
ROPE_PIVOT = (48.0, 11.1)                      # z, y
ROPE_LEN = 6.5
BARREL = (2.0, 66.0, 5.0)                      # centre x, centre z, radius
COMB1 = (70.0, Y_F, 8.2)                       # azimuth, blocks from, to
COMB2 = (135.0, 7.4, Y_DRUM)
MALLET_X = [-20.0, -16.0, -12.0, -8.0]
MALLET_Y = [7.6, 10.1, 12.6]
DIAL = (0.0, 21.5, 6.0)                        # centre x, centre y, radius, on the plane z = 90
PERCH = [(3.0, 18.8, 1.8, "A"), (5.2, 21.8, 1.6, "B"), (7.2, 24.6, 1.2, "C"),
         (-3.0, 18.8, 1.8, "A"), (-5.2, 21.8, 1.6, "B"), (-7.2, 24.6, 1.2, "C"), (0.0, 22.2, 2.2, "hub")]
CORNICE = 26.2
CP = [("CP0", 0, 0, 3), ("CP1", 0, Y_YARD, 13), ("CP2", -6, Y_GAL, 29), ("CP3", 10, Y_F, 30), ("CP4", 12, Y_F, 41.6),
      ("CP5", 14, Y_F, 55), ("CP6", -12, Y_F, 66), ("CP7", -4, Y_COURT, 82), ("CP8", 0, Y_BELL, 96), ("CP9", 0, Y_EXIT, 116)]


def barrel_pt(az_deg, r):
    a = math.radians(az_deg)
    return BARREL[0] + r * math.cos(a), BARREL[1] + r * math.sin(a)


LEDGE_X = (-6.0, -2.0)                          # the high ledge on the hammer wall's east end, at the drum's height
CAT_A = barrel_pt(122.3, 5.0)                   # the plank from the drum's top ...
CAT_B = (-4.0, 75.0)                            # ... to the high ledge


def path3d():
    p = [(0, 0, 0), (0, 0, 10), (0, Y_YARD, 10), (-6, Y_YARD, 20.8), (-6, 7.5, 20.9), (-6, 7.5, 22.4),
         (-6, Y_GAL, 22.4), (-6, Y_GAL, 29), (2, Y_GAL, 31.5), (2.5, 8.5, 31.5), (3.4, Y_F, 31.5),
         (9.8, Y_F, 36.9), (9.8, 5.6, 37.0), (10.5, 5.6, 36.95), (10.7, 5.6, 39.1), (10.9, Y_F, 39.9), (12, Y_F, 43.9)]
    for i in range(0, 11):                      # the swing
        a = math.radians(-40 + 80 * i / 10.0)
        p.append((12, ROPE_PIVOT[1] - ROPE_LEN * math.cos(a) - 1.1, ROPE_PIVOT[0] + ROPE_LEN * math.sin(a)))
    p += [(12, Y_F, 52.2), (12, Y_F, 56), (9.5, Y_F, 65.4)]
    keys = [(350, 5.5), (30, 7.4), (70, 8.7), (100, 8.2), (135, 6.9), (160, 5.9), (180, 5.5)]   # (azimuth, hips)
    for k in range(len(keys) - 1):
        a0, y0 = keys[k]
        a1, y1 = keys[k + 1]
        if a1 < a0:
            a1 += 360
        for i in range(0, 6):
            u = i / 5.0
            x, z = barrel_pt(a0 + (a1 - a0) * u, BARREL[2] + 0.3)
            p.append((x, y0 + (y1 - y0) * u - 0.5, z))
    p += [(-6, Y_F, 66), (-12, Y_F, 75.3), (-12, 7.6, 75.5), (-12, 10.1, 75.5), (-12, 12.6, 75.5), (-12, Y_COURT, 76.4),
          (3, Y_COURT, 89.4), (3, 18.8, 89.5), (5.2, 21.8, 89.5), (7.2, 24.6, 89.5), (8.3, 25.0, 89.75), (8.35, 25.6, 90.6),
          (8.35, Y_BELL, 91.2), (7.0, Y_BELL, 91.4), (0, Y_BELL, 96), (0, Y_BELL, 100), (0, Y_EXIT, 112), (0, Y_EXIT, 120)]
    return p


PATH = path3d()


def path_length():
    return sum(math.dist(PATH[i], PATH[i + 1]) for i in range(len(PATH) - 1))


# ---------------------------------------------------------------- svg helpers
class Svg:
    def __init__(self, w, h, title):
        self.out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="100%" '
                    f'style="height:auto;max-width:{w}px" font-family="{FONT}">',
                    f'<title>{title}</title>', '<defs>'
                    + "".join(f'<marker id="tip{n}" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" '
                              f'orient="auto"><path d="M0,0 L10,5 L0,10 Z" fill="{c}"/></marker>'
                              for n, c in [("", "#ffffff"), ("d", BG), ("y", YEL), ("s", SUB)]) + '</defs>',
                    f'<rect width="{w}" height="{h}" fill="{BG}"/>']

    def add(self, s):
        self.out.append(s)

    def rect(self, x, y, w, h, fill, stroke="none", sw=1, op=1, extra=""):
        self.add(f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" fill="{fill}" stroke="{stroke}" '
                 f'stroke-width="{sw}" opacity="{op}" {extra}/>')

    def line(self, x1, y1, x2, y2, stroke, sw=1, dash="", extra=""):
        d = f' stroke-dasharray="{dash}"' if dash else ""
        self.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{stroke}" '
                 f'stroke-width="{sw}"{d} {extra}/>')

    def poly(self, pts, fill, stroke="none", sw=1, op=1, dash="", close=True, extra=""):
        s = " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)
        d = f' stroke-dasharray="{dash}"' if dash else ""
        self.add(f'<{"polygon" if close else "polyline"} points="{s}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" '
                 f'opacity="{op}"{d} stroke-linejoin="round" {extra}/>')

    def circle(self, x, y, r, fill, stroke="none", sw=1, op=1):
        self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r:.1f}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" opacity="{op}"/>')

    def text(self, x, y, s, size=13, fill=INK, weight=400, anchor="start", extra=""):
        self.add(f'<text x="{x:.1f}" y="{y:.1f}" font-size="{size}" font-weight="{weight}" fill="{fill}" '
                 f'text-anchor="{anchor}" {extra}>{s}</text>')

    def badge(self, x, y, n, r=11):
        self.circle(x, y, r, "#ffffff", BG, 1.5)
        self.text(x, y + 4.5, str(n), 13, BG, 700, "middle")

    def notes(self, x, y, lines, size=12, gap=18):
        for i, s in enumerate(lines):
            self.text(x, y + i * gap, s, size, SUB)

    def save(self, name):
        self.out.append("</svg>")
        with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(self.out) + "\n")


NAMES = ["배우는 벽", "짝 추", "낙하 벽", "모서리 기둥(ㄱ자)", "종 줄", "편종 원통(반원)", "망치 벽", "대시계판", "구리 지붕"]


# ---------------------------------------------------------------- 1. plan + side view
def plan_and_side():
    S, X0, PY, EY = 9.0, 78.0, 112.0, 896.0
    sv = Svg(1250, 1180, f"{RN} 왕실 시계탑 - 위에서 본 그림과 옆에서 본 그림")
    sv.text(24, 36, f"코스 2 · 왕실 시계탑 레벨 디자인 초안 v0.1 ({RN})", 21, INK, 700)
    sv.text(24, 60, "계단이 없는 코스 - 벽이 길이다. 위: 위에서 본 그림(북쪽이 오른쪽, 서쪽이 위) · "
            "아래: 동쪽에서 본 옆 그림(같은 눈금). 흰 점선 = 진행 경로, 단위 m", 13, SUB)

    def px(z): return X0 + z * S
    def py(x): return PY + (x + 22) * S
    def ey(y): return EY - y * S

    def prect(z0, z1, x0, x1, fill, stroke="none", sw=1, op=1):
        sv.rect(px(z0), py(x0), (z1 - z0) * S, (x1 - x0) * S, fill, stroke, sw, op)

    # ---- plan
    prect(0, 120, -22, 22, PIT, EDGE, 1)
    prect(0, 10, -22, -12, STONE)
    prect(0, 10, 12, 22, STONE)
    prect(0, 10, -12, 12, REST, op=.55)                # entry floor
    prect(10, 24, -22, 22, REST, op=.62)               # weight yard
    prect(24, 34, -22, 2, REST, op=.9)                 # gallery
    prect(24, 37, 2, 22, REST, op=.7)                  # ringers' floor
    prect(ISLAND[2], ISLAND[3], ISLAND[0], ISLAND[1], REST, op=.7)
    prect(52, 58, 2, 22, REST, op=.7)
    prect(58, 76, 10, 22, REST, op=.7)
    prect(64.2, 66.6, 7.05, 10, REST, op=.7)           # east tongue
    prect(58, 76, -22, -6, REST, op=.7)                # west floor
    prect(64.8, 67.2, -6, -3.05, REST, op=.7)          # west tongue
    prect(76, 90, -22, 22, REST, op=.92)               # clock court
    prect(90, 100, -8, 8, REST, op=1)                  # belfry
    prect(90, 94, 8, 12, REST, op=.97)                 # side balconies
    prect(90, 94, -12, -8, REST, op=.97)
    prect(112, 120, -8, 8, REST, op=.97)               # exit terrace
    prect(100, 112, -8, 8, DOWN, op=.8)                # the copper roof
    for k in range(5):
        zz = 101.5 + k * 2.2
        sv.poly([(px(zz), py(-5)), (px(zz + 1.1), py(0)), (px(zz), py(5))], "none", "#ffffff", 1.2, .55, close=False)
    for (gz, gx, gr) in [(41, -14, 5.5), (48, -5.5, 4.2), (52.5, -15.5, 3.4)]:
        sv.circle(px(gz), py(gx), gr * S, "none", EDGE, 2)
        sv.circle(px(gz), py(gx), 1.2 * S, "none", EDGE, 2)
    sv.text(px(39.6), py(-3.2), "기계 구덩이(그림)", 12, "#6b7389", 400, "middle")
    sv.text(px(110), py(15), "탑 밖(하늘)", 12, "#6b7389", 400, "middle")
    # faces to climb
    sv.line(px(10), py(-12), px(10), py(12), CLIMB, 5)
    for a, b in [(-22, -17), (-10.5, -7.5), (-1, 2)]:
        sv.line(px(24), py(a), px(24), py(b), CLIMB, 5)
    sv.line(px(24), py(2), px(34), py(2), DOWN, 5)                           # the drop (the movement's east face)
    for (a, b) in PIERS:
        prect(PIER_Z[0], PIER_Z[1], a, b, STONE, CLIMB, 3)
    sv.line(px(76), py(-22), px(76), py(-6), CLIMB, 5)                       # the hammer wall
    sv.line(px(76), py(LEDGE_X[0]), px(76), py(LEDGE_X[1]), CLIMB, 5)
    sv.line(px(90), py(-8), px(90), py(8), CLIMB, 6)                         # the dial
    sv.line(px(90), py(8), px(93.5), py(8), CLIMB, 5)                        # the side faces above the balconies
    sv.line(px(90), py(-8), px(93.5), py(-8), CLIMB, 5)
    # what cannot be climbed: the grille and the rails
    sv.line(px(24), py(2), px(24), py(22), GREY, 2, "5 4")
    sv.line(px(34), py(-22), px(34), py(2), GREY, 1.5)
    sv.line(px(34), py(2), px(37), py(2), GREY, 1.5)
    sv.line(px(52), py(2), px(58), py(2), GREY, 1.5)
    sv.line(px(76), py(-2), px(76), py(22), GREY, 1.5)
    # weights
    for i, wx in enumerate(WEIGHT_X):
        prect(WEIGHT_Z[0], WEIGHT_Z[1], wx - 1.5, wx + 1.5, RIDE, "#ffffff", 1)
        sv.text(px(22.4), py(wx) + 4, "↑" if i % 2 == 0 else "↓", 13, BG, 700, "middle")
    # ropes
    for rx in ROPE_X:
        sv.line(px(43.8), py(rx), px(52.2), py(rx), RIDE, 2.5)
        sv.circle(px(48), py(rx), 3.2, RIDE)
    # the drum
    bz, bx = px(BARREL[1]), py(BARREL[0])
    sv.circle(bz, bx, BARREL[2] * S, RIDE, "#ffffff", 1.2, .95)
    sv.circle(bz, bx, 0.6 * S, BG)
    for az in (COMB1[0], COMB2[0]):
        x1, z1 = barrel_pt(az, 5.15)
        x2, z2 = barrel_pt(az, 6.9)
        sv.line(px(z1), py(x1), px(z2), py(x2), WARN, 5)
    arc = [(px(z), py(x)) for x, z in (barrel_pt(-20 + i * (215 / 24.0), 3.2) for i in range(25))]
    sv.poly(arc, "none", BG, 1.6, 1, close=False, extra='marker-end="url(#tipd)"')
    # the plank from the drum's top to the high ledge
    sv.line(px(CAT_A[1]), py(CAT_A[0]), px(CAT_B[1]), py(CAT_B[0]), REST, 7, extra='opacity=".95"')
    prect(75, 76, LEDGE_X[0], LEDGE_X[1], REST, op=.97)
    sv.line(px(CAT_A[1]), py(CAT_A[0]), px(CAT_B[1]), py(CAT_B[0]), YEL, 1.6, "5 4")
    for mx in MALLET_X:
        prect(75.1, 76, mx - 0.9, mx + 0.9, RIDE, "#ffffff", .8)
    prect(89.1, 90, -7.8, 7.8, CLIMB, op=.5)
    for name, cx, cy, cz in CP:
        sv.line(px(cz), py(cx), px(cz), py(cx) - 13, "#ffffff", 1.4)
        sv.poly([(px(cz), py(cx) - 13), (px(cz) + 9, py(cx) - 10), (px(cz), py(cx) - 7)], "#ffffff")
    sv.poly([(px(z), py(x)) for (x, y, z) in PATH], "none", "#ffffff", 1.8, 1, "6 4", close=False, extra='marker-end="url(#tip)"')
    for (z, x, s) in [(5, -8.5, "0"), (15, 8, "3.5"), (29, -19.5, "12"), (30, 18, "5"), (41.4, 6.7, "5"), (55, 19, "5"),
                      (70, 18.5, "5"), (62, -19, "5"), (83, 17, "15"), (95, -5.5, "28"), (116, -5.5, "22")]:
        sv.text(px(z), py(x) + 4, "높이 " + s, 11.5, BG, 700, "middle")
    for n, z, x in [(1, 10, -16.5), (2, 18, -19.2), (3, 30.5, 5.2), (4, 37.4, 19.2), (5, 48, 0.2), (6, 60.2, -3.6),
                    (7, 73.4, -14), (8, 90, -15), (9, 106, -10.6)]:
        sv.badge(px(z), py(x), n)
    sv.text(px(0) - 8, py(0) + 4, "입구", 13, INK, 700, "end")
    sv.text(px(120) + 8, py(0) + 4, "출구", 13, INK, 700)
    sv.text(px(0) - 8, py(-22) + 12, "서", 12, SUB, 400, "end")
    sv.text(px(0) - 8, py(22) - 2, "동", 12, SUB, 400, "end")
    sv.line(px(108), py(-19.5), px(117), py(-19.5), SUB, 1.5, extra='marker-end="url(#tips)"')
    sv.text(px(107), py(-19.5) + 4, "북", 12, SUB, 400, "end")
    for z in range(0, 121, 10):
        sv.line(px(z), py(22), px(z), py(22) + 5, SUB, 1)
        sv.text(px(z), py(22) + 18, str(z), 11, SUB, 400, "middle")

    # ---- side view (from the east, looking west): west things first
    sv.text(24, EY - 33 * S - 14, "옆에서 본 그림 - 오르고(주황) 쉬고(초록) 실려 가고(보라) 내려간다(파랑)", 14, INK, 700)

    def erect(z0, z1, y0, y1, fill, stroke="none", sw=1, op=1):
        sv.rect(px(z0), ey(y1), (z1 - z0) * S, (y1 - y0) * S, fill, stroke, sw, op)

    erect(0, 120, -2.6, -2, PIT, EDGE, 1)
    for yy in range(0, 31, 5):
        sv.line(px(0), ey(yy), px(120), ey(yy), EDGE, .6, "2 5")
        sv.text(px(0) - 8, ey(yy) + 4, str(yy), 11, SUB, 400, "end")
    sv.text(px(0) - 34, ey(15), "높이", 11, SUB, 400, "middle", f'transform="rotate(-90 {px(0) - 34:.1f} {ey(15):.1f})"')
    erect(0, 10, -2, 0, STONE, EDGE)
    erect(10, 24, -2, Y_YARD, STONE, EDGE)
    erect(24, 34, -2, Y_GAL, STONE, EDGE)
    erect(58, 76, -2, Y_F, STONE, EDGE)
    erect(76, 90, -2, Y_COURT, STONE, EDGE)
    erect(90, 100, -2, Y_BELL, STONE, EDGE)
    sv.poly([(px(100), ey(-2)), (px(100), ey(Y_BELL)), (px(112), ey(Y_EXIT)), (px(112), ey(-2))], STONE, EDGE)
    erect(112, 120, -2, Y_EXIT, STONE, EDGE)
    # a weight: the head that can be held (down), and where it goes (up)
    erect(WEIGHT_Z[0], WEIGHT_Z[1], Y_YARD, 7.5, RIDE, "#ffffff", 1, .95)
    erect(WEIGHT_Z[0], WEIGHT_Z[1], 8.0, Y_GAL, RIDE, "#ffffff", 1, .35)
    sv.line(px(22.4), ey(8.2), px(22.4), ey(11.4), "#ffffff", 1.5, extra='marker-end="url(#tip)"')
    sv.line(px(22.4), ey(Y_GAL), px(22.4), ey(17.2), RIDE, 1.2, "3 3")
    # the drum behind the east floor, its combs and the plank
    erect(BARREL[1] - 5, BARREL[1] + 5, -2, Y_DRUM, RIDE, "#ffffff", 1, .8)
    erect(70.7, 72.0, COMB1[1], COMB1[2], WARN, "none", 1, .95)
    erect(69.4, 70.7, COMB2[1], COMB2[2], WARN, "none", 1, .6)
    sv.line(px(70.3), ey(Y_DRUM), px(76), ey(Y_DRUM), REST, 4)
    # east side floors (nearer)
    erect(24, 37, -2, Y_F, "#343c50", EDGE)
    erect(ISLAND[2], ISLAND[3], -2, Y_F, "#343c50", EDGE)
    erect(PIER_Z[0], PIER_Z[1], Y_F, 9.0, STONE, CLIMB, 2.5)
    erect(52, 58, -2, Y_F, "#343c50", EDGE)
    erect(58, 76, -2, Y_F, "#343c50", EDGE, 1, .55)
    erect(24, 34, Y_F, Y_GAL, DOWN, DOWN, 2, .35)            # the drop: the movement's east face
    sv.line(px(29), ey(8.5), px(34), ey(8.5), REST, 4)       # its half-way ledge
    for z0, z1, y in [(0, 10, 0), (10, 20.9, Y_YARD), (24, 34, Y_GAL), (24, 37, Y_F), (ISLAND[2], ISLAND[3], Y_F),
                      (52, 76, Y_F), (76, 90, Y_COURT), (90, 100, Y_BELL), (112, 120, Y_EXIT)]:
        sv.line(px(z0), ey(y), px(z1), ey(y), REST, 4)
    sv.line(px(WEIGHT_Z[0]), ey(7.5), px(WEIGHT_Z[1]), ey(7.5), REST, 3)
    sv.line(px(10), ey(0), px(10), ey(Y_YARD), CLIMB, 5)
    sv.line(px(24), ey(Y_YARD), px(24), ey(Y_GAL), CLIMB, 5)
    sv.line(px(WEIGHT_Z[0]), ey(Y_YARD), px(WEIGHT_Z[0]), ey(7.5), CLIMB, 4)
    sv.line(px(76), ey(Y_F), px(76), ey(Y_COURT), CLIMB, 5)
    for my in MALLET_Y:
        erect(75.1, 76, my - .3, my, RIDE, "#ffffff", .8)
    sv.line(px(90), ey(Y_COURT), px(90), ey(CORNICE), CLIMB, 6)
    erect(89.4, 90, CORNICE, Y_BELL, GREY)                   # the cornice: no way over on the dial's side
    erect(90, 93.5, Y_BAL, Y_BELL, CLIMB, "none", 1, .9)     # the east face above the balcony, facing us
    sv.line(px(90), ey(Y_BAL), px(94), ey(Y_BAL), REST, 4)
    for (pxx, pyy, w, nm) in PERCH:
        if pxx >= 0:
            sv.line(px(89.1), ey(pyy), px(90), ey(pyy), REST, 4)
    sv.line(px(100), ey(Y_BELL), px(112), ey(Y_EXIT), DOWN, 5)
    zc, yc = ROPE_PIVOT
    sv.rect(px(44), ey(Y_F), 8 * S, (Y_F + 2) * S, PIT, "none", 1, .9)
    sv.rect(px(37), ey(Y_F), 1.6 * S, (Y_F + 2) * S, PIT, "none", 1, .9)
    erect(PIER_Z[0], PIER_Z[1], -2, 9.0, STONE, CLIMB, 2.5)
    for t in (-40, 0, 40):
        a = math.radians(t)
        sv.line(px(zc), ey(yc), px(zc + ROPE_LEN * math.sin(a)), ey(yc - ROPE_LEN * math.cos(a)), RIDE, 2, "" if t == -40 else "4 3")
    sv.circle(px(zc), ey(yc), 4, RIDE)
    sv.text(px(48), ey(1.0), "낙사", 12, "#6b7389", 400, "middle")
    sv.poly([(px(z), ey(y)) for (x, y, z) in PATH], "none", "#ffffff", 1.8, 1, "6 4", close=False, extra='marker-end="url(#tip)"')
    for z, y, s, col, anc in [(9.2, 1.4, "3.5 m", CLIMB, "end"), (20.2, 5.0, "3.5 m", CLIMB, "end"),
                              (21.2, 9.8, "추가 +4.5 m", RIDE, "end"), (25.0, 9.6, "−7 m", INK, "start"),
                              (38.2, 9.9, "ㄱ자", CLIMB, "middle"), (48, 12.6, "8 m 그네", RIDE, "middle"),
                              (66, 11.6, "↑3.2 ↓3.2", RIDE, "middle"), (72.6, 6.4, "빗살", WARN, "start"),
                              (77, 9.0, "2.6 · 2.5 · 2.5 · 2.4 m", CLIMB, "start"), (87.6, 20.5, "쉼터 셋", CLIMB, "end"),
                              (92.6, 30.3, "모서리 돌아 3.4 m", CLIMB, "start"), (107, 26.8, "−6 m", DOWN, "start")]:
        sv.text(px(z), ey(y), s, 12, col, 700, anc)
    for n, z, y in [(1, 10, 6.2), (2, 22.4, 18.8), (3, 31.5, 14.6), (4, 38.2, 12.8), (5, 48, 15.2), (6, 66, 14.2),
                    (7, 76, 17.6), (8, 90, 30.6), (9, 110.5, 29.2)]:
        sv.badge(px(z), ey(y), n)
    for z in range(0, 121, 10):
        sv.line(px(z), ey(-2.6), px(z), ey(-2.6) + 5, SUB, 1)
        sv.text(px(z), ey(-2.6) + 18, str(z), 11, SUB, 400, "middle")
    sv.text(px(60), ey(-2.6) + 34, "입구에서 북쪽으로 (m)", 11, SUB, 400, "middle")

    # ---- the rhythm strip and the legend
    y0 = EY + 88
    sv.text(24, y0, "리듬", 13, INK, 700)
    chips = [("달리기", REST), ("① 배우는 벽 3.5", CLIMB), ("쉬기", REST), ("② 추에 올라 +8.5", RIDE), ("쉬기", REST),
             ("③ 낙하 벽 −7", DOWN), ("쉬기", REST), ("④ ㄱ자 기둥", CLIMB), ("쉬기", REST), ("⑤ 종 줄 그네", RIDE),
             ("쉬기", REST), ("⑥ 원통 반원 ↑↓", RIDE), ("쉬기", REST), ("⑦ 망치 벽 10", CLIMB), ("쉬기", REST),
             ("⑧ 대시계판 13", CLIMB), ("쉬기", REST), ("⑨ 구리 지붕 −6", DOWN)]
    x = 70.0
    for i, (s, col) in enumerate(chips):
        w = max(46, 16 + 11.4 * len(s.replace(" ", "")) + 4 * s.count(" "))
        if x + w > 1226:
            x = 70.0
            y0 += 28
        sv.rect(x, y0 - 15, w, 22, col, "none", 1, 1, 'rx="4"')
        sv.text(x + w / 2, y0, s, 12, BG, 700, "middle")
        if i < len(chips) - 1:
            sv.text(x + w + 6.5, y0, "›", 13, SUB, 700, "middle")
        x += w + 13
    y1 = y0 + 40
    x = 24.0
    for col, s in [(REST, "초록 - 발 디디는 곳(스테미나 회복)"), (CLIMB, "주황 - 타는 벽(스테미나 소모)"),
                   (RIDE, "보라 - 매달려 실려 가는 장치(멈춰 있으면 소모 0)"), (DOWN, "파랑 - 내려가는 곳"),
                   (WARN, "살구 - 원통의 고정 빗살(닿으면 떨어짐)")]:
        sv.rect(x, y1 - 11, 14, 14, col)
        sv.text(x + 20, y1, s, 12, SUB)
        x += 34 + 10.4 * len(s)
    sv.text(24, y1 + 28, "  ".join(f"{'①②③④⑤⑥⑦⑧⑨'[i]} {n}" for i, n in enumerate(NAMES))
            + "   ·   깃발 = 체크포인트 10개   ·   회색 선 = 못 타는 격자 · 난간 · 처마", 12, SUB)
    sv.text(24, y1 + 50, f"경로 약 {path_length():.0f} m · 출구 높이 +22 m · 낙사는 ④ 기둥 밑, ⑤ 종 줄, ⑥ 원통 세 곳(모두 체크포인트 바로 뒤). "
            "나머지는 아래 마루로 떨어져 다시 오른다", 12, SUB)
    sv.save(f"{RN}_RoyalClockTower.svg")


# ---------------------------------------------------------------- 2. axonometric view from the south-east
def iso():
    SC = 7.2
    c30, s30 = math.cos(math.radians(30)), math.sin(math.radians(30))

    def pr(x, y, z):
        return ((x + z) * c30 * SC + 250, ((x - z) * s30 - y) * SC + 905)

    boxes = []

    def shade(col, k):
        col = col.lstrip("#")
        return "#%02x%02x%02x" % tuple(int(int(col[i:i + 2], 16) * k) for i in (0, 2, 4))

    def box(x0, x1, y0, y1, z0, z1, top=STONE, south=None, east=None, op=1.0, stroke=EDGE):
        boxes.append(dict(x0=x0, x1=x1, y0=y0, y1=y1, z0=z0, z1=z1, top=top, south=south or shade(top, .72),
                          east=east or shade(top, .55), op=op, stroke=stroke))

    def special(x0, x1, y0, y1, z0, z1, fn):
        boxes.append(dict(x0=x0, x1=x1, y0=y0, y1=y1, z0=z0, z1=z1, fn=fn))

    B, G = -2.0, "#3d4a5e"
    sv = Svg(1260, 1010, f"{RN} 왕실 시계탑 - 남동쪽 위에서 본 입체 그림")
    box(-22, 22, B - .4, B, 0, 120, top=PIT)
    box(-12, 12, B, 0, 0, 10, top=REST, south=G, east=G)
    box(-22, 22, B, Y_YARD, 10, 24, top=REST, south=G, east=G)
    box(-12, 12, 0, Y_YARD, 9.94, 10, top=CLIMB, south=CLIMB, east=CLIMB)
    box(-22, 2, B, Y_GAL, 24, 34, top=REST, south=G, east=G)
    for a, b in [(-22, -17), (-10.5, -7.5), (-1, 2)]:
        box(a, b, Y_YARD, Y_GAL, 23.94, 24, top=CLIMB, south=CLIMB, east=CLIMB)
    box(2, 2.06, Y_F, Y_GAL, 24, 34, top=DOWN, south=DOWN, east=DOWN)
    box(2.06, 2.86, 8.2, 8.5, 29, 34, top=REST)
    for i, wx in enumerate(WEIGHT_X):
        top = 7.5 if i % 2 == 0 else Y_GAL
        box(wx - 1.5, wx + 1.5, top - 4.0, top, WEIGHT_Z[0], WEIGHT_Z[1], top=RIDE, south=shade(RIDE, .8), east=shade(RIDE, .62), stroke="#ffffff")
        if top > 7.6:
            box(wx - 1.5, wx + 1.5, Y_YARD, top - 4.0, WEIGHT_Z[0], WEIGHT_Z[1], top=GREY, south=shade(GREY, .62), east=shade(GREY, .5))
    box(2, 22, B, Y_F, 24, 37, top=REST, south=G, east=G)
    for (a, b) in PIERS:
        box(a, b, B, 9.0, PIER_Z[0], PIER_Z[1], top=REST, south=CLIMB, east=shade(CLIMB, .8))
    box(ISLAND[0], ISLAND[1], B, Y_F, 39.4, ISLAND[3], top=REST, south=G, east=G)
    for a, b in [(ISLAND[0], PIERS[0][0]), (PIERS[0][1], PIERS[1][0]), (PIERS[1][1], ISLAND[1])]:
        box(a, b, B, Y_F, ISLAND[2], 39.4, top=REST, south=G, east=G)
    box(2, 22, B, Y_F, 52, 58, top=REST, south=G, east=G)
    box(10, 22, B, Y_F, 58, 76, top=REST, south=G, east=G)
    box(7.05, 10, Y_F - .4, Y_F, 64.2, 66.6, top=REST)
    box(-22, -6, B, Y_F, 58, 76, top=REST, south=G, east=G)
    box(-6, -3.05, Y_F - .4, Y_F, 64.8, 67.2, top=REST)
    x1, z1 = barrel_pt(COMB1[0], 5.75)
    box(x1 - .5, x1 + .5, COMB1[1], COMB1[2], z1 - .6, z1 + .6, top=WARN)
    x2, z2 = barrel_pt(COMB2[0], 5.75)
    box(x2 - .6, x2 + .6, COMB2[1], COMB2[2], z2 - .5, z2 + .5, top=WARN)
    box(-22, 22, B, Y_COURT, 76, 90, top=REST, south=G, east=G)
    box(-22, -6, Y_F, Y_COURT, 75.94, 76, top=CLIMB, south=CLIMB, east=CLIMB)
    box(LEDGE_X[0], LEDGE_X[1], Y_DRUM, Y_COURT, 75.94, 76, top=CLIMB, south=CLIMB, east=CLIMB)
    box(LEDGE_X[0], LEDGE_X[1], Y_DRUM - .3, Y_DRUM, 75.0, 75.94, top=REST)
    for mx in MALLET_X:
        for my in MALLET_Y:
            box(mx - .9, mx + .9, my - .3, my, 75.04, 75.94, top=RIDE, stroke="#ffffff")
    box(-8, 8, Y_COURT, Y_BELL, 90, 100, top=REST, south=G, east=G)
    box(-8, 8, Y_COURT, CORNICE, 89.94, 90, top=CLIMB, south="#b9791c", east=CLIMB)
    box(-8.4, 8.4, CORNICE, Y_BELL, 89.5, 90, top=GREY, south=shade(GREY, .7), east=shade(GREY, .55))
    box(8, 8.06, Y_BAL, Y_BELL, 90, 93.5, top=CLIMB, south=CLIMB, east=CLIMB)
    box(8.06, 12, Y_BAL - .35, Y_BAL, 90, 94, top=REST)
    box(-12, -8, Y_BAL - .35, Y_BAL, 90, 94, top=REST)
    for (pxx, pyy, w, nm) in PERCH:
        box(pxx - w / 2, pxx + w / 2, pyy - .3, pyy, 88.9, 89.8, top=REST)
    box(-8, 8, B, Y_EXIT, 112, 120, top=REST, south=G, east=G)

    def draw_barrel():
        cx, cz, r = BARREL
        a, b = r * c30 * math.sqrt(2) * SC, r * s30 * math.sqrt(2) * SC
        tx, ty = pr(cx, Y_DRUM, cz)
        bx, by = pr(cx, Y_F, cz)
        gx, gy = pr(cx, B, cz)
        sv.add(f'<path d="M{gx - a:.1f},{gy:.1f} A{a:.1f},{b:.1f} 0 0 0 {gx + a:.1f},{gy:.1f} L{bx + a:.1f},{by:.1f} '
               f'A{a:.1f},{b:.1f} 0 0 1 {bx - a:.1f},{by:.1f} Z" fill="{shade(RIDE, .38)}" stroke="{EDGE}"/>')
        sv.add(f'<path d="M{bx - a:.1f},{by:.1f} A{a:.1f},{b:.1f} 0 0 0 {bx + a:.1f},{by:.1f} L{tx + a:.1f},{ty:.1f} '
               f'A{a:.1f},{b:.1f} 0 0 1 {tx - a:.1f},{ty:.1f} Z" fill="{shade(RIDE, .72)}" stroke="#ffffff" stroke-width=".8"/>')
        for k in range(1, 6):
            qx, qy = pr(cx, Y_F + k * 0.85, cz)
            sv.add(f'<path d="M{qx - a:.1f},{qy:.1f} A{a:.1f},{b:.1f} 0 0 0 {qx + a:.1f},{qy:.1f}" fill="none" '
                   f'stroke="{shade(RIDE, .5)}" stroke-width="1" stroke-dasharray="2 5"/>')
        sv.add(f'<ellipse cx="{tx:.1f}" cy="{ty:.1f}" rx="{a:.1f}" ry="{b:.1f}" fill="{RIDE}" stroke="#ffffff" stroke-width=".8"/>')
        sv.add(f'<ellipse cx="{tx:.1f}" cy="{ty:.1f}" rx="{a * .12:.1f}" ry="{b * .12:.1f}" fill="{BG}"/>')
        # the plank to the high ledge
        w = 0.8
        d = (CAT_B[0] - CAT_A[0], CAT_B[1] - CAT_A[1])
        n = math.hypot(*d)
        nx, nz = -d[1] / n * w, d[0] / n * w
        q = [pr(CAT_A[0] + nx, Y_DRUM, CAT_A[1] + nz), pr(CAT_B[0] + nx, Y_DRUM, CAT_B[1] + nz),
             pr(CAT_B[0] - nx, Y_DRUM, CAT_B[1] - nz), pr(CAT_A[0] - nx, Y_DRUM, CAT_A[1] - nz)]
        sv.poly(q, REST, EDGE, .7)

    special(BARREL[0] - 5, BARREL[0] + 5, B, Y_DRUM, BARREL[1] - 5, BARREL[1] + 5, draw_barrel)

    def draw_roof():
        e = [pr(8, Y_BELL, 100), pr(8, Y_EXIT, 112), pr(8, B, 112), pr(8, B, 100)]
        sv.poly(e, shade(DOWN, .5), EDGE)
        sv.poly([pr(-8, Y_BELL, 100), pr(8, Y_BELL, 100), pr(8, Y_EXIT, 112), pr(-8, Y_EXIT, 112)], DOWN, EDGE)
        for k in range(1, 6):
            zz = 100 + k * 2
            yy = Y_BELL - (zz - 100) * 0.5
            sv.poly([pr(-5, yy, zz - .8), pr(0, yy - .5, zz + .2), pr(5, yy, zz - .8)], "none", "#ffffff", 1.1, .6, close=False)

    special(-8, 8, B, Y_BELL, 100, 112, draw_roof)

    def draw_grille():
        sv.poly([pr(2, Y_YARD, 24), pr(22, Y_YARD, 24), pr(22, 16, 24), pr(2, 16, 24)], GREY, GREY, 1, .16)
        for k in range(0, 11):
            a, b = pr(2 + k * 2, Y_YARD, 24), pr(2 + k * 2, 16, 24)
            sv.line(a[0], a[1], b[0], b[1], GREY, .8)
        for yy in (7.5, 11.5, 16):
            a, b = pr(2, yy, 24), pr(22, yy, 24)
            sv.line(a[0], a[1], b[0], b[1], GREY, .8)

    special(2, 22, Y_F + .01, 16, 23.9, 23.99, draw_grille)

    def draw_ropes():
        zc, yc = ROPE_PIVOT
        for i, rx in enumerate(ROPE_X):
            a = math.radians([-40, 25, -10, 40, -30][i])
            p0 = pr(rx, yc, zc)
            p1 = pr(rx, yc - ROPE_LEN * math.cos(a), zc + ROPE_LEN * math.sin(a))
            pt = pr(rx, yc + 7, zc)
            sv.line(pt[0], pt[1], p0[0], p0[1], shade(RIDE, .55), 1)
            sv.line(p0[0], p0[1], p1[0], p1[1], RIDE, 2.4)
            sv.circle(p1[0], p1[1], 3, RIDE)
            sv.add(f'<path d="M{p0[0] - 7:.1f},{p0[1] - 2:.1f} q7,-22 14,0 z" fill="{CLIMB}" opacity=".9"/>')

    special(5.9, 18.1, Y_F + .01, 19, 44.1, 51.9, draw_ropes)

    def draw_dial():
        cx, cy, r = DIAL
        pts = [pr(cx + r * math.cos(math.radians(t)), cy + r * math.sin(math.radians(t)), 89.9) for t in range(0, 360, 6)]
        sv.poly(pts, "#fff3d6", "#7a5310", 2, .93)
        for h in range(12):
            t = math.radians(90 - h * 30)
            a = pr(cx + (r - .9) * math.cos(t), cy + (r - .9) * math.sin(t), 89.9)
            b = pr(cx + (r - .2) * math.cos(t), cy + (r - .2) * math.sin(t), 89.9)
            sv.line(a[0], a[1], b[0], b[1], "#7a5310", 2.4)
        o = pr(cx, cy, 89.9)
        for t, ln, w in [(60, 4.9, 2.6), (160, 3.3, 3.6)]:        # the hands are a picture behind the glass
            e = pr(cx + ln * math.cos(math.radians(t)), cy + ln * math.sin(math.radians(t)), 89.9)
            sv.line(o[0], o[1], e[0], e[1], "#7a5310", w, "", 'opacity=".4"')

    special(-6.1, 6.1, 15.4, 27.6, 89.8, 89.93, draw_dial)

    def draw_bell():
        for xx in (-5, 5):
            a, b = pr(xx, Y_BELL, 96), pr(xx, Y_BELL + 5.4, 96)
            sv.line(a[0], a[1], b[0], b[1], "#7a5310", 3.2)
        a, b = pr(-5.6, Y_BELL + 5.4, 96), pr(5.6, Y_BELL + 5.4, 96)
        sv.line(a[0], a[1], b[0], b[1], "#7a5310", 4)
        p = pr(0, Y_BELL + 5.2, 96)
        w = 20.0
        sv.add(f'<path d="M{p[0] - w * .28:.1f},{p[1]:.1f} c{-w * .1:.1f},{w * .9:.1f} {-w * .5:.1f},{w * 1.25:.1f} {-w * .82:.1f},{w * 1.7:.1f} '
               f'l{w * 2.2:.1f},0 c{-w * .32:.1f},{-w * .45:.1f} {-w * .72:.1f},{-w * .8:.1f} {-w * .82:.1f},{-w * 1.7:.1f} z" '
               f'fill="{CLIMB}" stroke="#7a5310" stroke-width="1.4"/>')
        sv.circle(p[0], p[1] + w * 1.82, 3.4, "#7a5310")

    special(-5.6, 5.6, Y_BELL + .01, Y_BELL + 6, 95.5, 96.5, draw_bell)

    # painter's order: a is behind b when it is further west, further north, or lower
    n, EPS = len(boxes), 1e-6

    def span(b):
        pts = [pr(x, y, z) for x in (b["x0"], b["x1"]) for y in (b["y0"], b["y1"]) for z in (b["z0"], b["z1"])]
        return min(p[0] for p in pts), max(p[0] for p in pts), min(p[1] for p in pts), max(p[1] for p in pts)

    sp = [span(b) for b in boxes]

    def behind(a, b):
        return a["x1"] <= b["x0"] + EPS or a["z0"] >= b["z1"] - EPS or a["y1"] <= b["y0"] + EPS

    before = [set() for _ in range(n)]
    for i in range(n):
        for j in range(n):
            if i == j:
                continue
            a, b = sp[i], sp[j]
            if not (a[0] < b[1] - EPS and b[0] < a[1] - EPS and a[2] < b[3] - EPS and b[2] < a[3] - EPS):
                continue
            if behind(boxes[j], boxes[i]) and not behind(boxes[i], boxes[j]):
                before[i].add(j)
    key = lambda k: (boxes[k]["x0"] + boxes[k]["x1"]) / 2 - (boxes[k]["z0"] + boxes[k]["z1"]) / 2 + (boxes[k]["y0"] + boxes[k]["y1"]) / 2
    order, done = [], set()
    while len(order) < n:
        ready = [k for k in range(n) if k not in done and before[k] <= done] or [min((k for k in range(n) if k not in done), key=key)]
        k = min(ready, key=key)
        order.append(k)
        done.add(k)
    for k in order:
        b = boxes[k]
        if "fn" in b:
            b["fn"]()
            continue
        x0, x1, y0, y1, z0, z1 = b["x0"], b["x1"], b["y0"], b["y1"], b["z0"], b["z1"]
        sv.poly([pr(x1, y0, z0), pr(x1, y0, z1), pr(x1, y1, z1), pr(x1, y1, z0)], b["east"], b["stroke"], .7, b["op"])
        sv.poly([pr(x0, y0, z0), pr(x1, y0, z0), pr(x1, y1, z0), pr(x0, y1, z0)], b["south"], b["stroke"], .7, b["op"])
        sv.poly([pr(x0, y1, z0), pr(x1, y1, z0), pr(x1, y1, z1), pr(x0, y1, z1)], b["top"], b["stroke"], .7, b["op"])

    pts = [pr(x, y + .15, z) for (x, y, z) in PATH]
    sv.poly(pts, "none", BG, 4.2, .55, close=False)
    sv.poly(pts, "none", "#ffffff", 2, 1, "7 4", close=False, extra='marker-end="url(#tip)"')
    for name, cx, cy, cz in CP:
        a, b = pr(cx, cy, cz), pr(cx, cy + 1.9, cz)
        sv.line(a[0], a[1], b[0], b[1], "#ffffff", 1.6)
        sv.poly([b, (b[0] + 11, b[1] + 4), (b[0], b[1] + 8)], "#ffffff")

    def label(n, x, y, z, s, dx=16, dy=5, anchor="start"):
        p = pr(x, y, z)
        sv.badge(p[0], p[1], n, 12)
        w = 13.2 * len(s) * .78 + 14
        rx = p[0] + dx - 6 if anchor == "start" else p[0] - dx - w + 6
        sv.rect(rx, p[1] + dy - 15, w, 21, BG, "none", 1, .82, 'rx="4"')
        sv.text(p[0] + (dx if anchor == "start" else -dx), p[1] + dy, s, 13.5, INK, 700, anchor)

    label(1, 12, Y_YARD, 10, "배우는 벽 3.5 m")
    label(2, -22, 13, 24, "짝 추 - 머리를 잡고 올라탄다", anchor="end")
    label(3, 2, 15, 34, "낙하 벽 −7 m", dy=-4)
    label(4, 22, 8, 38, "모서리 기둥 - ㄱ자로 돌아 건넌다")
    label(5, 22, 12.4, 48, "종 줄 다섯 - 그네")
    label(6, 13, Y_F + 1.2, 61, "편종 원통 - 반원을 실려 가며 빗살 피하기")
    label(7, -22, 11, 76, "망치 벽 10 m - 들락날락 쉼터", anchor="end")
    label(8, -8, 24, 90, "대시계판 - 쉼터 셋, 처마 밑 모서리", anchor="end")
    label(9, 8, 24.4, 107, "구리 지붕 - 배로 미끄러지기")
    e = pr(0, 0, 0)
    sv.text(e[0] + 4, e[1] + 30, "입구", 15, INK, 700, "middle")
    e = pr(0, Y_EXIT, 120)
    sv.text(e[0] + 16, e[1] - 6, "출구 (+22 m)", 15, INK, 700)
    sv.text(24, 36, f"왕실 시계탑 - 남동쪽 위에서 본 입체 그림 ({RN})", 21, INK, 700)
    sv.text(24, 60, "주황 = 타는 벽 · 초록 = 쉬는 곳 · 보라 = 매달려 실려 가는 장치(추 · 종 줄 · 원통 · 망치) · "
            "파랑 = 내려가는 곳 · 살구 = 고정 빗살 · 회색 = 못 타는 면 · 흰 점선 = 진행 경로", 13, SUB)
    sv.text(24, 80, "회색 상자를 짓기 전의 구조 그림이다. 홀의 벽 · 천장 · 난간은 그리지 않았다.", 12, SUB)
    sv.save(f"{RN}_RoyalClockTower_Iso.svg")


# ---------------------------------------------------------------- 3. the devices and the two shapes the user asked for
def devices():
    sv = Svg(1250, 1338, f"{RN} 왕실 시계탑 - 장치와 ㄱ자 · 반원 구간")
    sv.text(24, 36, f"왕실 시계탑 - 새 장치 넷과 ㄱ자 · 반원 구간 ({RN})", 21, INK, 700)
    sv.text(24, 60, "움직이는 것은 모두 공유 시계의 시간 함수라 모든 PC에서 같은 자리에 있다. 숫자는 초안이고 회색 상자에서 달려 본 뒤 고친다.", 13, SUB)

    def panel(x, y, w, h, title, sub):
        sv.rect(x, y, w, h, "#1d212c", EDGE, 1, 1, 'rx="6"')
        sv.text(x + 14, y + 25, title, 15, INK, 700)
        sv.text(x + 14, y + 44, sub, 12, SUB)

    # ---- A. the paired weights
    panel(24, 78, 590, 388, "② 짝 추 - 머리를 잡고 올라타는 추", "기둥 넷이 3초씩 어긋나 오르내린다. 금빛 머리(위 4 m)만 잡힌다.")
    k, yb = 12.0, 318.0

    def wy(y): return yb - (y - Y_YARD) * k
    sv.rect(152, wy(Y_GAL), 22, (Y_GAL - Y_YARD) * k, STONE, EDGE)
    sv.line(40, yb, 152, yb, REST, 4)
    sv.line(150, wy(Y_GAL), 182, wy(Y_GAL), REST, 4)
    sv.rect(58, wy(7.5), 36, 4 * k, RIDE, "#ffffff", 1)
    sv.rect(58, yb, 36, 22, GREY, "none", 1, .4)
    sv.rect(104, wy(Y_GAL), 36, 4 * k, RIDE, "#ffffff", 1, .55)
    sv.rect(104, wy(8.0), 36, 4.5 * k, GREY, "none", 1, .5)
    sv.line(76, wy(7.5) - 6, 76, wy(7.5) - 38, "#ffffff", 1.5, extra='marker-end="url(#tip)"')
    sv.line(122, wy(Y_GAL) + 8, 122, wy(Y_GAL) + 40, "#ffffff", 1.5, extra='marker-end="url(#tip)"')
    sv.text(76, yb + 38, "아래", 11.5, SUB, 400, "middle")
    sv.text(122, yb + 38, "위", 11.5, SUB, 400, "middle")
    sv.text(36, yb - 5, "추 마당 3.5", 11, REST, 700)
    sv.text(186, wy(Y_GAL) + 4, "회랑 12", 11, REST, 700)
    sv.text(54, wy(7.5) + 4, "7.5", 11, SUB, 400, "end")
    tx0, tw = 300.0, 286.0 / 12.0
    states = [(0, 3, "탄다", REST, BG), (3, 6, "↑", RIDE, BG), (6, 9, "내린다", "#fff3d6", BG), (9, 12, "↓", "#475069", INK)]
    for r, (name, d) in enumerate([("추 1", 0), ("추 2 (1의 짝)", 6), ("추 3", 3), ("추 4 (3의 짝)", 9)]):
        yy = 150 + r * 40
        sv.text(tx0 - 10, yy + 16, name, 12, INK, 700, "end")
        for (a, b, s, col, tc) in states:
            a2, b2 = (a + d) % 12, (b + d) % 12
            for (p0, p1) in ([(a2, b2)] if b2 > a2 else [(a2, 12), (0, b2)]):
                if p1 - p0 < 1e-6:
                    continue
                sv.rect(tx0 + p0 * tw, yy, (p1 - p0) * tw - 1, 24, col)
                if p1 - p0 >= 1.9:
                    sv.text(tx0 + (p0 + p1) / 2 * tw, yy + 16, s, 11.5, tc, 700, "middle")
    for t in (0, 3, 6, 9, 12):
        sv.line(tx0 + t * tw, 312, tx0 + t * tw, 318, SUB, 1)
        sv.text(tx0 + t * tw, 332, f"{t}초", 11, SUB, 400, "middle")
    sv.notes(40, 376, ["· 기둥은 가로 3 × 세로 3 m. 마루를 뚫고 오르내려 밑에 깔릴 틈이 없다. 윗면 7.5 ↔ 12 m.",
                       "· 내려와 있는 3초 동안 머리 옆면 3.5 m를 올라 윗면에 선다. 서서 실려 가는 동안 스테미나가 찬다.",
                       "· 아랫부분(회색)은 매끈해서 못 잡는다 - 올라가 있는 추를 잡고 9 m를 오르는 실수가 없다.",
                       "· 늦게 잡으면 매달린 채 실려 간다(소모 0). 기다리기 싫은 폰은 옆 절벽 8.5 m를 한 번에(89%)."])

    # ---- B. the corner pier
    panel(636, 78, 590, 388, "④ 모서리 기둥 - ㄱ자로 돌아 건넌다", "마루가 끊긴 곳을 기둥 면으로 돌아 건넌다. 모서리에서 카메라가 같이 돈다(이 그림은 북쪽이 위).")
    sc, ox, oz = 46.0, 686.0, 442.0                  # screen origin for x = 6, z = 35

    def qx(x): return ox + (x - 6.0) * sc
    def qz(z): return oz - (z - 35.0) * sc
    sv.rect(qx(5.4), qz(40.6), 6.6 * sc, 5.6 * sc, PIT, EDGE)
    sv.rect(qx(5.4), qz(37.0), 6.6 * sc, 2.0 * sc, REST, "none", 1, .7)             # the floor before
    sv.rect(qx(5.4), qz(40.6), 6.6 * sc, 2.0 * sc, REST, "none", 1, .7)             # the island
    sv.rect(qx(8.0), qz(39.4), 2.4 * sc, 2.4 * sc, STONE, CLIMB, 4)                # the pier
    sv.text(qx(9.2), qz(38.2) + 2, "기둥", 12.5, INK, 700, "middle")
    sv.text(qx(9.2), qz(38.2) + 19, "2.4 × 2.4 m", 11, SUB, 400, "middle")
    sv.text(qx(5.6), qz(35.25), "종지기 마루 (높이 5)", 11.5, BG, 700)
    sv.text(qx(5.6), qz(40.25), "섬 - 종 줄 타는 곳 (높이 5)", 11.5, BG, 700)
    sv.text(qx(11.35), qz(37.75), "낙사", 11.5, "#6b7389", 400, "middle")
    sv.text(qx(6.7), qz(37.75), "낙사", 11.5, "#6b7389", 400, "middle")
    east = [(9.6, 36.55), (9.6, 36.85), (10.55, 36.85), (10.65, 37.0), (10.65, 39.2), (10.9, 39.95)]
    west = [(8.8, 36.55), (8.8, 36.85), (7.85, 36.85), (7.75, 37.0), (7.75, 39.2), (7.5, 39.95)]
    sv.poly([(qx(x), qz(z)) for x, z in west], "none", "#ffffff", 1.6, .55, "5 4", close=False)
    sv.poly([(qx(x), qz(z)) for x, z in east], "none", "#ffffff", 2.4, 1, close=False, extra='marker-end="url(#tip)"')
    sv.circle(qx(10.62), qz(36.88), 5.5, CLIMB, "#ffffff", 1.2)
    sv.text(qx(10.85), qz(36.52), "모서리 0.4초", 11.5, CLIMB, 700)
    sv.text(qx(10.85), qz(38.3), "옆으로 2.1 m", 11.5, CLIMB, 700)
    for (cx_, cz_, ang, lab, lx_, lz_, anc) in [(9.6, 35.75, -90, "카메라 ① 북쪽을 본다", 9.85, 35.68, "start"),
                                                 (11.6, 38.75, 180, "카메라 ②", 11.55, 39.12, "middle")]:
        a_ = math.radians(ang)
        sv.circle(qx(cx_), qz(cz_), 7, "none", YEL, 1.6)
        sv.line(qx(cx_), qz(cz_), qx(cx_) + 24 * math.cos(a_), qz(cz_) + 24 * math.sin(a_), YEL, 1.6, extra='marker-end="url(#tipy)"')
        sv.text(qx(lx_), qz(lz_), lab, 11, YEL, 700, anc)
    sv.notes(1016, 152, ["스테미나(가득 = 100%)", "폰 약 48% · 룩 약 69%", "", "0.3 m 올라 발을 떼고", "옆으로 1.2 m까지 →", "모서리(0.4초) →",
                         "옆으로 2.1 m →", "섬 위에서 손을 놓는다", "", "두 기둥 × 양쪽 = 네 줄", "", "떨어지면 낙사,", "CP3이 바로 앞",
                         "", "다른 길: 기둥 꼭대기", "(4 m)로 올라 뛰어내림"], 12, 17)

    # ---- C. the carillon drum, unrolled
    panel(24, 484, 590, 404, "⑥ 편종 원통 - 반원을 실려 가며 빗살 피하기",
          "원통 겉을 펼친 그림. 초당 15°로 돌며 매달린 폰을 동 → 북 → 서로 싣는다. 빗살은 서 있다.")

    def bxu(u): return 58.0 + u * 2.2
    def bx(az): return bxu((az - 330) % 360)
    def by(h): return 770.0 - (h - Y_F) * 34.0
    sv.rect(bxu(0), by(Y_DRUM), 235 * 2.2, (Y_DRUM - Y_F) * 34.0, RIDE, RIDE, 1, .2)
    sv.line(bxu(0), by(Y_DRUM), bxu(235), by(Y_DRUM), REST, 4)
    sv.text(bxu(235), by(Y_DRUM) - 8, "원통 윗면 10.1 m - 서서 쉰다 → 널다리", 11.5, REST, 700, "end")
    sv.rect(bxu(5), by(Y_F), 30 * 2.2, 9, REST)
    sv.rect(bxu(35), by(Y_F), 170 * 2.2, 9, PIT, EDGE)
    sv.rect(bxu(205), by(Y_F), 30 * 2.2, 9, REST)
    sv.text(bxu(20), by(Y_F) + 24, "타는 곳", 11.5, REST, 700, "middle")
    sv.text(bxu(120), by(Y_F) + 24, "낙사 구덩이", 11.5, "#6b7389", 400, "middle")
    sv.text(bxu(220), by(Y_F) + 24, "내리는 곳", 11.5, REST, 700, "middle")
    c1, c2 = (COMB1[0] - 330) % 360, (COMB2[0] - 330) % 360
    sv.rect(bxu(c1 - 5), by(COMB1[2]), 10 * 2.2, (COMB1[2] - COMB1[1]) * 34.0, WARN)
    sv.rect(bxu(c2 - 5), by(COMB2[2]), 10 * 2.2, (COMB2[2] - COMB2[1]) * 34.0, WARN)
    sv.text(bxu(c1), by(COMB1[2]) - 6, "빗살 1", 11.5, WARN, 700, "middle")
    sv.text(bxu(c2) + 16, by(8.8), "빗살 2", 11.5, WARN, 700)
    for kk in range(0, 8):
        u = 20 + 30 * kk
        sv.line(bxu(u), by(Y_F) + 30, bxu(u), by(Y_F) + 35, SUB, 1)
        sv.text(bxu(u), by(Y_F) + 48, f"{kk * 2}초", 11, SUB, 400, "middle")
    for az, s in [(0, "동"), (90, "북"), (180, "서")]:
        sv.text(bx(az), by(Y_DRUM) + 14, s, 11, GREY, 400, "middle")
    low = [(20, 5.5), (c1 - 9, 8.9), (c1 + 9, 8.9), (c2 - 9, 6.7), (c2 + 9, 6.7), (212, 5.5)]
    high = [(20, 5.5), (c1 - 9, 8.9), (c1 + 14, 9.6), (c2 - 22, Y_DRUM), (c2 + 30, Y_DRUM)]
    sv.poly([(bxu(u), by(h)) for u, h in high], "none", YEL, 2, 1, "6 4", close=False, extra='marker-end="url(#tipy)"')
    sv.poly([(bxu(u), by(h)) for u, h in low], "none", "#ffffff", 2.2, 1, close=False, extra='marker-end="url(#tip)"')
    for h in (5, 7, 9):
        sv.text(bxu(0) - 6, by(h) + 4, str(h), 11, SUB, 400, "end")
    sv.notes(40, 838, ["· 흰 선(낮은 길): 빗살 1 위로 3.2 m, 빗살 2 밑으로 1.8 m. 폰 47% · 룩 68%.",
                       "· 노란 선(높은 길): 꼭대기까지 4.6 m → 널다리 → 망치 벽 둘째 줄. 6 ~ 8초 빠름.",
                       "· 빗살에 닿으면 손이 떨어진다(넘어짐 없음) → 구덩이 → CP5."])

    # ---- D. why the half-circle turns: the comparison from the flow model
    panel(636, 484, 590, 404, "⑥ 반원을 어떻게 탈까 - 흐름 모형으로 견준 것", "같은 반원(반지름 5 m, 190° = 17.3 m)을 세 가지로. 6 대 6 · 300판의 중간값.")
    cxp, czp, rp = 760.0, 702.0, 80.0
    sv.circle(cxp, czp, rp, RIDE, "#ffffff", 1.2, .9)
    sv.circle(cxp, czp, 8, BG)
    arc = [(cxp + (rp + 14) * math.cos(math.radians(a)), czp - (rp + 14) * math.sin(math.radians(a))) for a in range(-10, 181, 5)]
    sv.poly(arc, "none", "#ffffff", 2.2, 1, "6 4", close=False, extra='marker-end="url(#tip)"')
    for a in (COMB1[0], COMB2[0]):
        r0, r1 = rp + 3, rp + 30
        sv.line(cxp + r0 * math.cos(math.radians(a)), czp - r0 * math.sin(math.radians(a)),
                cxp + r1 * math.cos(math.radians(a)), czp - r1 * math.sin(math.radians(a)), WARN, 5)
    for a, lab in [(-10, "카메라"), (85, "카메라"), (180, "카메라")]:
        px_, pz_ = cxp + (rp + 52) * math.cos(math.radians(a)), czp - (rp + 52) * math.sin(math.radians(a))
        tx_, tz_ = cxp + (rp + 26) * math.cos(math.radians(a)), czp - (rp + 26) * math.sin(math.radians(a))
        sv.circle(px_, pz_, 6, "none", YEL, 1.6)
        sv.line(px_, pz_, tx_, tz_, YEL, 1.6, extra='marker-end="url(#tipy)"')
    sv.text(cxp, czp + rp + 34, "타는 곳(동) → 북 → 내리는 곳(서)", 11.5, SUB, 400, "middle")
    sv.text(cxp, czp + rp + 52, "폰이 도는 만큼 카메라도 반 바퀴 돈다", 11.5, YEL, 700, "middle")
    sv.text(cxp, czp + 30, "원통", 12, BG, 700, "middle")
    for az_, lab in [(0, "동"), (90, "북"), (180, "서")]:
        sv.text(cxp + (rp + 66) * math.cos(math.radians(az_)), czp - (rp + 66) * math.sin(math.radians(az_)) + 4, lab, 11.5, GREY, 400, "middle")
    tx = 930.0
    rows = [("서 있는 반원 벽(옆으로만)", "33.8초 · 소모 19.3초 · 한 구간에 7.8명", False),
            ("원통 초당 12°", "20.2초 · 매달리면 소모 0 · 5.7명", False),
            ("원통 초당 15° - 고른 값", "17.1초 · 매달리면 소모 0 · 5.2명", True),
            ("원통 초당 18°", "15.1초 · 매달리면 소모 0 · 4.9명", False)]
    for r, (a_, b_, pick) in enumerate(rows):
        yy = 566 + r * 44
        if pick:
            sv.rect(tx - 8, yy - 17, 296, 40, RIDE, "none", 1, .22, 'rx="4"')
        sv.text(tx, yy, a_, 12.5, INK, 700)
        sv.text(tx, yy + 17, b_, 11.5, SUB)
    sv.notes(tx, 756, ["서 있는 벽은 17 m를 옆으로만 탄다:", "같은 키 하나로 19초, 세 번 쉬고,", "12명이 한 벽에 밀린다.",
                       "원통은 매달려 쉬는 동안에도 나아가고", "같은 쪽으로 타면 두 배 가까이 빨라진다", "(7.6초, 소모 7.6)."], 12, 18)

    # ---- E. the hammer wall, from the front
    panel(24, 906, 590, 408, "⑦ 망치 벽 - 들락날락 쉼터", "망치 머리가 발판이다. 나와 있는 5초만 설 수 있다(주기 9초). 10 m는 한 번에 못 오른다.")

    def hx(x): return 62.0 + (x + 22) * 24.0
    def hy(y): return 1232.0 - (y - Y_F) * 24.0
    sv.rect(hx(-22), hy(Y_COURT), 16 * 24.0, 10 * 24.0, CLIMB, "none", 1, .82)
    sv.rect(hx(LEDGE_X[0]), hy(Y_COURT), 4 * 24.0, (Y_COURT - Y_DRUM) * 24.0, CLIMB, "none", 1, .82)
    sv.rect(hx(LEDGE_X[0]), hy(Y_DRUM), 4 * 24.0, 7, REST)
    sv.text(hx(-4), hy(Y_DRUM) + 22, "높은 턱(원통 위에서)", 11, REST, 700, "middle")
    sv.line(44, hy(Y_F), hx(-6) + 4, hy(Y_F), REST, 4)
    sv.line(44, hy(Y_COURT), hx(-2) + 8, hy(Y_COURT), REST, 4)
    sv.text(hx(-2) + 14, hy(Y_COURT) + 4, "시계 마당 15", 11, REST, 700)
    sv.text(hx(-6) + 10, hy(Y_F) + 4, "서쪽 마루 5", 11, REST, 700)
    now = 2.0
    for r, my in enumerate(MALLET_Y):
        for c, mx in enumerate(MALLET_X):
            d = c * 1.5 + r * 2.5
            out = ((now - d) % 9.0) < 5.0
            x0, y0 = hx(mx - .9), hy(my)
            if out:
                sv.rect(x0, y0, 1.8 * 24.0, 8, RIDE, "#ffffff", 1)
            else:
                sv.rect(x0, y0, 1.8 * 24.0, 8, "none", BG, 1.4, 1, 'stroke-dasharray="4 3"')
            sv.text(hx(mx), y0 + 21, f"{d:.1f}초", 10.5, BG, 700, "middle")
    route = [(-15.3, 5), (-15.3, 7.6), (-16.6, 7.6), (-16.6, 10.1), (-15.4, 10.1), (-15.4, 12.6), (-16.5, 12.6), (-16.5, 15)]
    sv.poly([(hx(x), hy(y) - (4 if y in MALLET_Y else 0)) for x, y in route], "none", "#ffffff", 2, 1, "6 4",
            close=False, extra='marker-end="url(#tip)"')
    for (a, b, s) in [(5, 7.6, "2.6"), (7.6, 10.1, "2.5"), (10.1, 12.6, "2.5"), (12.6, 15, "2.4 m")]:
        sv.line(hx(-22) - 8, hy(a) - 2, hx(-22) - 8, hy(b) + 2, CLIMB, 2)
        sv.text(hx(-22) - 13, (hy(a) + hy(b)) / 2 + 4, s, 11, CLIMB, 700, "end")
    sv.line(hx(-21), hy(14.0), hx(-9), hy(14.0), BG, 1.6, extra='marker-end="url(#tipd)"')
    sv.text(hx(-15), hy(14.0) - 7, "물결: 옆 칸은 1.5초씩, 윗줄은 2.5초 늦게", 11.5, BG, 700, "middle")
    sv.notes(40, 1264, ["· 채운 것 = 지금 나와 있는 망치, 점선 = 들어간 망치, 숫자 = 나오기 시작하는 때.",
                        "· 벽은 늘 탈 수 있다(망치가 들어가도 벽을 잡으면 산다). 폰 한 번, 룩 두세 번 쉰다."])

    # ---- F. the great dial, from the front
    panel(636, 906, 590, 408, "⑧ 대시계판 - 쉼터 셋과 처마 밑 모서리(ㄱ자)", "위는 처마가 막는다. 쉼터 C에서 모서리를 돌아 옆면으로 오른다 - 카메라가 따라 돈다.")

    def dx(x): return 668.0 + (x + 8) * 18.5
    def dy(y): return 1288.0 - (y - Y_COURT) * 18.5
    sv.rect(dx(-8), dy(Y_BELL), 16 * 18.5, 13 * 18.5, CLIMB, "none", 1, .82)
    sv.rect(dx(-8.4), dy(Y_BELL), 16.8 * 18.5, (Y_BELL - CORNICE) * 18.5, GREY)
    sv.text(dx(0), dy(27.1) + 4, "처마 - 못 넘는다", 11, BG, 700, "middle")
    # the east face, folded out to the right of the corner
    sv.rect(dx(8), dy(Y_BELL), 3.5 * 18.5, (Y_BELL - Y_BAL) * 18.5, CLIMB, "#ffffff", 1, .82, 'stroke-dasharray="3 3"')
    sv.rect(dx(8), dy(Y_BAL), 4 * 18.5, 7, REST)
    sv.text(dx(9.9), dy(Y_BAL) + 22, "옆 발코니 23", 11, REST, 700, "middle")
    sv.text(dx(9.8), dy(Y_BELL) - 7, "동쪽 면(펼침)", 11, SUB, 400, "middle")
    sv.line(dx(8), dy(Y_COURT), dx(8), dy(Y_BELL), "#ffffff", 1.4, "2 3")
    sv.line(dx(-8) - 14, dy(Y_COURT), dx(8) + 4 * 18.5, dy(Y_COURT), REST, 4)
    sv.line(dx(-8) - 14, dy(Y_BELL), dx(8) + 3.5 * 18.5 + 8, dy(Y_BELL), REST, 4)
    cx, cy, r = DIAL
    sv.circle(dx(cx), dy(cy), r * 18.5, "#fff3d6", "#fff3d6", 2, .3)
    for h in range(12):
        t = math.radians(90 - h * 30)
        sv.line(dx(cx + (r - .8) * math.cos(t)), dy(cy + (r - .8) * math.sin(t)),
                dx(cx + (r - .15) * math.cos(t)), dy(cy + (r - .15) * math.sin(t)), "#7a5310", 2.2)
    for (pxx, pyy, w, nm) in PERCH:
        sv.rect(dx(pxx - w / 2), dy(pyy), w * 18.5, 7, REST, BG, .8)
        if pxx > 0:
            sv.text(dx(pxx), dy(pyy) + 20, nm, 11.5, BG, 700, "middle")
    easy = [(3.0, 15), (3.0, 18.8), (5.2, 21.8), (7.2, 24.6), (7.95, 25.0), (8.9, 25.4), (9.4, 28)]
    fast = [(5.6, 15), (5.6, 21.8)]
    safe = [(7.4, 24.6), (9.0, 23.2), (10.4, 23.2), (10.4, 28)]
    sv.poly([(dx(x), dy(y)) for x, y in safe], "none", DOWN, 2.2, 1, "2 4", close=False)
    sv.poly([(dx(x), dy(y)) for x, y in fast], "none", YEL, 2.2, 1, "6 4", close=False)
    sv.poly([(dx(x), dy(y)) for x, y in easy], "none", "#ffffff", 2.2, 1, close=False, extra='marker-end="url(#tip)"')
    sv.circle(dx(8), dy(25.05), 5, CLIMB, "#ffffff", 1.2)
    sv.text(dx(-8) - 18, dy(Y_COURT) + 4, "15", 11, REST, 700, "end")
    sv.text(dx(-8) - 18, dy(Y_BELL) + 4, "28", 11, REST, 700, "end")
    lx, ly = 1052.0, 978.0
    for i, (col, dash, a, b, c) in enumerate([("#ffffff", "", "쉼터 길", "3.8 · 3.0↗ · 2.8↗ ·", "모서리 돌아 3.4 m"),
                                              (YEL, "6 4", "빠른 길", "B까지 6.8 m 한 번에", "폰 71% · 룩은 못 함"),
                                              (DOWN, "2 4", "발코니 길", "C에서 내려서 5 m", "모서리가 겁나면")]):
        yy = ly + i * 72
        sv.line(lx, yy, lx + 30, yy, col, 2.4, dash)
        sv.text(lx + 38, yy + 4, a, 12.5, INK, 700)
        sv.text(lx, yy + 22, b, 11.5, SUB)
        sv.text(lx, yy + 39, c, 11.5, SUB)
    sv.notes(lx, 1204, ["가장 큰 피치(마지막):", "폰 54% · 룩 78%", "쉼터 한 곳에 폰 두셋", "떨어지면 시계 마당,", "모서리에서는 발코니로"], 11.5, 17)
    sv.save(f"{RN}_RoyalClockTower_Devices.svg")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    plan_and_side()
    iso()
    devices()
    print(f"path length {path_length():.1f} m")
