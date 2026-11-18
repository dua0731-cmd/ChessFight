"""R4 front-course gallery (seoungtest R687, PLAN.md R4, 2026-11-18): the library hall around the front course.

    blender -b --factory-startup --python rl_r4_gallery_model.py -- <parts> [--preview <dir>]

parts: comma list of bookcase, walls, ceiling, floor, frame, planters (or all). Every part is built every run (the
.blend keeps them all); only the listed parts are exported. --preview <dir> writes the FBX under <dir> instead of the
repo (rl_kit.ROOT swapped), for a look check while Unity is in Play.

Concept D "giant's desk": GC_D5 (walnut bookcase with brass inlays, cream stone wall with arched window, framed board),
GC_E1 (walnut hall, arched windows high up, framed boards), GC_D1. Style = the E5 models: rounded bevels, brass bands.
Sizes are the blockout's (RoyalLibrary_blockout.blend, Blender x, y, z = Unity x, z, y):
  gallery walls  inner faces x = +-37.25, y -150 .. -10, floor top z -22.5, ceiling underside z 29.5
  entrance wall  inner face y = -149 (portal hole x +-6, z -10 .. 10; R1 dresses the portal: |x| < 10.6 left plain)
  bookcases      15 x 3 m, 34 m tall from the floor, 4 left (x -35) + 3 right (x 35); one shape on all seven
  boards         4 frames 10 x 10 m (x +-37, y -121 / -45, z 3 .. 13): only the frame and back panel; the squares,
                 piece markers and caption are WarRoomScoreLabel's and stay in front, untouched
  planters       3 empty pots (two cream fluted, one brass tub); the blockout plant and soil stay until Tripo
Heights: wainscot to z 14.2 (above the boards' z 13 and the bookcases' 11.5 + crown), dado rail 14.2 .. 15.6, cream
stone above with one arched window a bay (shape only: a dark glass panel, no light - R7 lights it), cornice to 29.5.

Models (rl_kit slots only, metre UVs on the final mesh, rl_kit.export):
  Gallery/RlGalleryBookcase  frame "Royal library bookcase -35 -140" (hung on all seven)
  Gallery/RlGalleryWalls     root (both side walls + the entrance wall's inner face)
  Gallery/RlGalleryCeiling   root      Gallery/RlGalleryFloor  root
  Gallery/RlGalleryFrame     frame "Random GM replay preview 0" (hung on all four)
  Gallery/RlPlanterFluted112 · RlBrassTub53 · RlPlanterFluted35  each in its houseplant's frame
Never export into the repo while Unity is in Play.
"""
import math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rl_kit as k   # noqa: E402

ZONE = "Gallery"
BLEND = os.path.join(HERE, "rl_r4_gallery.blend")
FLOOR, CEIL = -22.5, 29.5
WALL_X = 37.25
DADO0, DADO1 = 14.2, 15.6
SMOOTH_ANGLE = math.radians(35)


