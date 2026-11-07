# R503 phase-2 (lower palace) material swatches for the B East Wing, following concept sheet
# Docs/PawnRush/ConceptArt/R503_RepairWing/B_03_props_materials.png (bottom swatch row, eight materials).
#
# Same method as the phase-1 swatches (../../Early/Materials/r502_material_swatches.py, whose helpers this
# script imports): build a real sample surface, render it straight down into tileable PBR maps.
#
#   PR_Mid_CreamMarble     2 m  polished cream marble with soft grey-gold veins (no joints; blocks are geometry)
#   PR_Mid_WalnutWainscot  1 m  one raised panel bay: rails, stiles, chamfered field
#   PR_Mid_WalnutFloor     2 m  walnut floorboards 0.4 m wide (the 2.5 x 2 m game plank is 5 boards)
#   PR_Mid_JoistWood       1 m  pale rough-sawn joist wood with knots
#   PR_Mid_DarkIron        1 m  near-black hammered iron with pits
#   PR_Mid_MatteBrass      1 m  brushed matte brass (decoration; functional gold stays the game's own)
#   PR_Mid_Canvas          1 m  cream canvas dust sheet: weave and soft folds
#   PR_Mid_BasementStone   2 m  dark rough basement blocks
#
# Continuous surfaces (marble, iron, brass, canvas) sample noise on a torus (x and y wrapped through cos/sin into
# 4D noise), so they repeat exactly every tile. Piece-built surfaces wrap through linked copies like R502.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r503_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import bpy, bmesh, math, os, sys, random, importlib.util
import numpy as np
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
TEX = os.path.join(HERE, "Textures")
CONCEPT = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R503_RepairWing", "B_03_props_materials.png")
R502 = os.path.normpath(os.path.join(HERE, "..", "..", "Early", "Materials", "r502_material_swatches.py"))

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048     # pixels per 2 m tile
ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None
os.makedirs(TEX, exist_ok=True)

# Import the R502 helpers without running its own bake/preview (it reads sys.argv at import time).
_argv = sys.argv
sys.argv = [sys.argv[0]]
_spec = importlib.util.spec_from_file_location("r502", R502)
r5 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r5)
sys.argv = _argv
r5.TEX = TEX            # bake() and preview_material() write/read this module global

lin, node, link, ramp, mixc, math_ = r5.lin, r5.node, r5.link, r5.ramp, r5.mixc, r5.math_
vscale, vmul, maprange, noise_tex = r5.vscale, r5.vmul, r5.maprange, r5.noise_tex
bake_material, box, deform, smooth, place = r5.bake_material, r5.box, r5.deform, r5.smooth, r5.place


# ------------------------------------------------------------------ periodic (torus) noise
def tnoise(nt, c, P, rx, ry, detail=4., rough=.55, dist=0.):
    """Noise that repeats every P metres in x and y. rx/ry set the feature count per tile (~2*pi*r)."""
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    link(nt, c["obj"], sep.inputs[0])
    ax = math_(nt, "MULTIPLY", sep.outputs["X"], 2 * math.pi / P)
    ay = math_(nt, "MULTIPLY", sep.outputs["Y"], 2 * math.pi / P)
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY", math_(nt, "COSINE", ax), rx), comb.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ax), rx), comb.inputs["Y"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "COSINE", ay), ry), comb.inputs["Z"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    link(nt, comb.outputs[0], n.inputs["Vector"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ay), ry), n.inputs["W"])
    n.inputs["Scale"].default_value = 1.
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.inputs["Distortion"].default_value = dist
    return n.outputs["Fac"]


