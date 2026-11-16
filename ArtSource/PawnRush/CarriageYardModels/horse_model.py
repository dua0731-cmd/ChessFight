# Royal Carriage Yard horse (R678, 승규 11-15 "백마 흑마 갈색 말", "아주 디테일 하게", then with a picture of two
# toy-like royal horses: "이런식으로 좀 두툼하게 만들어주고 말굽, 장식 등등 표현해줘"): one chunky horse, round and
# solid like a carved toy, with big hooves, a bridle with gold rosettes, a cream plume, and either a carriage harness
# (breast collar, back pad with rein rings, breeching) or a royal saddle; skinned to an armature whose joints
# YardHorse's pivots drive (YardHorseRig.cs) and coloured in Unity per coat (bay, grey = white, black) by slot.
#
#   exec(rcy_kit.py); exec(export_rcy.py); exec(horse_model.py); build_horse(); export_horse()
#
# Frame: YardHorse's root (Blender x, y, z = Unity x, z, y): nose to +y, standing on z 0, withers 1.55 m, YardHorse
# Length x Width x Height = 2.6 x 0.9 x 1.95.
#
# How it is made, like clay: the trunk, neck and head are lofted between a top line and an under line, the muscles
# (shoulder, breast, haunch, thigh, gaskin, jowl) are metaball masses, the legs are lofted from sections (forearm,
# knee, cannon, fetlock, pastern); everything is fused into one skin through signed distance grids, the creases
# filled round, then reduced. Hooves, eyes, mane, forelock and tail join that skin as one mesh, "Horse" (slots coat,
# hoof, eye, hair); the tack is three more meshes on the same bones: "Tack" (bridle, plume, girth, breast collar: every
# horse), "Harness" (a carriage horse) and "Saddle" (a ridden one).

import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Quaternion

HORSE_RNG = random.Random(1115)
MB_K = .575                      # a metaball's visible radius per unit radius (stiffness 2, threshold .6)

COAT, HOOF, EYE, HAIR = "RCY | coat", "RCY | hoof", "RCY | eye", "RCY | hair"
LEATHER, GOLD, PLUME, CLOTH = "RCY | leather", "RCY | gold", "RCY | plume", "RCY | cloth"
HORSE_SLOTS = (COAT, HOOF, EYE, HAIR)                 # the slot order Unity paints by (RoyalCarriageYard.Dressing.cs)
TACK_SLOTS = (LEATHER, GOLD, PLUME, CLOTH)
FORE_Y, HIND_Y, LEG_X = .52, -.8, .2     # where the hooves stand


# ------------------------------------------------------------------ metaball masses

def mb_ball(mb, c, r, neg=False):
    e = mb.elements.new(type="BALL")
    e.co = Vector(c); e.radius = r / MB_K; e.stiffness = 2.0; e.use_negative = neg
    return e


def mb_ell(mb, c, semi, rot=(0, 0, 0), neg=False):
    """An ellipsoid with visible semi-axes `semi` (x, y, z), turned by Euler degrees `rot`."""
    e = mb.elements.new(type="ELLIPSOID")
    e.co = Vector(c); e.radius = 1 / MB_K; e.stiffness = 2.0; e.use_negative = neg
    e.size_x, e.size_y, e.size_z = semi
    e.rotation = (Matrix.Rotation(math.radians(rot[2]), 3, "Z") @ Matrix.Rotation(math.radians(rot[1]), 3, "Y")
                  @ Matrix.Rotation(math.radians(rot[0]), 3, "X")).to_quaternion()
    return e


def mb_cap(mb, a, b, r):
    """A capsule of visible radius r from a to b."""
    a, b = Vector(a), Vector(b)
    e = mb.elements.new(type="CAPSULE")
    e.co = (a + b) / 2; e.radius = r / MB_K; e.stiffness = 2.0
    e.size_x = (b - a).length / 2
    e.rotation = Vector((1, 0, 0)).rotation_difference((b - a).normalized())
    return e


# ------------------------------------------------------------------ the outline: trunk, neck and head lofted between
# a top line and an under line (a horse drawn from the side), each section an ellipse a little squared

# Trunk, tail to chest: (y, top z, under z, half width).
TRUNK_LINES = [
    (-1.02, 1.3, 1.06, .12), (-.98, 1.4, .97, .22), (-.92, 1.48, .9, .28), (-.84, 1.54, .86, .315), (-.72, 1.565, .84, .335),
    (-.58, 1.565, .81, .34), (-.42, 1.54, .78, .348), (-.24, 1.51, .76, .352), (-.05, 1.49, .75, .352), (.14, 1.5, .75, .345),
    (.32, 1.54, .77, .33), (.46, 1.575, .8, .31), (.58, 1.56, .85, .29), (.68, 1.47, .9, .265), (.76, 1.34, .95, .225),
    (.81, 1.2, 1.0, .15)]
# Neck, withers to throatlatch: (crest y, z), (throat y, z), half width. Thick and arched.
NECK_LINES = [
    ((.34, 1.5), (.8, .97), .255), ((.46, 1.6), (.87, 1.07), .245), ((.58, 1.69), (.93, 1.21), .222),
    ((.7, 1.78), (.98, 1.36), .198), ((.81, 1.86), (1.02, 1.49), .178), ((.9, 1.915), (1.05, 1.6), .16),
    ((.97, 1.945), (1.07, 1.69), .148)]
# Head along its axis from the poll to the muzzle: (share of the axis, face side, jaw side, half width), the sides as
# offsets across the axis. Broad, with round cheeks and a big soft muzzle.
HEAD_P0, HEAD_P1 = Vector((0, .99, 1.88)), Vector((0, 1.36, 1.385))
HEAD_LINES = [
    (0, .078, .1, .14), (.1, .105, .157, .159), (.2, .116, .213, .168), (.32, .112, .24, .159), (.45, .104, .185, .137),
    (.6, .1, .137, .121), (.74, .1, .125, .119), (.86, .107, .128, .123), (.94, .096, .112, .114), (1.0, .056, .063, .074)]

# R683 (승규 11-16, a picture of two chunky toy horses: "말은 이런식으로 다듬고"): a bigger, rounder head, a thicker neck
# and barrel round the same bones (the joints stay where YardHorse's pivots are). The head grows across (HEAD_W) more
# than along its axis, the muzzle most; the eyes, nostrils and the bone over the eye move out with it.
HEAD_W, HEAD_D, NECK_W, TRUNK_W = 1.2, 1.07, 1.12, 1.05
TRUNK_LINES = [(y, t, u, w * TRUNK_W) for y, t, u, w in TRUNK_LINES]
NECK_LINES = [(c, th, w * NECK_W) for c, th, w in NECK_LINES]
HEAD_LINES = [(s, f * HEAD_D, j * HEAD_D, w * HEAD_W * (1 + .08 * max(0.0, s - .6) / .4)) for s, f, j, w in HEAD_LINES]


def resample(rows, steps=4):
    """Catmull-Rom through rows of numbers or Vectors (each row a tuple), `steps` rows per span: smooth profiles."""
    def lerp_row(a, b, t):
        return tuple(x + (y - x) * t for x, y in zip(a, b))

    def cr(p0, p1, p2, p3, t):
        t2, t3 = t * t, t * t * t
        return tuple(.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
                     for a, b, c, d in zip(p0, p1, p2, p3))
    flat = [tuple(v for item in row for v in (item if isinstance(item, (tuple, list, Vector)) else (item,))) for row in rows]
    out = []
    for i in range(len(flat) - 1):
        p0 = flat[max(0, i - 1)]; p1 = flat[i]; p2 = flat[i + 1]; p3 = flat[min(len(flat) - 1, i + 2)]
        for k in range(steps):
            out.append(cr(p0, p1, p2, p3, k / steps))
    out.append(flat[-1])
    return out


