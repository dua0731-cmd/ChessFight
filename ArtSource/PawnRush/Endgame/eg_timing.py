"""Safe windows and timing checks for the endgame layouts (pure Python, no Blender).

    python eg_timing.py [ld_v1|graybox] [--json out.json]

A pawn is a capsule 0.7 m wide and 1.0 m high (eg_layout.PAWN_R, PAWN_H). For each crossing it runs a straight
lane at 2.5 m/s (or waits at a pocket and goes), starting at every 0.05 s of one cycle of the devices that meet it.
A start time is safe when no device part touches the capsule on the way. The window table lists, per lane, the
longest run of safe start times; "union" is the time a pawn free to pick its lane has.
"""
import json
import math
import sys

import eg_layout as E

R, H = E.PAWN_R, E.PAWN_H
STEP = 0.05     # start times
SIM = 0.02      # simulation step on the way through


# ---------------------------------------------------------------------------------------------- shapes
def seg_box(x, z, y0, box):
    """Capsule (vertical segment at x, z from y0 + R to y0 + H - R, radius R) against an axis-aligned box."""
    bx0, bx1, by0, by1, bz0, bz1 = box
    dx = max(bx0 - x, 0.0, x - bx1)
    dz = max(bz0 - z, 0.0, z - bz1)
    s0, s1 = y0 + R, y0 + H - R
    dy = max(by0 - s1, 0.0, s0 - by1)
    return dx * dx + dy * dy + dz * dz < R * R


def seg_sphere(x, z, y0, c, r):
    cx, cy, cz = c
    s0, s1 = y0 + R, y0 + H - R
    py = min(max(cy, s0), s1)
    return (cx - x) ** 2 + (cy - py) ** 2 + (cz - z) ** 2 < (R + r) ** 2


def seg_bar(x, z, y0, a, b, half_thick, by0, by1):
    """Capsule against a horizontal bar from a to b (x, z), half_thick wide, between heights by0 and by1."""
    s0, s1 = y0 + R, y0 + H - R
    if by1 < s0 - R or by0 > s1 + R:
        return False
    ax, az = a
    bx, bz = b
    vx, vz = bx - ax, bz - az
    L2 = vx * vx + vz * vz
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((x - ax) * vx + (z - az) * vz) / L2))
    px, pz = ax + t * vx, az + t * vz
    d = math.hypot(x - px, z - pz)
    dy = max(by0 - s1, 0.0, s0 - by1)
    return math.hypot(max(0.0, d - half_thick), dy) < R


def seg_crosses_box2d(p, q, box):
    """Does the segment p -> q (x, z) cross the box footprint (x0, x1, z0, z1)? (Liang-Barsky)"""
    x0, x1, z0, z1 = box
    t0, t1 = 0.0, 1.0
    dx, dz = q[0] - p[0], q[1] - p[1]
    for pv, qv in ((-dx, p[0] - x0), (dx, x1 - p[0]), (-dz, p[1] - z0), (dz, z1 - p[1])):
        if pv == 0:
            if qv < 0:
                return False
        else:
            r = qv / pv
            if pv < 0:
                t0 = max(t0, r)
            else:
                t1 = min(t1, r)
            if t0 > t1:
                return False
    return True