def tvoronoi_edge(nt, c, P, r, warp=0.):
    """Distance to the nearest Voronoi cell edge, periodic every P metres (marble crackle veins).
    warp adds a little periodic noise to the torus coords so the edges wander."""
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    link(nt, c["obj"], sep.inputs[0])
    ax = math_(nt, "MULTIPLY", sep.outputs["X"], 2 * math.pi / P)
    ay = math_(nt, "MULTIPLY", sep.outputs["Y"], 2 * math.pi / P)
    if warp:
        wn = math_(nt, "MULTIPLY", math_(nt, "SUBTRACT", tnoise(nt, c, P, 1.2, 1.2, 3., .5), .5), warp)
        ax = math_(nt, "ADD", ax, wn)
        ay = math_(nt, "SUBTRACT", ay, wn)
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY", math_(nt, "COSINE", ax), r), comb.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ax), r), comb.inputs["Y"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "COSINE", ay), r), comb.inputs["Z"])
    v = nt.nodes.new("ShaderNodeTexVoronoi")
    v.voronoi_dimensions = "4D"
    v.feature = "DISTANCE_TO_EDGE"
    link(nt, comb.outputs[0], v.inputs["Vector"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ay), r), v.inputs["W"])
    v.inputs["Scale"].default_value = 1.
    return v.outputs["Distance"]


def ridge(nt, fac, width):
    """Thin line where noise crosses 0.5 (marble veins)."""
    d = math_(nt, "ABSOLUTE", math_(nt, "SUBTRACT", fac, .5))
    return maprange(nt, d, 0., width, 1., 0.)


def sheet(name, P, mat, nx=1, ny=1, fn=None, tint=.5):
    """A continuous surface covering exactly one tile (object coords 0..P) plus its eight wrapped copies."""
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=nx, y_segments=ny, size=.5)
    for v in bm.verts:
        v.co.x = (v.co.x + .5) * P
        v.co.y = (v.co.y + .5) * P
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if fn:
        deform(me, fn)
    smooth(me)
    place(me, mat, (0., 0., 0.), (P, P), tint=tint)


# ------------------------------------------------------------------ walnut family (shared grain shader)
def wood_shader(stops, fine_dark=.78, scale=(.25, 3.2, 3.2), wave=.9, dist=9.):
    def f(nt, c):
        mapn = nt.nodes.new("ShaderNodeMapping")
        link(nt, c["obj"], mapn.inputs["Vector"])
        off = nt.nodes.new("ShaderNodeCombineXYZ")
        link(nt, math_(nt, "MULTIPLY", c["tint"], 13.7), off.inputs["X"])
        link(nt, math_(nt, "MULTIPLY", c["tint"], 7.3), off.inputs["Z"])
        link(nt, off.outputs[0], mapn.inputs["Location"])
        mapn.inputs["Scale"].default_value = scale
        wv = nt.nodes.new("ShaderNodeTexWave")
        wv.wave_type = "BANDS"
        wv.bands_direction = "Y"
        link(nt, mapn.outputs[0], wv.inputs["Vector"])
        wv.inputs["Scale"].default_value = wave
        wv.inputs["Distortion"].default_value = dist
        wv.inputs["Detail"].default_value = 4.
        wv.inputs["Detail Scale"].default_value = 1.4
        grain = ramp(nt, wv.outputs["Fac"], stops)
        tint = ramp(nt, c["tint"], [(0., (.84, .82, .8, 1.)), (.5, (1., 1., 1., 1.)), (1., (1.08, 1.02, .96, 1.))])
        streak = maprange(nt, noise_tex(nt, mapn.outputs[0], 3., 6., .6), .3, .7, .88, 1.06)
        w2 = nt.nodes.new("ShaderNodeTexWave")
        w2.wave_type = "BANDS"
        w2.bands_direction = "Y"
        link(nt, mapn.outputs[0], w2.inputs["Vector"])
        w2.inputs["Scale"].default_value = 7.
        w2.inputs["Distortion"].default_value = 5.
        w2.inputs["Detail"].default_value = 2.
        fine = maprange(nt, w2.outputs["Fac"], .55, 1., 1.03, fine_dark)
        return vscale(nt, vscale(nt, vmul(nt, grain, tint), streak), fine)
    return f


