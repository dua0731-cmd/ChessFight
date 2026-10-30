"""Pawn Rush endgame (phase 3, the war room): level data shared by the Blender build, the checks and the docs.

Unity axes: x east, y up, z north (the way runs north). Origin: the S1 floor, front centre. 1 unit = 1 m.
Blender takes Unity x -> X, z -> Y, y -> Z (see to_blender).

Two layouts live here:
  GRAYBOX  the gray box as Course01v4Builder.WarRoom.cs builds it on seoungtest (10-30), for EG_GameplayRef.
  LD_V1    the hard redesign (ENDGAME_LD_HARD_v1.md).

Every moving thing is a pure function of the shared clock (ObstacleClock), written the same way as the Unity
components (ObstacleMotion, PendulumSwing, FoldingBridge, FallingChessPiece, AirVent), so the checks and the
timing previews sample exactly what the game will do.
"""
import math

# ---------------------------------------------------------------------------------------------- movement
HALF = 6.0            # the way is 12 m wide, x -6..6
UPPER = 3.0           # floor after the lift tiles
PAWN_R = 0.35         # a pawn as a capsule 0.7 wide and 1.0 high
PAWN_H = 1.0
RUN = 2.5
SPRINT = 3.6
JUMP_AIR = 0.7        # seconds in the air
JUMP_RISE = 0.87      # metres the pelvis rises
STEP_UP = 0.25        # walked up without a jump
LEDGE_SURE = 0.6      # jumped up for sure
GAP_MAX = 1.5         # the widest required gap
RAIL_H = 1.2


def to_blender(x, y, z):
    """Unity position -> Blender position."""
    return (x, z, y)


def clamp01(t):
    return 0.0 if t < 0.0 else 1.0 if t > 1.0 else t


def smooth(t):
    t = clamp01(t)
    return t * t * (3.0 - 2.0 * t)


def repeat(t, length):
    return t - math.floor(t / length) * length


def periodic_time(time, delay, period):
    """ObstacleContext.PeriodicTime (repeat = true)."""
    age = time - max(0.0, delay)
    if age < 0.0:
        return min(time, delay + period + 1.0)
    return max(0.0, delay) + age % max(0.001, period)


# ---------------------------------------------------------------------------------------------- devices
class Motion:
    """ObstacleMotion: Translate (sine), Gate (smoothstep of the sine) or Rotate (degrees per second).
    `phase` is the stored fraction of a cycle; shift() adds seconds the way ObstaclePhase.Shift does."""

    def __init__(self, kind, distance=0.0, period=5.0, phase=0.0, dps=0.0):
        self.kind, self.distance, self.period, self.phase, self.dps = kind, distance, period, phase, dps

    def shift(self, seconds):
        cycles = seconds * self.dps / 360.0 if self.kind == "rotate" else seconds / max(0.5, self.period)
        self.phase = repeat(self.phase + cycles, 1.0)
        return self

    def value(self, t):
        """Translate / Gate: metres along the axis. Rotate: degrees."""
        if self.kind == "rotate":
            return (t * self.dps + self.phase * 360.0) % 360.0
        wave = math.sin(((t / max(0.5, self.period) + self.phase) % 1.0) * math.pi * 2.0)
        if self.kind == "gate":
            return self.distance * smooth((wave + 0.35) / 0.7)
        return self.distance * wave

    def cycle(self):
        return 360.0 / abs(self.dps) if self.kind == "rotate" else self.period


class Pendulum:
    """PendulumSwing (11): +-55 degrees about the frame's forward axis, 2 s from side to side, 0.2 s at each end."""
    angle_max, swing, pause, delay = 55.0, 2.0, 0.2, 0.5
    pivot_h, chain, ball_r = 7.3, 6.0, 1.1

    def __init__(self, phase=0.0):
        self.phase = phase

    def cycle(self):
        return 2.0 * (self.swing + self.pause)

    def angle(self, t):
        elapsed = periodic_time(t + self.phase, self.delay, self.cycle())
        a, d, p = self.angle_max, self.swing, self.pause
        if elapsed <= self.delay:
            return -a
        u = repeat(elapsed - self.delay, 2.0 * (d + p))
        if u < p:
            return -a
        u -= p
        if u < d:
            return -a * math.cos(math.pi * u / d)
        u -= d
        if u < p:
            return a
        return a * math.cos(math.pi * (u - p) / d)

    def ball(self, t):
        """Ball centre in the frame's own axes (lateral, height over the floor)."""
        th = math.radians(self.angle(t))
        return self.chain * math.sin(th), self.pivot_h - self.chain * math.cos(th)


