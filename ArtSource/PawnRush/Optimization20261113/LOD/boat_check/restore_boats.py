# Put the original reservoir boats (git HEAD) back into the current GardenBoats.fbx; everything else stays current.
import bpy,sys,re
a=sys.argv[sys.argv.index("--")+1:];HEAD,CUR,OUT=a
isboat=lambda n:re.fullmatch(r"Boat \d-\d",n) is not None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=HEAD,use_custom_normals=True)
for o in list(bpy.context.scene.objects):
    if not isboat(o.name):bpy.data.objects.remove(o,do_unlink=True)
keep={o.name for o in bpy.context.scene.objects}
bpy.ops.import_scene.fbx(filepath=CUR,use_custom_normals=True)
for o in list(bpy.context.scene.objects):
    base=re.sub(r"\.\d{3}$","",o.name)
    if o.name not in keep and isboat(base):bpy.data.objects.remove(o,do_unlink=True)
# one material per name: fold "X.001" back onto "X"
for o in bpy.context.scene.objects:
    for s in o.material_slots:
        m=s.material
        if m and re.search(r"\.\d{3}$",m.name):
            b=bpy.data.materials.get(re.sub(r"\.\d{3}$","",m.name))
            if b:s.material=b
bad=[o.name for o in bpy.context.scene.objects if re.search(r"\.\d{3}$",o.name)]
print("BAD",bad,flush=True)
obs=[o for o in bpy.context.scene.objects if o.type=="MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in obs:o.select_set(True)
bpy.context.view_layer.objects.active=obs[0]
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,object_types={"MESH"},apply_unit_scale=True,apply_scale_options="FBX_SCALE_NONE",axis_forward="-Z",axis_up="Y",bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type="FACE",add_leaf_bones=False,path_mode="STRIP",embed_textures=False,colors_type="LINEAR")
print("DONE",len(obs),sorted(o.name for o in obs if isboat(o.name)),sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in obs),flush=True)
