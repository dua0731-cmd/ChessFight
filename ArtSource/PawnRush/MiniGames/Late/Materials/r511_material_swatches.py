# R511 I King's Bedchamber: the two new phase-3 swatches, on top of the R504 / R510 sets.
#
# The I concept swatch row (Docs/PawnRush/ConceptArt/R511_KingsBedchamber/I_03_props_materials.png) has eleven cubes.
# Nine are already baked by other chats and are reused as they are (user 11-08: reuse other chats' assets):
#   cream marble, navy marble, ermine fur, matte brass                -> PR_Late_* (R504, G)
#   rail walnut                                                       -> PR_Late_Walnut (R504), darkened in Unity
#                                                                        toward the game's rail colour #6A6152
#   ebony piece                                                       -> PR_Late_Ebony (R510, H)
#   ivory piece                                                       -> PR_Late_MarbleCream (same as G / H ivory)
#   bed linen                                                         -> PR_Mid_DollLinen (R509, J; neutral linen)
# New here (1 m tiles; every noise is the R504 torus noise and the weave uses whole thread counts, so it wraps):
#   PR_Late_I_CarpetLight  BaseColor · Normal · Roughness · AO   light beige woven carpet (#CBBF9F)
#   PR_Late_I_CarpetDark   BaseColor · Normal · Roughness · AO   darker beige woven carpet (#A79A7C)
#   PR_Late_I_Velvet       BaseColor · Normal · Roughness · AO   the concept's plum violet (#532051, user 11-08 "퍼플 더
#                          레퍼런스 사진과 같이"); G's PR_Late_Velvet is a brighter blue violet and stays for G / H
# The game draws the 64 squares itself (MgCheckBlock, low contrast on purpose): Unity only swaps their materials.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r511_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import os, sys, math, shutil
import bpy, bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
_argv = sys.argv
sys.argv = _argv[:_argv.index("--")] if "--" in _argv else list(_argv)
sys.path.insert(0, HERE)
import r504_material_swatches as m
sys.argv = _argv

RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048
m.ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None
CONCEPT = os.path.join(m.ROOT, "Docs", "PawnRush", "ConceptArt", "R511_KingsBedchamber", "I_03_props_materials.png")
MID_TEX = os.path.join(HERE, "..", "..", "Mid", "Materials", "Textures")
P = 1.
THREADS = 96            # threads per metre (about 1 cm): even, so the half-frequency basket pattern also wraps


def weave(nt, c):
    """Basket weave height 0..1: warp threads in one cell, weft in the next (period P / THREADS * 2)."""
    sx = nt.nodes.new("ShaderNodeSeparateXYZ")
    m.link(nt, c["obj"], sx.inputs[0])
    k = math.pi * THREADS / P
    # a little wobble so the rows do not look ruled (periodic, from the torus noise)
    wob = m.math_(nt, "MULTIPLY", m.math_(nt, "SUBTRACT", m.tnoise(nt, c, P, 6., 6., 3., .5, 0., 11.), .5), .25)
    x = m.math_(nt, "ADD", m.math_(nt, "MULTIPLY", sx.outputs["X"], k), wob)
    y = m.math_(nt, "ADD", m.math_(nt, "MULTIPLY", sx.outputs["Y"], k), wob)
    warp = m.math_(nt, "ABSOLUTE", m.math_(nt, "SINE", x))
    weft = m.math_(nt, "ABSOLUTE", m.math_(nt, "SINE", y))
    cell = m.math_(nt, "GREATER_THAN", m.math_(nt, "MULTIPLY", m.math_(nt, "SINE", m.math_(nt, "MULTIPLY", x, .5)),
                                                  m.math_(nt, "SINE", m.math_(nt, "MULTIPLY", y, .5))), 0.)
    return m.math_(nt, "ADD", m.math_(nt, "MULTIPLY", warp, cell),
                   m.math_(nt, "MULTIPLY", weft, m.math_(nt, "SUBTRACT", 1., cell)))


def carpet_albedo(stops):
    def f(nt, c):
        mott = m.tnoise(nt, c, P, 3., 3., 5., .6, .3, 2.)
        col = m.ramp(nt, mott, stops)
        fuzz = m.maprange(nt, m.tnoise(nt, c, P, 220., 220., 2., .5, 0., 8.), .3, .7, .94, 1.04)
        threads = m.maprange(nt, weave(nt, c), 0., 1., .86, 1.03)
        return m.vscale(nt, m.vscale(nt, col, fuzz), threads)
    return f


def carpet_rough(nt, c):
    return m.maprange(nt, m.tnoise(nt, c, P, 220., 220., 2., .5, 0., 8.), .3, .7, .86, .97)


def carpet_bump(nt, c):
    return m.math_(nt, "MULTIPLY_ADD", m.tnoise(nt, c, P, 220., 220., 2., .5, 0., 8.), .25, weave(nt, c))


