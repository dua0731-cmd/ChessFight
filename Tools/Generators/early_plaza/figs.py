# -*- coding: utf-8 -*-
# Figures for the early mini-game section sheet. Section coordinates: origin = middle of the entry threshold
# (the obstacle section's exit), +z north, +x east, y = plaza floor. White's station is west, black's east.
import math
from geom import Fig, Plan, Section, View, text, n
from html import escape as esc

TEAM_X = 12.0          # station centres x = -12 (white) / +12 (black)
EXIT_Z = 32.0          # exit threshold (to the 2-x section)
NORTH0, NORTH1 = 20.0, 26.0   # facade / north block
VEST1 = 32.0


# ---------------------------------------------------------------- figure 1: the seven slots

def fig_slots():
    W, H = 960, 196
    f = Fig("slots", W, H, "한 판 7구간 중 슬롯 2의 자리. 앞은 초반 장애물 다섯 곳 중 하나, 뒤는 중반 장애물 다섯 곳 중 하나다.")
    arw = f.marker()
    slots = [("1", "장애물 · 초반", "1-1 ~ 1-5", "s-obst"),
             ("2", "미니게임 · 초반", "C · D · A", "s-focus"),
             ("3", "장애물 · 중반", "2-1 ~ 2-5", "s-obst"),
             ("4", "미니게임 · 중반", "B · E · F · J", "s-mg"),
             ("5", "장애물 · 후반", "3-1 ~ 3-5", "s-obst"),
             ("6", "미니게임 · 후반", "G · H · I", "s-mg"),
             ("7", "마지막", "왕관 결승", "s-mg")]
    bw, gap, y, bh = 118, 16, 58, 76
    x0 = (W - (7 * bw + 6 * gap)) / 2
    for i, (num, kind, opts, cls) in enumerate(slots):
        x = x0 + i * (bw + gap)
        f.add(f'<rect x="{n(x)}" y="{y}" width="{bw}" height="{bh}" rx="3" class="{cls}"/>')
        text(f, x + 10, y + 18, "슬롯 " + num, "sm mono", "start")
        text(f, x + 10, y + 42, kind, "lb", "start", weight=600)
        text(f, x + 10, y + 62, opts, "sm", "start")
        if i < 6:
            ax = x + bw
            f.add(f'<line x1="{n(ax + 2)}" y1="{y + bh / 2}" x2="{n(ax + gap - 2)}" y2="{y + bh / 2}" '
                  f'class="s-ink" marker-end="url(#{arw})"/>')
    for i, lab in ((0, "이음매 ①"), (1, "이음매 ②")):
        gx = x0 + (i + 1) * (bw + gap) - gap / 2
        f.add(f'<line x1="{n(gx)}" y1="30" x2="{n(gx)}" y2="{y - 4}" class="s-acc"/>')
        text(f, gx, 24, lab, "acc", weight=600)
    for i, lab in ((0, "바깥뜰을 지나"), (1, "정문을 열고"), (2, "현관으로 들어간다")):
        text(f, x0 + i * (bw + gap) + bw / 2, y + bh + 26, lab, "sm")
    f.add(f'<line x1="{n(x0 + 8)}" y1="{y + bh + 10}" x2="{n(x0 + 3 * bw + 2 * gap - 8)}" y2="{y + bh + 10}" '
          f'class="s-faint" marker-end="url(#{arw})"/>')
    return f.svg()


# ---------------------------------------------------------------- figure 2: the joint, cut along x = 0

