"""R5 the observing stage, the great telescope and the balcony (ro-hall session, R725, 2026-11-19).

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python ro_r5_telescope_model.py -- --part <name>

One part at a time, each beside its sheet picture (RO_A2) and the concept (RO_3), for the user to pass (PLAN.md 2);
the parts are put together and exported once all have passed. Floors as the concept draws them (the user 11-19,
"컨셉처럼"): the stage a navy enamel disc with a gold sun, the tube's deck and the balcony cream marble.

Every number is RoyalObservatoryBuilder.cs's (ObservingStage, Telescope, Balcony); walking faces, colliders and the
moving pivots stay the builder's. Shared helpers come from ro_r3_globe_model.py (the mesh writer, the part pictures)
and ro_kit / ro_parts (R0, ro-path's: read only). Axes: Blender (x, y, z) = Unity (x, z, y).
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_r3_globe_model as g      # noqa: E402  (Mesh, ring, mat, the part pictures)
import ro_kit as k                 # noqa: E402
import ro_parts as parts           # noqa: E402

Mesh, ring, ring_section, empty, rng, U = g.Mesh, g.ring, g.ring_section, g.empty, g.rng, g.U

# ---------------------------------------------------------------- the builder's numbers (Unity space)
STAGE_TOP, EXIT_TOP, HALL_FLOOR = 12.0, 20.0, -18.0
STAGE = (9.0, STAGE_TOP, 90.0)
STAGE_R = 8.0
RAIL_ARCS = [(30, 60), (100, 330)]      # headings (0 north, 90 east) where the stage is railed (ro_blockout.json)
DECK_FOOT, DECK_HEAD = (9.0, STAGE_TOP, 93.0), (9.0, EXIT_TOP, 118.0)
DECK_HALF, TUBE_R = 2.5, 3.7
TUBE_RINGS = [.04, .2, .4, .5, .6, .8, .96]
IRISES = [.2, .4, .6, .8]
EXIT = (9.0, EXIT_TOP, 130.0)

STILLS = os.path.join(HERE, "stills", "r5")
BLEND = os.path.join(HERE, "ro_r5_telescope.blend")


def heading(h, r, c):
    """Blender point at heading h (degrees, 0 north) and radius r round centre c."""
    a = math.radians(h)
    return Vector((c.x + r * math.sin(a), c.y + r * math.cos(a), c.z))


# ---------------------------------------------------------------- 1 the observing stage


def star_fan(m, P, tips, rv, gold="gold", brass="brass"):
    """tips: [(angle, length)] in order round; P(x, y) -> the point. One flat star, no face over another."""
    n = len(tips)
    for j, (a, L) in enumerate(tips):
        a0, a1 = a - math.pi / n, a + math.pi / n          # the tips are evenly spaced round
        v0 = P(rv * math.sin(a0), rv * math.cos(a0))
        v1 = P(rv * math.sin(a1), rv * math.cos(a1))
        tip = P(L * math.sin(a), L * math.cos(a))
        c = P(0.0, 0.0)
        m.face([c, tip, v0], gold)
        m.face([c, v1, tip], brass)


def stage(coll):
    """The stage after RO_A2's observing stage and RO_3: a navy enamel disc 16 m across, its walking top the builder's
    (y 12): a gold twelve-ray sun in the middle (rays two-toned gold / brass along their ridges), a violet band and gold
    circles near the edge, small gold stars; a cream marble rim band where the balustrade stands, a rolled gold lip, a
    marble edge with a gold band, a navy soffit with gold brackets, and one fluted gold pillar to the hall floor. The
    balustrade (ro_parts.rail, as the way's) runs where the builder's rails do, a pearl lamp post every 30 degrees.
    Inlays lie 3 mm proud at most."""
    c = U(*STAGE)
    at = Matrix.Translation(c)
    m = Mesh()
    R = STAGE_R
    # the top: navy field, violet band, marble rim band (flat at z 0)
    # (RO_3: a violet field to a gold ring at 6.5 m, the marble rim band outside it)
    m.revolve([(.001, 0), (1.6, 0), (3.2, 0), (4.8, 0), (6.5, 0)], "violet", 128, at, smooth=False)
    m.revolve([(6.5, 0), (R - .06, 0)], "marble", 128, at, smooth=False)
    # gold circles (inlay strips, 2-3 mm)
    for r0, w in ((1.75, .12), (6.45, .14), (6.62, .05)):
        m.revolve([(r0 - w / 2, .01), (r0 + w / 2, .01)], "gold", 128, at, smooth=False)   # 1 cm: no flicker (11-20)
    # the sun: a gold disc, twelve long rays and twelve short, each two triangles (gold one side, brass the other)
    # (RO_3: a compass of sixteen points - eight long to the field's edge, eight short - round a gold disc)
    m.revolve([(.001, .03), (1.7, .03)], "gold", 64, at, smooth=False)
    m.star3d(at @ Matrix.Translation((0, 0, .035)), 1.2, .7, .25, 4, .004, "brass")
    # one fan, no ray over another (11-20: the separate rays crossed near the middle and flickered)
    star_fan(m, lambda x, y: at @ Vector((x, y, .02)),
             [(math.radians(j * 22.5), 6.1 if j % 2 == 0 else 4.0) for j in range(16)], 2.3)
    # small stars on the violet band and the field
    r_ = rng("RoStage stars")
    for j in range(28):
        t = 2 * math.pi * (j + .5) / 28
        rr = r_.uniform(2.4, 6.1)
        if j % 2 and any(abs(math.degrees(t) % 15 - 7.5) > 6 for _ in [0]):
            pass
        sm = at @ Matrix.Translation((rr * math.sin(t), rr * math.cos(t), .025))
        m.star3d(sm, .26 if j % 2 == 0 else .16, .18 if j % 2 == 0 else 0, .07, 4, .004, "gold")
    # the edge: rolled gold lip, marble face, gold band, gold bead, navy soffit, collar
    edge = [(R - .06, 0), (R, -.02), (R + .06, -.06), (R + .06, -.14), (R + .02, -.18)]
    m.revolve(edge, "gold", 160, at)
    m.revolve([(R + .02, -.18), (R + .02, -1.05)], "marble", 160, at)
    for j in range(40):                          # the blocks' joints, fine dark grooves
        t = 2 * math.pi * j / 40
        m.box((0, 0, -.6), (.03, .02, .86), "brass", at @ Matrix.Rotation(-t, 4, "Z") @ Matrix.Translation((0, R + .03, 0)))
    m.revolve([(R + .02, -1.05), (R + .14, -1.09), (R + .2, -1.19), (R + .22, -1.35), (R + .2, -1.51), (R + .14, -1.59),
               (R + .02, -1.63)], "gold", 160, at)
    m.revolve([(R + .02, -1.63), (R - .3, -1.67), (R * .7, -1.9), (R * .4, -2.05), (2.0, -2.1)], "navy", 128, at)
    m.revolve([(2.0, -2.1), (1.9, -2.3), (1.6, -2.4), (1.5, -2.7), (1.35, -2.8)], "gold", 64, at)
    # gold brackets under the soffit, one every 30 deg
    for j in range(12):
        t = math.radians(j * 30 + 15)
        d = Vector((math.sin(t), math.cos(t), 0))
        pts = [at @ (d * (1.6 + (R - 1.9) * u) + Vector((0, 0, -2.5 + 1.3 * u ** .6))) for u in (i / 10 for i in range(11))]
        m.tube(pts, .08, "gold", 6)
    # the pillar: base, fluted shaft (24 flutes), capital
    zt, zb = -2.8, HALL_FLOOR - STAGE_TOP
    shaft = []
    for i in range(48):
        a = 2 * math.pi * i / 48
        rr = 1.2 - (.06 if i % 2 else 0)
        shaft.append((rr * math.cos(a), rr * math.sin(a)))
    ring_lo = [at @ Vector((x, y, zb + 1.4)) for x, y in shaft]
    ring_hi = [at @ Vector((x, y, zt - 1.0)) for x, y in shaft]
    m.grid([ring_lo + ring_lo[:1], ring_hi + ring_hi[:1]], "gold", smooth=False)
    m.revolve([(1.2, zt - 1.0), (1.3, zt - .9), (1.3, zt - .8), (1.25, zt - .7), (1.55, zt - .35), (1.7, zt - .1), (1.35, zt)],
              "gold", 48, at)
    m.revolve([(2.0, zb), (2.0, zb + .4), (1.75, zb + .5), (1.75, zb + .8), (1.5, zb + .95), (1.35, zb + 1.1), (1.3, zb + 1.3),
               (1.2, zb + 1.4)], "gold", 48, at)
    m.revolve([(1.75, zb + .5), (1.75, zb + .8)], "navy", 48, at)
    ob = m.make("RoStage deck", coll, up=(0, 0, 1))

    # the balustrade on the marble rim (the builder's rail: 7.7 .. 8.0 m), a lamp post every 30 deg
    objs, lamps = [ob], []
    rr = R - .15
    for h0, h1 in RAIL_ARCS:
        n = int(round((h1 - h0) / 10))
        for j in range(n):
            a_ = heading(h0 + 10 * j, rr, c)
            b_ = heading(h0 + 10 * (j + 1), rr, c)
            lamp = (h0 + 10 * j) % 30 == 0
            made = parts.rail(coll, "RoStage rail %d" % (h0 + 10 * j), a_, b_, c.z, start_post=lamp or j == 0,
                              end_post=j == n - 1, lamp_posts=(0.0,) if lamp else ())
            objs += made
            if lamp:
                objs.append(lamp_pedestal(coll, "RoStage lamp %d" % (h0 + 10 * j), a_, lamps))
        if (h1 % 30) == 0:
            objs.append(lamp_pedestal(coll, "RoStage lamp %d" % h1, heading(h1, rr, c), lamps))
    return objs, lamps


def lamp_pedestal(coll, name, foot, lamps):
    """RO_3's stage lamp: a square gold pedestal over the rail post (0.5 m, 1.45 m high: base, panel, cornice), a gold
    cup and a big pearl globe (0.6 m). Its light is Unity's (the globe's centre goes into the lamp list)."""
    m = Mesh()
    at = Matrix.Translation(Vector((foot.x, foot.y, U(*STAGE).z)))
    m.box((0, 0, .08), (.62, .62, .16), "gold", at)
    m.box((0, 0, .7), (.48, .48, 1.08), "gold", at)
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        m.box((sx * .245, sy * .245, .7), (.02 if sx else .3, .02 if sy else .3, .8), "violet", at)
        sm = at @ Matrix.Translation((sx * .258, sy * .258, .7)) @ (Vector((sx, sy, 0)).to_track_quat("Z", "Y").to_matrix().to_4x4())
        m.star3d(sm, .11, .07, .03, 4, .02, "gold")
    m.box((0, 0, 1.3), (.6, .6, .1), "gold", at)
    m.box((0, 0, 1.38), (.52, .52, .06), "gold", at)
    m.revolve([(.001, 1.41), (.2, 1.41), (.24, 1.47), (.18, 1.53), (.12, 1.56)], "gold", 24, at)
    m.sphere((0, 0, 1.86), .32, "pearl", 24, 12, at, uv=False)
    lamps.append(at @ Vector((0, 0, 1.86)))
    return m.make(name, coll)


# ---------------------------------------------------------------- the tube's geometry (the builder's)

def deck_frame():
    """(along, normal, axis distance) of the tube's deck in Unity space."""
    f, h = Vector(DECK_FOOT), Vector(DECK_HEAD)
    along = (h - f).normalized()
    normal = along.cross(Vector((1, 0, 0)))
    axis = math.sqrt(TUBE_R ** 2 - DECK_HALF ** 2)
    return along, normal, axis


