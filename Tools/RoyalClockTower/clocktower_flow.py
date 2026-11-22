# Royal Clock Tower (course 2 draft, R775) - a flow model for the design-stage fun check.
# Not physics: pawns are points with the game's real speeds and stamina rules, the devices keep their real
# periods and phases, and how often a pawn fails a move is the designer's guess per skill (table FAIL below).
# Nobody has run the course: read the numbers for pacing, waiting and which road pays, not as truth.
# Usage: python clocktower_flow.py [runs] [seed] [report.txt]     (any Python 3; the report file is UTF-8)
# Design: Docs/PawnRush/PAWN_RUSH_ROYAL_CLOCK_TOWER_DESIGN_v0.1.md
import math
import random
import statistics
import sys
from collections import defaultdict

# ---- the game's numbers (RagdollTuning.asset, PawnRushPhysicsParams, ChessPieces)
RUN, SPRINT = 2.5, 3.6
UP, DOWN, SIDE = 1.2, 1.6, 0.9
POOL, REC, DELAY = 8.0, 2.0, 0.8
TOPOUT, CORNER = 0.45, 0.4
KNOCK_FALL = 4.13          # a longer fall floors the pawn
KNOCK, RESPAWN = 1.3, 1.5
DT = 0.05

# ---- the course (metres / seconds), the same table the drawings use
WALL_H = 3.5
PILLAR_X = [-15.5, -12.0, -6.0, -2.5]
PILLAR_PHASE = [0.0, 6.0, 3.0, 9.0]
PILLAR_PERIOD, PILLAR_DWELL, PILLAR_MOVE = 12.0, 3.0, 3.0
PILLAR_HEAD = 4.0          # only the top 4 m of a pillar can be held
CLIFF_H = 8.5
DROP_H = 7.0
PIER_LEG1, PIER_LEG2 = (0.3, 1.2), 2.1
ROPE_X = [6.0, 9.0, 12.0, 15.0, 18.0]
ROPE_PERIOD = 5.0
BARREL_R, BARREL_W = 5.0, 15.0          # radius, degrees per second
CFG = {"barrel": "drum", "pier": "pit"}      # variants: barrel drum / static, pier pit / trench
BARREL_BOARD = (335.0, 365.0)
COMB1_AZ, COMB1_UP = 70.0 + 360.0, 3.2     # be this much higher than the floor grip when it arrives
COMB2_AZ, COMB2_DOWN = 135.0 + 360.0, 1.8
BARREL_OFF = 175.0 + 360.0
BARREL_TOP = 4.6                         # floor grip to the drum's top, the high road
HIGH_LEFT = 4.9                          # what is left of the hammer wall from the high ledge
TRENCH = 2.0
MALLET_ROWS = [2.6, 5.1, 7.6]            # above the west floor; the top is at 10
HAMMER_H = 10.0
MALLET_PERIOD, MALLET_OUT = 9.0, 5.0
MALLET_ROW_LAG, MALLET_COL_LAG = 2.5, 1.5
MALLET_X = [-20.0, -16.0, -12.0, -8.0]
DIAL_EASY = [(3.8, 0.0), (3.0, 2.2), (2.8, 2.0)]     # (up, side) to perches A, B, C
DIAL_LAST = (3.4, 1.3)                               # round the corner and up the side face
DIAL_FAST = [(6.8, 0.0)]                             # straight to B, then B -> C -> corner -> top in one go
ROOF_RUN = 13.4

# chance to fail a move, by skill: new / mid / pro (the designer's guess, to be replaced by play data)
FAIL = {
    "pier": (0.12, 0.04, 0.01),          # lets go or stalls at the corner over the pit
    "rope": (0.28, 0.10, 0.03),          # lets go of the swing at the wrong moment
    "comb1": (0.22, 0.06, 0.01),         # did not climb over the low comb in time
    "comb2": (0.10, 0.03, 0.005),        # did not get under the hanging comb
    "corner": (0.08, 0.02, 0.005),       # the last corner under the cornice
    "shove": (0.10, 0.10, 0.10),         # pushed off a full perch, per extra pawn on it
}
SK = {"new": 0, "mid": 1, "pro": 2}
BEATS = ["1 배우는 벽", "2 짝 추", "3 낙하 벽", "4 모서리 기둥", "5 종 줄", "6 편종 원통", "7 망치 벽", "8 대시계판", "9 구리 지붕"]


