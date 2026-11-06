# The garden geysers' water, computed (R426). The same model, line for line, as Assets/Scripts/PawnRush/GardenJet.cs;
# this copy draws it in Blender (jet_preview.py) so its shape and motion can be checked before Unity.
#
# Time s = seconds since the geyser began to warn (RoyalWaterGardenRules: warn 0.8 s, jet 1.0 s, cycle 5 s);
# t = s - 0.8 = seconds since the valve opened (it closes at t = 1). Heights are local: 0 = the geyser group's origin
# (2 cm over the floor), the nozzle's mouth at JET_Z0.
#
#   Water leaves the nozzle at v0 = sqrt(2 g H) (H = 5 m: GardenGeyser.height). A parcel that left a seconds ago is at
#   z(a) = v0 a - g a^2 / 2 and rises at v(a) = v0 - g a, so the column's front climbs as a ballistic head and the
#   whole column stands only after a_top = v0 / g = 1.01 s.
#   Continuity (the same flow through every cross-section, A v = const) widens the column as the water slows:
#   r = (r0 + k z) sqrt(v0 / v); k z is the aerated jet's spread, the radius capped at R_CAP near the top.
#   Past the top each parcel spreads outward at U_S and falls freely: rho = R_CAP + U_S dt, z = H - g dt^2 / 2 - an
#   umbrella that lands A_LAND = 2 a_top after the parcel left, R_CAP + U_S a_top from the axis.
#   When the valve shuts the last parcel follows the same law: the column's foot leaves the nozzle and climbs.
#   Droplets are ballistic: shed from the rising head, broken off the umbrella, splashed up where it lands, and
#   spat out of the nozzle while the geyser warns.
import math

G = 9.81
H = 5.0
V0 = math.sqrt(2 * G * H)
A_TOP = V0 / G
A_LAND = 2 * A_TOP
T_WARN, T_ON = .8, 1.0
R0, K_AIR, V_MIN, R_CAP = .26, .05, 1.3, .62
U_S = .45
JET_Z0 = .025          # the nozzle's mouth
FLOOR_Z = -.02         # the floor (the group sits 2 cm over it)
POOL_Z = -.032         # the basin's water (1.2 cm under the floor)
N_SEG = 20
END = 1.8 + A_LAND + .45


def column_radius(z, v):
    return min(R_CAP, (R0 + K_AIR * max(z, 0.0)) * math.sqrt(V0 / max(v, V_MIN)))


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def hash01(*keys):
    """The same integer hash as GardenJet.Hash01."""
    h = 2166136261
    for k in keys:
        h ^= (int(k) & 0xffffffff)
        h = (h * 16777619) & 0xffffffff
        h ^= h >> 13
        h = (h * 1274126177) & 0xffffffff
    h ^= h >> 16
    return (h & 0xffffff) / float(0x1000000)


class Builder:
    """Surfaces of revolution and droplets: verts (x, y, z with z up), faces, and per-vertex (aeration, flow) data."""

    def __init__(self):
        self.verts, self.faces, self.data = [], [], []

    def strip(self, prof, flows, gs, stream=1.0):
        """prof: [(rho, z)] along the surface; one ring of N_SEG + 1 verts each, the seam on -x."""
        if len(prof) < 2:
            return
        base = len(self.verts)
        for k, ((rho, z), flow, g) in enumerate(zip(prof, flows, gs)):
            for s in range(N_SEG + 1):
                ph = math.pi + 2 * math.pi * s / N_SEG
                self.verts.append((rho * math.cos(ph), rho * math.sin(ph), z))
                self.data.append((g, flow, s / N_SEG, stream))
        row = N_SEG + 1
        for k in range(len(prof) - 1):
            for s in range(N_SEG):
                a = base + k * row + s
                self.faces.append((a, a + 1, a + row + 1, a + row))

    def droplet(self, p, vel, size, g):
        sp = math.sqrt(vel[0] ** 2 + vel[1] ** 2 + vel[2] ** 2)
        d = (vel[0] / sp, vel[1] / sp, vel[2] / sp) if sp > 1e-4 else (0.0, 0.0, 1.0)
        stretch = 1 + .045 * sp
        # Two axes across the motion.
        ax = (-d[1], d[0], 0.0) if abs(d[2]) < .95 else (1.0, 0.0, 0.0)
        n = math.sqrt(ax[0] ** 2 + ax[1] ** 2 + ax[2] ** 2)
        ax = (ax[0] / n, ax[1] / n, ax[2] / n)
        ay = (d[1] * ax[2] - d[2] * ax[1], d[2] * ax[0] - d[0] * ax[2], d[0] * ax[1] - d[1] * ax[0])
        base = len(self.verts)
        tips = [tuple(p[i] + d[i] * size * stretch for i in range(3)), tuple(p[i] - d[i] * size * .8 for i in range(3))]
        ring = [tuple(p[i] + (ax[i] * math.cos(q) + ay[i] * math.sin(q)) * size for i in range(3))
                for q in (0, math.pi / 2, math.pi, 3 * math.pi / 2)]
        self.verts += tips + ring
        self.data += [(g, 0.0, 0.0, 1.0)] * 6
        for q in range(4):
            a, b = base + 2 + q, base + 2 + (q + 1) % 4
            self.faces.append((base, a, b))
            self.faces.append((base + 1, b, a))


