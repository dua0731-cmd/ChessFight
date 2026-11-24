# The grand banquet hall's draft (level design v0.4) as a short film: one pawn (under the yellow marker) is followed
# along the one way through the hall, a cut and a caption for each beat, every device moving on its real period
# (banquet_layout.py). The pawn's own moves are drawn by hand from the design's numbers (run 2.5 m/s, climb 1.2 m/s):
# this shows how the section is meant to be played. It is not a recording of the game, and nothing here is in Unity.
#
#   blender --background --factory-startup --python banquet_flow_video.py -- <out.mp4> [width] [height]
#   blender --background --factory-startup --python banquet_flow_video.py -- <folder> [width] [height] stills
#           (one PNG from the middle of each beat instead of the film, to look at the framing)
import bpy, math, os, sys

_here = os.path.dirname(os.path.abspath(__file__))
_argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FILM_OUT = _argv[0] if _argv else "//banquet_flow.mp4"
FILM_W = int(_argv[1]) if len(_argv) > 1 else 1280
FILM_H = int(_argv[2]) if len(_argv) > 2 else 720
STILLS = len(_argv) > 3 and _argv[3] == "stills"
if "--" in sys.argv:
    sys.argv = sys.argv[:sys.argv.index("--") + 1] + ["//unused.png", "16", str(FILM_W), str(FILM_H)]
_set = open(os.path.join(_here, "banquet_blockout.py"), encoding="utf-8").read()
exec(compile(_set.split("# ==== still")[0], "banquet_blockout.py (the set)", "exec"), globals())

FPS = 24
DT = 1.0 / FPS


def ease(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3.0 - 2.0 * k)


def mix(a, b, k):
    return tuple(p + (q - p) * k for p, q in zip(a, b))


def hop(a, b, k, h):
    k = max(0.0, min(1.0, k))
    p = mix(a, b, k)
    return (p[0], p[1] + h * 4.0 * k * (1.0 - k), p[2])


def track(t, keys):
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, p0, _), (t1, p1, arc) in zip(keys, keys[1:]):
        if t <= t1:
            return hop(p0, p1, (t - t0) / max(1e-6, t1 - t0), arc)
    return keys[-1][1]


def K(*rows):
    return [(r[0], (r[1], r[2], r[3]), r[4] if len(r) > 4 else 0.0) for r in rows]


def fall(t, t0, a, b, dur=0.5, arc=0.4):
    # A pawn knocked off at t0 from a to b: (point, tumble degrees).
    k = (t - t0) / dur
    if k < 1.0:
        return hop(a, b, k, arc), 400.0 * k
    return b, 90.0


TOP = TABLE + 0.05                                   # the table with its cloth on


def wz(k, t):
    return L["waiter_z"](k, t)


# ---------------------------------------------------------------- 1 the waiters
def hero_waiters(t):
    z = wz(0, t)
    if t < 4.6:
        return track(t, K((2.5, 19.0, 0, 8.4), (3.2, 20.8, 0.8, 8.4, 0.3), (3.5, 21.6, 1.6, 8.4, 0.3), (3.9, 23.0, TRAY_Y, 8.4, 0.3), (4.6, 25.3, TRAY_Y, 9.0)))
    if t < 5.45:
        return (25.3, TRAY_Y, 9.0)
    if t < 5.85:
        return hop((25.3, TRAY_Y, 9.0), (26.7, TRAY_Y, z), (t - 5.45) / 0.4, 0.4)
    if t < 8.0:
        return (26.7, TRAY_Y + 1.2 * (t - 5.85), z)
    if t < 8.45:
        return hop((26.7, 4.98, z), (TRAY_X, STACK_TOP, z), (t - 8.0) / 0.45, 0.25)
    if t < 11.15:
        return (TRAY_X, STACK_TOP, z)
    z0 = wz(0, 11.15)
    if t < 11.65:
        return hop((TRAY_X, STACK_TOP, z), (24.9, TOP, z0 + 0.5), (t - 11.15) / 0.5, 0.3)
    return track(t, K((11.65, 24.9, TOP, z0 + 0.5), (12.6, 21.5, TOP, 26.0)))


