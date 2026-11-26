"""B1 the entry passage, the servery and the waiters' parade (seoungtest R837, 2026-11-25; PLAN.md B1), every part after
its cell of the R836 sheets BH_A03 (screens, counter, kitchen door, pantry frame, plate stack) and BH_A11 (the waiter).

    blender -b --factory-startup --python bh_b1_servery_model.py -- [stills] [export]
    -> bh_b1_servery.blend; "stills": stills/b1/parts/<part>_compare.png + stills/b1/servery.png;
       "export": Assets/Resources/BanquetHall/Models/B1/*.fbx (never while Unity is in Play).

Numbers are GrandBanquetHallBuilder's (Shell, Servery, WaitersParade); colliders, walking faces and movers stay its:
  screens   "BH screen S" x -23.5 .. 13.2, z 5.8 .. 6.2, 6 m (+ the brass cap to 6.3); "BH screen E" x 12.8 .. 13.2,
            z 6.2 .. 18; "BH passage end" x -6.4 .. -6, z 0 .. 5.8
  counter   x 22 .. 26, z 4 .. 12, top 2.4 (a floor); its ramp along x 15 .. 22 (0 -> 2.4), z 5 .. 11 (a floor: the
            sheet's steps are a walnut ramp here, gold nosings inlaid flush, stepped stringers on its sides)
  kitchen   west wall x 25.7 .. 26, z 0 .. 4, 7.8 high; roof 7.5 .. 7.8; the doorway x 26 .. 32 under 7.2 at z 4 (the
            waiters walk out through it: the half doors stand swung back inside, off the trays' way, x >= 26.3)
  pantry    the doorway x 26 .. 31.7 under 7.2 at z 26; its west wall over the table's east edge (y 4.4 .. 7.8, z 26 .. 56)
  waiter    (moves) root "BH waiter k" at (27.8, 0, z): the tray (a floor) r 1.5, top 2.4; the plate stack r 0.8 to 5.0
            (climbed, its top a floor); the body (not climbed) r 1 at x +2, 5.4 high, the head 1.2 across at 6.05.
            The model is built in the root's frame (tray at the origin): nothing of it stands over the tray but the stack.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\BanquetHall"
sys.path.insert(0, HERE)
import importlib
import bh_kit as k
import bh_parts as P
import bh_stills as st
for _m in (k, P, st):
    importlib.reload(_m)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
A03 = os.path.join(st.SHEETS, "BH_A03_servery_screens.png")
A11 = os.path.join(st.SHEETS, "BH_A11_guests_staff.png")
CELLS = {"screen": (A03, (25, 320, 335, 712)), "counter": (A03, (335, 385, 738, 700)), "kitchen": (A03, (745, 325, 1032, 712)),
         "pantry": (A03, (1050, 338, 1322, 695)), "stack": (A03, (1335, 435, 1515, 698)), "waiter": (A11, (38, 200, 288, 828))}

TRAY_Y, TRAY_R, STACK_R, STACK_TOP, BODY_X = 2.4, 1.5, .8, 5.0, 2.0


def U(x, y, z):
    return Vector((x, z, y))


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    for o in list(c.objects):
        bpy.data.objects.remove(o)
    return c


def basis(along, out, at):
    """A frame on an upright face: local z out of it, y up, x to the right seen from in front (`along` only names the
    run; the panels are symmetric), at `at`."""
    out = out.normalized()
    up = Vector((0, 0, 1))
    right = up.cross(out)
    m = Matrix((right, up, out)).transposed().to_4x4()
    m.translation = at
    return m


def plane_prism(outline, depth, mat, m):
    """An outline (u along, v up) raised `depth` out of a face, placed by m (basis())."""
    bm = k.prism(outline, 0.0, depth, mat=mat)
    return P.transform(bm, m)


def arched(w, h, shoulder):
    """A tall panel's outline with the sheet's cove-cut top corners (u -w/2..w/2, v 0..h): a quarter circle of
    `shoulder` cut into each top corner."""
    pts = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, h - shoulder)]
    for i in range(1, 6):
        a = math.pi / 2 * i / 6
        pts.append((w / 2 - shoulder * math.sin(a), h - shoulder * math.cos(a)))
    pts.append((w / 2 - shoulder, h))
    pts.append((-w / 2 + shoulder, h))
    for i in range(1, 6):
        a = math.pi / 2 * i / 6
        pts.append((-w / 2 + shoulder * math.cos(a), h - shoulder * math.sin(a)))
    pts.append((-w / 2, h - shoulder))
    return pts


def rect(w, h):
    return [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, h), (-w / 2, h)]


def frame_bead(outline, m, r, mat, lift=.0):
    pts = [m @ Vector((u, v, lift)) for u, v in outline] + [m @ Vector((outline[0][0], outline[0][1], lift))]
    return P.tube(pts, r, 6, mat=mat)


def frustum(outline, inset, height, mat):
    """A raised field with chamfered edges: the outline at 0, the same outline shrunk by `inset` (per axis) at `height`
    (local z), the top capped."""
    us = [u for u, v in outline]; vs = [v for u, v in outline]
    cu, cv = (min(us) + max(us)) / 2, (min(vs) + max(vs)) / 2
    ku = max(.05, (max(us) - min(us) - 2 * inset) / max(1e-6, max(us) - min(us)))
    kv = max(.05, (max(vs) - min(vs) - 2 * inset) / max(1e-6, max(vs) - min(vs)))
    bm = bmesh.new()
    lo = [bm.verts.new((u, v, 0.0)) for u, v in outline]
    hi = [bm.verts.new((cu + (u - cu) * ku, cv + (v - cv) * kv, height)) for u, v in outline]
    n = len(outline)
    for i in range(n):
        bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])).material_index = mat
    bm.faces.new(hi).material_index = mat
    bm.faces.new(list(reversed(lo))).material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def frame_ring(outline, r, mat=None):
    pts = [Vector((u, v, 0.0)) for u, v in outline] + [Vector((outline[0][0], outline[0][1], 0.0))]
    return P.tube(pts, r, 6, mat=P.GOLD if mat is None else mat)


def sunk_panel(w, h, m):
    """A deep panel on a face (frame m: x across, y up from its foot, z out): a chamfered moulded frame standing 10 cm
    proud, a gold bead inside it, and the raised chamfered field standing 18 cm proud."""
    bm = bmesh.new()
    P.merge(bm, P.transform(frustum(rect(w, h), .08, .1, P.WALNUT), m))
    P.merge(bm, P.transform(frame_ring(rect(w - .24, h - .24), .026), m @ Matrix.Translation((0, .12, .1))))
    P.merge(bm, P.transform(frustum(rect(w - .4, h - .4), .1, .18, P.WALNUT), m @ Matrix.Translation((0, .2, 0))))
    return bm


# ------------------------------------------------------------------ the screens (BH_A03 first cell)

def post(bm, at, height, w=.6):
    """A screen post at `at` (Blender, its foot): a plinth with a gold band, a square walnut shaft, a cap block with a
    gold fillet under it, a gold ball on a turned neck."""
    x, y, z = at
    P.merge(bm, P.box((x - w * .62, y - w * .62, z), (x + w * .62, y + w * .62, z + .55), P.WALNUT))
    P.merge(bm, P.box((x - w * .64, y - w * .64, z + .55), (x + w * .64, y + w * .64, z + .63), P.GOLD))
    P.merge(bm, P.box((x - w / 2, y - w / 2, z + .63), (x + w / 2, y + w / 2, z + height - .45), P.WALNUT))
    P.merge(bm, P.box((x - w * .56, y - w * .56, z + height - .45), (x + w * .56, y + w * .56, z + height - .38), P.GOLD))
    P.merge(bm, P.box((x - w * .6, y - w * .6, z + height - .38), (x + w * .6, y + w * .6, z + height), P.WALNUT))
    ball = k.lathe([(0.0, 0.0), (w * .32, 0.0), (w * .26, .06), (w * .14, .1), (w * .14, .16), (w * .3, .2)] +
                   [(w * .36 * math.sin(math.pi * i / 10), .2 + w * .36 * (1 - math.cos(math.pi * i / 10))) for i in range(1, 11)],
                   20, mat=P.GOLD)
    P.merge(bm, P.transform(ball, Matrix.Translation((x, y, z + height))))


def screen_run(col, name, a, b, height=6.0, thick=.4, modules=None, ends=(True, True)):
    """A run of screen from a to b (Blender points at the floor, the screen's middle line): walnut panelling on both
    faces between posts every module (the sheet: three tall cove-topped panels over three short ones, raised fields,
    gold beads round the tall ones), a plinth with a gold strip, a top rail under the gold cap (6.0 - 6.3)."""
    bm = bmesh.new()
    d = b - a
    length = d.length
    along = d.normalized()
    side = Vector((0, 0, 1)).cross(along)       # the screen's face normal (one side)
    n = modules or max(1, round(length / 6.1))
    step = length / n
    # the slab, the plinth, the top rail and the cap - one box each along the run
    def run_box(y0, y1, half, mat, x0=0.0, x1=None):
        x1 = length if x1 is None else x1
        bb = P.box((x0, -half, y0), (x1, half, y1), mat)
        m = basis(along, side, a)
        # basis: local x along, y up, z out - for the box (x along, y across, z up) use a plain frame
        f = Matrix((along, side, Vector((0, 0, 1)))).transposed().to_4x4()
        f.translation = a
        return P.transform(bb, f)
    P.merge(bm, run_box(0.0, 5.75, thick / 2, P.WALNUT))
    P.merge(bm, run_box(0.0, .4, thick / 2 + .05, P.WALNUT))
    P.merge(bm, run_box(.4, .46, thick / 2 + .06, P.GOLD))
    P.merge(bm, run_box(5.6, 6.0, thick / 2 + .05, P.WALNUT))
    P.merge(bm, run_box(6.0, 6.08, thick / 2 + .1, P.GOLD))
    P.merge(bm, run_box(6.08, 6.22, thick / 2 + .06, P.GOLD))
    P.merge(bm, run_box(6.22, 6.3, thick / 2 + .12, P.GOLD))
    for i in range(n + 1):
        if (i == 0 and not ends[0]) or (i == n and not ends[1]):
            continue
        post(bm, a + along * (step * i), height + .3 + .35)
    # panels on both faces
    for i in range(n):
        u0 = step * i + .45
        u1 = step * (i + 1) - .45
        w_all = u1 - u0
        pw = (w_all - .3 * 2) / 3
        for s in (-1, 1):
            out = side * s
            face = a + out * (thick / 2)
            for j in range(3):
                cu = u0 + pw / 2 + j * (pw + .3)
                if s < 0:
                    m = basis(-along, out, face + along * cu)
                else:
                    m = basis(along, out, face + along * cu)
                # tall panel: a sunk frame (moulding) round a raised field
                hi = arched(pw, 3.0, .32)
                lo = rect(pw, 1.15)
                mt = m @ Matrix.Translation((0, 2.3, 0))
                ml = m @ Matrix.Translation((0, .75, 0))
                P.merge(bm, plane_prism(hi, .025, P.WALNUT, mt))
                inner = arched(pw - .26, 2.74, .22)
                P.merge(bm, plane_prism(inner, .07, P.WALNUT, mt @ Matrix.Translation((0, .13, 0))))
                P.merge(bm, frame_bead(hi, mt, .025, P.GOLD, .03))
                P.merge(bm, plane_prism(lo, .025, P.WALNUT, ml))
                P.merge(bm, plane_prism(rect(pw - .22, .93), .06, P.WALNUT, ml @ Matrix.Translation((0, .11, 0))))
                P.merge(bm, frame_bead(lo, ml, .018, P.WALNUT, .03))
            # the mid rail's gold line
            P.merge(bm, P.tube([face + along * (u0 - .1) + Vector((0, 0, 2.08)) + out * .03,
                                face + along * (u1 + .1) + Vector((0, 0, 2.08)) + out * .03], .02, 6))
    ob = P.obj(col, name, bm)
    return P.finish(ob, bevel=.012)


def screens(col):
    objs = [screen_run(col, "BH1 screen S", U(-23.5, 0, 6.0), U(13.0, 0, 6.0), modules=6),
            screen_run(col, "BH1 screen E", U(13.0, 0, 6.0), U(13.0, 0, 18.0), modules=2, ends=(False, True)),
            screen_run(col, "BH1 passage end", U(-6.2, 0, 0.0), U(-6.2, 0, 5.8), modules=1, ends=(True, False))]
    return objs


# ------------------------------------------------------------------ the counter and its ramp (BH_A03 second cell)

X0C, X1C, Z0C, Z1C, TOP = 22.0, 26.0, 4.0, 12.0, 2.4


def counter(col):
    bm = bmesh.new()
    # marble top (its upper face the floor's), a gold edge band, overhanging 8 cm
    P.merge(bm, P.box(U(X0C - .08, TOP - .16, Z0C - .08), U(X1C + .08, TOP, Z1C + .08), P.MARBLE))
    for (a0, a1, b0, b1) in ((X0C - .1, X1C + .1, Z0C - .1, Z0C - .08), (X0C - .1, X1C + .1, Z1C + .08, Z1C + .1),
                             (X0C - .1, X0C - .08, Z0C - .1, Z1C + .1), (X1C + .08, X1C + .1, Z0C - .1, Z1C + .1)):
        P.merge(bm, P.box(U(a0, TOP - .2, b0), U(a1, TOP - .02, b1), P.GOLD))
    P.merge(bm, P.box(U(X0C - .06, TOP - .36, Z0C - .06), U(X1C + .06, TOP - .2, Z1C + .06), P.GOLD))   # a gilt fillet under the top
    # the body: walnut, a plinth with a gold strip, pilasters at the corners and between panels, raised panels
    P.merge(bm, P.box(U(X0C + .04, 0, Z0C + .04), U(X1C - .04, TOP - .36, Z1C - .04), P.WALNUT))
    P.merge(bm, P.box(U(X0C - .02, 0, Z0C - .02), U(X1C + .02, .3, Z1C + .02), P.WALNUT))
    P.merge(bm, P.box(U(X0C - .03, .3, Z0C - .03), U(X1C + .03, .35, Z1C + .03), P.GOLD))
    faces = ((U(X0C, 0, Z0C), U(X0C, 0, Z1C), Vector((-1, 0, 0))), (U(X1C, 0, Z1C), U(X1C, 0, Z0C), Vector((1, 0, 0))),
             (U(X1C, 0, Z0C), U(X0C, 0, Z0C), Vector((0, -1, 0))), (U(X0C, 0, Z1C), U(X1C, 0, Z1C), Vector((0, 1, 0))))
    for a, b, out in faces:
        length = (b - a).length
        along = (b - a).normalized()
        n = max(1, round(length / 2.0))
        step = length / n
        for i in range(n + 1):
            c = a + along * (step * i) + out * .02
            P.merge(bm, P.box(c - along * .16 - out * .06, c + along * .16 + out * .06 + Vector((0, 0, TOP - .36)), P.WALNUT))
            P.merge(bm, P.box(c - along * .19 - out * .07 + Vector((0, 0, .35)), c + along * .19 + out * .08 + Vector((0, 0, .45)), P.GOLD))
        for i in range(n):
            cu = step * (i + .5)
            m = basis(along, out, a + along * cu + out * .02)
            w = step - .5
            P.merge(bm, plane_prism(rect(w, 1.3), .025, P.WALNUT, m @ Matrix.Translation((0, .55, 0))))
            P.merge(bm, plane_prism(rect(w - .24, 1.06), .065, P.WALNUT, m @ Matrix.Translation((0, .67, 0))))
    ob = P.finish(P.obj(col, "BH1 counter", bm), bevel=.012)
    # the ramp: its top the builder's slope (x 15 -> 22, 0 -> 2.4), gold nosings inlaid flush every 0.875 m, walnut
    # stringers stepped like the sheet's stairs on its sides (under the slope)
    rb = bmesh.new()
    x0, x1, z0, z1 = 15.0, X0C, 5.0, 11.0
    def yat(x):
        return TOP * (x - x0) / (x1 - x0)
    verts = []
    for (x, z) in ((x0, z0), (x1, z0), (x1, z1), (x0, z1)):
        verts.append(rb.verts.new(U(x, yat(x), z)))
    for (x, z) in ((x0, z0), (x1, z0), (x1, z1), (x0, z1)):
        verts.append(rb.verts.new(U(x, yat(x) - .3, z)))
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        rb.faces.new([verts[i] for i in f]).material_index = P.WALNUT
    bmesh.ops.recalc_face_normals(rb, faces=rb.faces)
    steps = 8
    for i in range(1, steps):
        x = x0 + (x1 - x0) * i / steps
        tilt = math.atan2(TOP, x1 - x0)
        strip = P.box((-.06, -.0, -.004), (.06, (z1 - z0) - .3, .004), P.GOLD)
        m = Matrix.Translation(U(x, yat(x) + .001, z0 + .15)) @ Matrix.Rotation(-tilt, 4, "Y")
        P.merge(rb, P.transform(strip, m))
    for z in (z0, z1):
        sgn = -1 if z == z0 else 1
        # a stepped stringer: each step's riser and tread under the slope line
        for i in range(steps):
            xa = x0 + (x1 - x0) * i / steps
            xb = x0 + (x1 - x0) * (i + 1) / steps
            ya = yat(xa)
            P.merge(rb, P.box(U(xa, 0, z + sgn * .0 - (.12 if sgn < 0 else 0)), U(xb, ya + .02, z + (0 if sgn < 0 else .12)), P.WALNUT))
        P.merge(rb, P.box(U(x0, 0, z - .14 if sgn < 0 else z), U(x1, .12, z if sgn < 0 else z + .14), P.WALNUT))
        P.merge(rb, P.box(U(x0, .12, z - .15 if sgn < 0 else z), U(x1, .16, z if sgn < 0 else z + .15), P.GOLD))
    ramp = P.finish(P.obj(col, "BH1 counter ramp", rb), bevel=.008)
    # the spare plates on the counter (the builder's three 1 m stacks, 0.8 high)
    stacks = []
    for i in range(3):
        sb = plate_stack(.5, .8, 6)
        stacks.append(P.finish(P.obj(col, "BH1 spare plates %d" % (i + 1), sb, Matrix.Translation(U(23 + i * 1.1, TOP, 5.2)))))
    return [ob, ramp] + stacks


# ------------------------------------------------------------------ the plate stack (BH_A03 last cell)

def stack_plate(r, h, segs=40):
    """A plate of the stack (BH_A03): a deep white body, its rim rolled up to a thin gold lip, a white foot under."""
    prof = [(0.0, h * .38), (r * .55, h * .38), (r * .62, h * .5), (r * .8, h * .62), (r * .93, h * .8), (r * .985, h * .94),
            (r, h), (r * .99, h * 1.06), (r * .955, h * 1.0), (r * .96, h * .9), (r * .9, h * .68), (r * .78, h * .42),
            (r * .62, h * .18), (r * .56, 0.0), (r * .48, 0.0), (r * .46, h * .1), (0.0, h * .12)]
    mats = [P.CHINA, P.CHINA, P.CHINA, P.CHINA, P.CHINA, P.GOLD, P.GOLD, P.GOLD, P.GOLD, P.CHINA, P.CHINA, P.CHINA, P.CHINA,
            P.CHINA, P.CHINA, P.CHINA]
    return k.lathe(prof, segs, mat=mats)


def plate_stack(r, h, n):
    """n plates stacked to h, r across (the sheet's stack), each nesting on the one under it; the top plate's rim at h."""
    bm = bmesh.new()
    each = h / (n + .06)
    for i in range(n):
        P.merge(bm, P.transform(stack_plate(r, each), Matrix.Translation((0, 0, each * i))))
    return bm


# ------------------------------------------------------------------ the kitchen doorway and the pantry frame

def kitchen(col):
    """The kitchen's west wall and roof (walnut panelling, a gilt cornice) and its doorway after BH_A03's third cell:
    walnut posts on gold bases with gold capitals, a frieze of cream marble between gold frames, a cornice; the
    sheet's half doors stand swung back inside the kitchen, along the posts (the waiters walk out between them)."""
    bm = bmesh.new()
    # west wall's hall face (x 25.7): two panels over the 4 m; the roof slab and its cornice
    P.merge(bm, P.box(U(25.66, 0, 0), U(26.0, 7.5, 4.0), P.WALNUT))
    m = basis(Vector((0, -1, 0)), Vector((-1, 0, 0)), U(25.66, 0, 2.0))
    P.merge(bm, plane_prism(rect(3.2, 5.2), .025, P.WALNUT, m @ Matrix.Translation((0, 1.0, 0))))
    P.merge(bm, plane_prism(arched(2.9, 4.9, .35), .06, P.WALNUT, m @ Matrix.Translation((0, 1.15, 0))))
    P.merge(bm, frame_bead(arched(2.9, 4.9, .35), m @ Matrix.Translation((0, 1.15, 0)), .022, P.GOLD, .065))
    P.merge(bm, P.box(U(25.6, 0, 0), U(25.7, .35, 4.0), P.WALNUT))
    P.merge(bm, P.box(U(25.58, .35, 0), U(25.7, .4, 4.0), P.GOLD))
    P.merge(bm, P.box(U(25.6, 7.5, 0), U(32.0, 7.8, 4.0), P.WALNUT))
    for (a, b) in ((U(25.55, 7.6, 0), U(25.55, 7.6, 4.06)), (U(25.55, 7.6, 4.06), U(32.0, 7.6, 4.06))):
        P.merge(bm, P.sweep([a, b], [(-.0, -.12), (.1, -.12), (.14, 0.0), (.1, .1), (0.0, .14)][::-1], P.GOLD))
    # the doorway (user 11-25 on the first stills: "문을 좀 더 두껍고 입체적으로"): deep posts (0.95 m, the depth
    # going back into the kitchen - the hall side stays where the trays and the counter need it), every face in deep
    # relief: a chamfered sunk frame round a raised, chamfered field, stepped gold bases and capitals, a frieze of marble
    # in deep gold frames, a cornice stepping out in three. Posts x 25.2 - 26.2 and 31.42 - 32.4 (the trays pass from
    # x 26.3), front face z 4.12, back z 3.17.
    zf, zb = 4.12, 3.17
    for (a0, a1) in ((25.2, 26.2), (31.42, 32.4)):
        cx = (a0 + a1) / 2
        w = a1 - a0
        # plinth block, a gold torus and fillet over it
        P.merge(bm, P.box(U(a0 - .1, 0, zb - .1), U(a1 + .1, .55, zf + .1), P.WALNUT))
        P.merge(bm, P.box(U(a0 - .07, .55, zb - .07), U(a1 + .07, .6, zf + .07), P.GOLD))
        P.merge(bm, P.sweep([U(a0 - .06, .66, zf + .06), U(a1 + .06, .66, zf + .06)], [(0.0, -.06), (.06, -.04), (.08, 0.0), (.06, .04), (0.0, .06)], P.GOLD))
        P.merge(bm, P.box(U(a0 - .04, .6, zb - .04), U(a1 + .04, .74, zf + .04), P.GOLD))
        # the shaft
        P.merge(bm, P.box(U(a0, .74, zb), U(a1, 6.05, zf), P.WALNUT))
        # its faces: the hall face (z+), the doorway face (toward the opening) and the outer face, each a deep panel
        faces = [(U(cx, 0, zf), Vector((0, 1, 0)), w)]
        inner = a1 if a0 < 28 else a0
        out_n = Vector((1, 0, 0)) if a0 < 28 else Vector((-1, 0, 0))
        faces.append((U(inner, 0, (zf + zb) / 2), out_n, zf - zb))
        for at, n, fw in faces:
            m = basis(None, n, at)
            P.merge(bm, sunk_panel(fw - .2, 4.75, m @ Matrix.Translation((0, 1.0, 0))))
        # capital: an astragal, a gold neck, a stepped gold block
        P.merge(bm, P.sweep([U(a0 - .03, 6.08, zf + .03), U(a1 + .03, 6.08, zf + .03)], [(0.0, -.04), (.04, -.02), (.05, 0.0), (.04, .02), (0.0, .04)], P.GOLD))
        P.merge(bm, P.box(U(a0 - .03, 6.05, zb - .03), U(a1 + .03, 6.15, zf + .03), P.GOLD))
        P.merge(bm, P.box(U(a0 - .08, 6.15, zb - .08), U(a1 + .08, 6.3, zf + .08), P.GOLD))
        P.merge(bm, P.box(U(a0 - .14, 6.3, zb - .14), U(a1 + .14, 6.45, zf + .14), P.GOLD))
        P.merge(bm, P.box(U(a0 - .1, 6.45, zb - .1), U(a1 + .1, 6.55, zf + .1), P.WALNUT))
    # the frieze over the opening: a walnut beam with three marble panels in deep gold frames, then the cornice
    P.merge(bm, P.box(U(25.1, 6.55, zb), U(32.4, 7.2, zf + .02), P.WALNUT))
    mm = basis(None, Vector((0, 1, 0)), U(28.8, 6.55, zf + .02))
    for cu, w in ((-3.05, .75), (0.0, 4.4), (3.05, .75)):
        P.merge(bm, P.transform(frustum(rect(w + .26, .58), .07, .06, P.GOLD), mm @ Matrix.Translation((cu, .035, 0))))
        P.merge(bm, P.transform(k.prism(rect(w, .44), 0.0, .03, mat=P.MARBLE), mm @ Matrix.Translation((cu, .105, .06))))
    P.merge(bm, P.box(U(24.95, 7.2, zb - .06), U(32.4, 7.28, zf + .1), P.GOLD))
    P.merge(bm, P.sweep([U(24.9, 7.34, zf + .14), U(32.4, 7.34, zf + .14)], [(0.0, -.06), (.06, -.04), (.09, 0.0), (.06, .05), (0.0, .06)], P.GOLD))
    P.merge(bm, P.box(U(24.88, 7.28, zb - .1), U(32.4, 7.4, zf + .12), P.WALNUT))
    P.merge(bm, P.box(U(24.8, 7.4, zb - .14), U(32.4, 7.5, zf + .22), P.GOLD))
    P.merge(bm, P.box(U(24.75, 7.5, zb - .18), U(32.4, 7.62, zf + .28), P.WALNUT))
    ob = P.obj(col, "BH1 kitchen", bm)
    return [P.finish(ob, bevel=.012)]


def pantry(col):
    """The pantry's doorway after BH_A03's fourth cell - a plain walnut frame, a moulded head, the door taken off its
    iron strap hinges (left on the posts, brass pins) - and its west wall over the table's east edge (walnut panelling
    facing the table) and its roof."""
    bm = bmesh.new()
    zf = 26.0
    # the east post on the floor, the west post standing on the table top (the pantry wall's foot)
    for (a0, a1, foot) in ((25.7, 26.1, 4.4), (31.3, 32.4, 0.0)):
        P.merge(bm, P.box(U(a0 - .05, foot, zf - .06), U(a1 + .05, foot + .4, zf + .36), P.WALNUT))
        P.merge(bm, P.box(U(a0, foot + .4, zf - .02), U(a1, 7.2, zf + .32), P.WALNUT))
        for yy in (foot + 1.0, 6.2):   # strap hinges on the post's opening side
            hx = a1 if a0 < 28 else a0
            sgn = 1 if a0 < 28 else -1
            if foot > 0 and yy < 5.0:
                yy = foot + .7
            # the strap: a shaped iron plate on the post's south face, a knuckle at the opening, gold rivets
            plate = [(0.0, 0.0), (-sgn * .32, -.06), (-sgn * .38, .1), (-sgn * .38, .5), (-sgn * .32, .66), (0.0, .6)]
            pl = k.prism([(u, v) for u, v in plate], 0.0, .03, mat=P.SILVER)
            P.merge(bm, P.transform(pl, Matrix.Translation(U(hx, yy, zf - .05)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
            kn = k.lathe([(0.0, 0.0), (.05, 0.0), (.05, .62), (0.0, .62)], 10, mat=P.SILVER)
            P.merge(bm, P.transform(kn, Matrix.Translation(U(hx, yy - .01, zf - .05))))
            for (ru, rv) in ((.12, .15), (.27, .15), (.12, .45), (.27, .45), (.2, .3)):
                pin = k.lathe([(0.0, 0.0), (.04, 0.0)] + [(.04 * math.cos(math.pi / 2 * i / 4), .04 * math.sin(math.pi / 2 * i / 4)) for i in range(1, 5)], 10, mat=P.GOLD)
                P.merge(bm, P.transform(pin, Matrix.Translation(U(hx - sgn * ru, yy + rv, zf - .08)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
    # the head: a beam and a stepped cornice, 7.2 - 7.8
    P.merge(bm, P.box(U(25.7, 7.2, zf - .05), U(32.4, 7.55, zf + .35), P.WALNUT))
    P.merge(bm, P.box(U(25.7, 7.55, zf - .12), U(32.4, 7.68, zf + .4), P.WALNUT))
    P.merge(bm, P.box(U(25.7, 7.68, zf - .2), U(32.4, 7.85, zf + .42), P.WALNUT))
    door = P.finish(P.obj(col, "BH1 pantry door", bm), bevel=.012)
    bm = bmesh.new()
    # the west wall over the table (x 25.7 - 26, y 4.4 - 7.8, z 26 - 56): panels facing the table, a gold cornice
    P.merge(bm, P.box(U(25.66, 4.4, zf + .36), U(26.0, 7.5, 56.0), P.WALNUT))
    n = 10
    for i in range(n):
        za = zf + .36 + (56.0 - zf - .36) * (i + .5) / n
        m = basis(Vector((0, -1, 0)), Vector((-1, 0, 0)), U(25.66, 4.4, za))
        w = (56.0 - zf - .36) / n - .4
        P.merge(bm, plane_prism(rect(w, 2.3), .02, P.WALNUT, m @ Matrix.Translation((0, .4, 0))))
        P.merge(bm, plane_prism(rect(w - .24, 2.06), .055, P.WALNUT, m @ Matrix.Translation((0, .52, 0))))
        P.merge(bm, frame_bead(rect(w, 2.3), m @ Matrix.Translation((0, .4, 0)), .018, P.GOLD, .025))
    P.merge(bm, P.box(U(25.55, 4.4, zf + .36), U(25.66, 4.6, 56.0), P.WALNUT))
    P.merge(bm, P.box(U(25.6, 7.5, zf - .2), U(32.0, 7.8, 56.0), P.WALNUT))
    P.merge(bm, P.sweep([U(25.55, 7.62, zf - .2), U(25.55, 7.62, 56.0)], [(0.0, .14), (.1, .1), (.14, 0.0), (.1, -.12), (0.0, -.12)], P.GOLD))
    wall = P.finish(P.obj(col, "BH1 pantry wall", bm), bevel=.012)
    return [door, wall]


# The half doors (user 11-25: "열렸다 닫히게"): each leaf its own model, built round its hinge (Blender: the leaf along
# +x from the hinge - -x for the east one - its foot at z 0, its thickness across y), hung by
# GrandBanquetHallDressing.Servery on a pivot at the hinge that swings it out into the hall as each tray comes
# (KitchenDoors). Hinges (Unity): west (26.11, 1.6, 3.645), east (31.51, 1.6, 3.645) - in the posts' doorway faces, so a
# leaf swung open lies along the post (x <= 26.2, x >= 31.42), off the trays' way (x >= 26.3) and the waiters'.
LEAF_HINGES = {"W": (26.11, 1.6, 3.645), "E": (31.51, 1.6, 3.645)}
LEAF_W, LEAF_H, LEAF_T = 2.48, 2.9, .18


def kitchen_leaf(col, side):
    """A thick saloon leaf (BH_A03): 0.18 m, a raised chamfered field on both faces in a gold bead, the top rising in a
    curve from the hinge to the middle under a rolled gold rail, two gold barrel hinges on its hinge edge."""
    s_ = 1 if side == "W" else -1
    lw, lh, t = LEAF_W, LEAF_H, LEAF_T
    bm = bmesh.new()
    def smooth(x):
        return x * x * (3 - 2 * x)
    top = [(lw * i / 16, lh - .55 + .55 * smooth(i / 16)) for i in range(17)]    # u from the hinge (0) to the middle (lw)
    outline = [(0.0, 0.0), (lw, 0.0)] + list(reversed(top))
    P.merge(bm, k.prism(outline, -t / 2, t / 2, mat=P.WALNUT))
    for face in (-1, 1):
        fm = Matrix.Translation((lw / 2, .32, face * t / 2))
        if face < 0:
            fm = fm @ Matrix.Rotation(math.pi, 4, "Y")
        P.merge(bm, P.transform(frustum(rect(lw - .5, lh - 1.25), .09, .06, P.WALNUT), fm))
        P.merge(bm, P.transform(frame_ring(rect(lw - .3, lh - 1.05), .03), fm @ Matrix.Translation((0, -.1, .0))))
    P.merge(bm, P.tube([Vector((u, v + .03, 0)) for u, v in top], .1, 10, mat=P.GOLD))
    for zz in (.4, 1.95):
        P.merge(bm, P.transform(k.lathe([(0.0, 0.0), (.08, 0.0), (.08, .44), (.055, .48), (0.0, .5)], 14, mat=P.GOLD),
                                Matrix.Translation((-.02, zz, 0)) @ Matrix.Rotation(-math.pi / 2, 4, "X")))
    # the leaf so far: x along, y up, z thickness -> Blender: x along (mirrored for the east), y thickness, z up
    turn = Matrix(((s_, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    P.transform(bm, turn)
    if s_ > 0:   # the west turn swaps two axes: a mirror, so the faces turn inside out
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    ob = P.obj(col, "BH1 kitchen leaf " + side, bm)
    return P.finish(ob, bevel=.01)


def leaf_pose(side, open_deg):
    """Blender matrix of a leaf at its hinge, swung `open_deg` out into the hall (north)."""
    x, y, z = LEAF_HINGES[side]
    turn = math.radians(open_deg) * (1 if side == "W" else -1)
    return Matrix.Translation(U(x, y, z)) @ Matrix.Rotation(turn, 4, "Z")


# ------------------------------------------------------------------ the waiter (BH_A11 first cell) and his tray

WAITER_TOP, WAITER_TURN = 7.05, -160.0


def waiter(col):
    """In the waiter root's frame (Blender: the tray's middle at the origin, the body at x +2, walking +y): the user's
    figurine (bh_figures "waiter", BH_A11's black bishop in a tailcoat) scaled so his mitre's ball is 7.05 m up (under
    the kitchen's lintel, 7.2), turned 20 degrees from his walk toward the tray - no more, or the arm he holds out would
    reach over the tray, where pawns stand. The silver tray (top 2.4, a gold band round its edge, nothing raised over
    its top) and the plate stack on it."""
    import bh_figures as F
    importlib.reload(F)
    objs = []
    fig = F.figure("waiter", col, "BH1 waiter")
    h = F.size(fig)[2]
    fig.matrix_world = Matrix.Translation((BODY_X, 0, 0)) @ Matrix.Rotation(math.radians(WAITER_TURN), 4, "Z") @ Matrix.Scale(WAITER_TOP / h, 4)
    objs.append(fig)
    # the tray: silver, top at 2.4, a gold band round its edge (flush with the top), a foot ring under
    tray = k.lathe([(0.0, TRAY_Y), (TRAY_R - .06, TRAY_Y), (TRAY_R, TRAY_Y - .01), (TRAY_R + .005, TRAY_Y - .06),
                    (TRAY_R, TRAY_Y - .11), (TRAY_R - .05, TRAY_Y - .15), (TRAY_R * .55, TRAY_Y - .17), (TRAY_R * .5, TRAY_Y - .24),
                    (TRAY_R * .45, TRAY_Y - .24), (0.0, TRAY_Y - .2)], 48, mat=[P.SILVER, P.GOLD, P.GOLD, P.GOLD, P.GOLD, P.SILVER, P.SILVER, P.SILVER, P.SILVER])
    objs.append(P.finish(P.obj(col, "BH1 tray", tray)))
    stack = plate_stack(STACK_R, STACK_TOP - TRAY_Y, 10)
    objs.append(P.finish(P.obj(col, "BH1 plate stack", stack, Matrix.Translation((0, 0, TRAY_Y)))))
    return objs


# ------------------------------------------------------------------ main

def build():
    scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o)
    got = {}
    got["screen"] = screens(collection("BH1 screens"))
    got["counter"] = counter(collection("BH1 counter"))
    got["kitchen"] = kitchen(collection("BH1 kitchen"))
    leaves = collection("BH1 kitchen leaves")
    got["leaves"] = [kitchen_leaf(leaves, "W"), kitchen_leaf(leaves, "E")]
    for o, side in zip(got["leaves"], "WE"):
        o.matrix_world = leaf_pose(side, 25.0)   # for the stills: a little open, as the sheet
    got["pantry"] = pantry(collection("BH1 pantry"))
    got["waiter"] = waiter(collection("BH1 waiter"))
    k.preview_materials()
    print("[bh_b1] waiter figure: scale %.2f, foot %.2f m across" % (WAITER_TOP / 0.443, 0.161 * WAITER_TOP / 0.443))
    print("[bh_b1] tris", {n: k.tris(o) for n, o in got.items()})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b1_servery.blend"))
    return got


def stills(got):
    scene = bpy.context.scene
    out = os.path.join(HERE, "stills", "b1", "parts")
    os.makedirs(out, exist_ok=True)
    cam = st.setup_render(scene)
    by = {o.name: o for o in scene.objects}
    # the screen: one module of the south run, from the hall side; the stack: the waiter's; the others whole
    jobs = (("screen", [by["BH1 passage end"]], math.radians(-30), math.radians(10), 50),
            ("counter", [by["BH1 counter"], by["BH1 counter ramp"]] + [by["BH1 spare plates %d" % i] for i in (1, 2, 3)],
             math.radians(-130), math.radians(22), 45),
            ("kitchen", got["kitchen"] + got["leaves"], math.radians(170), math.radians(6), 40),
            ("pantry", [by["BH1 pantry door"]], math.radians(-15), math.radians(6), 40),
            ("stack", [by["BH1 plate stack"]], math.radians(-20), math.radians(22), 70),
            ("waiter", [by["BH1 waiter"]], math.radians(200), math.radians(6), 70))
    for name, objs, yaw, pitch, lens in jobs:
        st.frame(cam, objs, yaw, pitch, lens)
        path = os.path.join(out, name + "_render.png")
        st.render_only(scene, objs, path)
        sheet, box = CELLS[name]
        st.compare(sheet, box, path, os.path.join(out, name + "_compare.png"))
    # the servery together: the waiter copied to two places on his lane, seen from the hall's middle
    alls = [o for o in scene.objects if o.type == "MESH" and o not in got["waiter"]]
    focus = got["counter"] + got["kitchen"] + [by["BH1 pantry door"]]
    for z in (6.0, 13.25):
        for o in got["waiter"]:
            c = o.copy()
            c.matrix_world = Matrix.Translation(U(27.8, 0, z)) @ o.matrix_world
            scene.collection.objects.link(c)
            alls.append(c)
            focus.append(c)
    st.frame(cam, focus, math.radians(-140), math.radians(26), 30, 1.05)
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    st.render_only(scene, alls, os.path.join(HERE, "stills", "b1", "servery.png"))
    scene.render.resolution_x, scene.render.resolution_y = 900, 900
    # the waiter with his tray and stack, as he walks
    w = got["waiter"]
    st.frame(cam, w, math.radians(150), math.radians(18), 45)
    st.render_only(scene, w, os.path.join(HERE, "stills", "b1", "waiter_whole.png"))


def export(got):
    """B1 to Unity (GrandBanquetHallDressing.Servery.cs): the static pieces in the section's frame, the waiter in his
    root's frame (he was built there), each kitchen leaf round its own hinge (Servery makes the pivots)."""
    import bh_figures as F
    k.export("BH1 screens", "bh_screens", "B1")
    k.export("BH1 counter", "bh_counter", "B1")
    k.export("BH1 kitchen", "bh_kitchen", "B1")
    k.export("BH1 pantry", "bh_pantry", "B1")
    k.export("BH1 waiter", "bh_waiter", "B1")
    for o, side in zip(got["leaves"], "WE"):
        c = bpy.data.collections.new("BH1 leaf " + side)
        bpy.context.scene.collection.children.link(c)
        c.objects.link(o)
        o.matrix_world = Matrix.Identity(4)
        k.export(c.name, "bh_kitchen_leaf_" + side, "B1")
    F.unity_materials()


if __name__ == "__main__":
    got = build()
    if "stills" in ARGS:
        stills(got)
    if "export" in ARGS:
        export(got)
