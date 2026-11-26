# The rooks' rampart walk (룩 성벽길, Pawn Rush 1-5, early obstacle course, level design v0.1): the one place its
# numbers are worked out. Section frame: the entry's threshold is the origin, x east, y up, z north (the way in).
#
# The place: the royal castle's outer defences on a rock spur between two gorges (the concept picture's cliffs, river
# and waterfalls). Three curtain walls cross the spur one above the other - the outer wall, the middle wall with the
# great square tower in it, the inner wall with the inner gatehouse - with the lower ward between the first two and a
# rock-cut ditch between the last two. Today is the garrison's siege drill: the rooks (the castle's towers come alive,
# its guards) hold the walls and the pawns are the storming party. The way climbs the walls in switchbacks:
# up the outer wall's ladders, east along its walk over the outer gate, north in a siege tower across the lower ward,
# west along the middle wall across the rooks' drill square on the great tower, north on the cranes' rook or knight
# over the ditch, east along the inner wall, up the king's banner on the gatehouse, north over the keep bridge.
#
#   python rampart_layout.py                 prints the layout, the devices' cycles, the stamina, the time budget
#   python rampart_layout.py <plan.svg>      also draws the plan, the height along the way and the time budget
#
# rampart_blockout.py (the Blender stills) runs this file for the same numbers and for where every moving thing is at
# a time t. Nothing here is in Unity yet.
import math
import sys


def ease(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3.0 - 2.0 * k)


def lerp(a, b, k):
    return a + (b - a) * k


def on_line(p, q, x):
    # The z of the segment p-q at x (p, q are (x, z)).
    k = (x - p[0]) / (q[0] - p[0])
    return lerp(p[1], q[1], k)


# ---------------------------------------------------------------- pawn numbers (RagdollTuning.asset, RagdollPawn)
CLIMB_UP, STAMINA, ROOK, RUN = 1.2, 8.0, 0.7, 2.5
CLIMB_MIN_DEG = 55.0                             # a face less steep than this is not climbed: a climber on it lets go
JUMP_LEDGE = 0.6                                 # stepped or jumped onto; anything higher is climbed
MAX_DROP = 8.0                                   # FallDistanceRespawn

# ---------------------------------------------------------------- the levels
GLACIS = WARD = 0.0                              # the approach in front of the outer wall; the lower ward behind it
W1_Y, W2_Y, W3_Y, GATE_Y = 3.5, 9.0, 14.5, 20.0  # the three walls' walks and the inner gatehouse's roof
BOARD_Y = W2_Y + 2.0                             # the great tower's roof, two steps over the middle wall's walk
DITCH_Y, BAILEY_Y, RIVER_Y = -6.0, 9.0, -32.0    # the rock-cut ditch's floor, the inner bailey, the river in the gorges
SPUR_X = 46.0                                    # the spur's cliffs: |x| beyond this is the gorge
WALK, PARAPET, MERLON_H, SILL_H, MERLON_L, CRENEL = 6.0, 1.0, 1.8, 0.9, 1.8, 1.2
WALL_T = WALK + PARAPET                          # a curtain wall: the parapet on its outer (south) side, then the walk

# Each wall by its outer face, west to east, as (x, z) points; it is WALL_T thick, inward (north) of that line.
WALL1 = [(-44.0, 25.0), (-24.0, 11.0), (24.0, 11.0), (44.0, 25.0)]
WALL2_W = [(-44.0, 51.0), (-11.6, 46.0)]         # west of the great tower (further north: the board is crossed corner to corner)
WALL2_E = [(11.6, 38.0), (44.0, 47.0)]           # east of it
WALL3_W = [(-42.0, 64.0), (-10.0, 70.0)]         # west of the inner gatehouse
WALL3_E = [(10.0, 70.0), (42.0, 64.0)]

# Round towers (rooks: flat crenellated tops), x, z, radius, top.
TOWERS = [(-44.0, 28.0, 5.0, 9.0), (-24.0, 14.5, 4.2, 8.0), (24.0, 14.5, 4.2, 8.0), (44.0, 28.0, 5.0, 9.0),
          (-44.0, 54.0, 5.0, 14.5), (44.0, 50.0, 5.0, 14.5), (-42.0, 67.0, 5.0, 19.5), (42.0, 67.0, 5.0, 19.5)]

# ---------------------------------------------------------------- ① the siege-ladder drill (the outer wall's face)
# Six scaling ladders lean in six crenels of the outer wall's middle stretch. Two rook defenders on the walk push
# them off with forked poles, one after another: the ladder tips back from the wall, past upright, and falls onto the
# glacis behind its foot; the attackers' crew there hauls it up again by its ropes. Whoever is on it when it passes
# upright is let go (and whoever stands where it falls is knocked down).
LADDERS = [-18.5, -15.5, -12.5, -9.5, -6.5, -3.5]
LEAN = 75.0                                      # degrees from the ground on the wall's side, resting in the crenel
FALLEN = 180.0                                   # lying on the glacis behind its foot
LADDER_CLIMB = W1_Y + SILL_H                     # 4.4 m to the crenel's sill
LADDER_L = LADDER_CLIMB / math.sin(math.radians(LEAN)) + 0.6
PUSH_CYCLE = 9.0                                 # each ladder is pushed once every 9 s
PUSH_AT = [0.0, 3.0, 6.0, 1.5, 4.5, 7.5]         # defender A: ladders 0-2, defender B: 3-5
WIND, TIP, LIE, RAISE = 0.6, 0.9, 0.6, 1.4       # the pole lowered (the warning), the fall, on the ground, hauled up


def ladder_angle(i, t):
    # Degrees from the ground on the wall's side at time t: LEAN against the wall, 90 upright, FALLEN on the glacis.
    u = (t - PUSH_AT[i]) % PUSH_CYCLE
    if u < WIND:
        return LEAN
    u -= WIND
    if u < TIP:
        return LEAN + (FALLEN - LEAN) * (u / TIP) ** 2     # slow off the wall, then faster and faster
    u -= TIP
    if u < LIE:
        return FALLEN
    u -= LIE
    if u < RAISE:
        return FALLEN - (FALLEN - LEAN) * ease(u / RAISE)
    return LEAN


def ladder_climbable(i, t):
    return ladder_angle(i, t) <= 90.0


