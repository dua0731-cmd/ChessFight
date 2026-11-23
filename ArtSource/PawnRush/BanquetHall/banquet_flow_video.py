# The grand banquet hall's draft (level design v0.2) as a short film: one pawn (under the yellow marker) is followed
# through the nine beats, a cut and a caption for each, every device moving on its real period (banquet_layout.py).
# The pawn's own moves are drawn by hand from the design's numbers (run 2.5 m/s, climb 1.2 m/s): this shows how the
# section is meant to be played. It is not a recording of the game, and nothing here is in Unity.
#
#   blender --background --factory-startup --python banquet_flow_video.py -- <out.mp4> [width] [height]
#   blender --background --factory-startup --python banquet_flow_video.py -- <folder> [width] [height] stills
#           (one PNG from the middle of each beat instead of the film, to look at the framing)
import bpy, math, os, sys

_here = os.path.dirname(os.path.abspath(__file__))
_argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FILM_OUT = _argv[0] if _argv else "//banquet_flow.mp4"
FILM_W = int(_argv[1]) if len(_argv) > 1 else 1280
FILM_H = int(_argv[2]) if len(_argv) > 2 else 720
STILLS = len(_argv) > 3 and _argv[3] == "stills"
sys.argv = sys.argv[:sys.argv.index("--") + 1] + ["//unused.png", "16", str(FILM_W), str(FILM_H)] if "--" in sys.argv else sys.argv
_set = open(os.path.join(_here, "banquet_blockout.py"), encoding="utf-8").read()
exec(compile(_set.split("# ==== still")[0], "banquet_blockout.py (the set)", "exec"), globals())

FPS = 24
DT = 1.0 / FPS
TOP = [t[1] for t in TIERS]


def turn(v, deg):
    # A level vector (x, z) turned clockwise (seen from above) by a heading angle.
    c, s = math.cos(D(deg)), math.sin(D(deg))
    return (v[0] * c + v[1] * s, -v[0] * s + v[1] * c)


def ease(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3.0 - 2.0 * k)


def mix(a, b, k):
    return tuple(p + (q - p) * k for p, q in zip(a, b))


# ---------------------------------------------------------------- each beat: where the pawn is, frame by frame
def beat_cloth():
    t, x, y, z, mode, timer, out = 5.6, 1.0, 0.0, 9.6, "run", 0.0, []
    for f in range(int(7.0 * FPS)):
        if mode == "run":
            z += L["RUN"] * DT
            if z >= Z0 - 0.5:
                z, y, mode = Z0 - 0.5, 0.45, "hang"
        elif mode == "hang":
            y += (L["cloth_speed"](3, t) + L["CLIMB_UP"]) * DT
            if y >= TABLE - 0.1:
                mode, timer = "top", 0.0
        elif mode == "top":
            timer += DT
            k = ease(timer / 0.45)
            y, z = TABLE - 0.1 + 0.1 * k, Z0 - 0.5 + 1.2 * k
            if timer >= 0.45:
                mode = "walk"
        else:
            z = min(z + L["RUN"] * DT, Z0 + 5.0)
        out.append(((x, y, z), None))
        t += DT
    return 5.6, out


def beat_toast():
    t, z, out = 0.4, 25.6, []
    whole = sum(L["TOAST"])
    for f in range(int(7.5 * FPS)):
        speed = L["RUN"]
        for pair, zp in enumerate(L["GOBLETS"]):
            u = (t - pair * L["TOAST_WAVE"]) % whole
            if 0.2 < zp - z < 1.7 and 1.5 < u < 3.1:
                speed = 0.0                                   # the pair ahead is leaning back or swinging in: wait
            if 2.7 <= u < 3.1 and zp - 7.0 < z < zp - 0.3:
                speed -= L["RUNNER_JERK"][0] / L["RUNNER_JERK"][1]   # the runner under the pawn is jerked back
        z += speed * DT
        out.append(((0.3, TABLE, z), None))
        t += DT
    return 0.4, out