WALNUT = [(0., "#5A2D17"), (.35, "#6E3A1F"), (.7, "#814827"), (1., "#8F5530")]
JOIST = [(0., "#7E5B39"), (.3, "#9C7650"), (.65, "#B58F66"), (1., "#C6A27A")]


def wood_rough(lo, hi):
    return lambda nt, c: maprange(nt, noise_tex(nt, c["obj"], 6., 4.), .3, .7, lo, hi)


def wood_bump(nt, c):
    mapn = nt.nodes.new("ShaderNodeMapping")
    link(nt, c["obj"], mapn.inputs["Vector"])
    mapn.inputs["Scale"].default_value = (.25, 3.2, 3.2)
    wv = nt.nodes.new("ShaderNodeTexWave")
    wv.wave_type = "BANDS"
    wv.bands_direction = "Y"
    link(nt, mapn.outputs[0], wv.inputs["Vector"])
    wv.inputs["Scale"].default_value = 2.2
    wv.inputs["Distortion"].default_value = 7.
    wv.inputs["Detail"].default_value = 3.
    return math_(nt, "MULTIPLY_ADD", noise_tex(nt, c["obj"], 40., 3.), .3, wv.outputs["Fac"])


def plank(L, W, t, cuts=8, bevel=.012, wobble=.0018, chip=.008, bow=.004, saw=0.):
    me = box("plank", L, W, t, cuts=cuts, bevel=bevel, seg=3, subsurf=1)
    hx, hy = L / 2, W / 2
    b = random.uniform(-bow, bow)
    seed = Vector((random.uniform(0, 99), random.uniform(0, 99), 0.))

    def fn(p, n):
        u = p.x / hx
        d = Vector((0., 0., b * (1 - u * u)))
        q = Vector((p.x * 1.2, p.y * 9., p.z)) + seed
        d += n * noise.noise(q) * wobble
        if saw and n.z > .3:
            d.z += saw * math.sin(p.x * 140. + math.sin(p.y * 6.) * 2.)
        rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.)
        edge = max(0., (max(rx, ry) - .85) / .15)
        d += n * min(0., noise.noise(p * 11. + seed) + .15) * chip * edge
        return p + d
    deform(me, fn)
    smooth(me)
    return me


def build_floor(P=2.):
    random.seed(5031)
    mat = bake_material("PR_Mid_WalnutFloor", wood_shader(WALNUT), wood_rough(.55, .7), 0., wood_bump, .25, .002, .05)
    under = bake_material("under", "#1A0F09", 1., 0., None, 0., .001, .05)
    sheet("under", P, under)
    rows, pitch, gap, endgap = 5, P / 5, .012, .01
    for r in range(rows):
        y = r * pitch + pitch / 2
        x = random.uniform(0, P)
        end = x + P
        while x < end - 1e-4:
            L = random.uniform(1.1, 2.0)
            if end - x - L < .6:
                L = end - x
            me = plank(L - endgap, pitch - gap, .05, cuts=10, bevel=.014)
            rot = (random.uniform(-.005, .005), random.uniform(-.004, .004), random.uniform(-.002, .002))
            place(me, mat, (x + L / 2, y, .025 + .03), (P, P), rot, random.random())
            x += L
    return P


def build_joist(P=1.):
    random.seed(5032)
    def albedo(nt, c):
        col = wood_shader(JOIST, .8, (.2, 2.6, 2.6), 1.1, 6.)(nt, c)
        # dark knots: small round spots in object space
        knot = maprange(nt, noise_tex(nt, c["obj"], 2.2, 2., .5, w=11.), .7, .76, 0., .85)
        return mixc(nt, knot, col, lin("#4A3020"))

    mat = bake_material("PR_Mid_JoistWood", albedo, wood_rough(.82, .94), 0., wood_bump, .35, .003, .05)
    under = bake_material("under", "#2A1D12", 1., 0., None, 0., .001, .05)
    sheet("under", P, under)
    rows, pitch, gap = 2, P / 2, .012
    for r in range(rows):
        y = r * pitch + pitch / 2
        x = random.uniform(0, P)
        me = plank(P - .01, pitch - gap, .08, cuts=14, bevel=.008, wobble=.006, chip=.02, bow=.003)
        place(me, mat, (x + P / 2, y, .07), (P, P), (0., 0., random.uniform(-.003, .003)), random.random())
    return P


