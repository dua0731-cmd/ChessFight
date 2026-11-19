"""R1 part 1 (seoungtest R725, 2026-11-19): the entry gate - the arched doorway where the entry tunnel comes into the
hall, modelled after RO_A1's "입구 문" and RO_1 / RO_2 bottom left (second version: the user, on the first - a flat
lintel under a half-round panel: "컨셉 아트 처럼 다시 정밀하게 다시 모델링 해").

    blender -b --factory-startup --python ro_r1_gate_model.py -- [--stills] [--export]

Graybox (ro_blockout.json): the hall's south wall is z 14 .. 14.5 (Unity) with the tunnel's opening x -6.3 .. 6.3,
y 0 .. 6 (drawn only); the entry floor runs z 0 .. 14 between rails at x +-6 .. 6.3; the threshold tongue and the first
star platform lie north of the wall. The gate is drawn only and leaves every walking face and the opening clear.
As the concept, on the wall's hall face (Unity z 14.5):
  - two gold columns (x +-6.75) on square gold plinths: a torus-and-scotia base, a fluted shaft with two ring bands, a
    bell capital with four volutes and leaves, a square abacus - their capitals carry the arch;
  - the round arch springing from the capitals (round (0, 6), the opening's width): rounded mouldings swept round it - an
    inner gold torus, a navy band with gold stars, an outer gold roll with a bead string - and an eight-point gold star
    keystone standing over its crown;
  - the arch's field: the door's upper part, violet boards with a gold rim and a gold seam down the middle and the
    great gold crown of the concept (a tall middle point with its ball, two side points with theirs, a stepped base);
  - two marble piers (x +-7.45 .. 8.95) outside the columns: a gold base moulding, marble shaft, a navy frieze with a gold
    star between gold mouldings, a gold cornice, the big pearl lamp in its gold cup on top (light: Unity's
    RoyalObservatoryDressing); a corbelled foot down the wall under each;
  - the two door leaves stand open, swung back into the tunnel against its sides (Unity x +-6.36, z 14 .. 7.85): violet
    boards, gold rim and rails, the crown, studs, hinges and a ring.
-> ro_r1_gate.blend, stills/r1/ (beside RO_A1 and RO_1), --export Assets/Resources/RoyalObservatory/Models/Dipper/RoEntryGate.fbx
(+ _lamps.txt). Static: the section's frame. Never export while Unity is in Play.
"""
import math, os, random, sys
import bmesh, bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k        # noqa: E402
import ro_parts as P      # noqa: E402
from ro_parts import GOLD, NAVY, VIOLET, MARBLE, PEARL, _merge, _transform, star_stud, bead, torus  # noqa: E402

MODEL, ZONE = "RoEntryGate", "Dipper"
FACE_Y = 14.5            # Unity z of the wall's hall face = Blender y
OPEN_HALF = 6.3          # the opening's half width (Unity x)
SPRING = 6.0             # the arch springs here (the opening's top, the capitals' top)
COL_X, COL_R = 6.75, .4  # column centre and shaft radius
PIER_X0, PIER_X1 = 7.45, 8.95
PIER_TOP = 10.4

# swap matrices: a lathe round z turned so its axis is the wall's normal (+y) and its circle lies in the wall's plane
TO_WALL = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
# a profile drawn as (depth out of the wall, height) and pushed along x
ALONG_X = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def wall(b, x=0.0, z=0.0, d=0.0):
    _transform(b, Matrix.Translation((x, FACE_Y + d, z)))
    return b


def face_prism(outline, d0, d1, mat, bm):
    """An outline in the wall's plane (x, height) standing from depth d0 to d1 out of the wall."""
    b = k.prism(outline, d0, d1, mat)
    _transform(b, TO_WALL)
    bmesh.ops.reverse_faces(b, faces=b.faces)
    _merge(bm, wall(b))


