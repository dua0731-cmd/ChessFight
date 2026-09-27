#!/usr/bin/env python3
# Queen of the Hill graybox map, "sky palace", 8th revision (R50): seven floors, one
# per rank of the chessboard, and the summit, rank 8, where a pawn promotes. Every
# floor is a big course of its own, 26 m high: each team climbs its own half of the
# tower through a WEST or an EAST wing (two different courses to choose between), from
# the team's plaza on rank n out and round and back to its plaza on rank n+1.
# Floors 4 and 7 are shared (the centre of the board, and the final approach).
#
# The team's plazas stack up in front of its half of the tower. Beside them:
#   - the team's VACUUM TUBE: a fall puts a player back on rank 1 beside it, and it
#     lifts the player straight to the highest rank IT has stepped on (the rank pad
#     of each plaza). Personal: a teammate's progress never carries anyone.
#   - the team's SHORTCUTS: a lift up each floor that appears when the first player
#     of the team rings the team's bell on the next plaza. The team's alone.
# There are no checkpoints (QueenHillRace in Assets/Scripts/Core has the rules).
#
# This script is the ONLY source of the map. It writes
#   Assets/Resources/QueenHill/QueenHillLayout.json
# which QueenHillLevel (Assets/Scripts/Gameplay/QueenHill) turns into the scene
# when Play starts, and which Tools/QueenHill/preview renders without Unity.
# Never edit the JSON by hand; change this file and run it again:
#   python3 Tools/QueenHill/build_layout.py            (writes the JSON)
#   python3 Tools/QueenHill/build_layout.py --check    (fails if the JSON is stale)
#
# Coordinates are Unity's: x east, y up, z north, metres. The white team is south
# (-z), the black team north (+z); a team's half is built once and turned 180
# degrees for the other, so both get the same course.
#
# Each wing is three LEGS, a U round the team's half: out along the north lane (z -26),
# down the corner lane (x +-55), back along the south lane (z -60). Each leg is a row
# of MODULES (a flight of stairs, gondolas, a knight's pads, a clock face...) joined by
# landings; a module knows its own length and rise. That is also the shape future
# random generation will fill (DESIGN §3.6).
#
# What the pawn can do (Docs/RagdollLab/README.md, RagdollTuning): runs 5.5 m/s,
# sprints 9.6 m/s, jumps about 1 m high and 4-5 m far running, climbs any face
# steeper than 55 degrees at 1.2 m/s for about 8 m on one bar of stamina, grabs a
# ledge within 0.4 m of its top, climbs chains, swings on ropes over 8 m, rides
# anything that moves (IMovingSurface). The modules keep to that: jumps of up to
# 1.5 m across and 1 m up, climbs of 4-6.5 m between rests.
import json, math, os, sys

OUT = "Assets/Resources/QueenHill/QueenHillLayout.json"

# ------------------------------------------------------------------ the frame

FIRST_RANK, FLOOR_HEIGHT, RANKS = 16.0, 26.0, 8
FLOORS = RANKS - 1
BRIDGE_TOP, SEA_FLOOR, CLIFF, BRIDGE_GAP = 12.0, -8.0, 50.0, 10.5


def H(rank):
    """Height of the top of rank `rank` (1..8): 16, 42, ... 198."""
    return FIRST_RANK + FLOOR_HEIGHT * (rank - 1)


# Half-width of the tower core on each floor; floor 7 is the round crown drum inside the spiral stairs.
CORE = {1: 14.0, 2: 13.5, 3: 13.0, 4: 12.5, 5: 12.0, 6: 11.5, 7: 7.6}
TERRACE = 6.0
SLAB = 1.5


def terrace_half(rank):
    """Half-width of the square floor round the core on rank 2..7 (rank 7: the grand balcony)."""
    return 30.0 if rank == 7 else CORE[rank] + TERRACE


# The team's hub, in its own frame (white: as written; black: turned 180 degrees).
PLAZA_X, PLAZA_Z0, PLAZA_Z1 = 9.0, -50.0, -32.0
TUBE_X0, TUBE_X1, TUBE_Z0, TUBE_Z1 = -13.0, -9.0, -42.0, -38.0
LIFT_X0, LIFT_X1 = 9.3, 12.9


def lift_lane(floor):
    """The shortcut lifts of odd and even floors stand in two lanes, so one arriving and the next waiting never meet."""
    return (-46.0, -42.6) if floor % 2 == 1 else (-41.4, -38.0)


# The wing lanes (white frame, east wing; the west wing is the same with x negated).
LANE_N_Z, LANE_C_X, LANE_S_Z, HALF_LANE = -26.0, 55.0, -60.0, 3.0
LANE_N_LEN, LANE_C_LEN, LANE_S_LEN = 40.0, 28.0, 38.0

FLOOR_NAMES = {
    1: "폭포 테라스 · 대계단",
    2: "사슬 곤돌라",
    3: "떠오르는 석판 · 나이트 · 비숍",
    4: "궤도 고리 · 룩의 탑",
    5: "시계 태엽",
    6: "공중 정원",
    7: "궁전 대발코니 · 빛의 계단",
}

# ------------------------------------------------------------------ pieces

pieces = []
_names = {}


def uniq(name):
    n = _names.get(name, 0)
    _names[name] = n + 1
    return name if n == 0 else "%s %d" % (name, n + 1)


def r3(v):
    return [round(float(x), 3) for x in v]


def add(kind, name, p, s=(1, 1, 1), r=(0, 0, 0), c="", g="", m="", a=None, f="", b=None, t=""):
    d = {"n": uniq(name), "k": kind, "p": r3(p), "s": r3(s), "r": r3(r)}
    if c: d["c"] = c
    if g: d["g"] = g
    if m: d["m"] = m
    if a is not None: d["a"] = r3(a)
    if f: d["f"] = f
    if b is not None: d["b"] = r3(b)
    if t: d["t"] = t
    pieces.append(d)
    return d["n"]


class Frame:
    """A team half: identity (south, white) or turned 180 degrees about the tower axis (north, black).
    A piece built with F.p(...) and F.r(...) keeps its motion's local travel and axis in frame
    coordinates: the 180-degree turn carries them round with it."""

    def __init__(self, turned):
        self.turned = turned
        self.tag = "N" if turned else "S"

    def p(self, x, y, z):
        return (-x, y, -z) if self.turned else (x, y, z)

    def yaw(self, deg):
        return deg + 180.0 if self.turned else deg

    def r(self, rx=0.0, ry=0.0, rz=0.0):
        return (rx, self.yaw(ry), rz)


SOUTH, NORTH = Frame(False), Frame(True)
WORLD = SOUTH


def on_cliff_top(x0, x1, z0, z1, top):
    """A floor whose top would lie in the cliff's top face: on rank 1 the cliff is the floor."""
    return abs(top - H(1)) < 1e-6 and -CLIFF <= x0 and x1 <= CLIFF and -CLIFF <= z0 and z1 <= CLIFF


def span(name, x0, x1, y0, y1, z0, z1, c, F=WORLD, kind="box", g=""):
    """An axis-aligned box from its extents, in a frame. A floor lying on the cliff top is left out."""
    if kind == "box" and on_cliff_top(min(x0, x1), max(x0, x1), min(z0, z1), max(z0, z1), max(y0, y1)):
        return None
    cx, cy, cz = (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2
    return add(kind, name, F.p(cx, cy, cz), (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)), F.r(), c, g=g)


def slope(name, a, b, width, c, F=WORLD, thick=0.6, kind="box", g=""):
    """A ramp whose top surface runs along the centre line from a to b (frame points)."""
    ax, ay, az = a
    bx, by, bz = b
    dx, dy, dz = bx - ax, by - ay, bz - az
    flat = math.hypot(dx, dz)
    length = math.sqrt(flat * flat + dy * dy)
    yaw = math.degrees(math.atan2(dx, dz))
    pitch = -math.degrees(math.atan2(dy, flat))
    # Move the centre down along the ramp's own up so the TOP passes through the line.
    # Unity: Euler(pitch, yaw, 0) turns local up into (sin p sin y, cos p, sin p cos y).
    ux = math.sin(math.radians(pitch)) * math.sin(math.radians(yaw))
    uy = math.cos(math.radians(pitch))
    uz = math.sin(math.radians(pitch)) * math.cos(math.radians(yaw))
    cx = (ax + bx) / 2 - ux * thick / 2
    cy = (ay + by) / 2 - uy * thick / 2
    cz = (az + bz) / 2 - uz * thick / 2
    return add(kind, name, F.p(cx, cy, cz), (width, thick, length + 0.05), F.r(pitch, yaw, 0), c, g=g)


def cyl(name, p, diameter, height, c, F=WORLD, kind="cyl", r=(0, 0, 0), g=""):
    return add(kind, name, F.p(*p), (diameter, height, diameter), F.r(*r), c, g=g)