def other_waiter_early(t):
    z = wz(1, t)
    if z < 21.5:
        return (TRAY_X, STACK_TOP, z)
    return (24.9, TOP, min(30.0, 22.0 + (z - 21.5)))


def other_waiter_late(t):
    z = wz(-1, t)
    return (26.7, 3.9 + 0.3 * math.sin(t * 3.0), z) if z < L["WAITER_Z1"] - 0.6 else None


# ---------------------------------------------------------------- 2 the tablecloth trick
def cloth_y(t):
    return TOP if L["cloth_shift"](t) is not None else TABLE


def hero_trick(t):
    if t < 16.0:
        return track(t, K((12.0, 21.5, TOP, 26.0), (13.5, 20.8, TOP, 27.6), (14.2, 20.0, TOP + 0.3, 29.0, 0.4), (16.0, 20.0, TOP + 0.3, 29.0)))
    p = track(t, K((16.0, 20.0, TOP + 0.3, 29.0), (16.4, 20.6, TABLE, 30.8, 0.3), (21.5, 20.6, TABLE, 43.5)))
    return (p[0], cloth_y(t) if t > 16.4 else p[1], p[2])


def flung(z0, x, t_hit, land):
    def f(t):
        if t < 15.5:
            return (x, TOP, z0)
        if t < t_hit:
            k = (t - 15.5) / 0.3
            return (x, TOP, z0 - L["TRICK_PULL"] * k * k)
        return fall(t, t_hit, (x, TOP, 18.0), land, 0.6, 1.5)
    return f


def safe_plate(z):
    return lambda t: (20.0, TOP + 0.3 + (0.2 * abs(math.sin(t * 7.0)) if 15.9 < t < 17.0 else 0.0), z)


# ---------------------------------------------------------------- 3 the cloches
def lift(t):
    return L["cloche_lift"](t)[0]


def hero_cloches(t):
    if t < 23.6:
        return track(t, K((21.0, 20.6, TOP, 46.0), (22.4, 20.0, TOP, 49.8), (23.2, 20.0, TOP, 55.4), (23.6, 17.0, TOP, 55.4)))
    if t < 24.4:
        return (17.0, TOP + 1.2 * (t - 23.6), 55.4)
    if t < 25.8:
        return (17.0, TOP + 0.96 + lift(t), 55.4)
    if t < 26.2:
        return (17.0, TOP + 0.96 + lift(t) + 0.7 * (t - 25.8), 55.4)
    if t < 26.7:
        return hop((17.0, TOP + 0.96 + lift(t) + 0.28, 55.4), (17.0, DAIS_Y, 57.2), (t - 26.2) / 0.5, 0.5)
    return track(t, K((26.7, 17.0, DAIS_Y, 57.2), (29.6, 10.0, DAIS_Y, 57.0)))


def other_cloche(t):
    if t < 26.0:
        return (23.0, TOP + 0.96 + lift(t), 55.4)
    if t < 26.5:
        return hop((23.0, TOP + 0.96 + lift(t), 55.4), (23.0, DAIS_Y, 57.2), (t - 26.0) / 0.5, 0.5)
    return (23.0 + (t - 26.5) * 1.0, DAIS_Y, 57.2)


def trapped(t):
    return track(t, K((21.0, 25.0, TOP, 46.0), (26.0, 25.0, TOP, 47.5), (27.2, 23.4, TOP + 0.15, 51.6)))


# ---------------------------------------------------------------- 4 the waltz
T_STOP = L["STOP"] / L["WALTZ_SPEED"]
T_BOARD = 8 * T_STOP


def boarding_couple(tb):
    target = (L["BOARD_X"], L["TRACK_N"] + PAIR)
    return min(range(COUPLES), key=lambda i: L["dist"](L["couple"](i, tb)[1], target))


I_BOARD = boarding_couple(T_BOARD)
T_ALIGHT = T_BOARD + L["ALIGHT"][0]


def on_queen(i, t, r, y):
    mid, q, king, turn = L["couple"](i, t)
    a = math.radians(turn)
    return (q[0] + r * math.cos(a), y, q[1] + r * math.sin(a))