# ---------------------------------------------------------------------------------------------- device parts
def parts(d, t):
    """The moving parts of a device at time t: list of ("box", aabb) / ("sphere", centre, r) / ("bar", a, b, ht, y0, y1)."""
    kind = d["type"]
    px, py, pz = d["pos"]
    if kind == "rookscreen":
        off = d["timing"].value(t)
        hl, hd = d["length"] / 2, d["depth"] / 2
        return [("box", (px + off - hl, px + off + hl, py, py + d["height"], pz - hd, pz + hd))]
    if kind == "sliding":
        m = E.Motion("gate", distance=d["travel"], period=4.5)
        m.shift(d.get("phase_s", 0.0))
        left = E.Motion("gate", distance=d["travel"], period=4.5, phase=m.phase)
        right = E.Motion("gate", distance=d["travel"], period=4.5, phase=E.repeat(m.phase + 0.5, 1.0))
        hw = d["wall_w"] / 2
        lx = px - d["rest"] + left.value(t)
        rx = px + d["rest"] - right.value(t)
        return [("box", (lx - hw, lx + hw, py, py + 3, pz - 1.8 - 1.5, pz - 1.8 + 1.5)),
                ("box", (rx - hw, rx + hw, py, py + 3, pz + 1.8 - 1.5, pz + 1.8 + 1.5))]
    if kind == "clockgates":
        out = []
        for sx, ph in ((-1, 0.0), (1, 0.5)):
            m = E.Motion("gate", distance=4.1, period=5.0, phase=ph).shift(d.get("phase_s", 0.0))
            lift = m.value(t)
            cx = px + sx * 2.08
            out.append(("box", (cx - 1.9, cx + 1.9, py + lift, py + lift + 3.2, pz - 0.3, pz + 0.3)))
        for x in (-4.15, 0.0, 4.15):   # pillars: fixed, but they shape the doorways
            out.append(("box", (px + x - 0.15, px + x + 0.15, py, py + 5.6, pz - 0.5, pz + 0.5)))
        return out
    if kind == "pendulum":
        lat, h = d["timing"].ball(t)
        return [("sphere", (px + lat, py + h, pz), E.Pendulum.ball_r)]
    if kind == "spinbar":
        ang = math.radians(d["timing"].value(t))
        hl = d["length"] / 2
        ca, sa = math.cos(ang), math.sin(ang)
        # Unity: positive degrees about +y turn +x toward -z (clockwise seen from above)
        a = (px - hl * ca, pz + hl * sa)
        b = (px + hl * ca, pz - hl * sa)
        return [("bar", a, b, d["thick"] / 2, py + d["y0"], py + d["y1"]),
                ("sphere", (px, py + 0.45, pz), 0.425)]
    return []


def touches(plist, x, z, y0):
    for p in plist:
        if p[0] == "box" and seg_box(x, z, y0, p[1]):
            return True
        if p[0] == "sphere" and seg_sphere(x, z, y0, p[1], p[2]):
            return True
        if p[0] == "bar" and seg_bar(x, z, y0, p[1], p[2], p[3], p[4], p[5]):
            return True
    return False


# ---------------------------------------------------------------------------------------------- windows
def windows_from_flags(flags, step):
    """Contiguous safe runs (circular) in seconds: list of (start, length), longest first."""
    n = len(flags)
    if all(flags):
        return [(0.0, n * step)]
    if not any(flags):
        return []
    start = flags.index(False)
    runs, cur = [], None
    for k in range(1, n + 1):
        i = (start + k) % n
        if flags[i]:
            if cur is None:
                cur = [i, 0]
            cur[1] += 1
        elif cur is not None:
            runs.append((round(cur[0] * step, 2), round(cur[1] * step, 2)))
            cur = None
    if cur is not None:
        runs.append((round(cur[0] * step, 2), round(cur[1] * step, 2)))
    return sorted(runs, key=lambda r: -r[1])


def lane_run(devs, x, z0, z1, y0, speed, period, wait_free=True):
    """Safe flags for start times 0..period: the pawn runs from z0 to z1 along lane x."""
    n = int(round(period / STEP))
    flags = []
    dist = z1 - z0
    steps = max(1, int(math.ceil(dist / speed / SIM)))
    for i in range(n):
        t0 = i * STEP
        ok = True
        for k in range(steps + 1):
            t = t0 + k * SIM
            z = z0 + min(dist, speed * k * SIM)
            plist = []
            for d in devs:
                plist += parts(d, t)
            if touches(plist, x, z, y0):
                ok = False
                break
        flags.append(ok)
    return flags


