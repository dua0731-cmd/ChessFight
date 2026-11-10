# Strip the faces Tripo sculpted on the user's pieces, so that every piece can take the same mesh eyes.
#   face_shells   - loose shells glued on the face (the rook's eye pills, glints and cheek discs, the knight's brows and
#                   cheeks): small, reaching the front, off the middle
#   carved_boxes  - where an eye is carved into the body itself (the knight): Tripo leaves it as non-manifold, dense
#                   geometry; one box per side around those edges and the dense vertices next to them
#   refill        - cut the boxes out, drop what the cut leaves loose, fill each hole with an even triangle patch and
#                   fair it (biharmonic, two rings of the surface around it held), so the head is smooth there
# All in Tripo's normalised units (the piece about 1 tall), before the piece is scaled into the lineup.
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt


def face_shells(parts, body):
    out = []
    for p in parts:
        if p is body:
            continue
        co = np.array([(p.matrix_world @ v.co)[:] for v in p.data.vertices])
        if np.ptp(co[:, 2]) < 0.15 and co[:, 1].min() < -0.04 and abs(co[:, 0].min() + co[:, 0].max()) / 2 > 0.03:
            out.append(p)
    return out


def carved_boxes(o, margin=0.006):
    me = o.data
    P = np.array([v.co[:] for v in me.vertices])
    z0, z1 = P[:, 2].min(), P[:, 2].max()
    bm = bmesh.new()
    bm.from_mesh(me)
    nm = np.array([((e.verts[0].co + e.verts[1].co) / 2)[:] for e in bm.edges if not e.is_manifold] or np.zeros((0, 3)))
    bm.free()
    E = np.array([e.vertices[:] for e in me.edges])
    L = np.linalg.norm(P[E[:, 0]] - P[E[:, 1]], axis=1)
    el, cnt = np.zeros(len(P)), np.zeros(len(P))
    np.add.at(el, E[:, 0], L); np.add.at(el, E[:, 1], L)
    np.add.at(cnt, E[:, 0], 1); np.add.at(cnt, E[:, 1], 1)
    el /= np.maximum(cnt, 1)
    boxes = []
    for s in (1, -1):
        pts = nm[(nm[:, 0] * s > 0.03) & (nm[:, 2] > z0 + 0.55 * (z1 - z0))] if len(nm) else nm
        if len(pts) < 4:
            continue
        c = pts.mean(0)
        dense = P[(el < 0.0018) & (np.linalg.norm(P - c, axis=1) < 0.07) & (P[:, 0] * s > 0.03)]
        allp = np.vstack([pts, dense]) if len(dense) else pts
        boxes.append((allp.min(0) - margin, allp.max(0) + margin))
    return boxes, len(nm)


def _inside(pts, poly):
    x, y = pts[:, 0][:, None], pts[:, 1][:, None]
    x1, y1 = poly[:, 0][None], poly[:, 1][None]
    x2, y2 = np.roll(poly[:, 0], -1)[None], np.roll(poly[:, 1], -1)[None]
    cross = ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / np.where(y2 == y1, 1e-30, y2 - y1) + x1)
    return cross.sum(1) % 2 == 1


def _seg_dist(pts, poly):
    a, b = poly[None], np.roll(poly, -1, 0)[None]
    p = pts[:, None]
    ab = b - a
    t = np.clip(((p - a) * ab).sum(2) / np.maximum((ab * ab).sum(2), 1e-30), 0, 1)
    return np.linalg.norm(p - (a + t[..., None] * ab), axis=2).min(1)


def _loops(bm):
    bnd = [e for e in bm.edges if e.is_boundary]
    seen, loops = set(), []
    for e0 in bnd:
        if e0 in seen:
            continue
        # walk against the old face's direction, so the loop runs the way a filling face must
        l = e0.link_loops[0]
        start, v = l.link_loop_next.vert, l.vert
        loop, e = [start], e0
        seen.add(e0)
        while v is not start and len(loop) < 100000:
            loop.append(v)
            nxt = [f for f in v.link_edges if f.is_boundary and f not in seen]
            if not nxt:
                break
            e = nxt[0]
            seen.add(e)
            v = e.other_vert(v)
        loops.append(loop)
    return loops


