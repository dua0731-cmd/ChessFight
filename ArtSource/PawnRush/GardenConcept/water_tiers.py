# The royal water garden's three water tiers and the falls between them (R439), worked out from open-channel
# hydraulics so the water steps down the way real water would. Pure Python (no bpy): water_sample_v*.py builds the
# meshes from it; run it on its own (python water_tiers.py) for the table.
#
# 승규 11-05: "물을 초반 중반 후반 으로 층으로 나누어서 물 높이를 높여 층마다 높이를 나눈 후 물이 떨어지게 수학적으로
# 자연스럽게 계산해". The course climbs from the plaza (floor 0) over the platforms (2.0 .. 4.1) to the lock, the dyke and
# the boats (3.8); the water now climbs with it, in three level pools:
#
#   LATE    the reservoir (beat 6)
#   MIDDLE  the platform lake (beats 3-5); the canal walk (at 0) is its dam
#   EARLY   the plaza moat and the canal's south side (beats 1-2)
#
# R441 (승규 11-05, "층이 더 확실히 보이게 좀 더 높게 설정해주고 발판이 물에 잠기지 않게 해"): the steps made bigger.
# The lake is the one pool that cannot rise: the lowest platform (P1, top 2.35 half way up its bob, +-0.35) has its
# tub's feet 2.4 m under its top, at -0.40 when it is down, and at R439's -0.35 they dipped in. So the lake goes down
# to -0.55 (0.15 m under P1's feet, 0.085 m with the swell's 0.065), and the steps grow at the other two pools: the
# moat to -1.9 (1.35 m under the lake, was 0.85) and the reservoir up to 3.0 (the boats' decks 0.8 m over it, their
# hulls still in it; 3.55 m over the lake, was 2.3).
#
# Water comes into the reservoir (the boathouses' falls), spills south over a spillway in the reservoir's south wall
# west of the quay into the lake, and leaves the lake over two weirs on the canal walk's line (east and west of it)
# into the moat. In a steady state the same discharge Q passes every crest (continuity), which fixes the second
# crest's head from the first's.
#
# Over a broad stone crest the flow passes through critical depth (the most energy-efficient way over):
#     y_c = (q^2 / g)^(1/3),   v_c = sqrt(g y_c),   head over the crest h = 3/2 y_c,   so q = sqrt(g) (2/3 h)^(3/2)
# per metre of crest (q = 1.705 h^1.5 in SI). At the free overfall (the brink) the depth drops to about 0.715 y_c
# (Rouse's end-depth ratio), so the water leaves the brink horizontally at v_b = q / y_b. From there every parcel of
# the falling sheet (the nappe) is a projectile:
#     s(t) = v_b t (out from the brink),   y(t) = y_brink - g t^2 / 2,   speed v(t) = sqrt(v_b^2 + (g t)^2)
# and by continuity the sheet thins as it speeds up: thickness d(t) = q / v(t). It reaches a pool H below the brink at
# t_H = sqrt(2 H / g), x_H = v_b t_H out from the wall. Upstream of the crest the surface draws down smoothly from the
# pool's level to the critical depth (a cubic easing over a few heads, as measured drawdown curves look).
import math

G = 9.81
LATE, MIDDLE, EARLY = 3.0, -0.55, -1.9    # R441 (R439: 1.95, -0.35, -1.2)

# R441: the platforms stay out of the lake - P1's tub (the lowest; RoyalWaterGardenBuilder.PlatformTop(0) is its top
# half way up its bob, PlatformBob either way; the tub 2.4 m tall, platforms_sample_v3.tub) at the bottom of its bob,
# over the lake's highest swell (PR_GardenWater _WaveHeight).
PLATFORM_BOB, P1_TOP_MID, TUB_HEIGHT, SWELL = .35, 2.35, 2.4, .065
P1_FEET_LOW = P1_TOP_MID - PLATFORM_BOB - TUB_HEIGHT
assert MIDDLE + SWELL < P1_FEET_LOW, "the lake (%.2f + swell) reaches P1's feet (%.2f)" % (MIDDLE, P1_FEET_LOW)

# Where the crests are (section frame: x east, z north). Each fall: the crest line's west and east ends (x), the
# crest's upstream and downstream faces (z), which way the water goes (+1 north, -1 south), the pools above and below,
# the abutments' and piers' widths and how many bays.
FALLS = {
    # The reservoir's south wall west of the quay (x -26 .. -10.4, z 32.6 .. 33), the water going south into the lake.
    "spillway": dict(x0=-26.0, x1=-10.4, z_up=33.0, z_down=32.6, way=-1, upper=LATE, lower=MIDDLE,
                     abutment=.6, pier=.8, bays=4),
    # The canal walk's line (z -15.5) carried east of the walk to the garden's east wall, the water going south.
    "weir E": dict(x0=7.35, x1=46.5, z_up=-15.2, z_down=-15.8, way=-1, upper=MIDDLE, lower=EARLY,
                   abutment=.6, pier=.8, bays=6),
    # ... and west of the canal's tall west wall to the garden's west wall.
    "weir W": dict(x0=-46.5, x1=-35.6, z_up=-15.2, z_down=-15.8, way=-1, upper=MIDDLE, lower=EARLY,
                   abutment=.6, pier=.8, bays=2),
}
SPILL_HEAD = .20          # the reservoir 0.20 m over the spillway's crest: everything else follows from it
BRINK_RATIO = .715        # Rouse: depth at a free overfall / critical depth
V_REF = 3.0               # the falls' UV v is V_REF x time of flight; the fall shader's _Speed = V_REF moves its
                          # streaks with the water (they stretch as it speeds up, as real ones do)


