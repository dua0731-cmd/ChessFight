"""Pawn Rush library (작전 회의실): the blower cannons and the conveyor painted like their concept images — preview (R368).

    blender --background --factory-startup --python cannon_conveyor_look.py -- --preview D:/check   (renders, writes nothing)

승규 님 sent the two concept images (the cannon: cream stone barrel and base, teal plates with gold rook emblems,
gold bands, a wide gold nozzle ring, dark steel blades, gold hub and corner caps; the conveyor: dark slate deck
tiles with gold star inlays and cream grout, cream borders, teal steel rails, gold brackets, stone legs) with
"텍스쳐 입혀".

The cannon body, its fan and the conveyor's chassis are welded sculpts without UVs and with large faces, so a part
per face gives saw-tooth borders. Instead each gets a smart UV unwrap, Cycles bakes its object-space position and
normal per texel, and numpy decides the part per texel (by where it is and which way it faces: crisp bands, the
nozzle's inside, the rook emblem projected on the teal plates), then writes <name>.png (base colour, sRGB) and
<name>_MS.png (metallic RGB, smoothness A) — the same maps Unity will use. The belt links are the source deck panel
cut in eight 0.43 m strips (Shuttle_source_panel_piece_00 .. 07): each strip gets UVs back in the panel's space and
the deck is painted to the sculpt's own layout (border, rivet strips, side tiles, 2 x 7 planks, the X and the star).
The small machined parts (rails, piers, idlers, shoes ...) are flat parts by name.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from war_map import Canvas, PPM, fbm  # noqa: E402

LIB = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets",
                                    "Resources", "PawnRushLibrary"))
ENV, SHUTTLE = os.path.join(LIB, "PawnRushLibraryEnvironment.fbx"), os.path.join(LIB, "PawnRushLibraryShuttle.fbx")
CANNON = "BLOWER_A_left|Blower_GameReady_Arrowless|Dupli|"     # its mesh is shared with BLOWER_B_right's
FAN = "BLOWER_A_left_Fan"                                      # mesh shared with BLOWER_B_right_Fan
CHASSIS = "Uploaded_Platform_Traveling_Chassis"
PANEL_W, PANEL_L, STRIP = 3.358, 3.44, .43     # the source deck panel and one belt strip (m)

# Parts: base colour (linear), metallic, smoothness.
# R376 (승규 11-04, the reference again: "금색은 아닌데 그렇다고 노란색도 아니야 ... 레퍼런스는 중고 느낌에 먼지가 위에
# 뭍어 있어"): the conveyor's palette is worn, not new - old ochre brass (not gold, not yellow), dusty teal paint,
# grey stone, weathered wood; weather() then adds grime in the crevices and dust on what faces up.
PARTS = {
    "gold": ((.36, .22, .062), .55, .38),     # old brass
    "teal": ((.062, .145, .158), .2, .32),
    "cream": ((.60, .52, .40), 0, .18),
    "steel": ((.058, .062, .072), .6, .38),
    "stone": ((.20, .195, .205), 0, .14),
    "wood": ((.13, .07, .036), 0, .24),
    "dark": ((.03, .03, .033), .25, .3),
}
DUST = np.array((.47, .42, .34))      # pale grey-beige dust (linear)
NAMES = list(PARTS)


def srgb(lin):
    lin = np.clip(lin, 0, 1)
    return np.where(lin <= .0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - .055)


# ------------------------------------------------------------------ per-texel geometry

def smart_uv(ob):
    me = ob.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name="UVMap")
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    ob.hide_set(False)
    ob.select_set(True)
    view.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.003, area_weight=.7)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake_geometry(ob, size):
    """Object-space position and normal per texel of ob's UVs, and which texels are covered."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 1
    sc.render.bake.margin = 4
    sc.render.bake.use_clear = False
    sc.render.bake.normal_space = "OBJECT"
    tmp = bpy.data.materials.new("bake target")
    tmp.use_nodes = True
    keep = list(ob.data.materials)
    ob.data.materials.clear()
    ob.data.materials.append(tmp)
    out = {}
    for kind in ("POSITION", "NORMAL"):
        img = bpy.data.images.new(f"{ob.name} {kind}", size, size, alpha=True, float_buffer=True)
        img.colorspace_settings.name = "Non-Color"
        img.generated_color = (0, 0, 0, 0)
        tex = tmp.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = img
        tmp.node_tree.nodes.active = tex
        bpy.ops.object.bake(type=kind)
        out[kind] = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
    ob.data.materials.clear()
    for m in keep:
        ob.data.materials.append(m)
    inv = np.array(ob.matrix_world.inverted())
    pw = out["POSITION"][..., :3]
    p = pw @ inv[:3, :3].T + inv[:3, 3]
    n = out["NORMAL"][..., :3] * 2 - 1
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
    return p, n, out["POSITION"][..., 3] > .5


