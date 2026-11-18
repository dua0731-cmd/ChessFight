"""R2 closing books (seoungtest R687, 2026-11-18; PLAN.md R2): the two closing folios, their desks and the seal rest
island between them (blockout collection "03 Books", Unity z -100 .. -60). Concept E3, shapes from E5.

    blender -b --factory-startup --python rl_r2_books_model.py -- [book] [desk] [island] [--export] [--still]

Background Blender only (the open Blender belongs to the E5 sessions). Without --export nothing is written to Assets.
Never --export while Unity is in Play.

Shapes are the E5 props the user approved (read only, exec'd: e5/rl_e5_06_07_tables.py, which execs
rl_e5_props_model.py into the same globals): build_open_book (book 04) and build_table (table 06/07). R2 changes,
all here and none in the E5 files:
  - fewer segments (zone budget, PLAN.md R2: a table was 39,796 triangles): turned legs 16, rivets 8 with a 5-point
    profile, other lathes half their segments, curve tubes 2 bevel steps;
  - book: everything that overhangs the pages along the course (back cover, its corners, spine roll / bands / caps,
    moving cover board and corners, hinge pin) is drawn to the blockout's 0.2 m overhang (E5: 0.45 - 0.67 m), because
    tables touch both ends of each book; the outer hinge knuckles move in 0.35 m for the same reason; the inner page
    stack of the moving cover is 2 sheets with the flyleaf at 0.25 m (E5: 4 sheets, 0.34 m), so the closed cover
    clears the pages and the warning bookmark (0.05 m) instead of cutting 1 cm into them;
  - added: a brass crest with a king on the moving cover's outer face (over "Royal cover crest"); the pages stay
    blank like E3 and cover the blockout's "Printed folio line *" (PRINTED_ROWS draws ink word-rows instead);
  - the bookmark stays the blockout's "Book warning bookmark": it is ClosingBook.warningStrip (gold, red before a
    close), so it is not covered;
  - desks: the book grows 2 m down (pages 1.3 + boards 0.7), so the desk is drawn DESK_DROP lower with legs that still
    end on the floor (z -21); the planks, corner plates and top rivets are under the book and left out;
  - seal island: corner caps pulled 0.27 m in from the book ends and 0.05 m down, without plates and top rivets (the
    closed cover's corners come 0.2 m over the island at z 0.15); the seal rest is the E5 07 dish made low (top 0.12,
    collider top 0.08) - E5's 0.53 m drum would sink feet 0.45 m.
Exports (rl_kit.export, zone "Books"): RlFolioBook (book frame "Closing royal folio -100", hung on both books),
RlFolioCover (frame "Horizontal closing cover", rides both moving covers), RlFolioDesk (frame "Royal folio desk -100",
hung on both desks), RlSealIsland (course root). Unity: RoyalLibraryDressing.Books.cs.
"""
import math, os, random, re, sys
import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
STILLS = os.path.join(HERE, "stills", "r2")
BLEND = os.path.join(HERE, "rl_r2_books.blend")
ZONE = "Books"

g = {"__name__": "rl_e5_tables", "__file__": os.path.join(HERE, "e5", "rl_e5_06_07_tables.py")}
exec(open(g["__file__"], encoding="utf-8").read(), g)   # E5 shapes; builds nothing
import rl_kit as k   # noqa: E402

SLOT_OF = {"E5_Walnut": "walnut", "E5_WalnutTop": "walnut", "E5_Brass": "brass", "E5_BrassDark": "brass",
           "E5_RedLeather": "leather", "E5_Paper": "parchment", "E5_Ivory": "parchment", "E5_BlackEnamel": "ink",
           "E5_Silk": "cloth"}

BOOK_Y = -100.0       # Blender y (= Unity z) of "Closing royal folio -100"; pages y 0 .. 16 from it
OVER = 0.2            # blockout overhang of covers and spine past the pages, along the course
DESK_DROP = 2.22      # desk model below its blockout top (-0.85): corner caps under the book's lowest point -2.04
DESK_Y, ISLAND_Y = -92.0, -80.0
PRINTED_ROWS = False  # ink word-rows over the blockout's printed lines; off: blank pages like E3 (they read as dashes)

# ---------------------------------------------------------------- lighter E5 primitives
_lathe, _tube = g["lathe"], g["tube"]