class Pawn:
    def __init__(self, name, kind, skill, t0):
        self.name, self.kind, self.skill = name, kind, skill
        self.k = 0.7 if kind == "rook" else 1.0
        self.t0 = self.t = t0
        self.s, self.idle = POOL, 9.0
        self.st = defaultdict(float)
        self.beat = {}
        self.falls = defaultdict(int)
        self.kills = defaultdict(int)
        self.wait = defaultdict(float)
        self.note = defaultdict(int)

    def fail(self, what):
        return random.random() < FAIL[what][SK[self.skill]]


def tick(p, dt, state):
    if dt <= 0:
        return
    p.t += dt
    p.st[state] += dt
    if state in ("climb", "sprint"):
        p.s = max(0.0, p.s - dt)
        p.idle = 0.0
    else:
        before = p.idle
        p.idle += dt
        if state in ("run", "rest", "top", "wait"):          # on its feet and not sprinting: the bar comes back
            p.s = min(POOL, p.s + max(0.0, p.idle - max(before, DELAY)) * REC)


def adv(p, dt, state):
    n = max(1, int(math.ceil(dt / DT)))
    for _ in range(n):
        tick(p, dt / n, state)


def go(p, dist, keep):
    """Over the floor. keep = the stamina the next pitch needs."""
    while dist > 1e-6:
        left_walk = max(0.0, (dist / RUN) - DELAY) * REC
        if p.skill == "new":
            sprint = p.s > 0.3
        elif p.skill == "mid":
            sprint = dist > 6.0 and p.s > max(keep + 2.0, 4.0)
        else:
            sprint = p.s - DT + left_walk * 0.9 >= keep + 0.3 and p.s > 0.5
        v = SPRINT if sprint else RUN
        step = min(dist, v * DT)
        tick(p, step / v, "sprint" if sprint else "run")
        dist -= step


def rest_to(p, need, state="rest"):
    need = min(need, POOL)
    while p.s < need - 1e-9:
        tick(p, DT, state)


def pitch(up=0.0, side=0.0, down=0.0, k=1.0):
    """Seconds on the wall. Two keys at once move at 0.707 of each speed, so a slanted pitch beats up-then-across."""
    tv = up / (UP * k) + down / (DOWN * k)
    th = side / (SIDE * k)
    return max(tv, th) + 0.4142 * min(tv, th)


def ready(p, need):
    """Does it rest before the pitch? A new player goes at once; the others watch the bar."""
    if p.skill == "mid":
        rest_to(p, 1.15 * need)
    elif p.skill == "pro":
        rest_to(p, 1.03 * need)


def drop(p, height, beat):
    adv(p, math.sqrt(2 * max(height, 0.1) / 15.8) + 0.2, "fall")
    if height > KNOCK_FALL:
        adv(p, KNOCK, "fall")
    p.falls[beat] += 1


def climb_from_floor(p, need_t, height, beat, extra=0.0):
    """A pitch with a floor under it: run dry and it is a fall and another go, wiser."""
    tries = 0
    while True:
        if tries == 0:
            ready(p, need_t)
        else:
            rest_to(p, 1.1 * need_t)
        if p.s >= need_t - 1e-9:
            adv(p, need_t, "climb")
            adv(p, extra, "hang")
            return
        frac = p.s / need_t
        adv(p, p.s, "climb")
        drop(p, height * frac, beat)
        tries += 1


def kill(p, beat, walk_back):
    adv(p, 0.9, "fall")
    adv(p, RESPAWN, "fall")
    p.kills[beat] += 1
    p.s, p.idle = POOL, 9.0
    go(p, walk_back, 0.0)


class Slots:
    def __init__(self, n, headway=0.0):
        self.free = [0.0] * n
        self.headway = headway

    def take(self, t):
        i = min(range(len(self.free)), key=lambda j: self.free[j])
        return i, max(t, self.free[i])

    def hold(self, i, until):
        self.free[i] = until + self.headway


# ---------------------------------------------------------------- the nine beats
def b1_wall(p, w):
    need = pitch(up=WALL_H, k=p.k)
    go(p, 10.0, need)
    climb_from_floor(p, need, WALL_H, 0, TOPOUT)


