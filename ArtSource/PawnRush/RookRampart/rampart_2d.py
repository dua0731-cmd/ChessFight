# The rooks' rampart walk (룩 성벽길, level design v0.1): every beat explained in 2D, moving on the shared clock.
# Writes one self-contained HTML page: the plan (rampart_layout.py's drawing, inline), then for each beat a side, top
# or front view that moves the way the design's time functions move it, the device's cycle and the shown pawns'
# moves as a timeline under it, and how it is passed. The devices are rampart_layout.py's functions sampled every DT.
# The pawns' moves are planned here from the design's numbers - over the murder holes and the drill square by a search
# over time, so the pawn shown really gets through - and are drawn, not recorded from the game.
#
#   python rampart_2d.py <page.html>        (the page's template is rampart_2d_page.html, next to this file)
import heapq, io, json, math, os, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
L = {"__name__": "rampart_layout"}
exec(compile(io.open(os.path.join(HERE, "rampart_layout.py"), encoding="utf-8").read(), "rampart_layout.py", "exec"), L)
DT = 0.05
CLIMB, RUN, G = L["CLIMB_UP"], L["RUN"], 9.81
ROOKC = CLIMB * L["ROOK"]
W1, W2, W3 = L["W1_Y"], L["W2_Y"], L["W3_Y"]
ease, lerp = L["ease"], L["lerp"]


def steps(period):
    return int(round(period / DT))


class Track:
    # A pawn's moves as segments (t0, t1, f(t) -> (h, v) or (h, v, w), vis): vis 1 shown, 2 failing, 3 down, 4 sent
    # back to its checkpoint. (h, v) is where its feet are; w, when there, is the third axis (depth). Events are the
    # timeline's row for it.
    def __init__(self):
        self.segs, self.events = [], []

    def add(self, t0, t1, f, vis=1):
        self.segs.append((t0, t1, f, vis))
        return t1

    def stay(self, t0, t1, p, vis=1):
        return self.add(t0, t1, lambda t, p=p: p, vis)

    def move(self, t0, t1, a, b, vis=1, lift=0.0):
        def f(t, a=a, b=b, t0=t0, t1=t1):
            k = (t - t0) / (t1 - t0)
            p = [lerp(a[i], b[i], k) for i in range(len(a))]
            p[1] += lift * math.sin(math.pi * k)
            return tuple(p)
        return self.add(t0, t1, f, vis)

    def note(self, t0, t1, text, kind):
        self.events.append([round(t0, 2), round(t1, 2), text, kind])

    def at(self, t):
        for t0, t1, f, vis in self.segs:
            if t0 <= t < t1:
                p = f(t)
                return [round(p[0], 3), round(p[1], 3), vis] + [round(v, 3) for v in p[2:]]
        return [0, 0, 0]

    def sample(self, period):
        return [self.at(i * DT) for i in range(steps(period))]


def fall(track, t0, p0, v0, floor, vis=2):
    # A drop from p0 = (h, v) or (h, v, w) with speed v0 along h (along w when there is one) to the floor.
    d = max(0.0, p0[1] - floor)
    tf = math.sqrt(2.0 * d / G)
    if len(p0) == 2:
        f = lambda t: (p0[0] + v0 * (t - t0), p0[1] - 0.5 * G * (t - t0) ** 2)
    else:
        f = lambda t: (p0[0], p0[1] - 0.5 * G * (t - t0) ** 2, p0[2] + v0 * (t - t0))
    track.add(t0, t0 + tf, f, vis)
    end = f(t0 + tf)
    return t0 + tf, tuple([end[0], floor] + list(end[2:]))


def plan(cells, move_ok, start, goals, t0, horizon):
    # Earliest arrival over (cell, time step): wait a step, or move to a neighbour, each checked by move_ok.
    k0, kmax = steps(t0), steps(horizon)
    seen, prev = set(), {(start, k0): None}
    pq = [(k0, start)]
    while pq:
        k, c = heapq.heappop(pq)
        if (c, k) in seen:
            continue
        seen.add((c, k))
        if c in goals:
            path, node = [], (c, k)
            while node is not None:
                path.append(node)
                node = prev[node]
            return path[::-1]
        if k >= kmax:
            continue
        for c2, dur in [(c, DT)] + cells[c]:
            dk = max(1, steps(dur))
            if (c2, k + dk) in seen or not move_ok(c, c2, k, dk):
                continue
            if (c2, k + dk) not in prev:
                prev[(c2, k + dk)] = (c, k)
            heapq.heappush(pq, (k + dk, c2))
    return None


def phases(rows):
    return [[round(a, 2), round(b, 2), name, kind] for a, b, name, kind in rows]


# ---------------------------------------------------------------- ① the siege ladders (a section through ladder 0)
LL, LEAN, CL, P1 = L["LADDER_L"], L["LEAN"], L["LADDER_CLIMB"], L["PUSH_CYCLE"]
WIND, TIP, LIE, RAISE = L["WIND"], L["TIP"], L["LIE"], L["RAISE"]
FACE1 = 11.0                                     # the outer wall's face along its straight middle
ZF = FACE1 - LL * math.cos(math.radians(LEAN)) + 0.1     # the foot: the top rests 0.1 m into the crenel
BODY, MANTLE = 0.3, 0.5                          # a climber's feet off the ladder's line; over the sill
CLIMB_T = CL / CLIMB
U_REL = WIND + TIP * math.sqrt((90.0 - LEAN) / (L["FALLEN"] - LEAN))    # it passes upright: its climbers let go
U_UP = next(k * 0.01 for k in range(int((WIND + TIP + LIE) / 0.01), int(P1 / 0.01)) if L["ladder_climbable"](0, k * 0.01))
LATE_PAWN = P1 + U_REL - CLIMB_T - MANTLE        # the last start that is over the sill before the next push
LATE_ROOK = P1 + U_REL - CL / ROOKC - MANTLE


