"""Blender 4.3+: blender -b --python Tools/Art/build_chess_timer_pieces.py
Exports five metre-scale FBX models and a render of the actual meshes.
"""
import bpy, bmesh, math
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Resources/ChessTimerPieces'
PREVIEW = ROOT / 'Docs/KingRush/CHESS_TIMER_PIECES.png'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def material(name, color, metallic=0, roughness=.28):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF'); b.inputs['Base Color'].default_value=(*color,1)
    b.inputs['Metallic'].default_value=metallic; b.inputs['Roughness'].default_value=roughness
    return m
body=material('Body',(.88,.80,.64)); trim=material('Trim',(.55,.33,.10),.7,.25)
dark=material('Dark',(.035,.045,.055),.1,.32)
parts=[]
def finish(obj,name,mat=body,bevel=0):
    obj.name=name; obj.data.materials.append(mat)
    if bevel:
        mod=obj.modifiers.new('Soft machined edges','BEVEL'); mod.width=bevel; mod.segments=3
        bpy.context.view_layer.objects.active=obj; bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons: poly.use_smooth=True
    mod=obj.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL'); mod.keep_sharp=True; mod.weight=40
    bpy.context.view_layer.objects.active=obj
    try: bpy.ops.object.modifier_apply(modifier=mod.name)
    except RuntimeError: obj.modifiers.remove(mod)
    parts.append(obj); return obj
def lathe(name,profile,mat=body,n=64):
    vertices=[]; faces=[]
    for radius,z in profile:
        vertices.extend((radius*math.cos(i*math.tau/n),radius*math.sin(i*math.tau/n),z) for i in range(n))
    for j in range(len(profile)-1):
        for i in range(n):
            a=j*n+i; b=j*n+(i+1)%n; faces.append((a,b,b+n,a+n))
    faces += [tuple(reversed(range(n))),tuple((len(profile)-1)*n+i for i in range(n))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    return finish(obj,name,mat)
def sphere(name,pos,scale,mat=body):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,location=pos)
    obj=bpy.context.object;obj.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat)
def box(name,pos,scale,mat=body,bevel=.06,rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos,rotation=rotation)
    obj=bpy.context.object;obj.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat,bevel)
def base():
    lathe('Turned circular plinth',[(1.20,0),(1.34,.08),(1.39,.18),(1.39,.29),(1.34,.38),(1.19,.48),(1.17,.58),(1.05,.65),(.95,.72)])
    lathe('Brass foot inlay',[(1.395,.21),(1.395,.28)],trim)
    lathe('Upper plinth bead',[(1.05,.63),(1.09,.69),(1.05,.76),(.94,.79)])
def stem(top=3.5):
    lathe('Turned concave stem',[(.95,.72),(.91,.88),(.80,1.05),(.67,1.28),(.52,1.65),(.43,2.10),(.43,top-.6),(.5,top-.32),(.72,top-.20),(.76,top-.08),(.7,top)])
    lathe('Collar inlay',[(.76,top-.14),(.76,top-.08)],trim)
def boolean_difference(obj,cutter):
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Carved opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

def pawn():
    base();stem(2.35)
    sphere('Pawn round head',(0,0,3.0),(.76,.76,.76))
def rook():
    base()
    lathe('Rook fortress shaft',[(.95,.72),(.90,.95),(.78,1.18),(.75,3.35),(.84,3.49),(1.12,3.61),(1.20,3.78),(1.20,4.10)])
    lathe('Tower rim inlay',[(1.205,3.72),(1.205,3.81)],trim)
    # Ring with an open top rather than a flat block.
    lathe('Hollow tower rim',[(1.20,4.03),(1.20,4.27),(.91,4.27),(.91,4.03)])
    for i in range(6):
        a=i*math.tau/6
        box('Castle merlon',(.99*math.cos(a),.99*math.sin(a),4.48),(.52,.46,.60),rotation=(0,0,a),bevel=.055)
    lathe('Dark tower interior',[(.9,4.04),(.9,4.06)],dark)
def bishop():
    base();stem(3.3)
    mitre=lathe('Bishop pointed mitre',[(.45,3.25),(.65,3.45),(.80,3.82),(.79,4.12),(.66,4.45),(.43,4.79),(.17,5.12),(.025,5.29)])
    # A through-cut remains visible in silhouette from both sides.
    bpy.ops.mesh.primitive_cube_add(size=1,location=(.34,0,4.72),rotation=(0,-.40,0))
    cutter=bpy.context.object;cutter.scale=(.19,2.3,1.7);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    boolean_difference(mitre,cutter)
    bevel=mitre.modifiers.new('Cut edge bevel','BEVEL');bevel.width=.025;bevel.segments=2
    bpy.context.view_layer.objects.active=mitre;bpy.ops.object.modifier_apply(modifier=bevel.name)
    sphere('Mitre finial',(0,0,5.3),(.12,.12,.12),trim)
