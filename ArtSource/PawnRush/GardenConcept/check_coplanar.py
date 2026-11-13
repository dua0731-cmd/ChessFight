# R438: lists faces of different pieces lying in one plane, facing the same way and overlapping (Unity flickers
# between them, PITFALLS 39); a hit may still be hidden inside another piece.
# R474: faces in any plane, not only the axis-aligned ones (the castle walls stand on slanting chords), and a pair counts
# only when the faces themselves overlap (their triangles' common area over 1 cm2), not just their bounding boxes:
# rotated blocks side by side in one plane overlapped as boxes (11334 false pairs on walls_sample_v2's walls).
#   blender -b --python check_coplanar.py -- <Sample.blend> "*"        (every mesh in the dressing collections)
import bpy, sys, bmesh
from collections import defaultdict
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
blend, names = args[0], args[1:]
bpy.ops.wm.open_mainfile(filepath=blend)
if names == ["*"]:
    names = [o.name for c in bpy.data.collections if "dressing" in c.name.lower() and not c.name.startswith("Context")
             for o in c.objects if o.type == "MESH"]
extra = []
if "Boats" in blend:
    for s in (-1, 1):
        for z in (35.4, 39.0, 42.6, 46.2):
            x0, x1 = (18.65, 18.85) if s > 0 else (-18.85, -18.65)
            for zz, sg in ((z - 1.2, -1), (z + 1.2, 1)):
                extra.append(("falls", -1, 1, sg, round(zz, 3), (x0, 3.0), (x1, 5.5)))   # R446: reservoir 3.0, eave 5.5

PLANE_TOL = 1e-3          # faces this close along their normal are in one plane
MIN_AREA = 1e-4           # 1 cm2 in common makes a pair


def area2(p):
    return .5 * sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p)))


def ccw(t):
    return t if area2(t) > 0 else t[::-1]


def clip_area(a, b):
    """The area common to two convex counter-clockwise polygons."""
    out = list(a)
    for i in range(len(b)):
        if len(out) < 3:
            return 0.
        p0, p1 = b[i], b[(i + 1) % len(b)]
        ex, ey = p1[0] - p0[0], p1[1] - p0[1]
        src, out = out, []
        for j in range(len(src)):
            p, q = src[j], src[(j + 1) % len(src)]
            sp = ex * (p[1] - p0[1]) - ey * (p[0] - p0[0])
            sq = ex * (q[1] - p0[1]) - ey * (q[0] - p0[0])
            if sp >= 0:
                out.append(p)
            if (sp >= 0) != (sq >= 0):
                t = sp / (sp - sq)
                out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return area2(out) if len(out) >= 3 else 0.


def box(pts):
    return (min(p[0] for p in pts), min(p[1] for p in pts)), (max(p[0] for p in pts), max(p[1] for p in pts))


def common(ta, tb):
    s = 0.
    for a in ta:
        alo, ahi = box(a)
        for b in tb:
            blo, bhi = box(b)
            if alo[0] >= bhi[0] or blo[0] >= ahi[0] or alo[1] >= bhi[1] or blo[1] >= ahi[1]:
                continue
            s += clip_area(a, b)
    return s


def basis(n):
    u = n.cross(Vector((0, 0, 1)) if abs(n.z) < .9 else Vector((1, 0, 0))).normalized()
    return u, n.cross(u)


# key: ("a", axis, sign) for axis-aligned faces (2D = the other two axes, as R438), ("g", rounded normal) for the rest;
# each entry (piece, component, face, d along the normal, lo, hi, triangles in 2D, a 3D point).
groups = defaultdict(list)
bases = {}
for name in names:
    ob = bpy.data.objects[name]
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    comp = {}
    cid = 0
    for v in bm.verts:
        if v.index in comp:
            continue
        stack = [v]
        comp[v.index] = cid
        while stack:
            a = stack.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in comp:
                    comp[b.index] = cid
                    stack.append(b)
        cid += 1
    tris = defaultdict(list)
    for t in bm.calc_loop_triangles():
        tris[t[0].face.index].append([l.vert.co.copy() for l in t])
    for f in bm.faces:
        if f.calc_area() < 1e-4:
            continue
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        p0 = f.verts[0].co
        if abs(n[ax]) >= .99999:         # truly axis-aligned (a wall 2 degrees off an axis is not: R474)
            key = ("a", ax, 1 if n[ax] > 0 else -1)
            o = [i for i in range(3) if i != ax]
            d = p0[ax]
            to2 = (lambda c, o=o: (c[o[0]], c[o[1]]))
        else:
            key = ("g", round(n.x, 3), round(n.y, 3), round(n.z, 3))
            if key not in bases:
                bases[key] = basis(Vector(key[1:]).normalized())
            u, w = bases[key]
            d = n.dot(p0)                 # the face's own normal: the rounded one is off by up to 5e-4 (5 cm at 70 m)
            to2 = (lambda c, u=u, w=w: (c.dot(u), c.dot(w)))
        t2 = [ccw([to2(c) for c in t]) for t in tris[f.index]]
        lo, hi = box([p for t in t2 for p in t])
        groups[key].append((name, comp[f.verts[0].index], f.index, d, lo, hi, t2, f.calc_center_median().copy()))
    bm.free()
for nm, c, ax, sg, plane, lo, hi in extra:
    rect = [lo, (hi[0], lo[1]), hi, (lo[0], hi[1])]
    groups[("a", 2 if ax == 2 else 1, sg)].append((nm, -1, -1, plane, lo, hi, [rect[:3], [rect[0], rect[2], rect[3]]], None))

bad = 0
kinds = defaultdict(int)
for key, fs in groups.items():
    fs.sort(key=lambda f: f[3])
    planes, cur = [], [fs[0]]
    for f in fs[1:]:
        if f[3] - cur[-1][3] <= PLANE_TOL:
            cur.append(f)
        else:
            planes.append(cur)
            cur = [f]
    planes.append(cur)
    for pl in planes:
        if len(pl) < 2:
            continue
        pl.sort(key=lambda f: f[4][0])
        for i in range(len(pl)):
            for j in range(i + 1, len(pl)):
                a, b = pl[i], pl[j]
                if b[4][0] >= a[5][0] - 1e-4:
                    break
                if a[0] == b[0] and a[1] == b[1]:
                    continue
                w = min(a[5][0], b[5][0]) - max(a[4][0], b[4][0])
                h = min(a[5][1], b[5][1]) - max(a[4][1], b[4][1])
                if w <= .01 or h <= .01:
                    continue
                s = common(a[6], b[6])
                if s > MIN_AREA:
                    bad += 1
                    kind = (key[0], key[1:] if key[0] == "g" else (key[1], key[2]), a[0], b[0])
                    kinds[kind] += 1
                    if kinds[kind] <= 3:
                        at = a[7] if a[7] is not None else b[7]
                        print("COPLANAR %s at %.3f: %s/%d and %s/%d %.4f m2 near %s" % (
                            key, a[3], a[0], a[1], b[0], b[1], s,
                            tuple(round(v, 2) for v in at) if at is not None else a[4]))
# Kinds: ("a", (axis, facing), piece, piece) or ("g", normal, piece, piece); up to three examples of each above.
for k, c in sorted(kinds.items(), key=lambda kc: -kc[1])[:20]:
    print("COPLANAR KIND %s: %d" % (k, c))
print("COPLANAR PAIRS", bad)
