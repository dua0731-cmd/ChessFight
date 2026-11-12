# R619 (승규, armillary-sphere picture: "회전 시계가 첫 번 째 사진 같이 3D 3차원적으로 웅장하게 교차하면서 돌아 갔으면
# 좋겠고 안에 들어 있는 건 체스 킹으로 해줘"): a great gold armillary sphere in front of the stair end's round window,
# its rings turning about different axes and crossing each other, a chess king floating in the middle.
#
# Built at its place in the hall (Kit/stair_end.py axes: x across, y towards the stair end, z up; the window's centre
# is (0, BACK_Y 15.5, ROSE_Z 7.8)). Every moving part has its origin on the sphere's centre. Unity (LobbyStage.Hall.
# Armillary.cs) loads Armillary.fbx under the hall, nests the parts as gimbals and turns each about its axis, read
# from the empties Armillary_Axis_* (centre + 1 m along the axis, so no axis conversion has to be guessed):
#   Fixed : stand on the balcony, outer meridian ring (in the window's plane) and the horizon ring
#   A     : pinned to the meridian top and bottom, turns about the vertical; two crossing meridians
#   B     : pinned to A at left and right, turns about that line; the broad ecliptic band with its marks
#   C     : pinned to B on its band, a quarter turn from A's pins; two crossing rings
#   King  : a ceremonial ebony and gold king (2.4 m, king_mesh), floats and turns slowly on its own
#   blender -b --factory-startup --python armillary.py [-- out.fbx] [--preview]
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = "--preview" in argv
argv = [a for a in argv if not a.startswith("--")]
OUT = argv[0] if argv else os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "Lobby", "KingdomHall", "Armillary.fbx"))

CENTRE = Vector((0.0, 12.6, 7.8))       # 2.9 m in front of the window's glass, on its axis
BALCONY_Z = 3.51                        # stair_end.py LAND_Z
SEG = 160
TILT = math.radians(23.4)               # the ecliptic band's lean
KING_H = 2.4

# rings: radius, band width (along the ring's axis), thickness (radial)
FIXED_R, FIXED_W, FIXED_T = 2.6, .26, .09
A_R, A_W, A_T = 2.28, .19, .07
B_R, B_W, B_T = 1.98, .40, .07
C_R, C_W, C_T = 1.67, .15, .06

GOLD, EBONY, STONE, RUBY = 0, 1, 2, 3
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
B_N = Vector((0, math.sin(TILT), math.cos(TILT)))     # B's band: holds the X axis, leans 23.4 deg
C_AXIS = B_N.cross(X).normalized()                    # in B's band, a quarter turn from its pins


def mat(name, rgb, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = rgb + (1,)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    return m


MATS = [mat("Gold", (.80, .55, .16), 1.0, .22), mat("Ebony", (.012, .009, .007), 0.0, .08), mat("Stone", (.45, .36, .29), 0.0, .7),
        mat("Ruby", (.28, .0, .015), 0.0, .05)]


def align(n):
    """Rotation taking +Z onto n."""
    return Z.rotation_difference(n.normalized()).to_matrix().to_4x4()


def band(w, t):
    """A flat band's section (dr radial, h along the axis), closed, with a round bead along each outer edge."""
    b = min(t * .9, w * .28)                       # bead height
    bead = lambda h0: [(t / 2 + b * .45 * math.sin(math.pi * k / 6), h0 + b / 2 - b / 2 * math.cos(math.pi * k / 6))
                       for k in range(1, 6)]
    return ([(-t / 2, -w / 2), (t / 2, -w / 2)] + bead(-w / 2) + [(t / 2, -w / 2 + b), (t / 2, w / 2 - b)]
            + bead(w / 2 - b) + [(t / 2, w / 2), (-t / 2, w / 2)])


def ring(bm, R, w, t, n, mi=GOLD, M=Matrix()):
    """A band of radius R round axis n, centred on the origin."""
    prof = band(w, t)
    T = M @ align(n)
    vs = []
    for k in range(SEG):
        a = 2 * math.pi * k / SEG
        c, s = math.cos(a), math.sin(a)
        vs.append([bm.verts.new(T @ Vector(((R + dr) * c, (R + dr) * s, h))) for dr, h in prof])
    m = len(prof)
    for k in range(SEG):
        p, q = vs[k], vs[(k + 1) % SEG]
        for i in range(m):
            f = bm.faces.new((p[i], q[i], q[(i + 1) % m], p[(i + 1) % m]))
            f.material_index, f.smooth = mi, True


def box(bm, centre, axes, half, mi=GOLD):
    """A box: centre, three unit axes, half sizes."""
    cs = [centre + sum((axes[i] * half[i] * s[i] for i in range(3)), Vector())
          for s in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))]
    v = [bm.verts.new(c) for c in cs]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([v[i] for i in f]).material_index = mi


