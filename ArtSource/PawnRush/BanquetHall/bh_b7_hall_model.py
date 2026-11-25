"""B7 the grand banquet hall's shell (bh-hall session, seoungtest R838, 2026-11-25): the walls' bays, the walnut pilasters,
the arched doorways, the wall sconces, the coffered ceiling and its roses, banners (PLAN.md 1 "B7 홀 셸").

    blender -b --factory-startup --python bh_b7_hall_model.py -- --part window_bay

One part at a time (PLAN 2: each part alone beside its R836 sheet cell, passed by the user before the next; the hall is
put together after). --part <name> builds that part alone at the origin, saves bh_b7_hall_<name>.blend and writes
stills/b7/parts/<name>_compare.png: the sheet's cell, a render as the sheet draws it (white, 3/4 from the front
left), a render in the hall's candle light, a close one.

Until bh-path's bh_kit.py exists the shape is all this does (no export); material slots are PLAN 2's names, previewed
with bh-path's library textures (textures/library, read only).

Part frame: x along the wall (east of it when facing it), y out of the wall into the hall (the wall's face at y 0),
z up, metres. The builder's numbers (GrandBanquetHallBuilder.Shell): the hall x -32 .. 32, z 0 .. 72, ceiling 34; walls
1 m thick outside that; the south doorway x -6 .. 6 with its lintel from 7 m; the north exit x -6 .. 6 from the
balcony (16) to 24; the loft against the south wall at 16 .. 20, x -31 .. 4.6. Nothing here is walked on: the shell's
colliders stay the builder's.

Proportions are the sheet's (BH_A01: a bay with its wainscot, stone, window and cornice is 254 x 545 px; the pilaster's
shaft 115 px), measured in its pixels and scaled to a storey (STOREY m). The hall wall is two storeys (R787: an
arcade storey under a window storey): the plain-panel bay below, the window bay above.
"""
import json, math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SHEETS = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R836_BanquetHallAssets", "AssetRefs")
R787 = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R787_BanquetHall", "banquet_concept.webp")
LIB = os.path.join(HERE, "textures", "library")
STILLS = os.path.join(HERE, "stills", "b7")

STOREY = 16.0                 # one storey of the wall (the balcony's floor, 16, is the line between them)
PX = STOREY / 545.0           # metres per sheet pixel (BH_A01's bay is 545 px tall)

# ---------------------------------------------------------------- material slots (PLAN 2; bh_kit.slot() later)
# slot -> (library texture, preview colour if no texture (linear), metallic, roughness)
SLOTS = {
    "stone": ("BH_Stone", (.75, .62, .40), 0.0, .7),
    "walnut": ("BH_Walnut", (.10, .035, .015), 0.0, .45),
    "gold": ("BH_Gold", (.68, .42, .10), 1.0, .25),
    "silver": ("BH_Silver", (.55, .56, .58), 1.0, .2),
    "linen": ("BH_Linen", (.86, .82, .74), 0.0, .8),
    "runner": ("BH_Runner", (.09, .13, .025), 0.0, .78),
    "velvet": ("BH_Velvet", (.19, .006, .009), 0.0, .8),
    "marble": ("BH_Marble", (.86, .77, .62), 0.0, .22),
    "floor_light": ("BH_FloorLight", (.84, .76, .60), 0.0, .2),
    "floor_dark": ("BH_FloorDark", (.10, .035, .016), 0.0, .3),
    "chocolate": ("BH_Chocolate", (.065, .018, .009), 0.0, .14),
    "crystal": (None, (.80, .88, .95), 0.0, .03),
    "wax": ("BH_Wax", (.88, .79, .58), 0.0, .55),
    "piece_white": ("BH_Ivory", (.84, .77, .60), 0.0, .38),
    "piece_black": ("BH_Ebony", (.008, .005, .004), 0.0, .3),
    # added by bh-path to bh_kit.SLOTS after PLAN 2
    "olive": ("BH_Olive", (.07, .10, .012), 0.0, .8),
    "damask": ("BH_GoldDamask", (.66, .38, .07), 1.0, .22),
    "china": ("BH_China", (.86, .85, .82), 0.0, .12),
    "emerald": ("BH_Emerald", (.035, .10, .012), 0.0, .05),
}
_TILES = {}


def _tiles():
    if not _TILES:
        try:
            with open(os.path.join(LIB, "bh_textures.json")) as f:
                for k, v in json.load(f).items():
                    _TILES[k] = v.get("tile", 1.0)
        except OSError:
            pass
    return _TILES


def slot(name):
    key = "BH | " + name
    m = bpy.data.materials.get(key)
    if m is not None:
        return m
    tex, colour, metal, rough = SLOTS[name]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*colour, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if name == "crystal":
        # preview of Unity's clear BH_Crystal: a faint blue-white sheen over what is behind
        p.inputs["Base Color"].default_value = (.85, .92, 1.0, 1)
        p.inputs["Roughness"].default_value = .03
        p.inputs["Alpha"].default_value = .14
        try:
            m.surface_render_method = "BLENDED"
        except (AttributeError, TypeError):
            m.blend_method = "BLEND"
    alb = os.path.join(LIB, (tex or "") + "_Albedo.png")
    if tex and os.path.exists(alb):
        t = _tiles().get(tex, 1.0)
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1 / t, 1 / t, 1)
        nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
        ti = nt.nodes.new("ShaderNodeTexImage")
        ti.image = bpy.data.images.load(alb, check_existing=True)
        nt.links.new(mp.outputs["Vector"], ti.inputs["Vector"])
        nt.links.new(ti.outputs["Color"], p.inputs["Base Color"])
        # alpha = smoothness
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(ti.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs[0], p.inputs["Roughness"])
        nrm = os.path.join(LIB, tex + "_Normal.png")
        if os.path.exists(nrm):
            ni = bpy.data.images.load(nrm, check_existing=True)
            ni.colorspace_settings.name = "Non-Color"
            nn = nt.nodes.new("ShaderNodeTexImage")
            nn.image = ni
            nt.links.new(mp.outputs["Vector"], nn.inputs["Vector"])
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nn.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
    m.diffuse_color = (*colour, 1)
    return m


# ---------------------------------------------------------------- mesh helpers (bmesh, material index = slot order)

class Mesh:
    """A bmesh with named slots: m.mat("gold") gives the material index for that slot."""

    def __init__(self):
        self.bm = bmesh.new()
        self.slots = []

    def mat(self, name):
        if name not in self.slots:
            self.slots.append(name)
        return self.slots.index(name)

    def finish(self, name, col, bevel=.02, angle=35.0, segments=2, uv=True, merge=True, recalc=False):
        ob = bpy.data.objects.new(name, bpy.data.meshes.new(name))
        if merge:   # (not for meshes of touching boxes: merged, they bevel into slivers)
            bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        if recalc:   # closed turned shapes built in any order: point their faces outward
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.normal_update()
        self.bm.to_mesh(ob.data)
        self.bm.free()
        for s in self.slots:
            ob.data.materials.append(slot(s))
        col.objects.link(ob)
        if uv:
            box_uvs(ob)
        for p in ob.data.polygons:
            p.use_smooth = True
        if bevel:
            b = ob.modifiers.new("Bevel", "BEVEL")
            b.width, b.segments, b.limit_method = bevel, segments, "ANGLE"
            b.angle_limit = math.radians(angle)
            b.harden_normals = True
            b.miter_outer = "MITER_ARC"
        wn = ob.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
        wn.keep_sharp = True
        return ob


def face(bm, verts, mi, smooth=False):
    try:
        f = bm.faces.new(verts)
    except ValueError:
        return None
    f.material_index = mi
    f.smooth = smooth
    return f