def group(name, p, F=WORLD, r=(0, 0, 0), m="", a=None, f="", b=None, g=""):
    return add("group", name, F.p(*p), r=F.r(*r), m=m, a=a, f=f, b=b, g=g)


def sign(text, p, F=WORLD, yaw=0.0, size=0.5, standing=False):
    """Words on the floor (readable walking along the frame's +yaw direction), or standing on a wall."""
    rot = F.r(0, yaw, 0) if standing else F.r(90, yaw, 0)
    return add("sign", "Sign", F.p(*p), r=rot, f="sign", b=[size], t=text)


def cypress(x, y, z, F=WORLD, g=""):
    add("vbox", "Cypress", F.p(x, y + 1.6, z), (0.9, 3.2, 0.9), F.r(), "cypress", g=g)


def hoop(name, centre, radius, thickness, c, F=WORLD, facing=0.0, upright=True, segments=24, g=""):
    """A ring you only look at (a wheel rim, the gate): box segments round a centre.
    Upright rings stand in the frame's x-y plane turned by `facing` (their axis along the
    `facing` direction); flat ones lie in x-z."""
    rot = F.r(0, facing, 0) if upright else F.r()
    h = add("group", name, F.p(*centre), r=rot, g=g)
    chord = 2 * radius * math.sin(math.pi / segments) + 0.05
    for i in range(segments):
        a = 2 * math.pi * (i + 0.5) / segments
        if upright:
            add("vbox", "Hoop", (radius * math.cos(a), radius * math.sin(a), 0), (thickness, chord, thickness),
                (0, 0, math.degrees(a)), c, g=h)
        else:
            add("vbox", "Hoop", (radius * math.cos(a), 0, radius * math.sin(a)), (thickness, thickness, chord),
                (0, -math.degrees(a), 0), c, g=h)
    return h


def waterfall(x, y_top, y_bottom, z, width, F=WORLD, along_x=True):
    size = (width, y_top - y_bottom, 0.3) if along_x else (0.3, y_top - y_bottom, width)
    add("vbox", "Waterfall", F.p(x, (y_top + y_bottom) / 2, z), size, F.r(), "waterfall")


def mover(name, p, size, travel, speed, pause, c="gold", F=WORLD, spin=0.0, axis=(0, 1, 0), phase=0.0, kind="box", g=""):
    """A MovingPlatform: `travel` in frame coordinates (the frame's turn carries it round)."""
    ramp = max(0.5, speed / 6.0)
    return add(kind, name, F.p(*p), size, F.r(), c, g=g, m="move",
               a=[travel[0], travel[1], travel[2], speed, pause, ramp, spin, axis[0], axis[1], axis[2], phase])


def rope(name, top, length, F=WORLD, facing=0.0, swing=0.0, period=5.0, phase=0.0, thickness=0.1, c="chain"):
    """A chain or rope hanging from `top` (a frame point); a swing moves toward the frame's `facing`."""
    return add("group", name, F.p(*top), r=F.r(0, facing, 0), f="rope", b=[length, swing, period, phase, thickness], t=c)


def launch(name, surface, facing, throw, clearance, F=WORLD, period=0.0, phase=0.0, same=False, size=(1.8, 0.6, 1.8)):
    add("vbox", name + " Plate", F.p(surface[0], surface[1] + 0.02, surface[2]), (size[0], 0.04, size[2]), F.r(0, facing, 0), "gold")
    return add("trig", name, F.p(*surface), size, F.r(0, facing, 0), f="launch",
               b=[throw[0], throw[1], throw[2], clearance, period, phase, 1 if same else 0])


def orbit_ring(name, radius, low_y, high_y, low_east, dps):
    """A chequered ring round the tower, tilted about the north-south axis so it is low on one
    side (east or west) and high on the other, turning in its own plane: whoever rides it is
    carried up (or down) as it goes round."""
    centre = (low_y + high_y) / 2
    tilt = math.degrees(math.asin((high_y - low_y) / 2 / radius))
    # Tilting about Z by +tilt raises the +x side in Unity.
    rz = -tilt if low_east else tilt
    g = group(name, (0, centre, 0), WORLD, r=(0, 0, rz), m="move", a=[0, 0, 0, 0, 0, 0.5, dps, 0, 1, 0, 0])
    segs = 36
    chord = 2 * radius * math.sin(math.pi / segs) + 0.08
    for i in range(segs):
        a = 2 * math.pi * (i + 0.5) / segs
        add("box", "Ring Segment", (radius * math.cos(a), -0.3, radius * math.sin(a)), (3.0, 0.6, chord),
            (0, -math.degrees(a), 0), "lapis" if i % 2 == 0 else "palegold", g=g)
    return g


# ------------------------------------------------------------------ lanes and modules

class Lane:
    """A straight stretch of a wing, in a team frame. u runs along it (from `origin` in
    direction `sign` of `axis`), v across it (+v = +z for an x lane, +x for a z lane).
    Moving pieces are built unturned in the frame, so their travel is `vec(...)`."""

    def __init__(self, F, origin, axis, sign):
        self.F = F
        self.ox, self.oz = origin
        self.axis, self.dirn = axis, sign

    def xz(self, u, v):
        if self.axis == "x":
            return (self.ox + self.dirn * u, self.oz + v)
        return (self.ox + v, self.oz + self.dirn * u)

    def pt(self, u, v, y):
        x, z = self.xz(u, v)
        return (x, y, z)

    def vec(self, du, dv, dy):
        """A direction or offset along/across the lane, in frame coordinates."""
        if self.axis == "x":
            return (self.dirn * du, dy, dv)
        return (dv, dy, self.dirn * du)

    def dims(self, su, sy, sv):
        """The frame-axis size of a box su long (along the lane) and sv wide (across)."""
        return (su, sy, sv) if self.axis == "x" else (sv, sy, su)

    @property
    def yaw(self):
        """Yaw of the +u direction (what faces along the lane)."""
        if self.axis == "x":
            return 90.0 if self.dirn > 0 else -90.0
        return 0.0 if self.dirn > 0 else 180.0

    def box(self, name, u0, u1, v0, v1, y0, y1, c, kind="box", g=""):
        x0, z0 = self.xz(u0, v0)
        x1, z1 = self.xz(u1, v1)
        return span(name, min(x0, x1), max(x0, x1), y0, y1, min(z0, z1), max(z0, z1), c, self.F, kind, g)

    def solid(self, name, u, v, y, su, sy, sv, c, kind="box", m="", a=None):
        """A box centred at (u, v, y), unturned in the frame."""
        return add(kind, name, self.F.p(*self.pt(u, v, y)), self.dims(su, sy, sv), self.F.r(), c, m=m, a=a)

    def osc(self, name, u, v, y, su, sy, sv, c, du, dv, dy, period, phase):
        """A box sliding to and fro by (du, dv, dy) either side of (u, v, y) (an Oscillator)."""
        t = self.vec(du, dv, dy)
        return self.solid(name, u, v, y, su, sy, sv, c, m="osc", a=[t[0], t[1], t[2], period, phase])

    def ramp(self, name, u0, y0, u1, y1, v=0.0, width=2 * HALF_LANE, c="honey", thick=1.0):
        return slope(name, self.pt(u0, v, y0), self.pt(u1, v, y1), width, c, self.F, thick=thick)

    def sign(self, text, u, v, y, size=0.26):
        sign(text, self.pt(u, v, y + 0.03), self.F, yaw=self.yaw, size=size)


W = HALF_LANE


def landing(L, u, y, length=4.0, c="garden", name="Landing"):
    L.box(name, u, u + length, -W, W, y - 1, y, c)
    return u + length, y


def m_flight(L, u, y, length, rise, stone=False, gap=0.0, name="Grand Stair"):
    """A flight of the grand stair (a ramp), a balustrade on its -v side; a stone sliding across
    it; or a broken middle to jump."""
    rail = dict(v=-W + 0.15, width=0.3, c="marble", thick=0.9)
    if gap > 0:
        a, b = u + (length - gap) / 2, u + (length + gap) / 2
        ya, yb = y + rise * (a - u) / length, y + rise * (b - u) / length
        L.ramp(name, u, y, a, ya)
        L.ramp(name, b, yb, u + length, y + rise)
        L.ramp(name + " Balustrade", u, y + 0.9, a, ya + 0.9, **rail)
        L.ramp(name + " Balustrade", b, yb + 0.9, u + length, y + rise + 0.9, **rail)
        L.sign("무너진 계단: %.1f m 뛰어넘기" % gap, u + 1.0, 0.8, y + 0.3)
    else:
        L.ramp(name, u, y, u + length, y + rise)
        L.ramp(name + " Balustrade", u, y + 0.9, u + length, y + rise + 0.9, **rail)
    if stone:
        L.osc(name + " Sliding Stone", u + length * 0.45, 0, y + rise * 0.45 + 0.65, 1.0, 1.1, 1.0, "ruby",
              0, 2.2, 0, 4.0, 0)
        L.sign("미끄러지는 돌", u - 1.0, 0, y)
    return u + length, y + rise


