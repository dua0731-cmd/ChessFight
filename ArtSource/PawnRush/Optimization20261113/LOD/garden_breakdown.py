import bpy,sys,re,json,glob,os
root=sys.argv[sys.argv.index("--")+1]
rows=[]
for f in sorted(glob.glob(os.path.join(root,"Garden*.fbx"))):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=f)
    for o in bpy.context.scene.objects:
        if o.type=="MESH":rows.append((os.path.basename(f)[:-4],o.name,sum(len(p.vertices)-2 for p in o.data.polygons)))
json.dump(rows,open(os.path.join(os.path.dirname(os.path.abspath(sys.argv[sys.argv.index("--python")+1])),"garden_breakdown.json"),"w"))
print("ROWS",len(rows),sum(r[2] for r in rows),flush=True)