def pillar_plan(p, i, t_arrive):
    """When it would step off pillar i onto the gallery, and what that takes."""
    ph = PILLAR_PHASE[i]
    n = math.floor((t_arrive - ph) / PILLAR_PERIOD)
    while True:
        bs = ph + n * PILLAR_PERIOD                      # the head comes down to the floor
        last = bs + PILLAR_DWELL + 0.4                   # still in reach just after it starts up
        if last >= t_arrive + 0.15:
            break
        n += 1
    tb = max(t_arrive, bs)
    extra = max(0.0, tb - (bs + PILLAR_DWELL)) * (4.5 / PILLAR_MOVE)
    need = pitch(up=PILLAR_HEAD - 0.5 + extra, k=p.k)
    on_top = tb + need + TOPOUT
    top0, top1 = bs + PILLAR_DWELL + PILLAR_MOVE, bs + PILLAR_DWELL + PILLAR_MOVE + PILLAR_DWELL
    off = max(on_top, top0) + 0.8 if on_top <= top1 - 0.6 else top0 + PILLAR_PERIOD + 0.8
    return tb, need, on_top, off


def b2_weights(p, w):
    x0, t0 = 0.0, p.t
    dist = [math.hypot(x - x0, 10.8) for x in PILLAR_X]
    best, plan = None, None
    order = range(4)
    if p.skill == "new":
        order = [random.choice([2, 3, 3, random.randrange(4)])]      # runs at the nearest one it sees
    for i in order:
        ta = t0 + dist[i] / RUN
        tb, need, on_top, off = pillar_plan(p, i, ta)
        if best is None or off < plan[3]:
            best, plan = i, (tb, need, on_top, off)
    cliff_t = pitch(up=CLIFF_H, k=p.k)
    if p.skill == "pro" and p.k >= 1.0 and plan[3] - t0 > 10.8 / RUN + cliff_t + TOPOUT + 1.0:
        go(p, 10.8, cliff_t)
        rest_to(p, min(POOL, cliff_t * 1.05))
        adv(p, cliff_t, "climb")
        adv(p, TOPOUT, "hang")
        p.note["cliff"] += 1
        return 0.5
    i = best
    go(p, dist[i], plan[1])
    tries = 0
    while True:
        tb, need, on_top, off = pillar_plan(p, i, p.t)
        if p.skill != "new" or tries > 0:
            rest_to(p, min(POOL, 1.08 * need), "wait")
            tb, need, on_top, off = pillar_plan(p, i, p.t)
        if tb > p.t:
            p.wait["pillar"] += tb - p.t
            adv(p, tb - p.t, "wait")
        if p.s >= need:
            adv(p, need, "climb")
            adv(p, TOPOUT, "hang")
            if off - p.t > 0:
                if off - p.t > PILLAR_PERIOD - 3:
                    p.note["rode down again"] += 1
                adv(p, off - p.t, "top")
            return PILLAR_X[i]
        adv(p, p.s, "climb")                                   # ran dry on the pillar
        drop(p, 3.0, 1)
        tries += 1


def b3_drop(p, w, x_from):
    go(p, abs(x_from - 2.0) + 6.0, 0.0)
    r = random.random()
    way = {"new": [(0.5, "ledge"), (0.9, "jump"), (1.0, "dive")], "mid": [(0.5, "ledge"), (0.6, "jump"), (1.0, "dive")],
           "pro": [(0.0, "ledge"), (0.0, "jump"), (1.0, "dive")]}[p.skill]
    how = next(h for c, h in way if r <= c)
    p.note["drop " + how] += 1
    if how == "ledge":
        adv(p, 2 * (math.sqrt(2 * 3.5 / 15.8) + 0.55), "down")
    elif how == "jump":
        adv(p, math.sqrt(2 * DROP_H / 15.8) + 0.2 + KNOCK, "down")
    else:
        p.s = max(0.0, p.s - 0.6)
        p.idle = 0.0
        adv(p, math.sqrt(2 * DROP_H / 15.8) + 0.75, "down")


def pier_fall(p):
    if CFG["pier"] == "pit":
        kill(p, 3, 8.0)
        return
    drop(p, TRENCH + 0.6, 3)                                   # into the shallow trench under the pier
    go(p, 3.0, 0.0)
    back = pitch(up=TRENCH, k=p.k)
    rest_to(p, min(POOL, 1.1 * (back + 3.9 / p.k)))            # wiser now: breath for the way back and the corner
    adv(p, back, "climb")
    adv(p, TOPOUT, "hang")
    go(p, 3.0, 0.0)


