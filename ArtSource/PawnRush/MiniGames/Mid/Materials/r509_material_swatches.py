# R509 J Palace Laundry material swatches, following concept sheet
# Docs/PawnRush/ConceptArt/R509_PalaceLaundry/J_03_props_materials.png (bottom swatch row, ten materials).
#
# Five of the ten are the B East Wing's (r503_material_swatches.py, same palace stage) and are reused as baked:
# cream marble, walnut wainscot, dark iron, dark slate floor, matte brass.
# Soapy water and soap foam are not baked maps: the water is a Unity shader (like C's PR_MoatWater) and the
# foam is an effect, both made later.
# This script bakes the three the laundry adds, with the same helpers (imported, not edited):
#
#   PR_Mid_WhiteTile    1 m    white glazed wall/basin tiles, 4 x 4 of 25 cm, soft grey grout, glossy glaze
#   PR_Mid_OakStaves    1 m    warm oak wash-tub staves, six 16.7 cm boards with dark seams (grain along x)
#   PR_Mid_DollLinen    0.5 m  undyed pale linen for the cloth chess dolls (light so Unity can tint it)
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r509_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import bpy, bmesh, math, os, sys, random, importlib.util
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
CONCEPT = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R509_PalaceLaundry", "J_03_props_materials.png")

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

OAK = [(0., "#9A6533"), (.35, "#AE7640"), (.7, "#C08A50"), (1., "#CC9A60")]


# ------------------------------------------------------------------ white glazed tile (basin walls, wainscot)
def build_tile(P=1.):
    random.seed(5091)
    N = 4
    pitch, gap, h = P / N, .008, .018

    def albedo(nt, c):
        # each tile a slightly different white (tint), a faint cloud in the glaze, a few soft grey specks
        base = ramp(nt, c["tint"], [(0., "#E6DED0"), (.5, "#EEE7DB"), (1., "#F4EEE4")])
        cloud = maprange(nt, noise_tex(nt, c["obj"], 3., 4., .6), .3, .7, .965, 1.02)
        speck = maprange(nt, noise_tex(nt, c["obj"], 60., 2., .5), .68, .74, 1., .9)
        return vscale(nt, vscale(nt, base, cloud), speck)

    def rough(nt, c):
        return maprange(nt, noise_tex(nt, c["obj"], 5., 3.), .3, .7, .1, .2)

    def bump(nt, c):
        return noise_tex(nt, c["obj"], 9., 3., .5)
    mat = bake_material("PR_Mid_WhiteTile", albedo, rough, 0., bump, .02, .001, .04)
    grout = bake_material("grout", "#B5AFA6", .92, 0., lambda nt, c: noise_tex(nt, c["obj"], 40., 3.), .3, .001, .04)
    sheet("grout", P, grout)
    for i in range(N):
        for j in range(N):
            me = box("tile", pitch - gap, pitch - gap, h, cuts=6, bevel=.007, seg=3, subsurf=1)
            hx = (pitch - gap) / 2
            sd = Vector((random.uniform(0, 99), random.uniform(0, 99), 0.))

            def fn(p, n, hx=hx, sd=sd):
                # hand-made glaze: a very slight pillow and wobble on the face
                if n.z > .3:
                    rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hx, 1.)
                    z = .0025 * (math.cos(rx * math.pi / 2) * math.cos(ry * math.pi / 2)) ** .5
                    z += .0008 * math.sin(p.x * 31. + sd.x) * math.sin(p.y * 27. + sd.y)
                    return p + Vector((0., 0., z))
                return p
            deform(me, fn)
            smooth(me)
            place(me, mat, ((i + .5) * pitch, (j + .5) * pitch, h / 2), (P, P), (0., 0., random.uniform(-.003, .003)), random.random())
    return P


# ------------------------------------------------------------------ oak wash-tub staves
def build_staves(P=1.):
    random.seed(5092)
    mat = bake_material("PR_Mid_OakStaves", r3.wood_shader(OAK, .8, (.25, 3.4, 3.4), 1., 8.), r3.wood_rough(.5, .66), 0., r3.wood_bump, .25, .002, .05)
    under = bake_material("under", "#24160C", 1., 0., None, 0., .001, .05)
    sheet("under", P, under)
    rows, gap = 6, .007
    rh = P / rows
    for r in range(rows):
        # one stave per row, running the whole tile (the tub is cut into staves, never across them)
        me = r3.plank(P + .2, rh - gap, .035, cuts=12, bevel=.008, wobble=.0015, chip=.004, bow=0.)
        place(me, mat, (P / 2, (r + .5) * rh, .0175), (P, P), (0., 0., 0.), random.random())
    return P


