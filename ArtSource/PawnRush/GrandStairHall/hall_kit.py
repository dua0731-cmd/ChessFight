# The modelling kit shared by the hall's hand-built pieces (R361: lifted out of royal_balcony_v34.py, which keeps its
# own copy): materials Unity dresses by name, closed bmesh primitives (bevelled boxes, swept profiles with mitred
# corners, outlines round a polygon on a face, extrusions, lathes), moulding profiles, rosettes, the royal crown, and
# Part (one object per set of materials, triangulated, smooth with sharp edges by angle, tagged own_fbx).
# Use: sys.path.insert(0, HERE); from hall_kit import *
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
TAG = "HallKit"
sc = bpy.context.scene
PI = math.pi


# ------------------------------------------------------------------ materials (Unity dresses them by name)
def material(name, rgba, metal=0., rough=.5, emit=None):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m                                # the hall's own (Palace_Stone, Palace_Gold) as they are
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
        if emit:
            bsdf.inputs["Emission Color"].default_value = emit
            bsdf.inputs["Emission Strength"].default_value = 6.
    m.diffuse_color = rgba
    return m


STONE = material("Palace_Stone", (.90, .84, .72, 1), 0, .7)
GOLD = material("Palace_Gold", (.86, .64, .28, 1), .9, .3)
VELVET = material("Balcony_Velvet", (.42, .03, .06, 1), 0, .8)
WALNUT = material("Balcony_Walnut", (.30, .17, .10, 1), 0, .45)
IVORY = material("Balcony_Ivory", (.94, .90, .81, 1), 0, .25)
EBONY = material("Balcony_Ebony", (.07, .06, .07, 1), 0, .2)
STEEL = material("Balcony_Steel", (.07, .09, .16, 1), .9, .2)
RUBY = material("Balcony_Ruby", (.70, .04, .06, 1), 0, .3)
BULB = material("Balcony_Bulb", (1., .86, .6, 1), 0, .3, (1., .74, .45, 1))
MARBLE = material("Marble_Balcony", (.86, .85, .83, 1), 0, .25)


def P(x, y, z):
    return Vector((x, y, z))


# ------------------------------------------------------------------ primitives: each a small closed bmesh
def tidy(bm):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def box(lo, hi, bevel=0., segments=2):
    lo, hi = Vector([min(a, b) for a, b in zip(lo, hi)]), Vector([max(a, b) for a, b in zip(lo, hi)])
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co = Vector([lo[i] + (v.co[i] + .5) * (hi[i] - lo[i]) for i in range(3)])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, offset_type="OFFSET", segments=segments, profile=.5,
                        affect="EDGES", clamp_overlap=True)
    return bm


def sweep(path, outs, ups, prof, closed=False):
    """A profile (a along `outs`, b along `ups`, a closed polygon) swept along a path, capped at open ends."""
    bm = bmesh.new()
    rings = [[bm.verts.new(p + o * a + u * b) for a, b in prof] for p, o, u in zip(path, outs, ups)]
    n, m = len(prof), len(rings)
    for i in range(m if closed else m - 1):
        r0, r1 = rings[i], rings[(i + 1) % m]
        for j in range(n):
            k = (j + 1) % n
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))
    if not closed:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1][::-1])
    return bm


def mitre(seg_outs, n, closed=False):
    """Per-vertex offsets from per-segment out vectors, so a constant offset keeps parallel to every segment."""
    outs = []
    for i in range(n):
        if closed:
            a, b = seg_outs[i - 1], seg_outs[i % len(seg_outs)]
        elif i == 0:
            outs.append(seg_outs[0]); continue
        elif i == n - 1:
            outs.append(seg_outs[-1]); continue
        else:
            a, b = seg_outs[i - 1], seg_outs[i]
        outs.append((a + b) / max(1 + a.dot(b), 1e-3))
    return outs


def run(pts, seg_outs, z, prof, up=Vector((0, 0, 1))):
    """A horizontal moulding along a polyline of (x, y) at height z, `seg_outs` its segments' faces' normals."""
    path = [P(x, y, z) for x, y in pts]
    outs = mitre([Vector((o[0], o[1], 0)) for o in seg_outs], len(path))
    return sweep(path, outs, [up] * len(path), prof)


