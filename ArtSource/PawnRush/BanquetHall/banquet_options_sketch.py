# Sketches for the answer to the review of the banquet hall's v0.3 (R805): two ways to lay the hall out so the way
# never forks, the hall cut down its middle with the chandeliers raised and a grand piano on the gallery, and three
# small pictures of the proposed chair, toast and piano. Sketches only: the figures are rough, the real numbers come
# with v0.4 in banquet_layout.py.
#
#   python banquet_options_sketch.py <out.svg>
import math
import sys

INK, PAPER, ORANGE, PURPLE, OLIVE, GOLD, CREAM, WALNUT, RED, SLATE, DARK, GREY, GREEN = (
    "#2b2622", "#fbf6ea", "#e8853a", "#8a5cc2", "#6f8a4b", "#c9972e", "#f3e6c8", "#8a5a36", "#c8472f", "#8fa0ad", "#3a3531", "#6b6258", "#3f8f5a")
out = []


def w(s):
    out.append(s)


def text(x, y, s, size=12, anchor="start", weight="normal", fill=INK):
    w('<text x="%.1f" y="%.1f" font-size="%d" text-anchor="%s" font-weight="%s" fill="%s">%s</text>' % (x, y, size, anchor, weight, fill, s))


def box(x, y, wd, ht, fill, stroke=INK, width=1.0, extra=""):
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (x, y, wd, ht, fill, stroke, width, extra))


