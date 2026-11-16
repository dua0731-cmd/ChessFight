# Royal Carriage Yard models (R678): zone 2, the canal and the giant waterwheel (the pilot, 승규's painting U2).
#
#   exec(open(r".../rcy_kit.py").read()); exec(open(r".../canal_model.py").read())
#
# Builds two collections, both grey clay, both over the code's blockout (whose colliders stay):
#   "RCY Canal"  in the MAP frame (Blender x, y, z = Unity x, z, y): the landings and their balustrades, the parapets, the
#                canal walls, banks and bed, the four wooden walkways, the wheel's two axle posts with their bearings and
#                the axle, the flume's trough, cistern and trestles.
#   "RCY Wheel"  in the wheel's OWN frame (the axle at the origin, along Blender y = Unity z; spoke 1 up): rims, buckets,
#                hub, six pairs of spokes and their ties, the crowned-knight medallions. Unity hangs it on the
#                HorizontalWaterwheel, which turns it.
# Numbers are the blockout's (guides/rcy_built_play.json, read back with node()): every walked or struck face sits on
# its collider (within 5 cm); anything beyond a collider is out of reach (under the water, over 4 m up).
#
# Left to the code (never covered): the water sheets, the pour and the spill, the foam, the gold gap edges (the jump
# line), the sign.

# The stone and timber helpers (ashlar, flagstones, balustrade, beam ...) are rcy_kit.py's.


def walkway(col, lo, hi, gap_south):
    """One wooden walkway on its collider lo..hi (top at hi.z): planks across, two stringers, cross beams."""
    parts = []
    y = lo.y
    while y < hi.y - 1e-3:
        d = min(rnd.uniform(.26, .32), hi.y - y)
        if d > .08:
            parts.append(box(col, "Plank", (lo.x + .02, y + .01, hi.z - .08), (hi.x - .02, y + d - .01, hi.z), OAK, bevel=.015, segments=1))
        y += d
    for x in (lo.x + .05, hi.x - .27):
        parts.append(box(col, "Stringer", (x, lo.y + .05, lo.z + .02), (x + .22, hi.y - .05, hi.z - .08), WALNUT, bevel=.02))
    return join(col, "Walkway", parts)



# ------------------------------------------------------------------ the static canal