def lathe_lo(name, prof, mats, loc=(0, 0, 0), axis="Z", seg=48, smooth=30):
    if "rivet" in name:
        seg = 8
    elif re.search(r" leg [+-]1[+-]1$", name):
        seg = 16
    else:
        seg = max(12, seg // 3)
    return _lathe(name, prof, mats, loc, axis, seg, smooth)


def simplify(prof, tol, sx, sy):
    """Douglas-Peucker on a lathe profile (points scaled by sx, sy to metres before measuring)."""
    if len(prof) < 3:
        return list(prof)
    (r0, f0), (r1, f1) = prof[0], prof[-1]
    a, b = Vector((r0 * sx, f0 * sy)), Vector((r1 * sx, f1 * sy))
    d = (b - a).normalized() if (b - a).length > 1e-9 else Vector((1, 0))
    far, at = 0.0, 0
    for i, (r, f) in enumerate(prof[1:-1], 1):
        p = Vector((r * sx, f * sy)) - a
        dist = abs(p.x * d.y - p.y * d.x)
        if dist > far:
            far, at = dist, i
    if far <= tol:
        return [prof[0], prof[-1]]
    return simplify(prof[:at + 1], tol, sx, sy)[:-1] + simplify(prof[at:], tol, sx, sy)


def tube_lo(name, pts, r, mats, closed=False, kind="POLY", res=10, radii=None, bres=4):
    return _tube(name, pts, r, mats, closed, kind, min(res, 4), radii, min(bres, 2))


g["lathe"], g["tube"] = lathe_lo, tube_lo
g["RIVET"] = [(0, 0), (0.17, 0), (0.165, 0.05), (0.11, 0.097), (0, 0.117)]
g["LEG_PROFILE"] = simplify(g["LEG_PROFILE"], 0.03, g["LEG_R"], g["LEG_TOP"] - g["LEG_BOT"])


# ---------------------------------------------------------------- scene helpers
def fresh_collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    return col


def bake(objs, col_name, warp=None):
    """World-space evaluated copies (modifiers, curves) in their export collection, palette -> rl_kit slots.
    warp(name, co) -> co may move vertices (world space)."""
    col = fresh_collection(col_name)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    for o in objs:
        if o.type not in {"MESH", "CURVE"}:
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=False, depsgraph=dg)
        me.transform(o.matrix_world)
        if warp:
            for v in me.vertices:
                v.co = warp(o.name, v.co)
        for i, m in enumerate(me.materials):
            name = m.name if m else ""
            if name not in SLOT_OF:
                raise RuntimeError("no slot for %s on %s" % (name, o.name))
            me.materials[i] = k.slot(SLOT_OF[name])
        ob = bpy.data.objects.new(o.name, me)
        col.objects.link(ob)
        out.append(ob)
    return col, out


def finish(col, model):
    """Metre UVs per part, one mesh, one material slot per rl_kit slot."""
    obs = list(col.objects)
    for ob in obs:
        k.surface_uvs(ob)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in obs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    if len(obs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = model
    mats = ob.data.materials
    first = {}
    for i, m in enumerate(mats):
        first.setdefault(m.name, i)
    for p in ob.data.polygons:
        p.material_index = first[mats[p.material_index].name]
    for i in reversed(range(len(mats))):
        if first[mats[i].name] != i:
            mats.pop(index=i)
    print("[r2] %s: %d tris, slots %s" % (model, sum(len(p.vertices) - 2 for p in ob.data.polygons),
                                          [m.name for m in mats]))
    return ob


def drop_src(col_name):
    col = bpy.data.collections.get(col_name)
    if col:
        for o in list(col.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(col)


def add_box(bm, x0, x1, y0, y1, z0, z1):
    bmesh.ops.create_cube(bm, size=1, matrix=Matrix.LocRotScale(
        Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)), None, Vector((x1 - x0, y1 - y0, z1 - z0))))


# ---------------------------------------------------------------- book
KING = [(0.85, -1.5), (0.85, -1.28), (0.62, -1.18), (0.5, -1.0), (0.38, -0.2), (0.3, 0.35), (0.52, 0.45),
        (0.52, 0.6), (0.32, 0.68), (0.46, 0.98), (0.14, 1.02), (0.14, 1.16), (0.32, 1.16), (0.32, 1.3),
        (0.14, 1.3), (0.14, 1.5)]   # right half of a king, (across, up)


