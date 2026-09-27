#!/usr/bin/env python3
# Queen of the Hill graybox map, "sky palace", 7th revision (R49): seven floors,
# one per rank of the chessboard (ranks 1-7), and the summit, rank 8, where a pawn
# promotes. Every floor is a small course of its own with two or three ways up.
#
# This script is the ONLY source of the map. It writes
#   Assets/Resources/QueenHill/QueenHillLayout.json
# which QueenHillLevel (Assets/Scripts/Gameplay/QueenHill) turns into the scene
# when Play starts, and which Tools/QueenHill/preview renders without Unity.
# Never edit the JSON by hand; change this file and run it again:
#   python3 Tools/QueenHill/build_layout.py            (writes the JSON)
#   python3 Tools/QueenHill/build_layout.py --check    (fails if the JSON is stale)
#
# Coordinates are Unity's: x east, y up, z north, metres. The white team comes
# from the south (-z), the black team from the north (+z). Floors 1-3 and 5-6 are
# built once for the south half and turned 180 degrees for the north half, so both
# teams get the same course. Floors 4 and 7 are shared (the centre of the board,
# and the final approach).
#
# What the pawn can do (Docs/RagdollLab/README.md, RagdollTuning): runs 5.5 m/s,
# sprints 9.6 m/s, jumps about 1 m high and 4-5 m far running, climbs any face
# steeper than 55 degrees at 1.2 m/s for about 8 m on one bar of stamina, grabs a
# ledge within 0.4 m of its top, climbs chains, swings on ropes over 8 m, rides
# anything that moves (IMovingSurface). The numbers below keep to that: jumps of
# up to 2.5 m across and 1 m up, climbs of 3-7 m between rests.
import json, math, os, sys

OUT = "Assets/Resources/QueenHill/QueenHillLayout.json"

# ------------------------------------------------------------------ the frame

FIRST_RANK, FLOOR_HEIGHT, RANKS = 16.0, 20.0, 8
FLOORS = RANKS - 1
BRIDGE_TOP, SEA_FLOOR, CLIFF = 12.0, -8.0, 30.0


def H(rank):
    """Height of the top of rank `rank` (1..8): 16, 36, ... 156."""
    return FIRST_RANK + FLOOR_HEIGHT * (rank - 1)


# Half-width of the tower core on each floor (a square drum that narrows as it
# rises; floor 7 is the round-ish crown drum inside the double spiral stair).
CORE = {1: 12.0, 2: 11.6, 3: 11.2, 4: 10.8, 5: 10.4, 6: 10.0, 7: 7.6}
RING = 5.0            # the terrace round the core on every rank
SLAB = 1.5            # rank floors are this thick
SHAFT_X, SHAFT_Z, STRIP = 5.0, 4.0, 1.2   # the light shaft inside the core, and its middle walkway
DOOR_W, DOOR_H = 3.0, 3.5


def slab_half(rank):
    """Half-width of the square floor of rank 2..7 (the core above it plus the terrace)."""
    if rank == 7:
        return 24.0   # the grand balcony
    return CORE[rank] + RING


def cell_x(floor):
    """The light column's lift for floor n rises in the west half (odd floors) or the east half (even)."""
    return -(STRIP + SHAFT_X) / 2 if floor % 2 == 1 else (STRIP + SHAFT_X) / 2


def cell_half_range(floor):
    return (-SHAFT_X, -STRIP) if floor % 2 == 1 else (STRIP, SHAFT_X)


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
    """A team half: identity (south, white) or turned 180 degrees about the tower axis (north, black)."""

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


def span(name, x0, x1, y0, y1, z0, z1, c, F=WORLD, kind="box", g=""):
    """An axis-aligned box from its extents, in a frame."""
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
    Upright rings stand in the frame's x-y plane turned by `facing`; flat ones lie in x-z."""
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


def pier(name, x0, x1, z0, z1, y0, y1, F=WORLD, trees=True):
    """A stout marble pier with a garden on top: a landing to rest on."""
    span(name, x0, x1, y0, y1 - 0.3, z0, z1, "marble", F)
    span(name + " Garden", x0, x1, y1 - 0.3, y1, z0, z1, "garden", F)
    if trees:
        cypress(x0 + 0.8, y1, z0 + 0.8, F)


def flight(name, a, b, width, F=WORLD, rail_side=-1):
    """A flight of the grand stair: a thick ramp with a balustrade on one side (rail_side -1/+1 across it)."""
    slope(name, a, b, width, "honey", F, thick=1.2)
    ax, ay, az = a
    bx, by, bz = b
    dx, dz = bx - ax, bz - az
    L = math.hypot(dx, dz)
    nx, nz = -dz / L * rail_side, dx / L * rail_side      # sideways, toward the rail
    off = width / 2 - 0.15
    slope(name + " Balustrade", (ax + nx * off, ay + 0.9, az + nz * off), (bx + nx * off, by + 0.9, bz + nz * off),
          0.3, "marble", F, thick=0.9)


def waterfall(x, y_top, y_bottom, z, width, F=WORLD, along_x=True):
    size = (width, y_top - y_bottom, 0.3) if along_x else (0.3, y_top - y_bottom, width)
    add("vbox", "Waterfall", F.p(x, (y_top + y_bottom) / 2, z), size, F.r(), "waterfall")


