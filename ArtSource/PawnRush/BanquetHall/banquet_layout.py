# The grand banquet hall (Pawn Rush 2-4, level design v0.2): the one place its numbers are worked out.
# Section frame: the entry's threshold is the origin, x east, y up, z north (the way in).
#
#   python banquet_layout.py                 prints the layout, the pitches, the time budget and the way's length
#   python banquet_layout.py <plan.svg>      also draws the plan, the height profile and the budget
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
    # The point d from a on the way to b.
    k = d / dist(a, b)
    return (a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)


# ---------------------------------------------------------------- pawn numbers (RagdollTuning.asset, RagdollPawn)
CLIMB_UP, CLIMB_DOWN, CLIMB_SIDE, STAMINA, ROOK = 1.2, 1.6, 0.9, 8.0, 0.7
RUN, SPRINT = 2.5, 3.6
ROPE_REACH, ROPE_JUMP_UP, ROPE_REGRAB = 0.7, 3.5, 0.5

# ---------------------------------------------------------------- levels
KITCHEN, TABLE, STAGE_Y, LOFT_Y = 0.0, 4.4, 7.4, 11.6
WEST_WING = (-15.0, 15.0, 0.0, 115.0)            # x0, x1, z0, z1: the long table, the dance floor, the stage
EAST_WING = (15.0, 77.0, 88.0, 115.0)            # the chandeliers over the soup, the cake
CEILING = 26.0

# 1 the tablecloth: six flowing lanes on the long table's south face
TABLE_X, TABLE_Z0, TABLE_Z1 = 6.0, 16.0, 70.0    # half width, south end, north end
CLOTH_LANES, LANE_WIDTH, CLOTH_HEM = 6, 2.0, 1.6
CLOTH_UP, CLOTH_DOWN = 0.9, 1.2                  # m/s a lane carries a climber up, and down
CLOTH_CYCLE = (3.2, 0.8, 3.2, 0.8)               # up, still, down, still (s); lanes a third of it apart

# 2 the long table: the runner lane and the toasting goblets
RUNNER = 2.5                                     # half width of the lane between the place settings
GOBLETS = (24.0, 32.0, 40.0, 48.0, 56.0, 64.0)   # z of each pair
GOBLET_X, GOBLET_R, GOBLET_TALL, GOBLET_LEAN = 3.4, 1.3, 3.6, 36.0   # foot from the middle, bowl, its height, degrees to the clink
TOAST = (1.9, 0.5, 0.3, 0.3, 0.6)                # upright, lean back, swing in, clink, back (s)
TOAST_WAVE = 0.9                                 # each pair this much after the one before it (s)
RUNNER_JERK = (1.5, 0.4)                         # metres back, over seconds, at each clink

# 3 the knights: four of them hopping their L move round two diamonds of squares; a pawn hangs on and is carried
SQUARE = 3.0
KNIGHT_REST, KNIGHT_FLY, KNIGHT_ARC = 1.5, 1.5, 1.2      # seconds on a square, seconds in the air, metres over the higher square
KNIGHT_TURN = 0.7                                        # of its stand, turning to face the next square
KNIGHT_R, KNIGHT_DRAPE, KNIGHT_TALL = 1.3, 5.2, 7.0      # body radius, top of the caparison (the face to hang on), head
ISLAND = (-6.0, 6.0, 77.2, 83.2)                         # the middle dais, at the table's height
FLOOR_SQUARE = 0.4
#   west diamond: A on the table's end, B on the floor, C on the dais, D on the floor (shared with the east one)
KNIGHT_SQUARES = ((-3.0, TABLE, 67.6), (-6.0, FLOOR_SQUARE, 73.6), (-3.0, TABLE, 79.6), (0.0, FLOOR_SQUARE, 73.6))
KNIGHTS = ((-1, 0.0), (-1, 6.0), (1, 3.0), (1, 9.0))     # side (-1 west, 1 east), seconds ahead on the 12 s round
KNIGHT_ROUND = 4 * (KNIGHT_REST + KNIGHT_FLY)

# 4 the waltz: four couples round the floor, each couple turning the other way three times a lap, so the queen
#   comes to a stand at the dais and again at the stage and swings wide in between
GOWN_FOOT, GOWN_SHOULDER, SHOULDER_Y, HEAD_R = 1.8, 1.3, STAGE_Y, 0.55
PAIR = 1.9                                       # each partner this far from the couple's middle
LOOP_B, LOOP_A = PAIR * 3.0, 6.5                 # half width (x) and half length (z) of the couple's oval
LAP, COUPLES, SPINS = 20.0, 4, 3
REACH_GAP = 0.3                                  # dais edge to gown, shoulder to stage edge, at the stand


def gown(y):
    return GOWN_FOOT - (GOWN_FOOT - GOWN_SHOULDER) * y / SHOULDER_Y


LOOP_Z = ISLAND[3] + REACH_GAP + gown(TABLE) + PAIR + LOOP_A
STAGE = (-13.0, 14.0, LOOP_Z + LOOP_A + PAIR + GOWN_SHOULDER + REACH_GAP, 115.0)

# 5 the harp on the stage: nine strings to climb, plucked in a run; the neck is the walk up to the loft
HARP_Z = STAGE[2] + 5.6
STRINGS = tuple((-8.0 + i, 9.6 + 0.25 * i) for i in range(9))      # x, the string's top
PLUCK_EVERY, PLUCK_STEP, PLUCK_WARN, PLUCK_DEAD = 4.2, 0.3, 0.7, 0.6
NECK = 0.3                                       # the harp neck, a plank over the strings' tops: this thick, its top the walk
LOFT = (1.0, 14.0, STAGE[2] + 1.2, 115.0)

# 6 the great candles on the loft's east edge, one for each line of chandeliers, wax running down them
ROPE_HEAD = 97.0                                 # the way the chandeliers lead, a heading
CANDLE_MID = (12.0, STAGE[2] + 6.4)
CANDLE_R, DISH_R, CANDLE_TOP = 1.0, 2.0, LOFT_Y + 3.2
CANDLES = (add(CANDLE_MID, 2.6, ROPE_HEAD + 90.0), add(CANDLE_MID, -2.6, ROPE_HEAD + 90.0))
WAX_LANES, WAX_EVERY, WAX_WARN, WAX_SPEED = 6, 0.9, 0.7, 2.2

# 7 the chandeliers: two lines of five, each a pendulum (RopeLine) with its wheel of candles two thirds down
ROPE_L, ROPE_SWING, ROPE_PERIOD = 6.5, 40.0, 5.0
ROPE_END = ROPE_L * math.sin(math.radians(ROPE_SWING))               # how far the end swings either way
ROPE_RISE = ROPE_L * (1.0 - math.cos(math.radians(ROPE_SWING)))
WHEEL_AT, WHEEL_R = 4.0, 1.5                                         # down the line, radius (drawn only)
HANDOVERS = ((0.8, 0.5, "건네받기"), (0.6, 1.5, "떨어지며 잡기"), (0.8, 0.5, "건네받기"), (1.6, 0.7, "날아서 잡기"))   # gap, drop
ROPE_FIRST = CANDLE_R + 0.35 + ROPE_END                              # candle's middle to the first pivot
ROPE_ALONG = [ROPE_FIRST]
ROPE_LOW = [CANDLE_TOP + 0.55]                                       # the end's height at either end of its swing
for gap, drop, _ in HANDOVERS:
    ROPE_ALONG.append(ROPE_ALONG[-1] + 2.0 * ROPE_END + gap)
    ROPE_LOW.append(ROPE_LOW[-1] - drop)
