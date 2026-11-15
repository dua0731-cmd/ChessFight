# The low-poly pawn with its face as stickers: each eye and each blush is drawn in its own plane, tangent to the head
# at a point given by its angles on the head (azimuth from the front, positive towards the piece's left; elevation),
# and projected onto the head along that plane's normal. The eyes come from the expression atlas (eye_draw.py
# layout); four ways to put them on the head:
#   flat      - on the head's own facets (a copy of them, lifted 0.3 mm), facet shading
#   lens      - a smooth lens on the head's smooth surface (the ellipsoid through its vertices), domed in the middle
#   bevel     - flat, with the atlas' relief map as a bump (rounded edges)
#   lensbevel - the lens with the bump
# Views turn the piece, not the camera, so the light stays where the design sheet has it (upper left front).
# blender -b --factory-startup -P pawn_face.py -- <config.json>
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eye_draw as ed

CFG = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
OUT = CFG["out"]
os.makedirs(OUT, exist_ok=True)
TEAM = CFG.get("team", "white")
SKIN, HEADC, CHEEK = (0.945, 0.808, 0.702), (0.898, 0.753, 0.649), (0.91, 0.59, 0.53)
EYE, GLINT, HAND, FOOT = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
BODY = tuple(CFG.get("body", (0.97, 0.86, 0.73) if TEAM == "white" else (0.30, 0.30, 0.31)))
DARK = (0.035, 0.032, 0.032)


def near(c, ref, tol=0.04):
    return max(abs(c[i] - ref[i]) for i in range(3)) < tol


bpy.ops.wm.open_mainfile(filepath=CFG["blend"])
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
pawn = bpy.data.objects["Pawn"]
arm = pawn.parent
me = pawn.data
attr = me.color_attributes["Col"]

# ---- the old face out (its eyes, glints and blush discs are separate pieces), the body recoloured
dead = set()
for poly in me.polygons:
    c = attr.data[poly.loop_start].color_srgb[:3]
    if poly.center.z < 0.6:
        continue
    # the head is SKIN; the old eyes, glints and blush discs (pink, with HEADC rims) are separate pieces.
    # The eye colour is within 0.04 of the foot colour, so test it first.
    if near(c, EYE, 0.03) or near(c, GLINT, 0.03) or not (near(c, SKIN) or near(c, HAND) or near(c, FOOT)):
        dead.add(poly.index)
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in dead], context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(me)
bm.free()
attr = me.color_attributes["Col"]
for d in attr.data:
    c = d.color_srgb[:3]
    d.color_srgb = (*(DARK if (near(c, HAND) or near(c, FOOT)) else BODY), 1.0)
print("old face faces removed:", len(dead))

if not CFG.get("limbs", False):
    gidx = {g.index for g in pawn.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Foot"))}
    limb = [v.index for v in me.vertices if sum(g.weight for g in v.groups if g.group in gidx) >= 0.5]
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in limb], context="VERTS")
    bm.to_mesh(me)
    bm.free()

# ---- the head: the ellipsoid through its vertices (axis-aligned, centre on the axis)
NECK_Z = 0.64
hv = [v.co.copy() for v in me.vertices if v.co.z > NECK_Z]
A_ = np.array([[p.x * p.x, p.y * p.y, p.z * p.z, p.z] for p in hv])
coef = np.linalg.lstsq(A_, np.ones(len(hv)), rcond=None)[0]
ka, kb, kc, kd = coef
ZC = -kd / (2 * kc)
rhs = 1 + kc * ZC * ZC
EA, EB, EC = math.sqrt(rhs / ka), math.sqrt(rhs / kb), math.sqrt(rhs / kc)
HC = Vector((0, 0, ZC))
print("HEAD ellipsoid centre z %.4f  semi-axes x %.4f y %.4f z %.4f" % (ZC, EA, EB, EC))


def ell_normal(p):
    q = p - HC
    return Vector((q.x / EA ** 2, q.y / EB ** 2, q.z / EC ** 2)).normalized()


def ell_hit(o, d):
    """Where the line o + t d meets the ellipsoid (the larger t); None if it misses."""
    q = o - HC
    sx, sy, sz = 1 / EA ** 2, 1 / EB ** 2, 1 / EC ** 2
    a = d.x * d.x * sx + d.y * d.y * sy + d.z * d.z * sz
    b = 2 * (q.x * d.x * sx + q.y * d.y * sy + q.z * d.z * sz)
    c = q.x * q.x * sx + q.y * q.y * sy + q.z * q.z * sz - 1
    disc = b * b - 4 * a * c
    if disc < 0:
        return None
    return o + d * ((-b + math.sqrt(disc)) / (2 * a))


