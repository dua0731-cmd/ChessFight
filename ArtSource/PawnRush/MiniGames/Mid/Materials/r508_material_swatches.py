# R508 F Clockwork Ballroom material swatches, following concept sheet
# Docs/PawnRush/ConceptArt/R508_ClockworkBallroom/F_03_props_materials.png (bottom swatch row, eight materials).
#
# Five of the eight are the B East Wing's (r503_material_swatches.py, same palace stage) and are reused as baked:
# cream marble, walnut wainscot, dark slate floor, cream/grey checker marble, matte brass.
# This script bakes the three the ballroom adds, with the same helpers (imported, not edited):
#
#   PR_Mid_Parquet      1.1 m  one game lane square: maple/walnut chevron strips in a thin walnut frame
#   PR_Mid_PlumVelvet   0.6 m  plum velvet, diamond tufting with sunken fabric buttons (capture bench top)
#   PR_Mid_SoftMirror   1 m    soft grey mirror face: no picture in it, faint cloudiness only
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r508_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import bpy, bmesh, math, os, sys, random, importlib.util
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
CONCEPT = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R508_ClockworkBallroom", "F_03_props_materials.png")

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048     # pixels per 2 m tile
ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None

# Import the R503 helpers (which import R502's) without running their own bake/preview.
_argv = sys.argv
sys.argv = [sys.argv[0]]
_spec = importlib.util.spec_from_file_location("r503", os.path.join(HERE, "r503_material_swatches.py"))
r3 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r3)
sys.argv = _argv
r5 = r3.r5

lin, link, ramp, mixc, math_ = r5.lin, r5.link, r5.ramp, r5.mixc, r5.math_
vscale, maprange, noise_tex = r5.vscale, r5.maprange, r5.noise_tex
bake_material, box, deform, smooth, place = r5.bake_material, r5.box, r5.deform, r5.smooth, r5.place
tnoise, sheet = r3.tnoise, r3.sheet

MAPLE = [(0., "#C9975E"), (.35, "#D6A66C"), (.7, "#E2B47C"), (1., "#EBC28E")]
PARQUET_WALNUT = [(0., "#5E321A"), (.35, "#6B3B20"), (.7, "#7A4626"), (1., "#86512E")]


# ------------------------------------------------------------------ parquet (one 1.1 m lane square)
def strip_mesh(poly, t):
    """A thin slab from a convex polygon (field coords), returned in its own frame (x along the strip)."""
    bm = bmesh.new()
    vs = [bm.verts.new((p[0], p[1], 0.)) for p in poly]
    f = bm.faces.new(vs)
    if f.normal.z < 0:
        f.normal_flip()
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in r["geom"]:
        if isinstance(v, bmesh.types.BMVert):
            v.co.z += t
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("strip")
    bm.to_mesh(me)
    bm.free()
    return me


def clip_poly(poly, a):
    """Clip a polygon to the square |x|, |y| <= a (Sutherland-Hodgman)."""
    def clip(pts, inside, cross):
        out = []
        for i in range(len(pts)):
            p, q = pts[i - 1], pts[i]
            if inside(q):
                if not inside(p):
                    out.append(cross(p, q))
                out.append(q)
            elif inside(p):
                out.append(cross(p, q))
        return out
    for axis in (0, 1):
        for sgn in (1., -1.):
            def inside(p, axis=axis, sgn=sgn):
                return sgn * p[axis] <= a

            def cross(p, q, axis=axis, sgn=sgn):
                k = (sgn * a - p[axis]) / (q[axis] - p[axis])
                return (p[0] + (q[0] - p[0]) * k, p[1] + (q[1] - p[1]) * k)
            poly = clip(poly, inside, cross)
            if not poly:
                return poly
    return poly


