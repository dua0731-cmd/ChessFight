# Pass 4 (copy of pass 3, PASS4_FOCUS = one object to photograph): protected collapse decimation with custom normals transferred from the source.
# Usage: blender -b --factory-startup --python pass3_decimate.py -- <source.fbx> <outdir> <ratios comma> [object-name-prefixes comma]
# Never writes into Assets; candidates go to <outdir>/candidates/<ratio>/.
import bpy, bmesh, sys, os, json, math
from mathutils import Vector
args=sys.argv[sys.argv.index("--")+1:]
SRC,OUT,RATIOS=args[0],os.path.abspath(args[1]),[float(x) for x in args[2].split(",")]  # absolute: Blender's render path is not the shell's
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
# Pass 4: the garden's materials have no textures (URP Lit, no maps), so UV seams need no guard (PASS4_UVSEAM=0), and
# these sculpted meshes crease everywhere, so the angle guard is set apart (PASS4_ANGLE, radians; "none" = off).
ANG=os.environ.get("PASS4_ANGLE",".35");ANG=None if ANG=="none" else float(ANG)
USEUV=os.environ.get("PASS4_UVSEAM","1")!="0"
def protect(ob,only=None,angle="env"):
    # only: material name parts that may be thinned this step (the rest is held); angle: this step's angle guard
    ang=ANG if angle=="env" else angle
    bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update()
    uv=bm.loops.layers.uv.active if USEUV else None
    locked=set()
    if only:
        slots=[(s.material.name.lower() if s.material else "") for s in ob.material_slots]
        free_mats={i for i,n in enumerate(slots) if any(k in n for k in only)}
        for f in bm.faces:
            if f.material_index not in free_mats: locked.update(v.index for v in f.verts)
    for e in bm.edges:
        f=e.link_faces
        hard=len(f)!=2 or f[0].material_index!=f[1].material_index or (ang is not None and e.calc_face_angle(0)>ang)
        if not hard and uv is not None:
            # UV seam: the two faces disagree on UVs at a shared vertex
            for v in e.verts:
                a=[l[uv].uv for l in v.link_loops if l.face is f[0]]
                b=[l[uv].uv for l in v.link_loops if l.face is f[1]]
                if a and b and (a[0]-b[0]).length>1e-5: hard=True;break
        if hard: locked.update(v.index for v in e.verts)
    # PASS4_THIN (metres): hold vertices where the mesh is thinner than this (a leaf's flap): thinned, the flaps folded over
    # and showed their undersides
    thin=float(os.environ.get("PASS4_THIN","0") or 0)
    if thin>0:
        from mathutils.bvhtree import BVHTree
        tree=BVHTree.FromBMesh(bm)
        for v in bm.verts:
            n=v.normal
            if n.length<1e-6: continue
            hit=tree.ray_cast(v.co-n*1e-4,-n,thin)
            if hit[0] is not None: locked.add(v.index)
    # PASS4_KEEP: material name parts whose faces stay as they are (the trees' trunks went faceted when thinned)
    keep=[k.strip().lower() for k in os.environ.get("PASS4_KEEP","").split(",") if k.strip()]
    if keep:
        slots=[(s.material.name.lower() if s.material else "") for s in ob.material_slots]
        kept={i for i,n in enumerate(slots) if any(k in n for k in keep)}
        for f in bm.faces:
            if f.material_index in kept: locked.update(v.index for v in f.verts)
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
def collapse(ob,ratio,only=None,angle="env"):
    vg,nlock=protect(ob,only,angle)
    m=ob.modifiers.new("dec","DECIMATE");m.decimate_type="COLLAPSE";m.ratio=ratio
    m.vertex_group=vg.name;m.vertex_group_factor=1000.0;m.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=m.name)
    ob.vertex_groups.remove(ob.vertex_groups[vg.name])
    return nlock
def mat_tris(ob,keys):
    slots=[(s.material.name.lower() if s.material else "") for s in ob.material_slots]
    return sum(len(p.vertices)-2 for p in ob.data.polygons if any(k in slots[p.material_index] for k in keys))
# PASS4_STEPS: per-material thinning instead of one ratio, variants split by "|", steps by ";", each step
# "material parts(,):keep share:angle" - e.g. "leaf:.5:none;castle stone:.85:1.4" thins the leaves to half (stone held),
# then the stone to 85% (leaves held). The variants are tagged s0, s1, ... and the ratios argument is ignored.
STEPS=[[(s.split(":")[0].lower().split(","),float(s.split(":")[1]),None if s.split(":")[2]=="none" else float(s.split(":")[2]))
        for s in v.split(";")] for v in os.environ.get("PASS4_STEPS","").split("|") if v]
report=[]
for tag in ["source"]+([f"s{i}" for i in range(len(STEPS))] if STEPS else [str(r) for r in RATIOS]):
    obs=load();targets=[o for o in obs if chosen(o) and tris(o.data)>=1000]
    entry=dict(tag=tag,before=sum(tris(o.data) for o in obs),items=[])
    if tag!="source":
        for ob in targets:
            n0=tris(ob.data)
            src=ob.copy();src.data=ob.data.copy();bpy.context.scene.collection.objects.link(src);src.hide_render=True
            if STEPS:
                nlock=0
                for keys,share,ang in STEPS[int(tag[1:])]:
                    mt=mat_tris(ob,keys);now=tris(ob.data)
                    if mt<50: continue
                    nlock+=collapse(ob,(now-(1-share)*mt)/now,keys,ang)
            else:
                nlock=collapse(ob,float(tag))
            dt=ob.modifiers.new("nrm","DATA_TRANSFER");dt.object=src;dt.use_loop_data=True
            # PASS4_NMAP: how the source's normals are found (the nearest face could be a thin leaf's other side)
            dt.data_types_loops={"CUSTOM_NORMAL"};dt.loop_mapping=os.environ.get("PASS4_NMAP","POLYINTERP_NEAREST")
            bpy.ops.object.modifier_apply(modifier=dt.name)
            bpy.data.objects.remove(src,do_unlink=True)
            entry["items"].append(dict(name=ob.name,before=n0,after=tris(ob.data),lockedVerts=nlock))
        obs=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    entry["after"]=sum(tris(o.data) for o in obs)
    F=os.environ.get("PASS4_FOCUS");focus=[o for o in obs if o.name==F] if F else (targets if targets else obs)
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
