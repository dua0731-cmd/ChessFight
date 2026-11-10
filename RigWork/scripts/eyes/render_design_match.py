# The old pawn's own eyes against three ways of drawing the very same eyes, on a bare face (no blush), studio light.
#   old    - the old pawn's eye and glint geometry, as it sits on the head now
#   flat   - the same eyes drawn straight onto the head's facets
#   lens   - the same drawing on a smooth lens domed 2 mm over each eye (alpha cut)
#   bevel  - the flat drawing with the shader rounding its edges (bump from a blurred copy)
# The drawing is the old geometry seen from straight ahead (an orthographic render of it), so from the front all four
# are the same shape. blender -b -P render_eye_compare.py -- <out dir> <Pawn_Rigged.blend>
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
OUT, BLEND = a[:2]
os.makedirs(OUT, exist_ok=True)

SKIN, HEADC, CHEEK = (0.945, 0.808, 0.702), (0.898, 0.753, 0.649), (0.91, 0.59, 0.53)
EYE, GLINT, HAND, FOOT = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
CREAM = (0.97, 0.86, 0.73)
S = 0.2                      # the drawing covers x, z within +-S of the eyes' middle

def near(c, ref, tol=0.04):
    return max(abs(c[i] - ref[i]) for i in range(3)) < tol

bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
pawn = bpy.data.objects["Pawn"]
me = pawn.data
attr = me.color_attributes["Col"]

hv = [v.co for v in me.vertices if v.co.z > 0.66 and math.hypot(v.co.x, v.co.y) > 0.05]
s11 = sum(4 * p.z * p.z for p in hv); s12 = sum(2 * p.z for p in hv); s22 = len(hv)
t1 = sum(2 * p.z * (p.x * p.x + p.y * p.y + p.z * p.z) for p in hv); t2 = sum(p.x * p.x + p.y * p.y + p.z * p.z for p in hv)
det = s11 * s22 - s12 * s12
ZC = (t1 * s22 - s12 * t2) / det
R = max(math.sqrt(p.x * p.x + p.y * p.y + (p.z - ZC) ** 2) for p in hv)

# sort the faces: old eye / glint geometry, blush discs, the rest
eye_faces, blush_faces = set(), set()
for poly in me.polygons:
    c = attr.data[poly.loop_start].color_srgb[:3]
    if poly.center.z < 0.6:
        continue
    if near(c, EYE, 0.03) or near(c, GLINT, 0.03):
        eye_faces.add(poly.index)
    elif not near(c, SKIN) and not near(c, HAND) and not near(c, FOOT):
        blush_faces.add(poly.index)        # the blush discs: head colour at the rim, pink in the middle

# the old eyes, copied out with their colours
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
eb = bmesh.new()
cl_src = bm.loops.layers.color.get("Col")
cl_dst = eb.loops.layers.color.new("Col")
vmap = {}
for i in eye_faces:
    f = bm.faces[i]
    vs = []
    for v in f.verts:
        if v not in vmap:
            vmap[v] = eb.verts.new(v.co)
        vs.append(vmap[v])
    nf = eb.faces.new(vs)
    for l_src, l_dst in zip(f.loops, nf.loops):
        l_dst[cl_dst] = l_src[cl_src]
    nf.smooth = f.smooth
