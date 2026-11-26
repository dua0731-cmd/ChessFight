"""Banquet hall: lighter meshes and LODs (bh-hall session, seoungtest R838, 2026-11-25; the user: "모양이 변하지 않는
곳까지 경량화 ... 대연회장 LOD 할 곳 있으면 해").

    blender -b --factory-startup --python bh_lod.py -- --fbx B1/bh_counter [--write]
    blender -b --factory-startup --python bh_lod.py -- --part b5:pelmet

Every mesh goes through the same steps:
  1. LOD0 - as light as it gets with the shape unchanged: coplanar faces dissolved (material, UV seam and sharp edges
     kept), then an edge-collapse decimation at the lowest ratio whose surface stays within TOL0 of the original both
     ways (the decimated surface from the original's points and the original from the decimated points; every point
     checked, the 99.5th percentile within the tolerance and nothing past 1.5 times it, so no small relief is lost);
  2. LOD1 and LOD2 - the same from the level before, within TOL1 / TOL2 (a 20000-point sample, nothing past three
     times the tolerance), kept only where they save a third;
  3. every level is shaded smooth with edges over 30 degrees hard (the collapse would smear the original's custom
     normals over the new triangles), so flat faces read flat and turned shapes round, as before.
Tolerances are in metres, smaller for small things (a share of the object's diagonal).

--fbx: a model bh-path exported (Assets/Resources/BanquetHall/Models/<zone>/<model>.fbx). Imported as bh_kit.export
wrote it (-Z forward, Y up), judged, pictured; with --write the original is copied to lod/orig/ (outside Assets, not in
git) and the FBX is written again with each mesh as "<name>_LOD0/1/2" (Unity's importer makes the LODGroup from the
names; a mesh with no lighter level repeats its last one, so nothing drops out of a level). Unity must not be in Play.
--part: one of this session's parts (bh_b7_hall_model / bh_b6_* / bh_b5_stage_model PARTS): judged and pictured, the
levels saved to lod/parts/<zone>_<part>.blend for the export later.

Pictures: stills/lod/<name>.png - columns original, LOD0, LOD1, LOD2 (the same camera), the whole above, a close look
below; numbers in stills/lod/report.json.
"""
import json, math, os, shutil, sys
import bpy, bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
MODELS = os.path.join(ROOT, "Assets", "Resources", "BanquetHall", "Models")
STILLS = os.path.join(HERE, "stills", "lod")
REPORT = os.path.join(STILLS, "report.json")
TOL = (.002, .012, .04)            # LOD0 / LOD1 / LOD2: how far the surface may move (m) ...
TOL_SHARE = (.003, .015, .04)      # ... and at most this share of the object's diagonal
KEEP_LEVEL = .67                   # a lighter level is kept only if it has at most this share of the last one's tris
SMALL = 200                        # meshes under this many tris are left as they are


# ------------------------------------------------------------------ meshes

def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def world_points(me, mw, faces=True):
    pts = [mw @ v.co for v in me.vertices]
    if faces:
        pts += [mw @ p.center for p in me.polygons]
    return pts


def bvh(me, mw):
    return BVHTree.FromPolygons([mw @ v.co for v in me.vertices], [tuple(p.vertices) for p in me.polygons])


def deviation(me_a, me_b, mw, cap=20000):
    """How far each surface strays from the other (world metres): (99.5th percentile, max) over both directions."""
    ta, tb = bvh(me_a, mw), bvh(me_b, mw)
    out = []
    for pts, tree in ((world_points(me_b, mw), ta), (world_points(me_a, mw), tb)):
        if len(pts) > cap:
            idx = np.random.default_rng(7).choice(len(pts), cap, replace=False)
            pts = [pts[i] for i in idx]
        d = []
        for p in pts:
            hit = tree.find_nearest(p)
            d.append((hit[0] - p).length if hit[0] is not None else 1e9)
        out.extend(d)
    if not out:
        return 0.0, 0.0
    a = np.array(out)
    return float(np.percentile(a, 99.5)), float(a.max())