def b4_pier(p, w):
    leg1 = random.uniform(*PIER_LEG1)
    need = pitch(up=0.3, k=p.k) + pitch(side=leg1, k=p.k) + CORNER + pitch(side=PIER_LEG2, k=p.k)
    go(p, 10.0, need)
    tries = 0
    while True:
        if tries == 0:
            ready(p, need)
        else:
            rest_to(p, min(POOL, 1.1 * need))
        i, t = w["pier"].take(p.t)
        if t > p.t:
            p.wait["pier"] += t - p.t
            adv(p, t - p.t, "wait")
        w["pier"].hold(i, p.t + pitch(side=leg1, k=p.k) + CORNER + 0.6)
        tries += 1
        if p.s < need:                                         # ran dry on the wall
            adv(p, p.s, "climb")
            pier_fall(p)
            continue
        if tries == 1 and p.fail("pier"):
            adv(p, need * random.uniform(0.3, 0.8), "climb")
            pier_fall(p)
            continue
        adv(p, need, "climb")
        adv(p, 0.3, "hang")
        return


def b5_rope(p, w):
    go(p, 4.0, 0.0)
    x = random.uniform(5.5, 18.5)
    while True:
        best = None
        for i, rx in enumerate(ROPE_X):
            ta = p.t + abs(rx - x) / RUN
            n = math.ceil((ta - 0.45 - i * 1.0) / ROPE_PERIOD)
            while True:
                te = i * 1.0 + n * ROPE_PERIOD                 # the rope is at this end
                if te + 0.45 >= ta and w["rope"][(i, n)] < 2:
                    break
                n += 1
            grab = max(ta, te - 0.45)
            if best is None or grab < best[0]:
                best = (grab, i, n, rx)
        grab, i, n, rx = best
        w["rope"][(i, n)] += 1
        go(p, abs(rx - x), 0.0)
        if grab > p.t:
            p.wait["rope"] += grab - p.t
            adv(p, grab - p.t, "wait")
        x = rx
        adv(p, ROPE_PERIOD / 2, "hang")
        if p.fail("rope"):
            kill(p, 4, 2.0)
            continue
        adv(p, 0.5, "run")
        return


def b6_static(p, w):
    leg = pitch(side=17.3 / 4, k=p.k)
    go(p, 12.0, leg)
    tries = 0
    while True:
        if p.skill != "new" or tries:
            rest_to(p, min(POOL, 1.1 * leg))
        ok = True
        for n in range(4):
            if p.s < leg:
                adv(p, p.s, "climb")
                ok = False
                break
            adv(p, leg, "climb")
            if n < 3:
                want = {"new": 0.5 * leg, "mid": 1.15 * leg, "pro": 1.03 * leg}[p.skill] if tries == 0 else 1.15 * leg
                rest_to(p, min(POOL, want))
        if ok:
            return "low"
        kill(p, 5, 6.0)
        tries += 1


def b6_barrel(p, w):
    if CFG["barrel"] == "static":
        return b6_static(p, w)
    go(p, 12.0, pitch(up=COMB1_UP, k=p.k) + 1.0)
    high = p.skill == "pro" and p.k >= 1.0 and random.random() < 0.5
    shimmy = SIDE * p.k / (BARREL_R + 0.23) * 180 / math.pi      # degrees per second it adds by going along
    tries = 0
    while True:
        need_all = pitch(up=COMB1_UP, k=p.k) + pitch(down=COMB2_DOWN, k=p.k)
        if p.skill != "new" or tries:
            rest_to(p, min(POOL, (pitch(up=BARREL_TOP, k=p.k) if high else need_all) * 1.05))
        if high:
            adv(p, pitch(up=BARREL_TOP, k=p.k), "climb")
            adv(p, TOPOUT, "hang")
            adv(p, 3.0, "top")                                  # across the turning top to the plank
            p.note["barrel high"] += 1
            return "high"
        # low road: over comb 1, under comb 2, off at the west tongue
        az = random.uniform(*BARREL_BOARD)
        easy = 0.4 if tries else 1.0
        miss1 = random.random() < FAIL["comb1"][SK[p.skill]] * easy
        miss2 = random.random() < FAIL["comb2"][SK[p.skill]] * easy
        up_left, down_left, stage, ok = COMB1_UP, 0.0, 1, True
        keep = 1.5 if p.skill == "pro" else 4.5
        while az < BARREL_OFF:
            w_now = BARREL_W
            busy = up_left > 0 if stage == 1 else down_left > 0 if stage == 2 else False
            if stage == 1 and up_left > 0 and not miss1 and p.s > 0:
                up_left -= min(up_left, UP * p.k * DT)
                tick(p, DT, "climb")
            elif stage == 2 and down_left > 0 and not miss2 and p.s > 0:
                down_left -= min(down_left, DOWN * p.k * DT)
                tick(p, DT, "climb")
            elif not busy and p.skill != "new" and p.s > keep and (stage == 3 or p.skill == "pro"):
                w_now += shimmy
                tick(p, DT, "climb")
            else:
                tick(p, DT, "hang")
            az += w_now * DT
            if stage == 1 and az >= COMB1_AZ:
                if up_left > 0.05:
                    ok = False
                    break
                stage, down_left = 2, COMB2_DOWN
            elif stage == 2 and az >= COMB2_AZ:
                if down_left > 0.05:
                    ok = False
                    break
                stage = 3
        if ok:
            adv(p, 0.4, "hang")
            return "low"
        kill(p, 5, 6.0)
        tries += 1