def lad_line(t, s):
    a = math.radians(L["ladder_angle"](0, t))
    return ZF + s * math.cos(a), s * math.sin(a)


def on_ladder(t, s):
    z, y = lad_line(t, s)
    return z - BODY, y


def rest_pt(h):
    return ZF + h / math.tan(math.radians(LEAN)) - BODY, h


good1, fail1 = Track(), Track()
WAIT1 = (ZF + 0.75, 0.0)                         # at the foot on the wall's side: it falls the other way
good1.stay(0.0, 3.25, WAIT1)
good1.move(3.25, 3.65, WAIT1, rest_pt(0.0))
t = good1.add(3.65, 3.65 + CLIMB_T, lambda t: rest_pt((t - 3.65) * CLIMB))
good1.move(t, t + MANTLE, rest_pt(CL), (12.6, W1), lift=0.45)
good1.move(t + MANTLE, t + MANTLE + 1.4, (12.6, W1), (16.4, W1))
good1.note(0.0, 3.25, "발치 안쪽(성벽 쪽)에서 기다림", "still")
good1.note(3.65, t, "다시 서자 오르기 %.1f초" % CLIMB_T, "go")
good1.note(t, t + MANTLE, "넘음", "go")
fail1.move(3.7, 6.6, (ZF - 7.4, 0.0), rest_pt(0.0))
T_ON = P1 + WIND
fail1.add(6.6, T_ON, lambda t: rest_pt((t - 6.6) * CLIMB))
S_ON = (T_ON - 6.6) * CLIMB / math.sin(math.radians(LEAN))
T_REL1 = P1 + U_REL
fail1.add(T_ON, T_REL1, lambda t: on_ladder(t, S_ON), 2)
t_land, p_land = fall(fail1, T_REL1, on_ladder(T_REL1, S_ON), -1.8, 0.0)
fail1.stay(t_land, 12.0, p_land, 3)
fail1.move(12.0, 12.6, p_land, rest_pt(0.0))
t = fail1.add(12.6, 12.6 + CLIMB_T, lambda t: rest_pt((t - 12.6) * CLIMB))
fail1.move(t, t + MANTLE, rest_pt(CL), (12.6, W1), lift=0.45)
fail1.move(t + MANTLE, t + MANTLE + 1.2, (12.6, W1), (15.6, W1))
fail1.note(6.6, T_ON, "늦게(6.6초) 오르기 시작", "warn")
fail1.note(T_ON, T_REL1, "밀림", "danger")
fail1.note(T_REL1, 12.0, "수직에서 놓침 · 넘어짐", "danger")
fail1.note(12.6, t, "다시 서자 오르기", "go")
DEF_REST, DEF_IN, DEF_PUSH, HANDS = 13.2, 12.9, 12.45, 1.3


def def_z(t):
    u = t % P1
    if u < WIND:
        return lerp(DEF_REST, DEF_IN, ease(u / WIND))
    if u < U_REL:
        return lerp(DEF_IN, DEF_PUSH, (u - WIND) / (U_REL - WIND))
    if u < U_REL + 0.8:
        return lerp(DEF_PUSH, DEF_REST, ease((u - U_REL) / 0.8))
    return DEF_REST


def pole_tip(t):
    # The forked pole's tip: held up, lowered onto the ladder's top (the warning), pushing it past upright, raised.
    u = t % P1
    hz, hy = def_z(t) + 0.3, W1 + HANDS
    up = (hz - 0.75, hy + 2.9)

    def top_at(tt):
        z, y = lad_line(tt, LL)
        return z + 0.05, y + 0.05

    def swing(a, b, k):
        a0, a1 = math.atan2(a[1] - hy, a[0] - hz), math.atan2(b[1] - hy, b[0] - hz)
        r0, r1 = math.hypot(a[0] - hz, a[1] - hy), math.hypot(b[0] - hz, b[1] - hy)
        ang, r = lerp(a0, a1, k), lerp(r0, r1, k)
        return hz + r * math.cos(ang), hy + r * math.sin(ang)

    if u < WIND:
        return swing(up, top_at(t), ease(u / WIND))
    if u < U_REL:
        return top_at(t)
    if u < U_REL + 0.8:
        return swing(top_at(t - u + U_REL), up, ease((u - U_REL) / 0.8))
    return up


N1 = steps(2 * P1)
B1 = {
    "loop": 2 * P1, "cycle": P1, "zf": ZF, "len": LL, "face": FACE1, "w1": W1, "sill": W1 + L["SILL_H"], "merlon": W1 + L["MERLON_H"],
    "parapet": L["PARAPET"], "walk1": FACE1 + L["WALL_T"], "hands": HANDS,
    "theta": [round(L["ladder_angle"](0, i * DT), 2) for i in range(N1)],
    "defA": [round(1.0 - min(1.0, abs(L["defender_x"](0, i * DT) - L["LADDERS"][0]) / 1.6), 3) for i in range(N1)],
    "defz": [round(def_z(i * DT), 3) for i in range(N1)],
    "pole": [[round(v, 3) for v in pole_tip(i * DT)] for i in range(N1)],
    "good": good1.sample(2 * P1), "fail": fail1.sample(2 * P1), "goodEv": good1.events, "failEv": fail1.events,
    "ladders": L["LADDERS"], "pushAt": L["PUSH_AT"],
    "strip": [[round(L["ladder_angle"](i, k * DT), 1) for k in range(steps(P1))] for i in range(6)],
    "defx": [[round(L["defender_x"](d, k * DT), 2) for k in range(steps(P1))] for d in range(2)],
    "win": [round(U_UP, 2), round(LATE_PAWN, 2), round(LATE_ROOK, 2)], "rel": round(U_REL, 2), "wind": WIND,
    "phases": phases([(0.0, WIND, "장대 내림 (예고)", "warn"), (WIND, WIND + TIP, "밀려 넘어감", "danger"),
                      (WIND + TIP, WIND + TIP + LIE, "누움", "still"), (WIND + TIP + LIE, WIND + TIP + LIE + RAISE, "다시 세움", "move"),
                      (WIND + TIP + LIE + RAISE, P1, "기대어 있음 — 오를 때", "go")]),
}

