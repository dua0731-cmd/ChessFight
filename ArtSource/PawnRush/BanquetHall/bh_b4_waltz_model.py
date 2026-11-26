"""B4 the dance floor and the waltz (seoungtest R837, 2026-11-25; PLAN.md B4): the chessboard floor after the R836
sheet BH_T1's chessboard swatch (cream marble and walnut squares) and the R833 hall, and the ten dancing couples - the
user's figurines (bh_figures: "queen" and "partner", BH_A10; user 11-25 "인형 장식품은 이 에셋을 써") fitted to the
builder's colliders.

    blender -b --factory-startup --python bh_b4_waltz_model.py -- [stills] [export]
    -> bh_b4_waltz.blend; "stills": stills/b4/parts/<part>_compare.png + stills/b4/waltz.png;
       "export": Assets/Resources/BanquetHall/Models/B4/*.fbx (never while Unity is in Play).

Numbers are GrandBanquetHallBuilder's (Waltz) and BanquetWaltz's (BanquetMovers.cs); colliders and timing stay theirs:
  dance floor  x -32 .. 11.6, z 6.2 .. 56, top 0.02 (a run ends on it: nobody walks it)
  a couple     root "BH couple k" (k 1 .. 10) on the dance's track, turning; in its frame the queen at x +1.9, the
               partner (the builder's "king") at x -1.9:
               queen's gown  a cylinder r 1.3, 0 .. 7.4 - climbed; its top, the shoulders, a floor at 7.4
               queen's head  a ball r 0.6 at 8.0 (not climbed); her crown 8.6 .. 9.1 (no collider)
               partner       a cylinder r 1.3, 0 .. 7.4, head ball r 0.6 at 8.0 (neither climbed)
               joined arms   at 6.9 between them (no collider)
The figurines keep their own proportions (user 11-25 "너무 찌그러 졌어" on a first fit stretched 2.3 times up): each
at one scale - the queen's shoulder plate to the gown's r 1.3, the partner's head to the head ball's 0.6 - their
bodies only 1.12 taller, each standing on a music-box doll's turned pedestal (inside the collider, r 1.3) that puts
the shoulders on 7.4. The partner's reaching arm drawn out to her plate.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\BanquetHall"
sys.path.insert(0, HERE)
import importlib
import bh_kit as k
import bh_parts as P
import bh_stills as st
import bh_figures as F
for _m in (k, P, st, F):
    importlib.reload(_m)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
A10 = os.path.join(st.SHEETS, "BH_A10_royals_dancers.png")
T1 = os.path.join(st.SHEETS, "BH_T1_material_swatches.png")
CELLS = {"queen": (A10, (360, 250, 650, 760)), "partner": (A10, (665, 275, 955, 760)), "couple": (A10, (1015, 255, 1505, 760)),
         "floor": (T1, (448, 685, 768, 1010))}

STAGE_Y, PAIR, GOWN_R, HEAD_R = 7.4, 1.9, 1.3, .6
FLOOR_X0, FLOOR_X1, FLOOR_Z0, FLOOR_Z1, FLOOR_TOP = -32.0, 11.6, 6.2, 56.0, .02
BORDER, INLAY = .5, .06             # the walnut border round the board, the gold line inside it
COLS, ROWS = 14, 16                 # squares across x, along z (about 3.05 m each)
# BanquetWaltz (BanquetMovers.cs), for the stills: the couples at clock 0
COUPLES, SPEED = 10, 2.5
TRACK_W, TRACK_E, TRACK_S, TRACK_N, BOARD_X = -16.2, 7.5, 10.2, 51.8, -4.25


def U(x, y, z):
    return Vector((x, z, y))


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    for o in list(c.objects):
        bpy.data.objects.remove(o)
    return c


# ------------------------------------------------------------------ the dance floor

def _block(bm, x0, x1, z0, z1, y0, y1, mat):
    """A box in Unity numbers (x, z across the floor, y up)."""
    r = bmesh.ops.create_cube(bm, size=1.0)
    vs = r["verts"]
    bmesh.ops.transform(bm, verts=vs, matrix=Matrix.Translation(U((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @
                        Matrix.Diagonal((x1 - x0, z1 - z0, y1 - y0, 1)))
    for f in {f for v in vs for f in v.link_faces}:
        f.material_index = mat


def dance_floor(col):
    """The chessboard: 14 x 16 squares of cream marble and walnut, a chamfered joint between them, inside a gold line
    and a walnut border, all one slab 0 .. 0.02 over the builder's dance floor (its top)."""
    LIGHT, DARK, GOLD, WALNUT = range(4)
    bm = bmesh.new()
    x0, x1 = FLOOR_X0 + BORDER + INLAY, FLOOR_X1 - BORDER - INLAY
    z0, z1 = FLOOR_Z0 + BORDER + INLAY, FLOOR_Z1 - BORDER - INLAY
    dx, dz = (x1 - x0) / COLS, (z1 - z0) / ROWS
    for i in range(COLS):
        for j in range(ROWS):
            _block(bm, x0 + i * dx, x0 + (i + 1) * dx, z0 + j * dz, z0 + (j + 1) * dz, 0.0, FLOOR_TOP,
                   LIGHT if (i + j) % 2 == 0 else DARK)
    # the gold line round the board and the walnut border round that
    for (a0, a1, b0, b1) in ((x0 - INLAY, x1 + INLAY, z0 - INLAY, z0), (x0 - INLAY, x1 + INLAY, z1, z1 + INLAY),
                             (x0 - INLAY, x0, z0, z1), (x1, x1 + INLAY, z0, z1)):
        _block(bm, a0, a1, b0, b1, 0.0, FLOOR_TOP, GOLD)
    X0, X1, Z0, Z1 = FLOOR_X0, FLOOR_X1, FLOOR_Z0, FLOOR_Z1
    for (a0, a1, b0, b1) in ((X0, X1, Z0, z0 - INLAY), (X0, X1, z1 + INLAY, Z1),
                             (X0, x0 - INLAY, z0 - INLAY, z1 + INLAY), (x1 + INLAY, X1, z0 - INLAY, z1 + INLAY)):
        _block(bm, a0, a1, b0, b1, 0.0, FLOOR_TOP, WALNUT)
    ob = k.new_object("BH4 dance floor", bm, col, ("floor_light", "floor_dark", "gold", "walnut"))
    k.bevel(ob, .008, 1)
    k.apply_modifiers(ob)
    k.surface_uvs(ob)
    return ob