def mallet_out(r, c, t):
    return ((t - (MALLET_ROW_LAG * r + MALLET_COL_LAG * c)) % MALLET_PERIOD) < MALLET_OUT


def mallet_left(r, c, t):
    u = (t - (MALLET_ROW_LAG * r + MALLET_COL_LAG * c)) % MALLET_PERIOD
    return MALLET_OUT - u if u < MALLET_OUT else -(MALLET_PERIOD - u)      # >0: seconds it stays out, <0: seconds until out


def b7_hammer(p, w, came):
    if came == "high":
        need = pitch(up=HIGH_LEFT, k=p.k)
        go(p, 8.0, need)
        climb_from_floor(p, need, HIGH_LEFT, 6, TOPOUT)
        return
    c = random.randrange(4) if p.skill == "new" else min(range(4), key=lambda j: abs(MALLET_X[j] + 9))
    hts = [MALLET_ROWS[0], MALLET_ROWS[1] - MALLET_ROWS[0], MALLET_ROWS[2] - MALLET_ROWS[1], HAMMER_H - MALLET_ROWS[2]]
    go(p, 9.0 + abs(MALLET_X[c] + 6) * 0.6, pitch(up=hts[0], k=p.k))
    tries = 0
    while True:
        if tries:
            rest_to(p, POOL * 0.9)
        elif p.skill != "new":
            rest_to(p, min(POOL, 1.1 * pitch(up=hts[0] + hts[1], k=p.k)))
        fell = False
        for r in range(4):
            need = pitch(up=hts[r], k=p.k)
            if p.s < need:
                adv(p, p.s, "climb")
                drop(p, sum(hts[:r]) + hts[r] * 0.5, 6)
                fell = True
                break
            adv(p, need, "climb")
            if r == 3:
                adv(p, TOPOUT, "hang")
                return
            nxt = pitch(up=hts[r + 1], k=p.k)
            want = {"new": 0.0, "mid": 1.15 * nxt, "pro": 1.03 * nxt}[p.skill]
            if tries:
                want = 1.15 * nxt
            if p.s >= want:
                continue                                            # carries on without a rest
            left = mallet_left(r, c, p.t)
            if left < 0:                                            # the mallet is in: hang until it comes out
                p.wait["mallet"] += -left
                adv(p, -left, "hang")
                left = MALLET_OUT
            stay = max(0.0, left - 0.25)
            t_end = p.t + stay
            while p.s < want and p.t < t_end:
                tick(p, DT, "rest")
            if p.s < nxt:                                           # pushed off before it had its breath back: hangs on the wall
                back = (MALLET_PERIOD - MALLET_OUT) + 0.3
                p.wait["mallet"] += back
                adv(p, back, "hang")
                t_end = p.t + MALLET_OUT - 0.5
                while p.s < want and p.t < t_end:
                    tick(p, DT, "rest")
        if not fell:
            return
        tries += 1


