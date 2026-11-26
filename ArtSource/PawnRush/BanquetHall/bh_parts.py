"""Grand banquet hall common parts (B0, seoungtest R837, 2026-11-25): the pieces every zone uses, so the gold, the
bevels and the names are the same everywhere (PLAN.md B0 "공통 부품 키트"). Shapes follow the R836 sheets' cells.

    import sys; sys.path.insert(0, r"<this folder>"); import bh_kit as k, bh_parts as parts

Every maker returns a bmesh built round its own origin (Blender axes, z up, metres), faces' material_index into
SLOT_ORDER; obj(col, name, bm, matrix) makes the object with those slots, finish(ob) bevels and lays metre UVs.

  plate(r, h)                 BH_A04 plate: white china, a broad gold rim, a gold line inside it (top at h)
  goblet(h)                   BH_A04 goblet: tulip bowl, a band of emerald cabochons, knopped stem, stepped foot
  candelabra(h, candles)      BH_A04 five-branch candelabrum: turned column, two S-arm pairs, five drip cups
                              (candles=True puts cream candles with wicks in them; flames are Unity's)
  candle(h, r)                a candle: wax body, a little melted lip, black wick
  chair(h)                    BH_A04 guest's chair: walnut frame, olive velvet seat and back in gold beads, a gold
                              shell crest, gold-capped feet (h = the back's top; the seat at 0.42 h)
  sweep(points, profile)      a moulding: a 2D profile (across, up) swept along a polyline (gold trims, hems)
  fleur(size)                 a fleur-de-lis outline (x, y), for borders
  tube(points, r)             a round tube along a polyline (arms, chains)
  torus(R, r)
"""
import math
import bmesh, bpy
from mathutils import Matrix, Vector

import bh_kit as k

SLOT_ORDER = ("gold", "walnut", "linen", "runner", "olive", "china", "emerald", "wax", "silver", "marble", "stone",
              "velvet", "crystal", "piece_white", "piece_black", "damask")
GOLD, WALNUT, LINEN, RUNNER, OLIVE, CHINA, EMERALD, WAX, SILVER, MARBLE, STONE, VELVET, CRYSTAL, IVORY, EBONY, DAMASK = range(16)
WICK = EBONY   # the candle wick: the black pieces' ebony


def obj(col, name, bm, matrix=None):
    ob = k.new_object(name, bm, col, SLOT_ORDER)
    if matrix is not None:
        ob.matrix_world = matrix
    return ob


def finish(ob, bevel=None, uvs=True, segments=2):
    if bevel:
        k.bevel(ob, bevel, segments)
        k.apply_modifiers(ob)
    if uvs:
        k.surface_uvs(ob)
    # drop the slots no face uses (the export checks every slot)
    used = {p.material_index for p in ob.data.polygons}
    mats = ob.data.materials
    remap, keep = {}, []
    for i in range(len(mats)):
        if i in used:
            remap[i] = len(keep)
            keep.append(mats[i])
    old = [p.material_index for p in ob.data.polygons]
    mats.clear()            # (this resets every face's index to 0: set them after)
    for m in keep:
        mats.append(m)
    for p, i in zip(ob.data.polygons, old):
        p.material_index = remap[i]
    return ob


def transform(bm, m, verts=None):
    bmesh.ops.transform(bm, matrix=m, verts=verts if verts is not None else bm.verts)
    return bm


def merge(dst, src):
    """Append bmesh src into dst (material indices kept), then free src."""
    me = bpy.data.meshes.new("_tmp")
    src.to_mesh(me)
    src.free()
    dst.from_mesh(me)
    bpy.data.meshes.remove(me)
    return dst


def part(bm_list):
    out = bmesh.new()
    for b in bm_list:
        merge(out, b)
    return out


def torus(R, r, segs=48, rsegs=8, mat=GOLD):
    prof = [(R + r * math.cos(2 * math.pi * i / rsegs), r * math.sin(2 * math.pi * i / rsegs)) for i in range(rsegs + 1)]
    return k.lathe(prof, segs, mat=mat)


