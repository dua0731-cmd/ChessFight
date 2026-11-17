# blender -b --factory-startup --python qa/open_edges.py -- <fbx>
# Where are a mesh's open (boundary) edges? Groups them into connected loops and prints each loop's size, bounds,
# length, and whether its faces point down (an open underside) or the loop lies flat (a capped cut would not be open).
import sys
import bpy, bmesh

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
ob = [o for o in bpy.data.objects if o.type == "MESH"][0]
bm = bmesh.new()
bm.from_mesh(ob.data)
bm.transform(ob.matrix_world)
lo = [min(v.co[i] for v in bm.verts) for i in range(3)]
hi = [max(v.co[i] for v in bm.verts) for i in range(3)]
print("RCY_BND mesh bounds", [round(x, 2) for x in lo], [round(x, 2) for x in hi])
open_edges = [e for e in bm.edges if len(e.link_faces) == 1]
seen, loops = set(), []
for e in open_edges:
    if e.index in seen:
        continue
    stack, group = [e], []
    seen.add(e.index)
    while stack:
        cur = stack.pop()
        group.append(cur)
        for v in cur.verts:
            for n in v.link_edges:
                if n.index not in seen and len(n.link_faces) == 1:
                    seen.add(n.index)
                    stack.append(n)
    loops.append(group)
loops.sort(key=len, reverse=True)
for g in loops[:25]:
    vs = {v for e in g for v in e.verts}
    glo = [min(v.co[i] for v in vs) for i in range(3)]
    ghi = [max(v.co[i] for v in vs) for i in range(3)]
    length = sum(e.calc_length() for e in g)
    nz = sum(e.link_faces[0].normal.z for e in g) / len(g)
    print("RCY_BND loop edges %4d  len %6.2f m  x %6.2f..%6.2f  y %7.2f..%7.2f  z %5.2f..%5.2f  face normal z avg %+.2f"
          % (len(g), length, glo[0], ghi[0], glo[1], ghi[1], glo[2], ghi[2], nz))
print("RCY_BND loops", len(loops), "open edges", len(open_edges))