def build(s, order=0, cycle=0, pool=True):
    """Everything the geyser shows `s` seconds after it began to warn."""
    b = Builder()
    if s < 0 or s > END:
        return b
    t = s - T_WARN
    if t < 0:
        warn(b, s, pool)
    else:
        column(b, t)
        umbrella(b, t)
        boil(b, t)
        landing(b, t)
    droplets(b, t, order, cycle)
    return b


def warn(b, tw, pool):
    w = tw / T_WARN
    hb = .02 + .1 * w * w + .025 * math.sin(31 * tw) * w
    rb = .3 + .15 * w
    z0 = JET_Z0 - .01
    prof = [(rb * math.cos(q), z0 + hb * math.sin(q)) for q in [i * math.pi / 2 / 6 for i in range(7)]]
    b.strip(prof, [tw * 2.0] * len(prof), [.5] * len(prof))
    if pool:
        for k in range(3):
            p = (tw * 1.6 + k / 3.0) % 1.0
            rho = .35 + .6 * p
            g = .62 + .35 * p
            b.strip([(rho - .03, POOL_Z + .003), (rho + .03, POOL_Z + .003)], [k, k], [g, g])


def column(b, t):
    a_lo, a_hi = max(0.0, t - T_ON), min(t, A_TOP)
    if a_hi - a_lo < 1e-3:
        return
    prof, flows, gs = [], [], []
    if a_lo <= 0:
        prof.append((R0 * .9, JET_Z0 - .02)); flows.append(t * V0 * .5); gs.append(.5)
    K = 24
    for k in range(K):
        a = a_lo + (a_hi - a_lo) * k / (K - 1)
        z = V0 * a - .5 * G * a * a
        prof.append((column_radius(z, V0 - G * a), JET_Z0 + z))
        flows.append((t - a) * V0 * .5)
        gs.append(.5 + .3 * a / A_TOP)
    if a_lo > 0:
        # The valve has shut: the column's foot is a rounded tail climbing after it.
        rho, z = prof[0]
        prof = [(0.0, z - .3 * rho), (rho * .7, z - .14 * rho)] + prof
        flows = [flows[0]] * 2 + flows
        gs = [gs[0]] * 2 + gs
    rho, z = prof[-1]
    f, g = flows[-1], gs[-1]
    if t < A_TOP:
        # The starting jet's head: a mushroom wider than the column.
        cap = [(rho * 1.12, z + .12 * rho), (rho * .95, z + .35 * rho), (rho * .6, z + .55 * rho), (0.0, z + .62 * rho)]
    else:
        # Water still reaching the top: the crown it turns over at.
        cap = [(rho * .7, z + .25), (0.0, z + .32)]
    prof += cap
    flows += [f] * len(cap)
    gs += [max(g, .78)] * len(cap)
    b.strip(prof, flows, gs)


def umbrella(b, t):
    lo, hi = max(A_TOP, t - T_ON), min(t, A_LAND)
    if hi - lo < 1e-3:
        return
    prof, flows, gs = [], [], []
    K = 18
    for k in range(K):
        a = lo + (hi - lo) * k / (K - 1)
        dt = a - A_TOP
        prof.append((R_CAP + U_S * dt, JET_Z0 + H - .5 * G * dt * dt))
        flows.append((t - a) * V0 * .5)
        gs.append(.6 + .36 * dt / A_TOP)
    b.strip(prof, flows, gs, 0.0)  # a curtain: it thins into strands as it falls (GC_jet, vertex alpha 0)


