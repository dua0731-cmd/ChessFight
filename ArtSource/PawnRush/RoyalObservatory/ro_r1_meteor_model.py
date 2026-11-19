"""R1 part 4 (seoungtest R725, 2026-11-19): the meteors (MeteorShower) - the falling stone and its tail, and the ring
that warns where it lands; RO_A2's "유성" (a white marble stone with gold), RO_1 / RO_2's streaks. Shared with
Cassiopeia's shower (PLAN R4: "R1의 유성 모델을 같이 씀").

    blender -b --factory-startup --python ro_r1_meteor_model.py -- [--stills] [--export]

MeteorShower makes its views in Play (none is in the graybox dump): per meteor a "Meteor" root lying flat on the line
(Unity: y up off the line), under it "Warning ring (shove)" (a flat gold disc, 4.4 m), "Warning fill (knock-down)"
(grows to 2.8 m), "Streak" (a 0.35 m rod 6 m long coming down from 26 m in the last 0.4 s) and "Flash". Its timing and
hits stay as they are; RoyalObservatoryDressing's MeteorShowerDress stops drawing the rim and the rod and hangs these on
each view as it appears:
  RoMeteor.fbx      "Meteor stone": a faceted marble stone 0.9 m across with gold veins and gold-capped facets;
                    "Meteor tail": three crossed tapering ribbons and a scatter of sparks trailing 5.5 m up from it
                    (drawn glowing by the dressing, a warm white to gold), both round the origin = the stone's middle;
  RoMeteorRing.fbx  the warning ring at the shove's edge (4.4 m): a gold band with degree ticks, four eight-point stars
                    and an inner gold line at the knock-down's edge (2.8 m), lying on the origin (glowing gold in Unity).
-> ro_r1_meteor.blend, stills/r1/, --export Assets/Resources/RoyalObservatory/Models/Dipper/RoMeteor.fbx, RoMeteorRing.fbx.
"""
import math, os, random, sys
import bmesh, bpy
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402
from ro_parts import GOLD, MARBLE, _merge, _transform, star_stud  # noqa: E402

ZONE = "Dipper"
GLASS = 7                 # an extra slot index: the tail (drawn glowing by the dressing)
SLOTS = P.SLOT_ORDER + ("glass",)
SHOVE_R, KNOCK_R = 2.2, 1.4


def stone(bm):
    """A faceted marble chunk (an icosphere cut flat by a dozen planes) with gold-filled cracks: the faces within a few
    centimetres of three crack planes are gold."""
    g = bmesh.new()
    bmesh.ops.create_icosphere(g, subdivisions=3, radius=.5)
    rnd = random.Random(7)
    for v in g.verts:
        v.co *= 1.0 + .08 * noise.noise(v.co * 3.0 + Vector((3.1, 1.7, 5.3)))
    _transform(g, Matrix.Diagonal((1.0, .9, 1.15, 1.0)))
    # flat facets: pull every vertex beyond a cutting plane back onto it
    for _ in range(14):
        n = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))).normalized()
        d = rnd.uniform(.3, .42)
        for v in g.verts:
            h = v.co.dot(n)
            if h > d:
                v.co -= n * (h - d)
    bmesh.ops.remove_doubles(g, verts=g.verts, dist=.004)
    bmesh.ops.dissolve_degenerate(g, edges=g.edges, dist=.004)
    bmesh.ops.recalc_face_normals(g, faces=g.faces)
    cracks = [(Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-.4, .4))).normalized(), rnd.uniform(-.12, .12)) for _ in range(3)]
    for f in g.faces:
        c = f.calc_center_median()
        near = min(abs(c.dot(n) - d + .05 * noise.noise(c * 6.0)) for n, d in cracks)
        f.material_index = GOLD if near < .035 else MARBLE
        f.smooth = False
    _merge(bm, g)


def fade(z, length):
    return max(0.0, 1.0 - (z / length)) ** 1.3


def tail(bm, length=5.5):
    """A glowing cone trailing up from the stone, six thin strands twisting round it and a scatter of sparks; each
    vertex carries its glow in the colour attribute (alpha 1 at the stone, 0 at the end) - the dressing draws it with
    an additive particle material that uses it."""
    col = bm.loops.layers.float_color.get("Glow") or bm.loops.layers.float_color.new("Glow")
    made = []

    def face(vs):
        f = bm.faces.new(vs)
        f.material_index = GLASS
        made.append(f)
    segs, sides = 14, 12
    rings = []
    for i in range(segs + 1):
        t = i / segs
        z = .05 + length * t
        r = .44 * (1 - t) ** 1.2 + .015
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * j / sides), r * math.sin(2 * math.pi * j / sides), z)) for j in range(sides)])
    for i in range(segs):
        for j in range(sides):
            face((rings[i][j], rings[i][(j + 1) % sides], rings[i + 1][(j + 1) % sides], rings[i + 1][j]))
    for r_ in range(6):
        a0 = 2 * math.pi * r_ / 6
        rows = []
        for i in range(segs + 1):
            t = i / segs
            z = .2 + length * 1.05 * t
            a = a0 + t * 1.4
            rad = .5 * (1 - t) ** .9 + .03
            c = Vector((rad * math.cos(a), rad * math.sin(a), z))
            w = .05 * (1 - t) + .008
            sd = Vector((-math.sin(a), math.cos(a), 0)) * w
            rows.append((bm.verts.new(c - sd), bm.verts.new(c + sd)))
        for i in range(segs):
            face((rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0]))
    rnd = random.Random(11)
    for i in range(26):
        t = rnd.random() ** .8
        z = .4 + length * .9 * t
        rad = .7 * (1 - t) + .1
        a = rnd.uniform(0, 2 * math.pi)
        sp = bmesh.ops.create_icosphere(bm, subdivisions=0, radius=.045 * (1.3 - t),
                                        matrix=Matrix.Translation((rad * math.cos(a) * rnd.random(), rad * math.sin(a) * rnd.random(), z)))
        for f in {f for v in sp["verts"] for f in v.link_faces}:
            f.material_index = GLASS
            made.append(f)
    for f in made:
        for l in f.loops:
            a = fade(l.vert.co.z, length * 1.05)
            l[col] = (1.0, .78 + .22 * a, .45 + .55 * a, a)