def lathe(bm, prof, M=Matrix(), seg=48, mi=GOLD):
    """Turn (r, z) points round +Z (open at the ends unless r = 0 there)."""
    vs = [[bm.verts.new(M @ Vector((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg), z)))
           for r, z in prof] for k in range(seg)]
    for k in range(seg):
        p, q = vs[k], vs[(k + 1) % seg]
        for i in range(len(prof) - 1):
            f = bm.faces.new((p[i], q[i], q[i + 1], p[i + 1]))
            f.material_index, f.smooth = mi, True


def ball(bm, c, r, mi=GOLD):
    lathe(bm, [(r * math.sin(math.pi * i / 10), -r * math.cos(math.pi * i / 10)) for i in range(11)],
          Matrix.Translation(c), 20, mi)


def pin(bm, u, r0, r1, rad=.04):
    """An axle along u from radius r0 (the child ring) out to r1 (the parent), a ball on the child's side, both ways."""
    for s in (-1, 1):
        d = u * s
        lathe(bm, [(0, r0 - .02), (rad, r0 - .02), (rad, r1 + .02), (0, r1 + .02)], align(d), 16)
        ball(bm, d * (r0 + .07), .085)
        ball(bm, d * (r1 - .02), .06)


def ticks(bm, R, w, t, n, every=7.5):
    """The ecliptic's marks on the band's outer face: short ones, a long one every 30 degrees with a ball."""
    T = align(n)
    for k in range(int(360 / every)):
        a = math.radians(k * every)
        radial = T.to_3x3() @ Vector((math.cos(a), math.sin(a), 0))
        tang = T.to_3x3() @ Vector((-math.sin(a), math.cos(a), 0))
        major = k % 4 == 0
        h = w * (.62 if major else .32)
        box(bm, radial * (R + t / 2 + .012), (tang, radial, n.normalized()), (.011, .016, h / 2))
        if major:
            ball(bm, radial * (R + t / 2 + .03), .035)


def obj(name, bm, origin=CENTRE, col=None):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in MATS:
        me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    o.location = origin
    (col or bpy.context.scene.collection).objects.link(o)
    return o


def empty(name, at, col):
    e = bpy.data.objects.new(name, None)
    e.location = at
    e.empty_display_size = .2
    col.objects.link(e)
    return e


