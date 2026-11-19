"""Royal Observatory common parts (R0, seoungtest R725, 2026-11-19): the pieces every zone of the way uses, so the gold,
the bevels and the names are the same everywhere (PLAN.md R0 "공통 부품 키트").

    import sys; sys.path.insert(0, r"<this folder>"); import ro_kit as k, ro_parts as parts

  star_platform(col, key, top, radius, floor_z, ...)  a star platform (RO_A2 sheet, first piece; RO_2): navy / violet
      enamel top with a motif from the atlas (round cell, a gold five-point star always in the middle, a different
      surround per platform), a gold rim band with raised four-point stars and rivet rows, a beaded torus, an enamel
      soffit with gold brackets, a ring hung under it, one fluted gold pillar down to the hall floor's level (no base: the
      floor is the abyss now and ro-shell carries the pillar on down).
      Its walking top stays where the graybox's is (z of `top`) and nothing stands above it but the lamp posts outside
      the rim. `deck=False` builds only what is under a deck that moves on its own (a variable star's hub).
  lamp_post(col, key, foot, height)                    the pearl-globe lamp post (RO_A1, third piece). Light is Unity's.
  rail(col, key, a, b, floor_z, ...)                   a balustrade (RO_A1, first piece): marble plinth and coping, gold
      vase balusters, end posts with violet star panels and gold ball finials.
  line_bridge(col, key, a, b, width, ...)              a constellation line (RO_A1 bridge): its walking top in motif panels
      about 3 m long (ro_kit.motif_panels), gold seams and edging, a marble and enamel girder under it.
Every function returns the objects it made (meshes in ro_kit slots, metre UVs done, motif faces mapped) and
lamp positions where it has lamps (for the Unity lights, RoyalObservatoryDressing reads them from the export's side
file). Sizes are in metres; the graybox numbers are read by the caller from ro_blockout.json / the builder.
"""
import math
import bmesh, bpy
from mathutils import Matrix, Vector

import ro_kit as k

GOLD, BRASS, NAVY, VIOLET, MARBLE, PEARL, MOTIF = 0, 1, 2, 3, 4, 5, 6
SLOT_ORDER = ("gold", "brass", "navy", "violet", "marble", "pearl", "motif")


def _obj(col, name, bm, matrix=None):
    ob = k.new_object(name, bm, col, SLOT_ORDER)
    if matrix is not None:
        ob.matrix_world = matrix
    return ob


def _finish(ob, bevel=None, uvs=True):
    if bevel:
        k.bevel(ob, bevel, 2)
        k.apply_modifiers(ob)
    if uvs:
        k.surface_uvs(ob)
    return ob


def _transform(bm, m, verts=None):
    bmesh.ops.transform(bm, matrix=m, verts=verts if verts is not None else bm.verts)


def _merge(dst, src):
    """Append bmesh src into dst (material indices kept), then free src."""
    me = bpy.data.meshes.new("_tmp")
    src.to_mesh(me)
    src.free()
    dst.from_mesh(me)
    bpy.data.meshes.remove(me)
    return dst


def _part(bm_list):
    out = bmesh.new()
    for b in bm_list:
        _merge(out, b)
    return out


def ring_of(count, radius, z, make, phase=0.0):
    """count copies of make() (a bmesh built round its own origin, facing +x outward) round z at `radius`."""
    out = bmesh.new()
    for i in range(count):
        t = 2 * math.pi * (i + phase) / count
        b = make()
        _transform(b, Matrix.Translation((radius * math.cos(t), radius * math.sin(t), z)) @ Matrix.Rotation(t, 4, "Z"))
        _merge(out, b)
    return out


def star_stud(size, depth, mat=GOLD, points=4):
    """A raised star facing +x (its outline in the y-z plane), pyramid-faceted: a stud on a band."""
    bm = bmesh.new()
    outline = k.star_outline(points, size * .5, size * .5 * (.32 if points == 4 else .42), 90.0)
    ring = [bm.verts.new((0, x, y)) for x, y in outline]
    apex = bm.verts.new((depth, 0, 0))
    back = bm.verts.new((0, 0, 0))
    n = len(ring)
    for i in range(n):
        a, b = ring[i], ring[(i + 1) % n]
        bm.faces.new((a, b, apex)).material_index = mat
    bm.normal_update()
    for f in bm.faces:
        if f.normal.x < 0:
            f.normal_flip()
    bm.verts.remove(back)
    return bm