# ------------------------------------------------------------------ a couple (the user's figurines fitted)

def _centre_on_base(ob):
    """Move the mesh so its base's middle (the lowest 5 %) is on the z axis (the partner's arm skews the bounding box)."""
    vs = ob.data.vertices
    h = max(v.co.z for v in vs)
    base = [v.co for v in vs if v.co.z < .05 * h]
    cx = (min(v.x for v in base) + max(v.x for v in base)) / 2
    cy = (min(v.y for v in base) + max(v.y for v in base)) / 2
    ob.data.transform(Matrix.Translation((-cx, -cy, 0)))
    return h


BODY_K = 1.12          # the figurines' bodies a little taller than their own proportion (barely seen), no more


def _place(ob, s, shoulder, arm=None):
    """The figurine at one scale `s` (its own proportions; user 11-25 "너무 찌그러 졌어" on the stretched fit), its
    body (under `shoulder`) only BODY_K taller, set up on a pedestal so the shoulder lands on 7.4. Returns the
    pedestal's height. arm = (z0, z1, from x, reach): the arm's verts beyond x drawn out along +x to `reach` m."""
    vs = ob.data.vertices
    ped = STAGE_Y - shoulder * s * BODY_K
    if arm:
        band = [v.co for v in vs if arm[0] <= v.co.z <= arm[1]]
        tip = max(c.x for c in band)
        stretch = (arm[3] - arm[2] * s) / (tip - arm[2])
    for v in vs:
        x, y, z = v.co
        nx = x * s
        if arm and arm[0] <= z <= arm[1] and x > arm[2]:
            nx = arm[2] * s + (x - arm[2]) * stretch
        nz = ped + z * s * BODY_K if z <= shoulder else STAGE_Y + (z - shoulder) * s
        v.co = Vector((nx, y * s, nz))
    ob.data.update()
    return ped