def m_arcade(L, u, y, h1=4.5, h2=4.5):
    """A portico: climb a front pillar and the roof's edge onto the chequered roof, then the
    tower behind it from the roof (from the ground the roof is in the way)."""
    L.box("Arcade Floor", u, u + 14, -W, W, y - 1, y, "garden")
    for pu in (u, u + 5.0):
        for pv in (-2.0, 2.0):
            L.box("Arcade Pillar", pu, pu + 1.2, pv - 0.6, pv + 0.6, y, y + h1, "marble")
    L.box("Arcade Roof", u, u + 8, -W, W, y + h1, y + h1 + 0.6, "checker")
    L.box("Arcade Tower", u + 8, u + 14, -W, W, y, y + h1 + h2 - 0.3, "marble")
    L.box("Arcade Tower Top", u + 8, u + 14, -W, W, y + h1 + h2 - 0.3, y + h1 + h2, "garden")
    L.sign("아치: 앞 기둥 %.1f m → 지붕 → 탑 %.1f m" % (h1 + 0.6, h2 - 0.6), u - 1.2, 0, y)
    return u + 14, y + h1 + h2


def m_pawn_march(L, u, y, length, n=3):
    """A bridge where statues of pawns march up and down it: they push whoever is in the way."""
    L.box("Pawn Bridge", u, u + length, -2.2, 2.2, y - 1, y, "checker")
    for i in range(n):
        cu = u + length * (i + 0.5) / n
        du = length / n / 2 - 0.8
        g = L.osc("Marching Pawn", cu, (-1.1, 1.1, 0)[i % 3], y + 0.9, 1.2, 1.8, 1.2, "ruby", du, 0, 0, 5.0 + i, 1.3 * i)
        add("vbox", "Pawn Head", (0, 1.25, 0), (0.8, 0.7, 0.8), c="ruby", g=g)
    L.sign("폰의 행진: 밀려 떨어지지 않게", u + 1, 0, y)
    return u + length, y


def m_checkerboard(L, u, y, length, period=6.0):
    """The collapsing chessboard: the blue and the gold squares take turns to vanish."""
    n_u = int(round(length / 2.0))
    for i in range(n_u):
        for j in range(3):
            dark = (i + j) % 2 == 0
            L.solid("Chessboard Square", u + 2.0 * i + 1.0, -W + 2.0 * j + 1.0, y - 0.25, 1.96, 0.5, 1.96,
                    "lapis" if dark else "palegold", m="toggle", a=[period, 0.0 if dark else 0.45, 0.55 if dark else 1.0, 0.0])
    L.sign("무너지는 체스판: 파랑과 금색이 번갈아 사라진다", u - 1.2, 0, y)
    return u + 2.0 * n_u, y


def m_piston_ramp(L, u, y, length, rise, n=3):
    """A ramp with pistons shoving across it from the +v side, out of housings beside it."""
    L.ramp("Piston Ramp", u, y, u + length, y + rise)
    for i in range(n):
        pu = u + length * (i + 0.6) / (n + 0.2)
        py = y + rise * (pu - u) / length + 0.8
        # Retracted its face is at the lane's edge (v = W); pushed out it reaches v = -1.
        L.osc("Piston", pu, W - 1.4, py, 1.2, 1.2, 1.2, "ruby", 0, 2.0, 0, 3.2, 0.9 * i)
        L.box("Piston Housing", pu - 0.8, pu + 0.8, W + 1.2, W + 2.2, py - 1.2, py + 1.2, "marble")
    L.sign("피스톤 경사로", u - 1.0, 0, y)
    return u + length, y + rise


def m_wall(L, u, y, h1, h2, c="marble", ivy=False, name="Climbing Wall"):
    """A stepped wall across the lane, climbed from its -u face: h1, a 1 m rest ledge, h2."""
    L.box(name + " Foot", u, u + 1.5, -W, W, y - 1, y, "honey")
    L.box(name, u + 1.5, u + 6, -W, W, y, y + h1 - 0.3, c)
    L.box(name + " Rest", u + 1.5, u + 6, -W, W, y + h1 - 0.3, y + h1, "garden")
    L.box(name, u + 2.5, u + 6, -W, W, y + h1, y + h1 + h2 - 0.3, c)
    L.box(name + " Top", u + 2.5, u + 6, -W, W, y + h1 + h2 - 0.3, y + h1 + h2, "garden")
    if ivy:
        L.box("Ivy", u + 1.45, u + 1.5, -W + 0.4, W - 0.4, y + 0.5, y + h1 - 0.5, "cypress", kind="vbox")
        L.box("Ivy", u + 2.45, u + 2.5, -W + 0.4, W - 0.4, y + h1 + 0.3, y + h1 + h2 - 0.5, "cypress", kind="vbox")
    L.sign("벽 %.1f m + %.1f m (쉬는 턱)" % (h1, h2), u + 0.2, 0, y)
    return u + 6, y + h1 + h2


def m_statues(L, u, y, length, n=4):
    """Stepping stones: rook statues sliding across the gap, their tops the only floor."""
    step = length / n
    for i in range(n):
        g = L.osc("Rook Statue", u + step * (i + 0.5), 0, y - 1.5, 2.3, 3.0, 2.3, "gold", 0, 2.4, 0, 4.0 + 0.7 * i, 1.1 * i)
        for dx in (-0.85, 0.85):
            for dz in (-0.85, 0.85):
                add("vbox", "Merlon", (dx, 1.7, dz), (0.5, 0.4, 0.5), c="gold", g=g)
    L.sign("움직이는 룩 징검다리", u - 1.0, 0, y)
    return u + length, y


def m_steps(L, u, y, n, du=2.0, dy=1.0, gap=1.2, c="honey"):
    """Blocks rising dy each with a gap between: jump up them."""
    for i in range(n):
        u0 = u + i * (du + gap) + gap
        L.box("Jump Step", u0, u0 + du, -2.0, 2.0, y + dy * (i + 1) - 1.0, y + dy * (i + 1), c)
    return u + n * (du + gap) + gap, y + n * dy


def m_gondolas(L, u, y, rise, n=3, period=8.0):
    """Chequered cubes on chains, bobbing in turn: one at its top when the next is at its bottom."""
    step = (rise - 5.0) / (n - 1)
    for k in range(n):
        mid = y + 2.5 + step * k            # the top swings between mid-2.5 and mid+2.5
        g = L.osc("Gondola Cube", u + 2.7 + 4.2 * k, (-1.0, 1.0)[k % 2], mid - 1.5, 3, 3, 3, "checker",
                  0, 0, 2.5, period, period / 2 * k)
        add("vbox", "Chain", (0, 1.5 + 4, 0), (0.12, 8, 0.12), c="chain", g=g)
    L.sign("곤돌라: 꼭대기의 큐브에서 바닥의 다음 큐브로", u - 1.0, 0, y)
    return u + 2.4 + 4.2 * n, y + rise


def m_chain_ladder(L, u, y, rise):
    """Two chains to climb: one up to a balcony half way, one up to the top of the column."""
    half = rise / 2
    L.box("Ladder Foot", u, u + 2.0, -W, W, y - 1, y, "honey")
    L.box("Chain Beam", u + 1.2, u + 2.0, -0.4, 0.4, y + half - 0.3, y + half, "marble", kind="vbox")
    L.box("Ladder Balcony", u + 2.0, u + 3.6, -1.8, 1.8, y + half - 0.6, y + half, "garden")
    L.box("Ladder Column", u + 3.6, u + 6.4, -1.8, 1.8, y, y + rise - 0.3, "marble")
    L.box("Ladder Top", u + 3.6, u + 6.4, -W, W, y + rise - 0.3, y + rise, "garden")
    rope("Chain", L.pt(u + 1.6, 0, y + half), half - 0.3, L.F, facing=L.yaw)
    rope("Chain", L.pt(u + 3.3, 0, y + rise), half - 0.3, L.F, facing=L.yaw)
    L.sign("사슬 사다리: 우클릭으로 잡고 W", u - 0.8, 0, y)
    return u + 6.4, y + rise


