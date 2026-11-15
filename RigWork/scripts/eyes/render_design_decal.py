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

# ---- decals: each eye and each blush drawn in its own plane, tangent to the head at its middle, projected onto the
# head along that plane's normal, as a sticker sits (the design's side views show the eyes that way). Their middles
# stay where the front view put them; their sizes are the front-view sizes undone for the turn away from the front.
from mathutils.bvhtree import BVHTree
HC = Vector((0, 0, ZF))

def surface_at(x, z):
    hit = head_tree.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
    return hit[0]

def frame_at(x, z, w_front, h_front):
    p = surface_at(x, z)
    n = (p - HC).normalized()
    eu = Vector((-n.y, n.x, 0)).normalized()
    ev = n.cross(eu).normalized()
    turn = math.atan2(abs(n.x), -n.y)
    tilt = math.asin(max(-1.0, min(1.0, n.z)))
    return dict(p=p, n=n, eu=eu, ev=ev, w=w_front / math.cos(turn), h=h_front / math.cos(tilt), turn=turn, tilt=tilt)
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

head_tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
EYES = [frame_at(sx * EX, EZ, EW, EH) for sx in (-1, 1)]
BLUSH = [frame_at(sx * BX, BZ, BW, BH) for sx in (-1, 1)]
for f in EYES + BLUSH:
    print("decal at", [round(c, 4) for c in f["p"]], "turn %.1f tilt %.1f size %.4f x %.4f" % (
        math.degrees(f["turn"]), math.degrees(f["tilt"]), f["w"], f["h"]))

def decal_texture(name, w, h, half, glint, colour, size=1024):
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    U = ((xx + 0.5) / size * 2 - 1) * half
    V = ((yy + 0.5) / size * 2 - 1) * half
    ppm = size / (2 * half)
    d = (np.sqrt((U / (w / 2)) ** 2 + (V / (h / 2)) ** 2) - 1) * min(w, h) / 2 * ppm
    al = np.clip(0.5 - d / 1.2, 0, 1)
    rgb = np.zeros((size, size, 3), dtype=np.float32) + np.array(colour, dtype=np.float32)
    if glint:
        gd = (np.sqrt((U - 0.125 * w) ** 2 + (V - (h / 2 - 0.265 * h)) ** 2) - 0.19 * w) * ppm
        g = np.clip(0.5 - gd, 0, 1) * al
        rgb = rgb * (1 - g[..., None]) + g[..., None]
    img = bpy.data.images.new(name, size, size, alpha=True)
    img.pixels = np.dstack([rgb, al]).ravel()
    img.filepath_raw = os.path.join(OUT, name + ".png")
    img.file_format = "PNG"
    img.save()
    return os.path.join(OUT, name + ".png")

EHALF = 0.75 * max(EYES[0]["h"], EYES[0]["w"])
BHALF = 0.75 * max(BLUSH[0]["h"], BLUSH[0]["w"])
EYE_TEX = decal_texture("eye_decal", EYES[0]["w"], EYES[0]["h"], EHALF, True, (0.0, 0.0, 0.0))
BLUSH_TEX = decal_texture("blush_decal", BLUSH[0]["w"], BLUSH[0]["h"], BHALF, False, (0.96, 0.60, 0.54), 512)

def decal_uv(p, f, half):
    q = p - f["p"]
    return (q.dot(f["eu"]) / half + 1) / 2, (q.dot(f["ev"]) / half + 1) / 2

body_mat = bpy.data.materials.new("Body")
body_mat.use_nodes = True
_b = body_mat.node_tree.nodes["Principled BSDF"]
_v = body_mat.node_tree.nodes.new("ShaderNodeVertexColor")
_v.layer_name = "Col"
body_mat.node_tree.links.new(_v.outputs["Color"], _b.inputs["Base Color"])
_b.inputs["Roughness"].default_value = 0.5
me.materials.clear()
me.materials.append(body_mat)