def refill(o, boxes):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    inbox = lambda co: any(all(lo[i] <= co[i] <= hi[i] for i in range(3)) for lo, hi in boxes)
    cut = [v for v in bm.verts if inbox(v.co)]
    cuts = set(cut)
    # the vertices just outside the cut: the loops through them are the holes the cut opens (the others are Tripo's
    # own small holes, under the mane or thin slits, which get a plain fill)
    rim = {e.other_vert(v) for v in cut for e in v.link_edges} - cuts
    bmesh.ops.delete(bm, geom=cut, context="VERTS")
    # pieces the cut left loose (the carving's inner shells): keep the largest connected piece only
    seen, comps = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        stack, members = [v], []
        seen.add(v)
        while stack:
            u = stack.pop()
            members.append(u)
            for e in u.link_edges:
                w = e.other_vert(u)
                if w not in seen:
                    seen.add(w)
                    stack.append(w)
        comps.append(members)
    comps.sort(key=len)
    loose = [v for c in comps[:-1] for v in c]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    print("REFILL cut", len(comps) - 1, "loose pieces,", len(loose), "verts")
    # the cut can leave a few faces hanging by one edge; trim wire edges and lone vertices
    bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context="EDGES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_edges], context="VERTS")
    new_inner, plain = [], []
    for loop in _loops(bm):
        if len(loop) < 3:
            continue
        if sum(1 for v in loop if v in rim) * 2 < len(loop):
            plain.append(loop)
            continue
        Q = np.array([v.co[:] for v in loop])
        # the head is low-poly round the carving: fill at the spacing of the hole's own edges
        h = 0.9 * np.linalg.norm(Q - np.roll(Q, -1, 0), axis=1).mean()
        c = Q.mean(0)
        _, _, Vt = np.linalg.svd(Q - c)
        U, V = Vt[0], Vt[1]
        uv = np.stack([(Q - c) @ U, (Q - c) @ V], 1)
        lo, hi = uv.min(0), uv.max(0)
        rows = []
        for j in range(int((hi[1] - lo[1]) / (h * 0.866)) + 2):
            for i in range(int((hi[0] - lo[0]) / h) + 2):
                rows.append((lo[0] + i * h + (j % 2) * h / 2, lo[1] + j * h * 0.866))
        g = np.array(rows) if rows else np.zeros((0, 2))
        g = g[_inside(g, uv)] if len(g) else g
        g = g[_seg_dist(g, uv) > 0.6 * h] if len(g) else g
        nb = len(uv)
        pts = np.vstack([uv, g])
        vo, eo, fo, ov, oe, of_ = delaunay_2d_cdt([Vector(p) for p in pts.tolist()], [(i, (i + 1) % nb) for i in range(nb)], [], 0, 1e-10)
        if any(len(x) != 1 for x in ov):
            print("REFILL loop of", nb, "crosses itself in its plane; left open")
            continue
        verts = list(loop) + [bm.verts.new(Vector((c + a * U + b * V).tolist())) for a, b in g]
        new_inner += verts[nb:]
        made = []
        for f in fo:
            ids = [ov[k][0] for k in f]
            cen = pts[ids].mean(0)
            if not _inside(cen[None], uv)[0]:
                continue
            try:
                made.append(bm.faces.new([verts[k] for k in ids]))
            except ValueError:
                pass
        # the loop runs the way a filling face must: flip the patch if its faces go round the other way
        a_, b_ = loop[0], loop[1]
        for f in made:
            vs = list(f.verts)
            if a_ in vs and b_ in vs:
                k = vs.index(a_)
                if vs[(k + 1) % len(vs)] is not b_:
                    bmesh.ops.reverse_faces(bm, faces=made)
                break
        print("REFILL loop", nb, "boundary verts,", len(g), "inner,", len(made), "faces")
    for loop in plain:
        edges = [e for e in bm.edges if e.is_boundary and e.verts[0] in loop and e.verts[1] in loop]
        res = bmesh.ops.holes_fill(bm, edges=edges, sides=0)
        bmesh.ops.triangulate(bm, faces=res["faces"])
    print("REFILL", len(plain), "small Tripo holes closed")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table()
    bm.verts.index_update()
    P = np.array([v.co[:] for v in bm.verts])
    nbrs = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
    F = [v.index for v in new_inner]
    if F:
        Fs = set(F)
        R1 = sorted({j for i in F for j in nbrs[i]} - Fs)
        R2 = sorted({j for i in R1 for j in nbrs[i]} - Fs - set(R1))
        Vl = F + R1 + R2
        idx = {v: k for k, v in enumerate(Vl)}
        nF, nFR = len(F), len(F) + len(R1)
        Lm = np.zeros((nFR, len(Vl)))
        for r, i in enumerate(F + R1):
            Lm[r, r] = -1.0
            for j in nbrs[i]:
                Lm[r, idx[j]] += 1.0 / len(nbrs[i])
        M = Lm[:nF, :nFR] @ Lm
        X = P[Vl]
        X[:nF] = np.linalg.solve(M[:, :nF], -M[:, nF:] @ X[nF:])
        for k, i in enumerate(F):
            bm.verts[i].co = Vector(X[k].tolist())
    bm.to_mesh(o.data)
    left = sum(1 for e in bm.edges if not e.is_manifold)
    bm.free()
    o.data.update()
    return len(F), left
