# 승규's ball-and-chain asset (source/ball_and_chain.glb, a Tripo mesh: one piece, no UVs, no texture) made the royal
# water garden's two robot arms over the dyke (beat 5, R432), painted as its two reference renders: a cream block
# pillar on a plinth with a moss-green band, a royal blue banner with a gold crown on each side, a gold bracket on top
# reaching to a gold hub with a blue boss, a wooden handle with gold collars, a ball of blue gores between gold ribs.
#
# The mesh is cut in two under the hub - the pillar, bracket and hub drum stand still, the handle and ball swing - and
# fitted to the graybox robot (RoyalWaterGardenBuilder.Robot, RobotSweep):
#   pivot     the hub's centre (asset x -0.055, z 0.806) on the robot's pivot (section x, z 27, y 9.6); scaled 7.25,
#             which also puts the pillar's foot on the dyke (y 3.8)
#   pillar    north of the dyke's wall (its middle at z 31.4, behind the wall's z 30.5), widened east-west over the
#             gantry's two posts; only the bracket's plain tube is drawn out to reach the pivot 4.4 m south
#   swinging  hanging straight down at rest (the graybox arm's rest pose): the handle drawn out so the ball's middle
#             is 4.7 m under the pivot, the ball 0.9 m round (the graybox hand's middle 4.9 m, knocking within 1.1 m)
# Paint: planar colour edges are cut into the mesh first (_cut_bands), so collars, bands and stripes are straight.
# Asset frame: x from the pillar to the ball, y its depth, z up. Section frame (Blender): X east, Y north, Z up.
import math
import os
import bmesh
import bpy
import numpy as np
from mathutils import Vector

GLB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "source", "ball_and_chain.glb")
PIVOT = Vector((-.055, .806))          # the hub drum's centre (x, z), fitted to its rim
BALL = Vector((.33, .32))              # the ball's centre (x, z)
U = (BALL - PIVOT).normalized()        # along the handle, pivot -> ball
N = Vector((-U.y, U.x))                # across it, in the swing plane
L = (BALL - PIVOT).length
K = 7.25                               # the pillar, bracket, hub and handle's thickness
# The hub is a round drum on the swing's axis: it stands still with the bracket (turning, it would look the same),
# so the cut between still and swinging runs below its rim, through the handle's neck. HUB_R takes in the rim.
HUB_R, SWING_T0, STRETCH_T0, BALL_T0, BALL_R = .108, .09, .135, .43, .17
LIFT = .017                            # the handle's neck raised to meet the drum's rim (it hung 0.09 m short)
# The ball's middle 4.7 m under the pivot and 0.9 m round: its foot clears the dyke (y 3.8) by 0.2 m at the bottom of
# the swing; the graybox hand's middle is 4.9 m under the pivot and RobotSweep knocks within 1.1 m of it, so the
# drawn ball sits inside the knock's reach.
ARM, BALL_RADIUS = 4.7, .9
KB = BALL_RADIUS / BALL_R          # refined from the fitted ball in make_hammers
PIVOT_Y, PIVOT_Z = 27.0, 9.6
PILLAR_Y = 31.4                        # the pillar's middle, north of the dyke's wall
PILLAR_X0, PILLAR_X1 = -.36, -.23      # the asset pillar's middle and its edge towards the bracket
# R435 (승규 11-05, "이렇게 들어가버려"): the arm rests raised 60 degrees to the north and winds up to 82
# (RoyalWaterGardenRules.RobotAngle), so anything north of the pivot in its swing plane is struck - the asset's
# pillar and in-plane bracket were. The pillar stands beside the swing instead, east of it (x +1.15..+2.95 of the
# robot, over the gantry's east post; the ball is 0.9 m round), and the bracket is a gold axle out of the hub's east
# face, turning north into the pillar (_bracket): on the swing's axis, nothing for the arm to hit.
PILLAR_HALF_X, PILLAR_DX = .9, 2.05
KX = K                                 # the pillar's east-west scale, set from the asset in make_hammers


