# Tutorial map trees and bushes (R834, 승규 11-25 "나무는 특히 아주 정밀 해야 해"): grown, not modelled by hand.
#
# A tree is a skeleton first - a trunk, then branches off branches (3 levels), each a polyline bent a little at every step
# (gravitropism up, a random wiggle, branches leaving at a golden-angle azimuth and a pitch from their parent) with its
# radius tapering towards its tip - then meshed twice:
#   LOD0  bark tubes, 12 / 8 / 6 / 4 sides by level, with parallel-transport frames so they do not twist; leaf cards
#         (TU_Leaves: a spray of leaves on a twig) hung at the twigs, the card's stem at the twig, its up along it;
#   LOD1  6 / 4 / 3 sides, no finest twigs, every third card 1.5x larger.
# Leaf cards carry normals pointing out of the crown (not their own), so the crown shades as one round mass and its far
# side never goes black. Origin at the trunk's foot, z up. Material slots: 0 bark (TU_Bark), 1 leaves (TU_Leaves,
# alpha-clipped, two-sided).
import bmesh, math
import numpy as np
from mathutils import Vector, Quaternion

GOLDEN = math.radians(137.5)

VARIANTS = {
    #           trunk h, trunk r, levels, children per level, pitch deg per level, length ratio, up pull, wiggle, cards per twig
    "TreeA":  dict(h=4.2, r=.30, levels=3, kids=((5, 7), (4, 6), (3, 5)), pitch=((40, 62), (30, 55), (25, 50)), ratio=(.62, .55, .5),
                   up=(.10, .05, .02), wiggle=(.06, .12, .18), start=(.45, .25, .2), cards=6, card=(.85, 1.3), first=5.2),
    "TreeB":  dict(h=5.4, r=.26, levels=3, kids=((6, 8), (3, 5), (3, 4)), pitch=((28, 42), (25, 45), (25, 45)), ratio=(.48, .55, .5),
                   up=(.16, .08, .03), wiggle=(.05, .10, .16), start=(.35, .25, .2), cards=6, card=(.8, 1.2), first=4.4),
    "TreeC":  dict(h=3.0, r=.22, levels=3, kids=((5, 6), (3, 5), (2, 4)), pitch=((48, 70), (30, 55), (25, 50)), ratio=(.62, .55, .5),
                   up=(.06, .04, .02), wiggle=(.07, .13, .18), start=(.55, .25, .2), cards=6, card=(.75, 1.15), first=3.6),
    "Bush":   dict(h=0., r=.08, levels=2, kids=((6, 8), (3, 5), (0, 0)), pitch=((35, 70), (25, 50), (0, 0)), ratio=(.55, .5, .5),
                   up=(.04, .02, .0), wiggle=(.10, .18, .2), start=(.3, .2, .2), cards=7, card=(.6, .95), first=1.5),
}


def skeleton(name, seed):
    """Branches as (points, radii, level) and leaf anchors as (position, direction, level)."""
    P = VARIANTS[name]
    rng = np.random.default_rng(seed)
    branches, anchors = [], []

    def grow(start, d, length, radius, level):
        n = max(3, int(length / (.32 if level < 2 else .22)))
        pts = [start]; radii = [radius]; d = d.normalized()
        for i in range(1, n + 1):
            w = P["wiggle"][min(level, 2)]
            d = (d + Vector(rng.normal(0, w, 3)) + Vector((0, 0, P["up"][min(level, 2)]))).normalized()
            pts.append(pts[-1] + d * (length / n))
            radii.append(radius * (1 - .72 * i / n))
        branches.append((pts, radii, level))
        last = level >= P["levels"]
        if last:
            for t in (.45, .75, 1.):
                i = min(n, int(round(t * n)))
                dd = (pts[i] - pts[max(0, i - 1)]).normalized()
                anchors.append((pts[i], dd, level))
            return
        lo, hi = P["kids"][level]
        k = int(rng.integers(lo, hi + 1))
        az0 = rng.uniform(0, 2 * math.pi)
        for j in range(k):
            t = P["start"][level] + (1 - P["start"][level]) * (j + rng.uniform(.2, .8)) / k
            i = min(n - 1, int(t * n))
            f = t * n - i
            p = pts[i].lerp(pts[i + 1], f)
            pd = (pts[i + 1] - pts[i]).normalized()
            side = pd.orthogonal().normalized()
            side = Quaternion(pd, az0 + GOLDEN * j) @ side
            pitch = math.radians(rng.uniform(*P["pitch"][level]))
            cd = (pd * math.cos(pitch) + side * math.sin(pitch)).normalized()
            cl = length * P["ratio"][level] * rng.uniform(.8, 1.15) * (1.15 - .5 * t if level > 0 else 1.)
            cr = (radius * (1 - .72 * t)) * .62
            grow(p, cd, cl, max(cr, .012), level + 1)
        if level >= 1:
            anchors.append((pts[-1], (pts[-1] - pts[-2]).normalized(), level))

    if P["h"] > 0:
        # the trunk: straight-ish, the first branches from `first` up its height, its top one more branch
        grow(Vector((0, 0, -.25)), Vector((0, 0, 1)), P["h"] + P["first"] * .6, P["r"], 0)
    else:
        # a bush: stems out of the ground
        lo, hi = P["kids"][0]
        for j in range(int(rng.integers(lo, hi + 1))):
            a = GOLDEN * j + rng.uniform(-.3, .3); pitch = math.radians(rng.uniform(*P["pitch"][0]))
            d = Vector((math.cos(a) * math.sin(pitch), math.sin(a) * math.sin(pitch), math.cos(pitch)))
            grow(Vector((math.cos(a) * .15, math.sin(a) * .15, -.1)), d, P["first"] * rng.uniform(.8, 1.2), P["r"], 1)
    return branches, anchors, rng


