# Eight obstacle ideas taken from what a banquet hall brings to mind (R814), each as a short scene of plain shapes:
# the waiters' parade, the silver cloches, the magician's tablecloth trick, the champagne tower, the chocolate
# fountain, the stage curtain, the chimney fire and the timpani. One pawn under a yellow marker is followed in each.
# Ideas only: nothing here is in Unity; the pawns' moves are drawn by hand from the climbing numbers (run 2.5 m/s,
# climb 1.2 m/s); the devices' numbers are rough and are set for real when an idea is chosen.
# Each scene is built at its own place along x (local frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y)).
#
#   blender --background --factory-startup --python banquet_ideas_film.py -- <out.mp4> [width] [height]
#   blender --background --factory-startup --python banquet_ideas_film.py -- <folder> [width] [height] stills
#           (one PNG from each scene instead of the film, to look at the framing)
import bpy, bmesh, math, os, sys
from mathutils import Vector, Euler

_here = os.path.dirname(os.path.abspath(__file__))
_argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FILM_OUT = _argv[0] if _argv else "//banquet_ideas.mp4"
FILM_W = int(_argv[1]) if len(_argv) > 1 else 1280
FILM_H = int(_argv[2]) if len(_argv) > 2 else 720
STILLS = len(_argv) > 3 and _argv[3] == "stills"
if "--" in sys.argv:
    sys.argv = sys.argv[:sys.argv.index("--") + 1] + ["//unused.png", "16", str(FILM_W), str(FILM_H)]
_head = open(os.path.join(_here, "..", "RoyalObservatory", "observatory_dress_preview.py"), encoding="utf-8").read()
exec(compile(_head.split("# entry, ring court")[0], "observatory_dress_preview.py (head)", "exec"), globals())
D = math.radians
FPS = 24
DT = 1.0 / FPS
HIDE = (0.0, -80.0, 0.0)

# ---------------------------------------------------------------- materials
M_TILE = principled("MAT_Tile", (0.05, 0.04, 0.035), 0.55)
M_PARQ = principled("MAT_Parquet", (0.30, 0.17, 0.08), 0.35)
M_BOARDS = principled("MAT_StageBoards", (0.36, 0.22, 0.11), 0.45)
M_CLOTH = principled("MAT_Cloth", (0.80, 0.74, 0.60), 0.8)
M_CLOTH2 = principled("MAT_ClothShade", (0.62, 0.52, 0.36), 0.8)
M_LINEN = principled("MAT_Linen", (0.93, 0.91, 0.86), 0.85)
M_OLIVE = principled("MAT_Olive", (0.10, 0.19, 0.045), 0.75)
M_WALNUT = principled("MAT_Walnut", (0.20, 0.10, 0.045), 0.45)
M_STONE = principled("MAT_WarmStone", (0.42, 0.33, 0.23), 0.7)
M_SOOT = principled("MAT_Soot", (0.07, 0.055, 0.05), 0.9)
M_SECTION = principled("MAT_CutFace", (0.16, 0.12, 0.10), 0.9)
M_SILVER = principled("MAT_Silver", (0.80, 0.82, 0.84), 0.18, 1.0)
M_IRON = principled("MAT_Iron", (0.05, 0.05, 0.055), 0.4, 0.8)
M_CLOCHE = principled("MAT_Cloche", (0.86, 0.87, 0.89), 0.3, 0.45)
M_PLATE = principled("MAT_Plate", (0.85, 0.83, 0.78), 0.25)
M_GLASS = principled("MAT_Glass", (0.70, 0.80, 0.84), 0.05, 0.2)
M_FULL = principled("MAT_ChampagneStill", (0.86, 0.72, 0.36), 0.15, 0.0, (1.0, 0.8, 0.4), 0.3)
M_CHAMP = principled("MAT_ChampagneOverflow", (1.0, 0.84, 0.40), 0.15, 0.0, (1.0, 0.80, 0.30), 6.0)
M_CHOC = principled("MAT_Chocolate", (0.16, 0.07, 0.03), 0.25)
M_CHOC2 = principled("MAT_ChocolateLight", (0.40, 0.20, 0.09), 0.3)
M_BERRY = principled("MAT_Strawberry", (0.75, 0.06, 0.05), 0.4)
M_LEAF = principled("MAT_Leaf", (0.10, 0.35, 0.08), 0.6)
M_VELVET = principled("MAT_Curtain", (0.50, 0.03, 0.05), 0.85)
M_VELVET2 = principled("MAT_CurtainShade", (0.34, 0.02, 0.04), 0.85)
M_COPPER = principled("MAT_Copper", (0.72, 0.36, 0.18), 0.25, 1.0)
M_HEAD = principled("MAT_DrumHead", (0.92, 0.88, 0.78), 0.6)
M_EBONY = principled("MAT_Ebony", (0.07, 0.06, 0.06), 0.3)
M_IVORY = principled("MAT_Ivory", (0.80, 0.74, 0.60), 0.35)
M_COAT = principled("MAT_Coat", (0.10, 0.10, 0.12), 0.5)
M_SHIRT = principled("MAT_Shirt", (0.90, 0.90, 0.88), 0.5)
M_GLOVE = principled("MAT_Glove", (0.96, 0.96, 0.94), 0.5)
M_ROBE = principled("MAT_Robe", (0.24, 0.07, 0.38), 0.6)
M_ROAST = principled("MAT_Roast", (0.45, 0.20, 0.07), 0.5)
M_STEAM = principled("MAT_Steam", (1, 1, 1), 0.5, 0.0, (1, 1, 1), 0.8)
M_BOTTLE = principled("MAT_Bottle", (0.06, 0.20, 0.09), 0.2, 0.2)
M_FLAME = principled("MAT_Flame", (1, 0.32, 0.04), 0.3, 0.0, (1.0, 0.26, 0.02), 2.6)
M_EMBER = principled("MAT_Ember", (0.5, 0.12, 0.03), 0.5, 0.0, (1.0, 0.25, 0.04), 1.5)
M_EMBER_HOT = principled("MAT_EmberHot", (1, 0.4, 0.1), 0.3, 0.0, (1.0, 0.5, 0.1), 14.0)
M_WAX = principled("MAT_Wax", (0.84, 0.78, 0.62), 0.45)
M_CFLAME = principled("MAT_CandleFlame", (1, 0.8, 0.4), 0.3, 0.0, (1.0, 0.62, 0.22), 30.0)
M_HERO = principled("MAT_HeroMark", (1.0, 0.9, 0.2), 0.4, 0.0, (1.0, 0.85, 0.1), 6.0)
M_WARN = principled("MAT_Warning", (0.55, 0.30, 0.85), 0.4, 0.0, (0.6, 0.3, 1.0), 3.0)


# ---------------------------------------------------------------- helpers
def frustum(name, r0, r1, h, segments=36):
    # A cone with its point cut off, standing on its base.
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=r0, radius2=r1, depth=h)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, h / 2))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = abs(p.normal.z) < 0.9
    me.materials.append(None)
    return me


def group(name, at=(0.0, 0.0, 0.0)):
    e = bpy.data.objects.new(name, None)
    col.objects.link(e)
    e.location = U(*at)
    return e


def into(parent, *made):
    for o in made:
        o.parent = parent
    return parent


def ease(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3.0 - 2.0 * k)


def mix(a, b, k):
    return tuple(p + (q - p) * k for p, q in zip(a, b))


def hop(a, b, k, h):
    k = max(0.0, min(1.0, k))
    p = mix(a, b, k)
    return (p[0], p[1] + h * 4.0 * k * (1.0 - k), p[2])


def track(t, keys):
    # keys: (time, point, arc): straight between neighbours, the arc lifting the middle of the leg it ends.
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, p0, _), (t1, p1, arc) in zip(keys, keys[1:]):
        if t <= t1:
            return hop(p0, p1, (t - t0) / max(1e-6, t1 - t0), arc)
    return keys[-1][1]


def K(*rows):
    # Keys written as (time, x, y, z[, arc]).
    return [(r[0], (r[1], r[2], r[3]), r[4] if len(r) > 4 else 0.0) for r in rows]


def set_bar(o, a, b):
    # Puts a bar made by bar() between section points a and b.
    A, B = U(*a), U(*b)
    d = B - A
    o.location = (A + B) / 2
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    o.scale.z = max(1e-3, d.length)


