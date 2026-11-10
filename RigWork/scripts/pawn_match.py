# The new pawn, assembled to match the game's pawn (Pawn.fbx as RagdollLabBuilder reads it) and weighted by the
# builder's own rules, then rendered beside the old one in the same poses.
#
# blender -b -P pawn_match.py -- <Pawn.fbx> <parts dir> <out dir>
import bpy, bmesh, sys, os, math, json
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

PAWN_FBX, PARTS, OUT = sys.argv[sys.argv.index("--") + 1:][:3]
os.makedirs(OUT, exist_ok=True)
P = "mixamorig:"
WOOD, DARK, BLACK, PINK = (0.93, 0.84, 0.70), (0.10, 0.085, 0.075), (0.02, 0.02, 0.02), (0.95, 0.55, 0.55)
# The old texture's colours by part (sample_colors.py: sRGB medians of the texels under each part). TEAM is the marker
# the piece shader replaces with the team colour (blue for one side, red for the other); its preview is the old blue.
SKIN, HEADC, HANDC, FOOTC = (0.945, 0.808, 0.702), (0.898, 0.753, 0.649), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
EYE, GLINT, CHEEK, OLD_BLUE, TEAM = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93), (0.91, 0.59, 0.53), (0.314, 0.376, 0.58), (0.0, 0.0, 1.0)

def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)

def select_only(objs, active=None):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]

def bounds_pts(pts):
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))

def bounds(objs):
    return bounds_pts([o.matrix_world @ v.co for o in objs for v in o.data.vertices])

def material(rgb):
    name = "M_%02x%02x%02x" % tuple(int(c * 255) for c in rgb)
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = (*rgb, 1.0)
    return m

def paint(o, rgb, vc=None):
    """The preview material, and the part's colour as a vertex colour (sRGB bytes per face corner) for the shader."""
    o.data.materials.clear()
    o.data.materials.append(material(rgb))
    me = o.data
    attr = me.color_attributes.get("Col") or me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    col = (*(vc or rgb), 1.0)
    for d in attr.data:
        d.color_srgb = col
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = me.color_attributes.find("Col")

def add_weights(o, fn):
    """fn(world point, vertex index) -> {bone: weight}."""
    for v in o.data.vertices:
        w = fn(o.matrix_world @ v.co, v.index)
        tot = sum(w.values())
        for b, x in w.items():
            if x / tot < 0.01:
                continue
            g = o.vertex_groups.get(P + b) or o.vertex_groups.new(name=P + b)
            g.add([v.index], x / tot, "REPLACE")

def fresh_normals(o, angle=40):
    select_only([o])
    if o.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))

def dup(o):
    c = o.copy()
    c.data = o.data.copy()
    bpy.context.scene.collection.objects.link(c)
    return c

def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

def import_parts(path):
    existing = set(bpy.data.objects)
    new = import_fbx(path)
    mesh = [o for o in new if o.type == "MESH"][0]
    for o in new:
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    mesh.parent = None
    select_only([mesh])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [o for o in bpy.data.objects if o.type == "MESH" and o not in existing]
    return sorted(parts, key=lambda o: -len(o.data.vertices))

def bvh(o):
    return BVHTree.FromPolygons([o.matrix_world @ v.co for v in o.data.vertices], [tuple(p.vertices) for p in o.data.polygons])

def inside_parity(tree, p):
    votes = 0
    for d in (Vector((1, 0.013, 0.007)), Vector((-0.011, 1, 0.017)), Vector((0.009, -0.015, 1))):
        d.normalize()
        n, q = 0, p.copy()
        for _ in range(64):
            hit = tree.ray_cast(q, d)
            if hit[0] is None:
                break
            n += 1
            q = hit[0] + d * 1e-5
        votes += n % 2
    return votes >= 2

