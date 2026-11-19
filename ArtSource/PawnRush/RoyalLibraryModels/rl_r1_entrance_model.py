"""R1 entrance + stamp zone models (seoungtest R687, PLAN.md R1, 2026-11-18).

Blockout collections "01 Entrance" + "02 Stamp" (Unity z -150 .. -100). Run in the background only:

    blender -b --factory-startup --python rl_r1_entrance_model.py -- <parts> [preview] [export]

  parts   tables | stamp | entrance | all
  preview writes a preview FBX of the built parts (root frame) to stills/r1/_preview_<part>.fbx and renders it
          with rl_lookdev.py (library materials) -> stills/r1/<part>.png
  export  rl_kit.export of every model of the built parts -> Assets/Resources/RoyalLibrary/Models/Entrance/
          (never while Unity is in Play)
The scene is saved to rl_r1_entrance.blend.

Shapes come from the E5 props the user confirmed, read only (exec'd for their functions, never edited):
  course table kit   e5/rl_e5_06_07_tables.py  build_table(W, D)
  stamp press        rl_e5_props_model.py      build_stamp_press
  wax seal           e5/rl_e5_08_09_sign_seal.py build_wax_seal
Low-poly pass (PLAN: a table was 39,796 triangles, target about 12,000): the E5 helpers are wrapped in this
script's copy of their namespace - fewer lathe segments, simplified lathe profiles, one bevel segment less,
coarser curves, rivets further apart. The E5 files themselves are untouched.

Frame: the blockout's (Blender x, y, z = Unity x, z, y, metres). Walkable tops stay the prefab's (z 0).
"""
import math, os, subprocess, sys
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
STILLS = os.path.join(HERE, "stills", "r1")
BLEND = os.path.join(HERE, "rl_r1_entrance.blend")
ZONE = "Entrance"
WAX_SLOT = "wax"   # rl_kit slot added by R0 for R1 (11-18)

# ------------------------------------------------------------------ E5 namespace (read only)
E5 = {"__name__": "rl_r1_e5", "__file__": os.path.join(HERE, "e5", "rl_e5_06_07_tables.py")}
exec(open(E5["__file__"], encoding="utf-8").read(), E5)          # props helpers + table kit, builds nothing
_seal_src = open(os.path.join(HERE, "e5", "rl_e5_08_09_sign_seal.py"), encoding="utf-8").read()
_table_palette = dict(E5["PALETTE"])
exec(_seal_src.split("# =================================================================== main")[0], E5)
# the seal file re-runs the props helpers (a fresh PALETTE): put back the table kit's own colours
for _k, _v in _table_palette.items():
    E5["PALETTE"].setdefault(_k, _v)

import rl_kit as k   # noqa: E402

# tables: the E5 kit's varnished walnut (user 11-18 "탁자를 살짝 광택이 나게"), top included as R3 does
SLOT_OF = {"E5_Walnut": "walnut", "E5_TableWalnut": "varnished", "E5_WalnutTop": "varnished", "E5_Brass": "brass", "E5_BrassDark": "brass",
           "E5_BrassSatin": "brass", "E5_DarkSteel": "ink", "E5_RedWax": WAX_SLOT, "E5_RedLeather": "leather",
           "E5_Paper": "parchment", "E5_Ivory": "stone", "E5_BlackEnamel": "ink",
           # the vestibule (user 11-18: the colours of GC_assets_04): dark varnished panels, white marble
           "R1_PanelWalnut": "varnished", "R1_Marble": "marble" if "RL | marble" in k.SLOTS else "stone",
           "R1_Veil": "veil" if "RL | veil" in k.SLOTS else "ink"}

# ------------------------------------------------------------------ low-poly wrappers
LP = {"on": True, "big": 12, "small": 8, "tol": 0.015, "bevel_drop": 1, "curve_res": 6, "curve_bres": 2}
_orig = {n: E5[n] for n in ("lathe", "bevel", "tube", "lathe_bm", "chaikin")}


def _rdp(pts, tol):
    """Ramer-Douglas-Peucker on an open (r, z) polyline; keeps both ends."""
    if len(pts) < 3:
        return list(pts)
    (x0, y0), (x1, y1) = pts[0][:2], pts[-1][:2]
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy) or 1e-9
    best, bi = -1.0, 0
    for i in range(1, len(pts) - 1):
        d = abs(dy * (pts[i][0] - x0) - dx * (pts[i][1] - y0)) / L
        if d > best:
            best, bi = d, i
    if best <= tol:
        return [pts[0], pts[-1]]
    return _rdp(pts[:bi + 1], tol)[:-1] + _rdp(pts[bi:], tol)


def _seg(seg):
    if not LP["on"]:
        return seg
    return LP["small"] if seg <= 24 else (LP["big"] if seg <= 48 else max(LP["big"], int(seg * LP.get("hero", .34))))


def lathe(name, prof, mats, loc=(0, 0, 0), axis="Z", seg=48, smooth=30):
    s = _seg(seg)
    if LP["on"] and len(prof) > 8:
        prof = _rdp(list(prof), LP["tol"])
    return _orig["lathe"](name, prof, mats, loc, axis, s, max(smooth, 360.0 / s + 8))