def piece(x, y, z, team, parent=None, scale=1.0):
    parts = [cyl("Pawn skirt", x, y, y + 0.55 * scale, z, 0.62 * scale, M_WHITE),
             inst(ME_CONE, "Pawn body", M_WHITE, U(x, y + 0.6 * scale, z), (0.6 * scale, 0.6 * scale, 0.7 * scale)),
             ball("Pawn head", x, y + 0.92 * scale, z, 0.42 * scale, M_WHITE),
             cyl("Pawn scarf", x, y + 0.66 * scale, y + 0.8 * scale, z, 0.4 * scale, M_RED if team else M_BLUE)]
    if parent is not None:
        into(parent, *parts)
    return parts


def pawn(name, team, marker=False):
    g = group(name)
    piece(0.0, 0.0, 0.0, team, g, 1.25)
    if marker:
        into(g, inst(ME_CONE, "Hero marker", M_HERO, U(0, 2.5, 0), (0.55, 0.55, 0.9), (math.pi, 0, 0)))
    g.location = U(*HIDE)
    return g


def place(g, ox, p, tumble=0.0):
    if p is None:
        g.location = U(*HIDE)
        return
    g.location = U(ox + p[0], p[1], p[2])
    g.rotation_euler = (D(tumble), 0.0, 0.0)


def figure(name, x, y, z, height, mat, head=M_IVORY, radius=1.0):
    # A chess-piece person: a robe, a collar, a head.
    me = frustum("ME_" + name, radius, radius * 0.55, height * 0.82)
    return [inst(me, name + " robe", mat, U(x, y, z), (1, 1, 1)),
            torus(name + " collar", x, y + height * 0.82, z, radius * 0.5, 0.1, M_GOLD),
            ball(name + " head", x, y + height * 0.9, z, radius * 1.0, head)]


