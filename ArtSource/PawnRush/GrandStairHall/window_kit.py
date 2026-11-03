# The architectural wall's (source/architectural_wall.glb) two windows, open and clean.
# 승규 11-03: "외곽 벽 아치형 창문이 깨져보여 매끈하게" and then "창 전 버전 모양이 이뻐 거기서 부드럽게 안깨지게": the
# asset's own tracery (the earlier look) is kept as a design and drawn again in clean bars. Its glass panes are
# uneven, so cutting along them (the first version) left ragged bars; plain bars (the second) lost the look.
# Measured off the asset's glass (asset units, the asset's own frame; the two windows mirror):
#   opening   x +-0.2806, half 0.0465, sill 0.181, straight to 0.390, a pointed arch to 0.4434
#   tracery   a mullion up the middle to the eye, two more at the quarters to the top transom, five transoms
#             (0.2203 .. 0.3787), on the top one a pointed arch over each half and a small one over each quarter,
#             and the almond eye between the halves' crowns and the opening's crown
# The cut: the whole opening, from just behind the frame's front moulding (y 0.008, where the old bars and glass begin)
# through the back, on the full-resolution mesh, which is decimated afterwards everywhere but around the windows: the
# frame stays as modelled and the reveal follows the clean outline. 승규 again: "입체감이 줄었어 … 전 버전이랑 쉐잎 맞춰":
# the bars are round in section as the asset's (see BAR). Used by outer_walls_v17.py and foyer_sides_v20.py.
import bpy, bmesh, math
from mathutils import Matrix, Vector

CENTRES = (.2806, -.2806)
HALF, SILL, SPRING, CROWN = .0465, .181, .390, .4434
CUT_Y = (.008, .25)
GLASS_Y = .0186               # the old glass plane: the bars' middle
SECTION = 10                   # the bars' round section
TRANSOMS = (.2203, .2600, .3008, .3415, .3787)
EYE = (.4035, .4415, .0135)      # bottom, top, half width: an almond, as the asset's
# (width, depth) in metres, round on both faces like the asset's bars (measured: the quarter mullions 0.006 x 0.007, the
# middle one 0.0068 x 0.0116 asset units, bulging 0.002 in their middle), so they catch light and shadow as before
BAR, MID_BAR, TRANSOM, ARCH = (.28, .36), (.36, .50), (.26, .32), (.28, .36)


def pointed(xa, xb, zs, rise, n=16):
    """A pointed arch from (xa, zs) over to (xb, zs), its crown `rise` above the springing."""
    h = (xb - xa) / 2
    r = (h * h + rise * rise) / (2 * h)
    pts = []
    for i in range(n + 1):                      # left leg, centre (xa + r, zs)
        a0, a1 = math.pi, math.atan2(rise, h - r)
        a = a0 + (a1 - a0) * i / n
        pts.append((xa + r + r * math.cos(a), zs + r * math.sin(a)))
    for i in range(1, n + 1):                   # right leg, centre (xb - r, zs)
        a0, a1 = math.atan2(rise, r - h), 0.0
        a = a0 + (a1 - a0) * i / n
        pts.append((xb - r + r * math.cos(a), zs + r * math.sin(a)))
    return pts


def outline(cx, n=24):
    return [(cx - HALF, SILL), (cx + HALF, SILL)] + list(reversed(pointed(cx - HALF, cx + HALF, SPRING, CROWN - SPRING, n)))


def cutter(centres=CENTRES):
    """One prism per window, the opening's outline from just behind the frame's front to beyond the back."""
    bm = bmesh.new()
    for cx in centres:
        prof = outline(cx)
        front = [bm.verts.new((x, CUT_Y[0], z)) for x, z in prof]
        back = [bm.verts.new((x, CUT_Y[1], z)) for x, z in prof]
        bm.faces.new(front); bm.faces.new(list(reversed(back)))
        for i in range(len(prof)):
            j = (i + 1) % len(prof)
            bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    cm = bpy.data.meshes.new("_window_cutter"); bm.to_mesh(cm); bm.free()
    return cm


def _apply(me, kind, setup):
    sc = bpy.context.scene
    o = bpy.data.objects.new("_w", me); sc.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("m", kind)
    extra = setup(o, m)
    bpy.ops.object.modifier_apply(modifier=m.name)
    if extra:
        bpy.data.objects.remove(extra)
    bpy.data.objects.remove(o)
    return me