def _load():
    bpy.ops.import_scene.gltf(filepath=GLB)
    ob = [o for o in bpy.context.selected_objects if o.type == "MESH"][0]
    me = ob.data
    me.transform(ob.matrix_world)
    bm = bmesh.new()
    bm.from_mesh(me)
    # Drop the handful of stray triangles Tripo left floating.
    seen, small = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        comp, stack = [], [v]
        seen.add(v)
        while stack:
            a = stack.pop()
            comp.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b not in seen:
                    seen.add(b)
                    stack.append(b)
        if len(comp) < 100:
            small += comp
    bmesh.ops.delete(bm, geom=small, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    # About a quarter of the 235 thousand triangles is plenty at the dyke's distance.
    dec = ob.modifiers.new("dec", "DECIMATE")
    dec.ratio = .26
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=dec.name)
    return ob


def _swinging(c):
    rel = Vector((c.x, c.z)) - PIVOT
    t, s = rel.dot(U), rel.dot(N)
    return rel.length >= HUB_R and t > SWING_T0 and c.x > -.12 and abs(s) < .22


def _static_pos(p, x0):
    """The pillar and bracket: uniform K about the pivot, the bracket (between the pillar's edge and the hub's rim)
    drawn out so the pillar stands at PILLAR_Y."""
    x = p.x
    if _in_pillar(p):
        y = PILLAR_Y + (PILLAR_X0 - x) * K
        return Vector((x0 + PILLAR_DX + p.y * KX, y, PIVOT_Z + (p.z - PIVOT.y) * K))
    elif x < TUBE_X1:
        f = (x - TUBE_X0) / (TUBE_X1 - TUBE_X0)
        y0 = PILLAR_Y + (PILLAR_X0 - TUBE_X0) * K
        y1 = PIVOT_Y + (PIVOT.x - TUBE_X1) * K
        y = y0 + (y1 - y0) * f
        kx = KX + (K - KX) * min(1.0, f * 3)   # the tube narrows from the pillar's width to the hub's
    else:
        y = PIVOT_Y + (PIVOT.x - x) * K
        kx = K
    return Vector((x0 + p.y * kx, y, PIVOT_Z + (p.z - PIVOT.y) * K))


# The bracket from the pillar's top plate to the hub: a plain round tube (asset x -0.245..-0.205, all above z 0.70),
# then a knuckle and a short tube into the hub. Only the plain tube is drawn out, so the knuckle keeps its shape.
TUBE_X0, TUBE_X1, BRACKET_Z = -.245, -.205, .70


def _in_pillar(c):
    return c.x <= TUBE_X0 or (c.x < TUBE_X1 and c.z <= BRACKET_Z)


def _swing_pos(p, x0):
    """The handle and ball, hanging straight down from the pivot: the neck and top collar as they are, the wooden
    handle drawn out, the ball scaled to BALL_RADIUS."""
    rel = Vector((p.x, p.z)) - PIVOT
    t, s = rel.dot(U), rel.dot(N)
    if t < STRETCH_T0:
        T, S, D = (t - LIFT) * K, s * K, p.y * K
    elif t < BALL_T0:
        top = ARM - (L - BALL_T0) * KB
        t0 = (STRETCH_T0 - LIFT) * K
        T = t0 + (t - STRETCH_T0) * (top - t0) / (BALL_T0 - STRETCH_T0)
        S, D = s * K, p.y * K
    else:
        T, S, D = ARM + (t - L) * KB, s * KB, p.y * KB
    return Vector((x0 + D, PIVOT_Y - S, PIVOT_Z - T))


PILLAR_BANDS = (.09, .125, .69, .755)      # plinth | moss band | body | cap | gold top (asset z)
HANDLE_BANDS = (.135, .40, BALL_T0, .47)   # gold collar | wood | gold collar | ball's neck | ball (along U)
U3 = Vector((U.x, 0, U.y))
E1, E2 = Vector((-U.y, 0, U.x)), Vector((0, 1, 0))   # across the handle, for the ball's azimuth
STRIPE = 6.0                                         # half the width of a gold stripe, degrees round the ball
CAP_TOP, CAP_BOTTOM = -62.0, 70.0                    # the ball's gold neck and foot (latitude; + away from the hub)


