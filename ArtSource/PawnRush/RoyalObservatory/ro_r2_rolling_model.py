"""R2 part 2 (seoungtest R725, 2026-11-19): the bowl's short way - one planet 4.4 m across rolling to and fro between stars
4 and 7 on twin rails (RoyalObservatoryBuilder.RollingPlanetWay, RollingPlanet; RO_A2's "구르는 행성과 레일", RO_2).

    blender -b --factory-startup --python ro_r2_rolling_model.py -- [--stills] [--export]

Graybox (ro_blockout.json): "Device_RollingPlanet" (moved and turned by RollingPlanet) holding "Planet" (a 4.4 m ball - the
walked surface, its collider), "Band a", "Band b" and "Spot" (what showed it rolling); outside it the two "Planet rail"
bars, "Planet rail tie 1..3" and "Planet rail post 1..3" (from the hall floor). Modelled:
  RoRollingPlanet.fbx (the device's frame - it rolls with it): the ball at the collider's radius exactly in the
     course's Jupiter surface (cream, amber and rust bands, its poles joining), with three wavy gold inlay bands laid on
     it 5 mm proud and two small gold pole studs - all of it turns as it rolls, so the rolling reads;
  RoPlanetRails.fbx (the section's frame): the two rails as round gold tubes with collars, the three ties as moulded
     gold bars with knobs, at each end a gold buffer housing (a rounded block with a navy roundel and a gold ring), and
     the posts down to the hall floor (ro_parts.star_platform(deck=False)'s fluted pillar).
-> ro_r2_rolling.blend, stills/r2/, --export Models/Bowl/...; RoyalObservatoryDressing.Bowl.cs.
"""
import math, os, sys
import bmesh, bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402
from ro_parts import GOLD, NAVY, _merge, _transform, bead, torus  # noqa: E402
from ro_r2_orbits_model import planet  # noqa: E402

ZONE = "Bowl"
R = 2.2


def wavy_band(bm, lat, amp, waves, width, r, segs=160):
    """A gold ribbon round the ball at latitude `lat` (degrees), wobbling `amp` degrees `waves` times, `width` wide."""
    rows = []
    for i in range(segs):
        lon = 2 * math.pi * i / segs
        la = math.radians(lat + amp * math.sin(waves * lon))
        rows.append([])
        for d in (-.5, .5):
            l2 = la + d * width / r
            rows[-1].append(bm.verts.new((r * math.cos(l2) * math.cos(lon), r * math.cos(l2) * math.sin(lon), r * math.sin(l2))))
    for i in range(segs):
        a, b = rows[i], rows[(i + 1) % segs]
        f = bm.faces.new((a[0], b[0], b[1], a[1]))
        f.material_index = GOLD
        f.smooth = True


def planet_model(col):
    fr = k.frame("Device_RollingPlanet")
    ball = planet("jupiter", Vector((0, 0, 0)), R)
    ob = k.new_object("Rolling planet", ball, col, ("planet_jupiter",))
    ob.matrix_world = fr
    bm = bmesh.new()
    for lat, amp, waves in ((-28, 5, 3), (6, 4, 4), (34, 5, 3)):
        wavy_band(bm, lat, amp, waves, .11, R + .005)
        # flat gold studs along it (RO_A2's beaded bands), barely proud: the ball's top is walked as it rolls
        for q in range(18):
            lon = 2 * math.pi * (q + .5) / 18
            la = math.radians(lat + amp * math.sin(waves * lon) + (5.5 if q % 2 else -5.5))
            n_ = Vector((math.cos(la) * math.cos(lon), math.cos(la) * math.sin(lon), math.sin(la)))
            st = k.lathe([(0, 0), (.06, 0), (.05, .006), (0, .008)], 10, mat=GOLD)
            _transform(st, n_.to_track_quat("Z", "Y").to_matrix().to_4x4())
            _transform(st, Matrix.Translation(n_ * (R - .001)))
            _merge(bm, st)
    for s in (-1, 1):
        stud = k.lathe([(0, 0), (.22, 0), (.2, .004), (.08, .006), (0, .006)], 24, mat=GOLD)   # flush: the top is walked on as it rolls
        if s < 0:
            _transform(stud, Matrix.Rotation(math.pi, 4, "X"))
        _transform(stud, Matrix.Translation((0, 0, s * (R - .002))))
        _merge(bm, stud)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    gold = k.new_object("Rolling planet gold", bm, col, P.SLOT_ORDER)
    gold.matrix_world = fr
    k.surface_uvs(gold)
    return [ob, gold]


def ends_of(node):
    """A graybox bar's two end centres (Blender): Unity's unit cube scaled, its length along local z."""
    m = k.blender_matrix(node)
    return m @ Vector((0, -.5, 0)), m @ Vector((0, .5, 0))


