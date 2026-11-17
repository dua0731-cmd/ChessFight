# blender -b --factory-startup --python qa/zfight_check.py -- [out.json]
# R686 (승규 11-17 "깜빡깜빡 거리는 곳 고쳐"): finds z-fighting - two faces looking the same way within a few mm of each
# other, so the depth test picks one or the other frame by frame. Each FBX in Resources/PawnRushCarriageYard is checked
# against itself (faces of different connected parts) and, for the map-frame zone models, against the blockout pieces
# the code still draws over them (Dressing.cs ZoneModels' keep lists, read from the R678 Play dump). Prints the places
# (Unity map coordinates) grouped by model and the pair of parts' areas.
import json, math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
RES = os.path.join(ROOT, "Assets/Resources/PawnRushCarriageYard")
DUMP = os.path.join(ROOT, "ArtSource/PawnRush/CarriageYardModels/guides/rcy_built_play.json")
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1 else None
GAP = float(os.environ.get("ZF_GAP", ".004"))   # faces closer than this, looking the same way, fight
MIN_AREA = 4e-4  # m2: smaller overlaps are not seen
MAP_FRAME = {"RcyEntrance", "RcyCanal", "RcySorting", "RcyPlatform", "RcyFinishCourt", "RcySectionFloors", "RcyScenery",
             "RcySceneryOwn", "RcyCastleOuterGate", "RcyCastleFinalGate", "RcyCastleSectionEnd"}
KEEP = {  # Dressing.cs ZoneModels: what the code still draws in each zone
    "RcyEntrance": ("1 Entrance", ["Shed lamp", "Charge line*"]),
    "RcyCanal": ("2 Canal", ["Water", "Flume water", "Pour", "Spill", "Foam", "Gap edge"]),
    "RcySorting": ("3-4 (own frame)/3 Loading and sorting yard",
                   ["Pool water", "Waterfall", "Foam", "Sluice water", "Sluice foam", "Sluice tunnel", "Hatch tunnel", "Lamp",
                    "Landing edge trim", "Landing trim", "Loft edge", "Loft lip"]),
}


def u2b(v):
    return Vector((v[0], v[2], v[1]))


def uq2b(q):
    from mathutils import Quaternion
    return Quaternion((q[3], -q[0], -q[2], -q[1]))


def kept(name, keep):
    return any(name.startswith(k[:-1]) if k.endswith("*") else name == k for k in keep)


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y")
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"]


MATS = []


def world_bm(objs):
    bm = bmesh.new()
    MATS.clear()
    for o in objs:
        me = o.data.copy()
        me.transform(o.matrix_world)
        names = [m.name.split(".")[0] if m else "?" for m in me.materials]
        for nm in names:
            if nm not in MATS:
                MATS.append(nm)
        t = bmesh.new()
        t.from_mesh(me)
        for f in t.faces:
            f.material_index = MATS.index(names[f.material_index]) if names else 0
        t.to_mesh(me)
        t.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    bm.faces.ensure_lookup_table()
    return bm


def islands(bm):
    lab = [-1] * len(bm.faces)
    k = 0
    for f0 in bm.faces:
        if lab[f0.index] >= 0:
            continue
        stack = [f0]
        lab[f0.index] = k
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g in e.link_faces:
                    if lab[g.index] < 0:
                        lab[g.index] = k
                        stack.append(g)
        k += 1
    return lab


