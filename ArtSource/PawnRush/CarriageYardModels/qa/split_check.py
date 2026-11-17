# blender -b --factory-startup --python qa/split_check.py -- <before dir> <after dir> <name1,name2,...>
# R681: checks a zone model cut into cells (rcy_kit.split_cells) against the uncut export: the cells, their triangles and
# extents along Unity z, and the shading - every corner normal of the cut model against the uncut one's at the same
# point (the largest angle; points the cut made, on the cell lines, have no partner and are counted apart).
import sys, os, math
import bpy

before, after, names = sys.argv[sys.argv.index("--") + 1:]


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    out = []
    for ob in bpy.data.objects:
        if ob.type != "MESH":
            continue
        me = ob.data
        mw = ob.matrix_world
        rot = mw.to_3x3().normalized()
        cos = [mw @ v.co for v in me.vertices]
        normals = [(rot @ n.vector).normalized() for n in me.corner_normals]
        corners = [(cos[l.vertex_index], normals[i]) for i, l in enumerate(me.loops)]
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        out.append((ob.name, tris, cos, corners))
    return out


def key(p):
    return (round(p.x, 4), round(p.y, 4), round(p.z, 4))


for n in names.split(","):
    old = load(os.path.join(before, n + ".fbx"))
    new = load(os.path.join(after, n + ".fbx"))
    table = {}
    for _, _, _, corners in old:
        for p, nv in corners:
            table.setdefault(key(p), []).append(nv)
    worst, over1, missing, total = 0.0, 0, 0, 0
    for name, tris, cos, corners in new:
        # Blender z here is Unity y (the FBX import turns the axes back), Blender y is Unity z.
        ys = [c.y for c in cos]
        print("RCY_CELL %-14s %-18s tris %6d  z %.2f .. %.2f" % (n, name, tris, min(ys), max(ys)))
        for p, nv in corners:
            total += 1
            cands = table.get(key(p))
            if not cands:
                missing += 1
                continue
            a = min(math.degrees(nv.angle(c, 0.0)) for c in cands)
            worst = max(worst, a)
            if a > 1.0:
                over1 += 1
    print("RCY_SPLIT %-14s cells %d (was %d)  tris %d -> %d  corners %d  worst normal %.3f deg  over 1 deg %d  new points (cell lines) %d"
          % (n, len(new), len(old), sum(o[1] for o in old), sum(o[1] for o in new), total, worst, over1, missing))
