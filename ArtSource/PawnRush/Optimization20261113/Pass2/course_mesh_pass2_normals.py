import os,sys,json,math,shutil,time
SCRIPT=os.environ.get("CHESSFIGHT_PASS1_SCRIPT",os.path.normpath(os.path.join(os.path.dirname(__file__),"..","course_mesh_pass.py")))
exec(compile(open(SCRIPT,encoding="utf8").read().split("results=json.load")[0],SCRIPT,"exec"),globals())
import re,numpy as np
ASSETS2=[
 ("hall","Assets/Resources/PawnRushGrandStairHall/GrandStairHallV15.fbx",lambda n:bool(re.match(r"^HallV15_Round_wall_[01]_[01]$",n)) or n=="HallV15_Balustrades",lambda n:n=="HallV15_Round_wall_0_1"),
 ("towers","Assets/Resources/PawnRushGardenConcept/GardenWalls.fbx",lambda n:n.startswith("Castle tower "),lambda n:n=="Castle tower E1"),
 ("boats","Assets/Resources/PawnRushGardenConcept/GardenBoats.fbx",lambda n:n=="Boat tunnels" or bool(re.match(r"^Boat [1-4]-[1-2]$",n)),lambda n:n=="Boat tunnels"),
 ("plinths","Assets/Resources/PawnRushGrandStairHall/StairStatuesV36.fbx",lambda n:n.endswith("_plinth"),lambda n:n.startswith("StairStatue_Knight")),
]

def restore_original_normals(source, target):
 source_normals=[x.vector.copy() for x in source.corner_normals]
 target_normals=[x.vector.copy() for x in target.corner_normals]
 lookup={}
 for poly in source.polygons:
  for li in poly.loop_indices:
   co=source.vertices[source.loops[li].vertex_index].co
   k=(tuple(round(v,7) for v in co),poly.material_index)
   lookup.setdefault(k,[]).append(source_normals[li])
 normals=[None]*len(target.loops)
 misses=0
 for poly in target.polygons:
  for li in poly.loop_indices:
   co=target.vertices[target.loops[li].vertex_index].co
   k=(tuple(round(v,7) for v in co),poly.material_index)
   found=lookup.get(k)
   if found:normals[li]=max(found,key=lambda n:n.dot(target_normals[li]))
   else:normals[li]=target_normals[li];misses+=1
 target.normals_split_custom_set(normals)
 return misses

out=[]
for key,rel,choose,focusfn in ASSETS2:
 if os.environ.get("MESH_PASS2_ONLY") and key not in os.environ["MESH_PASS2_ONLY"].split(","):continue
 source=os.path.join(ROOT,rel); original=os.path.join(OUT,"original",rel)
 os.makedirs(os.path.dirname(original),exist_ok=True)
 if not os.path.exists(original):shutil.copy2(source,original);shutil.copy2(source+".meta",original+".meta")
 obs=load(original); targets=[o for o in obs if choose(o.name)];focus=[o for o in obs if focusfn(o.name)]
 originals={o.name:o.data for o in targets}
 total=sum(tris(o.data) for o in obs)
 render(focus,os.path.join(OUT,key+"_before.png"))
 render(focus,os.path.join(OUT,key+"_side_before.png"),(-5,-3,2))
 print("START",key,total,[(o.name,tris(o.data)) for o in targets],flush=True)
 if key=="towers":
  vg=protect(targets[0]); print("TOWER_GROUP",len(targets[0].data.vertices),sum(any(g.group==vg.index and g.weight>.5 for g in v.groups) for v in targets[0].data.vertices),flush=True);targets[0].vertex_groups.remove(vg)
 for angle in [.25,.5,1.0]:
  tag=str(angle).replace(".","p")
  items=[]
  for ob in targets:
   old=ob.data;ob.data=originals[ob.name].copy()
   if old!=originals[ob.name] and old.users==0:bpy.data.meshes.remove(old)
   n0=tris(originals[ob.name])
   mod=ob.modifiers.new("Planar preservation pass 2","DECIMATE")
   mod.decimate_type="DISSOLVE";mod.angle_limit=math.radians(angle)
   mod.delimit={"MATERIAL","UV","NORMAL"};mod.use_dissolve_boundaries=False
   bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=mod.name)
   misses=restore_original_normals(originals[ob.name],ob.data)
   items.append(dict(name=ob.name,before=n0,after=tris(ob.data),normalTransferMisses=misses))
  render(focus,os.path.join(OUT,key+"_"+tag+"_after.png"))
  render(focus,os.path.join(OUT,key+"_side_"+tag+"_after.png"),(-5,-3,2))
  bpy.ops.object.select_all(action="DESELECT")
  for o in obs:o.select_set(True)
  bpy.context.view_layer.objects.active=obs[0]
  target=os.path.join(OUT,"candidates",tag,rel);os.makedirs(os.path.dirname(target),exist_ok=True)
  bpy.ops.export_scene.fbx(filepath=target,use_selection=True,object_types={"MESH"},apply_unit_scale=True,apply_scale_options="FBX_SCALE_NONE",axis_forward="-Z",axis_up="Y",bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type="FACE",add_leaf_bones=False,path_mode="STRIP",embed_textures=False,colors_type="LINEAR")
  entry=dict(key=key,path=rel,angle=angle,tag=tag,before=total,after=sum(tris(o.data) for o in obs),items=items)
  out.append(entry)
  json.dump(out,open(os.path.join(OUT,"variants.json"),"w",encoding="utf8"),indent=2)
  print("VARIANT",key,angle,entry["before"],entry["after"],flush=True)