def tex_material(name, path, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    tc = nt.nodes.new("ShaderNodeTexCoord")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(path)
    img.extension = "CLIP"
    img.interpolation = "Cubic"
    nt.links.new(tc.outputs["UV"], img.inputs["Vector"])
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(img.outputs["Alpha"], bsdf.inputs["Alpha"])
    return m

from mathutils.kdtree import KDTree
_hv = [v.co.copy() for v in me.vertices if v.co.z > 0.64 and v.co.y < 0.05]
_kd = KDTree(len(_hv))
for i, p in enumerate(_hv):
    _kd.insert((p - HC).normalized(), i)
_kd.balance()

def smooth_radius(d):
    hits = _kd.find_n(d, 6)
    ws = [1.0 / max(dist, 1e-4) for _, _, dist in hits]
    return sum(w * (_hv[i] - HC).length for w, (_, i, _) in zip(ws, hits)) / sum(ws)

def lens(f, half, name, n=36):
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(n + 1):
        row = []
        for i in range(n + 1):
            u, v = (i / n * 2 - 1) * half, (j / n * 2 - 1) * half
            q = f["p"] + f["eu"] * u + f["ev"] * v
            d = (q - HC).normalized()
            dn = (u / (f["w"] * 0.62)) ** 2 + (v / (f["h"] * 0.62)) ** 2
            vert = bm.verts.new(HC + d * (smooth_radius(d) + 0.0004 + 0.0025 * max(0.0, 1 - dn)))
            row.append((vert, ((u / half + 1) / 2, (v / half + 1) / 2)))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            cs = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
            fc = bm.faces.new([c[0] for c in cs])
            for l, c in zip(fc.loops, cs):
                l[uvl].uv = c[1]
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    for pl in m.polygons:
        pl.use_smooth = True
    mid = m.polygons[len(m.polygons) // 2]
    if (mid.center - HC).dot(mid.normal) < 0:
        m.flip_normals()
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

def patch(f, half, name):
    bm = bmesh.new()
    bm.from_mesh(me)
    # every facet that reaches into the sticker's square (facets are bigger than a blush); the drawing is clear
    # outside the sticker, so the extra facets show nothing
    def near_(fc):
        c = fc.calc_center_median()
        q = c - f["p"]
        return abs(q.dot(f["eu"])) < half + 0.04 and abs(q.dot(f["ev"])) < half + 0.04 and (c - HC).normalized().dot(f["n"]) > 0.5
    keep = [fc for fc in bm.faces if near_(fc)]
    nb = bmesh.new()
    vm = {}
    for fc in keep:
        vs = []
        for v in fc.verts:
            if v not in vm:
                vm[v] = nb.verts.new(v.co + (v.co - HC).normalized() * 0.00025)
            vs.append(vm[v])
        nb.faces.new(vs)
    bm.free()
    uvl = nb.loops.layers.uv.new("UVMap")
    for fc in nb.faces:
        for l in fc.loops:
            l[uvl].uv = decal_uv(l.vert.co, f, half)
    m = bpy.data.meshes.new(name)
    nb.to_mesh(m)
    nb.free()
    for pl in m.polygons:
        pl.use_smooth = False
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

eye_mat = tex_material("EyeDecal", EYE_TEX, 0.28)
blush_mat = tex_material("BlushDecal", BLUSH_TEX, 0.5)
for k, f in enumerate(EYES):
    lens(f, EHALF, "EyeLens%d" % k).data.materials.append(eye_mat)
for k, f in enumerate(BLUSH):
    patch(f, BHALF, "Blush%d" % k).data.materials.append(blush_mat)
old_eyes.hide_render = True

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

VIEWS = [("v17", 72), ("v18", 0), ("v19", -72), ("v20", 38)]
cam.data.lens = 100
sc.render.resolution_x = sc.render.resolution_y = 627
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
