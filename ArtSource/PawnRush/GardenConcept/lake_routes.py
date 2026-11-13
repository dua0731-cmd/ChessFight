# R486: the lake boats' routes (GardenLakeBoat.Routes) and their check against everything in the middle lake (R487: two
# routes in the wide waters, two boats on each).
#
# 승규 11-06 (a Unity picture of the platform lake): "총 3개가 나올 수 있게 하고 각자 택스쳐가 다르게 해줘 중반 구간 물
# 쪽 전체를 누릴 수 있게 해주고 물이랑 상호 작용 시켜". The lake (-0.55) is three waters a boat cannot pass between: the
# platforms P1..P8 stand 1.2 m apart from the west landing to the east landing, and the shortcut's posts and pads run
# 2.2 .. 2.5 m apart from P4 to the dyke. So one boat sails each:
#   south   the south lake (under the platforms, over the canal walk and the weirs) and the east channel through the
#           water gate (R485), round a loop at the channel's north end
#   west    the north-west water round the island's north side and the west channel down to the weirs
#   east    the water between the shortcut, P5 .. P8, the east landing, the lock and the dyke
# Each route is a closed Catmull-Rom spline through its control points (section x, z). This samples each, sweeps the
# boat's hull (4.8 x 1.8 m, the royal boat at 0.75) along it and reports how near it comes to every obstacle (plan
# shapes from RoyalWaterGardenBuilder*.cs and watergate_sample_v1.py) and its tightest turn; it fails if a hull corner
# comes within MARGIN of one. It also draws a plan (lake_routes_plan.png) and prints the routes as C#.
#
#   blender -b --python lake_routes.py        (or any Python 3 with numpy; the plan needs Blender's image writer)
import math
import os
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
MARGIN = .35
GATE_MARGIN = .12                                # the water gate's passage is the one tight place (2.75 m for 1.8)
HALF_LEN, HALF_BEAM = 2.4, .9

# ------------------------------------------------------------------ obstacles (section x, z)
CIRCLES = []                                     # (x, z, r, name)
for i in range(8):                               # the platforms' tubs (6 m, the dressing's rim a little over)
    CIRCLES.append((-21 + 6 * i, 2.5 if i % 2 == 0 else 6.5, 3.15, "P%d" % (i + 1)))
CIRCLES.append((-18, 16, 5.3, "island"))
P4 = np.array([-3., 6.5])
ALONG = np.array([19.4 + 3, 24 - 6.5])
ALONG /= np.linalg.norm(ALONG)
for s, r, n in ((5.8, .7, "K1"), (9.3, .7, "K2"), (12.8, .7, "K3"), (16.9, 1.4, "K4"), (21.2, .9, "L1"), (25.1, .9, "L2")):
    c = P4 + ALONG * s
    CIRCLES.append((c[0], c[1], r, n))
BOXES = [                                        # (x0, x1, z0, z1, name)
    (-35.0, 7.3, -18.8, -12.2, "canal walk"),
    (-35.6, -35.0, -18.8, 8.3, "canal west wall"),
    (-35.0, -25.2, -12.5, 8.3, "ramp + west landing"),
    (25.2, 35.0, -.3, 10.0, "east landing"),
    (26.4, 27.0, 10.0, 24.0, "lock wall W"),
    (35.0, 35.6, -.3, 32.6, "lock wall E"),
    (27.0, 35.0, 10.0, 24.0, "lock"),
    (8.0, 36.0, 24.0, 33.0, "dyke"),
    (-26.0, 35.0, 32.6, 33.0, "reservoir wall"),
    (-46.0, -26.0, 33.0, 48.6, "terrace W"),
    (26.0, 44.0, 33.0, 48.6, "terrace E"),
    (45.8, 55.0, -35.1, 44.4, "bank E"),
    (-55.0, -45.8, -35.1, 44.4, "bank W"),
    (-46.0, 46.0, -40.0, -15.5, "the weirs (the moat beyond)"),
    # the water gate (R485): its footings and the gate over them, the passage x 41.2 .. 43.96 between
    (35.0, 41.15, 3.98, 18.33, "gate W"),
    (44.01, 52.0, 3.98, 18.33, "gate E"),
]

# ------------------------------------------------------------------ the routes
# R487 (승규 "2개씩 나와야 하고 텀이 길어 넓은 쪽으로 와야 해", "총 6척이 아니라 4척인데 그 주기 가 짧고 플레이어들이 볼
# 수 있어야 돼"): four boats, two on each of two short routes half a round apart, both in the wide waters the way runs
# beside - the south lake (seen from the canal walk, the ramp and the platforms) and the water north of the platforms
# east of the island (seen from the platforms and the shortcut). R486's east channel through the gate, west channel
# and the small east water are left out. A route two boats share must not cross itself (CROSS: its lanes at least
# this far apart).
CROSS = 3.4
ROUTES = {
    # west along the south lake under the platforms, round its west end, back east by the canal walk and the weirs,
    # round its east end south of the east landing
    "south": [(26.0, -3.9), (14.0, -3.8), (2.0, -4.0), (-10.0, -3.9), (-17.0, -4.0), (-21.4, -4.9), (-23.2, -7.2),
              (-21.4, -9.6), (-14.0, -10.0), (-2.0, -9.9), (10.0, -11.0), (22.0, -11.2), (29.5, -10.2), (32.2, -7.2),
              (30.6, -4.6)],
    # north of the platforms: east along the reservoir wall, down by the shortcut, west under it past P4 and up
    # between it and the island, west over the island's north, round and east again
    "north": [(-25.0, 29.7), (-12.0, 29.6), (-2.0, 29.2), (2.6, 26.6), (4.0, 22.0), (3.6, 17.0), (1.0, 13.0),
              (-4.0, 12.2), (-8.2, 14.6), (-9.6, 19.6), (-14.0, 23.4), (-21.0, 23.8), (-26.4, 24.3), (-29.2, 25.9),
              (-28.9, 28.6)],
}


