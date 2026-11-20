"""R2 part 1 (seoungtest R725, 2026-11-19): the bowl's long way - three crossing orbits, each a turning ring of three
planet platforms round a small sun (RoyalObservatoryBuilder.OrbitCrossing; RO_A2's "궤도" piece, RO_2's three orbits).

    blender -b --factory-startup --python ro_r2_orbits_model.py -- [--stills] [--export]

Graybox (ro_blockout.json): "Device_OrbitRing i" (i 1..3, turning on its tilted axis) holding, per platform n 1..3 at
Toward(n * 120) * 3.6: "Planet deck n" (a disc 2.9 m across, its top the device's y 0, 0.4 deep), "Planet brass rim n",
"Planet body n" (a 2.2 m ball under it), "Planet mark n", "Arm n" (a rod to the middle) and "Orbit sun" (1.5 m at y -0.7);
outside the device "Orbit track i" (the ring at y -0.5, radius 3.6, tilted with the device) and "Orbit pillar i" (0.5 m,
from the hall floor up to 1.3 m under the device's middle). Modelled:
  RoOrbitRing<i>.fbx (the device's frame - it turns with it): three small star platforms - a round motif top (a gold
     five-point star in each, the atlas's round cells), gold lip, a gold band with stars and rivets, a beaded torus and a
     soffit - each with its planet hung under it (the course's planet surfaces, no two neighbours alike) in a gold
     collar on a short stem; three gold arms from the sun, a ring and a knob on each; the sun - a gold ball in a crown of
     twelve long and twelve short rays;
  RoOrbitTrack<i>.fbx (the track's frame): the gold orbit ring - a rounded band with a bead string and a tick every
     ten degrees;
  RoOrbitPillars.fbx (the section's frame): the three pillars (ro_parts.star_platform(deck=False)'s pillar and base).
Walking tops and colliders stay the graybox's: each platform's top is its deck's top, nothing rises above it.
-> ro_r2_orbits.blend, stills/r2/, --export Models/Bowl/...; RoyalObservatoryDressing.Bowl.cs.
"""
import math, os, sys
import bmesh, bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402
from ro_parts import GOLD, NAVY, MARBLE, MOTIF, _merge, _transform, star_stud, bead, torus, rivet  # noqa: E402

ZONE = "Bowl"
ORBIT_R, DECK_R = 3.6, 1.45
PLANET_R = .92
# which planet hangs under which platform (ring, platform): the four the planning session picked for the orbits, so
# that no two neighbours on a ring and no two that meet at a crossing are alike
PLANETS = {1: ["neptune", "mars", "venus"], 2: ["mercury", "neptune", "mars"], 3: ["venus", "mercury", "neptune"]}


def toward(deg, r):
    """Builder's Toward(yaw) * r in a device's Blender local frame (x = sin, y = cos)."""
    t = math.radians(deg)
    return Vector((math.sin(t) * r, math.cos(t) * r, 0))


def small_deck(bm, centre, R):
    """A small star platform's deck round `centre` (its top at z 0): motif top, gold lip, a gold band with stars and
    rivets, a beaded torus, a navy soffit narrowing to the planet's stem."""
    prof = [(0.0, 0.0), (R * .5, 0.0), (R - .04, 0.0),
            (R + .04, 0.0), (R + .08, -.012), (R + .1, -.04),
            (R + .1, -.08), (R + .1, -.3),
            (R + .12, -.32), (R + .14, -.35), (R + .14, -.38), (R + .12, -.41), (R + .08, -.43),
            (R * .7, -.5), (.3, -.62), (.12, -.66)]
    mats = [MOTIF, MOTIF, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, GOLD, NAVY, NAVY, GOLD]
    d = k.lathe(prof, 64, mat=mats)
    circ = 2 * math.pi * (R + .1)
    _merge(d, P.ring_of(max(8, int(circ / .55)), R + .1, -.19, lambda: star_stud(.15, .025)))
    _merge(d, P.ring_of(max(8, int(circ / .55)), R + .1, -.19, lambda: rivet(.015, .01), phase=.5))
    _merge(d, P.ring_of(max(16, int(2 * math.pi * (R + .14) / .08)), R + .145, -.365, lambda: bead(.022, u=6, v=4)))
    _transform(d, Matrix.Translation(centre))
    _merge(bm, d)