def m_pendulums(L, u, y, length, n=2):
    """A narrow bridge swept by swinging weights."""
    L.box("Pendulum Bridge", u, u + length, -1.3, 1.3, y - 1, y, "checker")
    for i in range(n):
        pu = u + length * (i + 0.5) / n
        for fu in (pu - 1.4, pu + 1.4):
            for pv in (-W - 0.4, W + 0.4):
                L.box("Gallows Post", fu - 0.25, fu + 0.25, pv - 0.25, pv + 0.25, y - 1, y + 7.5, "marble", kind="vbox")
            L.box("Gallows Beam", fu - 0.25, fu + 0.25, -W - 0.4, W + 0.4, y + 7.5, y + 8.0, "marble", kind="vbox")
        L.box("Gallows Ridge", pu - 1.4, pu + 1.4, -0.25, 0.25, y + 7.5, y + 8.0, "marble", kind="vbox")
        ax = L.vec(1, 0, 0)
        pv = group("Pendulum", L.pt(pu, 0, y + 7.5), L.F, m="pend", a=[ax[0], ax[1], ax[2], 55, 4.2, 1.9 * i])
        add("vbox", "Pendulum Rod", (0, -3.2, 0), (0.2, 6.4, 0.2), c="chain", g=pv)
        add("box", "Pendulum Weight", (0, -6.6, 0), (1.8, 1.8, 1.8), c="ruby", g=pv)
    L.sign("흔들리는 추: 지나간 뒤에", u - 1.0, 0, y)
    return u + length, y


def m_lift(L, u, y, rise, speed=2.5, pause=2.0):
    """A lift: board it at the bottom, step off at the top onto what follows."""
    mover("Lift", L.pt(u + 1.9, 0, y - 0.2), (3.4, 0.4, 3.4), (0, rise, 0), speed, pause, "gold", L.F)
    return u + 3.8, y + rise


def m_swing(L, u, y, gap=8.0):
    """A rope swing over a gap (like the ragdoll lab's 7i)."""
    L.box("Swing Beam", u + gap / 2 - 0.2, u + gap / 2 + 0.2, -2.5, 2.5, y + 6.9, y + 7.3, "marble", kind="vbox")
    rope("Swing", L.pt(u + gap / 2, 0, y + 6.9), 8.0, L.F, facing=L.yaw, swing=40, period=5.0, thickness=0.07, c="rope")
    L.sign("그네: 우클릭으로 잡고 건너편에서 놓기", u - 1.0, 0, y)
    return u + gap, y


def m_drawbridge(L, u, y, length=9.0):
    """A drawbridge leaf that rises and falls about its near end: cross while it is down."""
    g = group("Drawbridge", L.pt(u, 0, y - 0.25), L.F, r=(-25, L.yaw, 0), m="pend", a=[1, 0, 0, 25, 7.0, 0])
    add("box", "Drawbridge Leaf", (0, 0, length / 2), (4.0, 0.5, length), c="honey", g=g)
    L.sign("도개교: 내려왔을 때 건너기", u - 1.0, 0, y)
    return u + length, y


def m_spiral(L, u, y, n=9):
    """Slabs round a floating column, a metre a step, bobbing a little; the column's cap on top
    and a bridge on from it."""
    cu = u + 5.4
    cyl("Spiral Column", L.pt(cu, 0, y + (n - 0.6) / 2), 3.0, n - 0.6, "marble", L.F)
    cyl("Spiral Cap", L.pt(cu, 0, y + n - 0.3), 5.0, 0.6, "garden", L.F)
    L.box("Spiral Bridge", cu + 2.3, u + 10.8, -1.0, 1.0, y + n - 0.6, y + n - 0.03, "honey")
    for i in range(n - 1):
        ang = math.radians(180 + 36 * i)
        su, sv = cu + 4.2 * math.cos(ang), 4.2 * math.sin(ang)
        add("box", "Spiral Slab", L.F.p(*L.pt(su, sv, y + 1.0 * (i + 1) - 0.25)), (2.3, 0.5, 1.8),
            L.F.r(0, L.yaw - math.degrees(ang) + 90, 0), "gold", m="osc", a=[0, 0.4, 0, 5.0, 0.7 * i])
    L.sign("떠오르는 석판: 기둥을 돌며 한 칸씩", u - 1.0, 0, y)
    return u + 10.8, y + n


def m_bishop(L, u, y, rise):
    """The bishop's rail: a platform sliding diagonally, forward and up by the same amount."""
    mover("Bishop Rail", L.pt(u + 1.7, 0, y - 0.25), (3.2, 0.5, 3.2), L.vec(rise, 0, rise), 2.2, 2.5, "gold", L.F)
    slope("Rail", L.pt(u + 1.7, -1.8, y - 0.3), L.pt(u + 1.7 + rise, -1.8, y + rise - 0.3), 0.25, "gold", L.F,
          thick=0.25, kind="vbox")
    L.sign("비숍 레일: 대각선", u - 1.0, 0, y)
    return u + 3.4 + rise, y + rise


def m_sliding_walls(L, u, y, length, n=3):
    """A corridor with walls sliding in and out across it."""
    L.box("Wall Corridor", u, u + length, -W, W, y - 1, y, "honey")
    for i in range(n):
        L.osc("Sliding Wall", u + length * (i + 0.5) / n, -1.6, y + 1.5, 1.0, 3.0, 3.2, "ruby", 0, 3.2, 0, 3.6, 1.2 * i)
    L.sign("밀려오는 벽: 틈이 열릴 때", u - 1.0, 0, y)
    return u + length, y


def m_knight(L, u, y, n):
    """The knight's pads: each throws whoever steps on it 6 m up and 3 m on, onto the next ledge."""
    L.box("Knight Start", u, u + 2.2, -W, W, y - 1, y, "honey")
    for i in range(n):
        L.box("Knight Ledge", u + 4 * i + 3, u + 4 * i + 6.2, -2.2, 2.2, y + 6 * (i + 1) - 1, y + 6 * (i + 1), "checker")
        launch("Knight Pad", L.pt(u + 4 * i + 1.1, 0, y + 6 * i), L.yaw, (0, 6, 3), 1.2, L.F)
    L.box("Knight Wall", u + 2.2, u + 4 * (n - 1) + 6.2, W, W + 0.6, y, y + 6 * n, "marble")
    L.sign("나이트 도약대: 밟으면 위 6 m + 앞 3 m", u - 1.0, 0, y)
    return u + 4 * (n - 1) + 6.2, y + 6 * n


def m_gears(L, u, y, n, dy=1.0):
    """Turning gears to hop across, each a step higher."""
    for i in range(n):
        add("cyl", "Gear", L.F.p(*L.pt(u + 1.4 + 3.4 * i, (-0.8, 0.8)[i % 2], y + dy * (i + 1) - 0.3)), (3.2, 0.6, 3.2),
            L.F.r(), "gold", m="move", a=[0, 0, 0, 0, 0, 0.5, (25 if i % 2 else -25), 0, 1, 0, 0])
    L.sign("톱니: 돌아가는 원판을 한 칸씩", u - 1.0, 0, y)
    return u + 3.4 * n + 0.6, y + n * dy


def m_clock(L, u, y, h1, h2):
    """A clock face to climb, its hands turning across it; a rest ledge after h1."""
    uu, yy = m_wall(L, u, y, h1, h2, c="marble", name="Clock Wall")
    cy = y + (h1 - 0.3) / 2
    add("vcyl", "Clock Dial", L.F.p(*L.pt(u + 1.45, 0, cy)), (4.4, 0.1, 4.4), L.F.r(90, L.yaw, 0), "palegold")
    for spd, length, off in ((14, 1.8, 0.25), (-24, 2.1, 0.45)):
        h = group("Clock Hand", L.pt(u + 1.5 - off, 0, cy), L.F, r=(0, L.yaw, 0), m="spin", a=[0, 0, 1, spd, 0])
        add("box", "Hand", (0, length / 2, 0), (0.35, length, 0.2), c="ruby", g=h)
    return uu, yy


def m_chess_clock(L, u, y, rise):
    """The chess clock: two plates side by side, one up while the other is down."""
    half = rise / 2 + 0.5
    mover("Clock Plate", L.pt(u + 1.9, -1.6, y - 0.2), (3.0, 0.4, 3.0), (0, half, 0), 1.6, 1.5, "gold", L.F)
    mover("Clock Plate", L.pt(u + 1.9, 1.6, y + rise - half - 0.2), (3.0, 0.4, 3.0), (0, half, 0), 1.6, 1.5, "gold", L.F,
          phase=(half / 1.6 + max(0.5, 1.6 / 6) + 1.5))
    L.sign("체스 시계: 한쪽이 올라가면 다른 쪽", u - 1.0, 0, y)
    return u + 3.8, y + rise


