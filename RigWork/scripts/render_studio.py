# The rigged pawn under soft studio light like the concept renders (Cycles): a big soft key light, a bright white
# surround that fills every shadow, a white floor that only catches soft shadows, the reference image's cream.
# blender -b -P render_studio.py -- <out.png> <Pawn_Rigged.blend> <view: front|three> [team white|black] [key W] [fill]
import bpy, sys, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
OUT, BLEND, VIEW = a[0], a[1], a[2]
TEAM = a[3] if len(a) > 3 else "white"
KEY = float(a[4]) if len(a) > 4 else 900.0
FILL = float(a[5]) if len(a) > 5 else 0.75
FILL_RGB = tuple(float(v) for v in a[6].split(",")) if len(a) > 6 else (1.0, 1.0, 1.0)

SKIN, HEADC, CHEEK = (0.945, 0.808, 0.702), (0.898, 0.753, 0.649), (0.91, 0.59, 0.53)
EYE, GLINT, HAND, FOOT = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
REF_CREAM = (0.97, 0.86, 0.73)      # a little under the reference's lit median (0.996, 0.898, 0.792): light adds the rest
CHARCOAL, IVORY = (0.30, 0.30, 0.31), (0.95, 0.91, 0.79)

def near(c, ref, tol=0.04):
    return max(abs(c[i] - ref[i]) for i in range(3)) < tol

bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
piece = bpy.data.objects["Pawn"]
body = REF_CREAM if TEAM == "white" else CHARCOAL
attr = piece.data.color_attributes["Col"]
for d in attr.data:
    c = d.color_srgb[:3]
    if near(c, SKIN) or near(c, HEADC):
        new = body
    elif near(c, EYE, 0.03):
        new = (0.02, 0.018, 0.018) if TEAM == "white" else IVORY
    elif near(c, GLINT, 0.03):
        new = (0.97, 0.97, 0.97) if TEAM == "white" else IVORY
    elif near(c, HAND) or near(c, FOOT):
        new = (0.035, 0.032, 0.032)
    else:
        w = max(0.0, min(1.0, sum((c[i] - HEADC[i]) * (CHEEK[i] - HEADC[i]) for i in range(3)) /
                         sum((CHEEK[i] - HEADC[i]) ** 2 for i in range(3))))
        new = tuple(body[i] + ((0.93, 0.55, 0.52)[i] - body[i]) * w for i in range(3))
    d.color_srgb = (*new, 1.0)

m = bpy.data.materials.new("Studio")
m.use_nodes = True
nt = m.node_tree
bsdf = nt.nodes["Principled BSDF"]
col = nt.nodes.new("ShaderNodeVertexColor")
col.layer_name = "Col"
nt.links.new(col.outputs["Color"], bsdf.inputs["Base Color"])
# satin wood on the light parts, a little glossier on the dark ones (eyes, hands, feet)
lum = nt.nodes.new("ShaderNodeRGBToBW")
rng = nt.nodes.new("ShaderNodeMapRange")
rng.inputs["From Min"].default_value, rng.inputs["From Max"].default_value = 0.02, 0.4
rng.inputs["To Min"].default_value, rng.inputs["To Max"].default_value = 0.45, 0.55
nt.links.new(col.outputs["Color"], lum.inputs[0])
nt.links.new(lum.outputs[0], rng.inputs["Value"])
nt.links.new(rng.outputs["Result"], bsdf.inputs["Roughness"])
piece.data.materials.clear()
piece.data.materials.append(m)

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
sc.cycles.samples = 96
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 640, 820
sc.view_settings.view_transform = "Standard"
sc.render.film_transparent = True

# the white surround: fills the shadows evenly, like a photo studio
world = bpy.data.worlds.new("studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (*FILL_RGB, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = FILL
sc.world = world

# a floor that only shows the soft shadows
bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
floor = bpy.context.active_object
floor.is_shadow_catcher = True

# the key: a big soft light from the upper left front
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"
key.data.size = 3.0
key.data.energy = KEY
sc.collection.objects.link(key)
key.location = Vector((-2.2, -3.2, 3.0))
key.rotation_euler = (Vector((0, 0, 0.5)) - key.location).to_track_quat("-Z", "Y").to_euler()

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
cam.data.lens = 85
az = math.radians(0 if VIEW == "front" else -30)
el = math.radians(5)
target = Vector((0, 0, 0.52))
dist = 4.3
cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * dist
cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam

sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)

# onto a white page, as the concept sheet is
import numpy as np
im = bpy.data.images.load(OUT)
w, h = im.size
x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
alpha = x[..., 3:4]
rgb = x[..., :3] + (1.0 - alpha) * 1.0
out = np.concatenate([np.clip(rgb, 0, 1), np.ones_like(alpha)], axis=2)
im2 = bpy.data.images.new("white", w, h, alpha=True)
im2.pixels = out.ravel()
im2.filepath_raw = OUT
im2.file_format = "PNG"
im2.save()
print("STUDIO", OUT, sc.cycles.device)