def mover(name, p, size, travel, speed, pause, c="gold", F=WORLD, spin=0.0, axis=(0, 1, 0), phase=0.0, kind="box", g=""):
    ramp = max(0.5, speed / 6.0)
    tx, ty, tz = travel
    if F.turned:
        tx, tz = -tx, -tz
    return add(kind, name, F.p(*p), size, F.r(), c, g=g, m="move",
               a=[tx, ty, tz, speed, pause, ramp, spin, axis[0], axis[1], axis[2], phase])


def bobber(name, p, size, amplitude, period, phase, c="lapis", F=WORLD, g=""):
    return add("box", name, F.p(*p), size, F.r(), c, g=g, m="osc", a=[0, amplitude, 0, period, phase])


def rope(name, top, length, F=WORLD, facing=0.0, swing=0.0, period=5.0, phase=0.0, thickness=0.1, c="chain"):
    """A chain or rope hanging from `top` (a frame point); a swing moves toward the frame's `facing`."""
    return add("group", name, F.p(*top), r=F.r(0, facing, 0), f="rope", b=[length, swing, period, phase, thickness], t=c)


def launch(name, surface, facing, throw, clearance, F=WORLD, period=0.0, phase=0.0, same=False, size=(1.8, 0.6, 1.8)):
    add("vbox", name + " Plate", F.p(surface[0], surface[1] + 0.02, surface[2]), (size[0], 0.04, size[2]), F.r(0, facing, 0), "gold")
    return add("trig", name, F.p(*surface), size, F.r(0, facing, 0), f="launch",
               b=[throw[0], throw[1], throw[2], clearance, period, phase, 1 if same else 0])


# ------------------------------------------------------------------ sea, cliff, bridges

def sea_and_cliff():
    add("vbox", "Sea Surface", (0, -0.05, 0), (1600, 0.1, 1600), c="sea")
    add("trig", "Sea", (0, SEA_FLOOR / 2, 0), (1600, -SEA_FLOOR, 1600), f="water")
    span("Cliff Base (rank 1 on top)", -CLIFF, CLIFF, SEA_FLOOR, H(1), -CLIFF, CLIFF, "cliff")
    for F in (SOUTH, NORTH):
        for x in (-22, 8):
            waterfall(x, H(1), 0.2, -CLIFF - 0.2, 4, F)
    for side in (-1, 1):
        for z in (-12, 10):
            add("vbox", "Waterfall", (side * (CLIFF + 0.2), H(1) / 2, z), (0.3, H(1) - 0.2, 5), c="waterfall")


def bridge(F):
    who = "White" if not F.turned else "Black"
    top = BRIDGE_TOP
    span(who + " Bridge Deck", -6, 6, top - 2, top, -110, -46, "stone", F)
    span(who + " Broken End", -11, 11, top - 2, top, -46, -40.5, "stone", F)
    span(who + " Stub", -11, -7, top - 2.2, top - 0.2, -40.5, -39, "stone", F)
    span(who + " Stub", 6, 10, top - 1.8, top, -40.5, -39.6, "stone", F)
    for x in (-5.7, 5.7):
        span(who + " Parapet", x - 0.3, x + 0.3, top, top + 0.9, -108, -52, "stone", F)
    for z in (-60, -80, -100):
        span(who + " Pier", -2.5, 2.5, SEA_FLOOR, top - 2, z - 2, z + 2, "stone", F)
    span(who + " Island", -18, 18, SEA_FLOOR, top, -140, -108, "rock", F)
    add("trig", who + " Hook Start Zone", F.p(0, top + 1.5, -44), (24, 4, 12), f="hookzone")
    for i in range(6):
        add("group", who + " Spawn", F.p(-7.5 + 3 * i, top, -43.5), r=F.r(), f="spawn",
            b=[(6 if F.turned else 0) + i, 1 if F.turned else 0])
    sign("갈고리: E, 좌클릭 꾹 = 게이지, 떼면 던짐 — 짧게 = 절벽 가장자리(1랭크), 가득 = 오른쪽 아치 지붕(2칸 이동)",
         (0, top + 0.02, -48), F, size=0.36)


# ------------------------------------------------------------------ the core, ranks, light column

def core_floor(n):
    """The core drum of floor n (from rank n up to the floor of rank n+1), hollow round the light shaft."""
    C = CORE[n]
    y0, y1 = H(n), H(n + 1) - SLAB
    span("F%d Core West" % n, -C, -SHAFT_X, y0, y1, -C, C, "marble")
    span("F%d Core East" % n, SHAFT_X, C, y0, y1, -C, C, "marble")
    for F in (SOUTH, NORTH):
        span("F%d Core Jamb" % n, -SHAFT_X, -DOOR_W / 2, y0, y1, -C, -SHAFT_Z, "marble", F)
        span("F%d Core Jamb" % n, DOOR_W / 2, SHAFT_X, y0, y1, -C, -SHAFT_Z, "marble", F)
        span("F%d Core Lintel" % n, -DOOR_W / 2, DOOR_W / 2, y0 + DOOR_H, y1, -C, -SHAFT_Z, "marble", F)
        # Two tall blind arches on the face and the rank number between them (only look).
        for x in (-C * 0.55, C * 0.55):
            span("Arch Niche", x - 1.6, x + 1.6, y0 + 2, y0 + 13, -C - 0.25, -C, "niche", F, kind="vbox")
        sign(str(n), (0, y0 + 9, -C - 0.3), F, yaw=0, size=6.0, standing=True)
    # Corner pillars carrying the rank above.
    if n <= 6:
        P = slab_half(n + 1) - 0.7
        for sx in (-1, 1):
            for sz in (-1, 1):
                span("F%d Corner Pillar" % n, sx * P - 0.6, sx * P + 0.6, y0, y1, sz * P - 0.6, sz * P + 0.6, "marble")
    # The light column shows through a glass strip on the east and west faces.
    for x in (-C - 0.15, C + 0.15):
        add("vbox", "Light Window", (x, (y0 + y1) / 2, 0), (0.3, y1 - y0 - 2, 2.4), c="light")


