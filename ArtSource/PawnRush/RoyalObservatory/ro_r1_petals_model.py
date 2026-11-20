"""R1 part 3 (seoungtest R725, 2026-11-19): the variable stars - the Big Dipper's stars 2 and 3, a small hub on its pillar
and five petals round it that fold down one after another (RoyalObservatoryBuilder.VariableStar, StarPetal), after
RO_A2's "변광성 꽃잎" and RO_2's handle.

    blender -b --factory-startup --python ro_r1_petals_model.py -- [--stills] [--export]

Graybox (ro_blockout.json): per star "Dipper star N core" (the hub, 0.7 m across, 0.6 deep, its top the stars' walking
height), "Dipper star N mark" (five gold rays), "Dipper star N pillar" (0.5 m, from the hall floor), and five
"Device_StarPetal Dipper star N i": the device's frame is the petal's hinge (Unity: x along the hinge, +z out along the
petal, y up; StarPetal turns it about its x, the outer end going down 84 degrees), holding "Petal" (a wedge 0.3 deep
under its top: a fifth of the ring from the hub to the star's radius - 0.02) and the warning overlay "Petal warning"
(left as it is: the game shows it while the petal is about to fold).
Each petal is modelled in its device's frame on that wedge, so it folds with it:
  top (flush): its own square motif from the atlas (no two petals of a star alike, ro_kit.pick_cell), its motif's up
       pointing out along the petal; a gold rim round it, flush;
  edges: gold moulding, navy enamel band with gold four-point stars, gold base bead;
  underside (seen when it hangs folded): cream marble with a gold rib down its middle and a gold eight-point star;
  hinge: two gold knuckles and a pin under the hinge line.
The hub (section frame): a gold drum with a faceted five-point star let into its top (flush), bead and torus rings; under
it a slim gold rod down to a collar below the folded petals' tips and the fluted pillar to the hall floor
(ro_parts.star_platform(deck=False)).
-> ro_r1_petals.blend, stills/r1/, --export Models/Dipper/RoPetal<N>_<i>.fbx (each in its device's frame) and
RoVariableHub<N>.fbx; RoyalObservatoryDressing.Dipper.cs hangs them over "Petal", the core, the mark and the pillar.
"""
import math, os, sys
import bmesh, bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402
from ro_parts import GOLD, NAVY, VIOLET, MARBLE, MOTIF, _merge, _transform, star_stud, bead, torus, rivet  # noqa: E402

ZONE = "Dipper"
STARS = {2: ((-10, .8, 26), 3.2), 3: ((-19, 1.8, 33), 3.2)}   # RoyalObservatoryBuilder.Dipper[1], [2]
CORE_R = .35
HINGE = CORE_R * math.cos(math.radians(36))
HALF = 35.3
THICK = .3
FOLD = -84.0          # Blender: the outer end down (StarPetal: +84 about Unity's local x)


def outline(radius, n=18):
    """The builder's PetalOutline (Unity x, z) -> petal-local Blender (x, y): the hinge edge, then the arc."""
    inner = CORE_R * math.sin(math.radians(HALF))
    pts = [(inner, 0.0)]
    for i in range(n + 1):
        a = math.radians(HALF - 2 * HALF * i / n)
        pts.append((radius * math.sin(a), radius * math.cos(a) - HINGE))
    pts.append((-inner, 0.0))
    return pts