def pedestal(col, name, height, r_top, colour):
    """A music-box doll's turned stand under a figurine, inside its collider (r 1.3): a gold foot of two rings, a
    plain column in the figure's colour between gold bands, a gold cap ring the figurine's base sits on."""
    G = P.GOLD
    prof = [(0.0, 0.0), (1.3, 0.0), (1.3, .14), (1.22, .2), (1.24, .3), (1.16, .36),          # foot
            (1.08, .44), (1.08, height - .7),                                                  # column
            (1.14, height - .64), (1.14, height - .52), (1.08, height - .46),                  # gold band
            (1.08, height - .3), (r_top + .04, height - .18), (r_top + .04, height - .04),    # cap
            (r_top, height), (0.0, height)]
    mats = [G, G, G, G, G, G, colour, G, G, G, colour, G, G, G, G]
    ob = P.obj(col, name, k.lathe(prof, 48, mat=mats))
    P.finish(ob, bevel=.015)
    return ob


def queen(col, name):
    """BH_A10 queen at her own proportions: her shoulder plate r 1.3 (the collider's top) flat on 7.4, the head then
    the head ball's 0.6; she stands on an olive pedestal (the gown collider 0 .. 7.4 is climbed: a pawn clings to the
    stand or the skirt). Her gown's holes (the model's) are backed by an olive underskirt."""
    ob = F.figure("queen", col, name, islands=True)
    h = _centre_on_base(ob)
    vs = [v.co for v in ob.data.vertices]
    plate = [v for v in vs if .55 * h < v.z < .72 * h]
    plate_r = max(math.hypot(v.x, v.y) for v in plate)
    top = max(v.z for v in plate if math.hypot(v.x, v.y) > .8 * plate_r)       # the plate's top (the outer ring)
    s = GOWN_R / plate_r
    base_r = max(math.hypot(v.x, v.y) for v in vs if v.z < .02 * h) * s
    ped = _place(ob, s, top)
    # the underskirt: at each 0.2 m of the skirt the fitted gown's inner tenth of radii there, 4 % in
    prof = []
    fitted = [(v.co.z, math.hypot(v.co.x, v.co.y)) for v in ob.data.vertices]
    y, y_top = ped + .05 * h * s * BODY_K, ped + .4 * h * s * BODY_K
    while y <= y_top:
        rs = sorted(r for z, r in fitted if abs(z - y) < .12)
        if rs:
            prof.append((rs[len(rs) // 10] * .96, y))
        y += .2
    under = P.obj(col, name + " underskirt", k.lathe(prof, 40, mat=P.OLIVE))
    P.finish(under)
    stand = pedestal(col, name + " stand", ped, base_r, P.OLIVE)
    return [ob, under, stand]


def partner(col, name):
    """BH_A10 partner (black tailcoat) at his own proportions, the head the head ball's 0.6, his shoulders on 7.4 on
    an ebony pedestal; his left arm drawn out to the queen's plate (her middle 3.8 m off, her plate r 1.3)."""
    ob = F.figure("partner", col, name, islands=True)
    h = _centre_on_base(ob)
    vs = [v.co for v in ob.data.vertices]
    shoulder = .70 * h
    s = .64 / max(math.hypot(v.x, v.y) for v in vs if v.z > .78 * h)
    base_r = max(math.hypot(v.x, v.y) for v in vs if v.z < .02 * h) * s
    ped = _place(ob, s, shoulder, arm=(.5 * h, .74 * h, .068, 2.6))
    return [ob, pedestal(col, name + " stand", ped, base_r, P.EBONY)]


def couple(col, tag):
    """One couple in its root's frame (Blender x = the root's +x): the queen at +1.9 and the partner at -1.9, each
    turned 15 degrees to the other, both facing the root's -z."""
    qs = queen(col, "BH4 queen" + tag)
    for q in qs:
        q.data.transform(Matrix.Rotation(math.radians(-15), 4, "Z"))
        q.data.transform(Matrix.Translation(U(PAIR, 0, 0)))
    ps = partner(col, "BH4 partner" + tag)
    for p in ps:
        p.data.transform(Matrix.Rotation(math.radians(15), 4, "Z"))
        p.data.transform(Matrix.Translation(U(-PAIR, 0, 0)))
    return qs + ps


# ------------------------------------------------------------------ the dance at clock 0 (stills only)

def _track(s):
    stop = 2 * math.pi * PAIR
    r = (2 * (TRACK_E - TRACK_W) + 2 * (TRACK_N - TRACK_S) - COUPLES * stop) / (8 - 2 * math.pi)
    length = COUPLES * stop
    w, e, so, n = TRACK_W, TRACK_E, TRACK_S, TRACK_N
    d = s % length
    quarter = .5 * math.pi * r
    north, side = (e - r) - (w + r), (n - r) - (so + r)

    def line(a, b, t):
        return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)

    def arc(c, start, along):
        a = math.radians(start) + along / r
        return (c[0] + r * math.cos(a), c[1] + r * math.sin(a))
    if d <= north: return line((e - r, n), (w + r, n), d / north)
    d -= north
    if d <= quarter: return arc((w + r, n - r), 90, d)
    d -= quarter
    if d <= side: return line((w, n - r), (w, so + r), d / side)
    d -= side
    if d <= quarter: return arc((w + r, so + r), 180, d)
    d -= quarter
    if d <= north: return line((w + r, so), (e - r, so), d / north)
    d -= north
    if d <= quarter: return arc((e - r, so + r), 270, d)
    d -= quarter
    if d <= side: return line((e, so + r), (e, n - r), d / side)
    d -= side
    return arc((e - r, n - r), 0, min(d, quarter))


def pose(i):
    stop = 2 * math.pi * PAIR
    r = (2 * (TRACK_E - TRACK_W) + 2 * (TRACK_N - TRACK_S) - COUPLES * stop) / (8 - 2 * math.pi)
    board_s = TRACK_E - r - BOARD_X
    x, z = _track(board_s + i * stop)
    return x, z, .5 * math.pi   # bearing at clock 0


# ------------------------------------------------------------------ build, stills, export

def build():
    scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o)
    got = {"floor": [dance_floor(collection("BH4 dance floor"))], "couple": couple(collection("BH4 couple"), "")}
    k.preview_materials()
    print("[bh_b4] tris", {n: k.tris(o) for n, o in got.items()})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b4_waltz.blend"))
    return got


def proxy(centre, size):
    p = bpy.data.objects.new("_proxy", bpy.data.meshes.new("_proxy"))
    b = bmesh.new()
    bmesh.ops.create_cube(b, size=1.0)
    b.transform(Matrix.Diagonal(tuple(size) + (1,)))
    b.to_mesh(p.data)
    p.location = centre
    bpy.context.scene.collection.objects.link(p)
    bpy.context.view_layer.update()
    return p


def stills(got):
    scene = bpy.context.scene
    out = os.path.join(HERE, "stills", "b4", "parts")
    os.makedirs(out, exist_ok=True)
    cam = st.setup_render(scene)
    figs = got["couple"]
    qs, ps = figs[:3], figs[3:]
    jobs = (("queen", qs, U(PAIR, 4.6, 0), (3.2, 9.2, 3.2), 0, 6, 60),
            ("partner", ps, U(-PAIR + .6, 4.4, 0), (4.4, 8.8, 3), 0, 6, 60),
            ("couple", figs, U(0, 4.6, 0), (7.6, 9.2, 3.2), 0, 6, 60),
            ("floor", got["floor"], U(-26, 0, 12), (7, .1, 7), -30, 55, 40))
    for name, objs, c, size, yaw, pitch, lens in jobs:
        pr = proxy(c, size)
        st.frame(cam, [pr], math.radians(yaw), math.radians(pitch), lens, .95)
        bpy.data.objects.remove(pr)
        path = os.path.join(out, name + "_render.png")
        st.render_only(scene, objs, path)
        st.compare(*CELLS[name], path, os.path.join(out, name + "_compare.png"))
    # the whole dance at clock 0: ten couples on the floor
    shown = list(got["floor"])
    for i in range(COUPLES):
        x, z, b = pose(i)
        m = Matrix.Translation(U(x, FLOOR_TOP * 0, z)) @ Matrix.Rotation(b, 4, "Z")
        for src in figs:
            o = src.copy()
            o.matrix_world = m
            scene.collection.objects.link(o)
            shown.append(o)
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    pr = proxy(U(-10, 4, 31), (46, 10, 52))
    st.frame(cam, [pr], math.radians(-25), math.radians(35), 30, 1.1)
    bpy.data.objects.remove(pr)
    st.render_only(scene, shown, os.path.join(HERE, "stills", "b4", "waltz.png"))
    for o in shown[1:]:
        bpy.data.objects.remove(o)


def export(got):
    """B4 to Unity (GrandBanquetHallDressing.Waltz.cs): the floor in the section's frame, the couple in a couple
    root's frame (built there; hung on each "BH couple k")."""
    k.export("BH4 dance floor", "bh_dance_floor", "B4")
    k.export("BH4 couple", "bh_couple", "B4")


if __name__ == "__main__":
    got = build()
    if "stills" in ARGS:
        stills(got)
    if "export" in ARGS:
        export(got)