# ---------------------------------------------------------------- ② the murder holes (a top view of the gate's roof)
HC, GX0, HOLE, NX, NZ = L["HOLE_CYCLE"], L["GATE_X"][0], L["HOLE"], L["HOLES_X"], L["HOLES_Z"]
Z12 = FACE1 + L["PARAPET"]                       # the walk's south edge; the trapdoors fill the walk over the gate


def hole_mid(i, j):
    return GX0 + (i + 0.5) * HOLE, Z12 + (j + 0.5) * HOLE


cells2 = {}
for i in range(-1, NX + 1):
    for j in range(NZ):
        cells2[(i, j)] = [((i + di, j + dj), HOLE * math.hypot(di, dj) / RUN)
                          for di in (-1, 0, 1) for dj in (-1, 0, 1) if (di or dj) and -1 <= i + di <= NX and 0 <= j + dj < NZ]


def hole_safe(c, t):
    i, j = c
    return i < 0 or i >= NX or L["trapdoor"](i, j, t) != 2


def hole_move(a, b, k, dk):
    return all(hole_safe(a if n < dk / 2.0 else b, (k + n) * DT) for n in range(dk + 1))


path2 = plan(cells2, hole_move, (-1, 1), {(NX, j) for j in range(NZ)}, 0.6, 2 * HC)
assert path2, "no way over the murder holes"
good2 = Track()
good2.move(0.0, 0.6, (GX0 - 2.5, 15.0), hole_mid(-1, 1))
for (c, k), (c2, k2) in zip(path2, path2[1:]):
    good2.move(k * DT, k2 * DT, hole_mid(*c), hole_mid(*c2))
T_END2 = path2[-1][1] * DT
good2.move(T_END2, T_END2 + 0.8, hole_mid(*path2[-1][0]), (GX0 + NX * HOLE + 2.6, 15.0))
good2.note(0.6, T_END2, "닫힌 칸만 밟아 건넘 (%.1f초)" % (T_END2 - 0.6), "go")
# A pawn that runs straight along the first row, not watching the lids: one opens under its feet.
s_fall = None
for n in range(steps(HC)):
    s0 = HC + n * DT
    for m in range(1, steps(6.0)):
        tt = s0 + m * DT
        x = GX0 - 2.0 + RUN * (tt - s0)
        if x > GX0 + NX * HOLE + 0.5:
            break
        i = int(math.floor((x - GX0) / HOLE))
        if 0 <= i < NX and L["trapdoor"](i, 0, tt) == 2:
            ip = int(math.floor((x - RUN * DT - GX0) / HOLE))
            if i >= 1 and ip == i and L["trapdoor"](i, 0, tt - DT) == 1:
                s_fall = (s0, tt, x)
            break
    if s_fall:
        break
assert s_fall, "no fall found on the murder holes"
S2, TF2, XF2 = s_fall
fail2 = Track()
fail2.move(S2 - 0.6, TF2, (GX0 - 3.5, hole_mid(0, 0)[1]), (XF2, hole_mid(0, 0)[1]))
fail2.stay(TF2, TF2 + 0.7, (XF2, hole_mid(0, 0)[1]), 2)
fail2.note(S2 - 0.6, TF2 - 0.4, "뚜껑을 안 보고 곧장 달림", "warn")
fail2.note(TF2 - 0.4, TF2, "덜컹이는 칸 위", "warn")
fail2.note(TF2, TF2 + 0.7, "열려 떨어짐 (3.5 m)", "danger")
B2 = {
    "loop": 2 * HC, "cycle": HC, "x0": GX0, "z0": Z12, "hole": HOLE, "nx": NX, "nz": NZ, "face": FACE1, "walkN": FACE1 + L["WALL_T"],
    "state": [[[L["trapdoor"](i, j, k * DT) for k in range(steps(HC))] for j in range(NZ)] for i in range(NX)],
    "good": good2.sample(2 * HC), "fail": fail2.sample(2 * HC), "goodEv": good2.events, "failEv": fail2.events,
    "route": [[round(v, 2) for v in hole_mid(*c)] for c, k in path2],
    "light": phases([(0.0, 0.4, "덜컹", "warn"), (0.4, 1.6, "열림", "danger"), (1.6, HC, "닫힘", "go")]),
    "dark": phases([(0.0, 2.2, "닫힘", "go"), (2.2, 2.6, "덜컹", "warn"), (2.6, 3.8, "열림", "danger"), (3.8, HC, "닫힘", "go")]),
}

# ---------------------------------------------------------------- ③ the siege tower (a section through tower A)
P3, FL, Z0, Z1 = L["SIEGE_CYCLE"], L["SIEGE_FLOORS"], L["SIEGE_Z0"], L["SIEGE_Z1"][0]
FACE2 = L["wall2_face_z"](L["SIEGE_X"][0])


def zc(t):
    return L["siege"](0, t)[0]


def inside(dz, y):
    return lambda t, dz=dz, y=y: (zc(t) + dz, y)


def climbing(t0, dur, a, b):
    return lambda t, t0=t0, dur=dur, a=a, b=b: (zc(t) + lerp(a[0], b[0], (t - t0) / dur), lerp(a[1], b[1], (t - t0) / dur))


