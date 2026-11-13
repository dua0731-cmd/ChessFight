"""Inspect Tripo GLBs: size, triangle/quad counts, UVs, materials, textures; render a preview each.

blender -b --factory-startup --python inspect_tripo.py -- <raw_dir> <out_dir>
"""
import bpy
import math
import os
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, OUT = args[0], args[1]
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
    if not f.endswith(".glb"):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(RAW, f))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    tris = quads = ngons = verts = 0
    uv = []
    mats = set()
    for o in meshes:
        me = o.data
        verts += len(me.vertices)
        for p in me.polygons:
            n = len(p.vertices)
            tris += n - 2
            if n == 3:
                pass
            elif n == 4:
                quads += 1
            else:
                ngons += 1
        uv.append(len(me.uv_layers))
        for m in me.materials:
            if m:
                mats.add(m.name)
    imgs = [(i.name, tuple(i.size)) for i in bpy.data.images]
    lo, hi = bounds(meshes)
    size = hi - lo
    print(f"INFO {f}: objs={len(meshes)} verts={verts} tris={tris} quads={quads} ngons={ngons} uv={uv} "
          f"size=({size.x:.3f},{size.y:.3f},{size.z:.3f}) min=({lo.x:.2f},{lo.y:.2f},{lo.z:.2f}) mats={sorted(mats)} imgs={imgs}")

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = 700
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.7, 0.9, 1)
    sc.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    center = (lo + hi) / 2
    r = max(size) * 1.6
    cam.location = center + Vector((r * 0.75, -r, r * 0.45))
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT, f.replace(".glb", "_raw.png"))
    bpy.ops.render.render(write_still=True)