# ------------------------------------------------------------------ mesh builder
class Builder:
    """One mesh per model: parts are made in a scratch bmesh, transformed and copied in with their slot."""

    def __init__(self):
        self.bm = bmesh.new()
        self.slots = []

    def slot_index(self, slot):
        if slot not in self.slots:
            self.slots.append(slot)
        return self.slots.index(slot)

    def merge(self, src, slot, M=Matrix.Identity(4)):
        mi = self.slot_index(slot)
        vm = {v: self.bm.verts.new(M @ v.co) for v in src.verts}
        for f in src.faces:
            try:
                nf = self.bm.faces.new([vm[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = mi
        src.free()

    def obj(self, name, col):
        me = bpy.data.meshes.new(name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for s in self.slots:
            me.materials.append(k.slot(s))
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=SMOOTH_ANGLE)
        o = bpy.data.objects.new(name, me)
        col.objects.link(o)
        return o


def box_bm(x0, x1, y0, y1, z0, z1, bev=0.0, seg=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    s = Vector((x1 - x0, y1 - y0, z1 - z0))
    for v in bm.verts:
        v.co = c + Vector((v.co.x * s.x, v.co.y * s.y, v.co.z * s.z))
    if bev > 0:
        b = min(bev, min(s) * 0.45)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, segments=seg, profile=0.5, affect="EDGES",
                        clamp_overlap=True)
    return bm


def box(B, slot, x0, x1, y0, y1, z0, z1, bev=0.0, seg=1, M=Matrix.Identity(4)):
    B.merge(box_bm(x0, x1, y0, y1, z0, z1, bev, seg), slot, M)


def lathe_bm(prof, seg=32, wobble=None):
    """(r, z) profile turned about Z; r = 0 points close the ends. wobble(theta) scales r (flutes)."""
    bm = bmesh.new()
    rings = []
    for r, z in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
            continue
        ring = []
        for i in range(seg):
            t = 2 * math.pi * i / seg
            rr = r * (wobble(t) if wobble else 1.0)
            ring.append(bm.verts.new((rr * math.cos(t), rr * math.sin(t), z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(seg):
            j = (i + 1) % seg
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], b[0], a[j]))
            else:
                bm.faces.new((a[i], b[i], b[j], a[j]))
    return bm


def prism_bm(pts, y0, y1, bev=0.0, seg=1):
    """2D outline (x, z) extruded along y from y0 to y1 (caps triangulated)."""
    bm = bmesh.new()
    f = [bm.verts.new((x, y0, z)) for x, z in pts]
    b = [bm.verts.new((x, y1, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(f)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((f[i], f[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bev > 0:
        sharp = [e for e in bm.edges if e.calc_face_angle(0) > math.radians(30)]
        bmesh.ops.bevel(bm, geom=sharp, offset=bev, segments=seg, profile=0.5, affect="EDGES", clamp_overlap=True)
    caps = [fc for fc in bm.faces if len(fc.verts) > 4]
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="BEAUTY")
    return bm


def ring_bm(outer, inner, y0, y1):
    """Band between two (x, z) loops with the same point count (open=False: closed loops), extruded y0 .. y1."""
    bm = bmesh.new()
    n = len(outer)
    of = [bm.verts.new((x, y0, z)) for x, z in outer]
    ob = [bm.verts.new((x, y1, z)) for x, z in outer]
    inf = [bm.verts.new((x, y0, z)) for x, z in inner]
    inb = [bm.verts.new((x, y1, z)) for x, z in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((of[i], of[j], inf[j], inf[i]))      # front
        bm.faces.new((ob[i], inb[i], inb[j], ob[j]))      # back
        bm.faces.new((of[i], ob[i], ob[j], of[j]))        # outer side
        bm.faces.new((inf[i], inf[j], inb[j], inb[i]))    # inner side
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def arch_loop(cx, w, z0, zs, n=14, grow=0.0):
    """Closed (x, z) outline of a round-headed opening: sill z0, springing zs, half width w (+grow all round)."""
    r = w + grow
    pts = [(cx - r, z0 - grow), (cx + r, z0 - grow), (cx + r, zs)]
    for i in range(1, n):
        t = math.pi * i / n
        pts.append((cx + r * math.cos(t), zs + r * math.sin(t)))
    pts.append((cx - r, zs))
    return pts


def rrect_loop(hx, hz, r, n=3, cx=0.0, cz=0.0):
    pts = []
    for (sx, sz, a0) in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        ox, oz = cx + sx * (hx - r), cz + sz * (hz - r)
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((ox + r * math.cos(a), oz + r * math.sin(a)))
    return pts


def inlay_rect(B, M, x0, x1, z0, z1, y, w=0.16, t=0.05):
    """Brass inlay line (four strips) on a face at depth y (front = -y)."""
    box(B, "brass", x0, x1, y - t, y, z1 - w, z1, M=M)
    box(B, "brass", x0, x1, y - t, y, z0, z0 + w, M=M)
    box(B, "brass", x0, x0 + w, y - t, y, z0 + w, z1 - w, M=M)
    box(B, "brass", x1 - w, x1, y - t, y, z0 + w, z1 - w, M=M)


def new_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    return col


def at(x, y, z, rot_deg=0.0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(rot_deg), 4, "Z")


# ------------------------------------------------------------------ bookcase (D5 #1)
BC_W, BC_FRONT, BC_BACK, BC_H = 7.5, -1.5, 1.5, 34.0
SHELVES = [(3.0, 3.6), (10.4, 11.0), (17.6, 18.2), (24.8, 25.4)]   # (underside, top) of each shelf; row 4 to 30.6
ROW_TOP = [10.4, 17.6, 24.8, 30.6]


def prismz_bm(pts, z0, z1):
    """2D outline (x, y) extruded along z from z0 to z1 (caps triangulated)."""
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, z0)) for x, y in pts]
    hi = [bm.verts.new((x, y, z1)) for x, y in pts]
    n = len(pts)
    caps = [bm.faces.new(list(reversed(lo))), bm.faces.new(hi)]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="BEAUTY")
    return bm


def spine_arc(th, y0, b, grow=0.0, n=6):
    """The rounded spine's front curve, x 0 .. th, bulging to y0 at the middle (grow: offset outwards)."""
    rx, ry = th / 2 + grow, b + grow
    return [(th / 2 - rx * math.cos(math.pi * i / n), y0 + b - ry * math.sin(math.pi * i / n)) for i in range(n + 1)]


# Spine colours (GC_D1 / GC_E1 shelves: dark brown, red-brown, slate, a few tan): the slots that read as bound leather.
BOOK_SLOTS = (("leather_brown", .27), ("leather_dark", .25), ("leather", .15), ("leather_tan", .15), ("leather_slate", .12), ("ink", .06))


def book_slot(rng):
    r, acc = rng.random(), 0.0
    for s, w in BOOK_SLOTS:
        acc += w
        if r < acc:
            return s
    return BOOK_SLOTS[0][0]


def book(B, M, x0, th, h, depth, slot, rng, lean=0.0, lie=False):
    """One book standing on z 0 at x0 .. x0 + th, rounded spine at the front (-y), raised brass ribs in pairs near
    head and tail, a brass title label, cream page edges showing at the head; lean = tilt about y (deg)."""
    y0 = -1.3 + rng.uniform(0, 0.1)
    b = min(0.24, th * 0.24)
    arc = spine_arc(th, y0, b)
    body = prismz_bm([(0, y0 + depth)] + arc + [(th, y0 + depth)], 0, h)
    parts = [(body, slot)]
    # pages: a cream block inset under the covers' edge at the head
    parts.append((box_bm(0.07, th - 0.07, y0 + b + 0.05, y0 + depth - 0.06, h - 0.25, h + 0.012), "parchment"))
    arc_band = spine_arc(th, y0, b, grow=0.045)
    shell = arc_band + [(th, y0 + b + 0.3), (0, y0 + b + 0.3)]
    # thin raised ribs: a pair at the tail, a pair at the head, two framing the title label between them
    for zc in (0.08, 0.12, 0.64, 0.74, 0.86, 0.90):
        parts.append((prismz_bm(shell, zc * h - 0.04, zc * h + 0.04), "brass"))
    if lie:   # lying on its side: thickness becomes the height
        R = Matrix.Translation((x0, 0, th)) @ Matrix.Rotation(math.radians(90), 4, "Y")
    else:
        R = Matrix.Translation((x0, 0, 0))
        if lean:
            pivot = th if lean > 0 else 0.0
            R = R @ Matrix.Translation((pivot, 0, 0)) @ Matrix.Rotation(math.radians(lean), 4, "Y") \
                @ Matrix.Translation((-pivot, 0, 0))
    for bm, s in parts:
        B.merge(bm, s, M @ R)


def book_row(B, M, z0, clear, rng):
    x, x_end = -6.15, 6.15
    lean_left = 0
    while x < x_end - 0.6:
        roll = rng.random()
        if roll < 0.025 and x < x_end - 4.5:      # now and then a short stack of books lying flat
            w = rng.uniform(3.6, 4.2)
            zz = z0
            for i in range(rng.randint(2, 3)):
                th = rng.uniform(0.6, 0.85)
                ww = w - i * rng.uniform(0.15, 0.35)
                book(B, M @ Matrix.Translation((0, 0, zz - z0)), x + (w - ww) / 2, th,
                     ww, rng.uniform(1.9, 2.2), book_slot(rng), rng, lie=True)
                zz += th
            x += w + 0.12
            continue
        th = rng.uniform(0.7, 1.15)
        if x + th > x_end:
            break
        # the shelves read as packed rows of near-equal volumes (a set now and then is a step lower)
        h = clear * (rng.uniform(0.84, 0.92) if rng.random() < 0.8 else rng.uniform(0.74, 0.8))
        slot = book_slot(rng)
        if roll > 0.975 and lean_left <= 0 and x < x_end - 2.5:   # a leaning book after a gap
            tilt = rng.uniform(10, 17)
            gap = h * math.sin(math.radians(tilt)) + 0.1
            book(B, M, x + gap - th * 0.0, th, h * 0.95, rng.uniform(1.9, 2.2), slot, rng, lean=-tilt)
            x += gap + th + 0.05
            lean_left = 4
            continue
        book(B, M, x, th, h, rng.uniform(1.9, 2.25), slot, rng)
        x += th + rng.uniform(0.0, 0.02)
        lean_left -= 1


def build_bookcase(col):
    B = Builder()
    I = Matrix.Identity(4)
    W, F, K = BC_W, BC_FRONT, BC_BACK
    # plinth: body, recessed front panel with a brass inlay, top lip
    box(B, "walnut", -W, W, F - 0.1, K, 0.0, 3.0, bev=0.08, seg=2)
    box(B, "walnut", -W - 0.15, W + 0.15, F - 0.3, K, 0.0, 0.45, bev=0.1, seg=2)          # toe moulding
    box(B, "walnut", -W + 0.9, W - 0.9, F - 0.22, F - 0.1, 0.8, 2.5, bev=0.06, seg=2)     # raised panel
    inlay_rect(B, I, -W + 1.2, W - 1.2, 1.05, 2.25, F - 0.22)
    box(B, "walnut", -W - 0.1, W + 0.1, F - 0.35, K, 3.0, 3.6, bev=0.12, seg=2)           # lip under the books
    # side posts (fluted look: two brass inlay lines), bases and capitals
    for s in (-1, 1):
        x0, x1 = (W - 1.2, W) if s > 0 else (-W, -W + 1.2)
        box(B, "walnut", x0, x1, F - 0.2, K, 3.6, 30.6, bev=0.1, seg=2)
        xc = (x0 + x1) / 2
        for dx in (-0.25, 0.25):
            box(B, "brass", xc + dx - 0.06, xc + dx + 0.06, F - 0.25, F - 0.15, 4.8, 29.4)
        box(B, "walnut", x0 - 0.12, x1 + 0.12, F - 0.38, K, 3.6, 4.4, bev=0.1, seg=2)       # base block
        box(B, "brass", x0 - 0.13, x1 + 0.13, F - 0.4, K - 0.2, 4.4, 4.6, bev=0.04)
        box(B, "brass", x0 - 0.13, x1 + 0.13, F - 0.4, K - 0.2, 29.6, 29.8, bev=0.04)
        box(B, "walnut", x0 - 0.12, x1 + 0.12, F - 0.38, K, 29.8, 30.6, bev=0.1, seg=2)     # capital
    # back and inner sides of the cases (darker varnish reads as depth)
    box(B, "varnished", -W + 1.2, W - 1.2, K - 0.35, K, 3.6, 30.6)
    # shelves with a brass edge
    for (z0, z1) in SHELVES[1:]:
        box(B, "walnut", -W + 1.2, W - 1.2, F - 0.05, K - 0.35, z0, z1, bev=0.06, seg=2)
        box(B, "brass", -W + 1.2, W - 1.2, F - 0.12, F - 0.02, z0 + 0.12, z0 + 0.3, bev=0.03)
    # frieze with an inlaid panel, stepped cornice crown
    box(B, "walnut", -W, W, F - 0.25, K, 30.6, 32.4, bev=0.08, seg=2)
    inlay_rect(B, I, -W + 1.6, W - 1.6, 30.95, 32.05, F - 0.25)
    box(B, "brass", -W - 0.2, W + 0.2, F - 0.42, K, 32.4, 32.6, bev=0.04)
    box(B, "walnut", -W - 0.35, W + 0.35, F - 0.6, K, 32.6, 33.3, bev=0.15, seg=2)
    box(B, "walnut", -W - 0.6, W + 0.6, F - 0.85, K, 33.3, 34.0, bev=0.2, seg=2)
    # books
    rng = random.Random(4)
    tops = [3.6] + [z1 for (_, z1) in SHELVES[1:]]
    for z0, top in zip(tops, ROW_TOP):
        book_row(B, Matrix.Translation((0, 0, z0)), z0, top - z0, rng)
    # place it at the first bookcase (-35, -140 in Unity), front towards the course (+x)
    o = B.obj("RlGalleryBookcase", col)
    o.matrix_world = at(-35.0, -140.0, FLOOR, 90)
    return o


# ------------------------------------------------------------------ gallery walls (E1 walnut hall, D5 stone + window)
# Wall frames: local x along the wall, the blockout wall's inner face at y = 0, the room at -y, z up (world z).
LEFT = at(-WALL_X, 0, 0, 90)        # local x = world y (-149 .. -10)
RIGHT = at(WALL_X, 0, 0, -90)       # local x = -world y (10 .. 149)
ENTRANCE = at(0, -149.0, 0, 180)    # local x = -world x (-37.25 .. 37.25)
WIN_W, WIN_SILL, WIN_SPRING = 3.6, 16.8, 23.6     # half width, sill, springing (crown 27.2)
CORNICE = 28.5


def run_mouldings(B, M, u0, u1, dado=True):
    """Skirting, dado rail and cornice along u0 .. u1 (continuous past the pilasters)."""
    box(B, "walnut", u0, u1, -0.55, 0, FLOOR, FLOOR + 1.4, bev=0.1, seg=2, M=M)
    box(B, "brass", u0, u1, -0.66, -0.4, FLOOR + 1.4, FLOOR + 1.55, M=M)
    if dado:
        box(B, "walnut", u0, u1, -0.7, 0, DADO0, DADO0 + 0.7, bev=0.15, seg=2, M=M)
        box(B, "walnut", u0, u1, -0.95, 0, DADO0 + 0.7, DADO1, bev=0.18, seg=2, M=M)
        box(B, "brass", u0, u1, -1.06, -0.9, DADO0 + 0.88, DADO0 + 1.02, M=M)
    box(B, "walnut", u0, u1, -0.8, 0, CORNICE, CORNICE + 0.5, bev=0.15, seg=2, M=M)
    box(B, "walnut", u0, u1, -1.3, 0, CORNICE + 0.5, CEIL, bev=0.2, seg=2, M=M)
    box(B, "brass", u0, u1, -0.9, -0.76, CORNICE + 0.1, CORNICE + 0.25, M=M)


def pilaster(B, M, u):
    box(B, "walnut", u - 1.2, u + 1.2, -0.75, 0, FLOOR, CORNICE, bev=0.1, seg=2, M=M)
    box(B, "walnut", u - 1.45, u + 1.45, -0.95, 0, FLOOR, FLOOR + 2.2, bev=0.12, seg=2, M=M)
    box(B, "brass", u - 1.5, u + 1.5, -1.05, -0.1, FLOOR + 2.2, FLOOR + 2.4, M=M)
    box(B, "brass", u - 1.5, u + 1.5, -1.1, -0.1, 26.9, 27.1, M=M)
    box(B, "walnut", u - 1.45, u + 1.45, -1.0, 0, 27.1, CORNICE, bev=0.12, seg=2, M=M)
    for du in (-0.45, 0.45):
        box(B, "brass", u + du - 0.06, u + du + 0.06, -0.86, -0.72, FLOOR + 3.0, 26.4, M=M)


def window(B, M, cx):
    """Round-headed window high in the stone: surround, reveal, sill, keystone, walnut glazing bars, dark glass."""
    w, z0, zs = WIN_W, WIN_SILL, WIN_SPRING
    loop = lambda g, ww=w: arch_loop(cx, ww, z0, zs, 14, g)   # noqa: E731
    B.merge(ring_bm(loop(0.9), loop(0.0), -0.5, 0.0), "stone", M)          # surround
    B.merge(ring_bm(loop(0.25), loop(0.0), -0.02, 0.8), "stone", M)        # reveal
    B.merge(prism_bm(loop(0.1), 0.75, 0.85), "ink", M)                     # glass (shape only, R7 lights it)
    B.merge(ring_bm(loop(0.0), loop(-0.28), 0.4, 0.68), "walnut", M)       # sash frame
    for dx in (-w / 3, 0.0, w / 3):
        top = zs + math.sqrt(max(w * w - dx * dx, 0)) - 0.2
        box(B, "walnut", cx + dx - 0.12, cx + dx + 0.12, 0.43, 0.66, z0, top, M=M)
    for z in (z0 + 2.3, z0 + 4.55, zs):
        box(B, "walnut", cx - w + 0.1, cx + w - 0.1, 0.43, 0.66, z - 0.12, z + 0.12, M=M)
    box(B, "stone", cx - w - 1.15, cx + w + 1.15, -0.95, 0, z0 - 0.55, z0, bev=0.1, seg=2, M=M)   # sill
    crown = zs + w
    B.merge(prism_bm([(cx - 0.55, crown + 0.05), (cx + 0.55, crown + 0.05), (cx + 0.75, crown + 1.25),
                      (cx - 0.75, crown + 1.25)], -0.75, 0.0, bev=0.06, seg=2), "stone", M)   # keystone


def bay(B, M, u0, u1, wainscot=True, win=True):
    """Between two pilaster centres: walnut wainscot with raised, brass-inlaid panels, cream stone above."""
    a0, a1 = u0 + 1.2, u1 - 1.2
    if wainscot:
        box(B, "walnut", a0, a1, -0.12, 0, FLOOR, DADO0, M=M)
        n = max(1, int(round((a1 - a0) / 8.5)))
        step = (a1 - a0) / n
        for i in range(n):
            x0, x1 = a0 + i * step + 0.45, a0 + (i + 1) * step - 0.45
            for (z0, z1) in ((FLOOR + 2.3, -4.6), (-3.6, DADO0 - 0.9)):
                box(B, "walnut", x0, x1, -0.36, -0.1, z0, z1, bev=0.12, seg=2, M=M)
                inlay_rect(B, M, x0 + 0.55, x1 - 0.55, z0 + 0.55, z1 - 0.55, -0.36, w=0.12, t=0.1)
    box(B, "stone", a0, a1, -0.25, 0, DADO1, DADO1 + 0.35, bev=0.08, seg=2, M=M)     # string course
    if not win:
        box(B, "stone", a0, a1, -0.06, 0, DADO1, CORNICE, M=M)
        return
    # cream stone face around the window's surround (outer outline = arch_loop grown 0.9)
    cx = (u0 + u1) / 2
    R = WIN_W + 0.9
    box(B, "stone", a0, cx - R, -0.06, 0, DADO1, CORNICE, M=M)
    box(B, "stone", cx + R, a1, -0.06, 0, DADO1, CORNICE, M=M)
    box(B, "stone", cx - R, cx + R, -0.06, 0, DADO1, WIN_SILL - 0.9, M=M)
    n = 14
    spandrel = [(cx + R * math.cos(math.pi * i / n), WIN_SPRING + R * math.sin(math.pi * i / n)) for i in range(n + 1)]
    spandrel += [(cx - R, CORNICE), (cx + R, CORNICE)]
    B.merge(prism_bm(spandrel, -0.06, 0.0), "stone", M)
    window(B, M, cx)


def wall_run(B, M, posts, dado=True, wainscot=True):
    for u in posts:
        pilaster(B, M, u)
    for u0, u1 in zip(posts, posts[1:]):
        bay(B, M, u0, u1, wainscot=wainscot)
    run_mouldings(B, M, posts[0] - 1.2, posts[-1] + 1.2, dado=dado)


def build_walls(col):
    B = Builder()
    side = [-147.8, -130, -110, -90, -70, -50, -30, -11.2]
    wall_run(B, LEFT, side)
    wall_run(B, RIGHT, [-u for u in reversed(side)])
    # entrance wall: two runs either side of the portal; the portal zone (|x| < 10.6) is plain stone for R1's arch,
    # with the portal hole (x +-6, z -10 .. 10) left open; the cornice runs right across
    for posts in ([-36.05, -23.3, -10.6], [10.6, 23.3, 36.05]):
        wall_run(B, ENTRANCE, posts)
    E = ENTRANCE
    box(B, "stone", -9.4, -6.0, -0.06, 0, FLOOR, CORNICE, M=E)
    box(B, "stone", 6.0, 9.4, -0.06, 0, FLOOR, CORNICE, M=E)
    box(B, "stone", -6.0, 6.0, -0.06, 0, FLOOR, -10.0, M=E)
    box(B, "stone", -6.0, 6.0, -0.06, 0, 10.0, CORNICE, M=E)
    box(B, "walnut", -9.4, 9.4, -0.55, 0, FLOOR, FLOOR + 1.4, bev=0.1, seg=2, M=E)
    box(B, "walnut", -9.4, 9.4, -0.8, 0, CORNICE, CORNICE + 0.5, bev=0.15, seg=2, M=E)
    box(B, "walnut", -9.4, 9.4, -1.3, 0, CORNICE + 0.5, CEIL, bev=0.2, seg=2, M=E)
    return B.obj("RlGalleryWalls", col)


# ------------------------------------------------------------------ ceiling and far floor
BAY_Y = [-130, -110, -90, -70, -50, -30]     # pilaster lines along the hall (world y)


def build_ceiling(col):
    """Coffered ceiling: cream plaster field, walnut cross beams on the pilaster lines, two long beams, brass
    rosettes in the coffers. Underside of the field = the blockout ceiling's underside (z 29.5)."""
    B = Builder()
    X, Y0, Y1 = WALL_X, -149.0, -10.0
    box(B, "stone", -X, X, Y0, Y1, CEIL, CEIL + 1.0, M=Matrix.Identity(4))
    for y in [Y0 + 0.9] + BAY_Y + [Y1 - 0.9]:
        box(B, "walnut", -X, X, y - 0.9, y + 0.9, CEIL - 1.6, CEIL, bev=0.15, seg=2)
        box(B, "brass", -X, X, y - 0.5, y + 0.5, CEIL - 1.72, CEIL - 1.6)
    for x in (-12.4, 12.4):
        box(B, "walnut", x - 0.7, x + 0.7, Y0, Y1, CEIL - 1.2, CEIL, bev=0.12, seg=2)
    ys = [Y0 + 0.9] + BAY_Y + [Y1 - 0.9]
    rosette = [(0, -0.45), (0.5, -0.45), (0.9, -0.35), (1.25, -0.18), (1.45, 0.0), (0, 0.0)]
    for ya, yb in zip(ys, ys[1:]):
        for xa, xb in ((-X, -12.4), (-12.4, 12.4), (12.4, X)):
            B.merge(lathe_bm(rosette, 20), "brass", Matrix.Translation(((xa + xb) / 2, (ya + yb) / 2, CEIL)))
    return B.obj("RlGalleryCeiling", col)


def build_floor(col):
    """Far floor (blockout top z -22.5): walnut boards along the hall with staggered joints, cream stone border."""
    B = Builder()
    X, Y0, Y1 = WALL_X, -149.0, -10.0
    border = 1.6
    # (no sub-floor slab: the boards and border cover the floor, nothing sees under it)
    for (x0, x1, y0, y1) in ((-X, X, Y0, Y0 + border), (-X, X, Y1 - border, Y1),
                             (-X, -X + border, Y0 + border, Y1 - border), (X - border, X, Y0 + border, Y1 - border)):
        box(B, "stone", x0, x1, y0, y1, FLOOR - 0.6, FLOOR, bev=0.05)
    inner = X - border
    n = int(round(2 * inner / 1.8))
    w = 2 * inner / n
    rng = random.Random(7)
    for i in range(n):
        x0 = -inner + i * w
        y = Y0 + border
        first = rng.uniform(6, 26)
        cuts = [y]
        y += first
        while y < Y1 - border - 4:
            cuts.append(y)
            y += rng.uniform(22, 32)
        cuts.append(Y1 - border)
        for ya, yb in zip(cuts, cuts[1:]):
            box(B, "walnut", x0 + 0.03, x0 + w - 0.03, ya + 0.03, yb - 0.03, FLOOR - 0.6, FLOOR, bev=0.05)
    return B.obj("RlGalleryFloor", col)


# ------------------------------------------------------------------ framed boards (D5 #3) and planters (plants sheet)
def lathe_y(B, prof, slot, x, z, seg=24, y=0.0):
    """A lathe facing the room (-y) at (x, y, z) in a wall frame (built about +z, turned so +z points to -y)."""
    return B.merge(lathe_bm(prof, seg), slot, Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(90), 4, "X"))


def build_frame(col):
    """Gilded frame and back panel for one 10 x 10 m board. Local frame: board front y = 0 (the room at -y), centre
    at the origin. The squares (|x|, |z| < 4, y -0.09 .. -0.01), WarRoomScoreLabel's piece markers (y -0.25) and its
    caption (z 4.65, in the top margin, y -0.35) stay in front: nothing here sits over |x|, |z| < 5 nearer than y 0
    except the fine brass fillet just outside the squares (4.05 .. 4.2)."""
    B = Builder()
    I = Matrix.Identity(4)
    box(B, "varnished", -5.0, 5.0, 0.0, 0.5, -5.0, 5.0)
    inlay_rect(B, I, -4.2, 4.2, -4.2, 4.2, 0.0, w=0.15, t=0.05)
    ring = lambda hi, ho, ri, ro, y0, y1, s: B.merge(  # noqa: E731
        ring_bm(rrect_loop(ho, ho, ro, 4), rrect_loop(hi, hi, ri, 4), y0, y1), s, I)
    ring(5.0, 5.85, 0.06, 0.45, -0.4, 0.5, "brass")       # moulding body
    ring(5.0, 5.28, 0.06, 0.12, -0.62, -0.38, "brass")    # inner bead
    ring(5.55, 5.92, 0.3, 0.48, -0.58, -0.38, "brass")    # outer bead
    ring(5.85, 6.05, 0.45, 0.6, 0.0, 0.5, "varnished")    # shadow lip against the wall
    # crests (top stays under the dado rail at world z 14.2 = local 6.2), side bosses, corner rosettes
    n = 16
    shell = [(1.45 * math.cos(math.pi * i / n), 1.2 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    for sgn in (1, -1):
        zc = 4.92 * sgn
        pts = [(x, zc + z * sgn) for x, z in shell]
        if sgn < 0:
            pts.reverse()
        B.merge(prism_bm(pts, -0.78, -0.3, bev=0.08, seg=2), "brass", I)
        lathe_y(B, [(0, 0), (0.42, 0), (0.42, 0.08), (0.3, 0.2), (0, 0.26)], "brass", 0, zc + 0.45 * sgn, y=-0.78)
    for x in (-5.45, 5.45):
        B.merge(prism_bm([(x + 0.55 * math.cos(2 * math.pi * i / 12), 0.95 * math.sin(2 * math.pi * i / 12))
                          for i in range(12)], -0.72, -0.3, bev=0.08, seg=2), "brass", I)
    for x in (-5.45, 5.45):
        for z in (-5.45, 5.45):
            lathe_y(B, [(0, 0), (0.55, 0), (0.55, 0.1), (0.4, 0.25), (0.15, 0.33), (0, 0.35)], "brass", x, z, y=-0.6)
    o = B.obj("RlGalleryFrame", col)
    o.matrix_world = at(-36.75, -121.0, 8.0, 90)
    return o


PLANTERS = [  # model, houseplant (prefab name), Blender position, scale of the blockout pot (unit pot: r 3, h 4)
    ("RlPlanterFluted112", "Library gallery houseplant (28, -22.5, -112)", (28, -112), 1.5, "fluted"),
    ("RlBrassTub53", "Library gallery houseplant (29, -22.5, -53)", (29, -53), 1.8, "tub"),
    ("RlPlanterFluted35", "Library gallery houseplant (-28, -22.5, -35)", (-28, -35), 1.2, "fluted"),
]
# brass lip shared by both: inner radius 2.88 hugs the blockout soil disc (r 2.85, top 4.35) that stays
RIM = [(2.86, 3.8), (3.18, 3.86), (3.27, 4.05), (3.22, 4.32), (3.08, 4.44), (2.88, 4.44), (2.88, 4.15), (0, 4.15)]


def build_planter(col, kind, s):
    B = Builder()
    S = Matrix.Scale(s, 4)
    if kind == "fluted":   # cream fluted bowl on a turned foot (plants sheet, cream pots)
        B.merge(lathe_bm([(0, 0), (2.3, 0), (2.42, 0.1), (2.42, 0.32), (2.3, 0.42), (2.45, 0.52), (0, 0.52)], 48),
                "stone", S)
        body = [(0, 0.5), (2.5, 0.5), (2.78, 0.95), (2.98, 1.7), (3.06, 2.5), (3.04, 3.3), (2.94, 3.85), (0, 3.85)]
        B.merge(lathe_bm(body, 128, wobble=lambda t: 1.0 - 0.06 * abs(math.sin(8 * t)) ** 0.6), "stone", S)
    else:                  # brass tub: base band, slightly flared wall
        B.merge(lathe_bm([(0, 0), (2.74, 0), (2.92, 0.06), (2.97, 0.25), (2.97, 0.52), (2.86, 0.6), (2.86, 0.72),
                          (2.95, 1.6), (3.03, 2.8), (3.06, 3.8), (0, 3.8)], 64), "brass", S)
        B.merge(lathe_bm([(0, 1.0), (2.98, 1.0), (3.02, 1.1), (3.02, 1.25), (2.98, 1.32), (0, 1.32)], 64), "brass", S)
    B.merge(lathe_bm(RIM, 64), "brass", S)
    return B


def build_frames_and_planters():
    """(collection, object, model, frame) for the frame and each planter."""
    out = []
    col = new_collection("R4 frame")
    out.append((col, build_frame(col), "RlGalleryFrame", "Random GM replay preview 0"))
    for model, host, (x, y), s, kind in PLANTERS:
        col = new_collection("R4 " + model)
        o = build_planter(col, kind, s).obj(model, col)
        o.matrix_world = Matrix.Translation((x, y, FLOOR))
        out.append((col, o, model, host))
    return out


# ------------------------------------------------------------------ preview / export
HALL_Y0, HALL_Y1 = -149.0, -10.0     # the hall's inside: |x| < WALL_X, HALL_Y0 < y < HALL_Y1, FLOOR < z < CEIL


def cull_outside(o, kind):
    """Delete the faces only the outside could show (11-18 "코스 외관 필요 없는 부분 지워"): backs of every piece
    lying on the wall planes (x = +-37.25, y = -149) facing out, faces at the hall's open end (y = -10) facing the
    chess room's wall, tops at the ceiling (z 29.5) and, for the ceiling, everything above its underside, for the
    floor, every face looking down. Inside faces and the prefab's colliders are untouched. Returns faces removed."""
    import bmesh as bmm
    bm = bmm.new()
    bm.from_mesh(o.data)
    e = 0.012
    kill = []
    for f in bm.faces:
        c, n = f.calc_center_median(), f.normal
        out = ((n.x > 0.7 and c.x >= WALL_X - e) or (n.x < -0.7 and c.x <= -WALL_X + e)
               or (n.y < -0.7 and c.y <= HALL_Y0 + e) or (n.y > 0.7 and c.y >= HALL_Y1 - e)
               or (n.z > 0.7 and c.z >= CEIL - e) or c.z > CEIL + e or (n.z < -0.7 and c.z <= FLOOR - 0.3))
        if kind == "floor" and n.z < -0.7:
            out = True
        if out:
            kill.append(f)
    bmm.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    return len(kill)


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def finish(o, kind=None):
    if kind in ("walls", "ceiling", "floor"):
        before = tris(o)
        cull_outside(o, kind)
        print("[r4] cull %s: %d -> %d tris" % (o.name, before, tris(o)))
        add_skin(o, kind)
    k.surface_uvs(o)


SKIN_X, SKIN_Y0, SKIN_TOP, SKIN_BOT = 38.75, -151.0, CEIL + 1.0, FLOOR - 0.6


def add_skin(o, kind):
    """Plain outer shell on the blockout walls' outer faces (x +-38.75, y -151), the ceiling's top and the floor's
    underside, so the hall is closed from outside (11-18 승규: no see-through, no flicker). One big quad a side (the
    entrance side keeps the portal hole x +-6, z -10 .. 10); nothing else lies on these planes."""
    import bmesh as bmm
    from mathutils import Vector as V
    me = o.data
    name = "RL | walnut" if kind == "walls" else "RL | stone"
    if name not in [m.name for m in me.materials]:
        me.materials.append(k.slot(name))
    mi = [m.name for m in me.materials].index(name)
    X, Y0, Y1, T, Bt = SKIN_X, SKIN_Y0, HALL_Y1, SKIN_TOP, SKIN_BOT
    quads = []   # (corners, outward normal)
    if kind == "walls":
        for s in (-1, 1):
            quads.append(([(s * X, Y0, Bt), (s * X, Y1, Bt), (s * X, Y1, T), (s * X, Y0, T)], V((s, 0, 0))))
        for (x0, x1, z0, z1) in ((-X, -6, Bt, T), (6, X, Bt, T), (-6, 6, Bt, -10), (-6, 6, 10, T)):
            quads.append(([(x0, Y0, z0), (x1, Y0, z0), (x1, Y0, z1), (x0, Y0, z1)], V((0, -1, 0))))
    elif kind == "ceiling":
        quads.append(([(-X, Y0, T), (X, Y0, T), (X, Y1, T), (-X, Y1, T)], V((0, 0, 1))))
    else:
        quads.append(([(-X, Y0, Bt), (X, Y0, Bt), (X, Y1, Bt), (-X, Y1, Bt)], V((0, 0, -1))))
    bm = bmm.new()
    bm.from_mesh(me)
    inv = o.matrix_world.inverted()
    for pts, n in quads:
        f = bm.faces.new([bm.verts.new(inv @ V(p)) for p in pts])
        f.normal_update()
        if f.normal.dot(n) < 0:
            f.normal_flip()
        f.material_index = mi
        f.smooth = False
    bm.to_mesh(me)
    bm.free()


def export(col, model, frame_name=None):
    k.export(col.name, model, ZONE, frame_name)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["none"]
    parts = set(argv[0].split(",")) if argv else set()
    if "--preview" in argv:
        k.ROOT = argv[argv.index("--preview") + 1]
    if "all" in parts:
        parts = {"bookcase", "walls", "ceiling", "floor", "frame", "planters"}
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    builds = {"bookcase": ("R4 bookcase", build_bookcase, "RlGalleryBookcase", "Royal library bookcase -35 -140"),
              "walls": ("R4 walls", build_walls, "RlGalleryWalls", None),
              "ceiling": ("R4 ceiling", build_ceiling, "RlGalleryCeiling", None),
              "floor": ("R4 floor", build_floor, "RlGalleryFloor", None)}
    made = []
    for key, (cname, fn, model, frame_name) in builds.items():
        col = new_collection(cname)
        made.append((key, col, fn(col), model, frame_name))
    for col, o, model, frame_name in build_frames_and_planters():
        made.append(("frame" if model == "RlGalleryFrame" else "planters", col, o, model, frame_name))
    for key, col, o, model, frame_name in made:
        finish(o, key)
        print("[r4] %s %d tris" % (model, sum(len(p.vertices) - 2 for p in o.data.polygons)))
        if key in parts:
            export(col, model, frame_name)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


main()