def lines_loft(col, name, sections, seg=72, ex=2.3, steps=6):
    """Lofts closed rings, each from a top point, an under point and a half width (x), through a smooth spline of the
    given sections; caps both ends."""
    rows = resample([(tuple(t), tuple(u), w) for t, u, w in sections], steps)
    sections = [((r[0], r[1], r[2]), (r[3], r[4], r[5]), r[6]) for r in rows]
    bm = bmesh.new()
    rings = []
    for top, under, hw in sections:
        top, under = Vector(top), Vector(under)
        c = (top + under) / 2
        up = (top - under) / 2
        ring = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            cs, sn = math.cos(th), math.sin(th)
            px = math.copysign(abs(cs) ** (2 / ex), cs) * hw
            pv = math.copysign(abs(sn) ** (2 / ex), sn)
            ring.append(bm.verts.new(c + Vector((px, 0, 0)) + up * pv))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_object(name, bm, col, (COAT,))


def trunk(col):
    return lines_loft(col, "Trunk", [((0, y, t), (0, y, u), w) for y, t, u, w in TRUNK_LINES], ex=2.4)


def neck(col):
    return lines_loft(col, "Neck", [((0, ty, tz), (0, by, bz), w) for (ty, tz), (by, bz), w in NECK_LINES], ex=2.2)


def head(col):
    axis = HEAD_P1 - HEAD_P0
    n = Vector((0, -axis.z, axis.y)).normalized()        # across the axis, toward the face (forward and up)
    secs = []
    for s, front, jaw, w in HEAD_LINES:
        p = HEAD_P0 + axis * s
        secs.append((p + n * front, p - n * jaw, w))
    return lines_loft(col, "Head", secs, ex=2.2)


# ------------------------------------------------------------------ muscle masses (metaballs: smooth unions)

def muscle_masses(mb):
    for s in (-1, 1):
        mb_ell(mb, (s * .2, .58, 1.28), (.09, .1, .26), (35, 0, 0))   # the shoulder, the blade sloping back
        # the hindquarters: the round of the haunch, the thigh forward to the stifle, the gaskin behind
        mb_ell(mb, (s * .15, -.68, 1.22), (.16, .21, .23))
        mb_ell(mb, (s * .21, -.6, 1.0), (.11, .17, .18), (20, 0, 0))
        mb_ell(mb, (s * .21, -.78, .9), (.08, .11, .14), (-10, 0, 0))
        mb_ell(mb, (s * .27, -.47, .99), (.055, .055, .06))        # the stifle
        # the head: the round of the jowl, the bone over the eye
        mb_ell(mb, (s * .1 * HEAD_W, 1.08, 1.69), (.085, .125, .125), (-35, 0, 0))
        mb_ball(mb, (s * .125 * HEAD_W, 1.08, 1.81), .036)
        # R683: the toy horse's chest and forearm muscle, round under the breast collar
        mb_ell(mb, (s * .14, .7, 1.12), (.1, .09, .12), (15, 0, 0))
    mb_ell(mb, (0, -.93, 1.4), (.07, .07, .06))                   # the dock


def masses(col):
    mb = bpy.data.metaballs.new("RCY horse masses")
    mb.resolution = .014
    mb.render_resolution = .014
    mb.threshold = .6
    muscle_masses(mb)
    ob = bpy.data.objects.new("Horse masses", mb)
    col.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.metaballs.remove(mb)
    out = bpy.data.objects.new("Horse muscles", me)
    col.objects.link(out)
    return out


# ------------------------------------------------------------------ legs

# Sections from the top (buried in the trunk) down to the coronet: (z, y offset from the hoof's line, half width,
# depth in front, depth behind). The cannon is narrow from the side with its tendon standing out behind; the knee and
# the fetlock are knobs.
FORE_SECTIONS = [
    (1.18, .04, .1, .1, .1), (1.08, .035, .13, .14, .15), (1.0, .03, .135, .15, .16), (.92, .02, .128, .138, .148),
    (.8, .01, .115, .12, .128), (.68, 0, .1, .102, .11), (.58, 0, .09, .092, .096), (.52, .002, .094, .098, .096),
    (.46, .004, .09, .092, .092), (.4, 0, .084, .084, .09), (.33, 0, .082, .082, .09), (.27, .004, .088, .088, .094),
    (.23, .01, .09, .09, .092), (.19, .025, .086, .086, .086), (.145, .045, .084, .084, .084)]
HIND_SECTIONS = [
    (1.24, .22, .11, .12, .12), (1.14, .22, .15, .17, .18), (1.04, .19, .155, .18, .19), (.94, .14, .14, .15, .17),
    (.84, .08, .12, .12, .15), (.74, .03, .105, .1, .135), (.66, -.01, .094, .09, .115), (.6, -.02, .094, .095, .12),
    (.55, -.022, .094, .096, .114), (.5, -.02, .086, .088, .096), (.43, -.015, .082, .082, .094), (.35, -.01, .082, .082, .094),
    (.28, 0, .086, .086, .098), (.23, .01, .09, .09, .094), (.19, .025, .086, .086, .086), (.145, .045, .084, .084, .084)]
# R683 (승규's toy horses): thick, sturdy legs - 30% wider and deeper all the way down, a little more at the knee /
# hock and the fetlock knobs so they keep their volume when the joint folds (the knee at 105 degrees lost a third).
LEG_THICK = 1.3


def _thicken(sections, knobs):
    out = []
    for z, dy, hw, f, b in sections:
        k = LEG_THICK * (1 + .06 * max(0.0, 1 - min(abs(z - kz) for kz in knobs) / .07))
        out.append((z, dy, hw * k, f * k, b * k))
    return out


FORE_SECTIONS = _thicken(FORE_SECTIONS, (.5, .22))
HIND_SECTIONS = _thicken(HIND_SECTIONS, (.57, .22))


