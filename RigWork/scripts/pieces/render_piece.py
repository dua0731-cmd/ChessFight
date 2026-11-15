# A piece .blend under the studio light (as render_studio.py), on white, several views; then a sheet with the design
# images on top and the renders below. blender -b -P render_piece.py -- <piece.blend> <out dir> <tag> <view:deg>...
#   [--ref <image> ...] (one per view, same order)
import bpy, sys, os, math
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
BLEND, OUT, TAG = a[0], a[1], a[2]
rest = a[3:]
refs = []
if "--ref" in rest:
    i = rest.index("--ref")
    refs = rest[i + 1:]
    rest = rest[:i]
VIEWS = [(v.split(":")[0], float(v.split(":")[1])) for v in rest]
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=BLEND)
piece = [o for o in bpy.data.objects if o.type == "MESH"][0]
mat = bpy.data.materials.new("Wood")
mat.use_nodes = True
b = mat.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1)
b.inputs["Roughness"].default_value = 0.5
for o in bpy.data.objects:
    if o.type == "MESH":
        o.data.materials.clear()
        o.data.materials.append(mat)
vs = [o.matrix_world @ v.co for o in bpy.data.objects if o.type == "MESH" for v in o.data.vertices]
zmin, zmax = min(p.z for p in vs), max(p.z for p in vs)

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
sc.cycles.samples = 96
sc.cycles.use_denoising = True
sc.render.resolution_x = sc.render.resolution_y = 627
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.86, 0.70, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
sc.world = world
bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, zmin))
bpy.context.active_object.is_shadow_catcher = True
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"; key.data.size = 3.0; key.data.energy = 170
sc.collection.objects.link(key)
key.location = Vector((-2.6, -3.8, 3.4))
key.rotation_euler = (Vector((0, 0, 0.6)) - key.location).to_track_quat("-Z", "Y").to_euler()
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
cam.data.lens = 100
sc.camera = cam
span = (zmax - zmin) * 1.18
dist = span / 0.36
target = Vector((0, 0, (zmin + zmax) / 2))

tiles = []
for name, deg in VIEWS:
    az, el = math.radians(deg), math.radians(5)
    cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, "%s_%s.png" % (TAG, name))
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    al = x[..., 3:4]
    tiles.append(np.concatenate([np.clip(x[..., :3] + (1 - al), 0, 1), np.ones_like(al)], axis=2))

def load_ref(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    ys = (np.arange(627) * h / 627).astype(int)
    xs = (np.arange(627) * w / 627).astype(int)
    return x[ys][:, xs]

grey = np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
def row(ts):
    parts = []
    for t in ts:
        parts += [t, np.ones((627, 8, 4), dtype=np.float32) * grey]
    return np.concatenate(parts[:-1], axis=1)
rows = [row(tiles)]
if refs:
    rows = [row(tiles), np.ones((8, row(tiles).shape[1], 4), dtype=np.float32) * grey, row([load_ref(p) for p in refs])]
sheet = np.concatenate(rows, axis=0)
img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = os.path.join(OUT, "%s_sheet.png" % TAG)
img.file_format = "PNG"
img.save()
print("SHEET", os.path.join(OUT, "%s_sheet.png" % TAG))