def book_extras(piv):
    """Printed text rows on the pages (book frame) and the crest on the moving cover (pivot frame)."""
    C = g["Ctx"]
    if PRINTED_ROWS:
        rnd = random.Random(7)
        bm = bmesh.new()
        for i in range(7):                       # the blockout's 7 "Printed folio line" rows, y 2.0 + 1.8 i
            yc, x = 2.0 + 1.8 * i, -6.0
            end = 6.0 - (rnd.uniform(1.5, 4.5) if i in (3, 6) else 0)   # paragraph ends
            while x < end - 0.15:
                w = min(rnd.uniform(0.25, 1.25), end - x)
                add_box(bm, x, x + w, yc - 0.16, yc + 0.16, -0.004, 0.012)
                x += w + rnd.uniform(0.28, 0.42)
        g["mesh_obj"]("BK printed rows", bm, [g["M"]("E5_BlackEnamel")], smooth=0)

    BR = g["M"]("E5_Brass")
    face = 0.16 - g["BK_COVER"]               # outer face of the moving cover (pivot frame), -0.54
    cx = -9.4
    ell = lambda rx, ry, n=48: [(cx + rx * math.cos(2 * math.pi * i / n), ry * math.sin(2 * math.pi * i / n))
                                for i in range(n)]
    made = [g["prism"]("BK crest plate", ell(2.0, 2.5), "XY", face - 0.015, face + 0.02, BR, (0.012, 2)),
            g["tube"]("BK crest ring", [(x, y, face - 0.02) for x, y in ell(1.8, 2.3, 40)], 0.045, BR, closed=True)]
    s = 1.15
    half = [(cx + u * s, v * s) for u, v in KING]
    outline = half + [(2 * cx - x, y) for x, y in reversed(half)]
    made.append(g["prism"]("BK crest king", outline, "XY", face - 0.075, face - 0.01, BR, (0.018, 2)))
    for o in made:
        o.parent = piv
    return C


def build_book():
    root = g["build_open_book"](offset=(0, 0, 0))
    C = g["Ctx"]
    piv = next(o for o in C.col.objects if o.name.startswith("Horizontal closing cover"))
    ob = {o.name: o for o in C.col.objects}
    ob["BK moving page sheets"].modifiers["Pages"].count = 2          # stack top 0.247 (pivot frame)
    ob["BK moving flyleaf"].location.z -= 0.09                        # 0.25 .. 0.27
    for tag, dy in (("-8", 0.35), ("+8", -0.35)):                     # outer knuckles 7.6 -> 7.25 from the middle
        ob["BK hinge knuckle " + tag].location.y += dy
        ob["BK hinge leaf " + tag].location.y += dy
    book_extras(piv)
    piv.rotation_euler = (0, 0, 0)                                    # open flat = the prefab's rest pose
    root.location = (0, BOOK_Y, 0)
    bpy.context.view_layer.update()

    mid = BOOK_Y + 8.0

    def fit(half_now):
        return lambda co: Vector((co.x, mid + (co.y - mid) * (8.0 + OVER) / half_now, co.z))
    rules = [(("BK back cover", "BK back corner", "BK moving cover", "BK moving corner"), fit(8.45)),
             (("BK spine",), fit(8.67)), (("BK hinge pin",), fit(8.2 / (8.0 / 8.2)))]

    def warp(name, co):
        for prefixes, f in rules:
            if name.startswith(prefixes):
                return f(co)
        return co

    def on_cover(o):
        while o is not None:
            if o == piv:
                return True
            o = o.parent
        return False
    objs = list(C.col.objects)
    body_col, _ = bake([o for o in objs if not on_cover(o)], "R2 book body", warp)
    cover_col, cover = bake([o for o in objs if on_cover(o) and o != piv], "R2 book cover", warp)
    pivot_world = piv.matrix_world.translation.copy()
    drop_src(C.col.name)
    body = finish(body_col, "RlFolioBook")
    cov = finish(cover_col, "RlFolioCover")
    check_cover(cov, pivot_world)
    return body, cov


