"""B6 the chandeliers (bh-hall session, seoungtest R838, 2026-11-25): the crown chandeliers the pawns swing on and the
great chandelier they rest on (PLAN.md 1 "B6"; BH_A09). Replaces BanquetChandelierShape's code meshes in the end.

    blender -b --factory-startup --python bh_b6_chandelier_model.py -- --part hanging

The builder's numbers kept (GrandBanquetHallBuilder.Chandeliers / GreatChandelier, BanquetChandelierShape):
  - hanging(L): its frame is the rope's pivot (origin), the rope down -z (Blender z = Unity y). The chain from the
    pivot, the chandelier the last 2.5 m (its crown cap at -(L - 2.5)), and at the very end the gilt hoop the pawn
    hangs from, its middle at -L + 0.175 (the rope's end - where it is caught). L = 6.5 (the first line) or 8.
  - great: its frame is its floor's middle (the dish's top, z 0 = the floor, radius 4.2 - walked on, a collider the
    builder adds); a column 0.55 round to 4.6 m (a collider: nothing of the model stands outside it there); the
    S-arms stay over the column's foot so the floor is clear (R836 README '볼 것': the sheet's arms stand on the dish).
Cups are empty as the sheet draws them; the candles (wax, a wick) are their own "(lit)" object, the flames Unity's.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h   # noqa: E402

h.STILLS = os.path.join(HERE, "stills", "b6")


def torus(m, sl, R, r, mat=Matrix(), segs=24, sides=8):
    """A ring of radius R (tube r) round local z, placed by `mat`."""
    mi = m.mat(sl)
    bm = m.bm
    rings = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        c, s = math.cos(a), math.sin(a)
        ring = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            p = Vector(((R + r * math.cos(b)) * c, (R + r * math.cos(b)) * s, r * math.sin(b)))
            ring.append(bm.verts.new(mat @ p))
        rings.append(ring)
    for i in range(segs):
        a, b = rings[i], rings[(i + 1) % segs]
        for j in range(sides):
            h.face(bm, (a[j], b[j], b[(j + 1) % sides], a[(j + 1) % sides]), mi, True)


def chain(m, top, bottom, link=.17, R=.07, r=.022, sl="gold"):
    """Interlocked oval links from z top down to z bottom on the axis (x 0, y 0), every other one turned."""
    z = top
    k = 0
    while z > bottom:
        rot = Matrix.Rotation(math.pi / 2, 4, "X") @ Matrix.Rotation(0, 4, "Z")
        turn = Matrix.Rotation(math.pi / 2 * (k % 2), 4, "Z")
        torus(m, sl, R, r, Matrix.Translation((0, 0, z)) @ turn @ rot @ Matrix.Diagonal((1, 1.6, 1, 1)), 14, 6)
        z -= link
        k += 1


def chain_along(m, a, b, link=.16, R=.06, r=.02, sl="gold"):
    """Links along a straight line from a to b (Vectors)."""
    d = (b - a)
    n = max(1, int(d.length / link))
    q = d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    for k in range(n + 1):
        p = a + d * (k / n)
        turn = Matrix.Rotation(math.pi / 2 * (k % 2), 4, "Z")
        torus(m, sl, R, r, Matrix.Translation(p) @ q @ turn @ Matrix.Rotation(math.pi / 2, 4, "X") @
              Matrix.Diagonal((1, 1.6, 1, 1)), 12, 6)


def cup(m, at, s=1.0):
    """A candle cup (the sheet's: a turned bowl with a rolled rim on a short knop)."""
    prof = [(0, -.1), (.035, -.1), (.05, -.07), (.03, -.04), (.055, -.01), (.08, .01), (.085, .1), (.105, .115),
            (.105, .14), (.085, .14), (.075, .03), (0, .03)]
    h.lathe(m, "gold", [(r * s, z * s) for r, z in prof], at=at, segs=16)


def candle(c, at, s=1.0):
    h.lathe(c, "wax", [(0, .03 * s), (.045 * s, .03 * s), (.045 * s, .36 * s), (.038 * s, .38 * s), (0, .375 * s)],
            at=at, segs=12)
    h.tube(c, "piece_black", [Vector(at) + Vector((0, 0, .37 * s)), Vector(at) + Vector((0, 0, .41 * s))], .005 * s, sides=5)


def drop(m, at, s=1.0):
    """A crystal drop hung from `at`: a gold eye and a faceted pear."""
    torus(m, "gold", .018 * s, .006 * s, Matrix.Translation(at) @ Matrix.Rotation(math.pi / 2, 4, "X"), 8, 4)
    h.lathe(m, "crystal", [(0, -.02 * s), (.035 * s, -.06 * s), (.055 * s, -.13 * s), (.04 * s, -.19 * s), (0, -.25 * s)],
            at=at, segs=8)


def spike(m, at, s=1.0):
    """A crown point: a little faceted spike on a ball."""
    h.ball(m, "gold", Vector(at) + Vector((0, 0, .03 * s)), .03 * s, 10, 6)
    h.lathe(m, "gold", [(0, .04 * s), (.04 * s, .07 * s), (.03 * s, .16 * s), (0, .24 * s)], at=at, segs=4)


def ring_dir(a):
    return Vector((math.cos(a), math.sin(a), 0))


def band(m, sl, R, w, hgt, z, segs=64):
    """A flat gilt band ring (the sheet's rings are bands, not round rods): radius R, w across, hgt tall, its middle
    at z; its edges chamfered."""
    c = min(w, hgt) * .18
    prof = [(R - w / 2 + c, -hgt / 2), (R + w / 2 - c, -hgt / 2), (R + w / 2, -hgt / 2 + c), (R + w / 2, hgt / 2 - c),
            (R + w / 2 - c, hgt / 2), (R - w / 2 + c, hgt / 2), (R - w / 2, hgt / 2 - c), (R - w / 2, -hgt / 2 + c),
            (R - w / 2 + c, -hgt / 2)]
    h.lathe(m, sl, prof, at=(0, 0, z), segs=segs)


def stud(m, at, out, s=1.0):
    """A gilt lozenge stud on a band's face (the sheet's diamonds)."""
    q = Vector(out).to_track_quat("Z", "Y").to_matrix() @ Matrix.Rotation(math.pi / 4, 3, "Z")
    h.lathe(m, "gold", [(0, 0), (.05 * s, .02 * s), (.035 * s, .035 * s), (0, .045 * s)], at=at, segs=4, axis=q)


def hanging(col, L=6.5, name="BH chandelier"):
    """The crown chandelier on its rope (the sheet's left one): the swivel at the pivot, a heavy chain hooked to an
    eye on the crown cap; a turned stem down to a bowl; an upper band of six cups sitting on it with drops under the
    studs; four S-straps sweeping from under the upper band out and down to the main band; the main band (2.4 m
    across) with four cups on it, eight S-arms from a collar under it out and up to their cups, crown points between,
    drops under the arms; under the bowl a neck and an eye, and through the eye the gilt hoop the pawn hangs from,
    its middle at the rope's end (-L + 0.175) - the hanging part joined to the body (the user, 11-25: "매달리는 부분
    연결부 만들어")."""
    m, c = h.Mesh(), h.Mesh()
    top = -(L - 2.5)                 # the crown cap's top (the builder's: the chandelier is the last 2.5 m)
    hz = -L + .175                   # the hoop's middle: the rope's end
    # the swivel at the pivot, its eye, the chain down to the cap's eye
    h.ball(m, "gold", Vector((0, 0, .02)), .2, 16, 10)
    torus(m, "gold", .1, .03, Matrix.Translation((0, 0, -.24)) @ Matrix.Rotation(math.pi / 2, 4, "X"), 16, 6)
    chain(m, -.4, top + .4, link=.2, R=.085, r=.03)
    torus(m, "gold", .11, .035, Matrix.Translation((0, 0, top + .27)) @ Matrix.Rotation(math.pi / 2, 4, "Y"), 18, 8)
    # the crown cap: a turned dome with a rim and a knop under its eye
    h.lathe(m, "gold", [(0, .17), (.07, .16), (.08, .1), (.15, .08), (.25, .02), (.28, -.03), (.25, -.07), (.13, -.12),
                        (0, -.13)], at=(0, 0, top), segs=24)
    # the stem: from under the cap to the bowl's top, knops, a vase, a ball (the sheet's)
    zu = top - .62                   # the upper band (the sheet: 27 % down the body)
    zl = top - 1.55                  # the main band (70 % down)
    zb = zl - .2                     # the bowl's top
    stem = [(.09, 0), (.09, -.06), (.16, -.12), (.17, -.18), (.1, -.24), (.08, -.32), (.13, -.4), (.11, -.48),
            (.07, -.52), (.1, -.58), (.2, -.72), (.25, -.86), (.22, -1.0), (.13, -1.1), (.09, -1.16), (.14, -1.22),
            (.11, -1.28), (0, -1.3)]
    k = (top - .12 - zb) / 1.3
    h.lathe(m, "gold", [(r, z * k) for r, z in reversed(stem)], at=(0, 0, top - .12), segs=28)
    # the upper tier: a band, six cups sitting on it, studs between with drops under them, four spokes to the stem
    band(m, "gold", .6, .16, .11, zu)
    for i in range(6):
        a = math.radians(i * 60)
        p = ring_dir(a) * .6 + Vector((0, 0, zu + .055))
        cup(m, p + Vector((0, 0, .1)), 1.4)
        candle(c, p + Vector((0, 0, .1)), 1.4)
        q = ring_dir(a + math.radians(30))
        stud(m, q * .69 + Vector((0, 0, zu)), q, 1.2)
        drop(m, q * .62 + Vector((0, 0, zu - .06)), 1.4)
    for i in range(4):
        a = math.radians(i * 90 + 45)
        h.tube(m, "gold", [Vector((0, 0, zu)) + ring_dir(a) * .14, Vector((0, 0, zu)) + ring_dir(a) * .53], .03, sides=8)
    # four S-straps: from the stem under the upper band, curling out, sweeping down onto the main band
    for i in range(4):
        a = math.radians(i * 90)
        d = ring_dir(a)
        pts = []
        for j in range(19):
            t = j / 18
            r = .18 + (.9 - .18) * (1 - math.cos(math.pi * t)) / 2 + .12 * math.sin(math.pi * t)
            z = zu - .14 - (zl + .09 - (zu - .14)) * -t
            z = zu - .14 + (zl + .09 - (zu - .14)) * t ** .8
            pts.append(d * r + Vector((0, 0, z)))
        h.tube(m, "gold", pts, .045, sides=10)
        h.spiral(m, "gold", d * .18 + Vector((0, 0, zu - .24)), d, (0, 0, 1), .1, .02, 1.3, .035, .02, start=math.pi / 2)
    # the main tier: the band (2.4 m across) and a thinner band under it, studs, four cups on it, eight S-arms from a
    # collar under it out and up to their cups, crown points between, drops under the arms
    R = 1.0                          # (the code mesh had 1.2: narrower, as tall as the sheet's, and clearer of a neighbour at a handover)
    band(m, "gold", R, .2, .17, zl)
    band(m, "gold", R - .03, .14, .07, zl - .13)
    for i in range(16):
        q = ring_dir(math.radians(i * 22.5 + 11.25))
        stud(m, q * (R + .1) + Vector((0, 0, zl)), q, 1.3)
    for i in range(4):
        a = math.radians(i * 90)
        p = ring_dir(a) * R + Vector((0, 0, zl + .085))
        cup(m, p + Vector((0, 0, .1)), 1.5)
        candle(c, p + Vector((0, 0, .1)), 1.5)
    h.lathe(m, "gold", [(0, .06), (.2, .05), (.25, -.02), (.2, -.09), (0, -.1)], at=(0, 0, zl - .1), segs=24)
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        d = ring_dir(a)
        pts = []
        for j in range(17):
            t = j / 16
            out = .2 + (R + .28 - .2) * t
            up = -.1 - .17 * math.sin(math.pi * t) + .55 * t ** 2.2
            pts.append(d * out + Vector((0, 0, zl + up)))
        h.tube(m, "gold", pts, .055, sides=10)
        tip = pts[-1]
        h.lathe(m, "gold", [(0, -.03), (.07, -.03), (.08, .03), (.05, .08), (0, .09)], at=tip, segs=14)
        cup(m, tip + Vector((0, 0, .19)), 1.5)
        candle(c, tip + Vector((0, 0, .19)), 1.5)
        drop(m, d * (R + .12) + Vector((0, 0, zl - .2)), 1.5)
        spike(m, ring_dir(a + math.radians(22.5)) * R + Vector((0, 0, zl + .085)), 1.5) if (i % 2 == 0) else None
        h.spiral(m, "gold", d * .34 + Vector((0, 0, zl - .26)), d, (0, 0, 1), .08, .02, 1.2, .026, .014)
    # under the bowl: the neck, a collar, the eye; through the eye the hoop (its top passes the eye's middle)
    bowl = [(0, 0), (.32, -.02), (.36, -.08), (.3, -.18), (.2, -.26), (.12, -.3), (.1, -.36), (.14, -.4), (.1, -.44),
            (.07, -.47), (0, -.48)]
    h.lathe(m, "gold", [(r, z * .5) for r, z in bowl], at=(0, 0, zb), segs=28)
    eye_z = hz + .22
    h.tube(m, "gold", [Vector((0, 0, zb - .23)), Vector((0, 0, eye_z + .1))], .05, sides=12)
    torus(m, "gold", .085, .03, Matrix.Translation((0, 0, eye_z)) @ Matrix.Rotation(math.pi / 2, 4, "Y"), 18, 8)
    torus(m, "gold", .22, .055, Matrix.Translation((0, 0, hz)) @ Matrix.Rotation(math.pi / 2, 4, "X"), 28, 10)
    return [m.finish(name + " %.1f" % L, col, bevel=0, recalc=False),
            c.finish(name + " %.1f candles (lit)" % L, col, bevel=0)]


GREAT_R, GREAT_COL, GREAT_TOP = 4.2, .55, 4.6     # BanquetChandelierShape: GreatDish, GreatColumn, GreatColumnTop


def great(col, chain_up=11.6, name="BH great chandelier"):
    """The great chandelier (the sheet's middle one) the pawns rest on: a gilt dish whose top is the floor (radius
    4.2, olive velvet carpet with a gold edge), its rim a heavy band with lozenge plaques, 24 cups on it, drops under
    it, its underside a shallow bowl to a crystal pendant; the column (inside the builder's collider: radius 0.55 to
    4.6 m) with the crown on top, four S-legs gathered round the column (R836 README: the sheet's legs stand out on
    the dish, which is walked on - here they stay within 0.85 m of the middle, scrolled feet on the floor), four
    chains from the crown to the rim, one up to the ceiling (chain_up)."""
    m, c = h.Mesh(), h.Mesh()
    R = GREAT_R
    # the dish: flush top to the rim, the rim band, the underside
    prof = [(0, -1.12), (.5, -1.08), (1.3, -.98), (2.6, -.8), (3.6, -.7), (4.25, -.72), (4.45, -.68), (4.55, -.58),
            (4.55, .1), (4.48, .2), (4.1, .2), (4.06, .02), (4.0, 0), (0, 0)]
    h.lathe(m, "gold", prof, at=(0, 0, 0), segs=96)
    band(m, "gold", 4.55, .1, .12, -.55, segs=96)
    band(m, "gold", 4.55, .1, .08, .1, segs=96)
    # the carpet (olive velvet) and its gold edge, a hair over the floor
    h.lathe(m, "olive", [(0, .005), (3.98, .005), (3.98, .015), (0, .015)], at=(0, 0, 0), segs=96)
    torus(m, "gold", 3.98, .025, Matrix.Translation((0, 0, .015)), 96, 6)
    # lozenge plaques on the rim, cups on it, drops under it
    for i in range(24):
        a = math.radians(i * 15 + 7.5)
        d = ring_dir(a)
        if i % 3 == 0:
            q = d.to_track_quat("Z", "Y").to_matrix()
            h.lathe(m, "gold", [(0, 0), (.34, 0), (.34, .06), (.28, .09), (0, .09)], at=d * 4.55 + Vector((0, 0, -.22)), segs=4,
                    axis=q @ Matrix.Rotation(math.pi / 4, 3, "Z"))
            stud(m, d * 4.63 + Vector((0, 0, -.22)), d, 3.4)
        else:
            stud(m, d * 4.55 + Vector((0, 0, -.22)), d, 1.6)
        p = d * 4.3 + Vector((0, 0, .2))
        # a squat ring cup sitting on the rim (the sheet's)
        h.lathe(m, "gold", [(0, 0), (.15, 0), (.17, .03), (.17, .13), (.19, .15), (.19, .19), (.15, .19), (.14, .06), (0, .06)],
                at=p, segs=16)
        candle(c, p + Vector((0, 0, .03)), 1.6)
        drop(m, d * 4.5 + Vector((0, 0, -.72)), 2.6)
    # the pendant under the bowl
    h.lathe(m, "gold", [(0, 0), (.4, -.02), (.42, -.1), (.3, -.18), (.12, -.24), (0, -.26)], at=(0, 0, -1.1), segs=32)
    h.lathe(m, "crystal", [(0, 0), (.32, -.2), (.26, -.55), (.12, -.95), (.18, -1.15), (0, -1.5)], at=(0, 0, -1.34), segs=16)
    # the column, inside the collider (r 0.55, to 4.6)
    colp = [(.55, 0), (.55, .12), (.42, .2), (.36, .5), (.4, .8), (.5, 1.1), (.52, 1.4), (.44, 1.72), (.26, 2.0),
            (.22, 2.6), (.34, 2.8), (.2, 3.0), (.18, 3.6), (.3, 3.8), (.16, 4.1), (.08, 4.48), (0, 4.6)]
    h.lathe(m, "gold", colp, at=(0, 0, 0), segs=32)
    # The S-legs, spread as the sheet draws them (the user, 11-25: "큰 샹들리에는 지금 시트는 s 자로 퍼져 있는데 넌
    # 가운데에 모여 있어 끝 부분 처리도 디테일 살려"): four flat gilt straps from a scroll curled in under the crown,
    # an S out and down onto the dish halfway to its rim, a big scrolled foot curling outward on a little plinth, an
    # acanthus leaf on each knee, a bead down each edge. Pictures only (no collider): a pawn on the dish walks through
    # them (R836 README had them gathered at the column; the user chose the sheet's look).
    def bez(p0, p1, p2, p3, t):
        u = 1 - t
        return p0 * u ** 3 + p1 * 3 * u * u * t + p2 * 3 * u * t * t + p3 * t ** 3

    for i in range(4):
        a = math.radians(i * 90 + 45)
        d = ring_dir(a)
        side = ring_dir(a + math.pi / 2)
        P = lambda r, z: d * r + Vector((0, 0, z))
        ctrl = (P(1.25, GREAT_TOP - .15), P(.55, 2.9), P(2.9, 2.2), P(2.4, .55))
        pts = [bez(*ctrl, j / 28) for j in range(29)]
        widths = [.5 - .14 * (j / 28) + .06 * math.sin(math.pi * j / 28) for j in range(29)]
        h.blade(m, "gold", pts, widths, d, thick=.24, sides=12, side=side)
        # a bead down each edge of the strap
        for sg in (-1, 1):
            edge = [pts[j] + side * sg * (widths[j] / 2 - .02) + (pts[min(j + 1, 28)] - pts[max(j - 1, 0)]).normalized().cross(side) * -.08
                    for j in range(2, 27)]
            h.tube(m, "gold", edge, .03, sides=6)
        # the top scroll: curled in under the crown
        top = pts[0]
        h.spiral(m, "gold", top + d * -.18 + Vector((0, 0, -.05)), -d, (0, 0, 1), .2, .04, 1.5, .1, .05, start=0)
        h.spiral(m, "gold", top + d * -.18 + side * .12 + Vector((0, 0, -.05)), -d, (0, 0, 1), .16, .04, 1.3, .05, .03, start=0)
        h.spiral(m, "gold", top + d * -.18 - side * .12 + Vector((0, 0, -.05)), -d, (0, 0, 1), .16, .04, 1.3, .05, .03, start=0)
        # the foot: a big scroll curling outward from the strap's end, two side scrolls, on a moulded plinth
        foot = pts[-1]
        fc = foot + d * .3 + Vector((0, 0, -.1))
        h.spiral(m, "gold", fc, d, (0, 0, 1), .32, .05, 1.6, .13, .05, start=math.pi)
        for sg in (-1, 1):
            h.spiral(m, "gold", fc + side * sg * .14, d, (0, 0, 1), .26, .04, 1.4, .06, .03, start=math.pi)
        h.ball(m, "gold", fc, .07, 12, 8)
        pc = foot + d * .25
        h.lathe(m, "gold", [(0, 0), (.36, 0), (.36, .05), (.3, .08), (.3, .15), (.26, .18), (0, .18)],
                at=(pc.x, pc.y, 0), segs=4, axis=Matrix.Rotation(a + math.pi / 4, 3, "Z"))
        # the knee: an acanthus leaf on the strap's outer curve, pointing down the leg
        kj = 15
        knee = pts[kj]
        tang = (pts[kj + 1] - pts[kj - 1]).normalized()
        outn = side.cross(tang).normalized()
        if outn.dot(d) < 0:
            outn = -outn
        h.broad_leaf(m, "gold", knee - tang * .05 + outn * .08, -tang, outn, .26, .5, curl=.5, bulge=.25, lobes=3,
                     thick=.06, sides=8)
        h.broad_leaf(m, "gold", knee + tang * .05 + outn * .08, tang, outn, .2, .35, curl=.5, bulge=.25, lobes=2,
                     thick=.05, sides=8)
    # The crown on top of the column (the user, 11-25: "왕관 부분도 디테일 살려"): a band between two rolled beads, a
    # lozenge jewel in a raised setting under each great point and a small one between; eight great points - curved
    # concave sides, a raised rim round each, a lozenge set in it, a big ball on a collar at the tip - and eight small
    # points between with small balls; inside, a domed cap and the finial the ceiling chain hangs from.
    zc = GREAT_TOP
    CR = 1.35
    bt, bh_ = zc, zc + .5                      # the band's foot and top
    band(m, "gold", CR, .16, .5, zc + .25, segs=72)
    torus(m, "gold", CR + .08, .07, Matrix.Translation((0, 0, bt)), 72, 10)
    torus(m, "gold", CR + .08, .05, Matrix.Translation((0, 0, bh_)), 72, 8)
    h.lathe(m, "gold", [(0, -.08), (CR, -.08), (CR, .0), (0, .0)], at=(0, 0, zc), segs=72)
    # the domed cap inside, and the finial
    h.lathe(m, "gold", [(0, .95), (.12, .93), (.16, .86), (.12, .8), (.3, .72), (.7, .55), (1.05, .4), (1.25, .3),
                        (CR - .05, .25), (0, .25)], at=(0, 0, zc), segs=48)

    def point(a, w, hgt, tip_r, thick, inset):
        """A crown point on the band's top at angle a: concave sides, a raised rim, an inset lozenge, a ball tip."""
        d = ring_dir(a)
        side = ring_dir(a + math.pi / 2)
        base = d * CR + Vector((0, 0, bh_))
        n = 10                                 # the left side, foot to tip, bowed in (concave)
        outline = [(-w / 2 * (1 - t) + w * .14 * math.sin(math.pi * t) * (1 - t), hgt * t) for t in [k / n for k in range(n + 1)]]
        right = [(-x, z) for x, z in reversed(outline[:-1])]
        poly = outline + right
        mi = m.mat("gold")
        bm = m.bm
        f0 = [bm.verts.new(base + side * x + Vector((0, 0, z)) - d * thick) for x, z in poly]
        f1 = [bm.verts.new(base + side * x + Vector((0, 0, z)) + d * thick) for x, z in poly]
        edges = [bm.edges.new((f1[k], f1[(k + 1) % len(f1)])) for k in range(len(f1))]
        got = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=d)
        for f in got["geom"]:
            if isinstance(f, bmesh.types.BMFace):
                f.material_index = mi
                f.normal_update()
                if f.normal.dot(d) < 0:
                    f.normal_flip()
        edges = [bm.edges.new((f0[k], f0[(k + 1) % len(f0)])) for k in range(len(f0))]
        got = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=-d)
        for f in got["geom"]:
            if isinstance(f, bmesh.types.BMFace):
                f.material_index = mi
                f.normal_update()
                if f.normal.dot(d) > 0:
                    f.normal_flip()
        for k in range(len(poly)):
            h.face(bm, (f0[k], f0[(k + 1) % len(poly)], f1[(k + 1) % len(poly)], f1[k]), mi)
        # the raised rim round its face and the lozenge set in it
        rim = [base + side * x * .86 + Vector((0, 0, z * .92 + .03)) + d * (thick + .02) for x, z in poly] + \
              [base + side * poly[0][0] * .86 + Vector((0, 0, .03)) + d * (thick + .02)]
        h.tube(m, "gold", rim, .025, sides=6, cap=False)
        if inset:
            stud(m, base + Vector((0, 0, hgt * .36)) + d * (thick + .01), d, inset)
        # the collar and the ball at its tip
        tip = base + Vector((0, 0, hgt))
        h.lathe(m, "gold", [(0, 0), (tip_r * .55, .0), (tip_r * .7, .04), (tip_r * .5, .08), (0, .08)], at=tip - Vector((0, 0, .04)), segs=12)
        h.ball(m, "gold", tip + Vector((0, 0, tip_r * .9)), tip_r, 16, 10)

    for i in range(8):
        a = math.radians(i * 45)
        point(a, .62, .7, .13, .05, 3.0)
        point(a + math.radians(22.5), .34, .36, .075, .04, 0)
        d = ring_dir(a)
        # the jewel under each great point: a lozenge in a round raised setting
        h.lathe(m, "gold", [(0, 0), (.14, 0), (.15, .03), (.12, .05), (0, .05)], at=d * (CR + .08) + Vector((0, 0, zc + .25)),
                segs=20, axis=d.to_track_quat("Z", "Y").to_matrix())
        stud(m, d * (CR + .13) + Vector((0, 0, zc + .25)), d, 2.4)
        q = ring_dir(a + math.radians(22.5))
        stud(m, q * (CR + .08) + Vector((0, 0, zc + .25)), q, 1.4)
    # four chains from the crown to the rim (along the hall's axes, off the rope lines), one up to the ceiling
    for i in range(4):
        d = ring_dir(math.radians(i * 90))
        chain_along(m, d * (CR + .05) + Vector((0, 0, zc + .2)), d * 4.3 + Vector((0, 0, .28)), link=.24, R=.1, r=.034)
        torus(m, "gold", .1, .03, Matrix.Translation(d * 4.3 + Vector((0, 0, .2))) @
              Matrix.Rotation(math.radians(i * 90), 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X"), 16, 6)
    h.lathe(m, "gold", [(0, 0), (.2, 0), (.22, .08), (.12, .16), (0, .2)], at=(0, 0, zc), segs=20)
    chain(m, zc + chain_up, zc + 1.0, link=.22, R=.1, r=.035)        # from the cap's finial up
    return [m.finish(name, col, bevel=0), c.finish(name + " candles (lit)", col, bevel=0)]


def great_part(col):
    return great(col, chain_up=2.0)


def hanging_part(col):
    objs = hanging(col, 6.5)
    for o in objs:          # the stills stand it on the ground (its frame is the pivot, 6.5 m up)
        o.location.z += 6.5
    return objs


h.CLOSE.update({
    "great": ((0, 0, 5.1), (-.4, -1, .35), 4.2, 40),
    "hanging": ((0, 0, .65), (.8, 1, .1), 2.2, 40),
})
h.PARTS.update({
    "great": (great_part, "BH_A09_chandeliers.png", (450, 110, 1160, 830), None),
    "hanging": (hanging_part, "BH_A09_chandeliers.png", (20, 200, 440, 820), None),
})


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    name = args[args.index("--part") + 1] if "--part" in args else "hanging"
    col = bpy.data.collections.new("BH B6 " + name)
    bpy.context.scene.collection.children.link(col)
    objs = h.PARTS[name][0](col)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b6_chandelier_%s.blend" % name))
    h.part_stills(name, objs)
