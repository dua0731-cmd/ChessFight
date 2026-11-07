# R510 H King's Chess Hall: the three new phase-3 swatches, on top of the R504 set (r504_material_swatches.py).
#
# The H concept swatch row (Docs/PawnRush/ConceptArt/R510_KingsChessHall/H_03_props_materials.png) has ten cubes. Seven
# are already baked by the G chat and are reused as they are (user 11-08: reuse other chats' assets):
#   cream marble, navy marble, maple, walnut, purple velvet, matte brass  -> PR_Late_* (R504)
#   ivory piece                                                         -> PR_Late_MarbleCream (same as G's ivory rook)
# New here (1 m tiles, continuous sheets, so every noise is the R504 torus noise and the tile wraps exactly):
#   PR_Late_FrozenStone  BaseColor · Normal · Roughness · Metallic · Emission   grey stone with thin gold cracks.
#                        Metallic and Emission are the crack mask: Unity lights the cracks (functional gold #E2B23C).
#   PR_Late_Ebony        BaseColor · Normal · Roughness                         polished dark-brown ebony wood, fine grain
#   PR_Late_Bronze       BaseColor · Normal · Roughness · Metallic              warm cast bronze for the hall bell
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r510_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import os, sys, math, shutil
import bpy, bmesh
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
# import the R504 helpers without letting them see our arguments (they would bake the whole R504 set)
_argv = sys.argv
sys.argv = _argv[:_argv.index("--")] if "--" in _argv else list(_argv)
sys.path.insert(0, HERE)
import r504_material_swatches as m
sys.argv = _argv

RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048
m.ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None
CONCEPT = os.path.join(m.ROOT, "Docs", "PawnRush", "ConceptArt", "R510_KingsChessHall", "H_03_props_materials.png")
GOLD = "#E2B23C"
P = 1.


def ridge(nt, c, fx, fy, width, amt, detail=3., dist=.6, seed=0.):
    """Thin wandering lines where the periodic noise crosses 0.5 (1 on the line, 0 away from it)."""
    n = m.tnoise(nt, c, P, fx, fy, detail, .55, dist, seed)
    return m.maprange(nt, m.math_(nt, "ABSOLUTE", m.math_(nt, "SUBTRACT", n, .5)), 0., width, amt, 0.)


def crack_mask(nt, c):
    big = ridge(nt, c, 1.6, 2.2, .010, 1., 4., 1.2, 1.)
    small = ridge(nt, c, 4., 4., .006, 1., 3., .8, 4.)
    # small cracks only near the big ones, so they read as branches rather than a second net
    near = m.maprange(nt, m.math_(nt, "ABSOLUTE", m.math_(nt, "SUBTRACT", m.tnoise(nt, c, P, 1.6, 2.2, 4., .55, 1.2, 1.), .5)),
                      .01, .035, 1., 0.)
    return m.math_(nt, "MAXIMUM", big, m.math_(nt, "MULTIPLY", small, near))


# ------------------------------------------------------------------ frozen king: grey stone, gold cracks
def frozen_albedo(nt, c):
    mott = m.tnoise(nt, c, P, 4., 4., 5., .6, 0., 2.)
    stone = m.ramp(nt, mott, [(.3, "#807C77"), (.5, "#8C8882"), (.7, "#98948E")])
    grain = m.maprange(nt, m.tnoise(nt, c, P, 180., 180., 2., .5, 0., 9.), .3, .7, .88, 1.08)
    stone = m.vscale(nt, stone, grain)
    # a darker rim along each crack, then the gold fill
    rim = m.math_(nt, "MULTIPLY", m.maprange(nt, m.math_(nt, "ABSOLUTE", m.math_(nt, "SUBTRACT",
                  m.tnoise(nt, c, P, 1.6, 2.2, 4., .55, 1.2, 1.), .5)), 0., .013, .45, 0.), 1.)
    stone = m.mixc(nt, rim, stone, m.lin("#4A4743"))
    return m.mixc(nt, crack_mask(nt, c), stone, m.lin(GOLD))


def frozen_rough(nt, c):
    stone = m.maprange(nt, m.tnoise(nt, c, P, 30., 30., 3., .5, 0., 6.), .3, .7, .72, .9)
    return m.math_(nt, "SUBTRACT", stone, m.math_(nt, "MULTIPLY", crack_mask(nt, c), .5))


