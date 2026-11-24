# Flicker check for the section-corridor kits (2026-11-22, 승규 "깜빡깜빡 거리는 부분 다 고쳐").
# Assembles every kit in Assets/Resources/PawnRushCorridors as MiniGamePalaceArt.CorridorModels places it (both walls,
# piers, bays stretched to the real span, ceiling pieces at the layout's height, headers, portals, end wall, floor,
# ornaments) and lists faces of different pieces (mesh islands) that lie in one plane, face the same way and overlap by
# more than 1 cm2 - Unity flickers between them (PITFALLS 39). Also lists faces in the plane of the code paving (top
# 0.024) for kits that keep it.
#   blender -b --factory-startup --python cr13_flicker_check.py -- [CR01_Garden ...] [--verbose]
import bpy, bmesh, os, sys, glob, json, math
from collections import defaultdict
from mathutils import Vector, Matrix

ROOT = r"C:/Users/Makebot2/Documents/GitHub/ChessFight/Assets/Resources/PawnRushCorridors"
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
VERBOSE = "--verbose" in ARGS
ONLY = [a for a in ARGS if not a.startswith("--")]
PLANE_TOL, MIN_AREA = 1.2e-3, 1e-4

# The ends each kit meets (SectionCorridor): from, to, portal at start, portal at end, war-room end wall.
ENDS = {
    "CR01_Garden": (0.0, 9.2, True, False, False),
    "CR02_CarriageYard": (0.0, 9.2, True, False, False),
    "CR03_HallPorch": (.65, 10.0, False, False, False),
    "CR04_LowerRooms": (0.0, 9.2, True, False, False),
    "CR05_KingsAntechamber": (0.0, 9.2, True, False, False),
    "CR06_WarRoom": (.65, 10.0, False, True, True),
    "CR07_Library": (.65, 10.0, False, True, False),
    "CR08_Observatory": (.65, 10.0, False, True, False),
}


def load(path, cache={}):
    """The FBX's meshes as world-space bmesh triangles, cached (module-local frame)."""
    if path in cache:
        return cache[path]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    tris = []
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(o.matrix_world)
        mats = [m.name if m else "?" for m in o.data.materials]
        # islands: pieces that may flicker against each other
        island = {}
        n = 0
        for f in bm.faces:
            if f.index in island:
                continue
            stack = [f]
            island[f.index] = n
            while stack:
                g = stack.pop()
                for e in g.edges:
                    for h in e.link_faces:
                        if h.index not in island:
                            island[h.index] = n
                            stack.append(h)
            n += 1
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        # triangulate keeps islands but reindexes; recompute by connectivity again
        island = {}
        n = 0
        bm.faces.ensure_lookup_table()
        for f in bm.faces:
            if f.index in island:
                continue
            stack = [f]
            island[f.index] = n
            while stack:
                g = stack.pop()
                for e in g.edges:
                    for h in e.link_faces:
                        if h.index not in island:
                            island[h.index] = n
                            stack.append(h)
            n += 1
        for f in bm.faces:
            if f.calc_area() < 1e-6:
                continue
            tris.append(([v.co.copy() for v in f.verts], f.normal.copy(), o.name + "#" + str(island[f.index]),
                         mats[f.material_index] if f.material_index < len(mats) else "?"))
        bm.free()
    cache[path] = tris
    return tris


def placed(tris, at, turned, stretch, tag):
    """Corridor frame (Blender: X across, Y along, Z up): rotate 180 about Z when turned, scale Y, move."""
    m = Matrix.Translation(at) @ Matrix.Rotation(math.pi if turned else 0, 4, "Z") @ Matrix.Diagonal((1, stretch, 1, 1))
    r = Matrix.Rotation(math.pi if turned else 0, 3, "Z")
    out = []
    for vs, n, piece, mat in tris:
        nn = (r @ Matrix.Diagonal((1, 1 / stretch, 1)) @ n).normalized()
        out.append(([m @ v for v in vs], nn, tag + ":" + piece, mat))
    return out