def deck_point(t):
    return Vector(DECK_FOOT).lerp(Vector(DECK_HEAD), t)


def tube_centre(t):
    """Unity point on the tube's axis at deck fraction t."""
    along, normal, axis = deck_frame()
    return deck_point(t) + normal * axis


# ---------------------------------------------------------------- 2 the fork mount

FORK_Z0, FORK_Z1, FORK_Y0, FORK_Y1 = 95.4, 97.4, STAGE_TOP - .8, STAGE_TOP + 9.5
FORK_X = [(STAGE[0] - 4.1, STAGE[0] - DECK_HALF - .1), (STAGE[0] + DECK_HALF + .1, STAGE[0] + 4.1)]


FORK_RI, FORK_RO = 4.0, 4.85      # the U's inner and outer radius round the tube's axis (the tube's rings reach 3.875)
FORK_D = .9                       # half the arms' depth along the tube (the builder's box is 2 m)


def fork_outline(sgn, pivot_y):
    """One arm's outline in (Unity x, y), seen along the tube (the user, 11-19: "U자로"): its lower part a quarter of
    the U round the tube's axis - from under the deck (buried in the stage, so it rises out of the floor) out to the
    axis's height - then a horn that narrows and curves back over the tube to a round head at y 21. Returns
    (inner edge, outer edge), both from the bottom up, and the head's centre."""
    cx = STAGE[0]
    inner, outer = [], []
    for j in range(19):                            # the quarter of the U: from straight down to level
        phi = math.radians(90 * j / 18)
        inner.append((cx + sgn * FORK_RI * math.sin(phi), pivot_y - FORK_RI * math.cos(phi)))
        outer.append((cx + sgn * FORK_RO * math.sin(phi), pivot_y - FORK_RO * math.cos(phi)))
    head = (cx + sgn * 3.35, FORK_Y1 - .62)
    H = head[1] - pivot_y
    for j in range(1, 13):                         # the horn: its middle from (4.5, axis) curving in to the head
        t = j / 12
        mx = 4.5 - 1.15 * t ** 2
        my = pivot_y + H * t
        w = (1.0 - .45 * t) / 2
        dx, dy = -2.3 * t, H                       # the middle line's direction (per unit t)
        L = math.hypot(dx, dy)
        nx, ny = dy / L, -dx / L                   # its outward normal (toward +x side)
        inner.append((cx + sgn * (mx - w * nx), my - w * ny))
        outer.append((cx + sgn * (mx + w * nx), my + w * ny))
    return inner, outer, head