def frozen_bump(nt, c):
    grain = m.tnoise(nt, c, P, 180., 180., 2., .5, 0., 9.)
    pits = m.tnoise(nt, c, P, 30., 30., 3., .5, 0., 6.)
    surf = m.math_(nt, "MULTIPLY_ADD", grain, .3, m.math_(nt, "MULTIPLY", pits, .5))
    return m.math_(nt, "SUBTRACT", surf, m.math_(nt, "MULTIPLY", crack_mask(nt, c), 1.2))


def build_frozen():
    mat = m.bake_material("PR_Late_FrozenStone", frozen_albedo, frozen_rough, crack_mask, frozen_bump, .35, .002, .02)
    m.sheet("stone", mat, P, 0, None, 0., .5)
    return P


# ------------------------------------------------------------------ ebony: polished dark-brown wood (H_03 swatch)
def ebony_albedo(nt, c):
    cloud = m.tnoise(nt, c, P, 2., 3., 5., .6, .4, 3.)
    base = m.ramp(nt, cloud, [(.3, "#1C130E"), (.5, "#2A1C14"), (.7, "#36251A")])
    grain = m.maprange(nt, m.tnoise(nt, c, P, 6., 160., 3., .55, 1.5, 5.), .3, .7, .78, 1.15)
    return m.vscale(nt, base, grain)


def ebony_rough(nt, c):
    return m.maprange(nt, m.tnoise(nt, c, P, 6., 6., 4., .55, 0., 7.), .3, .7, .16, .3)


def ebony_bump(nt, c):
    return m.math_(nt, "MULTIPLY", m.tnoise(nt, c, P, 6., 160., 3., .55, 1.5, 5.), .3)


def build_ebony():
    mat = m.bake_material("PR_Late_Ebony", ebony_albedo, ebony_rough, 0., ebony_bump, .05, .001, .02)
    m.sheet("stone", mat, P, 0, None, 0., .5)
    return P


# ------------------------------------------------------------------ bronze: warm cast bell metal, darker in the hollows
def bronze_albedo(nt, c):
    cast = m.tnoise(nt, c, P, 5., 5., 5., .6, 0., 4.)
    col = m.ramp(nt, cast, [(.3, "#7A4E26"), (.5, "#94602F"), (.7, "#A97240")])
    fine = m.maprange(nt, m.tnoise(nt, c, P, 120., 120., 2., .5, 0., 6.), .3, .7, .9, 1.06)
    patina = m.maprange(nt, m.tnoise(nt, c, P, 3., 3., 5., .6, 0., 12.), .58, .76, 0., .4)
    return m.mixc(nt, patina, m.vscale(nt, col, fine), m.lin("#4E3A22"))


def bronze_rough(nt, c):
    return m.math_(nt, "ADD", m.maprange(nt, m.tnoise(nt, c, P, 120., 120., 2., .5, 0., 6.), .3, .7, .32, .44),
                   m.maprange(nt, m.tnoise(nt, c, P, 3., 3., 5., .6, 0., 12.), .58, .76, 0., .25))


def bronze_bump(nt, c):
    return m.math_(nt, "MULTIPLY_ADD", m.tnoise(nt, c, P, 120., 120., 2., .5, 0., 6.), .4,
                   m.tnoise(nt, c, P, 5., 5., 5., .6, 0., 4.))


def build_bronze():
    mat = m.bake_material("PR_Late_Bronze", bronze_albedo, bronze_rough, .95, bronze_bump, .1, .001, .02)
    m.sheet("sheet", mat, P, 0, None, 0., .5)
    return P


m.HEAL.update({"PR_Late_FrozenStone": (.12, .12), "PR_Late_Ebony": (.12, .12), "PR_Late_Bronze": (.12, .12)})