old_me = bpy.data.meshes.new("OldEyes")
eb.to_mesh(old_me)
eb.free()
old_eyes = bpy.data.objects.new("OldEyes", old_me)
bpy.context.scene.collection.objects.link(old_eyes)
# the pawn without eyes and without blush
kill = [bm.faces[i] for i in eye_faces | blush_faces]
bmesh.ops.delete(bm, geom=kill, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bm.to_mesh(me)
bm.free()
attr = me.color_attributes["Col"]
for d in attr.data:
    c = d.color_srgb[:3]
    d.color_srgb = (*((0.035, 0.032, 0.032) if (near(c, HAND) or near(c, FOOT)) else CREAM), 1.0)
print("old eye faces", len(eye_faces), "blush faces", len(blush_faces))

ev = [v.co for v in old_me.vertices]
ZT = (min(p.z for p in ev) + max(p.z for p in ev)) / 2
centres = []
for side in (-1, 1):
    pts = [p for p in ev if p.x * side > 0]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    centres.append(((mn + mx) / 2, mx - mn))
print("old eyes middle z %.4f, centres %s" % (ZT, [(round(c.x, 4), round(c.z, 4), round(s.x, 4), round(s.z, 4)) for c, s in centres]))

def uv_of(p):
    return (p.x + S) / (2 * S), (p.z - (ZT - S)) / (2 * S)

# ---- the drawing: the design sheet's front view, measured relative to the head's circle (centre C, radius R):
# eyes at (+-0.40 R, -0.22 R), 0.315 R wide and 0.524 R tall, glint 0.38 of the eye's width, its centre 0.21 of the
# eye's height below its top and 0.09 of its width right of its middle; blush circles 0.31 x 0.29 R at
# (+-0.545 R, -0.53 R), pink (0.96, 0.60, 0.54)
sc = bpy.context.scene
hp = [(math.hypot(v.co.x, v.co.y), v.co.z) for v in me.vertices if v.co.z > 0.66]
A = np.array([[2 * z, 1.0] for r, z in hp]); bb = np.array([r * r + z * z for r, z in hp])
zc_, c_ = np.linalg.lstsq(A, bb, rcond=None)[0]
RF = math.sqrt(c_ + zc_ * zc_)
ZF = zc_
EW, EH = 0.315 * RF, 0.524 * RF
EX, EZ = 0.383 * RF, ZF - 0.16 * RF          # calibrated on the rendered image against the design
BX, BZ, BW, BH = 0.53 * RF, ZF - 0.48 * RF, 0.31 * RF, 0.29 * RF
ZT = EZ
centres = [(Vector((-EX, 0, EZ)), Vector((EW, 0, EH))), (Vector((EX, 0, EZ)), Vector((EW, 0, EH)))]
print("design face: head R %.4f centre %.4f; eyes %.4f x %.4f at x +-%.4f z %.4f; blush at x +-%.4f z %.4f" % (RF, ZF, EW, EH, EX, EZ, BX, BZ))
N = 2048
ppm = N / (2 * S)
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
X = (xx + 0.5) / ppm - S
Z = (yy + 0.5) / ppm - S + ZT
def ell(cx, cz, w, h):
    px, pz = (X - cx) / (w / 2), (Z - cz) / (h / 2)
    return (np.sqrt(px * px + pz * pz) - 1.0) * min(w, h) / 2 * ppm
de = np.minimum(ell(-EX, EZ, EW, EH), ell(EX, EZ, EW, EH))
gr = 0.38 * EW / 2
gz = EZ + EH / 2 - 0.265 * EH
dg = np.minimum(np.sqrt((X - (-EX + 0.125 * EW)) ** 2 + (Z - gz) ** 2), np.sqrt((X - (EX + 0.125 * EW)) ** 2 + (Z - gz) ** 2)) * ppm - gr * ppm
al = np.clip(0.5 - de, 0, 1)
white = np.clip(0.5 - dg, 0, 1) * al
draw = np.dstack([white, white, white, al]).astype(np.float32)
db = np.minimum(ell(-BX, BZ, BW, BH), ell(BX, BZ, BW, BH))
ba = np.clip(0.5 - db / 1.5, 0, 1)
pinkc = np.array([0.96, 0.60, 0.54], dtype=np.float32)              # byte image: sRGB values as they are
blush = np.dstack([np.full_like(ba, pinkc[0]), np.full_like(ba, pinkc[1]), np.full_like(ba, pinkc[2]), ba]).astype(np.float32)
def save(arr, name):
    hh_, ww_ = arr.shape[:2]
    img = bpy.data.images.new(name, ww_, hh_, alpha=True)
    if arr.ndim == 2:
        arr = np.dstack([arr, arr, arr, np.ones_like(arr)])
    img.pixels = arr.ravel()
    img.filepath_raw = os.path.join(OUT, name)
    img.file_format = "PNG"
    img.save()
    return os.path.join(OUT, name)
DRAW = save(draw, "design_eyes_texture.png")
BLUSH = save(blush, "design_blush_texture.png")
inside = np.clip(-de / (0.18 * EW * ppm), 0, 1) ** 0.6
HEIGHT = save(inside * (al > 0), "design_eyes_height.png")

# the piece alone, as on the design sheet: arms, hands and feet out
gidx = {g.index for g in pawn.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Foot"))}
limb = set()
for v in me.vertices:
    if sum(g.weight for g in v.groups if g.group in gidx) >= 0.5:
        limb.add(v.index)
bm2 = bmesh.new()
bm2.from_mesh(me)
bm2.verts.ensure_lookup_table()
bmesh.ops.delete(bm2, geom=[bm2.verts[i] for i in limb], context="VERTS")
bm2.to_mesh(me)
bm2.free()
# ---- materials and the three drawn versions
body_mat = bpy.data.materials.new("Body")
body_mat.use_nodes = True
b = body_mat.node_tree.nodes["Principled BSDF"]
v_ = body_mat.node_tree.nodes.new("ShaderNodeVertexColor"); v_.layer_name = "Col"
body_mat.node_tree.links.new(v_.outputs["Color"], b.inputs["Base Color"])
b.inputs["Roughness"].default_value = 0.5
me.materials.clear(); me.materials.append(body_mat)

old_mat = bpy.data.materials.new("OldEyeMat")
old_mat.use_nodes = True
ob = old_mat.node_tree.nodes["Principled BSDF"]
ov = old_mat.node_tree.nodes.new("ShaderNodeVertexColor"); ov.layer_name = "Col"
old_mat.node_tree.links.new(ov.outputs["Color"], ob.inputs["Base Color"])
ob.inputs["Roughness"].default_value = 0.28
old_me.materials.clear(); old_me.materials.append(old_mat)

def eye_material(name, bevel):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.28
    tc = nt.nodes.new("ShaderNodeTexCoord")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(DRAW)
    img.extension = "CLIP"; img.interpolation = "Cubic"
    nt.links.new(tc.outputs["UV"], img.inputs["Vector"])
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(img.outputs["Alpha"], bsdf.inputs["Alpha"])
    if bevel:
        hi = nt.nodes.new("ShaderNodeTexImage")
        hi.image = bpy.data.images.load(HEIGHT)
        hi.image.colorspace_settings.name = "Non-Color"
        hi.extension = "CLIP"; hi.interpolation = "Cubic"
        nt.links.new(tc.outputs["UV"], hi.inputs["Vector"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 1.0
        bump.inputs["Distance"].default_value = 0.004
        nt.links.new(hi.outputs["Color"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m

def face_patch(name):
    bm = bmesh.new()
    bm.from_mesh(me)
    keep = [f for f in bm.faces if all(abs(v.co.x) < S * 0.95 and abs(v.co.z - ZT) < S * 0.95 and v.co.y < -0.02 and
                                       v.co.z > 0.62 for v in f.verts) and f.normal.y < -0.2]
    nb = bmesh.new()
    vm = {}
    for f in keep:
        vs = []
        for v in f.verts:
            if v not in vm:
                d = (v.co - Vector((0, 0, ZC))).normalized()
                vm[v] = nb.verts.new(v.co + d * 0.00025)
            vs.append(vm[v])
        nb.faces.new(vs)
    bm.free()
    uvl = nb.loops.layers.uv.new("UVMap")
    for f in nb.faces:
        for l in f.loops:
            l[uvl].uv = uv_of(l.vert.co)
    m = bpy.data.meshes.new(name)
    nb.to_mesh(m)
    nb.free()
    for p in m.polygons:
        p.use_smooth = False
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

from mathutils.kdtree import KDTree
_hv = [v.co.copy() for v in me.vertices if v.co.z > 0.64 and v.co.y < 0.05]
_kd = KDTree(len(_hv))
for i, p in enumerate(_hv):
    _kd.insert((p - Vector((0, 0, ZC))).normalized(), i)
_kd.balance()

def smooth_radius(d):
    hits = _kd.find_n(d, 6)
    ws = [1.0 / max(dist, 1e-4) for _, _, dist in hits]
    return sum(w * (_hv[i] - Vector((0, 0, ZC))).length for w, (_, i, _) in zip(ws, hits)) / sum(ws)

def lenses(name):
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    n = 40
    for c, size in centres:
        hw, hh = size.x * 0.62, size.z * 0.62
        grid = []
        for j in range(n + 1):
            row = []
            for i in range(n + 1):
                x = c.x + (i / n * 2 - 1) * hw
                z = c.z + (j / n * 2 - 1) * hh
                y = -math.sqrt(max(R * R - x * x - (z - ZC) ** 2, 1e-8))
                d = (Vector((x, y, z)) - Vector((0, 0, ZC))).normalized()
                dn = ((x - c.x) / hw) ** 2 + ((z - c.z) / hh) ** 2
                p = Vector((0, 0, ZC)) + d * (smooth_radius(d) + 0.0004 + 0.0025 * max(0.0, 1 - dn))
                row.append(bm.verts.new(p))
            grid.append(row)
        for j in range(n):
            for i in range(n):
                f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
                for l in f.loops:
                    l[uvl].uv = uv_of(l.vert.co)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    for p in m.polygons:
        p.use_smooth = True
    if m.polygons[0].normal.y > 0:
        m.flip_normals()
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

old_eyes.hide_render = True
methods = {}
eyes_obj = lenses("Eyes_lens")
eyes_obj.data.materials.append(eye_material("EyeLens", False))
blush_obj = face_patch("Blush")
bm_mat = bpy.data.materials.new("BlushMat")
bm_mat.use_nodes = True
_nt = bm_mat.node_tree
_b = _nt.nodes["Principled BSDF"]
_b.inputs["Roughness"].default_value = 0.5
_tc = _nt.nodes.new("ShaderNodeTexCoord")
_im = _nt.nodes.new("ShaderNodeTexImage")
_im.image = bpy.data.images.load(BLUSH)
_im.extension = "CLIP"
_nt.links.new(_tc.outputs["UV"], _im.inputs["Vector"])
_nt.links.new(_im.outputs["Color"], _b.inputs["Base Color"])
_nt.links.new(_im.outputs["Alpha"], _b.inputs["Alpha"])
blush_obj.data.materials.append(bm_mat)

# ---- studio stage
sc.render.engine = "CYCLES"
prefs = bpy.context.preferences.addons["cycles"].preferences
try:
    prefs.compute_device_type = "OPTIX"; prefs.get_devices()
    for dv in prefs.devices:
        dv.use = True
    sc.cycles.device = "GPU"
except Exception:
    sc.cycles.device = "CPU"
sc.cycles.samples = 128
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 480, 480
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.86, 0.70, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
sc.world = world
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"; key.data.size = 3.0; key.data.energy = 170
sc.collection.objects.link(key)
key.location = Vector((-2.2, -3.2, 3.0))
key.rotation_euler = (Vector((0, 0, 0.5)) - key.location).to_track_quat("-Z", "Y").to_euler()
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
cam.data.lens = 135
sc.camera = cam

VIEWS = [("v17", 80), ("v18", 0), ("v19", -80), ("v20", 38)]
cam.data.lens = 100
sc.render.resolution_x = sc.render.resolution_y = 627
tiles = []
for vname, deg in VIEWS:
    az, el = math.radians(deg), math.radians(6)
    target = Vector((0, 0, 0.56))
    cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * 3.3
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, "design_%s.png" % vname)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED", vname)
print("DONE")