def rank_floor(rank):
    """The floor of rank 2..8: a square slab (or the summit) with the hole the previous lift rises through."""
    y1, y0 = H(rank), H(rank) - SLAB
    R = slab_half(rank) if rank < 8 else CORE[7]
    hx0, hx1 = cell_half_range(rank - 1)
    hz = 3.8
    span("R%d Floor" % rank, -R, hx0, y0, y1, -R, R, "honey")
    span("R%d Floor" % rank, hx1, R, y0, y1, -R, R, "honey")
    span("R%d Floor" % rank, hx0, hx1, y0, y1, -R, -hz, "honey")
    span("R%d Floor" % rank, hx0, hx1, y0, y1, hz, R, "honey")
    for z in (-hz, hz):
        add("vbox", "Lift Rim", ((hx0 + hx1) / 2, y1 + 0.03, z), (hx1 - hx0, 0.06, 0.12), c="light")
    for x in (hx0, hx1):
        add("vbox", "Lift Rim", (x, y1 + 0.03, 0), (0.12, 0.06, 2 * hz), c="light")
    if 2 <= rank <= 6:
        R = slab_half(rank)
        # Corner gardens: the rest spots of every rank.
        for sx in (-1, 1):
            for sz in (-1, 1):
                span("R%d Corner Garden" % rank, sx * (R - 1) if sx > 0 else -(R + 7), sx * (R + 7) if sx > 0 else -(R - 1),
                     y0, y1, sz * (R - 1) if sz > 0 else -(R + 7), sz * (R + 7) if sz > 0 else -(R - 1), "garden")
                cypress(sx * (R + 5.5), y1, sz * (R + 5.5))
                waterfall(sx * (R + 7.15), y0, y0 - 14, sz * (R + 3), 2.5, along_x=False)
    # Rank lines: a band of the rank's number on the floor, both sides.
    for F in (SOUTH, NORTH):
        if rank < 8:
            sign("%d랭크" % rank, (0, y1 + 0.02, -(slab_half(rank) - 2.2)), F, size=1.3)


def rank_one_floor():
    """Rank 1 is the top of the cliff; the core room and the first lift start there."""
    for F in (SOUTH, NORTH):
        sign("1랭크 — 갈고리 착지대", (0, H(1) + 0.02, -CLIFF + 3), F, size=1.2)


def light_column():
    """Per floor: the lift in the shaft (shown when that floor's bell is rung), the bell and the checkpoint."""
    for n in range(1, FLOORS + 1):
        g = group("F%d Light Lift (opens with the bell)" % n, (0, 0, 0), f="path", b=[n])
        x0, x1 = cell_half_range(n)
        rise = H(n + 1) - H(n)
        mover("F%d Light Lift" % n, ((x0 + x1) / 2, H(n) + 0.15, 0), (x1 - x0 - 0.3, 0.3, 2 * 3.8 - 0.3),
              (0, rise - 0.3, 0), 3.5, 2.0, c="light", g=g)
        top = H(n + 1)
        # The bell of floor n hangs at the top of the floor, in the middle of the shaft walkway.
        add("group", "F%d Pioneer Bell" % n, (0, top, 1.2), f="bell", b=[n])
        add("trig", "F%d Checkpoint (rank %d)" % (n, n + 1), (0, top + 1.5, 0),
            (70, 3, 70) if n + 1 < 8 else (24, 3, 24), f="checkpoint", b=[n, 0, top, -2.4, 0])
        sign("%d층 종 — F: 빛의 기둥 %d층 칸이 열린다 (20초는 친 팀만)" % (n, n), (0, top + 0.02, -1.0), size=0.22)


# ------------------------------------------------------------------ floor 1: waterfall terraces and the grand stair