def lanes_table(devs, lanes, z0, z1, y0, period, speed=E.RUN):
    rows, union = [], [False] * int(round(period / STEP))
    for x in lanes:
        f = lane_run(devs, x, z0, z1, y0, speed, period)
        union = [u or v for u, v in zip(union, f)]
        w = windows_from_flags(f, STEP)
        rows.append(dict(lane=x, longest=w[0][1] if w else 0.0, windows=w[:3], safe_share=round(sum(f) / len(f), 2)))
    uw = windows_from_flags(union, STEP)
    return dict(period=period, speed=speed, z=(z0, z1), lanes=rows, union_longest=uw[0][1] if uw else 0.0,
                union_share=round(sum(union) / len(union), 2))


def route_flags(devs, points, y0, speed, period, extra=None):
    """Safe flags for start times over `period`: the pawn walks the polyline `points` (x, z) at `speed`.
    `extra(t, x, z)` may veto a moment (e.g. standing on a folding panel that is not flat)."""
    n = int(round(period / STEP))
    segs = []
    for (ax, az), (bx, bz) in zip(points, points[1:]):
        segs.append((ax, az, bx, bz, math.hypot(bx - ax, bz - az)))
    total = sum(s[4] for s in segs)
    steps = int(math.ceil(total / speed / SIM))
    path = []
    for k in range(steps + 1):
        s = min(total, speed * k * SIM)
        for ax, az, bx, bz, ln in segs:
            if s <= ln or (ax, az, bx, bz, ln) == segs[-1]:
                u = 0.0 if ln == 0 else min(1.0, s / ln)
                path.append((k * SIM, ax + (bx - ax) * u, az + (bz - az) * u))
                break
            s -= ln
    flags = []
    for i in range(n):
        t0, ok = i * STEP, True
        for dt, x, z in path:
            t = t0 + dt
            plist = []
            for d in devs:
                plist += parts(d, t)
            if touches(plist, x, z, y0) or (extra is not None and not extra(t, x, z)):
                ok = False
                break
        flags.append(ok)
    return flags


def route_table(devs, routes, y0, period, speed=E.RUN, extra=None):
    rows, union = [], [False] * int(round(period / STEP))
    for name, pts in routes:
        f = route_flags(devs, pts, y0, speed, period, extra)
        union = [u or v for u, v in zip(union, f)]
        w = windows_from_flags(f, STEP)
        rows.append(dict(route=name, longest=w[0][1] if w else 0.0, windows=w[:2], share=round(sum(f) / len(f), 2)))
    uw = windows_from_flags(union, STEP)
    return dict(period=period, routes=rows, union_longest=uw[0][1] if uw else 0.0)


# ---------------------------------------------------------------------------------------------- beat checks
def check_r1(L):
    board, bar = L.device_by_id("R1_board"), L.device_by_id("R1_bar")
    w_bar = math.radians(abs(bar["timing"].dps))
    w_rel = math.radians(abs(bar["timing"].dps - board["timing"].dps))
    hl = bar["length"] / 2
    out = dict(bar_length=bar["length"], board_radius=board["diameter"] / 2,
               bar_reaches_rim=hl >= board["diameter"] / 2,
               world_pass_every=round(math.pi / w_bar, 2), rider_pass_every=round(math.pi / w_rel, 2))
    rows = []
    for r in (2.0, 4.0, 6.0, 8.0):
        v = r * w_rel
        overlap = (2 * R + bar["thick"]) / v
        rows.append(dict(radius=r, bar_speed_rel=round(v, 2), bar_speed_world=round(r * w_bar, 2),
                         in_contact=round(overlap, 2), jump_tolerance=round(E.JUMP_AIR - overlap, 2)))
    out["at_radius"] = rows
    out["tip_speed_world"] = round(hl * w_bar, 2)
    out["crossing_time"] = round(board["diameter"] / E.RUN, 2)
    out["jumps_per_crossing"] = round(board["diameter"] / E.RUN / (math.pi / w_rel), 1)
    return out


def gap_windows(gap_fn, period, limit=E.GAP_MAX):
    n = int(round(period / STEP))
    flags = [gap_fn(i * STEP) <= limit for i in range(n)]
    w = windows_from_flags(flags, STEP)
    return dict(longest=w[0][1] if w else 0.0, windows=w[:2], share=round(sum(flags) / n, 2))


