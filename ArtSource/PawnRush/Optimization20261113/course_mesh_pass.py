import bpy, bmesh, math, json, os, sys, time
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = os.environ.get("CHESSFIGHT_ROOT", "C:/Work/ChessFight")
OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
ASSETS = [
 ("knight","Assets/Resources/PawnRushGrandStairHall/FallingKnight.fbx",.70),
 ("pad","Assets/Resources/PawnRushGrandStairHall/FloatingPad.fbx",.75),
 ("plaza","Assets/Resources/PawnRushGardenConcept/GardenPlaza.fbx",.85),
 ("trees","Assets/Resources/PawnRushGardenConcept/GardenTrees.fbx",.80),
 ("gate","Assets/Resources/PawnRushGardenConcept/GardenGate.fbx",.85),
 ("stonework","Assets/Resources/PawnRushGardenConcept/GardenLockDyke.fbx",.85),
 ("hall","Assets/Resources/PawnRushGrandStairHall/GrandStairHallV15.fbx",.85),
 ("boats","Assets/Resources/PawnRushGardenConcept/GardenBoats.fbx",.85),
 ("walls","Assets/Resources/PawnRushGardenConcept/GardenWalls.fbx",.85),
 ("platforms","Assets/Resources/PawnRushGardenConcept/GardenPlatforms.fbx",.85),
 ("canal","Assets/Resources/PawnRushGardenConcept/GardenCanal.fbx",.85),
]
def tris(mesh):
    return sum(len(p.vertices)-2 for p in mesh.polygons)
def meshes():
    return [o for o in bpy.context.scene.objects if o.type=="MESH"]
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True)
    return meshes()
def box(objects):
    vs=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
    return [min(v[i] for v in vs) for i in range(3)], [max(v[i] for v in vs) for i in range(3)]
FRAMES={}
def render(objects, path, direction=(4,-6,3), near=False):
    sc=bpy.context.scene
    for ob in meshes():
        ob.hide_render=ob not in objects
    lo,hi=box(objects)
    mid=(Vector(lo)+Vector(hi))*.5
    radius=(Vector(hi)-Vector(lo)).length*.5
    if near and len(objects)==1 and "flowers" in objects[0].name.lower():
        # Keep the complete bed; frame a close section on its southern arc.
        verts=[objects[0].matrix_world@v.co for v in objects[0].data.vertices]
        axis=max(range(3),key=lambda i:hi[i]-lo[i])
        extreme=max(verts,key=lambda p:p[axis])
        close=[p for p in verts if (p-extreme).length<1.0]
        mid=sum(close,Vector())/len(close)
        radius=1.8
    framekey=os.path.basename(path).replace("_before","").replace("_after","")
    if framekey in FRAMES: mid,radius=FRAMES[framekey]
    else: FRAMES[framekey]=(mid.copy(),radius)
    cd=bpy.data.cameras.new("Audit camera")
    cam=bpy.data.objects.new("Audit camera",cd);sc.collection.objects.link(cam)
    sc.camera=cam
    cam.location=mid+Vector(direction).normalized()*radius*3.3
    cam.rotation_euler=(mid-cam.location).to_track_quat("-Z","Y").to_euler()
    cd.type="ORTHO";cd.ortho_scale=radius*2.25;cd.clip_start=.001;cd.clip_end=max(1000,radius*20)
    sc.render.engine="BLENDER_WORKBENCH"
    sc.render.resolution_x=1400;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
    sh=sc.display.shading;sh.light="STUDIO";sh.studiolight_rotate_z=.4
    sh.color_type="MATERIAL";sh.show_shadows=True;sh.show_cavity=True
    sh.cavity_type="BOTH";sh.curvature_ridge_factor=1.1;sh.curvature_valley_factor=1.0
    sh.show_specular_highlight=True;sh.background_type="WORLD"
    if sc.world is None: sc.world=bpy.data.worlds.new("Audit world")
    sc.world.color=(.045,.045,.055)
    sc.view_settings.view_transform="Standard"
    sc.render.image_settings.file_format="PNG";sc.render.film_transparent=True
    sc.render.filepath=path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam,do_unlink=True)
    for ob in meshes(): ob.hide_render=False
