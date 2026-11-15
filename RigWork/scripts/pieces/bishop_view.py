# Looks at any model (fbx, glb, blend) from several sides under the soft studio light, as one sheet: used to study the
# game's earlier bishop head and to check the new one. Prints the mesh islands of the framed part.
# blender -b --factory-startup -P bishop_view.py -- <model> <out.png> [--zfrom 0.55] [--res 420x520]
#   [--views az:el,az:el,...] (camera azimuth from -y towards +x, elevation, degrees)  [--smooth keep|flat]
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
MODEL, OUT = a[0], a[1]
def opt(name, default):
    return a[a.index(name) + 1] if name in a else default
ZFROM = float(opt("--zfrom", "0.0"))
RW, RH = [int(v) for v in opt("--res", "420x520").split("x")]
VIEWS = [tuple(float(x) for x in v.split(":")) for v in
         opt("--views", "0:3,90:3,180:3,270:3,135:25,225:25,45:25,0:88").split(",")]
SMOOTH = opt("--smooth", "keep")

bpy.ops.wm.read_factory_settings(use_empty=True)
ext = os.path.splitext(MODEL)[1].lower()
if ext == ".fbx":
    bpy.ops.import_scene.fbx(filepath=MODEL)
elif ext in (".glb", ".gltf"):
    bpy.ops.import_scene.gltf(filepath=MODEL)
else:
    bpy.ops.wm.open_mainfile(filepath=MODEL)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()
pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
print("MODEL", os.path.basename(MODEL), "objects", [(o.name, len(o.data.vertices), len(o.data.polygons)) for o in meshes])
print("BOUNDS", [round(v, 4) for v in mn], [round(v, 4) for v in mx])
H = mx.z - mn.z
z0 = mn.z + ZFROM * H
# islands (connected parts) that reach into the framed part
for o in meshes:
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    seen = set()
    isl = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            u = stack.pop()
            comp.append(u.index)
            for e in u.link_edges:
                w = e.other_vert(u)
                if w.index not in seen:
                    seen.add(w.index)
                    stack.append(w)
        cs = [o.matrix_world @ bm.verts[i].co for i in comp]
        lo = [min(c[k] for c in cs) for k in range(3)]
        hi = [max(c[k] for c in cs) for k in range(3)]
        if hi[2] > z0:
            isl.append((len(comp), [round(v, 3) for v in lo], [round(v, 3) for v in hi]))
    bm.free()
    isl.sort(reverse=True)
    print("ISLANDS", o.name, len(isl))
    for n, lo, hi in isl[:14]:
        print("   verts %5d  min %s  max %s" % (n, lo, hi))

mat = bpy.data.materials.new("Clay")
mat.use_nodes = True
b = mat.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1)
b.inputs["Roughness"].default_value = 0.5
for o in meshes:
    o.data.materials.clear()
    o.data.materials.append(mat)
    if SMOOTH == "flat":
        for p in o.data.polygons:
            p.use_smooth = False

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
sc.cycles.samples = 48
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = RW, RH
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.86, 0.70, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
sc.world = world
size = max(mx.x - mn.x, mx.y - mn.y, H)
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"
key.data.size = 3.0 * size
key.data.energy = 170 * size * size
sc.collection.objects.link(key)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
cam.data.type = "ORTHO"
sc.camera = cam
ctr = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (z0 + mx.z) / 2))
span_h = (mx.z - z0) * 1.12
span_w = max(mx.x - mn.x, mx.y - mn.y) * 1.12
cam.data.ortho_scale = max(span_w, span_h * RW / RH) if RW >= RH else max(span_h, span_w * RH / RW)
cam.data.clip_end = 100 * size

tiles = []
for az, el in VIEWS:
    azr, elr = math.radians(az), math.radians(el)
    d = Vector((math.sin(azr) * math.cos(elr), -math.cos(azr) * math.cos(elr), math.sin(elr)))
    cam.location = ctr + d * 10 * size
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
    # the key light: upper left front of each view, so every tile reads the same way
    side = Vector((math.cos(azr), math.sin(azr), 0))
    key.location = ctr + (d * 3.8 - side * 2.6 + Vector((0, 0, 3.0))) * size
    key.rotation_euler = (ctr - key.location).to_track_quat("-Z", "Y").to_euler()
    path = OUT[:-4] + "_%d_%d.png" % (az, el)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    tiles.append(bi.on_white(bi.load(path)))
    os.remove(path)
cols = 4 if len(tiles) > 4 else len(tiles)
rows = []
for i in range(0, len(tiles), cols):
    ts = tiles[i:i + cols]
    while len(ts) < cols:
        ts.append(np.ones_like(tiles[0]))
    parts = []
    for t in ts:
        parts += [t, np.full((RH, 6, 4), 0.75, dtype=np.float32)]
    rows += [np.concatenate(parts[:-1], axis=1)]
    rows += [np.full((6, rows[-1].shape[1], 4), 0.75, dtype=np.float32)]
sheet = np.concatenate(rows[:-1], axis=0)
sheet[..., 3] = 1
bi.save(OUT, sheet)
print("SHEET", OUT, sheet.shape[1], sheet.shape[0])
