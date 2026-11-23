# The grand banquet hall (Pawn Rush 2-4, level design v0.3): the one place its numbers are worked out.
# Section frame: the entry's threshold is the origin, x east, y up, z north (the way in).
#
# v0.3 is one hall, laid out as a banquet hall is: the musicians' gallery over the entry, a U of banquet tables
# round a dance floor, the chandeliers down the middle over the dancers, the cake at the head table and the royal
# balcony over it. The way goes up the hall along a table, back down it with the dance, and up it again in the air.
#
#   python banquet_layout.py                 prints the layout, the pitches, the time budget and the way's length
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
    # The point d from a on the way to b.
    k = d / dist(a, b)
    return (a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)


# ---------------------------------------------------------------- pawn numbers (RagdollTuning.asset, RagdollPawn)
CLIMB_UP, CLIMB_DOWN, CLIMB_SIDE, STAMINA, ROOK = 1.2, 1.6, 0.9, 8.0, 0.7
RUN, SPRINT = 2.5, 3.6
ROPE_REACH, ROPE_JUMP_UP, ROPE_REGRAB = 0.7, 3.5, 0.5
MAX_DROP = 8.0                                   # FallDistanceRespawn: this far under the last floor stood on is a fall

# ---------------------------------------------------------------- levels
KITCHEN, TABLE, STAGE_Y, LOFT_Y = 0.0, 4.4, 7.4, 11.6
HALL_X, CEILING = 32.0, 26.0                     # half width of the hall; its north wall is worked out below

# the musicians' gallery over the entry: the back tier (the loft) and, in front of it, the lower tier (the stage);
# under both is the servery the way comes in through
GALLERY_X = 14.0                                 # its half width: it fills the open end of the table's U
LOFT = (-GALLERY_X, GALLERY_X, 0.0, 10.0)        # x0, x1, z0, z1
STAGE = (-GALLERY_X, GALLERY_X, 10.0, 16.4)
GALLERY_UNDER = 6.9                              # the servery's ceiling
SERVERY_BACK = 0.6                               # its wall to the dance floor stands this far in under the stage's edge,
#                                                 clear of the queen's hem where she stands at the gallery

# 4 the waltz: four couples round the floor, each couple turning the other way three times a lap, so the queen
#   comes to a stand four times a lap: at the dais (north), at the gallery (south) and at either side
GOWN_FOOT, GOWN_SHOULDER, SHOULDER_Y, HEAD_R = 1.8, 1.3, STAGE_Y, 0.55
PAIR = 1.9                                       # each partner this far from the couple's middle
LOOP_R = PAIR * 3.0                              # the couple's middle goes round a circle this big
LAP, COUPLES, SPINS = 20.0, 4, 3
REACH_GAP = 0.3                                  # dais edge to gown, shoulder to gallery edge, at the stand


def gown(y):
    return GOWN_FOOT - (GOWN_FOOT - GOWN_SHOULDER) * y / SHOULDER_Y


LOOP_Z = STAGE[3] + REACH_GAP + GOWN_SHOULDER + PAIR + LOOP_R
ISLAND_Z0 = LOOP_Z + LOOP_R + PAIR + gown(TABLE) + REACH_GAP
ISLAND = (-10.5, 10.5, ISLAND_Z0, ISLAND_Z0 + 6.0)         # the dais in the middle of the floor, at the tables' height

# 3 the knights: four of them hopping their L move round two diamonds of squares; a pawn hangs on and is carried
SQUARE = 3.0
KNIGHT_REST, KNIGHT_FLY, KNIGHT_ARC = 1.5, 1.5, 1.2      # seconds on a square, seconds in the air, metres over the higher square
KNIGHT_TURN = 0.7                                        # of its stand, turning to face the next square
KNIGHT_R, KNIGHT_DRAPE, KNIGHT_TALL = 1.3, 5.2, 7.0      # body radius, top of the caparison (the face to hang on), head
FLOOR_SQUARE = 0.4
KC = ISLAND_Z0 + 3.0
#   west diamond: A on the head table, B on the floor, C on the dais, D on the floor
KNIGHT_SQUARES = ((-7.5, TABLE, KC + 12.0), (-10.5, FLOOR_SQUARE, KC + 6.0), (-7.5, TABLE, KC), (-4.5, FLOOR_SQUARE, KC + 6.0))
KNIGHTS = ((-1, 0.0), (-1, 6.0), (1, 3.0), (1, 9.0))     # side (-1 west, 1 east), seconds ahead on the 12 s round
KNIGHT_ROUND = 4 * (KNIGHT_REST + KNIGHT_FLY)

# 1, 2 the banquet table, a U open to the south: two arms up the hall and the head table across its north end
ARM_IN, ARM_OUT = GALLERY_X, 26.0                # an arm's inner and outer edge, from the middle
ARM_Z0 = 4.0                                     # the arms' south ends (the tablecloths), beside the gallery
RUN_END = 9.9                                    # |x| where the runner ends on the head table, at the knights' squares
HEAD_Z0, HEAD_Z1 = KNIGHT_SQUARES[0][2] - 1.8, KNIGHT_SQUARES[0][2] + 6.4
ARM_X = (ARM_IN + ARM_OUT) / 2.0                 # the runner's line up an arm
HEAD_Z = (HEAD_Z0 + HEAD_Z1) / 2.0               # and along the head table
CLOTH_LANES, LANE_WIDTH, CLOTH_HEM = 6, 2.0, 1.6
CLOTH_UP, CLOTH_DOWN = 0.9, 1.2                  # m/s a lane carries a climber up, and down
CLOTH_CYCLE = (3.2, 0.8, 3.2, 0.8)               # up, still, down, still (s); lanes a third of it apart
RUNNER = 2.5                                     # half width of the lane between the place settings
GOBLET_ARM = (11.5, 19.5, 27.5, 35.5, 43.5)      # z of each pair on an arm
GOBLET_HEAD = (13.0,)                            # |x| of each pair on the head table
GOBLET_X, GOBLET_R, GOBLET_TALL, GOBLET_LEAN = 3.4, 1.3, 3.6, 36.0   # foot from the runner's line, bowl, its height, degrees to the clink
TOAST = (1.9, 0.5, 0.3, 0.3, 0.6)                # upright, lean back, swing in, clink, back (s)
TOAST_WAVE = 0.9                                 # each pair this much after the one before it (s)
RUNNER_JERK = (1.5, 0.4)                         # metres back, over seconds, at each clink
FENCE = 2.0                                      # the hedge of bottles and flowers along the U's inner edge: not to be crossed.
#                                                 Under it the cloth hangs to the floor all round the dance floor (nothing to hold).