def fork(coll):
    """The fork mount's two arms as RO_A2's fork draws them, U-shaped round the tube (the user, 11-19: "U자로 해서 ...
    모양을 정밀하게"): each arm a gold band 1 m wide and 1.8 m deep along the tube, a quarter of the U rising out of the
    stage round the tube's axis, a bearing at the axis (a gold drum, a violet medallion with a big gold eight-point
    star in a gold ring on the outer face, as RO_3), then a horn narrowing and curving in to a round head with an eye
    (a gold ring round a navy disc). Rolled gold edges; on its faces along the tube violet enamel panels following the
    band, gold stars on them; a marble and gold plinth with beads where it leaves the stage. The collision proxy
    ("RoFork collider") is the same outline in three convex pieces."""
    out = []
    zc = (FORK_Z0 + FORK_Z1) / 2
    pivot_y = tube_centre((zc - DECK_FOOT[2]) / (DECK_HEAD[2] - DECK_FOOT[2])).y
    for side, sgn in (("W", -1), ("E", 1)):
        inner, outer, head = fork_outline(sgn, pivot_y)
        m = Mesh()
        n = len(inner)

        def P(x, y, z):
            return U(x, y, zc + z)
        # the leg (the sheet's upright): from the plinth on the stage to the bearing, flaring at its foot
        leg_in, leg_out = [], []
        for j in range(9):
            t = j / 8
            y = STAGE_TOP + .4 + (pivot_y - STAGE_TOP - .4) * t
            mid = 4.15 + .35 * t ** 1.5
            w = .72 - .22 * t ** .7
            leg_in.append((STAGE[0] + sgn * (mid - w), y))
            leg_out.append((STAGE[0] + sgn * (mid + w), y))
        for e_in, e_out in ((inner, outer), (leg_in, leg_out)):
            for z_ in (FORK_D, -FORK_D):               # the two faces along the tube
                m.grid([[P(x, y, z_) for x, y in e_in], [P(x, y, z_) for x, y in e_out]], "gold", smooth=False)
            for edge, o in ((e_in, -1), (e_out, 1)):   # the edges, rolled: a half round 0.12 proud
                rows = []
                for k_ in range(7):
                    a_ = math.pi * k_ / 6
                    z_ = -FORK_D * math.cos(a_)
                    bulge = .12 * math.sin(a_)
                    row = []
                    for i, (x, y) in enumerate(edge):
                        ox, oy = e_out[i][0] - e_in[i][0], e_out[i][1] - e_in[i][1]
                        L = math.hypot(ox, oy) or 1
                        row.append(P(x + o * ox / L * bulge, y + o * oy / L * bulge, z_))
                    rows.append(row)
                m.grid(rows, "gold", smooth=True)
            m.face([P(*e_in[0], -FORK_D), P(*e_out[0], -FORK_D), P(*e_out[0], FORK_D), P(*e_in[0], FORK_D)], "gold")
        # violet panels along the band on both faces, in two runs: the U up to the bearing, the horn above it
        for z_, d in ((FORK_D + .006, 1), (-FORK_D - .006, -1)):
            for e_in, e_out, i0, i1 in ((inner, outer, 9, 16), (inner, outer, 21, n - 3), (leg_in, leg_out, 1, 6)):
                inner_, outer_ = inner, outer
                inner, outer = e_in, e_out
                rows = [[P(inner[i][0] + (outer[i][0] - inner[i][0]) * f, inner[i][1] + (outer[i][1] - inner[i][1]) * f, z_)
                         for i in range(i0, i1 + 1)] for f in (.2, .8)]
                m.grid(rows, "violet", smooth=False)
                for f in (.12, .88):
                    pts = [P(inner[i][0] + (outer[i][0] - inner[i][0]) * f, inner[i][1] + (outer[i][1] - inner[i][1]) * f,
                             z_ + d * .02) for i in range(i0, i1 + 1)]
                    m.tube(pts, .03, "gold", 6)
                for i in range(i0 + 1, i1, 3):
                    q = P((inner[i][0] + outer[i][0]) / 2, (inner[i][1] + outer[i][1]) / 2, z_ + d * .004)
                    sm = Matrix.Translation(q) @ Vector((0, d, 0)).to_track_quat("Z", "Y").to_matrix().to_4x4()
                    m.star3d(sm, .17, .1, .05, 4, .04, "gold")
                inner, outer = inner_, outer_
        # the bearing at the axis: a drum through the arm along x, the medallion on the outer face
        bx = STAGE[0] + sgn * 4.5
        axis_m = Matrix.Translation(U(bx, pivot_y, zc)) @ Matrix.Rotation(sgn * math.pi / 2, 4, "Y")
        m.revolve([(.001, -.7), (1.0, -.7), (1.05, -.6), (1.05, .62), (1.0, .72), (.001, .72)], "gold", 48, axis_m)
        fm = axis_m @ Matrix.Translation((0, 0, .72))
        m.revolve([(.001, .004), (.8, .004)], "violet", 40, fm, smooth=False)
        ring(m, .9, ring_section(.2, .16, .05), "gold", 48, fm @ Matrix.Translation((0, 0, .06)))
        for kk in range(16):
            t = 2 * math.pi * kk / 16
            m.sphere((.9 * math.cos(t), .9 * math.sin(t), .16), .05, "gold", 6, 3, fm, uv=False)
        m.star3d(fm @ Matrix.Translation((0, 0, .004)), .7, .42, .17, 4, .14, "gold")
        for d in (1, -1):
            jm = Matrix.Translation(P(bx, pivot_y, d * (FORK_D + .02))) @ Vector((0, d, 0)).to_track_quat("Z", "Y").to_matrix().to_4x4()
            jm = Matrix.Translation(U(bx, pivot_y, zc + d * (FORK_D + .02))) @ (
                Matrix.Rotation(-math.pi / 2, 4, "X") if d > 0 else Matrix.Rotation(math.pi / 2, 4, "X"))
            m.revolve([(.001, 0), (.85, 0), (.85, .1), (.001, .1)], "gold", 40, jm)
            m.revolve([(.001, .104), (.66, .104)], "violet", 40, jm, smooth=False)
            ring(m, .75, ring_section(.16, .1, .04), "gold", 48, jm @ Matrix.Translation((0, 0, .14)))
            m.star3d(jm @ Matrix.Translation((0, 0, .104)), .58, .34, .14, 4, .12, "gold")
        # the head: a round boss through the horn's end with an eye on both faces
        hm = Matrix.Translation(U(head[0], head[1], zc)) @ Matrix.Rotation(math.pi / 2, 4, "X")
        m.revolve([(.001, -FORK_D - .06), (.62, -FORK_D - .06), (.66, -FORK_D + .04), (.66, FORK_D - .04), (.62, FORK_D + .06),
                   (.001, FORK_D + .06)], "gold", 40, hm)
        for z_ in (FORK_D + .07, -FORK_D - .07):
            ring(m, .4, ring_section(.14, .1, .03), "gold", 32, hm @ Matrix.Translation((0, 0, z_)))
            m.revolve([(.001, z_), (.33, z_)], "navy", 24, hm, smooth=False)
        # the plinth where the arm leaves the stage (only its part outside the deck shows)
        pm = Matrix.Translation(U(STAGE[0] + sgn * 4.15, STAGE_TOP, zc))
        m.box((0, 0, .2), (1.6, 2.15, .4), "marble", pm)
        m.box((0, 0, .44), (1.5, 2.05, .08), "gold", pm)
        m.box((0, 0, .02), (1.68, 2.22, .04), "gold", pm)
        for i in range(9):
            for su in (-1, 1):
                m.sphere((-.7 + 1.4 * i / 8, su * 1.08, .2), .07, "gold", 8, 4, pm, uv=False)
        for i in range(5):
            for su in (-1, 1):
                m.sphere((su * .81, -.8 + 1.6 * i / 4, .2), .07, "gold", 8, 4, pm, uv=False)
        out.append(m.make("RoFork arm " + side, coll))

        # the collision proxy: the outline in three convex pieces (U quarter, lower horn, upper horn and head)
        cm = Mesh()
        for e_in, e_out, i0, i1 in ((inner, outer, 6, 9), (inner, outer, 9, 12), (inner, outer, 12, 15),
                                    (inner, outer, 15, 18), (inner, outer, 18, 24), (inner, outer, 24, n - 1),
                                    (leg_in, leg_out, 0, 8)):
            pts = [e_in[i] for i in range(i0, i1 + 1)] + [e_out[i] for i in range(i1, i0 - 1, -1)]
            vs = [cm.bm.verts.new(P(x, y, z_)) for z_ in (-FORK_D, FORK_D) for x, y in pts]
            bmesh.ops.convex_hull(cm.bm, input=vs)
        for f in cm.bm.faces:
            f.material_index = cm.si("gold")
        col = cm.make("RoFork collider " + side, coll)
        col.hide_render = True
        col.display_type = "WIRE"
    return out


# ---------------------------------------------------------------- 3 the tube: rings and truss

def tube_point(t, r, theta, a=0.0):
    """Unity point on the tube: deck fraction t, radius r from the axis, angle theta (degrees; 0 east, 90 over the
    axis, -90 under it, in the plane across the tube), a metres along the tube."""
    along, normal, axis = deck_frame()
    th = math.radians(theta)
    return tube_centre(t) + Vector((1, 0, 0)) * (r * math.cos(th)) + normal * (r * math.sin(th)) + along * a


def tube_ring(m, t, R, W, T, kind):
    """One of the tube's rings after RO_A2's lens ring: a gold band W along the tube and T deep (R its middle radius),
    rolled lips on both edges, a raised middle band on its outer face (violet enamel with gold stars on an iris ring,
    gold with star studs on the others), and four round lugs where the struts pass. Above the deck it is an arch -
    its feet stand outside the deck's edges (0.15 m clear) on posts down past the deck - and under the deck a cradle,
    so nothing of it stands over the walking face."""
    _, normal, axis = deck_frame()
    ri, ro = R - T / 2, R + T / 2
    clear = DECK_HALF + .16
    th_end = -math.degrees(math.acos(min(1.0, clear / ri)))        # where the inner edge is `clear` out from the middle
    sec = [(ri, -W / 2 + .06), (ri + .05, -W / 2), (ro - .08, -W / 2), (ro, -W / 2 + .1), (ro + .05, -W / 2 + .16),
           (ro, -W / 2 + .22), (ro, W / 2 - .22), (ro + .05, W / 2 - .16), (ro, W / 2 - .1), (ro - .08, W / 2),
           (ri + .05, W / 2), (ri, W / 2 - .06)]

    def arc(a0, a1, n):
        rows = []
        for q in sec + sec[:1]:
            rows.append([U(*tube_point(t, q[0], a0 + (a1 - a0) * j / n, q[1])) for j in range(n + 1)])
        m.grid(rows, "gold", smooth=True)
        for th, flip in ((a0, False), (a1, True)):                 # end caps
            cap = [U(*tube_point(t, q[0], th, q[1])) for q in sec]
            m.face(cap[::-1] if flip else cap, "gold")

    arc(th_end, 180 - th_end, 72)                                  # the arch over the deck
    arc(-180 + 55, -55, 24)                                        # the cradle under it
    # the feet: posts outside the deck's edges from the arch's ends down past the deck to the cradle's ends
    for sx in (1, -1):
        top = tube_point(t, ri + T / 2, 180 - th_end if sx < 0 else th_end)
        bot = tube_point(t, R, -125 if sx < 0 else -55)
        x = sx * (clear + T / 2)
        foot = [Vector((STAGE[0] + x, top.y, top.z)), Vector((STAGE[0] + x, bot.y - .2, bot.z))]
        for p_ in foot:
            pass
        lo = U(*foot[1])
        hi = U(*foot[0])
        m.box(tuple((lo + hi) / 2), (T, W * .8, (hi - lo).z + .1), "gold",
              Matrix.Identity(4))
        m.box(tuple((U(*bot) + lo) / 2), (abs((U(*bot) - lo).x) + T, W * .8, T), "gold")
    # the raised band on the outer face
    band = "violet" if kind == "iris" else "gold"
    rows = [[U(*tube_point(t, ro + .06, th_end + (180 - 2 * th_end) * j / 72, z)) for j in range(73)] for z in (-W / 2 + .2, W / 2 - .2)]
    m.grid(rows, band, smooth=True)
    nst = 14 if kind != "objective" else 18
    for j in range(nst):
        th = th_end + (180 - 2 * th_end) * (j + .5) / nst
        q = U(*tube_point(t, ro + .07, th))
        out = (U(*tube_point(t, ro + 1, th)) - U(*tube_point(t, ro, th))).normalized()
        sm = Matrix.Translation(q) @ out.to_track_quat("Z", "Y").to_matrix().to_4x4()
        m.star3d(sm, .17 if kind == "iris" else .13, .1 if kind == "iris" else 0, .05, 4, .05, "gold")
    along, _, _ = deck_frame()
    for zf in (-1, 1):
        for j in range(nst + 4):
            th = th_end + (180 - 2 * th_end) * (j + .5) / (nst + 4)
            q = U(*tube_point(t, R, th, zf * (W / 2 + .002)))
            d = Vector(U(*(along * zf)))
            sm = Matrix.Translation(q) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
            m.star3d(sm, .12, .07, .04, 4, .035, "gold")
    # the lugs where the struts pass: round bosses with a hole, as the sheet's (45, 135 over the heads; 0, 180 the sides)
    for th in (45, 135, 0, 180):
        c_ = U(*tube_point(t, ro + .3, th))
        d = Vector(U(*along))
        lm = Matrix.Translation(c_) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        h = W * .45
        m.revolve([(.2, -h), (.42, -h), (.48, -h + .06), (.48, h - .06), (.42, h), (.2, h), (.2, -h)], "gold", 24, lm, closed=True)
        m.revolve([(.001, -h + .05), (.2, -h + .05)], "navy", 16, lm, smooth=False)


