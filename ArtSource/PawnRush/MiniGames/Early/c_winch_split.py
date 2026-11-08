# R502: the user's winch (Source/R502_WoodenGearWinch.fbx) is one mesh; the game turns its drum (MgDrawbridge "Drum"),
# so the drum is taken out as its own part. The drum's axis runs along the asset's long side (y): its centre line
# and radius are fitted to the vertices of the middle slice (RANSAC circle in x-z), the drum's length is where that
# circle's surface is found, and every face whose centre lies inside the cylinder (radius x 1.15) within that length
# goes to the drum. The rest (base, cheeks, gear, axle caps) stays the frame.
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_winch_split.py
# Writes Source/R502_Winch_Split.blend: WINCH_Frame (bottom centre at 0, as the asset) and WINCH_Drum (origin on the
# axis at the drum's middle, axis along x: Blender x = the game drum's local x), and winch_split_check.png.
import os, math
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R502_WoodenGearWinch.fbx")
OUT = os.path.join(HERE, "Source", "R502_Winch_Split.blend")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data
V = np.array([v.co[:] for v in me.vertices])
lo, hi = V.min(0), V.max(0)
me.transform(Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))
V = np.array([v.co[:] for v in me.vertices])
size = V.max(0) - V.min(0)
print("WINCH size", np.round(size, 3))

# 1. circle in x-z through the middle slice (the drum's surface dominates there)
mid = V[np.abs(V[:, 1]) < .15 * size[1]]
pts = mid[:, [0, 2]]
rng = np.random.default_rng(5)
best = (0, None)
for _ in range(4000):
    a, b, c = pts[rng.choice(len(pts), 3, replace=False)]
    d = 2 * (a[0] * (b[1] - c[1]) + b[0] * (c[1] - a[1]) + c[0] * (a[1] - b[1]))
    if abs(d) < 1e-9:
        continue
    ux = ((a @ a) * (b[1] - c[1]) + (b @ b) * (c[1] - a[1]) + (c @ c) * (a[1] - b[1])) / d
    uz = ((a @ a) * (c[0] - b[0]) + (b @ b) * (a[0] - c[0]) + (c @ c) * (b[0] - a[0])) / d
    r = math.hypot(a[0] - ux, a[1] - uz)
    if not (.06 * size[2] < r < .4 * size[2]):
        continue
    n = int((np.abs(np.hypot(pts[:, 0] - ux, pts[:, 1] - uz) - r) < .004 * size[2]).sum())
    if n > best[0]:
        best = (n, (ux, uz, r))
n, (cx, cz, R) = best
print("DRUM axis x %.4f z %.4f radius %.4f (%d inliers of %d)" % (cx, cz, R, n, len(pts)))
# 2. its length: where the surface of that circle is found
dist = np.hypot(V[:, 0] - cx, V[:, 2] - cz)
on = np.abs(dist - R) < .006 * size[2]
ys = np.sort(V[on, 1])
s0, s1 = np.percentile(ys, 1), np.percentile(ys, 99)
# ...but only between the cheeks and the gear: those rise past the drum (farther than 1.3 R from the axis, above it);
# the drum is the widest stretch around its middle with none of them
tall = V[(dist > 1.3 * R) & (V[:, 2] > cz) & (np.abs(V[:, 0] - cx) < 1.6 * R)]
bins = np.linspace(s0, s1, 121)
hist, _ = np.histogram(tall[:, 1], bins)
empty = hist <= max(2, int(.02 * hist.max()))
centre = np.argmin(np.abs((bins[:-1] + bins[1:]) / 2 - (s0 + s1) / 2))
a = b = centre
while a > 0 and empty[a - 1]:
    a -= 1
while b < len(empty) - 1 and empty[b + 1]:
    b += 1
y0, y1 = bins[a] + .003, bins[b + 1] - .003
print("DRUM surface y %.4f..%.4f, between the cheeks y %.4f..%.4f" % (s0, s1, y0, y1))
# 3. faces inside the cylinder within that length -> the drum
bm = bmesh.new()
bm.from_mesh(me)
inside = []
for f in bm.faces:
    c = f.calc_center_median()
    if y0 - .002 <= c.y <= y1 + .002 and math.hypot(c.x - cx, c.z - cz) < R * 1.15:
        inside.append(f)
drum_bm = bm.copy()
keep = set(f.index for f in inside)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index in keep], context="FACES")
bmesh.ops.delete(drum_bm, geom=[f for f in drum_bm.faces if f.index not in keep], context="FACES")
bm.to_mesh(me)
bm.free()
ob.name = "WINCH_Frame"
dme = bpy.data.meshes.new("WINCH_Drum")
drum_bm.to_mesh(dme)
drum_bm.free()
for m in me.materials:
    dme.materials.append(m)
# drum frame: origin on the axis at the drum's middle, the axis turned from y to x
dme.transform(Matrix.Rotation(-math.pi / 2, 4, "Z") @ Matrix.Translation(Vector((-cx, -(y0 + y1) / 2, -cz))))
drum = bpy.data.objects.new("WINCH_Drum", dme)
bpy.context.scene.collection.objects.link(drum)
drum["axis"] = (cx, (y0 + y1) / 2, cz)
drum["radius"] = R
drum["length"] = y1 - y0
print("SPLIT drum faces %d, frame faces %d" % (len(dme.polygons), len(me.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=OUT)

# check render: the frame, and the drum lifted out above it
sc = bpy.context.scene
show = drum.copy()
sc.collection.objects.link(show)
show.location = (cx, (y0 + y1) / 2, cz + .45 * size[2])
show.rotation_euler = (0, 0, math.pi / 2)
drum.hide_render = True
sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
sc.world = w
sun = bpy.data.lights.new("s", "SUN")
sun.energy = 3
so = bpy.data.objects.new("s", sun)
so.rotation_euler = (math.radians(50), 0, math.radians(30))
sc.collection.objects.link(so)
cam = bpy.data.cameras.new("c")
co = bpy.data.objects.new("c", cam)
sc.collection.objects.link(co)
sc.camera = co
co.location = (1.6, -1.3, 1.2)
co.rotation_euler = (Vector((0, 0, .45)) - co.location).to_track_quat("-Z", "Y").to_euler()
sc.render.resolution_x, sc.render.resolution_y = 800, 700
sc.render.filepath = os.path.join(HERE, "winch_split_check.png")
bpy.ops.render.render(write_still=True)