def box(m, slot_name, x0, x1, y0, y1, z0, z1):
    mi = m.mat(slot_name)
    bm = m.bm
    v = [bm.verts.new(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                                     (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        face(bm, [v[i] for i in q], mi)


def prism_yz(m, slot_name, poly, x0, x1):
    """A closed outline in the (y, z) plane (a moulding's section) run along x from x0 to x1, ends capped."""
    mi = m.mat(slot_name)
    bm = m.bm
    area = sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
    pts = poly if area > 0 else list(reversed(poly))
    a = [bm.verts.new((x0, y, z)) for y, z in pts]
    b = [bm.verts.new((x1, y, z)) for y, z in pts]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        face(bm, (a[i], a[j], b[j], b[i]), mi)
    face(bm, list(reversed(a)), mi)
    face(bm, b, mi)


def prism_xz(m, slot_name, poly, y0, y1, cap_back=True):
    """A closed outline in the (x, z) plane (the wall's face) extruded out of the wall from y0 to y1."""
    mi = m.mat(slot_name)
    bm = m.bm
    area = sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))
    pts = poly if area > 0 else list(reversed(poly))
    a = [bm.verts.new((x, y0, z)) for x, z in pts]
    b = [bm.verts.new((x, y1, z)) for x, z in pts]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        face(bm, (a[i], b[i], b[j], a[j]), mi)
    if cap_back:
        face(bm, a, mi)
    face(bm, list(reversed(b)), mi)


def ring_xz(m, slot_name, outer, inner, y0, y1, back=False):
    """A frame: the band between two outlines in the (x, z) plane with the same number of points (open loops are
    closed), from y0 out to y1: front face, both sides, optionally the back."""
    mi = m.mat(slot_name)
    bm = m.bm
    n = len(outer)
    oa = [bm.verts.new((x, y0, z)) for x, z in outer]
    ob = [bm.verts.new((x, y1, z)) for x, z in outer]
    ia = [bm.verts.new((x, y0, z)) for x, z in inner]
    ib = [bm.verts.new((x, y1, z)) for x, z in inner]
    for i in range(n):
        j = (i + 1) % n
        face(bm, (ob[i], ib[i], ib[j], ob[j]), mi)       # front
        face(bm, (oa[i], ob[i], ob[j], oa[j]), mi)       # outer side
        face(bm, (ia[j], ib[j], ib[i], ia[i]), mi)       # inner side (reveal)
        if back:
            face(bm, (oa[j], ia[j], ia[i], oa[i]), mi)
    bm.normal_update()


def arch_outline(cx, z0, w, spring, segs=24):
    """A round-headed opening (x, z), counter-clockwise from the bottom left: sides up to `spring`, a half circle."""
    r = w / 2
    pts = [(cx - r, z0), (cx + r, z0), (cx + r, spring)]
    for i in range(1, segs):
        t = math.pi * i / segs
        pts.append((cx + r * math.cos(t), spring + r * math.sin(t)))
    pts.append((cx - r, spring))
    return pts


def arch_offset(cx, z0, w, spring, d, segs=24):
    """arch_outline grown by d (the same point count, so ring_xz can join the two)."""
    r = w / 2 + d
    pts = [(cx - r, z0 - d), (cx + r, z0 - d), (cx + r, spring)]
    for i in range(1, segs):
        t = math.pi * i / segs
        pts.append((cx + r * math.cos(t), spring + r * math.sin(t)))
    pts.append((cx - r, spring))
    return pts


def field_with_hole(m, slot_name, x0, x1, z0, z1, hole, y=0.0, reveal=.6, facing=1):
    """A flat wall face (x0..x1, z0..z1 at y) with a hole (an outline in x, z), the hole's sides going `reveal` back."""
    mi = m.mat(slot_name)
    bm = m.bm
    rect = [bm.verts.new((x, y, z)) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
    hv = [bm.verts.new((x, y, z)) for x, z in hole]
    edges = [bm.edges.new((rect[i], rect[(i + 1) % 4])) for i in range(4)]
    edges += [bm.edges.new((hv[i], hv[(i + 1) % len(hv)])) for i in range(len(hv))]
    got = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=(0, 1, 0))
    for f in got["geom"]:
        if isinstance(f, bmesh.types.BMFace):
            f.material_index = mi
    # the reveal
    if reveal > 0:
        hb = [bm.verts.new((x, y - reveal, z)) for x, z in hole]
        n = len(hole)
        for i in range(n):
            j = (i + 1) % n
            face(bm, (hv[i], hb[i], hb[j], hv[j]), mi)
    bm.normal_update()
    # the fill's faces point out of the wall (+y; facing -1: the outside face, -y)
    for f in got["geom"]:
        if isinstance(f, bmesh.types.BMFace) and f.normal.y * facing < 0:
            f.normal_flip()


def lathe(m, slot_names, profile, at=(0, 0, 0), segs=24, axis=Matrix()):
    """A turned shape round local z: profile [(r, z), ...] bottom up; slot_names one name or one per span."""
    bm = m.bm
    rings = []
    for i in range(segs):
        t = 2 * math.pi * i / segs
        c, s = math.cos(t), math.sin(t)
        rings.append([bm.verts.new(Vector(at) + axis @ Vector((r * c, r * s, z))) for r, z in profile])
    span = len(profile) - 1
    for i in range(segs):
        r0, r1 = rings[i], rings[(i + 1) % segs]
        for j in range(span):
            nm = slot_names if isinstance(slot_names, str) else slot_names[j]
            mi = m.mat(nm)
            if profile[j][0] < 1e-6 and profile[j + 1][0] < 1e-6:
                continue
            if profile[j][0] < 1e-6:
                face(bm, (r0[j], r1[j + 1], r0[j + 1]), mi, True)
            elif profile[j + 1][0] < 1e-6:
                face(bm, (r0[j], r1[j], r0[j + 1]), mi, True)
            else:
                face(bm, (r0[j], r1[j], r1[j + 1], r0[j + 1]), mi, True)


def tube(m, slot_name, pts, r, sides=10, cap=True, taper=None):
    """A round tube along a polyline of Vectors (radius r, or taper(i) per point)."""
    mi = m.mat(slot_name)
    bm = m.bm
    pts = [Vector(p) for p in pts]
    rings = []
    prev_n = None
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        up = Vector((0, 0, 1)) if abs(d.z) < .9 else Vector((1, 0, 0))
        n1 = d.cross(up).normalized()
        if prev_n is not None and n1.dot(prev_n) < 0:
            n1 = -n1
        prev_n = n1
        n2 = d.cross(n1).normalized()
        rr = taper(i) if taper else r
        rings.append([bm.verts.new(p + rr * (math.cos(2 * math.pi * k / sides) * n1 + math.sin(2 * math.pi * k / sides) * n2))
                      for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            face(bm, (a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]), mi, True)
    if cap:
        face(bm, list(reversed(rings[0])), mi, True)
        face(bm, rings[-1], mi, True)


def ball(m, slot_name, at, r, u=16, v=10, scale=(1, 1, 1)):
    mi = m.mat(slot_name)
    got = bmesh.ops.create_uvsphere(m.bm, u_segments=u, v_segments=v, radius=r,
                                    matrix=Matrix.Translation(at) @ Matrix.Diagonal((*scale, 1)))
    for f in {f for vv in got["verts"] for f in vv.link_faces}:
        f.material_index = mi
        f.smooth = True


# Slot -> metres one library tile covers here over its library size: the stone's blocks at the sheet's size (BH_Stone's
# block is 0.6 m; the sheet's courses at a 16 m storey are about 1.4 m).
UV_SCALE = {"stone": 2.4}


def box_uvs(ob, seed=None):
    """Metre UVs by the face's main axis (the library textures are seamless tiles in metres), a random offset an object."""
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    rnd = random.Random(seed if seed is not None else hash(ob.name) & 0xffff)
    ou, ov = rnd.uniform(0, 10), rnd.uniform(0, 10)
    mats = [mt.name[5:] if mt else "" for mt in me.materials]
    for p in me.polygons:
        k = 1.0 / UV_SCALE.get(mats[p.material_index] if p.material_index < len(mats) else "", 1.0)
        n = p.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = ((1, 2), (0, 2), (0, 1))[ax]
        if mats and p.material_index < len(mats) and mats[p.material_index] == "walnut":
            # wood: the grain (the texture's u) along the face's longer side - a beam's grain runs along the beam
            cs = [me.vertices[i].co for i in p.vertices]
            if max(c[b] for c in cs) - min(c[b] for c in cs) > max(c[a] for c in cs) - min(c[a] for c in cs):
                a, b = b, a
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = (co[a] * k + ou, co[b] * k + ov)


# ---------------------------------------------------------------- the bay's shared runs: plinth, wainscot, cornice
# Sheet pixels (BH_A01 window bay, y from its top 237 .. bottom 782), as metres up from the floor: z(px) = (782 - px) * PX

def zp(px):
    return (782 - px) * PX


LEVEL = {
    "plinth": zp(760),          # 0.65  walnut skirting top
    "wainscot": zp(678),        # 3.05  the raised panels' frame top
    "cap": zp(670),             # 3.29  the walnut cap
    "rail": zp(660),            # 3.58  the gold rail on it
    "gold_band": zp(305),       # 14.0  the gold band under the frieze
    "frieze": zp(292),          # 14.39 walnut frieze from here
    "corona": zp(266),          # 15.15 the cornice's projecting top block
    "top": STOREY,
}


def wainscot(m, x0, x1, panels=3):
    """Walnut skirting, three raised walnut panels in their frame, a walnut cap and a gold rail (the sheet's foot)."""
    L = LEVEL
    # skirting: a plinth with a moulded top
    prism_yz(m, "walnut", [(0, 0), (.34, 0), (.34, L["plinth"] - .12), (.28, L["plinth"] - .04), (.24, L["plinth"]),
                           (0, L["plinth"])], x0, x1)
    # the frame (stiles and rails) and the panels
    fy = .18
    box(m, "walnut", x0, x1, 0, fy, L["plinth"], L["wainscot"])
    w = (x1 - x0)
    st = .2 * (w / 7.46) ** .3          # stiles
    rail = .22
    pw = (w - st * (panels + 1)) / panels
    for i in range(panels):
        a = x0 + st + i * (pw + st)
        b = a + pw
        z0, z1 = L["plinth"] + rail, L["wainscot"] - rail
        # sunk field, then a raised fielded panel (bevelled edges)
        box(m, "walnut", a, b, fy - .04, fy - .02, z0, z1)
        e = .22
        mi = m.mat("walnut")
        bm = m.bm
        lo = [bm.verts.new(p) for p in ((a, fy - .02, z0), (b, fy - .02, z0), (b, fy - .02, z1), (a, fy - .02, z1))]
        hi = [bm.verts.new(p) for p in ((a + e, fy + .14, z0 + e), (b - e, fy + .14, z0 + e), (b - e, fy + .14, z1 - e),
                                         (a + e, fy + .14, z1 - e))]
        for k in range(4):
            face(bm, (lo[k], lo[(k + 1) % 4], hi[(k + 1) % 4], hi[k]), mi)
        face(bm, list(reversed(hi)), mi)
        # the frame's moulded edge round the panel (a thin bead)
        for (xa, xb, za, zb) in ((a, b, z0 - .04, z0), (a, b, z1, z1 + .04), (a - .04, a, z0 - .04, z1 + .04),
                                 (b, b + .04, z0 - .04, z1 + .04)):
            box(m, "walnut", xa, xb, fy, fy + .06, za - .02, zb + .02)
    # cap and gold rail
    prism_yz(m, "walnut", [(0, L["wainscot"]), (.27, L["wainscot"]), (.3, L["wainscot"] + .06), (.3, L["cap"]),
                           (0, L["cap"])], x0, x1)
    prism_yz(m, "gold", [(0, L["cap"]), (.16, L["cap"]), (.2, L["cap"] + .07), (.2, L["rail"] - .1), (.16, L["rail"]),
                         (0, L["rail"])], x0, x1)


def cornice(m, x0, x1):
    """The gold band, the walnut frieze and the cornice's projecting top block with its cap (the sheet's head)."""
    L = LEVEL
    prism_yz(m, "gold", [(0, L["gold_band"]), (.12, L["gold_band"]), (.15, L["gold_band"] + .09), (.15, L["frieze"] - .09),
                         (.12, L["frieze"]), (0, L["frieze"])], x0, x1)
    prism_yz(m, "walnut", [(0, L["frieze"]), (.2, L["frieze"]), (.2, L["corona"] - .1), (.3, L["corona"] - .02),
                           (0, L["corona"] - .02)], x0, x1)
    top = L["top"]
    prism_yz(m, "walnut", [(0, L["corona"] - .02), (.42, L["corona"] - .02), (.5, L["corona"] + .08), (.5, top - .22),
                           (.62, top - .16), (.62, top - .02), (.58, top), (0, top)], x0, x1)
    # the cornice's soffit step - a thin walnut fillet between frieze and corona
    prism_yz(m, "walnut", [(0, L["corona"] - .16), (.36, L["corona"] - .16), (.36, L["corona"] - .02), (0, L["corona"] - .02)],
             x0, x1)


# ---------------------------------------------------------------- B7 parts

def window_bay(col, W=8.0, name="BH hall window bay"):
    """A01 cell 1: wainscot, cream stone, a tall round-headed window in a walnut frame with gold glazing bars, the
    gold band and walnut cornice. W = the stone field between pilasters."""
    sx = W / 7.46
    m = Mesh()
    L = LEVEL
    wainscot(m, -W / 2, W / 2)
    cornice(m, -W / 2, W / 2)
    # window, sheet px: outer frame 144 px wide, bottom 652, top 330; frame 12 px. A real window (the user, 11-25:
    # "창문 있는 벽은 창문 만들어"): open through the whole 1 m wall (the builder's wall is 1 m thick), a walnut
    # casement with clear glass halfway in, the gold glazing bars on both sides of the glass, a stone sill outside.
    fw = 144 * PX * sx
    fz0, fz1 = zp(652), zp(330)
    fr = 12 * PX
    ow = fw                                    # the frame's outer width
    iw = ow - 2 * fr                           # the opening inside the frame
    spring = fz1 - ow / 2
    WALL = 1.0
    hole = arch_outline(0, fz0, ow, spring, 32)
    field_with_hole(m, "stone", -W / 2, W / 2, L["rail"], L["gold_band"], hole, y=0.0, reveal=WALL)
    field_with_hole(m, "stone", -W / 2, W / 2, L["rail"], L["gold_band"], hole, y=-WALL, reveal=0, facing=-1)
    # the inside frame: an outer flat band and an inner raised bead, both walnut, lining the reveal 0.45 m deep
    inner = arch_outline(0, fz0 + fr, iw, spring, 32)
    ring_xz(m, "walnut", hole, inner, -.45, .16)
    bead_o = arch_offset(0, fz0 + fr, iw, spring, .09, 32)
    ring_xz(m, "walnut", bead_o, inner, -.45, .23)
    lip_o = arch_offset(0, fz0, ow, spring, .07, 32)
    ring_xz(m, "walnut", lip_o, hole, 0, .1)
    # the inside sill under the frame
    box(m, "walnut", -ow / 2 - .2, ow / 2 + .2, -.1, .3, fz0 - .16, fz0 + .02)
    # the casement: a walnut sash round the glass, set in the reveal behind the inside frame
    sash = .13
    glass = arch_outline(0, fz0 + fr + sash, iw - 2 * sash, spring, 32)
    sash_o = arch_outline(0, fz0 + fr, iw, spring, 32)
    ring_xz(m, "walnut", sash_o, glass, -.66, -.45, back=True)
    # the glass, clear, in the middle of the sash
    gy = -.555
    prism_xz(m, "crystal", glass, gy - .012, gy + .012)
    # gold glazing bars on both faces of the glass: the middle mullion to the crown, transoms at the sheet's 565 and
    # 420 px
    bw, bd = .075, .055
    gw = iw - 2 * sash
    gz0, gz1 = fz0 + fr + sash, spring + gw / 2
    for y0 in (gy + .012, gy - .012 - bd):
        box(m, "gold", -bw / 2, bw / 2, y0, y0 + bd, gz0, gz1 - .02)
        for zpx in (565, 420):
            z = zp(zpx)
            box(m, "gold", -gw / 2, gw / 2, y0, y0 + bd, z - bw / 2, z + bw / 2)
    # the stone sill outside, sloping out
    prism_yz(m, "stone", [(-WALL + .02, fz0 - .3), (-WALL - .32, fz0 - .3), (-WALL - .32, fz0 - .1),
                          (-WALL + .02, fz0 + .02)], -ow / 2 - .35, ow / 2 + .35)
    return [m.finish(name, col, bevel=.025)]


def panel_bay(col, W=8.0, name="BH hall panel bay"):
    """A01 cell 2: the same bay without a window - a tall gold-framed marble panel with hollow (quarter-round) corners
    in the stone."""
    sx = W / 7.46
    m = Mesh()
    L = LEVEL
    wainscot(m, -W / 2, W / 2)
    cornice(m, -W / 2, W / 2)
    box(m, "stone", -W / 2, W / 2, -.5, 0, L["rail"], L["gold_band"])
    # panel, sheet px: 150 px wide (405 .. 555 of the bay 342 .. 598), from 640 up to 325; frame 10 px; corners r 24 px
    pw = 150 * PX * sx
    z0, z1 = zp(640), zp(325)
    cr = 24 * PX
    fwid = 11 * PX

    def hollow(hw, za, zb, r, segs=8):
        pts = []
        # corner centres, going counter-clockwise starting bottom-right; arcs bulge inward
        for (cx, cz, a0) in ((hw, za, 180), (hw, zb, 270), (-hw, zb, 0), (-hw, za, 90)):
            for i in range(segs + 1):
                t = math.radians(a0 - 90 * i / segs)
                pts.append((cx + r * math.cos(t), cz + r * math.sin(t)))
        return pts

    outer = hollow(pw / 2, z0, z1, cr)
    inner = hollow(pw / 2 - fwid, z0 + fwid, z1 - fwid, cr + fwid)
    ring_xz(m, "gold", outer, inner, -.05, .12)
    # a raised bead inside the frame
    bead_o = hollow(pw / 2 - fwid * .55, z0 + fwid * .55, z1 - fwid * .55, cr + fwid * .55)
    ring_xz(m, "gold", bead_o, inner, -.05, .16)
    # the marble field inside, a little proud of the stone
    prism_xz(m, "marble", inner, -.05, .03)
    return [m.finish(name, col, bevel=.025)]


def frustum(m, slot_name, x0, x1, y1, z0, z1, grow, y0=0.0):
    """A box against the wall (y0 .. y1) whose top is `grow` wider each side (and out) than its bottom."""
    mi = m.mat(slot_name)
    bm = m.bm
    lo = [bm.verts.new(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0))]
    hi = [bm.verts.new(p) for p in ((x0 - grow, y0, z1), (x1 + grow, y0, z1), (x1 + grow, y1 + grow, z1), (x0 - grow, y1 + grow, z1))]
    for k in range(4):
        face(bm, (lo[k], lo[(k + 1) % 4], hi[(k + 1) % 4], hi[k]), mi)
    face(bm, list(reversed(lo)), mi)
    face(bm, hi, mi)


def leaf(m, slot_name, base, up, out, width, height, curl=.35, lobes=3, thick=.035, rib=.06, cols=9, rows=12):
    """An acanthus leaf: a pointed blade `width` x `height` rising from `base` along `up`, bulging toward `out`, its
    tip curling over outward and down by `curl`, lobed edges, a raised middle rib; two-sided `thick`."""
    mi = m.mat(slot_name)
    bm = m.bm
    up, out = Vector(up).normalized(), Vector(out).normalized()
    side = up.cross(out).normalized()
    base = Vector(base)
    grid = []
    for j in range(rows + 1):
        v = j / rows
        wv = width * .5 * math.sin(math.pi * min(1.0, .12 + v * .95)) ** .8
        wv *= 1 + .16 * math.sin(v * lobes * 2 * math.pi) * (v > .15)
        ct = max(0.0, (v - .55) / .45)
        rise = height * (v - curl * .5 * ct * ct)
        fwd = curl * height * ct * ct * 1.1 + .25 * width * math.sin(math.pi * v) * .5
        row = []
        for i in range(cols + 1):
            u = 2 * i / cols - 1
            p = base + up * rise + side * (u * wv) + out * (fwd + rib * (1 - abs(u)) ** 2 - .08 * width * u * u)
            row.append(p)
        grid.append(row)
    front = [[bm.verts.new(p) for p in row] for row in grid]
    back = [[bm.verts.new(p - out * thick) for p in row] for row in grid]
    for j in range(rows):
        for i in range(cols):
            face(bm, (front[j][i], front[j][i + 1], front[j + 1][i + 1], front[j + 1][i]), mi, True)
            face(bm, (back[j][i], back[j + 1][i], back[j + 1][i + 1], back[j][i + 1]), mi, True)
    for j in range(rows):
        for i in (0, cols):
            a, b = (front[j][i], front[j + 1][i]), (back[j][i], back[j + 1][i])
            face(bm, (a[0], b[0], b[1], a[1]) if i == 0 else (a[0], a[1], b[1], b[0]), mi, True)
    top = rows
    for i in range(cols):
        face(bm, (front[top][i], back[top][i], back[top][i + 1], front[top][i + 1]), mi, True)
        face(bm, (front[0][i], front[0][i + 1], back[0][i + 1], back[0][i]), mi, True)


def spiral(m, slot_name, centre, a_axis, b_axis, r0, r1, turns, tr0, tr1, start=0.0, n=60):
    """A volute's scroll: a tube winding in from radius r0 to r1 over `turns` in the plane of a_axis, b_axis."""
    a_axis, b_axis = Vector(a_axis).normalized(), Vector(b_axis).normalized()
    pts, rs = [], []
    for i in range(n + 1):
        t = i / n
        ang = start + 2 * math.pi * turns * t
        r = r0 + (r1 - r0) * t
        pts.append(Vector(centre) + r * (math.cos(ang) * a_axis + math.sin(ang) * b_axis))
        rs.append(tr0 + (tr1 - tr0) * t)
    tube(m, slot_name, pts, tr0, sides=12, taper=lambda i: rs[i])


def blade(m, slot_name, pts, widths, out, thick=.035, sides=8, side=None):
    """A carved finger: a lens-sectioned ribbon along pts (Vectors), widths[i] across, `thick` through, its flat
    facing `out` (a Vector, or a function of the point index)."""
    mi = m.mat(slot_name)
    bm = m.bm
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        o = out(i) if callable(out) else Vector(out)
        s = Vector(side) if side is not None else d.cross(o).normalized()
        n = s.cross(d).normalized()
        w = max(widths[i], .004) * .5
        rings.append([bm.verts.new(p + s * w * math.cos(2 * math.pi * k / sides) +
                                   n * thick * .5 * math.sin(2 * math.pi * k / sides)) for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            face(bm, (a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]), mi, True)
    face(bm, list(reversed(rings[0])), mi, True)
    face(bm, rings[-1], mi, True)


def acanthus(m, slot_name, base, up, out, width, height, fingers=5, spread=70.0, curl=.32, width_f=None, bulge=.16):
    """An acanthus leaf as the sheet carves it: `fingers` narrow lobes fanned `spread` degrees from a common base,
    the middle tallest, each swelling out from the face and curling over outward at its tip. A short boss at the base."""
    up, out = Vector(up).normalized(), Vector(out).normalized()
    side = up.cross(out).normalized()
    fw = width_f if width_f is not None else width * 2.3 / fingers
    for f in range(fingers):
        c = (f - (fingers - 1) / 2) / max(1, (fingers - 1) / 2)          # -1 .. 1
        ang = math.radians(spread * .5 * c)
        L = height * (1 - .28 * abs(c) ** 1.5)
        dirv = up * math.cos(ang) + side * math.sin(ang)
        pts, ws = [], []
        n = 14
        for i in range(n + 1):
            t = i / n
            tip = max(0.0, (t - .55) / .45)
            p = base + dirv * L * (t - curl * .55 * tip ** 2) + out * (L * (bulge * math.sin(math.pi * t * .9) + curl * .9 * tip ** 2)
                                                                       + .02)
            pts.append(p)
            ws.append(fw * (1 - t * .92) ** .4 * (.55 + .75 * math.sin(math.pi * min(1, t * 1.25))))
        blade(m, slot_name, pts, ws, out, thick=fw * .45)


def broad_leaf(m, slot_name, base, up, out, width, height, curl=.45, lean=0.0, bulge=.18, lobes=3, thick=.07, sides=14):
    """An acanthus leaf as the sheet carves it: one broad blade with scalloped edges and a raised middle rib, swelling
    out of the bell, its top curling over outward (and to `lean` -1 / 1 sideways)."""
    up, out = Vector(up).normalized(), Vector(out).normalized()
    side = up.cross(out).normalized()
    # The spine: up the bell (swelling out by `bulge`) for the lower 58 %, then rolling over outward on an arc of
    # `curl` x 230 degrees, so the top is a curled lip that shows its underside - the sheet's leaves' tips.
    pts, ws = [], []
    L1 = height * .58
    rc = height * .2
    turn = math.radians(230 * curl)
    n1, n2 = 12, 16
    for i in range(n1 + 1):
        t = i / n1
        pts.append(Vector(base) + up * L1 * t + out * height * bulge * math.sin(math.pi * t * .5))
    p1 = pts[-1]
    c = p1 + out * rc
    for i in range(1, n2 + 1):
        a = turn * i / n2
        pts.append(c - out * rc * math.cos(a) + up * rc * math.sin(a) + side * lean * rc * .5 * (i / n2) ** 2)
    n = len(pts) - 1
    for i in range(n + 1):
        t = i / n
        w = width * (.35 + .65 * math.sin(math.pi * min(1.0, .5 * t / .45))) if t < .45 else \
            width * (1 - ((t - .45) / .55) ** 1.6 * .82)
        w *= .78 + .22 * abs(math.cos(math.pi * lobes * t))          # the lobes
        ws.append(w)
    blade(m, slot_name, pts, ws, out, thick=thick, sides=sides, side=side)
    # the middle rib, standing proud of the blade's face, up to where it rolls over
    rib = [p + out * thick * .45 for p in pts[1:n1 + 4]]
    tube(m, slot_name, rib, width * .05, sides=8, taper=lambda i: width * (.065 - .03 * i / max(1, len(rib) - 1)))


def pilaster(col, F=2.4, D=1.2, H=STOREY, name="BH hall pilaster"):
    """A01 cell 3: a walnut pilaster - gold plinth block, walnut base step, a shaft with a sunk panel and three gold
    reeds on its face (sunk panels on its sides), a gold necking, a gold Corinthian capital (a row of acanthus, the
    middle palmette and bud, corner volutes on rolled bolsters), a gold fillet and the walnut abacus in two steps.
    F = the shaft's face, D = how far it stands out of the wall. Sheet px (y from its top), z = (782 - px) * PX."""
    m = Mesh()
    hf = F / 2
    z = {k: zp(v) for k, v in (("block", 737), ("torus", 727), ("step", 712), ("neck", 367), ("cap", 350),
                               ("fillet", 285), ("abacus", 277), ("slab", 256))}
    # base: gold block, a gold moulding, the walnut step
    bw, bd = hf * 1.3, D * 1.3
    box(m, "gold", -bw, bw, 0, bd, 0, z["block"])
    prism_yz(m, "gold", [(0, z["block"]), (D * 1.24, z["block"]), (D * 1.28, z["block"] + .1), (D * 1.24, z["torus"] - .05),
                         (D * 1.16, z["torus"]), (0, z["torus"])], -hf * 1.24, hf * 1.24)
    for sgn in (-1, 1):   # the moulding's returns on the sides
        prism_xz(m, "gold", [(sgn * hf * 1.16, z["block"]), (sgn * hf * 1.28, z["block"]), (sgn * hf * 1.28, z["torus"] - .08),
                             (sgn * hf * 1.16, z["torus"])], 0, D * 1.16)
    box(m, "walnut", -hf * 1.12, hf * 1.12, 0, D * 1.12, z["torus"], z["step"] - .1)
    frustum(m, "walnut", -hf * 1.12, hf * 1.12, D * 1.12, z["step"] - .1, z["step"], -.08 * hf)
    # The shaft (the user, 11-25: "여기 나온게 아니라 들어간 부분이야 이 부분 더 디테일 하게"): a walnut frame -
    # stiles and rails standing D out - round a panel sunk `rec` into it, a round bead in the sunk corner; in the
    # panel's floor three flutes (concave channels) between flat fillets, a gold reed lying in each flute. The sides
    # are sunk panels too (no reeds).
    st, rec, sd = .19 * F, .2, .12                    # stile width, the front panel's depth, the side panels' depth
    pz0, pz1 = z["step"] + .55, z["neck"] - .5
    px0, px1 = -hf + st, hf - st
    fl = D - rec                                      # the front panel's floor
    sy0, sy1 = .2 * D, fl - .1                        # the side panels' extent out of the wall
    zs, zn = z["step"], z["neck"]
    box(m, "walnut", -hf + sd, hf - sd, 0, fl, zs, zn)                                   # the core
    for sgn in (-1, 1):
        xo, xi = sgn * hf, sgn * (hf - sd)
        a, b = min(xo, xi), max(xo, xi)
        box(m, "walnut", a, b, 0, sy0, zs, zn)                                           # side stiles
        box(m, "walnut", a, b, sy1, fl, zs, zn)
        box(m, "walnut", a, b, sy0, sy1, zs, pz0)                                        # side rails
        box(m, "walnut", a, b, sy0, sy1, pz1, zn)
        sxs, sxe = sgn * hf, sgn * (hf - st)                                             # front stiles
        box(m, "walnut", min(sxs, sxe), max(sxs, sxe), fl, D, zs, zn)
    box(m, "walnut", px0, px1, fl, D, zs, pz0)                                           # front rails
    box(m, "walnut", px0, px1, fl, D, pz1, zn)
    cove = .055

    def bead_round(p0, p1):
        tube(m, "walnut", [p0, p1], cove, sides=10, cap=True)

    # beads in the sunk corners of the front panel and of each side panel; a small raised bead round the front
    # opening's lip
    for (xa, za), (xb, zb) in ((((px0, pz0), (px1, pz0))), (((px0, pz1), (px1, pz1))), (((px0, pz0), (px0, pz1))),
                               (((px1, pz0), (px1, pz1)))):
        dx = cove if xa == xb and xa == px0 else -cove if xa == xb else 0
        dz = cove if za == zb and za == pz0 else -cove if za == zb else 0
        bead_round(Vector((xa + dx, fl + cove, za + dz)), Vector((xb + dx, fl + cove, zb + dz)))
        bead_round(Vector((xa - dx * .9, D + .01, za - dz * .9)), Vector((xb - dx * .9, D + .01, zb - dz * .9)))
    for sgn in (-1, 1):
        x = sgn * (hf - sd + cove)
        for (ya, za), (yb, zb) in ((((sy0, pz0), (sy1, pz0))), (((sy0, pz1), (sy1, pz1))), (((sy0, pz0), (sy0, pz1))),
                                   (((sy1, pz0), (sy1, pz1)))):
            dy = cove if ya == yb and ya == sy0 else -cove if ya == yb else 0
            dz = cove if za == zb and za == pz0 else -cove if za == zb else 0
            bead_round(Vector((x, ya + dy, za + dz)), Vector((x, yb + dy, zb + dz)))
    # the flutes and fillets on the panel's floor
    ix0, ix1 = px0 + 2 * cove, px1 - 2 * cove
    fw = (ix1 - ix0) / 4.8                              # three flutes, four fillets of 0.45 of a flute
    ft = .45 * fw
    fd = .09                                            # the fillets stand fd off the floor; a flute dips to it
    fz0, fz1 = pz0 + 2 * cove, pz1 - 2 * cove
    x = ix0
    mi = m.mat("walnut")
    for k in range(4):
        box(m, "walnut", x, x + ft, fl, fl + fd, fz0, fz1)
        x += ft
        if k == 3:
            break
        # the flute: a half pipe from fillet to fillet
        n = 12
        bm = m.bm
        lo, hi = [], []
        for i in range(n + 1):
            u = i / n
            yy = fl + fd - fd * .95 * math.sin(math.pi * u)
            lo.append(bm.verts.new((x + fw * u, yy, fz0)))
            hi.append(bm.verts.new((x + fw * u, yy, fz1)))
        made = []
        for i in range(n):
            made.append((face(bm, (lo[i], lo[i + 1], hi[i + 1], hi[i]), mi, True), Vector((0, 1, 0))))
        # the flute's ends, closed flat at the rails' beads
        for row, zz, want in ((lo, fz0, Vector((0, 0, 1))), (hi, fz1, Vector((0, 0, -1)))):
            c = bm.verts.new((x + fw / 2, fl + fd, zz))
            for i in range(n):
                made.append((face(bm, (row[i], c, row[i + 1]), mi, True), want))
        for f, want in made:
            if f is not None:
                f.normal_update()
                if f.normal.dot(want) < 0:
                    f.normal_flip()
        # the gold reed lying in the flute, its ends rounded
        rr = fw * .3
        cx, cy = x + fw / 2, fl + fd * .05 + rr
        tube(m, "gold", [Vector((cx, cy, fz0 + rr * 1.2)), Vector((cx, cy, fz1 - rr * 1.2))], rr, sides=16, cap=False)
        ball(m, "gold", Vector((cx, cy, fz0 + rr * 1.2)), rr, 16, 10)
        ball(m, "gold", Vector((cx, cy, fz1 - rr * 1.2)), rr, 16, 10)
        x += fw
    # gold necking: a rounded band a little wider than the shaft
    nk = z["cap"] - z["neck"]
    prism_yz(m, "gold", [(0, z["neck"]), (D + .1, z["neck"]), (D + .16, z["neck"] + nk * .3), (D + .16, z["neck"] + nk * .7),
                         (D + .1, z["cap"]), (0, z["cap"])], -hf - .1, hf + .1)
    for sgn in (-1, 1):
        prism_xz(m, "gold", [(sgn * (hf + .1), z["neck"] + .02), (sgn * (hf + .16), z["neck"] + nk * .3),
                             (sgn * (hf + .16), z["neck"] + nk * .7), (sgn * (hf + .1), z["cap"] - .02)], 0, D + .1)
    # the capital's bell (gold), flaring to the fillet
    frustum(m, "gold", -hf * .96, hf * .96, D * .96, z["cap"], z["fillet"], .03 * F)
    # gold fillet, walnut abacus (a band, then the wider top slab with a chamfered top edge)
    box(m, "gold", -hf * 1.36, hf * 1.36, 0, D + .3, z["fillet"], z["abacus"])
    box(m, "walnut", -hf * 1.4, hf * 1.4, 0, D + .36, z["abacus"], z["slab"])
    prism_yz(m, "walnut", [(0, z["slab"]), (D + .5, z["slab"]), (D + .56, z["slab"] + .1), (D + .56, H - .16),
                           (D + .44, H), (0, H)], -hf * 1.5, hf * 1.5)
    shell = m.finish(name, col, bevel=.03)
    # ornament (gold, its own object: thin carved leaves and scrolls, smooth-shaded, no bevel)
    o = Mesh()
    zc0, zc1 = z["cap"], z["fillet"]
    ch = zc1 - zc0
    fy = D * .96 + .02                              # just proud of the bell's face at its foot
    bell_out = lambda t: fy + .03 * F * t           # the bell's face going up
    # the lower row: acanthus leaves (five fingers fanned, tips curling out and over) - three across the face, the
    # middle one behind the bud, and two on each side
    # (the user, 11-25: "벽 기둥이 좀 더 입체감 있게"): the lower row stands well out of the bell and swells forward,
    # a taller second row rises behind it toward the volutes
    # The sheet's layout: four broad leaves across the face, each curling over outward away from the middle; the
    # middle palmette - a tall leaf with a fan of lobes - over the bud; a leaf on each side face's front and back.
    for x, lean in ((-.78, -1), (-.36, -.6), (.36, .6), (.78, 1)):
        broad_leaf(o, "gold", Vector((x * hf, bell_out(0) + .03, zc0 + .02)), (0, 0, 1), (0, 1, 0), .3 * F, ch * .64,
                   curl=.85, lean=lean, bulge=.16, thick=.11)
    for sgn in (-1, 1):
        for y, lean in ((.3 * D, -1), (.74 * D, 1)):
            broad_leaf(o, "gold", Vector((sgn * (hf * .96 + .03), y, zc0 + .02)), (0, 0, 1), (sgn, 0, 0), .32 * D,
                       ch * .58, curl=.7, lean=lean * sgn * .5, bulge=.1, thick=.1)
    broad_leaf(o, "gold", Vector((0, bell_out(.25) + .03, zc0 + ch * .22)), (0, 0, 1), (0, 1, 0), .3 * F, ch * .9,
               curl=.45, bulge=.12, lobes=4, thick=.11)
    acanthus(o, "gold", Vector((0, bell_out(.3) + .05, zc0 + ch * .32)), (0, 0, 1), (0, 1, 0), .42 * F, ch * .5,
             fingers=4, spread=150, curl=.3, width_f=.1 * F, bulge=.2)
    ball(o, "gold", Vector((0, bell_out(.15) + .1, zc0 + ch * .26)), .1 * F, 20, 14, scale=(1, .8, 1.3))
    # the corner volutes: a rolled bolster running back to the wall and the scroll on its face, its stem from the middle
    vr = ch * .3
    vz = zc1 - vr * .95
    for sgn in (-1, 1):
        vx = sgn * (hf * 1.02 + .06 * F)
        tube(o, "gold", [Vector((vx, .05, vz)), Vector((vx, bell_out(.8) + .16, vz))], vr * .95, sides=24)
        spiral(o, "gold", Vector((vx, bell_out(.8) + .26, vz)), (sgn, 0, 0), (0, 0, 1), vr * .88, vr * .1, 1.6, vr * .17,
               vr * .07, start=math.radians(-90 if sgn > 0 else -90))
        ball(o, "gold", Vector((vx, bell_out(.8) + .26, vz)), vr * .18, 12, 8)
        # the stem (caulicolus): from behind the palmette, out and up into the scroll's outer turn
        pts = []
        for i in range(13):
            t = i / 12
            pts.append(Vector((sgn * (.1 * F + (abs(vx) - .1 * F) * t ** 1.4), bell_out(.6) + .06 + .2 * t,
                               zc0 + ch * .38 + (vz - vr * .88 - zc0 - ch * .38) * t + ch * .12 * math.sin(math.pi * t))))
        tube(o, "gold", pts, vr * .14, sides=10)
    orn = o.finish(name + " capital", col, bevel=0)
    return [shell, orn]


def arc_poly(cz, r0, r1, a0, a1, segs, cx=0.0):
    """The outline (x, z) of a ring's segment round (cx, cz) between radii r0 < r1, angles a0 .. a1 (radians)."""
    outer = [(cx + r1 * math.cos(a0 + (a1 - a0) * i / segs), cz + r1 * math.sin(a0 + (a1 - a0) * i / segs)) for i in range(segs + 1)]
    inner = [(cx + r0 * math.cos(a0 + (a1 - a0) * i / segs), cz + r0 * math.sin(a0 + (a1 - a0) * i / segs)) for i in range(segs + 1)]
    return outer + inner[::-1]


def transom_tympanum(m, R, lintel, spring):
    """The doorway's fill where the opening ends: a walnut transom with gold edges at `lintel` (the builder's
    lintel wall starts there) and a walnut tympanum with a gold sunburst and rim filling the arch (radius R,
    springing at `spring`) over it."""
    # the transom where the opening ends (the builder's lintel), and the tympanum over it
    box(m, "gold", -R, R, -.35, .3, lintel - .12, lintel)
    box(m, "walnut", -R, R, -.4, .25, lintel, lintel + .75)
    box(m, "gold", -R, R, -.35, .3, lintel + .75, lintel + .85)
    t0 = lintel + .85
    tym = [(-R, t0), (R, t0), (R, spring)] + [(R * math.cos(math.pi * i / 40), spring + R * math.sin(math.pi * i / 40))
                                              for i in range(1, 40)] + [(-R, spring)]
    prism_xz(m, "walnut", tym, -.5, -.1)
    # the sunburst: a gold half disc on the transom and rays out to a gold rim inside the arch
    cz = t0
    prism_xz(m, "gold", arc_poly(cz, 0.0001, 1.1, 0, math.pi, 24), -.1, .05)
    prism_xz(m, "gold", arc_poly(cz, 1.1, 1.3, 0, math.pi, 24), -.1, .1)
    rim = R - .35

    def reach(c, s_):
        """How far a ray from (0, cz) at (c, s_) goes before the rim: the straight sides under the springing, else
        the rim's arc over it."""
        if abs(c) > 1e-4:
            side = rim / abs(c)
            if cz + s_ * side <= spring:
                return side
        dz = cz - spring
        return -dz * s_ + math.sqrt(max(0.0, (dz * s_) ** 2 - (dz * dz - rim * rim)))

    rays = 15
    for k in range(1, rays):
        a = math.pi * k / rays
        c, s_ = math.cos(a), math.sin(a)
        L = reach(c, s_)
        r0 = 1.35
        hw = .07 + .05 * (k % 2)
        pts = [(c * r0 - s_ * hw, cz + s_ * r0 + c * hw), (c * r0 + s_ * hw, cz + s_ * r0 - c * hw),
               (c * L + s_ * hw * 1.8, cz + s_ * L - c * hw * 1.8), (c * L - s_ * hw * 1.8, cz + s_ * L + c * hw * 1.8)]
        prism_xz(m, "gold", pts, -.1, -.02 + .04 * (k % 2))
    # concentric gold rings across the rays where they fit inside the rim (a fanlight's bars)
    for rr in (rim * .45, rim * .8):
        n = 48
        ring = []
        for i in range(n + 1):
            a = math.pi * i / n
            c, s_ = math.cos(a), math.sin(a)
            r = min(rr, reach(c, s_) - .05)
            ring.append(Vector((c * r, -.02, cz + s_ * r)))
        tube(m, "gold", ring, .07, sides=10)
    # the tympanum's gold rim: a bead following the arch and the transom's top
    rimpts = [Vector((-R + .2, -.05, t0 + .05)), Vector((-R + .2, -.05, spring))] + \
        [Vector(((R - .2) * math.cos(math.pi - math.pi * i / 40), -.05, spring + (R - .2) * math.sin(math.pi - math.pi * i / 40)))
         for i in range(1, 40)] + [Vector((R - .2, -.05, spring)), Vector((R - .2, -.05, t0 + .05))]
    tube(m, "gold", rimpts, .07, sides=10)


def arch_doorway(col, W=12.0, sill=0.0, lintel=7.0, spring=10.0, name="BH hall arch doorway", rw=1.3, rs=1.5):
    """A01 cell 4: the great arched doorway - marble piers of three courses on a marble plinth between gold bands,
    a walnut lining on their inner edge, gold band and walnut impost, a walnut archivolt in two steps with a gold
    bead inside, a ring of marble voussoirs and a gold keystone. Sized for the hall's doorways (the builder: south
    x -6 .. 6, open to 7 m, the lintel wall over it; the loft at 16 .. 20 in front of the south wall): the opening W,
    a walnut transom at `lintel` (where the opening's collider ends) and a walnut tympanum with a gold sunburst
    filling the arch over it. The sheet's arch is taller for its width (spring 1.25 x the opening); here the arch and
    its keystone stay under the loft's floor (19.4 m, against the south wall from x -31 to 4.6)."""
    m = Mesh()
    R = W / 2
    back, face_y = -1.0, .6                 # through the 1 m wall, standing 0.6 m out of its face
    pier = 3.0                              # pier face width (rw: walnut archivolt, rs: marble voussoir ring)
    base, imp = sill + 1.5, spring - 1.15   # plinth top, impost band foot
    for sgn in (-1, 1):
        x0, x1 = sgn * R, sgn * (R + pier)
        a, b = min(x0, x1), max(x0, x1)
        # plinth: gold foot band, marble block, gold moulding on it
        box(m, "gold", a - .18, b + .18, back, face_y + .2, sill, sill + .22)
        box(m, "marble", a - .12, b + .12, back, face_y + .14, sill + .22, base)
        prism_yz(m, "gold", [(back, base), (face_y + .16, base), (face_y + .2, base + .1), (face_y + .16, base + .3),
                             (face_y + .06, base + .34), (back, base + .34)], a - .1, b + .1)
        # three courses of marble blocks, a joint between them
        z0 = base + .34
        hgt = (imp - z0) / 3
        for k in range(3):
            box(m, "marble", a + (.6 if sgn > 0 else 0), b - (.6 if sgn < 0 else 0), back, face_y,
                z0 + k * hgt + .02, z0 + (k + 1) * hgt - .02)
        # the walnut lining on the pier's inner edge and its reveal through the wall
        li0, li1 = sgn * R, sgn * (R + .6)
        box(m, "walnut", min(li0, li1), max(li0, li1), back, face_y + .06, base + .34, imp)
        box(m, "walnut", min(li0, li1) + (.08 if sgn < 0 else 0), max(li0, li1) - (0 if sgn < 0 else .08),
            face_y + .06, face_y + .1, base + .6, imp - .25)                   # a raised strip on it
        # impost: gold band, walnut block in two steps
        prism_yz(m, "gold", [(back, imp), (face_y + .14, imp), (face_y + .18, imp + .12), (face_y + .14, imp + .3),
                             (back, imp + .3)], a - .08, b + .08)
        box(m, "walnut", a - .22, b + .22, back, face_y + .3, imp + .3, imp + .62)
        box(m, "walnut", a - .3, b + .3, back, face_y + .38, imp + .62, spring)
    # the arch over the impost: walnut archivolt in two steps, the soffit through the wall, a gold bead inside
    segs = 40
    prism_xz(m, "walnut", arc_poly(spring, R, R + rw * .55, 0, math.pi, segs), back, face_y + .02)
    prism_xz(m, "walnut", arc_poly(spring, R + rw * .5, R + rw, 0, math.pi, segs), back, face_y + .14)
    bead = [Vector(((R + .12) * math.cos(math.pi * i / 48), face_y + .06, spring + (R + .12) * math.sin(math.pi * i / 48)))
            for i in range(49)]
    tube(m, "gold", bead, .1, sides=12)
    # the marble voussoirs (the middle one is the keystone's place)
    nv = 15
    gap = .012
    for k in range(nv):
        a0 = math.pi * k / nv + gap
        a1 = math.pi * (k + 1) / nv - gap
        if k == nv // 2:
            continue
        prism_xz(m, "marble", arc_poly(spring, R + rw, R + rw + rs, a0, a1, 4), back, face_y + .08)
    # the gold keystone: wider at its top, standing proud
    kh0, kh1 = spring + R + rw * .4, spring + R + rw + rs + .3
    hw0, hw1 = .65, .95
    prism_xz(m, "gold", [(-hw0, kh0), (hw0, kh0), (hw1, kh1), (-hw1, kh1)], back, face_y + .35)
    prism_xz(m, "gold", [(-hw1 - .08, kh1 - .05), (hw1 + .08, kh1 - .05), (hw1 + .08, kh1 + .12), (-hw1 - .08, kh1 + .12)],
             back, face_y + .42)
    transom_tympanum(m, R, lintel, spring)
    return [m.finish(name, col, bevel=.03)]


def sconce(col, name="BH hall wall sconce"):
    """A01 cell 5: the gold wall sconce - a backplate with notched corners and a raised panel, a turned stem in front
    of it (drop finial, collars, a big ball, a baluster up to the middle cup), two C arms from small scrolls by the
    stem to the side cups. 1.8 m tall. The cups are empty as the sheet draws them; the candles (wax, a black wick -
    PLAN: candles as wax and wick only, the flame is Unity's) are their own object, "(lit)", shown in the candle-light
    still only. Measured on the sheet at 3x (crop 705 x 810 px, the sconce 740 px tall): u metres a crop pixel,
    x from the middle (352), z up from the finial's tip (770)."""
    u = 1.8 / 740.0
    m = Mesh()

    def plate(hw, z0, z1, n):
        return [(-hw + n, z0), (hw - n, z0), (hw - n, z0 + n * .5), (hw, z0 + n), (hw, z1 - n), (hw - n, z1 - n * .5),
                (hw - n, z1), (-hw + n, z1), (-hw + n, z1 - n * .5), (-hw, z1 - n), (-hw, z0 + n), (-hw + n, z0 + n * .5)]

    # backplate: x 225 .. 475, y 295 .. 690; notched corners; a raised panel inside, a step between
    hw = 125 * u
    z0, z1 = (770 - 690) * u, (770 - 295) * u
    prism_xz(m, "gold", plate(hw, z0, z1, 26 * u), 0, .03)
    prism_xz(m, "gold", plate(hw - 12 * u, z0 + 12 * u, z1 - 12 * u, 22 * u), .03, .05)
    prism_xz(m, "gold", plate(hw - 24 * u, z0 + 24 * u, z1 - 24 * u, 18 * u), .05, .065)
    sy = .05 + 62 * u                  # the stem's axis out of the wall (its big ball just clear of the plate)
    # the stem, bottom up (radius px, z px from the tip)
    prof = [(0, 0), (12, 6), (30, 22), (44, 48), (45, 62), (36, 82), (20, 96), (16, 102), (30, 108), (34, 116), (30, 124),
            (18, 130), (24, 140), (40, 150), (40, 162), (24, 170), (30, 190), (52, 212), (60, 250), (56, 285), (40, 308),
            (24, 318), (30, 326), (36, 334), (30, 344), (20, 350), (24, 380), (32, 420), (28, 470), (22, 540), (20, 600),
            (26, 606), (30, 616), (26, 626), (16, 634), (20, 640), (36, 648), (56, 660), (70, 676), (78, 694), (80, 708),
            (94, 712), (98, 722), (92, 730), (78, 726), (68, 708), (40, 690), (0, 686)]
    lathe(m, "gold", [(r * u, z * u) for r, z in prof], at=(0, sy, 0), segs=32)
    # the side cups on their collars (at crop x 130 / 575 -> 222 px out; foot at 468 px up)
    cup = [(0, 0), (18, 0), (24, 6), (30, 12), (24, 18), (20, 24), (36, 32), (56, 44), (70, 60), (78, 78), (80, 92),
           (94, 96), (98, 106), (92, 114), (78, 110), (68, 92), (40, 74), (0, 70)]
    for sgn in (-1, 1):
        cx = sgn * 222 * u
        lathe(m, "gold", [(r * u, (z + 468) * u) for r, z in cup], at=(cx, sy, 0), segs=28)
        # the arm: from a small scroll by the stem (its eye at x 85, z 360 px), out and down round a C, up to the cup
        pts = []
        for i in range(13):
            a = math.radians(-90 - 300 * i / 12)
            r = 35 - 20 * i / 12
            pts.append((85 + r * math.cos(a), 360 + r * math.sin(a)))
        pts.reverse()
        for i in range(1, 15):
            t = i / 14
            pts.append((85 + 137 * (1 - math.cos(math.pi * t)) / 2 + 20 * math.sin(math.pi * t),
                        325 - 50 * math.sin(math.pi * t * .9) + (468 - 325 + 20) * t ** 2.2))
        path = [Vector((sgn * x * u, sy, z * u)) for x, z in pts]
        tube(m, "gold", path, 15 * u, sides=14, taper=lambda i: (9 + 7 * min(1, i / 10)) * u)
        lathe(m, "gold", [(0, 0), (20 * u, 0), (24 * u, 8 * u), (20 * u, 16 * u), (0, 16 * u)], at=(cx, sy, 458 * u), segs=20)
        tube(m, "gold", [Vector((sgn * 20 * u, sy, 330 * u)), Vector((sgn * 60 * u, sy, 345 * u))], 14 * u, sides=12)
    out = [m.finish(name, col, bevel=.006)]
    # candles in the cups (their own object): wax bodies, a black wick
    c = Mesh()
    for cx, zf in ((0, 686), (-222, 468 + 70), (222, 468 + 70)):
        h = .32
        lathe(c, "wax", [(0, 0), (.042, 0), (.042, h - .02), (.036, h), (0, h - .01)], at=(cx * u, sy, zf * u), segs=20)
        tube(c, "piece_black", [Vector((cx * u, sy, zf * u + h - .015)), Vector((cx * u, sy, zf * u + h + .025))], .005,
             sides=6)
    out.append(c.finish(name + " candles (lit)", col, bevel=0))
    return out


COFFER = 8.0          # the ceiling's coffers: 8 m squares, 8 x 9 over the hall's 64 x 72 m


def coffer(m, S=COFFER, full=True, at=(0.0, 0.0)):
    """A02 cell 1: one coffer of the ceiling, seen from below. The ceiling's underside is z 0 (the hall's 34 m);
    everything hangs below it. Two-strip walnut beams round it (the outer strip deeper), stepped walnut blocks at
    its corners, inside them a gold frame in two steps, a walnut step, and the cream marble panel sunk deepest.
    full: the beams and blocks whole (the sheet's single coffer); else halved / quartered at the square's edges so
    the coffers tile (a neighbour has the other half)."""
    cx, cy = at
    h = S / 2
    bo, bi = 1.0, .55                   # the beam's outer strip and inner strip (a groove between)
    bw = bo + bi
    # what of [x0, x1] lies in this square (full: everything; tiled: clipped to the square)
    lo, hi = (-h - bw, h + bw) if full else (-h, h)

    def clip_box(sl, x0, x1, y0, y1, z0, z1):
        x0, x1 = max(x0, lo), min(x1, hi)
        y0, y1 = max(y0, lo), min(y1, hi)
        if x1 - x0 > 1e-4 and y1 - y0 > 1e-4:
            box(m, sl, cx + x0, cx + x1, cy + y0, cy + y1, z0, z1)

    # the ceiling's back (marble, the panel's face) over the whole square
    clip_box("marble", -h, h, -h, h, -.02, 0)
    # beams along the four edges (each centred on the edge): the outer strip at the edge deeper, the inner one
    # toward the panel a step shallower, a fillet between them
    for side in range(4):
        def run(a0, a1, b0, b1, z0, z1, side=side):
            # a = across the edge (inward positive from the edge line), b = along it
            if side == 0:
                clip_box("walnut", b0, b1, -h + a0, -h + a1, z0, z1)
            elif side == 1:
                clip_box("walnut", b0, b1, h - a1, h - a0, z0, z1)
            elif side == 2:
                clip_box("walnut", -h + a0, -h + a1, b0, b1, z0, z1)
            else:
                clip_box("walnut", h - a1, h - a0, b0, b1, z0, z1)
        run(-bo / 2, bo / 2, -h, h, -1.35, -.02)                  # outer strip
        run(bo / 2 + .08, bo / 2 + bi, -h + bo / 2, h - bo / 2, -1.0, -.02)  # inner strip, a groove off the outer
        run(bo / 2 + bi - .1, bo / 2 + bi, -h + bo / 2 + bi, h - bo / 2 - bi, -1.05, -.9)   # a lip on its inner edge
    # stepped blocks at the corners, centred on them
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * h, sy * h
            clip_box("walnut", x - 1.0, x + 1.0, y - 1.0, y + 1.0, -1.55, -.02)
            clip_box("walnut", x - .82, x + .82, y - .82, y + .82, -1.95, -1.55)
            clip_box("walnut", x - .7, x + .7, y - .7, y + .7, -2.05, -1.95)
    # inside the beams: the gold frame (an outer flat band, an inner raised bead), a walnut step, the marble
    o = h - bo / 2 - bi                 # the opening's half width
    ring = lambda r: [(cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r), (cx - r, cy + r)]

    def frame(sl, r0, r1, z0, z1):
        """A square frame from half-width r1 in to r0, hanging from z0 down to z1 (z1 < z0): four boxes."""
        for x0, x1, y0, y1 in ((-r1, r1, -r1, -r0), (-r1, r1, r0, r1), (-r1, -r0, -r0, r0), (r0, r1, -r0, r0)):
            box(m, sl, cx + x0, cx + x1, cy + y0, cy + y1, z1, z0)

    frame("gold", o - .38, o, -.02, -.62)
    frame("gold", o - .5, o - .3, -.02, -.72)
    frame("walnut", o - .78, o - .5, -.02, -.38)
    frame("walnut", o - .86, o - .74, -.02, -.46)


def ceiling_part(col, name="BH hall ceiling coffer"):
    """The sheet's single coffer (full beams and blocks), and for the hall still a 3 x 3 patch of tiled ones
    ("(lit)": shown only there)."""
    m = Mesh()
    coffer(m, full=True)
    alone = m.finish(name + " (alone)", col, bevel=.06, merge=False)
    t = Mesh()
    for i in (-1, 0, 1):
        for j in (-1, 0, 1):
            coffer(t, full=False, at=(i * COFFER, j * COFFER))
    tiled = t.finish(name + " tiled (lit)", col, bevel=.06, merge=False)
    return [alone, tiled]


ROSE_R = 1.3          # the ceiling rose's radius (2.6 m across, read from the floor 34 m below)


def rosette(m, at=(0.0, 0.0, 0.0), R=ROSE_R):
    """A02 cell 2: the ceiling rose, hanging from the ceiling (z 0 at its back): a thick gold rim in steps and lips,
    a raised cream marble ring, a sunk field with acanthus leaves radiating (eight great, eight small between),
    a stepped gold boss in the middle and a short gold rod with a knob below it (where a chandelier's rod joins - B6)."""
    k = R / 1.3
    at = Vector(at)
    # the rim and ring, outside in, one closed turned solid (its back on the ceiling): gold spans then marble
    prof = [(0, 0), (1.3, 0), (1.3, -.1), (1.34, -.13), (1.34, -.18), (1.29, -.21), (1.24, -.21), (1.24, -.25),
            (1.19, -.29), (1.13, -.29), (1.13, -.27), (1.1, -.27), (1.07, -.33), (1.0, -.36), (.94, -.34),
            (.9, -.27), (.88, -.18), (0, -.18)]
    slots = ["gold"] * 9 + ["marble"] * 8
    lathe(m, slots, [(r * k, z * k) for r, z in prof], at=at, segs=64)
    # the leaves on the field (marble, carved), radiating from the boss
    for i in range(16):
        a = 2 * math.pi * i / 16
        great = i % 2 == 0
        radial = Vector((math.cos(a), math.sin(a), 0))
        base = at + radial * .3 * k + Vector((0, 0, -.19 * k))
        broad_leaf(m, "marble", base, radial, (0, 0, -1), (.36 if great else .22) * k, (.86 if great else .6) * k,
                   curl=(.28 if great else .22), bulge=.3, lobes=4 if great else 3, thick=.07 * k, sides=8)
    # the boss and the rod
    boss = [(0, -.17), (.42, -.17), (.42, -.25), (.38, -.29), (.32, -.29), (.3, -.34), (.26, -.38), (.2, -.39),
            (.2, -.43), (.15, -.45), (.11, -.5), (.09, -.52), (.09, -.86), (.13, -.88), (.15, -.92), (.15, -.98),
            (.1, -1.0), (.14, -1.04), (.15, -1.1), (.1, -1.14), (0, -1.15)]
    lathe(m, "gold", [(r * k, z * k) for r, z in boss], at=at, segs=32)


def rosette_part(col, name="BH hall ceiling rose"):
    """The rose alone (the sheet's), and for the hall still one tiled coffer with the rose on its panel ("(lit)")."""
    m = Mesh()
    rosette(m)
    alone = m.finish(name + " (alone)", col, bevel=.01, recalc=True)
    t = Mesh()
    coffer(t, full=False)
    tile = t.finish(name + " in a coffer (lit)", col, bevel=.06, merge=False)
    r = Mesh()
    rosette(r, at=(0, 0, -.02))
    rose2 = r.finish(name + " on the panel (lit)", col, bevel=.01, recalc=True)
    return [alone, tile, rose2]


PIECE_OUTLINES = {
    # half outlines (r, z) of the banners' gold pieces, height 1, from the foot up (R787 draws a bishop and a king)
    "bishop": [(.42, 0), (.42, .07), (.36, .09), (.36, .14), (.27, .17), (.2, .23), (.15, .32), (.13, .46), (.25, .5),
               (.25, .54), (.12, .57), (.17, .63), (.2, .7), (.19, .78), (.14, .85), (.08, .89), (.07, .91), (.09, .94),
               (.06, .99), (0, 1.0)],
    "king": [(.42, 0), (.42, .07), (.36, .09), (.36, .14), (.27, .17), (.2, .23), (.15, .32), (.13, .48), (.26, .52),
             (.26, .56), (.14, .58), (.22, .66), (.24, .74), (.2, .79), (.06, .8), (.06, .85), (.15, .85), (.15, .9),
             (.06, .9), (.06, 1.0), (0, 1.0)],
}


def banner(col, W=4.2, H=7.6, piece="bishop", name="BH hall banner"):
    """R787's wall banner (no sheet cell): white linen held still in soft vertical folds, cut to a point at the foot,
    gold bands down its sides and along the point, a long gold fringe under the point, a gold piece (a bishop or a
    king) on it; its sleeve over a gold rod on two wall brackets, ball finials, a tassel at each end, a gold rope
    swagged across its head. Hung with the rod at z H (the banner's top), the wall's face at y 0."""
    yb = .32                                    # the cloth's plane out of the wall
    V = H * .2                                  # the point's depth
    fold_a, fold_l = .07, .95

    def fold(x, z):
        """The cloth's held folds: out of its plane (y) at (x, z); deeper toward the foot."""
        t = 1 - z / H
        return fold_a * (.4 + .6 * t) * math.sin(2 * math.pi * x / fold_l + .6) + .04 * t * t

    def bottom(x):
        return V * abs(x) / (W / 2)             # the point: lowest (0) in the middle, V at the sides

    m = Mesh()
    nx, nz = 28, 30
    mi = m.mat("linen")
    bm = m.bm
    front, back = [], []
    for j in range(nz + 1):
        rf, rb = [], []
        for i in range(nx + 1):
            x = -W / 2 + W * i / nx
            z = bottom(x) + (H - bottom(x)) * j / nz
            y = yb + fold(x, z)
            rf.append(bm.verts.new((x, y, z)))
            rb.append(bm.verts.new((x, y - .025, z)))
        front.append(rf)
        back.append(rb)
    for j in range(nz):
        for i in range(nx):
            face(bm, (front[j][i], front[j][i + 1], front[j + 1][i + 1], front[j + 1][i]), mi, True)
            face(bm, (back[j][i], back[j + 1][i], back[j + 1][i + 1], back[j][i + 1]), mi, True)
    for j in range(nz):
        for i in (0, nx):
            a, b = (front[j][i], front[j + 1][i]), (back[j][i], back[j + 1][i])
            face(bm, (a[0], a[1], b[1], b[0]) if i == 0 else (a[0], b[0], b[1], a[1]), mi, True)
    for i in range(nx):
        face(bm, (front[0][i], back[0][i], back[0][i + 1], front[0][i + 1]), mi, True)

    def on_cloth(x, z, lift=.012):
        return Vector((x, yb + fold(x, z) + lift, z))

    # gold bands down the sides and along the point (on the cloth, following its folds)
    bw = .2
    for sgn in (-1, 1):
        xb = sgn * (W / 2 - bw / 2)
        pts = [on_cloth(xb, bottom(xb) + (H - .05 - bottom(xb)) * t / 20) for t in range(21)]
        blade(m, "gold", pts, [bw] * len(pts), (0, 1, 0), thick=.02, sides=6, side=(1, 0, 0))
        hem = []
        for t in range(21):
            x = sgn * (W / 2 - bw) * (1 - t / 20)
            hem.append(on_cloth(x, bottom(x) + .22))
        if sgn < 0:
            hem.reverse()
        side = Vector((1, 0, V / (W / 2) * sgn)).normalized()
        blade(m, "gold", hem, [.28] * len(hem), (0, 1, 0), thick=.02, sides=6, side=side)
    # the fringe: gold strands hanging from the point's edge
    n = int(W / .045)
    for i in range(n + 1):
        x = -W / 2 + W * i / n
        z = bottom(x)
        top = on_cloth(x, z + .04, .0)
        ln = .55 + .05 * math.sin(i * 1.7)
        box(m, "gold", x - .014, x + .014, top.y - .012, top.y + .012, z - ln, z + .04)
    # the piece, flat gold on the cloth (its outline pushed onto the folds)
    outline = PIECE_OUTLINES[piece]
    ph = H * .4
    pz = H * .3
    # filled as mirrored strips between the outline's steps (no fill to fail on the concave outline); a front face
    # and the outline's sides down to the cloth
    mi_g = m.mat("gold")
    prof = [(r * ph * .9, pz + z * ph) for r, z in outline]
    cols = 16          # each strip split across, so the flat gold follows the folds (else the cloth shows through)
    grid = [[bm.verts.new(on_cloth(-r + 2 * r * c / cols, z, .03)) for c in range(cols + 1)] for r, z in prof]
    for i in range(len(prof) - 1):
        for c in range(cols):
            f = face(bm, (grid[i][c], grid[i][c + 1], grid[i + 1][c + 1], grid[i + 1][c]), mi_g)
            if f is not None:
                f.normal_update()
                if f.normal.y < 0:
                    f.normal_flip()
    # the outline's sides down to the cloth
    for side_i in (0, cols):
        lo_v = [bm.verts.new(on_cloth(grid[i][side_i].co.x, prof[i][1], .008)) for i in range(len(prof))]
        for i in range(len(prof) - 1):
            q = (lo_v[i], lo_v[i + 1], grid[i + 1][side_i], grid[i][side_i])
            face(bm, q if side_i == cols else tuple(reversed(q)), mi_g)
    # the sleeve over the rod, the rod, finials, brackets, tassels, the rope swag
    ry = yb + .02
    rz = H + .12
    tube(m, "linen", [Vector((-W / 2, ry, rz)), Vector((W / 2, ry, rz))], .16, sides=16)
    tube(m, "gold", [Vector((-W / 2 - .55, ry, rz)), Vector((W / 2 + .55, ry, rz))], .07, sides=12)
    for sgn in (-1, 1):
        x = sgn * (W / 2 + .62)
        ball(m, "gold", Vector((x, ry, rz)), .14, 14, 10)
        ball(m, "gold", Vector((x + sgn * .14, ry, rz)), .06, 10, 6)
        # bracket: a gold arm from a round wall plate
        bx = sgn * (W / 2 + .3)
        lathe(m, "gold", [(0, 0), (.16, 0), (.16, .04), (.1, .07), (0, .08)], at=(bx, 0, rz),
              axis=Matrix.Rotation(-math.pi / 2, 4, "X").to_3x3(), segs=16)
        tube(m, "gold", [Vector((bx, .05, rz)), Vector((bx, ry - .05, rz + .05)), Vector((bx, ry, rz + .02))], .04, sides=8)
        # tassel: a cord from the rod's end, a knob, a skirt of strands
        tx = sgn * (W / 2 + .4)
        tube(m, "gold", [Vector((tx, ry + .05, rz)), Vector((tx, ry + .1, rz - .5)), Vector((tx, ry + .1, rz - 1.0))], .022,
             sides=6)
        lathe(m, "gold", [(0, 0), (.13, .02), (.16, .14), (.11, .3), (.06, .36), (.09, .42), (.05, .48), (0, .5)],
              at=(tx, ry + .1, rz - 1.5), segs=14)
        for k in range(14):
            a = 2 * math.pi * k / 14
            px, py = tx + .1 * math.cos(a), ry + .1 + .1 * math.sin(a)
            box(m, "gold", px - .015, px + .015, py - .015, py + .015, rz - 1.95, rz - 1.48)
    rope = []
    for i in range(41):
        t = i / 40
        x = -W / 2 - .45 + (W + .9) * t
        rope.append(Vector((x, ry + .2, rz - .15 - .55 * math.sin(math.pi * t))))
    tube(m, "gold", rope, .05, sides=8)
    return [m.finish(name, col, bevel=.006)]


FIRE = {"w": 4.4, "h": 4.0, "deep": 1.8, "mantel": 5.0}   # the hearth's opening, its depth into the wall, the shelf


def fireplace_bay(col, W=7.543, name="BH hall fireplace bay"):
    """The user (11-25): "아래 구간 3개 정도 ㄷ 자로 파고 거기다가 나무 화로를 넣어서 불에 타고 연기가 나는 연출을 해줘"
    (the east wall's lower bays, the user's pick). A lower bay with a hearth cut into the wall - a box open to the hall
    (ㄷ seen from above), 1.8 m deep behind the wall's face (the builder's wall collider stays at the face: nobody
    walks in) - a marble surround with gold bands and a keystone, a walnut mantel on scrolled corbels, the bay's gold
    panel shortened over it; inside, a cast-iron fireback with a gold king, two iron firedogs with gold finials, a
    stack of logs on a bed of embers, the flue going up. A marble hearth slab before it, 4 cm proud of the floor.
    The fire, its light and the smoke are Unity's (.Hall.cs, like the lobby's braziers); the stills draw a preview."""
    sx = W / 7.46
    m = Mesh()
    L = LEVEL
    ow, oh, dp, mz = FIRE["w"], FIRE["h"], FIRE["deep"], FIRE["mantel"]
    sw = .55                                  # the surround's sides
    xs = ow / 2 + sw                          # the surround's outer edge
    # the wainscot either side of the surround, the cornice, the stone field with the hearth's hole
    for a, b in ((-W / 2, -xs), (xs, W / 2)):
        wainscot(m, a, b, panels=1)
    cornice(m, -W / 2, W / 2)
    hole = [(-ow / 2, 0), (ow / 2, 0), (ow / 2, oh), (-ow / 2, oh)]
    field_with_hole(m, "stone", -W / 2, W / 2, 0, L["gold_band"], hole, y=0, reveal=0)
    box(m, "stone", -xs, xs, -.3, 0, oh, L["rail"])                 # the stone behind the surround and mantel
    # the box cut in: sides, back, roof with the flue's mouth in it
    box(m, "stone", -ow / 2 - .4, -ow / 2, -dp - .3, -.3, 0, oh)
    box(m, "stone", ow / 2, ow / 2 + .4, -dp - .3, -.3, 0, oh)
    box(m, "stone", -ow / 2 - .4, ow / 2 + .4, -dp - .6, -dp - .3, 0, oh + 2.5)
    box(m, "stone", -ow / 2, -.7, -dp - .3, -.3, oh, oh + .4)
    box(m, "stone", .7, ow / 2, -dp - .3, -.3, oh, oh + .4)
    box(m, "stone", -.7, .7, -.9, -.3, oh, oh + .4)                 # the flue's mouth: 1.4 x 0.9 m at the back
    for xa, xb in ((-1.1, -.7), (.7, 1.1)):
        box(m, "stone", xa, xb, -dp - .3, -.9, oh + .4, oh + 2.5)    # the flue's sides going up
    box(m, "stone", -1.1, 1.1, -.9, -.3, oh + .4, oh + 2.5)
    # the firebox floor (sunk 2 cm under the hearth) and its back: a cast-iron fireback with a raised gold king
    box(m, "stone", -ow / 2, ow / 2, -dp - .3, -.3, -.02, 0)
    box(m, "piece_black", -1.5, 1.5, -dp - .3, -dp - .22, 0, 2.6)
    prism_xz(m, "piece_black", [(-1.5, 2.6), (1.5, 2.6), (1.2, 3.0), (0, 3.25), (-1.2, 3.0)], -dp - .3, -dp - .22)
    king = PIECE_OUTLINES["king"]
    kp = [(r * 1.2 * .9, .55 + z * 1.9) for r, z in king]
    kp = kp + [(-x, z) for x, z in reversed(kp)]
    kp = [q for i, q in enumerate(kp) if i == 0 or (abs(q[0] - kp[i - 1][0]) > 1e-6 or abs(q[1] - kp[i - 1][1]) > 1e-6)]
    for i in range(len(king) - 1):
        (r0, z0), (r1, z1) = king[i], king[i + 1]
        if r0 > 1e-6 or r1 > 1e-6:
            prism_xz(m, "gold", [(-r0 * 1.08, .55 + z0 * 1.9), (r0 * 1.08, .55 + z0 * 1.9), (r1 * 1.08, .55 + z1 * 1.9),
                                 (-r1 * 1.08, .55 + z1 * 1.9)], -dp - .22, -dp - .19)
    # the surround: marble sides on gold bases with gold caps, the marble lintel and a gold keystone
    for sgn in (-1, 1):
        a, b = sorted((sgn * ow / 2, sgn * xs))
        box(m, "gold", a - .05, b + .05, -.3, .36, 0, .22)
        box(m, "marble", a, b, -.3, .3, .22, oh - .25)
        box(m, "gold", a - .04, b + .04, -.3, .36, oh - .25, oh - .1)
    box(m, "marble", -xs, xs, -.3, .3, oh - .1, oh + .55)
    prism_xz(m, "gold", [(-.32, oh - .05), (.32, oh - .05), (.42, oh + .6), (-.42, oh + .6)], -.3, .42)
    # the mantel: a walnut frieze with a gold band, the shelf on two scrolled corbels
    box(m, "walnut", -xs - .1, xs + .1, -.3, .34, oh + .55, mz - .1)
    box(m, "gold", -xs - .1, xs + .1, -.3, .4, oh + .55, oh + .65)
    prism_yz(m, "walnut", [(-.3, mz - .1), (.55, mz - .1), (.62, mz), (.62, mz + .2), (.56, mz + .28), (-.3, mz + .28)],
             -xs - .35, xs + .35)
    for sgn in (-1, 1):
        x = sgn * (xs + .05)
        pts = []
        for i in range(17):
            t = i / 16
            pts.append((.34 + .22 * math.sin(math.pi * t * .9), oh + .2 + (mz - .1 - oh - .2) * t))
        prism_yz(m, "walnut", [(0, oh + .2)] + pts + [(0, mz - .1)], x - .14, x + .14)
        spiral(m, "gold", Vector((x, .5, oh + .45)), (0, 1, 0), (0, 0, 1), .14, .03, 1.4, .035, .02)
    # the gold panel over the mantel (the bay's, shortened)
    pw = 150 * PX * sx
    z0, z1 = mz + 1.2, zp(325)
    cr, fwid = 24 * PX, 11 * PX

    def hollow(hw, za, zb, r, segs=8):
        pts = []
        for (cx, cz, a0) in ((hw, za, 180), (hw, zb, 270), (-hw, zb, 0), (-hw, za, 90)):
            for i in range(segs + 1):
                t = math.radians(a0 - 90 * i / segs)
                pts.append((cx + r * math.cos(t), cz + r * math.sin(t)))
        return pts

    outer = hollow(pw / 2, z0, z1, cr)
    inner = hollow(pw / 2 - fwid, z0 + fwid, z1 - fwid, cr + fwid)
    ring_xz(m, "gold", outer, inner, -.05, .12)
    ring_xz(m, "gold", hollow(pw / 2 - fwid * .55, z0 + fwid * .55, z1 - fwid * .55, cr + fwid * .55), inner, -.05, .16)
    prism_xz(m, "marble", inner, -.05, .03)
    # the hearth slab: marble, 4 cm proud, before the opening; a gold edge
    box(m, "marble", -xs - .2, xs + .2, 0, 1.0, 0, .04)
    box(m, "gold", -xs - .2, xs + .2, .98, 1.04, 0, .045)
    shell = m.finish(name, col, bevel=.02)
    # inside: two firedogs, the logs, the bed of embers (its own object: Unity may light it)
    w = Mesh()
    fy = -dp * .55 - .3
    for sgn in (-1, 1):
        x = sgn * 1.15
        box(w, "piece_black", x - .05, x + .05, fy - .55, fy + .55, .25, .35)          # the bar under the logs
        tube(w, "piece_black", [Vector((x, fy + .55, .3)), Vector((x, fy + .75, .3)), Vector((x, fy + .8, .55)),
                                Vector((x, fy + .8, .9))], .06, sides=10)
        for yy in (fy - .5, fy + .6):
            tube(w, "piece_black", [Vector((x, yy, .3)), Vector((x, yy, 0))], .04, sides=8)
        lathe(w, "gold", [(0, 0), (.09, .02), (.12, .1), (.08, .18), (.1, .24), (0, .3)], at=(x, fy + .8, .9), segs=16)
    logs = [((-.9, .9), fy - .15, .42, .17), ((-.85, .95), fy + .2, .42, .16), ((-.95, .8), fy + .02, .68, .15)]
    for (xa, xb), yy, zz, r in logs:
        tube(w, "walnut", [Vector((xa, yy, zz)), Vector((xb, yy + .05, zz + .02))], r, sides=12)
        for x in (xa, xb):
            lathe(w, "piece_black", [(0, -.01), (r * .98, -.01), (r * .98, .01), (0, .01)],
                  at=(x, yy, zz), axis=Matrix.Rotation(math.pi / 2, 3, "Y"), segs=12)
    # two logs leaning across, the crossing ones
    tube(w, "walnut", [Vector((-.5, fy - .45, .3)), Vector((.25, fy + .35, .95))], .13, sides=10)
    tube(w, "walnut", [Vector((.6, fy - .4, .3)), Vector((-.15, fy + .4, .9))], .12, sides=10)
    # embers: a low heap of black coals
    for i in range(36):
        a = i * 2.39996
        rr = .15 + .75 * ((i * .618) % 1.0)
        ball(w, "piece_black", Vector((rr * math.cos(a) * 1.4, fy + rr * math.sin(a) * .6, .06)), .09 + .04 * (i % 3), 8, 6)
    wood = w.finish(name + " logs", col, bevel=.01)
    return [shell, wood, *fire_preview(col, Vector((0, fy, .45)), name)]


def fire_preview(col, at, name):
    """The stills' fire (not exported - Unity draws it): flame tongues that glow, a glowing ember bed, smoke rising
    into the flue and a little curling out under the mantel, the fire's light. Named "(lit)": shown in the
    candle-light stills."""
    out = []
    fm = bpy.data.materials.new("preview flame")
    fm.use_nodes = True
    nt = fm.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    em = nt.nodes.new("ShaderNodeEmission")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (1.0, .62, .12, 1)
    ramp.color_ramp.elements[1].color = (.9, .12, .01, 1)
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], em.inputs["Color"])
    em.inputs["Strength"].default_value = 4.5
    nt.links.new(em.outputs[0], next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL").inputs[0])
    f = Mesh()
    import random as _r
    rnd = _r.Random(7)
    for i in range(19):
        x = -1.05 + 2.1 * i / 18 + rnd.uniform(-.08, .08)
        hgt = .6 + 1.2 * math.sin(math.pi * (i + .5) / 19) + rnd.uniform(-.3, .3)
        base = at + Vector((x, rnd.uniform(-.25, .25), 0))
        pts, ws = [], []
        for j in range(13):
            t = j / 12
            pts.append(base + Vector((.18 * math.sin(t * 5 + i) * t, .05 * math.cos(t * 4 + i), hgt * t)))
            ws.append(.2 * math.sin(math.pi * min(1, .15 + t)) ** .7 * (1 - t) ** .6)
        blade(f, "wax", pts, ws, (0, 1, 0), thick=.12, sides=8, side=(1, 0, 0))
    flames = f.finish(name + " flames (lit)", col, bevel=0, uv=False)
    flames.data.materials[0] = fm
    out.append(flames)
    # smoke: a volume over the fire, densest in the flue, thinning as it rises and a wisp under the mantel
    sm = bpy.data.materials.new("preview smoke")
    sm.use_nodes = True
    nt = sm.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (.3, .29, .28, 1)
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 2.2
    noise.inputs["Detail"].default_value = 6
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    fade = nt.nodes.new("ShaderNodeMapRange")
    fade.inputs["From Min"].default_value, fade.inputs["From Max"].default_value = 0, 1
    fade.inputs["To Min"].default_value, fade.inputs["To Max"].default_value = 1.0, .15
    nt.links.new(sep.outputs["Z"], fade.inputs["Value"])
    cut = nt.nodes.new("ShaderNodeMapRange")
    cut.inputs["From Min"].default_value, cut.inputs["From Max"].default_value = .35, .7
    nt.links.new(noise.outputs["Fac"], cut.inputs["Value"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    nt.links.new(cut.outputs["Result"], mul.inputs[0])
    nt.links.new(fade.outputs["Result"], mul.inputs[1])
    mul2 = nt.nodes.new("ShaderNodeMath")
    mul2.operation = "MULTIPLY"
    mul2.inputs[1].default_value = 14.0
    # soft edges: fade to nothing toward the volume's box (else a grey block shows)
    vs = nt.nodes.new("ShaderNodeVectorMath")
    vs.operation = "SUBTRACT"
    vs.inputs[1].default_value = (.5, .5, .5)
    nt.links.new(tc.outputs["Generated"], vs.inputs[0])
    vl = nt.nodes.new("ShaderNodeVectorMath")
    vl.operation = "LENGTH"
    nt.links.new(vs.outputs[0], vl.inputs[0])
    edge = nt.nodes.new("ShaderNodeMapRange")
    edge.inputs["From Min"].default_value, edge.inputs["From Max"].default_value = .15, .5
    edge.inputs["To Min"].default_value, edge.inputs["To Max"].default_value = 1.0, 0.0
    nt.links.new(vl.outputs["Value"], edge.inputs["Value"])
    soft = nt.nodes.new("ShaderNodeMath")
    soft.operation = "MULTIPLY"
    nt.links.new(mul.outputs[0], soft.inputs[0])
    nt.links.new(edge.outputs["Result"], soft.inputs[1])
    nt.links.new(soft.outputs[0], mul2.inputs[0])
    nt.links.new(mul2.outputs[0], vol.inputs["Density"])
    nt.links.new(vol.outputs[0], next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL").inputs["Volume"])
    for nm, (cx, cy, cz), (sx_, sy_, sz_) in (("flue", (0, at.y - .1, at.z + 3.2), (1.3, .8, 3.0)),
                                             ("wisp", (.4, at.y + 1.6, FIRE["h"] + .1), (2.2, 1.6, 1.0))):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        me = bpy.data.meshes.new("smoke " + nm)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(name + " smoke " + nm + " (lit)", me)
        ob.location = (cx, cy, cz)
        ob.scale = (sx_, sy_, sz_)
        me.materials.append(sm)
        col.objects.link(ob)
        out.append(ob)
    lt = bpy.data.lights.new(name + " fire light", "POINT")
    lt.energy, lt.color, lt.shadow_soft_size = 900, (1, .55, .2), .6
    lo = bpy.data.objects.new(name + " fire light (lit)", lt)
    lo.location = at + Vector((0, .6, .8))
    col.objects.link(lo)
    out.append(lo)
    return out


TRIPO_ARCH = os.path.join(HERE, "tripo", "bh_b7_arch_doorway.glb")
# The user's Tripo model of the sheet's doorway (11-25, "이 에셋 사용 해"; "크게, 회랑이 가려도 됨"): its opening is
# 0.4506 wide at the foot (0.2253 each side, measured), its arch's inside crown at 0.8126, the whole 0.978 tall.
TRIPO_K = 6.0 / .2253           # the opening to the builder's 12 m: 26.63 x -> 26.0 m tall, the arch's inside 21.6
TRIPO_DEPTH = 1.6               # through the 1 m wall and 0.6 m out of its face (its own 0.236 -> 1.6: y squeezed)


def arch_tripo(col, name="BH hall arch doorway (Tripo)"):
    """The south doorway from the user's Tripo arch, scaled up uniformly to the 12 m opening (the loft crosses its
    top - the user accepted that), squeezed in depth to the wall, turned to face the hall (+y), with its own
    baked textures; the transom at 7 m and the tympanum filling the arch over it are ours (the lintel wall)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=TRIPO_ARCH)
    new = [o for o in bpy.data.objects if o not in before]
    arch = next(o for o in new if o.type == "MESH")
    for o in new:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        col.objects.link(o)
    for o in new:
        if o is not arch and o.type != "MESH":
            bpy.data.objects.remove(o)
    arch.parent = None
    arch.name = name
    arch.rotation_mode = "XYZ"
    arch.rotation_euler = (0, 0, math.pi)
    arch.scale = (TRIPO_K, TRIPO_DEPTH / .2362, TRIPO_K)
    arch.location = (0, -1.0 + TRIPO_DEPTH / 2, 0)
    # the fill: the arch's inside measures 0.2367 wide each side at its springing (z 0.6) -> 6.3 m, crown 21.64
    m = Mesh()
    R = .2367 * TRIPO_K - .05
    transom_tympanum(m, R, 7.0, .8126 * TRIPO_K - R - .05)
    fill = m.finish(name + " fill", col, bevel=.03)
    return [arch, fill]


# ---------------------------------------------------------------- stills

PARTS = {
    # name: (builder, sheet, cell crop (x0, y0, x1, y1) px, R787 crop or None)
    "window_bay": (window_bay, "BH_A01_hall_walls.png", (20, 220, 310, 800), (0, 140, 300, 470)),
    "panel_bay": (panel_bay, "BH_A01_hall_walls.png", (325, 220, 615, 800), (180, 40, 520, 300)),
    "pilaster": (pilaster, "BH_A01_hall_walls.png", (625, 220, 805, 800), (440, 0, 640, 330)),
    "arch_doorway": (arch_doorway, "BH_A01_hall_walls.png", (795, 225, 1265, 800), (1150, 0, 1672, 300)),
    "arch_tripo": (arch_tripo, "BH_A01_hall_walls.png", (795, 225, 1265, 800), (1150, 0, 1672, 300)),
    "sconce": (sconce, "BH_A01_hall_walls.png", (1270, 380, 1530, 690), (500, 150, 720, 330)),
    "coffer": (ceiling_part, "BH_A02_ceiling_loft.png", (10, 385, 330, 620), None),
    "rosette": (rosette_part, "BH_A02_ceiling_loft.png", (335, 400, 585, 620), None),
    "banner": (banner, None, None, (180, 30, 530, 300)),
    "fireplace": (fireplace_bay, None, None, (40, 330, 330, 560)),
}


def camera(name, loc, look, lens):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_end = 2000
    co = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(co)
    co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return co


def _pixels(path, crop=None):
    import numpy as np
    im = bpy.data.images.load(path)
    w, h = im.size
    px = np.array(im.pixels[:], np.float32).reshape(h, w, 4)
    bpy.data.images.remove(im)
    if crop:
        x0, y0, x1, y1 = crop
        px = px[h - y1:h - y0, x0:x1]
    return px


def _fit(px, H):
    import numpy as np
    h, w = px.shape[:2]
    nw = max(1, int(w * H / h))
    return px[(np.arange(H) * h / H).astype(int)][:, (np.arange(nw) * w / nw).astype(int)]


def _light(name, kind, loc, energy, colour=(1, 1, 1), size=1.0, look=None):
    ld = bpy.data.lights.new(name, kind)
    ld.energy, ld.color = energy, colour
    if kind == "AREA":
        ld.size = size
    elif kind == "SUN":
        ld.angle = math.radians(size)
    else:
        ld.shadow_soft_size = size
    ob = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    if look is not None:
        ob.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return ob


def ceiling_stills(name, objs):
    """Stills of a ceiling part, looked up at: 0 as the sheet draws it (white, from below at 3/4), 1 the tiled patch
    from the hall floor in candle light, 2 close on a corner."""
    import numpy as np
    _, sheet, cell, crop = PARTS[name]
    alone = [o for o in objs if "(lit)" not in o.name]
    lit = [o for o in objs if "(lit)" in o.name]
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.eevee.taa_render_samples = 64
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.view_settings.view_transform = "Standard"
    out = os.path.join(STILLS, "parts")
    os.makedirs(out, exist_ok=True)
    w = bpy.data.worlds.new("part")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    H = 760
    sc.render.resolution_x, sc.render.resolution_y = int(H * 1.3), H
    shots = []
    # 0: white, from below; light from below-left
    bg.inputs["Color"].default_value = (1, 1, 1, 1)
    bg.inputs["Strength"].default_value = .9
    for o in lit:
        o.hide_render = True
    key = _light("key", "SUN", (0, 0, 0), 3.2, (1, .97, .92), 8)
    key.rotation_euler = (Vector((.8, -.45, .55) if name == "rosette" else (.4, -.5, 1)).to_track_quat("Z", "Y")).to_euler()
    d0, hall_cam, close_look, close_d = {"coffer": (24, (4, -26, -32), (3.2, 3.2, -.8), 9),
                                         "rosette": (6.5, (1.5, -6.5, -9), (0, 0, -.35), 3.2)}[name]
    look = Vector((0, 0, -.6 if name == "coffer" else -.35))
    cam = camera("sheet", look + Vector((-.55, -.75, -.75)).normalized() * d0, look, 50)
    sc.camera = cam
    for i, (cm, f) in enumerate(((cam, None),)):
        pass
    f = os.path.join(out, "%s_0.png" % name)
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    shots.append(f)
    # 1: the tiled patch from the floor 34 m below, candle light from below
    for o in alone:
        o.hide_render = True
    for o in lit:
        o.hide_render = False
    bg.inputs["Color"].default_value = (.03, .018, .01, 1)
    key.data.energy = .15
    lamps = [_light("glow %d" % i, "POINT", (x, y, -14), 9000, (1, .66, .34), 2) for i, (x, y) in
             enumerate(((-6, -4), (7, 5), (0, 9)))]
    cam = camera("hall", Vector(hall_cam), Vector((0, 2 if name == "coffer" else 0, 0)), 40)
    sc.camera = cam
    f = os.path.join(out, "%s_1.png" % name)
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    shots.append(f)
    # 2: close on a corner of the single coffer, the white light again
    for lp in lamps:
        lp.hide_render = True
    for o in lit:
        o.hide_render = True
    for o in alone:
        o.hide_render = False
    bg.inputs["Color"].default_value = (1, 1, 1, 1)
    key.data.energy = 3.2
    look = Vector(close_look)
    cam = camera("close", look + Vector((-.4, -.7, -1)).normalized() * close_d, look, 45)
    sc.camera = cam
    f = os.path.join(out, "%s_2.png" % name)
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    shots.append(f)
    row = [_fit(_pixels(os.path.join(SHEETS, sheet), cell), H)]
    for f in shots:
        row.append(_fit(_pixels(f), H))
    gap = np.ones((H, 14, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "%s_compare.png" % name)
    o.file_format = "PNG"
    o.save()
    dg = bpy.context.evaluated_depsgraph_get()
    tris = sum(sum(len(p.vertices) - 2 for p in ob.evaluated_get(dg).data.polygons) for ob in alone)
    print("PART", name, "tris (one)", tris, "->", o.filepath_raw)


CEILING_PARTS = ("coffer", "rosette")
CLOSE = {}


def part_stills(name, objs):
    import numpy as np
    if name in CEILING_PARTS:
        return ceiling_stills(name, objs)
    _, sheet, cell, crop = PARTS[name]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mid, size = (lo + hi) / 2, (hi - lo).length
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 64
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    out = os.path.join(STILLS, "parts")
    os.makedirs(out, exist_ok=True)
    # a pawn for scale (1.2 m) at the part's foot
    pm = Mesh()
    lathe(pm, "piece_white", [(0, 0), (.35, 0), (.35, .12), (.2, .25), (.14, .7), (.24, .78), (.12, .86), (.2, .95),
                              (.22, 1.02), (.18, 1.12), (.08, 1.19), (0, 1.2)], segs=24)
    pawn = pm.finish("Scale pawn 1.2 m", bpy.context.scene.collection, bevel=0, uv=False)
    pawn.location = (hi.x + .9, 1.2, 0)
    if name == "window_bay":
        # the day outside, seen through the glass (preview only - not part of the model; Unity: the sky / B8)
        sky = bpy.data.objects.new("preview sky (not exported)", bpy.data.meshes.new("sky"))
        sbm = bmesh.new()
        vs = [sbm.verts.new(q) for q in ((-3.3, -2.2, 3), (3.3, -2.2, 3), (3.3, -2.2, 15), (-3.3, -2.2, 15))]
        sbm.faces.new(vs)
        sbm.to_mesh(sky.data)
        sbm.free()
        sm = bpy.data.materials.new("preview sky")
        sm.use_nodes = True
        snt = sm.node_tree
        for n in list(snt.nodes):
            if n.type != "OUTPUT_MATERIAL":
                snt.nodes.remove(n)
        em = snt.nodes.new("ShaderNodeEmission")
        tc = snt.nodes.new("ShaderNodeTexCoord")
        sep = snt.nodes.new("ShaderNodeSeparateXYZ")
        ramp = snt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (.92, .88, .78, 1)
        ramp.color_ramp.elements[1].color = (.38, .58, .88, 1)
        mp = snt.nodes.new("ShaderNodeMapRange")
        mp.inputs["From Min"].default_value, mp.inputs["From Max"].default_value = 3, 15
        snt.links.new(tc.outputs["Object"], sep.inputs[0])
        snt.links.new(sep.outputs["Z"], mp.inputs["Value"])
        snt.links.new(mp.outputs["Result"], ramp.inputs["Fac"])
        snt.links.new(ramp.outputs["Color"], em.inputs["Color"])
        em.inputs["Strength"].default_value = 1.2
        snt.links.new(em.outputs[0], next(n for n in snt.nodes if n.type == "OUTPUT_MATERIAL").inputs[0])
        sky.data.materials.append(sm)
        sc.collection.objects.link(sky)
    w = bpy.data.worlds.new("part")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    # what the camera sees is `bg`; what metal reflects is a studio gradient (bright above, dim below), or the gold reads
    # as a flat orange (the sheet's gold has bright highlights and dark turns)
    wnt = w.node_tree
    refl = wnt.nodes.new("ShaderNodeBackground")
    tc = wnt.nodes.new("ShaderNodeTexCoord")
    sep = wnt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = wnt.nodes.new("ShaderNodeValToRGB")
    lp = wnt.nodes.new("ShaderNodeLightPath")
    mix = wnt.nodes.new("ShaderNodeMixShader")
    wnt.links.new(tc.outputs["Generated"], sep.inputs[0])
    mr = wnt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value, mr.inputs["From Max"].default_value = -.3, .9
    wnt.links.new(sep.outputs["Z"], mr.inputs["Value"])
    wnt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    wnt.links.new(ramp.outputs["Color"], refl.inputs["Color"])
    wnt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    wnt.links.new(refl.outputs[0], mix.inputs[1])
    wnt.links.new(bg.outputs[0], mix.inputs[2])
    wout = next(n for n in wnt.nodes if n.type == "OUTPUT_WORLD")
    wnt.links.new(mix.outputs[0], wout.inputs["Surface"])

    def reflect(low, high, strength):
        ramp.color_ramp.elements[0].color = (*low, 1)
        ramp.color_ramp.elements[1].color = (*high, 1)
        ramp.color_ramp.elements[0].position = .35
        refl.inputs["Strength"].default_value = strength

    reflect((.42, .38, .33), (1, .98, .95), 1.0)
    shots = []
    H = 760
    sc.render.resolution_x = int(H * max(.55, min(1.4, (hi.x - lo.x + 2) / (hi.z - lo.z + 1))))
    sc.render.resolution_y = H
    # 0: as the sheet draws it - white, soft daylight from the upper left, 3/4 from the front left a little above
    bg.inputs["Color"].default_value = (1, 1, 1, 1)
    bg.inputs["Strength"].default_value = .9
    key = _light("key", "SUN", (0, 0, 0), 3.2, (1, .97, .92), 8)
    key.rotation_euler = (math.radians(50), 0, math.radians(-35))
    d = Vector((-.42, 1, .18)).normalized()
    cam = camera("sheet", mid + d * size * 1.75, mid, 55)
    sc.camera = cam
    lit = [o for o in objs if "(lit)" in o.name]
    for o in lit:
        o.hide_render = True
    f = os.path.join(out, "%s_0.png" % name)
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    shots.append(f)
    # 1: in the hall - dim warm air, candle light from sconces either side and below
    bg.inputs["Color"].default_value = (.03, .018, .01, 1)
    bg.inputs["Strength"].default_value = 1.0
    reflect((.01, .006, .004), (.5, .32, .16), 1.0)
    key.data.energy = .25
    lamps = [_light("candle %d" % i, "POINT", (x, 2.4, z), 900, (1, .66, .34), .3)
             for i, (x, z) in enumerate(((lo.x - .6, 7.0), (hi.x + .6, 7.0)))]
    fill = _light("hall glow", "AREA", (mid.x + 4, 14, mid.z + 2), 2200, (1, .78, .5), 6, look=mid)
    if size < 4:
        # a small part: its own candles' light (where the flames will be), a faint warm fill
        for lp in lamps:
            lp.hide_render = True
        lamps = [_light("flame %d" % i, "POINT", (x, mid.y + .05, z + .02), 14, (1, .62, .3), .03)
                 for i, (x, z) in enumerate(((0, hi.z + .05), (lo.x + .25, hi.z - .5), (hi.x - .25, hi.z - .5)))]
        fill.data.energy = 60
        fill.location = mid + Vector((1.5, 4, .5))
    for o in lit:
        o.hide_render = False
    d = Vector((.25, 1, .02)).normalized()
    cam = camera("hall", mid + d * size * 1.6, mid, 50)
    sc.camera = cam
    pawn.hide_render = False
    f = os.path.join(out, "%s_1.png" % name)
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    shots.append(f)
    # 2: close, the sheet's light again - wainscot and the frame's foot
    for l in lamps + [fill]:
        l.hide_render = True
    for o in lit:
        o.hide_render = True
    bg.inputs["Color"].default_value = (1, 1, 1, 1)
    bg.inputs["Strength"].default_value = .9
    reflect((.42, .38, .33), (1, .98, .95), 1.0)
    key.data.energy = 3.2
    look = Vector((lo.x + 1.2, 0, 4.2))
    cam = camera("close", look + Vector((-.5, 1, .35)).normalized() * 7.5, look, 45)
    if name == "window_bay":
        look = Vector((0, -.4, zp(600)))
        cam = camera("close", look + Vector((-.75, 1, .3)).normalized() * 6.5, look, 40)
    if name in CLOSE:          # other zones' parts (bh_b6_*): (look at, from direction, distance, lens)
        lk, dv, dist, lens = CLOSE[name]
        look = Vector(lk)
        cam = camera("close", look + Vector(dv).normalized() * dist, look, lens)
    if name == "fireplace":
        look = Vector((0, -.6, 2.0))
        cam = camera("close", look + Vector((-.35, 1, .15)).normalized() * 7.5, look, 40)
        for o in lit:
            o.hide_render = False
        bg.inputs["Color"].default_value = (.03, .018, .01, 1)
        key.data.energy = .25
    if name == "banner":
        look = Vector((0, .4, 2.6))
        cam = camera("close", look + Vector((-.5, 1, .1)).normalized() * 6.5, look, 40)
    if name == "sconce":
        look = Vector((0, .15, .95))
        cam = camera("close", look + Vector((.55, 1, .45)).normalized() * 2.6, look, 45)
    if name in ("arch_doorway", "arch_tripo"):
        look = Vector((-7.5, .6, 10.0))
        cam = camera("close", look + Vector((-.6, 1, .25)).normalized() * 13, look, 40)
    if name == "pilaster":
        look = Vector((0, 1.1, 14.0))
        cam = camera("close", look + Vector((-.6, 1, .2)).normalized() * 7.5, look, 40)
    sc.camera = cam
    f = os.path.join(out, "%s_2.png" % name)
    sc.render.filepath = f
    bpy.ops.render.render(write_still=True)
    shots.append(f)
    if name == "pilaster":
        # 3: the shaft's sunk panel, raking light from the side so the flutes and reeds read
        key.rotation_euler = (math.radians(70), 0, math.radians(-70))
        look = Vector((0, 1.0, 7.0))
        cam = camera("panel", look + Vector((-.45, 1, .25)).normalized() * 11.0, look, 35)
        sc.camera = cam
        f = os.path.join(out, "%s_3.png" % name)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        shots.append(f)
    row = [_fit(_pixels(os.path.join(SHEETS, sheet), cell), H)] if sheet else []
    if crop:
        row.append(_fit(_pixels(R787, crop), H))
    for f in shots:
        row.append(_fit(_pixels(f), H))
    gap = np.ones((H, 14, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "%s_compare.png" % name)
    o.file_format = "PNG"
    o.save()
    dg = bpy.context.evaluated_depsgraph_get()
    tris = sum(sum(len(p.vertices) - 2 for p in ob.evaluated_get(dg).data.polygons) for ob in objs
               if ob.type == "MESH" and "(lit)" not in ob.name)
    print("PART", name, "tris", tris, "size", tuple(round(v, 2) for v in (hi - lo)), "->", o.filepath_raw)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    name = args[args.index("--part") + 1] if "--part" in args else "window_bay"
    col = bpy.data.collections.new("BH B7 " + name)
    bpy.context.scene.collection.children.link(col)
    objs = PARTS[name][0](col)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b7_hall_%s.blend" % name))
    part_stills(name, objs)