STRUTS = (45, 135, 0, 180, 250, 290)


def tube(coll):
    """The truss tube (drawn only): the builder's seven rings (the objective, the last, bigger and deeper), the struts
    - thin gold tubes at the builder's four places (two over the heads, two under the deck) and two lighter rails along
    the arch's sides - with a collar at every ring. Kept open and thin so the following camera sees through it."""
    m = Mesh()
    for i, t in enumerate(TUBE_RINGS):
        top = i == len(TUBE_RINGS) - 1
        kind = "objective" if top else "iris" if t in IRISES else "plain"
        tube_ring(m, t, TUBE_R + (.6 if top else .1), 1.4 if top else .9, .85 if top else .6, kind)
    t0, t1 = TUBE_RINGS[0], TUBE_RINGS[-1]
    for th in STRUTS:
        r_ = .1 if th in (45, 135, 250, 290) else .06
        R = TUBE_R + .4
        pts = [U(*tube_point(t0 + (t1 - t0) * j / 24, R, th)) for j in range(25)]
        m.tube(pts, r_, "gold", 8)
        for t in TUBE_RINGS:
            c_ = U(*tube_point(t, R, th))
            d = (U(*tube_point(t + .01, R, th)) - c_).normalized()
            cm = Matrix.Translation(c_) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
            m.revolve([(r_ + .001, -.5), (r_ + .05, -.42), (r_ + .05, .42), (r_ + .001, .5)], "gold", 12, cm)
    rings = m.make("RoTube rings", coll)
    # The glass skin as RO_3's: clear glass round the arch between the rings, inside them, open under the deck (its
    # lower edges 0.16 m outside the deck's edges, as the rings' feet); two-sided, so it reads from inside the tube.
    gm_ = Mesh()
    rg = TUBE_R - .2
    th_end = -math.degrees(math.acos(min(1.0, (DECK_HALF + .16) / rg)))
    rows = []
    n = 48
    for j in range(n + 1):
        th = th_end + (180 - 2 * th_end) * j / n
        rows.append([U(*tube_point(TUBE_RINGS[0] + (TUBE_RINGS[-1] - TUBE_RINGS[0]) * i / 32, rg, th)) for i in range(33)])
    gm_.grid(rows, "glass", smooth=True)
    glass = gm_.make("RoTube glass", coll, two_sided=True)
    return [rings, glass]


# ---------------------------------------------------------------- 4 the tube's deck

def deck(coll):
    """The tube's deck as RO_3 draws it: cream marble paving on the builder's ramp (its top is the walking face, from
    y 12 at z 93 to y 20 at z 118, 5 m wide, 0.4 m deep): slabs 1.25 m across and 1.6 m along in a running bond, their
    joints fine brass lines, a gold border along both edges, a gold nosing where it leaves the stage and where it
    meets the balcony; its sides marble with a gold moulding, its underside navy enamel with gold ribs under every
    ring. Inlays lie 2 mm proud; nothing else above the walking face (the builder's deck has no rails)."""
    m = Mesh()
    x0, x1 = DECK_FOOT[0] - DECK_HALF, DECK_FOOT[0] + DECK_HALF
    z0, z1 = DECK_FOOT[2], DECK_HEAD[2]
    y0, y1 = DECK_FOOT[1], DECK_HEAD[1]

    def top_y(z):
        return y0 + (y1 - y0) * (z - z0) / (z1 - z0)

    def P(x, z, h=0.0, down=0.0):
        return U(x, top_y(z) + h - down, z)

    nz = 64
    zs = [z0 + (z1 - z0) * j / nz for j in range(nz + 1)]
    xs = [x0 + (x1 - x0) * i / 8 for i in range(9)]
    m.grid([[P(x, z) for x in xs] for z in zs], "marble", smooth=False)
    # the joints: lines along every 1.25 m, and across every 1.6 m staggered by half a slab in alternate columns
    for i in range(1, 4):
        x = x0 + .25 + 1.125 * i
        m.grid([[P(x - .02, z, .002), P(x + .02, z, .002)] for z in zs], "brass", smooth=False)
    cols = [(x0 + .25, x0 + 1.375), (x0 + 1.375, x0 + 2.5), (x0 + 2.5, x0 + 3.625), (x0 + 3.625, x1 - .25)]
    for c_, (a_, b_) in enumerate(cols):
        z = z0 + (.8 if c_ % 2 else 0) + 1.6
        while z < z1 - .2:
            m.grid([[P(a_, z - .02, .002), P(b_, z - .02, .002)], [P(a_, z + .02, .002), P(b_, z + .02, .002)]], "brass", smooth=False)
            z += 1.6
    # the gold borders along both edges (0.25 m, a fine line inside each)
    for a_, b_ in ((x0, x0 + .25), (x1 - .25, x1)):
        m.grid([[P(a_, z, .002), P(b_, z, .002)] for z in zs], "gold", smooth=False)
    for x in (x0 + .32, x1 - .32):
        m.grid([[P(x - .015, z, .002), P(x + .015, z, .002)] for z in zs], "gold", smooth=False)
    # small gold stars down the middle line, one every slab length
    z = z0 + 2.4
    while z < z1 - 1:
        q = P((x0 + x1) / 2, z, .002)
        m.star3d(Matrix.Translation(q), .18, .11, .05, 4, .0005, "gold")
        z += 3.2
    # nosings at the foot and the head (flush gold bands across)
    for zz in (z0 + .05, z1 - .2):
        m.grid([[P(x0, zz, .002), P(x1, zz, .002)], [P(x0, zz + .15, .002), P(x1, zz + .15, .002)]], "gold", smooth=False)
    # the sides: marble face, a gold moulding at the top edge and a gold bead along the bottom
    for x, o in ((x0, -1), (x1, 1)):
        m.grid([[P(x, z, 0, 0), P(x, z, 0, .4)] for z in zs], "marble", smooth=False)
        prof = [(0, -.02), (.05, -.04), (.07, -.09), (.05, -.13), (0, -.15)]
        m.grid([[P(x + o * dx, z, dy) for dx, dy in prof] for z in zs], "gold", smooth=True)
        m.tube([P(x + o * .03, z, -.4) for z in zs], .05, "gold", 6)
    # the underside: navy, and gold ribs across under every ring
    m.grid([[P(x, z, -.4) for x in xs] for z in zs], "navy", smooth=False)
    for t in TUBE_RINGS[:-1]:
        zz = z0 + (z1 - z0) * t
        for dz in (-.25, .25):
            pass
        m.box(tuple(P((x0 + x1) / 2, zz, -.55)), (x1 - x0 - .1, .3, .3), "gold")
    return [m.make("RoDeck", coll, up=(0, 0, 1))]


# ---------------------------------------------------------------- 5 the iris gates

LEAF_LEN, LEAF_H, LEAF_CLEAR = 2.7, 2.4, .1


def leaf_frame(n, side, closed=0.0):
    """Blender matrix of Device_IrisLeaf n W/E (side -1 W, +1 E): the builder's pose, slid `closed` (0..1) of its travel.
    Its local z goes across the deck toward the middle, y off the deck, x along the tube."""
    along, normal, _ = deck_frame()
    fwd = Vector((-side, 0, 0))
    x = normal.cross(fwd)                                     # Unity's right = up x forward
    at = deck_point(IRISES[n - 1]) + Vector((side * (DECK_HALF + LEAF_CLEAR), 0, 0)) + fwd * (closed * (DECK_HALF + LEAF_CLEAR - .02))
    M = Matrix((x, normal, fwd)).transposed().to_4x4()       # columns: local x, y, z in Unity
    M.translation = at
    return k.C @ M @ k.C