def beat_knight():
    t0, out = 10.4, []
    t, pos, local, mode, timer = t0, (-3.0, TABLE, 65.55), None, "wait", 0.0
    for f in range(int(9.4 * FPS)):
        kx, ky, kz, yaw, air = L["knight"](0, t)
        if mode == "wait" and t >= 12.25:
            mode, local = "ride", turn((0.0, -(KR + 0.34)), -yaw)
        if mode == "ride":
            off = turn(local, yaw)
            pos = (kx + off[0], ky + 0.9, kz + off[1])
            if t >= 18.35:
                mode, timer = "drop", 0.0
        elif mode == "drop":
            timer += DT
            pos = (pos[0], max(TABLE, pos[1] - 3.0 * DT), pos[2])
            if timer > 0.4:
                mode = "walk"
        elif mode == "walk":
            goal = (0.0, TABLE, L["ISLAND"][3] - 0.45)
            d = math.hypot(goal[0] - pos[0], goal[2] - pos[2])
            step = min(d, L["RUN"] * DT)
            if d > 1e-3:
                pos = (pos[0] + (goal[0] - pos[0]) * step / d, TABLE, pos[2] + (goal[2] - pos[2]) * step / d)
        out.append((pos, None))
        t += DT
    return t0, out


def beat_waltz():
    t0, out = 18.2, []
    t, pos, mode, timer, y, local = t0, (0.0, TABLE, L["ISLAND"][3] - 0.45), "wait", 0.0, TABLE, None
    for f in range(int(13.4 * FPS)):
        mid, q, king, tr = L["couple"](0, t)
        if mode == "wait" and t >= 19.9:
            mode, y = "climb", TABLE + 0.3
            local = turn((pos[0] - q[0], pos[2] - q[1]), tr)       # the queen's frame turns by -tr
            reach = math.hypot(*local)
            local = (local[0] / reach, local[1] / reach)
        if mode == "climb":
            y += L["CLIMB_UP"] * DT
            r = L["gown"](y) + 0.3
            if y >= SY - 0.05:
                mode, timer = "top", 0.0
        if mode == "top":
            timer += DT
            y = SY
            r = L["gown"](SY) + 0.3 - 1.2 * ease(timer / 0.45)
            if timer >= 0.45:
                mode = "ride"
        if mode == "ride":
            r, y = 0.75, SY
            if t >= 30.0:
                mode, timer = "hop", 0.0
        if mode in ("climb", "top", "ride"):
            off = turn(local, -tr)
            pos = (q[0] + off[0] * r, y, q[1] + off[1] * r)
            hop_from = pos
        elif mode == "hop":
            timer += DT
            k = ease(timer / 0.5)
            goal = (hop_from[0], STAGE_Y, STAGE[2] + 0.9)
            pos = mix(hop_from, goal, k)
            pos = (pos[0], pos[1] + 0.5 * 4.0 * k * (1.0 - k), pos[2])
            if timer >= 0.5:
                mode = "walk"
        elif mode == "walk":
            pos = (pos[0], STAGE_Y, min(pos[2] + L["RUN"] * DT, STAGE[2] + 3.4))
        out.append((pos, None))
        t += DT
    return t0, out


def neck_top(x):
    a, b = STRINGS[0], STRINGS[-1]
    return a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0]) + L["NECK"] - 0.22 if x < b[0] else LOFT_Y


def beat_harp():
    t0, out = 0.2, []
    sx, stop = STRINGS[5]
    vx, vtop = STRINGS[7]
    t, pos, mode, timer = t0, (sx - 1.6, STAGE_Y, HZ - 3.2), "walk", 0.0
    victim, vmode, vfall = (vx + 1.4, STAGE_Y, HZ - 2.4), "walk", 0.0
    for f in range(int(8.2 * FPS)):
        if mode == "walk":
            goal = (sx, STAGE_Y, HZ - 0.4)
            d = math.hypot(goal[0] - pos[0], goal[2] - pos[2])
            step = min(d, L["RUN"] * DT)
            if d > 1e-3:
                pos = (pos[0] + (goal[0] - pos[0]) * step / d, STAGE_Y, pos[2] + (goal[2] - pos[2]) * step / d)
            elif L["PLUCK_DEAD"] < L["pluck"](5, t) < 1.2:
                mode = "climb"                               # just plucked and still again: now there is time
        elif mode == "climb":
            pos = (sx, pos[1] + L["CLIMB_UP"] * DT, HZ - 0.4)
            if pos[1] >= stop - 1.0:
                mode, timer, top_from = "top", 0.0, pos
        elif mode == "top":
            timer += DT
            pos = mix(top_from, (sx, neck_top(sx), HZ + 0.7), ease(timer / 0.5))
            if timer >= 0.5:
                mode = "neck"
        else:
            x = min(pos[0] + L["RUN"] * 0.8 * DT, LOFT[0] + 1.6)
            pos = (x, neck_top(x), HZ + 0.7)
        # the other pawn starts late on a long string and is still on it when its turn comes round
        if vmode == "walk":
            victim = (max(vx, victim[0] - L["RUN"] * DT), STAGE_Y, min(HZ - 0.4, victim[2] + L["RUN"] * DT))
            if t >= 3.3:
                vmode = "climb"
        elif vmode == "climb":
            victim = (vx, victim[1] + L["CLIMB_UP"] * L["ROOK"] * DT, HZ - 0.4)
            if L["pluck"](7, t) < 0.1 and t > 4.0:
                vmode = "fall"
        elif vmode == "fall":
            vfall += 9.8 * DT
            victim = (vx, max(STAGE_Y, victim[1] - vfall * DT), max(HZ - 1.3, victim[2] - 1.2 * DT))
        out.append((pos, victim))
        t += DT
    return t0, out