def noise3(p, seed=0):
    """Smooth 3D noise in [0, 1]: a few rotated sine products (no texture repeats over a prop)."""
    rng = np.random.default_rng(seed)
    acc = np.zeros(p.shape[:-1])
    amp, tot = 1.0, 0.0
    for octave in range(4):
        d = rng.normal(size=(3, 3))
        f = 3.0 * 2 ** octave
        ph = rng.random(3) * 6.3
        acc += amp * np.sin(p @ d[0] * f + ph[0]) * np.sin(p @ d[1] * f + ph[1]) * np.sin(p @ d[2] * f + ph[2])
        tot += amp
        amp *= .5
    return .5 + .5 * acc / tot


def write_maps(path_base, part, valid, p, emblem=None):
    """part: int index into NAMES per texel; emblem: 0..1 gold overlay. Writes <base>.png and <base>_MS.png."""
    size = part.shape[0]
    col = np.zeros((size, size, 3))
    metal = np.zeros((size, size))
    smooth = np.zeros((size, size))
    for i, name in enumerate(NAMES):
        m = part == i
        c, mt, sm = PARTS[name]
        col[m] = c
        metal[m] = mt
        smooth[m] = sm
    grime = noise3(p, 3)
    fine = noise3(p * 6, 9)
    col *= (.78 + .34 * grime * (.7 + .3 * fine))[..., None]
    smooth *= .85 + .3 * fine
    if emblem is not None:
        g = np.array(PARTS["gold"][0])
        col = col * (1 - emblem[..., None]) + g * emblem[..., None]
        metal = np.maximum(metal, emblem * .9)
        smooth = np.maximum(smooth, emblem * .7)
    col[~valid] = 0
    for suffix, rgb, alpha, colour in (("", srgb(col), np.ones((size, size)), "sRGB"),
                                        ("_MS", np.repeat(metal[..., None], 3, -1), np.clip(smooth, 0, 1), "Non-Color")):
        img = bpy.data.images.new(os.path.basename(path_base) + suffix, size, size, alpha=True)
        img.colorspace_settings.name = colour
        rgba = np.concatenate([rgb, alpha[..., None]], -1).astype(np.float32)
        img.pixels.foreach_set(rgba.ravel())   # baked rows are bottom first, as Blender keeps them
        img.filepath_raw = path_base + suffix + ".png"
        img.file_format = "PNG"
        img.save()
    counts = {n: int(((part == i) & valid).sum()) for i, n in enumerate(NAMES)}
    print("MAPS", os.path.basename(path_base), counts)


# ------------------------------------------------------------------ the parts, per texel (mesh space)

