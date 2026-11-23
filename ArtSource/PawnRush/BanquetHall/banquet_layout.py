# The grand banquet hall (Pawn Rush 2-4, level design v0.1): the one place its numbers are worked out.
# Section frame: the entry's threshold is the origin, x east, y up, z north (the way in).
#
#   python banquet_layout.py                 prints the layout, the pitches and the way's length
#   python banquet_layout.py <plan.svg>      also draws the plan and the height profile
#
# banquet_blockout.py (the Blender preview) runs this file for the same numbers. Nothing here is in Unity.
import math
import sys


def heading(deg):
    # A level direction by its heading: 0 north (+z), 90 east (+x).
    return (math.sin(math.radians(deg)), math.cos(math.radians(deg)))


def add(p, d, deg):
    h = heading(deg)
    return (p[0] + d * h[0], p[1] + d * h[1])


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def toward(a, b, d):
    # The point d from a on the way to b.
    k = d / dist(a, b)
    return (a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)


# ---------------------------------------------------------------- pawn numbers (RagdollTuning.asset, RagdollPawn)
CLIMB_UP, CLIMB_SIDE, STAMINA, ROOK = 1.2, 0.9, 8.0, 0.7
RUN, SPRINT, JUMP_RUN = 2.5, 3.6, 1.75

# ---------------------------------------------------------------- levels
KITCHEN, TABLE = 0.0, 4.4
HALL = (-16.0, 38.0, 0.0, 102.0)                 # x0, x1, z0, z1

# 1 the tablecloth: six flowing lanes on the long table's south face
TABLE_X, TABLE_Z0, TABLE_Z1 = 6.0, 16.0, 54.0    # half width, south end, north end
CLOTH_LANES, LANE_WIDTH, CLOTH_HEM = 6, 2.0, 1.6
CLOTH_UP, CLOTH_DOWN = 0.9, 1.2                  # m/s a lane carries a climber up, and down
CLOTH_CYCLE = (3.2, 0.8, 3.2, 0.8)               # up, still, down, still (s); lanes a third of it apart

# 2 the long table: the runner lane and the toasting goblets
RUNNER = 2.5                                     # half width of the lane between the place settings
GOBLETS = (25.0, 33.0, 41.0, 49.0)               # z of each pair
TOAST = (1.9, 0.5, 0.3, 0.3, 0.6)                # upright, lean back, swing in, clink, back (s)
TOAST_WAVE = 0.9                                 # each pair this much after the one before it (s)
RUNNER_JERK = (1.5, 0.4)                         # metres back, over seconds, at each clink

# 3 the great candelabra: a base drum, three arms, three candles with dripping wax
DRUM, DRUM_R, DRUM_TOP = (0.0, 58.5), 3.2, 7.4
CANDLE_R, DISH_R, CANDLE_TOP = 1.0, 2.0, 10.6
WAX_LANES, WAX_EVERY, WAX_WARN, WAX_SPEED = 6, 0.9, 0.7, 2.2

# 4 the chandeliers: a leap up onto the first one's skirt, a drop to the second
RING_R, LEAP = 2.6, 2.3
C1 = add(DRUM, 12.1, 12.0)
C1_TOP, C1_SKIRT = 13.2, 3.2
CANDLES = tuple(add(C1, CANDLE_R + LEAP + RING_R, 192.0 + a) for a in (41.7, 0.0, -41.7))
C2 = add(C1, RING_R * 2 + 2.2, 35.0)
C2_TOP, C2_SKIRT = 10.4, 2.4
SWING, SWING_PERIOD = 0.9, 5.2                   # metres either way along the way, seconds

# 5 the cake: four tiers turning against each other, cream stripes to climb and fondant that cannot be held
TIERS = ((7.0, 7.4, 6.0), (5.5, 10.2, -10.0), (4.0, 13.0, 14.0), (2.6, 15.8, -18.0))   # radius, top, deg/s
CREAM = ((4, 50.0), (4, 50.0), (4, 50.0), (3, 70.0))                                 # stripes, degrees each
CAKE = add(C2, RING_R + 2.4 + TIERS[2][0], 55.0)
CAKE_TABLE_R, TOPPER = 8.6, (1.1, 17.6)