class FoldingBridge:
    """FoldingBridge (12): open 5, warning 1.5, folding 2.5, closed 2, unfolding 2.5 (13.5 s), panels up to 70 degrees."""
    open_, warn, fold, closed, delay, angle_max = 5.0, 1.5, 2.5, 2.0, 1.0, 70.0

    def __init__(self, phase=0.0):
        self.phase = phase

    def cycle(self):
        return self.open_ + self.warn + 2.0 * self.fold + self.closed

    def local(self, t):
        e = periodic_time(t + self.phase, self.delay, self.cycle())
        u = e - self.delay
        if u < 0.0:
            return 0.0
        return repeat(u, self.cycle())

    def state(self, t):
        u = self.local(t)
        if u < self.open_:
            return "open", 0.0
        u -= self.open_
        if u < self.warn:
            return "warning", 0.0
        u -= self.warn
        if u < self.fold:
            return "folding", self.angle_max * smooth(u / self.fold)
        u -= self.fold
        if u < self.closed:
            return "closed", self.angle_max
        u -= self.closed
        return "unfolding", self.angle_max * (1.0 - smooth(u / self.fold))


class FallingPiece:
    """FallingChessPiece (10): warning 1.4, fall 0.65, shock 0.7, rest 1.8 (4.55 s); shock 10 + 4 up within 5 m."""
    warning, fall, shock, rest, drop, radius = 1.4, 0.65, 0.7, 1.8, 9.0, 5.0

    def __init__(self, phase=0.0):
        self.phase = phase

    def cycle(self):
        return self.warning + self.fall + self.shock + self.rest

    def local(self, t):
        return repeat(repeat(t, self.cycle()) + self.phase, self.cycle())

    def shocking(self, t):
        u = self.local(t)
        return self.warning + self.fall <= u < self.warning + self.fall + self.shock

    def warned(self, t):
        return self.local(t) < self.warning + self.fall

    def piece_height(self, t):
        """Height of the falling piece's base over its floor."""
        u = self.local(t)
        if u < self.warning:
            return self.drop
        u -= self.warning
        if u < self.fall:
            k = u / self.fall
            return self.drop * (1.0 - k * k)
        u -= self.fall
        if u < self.shock + self.rest * 0.6:
            return 0.0
        k = (u - self.shock - self.rest * 0.6) / (self.rest * 0.4)
        return self.drop * smooth(k)


class AirVent:
    """AirVent (15): idle 3, warning 0.8, blowing 3 (6.8 s); a 3 x 3 x 6 m zone in front, up to 10 m/s."""
    idle, warn, blow, delay = 3.0, 0.8, 3.0, 0.5

    def __init__(self, phase=0.0):
        self.phase = phase

    def cycle(self):
        return self.idle + self.warn + self.blow

    def state(self, t):
        e = periodic_time(t + self.phase, self.delay, self.cycle())
        u = e - self.delay
        if u < 0.0:
            return "idle"
        u = repeat(u, self.cycle())
        if u < self.idle:
            return "idle"
        return "warning" if u < self.idle + self.warn else "blowing"