def drop_hidden(parts):
    trees = {o.name: bvh(o) for o in parts}
    keep, drop = [], []
    for o in parts:
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        sample = pts[:: max(1, len(pts) // 80)]
        others = [trees[q.name] for q in parts if q is not o]
        frac = sum(1 for p in sample if any(inside_parity(t, p) for t in others)) / len(sample)
        (drop if frac >= 0.9 and others else keep).append(o)
    for o in drop:
        bpy.data.objects.remove(o, do_unlink=True)
    return keep

def load_old():
    """The game's pawn: its armature, mesh, and the parts RagdollLabBuilder finds in it."""
    new = import_fbx(PAWN_FBX)
    arm = [o for o in new if o.type == "ARMATURE"][0]
    arm.animation_data_clear()
    arm.data.pose_position = "POSE"
    mesh = [o for o in new if o.type == "MESH"][0]
    return arm, mesh

def bone(arm, name):
    return arm.matrix_world @ arm.data.bones[P + name].head_local

def builder_parts(mesh):
    """RagdollLabBuilder's components: welded by position, kinds by where they are."""
    me = mesh.data
    ws = [mesh.matrix_world @ v.co for v in me.vertices]
    H = max(p.z for p in ws) - min(p.z for p in ws)
    par = list(range(len(ws)))
    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    def union(a, b):
        a, b = find(a), find(b)
        if a != b:
            par[a] = b
    weld = {}
    for i, v in enumerate(me.vertices):
        k = tuple(round(c * 20000) for c in v.co)
        if k in weld:
            union(i, weld[k])
        else:
            weld[k] = i
    for e in me.edges:
        union(e.vertices[0], e.vertices[1])
    groups = {}
    for i in range(len(ws)):
        groups.setdefault(find(i), []).append(i)
    comps = sorted(groups.values(), key=len, reverse=True)
    kinds = []
    for c, ids in enumerate(comps):
        mn, mx = bounds_pts([ws[i] for i in ids])
        ctr = (mn + mx) / 2
        ax = abs(ctr.x)
        kinds.append(0 if c == 0 else 1 if ax > 0.27 * H else 2 if (ax > 0.13 * H and 0.4 * H < ctr.z < 0.65 * H) else 3)
    return ws, H, comps, kinds

def builder_rules(arm, H):
    """The builder's weight rules (RagdollLabBuilder.BuildPawnPrefab, unchanged since 09-23)."""
    yHips, ySpine, ySpine1, ySpine2 = (bone(arm, n).z for n in ("Hips", "Spine", "Spine1", "Spine2"))
    yNeck, yHead = bone(arm, "Neck").z, bone(arm, "Head").z
    chainY = [yHips + 0.03, ySpine + 0.03, ySpine1 + 0.03, ySpine2 + 0.03]
    chainB = ["Hips", "Spine", "Spine1", "Spine2"]
    footTop = 0.12 * H
    def chain(y):
        if y <= chainY[0]:
            return {chainB[0]: 1.0}
        for k in range(3):
            if y > chainY[k + 1]:
                continue
            t = smooth((y - chainY[k]) / (chainY[k + 1] - chainY[k]))
            return {chainB[k]: 1 - t, chainB[k + 1]: t}
        return {chainB[-1]: 1.0}
    def body(p):
        radius = math.hypot(p.x, p.y)
        if p.z < footTop and abs(p.x) > 0.035 * H:
            return {("Left" if p.x > 0 else "Right") + "Foot": 1.0}
        if p.z >= yHead or (p.z > yNeck - 0.025 * H and radius < 0.13 * H):
            return {"Head": 1.0}
        return chain(p.z)
    return chain, body

def old_pawn(arm, mesh):
    """The old mesh weighted by the builder, painted by part (for the side-by-side)."""
    ws, H, comps, kinds = builder_parts(mesh)
    chain, body = builder_rules(arm, H)
    for g in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(g)
    compOf = {}
    sideOf = []
    for c, ids in enumerate(comps):
        for i in ids:
            compOf[i] = c
        sideOf.append("Left" if sum(ws[j].x for j in ids) > 0 else "Right")
    def fn(p, i):
        c = compOf[i]
        if c == 0:
            return body(p)
        k = kinds[c]
        side = sideOf[c]
        return {side + "Hand": 1.0} if k == 1 else {side + "Arm": 1.0} if k == 2 else {"Head": 1.0}
    add_weights(mesh, fn)
    if not any(m.type == "ARMATURE" for m in mesh.modifiers):
        mod = mesh.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
    return ws, H, comps, kinds

def old_measures(ws, H, comps, kinds):
    m = {}
    for side, s in (("Left", 1), ("Right", -1)):
        ball = [c for c, k in enumerate(kinds) if k == 1 and sum(ws[i].x for i in comps[c]) * s > 0][0]
        stub = [c for c, k in enumerate(kinds) if k == 2 and sum(ws[i].x for i in comps[c]) * s > 0][0]
        bmn, bmx = bounds_pts([ws[i] for i in comps[ball]])
        smn, smx = bounds_pts([ws[i] for i in comps[stub]])
        sc = (smn + smx) / 2
        inner = min(abs(smn.x), abs(smx.x))
        outer = max(abs(smn.x), abs(smx.x))
        main = comps[0]
        fs = [ws[i] for i in main if ws[i].z < 0.12 * H and ws[i].x * s > 0.035 * H]
        fmn, fmx = bounds_pts(fs)
        m[side] = dict(
            ball_centre=(bmn + bmx) / 2, ball_d=((bmx.x - bmn.x) + (bmx.y - bmn.y) + (bmx.z - bmn.z)) / 3,
            stub_axis=Vector((0, sc.y, sc.z)), stub_d=((smx.y - smn.y) + (smx.z - smn.z)) / 2, stub_in=inner, stub_out=outer,
            arm_pivot=Vector((s * (inner + 0.012), sc.y, sc.z)), hand_pivot=Vector((s * (outer - 0.02), sc.y, sc.z)),
            foot_centre=(fmn + fmx) / 2, foot_size=fmx - fmn)
    skirt = min(ws[i].z for i in comps[0] if abs(ws[i].x) <= 0.035 * H or ws[i].z >= 0.12 * H)
    m["skirt"] = skirt
    m["top"] = max(p.z for p in ws)
    m["H"] = H
    return m

def old_tree(ws, comps, mesh):
    """The old pawn's main island (body, collar, head; no face details, hands or stubs) for ray casts."""
    main = set(comps[0])
    return BVHTree.FromPolygons(ws, [tuple(pl.vertices) for pl in mesh.data.polygons if all(v in main for v in pl.vertices)])

def reach(tree, z, ang):
    """How far the surface reaches from the body's axis at height z in direction ang (outermost hit)."""
    d = Vector((math.cos(ang), math.sin(ang), 0))
    o = Vector((0, 0, z))
    hit = tree.ray_cast(o + d, -d)
    return (hit[0] - o).length if hit[0] is not None else None

DIRS = [i * math.pi / 4 for i in range(8)]

def ratios(old, new, z, zlo, zhi):
    """Old reach over new reach in eight directions at height z, each averaged over +-15 degrees and +-1 cm (inside
    [zlo, zhi]) so neither model's facets print onto the other."""
    out = []
    for a in DIRS:
        so = sn = 0.0
        n = 0
        for dz in (-0.005, 0.0, 0.005):
            zz = min(max(z + dz, zlo), zhi)
            for da in (-15, -7.5, 0, 7.5, 15):
                ro, rn = reach(old, zz, a + math.radians(da)), reach(new, zz, a + math.radians(da))
                if ro and rn:
                    so += ro
                    sn += rn
                    n += 1
        out.append(min(1.2, max(0.8, so / sn)) if n else 1.0)
    return out

def ratio_at(rs, ang):
    """The eight direction ratios, interpolated around the axis."""
    t = (ang % (2 * math.pi)) / (math.pi / 4)
    i = int(t) % 8
    f = t - int(t)
    return rs[i] * (1 - f) + rs[(i + 1) % 8] * f

def match_profile(body, head, old):
    """The new body's silhouette onto the old pawn's, height by height and direction by direction. The base and the head
    follow the old surface slice by slice (their ends inside the collar keep the ratios of the last slice outside it);
    each collar ring is scaled whole by the ratios at its own middle height."""
    base = body[0]
    collars = [o for o in body if o is not base and o is not head]
    cz = [v.co.z for o in collars for v in o.data.vertices]
    c_lo, c_hi = min(cz), max(cz)
    report = {}
    for o, zlo, zhi in ((base, 0.14, c_lo - 0.004), (head, c_hi + 0.004, 0.985)):
        new = bvh(o)
        cache = {}
        worst = 1.0
        for v in o.data.vertices:
            z = min(max(v.co.z, zlo), zhi)
            key = round(z, 3)
            if key not in cache:
                cache[key] = ratios(old, new, key, zlo, zhi)
            k = ratio_at(cache[key], math.atan2(v.co.y, v.co.x))
            worst = min(worst, k)
            v.co.x *= k
            v.co.y *= k
        o.data.update()
        report[o.name] = dict(z=[round(zlo, 3), round(zhi, 3)], slimmest=round(worst, 3))
    # The collar rings together, as designed relative to each other, scaled direction by direction from their widest
    # reach to the old collar band's widest reach (its middle, clear of the tilted top edge and the flared foot).
    if collars:
        ring = BVHTree.FromPolygons([v.co.copy() for o in collars for v in o.data.vertices],
                                    [tuple(sum(len(q.data.vertices) for q in collars[:collars.index(o)]) + i for i in pl.vertices)
                                     for o in collars for pl in o.data.polygons])
        band = [0.575 + 0.005 * i for i in range(8)]                       # 0.575 .. 0.61
        new_band = [c_lo + (c_hi - c_lo) * (0.1 + 0.8 * i / 7) for i in range(8)]
        rs = []
        for a in DIRS:
            ro = max((reach(old, z, a) or 0) for z in band)
            rn = max((reach(ring, z, a) or 0) for z in new_band)
            rs.append(min(1.2, max(0.8, ro / rn)) if rn else 1.0)
        for o in collars:
            for v in o.data.vertices:
                k = ratio_at(rs, math.atan2(v.co.y, v.co.x))
                v.co.x *= k
                v.co.y *= k
            o.data.update()
        report["collar"] = dict(ratios=[round(r, 3) for r in rs])
    return report

def profile_table(old, objs, z0=0.14, z1=0.99, dz=0.01):
    """Old reach against the new objects' reach in +x, -x, +y, -y (front is -y), every dz."""
    new = BVHTree.FromPolygons([o.matrix_world @ v.co for o in objs for v in o.data.vertices],
                               [tuple(sum(len(q.data.vertices) for q in objs[:objs.index(o)]) + i for i in pl.vertices)
                                for o in objs for pl in o.data.polygons])
    rows, worst = [], 0.0
    z = z0
    while z <= z1 + 1e-9:
        o = [reach(old, z, a) or 0 for a in (0, math.pi, math.pi / 2, -math.pi / 2)]
        n = [reach(new, z, a) or 0 for a in (0, math.pi, math.pi / 2, -math.pi / 2)]
        dev = max(abs(b - a) for a, b in zip(o, n))
        worst = max(worst, dev)
        rows.append("%.3f | old %s | new %s | worst gap %.4f m" % (z, " ".join("%.3f" % a for a in o), " ".join("%.3f" % b for b in n), dev))
        z += dz
    return rows, worst

def face_parts(ws, comps, kinds, mesh):
    """The old pawn's eyes, eye glints and cheeks (the builder's face-detail islands), copied out as plain data."""
    out = []
    for c, ids in enumerate(comps):
        if kinds[c] != 3:
            continue
        pts = [ws[i] for i in ids]
        mn, mx = bounds_pts(pts)
        ctr, ext = (mn + mx) / 2, mx - mn
        kind = ("cheek", CHEEK) if ctr.z < 0.71 else ("glint", GLINT) if max(ext) < 0.045 else ("eye", EYE)
        idx = {v: k for k, v in enumerate(ids)}
        faces = [[idx[v] for v in pl.vertices] for pl in mesh.data.polygons if all(v in idx for v in pl.vertices)]
        out.append(dict(name="Face_%s_%s" % (kind[0], "L" if ctr.x > 0 else "R"), verts=[p.copy() for p in pts], faces=faces,
                        rgb=kind[1]))
    return out

def place_face(parts, old, head):
    """Each face part where it was on the old head, at the same height over the head's surface: every vertex is moved
    along its ray from the head's middle by how much the new head's surface there differs from the old one's."""
    hc = Vector((0.0, 0.0, 0.80))
    new = bvh(head)
    objs = []
    # The old cheeks are islands sunk just under the head's surface; what shows is the blush painted on the head's
    # texture over them. The new head is too sparse there to paint it into its vertex colours, so each blush is a thin
    # disc laid on the head's surface (0.8 mm over it): full pink within 2 cm of the middle, the head's own colour by
    # 3.6 cm, so its rim cannot be seen.
    for part in parts:
        if not part["name"].startswith("Face_cheek"):
            continue
        c = sum(part["verts"], Vector()) / len(part["verts"])
        hit = new.ray_cast(hc, (c - hc).normalized())
        if hit[0] is None:
            continue
        p0, n0 = hit[0], hit[1].normalized()
        t1 = n0.cross(Vector((0, 0, 1))).normalized()
        t2 = n0.cross(t1).normalized()
        R, K, SEG = 0.036, 9, 32
        verts, cols, faces = [], [], []
        for k in range(K + 1):
            r = R * k / K
            for j in range(1 if k == 0 else SEG):
                a = 2 * math.pi * j / SEG
                q = p0 + (t1 * math.cos(a) + t2 * math.sin(a)) * r
                h = new.ray_cast(hc, (q - hc).normalized())
                q = h[0] if h[0] is not None else q
                verts.append(q + (q - hc).normalized() * 0.0008)
                w = 1 - smooth((r - 0.02) / 0.016)
                cols.append(tuple(HEADC[i] + (CHEEK[i] - HEADC[i]) * 0.85 * w for i in range(3)))
        for j in range(SEG):
            faces.append([0, 1 + j, 1 + (j + 1) % SEG])
        for k in range(1, K):
            a0, b0 = 1 + (k - 1) * SEG, 1 + k * SEG
            for j in range(SEG):
                j1 = (j + 1) % SEG
                faces.append([a0 + j, b0 + j, b0 + j1, a0 + j1])
        name = part["name"].replace("Face_cheek", "Face_blush")
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], faces)
        me.update()
        o = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(o)
        add_weights(o, lambda p, i: {"Head": 1.0})
        paint(o, CHEEK)
        attr = me.color_attributes["Col"]
        for li, loop in enumerate(me.loops):
            attr.data[li].color_srgb = (*cols[loop.vertex_index], 1.0)
        # outward: the disc's faces must face away from the head's middle
        if (me.polygons[0].center - hc).dot(me.polygons[0].normal) < 0:
            me.flip_normals()
        fresh_normals(o, 180)
        objs.append(o)
    for part in parts:
        if part["name"].startswith("Face_cheek"):
            continue
        moved = []
        for v in part["verts"]:
            d = v - hc
            r = d.length
            d.normalize()
            ho, hn = old.ray_cast(hc, d), new.ray_cast(hc, d)
            if ho[0] is not None and hn[0] is not None:
                r += (hn[0] - hc).length - (ho[0] - hc).length
            moved.append(hc + d * r)
        me = bpy.data.meshes.new(part["name"])
        me.from_pydata(moved, [], part["faces"])
        me.update()
        o = bpy.data.objects.new(part["name"], me)
        bpy.context.scene.collection.objects.link(o)
        add_weights(o, lambda p, i: {"Head": 1.0})
        paint(o, part["rgb"])
        fresh_normals(o)
        objs.append(o)
    return objs