def cannon_parts(p, n, rook):
    """The cannon: axis along +x (nozzle at +x, fan at x .94), base below z .62, centre line y 0."""
    lx, ly, z = p[..., 0], p[..., 1], p[..., 2]
    axis_z = 1.27 + (lx + 1.96) / 2.87 * .23
    dy, dz = ly, z - axis_z
    r = np.hypot(dy, dz)
    inward = (n[..., 1] * dy + n[..., 2] * dz) / np.maximum(r, 1e-6) < -.35
    side = np.abs(n[..., 1]) > .55
    P = {k: i for i, k in enumerate(NAMES)}
    part = np.full(lx.shape, P["cream"])
    emblem_zone = np.zeros(lx.shape, bool)
    above = z >= .62
    # barrel bands along x (back cap, straps, the teal plate, the band before the nozzle)
    bands = (lx < -1.71) | ((lx > -1.2) & (lx < -1.08)) | ((lx > -.2) & (lx < -.08)) | ((lx > .27) & (lx < .39))
    part[above & bands] = P["gold"]
    plate = above & (lx >= -1.08) & (lx <= -.2)
    part[plate] = P["teal"]
    emblem_zone |= plate & side & (dz > .2)
    # the nozzle: gold outside, steel inside
    noz = above & (lx > .39)
    part[noz] = P["gold"]
    part[noz & inward & (r < 1.0)] = P["steel"]
    # the mount and trunnions
    mount = above & (lx > -1.15) & (lx < -.05) & (np.abs(ly) > .5) & (z < axis_z + .2) & (r > .62)
    part[mount] = P["teal"]
    part[mount & ((np.abs(ly) > 1.0) | (n[..., 2] > .75))] = P["gold"]
    # the base: cream blocks, teal side panels (the rook on the long sides), gold corner caps, a turntable
    base = ~above
    part[base] = P["cream"]
    panel = base & (np.abs(n[..., 2]) < .5) & (z > .12) & (z < .47)
    part[panel] = P["teal"]
    emblem_zone |= panel & side
    rim = base & (np.abs(n[..., 2]) < .5) & (((z > .1) & (z < .14)) | ((z > .45) & (z < .49)))
    part[rim] = P["gold"]
    table = base & (n[..., 2] > .7) & (z > .45) & (np.hypot(lx + .45, ly) < 1.0)
    part[table] = P["dark"]
    part[table & (np.hypot(lx + .45, ly) > .78)] = P["gold"]
    part[base & (np.abs(lx + .39) > .88) & (np.abs(ly) > .98)] = P["gold"]
    # the rook: projected on x-z on the plate above the trunnion and on the base panel, only where still teal
    emblem_zone &= part == P["teal"]
    emblem = np.zeros(lx.shape)
    for x0, z0, s in ((-.64, 1.86, .34), (-.39, .30, .3)):
        u = (lx - (x0 - s / 2)) / s
        v = (z - (z0 - s / 2)) / s
        inside = (u >= 0) & (u < 1) & (v >= 0) & (v < 1)
        h, w = rook.shape
        iu = np.clip((u * w).astype(int), 0, w - 1)
        iv = np.clip(((1 - v) * h).astype(int), 0, h - 1)
        emblem = np.maximum(emblem, np.where(inside, rook[iv, iu], 0))
    return part, emblem * emblem_zone


def fan_parts(p, n):
    P = {k: i for i, k in enumerate(NAMES)}
    part = np.where(np.hypot(p[..., 1], p[..., 2]) < .2, P["gold"], P["steel"])
    return part


def chassis_parts(p, n):
    """The travelling chassis (mesh space: deck along y from -6.93 to 6.9, belt top at z ~ .2)."""
    x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
    P = {k: i for i, k in enumerate(NAMES)}
    part = np.full(x.shape, P["steel"])
    part[x > 1.62] = P["wood"]
    rail = (x > 1.85) & (z >= -.95) & (z <= .05)
    part[rail] = P["teal"]
    tile = np.mod(y + 6.93, PANEL_L)
    part[rail & ((tile < .2) | (tile > PANEL_L - .2)) & (z > -.45)] = P["gold"]
    legs = (z < -.95) & (z >= -2.25)
    part[legs] = P["teal"]
    part[legs & (x > 1.45)] = P["stone"]
    part[legs & (x > 1.45) & ((z < -2.05) | (z > -1.15))] = P["gold"]
    part[z < -2.25] = P["gold"]
    part[z > .05] = P["gold"]
    return part