def fig_section():
    zmin, zmax, ymin, ymax, s = -32.0, 42.0, -2.5, 17.0, 12.0
    ox, oy = 36, 92
    W = int(ox * 2 + (zmax - zmin) * s)
    H = int(oy + (ymax - ymin) * s + 66)
    f = Fig("sec", W, H, "이음매 단면(x = 0). 장애물 구간이 테라스·아치·4단·입구 띠까지 만들고, (0, 0, 0)부터 미니게임 구간이 광장·파사드·문 통로·현관 회랑을 거쳐 (0, 0, 32)에서 다음 구간으로 넘긴다.")
    arw = f.marker()
    S = Section(f, zmin, ymax, s, ox, oy)

    # ownership bars
    def bar(z0, z1, row, cls, label, tcls="lb"):
        a, _ = S.P(z0, 0)
        b, _ = S.P(z1, 0)
        y = 14 + row * 24
        f.add(f'<rect x="{n(a)}" y="{y}" width="{n(b - a)}" height="18" rx="2" class="{cls}"/>')
        text(f, a + 8, y + 13, label, tcls, "start", size=11.5)
    bar(zmin, 0, 0, "own-obst", "장애물 구간 프리팹 · 1-1 ~ 1-5 중 하나 (테라스·아치·4단·띠까지)")
    bar(0, EXIT_Z, 0, "own-mg", "미니게임 구간 프리팹 · C · D · A 공통 껍데기", "lb-on-accent")
    bar(-6.4, 0, 1, "own-mg-soft", "앞뜰 담장·잔디", "sm")
    bar(EXIT_Z, zmax, 0, "own-obst", "다음 구간 · 2-x")

    # background silhouettes (not on the cut line)
    S.rect(-6.4, -5.6, 0, 7, "f-bg")                       # forecourt wall either side of the arch
    S.rect(0, 20, 0, 0.4, "f-dais-bg")                     # the stations' dais beside the aisle
    S.rect(NORTH0, NORTH1, 0, 12, "f-bg")                  # facade beyond the gatehouse
    S.rect(NORTH1, VEST1, 0, 6.6, "f-bg")                  # vestibule side walls
    S.rect(EXIT_Z, EXIT_Z + 1.0, 0, 7.5, "f-bg")           # next section's porch arch
    S.rect(EXIT_Z + 1.0, zmax, 0, 9.0, "f-bg2")
    S.text(EXIT_Z + 5, 10.4, "2-1 현관 아치 (예)", "sm")

    # cut: obstacle side
    S.poly([(zmin, ymin), (zmin, 1), (-6, 1), (-6, 0.75), (-5.5, 0.75), (-5.5, 0.5), (-5, 0.5), (-5, 0.25),
            (-4.5, 0.25), (-4.5, 0), (zmax, 0), (zmax, ymin)], "f-cut")
    S.rect(-6.4, -5.6, 6, 7, "f-cut")                      # arch lintel
    # cut: mini-game side
    S.poly([(NORTH0 - 0.6, 0), (NORTH0 - 0.6, 15.2), (NORTH0 - 0.6 + 0.8, 15.2), (NORTH0 - 0.6 + 0.8, 16),
            (NORTH0 + 1.4, 16), (NORTH0 + 1.4, 15.2), (NORTH0 + 2.6, 15.2), (NORTH0 + 2.6, 16), (NORTH0 + 3.8, 16),
            (NORTH0 + 3.8, 15.2), (NORTH1, 15.2), (NORTH1, 0)], "f-cut")
    S.rect(NORTH0 - 0.6, NORTH0 - 0.1, 0, 7, "f-walnut")   # the closed main gate
    S.rect(NORTH1, VEST1, 6, 6.7, "f-cut")                 # vestibule roof

    # the last device
    S.rect(-29.4, -29.0, 1, 2.0, "f-ink")
    S.line(-31.4, 1.6, -27.0, 1.6, "s-ink thick")
    S.text(-29.2, 3.4, "마지막 장치", "sm")

    # a pawn on the terrace and the camera's two rays to the facade
    S.rect(-9.25, -8.75, 1.0, 2.0, "f-ink", 'rx="3"')
    ex, ey = -9.0, 5.0
    S.line(ex, ey, NORTH0 - 0.6, 0.0, "ray")
    S.line(ex, ey, NORTH0 - 0.6, 16.0, "ray")
    a, b = S.P(ex, ey)
    f.add(f'<circle cx="{n(a)}" cy="{n(b)}" r="4" class="f-accent"/>')
    S.text(-12.6, 6.3, "테라스 카메라", "acc", size=11.5, weight=600)
    S.text(-6.0, 12.6, "아치 밑으로 파사드 0 ~ 16 m가 다 보인다", "sm", "start")

    # height labels
    S.text(-11.6, 1.35, "+1.0 테라스 · CP", "sm", "end")
    S.text(-3.2, 1.4, "0.25 × 4단", "sm", "start")
    S.text(1.0, -1.4, "+0.4 아레나 단은 양옆 스테이션", "sm-inv", "start")
    S.text(NORTH0 - 1.2, 12.6, "12 파사드", "sm", "end")
    S.text(NORTH0 - 1.2, 16.4, "16 누각", "sm", "end")
    S.text(NORTH0 - 1.2, 7.4, "7 닫힌 정문", "sm", "end")
    S.text(NORTH0 + 3.0, 2.6, "누각", "sm-inv")
    S.text(NORTH0 + 3.0, 0.7, "정문 뒤는 막힘", "sm-inv")
    S.text((NORTH1 + VEST1) / 2, 3.0, "현관 회랑", "sm")
    S.text((NORTH1 + VEST1) / 2, 1.7, "CP1", "sm mono")
    S.text(10.0, 3.0, "가운데 통로 (x ±2)", "sm")
    S.text(-6.0, 7.6, "아치 12 × 5", "sm")

    # joints
    for z, lab in ((0.0, "이음매 ① (0, 0, 0)"), (EXIT_Z, "이음매 ② (0, 0, 32)")):
        a, b = S.P(z, ymin)
        f.add(f'<line x1="{n(a)}" y1="62" x2="{n(a)}" y2="{n(b)}" class="s-acc dash"/>')
        text(f, a + 5, 76, lab, "acc", "start", size=11.5, weight=600)

    # dimension line under the ground
    dims = [(-27.0, -12.0, "정리 동선 12–18"), (-12.0, -6.0, "테라스 6"), (-6.0, -4.0, "4단"), (-4.0, 0.0, "띠 4"),
            (0.0, 20.0, "광장 20"), (NORTH0, NORTH1, "문 통로 6"), (NORTH1, VEST1, "회랑 6")]
    yd = oy + (ymax - ymin) * s + 16
    for z0, z1, lab in dims:
        a, _ = S.P(z0, 0)
        b, _ = S.P(z1, 0)
        f.add(f'<line x1="{n(a)}" y1="{n(yd)}" x2="{n(b)}" y2="{n(yd)}" class="s-ink thin"/>')
        for xx in (a, b):
            f.add(f'<line x1="{n(xx)}" y1="{n(yd - 4)}" x2="{n(xx)}" y2="{n(yd + 4)}" class="s-ink thin"/>')
        text(f, (a + b) / 2, yd + 17, lab, "sm")
    text(f, W - ox, yd + 40, "단위 m · 가로세로 같은 축척", "sm", "end")
    return f.svg()