# 6 the cake knife and the royal balcony
BALCONY_Y = 15.4
KNIFE = (add(CAKE, 2.2, 50.0), add(CAKE, 13.5, 50.0))
BALCONY = (23.5, 36.5, 87.5, 102.0)
EXIT = (30.0, BALCONY_Y, 102.0)

# the soup cauldron under the leaps: the one place a fall is the end
SOUP, SOUP_R, SOUP_Y = (2.6, 68.6), 8.6, 1.2

CHECKPOINTS = (("입구", (0.0, KITCHEN, 6.0)), ("식탁 남쪽 끝", (0.0, TABLE, 19.5)), ("식탁 북쪽 끝", (0.0, TABLE, 51.5)),
               ("촛대 받침 위", (0.0, DRUM_TOP, 58.5)), ("왕의 발코니", (27.0, BALCONY_Y, 92.5)))

# ---------------------------------------------------------------- the way (x, y, z, what)
K = CANDLES[1]
foot = toward(K, DRUM, CANDLE_R + 0.3)
c1_in, c1_out = toward(C1, K, RING_R), toward(C1, C2, RING_R)
c2_in, c2_out = toward(C2, C1, RING_R), toward(C2, CAKE, RING_R)
t3_wall, t4_wall = toward(CAKE, C2, TIERS[2][0]), toward(CAKE, C2, TIERS[3][0])
WAY = [
    (0.0, KITCHEN, 0.0, "입구"),
    (0.0, KITCHEN, TABLE_Z0 - 0.4, "식탁보 아래"),
    (0.0, TABLE, TABLE_Z0 + 0.4, "식탁 위"),
    (0.0, TABLE, TABLE_Z1, "식탁 북쪽 끝"),
    (0.0, TABLE, DRUM[1] - DRUM_R - 0.3, "받침 아래"),
    (0.0, DRUM_TOP, DRUM[1] - DRUM_R + 0.5, "받침 위"),
    (foot[0], DRUM_TOP, foot[1], "초 아래"),
    (K[0], CANDLE_TOP, K[1], "초 위"),
    (c1_in[0], C1_TOP, c1_in[1], "샹들리에 1"),
    (c1_out[0], C1_TOP, c1_out[1], "샹들리에 1 끝"),
    (c2_in[0], C2_TOP, c2_in[1], "샹들리에 2"),
    (c2_out[0], C2_TOP, c2_out[1], "샹들리에 2 끝"),
    (t3_wall[0], TIERS[2][1], t3_wall[1], "케이크 3층"),
    (t4_wall[0], TIERS[2][1], t4_wall[1], "4층 아래"),
    (t4_wall[0], TIERS[3][1], t4_wall[1], "4층 위"),
    (KNIFE[0][0], TIERS[3][1], KNIFE[0][1], "칼 다리"),
    (KNIFE[1][0], BALCONY_Y, KNIFE[1][1], "발코니"),
    (EXIT[0], BALCONY_Y, EXIT[2], "출구"),
]


def way_length():
    total = level = 0.0
    for a, b in zip(WAY, WAY[1:]):
        flat = math.hypot(b[0] - a[0], b[2] - a[2])
        level += flat
        total += math.hypot(flat, b[1] - a[1])
    return total, level


# Every pitch a climber has to make, and what it costs: metres up, then seconds of stamina for a dodge or a wait
# on the move. A pawn's stamina is 8 s of moving on a wall; a rook climbs at 0.7 of the speed for the same cost.
PITCHES = (
    ("식탁보(가만히 있는 줄로)", TABLE - 0.0, 0.0),
    ("식탁보(올라가는 줄에 매달려)", 0.0, 0.0),
    ("촛대 받침", DRUM_TOP - TABLE, 0.0),
    ("초(촛농 한 번 피함)", CANDLE_TOP - DRUM_TOP, 0.6),
    ("샹들리에 1의 치마(뛰어 붙어서)", 1.4, 0.0),
    ("케이크 3층(뛰어 붙어서)", 1.4, 0.0),
    ("케이크 3층(2층 턱에서 처음부터)", TIERS[2][1] - TIERS[1][1], 0.0),
    ("케이크 4층", TIERS[3][1] - TIERS[2][1], 0.0),
)


