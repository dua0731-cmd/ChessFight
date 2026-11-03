"""Blender FBX round-trip and motion-data audit; does not claim a Unity runtime test."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
folder=Path(__file__).resolve().parent
target=folder.parents[2]/'Assets/Resources/PawnRushGarden'
take=json.loads((target/'RoyalWaterGarden_Motion.json').read_text(encoding='utf8'))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(target/'RoyalWaterGarden.fbx'),use_anim=False)
bpy.context.view_layer.update()
errors=[]; worst=[]
for role in take['meshes']:
 o=bpy.data.objects.get(role['name']);assert o and o.type=='MESH',role['name']
 points=[o.matrix_world @ Vector(v) for v in o.bound_box]
 bounds=[min(v[k] for v in points) for k in range(3)]+[max(v[k] for v in points) for k in range(3)]
 error=max(abs(a-b) for a,b in zip(bounds,role['bounds']));errors.append(error);worst.append((error,role['source']))
 assert error<.12,(role['source'],error)
for track in take['tracks']:
 assert bpy.data.objects.get(track['name'])
 a=track['samples'];assert len(a)==take['frames']*7 and all(math.isfinite(v) for v in a)
 assert max(abs(a[i]-a[-7+i]) for i in range(3))<.001,track['source']
 q=a[3:7];end=a[-4:];assert abs(abs(sum(x*y for x,y in zip(q,end)))-1)<.001
 for f in range(take['frames']):assert abs(sum(v*v for v in a[f*7+3:f*7+7])-1)<.001
assert len(take['tracks'])==13 and take['duration']==24
# R287: the parade take (its pivot starts at sample 0, so Unity's rest pose is the take's first pose).
pa=take['parade'];piv=bpy.data.objects.get(pa['track']);assert piv,pa['track']
a=pa['samples'];assert len(a)==pa['frames']*7 and all(math.isfinite(v) for v in a)
for f in range(pa['frames']):assert abs(sum(v*v for v in a[f*7+3:f*7+7])-1)<.001
assert abs(pa['duration']-pa['frames']/pa['fps'])<1e-6 and 40<pa['duration']<120
report={'parade_s':round(pa['duration'],1),'meshes':len(take['meshes']),'tracks':len(take['tracks']),'frames':take['frames'],'max_roundtrip_bounds_error_m':max(errors),'worst':sorted(worst,reverse=True)[:5],'unity_runtime_tested':False}
(folder/'unity_export_check_v31.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(report)