# ---------------------------------------------------------------- shared pieces of the plan drawings

def forecourt(pl, props=True):
    # the obstacle section's end (dashed = not ours)
    pl.rect(-6, 6, -12, -6, "obst")
    for i in range(4):
        pl.rect(-6, 6, -6 + i * 0.5, -5.5 + i * 0.5, "obst")
    pl.rect(-6, 6, -4, 0, "obst")
    pl.rect(-6.3, -6, -5.6, 0, "f-cut")
    pl.rect(6, 6.3, -5.6, 0, "f-cut")
    pl.rect(-7, -6, -6.4, -5.6, "f-cut")
    pl.rect(6, 7, -6.4, -5.6, "f-cut")
    # ours: the forecourt wall, the lawns, the cypresses
    pl.rect(-23, -7, -6.4, -5.6, "f-cut")
    pl.rect(7, 23, -6.4, -5.6, "f-cut")
    pl.rect(-22, -6.3, -5.6, 0, "f-lawn")
    pl.rect(6.3, 22, -5.6, 0, "f-lawn")
    for x in (-21, -17.5, 17.5, 21):
        pl.circle(x, -4.2, 0.75, "f-cypress")
    pl.rect(-23, -22, -5.6, NORTH0, "f-cut")
    pl.rect(22, 23, -5.6, NORTH0, "f-cut")


def north_wall(pl, z1, door_cls="f-door"):
    for x0, x1 in ((-23, -14), (-10, -5), (5, 10), (14, 23)):
        pl.rect(x0, x1, NORTH0, z1, "f-cut")
    pl.rect(-5, 5, NORTH0 - 0.6, z1, "f-cut2")             # gatehouse
    pl.rect(-3, 3, NORTH0 - 0.6, NORTH0 - 0.1, "f-walnut")  # closed main gate
    for side in (-1, 1):
        x0, x1 = sorted((side * 10, side * 14))
        pl.rect(x0, x1, NORTH0, z1, "f-pave")
        pl.rect(x0, x1, NORTH0, NORTH0 + 0.4, door_cls)
        pl.rect(x0, x1, NORTH0 + 0.6, NORTH0 + 0.8, "f-none dash-ink")


def promo_pads(pl):
    for side in (-1, 1):
        x = side * (TEAM_X + 4.5)
        pl.rect(x - 1, x + 1, 0, 2, "pad")


def team_labels(pl, z=8.6, on_dark=True):
    pl.text(-19.4, z, "백", "wt-f" if on_dark else "wt-ink", size=13, weight=700)
    pl.text(19.4, z, "흑", "bt-f" if on_dark else "bt-ink", size=13, weight=700)


def scale_bar(f, x, y, s, label=True):
    f.add(f'<rect x="{n(x)}" y="{n(y)}" width="{n(5 * s)}" height="5" class="f-ink"/>'
          f'<rect x="{n(x + 5 * s)}" y="{n(y)}" width="{n(5 * s)}" height="5" class="f-none s-ink thin"/>')
    if label:
        for i, t in ((0, "0"), (5, "5"), (10, "10 m")):
            text(f, x + i * s, y + 18, t, "sm mono")


def north_arrow(f, x, y):
    f.add(f'<path d="M{n(x)} {n(y - 14)} L{n(x + 6)} {n(y + 6)} L{n(x)} {n(y + 1)} L{n(x - 6)} {n(y + 6)} z" class="f-ink"/>')
    text(f, x, y + 22, "북 +z", "sm mono")


# ---------------------------------------------------------------- figure 3: the shell plan

