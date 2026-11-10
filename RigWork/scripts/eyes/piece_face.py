# Stickers (eyes from the expression atlas, blush discs) on a lathe-like piece body imported from an FBX (Tripo parts
# are normalised to height 1, z up after import): the smooth surface is the body's own turned profile r(z), so the
# eye patches can be shaded smooth (custom normals) over the facets. Same patch methods, materials and views as
# pawn_face.py; the eyes are placed by azimuth and height (z) instead of elevation on a head.
# blender -b --factory-startup -P piece_face.py -- <config.json>
#   config: body (fbx), out, team, style, atlas, height, eye {az, z, w, roll}, blush {az, z, d, aspect, colour, glow},
#           methods, views, ...
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
BODY = tuple(CFG.get("body_colour", (0.97, 0.86, 0.73) if TEAM == "white" else (0.30, 0.30, 0.31)))

bpy.ops.wm.read_factory_settings(use_empty=True)
if CFG.get("body_blend"):
    # a clean low-poly body (RigWork/pieces/models/<Piece>_LowPoly.blend)
    with bpy.data.libraries.load(CFG["body_blend"], link=False) as (src, dst):
        dst.objects = [CFG.get("body_object", src.objects[0])]
    for o in dst.objects:
        bpy.context.scene.collection.objects.link(o)
else:
    bpy.ops.import_scene.fbx(filepath=CFG["body"])
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
for o in bpy.context.scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
pawn = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
s_ = CFG.get("scale", 1.0)
if s_ != 1.0:
    pawn.scale = (s_, s_, s_)
    bpy.ops.object.transform_apply(scale=True)
me = pawn.data
arm = None

# ---- the turned profile, from the back of the body (the Tripo bodies have the design's face sculpted in front: raised
# eyes and blush discs), as the median radius per thin height bin, smoothed
co = np.array([v.co[:] for v in me.vertices])
zs, rs = co[:, 2], np.hypot(co[:, 0], co[:, 1])
azs = np.degrees(np.arctan2(co[:, 0], -co[:, 1]))
back = np.abs(azs) > CFG.get("profile_from_az", 110)
NB = 250
z0, z1 = zs.min(), zs.max()
edges = np.linspace(z0, z1, NB + 1)
prof = np.full(NB, np.nan)
for i in range(NB):
    m = back & (zs >= edges[i]) & (zs <= edges[i + 1])
    if m.sum() >= 3:
        prof[i] = np.percentile(rs[m], 90)          # the outer surface, not an inner shell
idx = np.arange(NB)
ok = ~np.isnan(prof)
prof = np.interp(idx, idx[ok], prof[ok])
# sparse bins dip inwards: take the running maximum over a few bins, then smooth (Gaussian)
env = CFG.get("profile_envelope", 0)
if env:
    prof = np.array([prof[max(0, i - env):i + env + 1].max() for i in range(NB)])