# ---------------------------------------------------------------------------------------------- the layout
class Layout:
    """Static boxes, rails, checkpoints and devices of one version of the course."""

    def __init__(self, name):
        self.name = name
        self.floors = []      # walked on: dict(name, beat, x0, x1, z0, z1, top, bottom, mat)
        self.walls = []       # dict(name, beat, x0, x1, y0, y1, z0, z1, climb, top_walk, mat)
        self.rails = []       # dict(name, beat, x0, x1, z0, z1, floor, h)
        self.decor = []       # boxes with no colliders: dict(name, beat, x0..z1, mat)
        self.devices = []     # dict(id, beat, type, pos, yaw, params..., timing)
        self.checkpoints = []  # (index, x, y, z)
        self.beats = []       # dict(id, title, z0, z1, y, cp)
        self.route = []       # (z, y, note): the walked line, for the path check

    def floor(self, name, beat, x0, x1, z0, z1, top, bottom=None, mat="EG_Marble_Cream"):
        self.floors.append(dict(name=name, beat=beat, x0=x0, x1=x1, z0=z0, z1=z1, top=top,
                                bottom=top - 1.0 if bottom is None else bottom, mat=mat))

    def wall(self, name, beat, x0, x1, y0, y1, z0, z1, climb=False, top_walk=False, mat="EG_Stone"):
        self.walls.append(dict(name=name, beat=beat, x0=x0, x1=x1, y0=y0, y1=y1, z0=z0, z1=z1,
                               climb=climb, top_walk=top_walk, mat=mat))

    def rails_both(self, name, beat, z0, z1, floor, half=HALF):
        for side, label in ((-1, "W"), (1, "E")):
            x0 = -half - 0.5 if side < 0 else half
            self.rails.append(dict(name=f"{name} rail {label}", beat=beat, x0=x0, x1=x0 + 0.5, z0=z0, z1=z1,
                                   floor=floor, h=RAIL_H))

    def rail(self, name, beat, side, z0, z1, floor, half=HALF):
        x0 = -half - 0.5 if side < 0 else half
        self.rails.append(dict(name=name, beat=beat, x0=x0, x1=x0 + 0.5, z0=z0, z1=z1, floor=floor, h=RAIL_H))

    def box(self, name, beat, x0, x1, y0, y1, z0, z1, mat):
        self.decor.append(dict(name=name, beat=beat, x0=x0, x1=x1, y0=y0, y1=y1, z0=z0, z1=z1, mat=mat))

    def device(self, **d):
        self.devices.append(d)
        return d

    def cp(self, index, z, y=0.0, x=0.0):
        self.checkpoints.append((index, x, y, z))

    def beat(self, bid, title, z0, z1, y, cp=None):
        self.beats.append(dict(id=bid, title=title, z0=z0, z1=z1, y=y, cp=cp))

    def device_by_id(self, did):
        for d in self.devices:
            if d["id"] == did:
                return d
        raise KeyError(did)


def _war_floor(L, name, beat, x0, x1, z0, z1, top):
    L.floor(name, beat, x0, x1, z0, z1, top)