def build_wainscot(P=1.):
    random.seed(5033)
    mat = bake_material("PR_Mid_WalnutWainscot", wood_shader(WALNUT, .88, (.25, 2.2, 2.2), .6), wood_rough(.5, .65), 0., wood_bump, .15, .002, .06)
    back = bake_material("back", "#26150C", 1., 0., None, 0., .001, .06)
    sheet("back", P, back)
    rail_h, stile_w, t = .16, .14, .035
    # rail along x at y = 0 (wraps to the top), stile along y at x = 0 (wraps to the right)
    me = plank(P - .004, rail_h, t, cuts=8, bevel=.008, chip=.004, bow=0.)
    place(me, mat, (P / 2, 0., t / 2 + .02), (P, P), tint=.35)
    me = plank(P - rail_h - .006, stile_w, t, cuts=8, bevel=.008, chip=.004, bow=0.)
    place(me, mat, (0., P / 2, t / 2 + .02), (P, P), (0., 0., math.pi / 2), .6)
    # raised field: big chamfer, vertical grain
    fw, fh = P - stile_w - .1, P - rail_h - .1
    me = box("field", fh, fw, .03, cuts=8, bevel=.045, seg=2, subsurf=0, angle=30)
    smooth(me)
    place(me, mat, (P / 2, P / 2, .03 / 2 + .012), (P, P), (0., 0., math.pi / 2), .5)
    # small bead moulding round the field opening
    for (cx, cy, L, rot) in ((P / 2, rail_h / 2 + .012, fw + .05, 0.), (P / 2, P - rail_h / 2 - .012, fw + .05, 0.),
                             (stile_w / 2 + .012, P / 2, fh + .05, math.pi / 2), (P - stile_w / 2 - .012, P / 2, fh + .05, math.pi / 2)):
        me = box("bead", L, .018, .018, cuts=2, bevel=.008, seg=3)
        smooth(me)
        place(me, mat, (cx, cy, .045), (P, P), (0., 0., rot), .4)
    return P


# ------------------------------------------------------------------ marble, iron, brass, canvas (torus noise)
def build_marble(P=2.):
    def albedo(nt, c):
        base = ramp(nt, tnoise(nt, c, P, .9, .9, 5., .6), [(.3, "#DCCBB0"), (.5, "#E7DBC6"), (.7, "#EFE6D6")])
        # crackle network of soft veins (concept: large cells) plus faint finer cells
        v1 = maprange(nt, tvoronoi_edge(nt, c, P, .6, .7), 0., .03, 1., 0.)
        v2 = maprange(nt, tvoronoi_edge(nt, c, P, 1.3, .5), 0., .012, 1., 0.)
        fade = maprange(nt, tnoise(nt, c, P, .5, .5, 2.), .3, .6, .55, 1.)
        col = mixc(nt, math_(nt, "MULTIPLY", v1, fade), base, lin("#A08766"))
        col = mixc(nt, math_(nt, "MULTIPLY", v2, .7), col, lin("#B49C7C"))
        spk = maprange(nt, tnoise(nt, c, P, 30., 30., 2.), .4, .7, .97, 1.02)
        return vscale(nt, col, spk)

    def rough(nt, c):
        return maprange(nt, tnoise(nt, c, P, 2., 2., 3.), .3, .7, .26, .4)

    def bump(nt, c):
        return tnoise(nt, c, P, 8., 8., 4.)
    mat = bake_material("PR_Mid_CreamMarble", albedo, rough, 0., bump, .04, .001, .05)
    sheet("marble", P, mat)
    return P


