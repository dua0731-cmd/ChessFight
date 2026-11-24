# The grand banquet hall (Pawn Rush 2-4, level design v0.4): the one place its numbers are worked out.
# Section frame: the entry's threshold is the origin, x east, y up, z north (the way in).
#
# v0.4 is the user's choice of devices (R814 ideas 1-6, the chandeliers and the waltz) set in one hall laid out as
# a royal banquet-and-ball hall: the screens passage and servery at the entry, the long banquet table down the east
# side, the orchestra's stage with its curtain down the west side, the dessert buffet with the chocolate fountain in
# the south-west corner, the dance floor in the middle with the couples going round it, the chandeliers over it, the
# royal dais with the head table and the champagne tower at the north end and the king's balcony over that.
# One way, no fork: north up the long table, back across and down the west side, and north again in the air.
#
#   python banquet_layout.py                 prints the layout, the stops, the time budget and the way's length
#   python banquet_layout.py <plan.svg>      also draws the plan, the hall cut along its middle, the height profile
#
# banquet_blockout.py (the Blender still) and banquet_flow_video.py (the walkthrough film) run this file for the
# same numbers and for where every moving thing is at a time t. Nothing here is in Unity.
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
    k = d / dist(a, b)
    return (a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)


def ease(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3.0 - 2.0 * k)


# ---------------------------------------------------------------- pawn numbers (RagdollTuning.asset, RagdollPawn)
CLIMB_UP, CLIMB_DOWN, CLIMB_SIDE, STAMINA, ROOK = 1.2, 1.6, 0.9, 8.0, 0.7
RUN, SPRINT = 2.5, 3.6
ROPE_REACH, ROPE_JUMP_UP, ROPE_REGRAB = 0.7, 3.5, 0.5
MAX_DROP = 8.0                                   # FallDistanceRespawn: this far under the last floor stood on is a fall
KNOCKDOWN_DROP = 4.1

# ---------------------------------------------------------------- the hall and its levels
FLOOR, TABLE, STAGE_Y, DAIS_Y, LOFT_Y = 0.0, 4.4, 7.4, 10.4, 20.0
HEAD_TOP = DAIS_Y + 4.4                          # the head table, on the dais
HALL = (-32.0, 32.0, 0.0, 72.0)                  # x0, x1, z0 (the south wall, the entry), z1 (the north wall)
CEILING = 34.0
SCREEN_Z = 6.0                                   # the screens passage: z 0 .. 6 at the floor, shut off from the hall
SCREENS = ((-22.0, SCREEN_Z), (13.0, SCREEN_Z), (13.0, 18.0))   # the screen's line: along the passage, then round the servery to the guests
LOFT = (-31.0, 31.0, 0.0, SCREEN_Z)              # the chandelier keepers' gallery over it, at LOFT_Y
DAIS_Z = 56.0                                    # the dais's front (a sheer marble face, its stairs are behind)

# ---------------------------------------------------------------- 1 the waiters' parade (east lane)
LANE = (26.0, 32.0)                              # between the long table and the east wall
WAITER_X, TRAY_X = 29.8, 27.8                    # the body's line and the tray's
TRAY_R, TRAY_Y, STACK_R, STACK_TOP = 1.5, 2.4, 0.8, 5.0
WAITER_Z0, WAITER_Z1 = -3.0, 46.0                # out of the kitchen door, in at the pantry door
WAITERS = 5
WAITER_GAP = (WAITER_Z1 - WAITER_Z0) / WAITERS
WAITER_SPEED = 2.2
COUNTER = (22.0, 26.0, 4.0, 12.0)                # the servery counter: x0, x1, z0, z1, top TRAY_Y, steps on its west side


def waiter_z(k, t):
    # Where waiter k's tray is along the lane at time t (they keep walking, out of one door and in at the other).
    z = WAITER_Z0 + WAITER_SPEED * t + WAITER_GAP * k
    return (z - WAITER_Z0) % (WAITER_Z1 - WAITER_Z0) + WAITER_Z0


# ---------------------------------------------------------------- 2 the long table and the magician's tablecloth trick
TABLE_X0, TABLE_X1, TABLE_Z0, TABLE_Z1 = 14.0, 26.0, 18.0, DAIS_Z
RUNNER_X, RUNNER = 20.0, 2.5
PLATES = tuple(23.0 + 6.0 * i for i in range(5))  # z of each plate on the runner (radius 1.2): the only safe spots
PLATE_R = 1.2
GUESTS = tuple(21.0 + 4.0 * i for i in range(9))  # z of the guests seated along the inner (west) edge: a wall of backs
MAGICIAN = (20.0, FLOOR, 15.2)                   # at the south end, on the floor, holding the cloth's end
TRICK = (3.5, 2.0, 0.3, 2.2, 2.0)                # lying, drum roll, the pull, gone, laid again (s)
TRICK_PULL = 30.0                                # how far south the cloth goes in the pull (m)


def cloth_shift(t):
    # How far south the cloth is from where it lies, or None while it is in the magician's hands.
    lie, roll, pull, gone, lay = TRICK
    u = t % sum(TRICK)
    if u < lie + roll:
        return 0.0
    if u < lie + roll + pull:
        k = (u - lie - roll) / pull
        return TRICK_PULL * k * k
    if u < lie + roll + pull + gone:
        return None
    return -(TABLE_Z1 - TABLE_Z0) * (1.0 - ease((u - lie - roll - pull - gone) / lay))


def drum_roll(t):
    lie, roll = TRICK[0], TRICK[1]
    u = t % sum(TRICK)
    return lie <= u < lie + roll


# ---------------------------------------------------------------- 3 the silver cloches at the table's north end, up to the dais
CLOCHES = ((17.0, 52.9), (23.0, 52.9))
CLOCHE_R, CLOCHE_BAND = 2.2, 1.4
CLOCHE_LIFT = 5.6
CLOCHE_CYCLE = (0.8, 0.8, 2.6, 0.6, 2.4)          # warning, up, held, down, covered (s)
CLOCHE_PHASE = 2.6                               # the first warning


def cloche_lift(t):
    # (height lifted, jiggle) of both cloches at time t.
    u = t - CLOCHE_PHASE
    if u < 0:
        return 0.0, 0.0
    w, up, held, down, rest = CLOCHE_CYCLE
    u %= sum(CLOCHE_CYCLE)
    if u < w:
        return 0.0, math.sin(u * 40.0) * 0.05
    if u < w + up:
        return CLOCHE_LIFT * ease((u - w) / up), 0.0
    if u < w + up + held:
        return CLOCHE_LIFT, 0.0
    if u < w + up + held + down:
        k = (u - w - up - held) / down
        return CLOCHE_LIFT * (1.0 - k * k), 0.0
    return 0.0, 0.0