def hero_waltz(t):
    if t < 37.9:
        return track(t, K((33.0, 2.0, DAIS_Y, 56.6), (35.5, L["BOARD_X"], DAIS_Y, DAIS_Z + 0.35)))
    if t < 38.3:
        return hop((L["BOARD_X"], DAIS_Y, DAIS_Z + 0.35), on_queen(I_BOARD, t, 1.63, 6.6), (t - 37.9) / 0.4, 0.3)
    if t < 38.9:
        k = (t - 38.3) / 0.6
        return on_queen(I_BOARD, t, 1.63 - 0.78 * k, 6.6 + 0.8 * k)
    if t < T_ALIGHT:
        return on_queen(I_BOARD, t, 0.85, SHOULDER_Y)
    a = on_queen(I_BOARD, T_ALIGHT, 0.85, SHOULDER_Y)
    if t < T_ALIGHT + 0.5:
        return hop(on_queen(I_BOARD, t, 0.85, SHOULDER_Y), (-20.6, STAGE_Y, a[2]), (t - T_ALIGHT) / 0.5, 0.4)
    return track(t, K((T_ALIGHT + 0.5, -20.6, STAGE_Y, a[2]), (48.0, CURTAIN_X + 0.6, STAGE_Y, L["CURTAIN_Z"] - 0.6)))


I_NEXT = boarding_couple(T_BOARD + T_STOP)


def other_waltz(t):
    tb = T_BOARD + T_STOP
    if t < tb - 0.4:
        return track(t, K((33.0, 6.0, DAIS_Y, 57.0), (37.0, L["BOARD_X"] - 1.2, DAIS_Y, DAIS_Z + 0.5), (tb - 0.4, L["BOARD_X"], DAIS_Y, DAIS_Z + 0.35)))
    if t < tb:
        return hop((L["BOARD_X"], DAIS_Y, DAIS_Z + 0.35), on_queen(I_NEXT, t, 1.63, 6.6), (t - tb + 0.4) / 0.4, 0.3)
    if t < tb + 0.6:
        k = (t - tb) / 0.6
        return on_queen(I_NEXT, t, 1.63 - 0.78 * k, 6.6 + 0.8 * k)
    return on_queen(I_NEXT, t, 0.85, SHOULDER_Y)


# ---------------------------------------------------------------- 5 the curtain
def pz(k, t):
    return L["pleat_z"](-1, k, t)


def hero_curtain(t):
    x = CURTAIN_X + 0.3
    if t < 48.0:
        return (CURTAIN_X + 0.6, STAGE_Y, pz(0, t))
    if t < 51.0:
        return (x, STAGE_Y + 1.2 * (t - 48.0), pz(0, t))
    if t < 56.0:
        return (x, STAGE_Y + 3.6, pz(0, t))
    if t < 56.45:
        return hop((x, STAGE_Y + 3.6, pz(0, t)), (CURTAIN_X - 0.2, BUFFET_TOP, 15.6), (t - 56.0) / 0.45, 0.0)
    return track(t, K((56.45, CURTAIN_X - 0.2, BUFFET_TOP, 15.6), (57.5, -24.2, BUFFET_TOP, 14.2)))


T_BASS = 51.0
while pz(1, T_BASS) > BASSES[1][1] + 0.9 and T_BASS < 60.0:
    T_BASS += 0.01


def other_curtain(t):
    x = CURTAIN_X + 0.3
    if t < T_BASS:
        return (x, STAGE_Y + 1.2, pz(1, t))
    return fall(t, T_BASS, (x, STAGE_Y + 1.2, pz(1, T_BASS)), (x + 0.8, 0.08, pz(1, T_BASS) - 0.8), 0.6, 0.2)


# ---------------------------------------------------------------- 6 the chocolate fountain
F = FOUNTAIN
AH = (math.sin(math.radians(20.0)), math.cos(math.radians(20.0)))


def fp(r, y, a=None):
    h = AH if a is None else (math.sin(math.radians(a)), math.cos(math.radians(a)))
    return (F[0] + r * h[0], y, F[1] + r * h[1])


def dome_y(k, r):
    return TIER_Y[k] + 0.6 * (TIER_R[k] - r) / (TIER_R[k] - 0.55)


NET = 1.2 - FLOW


