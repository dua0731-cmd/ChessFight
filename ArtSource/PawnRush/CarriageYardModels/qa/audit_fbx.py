# blender -b --factory-startup --python qa/audit_fbx.py -- <fbx dir> <out json>
# Read-only audit of every carriage-yard FBX: triangles, zero-area faces, non-manifold edges, loose geometry, materials.
import sys, os, json
import bpy, bmesh

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
rows = []
for name in sorted(f for f in os.listdir(src) if f.lower().endswith(".fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(src, name))
    row = {"file": name, "objects": 0, "tris": 0, "zero_area": 0, "non_manifold_edges": 0,
           "boundary_edges": 0, "loose_verts": 0, "loose_edges": 0, "materials": set(), "skinned": 0,
           "size": None}
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for ob in bpy.data.objects:
        if ob.type != "MESH":
            continue
        row["objects"] += 1
        if any(m.type == "ARMATURE" for m in ob.modifiers) or ob.vertex_groups:
            row["skinned"] += 1
        for s in ob.material_slots:
            if s.material:
                row["materials"].add(s.material.name)
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.transform(ob.matrix_world)
        for f in bm.faces:
            row["tris"] += len(f.verts) - 2
            if f.calc_area() < 1e-10:
                row["zero_area"] += 1
        for e in bm.edges:
            n = len(e.link_faces)
            if n == 0:
                row["loose_edges"] += 1
            elif n == 1:
                row["boundary_edges"] += 1
            elif n > 2:
                row["non_manifold_edges"] += 1
        row["loose_verts"] += sum(1 for v in bm.verts if not v.link_edges)
        for v in bm.verts:
            for i in range(3):
                lo[i] = min(lo[i], v.co[i])
                hi[i] = max(hi[i], v.co[i])
        bm.free()
    row["materials"] = sorted(row["materials"])
    if row["objects"]:
        row["size"] = [round(hi[i] - lo[i], 2) for i in range(3)]
    row["bytes"] = os.path.getsize(os.path.join(src, name))
    rows.append(row)
    print("RCY_AUDIT", json.dumps(row))
with open(out, "w", encoding="utf-8") as fh:
    json.dump(rows, fh, indent=1)