FIXED = (("I_flange", "teal"), ("I_web", "teal"), ("diagonal", "teal"), ("hanger", "teal"), ("crossbeam", "teal"),
         ("support_pier", "stone"), ("takeoff", "cream"), ("ramp", "cream"), ("drum", "steel"), ("idler", "steel"))


def fixed_part(name):
    for key, part in FIXED:
        if key in name:
            return part
    return "gold"   # saddles, pier shoes, bearing blocks, axle caps, side beads, rail shoes


# ------------------------------------------------------------------ images drawn by hand

def paint_rook(size=400):
    """1 inside a framed chess rook, 0 outside (rows top to bottom)."""
    cv = Canvas(size / PPM, size / PPM)
    w = cv.wm

    def rect(x0, y0, x1, y1):
        cv.convex([(x0 * w, y0 * w), (x0 * w, y1 * w), (x1 * w, y1 * w), (x1 * w, y0 * w)], (1, 1, 1))
    rect(.06, .05, .94, .09)
    rect(.06, .91, .94, .95)
    rect(.06, .05, .10, .95)
    rect(.90, .05, .94, .95)
    rect(.30, .78, .70, .85)
    rect(.34, .72, .66, .78)
    rect(.38, .38, .62, .72)
    rect(.33, .30, .67, .38)
    for i in range(3):
        x = .33 + i * .135
        rect(x, .18, x + .07, .30)
    return cv.c[..., 0]