def rivet(r, h, mat=GOLD, segs=6):
    """A domed rivet facing +x."""
    prof = [(r, 0.0), (r * .85, h * .55), (r * .45, h * .9), (0.0, h)]
    bm = k.lathe(prof, segs, mat=mat)
    _transform(bm, Matrix.Rotation(math.pi / 2, 4, "Y"))
    return bm


def bead(r, mat=GOLD, u=8, v=5):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    for f in bm.faces:
        f.material_index = mat
        f.smooth = True
    return bm


def torus(R, r, segs=48, rsegs=8, mat=GOLD):
    prof = [(R + r * math.cos(2 * math.pi * i / rsegs), r * math.sin(2 * math.pi * i / rsegs)) for i in range(rsegs + 1)]
    return k.lathe(prof, segs, mat=mat)


def fluted_shaft(radius, z0, z1, flutes=16, depth=.035, mat=GOLD):
    """A fluted column shaft round z: `flutes` hollows, fillets between."""
    pts = []
    per = 4
    for i in range(flutes * per):
        t = 2 * math.pi * i / (flutes * per)
        ph = (i % per) / per
        r = radius - depth * math.sin(math.pi * ph) if ph > 0 else radius
        pts.append((r * math.cos(t), r * math.sin(t)))
    bm = k.prism(pts, z0, z1, mat, cap=False)
    for f in bm.faces:
        f.smooth = True
    return bm


# ------------------------------------------------------------------ the star platform

