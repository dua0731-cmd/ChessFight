# D portcullis court, the remaining parts (R505): the user's two Tripo FBX (made from AssetRefs/D_assets_04_structure_bundle
# and D_assets_02_props) split into single assets, scaled to their in-game sizes, and brought to a per-asset polygon
# budget (the user, 11-07: "각각 맞춰서 폴리곤 수 높여야 할 거 낮춰야 할 거 구분해서 해" -> "너가 블렌더에서 맞추면서 높여").
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python d_props_v1.py -- [--render] [--export]
#
# Both FBX came at ~5,000 triangles in all. Lowering is a decimate. Raising cannot invent shape, so:
#   - round or organic pieces are subdivided (Catmull-Clark, UVs kept) to smooth their silhouettes, then decimated to
#     the target: wheel, helmet bench, flower pot, lantern;
#   - what Tripo lost is rebuilt here: the pulley frame's hanging iron fork and axle (missing), the chain on the pile
#     (its links had melted into one lump; the pallet is kept), and a straight chain (missing).
# Origins: bottom centre, except the wheel (its axle centre, axle along Blender y) so the game can turn it.
import bpy, bmesh, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mg_holes   # closes Tripo's small face holes (R505)
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

STRUCTURE = "R505_Tripo_B_StylizedMedievalProps"   # pulley frame, wheel, wall, paving, post, lantern
PROPS = "R505_Tripo_A_MedievalGameProps"           # bar rack, chain pile, helmet bench, flower pot


def structure_rule(c):
    return ("PulleyFrame" if c.x < -.30 else "PulleyWheel" if c.x < -.17 else "Wall" if c.x < .072 else
            "Paving" if c.x < .264 else "GatePost" if c.x < .368 else "Lantern")


def props_rule(c):
    return "BarRack" if c.x < -.27 else "ChainPile" if c.x < -.003 else "HelmetBench" if c.x < .304 else "FlowerPot"


# Tripo built the paving tile as an upright wedge of 2 x 2 blocks (1.3 m high), not a flat 3 x 3 tile: not exported;
# the courtyard floor should be a tileable texture instead.
SKIP_EXPORT = {"Paving"}
CHAIN_LINK = (.34, .22, .055, 8)   # straight chain: link length, width, wire radius, links (MiniGameModels.ChainTile = its length)
POT_RIM = .62        # the flower pot's lip, as a fraction of the Tripo pot's height

# name: (size axis, size in metres, verdict, target triangles)
PLAN = {
    "PulleyFrame": ("x", 6.6, "keep + rebuilt fork", 0),
    "PulleyWheel": ("d", 2.0, "raise", 3000),   # a third of the frame, as on the sheet
    "Wall":        ("x", 4.5, "keep", 0),
    "Paving":      ("x", 2.0, "lower", 300),
    "GatePost":    ("z", 3.0, "keep", 0),
    "Lantern":     ("z", .8, "raise", 2000),
    "BarRack":     ("x", 2.0, "keep", 0),
    "ChainPile":   ("x", 1.2, "rebuilt chain", 0),
    "HelmetBench": ("x", 1.6, "raise", 2000),
    "FlowerPot":   ("z", 1.0, "raise", 4000),
}


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def load(name):
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"))
    ob = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    ob.name = name
    return ob


def split(ob, rule):
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    seen = np.zeros(len(bm.faces), bool); groups = {}
    for f in bm.faces:
        if seen[f.index]: continue
        stack = [f]; seen[f.index] = True; isl = []
        while stack:
            g = stack.pop(); isl.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if not seen[h.index]: seen[h.index] = True; stack.append(h)
        c = sum((bm.faces[i].calc_center_median() for i in isl), Vector()) / len(isl)
        groups.setdefault(rule(c), []).extend(isl)
    out = {}
    for key, idx in groups.items():
        keep = set(idx)
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new("D_" + key); b2.to_mesh(me); b2.free()
        for m in ob.data.materials: me.materials.append(m)
        o = bpy.data.objects.new("D_" + key, me); bpy.context.scene.collection.objects.link(o)
        out[key] = o
    bm.free(); bpy.data.objects.remove(ob)
    return out


def bounds(o):
    vs = [v.co for v in o.data.vertices]
    return Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)])


def fit(o, axis, size):
    lo, hi = bounds(o)
    ext = hi - lo
    k = size / (ext.x if axis == "x" else ext.z if axis == "z" else max(ext.x, ext.z))
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in o.data.vertices: v.co = (v.co - c) * k
    return k