good3, late3 = Track(), Track()
good3.stay(0.0, 1.2, (16.4, W1))
good3.move(1.2, 2.2, (16.4, W1), (Z0 - 1.2, FL[0]))
good3.add(2.2, 4.3, inside(-1.2, FL[0]))
c1 = (FL[1] - FL[0]) / CLIMB
t = good3.add(4.3, 4.3 + c1, climbing(4.3, c1, (-1.2, FL[0]), (-0.6, FL[1])))
t2 = t
t = good3.add(t2, t2 + 0.6, climbing(t2, 0.6, (-0.6, FL[1]), (0.6, FL[1])))
t3 = t
t = good3.add(t3, t3 + c1, climbing(t3, c1, (0.6, FL[1]), (1.2, FL[2])))
T_TOP3 = t
good3.add(t, 12.7, inside(1.2, FL[2]))
good3.move(12.7, 13.9, (Z1 + 1.2, FL[2]), (Z1 + 4.3, FL[2]))
good3.move(13.9, 14.3, (Z1 + 4.3, FL[2]), (Z1 + 4.9, W2), lift=0.2)
good3.move(14.3, 15.6, (Z1 + 4.9, W2), (Z1 + 8.2, W2))
good3.note(1.2, 2.2, "뒤로 탐", "go")
good3.note(4.3, T_TOP3, "구르는 동안 안 사다리 둘", "go")
good3.note(T_TOP3, 12.7, "꼭대기에서 기다림", "still")
good3.note(12.7, 14.3, "다리 건넘", "go")
late3.move(4.4, 6.0, (13.4, W1), (17.6, W1))
VZ3, VY3 = 3.0, 3.0
TAU3 = (VY3 + math.sqrt(VY3 * VY3 + 2.0 * G * W1)) / G
late3.add(6.0, 6.0 + TAU3, lambda t: (17.6 + VZ3 * (t - 6.0), W1 + VY3 * (t - 6.0) - 0.5 * G * (t - 6.0) ** 2), 2)
LAND3 = 17.6 + VZ3 * TAU3
late3.stay(6.0 + TAU3, 6.0 + TAU3 + 1.5, (LAND3, 0.0), 3)
late3.move(6.0 + TAU3 + 1.5, 6.0 + TAU3 + 2.6, (LAND3, 0.0), (LAND3 - 2.4, 0.0))
late3.note(4.4, 6.0, "늦게 옴 — 탑이 떠났다", "warn")
late3.note(6.0, 6.0 + TAU3 + 1.5, "뛰어 붙으려다 아래뜰로", "danger")
B3 = {
    "loop": P3, "cycle": P3, "w1": W1, "w2": W2, "floors": FL, "face1": FACE1, "walk1": FACE1 + L["WALL_T"], "face2": FACE2,
    "walk2": FACE2 + L["WALL_T"], "parapet": L["PARAPET"], "sillH": L["SILL_H"], "merlonH": L["MERLON_H"], "z0": Z0, "z1": Z1,
    "w": L["SIEGE_W"], "z": [round(zc(k * DT), 3) for k in range(steps(P3))],
    "bridge": [round(L["siege"](0, k * DT)[1], 2) for k in range(steps(P3))],
    "good": good3.sample(P3), "late": late3.sample(P3), "goodEv": good3.events, "lateEv": late3.events,
    "phases": phases([(0.0, 4.0, "타기", "go"), (4.0, 12.0, "굴러감 · 안에서 오르기", "move"), (12.0, 12.6, "다리 내림", "warn"),
                      (12.6, 16.0, "건너기", "go"), (16.0, 16.6, "다리 올림", "warn"), (16.6, P3, "돌아감", "still")]),
}

# ---------------------------------------------------------------- ④ the rooks' drill square (a top view of the board)
N, SQ, P4 = L["BOARD_N"], L["SQUARE"], L["ROOK_CYCLE"]
LOOP4 = 2 * P4
K4 = steps(LOOP4)
rooks = [[L["drill_rook"](n, k * DT) for k in range(K4)] for n in range(4)]
sq = L["square_mid"]
LINES = [("x", sq(idx, 0)[0]) if kind == "file" else ("z", sq(0, idx)[1]) for kind, idx, delay in L["DRILL_ROOKS"]]
CLEAR, BAND = 1.4, SQ / 2.0 + 0.3                # off a rook's middle; off a lit or sliding rook's line


def rook_at(n, k):
    return rooks[n][k % K4]


def rook_live(n, k):
    # Its line lit or the rook sliding along it.
    kind, idx, delay = L["DRILL_ROOKS"][n]
    return ((k * DT - delay) % P4) % (P4 / 2.0) >= L["ROOK_REST"] - 1e-9


def board_safe(x, z, k):
    for m in range(4):
        rx, rz, glow = rook_at(m, k)
        if math.hypot(rx - x, rz - z) < CLEAR:
            return False
        if rook_live(m, k) and abs((x if LINES[m][0] == "x" else z) - LINES[m][1]) < BAND:
            return False
    return True


def board_move(a, b, k, dk):
    pa, pb = sq(*a), sq(*b)
    return all(board_safe(lerp(pa[0], pb[0], n / dk), lerp(pa[1], pb[1], n / dk), k + n) for n in list(range(0, dk, 2)) + [dk])


ENTRY, EXIT = (N, 2), (-1, 5)
cells4 = {}
for c in range(-1, N + 1):
    for r in range(N):
        if (c < 0 or c >= N) and (c, r) not in (ENTRY, EXIT):
            continue
        cells4[(c, r)] = [((c + dc, r + dr), SQ * math.hypot(dc, dr) / RUN) for dc in (-1, 0, 1) for dr in (-1, 0, 1)
                          if (dc or dr) and 0 <= r + dr < N and ((0 <= c + dc < N) or (c + dc, r + dr) in (ENTRY, EXIT))]