def outline(poly, origin, U, V, N, prof, closed=True):
    """A moulding round a polygon (u, v) drawn on a face (origin + U u + V v, N its outward normal): the profile's a
    along N, its b outward in the face, away from the polygon's inside."""
    area = sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
    sign = 1 if area > 0 else -1
    k = len(poly) if closed else len(poly) - 1
    segs = []
    for i in range(k):
        a, b = Vector(poly[i]), Vector(poly[(i + 1) % len(poly)])
        d = (b - a).normalized()
        segs.append(Vector((d.y, -d.x)) * sign)
    ins = mitre(segs, len(poly), closed)
    path = [origin + U * u + V * v for u, v in poly]
    ups = [U * n.x + V * n.y for n in ins]
    return sweep(path, [N] * len(path), ups, prof, closed)


def extrude(poly, origin, U, V, N, d0, d1):
    """A polygon (u, v) on a face, from d0 to d1 along N."""
    bm = bmesh.new()
    a = [bm.verts.new(origin + U * u + V * v + N * d0) for u, v in poly]
    b = [bm.verts.new(origin + U * u + V * v + N * d1) for u, v in poly]
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    for i in range(len(poly)):
        j = (i + 1) % len(poly)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    return bm


def basis(axis):
    axis = Vector(axis).normalized()
    e1 = Vector((1, 0, 0)) if abs(axis.x) < .9 else Vector((0, 1, 0))
    e1 = (e1 - axis * e1.dot(axis)).normalized()
    return axis, e1, axis.cross(e1)


def lathe(prof, centre, axis=(0, 0, 1), segs=24, a0=0., a1=2 * PI, closed_prof=False, e1=None):
    """A profile (r, h) turned round `axis` through `centre`; a part turn is capped at both cut planes."""
    axis, b1, b2 = basis(axis)
    if e1 is not None:
        b1 = Vector(e1).normalized(); b2 = axis.cross(b1)
    full = abs(a1 - a0 - 2 * PI) < 1e-6
    count = segs if full else segs + 1
    bm = bmesh.new()
    rings = []
    for i in range(count):
        t = a0 + (a1 - a0) * i / segs
        d = b1 * math.cos(t) + b2 * math.sin(t)
        rings.append([bm.verts.new(centre + axis * h + d * r) for r, h in prof])
    n = len(prof)
    for i in range(segs):
        r0, r1 = rings[i], rings[(i + 1) % count]
        for j in range(n if closed_prof else n - 1):
            k = (j + 1) % n
            bm.faces.new((r0[j], r0[k], r1[k], r1[j]))
    if not full:
        if not closed_prof:
            bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
        else:
            bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
    elif closed_prof is False and (prof[0][0] > 1e-6 or prof[-1][0] > 1e-6):
        pass                                    # an open tube: its rims stay open (always against something)
    return bm


def circle(ca, cb, r, n=8):
    return [(ca + r * math.cos(2 * PI * i / n), cb + r * math.sin(2 * PI * i / n)) for i in range(n)]


def arc(cx, cy, r, t0, t1, n):
    return [(cx + r * math.cos(t0 + (t1 - t0) * i / n), cy + r * math.sin(t0 + (t1 - t0) * i / n)) for i in range(n + 1)]


# ------------------------------------------------------------------ profiles (a = out from the face, b = up / outward)
def scaled(pts, sa, sb):
    return [(a * sa, b * sb) for a, b in pts]


def ogee(a0, b0, a1, b1, n=6):
    """A cyma recta from (a0, b0) to (a1, b1): concave below, convex above."""
    out = []
    for i in range(1, n):
        t = i / n
        s = .5 - .5 * math.cos(PI * t)
        out.append((a0 + (a1 - a0) * s, b0 + (b1 - b0) * t))
    return out


CORNICE = ([(-.02, 0), (.05, 0), (.05, .04), (.09, .06), (.1, .1), (.14, .12)] + ogee(.14, .12, .3, .32)
           + [(.3, .32), (.36, .34), (.36, .44), (.32, .46), (.32, .5), (-.02, .5)])
BASE = ([(-.02, 0), (.14, 0), (.14, .14)] + [(.10 + .045 * math.cos(t), .19 + .05 * math.sin(t)) for t in
        [-PI / 2 + PI * i / 6 for i in range(7)]] + [(.07, .25), (.055, .3), (.06, .34), (.04, .37), (.04, .45), (-.02, .45)])
