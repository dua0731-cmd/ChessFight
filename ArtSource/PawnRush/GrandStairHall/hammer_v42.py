# 승규 11-04 (the grand stair hall's two swinging hammers over the curving stair, kit 11 pendulums on black blockout posts
# and beams: "해머 쪽 섬세하게 모델링 해줘"): the hammer and its stand in the hall's palace look - cream limestone
# (Palace_Stone), gold (Palace_Gold) and dark iron (Hammer_Iron) - as parts Unity puts on the blockout's places
# (Course01v4Builder.GrandStairHallHammer.cs). The blockout keeps its colliders; the parts are pictures only.
#   Hammer_Swing      under the kit pendulum's Pivot, which swings it about Unity +z: an iron axle through a gold hub, a
#                     gold yoke, a chain of 19 iron links down to a gold eye on the ball, the ball (1.1 m, its middle 6 m
#                     under the pivot, as the kit's collider) in dark iron with a gold crown round its top, a riveted
#                     gold equator band with four medallions, four riveted gold meridians and a gold cap and finial
#   Hammer_Hanger     the gold hanger the axle turns in: a block under the beam and two cheek plates with hex bolts;
#                     origin the pivot, Unity x along the beam
#   Hammer_Beam       the beam in the blockout beam's own unit cube (scaled by it to 0.4 x 0.4 x its length): dark iron
#                     with gold fillets along its top and bottom and two gold lines along each side
#   Hammer_BeamEnd    the beam's end over the post, 0.5 m back past the post's middle: a gold end plate and rosette;
#                     origin the beam's start (the post's middle line, the beam's middle height), Unity +z along the beam
#   Hammer_Bracket    an iron knee brace from the column up to the beam's underside, 1.6 m each way, with two gold
#                     scrolls inside it; origin the post's middle line at the beam's underside, Unity +z along the beam
#   Hammer_ColBase    the column's foot on the hall floor: a stone plinth with a gold band, gold tori and a stone scotia
#   Hammer_ColShaft   the fluted stone shaft (20 flutes), 1 m tall, stretched by Unity between the foot and the head
#   Hammer_ColCapital the column's head: a gold astragal, stone necking, a gold echinus and abacus and four gold ionic
#                     volutes facing along the beam; its top under the beam
# Frames: modelled with Blender X = Unity x, Y = Unity z, Z = Unity y, then turned half round about Z before export, as
# pads_v28.py, since this export brings Blender (X, Y, Z) in as Unity (-x, y, -z).
# Run: blender -b -P hammer_v42.py -- [render]   -> Assets/Resources/PawnRushGrandStairHall/PendulumHammer.fbx, renders_v42
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
OUT = os.path.join(HERE, "renders_v42")
RENDER = "render" in sys.argv
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
MATS = ("Hammer_Iron", "Palace_Gold", "Palace_Stone")
IRON, GOLD, STONE = 0, 1, 2

# The kit pendulum and the hall's blockout (GrandStairHallBuilder.Beats.cs Chandelier, 11_PendulumBall.prefab).
BALL_R, BALL_Z = 1.1, -6.0          # the ball's collider: 2.2 m, 6 m under the pivot
BEAM_BOTTOM = .4                    # the beam's underside over the pivot (its middle 0.6 over it, 0.4 thick)
SHAFT_R = .3


def M(x=0., y=0., z=0.):
    return Matrix.Translation((x, y, z))