def tube(points, r, segs=8, mat=GOLD, caps=True, radii=None):
    """A round tube along a polyline (Blender points); radii: one radius a point (tapers)."""
    bm = bmesh.new()
    rings = []
    prev_side = None
    for i, p in enumerate(points):
        a = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        a.normalize()
        side = a.cross(Vector((0, 0, 1))) if prev_side is None else prev_side - a * prev_side.dot(a)
        if side.length < 1e-4:
            side = a.cross(Vector((1, 0, 0)))
        side.normalize()
        prev_side = side
        up = side.cross(a)
        rr = radii[i] if radii else r
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * j / segs) + up * math.sin(2 * math.pi * j / segs)) * rr)
                      for j in range(segs)])
    for i in range(len(rings) - 1):
        for j in range(segs):
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]))
            f.material_index = mat
            f.smooth = True
    if caps:
        for ring in (rings[0], rings[-1]):
            f = bm.faces.new(ring)
            f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def curve(ctrl, steps=12):
    """Points on a Catmull-Rom curve through ctrl (Vectors)."""
    pts = []
    c = [ctrl[0]] + list(ctrl) + [ctrl[-1]]
    for i in range(1, len(c) - 2):
        p0, p1, p2, p3 = c[i - 1], c[i], c[i + 1], c[i + 2]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            pts.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    pts.append(ctrl[-1].copy())
    return pts


def box(lo, hi, mat):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    lo, hi = Vector(lo), Vector(hi)
    transform(bm, Matrix.Translation((lo + hi) / 2) @ Matrix.Diagonal((*(hi - lo), 1)))
    for f in bm.faces:
        f.material_index = mat
    return bm


def sweep(points, profile, mat=GOLD, closed=False, up=Vector((0, 0, 1))):
    """A moulding: profile = [(across, up), ...] (a closed section, counter-clockwise) swept along the polyline points;
    'across' is to the left of the direction of travel, 'up' along `up` (kept square to the path)."""
    bm = bmesh.new()
    rings = []
    n = len(points)
    for i, p in enumerate(points):
        if closed:
            a = points[(i + 1) % n] - points[(i - 1) % n]
        else:
            a = points[min(i + 1, n - 1)] - points[max(i - 1, 0)]
        a.normalize()
        left = Vector(up).cross(a)
        left.normalize()
        u = a.cross(left)
        # mitre: widen at corners
        scale = 1.0
        if 0 < i < n - 1 or closed:
            a0 = (p - points[(i - 1) % n]).normalized()
            a1 = (points[(i + 1) % n] - p).normalized()
            c = max(.3, (a0 + a1).normalized().dot(a1)) if (a0 + a1).length > 1e-6 else 1.0
            scale = 1.0 / c
        rings.append([bm.verts.new(p + left * x * scale + u * y) for x, y in profile])
    m = len(profile)
    last = n if closed else n - 1
    for i in range(last):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for j in range(m):
            f = bm.faces.new((r0[j], r0[(j + 1) % m], r1[(j + 1) % m], r1[j]))
            f.material_index = mat
    if not closed:
        for ring in (rings[0], list(reversed(rings[-1]))):
            try:
                bm.faces.new(ring).material_index = mat
            except ValueError:
                pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def ogee(w, h, steps=4):
    """A gilded ogee moulding's section (across 0..w out from the face, up 0..h), closed back to the face."""
    pts = [(0.0, 0.0), (w * .55, 0.0), (w * .6, h * .12)]
    for i in range(steps + 1):
        t = i / steps
        pts.append((w * (.6 + .4 * math.sin(t * math.pi)), h * (.12 + .5 * t)))
    pts += [(w * .7, h * .7), (w * .9, h * .78), (w, h * .88), (w * .9, h), (0.0, h)]
    return pts