def star_platform(col, key, top, radius, floor_z, lamps=(), ring_heading=None, deck=True, pillar_radius=.36,
                  under_depth=1.35, segments=None, motif_avoid=()):
    """A star platform round `top` (Blender point: the middle of its walking top). lamps: headings (degrees, Blender:
    0 = +x, counter-clockwise) where a lamp post stands outside the rim. ring_heading: where the ring hangs (None: from
    the key's hash). Returns (objects, lamp positions)."""
    top = Vector(top)
    R = radius
    seg = segments or max(64, int(round(2 * math.pi * R / .2 / 8)) * 8)
    objs = []
    lamps_at = []
    place = Matrix.Translation(top)

    # the deck, one lathe from its middle: the top (motif), the gold lip, the band, the torus, the soffit, the collar
    band_top, band_bot = -.08, -.52
    prof = [(0.0, 0.0), (R * .5, 0.0), (R - .05, 0.0),          # top (motif)       spans 0, 1
            (R + .06, 0.0), (R + .115, -.015), (R + .14, -.05),  # gold lip           2, 3, 4
            (R + .14, band_top), (R + .14, band_bot),            # gold band          5, 6
            (R + .165, -.54), (R + .19, -.57), (R + .195, -.60), (R + .185, -.635), (R + .16, -.66),   # torus  7-11
            (R + .1, -.68), (R - .05, -.70),                     # fillet             12, 13
            (R * .62, -.86), (R * .3, -1.0),                     # soffit (enamel)    14, 15
            (.78, -1.05), (.76, -1.09), (.66, -1.13), (.64, -under_depth), (pillar_radius + .02, -under_depth - .04)]  # collar
    mats = [MOTIF, MOTIF, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, NAVY, NAVY, GOLD,
            GOLD, GOLD, GOLD, GOLD]
    if deck:
        bm = k.lathe(prof, seg, mat=mats)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:
            if f.material_index == MOTIF:
                f.smooth = False
        d = _obj(col, key + " deck", bm, place)
        k.surface_uvs(d)
        k.motif_disc(d, key, top, R - .05, faces=lambda o, p: p.material_index == 6 and p.normal.z > .9, avoid=motif_avoid)
        objs.append(d)

        # band ornaments: raised four-point stars, rivet rows above and below, beads on the torus
        circ = 2 * math.pi * (R + .14)
        n_star = max(8, int(circ / .7))
        # (lightened 11-19, the user's "경량화 진행": rivets every 0.32 m (were 0.16) with five sides, beads every 0.16 m
        # (were 0.085) as small 5 x 3 balls, the wires 0.25 m a side (were 0.1) - the band's look from a pawn's distance
        # is the same; 26k -> about 8k tris)
        orn = ring_of(n_star, R + .14, (band_top + band_bot) * .5, lambda: star_stud(.28, .045))
        _merge(orn, ring_of(n_star, R + .14, (band_top + band_bot) * .5, lambda: rivet(.022, .015, segs=5), phase=.5))
        n_riv = max(16, int(circ / .32))
        _merge(orn, ring_of(n_riv, R + .14, band_top - .035, lambda: rivet(.018, .012, segs=5)))
        _merge(orn, ring_of(n_riv, R + .14, band_bot + .035, lambda: rivet(.018, .012, segs=5)))
        # engraved-looking fine lines: two thin raised wires along the band's edges
        _merge(orn, _wire_ring(R + .142, band_top - .012, .006))
        _merge(orn, _wire_ring(R + .142, band_bot + .012, .006))
        n_bead = max(24, int(2 * math.pi * (R + .19) / .16))
        _merge(orn, ring_of(n_bead, R + .192, -.60, lambda: bead(.034, u=5, v=3)))
        o = _obj(col, key + " band", orn, place)
        k.surface_uvs(o)
        objs.append(o)

        # brackets under the soffit, between the collar and the rim
        def bracket():
            pts = [(.55, 0, -1.02), (R * .74, 0, -.74), (R * .8, 0, -.72)]
            outline = [(.62, -1.08), (R * .8, -.76), (R * .8, -.84)]
            for i in range(1, 5):
                t = i / 5
                x = R * .8 - (R * .8 - .66) * t
                z = -.84 - (under_depth - .84 - .05) * (1 - (1 - t) ** 2.2)
                outline.append((x, z))
            outline.append((.62, -under_depth + .02))
            b = k.prism(outline, -.04, .04, GOLD)
            _transform(b, Matrix.Rotation(math.pi / 2, 4, "X"))   # outline (x, z) in the x-z plane, thickness along y
            return b
        brk = ring_of(8, 0, 0, bracket, phase=.5)
        # a scroll knob at each bracket's foot
        _merge(brk, ring_of(8, R * .78, -.86, lambda: bead(.05, u=6, v=4), phase=.5))
        ob = _obj(col, key + " brackets", brk, place)
        _finish(ob)   # under the deck, seen from below only: no bevel (11-19 lightening)
        objs.append(ob)

    # the ring hung under it (RO_A2: a small gold ring on the pillar's collar) and the pillar
    h = ring_heading if ring_heading is not None else (k._hash("ring:" + key) % 360)
    t = math.radians(h)
    ring = torus(.12, .018, 14, 4)
    _transform(ring, Matrix.Rotation(math.pi / 2, 4, "X"))
    _transform(ring, Matrix.Rotation(t, 4, "Z"))
    _transform(ring, Matrix.Translation(((pillar_radius + .2) * math.cos(t), (pillar_radius + .2) * math.sin(t), -under_depth - .35)))
    eye = k.lathe([(0, 0), (.035, 0), (.035, .06), (0, .06)], 10, mat=GOLD)
    _transform(eye, Matrix.Rotation(math.pi / 2, 4, "Y"))
    _transform(eye, Matrix.Rotation(t, 4, "Z"))
    _transform(eye, Matrix.Translation(((pillar_radius + .03) * math.cos(t), (pillar_radius + .03) * math.sin(t), -under_depth - .22)))
    _merge(ring, eye)
    stem = k.lathe([(0, 0), (.012, 0), (.012, .14), (0, .14)], 8, mat=GOLD)
    _transform(stem, Matrix.Translation(((pillar_radius + .2) * math.cos(t), (pillar_radius + .2) * math.sin(t), -under_depth - .24)))
    _merge(ring, stem)

    shaft_top = -under_depth - .04
    zf = floor_z - top.z      # the hall floor, relative
    p = k.lathe([(pillar_radius + .02, 0), (pillar_radius + .1, -.05), (pillar_radius + .1, -.15), (pillar_radius + .04, -.19),
                 (pillar_radius + .06, -.24), (pillar_radius + .02, -.28), (pillar_radius, -.32)], 20, mat=GOLD)
    _transform(p, Matrix.Translation((0, 0, shaft_top)))
    _merge(ring, p)
    if not deck:
        # a pillar with no deck of its own (an orbit's, a rail's, a variable star's under its hub): its top capped, a
        # shallow gold dome over the collar, not an open tube (11-19, the user on the orbits' pillars in Unity)
        cap = k.lathe([(0, .07), (pillar_radius * .5, .06), (pillar_radius + .02, 0)], 16, mat=GOLD)
        _transform(cap, Matrix.Translation((0, 0, shaft_top)))
        _merge(ring, cap)
    # 11-19 (the user, "1 2 번 적용"): the hall floor became the abyss (ro-shell's RoHallAbyss carries each pillar on
    # down to y -110), so the shaft runs to the old floor's level with no base - it joins ro-shell's pillar there
    _merge(ring, fluted_shaft(pillar_radius, zf, shaft_top - .32, 12, .04))   # 12 flutes (were 20), 11-19 lightening
    # astragal rings down the shaft, about every 4 m
    length = (shaft_top - .32) - zf
    n_rings = max(1, int(length / 4.0))
    for i in range(1, n_rings + 1):
        z = zf + length * i / (n_rings + 1)
        a = torus(pillar_radius + .02, .035, 20, 5)
        _transform(a, Matrix.Translation((0, 0, z)))
        _merge(ring, a)
    ob = _obj(col, key + " pillar", ring, place)
    _finish(ob)   # no bevel: a slim pillar seen across the hall (11-19 lightening)
    objs.append(ob)

    for hd in lamps:
        t = math.radians(hd)
        foot = top + Vector(((R + .02) * math.cos(t), (R + .02) * math.sin(t), 0))
        got, at = lamp_post(col, "%s lamp %d" % (key, int(hd) % 360), foot)
        objs += got
        lamps_at.append(at)
    return objs, lamps_at