def mesh_copy(me, name):
    c = me.copy()
    c.name = name
    return c


def dissolve_planar(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(.5), use_dissolve_boundaries=False, verts=bm.verts[:],
                             edges=bm.edges[:], delimit={"MATERIAL", "SEAM", "SHARP", "UV"})
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    out = bpy.data.meshes.new(me.name + " planar")
    bm.to_mesh(out)
    bm.free()
    for m in me.materials:
        out.materials.append(m)
    return out


def collapse(me, ratio):
    """An edge-collapse decimation of `me` to `ratio` (a new mesh)."""
    tmp = bpy.data.objects.new("bh_lod tmp", me)
    bpy.context.scene.collection.objects.link(tmp)
    mod = tmp.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    return out


def lightest(me, mw, tol, strict=False):
    """The lowest collapse ratio whose surface stays within tol of `me` (a binary search on a log scale)."""
    lo, hi, best = math.log(.01), 0.0, None
    for _ in range(8):
        mid = (lo + hi) / 2
        cand = collapse(me, math.exp(mid))
        # LOD0 (strict): every point checked and none past 1.5 x tol - a percentile over a sample let small reliefs go
        # (11-25, the great chandelier's crown plaques); the lighter levels may lose such things
        p, mx = deviation(me, cand, mw, cap=10 ** 9 if strict else 20000)
        if p <= tol and mx <= (1.5 if strict else 3) * tol:
            if best is not None:
                bpy.data.meshes.remove(best)
            best, hi = cand, mid
        else:
            bpy.data.meshes.remove(cand)
            lo = mid
    return best


def shade_like(me, source, mw):
    """Shading for a decimated level (in place, returned): the collapse blends the original's custom normals across
    the new big triangles and smears the light over flat faces (11-25, the counter's stair treads), so they are
    cleared; the faces are smooth with every edge turning more than 30 degrees hard and the normals weighted by face
    area - flat faces read flat, turned shapes round, as the original's bevel-hardened normals did."""
    if "custom_normal" in me.attributes:
        me.attributes.remove(me.attributes["custom_normal"])
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(30))
    # and a face-area weighting (the bevel's "harden normals"): a big flat face keeps its own normal where a thin
    # bevel strip meets it under 30 degrees, instead of a gradient over the whole face (11-25, the table's top)
    ob = bpy.data.objects.new("bh_lod wn", me)
    bpy.context.scene.collection.objects.link(ob)
    wn = ob.modifiers.new("wn", "WEIGHTED_NORMAL")
    wn.mode = "FACE_AREA"
    wn.keep_sharp = True
    wn.weight = 100
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    return out


def levels_for(me, mw, name):
    """[LOD0, LOD1, LOD2] meshes (a level may repeat the last) and their numbers."""
    pts = world_points(me, mw, faces=False)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    diag = (hi - lo).length
    t0 = tri_count(me)
    info = {"tris": [t0], "tol": [], "dev": []}
    if t0 < SMALL:
        return [me, me, me], info, diag
    out = []
    base = dissolve_planar(me)
    prev = base
    for k in range(3):
        tol = min(TOL[k], TOL_SHARE[k] * diag)
        cand = lightest(prev, mw, tol, strict=(k == 0))
        if cand is None or (k > 0 and tri_count(cand) > KEEP_LEVEL * tri_count(prev)):
            if cand is not None:
                bpy.data.meshes.remove(cand)
            cand = prev if k == 0 else None
        if k == 0 and tri_count(cand) > .75 * t0:
            # less than a quarter to take off without changing the shape: LOD0 is the original itself, its own
            # (bevel-hardened) shading untouched - the new shading would cost more than the few triangles save
            # (11-25, the great chandelier's crown plaques read softer); the lighter levels start from the light one
            prev = cand
            out.append(me)
            info["tris"].append(t0)
            info["tol"].append(round(tol, 4))
            info["dev"].append(0.0)
            continue
        if cand is None:
            out.append(out[-1])
            info["tris"].append(info["tris"][-1])
            info["tol"].append(round(tol, 4))
            info["dev"].append(info["dev"][-1] if info["dev"] else 0)
            continue
        p, mx = deviation(me, cand, mw)
        shaded = shade_like(cand, me, mw)
        shaded.name = "%s_LOD%d" % (name, k)
        out.append(shaded)
        info["tris"].append(tri_count(shaded))
        info["tol"].append(round(tol, 4))
        info["dev"].append(round(p, 4))
        prev = cand
    return out, info, diag