def build_velvet_i():
    P_ = 1.
    def albedo(nt, c):
        crush = m.tnoise(nt, c, P_, 3., 6., 5., .6, 1.5)
        col = m.ramp(nt, crush, [(.2, "#380B3C"), (.5, "#4E1250"), (.8, "#64206A")])
        pile = m.maprange(nt, m.tnoise(nt, c, P_, 260., 260., 2., .5, 0., 7.), .3, .7, .93, 1.04)
        return m.vscale(nt, col, pile)
    mat = m.bake_material("PR_Late_I_Velvet", albedo, .92, 0., m.velvet_bump(P_), .35, .004, .04)
    terms = m.sine_terms(5046, 10, 4, .004)
    m.sheet("cloth", mat, P_, 60, lambda w: m.psines(w, P_, terms))
    return P_


LIGHT = [(.3, "#C4B897"), (.5, "#CBBF9F"), (.7, "#D2C7A8")]
DARK = [(.3, "#A09373"), (.5, "#A79A7C"), (.7, "#AEA285")]


def build_carpet(name, stops):
    def build():
        mat = m.bake_material(name, carpet_albedo(stops), carpet_rough, 0., carpet_bump, .35, .002, .015)
        m.sheet("carpet", mat, P, 0, None, 0., .5)
        return P
    return build


m.HEAL.update({"PR_Late_I_CarpetLight": (.12, .12), "PR_Late_I_CarpetDark": (.12, .12), "PR_Late_I_Velvet": (.12, .12)})

# ------------------------------------------------------------------ preview next to the I swatch row
# I_03_props_materials.png swatch row (fractions of the image): x 178..1496 / 1536, y 884..1012 / 1024. Eleven cubes.
m.CROP = (178 / 1536, 1496 / 1536, 884 / 1024, 1012 / 1024)
m.CONCEPT = CONCEPT
ENTRIES = [("PR_Late_I_CarpetLight", "base", "밝은 카펫 (새)"),
           ("PR_Late_I_CarpetDark", "base", "어두운 카펫 (새)"),
           ("PR_Late_Walnut", "base", "레일 = 월넛 (R504)"),
           ("PR_Late_MarbleCream", "base", "크림 대리석 (R504)"),
           ("PR_Late_MarbleNavy", "base", "남색 대리석 (R504)"),
           ("PR_Late_I_Velvet", "base", "자두빛 벨벳 (새)"),
           ("PR_Late_Ermine", "base", "담비털 (R504)"),
           ("PR_Mid_DollLinen", "base", "리넨 (R509 J)"),
           ("PR_Late_Brass", "metal", "무광 황동 (R504)"),
           ("PR_Late_Ebony", "base", "흑단 기물 (R510)"),
           ("PR_Late_MarbleCream", "base", "상아 = 크림 대리석")]
TILE_M = dict(m.TILE_M, PR_Late_I_Velvet=1., PR_Late_I_CarpetLight=1., PR_Late_I_CarpetDark=1., PR_Late_Ebony=1., PR_Mid_DollLinen=.5)


def preview_mat(name, kind, span):
    if name.startswith("PR_Mid_"):          # J's linen lives in the mid-stage texture folder
        keep = m.TEX
        m.TEX = MID_TEX
        try:
            return m.preview_material(name, kind, span)
        finally:
            m.TEX = keep
    mat = m.preview_material(name, kind, span)
    if name == "PR_Late_I_Velvet":           # same sheen as G's velvet preview, tinted to the plum
        b = mat.node_tree.nodes["Principled BSDF"]
        b.inputs["Sheen Weight"].default_value = .6
        b.inputs["Sheen Tint"].default_value = m.lin("#B08DB8")
    return mat


def preview():
    sc = m.reset()
    step, size = 1.25, 1.
    W = step * len(ENTRIES)
    H = 3.2
    z1, z2 = 2.15, .82
    m.text("R511 I 침실 견본 · 새 3종 + 다른 방 재사용 8종 · 1줄: 1 m 크기  ·  2줄: 2×2 반복", W / 2, 3.0, .13)
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
    sc.render.resolution_x = 2200
    sc.render.resolution_y = int(2200 * H / W)
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    path = os.path.join(HERE, "r511_swatch_render.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    keep = m.HERE
    m.HERE = m.WORK
    m.compare(path)
    m.HERE = keep
    shutil.move(os.path.join(m.WORK, "swatch_compare.png"), os.path.join(HERE, "r511_swatch_compare.png"))


if "--bake" in ARGS:
    m.bake("PR_Late_I_CarpetLight", build_carpet("PR_Late_I_CarpetLight", LIGHT), RES // 2, height=False)
    m.bake("PR_Late_I_CarpetDark", build_carpet("PR_Late_I_CarpetDark", DARK), RES // 2, height=False)
    m.bake("PR_Late_I_Velvet", build_velvet_i, RES // 2, height=False)
if "--preview" in ARGS:
    preview()