def _wire_ring(radius, z, r, segs=None):
    segs = segs or max(48, int(2 * math.pi * radius / .25))
    t = torus(radius, r, segs, 3)
    _transform(t, Matrix.Translation((0, 0, z)))
    return t


# ------------------------------------------------------------------ the lamp post

def lamp_post(col, key, foot, height=1.2, globe=.16):
    """The lamp post (RO_A1): a stepped gold base with a navy band and studs, a slender ringed shaft, a four-armed cage
    holding a pearl globe. foot: Blender point on the floor. Returns ([objects], globe centre)."""
    foot = Vector(foot)
    prof = [(0.0, 0.0), (.15, 0.0), (.15, .03), (.13, .05), (.13, .07), (.11, .09),                 # plinth
            (.11, .12), (.105, .12), (.105, .27), (.11, .27), (.11, .29), (.085, .32), (.06, .36),   # navy drum (2 spans)
            (.05, .42), (.038, .46), (.034, .5), (.034, height - .42), (.05, height - .40), (.04, height - .37),
            (.034, height - .35), (.034, height - .26), (.06, height - .24), (.075, height - .21), (.045, height - .19),
            (.045, height - .17), (0.0, height - .17)]
    mats = [GOLD] * (len(prof) - 1)
    mats[7] = NAVY
    bm = k.lathe(prof, 14, mat=mats)   # 14 sides (were 24): a thin post (11-19 lightening)
    # studs on the navy drum
    _merge(bm, ring_of(6, .105, .195, lambda: star_stud(.07, .012)))
    # two rings on the shaft
    for z in (.62, height * .62):
        r = torus(.032, .012, 10, 4)
        _transform(r, Matrix.Translation((0, 0, z)))
        _merge(bm, r)
    # the cage: four arms curving up round the globe, a cap and finial
    gz = height - .17 + globe * .95
    for i in range(4):
        t = math.pi / 2 * i + math.pi / 4
        pts = []
        for j in range(6):
            a = -math.pi / 2 + math.pi * .85 * j / 5
            pts.append(Vector(((globe + .018) * math.cos(a) * math.cos(t), (globe + .018) * math.cos(a) * math.sin(t),
                               gz + (globe + .018) * math.sin(a))))
        pts[0] = Vector((.03 * math.cos(t), .03 * math.sin(t), height - .17))
        arm = _tube(pts, .007, 4)
        _merge(bm, arm)
    cap = k.lathe([(0, 0), (.05, 0), (.04, .025), (.012, .05), (.02, .07), (0, .095)], 10, mat=GOLD)
    _transform(cap, Matrix.Translation((0, 0, gz + globe * .93)))
    _merge(bm, cap)
    g = bmesh.new()
    bmesh.ops.create_uvsphere(g, u_segments=16, v_segments=9, radius=globe)
    for f in g.faces:
        f.material_index = PEARL
        f.smooth = True
    _transform(g, Matrix.Translation((0, 0, gz)))
    _merge(bm, g)
    ob = _obj(col, key, bm, Matrix.Translation(foot))
    k.surface_uvs(ob)
    return [ob], foot + Vector((0, 0, gz))