def beat_candle():
    t0, out = 0.0, []
    c = L["CANDLES"][0]
    az, r, t = 250.0, L["CANDLE_R"] + 0.3, t0
    h = L["heading"](az)
    foot = (c[0] + h[0] * r, LOFT_Y + 0.2, c[1] + h[1] * r)
    pos, mode, timer = (foot[0] - 3.2, LOFT_Y, foot[2] + 0.6), "walk", 0.0
    for f in range(int(7.0 * FPS)):
        if mode == "walk":
            d = math.hypot(foot[0] - pos[0], foot[2] - pos[2])
            step = min(d, L["RUN"] * DT)
            if d > 1e-3:
                pos = (pos[0] + (foot[0] - pos[0]) * step / d, LOFT_Y, pos[2] + (foot[2] - pos[2]) * step / d)
            else:
                mode, y = "climb", LOFT_Y + 0.3
        elif mode == "climb":
            if 2.7 <= t and az < 275.0:
                az = min(275.0, az + math.degrees(L["CLIMB_SIDE"] / r) * DT)     # wax is beading overhead: along the candle
            else:
                y += L["CLIMB_UP"] * DT
            h = L["heading"](az)
            pos = (c[0] + h[0] * r, y, c[1] + h[1] * r)
            if y >= L["CANDLE_TOP"] - 0.1:
                mode, timer, top_from = "top", 0.0, pos
        elif mode == "top":
            timer += DT
            h = L["heading"](az)
            pos = mix(top_from, (c[0] + h[0] * 0.45, L["CANDLE_TOP"], c[1] + h[1] * 0.45), ease(timer / 0.45))
            if timer >= 0.45:
                mode = "stand"
        else:
            goal = (c[0] + HEAD[0] * 0.6, L["CANDLE_TOP"], c[1] + HEAD[1] * 0.6)
            pos = mix(pos, goal, 0.08)
        out.append((pos, None))
        t += DT
    return t0, out


def on_rope(k, t):
    p = L["rope_point"](0, k, t, RL - 0.9)
    return (p[0], p[1] - 1.15, p[2])


def beat_ropes():
    t0, out = 2.9, []
    c = L["CANDLES"][0]
    stand = (c[0] + HEAD[0] * 0.6, L["CANDLE_TOP"], c[1] + HEAD[1] * 0.6)
    grab = L["ROPE_PERIOD"] * 0.75                              # the first chandelier is nearest the candle
    hand = [grab + L["ROPE_PERIOD"] / 2.0 * (k + 1) for k in range(4)]   # when each one meets the next
    off = hand[3] + L["ROPE_PERIOD"] / 2.0
    land = (L["LAND"][0][0], TOP[1], L["LAND"][0][1])
    t = t0
    for f in range(int((off + 0.9 - t0) * FPS)):
        if t < grab:
            pos = stand
        elif t < grab + 0.25:
            pos = mix(stand, on_rope(0, t), ease((t - grab) / 0.25))
        elif t >= off:
            k = ease((t - off) / 0.45)
            a = on_rope(4, off)
            pos = mix(a, land, k)
            pos = (pos[0], pos[1] + 0.35 * 4.0 * k * (1.0 - k), pos[2])
        else:
            n = sum(1 for h in hand if t >= h + 0.3)
            pos = on_rope(n, t)
            for i, h in enumerate(hand):
                if h - 0.1 <= t < h + 0.3:
                    k = ease((t - h + 0.1) / 0.4)
                    pos = mix(on_rope(i, t), on_rope(i + 1, t), k)
                    lift = 0.5 if L["HANDOVERS"][i][1] < 1.0 else 0.0
                    pos = (pos[0], pos[1] + lift * 4.0 * k * (1.0 - k), pos[2])
        out.append((pos, None))
        t += DT
    return t0, out