def assemble(kit):
    folder = os.path.join(ROOT, kit)
    prefix = kit.split("_")[0]
    lay = json.load(open(os.path.join(folder, prefix + "_Layout.json")))
    mods = set(lay.get("modules", []))
    frm, to, p0, p1, endwall = ENDS[kit]
    span_auth = lay.get("bay_span_authored", 1.9)
    piers = [frm + .45 + (to - frm - .9) * i / 3 for i in range(4)]
    stretch = (piers[1] - piers[0] - .9) / span_auth
    ceil = 0.0 if lay.get("ceiling_absolute") else lay.get("height", 6.5)
    f = lambda name: os.path.join(folder, prefix + "_" + name + ".fbx")
    tris = []
    def add(name, at, turned=False, s=1.0, tag=None):
        path = f(name) if not name.endswith(".fbx") else os.path.join(folder, name)
        if os.path.exists(path):
            tris.extend(placed(load(path), Vector(at), turned, s, tag or name))
    walls = "Pier" in mods and ({"Bay", "BayWest", "BayEast"} & mods)
    if walls:
        for side in (-1, 1):
            for i, z in enumerate(piers):
                if "Portal" in mods and (i == 0 and p0 or i == 3 and p1):
                    continue   # the loader skips a pier a gate frame covers
                add("Pier", (side * 6, z, 0), side > 0, 1, "Pier%d%s" % (i, "WE"[side > 0]))
            for b in range(3):
                name = "BayMid" if b == 1 and "BayMid" in mods else ("BayWest" if side < 0 and "BayWest" in mods else
                       ("BayEast" if side > 0 and "BayEast" in mods else "Bay"))
                add(name, (side * 6, (piers[b] + piers[b + 1]) / 2, 0), side > 0, stretch, "Bay%d%s" % (b, "WE"[side > 0]))
    if "CeilingBay" in mods:
        for b in range(3):
            add("CeilingBay", (0, (piers[b] + piers[b + 1]) / 2, ceil), False, stretch, "Ceil%d" % b)
        for i, z in enumerate(piers):
            add("CeilingBeam", (0, z, ceil), False, 1, "Beam%d" % i)
    if "Header" in mods:
        if frm > 0:
            add("Header", (0, frm, ceil), False, 1, "HeaderIn")
        if to < 10:
            add("Header", (0, to, ceil), True, 1, "HeaderOut")
    if "Portal" in mods:
        if p0:
            add("Portal", (0, 0, 0), False, 1, "PortalIn")
        if p1:
            add("Portal", (0, 10, 0), True, 1, "PortalOut")
    if endwall and "EndWall" in mods:
        add("EndWall", (0, 10, 0), False, 1, "EndWall")
    for name in ("Floor", "FloorInlay"):
        if name in mods:
            add(name, (0, 0, 0), False, 1, name)
    for o in lay.get("ornaments", []):
        off = o.get("offset", [0, 0, 0])
        per_bay = o.get("per") == "bay"
        for i in o.get("index", []):
            z = (piers[i] + piers[i + 1]) / 2 if per_bay else piers[i]
            path = o["name"] + ".fbx"
            if not o.get("mirror", True):
                add(path, (off[0], z + off[2], off[1]), False, 1, "%s%d" % (o["name"], i))
            else:
                add(path, (-6 + off[0], z + off[2], off[1]), False, 1, "%s%dW" % (o["name"], i))
                add(path, (6 - off[0], z - off[2], off[1]), True, 1, "%s%dE" % (o["name"], i))
    if "Floor" not in mods:
        # the code paving's tops (1.97 m slabs at +0.024) - faces of the kit lying on that plane would flicker with it
        for x in range(6):
            for zz in range(5):
                x0, x1 = x * 2 - 5 - .985, x * 2 - 5 + .985
                y0, y1 = zz * 2 + .015, zz * 2 + 1.985
                for t in (((x0, y0), (x1, y0), (x1, y1)), ((x0, y0), (x1, y1), (x0, y1))):
                    tris.append(([Vector((a, b, .024)) for a, b in t], Vector((0, 0, 1)), "CODE:paving", "PavingCode"))
    return tris


def clip_area(a, b):
    def area(p):
        return .5 * sum(p[i][0] * p[(i + 1) % len(p)][1] - p[(i + 1) % len(p)][0] * p[i][1] for i in range(len(p)))
    if area(a) < 0:
        a = a[::-1]
    if area(b) < 0:
        b = b[::-1]
    out = list(a)
    for i in range(len(b)):
        c, d = b[i], b[(i + 1) % len(b)]
        inside = lambda p: (d[0] - c[0]) * (p[1] - c[1]) - (d[1] - c[1]) * (p[0] - c[0]) >= -1e-9
        inp, out = out, []
        for j in range(len(inp)):
            p, q = inp[j], inp[(j + 1) % len(inp)]
            if inside(q):
                if not inside(p):
                    out.append(cut(p, q, c, d))
                out.append(q)
            elif inside(p):
                out.append(cut(p, q, c, d))
        if not out:
            return 0.0
    return abs(area(out))