def rect(x0, x1, z0, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


def moulding(bm, profile, x0, x1, mats):
    """A profile [(depth, height), ...] (a closed section, listed round) swept straight along x from x0 to x1."""
    b = k.prism(profile, x0, x1, mats)
    _transform(b, ALONG_X)
    _merge(bm, wall(b))


def arch_sweep(bm, profile, cz, mats, seg=72):
    """A section [(radius, depth), ...] swept round the half circle over (0, cz) in the wall's plane: rounded arch
    mouldings."""
    b = k.lathe(profile, seg, mat=mats, angle=180.0)
    _transform(b, TO_WALL)
    bmesh.ops.reverse_faces(b, faces=b.faces)
    _merge(bm, wall(b, z=cz))


def half_round(d0, h0, h1, out, n=6):
    """A section: a half round of height h0..h1 standing `out` from depth d0 (for mouldings), as (depth, height)."""
    pts = [(d0, h0)]
    for i in range(n + 1):
        a = -math.pi / 2 + math.pi * i / n
        pts.append((d0 + out * math.cos(a), (h0 + h1) * .5 + (h1 - h0) * .5 * math.sin(a)))
    pts.append((d0, h1))
    return pts


def on_face(b, x, h, d):
    """A part built facing +x (studs, stars) turned to face the hall (+y), at (x, height), d out of the wall."""
    _transform(b, Matrix.Rotation(math.pi / 2, 4, "Z"))
    return wall(b, x, h, d)


# ------------------------------------------------------------------ the crown (RO_A1 / RO_1)

def crown_parts(s):
    """The concept's crown as outlines (x, y) of size s (about its width / 2): a tall middle point and two side points
    curving out, each topped by a ball; a band; a stepped base. Returns (outlines, balls[(x, y, r)])."""
    body = []
    # left half from the band's top corner round to the middle tip, then mirrored
    half = [(-.78, -.15), (-1.0, .55), (-.92, .62), (-.52, .12), (-.38, .1), (-.14, .72), (0, .92)]
    body = half + [(-x, y) for x, y in reversed(half[:-1])]
    band = [(-.82, -.32), (.82, -.32), (.78, -.15), (-.78, -.15)]
    base1 = [(-.9, -.5), (.9, -.5), (.86, -.34), (-.86, -.34)]
    base2 = [(-.66, -.62), (.66, -.62), (.62, -.5), (-.62, -.5)]
    outlines = [[(x * s, y * s) for x, y in o] for o in (body, band, base1, base2)]
    balls = [(-.98 * s, .68 * s, .11 * s), (.98 * s, .68 * s, .11 * s), (0, 1.03 * s, .13 * s)]
    return outlines, balls


def crown(bm, s, place, depth):
    """The crown in relief, built in its own (x, y) plane facing +z, then placed by `place` (a matrix)."""
    outlines, balls = crown_parts(s)
    for i, o in enumerate(outlines):
        b = k.prism(o, 0, depth * (1.0 if i == 0 else 1.15), GOLD)
        _transform(b, place)
        _merge(bm, b)
    for (x, y, r) in balls:
        b = bead(r, u=14, v=8)
        _transform(b, place @ Matrix.Translation((x, y, depth * .8)))
        _merge(bm, b)
    # an engraved line along the band (a thin raised wire) and three small gems
    for x in (-.45 * s, 0, .45 * s):
        g = bead(.06 * s, u=10, v=6)
        _transform(g, place @ Matrix.Translation((x, -.235 * s, depth * 1.15)))
        _merge(bm, g)


# ------------------------------------------------------------------ the pieces

def column(bm, x):
    r = COL_R
    d = r + .12                                   # its axis stands this far out of the wall
    prof = [(0, .0), (r + .18, .0), (r + .18, .1),          # square-ish plinth top ring (on the plinth below)
            (r + .2, .14), (r + .2, .2), (r + .14, .26),    # lower torus
            (r + .06, .3), (r + .05, .36), (r + .1, .4),    # scotia
            (r + .12, .44), (r + .08, .5), (r + .02, .54), (r, .58)]
    base = k.lathe(prof + [(0, .58)], 40, mat=GOLD)
    shaft = P.fluted_shaft(r, .58, 5.0, 24, .05)
    for b in (base, shaft):
        _merge(bm, wall(b, x, 0, d))
    for z in (1.15, 4.45):                                  # the ring bands
        band = k.lathe([(0, -.08), (r + .04, -.08), (r + .07, -.04), (r + .07, .04), (r + .04, .08), (0, .08)], 40, mat=GOLD)
        _merge(bm, wall(band, x, z, d))
    # the bell capital: astragal, bell, then the abacus
    cap = k.lathe([(0, 5.0), (r + .05, 5.0), (r + .07, 5.05), (r + .03, 5.1), (r + .02, 5.15), (r + .1, 5.35),
                   (r + .22, 5.55), (r + .3, 5.66), (r + .3, 5.7), (0, 5.7)], 40, mat=GOLD)
    _merge(bm, wall(cap, x, 0, d))
    # leaves round the bell and four volutes at the corners under the abacus
    for i in range(8):
        t = 2 * math.pi * i / 8 + math.pi / 8
        lf = k.prism([(-.09, 0), (.09, 0), (.05, .28), (0, .38), (-.05, .28)], 0, .03, GOLD)
        _transform(lf, Matrix.Rotation(-.35, 4, "X"))
        _transform(lf, Matrix.Rotation(math.pi / 2, 4, "X"))
        _transform(lf, Matrix.Rotation(t - math.pi / 2, 4, "Z"))
        _transform(lf, Matrix.Translation(((r + .04) * math.cos(t), (r + .04) * math.sin(t), 5.12)))
        _merge(bm, wall(lf, x, 0, d))
    for i in range(4):
        t = 2 * math.pi * i / 4 + math.pi / 4
        v = torus(.09, .035, 16, 6)
        _transform(v, Matrix.Rotation(math.pi / 2, 4, "X"))
        _transform(v, Matrix.Rotation(t + math.pi / 2, 4, "Z"))
        _transform(v, Matrix.Translation(((r + .27) * math.cos(t), (r + .27) * math.sin(t), 5.55)))
        _merge(bm, wall(v, x, 0, d))
    # square plinth under it (gold, moulded) and the abacus over it
    w = r + .26
    face_prism(rect(x - w, x + w, -.42, 0), 0, 2 * d, GOLD, bm)
    face_prism(rect(x - w - .05, x + w + .05, -.5, -.42), 0, 2 * d + .06, GOLD, bm)
    face_prism(rect(x - w, x + w, 5.7, 5.86), 0, 2 * d, GOLD, bm)
    face_prism(rect(x - w - .06, x + w + .06, 5.86, SPRING), 0, 2 * d + .08, GOLD, bm)
    # the half pilaster behind it, marble, where the column meets the wall
    face_prism(rect(x - .5, x + .5, -.42, SPRING), 0, .06, MARBLE, bm)   # 6 cm (was 12, flush with the column's back: it flickered)


def arch(bm):
    cz = SPRING
    r_in = OPEN_HALF + .05
    # inner gold torus, navy band, outer roll: each a section swept round
    arch_sweep(bm, [(r_in, 0), (r_in - .02, .25), (r_in + .02, .55), (r_in + .12, .72), (r_in + .26, .74), (r_in + .36, .62),
                    (r_in + .38, .4), (r_in + .38, 0)], cz, GOLD)
    arch_sweep(bm, [(r_in + .38, 0), (r_in + .38, .5), (r_in + .9, .5), (r_in + .9, 0)], cz, NAVY)
    arch_sweep(bm, [(r_in + .9, 0), (r_in + .9, .58), (r_in + .95, .7), (r_in + 1.07, .8), (r_in + 1.2, .78), (r_in + 1.3, .66),
                    (r_in + 1.33, .5), (r_in + 1.42, .46), (r_in + 1.48, .3), (r_in + 1.48, 0)], cz, GOLD)
    mid = r_in + .64
    for i in range(9):
        a = math.radians(12 + 156 * i / 8)
        if abs(i - 4) < .5:
            continue                                        # the keystone's place
        _merge(bm, on_face(star_stud(.34, .07, points=4), mid * math.cos(a), cz + mid * math.sin(a), .5))
        for da in (-1, 1):                                  # small dots between
            a2 = a + math.radians(da * 9.75)
            dot = bead(.035, u=8, v=5)
            _merge(bm, wall(dot, mid * math.cos(a2), cz + mid * math.sin(a2), .52))
    n = 76
    for i in range(n + 1):
        a = math.radians(180 * i / n)
        b = bead(.055, u=8, v=5)
        _merge(bm, wall(b, (r_in + 1.33) * math.cos(a), cz + (r_in + 1.33) * math.sin(a), .58))
    # the keystone: a gold block, the eight-point star over the crown standing proud, two scrolls
    face_prism([(-.42, cz + r_in - .05), (.42, cz + r_in - .05), (.55, cz + r_in + 1.45), (-.55, cz + r_in + 1.45)], 0, .82, GOLD, bm)
    ks = star_stud(2.2, .42, points=8)
    _merge(bm, on_face(ks, 0, cz + r_in + 1.2, .82))
    for s in (-1, 1):
        sc = torus(.14, .05, 20, 6)
        _transform(sc, Matrix.Rotation(math.pi / 2, 4, "X"))
        _merge(bm, wall(sc, s * .62, cz + r_in + .35, .62))
    # the field inside the arch: the door's upper part - violet boards, a gold rim, a seam, the crown
    boards = 14
    for i in range(boards):
        x0 = -r_in + 2 * r_in * i / boards + .015
        x1 = -r_in + 2 * r_in * (i + 1) / boards - .015
        top0 = cz + math.sqrt(max(0.0, r_in * r_in - x0 * x0))
        top1 = cz + math.sqrt(max(0.0, r_in * r_in - x1 * x1))
        steps = 4
        outline = [(x0, cz - .02), (x1, cz - .02)]
        for j in range(steps + 1):
            xx = x1 - (x1 - x0) * j / steps
            outline.append((xx, cz + math.sqrt(max(0.0, r_in * r_in - xx * xx))))
        face_prism(outline, .02, .1 + .015 * ((i * 7) % 3), VIOLET, bm)
    face_prism(rect(-.05, .05, cz - .02, cz + r_in - .05), .1, .16, GOLD, bm)          # the seam between the leaves
    face_prism(rect(-r_in, r_in, cz - .02, cz + .16), .1, .2, GOLD, bm)               # a gold rail along its foot
    crown(bm, 1.75, Matrix.Translation((0, FACE_Y + .14, cz + 2.75)) @ Matrix.Rotation(math.pi, 4, "Z") @
          Matrix.Rotation(math.pi / 2, 4, "X"), .16)          # its plane onto the wall, its relief toward the hall
    # studs along the field's rim
    for i in range(1, 22):
        a = math.radians(180 * i / 22)
        st = bead(.05, u=8, v=5)
        _merge(bm, wall(st, (r_in - .25) * math.cos(a), cz + (r_in - .25) * math.sin(a), .12))


def pier(bm, side):
    x0, x1 = sorted((side * PIER_X0, side * PIER_X1))
    cx = (x0 + x1) * .5
    D = 1.15
    # gold base moulding at the walk's level (a plinth, a torus, a fillet)
    moulding(bm, [(0, -.45), (D + .18, -.45), (D + .18, -.15)] + half_round(D + .12, -.15, .12, .1)[1:-1] +
             [(D + .12, .12), (D + .05, .2), (0, .2)], x0 - .18, x1 + .18, GOLD)
    face_prism(rect(x0 - .14, x1 + .14, -.45, .2), 0, D + .12, GOLD, bm)
    # the marble shaft
    face_prism(rect(x0, x1, .2, PIER_TOP - 2.2), 0, D, MARBLE, bm)
    # the frieze: gold astragal, navy panel with a gold star and corner dots, gold moulding
    z0 = PIER_TOP - 2.2
    moulding(bm, half_round(D, z0, z0 + .14, .07), x0, x1, GOLD)
    face_prism(rect(x0, x1, z0 + .14, z0 + .9), 0, D, NAVY, bm)
    for (a, b_) in ((x0 + .04, x0 + .12), (x1 - .12, x1 - .04)):
        face_prism(rect(a, b_, z0 + .14, z0 + .9), D, D + .04, GOLD, bm)
    side_face = lambda b, h: _merge(bm, wall(b, cx, h, D))    # noqa: E731
    st = star_stud(.62, .1, points=4)
    _transform(st, Matrix.Rotation(math.pi / 2, 4, "Z"))
    side_face(st, z0 + .52)
    for xx in (x0 + .3, x1 - .3):
        g = bead(.06, u=10, v=6)
        _merge(bm, wall(g, xx, z0 + .52, D + .02))
    moulding(bm, half_round(D, z0 + .9, z0 + 1.04, .07), x0, x1, GOLD)
    # the cornice: gold cyma, a navy fascia line, gold crown moulding, marble cap
    zc = z0 + 1.04
    moulding(bm, [(0, zc), (D + .02, zc), (D + .1, zc + .08), (D + .2, zc + .2), (D + .26, zc + .34), (D + .28, zc + .44),
                  (D + .36, zc + .5), (D + .36, zc + .62), (0, zc + .62)], x0 - .36, x1 + .36, GOLD)
    face_prism(rect(x0 - .3, x1 + .3, zc, zc + .62), 0, D + .2, GOLD, bm)
    face_prism(rect(x0 - .24, x1 + .24, zc + .62, PIER_TOP), 0, D + .28, MARBLE, bm)
    # the corbelled foot down the wall under it, and a gold drop
    for i, (h0, h1, out) in enumerate(((-1.1, -.45, D + .05), (-1.6, -1.1, D - .25), (-2.0, -1.6, D - .5))):
        face_prism(rect(x0 + .12 * i, x1 - .12 * i, h0, h1), 0, out, MARBLE if i != 1 else GOLD, bm)
    drop = k.lathe([(0, 0), (.14, .05), (.18, .22), (.12, .38), (.06, .46), (0, .5)], 16, mat=GOLD)
    _transform(drop, Matrix.Rotation(math.pi, 4, "X"))
    _merge(bm, wall(drop, cx, -2.0, (D - .5) * .5))
    # thin gold edge beads up the shaft's front corners
    for xx in (x0 + .03, x1 - .03):
        e = k.lathe([(0, 0), (.035, 0), (.035, PIER_TOP - 2.4), (0, PIER_TOP - 2.4)], 8, mat=GOLD)
        _merge(bm, wall(e, xx, .2, D))


def pier_lamp(bm, x, y, z):
    """The concept's lamp on a pier: a gold footed cup, two lyre arms curling up round the big pearl globe, a finial.
    Returns the globe's centre."""
    cup = k.lathe([(0, 0), (.38, 0), (.38, .07), (.26, .12), (.14, .2), (.11, .34), (.18, .38), (.13, .43), (.09, .52),
                   (.18, .6), (.34, .66), (.36, .72), (0, .72)], 36, mat=GOLD)
    _merge(bm, wall(cup, x, z, y - FACE_Y))
    g = .66
    gz = z + .72 + g * .9
    for i in range(2):
        t = math.pi * i
        pts = []
        for j in range(12):
            a = -math.pi / 2 + math.pi * .72 * j / 11
            rr = (g + .05) * math.cos(a) + .08 * math.sin(math.pi * j / 11)
            pts.append(Vector((x + rr * math.cos(t), y + rr * math.sin(t), gz + (g + .05) * math.sin(a))))
        pts[0] = Vector((x + .25 * math.cos(t), y + .25 * math.sin(t), z + .68))
        _merge(bm, P._tube(pts, .035, 8))
        sc = torus(.06, .025, 12, 6)
        _transform(sc, Matrix.Rotation(math.pi / 2, 4, "X"))
        _transform(sc, Matrix.Rotation(t + math.pi / 2, 4, "Z"))
        _transform(sc, Matrix.Translation(tuple(pts[-1])))
        _merge(bm, sc)
    glb = bmesh.new()
    bmesh.ops.create_uvsphere(glb, u_segments=32, v_segments=18, radius=g)
    for f in glb.faces:
        f.material_index = PEARL
        f.smooth = True
    _transform(glb, Matrix.Translation((x, y, gz)))
    _merge(bm, glb)
    return Vector((x, y, gz))


def leaf(bm, side):
    """A door leaf swung back against the tunnel's side: in the plane x = side * 6.36, from z 14 back to 7.85 (Unity),
    its face toward the tunnel's middle; violet boards, gold rim and rails, the crown, studs, hinges and a ring."""
    xp = side * 6.36
    out = -side
    y0, y1 = 7.85, 13.95
    h0, h1 = 0.02, 5.9
    to_leaf = Matrix(((0, 0, out, xp), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # (along, height, out) -> world

    def slab(a0, a1, b0, b1, d0, d1, mat):
        b = k.prism([(a0, b0), (a1, b0), (a1, b1), (a0, b1)], d0, d1, mat)
        _transform(b, to_leaf)
        if out < 0:
            bmesh.ops.reverse_faces(b, faces=b.faces)
        _merge(bm, b)
    boards = 10
    for i in range(boards):
        a0 = y0 + (y1 - y0) * i / boards + .012
        a1 = y0 + (y1 - y0) * (i + 1) / boards - .012
        slab(a0, a1, h0, h1, -.16, .015 * ((i * 5) % 3), VIOLET)
    for (a0, a1, b0, b1) in ((y0, y0 + .16, h0, h1), (y1 - .16, y1, h0, h1), (y0, y1, h0, h0 + .16), (y0, y1, h1 - .16, h1),
                             (y0, y1, 1.9, 2.04), (y0, y1, 3.9, 4.04)):
        slab(a0, a1, b0, b1, 0, .07, GOLD)
    crown(bm, .95, to_leaf @ Matrix.Translation(((y0 + y1) * .5, 2.95, .02)), .1)
    for (b0, b1) in ((.3, 1.75), (4.2, 5.7)):
        for i in range(5):
            for j in range(2):
                st = star_stud(.18, .035, points=4)
                _transform(st, Matrix.Rotation(-math.pi / 2, 4, "Y"))
                _transform(st, to_leaf @ Matrix.Translation((y0 + .5 + (y1 - y0 - 1.0) * i / 4, b0 + (b1 - b0) * (j + .5) / 2, .03)))
                _merge(bm, st)
    for hh in (.8, 2.95, 5.1):
        kn = k.lathe([(0, 0), (.1, 0), (.11, .03), (.11, .47), (.1, .5), (0, .5)], 14, mat=GOLD)
        _transform(kn, Matrix.Translation((xp + out * .03, y1 + .03, hh - .25)))
        _merge(bm, kn)
        strap = k.prism([(y1 - 1.1, hh - .07), (y1, hh - .07), (y1, hh + .07), (y1 - 1.1, hh + .07)], .06, .09, GOLD)
        _transform(strap, to_leaf)
        _merge(bm, strap)
    ring = torus(.18, .03, 24, 6)
    _transform(ring, Matrix.Rotation(math.pi / 2, 4, "Y"))
    _transform(ring, Matrix.Translation((xp + out * .15, y0 + .6, 2.6)))
    _merge(bm, ring)


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    col = bpy.data.collections.new("RO R1 gate")
    bpy.context.scene.collection.children.link(col)
    objs, lamps = [], []
    for name, fn, bev in (("Gate piers", lambda b: (pier(b, -1), pier(b, 1)), .012),
                          ("Gate columns", lambda b: (column(b, -COL_X), column(b, COL_X)), .008),
                          ("Gate arch", arch, .008),
                          ("Gate leaf W", lambda b: leaf(b, -1), .01), ("Gate leaf E", lambda b: leaf(b, 1), .01)):
        bm = bmesh.new()
        fn(bm)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # the wall and leaf swaps mirror some pieces
        ob = k.new_object(name, bm, col, P.SLOT_ORDER)
        k.bevel(ob, bev, 1, angle=40.0)
        k.apply_modifiers(ob)
        k.surface_uvs(ob)
        objs.append(ob)
    for s in (-1, 1):
        bm = bmesh.new()
        at = pier_lamp(bm, s * (PIER_X0 + PIER_X1) * .5, FACE_Y + .6, PIER_TOP)
        ob = k.new_object("Gate lamp %s globe" % ("W" if s < 0 else "E"), bm, col, P.SLOT_ORDER)
        k.surface_uvs(ob)
        objs.append(ob)
        lamps.append(at)
    print("[r1 gate]", len(objs), "objects,", k.tris(objs), "tris")
    for o in objs:
        print("   ", o.name, k.tris([o]))
    return col, objs, lamps


def stills(out):
    import ro_r0_star_model as r0
    r0.LAMPS[:] = LAMPS
    os.makedirs(out, exist_ok=True)
    shots = {
        "r1_gate_front": (Vector((0, 40, 6.5)), Vector((0, 14.5, 6.4)), 30),
        "r1_gate_three_quarter": (Vector((14, 27, 5)), Vector((0, 14.5, 5.8)), 30),
        "r1_gate_capital_close": (Vector((-3.5, 19.5, 5.2)), Vector((-6.75, 15.0, 5.4)), 40),
        "r1_gate_from_tunnel": (Vector((0, 2.5, 1.6)), Vector((0, 14.5, 3.2)), 24),
    }
    r0.stills([], out, shots=shots, centre=Vector((0, 0, 0)), floor_at=(0, 34, -18), fill_offset=(7, 26, 10))


LAMPS = []

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    col, objs, LAMPS[:] = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r1_gate.blend"))
    if "--export" in argv:
        k.export(col.name, MODEL, ZONE)
        k.write_lamps(ZONE, MODEL, LAMPS)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r1"))