def modifier(o, kind, **kw):
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new(kind, kind)
    for a, b in kw.items(): setattr(m, a, b)
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier=m.name)


def raise_to(o, target, levels=1):
    modifier(o, 'WELD', merge_threshold=.002)
    modifier(o, 'SUBSURF', levels=levels, render_levels=levels, uv_smooth='PRESERVE_BOUNDARIES')
    if tris(o) > target:
        modifier(o, 'DECIMATE', ratio=target / tris(o), use_collapse_triangulate=True)


def lower_to(o, target):
    modifier(o, 'WELD', merge_threshold=.002)
    modifier(o, 'DECIMATE', ratio=min(1, target / tris(o)), use_collapse_triangulate=True)


def wheel_axis(o):
    """The wheel's axle = its thinnest direction (PCA). Turns it onto Blender y and moves the origin to its centre."""
    p = np.array([v.co[:] for v in o.data.vertices])
    c = p.mean(0)
    w, vec = np.linalg.eigh(np.cov((p - c).T))
    axis = Vector(vec[:, 0]).normalized()
    if axis.y < 0: axis = -axis
    rot = axis.rotation_difference(Vector((0, 1, 0))).to_matrix().to_4x4()
    lo, hi = bounds(o)
    centre = (lo + hi) / 2
    o.data.transform(Matrix.Translation(-centre))
    o.data.transform(rot)
    lo, hi = bounds(o)
    o.data.transform(Matrix.Translation(-(lo + hi) / 2))
    return bounds(o)


def iron_material(name, rgb):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = .55
    b.inputs["Metallic"].default_value = .6
    return m


def sample_colour(o, pick):
    """Mean base colour of the texels under o's faces that pick(rgb) accepts."""
    mat = o.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "base" in n.image.name.lower())
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    uv = o.data.uv_layers.active.data
    cols = []
    for l in range(0, len(uv), 7):
        u, v = uv[l].uv
        c = px[int(min(.999, max(0, v)) * (h - 1)), int(min(.999, max(0, u)) * (w - 1)), :3]
        if pick(c): cols.append(c)
    return tuple(np.mean(cols, 0)) if cols else (.05, .05, .055)


def box(bm, lo, hi):
    vs = [bm.verts.new((x, y, z)) for z in (lo[2], hi[2]) for y in (lo[1], hi[1]) for x in (lo[0], hi[0])]
    for q in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        bm.faces.new([vs[i] for i in q])


def add_part(o, build, material):
    """Appends new geometry (built in a fresh bmesh) to o with its own material slot."""
    o.data.materials.append(material); slot = len(o.data.materials) - 1
    nb = bmesh.new(); build(nb)
    for f in nb.faces: f.material_index = slot
    bmesh.ops.recalc_face_normals(nb, faces=nb.faces)
    me = bpy.data.meshes.new("part"); nb.to_mesh(me); nb.free()
    bm = bmesh.new(); bm.from_mesh(o.data); bm.from_mesh(me); bm.to_mesh(o.data); bm.free()
    bpy.data.meshes.remove(me)


def link(bm, at, along, up, length=.2, width=.13, wire=.032, seg=10, ring=5):
    """One oval chain link: a torus stretched along `along`, its plane spanned by along x up."""
    along, up = along.normalized(), up.normalized()
    side = along.cross(up).normalized()
    rows = []
    for i in range(seg):
        a = i * 2 * math.pi / seg
        cx, cz = math.cos(a), math.sin(a)
        centre = at + along * cx * (length / 2 - wire) + up * cz * (width / 2 - wire)
        radial = (along * cx * (width / length) + up * cz).normalized()
        row = []
        for j in range(ring):
            b = j * 2 * math.pi / ring
            row.append(bm.verts.new(centre + (radial * math.cos(b) + side * math.sin(b)) * wire))
        rows.append(row)
    for i in range(seg):
        for j in range(ring):
            bm.faces.new((rows[i][j], rows[(i + 1) % seg][j], rows[(i + 1) % seg][(j + 1) % ring], rows[i][(j + 1) % ring]))


def chain(bm, start, direction, n, up=Vector((0, 0, 1)), length=.2, width=.13, wire=.032, seg=10, ring=5):
    d = direction.normalized()
    for i in range(n):
        u = up if i % 2 == 0 else d.cross(up)          # each link turned a quarter to the last
        link(bm, start + d * (i * (length - 2 * wire)), d, u, length=length, width=width, wire=wire, seg=seg, ring=ring)


