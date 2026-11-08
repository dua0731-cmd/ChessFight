"""Print what each Tripo GLB holds and render it from the front, the side and three-quarters.

blender -b --factory-startup --python inspect_tripo.py -- <raw_dir> <out_dir> [file ...]
Front = looking along +Y (Blender), side = along -X. Workbench, texture colours, flat studio light.
"""
import bpy
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, OUT = args[0], args[1]
ONLY = set(args[2:])   # file names to look at (all when none)
os.makedirs(OUT, exist_ok=True)


def bounds(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


for f in sorted(os.listdir(RAW)):
    if not f.endswith((".glb", ".fbx")) or (ONLY and f not in ONLY):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if f.endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=os.path.join(RAW, f))
    else:
        bpy.ops.import_scene.gltf(filepath=os.path.join(RAW, f))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    lo, hi = bounds(meshes)
    size = hi - lo
    print(f"[tripo] {f}: {len(meshes)} meshes, {tris} tris, size x {size.x:.3f} y {size.y:.3f} z {size.z:.3f}, "
          f"min {tuple(round(v, 3) for v in lo)}")
    for o in meshes:
        print(f"[tripo]   {o.name}: rot {tuple(round(math.degrees(a), 1) for a in o.rotation_euler)} scale {tuple(round(s, 3) for s in o.scale)} "
              f"mats {[m.name for m in o.data.materials if m]}")
    for img in bpy.data.images:
        print(f"[tripo]   image {img.name} {tuple(img.size)}")
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x, sc.render.resolution_y = 700, 700
    sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.55, 0.55, 0.58)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    centre = (lo + hi) / 2
    r = max(size) * 1.2
    cam.data.ortho_scale = r
    for name, d in (("front", Vector((0, -1, 0))), ("side", Vector((1, 0, 0))), ("top", Vector((0, -0.001, 1))),
                    ("three_quarter", Vector((1, -1, 0.8)).normalized())):
        cam.location = centre + d * r * 3
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.clip_end = r * 10
        sc.render.filepath = os.path.join(OUT, f"{f[:-4]}_{name}.png")
        bpy.ops.render.render(write_still=True)