def leaf(m, F):
    """One leaf of an iris gate as half of RO_A2's iris (two gold half-doors meeting in the middle, a sun on each),
    inside the builder's leaf (0.5 thick along the tube, 2.7 long across the deck, 2.4 high, its leading edge a prow at
    z 0): a gold plate whose outer top corner is rounded so the two leaves shut make an arch over the deck; a rolled
    gold rim; a sun boss in a ring with rays engraved, small stars; a hinge knuckle on the leading edge. F: the leaf's
    frame (Blender), the model is written in it. Local here: x along the tube, y up off the deck, z toward the middle."""
    def L(x, y, z):
        q = F @ (k.C @ Vector((x, y, z)))                      # leaf-local (Unity axes) -> Blender
        return q
    # the outline in (z, y): from the outer foot up, round the top corner, down the leading edge
    ol = [(-LEAF_LEN, .05), (-LEAF_LEN, 1.0)]
    for j in range(1, 13):
        a = math.pi / 2 * j / 12
        ol.append((-LEAF_LEN + (LEAF_LEN - .05) * (1 - math.cos(a)), 1.0 + (LEAF_H - .05 - 1.0) * math.sin(a)))
    ol += [(-.05, LEAF_H - .05), (0, LEAF_H - .25), (0, .05)]
    # the plate (0.16 thick), its faces and edge
    T = .08
    for x_ in (T, -T):
        m.face([L(x_, y, z) for z, y in (ol if x_ > 0 else ol[::-1])], "gold")
    n = len(ol)
    for i in range(n):
        z0, y0 = ol[i]
        z1, y1 = ol[(i + 1) % n]
        m.face([L(T, y0, z0), L(-T, y0, z0), L(-T, y1, z1), L(T, y1, z1)], "gold")
    # the rim: a round bead round the edge on both faces
    inset = []
    cz, cy = -LEAF_LEN / 2, LEAF_H / 2
    for z, y in ol:
        dz, dy = cz - z, cy - y
        d = math.hypot(dz, dy) or 1
        inset.append((z + dz / d * .1, y + dy / d * .1))
    for x_ in (T + .04, -T - .04):
        m.tube([L(x_, y, z) for z, y in inset + inset[:1]], .06, "gold", 6)
    # the sun as RO_A2's: a raised sixteen-point star (eight long rays, eight short), a domed boss in a ring; small
    # eight-point stars in the corners; rivets round the rim
    sz, sy = -1.2, 1.2
    for sgn in (1, -1):
        out = (F.to_3x3() @ k.C.to_3x3() @ Vector((sgn, 0, 0))).normalized()

        def face_m(y, z, h=0.0):
            mm = Matrix.Translation(L(sgn * (T + h), y, z)) @ out.to_track_quat("Z", "Y").to_matrix().to_4x4()
            return mm
        m.star3d(face_m(sy, sz), 1.0, .66, .36, 8, .13, "gold")
        ring(m, .4, ring_section(.07, .06, .02), "gold", 32, face_m(sy, sz, .04))
        m.revolve([(.001, .16), (.15, .15), (.26, .1), (.33, .02), (.33, 0)], "gold", 24, face_m(sy, sz))
        for (z, y, r) in ((-2.35, .5, .17), (-.45, .5, .17), (-.5, 1.95, .17), (-1.85, 2.0, .14)):
            m.star3d(face_m(y, z), r, r * .62, r * .25, 4, .05, "gold")
        for j, (z, y) in enumerate(inset[::2]):
            m.sphere(tuple(L(sgn * (T + .07), y, z)), .045, "gold", 8, 4, uv=False)
    # the hinge knuckles on the leading edge (a gold rod with three collars)
    m.tube([L(0, .15, -.02), L(0, LEAF_H - .3, -.02)], .1, "gold", 8)
    for y in (.4, 1.2, 2.0):
        cm = Matrix.Translation(L(0, y, -.02)) @ (F.to_3x3() @ k.C.to_3x3() @ Vector((0, 1, 0))).to_track_quat("Z", "Y").to_matrix().to_4x4()
        m.revolve([(.1, -.12), (.15, -.1), (.15, .1), (.1, .12)], "gold", 12, cm)


def irises(coll, closed=1.0, only=None):
    out = []
    for n_ in range(1, 5):
        if only and n_ not in only:
            continue
        for side in (-1, 1):
            m = Mesh()
            leaf(m, leaf_frame(n_, side, closed))
            out.append(m.make("RoIris %d %s" % (n_, "W" if side < 0 else "E"), coll))
    return out


# ---------------------------------------------------------------- 6 the finder scope and the eyepiece

def scope_body(m, M, length, r, front_r, back_r, bands=3):
    """A small telescope after RO_A2's finder, along local +z from 0 (eyepiece) to `length` (objective), radius r:
    a navy enamel tube with gold bands and small stars, a flared gold objective cell with a blue-glass lens set back
    in it, a narrower gold eyepiece with a knurled ring and a dark glass at its end."""
    m.revolve([(r, .5), (r, length - .7)], "navy", 32, M)
    for j in range(bands):
        z = .5 + (length - 1.2) * (j + .5) / bands
        m.revolve([(r - .01, z - .3), (r + .05, z - .27), (r + .08, z - .2), (r + .08, z + .2), (r + .05, z + .27),
                   (r - .01, z + .3)], "gold", 32, M)
        m.revolve([(r + .081, z - .06), (r + .1, z), (r + .081, z + .06)], "gold", 32, M)
    for j in range(bands + 1):
        z = .5 + (length - 1.2) * j / bands
        for a in (0, 120, 240):
            q = M @ Vector((r * math.cos(math.radians(a + 40 * j)), r * math.sin(math.radians(a + 40 * j)), z + .02))
            d = (M.to_3x3() @ Vector((math.cos(math.radians(a + 40 * j)), math.sin(math.radians(a + 40 * j)), 0))).normalized()
            m.star3d(Matrix.Translation(q) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4(), .1, 0, .035, 4, .025, "gold")
    # objective cell
    m.revolve([(r - .02, length - .7), (r + .06, length - .66), (front_r, length - .4), (front_r + .04, length - .3),
               (front_r + .04, length - .05), (front_r - .03, length), (front_r - .12, length), (front_r - .12, length - .12)],
              "gold", 40, M)
    m.revolve([(.001, length - .14), (front_r - .12, length - .12)], "glass", 32, M, smooth=False)
    m.revolve([(.001, length - .18), (front_r - .12, length - .16)][::-1], "navy", 32, M, smooth=False)
    # eyepiece
    m.revolve([(.001, -.75), (back_r * .6, -.75), (back_r, -.65), (back_r, -.35), (back_r * .85, -.3), (back_r * .85, -.1),
               (r * .9, 0), (r + .04, .1), (r + .04, .4), (r - .01, .5)], "gold", 32, M)
    for j in range(16):
        a = 2 * math.pi * j / 16
        m.box((back_r * math.cos(a), back_r * math.sin(a), -.5), (.05, .05, .24), "gold", M @ Matrix.Rotation(a, 4, "Z")
              @ Matrix.Rotation(-a, 4, "Z"))
    m.revolve([(.001, -.752), (back_r * .45, -.752)], "glass", 24, M, smooth=False)


def finder(coll):
    """The finder scope on top of the tube (the builder's bar from 0.3 to 0.64 of the way, over the axis) after
    RO_A2's finder: 8.9 m long, 0.9 across, on two gold saddles standing on the tube's rings at 0.4 and 0.6; and the
    eyepiece at the tube's lower end: a short gold-and-navy draw tube on the bottom ring, pointing back down the tube
    over the stage, out of the way of the deck."""
    m = Mesh()
    along, normal, axis = deck_frame()
    R0 = TUBE_R + 1.15                                       # the finder's axis from the tube's (clear of the rings' tops)
    t0, t1 = .3, .64
    a = U(*tube_point(t0, R0, 90))
    b_ = U(*tube_point(t1, R0, 90))
    z = (b_ - a).normalized()
    M = Matrix.Translation(a) @ z.to_track_quat("Z", "Y").to_matrix().to_4x4()
    scope_body(m, M, (b_ - a).length, .45, .62, .3, bands=4)
    # saddles: a gold clamp round the finder, a waisted post down to the ring's top, a foot on it
    for t in (.4, .6):
        top = U(*tube_point(t, R0, 90))
        foot = U(*tube_point(t, TUBE_R + .1 + .6 / 2 + .05, 90))
        cm = Matrix.Translation(top) @ z.to_track_quat("Z", "Y").to_matrix().to_4x4()
        m.revolve([(.47, -.18), (.56, -.14), (.58, 0), (.56, .14), (.47, .18)], "gold", 32, cm, closed=True)
        up = (top - foot).normalized()
        pm = Matrix.Translation(foot) @ up.to_track_quat("Z", "Y").to_matrix().to_4x4()
        h = (top - foot).length - .45
        m.revolve([(.001, 0), (.42, 0), (.42, .08), (.3, .14), (.16, .22), (.12, h * .5), (.18, h - .12), (.3, h),
                   (.001, h)], "gold", 20, pm)
        m.box((0, 0, .04), (1.0, .7, .08), "gold", pm)
    # the eyepiece on the bottom ring, west of the top, pointing back down the tube
    base = U(*tube_point(TUBE_RINGS[0], TUBE_R + .95, 158))
    back = -Vector(U(*along)).normalized()
    e = Matrix.Translation(base) @ (-back).to_track_quat("Z", "Y").to_matrix().to_4x4()
    scope_body(m, e @ Matrix.Translation((0, 0, -2.0)), 2.8, .36, .44, .26, bands=2)
    m.revolve([(.001, -.3), (.5, -.3), (.55, -.2), (.55, .2), (.5, .3), (.001, .3)], "gold", 24,
              Matrix.Translation(base) @ (Vector(U(*tube_point(0, 1, 158))) - Vector(U(*tube_point(0, 0, 158)))).to_track_quat("Z", "Y").to_matrix().to_4x4())
    return [m.make("RoFinder", coll)]