# ------------------------------------------------------------------ pictures

def _cam(name, loc, look, lens):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    cam.data.clip_end = 2000
    return cam


def picture(name, columns, objs_bounds):
    """Render each column's objects alone from the same two cameras; stitch them (whole above, close below)."""
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = 720, 560
    world = sc.world or bpy.data.worlds.new("w")
    sc.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.92, .92, .92, 1)
    bg.inputs["Strength"].default_value = .8
    sun = bpy.data.objects.get("bh_lod sun")
    if sun is None:
        sun = bpy.data.objects.new("bh_lod sun", bpy.data.lights.new("bh_lod sun", "SUN"))
        sc.collection.objects.link(sun)
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    lo, hi = objs_bounds
    mid, size = (lo + hi) / 2, max((hi - lo).length, .3)
    d = Vector((-.55, -1, .45)).normalized()
    if name.endswith(("rosette", "coffer")):
        d = Vector((-.55, -1, -.7)).normalized()       # ceiling parts: seen from below, as in the hall
    shots = [_cam("bh_lod whole", mid + d * size * 1.5, mid, 50)]
    # the close look: at the busiest corner (a quarter in from the bounds' near top corner)
    near = Vector((lo.x + (hi.x - lo.x) * .3, lo.y + (hi.y - lo.y) * .3, lo.z + (hi.z - lo.z) * (.7 if d.z > 0 else .3)))
    shots.append(_cam("bh_lod close", near + d * size * .32, near, 50))
    allobjs = [o for col in columns for o in col]
    rows = []
    os.makedirs(STILLS, exist_ok=True)
    for cam in shots:
        sc.camera = cam
        row = []
        for ci, col in enumerate(columns):
            for o in allobjs:
                o.hide_render = o not in col
            f = os.path.join(STILLS, "_tmp_%d.png" % ci)
            sc.render.filepath = f
            bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(f)
            w, hgt = im.size
            px = np.array(im.pixels[:], dtype=np.float32).reshape(hgt, w, 4)
            bpy.data.images.remove(im)
            row.append(px)
            os.remove(f)
        rows.append(np.concatenate(row, axis=1))
    for o in allobjs:
        o.hide_render = False
    img = np.concatenate(list(reversed(rows)), axis=0)        # (Blender images are bottom-up: whole ends on top)
    out = bpy.data.images.new(name, img.shape[1], img.shape[0], alpha=True)
    out.pixels = img.ravel()
    out.filepath_raw = os.path.join(STILLS, name + ".png")
    out.file_format = "PNG"
    out.save()
    return out.filepath_raw


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


def report(key, entry):
    os.makedirs(STILLS, exist_ok=True)
    data = json.load(open(REPORT, encoding="utf-8")) if os.path.exists(REPORT) else {}
    data[key] = entry
    with open(REPORT, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1, ensure_ascii=False)


# ------------------------------------------------------------------ one model