def check_cover(cov, pv):
    """The closing sweep (Unity: cover.localRotation = Euler(0, 0, -180 fold)): lowest point of the cover over the
    pages (x > -8.9) at each angle, and the closed pose's clearance over the pages / bookmark (0.05)."""
    # the hinge knuckles and pin are round about the axis: they turn in place, so they are left out
    pts = [v.co.copy() for v in cov.data.vertices if math.hypot(v.co.x - pv.x, v.co.z - pv.z) > 0.6]
    worst = []
    for deg in range(0, 181, 10):
        a = math.radians(deg)
        lo = 1e9
        for p in pts:
            dx, dz = p.x - pv.x, p.z - pv.z
            x = pv.x + dx * math.cos(a) + dz * math.sin(a)
            z = pv.z - dx * math.sin(a) + dz * math.cos(a)
            if x > -8.9:
                lo = min(lo, z)
        worst.append((deg, lo))
    print("[r2] cover lowest z over the pages by angle:", ", ".join("%d:%.3f" % w for w in worst if w[1] < 1e8))
    closed = [w[1] for w in worst if w[0] == 180][0]
    print("[r2] closed cover lowest over the pages %.3f (pages 0, bookmark top 0.05) -> %s"
          % (closed, "OK" if closed > 0.05 else "TOO LOW"))


# ---------------------------------------------------------------- desk and island
def build_desk():
    g["begin_model"]("R2_desk_src")
    old = g["LEG_BOT"]
    g["LEG_BOT"] = old + DESK_DROP          # turned legs end DESK_DROP higher, so the lowered desk stands on z -21
    try:
        g["build_table"]("FD", 18.0, 16.0, g["M"]("E5_Brass"), g["M"]("E5_Walnut"), g["top_material"]())
    finally:
        g["LEG_BOT"] = old
    C = g["Ctx"]
    for o in list(C.col.objects):
        n = o.name
        if " top plank " in n or "corner plate" in n or "corner rivet top" in n:
            bpy.data.objects.remove(o, do_unlink=True)
        elif " leg foot " in n:
            o.location.z += DESK_DROP
        elif "corner cap" in n or "corner rivet" in n:
            o.location.y -= int(n[-2:]) * 0.27     # stay inside the desk's own length (books / tables at both ends)
    g["end_model"]((0, DESK_Y, -DESK_DROP))
    col, _ = bake([o for o in C.col.objects], "R2 desk")
    drop_src(C.col.name)
    return finish(col, "RlFolioDesk")


def build_island():
    g["begin_model"]("R2_island_src")
    BR = g["M"]("E5_Brass")
    g["build_table"]("SI", 18.0, 8.0, BR, g["M"]("E5_Walnut"), g["top_material"]())
    C = g["Ctx"]
    for o in list(C.col.objects):
        n = o.name
        if "corner plate" in n or "corner rivet top" in n:
            bpy.data.objects.remove(o, do_unlink=True)
        elif "corner cap" in n or "corner rivet" in n:
            o.location.y -= int(n[-2:]) * 0.27
            o.location.z -= 0.05                  # cap top 0.12, under the closed covers' corners (0.15)
    # the E5 07 seal dish, low: flange r 2.5, raised lip 0.12 at r 2.1, dished centre 0.075, a fine ring at r 1.4
    g["lathe"]("SI seal dish", [(0, -0.02), (2.50, -0.02), (2.56, 0.01), (2.57, 0.04), (2.52, 0.068), (2.40, 0.082),
                                (2.28, 0.088), (2.22, 0.10), (2.16, 0.118), (2.08, 0.12), (2.02, 0.11), (1.96, 0.085),
                                (1.50, 0.077), (1.45, 0.09), (1.38, 0.09), (1.33, 0.077), (0, 0.075)], BR, seg=96)
    for i in range(12):
        a = 2 * math.pi * (i + 0.5) / 12
        g["rivet"]("SI seal rivet %d" % i, (2.36 * math.cos(a), 2.36 * math.sin(a), 0.08), "Z", BR, 0.45)
    g["end_model"]((0, ISLAND_Y, 0))
    col, _ = bake([o for o in C.col.objects], "R2 seal island")
    drop_src(C.col.name)
    return finish(col, "RlSealIsland")


# ---------------------------------------------------------------- bookmark (the game's warning strip)
# Blockout "Book warning bookmark": 0.4 x 0.04 x 16 box at x 8.6, y 0 .. 16 of the book, z 0.005 .. 0.045 - it is
# ClosingBook.warningStrip (gold; a property block turns it red from the warning until the cover reopens). The model is
# a silk ribbon (cloth slot; RoyalLibraryDressing.Books.cs copies the strip's colour onto it, gold at rest) with brass
# tips at both ends, inside the strip's footprint and under the closed cover's lowest point over the pages (0.08).
BM_X0, BM_X1 = 8.4, 8.8
BM_TIP = 0.62          # brass tip length at each end


