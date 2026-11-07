# R506 phase-1 material swatches for A Ceremonial Chess Court (the painted stone chessboard and its forecourt).
#
# Same method as r502_material_swatches.py (phase 1) and Late/Materials/r504_material_swatches.py: build a real
# high-poly sample of each surface, render it straight down into tileable PBR maps, then lay the maps out next to the
# swatch row of the R506 concept sheet (Docs/PawnRush/ConceptArt/R506_CeremonialCourt/A_03_props_materials.png).
#
#   PR_A_Slate            2 m tile  BaseColor · Normal · Roughness · AO · Height   unpainted square, cleft slate (game #4A5060)
#   PR_A_IvoryLimestone   2 m tile  BaseColor · Normal · Roughness · AO · Height   white team's painted square (game #F3EADB)
#   PR_A_Basalt           2 m tile  BaseColor · Normal · Roughness · AO · Height   black team's painted square (game #231E1B)
#   PR_A_CourtPaving      2 m tile  BaseColor · Normal · Roughness · AO · Height   cream forecourt flagstones around the board
#   PR_Early_RoofSlate    1 m tile  BaseColor · Normal · Roughness · AO · Height   grey slate shingles (any phase-1 roof)
#
# Reused, not baked here: cream stone block (PR_Early_StoneBrick) and oak (PR_Early_WoodPlank) from the R502 script,
# matte brass (PR_Late_Brass) from the R504 script. The gold joint line stays the game's flat functional gold
# (#E2B23C, emission 0.4 in phase 1) and is only shown in the preview.
# Tiling: every sample object has linked copies one period away on all eight sides, so geometry, AO and the pixel filter
# wrap. Normal maps are OpenGL style (green = +V). Base colour holds no light or shadow (R499 4.3).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r506_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import bpy, bmesh, math, os, sys, random, tempfile
import numpy as np
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
TEX = os.path.join(HERE, "Textures")
LATE_TEX = os.path.normpath(os.path.join(HERE, "..", "..", "Late", "Materials", "Textures"))
WORK = os.path.join(tempfile.gettempdir(), "r506_swatch_work")
CONCEPT = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R506_CeremonialCourt", "A_03_props_materials.png")
FONT = "C:/Windows/Fonts/malgun.ttf"

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048
ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None
os.makedirs(TEX, exist_ok=True)
os.makedirs(WORK, exist_ok=True)

# continuous sheets: blend the wrap edge with a half-tile roll (see heal_band)
HEAL = {"PR_A_Slate": (.12, .12), "PR_A_IvoryLimestone": (.12, .12), "PR_A_Basalt": (.12, .12)}

# ------------------------------------------------------------------ small helpers (copied from r502 so the two stay independent)
def lin(h):
    c = [int(h[i:i + 2], 16) / 255. for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1.,)


def to_srgb(a):
    a = np.clip(a, 0., 1.)
    return np.where(a <= .0031308, a * 12.92, 1.055 * np.power(a, 1 / 2.4) - .055)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def node(nt, kind, x=0, y=0, **inp):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in inp.items():
        n.inputs[k].default_value = v
    return n


def link(nt, a, b):
    nt.links.new(a, b)


