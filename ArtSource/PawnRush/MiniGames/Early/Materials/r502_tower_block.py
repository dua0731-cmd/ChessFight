# R502: walls and floors from the Tripo stone block, dressed in the Tripo castle tower's own stone material.
#
# The user chose (2026-11-07): shape from Source/R502_StoneBlock_Tripo.glb (puffy chipped bricks, no texture, no UVs),
# material from Source/R502_CastleTower_Tripo.glb (one 4K auto-UV atlas: base colour, roughness/metal, normal).
# The tower atlas is cut into islands, so it cannot repeat on a wall. Instead:
#
#   --extract  render the tower's front wall straight on (emission of its base colour = the painted albedo), find the
#              brick interiors, and collage feathered patches of them into a periodic 1024 px "tower stone" tile
#              (Textures/PR_Early_TowerStone_Surface.png). The mortar colour and the stone roughness are measured
#              from the tower too (Textures/PR_Early_TowerStone.json).
#   --block    scale the block to a 1 m wide module, decimate a game copy, unwrap it and bake from the full sculpt:
#              BaseColor (tower stone tile box-projected + tower mortar colour in the crevices), Normal, Roughness, AO.
#              Writes Textures/PR_Early_StoneBlock_*.png and StoneBlock_Module.blend.
#   --preview  render_tower_block.png (tower next to the dressed block) and render_wall_floor.png (a test wall and floor
#              of modules with a 1 m pawn stand-in, warm sun).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r502_tower_block.py -- --extract --block --preview
import bpy, bmesh, math, os, sys, json, random
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.normpath(os.path.join(HERE, "..", "Source"))
TEX = os.path.join(HERE, "Textures")
TOWER = os.path.join(SRC, "R502_CastleTower_Tripo.glb")
BLOCK = os.path.join(SRC, "R502_StoneBlock_Tripo.glb")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
os.makedirs(TEX, exist_ok=True)

MODULE_W = 1.0            # the block's 0.896 wide face becomes a 1 m module
TILE_M = .5               # the tower stone tile covers 0.5 m (about the tower's own texel density)
LOW_TRIS = 6000           # game copy of one module
BAKE_RES = 2048


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def pixels(img):
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def write_png(path, arr, cs):
    h, w = arr.shape[:2]
    if arr.ndim == 2:
        arr = np.repeat(arr[:, :, None], 3, 2)
    rgba = np.concatenate([np.clip(arr[:, :, :3], 0, 1), np.ones((h, w, 1), np.float32)], 2)
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    img.colorspace_settings.name = cs
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("wrote", path)


def roll_min(a):
    return np.minimum.reduce([a, np.roll(a, 1, 0), np.roll(a, -1, 0), np.roll(a, 1, 1), np.roll(a, -1, 1)])


def box_blur(a, r):
    out = a.copy()
    for ax in (0, 1):
        c = np.cumsum(np.pad(out, [(r + 1, r) if i == ax else (0, 0) for i in range(out.ndim)], mode="edge"), axis=ax)
        hi = np.take(c, np.arange(2 * r + 1, c.shape[ax]), axis=ax)
        lo = np.take(c, np.arange(0, c.shape[ax] - 2 * r - 1), axis=ax)
        out = (hi - lo) / (2 * r + 1)
    return out


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    obs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    return obs[0]