def pulley_fork(frame, wheel_r, wheel_w, iron):
    """The hanging fork Tripo lost: two iron cheek plates from under the beam's middle down past the axle, the axle.
    The axle runs along the beam (Blender x) so the wheel stands in the plane the chain comes in (from the capstan,
    across the beam), and the chain can wrap over it (user, 11-07: wheel and chain must really work together). The
    axle hangs low enough for the chain to pass over the wheel under the beam."""
    vs = [v.co for v in frame.data.vertices if abs(v.co.x) < .25]
    top = max(v.z for v in vs)
    beam = [v for v in vs if v.z > top - 1.2]
    beam_bottom = min(v.z for v in beam)
    yc = (min(v.y for v in beam) + max(v.y for v in beam)) / 2
    axle_z = beam_bottom - .35 - wheel_r
    gap = wheel_w / 2 + .04
    print("FORK beam bottom %.2f, beam middle y %.2f, axle z %.2f (axle along x)" % (beam_bottom, yc, axle_z))

    def build(bm):
        for side in (-1, 1):
            x0, x1 = sorted((side * gap, side * (gap + .07)))
            box(bm, (x0, yc - .22, axle_z - .24), (x1, yc + .22, beam_bottom + .02))
        box(bm, (-gap - .1, yc - .26, beam_bottom - .04), (gap + .1, yc + .26, beam_bottom + .04))   # strap over the cheeks
        ax = bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=.06, radius2=.06, depth=2 * gap + .24)
        for v in ax["verts"]:
            v.co = Vector((v.co.z, v.co.y + yc, v.co.x + axle_z))    # cone axis (z) onto x
    add_part(frame, build, iron)
    return axle_z, yc