def fixed_part():
    bm = bmesh.new()
    ring(bm, FIXED_R, FIXED_W, FIXED_T, Y)                    # the meridian, in the window's plane
    ring(bm, FIXED_R, FIXED_W * .8, FIXED_T, Z)               # the horizon
    for s in (-1, 1):
        ball(bm, X * s * FIXED_R, .13)                        # where they meet
    # crown on top: an orb and a cross
    top = FIXED_R + FIXED_T / 2
    lathe(bm, [(0, top), (.09, top), (.06, top + .08), (.05, top + .12)], seg=24)
    ball(bm, Z * (top + .24), .14)
    box(bm, Z * (top + .5), (X, Y, Z), (.03, .03, .14))
    box(bm, Z * (top + .53), (X, Y, Z), (.1, .03, .03))
    # the stand: a stone plinth on the balcony and a gold baluster up to the meridian's foot
    z0 = BALCONY_Z - CENTRE.z
    z1 = -FIXED_R - FIXED_T / 2 + .02
    box(bm, Z * (z0 + .18), (X, Y, Z), (.55, .55, .18), STONE)
    box(bm, Z * (z0 + .42), (X, Y, Z), (.45, .45, .06), STONE)
    h = z1 - (z0 + .48)
    prof = [(.34, 0), (.36, .05), (.3, .1), (.26, .12), (.27, .18), (.2, .24), (.13, .32), (.11, .42), (.16, .5),
            (.21, .56), (.18, .62), (.1, .66), (.09, .8), (.14, .86), (.13, .9), (.2, .94), (.28, 1.0), (0, 1.0)]
    lathe(bm, [(r, z0 + .48 + z * h) for r, z in prof], seg=40)
    # the cradle: four curved arms from the baluster's cup up to the horizon ring
    for k in range(4):
        a = math.radians(45 + 90 * k)
        d = Vector((math.cos(a), math.sin(a), 0))
        pts = []
        for i in range(13):
            t = i / 12
            ang = -math.pi / 2 + t * (math.pi / 2 - .05)       # along the sphere from the foot up to the horizon
            rr = FIXED_R + FIXED_T / 2 + .14 * (1 - t) + .01
            pts.append(d * (rr * math.cos(ang) * (.15 + .85 * t ** .7)) + Z * (rr * math.sin(ang)))
        for p, q in zip(pts, pts[1:]):
            mid, seg = (p + q) / 2, (q - p)
            u = seg.normalized()
            side = d.cross(Z).normalized()
            box(bm, mid, (side, u.cross(side).normalized(), u), (.035, .025, seg.length / 2 + .01))
        ball(bm, pts[-1], .07)
    return bm


def gimbal_a():
    bm = bmesh.new()
    ring(bm, A_R, A_W, A_T, Y)
    ring(bm, A_R, A_W, A_T, X)
    pin(bm, Z, A_R + A_T / 2, FIXED_R - FIXED_T / 2)
    return bm


def gimbal_b():
    bm = bmesh.new()
    ring(bm, B_R, B_W, B_T, B_N)
    ring(bm, B_R, .08, .04, C_AXIS)          # a thin colure across the band, through the pins
    ticks(bm, B_R, B_W, B_T, B_N)
    pin(bm, X, B_R + B_T / 2, A_R - A_T / 2)
    return bm


def gimbal_c():
    bm = bmesh.new()
    ring(bm, C_R, C_W, C_T, X)
    ring(bm, C_R, C_W, C_T, B_N)
    pin(bm, C_AXIS, C_R + C_T / 2, B_R - B_T / 2)
    return bm


# R623 (승규 "그냥 저 모델링이 별로야" → recommendation 1, "블렌더에서 정밀하게"): a ceremonial chess king drawn here
# instead of PR_Piece_King. Polished ebony, gold trim; an oversized gold crown (a third of its height) with crossing
# arches, an orb and a tall cross, so it still reads as a king from the lobby 25 m away. Unit height, scaled to KING_H.
KING_BODY = [(0, 0), (.200, 0), (.214, .006), (.218, .018), (.212, .028),            # foot
             (.196, .034), (.196, .056), (.205, .062), (.205, .070), (.188, .078),   # under / over the gold base band
             (.170, .088), (.150, .104), (.130, .128), (.112, .170), (.100, .230),   # the body tapers ...
             (.091, .300), (.085, .380), (.084, .450), (.088, .510), (.098, .545),   # ... slightly waisted
             (.104, .552), (.104, .556),                                            # into the lower collar
             (.090, .600), (.088, .615),                                            # the neck
             (.100, .632), (.122, .652), (.146, .672), (.160, .688), (.160, .700), (0, .700)]   # the cup under the crown
KING_GOLD_BANDS = [(.044, .207, .013), (.548, .118, .012), (.566, .132, .016), (.585, .112, .011)]   # z, r, half height


def _band(bm, z, r, h, mi=GOLD, seg=96):
    """A gold ring round the body: rounded outside, flat inside."""
    prof = [(r - .012, z - h), (r - .002, z - h), (r + .004, z - h * .5), (r + .006, z), (r + .004, z + h * .5),
            (r - .002, z + h), (r - .012, z + h)]
    lathe(bm, prof + [prof[0]], seg=seg, mi=mi)


