# Grey clay kit of 승규's lobby asset sheet (asset_sheet.webp, 2026-11-11), one collection per part.
# Laid out exactly where each drawing sits on the sheet: 1 sheet pixel = 1 cm, sheet bottom = z 0,
# front faces look at -Y. Round and square parts are spun / stacked from the widths measured on the
# sheet (sheet_trace.py); knights, crowns, shields and brackets are the sheet's own outlines.
# Run inside Blender:  exec(open(kit_build.py).read(), {"__file__": path}); build()   or build(["B1", ...])
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix

KIT = os.path.dirname(os.path.abspath(__file__))
_T = {"__file__": os.path.join(KIT, "sheet_trace.py"), "__name__": "sheet_trace"}
exec(open(_T["__file__"], encoding="utf-8").read(), _T)
X, Z, U = _T["X"], _T["Z"], _T["U"]
rows, half_profile, centre, outline, rdp = _T["rows"], _T["half_profile"], _T["centre"], _T["outline"], _T["rdp"]
TAU = 2 * math.pi


# ---------------------------------------------------------------- scene bits
def clay():
    m = bpy.data.materials.get("Clay")
    if m is None:
        m = bpy.data.materials.new("Clay")
        m.use_nodes = True
        b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = (0.62, 0.62, 0.62, 1)
        b.inputs["Roughness"].default_value = 0.6
        m.diffuse_color = (0.62, 0.62, 0.62, 1)
    return m


# R576 (승규 "레퍼런스 사진 과 같이 컬러 텍스쳐 굽기"): faces can carry a paint tag (MB.mat) that becomes a material
# slot; Kit/hall_paint.py gives each slot its reference colour and bakes the textures. 0 = the object's own
# material (stone, wood, ...), the rest below.
PAINT_TAGS = {1: "Paint Gold", 2: "Paint Red"}
GOLD, RED = 1, 2


def paint_slot(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = clay().copy()
        m.name = name
    return m


def kit_root():
    c = bpy.data.collections.get("Kit")
    if c is None:
        c = bpy.data.collections.new("Kit")
        bpy.context.scene.collection.children.link(c)
    return c


def new_part(key):
    cname = "Kit " + key
    c = bpy.data.collections.get(cname)
    if c is not None:
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        c = bpy.data.collections.new(cname)
        kit_root().children.link(c)
    return c


# ---------------------------------------------------------------- mesh builder (metres)
class MB:
    def __init__(s):
        s.v, s.f = [], []
        s.M = Matrix.Identity(4)
        s.mat, s.fm = 0, []          # R576: paint tag for the faces added next, and each face's tag

    def add(s, verts, faces):
        o = len(s.v)
        s.v += [tuple(s.M @ Vector(p)) for p in verts]
        s.f += [tuple(i + o for i in f) for f in faces]
        s.fm += [s.mat] * len(faces)

    def box(s, x0, x1, y0, y1, z0, z1):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
             (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        s.add(v, [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)])

    def loft(s, rings, cap0=True, cap1=True):
        """Rings of equal length, closed loops; quads between them and fan caps at the ends."""
        n = len(rings[0])
        v, f = [], []
        for r in rings:
            v += r
        for i in range(len(rings) - 1):
            for k in range(n):
                a0, a1 = i * n + k, i * n + (k + 1) % n
                f.append((a0, a1, a1 + n, a0 + n))
        for ring_i, on in ((0, cap0), (len(rings) - 1, cap1)):
            if on:
                c = Vector((0, 0, 0))
                for p in rings[ring_i]:
                    c += Vector(p)
                v.append(tuple(c / n))
                ci = len(v) - 1
                f += [(ci, ring_i * n + k, ring_i * n + (k + 1) % n) for k in range(n)]
        s.add(v, f)

    def lathe(s, prof, cx, cy=0.0, seg=40, a0=0.0, sy=1.0):
        rings = [[(cx + r * math.cos(a0 + TAU * k / seg), cy + sy * r * math.sin(a0 + TAU * k / seg), z)
                  for k in range(seg)] for r, z in prof]
        s.loft(rings)

    def sq(s, prof, cx, cy=0.0, dr=1.0):
        rings = [[(cx - r, cy - r * dr, z), (cx + r, cy - r * dr, z), (cx + r, cy + r * dr, z), (cx - r, cy + r * dr, z)]
                 for r, z in prof]
        s.loft(rings)

    def prism(s, pts, y0, y1):
        """Polygon in the xz plane [(x, z)], extruded from y0 to y1. Caps of concave outlines
        (traced crowns, knights) are cut into triangles so they fill correctly."""
        n = len(pts)
        v = [(x, y0, z) for x, z in pts] + [(x, y1, z) for x, z in pts]
        if n <= 4:
            f = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
        else:
            from mathutils.geometry import tessellate_polygon
            tris = tessellate_polygon([[Vector((x, z, 0)) for x, z in pts]])
            f = [tuple(t) for t in tris] + [tuple(n + i for i in t[::-1]) for t in tris]
        f += [(k, (k + 1) % n, n + (k + 1) % n, n + k) for k in range(n)]
        s.add(v, f)

    def prism_plan(s, pts, z0, z1):
        """Polygon in the xy plane [(x, y)], extruded from z0 to z1 (concave caps triangulated)."""
        n = len(pts)
        v = [(x, y, z0) for x, y in pts] + [(x, y, z1) for x, y in pts]
        if n <= 4:
            f = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
        else:
            from mathutils.geometry import tessellate_polygon
            tris = tessellate_polygon([[Vector((x, y, 0)) for x, y in pts]])
            f = [tuple(t) for t in tris] + [tuple(n + i for i in t[::-1]) for t in tris]
        f += [(k, (k + 1) % n, n + (k + 1) % n, n + k) for k in range(n)]
        s.add(v, f)

    def strip(s, inner, outer, y0, y1):
        """Band between two open polylines in xz with the same point count, extruded in y."""
        n = len(inner)
        for k in range(n - 1):
            s.prism([inner[k], inner[k + 1], outer[k + 1], outer[k]], y0, y1)

    def sphere(s, c, r, seg=16, ring=10, sz=1.0):
        prof = [(r * math.sin(math.pi * i / ring), c[2] - sz * r * math.cos(math.pi * i / ring)) for i in range(ring + 1)]
        prof = [(max(p[0], 1e-5), p[1]) for p in prof]
        s.lathe(prof, c[0], c[1], seg)

    def torus(s, c, R, r, seg=24, segr=8, axis="z"):
        rings = []
        for i in range(seg):
            a = TAU * i / seg
            ring = []
            for j in range(segr):
                b = TAU * j / segr
                d = R + r * math.cos(b)
                p = (d * math.cos(a), d * math.sin(a), r * math.sin(b))
                if axis == "x":
                    p = (p[2], p[0], p[1])
                elif axis == "y":
                    p = (p[0], p[2], p[1])
                ring.append((c[0] + p[0], c[1] + p[1], c[2] + p[2]))
            rings.append(ring)
        v = [p for ring in rings for p in ring]
        f = []
        for i in range(seg):
            for j in range(segr):
                a, b = i * segr + j, i * segr + (j + 1) % segr
                c2, d = ((i + 1) % seg) * segr + (j + 1) % segr, ((i + 1) % seg) * segr + j
                f.append((a, b, c2, d))
        s.add(v, f)

    def tube(s, path, r, seg=8, caps=True):
        """Round tube along a 3D polyline (parallel-transport frames)."""
        path = [Vector(p) for p in path]
        t0 = (path[1] - path[0]).normalized()
        ref = Vector((0, 0, 1)) if abs(t0.z) < .9 else Vector((1, 0, 0))
        nrm = t0.cross(ref).normalized()
        rings = []
        for i, p in enumerate(path):
            if i == 0:
                t = t0
            elif i == len(path) - 1:
                t = (path[i] - path[i - 1]).normalized()
            else:
                t = (path[i + 1] - path[i - 1]).normalized()
            nrm = (nrm - t * nrm.dot(t)).normalized()
            b = t.cross(nrm)
            rings.append([tuple(p + (nrm * math.cos(TAU * k / seg) + b * math.sin(TAU * k / seg)) * r) for k in range(seg)])
        s.loft(rings, caps, caps)

    def obj(s, name, col, bevel=0.6, segs=2, sharp=35, smooth=True):
        me = bpy.data.meshes.new(name)
        me.from_pydata(s.v, [], s.f)
        tagged = any(s.fm)
        if tagged:
            me.polygons.foreach_set("material_index", s.fm)
        me.validate()
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        col.objects.link(o)
        me.materials.append(clay())
        if tagged:
            for k in range(1, max(s.fm) + 1):
                me.materials.append(paint_slot(PAINT_TAGS[k]))
        if smooth:
            me.shade_smooth()
            me.set_sharp_from_angle(angle=math.radians(sharp))
        if bevel:
            m = o.modifiers.new("Bevel", "BEVEL")
            m.width = bevel * U
            m.segments = segs
            m.limit_method = "ANGLE"
            m.angle_limit = math.radians(sharp)
            m.use_clamp_overlap = True
        return o


# ---------------------------------------------------------------- shared shapes (pixel inputs)
def arch_pts(cx, spring, a, b, n=24, kind="round", point=0.0):
    """Arch top from the left spring point over to the right, xz in metres.
    kind 'round' = semi-ellipse (a across, b up); 'pointed' = two arcs meeting at the top,
    point = how far each arc's centre sits past the middle (0 -> semicircle)."""
    pts = []
    for i in range(n + 1):
        t = i / n
        if kind == "round":
            ang = math.pi * (1 - t)
            pts.append((cx + a * math.cos(ang), spring + b * math.sin(ang)))
        else:
            R = a + point
            hb = math.sqrt(R * R - point * point)
            if t <= .5:   # left arc, centred right of the middle, up to the apex over cx
                c = cx + point
                ap = math.acos(-point / R)
                ang = math.pi - (math.pi - ap) * (t / .5)
            else:         # right arc, centred left of the middle, apex down to the right spring
                c = cx - point
                ap = math.acos(point / R)
                ang = ap * (1 - (t - .5) / .5)
            pts.append((c + R * math.cos(ang), spring + R * math.sin(ang) * b / hb))
    return pts


def scaled(pts, x0, z0, sx, sz, cx=None, cz=None):
    """Sheet-pixel outline -> metres, scaled about (cx, cz) (sheet px) and placed at (x0, z0) metres."""
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    if cx is None:
        cx = (min(xs) + max(xs)) / 2
    if cz is None:
        cz = max(ys)            # bottom of the outline
    return [(x0 + (p[0] - cx) * sx * U, z0 + (cz - p[1]) * sz * U) for p in pts]


def ccw(pts):
    a = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    return pts if a > 0 else pts[::-1]


def r_at(prof, z):
    """Radius of a bottom-to-top profile at height z (linear between its points)."""
    for (ra, za), (rb, zb) in zip(prof, prof[1:]):
        if min(za, zb) <= z <= max(za, zb) and abs(zb - za) > 1e-9:
            return ra + (rb - ra) * (z - za) / (zb - za)
    return min(prof, key=lambda p: abs(p[1] - z))[0]


def panel(mb, x0, x1, z0, z1, yf0, yf1=None, t=3, proud=1.5):
    """Raised rectangular frame on a front face (x, z in metres; face at y = yf0 at z0 .. yf1 at z1)."""
    if yf1 is None:
        yf1 = yf0
    t, proud = t * U, proud * U
    def fy(z):
        return yf0 + (yf1 - yf0) * (z - z0) / (z1 - z0)
    for (a, b, c, d) in ((x0, x1, z1 - t, z1), (x0, x1, z0, z0 + t), (x0, x0 + t, z0, z1), (x1 - t, x1, z0, z1)):
        za, zb = c, d
        y_a, y_b = fy(za), fy(zb)
        v = [(a, y_a - proud, za), (b, y_a - proud, za), (b, y_b - proud, zb), (a, y_b - proud, zb),
             (a, y_a + .002, za), (b, y_a + .002, za), (b, y_b + .002, zb), (a, y_b + .002, zb)]
        mb.add(v, [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)])