def build_canal():
    col = collection("RCY Canal")
    clear(col)
    water = -1.2
    pieces = []

    # Bed, banks, the canal's side walls (their inner faces coursed above the water).
    bl, bh = col_box("2 Canal/Canal bed")
    pieces.append(box(col, "Canal bed", bl, bh, STONE))
    for p in ("2 Canal/Bank S", "2 Canal/Bank N"):
        lo, hi = col_box(p)
        pieces.append(ashlar(col, p.split("/")[-1], lo, hi, "+y", below=None))
    for p, face in (("2 Canal/Canal wall W", "+x"), ("2 Canal/Canal wall E", "-x")):
        lo, hi = col_box(p)
        wall = ashlar(col, p.split("/")[-1], lo, Vector((hi.x, hi.y, hi.z - .3)), face, below=water)
        cope = box(col, "Coping", (lo.x - .05, lo.y, hi.z - .3), (hi.x + .05, hi.y, hi.z), STONE, bevel=.05)
        pieces += [wall, cope]

    # The parapets beside the landings.
    for p in ("2 Canal/Parapet S", "2 Canal/Parapet N"):
        for i in range(2):
            lo, hi = col_box(p, i)
            face = "+y" if p.endswith("S") else "-y"
            wall = ashlar(col, "Parapet", lo, Vector((hi.x, hi.y, hi.z - .25)), face, below=water)
            cope = box(col, "Parapet coping", (lo.x, lo.y - .04, hi.z - .25), (hi.x, hi.y + .04, hi.z), STONE, bevel=.05)
            pieces += [wall, cope]

    # The landings: paving on top, a stone face to the water, the piers under them, their balustrades.
    for name in ("Entry landing", "Far landing"):
        lo, hi = col_box("2 Canal/" + name)
        pieces.append(flagstones(col, name, lo.x, hi.x, lo.y, hi.y, hi.z))
        for i in range(2):
            plo, phi = col_box("2 Canal/" + name + " pier", i)
            pieces.append(ashlar(col, "Pier", plo, phi, "+x", course=.5, length=.7))
            pieces.append(box(col, "Pier cap", (plo.x - .1, plo.y - .1, phi.z - .25), (phi.x + .1, phi.y + .1, phi.z), STONE, bevel=.04))
        for i in range(3):
            rlo, rhi = col_box("2 Canal/" + name + " railing", i)
            pieces.append(balustrade(col, name + " balustrade", rlo, rhi))
        for side in ("W", "E"):
            rlo, rhi = col_box("2 Canal/" + name + " railing " + side)
            if rhi.z - rlo.z > 1.2:
                pieces.append(parapet_wall(col, name + " parapet " + side, rlo, rhi))
            else:
                pieces.append(balustrade(col, name + " balustrade " + side, rlo, rhi))

    # The walkways and their posts (the posts where the blockout drew them, square now, braced under the deck).
    for i, n in enumerate(by_path("2 Canal/Walkway")):
        c = n["cols"][0]
        lo, hi = u2b(c["min"]), u2b(c["max"])
        pieces.append(walkway(col, lo, hi, gap_south=(lo.y > 66)))
    for n in by_path("2 Canal/Walkway post"):
        p = u2b(n["pos"])
        h = n["scale"][1]
        z1, z0 = -.35, -.35 - h * 2 + .0
        pieces += square_post(col, "Walkway post", p.x, p.y, -4.5, -.33, w=.28, bands=(water - .3,))
    # Cross beams under each walkway end, post to post.
    for n in by_path("2 Canal/Walkway"):
        c = n["cols"][0]
        lo, hi = u2b(c["min"]), u2b(c["max"])
        for y in (lo.y + .4, hi.y - .4):
            pieces.append(box(col, "Cross beam", (lo.x + .05, y - .1, -.6), (hi.x - .05, y + .1, -.35), WALNUT, bevel=.02))
            pieces.append(beam(col, "Brace", (lo.x + .2, y, -.6), (hi.x - .2, y, water - .4), .14, .14))

    # The wheel's two axle posts (solid in the blockout), their bearings and braces, the axle between them.
    ap_z = u2b(by_path("2 Canal/Axle")[0]["pos"]).z
    for i in range(2):
        lo, hi = col_box("2 Canal/Axle post", i)
        cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
        pieces.append(box(col, "Axle post", lo, hi, WALNUT, bevel=.06))
        for z in (water + .2, 1.2, 3.2):
            pieces.append(box(col, "Post band", (lo.x - .025, lo.y - .025, z), (hi.x + .025, hi.y + .025, z + .14), IRON, bevel=.01, segments=1))
        out = -1 if cy < 66 else 1   # braces lean away from the wheel, under the water and the axle
        pieces.append(beam(col, "Post brace", (cx, cy + out * .3, 1.0), (cx, cy + out * 2.4, -4.5), .3, .3))
        for sx in (-1, 1):
            pieces.append(beam(col, "Post brace", (cx + sx * .35, cy, .6), (cx + sx * 1.8, cy, -4.5), .26, .26))
        # The bearing (a picture in the blockout) slimmed to a pillow block hugging the axle, so the hub's medallion
        # shows round it from the entry landing.
        b = by_path("2 Canal/Bearing")[i]
        bp, bs = u2b(b["pos"]), uscale2b(b["scale"])
        pieces.append(box(col, "Bearing", (bp.x - .36, bp.y - bs.y / 2, hi.z - .02), (bp.x + .36, bp.y + bs.y / 2, ap_z - .05), IRON, bevel=.03))
        pieces.append(cylinder(col, "Bearing cap", (bp.x, bp.y, ap_z), (0, 1, 0), .32, bs.y, IRON, 20, .02))
    ax = by_path("2 Canal/Axle")[0]
    ap = u2b(ax["pos"])
    pieces.append(cylinder(col, "Axle", ap, (0, 1, 0), .225, 3.3, IRON, 20))
    for y in (-1.6, 1.6):
        pieces.append(cylinder(col, "Axle collar", (ap.x, ap.y + y, ap.z), (0, 1, 0), .3, .12, IRON, 20, .015))

    # The flume: a planked trough with iron straps, the cistern, the two trestles (solid posts in the blockout).
    fl = by_path("2 Canal/Flume")[0]
    fp, fs = u2b(fl["pos"]), uscale2b(fl["scale"])
    flo, fhi = fp - fs / 2, fp + fs / 2
    pieces.append(box(col, "Flume floor", flo, fhi, WALNUT, bevel=.02))
    for side in by_path("2 Canal/Flume side"):
        sp, ss = u2b(side["pos"]), uscale2b(side["scale"])
        pieces.append(box(col, "Flume side", sp - ss / 2, sp + ss / 2, WALNUT, bevel=.02))
    y = flo.y + .4
    while y < fhi.y:
        pieces.append(box(col, "Flume strap", (flo.x - .12, y, flo.z - .05), (fhi.x + .12, y + .08, fhi.z + .52), IRON, bevel=.01, segments=1))
        y += 1.2
    cs = by_path("2 Canal/Cistern")[0]
    cp, csz = u2b(cs["pos"]), uscale2b(cs["scale"])
    clo, chi = cp - csz / 2, cp + csz / 2
    pieces.append(box(col, "Cistern", clo, chi, WALNUT, bevel=.05))
    for z in (clo.z + .25, chi.z - .35):
        pieces.append(box(col, "Cistern hoop", (clo.x - .03, clo.y - .03, z), (chi.x + .03, chi.y + .03, z + .1), IRON, bevel=.01, segments=1))
    for x in (clo.x, chi.x - .18):
        for yy in (clo.y, chi.y - .18):
            pieces.append(box(col, "Cistern corner", (x - .02, yy - .02, clo.z - .05), (x + .2, yy + .2, chi.z + .08), WALNUT, bevel=.02))
    posts = by_path("2 Canal/Trestle post")
    for n in posts:
        c = n["cols"][0]
        lo, hi = u2b(c["min"]), u2b(c["max"])
        pieces.append(box(col, "Trestle post", lo, hi, WALNUT, bevel=.03))
    for n in by_path("2 Canal/Trestle beam"):
        bp, bs = u2b(n["pos"]), uscale2b(n["scale"])
        pieces.append(box(col, "Trestle beam", bp - bs / 2, bp + bs / 2, WALNUT, bevel=.03))
    for y in sorted({round(u2b(n["pos"]).y, 2) for n in posts}):
        z = -4.5
        while z < 12.5:
            pieces.append(beam(col, "Trestle brace", (-.6, y, z), (.6, y, z + 3.0), .14, .12))
            pieces.append(beam(col, "Trestle brace", (.6, y, z), (-.6, y, z + 3.0), .14, .12))
            pieces.append(box(col, "Trestle tie", (-.75, y - .07, z + 3.0), (.75, y + .07, z + 3.18), WALNUT, bevel=.02))
            z += 3.0
    # Fold everything into one object per material family's worth of pieces (few draw calls).
    rest = [o for o in col.objects]
    # R681: then one object per ~10 m along the canal (rcy_kit.split_cells), so Unity can cull what is off screen.
    return split_cells(join(col, "Canal", rest))