def _petal(bm, a, r0, z0, z1, w, lean, tip, mi=GOLD):
    """One of the crown's points: a curved plate on the crown's wall at angle a, from the band (z0) to its tip (z1),
    w wide at the foot, leaning out by lean; the outline narrows to a point, thickness .012."""
    rows = 9
    vs = []
    for i in range(rows + 1):
        t = i / rows
        z = z0 + (z1 - z0) * t
        half = w / 2 * (1 - t) ** .6 * (1 + .35 * math.sin(math.pi * t))   # swells a little, then to a point
        r = r0 + lean * t * t
        ring_ = []
        for side in (-1, 1):
            for dr in (-.010, .010):
                ang = a + side * half / r
                ring_.append((side, dr, Vector(((r + dr) * math.cos(ang), (r + dr) * math.sin(ang), z))))
        vs.append([bm.verts.new(v) for _, _, v in ring_])      # order: (-,in) (-,out) (+,in) (+,out)
    for i in range(rows):
        p, q = vs[i], vs[i + 1]
        for a_, b_ in ((0, 1), (1, 3), (3, 2), (2, 0)):
            f = bm.faces.new((p[a_], p[b_], q[b_], q[a_]))
            f.material_index, f.smooth = mi, True
    f = bm.faces.new((vs[0][0], vs[0][2], vs[0][3], vs[0][1]))
    f.material_index = mi
    top = vs[-1]
    c = sum((v.co for v in top), Vector()) / 4
    ball(bm, c + Vector((math.cos(a), math.sin(a), 0)) * .002 + Vector((0, 0, tip * .8)), tip)


def _tube(bm, pts, rad, mi=GOLD, n=14):
    """One smooth tube along pts (no seams between the pieces), capped at both ends."""
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        u = d.cross(Z if abs(d.z) < .95 else X).normalized()
        v = d.cross(u)
        rings.append([bm.verts.new(p + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * rad)
                      for k in range(n)])
    for a_, b_ in zip(rings, rings[1:]):
        for k in range(n):
            f = bm.faces.new((a_[k], a_[(k + 1) % n], b_[(k + 1) % n], b_[k]))
            f.material_index, f.smooth = mi, True
    for r_ in (rings[0], rings[-1]):
        f = bm.faces.new(r_)
        f.material_index = mi


def _arch(bm, a, r0, z0, ztop, rad=.013, mi=GOLD):
    """Half of one of the crown's arches: from inside the band at angle a up over to the crown's top middle."""
    pts = []
    for i in range(33):
        t = i / 32
        ang = t * math.pi / 2
        r = r0 * math.cos(ang) * (1 + .1 * math.sin(ang * 2))
        pts.append(Vector((r * math.cos(a), r * math.sin(a), z0 + (ztop - z0) * math.sin(ang))))
    _tube(bm, pts, rad, mi)
    for i in range(10, 30, 4):                                # pearls along its upper part
        out = (pts[i] - Vector((0, 0, pts[i].z)))
        out = out.normalized() if out.length > 1e-6 else Z
        ball(bm, pts[i] + (out + Z).normalized() * rad * .95, rad * .7)