ARCHITRAVE = [(-.02, 0), (.04, 0), (.04, .12), (.06, .13), (.06, .25), (.09, .27), (.1, .4), (-.02, .4)]
STRING = [(-.02, 0), (.06, 0), (.08, .03), (.12, .08), (.14, .16), (.14, .24), (.1, .28), (.1, .32), (-.02, .32)]
BAND = [(-.02, 0), (.05, 0), (.05, .04), (.09, .06), (.1, .1), (.1, .24), (.07, .28), (-.02, .28)]
BEAD = circle(.03, 0, .03, 8)
FINE = circle(.016, 0, .016, 6)


def rosette(centre, normal, r):
    """A gold rosette: a ring of petals round a boss, turned about the face's normal."""
    s = r / .2
    return lathe(scaled([(0, 0), (.2, 0), (.2, .012), (.17, .03), (.14, .02), (.11, .035), (.08, .03), (.06, .055),
                         (0, .065)], s, s), centre, normal, segs=20)


# ------------------------------------------------------------------ parts: one object per material set
class Part:
    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []

    def add(self, bm, mat):
        tidy(bm)
        if mat not in self.mats:
            self.mats.append(mat)
        for f in bm.faces:
            f.material_index = self.mats.index(mat)
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me); bm.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def done(self, coll, tag=TAG):
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me); self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(32))
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        ob["own_fbx"] = tag
        print("PART", self.name, len(me.polygons), "faces")
        return ob


X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def crown(p, base, k, jewels=True):
    """The royal crown (gold band and points, velvet cap, two arches, an orb and cross), base at its bottom middle."""
    def L(prof, h=0., segs=40, closed=False, mat=GOLD):
        p.add(lathe(scaled(prof, k, k), base + Z_ * h * k, Z_, segs, closed_prof=closed), mat)
    L([(.42, 0), (.47, 0), (.48, .04), (.47, .08), (.47, .24), (.49, .27), (.48, .3), (.42, .3)], closed=True)
    L([(0, .18), (.41, .18), (.40, .36), (.33, .56), (.2, .68), (0, .72)], mat=VELVET)
    L(circle(.47, .02, .025, 6), 0, 40, True)
    L(circle(.48, .29, .02, 6), 0, 40, True)
    for i in range(8):
        t = 2 * PI * i / 8
        d = Vector((math.cos(t), math.sin(t), 0))
        e = Vector((-math.sin(t), math.cos(t), 0))
        point = [(-.1, 0), (.1, 0), (.07, .2), (.035, .28), (0, .4), (-.035, .28), (-.07, .2)]
        p.add(extrude(scaled(point, k, k), base + d * .45 * k + Z_ * .27 * k, e, Z_, d, -.022 * k, .022 * k), GOLD)
        p.add(lathe(scaled([(0, -.045), (.03, -.035), (.045, 0), (.03, .035), (0, .045)], k, k),
                    base + d * .45 * k + Z_ * .71 * k, Z_, 12), MARBLE)
        if jewels:
            tj = t + PI / 8
            dj = Vector((math.cos(tj), math.sin(tj), 0))
            p.add(lathe(scaled([(0, 0), (.045, 0), (.04, .02), (.02, .035), (0, .04)], k, k),
                        base + dj * .47 * k + Z_ * .15 * k, dj, 12), RUBY if i % 2 else EBONY)
    for t0 in (0, PI / 2):
        d = Vector((math.cos(t0), math.sin(t0), 0))
        pts = [base + (d * .44 * math.cos(t) + Z_ * (.3 + .52 * math.sin(t))) * k for t in [PI * i / 24 for i in range(25)]]
        ups = [(d * math.cos(t) + Z_ * math.sin(t)).normalized() for t in [PI * i / 24 for i in range(25)]]
        side = d.cross(Z_)
        p.add(sweep(pts, [side] * 25, ups, scaled([(-.03, -.02), (.03, -.02), (.03, .02), (-.03, .02)], k, k)), GOLD)
    L([(0, -.1), (.06, -.085), (.095, -.04), (.1, 0), (.085, .05), (.05, .085), (0, .1)], .92, 20)
    p.add(box(base + P(-.02, -.02, 1.0) * k, base + P(.02, .02, 1.24) * k), GOLD)
    p.add(box(base + P(-.075, -.02, 1.1) * k, base + P(.075, .02, 1.145) * k), GOLD)




