import bpy, os, json, sys, numpy as np
root=sys.argv[sys.argv.index("--")+1]
results=json.load(open(os.path.join(root,"results.json"),encoding="utf8"))
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True)
    return {o.name: o for o in bpy.context.scene.objects if o.type=="MESH"}
def sig(o):
    me=o.data
    return dict(name=o.name,matrix=np.array(o.matrix_world).tolist(),materials=[m.name if m else "" for m in me.materials],uv=[l.name for l in me.uv_layers],colors=[(l.name,l.domain,l.data_type) for l in me.color_attributes],vertices=len(me.vertices),tris=sum(len(p.vertices)-2 for p in me.polygons))
out=[]
for r in results:
    if r["before"]==r["after"]:continue
    a={n:sig(o) for n,o in load(os.path.join(root,"original",r["path"])).items()}
    b={n:sig(o) for n,o in load(os.path.join(root,"candidate",r["path"])).items()}
    errors=[]
    if set(a)!=set(b):errors.append("object names changed")
    for n,s in a.items():
        if n not in b:continue
        t=b[n]
        if s["materials"]!=t["materials"]:errors.append(n+": material slots changed")
        if s["uv"]!=t["uv"]:errors.append(n+": UV layers changed")
        if s["colors"]!=t["colors"]:errors.append(n+": vertex colors changed")
        if np.max(np.abs(np.array(s["matrix"])-np.array(t["matrix"])))>1e-5:errors.append(n+": transform changed")
    out.append(dict(key=r["key"],path=r["path"],objects=len(a),errors=errors))
with open(os.path.join(root,"source_validation.json"),"w") as f:json.dump(out,f,indent=2)
print(json.dumps(out,indent=2))
if any(x["errors"] for x in out):raise Exception("Candidate structure mismatch")
