"""R4 Cassiopeia (ro-hall session, R725, 2026-11-19; the user to ro-hall: "카시오 진행" - taken over from ro-shell).

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python ro_r4_cassiopeia_model.py -- --part <name>

One part at a time beside RO_A2 / RO_3 for the user to pass (PLAN.md 2), then put together and exported:
  star   Cassiopeia star 1, the one that stands: ro-path's star platform (ro_parts.star_platform, the user's "확인" on
         the Dipper's), its own round motif, lamps either side of the way in from the globe and away from the way out
  gear   the star gears (Device_StarGear Cassiopeia gear 2 .. 5: 16, 20, 16 and 12 teeth) - added next
The meteors are ro-path's (MeteorShowerDress dresses Cassiopeia's shower too).
Numbers are RoyalObservatoryBuilder.cs's; walking faces, colliders and pivots stay the builder's.
Axes: Blender (x, y, z) = Unity (x, z, y).
"""
import math, os, sys
import bpy
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_r3_globe_model as g      # noqa: E402  (Mesh, the part pictures)
import ro_kit as k                 # noqa: E402
import ro_parts as P               # noqa: E402
import bmesh                       # noqa: E402


def make_solid(m, name, coll):
    """Weld the pieces' shared corners (each grid makes its own), so the teeth are closed solids whose faces point out,
    then keep every face both ways (the thin enamel plates and the cog ring are seen from under too) - Unity draws
    front faces only (the user's screenshot, 11-19: faces missing, "부자연스러워")."""
    bmesh.ops.remove_doubles(m.bm, verts=m.bm.verts[:], dist=1e-4)
    return m.make(name, coll, two_sided=True)

Mesh, U = g.Mesh, g.U
STILLS = os.path.join(HERE, "stills", "r4")
BLEND = os.path.join(HERE, "ro_r4_cassiopeia.blend")

STAR1 = (19.5, 12.0, 66.5)
STAR1_R = 3.0
EAST_GATE_IN = (15.4, 12.0, 64.0)       # the east threshold's middle, where the way comes from the globe
GEARS = [  # name, Unity centre (deck top), teeth
    ("Cassiopeia gear 2", (23.7, 12.0, 72.1), 16),
    ("Cassiopeia gear 3", (18.3, 12.0, 79.3), 20),
    ("Cassiopeia gear 4", (23.7, 12.0, 86.5), 16),
    ("Cassiopeia gear 5", (19.76, 12.0, 92.29), 12),
]


def heading(a, b):
    """Blender heading (degrees, 0 = +x, counter-clockwise) from Unity point a to b."""
    return math.degrees(math.atan2(b[2] - a[2], b[0] - a[0]))


# ---------------------------------------------------------------- the gear look (RO_A2's star gear, RO_3)

TOOTH, TIP = .45, .32          # a tooth's share of the pitch at its root and at its tip (the rest is the gap)