def backdrop(ox, x0, x1, z, y0=0.0, h=16.0):
    box("Back wall", ox + x0, ox + x1, y0, y0 + h, z, z + 0.6, M_STONE)
    n = int((x1 - x0) // 7)
    for i in range(n):
        x = ox + x0 + 3.5 + i * 7
        box("Window", x - 1.2, x + 1.2, y0 + h * 0.45, y0 + h * 0.9, z - 0.05, z, M_DOOR)
        box("Banner", x + 2.2, x + 3.6, y0 + h * 0.40, y0 + h * 0.85, z - 0.12, z - 0.05, M_OLIVE)
        box("Banner mark", x + 2.6, x + 3.2, y0 + h * 0.58, y0 + h * 0.68, z - 0.15, z - 0.12, M_GOLD)


def hall_floor(ox, x0, x1, z0, z1, y=-4.4):
    box("Hall floor", ox + x0, ox + x1, y - 0.4, y, z0, z1, M_TILE)


def table(ox, x0, x1, z0, z1, top=0.0, foot=-4.4):
    box("Table top", ox + x0, ox + x1, top - 0.5, top, z0, z1, M_CLOTH)
    box("Table skirt S", ox + x0 - 0.05, ox + x1 + 0.05, top - 1.3, top, z0 - 0.08, z0, M_CLOTH2)
    for x in (x0 + 0.8, x1 - 0.8):
        for z in (z0 + 0.8, z1 - 0.8):
            box("Table leg", ox + x - 0.35, ox + x + 0.35, foot, top - 1.3, z - 0.35, z + 0.35, M_WALNUT)


def candle(x, y, z, h=1.6):
    cyl("Candle", x, y, y + h, z, 0.36, M_WAX, ME_CYL8)
    ball("Candle flame", x, y + h + 0.2, z, 0.26, M_CFLAME)


SCENES = []


def scene_(title, sub, ox, cam_from, cam_at, pose, hero, length=11.0, lens=30.0):
    SCENES.append(dict(title=title, sub=sub, ox=ox, cam=(cam_from, cam_at), pose=pose, hero=hero, length=length, lens=lens))


HERO = pawn("Hero", 1, True)


# ================================================================ 1 the waiters' parade
def build_waiters(ox):
    box("Floor", ox - 22, ox + 22, -0.4, 0, -10, 8, M_PARQ)
    backdrop(ox, -22, 22, 8.0, 0.0, 14.0)
    for x in (-20.0, 20.0):
        box("Side wall N", ox + x - 0.3, ox + x + 0.3, 0, 14, 3.2, 8, M_STONE)
        box("Side wall S", ox + x - 0.3, ox + x + 0.3, 0, 14, -10, -3.2, M_STONE)
        box("Lintel", ox + x - 0.3, ox + x + 0.3, 7.0, 14, -3.2, 3.2, M_STONE)
        box("Doorway", ox + x + (0.35 if x < 0 else -0.45), ox + x + (0.45 if x < 0 else -0.35), 0, 7.0, -3.2, 3.2, M_DOOR)
    box("Counter", ox - 13, ox - 7, 0, 2.3, -4.6, -1.9, M_WALNUT)
    box("Counter cloth", ox - 13.1, ox - 6.9, 2.3, 2.4, -4.7, -1.85, M_CLOTH)
    for x in (-12.2, -11.2):
        cyl("Spare plates", ox + x, 2.4, 3.2, -4.0, 1.0, M_PLATE)
    box("High table", ox + 5, ox + 14, 4.5, 5.0, -5.2, -1.3, M_CLOTH)
    for x in (5.4, 13.6):
        for z in (-4.8, -2.4):
            box("High table leg", ox + x - 0.3, ox + x + 0.3, 0, 4.5, z - 0.3, z + 0.3, M_WALNUT)
    for x in (7.5, 11.5):
        cyl("Table plate", ox + x, 5.0, 5.08, -3.6, 1.6, M_PLATE)
        candle(ox + x + 1.2, 5.0, -4.4)
    lamps.extend([(ox - 10, 6.5, -3), (ox + 9, 8.0, -3)])
    body = frustum("ME_WaiterCoat", 1.15, 0.6, 5.4)
    waiters = []
    for k in (-1, 0, 1):
        g = group("Waiter")
        into(g, inst(body, "Waiter coat", M_COAT, U(0, 0, 1.7), (1, 1, 1)),
             torus("Waiter collar", 0, 5.45, 1.7, 0.55, 0.12, M_SHIRT),
             box("Waiter bow tie", -0.35, 0.35, 5.2, 5.5, 1.05, 1.2, M_SHIRT),
             ball("Waiter head", 0, 6.05, 1.7, 1.2, M_IVORY),
             ball("Waiter top", 0, 6.75, 1.7, 0.35, M_IVORY),
             bar("Waiter arm", (0, 4.9, 1.2), (0, 2.3, -0.1), 0.36, M_COAT),
             ball("Waiter glove", 0, 2.2, -0.1, 0.55, M_GLOVE),
             box("Waiter towel", -0.3, 0.3, 3.4, 4.3, 0.75, 0.9, M_SHIRT),
             cyl("Tray", 0, 2.25, 2.4, -0.4, 3.0, M_SILVER),
             cyl("Plate stack", 0, 2.4, 5.0, -0.4, 1.6, M_PLATE))
        for r in range(10):
            into(g, torus("Plate rim", 0, 2.47 + r * 0.26, -0.4, 0.8, 0.035, M_GOLD))
        waiters.append((g, k))

    def wx(k, t):
        x = -16.0 + 2.2 * t + 9.0 * k
        return (x + 25.0) % 44.0 - 25.0

    riders = [pawn("Rider", 0), pawn("Rider", 1)]

    def rider_back(t):
        x = wx(-1, t)
        if t < 6.6:
            return (-8.4, 2.4, -3.2)
        if t < 7.0:
            return hop((-8.4, 2.4, -3.2), (x, 2.4, -1.55), (t - 6.6) / 0.4, 0.4)
        if t < 9.1:
            return (x, 2.4 + (t - 7.0) * 1.2, -1.55)
        if t < 9.55:
            return hop((x, 4.92, -1.55), (x, 5.0, -0.4), (t - 9.1) / 0.45, 0.25)
        return (x, 5.0, -0.4)

    def rider_front(t):
        x = wx(1, t)
        if t < 5.6:
            return (x, 5.0, -0.4)
        x0 = wx(1, 5.6)
        if t < 6.1:
            return hop((x, 5.0, -0.4), (x0 + 1.0, 5.0, -2.6), (t - 5.6) / 0.5, 0.3)
        return (min(12.5, x0 + 1.0 + 2.5 * (t - 6.1)), 5.0, -2.6)

    def pose(t):
        for g, k in waiters:
            g.location = U(ox + wx(k, t), 0.06 * abs(math.sin(t * 5.5 + k)), 0.0)
        place(riders[0], ox, rider_back(t))
        place(riders[1], ox, rider_front(t))

    def hero(t):
        x = wx(0, t)
        if t < 2.5:
            return (-10.5, 2.4, -2.7)
        if t < 2.9:
            return hop((-10.5, 2.4, -2.7), (x, 2.4, -1.55), (t - 2.5) / 0.4, 0.4)
        if t < 5.0:
            return (x, 2.4 + (t - 2.9) * 1.2, -1.55)
        if t < 5.45:
            return hop((x, 4.92, -1.55), (x, 5.0, -0.4), (t - 5.0) / 0.45, 0.25)
        if t < 9.7:
            return (x, 5.0, -0.4)
        x0 = wx(0, 9.7)
        if t < 10.2:
            return hop((x, 5.0, -0.4), (x0 + 1.0, 5.0, -2.4), (t - 9.7) / 0.5, 0.3)
        return (min(12.5, x0 + 1.0 + 2.5 * (t - 10.2)), 5.0, -2.4)

    return pose, hero


# ================================================================ 2 the silver cloches
def build_cloches(ox):
    hall_floor(ox, -18, 18, -12, 12)
    backdrop(ox, -18, 18, 11.0, -4.4, 22.0)
    table(ox, -12, 12, -5.5, 4.6)
    box("Shelf", ox - 12, ox + 12, -4.4, 6.4, 5.2, 8.4, M_WALNUT)
    box("Shelf cloth", ox - 12.1, ox + 12.1, 6.4, 6.5, 5.1, 8.5, M_CLOTH)
    for x in (-9.0, -3.5, 3.5, 9.0):
        cyl("Shelf cake", ox + x, 6.5, 7.6, 7.2, 1.8, M_PLATE)
        cyl("Shelf cake top", ox + x, 7.6, 7.9, 7.2, 1.1, M_BERRY)
    lamps.extend([(ox - 6, 6.0, -2), (ox + 6, 6.0, -2)])
    cloches = []
    for i, x in enumerate((-7.0, 0.0, 7.0)):
        cyl("Dish", ox + x, 0, 0.15, 1.9, 4.2, M_PLATE)
        inst(ME_SPH, "Roast", M_ROAST, U(ox + x, 0.75, 1.9), (2.6, 1.6, 1.5))
        g = group("Cloche", (ox + x, 0.0, 1.9))
        into(g, cyl("Cloche band", 0, 0.0, 1.4, 0, 4.4, M_CLOCHE),
             inst(ME_SPH, "Cloche dome", M_CLOCHE, U(0, 1.4, 0), (4.4, 4.4, 3.0)),
             cyl("Cloche stem", 0, 2.85, 3.05, 0, 0.3, M_CLOCHE),
             torus("Cloche handle", 0, 3.42, 0, 0.42, 0.1, M_CLOCHE, (math.pi / 2, 0, 0)),
             ball("Glove", 0, 3.5, 0.0, 1.05, M_GLOVE),
             bar("Sleeve", (0, 3.8, 0.2), (0, 12.0, 4.0), 0.8, M_COAT))
        steam = [ball("Steam", 0, -80, 0, 0.9, M_STEAM) for _ in range(3)]
        cloches.append((g, x, steam))

    def lift(t):
        u = t - 2.6
        if u < 0:
            return 0.0, 0.0
        u %= 7.2
        if u < 0.8:
            return 0.0, math.sin(u * 40.0) * 0.05
        if u < 1.6:
            return 5.6 * ease((u - 0.8) / 0.8), 0.0
        if u < 4.2:
            return 5.6, 0.0
        if u < 4.8:
            k = (u - 4.2) / 0.6
            return 5.6 * (1.0 - k * k), 0.0
        return 0.0, 0.0

    others = [pawn("Climber", 0), pawn("Greedy", 0)]

    def pose(t):
        h, jig = lift(t)
        for i, (g, x, steam) in enumerate(cloches):
            g.location = U(ox + x, h, 1.9)
            g.rotation_euler = (jig * (1 if i % 2 else -1), 0.0, 0.0)
            for n, s in enumerate(steam):
                if h > 0.6:
                    rise = (t * 0.9 + n * 0.33) % 1.0
                    s.location = U(ox + x + (n - 1) * 0.7, 1.4 + rise * 3.4, 1.9 + (n - 1) * 0.4)
                    s.scale = (0.6 + rise,) * 3
                else:
                    s.location = U(*HIDE)
        if t < 4.6:
            place(others[0], ox, (-7.0, 0.96 + h, 4.4))
        elif t < 5.1:
            place(others[0], ox, hop((-7.0, 0.96 + h, 4.4), (-7.0, 6.5, 6.2), (t - 4.6) / 0.5, 0.5))
        else:
            place(others[0], ox, (max(-11.0, -7.0 - (t - 5.1) * 1.6), 6.5, 6.2))
        place(others[1], ox, track(t, K((0, 9.8, 0, -3.8), (4.2, 9.8, 0, -3.8), (5.6, 7.4, 0.15, 0.6))))

    def hero(t):
        h, _ = lift(t)
        if t < 1.6:
            return track(t, K((0, -3.4, 0, -3.6), (1.0, -2.9, 0, 3.4), (1.6, -0.2, 0, 4.4)))
        if t < 2.4:
            return (0.0, (t - 1.6) * 1.2, 4.4)
        if t < 4.4:
            return (0.0, 0.96 + h, 4.4)
        if t < 4.9:
            return (0.0, 6.56 + (t - 4.4) * 0.9, 4.4)
        if t < 5.4:
            return hop((0.0, 7.0, 4.4), (0.0, 6.5, 6.2), (t - 4.9) / 0.5, 0.4)
        return (min(6.0, (t - 5.4) * 1.8), 6.5, 6.2)

    return pose, hero


# ================================================================ 3 the magician's tablecloth trick
def build_trick(ox):
    hall_floor(ox, -20, 24, -12, 10)
    backdrop(ox, -20, 24, 10.0, -4.4, 22.0)
    table(ox, -16, 12, -3.0, 3.0)
    cloth = group("Tablecloth", (ox, 0.0, 0.0))
    into(cloth, box("Cloth top", -16.2, 12.2, 0.0, 0.05, -3.25, 3.25, M_LINEN),
         box("Cloth side S", -16.2, 12.2, -0.9, 0.05, -3.3, -3.25, M_LINEN),
         box("Cloth side N", -16.2, 12.2, -0.9, 0.05, 3.25, 3.3, M_LINEN),
         box("Cloth hem", -16.2, 12.2, -0.95, -0.85, -3.33, -3.3, M_GOLD),
         box("Cloth runner", -16.2, 12.2, 0.05, 0.07, -0.8, 0.8, M_OLIVE))
    for x in (-11.0, -5.0, 1.0, 7.0):
        cyl("Plate", ox + x, 0.05, 0.35, 0.0, 2.4, M_PLATE)
        torus("Plate rim", ox + x, 0.36, 0.0, 1.15, 0.05, M_GOLD)
        cyl("Goblet stem", ox + x + 1.6, 0.05, 0.9, 1.9, 0.18, M_GLASS, ME_CYL8)
        cyl("Goblet bowl", ox + x + 1.6, 0.9, 1.5, 1.9, 0.8, M_GLASS)
        candle(ox + x - 1.6, 0.05, -2.0, 1.2)
    robe = frustum("ME_MagicianRobe", 1.7, 1.0, 9.0)
    mag = group("Magician", (ox + 15.4, -4.4, 0.0))
    into(mag, inst(robe, "Robe", M_ROBE, U(0, 0, 0), (1, 1, 1)),
         torus("Collar", 0, 9.0, 0, 0.9, 0.15, M_GOLD),
         ball("Head", 0, 9.8, 0, 1.5, M_IVORY),
         cyl("Hat brim", 0, 10.4, 10.55, 0, 2.3, M_EBONY),
         cyl("Hat", 0, 10.55, 12.2, 0, 1.4, M_EBONY),
         cyl("Hat band", 0, 10.6, 10.9, 0, 1.43, M_VELVET))
    for s in (-1, 1):
        into(mag, bar("Arm", (-0.4, 8.2, s * 1.0), (-2.8, 4.7, s * 2.6), 0.5, M_ROBE), ball("Hand", -2.8, 4.65, s * 2.6, 0.7, M_GLOVE))
    bundle = inst(ME_SPH, "Cloth in the air", M_LINEN, U(*HIDE), (5.0, 3.0, 1.4))
    drummer = group("Drummer", (ox + 3.0, -4.4, -7.2))
    into(drummer, *figure("Drummer", 0, 0, 0, 4.6, M_COAT, M_EBONY, 0.9))
    into(drummer, cyl("Snare", 0, 2.4, 3.1, -1.2, 1.6, M_SILVER), cyl("Snare head", 0, 3.1, 3.15, -1.2, 1.5, M_HEAD))
    sticks = [bar("Stick", (0, 0, 0), (0, 1, 0), 0.12, M_WALNUT) for _ in range(2)]
    lamps.extend([(ox - 8, 4.0, -4), (ox + 6, 4.0, -4)])

    def cloth_x(t):
        if t < 5.0:
            return 0.0
        if t < 5.3:
            k = (t - 5.0) / 0.3
            return 9.0 * k * k
        if t < 7.5:
            return None
        if t < 9.5:
            return -32.0 * (1.0 - ease((t - 7.5) / 2.0))
        return 0.0

    others = [pawn("On the cloth", 0), pawn("Late", 0), pawn("On a plate", 0), pawn("On a plate", 1)]

    def fly(t, start, t0, land, t1, base_x):
        if t < t0:
            return None
        k = (t - t0) / (t1 - t0)
        if k < 1.0:
            return hop(start, land, k, 2.6), 520.0 * k
        return land, 90.0

    def pose(t):
        cx = cloth_x(t)
        cloth.location = U(*HIDE) if cx is None else U(ox + cx, 0.0, 0.0)
        roll = 3.0 <= t < 5.0
        lean = 5.0 * ease((t - 3.0) / 2.0) if t < 5.0 else (14.0 if t < 5.5 else 14.0 * (1.0 - ease((t - 5.5) / 1.0)))
        mag.rotation_euler = (0.0, D(lean if t >= 3.0 else 0.0), 0.0)
        bundle.location = U(ox + 13.0, 9.4 + 0.3 * math.sin(t * 6.0), 0.0) if 5.3 <= t < 7.5 else U(*HIDE)
        for i, s in enumerate(sticks):
            beat = abs(math.sin(t * 30.0 + i)) * 0.5 if roll else 0.25
            set_bar(s, (3.0 + (i - 0.5) * 0.8, -4.4 + 3.5 + beat, -8.6), (3.0 + (i - 0.5) * 0.4, -4.4 + 3.2, -7.6))
        # one standing on the cloth is flung; one late is flung; two on plates stay
        if t < 5.0:
            place(others[0], ox, (-1.2 + 0.3 * math.sin(t * 2.0), 0.05, 1.2))
        elif t < 5.3:
            place(others[0], ox, (-1.2 + cloth_x(t), 0.05, 1.2))
        else:
            f = fly(t, (7.8, 0.05, 1.2), 5.3, (17.5, -4.4, 5.6), 6.2, 0)
            place(others[0], ox, f[0], f[1])
        if t < 5.0:
            place(others[1], ox, (-16.0 + 2.1 * t, 0.05, -2.0))
        elif t < 5.3:
            place(others[1], ox, (-5.5 + cloth_x(t), 0.05, -2.0))
        else:
            f = fly(t, (3.5, 0.05, -2.0), 5.3, (14.5, -4.4, -6.0), 6.3, 0)
            place(others[1], ox, f[0], f[1])
        place(others[2], ox, (1.0, 0.35 + (0.25 * abs(math.sin(t * 7.0)) if 5.6 < t < 7.0 else 0.0), 0.2))
        place(others[3], ox, (7.0, 0.35, -0.3))

    def hero(t):
        y = 0.05 if cloth_x(t) is not None else 0.0
        keys = K((0, -15.0, 0.05, -0.6), (3.0, -7.5, 0.05, -0.6), (3.7, -5.0, 0.35, 0.0, 0.6), (5.6, -5.0, 0.35, 0.0),
                 (6.0, -3.2, 0.0, -0.6, 0.4), (11.0, 9.0, 0.0, -0.6))
        p = track(t, keys)
        return p if t < 3.0 else (p[0], p[1] if 3.0 <= t < 6.0 else y, p[2])

    return pose, hero


# ================================================================ 4 the champagne tower
def build_champagne(ox):
    hall_floor(ox, -16, 16, -12, 12)
    backdrop(ox, -16, 16, 11.0, -4.4, 24.0)
    table(ox, -7.5, 7.5, -7.5, 7.5)
    bowl_me = frustum("ME_Coupe", 0.55, 0.95, 0.9)
    levels = {}
    for k in range(5):
        n, base = 5 - k, 1.5 * k
        for i in range(n):
            for j in range(n):
                x, z = (i - (n - 1) / 2.0) * 1.9, 1.0 + (j - (n - 1) / 2.0) * 1.9
                cyl("Glass foot", ox + x, base, base + 0.06, z, 1.1, M_GLASS)
                cyl("Glass stem", ox + x, base + 0.06, base + 0.6, z, 0.22, M_GLASS, ME_CYL8)
                levels.setdefault(k, []).append(inst(bowl_me, "Glass bowl", M_FULL, U(ox + x, base + 0.6, z), (1, 1, 1)))
    body = bar("Bottle", (0, 0, 0), (0, 1, 0), 1.5, M_BOTTLE)
    neck = bar("Bottle neck", (0, 0, 0), (0, 1, 0), 0.5, M_BOTTLE)
    label = bar("Bottle label", (0, 0, 0), (0, 1, 0), 1.56, M_GOLD)
    glove = ball("Glove", 0, -80, 0, 1.0, M_GLOVE)
    sleeve = bar("Sleeve", (0, 0, 0), (0, 1, 0), 0.8, M_COAT)
    stream = bar("Pour", (0, 0, 0), (0, 1, 0), 0.22, M_CHAMP)
    lamps.extend([(ox - 5, 6.0, -5), (ox + 5, 9.0, -4)])
    POURS = (1.0, 6.0, 11.0)
    H = (0.0, 8.9, 2.0)

    def tilt(t):
        for p in POURS:
            if p - 0.5 <= t < p:
                return 144.0 * ease((t - p + 0.5) / 0.5), False
            if p <= t < p + 1.2:
                return 144.0, True
            if p + 1.2 <= t < p + 1.6:
                return 144.0 * (1.0 - ease((t - p - 1.2) / 0.4)), False
        return 0.0, False

    def overflow(k, t):
        return any(p + 0.2 + 0.45 * (4 - k) <= t < p + 1.6 + 0.45 * (4 - k) for p in POURS)

    washed = pawn("Washed off", 0)

    def pose(t):
        a, pouring = tilt(t)
        u = (math.cos(D(a)), -math.sin(D(a)))                  # the mouth's way, in (y, z)
        mouth = (ox, H[1] + 0.9 * u[0] + 0.0, H[2] + 0.9 * u[1])
        shoulder = (ox, H[1], H[2])
        base = (ox, H[1] - 3.4 * u[0], H[2] - 3.4 * u[1])
        set_bar(body, base, shoulder)
        set_bar(neck, shoulder, mouth)
        mid = mix(base, shoulder, 0.5)
        set_bar(label, mix(base, shoulder, 0.35), mix(base, shoulder, 0.65))
        glove.location = U(mid[0] + 0.0, mid[1] + 0.2, mid[2] + 0.6)
        set_bar(sleeve, (mid[0], mid[1] + 0.2, mid[2] + 0.6), (ox, 18.0, 8.0))
        if pouring:
            set_bar(stream, mouth, (ox, 7.4, 1.0))
        else:
            stream.location = U(*HIDE)
        for k, bowls in levels.items():
            m = M_CHAMP if overflow(k, t) else M_FULL
            for b in bowls:
                b.material_slots[0].material = m
        if t < 2.55:
            p = track(t, K((0, 2.6, 0, -6.0), (0.7, 1.9, 0, -4.05), (1.95, 1.9, 1.5, -4.05), (2.2, 1.9, 1.55, -3.1, 0.2), (2.55, 1.9, 1.97, -3.1)))
            place(washed, ox, p)
        elif t < 3.0:
            place(washed, ox, hop((1.9, 1.97, -3.1), (2.6, 0.0, -4.9), (t - 2.55) / 0.45, 0.3), 400.0 * (t - 2.55))
        elif t < 4.6:
            place(washed, ox, (2.6, 0.0, -4.9), 90.0)
        else:
            place(washed, ox, track(t, K((4.6, 2.6, 0.0, -4.9), (5.2, 1.9, 0.0, -4.05), (6.45, 1.9, 1.5, -4.05), (6.8, 1.9, 1.55, -3.1, 0.2), (11.0, 1.9, 1.55, -3.1))))

    def hero(t):
        return track(t, K((0, -1.6, 0, -6.2), (0.8, 0, 0, -4.05), (2.05, 0, 1.5, -4.05), (2.45, -0.95, 1.55, -3.1, 0.2), (3.95, -0.95, 1.55, -3.1),
                          (5.2, -0.95, 3.0, -3.1), (5.6, 0, 3.05, -2.15, 0.2), (6.85, 0, 4.5, -2.15), (7.25, -0.95, 4.55, -1.2, 0.2), (8.1, -0.95, 4.55, -1.2),
                          (9.35, -0.95, 6.0, -1.2), (9.75, 0, 6.05, -0.25, 0.2), (11.0, 0, 7.4, -0.25)))

    return pose, hero


# ================================================================ 5 the chocolate fountain
def build_fountain(ox):
    hall_floor(ox, -16, 16, -12, 12)
    backdrop(ox, -16, 16, 11.0, -4.4, 24.0)
    table(ox, -9, 9, -8.5, 8.5)
    cyl("Bowl", ox, 0, 0.6, 0, 10.0, M_SILVER)
    cyl("Pool", ox, 0.55, 0.62, 0, 9.6, M_CHOC)
    R, Y = (4.0, 3.0, 2.0, 1.1), (2.4, 4.8, 7.2, 9.0)
    dome_me = [frustum("ME_Tier%d" % i, r, 0.55, 0.6) for i, r in enumerate(R)]
    bases = [0.62]
    for k in range(1, 4):
        bases.append(Y[k - 1] + 0.6 * (R[k - 1] - R[k]) / (R[k - 1] - 0.55))
    rings = []
    for k in range(4):
        cyl("Curtain", ox, bases[k], Y[k], 0, R[k] * 2, M_CHOC)
        inst(dome_me[k], "Tier", M_CHOC, U(ox, Y[k], 0), (1, 1, 1))
        torus("Tier rim", ox, Y[k], 0, R[k], 0.08, M_SILVER)
        for n in range(4):
            rings.append((torus("Flow", ox, -80, 0, R[k] + 0.03, 0.07, M_CHOC2), k, n))
    cyl("Column", ox, Y[3], Y[3] + 1.2, 0, 0.7, M_SILVER)
    inst(ME_CONE, "Crown", M_SILVER, U(ox, Y[3] + 1.2, 0), (1.4, 1.4, 1.0))
    for i in range(12):
        a = D(i * 30 + 15)
        x, z = 6.2 * math.sin(a), 6.2 * math.cos(a)
        ball("Strawberry", ox + x, 0.45, z, 0.9, M_BERRY)
        inst(ME_CONE, "Leaf", M_LEAF, U(ox + x, 0.8, z), (0.6, 0.6, 0.3))
    for i, a in enumerate((60, 140, 220, 300)):
        x, z = 5.4 * math.sin(D(a)), 5.4 * math.cos(D(a))
        bar("Skewer", (ox + x, 0.0, z), (ox + x * 0.7, 3.0, z * 0.7), 0.1, M_WALNUT)
        ball("Skewer berry", ox + x * 0.78, 2.2, z * 0.78, 0.8, M_BERRY)
    lamps.extend([(ox - 6, 7.0, -6), (ox + 6, 7.0, -6)])
    slider = pawn("Resting", 0)

    def pose(t):
        for o, k, n in rings:
            h = Y[k] - bases[k]
            o.location = U(ox, Y[k] - ((t * 0.5 + n * h / 4.0) % h), 0)
        a = D(145.0)
        if t < 3.4:
            y, r = 4.3 - 0.5 * t, 3.3
            place(slider, ox, (r * math.sin(a), y, r * math.cos(a)))
        elif t < 4.28:
            r = 3.3 + 0.8 * (t - 3.4)
            place(slider, ox, (r * math.sin(a), 2.6 - 0.15 * (r - 3.3), r * math.cos(a)))
        elif t < 4.75:
            p = hop((4.0 * math.sin(a), 2.45, 4.0 * math.cos(a)), (4.4 * math.sin(a), 0.62, 4.4 * math.cos(a)), (t - 4.28) / 0.47, 0.3)
            place(slider, ox, p, 300.0 * (t - 4.28))
        elif t < 6.0:
            place(slider, ox, (4.4 * math.sin(a), 0.62, 4.4 * math.cos(a)), 90.0)
        else:
            y = min(2.42, 0.62 + 0.7 * (t - 6.0))
            place(slider, ox, (4.3 * math.sin(a), y, 4.3 * math.cos(a)))

    def hero(t):
        return track(t, K((0, -2.0, 0, -7.6), (1.0, 0, 0.62, -4.7, 0.5), (1.4, 0, 0.62, -4.3), (3.95, 0, 2.42, -4.3), (4.4, 0, 2.5, -3.75, 0.25),
                          (4.75, 0, 2.6, -3.3), (7.9, 0, 4.8, -3.3), (8.35, 0, 4.9, -2.75, 0.25), (8.7, 0, 5.05, -2.3), (11.0, 0, 6.66, -2.3)))

    return pose, hero


# ================================================================ 6 the stage curtain
def build_curtain(ox):
    box("Stage", ox - 16, ox + 10, -0.5, 0, -8, 5, M_BOARDS)
    box("Pit floor", ox + 10, ox + 16, -5.4, -5.0, -8, 5, M_TILE)
    box("Pit wall W", ox + 10, ox + 10.3, -5.0, 0, -8, 5, M_WALNUT)
    box("Pit wall E", ox + 15.7, ox + 16, -5.0, 0, -8, 5, M_WALNUT)
    box("Far wing", ox + 16, ox + 26, -0.5, 0, -8, 5, M_BOARDS)
    backdrop(ox, -16, 26, 5.0, -5.0, 22.0)
    bar("Curtain rail", (ox - 16, 10.6, 1.0), (ox + 22, 10.6, 1.0), 0.3, M_GOLD)
    box("Pelmet", ox - 16, ox + 22, 10.4, 12.0, 0.6, 1.4, M_VELVET2)
    pleats = []
    for side in (-1, 1):
        for k in range(10):
            g = group("Pleat")
            z = 1.0 + (0.12 if k % 2 else -0.12)
            into(g, box("Pleat cloth", -0.55, 0.55, 0.3, 10.4, z - 0.12, z + 0.12, M_VELVET if k % 2 else M_VELVET2),
                 box("Pleat fringe", -0.55, 0.55, 0.15, 0.35, z - 0.14, z + 0.14, M_GOLD))
            closed = side * (0.55 + 1.05 * k)
            opened = side * (17.0 + 0.3 * k) if side > 0 else -(13.0 + 0.3 * k)
            pleats.append((g, closed, opened))
    bass = group("Double bass", (ox + 12.6, -5.0, 0.5))
    into(bass, box("Bass body", -0.7, 0.7, 0.0, 4.2, -0.3, 0.3, M_WALNUT), bar("Bass neck", (0, 4.2, 0), (0, 7.4, 0), 0.3, M_EBONY),
         ball("Bass scroll", 0, 7.6, 0, 0.5, M_EBONY))
    for x, z in ((11.2, -2.5), (13.8, -3.0), (14.9, -1.0)):
        for o in figure("Musician", ox + x, -5.0, z, 3.6, M_COAT, M_IVORY, 0.7):
            pass
    cond = group("Conductor", (ox + 11.0, -5.0, -5.2))
    into(cond, *figure("Conductor", 0, 0, 0, 4.4, M_COAT, M_EBONY, 0.8))
    baton = bar("Baton", (0, 0, 0), (0, 1, 0), 0.08, M_GLOVE)
    lamps.extend([(ox - 4, 7.0, -4), (ox + 13, 3.0, -4), (ox + 20, 6.0, -3)])

    def s(t):
        return 0.0 if t < 4.6 else ease((t - 4.6) / 4.0)

    def pleat_x(k, t):
        g, closed, opened = pleats[10 + k]
        return closed + (opened - closed) * s(t)

    low = pawn("Hanging low", 0)
    t_hit = 4.6
    while pleat_x(1, t_hit) < 11.9 and t_hit < 20:
        t_hit += 0.005

    def pose(t):
        for g, closed, opened in pleats:
            g.location = U(ox + closed + (opened - closed) * s(t), 0.0, 0.0)
        w = math.sin(t * 3.0)
        set_bar(baton, (ox + 11.0, -5.0 + 3.4, -4.6), (ox + 11.0 + 0.9 * w, -5.0 + 4.6, -4.2))
        if t < 1.0:
            place(low, ox, track(t, K((0, 3.6, 0, -3.4), (1.0, 1.6, 0, 0.6))))
        elif t < 2.0:
            place(low, ox, (1.6, (t - 1.0) * 1.2, 0.6))
        elif t < t_hit:
            place(low, ox, (pleat_x(1, t), 1.2, 0.6))
        elif t < t_hit + 0.6:
            k = (t - t_hit) / 0.6
            place(low, ox, (11.9 - 0.4 * k, 1.2 - 6.2 * k * k, 0.6 - 0.6 * k), 300.0 * k)
        else:
            place(low, ox, (11.5, -5.0, 0.0), 90.0)

    def hero(t):
        if t < 1.0:
            return track(t, K((0, -3.0, 0, -3.0), (1.0, 0.55, 0, 0.6)))
        if t < 4.4:
            return (0.55, (t - 1.0) * 1.2, 0.6)
        if t < 8.6:
            return (pleat_x(0, t), 4.08, 0.6)
        if t < 9.05:
            k = (t - 8.6) / 0.45
            return (17.0 + 0.4 * k, 4.08 * (1.0 - k * k), 0.6 - 1.0 * k)
        return (min(23.0, 17.4 + 2.0 * (t - 9.05)), 0.0, -0.4)

    return pose, hero


# ================================================================ 7 the chimney fire
def build_chimney(ox):
    box("Floor", ox - 14, ox + 14, -0.4, 0, -10, 7, M_PARQ)
    box("Hearth stone", ox - 4.2, ox + 4.2, 0, 0.1, 1.0, 5.5, M_STONE)
    box("Wall W", ox - 14, ox - 4.2, 0, 20, 2.0, 7.0, M_STONE)
    box("Wall E", ox + 4.2, ox + 14, 0, 20, 2.0, 7.0, M_STONE)
    box("Hearth side W", ox - 4.2, ox - 3.5, 0, 6.0, 2.0, 5.5, M_SECTION)
    box("Hearth side E", ox + 3.5, ox + 4.2, 0, 6.0, 2.0, 5.5, M_SECTION)
    box("Breast W", ox - 4.2, ox - 2.0, 6.0, 20, 2.0, 5.5, M_SECTION)
    box("Breast E", ox + 2.0, ox + 4.2, 6.0, 20, 2.0, 5.5, M_SECTION)
    box("Mantel", ox - 5.2, ox + 5.2, 6.0, 6.8, 1.3, 2.6, M_STONE)
    for x in (-4.6, 4.6):
        candle(ox + x, 6.8, 1.9, 1.4)
    # the back of the hearth and the flue, with two holes to hide in (old bread-oven doors)
    box("Back W", ox - 3.5, ox + 0.2, 0, 20, 5.5, 6.4, M_SOOT)
    box("Back E", ox + 2.0, ox + 3.5, 0, 20, 5.5, 6.4, M_SOOT)
    for y0, y1 in ((0.0, 3.0), (4.8, 5.8), (7.6, 20.0)):
        box("Back mid", ox + 0.2, ox + 2.0, y0, y1, 5.5, 6.4, M_SOOT)
    for y in (3.0, 5.8):
        box("Hole floor", ox + 0.2, ox + 2.0, y - 0.2, y, 5.5, 6.9, M_STONE)
        box("Hole back", ox + 0.2, ox + 2.0, y, y + 1.8, 6.9, 7.2, M_SECTION)
        box("Hole lintel", ox + 0.1, ox + 2.1, y + 1.8, y + 2.0, 5.4, 5.6, M_STONE)
    for x0, x1 in ((-1.6, 0.6), (-1.2, 1.0)):
        bar("Log", (ox + x0, 0.35, 3.6), (ox + x1, 0.35, 4.6), 0.55, M_WALNUT)
    embers = [ball("Ember", ox - 0.8 + 0.5 * i, 0.3, 4.1, 0.5, M_EMBER) for i in range(4)]
    base_fire = [inst(ME_CONE, "Fire", M_FLAME, U(ox - 0.6 + 0.5 * i, 0.4, 4.1), (0.8, 0.8, 1.3)) for i in range(3)]
    column = [inst(ME_CONE, "Flame", M_FLAME, U(*HIDE), (3.4, 2.6, 2.2)) for _ in range(13)]
    cook = group("Cook", (ox + 6.8, 0.0, -0.8))
    into(cook, *figure("Cook", 0, 0, 0, 5.0, M_SHIRT, M_IVORY, 0.9))
    into(cook, cyl("Chef hat", 0, 5.0, 6.2, 0, 1.0, M_LINEN))
    bellows = group("Bellows", (ox + 4.5, 1.0, 0.8))
    into(bellows, box("Bellows body", -0.2, 1.8, -0.4, 0.4, -0.6, 0.6, M_WALNUT), bar("Bellows nozzle", (-0.2, 0, 0), (-1.6, -0.2, 1.4), 0.25, M_IRON))
    lamps.extend([(ox, 2.5, 1.0), (ox - 6, 6.0, -4)])
    SURGES = (3.8, 8.0)

    def flame_span(t):
        for s in SURGES:
            if s <= t < s + 2.2:
                h = min(18.0, 0.5 + 12.0 * (t - s))
                b = 0.5 if t < s + 1.2 else 0.5 + 20.0 * (t - s - 1.2)
                return b, h
        return None

    def warning(t):
        return any(s - 0.8 <= t < s for s in SURGES)

    burnt = pawn("Too slow", 0)

    def pose(t):
        span = flame_span(t)
        for i, c in enumerate(column):
            y = 0.8 + 1.4 * i
            if span and span[0] <= y <= span[1]:
                f = 1.0 + 0.12 * math.sin(t * 23.0 + i)
                c.location = U(ox, y, 4.0)
                c.scale = (3.4 * f, 2.6 * f, 2.2)
            else:
                c.location = U(*HIDE)
        hot = warning(t) or span is not None
        for e in embers:
            e.material_slots[0].material = M_EMBER_HOT if hot else M_EMBER
        for i, c in enumerate(base_fire):
            c.scale = (0.8, 0.8, 1.3 + 0.3 * math.sin(t * 9.0 + i) + (1.2 if hot else 0.0))
        squeeze = 0.45 if warning(t) else 1.0
        bellows.scale = (1.0, 1.0, squeeze)
        if t < 3.98:
            place(burnt, ox, track(t, K((0, -5.5, 0, -2.4), (0.9, -1.1, 0, 5.2), (3.98, -1.1, 2.62, 5.2))))
        elif t < 4.45:
            place(burnt, ox, hop((-1.1, 2.62, 5.2), (-2.7, 0.1, 3.0), (t - 3.98) / 0.47, 0.2), 380.0 * (t - 3.98))
        elif t < 6.5:
            place(burnt, ox, (-2.7, 0.1, 3.0), 90.0)
        else:
            place(burnt, ox, (-2.7, 0.1, 3.0))

    def hero(t):
        return track(t, K((0, -5.0, 0, -3.0), (1.0, 1.1, 0, 5.2), (3.67, 1.1, 3.2, 5.2), (3.95, 1.1, 3.0, 6.1, 0.15), (5.3, 1.1, 3.0, 6.1),
                          (5.6, 1.1, 3.2, 5.2), (7.93, 1.1, 6.0, 5.2), (8.2, 1.1, 5.8, 6.1, 0.15), (9.6, 1.1, 5.8, 6.1), (9.9, 1.1, 6.0, 5.2),
                          (11.0, 1.1, 7.3, 5.2)))

    return pose, hero


# ================================================================ 8 the timpani
def build_timpani(ox):
    box("Platform", ox - 15, ox + 15, -0.4, 0, -9, 7, M_PARQ)
    backdrop(ox, -15, 15, 7.0, 0.0, 18.0)
    box("Gallery", ox + 8, ox + 15, 0, 6.4, -3.5, 4.5, M_WALNUT)
    box("Gallery floor", ox + 7.9, ox + 15, 6.3, 6.45, -3.6, 4.6, M_CLOTH)
    for i, (x0, x1, top) in enumerate(((-11.2, -10.4, 0.5), (-10.4, -9.6, 1.0), (-9.6, -8.8, 1.5))):
        box("Step", ox + x0, ox + x1, 0, top, -1.2, 1.2, M_WALNUT)
    DRUMS = ((-7.0, 1.6), (-1.5, 3.0), (4.0, 4.4))
    kettle = frustum("ME_Kettle", 1.0, 1.9, 1.5)
    heads, mallets = [], []
    for i, (x, top) in enumerate(DRUMS):
        for a in (0, 120, 240):
            bar("Drum leg", (ox + x + 0.9 * math.sin(D(a)), 0, 0.9 * math.cos(D(a))), (ox + x + 0.7 * math.sin(D(a)), top - 1.5, 0.7 * math.cos(D(a))), 0.18, M_BRASS)
        inst(kettle, "Kettle", M_COPPER, U(ox + x, top - 1.5, 0.0), (1, 1, 1))
        torus("Drum rim", ox + x, top, 0.0, 1.9, 0.1, M_GOLD)
        heads.append(cyl("Drum head", ox + x, top - 0.05, top + 0.05, 0.0, 3.7, M_HEAD))
        pivot = (ox + x, top + 4.2, 2.6)
        g = group("Mallet", pivot)
        into(g, bar("Mallet stick", (0, 0, 0), (0, -4.57, 0), 0.16, M_WALNUT), ball("Mallet head", 0, -4.57, 0, 0.75, M_HEAD))
        mallets.append(g)
        stand = max(0.0, top - 1.6)
        if stand > 0.0:
            box("Riser", ox + x - 1.2, ox + x + 1.2, 0.0, stand, 2.4, 4.6, M_WALNUT)
        for o in figure("Timpanist", ox + x, stand, 3.4, 5.6, M_COAT, M_EBONY, 0.85):
            pass
    cond = group("Conductor", (ox - 11.5, 0.0, -4.5))
    into(cond, *figure("Conductor", 0, 0, 0, 4.4, M_COAT, M_IVORY, 0.8))
    baton = bar("Baton", (0, 0, 0), (0, 1, 0), 0.08, M_GLOVE)
    lamps.extend([(ox - 4, 8.0, -4), (ox + 6, 9.0, -4)])
    ROUNDS = (2.0, 8.0)
    LEDGE = (9.8, 6.45, 0.2)

    def strike(i, t):
        # (raise 0..1, struck just now)
        for r in ROUNDS:
            s = r + i
            if s - 0.6 <= t < s:
                return ease((t - s + 0.6) / 0.4), False
            if s <= t < s + 0.15:
                return 0.0, True
        return 0.0, False

    def arc(t, t0, a, b):
        k = (t - t0) / 1.0
        p = mix(a, b, k)
        top = max(a[1], b[1]) + 2.4
        return (p[0], a[1] + (b[1] - a[1]) * k + (top - (a[1] + b[1]) / 2.0) * 4.0 * k * (1.0 - k), p[2])

    def chain(t, start_round, start_drum, z):
        # Where a pawn standing on start_drum before start_round + start_drum is, launched on every beat after.
        spots = [(x, top + 0.05, z) for x, top in DRUMS] + [(LEDGE[0] + z, LEDGE[1], LEDGE[2] + z)]
        t0 = start_round + start_drum
        if t < t0:
            return spots[start_drum]
        for i in range(start_drum, 3):
            ti = start_round + i
            if t < ti + 1.0:
                return arc(t, ti, spots[i], spots[i + 1])
        return (min(14.0, spots[3][0] + 1.5 * (t - start_round - 3.0)), spots[3][1], spots[3][2])

    others = [pawn("Riding along", 0), pawn("Missed the beat", 0)]

    def pose(t):
        for i, (x, top) in enumerate(DRUMS):
            up, hit = strike(i, t)
            mallets[i].rotation_euler = (D(-30.2 + 75.0 * up), 0.0, 0.0)
            heads[i].location = U(ox + x, top - (0.12 if hit else 0.0), 0.0)
        beat = (t % 1.0)
        set_bar(baton, (ox - 11.5, 3.6, -4.2), (ox - 11.5 + 0.5, 4.6 + 0.6 * math.cos(beat * math.pi * 2.0), -4.0))
        place(others[0], ox, chain(t, 2.0, 1, 0.8))
        if t < 2.4:
            place(others[1], ox, track(t, K((0, -13.5, 0, 1.2), (1.3, -10.8, 0.5, 0.8, 0.3), (1.7, -10.0, 1.0, 0.8, 0.3), (2.0, -9.2, 1.5, 0.8, 0.3), (2.4, -7.0, 1.65, 0.8, 0.4))))
        else:
            place(others[1], ox, chain(t, 8.0, 0, 0.8))

    def hero(t):
        if t < 1.7:
            return track(t, K((0, -12.5, 0, -0.4), (0.7, -10.8, 0.5, 0, 0.3), (1.0, -10.0, 1.0, 0, 0.3), (1.3, -9.2, 1.5, 0, 0.3), (1.7, -7.0, 1.65, 0, 0.4)))
        return chain(t, 2.0, 0, 0.0)

    return pose, hero


# ---------------------------------------------------------------- the scenes, in order
SPACING = 80.0
p, h = build_waiters(0 * SPACING)
scene_("1 · 웨이터 행렬 — 쟁반에 실려 간다", "카운터에서 쟁반에 올라타 접시 탑(2.6 m)을 오른다 · 웨이터는 멈추지 않는다 — 높은 식탁을 지나기 전에 꼭대기로",
       0 * SPACING, (-1.0, 13.0, -25.0), (-1.0, 3.0, -0.5), p, h)
p, h = build_cloches(1 * SPACING)
scene_("2 · 은 뚜껑 — 일제히 들린다", "장갑이 손잡이를 쥐면(흔들림) 모든 뚜껑이 '짜잔!' · 띠에 매달려 있으면 선반 위로, 뚜껑 밑에 있으면 다음 차례까지 갇힌다",
       1 * SPACING, (0.0, 11.0, -18.0), (0.0, 3.2, 2.5), p, h)
p, h = build_trick(2 * SPACING)
scene_("3 · 마술사의 식탁보 빼기", "북이 울리면(2초) 접시 위로 · 식탁보가 순식간에 빠진다 — 접시에 선 폰만 남고, 천 위의 폰은 식탁 밖으로 날아간다",
       2 * SPACING, (1.5, 14.0, -27.0), (1.5, 1.4, 0.0), p, h, lens=28.0)
p, h = build_champagne(3 * SPACING)
scene_("4 · 샴페인 잔 탑 — 넘쳐흐르는 물결 사이로", "부으면 넘친 샴페인이 층층이 내려온다(빛나는 층) · 넘치는 층의 잔에 매달려 있으면 씻겨 내려간다 — 턱에서 기다렸다 오른다",
       3 * SPACING, (6.0, 7.0, -17.0), (0.0, 3.8, -0.5), p, h, lens=32.0)
p, h = build_fountain(4 * SPACING)
scene_("5 · 초콜릿 분수 — 흘러내리는 벽을 거슬러", "초콜릿이 계속 흘러내린다 · 매달려 쉬면 밀려 내려가고, 층 위에서는 바깥으로 밀린다 — 쉬지 말고 오른다 (한 층 약 3초)",
       4 * SPACING, (3.5, 8.5, -18.0), (0.0, 4.4, -1.0), p, h)
p, h = build_curtain(5 * SPACING)
scene_("6 · 무대 막 — 걷히는 막에 매달려 건넌다", "막이 열리면 매달린 폰을 악단석 너머로 실어 간다(16 m) · 낮게 매달리면 콘트라베이스 머리에 걸려 떨어진다 — 3.5 m 위로",
       5 * SPACING, (7.0, 9.0, -21.0), (6.5, 3.2, 0.5), p, h)
p, h = build_chimney(6 * SPACING)
scene_("7 · 굴뚝의 불길 — 솟는 불을 피해 오른다", "요리사가 풀무를 누르면(불씨가 밝아진다) 불길이 굴뚝 끝까지 솟는다 · 벽의 구멍에 들어가 있으면 산다 — 벽에 붙어 있으면 떨어진다",
       6 * SPACING, (0.0, 8.5, -15.0), (0.0, 6.5, 4.0), p, h)
p, h = build_timpani(7 * SPACING)
scene_("8 · 팀파니 — 북을 칠 때 튀어 오른다", "채가 들리면(예고) 곧 친다 · 북 위에 서 있으면 다음 북으로 튄다 — '둥 · 둥 · 둥!' 세 번 이어 타면 위층, 놓치면 다음 소절까지 기다린다",
       7 * SPACING, (0.5, 9.5, -20.0), (1.0, 3.6, 0.5), p, h)

INTRO = dict(title="대연회장 장애물 아이디어 — 여덟 가지", sub="'대연회장 하면 떠오르는 것'에서 골라 폰 러쉬 장애물로 · 구현 전 기획 영상 · 폰의 움직임은 기획 수치로 그린 것이다",
             ox=0.0, cam=((-1.0, 24.0, -42.0), (-1.0, 2.0, 0.0)), pose=SCENES[0]["pose"], hero=None, length=4.5, lens=30.0)
OUTRO = dict(title="고른 것을 대연회장 v0.4에 넣는다", sub="어느 구간에 어울리는지는 문서에 · 숫자는 대략이고 회색 상자에서 맞춘다 · Unity에 만든 것은 없다",
             ox=7 * SPACING, cam=((0.5, 14.0, -30.0), (1.0, 3.0, 0.5)), pose=SCENES[7]["pose"], hero=None, length=5.0, lens=30.0, t0=3.0)
CUTS = [INTRO] + SCENES + [OUTRO]
for c in SCENES:
    c["pose"](0.0)
STARTS, total = [], 0
for c in CUTS:
    STARTS.append(total)
    total += int(round(c["length"] * FPS))
print("FILM", total, "frames,", round(total / FPS, 1), "s")

# ---------------------------------------------------------------- camera, captions, light
cam_d = bpy.data.cameras.new("CAM_Ideas")
cam_d.clip_end = 900
cam_d.sensor_width = 36.0
cam = bpy.data.objects.new("CAM_Ideas", cam_d)
col.objects.link(cam)
scene.camera = cam


def aim(o, target):
    o.rotation_euler = (Vector(target) - o.location).to_track_quat("-Z", "Y").to_euler()


M_TEXT = principled("MAT_Caption", (1, 1, 1), 0.5, 0.0, (1.0, 0.97, 0.9), 2.2)
M_TEXT2 = principled("MAT_CaptionSmall", (1, 1, 1), 0.5, 0.0, (1.0, 0.86, 0.6), 1.6)
M_BAR = principled("MAT_CaptionBar", (0.0, 0.0, 0.0), 1.0, 0.0, (0.02, 0.012, 0.006), 1.0)
font = None
for name in ("malgunbd.ttf", "malgun.ttf"):
    path = os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name)
    if os.path.exists(path):
        font = bpy.data.fonts.load(path)
        break