# ---------------------------------------------------------------- 4 the waltz: the couples going round the floor (the line of dance)
GOWN_FOOT, GOWN_SHOULDER, SHOULDER_Y, HEAD_R = 1.8, 1.3, STAGE_Y, 0.55
PAIR = 1.9                                       # each partner this far from the couple's middle
STOP = 2.0 * math.pi * PAIR                      # along the way between the queen's stops (she turns once per stop)
WALTZ_SPEED = 2.5                                # the couples' middles, round the floor anticlockwise
SPIN = WALTZ_SPEED / PAIR                        # rad/s, clockwise: the queen stands still each time she is outside
COUPLES = 10
TRACK_W, TRACK_E, TRACK_S, TRACK_N = -16.2, 7.5, 10.2, DAIS_Z - GOWN_SHOULDER - 1.0 - PAIR
TRACK_R = ((2 * (TRACK_E - TRACK_W) + 2 * (TRACK_N - TRACK_S)) - COUPLES * STOP) / (8.0 - 2.0 * math.pi)
BOARD_X = -4.25                                  # where on the dais's front a queen stops (the north straight)


def _track_parts():
    r = TRACK_R
    w, e, s, n = TRACK_W, TRACK_E, TRACK_S, TRACK_N
    return [("line", (e - r, n), (w + r, n)), ("arc", (w + r, n - r), 90.0), ("line", (w, n - r), (w, s + r)), ("arc", (w + r, s + r), 180.0),
            ("line", (w + r, s), (e - r, s)), ("arc", (e - r, s + r), 270.0), ("line", (e, s + r), (e, n - r)), ("arc", (e - r, n - r), 0.0)]


TRACK_LEN = sum(dist(a, b) if kind == "line" else math.pi / 2 * TRACK_R for kind, a, b in _track_parts())


def track_point(s):
    # (x, z) of the way round the floor at distance s from the north straight's east end, anticlockwise.
    s %= TRACK_LEN
    for kind, a, b in _track_parts():
        length = dist(a, b) if kind == "line" else math.pi / 2 * TRACK_R
        if s <= length:
            if kind == "line":
                k = s / length
                return (a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)
            ang = math.radians(b) + s / TRACK_R      # b: where on the circle the arc starts (degrees from +x, anticlockwise)
            return (a[0] + TRACK_R * math.cos(ang), a[1] + TRACK_R * math.sin(ang))
        s -= length
    return track_point(0.0)


S_BOARD = (TRACK_E - TRACK_R) - BOARD_X          # along the way to the boarding stop from the north straight's east end


def couple(i, t):
    # (middle, queen, king, turn degrees) of couple i: at t = 0 couple 0's queen stands still at the boarding stop.
    s = S_BOARD + i * STOP + WALTZ_SPEED * t
    mid = track_point(s)
    phi = math.pi / 2 - SPIN * t                   # clockwise from north (outside on the north straight) at t = 0
    off = (PAIR * math.cos(phi), PAIR * math.sin(phi))
    return mid, (mid[0] + off[0], mid[1] + off[1]), (mid[0] - off[0], mid[1] - off[1]), math.degrees(phi)


def queen_stops(i, t0, t1, step=0.01):
    # The times and places in [t0, t1) where couple i's queen stands still (a local minimum of her speed).
    out, prev, prev_v, t = [], None, None, t0
    best = None
    while t < t1:
        q = couple(i, t)[1]
        if prev is not None:
            v = dist(q, prev) / step
            if prev_v is not None and v < 0.25:
                if best is None or v < best[0]:
                    best = (v, t, q)
            elif best is not None:
                out.append((best[1], best[2]))
                best = None
            prev_v = v
        prev = q
        t += step
    return out


# ---------------------------------------------------------------- 5 the stage and its curtain (west side), the orchestra pit
STAGE_X0, STAGE_X1 = HALL[0], -20.0
_stops = queen_stops(0, 0.5, 30.0)
ALIGHT = next((tq, q) for tq, q in _stops if q[0] < TRACK_W - PAIR + 0.5 and q[1] < TRACK_N - 6.0)
CURTAIN_Z = ALIGHT[1][1]                         # the curtain's two halves meet where the queen sets a pawn down
STAGE_Z0, STAGE_Z1 = CURTAIN_Z - 11.0, CURTAIN_Z + 9.0
CURTAIN_X, RAIL_Y = -23.0, STAGE_Y + 10.6
PLEATS, PLEAT_W = 10, 1.05
BUFFET = (HALL[0], -22.0, 3.0, 17.0)             # the dessert buffet in the south-west corner: x0, x1, z0, z1
BUFFET_HEDGE = 3.0                               # tall cake stands along its east edge: no jumping on from a passing queen
BUFFET_TOP = STAGE_Y
PIT = (HALL[0], -20.0, BUFFET[3], STAGE_Z0)       # the orchestra pit between them, at the floor
BASSES = ((CURTAIN_X, BUFFET[3] + 2.0), (CURTAIN_X, STAGE_Z0 - 2.0))   # double basses beside the curtain's line
BASS_TOP = STAGE_Y + 2.6
CURTAIN_OPEN_S = 16.0                            # where the south half's leading pleat stops, over the buffet
CURTAIN_CYCLE = (6.0, 5.0, 2.0, 2.0)             # shut, opening, open, closing (s)
CURTAIN_PHASE = 0.0
HANG_OVER = BASS_TOP + 0.6                       # a pawn on the curtain this high clears the basses


def curtain_open(t):
    shut, opening, held, closing = CURTAIN_CYCLE
    u = (t - CURTAIN_PHASE) % sum(CURTAIN_CYCLE)
    if u < shut:
        return 0.0
    if u < shut + opening:
        return ease((u - shut) / opening)
    if u < shut + opening + held:
        return 1.0
    return 1.0 - ease((u - shut - opening - held) / closing)


def pleat_z(side, k, t):
    # z of pleat k of the north (+1) or south (-1) half of the curtain.
    closed = CURTAIN_Z + side * (0.55 + PLEAT_W * k)
    opened = (STAGE_Z1 + 0.5 - 0.3 * k) if side > 0 else (CURTAIN_OPEN_S - 0.3 * k)
    return closed + (opened - closed) * curtain_open(t)


# ---------------------------------------------------------------- 6 the chocolate fountain on the buffet
FOUNTAIN = (-26.5, 9.0)
BOWL_R, BOWL_TOP = 5.0, BUFFET_TOP + 0.6
TIER_R = (4.0, 3.0, 2.0, 1.1)
TIER_Y = tuple(BUFFET_TOP + y for y in (2.4, 4.8, 7.2, 9.0))
FLOW = 0.4                                       # m/s down the curtains of chocolate
DOME_FLOW = 0.8                                  # m/s outward on the tiers
TIER_BASE = [BOWL_TOP] + [TIER_Y[k - 1] + 0.6 * (TIER_R[k - 1] - TIER_R[k]) / (TIER_R[k - 1] - 0.55) for k in range(1, 4)]
CROWN_TOP = TIER_Y[3] + 0.6
LEAP_TO_LOFT = (FOUNTAIN[1] - TIER_R[3]) - SCREEN_Z   # from the top tier's south edge to the loft's face

# ---------------------------------------------------------------- 7 the chandeliers: two lines from the loft to the head table
LINES_X = (-2.6, 2.6)
ROPE_L, ROPE_SWING, ROPE_PERIOD = 6.5, 40.0, 5.0
ROPE_END = ROPE_L * math.sin(math.radians(ROPE_SWING))
WHEEL_AT, WHEEL_R = 4.0, 1.6
HANDOVERS = ((0.8, 0.5, "건네받기"), (0.6, 1.0, "떨어지며 잡기"), (0.8, 0.5, "건네받기"), (1.6, 0.5, "날아서 잡기"), (0.8, 0.5, "건네받기"))
ROPE_ALONG = [0.35 + ROPE_END]                    # from the loft's edge
ROPE_LOW = [LOFT_Y + 0.55]
for gap, drop, _ in HANDOVERS:
    ROPE_ALONG.append(ROPE_ALONG[-1] + 2.0 * ROPE_END + gap)
    ROPE_LOW.append(ROPE_LOW[-1] - drop)
