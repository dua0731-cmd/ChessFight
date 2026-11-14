# Drum check: the drawn drums' top surface height (ray down) at the middle and across, against the collider top
# (RoyalWaterGardenBuilder.PlatformTop(i).y = 2 + .25 i + .35), for one or more FBX files.
import bpy, sys, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
args=sys.argv[sys.argv.index("--")+1:]
for f in args:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=f,use_custom_normals=True)
    for i in range(8):
        o=bpy.context.scene.objects.get(f"Bobbing platform P{i+1}")
        if o is None: print("DRUM",f,i+1,"missing");continue
        dg=bpy.context.evaluated_depsgraph_get();tree=BVHTree.FromObject(o,dg)
        vs=[o.matrix_world@v.co for v in o.data.vertices]
        cx=sum(v.x for v in vs)/len(vs);cy=sum(v.y for v in vs)/len(vs);zmax=max(v.z for v in vs)
        # BVHTree.FromObject is in object space: cast in object space
        inv=o.matrix_world.inverted();hs=[]
        for r in (0,1,2,2.6):
            ring=[]
            for a in range(8 if r else 1):
                t=a*math.pi/4;w=Vector((cx+r*math.cos(t),cy+r*math.sin(t),zmax+2))
                d=(inv.to_3x3()@Vector((0,0,-1))).normalized()
                hit=tree.ray_cast(inv@w,d,10)
                if hit[0] is not None: ring.append((o.matrix_world@hit[0]).z)
            hs.append(round(sum(ring)/len(ring),3) if ring else None)
        top=2+.25*i+.35
        print("DRUM",f.split("/")[-1],f"P{i+1}","tris",sum(len(p.vertices)-2 for p in o.data.polygons),"zmax",round(zmax,3),"collider",round(top,3),"surface r0/1/2/2.6",hs)