class Part:
    def __init__(self, name):
        self.name, self.bm = name, bmesh.new()

    def faces(self, verts, mat):
        fs = {f for v in verts for f in v.link_faces}
        for f in fs:
            f.material_index = mat
        return fs

    def cone(self, r1, r2, depth, m, mat, segs=32):
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2,
                                  depth=depth, matrix=m)
        self.faces(g["verts"], mat)

    def sphere(self, r, m, mat, u=24, v=12):
        g = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=r, matrix=m)
        self.faces(g["verts"], mat)

    def box(self, sx, sy, sz, m, mat):
        g = bmesh.ops.create_cube(self.bm, size=1., matrix=m @ Matrix.Diagonal((sx, sy, sz, 1.)))
        self.faces(g["verts"], mat)

    def grid(self, rings, mat, closed_u=True, closed_v=False, caps=False):
        """Quads between rings of points (each ring closed round), optionally the rings closed into a loop and the
        first and last rings capped."""
        bm = self.bm
        vs = [[bm.verts.new(p) for p in ring] for ring in rings]
        n, k = len(vs), len(vs[0])
        made = []
        for i in range(n if closed_v else n - 1):
            a, b = vs[i], vs[(i + 1) % n]
            for j in range(k if closed_u else k - 1):
                made.append(bm.faces.new((a[j], a[(j + 1) % k], b[(j + 1) % k], b[j])))
        if caps and not closed_v:
            made.append(bm.faces.new(vs[0]))
            made.append(bm.faces.new(list(reversed(vs[-1]))))
        for f in made:
            f.material_index = mat

    def revolve(self, profile, m, mat, segs=48, closed=False):
        """A profile of (radius, z) turned round Z: ends on the axis close to a point; closed=True joins its last point
        back to its first (a ring of section)."""
        bm = self.bm
        rings = []
        for r, z in profile:
            if r < 1e-6:
                rings.append([bm.verts.new(m @ Vector((0., 0., z)))])
            else:
                rings.append([bm.verts.new(m @ Vector((r * math.cos(2 * math.pi * j / segs), r * math.sin(2 * math.pi * j / segs), z)))
                              for j in range(segs)])
        made = []
        n = len(rings)
        for i in range(n if closed else n - 1):
            a, b = rings[i], rings[(i + 1) % n]
            if len(a) == 1 and len(b) == 1:
                continue
            for j in range(segs):
                if len(a) == 1:
                    made.append(bm.faces.new((a[0], b[(j + 1) % segs], b[j])))
                elif len(b) == 1:
                    made.append(bm.faces.new((a[j], a[(j + 1) % segs], b[0])))
                else:
                    made.append(bm.faces.new((a[j], a[(j + 1) % segs], b[(j + 1) % segs], b[j])))
        for f in made:
            f.material_index = mat

    def torus(self, R, r, m, mat, segs=48, ring=12):
        self.revolve([(R + r * math.cos(2 * math.pi * i / ring), r * math.sin(2 * math.pi * i / ring)) for i in range(ring)],
                     m, mat, segs, closed=True)

    def sweep(self, path, profile, mat, closed=False, scale=None, normal=None):
        """A section (points (u, v)) carried along a path with parallel-transported frames: u along the frame's normal
        (first `normal`, or any square to the path), v along tangent x normal. `scale` sizes the section per point."""
        n = len(path)
        tangents = []
        for i in range(n):
            if closed:
                t = path[(i + 1) % n] - path[i - 1]
            else:
                t = path[min(i + 1, n - 1)] - path[max(i - 1, 0)]
            tangents.append(t.normalized())
        nrm = Vector(normal) if normal is not None else tangents[0].orthogonal()
        nrm = (nrm - tangents[0] * nrm.dot(tangents[0])).normalized()
        rings = []
        for i in range(n):
            t = tangents[i]
            nrm = (nrm - t * nrm.dot(t)).normalized()
            b = t.cross(nrm)
            s = scale[i] if scale is not None else 1.
            rings.append([path[i] + nrm * (u * s) + b * (v * s) for u, v in profile])
        self.grid(rings, mat, closed_u=True, closed_v=closed, caps=not closed)

    def prism(self, outline, y0, y1, mat):
        """An outline of (x, z) points drawn out along Y from y0 to y1."""
        bm = self.bm
        a = [bm.verts.new((x, y0, z)) for x, z in outline]
        b = [bm.verts.new((x, y1, z)) for x, z in outline]
        k = len(outline)
        made = [bm.faces.new(a), bm.faces.new(list(reversed(b)))]
        for j in range(k):
            made.append(bm.faces.new((a[j], a[(j + 1) % k], b[(j + 1) % k], b[j])))
        for f in made:
            f.material_index = mat

    def finish(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for name in MATS:
            mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            me.materials.append(mat)
        o = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(o)
        for s in bpy.context.scene.objects:
            s.select_set(False)
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
        print("PART", self.name, len(me.polygons), "faces")
        return o


def circle(r, k=10):
    return [(r * math.cos(2 * math.pi * i / k), r * math.sin(2 * math.pi * i / k)) for i in range(k)]


def rect(w, h):
    return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]


