"""Cutaway Blender preview of the royal library prefab geometry.
blender -b -t 4 --python Tools/RoyalPalace/render_library.py
Cutaway: the near wall and ceiling are hidden for overview. Not a Unity screenshot or physics test.
"""
from pathlib import Path
import re, math
import bpy
from mathutils import Matrix,Quaternion,Vector
ROOT=Path(__file__).resolve().parents[2]
text=(ROOT/'Assets/Prefabs/Obstacles/WarRoomComplete.prefab').read_text()
blocks={int(m[1]):s for s in re.split(r'(?=^--- !u!)',text,flags=re.M) if (m:=re.match(r'--- !u!\d+ &(\d+)',s))}
def ref(s,key):return int(re.search(r'^  '+key+r': \{fileID: (\d+)',s,re.M)[1])
def vec(s,key):return tuple(float(v) for v in re.findall(r'[xyzw]: ([\d.eE+-]+)',re.search(r'^  '+key+r': (.*)',s,re.M)[1]))
gos={k:s for k,s in blocks.items() if s.startswith('--- !u!1 ')}
names={k:re.search(r'^  m_Name: (.*)',s,re.M)[1] for k,s in gos.items()}
trans={ref(s,'m_GameObject'):k for k,s in blocks.items() if s.startswith('--- !u!4 ')}
root=next(k for k,v in names.items() if v=='WarRoomComplete - Operations Offices and Headquarters')
cache={}
def matrix(t):
 if t in cache:return cache[t]
 s=blocks[t];p=vec(s,'m_LocalPosition');q=vec(s,'m_LocalRotation');sc=vec(s,'m_LocalScale');parent=ref(s,'m_Father')
 m=Matrix.Translation(Vector(p))@Quaternion((q[3],q[0],q[1],q[2])).to_matrix().to_4x4()@Matrix.Diagonal((*sc,1))
 cache[t]=(matrix(parent)@m if parent else m);return cache[t]
def included(go):
 t=trans[go]
 while t:
  g=ref(blocks[t],'m_GameObject');name=names[g]
  if name.startswith(('Square perimeter','Operations ceiling','Entrance header','Distant operations','Room floor below','Royal library gallery wall -1','HQ front facade','HQ doorway header','HQ coffered ceiling','Carved ceiling beam','Ceiling cross beam','Narrow portal passage roof')):return False
  if name=='HQ side wall' and matrix(t).translation.x<0:return False
  t=ref(blocks[t],'m_Father')
 return True
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
colors={}
for meta in (ROOT/'Assets/Resources/RoyalPalace/Materials').glob('*.mat.meta'):
 guid=re.search('guid: (\\w+)',meta.read_text())[1];mat=Path(str(meta)[:-5]).read_text();m=re.search(r'_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+)',mat)
 if m:colors[guid]=tuple(map(float,m.groups()))
mats={}
def material(guid):
 if guid in mats:return mats[guid]
 c=colors.get(guid,(.3,.3,.3));m=bpy.data.materials.new(guid);m.diffuse_color=(*c,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Roughness'].default_value=.28
 if c[0]>.7 and c[1]<.7:bs.inputs['Metallic'].default_value=.8
 mats[guid]=m;return m
C=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)));root_inverse=matrix(trans[root]).inverted()
for go,s in gos.items():
 if not included(go):continue
 comps=[blocks[int(c)] for c in re.findall(r'component: \{fileID: (\d+)\}',s)]
 mesh=next((c for c in comps if c.startswith('--- !u!33 ')),None);renderer=next((c for c in comps if c.startswith('--- !u!23 ')),None)
 if mesh is None or renderer is None:continue
 shape=ref(mesh,'m_Mesh');guid=re.search(r'guid: (\w+)',renderer)[1]
 if shape==10206:bpy.ops.mesh.primitive_cylinder_add(vertices=40,radius=.5,depth=2)
 elif shape==10207:bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=.5)
 else:bpy.ops.mesh.primitive_cube_add(size=1)
 o=bpy.context.object;o.name=names[go];o.matrix_world=C@root_inverse@matrix(trans[go])@C;o.data.materials.append(material(guid))
 bevel=o.modifiers.new('Soft machined edges','BEVEL');bevel.width=.035;bevel.segments=2
# Example glyphs only; actual play generates the labels and clue sequence each round.
for go,name in names.items():
 if name!='Key letter' or not included(go):continue
 m=C@root_inverse@matrix(trans[go]);pos=m.translation
 bpy.ops.object.text_add(location=pos);o=bpy.context.object;o.data.body='ABCDEFGHIJKLMNOPQRSTUVWXYZ'[len([x for x in bpy.data.objects if x.type=='FONT'])%26];o.data.align_x='CENTER';o.data.align_y='CENTER';o.data.size=1.2;o.data.extrude=.002;o.data.materials.append(material('ink'))
# Floor and a neutral studio environment expose the key wells and the entrance.
bpy.ops.mesh.primitive_plane_add(size=400,location=(0,-80,-23));floor=bpy.context.object;floor.data.materials.append(material('floor'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=False
scene.world.color=(.3,.3,.3)
for pos,power,size in [((0,-90,130),200000,100),((-120,80,130),200000,90),((70,130,100),200000,100),((25,-140,60),90000,50)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,10,0))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';scene.camera=camera
scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
for filename,pos,target,scale in [
 ('ROYAL_LIBRARY_OVERVIEW.png',(-270,-330,320),(0,5,0),465),
 ('ROYAL_CHESS_LIBRARY.png',(-230,-115,210),(0,78,0),310),
 ('ROYAL_LIBRARY_PREVIEW.png',(-135,-190,135),(0,-80,0),195)]:
 camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
 scene.render.filepath=str(ROOT/'Docs/PawnRush'/filename);bpy.ops.render.render(write_still=True)
