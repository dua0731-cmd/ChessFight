# Pass 4: why so many vertices were locked - counts per reason for the named objects, plus loose parts.
import bpy, bmesh, sys, json, math
args=sys.argv[sys.argv.index("--")+1:]
SRC=args[0];NAMES=args[1].split(",")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC,use_custom_normals=True)
out=[]
for o in bpy.context.scene.objects:
    if o.type!="MESH" or o.name not in NAMES: continue
    bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update()
    uv=bm.loops.layers.uv.active
    border=set();mat=set();ang={a:set() for a in (.35,.6,1.0,1.4)};seam=set()
    for e in bm.edges:
        f=e.link_faces;vs=[v.index for v in e.verts]
        if len(f)!=2: border.update(vs);continue
        if f[0].material_index!=f[1].material_index: mat.update(vs)
        a=e.calc_face_angle(0)
        for k in ang:
            if a>k: ang[k].update(vs)
        if uv is not None:
            for v in e.verts:
                A=[l[uv].uv for l in v.link_loops if l.face is f[0]];B=[l[uv].uv for l in v.link_loops if l.face is f[1]]
                if A and B and (A[0]-B[0]).length>1e-5: seam.update(vs);break
    # loose parts
    bm.verts.ensure_lookup_table();seen=set();parts=[]
    for v in bm.verts:
        if v.index in seen: continue
        st=[v];seen.add(v.index);n=0;fs=set()
        while st:
            x=st.pop();n+=1
            for f in x.link_faces: fs.add(f.index)
            for e in x.link_edges:
                y=e.other_vert(x)
                if y.index not in seen: seen.add(y.index);st.append(y)
        parts.append((n,len(fs)))
    parts.sort(reverse=True)
    mats=[s.material.name if s.material else None for s in o.material_slots]
    out.append(dict(name=o.name,verts=len(bm.verts),faces=len(bm.faces),tris=sum(len(f.verts)-2 for f in bm.faces),
        border=len(border),material=len(mat),angle={str(k):len(v) for k,v in ang.items()},uvseam=len(seam),
        parts=len(parts),biggestParts=parts[:8],smallParts=sum(1 for p in parts if p[1]<=24),mats=mats,
        hasUV=uv is not None,hasColor=len(o.data.color_attributes)))
    bm.free()
print("JSON"+json.dumps(out))
