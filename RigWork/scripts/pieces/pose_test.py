# Rigged pieces at rest and in two test poses, with the arms turned the way the game turns them: about the piece's
# own shoulder mark (RagdollVisualSync.PoseAsPiece; PieceModelBaker.Mark puts it 12 mm out from the innermost vertex
# that is mostly on the arm bone, on the stub's middle line), carried by the chest. render_poses.py turns the arm
# bone about the pawn's own shoulder joint instead, which the game never does to a worn piece: there a piece whose
# arms sit above the pawn's (bishop, king, queen) seems to lose its arm.
#   calm  - chest bent 16 degrees, head turned 20 and nodding 18, one arm down 30 and the other forward 30 degrees
#           (30 is the most the game turns a calm arm)
#   reach - the same body, one arm up 65 and the other forward 55 degrees (grabbing, climbing)
# Tiles per piece: rest front, calm front, calm three-quarter, reach front, reach three-quarter.
# blender -b --factory-startup -P pose_test.py -- <out.png> <Rigged or game .blend> ...
import bpy, sys, os, math
import numpy as np
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
OUT, BLENDS = a[0], a[1:]
P = "mixamorig:"
RES = (460, 600)

def rot_about(arm, name, pivot, R):
    pb = arm.pose.bones[P + name]
    Mw = arm.matrix_world
    pb.matrix = Mw.inverted() @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ (Mw @ pb.matrix))
    bpy.context.view_layer.update()

def shoulder_mark(piece, side):
    """As PieceModelBaker.Mark: from the vertices whose strongest bone is this arm (at least half)."""
    gi = piece.vertex_groups[P + side + "Arm"].index
    pts = []
    for v in piece.data.vertices:
        if not v.groups:
            continue
        top = max(v.groups, key=lambda g: g.weight)
        if top.group == gi and top.weight >= 0.5:
            pts.append(piece.matrix_world @ v.co)
    arm_in = min(abs(p.x) for p in pts)
    sg = 1.0 if sum(p.x for p in pts) > 0 else -1.0
    ys, zs = [p.y for p in pts], [p.z for p in pts]
    return Vector((sg * (arm_in + 0.012), (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))

rows = []
for blend in BLENDS:
    bpy.ops.wm.open_mainfile(filepath=blend)
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    piece = [o for o in bpy.data.objects if o.type == "MESH"][0]
    arm.animation_data_clear()
    arm.data.pose_position = "POSE"
    wood = bpy.data.materials.new("Wood")
    wood.use_nodes = True
    wood.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1)
    wood.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.5
    dark = bpy.data.materials.new("Dark")
    dark.use_nodes = True
    dark.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.03, 0.028, 0.028, 1)
    dark.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.35
    piece.data.materials.clear()
    piece.data.materials.append(wood)
    piece.data.materials.append(dark)
    hf = {g.index for g in piece.vertex_groups if "Hand" in g.name or "Foot" in g.name}
    darkv = {v.index for v in piece.data.vertices if sum(g.weight for g in v.groups if g.group in hf) > 0.5}
    for p in piece.data.polygons:
        p.material_index = 1 if all(i in darkv for i in p.vertices) else 0
    vs = [piece.matrix_world @ v.co for v in piece.data.vertices]
    zmax = max(v.z for v in vs)
    marks = {s: shoulder_mark(piece, s) for s in ("Left", "Right")}
    print("MARKS", os.path.basename(blend), {s: [round(c, 3) for c in m] for s, m in marks.items()})

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
    sc.cycles.samples = 64
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = RES
    sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.95, 0.88, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.7
    sc.world = world
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    key.data.shape = "DISK"
    key.data.size = 3.0
    key.data.energy = 200
    sc.collection.objects.link(key)
    key.location = Vector((-2.2, -3.2, 3.0))
    key.rotation_euler = (Vector((0, 0, 0.6)) - key.location).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.lens = 85
    sc.camera = cam
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
    bpy.context.active_object.is_shadow_catcher = True
    span = (zmax + 0.05) * 1.3
    dist = span / (36 / 85) * 0.8
    target = Vector((0, 0, zmax / 2))
    W = arm.matrix_world
    rest = {n: W @ arm.data.bones[P + n].matrix_local for n in ("Spine2",)}

    def pose(kind):
        for p in arm.pose.bones:
            p.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        if kind == "rest":
            return
        s2 = W @ arm.data.bones[P + "Spine2"].head_local
        rot_about(arm, "Spine2", s2, Matrix.Rotation(math.radians(-16), 4, "X"))
        neck = W @ arm.pose.bones[P + "Head"].head
        rot_about(arm, "Head", neck, Matrix.Rotation(math.radians(20), 4, "Z") @ Matrix.Rotation(math.radians(-18), 4, "X"))
        # the chest's move carries the shoulder marks
        D = (W @ arm.pose.bones[P + "Spine2"].matrix) @ rest["Spine2"].inverted()
        left, right = (-30, 30) if kind == "calm" else (65, 55)
        rot_about(arm, "LeftArm", D @ marks["Left"], D.to_3x3().to_4x4() @ Matrix.Rotation(math.radians(-left), 4, "Y") @ D.to_3x3().to_4x4().inverted())
        rot_about(arm, "RightArm", D @ marks["Right"], D.to_3x3().to_4x4() @ Matrix.Rotation(math.radians(right), 4, "Z") @ D.to_3x3().to_4x4().inverted())

    row = []
    for kind, degs in (("rest", (0,)), ("calm", (0, -35)), ("reach", (0, -35))):
        pose(kind)
        for deg in degs:
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
            row.append(np.concatenate([np.clip(x[..., :3] * al + (1 - al), 0, 1), np.ones_like(al)], axis=2))
    rows.append(row)

grey = np.array([0.8, 0.8, 0.8, 1], dtype=np.float32)
strips = []
for row in rows:
    parts = []
    for t in row:
        parts += [t, np.ones((t.shape[0], 6, 4), dtype=np.float32) * grey]
    strips.append(np.concatenate(parts[:-1], axis=1))
stack = []
for r in reversed(strips):
    stack += [r, np.ones((6, strips[0].shape[1], 4), dtype=np.float32) * grey]
sheet = np.concatenate(stack[:-1], axis=0)
img = bpy.data.images.new("s", sheet.shape[1], sheet.shape[0], alpha=True)
img.pixels = sheet.ravel()
img.filepath_raw = OUT
img.file_format = "PNG"
img.save()
print("SHEET", OUT, sheet.shape[1], sheet.shape[0])