def floor1(F):
    y = H(1)
    # The grand stair: three flights zigzagging up the south front, a gap to jump in the second,
    # a stone that slides across the first, landings on stout piers.
    OUT, IN_ = (-28.0, -24.5), (-23.0, -19.5)
    flight("F1 Stair Flight 1", (-26, y, sum(OUT) / 2), (-10, y + 7.5, sum(OUT) / 2), 3.5, F, rail_side=-1)
    pier("F1 Landing 1", -10, -6, -28, -19.5, y, y + 7.5, F)
    flight("F1 Stair Flight 2", (-6, y + 7.5, sum(IN_) / 2), (3, y + 10.5, sum(IN_) / 2), 3.5, F, rail_side=-1)
    flight("F1 Stair Flight 2", (5.5, y + 11.3, sum(IN_) / 2), (13, y + 14, sum(IN_) / 2), 3.5, F, rail_side=-1)
    pier("F1 Landing 2", 13, 18, -28, -19.5, y, y + 14, F)
    flight("F1 Stair Flight 3", (13, y + 14, sum(OUT) / 2), (-2, y + 20, sum(OUT) / 2), 3.5, F, rail_side=1)
    pier("F1 Landing 3", -6, -2, -28, -24.5, y, y + 20, F, trees=False)
    span("F1 Landing 3 Ledge", -7, -6, y + 13.5, y + 14, -28, -24.5, "garden", F)   # a rest half way up its west face
    span("F1 Bridge to Rank 2", -6, -2, y + 19, y + 20, -24.5, -slab_half(2), "honey", F)
    # The sliding stone on flight 1 (pushes you off the outer side).
    bob = add("box", "F1 Sliding Stone", F.p(-18, y + 4.35, -26.25), (2.0, 1.1, 1.0), F.r(0, 0, 0), "ruby",
              m="osc", a=[0, 0, 2.2 if not F.turned else -2.2, 4.0, 0])
    sign("1층 대계단: 한가운데가 무너졌다 — 달려서 뛰어넘기", F=F, p=(-8, y + 7.52, -21), yaw=90, size=0.34)

    # The arcade (athletic): climb a pillar (6 m) to its roof, the tower above it (7 m), cross a beam.
    for px in (19, 24, 29):
        for pz in (-29, -21):
            span("F1 Arcade Pillar", px - 1, px + 1, y, y + 6, pz - 1, pz + 1, "marble", F)
    span("F1 Arcade Roof", 18, 30, y + 6, y + 7, -30, -20, "checker", F)
    span("F1 Arcade Tower", 22, 30, y + 7, y + 14, -30, -24, "marble", F)
    span("F1 Balance Beam", 18, 22, y + 13.6, y + 14, -26.4, -25.6, "gold", F)
    waterfall(26, y + 14, y + 7, -30.2, 6, F)
    sign("아치 회랑: 기둥 6 m → 탑 7 m → 외나무 다리", (24, y + 7.02, -22), F, yaw=-90, size=0.3)

    # The hook's "two squares": a full-strength throw from the bridge lands on the arcade roof.
    sign("2칸 이동 착지대", (21, y + 7.02, -28.5), F, size=0.45)


# ------------------------------------------------------------------ floor 2: the chain gondolas

def floor2(F):
    y, top = H(2), H(3)
    R3 = slab_half(3)
    # Three gallows arms out from rank 3; the chequered cubes hang below and bob in turn.
    for ax in (-14, 0, 14):
        span("F2 Arm", ax - 1.2, ax + 1.2, top - 1, top, -R3 - 18, -R3, "marble", F)
    cubes = [(3, -19.5), (3, -24), (7.5, -28), (12, -30.5), (17, -27)]
    for k, (cx, cz) in enumerate(cubes):
        mid = y + 3 + 3.5 * k            # tops 36.5-41.5, 40-45, ... 50.5-55.5
        name = "F2 Gondola Cube %d" % (k + 1)
        g = add("box", name, F.p(cx, mid - 1.5, cz), (3, 3, 3), F.r(), "checker", m="osc", a=[0, 2.5, 0, 8.0, 4.0 * k])
        add("vbox", "Chain", (0, 1.5 + 5, 0), (0.12, 10, 0.12), c="chain", g=g)
    sign("2층 사슬 곤돌라: 큐브가 번갈아 오르내린다 — 위에 있는 것에서 다음 것으로", (5, y + 0.02, -slab_half(2) + 1.2), F, size=0.3)

    # The chain ladder (steady): a column with two rest balconies, three chains.
    span("F2 Ladder Garden", -17, -11, y - 1, y, -28, -20, "garden", F)
    span("F2 Ladder Bridge", -15.5, -12.5, y - 1, y, -20, -slab_half(2), "honey", F)
    span("F2 Ladder Column", -15.5, -12.5, y, top - 1, -25.5, -22.5, "marble", F)
    for yb in (y + 7, y + 14):
        span("F2 Rest Balcony", -15.5, -12.5, yb - 0.8, yb, -27.2, -24.2, "garden", F)
    rope("F2 Chain 1", (-14, y + 7, -27.55), 6.3, F)
    rope("F2 Chain 2", (-14, y + 14, -27.55), 6.3, F)
    rope("F2 Chain 3", (-15.6, top, -26.5), 5.8, F)
    # A chequered weight swinging past the chains.
    pv = group("F2 Pendulum", (-14, top - 1, -28.8), F, m="pend", a=[0, 0, 1, 35, 6.0, 0])
    add("vbox", "Pendulum Rod", (0, -4.5, 0), (0.25, 9, 0.25), c="chain", g=pv)
    add("box", "Pendulum Weight", (0, -10, 0), (2.4, 2.4, 2.4), c="ruby", g=pv)
    sign("사슬 사다리: 쉬는 발코니 두 곳", (-14, y + 0.02, -26.5), F, size=0.3)


# ------------------------------------------------------------------ floor 3: rising slabs, knight pads, bishop rails

