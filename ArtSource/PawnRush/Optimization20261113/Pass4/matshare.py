# Pass 4: triangles per material for the named objects (where a tower's or tree's triangles go).
import bpy, sys, json, collections
args=sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=args[0])
for o in bpy.context.scene.objects:
    if o.type!="MESH" or o.name not in args[1].split(","): continue
    c=collections.Counter()
    for p in o.data.polygons: c[o.material_slots[p.material_index].material.name if o.material_slots[p.material_index].material else "-"]+=len(p.vertices)-2
    print("JSON"+json.dumps(dict(name=o.name,tris=c.most_common())))