def m_wheel(L, u, y, rise, n=6, period=24.0):
    """A wheel standing across the lane whose gondolas stay level: on at the bottom, off at the top."""
    R = rise / 2
    hub = L.pt(u + 2.0, 0, y + R)
    ax = L.vec(1, 0, 0)
    for i in range(n):
        add("box", "Wheel Gondola", L.F.p(*hub), (2.6, 0.4, 2.6), L.F.r(), "gold", m="orbit",
            a=[R, period, period * i / n, ax[0], ax[1], ax[2]])
    hoop("Wheel Rim", L.pt(u + 3.6, 0, y + R), R + 0.2, 0.4, "gold", L.F, facing=L.yaw)
    L.sign("관람차: 바닥에서 타고 꼭대기에서 내리기", u - 1.0, 0, y)
    return u + 4.0, y + rise


def m_spring(L, u, y, rise):
    """A wound spring: every 4 s it throws everyone on it up onto what follows."""
    L.box("Spring Floor", u, u + 3.6, -W, W, y - 1, y, "honey")
    launch("Spring", L.pt(u + 1.8, 0, y), L.yaw, (0, rise, 4.0), 1.5, L.F, period=4.0, size=(3.2, 0.6, 3.2))
    L.sign("태엽 스프링: 4초마다 %.0f m" % rise, u - 1.0, 0, y)
    return u + 4.6, y + rise


def m_islands(L, u, y, n, rise, period=6.0):
    """Floating garden islands, each bobbing a floor's worth: ride one up, step onto the next
    when it is at its bottom."""
    dy = rise / n
    amp = (dy + 0.4) / 2
    for i in range(n):
        mid = y + dy * (i + 0.5)             # the top swings between y + dy*i - 0.2 and y + dy*(i+1) + 0.2
        g = L.osc("Floating Island", u + 3.5 + 5.4 * i, (-0.8, 0.8)[i % 2], mid - 0.6, 4.0, 1.2, 4.0, "garden",
                  0, 0, amp, period, period / 2 * i + period * 0.75)
        add("vbox", "Island Rock", (0, -1.6, 0), (3.2, 2.0, 3.2), c="rock", g=g)
        add("vbox", "Cypress", (1.2, 2.1, 1.0), (0.8, 3.0, 0.8), c="cypress", g=g)
    L.sign("떠 있는 섬: 올라간 섬에서 내려온 다음 섬으로", u - 1.0, 0, y)
    return u + 1.5 + 5.4 * n, y + rise


def m_beams(L, u, y, length):
    """Balance beams on either side of a rest island: narrow and exposed."""
    third = length / 3
    L.box("Beam", u, u + third, -0.3, 0.3, y - 0.4, y, "gold")
    L.box("Beam Island", u + third, u + 2 * third, -1.8, 1.8, y - 1, y, "garden")
    L.box("Beam", u + 2 * third, u + length, 0.9, 1.5, y - 0.4, y, "gold")
    L.box("Beam", u + 2 * third, u + length, -1.5, -0.9, y - 0.4, y, "gold")
    L.sign("외나무다리", u - 1.0, 0, y)
    return u + length, y


def leg(L, y0, y1, u_end, modules):
    """Lay a leg's modules one after another from u 0 at height y0; a landing fills the rest
    up to u_end. The modules must rise exactly y1 - y0."""
    u, y = 0.0, y0
    for fn, kw in modules:
        u, y = fn(L, u, y, **kw)
    assert abs(y - y1) < 1e-6, "leg rises %.2f, wanted %.2f" % (y - y0, y1 - y0)
    assert u <= u_end + 1e-6, "leg too long: %.1f > %.1f" % (u, u_end)
    LEG_USE.append((u, u_end))
    if u < u_end - 0.01:
        landing(L, u, y, u_end - u)
    return y


LEG_USE = []


def wing(F, side, k, legs):
    """One wing of floor k in team frame F: side -1 west, +1 east. legs = (north, corner, south),
    each (rise, [modules]). Out along the north lane, down the corner lane, back along the south lane."""
    y0, y3 = H(k), H(k + 1)
    (r1, m1), (r2, m2), (r3, m3) = legs
    assert abs(r1 + r2 + r3 - FLOOR_HEIGHT) < 1e-6
    # From the plaza's north edge to the start of the north lane.
    span("Wing Link", min(side * 5, side * 12), max(side * 5, side * 12), y0 - 1, y0, PLAZA_Z1, LANE_N_Z + W, "honey", F)
    Ln = Lane(F, (side * 12.0, LANE_N_Z), "x", side)
    y1 = leg(Ln, y0, y0 + r1, LANE_N_LEN, m1)
    x_in, x_out = side * (LANE_C_X - W), side * (LANE_C_X + W)
    span("Corner", min(x_in, x_out), max(x_in, x_out), y1 - 1, y1, LANE_N_Z - W, LANE_N_Z + W, "garden", F)
    Lc = Lane(F, (side * LANE_C_X, LANE_N_Z - W), "z", -1)
    y2 = leg(Lc, y1, y1 + r2, LANE_C_LEN, m2)
    span("Corner", min(x_in, x_out), max(x_in, x_out), y2 - 1, y2, LANE_S_Z - W, LANE_S_Z + W, "garden", F)
    Ls = Lane(F, (side * (LANE_C_X - W), LANE_S_Z), "x", -side)
    y3b = leg(Ls, y2, y2 + r3, LANE_S_LEN, m3)
    assert abs(y3b - y3) < 1e-6
    # From the end of the south lane to the plaza of the next rank.
    span("Wing Arrival", min(side * 4, side * 14), max(side * 4, side * 14), y3 - 1, y3, LANE_S_Z - W, PLAZA_Z0, "honey", F)


# ------------------------------------------------------------------ sea, cliff, bridges

def sea_and_cliff():
    add("vbox", "Sea Surface", (0, -0.05, 0), (1800, 0.1, 1800), c="sea")
    add("trig", "Sea", (0, SEA_FLOOR / 2, 0), (1800, -SEA_FLOOR, 1800), f="water")
    add("box", "Cliff (rank 1 on top)", (0, (SEA_FLOOR + H(1)) / 2, 0), (2 * CLIFF, H(1) - SEA_FLOOR, 2 * CLIFF), c="cliff")
    add("trig", "Rank 1 Pad (the whole cliff top)", (0, H(1) + 1.5, 0), (2 * CLIFF, 3, 2 * CLIFF), f="rankpad", b=[1])
    for F in (SOUTH, NORTH):
        for x in (-32, 26):
            waterfall(x, H(1), 0.2, -CLIFF - 0.2, 5, F)
    for side in (-1, 1):
        for z in (-8, 12):
            add("vbox", "Waterfall", (side * (CLIFF + 0.2), H(1) / 2, z), (0.3, H(1) - 0.2, 6), c="waterfall")


def bridge(F):
    who = "White" if not F.turned else "Black"
    top = BRIDGE_TOP
    end = -CLIFF - BRIDGE_GAP
    span(who + " Bridge Deck", -6, 6, top - 2, top, end - 64, end - 5.5, "stone", F)
    span(who + " Broken End", -11, 11, top - 2, top, end - 5.5, end, "stone", F)
    span(who + " Stub", -11, -7, top - 2.2, top - 0.2, end, end + 1.5, "stone", F)
    span(who + " Stub", 6, 10, top - 1.8, top, end, end + 0.9, "stone", F)
    for x in (-5.7, 5.7):
        span(who + " Parapet", x - 0.3, x + 0.3, top, top + 0.9, end - 62, end - 12, "stone", F)
    for dz in (20, 40, 60):
        span(who + " Pier", -2.5, 2.5, SEA_FLOOR, top - 2, end - dz - 2, end - dz + 2, "stone", F)
    span(who + " Island", -18, 18, SEA_FLOOR, top, end - 96, end - 64, "rock", F)
    add("trig", who + " Hook Start Zone", F.p(0, top + 1.5, end - 4), (24, 4, 12), f="hookzone")
    for i in range(6):
        add("group", who + " Spawn", F.p(-7.5 + 3 * i, top, end - 3.5), r=F.r(), f="spawn",
            b=[(6 if F.turned else 0) + i, 1 if F.turned else 0])
    # Until a player has stood on rank 1, a fall puts it back here (the hook must not be skipped).
    add("group", who + " Bridge Respawn", F.p(0, top, end - 4.5), r=F.r(), f="bridgespawn", b=[1 if F.turned else 0])
    sign("갈고리: E, 좌클릭 꾹 = 게이지, 떼면 던짐 — 절벽 가장자리(1랭크)까지 10.5 m", (0, top + 0.02, end - 8), F, size=0.36)


# ------------------------------------------------------------------ the core and the rank terraces