# ------------------------------------------------------------------ 1. the tower's stone, as a periodic tile
def extract():
    sc = reset()
    tower = import_glb(TOWER)
    mat = tower.data.materials[0]
    nt = mat.node_tree
    base = [n for n in nt.nodes if n.type == "TEX_IMAGE" and "basecolor" in n.image.name][0]
    rm = [n for n in nt.nodes if n.type == "TEX_IMAGE" and "_rm" in n.image.name][0].image
    out = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(base.outputs[0], em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 16
    sc.cycles.filter_width = 1.
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.
    sc.world = w
    cam = bpy.data.cameras.new("c")
    cam.type = "ORTHO"
    cam.ortho_scale = 1.
    co = bpy.data.objects.new("c", cam)
    co.location = (0, -3, .5)
    co.rotation_euler = (math.pi / 2, 0, 0)
    sc.collection.objects.link(co)
    sc.camera = co
    sc.render.resolution_x = sc.render.resolution_y = 2048
    path = os.path.join(bpy.app.tempdir, "tower_front.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    A = pixels(bpy.data.images.load(path))[:, :, :3]          # sRGB values as painted
    beige = (A[:, :, 0] > .45) & (A[:, :, 0] > A[:, :, 2] + .12) & (A[:, :, 1] > .3) & (A[:, :, 2] > .35 * A[:, :, 0])  # not the gold finial
    L = A.mean(2)
    d = L - box_blur(L, 12)
    mortar = beige & (d < -.025)
    inside = (beige & ~mortar).astype(np.float32)
    dist = np.zeros_like(inside)
    cur = inside.copy()
    for i in range(70):                                         # steps to the nearest mortar / outside
        cur = roll_min(cur)
        dist += cur
    stone_c = np.median(A[dist > 20], 0)
    mortar_c = np.median(A[beige & (d < -.06)], 0)
    # roughness: the tower's rm atlas (glTF: G = roughness, B = metallic) under its beige texels
    R = pixels(rm)
    Bc = pixels(base.image)[:, :, :3]
    bm = (Bc[:, :, 0] > .45) & (Bc[:, :, 0] > Bc[:, :, 2] + .12)
    rough = float(np.median(R[:, :, 1][bm]))
    # collage: feathered round patches of brick interior, composited over, wrapping at the tile edge
    N = 1024
    tile = np.ones((N, N, 3), np.float32) * stone_c
    rng = np.random.default_rng(502)
    cand = np.argwhere(dist >= 58)
    yy, xx = np.mgrid[-56:57, -56:57]
    rr = np.sqrt(xx * xx + yy * yy)
    for k in range(1400):
        r = rng.uniform(44, 58)
        cy, cx = cand[rng.integers(len(cand))]
        patch = A[cy - 56:cy + 57, cx - 56:cx + 57]
        a = np.clip((r - rr) / 26., 0, 1)[:, :, None]
        # keep the mottling inside a patch, drop most of the brick-to-brick brightness shift (no visible discs)
        pm = (patch * a).sum((0, 1)) / a.sum((0, 1))
        patch = patch - .75 * (pm - stone_c)
        ty, tx = rng.integers(N), rng.integers(N)
        iy = (np.arange(-56, 57) + ty) % N
        ix = (np.arange(-56, 57) + tx) % N
        sub = tile[np.ix_(iy, ix)]
        tile[np.ix_(iy, ix)] = sub * (1 - a) + patch * a
    # keep the tower's own contrast: match per-channel mean/std of the interiors
    src = A[dist > 20]
    for c in range(3):
        t = tile[:, :, c]
        tile[:, :, c] = (t - t.mean()) / max(t.std(), 1e-4) * src[:, c].std() + src[:, c].mean()
    write_png(os.path.join(TEX, "PR_Early_TowerStone_Surface.png"), tile, "sRGB")
    write_png(os.path.join(HERE, "_tower_front_mask.png"), np.stack([mortar, dist > 20, beige], 2).astype(np.float32), "Non-Color")
    info = {"stone_srgb": [round(float(x), 4) for x in stone_c], "mortar_srgb": [round(float(x), 4) for x in mortar_c],
            "stone_std": [round(float(x), 4) for x in src.std(0)], "roughness": round(rough, 3),
            "tile_metres": TILE_M, "source": os.path.basename(TOWER)}
    with open(os.path.join(TEX, "PR_Early_TowerStone.json"), "w") as f:
        json.dump(info, f, indent=1)
    print("TOWER", info)


def srgb_to_lin(c):
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


# ------------------------------------------------------------------ 2. the block module, dressed and baked
def stone_material(info):
    m = bpy.data.materials.new("PR_Early_StoneBlock_hi")
    nt = m.node_tree
    nt.nodes.clear()
    L = nt.links.new
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1 / TILE_M,) * 3
    L(tc.outputs["Object"], mp.inputs["Vector"])
    tx = nt.nodes.new("ShaderNodeTexImage")
    tx.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_TowerStone_Surface.png"))
    tx.projection = "BOX"
    tx.projection_blend = .25
    L(mp.outputs[0], tx.inputs["Vector"])
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 24
    ao.inputs["Distance"].default_value = .035
    # crevices take the tower's mortar colour; worn rims lighten a touch (pointiness), as the tower's painted edges do
    crev = nt.nodes.new("ShaderNodeMapRange")
    L(ao.outputs["AO"], crev.inputs["Value"])
    crev.inputs["From Min"].default_value, crev.inputs["From Max"].default_value = .35, .85
    crev.inputs["To Min"].default_value, crev.inputs["To Max"].default_value = 1., 0.
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    s = {i.identifier: i for i in mix.inputs}
    L(crev.outputs["Result"], s["Factor_Float"])
    L(tx.outputs["Color"], s["A_Color"])
    s["B_Color"].default_value = srgb_to_lin(info["mortar_srgb"]) + (1.,)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    rim = nt.nodes.new("ShaderNodeMapRange")
    L(geo.outputs["Pointiness"], rim.inputs["Value"])
    rim.inputs["From Min"].default_value, rim.inputs["From Max"].default_value = .52, .62
    rim.inputs["To Min"].default_value, rim.inputs["To Max"].default_value = 1., 1.08
    sc_ = nt.nodes.new("ShaderNodeVectorMath")
    sc_.operation = "SCALE"
    L([o for o in mix.outputs if o.identifier == "Result_Color"][0], sc_.inputs[0])
    L(rim.outputs["Result"], sc_.inputs["Scale"])
    rough = nt.nodes.new("ShaderNodeMapRange")
    L(ao.outputs["AO"], rough.inputs["Value"])
    rough.inputs["To Min"].default_value = min(1., info["roughness"] + .08)
    rough.inputs["To Max"].default_value = info["roughness"]
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    L(sc_.outputs[0], bs.inputs["Base Color"])
    L(rough.outputs["Result"], bs.inputs["Roughness"])
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    L(bs.outputs[0], out.inputs["Surface"])
    return m


def tri_count(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    me.calc_loop_triangles()
    n = len(me.loop_triangles)
    ob.evaluated_get(dg).to_mesh_clear()
    return n


MORTAR_FILL = .4          # R502 user: "the gaps between bricks are a bit open; fill them a little, not much"


def mortar_core(hi):
    """A mortar box inside the bricks whose faces sit part-way up the joints: it fills MORTAR_FILL of the joint depth.
    The joint depth is measured by casting rays at the front face (bricks at the median hit, joint floors at the
    deepest hits). The same inset is used on all six sides, so module-to-module joints fill the same amount."""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(hi, dg)
    vs = [v.co for v in hi.data.vertices]
    lo_ = Vector([min(v[i] for v in vs) for i in range(3)])
    hi_ = Vector([max(v[i] for v in vs) for i in range(3)])
    depths = []
    for x in np.linspace(lo_.x + .03, hi_.x - .03, 140):
        for z in np.linspace(lo_.z + .03, hi_.z - .03, 150):
            loc, n, i, d = bvh.ray_cast(Vector((x, lo_.y - 1., z)), Vector((0, 1, 0)))
            if loc is not None:
                depths.append(loc.y - lo_.y)
    depths = np.array(depths)
    face, floor_ = np.percentile(depths, 50), np.percentile(depths, 99.5)
    inset = face + (floor_ - face) * (1 - MORTAR_FILL)
    print("JOINTS face %.4f floor %.4f -> mortar inset %.4f" % (face, floor_, inset))
    size = (hi_ - lo_) - Vector((2 * inset,) * 3)
    me = bpy.data.meshes.new("mortar")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=8, use_grid_fill=True)
    for v in bm.verts:
        v.co = Vector((v.co.x * size.x, v.co.y * size.y, v.co.z * size.z)) + (lo_ + hi_) / 2
    bm.to_mesh(me)
    bm.free()
    me.materials.append(hi.data.materials[0])
    ob = bpy.data.objects.new("StoneBlock_mortar", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def block():
    info = json.load(open(os.path.join(TEX, "PR_Early_TowerStone.json")))
    sc = reset()
    hi = import_glb(BLOCK)
    hi.name = "StoneBlock_hi"
    bpy.context.view_layer.objects.active = hi
    for o in bpy.context.selected_objects:
        o.select_set(False)
    hi.select_set(True)
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    s = MODULE_W / hi.dimensions.x
    hi.scale = [v * s for v in hi.scale]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # sit the module on z = 0 with its front face on y = 0 (wall face), centred in x
    lo_ = [min(v.co[i] for v in hi.data.vertices) for i in range(3)]
    hi_ = [max(v.co[i] for v in hi.data.vertices) for i in range(3)]
    shift = Vector((-(lo_[0] + hi_[0]) / 2, -lo_[1], -lo_[2]))
    hi.data.transform(__import__("mathutils").Matrix.Translation(shift))
    hi.data.materials.clear()
    hi.data.materials.append(stone_material(info))
    bpy.ops.object.shade_smooth()
    print("MODULE dims", tuple(round(d, 3) for d in hi.dimensions), "hi tris", tri_count(hi))
    # game copy
    core = mortar_core(hi)
    lo = hi.copy()
    lo.data = hi.data.copy()
    lo.name = "PR_Early_StoneBlock"
    sc.collection.objects.link(lo)
    c2 = core.copy()
    c2.data = core.data.copy()
    sc.collection.objects.link(c2)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    lo.select_set(True)
    c2.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.join()
    dec = lo.modifiers.new("dec", "DECIMATE")
    dec.ratio = LOW_TRIS / tri_count(lo)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.modifier_apply(modifier="dec")
    print("low tris", tri_count(lo))
    for o in bpy.context.selected_objects:
        o.select_set(False)
    lo.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    lo.data.materials.clear()
    bake_mat = bpy.data.materials.new("PR_Early_StoneBlock")
    lo.data.materials.append(bake_mat)
    nt = bake_mat.node_tree
    img_node = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = img_node
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 64
    bk = sc.render.bake
    bk.use_selected_to_active = True
    bk.cage_extrusion = .02
    bk.max_ray_distance = .06
    bk.margin = 8
    hi.select_set(True)
    core.select_set(True)
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    out = {}
    for kind, cs, extra in (("BaseColor", "sRGB", {"pass_filter": {"COLOR"}}), ("Normal", "Non-Color", {}),
                            ("Roughness", "Non-Color", {}), ("AO", "Non-Color", {})):
        img = bpy.data.images.new(kind, BAKE_RES, BAKE_RES, alpha=False, float_buffer=kind == "Normal")
        img.colorspace_settings.name = cs
        img_node.image = img
        btype = {"BaseColor": "DIFFUSE", "Normal": "NORMAL", "Roughness": "ROUGHNESS", "AO": "AO"}[kind]
        bpy.ops.object.bake(type=btype, **extra)
        p = os.path.join(TEX, f"PR_Early_StoneBlock_{kind}.png")
        if kind == "Normal":
            write_png(p, pixels(img)[:, :, :3], "Non-Color")
        else:
            img.filepath_raw = p
            img.file_format = "PNG"
            img.save()
            print("wrote", p)
        out[kind] = p
    # final game material on the low copy, so the .blend opens ready
    nt.nodes.clear()
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(bs.outputs[0], o_.inputs["Surface"])

    def t(kind, cs):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(out[kind])
        n.image.colorspace_settings.name = cs
        return n.outputs["Color"]
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(t("Normal", "Non-Color"), nm.inputs["Color"])
    nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    nt.links.new(t("BaseColor", "sRGB"), bs.inputs["Base Color"])
    nt.links.new(t("Roughness", "Non-Color"), bs.inputs["Roughness"])
    for o in (hi, core):
        o.hide_render = True
        o.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "StoneBlock_Module.blend"))