def ramp(nt, fac, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    for i, (p, c) in enumerate(stops):
        e = els[0] if i == 0 else els.new(p)
        e.position = p
        e.color = lin(c) if isinstance(c, str) else c
    link(nt, fac, r.inputs["Fac"])
    return r.outputs["Color"]


def mixc(nt, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    s = {i.identifier: i for i in m.inputs}
    for key, val in (("A_Color", a), ("B_Color", b)):
        if isinstance(val, tuple):
            s[key].default_value = val
        else:
            link(nt, val, s[key])
    if isinstance(fac, float):
        s["Factor_Float"].default_value = fac
    else:
        link(nt, fac, s["Factor_Float"])
    return [o for o in m.outputs if o.identifier == "Result_Color"][0]


def math_(nt, op, a, b=None, c=None):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    for i, v in enumerate((a, b, c)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            m.inputs[i].default_value = v
        else:
            link(nt, v, m.inputs[i])
    return m.outputs[0]


def vscale(nt, col, s):
    m = nt.nodes.new("ShaderNodeVectorMath")
    m.operation = "SCALE"
    link(nt, col, m.inputs[0])
    if isinstance(s, (int, float)):
        m.inputs["Scale"].default_value = s
    else:
        link(nt, s, m.inputs["Scale"])
    return m.outputs[0]


def vmul(nt, a, b):
    m = nt.nodes.new("ShaderNodeVectorMath")
    m.operation = "MULTIPLY"
    link(nt, a, m.inputs[0])
    link(nt, b, m.inputs[1])
    return m.outputs[0]


def maprange(nt, v, a0, a1, b0, b1):
    m = nt.nodes.new("ShaderNodeMapRange")
    link(nt, v, m.inputs["Value"])
    m.inputs["From Min"].default_value, m.inputs["From Max"].default_value = a0, a1
    m.inputs["To Min"].default_value, m.inputs["To Max"].default_value = b0, b1
    m.clamp = True
    return m.outputs["Result"]


def noise_tex(nt, vec, scale, detail=4., rough=.55, dist=0., w=None):
    n = nt.nodes.new("ShaderNodeTexNoise")
    if w is not None:
        n.noise_dimensions = "4D"
        n.inputs["W"].default_value = w
    link(nt, vec, n.inputs["Vector"])
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.inputs["Distortion"].default_value = dist
    return n.outputs["Fac"]


def tnoise(nt, c, P, fx, fy, detail=4., rough=.55, dist=0., seed=0.):
    """Noise that repeats every P metres in object X and Y: the plane is wrapped onto a torus in 4D noise space
    (cos/sin of X -> vector x,y; cos/sin of Y -> vector z and W). fx/fy = noise features per tile along X/Y."""
    sx = nt.nodes.new("ShaderNodeSeparateXYZ")
    link(nt, c["obj"], sx.inputs[0])
    k = 2 * math.pi / P
    ax = math_(nt, "MULTIPLY", sx.outputs["X"], k)
    ay = math_(nt, "MULTIPLY", sx.outputs["Y"], k)
    rx, ry = fx / (2 * math.pi), fy / (2 * math.pi)
    v = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY_ADD", math_(nt, "COSINE", ax), rx, seed), v.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ax), rx), v.inputs["Y"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "COSINE", ay), ry), v.inputs["Z"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    link(nt, v.outputs[0], n.inputs["Vector"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ay), ry), n.inputs["W"])
    n.inputs["Scale"].default_value = 1.
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.inputs["Distortion"].default_value = dist
    return n.outputs["Fac"]


def psines(p, P, terms):
    """Periodic height (period P in X and Y) from (kx, ky, amplitude, phase) sine terms."""
    return sum(a * math.sin(2 * math.pi * (kx * p.x + ky * p.y) / P + ph) for kx, ky, a, ph in terms)


def sine_terms(seed, n, kmax, amp):
    rng = random.Random(seed)
    out = []
    for _ in range(n):
        kx, ky = rng.randint(-kmax, kmax), rng.randint(-kmax, kmax)
        if kx == 0 and ky == 0:
            kx = 1
        out.append((kx, ky, amp * rng.uniform(.4, 1.) / math.hypot(kx, ky) ** .5, rng.uniform(0, 6.283)))
    return out


def mapping(nt, vec, scale=(1., 1., 1.), loc=None):
    m = nt.nodes.new("ShaderNodeMapping")
    link(nt, vec, m.inputs["Vector"])
    m.inputs["Scale"].default_value = scale
    if loc is not None:
        link(nt, loc, m.inputs["Location"])
    return m.outputs[0]


def offset_by_tint(nt, c, kx=13.7, kz=7.3):
    off = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY", c["tint"], kx), off.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", c["tint"], kz), off.inputs["Z"])
    return off.outputs[0]


def wave(nt, vec, scale, dist, detail, dscale=1., direction="Y", profile="SIN"):
    w = nt.nodes.new("ShaderNodeTexWave")
    w.wave_type = "BANDS"
    w.bands_direction = direction
    w.wave_profile = profile
    link(nt, vec, w.inputs["Vector"])
    w.inputs["Scale"].default_value = scale
    w.inputs["Distortion"].default_value = dist
    w.inputs["Detail"].default_value = detail
    w.inputs["Detail Scale"].default_value = dscale
    return w.outputs["Fac"]


# ------------------------------------------------------------------ bake materials (emission only; a pass rewires what it emits)
BAKE_MATS = []


def bake_material(name, albedo, rough, metal=0., bump=None, bump_strength=.3, bump_dist=.003, ao_dist=.08):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    tc = node(nt, "ShaderNodeTexCoord")
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_type = "OBJECT"
    attr.attribute_name = "tint"
    ctx = {"obj": tc.outputs["Object"], "tint": attr.outputs["Fac"]}

    def sock(v, kind):
        if callable(v):
            return v(nt, ctx)
        if kind == "color":
            r = nt.nodes.new("ShaderNodeRGB")
            r.outputs[0].default_value = lin(v) if isinstance(v, str) else v
            return r.outputs[0]
        r = nt.nodes.new("ShaderNodeValue")
        r.outputs[0].default_value = v
        return r.outputs[0]

    s = {"albedo": sock(albedo, "color"), "rough": sock(rough, "f"), "metal": sock(metal, "f")}
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    bn = nt.nodes.new("ShaderNodeBump")
    bn.inputs["Strength"].default_value = bump_strength
    bn.inputs["Distance"].default_value = bump_dist
    link(nt, geo.outputs["Normal"], bn.inputs["Normal"])
    if bump is not None:
        link(nt, bump(nt, ctx), bn.inputs["Height"])
    enc = nt.nodes.new("ShaderNodeVectorMath")
    enc.operation = "MULTIPLY_ADD"
    link(nt, bn.outputs["Normal"], enc.inputs[0])
    enc.inputs[1].default_value = (.5, .5, .5)
    enc.inputs[2].default_value = (.5, .5, .5)
    s["normal"] = enc.outputs[0]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    link(nt, geo.outputs["Position"], sep.inputs[0])
    s["height"] = sep.outputs["Z"]
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 32
    ao.inputs["Distance"].default_value = ao_dist
    link(nt, bn.outputs["Normal"], ao.inputs["Normal"])
    s["ao"] = ao.outputs["AO"]
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Strength"].default_value = 1.
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    link(nt, em.outputs[0], out.inputs["Surface"])
    BAKE_MATS.append((m, s, em))
    return m


def wire(pass_):
    for m, s, em in BAKE_MATS:
        nt = m.node_tree
        for l in list(em.inputs["Color"].links):
            nt.links.remove(l)
        link(nt, s[pass_], em.inputs["Color"])


# ------------------------------------------------------------------ geometry
def eval_mesh(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(old)
    return me


def box(name, sx, sy, sz, cuts=0, bevel=.02, seg=3, subsurf=0, angle=30):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co.x *= sx
        v.co.y *= sy
        v.co.z *= sz
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if bevel:
        b = ob.modifiers.new("bevel", "BEVEL")
        b.width = bevel
        b.segments = seg
        b.limit_method = "ANGLE"
        b.angle_limit = math.radians(angle)
    if subsurf:
        s = ob.modifiers.new("sub", "SUBSURF")
        s.levels = s.render_levels = subsurf
    return eval_mesh(ob)


def deform(me, fn):
    n = len(me.vertices)
    co = np.empty(n * 3, np.float32)
    nr = np.empty(n * 3, np.float32)
    me.vertices.foreach_get("co", co)
    me.vertices.foreach_get("normal", nr)
    co = co.reshape(-1, 3)
    nr = nr.reshape(-1, 3)
    for i in range(n):
        co[i] = fn(Vector(co[i]), Vector(nr[i]))
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def smooth(me):
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.update()


def place(me, mat, loc, P, rot=(0., 0., 0.), tint=0.):
    """One sample object plus its eight neighbours one period away (the tile wraps)."""
    if mat.name not in [m.name for m in me.materials if m]:
        me.materials.append(mat)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            ob = bpy.data.objects.new(me.name, me)
            ob.location = (loc[0] + dx * P[0], loc[1] + dy * P[1], loc[2])
            ob.rotation_euler = rot
            ob["tint"] = tint
            bpy.context.scene.collection.objects.link(ob)


def floor(mat, P, z):
    me = bpy.data.meshes.new("floor")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new("floor", me)
    ob.scale = (P[0] * 2, P[1] * 2, 1)
    ob.location = (P[0] / 2, P[1] / 2, z)
    ob["tint"] = .5
    bpy.context.scene.collection.objects.link(ob)


def wrap_noise(p, P, f):
    """Seamless noise on a P-periodic plane: blend of four shifted samples (bilinear in the tile position)."""
    u, v = (p.x % P) / P, (p.y % P) / P
    a = noise.noise(Vector((p.x * f, p.y * f, p.z * f)))
    b = noise.noise(Vector(((p.x - P) * f, p.y * f, p.z * f)))
    c = noise.noise(Vector((p.x * f, (p.y - P) * f, p.z * f)))
    d = noise.noise(Vector(((p.x - P) * f, (p.y - P) * f, p.z * f)))
    return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v


# ------------------------------------------------------------------ continuous surfaces (velvet, ermine, brass): one P x P
# plane per tile copy. All shader noise is tnoise and all displacement is psines, so both repeat exactly every P.
def sheet(name, mat, P, cuts, height_fn=None, z=0., tint=.5):
    # a flat grid, not a box: a smooth-shaded box averages the side faces into the top normals and bakes a dome
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=max(1, cuts), y_segments=max(1, cuts), size=P / 2)
    bm.to_mesh(me)
    bm.free()
    if height_fn is not None:
        def fn(p, n):
            if n.z < .3:
                return p
            w = Vector((p.x + P / 2, p.y + P / 2, 0.))      # tile space, so copies line up
            return p + Vector((0., 0., height_fn(w)))
        deform(me, fn)
    smooth(me)
    place(me, mat, (P / 2, P / 2, z), (P, P), tint=tint)


# ------------------------------------------------------------------ rendering the maps
def bake_scene(P, res):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.use_denoising = False
    sc.cycles.filter_width = 1.0
    sc.cycles.max_bounces = 1
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "OPEN_EXR"
    sc.render.image_settings.color_depth = "32"
    w = bpy.data.worlds.new("black")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.
    sc.world = w
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = P
    cam.clip_end = 50
    co = bpy.data.objects.new("cam", cam)
    co.location = (P / 2, P / 2, 5.)
    sc.collection.objects.link(co)
    sc.camera = co


def render_pass(name, pass_, samples):
    sc = bpy.context.scene
    wire(pass_)
    sc.cycles.samples = samples
    path = os.path.join(WORK, f"{name}_{pass_}.exr")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


def write_png(path, rgb, colorspace):
    h, w = rgb.shape[:2]
    if rgb.ndim == 2:
        rgb = np.repeat(rgb[:, :, None], 3, 2)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((h, w, 1), np.float32)], 2).astype(np.float32)
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("wrote", path)