ROPE_PIVOT_Y = [low + ROPE_L * math.cos(math.radians(ROPE_SWING)) for low in ROPE_LOW]
CHANDELIERS = len(ROPE_ALONG)
LANE_PHASE = tuple(tuple(((k + lane) % 2) * ROPE_PERIOD / 2.0 for k in range(CHANDELIERS)) for lane in (0, 1))


def rope_pivot(lane, k):
    return (LINES_X[lane], ROPE_PIVOT_Y[k], SCREEN_Z + ROPE_ALONG[k])


def rope_angle(lane, k, t):
    return ROPE_SWING * math.sin(2.0 * math.pi * (t + LANE_PHASE[lane][k]) / ROPE_PERIOD)


def rope_point(lane, k, t, s):
    px, py, pz = rope_pivot(lane, k)
    a = math.radians(rope_angle(lane, k, t))
    return (px, py - s * math.cos(a), pz + s * math.sin(a))


# ---------------------------------------------------------------- 8 the champagne tower in the middle of the head table, the balcony
LEVELS, GLASS_PITCH, LEVEL_H = 4, 1.9, 1.5
LAND_Z = SCREEN_Z + ROPE_ALONG[-1] + ROPE_END + 0.25
TOWER_Z = LAND_Z + 0.3 + (LEVELS - 2) / 2.0 * GLASS_PITCH + 0.95
TOWER = (0.0, TOWER_Z)
PEDESTAL = (-5.0, 5.0, TOWER_Z - 5.0, TOWER_Z + 5.0)    # the middle of the head table that carries it (skirted: not to be climbed)
HEAD_TABLES = ((-24.0, -6.0), (6.0, 24.0))
HEAD_Z = (TOWER_Z - 3.5, TOWER_Z + 2.5)
TOWER_TOP = HEAD_TOP + LEVELS * LEVEL_H
POURS_EVERY, POUR_FOR, WAVE = 5.0, 1.2, 0.45
BALCONY = (-6.0, 6.0, TOWER_Z + 0.95, HALL[3])
BALCONY_Y = TOWER_TOP
EXIT = (0.0, BALCONY_Y, HALL[3])


def overflow(k, t, first=1.0):
    # Level k (0 = the bottom) of the tower is running over at time t.
    u = (t - first) % POURS_EVERY
    start = 0.2 + WAVE * (LEVELS - 1 - k)
    return start <= u < start + 1.4


CHECKPOINTS = (("입구", (0.0, FLOOR, 2.0)), ("긴 식탁", (RUNNER_X, TABLE, 20.0)), ("단", (18.0, DAIS_Y, 58.0)), ("무대", (-24.0, STAGE_Y, CURTAIN_Z + 2.0)),
               ("뷔페", (-23.5, BUFFET_TOP, 15.5)), ("천장 회랑", (-20.0, LOFT_Y, 3.0)), ("왕의 식탁", (-4.4, HEAD_TOP, TOWER_Z)),
               ("왕의 발코니", (0.0, BALCONY_Y, BALCONY[2] + 3.0)))


# ---------------------------------------------------------------- the way (x, y, z, what, how it is travelled)
def the_way():
    way = [(0.0, FLOOR, 0.5, "입구", "run"), (20.0, FLOOR, 3.0, "배식실", "run"), (COUNTER[0] + 1.0, TRAY_Y, 8.0, "카운터", "run"),
           (TRAY_X, TRAY_Y, 9.0, "쟁반", "ride"), (TRAY_X, STACK_TOP, 15.0, "접시 탑 위", "climb"), (TRAY_X, STACK_TOP, 21.0, "", "ride"),
           (RUNNER_X, TABLE, 23.0, "긴 식탁", "run")]
    way += [(RUNNER_X, TABLE, PLATES[-1] + 2.0, "식탁보", "run"), (CLOCHES[0][0], TABLE, CLOCHES[0][1] + CLOCHE_R + 0.3, "은 뚜껑", "run"),
            (CLOCHES[0][0], TABLE + 0.96, CLOCHES[0][1] + CLOCHE_R + 0.3, "", "climb"), (CLOCHES[0][0], DAIS_Y + 0.6, CLOCHES[0][1] + CLOCHE_R + 0.3, "", "ride"),
            (CLOCHES[0][0], DAIS_Y, DAIS_Z + 0.8, "단", "run"), (BOARD_X, DAIS_Y, DAIS_Z + 0.4, "왈츠", "run")]
    t0 = 0.0
    board = couple(0, t0)[1]
    way.append((board[0], SHOULDER_Y, board[1], "여왕의 어깨", "climb"))
    n = 40
    for i in range(1, n + 1):
        q = couple(0, t0 + (ALIGHT[0] - t0) * i / n)[1]
        way.append((q[0], SHOULDER_Y, q[1], "", "ride"))
    way += [(CURTAIN_X + 0.6, STAGE_Y, CURTAIN_Z - 0.6, "무대 막", "run"), (CURTAIN_X + 0.3, HANG_OVER + 0.3, CURTAIN_Z - 0.6, "", "climb"),
            (CURTAIN_X + 0.3, HANG_OVER + 0.3, CURTAIN_OPEN_S, "", "ride"), (CURTAIN_X - 0.6, BUFFET_TOP, CURTAIN_OPEN_S - 0.5, "뷔페", "run")]
    f = FOUNTAIN
    way += [(f[0] + 2.0, BOWL_TOP, f[1] + BOWL_R - 0.4, "초콜릿 분수", "run")]
    for k in range(4):
        r = TIER_R[k] + 0.3
        way += [(f[0], TIER_BASE[k], f[1] + r, "", "run"), (f[0], TIER_Y[k], f[1] + r, "", "climb")]
    way += [(f[0], TIER_Y[3] + 0.5, f[1] - TIER_R[3] + 0.3, "분수 꼭대기", "run"), (f[0], LOFT_Y, SCREEN_Z - 0.5, "천장 회랑", "climb"),
            (LINES_X[0], LOFT_Y, SCREEN_Z - 0.3, "", "run")]
    for k in range(CHANDELIERS):
        way.append((LINES_X[0], ROPE_LOW[k] - 1.15, SCREEN_Z + ROPE_ALONG[k] + ROPE_END, "샹들리에 %d" % (k + 1), "swing"))
    way.append((LINES_X[0] + 0.4, HEAD_TOP + LEVEL_H, LAND_Z + 0.3, "샴페인 탑", "swing"))
    for k in range(1, LEVELS):
        z = TOWER_Z - (LEVELS - 1 - k) / 2.0 * GLASS_PITCH - 0.95 - 0.3
        way.append((LINES_X[0] + 0.4 - (0.95 if k % 2 else 0.0), HEAD_TOP + LEVEL_H * (k + 1), z, "%d층" % (k + 1), "climb"))
    way += [(0.0, BALCONY_Y, BALCONY[2] + 0.5, "발코니", "run"), (EXIT[0], EXIT[1], EXIT[2], "출구", "run")]
    return way