def ring(bm):
    band = k.lathe([(SHOVE_R - .16, 0), (SHOVE_R, 0), (SHOVE_R, .025), (SHOVE_R - .16, .025)], 96, mat=GOLD)
    _merge(bm, band)
    inner = k.lathe([(KNOCK_R - .03, 0), (KNOCK_R + .03, 0), (KNOCK_R + .03, .02), (KNOCK_R - .03, .02)], 72, mat=GOLD)
    _merge(bm, inner)
    for i in range(48):
        t = 2 * math.pi * i / 48
        long_ = i % 4 == 0
        r0 = SHOVE_R - .16 - (.22 if long_ else .1)
        tick = k.prism([(r0, -.012), (SHOVE_R - .16, -.012), (SHOVE_R - .16, .012), (r0, .012)], 0, .02, GOLD)
        _transform(tick, Matrix.Rotation(t, 4, "Z"))
        _merge(bm, tick)
    for i in range(4):
        t = math.pi / 2 * i + math.pi / 4
        st = star_stud(.42, .04, points=8)
        _transform(st, Matrix.Rotation(-math.pi / 2, 4, "Y"))    # facing up
        _transform(st, Matrix.Translation((SHOVE_R * .5 + KNOCK_R * .5 + .2) * Vector((math.cos(t), math.sin(t), 0)) + Vector((0, 0, .005))))
        _merge(bm, st)


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    cm = bpy.data.collections.new("RO R1 meteor")
    cr = bpy.data.collections.new("RO R1 meteor ring")
    for c in (cm, cr):
        bpy.context.scene.collection.children.link(c)
    bm = bmesh.new(); stone(bm)
    st = k.new_object("Meteor stone", bm, cm, SLOTS)
    k.surface_uvs(st)
    bm = bmesh.new(); tail(bm)
    tl = k.new_object("Meteor tail", bm, cm, SLOTS)
    k.surface_uvs(tl)
    if "Color" in tl.data.color_attributes:
        tl.data.color_attributes.remove(tl.data.color_attributes["Color"])
    tl.data.color_attributes.active_color = tl.data.color_attributes["Glow"]
    bm = bmesh.new(); ring(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rg = k.new_object("Meteor ring", bm, cr, SLOTS)
    k.surface_uvs(rg)
    print("[r1 meteor] stone", k.tris([st]), "tail", k.tris([tl]), "ring", k.tris([rg]), "tris")
    return cm, cr, st, tl, rg


def stills(out, st, tl, rg):
    import ro_r0_star_model as r0
    os.makedirs(out, exist_ok=True)
    # a piece of line under it for scale, and the stone mid-fall
    import ro_parts as Pp
    lc = bpy.data.collections.new("context line")
    bpy.context.scene.collection.children.link(lc)
    k.motif_reset()
    Pp.line_bridge(lc, "Dipper line 2-3", Vector((-4, 0, 0)), Vector((4, 0, 0)), 3.0, .4)
    for o in (st, tl):
        o.location = (0, 0, 3.2)
    # the tail as Unity's additive particle material draws it: glow colour times its alpha, added over what is behind
    m = bpy.data.materials.get("RO | glass")
    if m:
        nt = m.node_tree
        for nd in list(nt.nodes):
            if nd.type != "OUTPUT_MATERIAL":
                nt.nodes.remove(nd)
        outn = next(nd for nd in nt.nodes if nd.type == "OUTPUT_MATERIAL")
        attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Glow"
        mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"; mul.inputs[1].default_value = .55
        nt.links.new(attr.outputs["Alpha"], mul.inputs[0])
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(attr.outputs["Color"], em.inputs["Color"]); nt.links.new(mul.outputs[0], em.inputs["Strength"])
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        add = nt.nodes.new("ShaderNodeAddShader")
        nt.links.new(em.outputs[0], add.inputs[0]); nt.links.new(tr.outputs[0], add.inputs[1])
        nt.links.new(add.outputs[0], outn.inputs["Surface"])
        try:
            m.surface_render_method = "BLENDED"
        except (AttributeError, TypeError):
            pass
    shots = {
        "r1_meteor_falling": (Vector((9.0, -11.0, 4.5)), Vector((0, 0, 4.0)), 30),
        "r1_meteor_stone_close": (Vector((1.4, -1.8, 3.6)), Vector((0, 0, 3.2)), 45),
        "r1_meteor_ring": (Vector((3.0, -5.0, 5.0)), Vector((0, 0, 0)), 32),
    }
    r0.stills([], out, shots=shots, centre=Vector((0, 0, 0)), floor_at=(0, 0, -18), fill_offset=(5, -6, 7))


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    cm, cr, st, tl, rg = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r1_meteor.blend"))
    if "--export" in argv:
        k.export(cm.name, "RoMeteor", ZONE)
        k.export(cr.name, "RoMeteorRing", ZONE)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r1"), st, tl, rg)