def planet(name, centre, r):
    """A UV sphere with equirectangular UVs (u round, v up) in the planet's slot, its north up."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    nu, nv = 40, 20
    rows = []
    for j in range(nv + 1):
        lat = -math.pi / 2 + math.pi * j / nv
        rows.append([bm.verts.new((r * math.cos(lat) * math.cos(2 * math.pi * i / nu), r * math.cos(lat) * math.sin(2 * math.pi * i / nu),
                                   r * math.sin(lat))) for i in range(nu + 1)])
    for j in range(nv):
        for i in range(nu):
            vs = (rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i])
            try:
                f = bm.faces.new(vs)
            except ValueError:
                continue
            f.smooth = True
            for l, (ii, jj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                l[uv].uv = (ii / nu, jj / nv)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    _transform(bm, Matrix.Translation(centre))
    return bm


def sun(bm, centre):
    s = bmesh.new()
    bmesh.ops.create_uvsphere(s, u_segments=24, v_segments=14, radius=.4)
    for f in s.faces:
        f.material_index = GOLD
        f.smooth = True
    _merge(bm, wall(s, centre))
    # rays every way round it (a spiked sun, RO_A2), long and short by turns, on a Fibonacci sphere
    n = 30
    for i in range(n):
        zf = 1 - 2 * (i + .5) / n
        rad = math.sqrt(1 - zf * zf)
        a = math.pi * (3 - math.sqrt(5)) * i
        d = Vector((rad * math.cos(a), rad * math.sin(a), zf))
        long_ = i % 2 == 0
        ray = k.lathe([(0, 0), (.075 if long_ else .055, 0), (0, .42 if long_ else .26)], 8, mat=GOLD)
        _transform(ray, Matrix.Translation((0, 0, .36)))
        _transform(ray, d.to_track_quat("Z", "Y").to_matrix().to_4x4())
        _merge(bm, wall(ray, centre))
    r = torus(.5, .03, 32, 6)
    _merge(bm, wall(r, centre))


def wall(b, at):
    _transform(b, Matrix.Translation(at))
    return b


def ring_model(col, i):
    dev = "Device_OrbitRing %d" % i
    fr = k.frame(dev)
    bm = bmesh.new()
    planets = []
    for n in range(3):
        c = toward(n * 120, ORBIT_R)
        small_deck(bm, c, DECK_R)
        # the stem, collar and the planet under the deck
        stem = k.lathe([(0, 0), (.1, 0), (.1, -.3), (0, -.3)], 12, mat=GOLD)
        _merge(bm, wall(stem, c + Vector((0, 0, -.64))))
        collar = k.lathe([(0, 0), (.32, 0), (.36, -.05), (.3, -.1), (0, -.1)], 24, mat=GOLD)
        _merge(bm, wall(collar, c + Vector((0, 0, -.9))))
        planets.append((c + Vector((0, 0, -.94 - PLANET_R)), PLANETS[i][n]))
        # the arm from the sun to under the deck: a gold rod with a ring and a knob
        a = Vector((0, 0, -.7)) + c.normalized() * .18   # from inside the hub's ball (11-19: the arms started 0.33 m off it)
        b = c + Vector((0, 0, -.62)) - c.normalized() * .25
        _merge(bm, P._tube([a.lerp(b, t / 6) + Vector((0, 0, .12 * math.sin(math.pi * t / 6))) for t in range(7)], .06, 10))
        mid = a.lerp(b, .5) + Vector((0, 0, .12))
        rg = torus(.11, .025, 16, 6)
        _transform(rg, Matrix.Rotation(math.pi / 2, 4, "Y"))
        _transform(rg, Matrix.Rotation(math.atan2(c.y, c.x), 4, "Z"))
        _merge(bm, wall(rg, mid))
    # the hub the arms meet, and the sun raised over the ring on a stem (RO_A2 / RO_2: the sun stands above the
    # platforms; kept small - the next orbit's platforms pass 0.95 m from this middle)
    hub = bmesh.new()
    bmesh.ops.create_uvsphere(hub, u_segments=20, v_segments=12, radius=.22)
    for f in hub.faces:
        f.material_index = GOLD
        f.smooth = True
    _merge(bm, wall(hub, Vector((0, 0, -.7))))
    stem = k.lathe([(0, -.7), (.07, -.7), (.07, .55), (.12, .6), (.12, .66), (0, .66)], 14, mat=GOLD)
    _merge(bm, stem)
    for z in (-.2, .25):
        rg = torus(.1, .025, 16, 6)
        _merge(bm, wall(rg, Vector((0, 0, z))))
    sun(bm, Vector((0, 0, 1.05)))
    # the axle down from the hub to the still pillar's bearing (11-19, the user on Unity: "연결 축이 맞지 않음" - the
    # turning hub floated 0.4 m over an open pillar top): a gold rod into a bearing collar at the pillar's top (1.3 m
    # under the device's middle; the pillar model caps it)
    axle = k.lathe([(0, -.68), (.09, -.68), (.09, -1.22), (.16, -1.24), (.18, -1.3), (.14, -1.34), (0, -1.34)], 16, mat=GOLD)
    _merge(bm, axle)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = k.new_object("Orbit ring %d" % i, bm, col, P.SLOT_ORDER)
    ob.matrix_world = fr
    k.surface_uvs(ob)
    for n in range(3):
        top = fr @ toward(n * 120, ORBIT_R)
        k.motif_disc(ob, "%s deck %d" % (dev, n + 1), top, DECK_R - .04,
                     faces=lambda o, p, c=toward(n * 120, ORBIT_R): p.material_index == MOTIF and p.normal.z > .9
                     and (p.center - c).length < DECK_R)
    objs = [ob]
    for (c, name) in planets:
        pb = planet(name, c, PLANET_R)
        po = k.new_object("Orbit ring %d planet %s" % (i, name), pb, col, ("planet_" + name,))
        po.matrix_world = fr
        objs.append(po)
    return objs


def track_model(col, i):
    fr = k.frame("Orbit track %d" % i)
    bm = k.lathe([(ORBIT_R - .1, -.06), (ORBIT_R + .1, -.06), (ORBIT_R + .13, -.03), (ORBIT_R + .13, .03), (ORBIT_R + .1, .06),
                  (ORBIT_R - .1, .06), (ORBIT_R - .13, .03), (ORBIT_R - .13, -.03)], 128, mat=GOLD)
    # close the section ring (lathe leaves it open between the last and first profile points)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _merge(bm, P.ring_of(120, ORBIT_R + .135, 0, lambda: bead(.025, u=6, v=4)))
    # small ball posts standing on the ring (RO_A2's orbit ring), every 30 degrees between the platforms' paths
    for t in range(12):
        post = k.lathe([(0, 0), (.04, 0), (.03, .03), (.025, .16), (.05, .2), (.06, .26), (.045, .32), (0, .34)], 10, mat=GOLD)
        _transform(post, Matrix.Translation((ORBIT_R, 0, .06)))
        _transform(post, Matrix.Rotation(2 * math.pi * (t + .5) / 12, 4, "Z"))
        _merge(bm, post)
    for t in range(36):
        tick = k.prism([(ORBIT_R - .12, -.012), (ORBIT_R + .1, -.012), (ORBIT_R + .1, .012), (ORBIT_R - .12, .012)], .06, .075, GOLD)
        _transform(tick, Matrix.Rotation(2 * math.pi * t / 36, 4, "Z"))
        _merge(bm, tick)
    ob = k.new_object("Orbit track %d" % i, bm, col, P.SLOT_ORDER)
    ob.matrix_world = fr
    k.surface_uvs(ob)
    return [ob]


def pillars(col):
    objs = []
    for i in (1, 2, 3):
        n = k.node("Orbit pillar %d" % i)
        m = k.blender_matrix(n)
        top = m @ Vector((0, 0, 1))       # Unity cylinder's top (local y 1 -> Blender z 1)
        got, _ = P.star_platform(col, "Orbit %d" % i, top, .3, -18.0, deck=False, pillar_radius=.25, under_depth=.0)
        objs += got
    return objs


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    k.motif_reset()
    cols = {}
    for name in ("rings", "tracks", "pillars"):
        c = bpy.data.collections.new("RO R2 orbit " + name)
        bpy.context.scene.collection.children.link(c)
        cols[name] = c
    rings = {i: ring_model(bpy.data.collections.new("RO R2 orbit ring %d" % i), i) for i in (1, 2, 3)}
    for i, objs in rings.items():
        c = bpy.data.collections["RO R2 orbit ring %d" % i]
        bpy.context.scene.collection.children.link(c)
    tracks = {i: track_model(bpy.data.collections.new("RO R2 orbit track %d" % i), i) for i in (1, 2, 3)}
    for i in tracks:
        bpy.context.scene.collection.children.link(bpy.data.collections["RO R2 orbit track %d" % i])
    pl = pillars(cols["pillars"])
    print("[r2 orbits] ring", k.tris(rings[1]), "track", k.tris(tracks[1]), "pillars", k.tris(pl), "tris")
    return rings, tracks, pl


def stills(out):
    import ro_r0_star_model as r0
    c = k.frame("Device_OrbitRing 2").translation
    shots = {
        "r2_orbits_over": (Vector((12, -18, 14)), Vector((0, 0, -1)), 30),
        "r2_orbit_ring_close": (Vector((6, -7, 2.5)), Vector((0, 0, -.6)), 32),
        "r2_orbit_under": (Vector((5, -6, -4)), Vector((0, 0, -1.2)), 32),
    }
    r0.stills([], out, shots=shots, centre=c, floor_at=(c.x, c.y, -18), fill_offset=(6, -8, 8))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    rings, tracks, pl = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r2_orbits.blend"))
    if "--export" in argv:
        for i in (1, 2, 3):
            k.export("RO R2 orbit ring %d" % i, "RoOrbitRing%d" % i, ZONE, frame_name="Device_OrbitRing %d" % i)
            k.export("RO R2 orbit track %d" % i, "RoOrbitTrack%d" % i, ZONE, frame_name="Orbit track %d" % i)
        k.export("RO R2 orbit pillars", "RoOrbitPillars", ZONE)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r2"))