def around(mb, cx, cy, fn, sides=4):
    """Run fn(mb) for each side of a square part (front first), turning about the part's axis."""
    keep = mb.M.copy()
    for k in range(sides):
        mb.M = keep @ Matrix.Translation((cx, cy, 0)) @ Matrix.Rotation(k * math.pi / 2, 4, "Z") @ Matrix.Translation((-cx, -cy, 0))
        fn(mb)
    mb.M = keep


def crown_solid(mb, cx, cy, z0, R, H, seg=64):
    """3D crown like the sheet's: thick base ring, a lip, a wall whose top zigzags into 8 points
    (front/back tall with an orb and a spike, sides with a ball, short points between)."""
    k = H / 108.0
    band = [(R * .86, z0), (R * .9, z0 + 3 * k * U), (R * .9, z0 + 15 * k * U), (R * .84, z0 + 19 * k * U)]
    mb.lathe([(R * .6, z0)] + band + [(R * .6, z0 + 19 * k * U)], cx, cy, seg)
    mb.lathe([(R * .9, z0 + 19 * k * U), (R * 1.0, z0 + 21 * k * U), (R * 1.0, z0 + 29 * k * U), (R * .92, z0 + 31 * k * U),
              (R * .88, z0 + 31 * k * U), (R * .88, z0 + 19 * k * U)], cx, cy, seg)
    wb = z0 + 29 * k * U
    valley, short, side, tall = wb + 17 * k * U, wb + 35 * k * U, wb + 40 * k * U, wb + 52 * k * U
    peaks = [tall, short, side, short, tall, short, side, short]   # every 45 deg from the front (-Y)
    def top(a):
        u = (a / (TAU / 16)) % 16
        i = int(u)
        f = u - i
        if i % 2 == 0:   # from a peak down to a valley
            return peaks[(i // 2) % 8] * (1 - f) + valley * f
        return valley * (1 - f) + peaks[((i + 1) // 2) % 8] * f
    t = 2.5 * k * U
    outer = lambda a, z: (cx + (R + (z - wb) / (side - wb) * 3 * k * U) * math.sin(a), cy - (R + (z - wb) / (side - wb) * 3 * k * U) * math.cos(a), z)
    v, f = [], []
    for i in range(seg):
        a = TAU * i / seg
        zt = top(a)
        r_lo, r_hi = R * .96, R * .96 + 3 * k * U * (zt - wb) / (side - wb)
        v += [(cx + r_lo * math.sin(a), cy - r_lo * math.cos(a), wb), (cx + r_hi * math.sin(a), cy - r_hi * math.cos(a), zt),
              (cx + (r_hi - t) * math.sin(a), cy - (r_hi - t) * math.cos(a), zt), (cx + (r_lo - t) * math.sin(a), cy - (r_lo - t) * math.cos(a), wb)]
    for i in range(seg):
        a, b = i * 4, ((i + 1) % seg) * 4
        f += [(a, b, b + 1, a + 1), (a + 1, b + 1, b + 2, a + 2), (a + 2, b + 2, b + 3, a + 3), (a + 3, b + 3, b, a)]
    mb.add(v, f)
    for j, pk in enumerate(peaks):
        a = TAU * j / 8
        r_hi = R * .96 + 3 * k * U * (pk - wb) / (side - wb)
        px, py = cx + r_hi * math.sin(a), cy - r_hi * math.cos(a)
        if pk == tall:
            mb.sphere((px, py, pk + 4 * k * U), 7 * k * U, 12, 8)
            mb.lathe([(3 * k * U, pk + 9 * k * U), (2.4 * k * U, pk + 14 * k * U), (1e-4, pk + 21 * k * U)], px, py, 10)
        elif pk == side:
            mb.sphere((px, py, pk + 4 * k * U), 8 * k * U, 12, 8)


# ---------------------------------------------------------------- traced outlines (cached)
_OUT = {}


def traced(key, box, th=200, eps=.8):
    if key not in _OUT:
        _OUT[key] = outline(*box, th=th, eps=eps)
    return _OUT[key]


KNIGHT_BOX = (860, 554, 954, 714)
CROWN_BOX = (1089, 573, 1207, 681)
SHIELD_BOX = (418, 575, 519, 727)


def relief(mb, key, box, x0, z0, w, h, y_front, depth, mirror=False):
    """A traced sheet outline as a flat relief: fitted into w x h metres, bottom-centre at (x0, z0)."""
    pts = traced(key, box)
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    bw, bh = max(xs) - min(xs), max(ys) - min(ys)
    m = scaled(pts, x0, z0, w / (bw * U), h / (bh * U))
    if mirror:
        m = [(2 * x0 - x, z) for x, z in m]
    mb.prism(ccw(m), y_front - depth, y_front + .002)


# ---------------------------------------------------------------- row 2: posts, pedestals, blocks
def part_B1():
    c = new_part("B1 Baluster Large")
    mb = MB()
    box = (27, 343, 121, 545)
    cx = centre(*box)
    mb.lathe(half_profile(*box), X(cx))
    mb.obj("B1 Baluster Large", c, bevel=0)


def part_B2():
    c = new_part("B2 Baluster Small")
    mb = MB()
    box = (242, 349, 309, 540)
    cx = centre(*box)
    mb.lathe(half_profile(*box), X(cx))
    mb.obj("B2 Baluster Small", c, bevel=0)


def square_post(key, label, box, panel_box=None, dr=1.0, split=None):
    """Square post stacked from the sheet widths, inset panel on each side."""
    c = new_part(key + " " + label)
    mb = MB()
    cx = centre(*box)
    x1, y1, x2, y2 = box
    if split:   # round top above row `split` (P4's gold cushion)
        mb.lathe(half_profile(x1, y1, x2, split), X(cx))
        y1 = split
    prof = half_profile(x1, y1, x2, y2)
    mb.sq(prof, X(cx), 0, dr)
    if panel_box:
        px0, pz0, px1, pz1 = panel_box   # sheet px: left, top row, right, bottom row
        # half width of the body at the panel's top and bottom rows
        ra, rb = r_at(prof, Z(pz1)), r_at(prof, Z(pz0))
        hw = (px1 - px0) / 2 * U
        def one(m):
            panel(m, X(cx) - hw, X(cx) + hw, Z(pz1), Z(pz0), -ra * dr, -rb * dr)
        around(mb, X(cx), 0, one, 4 if dr == 1.0 else 1)
    o = mb.obj(key + " " + label, c)
    return o, cx, prof


def part_P1():
    square_post("P1", "Pilaster Square", (145, 351, 218, 540), (165, 397, 200, 485))


def part_P2():
    square_post("P2", "Pilaster Narrow", (333, 358, 399, 539), (355, 402, 382, 484))


def part_P3():
    square_post("P3", "Pedestal", (427, 370, 513, 533), (450, 424, 490, 480))


def part_M1():
    square_post("M1", "Cap Block", (540, 371, 627, 430), None, dr=.55)


def part_M2():
    square_post("M2", "Inset Block", (542, 465, 623, 520), (552, 472, 613, 503), dr=.5)


def part_P4():
    square_post("P4", "Small Pedestal", (1556, 404, 1648, 530), (1577, 445, 1627, 484), split=416)


def part_C1():
    c = new_part("C1 Crown Pedestal")
    mb = MB()
    box = (1423, 413, 1524, 531)
    cx = centre(*box)
    prof = half_profile(*box)
    mb.sq(prof, X(cx))
    r = lambda py: r_at(prof, Z(py))
    def diamond(m):
        # gold diamond on the tapered face: an octahedron flattened into the face
        dz0, dz1 = Z(482), Z(442)
        zc, h, w = (dz0 + dz1) / 2, (dz1 - dz0) / 2, 16.5 * U
        yf = -r(462)
        v = [(X(cx), yf - 4 * U, zc), (X(cx) - w, yf, zc), (X(cx), yf, zc + h), (X(cx) + w, yf, zc), (X(cx), yf, zc - h), (X(cx), yf + 2 * U, zc)]
        m.add(v, [(0, 1, 4), (0, 4, 3), (0, 3, 2), (0, 2, 1), (5, 4, 1), (5, 3, 4), (5, 2, 3), (5, 1, 2)])
    around(mb, X(cx), 0, diamond)
    # the crown on top (sheet: 75 px across its balls, 58 tall)
    crown_solid(mb, X(cx), 0, Z(413), 30 * U, 58)
    mb.obj("C1 Crown Pedestal", c, bevel=.4)


def part_R1():
    c = new_part("R1 Balustrade")
    mb = MB()
    # end posts: left edge of the left post measured, mirrored for the right one
    for ax in (1197, 1373):
        if ax == 1197:
            prof = half_profile(1170, 406, 1260, 497, axis=ax, side="left")
        else:
            prof = half_profile(1290, 406, 1400, 497, axis=ax, side="right")
        mb.sq(prof, X(ax), 0, .9)
        r = lambda py, prof=prof: r_at(prof, Z(py))
        def one(m, ax=ax):
            panel(m, X(ax) - 9 * U, X(ax) + 9 * U, Z(482), Z(440), -r(460) * .9)
        one(mb)
    mb.box(X(1212), X(1357), -10 * U, 10 * U, Z(437), Z(425))           # top rail
    mb.box(X(1182), X(1390), -14 * U, 14 * U, Z(497), Z(487))           # upper plinth
    mb.box(X(1174), X(1397), -17 * U, 17 * U, Z(519), Z(497))           # lower plinth
    for bx in (1235, 1270, 1305, 1338):
        cxx = centre(bx - 14, 445, bx + 14, 480)
        mb.lathe(half_profile(int(cxx) - 15, 438, int(cxx) + 15, 486), X(cxx), 0, 24)
    mb.obj("R1 Balustrade", c)


def ellipse_ring(mb, cx, spring, a_in, b_in, a_out, b_out, y0, y1, blocks=9, gap=1.0, n=6):
    """Arch ring of `blocks` voussoirs between two semi-ellipses (metres), small gaps between."""
    for i in range(blocks):
        t0, t1 = i / blocks, (i + 1) / blocks
        g = gap * U / (math.pi * (a_in + a_out) / 2)
        t0, t1 = t0 + (g if i else 0), t1 - (g if i < blocks - 1 else 0)
        inner, outer = [], []
        for j in range(n + 1):
            ang = math.pi * (1 - (t0 + (t1 - t0) * j / n))
            inner.append((cx + a_in * math.cos(ang), spring + b_in * math.sin(ang)))
            outer.append((cx + a_out * math.cos(ang), spring + b_out * math.sin(ang)))
        mb.prism(ccw(inner + outer[::-1]), y0, y1)


def keystone(mb, cx, z_bot, z_top, w_bot, w_top, y0, y1):
    mb.prism(ccw([(cx - w_bot / 2, z_bot), (cx + w_bot / 2, z_bot), (cx + w_top / 2, z_top), (cx - w_top / 2, z_top)]), y0, y1)


def part_A1():
    c = new_part("A1 Arch Large")
    mb = MB()
    cx, sp = X(747), Z(452)
    ellipse_ring(mb, cx, sp, 60 * U, 47 * U, 98 * U, 86 * U, -12 * U, 12 * U)
    ellipse_ring(mb, cx, sp, 96 * U, 84 * U, 102 * U, 90 * U, -14 * U, 10 * U, blocks=1, n=40)   # outer lip
    keystone(mb, cx, Z(405), Z(362), 20 * U, 28 * U, -15 * U, 13 * U)
    for s in (-1, 1):
        x0 = cx + s * 79 * U
        mb.box(x0 - 23 * U, x0 + 23 * U, -15 * U, 15 * U, Z(467), Z(452))   # impost cap
        mb.box(x0 - 20 * U, x0 + 20 * U, -12 * U, 12 * U, Z(490), Z(467))   # foot
        panel(mb, x0 - 14 * U, x0 + 14 * U, Z(486), Z(470), -12 * U, t=2, proud=1)
    mb.obj("A1 Arch Large", c)


def part_A2():
    c = new_part("A2 Arch Small")
    mb = MB()
    cx, sp = X(941), Z(452)
    ellipse_ring(mb, cx, sp, 45 * U, 35 * U, 71 * U, 59 * U, -10 * U, 10 * U, blocks=7)
    ellipse_ring(mb, cx, sp, 69 * U, 57 * U, 74 * U, 62 * U, -12 * U, 8 * U, blocks=1, n=40)
    keystone(mb, cx, Z(417), Z(388), 16 * U, 22 * U, -13 * U, 11 * U)
    for s in (-1, 1):
        x0 = cx + s * 58 * U
        mb.box(x0 - 15 * U, x0 + 15 * U, -12 * U, 12 * U, Z(471), Z(452))
    mb.obj("A2 Arch Small", c)


def part_M3():
    c = new_part("M3 Moulding")
    mb = MB()
    mb.box(X(1025), X(1152), -16 * U, 8 * U, Z(405), Z(390))
    mb.box(X(1032), X(1145), -11 * U, 8 * U, Z(420), Z(405))
    mb.obj("M3 Moulding", c)


def part_M4():
    c = new_part("M4 Corbel")
    mb = MB()
    mb.box(X(1057), X(1138), -16 * U, 10 * U, Z(462), Z(447))           # top slab
    mb.box(X(1065), X(1130), -12 * U, 10 * U, Z(471), Z(462))           # band
    mb.box(X(1107), X(1130), -10 * U, 10 * U, Z(529), Z(471))           # upright
    # the scroll bracket: traced outline of the part left of the upright, under the band
    pts = outline(1058, 471, 1108, 529)
    m = [(X(x), Z(y)) for x, y in pts]
    mb.prism(ccw(m), -7 * U, 7 * U)
    mb.obj("M4 Corbel", c)


# ---------------------------------------------------------------- row 3: banners, shield, lights, pieces
BANNERS = [  # (cloth left, right, side corner row, point row, rod left, rod right, crown?)
    (50, 122, 700, 745, 31, 137, True),
    (155, 216, 696, 740, 140, 227, False),
    (247, 309, 700, 745, 232, 320, False),
    (337, 396, 696, 740, 322, 410, False),
]


def part_BN():
    c = new_part("BN Banners")
    for i, (l, r, side, pt, rl, rr, crown) in enumerate(BANNERS):
        mb = MB()
        cxp = (l + r) / 2
        top = Z(576)
        cloth = [(X(l), Z(side)), (X(cxp), Z(pt)), (X(r), Z(side)), (X(r), top), (X(l), top)]
        mb.prism(ccw(cloth), -1.2 * U, 1.2 * U)
        # gold border inset 5 px, 3 px wide, standing 1.5 px proud on the front
        def inset(d):
            w2 = (r - l) / 2 - d
            # bottom point moves up by d / sin of the V's half angle
            half = math.atan2((r - l) / 2, pt - side)
            return [(X(cxp - w2), Z(side - d * .3)), (X(cxp), Z(pt - d / math.sin(half))),
                    (X(cxp + w2), Z(side - d * .3)), (X(cxp + w2), Z(586)), (X(cxp - w2), Z(586))]
        o5, o8 = inset(5), inset(8)
        loop5 = o5 + [o5[0]]
        loop8 = o8 + [o8[0]]
        mb.strip(loop8, loop5, -2.7 * U, -1.0 * U)
        # rod, sleeve, collars and ball ends
        mb.tube([(X(rl + 6), 0, Z(575)), (X(rr - 6), 0, Z(575))], 4.5 * U, 12)
        mb.tube([(X(l - 1), 0, Z(575)), (X(r + 1), 0, Z(575))], 7 * U, 16)
        for ex, d in ((rl, 1), (rr, -1)):
            mb.torus((X(ex + d * 14), 0, Z(575)), 5 * U, 2.2 * U, 16, 8, axis="x")
            mb.sphere((X(ex + d * 8), 0, Z(575)), 8 * U, 14, 10)
        if crown:
            relief(mb, "crown", CROWN_BOX, X(cxp), Z(680), 40 * U, 37 * U, -1.2 * U, 2.5 * U)
        mb.obj(f"BN Banner {i + 1}", c, bevel=.5)


def part_SH():
    c = new_part("SH Shield")
    mb = MB()
    pts = traced("shield", SHIELD_BOX, th=320)     # high: the lantern's glow spills onto its right edge
    out = ccw([(X(x), Z(y)) for x, y in pts])
    mb.prism(out, -5 * U, 5 * U)
    xs, zs = [p[0] for p in out], [p[1] for p in out]
    ccx, ccz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    w, h = max(xs) - min(xs), max(zs) - min(zs)
    def shrink(d):
        return [(ccx + (x - ccx) * (w - 2 * d * U) / w, ccz + (z - ccz) * (h - 2 * d * U) / h) for x, z in out]
    a, b = shrink(0), shrink(11)
    mb.strip(b + [b[0]], a + [a[0]], -8 * U, -4 * U)                    # raised rim
    relief(mb, "crown", CROWN_BOX, X(469), Z(683), 58 * U, 55 * U, -5 * U, 3.5 * U)
    for (rx, rz) in ((427, 598), (511, 600), (452, 708), (487, 708), (468, 584)):
        mb.sphere((X(rx), -8.5 * U, Z(rz)), 2.2 * U, 8, 6)
    mb.obj("SH Shield", c, bevel=.6)


def part_LT():
    c = new_part("LT Wall Lantern")
    mb = MB()
    ax = 577
    # back plate with a round boss, bolts
    mb.box(X(607), X(625), 2 * U, 8 * U, Z(722), Z(586))
    keep = mb.M
    mb.M = keep @ Matrix.Translation((X(616), 2 * U, Z(692.5))) @ Matrix.Diagonal((10 * U, 3 * U, 15.5 * U, 1))
    mb.sphere((0, 0, 0), 1.0, 16, 10)      # the oval boss the lower arm comes out of
    mb.M = keep
    # scroll arm over the top: from the plate out to a curl, the hook down to the cap
    arm = [(X(612), 0, Z(600))]
    for i in range(1, 13):
        t = i / 12
        arm.append((X(612 - 34 * t), 0, Z(600 - 18 * math.sin(math.pi * t * .8))))
    curl = [(X(574 + 9 * math.cos(a)), 0, Z(578 + 9 * math.sin(a))) for a in np.linspace(0, -1.6 * math.pi, 14)]
    mb.tube(arm + curl, 2.6 * U, 8, True)
    mb.tube([(X(ax), 0, Z(590)), (X(ax), 0, Z(598))], 2 * U, 8)
    # cap: knob, hexagonal roof, brim
    mb.sphere((X(ax), 0, Z(596)), 4 * U, 10, 6)
    mb.lathe([(1e-4, Z(626)), (40 * U, Z(626)), (40 * U, Z(622)), (36 * U, Z(619)), (11 * U, Z(603)), (6 * U, Z(600)), (1e-4, Z(600))], X(ax), 0, 6, a0=math.pi / 6)
    # body: hexagonal, wider at the top, corner posts, glass recessed
    mb.lathe([(1e-4, Z(680)), (21 * U, Z(680)), (30 * U, Z(630)), (1e-4, Z(630))], X(ax), 0, 6, a0=math.pi / 6)
    for k in range(6):
        a = math.pi / 6 + k * math.pi / 3
        mb.tube([(X(ax) + 31.5 * U * math.cos(a), 31.5 * U * math.sin(a), Z(630)), (X(ax) + 22.5 * U * math.cos(a), 22.5 * U * math.sin(a), Z(680))], 2.2 * U, 6)
    # base ring, bowl and finial
    mb.lathe([(1e-4, Z(697)), (8 * U, Z(697)), (22 * U, Z(688)), (25 * U, Z(684)), (25 * U, Z(679)), (1e-4, Z(679))], X(ax), 0, 6, a0=math.pi / 6)
    mb.lathe([(1e-4, Z(706)), (3 * U, Z(704)), (3 * U, Z(697)), (1e-4, Z(697))], X(ax), 0, 10)
    mb.sphere((X(ax), 0, Z(713)), 6.5 * U, 12, 8)
    # lower support arm from the boss to the base
    low = [(X(608), 0, Z(693))]
    for i in range(1, 11):
        t = i / 10
        low.append((X(608 - 31 * t), 0, Z(693 - 6 * math.sin(math.pi * t))))
    mb.tube(low, 2.4 * U, 8)
    mb.obj("LT Wall Lantern", c, bevel=.4)


def part_CH():
    c = new_part("CH Chandelier")
    mb = MB()
    ax = 748
    ring_z0, ring_z1 = Z(703), Z(685)
    mb.lathe([(72 * U, ring_z0), (85 * U, ring_z0), (87 * U, ring_z0 + 3 * U), (87 * U, ring_z1 - 3 * U), (85 * U, ring_z1),
              (72 * U, ring_z1), (70 * U, ring_z1 - 2 * U), (70 * U, ring_z0 + 2 * U)], X(ax), 0, 64)
    # candles on cups every 45 deg, spike finials between
    for k in range(8):
        a = -math.pi / 2 + k * TAU / 8
        px, py = X(ax) + 79 * U * math.cos(a), 79 * U * math.sin(a)
        z = ring_z1
        mb.lathe([(1e-4, z), (4 * U, z), (4 * U, z + 3 * U), (10 * U, z + 5 * U), (10 * U, z + 8 * U), (1e-4, z + 8 * U)], px, py, 16)
        mb.lathe([(8 * U, z + 8 * U), (8 * U, z + 31 * U), (1e-4, z + 31 * U)], px, py, 16)
        mb.lathe([(1e-4, z + 31 * U), (3.5 * U, z + 34 * U), (4 * U, z + 38 * U), (2 * U, z + 43 * U), (1e-4, z + 47 * U)], px, py, 10)
        a2 = a + TAU / 16
        qx, qy = X(ax) + 79 * U * math.cos(a2), 79 * U * math.sin(a2)
        mb.sphere((qx, qy, z + 4 * U), 4 * U, 10, 6)
        mb.lathe([(4 * U, z + 6 * U), (1e-4, z + 22 * U)], qx, qy, 8)
    # crown on top, collar, three chains to brackets on the ring
    crown_solid(mb, X(ax), 0, Z(573), 18 * U, 45)
    mb.lathe([(1e-4, Z(583)), (15 * U, Z(583)), (20 * U, Z(578)), (20 * U, Z(573)), (1e-4, Z(573))], X(ax), 0, 24)
    for a in (math.pi / 2, math.pi * 5 / 4, math.pi * 7 / 4):
        ex, ey = X(ax) + 58 * U * math.cos(a), 58 * U * math.sin(a)
        mb.box(ex - 3 * U, ex + 3 * U, ey - 3 * U, ey + 3 * U, ring_z1, ring_z1 + 5 * U)
        mb.tube([(ex, ey, ring_z1 + 2 * U), (X(ax) + 85 * U * math.cos(a), 85 * U * math.sin(a), ring_z1 + 2 * U)], 1.5 * U, 6)
        p0 = Vector((X(ax) + 6 * U * math.cos(a), 6 * U * math.sin(a), Z(583)))
        p1 = Vector((ex, ey, ring_z1 + 5 * U))
        L = (p1 - p0).length
        nlink = int(L / (5 * U))
        d = (p1 - p0).normalized()
        for j in range(nlink):
            pc = p0 + d * (j + .5) * L / nlink
            # a link lies along the chain, every other one turned a quarter
            side = d.cross(Vector((0, 0, 1))).normalized()
            if j % 2:
                side = d.cross(side).normalized()
            n = d.cross(side).normalized()
            rot = Matrix((side, d, n)).transposed().to_4x4()
            keep = mb.M
            mb.M = keep @ Matrix.Translation(pc) @ rot @ Matrix.Diagonal((1, 1.6, 1, 1))
            mb.torus((0, 0, 0), 2.0 * U, .7 * U, 10, 5)
            mb.M = keep
    # hub under the ring: four spokes, bowl and pendant
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        mb.tube([(X(ax) + 71 * U * math.cos(a), 71 * U * math.sin(a), ring_z0 + 4 * U), (X(ax) + 10 * U * math.cos(a), 10 * U * math.sin(a), Z(705))], 2 * U, 6)
    mb.lathe([(1e-4, Z(714)), (8 * U, Z(712)), (14 * U, Z(706)), (16 * U, Z(702)), (1e-4, Z(702))], X(ax), 0, 20)
    mb.lathe([(1e-4, Z(731)), (3 * U, Z(727)), (5 * U, Z(722)), (2 * U, Z(716)), (3 * U, Z(714)), (1e-4, Z(714))], X(ax), 0, 12)
    mb.obj("CH Chandelier", c, bevel=.3)


def inflate(mb, box, th, place_x, ref_cx, T, R, step=2.0, mirror=False, y_c=0.0):
    """Blow a sheet silhouette up into a rounded solid: exact outline, thickness 2T in the middle,
    rims rounded over R pixels (circle profile). Front and back share the outline vertices."""
    x1, y1, x2, y2 = box
    m = _T["filled"](x1, y1, x2, y2, th)
    H, W = m.shape
    pad = np.pad(m, 1)
    edge = m & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    bpts = np.stack(np.nonzero(edge), 1).astype(float)
    gy, gx = np.arange(0, H, step), np.arange(0, W, step)
    GY, GX = np.meshgrid(gy, gx, indexing="ij")
    inside = m[np.round(GY).astype(int).clip(0, H - 1), np.round(GX).astype(int).clip(0, W - 1)]
    P = np.stack([GY[inside], GX[inside]], 1)
    d = np.empty(len(P))
    for s0 in range(0, len(P), 256):
        dd = P[s0:s0 + 256, None, :] - bpts[None, :, :]
        d[s0:s0 + 256] = np.sqrt((dd ** 2).sum(2)).min(1)
    hh = np.zeros(GY.shape)
    dc = np.minimum(d, R)
    hh[inside] = T * np.sqrt(np.clip(1 - ((R - dc) / R) ** 2, 0, 1))
    ip = np.pad(inside, 1)
    bnd = inside & ~(ip[:-2, 1:-1] & ip[2:, 1:-1] & ip[1:-1, :-2] & ip[1:-1, 2:])
    ni, nj = GY.shape
    fi = -np.ones((ni, nj), int)
    bi = -np.ones((ni, nj), int)
    verts = []
    sgn = -1 if mirror else 1
    for i in range(ni):
        for j in range(nj):
            if not inside[i, j]:
                continue
            x, z = X(place_x + sgn * (x1 + GX[i, j] - ref_cx)), Z(y1 + GY[i, j])
            if bnd[i, j]:
                verts.append((x, y_c, z))
                fi[i, j] = bi[i, j] = len(verts) - 1
            else:
                h = hh[i, j] * U
                verts.append((x, y_c - h, z))
                fi[i, j] = len(verts) - 1
                verts.append((x, y_c + h, z))
                bi[i, j] = len(verts) - 1
    faces = []
    for i in range(ni - 1):
        for j in range(nj - 1):
            cs = [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]
            cs = [a for a in cs if inside[a]]
            if len(cs) < 3 or all(bnd[a] for a in cs):
                continue
            faces.append(tuple(fi[a] for a in cs))
            faces.append(tuple(bi[a] for a in cs[::-1]))
    mb.add(verts, faces)
    return lambda px, py: hh[int(round((py - y1) / step)), int(round((px - x1) / step))]


def knight(mb, cx, base_box, head_box, mirror=False):
    """Chess knight: the base spun from the sheet's widths, the head the sheet's own outline
    blown up round (about 40 % as thick as it is long, like the drawing)."""
    bcx = centre(*base_box)
    mb.lathe(half_profile(*base_box), X(cx), 0, 40)
    h = inflate(mb, head_box, 250, cx, bcx, T=19, R=21, step=1.5, mirror=mirror)
    sgn = -1 if mirror else 1
    ex, ey = 922, 586     # the eye on the drawing
    for side in (-1, 1):
        mb.sphere((X(cx + sgn * (ex - bcx)), side * (h(ex, ey) - .8) * U, Z(ey)), 3.2 * U, 10, 6)


def part_KN():
    c = new_part("KN Knights")
    for name, cx, mirror in (("KN Knight White", 907, False), ("KN Knight Black", 1019, True)):
        mb = MB()
        knight(mb, cx, (860, 676, 954, 714), (860, 554, 954, 682), mirror)
        mb.obj(name, c, bevel=0, sharp=80)


def part_CR():
    c = new_part("CR Crown")
    mb = MB()
    crown_solid(mb, X(1148), 0, Z(680), 48 * U, 108)
    mb.obj("CR Crown", c, bevel=.3)


def part_BD():
    """The chess board stage with its carpet steps (Unity draws its own; reference only)."""
    c = new_part("BD Board Stage")
    mb = MB()
    cx, half, ch = X(1436), 219 * U, 26 * U
    top, rim_z, base_z = Z(647), Z(667), Z(710)
    depth_c = half + 10 * U          # stage centre sits behind the front face
    def octo(h, cut):
        return [(cx - h + cut, depth_c - h), (cx + h - cut, depth_c - h), (cx + h, depth_c - h + cut), (cx + h, depth_c + h - cut),
                (cx + h - cut, depth_c + h), (cx - h + cut, depth_c + h), (cx - h, depth_c + h - cut), (cx - h, depth_c - h + cut)]
    mb.prism_plan(octo(half, ch), rim_z, top)                           # rim
    mb.prism_plan(octo(half - 8 * U, ch - 3 * U), base_z, rim_z)        # plinth
    mb.prism_plan(octo(half - 26 * U, ch - 12 * U), top - 1 * U, top + .5 * U)   # board inlay line
    for s in (-1, 1):   # gold-framed panels on the plinth front
        px = cx + s * 157 * U
        panel(mb, px - 33 * U, px + 33 * U, Z(697), Z(671), depth_c - half + 8 * U, t=2.5, proud=1.2)
    # three steps up to the stage, a carpet with two stripes, the crown on the rim
    rise, tread = (base_z - top) / 3 * -1, 25 * U
    y_front = depth_c - half
    for k in range(3):
        w = (145 - k * 11) * U
        y0 = y_front - (3 - k) * tread
        mb.box(cx - w, cx + w, y0, y_front, base_z, base_z + rise * (k + 1))
        mb.box(cx - 105 * U, cx + 105 * U, y0 - 1 * U, y_front, base_z + rise * (k + 1), base_z + rise * (k + 1) + 1.2 * U)
        mb.box(cx - 105 * U, cx + 105 * U, y0 - 1.2 * U, y0, base_z + rise * k, base_z + rise * (k + 1) + 1.2 * U)
        for sx in (-97, 97):
            mb.box(cx + (sx - 3) * U, cx + (sx + 3) * U, y0 - 2 * U, y_front, base_z + rise * (k + 1) + 1 * U, base_z + rise * (k + 1) + 2 * U)
            mb.box(cx + (sx - 3) * U, cx + (sx + 3) * U, y0 - 2.2 * U, y0 - 1 * U, base_z + rise * k, base_z + rise * (k + 1) + 2 * U)
    relief(mb, "crown", CROWN_BOX, cx, rim_z + 1 * U, 36 * U, 32 * U, y_front, 3 * U)
    mb.obj("BD Board Stage", c, bevel=.6)


# ---------------------------------------------------------------- row 1: walls, gate, door, windows
FIELD_ROW = 27
BLOCK_W = 60


def wall_bands(mb, x0, x1, top, bot, y_face=0.0, depth=30):
    """Wall body and its horizontal mouldings between sheet rows top and bot (W1 layout)."""
    d = depth * U
    mb.box(x0, x1, y_face, y_face + d, Z(bot), Z(top))
    for (r0, r1, proud) in ((top, top + 22, 6), (top + 42, top + 57, 4), (bot - 50, bot - 35, 4), (bot - 35, bot, 6)):
        mb.box(x0, x1, y_face - proud * U, y_face + d, Z(r1), Z(r0))


def wall_blocks(mb, x0, x1, z_lo, z_hi, y_face, skip=None):
    """Stone blocks on a wall face (running bond), 1 px proud with 1 px joints.
    skip(xa, xb, za, zb) -> list of (xa, xb) pieces to keep for that block."""
    g = 1.0 * U
    r = 0
    z = z_lo
    while z < z_hi - 1e-6:
        zb = min(z + FIELD_ROW * U, z_hi)
        off = (BLOCK_W / 2 * U) if r % 2 else 0.0
        x = x0 - off
        while x < x1 - 1e-6:
            xa, xb = max(x, x0), min(x + BLOCK_W * U, x1)
            pieces = [(xa, xb)] if skip is None else skip(xa, xb, z, zb)
            for pa, pb in pieces:
                if pb - pa > 4 * U:
                    mb.box(pa + g / 2, pb - g / 2, y_face - 1.0 * U, y_face + .2 * U, z + g / 2, zb - g / 2)
            x += BLOCK_W * U
        z = zb
        r += 1


def column_round(mb, ax, top, bot, axis_y, x_lo):
    prof = half_profile(x_lo, top, int(ax) + 1, bot, axis=ax, side="left")
    mb.lathe(prof, X(ax), axis_y, 40)
    return prof


def right_pilaster(mb, x1, top, bot):
    mb.box(X(x1 - 22), X(x1), -5 * U, 30 * U, Z(bot), Z(top + 57))
    mb.box(X(x1 - 24), X(x1 + 1), -8 * U, 30 * U, Z(top + 25), Z(top - 3))
    mb.box(X(x1 - 23), X(x1 + .5), -7 * U, 30 * U, Z(top + 57), Z(top + 42))
    mb.box(X(x1 - 24), X(x1 + 1), -8 * U, 30 * U, Z(bot), Z(bot - 35))


def opening_frame(mb, cx, w, spring, sill, top_in, frame=15, proud=6, kind="round", point=0.0, key=True):
    """Arched frame round an opening: jambs, arch band, impost blocks, keystone, sill (sheet px)."""
    a = w / 2
    b = spring - top_in
    inner = arch_pts(X(cx), Z(spring), a * U, b * U, 24, kind, point * U)
    outer = arch_pts(X(cx), Z(spring), (a + frame) * U, (b + frame) * U, 24, kind, point * U)
    mb.strip(inner, outer, -proud * U, 4 * U)
    for s in (-1, 1):
        xa, xb = sorted((X(cx + s * a), X(cx + s * (a + frame))))
        mb.box(xa, xb, -proud * U, 4 * U, Z(sill), Z(spring))
        xi, xo = sorted((X(cx + s * (a - 2)), X(cx + s * (a + frame + 3))))
        mb.box(xi, xo, -(proud + 2) * U, 4 * U, Z(spring + 5), Z(spring - 6))        # impost block
    if key:
        keystone(mb, X(cx), Z(top_in + 5), Z(top_in - frame - 8), 13 * U, 17 * U, -(proud + 3) * U, 4 * U)
    mb.box(X(cx - a - frame - 2), X(cx + a + frame + 2), -(proud + 4) * U, 6 * U, Z(sill + 13), Z(sill))   # sill
    return inner


def lattice(mb, cx, w, spring, sill, top_in, y, pitch=22, slope=1.45, bar=2.2, kind="round", point=0.0, transom=None, mullion=False):
    """Diamond leading over a window opening, clipped to the opening; optional transom / mullion."""
    a, b = w / 2, spring - top_in
    def inside(x, z):   # sheet px coords, z = sheet row
        if abs(x - cx) > a - .5 or z > sill - .5:
            return False
        if z >= spring:
            return True
        if kind == "round":
            return ((x - cx) / a) ** 2 + ((spring - z) / b) ** 2 < .97
        R = a + point
        hh = math.sqrt(max(R * R - (abs(x - cx) + point) ** 2, 0)) * b / math.sqrt(R * R - point * point)
        return spring - z < hh - .8
    for sgn in (1, -1):
        for k in range(-24, 25):
            x0 = cx + k * pitch
            seg, cur = [], None
            for i in range(0, int((sill - top_in) * 2) + 4):
                z = sill - i * .5
                x = x0 + sgn * (i * .5) / slope
                if inside(x, z):
                    if cur is None:
                        cur = [(x, z), (x, z)]
                    else:
                        cur[1] = (x, z)
                elif cur is not None:
                    seg.append(cur)
                    cur = None
            if cur is not None:
                seg.append(cur)
            for (p, q) in seg:
                pa, pb = Vector((X(p[0]), 0, Z(p[1]))), Vector((X(q[0]), 0, Z(q[1])))
                if (pb - pa).length < 2 * U:
                    continue
                d = (pb - pa).normalized()
                n = Vector((-d.z, 0, d.x)) * bar / 2 * U
                mb.prism(ccw([(pa.x - n.x, pa.z - n.z), (pb.x - n.x, pb.z - n.z), (pb.x + n.x, pb.z + n.z), (pa.x + n.x, pa.z + n.z)]), y - 1.5 * U, y + 1.5 * U)
    if transom:
        mb.box(X(cx - a), X(cx + a), y - 2 * U, y + 2 * U, Z(transom + 2), Z(transom - 2))
    if mullion:
        mb.box(X(cx - 1.5), X(cx + 1.5), y - 2 * U, y + 2 * U, Z(sill), Z(top_in))


def glass(mb, cx, w, spring, sill, top_in, y, kind="round", point=0.0):
    pts = arch_pts(X(cx), Z(spring), w / 2 * U, (spring - top_in) * U, 24, kind, point * U)
    poly = [(X(cx - w / 2), Z(sill))] + pts + [(X(cx + w / 2), Z(sill))]
    mb.prism(ccw(poly), y - .4 * U, y + .4 * U)


def wall_with_opening(mb, x0, x1, top, bot, cx, w, spring, sill, top_in, frame, niche=False):
    """Wall field with an arched hole (window) or recess (niche) cut out of it."""
    a = w / 2
    yd = 30 * U
    # body pieces round the hole: left, right, top-with-arch, below-sill
    mb.box(X(x0), X(cx - a), 0, yd, Z(bot), Z(top))
    mb.box(X(cx + a), X(x1), 0, yd, Z(bot), Z(top))
    mb.box(X(cx - a), X(cx + a), 0, yd, Z(bot), Z(sill))
    arc = arch_pts(X(cx), Z(spring), a * U, (spring - top_in) * U, 24)
    mb.prism(ccw(arc + [(X(cx + a), Z(top)), (X(cx - a), Z(top))]), 0, yd)
    for (r0, r1, proud) in ((top, top + 22, 6), (top + 42, top + 57, 4), (bot - 50, bot - 35, 4), (bot - 35, bot, 6)):
        mb.box(X(x0), X(x1), -proud * U, yd, Z(r1), Z(r0))
    if niche:   # recess back wall, 16 px deep, with its own blocks
        back = 16 * U
        poly = [(X(cx - a), Z(sill))] + arc + [(X(cx + a), Z(sill))]
        mb.prism(ccw(poly), back, yd)
        wall_blocks(mb, X(cx - a), X(cx + a), Z(sill), Z(top_in), back,
                    skip=lambda xa, xb, za, zb: [(xa, xb)] if zb <= Z(spring) or all(
                        ((x - X(cx)) / (a * U)) ** 2 + ((zb - Z(spring)) / ((spring - top_in) * U)) ** 2 < .9 for x in (xa, xb)) else [])
    fx0, fx1 = X(cx - a - frame - 3), X(cx + a + frame + 3)
    def skip(xa, xb, za, zb):
        if zb <= Z(sill + 13) or za >= Z(top_in - frame - 8):
            return [(xa, xb)]
        out = []
        if xa < fx0:
            out.append((xa, min(xb, fx0)))
        if xb > fx1:
            out.append((max(xa, fx1), xb))
        return out
    wall_blocks(mb, X(x0), X(x1), Z(bot - 50), Z(top + 57), 0.0, skip)


def part_W1():
    c = new_part("W1 Wall Column")
    mb = MB()
    ax = centre(20, 28, 110, 46)
    column_round(mb, ax, 28, 327, 6 * U, 15)
    wall_bands(mb, X(ax), X(219), 50, 323)
    wall_blocks(mb, X(ax + 30), X(219), Z(273), Z(107), 0.0)
    mb.obj("W1 Wall Column", c)


def part_W2():
    c = new_part("W2 Wall Window")
    mb = MB()
    ax = centre(232, 29, 315, 46)
    column_round(mb, ax, 29, 329, 6 * U, 232)
    wall_with_opening(mb, ax, 432, 50, 323, 361, 68, 176, 282, 142, 15)
    right_pilaster(mb, 432, 50, 323)
    opening_frame(mb, 361, 68, 176, 282, 142)
    glass(mb, 361, 68, 176, 282, 142, 15 * U)
    lattice(mb, 361, 68, 176, 282, 142, 13 * U, mullion=True)
    mb.obj("W2 Wall Window", c)


def part_W3():
    c = new_part("W3 Wall Niche")
    mb = MB()
    ax = centre(440, 28, 520, 46)
    column_round(mb, ax, 28, 329, 6 * U, 440)
    wall_with_opening(mb, ax, 649, 50, 323, 572, 85, 177, 282, 135, 13, niche=True)
    right_pilaster(mb, 649, 50, 323)
    opening_frame(mb, 572, 85, 177, 282, 135, frame=13)
    mb.obj("W3 Wall Niche", c)


def square_column(mb, ax, ay, top, bot):
    """Corner piece column: square two-tier cap, double ring, round shaft, ring, square base."""
    def sqb(x0, x1, z0, z1):
        h = (x1 - x0) / 2 * U
        mb.box(X(ax) - h, X(ax) + h, ay - h, ay + h, Z(z1), Z(z0))
    sqb(670 - 700 + ax, 730 - 700 + ax, top, top + 25)
    sqb(673 - 700 + ax, 727 - 700 + ax, top + 25, top + 50)
    mb.lathe([(22 * U, Z(top + 80)), (25 * U, Z(top + 78)), (25 * U, Z(top + 68)), (22 * U, Z(top + 66)), (25 * U, Z(top + 64)),
              (25 * U, Z(top + 55)), (22 * U, Z(top + 52)), (1e-4, Z(top + 52))][::-1][::-1], X(ax), ay, 32)
    mb.lathe([(1e-4, Z(bot - 52)), (18 * U, Z(bot - 52)), (17 * U, Z(top + 80)), (1e-4, Z(top + 80))], X(ax), ay, 32)
    mb.lathe([(1e-4, Z(bot - 33)), (25 * U, Z(bot - 33)), (27 * U, Z(bot - 38)), (27 * U, Z(bot - 47)), (24 * U, Z(bot - 52)), (1e-4, Z(bot - 52))], X(ax), ay, 32)
    sqb(670 - 700 + ax, 730 - 700 + ax, bot - 33, bot)
    keep = mb.M
    def one(m):
        panel(m, X(ax) - 20 * U, X(ax) + 20 * U, Z(bot - 5), Z(bot - 28), ay - 30 * U)
    around(mb, X(ax), ay, one)
    mb.M = keep


def part_W4():
    c = new_part("W4 Wall Corner")
    mb = MB()
    top, bot = 57, 320
    xl, xr = 700, 840
    cx, cy = (xl + xr) / 2, 70 * U
    L = math.hypot((cx - xl) * U, cy)
    for s in (-1, 1):
        # one straight wall, built along +x from the column, then turned round the column axis
        ang = math.atan2(cy, (cx - xl) * U)
        keep = mb.M
        if s < 0:
            mb.M = Matrix.Translation((X(xl), 0, 0)) @ Matrix.Rotation(ang, 4, "Z") @ Matrix.Translation((-X(xl), 0, 0))
            x0 = X(xl)
        else:
            mb.M = Matrix.Translation((X(xr), 0, 0)) @ Matrix.Rotation(-ang, 4, "Z") @ Matrix.Translation((-X(xr), 0, 0))
            x0 = X(xr) - L
        x1 = x0 + L
        # wall seen from the inside of the corner: face at -y, body behind
        d = 24 * U
        mb.box(x0, x1, 0, d, Z(bot), Z(top + 18))
        for (r0, r1, proud) in ((top + 18, top + 38, 5), (top + 50, top + 62, 3), (bot - 45, bot - 32, 3), (bot - 32, bot, 5)):
            mb.box(x0, x1, -proud * U, d, Z(r1), Z(r0))
        wall_blocks(mb, x0 + 8 * U, x1 - 4 * U, Z(bot - 45), Z(top + 62), 0.0)
        mb.M = keep
    for ax in (xl, xr):
        square_column(mb, ax, 0, top, bot)
    mb.obj("W4 Wall Corner", c)


def door_leaves(mb, cx, half, spring, top_in, bot, y, knight_w, knight_h, knight_z, ogee):
    """Two door leaves under an elliptic arch with stiles, straps, knights and ogee lines (sheet px)."""
    a, b = half, spring - top_in
    arc = arch_pts(X(cx), Z(spring), a * U, b * U, 32)
    poly = [(X(cx - a), Z(bot))] + arc + [(X(cx + a), Z(bot))]
    mb.prism(ccw(poly), y, y + 8 * U)
    # meeting stiles and the leaves' own arched edge
    mb.box(X(cx - 14), X(cx + 14), y - 4 * U, y, Z(bot), Z(top_in))
    inner = arch_pts(X(cx), Z(spring), (a - 8) * U, (b - 8) * U, 32)
    outer = arch_pts(X(cx), Z(spring), (a - 2) * U, (b - 2) * U, 32)
    mb.strip(inner, outer, y - 3 * U, y)
    for s in (-1, 1):
        mb.box(X(cx + s * (a - 8)), X(cx + s * (a - 2)), y - 3 * U, y, Z(bot), Z(spring))
        kx = cx + s * a / 2
        relief(mb, "knight", KNIGHT_BOX, X(kx), Z(knight_z), knight_w * U, knight_h * U, y - 1 * U, 5 * U, mirror=(s > 0))
        # ogee lines over and under the knight: raised round cords
        top_l, top_peak, bot_l, bot_peak = ogee
        for (z_side, z_mid) in ((top_l, top_peak), (bot_l, bot_peak)):
            pts = []
            for i in range(17):
                t = i / 16
                xx = (cx + s * 14) + s * (a - 22) * t
                u = abs(t - .5) * 2
                zz = z_mid + (z_side - z_mid) * (u ** .7)
                pts.append((X(xx), y - 1.5 * U, Z(zz)))
            mb.tube(pts, 1.6 * U, 6)
    # straps at the top, middle and foot
    for zr in (spring + 8, bot - 10):
        for xs in (cx - a + 6, cx + a - 6, cx):
            mb.box(X(xs - 9), X(xs + 9), y - 5 * U, y, Z(zr + 9), Z(zr - 9))


def part_G1():
    c = new_part("G1 Great Gate")
    mb = MB()
    cx = 1042
    # piers: cap, gold ring, panelled shaft, ring, panelled base
    for s in (-1, 1):
        px = cx + s * 131
        def sqb(w, z0, z1, d=None, px=px):
            h = w / 2 * U
            dd = (d or w) / 2 * U
            mb.box(X(px) - h, X(px) + h, -dd, dd, Z(z1), Z(z0))
        sqb(64, 140, 160, 64)
        mb.box(X(px) - 29 * U, X(px) + 29 * U, -27 * U, 27 * U, Z(172), Z(160))
        sqb(52, 172, 275, 48)
        mb.box(X(px) - 29 * U, X(px) + 29 * U, -27 * U, 27 * U, Z(290), Z(275))
        sqb(62, 290, 335, 58)
        panel(mb, X(px) - 18 * U, X(px) + 18 * U, Z(268), Z(180), -24 * U)
        panel(mb, X(px) - 22 * U, X(px) + 22 * U, Z(328), Z(297), -29 * U)
    # stone arch over the piers, 9 voussoirs and an outer lip
    ellipse_ring(mb, X(cx), Z(150), 104 * U, 68 * U, 152 * U, 112 * U, -22 * U, 22 * U, blocks=9)
    ellipse_ring(mb, X(cx), Z(150), 149 * U, 109 * U, 157 * U, 116 * U, -25 * U, 18 * U, blocks=1, n=48)
    # the deep reveal behind the doors, filling the arch down to the floor
    rv = arch_pts(X(cx), Z(150), 112 * U, 76 * U, 32)
    mb.prism(ccw([(X(cx - 112), Z(335))] + rv + [(X(cx + 112), Z(335))]), 6 * U, 16 * U)
    # gold door frame: posts and arch band, then the leaves
    fa, fb = 100, 73
    inner = arch_pts(X(cx), Z(168), (fa - 13) * U, (fb - 13) * U, 32)
    outer = arch_pts(X(cx), Z(168), fa * U, fb * U, 32)
    mb.strip(inner, outer, -10 * U, 6 * U)
    for s in (-1, 1):
        mb.box(X(cx + s * (fa - 13)), X(cx + s * fa), -10 * U, 6 * U, Z(335), Z(168))
    door_leaves(mb, cx, fa - 13, 168, 168 - (fb - 13), 333, -4 * U, 34, 60, 250, (183, 152, 262, 292))
    # crest shield with its crown over the keystone
    pts = traced("shield", SHIELD_BOX)
    sh = scaled(pts, X(cx), Z(97), 72 / 101, 90 / 152)
    mb.prism(ccw(sh), -30 * U, -18 * U)
    relief(mb, "crown", CROWN_BOX, X(cx), Z(66), 40 * U, 34 * U, -30 * U, 3 * U)
    mb.box(X(cx - 32), X(cx + 32), -10 * U, 10 * U, Z(337), Z(331))     # threshold
    mb.obj("G1 Great Gate", c)


def part_D1():
    c = new_part("D1 Double Door")
    mb = MB()
    cx = 1308
    fa, fb = 78, 76
    inner = arch_pts(X(cx), Z(181), (fa - 15) * U, (fb - 15) * U, 32)
    outer = arch_pts(X(cx), Z(181), fa * U, fb * U, 32)
    mb.strip(inner, outer, -8 * U, 6 * U)
    for s in (-1, 1):
        mb.box(X(cx + s * (fa - 15)), X(cx + s * fa), -8 * U, 6 * U, Z(330), Z(181))
        mb.box(X(cx + s * (fa - 17)), X(cx + s * (fa + 3)), -11 * U, 6 * U, Z(195), Z(178))   # side lugs
        mb.box(X(cx + s * (fa - 17)), X(cx + s * (fa + 3)), -11 * U, 6 * U, Z(331), Z(313))
    door_leaves(mb, cx, fa - 15, 181, 181 - (fb - 15), 330, -2 * U, 30, 57, 252, (195, 166, 265, 288))
    mb.obj("D1 Double Door", c)


def part_WN1():
    c = new_part("WN1 Window Arched")
    mb = MB()
    cx, w, spring, sill, top_in = 1481, 99, 145, 312, 78
    opening_frame(mb, cx, w, spring, sill, top_in, frame=16, proud=8, kind="pointed", point=18, key=False)
    glass(mb, cx, w, spring, sill, top_in, 4 * U, kind="pointed", point=18)
    lattice(mb, cx, w, spring, sill, top_in, 1 * U, pitch=35, slope=1.35, kind="pointed", point=18, transom=152)
    mb.obj("WN1 Window Arched", c)


def part_WN2():
    c = new_part("WN2 Window Gothic")
    mb = MB()
    cx, w, spring, sill, top_in = 1610, 47, 152, 310, 120
    opening_frame(mb, cx, w, spring, sill, top_in, frame=12, proud=7, kind="pointed", point=14, key=False)
    glass(mb, cx, w, spring, sill, top_in, 4 * U, kind="pointed", point=14)
    lattice(mb, cx, w, spring, sill, top_in, 1 * U, pitch=24, slope=1.7, kind="pointed", point=14, transom=167, mullion=True)
    mb.obj("WN2 Window Gothic", c)


# ---------------------------------------------------------------- row 4: stairs, tiles, trims
def part_ST1():
    """Carpet stairs: five steps, each a little narrower, a runner with two stripes."""
    c = new_part("ST1 Stairs Carpet")
    mb = MB()
    cx, rise, tread = X(151), 16 * U, 30 * U
    for k in range(5):
        w = (129 - 10 * k) * U
        y0 = k * tread
        mb.box(cx - w, cx + w, y0, 5 * tread, Z(900), Z(900) + rise * (k + 1))
        zt = Z(900) + rise * (k + 1)
        mb.box(cx - 65 * U, cx + 65 * U, y0 - 1.2 * U, y0 + tread, zt, zt + 1.2 * U)                  # carpet tread
        mb.box(cx - 65 * U, cx + 65 * U, y0 - 1.2 * U, y0, zt - rise, zt + 1.2 * U)                   # carpet riser
        for sx in (-57, 57):
            mb.box(cx + (sx - 3) * U, cx + (sx + 3) * U, y0 - 2 * U, y0 + tread, zt + 1 * U, zt + 2 * U)
            mb.box(cx + (sx - 3) * U, cx + (sx + 3) * U, y0 - 2.2 * U, y0 - 1 * U, zt - rise, zt + 2 * U)
    mb.obj("ST1 Stairs Carpet", c, bevel=.8)


def part_ST2():
    c = new_part("ST2 Stairs Stone")
    mb = MB()
    cx, rise, tread = X(377), 16 * U, 30 * U
    for k in range(5):
        mb.box(cx - 80 * U, cx + 80 * U, k * tread, 5 * tread, Z(904), Z(904) + rise * (k + 1))
        for jx in (-27, 27):   # joints between the step blocks
            mb.box(cx + (jx - .6) * U, cx + (jx + .6) * U, k * tread - .3 * U, k * tread + .3 * U, Z(904) + rise * k + 1 * U, Z(904) + rise * (k + 1) - 1 * U)
    mb.obj("ST2 Stairs Stone", c, bevel=.8)


def part_ST3():
    """Round stairs: quarter-round steps fanning from the back right, on a block landing."""
    c = new_part("ST3 Stairs Round")
    mb = MB()
    px, rise = X(634), 22 * U
    py = 60 * U
    for k, R in enumerate((172, 145, 118, 92)):
        pts = [(px, py)]
        for i in range(25):
            a = math.pi + (math.pi / 2) * i / 24
            pts.append((px + R * U * math.cos(a), py + R * U * math.sin(a) * .75))
        pts.append((px, py))
        pts = pts[:-1]
        mb.prism_plan(pts, Z(903), Z(903) + rise * (k + 1))
    mb.box(px - 92 * U, px, py, py + 40 * U, Z(903), Z(903) + rise * 5)
    mb.obj("ST3 Stairs Round", c, bevel=.8)


def part_ST4():
    """Curving carpet stair between two solid parapets, rising front-left to back-right."""
    c = new_part("ST4 Stairs Curved")
    mb = MB()
    P = [Vector((X(705), -60 * U)), Vector((X(705), 10 * U)), Vector((X(805), -10 * U)), Vector((X(800), 70 * U))]
    def bez(t):
        a, b, c2, d = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
        return P[0] * a + P[1] * b + P[2] * c2 + P[3] * d
    N, rise, half = 13, 9 * U, 38 * U
    base = Z(908)
    for k in range(N):
        p0, p1 = bez(k / N), bez((k + 1) / N)
        t = (p1 - p0).normalized()
        n = Vector((-t.y, t.x))
        q = [p0 - n * half, p0 + n * half, p1 + n * half, p1 - n * half]
        mb.prism_plan(ccw([tuple(v) for v in q]), base, base + rise * (k + 1))
        zt = base + rise * (k + 1)
        qc = [p0 - n * 26 * U, p0 + n * 26 * U, p1 + n * 26 * U, p1 - n * 26 * U]
        mb.prism_plan(ccw([tuple(v) for v in qc]), zt, zt + 1.2 * U)
    # parapets: low on the left, tall on the right
    for side, hgt, th in ((-1, 18, 10), (1, 34, 13)):
        rings = []
        for i in range(N * 2 + 1):
            t = i / (N * 2)
            p = bez(t)
            tt = (bez(min(t + .01, 1)) - bez(max(t - .01, 0))).normalized()
            n = Vector((-tt.y, tt.x)) * side
            zt = base + rise * (min(int(t * N), N - 1) + 1) * 0 + rise * N * t + hgt * U
            a = p + n * half
            b = p + n * (half + th * U)
            rings.append([(a.x, a.y, base), (b.x, b.y, base), (b.x, b.y, zt), (a.x, a.y, zt)])
        mb.loft(rings)
    mb.obj("ST4 Stairs Curved", c, bevel=.8)


def tile(mb, x0, x1, z0, z1, th=8):
    mb.box(X(x0), X(x1), 0, th * U, Z(z1), Z(z0))


def part_TL():
    c = new_part("TL Tiles Trims")
    mb = MB()
    tile(mb, 869, 972, 777, 890)                                        # marble
    tile(mb, 985, 1085, 777, 889)                                       # wood
    for (a, b, cz0, cz1) in ((1098, 1150, 777, 833), (1152, 1204, 777, 833), (1098, 1150, 835, 890), (1152, 1204, 835, 890)):
        tile(mb, a, b, cz0, cz1)                                        # checker 2 x 2
    for x0, x1, lat in ((1218, 1318, False), (1334, 1436, True)):
        mb.box(X(x0), X(x1), 0, 8 * U, Z(890), Z(776))
        # raised frame
        for (a, b, z0, z1) in ((x0, x1, 776, 783), (x0, x1, 883, 890), (x0, x0 + 7, 776, 890), (x1 - 7, x1, 776, 890)):
            mb.box(X(a), X(b), -2 * U, 0, Z(z1), Z(z0))
        if lat:   # gold lattice strips (2.5 diamonds across)
            cxx, w = (x0 + x1) / 2, (x1 - x0 - 14)
            for sgn in (1, -1):
                for k in range(-4, 5):
                    pts = []
                    for i in range(0, 260):
                        zz = 883 - i * .5
                        xx = cxx + k * 32 + sgn * (i * .5) * .62
                        if abs(xx - cxx) < w / 2 and 783 < zz < 883:
                            pts.append((xx, zz))
                    if len(pts) > 3:
                        a, b = Vector((X(pts[0][0]), 0, Z(pts[0][1]))), Vector((X(pts[-1][0]), 0, Z(pts[-1][1])))
                        d = (b - a).normalized()
                        nn = Vector((-d.z, 0, d.x)) * 1.6 * U
                        mb.prism(ccw([(a.x - nn.x, a.z - nn.z), (b.x - nn.x, b.z - nn.z), (b.x + nn.x, b.z + nn.z), (a.x + nn.x, a.z + nn.z)]), -1.5 * U, 0)
    for (z0, z1) in ((776, 800), (812, 834)):                           # gold strips with a groove
        mb.box(X(1456), X(1638), 0, 6 * U, Z(z1), Z(z0))
        mb.box(X(1456), X(1638), -2 * U, 0, Z(z0 + 4), Z(z0))
        mb.box(X(1456), X(1638), -2 * U, 0, Z(z1), Z(z1 - 4))
        mb.box(X(1456), X(1462), -2 * U, 0, Z(z1), Z(z0))
        mb.box(X(1632), X(1638), -2 * U, 0, Z(z1), Z(z0))
    mb.box(X(1478), X(1535), 0, 8 * U, Z(904), Z(846))                  # square inset tile
    for (a, b, z0, z1) in ((1478, 1535, 846, 856), (1478, 1535, 894, 904), (1478, 1488, 846, 904), (1525, 1535, 846, 904)):
        mb.box(X(a), X(b), -3 * U, 0, Z(z1), Z(z0))
    mb.box(X(1562), X(1617), 0, 8 * U, Z(903), Z(846))                  # pyramid stud
    for (a, b, z0, z1) in ((1562, 1617, 846, 853), (1562, 1617, 896, 903), (1562, 1569, 846, 903), (1610, 1617, 846, 903)):
        mb.box(X(a), X(b), -2 * U, 0, Z(z1), Z(z0))
    cxp, czp, h = X(1589.5), Z(874.5), 20.5 * U
    v = [(cxp - h, 0, czp - h), (cxp + h, 0, czp - h), (cxp + h, 0, czp + h), (cxp - h, 0, czp + h), (cxp, -12 * U, czp)]
    mb.add(v, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (3, 2, 1, 0)])
    mb.obj("TL Tiles Trims", c, bevel=.8)


PARTS = {
    "W1": part_W1, "W2": part_W2, "W3": part_W3, "W4": part_W4, "G1": part_G1, "D1": part_D1, "WN1": part_WN1, "WN2": part_WN2,
    "B1": part_B1, "P1": part_P1, "B2": part_B2, "P2": part_P2, "P3": part_P3, "M1": part_M1, "M2": part_M2,
    "A1": part_A1, "A2": part_A2, "M3": part_M3, "M4": part_M4, "R1": part_R1, "C1": part_C1, "P4": part_P4,
    "BN": part_BN, "SH": part_SH, "LT": part_LT, "CH": part_CH, "KN": part_KN, "CR": part_CR, "BD": part_BD,
    "ST1": part_ST1, "ST2": part_ST2, "ST3": part_ST3, "ST4": part_ST4, "TL": part_TL,
}


def build(keys=None):
    done, failed = [], []
    for k in (keys or PARTS):
        try:
            PARTS[k]()
            done.append(k)
        except Exception as e:   # keep going, report what broke
            import traceback
            failed.append((k, repr(e), traceback.format_exc().splitlines()[-3:]))
    return done, failed


# ---------------------------------------------------------------- scene setup and the check render
def setup_scene():
    sc = bpy.context.scene
    for o in list(sc.objects):
        if o.name in ("Cube", "Light", "Camera"):
            bpy.data.objects.remove(o, do_unlink=True)
    ref = bpy.data.objects.get("Sheet Reference")
    if ref is None:
        img = bpy.data.images.get("asset_sheet.webp") or bpy.data.images.load(os.path.join(KIT, "asset_sheet.webp"), check_existing=True)
        ref = bpy.data.objects.new("Sheet Reference", None)
        ref.empty_display_type = "IMAGE"
        ref.data = img
        sc.collection.objects.link(ref)
    ref.empty_display_size = 1671 * U
    ref.location = (1671 * U / 2, 2.5, 941 * U / 2)
    ref.rotation_euler = (math.pi / 2, 0, 0)
    ref.empty_image_offset = (-.5, -.5)
    ref.color[3] = .55
    ref.use_empty_image_alpha = True
    ref.show_empty_image_perspective = False
    cam = bpy.data.objects.get("Sheet Camera")
    if cam is None:
        cd = bpy.data.cameras.new("Sheet Camera")
        cam = bpy.data.objects.new("Sheet Camera", cd)
        sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1671 * U
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.clip_end = 100
    cam.location = (1671 * U / 2, -20, 941 * U / 2)
    cam.rotation_euler = (math.pi / 2, 0, 0)
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1671, 941
    sc.render.resolution_percentage = 100
    return cam


def render_compare(out_png, crop=None, scale=1):
    """Workbench clay render through the sheet camera, stacked under the sheet (sheet on top).
    crop = (x1, y1, x2, y2) sheet pixels to keep only one area, scale = pixel repeat."""
    sc = bpy.context.scene
    setup_scene()
    bpy.data.objects["Sheet Reference"].hide_render = True
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light, sh.color_type, sh.single_color = "STUDIO", "SINGLE", (0.8, 0.78, 0.74)
    sh.show_shadows, sh.show_cavity, sh.cavity_type = True, True, "BOTH"
    tmp = os.path.join(bpy.app.tempdir, "kit_render.png")
    sc.render.filepath = tmp
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    r = bpy.data.images.load(tmp, check_existing=False)
    s = bpy.data.images["asset_sheet.webp"]
    w, h = s.size
    A = np.empty(w * h * 4, np.float32)
    s.pixels.foreach_get(A)
    B = np.empty(w * h * 4, np.float32)
    r.pixels.foreach_get(B)
    A, B = A.reshape(h, w, 4), B.reshape(h, w, 4)
    if crop:
        x1, y1, x2, y2 = crop
        A, B = A[h - y2:h - y1, x1:x2], B[h - y2:h - y1, x1:x2]
    if scale > 1:
        A = np.repeat(np.repeat(A, scale, 0), scale, 1)
        B = np.repeat(np.repeat(B, scale, 0), scale, 1)
    C = np.concatenate([B, A], 0)
    im = bpy.data.images.new("kit_compare", C.shape[1], C.shape[0], alpha=True)
    im.pixels.foreach_set(C.ravel())
    im.filepath_raw = out_png
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)
    bpy.data.images.remove(r)
    return out_png
