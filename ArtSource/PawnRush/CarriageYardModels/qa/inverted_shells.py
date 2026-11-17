# blender -b --factory-startup --python qa/inverted_shells.py -- <fbx dir> [names]
# Counts closed shells (every edge shared by two faces) whose signed volume is negative = faces pointing inward.
import sys, os
import bpy, bmesh

args = sys.argv[sys.argv.index("--") + 1:]
src = args[0]
names = args[1].split(",") if len(args) > 1 else sorted(f[:-4] for f in os.listdir(src) if f.endswith(".fbx"))
for n in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(src, n + ".fbx"))
    inv = inv_tris = closed = 0
    for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        seen = set()
        for f0 in bm.faces:
            if f0.index in seen:
                continue
            stack, shell = [f0], []
            seen.add(f0.index)
            while stack:
                f = stack.pop()
                shell.append(f)
                for e in f.edges:
                    for g in e.link_faces:
                        if g.index not in seen:
                            seen.add(g.index)
                            stack.append(g)
            edges = {e for f in shell for e in f.edges}
            if any(len(e.link_faces) != 2 for e in edges):
                continue
            closed += 1
            vol = 0.0
            for f in shell:
                vs = [v.co for v in f.verts]
                for i in range(1, len(vs) - 1):
                    vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
            if vol < -1e-9:
                inv += 1
                inv_tris += sum(len(f.verts) - 2 for f in shell)
        bm.free()
    print("RCY_INV %-22s closed shells %6d  inverted %4d  (%d tris)" % (n, closed, inv, inv_tris))