def cream(i, az, t):
    count, degrees = L["CREAM"][i]
    return ((az - L["tier_turn"](i, t) - 17.0 * i) % (360.0 / count)) < degrees


def beat_cake(t0):
    out = []
    land = L["LAND"][0]
    az = math.degrees(math.atan2(land[0] - CK[0], land[1] - CK[1]))
    r, y, on, mode, timer, t = L["dist"](land, CK), TOP[1], 1, "ledge", 0.0, t0
    for f in range(int(40 * FPS)):
        nxt = on + 1
        if mode == "ledge":
            az += TIERS[on][2] * DT
            wall = TIERS[nxt][0] + 0.3
            if r > wall:
                r = max(wall, r - 1.5 * DT)
            else:
                ready = cream(nxt, az, t) and cream(nxt, az + 4.0, t) and cream(nxt, az - 4.0, t)
                if nxt == 4:
                    # the top tier carries its climber round toward the knife (north, heading 0): only start well clear
                    end = az + TIERS[4][2] * (2.8 / L["CLIMB_UP"] + 0.6)
                    lo, hi = min(az, end), max(az, end)
                    ready = ready and not any(lo - 16.0 <= n <= hi + 16.0 for n in (-360.0, 0.0, 360.0, 720.0))
                    az = (az + 180.0) % 360.0 - 180.0 if not ready else az
                if ready:
                    mode = "climb"
        elif mode == "climb":
            az += TIERS[nxt][2] * DT
            y += L["CLIMB_UP"] * DT
            if y >= TOP[nxt] - 0.05:
                mode, timer = "top", 0.0
        elif mode == "top":
            timer += DT
            az += TIERS[nxt][2] * DT
            y = TOP[nxt]
            r = TIERS[nxt][0] + 0.3 - 0.95 * ease(timer / 0.45)
            if timer >= 0.45:
                on = nxt
                mode = "ledge" if on < 4 else "crown"
                timer = 0.0
        if mode == "crown":
            timer += DT
            az += TIERS[4][2] * DT
            h = L["heading"](az)
            here = (CK[0] + h[0] * r, TOP[4] + 0.06 * ease(timer), CK[1] + h[1] * r)
            pos = mix(here, (K0[0], TOP[4] + 0.06, CK[1] + 1.8), ease((timer - 0.4) / 1.2))
            out.append((pos, None))
            if timer >= 1.9:
                break
        else:
            h = L["heading"](az)
            out.append(((CK[0] + h[0] * r, y, CK[1] + h[1] * r), None))
        t += DT
    return t0, out


def beat_knife():
    out, s = [], 0.0
    a = (K0[0], TOP[4] + 0.06, CK[1] + 1.8)
    b = (K0[0], BY, BAL[2] + 0.4)
    c = (EXIT[0], BY, EXIT[2] - 0.6)
    ab = math.hypot(b[2] - a[2], b[1] - a[1])
    for f in range(int(7.6 * FPS)):
        s += L["RUN"] * DT
        pos = mix(a, b, s / ab) if s < ab else mix(b, c, min(1.0, (s - ab) / (c[2] - b[2])))
        out.append((pos, None))
    return 3.0, out


def still(seconds, t0=2.0):
    return t0, [(None, None)] * int(seconds * FPS)


