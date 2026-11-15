# The Tripo rook's and knight's face pieces (separate loose shells on the front of the head):
# columns  as delivered | face pieces in red (other loose shells blue) | face pieces removed
# rows     rook front, rook 3/4, knight front, knight 3/4. Normalised Tripo units, studio light.
# blender -b -P render_face_parts.py -- <out.png>
import bpy, os, sys, math
import numpy as np
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
RES = 420
TARGET = {"rook": 0.56, "knight": 0.70}

def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath="C:/Users/trews/Downloads/" + name + ".fbx")
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
    span = lambda p: max(v.co.z for v in p.data.vertices) - min(v.co.z for v in p.data.vertices)
    body = max(parts, key=span)
    z0 = min(v.co.z for v in body.data.vertices)
    for p in parts:
        p.data.transform(__import__("mathutils").Matrix.Translation(Vector((0, 0, -z0))))
        for f in p.data.polygons:
            f.use_smooth = False
    face = [p for p in parts if p is not body and min(v.co.y for v in p.data.vertices) < -0.04]
    other = [p for p in parts if p is not body and p not in face]
    return body, face, other

def mat(name, rgb, rough=0.5):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (rgb[0] ** 2.2, rgb[1] ** 2.2, rgb[2] ** 2.2, 1)
    b.inputs["Roughness"].default_value = rough
    return m

def stage():
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
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("studio"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.86, 0.70, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    sc.world = w
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    key.data.shape = "DISK"; key.data.size = 3.0; key.data.energy = 170
    sc.collection.objects.link(key)
    key.location = Vector((-2.2, -3.2, 3.0))
    key.rotation_euler = (Vector((0, 0, 0.6)) - key.location).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam); cam.data.lens = 100; sc.camera = cam
    return sc, cam

rows = []
for name in ("rook", "knight"):
    body, face, other = load(name)
    print("FACE", name, len(face), "face shells,", len(other), "other shells")
    cream, red, blue = mat("cream", (0.97, 0.86, 0.73)), mat("red", (0.90, 0.15, 0.12)), mat("blue", (0.35, 0.60, 0.95))
    sc, cam = stage()
    target = Vector((0, 0, TARGET[name]))
    tiles = {0: [], -35: []}
    for col in range(3):
        for p in [body] + face + other:
            p.data.materials.clear()
            p.data.materials.append(cream if (col == 0 or p is body) else (red if p in face else blue))
            if col == 2 and p in other:
                p.data.materials[0] = cream
            p.hide_render = col == 2 and p in face
        for deg in (0, -35):
            az, el = math.radians(deg), math.radians(4)
            cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * 3.4
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            path = os.path.join(os.path.dirname(OUT), "face_tmp.png")
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(path)
            x = np.array(im.pixels[:], dtype=np.float32).reshape(RES, RES, 4)
            bpy.data.images.remove(im)
            al = x[..., 3:4]
            tiles[deg].append(np.concatenate([np.clip(x[..., :3] + (1 - al), 0, 1), np.ones_like(al)], axis=2))
    for deg in (0, -35):
        parts = []
        for t in tiles[deg]:
            parts += [t, np.full((RES, 6, 4), 0.8, dtype=np.float32)]
        rows.append(np.concatenate(parts[:-1], axis=1))
stack = []
for r in reversed(rows):
    stack += [r, np.full((6, rows[0].shape[1], 4), 0.8, dtype=np.float32)]
sheet = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel(); img.filepath_raw = OUT; img.file_format = "PNG"; img.save()
print("SHEET", OUT)