def blockout_bm(group, keep):
    """The kept pieces the code draws (cubes and cylinders) as one bmesh, map frame."""
    with open(DUMP, encoding="utf-8") as f:
        nodes = json.load(f)["nodes"]
    bm = bmesh.new()
    names = []
    for n in nodes:
        if not n["path"].startswith(group + "/") or n.get("draw") != 1 or n["mesh"] not in ("Cube", "Cylinder"):
            continue
        if not kept(n["path"].split("/")[-1], keep):
            continue
        tmp = bmesh.new()
        if n["mesh"] == "Cube":
            bmesh.ops.create_cube(tmp, size=1.0)
        else:
            bmesh.ops.create_cone(tmp, cap_ends=True, segments=24, radius1=.5, radius2=.5, depth=2.0)
            bmesh.ops.rotate(tmp, verts=tmp.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
        s = u2b(n["scale"])
        m = Matrix.Translation(u2b(n["pos"])) @ uq2b(n["rot"]).to_matrix().to_4x4() @ Matrix.Diagonal(Vector((s.x, s.y, s.z, 1)))
        bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
        me = bpy.data.meshes.new("t")
        tmp.to_mesh(me)
        tmp.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        names.append(n["path"].split("/")[-1])
    bm.faces.ensure_lookup_table()
    return bm, names


def open_front(tree, f, n, reach=.5):
    c = f.calc_center_median()
    for p in [c] + [(c + v.co) / 2 for v in f.verts]:
        if tree.ray_cast(p + n * .01, n, reach)[0] is None:
            return True
    return False


def fights(bm, lab, other=None):
    """Faces of bm whose centre has, within GAP along its normal (either way), a face looking the same way that is of
    another part (other is None) or of `other`'s mesh."""
    target = other if other is not None else bm
    tree = BVHTree.FromBMesh(target)
    out = []
    for f in bm.faces:
        a = f.calc_area()
        if a < MIN_AREA or f.normal.z < -.5:   # facing down: resting on a floor or under a deck, never seen
            continue
        c, n = f.calc_center_median(), f.normal
        near = []   # (a ray missed exactly level faces; the middle alone missed partial overlaps)
        for p in [c] + [(c + v.co) / 2 for v in f.verts]:
            near += [hit[2] for hit in tree.find_nearest_range(p, GAP)]
        for gi in dict.fromkeys(near):
            g = target.faces[gi]
            if other is None and (g.index == f.index or lab[g.index] == lab[f.index]):
                continue
            if abs((g.verts[0].co - c).dot(n)) > GAP:
                continue
            if g.normal.dot(n) > .97:
                if open_front(tree, f, n):   # only faces that can be seen (the space in front is open)
                    out.append((c, a, f.index, g.index))
                break
    return out


def describe(bm, lab, other, fi, gi, objs_mats):
    """Material and part size of the two faces (for the report)."""
    def part(b, l, idx):
        if l is None:
            return "blockout"
        faces = [h for h in b.faces if l[h.index] == l[idx]]
        vs = {v for h in faces for v in h.verts}
        lo = Vector((min(v.co.x for v in vs), min(v.co.y for v in vs), min(v.co.z for v in vs)))
        hi = Vector((max(v.co.x for v in vs), max(v.co.y for v in vs), max(v.co.z for v in vs)))
        d = hi - lo
        return "%s %.2fx%.2fx%.2f" % (objs_mats[b.faces[idx].material_index] if b is bm else "", d.x, d.y, d.z)
    f = bm.faces[fi]
    return "%s | %s | n %.2f %.2f %.2f" % (part(bm, lab, fi), part(other if other is not None else bm, None if other is not None else lab, gi), f.normal.x, f.normal.z, f.normal.y)


def _unused():
    out = []
    return out


def place(c):
    return "(x %.1f, y %.1f, z %.1f)" % (c.x, c.z, c.y)   # Unity map frame


def cluster(hits, size=1.0):
    groups = {}
    for c, a, fi, gi in hits:
        key = (round(c.x / size), round(c.y / size), round(c.z / size))
        g = groups.setdefault(key, [Vector(), 0.0, 0])
        g[0] += c * a
        g[1] += a
        g[2] += 1
    return sorted(((g[0] / g[1], g[1], g[2]) for g in groups.values()), key=lambda t: -t[1])


report = {}
for fn in sorted(os.listdir(RES)):
    if not fn.endswith(".fbx"):
        continue
    name = fn[:-4]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = import_fbx(os.path.join(RES, fn))
    if not objs:
        continue
    bm = world_bm(objs)
    lab = islands(bm)
    rows = []
    mats = MATS
    raw = fights(bm, lab)
    if os.environ.get("ZF_DETAIL") and name in os.environ["ZF_DETAIL"].split(","):
        raw.sort(key=lambda t: -t[1])
        for c, a, fi, gi in raw[:int(os.environ.get("ZF_N", "25"))]:
            print("ZFD", name, place(c) if name in MAP_FRAME else c, round(a, 3), describe(bm, lab, None, fi, gi, mats))
    for c, area, n in cluster(raw):
        rows.append({"kind": "model-model", "at": place(c) if name in MAP_FRAME else "(local %.2f, %.2f, %.2f)" % (c.x, c.z, c.y), "area": round(area, 3), "faces": n})
    if name in KEEP:
        kb, knames = blockout_bm(*KEEP[name])
        if kb.faces:
            for c, area, n in cluster(fights(bm, lab, kb)):
                rows.append({"kind": "model-blockout", "at": place(c), "area": round(area, 3), "faces": n})
        kb.free()
    bm.free()
    total = sum(r["area"] for r in rows)
    report[name] = {"area": round(total, 3), "places": rows[:25]}
    print("ZF %-22s %3d places %7.3f m2" % (name, len(rows), total))
    for r in rows[:8]:
        print("ZF    ", r["kind"], r["at"], r["area"], "m2", r["faces"], "faces")
# Across models: every map-frame model at once (a zone's paving under another model's slab, the scenery's posts in a
# zone's coping), each FBX's parts their own islands.
bpy.ops.wm.read_factory_settings(use_empty=True)
objs, owner = [], []
for nm in sorted(MAP_FRAME):
    path = os.path.join(RES, nm + ".fbx")
    if os.path.exists(path):
        got = import_fbx(path)
        objs += got
        owner += [nm] * len(got)
bm = bmesh.new()
src = []
MATS.clear()
for o, nm in zip(objs, owner):
    me = o.data.copy()
    me.transform(o.matrix_world)
    n0 = len(bm.faces)
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    src += [nm] * (len(bm.faces) - n0)
bm.faces.ensure_lookup_table()
lab = islands(bm)
cross = []
for c, a, fi, gi in fights(bm, lab):
    if src[fi] != src[gi] and {src[fi], src[gi]} != {"RcyFinishCourt", "RcySectionFloors"}:   # never both (map / section)
        cross.append((c, a, fi, gi))
cross.sort(key=lambda t: -t[1])
for c, a, fi, gi in cross[:int(os.environ.get("ZF_N", "25"))]:
    print("ZFX", src[fi], "x", src[gi], place(c), round(a, 3))
report["_across"] = [{"a": src[fi], "b": src[gi], "at": place(c), "area": round(a, 3)} for c, a, fi, gi in cross]
print("ZFX total", round(sum(t[1] for t in cross), 3), "m2", len(cross), "faces")
if OUT:
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1, ensure_ascii=False)
print("ZF_DONE")