ropes = beat_ropes()
BEATS = [
    # title, line under it, (clock at its first frame, the pawn's track), camera (from, at, follow)
    ("대연회장 — 부엌에서 왕의 발코니까지", "레벨 디자인 초안 v0.2 · 연회의 순서를 따라 아홉 구간 · 보통 2분 30초 (대계단 현관홀과 비슷하게)",
     still(5.0), ((104, 78, -6), (19, 4, 82), None)),
    ("① 식탁보 — 상 차리기", "올라가는 줄(금색 무늬)에 매달리면 식탁 위로 실려 간다 · 내려가는 줄(회색)은 밀려 내려간다",
     beat_cloth(), ((10.5, 5.2, 4.5), (0.6, 2.7, Z0), None)),
    ("② 건배하는 식탁", "잔이 뒤로 젖혀지면 멈추고, '챙' 한 뒤에 지나간다 · 부딪칠 때 러너가 뒤로 당겨진다 (벽타기 사이의 달리기)",
     beat_toast(), ((9.5, 13.0, 19.0), (0.0, 5.2, 37.0), None)),
    ("③ 나이트의 도약", "식탁 끝 칸에 내려앉은 나이트의 덮개에 매달린다 · ㄱ자로 두 번 실려 가 가운데 단에서 손을 놓는다",
     beat_knight(), ((-13.6, 12.5, 62.0), (-2.0, 4.0, 74.0), None)),
    ("④ 왈츠", "여왕이 멈춘 순간 드레스를 잡고 어깨까지 3 m · 춤에 실려 반 바퀴 · 악단석 앞에 멈추면 건너간다",
     beat_waltz(), ((19.5, 17.5, 82.0), (0.0, 5.0, 93.5), None)),
    ("⑤ 하프", "튕겨진 직후에 잡고, 다음 차례가 오기 전에 꼭대기까지 · 보라 = 곧 튕긴다, 빨강 = 튕겼다(매달려 있으면 떨어진다)",
     beat_harp(), ((-4.0, 12.6, STAGE[2] - 4.5), (-3.6, 9.5, HZ), None)),
    ("⑥ 큰 촛대", "꼭대기에 촛농이 맺히면(보라) 옆으로 비킨다 · 흘러내리는 촛농에 맞으면 손이 풀린다",
     beat_candle(), ((4.2, 16.2, STAGE[2] - 2.2), (11.6, 13.2, L["CANDLES"][0][1]), None)),
    ("⑦ 샹들리에 다섯", "끝에서 만나는 순간 다음 샹들리에로 — 건네받기 · 떨어지며 잡기 · 날아서 잡기 · 밑은 수프(빠지면 윗단에서 다시)",
     ropes, ((0.0, 16.5, -24.0), (2.5, 12.0, 0.0), "side")),
    ("⑧ 케이크 — 커팅", "층마다 반대로 돈다 · 크림 줄(주황)만 잡힌다 · 꼭대기 층은 칼 쪽으로 실려 가기 전에 올라선다",
     beat_cake(ropes[0] + len(ropes[1]) * DT), ((CK[0] - 20.5, 21.5, CK[1] - 13.5), (CK[0] - 1.0, 12.6, CK[1] + 0.5), None)),
    ("⑨ 칼등과 왕의 발코니", "케이크에 꽂힌 칼의 등(폭 1 m)을 걸어 발코니로 · 출구",
     beat_knife(), ((CK[0] + 17.0, 25.0, CK[1] + 0.0), (CK[0] - 0.5, 18.2, CK[1] + 9.0), None)),
    ("여섯 구간이 벽이나 줄을 탄다 · 낙사는 수프 하나", "아직 초안이다 — Unity에 만든 것이 없고, 폰의 움직임은 기획 수치로 그린 것이다",
     still(4.5, 9.0), ((104, 78, -6), (19, 4, 82), None)),
]
STARTS, total = [], 0
for b in BEATS:
    STARTS.append(total)
    total += len(b[2][1])
print("FILM", total, "frames,", round(total / FPS, 1), "s;", [len(b[2][1]) for b in BEATS])

# ---------------------------------------------------------------- the pawn that is followed, the other one, the captions
HERO = group("Hero")
piece(0.0, 0.0, 0.0, 1, HERO, 1.25)
into(HERO, inst(ME_CONE, "Hero marker", M_HERO, U(0, 2.5, 0), (0.55, 0.55, 0.9), (math.pi, 0, 0)))
OTHER = group("Other pawn")
piece(0.0, 0.0, 0.0, 0, OTHER, 1.25)

M_TEXT = principled("MAT_Caption", (1, 1, 1), 0.5, 0.0, (1.0, 0.97, 0.9), 2.2)
M_TEXT2 = principled("MAT_CaptionSmall", (1, 1, 1), 0.5, 0.0, (1.0, 0.86, 0.6), 1.6)
M_BAR = principled("MAT_CaptionBar", (0.0, 0.0, 0.0), 1.0, 0.0, (0.02, 0.012, 0.006), 1.0)
font = None
for name in ("malgunbd.ttf", "malgun.ttf"):
    path = os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name)
    if os.path.exists(path):
        font = bpy.data.fonts.load(path)
        break
cam_d.lens = 30.0
cam_d.sensor_width = 36.0
AHEAD = 0.5
half_w = AHEAD * 18.0 / cam_d.lens
half_h = half_w * FILM_H / FILM_W


def caption(body, size, y, mat):
    cu = bpy.data.curves.new("Caption", "FONT")
    cu.body = body
    cu.size = size
    if font is not None:
        cu.font = font
    o = bpy.data.objects.new("Caption", cu)
    col.objects.link(o)
    o.data.materials.append(mat)
    o.parent = cam
    o.location = (-half_w * 0.94, y, -AHEAD)
    return o