def prepare(me, ratio, centres=CENTRES):
    """The windows cut on the full mesh, then the rest decimated to `ratio`; the windows' surroundings keep every vertex."""
    def cut(o, m):
        co = bpy.data.objects.new("_c", cutter(centres)); bpy.context.scene.collection.objects.link(co)
        m.operation = "DIFFERENCE"; m.object = co; m.solver = "EXACT"; m.use_hole_tolerant = True
        return co
    me = _apply(me, "BOOLEAN", cut)

    def thin(o, m):
        g = o.vertex_groups.new(name="thin")
        for v in me.vertices:
            near = any(abs(v.co.x - cx) < HALF + .02 for cx in centres) and SILL - .03 < v.co.z < CROWN + .03
            g.add([v.index], 0.0 if near else 1.0, "REPLACE")
        m.ratio = ratio; m.vertex_group = "thin"
        return None
    n = len(me.polygons)
    me = _apply(me, "DECIMATE", thin)
    print("WINDOWS cut on", n, "faces, decimated to", len(me.polygons))
    return me


def _ribbon(bm, pts, size, scale, closed=False):
    """A bar along `pts` (asset x, z) of the wall's stretch `scale` (sx, sy, sz), its section `size` (width, depth)
    metres: a lens round on both faces like the asset's own bars, centred on the old glass plane."""
    sx, sy, sz = scale
    width, depth = size
    ring = [(width / 2 * math.cos(2 * math.pi * k / SECTION), depth / 2 * math.sin(2 * math.pi * k / SECTION))
            for k in range(SECTION)]
    m = [Vector((x * sx, z * sz)) for x, z in pts]
    rows = []
    for i, p in enumerate(m):
        a = m[i - 1] if (i > 0 or closed) else p
        b = m[(i + 1) % len(m)] if (i < len(m) - 1 or closed) else p
        t = (b - a).normalized()
        n = Vector((-t.y, t.x))
        rows.append([bm.verts.new(((p.x + n.x * u) / sx, GLASS_Y + w / sy, (p.y + n.y * u) / sz)) for u, w in ring])
    segs = len(rows) if closed else len(rows) - 1
    for i in range(segs):
        a, b = rows[i], rows[(i + 1) % len(rows)]
        for k in range(SECTION):
            k1 = (k + 1) % SECTION
            f = bm.faces.new((a[k], a[k1], b[k1], b[k]))
            f.smooth = True
    if not closed:
        for r in (rows[0], rows[-1]):
            bm.faces.new(r)


def _extend(pts, d):
    """The polyline lengthened by d at both ends (asset units along its own end directions)."""
    def out(p, q):
        v = Vector((p[0] - q[0], p[1] - q[1])); v.normalize()
        return (p[0] + v.x * d, p[1] + v.y * d)
    return [out(pts[0], pts[1])] + pts[1:-1] + [out(pts[-1], pts[-2])]


def tracery(name, matrix, centres, col, material):
    """The bars of the windows at `centres` (asset x) of a wall placed by `matrix` (asset -> world)."""
    sx, sy, sz = [(matrix.to_3x3() @ Vector(axis)).length for axis in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
    sc2 = (sx, sy, sz)
    e = .01                                         # into the reveal at the ends
    bm = bmesh.new()
    for cx in centres:
        x0, x1, q = cx - HALF, cx + HALF, HALF / 2
        top = TRANSOMS[-1]
        _ribbon(bm, [(cx, SILL - e), (cx, EYE[0])], MID_BAR, sc2)
        for x in (cx - q, cx + q):
            _ribbon(bm, [(x, SILL - e), (x, top)], BAR, sc2)
        for z in TRANSOMS:
            _ribbon(bm, [(x0 - e, z), (x1 + e, z)], TRANSOM, sc2)
        for a, b in ((x0, cx), (cx, x1)):           # each half's arch, its crown at the eye's foot
            _ribbon(bm, _extend(pointed(a, b, top, EYE[0] - top), .004), ARCH, sc2)
        for a in (x0, x0 + q, cx, cx + q):          # each quarter's small arch
            _ribbon(bm, pointed(a, a + q, top, .014, 8), ARCH, sc2)
        lo, hi, w = EYE
        mid = (lo + hi) / 2
        eye = pointed(cx - w, cx + w, mid, hi - mid, 10)
        eye = eye + [(x, 2 * mid - z) for x, z in reversed(eye[1:-1])]
        _ribbon(bm, eye, BAR, sc2, closed=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(material)
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    o.matrix_world = matrix
    return o