def build_parquet(P=1.1):
    random.seed(5081)
    maple = r3.wood_shader(MAPLE, .85, (.25, 3.6, 3.6), 1.1, 7.)
    walnut = r3.wood_shader(PARQUET_WALNUT, .82, (.25, 3.6, 3.6), 1.1, 7.)

    def albedo(nt, c):
        # tint < .5: maple strip, > .5: walnut strip (the tint also shades each strip a little)
        pick = maprange(nt, c["tint"], .49, .51, 0., 1.)
        return mixc(nt, pick, maple(nt, c), walnut(nt, c))
    mat = bake_material("PR_Mid_Parquet", albedo, r3.wood_rough(.42, .55), 0., r3.wood_bump, .2, .002, .04)
    frame_mat = bake_material("frame", r3.wood_shader(r3.WALNUT, .8, (.25, 3., 3.), .9), r3.wood_rough(.45, .58), 0., r3.wood_bump, .2, .002, .04)
    under = bake_material("under", "#1A0F09", 1., 0., None, 0., .001, .04)
    sheet("under", P, under)

    fw = .08                         # frame plank centred on each tile edge: 4 cm shows on each square
    gap, t = .006, .022
    a = P / 2 - fw / 2 - .004        # chevron field half size
    w = .14                          # strip width across the grain (measured along y)
    c = (P / 2, P / 2)
    k0 = int(math.floor(-2 * a / w)) - 1
    for side in (-1., 1.):
        for k in range(k0, int(math.ceil(a / w)) + 2):
            # strip k: y - |x| in [k w, (k+1) w], x on this side of the centre line
            lo, hi = k * w + gap / 2, (k + 1) * w - gap / 2
            x0, x1 = side * gap / 2, side * (a + .2)
            poly = [(x0, lo + abs(x0)), (x0, hi + abs(x0)), (x1, hi + abs(x1)), (x1, lo + abs(x1))]
            poly = clip_poly(poly, a)
            if len(poly) < 3:
                continue
            # strip frame: grain runs along (side, 1)/sqrt2
            ang = math.atan2(1., side)
            cx = sum(p[0] for p in poly) / len(poly)
            cy = sum(p[1] for p in poly) / len(poly)
            ca, sa = math.cos(-ang), math.sin(-ang)
            local = [((p[0] - cx) * ca - (p[1] - cy) * sa, (p[0] - cx) * sa + (p[1] - cy) * ca) for p in poly]
            me = strip_mesh(local, t)
            # soften: tiny bevel look via vertex noise is overkill; a slight per-strip tilt reads as laid wood
            rot = (random.uniform(-.003, .003), random.uniform(-.003, .003), ang)
            tint = (.12 + random.uniform(-.1, .1)) if k % 2 == 0 else (.88 + random.uniform(-.1, .1))
            place(me, mat, (c[0] + cx, c[1] + cy, .006), (P, P), rot, tint)
    # walnut frame: rail along x at y = 0 (wraps to the top), stile along y at x = 0 (wraps to the right)
    me = r3.plank(P - .004, fw, .03, cuts=10, bevel=.006, chip=.002, bow=0.)
    place(me, frame_mat, (P / 2, 0., .015 + .004), (P, P), tint=.45)
    me = r3.plank(P - fw - .006, fw, .03, cuts=10, bevel=.006, chip=.002, bow=0.)
    place(me, frame_mat, (0., P / 2, .015 + .004), (P, P), (0., 0., math.pi / 2), .55)
    return P


# ------------------------------------------------------------------ plum velvet, diamond tufted
def build_velvet(P=.6):
    random.seed(5082)
    D = P / 4                         # diamond pitch: u = (x + y) / D and v = (x - y) / D repeat every P
    H = .028

    def albedo(nt, c):
        base = ramp(nt, tnoise(nt, c, P, 1.3, 1.3, 5., .6), [(.3, "#4E1A33"), (.5, "#5C2140"), (.7, "#68294A")])
        # pile direction patches: velvet looks lighter or darker where the nap lies differently
        nap = maprange(nt, tnoise(nt, c, P, 2.2, 2.2, 3., .55), .3, .7, .86, 1.12)
        return vscale(nt, base, nap)

    def rough(nt, c):
        return maprange(nt, tnoise(nt, c, P, 6., 6., 3.), .3, .7, .8, .92)

    def bump(nt, c):
        return tnoise(nt, c, P, 40., 40., 3., .5)
    mat = bake_material("PR_Mid_PlumVelvet", albedo, rough, 0., bump, .25, .001, .05)
    btn = bake_material("button", "#3A1226", .75, 0., None, 0., .001, .05)

    def pillow(p, n):
        u = (p.x + p.y) / D
        v = (p.x - p.y) / D
        su, sv = abs(math.sin(math.pi * u)), abs(math.sin(math.pi * v))
        z = H * (su * sv) ** .45
        # a soft fold running from each button along the diamond edges
        z -= .004 * (1. - su) ** 6 + .004 * (1. - sv) ** 6
        return p + Vector((0., 0., z + .004))
    sheet("velvet", P, mat, 192, 192, pillow)
    # sunken fabric buttons where the creases cross (u, v both whole numbers)
    for i in range(-4, 9):
        for j in range(-4, 9):
            x, y = (i + j) * D / 2, (i - j) * D / 2
            if 0 <= x < P - 1e-6 and 0 <= y < P - 1e-6:
                me = box("button", .022, .022, .012, cuts=2, bevel=.0105, seg=4, subsurf=1)
                smooth(me)
                place(me, btn, (x, y, .006), (P, P), (0., 0., math.pi / 4), random.random())
    return P