# ------------------------------------------------------------------ the wheel (its own frame)


# The crest: a chess knight's head facing left (its outline in a unit box, base at v 0) and a crown over it.
KNIGHT = [(.18, 0), (.86, 0), (.86, .07), (.78, .1), (.8, .3), (.84, .48), (.82, .64), (.76, .78), (.66, .9),
          (.6, .97), (.55, 1.04), (.5, .94), (.42, .92), (.32, .86), (.22, .76), (.12, .64), (.06, .56), (.04, .5),
          (.07, .44), (.16, .43), (.2, .47), (.26, .44), (.36, .46), (.42, .42), (.4, .32), (.33, .22), (.28, .12),
          (.2, .1)]
CROWN = [(.3, 1.08), (.66, 1.08), (.7, 1.26), (.6, 1.16), (.48, 1.3), (.38, 1.16), (.27, 1.26)]


def relief(col, name, outline, centre_y, face, size, depth, mat=BRASS):
    """An outline (unit square, x right, y up) extruded `depth` out of the plane y = centre_y towards `face` (-1 or 1),
    `size` metres tall, centred on the axle."""
    bm = bmesh.new()
    vs = [bm.verts.new(((u - .5) * size * (-face), centre_y, (v - .65) * size)) for u, v in outline]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    moved = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, verts=moved, vec=(0, face * depth, 0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[x for x in bm.faces if len(x.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    ob = mesh_object(name, bm, col, (mat,))
    bevel_mod(ob, .03, 2)   # a rounded edge catches the light: the crest reads in grey clay too
    return ob


def ring(col, name, r0, r1, y0, y1, segments, mat, bevel=.03):
    """A flat ring r0..r1 in the wheel's plane (Blender xz), y0..y1 thick, in `segments` blocks with a small gap."""
    parts = []
    for k in range(segments):
        a0 = 2 * math.pi * k / segments + .004
        a1 = 2 * math.pi * (k + 1) / segments - .004
        bm = bmesh.new()
        steps = 3
        inner, outer = [], []
        for s in range(steps + 1):
            a = a0 + (a1 - a0) * s / steps
            for (r, lst) in ((r0, inner), (r1, outer)):
                lst.append((r * math.sin(a), r * math.cos(a)))
        vf = [bm.verts.new((x, y0, z)) for x, z in inner + outer[::-1]]
        vb = [bm.verts.new((x, y1, z)) for x, z in inner + outer[::-1]]
        n = len(vf)
        bm.faces.new(vf[::-1])
        bm.faces.new(vb)
        for i in range(n):
            bm.faces.new((vf[i], vf[(i + 1) % n], vb[(i + 1) % n], vb[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        ob = mesh_object(name, bm, col, (mat,))
        if bevel:
            bevel_mod(ob, bevel, 1)
        parts.append(ob)
    return parts


def build_wheel():
    col = collection("RCY Wheel")
    clear(col)
    R, rim, depth, hub, spokes, buckets = 9.0, .6, 1.0, 1.1, 6, 28
    half = depth / 2
    parts = []
    # Two rims (the blockout's: r 8.4..9.0, y 0.3..0.5 either side), segmented, an iron plate over every joint.
    for side in (-1, 1):
        y0, y1 = sorted((side * (half - .2), side * half))
        parts += ring(col, "Rim", R - rim, R, y0, y1, buckets, WALNUT)
        for k in range(buckets):
            a = 2 * math.pi * k / buckets
            plate_y = (y1 + .02) if side > 0 else (y0 - .02)
            p = box(col, "Rim plate", (-.12, min(plate_y, plate_y - side * .02), R - rim + .05), (.12, max(plate_y, plate_y - side * .02), R - .05), IRON, bevel=.008, segments=1)
            p.rotation_euler = (0, a, 0)
            parts.append(p)
    # The buckets: boards across the rims at the rim's outside, a wall inside each.
    chord = 2 * R * math.sin(math.pi / buckets)
    for k in range(buckets):
        a = 2 * math.pi * k / buckets
        b = box(col, "Bucket board", (-chord * .46, -half + .2, R - .1), (chord * .46, half - .2, R), OAK, bevel=.02, segments=1)
        b.rotation_euler = (0, a, 0)
        w = box(col, "Bucket wall", (-.05, -half + .2, R - rim + .03), (.05, half - .2, R - .1), WALNUT, bevel=.01, segments=1)
        w.rotation_euler = (0, a + math.pi / buckets * .5, 0)
        parts += [b, w]
    # The hub: a drum of staves with iron caps, the crowned knight on brass on both faces.
    parts.append(cylinder(col, "Hub", (0, 0, 0), (0, 1, 0), hub, depth + .4, WALNUT, 24, .05))
    for k in range(12):
        a = 2 * math.pi * k / 12
        s = box(col, "Hub band", (-.08, -half - .2, hub - .02), (.08, half + .2, hub + .03), IRON, bevel=.01, segments=1)
        s.rotation_euler = (0, a, 0)
        parts.append(s)
    for side in (-1, 1):
        # Cap and medallion as wide as the hub's face (U2), so the crowned knight shows round the axle's bearing.
        parts.append(cylinder(col, "Hub cap", (0, side * (half + .24), 0), (0, 1, 0), hub - .02, .1, IRON, 32, .02))
        parts.append(cylinder(col, "Medallion", (0, side * (half + .31), 0), (0, 1, 0), .92, .06, BRASS, 40, .015))
        my0, my1 = sorted((side * (half + .34), side * (half + .4)))
        parts += ring(col, "Medallion rim", .8, .92, my0, my1, 16, BRASS, .01)
        parts.append(relief(col, "Medallion knight", KNIGHT, side * (half + .34), side, 1.05, .12))
        parts.append(relief(col, "Medallion crown", CROWN, side * (half + .34), side, 1.05, .1))
        for k in range(12):   # bolt heads round the cap
            a = 2 * math.pi * k / 12 + math.pi / 12
            parts.append(cylinder(col, "Hub bolt", (math.sin(a) * 1.0, side * (half + .3), math.cos(a) * 1.0), (0, 1, 0), .045, .05, IRON, 8))
    # Spokes: a pair per blade (the blockout's: .45 wide, .22 thick at y +-.39, r 1.0..8.5), tied twice through the
    # wheel, an iron shoe where each meets the rim, an iron flange where each meets the hub.
    inside = R - rim
    length = inside - hub + .2
    mid = hub - .1 + length / 2
    for i in range(spokes):
        a = 2 * math.pi * i / spokes
        blade = []
        for side in (-1, 1):
            y = side * (half - .11)
            blade.append(box(col, "Spoke", (-.225, y - .11, mid - length / 2), (.225, y + .11, mid + length / 2), OAK, bevel=.04))
            blade.append(box(col, "Spoke shoe", (-.245, y + side * .11, inside - .55), (.245, y + side * .13, inside - .1), IRON, bevel=.008, segments=1))
            blade.append(box(col, "Spoke flange", (-.3, y + side * .11, hub - .05), (.3, y + side * .13, hub + .45), IRON, bevel=.008, segments=1))
        for at in (.45, .8):
            r = hub + (inside - hub) * at
            blade.append(box(col, "Spoke tie", (-.1575, -half + .15, r - .11), (.1575, half - .15, r + .11), WALNUT, bevel=.02))
        for o in blade:
            # Blender's turn about +y is the mirror of Unity's about +z: spoke i at -i*60 degrees here.
            o.rotation_euler = (0, -a, 0)
        parts += blade
    return join(col, "Giant waterwheel", parts)


canal = build_canal()
wheel = build_wheel()
print("canal tris", tris(collection("RCY Canal")), "wheel tris", tris(collection("RCY Wheel")))