def b8_dial(p, w):
    go(p, 18.0, pitch(up=3.8, k=p.k))
    side = 0 if w["dial"][0] <= w["dial"][1] else 1
    w["dial"][side] += 1
    last = pitch(side=0.8, k=p.k) + CORNER + pitch(up=DIAL_LAST[0], side=0.5, k=p.k)
    fast = p.k >= 1.0 and (p.skill == "pro" or (p.skill == "mid" and random.random() < 0.3))
    tries = 0
    while True:
        if fast and tries < 2:
            legs = [pitch(up=6.8, k=p.k), pitch(up=2.8, side=2.0, k=p.k), last]
            perch = ["B", "C", None]
            heights = [6.8, 9.6, 13.0]
            p.note["dial fast"] += 1 if tries == 0 else 0
        else:
            legs = [pitch(up=u, side=s, k=p.k) for u, s in DIAL_EASY] + [last]
            perch = ["A", "B", "C", None]
            heights = [3.8, 6.8, 9.6, 13.0]
        if tries:
            rest_to(p, POOL * 0.95)
        elif p.skill != "new":
            rest_to(p, min(POOL, 1.1 * legs[0]))
        fell = False
        for n, need in enumerate(legs):
            if p.s < need:
                adv(p, p.s, "climb")
                drop(p, heights[n] - (heights[n] - (heights[n - 1] if n else 0)) * 0.5, 7)
                fell = True
                break
            is_last = n == len(legs) - 1
            if is_last and (p.fail("corner") or (p.skill == "new" and random.random() < 0.4)):
                bal = pitch(up=5.0, k=p.k)
                if p.fail("corner"):
                    adv(p, need * 0.5, "climb")
                    drop(p, 3.0, 7)
                    p.note["fell at the last corner"] += 1
                else:
                    adv(p, 1.0, "down")                           # hops down to the side balcony instead
                    p.note["took the balcony"] += 1
                rest_to(p, min(POOL, 1.1 * bal))
                adv(p, bal, "climb")
                adv(p, TOPOUT, "hang")
                return
            adv(p, need, "climb")
            if is_last:
                adv(p, TOPOUT, "hang")
                return
            key = (side, perch[n])
            i, t = w["perch"][key].take(p.t)
            if t > p.t:                                           # the perch is full: hangs under it
                p.wait["perch"] += t - p.t
                adv(p, t - p.t, "hang")
            nxt = legs[n + 1]
            want = {"new": 0.5 * nxt, "mid": 1.15 * nxt, "pro": 1.03 * nxt}[p.skill] if tries == 0 else 1.15 * nxt
            t_in = p.t
            rest_to(p, want)
            if p.t - t_in < 0.4:
                adv(p, 0.4, "rest")
            w["perch"][key].hold(i, p.t)
        if not fell:
            return
        tries += 1


def b9_roof(p, w):
    go(p, 10.0, 0.0)
    if p.skill != "new" or random.random() < 0.3:
        p.s = max(0.0, p.s - 0.6)
        adv(p, 2.3, "down")
        p.note["slide"] += 1
    else:
        go(p, ROOF_RUN, 0.0)
    go(p, 8.0, 0.0)


def run_round(team, spread):
    """team: list of (kind, skill). Everyone comes in over `spread` seconds."""
    ps = [Pawn(f"{k[0]}{s[0]}{i}", k, s, random.uniform(0, spread)) for i, (k, s) in enumerate(team)]
    w = {"pier": Slots(4, 0.0), "rope": defaultdict(int), "dial": [0, 0],
         "perch": defaultdict(lambda: Slots(2, 0.0))}
    carry = {}

    def stage(idx, fn):
        for p in sorted(ps, key=lambda q: q.t):
            t_in = p.t
            carry[p.name] = fn(p)
            p.beat[idx] = (t_in, p.t)

    stage(0, lambda p: b1_wall(p, w))
    stage(1, lambda p: b2_weights(p, w))
    for p in sorted(ps, key=lambda q: q.t):
        t_in = p.t
        b3_drop(p, w, carry[p.name])
        p.beat[2] = (t_in, p.t)
    stage(3, lambda p: b4_pier(p, w))
    stage(4, lambda p: b5_rope(p, w))
    stage(5, lambda p: b6_barrel(p, w))
    came = dict(carry)
    for p in sorted(ps, key=lambda q: q.t):
        t_in = p.t
        b7_hammer(p, w, came[p.name])
        p.beat[6] = (t_in, p.t)
    stage(7, lambda p: b8_dial(p, w))
    stage(8, lambda p: b9_roof(p, w))
    return ps