def core_floor(n):
    C = CORE[n]
    y0, y1 = H(n), H(n + 1) - SLAB
    if n == 7:
        cyl("F7 Crown Drum", (0, (y0 + y1) / 2, 0), 2 * C, y1 - y0, "marble")
    else:
        span("F%d Core" % n, -C, C, y0, y1, -C, C, "marble")
    for F in (SOUTH, NORTH):
        if n < 7:
            for x in (-C * 0.55, C * 0.55):
                span("Arch Niche", x - 2.2, x + 2.2, y0 + 2, y0 + 20, -C - 0.25, -C, "niche", F, kind="vbox")
        sign(str(n), (0, y0 + 14, -C - 0.3), F, yaw=0, size=8.0, standing=True)
    for x in (-C - 0.15, C + 0.15):
        add("vbox", "Light Window", (x, (y0 + y1) / 2, 0), (0.3, y1 - y0 - 3, 3.0), c="light")


def terrace(rank):
    """The square floor round the core on rank 2..7, shared by both teams, with pillars carrying
    the next one (not round floor 4, where the orbit rings turn)."""
    R = terrace_half(rank)
    y = H(rank)
    span("R%d Terrace" % rank, -R, R, y - SLAB, y, -R, R, "honey")
    for F in (SOUTH, NORTH):
        sign("%d랭크" % rank, (0, y + 0.02, -(R - 2.5)), F, size=1.4)
    if rank < 7 and rank != 4:
        P = terrace_half(rank + 1) - 0.8 if rank + 1 < 7 else R - 0.8
        for sx in (-1, 1):
            for sz in (-1, 1):
                span("Terrace Pillar", sx * P - 0.7, sx * P + 0.7, y, H(rank + 1) - SLAB, sz * P - 0.7, sz * P + 0.7, "marble")


# ------------------------------------------------------------------ the team's hub: plazas, tube, shortcuts

def hub(F):
    team = 1 if F.turned else 0
    who = "Black" if team else "White"
    # The vacuum tube: a glass column from rank 1 to rank 7, its mouth on rank 1.
    tx, tz = (TUBE_X0 + TUBE_X1) / 2, (TUBE_Z0 + TUBE_Z1) / 2
    add("vbox", who + " Vacuum Tube", F.p(tx, (H(1) + H(7) + 4) / 2, tz), (4.0, H(7) + 4 - H(1), 4.0), F.r(), "glass")
    for r in range(1, 8):
        add("vbox", "Tube Band", F.p(tx, H(r) + 3.2, tz), (4.4, 0.4, 4.4), F.r(), "gold")
    add("trig", who + " Vacuum Tube Mouth", F.p(tx, H(1) + 1.5, tz), (3.6, 3, 3.6), f="tube", b=[team, 32])
    add("group", who + " Respawn (rank 1, by the tube)", F.p(-5.5, H(1), -44.5), r=F.r(0, -90, 0), f="respawn", b=[team])
    sign("진공관: 들어가면 내가 밟은 가장 높은 랭크로", (-6, H(1) + 0.03, -40), F, yaw=-90, size=0.3)
    for r in range(1, RANKS + 1):
        y = H(r)
        if r > 1:
            span("R%d Plaza" % r, -PLAZA_X, PLAZA_X, y - SLAB, y, PLAZA_Z0, PLAZA_Z1, "honey", F)
            near = PLAZA_Z1
            if r <= 7:
                near = -terrace_half(r)
                span("R%d Plaza Bridge" % r, -3, 3, y - 1, y, PLAZA_Z1, near, "honey", F)
            add("group", who + " Tube Station R%d" % r, F.p(-7.0, y, -40.0), r=F.r(0, 90, 0), f="station", b=[team, r])
            # Whoever walks onto the plaza or its bridge has reached this rank.
            add("trig", who + " Rank %d Pad" % r, F.p(0, y + 1.5, (PLAZA_Z0 + near) / 2),
                (2 * PLAZA_X, 3, near - PLAZA_Z0), f="rankpad", b=[r])
        add("vbox", "Rank Pad", F.p(0, y + 0.03, -41), (6, 0.06, 6), F.r(), "gold")
        sign("%d랭크" % r, (0, y + 0.07, -41), F, size=1.2)
        if r < RANKS:
            # The team's shortcut up floor r: a lift beside the plaza, hidden until the bell on the next plaza rings.
            z0, z1 = lift_lane(r)
            g = group("%s Shortcut F%d (opens with the team's bell)" % (who, r), (0, 0, 0), WORLD, f="teampath", b=[r, team])
            mover("Shortcut Lift F%d" % r, ((LIFT_X0 + LIFT_X1) / 2, y + 0.15, (z0 + z1) / 2),
                  (LIFT_X1 - LIFT_X0, 0.3, z1 - z0), (0, FLOOR_HEIGHT - 0.3, 0), 4.0, 1.5, c="light", F=F, g=g)
            add("vbox", "Shortcut Light", F.p(LIFT_X1 + 0.25, y + FLOOR_HEIGHT / 2, (z0 + z1) / 2),
                (0.2, FLOOR_HEIGHT, 0.2), F.r(), "light", g=g)
            sign("%d층 지름길 (종을 치면 우리 팀만)" % r, (7, y + 0.03, (z0 + z1) / 2), F, yaw=90, size=0.22)
        if r > 1:
            # The team's bell for the floor below: the first of the team to ring it opens that floor's shortcut.
            add("group", "%s Bell F%d" % (who, r - 1), F.p(4.5, y, -34.5), r=F.r(0, 180, 0), f="teambell", b=[r - 1, team])
            sign("%d층 종 — F" % (r - 1), (4.5, y + 0.03, -36.5), F, size=0.28)


# ------------------------------------------------------------------ floors 1-3, 5-6 (a team's two wings)

def floor1(F):
    """The waterfall terraces and the grand stair."""
    wing(F, -1, 1, (
        (9, [(landing, {"length": 3}), (m_flight, {"length": 18, "rise": 5, "stone": True}), (landing, {"length": 4}),
             (m_flight, {"length": 14, "rise": 4, "gap": 2.5})]),
        (9, [(landing, {"length": 2}), (m_arcade, {"h1": 4.5, "h2": 4.5}), (landing, {"length": 2}), (m_beams, {"length": 9})]),
        (8, [(landing, {"length": 2}), (m_pawn_march, {"length": 15}), (m_flight, {"length": 18, "rise": 8})]),
    ))
    wing(F, 1, 1, (
        (9, [(landing, {"length": 1}), (m_piston_ramp, {"length": 14, "rise": 5}), (landing, {"length": 1}),
             (m_checkerboard, {"length": 10}), (m_steps, {"n": 4})]),
        (9, [(landing, {"length": 2}), (m_wall, {"h1": 4.5, "h2": 4.5}), (landing, {"length": 2}), (m_statues, {"length": 14})]),
        (8, [(landing, {"length": 2}), (m_sliding_walls, {"length": 9}), (m_steps, {"n": 8, "du": 2.0})]),
    ))


def floor2(F):
    """The chain gondolas."""
    wing(F, -1, 2, (
        (9, [(landing, {"length": 2}), (m_gondolas, {"rise": 9}), (landing, {"length": 3}), (m_pendulums, {"length": 16})]),
        (9, [(landing, {"length": 1}), (m_chain_ladder, {"rise": 9}), (landing, {"length": 2}), (m_statues, {"length": 14})]),
        (8, [(landing, {"length": 2}), (m_drawbridge, {"length": 9}), (landing, {"length": 3}), (m_flight, {"length": 18, "rise": 8})]),
    ))
    wing(F, 1, 2, (
        (9, [(landing, {"length": 2}), (m_statues, {"length": 14}), (landing, {"length": 2}), (m_pendulums, {"length": 12}),
             (landing, {"length": 2}), (m_lift, {"rise": 9})]),
        (9, [(landing, {"length": 2}), (m_gondolas, {"rise": 9}), (landing, {"length": 2}), (m_beams, {"length": 9})]),
        (8, [(landing, {"length": 3}), (m_swing, {"gap": 8}), (landing, {"length": 3}), (m_piston_ramp, {"length": 18, "rise": 8})]),
    ))


def floor3(F):
    """The rising slabs, the knight's pads and the bishop's rails."""
    wing(F, -1, 3, (
        (9, [(landing, {"length": 2}), (m_spiral, {"n": 9}), (landing, {"length": 3}), (m_sliding_walls, {"length": 15})]),
        (9, [(landing, {"length": 2}), (m_bishop, {"rise": 9}), (landing, {"length": 2}), (m_checkerboard, {"length": 8})]),
        (8, [(landing, {"length": 2}), (m_drawbridge, {"length": 9}), (landing, {"length": 3}), (m_piston_ramp, {"length": 18, "rise": 8})]),
    ))
    wing(F, 1, 3, (
        (9, [(landing, {"length": 2}), (m_knight, {"n": 1}), (landing, {"length": 3}), (m_steps, {"n": 3}), (landing, {"length": 2}),
             (m_checkerboard, {"length": 10})]),
        (9, [(landing, {"length": 2}), (m_knight, {"n": 1}), (landing, {"length": 2}), (m_steps, {"n": 3}), (landing, {"length": 1}),
             (m_beams, {"length": 6})]),
        (8, [(landing, {"length": 2}), (m_sliding_walls, {"length": 12}), (m_flight, {"length": 18, "rise": 8})]),
    ))