def fountain_keys():
    keys = [(56.3, (CURTAIN_X - 0.2, BUFFET_TOP, 15.6), 0.0), (56.45, (CURTAIN_X - 0.2, BUFFET_TOP, 15.6), 0.0), (57.2, fp(5.3, BUFFET_TOP), 0.0),
            (57.6, fp(4.6, BOWL_TOP), 0.3), (57.9, fp(4.3, BOWL_TOP), 0.0)]
    t, y = 57.9, BOWL_TOP
    for k in range(4):
        r = TIER_R[k] + 0.3
        t += (TIER_Y[k] - y) / NET
        keys.append((t, fp(r, TIER_Y[k]), 0.0))
        t += 0.45
        if k < 3:
            keys.append((t, fp(TIER_R[k] - 0.3, dome_y(k, TIER_R[k] - 0.3)), 0.25))
            t += 0.3
            y = TIER_BASE[k + 1]
            keys.append((t, fp(TIER_R[k + 1] + 0.3, y), 0.0))
        else:
            keys.append((t, fp(0.85, dome_y(3, 0.85)), 0.25))
    return keys, t


FKEYS, T_TOP = fountain_keys()
T_LEAP = T_TOP + 1.0


def hero_fountain(t):
    if t <= T_TOP:
        return track(t, FKEYS)
    if t < T_LEAP:
        a = 20.0 + 160.0 * (t - T_TOP) / 1.0
        return fp(0.85, dome_y(3, 0.85), a)
    if t < T_LEAP + 0.45:
        return hop(fp(0.85, dome_y(3, 0.85), 180.0), (F[0], 18.0, SCREEN_Z + 0.3), (t - T_LEAP) / 0.45, 0.6)
    if t < T_LEAP + 1.95:
        return (F[0], 18.0 + 1.2 * (t - T_LEAP - 0.45), SCREEN_Z + 0.3)
    return hop((F[0], 19.8, SCREEN_Z + 0.3), (F[0], LOFT_Y, SCREEN_Z - 0.9), (t - T_LEAP - 1.95) / 0.45, 0.3)


def other_fountain(t):
    a = 65.0
    if t < 60.1:
        return fp(TIER_R[1] + 0.3, 11.5 - FLOW * (t - 56.3), a)
    if t < 60.6:
        r = TIER_R[0] - 0.7 + 0.8 * (t - 60.1)
        return fp(r, dome_y(0, r), a)
    return fall(t, 60.6, fp(TIER_R[0], TIER_Y[0], a), fp(4.6, BOWL_TOP, a), 0.45, 0.3)


# ---------------------------------------------------------------- 7 the chandeliers
LANE = 0
T_GRAB = 78.75
HANDS = [T_GRAB + 2.5 * (k + 1) for k in range(CHANDELIERS - 1)]
T_OFF = HANDS[-1] + 2.5
LAND = (LINES_X[0] + 0.4, HEAD_TOP + LEVEL_H, L["LAND_Z"] + 0.05)


def on_rope(k, t):
    p = L["rope_point"](LANE, k, t, ROPE_L - 0.9)
    return (p[0], p[1] - 1.15, p[2])


def hero_ropes(t):
    stand = (LINES_X[0], LOFT_Y, SCREEN_Z - 0.3)
    if t < T_GRAB:
        return stand
    if t < T_GRAB + 0.25:
        return mix(stand, on_rope(0, t), ease((t - T_GRAB) / 0.25))
    if t >= T_OFF:
        k = ease((t - T_OFF) / 0.45)
        p = mix(on_rope(CHANDELIERS - 1, T_OFF), LAND, k)
        return (p[0], p[1] + 0.4 * 4.0 * k * (1.0 - k), p[2])
    n = sum(1 for h in HANDS if t >= h + 0.3)
    pos = on_rope(n, t)
    for i, h in enumerate(HANDS):
        if h - 0.1 <= t < h + 0.3:
            k = ease((t - h + 0.1) / 0.4)
            pos = mix(on_rope(i, t), on_rope(i + 1, t), k)
            lift_ = 0.5 if L["HANDOVERS"][i][1] < 0.9 else 0.0
            pos = (pos[0], pos[1] + lift_ * 4.0 * k * (1.0 - k), pos[2])
    return pos