# 5 the harp on the stage, leaning on the loft: nine strings to climb, plucked in a run; its neck is the walk up
HARP_Z = 12.2
STRINGS = tuple((-10.0 + i, 9.6 + 0.25 * i) for i in range(9))      # x, the string's top
PLUCK_EVERY, PLUCK_STEP, PLUCK_WARN, PLUCK_DEAD = 4.2, 0.3, 0.7, 0.6
NECK = 0.3                                       # the harp neck, a plank over the strings' tops: this thick, its top the walk

# 6 the great candles on the loft, one for each line of chandeliers, wax running down them
ROPE_HEAD = 0.0                                  # the chandeliers lead straight up the hall
CANDLE_MID = (0.0, LOFT[3] - 2.2)
CANDLE_R, DISH_R, CANDLE_TOP = 1.0, 2.0, LOFT_Y + 3.2
CANDLES = (add(CANDLE_MID, 2.6, ROPE_HEAD - 90.0), add(CANDLE_MID, 2.6, ROPE_HEAD + 90.0))
WAX_LANES, WAX_EVERY, WAX_WARN, WAX_SPEED = 6, 0.9, 0.7, 2.2

# 7 the chandeliers: two lines of five down the middle of the hall, each a pendulum (RopeLine)
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

# 8 the cake in the middle of the head table: five tiers turning against each other. The lowest tier is all fondant
#   (nothing to hold), so the cake is reached from the chandeliers only; its ledge has a rail and catches a fall.
TIERS = ((8.2, 7.4, 6.0), (6.8, 10.2, -8.0), (5.4, 13.0, 10.0), (4.0, 15.8, -13.0), (2.6, 18.6, 18.0))   # radius, top, deg/s
CREAM = ((0, 0.0), (5, 40.0), (4, 50.0), (4, 50.0), (3, 70.0))                                        # stripes, degrees each
LAND_R = (TIERS[1][0] + TIERS[2][0]) / 2.0                           # where the chandeliers set a pawn down: tier 2's ledge
CAKE = add(CANDLE_MID, ROPE_SPAN + math.sqrt(LAND_R ** 2 - 2.6 ** 2), ROPE_HEAD)
CAKE_TABLE_R, TOPPER = 9.8, (0.7, 20.4)

# 9 the cake knife, cut into the top tier and lying north to the royal balcony over the head of the hall
BALCONY_Y = 18.2
WALL_N = math.ceil(CAKE[1] + 1.0 + 10.6)
KNIFE = ((CAKE[0], CAKE[1] + 1.0), (CAKE[0], WALL_N + 0.6))
KNIFE_WIDTH, KNIFE_PEEL = 1.0, 12.0                                  # the back to walk on; degrees either side that peel a climber
BALCONY = (CAKE[0] - 6.0, CAKE[0] + 6.0, WALL_N, WALL_N + 12.0)
EXIT = (CAKE[0], BALCONY_Y, BALCONY[3])
HALL = (-HALL_X, HALL_X, 0.0, WALL_N)
FLOOR = (-ARM_IN + 0.4, ARM_IN - 0.4, STAGE[3], HEAD_Z0)             # the dance floor, shut in by the U and the gallery


# ---------------------------------------------------------------- where the moving things are at time t (seconds)
def cloth_speed(lane, t):
    # Metres a second up (+) or down (-) that lane's cloth is running (the same on both arms).
    up, hold, down, hold2 = CLOTH_CYCLE
    u = (t + lane * sum(CLOTH_CYCLE) / 3.0) % sum(CLOTH_CYCLE)
    return CLOTH_UP if u < up else 0.0 if u < up + hold else -CLOTH_DOWN if u < up + hold + down else 0.0


def goblet_spots(side):
    # The pairs along one side's way, in order: (middle of the runner there (x, z), heading of the runner).
    spots = [((side * ARM_X, z), 0.0) for z in GOBLET_ARM]
    spots += [((side * x, HEAD_Z), -90.0 * side) for x in GOBLET_HEAD]
    return spots


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
    # At t = 0 couple 0's queen stands at the gallery (south); half a lap on she stands at the dais (north).
    th = 2.0 * math.pi * (t / LAP + i / float(COUPLES))
    mid = (LOOP_R * math.sin(th), LOOP_Z - LOOP_R * math.cos(th))
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

CHECKPOINTS = (("입구", (0.0, KITCHEN, 3.0)), ("서쪽 식탁", (-ARM_X, TABLE, ARM_Z0 + 2.6)), ("동쪽 식탁", (ARM_X, TABLE, ARM_Z0 + 2.6)), ("상석", (-RUN_END - 3.0, TABLE, HEAD_Z + 2.4)),
               ("가운데 단", (0.0, TABLE, ISLAND[2] + 2.0)), ("악단석", (4.0, STAGE_Y, STAGE[3] - 2.4)), ("윗단", (-8.0, LOFT_Y, 5.0)),
               ("케이크 2단 턱", (LAND[0][0], TIERS[1][1], LAND[0][1])), ("케이크 4단 턱", (CAKE[0] - 3.3, TIERS[3][1], CAKE[1])),
               ("왕의 발코니", (CAKE[0], BALCONY_Y, BALCONY[2] + 3.0)))


# ---------------------------------------------------------------- the way (x, y, z, what, how it is travelled)
def queen_ride():
    # The queen's shoulder from her stand at the dais to her stand at the gallery (couple 0, half a lap).
    return [(q[0], SHOULDER_Y, q[1]) for q in (couple(0, LAP * 0.5 * (1.0 + n / 40.0))[1] for n in range(41))]