def sample(ctrl, per=40):
    R = np.array(ctrl, float)
    n = len(R)
    pts = []
    for k in range(n):
        a, b, c, d = R[(k - 1) % n], R[k], R[(k + 1) % n], R[(k + 2) % n]
        for i in range(per):
            t = i / per
            pts.append(.5 * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t ** 3))
    return np.array(pts)


def hull_points(p, d):
    nrm = np.array([-d[1], d[0]])
    out = [p + d * HALF_LEN, p - d * HALF_LEN]
    for s1 in (-1, -.5, 0, .5, 1):
        for s2 in (-1, 1):
            out.append(p + d * (HALF_LEN - .8) * s1 + nrm * HALF_BEAM * s2)
    return out


def clearance(q):
    best = (1e9, "")
    for x, z, r, n in CIRCLES:
        best = min(best, (math.hypot(q[0] - x, q[1] - z) - r, n))
    for x0, x1, z0, z1, n in BOXES:
        dx = max(x0 - q[0], 0, q[0] - x1)
        dz = max(z0 - q[1], 0, q[1] - z1)
        inside = x0 < q[0] < x1 and z0 < q[1] < z1
        best = min(best, (-1 if inside else math.hypot(dx, dz), n))
    return best


def check(name, ctrl):
    P = sample(ctrl)
    S = len(P)
    seg = np.linalg.norm(np.diff(np.vstack([P, P[:1]]), axis=0), axis=1)
    L = seg.sum()
    m = max(1, int(round(.8 / (L / S))))
    worst, rmin, ok = (1e9, "", None), 1e9, True
    for i in range(S):
        d = P[(i + m) % S] - P[(i - m) % S]
        d /= np.linalg.norm(d)
        for q in hull_points(P[i], d):
            c, n = clearance(q)
            if c < (GATE_MARGIN if n.startswith("gate") else MARGIN):
                ok = False
            if c < worst[0]:
                worst = (c, n, tuple(np.round(P[i], 1)))
        a, b, c = P[(i - m) % S], P[i], P[(i + m) % S]
        v1, v2 = b - a, c - b
        ang = abs(math.atan2(v1[0] * v2[1] - v1[1] * v2[0], v1 @ v2))
        if ang > 1e-6:
            rmin = min(rmin, (np.linalg.norm(v1) + np.linalg.norm(v2)) / 2 / ang)
    # its own lanes: samples more than 12 m apart along it must keep CROSS apart (two boats share it)
    cum = np.r_[0, np.cumsum(seg)][:S]
    lanes = 1e9
    for i in range(0, S, 2):
        d = np.linalg.norm(P - P[i], axis=1)
        apart = np.abs(cum - cum[i])
        apart = np.minimum(apart, L - apart)
        far = apart > 12
        if far.any():
            lanes = min(lanes, d[far].min())
    if lanes < CROSS:
        ok = False
    print("ROUTE %-6s %5.1f m  nearest %.2f m (%s at %s)  tightest turn %.1f m  lanes %.1f m apart  %s"
          % (name, L, worst[0], worst[1], worst[2], rmin, lanes, "ok" if ok else "FAIL"))
    return ok, P


def plan(paths, fname):
    """A plan of the lake at 8 px per metre: obstacles grey, the routes in colour."""
    W, H, s, x0, z0 = 800, 440, 8, -50, -18
    img = np.full((H, W, 4), (.15, .35, .55, 1.0))
    xs = x0 + (np.arange(W) + .5) / s
    zs = z0 + (np.arange(H) + .5) / s
    gx, gz = np.meshgrid(xs, zs)
    for x, z, r, n in CIRCLES:
        img[(gx - x) ** 2 + (gz - z) ** 2 < r * r] = (.7, .7, .7, 1)
    for bx0, bx1, bz0, bz1, n in BOXES:
        img[(gx > bx0) & (gx < bx1) & (gz > bz0) & (gz < bz1)] = (.55, .5, .45, 1)
    for (P, col) in paths:
        for q in P:
            i, j = int((q[1] - z0) * s), int((q[0] - x0) * s)
            if 0 <= i < H and 0 <= j < W:
                img[max(0, i - 1):i + 2, max(0, j - 1):j + 2] = col
    try:
        import bpy
        im = bpy.data.images.new("plan", W, H, alpha=True)
        im.pixels = img.ravel()
        im.filepath_raw = os.path.join(HERE, fname)
        im.file_format = "PNG"
        im.save()
    except ImportError:
        pass


if __name__ == "__main__":
    cols = {"south": (1, .85, .2, 1), "north": (1, .35, .35, 1)}
    paths, all_ok = [], True
    for name, ctrl in ROUTES.items():
        ok, P = check(name, ctrl)
        all_ok &= ok
        paths.append((P, cols[name]))
    plan(paths, "lake_routes_plan.png")
    for name, ctrl in ROUTES.items():
        print("        // %s\n        new[] {\n%s },"
              % (name, ",\n".join("            new Vector2(%.2ff, %.2ff)" % c for c in ctrl)))
    print("LAKE ROUTES", "ok" if all_ok else "FAIL")