class Ball:
    """The ball as Tripo made it: a sphere fitted to it, and its eight shallow grooves (the azimuths where it is
    narrowest round its equator), which the reference paints as gold stripes between blue gores."""

    def __init__(self, verts):
        b = np.array([v[:] for v in verts])
        A = np.c_[2 * b, np.ones(len(b))]
        c, *_ = np.linalg.lstsq(A, (b ** 2).sum(1), rcond=None)
        self.centre = Vector(c[:3])
        self.radius = math.sqrt(c[3] + (c[:3] ** 2).sum())
        az, lat, r = self.polar(b)
        band = np.abs(lat) < 30
        prof = np.array([r[band & (np.abs((az - a + 180) % 360 - 180) < 4)].mean() for a in range(360)])
        grooves = []
        for a in np.argsort(prof):
            if all(abs((a - g + 180) % 360 - 180) > 25 for g in grooves):
                grooves.append(float(a))
            if len(grooves) == 8:
                break
        self.grooves = sorted(grooves)

    def polar(self, pts):
        d = pts - np.array(self.centre[:])
        ax = d @ np.array(U3[:])
        perp = d - np.outer(ax, np.array(U3[:]))
        az = np.degrees(np.arctan2(perp @ np.array(E2[:]), perp @ np.array(E1[:]))) % 360
        r = np.linalg.norm(d, axis=1)
        lat = np.degrees(np.arcsin(np.clip(ax / np.maximum(r, 1e-9), -1, 1)))
        return az, lat, r

    def paint(self, c):
        az, lat, _ = self.polar(np.array([c[:]]))
        if lat[0] < CAP_TOP or lat[0] > CAP_BOTTOM:
            return "gold"
        near = min(abs((az[0] - g + 180) % 360 - 180) for g in self.grooves)
        return "gold" if near < STRIPE else "blue"


def _paint(c, swing, ball):
    """The reference renders' colours, by where a face is on the asset."""
    if not swing:
        if _in_pillar(c) and not (c.z > BRACKET_Z and c.x > TUBE_X0 - .02):   # the pillar and its top plate
            if c.z < PILLAR_BANDS[0]:
                return "stone2"
            if c.z < PILLAR_BANDS[1]:
                return "moss"
            if c.z > PILLAR_BANDS[3]:
                return "gold"
            if c.z > PILLAR_BANDS[2]:
                return "stone3"
            return "stone"
        if (Vector((c.x, c.z)) - PIVOT).length < .05 and abs(c.y) > .025:
            return "blue"   # the hub's boss, both faces
        return "gold"       # the bracket and the hub
    t = (Vector((c.x, c.z)) - PIVOT).dot(U)
    if t < HANDLE_BANDS[2]:
        return "gold" if t < HANDLE_BANDS[0] or t > HANDLE_BANDS[1] else "wood"
    if t < HANDLE_BANDS[3]:
        return "gold"
    return ball.paint(c)