T4 = 0.8
path4 = plan(cells4, board_move, ENTRY, {EXIT}, T4, LOOP4 - 3.0)
assert path4, "no way over the board"
good4 = Track()
EAST4, WEST4 = (L["GREAT_TOWER"][1] + 3.0, L["BOARD_ENTRY_Z"]), (L["GREAT_TOWER"][0] - 3.0, L["BOARD_EXIT_Z"])
good4.move(T4 - 1.2, T4, EAST4, sq(*ENTRY))
for (c, k), (c2, k2) in zip(path4, path4[1:]):
    good4.move(k * DT, k2 * DT, sq(*c), sq(*c2))
T_END4 = path4[-1][1] * DT
good4.move(T_END4, T_END4 + 1.2, sq(*EXIT), WEST4)
good4.note(T4, T_END4, "빛나는 줄 · 미끄러지는 줄을 비켜 건넘 (%.1f초)" % (T_END4 - T4), "go")
# A pawn that walks straight across, corner to corner, and is swept by a sliding rook.
fail4, hit = Track(), None
pa, pb = sq(*ENTRY), sq(*EXIT)
dist4 = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
for n in range(steps(P4)):
    s0 = P4 * 0.5 + n * DT
    for m in range(1, steps(dist4 / RUN)):
        tt = s0 + m * DT
        f = RUN * (tt - s0) / dist4
        x, z = lerp(pa[0], pb[0], f), lerp(pa[1], pb[1], f)
        k = steps(tt)
        for r in range(4):
            rx, rz, glow = rook_at(r, k)
            px, pz, _ = rook_at(r, k - 1)
            if not glow and abs(rx - px) + abs(rz - pz) > 1e-6 and math.hypot(rx - x, rz - z) < 1.3:
                hit = (s0, tt, r, (x, z))
                break
        if hit:
            break
    if hit:
        break
assert hit, "the straight walker is never hit"
S4, TH4, RH4, PH4 = hit
fail4.move(S4, TH4, pa, PH4)
kh = steps(TH4)
rx0, rz0, _ = rook_at(RH4, kh)
OFF4 = (PH4[0] - rx0, PH4[1] - rz0)
k = kh
while True:
    k += 1
    rx, rz, _ = rook_at(RH4, k)
    px, pz, _ = rook_at(RH4, k - 1)
    if abs(rx - px) + abs(rz - pz) < 1e-6:
        break
T_STOP4 = k * DT
LIMX, Z4A, Z4B = -L["BOARD_X0"] - 0.3, L["BOARD_Z0"] + 0.3, L["BOARD_Z0"] + N * SQ - 0.3


def swept(t):
    rx, rz, _ = rook_at(RH4, steps(t))
    return max(-LIMX, min(LIMX, rx + OFF4[0])), max(Z4A, min(Z4B, rz + OFF4[1]))


fail4.add(TH4, T_STOP4, swept, 2)
END4 = swept(T_STOP4 - DT)
fail4.stay(T_STOP4, T_STOP4 + 2.0, END4, 3)
fail4.note(S4, TH4, "대각선으로 곧장", "warn")
fail4.note(TH4, T_STOP4 + 2.0, "룩에 쓸려 흉벽까지 · 넘어짐", "danger")
B4 = {
    "loop": LOOP4, "cycle": P4, "n": N, "sq": SQ, "x0": L["BOARD_X0"], "z0": L["BOARD_Z0"], "tower": L["GREAT_TOWER"],
    "rooks": [[[round(x, 3), round(z, 3), 1 if g else 0] for x, z, g in rooks[n]] for n in range(4)],
    "lines": [[a, round(v, 3)] for a, v in LINES], "kinds": [[kind, idx, round(delay, 2)] for kind, idx, delay in L["DRILL_ROOKS"]],
    "good": good4.sample(LOOP4), "fail": fail4.sample(LOOP4), "goodEv": good4.events, "failEv": fail4.events,
    "route": [[round(v, 2) for v in sq(*c)] for c, k in path4], "entry": sq(*ENTRY), "exit": sq(*EXIT), "east": EAST4, "west": WEST4,
    "wall2E": L["WALL2_E"], "wall2W": L["WALL2_W"], "wallT": L["WALL_T"], "parapet": L["PARAPET"],
    "phases": phases([(0.0, 2.0, "쉼", "still"), (2.0, 3.0, "줄 빛남", "warn"), (3.0, 3.0 + L["ROOK_SLIDE"], "미끄러짐", "danger"),
                      (3.0 + L["ROOK_SLIDE"], 5.0 + L["ROOK_SLIDE"], "쉼", "still"), (5.0 + L["ROOK_SLIDE"], 6.0 + L["ROOK_SLIDE"], "줄 빛남", "warn"),
                      (6.0 + L["ROOK_SLIDE"], P4, "되돌아감", "danger")]),
}

# ---------------------------------------------------------------- ⑤ the cranes (sections across the ditch)
XR = L["ROOK_CRANE_X"]
W2I, W3F = L["wall2_inner_z"](XR), L["wall3_face_z"](XR)
PR, PK = L["RC_CYCLE"], L["KNIGHT_CYCLE"]
LOOPK = 2 * PK
# The knight's crane is 5.5 m east; drawn on the rook's section with its ends kept as far from the walls as they are.
K_LO = W2I + (L["KN_LOW_Z"] - L["wall2_inner_z"](L["KNIGHT_CRANE_X"]))
K_HI = W3F + (L["KN_HIGH_Z"] - L["wall3_face_z"](L["KNIGHT_CRANE_X"]))


def crown(u):
    # The giant rook's crown (z, y), u = 0 when it docks at the middle wall.
    y, z = L["rook_crane"](u + L["RC_DELAY"])
    return z, y


def knight(u):
    # (z, y, facing) of the knight's base, u = 0 when it starts turning at the middle wall's edge.
    y, z = L["knight_crane"](u + L["KNIGHT_DELAY"])
    s, facing = L["knight_phase"](u + L["KNIGHT_DELAY"])
    return lerp(K_LO, K_HI, (z - L["KN_LOW_Z"]) / (L["KN_HIGH_Z"] - L["KN_LOW_Z"])), y, facing