def near(ps, i):
    """How many others are in the same beat at the same time, time-weighted."""
    out = []
    for p in ps:
        a, b = p.beat[i]
        if b - a < 1e-6:
            continue
        tot = 0.0
        for q in ps:
            if q is p:
                continue
            c, d = q.beat[i]
            tot += max(0.0, min(b, d) - max(a, c))
        out.append(tot / (b - a))
    return out


def med(x):
    return statistics.median(x) if x else 0.0


def report(label, team, runs, spread):
    all_ps, rounds = [], []
    for _ in range(runs):
        ps = run_round(team, spread)
        rounds.append(ps)
        all_ps += ps
    print(f"\n=== {label}: {len(team)}명, {runs}판, 입장 {spread:.0f}초에 걸쳐")
    tot = [[p.t - p.t0 for p in ps] for ps in rounds]
    print("구간에 머문 시간  1등 %.1f  중간 %.1f  꼴찌 %.1f 초" % (med([min(t) for t in tot]), med([med(t) for t in tot]), med([max(t) for t in tot])))
    fin = [[p.t for p in ps] for ps in rounds]
    print("출구 도착 차이(1등~꼴찌) 중간값 %.1f초  (입장 차이 %.1f초)" % (med([max(f) - min(f) for f in fin]),
                                                                  med([max(p.t0 for p in ps) - min(p.t0 for p in ps) for ps in rounds])))
    groups = [("전체", lambda p: True), ("폰 보통", lambda p: p.kind != "rook" and p.skill == "mid"),
              ("처음", lambda p: p.skill == "new"), ("숙련", lambda p: p.skill == "pro"), ("룩", lambda p: p.kind == "rook")]
    print("비트                " + "".join(f"{g[0]:>9s}" for g in groups) + "   같이 있는 사람  낙하  낙사")
    for i, name in enumerate(BEATS):
        row = f"{name:14s}"
        for g, f in groups:
            row += "%9.1f" % med([p.beat[i][1] - p.beat[i][0] for p in all_ps if f(p)])
        nb = [v for ps in rounds for v in near(ps, i)]
        row += "   %6.2f명" % (sum(nb) / max(1, len(nb)))
        row += "   %5.2f %5.2f" % (sum(p.falls[i] for p in all_ps) / len(all_ps), sum(p.kills[i] for p in all_ps) / len(all_ps))
        print(row)
    for g, f in groups:
        sel = [p for p in all_ps if f(p)]
        if not sel:
            continue
        T = sum(p.t - p.t0 for p in sel)
        sh = defaultdict(float)
        for p in sel:
            for k, v in p.st.items():
                sh[k] += v
        txt = "  ".join(f"{n} {sh[k] / T * 100:4.1f}%" for k, n in [("climb", "벽타기"), ("hang", "매달림·실림"), ("top", "추 위"), ("rest", "쉬기"),
                                                                      ("wait", "기다림"), ("run", "달리기"), ("sprint", "질주"), ("down", "내려감"), ("fall", "낙하·부활")])
        print(f"시간 나눔 {g:6s} {med([p.t - p.t0 for p in sel]):6.1f}초 | {txt}")
    waits = defaultdict(list)
    for p in all_ps:
        for k in ("pillar", "pier", "rope", "mallet", "perch"):
            waits[k].append(p.wait[k])
    print("기다림(초, 평균/최대): " + "  ".join(f"{n} {sum(v) / len(v):.1f}/{max(v):.1f}" for k, n in [("pillar", "추"), ("pier", "모서리"), ("rope", "줄"), ("mallet", "망치"), ("perch", "쉼터")]
                                          for v in [waits[k]]))
    notes = defaultdict(int)
    for p in all_ps:
        for k, v in p.note.items():
            notes[k] += v
    print("고른 길(사람당): " + "  ".join(f"{k} {v / len(all_ps):.2f}" for k, v in sorted(notes.items())))
    for i in range(1, 9):
        sp = med([max(p.beat[i][0] for p in ps) - min(p.beat[i][0] for p in ps) for ps in rounds])
        print(f"  {BEATS[i]} 입장 때 1등~꼴찌 {sp:5.1f}초", end="")
    print()