def fig_shell():
    s = 13.0
    xmin, xmax, zmin, zmax = -25.0, 25.0, -13.0, 35.5
    ox, oy = 18, 16
    W = int(ox * 2 + (xmax - xmin) * s)
    H = int(oy + (zmax - zmin) * s + 40)
    f = Fig("shell", W, H, "정문 앞 광장의 공통 껍데기 평면. 아치 양옆 앞뜰, 스테이션 둘과 가운데 통로, 문 셋의 파사드, 문 뒤 통로 둘이 현관 회랑에서 만나 출구 하나로 나간다.")
    arw = f.marker()
    mk = {"wt-s": f.marker("-wt", "mk-wt"), "bt-s": f.marker("-bt", "mk-bt")}
    pl = Plan(f, xmin, zmax, s, ox, oy)

    forecourt(pl)
    # the terrace camera's view through the arch (rays through the arch's inner edges)
    pl.poly([(0, -9), (-6, -6), (-22, 2), (-22, 20), (22, 20), (22, 2), (6, -6)], "cone")
    pl.line(0, -9, -22, 2, "ray")
    pl.line(0, -9, 22, 2, "ray")
    pl.circle(0, -9, 0.45, "f-accent")

    # plaza
    pl.rect(-2, 2, 0, 20, "f-pave")
    for side in (-1, 1):
        x0, x1 = sorted((side * 2, side * 22))
        pl.rect(x0, x1, 0, 20, "f-slot")
        pl.line(x0, 2.5, x1, 2.5, "s-faint dash")
        sx0, sx1 = sorted((side * (TEAM_X - 2.8), side * (TEAM_X + 2.8)))
        pl.rect(sx0, sx1, 0.1, 0.45, "f-ink")                      # the arena sign at u 0
    pl.text(-12, 10.6, "백 스테이션 20 × 20", "lb-inv")
    pl.text(-12, 8.8, "게임이 바닥을 만든다", "sm-inv")
    pl.text(12, 10.6, "흑 스테이션 20 × 20", "lb-inv")
    pl.text(12, 8.8, "게임이 바닥을 만든다", "sm-inv")
    promo_pads(pl)
    # the split marks at the end of the band
    for side, cls in ((-1, "wt-s"), (1, "bt-s")):
        pl.line(side * 2.6, 1.0, side * 6.4, 1.0, cls + " thick", f'marker-end="url(#{mk[cls]})"')

    # north block, gatehouse, doors, passages, vestibule, exit
    north_wall(pl, NORTH1)
    pl.rect(-14, 14, NORTH1, VEST1, "f-marble")
    pl.rect(-15, -14, NORTH1, VEST1 + 1, "f-cut")
    pl.rect(14, 15, NORTH1, VEST1 + 1, "f-cut")
    pl.rect(-15, -6, VEST1, VEST1 + 1, "f-cut")
    pl.rect(6, 15, VEST1, VEST1 + 1, "f-cut")
    pl.rect(-6, 6, VEST1, 35.5, "obst")
    pl.line(0, 27.4, 0, 35.0, "s-ink thick", f'marker-end="url(#{arw})"')
    for side in (-1, 1):
        pl.path([("M", side * 12, 21.0), ("L", side * 12, 28.6), ("L", side * 3.2, 28.6)], "s-ink route",
                f'marker-end="url(#{arw})"')

    # checkpoints
    for x, z, lab in ((0, 2.2, "CP0"), (0, 29.4, "CP1")):
        pl.circle(x, z, 0.5, "f-accent")
        pl.text(x + 1.0, z - 0.35, lab, "sm mono", "start")
    pl.text(0, -9.0 - 1.6, "테라스 카메라", "acc", size=11)

    # callouts
    for x, z, k in ((-4.2, -7.6, 1), (-14.6, -2.6, 2), (14.6, -2.6, 3), (-4.6, 2.8, 4), (-16.5, 3.6, 5),
                    (-12, -1.1, 6), (-12, 14.0, 7), (0, 15.0, 8), (0, 22.0, 9), (-16.2, 21.0, 10),
                    (-12, 24.2, 11), (8.0, 30.4, 12), (3.0, 34.2, 13)):
        pl.callout(x, z, k)

    # coordinates
    for x in (-22, -12, -2, 2, 12, 22):
        a, _ = pl.P(x, zmin)
        text(f, a, H - 30, str(x), "sm mono")
        f.add(f'<line x1="{n(a)}" y1="{H - 44}" x2="{n(a)}" y2="{H - 40}" class="s-faint"/>')
    for z in (-6, 0, 20, 26, 32):
        _, b = pl.P(xmin, z)
        text(f, ox + 2, b + 4, f"z {z}", "sm mono", "start")
    text(f, W / 2, H - 12, "x (m)", "sm mono")
    scale_bar(f, ox + 30, oy + 586, s)
    north_arrow(f, ox + 196, oy + 584)
    return f.svg()


# ---------------------------------------------------------------- figures 4: the three games in the same shell

GP = dict(s=14.0, xmin=-24.0, xmax=24.0, zmin=-7.0, zmax=22.5, ox=12, oy=12)


def game_plan(fid, label, draw):
    g = GP
    W = int(g["ox"] * 2 + (g["xmax"] - g["xmin"]) * g["s"])
    H = int(g["oy"] * 2 + (g["zmax"] - g["zmin"]) * g["s"] + 46)
    f = Fig(fid, W, H, label)
    arw = f.marker()
    pl = Plan(f, g["xmin"], g["zmax"], g["s"], g["ox"], g["oy"])
    forecourt(pl)
    draw(pl, arw)
    scale_bar(f, W - g["ox"] - 150, H - 34, g["s"])
    north_arrow(f, g["ox"] + 26, H - 34)
    return f.svg()


