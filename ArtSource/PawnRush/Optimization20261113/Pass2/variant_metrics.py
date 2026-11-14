import bpy,numpy as np,os,json,sys
root=sys.argv[sys.argv.index("--")+1]
vs=json.load(open(os.path.join(root,"variants.json"),encoding="utf8"))
for r in vs:
 metrics=[]
 for side in ["","_side"]:
  key=r["key"]+side
  a=bpy.data.images.load(os.path.join(root,key+"_before.png"),check_existing=False);b=bpy.data.images.load(os.path.join(root,key+"_"+r["tag"]+"_after.png"),check_existing=False)
  pa=np.array(a.pixels[:],dtype=np.float32).reshape(-1,4);pb=np.array(b.pixels[:],dtype=np.float32).reshape(-1,4)
  ma=pa[:,3]>.5;mb=pb[:,3]>.5;u=ma|mb
  metrics.append(dict(view=side or "front",iou=float(np.sum(ma&mb)/max(1,np.sum(u))),diff=float(np.abs(pa[u,:3]-pb[u,:3]).mean())))
  bpy.data.images.remove(a);bpy.data.images.remove(b)
 r["metrics"]=metrics
json.dump(vs,open(os.path.join(root,"variant_metrics.json"),"w"),indent=2)
print(json.dumps([dict(key=r["key"],angle=r["angle"],saved=r["before"]-r["after"],metrics=r["metrics"]) for r in vs],indent=2))