def build_iron(P=1.):
    def mot(nt, c):
        return tnoise(nt, c, P, 1.2, 1.2, 6., .62)

    def pits(nt, c):
        return maprange(nt, tnoise(nt, c, P, 6., 6., 3., .55), .6, .66, 0., 1.)

    def albedo(nt, c):
        base = ramp(nt, mot(nt, c), [(.25, "#211F1D"), (.5, "#302D2A"), (.75, "#45403B")])
        base = vscale(nt, base, maprange(nt, tnoise(nt, c, P, 3.5, 3.5, 2., .4), .3, .7, .8, 1.25))
        return mixc(nt, math_(nt, "MULTIPLY", pits(nt, c), .8), base, lin("#0C0B0A"))

    def rough(nt, c):
        return math_(nt, "ADD", maprange(nt, mot(nt, c), .3, .7, .5, .66), math_(nt, "MULTIPLY", pits(nt, c), .2))

    def metal(nt, c):
        return math_(nt, "SUBTRACT", .78, math_(nt, "MULTIPLY", pits(nt, c), .3))

    def bump(nt, c):
        ham = tnoise(nt, c, P, 5., 5., 2., .4)
        fine = tnoise(nt, c, P, 16., 16., 3., .5)
        h = math_(nt, "MULTIPLY_ADD", fine, .35, ham)
        return math_(nt, "SUBTRACT", h, math_(nt, "MULTIPLY", pits(nt, c), .6))
    mat = bake_material("PR_Mid_DarkIron", albedo, rough, metal, bump, 1., .012, .05)
    sheet("iron", P, mat)
    return P


def build_brass(P=1.):
    def brush(nt, c):
        return tnoise(nt, c, P, .6, 26., 3., .5)

    def grime(nt, c):
        return maprange(nt, tnoise(nt, c, P, 1.5, 1.5, 5., .6), .62, .78, 0., 1.)

    def albedo(nt, c):
        base = ramp(nt, tnoise(nt, c, P, .9, .9, 3.), [(.3, "#9A742F"), (.5, "#AE883C"), (.7, "#BF984A")])
        base = vscale(nt, base, maprange(nt, brush(nt, c), .3, .7, .82, 1.12))
        return mixc(nt, math_(nt, "MULTIPLY", grime(nt, c), .3), base, lin("#6A5028"))

    def rough(nt, c):
        return math_(nt, "ADD", maprange(nt, brush(nt, c), .3, .7, .42, .55), math_(nt, "MULTIPLY", grime(nt, c), .2))

    def metal(nt, c):
        return math_(nt, "SUBTRACT", .95, math_(nt, "MULTIPLY", grime(nt, c), .25))
    mat = bake_material("PR_Mid_MatteBrass", albedo, rough, metal, brush, .06, .001, .05)
    sheet("brass", P, mat)
    return P