def floor3(F):
    y, top = H(3), H(4)
    R3, R4 = slab_half(3), slab_half(4)
    # The slab spiral round a floating column: twelve slabs a metre apart, each bobbing a little.
    colx, colz = -5.0, -28.0
    span("F3 Pier", -7, -3, y - 1, y, -21, -R3, "honey", F)
    cap = y + 1.1 + 1.0 * 12          # one step above the last slab
    cyl("F3 Floating Column", (colx, (y + 3 + cap - 0.6) / 2, colz), 3.0, cap - 0.6 - (y + 3), "marble", F)
    cyl("F3 Column Cap", (colx, cap - 0.3, colz), 5.0, 0.6, "garden", F)
    n = 12
    for i in range(n):
        ang = math.radians(90 + 32 * i)          # starting on the tower side, going round
        sx, sz = colx + 4.2 * math.cos(ang), colz + 4.2 * math.sin(ang)
        mid = y + 1.1 + 1.0 * i
        add("box", "F3 Rising Slab %d" % (i + 1), F.p(sx, mid - 0.25, sz), (2.3, 0.5, 1.8),
            F.r(0, -math.degrees(ang) + 90, 0), "gold", m="osc", a=[0, 0.45, 0, 5.0, 0.7 * i])
    span("F3 Buttress", -7, -3, top - 6, top - 1, -24, -22.5, "marble", F)
    span("F3 Flying Bridge", -7, -3, top - 1, top, -24, -R4, "honey", F)
    sign("3층 떠오르는 석판: 기둥을 돌며 한 칸씩 → 기둥 꼭대기 → 벽 6 m", (-5, y + 0.02, -19.5), F, size=0.3)

    # The knight's pads (fast): three L-pads up a buttress, six metres each.
    span("F3 Knight Buttress", 26.5, 28, y, top - 2, -26, -17, "marble", F)
    ledges = [(62.0 - 56 + y, -23, -19), (68.0 - 56 + y, -26, -22.5), (74.0 - 56 + y, -23, -19)]
    for i, (ly, z0, z1) in enumerate(ledges):
        span("F3 Knight Ledge %d" % (i + 1), 23.5, 26.5, ly - 1, ly, z0, z1, "checker", F)
    launch("F3 Knight Pad 1", (21, y, -21), 90, (0, 6, 3), 1.2, F)
    launch("F3 Knight Pad 2", (25, ledges[0][0], -21), 180, (0, 6, 3.5), 1.2, F)
    launch("F3 Knight Pad 3", (25, ledges[1][0], -24.5), 0, (0, 6, 3.5), 1.2, F)
    sign("나이트의 도약대: 밟으면 위 6 m + 옆 3 m, 세 번", (19, y + 0.02, -19.5), F, yaw=90, size=0.3)

    # The bishop's rails (safe ride): two diagonal lifts up the south-west.
    mover("F3 Bishop Rail 1", (-19, y + 0.25, -25), (3, 0.5, 3), (-9, 9, 0), 2.2, 2.5, F=F)
    span("F3 Rail Landing", -36, -29.5, y + 8.5, y + 9, -27, -20, "garden", F)
    mover("F3 Bishop Rail 2", (-34, y + 9.25, -21.5), (3, 0.5, 3), (9, 10.5, 0), 2.2, 2.5, F=F)
    for a0, a1 in (((-19, y, -26.8), (-28, y + 9, -26.8)), ((-34, y + 9, -19.7), (-25, y + 19.5, -19.7))):
        slope("Rail", a0, a1, 0.25, "gold", F, thick=0.25, kind="vbox")
    sign("비숍 레일: 대각선으로 두 번", (-19, y + 0.02, -21), F, yaw=-90, size=0.3)


# ------------------------------------------------------------------ floor 4: the orbit rings and the rook towers (shared)

def orbit_ring(name, radius, low_y, high_y, low_at_south, dps):
    """A chequered ring round the tower, tilted so it is low on one side and high on the other,
    turning in its own plane: whoever rides it is carried up (or down) as it goes round."""
    centre = (low_y + high_y) / 2
    tilt = math.degrees(math.asin((high_y - low_y) / 2 / radius))
    # Tilting about X by +tilt lowers the +z side in Unity; low at south means +z (north) high.
    rx = -tilt if low_at_south else tilt
    g = group(name, (0, centre, 0), WORLD, r=(rx, 0, 0), m="move",
              a=[0, 0, 0, 0, 0, 0.5, dps, 0, 1, 0, 0])
    segs = 36
    chord = 2 * radius * math.sin(math.pi / segs) + 0.08
    for i in range(segs):
        a = 2 * math.pi * (i + 0.5) / segs
        add("box", "Ring Segment", (radius * math.cos(a), -0.3, radius * math.sin(a)), (3.0, 0.6, chord),
            (0, -math.degrees(a), 0), "lapis" if i % 2 == 0 else "palegold", g=g)
    return g