def boil(b, t):
    if t > T_ON + .45:
        return
    w = 1.0 if t < T_ON else 1 - (t - T_ON) / .45
    hb = (.4 * math.exp(-3 * t) + .14) * w
    rb = .85 * (.75 + .25 * w)
    prof = [(rb * math.cos(q), FLOOR_Z + .01 + hb * math.sin(q)) for q in [i * math.pi / 2 / 6 for i in range(7)]]
    b.strip(prof, [t * 2.0] * len(prof), [.74] * len(prof))


def landing(b, t):
    w = smooth((t - (A_LAND - .05)) / .1) * (1 - smooth((t - (A_LAND + T_ON)) / .4))
    if w <= .01:
        return
    rl = R_CAP + U_S * A_TOP
    prof = [(rl - .3, FLOOR_Z + .005), (rl - .15, FLOOR_Z + .12 * w), (rl + .1, FLOOR_Z + .14 * w), (rl + .3, FLOOR_Z + .005)]
    b.strip(prof, [t * 1.5] * 4, [.76] * 4)


def droplets(b, t, order, cycle):
    def fly(j, k, tb, p0, v0, size, g=.66):
        age = t - tb
        if age < 0:
            return
        p = (p0[0] + v0[0] * age, p0[1] + v0[1] * age, p0[2] + v0[2] * age - .5 * G * age * age)
        if p[2] < FLOOR_Z:
            return
        b.droplet(p, (v0[0], v0[1], v0[2] - G * age), size, g)

    for j in range(60):        # shed from the rising head
        r = [hash01(j, 1, cycle, order, q) for q in range(5)]
        tb = .06 + r[0] * (min(T_ON, A_TOP) - .06)
        zf, vf = V0 * tb - .5 * G * tb * tb, V0 - G * tb
        rho = column_radius(zf, vf) * .85
        ph = r[1] * 2 * math.pi
        u = .5 + 1.4 * r[3]
        fly(j, 1, tb, (rho * math.cos(ph), rho * math.sin(ph), JET_Z0 + zf),
            (u * math.cos(ph), u * math.sin(ph), vf * (.55 + .45 * r[2])), .035 + .04 * r[4])
    for j in range(50):        # broken off the umbrella
        r = [hash01(j, 2, cycle, order, q) for q in range(5)]
        a = A_TOP + r[0] * A_TOP * .85
        tb = r[2] * T_ON + a
        dt = a - A_TOP
        rho = R_CAP + U_S * dt
        ph = r[1] * 2 * math.pi
        u = U_S + .2 + .7 * r[3]
        fly(j, 2, tb, (rho * math.cos(ph), rho * math.sin(ph), JET_Z0 + H - .5 * G * dt * dt),
            (u * math.cos(ph), u * math.sin(ph), -G * dt * (1 + .1 * r[4])), .03 + .035 * r[4])
    rl = R_CAP + U_S * A_TOP
    for j in range(50):        # splashed up where the umbrella lands
        r = [hash01(j, 3, cycle, order, q) for q in range(5)]
        tb = r[0] * T_ON + A_LAND
        ph = r[1] * 2 * math.pi
        rho = rl * (.9 + .2 * r[2])
        u = .3 + 1.0 * r[3]
        fly(j, 3, tb, (rho * math.cos(ph), rho * math.sin(ph), FLOOR_Z + .05),
            (u * math.cos(ph), u * math.sin(ph), .9 + 1.6 * r[4]), .03 + .03 * r[1])
    for j in range(24):        # spat out of the nozzle while it warns
        r = [hash01(j, 4, cycle, order, q) for q in range(5)]
        tb = -.5 + r[0] * .48
        ph = r[1] * 2 * math.pi
        u = .1 + .5 * r[3]
        fly(j, 4, tb, (.12 * math.cos(ph), .12 * math.sin(ph), JET_Z0),
            (u * math.cos(ph), u * math.sin(ph), 1.2 + 2.2 * r[2]), .03 + .025 * r[4])