def tables():
    print("\n=== 피치마다 드는 스테미나 (가득 = 8초)")
    rows = [("배우는 벽 3.5", pitch(up=3.5)), ("추 머리 3.5", pitch(up=3.5)), ("절벽 직등 8.5", pitch(up=8.5)),
            ("모서리 기둥(0.3↑ 0.8→ 모서리 2.1→)", pitch(up=0.3) + pitch(side=0.8) + CORNER + pitch(side=2.1)),
            ("원통 낮은 길(3.2↑ 1.8↓)", pitch(up=3.2) + pitch(down=1.8)), ("원통 꼭대기 4.6", pitch(up=4.6)), ("높은 턱에서 4.9", pitch(up=4.9)),
            ("망치 한 칸 2.6", pitch(up=2.6)), ("망치 벽 통째 10", pitch(up=10.0)),
            ("시계판 A 3.8", pitch(up=3.8)), ("A→B (3.0↑ 2.2→)", pitch(up=3.0, side=2.2)), ("B→C (2.8↑ 2.0→)", pitch(up=2.8, side=2.0)),
            ("C→모서리→꼭대기", pitch(side=0.8) + CORNER + pitch(up=3.4, side=0.5)), ("빠른 길 6.8", pitch(up=6.8)),
            ("빠른 길 B→꼭대기", pitch(up=2.8, side=2.0) + pitch(side=0.8) + CORNER + pitch(up=3.4, side=0.5))]
    for name, t in rows:
        print(f"{name:34s} 폰 {t:5.2f}초 {t / POOL * 100:4.0f}%   룩 {t / 0.7:5.2f}초 {t / 0.7 / POOL * 100:4.0f}%")
    print("\n=== 반원 구간 견주기 (반지름 5 m, 190도 = 17.3 m)")
    arc = math.radians(190) * (BARREL_R + 0.23)
    print(f"서 있는 반원 벽을 옆으로만: {arc / SIDE:5.1f}초 내내 소모 {arc / SIDE:4.1f} -> 쉼터 {math.ceil(arc / SIDE / 6.5) - 1}곳 필요, 쉬는 시간까지 약 {arc / SIDE + (math.ceil(arc / SIDE / 6.5) - 1) * 3.6:4.1f}초")
    for wdeg in (6.0, 9.0, 12.0, 15.0):
        hang = 190 / wdeg
        sh = SIDE / (BARREL_R + 0.23) * 180 / math.pi
        both = 190 / (wdeg + sh)
        print(f"원통이 초당 {wdeg:4.1f}도: 매달려만 {hang:5.1f}초(소모 0) / 내내 같이 타면 {both:4.1f}초(소모 {both:4.1f}) / 겉 빠르기 {math.radians(wdeg) * BARREL_R:4.2f} m/s")
    small = math.pi * (2.5 + 0.23)
    print(f"작은 반원(반지름 2.5 m, 180도 = {small:4.1f} m): 옆으로만 {small / SIDE:4.1f}초 소모 {small / SIDE:4.1f} (룩 {small / SIDE / 0.7:4.1f})")


if __name__ == "__main__":
    runs = int(sys.argv[1]) if len(sys.argv) > 1 else 300
    random.seed(int(sys.argv[2]) if len(sys.argv) > 2 else 775)
    if len(sys.argv) > 3:
        sys.stdout = open(sys.argv[3], "w", encoding="utf-8", newline=chr(10))
    tables()
    six = [("pawn", "new"), ("pawn", "new"), ("pawn", "mid"), ("rook", "mid"), ("pawn", "mid"), ("pawn", "pro")]
    twelve = [("pawn", "new")] * 3 + [("pawn", "mid")] * 4 + [("rook", "mid")] * 2 + [("pawn", "pro")] * 3
    report("3 대 3", six, runs, 15.0)
    report("6 대 6", twelve, runs, 15.0)
    report("6 대 6, 모두 같이 입장", twelve, runs, 2.0)
    CFG["pier"] = "trench"
    report("견주기: 모서리 기둥 밑이 낙사 대신 2 m 도랑일 때 (6 대 6)", twelve, runs, 15.0)
    CFG["pier"] = "pit"
    CFG["barrel"] = "static"
    report("견주기: 원통 대신 서 있는 반원 벽일 때 (6 대 6)", twelve, runs, 15.0)
    CFG["barrel"] = "drum"
    for wdeg in (12.0, 18.0):
        globals()["BARREL_W"] = wdeg
        report(f"견주기: 원통이 초당 {wdeg:.0f}도일 때 (6 대 6)", twelve, runs, 15.0)