# ------------------------------------------------------------------ preview next to the H swatch row
# H_03_props_materials.png swatch row (fractions of the image): x 60..1475 / 1536, y 880..1010 / 1024. Ten cubes.
m.CROP = (60 / 1536, 1475 / 1536, 878 / 1024, 1012 / 1024)
m.CONCEPT = CONCEPT
ENTRIES = [("PR_Late_MarbleCream", "base", "크림 대리석 (R504)"),
           ("PR_Late_MarbleNavy", "base", "남색 대리석 (R504)"),
           ("PR_Late_Maple", "base", "메이플 칸 (R504)"),
           ("PR_Late_Walnut", "base", "월넛 칸 (R504)"),
           ("PR_Late_FrozenStone", "frozen", "굳은 돌·금 금 (새)"),
           ("PR_Late_Ebony", "base", "흑단 기물 (새)"),
           ("PR_Late_MarbleCream", "base", "상아 기물 = 크림 대리석"),
           ("PR_Late_Velvet", "base", "보라 벨벳 (R504)"),
           ("PR_Late_Brass", "metal", "무광 황동 (R504)"),
           ("PR_Late_Bronze", "metal", "청동 종 (새)")]
TILE_M = dict(m.TILE_M, PR_Late_FrozenStone=1., PR_Late_Ebony=1., PR_Late_Bronze=1.)


def preview_mat(name, kind, span):
    mat = m.preview_material(name, "metal" if kind == "frozen" else kind, span)
    if kind == "frozen":      # the game lights the cracks: emission = crack mask x functional gold
        nt = mat.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        mp = [n for n in nt.nodes if n.type == "MAPPING"][0]
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = m.load_img(os.path.join(m.TEX, f"{name}_Emission.png"), "Non-Color")
        m.link(nt, mp.outputs[0], t.inputs["Vector"])
        bsdf.inputs["Emission Color"].default_value = m.lin(GOLD)
        m.link(nt, t.outputs["Color"], bsdf.inputs["Emission Strength"])
    return mat


def preview():
    sc = m.reset()
    step, size = 1.25, 1.
    W = step * len(ENTRIES)
    H = 3.2
    z1, z2 = 2.15, .82
    m.text("R510 H 체스 홀 견본 · 새 3종 + R504 세트 재사용 · 1줄: 1 m 크기  ·  2줄: 2×2 반복", W / 2, 3.0, .13)
    for i, (name, kind, label) in enumerate(ENTRIES):
        x = step * (i + .5)
        tile = TILE_M.get(name, 1.)
        m.slab(preview_mat(name, kind, size / tile), x, z1, size, .07, 1.)
        m.slab(preview_mat(name, kind, 2.), x, z2, size, .012, 1.)
        m.text("2×2 반복", x, z2 - .62, .09)
        m.text(label, x, z1 - .64, .085)
    bd = bpy.data.meshes.new("bd")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.)
    bm.to_mesh(bd)
    bm.free()
    bd.materials.append(m.plain("backdrop", "#F5EDE0", .95))
    bo = bpy.data.objects.new("bd", bd)
    bo.rotation_euler = (math.pi / 2, 0, 0)
    bo.location = (W / 2, .12, H / 2)
    sc.collection.objects.link(bo)
    la = bpy.data.lights.new("key", "AREA")
    la.energy = 1300
    la.size = 6.
    la.color = (1., .96, .9)
    lo = bpy.data.objects.new("key", la)
    lo.location = (W / 2 - 4, -6, H + 3.5)
    lo.rotation_euler = (Vector((W / 2, 0, H / 2)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = m.lin("#FFF4E6")
    bg.inputs["Strength"].default_value = .5
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
    sc.cycles.device = "CPU"
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 2000
    sc.render.resolution_y = int(2000 * H / W)
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    path = os.path.join(HERE, "r510_swatch_render.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    # m.compare writes swatch_compare.png next to r504 (G's file): write to a temp HERE, then move it to our name
    keep = m.HERE
    m.HERE = m.WORK
    m.compare(path)
    m.HERE = keep
    shutil.move(os.path.join(m.WORK, "swatch_compare.png"), os.path.join(HERE, "r510_swatch_compare.png"))


if "--bake" in ARGS:
    m.bake("PR_Late_FrozenStone", build_frozen, RES // 2, metallic=True, height=False, ao=False)
    if not m.ONLY or m.ONLY == "PR_Late_FrozenStone":
        shutil.copyfile(os.path.join(m.TEX, "PR_Late_FrozenStone_Metallic.png"), os.path.join(m.TEX, "PR_Late_FrozenStone_Emission.png"))
    m.bake("PR_Late_Ebony", build_ebony, RES // 2, height=False, ao=False)
    m.bake("PR_Late_Bronze", build_bronze, RES // 2, metallic=True, height=False, ao=False)
if "--preview" in ARGS:
    preview()