head_faces = [p for p in me.polygons if all(me.vertices[i].co.z > NECK_Z - 0.03 for i in p.vertices)]
tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in head_faces])


def frame(az, el, roll=0.0, front=0.0):
    """The sticker frame at (azimuth, elevation) in degrees: centre on the faceted head, normal of the smooth head,
    u horizontal (towards +x seen from the front), v up; roll > 0 leans the top towards the face's middle.
    front (0..1) turns the projection from the head's normal (a sticker) towards straight from the front (0 = sticker,
    1 = projected from the front: the front view shows the drawing exactly)."""
    a, e = math.radians(az), math.radians(el)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    hit = tree.ray_cast(HC + d * 0.6, -d)
    p = hit[0]
    n = ell_normal(ell_hit(HC, d))
    if front:
        n = (n * (1 - front) + Vector((0, -1, 0)) * front).normalized()
    eu = Vector((-n.y, n.x, 0)).normalized()
    ev = n.cross(eu).normalized()
    if roll:
        r = math.radians(roll) * (1 if az < 0 else -1)
        eu, ev = eu * math.cos(r) - ev * math.sin(r), eu * math.sin(r) + ev * math.cos(r)
    return dict(p=p, n=n, eu=eu, ev=ev, az=az, el=el)


# ---- the face's numbers
EYE_CFG = CFG["eye"]
W_M = EYE_CFG["w"]                                  # the eye's width in its sticker plane (m)
EYES = [frame(-EYE_CFG["az"], EYE_CFG["el"], EYE_CFG.get("roll", 0), EYE_CFG.get("front", 0)),
        frame(EYE_CFG["az"], EYE_CFG["el"], EYE_CFG.get("roll", 0), EYE_CFG.get("front", 0))]   # viewer's left, right
BL = CFG.get("blush")
BLUSH = [frame(-BL["az"], BL["el"]), frame(BL["az"], BL["el"])] if BL else []
for f in EYES + BLUSH:
    print("sticker at az %+.1f el %+.1f  p (%.4f, %.4f, %.4f)" % (f["az"], f["el"], *f["p"]))


def sticker_coords(p, f):
    q = p - f["p"]
    return q.dot(f["eu"]), q.dot(f["ev"])


def eye_uv(u, v, side):
    """Sticker coordinates (m) of eye `side` -> the cell's own UV (0..1): halves side by side."""
    lu = (u / W_M + ed.HU) / (2 * ed.HU)
    lv = (v / W_M + ed.HV) / (2 * ed.HV)
    return (side + lu) / 2, lv


# ---- patches
def cut_to_box(bm_, f, su, sv):
    """Keep only the part of the patch inside the sticker's box |u| <= su, |v| <= sv."""
    for axis, lim in ((f["eu"], su), (f["ev"], sv)):
        for sgn in (1, -1):
            co = f["p"] + axis * (lim * sgn)
            res = bmesh.ops.bisect_plane(bm_, geom=bm_.verts[:] + bm_.edges[:] + bm_.faces[:], plane_co=co,
                                         plane_no=axis * sgn, clear_outer=True)