def leg_loft(col, name, x, y0, sections, seg=24):
    bm = bmesh.new()
    rings = []
    for z, dy, hw, front, back in resample(sections, 3):
        ring = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            c, s = math.cos(th), math.sin(th)
            depth = front if s > 0 else back
            ex = 2 / 2.4                           # a little squarer than an ellipse
            px = math.copysign(abs(c) ** ex, c) * hw
            py = math.copysign(abs(s) ** ex, s) * depth
            ring.append(bm.verts.new((x + px, y0 + dy + py, z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_object(name, bm, col, (COAT,))


def hock_point(col, x):
    """The point of the hock, standing out behind the joint."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=1)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((x, HIND_Y - .135, .6)) @ Matrix.Diagonal((.048, .05, .062, 1)), verts=bm.verts)
    return mesh_object("Hock point", bm, col, (COAT,))


def hoof(col, x, y0):
    """A big round hoof: the wall flaring a little to the ground and forward to the toe, a flat sole."""
    bm = bmesh.new()
    seg = 28
    rows = []
    # R683: the toy horses' big bell hooves - wider (r .082 -> .112), taller, with a rolled rim at the coronet.
    for z, grow, toe in ((.182, -.006, 0), (.176, .004, .003), (.162, .008, .008), (.12, .016, .018), (.072, .026, .028),
                         (.03, .036, .038), (.008, .035, .038), (0, .027, .035)):
        ring = []
        for k in range(seg):
            th = 2 * math.pi * k / seg
            c, s = math.cos(th), math.sin(th)
            r = .112 + grow
            front = s > 0
            px = c * (r + (.004 if front else 0))
            py = s * (r + (toe * s if front else -.004))
            ring.append(bm.verts.new((x + px, y0 + .045 + toe * .35 + py, z)))
        rows.append(ring)
    for a, b in zip(rows, rows[1:]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bm.faces.new(list(reversed(rows[0])))
    bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object("Hoof", bm, col, (HOOF,))
    bevel_mod(ob, .006, 2)
    return ob


# ------------------------------------------------------------------ the head's details: ears, eyes, nostrils, mouth

def ellipsoid(col, name, centre, semi, rot=(0, 0, 0), mat=COAT, seg=20):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(8, seg // 2), radius=1)
    r = (Matrix.Rotation(math.radians(rot[2]), 4, "Z") @ Matrix.Rotation(math.radians(rot[1]), 4, "Y")
         @ Matrix.Rotation(math.radians(rot[0]), 4, "X"))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(centre) @ r @ Matrix.Diagonal((semi[0], semi[1], semi[2], 1)), verts=bm.verts)
    return mesh_object(name, bm, col, (mat,))


HEAD_TILT = math.degrees(math.atan2((HEAD_P1 - HEAD_P0).z, (HEAD_P1 - HEAD_P0).y))   # about -53: the face's slope
EAR_BASE = (.08, .995, 1.93)
EYE_AT, EYE_R = (.15 * HEAD_W, 1.07, 1.79), .039


def ear(col, s):
    """An ear: a leaf lofted up and a little forward and out from the poll."""
    base = Vector((s * EAR_BASE[0], EAR_BASE[1], EAR_BASE[2]))
    axis = Vector((s * .16, .22, 1)).normalized()
    side = axis.cross(Vector((0, 1, 0))).normalized()
    fwd = side.cross(axis).normalized()
    bm = bmesh.new()
    seg, rings = 14, []
    for t, w, d in ((0, .042, .034), (.25, .047, .036), (.55, .04, .029), (.8, .025, .018), (.93, .012, .009), (1.0, .003, .003)):
        c = base + axis * (t * .17)
        ring = [bm.verts.new(c + side * (math.cos(2 * math.pi * k / seg) * w) + fwd * (math.sin(2 * math.pi * k / seg) * d))
                for k in range(seg)]
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    cup = ellipsoid(col, "Ear cup", base + axis * .085 + fwd * .029, (.025, .015, .062),
                    (math.degrees(math.atan2(axis.y, axis.z)) * -1, 0, 0))
    return mesh_object("Ear", bm, col, (COAT,)), cup


def head_carvers(col):
    """What is cut out of the head: the nostrils, the mouth's line, the eyes' sockets."""
    cuts = []
    for s in (-1, 1):
        cuts.append(ellipsoid(col, "Nostril", (s * .068 * HEAD_W, 1.4, 1.462), (.022, .025, .034), (HEAD_TILT + 90, 0, s * 12)))
        cuts.append(ellipsoid(col, "Eye socket", (s * EYE_AT[0], EYE_AT[1], EYE_AT[2]), (.033, .041, .036)))
    cuts.append(ellipsoid(col, "Mouth", (0, 1.315, 1.362), (.11, .07, .008), (HEAD_TILT, 0, 0)))
    return cuts


def eyes(col):
    out = []
    for s in (-1, 1):
        e = ellipsoid(col, "Eye", (s * (EYE_AT[0] - .006), EYE_AT[1], EYE_AT[2]), (EYE_R * .8, EYE_R * 1.15, EYE_R), mat=EYE, seg=24)
        for f in e.data.polygons:      # R683: smooth (it read as a faceted bead up close)
            f.use_smooth = True
        out.append(e)
    return out


# ------------------------------------------------------------------ hair: the mane, the forelock, the tail

def strand(bm, pts, width, thick, along_hint, sides=8, tip=.3):
    """A lock of hair through `pts`: a flat ribbon with some body tapering to `tip` of its size, `width` and `thick`
    its half sizes, the width across the lock roughly along `along_hint`. R683: eight sides and smooth shading (the
    six-sided locks read as faceted sticks up close)."""
    first = len(bm.faces)
    _strand(bm, pts, width, thick, along_hint, sides, tip)
    bm.faces.ensure_lookup_table()
    for f in bm.faces[first:]:
        f.smooth = True


def _strand(bm, pts, width, thick, along_hint, sides, tip):
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        t = i / (n - 1)
        d = (pts[min(n - 1, i + 1)] - pts[max(0, i - 1)]).normalized()
        a = (along_hint - d * along_hint.dot(d)).normalized()
        b = d.cross(a).normalized()
        taper = (1 - t) ** .7 * (1 - tip) + tip
        w, h = width * taper, thick * taper
        ring = [bm.verts.new(p + a * (math.cos(2 * math.pi * k / sides) * w) + b * (math.sin(2 * math.pi * k / sides) * h))
                for k in range(sides)]
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]) * .5)
    for k in range(sides):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], tip))


def mane(col):
    """R683 (승규 11-16 "머리 꼬리 흩날리게", with his picture of two toy horses): a thick mane blowing back in the wind.
    Along the crest from the poll to the withers: a row of short locks standing up and sweeping back over the crest
    (the mane's volume on top), and two layers of long chunky locks falling to the horse's right (+x), lying on the
    neck at their roots, then streaming back toward the withers with an S-wave and their tips flicking out and up.
    The forelock blows back from between the ears and flips to the side over the forehead."""
    rng = random.Random(7)
    bm = bmesh.new()
    rows = resample([(tuple(t), tuple(u), w) for t, u, w in
                     [((0, ty, tz), (0, by, bz), w) for (ty, tz), (by, bz), w in NECK_LINES]], 2)
    up = Vector((0, 0, 1))
    for k in range(len(rows) - 1, 1, -1):
        r = rows[k]
        top = Vector(r[0:3])
        along = (Vector(rows[k - 1][0:3]) - top).normalized()      # down the crest toward the withers
        share = k / (len(rows) - 1)
        # the crest: short locks rising off the top and sweeping back, leaning a little to the right
        length = (.19 + .07 * (1 - abs(share - .5) * 2)) * (1 + rng.uniform(-.15, .15))
        lean = (.07, -.035)[k % 2]                                 # alternate locks fall right and left
        pts = [top + up * (.015 + .1 * math.sin(t * math.pi * .6) * length / .22) + along * (length * t * .95)
               + Vector((.008 * (1 if lean > 0 else -1) + lean * t * t, 0, 0)) for t in (j / 9 for j in range(10))]
        strand(bm, pts, .085, .04, along, tip=.3)
        for layer in range(2):
            r = rows[k]
            top, under, w = Vector(r[0:3]), Vector(r[3:6]), r[6]
            depth = (under - top).length
            down = (under - top).normalized()
            length = (.36 + .14 * (1 - abs(share - .55) * 1.6)) * (1 + rng.uniform(-.15, .15)) * (1.0, .72)[layer]
            shift = along * (.04 * layer)           # the outer layer between the inner one's locks
            wave = rng.uniform(0, 2 * math.pi)
            pts = []
            for j in range(14):
                t = j / 13
                d = length * t
                # lying on the neck's side (the section is a squared ellipse), standing off it by the lock's body,
                # then streaming back down the neck, waving, the tip blown out and up
                v = max(-1.0, 1 - 2 * d / depth)
                x = w * max(0.0, 1 - abs(v) ** 2.2) ** (1 / 2.2)
                x = max(x, .015) + .026 + .028 * layer + .15 * t ** 2.2 + .018 * math.sin(wave + t * 2 * math.pi) * t
                p = (top + shift + Vector((x, 0, 0)) + down * (d * (1 - .35 * t * t)) + along * (.2 * t ** 1.6)
                     + up * (.07 * t ** 3) + down * .006)
                pts.append(p)
            strand(bm, pts, .1, .034, along, tip=.3)
    # The forelock: from between the ears, forward and down over the forehead, blown to the right and flipping up.
    axis = (HEAD_P1 - HEAD_P0).normalized()
    n = Vector((0, -axis.z, axis.y)).normalized()
    for k in range(6):
        x = (k - 2.5) * .022
        start = HEAD_P0 + n * .085 + Vector((x, -.01, .035))
        pts = [start + axis * (.2 * t) + n * (.03 * math.sin(t * math.pi) + .025 * t ** 3)
               + Vector((x * .5 * t + .06 * t * t, 0, 0)) for t in (j / 10 for j in range(11))]
        strand(bm, pts, .032, .014, Vector((1, 0, 0)))
    return mesh_object("Mane", bm, col, (HAIR,))


TAIL_DOCK = [Vector((0, -.915, 1.42)), Vector((0, -.97, 1.35)), Vector((0, -1.0, 1.24)), Vector((0, -1.01, 1.12))]
# A team's swingle-tree crosses behind each horse at its y -1.335, z .95 (movers_model.hitch): the tail stays 9 cm in
# front of it there, and in front of the coach's footboard below.
TAIL_CLEAR = ((.8, 1.12, -1.24), (-1.0, 9.0, -1.37))


def tail(col):
    """R683 (승규 "꼬리 흩날리게"): the dock, and a big full tail of chunky wavy locks blowing out behind: from the dock
    they arc back, fall to below the hocks, drift to the horse's right with the mane and fan out, and their ends
    sweep back and curl up."""
    rng = random.Random(11)
    bm = bmesh.new()
    strand(bm, [p.copy() for p in TAIL_DOCK], .07, .065, Vector((1, 0, 0)))
    count = 30
    for k in range(count):
        u = min(1.0, max(0.0, k / (count - 1) + rng.uniform(-.04, .04)))
        root = TAIL_DOCK[0].lerp(TAIL_DOCK[-1], u * .85) + Vector((rng.uniform(-.04, .04), rng.uniform(-.03, .012), 0))
        bottom_z = .46 + rng.uniform(-.07, .07)
        spread = Vector((rng.uniform(-.17, .17), rng.uniform(-.1, .02), 0))
        arc = .1 + .1 * (1 - u)              # the hair from the top of the dock arcs out behind before it falls
        wave = rng.uniform(0, 2 * math.pi)
        pts = []
        for j in range(14):
            t = j / 13
            flick = max(0.0, (t - .55) / .45) ** 2  # the end blown back and curling up
            z = root.z + (bottom_z - root.z) * t + .13 * flick
            p = Vector((root.x, root.y - arc * math.sin(t * math.pi * .8) - .02 * t - .14 * flick, z)) + spread * (t ** 1.4)
            p.x += .04 * math.sin(t * 4.5 + wave) * t + .09 * t * t
            for z0, z1, ymin in TAIL_CLEAR:
                if z0 <= p.z <= z1:
                    p.y = max(p.y, ymin)
                    break
            pts.append(p)
        strand(bm, pts, .085, .036, Vector((1, 0, 0)) if k % 2 else Vector((0, 1, 0)), tip=.25)
    return mesh_object("Tail", bm, col, (HAIR,))


# ------------------------------------------------------------------ fusing

def apply_all(ob):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def sdf_fuse(col, parts, carve=(), voxel=.007, fillet=8, mean=1, keep=13000):
    """Joins the clay pieces as one skin the way a sculptor would: each piece to a signed distance grid, their union,
    the creases between them filled round (SDF fillet), the `carve` pieces cut out, a light smoothing, back to a mesh,
    reduced to about `keep` triangles. The pieces are removed."""
    ng = bpy.data.node_groups.get("RCY horse fuse") or bpy.data.node_groups.new("RCY horse fuse", "GeometryNodeTree")
    ng.nodes.clear()
    if not ng.interface.items_tree:
        ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
        ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    nodes, links = ng.nodes, ng.links
    out = nodes.new("NodeGroupOutput")
    union = nodes.new("GeometryNodeSDFGridBoolean")
    union.operation = "UNION"
    for i, part in enumerate(parts):
        info = nodes.new("GeometryNodeObjectInfo")
        info.inputs["Object"].default_value = part
        info.transform_space = "RELATIVE"
        sdf = nodes.new("GeometryNodeMeshToSDFGrid")
        sdf.inputs["Voxel Size"].default_value = voxel
        sdf.inputs["Band Width"].default_value = 4
        links.new(info.outputs["Geometry"], sdf.inputs["Mesh"])
        links.new(sdf.outputs["SDF Grid"], union.inputs[1])      # a union takes every grid on its one multi-input
    fil = nodes.new("GeometryNodeSDFGridFillet")
    fil.inputs["Iterations"].default_value = fillet
    links.new(union.outputs["Grid"], fil.inputs["Grid"])
    shaped = fil.outputs["Grid"]
    if carve:
        cut = nodes.new("GeometryNodeSDFGridBoolean")
        cut.operation = "DIFFERENCE"
        links.new(shaped, cut.inputs["Grid 1"])
        for part in carve:
            info = nodes.new("GeometryNodeObjectInfo")
            info.inputs["Object"].default_value = part
            info.transform_space = "RELATIVE"
            sdf = nodes.new("GeometryNodeMeshToSDFGrid")
            sdf.inputs["Voxel Size"].default_value = voxel
            sdf.inputs["Band Width"].default_value = 4
            links.new(info.outputs["Geometry"], sdf.inputs["Mesh"])
            links.new(sdf.outputs["SDF Grid"], cut.inputs["Grid 2"])
        shaped = cut.outputs["Grid"]
    smooth = nodes.new("GeometryNodeSDFGridMean")
    smooth.inputs["Width"].default_value = 1
    smooth.inputs["Iterations"].default_value = mean
    links.new(shaped, smooth.inputs["Grid"])
    mesh = nodes.new("GeometryNodeGridToMesh")
    mesh.inputs["Threshold"].default_value = 0.0          # the zero level of the distance field is the skin
    links.new(smooth.outputs["Grid"], mesh.inputs["Grid"])
    links.new(mesh.outputs["Mesh"], out.inputs[0])
    body = bpy.data.objects.new("Horse body", bpy.data.meshes.new("Horse body"))
    col.objects.link(body)
    mod = body.modifiers.new("Fuse", "NODES")
    mod.node_group = ng
    apply_all(body)
    for p in list(parts) + list(carve):
        bpy.data.objects.remove(p)
    faces = len(body.data.polygons) * 2
    dc = body.modifiers.new("Decimate", "DECIMATE")
    dc.ratio = min(1.0, keep / max(1, faces))
    apply_all(body)
    body.data.materials.clear()
    body.data.materials.append(material(COAT))
    for p in body.data.polygons:
        p.use_smooth = True
    return body


# ------------------------------------------------------------------ tack (승규's picture: brown leather straps, gold
# buckles, studs and rosettes, a cream plume). Every strap is cast onto the finished skin: rays from inside the body
# find where they leave it, so a strap lies on the horse whatever its shape.

def cast(body, rays, lift):
    """Where each ray (an origin inside the body, a direction out) leaves the skin, `lift` further out. A ray that
    misses, or runs far out along something standing off the skin (an ear, a leg), takes its neighbours' distance;
    the distances are smoothed a little but never pulled under the skin."""
    rs = []
    for o, d in rays:
        ok, loc, nrm, idx = body.ray_cast(o, d)
        rs.append((loc - o).length if ok else None)
    fixed = []
    for i, r in enumerate(rs):
        near = sorted(x for x in rs[max(0, i - 3):i + 4] if x is not None)
        m = near[len(near) // 2] if near else .2
        fixed.append(m if r is None or r > m * 1.35 else r)
    out = []
    for i, r in enumerate(fixed):
        near = fixed[max(0, i - 1):i + 2]
        out.append(max(r, sum(near) / len(near)))
    return [o + d * (r + lift) for (o, d), r in zip(rays, out)]


def ring_rays(c, u, v, a0, a1, n, closed=False):
    """Rays from c in the plane of u and v, from angle a0 to a1 (degrees: 0 along u, 90 along v)."""
    c, u, v = Vector(c), Vector(u).normalized(), Vector(v).normalized()
    return [(c, u * math.cos(th) + v * math.sin(th))
            for th in (math.radians(a0 + (a1 - a0) * i / (n if closed else n - 1)) for i in range(n))]


def line_rays(a, b, d, n):
    """Rays the same way `d` from points along a..b."""
    a, b, d = Vector(a), Vector(b), Vector(d).normalized()
    return [(a.lerp(b, i / (n - 1)), d) for i in range(n)]


def head_at(share, off):
    """A point in the head's frame: `share` of the way from the poll to the muzzle, `off` toward the face (- jaw)."""
    a = HEAD_P1 - HEAD_P0
    return HEAD_P0 + a * share + Vector((0, -a.z, a.y)).normalized() * off


class Strap:
    def __init__(self, pts, outs, closed, t):
        self.pts, self.outs, self.closed, self.t = pts, outs, closed, t

    def at(self, u):
        """The strap's outer face at share u of its length: (point, outward, along)."""
        n = len(self.pts)
        i = int(round(u * (n if self.closed else n - 1))) % n
        p = self.pts[i]
        nxt = self.pts[(i + 1) % n] if (self.closed or i < n - 1) else p
        prv = self.pts[i - 1] if (self.closed or i > 0) else p
        along = (nxt - prv).normalized()
        o = (self.outs[i] - along * self.outs[i].dot(along)).normalized()
        return p + o * (self.t / 2), o, along


def band(bm, pts, outs, w, t, closed=False):
    """A strap through pts, `w` wide along the skin and `t` thick off it (`outs`: outward at each point)."""
    n = len(pts)
    rings = []
    for i, p in enumerate(pts):
        nxt = pts[(i + 1) % n] if (closed or i < n - 1) else p
        prv = pts[i - 1] if (closed or i > 0) else p
        tan = (nxt - prv).normalized()
        out = (outs[i] - tan * outs[i].dot(tan)).normalized()
        side = tan.cross(out)
        rings.append([bm.verts.new(p + side * (sx * w / 2) + out * (sy * t / 2))
                      for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    spans = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for r0, r1 in spans:
        for k in range(4):
            bm.faces.new((r0[k], r0[(k + 1) % 4], r1[(k + 1) % 4], r1[k]))
    if not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])


def strap(bm, body, rays, w=.06, t=.016, closed=False, gap=.005):
    pts = cast(body, rays, t / 2 + gap)
    outs = [d for o, d in rays]
    band(bm, pts, outs, w, t, closed)
    return Strap(pts, outs, closed, t)


def slab(bm, body, y0, y1, a, oz=1.15, nu=13, nv=8, t=.03, gap=.005):
    """A pad or a cloth lying over the back from y0 to y1, `a` degrees down each side from the top (rays from
    (0, y, oz)), `t` thick. Returns its outer face as rows of (point, outward)."""
    inner, outer = [], []
    for j in range(nv):
        y = y0 + (y1 - y0) * j / (nv - 1)
        rays = [(Vector((0, y, oz)), Vector((math.sin(math.radians(th)), 0, math.cos(math.radians(th)))))
                for th in (-a + 2 * a * i / (nu - 1) for i in range(nu))]
        pts = cast(body, rays, gap)
        inner.append([bm.verts.new(p) for p in pts])
        outer.append([(p + d * t, d) for p, (o, d) in zip(pts, rays)])
    top = [[bm.verts.new(p) for p, d in row] for row in outer]
    for j in range(nv - 1):
        for i in range(nu - 1):
            bm.faces.new((top[j][i], top[j][i + 1], top[j + 1][i + 1], top[j + 1][i]))
            bm.faces.new((inner[j][i], inner[j + 1][i], inner[j + 1][i + 1], inner[j][i + 1]))
    rim = ([(0, i) for i in range(nu)] + [(j, nu - 1) for j in range(1, nv)] + [(nv - 1, i) for i in range(nu - 2, -1, -1)]
           + [(j, 0) for j in range(nv - 2, 0, -1)])
    for (ja, ia), (jb, ib) in zip(rim, rim[1:] + rim[:1]):
        bm.faces.new((inner[ja][ia], inner[jb][ib], top[jb][ib], top[ja][ia]))
    return outer


def stud(bm, at, out, r, h=.012, seg=16):
    """A round gold stud standing on a strap, domed: a short cone with a smaller top."""
    m = Matrix.Translation(at + out * (h / 2)) @ Vector((0, 0, 1)).rotation_difference(out).to_matrix().to_4x4()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r * .7, depth=h, matrix=m)


def rosette(bm, at, out, r):
    """A gold rosette: a disc and a boss on it."""
    stud(bm, at, out, r, .01, 20)
    stud(bm, at + out * .01, out, r * .55, .014, 16)


def buckle(bm, at, out, along, w, h, bar=.013, depth=.012):
    """A square gold buckle lying on a strap, `w` across it and `h` along it, with its tongue."""
    along = (along - out * along.dot(out)).normalized()
    side = along.cross(out)
    rot = Matrix((side, along, out)).transposed().to_4x4()
    for cx, cy, sx, sy in ((0, h / 2 - bar / 2, w, bar), (0, -h / 2 + bar / 2, w, bar), (w / 2 - bar / 2, 0, bar, h),
                           (-w / 2 + bar / 2, 0, bar, h), (0, 0, bar * .7, h * .9)):
        c = at + side * cx + along * cy + out * (depth / 2)
        bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(c) @ rot @ Matrix.Diagonal((sx, sy, depth, 1)))


def torus(bm, centre, axis, big, small, seg=20, sides=8):
    """A gold ring: the bit's rings, the rein rings on the pad, the stirrups."""
    axis = Vector(axis).normalized()
    a = axis.orthogonal().normalized()
    b = axis.cross(a)
    rings = []
    for i in range(seg):
        th = 2 * math.pi * i / seg
        radial = a * math.cos(th) + b * math.sin(th)
        cen = Vector(centre) + radial * big
        rings.append([bm.verts.new(cen + radial * (small * math.cos(2 * math.pi * j / sides))
                                   + axis * (small * math.sin(2 * math.pi * j / sides))) for j in range(sides)])
    for i in range(seg):
        r0, r1 = rings[i], rings[(i + 1) % seg]
        for j in range(sides):
            bm.faces.new((r0[j], r0[(j + 1) % sides], r1[(j + 1) % sides], r1[j]))


def blob(bm, centre, semi, along=None):
    """A smooth lump (a saddle's pommel and cantle), its y semi-axis turned to `along`."""
    rot = (Vector((0, 1, 0)).rotation_difference(along).to_matrix().to_4x4() if along is not None else Matrix())
    bmesh.ops.create_uvsphere(bm, u_segments=18, v_segments=10, radius=1,
                              matrix=Matrix.Translation(centre) @ rot @ Matrix.Diagonal((*semi, 1)))


def assemble(col, name, parts):
    """One object from bmeshes, each its own slot, in order: [(bm, material), ...]."""
    bm = bmesh.new()
    for slot, (part, mat) in enumerate(parts):
        for f in part.faces:
            f.material_index = slot
        me = bpy.data.meshes.new("tmp")
        part.to_mesh(me)
        part.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object(name, bm, col, [m for _, m in parts])
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def tack(col, body):
    """Every horse's: the bridle (crownpiece, browband, cheek pieces, noseband, the bit's rings, gold rosettes), the
    plume on the poll, the girth with its buckles, the breast collar with its studs and medallion and the strap over
    the withers that holds it up."""
    lea, gold, plume = bmesh.new(), bmesh.new(), bmesh.new()
    X = Vector((1, 0, 0))
    a = HEAD_P1 - HEAD_P0
    face = Vector((0, -a.z, a.y)).normalized()
    # the girth round the barrel behind the elbows
    girth = strap(lea, body, ring_rays((0, .3, 1.15), X, (0, 0, 1), 0, 360, 72, True), .085, .018, True)
    for u in (335 / 360, 205 / 360):
        p, o, al = girth.at(u)
        buckle(gold, p, o, al, .1, .075)
    # the breast collar round the chest, and the strap over the withers holding it up
    breast = strap(lea, body, ring_rays((0, .32, 1.1), X, (0, 1, 0), 6, 174, 48), .075, .018)
    # R683 (승규's picture): studs all along the breast collar
    for u in (.12, .18, .24, .3, .36, .42, .58, .64, .7, .76, .82, .88):
        p, o, al = breast.at(u)
        stud(gold, p, o, .019)
    p, o, al = breast.at(.5)
    rosette(gold, p, o, .055)
    withers = strap(lea, body, ring_rays((0, .56, 1.3), X, (0, -.38, .92), -25, 205, 40), .06, .016)
    for u in (0, 1):
        p, o, al = withers.at(u)
        buckle(gold, p, o, al, .075, .06)
    # the bridle: the crownpiece over the poll behind the ears, the browband before them, the noseband, and each
    # side's cheek piece from the crownpiece down behind and below the eye to the bit's ring
    crown = strap(lea, body, ring_rays((0, .95, 1.83), X, (0, -.25, 1), -50, 230, 44), .05, .014)
    brow = strap(lea, body, ring_rays(head_at(.05, 0), X, face, 22, 158, 24), .04, .013)
    nose = strap(lea, body, ring_rays(head_at(.64, -.012), X, face, 0, 360, 56, True), .05, .014, True)
    for u in (0, 1):
        p, o, al = brow.at(u)
        rosette(gold, p, o, .036)
    for s in (-1, 1):
        rays = [(head_at(sh, off), X * s) for sh, off in
                resample([(.2, -.11), (.4, -.085), (.64, -.06), (.8, -.05), (.92, -.045)], 4)]
        cheek = strap(lea, body, rays, .045, .014)
        p, o, al = cheek.at(0)
        rosette(gold, p, o, .03)
        p, o, al = cheek.at(.62)
        stud(gold, p, o, .02)
        p, o, al = cheek.at(1)
        # R683 (R680 QA: the bit's ring sat half in the muzzle): the ring stands off the cheek by its own thickness
        # and hangs a little forward and down of the strap's end, a gold boss where the strap holds it
        torus(gold, p + o * .03 + al * .03, X, .04, .01)
        stud(gold, p, o, .018)
    # R683 (승규's picture): a drop from the browband down the forehead to a big gold face badge
    drop = strap(lea, body, [(head_at(sh, 0), face) for sh in (.06, .12, .18, .24, .3)], .035, .012)
    p, o, al = drop.at(1)
    rosette(gold, p, o, .052)
    stud(gold, p + o * .024, o, .024, .014)
    # the plume: a gold holder on the crownpiece between the ears and a spray of cream feathers curving back
    p, o, al = crown.at(.5)
    up = Vector((0, -.3, 1)).normalized()
    m = Matrix.Translation(p + up * .04) @ Vector((0, 0, 1)).rotation_difference(up).to_matrix().to_4x4()
    bmesh.ops.create_cone(gold, cap_ends=True, segments=16, radius1=.02, radius2=.026, depth=.08, matrix=m)
    bmesh.ops.create_uvsphere(gold, u_segments=16, v_segments=8, radius=.03, matrix=Matrix.Translation(p + up * .085))
    rng = random.Random(5)
    base = p + up * .09
    # R683 ("흩날리게"): fuller, the feathers blown back and their tips curling down and out
    for k in range(20):
        side = (k % 5 - 2) / 2
        back = .3 + .15 * (k // 5) + rng.uniform(-.04, .04)
        length = .28 + .1 * (1 - abs(side)) + rng.uniform(-.03, .03)
        pts = [base + up * (length * t) + Vector((side * .075 * t + .03 * t ** 3, -(back * length) * t * t, -.08 * t ** 3))
               for t in (j / 11 for j in range(12))]
        strand(plume, pts, .07, .026, Vector((0, .6, .8)), tip=.25)
    return assemble(col, "Tack", [(lea, LEATHER), (gold, GOLD), (plume, PLUME)])


def harness(col, body):
    """A carriage horse's: the pad on the back with its two rein rings and a medallion, the straps along each side
    to the breeching round the quarters, the strap over the croup and the crupper along the spine to the tail."""
    lea, gold = bmesh.new(), bmesh.new()
    X = Vector((1, 0, 0))
    pad = slab(lea, body, .17, .45, 38, oz=1.2, nu=13, nv=8, t=.035)
    mid = pad[len(pad) // 2]
    p, o = mid[len(mid) // 2]
    rosette(gold, p, o, .045)
    for i in (3, len(mid) - 4):
        p, o = mid[i]
        torus(gold, p + o * .035, (0, 1, 0), .034, .009)
    for s in (-1, 1):
        side = strap(lea, body, line_rays((0, .26, 1.06), (0, -.52, 1.06), (s, 0, 0), 24), .055, .016)
        for u in (.12, .3, .48, .66):                      # R683: studs along the side straps
            p, o, al = side.at(u)
            stud(gold, p, o, .016)
    breech = strap(lea, body, ring_rays((0, -.52, 1.06), X, (0, 1, 0), 0, -180, 48), .07, .018)
    for u in (.3, .4, .5, .6, .7):
        p, o, al = breech.at(u)
        stud(gold, p, o, .02)
    for u in (0, 1):
        p, o, al = breech.at(u)
        buckle(gold, p, o, al, .085, .07)
    hip = strap(lea, body, ring_rays((0, -.62, 1.2), X, (0, 0, 1), -18, 198, 36), .05, .016)
    for u in (.2, .35, .65, .8):                           # R683: studs on the hip strap and the crupper
        p, o, al = hip.at(u)
        stud(gold, p, o, .016)
    crupper = strap(lea, body, line_rays((0, .4, 1.25), (0, -.88, 1.25), (0, 0, 1), 40), .045, .016)
    p, o, al = crupper.at((.4 + .62) / 1.28)
    rosette(gold, p, o, .035)
    for u in (.12, .32, .62):
        p, o, al = crupper.at(u)
        stud(gold, p, o, .015)
    return assemble(col, "Harness", [(lea, LEATHER), (gold, GOLD)])


def saddle(col, body):
    """A ridden horse's: a red cloth over the back edged in gold, the leather seat with its pommel and cantle, the
    stirrup leathers and gold stirrups."""
    cloth, gold, lea = bmesh.new(), bmesh.new(), bmesh.new()
    nu, nv = 25, 10
    top = slab(cloth, body, -.25, .38, 78, oz=1.15, nu=nu, nv=nv, t=.014)
    rim = ([top[0][i] for i in range(nu)] + [top[j][nu - 1] for j in range(1, nv)]
           + [top[nv - 1][i] for i in range(nu - 2, -1, -1)] + [top[j][0] for j in range(nv - 2, 0, -1)])
    band(gold, [p + d * .006 for p, d in rim], [d for p, d in rim], .035, .01, True)
    seat = slab(lea, body, -.12, .26, 30, oz=1.15, nu=11, nv=8, t=.045, gap=.02)
    p, o = seat[-1][5]
    blob(lea, p + o * .005, (.11, .04, .04), Vector((0, 1, 0)))
    p, o = seat[0][5]
    blob(lea, p + o * .01, (.15, .035, .05), Vector((0, 1, 0)))
    for s in (-1, 1):
        rays = [(Vector((0, .1, 1.15)), Vector((s * math.sin(math.radians(ph)), 0, math.cos(math.radians(ph)))))
                for ph in range(36, 106, 5)]
        leather = strap(lea, body, rays, .035, .012, gap=.024)
        p, o, al = leather.at(1)
        torus(gold, p - al * .05 + o * .02, (0, 1, 0), .045, .009)
    return assemble(col, "Saddle", [(cloth, CLOTH), (gold, GOLD), (lea, LEATHER)])


# ------------------------------------------------------------------ the armature (YardHorseRig.cs reads these names)
#
# Root holds the trunk still (YardHorse's Body pivot moves the whole model); Neck, Head and Tail1-3 follow YardHorse's
# Neck, Head and Tail pivots; each leg has four bones on its Leg, Elbow, Knee and Fetlock pivots: in front the upper arm
# from the shoulder joint, the forearm from the elbow, the cannon from the knee, the pastern from the fetlock; behind
# the thigh from the hip joint, the gaskin from the stifle, the cannon from the hock, the pastern from the fetlock.

def leg_bones(s, fore):
    x = s * LEG_X
    if fore:
        return [("UpperArm", (x, .68, 1.2), (x, .54, .93)), ("Forearm", (x, .54, .93), (x, FORE_Y, .5)),
                ("Cannon", (x, FORE_Y, .5), (x, FORE_Y, .245)), ("Pastern", (x, FORE_Y, .245), (x, FORE_Y + .07, .04))]
    x = s * (LEG_X + .01)
    return [("Thigh", (x, -.66, 1.3), (x, -.5, .98)), ("Gaskin", (x, -.5, .98), (x, HIND_Y - .02, .57)),
            ("HindCannon", (x, HIND_Y - .02, .57), (x, HIND_Y, .245)), ("HindPastern", (x, HIND_Y, .245), (x, HIND_Y + .07, .04))]


def rig(col):
    for o in [o for o in col.objects if o.type == "ARMATURE"]:
        bpy.data.objects.remove(o)
    arm = bpy.data.armatures.new("RCY Horse rig")
    ob = bpy.data.objects.new("Horse rig", arm)
    col.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.edit_bones

    def bone(name, h, t, parent=None):
        b = eb.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        b.roll = 0
        if parent:
            b.parent = eb[parent]
        return b
    bone("Root", (0, 0, 1.15), (0, .3, 1.15))
    bone("Neck", (0, .6, 1.33), (0, .95, 1.78), "Root")
    bone("Head", (0, .98, 1.86), (0, 1.33, 1.42), "Neck")
    bone("Tail1", TAIL_DOCK[0], TAIL_DOCK[1], "Root")
    bone("Tail2", TAIL_DOCK[1], TAIL_DOCK[2], "Tail1")
    bone("Tail3", TAIL_DOCK[2], (0, -1.04, .7), "Tail2")
    for s, side in ((-1, "L"), (1, "R")):
        for fore in (True, False):
            parent = "Root"
            for name, h, t in leg_bones(s, fore):
                bone(name + "." + side, h, t, parent)
                parent = name + "." + side
    bpy.ops.object.mode_set(mode="OBJECT")
    return ob


def weigh_rigid(ob, group):
    vg = ob.vertex_groups.get(group) or ob.vertex_groups.new(name=group)
    vg.add([v.index for v in ob.data.vertices], 1.0, "REPLACE")


def weigh_by(ob, fn):
    """Weights each vertex by fn(world point) -> [(group, weight), ...]."""
    for v in ob.data.vertices:
        for group, w in fn(ob.matrix_world @ v.co):
            vg = ob.vertex_groups.get(group) or ob.vertex_groups.new(name=group)
            if w > 0:
                vg.add([v.index], w, "REPLACE")


def side_of(p):
    return "L" if p.x < 0 else "R"


def transfer_weights(src, dst):
    """Gives `dst` the weights of the skin under it (the mane and the tack follow the skin they lie on)."""
    for g in src.vertex_groups:
        if dst.vertex_groups.get(g.name) is None:
            dst.vertex_groups.new(name=g.name)
    mod = dst.modifiers.new("Weights", "DATA_TRANSFER")
    mod.object = src
    mod.use_vert_data = True
    mod.data_types_verts = {"VGROUP_WEIGHTS"}
    mod.vert_mapping = "POLYINTERP_NEAREST"
    mod.layers_vgroup_select_src = "ALL"
    mod.layers_vgroup_select_dst = "NAME"
    apply_all(dst)


def only_groups(ob, keep, fallback="Neck"):
    """Each vertex's weights limited to the groups in `keep`, renormalised (the mane lies on the shoulder but must
    not swing with the leg)."""
    names = {g.index: g.name for g in ob.vertex_groups}
    for v in ob.data.vertices:
        ws = [(names[x.group], x.weight) for x in v.groups]
        total = sum(w for n, w in ws if n in keep)
        for n, w in ws:
            if n not in keep:
                ob.vertex_groups[n].remove([v.index])
            elif total > 0:
                ob.vertex_groups[n].add([v.index], w / total, "REPLACE")
        if total <= 0:
            ob.vertex_groups[fallback].add([v.index], 1.0, "REPLACE")


def rigid_slot(ob, slot, group):
    """Every vertex of the faces in `slot` on `group` alone (the plume rides the head, it does not bend with it)."""
    verts = {v for p in ob.data.polygons if p.material_index == slot for v in p.vertices}
    for g in ob.vertex_groups:
        g.remove(list(verts))
    ob.vertex_groups[group].add(list(verts), 1.0, "REPLACE")


def set_slots(ob, slots):
    """The object's slots made exactly `slots`, in that order, its faces keeping their materials."""
    own = [m.name for m in ob.data.materials]
    idx = [p.material_index for p in ob.data.polygons]
    ob.data.materials.clear()
    for n in slots:
        ob.data.materials.append(bpy.data.materials.get(n) or material(n))
    for p, i in zip(ob.data.polygons, idx):
        p.material_index = slots.index(own[i])


def tail_weights(p):
    # the dock bends along its three bones; the hair hangs from the last
    if p.z > 1.35:
        return [("Tail1", 1.0)]
    if p.z > 1.24:
        u = (1.35 - p.z) / .11
        return [("Tail1", 1 - u), ("Tail2", u)]
    u = max(0.0, min(1.0, (1.24 - p.z) / .14))
    return [("Tail2", 1 - u), ("Tail3", u)]


def bind(ob, armature):
    if not any(m.type == "ARMATURE" for m in ob.modifiers):
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = armature
    ob.parent = armature


def skin(col, body, extras, tack_pieces, armature):
    """Weights every piece to the armature: the skin by bone heat, the hooves and eyes rigid, the mane and the tack
    from the skin under them, the tail along its dock. The skin, hooves, eyes and hair become one mesh, "Horse"."""
    for o in bpy.context.selected_objects:
        o.select_set(False)
    body.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    mask_skin(body)
    joint_blend(body)
    for o in extras:
        o.parent = None
        n = o.name
        if n.startswith("Hoof"):
            p = o.matrix_world @ o.data.vertices[0].co
            weigh_rigid(o, ("Pastern." if p.y > 0 else "HindPastern.") + side_of(p))
        elif n.startswith("Eye"):
            weigh_rigid(o, "Head")
        elif n.startswith("Mane"):
            transfer_weights(body, o)
            only_groups(o, ("Root", "Neck", "Head"))
        elif n.startswith("Tail"):
            weigh_by(o, tail_weights)
    for o in [body] + list(extras):
        set_slots(o, HORSE_SLOTS)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in [body] + list(extras):
        o.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = body.data.name = "Horse"
    bind(body, armature)
    for t in tack_pieces:
        transfer_weights(body, t)
        names = [m.name for m in t.data.materials]
        if PLUME in names:
            rigid_slot(t, names.index(PLUME), "Head")
        if t.name in ("Harness", "Saddle"):
            # R680 QA (rigging): the skin's bone heat let the tail bones carry the breeching (it swung 20-23 cm with
            # the tail) and the neck the harness pad and saddle (6-10 cm); these ride the trunk and the legs only.
            only_groups(t, [g.name for g in t.vertex_groups if g.name != "Neck" and not g.name.startswith("Tail")],
                        fallback="Root")
        bind(t, armature)
    for o in [body] + list(tack_pieces):
        limit_weights(o, 4)
    return body


LEG_NAMES = ("UpperArm", "Forearm", "Cannon", "Pastern", "Thigh", "Gaskin", "HindCannon", "HindPastern")


def _ramp(a, b, t):
    """0 at a, 1 at b, straight between."""
    return min(1.0, max(0.0, (t - a) / (b - a)))


def _skin_reach(name, p):
    """How much of its bone-heat weight a bone keeps at skin point p (1 = all)."""
    base = name.split(".")[0]
    if base in LEG_NAMES:
        s = -1.0 if name.endswith(".L") else 1.0
        fore = not (base in ("Thigh", "Gaskin") or base.startswith("Hind"))
        k = _ramp(-.04, .02, s * p.x)                          # its own side of the body only
        k *= _ramp(.2, .38, p.y) if fore else _ramp(.25, .45, -p.y)   # not the middle of the barrel
        if base in ("UpperArm", "Thigh"):                        # nor the back above the shoulder / hip joint
            top = 1.2 if fore else 1.3
            k *= 1.0 - _ramp(top - .05, top + .2, p.z)
        return k
    if base.startswith("Tail"):                                 # the dock, not the buttocks
        a, b = TAIL_DOCK[0], TAIL_DOCK[-1]
        t = max(0.0, min(1.0, (p - a).dot(b - a) / (b - a).length_squared))
        return 1.0 - _ramp(.1, .2, (p - (a + (b - a) * t)).length)
    if base == "Neck":                                          # in front of the withers
        return _ramp(.45, .6, p.y)
    return 1.0


def mask_skin(ob):
    """R680 QA (rigging): bone heat let the leg bones bend the middle of the barrel and the other side (the belly's
    underside moved 21 cm at the gallop), the tail bones the buttocks (15 cm) and the neck the withers under the harness
    pad and saddle. Each bone keeps its weight only in its own region (fading over 15-20 cm); what it gives up goes to
    Root, the trunk."""
    names = {g.index: g.name for g in ob.vertex_groups}
    root = ob.vertex_groups.get("Root") or ob.vertex_groups.new(name="Root")
    for v in ob.data.vertices:
        p = ob.matrix_world @ v.co
        moved = 0.0
        for gi, w in [(x.group, x.weight) for x in v.groups]:
            n = names[gi]
            k = _skin_reach(n, p)
            if k >= 1.0:
                continue
            moved += w * (1.0 - k)
            if w * k > 1e-4:
                ob.vertex_groups[n].add([v.index], w * k, "REPLACE")
            else:
                ob.vertex_groups[n].remove([v.index])
        if moved > 0:
            had = next((x.weight for x in v.groups if x.group == root.index), 0.0)
            root.add([v.index], had + moved, "REPLACE")


# R683 (R680 QA: the fore knee folded to 105 degrees kept 64% of its section, 87 triangles through each other): bone heat
# handed a joint from one bone to the next over 2-3 cm, so the skin creased at a line. Across each lower joint the two
# bones' share is now eased over `half` above and below the joint: (upper bone, lower bone, joint height, half).
LEG_JOINTS = (("Forearm", "Cannon", .5, .085), ("Cannon", "Pastern", .245, .05),
              ("Gaskin", "HindCannon", .57, .085), ("HindCannon", "HindPastern", .245, .05))


def joint_blend(ob):
    names = {g.index: g.name for g in ob.vertex_groups}
    for v in ob.data.vertices:
        p = ob.matrix_world @ v.co
        ws = {names[x.group]: x.weight for x in v.groups}
        for a, b, zj, half in LEG_JOINTS:
            for side in ("L", "R"):
                ga, gb = a + "." + side, b + "." + side
                tot = ws.get(ga, 0.0) + ws.get(gb, 0.0)
                if tot < 1e-4 or abs(p.z - zj) > half * 1.5:
                    continue
                t = max(0.0, min(1.0, (zj + half - p.z) / (2 * half)))
                t = t * t * (3 - 2 * t)
                for g, w in ((ga, tot * (1 - t)), (gb, tot * t)):
                    if w > 1e-4:
                        ob.vertex_groups[g].add([v.index], w, "REPLACE")
                    elif g in ws:
                        ob.vertex_groups[g].remove([v.index])
                    ws[g] = w


def limit_weights(ob, most=4):
    """R680 QA (rigging / unity-export): Unity skins with at most 4 bones a vertex and renormalises, so keep each
    vertex's 4 largest weights summing to 1 here too (the bone heat gave up to 7, summing to 0.89-1.0) and Blender and
    Unity bend the same."""
    names = {g.index: g.name for g in ob.vertex_groups}
    for v in ob.data.vertices:
        ws = sorted(((x.weight, names[x.group]) for x in v.groups if x.weight > 0), reverse=True)
        keep = ws[:most]
        total = sum(w for w, _ in keep)
        if total <= 0:
            continue
        for w, n in ws[most:]:
            ob.vertex_groups[n].remove([v.index])
        for w, n in keep:
            ob.vertex_groups[n].add([v.index], w / total, "REPLACE")
        for x in list(v.groups):
            if x.weight <= 0:
                ob.vertex_groups[names[x.group]].remove([v.index])


def build_horse():
    col = collection("RCY Horse")
    body, extras = build_horse_body(col)
    pieces = [tack(col, body), harness(col, body), saddle(col, body)]
    armature = rig(col)
    horse = skin(col, body, extras, pieces, armature)
    return horse, pieces, armature


# R680 QA (lod-pipeline / creature-artist): a team horse was 33k triangles with no LOD and ten of them run at once. LOD1
# is each mesh decimated (weights kept, 4 a vertex): Horse 23k -> ~5.8k, Tack 7.1k -> ~2.1k, Harness / Saddle -> ~1.2k.
HORSE_LOD1 = {"Horse": .25, "Tack": .3, "Harness": .35, "Saddle": .35}


def horse_lods(col):
    """Each skinned mesh M becomes M_LOD0 and gets a decimated M_LOD1 on the same bones (Unity's importer makes the
    LOD Group from the names). Returns the LOD1 objects; restore_lods() undoes it after the export."""
    made = []
    for o in [o for o in col.objects if o.type == "MESH" and o.name in HORSE_LOD1]:
        lod = o.copy()
        lod.data = o.data.copy()
        col.objects.link(lod)
        dec = lod.modifiers.new("LOD1", "DECIMATE")
        dec.ratio = HORSE_LOD1[o.name]
        with bpy.context.temp_override(object=lod, active_object=lod, selected_objects=[lod]):
            bpy.ops.object.modifier_move_to_index(modifier="LOD1", index=0)
            bpy.ops.object.modifier_apply(modifier="LOD1")
        limit_weights(lod, 4)
        lod.name = lod.data.name = o.name + "_LOD1"
        o.name = o.name + "_LOD0"
        made.append(lod)
    return made


def restore_lods(col, made):
    for lod in made:
        bpy.data.objects.remove(lod, do_unlink=True)
    for o in col.objects:
        if o.name.endswith("_LOD0"):
            o.name = o.name[:-5]


def export_horse(name="RcyHorse"):
    """Writes the skinned horse, its tack and the armature as one FBX (the zone models' import settings but a
    Generic rig, so the skin weights come in; no animation, the bones kept as transforms), with an LOD1 of each mesh."""
    col = bpy.data.collections["RCY Horse"]
    lods = horse_lods(col)
    try:
        return _export_horse(col, name)
    finally:
        restore_lods(col, lods)


def _export_horse(col, name):
    objs = [o for o in col.all_objects if o.type in ("MESH", "ARMATURE")]
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = [o for o in objs if o.type == "ARMATURE"][0]
    rel = RCY_OUT_REL + "/" + name + ".fbx"
    bpy.ops.export_scene.fbx(filepath=os.path.join(RCY_ROOT, rel), use_selection=True, object_types={"MESH", "ARMATURE"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True,
                             armature_nodetype="NULL", path_mode="AUTO", embed_textures=False)
    if not os.path.exists(os.path.join(RCY_ROOT, rel + ".meta")):
        with open(RCY_FBX_TEMPLATE, encoding="utf-8") as f:
            text = f.read()
        lines = text.split("\n")
        lines[1] = "guid: " + _guid(rel)
        # A Generic rig: with none, Unity drops the skin weights and the meshes come in rigid.
        _write(rel + ".meta", "\n".join(lines).replace("  animationType: 0", "  animationType: 2"))
    each = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH"}
    print("exported", rel, sum(each.values()), "tris", each)
    return rel


def build_horse_body(col):
    clear(col)
    parts = [trunk(col), neck(col), head(col), masses(col)]
    for s in (-1, 1):
        parts.append(leg_loft(col, "Fore leg", s * LEG_X, FORE_Y, FORE_SECTIONS))
        parts.append(leg_loft(col, "Hind leg", s * (LEG_X + .01), HIND_Y, HIND_SECTIONS))
        parts.append(hock_point(col, s * (LEG_X + .01)))
    cuts = head_carvers(col)
    for s in (-1, 1):
        e, cup = ear(col, s)
        parts.append(e)
        cuts.append(cup)
    body = sdf_fuse(col, parts, cuts)
    body_eyes = eyes(col)
    hooves = []
    for s in (-1, 1):
        hooves.append(hoof(col, s * LEG_X, FORE_Y))
        hooves.append(hoof(col, s * (LEG_X + .01), HIND_Y))
    return body, hooves + body_eyes + [mane(col), tail(col)]