def _tube(points, r, segs=6, mat=GOLD):
    """A round tube along a polyline (Blender points)."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        a = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        a.normalize()
        side = a.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = a.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(a)
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * j / segs) + up * math.sin(2 * math.pi * j / segs)) * r)
                      for j in range(segs)])
    for i in range(len(rings) - 1):
        for j in range(segs):
            f = bm.faces.new((rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]))
            f.material_index = mat
            f.smooth = True
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ------------------------------------------------------------------ the balustrade

def baluster(h, mat=GOLD):
    s = h / .62
    prof = [(0, 0), (.045 * s, 0), (.045 * s, .03 * s), (.03 * s, .05 * s), (.028 * s, .07 * s), (.05 * s, .17 * s),
            (.055 * s, .24 * s), (.04 * s, .33 * s), (.022 * s, .42 * s), (.02 * s, .48 * s), (.032 * s, .52 * s),
            (.022 * s, .55 * s), (.04 * s, .58 * s), (.04 * s, .62 * s), (0, .62 * s)]
    return k.lathe(prof, 8, mat=mat)


def rail(col, key, a, b, floor_z, height=1.1, posts=True, start_post=True, end_post=True, lamp_posts=()):
    """A balustrade from a to b (Blender points, z ignored) on a floor at floor_z: marble plinth, gold vase balusters
    every ~0.22 m, marble coping with a gold top moulding, posts at the ends (and every ~4 m) with violet star panels
    and gold ball finials. Its footprint is the graybox rail's (0.3 m). lamp_posts: distances along it (metres from a)
    of posts that carry a lamp instead of a ball (the caller stands ro_parts.lamp_post on their caps, height + 0.12)."""
    a, b = Vector((a[0], a[1], floor_z)), Vector((b[0], b[1], floor_z))
    d = b - a
    L = d.length
    yaw = math.atan2(d.y, d.x)
    bm = bmesh.new()
    w = .3
    plinth = k.prism([(0, -w / 2), (L, -w / 2), (L, w / 2), (0, w / 2)], 0, .16, MARBLE)
    _merge(bm, plinth)
    coping = k.prism([(0, -w * .45), (L, -w * .45), (L, w * .45), (0, w * .45)], height - .12, height - .04, MARBLE)
    _merge(bm, coping)
    gtop = k.prism([(0, -w * .32), (L, -w * .32), (L, w * .32), (0, w * .32)], height - .04, height, GOLD)
    _merge(bm, gtop)
    gbase = k.prism([(0, -w * .36), (L, -w * .36), (L, w * .36), (0, w * .36)], .16, .19, GOLD)
    _merge(bm, gbase)
    span_posts = [0.0, L] if posts else []
    if posts and L > 5:
        n = int(L / 4.0)
        span_posts = [L * i / n for i in range(n + 1)]
    if not start_post and span_posts:
        span_posts = span_posts[1:]
    if not end_post and span_posts:
        span_posts = span_posts[:-1]
    hb = height - .12 - .19
    x = .0
    step = .22
    nb = max(1, int(L / step))
    for i in range(nb):
        x = (i + .5) * L / nb
        if any(abs(x - p) < .24 for p in span_posts):
            continue
        bl = baluster(hb)
        _transform(bl, Matrix.Translation((x, 0, .19)))
        _merge(bm, bl)
    for p in span_posts:
        post = k.prism([(-.17, -.19), (.17, -.19), (.17, .19), (-.17, .19)], 0, height + .05, MARBLE)
        _transform(post, Matrix.Translation((p, 0, 0)))
        _merge(bm, post)
        cap = k.prism([(-.2, -.22), (.2, -.22), (.2, .22), (-.2, .22)], height + .05, height + .12, GOLD)
        _transform(cap, Matrix.Translation((p, 0, 0)))
        _merge(bm, cap)
        if not any(abs(p - q) < .3 for q in lamp_posts):
            ball = k.lathe([(0, 0), (.06, 0), (.045, .03), (.03, .05), (.1, .1), (.12, .18), (.1, .26), (.05, .3), (0, .31)], 16, mat=GOLD)
            _transform(ball, Matrix.Translation((p, 0, height + .12)))
            _merge(bm, ball)
        for side in (-1, 1):   # violet star panels on the two faces along the rail's sides
            panel = k.prism([(-.12, -.004), (.12, -.004), (.12, .004), (-.12, .004)], .25, height - .2, VIOLET)
            _transform(panel, Matrix.Translation((p, side * .192, 0)))
            _merge(bm, panel)
            frame = _rect_wire(.24, height - .45, .012)
            _transform(frame, Matrix.Rotation(math.pi / 2, 4, "X") if False else Matrix.Identity(4))
            _transform(frame, Matrix.Translation((p, side * .197, (height - .2 + .25) * .5)))
            _merge(bm, frame)
            st = star_stud(.13, .015, points=8)
            _transform(st, Matrix.Rotation(side * math.pi / 2, 4, "Z"))
            _transform(st, Matrix.Translation((p, side * .196, (height - .2 + .25) * .5)))
            _merge(bm, st)
    ob = _obj(col, key, bm, Matrix.Translation(a) @ Matrix.Rotation(yaw, 4, "Z"))
    _finish(ob)   # no bevel modifier: on ~60 balusters a rail it quadrupled the count (218k for the entry's four)
    return [ob]


def _rect_wire(w, h, r):
    """A rectangular gold frame in the x-z plane, centred, wire radius r (square section)."""
    bm = bmesh.new()
    for (x0, z0, x1, z1) in ((-w / 2, -h / 2, w / 2, -h / 2), (w / 2, -h / 2, w / 2, h / 2), (w / 2, h / 2, -w / 2, h / 2),
                             (-w / 2, h / 2, -w / 2, -h / 2)):
        lo = (min(x0, x1) - r, min(z0, z1) - r)
        hi = (max(x0, x1) + r, max(z0, z1) + r)
        bx = k.prism([(lo[0], -r), (hi[0], -r), (hi[0], r), (lo[0], r)], lo[1], hi[1], GOLD)
        _merge(bm, bx)
    return bm


# ------------------------------------------------------------------ the constellation line

def line_bridge(col, key, a, b, width=3.0, depth=.4, cut_a=None, cut_b=None, panel=3.0, body_depth=.7, curve=True):
    """A constellation line's bridge (RO_A1's bridge piece, RO_2's lines) over the graybox's SlopeBetween: its walking
    top a plane through a and b (Blender points on the top's middle line, the graybox's line ends), `width` across.
    cut_a / cut_b: (centre, radius) of the star platform at that end - the end is cut round it (in plan) so the bridge
    meets the platform's gold rim instead of overlapping it.
      top (all flush, one plane): the motif atlas in panels of about `panel` metres (ro_kit.motif_panels: a different
           square motif a panel, no two neighbours alike) with gold seams between, a cream marble border along both
           edges and a gold line between border and panels;
      sides (`body_depth` deep - drawn only, deeper than the graybox's `depth`): a gold moulding, a navy enamel frieze
           in framed panels with gold eight-point stars and rivets, a gold base moulding with a bead string;
      under: an arched girder along each side - navy spandrels, gold-edged, deep at the ends like piers, a gold ring
           medallion with a star at mid-span - and a navy soffit between.
    curve (the user, 11-19, on the straight slope in Unity: "일자로 다리가 지어지게 말고 아래에서 위로 곡선을 그려지게",
    then on a smoothstep "c(오른쪽으로 90도 회전)로 해줘" and "아치 반대야"): the whole bridge is bent so its top is a
    hollow curve rising from the lower end - level as it leaves the lower star, steepest as it meets the higher one.
    The same curve as the walking collider (RoyalObservatoryBuilder.ArchPoint, the planning session 11-19): the straight
    line from a to b minus arch_rise(a, b) * 4t(1 - t), t along it. Returns the objects."""
    a, b = Vector(a), Vector(b)
    fwd = (b - a).normalized()
    side = Vector((-fwd.y, fwd.x, 0)).normalized()          # horizontal, to the left of the way
    up = side.cross(fwd)
    if up.z < 0:
        up, side = -up, -side
    frame = Matrix((
        (fwd.x, side.x, up.x, a.x),
        (fwd.y, side.y, up.y, a.y),
        (fwd.z, side.z, up.z, a.z),
        (0, 0, 0, 1)))
    inv = frame.inverted()
    L = (b - a).length
    hw = width / 2
    border = .3            # the marble border's width

    def end_x(cut, y, at_start):
        if cut is None:
            return 0.0 if at_start else L
        c, r = cut
        c = inv @ Vector(c)
        dy = y - c.y
        if abs(dy) >= r:
            return 0.0 if at_start else L
        dx = math.sqrt(r * r - dy * dy)
        return c.x + dx if at_start else c.x - dx

    # rows across: the border, a gold line, the panels' field, a gold line, the border
    inner = hw - border
    ys = [-hw, -hw + border * .5, -inner, -inner + .04]
    field = 8
    ys += [-inner + .04 + (2 * inner - .08) * j / field for j in range(1, field)]
    ys += [inner - .04, inner, hw - border * .5, hw]
    ny = len(ys) - 1
    row_mat = []
    for j in range(ny):
        yc = (ys[j] + ys[j + 1]) * .5
        row_mat.append(MARBLE if abs(yc) > inner else GOLD if abs(yc) > inner - .04 else MOTIF)
    ends = [(end_x(cut_a, y, True), end_x(cut_b, y, False)) for y in ys]
    nx = max(4, int(math.ceil(L / .5)))

    top = bmesh.new()
    grid = [[top.verts.new((x0 + (x1 - x0) * i / nx, y, 0.0)) for i in range(nx + 1)] for y, (x0, x1) in zip(ys, ends)]
    for j in range(ny):
        for i in range(nx):
            f = top.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            f.material_index = row_mat[j]
    deck = _obj(col, key + " top", top, frame)
    mid_a = frame @ Vector((max(e[0] for e in ends), 0, 0))
    mid_b = frame @ Vector((min(e[1] for e in ends), 0, 0))
    k.surface_uvs(deck)
    cuts, _ = k.motif_panels(deck, key, mid_a, mid_b, 2 * inner - .08, panel=panel,
                             faces=lambda o, p: p.material_index == MOTIF, up=frame.to_3x3() @ Vector((0, 0, 1)))
    objs = [deck]

    # the outline round the top (local x, y), counter-clockwise: the a end, a long side, the b end, the other side
    loop = [(ends[j][0], ys[j]) for j in range(ny + 1)]
    loop += [(ends[ny][0] + (ends[ny][1] - ends[ny][0]) * i / nx, ys[ny]) for i in range(1, nx)]
    loop += [(ends[j][1], ys[j]) for j in range(ny, -1, -1)]
    loop += [(ends[0][0] + (ends[0][1] - ends[0][0]) * i / nx, ys[0]) for i in range(nx - 1, 0, -1)]
    if sum(loop[i][0] * loop[(i + 1) % len(loop)][1] - loop[(i + 1) % len(loop)][0] * loop[i][1] for i in range(len(loop))) < 0:
        loop.reverse()
    n = len(loop)

    def normal_at(i):
        p0, p1 = Vector(loop[i - 1]), Vector(loop[(i + 1) % n])
        t = (p1 - p0).normalized()
        return Vector((t.y, -t.x))

    body = bmesh.new()

    def band(z0, z1, out0, out1, mat):
        lo = [body.verts.new((*(Vector(loop[i]) + normal_at(i) * out0), z0)) for i in range(n)]
        hi = [body.verts.new((*(Vector(loop[i]) + normal_at(i) * out1), z1)) for i in range(n)]
        for i in range(n):
            f = body.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i]))
            f.material_index = mat
        return lo, hi
    D = body_depth
    # down the side: marble edge under the border, gold torus, navy frieze, gold base moulding
    band(-.05, 0.0, .0, 0.0, MARBLE)
    band(-.08, -.05, .04, .0, GOLD)
    band(-.12, -.08, .05, .04, GOLD)
    band(-.16, -.12, .02, .05, GOLD)
    band(-.58, -.16, .02, .02, NAVY)                        # the frieze
    band(-.62, -.58, .06, .02, GOLD)
    band(-.66, -.62, .06, .06, GOLD)
    lo, _ = band(-D, -.66, .02, .06, GOLD)
    cap = body.faces.new(list(reversed(lo)))
    cap.material_index = NAVY
    # gold seams between the motif panels, flush (a hair over the top)
    for c in cuts:
        x = (inv @ c).x
        seam = k.prism([(x - .03, -inner), (x + .03, -inner), (x + .03, inner), (x - .03, inner)], -.006, .0005, GOLD)
        _merge(body, seam)
    # the frieze in framed panels along the long sides: gold frame strips, an eight-point star in each, rivets
    for s in (-1, 1):
        x0a, x1a = ends[0 if s < 0 else ny]
        x0a, x1a = x0a + .1, x1a - .1
        cnt = max(1, int(round((x1a - x0a) / 1.1)))
        w = (x1a - x0a) / cnt
        y_face = s * (hw + .02)
        for i in range(cnt + 1):                              # the uprights between panels
            x = x0a + w * i
            up_ = k.prism([(x - .035, -.58), (x + .035, -.58), (x + .035, -.16), (x - .035, -.16)], 0, .035, GOLD)
            _transform(up_, Matrix(((1, 0, 0, 0), (0, 0, s, y_face), (0, 1, 0, 0), (0, 0, 0, 1))))
            _merge(body, up_)
        for i in range(cnt):
            xc = x0a + w * (i + .5)
            st = star_stud(.26, .045, points=8)
            _transform(st, Matrix.Rotation(s * math.pi / 2, 4, "Z"))
            _transform(st, Matrix.Translation((xc, y_face, -.37)))
            _merge(body, st)
            for dx in (-.32 * w, .32 * w):
                for zz in (-.24, -.5):
                    r = rivet(.02, .015)
                    _transform(r, Matrix.Rotation(s * math.pi / 2, 4, "Z"))
                    _transform(r, Matrix.Translation((xc + dx, y_face, zz)))
                    _merge(body, r)
        nb = max(4, int((x1a - x0a) / .11))
        for i in range(nb):
            bd = bead(.022, u=6, v=4)
            _transform(bd, Matrix.Translation((x0a + (x1a - x0a) * (i + .5) / nb, s * (hw + .075), -.64)))
            _merge(body, bd)
    ob = _obj(col, key + " body", body, frame)
    _finish(ob)   # no bevel modifier: on the studs, beads and rivets it tripled the count (161k for four lines)
    objs.append(ob)

    # under it: an arched girder along each side, deep at the ends like piers
    under = bmesh.new()
    xs0, xs1 = max(e[0] for e in ends) + .05, min(e[1] for e in ends) - .05
    span = xs1 - xs0
    rise = min(.9, max(.45, span * .16))
    steps = 20

    def soffit(t):
        return -D - .12 - rise * (1 - math.sin(math.pi * t) ** .7)
    for s in (-1, 1):
        y0, y1 = s * (hw - .34), s * (hw - .02)
        outline = [(xs0, -D), (xs1, -D)] + [(xs1 - span * i / steps, soffit(1 - i / steps)) for i in range(steps + 1)]
        g = k.prism(outline, min(y0, y1), max(y0, y1), NAVY)
        _transform(g, Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1))))
        _merge(under, g)
        # gold edging: a roll along the arch's curve, a fillet along the top, square gold piers' faces at the ends
        pts = [Vector((xs1 - span * i / steps, s * hw, soffit(1 - i / steps) + .03)) for i in range(steps + 1)]
        _merge(under, _tube(pts, .04, 8))
        for xe in (xs0, xs1 - .3):
            pf = k.prism([(xe, -D - rise - .12), (xe + .3, -D - rise - .12), (xe + .3, -D), (xe, -D)], 0, .04, GOLD)
            _transform(pf, Matrix(((1, 0, 0, 0), (0, 0, s, s * hw), (0, 1, 0, 0), (0, 0, 0, 1))))
            _merge(under, pf)
            st = star_stud(.18, .03, points=4)
            _transform(st, Matrix.Rotation(s * math.pi / 2, 4, "Z"))
            _transform(st, Matrix.Translation((xe + .15, s * (hw + .04), -D - rise * .55)))
            _merge(under, st)
        # the medallion: a gold ring round a navy disc, an eight-point star on it
        xm = (xs0 + xs1) * .5
        zm = -D - .1
        ring = torus(.2, .035, 28, 8)
        _transform(ring, Matrix.Rotation(math.pi / 2, 4, "X"))
        _transform(ring, Matrix.Translation((xm, s * (hw + .01), zm)))
        _merge(under, ring)
        disc = k.lathe([(0, 0), (.18, 0), (.18, .02), (0, .02)], 24, mat=NAVY)
        _transform(disc, Matrix.Rotation(-s * math.pi / 2, 4, "X"))
        _transform(disc, Matrix.Translation((xm, s * hw, zm)))
        _merge(under, disc)
        st = star_stud(.3, .05, points=8)
        _transform(st, Matrix.Rotation(s * math.pi / 2, 4, "Z"))
        _transform(st, Matrix.Translation((xm, s * (hw + .02), zm)))
        _merge(under, st)
    bmesh.ops.recalc_face_normals(under, faces=under.faces)
    ob = _obj(col, key + " girder", under, frame)
    _finish(ob)   # no bevel modifier: on the studs, beads and rivets it tripled the count (161k for four lines)
    objs.append(ob)
    sag = arch_rise(a, b) if curve else 0.0
    if sag > 1e-4:
        for o in objs:
            mw, mi = o.matrix_world, o.matrix_world.inverted()
            for v in o.data.vertices:
                w = mw @ v.co
                t = min(1.0, max(0.0, (inv @ w).x / L))
                w.z -= sag * 4 * t * (1 - t)
                v.co = mi @ w
            o.data.update()
    return objs


ARCH_STEEPEST = 25.0   # RoyalObservatoryBuilder.ArchSteepest


def arch_rise(a, b):
    """RoyalObservatoryBuilder.ArchRise for Blender points: how far a line's middle sags under the straight line."""
    run = math.hypot(b.x - a.x, b.y - a.y)
    dy = abs(b.z - a.z)
    room = (math.tan(math.radians(ARCH_STEEPEST)) * run - dy) * .25
    return max(0.0, min(.1 * run, dy * .25, room))