WAY = the_way()


def way_length():
    total = level = 0.0
    for a, b in zip(WAY, WAY[1:]):
        flat = math.hypot(b[0] - a[0], b[2] - a[2])
        level += flat
        total += math.hypot(flat, b[1] - a[1])
    return total, level


PITCHES = (
    ("웨이터 — 접시 탑", STACK_TOP - TRAY_Y, 0.0),
    ("은 뚜껑 — 띠", 0.96, 0.0),
    ("여왕 — 뛰어 붙어서 어깨까지", 1.2, 0.0),
    ("무대 막 — 베이스 위로", HANG_OVER + 0.3 - STAGE_Y, 0.0),
    ("초콜릿 분수 — 한 층(흐름을 거슬러)", 2.2, 2.2 * FLOW / (CLIMB_UP - FLOW)),
    ("분수 꼭대기 → 회랑(뛰어 붙어서)", LOFT_Y - CROWN_TOP, 0.0),
    ("샴페인 탑 — 한 층", LEVEL_H, 0.0),
)


def cost(metres, extra):
    pawn = (metres / CLIMB_UP + extra) / STAMINA
    rook = (metres / (CLIMB_UP * ROOK) + extra / ROOK) / STAMINA
    return pawn, rook


def fountain_tier_time(h, rook=False):
    up = CLIMB_UP * (ROOK if rook else 1.0) - FLOW
    return h / up if up > 0 else float("inf")


RIDE = ALIGHT[0]
CURTAIN_RIDE = CURTAIN_CYCLE[1]
CHANDELIER_RIDE = (CHANDELIERS - 0.5) * ROPE_PERIOD / 2.0
BUDGET = (
    ("입구 · 배식실", 30.0, 30.0 / RUN, 0.0, 0.0),
    ("① 웨이터 행렬", 15.0, (STACK_TOP - TRAY_Y) / CLIMB_UP + 3.5, WAITER_GAP / WAITER_SPEED / 2.0, 0.3 * 12.0),
    ("② 식탁보 빼기", TABLE_Z1 - 23.0, (TABLE_Z1 - 23.0) / RUN, TRICK[1] * 0.7, 0.35 * 16.0),
    ("③ 은 뚜껑", 4.0, 0.96 / CLIMB_UP + CLOCHE_CYCLE[1] + 1.0, sum(CLOCHE_CYCLE) / 2.0, 0.2 * sum(CLOCHE_CYCLE)),
    ("④ 왈츠", 24.0 + WALTZ_SPEED * RIDE, 24.0 / RUN + 1.5 + RIDE, STOP / WALTZ_SPEED / 2.0, 0.3 * 22.0),
    ("⑤ 무대 막", 22.0, (HANG_OVER + 0.3 - STAGE_Y) / CLIMB_UP + CURTAIN_RIDE + 1.0, sum(CURTAIN_CYCLE) * 0.35, 0.25 * 16.0),
    ("⑥ 초콜릿 분수", 14.0, sum(fountain_tier_time(TIER_Y[k] - TIER_BASE[k]) for k in range(4)) + 4 * 0.8 + (LOFT_Y - CROWN_TOP) / CLIMB_UP + 1.0, 0.0, 0.3 * 10.0),
    ("⑦ 샹들리에", 60.0, 22.0 / RUN + CHANDELIER_RIDE, 2.5, 0.45 * 14.0),
    ("⑧ 샴페인 탑 · 발코니", 16.0, (LEVELS - 1) * LEVEL_H / CLIMB_UP + 1.6 + 6.0 / RUN, 2.5, 0.3 * 5.0),
)
STAIR_HALL, CROWD = 157.0, 1.12


def budget_total():
    return sum(row[2] + row[3] + row[4] for row in BUDGET)