# ---------------------------------------------------------------- 7 the objective and the focus beams' source

def objective(coll):
    """The objective at the tube's top ring (0.96 of the way): where the focus beams come from (FocusBeam draws its
    beam from the ring's middle to the spot on the deck; its light is the code's). The lens is glass only above the
    way (a cap of the ring from 3.1 m over the deck), so nothing stands in the walk or in the following camera's way
    lower down: a slightly domed glass cap in a gold bezel with stars, and from its lower edge three gold vanes to
    a gold hub at the ring's middle holding the focus lens (a small pearl-glass bead - it glows, it is the beams'
    source)."""
    m = Mesh()
    t = TUBE_RINGS[-1]
    R = TUBE_R + .6 - .85 / 2 - .02                          # the objective ring's inner radius
    along, normal, axis = deck_frame()
    # the cap: the circle above a chord 1.1 m over the axis (3.8 m over the deck, over the following camera)
    h = 1.1
    th0 = math.degrees(math.asin(h / R))
    rows = []
    for i in range(9):
        f = i / 8
        row = []
        for j in range(49):
            th = th0 + (180 - 2 * th0) * j / 48
            px = R * math.cos(math.radians(th))
            py_top = R * math.sin(math.radians(th))
            py = h + (py_top - h) * f
            r = math.hypot(px, py)
            dome = .25 * (1 - (r / R) ** 2)
            p = tube_centre(t) + Vector((1, 0, 0)) * px + normal * py + along * dome
            row.append(U(*p))
        rows.append(row)
    m.grid(rows, "glass", smooth=True)
    # its bezel: a gold rail along the chord, stars on it
    a = U(*(tube_centre(t) + Vector((1, 0, 0)) * (-R * math.cos(math.radians(th0))) + normal * h))
    b_ = U(*(tube_centre(t) + Vector((1, 0, 0)) * (R * math.cos(math.radians(th0))) + normal * h))
    m.tube([a, b_], .12, "gold", 10)
    for j in range(1, 8):
        q = a.lerp(b_, j / 8)
        d = Vector(U(*along))
        m.star3d(Matrix.Translation(q + d * .12) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4(), .14, .08, .04, 4, .04, "gold")
    # the vanes and the hub with the focus lens
    hub = U(*tube_centre(t))
    for x_ in (-1.8, 0.0, 1.8):
        rim = U(*(tube_centre(t) + Vector((1, 0, 0)) * x_ + normal * h))
        m.tube([hub, rim], .07, "gold", 8)
    d = Vector(U(*along))
    hm = Matrix.Translation(hub) @ (-d).to_track_quat("Z", "Y").to_matrix().to_4x4()
    m.revolve([(.001, -.35), (.32, -.35), (.42, -.2), (.42, .15), (.3, .25), (.2, .25), (.2, .3), (.001, .3)], "gold", 32, hm)
    ring(m, .44, ring_section(.08, .1, .02), "gold", 32, hm)
    m.sphere((0, 0, .38), .2, "pearl", 20, 10, hm, uv=False)
    return [m.make("RoObjective", coll)]


# ---------------------------------------------------------------- 8 the balcony

BAL_X0, BAL_X1, BAL_Z0, BAL_Z1 = EXIT[0] - 6, EXIT[0] + 6, DECK_HEAD[2], EXIT[2]


def balcony(coll):
    """The balcony outside the dome (the builder's floor 12 x 12 m at y 20, railed west, east and either side of the
    deck's head, open north to the exit) as RO_3 draws it: cream marble paving in a running bond with brass joints, a
    gold border and a gold eight-point star medallion in its middle; a thick marble edge with a gold moulding and a
    bead, a navy soffit with gold brackets; the common balustrade (ro_parts.rail) on the builder's rail lines with a
    lamp on a gold pedestal at each corner and midway along the sides. Inlays 2 mm proud."""
    m = Mesh()
    y = EXIT_TOP
    x0, x1, z0, z1 = BAL_X0, BAL_X1, BAL_Z0, BAL_Z1
    xs = [x0 + (x1 - x0) * i / 16 for i in range(17)]
    zs = [z0 + (z1 - z0) * j / 16 for j in range(17)]
    m.grid([[U(x, y, z) for x in xs] for z in zs], "marble", smooth=False)
    # joints: along every 1.5 m, across every 1.5 m staggered half a slab
    for i in range(1, 8):
        x = x0 + .3 + (x1 - x0 - .6) * i / 8
        m.grid([[U(x - .02, y + .002, z), U(x + .02, y + .002, z)] for z in zs], "brass", smooth=False)
    for c_ in range(8):
        a_ = x0 + .3 + (x1 - x0 - .6) * c_ / 8
        b_ = x0 + .3 + (x1 - x0 - .6) * (c_ + 1) / 8
        z = z0 + (.75 if c_ % 2 else 0) + 1.5
        while z < z1 - .3:
            m.grid([[U(a_, y + .002, z - .02), U(b_, y + .002, z - .02)], [U(a_, y + .002, z + .02), U(b_, y + .002, z + .02)]],
                   "brass", smooth=False)
            z += 1.5
    # the gold border round it
    for (a, b_, c, d) in ((x0, x0 + .3, z0, z1), (x1 - .3, x1, z0, z1), (x0, x1, z0, z0 + .3), (x0, x1, z1 - .3, z1)):
        m.grid([[U(a, y + .002, c), U(b_, y + .002, c)], [U(a, y + .002, d), U(b_, y + .002, d)]], "gold", smooth=False)
    # (no medallion: the user 11-19, "발코니는 문양 하지 말고")
    # the edge (marble with a gold moulding and bead) and the navy soffit, gold brackets under it
    edge = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
    for i in range(4):
        (ax, az), (bx, bz) = edge[i], edge[(i + 1) % 4]
        dx, dz = bx - ax, bz - az
        L = math.hypot(dx, dz)
        ox, oz = dz / L, -dx / L
        if i == 0:
            ox, oz = 0, -1
        m.grid([[U(ax, y, az), U(bx, y, bz)], [U(ax, y - .9, az), U(bx, y - .9, bz)]], "marble", smooth=False)
        prof = [(0, -.02), (.06, -.05), (.09, -.12), (.06, -.18), (0, -.2)]
        m.grid([[U(ax + ox * dd, y + dy, az + oz * dd), U(bx + ox * dd, y + dy, bz + oz * dd)] for dd, dy in prof], "gold", smooth=True)
        m.tube([U(ax + ox * .04, y - .9, az + oz * .04), U(bx + ox * .04, y - .9, bz + oz * .04)], .06, "gold", 6)
    m.grid([[U(x, y - .9, z) for x in xs] for z in zs[::-1]], "navy", smooth=False)
    for i in range(1, 6):
        x = x0 + (x1 - x0) * i / 6
        m.box(tuple(U(x, y - 1.1, (z0 + z1) / 2)), (.25, z1 - z0 - .4, .4), "gold")
    objs = [m.make("RoBalcony floor", coll, up=(0, 0, 1))]
    # the balustrade on the builder's rail lines (0.3 m outside the floor's edges), lamps at corners and midway
    c_ = U(0, y, 0).z
    T = .3
    lines = [((x0 - T / 2, z0), (x0 - T / 2, z1)), ((x1 + T / 2, z0), (x1 + T / 2, z1)),
             ((x0 - T, z0 - T / 2), (EXIT[0] - DECK_HALF, z0 - T / 2)), ((EXIT[0] + DECK_HALF, z0 - T / 2), (x1 + T, z0 - T / 2))]
    lamps = []
    for k_, (a, b_) in enumerate(lines):
        A, B = U(a[0], 0, a[1]), U(b_[0], 0, b_[1])
        objs += parts.rail(coll, "RoBalcony rail %d" % k_, A, B, c_)
    for (x, z) in ((x0 - T / 2, z0), (x1 + T / 2, z0), (x0 - T / 2, z1 - .3), (x1 + T / 2, z1 - .3),
                   (x0 - T / 2, (z0 + z1) / 2), (x1 + T / 2, (z0 + z1) / 2), (EXIT[0] - DECK_HALF - .2, z0 - T / 2),
                   (EXIT[0] + DECK_HALF + .2, z0 - T / 2)):
        objs.append(lamp_pedestal_at(coll, "RoBalcony lamp %.0f %.0f" % (x, z), U(x, y, z), lamps))
    return objs, lamps


def lamp_pedestal_at(coll, name, foot, lamps):
    """lamp_pedestal at any floor point (Blender) - the stage's lamp, on the balcony's floor."""
    m = Mesh()
    at = Matrix.Translation(foot)
    m.box((0, 0, .08), (.62, .62, .16), "gold", at)
    m.box((0, 0, .7), (.48, .48, 1.08), "gold", at)
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        m.box((sx * .245, sy * .245, .7), (.02 if sx else .3, .02 if sy else .3, .8), "violet", at)
        sm = at @ Matrix.Translation((sx * .258, sy * .258, .7)) @ (Vector((sx, sy, 0)).to_track_quat("Z", "Y").to_matrix().to_4x4())
        m.star3d(sm, .11, .07, .03, 4, .02, "gold")
    m.box((0, 0, 1.3), (.6, .6, .1), "gold", at)
    m.box((0, 0, 1.38), (.52, .52, .06), "gold", at)
    m.revolve([(.001, 1.41), (.2, 1.41), (.24, 1.47), (.18, 1.53), (.12, 1.56)], "gold", 24, at)
    m.sphere((0, 0, 1.86), .32, "pearl", 24, 12, at, uv=False)
    lamps.append(at @ Vector((0, 0, 1.86)))
    return m.make(name, coll)