sig = CFG.get("profile_sigma", 1.5)
kx = np.arange(-int(3 * sig) - 1, int(3 * sig) + 2)
k = np.exp(-0.5 * (kx / sig) ** 2); k /= k.sum()
prof = np.convolve(np.pad(prof, len(kx) // 2, mode="edge"), k, mode="valid")
zc_bins = (edges[:-1] + edges[1:]) / 2
def radius(z):
    return float(np.interp(z, zc_bins, prof))
def dradius(z, h=0.01):
    return (radius(z + h) - radius(z - h)) / (2 * h)
print("PROFILE z %.3f..%.3f, r at 0.25/0.5/0.75 of the height: %.3f %.3f %.3f" % (
    z0, z1, radius(z0 + 0.25 * (z1 - z0)), radius(z0 + 0.5 * (z1 - z0)), radius(z0 + 0.75 * (z1 - z0))))
# a clean turned body instead (the Tripo faces' sculpted eyes and blush, and their big front faces, spoil stickers):
# rings every other bin of the back profile, N flat sides, capped
NS = CFG.get("lathe_sides", 0)
if NS:
    bm_ = bmesh.new()
    ring_z = zc_bins[::2]
    rings = []
    for z in ring_z:
        r = radius(z)
        rings.append([bm_.verts.new((r * math.sin(2 * math.pi * (i + 0.5) / NS), -r * math.cos(2 * math.pi * (i + 0.5) / NS), z))
                      for i in range(NS)])
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for i in range(NS):
            bm_.faces.new((a_[i], a_[(i + 1) % NS], b_[(i + 1) % NS], b_[i]))
    for ring, z in ((rings[0], ring_z[0]), (rings[-1], ring_z[-1])):
        c_ = bm_.verts.new((0, 0, z))
        for i in range(NS):
            bm_.faces.new((ring[i], ring[(i + 1) % NS], c_) if z == ring_z[-1] else (ring[(i + 1) % NS], ring[i], c_))
    bmesh.ops.recalc_face_normals(bm_, faces=bm_.faces[:])
    bm_.to_mesh(me)
    bm_.free()
    for pl in me.polygons:
        pl.use_smooth = False
    print("rebuilt as a turned body: %d sides, %d rings" % (NS, len(ring_z)))
# the sculpted face out: front vertices in the face band go to the turned surface
FB = CFG.get("face_band") if not NS else None
if FB:
    n_moved = 0
    for v, z, r, a in zip(me.vertices, zs, rs, azs):
        if FB[0] <= z <= FB[1] and abs(a) < 85 and r > 1e-4:
            t = radius(z) / r
            v.co.x *= t; v.co.y *= t
            n_moved += 1
    me.update()
    print("face band %.3f..%.3f flattened: %d vertices" % (FB[0], FB[1], n_moved))
HC = Vector((0, 0, 0))


def ell_normal_profile(p):
    """The smooth (turned) surface's normal at p."""
    rho = math.hypot(p.x, p.y)
    if rho < 1e-6:
        return Vector((0, 0, 1))
    return Vector((p.x / rho, p.y / rho, -dradius(p.z))).normalized()


SMOOTH = None
if CFG.get("smooth") == "mesh":
    # smooth shading of the body itself: its vertex normals, interpolated over the nearest triangle
    import mathutils.geometry as mg
    _bm = bmesh.new()
    _bm.from_mesh(me)
    bmesh.ops.triangulate(_bm, faces=_bm.faces[:])
    _bm.normal_update()
    _tris = [[(v.co.copy(), v.normal.copy()) for v in f.verts] for f in _bm.faces]
    SMOOTH = BVHTree.FromBMesh(_bm)
    _bm.free()


def ell_normal(p):
    if SMOOTH is None:
        return ell_normal_profile(p)
    loc, nrm, idx, dist = SMOOTH.find_nearest(p)
    (a, na), (b, nb), (c, nc) = _tris[idx]
    w = mg.barycentric_transform(loc, a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    n = na * w.x + nb * w.y + nc * w.z
    return n.normalized() if n.length > 1e-8 else nrm


tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
NECK_Z = -1e9


def frame(az, z, roll=0.0):
    """Sticker frame at azimuth az (degrees from the front, towards +x) and height z."""
    a = math.radians(az)
    d = Vector((math.sin(a), -math.cos(a), 0))
    hit = tree.ray_cast(Vector((0, 0, z)) + d * 2.0, -d)
    p = hit[0]
    n = ell_normal(p)
    eu = Vector((-n.y, n.x, 0)).normalized()
    ev = n.cross(eu).normalized()
    if roll:
        r = math.radians(roll) * (1 if az < 0 else -1)
        eu, ev = eu * math.cos(r) - ev * math.sin(r), eu * math.sin(r) + ev * math.cos(r)
    return dict(p=p, n=n, eu=eu, ev=ev, az=az, el=z)


# ---- the face's numbers
EYE_CFG = CFG["eye"]
W_M = EYE_CFG["w"]                                  # the eye's width in its sticker plane (m)
EYES = [frame(-EYE_CFG["az"], EYE_CFG["z"], EYE_CFG.get("roll", 0)),
        frame(EYE_CFG["az"], EYE_CFG["z"], EYE_CFG.get("roll", 0))]                     # viewer's left, right
BL = CFG.get("blush")
BLUSH = [frame(-BL["az"], BL["z"]), frame(BL["az"], BL["z"])] if BL else []
for f in EYES + BLUSH:
    print("sticker at az %+.1f z %.3f  p (%.4f, %.4f, %.4f)" % (f["az"], f["el"], *f["p"]))


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
        uv_ = [sticker_coords(v.co, f) for v in fc.verts]
        depth = (fc.calc_center_median() - f["p"]).dot(f["n"])
        if abs(depth) > 0.06:                       # not the inner shell or the far side
            continue
        if (min(q[0] for q in uv_) < su + 0.005 and max(q[0] for q in uv_) > -su - 0.005 and
                min(q[1] for q in uv_) < sv + 0.005 and max(q[1] for q in uv_) > -sv - 0.005):
            keep.add(fc)
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
        fn = fn.normalized() if fn.length > 0 else ell_normal(v.co)
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
    if ell_normal(mid.center).dot(mid.normal) < 0:
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
_b.inputs["Base Color"].default_value = (*[c ** 2.2 for c in BODY], 1)
_b.inputs["Roughness"].default_value = CFG.get("body_rough", 0.5)
me.materials.clear()
me.materials.append(body_mat)

# ---- build the face
METHODS = CFG.get("methods", ["lens"])
SU, SV = ed.HU * W_M, ed.HV * W_M
ed.STYLES[CFG.get("style", "oval")].update(ed.TEAM_STYLE.get((TEAM, CFG.get("style", "oval")), {}))
ed.STYLES[CFG.get("style", "oval")].update({k: (tuple(v) if isinstance(v, list) else v) for k, v in CFG.get("style_over", {}).items()})
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
    o.parent = root

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