def X(side, v):
    # station-local v (across, + = towards the aisle for both teams: black is white's mirror) -> plaza x
    return side * (TEAM_X - v)


def draw_C(pl, arw):
    alt = pl.f.marker("-lt", "mk-light")
    pl.rect(-22, 22, 12, 16, "f-water")
    pl.line(-22, 12, 22, 12, "s-bank")
    pl.line(-22, 16, 22, 16, "s-bank")
    pl.rect(-2, 2, 0, 12, "f-pave")
    pl.rect(-2, 2, 16, 20, "f-pave")
    pl.rect(-2, 2, 11.7, 12, "f-cut")                         # low rail at the aisle's end
    for side in (-1, 1):
        x = lambda v: X(side, v)
        pl.rect(x(-10), x(10), 0, 12, "f-dais")
        pl.rect(x(-10), x(10), 16, 20, "f-dais")
        pl.rect(x(-1.2), x(1.2), 0.2, 10.9, "f-lane")
        for k in range(8):
            pl.line(x(-1.2), 3.2 + k, x(1.2), 3.2 + k, "s-goldfill")
        for u in (2.4, 5.0, 7.6):
            pl.line(x(-0.85), u + 0.8, x(-0.85), u - 0.6, "s-arrow", f'marker-end="url(#{alt})"')
        pl.rect(x(-1.6), x(1.6), 10.8, 11.8, "f-walnut")       # winch frame
        pl.rect(x(-1.1), x(1.1), 10.75, 11.85, "f-iron")       # drum
        pl.circle(x(1.32), 11.3, 0.34, "f-gold")                # cogs
        pl.rect(min(x(1.66), x(1.78)), max(x(1.66), x(1.78)), 10.95, 11.65, "f-gold")  # latch bar
        pl.circle(x(2.0), 11.3, 0.2, "f-gold")                  # latch lamps
        pl.line(x(0), 10.9, x(0), 6.4, "s-rope")
        for k in range(4):
            pl.circle(x(0), 10.2 - k, 0.26, "f-gold s-knot")
        pl.rect(x(-2), x(2), 11.85, 15.95, "f-none dash-light")  # where the bridge lands
        pl.rect(x(-2), x(2), 15.95, 16.3, "f-walnut")           # the bridge, standing
        for v in (-2.4, 2.4):
            pl.rect(x(v) - 0.35, x(v) + 0.35, 15.85, 16.55, "f-wall s-ink-thin")
        for v0, v1 in ((-1.7, -0.9), (1.7, 0.9)):
            pl.line(x(v0), 16.1, x(v1), 11.5, "s-chain")
    pl.rect(-3, 3, 16, 16.35, "f-walnut")                       # the fixed show bridge before the main gate
    north_wall(pl, GP["zmax"])
    promo_pads(pl)
    team_labels(pl, 8.4)
    pl.text(0, 13.75, "해자 · 청록 물 · 수면 −0.9", "sm-inv")
    # threshold set: rope coil, spare cog, life buoy, stone pot
    pl.circle(-9.6, -2.3, 0.8, "f-maple s-ink-thin")
    pl.circle(-9.6, -2.3, 0.4, "f-none s-ink-thin")
    pl.circle(-13.2, -2.3, 0.75, "f-iron")
    pl.circle(-13.2, -2.3, 0.3, "f-gold")
    pl.circle(9.6, -2.3, 0.75, "f-buoy")
    pl.circle(9.6, -2.3, 0.38, "f-lawn")
    pl.circle(13.2, -2.3, 0.65, "f-wall s-ink-thin")
    for x, z, k in ((-15.0, 5.0, 1), (-9.6, 8.7, 2), (-16.4, 11.3, 3), (19.0, 14.0, 4), (-7.4, 17.8, 5),
                    (-16.0, 17.6, 6), (-12, 21.4, 7), (0, 9.6, 8), (-11.4, -0.6, 9), (-16.5, 3.6, 10)):
        pl.callout(x, z, k)


