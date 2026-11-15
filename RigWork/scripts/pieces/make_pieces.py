# Low-poly chess pieces built from the design images, at the game's scale (the pawn: skirt at 0.12 m on its feet,
# head top at 1.0 m), standing on the axis, facing -y. Each body is a lathe of the design's front silhouette
# (measured row by row), cut to the fewest rings that stay within TOL of it, with flat faces, plus what a lathe
# cannot make: the bishop's split mitre, the rook's merlons, the knight's head (the user's Tripo knight, simplified).
# blender -b -P make_pieces.py -- <out dir> <piece> [<piece> ...]       pieces: bishop rook knight
import bpy, bmesh, sys, os, math, json
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

a = sys.argv[sys.argv.index("--") + 1:]
OUT, PIECES = a[0], a[1:]
os.makedirs(OUT, exist_ok=True)
DL = "C:/Users/trews/Downloads/"
REF = "C:/Users/trews/.codex/visualizations/2026/11/09/01a1bf32-96d3-7940-bc68-578364f54aa6/ChessFight/FinalMultiview/WhiteBlack_WithFace_BodyOnly_v1/"
FRONT = {"bishop": DL + "ChatGPT 이미지 2026년 11월 9일 오후 10_39_12.png",
         "rook": REF + "white_09_rook_front_noface.png",
         "knight": REF + "white_05_knight_front_noface.png"}
# Heights from the feet's top (0.12 m) to the top, as the lineup has them against the pawn (0.88 m)
HEIGHT = {"bishop": 1.508 * 0.88, "rook": 1.150 * 0.88, "knight": 1.332 * 0.88}
Z0 = 0.12
TOL = 0.0016

def load(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    x = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]     # rows from the top
    bpy.data.images.remove(im)
    return x

def mask_of(x):
    lum = x @ np.array([0.2126, 0.7152, 0.0722])
    return (x[..., 0] - x[..., 2] > 0.05) & (lum < 0.985) & (lum > 0.25)

def front_profile(path, height):
    """(r, z) from bottom to top, one per image row: the silhouette's half width about its own axis, scaled so the
    whole piece is `height` tall standing at Z0."""
    m = mask_of(load(path))
    rows = np.nonzero(m.any(axis=1))[0]
    top, bot = rows.min(), rows.max()
    # the floor's soft shadow can bleed a few rows under the base: stop where the width jumps out
    lr = {}
    for r in range(top, bot + 1):
        cols = np.nonzero(m[r])[0]
        if len(cols):
            lr[r] = (cols.min(), cols.max())
    mid = np.median([(l + rr) / 2 for r, (l, rr) in lr.items() if r > top + 0.5 * (bot - top)])
    s = height / (bot - top)
    prof = []
    for r in range(bot, top - 1, -1):
        if r not in lr:
            continue
        l, rr = lr[r]
        half = max(mid - l, rr - mid)
        prof.append((half * s, Z0 + (bot - r) * s))
    return prof, s, (top, bot, mid)

def douglas_peucker(pts, tol):
    keep = [False] * len(pts)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        i0, i1 = stack.pop()
        (ax, ay), (bx, by) = pts[i0], pts[i1]
        dx, dy = bx - ax, by - ay
        L = math.hypot(dx, dy) or 1e-12
        far, at = 0.0, -1
        for i in range(i0 + 1, i1):
            px, py = pts[i]
            d = abs((px - ax) * dy - (py - ay) * dx) / L
            if d > far:
                far, at = d, i
        if far > tol:
            keep[at] = True
            stack += [(i0, at), (at, i1)]
    return [p for p, k in zip(pts, keep) if k]