def chain_pile(pile, iron):
    """Keeps the pallet (wood-coloured faces), drops the melted chain, coils new links on it."""
    mat = pile.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "base" in n.image.name.lower())
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bm = bmesh.new(); bm.from_mesh(pile.data)
    uvl = bm.loops.layers.uv.active
    wood_z = []
    for f in bm.faces:
        u = sum(l[uvl].uv.x for l in f.loops) / len(f.loops); v = sum(l[uvl].uv.y for l in f.loops) / len(f.loops)
        c = px[int(min(.999, max(0, v)) * (h - 1)), int(min(.999, max(0, u)) * (w - 1)), :3] ** (1 / 2.2)
        if (c.max() - c.min()) / max(c.max(), 1e-4) > .35: wood_z.append(f.calc_center_median().z)
    wood_z.sort()
    top = wood_z[int(len(wood_z) * .9)]                  # the pallet's plank tops
    grey = []
    for f in bm.faces:
        u = sum(l[uvl].uv.x for l in f.loops) / len(f.loops); v = sum(l[uvl].uv.y for l in f.loops) / len(f.loops)
        c = px[int(min(.999, max(0, v)) * (h - 1)), int(min(.999, max(0, u)) * (w - 1)), :3] ** (1 / 2.2)
        z = f.calc_center_median().z
        if z > top + .01: grey.append(f)     # the pallet's own top stays (grey-picking holed it, 11-07)
    bmesh.ops.delete(bm, geom=grey, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(pile.data); bm.free()
    print("PILE pallet top %.2f" % top)

    def build(bm):
        z = top - .005
        for layer, (r, n) in enumerate(((.53, 17), (.44, 14), (.36, 11), (.27, 8), (.17, 5))):   # the wide first ring hides the old chain's stumps
            for i in range(n):
                a0, a1 = i * 2 * math.pi / n, (i + 1) * 2 * math.pi / n
                p0 = Vector((math.cos(a0) * r, math.sin(a0) * r, z)); p1 = Vector((math.cos(a1) * r, math.sin(a1) * r, z))
                up = Vector((0, 0, 1)) if i % 2 == 0 else (p1 - p0).cross(Vector((0, 0, 1)))
                link(bm, (p0 + p1) / 2, p1 - p0, up, length=(p1 - p0).length + .07, seg=10, ring=5)
            z += .06
    add_part(pile, build, iron)
    return top


def straight_chain(iron):
    me = bpy.data.meshes.new("D_ChainStraight"); o = bpy.data.objects.new("D_ChainStraight", me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(iron)
    # Thicker than the first version (user, 11-07: "쇠슬 더 두껍게"): 0.34 m links of 5.5 cm wire, as the pile's.
    L, W, R, N = CHAIN_LINK
    span = (N - 1) * (L - 2 * R) + L
    bm = bmesh.new(); chain(bm, Vector((-span / 2 + L / 2, 0, W / 2)), Vector((1, 0, 0)), N, length=L, width=W, wire=R, seg=12, ring=6)
    print("CHAIN straight length %.3f m" % span)
    bm.to_mesh(me); bm.free()
    return o


def save_atlas(o, stem):
    mat = o.data.materials[0]
    imgs = {n.image.name: n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
    for key, n in imgs.items():
        kind = "BaseColor" if "base" in key.lower() else "Normal"
        img = n.image.copy(); img.name = "%s_%s" % (stem, kind)
        if kind == "Normal": img.colorspace_settings.name = 'Non-Color'
        else: img.scale(2048, 2048)
        img.filepath_raw = os.path.join(TEX, "%s_%s.png" % (stem, kind)); img.file_format = 'PNG'; img.save()
        n.image = img
    mat.name = stem


def flat_material(name, rgb, rough=.7):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
    return m


def rebuild_wheel(o, iron, r, w):
    """Tripo's wheel had melted spokes and a pale web between them; a clean one turns better. Same size and axle
    (Blender x through the origin since the chain wraps it): grooved rim (two flanges), eight spokes, a hub with an axle hole, rivets."""
    bm = bmesh.new()
    # rim: a lathe profile (radius, y) turned about y
    prof = [(r * .80, -w * .36), (r, -w * .5), (r, -w * .38), (r * .90, -w * .12), (r * .90, w * .12),
            (r, w * .38), (r, w * .5), (r * .80, w * .36)]
    seg = 48
    rings = []
    for i in range(seg):
        a = i * 2 * math.pi / seg
        rings.append([bm.verts.new((math.cos(a) * rr, y, math.sin(a) * rr)) for rr, y in prof])
    for i in range(seg):
        j = (i + 1) % seg
        for k in range(len(prof)):
            k2 = (k + 1) % len(prof)
            bm.faces.new((rings[i][k], rings[j][k], rings[j][k2], rings[i][k2]))
    # hub: a tube along y
    hub_r, hole, hub_w = r * .24, r * .08, w * .7
    hs = 20
    out0, out1, in0, in1 = [], [], [], []
    for i in range(hs):
        a = i * 2 * math.pi / hs; c, s = math.cos(a), math.sin(a)
        out0.append(bm.verts.new((c * hub_r, -hub_w / 2, s * hub_r))); out1.append(bm.verts.new((c * hub_r, hub_w / 2, s * hub_r)))
        in0.append(bm.verts.new((c * hole, -hub_w / 2, s * hole))); in1.append(bm.verts.new((c * hole, hub_w / 2, s * hole)))
    for i in range(hs):
        j = (i + 1) % hs
        bm.faces.new((out0[i], out0[j], out1[j], out1[i])); bm.faces.new((in0[j], in0[i], in1[i], in1[j]))
        bm.faces.new((out0[j], out0[i], in0[i], in0[j])); bm.faces.new((out1[i], out1[j], in1[j], in1[i]))
    # spokes: boxes from the hub to the rim's inner face
    for i in range(8):
        a = i * math.pi / 4
        b2 = bmesh.ops.create_cube(bm, size=1.0)["verts"]
        L = r * .82 - hub_r * .8
        for v in b2:
            x, y, z = v.co.x * L, v.co.y * w * .32, v.co.z * r * .11
            x += hub_r * .8 + L / 2
            v.co = Vector((math.cos(a) * x - math.sin(a) * z, y, math.sin(a) * x + math.cos(a) * z))
    # rivets on both flanges
    for side in (-1, 1):
        for i in range(12):
            a = (i + .5) * 2 * math.pi / 12
            sph = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r * .035)["verts"]
            for v in sph:
                v.co += Vector((math.cos(a) * r * .9, side * w * .44, math.sin(a) * r * .9))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o.data.materials.clear(); o.data.materials.append(iron)
    bm.to_mesh(o.data); bm.free()
    o.data.transform(Matrix.Rotation(math.pi / 2, 4, 'Z'))        # axle onto Blender x (see pulley_fork)
    for p in o.data.polygons: p.material_index = 0; p.use_smooth = False


def islands_of(bm):
    bm.faces.ensure_lookup_table()
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen: continue
        st, isl = [f], []
        seen.add(f.index)
        while st:
            g = st.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); st.append(h)
        out.append(isl)
    return out


def clean_lantern(o):
    """Drops the twisted bits Tripo left inside the cage. They are joined to the bars, so they go by place: faces whose
    centre lies in the cage's open middle (within 55% of its radius of its axis, between its floor and its hood)."""
    bm = bmesh.new(); bm.from_mesh(o.data)
    lo, hi = bounds(o); H = hi.z - lo.z
    band = [v.co for v in bm.verts if lo.z + H * .12 < v.co.z < lo.z + H * .55]
    # the cage hangs on the far side from the wall plate: its axis is the middle of the band's verts away from the plate
    xs = sorted(v.x for v in band); ys = sorted(v.y for v in band)
    cage = [v for v in band if v.x > xs[len(xs) // 3]] if (xs[-1] - xs[0]) > (ys[-1] - ys[0]) else [v for v in band if v.y > ys[len(ys) // 3]]
    ax = sum((Vector((v.x, v.y, 0)) for v in cage), Vector()) / len(cage)
    rad = max((Vector((v.x, v.y, 0)) - ax).length for v in cage)
    drop = [f for f in bm.faces
            if lo.z + H * .14 < f.calc_center_median().z < lo.z + H * .52
            and (Vector((f.calc_center_median().x, f.calc_center_median().y, 0)) - ax).length < rad * .55]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    print("LANTERN dropped %d faces inside the cage (axis %.2f %.2f, radius %.2f)" % (len(drop), ax.x, ax.y, rad))


def replant(o):
    """Keeps the stone urn, replaces Tripo's crumpled foliage with leaves and small white flowers."""
    mat = o.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "base" in n.image.name.lower())
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bm = bmesh.new(); bm.from_mesh(o.data); uvl = bm.loops.layers.uv.active
    lo, hi = bounds(o); H = hi.z - lo.z
    kinds = []
    for f in bm.faces:
        u = sum(l[uvl].uv.x for l in f.loops) / len(f.loops); v = sum(l[uvl].uv.y for l in f.loops) / len(f.loops)
        c = px[int(min(.999, max(0, v)) * (h - 1)), int(min(.999, max(0, u)) * (w - 1)), :3] ** (1 / 2.2)
        mx, mn = c.max(), c.min(); sat = (mx - mn) / max(mx, 1e-4)
        green = c[1] >= c[0] and c[1] > c[2] and sat > .2
        stone = c[0] >= c[1] >= c[2] and .08 < sat < .45 and mx > .45
        kinds.append((f, f.calc_center_median(), green or (mx > .8 and sat < .2), stone))
    # The leftover foliage is painted stone-coloured too, so colour cannot find the urn's rim: cut at its height on
    # the sheet (the bowl's lip at ~62% of the whole), everything above goes.
    rim = lo.z + H * POT_RIM
    drop = [f for f, cc, fol, st in kinds if cc.z > rim]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    rim_r = max(math.hypot(v.co.x, v.co.y) for v in o.data.vertices if v.co.z > rim - .06)
    soil, leaf, petal, heart = (flat_material("PR_Early_Soil", (.10, .06, .035), .95), flat_material("PR_Early_Leaf", (.10, .27, .07), .6),
                                flat_material("PR_Early_Petal", (.86, .85, .80), .6), flat_material("PR_Early_FlowerHeart", (.80, .58, .10), .6))
    import random
    rnd = random.Random(505)

    def blade(bm, base, yaw, tilt, length, width):
        """A thin, slightly cupped leaf (both faces), from base outward."""
        d = Vector((math.cos(yaw) * math.cos(tilt), math.sin(yaw) * math.cos(tilt), math.sin(tilt)))
        side = Vector((-math.sin(yaw), math.cos(yaw), 0))
        up = side.cross(d).normalized()
        pts = []
        for t in (0, .25, .55, .85, 1):
            half = width * math.sin(math.pi * min(t, .95)) * .5
            sag = -length * .12 * t * t
            pts.append((base + d * length * t + up * sag, half))
        top, bot = [], []
        for p, half in pts:
            top.append([bm.verts.new(p - side * half + up * .006), bm.verts.new(p + side * half + up * .006)])
            bot.append([bm.verts.new(p - side * half - up * .006), bm.verts.new(p + side * half - up * .006)])
        for i in range(len(pts) - 1):
            bm.faces.new((top[i][0], top[i][1], top[i + 1][1], top[i + 1][0]))
            bm.faces.new((bot[i][1], bot[i][0], bot[i + 1][0], bot[i + 1][1]))

    def build_soil(bm):
        c = bmesh.ops.create_circle(bm, cap_ends=True, segments=24, radius=rim_r * .82)
        for v in c["verts"]: v.co.z = rim - .04

    def build_leaves(bm):
        for i in range(18):
            yaw = i * 2 * math.pi / 18 + rnd.uniform(-.15, .15)
            rr = rim_r * rnd.uniform(.15, .55)
            base = Vector((math.cos(yaw) * rr, math.sin(yaw) * rr, rim - .04))
            blade(bm, base, yaw, math.radians(rnd.uniform(25, 50)), rnd.uniform(.22, .30), rnd.uniform(.15, .20))

    flowers = []
    for i in range(9):
        a = i * 2 * math.pi / 9 + rnd.uniform(-.2, .2)
        rr = 0 if i == 0 else rim_r * rnd.uniform(.25, .6)
        flowers.append(Vector((math.cos(a) * rr, math.sin(a) * rr, rim + rnd.uniform(.10, .20))))

    def build_petals(bm):
        for c in flowers:
            for k in range(5):
                a = k * 2 * math.pi / 5
                blade(bm, c, a, math.radians(18), .075, .06)

    def build_hearts(bm):
        for c in flowers:
            s = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=.022)["verts"]
            for v in s: v.co += c + Vector((0, 0, .012))

    for build, m in ((build_soil, soil), (build_leaves, leaf), (build_petals, petal), (build_hearts, heart)):
        add_part(o, build, m)
    print("POT foliage replaced: %d Tripo faces out, rim at %.2f m, radius %.2f" % (len(drop), rim, rim_r))


def erase_marks(o, ratio=.55, radius=10):
    """The entrance post's stones carry dark curved crack strokes painted by Tripo (user, 11-07: "자국 지워주고").
    Only inside the post's own UV islands of the shared atlas: a texel much darker than its neighbourhood (below
    `ratio` of a box blur's brightness) takes the blurred stone colour. The mortar joints are lighter than the cracks
    and stay. The atlas is saved again (the other parts on it are untouched)."""
    mat = o.data.materials[0]
    node = next(n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "BaseColor" in n.image.name)
    img = node.image
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    # the post's islands: rasterise its UV triangles
    mask = np.zeros((h, w), bool)
    uv = o.data.uv_layers.active.data
    for poly in o.data.polygons:
        pts = [uv[i].uv for i in poly.loop_indices]
        for k in range(1, len(pts) - 1):
            tri = np.array([[pts[0].x * (w - 1), pts[0].y * (h - 1)], [pts[k].x * (w - 1), pts[k].y * (h - 1)], [pts[k + 1].x * (w - 1), pts[k + 1].y * (h - 1)]])
            x0, y0 = np.floor(tri.min(0)).astype(int); x1, y1 = np.ceil(tri.max(0)).astype(int)
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, w - 1), min(y1, h - 1)
            if x1 < x0 or y1 < y0: continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
            (ax, ay), (bx, by), (cx, cy) = tri
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(d) < 1e-9: continue
            l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / d
            l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / d
            inside = (l1 >= -.02) & (l2 >= -.02) & (1 - l1 - l2 >= -.02)
            mask[gy[inside], gx[inside]] = True
    rgb = px[..., :3]
    lum = rgb @ np.array([.2126, .7152, .0722], dtype=np.float32)

    def blur(a):
        k = 2 * radius + 1
        c = np.cumsum(np.pad(a, ((radius + 1, radius), (0, 0)) + ((0, 0),) * (a.ndim - 2), mode='edge'), 0)
        a = (c[k:] - c[:-k]) / k
        c = np.cumsum(np.pad(a, ((0, 0), (radius + 1, radius)) + ((0, 0),) * (a.ndim - 2), mode='edge'), 1)
        return (c[:, k:] - c[:, :-k]) / k
    soft_lum = blur(lum)
    soft_rgb = blur(rgb)
    # dark crack strokes, and the pale rims Tripo painted along them
    mark = mask & ((lum < soft_lum * ratio) | (lum > soft_lum * (2 - ratio) * .85))
    # grow the marks a pixel so their soft edges go too
    grown = mark.copy()
    grown[1:] |= mark[:-1]; grown[:-1] |= mark[1:]; grown[:, 1:] |= mark[:, :-1]; grown[:, :-1] |= mark[:, 1:]
    grown &= mask
    px[grown, :3] = soft_rgb[grown] * 1.05
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(TEX, "PR_Early_TripoStructure_BaseColor.png"); img.file_format = 'PNG'; img.save()
    # the cracks are also carved in the normal map: flatten it there (and a little around)
    nnode = next((n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "Normal" in n.image.name), None)
    if nnode is not None:
        nimg = nnode.image; nw, nh = nimg.size
        npx = np.array(nimg.pixels[:], dtype=np.float32).reshape(nh, nw, 4)
        ys, xs = np.nonzero(grown)
        ys = (ys * (nh / h)).astype(int); xs = (xs * (nw / w)).astype(int)
        flat = np.zeros((nh, nw), bool); flat[ys, xs] = True
        for _ in range(2):
            g = flat.copy(); g[1:] |= flat[:-1]; g[:-1] |= flat[1:]; g[:, 1:] |= flat[:, :-1]; g[:, :-1] |= flat[:, 1:]; flat = g
        npx[flat, :3] = (.5, .5, 1.0)
        nimg.pixels.foreach_set(npx.ravel())
        nimg.filepath_raw = os.path.join(TEX, "PR_Early_TripoStructure_Normal.png"); nimg.file_format = 'PNG'; nimg.save()
    print("MARKS erased on the gate post: %d texels of %d in its islands" % (int(grown.sum()), int(mask.sum())))


