# The user's Tripo pieces against the low-poly rebuilds, one sheet per piece, same studio light and cameras:
#   row 1  Tripo as delivered (its own smooth normals)
#   row 2  Tripo with flat faces (what its topology gives as low-poly)
#   row 3  the low-poly rebuild
# columns: front, side (facing left), three-quarter. The Tripo model is scaled to the rebuild's height and stood on
# the same base (z 0.12), centred on its base. blender -b -P compare_tripo.py -- <out dir> <pieces dir> <piece>...
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
OUT, PDIR, NAMES = a[0], a[1], a[2:]
os.makedirs(OUT, exist_ok=True)
DL = "C:/Users/trews/Downloads/"
VIEWS = [("front", 0), ("side", 90), ("three", 35)]
RES = 520

def import_tripo(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=DL + name + ".fbx")
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    ms = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in ms:
        o.parent = None
    bpy.ops.object.select_all(action="DESELECT")
    for o in ms:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(ms) > 1:
        bpy.ops.object.join()
    return bpy.context.view_layer.objects.active

def fit(o, zmin, zmax):
    vs = [v.co for v in o.data.vertices]
    mn = min(p.z for p in vs)
    mx = max(p.z for p in vs)
    low = [p for p in vs if p.z < mn + 0.05 * (mx - mn)]
    bx = (min(p.x for p in low) + max(p.x for p in low)) / 2
    by = (min(p.y for p in low) + max(p.y for p in low)) / 2
    k = (zmax - zmin) / (mx - mn)
    o.data.transform(Matrix.Translation(Vector((0, 0, zmin))) @ Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-bx, -by, -mn))))

def flat_faces(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    if o.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for p in o.data.polygons:
        p.use_smooth = False

def stage(zmax):
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
    sc.cycles.samples = 64
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.86, 0.70, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    sc.world = world
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0.12))
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
    return sc, cam

grey = np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
for name in NAMES:
    Name = name.capitalize()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(os.path.join(PDIR, Name + "_LowPoly.blend")) as (src, dst):
        dst.objects = [n for n in src.objects]
    ours = [o for o in dst.objects if o.type == "MESH"][0]
    bpy.context.scene.collection.objects.link(ours)
    vs = [v.co for v in ours.data.vertices]
    zmin, zmax = min(p.z for p in vs), max(p.z for p in vs)
    smooth_t = import_tripo(name)
    fit(smooth_t, zmin, zmax)
    flat_t = smooth_t.copy()
    flat_t.data = smooth_t.data.copy()
    bpy.context.scene.collection.objects.link(flat_t)
    flat_faces(flat_t)
    mat = bpy.data.materials.new("Wood")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1)
    b.inputs["Roughness"].default_value = 0.5
    for o in (smooth_t, flat_t, ours):
        o.data.materials.clear()
        o.data.materials.append(mat)
    sc, cam = stage(zmax)
    span = (zmax - 0.12) * 1.2
    dist = span / 0.36
    target = Vector((0, 0, (zmax + 0.12) / 2))
    rows = []
    for obj in (smooth_t, flat_t, ours):
        for o in (smooth_t, flat_t, ours):
            o.hide_render = o is not obj
        row = []
        for vname, deg in VIEWS:
            az, el = math.radians(deg), math.radians(5)
            cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * dist
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            path = os.path.join(OUT, "tmp.png")
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(path)
            w, h = im.size
            x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
            bpy.data.images.remove(im)
            al = x[..., 3:4]
            row.append(np.concatenate([np.clip(x[..., :3] + (1 - al), 0, 1), np.ones_like(al)], axis=2))
        parts = []
        for t in row:
            parts += [t, np.ones((RES, 8, 4), dtype=np.float32) * grey]
        rows.append(np.concatenate(parts[:-1], axis=1))
    stack = []
    for r in reversed(rows):
        stack += [r, np.ones((8, rows[0].shape[1], 4), dtype=np.float32) * grey]
    sheet = np.concatenate(stack[:-1], axis=0)
    img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = os.path.join(OUT, "tripo_vs_ours_%s.png" % name)
    img.file_format = "PNG"
    img.save()
    print("SHEET", name, len(smooth_t.data.polygons), "tripo faces vs", len(ours.data.polygons), "ours")
