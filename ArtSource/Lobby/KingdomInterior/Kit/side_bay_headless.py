# Side bays only (R553), headless: blender -b KingdomKit.blend --factory-startup --python side_bay_headless.py
# Builds the side walls of the full hall, renders the left wall face-on (ortho, at the reference's scale)
# and a player's view, and stacks the reference over the face-on render.
import bpy, os, sys, builtins, time, math
import numpy as np
from mathutils import Vector

KIT = r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"

t0 = time.time()
K = {"__file__": os.path.join(KIT, "kit_build.py"), "__name__": "kit_build"}
exec(open(K["__file__"], encoding="utf-8").read(), K)
builtins.KIT_G = K
p = os.path.join(KIT, "stair_end.py")
G = {"__file__": p, "__name__": "stair_end"}
exec(open(p, encoding="utf-8").read(), G)

G["full_hall"](-25.0)
G["clear_se"]()
sc, root = G["se_scene"]()
shell = G["se_col"]("Shell")
G["side_walls"](shell)
G["vault"](shell)
mb = G["MB"]()
mb.box(-G["SIDE_X"], G["SIDE_X"], G["HALL_FRONT"], G["BACK_Y"], -.2, 0)
G["finish"](mb, "Floor", shell, bevel=0)
pitch, sh, sv = G["bay_scale"]()
piers = G["bay_piers"]()
print("pitch", round(pitch, 3), "sh", round(sh, 5), "sv", round(sv, 5), "piers", [round(y, 3) for y in piers])

with bpy.context.temp_override(scene=sc, view_layer=sc.view_layers[0]):
    dg = bpy.context.evaluated_depsgraph_get()
for o in shell.objects:
    e = o.evaluated_get(dg)
    m = e.to_mesh()
    print("tris", o.name, sum(len(f.vertices) - 2 for f in m.polygons))
    e.to_mesh_clear()

# face-on ortho camera on the left wall: the reference's middle bay is bay 2 (piers 0 and 1)
bay_c = (piers[0] + piers[1]) / 2
W_px, H_px = 1523, 822
cam = bpy.data.objects.get("SB Cam Face") or bpy.data.objects.new("SB Cam Face", bpy.data.cameras.new("SB Cam Face"))
if cam.name not in sc.objects:
    G["se_col"]("Cameras").objects.link(cam)
cam.data.type = "ORTHO"
cam.data.sensor_fit = "HORIZONTAL"
cam.data.ortho_scale = W_px * sh
cam.data.clip_end = 100
u_mid = bay_c + (761.5 - (540 + 967) / 2) * sh            # looking at -x from inside: screen right = world +y
z_mid = (590 - 411) * sv
cam.location = (-G["SIDE_X"] + 12, u_mid, z_mid)
cam.rotation_euler = (Vector((-1, 0, 0))).to_track_quat("-Z", "Y").to_euler()
res_y = round(H_px * sv / sh)
G["view_cam"]("SB Cam Walk", (2.5, -14.0, 1.7), (-9.5, -4.0, 4.2), 60)
print("lantern glass middles", [tuple(round(c, 3) for c in a) for a in G["LANTERN_AXIS"]])
lx, ly, lz = next(a for a in G["LANTERN_AXIS"] if a[0] < 0 and abs(a[1] - piers[0]) < .01)
G["view_cam"]("SB Cam Lantern", (lx + 4.5, ly - 3.2, lz - .6), (lx, ly, lz), 50)

shots = {}
for name, cam_name, w, h in (("side_bay_face.png", "SB Cam Face", W_px, res_y),
                              ("side_bay_walk.png", "SB Cam Walk", 1600, 900),
                              ("side_bay_lantern.png", "SB Cam Lantern", 1200, 900)):
    out = os.path.join(KIT, name)
    G["render_view"](out, cam_name, w, h)
    print("rendered", out, round(time.time() - t0, 1))
    shots[name] = out


def load(path):
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)


def fit(a, w):
    h0, w0 = a.shape[:2]
    h = round(h0 * w / w0)
    ys = (np.arange(h) * h0 / h).astype(int)
    xs = (np.arange(w) * w0 / w).astype(int)
    return a[ys][:, xs]


ref = load(os.path.join(KIT, "reference_side_windows.webp"))
face = load(shots["side_bay_face.png"])
w = ref.shape[1]
parts = [fit(ref, w), fit(face, w)]
bar = np.ones((10, w, 4), np.float32)
bar[..., :3] = .12
C = np.concatenate([parts[1], bar, parts[0]], 0)      # rows start at the bottom: reference on top
im = bpy.data.images.new("sb_cmp", C.shape[1], C.shape[0], alpha=True)
im.pixels.foreach_set(C.ravel())
im.filepath_raw = os.path.join(KIT, "side_bay_compare.png")
im.file_format = "PNG"
im.save()
print("done", round(time.time() - t0, 1))