# ------------------------------------------------------------------ ballroom cream marble (user 11-08: closer to the concept)
def build_ballroom_marble(P=2.):
    """Concept F_03: smooth warm cream with a few long, thin, meandering grey-gold veins, no crackle net.
    The B marble (PR_Mid_CreamMarble, shared by B and E) stays as it is."""
    def flow(nt, c, a, b, warp, wr):
        """0.5 + 0.5 sin(2 pi (a x + b y) / P + warp noise): long veins along a slanted direction.
        a and b are whole numbers, so the lines repeat every tile; ridge() draws them where it crosses 0.5."""
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        link(nt, c["obj"], sep.inputs[0])
        s = math_(nt, "ADD", math_(nt, "MULTIPLY", sep.outputs["X"], 2 * math.pi * a / P),
                  math_(nt, "MULTIPLY", sep.outputs["Y"], 2 * math.pi * b / P))
        s = math_(nt, "ADD", s, math_(nt, "MULTIPLY", math_(nt, "SUBTRACT", tnoise(nt, c, P, wr, wr, 3., .5), .5), warp))
        return math_(nt, "MULTIPLY_ADD", math_(nt, "SINE", s), .5, .5)

    def albedo(nt, c):
        base = ramp(nt, tnoise(nt, c, P, .7, .7, 4., .55), [(.3, "#D8C3A2"), (.5, "#E0CDAF"), (.7, "#E7D7BD")])
        cloud = maprange(nt, tnoise(nt, c, P, 2.2, 2.2, 5., .6), .3, .7, .965, 1.025)
        base = vscale(nt, base, cloud)
        main = r3.ridge(nt, flow(nt, c, 1, 1, 3.5, .8), .055)        # main veins: two long diagonals per tile, wandering
        side = r3.ridge(nt, flow(nt, c, 2, -1, 4.5, 1.1), .03)      # fainter cross branches
        fade = maprange(nt, tnoise(nt, c, P, .7, .7, 2.), .35, .62, .08, 1.)
        col = mixc(nt, math_(nt, "MULTIPLY", main, math_(nt, "MULTIPLY", fade, .95)), base, lin("#9B7C50"))
        return mixc(nt, math_(nt, "MULTIPLY", side, math_(nt, "MULTIPLY", fade, .6)), col, lin("#B09670"))

    def rough(nt, c):
        return maprange(nt, tnoise(nt, c, P, 2., 2., 3.), .3, .7, .2, .3)
    mat = bake_material("PR_Mid_BallroomMarble", albedo, rough, 0., lambda nt, c: tnoise(nt, c, P, 8., 8., 4.), .02, .001, .05)
    sheet("marble", P, mat)
    return P


# ------------------------------------------------------------------ soft grey mirror (no reflection picture)
def build_mirror(P=1.):
    def cloud(nt, c):
        return tnoise(nt, c, P, 1.1, 1.1, 4., .55)

    def albedo(nt, c):
        base = ramp(nt, cloud(nt, c), [(.3, "#B3B9BF"), (.5, "#B9BFC5"), (.7, "#BFC4C9")])
        # very faint vertical soft streaks, like polished silvered glass
        streak = maprange(nt, tnoise(nt, c, P, 14., .7, 2., .5), .3, .7, .985, 1.015)
        return vscale(nt, base, streak)

    def rough(nt, c):
        return maprange(nt, cloud(nt, c), .3, .7, .07, .1)

    def metal(nt, c):
        return maprange(nt, cloud(nt, c), .3, .7, .62, .58)
    mat = bake_material("PR_Mid_SoftMirror", albedo, rough, metal, lambda nt, c: tnoise(nt, c, P, 3., 3., 2.), .01, .001, .05)
    sheet("mirror", P, mat)
    return P


NEW = [
    ("PR_Mid_BallroomMarble", build_ballroom_marble, 2., False),
    ("PR_Mid_Parquet", build_parquet, 1.1, False),
    ("PR_Mid_PlumVelvet", build_velvet, .6, False),
    ("PR_Mid_SoftMirror", build_mirror, 1., True),
]