def fill_wall(o):
    """Tripo's wall is a shell with holes between blocks and at its back: seen through in Unity (user, 11-07: "여기
    깨졌다", "뒤에 깨져 있잖아"). A solid stone core goes inside the body, up to the crenels' floor.
    It sits behind the face everywhere, so only holes show it; flat colour = the wall's mean stone."""
    lo, hi = bounds(o)
    vs = [v.co for v in o.data.vertices]
    # top profile across the wall: highest vertex per 10 cm strip
    xs = np.arange(lo.x + .05, hi.x - .05, .1)
    prof = []
    for x in xs:
        z = [v.z for v in vs if abs(v.x - x) < .05]
        prof.append(max(z) if z else lo.z)
    prof = np.array(prof)
    crenel = float(np.percentile(prof, 15))          # the gaps' floor
    # The core must stay behind every bit of the face: per 20 cm of height, the face's nearest point (front, -y) and
    # the back's; the core lies between the deepest front and the shallowest back (first try took the extremes and
    # stood out of the recessed middle rows).
    fronts, backs = [], []
    for z0 in np.arange(lo.z + .1, crenel - .1, .2):
        band = [v for v in vs if z0 <= v.z < z0 + .2 and lo.x + .3 < v.x < hi.x - .3]
        if len(band) < 20: continue
        ys = sorted(v.y for v in band)
        fronts.append(ys[int(len(ys) * .05)]); backs.append(ys[int(len(ys) * .95)])
    # Tripo left the middle rows without faces on both sides (a big hole, user 11-07), so the core comes almost to the
    # faces and carries the tileable phase-1 stone brick (R502 PR_Early_StoneBrick, 2 m a tile, box-mapped in metres),
    # tinted to this wall's mean stone; the bulging stones stand a little proud of it.
    f = min(fronts) + .05
    bk = max(backs) - .05
    y0, y1 = f, bk
    stone = sample_colour(o, lambda c: c.max() > .25)
    brick_mean = (.835, .724, .614)                       # mean cream of PR_Early_StoneBrick_BaseColor (linear)
    tint = tuple(min(1.0, float(a) / b) for a, b in zip(stone, brick_mean))
    core = bpy.data.materials.new("PR_Early_WallCore"); core.use_nodes = True
    nt = core.node_tree; bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_StoneBrick_BaseColor.png"))
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1
    mix.inputs[7].default_value = (*tint, 1)
    nt.links.new(tex.outputs[0], mix.inputs[6]); nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    nrm = nt.nodes.new("ShaderNodeTexImage"); nrm.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_StoneBrick_Normal.png"))
    nrm.image.colorspace_settings.name = 'Non-Color'
    nmap = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(nrm.outputs[0], nmap.inputs["Color"]); nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])
    uvname = o.data.uv_layers.active.name

    def build(bm):
        uvl = bm.loops.layers.uv.new(uvname)
        x0, x1, z0, z1 = lo.x + .1, hi.x - .1, lo.z + .05, crenel - .06
        vs8 = [bm.verts.new((x, y, z)) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
        for q in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
            face = bm.faces.new([vs8[i] for i in q]); face.normal_update()
            nx, ny, nz = (abs(c) for c in face.normal)
            for l in face.loops:
                co = l.vert.co
                u, v = (co.x, co.y) if nz >= max(nx, ny) else ((co.y, co.z) if nx >= ny else (co.x, co.z))
                l[uvl].uv = (u / 2.0, v / 2.0)
    add_part(o, build, core)
    srgb = tuple(round(((1.055 * c ** (1 / 2.4) - .055) if c > .0031308 else 12.92 * c), 2) for c in tint)
    print("WALL core: body to %.2f m, depth %.2f..%.2f, brick tint linear %s sRGB %s" % (crenel, y0, y1, tuple(round(c, 3) for c in tint), srgb))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = split(load(STRUCTURE), structure_rule)
    parts.update(split(load(PROPS), props_rule))
    save_atlas(parts["Wall"], "PR_Early_TripoStructure")
    save_atlas(parts["BarRack"], "PR_Early_TripoProps")
    report = []
    for name, (axis, size, verdict, target) in PLAN.items():
        o = parts[name]; before = tris(o)
        k = fit(o, axis, size)
        if name == "PulleyWheel": raise_to(o, target, levels=2)
        elif verdict == "raise": raise_to(o, target)
        elif verdict == "lower": lower_to(o, target)
        report.append([name, before, k, verdict])
    dark = sample_colour(parts["PulleyWheel"], lambda c: c.max() < .2)
    iron = iron_material("PR_Early_DarkIron", dark)
    lo, hi = wheel_axis(parts["PulleyWheel"])
    wheel_r, wheel_w = (hi.z - lo.z) / 2, hi.y - lo.y
    axle_z, axle_y = pulley_fork(parts["PulleyFrame"], wheel_r, wheel_w, iron)
    chain_pile(parts["ChainPile"], iron)
    parts["ChainStraight"] = straight_chain(iron); report.append(["ChainStraight", 0, 1, "new"])
    # Fixes after the first look (user: "부품 먼저 고치기", 11-07)
    rebuild_wheel(parts["PulleyWheel"], iron, wheel_r, wheel_w)
    clean_lantern(parts["Lantern"])
    replant(parts["FlowerPot"])
    mg_holes.close_small_holes(parts["Wall"], 4.0)
    fill_wall(parts["Wall"])
    erase_marks(parts["GatePost"])
    print("IRON colour", tuple(round(x, 3) for x in dark), " wheel r %.2f w %.2f, axle at %.2f under the beam" % (wheel_r, wheel_w, axle_z))
    for name, before, k, verdict in report:
        o = parts[name]; lo, hi = bounds(o); e = hi - lo
        print("ASSET %-13s %-20s tris %5d -> %5d  size %.2f x %.2f x %.2f  (x%.1f)" % (name, verdict, before, tris(o), e.x, e.y, e.z, k))
    if "--export" in ARGS:
        os.makedirs(OUT, exist_ok=True)
        for name, o in parts.items():
            if name in SKIP_EXPORT: continue
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "D_%s.fbx" % name), use_selection=True,
                                     axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                     mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    # lay out for the renders (export used each asset at its own origin)
    x = 0
    order = ["PulleyFrame", "PulleyWheel", "Wall", "Paving", "GatePost", "Lantern", "BarRack", "ChainPile", "ChainStraight", "HelmetBench", "FlowerPot"]
    for name in order:
        o = parts[name]; lo, hi = bounds(o)
        o.location = (x - lo.x, 0, 0 if name != "PulleyWheel" else -lo.z)
        x += (hi.x - lo.x) + .8
    pawn = bpy.data.materials.new("pawn"); pawn.use_nodes = True
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(-1, 0, .31)); bpy.context.object.data.materials.append(pawn)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(-1, 0, .84)); bpy.context.object.data.materials.append(pawn)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "D_Props_v1.blend"))
    if "--render" in ARGS:
        render(parts, order)
        assembled(parts, axle_y, axle_z)


def assembled(parts, axle_y, axle_z):
    sc = bpy.context.scene; cam = sc.camera
    f, wh = parts["PulleyFrame"], parts["PulleyWheel"]
    copy = wh.copy(); sc.collection.objects.link(copy)
    copy.location = f.location + Vector((0, axle_y, axle_z))
    c = f.location + Vector((0, 0, 2.2))
    for name, off in (("front", Vector((1.5, -9, 1.8))), ("side", Vector((7, -4, 2.5)))):
        cam.location = c + off
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_d_prop_PulleyAssembled_%s.png" % name)
        bpy.ops.render.render(write_still=True)
    sc.collection.objects.unlink(copy)


def render(parts, order):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1000, 800
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.6, .66, .74, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .8
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-25))); bpy.context.object.data.energy = 3.4
    cd = bpy.data.cameras.new("c"); cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    cd.lens = 50
    for name in order:
        o = parts[name]; lo, hi = bounds(o)
        c = o.location + (lo + hi) / 2
        d = max(hi - lo) * 2.0 + .8
        cam.location = c + Vector((d * .5, -d * .8, d * .45))
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_d_prop_%s.png" % name)
        bpy.ops.render.render(write_still=True)


main()
