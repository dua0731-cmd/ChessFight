# Prints each model's mesh bounds, vertex count, bones and vertex groups (fbx, glb or blend).
# blender -b --factory-startup -P fbx_bounds.py -- <model> ...
import bpy, sys, os
from mathutils import Vector

for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ext = os.path.splitext(path)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        bpy.ops.wm.open_mainfile(filepath=path)
    bpy.context.view_layer.update()
    print("FILE", os.path.basename(path))
    for o in bpy.data.objects:
        if o.type == "MESH":
            pts = [o.matrix_world @ v.co for v in o.data.vertices]
            mn = [round(min(p[i] for p in pts), 4) for i in range(3)]
            mx = [round(max(p[i] for p in pts), 4) for i in range(3)]
            print("  MESH %s verts %d faces %d min %s max %s groups %s" % (
                o.name, len(o.data.vertices), len(o.data.polygons), mn, mx, [g.name for g in o.vertex_groups]))
        elif o.type == "ARMATURE":
            print("  ARMATURE %s scale %s bones %s" % (o.name, [round(s, 4) for s in o.scale], [
                (b.name, [round(v, 3) for v in (o.matrix_world @ b.head_local)]) for b in o.data.bones]))