def rails_model(col):
    bm = bmesh.new()
    rails = [n for n in k.dump()["nodes"] if n["name"] == "Planet rail"]
    # The graybox's rails cut 0.27 m into the ball where it rests on them (their middles 0.8 m either side, 1.9 m under
    # the ball's middle, where its surface is 2.05 m under): drawn only, so the model's rails sit that much lower and
    # just touch it. The ties and the buffers go down with them.
    drop = Vector((0, 0, -.27))
    for n in rails:
        a, b = ends_of(n)
        a, b = a + drop, b + drop
        d = (b - a).normalized()
        side = d.cross(Vector((0, 0, 1))).normalized()
        L = (b - a).length
        # the running rail, a thick round gold tube with collars, and a slimmer guard rail 0.3 m under it on little
        # balusters (RO_A2's twin rails)
        _merge(bm, P._tube([a + (b - a) * t / 40 for t in range(41)], .12, 14))
        lo_a, lo_b = a + Vector((0, 0, -.3)), b + Vector((0, 0, -.3))
        _merge(bm, P._tube([lo_a + (lo_b - lo_a) * t / 40 for t in range(41)], .06, 10))
        for t in range(1, int(L / 1.0)):
            c = a + d * (t * 1.0)
            ring = torus(.13, .035, 16, 6)
            _transform(ring, d.to_track_quat("Z", "Y").to_matrix().to_4x4())
            _transform(ring, Matrix.Translation(c))
            _merge(bm, ring)
        nb = int(L / .5)
        for t in range(1, nb):
            c = a + d * (L * t / nb)
            bal = k.lathe([(0, 0), (.04, 0), (.03, .05), (.045, .14), (.03, .23), (.04, .27), (0, .3)], 8, mat=GOLD)
            _transform(bal, Matrix.Translation(c + Vector((0, 0, -.3))))
            _merge(bm, bal)
        for e, out in ((a, -d), (b, d)):
            # the buffer housing at the rail's end (RO_A2 / RO_2): an arched gold upright - a block with a round top -
            # a roundel with a bolt facing along the rail, a moulded foot
            yaw = math.atan2(out.y, out.x)
            arch = [(-.3, -.55), (.3, -.55), (.3, .25)] + [(.3 * math.cos(math.pi * q / 12), .25 + .3 * math.sin(math.pi * q / 12))
                                                            for q in range(1, 12)] + [(-.3, .25)]
            blk = k.prism(arch, -.22, .22, GOLD)       # outline (across, up) pushed along the rail
            _transform(blk, Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1))))
            _transform(blk, Matrix.Rotation(yaw, 4, "Z"))
            _transform(blk, Matrix.Translation(e + out * .3))
            _merge(bm, blk)
            foot = k.prism([(-.3, -.36), (.3, -.36), (.3, .36), (-.36, .36)], -.68, -.55, GOLD)
            _transform(foot, Matrix.Rotation(yaw + math.pi / 2, 4, "Z"))
            _transform(foot, Matrix.Translation(e + out * .3))
            _merge(bm, foot)
            q = out.to_track_quat("Z", "Y").to_matrix().to_4x4()
            for (rad, depth, mat_, at) in ((.2, .03, NAVY, .53), (.06, .1, GOLD, .56)):
                disc = k.lathe([(0, 0), (rad, 0), (rad, depth), (0, depth)], 24, mat=mat_)
                _transform(disc, q)
                _transform(disc, Matrix.Translation(e + out * at + Vector((0, 0, .1))))
                _merge(bm, disc)
            rg = torus(.21, .03, 24, 6)
            _transform(rg, q)
            _transform(rg, Matrix.Translation(e + out * .55 + Vector((0, 0, .1))))
            _merge(bm, rg)
            for sgn in (-1, 1):
                kb = bead(.05, u=10, v=6)
                _transform(kb, Matrix.Translation(e + out * .3 + side * sgn * .3 + Vector((0, 0, .25))))
                _merge(bm, kb)
    objs = []
    for i in (1, 2, 3):
        tie = k.node("Planet rail tie %d" % i)
        a, b = ends_of(tie)
        a, b = a + drop, b + drop
        d = (b - a).normalized()
        _merge(bm, P._tube([a + (b - a) * t / 8 for t in range(9)], .08, 10))
        for e in (a - d * .05, b + d * .05):
            kn = bead(.11, u=12, v=8)
            _transform(kn, Matrix.Translation(e))
            _merge(bm, kn)
        mid = (a + b) * .5
        med = torus(.14, .03, 20, 6)
        _transform(med, d.to_track_quat("Z", "Y").to_matrix().to_4x4())
        _transform(med, Matrix.Translation(mid))
        _merge(bm, med)
        post = k.node("Planet rail post %d" % i)
        top = k.blender_matrix(post) @ Vector((0, 0, 1)) + drop
        got, _ = P.star_platform(col, "Planet rail post %d" % i, top, .2, -18.0, deck=False, pillar_radius=.17, under_depth=.0)
        objs += got
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = k.new_object("Planet rails", bm, col, P.SLOT_ORDER)
    k.surface_uvs(ob)
    return [ob] + objs


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    cp = bpy.data.collections.new("RO R2 rolling planet")
    cr = bpy.data.collections.new("RO R2 planet rails")
    for c in (cp, cr):
        bpy.context.scene.collection.children.link(c)
    p = planet_model(cp)
    r = rails_model(cr)
    print("[r2 rolling] planet", k.tris(p), "rails", k.tris(r), "tris")
    return p, r


def stills(out):
    import ro_r0_star_model as r0
    c = k.frame("Device_RollingPlanet").translation
    shots = {
        "r2_rolling_side": (Vector((8.5, -6.0, 2.0)), Vector((0, 4.0, -.5)), 30),
        "r2_rolling_close": (Vector((3.6, -4.0, 2.6)), Vector((0, 0, 0)), 32),
        "r2_rolling_along": (Vector((-1.0, -6.0, 3.6)), Vector((0, 5.0, 0)), 28),
    }
    r0.stills([], out, shots=shots, centre=c, floor_at=(c.x, c.y, -18), fill_offset=(6, -6, 8))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p, r = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r2_rolling.blend"))
    if "--export" in argv:
        k.export("RO R2 rolling planet", "RoRollingPlanet", ZONE, frame_name="Device_RollingPlanet")
        k.export("RO R2 planet rails", "RoPlanetRails", ZONE)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r2"))