# ---------------------------------------------------------------- 9 the exit arch

EXIT_SCALE = .85


def exit_gate(coll):
    """The exit arch at the balcony's north edge (the builder's posts and lintel there are drawn only), as the way's
    entry gate the user passed (ro-path's ro_r1_gate_model: RO_A1's 입구 문 - gold columns, the round arch with its
    navy starred band and keystone star, the violet field with the great gold crown), its columns and arch only (no
    wall, piers or door leaves here) at 0.85 of its size, so the columns stand on the balcony inside its rails and the
    opening is 10.7 m wide and 5.1 m to the springing. Read-only use of ro-path's file."""
    import importlib
    gm = importlib.import_module("ro_r1_gate_model")
    from ro_parts import SLOT_ORDER
    objs = []
    # turned to face south: its crown toward the pawns coming up the balcony
    place = (Matrix.Translation(U(EXIT[0], EXIT_TOP, EXIT[2] - .55)) @ Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Scale(EXIT_SCALE, 4)
             @ Matrix.Translation((0, -gm.FACE_Y, 0)))
    for name, fn, bev in (("RoExit columns", lambda b: (gm.column(b, -gm.COL_X), gm.column(b, gm.COL_X)), .008),
                          ("RoExit arch", gm.arch, .008)):
        bm = bmesh.new()
        fn(bm)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.transform(place)
        ob = k.new_object(name, bm, coll, SLOT_ORDER)
        k.bevel(ob, bev * EXIT_SCALE, 1, angle=40.0)
        k.apply_modifiers(ob)
        k.surface_uvs(ob)
        objs.append(ob)
    return objs


# ---------------------------------------------------------------- the scene, the part pictures

PARTS = {
    "exit": (["RoExit", "RoBalcony"], [("AssetRefs/RO_A1_architecture_modules.png", (0, 0, 1536, 1024)),
                                        ("RO_3_cassiopeia_telescope.png", (1240, 40, 1536, 230))]),
    "balcony": (["RoBalcony"], [("RO_3_cassiopeia_telescope.png", (1240, 40, 1536, 230))]),
    "objective": (["RoObjective", "RoTube", "RoDeck"], [("RO_3_cassiopeia_telescope.png", (1130, 60, 1400, 260))]),
    "finder": (["RoFinder", "RoTube"], [("AssetRefs/RO_A2_course_devices.png", (384, 640, 768, 1000)),
                                         ("RO_3_cassiopeia_telescope.png", (880, 90, 1240, 220))]),
    "iris": (["RoIris", "RoDeck"], [("AssetRefs/RO_A2_course_devices.png", (768, 640, 1150, 1000)),
                                     ("RO_3_cassiopeia_telescope.png", (700, 80, 1536, 480))]),
    "deck": (["RoDeck"], [("RO_3_cassiopeia_telescope.png", (700, 80, 1536, 480))]),
    "tube": (["RoTube"], [("AssetRefs/RO_A2_course_devices.png", (1150, 300, 1536, 640)),
                          ("RO_3_cassiopeia_telescope.png", (700, 80, 1536, 480))]),
    "fork": (["RoFork arm"], [("AssetRefs/RO_A2_course_devices.png", (0, 640, 384, 1000)),
                          ("RO_3_cassiopeia_telescope.png", (880, 280, 1030, 540))]),
    "stage": (["RoStage"], [("AssetRefs/RO_A2_course_devices.png", (768, 300, 1150, 640)),
                            ("RO_3_cassiopeia_telescope.png", (490, 360, 920, 580))]),
}


LAMPS_STAGE, LAMPS_BALCONY = [], []