def cut(p, q, c, d):
    x1, y1, x2, y2 = p[0], p[1], q[0], q[1]
    x3, y3, x4, y4 = c[0], c[1], d[0], d[1]
    den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
    if abs(den) < 1e-12:
        return q
    t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den
    return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))


def visible(bvh, p, n):
    """Is the face at p (normal n) seen from anywhere? Not when the first surface in front of it is the back of
    another piece (it lies inside that piece) or a surface a hair away (it is covered)."""
    if n.z < -.9 and p.z < .05:  # an underside on the ground
        return False
    start = p + n * .003
    hit = bvh.ray_cast(start, n, 50.0)
    if hit[0] is None:
        return True
    loc, hn, idx, dist = hit
    if hn.dot(n) > 0:          # we are inside a closed piece
        return False
    return dist > .25          # a face looking into a gap narrower than a camera fits in is hidden too


def check(kit):
    from mathutils.bvhtree import BVHTree
    tris = assemble(kit)
    verts, polys = [], []
    for vs, n, piece, mat in tris:
        base = len(verts)
        verts.extend(vs)
        polys.append((base, base + 1, base + 2))
    bvh = BVHTree.FromPolygons(verts, polys, epsilon=0.0)
    buckets = defaultdict(list)
    for t in tris:
        vs, n, piece, mat = t
        if n.length < .5:
            continue
        nq = (round(n.x, 2), round(n.y, 2), round(n.z, 2))
        d = n.dot(vs[0])
        buckets[nq].append((d, t))
    pairs = defaultdict(float)
    where = {}
    for nq, items in buckets.items():
        items.sort(key=lambda it: it[0])
        n = Vector(nq).normalized()
        u = n.orthogonal().normalized()
        v = n.cross(u)
        proj = lambda p: (p.dot(u), p.dot(v))
        for i in range(len(items)):
            di, (vi, ni, pi, mi) = items[i]
            bi = None
            for j in range(i + 1, len(items)):
                dj, (vj, nj, pj, mj) = items[j]
                if dj - di > PLANE_TOL:
                    break
                if pi == pj:
                    continue
                if bi is None:
                    bi = [proj(p) for p in vi]
                    bx0, bx1 = min(p[0] for p in bi), max(p[0] for p in bi)
                    by0, by1 = min(p[1] for p in bi), max(p[1] for p in bi)
                bj = [proj(p) for p in vj]
                if max(p[0] for p in bj) < bx0 or min(p[0] for p in bj) > bx1 or max(p[1] for p in bj) < by0 or min(p[1] for p in bj) > by1:
                    continue
                a = clip_area(bi, bj)
                if a > MIN_AREA * .2 and (visible(bvh, sum(vi, Vector()) / 3, ni) or visible(bvh, sum(vj, Vector()) / 3, nj)):
                    key = tuple(sorted(((pi, mi), (pj, mj))))
                    pairs[key] += a
                    where.setdefault(key, sum(vi, Vector()) / 3)
                    if VERBOSE and a > .02:
                        print("  V %s %s n=%s  %s | %s  tri_i=%s" % (pi, mi, tuple(round(c, 3) for c in ni), pj, mj, [tuple(round(c, 3) for c in p) for p in vi]))
    hits = [(k, a) for k, a in pairs.items() if a > MIN_AREA]
    print("FLICKER %-24s %d pairs" % (kit, len(hits)))
    # group by module pair names without instance numbers, for a readable list
    groups = defaultdict(lambda: [0, 0.0, None])
    for (a, b), area in hits:
        strip = lambda s: "".join(ch for ch in s[0].split(":")[0] if not ch.isdigit()) + ":" + s[0].split(":", 1)[1].split("#")[0] + "[" + s[1] + "]"
        g = tuple(sorted((strip(a), strip(b))))
        groups[g][0] += 1
        groups[g][1] += area
        groups[g][2] = where[(a, b)]
    for g, (cnt, area, at) in sorted(groups.items(), key=lambda kv: -kv[1][1]):
        print("   %4d x %7.3f m2  %-50s | %-50s at (%.2f, %.2f, %.2f)" % (cnt, area, g[0][:50], g[1][:50], at.x, at.y, at.z))


for kit in sorted(ENDS):
    if ONLY and kit not in ONLY:
        continue
    if not os.path.exists(os.path.join(ROOT, kit)):
        continue
    check(kit)
