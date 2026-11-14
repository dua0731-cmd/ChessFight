import bpy, numpy as np, os, json, sys
root=sys.argv[sys.argv.index("--")+1]
stats=[]
for key in ["knight","knight_side","pad","plaza","trees","gate","stonework","hall","boats","walls","platforms","canal"]:
    a=bpy.data.images.load(os.path.join(root,key+"_before.png"),check_existing=False)
    b=bpy.data.images.load(os.path.join(root,key+"_after.png"),check_existing=False)
    pa=np.array(a.pixels[:],dtype=np.float32).reshape(-1,4)
    pb=np.array(b.pixels[:],dtype=np.float32).reshape(-1,4)
    ma=pa[:,3]>.5; mb=pb[:,3]>.5
    u=ma|mb
    stats.append(dict(key=key,silhouetteIoU=float(np.sum(ma&mb)/max(1,np.sum(u))),meanForegroundRGBDifference=float(np.abs(pa[u,:3]-pb[u,:3]).mean()),changedPixelsOver5pct=float(np.mean(np.abs(pa[u,:3]-pb[u,:3]).max(axis=1)>.05))))
    bpy.data.images.remove(a);bpy.data.images.remove(b)
with open(os.path.join(root,"visual_metrics.json"),"w") as f:json.dump(stats,f,indent=2)
print(json.dumps(stats,indent=2))