def dot(x, y, r, fill, stroke=INK, width=1.0, extra=""):
    w('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (x, y, r, fill, stroke, width, extra))


def seg(x0, y0, x1, y1, stroke=INK, width=1.0, extra=""):
    w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="%.1f" %s/>' % (x0, y0, x1, y1, stroke, width, extra))


def path(points, stroke, width, extra="", fill="none"):
    w('<polyline points="%s" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (" ".join("%.1f,%.1f" % p for p in points), fill, stroke, width, extra))


def arrow(points, colour, width=3.0):
    # A way drawn as a line with a head at its end.
    path(points, colour, width, 'stroke-linejoin="round" stroke-linecap="round"')
    (x0, y0), (x1, y1) = points[-2], points[-1]
    a = math.atan2(y1 - y0, x1 - x0)
    head = [(x1 + 4 * math.cos(a), y1 + 4 * math.sin(a)), (x1 - 9 * math.cos(a - 0.5), y1 - 9 * math.sin(a - 0.5)), (x1 - 9 * math.cos(a + 0.5), y1 - 9 * math.sin(a + 0.5))]
    w('<polygon points="%s" fill="%s"/>' % (" ".join("%.1f,%.1f" % p for p in head), colour))


def pawn(x, y, s=1.0, colour="#ffffff"):
    w('<path d="M %.1f %.1f l %.1f %.1f l %.1f 0 z" fill="%s" stroke="%s" stroke-width="1"/>' % (x, y - 12 * s, -5 * s, 12 * s, 10 * s, colour, INK))
    dot(x, y - 14 * s, 4 * s, colour, INK, 1)


# ---------------------------------------------------------------- the three plans
S = 4.6


def plan(X0, Y0, kind, title, note):
    def px(x):
        return X0 + (x + 34.0) * S

    def pz(z):
        return Y0 + (88.0 - z) * S

    def rect(x0, x1, z0, z1, fill, stroke=INK, width=1.0, extra=""):
        box(px(x0), pz(z1), (x1 - x0) * S, (z1 - z0) * S, fill, stroke, width, extra)

    def circle(x, z, r, fill, stroke=INK, width=1.0, extra=""):
        dot(px(x), pz(z), r * S, fill, stroke, width, extra)

    def way(points, colour, width=3.0):
        arrow([(px(x), pz(z)) for x, z in points], colour, width)

    def guests(xs, zs):
        for x in xs:
            for z in zs:
                circle(x, z, 1.5, "#cfd6dc", SLATE, 0.8)

    def piano(cx, north):
        # keys along the gallery's front edge, the body behind them, the lid's free edge
        rect(cx - 6, cx + 6, north - 2.2, north, "#ffffff", INK, 1.0)
        for k in range(1, 12):
            seg(px(cx - 6 + k), pz(north), px(cx - 6 + k), pz(north - 2.2), INK, 0.5)
        pts = [(cx - 6, north - 2.2), (cx + 6, north - 2.2), (cx + 6, north - 9), (cx + 3, north - 13.5), (cx - 2, north - 14.5), (cx - 6, north - 10)]
        w('<polygon points="%s" fill="#3a3531" stroke="%s" stroke-width="1"/>' % (" ".join("%.1f,%.1f" % (px(x), pz(z)) for x, z in pts), INK))
        seg(px(cx + 5.4), pz(north - 2.6), px(cx + 5.4), pz(north - 9), ORANGE, 3.0)

    def chandeliers(x, z0, z1):
        for lane in (-2.6, 2.6):
            seg(px(x + lane), pz(z0), px(x + lane), pz(z1), PURPLE, 1.0, 'stroke-dasharray="4 3"')
            for k in range(5):
                circle(x + lane, z0 + 4 + (z1 - z0 - 8) * k / 4.0, 1.5, "#f7e3a6", ORANGE, 1.2)

    def cake(x, z):
        circle(x, z, 9.0, CREAM, WALNUT, 1.2)
        for r, c in ((7.0, "#f7cfc6"), (5.4, "#f9d9d2"), (4.0, "#fbe6df"), (2.6, "#fffaf6")):
            circle(x, z, r, c, ORANGE, 1.2)
        seg(px(x), pz(z + 1), px(x), pz(72.5), "#8f989f", 4.0)

    text(X0, Y0 - 26, title, 14, "start", "bold", INK if kind != "C" else GREY)
    text(X0, Y0 - 9, note, 11, "start", "normal", "#4a433c")
    rect(-32, 32, 0, 72, "#e9e2d2", WALNUT, 2.5)
    rect(-6, 6, 72, 84, "#efe2bf", WALNUT, 1.5)
    arms, table = (kind in ("A", "C")), kind == "B"
    if arms:
        gal = 20.0 if kind == "A" else 16.4
        for i in range(9):
            for j in range(11):
                if (i + j) % 2 == 0 and gal + j * 3 < 50:
                    rect(-13.5 + i * 3, -10.5 + i * 3, gal + j * 3, min(gal + j * 3 + 3, 50), "#ddd3bd", "none", 0)
        for s in (-1, 1):
            x0, x1 = sorted((s * 14, s * 26))
            rect(x0, x1, 4, 58, CREAM, WALNUT, 1.5)
        rect(-26, 26, 50, 58, CREAM, WALNUT, 1.5)
        rect(-13.8, 13.8, 50.2, 57.6, CREAM, "none", 0)
        for s in ((-1,) if kind == "A" else (-1, 1)):
            x0, x1 = sorted((s * 17.5, s * 22.5))
            rect(x0, x1, 6, 56.5, OLIVE, "none", 0, 'opacity="0.7"')
            x0, x1 = sorted((s * 17.5, s * 9))
            rect(x0, x1, 51.5, 56.5, OLIVE, "none", 0, 'opacity="0.7"')
        if kind == "A":
            guests((-27.6, -12.4), range(9, 48, 5))
            guests((27.6, 12.4, 20.0), range(9, 48, 5))
            for x in (-24.5, -20, -15.5):
                rect(x - 1.4, x + 1.4, 1.0, 3.8, "#d9c6ea", PURPLE, 1.2)
        rect(-14, 14, 0, gal, "#e0cb98", WALNUT, 1.5)
        rect(-10.5, 10.5, 37, 43, CREAM, WALNUT, 1.5)
        circle(0, gal + 8.4, 5.7, "none", PURPLE, 1.0, 'stroke-dasharray="3 3"')
        for (x, z) in ((0, gal + 2.7), (0, gal + 14.1), (7.6, gal + 8.4), (-7.6, gal + 8.4)):
            circle(x, z, 1.8, "#f6dcc6", ORANGE, 1.6)
        for s in (-1, 1):
            for (x, z) in ((-7.5, 52), (-10.5, 46), (-7.5, 40), (-4.5, 46)):
                rect(s * x - 1.4, s * x + 1.4, z - 1.4, z + 1.4, "none", PURPLE, 1.0)
        if kind == "A":
            piano(0.0, gal)
        else:
            for k in range(9):
                circle(-10 + k, 12.2, 0.3, ORANGE, INK, 0.4)
        chandeliers(0.0, 10.0, 56.0)
        cake(0.0, 62.5)
        if kind == "A":
            way([(0, 0), (-20, 1.6), (-20, 6), (-20, 54), (-10, 54)], OLIVE, 3.4)
            way([(-9, 53), (-10.5, 46), (-7.5, 40), (-1, 37.4), (-4.5, gal + 11), (0, gal + 2.6), (0, gal + 0.2)], PURPLE, 3.0)
            way([(0, gal - 1), (4.5, gal - 6), (0.6, 13), (0.6, 30), (0.6, 54), (0, 62), (0, 71), (0, 81)], ORANGE, 3.4)
        else:
            for s in (-1, 1):
                way([(0, 0), (s * 20, 1.6), (s * 20, 6), (s * 20, 54), (s * 10, 54)], GREY, 2.6)
            text(px(0), pz(25), "길이 둘로 갈린다", 12, "middle", "bold", RED)
    if table:
        for i in range(8):
            for j in range(12):
                if (i + j) % 2 == 0:
                    rect(-31 + i * 2.7, -28.3 + i * 2.7, 20 + j * 3, 23 + j * 3, "#ddd3bd", "none", 0)
        rect(-24, 24, 0, 20, "#e0cb98", WALNUT, 1.5)
        rect(-7, 7, 26, 58, CREAM, WALNUT, 1.5)
        rect(-2.5, 2.5, 28, 56, OLIVE, "none", 0, 'opacity="0.7"')
        guests((-8.8, 8.8), range(30, 56, 5))
        for x in (-4.5, 0, 4.5):
            rect(x - 1.4, x + 1.4, 22.6, 25.4, "#d9c6ea", PURPLE, 1.2)
        rect(-26, -13, 39, 45, CREAM, WALNUT, 1.5)
        circle(-19.5, 29.5, 5.7, "none", PURPLE, 1.0, 'stroke-dasharray="3 3"')
        for (x, z) in ((-19.5, 23.8), (-19.5, 35.2), (-11.9, 29.5), (-27.1, 29.5)):
            circle(x, z, 1.8, "#f6dcc6", ORANGE, 1.6)
        circle(19.5, 36, 5.7, "none", GREY, 1.0, 'stroke-dasharray="3 3"')
        for (x, z) in ((19.5, 30.3), (19.5, 41.7), (27.1, 36), (11.9, 36)):
            circle(x, z, 1.8, "#e3e0da", GREY, 1.2)
        for (x, z) in ((-5.2, 54.5), (-11.5, 51.5), (-17.5, 45.5)):
            rect(x - 1.4, x + 1.4, z - 1.4, z + 1.4, "none", PURPLE, 1.0)
        piano(0.0, 20.0)
        chandeliers(0.0, 10.0, 56.0)
        cake(0.0, 63.0)
        way([(0, 0), (0, 21.5), (0, 27), (0, 54), (-4, 54.5)], OLIVE, 3.4)
        way([(-5.2, 54.5), (-11.5, 51.5), (-17.5, 45.5), (-19.5, 39), (-25, 31), (-19.5, 23.6), (-19.5, 20.4)], PURPLE, 3.0)
        way([(-19.5, 19), (-8, 18.8), (4.5, 14), (0.6, 13), (0.6, 30), (0.6, 54), (0, 62), (0, 71), (0, 81)], ORANGE, 3.4)
        text(px(19.5), pz(47.5), "동쪽도 춤", 10, "middle", "normal", GREY)
        text(px(19.5), pz(47.5) + 12, "(길이 아님)", 10, "middle", "normal", GREY)
    w('<path d="M %.1f %.1f l -6 11 l 12 0 z" fill="%s"/>' % (px(0), pz(0) + 1, DARK))


w('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1400 1390" font-family="Malgun Gothic, Apple SD Gothic Neo, sans-serif">')
w('<rect width="1400" height="1390" fill="%s"/>' % PAPER)
text(34, 34, "대연회장 v0.3 검수 뒤 — 고칠 다섯 가지의 안 (스케치)", 19, "start", "bold")
text(34, 56, "수치는 대략이다. 고른 안으로 v0.4를 지을 때 banquet_layout.py에서 정확히 낸다. 초록 = 식탁으로 올라가는 길, 보라 = 춤에 실려 내려오는 길, 주황 = 샹들리에로 다시 올라가는 길.", 12)
text(34, 90, "1. 길이 갈리지 않게 — 홀의 배치", 15, "start", "bold", RED)
plan(40.0, 150.0, "A", "안 A (권장) — 한쪽 식탁만 길", "v0.3 그대로. 서쪽 식탁으로만 올라간다. 동쪽 식탁은 손님 자리.")
plan(500.0, 150.0, "B", "안 B — 긴 식탁 하나를 홀 가운데로", "식탁 위에 샹들리에, 춤은 식탁 둘레. 길은 서쪽 춤으로 내려온다.")
plan(960.0, 150.0, "C", "지금 (v0.3)", "양쪽 식탁으로 갈려 상석에서 만난다 — 58 m 동안 떨어져 있다.")
for x, lines in ((40, ("손님 기물이 식탁 양쪽에 앉아 있다(회청색 점).", "안쪽에 앉은 손님이 무도장 쪽 울타리가 된다.", "피아노가 악단석을 채운다(악단석이 3.6 m 깊어진다).")),
                 (500, ("가장 연회장다운 그림: 긴 식탁 위로 샹들리에가 걸린다.", "악단석이 홀 폭으로 넓어지고, 무도장이 좌우로 나뉜다.", "왈츠가 한쪽(서)에만 길이 된다. 바꿀 것이 가장 많다.")),
                 (960, ("열두 명이 여섯씩 갈리고, 건너편은 보이기만 한다.", "→ 이 안을 뺀다.", ""))):
    for i, line in enumerate(lines):
        text(x, 574 + i * 16, line, 11, "start", "normal", "#4a433c")

# ---------------------------------------------------------------- the hall cut down its middle, plan A, the new heights
text(34, 652, "2. 샹들리에를 높이고, 하프 대신 피아노 — 가운데를 남북으로 자른 모습 (안 A)", 15, "start", "bold", RED)
CX, CY, CS = 70.0, 990.0, 8.6


def cut(z, y):
    return CX + z * CS, CY - y * CS


def slab(z0, z1, y0, y1, fill, stroke=INK, width=1.0, extra=""):
    a, b = cut(z0, y1), cut(z1, y0)
    box(a[0], a[1], b[0] - a[0], b[1] - a[1], fill, stroke, width, extra)


box(40, 668, 800, 344, "#f4eedf", INK, 1.0)
slab(0, 84, -0.6, 0, "#cfc6b3", "none", 0)
for h, label in ((0, "0"), (4.4, "4.4"), (7.4, "7.4"), (19.0, "19"), (24.0, "24"), (34.0, "34")):
    p = cut(0, h)
    text(CX - 4, p[1] + 4, label, 10, "end", "normal", GREY)
seg(*cut(0, 34), *cut(84, 34), WALNUT, 2.0)
text(cut(83, 34)[0], cut(83, 34)[1] - 5, "천장 34 (지금 26)", 11, "end", "bold", WALNUT)
seg(*cut(0, 26), *cut(72, 26), GREY, 1.0, 'stroke-dasharray="5 4"')
# the gallery, the piano on it
slab(0, 20, 4.5, 5.0, "#efe2bf", WALNUT, 1.5)
for z in (3, 11, 18.4):
    slab(z - 0.4, z + 0.4, 0, 4.5, "#d8cfbc", "none", 0)
slab(17.8, 20.6, 6.8, 7.4, "#ffffff", INK, 1.2)
slab(5, 18, 5.8, 8.6, "#3a3531", INK, 1.0)
for z in (6, 16.5):
    slab(z - 0.3, z + 0.3, 5.0, 5.8, "#3a3531", "none", 0)
lid = [cut(5.2, 8.6), cut(17.6, 8.6), cut(17.6, 19.0), cut(5.2, 19.0)]
w('<polygon points="%s" fill="%s" fill-opacity="0.28" stroke="%s" stroke-width="2.5"/>' % (" ".join("%.1f,%.1f" % p for p in lid), ORANGE, ORANGE))
text(cut(11.4, 14.2)[0], cut(11.4, 14.2)[1], "피아노 뚜껑", 12, "middle", "bold", ORANGE)
text(cut(11.4, 14.2)[0], cut(11.4, 14.2)[1] + 14, "열리며 실어 올린다", 10, "middle", "normal", "#4a433c")
text(cut(21.2, 10.6)[0], cut(21.2, 10.6)[1], "건반 7.4 = 여왕의 어깨 높이", 10, "start", "bold", INK)
seg(cut(21.0, 10.2)[0], cut(21.0, 10.2)[1], cut(20.2, 7.7)[0], cut(20.2, 7.7)[1], INK, 0.8)
text(cut(1, 5.0)[0], cut(1, 5.0)[1] - 5, "악단석 5.0", 10, "start", "bold", WALNUT)
text(cut(1, 2.0)[0], cut(1, 2.0)[1], "배식실 (입구)", 10, "start", "normal", WALNUT)
# dancers, dais, head table, the cake on its stand, the balcony
for z, fill, stroke in ((23.4, "#f6dcc6", ORANGE), (34.8, "#f6dcc6", ORANGE), (29.1, "#cfd6dc", SLATE)):
    p = [cut(z - 1.8, 0), cut(z - 1.3, 7.4), cut(z + 1.3, 7.4), cut(z + 1.8, 0)]
    w('<polygon points="%s" fill="%s" stroke="%s" stroke-width="1.6" fill-opacity="0.85"/>' % (" ".join("%.1f,%.1f" % q for q in p), fill, stroke))
    c = cut(z, 8.2)
    dot(c[0], c[1], 0.8 * CS, GOLD, INK, 0.8)
slab(37, 43, 3.9, 4.4, CREAM, WALNUT, 1.2)
slab(38.6, 41.4, 0, 3.9, "#d8cfbc", "none", 0)
slab(50, 71, 3.9, 4.4, CREAM, WALNUT, 1.2)
slab(52, 69, 0, 3.9, "#d8cfbc", "none", 0)
slab(60.6, 64.4, 4.4, 12.4, GOLD, INK, 0.8)
slab(54.5, 70.5, 12.4, 13.0, GOLD, INK, 0.8)
bottom = 13.0
for r, top, c in ((6.8, 15.8, "#f9d9d2"), (5.4, 18.6, "#fbe6df"), (4.0, 21.4, "#fdf1ec"), (2.6, 24.2, "#fffaf6")):
    slab(62.5 - r, 62.5 + r, bottom, top, c, ORANGE, 1.6)
    bottom = top
text(cut(62.5, 9.0)[0] + 20, cut(62.5, 9.0)[1], "높은 케이크 받침(못 탄다)", 10, "start", "normal", "#4a433c")
text(cut(69.6, 15.8)[0] + 6, cut(69.6, 15.8)[1] + 4, "내리는 턱 15.8", 10, "start", "bold", ORANGE)
slab(72, 72.8, 0, 23.0, "#b9ae9a", "none", 0)
slab(72, 84, 23.0, 23.8, "#efe2bf", WALNUT, 1.5)
a, b = cut(63.5, 24.2), cut(72.6, 23.8)
seg(a[0], a[1] - 2, b[0], b[1] - 2, "#8f989f", 5.0)
text(cut(78, 23.8)[0], cut(78, 23.8)[1] - 8, "발코니 23.8", 11, "middle", "bold", WALNUT)
# the chandeliers: where they are now (grey) and where they go (orange)
for k in range(5):
    z = 21.5 + k * 9.3
    old_p, old_e = 20.3 - k * 0.8, 15.4 - k * 0.8
    new_p, new_e = 26.0 - k * 0.7, 20.0 - k * 0.7
    for sgn in (-1, 1):
        a, b = cut(z, old_p), cut(z + sgn * 4.2, old_e)
        seg(a[0], a[1], b[0], b[1], "#b9b1a4", 1.0, 'stroke-dasharray="4 3"')
        a, b = cut(z, new_p), cut(z + sgn * 4.2, new_e)
        seg(a[0], a[1], b[0], b[1], ORANGE, 1.8)
        m = (a[0] + (b[0] - a[0]) * 0.62, a[1] + (b[1] - a[1]) * 0.62)
        w('<ellipse cx="%.1f" cy="%.1f" rx="11" ry="3.5" fill="#f7e3a6" stroke="%s" stroke-width="1"/>' % (m[0], m[1], GOLD))
        dot(b[0], b[1], 2.6, DARK, DARK, 0)
    a = cut(z, new_p)
    dot(a[0], a[1], 3, GOLD, GOLD, 0)
a, b = cut(40.2, 13.6), cut(40.2, 18.4)
arrow([a, b], RED, 2.0)
text(a[0] + 8, (a[1] + b[1]) / 2 + 4, "약 5 m 위로", 11, "start", "bold", RED)
text(cut(30, 11.6)[0], cut(30, 11.6)[1], "(회색 점선 = 지금 높이)", 10, "middle", "normal", GREY)
text(cut(46, 29.4)[0], cut(46, 29.4)[1], "⑦ 샹들리에: 줄 끝 20 → 17 m (지금 15 → 12)", 12, "middle", "bold", ORANGE)
# beside it, in words
for i, (head, body) in enumerate((
        ("샹들리에", "줄 끝을 15 → 12 m에서 20 → 17 m로. 왕관(9.7 m)과 10 m 떨어진다. 천장 26 → 34 m."),
        ("그러려면", "출발점과 도착점이 같이 올라가야 한다 —"),
        ("  출발", "초 위(14.8) 대신 피아노 뚜껑 끝(약 19 m)."),
        ("  도착", "케이크를 높은 받침 위에 올린다. 내리는 턱 10.2 → 15.8 m,"),
        ("", "꼭대기 18.6 → 24.2 m, 발코니 18.2 → 23.8 m. 오르는 것은 그대로 세 층."),
        ("피아노", "하프는 좁고 높은 것, 피아노는 낮고 넓은 것이다. 그래서 '타고 오르기'가"),
        ("", "'건반을 건너고 → 뚜껑에 실려 오르기'로 바뀐다. 악단석이 피아노만큼 커진다"),
        ("", "(깊이 16 → 20 m). 윗단과 큰 촛대는 빠지고 피아노가 그 높이를 맡는다."),
        ("낙사", "8 m 낙하 규칙 그대로. 높아질수록 떨어지는 것이 더 분명해진다."))):
    text(862, 690 + i * 22, head, 12, "start", "bold", RED if head.strip() in ("샹들리에", "피아노") else INK)
    text(930, 690 + i * 22, body, 12, "start", "normal", "#3a3531")

# ---------------------------------------------------------------- three small pictures: the chair, the toast, the piano
text(34, 1052, "3. 식탁보 대신 의자 · 한눈에 읽히는 건배 · 피아노", 15, "start", "bold", RED)
for i, (title, sub) in enumerate((("① 의자 — 손님을 앉힌다", "시종이 의자를 밀어 넣었다 뺀다. 사다리 등받이를 탄다."),
                                  ("② 건배 — 손님이 다 같이", "'땡 땡 땡' → 모두 잔을 든다 → 러너 위에서 '챙'."),
                                  ("⑤ 피아노 — 건반 · 해머 · 뚜껑", "연주하는 손을 피해 건너고, 뚜껑에 실려 오른다."))):
    X = 40 + i * 450
    box(X, 1068, 430, 290, "#f4eedf", INK, 1.0)
    text(X + 12, 1090, title, 13, "start", "bold")
    text(X + 12, 1107, sub, 11, "start", "normal", "#4a433c")

# the chair: the table on the right, the chair pushed in (solid) and drawn out (dashed)
X, G = 40.0, 1330.0
seg(X + 14, G, X + 416, G, DARK, 2.0)
box(X + 250, G - 110, 160, 12, CREAM, WALNUT, 1.5)
box(X + 380, G - 98, 14, 98, "#d8cfbc", "none", 0)
text(X + 334, G - 118, "식탁 4.4", 11, "middle", "bold", WALNUT)
for off, dash, colour in ((0, "", ORANGE), (-74, 'stroke-dasharray="5 4"', "#c9a27c")):
    bx = X + 214 + off
    seg(bx, G, bx, G - 158, colour, 5.0, dash)                        # the back: legs and the ladder of slats
    for k in range(7):
        seg(bx, G - 20 - k * 22, bx + 16, G - 20 - k * 22, colour, 2.0, dash)
    seg(bx + 16, G - 66, bx + 16, G - 158, colour, 2.0, dash)
    seg(bx, G - 66, bx + 62, G - 66, colour, 4.0, dash)               # the seat, going under the table
    seg(bx + 62, G, bx + 62, G - 66, colour, 3.0, dash)
pawn(X + 208, G - 118, 1.0)
arrow([(X + 150, G - 176), (X + 205, G - 176)], PURPLE, 2.0)
arrow([(X + 205, G - 190), (X + 150, G - 190)], PURPLE, 2.0)
text(X + 178, G - 198, "6초에 한 번", 10, "middle", "bold", PURPLE)
arrow([(X + 222, G - 162), (X + 262, G - 128)], DARK, 1.6)
text(X + 272, G - 150, "들어갔을 때 건너간다", 10, "start", "bold", DARK)
w('<path d="M %.1f %.1f l -14 36 l 28 0 z" fill="#cfd6dc" stroke="%s" stroke-width="1"/>' % (X + 96, G - 44, SLATE))
dot(X + 96, G - 50, 9, "#cfd6dc", SLATE, 1)
text(X + 96, G - 68, "시종", 10, "middle", "normal", GREY)
text(X + 20, G - 132, "등받이 = 타는 면", 10, "start", "bold", ORANGE)
text(X + 20, G - 118, "(바닥에서 4.9 m)", 10, "start", "normal", "#4a433c")

# the toast: the table seen end on, a guest either side, the glasses meeting over the runner; under it, the runner from above
X = 490.0
seg(X + 14, G - 62, X + 416, G - 62, DARK, 1.2)
box(X + 130, G - 142, 170, 10, CREAM, WALNUT, 1.5)
box(X + 190, G - 146, 50, 4, OLIVE, "none", 0)
for sgn in (-1, 1):
    cx = X + 215 + sgn * 128
    w('<polygon points="%.1f,%.1f %.1f,%.1f %.1f,%.1f %.1f,%.1f" fill="#cfd6dc" stroke="%s" stroke-width="1.2"/>' % (
        cx - 30, G - 62, cx - 17, G - 200, cx + 17, G - 200, cx + 30, G - 62, SLATE))
    dot(cx, G - 214, 15, "#cfd6dc", SLATE, 1.2)
    seg(cx - sgn * 14, G - 186, X + 215 + sgn * 12, G - 200, SLATE, 5.0)
    w('<path d="M %.1f %.1f l %.1f -22 l %.1f 0 z" fill="#d9c6ea" stroke="%s" stroke-width="1.2"/>' % (X + 215 + sgn * 12, G - 196, sgn * -9, sgn * 18, PURPLE))
text(X + 215, G - 228, "챙!", 14, "middle", "bold", RED)
pawn(X + 215, G - 146, 0.9)
text(X + 215, G - 122, "러너", 10, "middle", "normal", GREY)
box(X + 30, G - 44, 370, 26, OLIVE, "none", 0, 'opacity="0.55"')
for k in range(5):
    zx = X + 48 + k * 80
    box(zx, G - 44, 44, 26, RED, "none", 0, 'opacity="0.55"')
    if k < 4:
        dot(zx + 62, G - 31, 6, GREEN, INK, 0.8)
text(X + 30, G - 50, "위에서 본 러너: 붉은 띠 = 잔이 만나는 자리, 초록 = 꽃 장식 옆 안전한 자리", 10, "start", "normal", "#4a433c")
text(X + 215, G - 4, "모두가 같이 멈추고 같이 달린다 (8초에 한 번)", 10, "middle", "bold", DARK)

# the piano: the keys with the playing hands, the hammers under the strings, the lid lifting
X = 940.0
seg(X + 14, G, X + 416, G, DARK, 2.0)
box(X + 40, G - 70, 300, 44, "#3a3531", INK, 1.0)
for xx in (X + 60, X + 310):
    box(xx, G - 26, 12, 26, "#3a3531", "none", 0)
box(X + 22, G - 84, 70, 14, "#ffffff", INK, 1.2)
text(X + 26, G - 12, "건반", 10, "start", "bold", INK)
for hx, down in ((X + 44, 0), (X + 74, 8)):
    w('<path d="M %.1f %.1f q 8 -16 16 0 l 0 10 l -16 0 z" fill="#ffffff" stroke="%s" stroke-width="1.2"/>' % (hx - 8, G - 108 + down, INK))
text(X + 58, G - 122, "연주하는 손", 10, "middle", "bold", RED)
seg(X + 100, G - 76, X + 334, G - 76, GOLD, 1.6)
for k in range(6):
    hx = X + 120 + k * 36
    up = 8 if k in (1, 4) else 0
    seg(hx, G - 44, hx, G - 62 - up, "#d9c6ea", 3.0)
    dot(hx, G - 64 - up, 4, RED if up else "#d9c6ea", INK, 0.8)
text(X + 214, G - 36, "해머가 줄을 친다", 10, "middle", "bold", "#d9c6ea")
pawn(X + 196, G - 78, 0.8)
hinge = (X + 336, G - 72)
LID = 176.0
for ang, dash, colour in ((16, 'stroke-dasharray="5 4"', "#c9a27c"), (56, "", ORANGE)):
    ex, ey = hinge[0] - LID * math.cos(math.radians(ang)), hinge[1] - LID * math.sin(math.radians(ang))
    seg(hinge[0], hinge[1], ex, ey, colour, 5.0, dash)
ex, ey = hinge[0] - LID * math.cos(math.radians(56)), hinge[1] - LID * math.sin(math.radians(56))
pawn(ex + 10, ey + 22, 0.9)
w('<path d="M %.1f %.1f A %.1f %.1f 0 0 1 %.1f %.1f" fill="none" stroke="%s" stroke-width="1.6" stroke-dasharray="3 3"/>' % (
    hinge[0] - LID * math.cos(math.radians(16)), hinge[1] - LID * math.sin(math.radians(16)), LID, LID, ex, ey, PURPLE))
text(X + 122, G - 132, "뚜껑이 열리며", 10, "start", "bold", ORANGE)
text(X + 122, G - 119, "실어 올린다", 10, "start", "bold", ORANGE)
seg(ex + 30, ey - 26, ex + 16, ey + 2, ORANGE, 1.8)
w('<ellipse cx="%.1f" cy="%.1f" rx="12" ry="4" fill="#f7e3a6" stroke="%s" stroke-width="1"/>' % (ex + 25, ey - 16, GOLD))
text(ex + 42, ey - 14, "첫 샹들리에", 10, "start", "bold", ORANGE)
w('</svg>')
open(sys.argv[1], "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("drawn", sys.argv[1])