def build(which):
    for c in list(bpy.data.collections):
        if c.name.startswith("RO R5"):
            for o in list(c.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(c)
    root = bpy.data.collections.new("RO R5 Stage and telescope")
    bpy.context.scene.collection.children.link(root)
    lamps = []
    if which in ("exit", "all"):
        exit_gate(root)
    if which in ("balcony", "all", "exit"):
        _, l_ = balcony(root)
        lamps += l_
        LAMPS_BALCONY[:] = l_
    if which == "all":
        irises(root, 0.0)
        objective(root)
        finder(root)
    if which == "objective":
        objective(root)
        tube(root)
        deck(root)
    if which == "finder":
        finder(root)
        tube(root)
    if which == "iris":
        irises(root, 1.0, only=(1,))
        deck(root)
    if which in ("deck", "all"):
        deck(root)
    if which in ("tube", "all"):
        tube(root)
    if which in ("fork", "all"):
        fork(root)
    if which in ("stage", "all"):
        _, l_ = stage(root)
        lamps += l_
        LAMPS_STAGE[:] = l_
    tris = 0
    for o in root.all_objects:
        if o.type == "MESH":
            tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
    print("TOTAL", tris, "lamps", len(lamps))
    return lamps


# ---------------------------------------------------------------- export

ZONE = "Telescope"


def triangulate(o):
    """Every face a triangle before the FBX (Unity dropped self-intersecting polygons from RoExit columns and the iris
    leaf, 11-19; ro-shell's fix for its bays)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()


# The light pass (planning session 11-19): the parts seen up close only at the course's end get LODs - LOD1 about half,
# LOD2 about a fifth, never culled (RoyalObservatoryDressing.Telescope.cs builds the LODGroup from <model>_LOD1/_LOD2).
LOD_MODELS = ("RoBalcony", "RoStage")


def export_lods(coll, model):
    for n, ratio in ((1, .5), (2, .2)):
        lc = bpy.data.collections.new("RO export %s_LOD%d" % (model, n))
        bpy.context.scene.collection.children.link(lc)
        for o in list(coll.objects):
            c = o.copy()
            c.data = o.data.copy()
            c.name = o.name + " LOD%d" % n
            lc.objects.link(c)
            d = c.modifiers.new("dec", "DECIMATE")
            d.decimate_type = "COLLAPSE"
            d.ratio = ratio
            d.use_collapse_triangulate = True
        k.export(lc.name, "%s_LOD%d" % (model, n), ZONE, None)


def export_all():
    """FBX to Assets/Resources/RoyalObservatory/Models/Telescope/ (ro_kit.export), the lamps beside them, and the fork's
    collision pieces as C# (stills/r5/fork_colliders.cs.txt, pasted into RoyalObservatoryDressing.Telescope.cs)."""
    coll_all = bpy.data.collections["RO R5 Stage and telescope"]
    groups = {
        "RoStage": ["RoStage"],
        "RoFork": ["RoFork arm"],
        "RoTube": ["RoTube", "RoFinder", "RoObjective"],
        "RoDeck": ["RoDeck"],
        "RoBalcony": ["RoBalcony", "RoExit"],
    }
    for model, starts in groups.items():
        coll = bpy.data.collections.new("RO export " + model)
        bpy.context.scene.collection.children.link(coll)
        obs = [o for o in coll_all.objects if o.type == "MESH" and any(o.name.startswith(s_) for s_ in starts)]
        for o in obs:
            coll_all.objects.unlink(o)
            coll.objects.link(o)
            g.metre_uvs(o)
            triangulate(o)
        print("EXPORT", model, len(obs))
        k.export(coll.name, model, ZONE, None)
        if model in LOD_MODELS:
            export_lods(coll, model)
    # the iris leaf, once, in its own device's frame (every leaf shares it: they are the same shape in their frames)
    coll = bpy.data.collections.new("RO export RoIrisLeaf")
    bpy.context.scene.collection.children.link(coll)
    m = Mesh()
    leaf(m, Matrix())
    ob = m.make("RoIris leaf", coll)
    g.metre_uvs(ob)
    triangulate(ob)
    k.export(coll.name, "RoIrisLeaf", ZONE, None)


def write_lamps(stage_lamps, balcony_lamps):
    k.write_lamps(ZONE, "RoStage", stage_lamps)
    k.write_lamps(ZONE, "RoBalcony", balcony_lamps)


def fork_colliders_cs():
    """The fork's convex collision pieces (Unity section coordinates) as C# arrays: per arm the blocker by the deck's
    edge (the builder's box's lower part, 0.1 m off the deck as it was), the U from 45 deg, the horn with its head,
    the leg. Each piece an outline (x, y) between z0 and z1."""
    zc = (FORK_Z0 + FORK_Z1) / 2
    pivot_y = tube_centre((zc - DECK_FOOT[2]) / (DECK_HEAD[2] - DECK_FOOT[2])).y
    lines = []
    report = []
    for side, sgn in (("W", -1), ("E", 1)):
        inner, outer, head = fork_outline(sgn, pivot_y)
        n = len(inner)
        leg_in, leg_out = [], []
        for j in range(9):
            t = j / 8
            y = STAGE_TOP + .4 + (pivot_y - STAGE_TOP - .4) * t
            mid = 4.15 + .35 * t ** 1.5
            w = .72 - .22 * t ** .7
            leg_in.append((STAGE[0] + sgn * (mid - w), y))
            leg_out.append((STAGE[0] + sgn * (mid + w), y))
        cx = STAGE[0]
        blocker = [(cx + sgn * 2.6, STAGE_TOP), (cx + sgn * 4.2, STAGE_TOP), (cx + sgn * 4.2, 15.6), (cx + sgn * 2.6, 15.6)]
        head_ring = [(head[0] + .66 * math.cos(a), head[1] + .66 * math.sin(a)) for a in (2 * math.pi * j / 12 for j in range(12))]
        pieces = {
            "blocker": blocker,
            "U": [inner[i] for i in range(9, 19)] + [outer[i] for i in range(18, 8, -1)],
            "horn": [inner[i] for i in range(18, n)] + [outer[i] for i in range(n - 1, 17, -1)] + head_ring,
            "leg": leg_in + leg_out[::-1],
        }
        for name, pts in pieces.items():
            xs = [x for x, _ in pts]
            ys = [y for _, y in pts]
            report.append("%s %-7s x %.2f .. %.2f  y %.2f .. %.2f  z %.1f .. %.1f" % (side, name, min(xs), max(xs), min(ys), max(ys),
                                                                                      FORK_Z0, FORK_Z1))
            lines.append("            new Piece(\"%s\", \"%s\", new[] { %s })," % (
                "Fork arm " + side, name, ", ".join("new Vector2(%.3ff, %.3ff)" % (x, y) for x, y in pts)))
    out = os.path.join(STILLS, "fork_colliders.cs.txt")
    os.makedirs(STILLS, exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(report))
    print("COLLIDERS", out)


def assembly_stills():
    """The parts put together, in ro-path's library materials, lit like the hall, beside the concept."""
    import numpy as np
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1500, 1000
    sc.view_settings.view_transform = "AgX"
    try:
        sc.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    g.library_materials()
    for m_ in bpy.data.materials:
        m_.use_backface_culling = True
    w = sc.world
    w.use_nodes = True
    nt = w.node_tree
    bg = next(n for n in nt.nodes if n.type == "BACKGROUND")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[1].position = .35, .62
    ramp.color_ramp.elements[0].color = (.55, .36, .16, 1)
    ramp.color_ramp.elements[1].color = (.04, .06, .16, 1)
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value, mr.inputs["From Max"].default_value = -1, 1
    nt.links.new(tc.outputs["Generated"], sep.inputs["Vector"])
    nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.6
    c = U(*STAGE)
    for name, kind, loc, energy, col, size in (("Moonlight", "AREA", c + Vector((-30, 40, 60)), 90000, (.7, .78, 1), 30),
                                              ("Warm key", "AREA", c + Vector((35, -40, 25)), 70000, (1, .8, .55), 25)):
        ld = bpy.data.lights.new(name, kind)
        ld.energy, ld.color, ld.size = energy, col, size
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = (c - lo.location).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)
    out = os.path.join(STILLS, "assembly")
    os.makedirs(out, exist_ok=True)
    nrm = deck_frame()[1]
    shots = {
        "r5_whole": (c + Vector((-26, -22, 16)), U(9, 16, 102), 28, ("RO_3_cassiopeia_telescope.png", (480, 60, 1536, 620))),
        "r5_up_the_tube": (U(*tube_point(-.12, 9, 160)), U(*tube_point(.55, 0, 0)), 30, ("RO_3_cassiopeia_telescope.png", (700, 80, 1536, 480))),
        "r5_on_the_deck": (U(*(deck_point(.1) + nrm * 2.4)), U(*(deck_point(.45) + nrm * 1.5)), 26, ("RO_3_cassiopeia_telescope.png", (880, 90, 1300, 330))),
        "r5_balcony": (U(9, 24, 112), U(9, 22, 128), 28, ("RO_3_cassiopeia_telescope.png", (1240, 40, 1536, 230))),
    }
    H = 640
    for name, (loc, look, lens, (src, (x0, y0, x1, y1))) in shots.items():
        cam = g.camera(name, loc, look, lens)
        sc.camera = cam
        f = os.path.join(out, name + ".png")
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        cpx, cw, ch = g._pixels(os.path.join(g.CONCEPT, src), H)
        img = np.concatenate([g._fit(cpx[ch - y1:ch - y0, x0:x1], H), np.ones((H, 10, 4), np.float32), g._fit(g._pixels(f, H)[0], H)], axis=1)
        img[..., 3] = 1
        o = bpy.data.images.new(name, img.shape[1], H, alpha=True)
        o.pixels[:] = img.ravel()
        o.filepath_raw = os.path.join(out, name + "_compare.png")
        o.file_format = "PNG"
        o.save()
        print("ASSEMBLY", o.filepath_raw)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    part = args[args.index("--part") + 1] if "--part" in args else "all"
    lamps = build(part)
    w = bpy.data.worlds.new("World")
    bpy.context.scene.world = w
    w.use_nodes = True
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "--export" in args:
        st = [o for o in bpy.data.objects if o.name.startswith("RoStage lamp")]
        fork_colliders_cs()
        stage_l = [o.matrix_world @ Vector((0, 0, 1.86)) for o in st]
        bal_l = [o.matrix_world @ Vector((0, 0, 1.86)) for o in bpy.data.objects if o.name.startswith("RoBalcony lamp")]
        export_all()
        write_lamps(LAMPS_STAGE, LAMPS_BALCONY)
    if "--assembly" in args:
        for o in [o for o in bpy.data.objects if o.name.startswith("RoFork collider")]:
            o.hide_render = True
        assembly_stills()
    if "--part" in args:
        g.PARTS = PARTS
        g.STILLS = STILLS
        g.DARK_PARTS = set()
        g.VIEWS = {"stage": (Vector((0, -1, .55)), Vector((.8, -.7, .7))), "fork": (Vector((-.5, -1, .3)), Vector((1, -.4, .25))),
                   "tube": (Vector((1, -.35, .45)), Vector((-.05, -1, .32))),
                   "deck": (Vector((.9, -.4, .6)), Vector((-.2, -1, .5))),
                   "iris": (Vector((.15, -1, .35)), Vector((.8, -.6, .5))),
                   "balcony": (Vector((-.6, -1, .7)), Vector((1, -.3, .5))),
                   "exit": (Vector((-.3, -1, .35)), Vector((.6, 1, .4)))}
        c = U(*STAGE)
        r3 = U(*tube_point(.2, TUBE_R, 90))
        nrm = deck_frame()[1]
        fc = U(*tube_point(.5, TUBE_R + 1.15, 90))
        ep = U(*tube_point(.04, TUBE_R + .95, 158))
        g.CLOSE = {"objective": {"objective_close": (U(*(deck_point(.68) + deck_frame()[1] * 3.0 + Vector((1.5, 0, 0)))), U(*tube_centre(.96)), 22,
                                                     (1130, 60, 1400, 260), "RO_3_cassiopeia_telescope.png")},
                   "finder": {"finder_close": (U(*tube_point(.75, TUBE_R + 6, 30)), fc, 40, (384, 640, 768, 1000),
                                               "AssetRefs/RO_A2_course_devices.png"),
                              "eyepiece_close": (ep + Vector((-5, -4, 1.5)), ep + Vector((0, -1, -.4)), 35,
                                                 (384, 640, 768, 1000), "AssetRefs/RO_A2_course_devices.png")},
                   "iris": {"iris_close": (U(*(deck_point(.07) + nrm * 2.6 + Vector((1.2, 0, 0)))), U(*(deck_point(.2) + nrm * 1.2)), 32,
                                        (768, 640, 1150, 1000), "AssetRefs/RO_A2_course_devices.png")},
                   "deck": {"deck_up": (U(*tube_point(-.12, 9, 160)), U(*tube_point(.55, 0, 0)), 30, (700, 80, 1536, 480),
                                        "RO_3_cassiopeia_telescope.png"),
                            "deck_close": (U(DECK_FOOT[0] + 4, DECK_FOOT[1] + 3.5, DECK_FOOT[2] + 2), U(DECK_FOOT[0], DECK_FOOT[1] + 2, DECK_FOOT[2] + 8),
                                           35, (700, 280, 1100, 480), "RO_3_cassiopeia_telescope.png")},
                   "tube": {"tube_up": (U(*tube_point(-.12, 9, 160)), U(*tube_point(.55, 0, 0)), 30, (700, 80, 1536, 480),
                                        "RO_3_cassiopeia_telescope.png"),
                            "tube_ring": (U(*tube_point(.2, 8, 115, -6)), U(*tube_point(.2, 2, 90)), 35, (1150, 300, 1536, 640),
                                          "AssetRefs/RO_A2_course_devices.png")},"stage": {"stage_close": (c + Vector((9, -13, 8)), c + Vector((0, 0, -1)), 30, (768, 300, 1150, 640),
                                             "AssetRefs/RO_A2_course_devices.png"),
                             "stage_concept": (c + Vector((-2, -17, 9)), c + Vector((0, 0, -.5)), 32, (490, 360, 920, 580),
                                               "RO_3_cassiopeia_telescope.png")}}
        g.part_stills(part)