def check_s2(L):
    f = L.device_by_id("S2_ferry")
    m, hz = f["timing"], f["half_z"]
    mid = f["pos"][2]
    south_floor, north_floor = 40.0, 60.4
    out = dict(period=m.period, travel=m.distance, max_speed=round(m.distance * 2 * math.pi / m.period, 2))
    out["board"] = gap_windows(lambda t: (mid + m.value(t) - hz) - south_floor, m.period)
    out["alight"] = gap_windows(lambda t: north_floor - (mid + m.value(t) + hz), m.period)
    out["min_gap"] = round(min(min((mid + m.value(i * STEP) - hz) - south_floor,
                                   north_floor - (mid + m.value(i * STEP) + hz)) for i in range(int(m.period / STEP))), 2)
    try:
        h = L.device_by_id("S2_hurdle")
        v_mid = out["max_speed"]
        overlap = (2 * R + h["thick"]) / v_mid
        out["hurdle"] = dict(height=(h["y0"], h["y1"]), deck_speed_at_bar=v_mid, in_contact_standing=round(overlap, 2),
                             jump_tolerance_standing=round(E.JUMP_AIR - overlap, 2),
                             jump_tolerance_running_at_it=round(E.JUMP_AIR - (2 * R + h["thick"]) / (v_mid + E.RUN), 2),
                             frame_clearance=round(h["y0"] - f["frame_h"], 2))
    except KeyError:
        pass
    return out


def check_r2(L):
    walls = [d for d in L.devices if d["type"] == "sliding"]
    gates = L.device_by_id("R2_gates")
    lanes = [round(-5.5 + 0.5 * i, 2) for i in range(23)]
    out = dict(walls=[])
    for w in walls:
        zw = w["pos"][2]
        out["walls"].append(dict(id=w["id"], **lanes_table([w], lanes, zw - 4.0, zw + 4.0, 0.0, 4.5)))
    if len(walls) > 1:
        # the two pairs one after the other, a pawn free to switch halves between them
        za, zb = walls[0]["pos"][2], walls[1]["pos"][2]
        routes = []
        for a in (-3.0, 3.0):
            for b in (-3.0, 3.0):
                routes.append((f"A {'W' if a < 0 else 'E'} -> B {'W' if b < 0 else 'E'}",
                               [(a, za - 4.0), (a, za + 3.6), (b, zb - 3.6), (b, zb + 4.0)]))
        out["walls_chain"] = route_table(walls, routes, 0.0, 4.5)
    zg = gates["pos"][2]
    glanes = [round(-3.6 + 0.4 * i, 2) for i in range(19)]
    out["gates"] = lanes_table([gates], glanes, zg - 1.2, zg + 1.2, 0.0, 5.0)
    return out


def check_s3(L):
    screens = [d for d in L.devices if d["type"] == "rookscreen" and d["beat"] == "S3"]
    lanes = [round(-5.5 + 0.5 * i, 2) for i in range(23)]
    period = screens[0]["timing"].period
    out = dict(each=[lanes_table([s], lanes, s["pos"][2] - 1.8, s["pos"][2] + 1.8, 0.0, period) for s in screens])
    z0, z1 = screens[0]["pos"][2] - 1.8, screens[-1]["pos"][2] + 1.8
    out["chain_straight_lane"] = lanes_table(screens, lanes, z0, z1, 0.0, period)
    try:
        gates = L.device_by_id("R2_gates")
        zg = gates["pos"][2]
        s1, s2 = screens[0]["pos"][2], screens[-1]["pos"][2]
        routes = []
        for door, dx in (("L", -2.08), ("R", 2.08)):
            for a in (-4.5, 4.5):
                pts = [(dx, zg - 1.0), (dx, zg + 1.0), (a, s1 - 2.6), (a, s1 + 1.8), (-a, s2 - 1.8), (-a, s2 + 1.8)]
                routes.append((f"door {door} -> screen1 {'W' if a < 0 else 'E'} -> screen2 {'E' if a < 0 else 'W'}", pts))
        out["rhythm_from_gate"] = route_table([gates] + screens, routes, 0.0, period)
    except KeyError:
        pass
    return out