def blur_wrap(a, n=1):
    for _ in range(n):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.
    return a


def heal_band(a, bx, by):
    """Continuous sheets: the bump/AO near the outer pixels is computed across two touching objects and comes out
    wrong, and door plank end bevels leave a groove at the X wrap. Classic offset blend: mix the map with a copy rolled
    by half a tile, the copy weighted 1 at the wrap edge and 0 from bx/by (fraction of the tile) inward, so the
    copy's own seam (now in the middle) never shows."""
    for axis, f in ((1, bx), (0, by)):
        if f <= 0:
            continue
        n = a.shape[axis]
        d = np.minimum(np.arange(n) + .5, n - np.arange(n) - .5) / n        # 0 at the wrap edge, .5 in the middle
        w = np.clip(1. - d / f, 0., 1.)
        w = w * w * (3 - 2 * w)
        shape = [1] * a.ndim
        shape[axis] = n
        w = w.reshape(shape)
        a = a * (1 - w) + np.roll(a, n // 2, axis) * w
    return a


MAPS = ("BaseColor", "Roughness", "Metallic", "Normal", "Height", "AO")


def heal_files(name):
    """Apply heal_band to maps already on disk (raw values in, raw values out)."""
    bx, by = HEAL[name]
    for s in MAPS:
        p = os.path.join(TEX, f"{name}_{s}.png")
        if not os.path.exists(p):
            continue
        im = bpy.data.images.load(p, check_existing=False)
        im.colorspace_settings.name = "Non-Color"
        w, h = im.size
        a = np.empty(w * h * 4, np.float32)
        im.pixels.foreach_get(a)
        bpy.data.images.remove(im)
        a = heal_band(a.reshape(h, w, 4)[:, :, :3].copy(), bx, by)
        if s == "Normal":
            n = a * 2 - 1
            n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
            a = n * .5 + .5
        write_png(p, a, "Non-Color")


def bake(name, build, res, metallic=False, height=True, ao=True):
    if ONLY and ONLY != name:
        return
    reset()
    BAKE_MATS.clear()
    P = build()
    bake_scene(P, res)
    out = lambda s: os.path.join(TEX, f"{name}_{s}.png")
    write_png(out("BaseColor"), to_srgb(render_pass(name, "albedo", 16)[:, :, :3]), "sRGB")
    write_png(out("Roughness"), render_pass(name, "rough", 16)[:, :, 0], "Non-Color")
    if metallic:
        write_png(out("Metallic"), render_pass(name, "metal", 16)[:, :, 0], "Non-Color")
    n = render_pass(name, "normal", 16)[:, :, :3] * 2 - 1
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    write_png(out("Normal"), n * .5 + .5, "Non-Color")
    if height:
        z = render_pass(name, "height", 16)[:, :, 0]
        lo, hi = np.percentile(z, .5), np.percentile(z, 99.5)
        write_png(out("Height"), (z - lo) / max(hi - lo, 1e-6), "Non-Color")
    if ao:
        write_png(out("AO"), blur_wrap(render_pass(name, "ao", 64)[:, :, 0], 1), "Non-Color")
    if name in HEAL:
        heal_files(name)


# ------------------------------------------------------------------ A board squares: three continuous stone sheets
# All shader noise is tnoise and all displacement is psines, so each sheet repeats exactly every P.
def stepped(nt, fac, n, lo=.32, hi=.68):
    """Terraces: a constant colour ramp turns smooth noise into n flat levels with sharp edges (cleft slate layers)."""
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.color_ramp.interpolation = "CONSTANT"
    els = r.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    for i in range(n):
        e = els[0] if i == 0 else els.new(0.)
        e.position = 0. if i == 0 else lo + (hi - lo) * i / n
        v = i / (n - 1)
        e.color = (v, v, v, 1.)
    link(nt, fac, r.inputs["Fac"])
    return r.outputs["Color"]


def slate_layers(nt, c, P):
    return stepped(nt, tnoise(nt, c, P, 18., 7., 5., .6, 1.2, 3.), 7)


def slate_albedo(P):
    def f(nt, c):
        base = ramp(nt, tnoise(nt, c, P, 6., 6., 5., .6), [(.3, "#3B3F47"), (.5, "#4B4F59"), (.7, "#5D616B")])
        cleft = maprange(nt, slate_layers(nt, c, P), 0., 1., .86, 1.12)
        flake = maprange(nt, tnoise(nt, c, P, 10., 10., 4., .6, 0., 12.), .55, .62, 0., .35)
        speck = maprange(nt, tnoise(nt, c, P, 300., 300., 2., .5, 0., 9.), .62, .7, 0., .25)
        col = mixc(nt, flake, vscale(nt, base, cleft), lin("#6B6F78"))
        return mixc(nt, speck, col, lin("#6A6E76"))
    return f


def slate_bump(P):
    # cleft layers (stretched along X) plus fine grit
    return lambda nt, c: math_(nt, "MULTIPLY_ADD", tnoise(nt, c, P, 80., 80., 2., .5, 0., 6.), .25,
                               math_(nt, "ADD", math_(nt, "MULTIPLY", slate_layers(nt, c, P), .5),
                                     maprange(nt, tnoise(nt, c, P, 10., 10., 4., .6, 0., 12.), .55, .62, 0., .4)))


def build_slate(P=2.):
    mat = bake_material("PR_A_Slate", slate_albedo(P),
                        lambda nt, c: maprange(nt, tnoise(nt, c, P, 6., 6., 4.), .3, .7, .55, .75), 0., slate_bump(P),
                        .6, .012, .05)
    terms = sine_terms(5061, 14, 6, .002)
    sheet("slate", mat, P, 160, lambda w: psines(w, P, terms))
    return P


def ivory_albedo(P):
    def f(nt, c):
        base = ramp(nt, tnoise(nt, c, P, 5., 5., 5., .6), [(.3, "#E3D5BD"), (.5, "#EDE2CE"), (.7, "#F4EBDC")])
        vein = maprange(nt, math_(nt, "ABSOLUTE", math_(nt, "SUBTRACT", tnoise(nt, c, P, 6., 6., 6., .6, 3., 5.), .5)),
                        0., .03, .7, 0.)
        fine = maprange(nt, math_(nt, "ABSOLUTE", math_(nt, "SUBTRACT", tnoise(nt, c, P, 12., 12., 5., .6, 2.5, 13.), .5)),
                        0., .018, .45, 0.)
        pores = maprange(nt, tnoise(nt, c, P, 420., 420., 1., .5, 0., 7.), .64, .7, 0., .6)
        col = mixc(nt, fine, mixc(nt, vein, base, lin("#CDB894")), lin("#D8C6A6"))
        return mixc(nt, pores, col, lin("#BFAE92"))
    return f


def ivory_bump(P):
    return lambda nt, c: math_(nt, "MULTIPLY_ADD", maprange(nt, tnoise(nt, c, P, 420., 420., 1., .5, 0., 7.), .64, .7, 0., -1.),
                               .5, math_(nt, "MULTIPLY", tnoise(nt, c, P, 40., 40., 3., .5, 0., 2.), .3))


def build_ivory(P=2.):
    mat = bake_material("PR_A_IvoryLimestone", ivory_albedo(P),
                        lambda nt, c: maprange(nt, tnoise(nt, c, P, 5., 5., 4.), .3, .7, .55, .7), 0., ivory_bump(P),
                        .3, .002, .04)
    terms = sine_terms(5062, 10, 4, .0015)
    sheet("ivory", mat, P, 120, lambda w: psines(w, P, terms))
    return P


def basalt_albedo(P):
    def f(nt, c):
        base = ramp(nt, tnoise(nt, c, P, 6., 6., 5., .6), [(.3, "#1C1916"), (.5, "#27231F"), (.7, "#36312C")])
        grains = maprange(nt, tnoise(nt, c, P, 500., 500., 1., .5, 0., 4.), .6, .68, 0., .75)
        pits = maprange(nt, tnoise(nt, c, P, 120., 120., 1., .5, 0., 8.), .66, .72, 0., .9)
        return mixc(nt, pits, mixc(nt, grains, base, lin("#6A635A")), lin("#0F0D0C"))
    return f


def basalt_bump(P):
    return lambda nt, c: math_(nt, "MULTIPLY_ADD", maprange(nt, tnoise(nt, c, P, 120., 120., 1., .5, 0., 8.), .66, .72, 0., -1.),
                               .6, math_(nt, "MULTIPLY", tnoise(nt, c, P, 30., 30., 3., .5, 0., 1.), .25))


def build_basalt(P=2.):
    mat = bake_material("PR_A_Basalt", basalt_albedo(P),
                        lambda nt, c: maprange(nt, tnoise(nt, c, P, 5., 5., 4.), .3, .7, .4, .55), 0., basalt_bump(P),
                        .45, .002, .04)
    terms = sine_terms(5063, 10, 4, .0015)
    sheet("basalt", mat, P, 120, lambda w: psines(w, P, terms))
    return P


# ------------------------------------------------------------------ cream forecourt flagstones (rows of uneven slabs)
def paving_albedo(nt, c):
    o = mapping(nt, c["obj"], (1., 1., 1.), offset_by_tint(nt, c, 9.1, 4.7))
    base = ramp(nt, c["tint"], [(0., "#CDB89A"), (.5, "#DCC9AD"), (1., "#E8D9C2")])
    cloud = maprange(nt, noise_tex(nt, o, 3., 6., .6), .3, .7, .82, 1.1)
    speck = maprange(nt, noise_tex(nt, o, 160., 2., .5), .66, .72, 0., .35)
    return mixc(nt, speck, vscale(nt, base, cloud), lin("#B8A486"))


def paving_bump(nt, c):
    return math_(nt, "MULTIPLY_ADD", noise_tex(nt, c["obj"], 40., 3.), .3, noise_tex(nt, c["obj"], 6., 4.))


def build_paving(P=2.):
    random.seed(5064)
    mat = bake_material("PR_A_CourtPaving", paving_albedo,
                        lambda nt, c: maprange(nt, noise_tex(nt, c["obj"], 6., 4.), .3, .7, .62, .8), 0., paving_bump,
                        .25, .002, .05)
    grout = bake_material("grout", "#B3A38A", .95, 0., None, 0., .001, .05)
    floor(grout, (P, P), 0.)
    gap, t, y = .012, .06, 0.
    for row_h in (.55, .45, .6, .4):
        x = random.uniform(0, P)
        end = x + P
        while x < end - 1e-4:
            L = random.choice((.5, .65, .8, 1.))
            if end - x - L < .35:
                L = end - x
            me = box("flag", L - gap, row_h - gap, t, cuts=6, bevel=.015, seg=3)
            hx, hy = (L - gap) / 2, (row_h - gap) / 2
            sv = Vector((random.uniform(0, 99), random.uniform(0, 99), 0.))

            def fn(p, n, hx=hx, hy=hy, sv=sv):
                r = max(min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.))
                edge = max(0., (r - .9) / .1)
                dome = (1. - (p.x / hx) ** 2) * (1. - (p.y / hy) ** 2) * .002 if n.z > .5 else 0.
                return p + Vector((0., 0., dome)) + n * min(0., noise.noise(p * 9. + sv) + .2) * .006 * edge
            deform(me, fn)
            smooth(me)
            rot = (random.uniform(-.003, .003), random.uniform(-.003, .003), 0.)
            place(me, mat, (x + L / 2, y + row_h / 2, t / 2), (P, P), rot, random.random())
            x += L
        y += row_h
    return P