# ---- the gray box (Course01v4Builder.WarRoom.cs, seoungtest 10-30) -------------------------------------------
def graybox():
    L = Layout("graybox")
    ferry_gap0 = 40.0
    ferry_gap1 = ferry_gap0 + 2.0 * (4.0 + 6.0) + 0.4     # 60.4
    s4_start, bridge_half = 196.0, 7.05
    s4_end = s4_start + 2.0 * bridge_half                 # 210.1
    U = UPPER

    L.beat("S1", "진입 회랑", 0, 14, 0, 1)
    _war_floor(L, "S1 floor", "S1", -6, 6, 0, 14, 0)
    L.wall("S1 back wall", "S1", -6.5, 6.5, -1, 4, -0.5, 0)
    L.rails_both("S1", "S1", 0, 14, 0)
    L.cp(1, 3)

    L.beat("R1", "회전 작전 탁자", 14, 40, 0, 2)
    _war_floor(L, "R1 bridge on", "R1", -4, 4, 14, 16, 0)
    L.device(id="R1_board", beat="R1", type="board", pos=(0, 0, 24), yaw=0, diameter=16.0, thick=0.5,
             timing=Motion("rotate", dps=18.0))
    L.device(id="R1_bar", beat="R1", type="spinbar", pos=(0, 0, 24), yaw=0, length=15.0, y0=0.2, y1=0.52, thick=0.45,
             timing=Motion("rotate", dps=-60.0))
    _war_floor(L, "R1 bridge off", "R1", -4, 4, 32, 34, 0)
    _war_floor(L, "R1 landing", "R1", -6, 6, 34, ferry_gap0, 0)
    L.rails_both("R1 landing", "R1", 34, ferry_gap0, 0)
    L.cp(2, 36)

    L.beat("S2", "첫 작전교", 40, ferry_gap1, 0, None)
    mid = (ferry_gap0 + ferry_gap1) * 0.5
    L.device(id="S2_ferry", beat="S2", type="ferry", pos=(0, 0, mid), yaw=0, half_x=4.0, half_z=4.0, travel=6.0,
             timing=Motion("translate", distance=6.0, period=14.0, phase=0.75), frame_h=0.5, posts=True)

    L.beat("R2", "책장 회랑", ferry_gap1, 88, 0, 3)
    _war_floor(L, "R2 floor", "R2", -6, 6, ferry_gap1, 88, 0)
    L.rails_both("R2", "R2", ferry_gap1, 88, 0)
    L.cp(3, ferry_gap1 + 2)
    L.device(id="R2_walls", beat="R2", type="sliding", pos=(0, 0, 70), yaw=0, wall_w=3.0, rest=5.5, travel=3.5,
             housing=7.1, timing=None, phase_s=0.0)
    L.device(id="R2_gates", beat="R2", type="clockgates", pos=(0, 0, 80), yaw=0, phase_s=0.0)
    L.wall("R2 gate side W", "R2", -6, -4.3, 0, 6, 79.4, 80.6)
    L.wall("R2 gate side E", "R2", 4.3, 6, 0, 6, 79.4, 80.6)

    L.beat("S3", "지휘 착지면", 88, 112, 0, 4)
    _war_floor(L, "S3 floor", "S3", -6, 6, 88, 112, 0)
    L.rails_both("S3", "S3", 88, 112, 0)
    L.cp(4, 90)
    for z, ph in ((95.0, 0.0), (106.0, 3.0)):
        L.device(id=f"S3_screen_{int(z)}", beat="S3", type="rookscreen", pos=(0, 0, z), yaw=0, length=6.0, depth=2.0,
                 height=3.2, timing=Motion("translate", distance=3.0, period=6.0).shift(ph))

    L.beat("R3", "깃발 다리", 112, 132, 0, None)
    _war_floor(L, "R3 bridge", "R3", -5, 5, 112, 132, 0)
    for z, ph in ((118.0, 0.0), (126.0, 2.2)):
        L.device(id=f"R3_pendulum_{int(z)}", beat="R3", type="pendulum", pos=(0, 0, z), yaw=0, timing=Pendulum(ph))
        L.wall(f"R3 frame post W {int(z)}", "R3", -7.8, -7, -3, 8.3, z - 0.4, z + 0.4, mat="EG_Teal")
        L.wall(f"R3 frame post E {int(z)}", "R3", 7, 7.8, -3, 8.3, z - 0.4, z + 0.4, mat="EG_Teal")
        L.wall(f"R3 frame beam {int(z)}", "R3", -7.8, 7.8, 7.8, 8.3, z - 0.4, z + 0.4, mat="EG_Gold")

    L.beat("R4", "승강 지도함", 132, 143, 0, 5)
    _war_floor(L, "R4 floor", "R4", -6, 6, 132, 136, 0)
    L.cp(5, 133)
    L.device(id="R4_tiles_W", beat="R4", type="lifttiles", pos=(-3.35, 0, 139.6), yaw=0, phase_s=0.0)
    L.device(id="R4_tiles_E", beat="R4", type="lifttiles", pos=(3.35, 0, 139.6), yaw=0, phase_s=3.0)
    L.wall("R4 upper front", "R4", -6, 6, -2, U, 143, 144, top_walk=True)

    L.beat("R5", "벨트 · 낙하 말", 143, 182, U, 6)
    _war_floor(L, "R5 floor 1", "R5", -6, 6, 144, 146, U)
    L.cp(6, 145, U)
    for x in (-3.2, 3.2):
        L.device(id=f"R5_belt_{'W' if x < 0 else 'E'}", beat="R5", type="conveyor", pos=(x, U - 0.4, 150), yaw=0,
                 width=6.0, length=8.0, speed=-1.5)
    _war_floor(L, "R5 floor 2", "R5", -6, 6, 154, 182, U)
    L.rails_both("R5", "R5", 144, 182, U)
    L.device(id="R5_piece_W", beat="R5", type="fallingpiece", pos=(-3, U, 162), yaw=0, timing=FallingPiece(0.0))
    L.device(id="R5_piece_E", beat="R5", type="fallingpiece", pos=(3, U, 174), yaw=0, timing=FallingPiece(2.3))
    for i, (x, z) in enumerate(((2.6, 159), (-2.6, 171), (0, 178.5))):
        L.wall(f"R5 crate {i + 1}", "R5", x - 1, x + 1, U, U + 1.5, z - 0.75, z + 0.75, top_walk=True, mat="EG_Walnut")

    L.beat("R6", "회전 원판", 182, 196, U, 7)
    L.device(id="R6_disc", beat="R6", type="disc", pos=(0, U - 0.4, 186), yaw=0, diameter=8.0)
    _war_floor(L, "R6 landing", "R6", -6, 6, 190, s4_start, U)
    L.rails_both("R6 landing", "R6", 190, s4_start, U)
    L.cp(7, 191.5, U)

    L.beat("S4", "마지막 작전교", s4_start, s4_end, U, None)
    mid4 = s4_start + bridge_half
    L.device(id="S4_bridge_W", beat="S4", type="foldingbridge", pos=(-3, U, mid4), yaw=90, timing=FoldingBridge(0.0))
    L.device(id="S4_bridge_E", beat="S4", type="foldingbridge", pos=(3, U, mid4), yaw=90, timing=FoldingBridge(6.75))

    _exit(L, s4_end, U, runout=12.0)
    return L