AHEAD = 0.5


def frame_size(lens):
    half_w = AHEAD * 18.0 / lens
    return half_w, half_w * FILM_H / FILM_W


def caption(body, size, mat):
    cu = bpy.data.curves.new("Caption", "FONT")
    cu.body = body
    cu.size = size
    if font is not None:
        cu.font = font
    o = bpy.data.objects.new("Caption", cu)
    col.objects.link(o)
    o.data.materials.append(mat)
    o.parent = cam
    o.location = (0, 0, AHEAD * 40.0)
    return o


bar_o = inst(ME_CUBE, "Caption bar", M_BAR, (0, 0, 0), (1, 1, 1))
bar_o.parent = cam
CAPTIONS = []
for c in CUTS:
    half_w, half_h = frame_size(c["lens"])
    CAPTIONS.append((caption(c["title"], half_h * 0.105, M_TEXT), caption(c["sub"], half_h * 0.052, M_TEXT2)))


def light(name, kind, loc, energy, color, **kw):
    d = bpy.data.lights.new(name, kind)
    d.energy = energy
    d.color = color
    for k, v in kw.items():
        setattr(d, k, v)
    o = bpy.data.objects.new(name, d)
    col.objects.link(o)
    o.location = loc
    return o


world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.13, 0.10, 1)
sun = light("LGT_Sun", "SUN", (0, 0, 0), 2.6, (1.0, 0.90, 0.74), angle=D(8))
sun.rotation_euler = (D(50), 0.0, D(-30))
back = light("LGT_Back", "SUN", (0, 0, 0), 0.9, (0.72, 0.80, 1.0), angle=D(20))
back.rotation_euler = (D(-55), 0.0, D(160))
back.data.use_shadow = False
for (x, y, z) in lamps:
    o = light("LGT_Lamp", "POINT", U(x, y, z), 1400, (1.0, 0.62, 0.30), shadow_soft_size=0.5)
    o.data.use_shadow = False