def basis(x, y, z, origin):
    """A matrix whose local X, Y, Z are the given directions at `origin`."""
    m = Matrix((x.normalized(), y.normalized(), z.normalized())).transposed().to_4x4()
    m.translation = origin
    return m


def on_ball(lat, lon, r):
    """A point on a sphere round the ball's middle: latitude up from the equator, longitude round from +X."""
    la, lo = math.radians(lat), math.radians(lon)
    return Vector((r * math.cos(la) * math.cos(lo), r * math.cos(la) * math.sin(lo), BALL_Z + r * math.sin(la)))


def zone(p, lat0, lat1, r_out, r_in, mat, segs=72):
    """A band of the ball's surface between two latitudes, r_out over it and r_in under it (hidden in the ball)."""
    k = 10
    outer = [(r_out * math.cos(math.radians(a)), BALL_Z + r_out * math.sin(math.radians(a)))
             for a in [lat0 + (lat1 - lat0) * i / k for i in range(k + 1)]]
    inner = [(r_in * math.cos(math.radians(a)), BALL_Z + r_in * math.sin(math.radians(a)))
             for a in [lat1 + (lat0 - lat1) * i / k for i in range(k + 1)]]
    p.revolve(outer + inner, Matrix.Identity(4), mat, segs, closed=True)