def the_way(side=-1):
    # The way by the west arm (side -1) or the east one: they are mirrors until the dais.
    way = [(0.0, KITCHEN, 0.0, "입구", "run"), (side * ARM_X, KITCHEN, ARM_Z0 - 0.4, "식탁보 아래", "run"),
           (side * ARM_X, TABLE, ARM_Z0 + 0.4, "식탁 위", "climb"), (side * ARM_X, TABLE, HEAD_Z, "상석 모퉁이", "run"),
           (side * RUN_END, TABLE, HEAD_Z - 1.2, "나이트의 칸", "run")]
    a, b, c = KNIGHT_SQUARES[0], KNIGHT_SQUARES[1], KNIGHT_SQUARES[2]
    way += [(-side * a[0] + side * KNIGHT_R, TABLE + 0.6, a[2], "나이트에 매달림", "run"), (-side * b[0], FLOOR_SQUARE + 0.6, b[2], "", "ride"),
            (-side * c[0], TABLE + 0.6, c[2], "", "ride"), (0.0, TABLE, ISLAND[2] + 0.4, "가운데 단", "run")]
    ride = queen_ride()
    way += [(ride[0][0], TABLE + 0.4, ride[0][2] + gown(TABLE), "여왕의 드레스", "run"), (ride[0][0], SHOULDER_Y, ride[0][2] + 1.0, "여왕의 어깨", "climb")]
    way += [(p[0], p[1], p[2], "", "ride") for p in ride[1:]]
    way += [(0.0, STAGE_Y, STAGE[3] - 0.6, "악단석", "run"), (STRINGS[5][0], STAGE_Y, HARP_Z + 0.4, "하프 아래", "run"),
            (STRINGS[5][0], STRINGS[5][1] + 0.1, HARP_Z - 0.6, "하프 목", "climb"), (STRINGS[8][0] + 1.4, LOFT_Y, LOFT[3] - 1.0, "윗단", "run")]
    k = CANDLES[0]
    foot = toward(k, (k[0] - 3.0, k[1]), CANDLE_R + 0.3)
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