def floor5(F):
    """The clockwork."""
    wing(F, -1, 5, (
        (9, [(landing, {"length": 2}), (m_gears, {"n": 9}), (landing, {"length": 2})]),
        (9, [(landing, {"length": 2}), (m_clock, {"h1": 4.5, "h2": 4.5}), (landing, {"length": 2}), (m_pendulums, {"length": 12})]),
        (8, [(landing, {"length": 2}), (m_pawn_march, {"length": 14}), (m_piston_ramp, {"length": 18, "rise": 8})]),
    ))
    wing(F, 1, 5, (
        (9, [(landing, {"length": 2}), (m_chess_clock, {"rise": 9}), (landing, {"length": 3}), (m_checkerboard, {"length": 14}),
             (landing, {"length": 2}), (m_sliding_walls, {"length": 12})]),
        (9, [(landing, {"length": 2}), (m_wheel, {"rise": 9}), (landing, {"length": 3}), (m_statues, {"length": 14})]),
        (8, [(landing, {"length": 2}), (m_spring, {"rise": 8}), (landing, {"length": 3}), (m_beams, {"length": 14}),
             (landing, {"length": 2}), (m_pawn_march, {"length": 10})]),
    ))


def floor6(F):
    """The floating gardens."""
    wing(F, -1, 6, (
        (9, [(landing, {"length": 2}), (m_islands, {"n": 3, "rise": 9}), (landing, {"length": 3}), (m_beams, {"length": 12})]),
        (9, [(landing, {"length": 2}), (m_chain_ladder, {"rise": 9}), (landing, {"length": 2}), (m_swing, {"gap": 8}),
             (landing, {"length": 2}), (m_beams, {"length": 6})]),
        (8, [(landing, {"length": 2}), (m_drawbridge, {"length": 9}), (landing, {"length": 3}), (m_pendulums, {"length": 12}),
             (landing, {"length": 2}), (m_lift, {"rise": 8})]),
    ))
    wing(F, 1, 6, (
        (9, [(landing, {"length": 2}), (m_beams, {"length": 12}), (landing, {"length": 2}), (m_wall, {"h1": 4.5, "h2": 4.5, "ivy": True}),
             (landing, {"length": 2}), (m_statues, {"length": 12})]),
        (9, [(landing, {"length": 2}), (m_islands, {"n": 3, "rise": 9}), (landing, {"length": 2})]),
        (8, [(landing, {"length": 2}), (m_pawn_march, {"length": 12}), (m_flight, {"length": 18, "rise": 8})]),
    ))


# ------------------------------------------------------------------ floor 4: the orbit rings and the rook towers (shared)

RING_A, RING_B = 22.0, 28.0      # the rings' radii (their tracks are 3 m wide)


def floor4():
    """The centre of the board. Two chequered rings turn round the tower, tilted about the
    north-south axis: the inner ring A runs low past the east and high past the west, the outer
    ring B the other way round. White boards A from its dock on the east side of rank 4, rides
    (or runs) half way round and steps off onto the west tower's ledge, then climbs to rank 5;
    black does the same on B, from the west to the east tower. Either team may take either
    ring. The rook towers east and west of the terrace are the steady way: a walk out and four
    walls with rests."""
    y, top = H(4), H(5)
    lo, hi = y + 3.5, y + 13.5
    orbit_ring("F4 Orbit Ring A (low east, high west)", RING_A, lo, hi, True, 5.0)
    orbit_ring("F4 Orbit Ring B (low west, high east)", RING_B, lo, hi, False, -5.0)

    # White's dock for ring A: three steps up from the terrace, inside the ring where it runs low.
    for x0, x1, t in ((14.5, 16.0, y + 1.0), (16.0, 17.5, y + 2.3), (17.5, 19.5, y + 3.7)):
        span("F4 Ring A Dock", x0, x1, y - 1, t, -2.5, 2.5, "gold")
    sign("궤도 고리 A 부두: 고리가 낮게 지나갈 때 올라타기", (15, y + 0.03, -4), yaw=90, size=0.28)

    # Black's dock for ring B: a walk out under ring A (high here) and steps up between the rings.
    span("F4 Ring B Walk", -21.0, -18.5, y - 1, y, 4.0, 8.0, "honey")
    for x0, x1, t in ((-22.1, -21.0, y + 1.2), (-23.2, -22.1, y + 2.5), (-24.5, -23.2, y + 3.8)):
        span("F4 Ring B Dock", x0, x1, y - 1, t, 4.0, 8.0, "gold")
    sign("궤도 고리 B 부두", (-19.5, y + 0.03, 9.5), yaw=-90, size=0.28)

    # The west tower, where ring A runs high: a ledge level with the ring, then three walls with
    # rests, and a bridge from the top to rank 5 beside them (not over the climb).
    A_hi = y + 13.8
    span("F4 West Tower Ledge", -24.2, -23.2, A_hi - 0.6, A_hi, -2.5, 2.0, "checker")
    for x_face, y0, y1 in ((-24.2, y + 6.0, y + 17.9), (-24.9, y + 17.9, y + 22.0), (-25.6, y + 22.0, top)):
        span("F4 West Tower", -27.5, x_face, y0, y1 - 0.3, -2.5, 2.0, "marble")
        span("F4 West Tower Rest", -27.5, x_face, y1 - 0.3, y1, -2.5, 2.0, "garden")
    span("F4 West Tower Bridge", -27.5, -terrace_half(5), top - 1, top, 2.0, 4.5, "honey")

    # The east tower, where ring B runs high (the same the other way round).
    B_hi = y + 13.2
    span("F4 East Tower Ledge", 24.66, 25.66, B_hi - 0.6, B_hi, -2.0, 2.5, "checker")
    for x_face, y0, y1 in ((24.66, y + 6.0, y + 17.4), (23.96, y + 17.4, y + 21.7), (23.26, y + 21.7, top)):
        span("F4 East Tower", x_face, 21.0, y0, y1 - 0.3, -2.0, 2.5, "marble")
        span("F4 East Tower Rest", x_face, 21.0, y1 - 0.3, y1, -2.0, 2.5, "garden")
    span("F4 East Tower Bridge", terrace_half(5), 23.26, top - 1, top, -4.5, -2.0, "honey")
    sign("4층 궤도 고리: 백은 동쪽 부두 → 안쪽 고리 A → 서쪽 탑, 흑은 서쪽 부두 → 바깥 고리 B → 동쪽 탑. 반 바퀴에 10 m",
         (0, y + 0.03, -terrace_half(4) + 1.3), size=0.26)

    # The rook towers: white's two at z -10, black's two at z +10. A bridge out at rank 4, the
    # outer face climbed in four walls of 6.5 m with a rest on each, a bridge back at rank 5.
    for sx in (-1, 1):
        for sz in (-1, 1):
            def X(a, b):
                return (min(sx * a, sx * b), max(sx * a, sx * b))

            def Z(a, b):
                return (min(sz * a, sz * b), max(sz * a, sz * b))

            span("F4 Rook Base", *X(32.0, 40.0), y - 1, y, *Z(5.5, 14.5), "honey")
            span("F4 Rook Bridge", *X(terrace_half(4), 32.0), y - 1, y, *Z(8.5, 11.5), "honey")
            for k in range(4):
                y0, y1 = y + 6.5 * k, y + 6.5 * (k + 1)
                face = 12.5 - 0.7 * k
                span("F4 Rook Tower", *X(33.5, 38.5), y0, y1 - 0.3, *Z(7.5, face), "marble")
                span("F4 Rook Rest", *X(33.5, 38.5), y1 - 0.3, y1, *Z(7.5, face), "garden")
            for m in (8.0, 9.2, 10.4):
                span("Merlon", *X(37.8, 38.5), top, top + 0.8, *Z(m - 0.3, m + 0.3), "marble")
            span("F4 Rook Top Bridge", *X(terrace_half(5), 33.5), top - 1, top, *Z(7.8, 10.2), "honey")
    for F in (SOUTH, NORTH):
        for sx in (-1, 1):
            sign("룩의 탑: 바깥 벽 6.5 m씩 네 번", (sx * 36, y + 0.03, -16), F, size=0.28)


# ------------------------------------------------------------------ floor 7: the double spiral stair, the light stair (shared)