def protect(ob, preserve_materials=True):
    bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update()
    bm.verts.ensure_lookup_table()
    locked=set()
    for e in bm.edges:
        if len(e.link_faces)!=2 or (preserve_materials and e.link_faces[0].material_index!=e.link_faces[1].material_index) or e.calc_face_angle(0)>(.35 if preserve_materials else 1.0):
            locked.update(v.index for v in e.verts)
    # Protect a ring around material and hard-edge boundaries.
    ring=set(locked)
    for v in bm.verts:
        if preserve_materials and v.index in locked:
            ring.update(w.index for e in v.link_edges for w in e.verts)
    bm.free()
    vg=ob.vertex_groups.new(name="Audit preserve boundaries")
    free=[v.index for v in ob.data.vertices if v.index not in ring]
    if free: vg.add(free,1.0,"REPLACE")
    if ring: vg.add(list(ring),0.0,"REPLACE")
    return vg
results=json.load(open(os.path.join(OUT,"results.json"),encoding="utf8")) if os.path.exists(os.path.join(OUT,"results.json")) else []
for key,rel,ratio in ASSETS:
    if os.environ.get("MESH_PASS_ONLY") and key not in os.environ["MESH_PASS_ONLY"].split(","): continue
    t=time.time()
    source=os.path.join(ROOT,rel)
    original=os.path.join(OUT,"original",rel)
    os.makedirs(os.path.dirname(original),exist_ok=True)
    import shutil
    if not os.path.exists(original): shutil.copy2(source,original)
    if not os.path.exists(original+".meta"): shutil.copy2(source+".meta",original+".meta")
    obs=load(original)
    inventory=[dict(name=o.name,tris=tris(o.data),vertices=len(o.data.vertices),materials=[m.name if m else "" for m in o.data.materials]) for o in obs]
    print("START",key,sum(x["tris"] for x in inventory),len(obs),flush=True)
    if key=="plaza":
        focus=[o for o in obs if "planter flowers" in o.name.lower()]
    elif key=="hall":
        focus=[o for o in obs if "Round_wall_0_1" in o.name]
    else: focus=obs
    if not focus: focus=obs
    render(focus,os.path.join(OUT,key+"_before.png"),near=key=="plaza")
    if key=="knight":render(focus,os.path.join(OUT,key+"_side_before.png"),(6,0,2))
    items=[]
    for ob in obs:
        n0=tris(ob.data)
        if n0<1000: continue
        if key=="plaza" and not any(s in ob.name.lower() for s in ["flower","planter"]): continue
        # Do not alter source object transforms, names, material slots, UVs or vertex colours.
        original_box=box([ob])
        vg=protect(ob, key!="trees")
        mod=ob.modifiers.new("Conservative visual reduction","DECIMATE")
        mod.decimate_type="COLLAPSE";mod.ratio=ratio
        if key not in ["knight","pad"]:
            mod.decimate_type="DISSOLVE";mod.angle_limit=math.radians(.05)
            mod.delimit={"MATERIAL","UV","NORMAL"};mod.use_dissolve_boundaries=False
        mod.vertex_group=vg.name;mod.vertex_group_factor=1000.0
        mod.use_collapse_triangulate=True
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
        ob.vertex_groups.remove(ob.vertex_groups[vg.name])
        items.append(dict(name=ob.name,before=n0,after=tris(ob.data),boundsBefore=original_box,boundsAfter=box([ob])))
    render(focus,os.path.join(OUT,key+"_after.png"),near=key=="plaza")
    if key=="knight":render(focus,os.path.join(OUT,key+"_side_after.png"),(6,0,2))
    bpy.ops.object.select_all(action="DESELECT")
    for ob in obs:ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    target=os.path.join(OUT,"candidate",rel)
    os.makedirs(os.path.dirname(target),exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=target,use_selection=True,object_types={"MESH"},apply_unit_scale=True,apply_scale_options="FBX_SCALE_NONE",axis_forward="-Z",axis_up="Y",bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type="FACE",add_leaf_bones=False,path_mode="STRIP",embed_textures=False,colors_type="LINEAR")
    result=dict(key=key,path=rel,ratio=ratio,method=("collapse_protected" if key in ["knight","pad"] else "planar_0.05deg"),before=sum(x["tris"] for x in inventory),after=sum(tris(o.data) for o in obs),inventory=inventory,items=items,seconds=time.time()-t)
    results=[r for r in results if r["key"]!=key]
    results.append(result)
    with open(os.path.join(OUT,"results.json"),"w",encoding="utf8") as f:json.dump(results,f,indent=2)
    print("DONE",key,result["before"],result["after"],"seconds",round(result["seconds"],1),flush=True)