def king_mesh():
    bm = bmesh.new()
    lathe(bm, KING_BODY, seg=96, mi=EBONY)
    for z, r, h in KING_GOLD_BANDS:
        _band(bm, z, r, h)
    # the crown: a gold band, eight points (tall and short in turn), two crossing arches, a dark cap inside,
    # an orb with an equator and a tall flared cross on top
    zb0, zb1, rb = .690, .745, .166
    lathe(bm, [(rb - .02, zb0), (rb, zb0), (rb + .008, zb0 + .006), (rb + .008, zb1 - .006), (rb, zb1), (rb - .02, zb1),
               (rb - .02, zb0)], seg=96)
    for k in range(32):                                        # pearl rows on the band's edges
        a = 2 * math.pi * k / 32
        for z in (zb0 + .006, zb1 - .006):
            ball(bm, Vector(((rb + .012) * math.cos(a), (rb + .012) * math.sin(a), z)), .0065)
    for k in range(8):                                         # rubies between the pearls
        a = 2 * math.pi * (k + .5) / 8
        c = Vector(((rb + .012) * math.cos(a), (rb + .012) * math.sin(a), (zb0 + zb1) / 2))
        lathe(bm, [(0, -.016), (.011, -.012), (.016, 0), (.011, .012), (0, .016)],
              Matrix.Translation(c) @ align(Vector((math.cos(a), math.sin(a), 0))) @ Matrix.Scale(.8, 4), 12, RUBY)
    for k in range(8):
        a = 2 * math.pi * k / 8
        tall = k % 2 == 0
        _petal(bm, a, rb - .004, zb1 - .004, zb1 + (.12 if tall else .08), .11 if tall else .09, .018 if tall else .01,
               .015 if tall else .012)
    ztop = .875
    lathe(bm, [(0, zb0 + .01), (.150, zb0 + .01), (.150, zb1), (.128, .775), (.085, .800), (0, .812)], seg=64, mi=EBONY)
    for k in range(4):                                         # over the short points, so the tall ones stay clear
        _arch(bm, math.pi / 4 * (2 * k + 1), rb - .02, zb1 - .01, ztop - .02)
    ball(bm, Vector((0, 0, ztop + .018)), .034)                 # the orb
    _band(bm, ztop + .018, .034, .005, seg=48)
    lathe(bm, [(0, ztop + .048), (.012, ztop + .05), (.008, ztop + .06), (0, ztop + .062)], seg=24)
    # the cross: flared arms with a ball on each end
    zc = ztop + .062
    H, A, zm = .095, .042, zc + .062
    box(bm, Vector((0, 0, zc + H / 2)), (X, Y, Z), (.010, .009, H / 2))
    box(bm, Vector((0, 0, zm)), (X, Y, Z), (A, .009, .010))
    for v, half in ((Vector((0, 0, zc + H)), Vector((.017, .011, .006))), (Vector((A, 0, zm)), Vector((.006, .011, .017))),
                    (Vector((-A, 0, zm)), Vector((.006, .011, .017)))):
        box(bm, v, (X, Y, Z), tuple(half))
        ball(bm, v + (v - Vector((0, 0, zm))).normalized() * .016 if v.x else v + Vector((0, 0, .016)), .009)
    bmesh.ops.scale(bm, vec=(KING_H, KING_H, KING_H), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, 0, -KING_H / 2), verts=bm.verts)
    return bm


def king(col):
    return obj("Armillary_King", king_mesh(), col=col)


def build(col):
    obj("Armillary_Fixed", fixed_part(), col=col)
    obj("Armillary_A", gimbal_a(), col=col)
    obj("Armillary_B", gimbal_b(), col=col)
    obj("Armillary_C", gimbal_c(), col=col)
    king(col)
    empty("Armillary_Centre", CENTRE, col)
    empty("Armillary_Axis_A", CENTRE + Z, col)
    empty("Armillary_Axis_B", CENTRE + X, col)
    empty("Armillary_Axis_C", CENTRE + C_AXIS, col)
    tris = 0
    for o in col.objects:
        if o.type == "MESH":
            t = sum(len(f.vertices) - 2 for f in o.data.polygons)
            tris += t
            print("ARMILLARY %-18s %6d tris" % (o.name, t))
    print("ARMILLARY TRIS", tris)


def spin_pose(col, t):
    """Pose the gimbals as Unity turns them at time t (for preview renders only)."""
    o = {x.name: x for x in col.objects}
    qa = Quaternion(Z, math.radians(7 * t))
    qb = qa @ Quaternion(X, math.radians(-11 * t))
    qc = qb @ Quaternion(C_AXIS, math.radians(17 * t))
    for name, q in (("Armillary_A", qa), ("Armillary_B", qb), ("Armillary_C", qc), ("Armillary_King", Quaternion(Z, math.radians(-9 * t)))):
        o[name].rotation_mode = "QUATERNION"
        o[name].rotation_quaternion = q


if __name__ == "__main__" and not PREVIEW:
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    col = bpy.context.scene.collection
    build(col)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=False, object_types={"EMPTY", "MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE", add_leaf_bones=False)
    print("WROTE", OUT)