HANG = L["KNIGHT_HANG"]


def on_mane(u):
    # A rider's feet: behind the knight (south when it faces north); it swings round with it when it turns.
    z, y, facing = knight(u)
    return z - HANG[0] * math.cos(math.radians(facing)), y + HANG[1]


rider = Track()
LO = crown(0.0)
rider.stay(0.0, 0.3, (W2I - 1.4, W2))
rider.move(0.3, 1.7, (W2I - 1.4, W2), (LO[0] - 0.6, W2))
T_UP5 = L["RC_BOARD"] + L["RC_UP"] + L["RC_ACROSS"]
rider.add(1.7, T_UP5, lambda u: (crown(u)[0] - 0.6, crown(u)[1]))
HI = crown(T_UP5)
rider.move(T_UP5, T_UP5 + 2.2, (HI[0] - 0.6, W3), (W3F + 2.2, W3))
rider.move(T_UP5 + 2.2, T_UP5 + 3.2, (W3F + 2.2, W3), (W3F + 4.6, W3))
rider.note(0.3, 1.7, "멈춘 머리로 걸어 오름", "go")
rider.note(1.7, T_UP5, "서서 실려 감", "still")
rider.note(T_UP5, T_UP5 + 2.2, "승강 문으로 걸어 내림", "go")
jumper, faller = Track(), Track()
faller.stay(0.0, 1.0, (W2I - 0.5, W2))
MISS = (W2I + 0.35, W2 - 0.2)
faller.move(1.0, 1.4, (W2I - 0.5, W2), MISS, 2, lift=0.5)
t_l5, p_l5 = fall(faller, 1.4, MISS, 0.4, W2 - 0.2 - L["MAX_DROP"])
faller.stay(t_l5, t_l5 + 0.8, p_l5, 4)
faller.note(0.0, 1.0, "망설임", "warn")
faller.note(1.0, t_l5 + 0.8, "늦게 뛰어 놓침 → 해자, 체크포인트", "danger")
jumper.move(4.8, 5.8, (W2I - 3.0, W2), (W2I - 0.5, W2))
jumper.stay(5.8, 7.6, (W2I - 0.5, W2))
T_GRAB = PK + L["KNIGHT_TURN"] + 0.1
jumper.move(7.6, T_GRAB, (W2I - 0.5, W2), on_mane(T_GRAB), lift=0.5)
T_LET = PK + 2 * L["KNIGHT_TURN"] + L["KNIGHT_LEAP"]
jumper.add(T_GRAB, T_LET, on_mane)
t_lj, p_lj = fall(jumper, T_LET, on_mane(T_LET), 0.3, W3, 1)
jumper.move(t_lj, t_lj + 0.9, p_lj, (p_lj[0] + 2.2, W3))
jumper.note(5.8, 7.6, "돌아서는 끝을 기다림", "still")
jumper.note(7.6, T_GRAB, "뛰어 갈기 잡기", "go")
jumper.note(T_GRAB, T_LET - L["KNIGHT_TURN"], "매달려 날아감", "still")
jumper.note(T_LET - L["KNIGHT_TURN"], t_lj, "돌아설 때 앞쪽으로 돌아 놓음", "go")
B5 = {
    "rookLoop": PR, "knightLoop": LOOPK, "knightCycle": PK, "turn": L["KNIGHT_TURN"],
    "w2": W2, "w3": W3, "ditch": L["DITCH_Y"], "bailey": L["BAILEY_Y"], "face2": L["wall2_face_z"](XR), "inner2": W2I, "face3": W3F,
    "wallT": L["WALL_T"], "parapet": L["PARAPET"], "sillH": L["SILL_H"], "merlonH": L["MERLON_H"],
    "beam": 23.0, "crownR": L["CROWN_R"], "rookBody": L["ROOK_BODY"], "knightH": L["KNIGHT_H"], "knightR": L["KNIGHT_R"],
    "kLo": K_LO, "kHi": K_HI, "rcLow": LO[0], "rcHigh": HI[0], "drop": L["MAX_DROP"], "hang": HANG,
    "crown": [[round(v, 3) for v in crown(k * DT)] for k in range(steps(PR))],
    "knight": [[round(v, 3) for v in knight(k * DT)] for k in range(steps(PK))],
    "rider": rider.sample(PR), "jumper": jumper.sample(LOOPK), "faller": faller.sample(LOOPK),
    "riderEv": rider.events, "jumperEv": jumper.events, "fallerEv": faller.events,
    "rookPhases": phases([(0.0, L["RC_BOARD"], "멈춤 — 타기", "go"), (L["RC_BOARD"], L["RC_BOARD"] + L["RC_UP"], "곧게 위로", "move"),
                          (L["RC_BOARD"] + L["RC_UP"], T_UP5, "곧게 건넘", "move"), (T_UP5, T_UP5 + L["RC_OFF"], "멈춤 — 내리기", "go"),
                          (T_UP5 + L["RC_OFF"], PR - L["RC_UP"], "돌아옴", "still"), (PR - L["RC_UP"], PR, "아래로", "still")]),
    "knightPhases": phases([(0.0, L["KNIGHT_TURN"], "돌아섬 — 잡을 때", "go"), (L["KNIGHT_TURN"], L["KNIGHT_TURN"] + L["KNIGHT_LEAP"], "도약 → 안 성벽", "move"),
                            (L["KNIGHT_TURN"] + L["KNIGHT_LEAP"], 2 * L["KNIGHT_TURN"] + L["KNIGHT_LEAP"], "돌아섬 — 놓을 때", "go"),
                            (2 * L["KNIGHT_TURN"] + L["KNIGHT_LEAP"], PK, "도약 ← 가운데 성벽", "still")]),
}