def petal(col, name, radius):
    """One petal in its device's frame (Blender local: x along the hinge, +y out, z up; top z 0)."""
    inner = CORE_R * math.sin(math.radians(HALF))
    nu, nv = 18, 9
    # the top as a grid: u across (along the arc), v out from the hinge edge to the arc; boundary rows gold
    us = [0, .03] + [.03 + .94 * i / (nu - 2) for i in range(1, nu - 2)] + [.97, 1.0]
    vs = [0, .05] + [.05 + .9 * i / (nv - 2) for i in range(1, nv - 2)] + [.95, 1.0]

    def at(u, v):
        a = math.radians(HALF - 2 * HALF * u)
        arc = Vector((radius * math.sin(a), radius * math.cos(a) - HINGE, 0))
        hinge = Vector((inner - 2 * inner * u, 0, 0))
        return hinge.lerp(arc, v)
    bm = bmesh.new()
    grid = [[bm.verts.new(at(u, v)) for u in us] for v in vs]
    for j in range(len(vs) - 1):
        for i in range(len(us) - 1):
            f = bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
            edge = j == 0 or j == len(vs) - 2 or i == 0 or i == len(us) - 2
            f.material_index = GOLD if edge else MOTIF
    # the boundary loop (counter-clockwise from above) for the sides
    loop = [grid[0][i].co.copy() for i in range(len(us))]
    loop += [grid[j][-1].co.copy() for j in range(1, len(vs))]
    loop += [grid[-1][i].co.copy() for i in range(len(us) - 2, -1, -1)]
    loop += [grid[j][0].co.copy() for j in range(len(vs) - 2, 0, -1)]
    area = sum(loop[i].x * loop[(i + 1) % len(loop)].y - loop[(i + 1) % len(loop)].x * loop[i].y for i in range(len(loop)))
    if area < 0:
        loop.reverse()
    n = len(loop)

    def nrm(i):
        t = (loop[(i + 1) % n] - loop[i - 1]).normalized()
        return Vector((t.y, -t.x, 0))
    rings = []
    for (z, out, mat) in ((0.0, 0.0, None), (-.04, .025, GOLD), (-.08, .02, GOLD), (-.24, .01, NAVY), (-.27, .03, GOLD),
                          (-THICK, .0, GOLD)):
        rings.append(([bm.verts.new(loop[i] + nrm(i) * out + Vector((0, 0, z))) for i in range(n)], mat))
    top_ring = rings[0][0]
    for r in range(1, len(rings)):
        a, b = rings[r - 1][0], rings[r][0]
        for i in range(n):
            f = bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
            f.material_index = rings[r][1]
    under = bm.faces.new(list(reversed(rings[-1][0])))
    under.material_index = MARBLE
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)   # the top's boundary welds to the side's first ring
    ob = k.new_object(name + " petal", bm, col, P.SLOT_ORDER)
    return ob, loop


def petal_details(bm, loop, radius):
    """Studs on the navy band, the underside's rib and star, the hinge knuckles."""
    n = len(loop)
    # four-point stars along the arc's band (the outer edge) and the long sides
    for i in range(0, n, 3):
        p0, p1 = loop[i], loop[(i + 1) % n]
        if (p0.length < .5 and p1.length < .5):
            continue
        t = (p1 - p0).normalized()
        o = Vector((t.y, -t.x, 0))
        st = star_stud(.11, .02)
        _transform(st, Matrix.Rotation(math.atan2(o.y, o.x), 4, "Z"))
        _transform(st, Matrix.Translation((p0 + p1) * .5 + o * .012 + Vector((0, 0, -.16))))
        _merge(bm, st)
    # the underside: a gold rib along the middle and an eight-point star
    rib = k.prism([(-.05, .25), (.05, .25), (.05, radius - HINGE - .25), (-.05, radius - HINGE - .25)], -THICK - .03, -THICK, GOLD)
    _merge(bm, rib)
    st = star_stud(1.1, .08, points=8)
    _transform(st, Matrix.Rotation(math.pi / 2, 4, "Y"))         # facing -z (down)
    _transform(st, Matrix.Translation((0, (radius - HINGE) * .62, -THICK - .005)))
    _merge(bm, st)
    for xx in (-.75, .75):
        for yy in ((radius - HINGE) * .35, (radius - HINGE) * .85):
            if abs(xx) < yy * .55:
                r = rivet(.025, .02)
                _transform(r, Matrix.Rotation(math.pi / 2, 4, "Y"))
                _transform(r, Matrix.Translation((xx * yy / (radius - HINGE), yy, -THICK)))
                _merge(bm, r)
    # the hinge: knuckles along x under the hinge line, a pin through them
    for x0, x1 in ((-.19, -.06), (.06, .19)):
        kn = k.lathe([(0, x0), (.075, x0), (.085, x0 + .015), (.085, x1 - .015), (.075, x1), (0, x1)], 12, mat=GOLD)
        _transform(kn, Matrix.Rotation(math.pi / 2, 4, "Y"))
        _transform(kn, Matrix.Translation((0, .05, -.12)))
        _merge(bm, kn)