def check_r3(L):
    pends = [d for d in L.devices if d["type"] == "pendulum" and d["beat"] == "R3"]
    half = max(fl["x1"] for fl in L.floors if fl["beat"] == "R3")
    lanes = [round(-half + R + 0.25 * i, 2) for i in range(int((2 * (half - R)) / 0.25) + 1)]
    period = pends[0]["timing"].cycle()
    out = dict(bridge_width=2 * half,
               each=[lanes_table([p], lanes, p["pos"][2] - 1.8, p["pos"][2] + 1.8, 0.0, period) for p in pends])
    out["both_straight"] = lanes_table(pends, lanes, pends[0]["pos"][2] - 1.8, pends[-1]["pos"][2] + 1.8, 0.0, period)
    out["both_sprint"] = lanes_table(pends, lanes, pends[0]["pos"][2] - 1.8, pends[-1]["pos"][2] + 1.8, 0.0, period,
                                     speed=E.SPRINT)
    # lanes no ball ever touches (an exploit if any)
    never = []
    for x in lanes:
        hit = False
        for p in pends:
            for i in range(int(period / STEP)):
                t = i * STEP
                for dz in (-1.0, 0.0, 1.0):
                    if touches(parts(p, t), x, p["pos"][2] + dz, 0.0):
                        hit = True
                        break
                if hit:
                    break
            if hit:
                break
        if not hit:
            never.append(x)
    out["lanes_never_hit"] = never
    return out


def check_r4(L):
    sets = [d for d in L.devices if d["type"] == "lifttiles"]
    vents = {d["id"][-1]: d for d in L.devices if d["type"] == "vent"}
    U = E.UPPER
    super_t = 102.0 if vents else 6.0
    out = []
    for s in sets:
        tag = s["id"][-1]
        # the north row: tiles (x-, z+) phase 0.5 and (x+, z+) phase 0, shifted by the set's phase
        rows = []
        for name, ph in (("north W", 0.5), ("north E", 0.0)):
            m = E.Motion("translate", distance=3.0, period=6.0, phase=ph).shift(s["phase_s"])
            n = int(round(super_t / STEP))
            top_ok, wind_ok = [], []
            for i in range(n):
                t = i * STEP
                y = m.value(t)
                top_ok.append(U - E.LEDGE_SURE <= y <= U + E.STEP_UP)
                v = vents.get(tag)
                wind_ok.append(v is None or v["timing"].state(t) != "blowing")
            both = [a and b for a, b in zip(top_ok, wind_ok)]
            w_top = windows_from_flags(top_ok[:int(6.0 / STEP)], STEP)
            per_cycle = []
            for c in range(int(super_t / 6.0)):
                seg = both[int(c * 6.0 / STEP):int((c + 1) * 6.0 / STEP)]
                w = windows_from_flags(seg + [False], STEP)
                per_cycle.append(w[0][1] if w else 0.0)
            rows.append(dict(tile=name, step_off_window=w_top[0][1] if w_top else 0.0,
                             with_gust_min=min(per_cycle), with_gust_mean=round(sum(per_cycle) / len(per_cycle), 2),
                             cycles_lost_to_gust=sum(1 for v in per_cycle if v < 0.8)))
        out.append(dict(set=s["id"], rows=rows))
    if vents:
        # any north tile of either set at the top with its own vent quiet: how long can a pawn have to wait?
        n = int(round(super_t / STEP))
        free = [False] * n
        for s in sets:
            v = vents.get(s["id"][-1])
            for ph in (0.5, 0.0):
                m = E.Motion("translate", distance=3.0, period=6.0, phase=ph).shift(s["phase_s"])
                for i in range(n):
                    t = i * STEP
                    if U - E.LEDGE_SURE <= m.value(t) <= U + E.STEP_UP and v["timing"].state(t) != "blowing":
                        free[i] = True
        gaps, run = [], 0
        for i in range(2 * n):
            if free[i % n]:
                if run:
                    gaps.append(run * STEP)
                run = 0
            else:
                run += 1
        w = windows_from_flags(free, STEP)
        out.append(dict(set="any", longest_free=w[0][1] if w else 0.0, shortest_free=w[-1][1] if w else 0.0,
                        longest_wait=round(max(gaps) if gaps else 0.0, 2), free_share=round(sum(free) / n, 2)))
    return out