def bays(f):
    """The bays' (x0, x1) along a crest, piers and abutments between them."""
    span = f["x1"] - f["x0"]
    clear = span - 2 * f["abutment"] - (f["bays"] - 1) * f["pier"]
    w = clear / f["bays"]
    out, x = [], f["x0"] + f["abutment"]
    for _ in range(f["bays"]):
        out.append((x, x + w))
        x += w + f["pier"]
    return out


def crest_length(f):
    return sum(b - a for a, b in bays(f))


def head_for(q):
    """The head over a broad crest that passes q (m^2/s per metre)."""
    y_c = (q * q / G) ** (1 / 3)
    return 1.5 * y_c


def solve():
    """Every fall's hydraulics, with one discharge Q through the system (continuity)."""
    sp = FALLS["spillway"]
    q_sp = math.sqrt(G) * (2 / 3 * SPILL_HEAD) ** 1.5
    Q = q_sp * crest_length(sp)
    weir_length = crest_length(FALLS["weir E"]) + crest_length(FALLS["weir W"])
    q_weir = Q / weir_length
    out = {}
    for name, f in FALLS.items():
        q = q_sp if name == "spillway" else q_weir
        h = head_for(q)
        y_c = (q * q / G) ** (1 / 3)
        y_b = BRINK_RATIO * y_c
        crest = f["upper"] - h
        brink = crest + y_b                      # the falling sheet's top surface at the brink
        H = brink - f["lower"]
        t_H = math.sqrt(2 * H / G)
        v_b = q / y_b
        out[name] = dict(f, q=q, h=h, y_c=y_c, y_b=y_b, v_c=math.sqrt(G * y_c), v_b=v_b, crest=crest, brink=brink,
                         H=H, t_H=t_H, throw=v_b * t_H, v_foot=math.hypot(v_b, G * t_H), d_foot=q / math.hypot(v_b, G * t_H),
                         Q=q * crest_length(f), spans=bays(f))
    return Q, out


def profile(fall, n_up=6, n_fall=18, below=.18, edge=.05):
    """The falling water's top surface across the crest, as (s, y, t, fall01) from the crest's upstream edge to just
    under the lower pool: s metres downstream of that edge, y height, t seconds of travel (0 at the brink, negative
    upstream), fall01 0 at the brink -> 1 at the lower pool. The crest is the wall's width plus its coping's overhang
    (`edge`) each side. The drawdown is drawn from the edge, where the pool's sheet stops (1 cm under it, so the two
    never lie in one plane); upstream of it the pool is level to the eye."""
    w = abs(fall["z_up"] - fall["z_down"]) + 2 * edge
    pts = []
    # Drawdown to the brink, eased (smoothstep) through critical depth.
    start = 0.0
    y0, y1 = fall["upper"] - .01, fall["brink"]
    t = 0.0
    prev = None
    raw = []
    for i in range(n_up + 1):
        u = i / n_up
        s = start + (w - start) * u
        e = u * u * (3 - 2 * u)
        y = y0 + (y1 - y0) * e
        depth = max(y - fall["crest"], fall["y_b"])
        v = fall["q"] / depth                    # mean speed at this depth (continuity)
        raw.append((s, y, v))
    # Travel time upstream of the brink, backwards from it.
    times = [0.0] * len(raw)
    for i in range(len(raw) - 2, -1, -1):
        ds = raw[i + 1][0] - raw[i][0]
        times[i] = times[i + 1] - ds / (.5 * (raw[i][2] + raw[i + 1][2]))
    for (s, y, _), tt in zip(raw, times):
        pts.append((s, y, tt, 0.0))
    # The nappe: a projectile from the brink, on until it is `below` under the lower pool.
    t_end = math.sqrt(2 * (fall["H"] + below) / G)
    for i in range(1, n_fall + 1):
        u = i / n_fall
        tt = t_end * u ** .8                     # finer near the brink where it curves most
        s = w + fall["v_b"] * tt
        y = fall["brink"] - .5 * G * tt * tt
        pts.append((s, y, tt, min(1.0, (fall["brink"] - y) / max(fall["H"], 1e-6))))
    return pts


if __name__ == "__main__":
    Q, falls = solve()
    print("Tiers: late %.2f, middle %.2f, early %.2f m;  discharge Q = %.3f m^3/s through every crest" % (LATE, MIDDLE, EARLY, Q))
    print("P1's feet at the bottom of its bob %.2f m: %.3f m over the lake's highest swell" % (P1_FEET_LOW, P1_FEET_LOW - MIDDLE - SWELL))
    for name, f in falls.items():
        print("%-9s crest %5.2f m long  q %.4f m2/s  head %.3f  y_c %.3f  y_b %.3f  v_b %.2f m/s  crest %.3f  drop %.2f m"
              "  fall %.3f s  throw %.2f m  foot %.2f m/s  sheet %.1f cm"
              % (name, crest_length(f), f["q"], f["h"], f["y_c"], f["y_b"], f["v_b"], f["crest"], f["H"], f["t_H"],
                 f["throw"], f["v_foot"], 100 * f["d_foot"]))