def hub(col, n, centre, floor_z):
    top = k.at(centre)
    bm = bmesh.new()
    prof = [(0, 0), (CORE_R - .04, 0), (CORE_R - .01, -.01), (CORE_R, -.04), (CORE_R, -.45), (CORE_R + .03, -.48),
            (CORE_R + .03, -.54), (CORE_R - .02, -.6), (.2, -.66), (.16, -.7)]
    d = k.lathe(prof, 32, mat=GOLD)
    _merge(bm, d)
    _merge(bm, P.ring_of(20, CORE_R + .005, -.26, lambda: bead(.025, u=6, v=4)))
    rod = k.lathe([(.16, -.7), (.16, -3.3), (0, -3.3)], 16, mat=GOLD)
    _merge(bm, rod)
    for z in (-1.4, -2.4):
        t = torus(.18, .03, 20, 6)
        _transform(t, Matrix.Translation((0, 0, z)))
        _merge(bm, t)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = k.new_object("Dipper star %d hub" % n, bm, col, P.SLOT_ORDER)
    ob.matrix_world = Matrix.Translation(top)
    # the star let into its top: the hub's top is the motif slot mapped to a round cell (a gold star in every one)
    k.surface_uvs(ob)
    k.motif_disc(ob, "Dipper star %d core" % n, top, CORE_R - .04, faces=lambda o, p: p.normal.z > .9 and p.center.z > -.005)
    objs, _ = P.star_platform(col, "Dipper star %d" % n, top, CORE_R, floor_z, deck=False, pillar_radius=.22, under_depth=3.3)
    return [ob] + objs


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    k.motif_reset()
    col = bpy.data.collections.new("RO R1 petals")
    bpy.context.scene.collection.children.link(col)
    petals = {}
    hubs = {}
    for n, (centre, radius) in STARS.items():
        hubs[n] = hub(col, n, centre, -18.0)
        chosen = []
        for i in range(1, 6):
            dev = "Device_StarPetal Dipper star %d %d" % (n, i)
            fr = k.frame(dev)
            ob, loop = petal(col, dev, radius - .02)
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            petal_details(bm, loop, radius - .02)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            bm.to_mesh(ob.data)
            bm.free()
            ob.matrix_world = fr
            k.surface_uvs(ob)
            # its motif: a square cell, none of the star's other petals', its up pointing out along the petal
            mid = fr @ Vector((0, (radius - HINGE) * .55, 0))
            cell = k.pick_cell(dev, "square", mid, reach=.1, avoid=chosen)
            chosen.append(cell)
            x_ax = fr.to_3x3() @ Vector((1, 0, 0))
            y_ax = fr.to_3x3() @ Vector((0, 1, 0))
            span = radius - HINGE
            k.motif_planar(ob, lambda o, p: p.material_index == 6, cell, fr @ Vector((0, span * .5, 0)), x_ax, y_ax,
                           2 * radius * math.sin(math.radians(HALF)) + .1, span + .1, k.pick_turn(dev, cell, "square") if cell in k.TURN_ANY else 0.0)
            petals[dev] = ob
        print("[r1 petals] star", n, "hub", k.tris(hubs[n]), "tris, petal", k.tris([petals[dev]]), "tris each")
    return col, petals, hubs


def stills(out, petals):
    import ro_r0_star_model as r0
    os.makedirs(out, exist_ok=True)
    c2 = k.at(STARS[2][0])
    shots = {
        "r1_petals_open": (Vector((5.5, -6.0, 4.0)), Vector((0, 0, -.6)), 32),
        "r1_petals_top": (Vector((0, 0, 9.0)), Vector((0, .01, 0)), 40),
    }
    r0.stills([], out, shots=shots, centre=c2, floor_at=(-10, 26, -18), fill_offset=(5, -6, 7))
    # fold star 2's petals 1, 2 and 3 (part way, all the way) and look again
    for i, ang in ((1, FOLD), (2, FOLD), (3, FOLD * .5)):
        ob = petals["Device_StarPetal Dipper star 2 %d" % i]
        ob.matrix_world = ob.matrix_world @ Matrix.Rotation(math.radians(ang), 4, "X")
    sc = bpy.context.scene
    cam = sc.camera
    for name, (eye, look, lens) in {"r1_petals_folding": (Vector((5.5, -6.0, 3.0)), Vector((0, 0, -1.2)), 30),
                                    "r1_petals_folded_under": (Vector((4.0, -4.5, -2.5)), Vector((0, 0, -1.3)), 30)}.items():
        cam.location = c2 + eye
        cam.rotation_euler = ((c2 + look) - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        sc.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("[r1 petals] still", name)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    col, petals, hubs = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r1_petals.blend"))
    if "--export" in argv:
        for dev, ob in petals.items():
            c = bpy.data.collections.new("export " + dev)
            bpy.context.scene.collection.children.link(c)
            c.objects.link(ob)
            n, i = dev.split()[-2:]
            k.export(c.name, "RoPetal%s_%s" % (n, i), ZONE, frame_name=dev)
        for n, objs in hubs.items():
            c = bpy.data.collections.new("export hub %d" % n)
            bpy.context.scene.collection.children.link(c)
            for o in objs:
                c.objects.link(o)
            k.export(c.name, "RoVariableHub%d" % n, ZONE)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r1"), petals)