ROPE_PIVOT_Y = [low + ROPE_L * math.cos(math.radians(ROPE_SWING)) for low in ROPE_LOW]
ROPE_SPAN = ROPE_ALONG[-1] + ROPE_END                                # candle's middle to where the last one lets go
LANE_PHASE = ((0.0, 2.5, 0.0, 2.5, 0.0), (2.5, 0.0, 2.5, 0.0, 2.5))  # seconds, so neighbours meet end to end

# 8 the cake: five tiers turning against each other, cream stripes to climb and fondant that cannot be held
TIERS = ((8.2, 7.4, 6.0), (6.8, 10.2, -8.0), (5.4, 13.0, 10.0), (4.0, 15.8, -13.0), (2.6, 18.6, 18.0))   # radius, top, deg/s
CREAM = ((5, 40.0), (5, 40.0), (4, 50.0), (4, 50.0), (3, 70.0))                                       # stripes, degrees each
LAND_R = (TIERS[1][0] + TIERS[2][0]) / 2.0                           # where the chandeliers set a pawn down: tier 2's ledge
CAKE = add(CANDLE_MID, ROPE_SPAN + math.sqrt(LAND_R ** 2 - 2.6 ** 2), ROPE_HEAD)
CAKE_TABLE_R, TOPPER = 9.8, (0.7, 20.4)

# 9 the cake knife, cut into the top tier and lying north to the royal balcony
BALCONY_Y = 18.2
KNIFE = ((CAKE[0], CAKE[1] + 1.0), (CAKE[0], WEST_WING[3] + 0.6))
KNIFE_WIDTH, KNIFE_PEEL = 1.0, 12.0                                  # the back to walk on; degrees either side that peel a climber
BALCONY = (CAKE[0] - 6.0, CAKE[0] + 6.0, WEST_WING[3], WEST_WING[3] + 12.0)
EXIT = (CAKE[0], BALCONY_Y, BALCONY[3])

# the soup under the chandeliers: the one place a fall is the end
SOUP = (EAST_WING[0] + 1.0, CAKE[0] - CAKE_TABLE_R - 0.6, 98.0, 114.0)
SOUP_Y = 1.2


# ---------------------------------------------------------------- where the moving things are at time t (seconds)
def cloth_speed(lane, t):
    # Metres a second up (+) or down (-) that lane's cloth is running.
    up, hold, down, hold2 = CLOTH_CYCLE
    u = (t + lane * sum(CLOTH_CYCLE) / 3.0) % sum(CLOTH_CYCLE)
    return CLOTH_UP if u < up else 0.0 if u < up + hold else -CLOTH_DOWN if u < up + hold + down else 0.0


def goblet_lean(pair, t):
    # 0 upright, -1 leant back (the warning), +1 clinking over the runner.
    u = (t - pair * TOAST_WAVE) % sum(TOAST)
    a, b, c, d, e = TOAST
    if u < a:
        return 0.0
    if u < a + b:
        return -(u - a) / b
    if u < a + b + c:
        return -1.0 + 2.0 * (u - a - b) / c
    if u < a + b + c + d:
        return 1.0
    return 1.0 - (u - a - b - c - d) / e