def floor4():
    y, top = H(4), H(5)
    R4, R5 = slab_half(4), slab_half(5)
    omega = 7.5
    orbit_ring("F4 Orbit Ring A (low south, high north)", 19.0, y + 2, y + 11, True, omega)
    orbit_ring("F4 Orbit Ring B (low north, high south)", 25.0, y + 2, y + 11, False, omega)
    hi = y + 11
    # White boards ring A from a dock on the south. At the ring's high point, by the north
    # tower, it steps off onto a ledge cut into the tower and climbs its upper wall to rank 5.
    span("F4 South Dock (ring A)", -3, 3, y, y + 2, -17.3, -R4, "gold")
    span("F4 North Tower", -3, 3, y, hi, R4, 17.2, "marble")
    span("F4 North Tower Ledge", -3, 3, hi - 0.3, hi, 15.8, 17.2, "checker")
    span("F4 North Tower Wall", -3, 3, hi, top, 14.4, 15.8, "marble")
    span("F4 North Tower Rest", -3, 3, hi + 4, hi + 4.5, 15.8, 16.6, "garden")
    # Black boards ring B from a pier on the north; its high point is by the south tower, which
    # stands between the two rings.
    span("F4 North Pier (ring B)", -10, -4, y, y + 2, R4, 22.2, "gold")
    span("F4 South Tower", -2.5, 2.5, y, hi, -23.0, -20.8, "marble")
    span("F4 South Tower Ledge", -2.5, 2.5, hi - 0.3, hi, -23.0, -21.8, "checker")
    span("F4 South Tower Wall", -2.5, 2.5, hi, top, -21.8, -20.8, "marble")
    span("F4 South Tower Rest", -2.5, 2.5, hi + 4, hi + 4.5, -22.6, -21.8, "garden")
    span("F4 South Tower Bridge", -2.5, 2.5, top - 1, top, -20.8, -R5, "honey")
    sign("4층 궤도 고리: 백은 남쪽 부두 → 안쪽 고리 A, 흑은 북쪽 부두 → 바깥 고리 B. 반 바퀴 돌면 9 m 위",
         (0, y + 0.02, -R4 + 1.4), size=0.28)
    sign("궤도 고리 B 부두", (-7, y + 2.02, 19), NORTH, size=0.35)
    # The rook towers in the four corners (the steady way): a base to stand on, the outer
    # face to climb with two rests, and a bridge from the top to rank 5's corner garden.
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = sx * 24, sz * 28

            def X(a, b):
                return (min(sx * a, sx * b), max(sx * a, sx * b))

            def Z(a, b):
                return (min(sz * a, sz * b), max(sz * a, sz * b))
            span("F4 Rook Base", *X(20.5, 27.5), y - 1, y, *Z(24.5, 31.5), "honey")
            span("F4 Rook Tower", *X(22, 26), y, top, *Z(26, 30), "marble")
            for ly in (y + 6.5, y + 13):
                span("F4 Rook Rest", *X(26, 27), ly - 0.5, ly, *Z(26, 30), "garden")
            for m in (22.3, 23.4, 24.6, 25.7):
                span("Merlon", *X(m - 0.3, m + 0.3), top, top + 0.8, *Z(29.4, 30), "marble")
            span("F4 Rook Bridge", *X(21, 23.5), y - 1, y, *Z(R4 + 7, 24.5), "honey")
            span("F4 Rook Bridge", *X(22, 24.4), top - 1, top, *Z(R5 + 7, 26), "honey")
    sign("룩의 탑: 바깥 벽 6.5 m씩 세 번 (쉬는 곳 두 곳)", (24, y + 0.02, -(R4 + 5)), size=0.3)


# ------------------------------------------------------------------ floor 5: the clockwork

def floor5(F):
    y, top = H(5), H(6)
    R5, R6 = slab_half(5), slab_half(6)
    # The clock wheel: eight gondolas that stay level, round a hub 9.5 m out.
    hub = (6, y + 10.5, -26)
    hoop("F5 Wheel Rim", (hub[0], hub[1], hub[2] - 1.6), 9.8, 0.5, "gold", F)
    for i in range(8):
        a = 22.5 + 45 * i
        add("vbox", "Spoke", F.p(hub[0] + 4.9 * math.cos(math.radians(a)), hub[1] + 4.9 * math.sin(math.radians(a)), hub[2] - 1.6),
            (9.8, 0.3, 0.3), F.r(0, 0, a), "gold")
    add("vcyl", "Hub", F.p(hub[0], hub[1], hub[2] - 1.6), (2.2, 0.8, 2.2), F.r(90, 0, 0), "gold")
    for i in range(8):
        add("box", "F5 Wheel Gondola %d" % (i + 1), F.p(*hub), (3.0, 0.4, 2.4), F.r(), "gold",
            m="orbit", a=[9.5, 32.0, 4.0 * i, 0, 0, 1])
    span("F5 Wheel Dock", 4, 8, y - 1, y, -24.6, -R5, "honey", F)
    span("F5 Wheel Top Dock", 4, 8, top - 1, top, -24.6, -R6, "honey", F)
    sign("5층 시계 관람차: 아래 부두에서 타고 꼭대기 부두로", (6, y + 0.02, -R5 - 1.5), F, size=0.3)

    # The clock face (a 20 m wall) with its hands turning: climb and dodge, two rests.
    span("F5 Clock Wall", -23, -13, y, top, -27, -25, "marble", F)
    span("F5 Clock Base", -23, -11, y - 1, y, -29, -27, "honey", F)
    span("F5 Clock Walk", -13, -11, y - 1, y, -27, -22.4, "honey", F)
    span("F5 Clock Walk", -23, -11, y - 1, y, -25, -22.4, "honey", F)
    for lx, ly in ((-21.5, y + 6.5), (-14.5, y + 13)):
        span("F5 Clock Rest", lx - 1.5, lx + 1.5, ly - 0.5, ly, -28, -27, "garden", F)
    add("vcyl", "F5 Clock Dial", F.p(-18, y + 10, -27.1), (9, 0.2, 9), F.r(90, 0, 0), "palegold")
    hands = group("F5 Clock Hands", (-18, y + 10, -27.6), F, m="spin", a=[0, 0, 1, 14, 0])
    add("box", "Hour Hand", (0, 2.3, 0), (0.6, 4.6, 0.4), c="ruby", g=hands)
    hands2 = group("F5 Clock Minute Hand", (-18, y + 10, -27.75), F, m="spin", a=[0, 0, 1, -24, 0])
    add("box", "Minute Hand", (0, 3.4, 0), (0.45, 6.8, 0.4), c="ruby", g=hands2)
    span("F5 Clock Top Bridge", -20, -16, top - 1, top, -25, -R6 - 7, "honey", F)
    sign("시계판 오르기: 바늘을 피하며 20 m, 쉬는 곳 두 곳", (-17, y + 0.02, -28), F, size=0.28)

    # The wound springs (fast): two throws of ten metres.
    span("F5 Spring Pillar", 17, 22, y - 6, y + 9, -27, -22.5, "marble", F)
    add("vcyl", "Gear", F.p(19.5, y + 4, -27.2), (6, 0.3, 6), F.r(90, 0, 0), "gold")
    launch("F5 Spring 1", (19.5, y, -20.5), 180, (0, 9, 3.5), 1.5, F, period=5.0, phase=0.0, same=True, size=(3.2, 0.6, 3.2))
    launch("F5 Spring 2", (19.5, y + 9, -25), 0, (0, 11, 4.5), 1.5, F, period=5.0, phase=2.5, same=True, size=(3.2, 0.6, 3.2))
    sign("태엽 스프링: 5초마다 10 m씩 두 번", (19.5, y + 0.02, -17.5), F, size=0.3)


