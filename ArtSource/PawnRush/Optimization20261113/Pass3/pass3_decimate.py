# Pass 3: protected collapse decimation with custom normals transferred from the source.
# Usage: blender -b --factory-startup --python pass3_decimate.py -- <source.fbx> <outdir> <ratios comma> [object-name-prefixes comma]
# Never writes into Assets; candidates go to <outdir>/candidates/<ratio>/.
import bpy, bmesh, sys, os, json, math
from mathutils import Vector
args=sys.argv[sys.argv.index("--")+1:]
SRC,OUT,RATIOS=args[0],args[1],[float(x) for x in args[2].split(",")]
PREFIX=args[3].split(",") if len(args)>3 and args[3] else None
os.makedirs(OUT,exist_ok=True)
VIEWS={"front":(0,-6,1.5),"side":(6,0,1.5),"back":(-3,6,2),"bottom":(2,-3,-6)}
def tris(m):return sum(len(p.vertices)-2 for p in m.polygons)
def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC,use_custom_normals=True)
    return [o for o in bpy.context.scene.objects if o.type=="MESH"]
EXCLUDE=os.environ.get("PASS3_EXCLUDE_SUFFIX")
def chosen(o):return (PREFIX is None or any(o.name.startswith(p) for p in PREFIX)) and not (EXCLUDE and o.name.endswith(EXCLUDE))
def protect(ob):
    bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update()
    uv=bm.loops.layers.uv.active
    locked=set()
    for e in bm.edges:
        f=e.link_faces
        hard=len(f)!=2 or f[0].material_index!=f[1].material_index or e.calc_face_angle(0)>.35
        if not hard and uv is not None:
            # UV seam: the two faces disagree on UVs at a shared vertex
            for v in e.verts:
                a=[l[uv].uv for l in v.link_loops if l.face is f[0]]
                b=[l[uv].uv for l in v.link_loops if l.face is f[1]]
                if a and b and (a[0]-b[0]).length>1e-5: hard=True;break
        if hard: locked.update(v.index for v in e.verts)
    bm.free()
    vg=ob.vertex_groups.new(name="pass3_preserve")
    free=[i for i in range(len(ob.data.vertices)) if i not in locked]
    if free: vg.add(free,1.0,"REPLACE")
    if locked: vg.add(list(locked),0.0,"REPLACE")
    return vg,len(locked)
def render(objs,path,d):
    sc=bpy.context.scene
    for o in sc.objects:
        if o.type=="MESH":o.hide_render=o not in objs
    vs=[o.matrix_world@Vector(c) for o in objs for c in o.bound_box]
    lo=Vector([min(v[i] for v in vs) for i in range(3)]);hi=Vector([max(v[i] for v in vs) for i in range(3)])
    mid=(lo+hi)*.5;r=(hi-lo).length*.5
    cd=bpy.data.cameras.new("c");cam=bpy.data.objects.new("c",cd);sc.collection.objects.link(cam);sc.camera=cam
    cam.location=mid+Vector(d).normalized()*r*3.3
    cam.rotation_euler=(mid-cam.location).to_track_quat("-Z","Y").to_euler()
    cd.type="ORTHO";cd.ortho_scale=r*2.25;cd.clip_start=.001;cd.clip_end=max(1000,r*20)
    sc.render.engine="BLENDER_WORKBENCH";sc.render.resolution_x=1200;sc.render.resolution_y=1200
    sh=sc.display.shading;sh.light="STUDIO";sh.color_type="MATERIAL";sh.show_shadows=True;sh.show_cavity=True
    sh.cavity_type="BOTH";sh.show_specular_highlight=True
    if sc.world is None: sc.world=bpy.data.worlds.new("w")
    sc.world.color=(.045,.045,.055);sc.view_settings.view_transform="Standard"
    sc.render.film_transparent=True;sc.render.filepath=path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam,do_unlink=True)
report=[]
for tag in ["source"]+[str(r) for r in RATIOS]:
    obs=load();targets=[o for o in obs if chosen(o) and tris(o.data)>=1000]
    entry=dict(tag=tag,before=sum(tris(o.data) for o in obs),items=[])
    if tag!="source":
        ratio=float(tag)
        for ob in targets:
            n0=tris(ob.data)
            src=ob.copy();src.data=ob.data.copy();bpy.context.scene.collection.objects.link(src);src.hide_render=True
            vg,nlock=protect(ob)
            m=ob.modifiers.new("dec","DECIMATE");m.decimate_type="COLLAPSE";m.ratio=ratio
            m.vertex_group=vg.name;m.vertex_group_factor=1000.0;m.use_collapse_triangulate=True
            bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=m.name)
            ob.vertex_groups.remove(ob.vertex_groups[vg.name])
            dt=ob.modifiers.new("nrm","DATA_TRANSFER");dt.object=src;dt.use_loop_data=True
            dt.data_types_loops={"CUSTOM_NORMAL"};dt.loop_mapping="POLYINTERP_NEAREST"
            bpy.ops.object.modifier_apply(modifier=dt.name)
            bpy.data.objects.remove(src,do_unlink=True)
            entry["items"].append(dict(name=ob.name,before=n0,after=tris(ob.data),lockedVerts=nlock))
        obs=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    entry["after"]=sum(tris(o.data) for o in obs)
    focus=targets if targets else obs
    for k,d in VIEWS.items(): render(focus,os.path.join(OUT,f"{k}_{tag}.png"),d)
    if tag!="source":
        bpy.ops.object.select_all(action="DESELECT")
        for o in obs:o.select_set(True)
        bpy.context.view_layer.objects.active=obs[0]
        t=os.path.join(OUT,"candidates",tag,os.path.basename(SRC))
        os.makedirs(os.path.dirname(t),exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=t,use_selection=True,object_types={"MESH"},apply_unit_scale=True,apply_scale_options="FBX_SCALE_NONE",axis_forward="-Z",axis_up="Y",bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type="FACE",add_leaf_bones=False,path_mode="STRIP",embed_textures=False,colors_type="LINEAR")
    report.append(entry);print("DONE",tag,entry["before"],entry["after"],flush=True)
    json.dump(report,open(os.path.join(OUT,"report.json"),"w"),indent=1)