def cost(metres, extra):
    pawn = (metres / CLIMB_UP + extra) / STAMINA
    rook = (metres / (CLIMB_UP * ROOK) + extra / ROOK) / STAMINA
    return pawn, rook


def report():
    total, level = way_length()
    print("the way: %.0f m (%.0f m level)" % (total, level))
    print("drum (%.1f, %.1f)  candles %s" % (DRUM[0], DRUM[1], ", ".join("(%.1f, %.1f)" % c for c in CANDLES)))
    print("chandelier 1 (%.1f, %.1f) top %.1f   chandelier 2 (%.1f, %.1f) top %.1f" % (C1[0], C1[1], C1_TOP, C2[0], C2[1], C2_TOP))
    print("cake (%.1f, %.1f)  knife (%.1f, %.1f) -> (%.1f, %.1f)" % (CAKE[0], CAKE[1], KNIFE[0][0], KNIFE[0][1], KNIFE[1][0], KNIFE[1][1]))
    print("gaps: candle -> chandelier 1 %s   1 -> 2 %.2f   2 -> tier 3 wall %.2f   2 -> tier 2 rim %.2f" % (
        " ".join("%.2f" % (dist(c, C1) - CANDLE_R - RING_R) for c in CANDLES), dist(C1, C2) - RING_R * 2,
        dist(C2, CAKE) - RING_R - TIERS[2][0], dist(C2, CAKE) - RING_R - TIERS[1][0]))
    print("candle spacing %.2f (dishes %.1f across)   arms from the drum %s" % (
        dist(CANDLES[0], CANDLES[1]), DISH_R * 2, " ".join("%.2f" % (dist(c, DRUM) - DRUM_R - DISH_R) for c in CANDLES)))
    print("soup: candles %s   chandelier 1 %.1f   chandelier 2 %.1f from its middle (r %.1f); cake table to soup %.2f" % (
        " ".join("%.1f" % dist(c, SOUP) for c in CANDLES), dist(C1, SOUP), dist(C2, SOUP), SOUP_R, dist(CAKE, SOUP) - CAKE_TABLE_R - SOUP_R))
    for name, metres, extra in PITCHES:
        pawn, rook = cost(metres, extra)
        print("  %-34s %.1f m  pawn %3.0f%%  rook %3.0f%%" % (name, metres, pawn * 100, rook * 100))
    for i, (r, top, speed) in enumerate(TIERS):
        print("  tier %d: r %.1f top %.1f  %+.0f deg/s  rim %.2f m/s  cream %d x %.0f deg" % (i + 1, r, top, speed, abs(math.radians(speed)) * r, CREAM[i][0], CREAM[i][1]))


# ---------------------------------------------------------------- the drawing
INK, PAPER, ORANGE, PURPLE, OLIVE, GOLD, CREAMC, WALNUT, RED, SLATE, DARK = (
    "#2b2622", "#fbf6ea", "#e8853a", "#8a5cc2", "#6f8a4b", "#c9972e", "#f3e6c8", "#8a5a36", "#c8472f", "#8fa0ad", "#3a3531")