def fleur(size=1.0, t=.03, mat=GOLD):
    """A gilded fleur-de-lis in the x-y plane (y up, its foot at y 0, `size` tall), raised t along +z: the tall middle
    petal, two petals curling out and down, the band across them, the three-lobed foot."""
    s = size
    bm = bmesh.new()

    def add(outline):
        pts = [(x * s, y * s) for x, y in outline]
        k.prism(pts, 0.0, t, mat=mat, bm=bm)
    # middle petal: a pointed teardrop from the band up
    add([(0.0, .40), (.10, .48), (.13, .62), (.11, .78), (.05, .92), (0.0, 1.0), (-.05, .92), (-.11, .78), (-.13, .62), (-.10, .48)])
    for side in (-1, 1):
        add([(side * x, y) for x, y in ((.06, .42), (.16, .50), (.30, .62), (.40, .60), (.42, .50), (.36, .40), (.30, .42),
                                        (.33, .50), (.27, .53), (.18, .46), (.10, .38))][::side])
        add([(side * x, y) for x, y in ((.05, .30), (.18, .22), (.24, .10), (.20, .04), (.14, .10), (.10, .18), (.03, .24))][::side])
    add([(-.20, .33), (.20, .33), (.22, .40), (-.22, .40)])        # the band
    add([(-.05, .0), (.05, .0), (.06, .33), (-.06, .33)])           # the foot's stem
    return bm


# ------------------------------------------------------------------ table ware (BH_A04)

def plate(r=1.2, h=.12, segs=48):
    """BH_A04's plate: white china, the well (0.62 r) a step down, a flat white marley out to 0.85 r, the broad gold
    rim from there, rising to a thick gold edge; a foot ring under it. Top at h (the graybox plate's collider top),
    nothing above it."""
    prof = [(0.0, h * .34), (r * .58, h * .34), (r * .62, h * .40), (r * .64, h * .62), (r * .66, h * .66),
            (r * .845, h * .70), (r * .85, h * .72), (r * .97, h * .92), (r * .99, h), (r, h * .96), (r * 1.0, h * .62),
            (r * .97, h * .50), (r * .90, h * .34), (r * .62, h * .14), (r * .58, 0.0), (r * .52, 0.0), (r * .50, h * .10),
            (0.0, h * .14)]
    mats = [CHINA, CHINA, CHINA, CHINA, CHINA, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, CHINA, CHINA, CHINA, CHINA, CHINA]
    return k.lathe(prof, segs, mat=mats)


def goblet(h=1.3, gems=14, segs=40):
    """BH_A04's goblet (measured off the sheet, height 1): a stepped foot (r 0.216), a lower knop, a waist, a big middle
    knop, a collar under the bowl; the bowl's rounded foot, a raised band of oval emerald cabochons in gold bezels
    between two fillets, a straight upper bowl and a thick rolled lip. h = its height."""
    s = h
    outer = [(0.0, 0.0), (.216, 0.0), (.218, .015), (.212, .045), (.205, .06), (.175, .07), (.17, .085), (.15, .10),
             (.15, .12), (.12, .14), (.085, .165), (.062, .19), (.07, .215), (.075, .24), (.066, .265), (.05, .29),
             (.048, .31), (.06, .33), (.083, .35), (.087, .37), (.075, .395), (.05, .41), (.047, .43), (.068, .455),
             (.07, .47), (.1, .50), (.15, .525), (.19, .56), (.212, .60), (.22, .615),
             (.228, .62), (.228, .633), (.22, .638), (.22, .737), (.228, .742), (.228, .755), (.22, .76),
             (.214, .80), (.214, .87), (.222, .875), (.224, .89), (.218, .895), (.222, .94), (.234, .965), (.238, .985),
             (.232, 1.0), (.214, 1.0), (.208, .97), (.204, .90), (.2, .80), (.195, .70), (.17, .62), (.10, .575), (0.0, .565)]
    bm = k.lathe([(r * s, z * s) for r, z in outer], segs, mat=GOLD)
    # the cabochons: tall ovals round the band, each in a raised bezel. Local x along the band, y up, z out.
    for i in range(gems):
        t = 2 * math.pi * (i + .5) / gems
        tangent, radial = Vector((-math.sin(t), math.cos(t), 0)), Vector((math.cos(t), math.sin(t), 0))
        frame = Matrix((tangent, Vector((0, 0, 1)), radial)).transposed().to_4x4()
        frame.translation = radial * (.222 * s) + Vector((0, 0, .688 * s))
        g = bmesh.new()
        bmesh.ops.create_uvsphere(g, u_segments=8, v_segments=6, radius=1.0)
        transform(g, frame @ Matrix.Diagonal((.03 * s, .042 * s, .02 * s, 1)))
        for f in g.faces:   # flat facets: a cut stone
            f.material_index = EMERALD
            f.smooth = False
        merge(bm, g)
        bz = torus(1.0, .14, 16, 6, GOLD)
        transform(bz, frame @ Matrix.Diagonal((.034 * s, .047 * s, .03 * s, 1)))
        merge(bm, bz)
    return bm