# preview in the concept's swatch order: name, tile (m), kind, label
ROW = [
    ("PR_Mid_BallroomMarble", 2., "stone", "크림 대리석 (F 전용)"),
    ("PR_Mid_WalnutWainscot", 1., "wood", "호두나무 판벽 (B)"),
    ("PR_Mid_Parquet", 1.1, "wood", "쪽마루 (새)"),
    ("PR_Mid_SlateFloor", 2., "stone", "슬레이트 (B)"),
    ("PR_Mid_CheckerMarble", 2., "stone", "체크 대리석 (B)"),
    ("PR_Mid_MatteBrass", 1., "metal", "무광 황동 (B)"),
    ("PR_Mid_PlumVelvet", .6, "stone", "자두색 벨벳 (새)"),
    ("PR_Mid_SoftMirror", 1., "metal", "회색 거울 (새)"),
]
CROP = (70 / 1536, 1470 / 1536, 830 / 1024, 1005 / 1024)      # concept sheet 3 swatch row incl. labels


def preview():
    sc = r5.reset()
    n = len(ROW)
    step, size = 1.12, 1.
    W = n * step
    H = 3.1
    r5.text("R508 F 무도회장 재질 견본 · 1줄: 1 m 크기(쪽마루는 레인 한 칸 1.1 m)  ·  2줄: 2×2 반복", W / 2, 2.95, .12)
    for i, (name, tile, kind, label) in enumerate(ROW):
        x = step / 2 + i * step
        span = 1. / tile if name != "PR_Mid_Parquet" else 1.
        r5.slab(r5.preview_material(name, kind, span), x, 2.05, size, .06, 1.)
        r5.slab(r5.preview_material(name, kind, 2.), x, .78, size, .012, 1.)
        r5.text(label, x, 2.05 - .66, .1)
        r5.text(f"{tile:g} m 타일", x, .78 - .62, .09)
    bd = bpy.data.meshes.new("bd")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.)
    bm.to_mesh(bd)
    bm.free()
    bd.materials.append(r5.plain("backdrop", "#F5EDE0", .95))
    bo = bpy.data.objects.new("bd", bd)
    bo.rotation_euler = (math.pi / 2, 0, 0)
    bo.location = (W / 2, .12, H / 2)
    sc.collection.objects.link(bo)
    la = bpy.data.lights.new("key", "AREA")
    la.energy = 1200
    la.size = 6.
    la.color = (1., .93, .84)
    lo = bpy.data.objects.new("key", la)
    lo.location = (W / 2 - 4, -6, H + 3.5)
    lo.rotation_euler = (Vector((W / 2, 0, H / 2)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = lin("#FFF4E6")
    bg.inputs["Strength"].default_value = .45
    sc.world = w
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = W
    co = bpy.data.objects.new("cam", cam)
    co.location = (W / 2, -10, H / 2)
    co.rotation_euler = (math.pi / 2, 0, 0)
    sc.collection.objects.link(co)
    sc.camera = co
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1800
    sc.render.resolution_y = int(1800 * H / W)
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    path = os.path.join(HERE, "r508_swatch_render.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    compare(path)


def compare(render_path):
    R = r5.pixels(bpy.data.images.load(render_path, check_existing=False))
    C = r5.pixels(bpy.data.images.load(CONCEPT, check_existing=False))
    h, w = C.shape[:2]
    x0, x1 = int(CROP[0] * w), int(CROP[1] * w)
    y0, y1 = h - int(CROP[3] * h), h - int(CROP[2] * h)
    crop = C[y0:y1, x0:x1].copy()
    ci = bpy.data.images.new("crop", crop.shape[1], crop.shape[0], alpha=True)
    ci.pixels.foreach_set(crop.ravel())
    W = R.shape[1]
    ci.scale(W, int(crop.shape[0] * W / crop.shape[1]))
    top = r5.pixels(ci)
    bar = np.ones((6, W, 4), np.float32) * np.array([.55, .45, .35, 1.], np.float32)
    out_px = np.concatenate([R, bar, top], 0)
    out = bpy.data.images.new("compare", W, out_px.shape[0], alpha=True)
    out.pixels.foreach_set(out_px.ravel())
    out.filepath_raw = os.path.join(HERE, "r508_swatch_compare.png")
    out.file_format = "PNG"
    out.save()
    print("wrote", out.filepath_raw)


if "--bake" in ARGS:
    for name, build, tile, metallic in NEW:
        if ONLY and ONLY not in name:
            continue
        r5.bake(name, build, int(RES * tile / 2), metallic=metallic)
if "--preview" in ARGS:
    preview()