# ------------------------------------------------------------------ floor 6: the floating gardens

def floor6(F):
    y, top = H(6), H(7)
    R6 = slab_half(6)

    def island(name, cx, cz, sx, sz, ty, bob=0.0, period=7.0, phase=0.0, fixed_rock=True):
        if bob > 0:
            n = add("box", name, F.p(cx, ty - 0.6, cz), (sx, 1.2, sz), F.r(), "garden", m="osc", a=[0, bob, 0, period, phase])
        else:
            n = add("box", name, F.p(cx, ty - 0.6, cz), (sx, 1.2, sz), F.r(), "garden")
        add("vbox", "Island Rock", (0, -1.8, 0), (sx * 0.8, 2.4, sz * 0.8), c="rock", g=n)
        add("vbox", "Cypress", (sx * 0.3, 2.2, sz * 0.25), (0.8, 3.0, 0.8), c="cypress", g=n)
        return n

    island("F6 Island 1", -8, -18.5, 5, 4, y + 1.5, bob=0.8, phase=0)
    island("F6 Island 2", -13, -25, 4, 4, y + 4.5, bob=1.0, phase=3.5)
    island("F6 Island 3", -8, -30, 6, 5, y + 6.5)
    waterfall(-8, y + 5.9, y - 6, -32.6, 3, F)
    island("F6 Island 4", -2, -30, 4, 4, y + 12.5)
    rope("F6 Chain", (-4.35, y + 12.5, -30), 6.2, F, facing=-90)
    island("F6 Island 5", 10, -30, 4, 4, y + 12.5)
    add("vbox", "Swing Beam", F.p(4, y + 19.6, -30), (12, 0.4, 0.4), F.r(), "marble")
    rope("F6 Swing", (4, y + 19.4, -30), 8.0, F, facing=90, swing=40, period=5.0, thickness=0.07, c="rope")
    mover("F6 Rising Island", (14, y + 12.5 - 0.6, -26), (4, 1.2, 4), (0, 7, 0), 1.5, 2.0, c="garden", F=F)
    sign("6층 공중 정원: 떠 있는 섬 → 사슬 → 그네(8 m) → 오르는 섬", (-8, y + 0.02, -R6 - 0.8), F, size=0.28)

    # The ivy buttress (steady): three climbs of 6-7 m with two rests.
    span("F6 Ivy Buttress", -23, -19, y, top, -27, -23, "marble", F)
    span("F6 Ivy Base Ledge", -23, -13, y - 0.5, y, -28.2, -27, "garden", F)   # along the clock wall's top
    for ly in (y + 6, y + 13):
        span("F6 Ivy Rest", -23, -19, ly - 0.5, ly, -28.2, -27, "garden", F)
    add("vbox", "Ivy", F.p(-21, y + 10, -27.05), (3.4, 18, 0.1), F.r(), "cypress")
    sign("담쟁이 버팀벽: 시계탑 꼭대기 남쪽 턱에서 6-7 m씩 세 번", (-16, y + 0.02, -27.6), F, size=0.24)


# ------------------------------------------------------------------ floor 7: the double spiral stair, the light stair (shared)

def floor7():
    y, top = H(7), H(8)
    R = 12.5
    turn = 450.0
    for k, start in enumerate((-90.0, 90.0)):
        segs = 44
        for i in range(segs):
            u0, u1 = i / segs, (i + 1) / segs
            um = (u0 + u1) / 2
            phi = math.radians(start + turn * um)
            h = y + (top - y) * um
            yaw = -math.degrees(phi)
            ds = R * math.radians(turn) / segs
            pitch = -math.degrees(math.atan2((top - y) / segs, ds))
            add("box", "F7 Spiral Stair %s" % ("A" if k == 0 else "B"),
                (R * math.cos(phi), h - 0.3, R * math.sin(phi)), (3.2, 0.6, ds + 0.1), (pitch, yaw, 0),
                "honey" if i % 2 == 0 else "palegold")
    # A sweeping arm round the drum at mid height.
    arm = group("F7 Sweeper", (0, y + 10.8, 0), m="spin", a=[0, 1, 0, 20, 0])
    add("box", "Sweeper Arm", (11.3, 0, 0), (6.6, 0.5, 0.6), c="ruby", g=arm)
    add("box", "Sweeper Arm", (-11.3, 0, 0), (6.6, 0.5, 0.6), c="ruby", g=arm)
    add("vbox", "Sweeper Collar", (0, y + 10.8, 0), (2 * CORE[7] + 0.6, 0.8, 2 * CORE[7] + 0.6), c="gold")

    # The light stair: floating steps that come and go, straight up the south and north faces.
    for F in (SOUTH, NORTH):
        for i in range(19):
            x = -1.2 if i % 2 == 0 else 1.2
            z = -(19.7 - 0.25 * i)
            add("box", "F7 Light Step", F.p(x, y + 1.0 * (i + 1) - 0.15, z), (1.6, 0.3, 0.9), F.r(), "light",
                m="toggle", a=[4.0, 0.0, 0.7, 0.22 * i])
        span("F7 Light Bridge", -1.5, 1.5, top - 1, top, -14.5, -CORE[7], "light", F)
        sign("빛의 계단: 켜졌다 꺼지는 발판 19칸", (0, y + 0.02, -21.5), F, size=0.35)
    sign("7층 대발코니: 두 갈래 나선 계단 (가운데 쓸어내는 팔 조심)", (0, y + 0.02, -17), size=0.32)
    # Gardens in the corners of the balcony (only look; the balcony is the floor).
    for sx in (-1, 1):
        for sz in (-1, 1):
            add("vbox", "Balcony Garden", (sx * 19.5, y + 0.05, sz * 19.5), (7, 0.1, 7), c="garden")
            cypress(sx * 21.5, y, sz * 21.5)
            cypress(sx * 17.5, y, sz * 21.5)


