# Rigged pieces at rest and in a test pose (chest bent forward, head nodding and turning, one arm raised, the other
# swung forward, one foot stepping out), under the studio light, front and three-quarter.
# blender -b -P render_poses.py -- <out.png> <Rigged.blend> ...
import bpy, sys, os, math
import numpy as np
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
OUT, BLENDS = a[0], a[1:]
P = "mixamorig:"

def rot_about(arm, name, pivot, R):
    pb = arm.pose.bones[P + name]
    Mw = arm.matrix_world
    pb.matrix = Mw.inverted() @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ (Mw @ pb.matrix))
    bpy.context.view_layer.update()

def stage(sc, zmin, zmax):
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
    sc.render.resolution_x, sc.render.resolution_y = 460, 560
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
    key.rotation_euler = (Vector((0, 0, 0.6)) - key.location).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.lens = 85
    sc.camera = cam
    return cam

tiles_rows = []
for blend in BLENDS:
    bpy.ops.wm.open_mainfile(filepath=blend)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    piece = [o for o in bpy.data.objects if o.type == "MESH"][0]
    arm.animation_data_clear()
    arm.data.pose_position = "POSE"
    mat = bpy.data.materials.new("Wood")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1)
    b.inputs["Roughness"].default_value = 0.5
    # the limbs dark, as on the pieces: vertices on hand or foot bones
    dark = bpy.data.materials.new("Dark")
    dark.use_nodes = True
    dark.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.03, 0.028, 0.028, 1)
    dark.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.35
    piece.data.materials.clear()
    piece.data.materials.append(mat)
    piece.data.materials.append(dark)
    hf = {g.index for g in piece.vertex_groups if "Hand" in g.name or "Foot" in g.name}
    darkv = {v.index for v in piece.data.vertices if sum(g.weight for g in v.groups if g.group in hf) > 0.5}
    for p in piece.data.polygons:
        p.material_index = 1 if all(i in darkv for i in p.vertices) else 0
    vs = [piece.matrix_world @ v.co for v in piece.data.vertices]
    zmin, zmax = min(v.z for v in vs), max(v.z for v in vs)
    sc = bpy.context.scene
    cam = stage(sc, zmin, zmax)
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
    bpy.context.active_object.is_shadow_catcher = True
    span = (zmax + 0.05) * 1.25
    dist = span / (36 / 85) * 0.8
    target = Vector((0, 0, (zmax + 0.0) / 2))
    W = arm.matrix_world
    head_of = lambda n: W @ arm.data.bones[P + n].head_local
    row = []
    for posed in (False, True):
        for p in arm.pose.bones:
            p.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        if posed:
            rot_about(arm, "Spine2", head_of("Spine2"), Matrix.Rotation(math.radians(-16), 4, "X"))
            neck = W @ arm.pose.bones[P + "Head"].head
            rot_about(arm, "Head", neck, Matrix.Rotation(math.radians(20), 4, "Z") @ Matrix.Rotation(math.radians(-18), 4, "X"))
            la = W @ arm.pose.bones[P + "LeftArm"].head
            rot_about(arm, "LeftArm", la, Matrix.Rotation(math.radians(65), 4, "Y"))
            ra = W @ arm.pose.bones[P + "RightArm"].head
            rot_about(arm, "RightArm", ra, Matrix.Rotation(math.radians(-55), 4, "X"))
            lf = arm.pose.bones[P + "LeftFoot"]
            lf.matrix = W.inverted() @ (Matrix.Translation(Vector((0, -0.12, 0.05))) @ (W @ lf.matrix))
            bpy.context.view_layer.update()
        for deg in (0, -35):
            az, el = math.radians(deg), math.radians(6)
            cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * dist
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            path = os.path.join(os.path.dirname(OUT), "pose_tmp.png")
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(path)
            w, h = im.size
            x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
            bpy.data.images.remove(im)
            al = x[..., 3:4]
            row.append(np.concatenate([np.clip(x[..., :3] + (1 - al), 0, 1), np.ones_like(al)], axis=2))
    print("POSED", blend)
    tiles_rows.append(row)

grey = np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
rows = []
for row in tiles_rows:
    parts = []
    for t in row:
        parts += [t, np.ones((t.shape[0], 6, 4), dtype=np.float32) * grey]
    rows.append(np.concatenate(parts[:-1], axis=1))
stack = []
for r in reversed(rows):
    stack += [r, np.ones((6, rows[0].shape[1], 4), dtype=np.float32) * grey]
sheet = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = OUT
img.file_format = "PNG"
img.save()
print("SHEET", OUT)