def knight(k, t):
    # (x, y, z, yaw degrees, in the air 0..1 or -1) of knight k: y is the foot of its base.
    side, ahead = KNIGHTS[k]
    u = (t + ahead) % KNIGHT_ROUND
    leg = int(u // (KNIGHT_REST + KNIGHT_FLY))
    v = u - leg * (KNIGHT_REST + KNIGHT_FLY)
    a, b = KNIGHT_SQUARES[leg], KNIGHT_SQUARES[(leg + 1) % 4]
    ax, bx = a[0] * -side, b[0] * -side          # the west diamond as written; the east one mirrored
    yaw = math.degrees(math.atan2(bx - ax, b[2] - a[2]))
    if v < KNIGHT_REST:
        # It lands still facing the way it came, and turns to the next square over the first part of its stand.
        c = KNIGHT_SQUARES[(leg + 3) % 4]
        came = math.degrees(math.atan2(ax - c[0] * -side, a[2] - c[2]))
        k = min(1.0, v / KNIGHT_TURN)
        k = k * k * (3.0 - 2.0 * k)
        return ax, a[1], a[2], came + ((yaw - came + 180.0) % 360.0 - 180.0) * k, -1.0
    s = (v - KNIGHT_REST) / KNIGHT_FLY
    top = max(a[1], b[1]) + KNIGHT_ARC
    y = a[1] + (b[1] - a[1]) * s + (top - (a[1] + b[1]) / 2.0) * 4.0 * s * (1.0 - s)
    return ax + (bx - ax) * s, y, a[2] + (b[2] - a[2]) * s, yaw, s


def couple(i, t):
    # (middle, queen, king, turn degrees) of couple i on the floor: points are (x, z).
    th = 2.0 * math.pi * (t / LAP + i / float(COUPLES))
    mid = (LOOP_B * math.sin(th), LOOP_Z - LOOP_A * math.cos(th))
    off = (-PAIR * math.sin(SPINS * th), -PAIR * math.cos(SPINS * th))
    return mid, (mid[0] + off[0], mid[1] + off[1]), (mid[0] - off[0], mid[1] - off[1]), -math.degrees(SPINS * th)


def pluck(string, t):
    # Seconds since that string was last plucked (0 .. PLUCK_EVERY).
    return (t - string * PLUCK_STEP) % PLUCK_EVERY


def rope_pivot(lane, k):
    p = add(CANDLES[lane], ROPE_ALONG[k], ROPE_HEAD)
    return (p[0], ROPE_PIVOT_Y[k], p[1])


def rope_angle(lane, k, t):
    # Degrees off straight down, positive the way the line leads.
    return ROPE_SWING * math.sin(2.0 * math.pi * (t + LANE_PHASE[lane][k]) / ROPE_PERIOD)


def rope_point(lane, k, t, s):
    # (x, y, z) of the point s metres down chandelier k's line.
    px, py, pz = rope_pivot(lane, k)
    a = math.radians(rope_angle(lane, k, t))
    h = heading(ROPE_HEAD)
    return (px + h[0] * s * math.sin(a), py - s * math.cos(a), pz + h[1] * s * math.sin(a))


def tier_turn(i, t):
    return TIERS[i][2] * t


LAND = tuple(add(c, ROPE_SPAN, ROPE_HEAD) for c in CANDLES)          # where each line sets down on tier 2's ledge

CHECKPOINTS = (("입구", (0.0, KITCHEN, 6.0)), ("식탁 남쪽 끝", (0.0, TABLE, 19.5)), ("식탁 북쪽 끝", (0.0, TABLE, 65.5)),
               ("가운데 단", (0.0, TABLE, 80.2)), ("악단석", (0.0, STAGE_Y, STAGE[2] + 2.2)), ("윗단", (7.0, LOFT_Y, STAGE[2] + 4.0)),
               ("케이크 2단 턱", (LAND[0][0], TIERS[1][1], LAND[0][1])), ("케이크 4단 턱", (CAKE[0] - 3.3, TIERS[3][1], CAKE[1])),
               ("왕의 발코니", (CAKE[0], BALCONY_Y, BALCONY[2] + 3.0)))


# ---------------------------------------------------------------- the way (x, y, z, what, how it is travelled)
def queen_ride():
    # The queen's shoulder from her stand at the dais to her stand at the stage (couple 0, half a lap).
    return [(q[0], SHOULDER_Y, q[1]) for q in (couple(0, LAP * 0.5 * n / 40.0)[1] for n in range(41))]


def the_way():
    way = [(0.0, KITCHEN, 0.0, "입구", "run"), (0.0, KITCHEN, TABLE_Z0 - 0.4, "식탁보 아래", "run"),
           (0.0, TABLE, TABLE_Z0 + 0.4, "식탁 위", "climb"), (-3.0, TABLE, TABLE_Z1 - 4.4, "나이트의 칸", "run")]
    a, b, c = KNIGHT_SQUARES[0], KNIGHT_SQUARES[1], KNIGHT_SQUARES[2]
    way += [(a[0], TABLE + 0.6, a[2] - KNIGHT_R, "나이트에 매달림", "run"), (b[0], FLOOR_SQUARE + 0.6, b[2], "", "ride"),
            (c[0], TABLE + 0.6, c[2], "", "ride"), (0.0, TABLE, ISLAND[3] - 0.4, "가운데 단", "run")]
    ride = queen_ride()
    way += [(ride[0][0], TABLE + 0.4, ride[0][2] - gown(TABLE), "여왕의 드레스", "run"), (ride[0][0], SHOULDER_Y, ride[0][2] - 1.0, "여왕의 어깨", "climb")]
    way += [(p[0], p[1], p[2], "", "ride") for p in ride[1:]]
    way += [(0.0, STAGE_Y, STAGE[2] + 0.6, "악단석", "run"), (STRINGS[5][0], STAGE_Y, HARP_Z - 0.4, "하프 아래", "run"),
            (STRINGS[5][0], STRINGS[5][1] + 0.1, HARP_Z + 0.5, "하프 목", "climb"), (1.6, LOFT_Y, HARP_Z + 1.0, "윗단", "run")]
    k = CANDLES[0]
    foot = toward(k, (LOFT[0], k[1]), CANDLE_R + 0.3)
    way += [(foot[0], LOFT_Y, foot[1], "초 아래", "run"), (k[0], CANDLE_TOP, k[1], "초 위", "climb")]
    for i in range(5):
        e = add(CANDLES[0], ROPE_ALONG[i] + ROPE_END, ROPE_HEAD)
        way.append((e[0], ROPE_LOW[i], e[1], "샹들리에 %d" % (i + 1), "swing"))
    land = LAND[0]
    way.append((land[0], TIERS[1][1], land[1], "케이크 2단 턱", "swing"))
    for i in (2, 3, 4):
        wall = toward(CAKE, land, TIERS[i][0] + 0.25)
        up = toward(CAKE, land, TIERS[i][0] - 0.5)
        way += [(wall[0], TIERS[i - 1][1], wall[1], "", "run"), (up[0], TIERS[i][1], up[1], "%d단 위" % (i + 1), "climb")]
    way += [(KNIFE[0][0], TIERS[4][1], KNIFE[0][1], "칼등", "run"), (KNIFE[1][0], BALCONY_Y, KNIFE[1][1], "발코니", "run"),
            (EXIT[0], BALCONY_Y, EXIT[2], "출구", "run")]
    return way


WAY = the_way()


def way_length():
    total = level = 0.0
    for a, b in zip(WAY, WAY[1:]):
        flat = math.hypot(b[0] - a[0], b[2] - a[2])
        level += flat
        total += math.hypot(flat, b[1] - a[1])
    return total, level


# Every pitch a climber has to make, and what it costs: metres up, then seconds of stamina for a dodge on the move.
# A pawn's stamina is 8 s of moving on a wall or a rope; a rook climbs at 0.7 of the speed for the same cost.
# Hanging still costs nothing, on a wall or on a rope.
PITCHES = (
    ("식탁보 — 가만히 있는 줄로", TABLE, 0.0),
    ("식탁보 — 올라가는 줄에 매달려", 0.0, 0.0),
    ("나이트 — 매달려 실려 감", 0.0, 0.0),
    ("여왕의 드레스", SHOULDER_Y - TABLE, 0.0),
    ("하프 — 짧은 줄", STRINGS[0][1] - STAGE_Y, 0.0),
    ("하프 — 가운데 줄", STRINGS[4][1] - STAGE_Y, 0.0),
    ("하프 — 긴 줄", STRINGS[8][1] - STAGE_Y, 0.0),
    ("초 (촛농 한 번 피함)", CANDLE_TOP - LOFT_Y, 0.6),
    ("샹들리에 — 매달려 실려 감", 0.0, 0.0),
    ("케이크 한 층 (세 번)", TIERS[2][1] - TIERS[1][1], 0.0),
)


def cost(metres, extra):
    pawn = (metres / CLIMB_UP + extra) / STAMINA
    rook = (metres / (CLIMB_UP * ROOK) + extra / ROOK) / STAMINA
    return pawn, rook


def ride_length():
    ride = queen_ride()
    return sum(math.hypot(b[0] - a[0], b[2] - a[2]) for a, b in zip(ride, ride[1:]))


# What an ordinary player spends, the Grand Stair Hall's way of counting (its design v0.3, section 7): the way at a
# run or a climb, the wait for a device, and each failure's chance times what it costs. Seconds.
#   beat, metres, moving, waiting, failures
BUDGET = (
    ("부엌 통로", 16.0, 16.0 / RUN, 0.0, 0.0),
    ("① 식탁보", 5.0, TABLE / (CLIMB_UP + CLOTH_UP) + 1.4, 1.5, 0.3 * 4.0),
    ("② 건배하는 식탁", TABLE_Z1 - TABLE_Z0, (TABLE_Z1 - TABLE_Z0) / RUN, 4.5, 0.4 * 6.0),
    ("③ 나이트의 도약", 17.0, 2.0 * (KNIGHT_REST + KNIGHT_FLY) + 1.4, 3.0, 0.3 * 9.0),
    ("④ 왈츠", ride_length() + 3.0, LAP / 2.0 + 1.7, LAP / COUPLES / 2.0, 0.25 * 5.0 + 0.2 * 23.0),
    ("⑤ 하프", 14.0, 3.2 / CLIMB_UP + 5.6, 1.5, 0.3 * 5.0),
    ("⑥ 큰 촛대", 14.0, 3.2 / CLIMB_UP + 5.5, 0.0, 0.3 * 6.0),
    ("⑦ 샹들리에 다섯", ROPE_SPAN, 5 * ROPE_PERIOD / 2.0 + 0.5, 2.0, 0.5 * 13.0),
    ("⑧ 케이크", 13.0, 3 * (2.8 / CLIMB_UP + 0.45) + 1.7, 6.5, 0.35 * 8.0),
    ("⑨ 칼등과 발코니", dist(KNIFE[0], KNIFE[1]) + 12.0, (dist(KNIFE[0], KNIFE[1]) + 12.0) / RUN + 0.6, 0.0, 0.15 * 12.0),
)
STAIR_HALL, CROWD = 157.0, 1.12                  # the Grand Stair Hall's own total, and its factor for twelve players


def budget_total():
    return sum(row[2] + row[3] + row[4] for row in BUDGET)


def clock(seconds):
    return "%d분 %02d초" % (int(seconds) // 60, int(round(seconds)) % 60)


def report():
    total, level = way_length()
    print("the way: %.0f m (%.0f m level), %.1f m climbed or carried up" % (total, level, sum(max(0.0, b[1] - a[1]) for a, b in zip(WAY, WAY[1:]))))
    print("loop middle z %.2f   stage from z %.2f   harp z %.2f   loft from z %.2f" % (LOOP_Z, STAGE[2], HARP_Z, LOFT[2]))
    print("queen: stands at the dais %.2f from its edge, at the stage %.2f from its edge; half a lap is %.1f m" % (
        couple(0, 0.0)[1][1] - gown(TABLE) - ISLAND[3], STAGE[2] - couple(0, LAP / 2.0)[1][1] - GOWN_SHOULDER, ride_length()))
    print("candles %s  top %.1f" % (", ".join("(%.2f, %.2f)" % c for c in CANDLES), CANDLE_TOP))
    print("chandeliers along the line %s" % " ".join("%.2f" % a for a in ROPE_ALONG))
    print("   end swings %.2f either way, rises %.2f; pivots at y %s; ends at y %s" % (
        ROPE_END, ROPE_RISE, " ".join("%.2f" % y for y in ROPE_PIVOT_Y), " ".join("%.2f" % y for y in ROPE_LOW)))
    print("   fastest the end goes %.1f m/s; span %.1f m; lands %.2f from the cake's middle (ledge %.1f .. %.1f)" % (
        ROPE_L * math.radians(ROPE_SWING) * 2.0 * math.pi / ROPE_PERIOD, ROPE_SPAN, dist(LAND[0], CAKE), TIERS[2][0], TIERS[1][0]))
    print("cake (%.2f, %.2f)  knife %.1f m  balcony x %.1f .. %.1f  exit (%.1f, %.1f, %.1f)" % (
        CAKE[0], CAKE[1], dist(KNIFE[0], KNIFE[1]), BALCONY[0], BALCONY[1], EXIT[0], EXIT[1], EXIT[2]))
    for name, metres, extra in PITCHES:
        pawn, rook = cost(metres, extra)
        print("  %-30s %.1f m  pawn %3.0f%%  rook %3.0f%%" % (name, metres, pawn * 100, rook * 100))
    for i, (r, top, speed) in enumerate(TIERS):
        print("  tier %d: r %.1f top %.1f  %+.0f deg/s  rim %.2f m/s  cream %d x %.0f deg" % (i + 1, r, top, speed, abs(math.radians(speed)) * r, CREAM[i][0], CREAM[i][1]))
    for i in (2, 3, 4):
        rel = abs(TIERS[i][2] - TIERS[i - 1][2])
        print("  from tier %d's ledge tier %d's cream comes every %.1f s and stays %.1f s" % (i, i + 1, 360.0 / CREAM[i][0] / rel, CREAM[i][1] / rel))
    for row in BUDGET:
        print("  %-16s %5.1f m  move %5.1f  wait %4.1f  fail %4.1f  = %5.1f" % (row[0], row[1], row[2], row[3], row[4], row[2] + row[3] + row[4]))
    t = budget_total()
    print("  total %.0f s (%s), with twelve players %.0f s (%s); the stair hall %s and %s" % (
        t, clock(t), t * CROWD, clock(t * CROWD), clock(STAIR_HALL), clock(STAIR_HALL * CROWD)))


# ---------------------------------------------------------------- the drawing
INK, PAPER, ORANGE, PURPLE, OLIVE, GOLD, CREAMC, WALNUT, RED, SLATE, DARK, GREY = (
    "#2b2622", "#fbf6ea", "#e8853a", "#8a5cc2", "#6f8a4b", "#c9972e", "#f3e6c8", "#8a5a36", "#c8472f", "#8fa0ad", "#3a3531", "#6b6258")


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

        def poly(points, stroke, width, extra=""):
            w('<polyline points="%s" fill="none" stroke="%s" stroke-width="%.1f" %s/>' % (" ".join("%.1f,%.1f" % (px(p[0]), pz(p[1])) for p in points), stroke, width, extra))

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
        for wing in (WEST_WING, EAST_WING):
            rect(wing[0], wing[1], wing[2], wing[3], "#e9e2d2", "none", 0)
        # the dance floor: a chessboard of 3 m squares from the table's end to the stage
        for i in range(8):
            for j in range(int((STAGE[2] - TABLE_Z1) / SQUARE) + 1):
                if (i + j) % 2 == 0:
                    z0 = TABLE_Z1 + j * SQUARE
                    rect(-12.0 + i * SQUARE, -9.0 + i * SQUARE, z0, min(z0 + SQUARE, STAGE[2]), "#ddd3bd", "none", 0)
        w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="%.1f" fill="#e7a48f" stroke="%s" stroke-width="1.5"/>' % (
            px(SOUP[0]), pz(SOUP[3]), (SOUP[1] - SOUP[0]) * S, (SOUP[3] - SOUP[2]) * S, 6.0 * S, RED))
        # the long table, its runner, the cloth lanes, the goblets
        rect(-TABLE_X, TABLE_X, TABLE_Z0, TABLE_Z1, CREAMC, WALNUT, 2.0)
        rect(-RUNNER, RUNNER, TABLE_Z0 + 2, TABLE_Z1 - 5.0, OLIVE, "none", 0, 'opacity="0.75"')
        for i in range(CLOTH_LANES):
            x0 = -TABLE_X + i * LANE_WIDTH
            rect(x0, x0 + LANE_WIDTH, TABLE_Z0 - 1.0, TABLE_Z0, PURPLE if i % 3 != 1 else "#b9a2dc", INK, 0.8)
        for z in GOBLETS:
            for s in (-1, 1):
                circle((s * GOBLET_X, z), GOBLET_R, "#d9c6ea", PURPLE, 1.4)
                line((s * (GOBLET_X - GOBLET_R), z), (s * 0.3, z), PURPLE, 1.2, 'stroke-dasharray="3 2"')
        # the dais, the knights' squares and their L moves
        rect(ISLAND[0], ISLAND[1], ISLAND[2], ISLAND[3], CREAMC, WALNUT, 2.0)
        for side in (-1, 1):
            sq = [(p[0] * -side, p[2]) for p in KNIGHT_SQUARES]
            for a, b in zip(sq, sq[1:] + sq[:1]):
                corner = (b[0], a[1]) if abs(b[1] - a[1]) > abs(b[0] - a[0]) else (a[0], b[1])
                poly((a, corner, b), PURPLE, 1.2, 'stroke-dasharray="4 3"')
            for p in sq:
                rect(p[0] - 1.5, p[0] + 1.5, p[1] - 1.5, p[1] + 1.5, "none", PURPLE, 1.4)
        for k in range(4):
            x, y, z, yaw, air = knight(k, 0.0)
            circle((x, z), KNIGHT_R, "#d9c6ea", ORANGE, 3.0 * fat)
        # the waltz: the queen's track, and the four couples as they stand at t = 0
        poly([couple(0, LAP * n / 240.0)[1] for n in range(241)], PURPLE, 1.0, 'stroke-dasharray="2 3"')
        for i in range(COUPLES):
            mid, q, k, turn = couple(i, 0.0)
            line(q, k, GOLD, 3.0 * fat)
            circle(k, GOWN_FOOT, "#cfd6dc", SLATE, 1.2)
            circle(q, GOWN_FOOT, "#f6dcc6", ORANGE, 3.0 * fat)
            circle(q, HEAD_R, GOLD, INK, 0.6)
            circle(k, HEAD_R, GOLD, INK, 0.6)
        # the stage, the harp, the loft, the candles
        rect(STAGE[0], STAGE[1], STAGE[2], STAGE[3], "#efe2bf", WALNUT, 2.0)
        rect(LOFT[0], LOFT[1], LOFT[2], LOFT[3], "#e6d3a3", WALNUT, 2.0)
        rect(STRINGS[0][0] - 0.8, LOFT[0], HARP_Z + 0.3, HARP_Z + 1.9, GOLD, INK, 0.8, 'opacity="0.85"')
        for x, top in STRINGS:
            circle((x, HARP_Z), 0.28, ORANGE, INK, 0.6)
        for c in CANDLES:
            circle(c, DISH_R, "#f0d9a8", GOLD, 1.5)
            circle(c, CANDLE_R, "#fff6dd", ORANGE, 3.0 * fat)
        # the cake on its table
        circle(CAKE, CAKE_TABLE_R, CREAMC, WALNUT, 1.5)
        shades = ("#f7cfc6", "#f9d9d2", "#fbe6df", "#fdf1ec", "#fffaf6")
        for i, (r, top, speed) in enumerate(TIERS):
            circle(CAKE, r, shades[i], "none", 0)
            stripes(CAKE, r, CREAM[i][0], CREAM[i][1], 17.0 * i, 3.0 * fat)
        circle(CAKE, TOPPER[0], GOLD, INK, 0.8)
        # the chandeliers, over all of that: each line's pivots, the sweep of the end, the wheel
        for lane in (0, 1):
            first, last = add(CANDLES[lane], CANDLE_R, ROPE_HEAD), LAND[lane]
            line(first, last, PURPLE, 1.0, 'stroke-dasharray="5 3"')
            for k in range(5):
                p = add(CANDLES[lane], ROPE_ALONG[k], ROPE_HEAD)
                line(add(p, -ROPE_END, ROPE_HEAD), add(p, ROPE_END, ROPE_HEAD), PURPLE, 2.6 * fat)
                circle(p, WHEEL_R, "#f7e3a6", ORANGE, 2.0 * fat, 'fill-opacity="0.92"')
                circle(p, 0.4, GOLD, INK, 0.6)
        # the knife and the balcony
        line(KNIFE[0], KNIFE[1], "#b9c2c9", 7.0 * fat)
        line(KNIFE[0], KNIFE[1], INK, 0.8)
        rect(BALCONY[0], BALCONY[1], BALCONY[2], BALCONY[3], "#efe2bf", WALNUT, 2.0)
        # the way and its checkpoints
        poly([(p[0], p[2]) for p in WAY], DARK, 1.5, 'stroke-dasharray="6 4"')
        for cp_name, at in CHECKPOINTS:
            w('<path d="M %.1f %.1f v -13 l 9 4 l -9 4" fill="#3f8f5a" stroke="%s" stroke-width="1"/>' % (px(at[0]) + 1.2 * S, pz(at[2]) + 4, INK))
        w('</g>')
        w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" stroke="%s" stroke-width="1.5"/>' % (X0, Y0, (win[1] - win[0]) * S, (win[3] - win[2]) * S, INK))
        return px, pz

    def note(px, pz, at, to, lines, anchor="start", fill=INK):
        # A label at `to` (world x, z) with a thin line to what it names at `at`.
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="0.8"/>' % (px(at[0]), pz(at[1]), px(to[0]), pz(to[1]) - 4, GREY))
        w('<circle cx="%.1f" cy="%.1f" r="2" fill="%s"/>' % (px(at[0]), pz(at[1]), GREY))
        for i, s in enumerate(lines):
            text(px(to[0]) + (4 if anchor == "start" else -4), pz(to[1]) + i * 14, s, 12 if i == 0 else 11, anchor, "bold" if i == 0 else "normal", fill if i == 0 else "#4a433c")

    W, H = 1460, 2130
    w('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" font-family="Malgun Gothic, Apple SD Gothic Neo, sans-serif">' % (W, H))
    w('<rect width="%d" height="%d" fill="%s"/>' % (W, H, PAPER))
    text(34, 34, "대연회장 레벨 디자인 초안 v0.2 — 연회의 순서를 따라, 부엌에서 왕의 발코니까지", 19, "start", "bold")
    text(34, 56, "위에서 본 배치(전체 · 무도장 · 동쪽 날개)와 길을 펴서 본 높이. 주황 = 타는 면과 줄, 보라 = 움직이는 것, 청회색 = 못 잡는 면. 단위 m.", 12)

    # the whole hall
    ALL = (WEST_WING[0], EAST_WING[1], 0.0, BALCONY[3])
    text(34, 86, "전체 (ㄱ자 홀)", 13, "start", "bold")
    px, pz = plan(34.0, 96.0, 4.7, ALL, "whole")
    w('<path d="M %.1f %.1f l -6 12 l 12 0 z" fill="%s"/>' % (px(0), pz(0) - 12, DARK))
    text(px(-14.0), pz(2.0), "입구 0 m", 11, "start", "normal", GREY)
    marks = (("①", 8.6, 14.6), ("②", 8.6, 42.0), ("③", 10.4, 73.0), ("④", 11.6, 93.0), ("⑤", -10.6, 105.2), ("⑥", 11.4, 117.6),
             ("⑦", 34.0, 117.6), ("⑧", 64.0, 90.4), ("⑨", 72.6, 120.0))
    for mark, mx, mz in marks:
        text(px(mx), pz(mz) + 5, mark, 15, "middle", "bold", RED)
    listed = (("① 식탁보 — 상 차리기", "매달리면 식탁 위로 실려 간다 · 0 → 4.4"), ("② 건배하는 식탁", "잔 여섯 쌍 사이로 달린다 · 54 m"),
              ("③ 나이트의 도약", "ㄱ자로 뛰는 나이트에 매달려 가운데 단까지"), ("④ 왈츠", "여왕의 드레스를 타고 어깨로, 악단석까지 · → 7.4"),
              ("⑤ 하프", "튕겨지는 줄을 타고 윗단으로 · → 11.6"), ("⑥ 큰 촛대", "촛농을 피하며 초를 오른다 · → 14.8"),
              ("⑦ 샹들리에 다섯 × 두 줄", "매달려 다음 샹들리에로 · 밑은 수프"), ("⑧ 케이크 5단 — 커팅", "도는 케이크의 크림 줄을 타고 세 층 · → 18.6"),
              ("⑨ 칼등과 발코니", "칼등을 걸어 왕의 발코니로 · 출구 18.2"))
    for i, (head, body) in enumerate(listed):
        text(px(19.0), pz(80.0 - i * 8.6), head, 12, "start", "bold", RED if i in (0, 3, 4, 5, 6, 7) else INK)
        text(px(19.0), pz(80.0 - i * 8.6) + 14, body, 11, "start", "normal", "#4a433c")
    text(px(19.0), pz(2.0), "남동쪽은 벽 밖이다(홀이 ㄱ자) · 붉은 제목 = 벽이나 줄을 타는 구간", 10, "start", "normal", GREY)
    text(px(EXIT[0]) - 12, pz(EXIT[2]) + 14, "출구 18.2", 11, "end")
    w('<path d="M %.1f %.1f l -6 12 l 12 0 z" fill="%s"/>' % (px(EXIT[0]), pz(EXIT[2]) - 14, DARK))

    # the dance floor, larger, with a blank margin on its west for the notes
    DANCE = (-40.0, 15.0, TABLE_Z1 - 6.0, 115.0)
    text(496, 86, "무도장 확대 — ③ 나이트 · ④ 왈츠 · ⑤ 하프 · ⑥ 촛대", 13, "start", "bold")
    dx, dz = plan(496.0, 96.0, 8.8, DANCE, "dance")
    edge = -16.2
    note(dx, dz, (-9.0, 110.6), (edge, 112.8), ["악단석 7.4 (체크포인트)"], "end", WALNUT)
    note(dx, dz, (STRINGS[1][0], HARP_Z), (edge, 108.6), ["⑤ 하프 — 줄 아홉", "서쪽 짧은 줄 2.2 m … 동쪽 긴 줄 4.2 m", "차례로 튕겨진다 · 튕기면 떨어진다"], "end", ORANGE)
    qn = couple(2, 0.0)[1]
    note(dx, dz, (qn[0] - 1.2, qn[1]), (edge, 101.6), ["여왕이 멈추는 곳 (북)", "어깨에서 악단석으로 건너간다"], "end", ORANGE)
    note(dx, dz, (-LOOP_B - PAIR - 1.0, LOOP_Z), (edge, 95.4), ["④ 왈츠 — 네 쌍, 한 바퀴 %d초" % LAP, "점선 = 여왕이 그리는 길", "주황 테 = 여왕(탄다) · 회색 = 왕"], "end", PURPLE)
    q0 = couple(0, 0.0)[1]
    note(dx, dz, (q0[0] - 1.2, q0[1]), (edge, 87.6), ["여왕이 멈추는 곳 (남)", "가운데 단에서 드레스를 잡는다"], "end", ORANGE)
    note(dx, dz, (ISLAND[0], 80.2), (edge, 81.8), ["C 가운데 단 4.4 (체크포인트)"], "end", WALNUT)
    note(dx, dz, (-7.5, 73.6), (edge, 77.6), ["B · D 바닥 칸", "떨어진 폰이 나이트를 잡는 곳"], "end", PURPLE)
    note(dx, dz, (-4.5, 67.6), (edge, 71.0), ["③ 나이트 넷 — A 식탁 끝 칸 4.4", "%.1f초 서 있다 %.1f초에 ㄱ자로 뛴다" % (KNIGHT_REST, KNIGHT_FLY), "A → B → C → D → A, 한 바퀴 %d초" % KNIGHT_ROUND], "end", PURPLE)
    text(dx(2.0), dz(LOFT[2] + 1.6), "윗단 11.6", 12, "start", "bold")
    text(dx(4.8), dz(CANDLES[0][1] - 2.9), "⑥ 초 14.8", 11, "start", "bold", ORANGE)

    # beside it: when each device does what, on one 20 second strip
    BX, BY, BW = 992.0, 96.0, 438.0
    text(BX, 86, "장치의 박자 — 20초 동안 (전부 같은 시계로 움직인다)", 13, "start", "bold")
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="250" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (BX, BY, BW, INK))
    T0, TS = BX + 112.0, (BW - 124.0) / 20.0
    for sec in range(0, 21, 5):
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#cfc6b3" stroke-width="0.8"/>' % (T0 + sec * TS, BY + 8, T0 + sec * TS, BY + 232))
        text(T0 + sec * TS, BY + 244, "%d초" % sec, 10, "middle", "normal", GREY)

    def bars(row, label, spans):
        y = BY + 14 + row * 31
        text(BX + 8, y + 12, label, 11, "start", "bold")
        for t0, t1, colour in spans:
            if t1 > t0:
                w('<rect x="%.1f" y="%.1f" width="%.1f" height="14" fill="%s" stroke="%s" stroke-width="0.4"/>' % (T0 + t0 * TS, y, (t1 - t0) * TS, colour, INK))

    def every(period, parts):
        spans, t = [], 0.0
        while t < 20.0:
            u = t
            for length, colour in parts:
                if colour:
                    spans.append((u, min(u + length, 20.0), colour))
                u += length
            t += period
        return [sp for sp in spans if sp[0] < 20.0]

    bars(0, "식탁보 한 줄", every(sum(CLOTH_CYCLE), ((CLOTH_CYCLE[0], ORANGE), (CLOTH_CYCLE[1], None), (CLOTH_CYCLE[2], SLATE), (CLOTH_CYCLE[3], None))))
    bars(1, "잔 한 쌍", every(sum(TOAST), ((TOAST[0], None), (TOAST[1], "#d9c6ea"), (TOAST[2] + TOAST[3], RED), (TOAST[4], None))))
    bars(2, "나이트", every(KNIGHT_REST + KNIGHT_FLY, ((KNIGHT_REST, ORANGE), (KNIGHT_FLY, PURPLE))))
    bars(3, "여왕", [(0.0, 0.9, ORANGE), (0.9, 9.1, PURPLE), (9.1, 10.9, ORANGE), (10.9, 19.1, PURPLE), (19.1, 20.0, ORANGE)])
    bars(4, "하프 줄 하나", every(PLUCK_EVERY, ((PLUCK_DEAD, RED), (PLUCK_EVERY - PLUCK_DEAD - PLUCK_WARN, ORANGE), (PLUCK_WARN, "#d9c6ea"))))
    bars(5, "샹들리에", every(ROPE_PERIOD, ((0.35, ORANGE), (ROPE_PERIOD / 2.0 - 0.7, PURPLE), (0.7, ORANGE), (ROPE_PERIOD / 2.0 - 0.7, PURPLE), (0.35, ORANGE))))
    rel = abs(TIERS[4][2] - TIERS[3][2])
    bars(6, "케이크 5단 크림", every(360.0 / CREAM[4][0] / rel, ((CREAM[4][1] / rel, ORANGE), (360.0 / CREAM[4][0] / rel - CREAM[4][1] / rel, SLATE))))
    ly2 = BY + 268
    for colour, label in ((ORANGE, "잡을 때"), (PURPLE, "실려 감"), ("#d9c6ea", "예고"), (RED, "맞으면 떨어짐"), (SLATE, "못 탐")):
        w('<rect x="%.1f" y="%.1f" width="14" height="10" fill="%s" stroke="%s" stroke-width="0.5"/>' % (BX, ly2 - 9, colour, INK))
        text(BX + 19, ly2, label, 11)
        BX += 30 + len(label) * 11.5
    BX = 992.0

    # the chandeliers from the side: how one hands a pawn to the next
    SX, SY, SS = 1010.0, 596.0, 8.0
    text(BX, 404, "샹들리에를 옆에서 — 한 줄(다섯), 줄 끝이 가장 멀리 간 두 자리", 13, "start", "bold")

    def side(along, y):
        return SX + along * SS, SY - (y - 9.0) * SS

    w('<rect x="%.1f" y="414" width="%.1f" height="250" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (BX, BW, INK))
    text(BX + 8, 432, "줄 %.1f m · 좌우 %d° · %d초 왕복 · 가장 빠를 때 초당 %.1f m (랩에서 점검한 그네 값)" % (
        ROPE_L, ROPE_SWING, ROPE_PERIOD, ROPE_L * math.radians(ROPE_SWING) * 2.0 * math.pi / ROPE_PERIOD), 11)
    cx0, cy0 = side(-1.0, CANDLE_TOP)
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#fff6dd" stroke="%s" stroke-width="2"/>' % (cx0, cy0, 2.0 * SS, 3.2 * SS, ORANGE))
    text(cx0 - 4, cy0 + 3.2 * SS + 14, "초 14.8", 11)
    lx0, ly0 = side(ROPE_SPAN - 0.6, TIERS[1][1])
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#f9d9d2" stroke="%s" stroke-width="1"/>' % (lx0, ly0, 1.6 * SS, 0.9 * SS, INK))
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#fbe6df" stroke="%s" stroke-width="2"/>' % (lx0 + 1.4 * SS, ly0 - 2.8 * SS, 0.6 * SS, 2.8 * SS, ORANGE))
    text(lx0 + 14, ly0 + 22, "케이크 2단 턱 10.2", 11, "end")
    for k in range(5):
        ax, ay = side(ROPE_ALONG[k], ROPE_PIVOT_Y[k])
        for sgn in (-1, 1):
            bx, by = side(ROPE_ALONG[k] + sgn * ROPE_END, ROPE_LOW[k])
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1.6"/>' % (ax, ay, bx, by, ORANGE))
            mx, my = ax + (bx - ax) * WHEEL_AT / ROPE_L, ay + (by - ay) * WHEEL_AT / ROPE_L
            w('<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="3.5" fill="#f7e3a6" stroke="%s" stroke-width="1"/>' % (mx, my, WHEEL_R * SS * 0.8, GOLD))
            w('<circle cx="%.1f" cy="%.1f" r="2.6" fill="%s"/>' % (bx, by, DARK))
        w('<circle cx="%.1f" cy="%.1f" r="3" fill="%s"/>' % (ax, ay, GOLD))
        text(ax, ay - 7, "%d" % (k + 1), 11, "middle", "bold")
    for k, (gap, drop, name) in enumerate(HANDOVERS):
        gx, gy = side(ROPE_ALONG[k] + ROPE_END + gap / 2.0, ROPE_LOW[k])
        text(gx, 626 + 17 * (k % 2), "%s · 틈 %.1f, %.1f 아래" % (name, gap, drop), 10, "middle", "bold", PURPLE)
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="0.7"/>' % (gx, gy + 6, gx, 614 + 17 * (k % 2), GREY))

    # the east wing, across the whole sheet, with a blank margin on its east for the notes
    EAST = (0.0, 104.0, 88.0, BALCONY[3])
    text(34, 716, "동쪽 날개 확대 — ⑥ 촛대 · ⑦ 샹들리에 · ⑧ 케이크 · ⑨ 칼등과 발코니", 13, "start", "bold")
    ex, ez = plan(34.0, 726.0, 13.4, EAST, "east")
    p2 = add(CANDLES[1], ROPE_ALONG[1], ROPE_HEAD)
    note(ex, ez, (p2[0], p2[1] + 1.6), (18.0, 124.0), ["⑦ 샹들리에 — 줄마다 다섯 (원 = 촛불 바퀴, 보라 굵은 선 = 줄 끝이 흔들리는 폭 ±%.1f m)" % ROPE_END,
                                                 "이웃끼리 반 박자 어긋나 끝에서 만난다. 두 줄은 서로 반 박자 어긋난다"], "start", PURPLE)
    note(ex, ez, (CANDLES[1][0], CANDLES[1][1] + 1.0), (2.0, 120.0), ["⑥ 초 둘 · 위 14.8", "촛농을 피하며 3.2 m"], "start", ORANGE)
    note(ex, ez, ((SOUP[0] + SOUP[1]) / 2 - 8.0, SOUP[2] + 1.0), (18.0, 93.4), ["수프 — 빠지면 윗단(체크포인트)에서 다시"], "start", RED)
    note(ex, ez, (CAKE[0] + TIERS[0][0] - 0.4, CAKE[1] + 1.0), (79.0, 112.0), ["⑧ 케이크 5단 — 커팅", "층 위: 7.4 · 10.2 · 13.0 · 15.8 · 18.6", "층마다 반대로 돈다",
                                                                         "주황 원호 = 크림(잡힌다)", "청회색 = 퐁당(못 잡는다)"], "start", ORANGE)
    note(ex, ez, (LAND[0][0], LAND[0][1]), (79.0, 98.0), ["샹들리에가 내려 주는 곳", "2단 턱 10.2 (체크포인트)"], "start", PURPLE)
    note(ex, ez, (CAKE[0] + 0.5, CAKE[1] + 8.4), (79.0, 121.0), ["⑨ 케이크 칼 — 칼등 %.1f m, 폭 %.0f m" % (dist(KNIFE[0], KNIFE[1]), KNIFE_WIDTH), "꼭대기 층을 가르고 발코니에 걸쳐 있다",
                                                          "5단 옆면에서 칼에 닿으면 떨어진다"], "start", INK)
    note(ex, ez, (CAKE[0] + 6.9, CAKE[1] - 6.9), (79.0, 91.6), ["케이크 식탁 4.4", "떨어지면 여기서 다시 오른다"], "start", WALNUT)
    text(ex((BALCONY[0] + BALCONY[1]) / 2), ez(BALCONY[2] + 4.0), "왕의 발코니 18.2", 12, "middle", "bold")
    text(ex(EXIT[0]) + 12, ez(EXIT[2]) + 16, "출구", 11)
    w('<path d="M %.1f %.1f l -6 12 l 12 0 z" fill="%s"/>' % (ex(EXIT[0]), ez(EXIT[2]) + 4, DARK))

    # the height profile, the way laid out straight
    EX, EY, EH = 80.0, 1570.0, 12.0
    flat = [0.0]
    for a, b in zip(WAY, WAY[1:]):
        flat.append(flat[-1] + math.hypot(b[0] - a[0], b[2] - a[2]))
    ES = 1320.0 / flat[-1]
    prof = [(EX + s * ES, EY - p[1] * EH) for s, p in zip(flat, WAY)]
    text(34, 1294, "길을 펴서 본 높이 — 가로는 지나간 거리, 세로는 높이. 굵은 주황 = 타고 오르는 곳, 보라 = 실려 가는 곳(나이트 · 여왕 · 샹들리에)", 13, "start", "bold")
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (EX - 14, EY - 21.2 * EH, 1320 + 44, 21.2 * EH + 16, INK))
    for h in (0, TABLE, STAGE_Y, LOFT_Y, CANDLE_TOP, BALCONY_Y):
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#cfc6b3" stroke-width="0.8"/>' % (EX - 14, EY - h * EH, EX + 1320 + 30, EY - h * EH))
        text(EX - 20, EY - h * EH + 4, "%.1f" % h, 10, "end")
    for i in range(1, len(WAY)):
        (x0, y0), (x1, y1), how = prof[i - 1], prof[i], WAY[i][4]
        if how == "climb":
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="5"/>' % (x0, y0, x1, y1, ORANGE))
        elif how == "swing":
            w('<path d="M %.1f %.1f Q %.1f %.1f %.1f %.1f" fill="none" stroke="%s" stroke-width="2.4"/>' % (x0, y0, (x0 + x1) / 2, max(y0, y1) + 18, x1, y1, PURPLE))
        elif how == "ride":
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="2.6"/>' % (x0, y0, x1, y1, PURPLE))
        else:
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="2.4"/>' % (x0, y0, x1, y1, DARK))
    index = dict((p[3], i) for i, p in enumerate(WAY) if p[3])
    for key, label, sub, ddx, ddy in (
            ("식탁 위", "① 식탁보", "+4.4 실려 오름", -62, -30), ("나이트의 칸", "② 건배하는 식탁", "달리기 54 m", -190, -22),
            ("나이트에 매달림", "③ 나이트", "ㄱ자 도약 둘", 26, 34), ("여왕의 어깨", "④ 왈츠", "+3.0, 반 바퀴 실려 감", 10, -26),
            ("하프 목", "⑤ 하프", "줄 2.2 ~ 4.2", -76, -26), ("초 위", "⑥ 촛대", "+3.2, 촛농", -76, -26),
            ("샹들리에 3", "⑦ 샹들리에 다섯", "매달려 건넘 · 내려감 3.2", -60, -66), ("3단 위", "⑧ 케이크", "+2.8 세 번", -104, 42),
            ("발코니", "⑨ 칼등 · 발코니", "출구 18.2", -30, -30)):
        at = prof[index[key]]
        text(at[0] + ddx, at[1] + ddy, label, 12, "start", "bold")
        text(at[0] + ddx, at[1] + ddy + 14, sub, 11, "start", "normal", "#4a433c")
    total, level = way_length()
    text(EX + 1320 + 30, EY + 34, "길 약 %.0f m · 오르는 높이를 다 더하면 %.0f m" % (total, sum(max(0.0, b[1] - a[1]) for a, b in zip(WAY, WAY[1:]))), 11, "end")

    # the beats, with the time budget beside them
    rows = (
        ("부엌 통로", "입구에서 식탁 밑까지 달린다", "달리기"),
        ("① 식탁보 — 상 차리기", "시종들이 식탁보를 감아 올린다. 올라가는 줄에 매달려 식탁 위로 실려 간다", "실려 가기"),
        ("② 건배하는 식탁", "잔 여섯 쌍이 차례로 '챙' 부딪치고 그때마다 러너가 뒤로 당겨진다", "달리기"),
        ("③ 나이트의 도약", "나이트가 식탁 끝에 내려앉았다 ㄱ자로 뛴다. 덮개 천에 매달려 두 번 뛰어 가운데 단에 내린다", "매달려 실려 가기"),
        ("④ 왈츠", "왕과 여왕이 돌며 무도장을 돈다. 멈춘 여왕의 드레스를 타고 어깨에 올라, 악단석 앞에서 내린다", "움직이는 벽 오르기"),
        ("⑤ 하프", "하프 줄을 타고 오른다. 연주자가 줄을 차례로 튕기고, 튕겨진 줄에서는 떨어진다", "줄 타기 · 박자"),
        ("⑥ 큰 촛대", "촛농이 흘러내리는 초를 옆으로 피하며 오른다", "피하며 오르기"),
        ("⑦ 샹들리에 다섯", "흔들리는 샹들리에에 매달려, 끝에서 만나는 다음 샹들리에로 건너간다. 밑은 수프", "매달려 건너기"),
        ("⑧ 케이크 — 커팅", "층마다 반대로 도는 케이크의 크림 줄을 타고 세 층. 꼭대기 층은 칼 쪽으로 실려 가기 전에", "도는 벽 읽기"),
        ("⑨ 칼등과 발코니", "케이크에 꽂힌 칼의 등(폭 1 m)을 걸어 왕의 발코니로, 출구", "달리기"),
    )
    ty = 1640.0
    text(34, ty, "구간", 12, "start", "bold")
    text(240, ty, "무슨 일이 벌어지고, 폰은 무엇을 하나", 12, "start", "bold")
    text(1010, ty, "벽타기", 12, "start", "bold")
    for x, label in ((1190, "길 m"), (1250, "이동"), (1310, "기다림"), (1370, "실패"), (1430, "합 (초)")):
        text(x, ty, label, 12, "end", "bold")
    for (name, what, verb), row in zip(rows, BUDGET):
        ty += 27
        w('<line x1="34" y1="%.1f" x2="1430" y2="%.1f" stroke="#d8cfbc" stroke-width="0.8"/>' % (ty - 18, ty - 18))
        text(34, ty, name, 13, "start", "bold")
        text(240, ty, what, 12)
        text(1010, ty, verb, 12, "start", "normal", ORANGE if "달리기" not in verb else GREY)
        for x, value in ((1190, "%.0f" % row[1]), (1250, "%.0f" % row[2]), (1310, "%.0f" % row[3]), (1370, "%.0f" % row[4]), (1430, "%.0f" % (row[2] + row[3] + row[4]))):
            text(x, ty, value, 12, "end")
    ty += 27
    w('<line x1="34" y1="%.1f" x2="1430" y2="%.1f" stroke="%s" stroke-width="1.2"/>' % (ty - 18, ty - 18, INK))
    t = budget_total()
    text(34, ty, "합계", 13, "start", "bold")
    text(240, ty, "보통 플레이어 한 명 %s · 열두 명 혼잡 보정(1.12배) %s — 대계단 현관홀은 %s · %s" % (clock(t), clock(t * CROWD), clock(STAIR_HALL), clock(STAIR_HALL * CROWD)), 12, "start", "bold")
    for x, value in ((1190, "%.0f" % sum(r[1] for r in BUDGET)), (1250, "%.0f" % sum(r[2] for r in BUDGET)), (1310, "%.0f" % sum(r[3] for r in BUDGET)),
                     (1370, "%.0f" % sum(r[4] for r in BUDGET)), (1430, "%.0f" % t)):
        text(x, ty, value, 12, "end", "bold")
    ty += 34
    text(34, ty, "셈법은 대계단 기획서와 같다: 이동 = 달리기 초당 2.5 m · 오르기 초당 1.2 m, 실패 = 실패할 확률 × 한 번의 값. 손계산이고 사람이 달려 본 적이 없다.", 12)
    ty += 19
    text(34, ty, "떨어지면: 무도장 바닥에서는 바닥 칸(B · D)에 내려앉는 나이트를 잡고 가운데 단으로 돌아온다. 끝나는 곳(낙사)은 샹들리에 밑의 수프뿐이다.", 12)
    ty += 19
    text(34, ty, "한 번에 오르는 높이는 폰 스테미나의 46% 이하, 룩의 65% 이하. 벽이든 줄이든 매달려 멈춰 있는 동안은 스테미나가 줄지 않는다.", 12)
    lx, ly = 34.0, ty + 34
    for colour, label in ((ORANGE, "타는 면 · 줄"), (PURPLE, "움직이는 것"), (SLATE, "못 잡는 면"), ("#e7a48f", "수프(끝)"), ("#3f8f5a", "체크포인트")):
        w('<rect x="%.1f" y="%.1f" width="18" height="11" fill="%s" stroke="%s" stroke-width="0.6"/>' % (lx, ly - 10, colour, INK))
        text(lx + 25, ly, label, 12)
        lx += 44 + len(label) * 13
    w('</svg>')
    open(path, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")   # a Korean console is cp949: no long dash in it
    report()
    if len(sys.argv) > 1:
        draw(sys.argv[1])
        print("drawn", sys.argv[1])