# ------------------------------------------------------------------ the swing
def swing():
    p = Part("Hammer_Swing")
    # Axle (through the hanger's cheeks), hub and the yoke's two straps down to the chain's bolt.
    p.cone(.09, .09, .9, Matrix.Rotation(math.pi / 2, 4, "X"), IRON, 24)
    p.cone(.16, .16, .32, Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 32)
    for y in (-.13, .1):
        strap = [(-.15, .05), (.15, .05), (.08, -.48)] + [(.08 * math.cos(a), -.5 - .08 * math.sin(a))
                                                          for a in [math.pi * i / 8 for i in range(9)]][1:] + [(-.08, -.48)]
        p.prism(strap, y, y + .03, GOLD)
    p.cone(.04, .04, .32, M(0, 0, -.5) @ Matrix.Rotation(math.pi / 2, 4, "X"), IRON, 16)
    for y in (-.17, .17):
        p.cone(.065, .065, .04, M(0, y, -.5) @ Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 6)

    # The chain: interlocking stadium links, each turned a quarter from the last, from the bolt to the ball's eye. Each
    # link's top wire rests on the wire above it (the bolt, then the last link's foot), the eye's on the last link's
    # foot, so their middle lines are two wire radii apart there; the links' length is what fits that exactly.
    w, a = .035, .075
    eye_r = .11
    eye_z = BALL_Z + BALL_R + .05 + w + eye_r          # the eye ring's middle, its foot on the boss
    top = -.5 + .04 + w                                  # the first link's middle line at its top, on the bolt
    links = 19
    half = (top - eye_z - eye_r + 2 * w * links) / (2 * links)   # half a link's length along its middle line
    h = half - a                                         # half its straight sides
    pitch = 2 * half - 2 * w
    z0 = top - half
    print("CHAIN", links, "links, length %.3f, pitch %.3f" % (2 * half, pitch))
    for i in range(links):
        zc = z0 - i * pitch
        path = []
        for j in range(12):                               # top semicircle, then bottom
            t = math.pi * j / 11
            path.append(Vector((a * math.cos(t), 0., zc + h + a * math.sin(t))))
        for j in range(12):
            t = math.pi + math.pi * j / 11
            path.append(Vector((a * math.cos(t), 0., zc - h + a * math.sin(t))))
        if i % 2:
            rot = Matrix.Rotation(math.pi / 2, 3, "Z")
            path = [Vector((0, 0, zc)) + rot @ (q - Vector((0, 0, zc))) for q in path]
        p.sweep(path, circle(w, 10), IRON, closed=True)
    # The eye: a gold ring square to the last link (a link in XZ when even, YZ when odd), on a boss on the ball's top.
    p.torus(eye_r, w, M(0, 0, eye_z) @ Matrix.Rotation(math.pi / 2, 4, "X" if (links - 1) % 2 else "Y"), GOLD, 32, 10)
    p.revolve([(0, BALL_Z + BALL_R - .06), (.22, BALL_Z + BALL_R - .06), (.2, BALL_Z + BALL_R + .02),
               (.12, BALL_Z + BALL_R + .06), (0, BALL_Z + BALL_R + .06)], Matrix.Identity(4), GOLD, 32)

    # The ball.
    p.sphere(BALL_R, M(0, 0, BALL_Z), IRON, 64, 32)
    # A gold crown round its top: a band, eight points tipped with balls, studs between them.
    zone(p, 52, 62, BALL_R + .035, BALL_R - .03, GOLD)
    for k in range(8):
        lon = 22.5 + 45 * k
        foot = on_ball(62, lon, BALL_R + .02)
        out = (foot - Vector((0, 0, BALL_Z))).normalized()
        up = (out * .35 + Vector((0, 0, 1)) * .94).normalized()
        tip = foot + up * .3
        side = Vector((0, 0, 1)).cross(out).normalized()
        p.cone(.075, .012, .3, basis(side, up.cross(side), up, (foot + tip) / 2), GOLD, 12)
        p.sphere(.05, M(*tip), GOLD, 12, 6)
        p.sphere(.04, M(*on_ball(57, lon + 22.5, BALL_R + .04)), GOLD, 10, 5)
    # The equator band with rivets, and four medallions on it between the meridians.
    zone(p, -6, 6, BALL_R + .03, BALL_R - .03, GOLD)
    for k in range(32):
        lon = 360 * k / 32
        if k % 8 in (3, 4, 5):
            continue                                      # room for the medallions
        p.sphere(.028, M(*on_ball(0, lon, BALL_R + .035)), GOLD, 8, 5)
    for k in range(4):
        lon = 45 + 90 * k
        c = on_ball(0, lon, BALL_R + .04)
        out = (c - Vector((0, 0, BALL_Z))).normalized()
        side = Vector((0, 0, 1)).cross(out).normalized()
        frame = basis(side, Vector((0, 0, 1)), out, c)
        p.cone(.21, .19, .06, frame, GOLD, 32)
        p.torus(.19, .022, frame @ M(0, 0, .03), GOLD, 32, 8)
        p.sphere(.07, frame @ M(0, 0, .03), GOLD, 16, 8)
        for j in range(8):
            t = 2 * math.pi * j / 8
            p.sphere(.025, frame @ M(.12 * math.cos(t), .12 * math.sin(t), .035), GOLD, 8, 4)
    # Four riveted meridians from the crown down to the cap.
    for k in range(4):
        lon = 90 * k
        path = [on_ball(lat, lon, BALL_R + .015) for lat in [52 - (52 + 58) * i / 40 for i in range(41)]]
        out0 = (path[0] - Vector((0, 0, BALL_Z))).normalized()
        p.sweep(path, rect(.035, .1), GOLD, normal=out0)
        for lat in range(42, -50, -14):
            if abs(lat) < 8:
                continue
            p.sphere(.026, M(*on_ball(lat, lon, BALL_R + .04)), GOLD, 8, 5)
    # The cap and the finial under it.
    zone(p, -88, -58, BALL_R + .03, BALL_R - .03, GOLD)    # its hole at the pole under the finial
    zb = BALL_Z - BALL_R - .03
    p.revolve([(0, zb + .02), (.16, zb + .02), (.14, zb - .04), (.09, zb - .07), (.11, zb - .1), (.08, zb - .13),
               (.03, zb - .2), (0, zb - .3)], Matrix.Identity(4), GOLD, 32)
    return p.finish()