def draw_D(pl, arw):
    abr = pl.f.marker("-br", "mk-brass")
    pl.rect(-2, 2, 0, 20, "f-pave")
    for side in (-1, 1):
        x = lambda v: X(side, v)
        pl.rect(x(-10), x(10), 0, 20, "f-dais")
        cx, cz = x(0), 10.0
        pl.circle(cx, cz, 3.6, "f-board")
        for k in range(4):
            if side < 0:
                a0, a1 = 90 * k + 28, 90 * k - 28
            else:
                a0, a1 = 90 * k - 28, 90 * k + 28
            pl.arc(cx, cz, 3.3, a0, a1, "s-brass", f'marker-end="url(#{abr})"')
        for a in (45, 135, 225, 315):
            c, sn = math.cos(math.radians(a)), math.sin(math.radians(a))
            pl.line(cx + 0.75 * c, cz + 0.75 * sn, cx + 2.7 * c, cz + 2.7 * sn, "s-bar")
            pl.line(cx + 2.7 * c, cz + 2.7 * sn, cx + 3.0 * c, cz + 3.0 * sn, "s-bartip")
        pl.circle(cx, cz, 0.62, "f-walnut")
        pl.circle(cx, cz, 0.34, "f-gold")
        pl.line(cx, cz + 0.7, cx, NORTH0 - 0.2, "s-chainhi")
    north_wall(pl, GP["zmax"], "f-portcullis")
    for side in (-1, 1):
        for i in range(1, 8):
            xx = side * (10 + i * 0.5)
            pl.line(xx, NORTH0, xx, NORTH0 + 0.4, "s-ink-thin")
    promo_pads(pl)
    team_labels(pl, 8.4)
    pl.text(-12, 4.4, "백: 위에서 시계 방향", "sm-inv")
    pl.text(12, 4.4, "흑: 거울", "sm-inv")
    # threshold set: spare bar rack, chain pile, bench with a guard helmet, stone pot
    pl.rect(-10.8, -8.4, -2.7, -1.9, "f-walnut")
    for xx in (-10.4, -9.8, -9.2, -8.6):
        pl.line(xx, -2.65, xx, -1.95, "s-bartip")
    pl.circle(-13.2, -2.3, 0.7, "f-iron")
    pl.rect(8.2, 11.0, -2.7, -1.9, "f-wall s-ink-thin")
    pl.circle(9.0, -2.3, 0.3, "f-iron")
    pl.circle(13.2, -2.3, 0.65, "f-wall s-ink-thin")
    for x, z, k in ((-7.8, 12.6, 1), (-16.8, 13.4, 2), (-16.8, 6.4, 3), (-10.5, 16.4, 4), (-12, 21.4, 5),
                    (0, 21.4, 6), (-11.4, -0.6, 7), (-16.5, 3.6, 8)):
        pl.callout(x, z, k)


PAINT_W = [(1, 0), (3, 0), (0, 1), (2, 1), (1, 2)]
PAINT_B = [(4, 0), (6, 0), (5, 1), (7, 1), (4, 2)]
CHARGE_W, CHARGE_B = (3, 2), (6, 2)


def draw_A(pl, arw):
    pl.rect(-22, 22, 0, 20, "f-dais")
    pl.rect(-9, 9, 2, 20, "f-gold")
    for fl in range(8):
        for rk in range(8):
            x0, z0 = -9 + fl * 2.25 + 0.05, 2 + rk * 2.25 + 0.05
            dark = (fl + rk) % 2 == 0
            cls = "f-board"
            if (fl, rk) in PAINT_W:
                cls = "pw"
            elif (fl, rk) in PAINT_B:
                cls = "pb"
            pl.rect(x0, x0 + 2.15, z0, z0 + 2.15, cls)
            cxm, czm = x0 + 1.075, z0 + 1.075
            if cls == "f-board":
                dot = "dot-b" if dark else "dot-w"
                pl.circle(cxm - 0.8, czm - 0.8, 0.13, dot)
                pl.circle(cxm + 0.8, czm + 0.8, 0.13, dot)
            for (cf, cr), pcls in ((CHARGE_W, "pw"), (CHARGE_B, "pb")):
                if (fl, rk) == (cf, cr):
                    pl.rect(cxm - 0.62, cxm + 0.62, czm - 0.62, czm + 0.62, pcls)
    for i, ch in enumerate("abcdefgh"):
        pl.text(-9 + i * 2.25 + 1.125, 1.05, ch, "goldtxt mono", size=10)
    for r in range(8):
        pl.text(-9.55, 2 + r * 2.25 + 1.45, str(r + 1), "goldtxt mono", size=10)
        pl.text(9.55, 2 + r * 2.25 + 1.45, str(r + 1), "goldtxt mono", size=10)
    for side in (-1, 1):
        x0, x1 = sorted((side * 20.2, side * 22))
        pl.rect(x0, x1, 3, 17, "f-wall s-ink-thin")
        for z in (4, 8, 12, 16):
            pl.circle(side * 19.4, z, 0.22, "f-ink")
    north_wall(pl, GP["zmax"])
    promo_pads(pl)
    pl.text(-12, 21.6, "0 / 32", "wt-f mono", size=11)
    pl.text(12, 21.6, "0 / 32", "bt-f mono", size=11)
    pl.text(-15.6, 8.4, "백", "wt-f", size=13, weight=700)
    pl.text(15.6, 8.4, "흑", "bt-f", size=13, weight=700)
    # threshold set: white and black paint pots, brush rack, rolled carpet, stone pot
    pl.circle(-9.2, -2.3, 0.5, "pw")
    pl.circle(-10.5, -2.3, 0.5, "pb")
    pl.rect(-14.2, -12.6, -2.9, -1.7, "f-walnut")
    pl.rect(8.2, 11.6, -2.6, -2.0, "f-carpet")
    pl.circle(13.2, -2.3, 0.65, "f-wall s-ink-thin")
    for x, z, k in ((-4.5, 11.0, 1), (-10.4, 2.0, 2), (-5.6, 10.1, 3), (9.9, 14.0, 4), (-18.2, 10.0, 5),
                    (-15.6, 21.4, 6), (0, 21.4, 7), (-11.4, -0.6, 8), (-16.5, 3.6, 9)):
        pl.callout(x, z, k)


