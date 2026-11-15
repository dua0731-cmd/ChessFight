# Renders a model through the design images' own camera (a long lens a little above the piece's middle, key light
# from the upper right) and puts each view beside its design image with an outline overlay; prints how far the
# silhouettes are apart. Used for the bishop, king and queen.
# blender -b --factory-startup -P bishop_compare.py -- <model.blend> <out.png> --views front=<img>@<axis>:<top>,...
#   [--rows 44,1167] [--z 1.447,0.12] [--size 1254] [--cam 1.2,13.5] [--tile 627] [--floor 0.12] [--hide name,...]
#   view names: front left right back. <axis>:<top> = the design's axis column and top row (to line the render up).
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bishop_img as bi

a = sys.argv[sys.argv.index("--") + 1:]
MODEL, OUT = a[0], a[1]
def opt(name, default):
    return a[a.index(name) + 1] if name in a else default
VIEWS = []
for v in opt("--views", "").split(","):
    name, rest = v.split("=", 1)
    path, al = rest.rsplit("@", 1)
    VIEWS.append((name, path, float(al.split(":")[0]), float(al.split(":")[1])))
ROW_TOP, ROW_BOT = [float(v) for v in opt("--rows", "44,1167").split(",")]
Z_TOP, Z_BOT = [float(v) for v in opt("--z", "1.447,0.12").split(",")]
SIZE = int(opt("--size", "1254"))
CAM_Z, CAM_D = [float(v) for v in opt("--cam", "1.2,13.5").split(",")]
TILE = int(opt("--tile", "627"))
FLOOR = float(opt("--floor", str(Z_BOT)))
S = (Z_TOP - Z_BOT) / (ROW_BOT - ROW_TOP)
AZ = dict(front=0.0, left=90.0, back=180.0, right=270.0)
# --table left:80:640:14  prints design and render runs of that view, rows from the top, every 14th
TABLE = {}
for i, v in enumerate(a):
    if v == "--table":
        n, r0, r1, st = a[i + 1].split(":")
        TABLE[n] = (int(r0), int(r1), int(st))
OVERLAY = "--overlay" in a                         # also save full-size overlays and renders per view
KEY = [float(v) for v in opt("--key", "3.8,2.6,3.0,170").split(",")]      # towards camera, right, up, watts
WORLD = [float(v) for v in opt("--world", "1.0,0.86,0.70,0.55").split(",")]
SHADOW = "--no-shadow" not in a
INK = "--ink" in a                                 # the design's mask: all that is not the white page

bpy.ops.wm.open_mainfile(filepath=MODEL)
for o in list(bpy.data.objects):
    if o.type in ("CAMERA", "LIGHT"):
        bpy.data.objects.remove(o, do_unlink=True)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
mat = bpy.data.materials.new("Wood")
mat.use_nodes = True
b = mat.node_tree.nodes["Principled BSDF"]
b.inputs["Base Color"].default_value = (0.97 ** 2.2, 0.86 ** 2.2, 0.73 ** 2.2, 1)
b.inputs["Roughness"].default_value = 0.5
for o in meshes:
    if not o.data.materials:
        o.data.materials.append(mat)
    else:
        for i in range(len(o.data.materials)):
            if o.data.materials[i] is None or o.data.materials[i].name.startswith("Material"):
                o.data.materials[i] = mat

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
world.node_tree.nodes["Background"].inputs[0].default_value = (WORLD[0], WORLD[1], WORLD[2], 1)
world.node_tree.nodes["Background"].inputs[1].default_value = WORLD[3]
sc.world = world
if SHADOW:
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, FLOOR))
    bpy.context.active_object.is_shadow_catcher = True
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
key.data.shape = "DISK"
key.data.size = 3.0
key.data.energy = KEY[3]
sc.collection.objects.link(key)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
# the image centre row is this height on the axis; one pixel there is S metres
zc = Z_BOT + (ROW_BOT - SIZE / 2.0) * S
target = Vector((0, 0, zc))
dist = math.hypot(CAM_D, CAM_Z - zc)
cam.data.sensor_width = 36.0
cam.data.lens = 36.0 * dist / (SIZE * S)
cam.data.clip_end = 100

def edges(m):
    e = np.zeros_like(m)
    e[1:-1, 1:-1] = m[1:-1, 1:-1] & ~(m[:-2, 1:-1] & m[2:, 1:-1] & m[1:-1, :-2] & m[1:-1, 2:])
    return e

def thicken(e):
    t = e.copy()
    t[1:] |= e[:-1]; t[:-1] |= e[1:]; t[:, 1:] |= e[:, :-1]; t[:, :-1] |= e[:, 1:]
    return t