def build_canvas(P=1.):
    K = 34         # threads per tile, integer so the weave repeats (coarse, as the concept)

    def weave(nt, c):
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        link(nt, c["obj"], sep.inputs[0])
        sx = math_(nt, "SINE", math_(nt, "MULTIPLY", sep.outputs["X"], 2 * math.pi * K / P))
        sy = math_(nt, "SINE", math_(nt, "MULTIPLY", sep.outputs["Y"], 2 * math.pi * K / P))
        return math_(nt, "ADD", math_(nt, "MULTIPLY", sx, sx), math_(nt, "MULTIPLY", sy, sy))

    def albedo(nt, c):
        base = ramp(nt, tnoise(nt, c, P, 1.4, 1.4, 5., .6), [(.3, "#BDAE92"), (.5, "#CABCA1"), (.7, "#D5C8AE")])
        stain = maprange(nt, tnoise(nt, c, P, 1.1, 1.1, 4., .6), .6, .75, 0., .35)
        base = mixc(nt, stain, base, lin("#A8957A"))
        return vscale(nt, base, maprange(nt, weave(nt, c), 0., 2., .72, 1.08))

    def rough(nt, c):
        return maprange(nt, weave(nt, c), 0., 2., .9, .97)

    def bump(nt, c):
        return math_(nt, "MULTIPLY_ADD", weave(nt, c), .5, tnoise(nt, c, P, 12., 12., 3.))
    mat = bake_material("PR_Mid_Canvas", albedo, rough, 0., bump, .6, .002, .08)

    def folds(p, n):
        x, y = p.x / P, p.y / P
        z = (.022 * math.sin(2 * math.pi * (x + .35 * math.sin(2 * math.pi * y)))
             + .012 * math.sin(2 * math.pi * (2 * y + .2 * math.sin(2 * math.pi * x)) + 1.)
             + .006 * math.sin(2 * math.pi * (3 * x - 2 * y) + 2.))
        return p + Vector((0., 0., z))
    sheet("canvas", P, mat, 96, 96, folds)
    return P


def build_basement(P=2.):
    random.seed(5034)

    def albedo(nt, c):
        base = ramp(nt, c["tint"], [(0., "#3F3B37"), (.35, "#4A4540"), (.7, "#554F48"), (1., "#45403B")])
        mot = maprange(nt, noise_tex(nt, c["obj"], 2.4, 5., .62), .3, .7, .8, 1.15)
        damp = maprange(nt, noise_tex(nt, c["obj"], 1.3, 3., .5, w=7.), .55, .7, 1., .78)
        return vscale(nt, vscale(nt, base, mot), damp)

    def rough(nt, c):
        return maprange(nt, noise_tex(nt, c["obj"], 9., 4.), .3, .7, .82, .96)

    def bump(nt, c):
        return math_(nt, "MULTIPLY_ADD", noise_tex(nt, c["obj"], 55., 2., .5), .4, noise_tex(nt, c["obj"], 14., 8., .65))
    mat = bake_material("PR_Mid_BasementStone", albedo, rough, 0., bump, .8, .006, .1)
    mortar = bake_material("mortar", "#1B1918", .98, 0., lambda nt, c: noise_tex(nt, c["obj"], 30., 4.), .3, .002, .1)
    sheet("mortar", P, mortar)
    rows, rh, gap = 4, P / 4, .03
    for r in range(rows):
        y = r * rh + rh / 2
        x = random.uniform(0, P)
        end = x + P
        while x < end - 1e-4:
            L = random.uniform(.5, 1.0)
            if end - x - L < .4:
                L = end - x if end - x < .95 else (end - x) / 2
            h = .14 + random.uniform(-.015, .015)
            me = box("stone", L - gap, rh - gap, h, cuts=16, bevel=.04, seg=4, subsurf=2)
            hx, hy = (L - gap) / 2, (rh - gap) / 2
            seed = Vector((random.uniform(0, 99), random.uniform(0, 99), random.uniform(0, 99)))

            def fn(p, n, hx=hx, hy=hy, seed=seed):
                rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.)
                d = Vector()
                if n.z > .3:
                    d.z += .012 * (math.cos(rx * math.pi / 2) ** .5) * (math.cos(ry * math.pi / 2) ** .5)
                q = p * 4. + seed
                # chunky, faceted rock face like the concept's basement stone
                d += n * (noise.noise(q) * .05 + noise.noise(q * 2.7) * .022 + noise.noise(q * 7.) * .007)
                edge = max(0., (max(rx, ry) - .65) / .35)
                chip = min(0., noise.noise(p * 8. + seed * 2.) + .1) + .5 * min(0., noise.noise(p * 21. + seed))
                d += n * (chip * .045 * edge)
                return p + d
            deform(me, fn)
            smooth(me)
            rot = (random.uniform(-.012, .012), random.uniform(-.012, .012), random.uniform(-.015, .015))
            place(me, mat, (x + L / 2, y, h / 2), (P, P), rot, random.random())
            x += L
    return P