# ------------------------------------------------------------------ grey slate roof shingles (rows overlap downhill = -Y)
def roof_albedo(nt, c):
    o = mapping(nt, c["obj"], (1., 1., 1.), offset_by_tint(nt, c, 7.7, 3.1))
    base = ramp(nt, c["tint"], [(0., "#56585E"), (.5, "#63656B"), (1., "#727479")])
    cloud = maprange(nt, noise_tex(nt, o, 8., 5., .6), .3, .7, .88, 1.08)
    return vscale(nt, base, cloud)


def build_roof(P=1.):
    random.seed(5065)
    mat = bake_material("PR_Early_RoofSlate", roof_albedo,
                        lambda nt, c: maprange(nt, noise_tex(nt, c["obj"], 9., 4.), .3, .7, .62, .8), 0.,
                        lambda nt, c: noise_tex(nt, c["obj"], 30., 4.), .25, .002, .03)
    under = bake_material("under", "#2A2B2F", 1., 0., None, 0., .001, .03)
    floor(under, (P, P), -.03)
    rows, pitch, length, t = 4, P / 4, .32, .02
    tilt = -math.atan2(.022, length)              # down-slope (-Y) edge up, so each row lies on the one above it
    for r in range(rows):
        y = r * pitch + pitch / 2 - (length - pitch) / 2
        x = (r % 2) * .12
        end = x + P
        while x < end - 1e-4:
            w = random.uniform(.18, .28)
            if end - x - w < .12:
                w = end - x
            me = box("shingle", w - .008, length, t, cuts=4, bevel=.004, seg=2)
            hy = length / 2
            sv = Vector((random.uniform(0, 99), random.uniform(0, 99), 0.))

            def fn(p, n, hy=hy, sv=sv):
                low = max(0., (-p.y / hy - .8) / .2)         # chipped down-slope edge
                return p + n * min(0., noise.noise(p * 25. + sv) + .1) * .004 * low
            deform(me, fn)
            smooth(me)
            place(me, mat, (x + w / 2, y, .012 + r * .0005), (P, P), (tilt, 0., random.uniform(-.01, .01)), random.random())
            x += w
    return P