WAY = the_way(-1)
TABLE_RUN = (HEAD_Z - ARM_Z0 - 0.4) + (ARM_X - RUN_END)


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
ENTRY_RUN = math.hypot(ARM_X, ARM_Z0 - 0.4)
BUDGET = (
    ("배식실 통로", ENTRY_RUN, ENTRY_RUN / RUN, 0.0, 0.0),
    ("① 식탁보", 5.0, TABLE / (CLIMB_UP + CLOTH_UP) + 1.4, 1.5, 0.3 * 4.0),
    ("② 건배하는 식탁", TABLE_RUN, TABLE_RUN / RUN, 4.5, 0.4 * 6.0),
    ("③ 나이트의 도약", 17.0, 2.0 * (KNIGHT_REST + KNIGHT_FLY) + 1.4, 3.0, 0.3 * 9.0),
    ("④ 왈츠", ride_length() + 3.0, LAP / 2.0 + 1.7, LAP / COUPLES / 2.0, 0.25 * 5.0 + 0.2 * 23.0),
    ("⑤ 하프", 14.0, 3.2 / CLIMB_UP + 5.6, 1.5, 0.3 * 5.0),
    ("⑥ 큰 촛대", 9.0, 3.2 / CLIMB_UP + 3.5, 0.0, 0.3 * 6.0),
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
    print("hall x %.0f .. %.0f, z 0 .. %.0f (balcony to %.0f)" % (HALL[0], HALL[1], WALL_N, BALCONY[3]))
    print("gallery: loft z %.1f .. %.1f at %.1f, stage z %.1f .. %.1f at %.1f" % (LOFT[2], LOFT[3], LOFT_Y, STAGE[2], STAGE[3], STAGE_Y))
    print("waltz: middle z %.2f, circle r %.1f; dais z %.2f .. %.2f" % (LOOP_Z, LOOP_R, ISLAND[2], ISLAND[3]))
    q_n, q_s = couple(0, LAP / 2.0)[1], couple(0, 0.0)[1]
    print("queen: stands at the dais %.2f from its edge, at the gallery %.2f from its edge; half a lap is %.1f m" % (
        ISLAND[2] - q_n[1] - gown(TABLE), q_s[1] - GOWN_SHOULDER - STAGE[3], ride_length()))
    print("knights' squares (west): %s" % "  ".join("(%.0f, %.1f, %.1f)" % s for s in KNIGHT_SQUARES))
    print("table: arms |x| %.0f .. %.0f from z %.0f; head table z %.1f .. %.1f; the run on it %.1f m" % (ARM_IN, ARM_OUT, ARM_Z0, HEAD_Z0, HEAD_Z1, TABLE_RUN))
    print("candles %s  top %.1f" % (", ".join("(%.2f, %.2f)" % c for c in CANDLES), CANDLE_TOP))
    print("chandeliers along the line %s (z %s)" % (" ".join("%.2f" % a for a in ROPE_ALONG), " ".join("%.1f" % (CANDLE_MID[1] + a) for a in ROPE_ALONG)))
    print("   end swings %.2f either way, rises %.2f; pivots at y %s; ends at y %s" % (
        ROPE_END, ROPE_RISE, " ".join("%.2f" % y for y in ROPE_PIVOT_Y), " ".join("%.2f" % y for y in ROPE_LOW)))
    print("   span %.1f m; lands %.2f from the cake's middle (ledge %.1f .. %.1f)" % (ROPE_SPAN, dist(LAND[0], CAKE), TIERS[2][0], TIERS[1][0]))
    print("cake (%.2f, %.2f)  knife %.1f m  north wall z %.0f  exit (%.1f, %.1f, %.1f)" % (
        CAKE[0], CAKE[1], dist(KNIFE[0], KNIFE[1]), WALL_N, EXIT[0], EXIT[1], EXIT[2]))
    # what hangs over what: the least room between a chandelier's end with a pawn on it and the heads below
    crown, knight_top = SHOULDER_Y + 2.3, TABLE + KNIGHT_ARC + KNIGHT_TALL
    low = [(CANDLE_MID[1] + ROPE_ALONG[k], ROPE_PIVOT_Y[k] - ROPE_L - 1.2) for k in range(5)]
    print("   a pawn hanging at the lowest of each swing: %s; kings' crowns %.1f; a knight's head in the air %.1f, %.1f m to the side" % (
        " ".join("%.1f" % y for z, y in low), crown, knight_top, abs(KNIGHT_SQUARES[3][0]) - 0.5 - 2.6 - 0.3))
    print("   under the lines the last floor stood on is the candle (%.1f): the dance floor is %.1f m down, the head table %.1f (more than %.0f is a fall)" % (
        CANDLE_TOP, CANDLE_TOP, CANDLE_TOP - TABLE, MAX_DROP))
    for name, metres, extra in PITCHES:
        pawn, rook = cost(metres, extra)
        print("  %-30s %.1f m  pawn %3.0f%%  rook %3.0f%%" % (name, metres, pawn * 100, rook * 100))
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
        # The hall from above: `win` (x0, x1, z0, z1) drawn at S pixels a metre with its corner at (X0, Y0).
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
        rect(HALL[0], HALL[1], HALL[2], HALL[3], "#e9e2d2", WALNUT, 3.0)
        # the dance floor: a chessboard shut in by the U of tables and the gallery
        n = int(round((FLOOR[1] - FLOOR[0]) / SQUARE))
        for i in range(n):
            for j in range(int((FLOOR[3] - FLOOR[2]) / SQUARE) + 1):
                if (i + j) % 2 == 0:
                    z0 = FLOOR[2] + j * SQUARE
                    x0 = FLOOR[0] + i * (FLOOR[1] - FLOOR[0]) / n
                    rect(x0, x0 + (FLOOR[1] - FLOOR[0]) / n, z0, min(z0 + SQUARE, FLOOR[3]), "#ddd3bd", "none", 0)
        # the U of tables, its runner, the cloth lanes, the goblets, the hedge along its inner edge
        for side in (-1, 1):
            x0, x1 = sorted((side * ARM_IN, side * ARM_OUT))
            rect(x0, x1, ARM_Z0, HEAD_Z1, CREAMC, "none", 0)
        rect(-ARM_OUT, ARM_OUT, HEAD_Z0, HEAD_Z1, CREAMC, "none", 0)
        outline = [(-ARM_OUT, ARM_Z0), (-ARM_OUT, HEAD_Z1), (ARM_OUT, HEAD_Z1), (ARM_OUT, ARM_Z0), (ARM_IN, ARM_Z0), (ARM_IN, HEAD_Z0), (-ARM_IN, HEAD_Z0),
                   (-ARM_IN, ARM_Z0), (-ARM_OUT, ARM_Z0)]
        poly(outline, WALNUT, 2.0)
        for side in (-1, 1):
            x0, x1 = sorted((side * (ARM_X - RUNNER), side * (ARM_X + RUNNER)))
            rect(x0, x1, ARM_Z0 + 2, HEAD_Z + RUNNER, OLIVE, "none", 0, 'opacity="0.75"')
            x0, x1 = sorted((side * (ARM_X - RUNNER), side * (RUN_END - 0.6)))
            rect(x0, x1, HEAD_Z - RUNNER, HEAD_Z + RUNNER, OLIVE, "none", 0, 'opacity="0.75"')
            for i in range(CLOTH_LANES):
                xa = side * ARM_IN + side * i * LANE_WIDTH
                xa, xb = sorted((xa, xa + side * LANE_WIDTH))
                rect(xa, xb, ARM_Z0 - 1.0, ARM_Z0, PURPLE if i % 3 != 1 else "#b9a2dc", INK, 0.8)
            for (mid, head) in goblet_spots(side):
                across = heading(head + 90.0)
                for s in (-1, 1):
                    g = (mid[0] + across[0] * s * GOBLET_X, mid[1] + across[1] * s * GOBLET_X)
                    circle(g, GOBLET_R, "#d9c6ea", PURPLE, 1.4)
                    line(g, (mid[0] + across[0] * s * 0.3, mid[1] + across[1] * s * 0.3), PURPLE, 1.0, 'stroke-dasharray="3 2"')
            line((side * ARM_IN, ARM_Z0), (side * ARM_IN, HEAD_Z0), SLATE, 3.0)
            line((side * ARM_IN, HEAD_Z0), (side * 11.4, HEAD_Z0), SLATE, 3.0)
        line((STAGE[0], STAGE[3]), (STAGE[1], STAGE[3]), SLATE, 3.0)
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
        poly([couple(0, LAP * i / 240.0)[1] for i in range(241)], PURPLE, 1.0, 'stroke-dasharray="2 3"')
        for i in range(COUPLES):
            mid, q, k, turn = couple(i, 0.0)
            line(q, k, GOLD, 3.0 * fat)
            circle(k, GOWN_FOOT, "#cfd6dc", SLATE, 1.2)
            circle(q, GOWN_FOOT, "#f6dcc6", ORANGE, 3.0 * fat)
            circle(q, HEAD_R, GOLD, INK, 0.6)
            circle(k, HEAD_R, GOLD, INK, 0.6)
        # the gallery over the entry: the stage and the loft, the harp, the candles
        rect(STAGE[0], STAGE[1], STAGE[2], STAGE[3], "#efe2bf", WALNUT, 2.0)
        rect(LOFT[0], LOFT[1], LOFT[2], LOFT[3], "#e0cb98", WALNUT, 2.0)
        rect(STRINGS[0][0] - 0.8, STRINGS[8][0] + 0.8, LOFT[3], HARP_Z, GOLD, INK, 0.8, 'opacity="0.85"')
        for x, top in STRINGS:
            circle((x, HARP_Z), 0.28, ORANGE, INK, 0.6)
        for c in CANDLES:
            circle(c, DISH_R, "#f0d9a8", GOLD, 1.5)
            circle(c, CANDLE_R, "#fff6dd", ORANGE, 3.0 * fat)
        # the cake on the head table
        circle(CAKE, CAKE_TABLE_R, CREAMC, WALNUT, 1.5)
        shades = ("#f7cfc6", "#f9d9d2", "#fbe6df", "#fdf1ec", "#fffaf6")
        for i, (r, top, speed) in enumerate(TIERS):
            circle(CAKE, r, shades[i], "none", 0)
            stripes(CAKE, r, CREAM[i][0], CREAM[i][1], 17.0 * i, 3.0 * fat)
        circle(CAKE, TOPPER[0], GOLD, INK, 0.8)
        # the chandeliers, over all of that: each line's pivots, the sweep of the end, the wheel
        for lane in (0, 1):
            line(add(CANDLES[lane], CANDLE_R, ROPE_HEAD), LAND[lane], PURPLE, 1.0, 'stroke-dasharray="5 3"')
            for k in range(5):
                p = add(CANDLES[lane], ROPE_ALONG[k], ROPE_HEAD)
                line(add(p, -ROPE_END, ROPE_HEAD), add(p, ROPE_END, ROPE_HEAD), PURPLE, 2.6 * fat)
                circle(p, WHEEL_R, "#f7e3a6", ORANGE, 2.0 * fat, 'fill-opacity="0.8"')
                circle(p, 0.4, GOLD, INK, 0.6)
        # the knife and the balcony
        line(KNIFE[0], KNIFE[1], "#b9c2c9", 7.0 * fat)
        line(KNIFE[0], KNIFE[1], INK, 0.8)
        rect(BALCONY[0], BALCONY[1], BALCONY[2], BALCONY[3], "#efe2bf", WALNUT, 2.0)
        # the way (by the west arm; the east one is its mirror as far as the dais) and the checkpoints
        east = the_way(1)
        poly([(p[0], p[2]) for p in east[:9]], GREY, 1.2, 'stroke-dasharray="3 4"')
        poly([(p[0], p[2]) for p in WAY], DARK, 1.6, 'stroke-dasharray="6 4"')
        for cp_name, at in CHECKPOINTS:
            w('<path d="M %.1f %.1f v -13 l 9 4 l -9 4" fill="#3f8f5a" stroke="%s" stroke-width="1"/>' % (px(at[0]) + 1.2 * S, pz(at[2]) + 4, INK))
        w('</g>')
        return px, pz

    W, H = 1460, 1890
    w('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" font-family="Malgun Gothic, Apple SD Gothic Neo, sans-serif">' % (W, H))
    w('<rect width="%d" height="%d" fill="%s"/>' % (W, H, PAPER))
    text(34, 34, "대연회장 레벨 디자인 초안 v0.3 — 한 홀 안에서: 식탁을 따라 올라가고, 춤에 실려 내려오고, 샹들리에로 다시 올라간다", 19, "start", "bold")
    text(34, 56, "위에서 본 홀, 가운데를 남북으로 자른 모습, 길을 펴서 본 높이. 주황 = 타는 면과 줄, 보라 = 움직이는 것, 청회색 = 못 넘는 울타리 · 못 잡는 면. 단위 m.", 12)

    # the hall from above
    WIN = (-HALL_X - 2.0, HALL_X + 2.0, -3.0, BALCONY[3] + 3.0)
    S = 8.4
    text(34, 86, "위에서 본 대연회장 (%.0f × %.0f m, 북쪽이 위)" % (HALL_X * 2, WALL_N), 13, "start", "bold")
    px, pz = plan(34.0, 96.0, S, WIN, "hall")
    w('<path d="M %.1f %.1f l -7 13 l 14 0 z" fill="%s"/>' % (px(0), pz(0) - 2, DARK))
    text(px(0) + 12, pz(-1.6), "입구 (배식실, 0 m)", 11)
    w('<path d="M %.1f %.1f l -7 13 l 14 0 z" fill="%s"/>' % (px(EXIT[0]), pz(EXIT[2]) - 16, DARK))
    text(px(EXIT[0]) + 12, pz(EXIT[2]) - 6, "출구 18.2", 11)
    marks = (("①", -ARM_OUT - 2.6, ARM_Z0 - 1.8), ("①", ARM_OUT + 2.6, ARM_Z0 - 1.8), ("②", -ARM_OUT - 2.6, 31.0), ("②", ARM_OUT + 2.6, 31.0),
             ("③", -4.5, KNIGHT_SQUARES[1][2] - 3.0), ("③", 4.5, KNIGHT_SQUARES[1][2] - 3.0), ("④", -11.4, LOOP_Z), ("⑤", -11.8, HARP_Z + 2.4),
             ("⑥", -6.6, CANDLE_MID[1] - 3.6), ("⑦", 6.6, LOOP_Z + 3.0), ("⑧", 12.6, CAKE[1] + 5.0), ("⑨", 3.2, WALL_N - 3.0))
    for mark, mx, mz in marks:
        w('<circle cx="%.1f" cy="%.1f" r="10" fill="%s" stroke="%s" stroke-width="1"/>' % (px(mx), pz(mz), PAPER, RED))
        text(px(mx), pz(mz) + 5, mark, 15, "middle", "bold", RED)
    for label, lx, lz, anchor in (("윗단 11.6", LOFT[0] + 1.0, 2.0, "start"), ("악단석 7.4", STAGE[1] - 1.0, STAGE[2] + 2.4, "end"), ("무도장 0", 7.0, STAGE[3] + 1.6, "start"),
                                  ("가운데 단 4.4", 0.0, ISLAND[2] + 2.6, "middle"), ("서쪽 식탁 4.4", -ARM_X, ARM_Z0 + 3.0, "middle"), ("동쪽 식탁 4.4", ARM_X, ARM_Z0 + 3.0, "middle"),
                                  ("상석 4.4", -ARM_X, HEAD_Z1 + 1.4, "middle"), ("왕의 발코니 18.2", 0.0, BALCONY[2] + 6.4, "middle")):
        text(px(lx), pz(lz), label, 11, anchor, "bold", WALNUT)

    # beside it: the order of the banquet, what moves when, and the hall cut down its middle
    RX = 650.0
    text(RX, 86, "연회의 순서 = 길의 순서", 13, "start", "bold")
    listed = (("배식실", "입구(악단석 밑) → 옆으로 나가 한쪽 식탁의 남쪽 끝으로 (서쪽 · 동쪽, 거울)"),
              ("① 식탁보 — 상 차리기", "감아 올리는 줄에 매달려 식탁 위로 · 0 → 4.4"),
              ("② 건배하는 식탁", "ㄷ자 식탁을 따라 북쪽으로, 상석에서 가운데로 · %.0f m" % TABLE_RUN),
              ("③ 나이트의 도약", "상석 가운데에서 나이트에 매달려 무도장의 가운데 단으로 (남쪽으로)"),
              ("④ 왈츠", "여왕의 드레스를 타고 어깨로, 춤에 실려 악단석까지 (남쪽으로) · → 7.4"),
              ("⑤ 하프", "악단석에서 튕겨지는 줄을 타고 윗단으로 · → 11.6"),
              ("⑥ 큰 촛대", "촛농을 피하며 초를 오른다 · → 14.8"),
              ("⑦ 샹들리에 다섯 × 두 줄", "홀 한가운데, 춤추는 기물들 위로 매달려 건너간다 (다시 북쪽으로)"),
              ("⑧ 케이크 — 커팅", "상석 한가운데의 케이크. 2단 턱에 내려 세 층을 오른다 · → 18.6"),
              ("⑨ 칼등과 왕의 발코니", "칼등을 걸어 상석 위 발코니로 · 출구 18.2"))
    for i, (head, body) in enumerate(listed):
        y = 108 + i * 30
        text(RX, y, head, 12, "start", "bold", RED if i in (1, 3, 4, 5, 6, 7, 8) else INK)
        text(RX + 176, y, body, 11, "start", "normal", "#4a433c")
    text(RX, 108 + 10 * 30, "점선 = 서쪽 식탁으로 가는 길. 동쪽 식탁(옅은 점선)은 가운데 단까지 거울이다. 붉은 제목 = 벽이나 줄을 타는 구간.", 11, "start", "normal", GREY)

    # when each device does what, on one 20 second strip
    BX, BY, BW = RX, 446.0, 780.0
    text(BX, BY - 10, "장치의 박자 — 20초 동안 (전부 같은 시계로 움직인다)", 13, "start", "bold")
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="208" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (BX, BY, BW, INK))
    T0, TS = BX + 124.0, (BW - 140.0) / 20.0
    for sec in range(0, 21, 5):
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#cfc6b3" stroke-width="0.8"/>' % (T0 + sec * TS, BY + 6, T0 + sec * TS, BY + 190))
        text(T0 + sec * TS, BY + 202, "%d초" % sec, 10, "middle", "normal", GREY)

    def bars(row, label, spans):
        y = BY + 10 + row * 26
        text(BX + 8, y + 11, label, 11, "start", "bold")
        for t0, t1, colour in spans:
            if t1 > t0:
                w('<rect x="%.1f" y="%.1f" width="%.1f" height="13" fill="%s" stroke="%s" stroke-width="0.4"/>' % (T0 + t0 * TS, y, (t1 - t0) * TS, colour, INK))

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
    lx2 = BX
    for colour, label in ((ORANGE, "잡을 때 · 옮겨 탈 때"), (PURPLE, "실려 가는 동안"), ("#d9c6ea", "예고"), (RED, "맞으면 떨어진다"), (SLATE, "못 탄다")):
        w('<rect x="%.1f" y="%.1f" width="14" height="10" fill="%s" stroke="%s" stroke-width="0.5"/>' % (lx2, BY + 216, colour, INK))
        text(lx2 + 19, BY + 225, label, 11)
        lx2 += 34 + len(label) * 11.5

    # the hall cut down its middle, seen from the east: what is stacked over what
    SS = 8.6
    SX, SY = RX + 30.0, 706.0 + 28.0 * SS
    text(RX, 700, "가운데를 남북으로 자른 모습 — 무엇이 무엇 위에 있나 (왼쪽이 남 · 입구, 오른쪽이 북 · 출구)", 13, "start", "bold")

    def cut(z, y):
        return SX + z * SS, SY - y * SS

    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#f4eedf" stroke="%s" stroke-width="1"/>' % (RX, 712, 780, 28.0 * SS + 42, INK))

    def slab(z0, z1, y0, y1, fill, stroke=INK, width=1.0, extra=""):
        a, b = cut(z0, y1), cut(z1, y0)
        w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="%s" stroke="%s" stroke-width="%.1f" %s/>' % (a[0], a[1], b[0] - a[0], b[1] - a[1], fill, stroke, width, extra))

    slab(0, BALCONY[3], -0.5, 0, "#cfc6b3", "none", 0)
    slab(LOFT[2], LOFT[3], STAGE_Y, LOFT_Y, "#e0cb98", WALNUT, 1.5)
    slab(LOFT[2], STAGE[3], GALLERY_UNDER, STAGE_Y, "#efe2bf", WALNUT, 1.5)
    for z in (4.0, 12.0):
        slab(z - 0.4, z + 0.4, 0, GALLERY_UNDER, "#d8cfbc", "none", 0)
    slab(ARM_Z0, HEAD_Z0, TABLE - 0.5, TABLE, CREAMC, WALNUT, 1.0, 'stroke-dasharray="4 3" fill-opacity="0.45"')
    slab(ISLAND[2], ISLAND[3], TABLE - 0.5, TABLE, CREAMC, WALNUT, 1.5)
    slab((ISLAND[2] + ISLAND[3]) / 2 - 1.5, (ISLAND[2] + ISLAND[3]) / 2 + 1.5, 0, TABLE - 0.5, "#d8cfbc", "none", 0)
    slab(HEAD_Z0, CAKE[1] + CAKE_TABLE_R, TABLE - 0.5, TABLE, CREAMC, WALNUT, 1.5)
    slab(HEAD_Z0 + 2.0, CAKE[1] + CAKE_TABLE_R - 2.0, 0, TABLE - 0.5, "#d8cfbc", "none", 0)
    slab(WALL_N, WALL_N + 0.8, 0, BALCONY_Y - 0.8, "#b9ae9a", "none", 0)
    slab(BALCONY[2], BALCONY[3], BALCONY_Y - 0.8, BALCONY_Y, "#efe2bf", WALNUT, 1.5)
    bottom = TABLE
    for i, (r, top, speed) in enumerate(TIERS):
        slab(CAKE[1] - r, CAKE[1] + r, bottom, top, ("#f7cfc6", "#f9d9d2", "#fbe6df", "#fdf1ec", "#fffaf6")[i], SLATE if i == 0 else ORANGE, 2.0)
        bottom = top
    a, b = cut(KNIFE[0][1] - 1.0, TIERS[4][1]), cut(KNIFE[1][1], BALCONY_Y)
    w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#8f989f" stroke-width="5"/>' % (a[0], a[1] - 2, b[0], b[1] - 2))
    # harp, candle
    a, b = cut(HARP_Z, STAGE_Y), cut(HARP_Z, LOFT_Y)
    w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="3"/>' % (a[0], a[1], b[0], b[1], ORANGE))
    slab(CANDLE_MID[1] - CANDLE_R, CANDLE_MID[1] + CANDLE_R, LOFT_Y, CANDLE_TOP, "#fff6dd", ORANGE, 2.0)
    # the queen at her two stands, a king between, a knight on the head table and one in the air
    for z, fill, stroke in ((couple(0, 0.0)[1][1], "#f6dcc6", ORANGE), (couple(0, LAP / 2.0)[1][1], "#f6dcc6", ORANGE), (LOOP_Z, "#cfd6dc", SLATE)):
        p = [cut(z - GOWN_FOOT, 0), cut(z - GOWN_SHOULDER, SHOULDER_Y), cut(z + GOWN_SHOULDER, SHOULDER_Y), cut(z + GOWN_FOOT, 0)]
        w('<polygon points="%s" fill="%s" stroke="%s" stroke-width="2" fill-opacity="0.85"/>' % (" ".join("%.1f,%.1f" % q for q in p), fill, stroke))
        c = cut(z, SHOULDER_Y + 0.8)
        w('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" stroke="%s" stroke-width="0.8"/>' % (c[0], c[1], 0.8 * SS, GOLD, INK))
    for z, y in ((KNIGHT_SQUARES[0][2], TABLE), (KNIGHT_SQUARES[2][2], TABLE), (KNIGHT_SQUARES[1][2] + 2.2, 3.6)):
        slab(z - KNIGHT_R, z + KNIGHT_R, y, y + KNIGHT_DRAPE + 0.3, "#f0c9a4", ORANGE, 2.0, 'fill-opacity="0.85"')
        slab(z - 0.6, z + 0.9, y + KNIGHT_DRAPE + 0.3, y + KNIGHT_TALL, "#d9c6ea", INK, 0.8)
    # the chandeliers: each at both ends of its swing
    for k in range(5):
        zp = CANDLE_MID[1] + ROPE_ALONG[k]
        ax, ay = cut(zp, ROPE_PIVOT_Y[k])
        for sgn in (-1, 1):
            bx, by = cut(zp + sgn * ROPE_END, ROPE_LOW[k])
            w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1.6"/>' % (ax, ay, bx, by, ORANGE))
            mx, my = ax + (bx - ax) * WHEEL_AT / ROPE_L, ay + (by - ay) * WHEEL_AT / ROPE_L
            w('<ellipse cx="%.1f" cy="%.1f" rx="%.1f" ry="3.5" fill="#f7e3a6" stroke="%s" stroke-width="1"/>' % (mx, my, WHEEL_R * SS * 0.8, GOLD))
            w('<circle cx="%.1f" cy="%.1f" r="2.6" fill="%s"/>' % (bx, by, DARK))
        w('<circle cx="%.1f" cy="%.1f" r="3" fill="%s"/>' % (ax, ay, GOLD))
        text(ax, ay - 7, "%d" % (k + 1), 11, "middle", "bold")
    for k, (gap, drop, name) in enumerate(HANDOVERS):
        gx, gy = cut(CANDLE_MID[1] + ROPE_ALONG[k] + ROPE_END + gap / 2.0, ROPE_LOW[k])
        text(gx, cut(0, 21.0)[1], name, 10, "middle", "bold", PURPLE)
    # the 8 m line under the chandeliers, and the arrows of the three passes
    a, b = cut(STAGE[3], CANDLE_TOP - MAX_DROP), cut(CAKE[1] - TIERS[1][0], CANDLE_TOP - MAX_DROP)
    w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1.2" stroke-dasharray="6 4"/>' % (a[0], a[1], b[0], b[1], RED))
    text(b[0] + 4, b[1] - 4, "8 m 선", 10, "start", "bold", RED)
    text(RX, 1006, "붉은 점선(8 m 선) = 초 위에서 8 m 아래. 샹들리에에서 그 밑으로 떨어지면 바닥에 닿기 전에 윗단(체크포인트)에서 다시 시작한다.", 11, "start", "normal", RED)
    for (z0, z1, y, label) in ((ARM_Z0 + 1.0, HEAD_Z0 - 1.0, -2.6, "② 식탁을 따라 북쪽으로 (양옆의 식탁 — 점선으로 그린 것)"), (HEAD_Z0 + 2.0, STAGE[3] + 1.0, -4.4, ""),
                               (STAGE[3] + 3.0, CAKE[1] - 9.0, 23.6, "⑦ 샹들리에로 다시 북쪽으로")):
        a, b = cut(z0, y), cut(z1, y)
        d = 1 if b[0] > a[0] else -1
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" stroke-width="1.6"/>' % (a[0], a[1], b[0], b[1], DARK))
        w('<path d="M %.1f %.1f l %d -5 l 0 10 z" fill="%s"/>' % (b[0], b[1], -9 * d, DARK))
        text((a[0] + b[0]) / 2, a[1] - 5, label, 11, "middle", "bold", DARK)
    back = cut(HEAD_Z0 + 3.0, -4.4)
    text(back[0] + 8, back[1] + 4, "③ ④ 춤에 실려 남쪽으로", 11, "start", "bold", DARK)
    for label, z, y, anchor in (("윗단 11.6", 0.6, LOFT_Y + 0.6, "start"), ("배식실 (입구)", 0.6, 2.6, "start"), ("악단석 7.4", STAGE[2] + 0.2, 5.2, "start"),
                                ("⑤ 하프", HARP_Z + 0.6, 8.8, "start"), ("⑥ 초 14.8", CANDLE_MID[1] - 1.4, CANDLE_TOP + 0.9, "end"), ("가운데 단", (ISLAND[2] + ISLAND[3]) / 2, TABLE - 2.2, "middle"),
                                ("상석 4.4", HEAD_Z0 + 5.0, TABLE - 2.2, "middle"), ("⑧ 케이크", CAKE[1], 8.4, "middle"), ("2단 턱 10.2", CAKE[1] + TIERS[1][0] + 0.6, TIERS[1][1] + 0.5, "start"),
                                ("⑨ 칼등", CAKE[1] + 6.4, TIERS[4][1] + 1.2, "middle"), ("발코니 18.2", BALCONY[2] + 1.0, BALCONY_Y + 1.2, "start"), ("출구 →", BALCONY[3] - 1.0, BALCONY_Y + 3.0, "end")):
        p = cut(z, y)
        text(p[0], p[1], label, 11, anchor, "bold", WALNUT)
    for h in (0, TABLE, STAGE_Y, LOFT_Y, CANDLE_TOP, BALCONY_Y):
        p = cut(0, h)
        text(SX - 4, p[1] + 4, "%.1f" % h, 10, "end", "normal", GREY)

    # the height profile, the way laid out straight
    EX, EY, EH = 80.0, 1296.0, 12.0
    flat = [0.0]
    for a, b in zip(WAY, WAY[1:]):
        flat.append(flat[-1] + math.hypot(b[0] - a[0], b[2] - a[2]))
    ES = 1320.0 / flat[-1]
    prof = [(EX + s * ES, EY - p[1] * EH) for s, p in zip(flat, WAY)]
    text(34, 1022, "길을 펴서 본 높이 — 가로는 지나간 거리, 세로는 높이. 굵은 주황 = 타고 오르는 곳, 보라 = 실려 가는 곳(나이트 · 여왕 · 샹들리에)", 13, "start", "bold")
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
            ("식탁 위", "① 식탁보", "+4.4 실려 오름", -62, -30), ("나이트의 칸", "② 건배하는 식탁", "달리기 %.0f m (북쪽으로)" % TABLE_RUN, -200, -22),
            ("나이트에 매달림", "③ 나이트", "ㄱ자 도약 둘 (남쪽으로)", 22, 36), ("여왕의 어깨", "④ 왈츠", "+3.0, 반 바퀴 실려 감", 10, -26),
            ("하프 목", "⑤ 하프", "줄 2.2 ~ 4.2", -76, -26), ("초 위", "⑥ 촛대", "+3.2, 촛농", -76, -26),
            ("샹들리에 3", "⑦ 샹들리에 다섯", "매달려 건넘 (다시 북쪽으로) · 내려감 3.2", -80, -66), ("3단 위", "⑧ 케이크", "+2.8 세 번", -104, 42),
            ("발코니", "⑨ 칼등 · 발코니", "출구 18.2", -30, -30)):
        at = prof[index[key]]
        text(at[0] + ddx, at[1] + ddy, label, 12, "start", "bold")
        text(at[0] + ddx, at[1] + ddy + 14, sub, 11, "start", "normal", "#4a433c")
    total, level = way_length()
    text(EX + 1320 + 30, EY + 34, "길 약 %.0f m · 오르는 높이를 다 더하면 %.0f m" % (total, sum(max(0.0, b[1] - a[1]) for a, b in zip(WAY, WAY[1:]))), 11, "end")

    # the beats, with the time budget beside them
    rows = (
        ("배식실 통로", "악단석 밑의 입구에서 옆으로 나가 한쪽 식탁의 남쪽 끝까지 달린다", "달리기"),
        ("① 식탁보 — 상 차리기", "시종들이 식탁보를 감아 올린다. 올라가는 줄에 매달려 식탁 위로 실려 간다", "실려 가기"),
        ("② 건배하는 식탁", "잔 여섯 쌍이 차례로 '챙' 부딪친다. 식탁을 따라 홀 끝까지, 모퉁이를 돌아 상석 가운데로", "달리기"),
        ("③ 나이트의 도약", "상석 가운데 칸에 내려앉은 나이트의 덮개 천에 매달려, 두 번 뛰어 무도장의 가운데 단에 내린다", "매달려 실려 가기"),
        ("④ 왈츠", "가운데 단 앞에 멈춘 여왕의 드레스를 타고 어깨에 올라, 반 바퀴 실려 가 악단석 앞에서 내린다", "움직이는 벽 오르기"),
        ("⑤ 하프", "하프 줄을 타고 윗단으로. 연주자가 줄을 차례로 튕기고, 튕겨진 줄에서는 떨어진다", "줄 타기 · 박자"),
        ("⑥ 큰 촛대", "촛농이 흘러내리는 초를 옆으로 피하며 오른다", "피하며 오르기"),
        ("⑦ 샹들리에 다섯", "홀 한가운데의 샹들리에에 매달려, 끝에서 만나는 다음 샹들리에로. 발밑에서 왈츠와 나이트가 돈다", "매달려 건너기"),
        ("⑧ 케이크 — 커팅", "층마다 반대로 도는 케이크의 크림 줄을 타고 세 층. 꼭대기 층은 칼 쪽으로 실려 가기 전에", "도는 벽 읽기"),
        ("⑨ 칼등과 발코니", "케이크에 꽂힌 칼의 등(폭 1 m)을 걸어 상석 위 왕의 발코니로, 출구", "달리기"),
    )
    ty = 1366.0
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
    text(34, ty, "무도장은 ㄷ자 식탁(위는 병과 꽃의 울타리, 아래는 바닥까지 드리운 식탁보)과 악단석에 둘러싸여 입구에서 바로 들어갈 수 없다. 떨어진 폰은 바닥 칸의 나이트를 잡고 돌아온다.", 12)
    ty += 19
    text(34, ty, "낙사는 '8 m 넘게 떨어지면 체크포인트에서 다시'(기존 규칙) 하나다: 샹들리에에서, 케이크 위층에서. 수프 솥은 없앴다.", 12)
    lx, ly = 34.0, ty + 34
    for colour, label in ((ORANGE, "타는 면 · 줄"), (PURPLE, "움직이는 것"), (SLATE, "울타리 · 못 잡는 면"), ("#3f8f5a", "체크포인트")):
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
