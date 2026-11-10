# Before / after the knight's face is stripped: brow and cheek shells removed | carved eyes cut out and refilled.
# Rows: front, three-quarter, side, other three-quarter. blender -b -P knight_clean_test.py -- <out.png>
import bpy, os, sys, math
import numpy as np
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tripo_clean as tc

OUT = sys.argv[sys.argv.index("--") + 1]
RES = 440
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/trews/Downloads/knight.fbx")
ms = [o for o in bpy.data.objects if o.type == "MESH"]
for o in [o for o in bpy.data.objects if o.type != "MESH"]:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action="DESELECT")
for o in ms:
    o.parent = None; o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if len(ms) > 1:
    bpy.ops.object.join()
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.separate(type="LOOSE"); bpy.ops.object.mode_set(mode="OBJECT")
parts = [p for p in bpy.context.selected_objects if p.type == "MESH"]
zr = lambda p: np.ptp([v.co.z for v in p.data.vertices])
body = max(parts, key=zr)
for p in tc.face_shells(parts, body):
    bpy.data.objects.remove(p, do_unlink=True)
parts = [p for p in bpy.data.objects if p.type == "MESH"]
z0 = min(v.co.z for v in body.data.vertices)
for p in parts:
    p.data.transform(Matrix.Translation(Vector((0, 0, -z0))))
    for f in p.data.polygons:
        f.use_smooth = False
before = body.copy(); before.data = body.data.copy(); bpy.context.scene.collection.objects.link(before)
boxes, nnm = tc.carved_boxes(body)
print("BOXES", nnm, "non-manifold edges;", [(lo.round(3).tolist(), hi.round(3).tolist()) for lo, hi in boxes])
nf, left = tc.refill(body, boxes)
print("CLEAN", nf, "faired verts,", left, "non-manifold edges left")

m = bpy.data.materials.new("cream"); m.use_nodes = True
b = m.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1); b.inputs["Roughness"].default_value = 0.5
for p in parts + [before]:
    p.data.materials.clear(); p.data.materials.append(m)
    for f in p.data.polygons:
        f.use_smooth = False
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
sc.cycles.samples = 48; sc.cycles.use_denoising = True
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
wd = bpy.data.worlds.new("studio"); wd.use_nodes = True
wd.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.86, 0.70, 1)
wd.node_tree.nodes["Background"].inputs[1].default_value = 0.55
sc.world = wd
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"; key.data.size = 3.0; key.data.energy = 170
sc.collection.objects.link(key)
key.location = Vector((-2.2, -3.2, 3.0))
key.rotation_euler = (Vector((0, 0, 0.6)) - key.location).to_track_quat("-Z", "Y").to_euler()
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam); cam.data.lens = 100; sc.camera = cam
target = Vector((0, -0.05, 0.70))
VIEWS = (0, -35, -90, 35)
tiles = {d: [] for d in VIEWS}
for col in range(2):
    before.hide_render = col == 1
    body.hide_render = col == 0
    for deg in VIEWS:
        az, el = math.radians(deg), math.radians(4)
        cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * 2.0
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(os.path.dirname(OUT), "clean_tmp.png")
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(path)
        x = np.array(im.pixels[:], dtype=np.float32).reshape(RES, RES, 4)
        bpy.data.images.remove(im)
        al = x[..., 3:4]
        tiles[deg].append(np.concatenate([np.clip(x[..., :3] + (1 - al), 0, 1), np.ones_like(al)], axis=2))
rows = []
for d in VIEWS:
    rows.append(np.concatenate([tiles[d][0], np.full((RES, 6, 4), 0.8, dtype=np.float32), tiles[d][1]], axis=1))
stack = []
for r in reversed(rows):
    stack += [r, np.full((6, rows[0].shape[1], 4), 0.8, dtype=np.float32)]
sheet = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel(); img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("SHEET", OUT)