# ---------------------------------------------------------------- 8 the champagne tower and the balcony
def row_face(k):
    n = LEVELS - k
    return TOWER_Z - (n - 1) / 2.0 * GLASS_PITCH - 0.95 - 0.3


def hero_tower(t):
    return track(t, K((94.2, LAND[0], LAND[1], LAND[2]), (94.6, -1.9, HEAD_TOP + LEVEL_H, row_face(1)), (95.85, -1.9, HEAD_TOP + 2 * LEVEL_H, row_face(1)),
                      (96.25, -0.95, HEAD_TOP + 2 * LEVEL_H, row_face(2), 0.2), (98.1, -0.95, HEAD_TOP + 2 * LEVEL_H, row_face(2)),
                      (99.35, -0.95, HEAD_TOP + 3 * LEVEL_H, row_face(2)), (99.75, 0.0, HEAD_TOP + 3 * LEVEL_H, row_face(3), 0.2),
                      (99.8, 0.0, HEAD_TOP + 3 * LEVEL_H, row_face(3)), (101.05, 0.0, TOWER_TOP, row_face(3)),
                      (101.6, 0.0, BALCONY_Y, BALCONY[2] + 0.6, 0.3), (103.8, 0.0, BALCONY_Y, BALCONY[3] - 0.8)))


def other_tower(t):
    if t < 96.65:
        return track(t, K((94.2, 1.9, HEAD_TOP + LEVEL_H, row_face(1)), (96.0, 0.95, HEAD_TOP + 2 * LEVEL_H, row_face(2), 0.2),
                          (96.65, 0.95, HEAD_TOP + 2 * LEVEL_H + 0.78, row_face(2))))
    return fall(t, 96.65, (0.95, HEAD_TOP + 2 * LEVEL_H + 0.78, row_face(2)), (2.6, HEAD_TOP, PEDESTAL[2] + 0.8), 0.6, 0.3)


# ---------------------------------------------------------------- the cuts
HERO = pawn("Hero", 1, True)
EXTRAS = {}


def extra(name, team, fn):
    g = pawn(name, team)
    EXTRAS[g] = fn
    return (g, fn)