# ------------------------------------------------------------------ the stand
def hanger():
    p = Part("Hammer_Hanger")
    p.box(.8, 1.06, .08, M(0, 0, BEAM_BOTTOM - .04), GOLD)
    for y0 in (-.49, .44):
        outline = [(-.26, BEAM_BOTTOM - .08), (.26, BEAM_BOTTOM - .08)] + \
                  [(.26 * math.cos(-math.pi * i / 16), .26 * math.sin(-math.pi * i / 16)) for i in range(17)]
        p.prism(outline, y0, y0 + .05, GOLD)
        y = y0 - .03 if y0 < 0 else y0 + .08
        p.cone(.1, .1, .06, M(0, y, 0) @ Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 6)
        for x in (-.16, .16):
            p.sphere(.025, M(x, y0 - .005 if y0 < 0 else y0 + .055, BEAM_BOTTOM - .16), GOLD, 8, 5)
    return p.finish()


def beam():
    """In the blockout beam's unit cube: X its width, Y its length, Z its height."""
    p = Part("Hammer_Beam")
    p.box(1., 1., 1., M(), IRON)
    for z in (-.56, .56):
        p.box(1.2, 1., .12, M(0, 0, z), GOLD)
    for z in (-.25, .25):
        p.box(1.05, 1., .06, M(0, 0, z), GOLD)
    return p.finish()


def beam_end():
    p = Part("Hammer_BeamEnd")
    p.box(.4, .5, .4, M(0, -.25, 0), IRON)
    for z in (-.224, .224):
        p.box(.48, .5, .048, M(0, -.25, z), GOLD)
    for z in (-.1, .1):
        p.box(.42, .5, .024, M(0, -.25, z), GOLD)
    p.box(.48, .04, .48, M(0, -.52, 0), GOLD)
    face = Matrix.Rotation(math.pi / 2, 4, "X")
    p.cone(.15, .13, .05, M(0, -.565, 0) @ face, GOLD, 32)
    p.sphere(.065, M(0, -.6, 0), GOLD, 16, 8)
    for j in range(8):
        t = 2 * math.pi * j / 8
        p.sphere(.045, M(.15 * math.cos(t), -.585, .15 * math.sin(t)), GOLD, 10, 5)
    return p.finish()


def scroll(p, centre, r0, r1, turns, plane_u, plane_v, thick0, thick1, mat, start=0.):
    """A flat spiral from radius r0 in to r1 over `turns`, in the plane of plane_u and plane_v, its outer end at the
    angle `start` from plane_u."""
    n = int(48 * turns)
    path, scale = [], []
    for i in range(n + 1):
        f = i / n
        t = start + 2 * math.pi * turns * f
        r = r0 + (r1 - r0) * f
        path.append(centre + plane_u * (r * math.cos(t)) + plane_v * (r * math.sin(t)))
        scale.append(thick0 + (thick1 - thick0) * f)
    p.sweep(path, circle(1., 8), mat, scale=scale)