# ------------------------------------------------------------------ undyed pale linen (cloth dolls)
def build_linen(P=.5):
    K = 40         # threads per tile, integer so the weave repeats

    def weave(nt, c):
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        link(nt, c["obj"], sep.inputs[0])
        sx = math_(nt, "SINE", math_(nt, "MULTIPLY", sep.outputs["X"], 2 * math.pi * K / P))
        sy = math_(nt, "SINE", math_(nt, "MULTIPLY", sep.outputs["Y"], 2 * math.pi * K / P))
        return math_(nt, "ADD", math_(nt, "MULTIPLY", sx, sx), math_(nt, "MULTIPLY", sy, sy))

    def albedo(nt, c):
        # near-neutral and light: Unity multiplies a team-side tint over it (charcoal or cream)
        base = ramp(nt, tnoise(nt, c, P, 1.4, 1.4, 5., .6), [(.3, "#DCD6CA"), (.5, "#E4DED3"), (.7, "#EBE6DC")])
        slub = maprange(nt, tnoise(nt, c, P, 1., 24., 2., .5), .62, .72, 1., .93)
        base = vscale(nt, base, slub)
        return vscale(nt, base, maprange(nt, weave(nt, c), 0., 2., .82, 1.05))

    def rough(nt, c):
        return maprange(nt, weave(nt, c), 0., 2., .88, .96)

    def bump(nt, c):
        return math_(nt, "MULTIPLY_ADD", weave(nt, c), .5, tnoise(nt, c, P, 10., 10., 3.))
    mat = bake_material("PR_Mid_DollLinen", albedo, rough, 0., bump, .5, .0015, .06)

    def puff(p, n):
        x, y = p.x / P, p.y / P
        z = (.006 * math.sin(2 * math.pi * (x + .3 * math.sin(2 * math.pi * y)))
             + .004 * math.sin(2 * math.pi * (2 * y + .2 * math.sin(2 * math.pi * x)) + 1.))
        return p + Vector((0., 0., z))
    sheet("linen", P, mat, 96, 96, puff)
    return P


NEW = [
    ("PR_Mid_WhiteTile", build_tile, 1., False),
    ("PR_Mid_OakStaves", build_staves, 1., False),
    ("PR_Mid_DollLinen", build_linen, .5, False),
]

# preview in the concept's swatch order (soapy water and foam left out): name, tile (m), kind, label
ROW = [
    ("PR_Mid_WhiteTile", 1., "stone", "흰 유약 타일 (새)"),
    ("PR_Mid_CreamMarble", 2., "stone", "크림 대리석 (B)"),
    ("PR_Mid_WalnutWainscot", 1., "wood", "호두나무 판벽 (B)"),
    ("PR_Mid_OakStaves", 1., "wood", "참나무 통 판자 (새)"),
    ("PR_Mid_DarkIron", 1., "metal", "어두운 쇠 테 (B)"),
    ("PR_Mid_SlateFloor", 2., "stone", "슬레이트 (B)"),
    ("PR_Mid_MatteBrass", 1., "metal", "무광 황동 (B)"),
    ("PR_Mid_DollLinen", .5, "stone", "인형 리넨 (새)"),
]
CROP = (35 / 1536, 1505 / 1536, 872 / 1024, 1012 / 1024)      # concept sheet 3 swatch row incl. labels


def preview():
    sc = r5.reset()
    n = len(ROW)
    step, size = 1.12, 1.
    W = n * step
    H = 3.1
    r5.text("R509 J 세탁장 재질 견본 · 1줄: 1 m 크기  ·  2줄: 2×2 반복  (비눗물·거품은 Unity 셰이더·효과로)", W / 2, 2.95, .11)
    for i, (name, tile, kind, label) in enumerate(ROW):
        x = step / 2 + i * step
        r5.slab(r5.preview_material(name, kind, 1. / tile), x, 2.05, size, .06, 1.)
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
    path = os.path.join(HERE, "r509_swatch_render.png")
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
    out.filepath_raw = os.path.join(HERE, "r509_swatch_compare.png")
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