def shock_hits(pieces, crates, x, z, y0, t):
    for p in pieces:
        if not p["timing"].shocking(t):
            continue
        cx, cy, cz = p["pos"]
        if abs(y0 - cy) > 2.0:
            continue
        if math.hypot(x - cx, z - cz) > E.FallingPiece.radius + R:
            continue
        blocked = any(seg_crosses_box2d((cx, cz), (x, z), (c["x0"], c["x1"], c["z0"], c["z1"])) for c in crates)
        if not blocked:
            return True
    return False


def check_r5(L):
    pieces = [d for d in L.devices if d["type"] == "fallingpiece"]
    crates = [w for w in L.walls if "crate" in w["name"]]
    belts = [d for d in L.devices if d["type"] == "conveyor"]
    U = pieces[0]["pos"][1]
    period = pieces[0]["timing"].cycle()
    n = int(round(period / STEP))
    zc = pieces[0]["pos"][2]

    def speed_at(x, z, base):
        for b in belts:
            bx, _, bz = b["pos"]
            if abs(x - bx) <= b["width"] / 2 and abs(z - bz) <= b["length"] / 2:
                return base + b["speed"]
        return base

    def run(x, z0, z1, base):
        flags = []
        for i in range(n):
            t, z, ok = i * STEP, z0, True
            while z < z1:
                if shock_hits(pieces, crates, x, z, U, t):
                    ok = False
                    break
                z += speed_at(x, z, base) * SIM
                t += SIM
            flags.append(ok)
        w = windows_from_flags(flags, STEP)
        return dict(lane=x, z=(round(z0, 2), round(z1, 2)), base_speed=base, longest=w[0][1] if w else 0.0,
                    share=round(sum(flags) / n, 2))

    entry = zc - E.FallingPiece.radius - R - 0.1
    exit_z = zc + E.FallingPiece.radius + R + 0.1
    out = dict(period=period, shock=E.FallingPiece.shock, warning=E.FallingPiece.warning + E.FallingPiece.fall)
    out["straight"] = [run(x, entry, exit_z, E.RUN) for x in (0.0, 1.5, 3.0, 4.5, 5.5)]
    out["straight_sprint"] = [run(x, entry, exit_z, E.SPRINT) for x in (0.0, 3.0, 5.5)]
    out["seam_hits_every"] = round(period / max(1, len(pieces)), 3)
    if belts:
        out["belts"] = [dict(id=b["id"], speed=b["speed"], length=b["length"], on_belt_run=round(E.RUN + b["speed"], 2))
                        for b in belts]
    return out


def check_r5b(L):
    crates = [d for d in L.devices if d["type"] == "crate"]
    if not crates:
        return None
    period = crates[0]["timing"].period
    floors = sorted([f for f in L.floors if f["top"] == E.UPPER], key=lambda f: f["z0"])
    south = max(f["z1"] for f in floors if f["z1"] <= crates[0]["pos"][2])
    north = min(f["z0"] for f in floors if f["z0"] >= crates[-1]["pos"][2])

    def edge(c, side, t):
        return c["pos"][2] + c["timing"].value(t) + side * c["half_z"]

    hops = [("floor -> 1", lambda t: edge(crates[0], -1, t) - south)]
    for a, b in zip(crates, crates[1:]):
        hops.append((f"{a['id'][-1]} -> {b['id'][-1]}", lambda t, a=a, b=b: edge(b, -1, t) - edge(a, 1, t)))
    hops.append((f"{crates[-1]['id'][-1]} -> floor", lambda t: north - edge(crates[-1], 1, t)))
    rows = []
    n = int(round(period / STEP))
    for name, fn in hops:
        g = [fn(i * STEP) for i in range(n)]
        w = gap_windows(fn, period)
        rows.append(dict(hop=name, gap_min=round(min(g), 2), gap_max=round(max(g), 2), **w))
    return dict(period=period, amplitude=crates[0]["timing"].distance, hops=rows)