def knight():
    base()
    # Extruded, bevelled horse profile: long muzzle, inset throat and sweeping neck.
    outline=[(.64,.75),(-.72,.75),(-.66,1.28),(-.32,1.85),(-.28,2.52),(-.49,2.94),
             (-1.10,3.05),(-1.34,3.30),(-1.31,3.72),(-.83,3.98),(-.45,4.52),
             (-.09,4.74),(.34,4.54),(.67,4.02),(.80,3.35),(.66,2.66),(.33,2.03),(.29,1.44)]
    # Profile coordinates are forward(Y), height(Z); X is thickness.
    verts=[(x,y,z) for x in (-.43,.43) for y,z in outline]; n=len(outline)
    faces=[tuple(reversed(range(n))),tuple(n+i for i in range(n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new('Horse profile');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Sculpted horse neck and muzzle',mesh);bpy.context.collection.objects.link(obj)
    finish(obj,obj.name,body,.13)
    for side in (-1,1):
        # Two tall ears and recessed eye accents make this read as a horse from the gameplay camera.
        ear=box('Horse pointed ear',(side*.29,-.03,4.85),(.26,.40,.85),bevel=.10,rotation=(.12,side*.15,0))
        sphere('Horse eye',(side*.454,-.48,4.12),(.075,.13,.13),dark)
        sphere('Eye glint',(side*.49,-.51,4.16),(.026,.045,.045),trim)
        sphere('Horse nostril',(side*.435,-1.07,3.57),(.045,.09,.07),dark)
    for i in range(6):
        z=2.55+i*.27;y=.66 if i<3 else .63-(i-3)*.06
        box('Carved mane crest',(0,y,z),(.48,.30,.38),trim,.07,rotation=(.20,0,0))
    lathe('Knight collar',[(.85,.76),(.87,.86),(.80,.98)],trim)
def queen():
    base();stem(3.65)
    lathe('Queen crown cup',[(.60,3.60),(.66,3.86),(.85,4.10),(1.04,4.32),(1.08,4.52),(.84,4.52),(.64,4.02)])
    lathe('Crown circlet',[(1.065,4.35),(1.09,4.43),(1.06,4.50)],trim)
    for i in range(8):
        a=i*math.tau/8
        box('Crown prong',(.92*math.cos(a),.92*math.sin(a),4.78),(.23,.23,.73),bevel=.07)
        sphere('Crown pearl',(.92*math.cos(a),.92*math.sin(a),5.16),(.18,.18,.18),trim)
    lathe('Queen central finial',[(.35,4.27),(.30,4.59),(.17,4.91),(.10,5.38)])
    sphere('Royal finial',(0,0,5.44),(.20,.20,.20),trim)

collections=[]
for name,builder in [('Pawn',pawn),('Rook',rook),('Bishop',bishop),('Knight',knight),('Queen',queen)]:
    parts=[];builder()
    # Bake triangulation for stable cross-application import, including the horse's concave profile.
    for obj in parts:
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
        bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join();parts=[bpy.context.object];parts[0].name=name+' sculpted mesh'
    OUT.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE')
    # Track actual geometry budgets and bounds for review.
    points=[obj.matrix_world@v.co for obj in parts for v in obj.data.vertices]
    print('MODEL',name,'triangles',sum(len(o.data.polygons) for o in parts),'height',round(max(v.z for v in points),3))
    collections.append((name,parts[:]))

# Render the exported source geometry in both finishes; this is not a concept illustration.
black=material('Preview ebony',(.065,.085,.105),.15,.27)
for row in range(2):
    for col,(name,objects) in enumerate(collections):
        for original in objects:
            obj=original if row==0 else original.copy()
            if row==1:
                obj.data=original.data.copy();bpy.context.collection.objects.link(obj)
                for i,m in enumerate(obj.data.materials):
                    if m==body:obj.data.materials[i]=black
                obj.location.x-= (col-2)*3.5;obj.location.y-= -2.6
            obj.location.x+=(col-2)*3.5;obj.location.y+= -2.6 if row==0 else 3.0
floor=material('Backdrop',(.13,.16,.20),0,.65)
bpy.ops.mesh.primitive_plane_add(size=200);bpy.context.object.data.materials.append(floor);bpy.context.object.location.z=-.04
for pos,power,size in [((2,-9,15),2200,10),((-10,-2,9),1800,8),((3,9,14),2600,7)]:
    bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,2))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(12,-24,16));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,2.3))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=23
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(.25,.25,.25);scene.render.image_settings.file_format='PNG';scene.render.filepath=str(PREVIEW)
PREVIEW.parent.mkdir(parents=True,exist_ok=True);bpy.ops.render.render(write_still=True)