def run(name, sources, write_fbx=None):
    """sources: the original mesh objects (modifiers applied). Builds the levels, pictures them, reports."""
    columns = [list(sources), [], [], []]
    entry = {"objects": {}, "tris": [0, 0, 0, 0]}
    lod_objs = {k: [] for k in range(3)}
    for o in sources:
        lv, info, diag = levels_for(o.data, o.matrix_world, o.name)
        entry["objects"][o.name] = info
        entry["tris"][0] += info["tris"][0]
        for k in range(3):
            c = bpy.data.objects.new("%s_LOD%d" % (o.name, k), lv[k])
            bpy.context.scene.collection.objects.link(c)
            c.matrix_world = o.matrix_world
            columns[k + 1].append(c)
            lod_objs[k].append(c)
            entry["tris"][k + 1] += tri_count(lv[k])
    # the levels kept: each at most KEEP_LEVEL of the last kept one (LOD2 may stand in for a LOD1 that saved too little)
    kept = [0]
    for k in (1, 2):
        if entry["tris"][k + 1] <= KEEP_LEVEL * entry["tris"][kept[-1] + 1]:
            kept.append(k)
    used = len(kept)
    entry["levels"] = used
    entry["kept"] = kept
    entry["picture"] = os.path.relpath(picture(name, columns, bounds(sources)), ROOT).replace("\\", "/")
    print("BH_LOD %s tris %s levels %d" % (name, entry["tris"], used))
    report(name, entry)
    if write_fbx:
        export_levels(write_fbx, [lod_objs[k] for k in kept])
    return entry, [lod_objs[k] for k in kept], used


def export_levels(path, levels):
    """The FBX again, each mesh as <name>_LOD<k> (only the levels kept, numbered 0.. in order), bh_kit.export's
    settings."""
    for i, lv in enumerate(levels):
        for o in lv:
            o.name = o.name[:o.name.rindex("_LOD")] + "_LOD%d" % i
    keep = [o for lv in levels for o in lv]
    if len(levels) == 1:
        for o in keep:
            o.name = o.name[:-5]            # one level: the plain names (no LODGroup)
    bpy.ops.object.select_all(action="DESELECT")
    for o in keep:
        o.select_set(True)
    bpy.context.view_layer.objects.active = keep[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="AUTO",
                             embed_textures=False, colors_type="LINEAR")
    print("BH_LOD wrote", path, len(levels), "levels")


def fbx_job(rel, write):
    path = os.path.join(MODELS, rel + ".fbx")
    orig = os.path.join(HERE, "lod", "orig", rel + ".fbx")
    src = orig if os.path.exists(orig) else path      # always judge from the original, never from our own output
    bpy.ops.import_scene.fbx(filepath=src, axis_forward="-Z", axis_up="Y")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in meshes:
        o.parent = None if o.parent is None else o.parent
    import bh_kit
    try:
        bh_kit.preview_materials()
    except Exception as e:
        print("BH_LOD (no preview materials)", e)
    if write:
        os.makedirs(os.path.dirname(orig), exist_ok=True)
        if not os.path.exists(orig):
            shutil.copy2(path, orig)
    run(rel.replace("/", "_"), meshes, path if write else None)


PART_MODULES = {"b7": "bh_b7_hall_model", "b6l": "bh_b6_loft_model", "b6c": "bh_b6_chandelier_model", "b5": "bh_b5_stage_model"}


def part_job(spec):
    zone, part = spec.split(":")
    mod = __import__(PART_MODULES[zone])
    import bh_b7_hall_model as h
    col = bpy.data.collections.new("BH " + part)
    bpy.context.scene.collection.children.link(col)
    objs = h.PARTS[part][0](col)
    dg = bpy.context.evaluated_depsgraph_get()
    sources = []
    for o in objs:
        if o.type != "MESH" or "(lit)" in o.name:
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        s = bpy.data.objects.new(o.name, me)
        bpy.context.scene.collection.objects.link(s)
        s.matrix_world = o.matrix_world
        sources.append(s)
    bpy.data.collections.remove(col)
    entry, lod_objs, used = run("%s_%s" % (zone, part), sources)
    keep = set(o for lv in lod_objs for o in lv)
    for i, lv in enumerate(lod_objs):
        for o in lv:
            o.name = o.name[:o.name.rindex("_LOD")] + "_LOD%d" % i
    for o in list(bpy.context.scene.objects):
        if o.type == "MESH" and o not in keep:
            bpy.data.objects.remove(o)
    out = os.path.join(HERE, "lod", "parts", "%s_%s.blend" % (zone, part))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if "--fbx" in args:
        fbx_job(args[args.index("--fbx") + 1], "--write" in args)
    elif "--part" in args:
        part_job(args[args.index("--part") + 1])