# ------------------------------------------------------------------ floors (R503: the shell's paving is gone with the shell)
def flagstones(name, P, rows, length, h, stops, mortar_col, rough, puff, seed, bump_k=.35, chips=.02):
    """Rows of slightly domed stone slabs on mortar (2 m tile), per-slab tint picks a colour from `stops`."""
    random.seed(seed)

    def albedo(nt, c):
        base = ramp(nt, c["tint"], stops)
        mot = maprange(nt, noise_tex(nt, c["obj"], 2.2, 5., .6), .3, .7, .88, 1.1)
        spk = maprange(nt, noise_tex(nt, c["obj"], 40., 3., .7), .35, .7, .94, 1.03)
        return vscale(nt, vscale(nt, base, mot), spk)

    def bump(nt, c):
        return math_(nt, "MULTIPLY_ADD", noise_tex(nt, c["obj"], 50., 2., .5), .4, noise_tex(nt, c["obj"], 12., 6., .6))
    mat = bake_material(name, albedo, lambda nt, c: maprange(nt, noise_tex(nt, c["obj"], 8., 3.), .3, .7, *rough), 0., bump, bump_k, .003, .08)
    mortar = bake_material("mortar", mortar_col, .95, 0., lambda nt, c: noise_tex(nt, c["obj"], 30., 4.), .3, .002, .08)
    sheet("mortar", P, mortar)
    rh, gap = P / rows, .016
    for r in range(rows):
        y = r * rh + rh / 2
        x = random.uniform(0, P) if length else 0.
        end = x + P
        while x < end - 1e-4:
            L = random.uniform(*length) if length else rh
            if end - x - L < .25:
                L = end - x
            me = box("slab", L - gap, rh - gap, h, cuts=8, bevel=.02, seg=3, subsurf=1)
            hx, hy = (L - gap) / 2, (rh - gap) / 2
            sd = Vector((random.uniform(0, 99), random.uniform(0, 99), random.uniform(0, 99)))

            def fn(p, n, hx=hx, hy=hy, sd=sd):
                rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.)
                d = Vector()
                if n.z > .3:
                    d.z += puff * (math.cos(rx * math.pi / 2) ** .5) * (math.cos(ry * math.pi / 2) ** .5)
                d += n * noise.noise(p * 3. + sd) * .004
                edge = max(0., (max(rx, ry) - .8) / .2)
                d += n * min(0., noise.noise(p * 10. + sd) + .2) * chips * edge
                return p + d
            deform(me, fn)
            smooth(me)
            place(me, mat, (x + L / 2, y, h / 2), (P, P), (0., 0., random.uniform(-.004, .004)), random.random())
            x += L
    return P


def build_slate(P=2.):
    # station floors: dark blue-grey slate flagstones (concept: the hall floor around the repair gaps)
    return flagstones("PR_Mid_SlateFloor", P, 3, (.55, 1.0), .06,
                      [(0., "#323A47"), (.35, "#3B4452"), (.7, "#454F5E"), (1., "#38404C")], "#171A20", (.6, .78), .006, 5035)