# ------------------------------------------------------------------ R361: the entrance's furniture (entrance_v46.py),
# shared with the foyer's side walls (foyer_sides_v47.py): plaques, crowns in relief, candelabra, banners,
# balustrades, posts with lanterns, pilasters, entablatures, rusticated masonry. What glows goes in `flames` (one
# Part; a script names it), the warm lights' places in `lights` (kind, x, y, z in Blender's frame).
RED = material("Entrance_Red", (.62, .03, .04, 1), 0, .7)
BRONZE = material("Entrance_Bronze", (.32, .18, .09, 1), .6, .35)
WAX = IVORY
lights = []
flames = Part("Flames")


# ------------------------------------------------------------------ small furniture
def crown_relief(p, origin, U, N, w, h, d0=0., d1=.025):
    """A flat crown (band and three points with pearls) standing proud of a face."""
    sil = [(-1, 0), (1, 0), (1, .72), (.66, .44), (.36, .96), (.1, .52), (0, .62), (-.1, .52), (-.36, .96), (-.66, .44), (-1, .72)]
    p.add(extrude([(u * w, v * h) for u, v in sil], origin, U, Z_, N, d0, d1), GOLD)
    for u, v in ((-1, .76), (-.36, 1.0), (.36, 1.0), (1, .76), (0, .66)):
        p.add(lathe([(0, 0), (.03, 0), (.03, .02), (0, .03)], origin + U * (u * w) + Z_ * (v * h), N, 8), GOLD)


def plaque(p, centre, U, N, w, h, crown=True):
    """A bronze plaque in a gold frame with a gold crown, on a face (centre on the face)."""
    rect = [(-w, -h), (w, -h), (w, h), (-w, h)]
    p.add(extrude(rect, centre, U, Z_, N, 0, .06), BRONZE)
    p.add(outline(rect, centre + N * .06, U, Z_, N, FINE), GOLD)
    if crown:
        crown_relief(p, centre + N * .06 - Z_ * (h * .55), U, N, w * .62, h * 1.0)


def sconce(p, at, N, arms=3):
    """A gold candelabrum on a wall: back plate, a stem, curved arms, cups, wax candles, flames."""
    U = Vector((-N.y, N.x, 0))
    p.add(lathe([(0, 0), (.16, 0), (.16, .02), (.11, .04), (.06, .06), (0, .07)], at, N, 20), GOLD)
    stem = [at + N * .06 - Z_ * .25, at + N * .16 + Z_ * .05, at + N * .2 + Z_ * .3]
    p.add(sweep(stem, [U] * 3, [N, (N + Z_).normalized(), Z_], circle(0, 0, .025, 6)), GOLD)
    p.add(lathe([(0, 0), (.05, 0), (.07, .04), (.05, .08), (0, .09)], at + N * .06 - Z_ * .32, Z_, 12), GOLD)
    tips = []
    for k in range(arms):
        side = (k - (arms - 1) / 2) * .26
        base = at + N * .2 + Z_ * .3
        tip = at + N * .28 + U * side + Z_ * (.42 + (.08 if side == 0 else 0))
        mid = (base + tip) / 2 + Z_ * -.12 + U * side * .2
        pts = [base, mid, tip]
        ups = [Z_, Z_, Z_]
        outs = [(pts[min(i + 1, 2)] - pts[max(i - 1, 0)]).normalized().cross(Z_).normalized() for i in range(3)]
        if side != 0:
            p.add(sweep(pts, outs, [o.cross((pts[2] - pts[0]).normalized()).normalized() for o in outs], circle(0, 0, .02, 6)), GOLD)
        tips.append(tip)
    for tip in tips:
        p.add(lathe([(0, 0), (.06, .01), (.07, .05), (.04, .06), (0, .06)], tip, Z_, 12), GOLD)
        p.add(lathe([(0, 0), (.022, 0), (.022, .2), (0, .2)], tip + Z_ * .05, Z_, 10), WAX)
        flames.add(lathe([(0, 0), (.022, .02), (.026, .05), (.014, .09), (0, .12)], tip + Z_ * .25, Z_, 10), BULB)
    lights.append(("sconce", *(at + N * .3 + Z_ * .55)))


