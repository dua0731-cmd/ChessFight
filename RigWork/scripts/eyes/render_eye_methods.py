# Three ways to put the expression sheet on the low-poly pawn's head, rendered under the studio light (Cycles):
#   flat   - the eyes drawn straight onto the head's own facets (a texture layer on the head)
#   lens   - the same drawing on a smooth, slightly domed lens over each eye (alpha cut: only the shape stays)
#   bevel  - the flat drawing, but the shader rounds its edges from the shape (bump from a blurred copy)
# blender -b -P render_eye_methods.py -- <out dir> <Pawn_Rigged.blend> <sheet.png> <height.png> [view front|three]
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
OUT, BLEND, SHEET, HEIGHT = a[:4]
VIEW = a[4] if len(a) > 4 else "three"
os.makedirs(OUT, exist_ok=True)

SKIN, HEADC, CHEEK = (0.945, 0.808, 0.702), (0.898, 0.753, 0.649), (0.91, 0.59, 0.53)
EYE, GLINT, HAND, FOOT = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
CREAM = (0.97, 0.86, 0.73)
CELLS = {"neutral": (0, 0), "happy": (0, 3), "angry": (1, 1), "ko": (2, 0), "surprised": (1, 0), "hearts": (3, 1)}
SHOW = ["neutral", "happy", "angry", "ko"]
PX_PER_M = 92.0 / 0.070        # the sheet's eye (92 px wide) is 7 cm wide on the head
CELL = 512

def near(c, ref, tol=0.04):
    return max(abs(c[i] - ref[i]) for i in range(3)) < tol

bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
pawn = bpy.data.objects["Pawn"]
me = pawn.data

# the head's middle and radius, fitted to the lathe's head vertices (centre on the axis)
hv = [v.co for v in me.vertices if v.co.z > 0.66 and math.hypot(v.co.x, v.co.y) > 0.05]
s11 = sum(4 * p.z * p.z for p in hv); s12 = sum(2 * p.z for p in hv); s22 = len(hv)
t1 = sum(2 * p.z * (p.x * p.x + p.y * p.y + p.z * p.z) for p in hv); t2 = sum(p.x * p.x + p.y * p.y + p.z * p.z for p in hv)
det = s11 * s22 - s12 * s12
ZC = (t1 * s22 - s12 * t2) / det
R = max(math.sqrt(p.x * p.x + p.y * p.y + (p.z - ZC) ** 2) for p in hv)
EYE_Z = ZC - 0.35 * R           # the concept: eyes a third of the radius below the head's middle
print("HEAD centre z %.4f radius %.4f eye z %.4f" % (ZC, R, EYE_Z))

# colours (cream body), and the old eye and glint geometry removed (the blush stays)
attr = me.color_attributes["Col"]
dead = set()
for poly in me.polygons:
    c = attr.data[poly.loop_start].color_srgb[:3]
    if poly.center.z > 0.6 and (near(c, EYE, 0.03) or near(c, GLINT, 0.03)):
        dead.add(poly.index)
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
kill = [bm.faces[i] for i in dead]
bmesh.ops.delete(bm, geom=kill, context="FACES")
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context="VERTS")
bm.to_mesh(me)
bm.free()
attr = me.color_attributes["Col"]
for d in attr.data:
    c = d.color_srgb[:3]
    if near(c, SKIN) or near(c, HEADC):
        new = CREAM
    elif near(c, HAND) or near(c, FOOT):
        new = (0.035, 0.032, 0.032)
    else:
        w = max(0.0, min(1.0, sum((c[i] - HEADC[i]) * (CHEEK[i] - HEADC[i]) for i in range(3)) /
                         sum((CHEEK[i] - HEADC[i]) ** 2 for i in range(3))))
        new = tuple(CREAM[i] + ((0.93, 0.55, 0.52)[i] - CREAM[i]) * w for i in range(3))
    d.color_srgb = (*new, 1.0)
print("removed old eye faces", len(kill))