def check_s4(L):
    bridges = [d for d in L.devices if d["type"] == "foldingbridge"]
    fb = bridges[0]["timing"]
    period = fb.cycle()
    n = int(round(period / STEP))
    panel_len = 2 * 3.9
    landing = 3.0
    rows = []
    for b in bridges:
        flags = []
        for i in range(n):
            t0 = i * STEP
            t_in, t_out = t0 + landing / E.RUN, t0 + (landing + panel_len) / E.RUN
            ok = True
            k = 0
            while t_in + k * SIM <= t_out:
                st, _ = b["timing"].state(t_in + k * SIM)
                if st not in ("open", "warning"):
                    ok = False
                    break
                k += 1
            flags.append(ok)
        w = windows_from_flags(flags, STEP)
        rows.append(dict(bridge=b["id"], phase=b["timing"].phase, longest=w[0][1] if w else 0.0, windows=w[:2]))
    out = dict(period=period, cross_panels=round(panel_len / E.RUN, 2), entry=rows)
    try:
        sc = L.device_by_id("S4_screen")
        lanes = [round(-5.5 + 0.5 * i, 2) for i in range(23)]
        out["screen"] = lanes_table([sc], lanes, sc["pos"][2] - 1.8, sc["pos"][2] + 1.8, E.UPPER, sc["timing"].period)
        # the chain: cross a bridge on its lane and go on through the screen without stopping
        chain = []
        for b in bridges:
            side = b["pos"][0]
            z_start, z_end = b["pos"][2] - 7.05, b["pos"][2] + 7.05

            def flat(t, x, z, b=b, z_start=z_start, z_end=z_end):
                if z_start + 3.0 <= z <= z_end - 3.0:
                    return b["timing"].state(t)[0] in ("open", "warning")
                return True
            pts = [(side, z_start), (side, z_end), (side * 1.5, sc["pos"][2] + 1.8)]
            chain.append(dict(bridge=b["id"], **route_table([sc], [("through", pts)], E.UPPER, period, extra=flat)["routes"][0]))
        out["chain"] = chain
        out["chain_union"] = round(sum(c["longest"] for c in chain), 2)
    except KeyError:
        pass
    return out


def run_all(name):
    L = E.LAYOUTS[name]()
    res = dict(layout=name)
    res["R1"] = check_r1(L)
    res["S2"] = check_s2(L)
    res["R2"] = check_r2(L)
    res["S3"] = check_s3(L)
    res["R3"] = check_r3(L)
    res["R4"] = check_r4(L)
    res["R5"] = check_r5(L)
    res["R5b"] = check_r5b(L)
    res["S4"] = check_s4(L)
    return res