# ------------------------------------------------------------------ preview: swatches in the order of the R506 sheet's swatch row
# A_03_props_materials.png swatch row (fractions of the image): x 40..1500 / 1536, y 848..1012 / 1024. Nine cubes.
CROP = (40 / 1536, 1500 / 1536, 848 / 1024, 1012 / 1024)
ENTRIES = [("PR_Early_StoneBrick", "base", "크림 돌 벽돌 (R502, 2 m)"),
           ("PR_A_CourtPaving", "base", "광장 크림 포장 (2 m)"),
           ("PR_A_Slate", "base", "슬레이트 · 칠 전 칸 (2 m)"),
           ("PR_A_IvoryLimestone", "base", "상아 석회암 · 흰 팀 칸 (2 m)"),
           ("PR_A_Basalt", "base", "검은 현무암 · 흑 팀 칸 (2 m)"),
           ("JOINT", "joint", "금 줄눈 (게임 색)"),
           ("PR_Late_Brass", "metal", "무광 황동 (R504, 1 m)"),
           ("PR_Early_WoodPlank", "base", "참나무 (R502, 2 m)"),
           ("PR_Early_RoofSlate", "base", "회색 지붕 슬레이트 (1 m)")]
TILE_M = {"PR_Early_StoneBrick": 2., "PR_A_CourtPaving": 2., "PR_A_Slate": 2., "PR_A_IvoryLimestone": 2.,
          "PR_A_Basalt": 2., "PR_Late_Brass": 1., "PR_Early_WoodPlank": 2., "PR_Early_RoofSlate": 1.}