def _exit(L, start, y, runout):
    """The runout, the 12 x 6 m terrace (CP 8), the open arch, four 0.25 m steps down, the 4 m entry strip."""
    terrace = start + runout
    arch = terrace + 6.0
    L.beat("EX", "출구 · 미션실 입구", start, arch + 6.0, y, 8)
    _war_floor(L, "Runout", "EX", -6, 6, start, terrace, y)
    _war_floor(L, "Terrace", "EX", -6, 6, terrace, arch, y)
    L.rails_both("Exit", "EX", start, arch, y)
    L.cp(8, terrace + 3.0, y)
    L.wall("Arch pier W", "EX", -7, -6, y - 1, y + 5.5, arch - 0.5, arch + 0.5)
    L.wall("Arch pier E", "EX", 6, 7, y - 1, y + 5.5, arch - 0.5, arch + 0.5)
    L.wall("Arch lintel", "EX", -7, 7, y + 5.5, y + 6.5, arch - 0.5, arch + 0.5, mat="EG_Gold")
    for i in range(4):
        _war_floor(L, f"Step {i + 1}", "EX", -6, 6, arch + i * 0.5, arch + (i + 1) * 0.5, y - 0.25 * (i + 1))
    _war_floor(L, "Entry strip", "EX", -6, 6, arch + 2.0, arch + 6.0, y - 1.0)
    L.wall("Mission room (not built) wall", "EX", -6.5, 6.5, y - 2, y + 4, arch + 6.0, arch + 6.5)
    L.exit = dict(terrace=terrace, arch=arch, end=arch + 6.0, y=y)


# ---- LD v1: the hard redesign ----------------------------------------------------------------------------------
# Values marked "v1" differ from the gray box; each is listed in ENDGAME_LD_HARD_v1.md "Unity 반영 목록".
V1 = dict(
    bar_length=16.4,                  # R1 sweep bar reaches past the 16 m board (gray box 15 m)
    ferry_period=12.0,                # S2 (gray box 14 s): boarding window 3.0 -> 2.6 s
    ferry_frame_h=0.12,               # S2 deck rim flush, under the hurdle bar (gray box 0.5 m beams, 0.9 m posts)
    hurdle=(0.2, 0.52, 0.45),         # S2 fixed hurdle across the chasm middle: bottom, top, thickness
    screen_period=5.0,                # S3 rook screens, the 09 gate's 5 s
    screen_phase=(0.25, 2.25),        # seconds: a runner leaving a gate door meets the open half (1.55 s)
    r3_half=3.0,                      # R3 bridge 6 m wide (gray box 10 m: lanes |x| > 3.4 never met a ball)
    r3_pend_phase=(0.0, 1.1),
    belt_speed=1.5,                   # R5 belts push north (toward the pieces)
    piece_phase=(0.0, 2.275),         # R5 pair at one z, half a cycle apart
    crate_period=4.4, crate_amp=0.5,  # R5b hanging crates
    s4_bridge_phase=(0.0, 6.75),
    s4_screen_period=13.5, s4_screen_phase=7.25,  # one sweep per bridge cycle: 2.5 s to cross bridge and screen in one go
    vent_phase=(0.0, 3.4),
)