def build_bookmark():
    col = fresh_collection("R2 bookmark src")
    g["PALETTE"]["E5_Silk"] = dict(rgb=(0.80, 0.70, 0.52), rough=0.35)   # previews; Unity draws RL_CreamCloth tinted
    silk, brass = g["M"]("E5_Silk"), g["M"]("E5_Brass")
    g["Ctx"].col, g["Ctx"].root = col, None

    def link(o):
        col.objects.link(o)
        return o
    # silk: a gently undulating ribbon with curled selvedges, lying on the top page
    nx, ny = 6, 72
    y0, y1 = BM_TIP - 0.08, 16.0 - BM_TIP + 0.08
    bm = bmesh.new()
    rows = []
    for j in range(ny + 1):
        y = y0 + (y1 - y0) * j / ny
        sway = 0.025 * math.sin(2 * math.pi * y / 5.3)
        lift = 0.012 + 0.006 * math.sin(2 * math.pi * y / 3.1 + 0.7)
        row = []
        for i in range(nx + 1):
            u = -1 + 2 * i / nx
            x = (BM_X0 + BM_X1) / 2 + u * (BM_X1 - BM_X0 - 0.06) / 2 + sway
            row.append(bm.verts.new((x, BOOK_Y + y, lift + 0.007 * u * u)))
        rows.append(row)
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    me = bpy.data.meshes.new("BM silk")
    bm.to_mesh(me); bm.free()
    me.materials.append(silk)
    me.shade_smooth()
    ribbon = link(bpy.data.objects.new("BM silk", me))
    sol = ribbon.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness, sol.offset = 0.012, -1
    # brass tips: pointed clasp toward the page end, a bead across its inner end and a rivet
    for end in (0, 1):
        def Y(d):
            return BOOK_Y + (d if end == 0 else 16.0 - d)
        pts = [(BM_X0 - 0.03, BM_TIP), (BM_X0 - 0.03, 0.3), (8.6, 0.08), (BM_X1 + 0.03, 0.3), (BM_X1 + 0.03, BM_TIP)]
        pts = [(x, Y(d)) for x, d in pts]
        if end:
            pts.reverse()
        tip = g["prism"]("BM tip %d" % end, pts, "XY", -0.002, 0.04, brass, (0.012, 2))
        tip.parent = None
        bead = g["tube"]("BM tip bead %d" % end, [(BM_X0 - 0.02, Y(BM_TIP - 0.03), 0.04), (BM_X1 + 0.02, Y(BM_TIP - 0.03), 0.04)],
                         0.018, brass)
        bead.parent = None
        g["rivet"]("BM tip rivet %d" % end, (8.6, Y(0.33), 0.03), "Z", brass, 0.3).parent = None
    out_col, _ = bake(list(col.objects), "R2 bookmark")
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.collections.remove(col)
    ob = finish(out_col, "RlBookmark")
    top = max(v.co.z for v in ob.data.vertices)
    print("[r2] bookmark top z %.3f (closed cover lowest over the pages 0.080) -> %s" % (top, "OK" if top < 0.075 else "TOO HIGH"))
    return ob


# ---------------------------------------------------------------- export and stills
def frame_index(name, y):
    k.frame(name)
    for i in range(len(k._poses[name])):
        if abs(k.frame(name, i).translation.y - y) < 0.5:
            return i
    raise RuntimeError("no %s near y %.1f" % (name, y))


EXPORTS = {   # model -> (collection, frame name, frame y)
    "RlFolioBook": ("R2 book body", "Closing royal folio -100", BOOK_Y),
    "RlFolioCover": ("R2 book cover", "Horizontal closing cover", BOOK_Y + 8),
    "RlFolioDesk": ("R2 desk", "Royal folio desk -100", DESK_Y),
    "RlSealIsland": ("R2 seal island", None, 0),
    "RlBookmark": ("R2 bookmark", "Book warning bookmark", BOOK_Y + 8),
}


def export(model):
    col, fname, fy = EXPORTS[model]
    if fname:
        return k.export(col, model, ZONE, frame_name=fname, frame_index=frame_index(fname, fy))
    return k.export(col, model, ZONE)