def build_checker(P=2.):
    # entry band: cream and grey marble squares, 1 m (concept: the chequered threshold); tint 0/1 picks the colour
    random.seed(5036)

    def albedo(nt, c):
        base = ramp(nt, c["tint"], [(0., "#E6DCCA"), (.49, "#E6DCCA"), (.51, "#8E9096"), (1., "#8E9096")])
        vein = maprange(nt, noise_tex(nt, c["obj"], 1.6, 6., .62, 2.), .47, .53, 0., 1.)
        vein = math_(nt, "SUBTRACT", 1., math_(nt, "ABSOLUTE", math_(nt, "SUBTRACT", math_(nt, "MULTIPLY", vein, 2.), 1.)))
        return mixc(nt, math_(nt, "MULTIPLY", vein, .35), base, lin("#6F6A66"))
    mat = bake_material("PR_Mid_CheckerMarble", albedo, .3, 0., lambda nt, c: noise_tex(nt, c["obj"], 10., 3.), .05, .001, .06)
    mortar = bake_material("mortar", "#4A4642", .9, 0., None, 0., .001, .06)
    sheet("mortar", P, mortar)
    for i in range(2):
        for j in range(2):
            me = box("square", .985, .985, .05, cuts=4, bevel=.01, seg=2)
            smooth(me)
            place(me, mat, (i + .5, j + .5, .025), (P, P), tint=(.15 if (i + j) % 2 == 0 else .85))
    return P


def build_creamtile(P=2.):
    # rear corridor: large cream marble tiles, 1 m, soft veins
    return flagstones("PR_Mid_CreamFloor", P, 2, None, .05,
                      [(0., "#DCCFB8"), (.5, "#E5DAC6"), (1., "#D6C9B2")], "#9C8C76", (.3, .45), .003, 5037, .12, .008)


# name, builder, tile size (m), metallic map, preview kind, concept swatch order
MATERIALS = [
    ("PR_Mid_CreamMarble", build_marble, 2., False, "stone", "크림 대리석"),
    ("PR_Mid_WalnutWainscot", build_wainscot, 1., False, "wood", "호두나무 판벽"),
    ("PR_Mid_WalnutFloor", build_floor, 2., False, "wood", "호두나무 마루"),
    ("PR_Mid_JoistWood", build_joist, 1., False, "wood", "장선 나무"),
    ("PR_Mid_DarkIron", build_iron, 1., True, "metal", "어두운 쇠"),
    ("PR_Mid_MatteBrass", build_brass, 1., True, "metal", "무광 황동"),
    ("PR_Mid_Canvas", build_canvas, 1., False, "stone", "먼지막이 천"),
    ("PR_Mid_BasementStone", build_basement, 2., False, "stone", "지하 어두운 돌"),
    ("PR_Mid_SlateFloor", build_slate, 2., False, "stone", "슬레이트 바닥"),
    ("PR_Mid_CheckerMarble", build_checker, 2., False, "stone", "체크 대리석"),
    ("PR_Mid_CreamFloor", build_creamtile, 2., False, "stone", "크림 바닥"),
]


# ------------------------------------------------------------------ preview laid out like the concept swatch row
CROP = (28 / 1536, 1512 / 1536, 778 / 1024, 992 / 1024)      # concept sheet 3 swatch row incl. labels


def preview():
    sc = r5.reset()
    n = len(MATERIALS)
    step, size = 1.12, 1.
    W = n * step
    H = 3.1
    r5.text("R503 2단계 재질 견본 · 1줄: 1 m 크기  ·  2줄: 2×2 반복(이음새 확인)", W / 2, 2.95, .12)
    for i, (name, _, tile, _, kind, label) in enumerate(MATERIALS):
        x = step / 2 + i * step
        span = 1. / tile          # one swatch = 1 m
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
    path = os.path.join(HERE, "swatch_render.png")
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
    sheet_ = np.concatenate([R, bar, top], 0)
    out = bpy.data.images.new("compare", W, sheet_.shape[0], alpha=True)
    out.pixels.foreach_set(sheet_.ravel())
    out.filepath_raw = os.path.join(HERE, "swatch_compare.png")
    out.file_format = "PNG"
    out.save()
    print("wrote", out.filepath_raw)


if "--bake" in ARGS:
    for name, build, tile, metallic, _, _ in MATERIALS:
        if ONLY and ONLY not in name:
            continue
        r5.bake(name, build, int(RES * tile / 2), metallic=metallic)
if "--preview" in ARGS:
    preview()
