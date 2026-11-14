import bpy,numpy as np,os,json,sys
root=sys.argv[sys.argv.index("--")+1]
keys=["Falling_knight","Floating_pad","Exit_gate","Garden_tree_E1","Plaza_planter_flowers","Lock_and_dyke_stonework","HallV15_Round_wall_0_1","Tree_far"]
out=[]
for k in keys:
 a=bpy.data.images.load(os.path.join(root,"unity_"+k+"_before.png"),check_existing=False)
 b=bpy.data.images.load(os.path.join(root,"unity_"+k+"_after.png"),check_existing=False)
 pa=np.array(a.pixels[:],dtype=np.float32).reshape(-1,4);pb=np.array(b.pixels[:],dtype=np.float32).reshape(-1,4)
 ma=np.abs(pa[:,:3]-pa[0,:3]).max(axis=1)>.008;mb=np.abs(pb[:,:3]-pb[0,:3]).max(axis=1)>.008
 u=ma|mb
 out.append(dict(key=k,silhouetteIoU=float(np.sum(ma&mb)/max(1,np.sum(u))),meanForegroundRGBDifference=float(np.abs(pa[u,:3]-pb[u,:3]).mean())))
 bpy.data.images.remove(a);bpy.data.images.remove(b)
json.dump(out,open(os.path.join(root,"unity_visual_metrics.json"),"w"),indent=2)
print(json.dumps(out,indent=2))