# ---------------------------------------------------------------- figures 5: what the terrace camera sees

def view(fid, label, game):
    W, H = 360, 236
    f = Fig(fid, W, H, label, "dg view")
    clip = fid + "-clip"
    f.defs.append(f'<clipPath id="{clip}"><rect x="0" y="0" width="{W}" height="{H}"/></clipPath>')
    v = View(f, W, H, eye=(0.0, 5.0, -9.0), pitch=12.0, hfov=84.0)
    f.add(f'<g clip-path="url(#{clip})">')
    f.add(f'<rect x="0" y="0" width="{W}" height="{H}" class="f-sky"/>')
    v.ground(-22, 22, -5.6, 0, 0, "f-lawn")
    v.ground(-6, 6, -4, 0, 0.01, "f-pave2")
    v.ground(-2, 2, 0, 20, 0, "f-pave")
    # side walls' inner faces
    f.add(v.face([(-22, 0, -5.6), (-22, 0, 20), (-22, 6, 20), (-22, 6, -5.6)], "f-wall", "side"))
    f.add(v.face([(22, 0, -5.6), (22, 0, 20), (22, 6, 20), (22, 6, -5.6)], "f-wall", "side"))
    game_ground(v, game)
    facade(v, game)
    game_items(v, game)
    v.flush()
    f.add("</g>")
    return f.svg()


def facade(v, game):
    f = v.f
    f.add(v.face([(-22, 0, 20), (22, 0, 20), (22, 12, 20), (-22, 12, 20)], "f-wall"))
    for x in (-19.5, -16.5, -7.5, 7.5, 16.5, 19.5):
        f.add(v.face([(x - 0.75, 6.4, 19.98), (x + 0.75, 6.4, 19.98), (x + 0.75, 10.2, 19.98), (x - 0.75, 10.2, 19.98)],
                     "f-window"))
    for i in range(22):
        x = -22 + i * 2 + 0.4
        if -5.5 < x < 5.5:
            continue
        f.add(v.face([(x, 12, 20), (x + 1.1, 12, 20), (x + 1.1, 12.8, 20), (x, 12.8, 20)], "f-wall"))
    f.add(v.box_svg(-5, 5, 0, 16, 19.4, 26, "f-wall2"))
    for i in range(5):
        x = -5 + i * 2 + 0.3
        f.add(v.face([(x, 16, 19.4), (x + 1.2, 16, 19.4), (x + 1.2, 16.9, 19.4), (x, 16.9, 19.4)], "f-wall2"))
    for x in (-2.2, 2.2):
        f.add(v.face([(x - 0.6, 10.4, 19.38), (x + 0.6, 10.4, 19.38), (x + 0.6, 13.6, 19.38), (x - 0.6, 13.6, 19.38)],
                     "f-window"))
    arch = [(-3, 0, 19.38), (3, 0, 19.38), (3, 5.6, 19.38)]
    for i in range(1, 8):
        t = math.pi * i / 8
        arch.append((3 * math.cos(t), 5.6 + 1.4 * math.sin(t), 19.38))
    arch.append((-3, 5.6, 19.38))
    f.add(v.face(arch, "f-walnut"))
    for side in (-1, 1):
        x0, x1 = sorted((side * 10, side * 14))
        door = "f-portcullis" if game == "D" else "f-door"
        f.add(v.face([(x0, 0, 19.97), (x1, 0, 19.97), (x1, 4.6, 19.97), (x0, 4.6, 19.97)], door))
        if game == "D":
            for i in range(1, 8):
                xx = x0 + i * 0.5
                f.add(v.seg((xx, 0, 19.96), (xx, 4.6, 19.96), "s-grill"))
            for y in (1.0, 2.0, 3.0, 4.0):
                f.add(v.seg((x0, y, 19.96), (x1, y, 19.96), "s-grill"))
        f.add(v.face([(x0, 5.0, 19.97), (x1, 5.0, 19.97), (x1, 5.22, 19.97), (x0, 5.22, 19.97)], "f-groove"))
        bx = side * 12
        f.add(v.face([(bx - 0.4, 6.8, 19.96), (bx + 0.4, 6.8, 19.96), (bx + 0.4, 11.0, 19.96), (bx - 0.4, 11.0, 19.96)],
                     "f-banner"))