def paint_deck(path_base):
    """One deck panel to the sculpt's layout; x across (-1.679 .. 1.679), d down from the panel's +y end (0 .. 3.44).
    Writes <base>.png and <base>_MS.png."""
    rng = np.random.default_rng(5)
    scale = .6
    cvs = []
    for metal_pass in (False, True):
        cv = Canvas(PANEL_W * scale, PANEL_L * scale)
        cv.aa = 1.3 / PPM

        def m(x, d):
            return ((x + PANEL_W / 2) * scale, d * scale)

        def rect(x0, d0, x1, d1, col):
            a, b = m(x0, d0), m(x1, d1)
            cv.convex([a, (a[0], b[1]), b, (b[0], a[1])], col)

        def line(pts, wd, col, alpha=1.0):
            cv.polyline([(m(x, d)[0] / cv.wm, m(x, d)[1] / cv.hm) for x, d in pts], wd * scale, col, alpha=alpha)

        def dot(x, d, r, col):
            cv.disc(m(x, d), r * scale, col)
        cream = (.80, .75, .66) if not metal_pass else (0, 0, 0)
        cream_dk = (.70, .65, .56) if not metal_pass else (0, 0, 0)
        slate = np.array((.27, .255, .27))
        gold = (.76, .60, .30) if not metal_pass else (1, 1, 1)     # old brass inlay
        wood = (.46, .34, .24) if not metal_pass else (0, 0, 0)
        dark = (.14, .13, .13) if not metal_pass else (0, 0, 0)
        if metal_pass:
            cv.c[:] = 0
        else:
            t = rng.random((cv.h // 40 + 2, cv.w // 40 + 2))
            t = np.kron(t, np.ones((40, 40)))[:cv.h, :cv.w]
            cv.c[:] = slate * (.88 + .2 * t)[..., None]
        rect(-1.679, 0, 1.679, 3.44, cream)                    # borders (the field is drawn over)
        for s in (-1, 1):
            rect(s * 1.52, 0, s * 1.679, 3.44, wood)            # the raised side guard: wood
            rect(s * 1.33, .19, s * 1.52, 2.94, wood)           # rivet strips
            for d in np.arange(.35, 2.9, .55):
                dot(s * 1.425, d, .025, gold)
            rows = (.19, .78, 1.11, 1.45, 1.77, 2.04, 2.38, 2.82, 2.94)
            for a, b in zip(rows, rows[1:]):                    # side tiles
                rect(s * 1.10, a + .015, s * 1.32, b - .015, cream_dk)
        if not metal_pass:
            rect(-1.09, .19, 1.09, 2.94, slate)
        else:
            rect(-1.09, .19, 1.09, 2.94, (0, 0, 0))
        for d in (.78, 1.14, 1.47, 1.77, 2.15, 2.38):          # planks
            line([(-1.09, d), (1.09, d)], .02, cream, .9)
        line([(0, .19), (0, 2.94)], .02, cream, .9)
        # gold: the X, the cross through the star, a star and a ring at the crossing, a rim round the field
        cx, cd = 0.0, 1.47
        for ex, ed in ((-1.09, .19), (1.09, .19), (-1.09, 2.94), (1.09, 2.94)):
            line([(cx, cd), (ex, ed)], .03, gold)
        line([(0, .19), (0, 2.94)], .02, gold)
        line([(-1.09, cd), (1.09, cd)], .02, gold)
        line([(-1.09, .19), (1.09, .19), (1.09, 2.94), (-1.09, 2.94), (-1.09, .19)], .025, gold)
        c = m(cx, cd)
        for i in range(8):
            a = i * math.pi / 4
            ln = (.36 if i % 2 == 0 else .22) * scale
            wd = .055 * scale
            tip = (c[0] + ln * math.sin(a), c[1] - ln * math.cos(a))
            l = (c[0] + wd * math.cos(a), c[1] + wd * math.sin(a))
            r = (c[0] - wd * math.cos(a), c[1] - wd * math.sin(a))
            cv.convex([c, tip, l], gold)
            cv.convex([c, r, tip], gold if metal_pass else (.52, .40, .20))
        cv.ring(c, .26 * scale, .025 * scale, gold)
        for d in (.09, 3.2):
            for x in np.linspace(-1.3, 1.3, 9):
                dot(x, d, .03, gold)                            # border rivets
        if not metal_pass:                                      # scuffs and scratches on the slate
            srng = np.random.default_rng(17)
            for _ in range(140):
                x0, d0 = srng.uniform(-1.05, 1.05), srng.uniform(.25, 2.9)
                a = srng.uniform(0, math.pi)
                ln = srng.uniform(.04, .22)
                line([(x0, d0), (x0 + ln * math.cos(a), d0 + ln * math.sin(a))], srng.uniform(.002, .005),
                     (.55, .52, .50), srng.uniform(.15, .4))
        cvs.append(cv.c.copy())
    col, metal = cvs
    h, w = col.shape[:2]
    mval = metal[..., 0]
    # wear: the brass tarnished in patches, grime blotches, dust settled over it all (heavier along the edges)
    wrng = np.random.default_rng(23)
    tarnish = mval * np.clip(fbm(h, w, 14, 4, wrng) * 1.8 - .55, 0, 1)
    col = col * (1 - .55 * tarnish[..., None]) + np.array((.30, .24, .15)) * (.55 * tarnish[..., None])
    mval = mval * (1 - .5 * tarnish)
    grime = np.clip(fbm(h, w, 7, 5, wrng) * 1.5 - .45, 0, 1) * .3
    col *= (1 - grime)[..., None]
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)) / (.25 * w)
    dust = np.clip(fbm(h, w, 5, 5, wrng) * 1.7 - .55, 0, 1) * .34 + .05 + np.clip(1 - edge, 0, 1) * .16
    dust += np.clip(fbm(h, w, 160, 2, wrng) - .62, 0, 1) * .45
    dust = np.clip(dust, 0, .8)
    col = col * (1 - dust[..., None]) + np.array((.73, .69, .62)) * dust[..., None]
    mval = mval * (1 - dust)
    smooth = (.16 + .26 * mval) * (1 - .6 * dust)
    for suffix, rgb, alpha, colour in (("", col, np.ones((h, w)), "sRGB"),
                                        ("_MS", np.repeat(mval[..., None] * .9, 3, -1), smooth, "Non-Color")):
        img = bpy.data.images.new(os.path.basename(path_base) + suffix, w, h, alpha=True)
        img.colorspace_settings.name = colour
        rgba = np.concatenate([rgb, alpha[..., None]], -1)[::-1].astype(np.float32)
        img.pixels.foreach_set(rgba.ravel())
        img.filepath_raw = path_base + suffix + ".png"
        img.file_format = "PNG"
        img.save()