def draw(path):
    out = []

    def w(s):
        out.append(s)

    def text(x, y, s, size=12, anchor="start", weight="normal", fill=INK):
        w('<text x="%.1f" y="%.1f" font-size="%d" text-anchor="%s" font-weight="%s" fill="%s">%s</text>' % (x, y, size, anchor, weight, fill, s))

    def plan(X0, Y0, S, win, name):
        # One view from above: `win` (x0, x1, z0, z1) drawn at S pixels a metre with its corner at (X0, Y0).
        def px(x):
            return X0 + (x - win[0]) * S

        def pz(z):
            return Y0 + (win[3] - z) * S

        def circle(c, r, fill, stroke=INK, width=1.0, extra=""):
            w('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (px(c[0]), pz(c[1]), r * S, fill, stroke, width, extra))

        def rect(x0, x1, z0, z1, fill, stroke=INK, width=1.0, extra=""):
            w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (
                px(x0), pz(z1), (x1 - x0) * S, (z1 - z0) * S, fill, stroke, width, extra))

        def line(a, b, stroke=INK, width=1.0, extra=""):
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="%.1f" %s/>' % (px(a[0]), pz(a[1]), px(b[0]), pz(b[1]), stroke, width, extra))

        def stripes(c, r, count, degrees, turn, width):
            circle(c, r, "none", SLATE, width)
            for i in range(count):
                a0 = turn + i * 360.0 / count
                p0, p1 = add(c, r, a0), add(c, r, a0 + degrees)
                w('<path d="M %.1f %.1f A %.1f %.1f 0 0 1 %.1f %.1f" fill="none" stroke="%s" stroke-width="%.1f"/>' % (
                    px(p0[0]), pz(p0[1]), r * S, r * S, px(p1[0]), pz(p1[1]), ORANGE, width + 0.4))

        fat = S / 6.0
        w('<clipPath id="%s"><rect x="%.1f" y="%.1f" width="%.1f" height="%.1f"/></clipPath>' % (name, X0, Y0, (win[1] - win[0]) * S, (win[3] - win[2]) * S))
        w('<g clip-path="url(#%s)">' % name)
        rect(HALL[0], HALL[1], HALL[2], HALL[3], "#e9e2d2", "none", 0)
        circle(SOUP, SOUP_R, "#e7a48f", RED, 1.5)
        # the long table, its runner, the cloth lanes, the goblets
        rect(-TABLE_X, TABLE_X, TABLE_Z0, TABLE_Z1, CREAMC, WALNUT, 2.0)
        rect(-RUNNER, RUNNER, TABLE_Z0 + 2, TABLE_Z1, OLIVE, "none", 0, 'opacity="0.75"')
        for i in range(CLOTH_LANES):
            x0 = -TABLE_X + i * LANE_WIDTH
            rect(x0, x0 + LANE_WIDTH, TABLE_Z0 - 1.0, TABLE_Z0, PURPLE if i % 3 != 1 else "#b9a2dc", INK, 0.8)
        for z in (21.0, 29.0, 37.0, 45.0):
            rect(TABLE_X, TABLE_X + 0.8, z, z + 2.0, ORANGE, INK, 0.6)
            rect(-TABLE_X - 0.8, -TABLE_X, z, z + 2.0, ORANGE, INK, 0.6)
        for z in GOBLETS:
            for s in (-1, 1):
                rect(4.9 if s > 0 else -TABLE_X, TABLE_X if s > 0 else -4.9, z - 0.5, z + 0.5, SLATE, "none", 0)
                circle((s * 4.0, z), 1.3, "#d9c6ea", PURPLE, 1.4)
                line((s * 2.9, z), (s * 0.4, z), PURPLE, 1.2, 'stroke-dasharray="3 2"')
        # the candelabra
        circle(DRUM, DRUM_R + 1.0, CREAMC, WALNUT, 1.5)
        circle(DRUM, DRUM_R, "#f0d9a8", ORANGE, 3.0 * fat)
        for c in CANDLES:
            line(toward(DRUM, c, DRUM_R), toward(c, DRUM, DISH_R), GOLD, 5.0 * fat)
            circle(c, DISH_R, "#f0d9a8", GOLD, 1.5)
            circle(c, CANDLE_R, "#fff6dd", ORANGE, 3.0 * fat)
        # the cake on its table
        circle(CAKE, CAKE_TABLE_R, CREAMC, WALNUT, 1.5)
        shades = ("#f9d9d2", "#fbe6df", "#fdf1ec", "#fffaf6")
        for i, (r, top, speed) in enumerate(TIERS):
            circle(CAKE, r, shades[i], "none", 0)
            stripes(CAKE, r, CREAM[i][0], CREAM[i][1], 20.0 * i, 3.0 * fat)
        circle(CAKE, TOPPER[0], GOLD, INK, 0.8)
        # the chandeliers, over all of that
        for c in (C1, C2):
            circle(c, RING_R, "#f7e3a6", ORANGE, 3.0 * fat, 'fill-opacity="0.92"')
            circle(c, 0.5, GOLD, INK, 0.8)
        # the knife and the balcony
        line(KNIFE[0], KNIFE[1], "#b9c2c9", 11.0 * fat)
        line(KNIFE[0], KNIFE[1], INK, 0.8)
        rect(BALCONY[0], BALCONY[1], BALCONY[2], BALCONY[3], "#efe2bf", WALNUT, 2.0)
        # the way and its checkpoints
        w('<polyline points="%s" fill="none" stroke="%s" stroke-width="1.6" stroke-dasharray="6 4"/>' % (" ".join("%.1f,%.1f" % (px(p[0]), pz(p[2])) for p in WAY), DARK))
        for cp_name, at in CHECKPOINTS:
            w('<path d="M %.1f %.1f v -14 l 10 4.5 l -10 4.5" fill="#3f8f5a" stroke="%s" stroke-width="1"/>' % (px(at[0]) + 2.4 * S, pz(at[2]) + 5, INK))
        w('</g>')
        w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" stroke="%s" stroke-width="1.5"/>' % (X0, Y0, (win[1] - win[0]) * S, (win[3] - win[2]) * S, INK))
        return px, pz

    def note(px, pz, at, to, lines, anchor="start", fill=INK):
        # A label at `to` (world x, z) with a thin line to what it names at `at`.
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="0.8"/>' % (px(at[0]), pz(at[1]), px(to[0]), pz(to[1]) - 4, "#6b6258"))
        w('<circle cx="%.1f" cy="%.1f" r="2" fill="#6b6258"/>' % (px(at[0]), pz(at[1])))
        for i, s in enumerate(lines):
            text(px(to[0]) + (4 if anchor == "start" else -4), pz(to[1]) + i * 14, s, 12 if i == 0 else 11, anchor, "bold" if i == 0 else "normal", fill if i == 0 else "#4a433c")

    w('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1180 1380" font-family="Malgun Gothic, Apple SD Gothic Neo, sans-serif">')
    w('<rect width="1180" height="1380" fill="%s"/>' % PAPER)
    text(34, 34, "대연회장 레벨 디자인 초안 v0.1 — 부엌에서 왕의 식탁까지, 한 접시가 가는 길", 19, "start", "bold")
    text(34, 56, "위에서 본 배치(전체와 북쪽 확대)와 길을 펴서 본 높이. 주황 = 타는 면, 보라 = 움직이는 것, 청회색 = 못 잡는 면. 단위 m.", 12)

    # the whole hall
    text(34, 86, "전체", 13, "start", "bold")
    px, pz = plan(34.0, 96.0, 6.0, HALL, "whole")
    ZOOM = (-9.0, 38.0, 53.0, 102.0)
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" stroke="%s" stroke-width="1.2" stroke-dasharray="5 4"/>' % (
        px(ZOOM[0]), pz(ZOOM[3]) + 2, (ZOOM[1] - ZOOM[0]) * 6.0 - 2, (ZOOM[3] - ZOOM[2]) * 6.0 - 2, "#6b6258"))
    w('<path d="M %.1f %.1f l -6 12 l 12 0 z" fill="%s"/>' % (px(0), pz(0) - 12, DARK))
    text(px(0) + 12, pz(0) - 6, "입구 · 부엌 통로 0 m", 11)
    note(px, pz, (3.0, 15.5), (9.0, 12.0), ["① 흐르는 식탁보", "여섯 줄이 위로 감겼다", "아래로 미끄러진다"], "start", PURPLE)
    note(px, pz, (2.5, 37.0), (9.0, 39.0), ["② 건배하는 식탁 · 4.4", "잔 네 쌍이 차례로 부딪친다", "그때마다 러너가 뒤로 당겨진다"], "start", PURPLE)
    note(px, pz, (6.8, 30.0), (9.0, 26.0), ["옆 식탁보", "떨어지면 다시 오르는 곳"], "start", ORANGE)
    text(px(10.0), pz(47.5), "북쪽은 오른쪽에 크게 →", 11, "start", "normal", "#6b6258")
    text(px(-15.0), pz(4.0), "부엌 바닥 0 m", 11, "start", "normal", "#6b6258")
    text(px(-15.0), pz(4.0) + 13, "(떨어져도 산다)", 10, "start", "normal", "#6b6258")

    # the north half, larger
    text(420, 86, "북쪽 확대 — 촛대 · 샹들리에 · 케이크 · 발코니", 13, "start", "bold")
    zx, zz = plan(420.0, 96.0, 11.6, ZOOM, "north")
    note(zx, zz, (DRUM[0] + DRUM_R, DRUM[1] - 1.0), (12.0, 56.5), ["③ 촛대 받침 · 위 7.4", "기둥 3.0 m를 오른다 (체크포인트)"])
    note(zx, zz, (CANDLES[2][0] + 1.0, CANDLES[2][1]), (13.5, 62.5), ["초 셋 · 위 10.6", "촛농이 흘러내린다 — 옆으로 피하며 3.2 m"])
    note(zx, zz, (C1[0] + RING_R, C1[1]), (15.5, 68.0), ["④ 샹들리에 1 · 위 13.2", "초 위에서 치마에 뛰어 붙는다", "앞뒤로 ±0.9 m 흔들린다"])
    note(zx, zz, (C2[0] - RING_R, C2[1] + 0.6), (-8.2, 82.5), ["샹들리에 2 · 위 10.4", "1에서 2.8 m 뛰어내린다"])
    note(zx, zz, (SOUP[0] - 6.0, SOUP[1] + 2.0), (-8.2, 74.5), ["수프 솥", "빠지면 끝"], "start", RED)
    note(zx, zz, (CAKE[0] + TIERS[0][0], CAKE[1] - 1.5), (24.0, 78.5), ["⑤ 케이크 4단", "층 위: 7.4 · 10.2 · 13.0 · 15.8", "층마다 반대로 돈다", "주황 = 크림(잡힌다)", "청회색 = 퐁당(못 잡는다)"])
    note(zx, zz, (CAKE[0] + 6.5, CAKE[1] - 5.2), (24.0, 72.0), ["케이크 식탁 · 4.4", "떨어지면 여기서 다시 오른다"])
    note(zx, zz, ((KNIFE[0][0] + KNIFE[1][0]) / 2, (KNIFE[0][1] + KNIFE[1][1]) / 2 + 0.5), (5.5, 95.0), ["⑥ 케이크 칼", "칼등을 걸어 발코니로"])
    text(zx((BALCONY[0] + BALCONY[1]) / 2), zz(96.5), "왕의 발코니 · 15.4", 12, "middle", "bold")
    w('<path d="M %.1f %.1f l -6 12 l 12 0 z" fill="%s"/>' % (zx(EXIT[0]), zz(EXIT[2]) - 16, DARK))
    text(zx(EXIT[0]) + 12, zz(EXIT[2]) - 22, "출구", 11)
    text(zx(-8.2), zz(54.5), "긴 식탁의 북쪽 끝 · 4.4", 11, "start", "normal", WALNUT)

    # the height profile, the way laid out straight
    EX, EY, ES, EH = 80.0, 1004.0, 8.6, 13.0
    s, prof = 0.0, []
    for i, p in enumerate(WAY):
        if i:
            s += math.hypot(p[0] - WAY[i - 1][0], p[2] - WAY[i - 1][2])
        prof.append((EX + s * ES, EY - p[1] * EH, p))
    text(34, 752, "길을 펴서 본 높이 — 가로는 걸어간 거리, 세로는 높이. 굵은 주황 = 벽을 타는 곳, 보라 점선 = 뛰는 곳", 13, "start", "bold")
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (EX - 14, EY - 18.2 * EH, s * ES + 44, 18.2 * EH + 16, INK))
    for h in (0, 4.4, 7.4, 10.6, 13.2, 15.8):
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#cfc6b3" stroke-width="0.8"/>' % (EX - 14, EY - h * EH, EX + s * ES + 30, EY - h * EH))
        text(EX - 20, EY - h * EH + 4, "%.1f" % h, 10, "end")
    climbs, leaps = (2, 5, 7, 14), (8, 10, 12)
    for i in range(1, len(prof)):
        (x0, y0, _), (x1, y1, _) = prof[i - 1], prof[i]
        if i in climbs:
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="5"/>' % (x0, y0, x1, y1, ORANGE))
        elif i in leaps:
            w('<path d="M %.1f %.1f Q %.1f %.1f %.1f %.1f" fill="none" stroke="%s" stroke-width="2" stroke-dasharray="5 3"/>' % (
                x0, y0, (x0 + x1) / 2, min(y0, y1) - 18, x1, y1, PURPLE))
        else:
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="2.4"/>' % (x0, y0, x1, y1, DARK))
    for i, label, sub, dx, dy in ((1, "① 흐르는 식탁보", "+4.4 m", 10, -22), (2, "② 건배하는 식탁", "쉬는 구간 38 m", 60, -12), (5, "③ 촛대", "+3.0, +3.2 m", -84, -34),
                                  (8, "④ 샹들리에", "뛰어 붙기 +2.6 · 뛰어내림 −2.8", -40, -26), (13, "⑤ 케이크", "뛰어 붙기 +2.6 · 4층 +2.8", -30, 48),
                                  (15, "⑥ 칼 다리 · 발코니", "출구 15.4 m", 10, -16)):
        text(prof[i][0] + dx, prof[i][1] + dy, label, 12, "start", "bold")
        text(prof[i][0] + dx, prof[i][1] + dy + 14, sub, 11, "start", "normal", "#4a433c")
    total, level = way_length()
    text(EX + s * ES + 30, EY + 34, "길 약 %.0f m · 오르는 높이를 다 더하면 %.0f m" % (total, sum(max(0.0, b[1] - a[1]) for a, b in zip(WAY, WAY[1:]))), 11, "end")

    # the beats
    rows = (
        ("①", "흐르는 식탁보", "식탁보 여섯 줄이 위로 감겼다 아래로 미끄러진다. 올라가는 줄에 매달리면 공짜로 오른다", "0 → 4.4"),
        ("②", "건배하는 식탁", "잔 네 쌍이 차례로 '챙' 하고 부딪치고, 그때마다 러너가 뒤로 당겨진다. 벽타기 사이의 쉬는 구간", "4.4"),
        ("③", "큰 촛대", "받침 기둥을 오르고, 촛농이 흘러내리는 초 셋 가운데 하나를 옆으로 피하며 오른다", "→ 7.4 → 10.6"),
        ("④", "샹들리에", "흔들리는 샹들리에의 크리스털 치마에 뛰어 붙어 오르고, 다음 샹들리에로 뛰어내린다", "→ 13.2 → 10.4"),
        ("⑤", "케이크", "층마다 반대로 도는 케이크. 크림 줄만 잡힌다 — 뛰어 붙거나, 줄이 돌아올 때를 기다려 오른다", "→ 13.0 → 15.8"),
        ("⑥", "케이크 칼과 발코니", "꼭대기 층에 걸친 칼등을 걸어 왕의 발코니로, 출구", "15.4"),
    )
    ty = 1076.0
    text(34, ty, "구간", 12, "start", "bold")
    text(1146, ty, "높이 (m)", 12, "end", "bold")
    for n, name, what, height in rows:
        ty += 27
        w('<line x1="34" y1="%.1f" x2="1146" y2="%.1f" stroke="#d8cfbc" stroke-width="0.8"/>' % (ty - 18, ty - 18))
        text(34, ty, n, 14, "start", "bold")
        text(60, ty, name, 13, "start", "bold")
        text(214, ty, what, 12)
        text(1146, ty, height, 12, "end")
    ty += 36
    text(34, ty, "벽타기가 쓰이는 모양 넷 — 실려 가기(①) · 피하며 오르기(③) · 뛰어 붙기(④ ⑤) · 도는 벽 읽기(⑤)", 13, "start", "bold")
    ty += 21
    text(34, ty, "떨어지면 대개 죽지 않는다: 부엌 바닥에 떨어져 식탁보를 타고 다시 올라온다. 끝나는 곳은 수프 솥 하나뿐이다.", 12)
    ty += 19
    text(34, ty, "한 번에 오르는 높이는 폰 스테미나의 46% 이하, 룩의 65% 이하. 매달려 기다리는 동안은 스테미나가 줄지 않는다.", 12)
    lx, ly = 34.0, ty + 34
    for colour, label in ((ORANGE, "타는 면"), (PURPLE, "움직이는 것"), (SLATE, "못 잡는 면"), ("#e7a48f", "수프(끝)"), ("#3f8f5a", "체크포인트")):
        w('<rect x="%.1f" y="%.1f" width="18" height="11" fill="%s" stroke="%s" stroke-width="0.6"/>' % (lx, ly - 10, colour, INK))
        text(lx + 25, ly, label, 12)
        lx += 44 + len(label) * 13
    w('</svg>')
    open(path, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")


if __name__ == "__main__":
    report()
    if len(sys.argv) > 1:
        draw(sys.argv[1])
        print("drawn", sys.argv[1])