def banner(p, top, N, w, h):
    """A red banner with a gold border and crown, swallow-tailed, from a gold rod on two brackets."""
    U = Vector((-N.y, N.x, 0))
    cloth = [(-w, 0), (w, 0), (w, -h), (0, -h * .82), (-w, -h)]
    p.add(extrude(cloth, top + N * .1, U, Z_, N, 0, .025), RED)
    inset = [(-w + .07, -.08), (w - .07, -.08), (w - .07, -h + .1), (0, -h * .82 + .08), (-w + .07, -h + .1)]
    p.add(outline(inset, top + N * .125, U, Z_, N, FINE), GOLD)
    crown_relief(p, top + N * .125 - Z_ * (h * .38), U, N, w * .45, w * .7, 0, .02)
    p.add(extrude([(-w, -.02), (w, -.02), (w, -.12), (-w, -.12)], top + N * .125, U, Z_, N, 0, .012), GOLD)
    rod = [top + N * .12 - U * (w + .18), top + N * .12 + U * (w + .18)]
    p.add(sweep(rod, [N, N], [Z_, Z_], circle(0, 0, .035, 8)), GOLD)
    for s in (-1, 1):
        p.add(lathe([(0, 0), (.06, .03), (.05, .09), (0, .14)], rod[0 if s < 0 else 1], U * s, 12), GOLD)
        p.add(box(top + U * (s * (w - .1)) - Z_ * .04 - U * .02, top + U * (s * (w - .1)) + N * .14 + Z_ * .04 + U * .02), GOLD)
    for u in (-w, w):
        p.add(lathe([(0, 0), (.03, 0), (.05, -.08), (.035, -.16), (0, -.18)], top + N * .11 + U * u - Z_ * h, Z_, 10), GOLD)