def gear_deck(m, M, outer, inner, teeth, tip_r, notch, compass=True, under_r=0.0):
    """A star gear as RO_A2 / RO_3 draw it, in frame M (Blender; z up, its walking top at z 0). The user (11-19: "톱니바퀴가
    맞물려서 돌아가는 걸 더 잘 표현해줘"): real teeth you see interlock -
      the top: navy enamel (a full disc when inner is 0, else the builder's ring - its hole stays open), a gold ring;
      the band of teeth: a tooth a pitch, 45 % of it at the root narrowing to 32 % at the tip (a trapezoid), cream
        marble and violet enamel by turns, out to `tip_r` - flush with the walking face to the builder's rim (`outer`),
        3 cm under it beyond, so it reads as the tooth's tip, not floor; between teeth the gap, cut `notch` in from the
        rim (navy), where the next gear's tooth comes in; 0.7 m deep, gold seams and a gold cap on every tip;
      the under-gear (when under_r > 0, the pitch radius): a gold toothed wheel under the deck (y -0.55 .. -0.95, the
        builder's cogs' place), its trapezoid teeth at the same angles, so two gears' wheels mesh where they meet;
      a gold cog ring and a navy soffit under it all.
    Tooth i is at i * 360 / teeth degrees from local +y (the builder's tooth 1 at heading 0, local +z in Unity)."""
    pitch = 2 * math.pi / teeth
    band = min(.75, (outer - inner) * .5) if inner > 0 else .75
    r_band = outer - band
    gap_r = outer - notch
    H = .7
    seg = teeth * 6
    if inner > 0:
        m.revolve([(inner, 0), (r_band, 0)], "navy", seg, M, smooth=False)
        pass  # (gold ring taken out, the user 11-19) m.revolve([(inner, .002), (inner + .06, .002)], "gold", seg, M, smooth=False)
        m.revolve([(inner, 0), (inner, -.5)], "gold", seg, M, smooth=False)
    else:
        m.revolve([(.001, 0), (r_band * .5, 0), (r_band, 0)], "navy", seg, M, smooth=False)
    # (gold ring taken out, the user 11-19: "금 테 ... 그냥 지워버려") m.revolve([(r_band - .08, .002), (r_band, .002)], "gold", seg, M, smooth=False)

    def P(r, a, z):
        return M @ Vector((r * math.sin(a), r * math.cos(a), z))

    def tooth(ac, r0, r1, f0, f1, z_top, z_bot, slot, n=4):
        """A trapezoid tooth: from radius r0 (share f0 of the pitch) to r1 (share f1), z_top .. z_bot, closed."""
        rows_top, rows_bot = [], []
        for j in range(n + 1):
            r = r0 + (r1 - r0) * j / n
            f = f0 + (f1 - f0) * j / n
            rows_top.append((P(r, ac - pitch * f / 2, z_top), P(r, ac + pitch * f / 2, z_top)))
            rows_bot.append((P(r, ac - pitch * f / 2, z_bot), P(r, ac + pitch * f / 2, z_bot)))
        m.grid([[q[0] for q in rows_top], [q[1] for q in rows_top]][::-1], slot, smooth=False)       # top
        m.grid([[q[0] for q in rows_bot], [q[1] for q in rows_bot]], slot, smooth=False)             # bottom
        m.grid([[q[0] for q in rows_top], [q[0] for q in rows_bot]], slot, smooth=False)             # sides
        m.grid([[q[1] for q in rows_bot], [q[1] for q in rows_top]], slot, smooth=False)
        m.face([rows_top[-1][0], rows_top[-1][1], rows_bot[-1][1], rows_bot[-1][0]], slot)           # tip face
        m.face([rows_top[0][1], rows_top[0][0], rows_bot[0][0], rows_bot[0][1]], slot)               # root face
        return rows_top[-1]

    for i in range(teeth):
        ac = i * pitch
        slot = "marble" if i % 2 == 0 else "violet"
        # the root of the tooth on the band, flush, out to the rim
        tooth(ac, r_band, max(tip_r, outer), TOOTH, TIP if tip_r > outer + .01 else TOOTH, 0.0, -H, slot, 6)
        # the gap between this tooth and the next: navy, cut in to gap_r
        g0, g1 = ac + pitch * TOOTH / 2, ac + pitch * (1 - TOOTH / 2)
        gp = (1 - TOOTH)                          # the gap's share of the pitch at the band
        tooth(ac + pitch / 2, r_band, gap_r, gp, gp, 0.0, -H, "navy", 2)
        for a_ in (g0, g1):
            sm = M @ Matrix.Rotation(-a_, 4, "Z")
            pass  # (gold seam taken out, 11-19) m.box((0, (r_band + outer) / 2, .002), (.05, outer - r_band, .004), "gold", sm)
        pass

    # gold cog ring and the soffit
    m.revolve([(gap_r, -H), (gap_r - .05, -H - .06), (gap_r - .3, -H - .1), (gap_r - .35, -H - .25), (gap_r - .5, -H - .3)],
              "gold", seg, M)
    m.revolve([(gap_r - .5, -H - .3), (max(inner, .4), -H - .3)], "navy", seg, M, smooth=False)
    if inner > 0:
        m.revolve([(max(inner, .4), -H - .3), (inner, -.5)], "navy", seg, M, smooth=False)
    if compass and inner == 0:
        compass_star(m, M, r_band - .15)



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