# ---------------------------------------------------------------- ⑥ the king's banner (a front view and a section)
P6 = L["GUST_CYCLE"]
LOOP6 = 2 * P6
FACE6, LEDGE, GY = L["GATEHOUSE"][0], L["LEDGE_Y"], L["GATE_Y"]
CLOTH = FACE6 - 0.08                             # the upper length hangs flat on the face from the roof's edge
LEDGE_X = FACE6 - L["LEDGE_DEPTH"]
# R860: the lower length hangs from the ledge's front edge (the climb's top-out lands 0.63 m in from a face's top).
LOW_H, UP_H = ((LEDGE_X - 0.06) if L.get("LOWER_HUNG_OUT") else CLOTH, LEDGE - 0.05), (CLOTH, GY)
OFF6 = 0.35                                      # a climber's middle off the cloth
ON_LEDGE = LEDGE_X + 0.55                        # standing on the ledge
BZ = L["BANNER_Z"]
LIP, REACH = 1.25, 1.3                           # hands on the ledge's lip / on the roof's edge: this far below the top


def on_cloth(hinge, d, t):
    # (x, y) of a climber d below the length's top, its cloth billowed out by the gust.
    a = math.radians(L["banner_angle"](t))
    return hinge[0] - d * math.sin(a) - OFF6 * math.cos(a), hinge[1] - d * math.cos(a) + OFF6 * math.sin(a)


def climb_cloth(z, hinge, d0, t0):
    def f(t):
        x, y = on_cloth(hinge, d0 - (t - t0) * CLIMB, t)
        return z, y, x
    return f


def at_cloth(z, hinge, d, t):
    x, y = on_cloth(hinge, d, t)
    return z, y, x


def cloth_x(y, t):
    # The upper length's x at height y.
    a = math.radians(L["banner_angle"](t))
    return CLOTH - (UP_H[1] - y) * math.tan(a)


def mantle(track, t, z, p0):
    # From the lower length's top onto the ledge: hands on the lip, over it.
    mid = (z, LEDGE + 0.25, LEDGE_X + 0.15)
    track.move(t, t + 0.45, p0, mid)
    track.move(t + 0.45, t + 0.8, mid, (z, LEDGE, ON_LEDGE))
    return t + 0.8


ZG, ZB = 74.0, 72.4                              # where on the 3.6 m wide banner the two climb
ASIDE = BZ[1] + 1.2                              # beside the banner, north of it, on the walk and on the ledge
WALK_X = FACE6 - 1.6
D_LOW, D_UP = LOW_H[1] - W3, UP_H[1] - LEDGE
g6, f6 = Track(), Track()
g6.stay(0.0, 3.0, (ASIDE, W3, WALK_X))
g6.move(3.0, 4.0, (ASIDE, W3, WALK_X), (ZG, W3, CLOTH - OFF6))
t = g6.add(4.0, 4.0 + (D_LOW - LIP) / CLIMB, climb_cloth(ZG, LOW_H, D_LOW, 4.0))
t = mantle(g6, t, ZG, at_cloth(ZG, LOW_H, LIP, t))
g6.move(t, t + 1.0, (ZG, LEDGE, ON_LEDGE), (ASIDE, LEDGE, ON_LEDGE))
T_ASIDE = t + 1.0
g6.stay(T_ASIDE, P6 + 3.4, (ASIDE, LEDGE, ON_LEDGE))
g6.move(P6 + 3.4, P6 + 4.4, (ASIDE, LEDGE, ON_LEDGE), (ZG, LEDGE, CLOTH - OFF6))
t = g6.add(P6 + 4.4, P6 + 4.4 + (D_UP - REACH) / CLIMB, climb_cloth(ZG, UP_H, D_UP, P6 + 4.4))
p_top = at_cloth(ZG, UP_H, REACH, t)
g6.move(t, t + 0.4, p_top, (ZG, GY - 0.25, FACE6 - 0.15))
g6.move(t + 0.4, t + 0.7, (ZG, GY - 0.25, FACE6 - 0.15), (ZG, GY, FACE6 + 0.6))
T_ROOF6 = t + 0.7
g6.move(T_ROOF6, LOOP6 - 0.05, (ZG, GY, FACE6 + 0.6), (ZG, GY, FACE6 + 2.2))
g6.note(0.0, 3.0, "깃발 옆에서 기다림", "still")
g6.note(4.0, T_ASIDE - 1.0, "아래 폭 → 턱", "go")
g6.note(T_ASIDE - 1.0, P6 + 3.4, "턱 위, 깃발 옆에서 돌풍을 기다림", "still")
g6.note(P6 + 4.4, T_ROOF6, "위 폭 → 지붕", "go")
# The one who gets it wrong twice: starts up while the pennants stream, then waits on the ledge in front of the banner.
f6.stay(0.0, 0.6, (ZB, W3, CLOTH - OFF6))
T_G1 = L["GUST_WARN"]
f6.add(0.6, T_G1, climb_cloth(ZB, LOW_H, D_LOW, 0.6))
t_r6 = T_G1
while L["banner_angle"](t_r6) < 90.0 - L["CLIMB_MIN_DEG"]:
    t_r6 += 0.01
f6.add(T_G1, t_r6, climb_cloth(ZB, LOW_H, D_LOW, 0.6), 2)
p_r6 = climb_cloth(ZB, LOW_H, D_LOW, 0.6)(t_r6)
t_l6, p_l6 = fall(f6, t_r6, p_r6, -1.2, W3)
f6.stay(t_l6, 3.3, p_l6, 3)
f6.move(3.3, 3.9, p_l6, (ZB, W3, CLOTH - OFF6))
t = f6.add(3.9, 3.9 + (D_LOW - LIP) / CLIMB, climb_cloth(ZB, LOW_H, D_LOW, 3.9))
T_LEDGE_F = mantle(f6, t, ZB, at_cloth(ZB, LOW_H, LIP, t))
T_G2 = P6 + L["GUST_WARN"]
f6.stay(T_LEDGE_F, T_G2, (ZB, LEDGE, ON_LEDGE))
t_o6 = T_G2
while cloth_x(LEDGE + 0.7, t_o6) - 0.3 > LEDGE_X:
    t_o6 += 0.01
