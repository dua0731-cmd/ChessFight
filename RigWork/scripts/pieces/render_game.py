# Renders the game meshes (game_<Piece>.blend from game_export.py: eyes joined, one colour stripe per face) with their
# stripe atlases, white and black team, through a design-like camera, and puts them beside a design image.
# blender -b --factory-startup -P render_game.py -- <game .blend> <white atlas.png> <black atlas.png> <out.png>
#   --rows top,bottom --z top,bottom --size N --cam z,dist --views front,left,back [--tile N] [--crop w]
#   [--design img@axis:top[@x0,y0,x1,y1],...] (one per view, '-' for none; the crop is cut from the image first)
import bpy, sys, os, math
import numpy as np
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
BLEND, AT_W, AT_B, OUT = a[:4]
def opt(name, default):
    return a[a.index(name) + 1] if name in a else default
ROW_TOP, ROW_BOT = [float(v) for v in opt("--rows", "44,1167").split(",")]
Z_TOP, Z_BOT = [float(v) for v in opt("--z", "1.447,0.12").split(",")]
SIZE = int(opt("--size", "1254"))
CAM_Z, CAM_D = [float(v) for v in opt("--cam", "1.2,13.5").split(",")]
VIEWS = opt("--views", "front").split(",")
TILE = int(opt("--tile", "627"))
CROP = int(opt("--crop", str(SIZE)))               # keep this many columns about the axis
DESIGN = opt("--design", "").split(";") if "--design" in a else []      # one per view, separated by ';'
S = (Z_TOP - Z_BOT) / (ROW_BOT - ROW_TOP)
AZ = dict(front=0.0, left=90.0, back=180.0, right=270.0, q34=35.0)

bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
piece = [o for o in bpy.data.objects if o.type == "MESH"][0]

def material(atlas):
    m = bpy.data.materials.new("Team")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(atlas)
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.5
    return m
MATS = dict(white=material(AT_W), black=material(AT_B))

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
sc.render.resolution_x = sc.render.resolution_y = SIZE
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.95, 0.88, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.75
sc.world = world
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
bpy.context.active_object.is_shadow_catcher = True
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"
key.data.size = 4.0
key.data.energy = 220
sc.collection.objects.link(key)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
zc = Z_BOT + (ROW_BOT - SIZE / 2.0) * S
target = Vector((0, 0, zc))
dist = math.hypot(CAM_D, CAM_Z - zc)
cam.data.sensor_width = 36.0
cam.data.lens = 36.0 * dist / (SIZE * S)
cam.data.clip_end = 100

def cut(img):
    x0 = (img.shape[1] - CROP) // 2
    t = img[:, x0:x0 + CROP]
    k = SIZE // TILE
    return bi.shrink(t, k) if k > 1 else t

rows = []
for i, name in enumerate(VIEWS):
    az = math.radians(AZ[name])
    d = Vector((math.sin(az), -math.cos(az), 0))
    right = Vector((math.cos(az), math.sin(az), 0))
    cam.location = Vector((0, 0, CAM_Z)) + d * CAM_D
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    key.location = target + d * 4.5 + right * 2.0 + Vector((0, 0, 4.0))
    key.rotation_euler = (target + Vector((0, 0, 0.1)) - key.location).to_track_quat("-Z", "Y").to_euler()
    tiles = []
    if i < len(DESIGN) and DESIGN[i] != "-":
        bits = DESIGN[i].split("@")
        des = bi.load(bits[0])
        if len(bits) > 2:
            x0, y0, x1, y1 = [int(v) for v in bits[2].split(",")]
            des = des[y0:y1, x0:x1]
        axis, top = [float(v) for v in bits[1].split(":")]
        # onto a SIZE page with its axis in the middle and its top row at ROW_TOP
        page = np.ones((SIZE, SIZE, 4), dtype=np.float32)
        page[..., :3] = des[2, 2, :3]
        dx, dy = int(round(SIZE / 2 - axis)), int(round(ROW_TOP - top))
        h, w = des.shape[:2]
        sx0, sy0 = max(0, -dx), max(0, -dy)
        tx0, ty0 = max(0, dx), max(0, dy)
        ww, hh = min(w - sx0, SIZE - tx0), min(h - sy0, SIZE - ty0)
        page[ty0:ty0 + hh, tx0:tx0 + ww] = des[sy0:sy0 + hh, sx0:sx0 + ww]
        page[..., 3] = 1
        tiles.append(cut(page))
    for team in ("white", "black"):
        piece.data.materials.clear()
        piece.data.materials.append(MATS[team])
        tmp = OUT[:-4] + "_%s_%s.png" % (name, team)
        sc.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        tiles.append(cut(bi.on_white(bi.load(tmp))))
        os.remove(tmp)
    gap = np.full((tiles[0].shape[0], 6, 4), 0.75, dtype=np.float32)
    parts = []
    for t in tiles:
        parts += [t, gap]
    rows.append(np.concatenate(parts[:-1], axis=1))
    rows.append(np.full((6, rows[-1].shape[1], 4), 0.75, dtype=np.float32))
sheet = np.concatenate(rows[:-1], axis=0)
sheet[..., 3] = 1
bi.save(OUT, sheet)
print("GAMEVIEW", OUT, sheet.shape[1], sheet.shape[0])
