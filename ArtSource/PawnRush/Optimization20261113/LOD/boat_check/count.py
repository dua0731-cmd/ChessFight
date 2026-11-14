import bpy,sys
for f in sys.argv[sys.argv.index("--")+1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=f)
    print("FILE",f.split("/")[-1])
    for o in sorted(bpy.context.scene.objects,key=lambda o:o.name):
        if o.type=="MESH" and ("Boat" in o.name or "boat" in o.name):print("  ",o.name,sum(len(p.vertices)-2 for p in o.data.polygons))