rows_out, stats = [], {}
for name, path, axis, top in VIEWS:
    az = math.radians(AZ[name])
    d = Vector((math.sin(az), -math.cos(az), 0))
    cam.location = Vector((0, 0, CAM_Z)) + d * CAM_D
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    right = Vector((math.cos(az), math.sin(az), 0))
    key.location = target + d * KEY[0] + right * KEY[1] + Vector((0, 0, KEY[2]))
    key.rotation_euler = (target + Vector((0, 0, 0.1)) - key.location).to_track_quat("-Z", "Y").to_euler()
    tmp = OUT[:-4] + "_" + name + ".png"
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    r = bi.load(tmp)
    des = bi.load(path)
    if des.shape[0] != des.shape[1]:
        # pad a tall or wide design to a square page; its axis and top row move with it
        hh, ww = des.shape[:2]
        side = max(hh, ww)
        page = np.ones((side, side, 4), dtype=np.float32)
        ox, oy = (side - ww) // 2, (side - hh) // 2
        page[oy:oy + hh, ox:ox + ww] = des
        des, axis, top = page, axis + ox, top + oy
    if des.shape[0] != SIZE:
        des = bi.resize(des, SIZE, SIZE)
    # line the render up with the design: its axis column and top row
    dx, dy = int(round(axis - SIZE / 2.0)), int(round(top - ROW_TOP))
    r = np.roll(np.roll(r, dx, axis=1), dy, axis=0)
    mr = r[..., 3] > 0.5
    # the shadow catcher is part of the alpha: keep only the cream body for the mask
    rw = bi.on_white(r)
    mr = bi.body_mask(rw) & (r[..., 3] > 0.9)
    if INK:
        # a white piece on a white page: everything that is not the page (arms, feet and face included)
        lum = des[..., :3] @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        md = (lum < 0.972) | (des[..., :3].max(axis=2) - des[..., :3].min(axis=2) > 0.05)
    else:
        md = bi.body_mask(des)
    inter, union = (mr & md).sum(), (mr | md).sum()
    # per row: how far the outer edges are apart
    dl, dr = [], []
    for row in range(SIZE):
        cr, cd = np.nonzero(mr[row])[0], np.nonzero(md[row])[0]
        if len(cr) > 3 and len(cd) > 3:
            dl.append(cr[0] - cd[0]); dr.append(cr[-1] - cd[-1])
    dl, dr = np.array(dl), np.array(dr)
    stats[name] = dict(iou=round(float(inter) / float(union), 4),
                       left_mean_abs=round(float(np.abs(dl).mean()), 2), right_mean_abs=round(float(np.abs(dr).mean()), 2),
                       left_max=int(np.abs(dl).max()), right_max=int(np.abs(dr).max()))
    if name in TABLE:
        # design runs | render runs about the design's axis, every few rows of the part asked for
        r0, r1, step = TABLE[name]
        print("TABLE", name, "(row: design | render, columns about the axis)")
        for row in range(int(top + r0), int(top + r1), step):
            f = lambda m: " ".join("%+.0f..%+.0f" % (s - axis, e - 1 - axis) for s, e in bi.runs(m[row]))
            print("  %4d  %-34s | %s" % (row, f(md), f(mr)))
    if OVERLAY:
        full = des.copy()
        full[..., :3] = full[..., :3] * 0.45 + 0.55
        full[thicken(edges(md)), :3] = (0.1, 0.3, 1.0)
        full[thicken(edges(mr)), :3] = (1.0, 0.1, 0.1)
        full[..., 3] = 1
        bi.save(OUT[:-4] + "_ov_" + name + ".png", full)
        bi.save(OUT[:-4] + "_r_" + name + ".png", rw)
    ov = des.copy()
    ov[..., :3] = ov[..., :3] * 0.45 + 0.55
    er, ed = thicken(edges(mr)), thicken(edges(md))
    ov[ed, :3] = (0.1, 0.3, 1.0)
    ov[er, :3] = (1.0, 0.1, 0.1)
    ov[er & ed, :3] = (0.5, 0.0, 0.6)
    ov[..., 3] = 1
    k = SIZE // TILE
    tiles = [bi.shrink(t, k) if k > 1 else t for t in (des, rw, ov)]
    gap = np.full((tiles[0].shape[0], 6, 4), 0.75, dtype=np.float32)
    rows_out.append(np.concatenate([tiles[0], gap, tiles[1], gap, tiles[2]], axis=1))
    rows_out.append(np.full((6, rows_out[-1].shape[1], 4), 0.75, dtype=np.float32))
sheet = np.concatenate(rows_out[:-1], axis=0)
sheet[..., 3] = 1
bi.save(OUT, sheet)
print("COMPARE", OUT, sheet.shape[1], sheet.shape[0])
print("STATS", json.dumps(stats))