def tex_path(name, suffix):
    for d in (TEX, LATE_TEX):
        p = os.path.join(d, f"{name}_{suffix}.png")
        if os.path.exists(p):
            return p
    return None


def load_img(path, cs):
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = cs
    return img


def preview_material(name, kind, span):
    m = bpy.data.materials.new("prev " + name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial")
    bsdf = node(nt, "ShaderNodeBsdfPrincipled")
    link(nt, bsdf.outputs[0], out.inputs["Surface"])
    if kind == "gold":
        bsdf.inputs["Base Color"].default_value = lin("#E2B23C")
        bsdf.inputs["Metallic"].default_value = 1.
        bsdf.inputs["Roughness"].default_value = .25
        bsdf.inputs["Emission Color"].default_value = lin("#E2B23C")
        bsdf.inputs["Emission Strength"].default_value = .4
        return m
    uv = node(nt, "ShaderNodeUVMap")
    mp = node(nt, "ShaderNodeMapping")
    link(nt, uv.outputs[0], mp.inputs["Vector"])
    mp.inputs["Scale"].default_value = (span, span, 1.)

    def tex(suffix, cs):
        p = tex_path(name, suffix)
        if p is None:
            return None
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = load_img(p, cs)
        link(nt, mp.outputs[0], t.inputs["Vector"])
        return t.outputs["Color"]
    nm = nt.nodes.new("ShaderNodeNormalMap")
    link(nt, tex("Normal", "Non-Color"), nm.inputs["Color"])
    col = tex("BaseColor", "sRGB")
    ao = tex("AO", "Non-Color")
    if ao is not None:
        col = vscale(nt, col, math_(nt, "POWER", ao, .7))
    link(nt, col, bsdf.inputs["Base Color"])
    r = tex("Roughness", "Non-Color")
    if r is not None:
        link(nt, r, bsdf.inputs["Roughness"])
    if kind == "metal":
        link(nt, tex("Metallic", "Non-Color"), bsdf.inputs["Metallic"])
    link(nt, nm.outputs[0], bsdf.inputs["Normal"])
    return m


def plain(name, col, rough=.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(col)
    b.inputs["Roughness"].default_value = rough
    return m


def slab(mat, cx, cz, size, bevel, uvk, depth=.08, y=0.):
    me = box("slab", size, depth, size, cuts=0, bevel=bevel, seg=8, angle=30)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((p.x / size + .5) * uvk, (p.z / size + .5) * uvk)
    me.materials.append(mat)
    smooth(me)
    ob = bpy.data.objects.new("slab", me)
    ob.location = (cx, y, cz)
    bpy.context.scene.collection.objects.link(ob)


def joint_slab(cx, cz, size, bevel, span):
    """Slate slab with the game's gold joint (0.07 m wide at board scale) crossing it, like the sheet's swatch."""
    slab(preview_material("PR_A_Slate", "base", span), cx, cz, size, bevel, 1.)
    w = .07                                          # the game joint is 7 cm wide; the slab is shown at 1:1
    gold = preview_material("JOINT", "gold", 1.)
    for sx, sz in ((size, w), (w, size)):
        me = box("joint", sx, .02, sz, cuts=0, bevel=0.)
        me.materials.append(gold)
        ob = bpy.data.objects.new("joint", me)
        ob.location = (cx, -.045, cz)
        bpy.context.scene.collection.objects.link(ob)


def text(body, x, z, size=.12, col="#4A3C2A"):
    cu = bpy.data.curves.new("t", "FONT")
    cu.body = body
    cu.font = bpy.data.fonts.load(FONT, check_existing=True)
    cu.size = size
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = bpy.data.objects.new("t", cu)
    ob.location = (x, -.06, z)
    ob.rotation_euler = (math.pi / 2, 0, 0)
    ob.data.materials.append(plain("text", col, .8))
    bpy.context.scene.collection.objects.link(ob)


def preview():
    sc = reset()
    step, size = 1.25, 1.
    W = step * len(ENTRIES)
    H = 3.2
    z1, z2 = 2.15, .82
    text("R506 A 의장 광장 견본 · 1줄: 1 m 크기  ·  2줄: 2×2 반복(이음새 확인)", W / 2, 3.0, .13)
    for i, (name, kind, label) in enumerate(ENTRIES):
        x = step * (i + .5)
        if kind == "joint":
            joint_slab(x, z1, size, .07, size / 2.)
            text("텍스처 없음 · 슬레이트 위", x, z2, .085, "#8A7C6A")
        else:
            tile = TILE_M.get(name, 1.)
            slab(preview_material(name, kind, size / tile), x, z1, size, .07, 1.)
            slab(preview_material(name, kind, 2.), x, z2, size, .012, 1.)
            text("2×2 반복", x, z2 - .62, .09)
        text(label, x, z1 - .64, .085)
    bd = bpy.data.meshes.new("bd")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.)
    bm.to_mesh(bd)
    bm.free()
    bd.materials.append(plain("backdrop", "#F5EDE0", .95))
    bo = bpy.data.objects.new("bd", bd)
    bo.rotation_euler = (math.pi / 2, 0, 0)
    bo.location = (W / 2, .12, H / 2)
    sc.collection.objects.link(bo)
    # warm sunny key (phase 1 is outdoors at noon) plus a parchment fill, like the concept sheet
    la = bpy.data.lights.new("key", "AREA")
    la.energy = 1100
    la.size = 6.
    la.color = (1., .95, .86)
    lo = bpy.data.objects.new("key", la)
    lo.location = (W / 2 - 4, -6, H + 3.5)
    lo.rotation_euler = (Vector((W / 2, 0, H / 2)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = lin("#FFF4E6")
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
    sc.render.resolution_x = 1800
    sc.render.resolution_y = int(1800 * H / W)
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    path = os.path.join(HERE, "r506_swatch_render.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    compare(path)


def pixels(img):
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def compare(render_path):
    R = pixels(bpy.data.images.load(render_path, check_existing=False))
    C = pixels(bpy.data.images.load(CONCEPT, check_existing=False))
    h, w = C.shape[:2]
    x0, x1 = int(CROP[0] * w), int(CROP[1] * w)
    y0, y1 = h - int(CROP[3] * h), h - int(CROP[2] * h)     # rows are stored bottom-up
    crop = C[y0:y1, x0:x1].copy()
    ci = bpy.data.images.new("crop", crop.shape[1], crop.shape[0], alpha=True)
    ci.pixels.foreach_set(crop.ravel())
    W = R.shape[1]
    ci.scale(W, int(crop.shape[0] * W / crop.shape[1]))
    top = pixels(ci)
    bar = np.ones((6, W, 4), np.float32) * np.array([.4, .33, .25, 1.], np.float32)
    sheet_ = np.concatenate([R, bar, top], 0)
    out = bpy.data.images.new("compare", W, sheet_.shape[0], alpha=True)
    out.pixels.foreach_set(sheet_.ravel())
    out.filepath_raw = os.path.join(HERE, "r506_swatch_compare.png")
    out.file_format = "PNG"
    out.save()
    print("wrote", out.filepath_raw)


if "--bake" in ARGS:
    bake("PR_A_Slate", build_slate, RES)
    bake("PR_A_IvoryLimestone", build_ivory, RES)
    bake("PR_A_Basalt", build_basalt, RES)
    bake("PR_A_CourtPaving", build_paving, RES)
    bake("PR_Early_RoofSlate", build_roof, RES // 2)
if "--preview" in ARGS:
    preview()