def _cut_bands(ob, swing_of, ball):
    """Slice the mesh along every colour boundary that is a plane, so the paint's edges are straight lines rather
    than the decimated triangles' zigzag: the pillar's bands (z), the handle's collars (across U), the ball's neck
    and foot (across U) and its stripes' edges (planes through the ball's axis)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)

    def faces(pick):
        fs = [f for f in bm.faces if pick(f.calc_center_median(), swing_of(f.calc_center_median()))]
        return list({e for f in fs for e in f.edges}) + list({v for f in fs for v in f.verts}) + fs

    def cut(geom, co, no):
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=co, plane_no=no)

    for z in PILLAR_BANDS:
        cut(faces(lambda c, sw: not sw and _in_pillar(c)), Vector((0, 0, z)), Vector((0, 0, 1)))
    # R434: the bracket's tube leaves the top plate along a clean line (where it starts being drawn out, and where its
    # gold starts), not through the decimated triangles.
    for x in (TUBE_X0 - .02, TUBE_X0):
        cut(faces(lambda c, sw: not sw and c.z > BRACKET_Z and c.x > -.3 and c.x < TUBE_X1), Vector((x, 0, 0)), Vector((1, 0, 0)))
    cut(faces(lambda c, sw: not sw and c.x > -.3 and c.x < TUBE_X1), Vector((0, 0, BRACKET_Z)), Vector((0, 0, 1)))
    piv3 = Vector((PIVOT.x, 0, PIVOT.y))
    for t in HANDLE_BANDS:
        cut(faces(lambda c, sw: sw), piv3 + U3 * t, U3)
    for lat in (CAP_TOP, CAP_BOTTOM):
        cut(faces(lambda c, sw: sw and (Vector((c.x, c.z)) - PIVOT).dot(U) >= HANDLE_BANDS[3]),
            ball.centre + U3 * ball.radius * math.sin(math.radians(lat)), U3)
    for g in ball.grooves:
        for edge in (g - STRIPE, g + STRIPE):
            a = math.radians(edge)
            radial = E1 * math.cos(a) + E2 * math.sin(a)
            # The plane through the axis at this azimuth; only the half on this side of the axis is wanted, so cut
            # only faces whose azimuth is within 90 degrees of it.
            def near(c, sw, radial=radial):
                if not sw or (Vector((c.x, c.z)) - PIVOT).dot(U) < HANDLE_BANDS[3]:
                    return False
                d = Vector(c[:]) - ball.centre
                return (d - U3 * d.dot(U3)).dot(radial) > 0
            cut(faces(near), ball.centre, U3.cross(radial))
    bm.to_mesh(ob.data)
    bm.free()


DRUM_R, DRUM_HALF = .091, .09         # the hub drum's radius (fitted to its rim) and half its depth (asset units)


def _neck_stub(c):
    """What is left on the still drum of the handle's neck (it left the drum slanting, towards the ball as Tripo set
    it): the swinging neck hangs straight down instead, so the stub goes and the drum band closes the drum."""
    rel = Vector((c.x, c.z)) - PIVOT
    d = rel.length
    return .08 < d < .2 and rel.dot(U) > d * math.cos(math.radians(42)) and abs(c.y) < DRUM_HALF - .006


HUB_CUT = .1                          # R434: the asset's drum (everything still within this of the pivot) is redrawn


def _hub_hidden(c):
    return (Vector((c.x, c.z)) - PIVOT).length < HUB_CUT


def _drum(bm, x0, gold, blue, segs=56):
    """R434 (승규 11-05, "연결부 부근이 깨져"): the hub drum modelled clean instead of the asset's (its rim was cut by
    the still / swinging split and Unity drew through it): a gold drum on the swing's axis (x), the bracket's tube
    ending flush in its side, on each face a gold rim, a recessed royal blue disc and a gold knob. Closed, its
    normals outward (Unity draws front faces only)."""
    r, h = HUB_CUT * K + .01, DRUM_HALF * K
    disc, knob = r * .64, r * .23
    # (radius, x) round the profile from one knob's tip to the other's; each segment's material is its start's.
    side = [((0, h + .13), gold), ((knob * .55, h + .12), gold), ((knob, h + .06), gold), ((knob, h - .03), blue),
            ((disc, h - .03), gold), ((disc, h), gold), ((r - .07, h), gold), ((r, h - .07), gold)]
    prof = [((p[0], -p[1]), m) for p, m in side] + [((p[0], p[1]), m) for p, m in reversed(side)]
    # Back up the other face the same segments come in reverse, each still with its inner point's material.
    mats = [m for _, m in side] + [side[len(side) - 1 - k][1] for k in range(1, len(side))]
    rings = []
    for (rr, x), _ in prof:
        if rr < 1e-6:
            rings.append([bm.verts.new((x0 + x, PIVOT_Y, PIVOT_Z))])
        else:
            rings.append([bm.verts.new((x0 + x, PIVOT_Y + rr * math.cos(2 * math.pi * i / segs),
                                        PIVOT_Z + rr * math.sin(2 * math.pi * i / segs))) for i in range(segs)])
    made = []
    for k, (a, b) in enumerate(zip(rings, rings[1:])):
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(segs):
            j = (i + 1) % segs
            if len(a) == 1:
                f = bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                f = bm.faces.new((a[i], b[0], a[j]))
            else:
                f = bm.faces.new((a[i], a[j], b[j], b[i]))
            f.material_index = mats[k]
            f.smooth = k == len(side) - 1      # the round band; the faces' rings stay flat
            made.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=made)


def _tube(bm, a, b, r, mat, segs=32, smooth=True):
    """A closed round tube from a to b (normals outward)."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = d.cross(Vector((0, 0, 1)) if abs(d.z) < .9 else Vector((1, 0, 0))).normalized()
    v = d.cross(u)
    rings = [[bm.verts.new(p + (u * math.cos(2 * math.pi * i / segs) + v * math.sin(2 * math.pi * i / segs)) * r)
              for i in range(segs)] for p in (a, b)]
    made = [bm.faces.new(list(reversed(rings[0]))), bm.faces.new(rings[1])]
    for i in range(segs):
        j = (i + 1) % segs
        made.append(bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i])))
    for f in made:
        f.material_index = mat
        f.smooth = smooth
    made[0].smooth = made[1].smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=made)