def candle(h=.5, r=.05, segs=12):
    """A cream candle: body, a slightly melted shoulder, a black wick (the flame is Unity's)."""
    prof = [(0.0, 0.0), (r, 0.0), (r, h * .94), (r * .92, h), (r * .45, h * .985), (0.0, h * .975)]
    bm = k.lathe(prof, segs, mat=WAX)
    w = k.lathe([(0.0, h * .97), (r * .09, h * .97), (r * .08, h + r * .9), (0.0, h + r * 1.0)], 6, mat=WICK)
    merge(bm, w)
    return bm


def cup(r, segs=20):
    """A candle cup (BH_A04): a wide shallow bowl flaring to a rolled lip of radius r, on a short collar (z 0 = its
    foot). Open: the candle stands in it."""
    prof = [(0.0, 0.0), (r * .32, 0.0), (r * .34, r * .12), (r * .28, r * .2), (r * .36, r * .28), (r * .62, r * .42),
            (r * .86, r * .62), (r * .97, r * .78), (r, r * .86), (r * .96, r * .92), (r * .88, r * .86), (r * .7, r * .66),
            (r * .45, r * .52), (0.0, r * .48)]
    return k.lathe(prof, segs, mat=GOLD)


def candelabra(h=3.0, candles=True, segs=24):
    """BH_A04's five-branch candelabrum (measured off the sheet, height 1), in the x-z plane: a two-step round foot
    (r 0.19), a turned baluster column with knops, the top cup at 0.9; an inner pair of arms out from a collar at 0.64
    and up to cups at x +-0.17, z 0.77; an outer pair in S-curves from 0.57, dipping to 0.52, out to cups at x +-0.29,
    z 0.65, a scroll curl under each where it leaves the column. Returns (bmesh, the candle wicks' tops)."""
    s = h
    col = [(0.0, 0.0), (.19, 0.0), (.195, .012), (.19, .028), (.17, .036), (.165, .05), (.13, .06), (.12, .075),
           (.09, .09), (.06, .11), (.045, .14), (.05, .17), (.062, .20), (.065, .23), (.055, .26), (.04, .28),
           (.034, .32), (.036, .38), (.045, .42), (.058, .45), (.062, .47), (.05, .50), (.04, .52), (.055, .545),
           (.06, .575), (.045, .595), (.04, .62), (.052, .635), (.052, .655), (.04, .665), (.048, .70), (.05, .73),
           (.045, .76), (.05, .79), (.042, .83), (.03, .855), (.028, .87), (.04, .885), (.025, .9), (0.0, .9)]
    bm = k.lathe([(r * s, z * s) for r, z in col], segs, mat=GOLD)
    cups = [Vector((0, 0, .9 * s))]
    for side in (-1, 1):
        c = [Vector((0, 0, .645 * s)), Vector((side * .05 * s, 0, .64 * s)), Vector((side * .12 * s, 0, .65 * s)),
             Vector((side * .165 * s, 0, .70 * s)), Vector((side * .17 * s, 0, .765 * s))]
        merge(bm, tube(curve(c, 8), .016 * s, 10))
        cups.append(Vector((side * .17 * s, 0, .765 * s)))
        c = [Vector((0, 0, .565 * s)), Vector((side * .08 * s, 0, .548 * s)), Vector((side * .17 * s, 0, .515 * s)),
             Vector((side * .245 * s, 0, .53 * s)), Vector((side * .285 * s, 0, .585 * s)), Vector((side * .29 * s, 0, .645 * s))]
        merge(bm, tube(curve(c, 8), .017 * s, 10))
        cups.append(Vector((side * .29 * s, 0, .645 * s)))
        cc = [Vector((side * .085 * s, 0, .545 * s)), Vector((side * .075 * s, 0, .505 * s)), Vector((side * .095 * s, 0, .487 * s)),
              Vector((side * .115 * s, 0, .50 * s)), Vector((side * .105 * s, 0, .515 * s))]
        merge(bm, tube(curve(cc, 6), .011 * s, 8))
    wicks = []
    for i, at in enumerate(cups):
        if i:   # a collar under each arm's cup
            merge(bm, transform(k.lathe([(0.0, -.01 * s), (.03 * s, -.01 * s), (.034 * s, .005 * s), (.025 * s, .02 * s),
                                         (0.0, .02 * s)], 14, mat=GOLD), Matrix.Translation(at)))
        cp = cup(.092 * s)
        transform(cp, Matrix.Translation(at + Vector((0, 0, .015 * s))))
        merge(bm, cp)
        if candles:
            cd = candle(.16 * s, .028 * s)
            transform(cd, Matrix.Translation(at + Vector((0, 0, .055 * s))))
            merge(bm, cd)
            wicks.append(at + Vector((0, 0, (.055 + .16) * s + .028 * s)))
    return bm, wicks