def clock(seconds):
    return "%d분 %02d초" % (int(seconds) // 60, int(round(seconds)) % 60)


def report():
    total, level = way_length()
    print("the way: %.0f m (%.0f m level), %.1f m climbed or carried up" % (total, level, sum(max(0.0, b[1] - a[1]) for a, b in zip(WAY, WAY[1:]))))
    print("hall x %.0f .. %.0f, z %.0f .. %.0f; loft %.1f; dais from z %.1f at %.1f; head table %.1f" % (HALL[0], HALL[1], HALL[2], HALL[3], LOFT_Y, DAIS_Z, DAIS_Y, HEAD_TOP))
    print("waiters: %d, %.1f m apart, %.1f m/s; tray past the counter every %.1f s" % (WAITERS, WAITER_GAP, WAITER_SPEED, WAITER_GAP / WAITER_SPEED))
    print("waltz: track x %.1f .. %.1f, z %.1f .. %.1f, corners r %.2f, %.1f m round (%d stops); a queen stops every %.2f s" % (
        TRACK_W, TRACK_E, TRACK_S, TRACK_N, TRACK_R, TRACK_LEN, COUPLES, STOP / WALTZ_SPEED))
    b = couple(0, 0.0)[1]
    print("   boarding: the queen at (%.2f, %.2f), %.2f from the dais's face; she sets down at (%.2f, %.2f) after %.1f s" % (
        b[0], b[1], DAIS_Z - b[1] - GOWN_SHOULDER, ALIGHT[1][0], ALIGHT[1][1], ALIGHT[0]))
    print("   her stops on the way: %s" % ", ".join("%.1f s (%.1f, %.1f)" % (tq, q[0], q[1]) for tq, q in _stops[:5]))
    near = 99.0
    for i in range(COUPLES):
        for j in range(i + 1, COUPLES):
            for n in range(0, 100):
                ci, cj = couple(i, n * 0.1), couple(j, n * 0.1)
                for p in (ci[1], ci[2]):
                    for q in (cj[1], cj[2]):
                        near = min(near, dist(p, q) - 2 * GOWN_FOOT)
    print("   gowns of different couples never nearer than %.2f m" % near)
    print("stage z %.1f .. %.1f, the curtain meets at z %.1f; it opens %.1f m in %.1f s; basses top %.1f, hang over %.1f" % (
        STAGE_Z0, STAGE_Z1, CURTAIN_Z, CURTAIN_Z - 0.55 - CURTAIN_OPEN_S, CURTAIN_CYCLE[1], BASS_TOP, HANG_OVER))
    print("fountain at (%.1f, %.1f): tiers %s, crown %.1f; leap from the top to the loft %.2f m, up %.1f" % (
        FOUNTAIN[0], FOUNTAIN[1], " ".join("%.1f" % y for y in TIER_Y), CROWN_TOP, LEAP_TO_LOFT, LOFT_Y - CROWN_TOP))
    for k in range(4):
        h = TIER_Y[k] - TIER_BASE[k]
        print("   curtain %d: %.1f m, %.1f s pawn, %.1f s rook" % (k + 1, h, fountain_tier_time(h), fountain_tier_time(h, True)))
    print("chandeliers: pivots z %s; ends %s; pivots y %s" % (" ".join("%.1f" % (SCREEN_Z + a) for a in ROPE_ALONG), " ".join("%.2f" % y for y in ROPE_LOW),
                                                         " ".join("%.1f" % y for y in ROPE_PIVOT_Y)))
    print("   lands at z %.1f on the tower's first level (%.1f); tower at z %.2f, top %.1f; balcony z %.1f .. %.1f" % (
        LAND_Z, HEAD_TOP + LEVEL_H, TOWER_Z, TOWER_TOP, BALCONY[2], BALCONY[3]))
    for name, metres, extra in PITCHES:
        pawn, rook = cost(metres, extra)
        print("  %-28s %.1f m  pawn %3.0f%%  rook %3.0f%%" % (name, metres, pawn * 100, rook * 100))
    for row in BUDGET:
        print("  %-14s %5.1f m  move %5.1f  wait %4.1f  fail %4.1f  = %5.1f" % (row[0], row[1], row[2], row[3], row[4], row[2] + row[3] + row[4]))
    t = budget_total()
    print("  total %.0f s (%s), with twelve players %.0f s (%s); the stair hall %s and %s" % (
        t, clock(t), t * CROWD, clock(t * CROWD), clock(STAIR_HALL), clock(STAIR_HALL * CROWD)))


# ---------------------------------------------------------------- the drawing
INK, PAPER, ORANGE, PURPLE, OLIVE, GOLD, CREAMC, WALNUT, RED, SLATE, DARK, GREY, CHOC, VELVET = (
    "#2b2622", "#fbf6ea", "#e8853a", "#8a5cc2", "#6f8a4b", "#c9972e", "#f3e6c8", "#8a5a36", "#c8472f", "#8fa0ad", "#3a3531", "#6b6258",
    "#6b3a1f", "#9b2335")


def draw(path):
    out = []

    def w(s):
        out.append(s)

    def text(x, y, s, size=12, anchor="start", weight="normal", fill=INK):
        w('<text x="%.1f" y="%.1f" font-size="%d" text-anchor="%s" font-weight="%s" fill="%s">%s</text>' % (x, y, size, anchor, weight, fill, s))

    def plan(X0, Y0, S, win, name):
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

        def poly(points, stroke, width, extra=""):
            w('<polyline points="%s" fill="none" stroke="%s" stroke-width="%.1f" %s/>' % (" ".join("%.1f,%.1f" % (px(p[0]), pz(p[1])) for p in points), stroke, width, extra))

        fat = S / 6.0
        w('<clipPath id="%s"><rect x="%.1f" y="%.1f" width="%.1f" height="%.1f"/></clipPath>' % (name, X0, Y0, (win[1] - win[0]) * S, (win[3] - win[2]) * S))
        w('<g clip-path="url(#%s)">' % name)
        rect(HALL[0], HALL[1], HALL[2], HALL[3], "#e9e2d2", WALNUT, 3.0)
        for i in range(int((TABLE_X0 - 1.6 - STAGE_X1) // 3.0)):
            for j in range(int((DAIS_Z - SCREEN_Z) // 3.0)):
                if (i + j) % 2 == 0:
                    rect(STAGE_X1 + i * 3.0, STAGE_X1 + i * 3.0 + 3.0, SCREEN_Z + j * 3.0, SCREEN_Z + j * 3.0 + 3.0, "#ddd3bd", "none", 0)
        rect(BUFFET[1], HALL[1], 0.0, SCREEN_Z, "#e0cb98", "none", 0, 'fill-opacity="0.55"')
        rect(13.0, LANE[0], SCREEN_Z, TABLE_Z0, "#e0cb98", "none", 0, 'fill-opacity="0.55"')
        poly(SCREENS, WALNUT, 3.0)
        rect(LANE[0], LANE[1], 0.0, WAITER_Z1, "#efe6d4", "none", 0)
        rect(COUNTER[0], COUNTER[1], COUNTER[2], COUNTER[3], "#c9a27c", WALNUT, 1.5)
        for k in range(WAITERS):
            z = waiter_z(k, 0.0)
            if z < WAITER_Z1:
                circle((WAITER_X, z), 1.1, DARK, INK, 1.0)
                circle((TRAY_X, z), TRAY_R, "#d9dde0", SLATE, 1.0)
                circle((TRAY_X, z), STACK_R, "#ffffff", GOLD, 1.5)
        rect(TABLE_X0, TABLE_X1, TABLE_Z0, TABLE_Z1, CREAMC, WALNUT, 2.0)
        rect(RUNNER_X - RUNNER, RUNNER_X + RUNNER, TABLE_Z0 + 1.0, PLATES[-1] + 2.0, OLIVE, "none", 0, 'opacity="0.7"')
        for z in PLATES:
            circle((RUNNER_X, z), PLATE_R, "#ffffff", GOLD, 1.5)
        for z in GUESTS:
            circle((TABLE_X0 - 1.0, z), 1.0, "#cfd6dc", SLATE, 1.0)
        circle((MAGICIAN[0], MAGICIAN[2]), 1.5, "#d3b9ea", PURPLE, 2.0)
        for c in CLOCHES:
            circle(c, CLOCHE_R, "#e8ebee", SLATE, 2.0)
        rect(STAGE_X0, STAGE_X1, STAGE_Z0, STAGE_Z1, "#efe2bf", WALNUT, 2.0)
        rect(PIT[0], PIT[1], PIT[2], PIT[3], "#d8cfbc", WALNUT, 1.0)
        for b in BASSES:
            circle(b, 0.7, "#8a5a36", INK, 1.0)
        rect(BUFFET[0], BUFFET[1], BUFFET[2], BUFFET[3], CREAMC, WALNUT, 2.0)
        circle(FOUNTAIN, BOWL_R, "#d9dde0", SLATE, 1.5)
        for r in TIER_R:
            circle(FOUNTAIN, r, "#8a5a36" if r > 1.5 else CHOC, CHOC, 1.2)
        line((CURTAIN_X, CURTAIN_OPEN_S - 3.0), (CURTAIN_X, STAGE_Z1 + 0.5), GOLD, 1.6)
        for side in (-1, 1):
            for k in range(PLEATS):
                z = pleat_z(side, k, 0.0)
                rect(CURTAIN_X - 0.25, CURTAIN_X + 0.25, z - 0.5, z + 0.5, VELVET if k % 2 else "#c0394f", "none", 0)
        rect(HALL[0], HALL[1], DAIS_Z, HALL[3], "#e6d3a3", WALNUT, 2.0)
        for x0, x1 in HEAD_TABLES:
            rect(x0, x1, HEAD_Z[0], HEAD_Z[1], CREAMC, WALNUT, 1.5)
        rect(PEDESTAL[0], PEDESTAL[1], PEDESTAL[2], PEDESTAL[3], CREAMC, WALNUT, 1.5)
        for k in range(LEVELS):
            n = LEVELS - k
            for i in range(n):
                for j in range(n):
                    circle(((i - (n - 1) / 2.0) * GLASS_PITCH, TOWER_Z + (j - (n - 1) / 2.0) * GLASS_PITCH), 0.95, "#f6e7b4" if k < LEVELS - 1 else GOLD, GOLD, 0.8)
        rect(BALCONY[0], BALCONY[1], BALCONY[2], BALCONY[3], "#efe2bf", WALNUT, 2.0, 'fill-opacity="0.8"')
        poly([track_point(TRACK_LEN * i / 240.0) for i in range(241)], PURPLE, 1.0, 'stroke-dasharray="3 3"')
        for i in range(COUPLES):
            mid, q, k, turn = couple(i, 0.0)
            line(q, k, GOLD, 2.0 * fat)
            circle(k, GOWN_FOOT, "#cfd6dc", SLATE, 1.0)
            circle(q, GOWN_FOOT, "#f6dcc6", ORANGE, 2.0 * fat)
        for lane in (0, 1):
            x = LINES_X[lane]
            line((x, SCREEN_Z), (x, LAND_Z), PURPLE, 1.0, 'stroke-dasharray="5 3"')
            for k in range(CHANDELIERS):
                z = SCREEN_Z + ROPE_ALONG[k]
                line((x, z - ROPE_END), (x, z + ROPE_END), PURPLE, 2.6 * fat)
                circle((x, z), WHEEL_R, "#f7e3a6", ORANGE, 2.0 * fat, 'fill-opacity="0.85"')
        poly([(p[0], p[2]) for p in WAY], DARK, 1.8, 'stroke-dasharray="7 4"')
        for cp_name, at in CHECKPOINTS:
            w('<path d="M %.1f %.1f v -13 l 9 4 l -9 4" fill="#3f8f5a" stroke="%s" stroke-width="1"/>' % (px(at[0]) + 1.2 * S, pz(at[2]) + 4, INK))
        w('</g>')
        return px, pz

    W, H = 1460, 2020
    w('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" font-family="Malgun Gothic, Apple SD Gothic Neo, sans-serif">' % (W, H))
    w('<rect width="%d" height="%d" fill="%s"/>' % (W, H, PAPER))
    text(34, 34, "대연회장 레벨 디자인 초안 v0.4 — 연회와 무도회가 한창인 홀, 한 줄 길", 19, "start", "bold")
    text(34, 56, "위에서 본 홀, 가운데를 잘라 서쪽을 본 모습, 길을 펴서 본 높이. 보라 = 움직이는 것, 점선 = 길(갈림 없음). 단위 m.", 12)

    WIN = (HALL[0] - 2.0, HALL[1] + 2.0, -3.0, HALL[3] + 3.0)
    S = 9.6
    text(34, 86, "위에서 본 대연회장 (%.0f × %.0f m, 북쪽이 위)" % (HALL[1] - HALL[0], HALL[3]), 13, "start", "bold")
    px, pz = plan(34.0, 96.0, S, WIN, "hall")
    w('<path d="M %.1f %.1f l -7 13 l 14 0 z" fill="%s"/>' % (px(0), pz(0) - 2, DARK))
    text(px(0) + 10, pz(-1.8), "입구", 11, "start", "bold")
    marks = (("①", 30.6, 20.0), ("②", 20.0, 40.0), ("③", 20.0, 49.0), ("④", -9.0, 32.0), ("⑤", -26.0, CURTAIN_Z + 6.0), ("⑥", -26.5, 15.6),
             ("⑦", 0.0, 30.0), ("⑧", 0.0, TOWER_Z - 6.0))
    for mark, mx, mz in marks:
        w('<circle cx="%.1f" cy="%.1f" r="11" fill="%s" stroke="%s" stroke-width="1.2"/>' % (px(mx), pz(mz), PAPER, RED))
        text(px(mx), pz(mz) + 5, mark, 16, "middle", "bold", RED)
    for label, lx, lz, anchor in (("배식실 · 칸막이 통로 0", 0.0, 3.0, "middle"), ("긴 연회 식탁 4.4", 20.0, 36.0, "middle"), ("웨이터 길", 29.0, 30.0, "middle"),
                                  ("무대 7.4", -26.0, CURTAIN_Z - 6.0, "middle"), ("악단석(바닥)", -26.0, (PIT[2] + PIT[3]) / 2.0, "middle"),
                                  ("디저트 뷔페 7.4", -26.5, BUFFET[2] + 0.8, "middle"), ("무도장 0", -2.0, 20.0, "middle"), ("왕의 단 10.4", -18.0, DAIS_Z + 2.5, "middle"),
                                  ("왕의 식탁 14.8", 15.0, HEAD_Z[1] + 1.4, "middle"), ("왕의 발코니 %.1f" % BALCONY_Y, 0.0, BALCONY[3] - 1.4, "middle"),
                                  ("마술사", MAGICIAN[0], MAGICIAN[2] - 2.6, "middle")):
        text(px(lx), pz(lz), label, 11, anchor, "bold", WALNUT)

    RX = 690.0
    text(RX, 86, "길의 순서 — 갈림 없이 한 줄", 13, "start", "bold")
    listed = (("입구 → 배식실", "칸막이 통로를 따라 동쪽 배식실로 (무도장은 칸막이로 막혀 있다)"),
              ("① 웨이터 행렬", "카운터에서 쟁반에 올라타 접시 탑을 오르고, 긴 식탁 위로 내린다 · → 4.4"),
              ("② 식탁보 빼기", "긴 식탁을 따라 북쪽으로 %.0f m · 북이 울리면 접시 위로" % (TABLE_Z1 - TABLE_Z0)),
              ("③ 은 뚜껑", "식탁 북쪽 끝, 단 앞 · 띠에 매달려 들려 왕의 단으로 · → 10.4"),
              ("④ 왈츠", "단 끝에서 여왕의 드레스로 뛰어 붙어 어깨로 · 춤에 실려 서쪽 무대로 · → 7.4"),
              ("⑤ 무대 막", "열리는 막에 매달려 악단석 너머로 · 베이스보다 높이"),
              ("⑥ 초콜릿 분수", "흐름을 거슬러 네 층 · 꼭대기에서 천장 회랑으로 뛰어 붙는다 · → 20.0"),
              ("⑦ 샹들리에", "회랑에서 무도장 위로, 매달려 %d개를 건너 왕의 식탁으로" % CHANDELIERS),
              ("⑧ 샴페인 탑", "넘치는 층을 피해 꼭대기로, 왕의 발코니 · 출구 %.1f" % BALCONY_Y))
    for i, (head, body) in enumerate(listed):
        y = 110 + i * 29
        text(RX, y, head, 12, "start", "bold", RED if i else INK)
        text(RX + 120, y, body, 11, "start", "normal", "#4a433c")
    text(RX, 110 + 9 * 29 + 4, "길은 홀을 세 번 지난다: 동쪽 식탁으로 북 → 단 앞에서 서쪽으로, 무대를 따라 남 → 공중으로 다시 북.", 11, "start", "bold", DARK)
    text(RX, 110 + 9 * 29 + 22, "무도장 · 왕의 식탁 · 뷔페는 아래에서 오를 수 없다(칸막이 · 손님 · 대리석 · 식탁보 치마). 지름길이 없다.", 11, "start", "normal", GREY)

    SS = 9.4
    SX, SY = RX + 40.0, 470.0 + 37.0 * SS
    text(RX, 440, "가운데(x = 0)에서 잘라 서쪽을 본 모습 — 왼쪽이 남(입구), 오른쪽이 북(왕의 단)", 13, "start", "bold")

    def cut(z, y):
        return SX + z * SS, SY - y * SS

    def slab(z0, z1, y0, y1, fill, stroke=INK, width=1.0, extra=""):
        a, b = cut(z0, y1), cut(z1, y0)
        w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (a[0], a[1], b[0] - a[0], b[1] - a[1], fill, stroke, width, extra))

    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (RX, 452, 740, 37.0 * SS + 30, INK))
    slab(0, HALL[3], -0.5, 0, "#cfc6b3", "none", 0)
    slab(0, HALL[3], CEILING - 0.4, CEILING, "#cfc6b3", "none", 0)
    slab(LOFT[2], LOFT[3], LOFT_Y - 0.6, LOFT_Y, "#e0cb98", WALNUT, 1.5)
    slab(LOFT[2], LOFT[3], 0, 6.0, "#efe6d4", WALNUT, 0.8, 'fill-opacity="0.6"')
    slab(BUFFET[2], BUFFET[3], 0, BUFFET_TOP, CREAMC, WALNUT, 1.0, 'fill-opacity="0.7"')
    slab(FOUNTAIN[1] - BOWL_R, FOUNTAIN[1] + BOWL_R, BUFFET_TOP, BOWL_TOP, "#d9dde0", SLATE, 1.0)
    for k in range(4):
        slab(FOUNTAIN[1] - TIER_R[k], FOUNTAIN[1] + TIER_R[k], TIER_BASE[k], TIER_Y[k], CHOC, INK, 0.8)
    slab(PIT[2], PIT[3], 0, 0.3, "#d8cfbc", "none", 0)
    for b in BASSES:
        slab(b[1] - 0.6, b[1] + 0.6, 0, BASS_TOP, "#8a5a36", INK, 0.6)
    slab(STAGE_Z0, STAGE_Z1, 0, STAGE_Y, "#efe2bf", WALNUT, 1.5, 'fill-opacity="0.7"')
    slab(CURTAIN_OPEN_S - 3.0, STAGE_Z1 + 0.5, RAIL_Y - 0.2, RAIL_Y, GOLD, "none", 0)
    for side in (-1, 1):
        for k in range(PLEATS):
            z = pleat_z(side, k, 0.0)
            slab(z - 0.5, z + 0.5, STAGE_Y, RAIL_Y, VELVET if k % 2 else "#c0394f", "none", 0, 'fill-opacity="0.55"')
    for i in range(COUPLES):
        q = couple(i, 0.0)[1]
        if q[0] < 0:
            z = q[1]
            p = [cut(z - GOWN_FOOT, 0), cut(z - GOWN_SHOULDER, SHOULDER_Y), cut(z + GOWN_SHOULDER, SHOULDER_Y), cut(z + GOWN_FOOT, 0)]
            w('<polygon points="%s" fill="#f6dcc6" stroke="%s" stroke-width="1.4" fill-opacity="0.8"/>' % (" ".join("%.1f,%.1f" % c for c in p), ORANGE))
    slab(DAIS_Z, HALL[3], 0, DAIS_Y, "#e6d3a3", WALNUT, 1.5)
    slab(PEDESTAL[2], PEDESTAL[3], DAIS_Y, HEAD_TOP, CREAMC, WALNUT, 1.2)
    for k in range(LEVELS):
        n = LEVELS - k
        for j in range(n):
            z = TOWER_Z + (j - (n - 1) / 2.0) * GLASS_PITCH
            slab(z - 0.95, z + 0.95, HEAD_TOP + k * LEVEL_H + 0.6, HEAD_TOP + (k + 1) * LEVEL_H, "#f6e7b4", GOLD, 0.8)
    slab(BALCONY[2], BALCONY[3], BALCONY_Y - 0.6, BALCONY_Y, "#efe2bf", WALNUT, 1.5)
    slab(HALL[3], HALL[3] + 0.8, 0, CEILING, "#b9ae9a", "none", 0)
    for k in range(CHANDELIERS):
        zp = SCREEN_Z + ROPE_ALONG[k]
        ax, ay = cut(zp, ROPE_PIVOT_Y[k])
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="0.8"/>' % (ax, ay, ax, cut(0, CEILING)[1], GREY))
        for sgn in (-1, 1):
            bx, by = cut(zp + sgn * ROPE_END, ROPE_LOW[k])
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1.6"/>' % (ax, ay, bx, by, ORANGE))
            mx, my = ax + (bx - ax) * WHEEL_AT / ROPE_L, ay + (by - ay) * WHEEL_AT / ROPE_L
            w('<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="3.5" fill="#f7e3a6" stroke="%s" stroke-width="1"/>' % (mx, my, WHEEL_R * SS * 0.8, GOLD))
        w('<circle cx="%.1f" cy="%.1f" r="3" fill="%s"/>' % (ax, ay, GOLD))
    a, b = cut(SCREEN_Z, LOFT_Y - MAX_DROP), cut(LAND_Z, LOFT_Y - MAX_DROP)
    w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1.2" stroke-dasharray="6 4"/>' % (a[0], a[1], b[0], b[1], RED))
    text(b[0] - 2, b[1] - 5, "회랑에서 8 m 아래", 10, "end", "bold", RED)
    for label, z, y, anchor in (("천장 회랑 20.0", 0.5, LOFT_Y + 0.8, "start"), ("배식실 (입구)", 0.5, 2.4, "start"), ("⑥ 초콜릿 분수", FOUNTAIN[1], TIER_Y[3] + 2.4, "middle"),
                                ("뷔페 7.4", BUFFET[3] - 0.5, BUFFET_TOP - 1.6, "end"), ("베이스", BASSES[0][1], BASS_TOP + 0.8, "middle"),
                                ("⑤ 무대 · 막", (STAGE_Z0 + STAGE_Z1) / 2.0, RAIL_Y + 0.6, "middle"), ("④ 왈츠 (어깨 7.4)", 24.0, 9.0, "middle"),
                                ("왕의 단 10.4", DAIS_Z + 0.6, DAIS_Y - 1.6, "start"), ("⑧ 샴페인 탑", TOWER_Z - 3.4, HEAD_TOP + 2.6, "end"),
                                ("발코니 · 출구", HALL[3] - 0.5, BALCONY_Y + 1.6, "end"), ("⑦ 샹들리에", 30.0, ROPE_PIVOT_Y[2] + 1.6, "middle")):
        p = cut(z, y)
        text(p[0], p[1], label, 11, anchor, "bold", WALNUT)
    for h in (0, TABLE, STAGE_Y, DAIS_Y, HEAD_TOP, LOFT_Y, CEILING):
        p = cut(0, h)
        text(SX - 4, p[1] + 4, "%.1f" % h, 10, "end", "normal", GREY)
    text(RX + 8, SY + 22, "가까운 쪽(동쪽)의 긴 식탁 · 웨이터 · 은 뚜껑은 이 그림에 없다.", 10, "start", "normal", GREY)

    EX, EY, EH = 80.0, 1460.0, 12.0
    flat = [0.0]
    for a, b in zip(WAY, WAY[1:]):
        flat.append(flat[-1] + math.hypot(b[0] - a[0], b[2] - a[2]))
    ES = 1320.0 / flat[-1]
    prof = [(EX + s * ES, EY - p[1] * EH) for s, p in zip(flat, WAY)]
    text(34, 1170, "길을 펴서 본 높이 — 굵은 주황 = 타고 오르는 곳, 보라 = 실려 가는 곳(쟁반 · 뚜껑 · 여왕 · 막 · 샹들리에)", 13, "start", "bold")
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (EX - 14, EY - 23.0 * EH, 1320 + 44, 23.0 * EH + 16, INK))
    for h in (0, TABLE, STAGE_Y, DAIS_Y, HEAD_TOP, LOFT_Y):
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#cfc6b3" stroke-width="0.8"/>' % (EX - 14, EY - h * EH, EX + 1320 + 30, EY - h * EH))
        text(EX - 20, EY - h * EH + 4, "%.1f" % h, 10, "end")
    for i in range(1, len(WAY)):
        (x0, y0), (x1, y1), how = prof[i - 1], prof[i], WAY[i][4]
        if how == "climb":
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="5"/>' % (x0, y0, x1, y1, ORANGE))
        elif how == "swing":
            w('<path d="M %.1f %.1f Q %.1f %.1f %.1f %.1f" fill="none" stroke="%s" stroke-width="2.4"/>' % (x0, y0, (x0 + x1) / 2, max(y0, y1) + 14, x1, y1, PURPLE))
        elif how == "ride":
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="2.6"/>' % (x0, y0, x1, y1, PURPLE))
        else:
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="2.4"/>' % (x0, y0, x1, y1, DARK))
    index = dict((p[3], i) for i, p in enumerate(WAY) if p[3])
    for key, label, sub, ddx, ddy in (("쟁반", "① 웨이터", "쟁반 · 접시 탑", -20, -40), ("긴 식탁", "② 식탁보 빼기", "긴 식탁 %.0f m" % (TABLE_Z1 - TABLE_Z0), 10, -22),
                                      ("은 뚜껑", "③ 은 뚜껑", "들려 단으로", -30, -48), ("여왕의 어깨", "④ 왈츠", "%.0f초 실려 감" % RIDE, 6, -24),
                                      ("무대 막", "⑤ 무대 막", "막에 실려 남쪽으로", -20, -48), ("초콜릿 분수", "⑥ 초콜릿 분수", "네 층 · 회랑으로", -30, 40),
                                      ("샹들리에 3", "⑦ 샹들리에", "%d개 건너기" % CHANDELIERS, -40, -40), ("샴페인 탑", "⑧ 샴페인 탑", "세 층 · 발코니", -40, 40)):
        at = prof[index[key]]
        text(at[0] + ddx, at[1] + ddy, label, 12, "start", "bold")
        text(at[0] + ddx, at[1] + ddy + 14, sub, 11, "start", "normal", "#4a433c")
    total, level = way_length()
    text(EX + 1320 + 30, EY + 34, "길 약 %.0f m" % total, 11, "end")

    rows = (("입구 · 배식실", "칸막이 통로를 지나 배식실의 카운터로", "달리기"),
            ("① 웨이터 행렬", "웨이터는 멈추지 않는다. 쟁반에 올라 접시 탑을 오르고, 문으로 나가기 전에 식탁으로", "실려 가며 오르기"),
            ("② 식탁보 빼기", "북이 울리면 접시 위로. 천 위에 있으면 남쪽으로 끌려가 식탁 밖(배식실)으로 날아간다", "달리기 · 박자"),
            ("③ 은 뚜껑", "장갑이 손잡이를 쥐면 들린다. 띠에 매달려 단 높이로, 뚜껑 밑에 있으면 갇힌다", "매달려 들려 가기"),
            ("④ 왈츠", "여왕이 단 앞에 멈춘 순간 뛰어 붙는다. 어깨에서 춤에 실려 무대 앞에서 내린다", "뛰어 붙기 · 실려 가기"),
            ("⑤ 무대 막", "닫힌 막에 매달려 베이스보다 높이 오른다. 막이 열리면 악단석 너머 뷔페 위로", "매달려 옆으로"),
            ("⑥ 초콜릿 분수", "흘러내리는 초콜릿을 거슬러 네 층. 꼭대기에서 천장 회랑에 뛰어 붙는다", "거슬러 오르기"),
            ("⑦ 샹들리에", "회랑에서 무도장 위로, 끝에서 만나는 다음 샹들리에로. 아래에서 왈츠가 돈다", "매달려 건너기"),
            ("⑧ 샴페인 탑", "왕의 식탁 한가운데. 넘쳐 내려오는 물결을 피해 세 층, 꼭대기에서 왕의 발코니", "피하며 오르기"))
    ty = 1530.0
    text(34, ty, "구간", 12, "start", "bold")
    text(200, ty, "무슨 일이 벌어지고, 폰은 무엇을 하나", 12, "start", "bold")
    text(1000, ty, "벽타기", 12, "start", "bold")
    for x, label in ((1190, "길 m"), (1250, "이동"), (1310, "기다림"), (1370, "실패"), (1430, "합 (초)")):
        text(x, ty, label, 12, "end", "bold")
    for (name, what, verb), row in zip(rows, BUDGET):
        ty += 27
        w('<line x1="34" y1="%.1f" x2="1430" y2="%.1f" stroke="#d8cfbc" stroke-width="0.8"/>' % (ty - 18, ty - 18))
        text(34, ty, name, 13, "start", "bold")
        text(200, ty, what, 12)
        text(1000, ty, verb, 12, "start", "normal", ORANGE if verb != "달리기" else GREY)
        for x, value in ((1190, "%.0f" % row[1]), (1250, "%.0f" % row[2]), (1310, "%.0f" % row[3]), (1370, "%.0f" % row[4]), (1430, "%.0f" % (row[2] + row[3] + row[4]))):
            text(x, ty, value, 12, "end")
    ty += 27
    w('<line x1="34" y1="%.1f" x2="1430" y2="%.1f" stroke="%s" stroke-width="1.2"/>' % (ty - 18, ty - 18, INK))
    t = budget_total()
    text(34, ty, "합계", 13, "start", "bold")
    text(200, ty, "보통 플레이어 한 명 %s · 열두 명 혼잡 보정(1.12배) %s — 대계단 현관홀은 %s · %s" % (clock(t), clock(t * CROWD), clock(STAIR_HALL), clock(STAIR_HALL * CROWD)), 12, "start", "bold")
    for x, value in ((1190, "%.0f" % sum(r[1] for r in BUDGET)), (1250, "%.0f" % sum(r[2] for r in BUDGET)), (1310, "%.0f" % sum(r[3] for r in BUDGET)),
                     (1370, "%.0f" % sum(r[4] for r in BUDGET)), (1430, "%.0f" % t)):
        text(x, ty, value, 12, "end", "bold")
    ty += 32
    text(34, ty, "셈법은 대계단 기획서와 같다: 이동 + 장치 기다림 + 실패할 확률 × 한 번의 값. 손계산이고 사람이 달려 본 적이 없다.", 12)
    ty += 19
    text(34, ty, "떨어지면: 8 m 넘게는 체크포인트에서 다시(기존 규칙). 무도장 · 악단석 바닥은 떨어지면 끝나는 곳이다 — 왕의 단(또는 무대)에서 다시.", 12)
    w('</svg>')
    open(path, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    report()
    if len(sys.argv) > 1:
        draw(sys.argv[1])
        print("drawn", sys.argv[1])