def still(name, angle=112.0, parts=("RlFolioBook", "RlFolioCover", "RlFolioDesk", "RlSealIsland"),
          view=(-0.75, -1.0, 0.75), scale=0.0, res=(1600, 1100), focus=None, tint=None):
    """Look check in the library materials (rl_lookdev's shading and light) of the exported FBXs in place; the moving
    cover turned `angle` degrees closed. focus = (centre, ortho scale) for a close-up; tint = {slot: rgb} multiplies a
    library material like a URP _BaseColor property block (the bookmark's signal colour)."""
    src = open(os.path.join(HERE, "rl_lookdev.py"), encoding="utf-8").read().replace("\nmain()\n", "\n")
    lk = {"__name__": "rl_lookdev_lib", "__file__": os.path.join(HERE, "rl_lookdev.py")}
    exec(src, lk)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    cache, objs = {}, []
    for model in parts:
        col, fname, fy = EXPORTS[model]
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(k.ROOT, k.OUT_REL, ZONE, model + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        F = k.frame(fname, frame_index(fname, fy)) if fname else Matrix.Identity(4)
        if model == "RlFolioCover":
            F = F @ Matrix.Rotation(math.radians(angle), 4, "Y")
        bpy.context.view_layer.update()
        for o in new:
            o.matrix_world = F @ o.matrix_world
        objs += new
    for o in objs:
        for s in o.material_slots:
            # a second FBX's "RL | walnut" imports as "RL | walnut.001"
            key = s.material.name.split(".")[0].split("|")[-1].split("_")[-1].strip().lower() if s.material else ""
            if key in lk["SLOT"]:
                cache.setdefault(key, lk["library_material"](lk["SLOT"][key]))
                s.material = cache[key]
    for key, rgb in (tint or {}).items():
        m = cache.get(key)
        if m is None:
            continue
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        src_sock = bsdf.inputs["Base Color"].links[0].from_socket
        mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1
        nt.links.new(src_sock, mul.inputs[6]); mul.inputs[7].default_value = (*rgb, 1)
        nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
    bpy.context.view_layer.update()
    lo = Vector((min(min((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in objs) for i in range(3)))
    hi = Vector((max(max((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in objs) for i in range(3)))
    centre, size = (lo + hi) / 2, (hi - lo).length
    if focus:
        centre, scale = Vector(focus[0]), focus[1]
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "AgX"
    sc.render.resolution_x, sc.render.resolution_y = res
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.95, .82, .66, 1); bg.inputs["Strength"].default_value = .7
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 4.5; sun.data.color = (1.0, .86, .68); sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-40))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = scale or size * 0.95
    v = Vector(view).normalized()
    cam.location = centre + v * size * 3
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = size * 10
    os.makedirs(STILLS, exist_ok=True)
    sc.render.filepath = os.path.join(STILLS, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("[r2] still", sc.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = [a for a in argv if not a.startswith("--")] or ["book", "desk", "island", "bookmark"]
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    made = []
    if "bookmark" in which:
        build_bookmark(); made.append("RlBookmark")
    if "book" in which:
        build_book(); made += ["RlFolioBook", "RlFolioCover"]
    if "desk" in which:
        build_desk(); made.append("RlFolioDesk")
    if "island" in which:
        build_island(); made.append("RlSealIsland")
    for c in list(bpy.data.collections):
        if c.name.startswith("E5_"):
            for o in list(c.all_objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(c)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("[r2] saved", BLEND)
    for a in argv:   # --export = everything built, --export=RlBookmark,RlFolioBook = only those
        if a == "--export" or a.startswith("--export="):
            only = a.split("=", 1)[1].split(",") if "=" in a else made
            for m in made:
                if m in only:
                    export(m)
    have = [m for m in EXPORTS if os.path.exists(os.path.join(k.ROOT, k.OUT_REL, ZONE, m + ".fbx"))]
    if "--still" in argv:
        still("r2_books_open", 112.0, have)
        still("r2_books_closed", 180.0, have)
        still("r2_books_side", 60.0, have, view=(-1.0, -0.15, 0.35))
    if "--still-bookmark" in argv:
        # the ribbon's two colours, as Books.cs tints it: at rest deep gold (.95, .5, .08), warning ClosingBook's Color.red
        parts = [m for m in ("RlFolioBook", "RlFolioDesk", "RlBookmark") if m in have]
        for nm, rgb in (("r2_bookmark_rest", (.95, .5, .08)), ("r2_bookmark_warning", (1.0, .0, .0))):
            still(nm, 0, parts, view=(-.45, -1.0, .6), focus=((8.6, BOOK_Y + 1.6, 0), 4.2), tint={"cloth": rgb})
        still("r2_bookmark_whole", 0, parts, view=(-.45, -1.0, .9), focus=((4.0, BOOK_Y + 8, 0), 21),
              tint={"cloth": (.95, .5, .08)})


if __name__ == "__main__":
    main()