def summary(res):
    lines = [f"== {res['layout']}"]
    r1 = res["R1"]
    lines.append(f"R1 bar {r1['bar_length']} m reaches rim: {r1['bar_reaches_rim']}; passes a rider every {r1['rider_pass_every']} s; "
                 f"tip {r1['tip_speed_world']} m/s; jump tolerance r4 {r1['at_radius'][1]['jump_tolerance']} s r8 {r1['at_radius'][3]['jump_tolerance']} s")
    s2 = res["S2"]
    lines.append(f"S2 ferry {s2['period']} s, max {s2['max_speed']} m/s: board {s2['board']['longest']} s, alight {s2['alight']['longest']} s, min gap {s2['min_gap']}"
                 + (f"; hurdle tolerance standing {s2['hurdle']['jump_tolerance_standing']} s, running {s2['hurdle']['jump_tolerance_running_at_it']} s" if "hurdle" in s2 else ""))
    for t in res["R2"]["walls"]:
        best = max(t["lanes"], key=lambda r: r["longest"])
        lines.append(f"R2 {t['id']}: union {t['union_longest']} s ({t['union_share']}), best lane {best['lane']} {best['longest']} s")
    if "walls_chain" in res["R2"]:
        lines.append("R2 walls A -> B: " + ", ".join(f"{r['route']} {r['longest']}s" for r in res["R2"]["walls_chain"]["routes"])
                     + f" | union {res['R2']['walls_chain']['union_longest']} s")
    t = res["R2"]["gates"]
    best = max(t["lanes"], key=lambda r: r["longest"])
    lines.append(f"R2 gates: union {t['union_longest']} s ({t['union_share']}), best lane {best['lane']} {best['longest']} s")
    for i, t in enumerate(res["S3"]["each"]):
        worst = min(t["lanes"], key=lambda r: r["longest"])
        lines.append(f"S3 screen {i + 1}: union {t['union_longest']} s, worst lane {worst['lane']} {worst['longest']} s")
    t = res["S3"]["chain_straight_lane"]
    lines.append(f"S3 both screens one lane: union {t['union_longest']} s")
    if "rhythm_from_gate" in res["S3"]:
        rt = res["S3"]["rhythm_from_gate"]
        lines.append("S3 rhythm from the gate: " + ", ".join(f"{r['route']} {r['longest']}s" for r in rt["routes"]))
    r3 = res["R3"]
    for i, t in enumerate(r3["each"]):
        worst = min(t["lanes"], key=lambda r: r["longest"])
        best = max(t["lanes"], key=lambda r: r["longest"])
        lines.append(f"R3 pendulum {i + 1} ({r3['bridge_width']} m): union {t['union_longest']} s, lanes {worst['longest']}..{best['longest']} s")
    lines.append(f"R3 both straight {r3['both_straight']['union_longest']} s, sprint {r3['both_sprint']['union_longest']} s, never-hit lanes {r3['lanes_never_hit']}")
    for s in res["R4"]:
        if s["set"] == "any":
            lines.append(f"R4 any set: free step-off windows {s['shortest_free']}..{s['longest_free']} s, longest wait {s['longest_wait']} s, share {s['free_share']}")
            continue
        for r in s["rows"]:
            lines.append(f"R4 {s['set']} {r['tile']}: step-off {r['step_off_window']} s, with gust min {r['with_gust_min']} mean {r['with_gust_mean']}, lost cycles {r['cycles_lost_to_gust']}/17")
    r5 = res["R5"]
    lines.append("R5 run: " + ", ".join(f"x{r['lane']} {r['longest']}s" for r in r5["straight"]) + " | sprint: "
                 + ", ".join(f"x{r['lane']} {r['longest']}s" for r in r5["straight_sprint"]))
    if res["R5b"]:
        lines.append("R5b hops: " + ", ".join(f"{h['hop']} {h['longest']}s (gap {h['gap_min']}..{h['gap_max']})" for h in res["R5b"]["hops"]))
    s4 = res["S4"]
    lines.append("S4 entry: " + ", ".join(f"{r['bridge']} {r['longest']}s" for r in s4["entry"]) + f" (panels {s4['cross_panels']} s)")
    if "screen" in s4:
        lines.append(f"S4 screen union {s4['screen']['union_longest']} s; bridge+screen without stopping: " + ", ".join(f"{c['bridge']} {c['longest']}s" for c in s4["chain"]))
    return "\n".join(lines)


if __name__ == "__main__":
    name = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "ld_v1"
    res = run_all(name)
    print(summary(res))
    if "--json" in sys.argv:
        with open(sys.argv[sys.argv.index("--json") + 1], "w", encoding="utf-8") as f:
            json.dump(res, f, ensure_ascii=False, indent=1)