body_mat = bpy.data.materials.new("Body")
body_mat.use_nodes = True
nt = body_mat.node_tree
bsdf = nt.nodes["Principled BSDF"]
vc = nt.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
nt.links.new(vc.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.5
me.materials.clear()
me.materials.append(body_mat)

def uv_of(p):
    """Front projection into the sheet's bottom-left cell; the material's mapping moves it to the expression."""
    u = (CELL / 2 + p.x * PX_PER_M) / CELL * 0.25
    v = (CELL / 2 + (p.z - EYE_Z) * PX_PER_M) / CELL * 0.25
    return u, v

def eye_material(name, bevel):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.28
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.name = "Expr"
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(SHEET)
    img.extension = "CLIP"
    img.interpolation = "Cubic"
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], img.inputs["Vector"])
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(img.outputs["Alpha"], bsdf.inputs["Alpha"])
    if bevel:
        hi = nt.nodes.new("ShaderNodeTexImage")
        hi.image = bpy.data.images.load(HEIGHT)
        hi.image.colorspace_settings.name = "Non-Color"
        hi.extension = "CLIP"
        hi.interpolation = "Cubic"
        nt.links.new(mp.outputs["Vector"], hi.inputs["Vector"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 1.0
        bump.inputs["Distance"].default_value = 0.004
        nt.links.new(hi.outputs["Color"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m

def face_patch(name):
    """The head's own front facets around the eyes, lifted 0.25 mm: a texture layer that follows every facet."""
    bm = bmesh.new()
    bm.from_mesh(me)
    keep = [f for f in bm.faces if all(abs(v.co.x) < 0.18 and abs(v.co.z - EYE_Z) < 0.17 and v.co.y < -0.02 and
                                       v.co.z > 0.62 for v in f.verts)
            and f.calc_center_median().z > 0.63 and f.normal.y < -0.2]
    nb = bmesh.new()
    vmap = {}
    for f in keep:
        vs = []
        for v in f.verts:
            if v not in vmap:
                d = (v.co - Vector((0, 0, ZC))).normalized()
                vmap[v] = nb.verts.new(v.co + d * 0.00025)
            vs.append(vmap[v])
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

def lenses(name):
    """A smooth lens over each eye: the head's smooth sphere (through the facets' corners), domed 2 mm in the
    middle; the drawing's alpha cuts it to the eye's shape, so the shape stands a little proud of the facets."""
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    hw, hh, n = 0.052, 0.085, 40
    for sx in (-1, 1):
        cx = sx * 0.0875
        grid = []
        for j in range(n + 1):
            row = []
            for i in range(n + 1):
                x = cx + (i / n * 2 - 1) * hw
                z = EYE_Z + (j / n * 2 - 1) * hh
                rr = R * R - x * x - (z - ZC) ** 2
                y = -math.sqrt(max(rr, 1e-8))
                p = Vector((x, y, z))
                dn = ((x - cx) / hw) ** 2 + ((z - EYE_Z) / hh) ** 2
                lift = 0.0004 + 0.002 * max(0.0, 1 - dn)
                p = p + (p - Vector((0, 0, ZC))).normalized() * lift
                row.append(bm.verts.new(p))
            grid.append(row)
        for j in range(n):
            for i in range(n):
                f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
                for l in f.loops:
                    l[uvl].uv = uv_of(l.vert.co)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    for p in m.polygons:
        p.use_smooth = True
    # outward
    if m.polygons[0].normal.y > 0:
        m.flip_normals()
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

methods = {
    "flat": (face_patch("Eyes_flat"), eye_material("EyeFlat", False)),
    "lens": (lenses("Eyes_lens"), eye_material("EyeLens", False)),
    "bevel": (face_patch("Eyes_bevel"), eye_material("EyeBevel", True)),
}
for key, (obj, mat) in methods.items():
    obj.data.materials.append(mat)

# studio stage (as render_studio.py)
sc = bpy.context.scene
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
sc.render.resolution_x, sc.render.resolution_y = 520, 520
sc.view_settings.view_transform = "Standard"
sc.render.film_transparent = True
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
cam.data.lens = 85
az = math.radians(0 if VIEW == "front" else -24)
el = math.radians(4)
target = Vector((0, 0, ZC - 0.07))
dist = 1.55
cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * dist
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam

tiles = []
for expr in SHOW:
    r, c = CELLS[expr]
    row = []
    for key_, (obj, mat) in methods.items():
        for k2, (o2, _) in methods.items():
            o2.hide_render = (k2 != key_)
        mp = mat.node_tree.nodes["Expr"]
        mp.inputs["Location"].default_value = (c * 0.25, (3 - r) * 0.25, 0)
        path = os.path.join(OUT, "%s_%s_%s.png" % (VIEW, expr, key_))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(path)
        w, h = im.size
        x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
        bpy.data.images.remove(im)
        al = x[..., 3:4]
        rgb = x[..., :3] + (1 - al)          # onto white
        row.append(np.concatenate([np.clip(rgb, 0, 1), np.ones_like(al)], axis=2))
        print("RENDERED", expr, key_)
    tiles.append(row)

gap_v = np.ones((520, 8, 4), dtype=np.float32) * np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
rows = []
for row in tiles:
    parts = []
    for t in row:
        parts += [t, gap_v]
    rows.append(np.concatenate(parts[:-1], axis=1))
gap_h = np.ones((8, rows[0].shape[1], 4), dtype=np.float32) * np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
stack = []
for r_ in reversed(rows):                 # Blender images start at the bottom
    stack += [r_, gap_h]
sheet = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("cmp", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = os.path.join(OUT, "eye_methods_%s.png" % VIEW)
img.file_format = "PNG"
img.save()
print("SHEET", os.path.join(OUT, "eye_methods_%s.png" % VIEW))