def defender_x(d, t):
    # Where defender d (0: ladders 0-2, 1: ladders 3-5) stands: at each ladder while it pushes it, walking between.
    first = 3 * d
    u = (t - PUSH_AT[first]) % PUSH_CYCLE
    step = PUSH_CYCLE / 3.0
    k = int(u // step)
    v = u - k * step
    here = LADDERS[first + k]
    there = LADDERS[first + (k + 1) % 3]
    hold = WIND + TIP
    return here if v < hold else lerp(here, there, ease((v - hold) / (step - hold)))


LADDER_WINDOW = PUSH_CYCLE - WIND - TIP - LIE - RAISE    # resting in its crenel, before the next pole comes down

# ---------------------------------------------------------------- ② the murder holes over the outer gate
# The walk crosses the outer gate's roof: 5 x 3 trapdoors of 2 m over the gate passage (3.5 m down). The defenders
# drill dropping sandbags on the attackers below: the light squares open, then the dark ones, each after their lids
# rattle. One step on the shut colour at a time, the way a bishop goes.
GATE_X = (0.0, 10.0)                             # the outer gate (its passage at y 0, its portcullis down today)
HOLE = 2.0
HOLES_X, HOLES_Z = 5, 3
HOLE_RATTLE, HOLE_OPEN, HOLE_SHUT = 0.4, 1.2, 0.6
HOLE_CYCLE = 2 * (HOLE_RATTLE + HOLE_OPEN + HOLE_SHUT)


def trapdoor(i, j, t):
    # 0 shut, 1 rattling (about to open), 2 open; light squares (i + j even) first.
    u = t % HOLE_CYCLE
    half = HOLE_CYCLE / 2.0
    mine = 0 if (i + j) % 2 == 0 else 1
    if (u >= half) != (mine == 1):
        return 0
    u %= half
    return 1 if u < HOLE_RATTLE else 2 if u < HOLE_RATTLE + HOLE_OPEN else 0


# ---------------------------------------------------------------- ③ the siege towers across the lower ward
# Two wheeled siege towers (belfries) shuttle on ruts between the outer wall's inner face and the middle wall's outer
# face, pushed by their crews. Docked at the outer wall, their open back takes the pawns in on the first floor (level
# with the outer wall's walk); they climb two inner ladders while it rolls; docked at the middle wall, the drop-bridge
# falls into a crenel and they cross. Whoever is not up by then rides back and tries again.
SIEGE_X = [13.5, 18.0]
SIEGE_W = 4.0                                    # square footprint
SIEGE_FLOORS = [W1_Y, W1_Y + 3.2, W1_Y + 6.4]    # first, middle, top (the top level with the middle wall's sills)
SIEGE_Z0 = 11.0 + WALL_T + SIEGE_W / 2.0         # its middle when docked at the outer wall (back to the wall)


def wall2_face_z(x):
    return on_line(WALL2_E[0], WALL2_E[1], x) if x > 0 else on_line(WALL2_W[0], WALL2_W[1], x)


SIEGE_Z1 = [wall2_face_z(x) - SIEGE_W / 2.0 for x in SIEGE_X]
BOARDING, ROLL, BRIDGE_MOVE, BRIDGE_OPEN, ROLL_BACK = 4.0, 8.0, 0.6, 3.4, 7.4
SIEGE_CYCLE = BOARDING + ROLL + BRIDGE_MOVE + BRIDGE_OPEN + BRIDGE_MOVE + ROLL_BACK
SIEGE_DELAY = [0.0, SIEGE_CYCLE / 2.0]


def siege(k, t):
    # (z of its middle, the drop-bridge's angle: 90 up, 0 down onto the wall).
    u = (t - SIEGE_DELAY[k]) % SIEGE_CYCLE
    z0, z1 = SIEGE_Z0, SIEGE_Z1[k]
    if u < BOARDING:
        return z0, 90.0
    u -= BOARDING
    if u < ROLL:
        return lerp(z0, z1, ease(u / ROLL)), 90.0
    u -= ROLL
    if u < BRIDGE_MOVE:
        return z1, 90.0 * (1.0 - ease(u / BRIDGE_MOVE))
    u -= BRIDGE_MOVE
    if u < BRIDGE_OPEN:
        return z1, 0.0
    u -= BRIDGE_OPEN
    if u < BRIDGE_MOVE:
        return z1, 90.0 * ease(u / BRIDGE_MOVE)
    u -= BRIDGE_MOVE
    return lerp(z1, z0, ease(u / ROLL_BACK)), 90.0


SIEGE_CLIMB = SIEGE_FLOORS[2] - SIEGE_FLOORS[0]

# ---------------------------------------------------------------- ④ the rooks' drill square on the great tower
# The middle wall's great square tower (itself a rook) has a 9 x 9 board on its roof where four rooks drill. A rook
# moves the way a rook does: along its rank or file, the whole way across, fast. Its line glows a second before; a
# pawn on it is swept to the parapet and knocked down. Two rooks run along files (across the way), two along ranks.
BOARD_N, SQUARE = 9, 2.4
BOARD_X0, BOARD_Z0 = -BOARD_N * SQUARE / 2.0, 36.2
GREAT_TOWER = (-11.6, 11.6, 35.4, 58.6)          # x0, x1, z0, z1 with its parapet
BOARD_ENTRY_Z = BOARD_Z0 + 2.5 * SQUARE          # the way comes on at the east end of rank 2 (from the middle wall's east walk)
BOARD_EXIT_Z = BOARD_Z0 + 5.5 * SQUARE           # and goes off at the west end of rank 5 (onto the west walk)
ROOK_REST, ROOK_GLOW, ROOK_SPEED = 2.0, 1.0, 8.0
ROOK_SLIDE = (BOARD_N - 1) * SQUARE / ROOK_SPEED
ROOK_CYCLE = 2 * (ROOK_REST + ROOK_GLOW + ROOK_SLIDE)
# (along "file" = north-south at column c, or "rank" = east-west at row r), its index, its delay
DRILL_ROOKS = [("file", 2, 0.0), ("rank", 3, ROOK_CYCLE / 4.0), ("file", 6, ROOK_CYCLE / 2.0), ("rank", 6, 3 * ROOK_CYCLE / 4.0)]


def square_mid(c, r):
    return BOARD_X0 + (c + 0.5) * SQUARE, BOARD_Z0 + (r + 0.5) * SQUARE


def drill_rook(n, t):
    # (x, z, glowing): it rests at one end, glows, slides to the other, rests, glows, slides back.
    kind, idx, delay = DRILL_ROOKS[n]
    u = (t - delay) % ROOK_CYCLE
    half = ROOK_CYCLE / 2.0
    back = u >= half
    u %= half
    k, glow = 0.0, False
    if u < ROOK_REST:
        k = 0.0
    elif u < ROOK_REST + ROOK_GLOW:
        glow = True
    else:
        k = min(1.0, (u - ROOK_REST - ROOK_GLOW) / ROOK_SLIDE)
    s = (1.0 - k) if back else k                 # 0 at the start end, 1 at the far end
    pos = s * (BOARD_N - 1)
    x, z = square_mid(idx, pos) if kind == "file" else square_mid(pos, idx)
    return x, z, glow


# ---------------------------------------------------------------- ⑤ the cranes: the rook's straight move and the knight's leap
# At the middle wall's west end two cranes reach over the ditch to a hoist door in the inner wall's parapet; their
# treadwheels are walked by the castle's workers. The gantry carries a giant rook the way a rook moves - straight up,
# straight across, straight down - and stops at each end: stand on its crown (the easy, slow way). The knight hangs
# from the next gantry and leaps the way a knight does, over the wall and back, never stopping: jump and grab its
# mane as it swings by low, let go over the walk (the fast, hard way).
def wall2_inner_z(x):
    return wall2_face_z(x) + WALL_T


def wall3_face_z(x):
    return on_line(WALL3_W[0], WALL3_W[1], x) if x < 0 else on_line(WALL3_E[0], WALL3_E[1], x)


ROOK_CRANE_X, KNIGHT_CRANE_X = -30.0, -24.5
CROWN_R = 2.5                                    # the giant rook's crown: a floor 5 m across, merlons round it
ROOK_BODY = 5.0
RC_LOW_Z = wall2_inner_z(ROOK_CRANE_X) + CROWN_R + 0.15
RC_HIGH_Z = wall3_face_z(ROOK_CRANE_X) - CROWN_R - 0.15
RC_TOP = W3_Y                                    # straight up to the inner wall's walk, then straight across to its hoist door
RC_BOARD, RC_UP, RC_ACROSS, RC_OFF = 3.5, 1.8, 1.2, 3.0
RC_CYCLE = RC_BOARD + RC_UP + RC_ACROSS + RC_OFF + RC_ACROSS + RC_UP
RC_DELAY = 7.85                                  # its own phase: nothing else waits on it


def rook_crane(t):
    # (crown's y, crown's z).
    u = (t - RC_DELAY) % RC_CYCLE
    lo, top = W2_Y, RC_TOP
    for length, f in ((RC_BOARD, lambda k: (lo, RC_LOW_Z)), (RC_UP, lambda k: (lerp(lo, top, ease(k)), RC_LOW_Z)),
                      (RC_ACROSS, lambda k: (top, lerp(RC_LOW_Z, RC_HIGH_Z, ease(k)))), (RC_OFF, lambda k: (top, RC_HIGH_Z)),
                      (RC_ACROSS, lambda k: (top, lerp(RC_HIGH_Z, RC_LOW_Z, ease(k))))):
        if u < length:
            return f(u / length)
        u -= length
    return lerp(top, lo, ease(u / RC_UP)), RC_LOW_Z


KNIGHT_H = 4.6                                   # the giant knight, base to ears
KNIGHT_R = 1.2                                   # its base's radius
KNIGHT_LOW = W2_Y - 1.8                          # its base at the near end: its neck level with a pawn's hands on the walk
KN_LOW_Z = wall2_inner_z(KNIGHT_CRANE_X) + 1.8   # 1.8 m off the walk's edge: a jump and a grab
KN_HIGH_Z = wall3_face_z(KNIGHT_CRANE_X) + PARAPET + 1.5   # set down on the inner wall's walk, in its hoist door
KN_HIGH_Y = W3_Y
KNIGHT_HOP = 4.0                                 # the leap's height over the straight line between its ends
KNIGHT_LEAP = 3.0                                # one leap, either way


def knight_crane(t):
    # (base's y, base's z): a leap there, a leap back, no stops (it turns round at the ends).
    u = t % (2 * KNIGHT_LEAP)
    back = u >= KNIGHT_LEAP
    k = (u % KNIGHT_LEAP) / KNIGHT_LEAP
    s = 0.5 * (1.0 - math.cos(math.pi * k))      # along: slow at the ends
    if back:
        s = 1.0 - s
    y = lerp(KNIGHT_LOW, KN_HIGH_Y, s) + KNIGHT_HOP * math.sin(math.pi * s)
    return y, lerp(KN_LOW_Z, KN_HIGH_Z, s)


# ---------------------------------------------------------------- ⑥ the king's banner on the inner gatehouse
# The inner wall's walk ends at the gatehouse's west face; the king's banner hangs there in two lengths, a stone ledge
# between them, up to the gatehouse's roof. The wind off the gorge comes in gusts: the pennants on the roof stream out
# first, then the banners billow out from the wall past the climbable angle - be on the ledge or over the top by then.
GATEHOUSE = (-10.0, 10.0, 70.0, 82.0)            # x0, x1, z0, z1; roof GATE_Y; twin rook towers on its south face
GATE_TOWERS = [(-6.5, 70.0, 4.0, GATE_Y + 2.5), (6.5, 70.0, 4.0, GATE_Y + 2.5)]
BANNER_Z = (71.6, 75.2)
LEDGE_Y = (W3_Y + GATE_Y) / 2.0
GUST_CYCLE, GUST_WARN, GUST_OUT, GUST_HOLD, GUST_BACK = 7.0, 1.0, 0.6, 1.0, 0.8
BILLOW = 40.0                                    # degrees out from the wall: the banner's face is then 50 from level


def banner_angle(t):
    u = t % GUST_CYCLE
    if u < GUST_WARN:
        return 0.0
    u -= GUST_WARN
    if u < GUST_OUT:
        return BILLOW * ease(u / GUST_OUT)
    u -= GUST_OUT
    if u < GUST_HOLD:
        return BILLOW
    u -= GUST_HOLD
    if u < GUST_BACK:
        return BILLOW * (1.0 - ease(u / GUST_BACK))
    return 0.0


CALM = GUST_CYCLE - GUST_OUT - GUST_HOLD - GUST_BACK       # the banner flat on the wall (the warning included)
PITCH = LEDGE_Y - W3_Y

# ---------------------------------------------------------------- the keep bridge and the exit
BRIDGE = (-2.0, 2.0, GATEHOUSE[3], 98.0)         # over the inner bailey, level with the gatehouse's roof
EXIT = (0.0, GATE_Y, 98.0)

# ---------------------------------------------------------------- checkpoints (slot 0: 0 .. 7)
CHECKPOINTS = [("입구", 0.0, GLACIS, 3.0), ("바깥 성벽 위 (사다리 끝)", -11.0, W1_Y, 15.0), ("바깥 성벽 위 (공성탑 앞)", 15.5, W1_Y, 15.0),
               ("가운데 성벽 동쪽", 20.0, W2_Y, 43.5), ("가운데 성벽 서쪽", -20.0, W2_Y, 51.0), ("안 성벽 서쪽 (기중기 끝)", -24.0, W3_Y, 71.0),
               ("안 문루 지붕", 0.0, GATE_Y, 76.0), ("본성 다리 끝", 0.0, GATE_Y, 96.0)]

# ---------------------------------------------------------------- the way (x, y, z, what, how it is travelled)
def the_way():
    lad = LADDERS[3]
    way = [(0.0, GLACIS, 0.5, "입구", "run"), (-6.0, GLACIS, 6.0, "", "run"), (lad, GLACIS, 9.0, "공성 사다리", "run"),
           (lad, LADDER_CLIMB, 11.4, "", "climb"), (lad, W1_Y, 13.5, "바깥 성벽 위", "run"),
           (GATE_X[0] - 0.5, W1_Y, 15.0, "살인 구멍", "run"), (GATE_X[1] + 0.5, W1_Y, 15.0, "", "run"),
           (SIEGE_X[0], W1_Y, 16.5, "공성탑", "run"), (SIEGE_X[0], SIEGE_FLOORS[0], SIEGE_Z0, "", "run"),
           (SIEGE_X[0], SIEGE_FLOORS[2], SIEGE_Z1[0] - 0.5, "", "climb"), (SIEGE_X[0], W2_Y, wall2_face_z(SIEGE_X[0]) + 2.5, "가운데 성벽 위", "run"),
           (GREAT_TOWER[1] + 1.5, W2_Y, BOARD_ENTRY_Z, "", "run"), (GREAT_TOWER[1] - 0.5, BOARD_Y, BOARD_ENTRY_Z, "룩의 훈련장", "run")]
    # Across the board corner to corner, round the rooks' lines: a little longer than straight.
    zig = [(BOARD_X0 + 6.5 * SQUARE, BOARD_ENTRY_Z), (BOARD_X0 + 5.5 * SQUARE, BOARD_Z0 + 4.5 * SQUARE), (BOARD_X0 + 3.5 * SQUARE, BOARD_Z0 + 4.5 * SQUARE),
           (BOARD_X0 + 1.5 * SQUARE, BOARD_EXIT_Z)]
    way += [(x, BOARD_Y, z, "", "run") for x, z in zig]
    way += [(GREAT_TOWER[0] + 0.5, BOARD_Y, BOARD_EXIT_Z, "", "run"), (GREAT_TOWER[0] - 1.5, W2_Y, BOARD_EXIT_Z, "", "run"),
            (ROOK_CRANE_X + 1.0, W2_Y, wall2_inner_z(ROOK_CRANE_X) - 0.8, "기중기", "run"),
            (ROOK_CRANE_X, W2_Y, RC_LOW_Z, "", "run"), (ROOK_CRANE_X, RC_TOP, RC_LOW_Z, "", "ride"), (ROOK_CRANE_X, RC_TOP, RC_HIGH_Z, "", "ride"),
            (ROOK_CRANE_X, W3_Y, RC_HIGH_Z, "", "ride"), (ROOK_CRANE_X + 1.0, W3_Y, wall3_face_z(ROOK_CRANE_X) + 3.0, "안 성벽 위", "run"),
            (GATEHOUSE[0] - 2.0, W3_Y, (BANNER_Z[0] + BANNER_Z[1]) / 2.0, "왕의 깃발", "run"),
            (GATEHOUSE[0] - 0.6, LEDGE_Y, (BANNER_Z[0] + BANNER_Z[1]) / 2.0, "", "climb"),
            (GATEHOUSE[0] - 0.4, GATE_Y, (BANNER_Z[0] + BANNER_Z[1]) / 2.0, "", "climb"), (GATEHOUSE[0] + 1.0, GATE_Y, (BANNER_Z[0] + BANNER_Z[1]) / 2.0, "안 문루 지붕", "run"),
            (0.0, GATE_Y, GATEHOUSE[3], "본성 다리", "run"), (EXIT[0], EXIT[1], EXIT[2], "출구", "run")]
    return way


WAY = the_way()


def way_length():
    total = level = 0.0
    for a, b in zip(WAY, WAY[1:]):
        flat = math.hypot(b[0] - a[0], b[2] - a[2])
        level += flat
        total += math.hypot(flat, b[1] - a[1])
    return total, level


def leg(i0, i1):
    # Level length of the way between two of its points.
    return sum(math.hypot(b[0] - a[0], b[2] - a[2]) for a, b in zip(WAY[i0:i1], WAY[i0 + 1:i1 + 1]))


# ---------------------------------------------------------------- stamina (climbing drains 1 a second while moving)
PITCHES = (
    ("① 공성 사다리 — 총안 턱까지", LADDER_CLIMB),
    ("③ 공성탑 — 안 사다리 한 칸", SIEGE_CLIMB / 2.0),
    ("⑤ 나이트 — 갈기에서 귀까지(빠른 길)", 1.6),
    ("⑥ 왕의 깃발 — 한 폭", PITCH),
)


def cost(metres):
    return metres / CLIMB_UP / STAMINA, metres / (CLIMB_UP * ROOK) / STAMINA


# ---------------------------------------------------------------- the time budget (the grand stair hall's way of counting)
# move = the way's length at a run plus climbing; wait = for the device, on average; fail = the chance of failing x its cost.
STAIR_HALL, GARDEN, CROWD = 157.0, 65.0, 1.12
SIEGE_WAIT = (SIEGE_CYCLE / 2.0 - BOARDING) ** 2 / 2.0 / (SIEGE_CYCLE / 2.0)
RC_WAIT = (RC_CYCLE - RC_BOARD) ** 2 / 2.0 / RC_CYCLE
BUDGET = (
    ("입구 → 사다리", leg(0, 2), leg(0, 2) / RUN, 0.0, 0.0),
    ("① 공성 사다리", LADDER_CLIMB, LADDER_CLIMB / CLIMB_UP + 0.8, 1.5 + 1.0, 0.3 * 7.0),
    ("성벽 1 위", leg(4, 5), leg(4, 5) / RUN, 0.0, 0.0),
    ("② 살인 구멍", leg(5, 6), leg(5, 6) / RUN, 1.2, 0.2 * 7.0),
    ("③ 공성탑", leg(6, 10), leg(6, 8) / RUN + ROLL + BRIDGE_MOVE + 1.5, SIEGE_WAIT, 0.15 * 12.0),
    ("성벽 2 동쪽", leg(10, 12), leg(10, 12) / RUN + 0.8, 0.0, 0.0),
    ("④ 룩의 훈련장", leg(12, 17), leg(12, 17) / RUN, 4.0, 0.35 * 4.0),
    ("성벽 2 서쪽", leg(17, 19), leg(17, 19) / RUN + 0.8, 0.0, 0.0),
    ("⑤ 룩 · 나이트 기중기", leg(19, 24), RC_UP + RC_ACROSS + 1.0, RC_WAIT, 0.1 * 8.0),
    ("성벽 3 위", leg(24, 25), leg(24, 25) / RUN, 0.0, 0.0),
    ("⑥ 왕의 깃발", 2 * PITCH, 2 * PITCH / CLIMB_UP + 0.8, 1.5, 0.25 * 6.0),
    ("문루 지붕 → 다리 → 출구", leg(27, len(WAY) - 1), leg(27, len(WAY) - 1) / RUN, 0.0, 0.0),
)


def budget_total():
    return sum(row[2] + row[3] + row[4] for row in BUDGET)


def clock(seconds):
    return "%d분 %02d초" % (int(seconds) // 60, int(round(seconds)) % 60)


def report():
    total, level = way_length()
    print("the way: %.0f m (%.0f m level), from the glacis (%.1f) to the keep bridge (%.1f)" % (total, level, GLACIS, GATE_Y))
    print("walls: walks %.1f / %.1f / %.1f, the great tower's board %.1f, the gatehouse's roof %.1f; ditch %.1f, bailey %.1f" % (
        W1_Y, W2_Y, W3_Y, BOARD_Y, GATE_Y, DITCH_Y, BAILEY_Y))
    print("① ladders at x %s, %.1f m long at %.0f deg, climb %.1f m; each pushed every %.1f s, standing %.1f s of it" % (
        " ".join("%.1f" % x for x in LADDERS), LADDER_L, LEAN, LADDER_CLIMB, PUSH_CYCLE, LADDER_WINDOW))
    up = sum(1 for n in range(int(PUSH_CYCLE / 0.01)) if ladder_climbable(0, n * 0.01)) * 0.01
    print("   on the wall's side of upright %.2f s of every %.1f (the push %.1f s after the pole comes down)" % (up, PUSH_CYCLE, WIND))
    print("   a pawn climbs it in %.1f s, a rook in %.1f s" % (LADDER_CLIMB / CLIMB_UP, LADDER_CLIMB / CLIMB_UP / ROOK))
    print("② trapdoors %d x %d of %.0f m over x %.0f .. %.0f; cycle %.1f s (rattle %.1f, open %.1f, all shut %.1f)" % (
        HOLES_X, HOLES_Z, HOLE, GATE_X[0], GATE_X[1], HOLE_CYCLE, HOLE_RATTLE, HOLE_OPEN, HOLE_SHUT))
    print("③ siege towers at x %s: z %.1f -> %s, cycle %.1f s (board %.0f, roll %.0f, bridge %.1f); climb %.1f m; wait %.1f s" % (
        " ".join("%.1f" % x for x in SIEGE_X), SIEGE_Z0, " / ".join("%.1f" % z for z in SIEGE_Z1), SIEGE_CYCLE, BOARDING, ROLL, BRIDGE_OPEN,
        SIEGE_CLIMB, SIEGE_WAIT))
    print("   pawn %.1f s up the two ladders, rook %.1f s, during the %.1f s roll" % (SIEGE_CLIMB / CLIMB_UP, SIEGE_CLIMB / CLIMB_UP / ROOK, ROLL + BRIDGE_MOVE))
    print("④ board %d x %d of %.1f m at x %.1f .. %.1f, z %.1f .. %.1f; rooks slide %.1f s, cycle %.1f s" % (
        BOARD_N, BOARD_N, SQUARE, BOARD_X0, -BOARD_X0, BOARD_Z0, BOARD_Z0 + BOARD_N * SQUARE, ROOK_SLIDE, ROOK_CYCLE))
    print("⑤ rook crane at x %.1f: crown z %.1f -> %.1f, up to %.1f, cycle %.1f s, wait %.1f s; knight at x %.1f: z %.1f -> %.1f, leap %.1f s, top %.1f" % (
        ROOK_CRANE_X, RC_LOW_Z, RC_HIGH_Z, RC_TOP, RC_CYCLE, RC_WAIT, KNIGHT_CRANE_X, KN_LOW_Z, KN_HIGH_Z, KNIGHT_LEAP,
        max(knight_crane(n * 0.05)[0] for n in range(120)) + KNIGHT_H))
    print("   ditch at the cranes: %.1f m (wall 2's inner edge z %.1f, wall 3's face z %.1f)" % (
        wall3_face_z(ROOK_CRANE_X) - wall2_inner_z(ROOK_CRANE_X), wall2_inner_z(ROOK_CRANE_X), wall3_face_z(ROOK_CRANE_X)))
    print("⑥ banner: two lengths of %.2f m, ledge %.2f; gust every %.1f s, %.0f deg out; calm %.1f s; pawn %.1f s a length, rook %.1f s" % (
        PITCH, LEDGE_Y, GUST_CYCLE, BILLOW, CALM, PITCH / CLIMB_UP, PITCH / CLIMB_UP / ROOK))
    print("gap: the great tower's north face z %.1f, the gatehouse's south towers from z %.1f" % (GREAT_TOWER[3], GATE_TOWERS[0][1] - GATE_TOWERS[0][2]))
    for name, metres in PITCHES:
        pawn, rook = cost(metres)
        print("  %-34s %.2f m  pawn %3.0f%%  rook %3.0f%%" % (name, metres, pawn * 100, rook * 100))
    for row in BUDGET:
        print("  %-18s %5.1f m  move %5.1f  wait %4.1f  fail %4.1f  = %5.1f" % (row[0], row[1], row[2], row[3], row[4], row[2] + row[3] + row[4]))
    t = budget_total()
    print("  total %.0f s (%s), with twelve players %.0f s (%s); the garden ~%s (12 players, median), the stair hall %s" % (
        t, clock(t), t * CROWD, clock(t * CROWD), clock(GARDEN), clock(STAIR_HALL)))


# ---------------------------------------------------------------- the drawing
INK, PAPER, STONE, STONE2, GOLD, RED, BLUE, GREEN, ORANGE, PURPLE, SLATE, WATER, GRASS, ROCK = (
    "#2b2622", "#fbf6ea", "#e9dcc2", "#cdbb98", "#c9972e", "#c8472f", "#3f6fb5", "#5f8a3c", "#e8853a", "#8a5cc2", "#8fa0ad",
    "#9cc3e0", "#cfe0b0", "#b8a68e")
BEATS = [("①", "공성 사다리", "바깥 성벽", ORANGE), ("②", "살인 구멍", "바깥 문 위", RED), ("③", "공성탑", "아래뜰", PURPLE),
         ("④", "룩의 훈련장", "큰 탑 지붕", BLUE), ("⑤", "룩 · 나이트 기중기", "바위 해자", GREEN), ("⑥", "왕의 깃발", "안 문루", GOLD)]


def draw(path):
    out = []

    def w(s):
        out.append(s)

    def text(x, y, s, size=12, anchor="start", weight="normal", fill=INK):
        w('<text x="%.1f" y="%.1f" font-size="%d" text-anchor="%s" font-weight="%s" fill="%s">%s</text>' % (x, y, size, anchor, weight, fill, s))

    # The plan: x -50 .. 50, z -6 .. 96 at S px a metre, north up.
    S, PX, PY = 7.2, 30.0, 84.0
    X0, Z1 = -50.0, 102.0

    def P(x, z):
        return PX + (x - X0) * S, PY + (Z1 - z) * S

    def poly(points, fill, stroke=INK, width=1.0, opacity=1.0, dash=None):
        d = " ".join("%.1f,%.1f" % P(x, z) for x, z in points)
        w('<polygon points="%s" fill="%s" stroke="%s" stroke-width="%.1f" fill-opacity="%.2f"%s/>' % (
            d, fill, stroke, width, opacity, ' stroke-dasharray="%s"' % dash if dash else ""))

    def line(points, stroke, width=2.0, dash=None, opacity=1.0):
        d = " ".join("%.1f,%.1f" % P(x, z) for x, z in points)
        w('<polyline points="%s" fill="none" stroke="%s" stroke-width="%.1f" stroke-opacity="%.2f"%s stroke-linejoin="round"/>' % (
            d, stroke, width, opacity, ' stroke-dasharray="%s"' % dash if dash else ""))

    def circle(x, z, r, fill, stroke=INK, width=1.0):
        cx, cy = P(x, z)
        w('<circle cx="%.1f" cy="%.1f" r="%.1f" fill="%s" stroke="%s" stroke-width="%.1f"/>' % (cx, cy, r * S, fill, stroke, width))

    def wall(points, fill):
        # The wall's band: its outer face and WALL_T inward of each segment (joined at the bends).
        inner = []
        for i in range(len(points)):
            a = points[max(0, i - 1)]
            b = points[min(len(points) - 1, i + 1)]
            dx, dz = b[0] - a[0], b[1] - a[1]
            n = math.hypot(dx, dz)
            nx, nz = -dz / n, dx / n
            inner.append((points[i][0] + nx * WALL_T, points[i][1] + nz * WALL_T))
        poly(points + inner[::-1], fill, INK, 1.2)
        for i in range(len(points) - 1):        # the parapet's line
            a, b = points[i], points[i + 1]
            dx, dz = b[0] - a[0], b[1] - a[1]
            n = math.hypot(dx, dz)
            nx, nz = -dz / n, dx / n
            line([(a[0] + nx * PARAPET, a[1] + nz * PARAPET), (b[0] + nx * PARAPET, b[1] + nz * PARAPET)], INK, 0.6, "3,2")

    W, H = 1640, 930
    w('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" font-family="Malgun Gothic, Apple SD Gothic Neo, sans-serif">' % (W, H))
    w('<rect width="%d" height="%d" fill="%s"/>' % (W, H, PAPER))
    text(30, 34, "룩 성벽길 (1-5, 초반 장애물) — 레벨 디자인 초안 v0.1", 22, weight="bold")
    text(30, 56, "바위 능선을 가로지르는 성벽 셋을 지그재그로 오른다 · 공성 훈련의 날 · 위에서 본 배치(북쪽이 위), 높이는 m", 13, fill="#5a524a")
    # the gorges, the spur, the ward, the ditch, the bailey
    poly([(X0, -6), (-SPUR_X, -6), (-SPUR_X, Z1), (X0, Z1)], WATER, "none", 0, 0.55)
    poly([(SPUR_X, -6), (50, -6), (50, Z1), (SPUR_X, Z1)], WATER, "none", 0, 0.55)
    poly([(-SPUR_X, -6), (SPUR_X, -6), (SPUR_X, Z1), (-SPUR_X, Z1)], GRASS, INK, 1.0, 0.9)
    ditch = [(-SPUR_X, wall2_inner_z(-SPUR_X)), (-11.6, wall2_inner_z(-11.6)), (-11.6, GREAT_TOWER[3]), (11.6, GREAT_TOWER[3]),
             (11.6, wall2_inner_z(11.6)), (SPUR_X, wall2_inner_z(SPUR_X)), (SPUR_X, wall3_face_z(SPUR_X)), (10.0, 70.0), (-10.0, 70.0),
             (-SPUR_X, wall3_face_z(-SPUR_X))]
    poly(ditch, ROCK, INK, 1.0, 0.9)
    text(*P(28, 59.5), s="바위 해자 (바닥 −6)", size=11, anchor="middle")
    text(*P(-30, 31), s="아래뜰 (0)", size=11, anchor="middle")
    text(*P(-30, 4), s="성 앞 비탈 (0)", size=11, anchor="middle")
    text(*P(-26, 84), s="안뜰 (9)", size=11, anchor="middle")
    text(*P(-49, 99), s="협곡", size=12, fill="#2c5a80")
    text(*P(47.5, 99), s="협곡", size=12, fill="#2c5a80")
    # walls, towers, the great tower, the gatehouse, the bridge
    wall(WALL1, STONE)
    wall(WALL2_W, STONE)
    wall(WALL2_E, STONE)
    wall(WALL3_W, STONE)
    wall(WALL3_E, STONE)
    for x, z, r, top in TOWERS:
        circle(x, z, r, STONE2, INK, 1.2)
        text(*P(x, z - 0.8), s="%.1f" % top, size=10, anchor="middle")
    poly([(GATE_X[0], 3.0), (GATE_X[1], 3.0), (GATE_X[1], 18.0), (GATE_X[0], 18.0)], STONE2, INK, 1.2)
    for i in range(HOLES_X):
        for j in range(HOLES_Z):
            x0, z0 = GATE_X[0] + i * HOLE, 12.0 + j * HOLE
            poly([(x0, z0), (x0 + HOLE, z0), (x0 + HOLE, z0 + HOLE), (x0, z0 + HOLE)], "#f6efe0" if (i + j) % 2 == 0 else "#6b5a45", INK, 0.5)
    text(*P(4.0, 5.0), s="바깥 문 (닫힘)", size=10, anchor="middle")
    g = GREAT_TOWER
    poly([(g[0], g[2]), (g[1], g[2]), (g[1], g[3]), (g[0], g[3])], STONE2, INK, 1.4)
    for c in range(BOARD_N):
        for r in range(BOARD_N):
            x0, z0 = BOARD_X0 + c * SQUARE, BOARD_Z0 + r * SQUARE
            poly([(x0, z0), (x0 + SQUARE, z0), (x0 + SQUARE, z0 + SQUARE), (x0, z0 + SQUARE)], "#f6efe0" if (c + r) % 2 == 0 else "#7a6650", "none", 0)
    for n, (kind, idx, delay) in enumerate(DRILL_ROOKS):
        a = square_mid(idx, 0) if kind == "file" else square_mid(0, idx)
        b = square_mid(idx, BOARD_N - 1) if kind == "file" else square_mid(BOARD_N - 1, idx)
        line([a, b], BLUE, 3.0, "6,3", 0.85)
        circle(a[0], a[1], 0.95, BLUE, INK, 1.0)
    text(*P(0, 60.0), s="큰 탑 (룩의 훈련장, 지붕 11)", size=11, anchor="middle", weight="bold")
    gh = GATEHOUSE
    poly([(gh[0], gh[2]), (gh[1], gh[2]), (gh[1], gh[3]), (gh[0], gh[3])], STONE2, INK, 1.4)
    for x, z, r, top in GATE_TOWERS:
        circle(x, z, r, STONE2, INK, 1.2)
    text(*P(0, 78.5), s="안 문루 (지붕 20)", size=11, anchor="middle", weight="bold")
    poly([(BRIDGE[0], BRIDGE[2]), (BRIDGE[1], BRIDGE[2]), (BRIDGE[1], BRIDGE[3]), (BRIDGE[0], BRIDGE[3])], STONE, INK, 1.0)
    poly([(-14, EXIT[2]), (14, EXIT[2]), (14, EXIT[2] + 3.0), (-14, EXIT[2] + 3.0)], STONE2, INK, 1.2)
    text(*P(0, EXIT[2] + 1.0), s="본성 (출구)", size=11, anchor="middle", weight="bold")
    text(*P(5.2, 90), s="본성 다리 (20)", size=10)
    # the devices
    for x in LADDERS:
        line([(x, 11.0 - 1.4), (x, 11.0)], ORANGE, 4.0)
    for d in range(2):
        circle(defender_x(d, 0.0), 13.0, 0.8, "#efe6d5", INK, 1.0)
    for k, x in enumerate(SIEGE_X):
        poly([(x - 2, SIEGE_Z0 - 2), (x + 2, SIEGE_Z0 - 2), (x + 2, SIEGE_Z0 + 2), (x - 2, SIEGE_Z0 + 2)], PURPLE, INK, 1.0, 0.85)
        line([(x, SIEGE_Z0), (x, SIEGE_Z1[k])], PURPLE, 1.6, "4,3")
        poly([(x - 2, SIEGE_Z1[k] - 2), (x + 2, SIEGE_Z1[k] - 2), (x + 2, SIEGE_Z1[k] + 2), (x - 2, SIEGE_Z1[k] + 2)], "none", PURPLE, 1.2, 1.0, "3,2")
    for x, z0, z1 in ((ROOK_CRANE_X, RC_LOW_Z, RC_HIGH_Z), (KNIGHT_CRANE_X, KN_LOW_Z, KN_HIGH_Z)):
        line([(x - 1.6, z0 - 3.5), (x - 1.6, z1 + 3.0)], "#7a5a3a", 2.0)
        line([(x + 1.6, z0 - 3.5), (x + 1.6, z1 + 3.0)], "#7a5a3a", 2.0)
    circle(ROOK_CRANE_X, RC_LOW_Z, CROWN_R, GREEN, INK, 1.0)
    circle(KNIGHT_CRANE_X, KN_LOW_Z, 1.3, "#a4c37a", INK, 1.0)
    poly([(GATEHOUSE[0] - 0.4, BANNER_Z[0]), (GATEHOUSE[0], BANNER_Z[0]), (GATEHOUSE[0], BANNER_Z[1]), (GATEHOUSE[0] - 0.4, BANNER_Z[1])], GOLD, INK, 1.0)
    # the way
    line([(p[0], p[2]) for p in WAY], RED, 3.2, None, 0.9)
    for x, z in ((WAY[0][0], WAY[0][2]), (EXIT[0], EXIT[2])):
        circle(x, z, 1.0, RED, INK, 1.0)
    # the beat labels on the plan
    for (num, name, where, col), (x, z) in zip(BEATS, ((-11.0, 6.0), (5.0, 20.8), (24.0, 28.0), (0.0, 47.0), (-27.2, 61.5), (-16.0, 77.0))):
        cx, cy = P(x, z)
        w('<circle cx="%.1f" cy="%.1f" r="11" fill="%s" stroke="%s" stroke-width="1.2"/>' % (cx, cy, col, INK))
        text(cx, cy + 4.5, num, 13, "middle", "bold", "#ffffff")
    for k, (name, x, y, z) in enumerate(CHECKPOINTS):
        cx, cy = P(x, z)
        w('<rect x="%.1f" y="%.1f" width="9" height="9" fill="#2a7a3a" stroke="%s" stroke-width="0.8"/>' % (cx - 4.5, cy - 4.5, INK))
    # the height along the way
    RX, RY, RW, RH = 790.0, 96.0, 820.0, 300.0
    total, level = way_length()
    w('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#ffffff" stroke="%s" stroke-width="1"/>' % (RX, RY, RW, RH, INK))
    text(RX, RY - 10, "길을 따라 본 높이 (가로: 길 %.0f m, 세로: 높이 m)" % total, 13, weight="bold")
    ymin, ymax = -2.0, 24.0

    def R(s, y):
        return RX + 10 + s / total * (RW - 20), RY + RH - 14 - (y - ymin) / (ymax - ymin) * (RH - 28)

    for y in range(0, 25, 4):
        x0, yy = R(0, y)
        w('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#e5ddd0" stroke-width="1"/>' % (RX + 10, yy, RX + RW - 10, yy))
        text(RX + 4, yy + 4, "%d" % y, 9, "end", fill="#7a7068")
    s, pts, marks = 0.0, [R(0, WAY[0][1])], []
    for a, b in zip(WAY, WAY[1:]):
        s += math.hypot(math.hypot(b[0] - a[0], b[2] - a[2]), b[1] - a[1])
        pts.append(R(s, b[1]))
        if b[3]:
            marks.append((s, b[1], b[3], b[4]))
    w('<polyline points="%s" fill="none" stroke="%s" stroke-width="2.6" stroke-linejoin="round"/>' % (" ".join("%.1f,%.1f" % p for p in pts), RED))
    for i, (s_, y_, name, how) in enumerate(marks):
        x, yy = R(s_, y_)
        w('<circle cx="%.1f" cy="%.1f" r="3" fill="%s"/>' % (x, yy, INK))
        text(x + 3, yy - 6 - (i % 2) * 11, name, 10)
    # the beats' table
    TX, TY = 790.0, 440.0
    text(TX, TY, "구간", 13, weight="bold")
    rows = [("①", "공성 사다리", "룩 수비병이 장대로 밀어 넘기는 사다리를, 막 세워진 것으로 골라 오른다 (4.4 m)"),
            ("②", "살인 구멍", "바깥 문 위 함정문 5 × 3 — 흰 칸, 검은 칸이 번갈아 열린다 (덜컹 0.4초 예고)"),
            ("③", "공성탑", "굴러가는 공성탑에 타 안 사다리 둘을 오르고(6.4 m), 붙으면 다리를 건넌다"),
            ("④", "룩의 훈련장", "큰 탑 지붕 9 × 9판, 룩 넷이 줄 · 칸으로 끝까지 미끄러진다 (1초 전 빛)"),
            ("⑤", "룩 · 나이트 기중기", "룩: 머리에 서서 곧게 위 · 앞 · 아래 (쉬움) | 나이트: 낮게 지날 때 갈기를 잡고 뛴다 (빠름)"),
            ("⑥", "왕의 깃발", "문루 서쪽 면의 깃발 두 폭 — 돌풍이 깃발을 젖히기 전에 턱이나 꼭대기로")]
    for k, (num, name, what) in enumerate(rows):
        y = TY + 22 + k * 22
        col = BEATS[k][3]
        w('<circle cx="%.1f" cy="%.1f" r="9" fill="%s" stroke="%s"/>' % (TX + 9, y - 4, col, INK))
        text(TX + 9, y, num, 11, "middle", "bold", "#ffffff")
        text(TX + 24, y, name, 12, weight="bold")
        text(TX + 150, y, what, 11)
    # the time budget
    BY = TY + 22 * 7 + 14
    text(TX, BY, "시간 예산 — 보통 플레이어 한 명 (대계단 셈법: 이동 + 장치 기다림 + 실패 확률 × 손실), 초", 13, weight="bold")
    cols = (TX, TX + 200, TX + 270, TX + 340, TX + 410, TX + 480)
    for c, h in zip(cols, ("구간", "길 m", "이동", "기다림", "실패", "합")):
        text(c, BY + 20, h, 11, weight="bold")
    for k, row in enumerate(BUDGET):
        y = BY + 38 + k * 16
        vals = (row[0], "%.0f" % row[1], "%.1f" % row[2], "%.1f" % row[3], "%.1f" % row[4], "%.1f" % (row[2] + row[3] + row[4]))
        for c, v in zip(cols, vals):
            text(c, y, v, 11)
    t = budget_total()
    y = BY + 38 + len(BUDGET) * 16 + 6
    text(TX, y, "합 %.0f초 (%s) · 12명 혼잡 보정 ×%.2f → %s · 수로 정원 약 %s(12명 중앙) · 대계단 %s" % (
        t, clock(t), CROWD, clock(t * CROWD), clock(GARDEN), clock(STAIR_HALL)), 12, weight="bold")
    # the legend
    LY = H - 26
    items = [(RED, "길"), (ORANGE, "사다리"), (PURPLE, "공성탑과 레일"), (BLUE, "룩의 줄"), (GREEN, "룩 기중기(머리 5 m)"), ("#a4c37a", "나이트 기중기"),
             (GOLD, "왕의 깃발"), ("#2a7a3a", "체크포인트")]
    x = 30.0
    for col, name in items:
        w('<rect x="%.1f" y="%.1f" width="14" height="10" fill="%s" stroke="%s" stroke-width="0.6"/>' % (x, LY - 9, col, INK))
        text(x + 18, LY, name, 11)
        x += 22 + 12 * len(name) + 8
    w('</svg>')
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(out))


if __name__ == "__main__":
    report()
    if len(sys.argv) > 1:
        draw(sys.argv[1])
        print("drew", sys.argv[1])