def compass_star(m, M, R):
    """RO_3's gold compass on a gear's top: four long points and four short, two-toned along their ridges, a boss,
    small stars between (2-3 mm proud)."""
    star_fan(m, lambda x, y: M @ Vector((x, y, .015)), [(math.pi / 4 * j, R if j % 2 == 0 else R * .55) for j in range(8)],
             R * .2)                                                # one fan, no ray over another (11-20)
    m.revolve([(.001, .026), (R * .12, .022)], "gold", 24, M, smooth=False)
    for j in range(8):
        t = math.pi / 4 * j + math.pi / 8
        m.star3d(M @ Matrix.Translation((R * .72 * math.sin(t), R * .72 * math.cos(t), .019)), R * .06, 0, R * .02, 4, .003, "gold")


def axle(m, M, top_z, bottom_z, r=.345):
    """A fluted gold axle from under a gear down to the hall's floor level (y -18), radius r there (ro-shell's
    abyss continuation takes it on downward): a collar under the deck, the fluted shaft, a ring every 6 m."""
    n = 24
    ring_lo = [M @ Vector(((r - (.04 if i % 2 else 0)) * math.cos(2 * math.pi * i / n), (r - (.04 if i % 2 else 0)) * math.sin(2 * math.pi * i / n),
                           bottom_z)) for i in range(n)]
    ring_hi = [Vector((q.x, q.y, M.translation.z + top_z)) for q in ring_lo]
    m.grid([ring_lo + ring_lo[:1], ring_hi + ring_hi[:1]], "gold", smooth=False)
    m.revolve([(r, top_z), (r + .15, top_z + .1), (r + .3, top_z + .3), (r + .2, top_z + .45), (.001, top_z + .5)], "gold", 24, M)
    z = top_z - 6
    while z > bottom_z + 1:
        m.revolve([(r - .01, z - .1), (r + .07, z - .05), (r + .07, z + .05), (r - .01, z + .1)], "gold", 24, M)
        z -= 6


def star(coll):
    """Cassiopeia's standing star as a gear (the user, 11-19: "톱니로 해" - RO_3 draws it as one): the builder's round
    deck (radius 3, y 12) dressed as a gear of twelve teeth that do not stand out past it (gear 2 meshes against its
    edge), the compass on a full navy top, the rim band of marble and violet tooth tiles, the teeth hanging inside the
    rim, a gold cog ring, the axle down; four lamps (ro_parts.lamp_post) on the rim tiles either side of the way in from
    the globe and on the widest stretch clear of the way out."""
    import ro_stars_model as rs
    m = Mesh()
    M = Matrix.Translation(k.at(STAR1))
    gear_deck(m, M, STAR1_R - .3, 0.0, 16, STAR1_R - .3, .32)
    axle(m, M, -1.1, -18.0 - STAR1[1])
    objs = [make_solid(m, "Cassiopeia star 1 gear", coll)]
    h_in = heading(STAR1, EAST_GATE_IN)
    h_out = heading(STAR1, GEARS[0][1])
    lamps = []
    for h in rs.lamp_headings(h_in, [h_in, h_out], STAR1_R):
        foot = k.at(STAR1) + Vector((math.cos(math.radians(h)), math.sin(math.radians(h)), 0)) * (STAR1_R - .3)
        post, globe = P.lamp_post(coll, "Cassiopeia star 1 lamp %.0f" % h, foot)
        objs += post
        lamps.append(globe)
    print("[r4] star 1: %d tris, %d lamps" % (k.tris(objs), len(lamps)))
    return objs, lamps


# ---------------------------------------------------------------- 2 the star gears

GEAR_GAP = .08


def gear_dims(teeth):
    R = teeth * .25
    outer = R - GEAR_GAP
    width = min(1.8, R * .45)
    return R, outer, outer - width