# ------------------------------------------------------------------ rank 8: the summit

def summit():
    y = H(8)
    C = CORE[7]
    for sx, sz, ex, ez in ((1, 0, 10.2, 3.0), (-1, 0, 10.2, 3.0), (0, 1, 3.0, 10.2), (0, -1, 3.0, 10.2)):
        if sx:
            span("R8 Petal", min(sx * C, sx * ex), max(sx * C, sx * ex), y - SLAB, y, -ez, ez, "honey")
        else:
            span("R8 Petal", -ex, ex, y - SLAB, y, min(sz * C, sz * ez), max(sz * C, sz * ez), "honey")
    add("vcyl", "R8 Summit Disc", (0, y + 0.01, 0), (16.4, 0.02, 16.4), c="palegold")   # inside the floor everywhere
    # The eighth rank: eight squares round the gate, the pieces of the back rank.
    files = "abcdefgh"
    kinds = ["rook", "knight", "bishop", "queen", "king", "bishop", "knight", "rook"]
    kind_ids = {"rook": 1, "bishop": 2, "knight": 3, "king": 4}
    names = {"rook": "룩", "knight": "나이트", "bishop": "비숍", "queen": "퀸 (보석판, 아직 없음)", "king": "킹"}
    for i in range(8):
        phi = math.radians(22.5 + 45 * i + 180)
        cx, cz = 6.4 * math.cos(phi), 6.4 * math.sin(phi)
        yaw = -math.degrees(phi) - 90
        add("vbox", "Rank 8 Square %s8" % files[i], (cx, y + 0.03, cz), (3.0, 0.06, 3.0), (0, yaw, 0),
            "lapis" if i % 2 == 0 else "palegold")
        k = kinds[i]
        if k == "queen":
            add("vbox", "Queen Jewel Plate", (cx, y + 0.08, cz), (1.8, 0.06, 1.8), (0, yaw, 0), "light")
        else:
            add("box", "Pedestal %s8" % files[i], (cx, y + 0.4, cz), (0.6, 0.8, 0.6), (0, yaw, 0), "gold")
            add("group", "Promote %s8" % files[i], (cx, y, cz), f="promote", b=[kind_ids[k]])
        add("sign", "Sign", (cx * 1.32, y + 0.05, cz * 1.32), r=(90, yaw, 0), f="sign", b=[0.35],
            t="%s8 %s" % (files[i], names[k]))
    # The gate and the crown (only look).
    for x in (-3.2, 3.2):
        add("box", "Gate Pillar", (x, y + 5, 0), (0.9, 10, 0.9), c="marble")
    hoop("Gate Ring", (0, y + 11.2, 0), 4.2, 0.5, "gold", facing=90)
    hoop("Crown Halo", (0, y + 7.5, 0), 2.2, 0.25, "light", upright=False)
    add("vbox", "Crown", (0, y + 7.5, 0), (1.6, 1.6, 1.6), (45, 45, 0), "gold")
    sign("8랭크 — 칸 위 받침대 앞에서 F = 승격", (0, y + 0.02, 5.2), size=0.4)


# ------------------------------------------------------------------ scenery

def scenery():
    rocks = [(-150, 10, 70, 30), (170, 18, -40, 40), (95, 6, 180, 26), (-120, 4, -160, 22),
             (-62, 70, 48, 9), (58, 110, -38, 11), (52, 40, 55, 8), (-55, 135, -32, 7), (40, 150, 40, 6)]
    for x, y, z, s in rocks:
        add("vbox", "Floating Rock", (x, y, z), (s, s * 0.8, s * 1.1), (0, x, 0), "rock")
    for i in range(10):
        a = i * 36.0
        rr = 60 + (i % 3) * 25
        add("vbox", "Cloud", (rr * math.cos(math.radians(a)), 70 + (i % 4) * 6, rr * math.sin(math.radians(a))),
            (22, 3, 12), (0, a, 0), "cloud")


# ------------------------------------------------------------------ build

def build():
    pieces.clear()
    _names.clear()
    sea_and_cliff()
    for F in (SOUTH, NORTH):
        bridge(F)
    rank_one_floor()
    for n in range(1, FLOORS + 1):
        core_floor(n)
    for rank in range(2, RANKS + 1):
        rank_floor(rank)
    light_column()
    for F in (SOUTH, NORTH):
        floor1(F)
        floor2(F)
        floor3(F)
    floor4()
    for F in (SOUTH, NORTH):
        floor5(F)
        floor6(F)
    floor7()
    summit()
    scenery()
    return {
        "version": 7,
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