def bevel(o, w, seg=3, angle=40, limit="ANGLE", profile=0.5):
    if LP["on"]:
        seg = max(1, seg - LP["bevel_drop"])
    return _orig["bevel"](o, w, seg, angle, limit, profile)


def tube(name, pts, r, mats, closed=False, kind="POLY", res=10, radii=None, bres=4):
    if LP["on"]:
        res, bres = min(res, LP["curve_res"]), min(bres, LP["curve_bres"])
    return _orig["tube"](name, pts, r, mats, closed, kind, res, radii, bres)


E5.update(lathe=lathe, bevel=bevel, tube=tube)
E5["RIVET_STEP"] = 3.2


# ------------------------------------------------------------------ scene helpers
def collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    return col


def bake(src_col_name, model, dst, keep=None, matrix=None):
    """Evaluate every mesh/curve of an E5 collection (modifiers applied), palette -> rl_kit slots, metre UVs,
    join into one object `model` in collection `dst`. keep(name) filters parts; matrix moves the result."""
    src = bpy.data.collections[src_col_name]
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    obs = []
    for o in list(src.all_objects):
        if o.type not in {"MESH", "CURVE"} or o.hide_render or (keep and not keep(o.name)):
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=False, depsgraph=dg)
        if not len(me.polygons):
            continue
        for i, m in enumerate(me.materials):
            nm = m.name if m else ""
            if nm.startswith("RL | "):
                continue
            if nm not in SLOT_OF:
                raise RuntimeError("no slot for %s on %s" % (nm, o.name))
            me.materials[i] = k.slot(SLOT_OF[nm])
        ob = bpy.data.objects.new(o.name, me)
        mw = (matrix @ o.matrix_world) if matrix is not None else o.matrix_world.copy()
        if mw.determinant() < 0:      # mirrored copy: bake the mirror into the mesh, keep the faces outward
            me.transform(mw)
            me.flip_normals()
            mw = mw.Identity(4)
        ob.matrix_world = mw
        dst.objects.link(ob)
        obs.append(ob)
    return join(obs, model)