def ld_v1():
    L = Layout("ld_v1")
    U = UPPER
    ferry_gap0 = 40.0
    ferry_gap1 = 60.4
    mid = 50.2

    # S1: entry corridor, the map table (where the ferry is, which S4 bridge is open) ------------------------------
    L.beat("S1", "진입 회랑 · 예고 확인", 0, 14, 0, 1)
    _war_floor(L, "S1 floor", "S1", -6, 6, 0, 14, 0)
    L.wall("S1 back wall", "S1", -6.5, 6.5, -1, 4, -0.5, 0)
    L.rails_both("S1", "S1", 0, 14, 0)
    L.cp(1, 3)
    L.wall("S1 map table", "S1", 6.5, 8.5, -1, 1.3, 4, 10, mat="EG_Walnut")

    # R1: the rotating war table, a full-length sweep bar --------------------------------------------------------
    L.beat("R1", "회전 작전 탁자", 14, 40, 0, 2)
    _war_floor(L, "R1 bridge on", "R1", -4, 4, 14, 16, 0)
    L.device(id="R1_board", beat="R1", type="board", pos=(0, 0, 24), yaw=0, diameter=16.0, thick=0.5,
             timing=Motion("rotate", dps=18.0))
    L.device(id="R1_bar", beat="R1", type="spinbar", pos=(0, 0, 24), yaw=0, length=V1["bar_length"], y0=0.2, y1=0.52,
             thick=0.45, timing=Motion("rotate", dps=-60.0))
    _war_floor(L, "R1 bridge off", "R1", -4, 4, 32, 34, 0)
    _war_floor(L, "R1 landing", "R1", -6, 6, 34, ferry_gap0, 0)
    L.rails_both("R1 landing", "R1", 34, ferry_gap0, 0)
    L.cp(2, 36)

    # S2: the first crossing, a ferry deck under a fixed hurdle ---------------------------------------------------
    L.beat("S2", "첫 작전교 (결정적 건넘 1)", 40, ferry_gap1, 0, None)
    L.device(id="S2_ferry", beat="S2", type="ferry", pos=(0, 0, mid), yaw=0, half_x=4.0, half_z=4.0, travel=6.0,
             timing=Motion("translate", distance=6.0, period=V1["ferry_period"], phase=0.75),
             frame_h=V1["ferry_frame_h"], posts=False)
    hb, ht, hd = V1["hurdle"]
    L.device(id="S2_hurdle", beat="S2", type="hurdle", pos=(0, 0, mid), yaw=0, x0=-5.6, x1=5.6, y0=hb, y1=ht, thick=hd)
    for side in (-1, 1):
        L.wall(f"S2 hurdle pier {'W' if side < 0 else 'E'}", "S2", min(side * 5.6, side * 6.4),
               max(side * 5.6, side * 6.4), -16, 1.0, mid - 0.4, mid + 0.4, mat="EG_Stone")

    # R2: the bookshelf hall: 06 walls stretched to the 12 m way, a pocket, 09 gates ----------------------------
    L.beat("R2", "책장 회랑", ferry_gap1, 88, 0, 3)
    _war_floor(L, "R2 floor", "R2", -6, 6, ferry_gap1, 88, 0)
    # rails stop where a wall slides through them (W 66.7..69.7, E 70.3..73.3)
    L.rail("R2 rail W south", "R2", -1, ferry_gap1, 66.7, 0)
    L.rail("R2 rail W north", "R2", -1, 69.7, 88, 0)
    L.rail("R2 rail E south", "R2", 1, ferry_gap1, 70.3, 0)
    L.rail("R2 rail E north", "R2", 1, 73.3, 88, 0)
    L.cp(3, ferry_gap1 + 2)
    L.device(id="R2_walls", beat="R2", type="sliding", pos=(0, 0, 70), yaw=0, wall_w=6.0, rest=9.0, travel=6.0,
             housing=12.3, phase_s=0.0)
    L.device(id="R2_gates", beat="R2", type="clockgates", pos=(0, 0, 80), yaw=0, phase_s=0.0)
    L.wall("R2 gate side W", "R2", -6, -4.3, 0, 6, 79.4, 80.6)
    L.wall("R2 gate side E", "R2", 4.3, 6, 0, 6, 79.4, 80.6)

    # S3: the command landing: CP 4 keeps the lead, two rook screens chained to the gate's 5 s -------------------
    L.beat("S3", "지휘 착지면 · 중앙 견제", 88, 112, 0, 4)
    _war_floor(L, "S3 floor", "S3", -6, 6, 88, 112, 0)
    L.rails_both("S3", "S3", 88, 112, 0)
    L.cp(4, 90)
    for z, ph in zip((95.0, 106.0), V1["screen_phase"]):
        L.device(id=f"S3_screen_{int(z)}", beat="S3", type="rookscreen", pos=(0, 0, z), yaw=0, length=6.0, depth=2.0,
                 height=3.2, timing=Motion("translate", distance=3.0, period=V1["screen_period"]).shift(ph))

    # R3: the banner bridge, 6 m wide, two pendulums ------------------------------------------------------------
    h3 = V1["r3_half"]
    L.beat("R3", "깃발 다리", 112, 132, 0, None)
    _war_floor(L, "R3 bridge", "R3", -h3, h3, 112, 132, 0)
    for z, ph in zip((118.0, 126.0), V1["r3_pend_phase"]):
        L.device(id=f"R3_pendulum_{int(z)}", beat="R3", type="pendulum", pos=(0, 0, z), yaw=0, timing=Pendulum(ph))
        L.wall(f"R3 frame post W {int(z)}", "R3", -7.8, -7, -3, 8.3, z - 0.4, z + 0.4, mat="EG_Teal")
        L.wall(f"R3 frame post E {int(z)}", "R3", 7, 7.8, -3, 8.3, z - 0.4, z + 0.4, mat="EG_Teal")
        L.wall(f"R3 frame beam {int(z)}", "R3", -7.8, 7.8, 7.8, 8.3, z - 0.4, z + 0.4, mat="EG_Gold")

    # R4: the lift tiles up to y 3, a side gust over the top row --------------------------------------------------
    L.beat("R4", "승강 지도함 · 옆바람", 132, 144, 0, 5)
    _war_floor(L, "R4 floor", "R4", -6, 6, 132, 136, 0)
    L.rails_both("R4", "R4", 132, 136, 0)
    L.cp(5, 133)
    L.device(id="R4_tiles_W", beat="R4", type="lifttiles", pos=(-3.35, 0, 139.6), yaw=0, phase_s=0.0)
    L.device(id="R4_tiles_E", beat="R4", type="lifttiles", pos=(3.35, 0, 139.6), yaw=0, phase_s=3.0)
    L.wall("R4 upper front", "R4", -6, 6, -2, U, 143, 144, top_walk=True)
    north_row = 139.6 + 1.575
    for side, ph in zip((-1, 1), V1["vent_phase"]):
        # 15 on a stone bracket outside the way, blowing across the top of its set's north row, toward the middle
        L.device(id=f"R4_vent_{'W' if side < 0 else 'E'}", beat="R4", type="vent", pos=(side * 6.85, U, north_row),
                 yaw=90 if side < 0 else 270, timing=AirVent(ph))
        L.wall(f"R4 vent bracket {'W' if side < 0 else 'E'}", "R4", min(side * 6.7, side * 8.2),
               max(side * 6.7, side * 8.2), -2, U, north_row - 1.6, north_row + 1.6, mat="EG_Stone")

    # R5: two belts run north through a pair of falling pieces landing on them, half a cycle apart. A pawn waits on
    # the floor, steps on when its side has just struck and rides through at 4 m/s; a late step carries it into
    # the next shock. The seam between the belts is under both pieces (a strike every 2.3 s): no way through there.
    zc = 161.0
    L.beat("R5", "벨트 · 낙하 말", 144, 172.6, U, None)
    _war_floor(L, "R5 floor 1", "R5", -6, 6, 144, zc - 5.0, U)
    for x in (-3.0, 3.0):
        L.device(id=f"R5_belt_{'W' if x < 0 else 'E'}", beat="R5", type="conveyor", pos=(x, U - 0.4, zc), yaw=180,
                 width=6.0, length=10.0, speed=V1["belt_speed"])
    L.box("R5 conveyor bed", "R5", -6, 6, U - 2, U - 1, zc - 5.0, zc + 5.0, "EG_Walnut")
    _war_floor(L, "R5 floor 2", "R5", -6, 6, zc + 5.0, 172.6, U)
    L.rails_both("R5", "R5", 144, 172.6, U)
    for x, ph in zip((-3.0, 3.0), V1["piece_phase"]):
        L.device(id=f"R5_piece_{'W' if x < 0 else 'E'}", beat="R5", type="fallingpiece", pos=(x, U, zc), yaw=0,
                 timing=FallingPiece(ph))
    L.cp(6, 170, U)

    # R5b: three crates hanging on chains over the drop, each hop opening in turn ---------------------------------
    L.beat("R5b", "사슬 상자 징검다리", 172.6, 187.4, U, 6)
    for i, (z, ph) in enumerate(((175.5, 0.0), (180.0, 0.5), (184.5, 0.0))):
        L.device(id=f"R5b_crate_{i + 1}", beat="R5b", type="crate", pos=(0, U, z), yaw=0, half_x=3.0, half_z=1.5,
                 timing=Motion("translate", distance=V1["crate_amp"], period=V1["crate_period"], phase=ph))

    # R6: the spinning disc, the second map table ------------------------------------------------------------------
    L.beat("R6", "회전 원판", 187.4, 204, U, 7)
    _war_floor(L, "R6 ledge", "R6", -6, 6, 187.4, 190, U)
    L.rails_both("R6 ledge", "R6", 187.4, 190, U)
    L.device(id="R6_disc", beat="R6", type="disc", pos=(0, U - 0.4, 194), yaw=0, diameter=8.0)
    _war_floor(L, "R6 landing", "R6", -6, 6, 198, 204, U)
    L.rails_both("R6 landing", "R6", 198, 204, U)
    L.wall("R6 map table", "R6", 6.5, 8.5, U - 1, U + 1.3, 198, 203.5, mat="EG_Walnut")
    L.cp(7, 199.5, U)

    # S4: the last crossing: two folding bridges, a rook screen on the far side -----------------------------------
    s4_start, bridge_half = 204.0, 7.05
    s4_end = s4_start + 2 * bridge_half
    L.beat("S4", "마지막 작전교 (결정적 건넘 2)", s4_start, s4_end + 2.5, U, None)
    for x, ph, tag in ((-3, V1["s4_bridge_phase"][0], "W"), (3, V1["s4_bridge_phase"][1], "E")):
        L.device(id=f"S4_bridge_{tag}", beat="S4", type="foldingbridge", pos=(x, U, s4_start + bridge_half), yaw=90,
                 timing=FoldingBridge(ph))
    screen_z = s4_end + 1.5
    L.device(id="S4_screen", beat="S4", type="rookscreen", pos=(0, U, screen_z), yaw=0, length=6.0, depth=2.0,
             height=3.2, timing=Motion("translate", distance=3.0, period=V1["s4_screen_period"]).shift(V1["s4_screen_phase"]))
    _war_floor(L, "S4 far floor", "S4", -6, 6, s4_end, screen_z + 1.0, U)
    L.rails_both("S4 far floor", "S4", s4_end, screen_z + 1.0, U)

    _exit(L, screen_z + 1.0, U, runout=12.0)
    return L


LAYOUTS = {"graybox": graybox, "ld_v1": ld_v1}