def tube(bm, uv, pts, radii, sides):
    rings = []; n_prev = None; vlen = 0.
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if n_prev is None:
            nrm = d.orthogonal().normalized()
        else:
            nrm = (n_prev - d * n_prev.dot(d)).normalized()
        b = d.cross(nrm)
        if i > 0: vlen += (pts[i] - pts[i - 1]).length
        ring = []
        for j in range(sides + 1):
            a = 2 * math.pi * j / sides
            ring.append((bm.verts.new(p + (nrm * math.cos(a) + b * math.sin(a)) * radii[i]), j / sides, vlen / 2.))
        rings.append(ring); n_prev = nrm
    for r0, r1 in zip(rings, rings[1:]):
        for j in range(sides):
            vs = [r0[j], r0[j + 1], r1[j + 1], r1[j]]
            f = bm.faces.new([v[0] for v in vs]); f.material_index = 0; f.smooth = True
            for loop, v in zip(f.loops, vs):
                loop[uv].uv = (v[1], v[2])
    # close the tip
    tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * radii[-1])
    last = rings[-1]
    for j in range(sides):
        f = bm.faces.new([last[j][0], last[j + 1][0], tip]); f.material_index = 0; f.smooth = True
        for loop, uvv in zip(f.loops, ((last[j][1], last[j][2]), (last[j + 1][1], last[j][2]), (last[j][1], last[j][2] + .05))):
            loop[uv].uv = uvv


def build(bm, name, seed, lod):
    """Meshes one variant into bm (two material slots). Returns the leaf faces' outward normal centre."""
    P = VARIANTS[name]
    branches, anchors, rng = skeleton(name, seed)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    sides0 = (12, 8, 6, 4) if lod == 0 else (6, 4, 3, 3)
    for pts, radii, level in branches:
        if lod == 1 and level >= 3: continue
        tube(bm, uv, pts, radii, sides0[min(level, 3)])
    crown = Vector()
    for p, _, _ in anchors: crown += p
    crown /= max(1, len(anchors))
    cards = []
    every = 1 if lod == 0 else 2
    grow = 1. if lod == 0 else 1.4
    k = 0
    for p, d, level in anchors:
        for c in range(P["cards"]):
            k += 1
            if k % every: continue
            s = rng.uniform(*P["card"]) * grow
            up = (d + Vector(rng.normal(0, .55, 3))).normalized()
            out = (p - crown); out.z *= .6
            side = up.cross(out if out.length > .01 else Vector((1, 0, 0)))
            if side.length < 1e-3: side = up.orthogonal()
            side = (Quaternion(up, rng.uniform(-1.2, 1.2)) @ side).normalized()
            base = p - up * s * .08
            q = [base - side * s * .5, base + side * s * .5, base + side * s * .5 + up * s, base - side * s * .5 + up * s]
            vs = [bm.verts.new(v) for v in q]
            f = bm.faces.new(vs); f.material_index = 1; f.smooth = True
            for loop, uvv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
                loop[uv].uv = uvv
            cards.append(f)
    # crown fill (11-25 "멀리서 보면 나무가 비어보여"): large cards spread through the crown's volume, so the gaps
    # between twig clusters read as foliage further in, not as sky - the more of them in the far LOD
    if anchors:
        pa = np.array([tuple(p) for p, _, _ in anchors])
        lo, hi = pa.min(0), pa.max(0)
        half = Vector(((hi - lo) * .5).tolist()) * .78
        fill = (28 if name != "Bush" else 10) * (1 if lod == 0 else 2)
        for i in range(fill):
            for _ in range(20):                      # a point inside the crown's ellipsoid
                q = Vector(rng.uniform(-1, 1, 3))
                if q.length <= 1: break
            p = crown + Vector((q.x * half.x, q.y * half.y, q.z * half.z))
            s = rng.uniform(1.5, 2.4) * (1. if name != "Bush" else .7) * (1. if lod == 0 else 1.25)
            up = (Vector((0, 0, 1)) + Vector(rng.normal(0, .5, 3))).normalized()
            side = up.orthogonal()
            side = (Quaternion(up, rng.uniform(0, math.pi)) @ side).normalized()
            base = p - up * s * .45
            quad = [base - side * s * .5, base + side * s * .5, base + side * s * .5 + up * s, base - side * s * .5 + up * s]
            vs = [bm.verts.new(v) for v in quad]
            f = bm.faces.new(vs); f.material_index = 1; f.smooth = True
            for loop, uvv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
                loop[uv].uv = uvv
            cards.append(f)
    return crown, cards


def make_object(name, seed, lod, mats, coll):
    import bpy
    bm = bmesh.new()
    crown, cards = build(bm, name, seed, lod)
    bm.faces.index_update()
    card_ids = {f.index for f in cards}
    me = bpy.data.meshes.new("TU_%s_LOD%d" % (name, lod))
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    # normals: bark keeps its smooth vertex normals; leaf cards point out of the crown (centre lifted a little, so the
    # under side of the crown faces down and out instead of straight down)
    me.update()
    centre = crown - Vector((0, 0, .6))
    vn = [v.normal.copy() for v in me.vertices]
    loops = []
    for poly in me.polygons:
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            if poly.index in card_ids:
                o = (me.vertices[vi].co - centre).normalized()
                loops.append(o)
            else:
                loops.append(vn[vi])
    me.normals_split_custom_set([tuple(n) for n in loops])
    o = bpy.data.objects.new(me.name, me); coll.objects.link(o)
    return o