WIDE = ((74.0, 64.0, 4.0), (-7.0, 3.0, 33.0))
CUTS = [
    dict(title="대연회장 — 연회와 무도회가 한창인 홀", sub="레벨 디자인 초안 v0.4 · 고른 장치 여덟(웨이터 · 식탁보 빼기 · 은 뚜껑 · 왈츠 · 무대 막 · 초콜릿 분수 · 샹들리에 · 샴페인 탑) · 갈림 없는 한 줄 길",
         t0=0.0, length=5.0, hero=None, others=[], cam=WIDE),
    dict(title="① 웨이터 행렬", sub="배식실 카운터에서 지나가는 쟁반에 올라탄다 · 접시 탑을 오르는 동안에도 웨이터는 걷는다 — 문으로 나가기 전에 긴 식탁으로",
         t0=2.5, length=10.0, hero=hero_waiters, others=[extra("Rider early", 0, other_waiter_early), extra("Rider late", 0, other_waiter_late)],
         cam=((46.0, 13.0, 1.0), (24.0, 3.2, 15.0))),
    dict(title="② 마술사의 식탁보 빼기", sub="북이 울리면 접시 위로 · 마술사가 식탁보를 남쪽으로 빼낸다 — 천 위의 폰은 배식실로 날아가 웨이터부터 다시",
         t0=12.0, length=9.5, hero=hero_trick,
         others=[extra("Flung", 0, flung(33.5, 21.0, 15.715, (19.0, 0.0, 9.0))), extra("Flung", 0, flung(24.0, 21.6, 15.63, (23.0, TRAY_Y, 10.5))),
                 extra("On a plate", 0, safe_plate(35.0)), extra("On a plate", 1, safe_plate(41.0))],
         cam=((40.0, 15.0, 15.0), (19.0, 4.5, 30.0))),
    dict(title="③ 은 뚜껑 — 들려서 왕의 단으로", sub="장갑이 손잡이를 쥐면(흔들림) 뚜껑이 '짜잔!' 들린다 · 띠에 매달려 단 높이(10.4)로 · 뚜껑 밑에 있으면 갇힌다",
         t0=21.0, length=8.6, hero=hero_cloches, others=[extra("Lifted", 0, other_cloche), extra("Trapped", 0, trapped)],
         cam=((38.0, 20.0, 67.0), (18.0, 7.5, 53.5))),
    dict(title="④ 왈츠 — 춤추는 여왕에 실려", sub="여왕이 단 앞에 멈춘 순간 드레스로 뛰어 붙어 어깨로 · 커플들은 홀을 돌며 춤춘다 — 무대 앞에서 멈출 때 건너간다",
         t0=33.0, length=15.0, hero=hero_waltz, others=[extra("Next rider", 0, other_waltz)],
         cam=((-14.0, 24.0, 70.0), (-10.0, 5.0, 44.0))),
    dict(title="⑤ 무대 막 — 열리는 막에 매달려", sub="닫힌 막을 베이스 머리보다 높이(3.5 m) 오른다 · 막이 열리면 악단석 너머 디저트 뷔페 위로 · 낮으면 걸려 떨어진다",
         t0=47.0, length=10.5, hero=hero_curtain, others=[extra("Too low", 0, other_curtain)],
         cam=((-2.0, 16.0, 22.0), (-23.0, 9.0, 30.0))),
    dict(title="⑥ 초콜릿 분수 — 흐름을 거슬러", sub="흘러내리는 초콜릿을 거슬러 네 층 · 매달려 쉬면 밀려 내려간다 · 꼭대기에서 천장 회랑으로 뛰어 붙는다",
         t0=56.3, length=T_LEAP + 2.5 - 56.3, hero=hero_fountain, others=[extra("Resting", 0, other_fountain)],
         cam=((-8.0, 17.0, 30.0), (-26.5, 12.5, 9.0))),
    dict(title="⑦ 샹들리에 — 무도장 위를 건너", sub="회랑에서 첫 샹들리에로 · 끝에서 만나는 순간 다음 것으로 — 건네받기 · 떨어지며 잡기 · 날아서 잡기 · 아래에서 왈츠가 돈다",
         t0=77.5, length=T_OFF + 0.9 - 77.5, hero=hero_ropes, others=[], cam="side"),
    dict(title="⑧ 샴페인 탑 — 왕의 발코니로", sub="왕의 식탁 한가운데 · 넘쳐 내려오는 물결(빛나는 층)을 턱에서 기다렸다 오른다 · 꼭대기에서 발코니, 출구",
         t0=94.2, length=9.8, hero=hero_tower, others=[extra("Washed off", 0, other_tower)],
         cam=((14.0, 24.5, 55.0), (-0.5, 18.5, 64.0))),
    dict(title="홀을 세 번 지나는 한 줄 길 · 보통 2분 40초", sub="아직 초안이다 — Unity에 만든 것이 없고, 폰의 움직임은 기획 수치로 그린 것이다",
         t0=110.0, length=5.0, hero=None, others=[], cam=WIDE),
]
STARTS, total = [], 0
for c in CUTS:
    STARTS.append(total)
    total += int(round(c["length"] * FPS))
print("FILM", total, "frames,", round(total / FPS, 1), "s; boarding couple", I_BOARD, "alight at", round(T_ALIGHT, 2))

# ---------------------------------------------------------------- captions and light
M_TEXT = principled("MAT_Caption", (1, 1, 1), 0.5, 0.0, (1.0, 0.97, 0.9), 2.2)
M_TEXT2 = principled("MAT_CaptionSmall", (1, 1, 1), 0.5, 0.0, (1.0, 0.86, 0.6), 1.6)
M_BAR = principled("MAT_CaptionBar", (0.0, 0.0, 0.0), 1.0, 0.0, (0.02, 0.012, 0.006), 1.0)
font = None
for name in ("malgunbd.ttf", "malgun.ttf"):
    path = os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name)
    if os.path.exists(path):
        font = bpy.data.fonts.load(path)
        break
cam_d.lens = 30.0
cam_d.sensor_width = 36.0
AHEAD = 0.5
half_w = AHEAD * 18.0 / cam_d.lens
half_h = half_w * FILM_H / FILM_W


