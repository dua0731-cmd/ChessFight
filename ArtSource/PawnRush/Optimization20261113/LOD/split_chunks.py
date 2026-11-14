# Split big static garden meshes into ~CELL m chunks (whole faces by centroid, no cuts) so Unity Mesh LOD can pick
# a level per chunk. Usage: blender -b --factory-startup --python split_chunks.py -- <src.fbx> <out.fbx> [cell=20]
# Object names that code looks up (hung movers, lake boats, water) are never split: only meshes wider than CELL*1.2
# with >= 5000 triangles whose name is not hung/boat/water.
import bpy, bmesh, sys, os, json, math
a=sys.argv[sys.argv.index("--")+1:]
SRC,OUT=a[0],a[1];CELL=float(a[2]) if len(a)>2 else 20.0
SKIP=("Plaza fountain jets","Spin bar","Bobbing platform","Shortcut","Sluice gate","Robot arm","Boat ","Lake boat","Water ","Fall ")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC,use_custom_normals=True)
def tris(m):return sum(len(p.vertices)-2 for p in m.polygons)
report=[]
for ob in [o for o in bpy.context.scene.objects if o.type=="MESH"]:
    if ob.name.startswith(SKIP) or ob.children or tris(ob.data)<5000: continue
    mw=ob.matrix_world
    cs=[mw@p.center for p in ob.data.polygons]
    xs=[c.x for c in cs];ys=[c.y for c in cs]
    if max(max(xs)-min(xs),max(ys)-min(ys))<CELL*1.2: continue
    x0,y0=min(xs),min(ys)
    cells=[(int((c.x-x0)//CELL),int((c.y-y0)//CELL)) for c in cs]
    keys=sorted(set(cells))
    if len(keys)<2: continue
    base=ob.name;n0=tris(ob.data);parts=[]
    for i,k in enumerate(keys):
        dup=ob.copy();dup.data=ob.data.copy();bpy.context.scene.collection.objects.link(dup)
        for c in ob.users_collection:
            if dup.name not in c.objects: pass
        bm=bmesh.new();bm.from_mesh(dup.data);bm.faces.ensure_lookup_table()
        kill=[f for f,cc in zip(bm.faces,cells) if cc!=k]
        bmesh.ops.delete(bm,geom=kill,context="FACES");bm.to_mesh(dup.data);bm.free()
        dup.name=f"{base} #{i:02d}";parts.append(tris(dup.data))
    bpy.data.objects.remove(ob,do_unlink=True)
    report.append(dict(name=base,tris=n0,chunks=len(keys),sum=sum(parts)))
    print("SPLIT",base,n0,len(keys),sum(parts),flush=True)
bpy.ops.object.select_all(action="DESELECT")
obs=[o for o in bpy.context.scene.objects if o.type=="MESH"]
for o in obs:o.select_set(True)
bpy.context.view_layer.objects.active=obs[0]
os.makedirs(os.path.dirname(OUT),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,object_types={"MESH"},apply_unit_scale=True,apply_scale_options="FBX_SCALE_NONE",axis_forward="-Z",axis_up="Y",bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type="FACE",add_leaf_bones=False,path_mode="STRIP",embed_textures=False,colors_type="LINEAR")
json.dump(dict(src=SRC,cell=CELL,split=report,objects=len(obs),tris=sum(tris(o.data) for o in obs)),open(OUT+".json","w"),indent=1)
print("DONE",len(obs),flush=True)