def _bracket(bm, x0, gold):
    """R435: the hub's support - a gold axle on the swing's axis out of the drum's east face, an elbow, and an arm
    north (at the asset tube's height and girth, so it closes the tube's root on the pillar's top plate) into the
    pillar beside the swing; gold collars along it."""
    xe = x0 + PILLAR_DX
    arm_z, arm_r = 9.25, .31
    root_y = PILLAR_Y + (PILLAR_X0 - TUBE_X0) * K + .08
    _tube(bm, (x0 + DRUM_HALF * K - .05, PIVOT_Y, PIVOT_Z), (xe, PIVOT_Y, PIVOT_Z), .28, gold)
    _tube(bm, (x0 + DRUM_HALF * K + .12, PIVOT_Y, PIVOT_Z), (x0 + DRUM_HALF * K + .3, PIVOT_Y, PIVOT_Z), .36, gold)
    _tube(bm, (xe, PIVOT_Y - .05, PIVOT_Z + .1), (xe, PIVOT_Y - .05, arm_z - .3), .4, gold)          # the elbow
    _tube(bm, (xe, PIVOT_Y - .05, arm_z), (xe, root_y, arm_z), arm_r, gold)
    for y in (PIVOT_Y + .9, PIVOT_Y + 2.1, root_y - .45):
        _tube(bm, (xe, y, arm_z), (xe, y + .16, arm_z), arm_r + .06, gold)


def make_hammers(robots, mats, collection):
    """robots: [(name suffix, section x)]. Makes 'Robot pillar N' (still) and 'Robot arm N' (to hang on the mover of
    that name) for each, in `collection`; mats: key -> material (stone, stone2, stone3, moss, gold, wood, blue)."""
    src = _load()
    me = src.data
    ball = Ball([v.co for v in me.vertices
                 if v.co.x > -.12 and (Vector((v.co.x, v.co.z)) - PIVOT).dot(U) > HANDLE_BANDS[3]])
    print("HAMMER ball centre", tuple(round(x, 4) for x in ball.centre), "radius %.4f" % ball.radius,
          "grooves", ball.grooves)
    global L, KB, KX
    L = (Vector((ball.centre.x, ball.centre.z)) - PIVOT).dot(U)   # the fitted ball's middle, along the handle
    KB = BALL_RADIUS / ball.radius
    KX = PILLAR_HALF_X / max(abs(v.co.y) for v in me.vertices if v.co.x <= PILLAR_X1)
    _cut_bands(src, _swinging, ball)
    cen = [p.center.copy() for p in me.polygons]
    swing = [_swinging(c) for c in cen]
    keys = [_paint(c, sw, ball) for c, sw in zip(cen, swing)]
    order = ["stone", "stone2", "stone3", "moss", "gold", "wood", "blue"]
    made = []
    for suffix, x0 in robots:
        for part, want, place in (("Robot pillar " + suffix, False, _static_pos), ("Robot arm " + suffix, True, _swing_pos)):
            bm = bmesh.new()
            uv = bm.loops.layers.uv.new("UVMap")
            vmap = {}
            for poly, sw, key, c in zip(me.polygons, swing, keys, cen):
                if sw != want or (not sw and not _in_pillar(c)):   # R435: the asset's bracket and hub are redrawn
                    continue
                vs = []
                for vi in poly.vertices:
                    if vi not in vmap:
                        vmap[vi] = bm.verts.new(place(me.vertices[vi].co, x0))
                    vs.append(vmap[vi])
                try:
                    f = bm.faces.new(vs)
                except ValueError:
                    continue
                f.material_index = order.index(key)
                f.smooth = True
            if not want:
                _drum(bm, x0, order.index("gold"), order.index("blue"))
                _bracket(bm, x0, order.index("gold"))
            bm.normal_update()
            for f in bm.faces:
                n = f.normal
                for loop in f.loops:
                    co = loop.vert.co
                    loop[uv].uv = (co.x, co.y) if abs(n.z) > .7 else ((co.y, co.z) if abs(n.x) > abs(n.y) else (co.x, co.z))
            out = bpy.data.meshes.new(part)
            bm.to_mesh(out)
            bm.free()
            for k in order:
                out.materials.append(mats[k])
            ob = bpy.data.objects.new(part, out)
            collection.objects.link(ob)
            made.append(ob)
    bpy.data.objects.remove(src)
    return made