def team_band(o, lo, hi):
    """Only the old collar's blue band is team-coloured: the ring is cut level at the band's edges and each face is
    coloured whole (face-corner colours), so the band's edges stay sharp however sparse the ring is."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    for z in (lo, hi):
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=Vector((0, 0, z)),
                               plane_no=Vector((0, 0, 1)))
    bm.to_mesh(o.data)
    bm.free()
    attr = o.data.color_attributes["Col"]
    for poly in o.data.polygons:
        col = (*(TEAM if lo <= poly.center.z <= hi else SKIN), 1.0)
        for li in poly.loop_indices:
            attr.data[li].color_srgb = col

# The old collar's blue band, measured on the old front render (band_rows.py): 0.5625 .. 0.5906 m.
BAND = (0.55, 0.591)   # the bottom edge below the new collar: no cut through its rounded lower rim

# ---------------------------------------------------------------- the new pawn
LOWPOLY = "--lowpoly" in sys.argv

def douglas_peucker(pts, tol):
    """Fewest points of a 2D polyline that keep every dropped point within tol of the kept ones."""
    keep = [False] * len(pts)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, b = stack.pop()
        ax, ay = pts[a]
        bx, by = pts[b]
        dx, dy = bx - ax, by - ay
        L = math.hypot(dx, dy) or 1e-12
        far, at = 0.0, -1
        for i in range(a + 1, b):
            px, py = pts[i]
            d = abs((px - ax) * dy - (py - ay) * dx) / L
            if d > far:
                far, at = d, i
        if far > tol:
            keep[at] = True
            stack += [(a, at), (at, b)]
    return [p for p, k in zip(pts, keep) if k]

def lathe_from(objs, segments=16, tol=0.0016, name="PawnBody"):
    """A clean low-poly copy of a body of revolution, like the concept art: its silhouette measured every millimetre
    of height (the mean reach of 32 rays, and how much wider it is across than front to back), cut down to the fewest
    rings that stay within `tol` of it, and turned `segments` times round the vertical axis with one face centred on
    the front (-y). Every face is flat. Nothing inside the old shells survives: only the outside is measured."""
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    polys, base = [], 0
    for o in objs:
        polys += [tuple(base + i for i in pl.vertices) for pl in o.data.polygons]
        base += len(o.data.vertices)
    tree = BVHTree.FromPolygons(pts, polys)
    z0, z1 = min(p.z for p in pts), max(p.z for p in pts)
    prof, z = [], z0 + 0.0005
    while z < z1 - 0.0004:
        rs = [reach(tree, z, 2 * math.pi * i / 32) or 0.0 for i in range(32)]
        r = sum(rs) / len(rs)
        if r > 1e-4:
            rx = (rs[0] + rs[16]) / 2
            ry = (rs[8] + rs[24]) / 2
            prof.append((r, z, rx / r, ry / r))
        z += 0.001
    line = [(0.0, z0)] + [(r, zz) for r, zz, _, _ in prof] + [(0.0, z1)]
    kept = douglas_peucker(line, tol)
    # The head: rings at even steps of latitude round its middle, like a UV sphere (the simplification alone cut it
    # unevenly, with slivers at the top). Its middle is fitted to the profile (centre on the axis); the rings sit on the
    # real profile, so the head keeps its own shape.
    upper = [q for q in prof if q[1] > z0 + 0.6 * (z1 - z0)]
    if upper:
        z_wide = max(upper, key=lambda q: q[0])[1]
        lower = [q for q in prof if z0 + 0.45 * (z1 - z0) <= q[1] <= z_wide]
        neck = min(lower, key=lambda q: q[0]) if lower else None
        if neck and neck[0] < 0.8 * max(q[0] for q in upper):
            z_neck = neck[1]
            hp = [(r, zz) for r, zz, _, _ in prof if zz >= z_neck]
            s11 = sum(4 * zz * zz for _, zz in hp); s12 = sum(2 * zz for _, zz in hp); s22 = len(hp)
            t1 = sum(2 * zz * (r * r + zz * zz) for r, zz in hp); t2 = sum(r * r + zz * zz for r, zz in hp)
            det = s11 * s22 - s12 * s12
            zc = (t1 * s22 - s12 * t2) / det
            phis = [(math.atan2(r, zz - zc), r, zz) for r, zz in hp]
            phi_neck = max(p for p, _, _ in phis)
            n = max(3, round(phi_neck / math.radians(15)))
            ordered = sorted(phis)
            head = []
            for k in range(n, 0, -1):
                t = phi_neck * k / n
                for (pa, ra, za), (pb, rb, zb) in zip(ordered, ordered[1:]):
                    if pa <= t <= pb:
                        f = (t - pa) / (pb - pa) if pb > pa else 0.0
                        head.append((ra + (rb - ra) * f, za + (zb - za) * f))
                        break
            head.sort(key=lambda q: q[1])
            print("HEADFIT z_neck %.4f r_neck %.4f zc %.4f phi_neck %.1f n %d first %s" % (z_neck, neck[0], zc, math.degrees(phi_neck), n, head[:2]))
            kept = [q for q in kept if q[1] < z_neck - 1e-4] + head + [(0.0, z1)]
    def stretch(zz):
        best = min(prof, key=lambda q: abs(q[1] - zz))
        return best[2], best[3]
    bm = bmesh.new()
    rings = []
    for r, zz in kept:
        if r < 1e-6:
            rings.append([bm.verts.new((0.0, 0.0, zz))])
            continue
        kx, ky = stretch(zz)
        ring = []
        for k in range(segments):
            a = -math.pi / 2 + (k + 0.5) * 2 * math.pi / segments
            ring.append(bm.verts.new((r * kx * math.cos(a), r * ky * math.sin(a), zz)))
        rings.append(ring)
    for lo, hi in zip(rings, rings[1:]):
        if len(lo) == 1:
            for k in range(segments):
                bm.faces.new((lo[0], hi[(k + 1) % segments], hi[k]))
        elif len(hi) == 1:
            for k in range(segments):
                bm.faces.new((lo[k], lo[(k + 1) % segments], hi[0]))
        else:
            for k in range(segments):
                bm.faces.new((lo[k], lo[(k + 1) % segments], hi[(k + 1) % segments], hi[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for pl in me.polygons:
        pl.use_smooth = False
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o, dict(rings=len(kept), segments=segments, faces=len(me.polygons), tolerance_mm=tol * 1000,
                   ring_heights=[round(zz, 4) for _, zz in kept])

def new_pawn(arm, M):
    H = M["H"]
    chain, body_rule = builder_rules(arm, H)
    info = {}

    # body: base bottom on the old skirt's bottom, head top on the old head top
    body = drop_hidden(import_parts(os.path.join(PARTS, "pawn.fbx")))
    body.sort(key=lambda o: -(bounds([o])[1] - bounds([o])[0]).length)
    mn, mx = bounds(body)
    base = [o.matrix_world @ v.co for o in body for v in o.data.vertices if (o.matrix_world @ v.co).z < mn.z + 0.03]
    bmn, bmx = bounds_pts(base)
    bc = Vector(((bmn.x + bmx.x) / 2, (bmn.y + bmx.y) / 2, mn.z))
    s_body = (M["top"] - M["skirt"]) / (mx.z - mn.z)
    for o in body:
        o.data.transform(Matrix.Translation(Vector((0, 0, M["skirt"]))) @ Matrix.Scale(s_body, 4) @ Matrix.Translation(-bc))
    head = max(body, key=lambda o: bounds([o])[1].z)
    # The collar is rings that only meet edge to edge: in the game the hairline seam between them showed the sky
    # through the collar as bright specks. Each lower ring is stretched up 2 mm over the foot of the one above it.
    rings = sorted([o for o in body if o is not head and o is not body[0]], key=lambda o: bounds([o])[0].z)
    for lower in rings[:-1]:
        zs = [v.co.z for v in lower.data.vertices]
        z0, z1 = min(zs), max(zs)
        k = (z1 - z0 + 0.002) / (z1 - z0)
        for v in lower.data.vertices:
            v.co.z = z0 + (v.co.z - z0) * k
        lower.data.update()
    info["collar_rings"] = [[round(bounds([o])[0].z, 4), round(bounds([o])[1].z, 4)] for o in rings]
    if M.get("old_tree") is not None:
        before, _ = profile_table(M["old_tree"], body)
        info["profile"] = match_profile(body, head, M["old_tree"])
        after, worst = profile_table(M["old_tree"], body)
        info["profile_worst_gap_m"] = round(worst, 4)
        with open(os.path.join(OUT, "profile_before_after.txt"), "w") as f:
            f.write("BEFORE (height | old +x -x +y -y | new ...)\n" + "\n".join(before) + "\n\nAFTER\n" + "\n".join(after))
    if LOWPOLY:
        # The matched body, rebuilt as one clean low-poly lathe; the Tripo shells (and the junk inside them) go.
        lathe, info["lowpoly"] = lathe_from(body)
        for o in body:
            bpy.data.objects.remove(o, do_unlink=True)
        # The body alone, unrigged, with a plain UV unwrap, to take into Tripo's texture generator.
        alone = dup(lathe)
        select_only([alone], alone)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
        bpy.ops.object.mode_set(mode="OBJECT")
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Pawn_LowPoly_Body.fbx"), use_selection=True,
                                 object_types={"MESH"}, mesh_smooth_type="FACE")
        bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "Pawn_LowPoly_Body.glb"), use_selection=True, export_format="GLB")
        bpy.data.objects.remove(alone, do_unlink=True)
        add_weights(lathe, lambda p, i: body_rule(p))     # the builder's own rule, as on the old one-piece pawn
        paint(lathe, SKIN)
        body, head = [lathe], lathe
        info["body"] = dict(scale=round(s_body, 4), base_bottom=round(M["skirt"], 4), top=round(M["top"], 4))
    for o in ([] if LOWPOLY else body):
        if o is head:
            add_weights(o, lambda p, i: {"Head": 1.0})
        elif o is body[0]:
            add_weights(o, lambda p, i: body_rule(p))     # the base: the builder's chain by height
        else:
            c = sum((o.matrix_world @ v.co for v in o.data.vertices), Vector()) / len(o.data.vertices)
            add_weights(o, lambda p, i, c=c: chain(c.z))  # the collar rings: whole, on the chest
        if o is head:
            paint(o, HEADC)
        elif o is body[0]:
            paint(o, SKIN)
        else:
            paint(o, SKIN)          # the collar is the body's colour (the old blue band was a first-version mistake)
    if not LOWPOLY:
        info["body"] = dict(scale=round(s_body, 4), base_bottom=round(M["skirt"], 4), top=round(M["top"], 4))
        # a wooden disc across the collar's top, on the chest, so a turning head never shows the hole under it
        collars = [o for o in body if o is not head and o is not body[0]]
        top = max(bounds([o])[1].z for o in collars)
        ring = [o.matrix_world @ v.co for o in collars for v in o.data.vertices if (o.matrix_world @ v.co).z > top - 0.004]
        inner = min(math.hypot(p.x, p.y) for p in ring)
        bm = bmesh.new()
        bmesh.ops.create_circle(bm, cap_ends=True, segments=32, radius=inner + 0.003)
        bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0, 0, top - 0.003)))
        me = bpy.data.meshes.new("CollarPlug")
        bm.to_mesh(me)
        bm.free()
        plug = bpy.data.objects.new("CollarPlug", me)
        bpy.context.scene.collection.objects.link(plug)
        add_weights(plug, lambda p, i: chain(top))
        paint(plug, SKIN)
        body.append(plug)

    # arms: the ball where the old ball is, as big; the stub as thick as the old stub, its root inside the body
    parts = drop_hidden(import_parts(os.path.join(PARTS, "arm.fbx")))
    stub = max(parts, key=lambda o: bounds([o])[1].y)
    ball = min(parts, key=lambda o: bounds([o])[0].y)
    cuffs = [o for o in parts if o is not stub and o is not ball]
    sv = [stub.matrix_world @ v.co for v in stub.data.vertices]
    axis_x = (min(p.x for p in sv) + max(p.x for p in sv)) / 2
    axis_z = (min(p.z for p in sv) + max(p.z for p in sv)) / 2
    stub_r_model = ((max(p.x for p in sv) - min(p.x for p in sv)) + (max(p.z for p in sv) - min(p.z for p in sv))) / 4
    bmn_, bmx_ = bounds([ball])
    ball_d_model = ((bmx_.x - bmn_.x) + (bmx_.y - bmn_.y) + (bmx_.z - bmn_.z)) / 3
    ball_c_model = (bmn_ + bmx_) / 2
    cuff_r_model = max(max(abs(p.x - axis_x), abs(p.z - axis_z)) for o in cuffs for p in (o.matrix_world @ v.co for v in o.data.vertices)) if cuffs else 0
    right = [dup(o) for o in parts]
    arms = []
    for side, s, objs in (("Left", 1, parts), ("Right", -1, right)):
        L = M[side]
        s_arm = L["ball_d"] / ball_d_model
        thick = L["stub_d"] / (2 * stub_r_model * s_arm)            # the stub as thick as the old one
        cuff_k = max(1.0, 1.15 * stub_r_model * thick / max(1e-6, cuff_r_model))   # the cuff stays just wider than it
        for o in objs:
            k = thick if o is objs[parts.index(stub)] else cuff_k if o in [objs[parts.index(c)] for c in cuffs] else 1.0
            # model: arm along y (ball at -y), axis at (axis_x, axis_z). Radial scale about the axis, then the arm
            # scale, the ball's centre to the origin, turned to point out along +x / -x, onto the old ball.
            radial = Matrix.Diagonal((k, 1, k, 1))
            m = (Matrix.Translation(L["ball_centre"]) @ Matrix.Rotation(math.radians(90 * s), 4, "Z")
                 @ Matrix.Scale(s_arm, 4) @ Matrix.Translation(Vector((0, -ball_c_model.y, 0)))
                 @ radial @ Matrix.Translation(Vector((-axis_x, 0, -axis_z))))
            o.data.transform(m)
            o.data.update()
        o_stub = objs[parts.index(stub)]
        # the root a centimetre deeper than the old stub's, capped
        cut = L["stub_in"] - 0.01
        bm = bmesh.new()
        bm.from_mesh(o_stub.data)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=Vector((s * cut, 0, 0)),
                               plane_no=Vector((s, 0, 0)), clear_inner=True)
        edges = [e for e in bm.edges if e.is_boundary]
        if edges:
            filled = bmesh.ops.holes_fill(bm, edges=edges, sides=0)
            bmesh.ops.triangulate(bm, faces=filled["faces"])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(o_stub.data)
        bm.free()
        for o in objs:
            if o is o_stub:
                add_weights(o, lambda p, i, side=side: {side + "Arm": 1.0})
                paint(o, SKIN)
            else:
                add_weights(o, lambda p, i, side=side: {side + "Hand": 1.0})
                paint(o, HANDC)
            fresh_normals(o)
        arms += objs
        info.setdefault("arms", {})[side] = dict(ball=[round(c, 4) for c in L["ball_centre"]], ball_d=round(L["ball_d"], 4),
                                                 stub_d=round(L["stub_d"], 4), root=round(s * cut, 4), scale=round(s_arm, 4),
                                                 stub_thicker=round(thick, 3), cuff_wider=round(cuff_k, 3))

    # feet: each where the old foot is, as wide, as long and as high
    foot = import_parts(os.path.join(PARTS, "foot.fbx"))[0]
    fv = [foot.matrix_world @ v.co for v in foot.data.vertices]
    ztop = max(p.z for p in fv)
    post = [p for p in fv if p.z > ztop - 0.12]
    pc = Vector((sum(p.x for p in post) / len(post), sum(p.y for p in post) / len(post), 0))
    post_r = max(math.hypot(p.x - pc.x, p.y - pc.y) for p in post)
    bulb_top = max(p.z for p in fv if math.hypot(p.x - pc.x, p.y - pc.y) > post_r + 0.02)
    bulb = [p for p in fv if p.z <= bulb_top]
    fmn, fmx = bounds_pts(bulb)
    fcen = Vector(((fmn.x + fmx.x) / 2, (fmn.y + fmx.y) / 2, 0))
    right_foot = dup(foot)
    right_foot.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
    right_foot.data.flip_normals()
    feet = []
    for side, s, o in (("Left", 1, foot), ("Right", -1, right_foot)):
        L = M[side]
        size = L["foot_size"]
        sx = size.x / (fmx.x - fmn.x)
        sy = size.y / (fmx.y - fmn.y)
        sz = (M["skirt"] - 0.002) / bulb_top          # the bulb's top just under the skirt, as the old feet
        c = Vector((s * fcen.x, fcen.y, 0))
        o.data.transform(Matrix.Translation(Vector((L["foot_centre"].x, L["foot_centre"].y, 0))) @ Matrix.Diagonal((sx, sy, sz, 1))
                         @ Matrix.Translation(-c))
        o.data.update()
        bt, tp = bulb_top * sz, ztop * sz
        add_weights(o, lambda p, i, side=side, bt=bt, tp=tp: {side + "Foot": 1.0} if p.z <= bt else
                    {side + "Foot": 1 - smooth((p.z - bt) / (tp - bt)), "Hips": smooth((p.z - bt) / (tp - bt))})
        paint(o, FOOTC)
        fresh_normals(o)
        feet.append(o)
        info.setdefault("feet", {})[side] = dict(centre=[round(L["foot_centre"].x, 4), round(L["foot_centre"].y, 4)],
                                                 size=[round(c, 4) for c in size], scale=[round(sx, 4), round(sy, 4), round(sz, 4)])

    face = []
    if M.get("face"):
        face = place_face(M["face"], M["old_tree"], head)
        info["face"] = {o.name: len(o.data.vertices) for o in face}
    objs = body + arms + feet + face
    select_only(objs, objs[0])
    bpy.ops.object.join()
    piece = bpy.context.view_layer.objects.active
    piece.name = "Pawn"
    mod = piece.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    piece.parent = arm
    piece.matrix_parent_inverse = arm.matrix_world.inverted()
    info["vertices"] = len(piece.data.vertices)
    info["triangles"] = sum(len(p.vertices) - 2 for p in piece.data.polygons)
    return piece, info

# ---------------------------------------------------------------- posing like the game (PoseAsPawn: bones follow bodies)
def rotate_about(arm, pb, pivot, R):
    Mw = arm.matrix_world
    pb.matrix = Mw.inverted() @ (Matrix.Translation(pivot) @ R @ Matrix.Translation(-pivot) @ (Mw @ pb.matrix))
    bpy.context.view_layer.update()

def reset(arm):
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

def deform_of(arm, b):
    pb = arm.pose.bones[P + b]
    return arm.matrix_world @ pb.matrix @ arm.data.bones[P + b].matrix_local.inverted() @ arm.matrix_world.inverted()

def pose(arm, M, chest=(0, 0, 0), head=(0, 0), arms=None, legs=None):
    reset(arm)
    pb = arm.pose.bones
    fwd, side, twist = chest
    for b in ("Spine", "Spine1", "Spine2"):
        piv = arm.matrix_world @ pb[P + b].head
        R = (Matrix.Rotation(math.radians(fwd / 3), 4, "X") @ Matrix.Rotation(math.radians(side / 3), 4, "Y")
             @ Matrix.Rotation(math.radians(twist / 3), 4, "Z"))
        rotate_about(arm, pb[P + b], piv, R)
    chest_m = deform_of(arm, "Spine2")
    yaw, nod = head
    neck = chest_m @ bone(arm, "Neck")                      # the Head body turns about its joint at the neck
    rotate_about(arm, pb[P + "Head"], neck, Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Rotation(math.radians(nod), 4, "X"))
    for s, target in (arms or {}).items():
        piv = chest_m @ M[s]["arm_pivot"]                   # the Arm body's joint, carried by the chest
        rest = chest_m.to_3x3() @ Vector((1 if s == "Left" else -1, 0, 0))
        want = chest_m.to_3x3() @ Vector(target).normalized()
        rotate_about(arm, pb[P + s + "Arm"], piv, rest.rotation_difference(want).to_matrix().to_4x4())
    for s, deg in (legs or {}).items():                     # the thigh swings about the hip, the foot with it
        hip = arm.matrix_world @ pb[P + s + "UpLeg"].head
        rotate_about(arm, pb[P + s + "UpLeg"], hip, Matrix.Rotation(math.radians(deg), 4, "X"))

POSES = [
    ("rest", dict()),
    ("run", dict(chest=(10, 0, 0), arms={"Left": (0.7, 0.7, -0.15), "Right": (-0.7, -0.7, -0.15)}, legs={"Left": -25, "Right": 25})),
    ("arms up", dict(arms={"Left": (0.3, -0.1, 0.95), "Right": (-0.3, -0.1, 0.95)})),
    ("arms forward", dict(chest=(8, 0, 0), arms={"Left": (0.2, -1, 0), "Right": (-0.2, -1, 0)})),
    ("arms 45 down", dict(arms={"Left": (0.7, 0, -0.7), "Right": (-0.7, 0, -0.7)})),
    ("head turn + nod", dict(head=(40, 20))),
    ("bend + twist", dict(chest=(15, 20, 20), arms={"Left": (0.6, -0.3, 0.75), "Right": (-0.9, 0.2, -0.4)})),
]

def shots(arm, M, tag, color_type):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = color_type
    scene.render.resolution_x = scene.render.resolution_y = 400
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.color = (1, 1, 1)
    cam = bpy.data.objects.get("cam")
    if cam is None:
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        scene.collection.objects.link(cam)
    scene.camera = cam
    centre = Vector((0, 0, 0.5))
    paths = []
    reset(arm)
    for view, d in (("front", Vector((0, -5, 0))), ("side", Vector((5, 0, 0)))):
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = 1.25
        cam.location = centre + d
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(OUT, "%s_rest_%s.png" % (tag, view))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    for k, (label, kw) in enumerate(POSES[1:], 1):
        pose(arm, M, **kw)
        cam.data.type = "PERSP"
        cam.data.lens = 50
        cam.location = centre + Vector((1.5, -2.3, 1.0))
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(OUT, "%s_pose%d.png" % (tag, k))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    reset(arm)
    return paths

def stitch(rows, out):
    import numpy as np
    imgs = []
    for row in rows:
        r = []
        for p in row:
            im = bpy.data.images.load(p)
            w, h = im.size
            r.append(np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4))
            bpy.data.images.remove(im)
        imgs.append(r)
    h, w = imgs[0][0].shape[:2]
    cols = max(len(r) for r in imgs)
    sheet = np.ones((len(imgs) * h, cols * w, 4), dtype=np.float32)
    for ri, r in enumerate(imgs):
        for ci, im in enumerate(r):
            y0 = (len(imgs) - 1 - ri) * h
            sheet[y0:y0 + h, ci * w:(ci + 1) * w] = im
    img = bpy.data.images.new("sheet", cols * w, len(imgs) * h, alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()

# ---------------------------------------------------------------- run
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm, old_mesh = load_old()
    ws, H, comps, kinds = old_pawn(arm, old_mesh)
    M = old_measures(ws, H, comps, kinds)
    M["old_tree"] = old_tree(ws, comps, old_mesh) if "--no-profile" not in sys.argv else None
    M["face"] = face_parts(ws, comps, kinds, old_mesh) if "--no-face" not in sys.argv else None
    print("OLD", json.dumps({k: (v if not isinstance(v, dict) else {kk: ([round(c, 4) for c in vv] if hasattr(vv, "__len__") else round(vv, 4)) for kk, vv in v.items()}) for k, v in M.items()}, default=str))
    old_paths = shots(arm, M, "old", "TEXTURE")

    # the new pawn on the same skeleton
    bpy.data.objects.remove(old_mesh, do_unlink=True)
    piece, info = new_pawn(arm, M)
    print("NEW", json.dumps(info))
    select_only([arm, piece], arm)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Pawn_Rigged.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, path_mode="COPY", embed_textures=True, use_armature_deform_only=False,
                             colors_type="SRGB")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Pawn_Rigged.blend"))
    new_paths = shots(arm, M, "new", "VERTEX")
    stitch([old_paths, new_paths], os.path.join(OUT, "pawn_old_vs_new.png"))
    with open(os.path.join(OUT, "pawn_report.json"), "w") as f:
        json.dump(dict(new=info), f, indent=1)
    print("DONE")

if __name__ == "__main__":
    main()