def facet_patch(f, su, sv, name, uvfun, lift=0.0003, smooth=False, dome=0.0, dome_w=1.0, dome_h=1.0, cuts=0):
    """The head's facets under the sticker box, lifted a little, cut to the box. smooth: shade it with the smooth
    head's normals (custom normals), tilted by a dome of height `dome` that reaches zero at dome_w x dome_h (half
    sizes); the dome also lifts the geometry. cuts: subdivide first so the dome is smooth."""
    bm_ = bmesh.new()
    bm_.from_mesh(me)
    keep = set()
    for fc in bm_.faces:
        if fc.normal.dot(f["n"]) < 0.15:
            continue
        if all(v.co.z < NECK_Z - 0.02 for v in fc.verts):
            continue
        for v in fc.verts:
            u, w = sticker_coords(v.co, f)
            if abs(u) < su + 0.03 and abs(w) < sv + 0.03:
                keep.add(fc)
                break
    nb = bmesh.new()
    vm = {}
    for fc in keep:
        vs = []
        for v in fc.verts:
            if v not in vm:
                vm[v] = nb.verts.new(v.co.copy())
            vs.append(vm[v])
        try:
            nb.faces.new(vs)
        except ValueError:
            pass
    bm_.free()
    cut_to_box(nb, f, su, sv)
    if cuts:
        bmesh.ops.triangulate(nb, faces=nb.faces[:])
        bmesh.ops.subdivide_edges(nb, edges=nb.edges[:], cuts=cuts, use_grid_fill=True)
    nb.normal_update()
    # lift along each vertex's (flat) face normal average, plus the dome
    def dome_of(u, v):
        dn = (u / dome_w) ** 2 + (v / dome_h) ** 2
        return dome * max(0.0, 1 - dn)
    moves = {}
    for v in nb.verts:
        fn = Vector((0, 0, 0))
        for fc in v.link_faces:
            fn += fc.normal
        fn = fn.normalized() if fn.length > 0 else (v.co - HC).normalized()
        u, w = sticker_coords(v.co, f)
        moves[v] = fn * (lift + (dome_of(u, w) if smooth else 0.0))
    for v, mv in moves.items():
        v.co += mv
    uvl = nb.loops.layers.uv.new("UVMap")
    for fc in nb.faces:
        fc.smooth = smooth
        for l in fc.loops:
            l[uvl].uv = uvfun(*sticker_coords(l.vert.co, f))
    m = bpy.data.meshes.new(name)
    nb.to_mesh(m)
    nb.free()
    if smooth:
        # the smooth head's normal, tilted by the dome's slope (in the sticker plane)
        eps = 0.0005
        nrm = []
        for l in m.loops:
            p = m.vertices[l.vertex_index].co
            u, w = sticker_coords(p, f)
            gu = (dome_of(u + eps, w) - dome_of(u - eps, w)) / (2 * eps)
            gv = (dome_of(u, w + eps) - dome_of(u, w - eps)) / (2 * eps)
            n0 = ell_normal(p)
            nrm.append((n0 - f["eu"] * gu - f["ev"] * gv).normalized())
        m.normals_split_custom_set(nrm)
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o