# ------------------------------------------------------------------ 3. previews
def light_rig(sc, target, dist):
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 4.
    sun.angle = math.radians(4)
    sun.color = (1., .93, .82)
    so = bpy.data.objects.new("sun", sun)
    so.rotation_euler = (math.radians(48), 0, math.radians(-38))
    sc.collection.objects.link(so)
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    sky = w.node_tree.nodes["Background"]
    sky.inputs["Color"].default_value = (.62, .74, .92, 1)
    sky.inputs["Strength"].default_value = .9
    sc.world = w
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 128
    sc.cycles.use_denoising = True
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Base Contrast"
    except TypeError:
        pass


def camera(sc, loc, target, lens=50):
    cam = bpy.data.cameras.new("cam")
    cam.lens = lens
    co = bpy.data.objects.new("cam", cam)
    co.location = loc
    co.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(co)
    sc.camera = co


def preview():
    sc = reset()
    with bpy.data.libraries.load(os.path.join(HERE, "StoneBlock_Module.blend")) as (src, dst):
        dst.objects = ["PR_Early_StoneBlock"]
    mod = dst.objects[0]
    me = mod.data
    # A. tower vs one module, same scale
    tower = import_glb(TOWER)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    tower.select_set(True)
    bpy.context.view_layer.objects.active = tower
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    k = MODULE_W / .896
    tower.scale = [v * k for v in tower.scale]
    tower.location = (-.75, .3, 0)
    b = bpy.data.objects.new("m", me)
    b.location = (.55, 0, 0)
    sc.collection.objects.link(b)
    light_rig(sc, (0, 0, .5), 4)
    camera(sc, (1.4, -3.6, 1.6), (-.1, .2, .55), 55)
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1000
    sc.render.filepath = os.path.join(HERE, "render_tower_block.png")
    bpy.ops.render.render(write_still=True)
    # B. test wall (two courses) + floor (modules face up) + 1 m pawn stand-in
    for o in list(sc.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o)
    rng = random.Random(502)
    H = max(v.co.z for v in me.vertices) - min(v.co.z for v in me.vertices)
    D = max(v.co.y for v in me.vertices) - min(v.co.y for v in me.vertices)
    for row in range(2):
        for i in range(7):
            o = bpy.data.objects.new("w", me)
            flip = rng.random() < .5           # half the modules turned round: the pattern does not march
            o.location = (-3 + i, 2. + (D if flip else 0), row * H)
            o.rotation_euler = (0, 0, math.pi if flip else 0)
            sc.collection.objects.link(o)
    for j in range(4):
        for i in range(7):
            o = bpy.data.objects.new("f", me)
            o.rotation_euler = (-math.pi / 2, 0, 0)      # front face up at z = 0, body below
            o.location = (-3 + i, 2. - (j + 1) * H, 0.)
            sc.collection.objects.link(o)
    pawn = bpy.data.meshes.new("pawn")
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=.28, radius2=.16, depth=.7)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, .35))
    s2 = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.17)
    bmesh.ops.translate(bm, verts=s2["verts"], vec=(0, 0, .83))
    bm.to_mesh(pawn)
    bm.free()
    pm = bpy.data.materials.new("pawn")
    pm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.9, .9, .92, 1)
    pawn.materials.append(pm)
    for p in pawn.polygons:
        p.use_smooth = True
    po = bpy.data.objects.new("pawn", pawn)
    po.location = (.5, .8, 0)
    sc.collection.objects.link(po)
    camera(sc, (3.2, -4.6, 2.6), (0, 1.4, .7), 32)
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.render.filepath = os.path.join(HERE, "render_wall_floor.png")
    bpy.ops.render.render(write_still=True)


if "--extract" in ARGS:
    extract()
if "--block" in ARGS:
    block()
if "--preview" in ARGS:
    preview()