def deck_uvs():
    """Each belt strip k is the source panel's k-th 0.43 m from its +y end: UVs back in the panel's space."""
    for k in range(8):
        me = bpy.data.meshes["Shuttle_source_panel_piece_%02d" % k]
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        uv = me.uv_layers.new(name="UVMap")
        for loop in me.loops:
            co = me.vertices[loop.vertex_index].co
            u = (co.x + PANEL_W / 2) / PANEL_W
            v = 1 - (k * STRIP + (STRIP / 2 - co.y)) / PANEL_L
            uv.data[loop.index].uv = (u, v)


# ------------------------------------------------------------------ materials (as Unity will draw them)

def mapped(name, base):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    col = nt.nodes.new("ShaderNodeTexImage")
    col.image = bpy.data.images.load(base + ".png", check_existing=True)
    nt.links.new(col.outputs["Color"], b.inputs["Base Color"])
    ms = nt.nodes.new("ShaderNodeTexImage")
    ms.image = bpy.data.images.load(base + "_MS.png", check_existing=True)
    ms.image.colorspace_settings.name = "Non-Color"
    nt.links.new(ms.outputs["Color"], b.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs["Value"], b.inputs["Roughness"])
    return m


def flat(part):
    name = "Library " + part
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        c, mt, sm = PARTS[part]
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Metallic"].default_value = mt
        b.inputs["Roughness"].default_value = 1 - sm
    return m


def assign(me, m):
    me.materials.clear()
    me.materials.append(m)
    for poly in me.polygons:
        poly.material_index = 0


# ------------------------------------------------------------------ building the looks (shared with the Unity step)

def dress_cannon(out, size=2048):
    ob = bpy.data.objects[CANNON]
    smart_uv(ob)
    p, n, valid = bake_geometry(ob, size)
    part, emblem = cannon_parts(p, n, paint_rook())
    write_maps(os.path.join(out, "LibraryCannon"), part, valid, p, emblem)
    assign(ob.data, mapped("Library cannon", os.path.join(out, "LibraryCannon")))
    fan = bpy.data.objects[FAN]
    smart_uv(fan)
    p, n, valid = bake_geometry(fan, 512)
    write_maps(os.path.join(out, "LibraryCannonFan"), fan_parts(p, n), valid, p)
    assign(fan.data, mapped("Library cannon fan", os.path.join(out, "LibraryCannonFan")))