def lathe(prof, segments, name, tol=TOL, cap_bottom=True, cap_top=True):
    """Rings at the simplified profile's points, `segments` around, a face centred on the front (-y), flat faces."""
    line = ([(0.0, prof[0][1])] if cap_bottom else []) + prof + ([(0.0, prof[-1][1])] if cap_top else [])
    kept = douglas_peucker(line, tol)
    bm = bmesh.new()
    rings = []
    for r, z in kept:
        if r < 1e-5:
            rings.append([bm.verts.new((0, 0, z))])
            continue
        rings.append([bm.verts.new((r * math.cos(-math.pi / 2 + (k + 0.5) * 2 * math.pi / segments),
                                    r * math.sin(-math.pi / 2 + (k + 0.5) * 2 * math.pi / segments), z))
                      for k in range(segments)])
    for lo, hi in zip(rings, rings[1:]):
        if len(lo) == 1 and len(hi) == 1:
            continue
        for k in range(segments):
            k1 = (k + 1) % segments
            if len(lo) == 1:
                bm.faces.new((lo[0], hi[k1], hi[k]))
            elif len(hi) == 1:
                bm.faces.new((lo[k], lo[k1], hi[0]))
            else:
                bm.faces.new((lo[k], lo[k1], hi[k1], hi[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    flat(o)
    return o, [round(z, 4) for _, z in kept]

def flat(o):
    for p in o.data.polygons:
        p.use_smooth = False

def cut_keep(o, co, no):
    """Keep the part of o on the side `no` points away from (bisect, drop the rest, cap the cut flat)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=co,
                                 plane_no=no, clear_outer=True)
    edges = [e for e in bm.edges if e.is_boundary]
    if edges:
        filled = bmesh.ops.holes_fill(bm, edges=edges, sides=0)
        bmesh.ops.triangulate(bm, faces=filled["faces"])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    flat(o)

def dup(o, name):
    c = o.copy()
    c.data = o.data.copy()
    c.name = name
    bpy.context.scene.collection.objects.link(c)
    return c

def split_at(o, z, name_top):
    """o below z (capped), and a new object above z (capped)."""
    top = dup(o, name_top)
    cut_keep(o, Vector((0, 0, z)), Vector((0, 0, 1)))     # keep below
    cut_keep(top, Vector((0, 0, z)), Vector((0, 0, -1)))  # keep above
    return o, top

# ------------------------------------------------------------------ pieces
def bishop():
    prof, s, (top, bot, mid) = front_profile(FRONT["bishop"], HEIGHT["bishop"])
    H = HEIGHT["bishop"]
    zs = [z for _, z in prof]
    rs = [r for r, _ in prof]
    # the mitre: above the flange. The flange is the widest disc in the upper half; the mitre starts where the
    # profile is narrowest just above it.
    upper = [i for i, z in enumerate(zs) if z > Z0 + 0.45 * H]
    sm = np.convolve(np.array(rs), np.ones(5) / 5, mode="same")
    def is_max(i):
        return all(sm[i] >= sm[j] for j in range(max(0, i - 4), min(len(sm), i + 5)))
    def is_min(i):
        return all(sm[i] <= sm[j] for j in range(max(0, i - 4), min(len(sm), i + 5)))
    # going up from the cone: the first wide disc (the flange), then the first narrowing above it (the mitre's foot)
    start = next(i for i, z in enumerate(zs) if z > Z0 + 0.40 * H)
    fl = next(i for i in range(start, len(zs)) if is_max(i) and sm[i] > 0.6 * max(rs))
    mstart = next(i for i in range(fl + 2, len(zs)) if is_min(i))
    z_m0 = zs[mstart]
    mitre = [i for i in upper if zs[i] > z_m0]
    widest = max(mitre, key=lambda i: rs[i] if zs[i] < Z0 + 0.92 * H else -1)
    R_m = rs[widest]
    # the tip: where the teardrop narrows to its point under the ball (the narrowest point above the widest)
    tipi = min([i for i in mitre if zs[i] > zs[widest] and zs[i] < Z0 + 0.985 * H], key=lambda i: rs[i])
    z_tip = zs[tipi]
    # below the mitre: one lathe, capped at the mitre's foot
    lower, rings = lathe([(r, z) for r, z in prof if z <= z_m0 + 0.002], 16, "Bishop")
    # The mitre, as the design draws it: two leaves from one foot, not one teardrop cut in two. The front leaf is the
    # whole teardrop (with the ball) seen from the front, a little shallower front to back; the back leaf is a smaller
    # teardrop (0.86 as tall, 0.72 as wide, half as deep) standing behind it and leaning back 7 degrees, hidden from the
    # front, a narrow V between the two from the side.
    mp = [(r, z) for r, z in prof if z >= z_m0]
    main, mrings = lathe(mp, 16, "MitreFront")
    main.data.transform(Matrix.Translation(Vector((0, -0.06 * R_m, 0))) @ Matrix.Diagonal((1.0, 0.80, 1.0, 1.0)))
    leaf, _ = lathe([(r, z) for r, z in mp if z <= z_tip], 16, "MitreBack")
    foot = Vector((0, 0, z_m0))
    leaf.data.transform(Matrix.Translation(foot + Vector((0, 0.50 * R_m, 0))) @ Matrix.Rotation(math.radians(-7), 4, "X")
                        @ Matrix.Diagonal((0.72, 0.52, 0.86, 1.0)) @ Matrix.Translation(-foot))
    flat(main)
    flat(leaf)
    info = dict(scale_px=s, mitre_foot=round(z_m0, 4), mitre_widest_r=round(R_m, 4), tip=round(z_tip, 4), rings=rings + mrings)
    return [lower, main, leaf], info

def rook():
    prof, s, (top, bot, mid) = front_profile(FRONT["rook"], HEIGHT["rook"])
    H = HEIGHT["rook"]
    img = mask_of(load(FRONT["rook"]))
    # the merlons' floor: in a notch column (45 degrees round, 0.62 of the top radius out) the first body pixel from
    # the top lies below the merlons' tops
    zs = [z for _, z in prof]
    rs = [r for r, _ in prof]
    r_top = max(r for r, z in prof if z > Z0 + 0.85 * H)
    z_top = max(zs)
    # the merlons stand 7.5 percent of the rook's height above the notches' floor (measured on the front view)
    z_floor = z_top - 0.075 * H
    body_prof = [(r, z) for r, z in prof if z < z_floor]
    body, rings = lathe(body_prof, 24, "Rook")
    # the crown's rim radius at the floor, and four merlons (front, sides, back), 60 degrees each, notches 30
    r_rim = max(r for r, z in prof if z_floor - 0.03 < z <= z_floor)
    thick = 0.22 * r_rim
    merl = []
    bm = bmesh.new()
    for m in range(4):
        c = -math.pi / 2 + m * math.pi / 2
        n = 4
        angs = [c - math.radians(30) + k * math.radians(60) / n for k in range(n + 1)]
        def ring(rad, z):
            return [bm.verts.new((rad * math.cos(t), rad * math.sin(t), z)) for t in angs]
        ob, ot = ring(r_rim, z_floor - 0.002), ring(r_rim, z_top)
        ib, it = ring(r_rim - thick, z_floor - 0.002), ring(r_rim - thick, z_top)
        for k in range(n):
            bm.faces.new((ob[k], ob[k + 1], ot[k + 1], ot[k]))       # outside
            bm.faces.new((ib[k + 1], ib[k], it[k], it[k + 1]))       # inside
            bm.faces.new((ot[k], ot[k + 1], it[k + 1], it[k]))       # top
            bm.faces.new((ob[k + 1], ob[k], ib[k], ib[k + 1]))       # bottom
        bm.faces.new((ob[0], ot[0], it[0], ib[0]))                   # ends
        bm.faces.new((ot[n], ob[n], ib[n], it[n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("Merlons")
    bm.to_mesh(me)
    bm.free()
    mo = bpy.data.objects.new("Merlons", me)
    bpy.context.scene.collection.objects.link(mo)
    flat(mo)
    return [body, mo], dict(scale_px=s, floor=round(z_floor, 4), top=round(z_top, 4), rim=round(r_rim, 4), rings=rings)

def knight():
    prof, s, (top, bot, mid) = front_profile(FRONT["knight"], HEIGHT["knight"])
    H = HEIGHT["knight"]
    zs = [z for _, z in prof]
    rs = [r for r, _ in prof]
    # the base: up to where the profile, rising from the skirt, first becomes the neck (narrowest in the lower half)
    # the base is the rings at the bottom: 23 percent of the knight's height (front view); the neck above it is the
    # Tripo model's own S curve
    z_base = Z0 + 0.23 * H
    base, rings = lathe([(r, z) for r, z in prof if z <= z_base], 16, "KnightBase")
    # the head: the user's Tripo knight, scaled to the same height, its own base cut away just inside ours
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=DL + "knight.fbx")
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in meshes:
        o.parent = None
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    head = bpy.context.view_layer.objects.active
    head.name = "KnightHead"
    vs = [v.co for v in head.data.vertices]
    mn = Vector((min(p.x for p in vs), min(p.y for p in vs), min(p.z for p in vs)))
    mx = Vector((max(p.x for p in vs), max(p.y for p in vs), max(p.z for p in vs)))
    k = H / (mx.z - mn.z)
    cx, cy = (mn.x + mx.x) / 2, (mn.y + mx.y) / 2
    # the base's centre (its lowest 5 percent) is the axis
    lowp = [p for p in vs if p.z < mn.z + 0.05 * (mx.z - mn.z)]
    bx = (min(p.x for p in lowp) + max(p.x for p in lowp)) / 2
    by = (min(p.y for p in lowp) + max(p.y for p in lowp)) / 2
    head.data.transform(Matrix.Translation(Vector((0, 0, Z0))) @ Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-bx, -by, -mn.z))))
    # face -y: the muzzle is where the upper fifth leans out furthest from the axis
    vs = [v.co for v in head.data.vertices]
    upp = [p for p in vs if p.z > Z0 + 0.6 * H]
    ax_ = sum(p.x for p in upp) / len(upp)
    ay_ = sum(p.y for p in upp) / len(upp)
    ang = math.atan2(ay_, ax_)
    head.data.transform(Matrix.Rotation(-math.pi / 2 - ang, 4, "Z"))
    cut_keep(head, Vector((0, 0, z_base - 0.02)), Vector((0, 0, -1)))
    # simplify to a low-poly sculpt: voxel remesh (clean, closed), then collapse to about 1600 faces, flat
    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head
    rm = head.modifiers.new("remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = 0.006
    bpy.ops.object.modifier_apply(modifier="remesh")
    dm = head.modifiers.new("dec", "DECIMATE")
    dm.decimate_type = "COLLAPSE"
    dm.ratio = min(1.0, 1400.0 / max(1, len(head.data.polygons)))
    bpy.ops.object.modifier_apply(modifier="dec")
    flat(head)
    return [base, head], dict(scale_px=s, base_top=round(z_base, 4), head_faces=len(head.data.polygons),
                              facing_turn_deg=round(math.degrees(-math.pi / 2 - ang), 1), rings=rings)

# ------------------------------------------------------------------ run
report = {}
for name in PIECES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts, info = {"bishop": bishop, "rook": rook, "knight": knight}[name]()
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    piece = bpy.context.view_layer.objects.active
    piece.name = name.capitalize()
    flat(piece)
    info["faces"] = len(piece.data.polygons)
    vs = [v.co for v in piece.data.vertices]
    info["bounds"] = [[round(min(p[i] for p in vs), 4) for i in range(3)], [round(max(p[i] for p in vs), 4) for i in range(3)]]
    # radius at the arms' height (the pawn's hand balls at z 0.508; the arm stubs' roots sit 0.104 m out)
    t = BVHTree.FromPolygons([v.co for v in piece.data.vertices], [tuple(p.vertices) for p in piece.data.polygons])
    for zz in (0.47, 0.508, 0.55):
        hits = []
        for ang in (0, math.pi):
            d = Vector((math.cos(ang), 0, 0))
            hh = t.ray_cast(Vector((0, 0, zz)) + d, -d)
            hits.append(round((hh[0] - Vector((0, 0, zz))).length, 4) if hh[0] else None)
        info["radius_at_%.3f" % zz] = hits
    report[name] = info
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name.capitalize() + "_LowPoly.blend"))
    print("PIECE", name, json.dumps(info))
with open(os.path.join(OUT, "pieces_report.json"), "w") as f:
    json.dump(report, f, indent=1)
print("DONE")