def join(obs, model):
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
    return ob


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def drop_e5(col_name):
    c = bpy.data.collections.get(col_name)
    if c:
        for o in list(c.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(c)


MODELS = {}   # model name -> (collection name, frame name or None, covers)


# ------------------------------------------------------------------ 1. tables
TABLES = (("RlStartTable", 18.0, 17.0, -137.5, ["Approach floor -146*"]),
          ("RlDispatchTable", 18.0, 27.0, -113.5, ["Dispatch table after jump*"]))


def build_tables():
    for model, W, D, cy, covers in TABLES:
        E5["build_course_table"](offset=(0, cy, 0), W=W, D=D)
        col = collection("R1 " + model)
        ob = bake("E5_06_CourseTable", model, col)
        drop_e5("E5_06_CourseTable")
        print("[r1] %s %d tris, slots %s" % (model, tris(ob), [m.name for m in ob.data.materials]))
        MODELS[model] = (col.name, None, covers)


# ------------------------------------------------------------------ 2. stamp presses
# Blockout: two `Command stamp press - warning then strike` at x -3 / +3, z -122 (Unity), each a square brass head
# (`Stamp head - edit timing and impulse`, frame at the head's struck pose on the table top) that the game holds
# 3 m up (CommandStampPress.raisedHeight, 6 before 11-18) and slams down; no column or arm in the blockout.
# The E5 press is fitted to that (shape kept, parts moved):
#  - no base: it stood under the head (0.62 m above the walking top, and the head would strike it);
#  - the column stands off the table's long side (x +-10.6), clamped to the table's edge, run down below the rim;
#  - column top, hinge, arm and guide collar ride STAMP_TRAVEL - SP_LIFT higher - the collar sits just above the
#    raised head; the ram (plunger) is 4.4 m longer so it stays in the collar when the head strikes;
#  - moving = pad, head, ram, ferrule, knob (round: one FBX serves both heads); fixed = the rest, mirrored at x -3.
STAMP_X, STAMP_Y = 3.0, -122.0
STAMP_TRAVEL = 3.0       # CommandStampPress.raisedHeight in the prefab (6 -> 3, user 11-18: keep the E5 proportions)
COL_OUT = 1.0            # column moved this much further out than E5 (clears the table rim and rivets)
RAM_LONGER = STAMP_TRAVEL - 1.4 + 0.1   # E5 ram fits a 1.4 m lift (SP_LIFT)
HEAD = "Stamp head - edit timing and impulse"
MOVING = ("SP rubber pad", "SP brass head", "SP plunger", "SP knob ferrule", "SP walnut knob")


def _shift_mesh(o, fn):
    for v in o.data.vertices:
        v.co = fn(v.co)


def build_stamp():
    from mathutils import Matrix, Vector
    E5["LP"] = LP
    LP["hero"] = .34
    E5["build_stamp_press"](offset=(0, 0, 0))
    bt, head_z = E5["SP_BASE_TOP"], E5["SP_BASE_TOP"] + E5["SP_LIFT"]
    col_e5 = bpy.data.collections["E5_03_StampPress"]
    obs = {o.name: o for o in col_e5.all_objects}
    # ram + knob longer
    _shift_mesh(obs["SP plunger"], lambda c: Vector((c.x, c.y, c.z + RAM_LONGER)) if c.z > 3 else c)
    for n in ("SP knob ferrule", "SP walnut knob"):
        _shift_mesh(obs[n], lambda c: Vector((c.x, c.y, c.z + RAM_LONGER)))
    # column out and down (E5 z -> world z + dz)
    dz = (STAMP_TRAVEL + 1.85) - (head_z + 1.85) + 0.05
    col_bottom = -3.7 - dz
    for n, o in obs.items():
        if n.startswith(("SP column", "SP hinge")):
            o.location.x += COL_OUT
        if n.startswith(("SP column core", "SP column bar")):
            _shift_mesh(o, lambda c: Vector((c.x, c.y, col_bottom)) if c.z < bt + 0.3 else c)
    arm = obs["SP arm"]
    arm.data.resolution_u = 8
    for bp, dx in zip(arm.data.splines[0].bezier_points, (COL_OUT, COL_OUT, .6 * COL_OUT, .3 * COL_OUT, 0, 0)):
        bp.co.x += dx
    # clamp to the table edge + drop finial under the column (my parts, E5 helpers)
    E5["begin_model"]("R1_StampClamp")
    BR, BD, WA = E5["M"]("E5_Brass"), E5["M"]("E5_BrassDark"), E5["M"]("E5_Walnut")
    cx = 6.6 + COL_OUT                       # column centre, head-relative
    for z0, z1, xin in ((-1.28 - dz, -0.12 - dz, 6.25), (-3.0 - dz, -2.25 - dz, 5.62)):
        E5["box"]("SC clamp plate %.0f" % z0, xin, cx - 0.65, -1.15, 1.15, z0, z1, BR, (0.05, 2))
        for y in (-0.75, 0.75):
            E5["lathe"]("SC clamp bolt %.0f %+.1f" % (z0, y), [(0, 0), (0.16, 0), (0.16, 0.04), (0.12, 0.09), (0, 0.11)],
                        BD, (xin + 0.35, y, z1), seg=16)
    E5["lathe"]("SC column drop", [(0, col_bottom), (1.05, col_bottom), (1.1, col_bottom - 0.12), (0.9, col_bottom - 0.3),
                                  (0.7, col_bottom - 0.6), (0.62, col_bottom - 0.9), (0.75, col_bottom - 1.15),
                                  (0.55, col_bottom - 1.45), (0.25, col_bottom - 1.7), (0, col_bottom - 1.75)],
                BR, (cx, 0, 0), seg=32)
    E5["end_model"]((0, 0, 0))

    # bake: moving head at the -3 head's struck pose; fixed frames at both sides (x -3 mirrored)
    def moving(n):
        return n in MOVING

    def fixed(n):
        return not n.startswith(("SP base", "SP foot")) and n not in MOVING

    col = collection("R1 RlStampHead")
    head = bake("E5_03_StampPress", "RlStampHead", col, moving, Matrix.Translation((-STAMP_X, STAMP_Y, -head_z)))
    MODELS["RlStampHead"] = (col.name, HEAD, ["Press brass head", "Crown die"])
    col = collection("R1 RlStampFrames")
    parts = []
    for sx in (1, -1):
        m = Matrix.Translation((sx * STAMP_X, STAMP_Y, dz)) @ Matrix.Scale(sx, 4, (1, 0, 0))
        parts.append(bake("E5_03_StampPress", "frame %+d" % sx, col, fixed, m))
        parts.append(bake("R1_StampClamp", "clamp %+d" % sx, col, None, m))
    frames = join(parts, "RlStampFrames")
    MODELS["RlStampFrames"] = (col.name, None, [])
    drop_e5("E5_03_StampPress"), drop_e5("R1_StampClamp")
    LP.pop("hero", None)
    build_seals()
    print("[r1] RlStampHead %d tris, RlStampFrames %d tris" % (tris(head), tris(frames)))

    # preview only: the +3 head raised (the pose it holds most of the cycle); -3 stays struck
    pv = collection("R1 _preview stamp")
    cp = head.copy(); cp.data = head.data.copy()
    cp.matrix_world = Matrix.Translation((2 * STAMP_X, 0, STAMP_TRAVEL)) @ head.matrix_world
    pv.objects.link(cp)


WARN = "Rectangular strike warning"   # CommandStampPress.warningVisual: on from the warning until the strike
WARN_HALF, WARN_HOLE = 3.0, 2.85      # 6 x 6 warning square; round opening round the seal (seal lobes reach ~2.76)


def build_seals():
    """The stamp zone in the seal's look (user 11-18 "도장 구역을 인장이랑 어울리게"):
    - the red wax seal (E5 09) lies under each stamp, where it strikes: full 5 m size, 0.3 of its height (highest
      point ~0.11 m, inside the 5.24 m pad when it lands, so the pad covers it);
    - the safe waiting line: a red wax strip between brass edges on the table top (the blockout inlay hung half over
      the jump gap; this one stops at the table's rim);
    - the strike warning (RlStrikeWarning, hung on the warning object so CommandStampPress shows and hides it): a red
      wax square plate with a round opening round the seal, a brass frame, a brass ring at the opening, rosettes."""
    from mathutils import Matrix
    E5["lathe_bm"] = lambda prof, seg=48, bm=None, matrix=None: _orig["lathe_bm"](prof, min(seg, 54), bm, matrix)
    E5["chaikin"] = lambda pts, it=3, closed=True: _orig["chaikin"](pts, min(it, 1), closed)
    E5["build_wax_seal"](offset=(0, 0, 0))
    E5["lathe_bm"], E5["chaikin"] = _orig["lathe_bm"], _orig["chaikin"]
    col = collection("R1 RlWaxSeals")
    parts = []
    for i, (sx, rot) in enumerate(((-1, 15), (1, -25))):
        m = (Matrix.Translation((sx * STAMP_X, STAMP_Y, 0)) @ Matrix.Rotation(math.radians(rot), 4, "Z")
             @ Matrix.Diagonal((1, 1, .3, 1)))
        parts.append(bake("E5_09_WaxSeal", "seal %d" % i, col, None, m))
    drop_e5("E5_09_WaxSeal")

    # safe waiting lines
    E5["begin_model"]("R1_SafeLines")
    WX, BR, box = E5["M"]("E5_RedWax"), E5["M"]("E5_Brass"), E5["box"]
    for sx in (-1, 1):
        x0, x1 = sorted((sx * 0.25, sx * 5.75))
        box("SL wax %+d" % sx, x0, x1, -126.5, -126.05, -0.01, 0.035, WX, (0.012, 1))
        for y0, y1 in ((-126.58, -126.5), (-126.05, -125.97)):
            box("SL brass %+d %.0f" % (sx, y0 * 100), x0 - 0.08, x1 + 0.08, y0, y1, -0.01, 0.055, BR, (0.015, 1))
        for x in (x0 + 0.25, x1 - 0.25):
            E5["rivet"]("SL stud %+d %+.1f" % (sx, x), (x, -126.275, 0.03), "Z", BR, 0.9)
    E5["end_model"]((0, 0, 0))
    parts.append(bake("R1_SafeLines", "safe lines", col))
    drop_e5("R1_SafeLines")
    ob = join(parts, "RlWaxSeals")
    MODELS["RlWaxSeals"] = (col.name, None, ["Safe waiting inlay"])
    print("[r1] RlWaxSeals %d tris" % tris(ob))

    # strike warning, built round the first warning's place (both are the same: one FBX on both)
    c = k.frame(WARN, 0).translation
    n = 64
    circle = [(c.x + WARN_HOLE * math.cos(2 * math.pi * i / n), c.y + WARN_HOLE * math.sin(2 * math.pi * i / n))
              for i in range(n)]

    def square(h, pts):
        out = []
        for x, y in pts:
            dx, dy = x - c.x, y - c.y
            f = h / max(abs(dx), abs(dy))
            out.append((c.x + dx * f, c.y + dy * f))
        return out

    E5["begin_model"]("R1_StrikeWarning")
    WX, BR = E5["M"]("E5_RedWax"), E5["M"]("E5_Brass")   # begin_model purges orphans: fetch them again
    ring = E5["ring_prism"]
    ring("SW wax plate", square(WARN_HALF - 0.1, circle), circle, "XY", 0.0, 0.06, WX, (0.02, 1))
    corners = [(c.x + WARN_HALF * sx, c.y + WARN_HALF * sy) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
    ring("SW brass frame", corners, square(WARN_HALF - 0.16, corners), "XY", 0.0, 0.09, BR, (0.025, 1))
    ring("SW brass ring", [(c.x + (x - c.x) * 1.04, c.y + (y - c.y) * 1.04) for x, y in circle], circle, "XY",
         0.0, 0.09, BR, (0.025, 1))
    for sx in (-1, 1):
        for sy in (-1, 1):
            E5["rivet"]("SW rosette %+d%+d" % (sx, sy), (c.x + sx * 2.55, c.y + sy * 2.55, 0.06), "Z", BR, 1.8)
    E5["end_model"]((0, 0, 0))
    col = collection("R1 RlStrikeWarning")
    ob = bake("R1_StrikeWarning", "RlStrikeWarning", col)
    drop_e5("R1_StrikeWarning")
    MODELS["RlStrikeWarning"] = (col.name, WARN, [WARN])
    print("[r1] RlStrikeWarning %d tris" % tris(ob))
    # preview only: the same warning round the other stamp
    pv = collection("R1 _preview stamp warning")
    cp = ob.copy(); cp.data = ob.data.copy()
    cp.matrix_world = Matrix.Translation((2 * STAMP_X, 0, 0)) @ ob.matrix_world
    pv.objects.link(cp)


# ------------------------------------------------------------------ 3. dimensional entry vestibule
# Concept: Docs/PawnRush/ConceptArt/R687_GiantChessLibrary/AssetRefs/GC_assets_04_doors_portal.png, right: a walnut
# panelled box with a brass-capped cornice, a cream stone arch whose ring carries 24 ebony rune stones with chess
# piece silhouettes (no light), a marble floor and threshold with a brass edge, two brass ball posts at the arch foot.
# Blockout `Dimensional entry vestibule` (Unity z -137): passage x +-5 (sides x 5..6), y -145 .. -137, roof 7.7 .. 8.3,
# shoulders x +-5..9 at y -137.5 .. -136.5, runes on a 5.2 m circle, `Dimensional veil` at y -136.8 (not covered:
# RoyalLibraryPortal hides it). The user let the map follow the model (11-18): the box is the shoulders' 18 m wide,
# the passage is drawn 9.8 m high (the roof collider stays at 7.7) so the round arch (springing 4.8, intrados 4.95)
# fits like the sheet, and the runes follow the arch instead of the full circle (the blockout runes stop drawing).
VB_YF, VB_YB = -136.5, -145.2      # front (arch) face, back face
VB_H, VB_CEIL = 11.7, 9.8          # box wall top, passage ceiling
ARCH_ZS, ARCH_RI, ARCH_RO = 4.8, 4.95, 6.2
RING_Y0, RING_Y1 = -137.65, -136.25  # stone ring depth (front protrudes 0.25 from the box face)
PIECES = {
    "pawn": [[(-0.15, -0.22), (0.15, -0.22), (0.07, 0.02), (-0.07, 0.02)], ("rr", -0.11, 0.11, 0.02, 0.07),
             ("circle", 0, 0.16, 0.11)],
    "rook": [[(-0.14, -0.22), (0.14, -0.22), (0.11, 0.12), (-0.11, 0.12)],
             [(-0.17, 0.12), (0.17, 0.12), (0.17, 0.3), (0.1, 0.3), (0.1, 0.23), (0.035, 0.23), (0.035, 0.3),
              (-0.035, 0.3), (-0.035, 0.23), (-0.1, 0.23), (-0.1, 0.3), (-0.17, 0.3)]],
    "bishop": [[(-0.14, -0.22), (0.14, -0.22), (0.06, -0.02), (-0.06, -0.02)], ("rr", -0.1, 0.1, -0.03, 0.01),
               ("ellipse", 0, 0.13, 0.095, 0.14), ("circle", 0, 0.3, 0.04)],
    "knight": [[(-0.16, -0.22), (0.16, -0.22), (0.12, -0.05), (0.16, 0.05), (0.19, 0.13), (0.14, 0.16), (0.05, 0.12),
                (0.08, 0.2), (0.02, 0.3), (-0.06, 0.28), (-0.14, 0.18), (-0.17, 0.02), (-0.12, -0.1)]],
}
PIECE_ORDER = ("rook", "pawn", "knight", "bishop", "pawn", "rook", "bishop", "knight")


def _fbox(name, face, a0, a1, z0, z1, d0, d1, mat, bev=None):
    """Box on a wall face: face = (axis 'x'|'y', plane coordinate, outward sign); a along the face, d outward."""
    ax, c, s = face
    lo, hi = sorted((c + s * d0, c + s * d1))
    if ax == "x":
        return E5["box"](name, lo, hi, a0, a1, z0, z1, mat, bev)
    return E5["box"](name, a0, a1, lo, hi, z0, z1, mat, bev)


def _panelled(prefix, face, a0, a1, top, WA, BR, ST, body=True):
    """Walnut wall like the sheet: stone base course, two tiers of raised panels, riveted brass band between."""
    ax, c, s = face
    if body:
        _fbox(prefix + " body", face, a0, a1, 0, top, -0.4, 0, WA)
    n = max(1, round((a1 - a0) / 1.8))
    w = (a1 - a0) / n
    for i in range(n):
        _fbox("%s base %d" % (prefix, i), face, a0 + i * w + 0.02, a0 + (i + 1) * w - 0.02, 0, 0.8, 0, 0.12, ST, (0.04, 2))
    _fbox(prefix + " band", face, a0, a1, 3.0, 3.25, 0, 0.07, BR, (0.02, 1))
    axis = ("X+" if s > 0 else "X-") if ax == "x" else ("Y+" if s > 0 else "Y-")
    for a in E5["spaced"](a0 + 0.3, a1 - 0.3, 1.0):
        p = (c + s * 0.07, a, 3.125) if ax == "x" else (a, c + s * 0.07, 3.125)
        E5["rivet"]("%s band rivet %+.1f" % (prefix, a), p, axis, BR, 0.7)
    n = max(1, round((a1 - a0) / 2.3))
    w = (a1 - a0) / n
    for i in range(n):
        for z0, z1 in ((0.95, 2.85), (3.45, top - 0.9)):
            _fbox("%s panel %d %.0f" % (prefix, i, z0), face, a0 + i * w + 0.22, a0 + (i + 1) * w - 0.22, z0, z1,
                  0, 0.1, WA, (0.07, 2, 40))
        _fbox("%s rail %d" % (prefix, i), face, a0 + i * w, a0 + (i + 1) * w, top - 0.75, top - 0.55, 0, 0.12, WA,
              (0.03, 1))


def _piece(prefix, kind, loc, rot, a0, a1, mat):
    rr, prism = E5["rrect"], E5["prism"]
    for j, part in enumerate(PIECES[kind]):
        if isinstance(part, list):
            pts = part
        elif part[0] == "rr":
            pts = rr(part[1], part[2], part[3], part[4], 0.015, 2)
        elif part[0] == "circle":
            pts = [(part[1] + part[3] * math.cos(t * math.pi / 6), part[2] + part[3] * math.sin(t * math.pi / 6))
                   for t in range(12)]
        else:
            pts = [(part[1] + part[3] * math.cos(t * math.pi / 7), part[2] + part[4] * math.sin(t * math.pi / 7))
                   for t in range(14)]
        prism("%s %s %d" % (prefix, kind, j), pts, "XZ", a0, a1, mat, None, loc, rot)


def build_entrance():
    from mathutils import Matrix
    E5["begin_model"]("R1_Vestibule")
    M, box, lathe, prism = E5["M"], E5["box"], E5["lathe"], E5["prism"]
    E5["PALETTE"]["R1_PanelWalnut"] = dict(rgb=(0.06, 0.018, 0.008), rough=0.3, coat=0.3)
    E5["PALETTE"]["R1_Marble"] = dict(rgb=(0.82, 0.72, 0.60), rough=0.45)
    WA, BR, ST, INK = M("R1_PanelWalnut"), M("E5_Brass"), M("R1_Marble"), M("E5_BlackEnamel")
    yf, yb, H = VB_YF, VB_YB, VB_H

    # --- box walls: outside of the long sides, the passage walls inside, the back face, the front bays
    for s in (-1, 1):
        _panelled("VB side out %+d" % s, ("x", 9 * s, s), yb, yf, H, WA, BR, ST)
        _panelled("VB side in %+d" % s, ("x", 5 * s, -s), yb + 0.4, RING_Y0, VB_CEIL, WA, BR, ST)
        _panelled("VB back bay %+d" % s, ("y", yb, -1), *sorted((5.0 * s, 9.0 * s)), H, WA, BR, ST, body=False)
        _panelled("VB front bay %+d" % s, ("y", yf, 1), *sorted((6.35 * s, 9.0 * s)), H, WA, BR, ST, body=False)
        # hollow between the passage wall and the outside: closed by the front and back slabs
    # back slab with the passage opening, casing round it
    for s in (-1, 1):
        pts = [(9 * s, 0), (5 * s, 0), (5 * s, VB_CEIL), (0, VB_CEIL), (0, H), (9 * s, H)]
        prism("VB back slab %+d" % s, pts, "XZ", yb, yb + 0.4, WA)
        box("VB back casing %+d" % s, *sorted((4.95 * s, 5.45 * s)), yb - 0.15, yb + 0.4, 0, VB_CEIL + 0.5, WA, (0.05, 2))
    box("VB back casing top", -5.45, 5.45, yb - 0.15, yb + 0.4, VB_CEIL, VB_CEIL + 0.5, WA, (0.05, 2))
    # front slab: two halves round the arch's extrados
    for s in (-1, 1):
        arc = [(s * ARCH_RO * math.cos(math.radians(a)), ARCH_ZS + ARCH_RO * math.sin(math.radians(a)))
               for a in range(0, 91, 10)]
        pts = [(9 * s, 0), (ARCH_RO * s, 0)] + arc + [(0, H), (9 * s, H)]
        prism("VB front slab %+d" % s, pts, "XZ", yf - 0.4, yf, WA)

    # --- ceiling (beams across) and roof: slab, cornice, riveted brass band, corner caps
    box("VB ceiling", -5.4, 5.4, yb, RING_Y0, VB_CEIL, VB_CEIL + 0.4, WA)
    for y in E5["spaced"](yb + 0.4, RING_Y0, 1.9):
        box("VB beam %+.1f" % y, -5.0, 5.0, y - 0.25, y + 0.25, VB_CEIL - 0.4, VB_CEIL, WA, (0.05, 2))
    box("VB roof", -9.1, 9.1, yb - 0.1, yf + 0.1, H - 0.2, H + 0.25, WA)
    box("VB cornice", -9.4, 9.4, yb - 0.4, yf + 0.4, H + 0.25, H + 0.65, WA, (0.08, 2))
    for face, a0, a1 in ((("y", yf, 1), -9, 9), (("y", yb, -1), -9, 9), (("x", 9, 1), yb, yf), (("x", -9, -1), yb, yf)):
        _fbox("VB cornice band %s%+.0f" % face[:2], face, a0, a1, H - 0.5, H - 0.2, 0, 0.12, BR, (0.03, 1))
        ax, c, s = face
        axis = ("X+" if s > 0 else "X-") if ax == "x" else ("Y+" if s > 0 else "Y-")
        for a in E5["spaced"](a0 + 1.4, a1 - 1.4, 1.3):
            p = (c + s * 0.12, a, H - 0.35) if ax == "x" else (a, c + s * 0.12, H - 0.35)
            E5["rivet"]("VB cornice rivet %s%+.0f %+.1f" % (ax, c, a), p, axis, BR, 0.8)
    for sx in (-1, 1):
        for y, sy in ((yf, 1), (yb, -1)):
            x0, x1 = sorted((sx * 9.45, sx * 8.0))
            y0, y1 = sorted((y + sy * 0.45, y - sy * 1.0))
            box("VB corner cap %+d%+d" % (sx, sy), x0, x1, y0, y1, H - 0.6, H + 0.75, BR, (0.06, 2))
            E5["rivet"]("VB corner rivet %+d%+d" % (sx, sy), ((x0 + x1) / 2, (y0 + y1) / 2, H + 0.75), "Z", BR, 2.0)
            E5["rivet"]("VB corner rivet f %+d%+d" % (sx, sy), ((x0 + x1) / 2, y + sy * 0.45, H + 0.05),
                        "Y+" if sy > 0 else "Y-", BR, 2.0)

    # --- stone arch: plinths, pier blocks, nine voussoirs (keystone proud), all through the ring depth
    for s in (-1, 1):
        box("VB plinth %+d" % s, *sorted((s * 6.5, s * 4.85)), RING_Y0 - 0.15, RING_Y1 + 0.2, 0, 0.9, ST, (0.06, 2))
        for i, (z0, z1) in enumerate(((0.9, 2.2), (2.2, 3.5), (3.5, ARCH_ZS))):
            box("VB pier %+d %d" % (s, i), *sorted((s * ARCH_RO, s * ARCH_RI)), RING_Y0, RING_Y1, z0 + 0.02, z1 - 0.02,
                ST, (0.05, 2))
    for i in range(9):
        a0, a1 = 20 * i + 0.4, 20 * (i + 1) - 0.4
        ro = ARCH_RO + (0.35 if i == 4 else 0)
        outer = [(ro * math.cos(math.radians(a0 + (a1 - a0) * t / 4)), ARCH_ZS + ro * math.sin(math.radians(a0 + (a1 - a0) * t / 4)))
                 for t in range(5)]
        inner = [(ARCH_RI * math.cos(math.radians(a1 - (a1 - a0) * t / 4)), ARCH_ZS + ARCH_RI * math.sin(math.radians(a1 - (a1 - a0) * t / 4)))
                 for t in range(5)]
        y1 = RING_Y1 + (0.15 if i == 4 else 0)
        prism("VB voussoir %d" % i, outer + inner, "XZ", RING_Y0 - (0.15 if i == 4 else 0), y1, ST, (0.05, 2))

    # --- 24 rune stones along the ring's centre line (piers up, round the arch, down), both faces
    rc = (ARCH_RI + ARCH_RO) / 2
    pier, arc_len = ARCH_ZS - 1.35, math.pi * rc
    total = 2 * pier + arc_len
    for i in range(24):
        t = (i + 0.5) / 24 * total
        if t < pier:
            x, z, phi = -rc, 1.35 + t, 180.0
        elif t < pier + arc_len:
            phi = 180.0 - math.degrees((t - pier) / rc)
            x, z = rc * math.cos(math.radians(phi)), ARCH_ZS + rc * math.sin(math.radians(phi))
        else:
            x, z, phi = rc, ARCH_ZS - (t - pier - arc_len), 0.0
        rot = (0, math.radians(90.0 - phi), 0)
        kind = PIECE_ORDER[i % len(PIECE_ORDER)]
        tile = E5["rrect"](-0.41, 0.41, -0.41, 0.41, 0.12, 3)
        for side, y, sgn in (("f", RING_Y1, 1), ("b", RING_Y0, -1)):
            prism("VB rune %d %s" % (i, side), tile, "XZ", *sorted((-0.03 * sgn, 0.09 * sgn)), INK, (0.035, 2, 40),
                  (x, y, z), rot)
            _piece("VB rune %d %s" % (i, side), kind, (x, y, z), rot, *sorted((0.08 * sgn, 0.12 * sgn)), ST)

    # --- marble floor, threshold under the arch, the outside step with a brass nosing
    xs = [-4.95 + 9.9 * i / 4 for i in range(5)]
    ys = [yb + 0.4 + (RING_Y0 - yb - 0.4) * i / 3 for i in range(4)]
    for i in range(4):
        for j in range(3):
            box("VB floor %d %d" % (i, j), xs[i] + 0.015, xs[i + 1] - 0.015, ys[j] + 0.015, ys[j + 1] - 0.015, -0.02, 0.04,
                ST, (0.012, 1))
    box("VB threshold", -4.95, 4.95, RING_Y0, RING_Y1, -0.02, 0.06, ST, (0.015, 1))
    box("VB step", -5.2, 5.2, RING_Y1, RING_Y1 + 1.1, -0.02, 0.1, ST, (0.025, 1))
    E5["tube"]("VB step nosing", [(-5.2, RING_Y1 + 1.1, 0.06), (5.2, RING_Y1 + 1.1, 0.06)], 0.05, BR)
    box("VB threshold brass", -4.95, 4.95, RING_Y1 - 0.06, RING_Y1 + 0.04, -0.02, 0.075, BR, (0.015, 1))

    # --- brass ball posts on riveted plates in front of the plinths
    ball_c, ball_r = 2.84, 0.42
    prof = [(0, 0.18), (0.42, 0.18), (0.42, 0.28), (0.34, 0.36), (0.30, 0.50), (0.36, 0.58), (0.36, 0.66), (0.24, 0.74),
            (0.20, 0.90), (0.20, 1.90), (0.27, 1.98), (0.27, 2.08), (0.20, 2.14), (0.22, 2.24), (0.30, 2.30),
            (0.30, 2.38), (0.14, 2.44)]
    prof += [(ball_r * math.sin(math.pi * k / 10), ball_c - ball_r * math.cos(math.pi * k / 10)) for k in range(1, 10)]
    prof += [(0, ball_c + ball_r)]
    for s in (-1, 1):
        px, py = s * 5.75, RING_Y1 + 0.75
        box("VB post plate %+d" % s, px - 0.55, px + 0.55, py - 0.55, py + 0.55, 0, 0.18, BR, (0.04, 2))
        for dx in (-0.4, 0.4):
            for dy in (-0.4, 0.4):
                E5["rivet"]("VB post rivet %+d %+.1f%+.1f" % (s, dx, dy), (px + dx, py + dy, 0.18), "Z", BR, 0.6)
        lathe("VB post %+d" % s, prof, BR, (px, py, 0), seg=64)

    E5["end_model"]((0, 0, 0))
    col = collection("R1 RlPortal")
    LP["hero"] = .34
    ob = bake("R1_Vestibule", "RlPortal", col)
    LP.pop("hero", None)
    drop_e5("R1_Vestibule")
    MODELS["RlPortal"] = (col.name, None, ["Narrow portal passage*", "Entry shoulder*", "Portal rune*"])
    print("[r1] RlPortal %d tris" % tris(ob))
    build_veil()


# The dimensional veil (user 11-18 "게임 신호 블록아웃 지금 컨셉과 맞게"): RoyalLibraryPortal only SetActive()s the
# `Dimensional veil` object (a collider-less 10 x 9 x 0.08 teal box at y -136.8) - off when the pawn crosses the
# threshold, on again on reset; its 0.3 s flash is screen-space (OnGUI). So the model hangs on that object (shows and
# hides with it) and only its box stops drawing: a thin membrane filling the arch's opening exactly (intrados: half
# width 4.95 up to the springing 4.8, then the round head to 9.75), rippled from the middle and flat at the stone.
# No light in the model (code's job); slot "veil" (translucent, asked R0 11-18), "ink" until it exists.
VEIL_Y = -136.8


def build_veil():
    import bmesh
    from mathutils import Vector
    E5["PALETTE"]["R1_Veil"] = dict(rgb=(0.62, 0.78, 0.82), rough=0.1)
    E5["begin_model"]("R1_Veil")
    nu, nv, top = 28, 40, ARCH_ZS + ARCH_RI - 0.01
    bm = bmesh.new()
    rows = []
    for j in range(nv + 1):
        v = j / nv
        z = v * top
        hw = ARCH_RI if z <= ARCH_ZS else math.sqrt(max(ARCH_RI ** 2 - (z - ARCH_ZS) ** 2, 0.0))
        hw -= 0.02
        row = []
        for i in range(nu + 1):
            u = -1 + 2 * i / nu
            x = u * hw
            r = math.hypot(x, z - 4.6)
            edge = (1 - u * u) * min(1.0, z / 0.8) * min(1.0, (top - z) / 0.8 + 0.0)
            dy = edge * (0.16 * math.sin(r * 2.1) * math.exp(-r / 7) + 0.04 * math.sin(3.1 * x + 1.7 * z))
            row.append(bm.verts.new((x, VEIL_Y + dy, z)))
        rows.append(row)
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    o = E5["mesh_obj"]("VL membrane", bm, E5["M"]("R1_Veil"), smooth=80)
    s = o.modifiers.new("Solidify", "SOLIDIFY")
    s.thickness, s.offset = 0.03, 0
    E5["end_model"]((0, 0, 0))
    col = collection("R1 RlVeil")
    ob = bake("R1_Veil", "RlVeil", col)
    drop_e5("R1_Veil")
    MODELS["RlVeil"] = (col.name, "Dimensional veil", ["Dimensional veil"])
    print("[r1] RlVeil %d tris" % tris(ob))


# ------------------------------------------------------------------ preview / export
def preview(part, models, view="-0.7,-1,0.55", scale=0):
    os.makedirs(STILLS, exist_ok=True)
    fbx = os.path.join(STILLS, "_preview_%s.fbx" % part)
    bpy.ops.object.select_all(action="DESELECT")
    extra = [c.name for c in bpy.data.collections if c.name.startswith("R1 _preview " + part)]
    for c in [MODELS[m][0] for m in models if m in MODELS] + extra:
        for o in bpy.data.collections[c].all_objects:
            if o.type == "MESH":
                o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    out = os.path.join(STILLS, part + ".png")
    args = [bpy.app.binary_path, "-b", "--factory-startup", "--python", os.path.join(HERE, "rl_lookdev.py"), "--",
            fbx, out, view] + ([str(scale)] if scale else [])
    subprocess.run(args, check=True)
    print("[r1] still", out)


def export(models):
    for m in models:
        col, frame_name, _ = MODELS[m]
        k.export(col, m, ZONE, frame_name)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["tables"]
    parts = ["tables", "stamp", "entrance"] if "all" in argv else [p for p in ("tables", "stamp", "entrance") if p in argv]
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    built = {}
    for p in parts:
        before = set(MODELS)
        globals()["build_" + p]()
        built[p] = [m for m in MODELS if m not in before]
    if any(a.startswith("preview") for a in argv):
        for p, ms in built.items():
            if any(a.startswith("preview=") for a in argv) and "preview=" + p not in argv:
                continue
            preview(p, ms + PREVIEW_WITH.get(p, []), *PREVIEW_VIEW.get(p, ()))
    if "export" in argv:
        for ms in built.values():
            export(ms)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


PREVIEW_VIEW = {"tables": ("-0.75,-1,0.6",), "stamp": ("-0.8,-1,0.45",), "entrance": ("-0.75,1,0.35",)}
PREVIEW_WITH = {"stamp": ["RlDispatchTable"]}

if __name__ == "__main__":
    main()