def baluster_run(p, a, b, z, n_hint=.32):
    """Balusters on a plinth rail under a top rail from a to b (2D points), the rail .44 wide, .95 high."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = b - a
    L = d.length
    d.normalize()
    side = Vector((-d.y, d.x, 0))
    p.add(box(a - side * .22 + Z_ * z, b + side * .22 + Z_ * (z + .14), .015), STONE)
    p.add(box(a - side * .2 + Z_ * (z + .78), b + side * .2 + Z_ * (z + .86), .01), STONE)
    p.add(box(a - side * .24 + Z_ * (z + .86), b + side * .24 + Z_ * (z + .95), .02), STONE)
    p.add(box(a - side * .245 + Z_ * (z + .84), b + side * .245 + Z_ * (z + .865)), GOLD)
    count = max(1, int(L / n_hint))
    prof = [(0, 0), (.08, 0), (.08, .04), (.055, .07), (.045, .13), (.075, .23), (.095, .32), (.075, .42), (.04, .5),
            (.045, .53), (.075, .55), (.075, .64), (0, .64)]
    for i in range(count):
        c = a + d * (L * (i + .5) / count) + Z_ * (z + .14)
        p.add(lathe(prof, c, Z_, 10), STONE)


def post(p, at, z, lantern=False, h=1.2):
    """A balustrade post .7 square: base, a bronze crown plaque on each face, a gold-banded cap; a lantern if asked."""
    c = Vector((at[0], at[1], 0))
    p.add(box(c + P(-.4, -.4, z), c + P(.4, .4, z + .16), .02), STONE)
    p.add(box(c + P(-.35, -.35, z + .16), c + P(.35, .35, z + h - .2), .015), STONE)
    p.add(box(c + P(-.37, -.37, z + h - .2), c + P(.37, .37, z + h - .14)), GOLD)
    p.add(box(c + P(-.42, -.42, z + h - .14), c + P(.42, .42, z + h), .02), STONE)
    for n in (X_, -X_, Y_, -Y_):
        U = Vector((-n.y, n.x, 0))
        plaque(p, c + n * .35 + Z_ * (z + .16 + (h - .36) / 2), U, n, .2, .26)
    if lantern:
        t = c + Z_ * (z + h)
        p.add(lathe([(0, 0), (.24, 0), (.24, .05), (.16, .1), (.12, .2), (.16, .24), (0, .24)], t, Z_, 16), GOLD)
        g = t + Z_ * .24
        for k in range(6):
            a0 = 2 * PI * k / 6 + PI / 6
            q = g + Vector((math.cos(a0), math.sin(a0), 0)) * .17
            p.add(box(q - P(.018, .018, 0), q + P(.018, .018, .46)), GOLD)
        flames.add(lathe([(0, 0), (.15, 0), (.17, .1), (.16, .36), (.13, .46), (0, .46)], g, Z_, 6), BULB)
        p.add(lathe([(0, .46), (.22, .46), (.22, .5), (.12, .64), (.05, .72), (.06, .78), (0, .86)], g, Z_, 6), GOLD)
        p.add(lathe([(0, 0), (.035, 0), (.05, .05), (.02, .1), (0, .16)], g + Z_ * .86, Z_, 10), GOLD)
        lights.append(("lantern", *(g + Z_ * .23)))


def pilaster(p, face, U, N, u0, u1, z0, z1, proj=.14):
    """A pilaster on a face: plinth, a shaft with a recessed panel lined in gold, a gold band, a capital block with a
    bronze crown plaque, a gold top band."""
    def B(a0, a1, b0, b1, d, mat=STONE, bev=.015):
        p.add(box(face + U * a0 + Z_ * b0, face + U * a1 + Z_ * b1 + N * d, bev), mat)
    B(u0 - .06, u1 + .06, z0, z0 + .55, proj + .06)
    B(u0 - .07, u1 + .07, z0 + .55, z0 + .6, proj + .07, GOLD, 0)
    B(u0, u1, z0 + .6, z1 - .9, proj)
    rect = [(u0 + .12, z0 + .9), (u1 - .12, z0 + .9), (u1 - .12, z1 - 1.15), (u0 + .12, z1 - 1.15)]
    p.add(outline(rect, face + N * proj, U, Z_, N, FINE), GOLD)
    B(u0 - .02, u1 + .02, z1 - .9, z1 - .84, proj + .03, GOLD, 0)
    B(u0 - .04, u1 + .04, z1 - .84, z1 - .12, proj + .06)
    plaque(p, face + U * ((u0 + u1) / 2) + Z_ * (z1 - .48) + N * (proj + .06), U, N, (u1 - u0) * .32, .22)
    B(u0 - .08, u1 + .08, z1 - .12, z1, proj + .1, GOLD, 0)


def entablature(p, pts, outs, z0, closed=False):
    """Architrave, gold bead, frieze, cornice along a run of faces from z0 (0.8 high) - or round a closed ring."""
    def R(prof, z):
        if closed:
            path = [P(x, y, z) for x, y in pts]
            o = mitre([Vector((v.x, v.y, 0)) for v in outs], len(path), True)
            return sweep(path, o, [Z_] * len(path), prof, True)
        return run(pts, outs, z, prof)
    p.add(R(scaled(ARCHITRAVE, 1, .75), z0), STONE)
    p.add(R(BEAD, z0 + .31), GOLD)
    p.add(R(scaled(CORNICE, .8, .9), z0 + .35), STONE)
    p.add(R(FINE, z0 + .33), GOLD)


def masonry(p, x0, x1, y0, y1, z0, z1, faces, course=.62, block=1.5):
    """A core box with rusticated blocks on the listed faces ('-y', '+y', '-x', '+x'), chamfered, joints offset."""
    p.add(box(P(x0, y0, z0), P(x1, y1, z1)), STONE)
    spec = {"-y": (P(x0, y0, 0), X_, -Y_, x1 - x0), "+y": (P(x1, y1, 0), -X_, Y_, x1 - x0),
            "-x": (P(x0, y1, 0), -Y_, -X_, y1 - y0), "+x": (P(x1, y0, 0), Y_, X_, y1 - y0)}
    for f in faces:
        o, U, N, L = spec[f]
        z, row = z0, 0
        while z < z1 - .05:
            zt = min(z + course, z1)
            cuts = [0.]
            u = block * (.5 if row % 2 else 1.)
            while u < L - .3:
                cuts.append(u); u += block
            cuts.append(L)
            for a, b in zip(cuts, cuts[1:]):
                p.add(box(o + U * (a + .02) + Z_ * (z + .02), o + U * (b - .02) + Z_ * (zt - .02) + N * .07, .035, 1), STONE)
            z, row = zt, row + 1