def bracket():
    """Origin the post's middle line at the beam's underside, Y along the beam, Z up."""
    p = Part("Hammer_Bracket")
    reach, drop = 1.6, 1.6
    start_y = SHAFT_R + .01
    path = [Vector((0., reach - (reach - start_y) * math.cos(t), -drop + drop * math.sin(t)))
            for t in [math.pi / 2 * i / 40 for i in range(41)]]
    p.sweep(path, rect(.1, .08), IRON, normal=(0, 1, 0))
    # Gold collars where it meets the column and the beam.
    p.box(.12, .06, .14, M(0, start_y + .02, -drop + .02), GOLD)
    p.box(.12, .14, .05, M(0, reach - .02, -.03), GOLD)
    # Two gold scrolls hanging under the arm, each one's outer end on it, and a rosette on the arc.
    U, V = Vector((0, 1, 0)), Vector((0, 0, 1))

    def on_arm(c, d):
        """How far from c along d (in Y, Z) the arm's middle line is: its quarter ellipse round (reach, -drop)."""
        lo, hi = 0., 2.
        for _ in range(40):
            m = (lo + hi) / 2
            y, z = c[0] + d[0] * m, c[1] + d[1] * m
            if ((y - reach) / (reach - start_y)) ** 2 + ((z + drop) / drop) ** 2 < 1:
                lo = m
            else:
                hi = m
        return lo

    for (cy, cz), r1, turns, th in (((1.05, -.8), .06, 1.6, (.03, .016)), ((.62, -1.28), .04, 1.4, (.024, .014))):
        d = Vector((-.6, .8)).normalized()
        r0 = on_arm((cy, cz), d)
        scroll(p, Vector((0, cy, cz)), r0, r1, turns, U, V, th[0], th[1], GOLD, math.atan2(d.y, d.x))
    mid = path[20]
    p.cone(.07, .06, .14, M(*mid) @ Matrix.Rotation(math.pi / 2, 4, "Y"), GOLD, 24)
    for x in (-.075, .075):
        p.sphere(.045, M(x, mid.y, mid.z), GOLD, 12, 6)
    return p.finish()


def col_base():
    p = Part("Hammer_ColBase")
    p.box(1., 1., .3, M(0, 0, .15), STONE)
    p.box(1.04, 1.04, .04, M(0, 0, .3), GOLD)
    p.torus(.4, .07, M(0, 0, .39), GOLD, 64, 12)
    p.revolve([(0, .45), (.37, .45), (.35, .48), (.34, .51), (.35, .53), (0, .53)], Matrix.Identity(4), STONE, 64)
    p.torus(.35, .045, M(0, 0, .57), GOLD, 64, 10)
    p.revolve([(0, .6), (.33, .6), (SHAFT_R + .005, .66), (SHAFT_R, .68), (0, .68)], Matrix.Identity(4), STONE, 64)
    return p.finish()


BASE_TOP = .68
CAPITAL_H = .52


def col_shaft():
    """1 m tall from Z 0; Unity stretches it."""
    p = Part("Hammer_ColShaft")
    flutes, per = 20, 10
    ring = []
    for i in range(flutes * per):
        th = 2 * math.pi * i / (flutes * per)
        f = (i % per) / per
        dip = 0. if f < .2 else math.sin(math.pi * (f - .2) / .8)
        r = SHAFT_R - .028 * dip
        ring.append((r * math.cos(th), r * math.sin(th)))
    rings = [[Vector((x, y, z)) for x, y in ring] for z in (0., 1.)]
    p.grid(rings, STONE, caps=True)
    return p.finish()


def col_capital():
    p = Part("Hammer_ColCapital")
    p.torus(SHAFT_R + .01, .035, M(0, 0, .035), GOLD, 64, 10)
    p.revolve([(0, .06), (SHAFT_R, .06), (SHAFT_R, .21), (0, .21)], Matrix.Identity(4), STONE, 64)
    p.revolve([(0, .21), (.32, .21), (.32, .25), (.36, .27), (.4, .31), (.42, .36), (.42, .38), (0, .38)],
              Matrix.Identity(4), GOLD, 64)
    p.box(.96, .96, .1, M(0, 0, .43), GOLD)
    p.box(.9, .9, .04, M(0, 0, .5), GOLD)
    # Ionic volutes on the two faces along the beam (±Y), a bolster between each pair.
    for x in (-.38, .38):
        p.cone(.11, .11, .84, M(x, 0, .27) @ Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 24)
        for y in (-.43, .43):
            out = Vector((math.copysign(1, x), 0, 0))
            scroll(p, Vector((x, y, .27)), .13, .02, 2.2, out, Vector((0, 0, 1)), .03, .014, GOLD)
            p.sphere(.035, M(x, y, .27), GOLD, 10, 5)
    return p.finish()