def game_ground(v, game):
    f = v.f
    if game == "C":
        for side in (-1, 1):
            x0, x1 = sorted((side * 2, side * 22))
            v.ground(x0, x1, 0, 12, 0.005, "f-dais")
            v.ground(x0, x1, 16, 20, 0.005, "f-dais")
            l0, l1 = sorted((X(side, -1.2), X(side, 1.2)))
            v.ground(l0, l1, 0.2, 10.9, 0.02, "f-lane")
            p = side * (TEAM_X + 4.5)
            v.ground(p - 1, p + 1, 0, 2, 0.02, "pad")
        v.ground(-22, 22, 12, 16, -0.9, "f-water")
        f.add(v.face([(-22, -0.9, 16), (22, -0.9, 16), (22, 0, 16), (-22, 0, 16)], "f-wall", "side"))
        v.ground(-2, 2, 16, 20, 0.004, "f-pave")
    elif game == "D":
        for side in (-1, 1):
            x0, x1 = sorted((side * 2, side * 22))
            f.add(v.box_svg(x0, x1, 0, 0.4, 0, 20, "f-dais"))
            f.add(v.disc(side * TEAM_X, 10, 3.6, 0.41, "f-board"))
            p = side * (TEAM_X + 4.5)
            v.ground(p - 1, p + 1, 0, 2, 0.42, "pad")
    elif game == "A":
        f.add(v.box_svg(-22, 22, 0, 0.4, 0, 20, "f-dais"))
        v.ground(-9, 9, 2, 20, 0.41, "f-gold")
        for fl in range(8):
            for rk in range(8):
                x0, z0 = -9 + fl * 2.25 + 0.05, 2 + rk * 2.25 + 0.05
                v.ground(x0, x0 + 2.15, z0, z0 + 2.15, 0.43, "f-board")
        for side in (-1, 1):
            p = side * (TEAM_X + 4.5)
            v.ground(p - 1, p + 1, 0, 2, 0.42, "pad")


def game_items(v, game):
    if game == "C":
        for side in (-1, 1):
            x = lambda u: X(side, u)
            a, b = sorted((x(-1.6), x(1.6)))
            v.box(a, b, 0, 1.3, 10.8, 11.8, "f-walnut")
            for k in range(4):
                px, py = v.P(x(0), 0.55, 10.2 - k)
                r = max(1.6, v.F * 0.22 / v.depth(x(0), 0.55, 10.2 - k))
                v.item((x(0), 0.5, 10.2 - k), f'<circle cx="{n(px)}" cy="{n(py)}" r="{n(r)}" class="f-gold"/>')
            v.item((x(0), 0.5, 8.6), v.seg((x(0), 0.8, 10.9), (x(0), 0.35, 6.4), "s-rope"))
            a, b = sorted((x(-2), x(2)))
            v.box(a, b, 0, 4.1, 15.95, 16.3, "f-walnut")
            for u in (-2.4, 2.4):
                v.box(x(u) - 0.35, x(u) + 0.35, 0, 6, 15.85, 16.55, "f-wall")
                v.box(x(u) - 0.45, x(u) + 0.45, 6, 6.5, 15.75, 16.65, "f-slate")
            for u0, u1 in ((-1.7, -0.9), (1.7, 0.9)):
                v.item((x(u0), 2, 14), v.seg((x(u0), 4.0, 16.0), (x(u1), 1.3, 11.3), "s-chain"))
        v.box(-2, 2, 0, 1.0, 11.75, 12, "f-cut")
        v.box(-3, 3, 0, 6.0, 16, 16.35, "f-walnut")
    elif game == "D":
        for side in (-1, 1):
            cx = side * TEAM_X
            v.box(cx - 0.62, cx + 0.62, 0.4, 1.6, 9.38, 10.62, "f-walnut")
            v.box(cx - 0.36, cx + 0.36, 1.6, 1.85, 9.64, 10.36, "f-gold")
            for a in (45, 135, 225, 315):
                c, sn = math.cos(math.radians(a)), math.sin(math.radians(a))
                p0 = (cx + 0.7 * c, 0.95, 10 + 0.7 * sn)
                p1 = (cx + 2.7 * c, 0.95, 10 + 2.7 * sn)
                p2 = (cx + 3.0 * c, 0.95, 10 + 3.0 * sn)
                v.item(p1, v.seg(p0, p1, "s-bar-v") + v.seg(p1, p2, "s-bartip-v"))
            v.item((cx, 3, 15), v.seg((cx, 1.85, 10.2), (cx, 2.5, 11.0), "s-chainhi-v")
                   + v.seg((cx, 2.5, 11.0), (cx, 5.0, 19.9), "s-chainhi-v"))
    elif game == "A":
        for side in (-1, 1):
            x0, x1 = sorted((side * 20.2, side * 22))
            v.box(x0, x1, 0.4, 0.85, 3, 17, "f-wall")
            for z in (4, 8, 12, 16):
                xx = side * 19.4
                v.item((xx, 3, z), v.seg((xx, 0.4, z), (xx, 6.2, z), "s-pole")
                       + v.face([(xx, 5.0, z), (xx - side * 1.3, 5.0, z), (xx - side * 1.3, 6.0, z), (xx, 6.0, z)],
                                "f-banner"))