def bake_set(objs, size, codes=None):
    """Bakes, per texel of the objects' shared UV space: world position, object-space normal, ambient occlusion
    and (with codes, one part index per object) the part. Returns bottom-first arrays and the covered texels."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.render.bake.margin = 4
    sc.render.bake.use_clear = False
    sc.render.bake.normal_space = "OBJECT"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("bake world")
    sc.world.light_settings.distance = .6          # AO reach: crevices and joints, not the whole room
    keep = {o: list(o.data.materials) for o in objs}
    temp = {}
    for i, o in enumerate(objs):
        code = codes[i] if codes else 0
        if code not in temp:
            m = bpy.data.materials.new(f"bake code {code}")
            m.use_nodes = True
            nt = m.node_tree
            em = nt.nodes.new("ShaderNodeEmission")
            em.inputs["Color"].default_value = ((code + .5) / 10, 0, 0, 1)
            nt.links.new(em.outputs["Emission"], nt.nodes["Material Output"].inputs["Surface"])
            temp[code] = m
        o.data.materials.clear()
        o.data.materials.append(temp[code])
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    view.objects.active = objs[0]
    out = {}
    for kind, samples in (("POSITION", 1), ("NORMAL", 1), ("AO", 24), ("EMIT", 1)):
        if kind == "EMIT" and not codes:
            continue
        sc.cycles.samples = samples
        img = bpy.data.images.new(f"bake {kind}", size, size, alpha=True, float_buffer=True)
        img.colorspace_settings.name = "Non-Color"
        img.generated_color = (0, 0, 0, 0)
        nodes = []
        for m in temp.values():
            tex = m.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = img
            m.node_tree.nodes.active = tex
            nodes.append((m, tex))
        bpy.ops.object.bake(type=kind)
        for m, tex in nodes:
            m.node_tree.nodes.remove(tex)
        out[kind] = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
    for o, mats in keep.items():
        o.data.materials.clear()
        for m in mats:
            o.data.materials.append(m)
    n = out["NORMAL"][..., :3] * 2 - 1
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
    code = np.floor(out["EMIT"][..., 0] * 10).astype(int) if codes else None
    return out["POSITION"][..., :3], n, out["AO"][..., 0], code, out["POSITION"][..., 3] > .5


def weather(path_base, part, valid, p, n, ao, palette=None, dust_amount=1.0):
    """The worn look: each part's colour with large-scale variation, grime in the occluded joints, dust lying on what
    faces up (patchy, with fine specks). palette: [(colour, metallic, smoothness)] by part index (the conveyor's PARTS
    when None). Writes <base>.png and <base>_MS.png."""
    size = part.shape[0]
    col = np.zeros((size, size, 3))
    metal = np.zeros((size, size))
    smooth = np.zeros((size, size))
    palette = palette or [PARTS[name] for name in NAMES]
    for i, (c, mt, sm) in enumerate(palette):
        m = part == i
        col[m], metal[m], smooth[m] = c, mt, sm
    col *= (.82 + .3 * noise3(p * 1.3, 3))[..., None]
    grime = np.clip((1 - ao) * 1.5, 0, 1)
    col *= (1 - .62 * grime)[..., None]
    smooth *= 1 - .5 * grime
    up = np.clip((n[..., 2] - .3) / .5, 0, 1)
    dust = up * (np.clip(noise3(p * 2.4, 11) * 1.7 - .35, 0, 1) * .65 + .14)
    dust += up * np.clip(noise3(p * 16, 5) - .66, 0, 1) * .8
    dust += np.clip(noise3(p * 3.1, 7) * 1.4 - .9, 0, 1) * .15                # a light film on the sides too
    dust = np.clip(dust * (.75 + .5 * grime) * dust_amount, 0, .85)
    col = col * (1 - dust[..., None]) + DUST * dust[..., None]
    metal *= 1 - dust
    smooth *= 1 - .8 * dust
    col[~valid] = 0
    save_maps(path_base, col, metal, smooth)
    print("WEATHER", os.path.basename(path_base), {i: int(((part == i) & valid).sum()) for i in range(len(palette))},
          f"dust mean {dust[valid].mean():.2f}, grime mean {grime[valid].mean():.2f}")


def save_maps(path_base, col, metal, smooth):
    size = col.shape[0]
    for suffix, rgb, alpha, colour in (("", srgb(col), np.ones((size, size)), "sRGB"),
                                        ("_MS", np.repeat(metal[..., None], 3, -1), np.clip(smooth, 0, 1), "Non-Color")):
        img = bpy.data.images.new(os.path.basename(path_base) + suffix, size, size, alpha=True)
        img.colorspace_settings.name = colour
        img.pixels.foreach_set(np.concatenate([rgb, alpha[..., None]], -1).astype(np.float32).ravel())
        img.filepath_raw = path_base + suffix + ".png"
        img.file_format = "PNG"
        img.save()


def joint_uv(objs):
    """One smart unwrap over several objects, their islands packed together in one 0..1 square."""
    for o in objs:
        me = o.data
        while me.uv_layers:
            me.uv_layers.remove(me.uv_layers[0])
        me.uv_layers.new(name="UVMap")
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    view.objects.active = objs[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.002, area_weight=.7)
    bpy.ops.object.mode_set(mode="OBJECT")


def dress_conveyor(out, size=2048):
    """The deck panel painted; the chassis and, in one shared atlas, every small part (rails, piers, shoes, rollers,
    hangers, the ramp) baked and weathered. Materials: Library conveyor deck / chassis / parts."""
    deck_uvs()
    paint_deck(os.path.join(out, "LibraryConveyorDeck"))
    deck = mapped("Library conveyor deck", os.path.join(out, "LibraryConveyorDeck"))
    ch = bpy.data.objects[CHASSIS]
    smart_uv(ch)
    pw, n, ao, _, valid = bake_set([ch], size)
    inv = np.array(ch.matrix_world.inverted())
    p = pw @ inv[:3, :3].T + inv[:3, 3]
    weather(os.path.join(out, "LibraryConveyorChassis"), chassis_parts(p, n), valid, p, n, ao)
    chassis = mapped("Library conveyor chassis", os.path.join(out, "LibraryConveyorChassis"))
    small, seen = [], set()
    for o in bpy.data.objects:
        if o.type != "MESH" or o.data.name in seen:
            continue
        seen.add(o.data.name)
        if o.data.name.startswith("Shuttle_source_panel_piece_"):
            assign(o.data, deck)
        elif o.name == CHASSIS:
            assign(o.data, chassis)
        else:
            small.append(o)
    joint_uv(small)
    codes = [NAMES.index(fixed_part(o.name)) for o in small]
    pw, n, ao, code, valid = bake_set(small, size, codes)
    weather(os.path.join(out, "LibraryConveyorParts"), np.clip(code, 0, len(NAMES) - 1), valid, pw, n, ao)
    parts = mapped("Library conveyor parts", os.path.join(out, "LibraryConveyorParts"))
    for o in small:
        assign(o.data, parts)
    for o in bpy.data.objects:   # the belt links share strip meshes: every slot follows its mesh, or the FBX drops it
        for slot in getattr(o, "material_slots", ()):
            if slot.link == "OBJECT":
                slot.link = "DATA"
    print("CONVEYOR small parts in one atlas:", len(small))


# ------------------------------------------------------------------ preview

def stage(sc):
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy, sun.data.color, sun.data.angle = 3.5, (1.0, .88, .74), math.radians(2)
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.45, .48, .55, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .8
    sc.world = world
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.use_raytracing = True
    sc.eevee.taa_render_samples = 64
    sc.view_settings.view_transform = "AgX"
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1100
    return sun, cam


def shoot(sc, sun, cam, out, shots):
    for name, at, target, lens, turn in shots:
        cam.location = Vector(at)
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sun.rotation_euler = (math.radians(50), 0, math.radians(turn))
        sc.render.filepath = os.path.join(out, name)
        bpy.ops.render.render(write_still=True)
        print("WROTE", sc.render.filepath)


def preview(out, shuttle=SHUTTLE):
    """The conveyor only: the cannon is cannon_model.py's since R370 (dress_cannon painted the old sculpt)."""
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=shuttle)
    sc = bpy.context.scene
    dress_conveyor(out)
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True
    sun, cam = stage(sc)
    shoot(sc, sun, cam, out, (
        ("conveyor_three_quarter.png", (6.5, -17.5, 3.6), (0, -6.0, -1.0), 30, 30),
        ("conveyor_top.png", (2.8, 2.6, 2.0), (0, -3.5, -.2), 32, 200),
        ("conveyor_low.png", (-5.5, -2.0, -2.2), (0, -5.0, -1.4), 30, 120),
    ))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" not in argv:
        sys.exit("Preview only for now (not applied to Unity): -- --preview <folder>")
    preview(argv[argv.index("--preview") + 1], argv[argv.index("--shuttle") + 1] if "--shuttle" in argv else SHUTTLE)