# ------------------------------------------------------------------ build, export, render
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)
parts = [swing(), hanger(), beam(), beam_end(), bracket(), col_base(), col_shaft(), col_capital()]

turn = Matrix.Rotation(math.pi, 4, "Z")
for o in parts:
    o.data.transform(turn)
for s in bpy.context.scene.objects:
    s.select_set(s in parts)
bpy.context.view_layer.objects.active = parts[0]
path = os.path.join(RES, "PendulumHammer.fbx")
bpy.ops.export_scene.fbx(filepath=path, **FBX)
print("EXPORT", path, sum(len(o.data.polygons) for o in parts), "faces")
for o in parts:
    o.data.transform(turn)                              # back to the modelling frame for the pictures

if RENDER:
    os.makedirs(OUT, exist_ok=True)
    look = {"Hammer_Iron": ((.09, .09, .11, 1), .85, .38), "Palace_Gold": ((.86, .64, .28, 1), 1., .28),
            "Palace_Stone": ((.86, .82, .72, 1), 0., .7)}
    for name, (col, metal, rough) in look.items():
        mat = bpy.data.materials[name]
        try:
            mat.use_nodes = True
        except AttributeError:
            pass
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
    # The stand as the hall puts it: the post 6.2 m in from the pivot, the pivot 7.3 m over a 9 m walkway.
    by = {o.name: o for o in parts}
    pivot_z, post_y = 16., -6.2
    floor = -4.
    capital_top = pivot_z + BEAM_BOTTOM
    shaft_h = capital_top - CAPITAL_H - (floor + BASE_TOP)

    def place(o, m):
        o.matrix_world = m

    place(by["Hammer_ColBase"], M(0, post_y, floor))
    place(by["Hammer_ColShaft"], M(0, post_y, floor + BASE_TOP) @ Matrix.Diagonal((1, 1, shaft_h, 1)))
    place(by["Hammer_ColCapital"], M(0, post_y, capital_top - CAPITAL_H))
    place(by["Hammer_BeamEnd"], M(0, post_y, pivot_z + .6))
    place(by["Hammer_Bracket"], M(0, post_y, capital_top))
    length = 9.
    place(by["Hammer_Beam"], M(0, post_y + length / 2, pivot_z + .6) @ Matrix.Diagonal((.4, length, .4, 1)))
    # The hanger's X is the pendulum's x, along the beam (Y here).
    along = Matrix.Rotation(math.pi / 2, 4, "Z")
    place(by["Hammer_Hanger"], M(0, 0, pivot_z) @ along)
    place(by["Hammer_Swing"], M(0, 0, pivot_z) @ along @ Matrix.Rotation(math.radians(28), 4, "Y"))
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, floor))
    # The walkway under it, along the way (the axle, X here), its top 7.45 m under the pivot.
    bpy.ops.mesh.primitive_cube_add(location=(0, .6, pivot_z - 7.45 - .6))
    walk = bpy.context.active_object
    walk.scale = (8, 2.4, .6)
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = 1100, 900
    world = bpy.data.worlds.new("w") if sc.world is None else sc.world
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.32, .3, .28, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), math.radians(10), math.radians(40)))
    bpy.context.active_object.data.energy = 4
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(name, eye, at, lens):
        cam.location = eye
        d = Vector(at) - Vector(eye)
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam_data.lens = lens
        sc.render.filepath = os.path.join(OUT, name)
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)

    shot("hammer_all.png", (17, -15, 13), (0, -2.6, 11), 22)
    shot("hammer_ball.png", (5.5, 2.4, 11.5), (0, -.8, 10.8), 50)
    shot("hammer_head.png", (2.6, -2.4, 16.4), (0, -5.4, 15.4), 32)
    shot("hammer_pivot.png", (2.2, 1.6, 16.2), (0, 0, 15.9), 45)