def caption(body, size, mat):
    cu = bpy.data.curves.new("Caption", "FONT")
    cu.body = body
    cu.size = size
    if font is not None:
        cu.font = font
    o = bpy.data.objects.new("Caption", cu)
    col.objects.link(o)
    o.data.materials.append(mat)
    o.parent = cam
    o.location = (0, 0, AHEAD * 40.0)
    return o


bar_o = inst(ME_CUBE, "Caption bar", M_BAR, (0, 0, 0), (half_w * 2.1, half_h * 0.42, 0.002))
bar_o.parent = cam
bar_o.location = (0, -half_h * 0.81, -AHEAD - 0.004)
CAPTIONS = [(caption(c["title"], half_h * 0.105, M_TEXT), caption(c["sub"], half_h * 0.05, M_TEXT2)) for c in CUTS]

world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.13, 0.10, 1)
sun = light("LGT_FilmSun", "SUN", U(90, 90, 20), 2.6, (1.0, 0.90, 0.74), angle=D(8))
aim(sun, U(0, 4, 36))
back = light("LGT_FilmBack", "SUN", U(-60, 70, 120), 0.9, (0.72, 0.80, 1.0), angle=D(20))
aim(back, U(0, 4, 36))
back.data.use_shadow = False
for (x, y, z) in lamps:
    o = light("LGT_Lamp", "POINT", U(x, y, z), 1200, (1.0, 0.60, 0.28), shadow_soft_size=0.4)
    o.data.use_shadow = False
scene.render.engine = "BLENDER_EEVEE"
scene.eevee.taa_render_samples = 12
scene.view_settings.exposure = -0.35
scene.render.resolution_x, scene.render.resolution_y = FILM_W, FILM_H
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 1, total


def show(frame):
    index = max(i for i, s in enumerate(STARTS) if s <= frame - 1)
    c = CUTS[index]
    n = frame - 1 - STARTS[index]
    t = c["t0"] + n * DT
    pose(t)
    p = c["hero"](t) if c["hero"] else None
    place(HERO, p)
    for g in EXTRAS:
        g.location = U(*HIDE)
    for g, fn in c["others"]:
        r = fn(t)
        if isinstance(r, tuple) and len(r) == 2 and isinstance(r[0], tuple):
            place(g, r[0], r[1])
        else:
            place(g, r)
    if c["cam"] == "side" and p:
        z = p[2]
        smooth = show.z + (z - show.z) * 0.06 if show.cut == index else z
        show.z = smooth
        cam.location = U(24.0, 23.0, smooth - 4.0)
        aim(cam, U(LINES_X[0], 18.0, smooth + 2.0))
    elif c["cam"] != "side":
        (fx, fy, fz), (ax, ay, az) = c["cam"]
        k = n / float(max(1, int(c["length"] * FPS) - 1))
        drift = 0.03 * (k - 0.5)
        cam.location = U(fx + (ax - fx) * drift, fy, fz + (az - fz) * drift)
        aim(cam, U(ax, ay, az))
    show.cut = index
    for i, (a, b) in enumerate(CAPTIONS):
        if i == index:
            a.location = (-half_w * 0.94, -half_h * 0.76, -AHEAD)
            b.location = (-half_w * 0.94, -half_h * 0.92, -AHEAD)
        else:
            a.location = b.location = (0, 0, AHEAD * 40.0)


show.cut, show.z = -1, 0.0


def on_frame(sc, depsgraph=None):
    show(sc.frame_current)


if STILLS:
    scene.render.image_settings.file_format = "PNG"
    for i, start in enumerate(STARTS):
        frame = start + 1 + int(CUTS[i]["length"] * FPS * 0.55)
        scene.frame_set(frame)
        show(frame)
        scene.render.filepath = os.path.join(FILM_OUT, "beat_%02d.png" % i)
        bpy.ops.render.render(write_still=True)
        print("STILL", i, frame)
else:
    bpy.app.handlers.frame_change_pre.append(on_frame)
    img = scene.render.image_settings
    if "media_type" in img.bl_rna.properties:
        img.media_type = "VIDEO"
    img.file_format = "FFMPEG"
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "HIGH"
    scene.render.ffmpeg.gopsize = 24
    scene.render.filepath = FILM_OUT
    bpy.ops.render.render(animation=True)
    print("FILMED", FILM_OUT)