bar_o = inst(ME_CUBE, "Caption bar", M_BAR, (0, 0, 0), (half_w * 2.1, half_h * 0.42, 0.002))
bar_o.parent = cam
bar_o.location = (0, -half_h * 0.81, -AHEAD - 0.004)
CAPTIONS = [(caption(b[0], half_h * 0.105, -half_h * 0.76, M_TEXT), caption(b[1], half_h * 0.058, -half_h * 0.92, M_TEXT2)) for b in BEATS]

# ---------------------------------------------------------------- light for the film (EEVEE: no bounced light, so more of it)
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.13, 0.10, 1)
sun = light("LGT_FilmSun", "SUN", U(90, 90, 20), 2.6, (1.0, 0.90, 0.74), angle=D(8))
aim(sun, U(10, 4, 80))
back = light("LGT_FilmBack", "SUN", U(-60, 70, 160), 0.9, (0.72, 0.80, 1.0), angle=D(20))
aim(back, U(10, 4, 60))
back.data.use_shadow = False
for (x, y, z) in lamps:
    o = light("LGT_Candle", "POINT", U(x, y, z), 900, (1.0, 0.58, 0.24), shadow_soft_size=0.4)
    o.data.use_shadow = False
scene.render.engine = "BLENDER_EEVEE"
scene.eevee.taa_render_samples = 12
scene.view_settings.exposure = -0.35
scene.render.resolution_x, scene.render.resolution_y = FILM_W, FILM_H
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 1, total


def show(frame):
    index = max(i for i, s in enumerate(STARTS) if s <= frame - 1)
    title, line, (t0, track), (c_from, c_at, follow) = BEATS[index]
    n = frame - 1 - STARTS[index]
    hero, other = track[n]
    pose(t0 + n * DT)
    HERO.location = U(*hero) if hero else U(0, -60, 0)
    OTHER.location = U(*other) if other else U(0, -60, 0)
    if follow == "side" and hero:
        # beside the line of chandeliers, keeping level with the pawn (eased, so the swing does not shake the frame)
        along = (hero[0] - L["CANDLES"][0][0]) * HEAD[0] + (hero[2] - L["CANDLES"][0][1]) * HEAD[1]
        smooth = show.along + (along - show.along) * 0.06 if show.beat == index else along
        show.along = smooth
        base = (L["CANDLES"][0][0] + HEAD[0] * smooth, L["CANDLES"][0][1] + HEAD[1] * smooth)
        side = (HEAD[1], -HEAD[0])
        cam.location = U(base[0] + side[0] * -c_from[2] + HEAD[0] * c_from[0], c_from[1], base[1] + side[1] * -c_from[2] + HEAD[1] * c_from[0])
        aim(cam, U(base[0] + HEAD[0] * c_at[0], c_at[1], base[1] + HEAD[1] * c_at[0]))
    else:
        k = n / float(max(1, len(track) - 1))
        drift = 0.03 * (k - 0.5)
        cam.location = U(c_from[0] + (c_at[0] - c_from[0]) * drift, c_from[1], c_from[2] + (c_at[2] - c_from[2]) * drift)
        aim(cam, U(*c_at))
    show.beat = index
    for i, (a, b) in enumerate(CAPTIONS):
        a.location.z = b.location.z = -AHEAD if i == index else AHEAD * 30.0


show.beat, show.along = -1, 0.0


def on_frame(sc, depsgraph=None):
    show(sc.frame_current)


if STILLS:
    scene.render.image_settings.file_format = "PNG"
    for i, start in enumerate(STARTS):
        frame = start + 1 + int(len(BEATS[i][2][1]) * (0.55 if i not in (4, 7) else 0.3))
        scene.frame_set(frame)
        show(frame)
        scene.render.filepath = os.path.join(FILM_OUT, "beat_%02d.png" % i)
        bpy.ops.render.render(write_still=True)
        print("STILL", i, frame)
else:
    bpy.app.handlers.frame_change_pre.append(on_frame)
    img = scene.render.image_settings
    if "media_type" in img.bl_rna.properties:
        img.media_type = "VIDEO"
    img.file_format = "FFMPEG"
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "HIGH"
    scene.render.ffmpeg.gopsize = 24
    scene.render.filepath = FILM_OUT
    bpy.ops.render.render(animation=True)
    print("FILMED", FILM_OUT)
