# Pass 4: faces of a thinned object whose own normal disagrees with the source surface nearest to them (folded or flipped).
import bpy, sys
from mathutils.bvhtree import BVHTree
args=sys.argv[sys.argv.index("--")+1:]
name,src,cands=args[0],args[1],args[2:]
def get(f):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=f,use_custom_normals=True)
    o=bpy.context.scene.objects[name];me=o.data
    vs=[o.matrix_world@v.co for v in me.vertices]
    return vs,[tuple(p.vertices) for p in me.polygons],[(o.matrix_world.to_3x3()@p.normal).normalized() for p in me.polygons],[p.area for p in me.polygons],[o.matrix_world@p.center for p in me.polygons]
sv,sp,sn,_,_=get(src);tree=BVHTree.FromPolygons(sv,sp)
for c in cands:
    v,p,n,a,ce=get(c);flip=0;fa=0;ta=sum(a);dist=0
    for i in range(len(p)):
        hit=tree.find_nearest(ce[i])
        if hit[0] is None: continue
        dist=max(dist,hit[3])
        if n[i].dot(sn[hit[2]])<-.3: flip+=1;fa+=a[i]
    print("FACE",c,"faces",len(p),"flipped",flip,"area%",round(100*fa/ta,2),"maxdist",round(dist,4))