scene.render.engine = "BLENDER_EEVEE"
scene.eevee.taa_render_samples = 12
scene.view_settings.view_transform = "AgX"
for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
    try:
        scene.view_settings.look = look
        break
    except Exception:
        pass
scene.view_settings.exposure = -0.35
scene.render.resolution_x, scene.render.resolution_y = FILM_W, FILM_H
scene.render.resolution_percentage = 100
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 1, total


def show(frame):
    index = max(i for i, s in enumerate(STARTS) if s <= frame - 1)
    c = CUTS[index]
    n = frame - 1 - STARTS[index]
    t = c.get("t0", 0.0) + n * DT
    c["pose"](t)
    p = c["hero"](t) if c["hero"] else None
    place(HERO, c["ox"], p)
    cam_d.lens = c["lens"]
    (fx, fy, fz), (ax, ay, az) = c["cam"]
    k = n / float(max(1, int(c["length"] * FPS) - 1))
    drift = 0.03 * (k - 0.5)
    cam.location = U(c["ox"] + fx + (ax - fx) * drift, fy, fz + (az - fz) * drift)
    aim(cam, U(c["ox"] + ax, ay, az))
    half_w, half_h = frame_size(c["lens"])
    bar_o.scale = (half_w * 2.1, half_h * 0.42, 0.002)
    bar_o.location = (0, -half_h * 0.81, -AHEAD - 0.004)
    for i, (a, b) in enumerate(CAPTIONS):
        if i == index:
            a.location = (-half_w * 0.94, -half_h * 0.76, -AHEAD)
            b.location = (-half_w * 0.94, -half_h * 0.92, -AHEAD)
        else:
            a.location = b.location = (0, 0, AHEAD * 40.0)


def on_frame(sc, depsgraph=None):
    show(sc.frame_current)


if STILLS:
    scene.render.image_settings.file_format = "PNG"
    for i, start in enumerate(STARTS):
        frame = start + 1 + int(CUTS[i]["length"] * FPS * 0.5)
        scene.frame_set(frame)
        show(frame)
        scene.render.filepath = os.path.join(FILM_OUT, "cut_%02d.png" % i)
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