# ------------------------------------------------------------------ the guest's chair (BH_A04)

def chair(h=5.4, w=2.6, d=2.3, seat=None):
    """BH_A04's chair (measured off the sheet, its crest's top = h), facing +x (its back at x = -d/2). Returns
    (hard, soft): hard = the walnut frame - square back posts raking out a little to square capped tops with a gold
    band, an arched top rail, a gold shell-and-scroll crest, a seat apron, turned front legs with a gold collar and gold
    sabots, H stretchers; soft = the padded olive seat and the padded arched back panel (bevel them round), each in a
    gold bead (in hard)."""
    seat = seat or .45 * h
    hw, hd = w / 2, d / 2
    post = .075 * h
    hard, soft = bmesh.new(), bmesh.new()
    top = .86 * h
    back_x = -hd + post / 2
    # back posts: from the floor to their caps, raking out sideways from the seat up
    for side in (-1, 1):
        y0 = side * (hw - post / 2)
        b = box((-hd, y0 - post / 2, 0), (-hd + post, y0 + post / 2, top), WALNUT)
        for v in b.verts:
            if v.co.z > seat:
                v.co.y += side * .012 * h * (v.co.z - seat) / (top - seat)
                v.co.x -= .02 * h * (v.co.z - seat) / (top - seat)
        merge(hard, b)
        yt, xt = y0 + side * .012 * h, -hd + post / 2 - .02 * h
        merge(hard, box((xt - post * .62, yt - post * .62, top - .045 * h), (xt + post * .62, yt + post * .62, top - .03 * h), GOLD))
        merge(hard, box((xt - post * .7, yt - post * .7, top - .03 * h), (xt + post * .7, yt + post * .7, top + .02 * h), WALNUT))
        merge(hard, box((xt - post * .58, yt - post * .58, top + .02 * h), (xt + post * .58, yt + post * .58, top + .035 * h), WALNUT))
        merge(hard, box((-hd - .005 * h, y0 - post * .56, 0), (-hd + post + .005 * h, y0 + post * .56, .05 * h), GOLD))
    # front legs: turned (lathe), wide under the apron, waisted, flaring a little to a gold sabot
    leg = [(0.0, 0.0), (.05, 0.0), (.052, .05), (.044, .055), (.038, .09), (.034, .14), (.04, .2), (.052, .25), (.062, .29),
           (.064, .31), (0.0, .31)]
    for side in (-1, 1):
        at = Vector((hd - .05 * h, side * (hw - .05 * h), 0))
        mats = [GOLD, GOLD] + [WALNUT] * 7 + [WALNUT]
        lg = k.lathe([(r * h, z * h) for r, z in leg], 4, mat=mats, start=45.0, smooth=False)
        merge(hard, transform(lg, Matrix.Translation(at)))
        merge(hard, transform(k.lathe([(0.0, .29 * h), (.068 * h, .29 * h), (.07 * h, .315 * h), (.068 * h, .33 * h), (0.0, .33 * h)], 4, mat=GOLD, start=45.0, smooth=False),
                              Matrix.Translation(at)))
    # the apron (seat rails) and its gold fillet; the stretchers
    merge(hard, box((-hd + post * .5, -hw + .01 * h, .33 * h), (hd - .005 * h, hw - .01 * h, seat - .07 * h), WALNUT))
    merge(hard, box((-hd + post * .5, -hw + .005 * h, seat - .075 * h), (hd + .005 * h, hw - .005 * h, seat - .065 * h), GOLD))
    for side in (-1, 1):
        merge(hard, box((-hd + post, side * (hw - .05 * h) - .012 * h, .12 * h), (hd - .05 * h, side * (hw - .05 * h) + .012 * h, .145 * h), WALNUT))
    merge(hard, box((-.012 * h, -hw + .05 * h, .125 * h), (.012 * h, hw - .05 * h, .14 * h), WALNUT))
    # the seat cushion (soft): over the apron, a little proud at the front; a gold welt at its foot
    s0, s1 = seat - .065 * h, seat + .03 * h
    merge(soft, box((-hd + post * .9, -hw + .015 * h, s0), (hd + .015 * h, hw - .015 * h, s1), OLIVE))
    welt = [Vector((-hd + post * .9, -hw + .012 * h, s0 + .004 * h)), Vector((hd + .018 * h, -hw + .012 * h, s0 + .004 * h)),
            Vector((hd + .018 * h, hw - .012 * h, s0 + .004 * h)), Vector((-hd + post * .9, hw - .012 * h, s0 + .004 * h))]
    merge(hard, tube(welt, .007 * h, 6))
    # the back: a bottom rail over the seat, an arched top rail between the posts, the padded panel between, in a gold bead
    rail_y = hw - post * .9
    merge(hard, box((-hd + .005 * h, -rail_y, seat), (-hd + post * .8, rail_y, seat + .035 * h), WALNUT))
    arch = []
    for i in range(17):
        yy = -rail_y + 2 * rail_y * i / 16
        u = yy / rail_y
        arch.append((yy, top - .06 * h + .035 * h * (1 - u * u)))
    rail = [(yy, z) for yy, z in arch] + [(yy, top + .01 * h + .035 * h * (1 - (yy / rail_y) ** 2)) for yy, z in reversed(arch)]
    rb = k.prism(rail, -hd - .02 * h, -hd + post * .75, mat=WALNUT)
    # prism builds in x-y with z extruded: turn it so outline (y, z) and extrude along x
    turn = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    for v in rb.verts:
        v.co = Vector((v.co.z, v.co.x, v.co.y))
    merge(hard, rb)
    py, p0, p1 = rail_y - .012 * h, seat + .045 * h, top - .07 * h
    panel = [(py, p0)] + [(py * math.cos(math.pi * i / 12), p1 - .04 * h + .04 * h * math.sin(math.pi * i / 12) + .0 * h)
                          for i in range(13)] + [(-py, p0)]
    pb = k.prism(panel, -hd + post * .1, -hd + post * .55, mat=OLIVE)
    for v in pb.verts:
        v.co = Vector((v.co.z, v.co.x, v.co.y))
        if v.co.x > -hd + post * .5:
            yy, zz = v.co.y / py, (v.co.z - p0) / max(1e-6, p1 - p0)
            v.co.x += .018 * h * max(0.0, 1 - yy * yy) ** .5 * max(0.0, math.sin(math.pi * min(1.0, max(0.0, zz)))) ** .5
    merge(soft, pb)
    bead = [Vector((-hd + post * .58, y, z)) for y, z in panel] + [Vector((-hd + post * .58, panel[0][0], panel[0][1]))]
    merge(hard, tube(bead, .008 * h, 6))
    # the crest: a gold shell (a fan of ribs) over the arch's crown, C-scrolls either side, a scrolled base
    crest = bmesh.new()
    for i in range(9):
        t = math.radians(-64 + 16 * i)
        b = Vector((0, math.sin(t) * .085 * h, math.cos(t) * .1 * h))
        merge(crest, tube([Vector((0, 0, .005 * h)), b * .55 + Vector((.012 * h, 0, 0)), b], .012 * h, 8,
                          radii=[.008 * h, .016 * h, .011 * h]))
    for side in (-1, 1):
        sc = [Vector((0, side * .05 * h, .0)), Vector((0, side * .1 * h, .0)), Vector((0, side * .15 * h, .02 * h)),
              Vector((0, side * .165 * h, .05 * h)), Vector((0, side * .14 * h, .065 * h)), Vector((0, side * .12 * h, .045 * h)),
              Vector((0, side * .13 * h, .03 * h))]
        merge(crest, tube(curve(sc, 6), .012 * h, 8))
        lf = [Vector((0, side * .03 * h, .01 * h)), Vector((0, side * .07 * h, .045 * h)), Vector((0, side * .06 * h, .075 * h))]
        merge(crest, tube(curve(lf, 5), .01 * h, 8, radii=None))
    merge(crest, transform(k.lathe([(0.0, 0.0), (.02 * h, 0.0), (.012 * h, .02 * h), (0.0, .03 * h)], 10, mat=GOLD),
                           Matrix.Translation((0, 0, -.01 * h))))
    transform(crest, Matrix.Translation((-hd + post * .35, 0, top + .03 * h)) @ Matrix.Scale(1.35, 4))
    merge(hard, crest)
    # --- the lines (user 11-25: "라인이나 디테일"): every walnut face the sheet shows carries a moulding or a bead
    bead = .0055 * h
    for side in (-1, 1):
        y0 = side * (hw - post / 2)
        def post_at(z):   # the back post's front-face middle at height z (it rakes out and back over the seat)
            u = max(0.0, (z - seat) / (top - seat))
            return Vector((-hd + post - .02 * h * u, y0 + side * .012 * h * u, z))
        # a sunk panel outlined in walnut beads down the post's front, from over the seat to under the cap
        za, zb = seat + .04 * h, top - .07 * h
        for dy in (-post * .3, post * .3):
            merge(hard, tube([post_at(z) + Vector((.004 * h, dy, 0)) for z in (za, (za + zb) / 2, zb)], bead, 6, mat=WALNUT))
        for z in (za, zb):
            merge(hard, tube([post_at(z) + Vector((.004 * h, dy, 0)) for dy in (-post * .3, post * .3)], bead, 6, mat=WALNUT))
        # a gold ring where the post passes the seat, and under the post a little splay back (the sheet's back legs)
        merge(hard, box((-hd - .006 * h, y0 - post * .56, seat - .075 * h), (-hd + post + .006 * h, y0 + post * .56, seat - .06 * h), GOLD))
        # the cap: a stepped moulding and a low pyramid on top
        yt, xt = y0 + side * .012 * h, -hd + post / 2 - .02 * h
        merge(hard, box((xt - post * .66, yt - post * .66, top + .035 * h), (xt + post * .66, yt + post * .66, top + .042 * h), GOLD))
        pyr = k.lathe([(0.0, 0.0), (post * .86, 0.0), (post * .5, .03 * h), (0.0, .04 * h)], 4, mat=WALNUT, start=45.0, smooth=False)
        merge(hard, transform(pyr, Matrix.Translation((xt, yt, top + .042 * h))))
    # the arched top rail: a gold bead along its under edge and a walnut step over it
    merge(hard, tube([Vector((-hd + post * .78, yy, z - .002 * h)) for yy, z in arch], bead * 1.3, 6))
    merge(hard, tube([Vector((-hd + post * .78, yy, z + .05 * h)) for yy, z in arch], bead, 6, mat=WALNUT))
    # the apron: a gold bead along its foot on the front and sides
    ya = hw - .012 * h
    merge(hard, tube([Vector((-hd + post * .6, -ya, .335 * h)), Vector((hd + .002 * h, -ya, .335 * h)),
                      Vector((hd + .002 * h, ya, .335 * h)), Vector((-hd + post * .6, ya, .335 * h))], bead * 1.2, 6))
    # the seat's front: a rolled edge (a walnut-coloured welt is the sheet's gold one, already round the foot)
    return hard, soft
