"""Run in Blender 5.2: --background --python export_unity_v31.py (R296: v30 with decimated V19 / V16 meshes). Writes the visible course, the hidden plaza
collision proxy and a sampled pose take (R287), and the royal boat's parade take (played by Unity on cue)."""
import bpy, json, os, hashlib
from mathutils import Vector
from pathlib import Path

folder=Path(__file__).resolve().parent
target=folder.parents[2]/'Assets/Resources/PawnRushGarden'
target.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(folder/'RoyalWaterGarden_v31.blend'))
scene=bpy.context.scene;scene.frame_set(1);bpy.context.view_layer.update()
visible=[o for o in bpy.data.objects if o.type=='MESH' and ((not o.hide_render and not o.hide_get() and not o.name.startswith('V14 |')) or o.get('unity_collision_proxy'))]
selected=set(visible)
for o in visible:
 p=o.parent
 while p:selected.add(p);p=p.parent
ordered=sorted(selected,key=lambda o:o.name)
# R287: node names come from the Blender name, not the sort position. A saved scene keeps its colliders, materials and
# hidden renderers on the FBX's nodes by name; with numbered names one object added or removed shifted every later
# number, and a scene built before such an export dressed the wrong meshes (승규 11-02: "맵이 엉망이 됐어").
names={o:'GW_'+hashlib.sha1(o.name.encode('utf8')).hexdigest()[:12] for o in ordered}
assert len(set(names.values()))==len(names)
def moving(o):
 return (o.name=='회전봉_PIVOT_Z_8초1회전' or (o.name.startswith('V16 |') and 'C platform' in o.name) or o.name.startswith('V20 | robot elbow') or o.name.startswith('V02_필수진자_PIVOT') or (o.name.startswith('V02_') and o.name.endswith('보트_PIVOT')))
def depth(o):
 n=0
 while o.parent:n+=1;o=o.parent
 return n
movers=sorted([o for o in ordered if moving(o)],key=depth)
tracks=[{'name':names[o],'source':o.name,'samples':[]} for o in movers]
for frame in range(1,722):
 scene.frame_set(frame);bpy.context.view_layer.update()
 for o,track in zip(movers,tracks):
  p=o.matrix_world.translation;q=o.matrix_world.to_quaternion()
  track['samples'].extend(round(float(v),7) for v in (*p,q.x,q.y,q.z,q.w))
scene.frame_set(1);bpy.context.view_layer.update()
meshes=[]
for o in visible:
 name=o.name;kind='none'
 if name.startswith(('V19 |','V17 | castle wall','V17 | gate to tower')) or name=='전체기단_96x120m' or 'entry floor infill' in name or 'repaired pavement' in name:kind='box'
 if name.startswith(('V18 | round garden plaza','V16 |')) and ('platform' in name or 'royal boat' in name or 'round garden plaza' in name):kind='mesh'
 if name.startswith(('V17 | finish castle arch','V20 | robot ','V20 | clockwise plaza rotor','V18 | pendulum')):kind='mesh'
 if '경사지형' in name:kind='mesh'
 # R277: the decorative plaza (496k faces, grooved pavement) no longer collides; its smooth proxy does.
 if name.startswith('V18 | round garden plaza') or name.startswith('V20 | repaired pavement'):kind='none'
 if name.startswith(('V21 | plaza entry pier','V21 | plaza entry coping','V21 | plaza entry post')):kind='box'
 if o.get('unity_collision_proxy'):kind='proxy'
 if name.startswith('V22 | reservoir') and 'spout' in name:kind='box'
 if name.startswith(('V23 | tank pier','V23 | centre spout')):kind='box'
 # R283: the outlets' stone (surrounds, troughs, tank parapets) blocks; the dark mouths do not.
 if name.startswith('V26 | spout stone'):kind='box'
 # R286: the big castle walls and corner towers do not collide; hidden slabs on the play edge do (proxy).
 # R287: the water gate stops a player who jumps at it; the parade boat and the arch's water never collide.
 if name.startswith('V30 | castle water gate'):kind='box'
 points=[o.matrix_world @ Vector(p) for p in o.bound_box]
 bounds=[min(p[k] for p in points) for k in range(3)]+[max(p[k] for p in points) for k in range(3)]
 meshes.append({'name':names[o],'source':name,'collider':kind,'bounds':bounds})
 if len(o.data.materials)==1:
  for face in o.data.polygons:face.material_index=0
 for slot in o.material_slots:slot.link='DATA'
 if o.data.shape_keys:o.shape_key_clear()
bpy.ops.object.select_all(action='DESELECT')
for o in ordered:o.hide_set(False);o.select_set(True)
parade_pivot=bpy.data.objects['V30 | parade boat PIVOT']
for o in ordered:o.name=names[o]
bpy.ops.export_scene.fbx(filepath=str(target/'RoyalWaterGarden.fbx'),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_NONE',axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type='FACE',add_leaf_bones=False,path_mode='AUTO',embed_textures=False,colors_type='LINEAR')
extra=json.loads((folder/'water_interaction_v30.json').read_text(encoding='utf8'))
track_of={t['source']:t['name'] for t in tracks}
boats=[dict(b,track=track_of[b['pivot']]) for b in extra['boats']]
bobbers=[dict(b,track=track_of[b['object']]) for b in extra['bobbers']]
parade=json.loads((folder/'parade_v30.json').read_text(encoding='utf8'))
parade['track']=names[parade_pivot];del parade['pivot']
manifest={'fps':30,'duration':24.0,'frames':721,'tracks':tracks,'meshes':meshes,'splashes':extra['splashes'],'boats':boats,'bobbers':bobbers,'parade':parade}
(target/'RoyalWaterGarden_Motion.json').write_text(json.dumps(manifest,separators=(',',':'),ensure_ascii=False),encoding='utf8')
print('EXPORTED',len(visible),'meshes',len(tracks),'motion tracks',target,flush=True)