def floor7():
    """The grand balcony and the final approach. Two spiral stairs wind a turn and a quarter round
    the crown drum, white's from the south, black's from the north, with pistons shoving out of
    the drum across them. The fast way is each team's stair of light: floating steps a metre
    apart that come and go in a wave, straight up to the summit."""
    y, top = H(7), H(8)
    R, turn, segs = 12.5, 450.0, 44
    for k, start in enumerate((-90.0, 90.0)):
        tag = "A (white, from the south)" if k == 0 else "B (black, from the north)"
        for i in range(segs):
            u0, u1 = i / segs, (i + 1) / segs
            um = (u0 + u1) / 2
            phi = math.radians(start + turn * um)
            h = y + (top - y) * um
            ds = R * math.radians(turn) / segs
            pitch = -math.degrees(math.atan2((top - y) / segs, ds))
            add("box", "F7 Spiral Stair %s" % tag, (R * math.cos(phi), h - 0.3, R * math.sin(phi)), (3.2, 0.6, ds + 0.1),
                (pitch, -math.degrees(phi), 0), "honey" if i % 2 == 0 else "palegold")
        for j, um in enumerate((0.14, 0.34, 0.54, 0.74)):
            phi = math.radians(start + turn * um)
            h = y + (top - y) * um
            d = (math.cos(phi), math.sin(phi))
            p = add("box", "F7 Drum Piston", (10.5 * d[0], h + 0.8, 10.5 * d[1]), (1.2, 1.0, 1.2), c="ruby",
                    m="osc", a=[1.5 * d[0], 0, 1.5 * d[1], 3.4, 0.85 * j + 1.7 * k])
            add("vbox", "Piston Rod", (-2.1 * d[0], 0, -2.1 * d[1]), (3.0, 0.3, 0.3), (0, -math.degrees(phi), 0), "marble", g=p)

    # The stairs of light: white's up the south-west diagonal, black's up the north-east.
    az = math.radians(225.0)
    out = (math.cos(az), math.sin(az))
    across = (-out[1], out[0])
    yaw = 90.0 - 225.0
    for F in (SOUTH, NORTH):
        for i in range(25):
            r = 24.0 - 0.3 * i
            s = -1.2 if i % 2 == 0 else 1.2
            add("box", "F7 Light Step", F.p(r * out[0] + s * across[0], y + 1.0 * (i + 1) - 0.15, r * out[1] + s * across[1]),
                (1.6, 0.3, 0.9), F.r(0, yaw, 0), "light", m="toggle", a=[4.0, 0.0, 0.7, 0.22 * i])
        r0, r1 = 16.3, 10.6
        rm = (r0 + r1) / 2
        add("box", "F7 Light Bridge", F.p(rm * out[0], top - 0.5, rm * out[1]), (2.4, 1.0, r0 - r1), F.r(0, yaw, 0), "light")
        sign("빛의 계단: 켜졌다 꺼지는 발판 25칸", (25.5 * out[0], y + 0.03, 25.5 * out[1]), F, yaw=yaw, size=0.32)
        # From the plaza of rank 8 to the summit.
        span("R8 Plaza Bridge", -2, 2, top - 1, top, PLAZA_Z1, -(SUMMIT - 0.02), "honey", F)
        sign("7층 대발코니: 나선 계단 (드럼의 피스톤 조심) 또는 빛의 계단", (0, y + 0.03, -17.5), F, size=0.3)
    # Gardens in two corners of the balcony (only look; the balcony is the floor).
    for sx, sz in ((-1, 1), (1, -1)):
        add("vbox", "Balcony Garden", (sx * 22, y + 0.05, sz * 22), (9, 0.1, 9), c="garden")
        for dx, dz in ((2.5, 2.5), (-2.5, 2.5), (2.5, -2.5)):
            cypress(sx * 22 + dx, y, sz * 22 + dz)


# ------------------------------------------------------------------ rank 8: the summit

SUMMIT = 10.6


def summit():
    y = H(8)
    cyl("R8 Summit", (0, y - SLAB / 2, 0), 2 * SUMMIT, SLAB, "honey")
    add("vcyl", "R8 Summit Inlay", (0, y + 0.02, 0), (16.4, 0.04, 16.4), c="palegold")
    add("trig", "Rank 8 Pad (the summit)", (0, y + 1.5, 0), (2 * SUMMIT, 3, 2 * SUMMIT), f="rankpad", b=[8])
    # The eighth rank: eight squares round the gate, the pieces of the back rank.
    files = "abcdefgh"
    kinds = ["rook", "knight", "bishop", "queen", "king", "bishop", "knight", "rook"]
    kind_ids = {"rook": 1, "bishop": 2, "knight": 3, "king": 4}
    names = {"rook": "룩", "knight": "나이트", "bishop": "비숍", "queen": "퀸 (보석판, 아직 없음)", "king": "킹"}
    for i in range(8):
        phi = math.radians(22.5 + 45 * i + 180)
        cx, cz = 6.4 * math.cos(phi), 6.4 * math.sin(phi)
        yaw = -math.degrees(phi) - 90
        add("vbox", "Rank 8 Square %s8" % files[i], (cx, y + 0.07, cz), (3.0, 0.06, 3.0), (0, yaw, 0),
            "lapis" if i % 2 == 0 else "palegold")
        k = kinds[i]
        if k == "queen":
            add("vbox", "Queen Jewel Plate", (cx, y + 0.12, cz), (1.8, 0.04, 1.8), (0, yaw, 0), "light")
        else:
            add("box", "Pedestal %s8" % files[i], (cx, y + 0.5, cz), (0.6, 0.8, 0.6), (0, yaw, 0), "gold")
            add("group", "Promote %s8" % files[i], (cx, y + 0.1, cz), f="promote", b=[kind_ids[k]])
        add("sign", "Sign", (cx * 1.4, y + 0.05, cz * 1.4), r=(90, yaw, 0), f="sign", b=[0.35],
            t="%s8 %s" % (files[i], names[k]))
    # The gate and the crown (only look).
    for x in (-3.2, 3.2):
        add("box", "Gate Pillar", (x, y + 5, 0), (0.9, 10, 0.9), c="marble")
    hoop("Gate Ring", (0, y + 11.2, 0), 4.2, 0.5, "gold", facing=90)
    hoop("Crown Halo", (0, y + 7.5, 0), 2.2, 0.25, "light", upright=False)
    add("vbox", "Crown", (0, y + 7.5, 0), (1.6, 1.6, 1.6), (45, 45, 0), "gold")
    sign("8랭크 — 칸 위 받침대 앞에서 F = 승격", (0, y + 0.1, -4.2), size=0.4)


# ------------------------------------------------------------------ scenery

def scenery():
    rocks = [(-170, 12, 90, 30), (190, 20, -60, 40), (110, 8, 200, 26), (-140, 5, -190, 22),
             (-105, 80, 70, 10), (100, 130, -70, 12), (95, 50, 95, 9), (-100, 160, -60, 8), (80, 185, 90, 7),
             (-90, 110, -110, 9)]
    for x, y, z, s in rocks:
        add("vbox", "Floating Rock", (x, y, z), (s, s * 0.8, s * 1.1), (0, x, 0), "rock")
    for i in range(12):
        a = i * 30.0
        rr = 95 + (i % 3) * 30
        add("vbox", "Cloud", (rr * math.cos(math.radians(a)), 60 + (i % 5) * 30, rr * math.sin(math.radians(a))),
            (26, 3, 14), (0, a, 0), "cloud")


# ------------------------------------------------------------------ build

def build():
    pieces.clear()
    _names.clear()
    LEG_USE.clear()
    sea_and_cliff()
    for F in (SOUTH, NORTH):
        bridge(F)
    for n in range(1, FLOORS + 1):
        core_floor(n)
    for rank in range(2, 8):
        terrace(rank)
    for F in (SOUTH, NORTH):
        hub(F)
        floor1(F)
        floor2(F)
        floor3(F)
        floor5(F)
        floor6(F)
    floor4()
    floor7()
    summit()
    scenery()
    return {
        "version": 8,
        "ranks": [H(r) for r in range(1, RANKS + 1)],
        "floors": FLOORS,
        "names": [FLOOR_NAMES[n] for n in range(1, FLOORS + 1)],
        "pieces": pieces,
    }


def dump(data):
    return json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n"


def main():
    data = build()
    text = dump(data)
    if "--check" in sys.argv:
        old = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
        if old != text:
            sys.exit("FAIL: %s is stale. Run python3 Tools/QueenHill/build_layout.py" % OUT)
        print("PASS: Queen of the Hill layout is up to date (%d pieces)." % len(data["pieces"]))
        return
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8").write(text)
    print("wrote %s: %d pieces, ranks %s" % (OUT, len(data["pieces"]), data["ranks"]))


if __name__ == "__main__":
    main()
