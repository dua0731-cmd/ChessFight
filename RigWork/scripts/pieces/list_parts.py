# List the loose parts of the user's Tripo exports: vertex count, height band and front-most point, normalised units.
import bpy, sys
for name in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath="C:/Users/trews/Downloads/" + name + ".fbx")
    ms = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in bpy.data.objects:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in ms:
        o.parent = None; o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(ms) > 1:
        bpy.ops.object.join()
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE"); bpy.ops.object.mode_set(mode="OBJECT")
    parts = [p for p in bpy.context.selected_objects if p.type == "MESH"]
    allz = [v.co.z for p in parts for v in p.data.vertices]
    z0, z1 = min(allz), max(allz)
    print("PIECE", name, len(parts), "parts, uv layers:", len(parts[0].data.uv_layers))
    for p in sorted(parts, key=lambda p: -len(p.data.vertices)):
        vs = [v.co for v in p.data.vertices]
        f = lambda z: (z - z0) / (z1 - z0)
        print("  %6d verts  z %.2f-%.2f  x %+.2f..%+.2f  y %+.2f..%+.2f" % (len(vs), f(min(v.z for v in vs)), f(max(v.z for v in vs)),
              min(v.x for v in vs), max(v.x for v in vs), min(v.y for v in vs), max(v.y for v in vs)))
