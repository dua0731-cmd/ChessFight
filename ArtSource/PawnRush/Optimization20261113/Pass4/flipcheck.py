# Pass 4: how many face corners have a shading normal pointing away from their face (dot < 0), per file, for one object.
import bpy, sys
args=sys.argv[sys.argv.index("--")+1:]
for f in args[1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=f,use_custom_normals=True)
    o=bpy.context.scene.objects[args[0]];m=o.data
    cn=m.corner_normals;bad=0;tot=0;soft=0
    for p in m.polygons:
        for li in p.loop_indices:
            d=cn[li].vector.dot(p.normal);tot+=1
            if d<0: bad+=1
            elif d<.3: soft+=1
    print("FLIP",f,args[0],"corners",tot,"away",bad,"grazing",soft)