def gear_model(m, M, teeth):
    """One star gear in its device's frame M (Device_StarGear: turns about its up axis; tooth i of the builder at i x
    pitch from local forward): the builder's rim (its walking ring, outer - width .. outer, 0.5 deep) as RO_3's gear -
    the navy ring top and the marble / violet tooth band (gear_deck), the teeth standing out past the rim under its
    top, where the builder's cogs mesh (to radius + 0.3, as wide as its cogs, so two gears' teeth pass between each
    other); the hub (a gold disc with the compass, 1.8 m across) and four spokes (navy beams with gold edges) - all
    flush with the walking faces - and the hole left open between them."""
    R, outer, inner = gear_dims(teeth)
    pitch = 360.0 / teeth
    gear_deck(m, M, outer, inner, teeth, R + .24, .42, compass=False, under_r=R)
    # small gold stars on the navy ring (2 mm)
    rr = (inner + outer - min(.75, (outer - inner) * .5)) / 2
    for j in range(teeth // 2):
        a = 2 * math.pi * (j + .25) / (teeth // 2)
        m.star3d(M @ Matrix.Translation((rr * math.sin(a), rr * math.cos(a), .012)), .14, .08 if j % 2 else 0, .045, 4, .003, "gold")
    # the hub
    m.revolve([(.001, 0), (.9, 0)], "gold", 32, M, smooth=False)
    m.revolve([(.9, 0), (.9, -.5), (.6, -.65), (.001, -.65)], "gold", 32, M)
    compass_star(m, M @ Matrix.Translation((0, 0, .0)), .8)
    # the spokes (the builder's: 0.9 wide, from 0.7 to the rim, 0.4 deep, at s x 90 + half a pitch)
    for sp in range(4):
        a = math.radians(sp * 90 + pitch * .5)
        sm = M @ Matrix.Rotation(-a, 4, "Z")
        L0, L1 = .92, inner + .05          # from outside the hub disc (r .9): no shared top (11-20)
        m.box((0, (L0 + L1) / 2, -.2), (.9, L1 - L0, .4), "navy", sm)
        for sx in (-1, 1):
            m.box((sx * .43, (L0 + L1) / 2, -.2), (.06, L1 - L0, .42), "gold", sm)
        pass  # (gold strip taken out, 11-19) m.box((0, (L0 + L1) / 2, .002), (.12, L1 - L0 - .2, .004), "gold", sm)
        for j in range(3):
            y_ = L0 + (L1 - L0) * (j + 1) / 4
            m.star3d(sm @ Matrix.Translation((0, y_, .012)), .16, 0, .05, 4, .003, "gold")


def gears(coll, local=False):
    """The four gears at the builder's places and poses (ro_blockout.json - the pose Unity dumped, so the pictures
    show them meshed as they run) and their axles (static: the builder's axle pillars)."""
    out = []
    for name, centre, teeth in GEARS:
        m = Mesh()
        M = Matrix() if local else k.frame("Device_StarGear " + name)
        gear_model(m, M, teeth)
        out.append(make_solid(m, name + " gear", coll))
        if not local:
            a = Mesh()
            A = Matrix.Translation(k.at(centre))
            axle(a, A, -1.1, -18.0 - centre[1])
            out.append(a.make(name + " axle", coll))
    return out


def library_look():
    for m_ in bpy.data.materials:
        m_.use_backface_culling = True
    _library_look()


def _library_look():
    """The pictures in ro-path's library materials (the parts made with ro_kit's slots), the motif atlas on its UVs."""
    g.library_materials()
    m = bpy.data.materials.get("RO | motif")
    if m is not None:
        nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(HERE, "textures", "library", "RO_Motif_Albedo.png"), check_existing=True)
        nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
        p.inputs["Metallic"].default_value = .3


PARTS = {
    "gear": (["Cassiopeia gear", "Cassiopeia star 1"], [("AssetRefs/RO_A2_course_devices.png", (384, 300, 768, 640)),
                                                       ("RO_3_cassiopeia_telescope.png", (430, 560, 1420, 1024))]),
    "star": (["Cassiopeia star 1"], [("AssetRefs/RO_A2_course_devices.png", (384, 300, 768, 640)),
                                     ("RO_3_cassiopeia_telescope.png", (430, 560, 760, 900))]),
}


def build(which):
    for c in list(bpy.data.collections):
        if c.name.startswith("RO R4"):
            for o in list(c.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(c)
    root = bpy.data.collections.new("RO R4 Cassiopeia")
    bpy.context.scene.collection.children.link(root)
    lamps = []
    if which in ("gear", "all"):
        gears(root)
    if which in ("star", "all", "gear"):
        _, at = star(root)
        lamps += at
    return lamps


ZONE = "Cassiopeia"


def export_all(lamps):
    """FBX to Assets/Resources/RoyalObservatory/Models/Cassiopeia/: RoCassiopeiaStatic (star 1 as a gear, its lamps, the
    four axles; the section's frame) with its lamps beside it, and RoGear16 / RoGear20 / RoGear12, each in its
    device's own frame (the gears turn: Device_StarGear Cassiopeia gear 2 .. 5 carry them)."""
    root = bpy.data.collections["RO R4 Cassiopeia"]
    col = bpy.data.collections.new("RO export RoCassiopeiaStatic")
    bpy.context.scene.collection.children.link(col)
    for o in [o for o in root.objects if o.type == "MESH" and (o.name.startswith("Cassiopeia star 1") or o.name.endswith(" axle"))]:
        root.objects.unlink(o)
        col.objects.link(o)
        g.metre_uvs(o)
    k.export(col.name, "RoCassiopeiaStatic", ZONE, None)
    k.write_lamps(ZONE, "RoCassiopeiaStatic", lamps)
    done = set()
    for name, centre, teeth in GEARS:
        if teeth in done:
            continue
        done.add(teeth)
        col = bpy.data.collections.new("RO export RoGear%d" % teeth)
        bpy.context.scene.collection.children.link(col)
        m = Mesh()
        gear_model(m, Matrix(), teeth)
        ob = make_solid(m, "RoGear%d" % teeth, col)
        g.metre_uvs(ob)
        k.export(col.name, "RoGear%d" % teeth, ZONE, None)


def assembly_stills():
    """The parts put together, in ro-path's library, lit like the hall, beside RO_3."""
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
    library_look()
    for m_ in bpy.data.materials:
        m_.use_backface_culling = True
    w = sc.world
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
    c = k.at(GEARS[1][1])
    for name, loc, energy, col, size in (("Moonlight", c + Vector((-30, 40, 60)), 90000, (.7, .78, 1), 30),
                                         ("Warm key", c + Vector((35, -40, 25)), 70000, (1, .8, .55), 25)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy, ld.color, ld.size = energy, col, size
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = (c - lo.location).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)
    out = os.path.join(STILLS, "assembly")
    os.makedirs(out, exist_ok=True)
    mid = (k.at(STAR1) + k.at(GEARS[3][1])) / 2
    shots = {
        "r4_whole": (mid + Vector((26, -14, 16)), mid + Vector((0, 0, -2)), 30, ("RO_3_cassiopeia_telescope.png", (430, 560, 1420, 1024))),
        "r4_on_gear": (k.at(STAR1) + Vector((0, -2, 2.4)), k.at(GEARS[0][1]) + Vector((0, 0, .5)), 24,
                       ("RO_3_cassiopeia_telescope.png", (430, 560, 1420, 1024))),
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
        export_all(lamps)
    if "--assembly" in args:
        assembly_stills()
    if "--part" in args:
        g.PARTS = PARTS
        g.STILLS = STILLS
        g.DARK_PARTS = set()
        g.VIEWS = {"star": (Vector((.4, -1, .55)), Vector((-1, -.4, .5))), "gear": (Vector((.9, -.6, .55)), Vector((-.7, -1, .45)))}
        c = k.at(STAR1)
        c2 = k.at(GEARS[1][1])
        mid = (k.at(GEARS[0][1]) + k.at(GEARS[2][1])) / 2
        c2, c3 = k.at(GEARS[0][1]), k.at(GEARS[1][1])
        mp = c2 + (c3 - c2) * (4.0 / 9.0)
        side = (c3 - c2).cross(Vector((0, 0, 1))).normalized()
        g.CLOSE = {"gear": {"gear_mesh": (mp + side * 5.5 + Vector((0, 0, 3.2)), mp + Vector((0, 0, -.4)), 30, (430, 560, 1420, 1024),
                                          "RO_3_cassiopeia_telescope.png"),
                            "gear_mesh_top": (mp + Vector((0, -.01, 9)), mp, 30, (430, 560, 1420, 1024), "RO_3_cassiopeia_telescope.png"),
                            "gear_below": (mp + side * 4 + Vector((0, 0, -3.5)), mp + Vector((0, 0, -.3)), 30, (430, 560, 1420, 1024),
                                           "RO_3_cassiopeia_telescope.png"),
                            "gear_close": (mid + Vector((16, -9, 11)), mid + Vector((0, 0, -1.5)), 30, (430, 560, 1420, 1024),
                                           "RO_3_cassiopeia_telescope.png")},
                   "star": {"star_close": (c + Vector((6, -7, 4.5)), c + Vector((0, 0, -.6)), 35, (430, 560, 760, 900),
                                           "RO_3_cassiopeia_telescope.png")}}
        library_look()
        g.part_stills(part)