def lens_patch(f, su, sv, name, uvfun, dome, lift, dome_w, dome_h, n_u=40, n_v=80):
    """A smooth patch on the smooth head (each grid point of the sticker's box carried along the sticker's normal to
    the ellipsoid), lifted, with a dome of height `dome` reaching zero at dome_w x dome_h (half sizes)."""
    bm_ = bmesh.new()
    uvl = bm_.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(n_v + 1):
        row = []
        for i in range(n_u + 1):
            u, v = (i / n_u * 2 - 1) * su, (j / n_v * 2 - 1) * sv
            q = f["p"] + f["eu"] * u + f["ev"] * v
            s = ell_hit(q - f["n"] * 0.3, f["n"])
            dn = (u / dome_w) ** 2 + (v / dome_h) ** 2
            s = s + ell_normal(s) * (lift + dome * max(0.0, 1 - dn) ** 1.0)
            row.append((bm_.verts.new(s), uvfun(u, v)))
        grid.append(row)
    for j in range(n_v):
        for i in range(n_u):
            cs = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
            fc = bm_.faces.new([c[0] for c in cs])
            fc.smooth = True
            for l, c in zip(fc.loops, cs):
                l[uvl].uv = c[1]
    m = bpy.data.meshes.new(name)
    bm_.to_mesh(m)
    bm_.free()
    mid = m.polygons[len(m.polygons) // 2]
    if (mid.center - HC).dot(mid.normal) < 0:
        m.flip_normals()
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o


# ---- materials
def img_node(nt, path, non_color=False):
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = bpy.data.images.load(path, check_existing=True)
    if non_color:
        n.image.colorspace_settings.name = "Non-Color"
    n.extension = "CLIP"
    n.interpolation = CFG.get("interp", "Cubic")
    return n


def eye_material(name, bump):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = CFG.get("eye_rough", 0.25 if TEAM == "white" else 0.5)
    bsdf.inputs["Specular IOR Level"].default_value = CFG.get("eye_spec", 0.12 if TEAM == "white" else 0.2)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.name = "Cell"
    mp.inputs["Scale"].default_value = (0.25, 0.25, 1)
    r, c = CFG.get("cell", (0, 0))
    mp.inputs["Location"].default_value = (c * 0.25, (3 - r) * 0.25, 0)
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
    col = img_node(nt, CFG["atlas"])
    nt.links.new(mp.outputs["Vector"], col.inputs["Vector"])
    # eye_albedo < 1 lets the glow carry the colour (the design's ivory eyes are flat, the same under any light)
    alb = nt.nodes.new("ShaderNodeMix")
    alb.data_type = "RGBA"
    alb.blend_type = "MULTIPLY"
    alb.inputs["Factor"].default_value = 1.0
    nt.links.new(col.outputs["Color"], alb.inputs["A"])
    alb.inputs["B"].default_value = (*([CFG.get("eye_albedo", 1.0)] * 3), 1)
    nt.links.new(alb.outputs["Result"], bsdf.inputs["Base Color"])
    nt.links.new(col.outputs["Alpha"], bsdf.inputs["Alpha"])
    # a little glow on the light parts (the glint; the black team's ivory eyes), so they stay white under any light
    lum = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(col.outputs["Color"], lum.inputs[0])
    rng = nt.nodes.new("ShaderNodeMapRange")
    rng.inputs["From Min"].default_value, rng.inputs["From Max"].default_value = 0.45, 0.85
    rng.inputs["To Min"].default_value = 0.0
    rng.inputs["To Max"].default_value = CFG.get("glow", 0.5 if TEAM == "white" else 0.35)
    nt.links.new(lum.outputs[0], rng.inputs["Value"])
    nt.links.new(col.outputs["Color"], bsdf.inputs["Emission Color"])
    nt.links.new(rng.outputs["Result"], bsdf.inputs["Emission Strength"])
    if bump:
        hi = img_node(nt, CFG["height"], True)
        nt.links.new(mp.outputs["Vector"], hi.inputs["Vector"])
        bp = nt.nodes.new("ShaderNodeBump")
        bp.inputs["Strength"].default_value = 1.0
        bp.inputs["Distance"].default_value = CFG.get("bump", 0.002)
        nt.links.new(hi.outputs["Color"], bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def blush_material(name):
    """A flat disc: the sticker's UV is (u, v) / blush diameter + 0.5; alpha from the distance to its edge."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.5
    bsdf.inputs["Base Color"].default_value = (*[c ** 2.2 for c in BL["colour"]], 1)
    bsdf.inputs["Emission Color"].default_value = (*[c ** 2.2 for c in BL["colour"]], 1)
    bsdf.inputs["Emission Strength"].default_value = BL.get("glow", 0.0)
    img = img_node(nt, BLUSH_TEX)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tc.outputs["UV"], img.inputs["Vector"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = BL.get("alpha", 1.0)
    nt.links.new(img.outputs["Alpha"], mul.inputs[0])
    nt.links.new(mul.outputs[0], bsdf.inputs["Alpha"])
    return m


def blush_texture(path, aspect, size=512):
    """An anti-aliased disc (aspect = height / width) filling 80% of the square."""
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    U = (xx + 0.5) / size * 2 - 1
    V = (yy + 0.5) / size * 2 - 1
    rx, ry = 0.8, 0.8 * aspect
    r = np.sqrt((U / rx) ** 2 + (V / ry) ** 2)
    d = (r - 1) * min(rx, ry) * size / 2
    al = np.clip(0.5 - d, 0, 1)
    img = bpy.data.images.new("blush", size, size, alpha=True)
    img.pixels = np.dstack([np.ones_like(al), np.ones_like(al), np.ones_like(al), al]).ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return path


body_mat = bpy.data.materials.new("Body")
body_mat.use_nodes = True
_nt = body_mat.node_tree
_b = _nt.nodes["Principled BSDF"]
_vc = _nt.nodes.new("ShaderNodeVertexColor")
_vc.layer_name = "Col"
_nt.links.new(_vc.outputs["Color"], _b.inputs["Base Color"])
_b.inputs["Roughness"].default_value = CFG.get("body_rough", 0.5)
me.materials.clear()
me.materials.append(body_mat)

# ---- build the face
METHODS = CFG.get("methods", ["lens"])
SU, SV = ed.HU * W_M, ed.HV * W_M
ed.STYLES[CFG.get("style", "oval")].update(ed.TEAM_STYLE.get((TEAM, CFG.get("style", "oval")), {}))
EA_, EH_ = 0.5 * W_M, 0.5 * W_M * ed.STYLES[CFG.get("style", "oval")]["aspect"]
objs = {}
for meth in METHODS:
    if meth == "none":
        continue
    mat = eye_material("Eye_" + meth, meth in ("bevel", "lensbevel"))
    parts = []
    for side, f in enumerate(EYES):
        uvf = (lambda s: (lambda u, v: eye_uv(u, v, s)))(side)
        if meth in ("flat", "bevel"):
            o = facet_patch(f, SU, SV, "Eye%d_%s" % (side, meth), uvf)
        elif not CFG.get("float_lens", False):
            o = facet_patch(f, SU, SV, "Eye%d_%s" % (side, meth), uvf, smooth=True, dome=CFG.get("dome", 0.0025),
                            dome_w=EA_ * CFG.get("dome_reach", 1.24), dome_h=EH_ * CFG.get("dome_reach", 1.24),
                            cuts=CFG.get("cuts", 4))
        else:
            o = lens_patch(f, SU, SV, "Eye%d_%s" % (side, meth), uvf, CFG.get("dome", 0.0025), CFG.get("lift", 0.0004),
                           EA_ * CFG.get("dome_reach", 1.24), EH_ * CFG.get("dome_reach", 1.24))
        o.data.materials.append(mat)
        parts.append(o)
    objs[meth] = parts
if BL:
    BLUSH_TEX = blush_texture(os.path.join(OUT, "blush_disc.png"), BL.get("aspect", 1.0))
    bmat = blush_material("Blush")
    half = BL["d"] / 2 / 0.8
    for side, f in enumerate(BLUSH):
        o = facet_patch(f, half, half, "Blush%d" % side, lambda u, v: (u / (2 * half) + 0.5, v / (2 * half) + 0.5),
                        lift=0.00015)
        o.data.materials.append(bmat)
        objs.setdefault("_blush", []).append(o)

# everything that turns with the piece
root = bpy.data.objects.new("Turn", None)
bpy.context.scene.collection.objects.link(root)
for o in [pawn] + [o for ps in objs.values() for o in ps]:
    if o is pawn:
        continue
    o.parent = root
arm.parent = root

# ---- studio stage (render_studio.py: big soft key from the upper left front, warm bright surround)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
prefs = bpy.context.preferences.addons["cycles"].preferences
try:
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for dv in prefs.devices:
        dv.use = True
    sc.cycles.device = "GPU"
except Exception:
    sc.cycles.device = "CPU"
sc.cycles.samples = CFG.get("samples", 96)
sc.cycles.use_denoising = True
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (*CFG.get("world_rgb", (1.0, 0.86, 0.70)), 1)
world.node_tree.nodes["Background"].inputs[1].default_value = CFG.get("world", 0.55)
sc.world = world
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"
key.data.size = 3.0
key.data.energy = CFG.get("key", 170)
sc.collection.objects.link(key)
key.location = Vector((-2.2, -3.2, 3.0))
key.rotation_euler = (Vector((0, 0, 0.5)) - key.location).to_track_quat("-Z", "Y").to_euler()
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam

for vw in CFG["views"]:
    root.rotation_euler = (0, 0, math.radians(-vw["turn"]))      # turn > 0 shows the piece's left side
    el = math.radians(vw.get("el", 5))
    target = Vector(vw.get("target", (0, 0, 0.56)))
    cam.data.lens = vw.get("lens", 100)
    cam.location = target + Vector((0, -math.cos(el), math.sin(el))) * vw.get("dist", 3.3)
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.resolution_x = sc.render.resolution_y = vw.get("res", 627)
    for meth in vw.get("methods", METHODS):
        for k, ps in objs.items():
            for o in ps:
                o.hide_render = not (k == meth or k == "_blush")
        cells = CFG.get("cells")
        for cell in (cells or [None]):
            name = "%s_%s" % (vw["name"], meth)
            if cell is not None:
                r, c = cell
                for m in bpy.data.materials:
                    if m.node_tree and "Cell" in m.node_tree.nodes:
                        m.node_tree.nodes["Cell"].inputs["Location"].default_value = (c * 0.25, (3 - r) * 0.25, 0)
                name += "_r%dc%d" % (r, c)
            path = os.path.join(OUT, name + ".png")
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            print("RENDERED", path)
print("DONE")