f6.add(T_G2, t_o6, lambda t: (ZB, LEDGE, min(ON_LEDGE, cloth_x(LEDGE + 0.7, t) - 0.3)), 2)
t_l62, p_l62 = fall(f6, t_o6, (ZB, LEDGE, LEDGE_X), -1.0, W3)
f6.stay(t_l62, t_l62 + 1.5, p_l62, 3)
f6.note(0.6, T_G1, "작은 깃발이 펄럭이는데 오름", "warn")
f6.note(T_G1, t_l6, "젖혀져 놓침", "danger")
f6.note(3.9, T_LEDGE_F, "다시 → 턱", "go")
f6.note(T_LEDGE_F, T_G2, "턱 위, 깃발 앞에 서 있음", "warn")
f6.note(T_G2, t_l62 + 1.5, "젖혀지는 깃발에 밀려 떨어짐", "danger")
B6 = {
    "loop": LOOP6, "cycle": P6, "face": FACE6, "cloth": CLOTH, "w3": W3, "ledge": LEDGE, "ledgeX": LEDGE_X, "ledgeZ": L["LEDGE_Z"], "roof": GY,
    "lowH": LOW_H, "upH": UP_H, "bz": BZ, "gate": L["GATEHOUSE"], "warn": L["GUST_WARN"], "limit": 90.0 - L["CLIMB_MIN_DEG"],
    "billow": L["BILLOW"], "walk3": [L["wall3_face_z"](FACE6), L["wall3_face_z"](FACE6) + L["WALL_T"]],
    "tower": L["GATE_TOWERS"][0], "bailey": L["BAILEY_Y"], "parapet": L["PARAPET"], "sillH": L["SILL_H"], "merlonH": L["MERLON_H"],
    "angle": [round(L["banner_angle"](k * DT), 2) for k in range(steps(P6))],
    "good": g6.sample(LOOP6), "fail": f6.sample(LOOP6), "goodEv": g6.events, "failEv": f6.events,
    "phases": phases([(0.0, L["GUST_WARN"], "작은 깃발 펄럭 (예고)", "warn"), (L["GUST_WARN"], L["GUST_WARN"] + L["GUST_OUT"], "젖혀짐", "danger"),
                      (L["GUST_WARN"] + L["GUST_OUT"], L["GUST_WARN"] + L["GUST_OUT"] + L["GUST_HOLD"], "버팀", "danger"),
                      (L["GUST_WARN"] + L["GUST_OUT"] + L["GUST_HOLD"], P6 - L["CALM"] + L["GUST_WARN"], "돌아옴", "danger"),
                      (P6 - L["CALM"] + L["GUST_WARN"], P6, "잠잠함 — 오를 때", "go")]),
}

DATA = {"dt": DT, "b1": B1, "b2": B2, "b3": B3, "b4": B4, "b5": B5, "b6": B6}

# ---------------------------------------------------------------- the plan (inline) and the page
tmp = os.path.join(tempfile.gettempdir(), "rampart_plan_inline.svg")
L["draw"](tmp)
PLAN = io.open(tmp, encoding="utf-8").read()
PLAN = PLAN.replace('<svg xmlns="http://www.w3.org/2000/svg" viewBox=', '<svg class="plan" xmlns="http://www.w3.org/2000/svg" viewBox=', 1)
TOTAL = L["budget_total"]()

print("① ladder: climbable from %.2f s, a pawn starts by %.2f, a rook by %.2f; let go at %.2f; the late one lands at z %.2f (%.2f s)" % (
    U_UP, LATE_PAWN, LATE_ROOK, T_REL1, p_land[0], t_land))
print("② murder holes: across %.2f -> %.2f s in %d steps; the runner falls at %.2f s (x %.2f)" % (path2[0][1] * DT, T_END2, len(path2), TF2, XF2))
print("③ siege tower: up at %.2f s; the late jumper lands at z %.2f" % (T_TOP3, LAND3))
print("④ drill square: across %.2f -> %.2f s over %d steps; the straight walker is swept by rook %d at %.2f s, stops at %.2f" % (
    path4[0][1] * DT, T_END4, len(path4), RH4, TH4, T_STOP4))
print("⑤ knight: grab %.2f, let go %.2f, lands at z %.2f at %.2f (its base reaches z %.2f); the faller goes at %.2f" % (
    T_GRAB, T_LET, p_lj[0], t_lj, K_HI + L["KNIGHT_R"], t_l5))
print("⑥ banner: aside on the ledge at %.2f; the careless one lets go at %.2f, pushed off the ledge at %.2f" % (T_ASIDE, t_r6, t_o6))

PAGE = io.open(os.path.join(HERE, "rampart_2d_page.html"), encoding="utf-8").read()
PAGE = PAGE.replace("/*DATA*/null", json.dumps(DATA, ensure_ascii=False, separators=(",", ":")))
PAGE = PAGE.replace("<!--PLAN-->", PLAN)
PAGE = PAGE.replace("{{TOTAL}}", L["clock"](TOTAL)).replace("{{CROWD}}", L["clock"](TOTAL * L["CROWD"])).replace("{{LENGTH}}", "%.0f" % L["way_length"]()[0])
with io.open(sys.argv[1], "w", encoding="utf-8", newline="\n") as f:
    f.write(PAGE)
print("wrote", sys.argv[1], "%.0f KB" % (len(PAGE.encode("utf-8")) / 1024.0))
