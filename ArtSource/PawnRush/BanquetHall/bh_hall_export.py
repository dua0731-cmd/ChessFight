"""B7 / B6 / B5 to Unity (bh-hall session, seoungtest R838, 2026-11-25; the user: "유니티 내보내서 걸기까지 진행해").

    blender -b --factory-startup --python bh_hall_export.py [-- --only <model>]

Builds the hall as the assemblies do (bh_b7_hall_assembly, bh_b6_assembly, bh_b5_assembly - every part at the
builder's numbers), gives every distinct part its levels (bh_lod.levels_for: LOD0 within 2 mm or the original, LOD1 /
LOD2 kept where they save a third) and writes Assets/Resources/BanquetHall/Models/<zone>/<model>.fbx with each placed
part as "<part> <nnn>_LOD<k>" (one level: no suffix). GrandBanquetHallDressing.Hall.cs gives every such part its own
LODGroup in Play (Unity's importer would make one for the whole FBX, as big as the hall, which never switches).

Frames (bh_kit.export's: Blender x, y, z = Unity x, z, y of the frame, -Z forward, Y up, metres):
  - the section (Content) for everything that stands still - the models hang on Content (the Tripo arch's baked
    colour goes out as Models/Hall/bh_hall_entry_basecolor.png, 2048 px, put on by .Hall.cs);
  - a hanging chandelier in its "BH chandelier look" (the rope's pivot, turned with the rope): bh_chandelier_65 / _80;
  - a curtain pleat in its "BH pleat S1 .. N10" root (x -24.5, y 0, its z): bh_curtain_pleat.
Nothing is written while Unity is in Play (Editor.log). The candles (wax) go out; the stills' fire, smoke and lights
("(lit)") do not - the hearths' fire is Unity's (.Hall.cs).
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_kit                        # noqa: E402
import bh_lod                        # noqa: E402
import bh_b7_hall_model as h         # noqa: E402
import bh_b7_hall_assembly as a7     # noqa: E402
import bh_b6_assembly as b6          # noqa: E402
import bh_b6_chandelier_model as cm  # noqa: E402
import bh_b5_stage_model as st       # noqa: E402
import bh_b5_assembly as b5          # noqa: E402

ROOT = bh_kit.ROOT

# Kit key -> (zone, model); None: not exported from the hall build (built in its own frame below, or not at all)
GROUPS = {
    "panel": ("Hall", "bh_hall_walls"), "window": ("Hall", "bh_hall_walls"), "south back": ("Hall", "bh_hall_walls"),
    "north back": ("Hall", "bh_hall_walls"), "arch_north": ("Hall", "bh_hall_walls"), "frieze": ("Hall", "bh_hall_walls"),
    "arch_tripo": ("Hall", "bh_hall_entry"),
    "pilaster": ("Hall", "bh_hall_pilasters"), "sconce": ("Hall", "bh_hall_pilasters"), "banner": ("Hall", "bh_hall_pilasters"),
    "fireplace": ("Hall", "bh_hall_fireplaces"),
    "ceiling": None, "rose": ("Hall", "bh_hall_ceiling"),      # (the ceiling goes out coffer by coffer, below)
    "loft face": ("Chandeliers", "bh_loft"), "loft rail": ("Chandeliers", "bh_loft"), "loft ladder": ("Chandeliers", "bh_loft"),
    "loft body": ("Chandeliers", "bh_loft"),
    "rod": ("Chandeliers", "bh_chandelier_rods"), "great": ("Chandeliers", "bh_great_chandelier"),
    "hanging": None,
    "stage front E": ("Stage", "bh_stage"), "stage front S": ("Stage", "bh_stage"), "stage front N": ("Stage", "bh_stage"),
    "stage top": ("Stage", "bh_stage"),
    "pleat": None, "rail": ("Stage", "bh_curtain_rail"), "pelmet": ("Stage", "bh_curtain_rail"),
    "bass": ("Stage", "bh_double_basses"), "buffet": ("Stage", "bh_buffet"), "cake": ("Stage", "bh_buffet"),
    "fountain": ("Stage", "bh_fountain"),
}


def exported(o):
    return o.type == "MESH" and ("(lit)" not in o.name or "candles (lit)" in o.name)


# ------------------------------------------------------------------ the hall, tagged

_place = a7.Kit.place


def _tagged_place(self, key, builder, m, name=None):
    out = _place(self, key, builder, m, name)
    k = key[0] if isinstance(key, tuple) else key
    for o in out:
        o["bh_group"] = k
    return out


a7.Kit.place = _tagged_place


def build_hall():
    hall, lamps = a7.assemble()
    kit = a7.Kit(hall)
    b6.loft(kit)
    b6.chandeliers(kit, lamps)
    b5.stage(kit)
    b5.curtain(kit)
    b5.basses(kit)
    b5.buffet(kit, lamps)
    return hall


# ------------------------------------------------------------------ levels

_levels = {}


def evaluated(o):
    """The object's mesh with its modifiers and triangles (custom normals kept), in its own space."""
    tm = o.modifiers.new("bh export triangulate", "TRIANGULATE")
    tm.quad_method, tm.ngon_method, tm.keep_custom_normals = "BEAUTY", "BEAUTY", True
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    o.modifiers.remove(tm)
    return me


def levels_of(o):
    """[mesh per kept level] for this object's part (computed once per part: copies share the template's mesh)."""
    key = o.data.name
    if key not in _levels:
        me = evaluated(o)
        scale = Matrix.Diagonal((*o.matrix_world.to_scale(), 1))
        lv, info, _ = bh_lod.levels_for(me, scale, o.data.name)
        kept = [0]
        for k in ((1, 2) if len(info["tris"]) == 4 else ()):        # (a small part has the one level)
            if info["tris"][k + 1] <= bh_lod.KEEP_LEVEL * info["tris"][kept[-1] + 1]:
                kept.append(k)
        _levels[key] = [lv[k] for k in kept]
        print("BH_EXPORT levels", o.data.name, [bh_lod.tri_count(m) for m in _levels[key]])
    return _levels[key]


def lod_objects(o, base, col, matrix=None):
    out = []
    lvs = levels_of(o)
    for k, me in enumerate(lvs):
        name = base if len(lvs) == 1 else "%s_LOD%d" % (base, k)
        c = bpy.data.objects.new(name, me)
        col.objects.link(c)
        c.matrix_world = matrix if matrix is not None else o.matrix_world
        out.append(c)
    return out


# ------------------------------------------------------------------ writing

def unity_in_play():
    log = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Unity", "Editor", "Editor.log")
    if not os.path.exists(log):
        return False
    p = b = -1
    with open(log, "rb") as f:
        for i, line in enumerate(f):
            if b"Reloading assemblies for play mode" in line:
                p = i
            elif b"Temp/__Backupscenes" in line:
                b = i
    return p > b


def write_fbx(objs, zone, model, tripo=False):
    """bh_kit.export's settings and .meta (stable GUIDs). tripo: the Tripo arch keeps its own material (its baked
    colour is Resources/BanquetHall/Models/Hall/bh_hall_entry_basecolor.png, put on it by .Hall.cs - Unity did not
    take the texture embedded in the FBX), so the slot check is skipped."""
    for rel in ("Assets/Resources/BanquetHall", bh_kit.OUT_REL, bh_kit.OUT_REL + "/" + zone):
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            os.makedirs(os.path.join(ROOT, rel), exist_ok=True)
            bh_kit._write(rel + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (bh_kit._guid(rel), bh_kit.FOLDER_META))
    if not tripo:
        bh_kit.check_slots(objs, model)
    rel = "%s/%s/%s.fbx" % (bh_kit.OUT_REL, zone, model)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, rel), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        with open(bh_kit.FBX_META_TEMPLATE, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + bh_kit._guid(rel)
        bh_kit._write(rel + ".meta", "\n".join(lines))
    tris = sum(bh_lod.tri_count(o.data) for o in objs if not o.name.endswith(("_LOD1", "_LOD2")))
    print("BH_EXPORT wrote", rel, len(objs), "objects", tris, "tris (nearest level)")


def hall_floor(col):
    """The hall's floor outside the dance floor (B4's, x -32 .. 11.6, z 6.2 .. 56, 2 cm over it): cream marble slabs
    2.4 m square with walnut joints, a walnut band with a gold line along the walls - all flush with the builder's
    floor (Unity y 0), faces only on top."""
    m = h.Mesh()
    X0, X1, Z0, Z1, S, J, B = -32.0, 32.0, 0.0, 72.0, 2.4, .05, .8
    dance = (-32.0, 11.6, 6.2, 56.0)
    mi = {k: m.mat(k) for k in ("marble", "floor_dark", "gold")}

    def quad(slot, x0, x1, y0, y1):
        if x1 - x0 < 1e-4 or y1 - y0 < 1e-4:
            return
        # cut out the dance floor's rectangle (B4 lies there)
        if x0 >= dance[0] - 1e-6 and x1 <= dance[1] + 1e-6 and y0 >= dance[2] - 1e-6 and y1 <= dance[3] + 1e-6:
            return
        bm = m.bm
        vs = [bm.verts.new((x, y, 0.0)) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]
        h.face(bm, vs, mi[slot])

    # the band along the walls: walnut, a gold line 0.06 inside it
    for (a0, a1, b0, b1) in ((X0, X1, Z0, Z0 + B), (X0, X1, Z1 - B, Z1), (X0, X0 + B, Z0 + B, Z1 - B), (X1 - B, X1, Z0 + B, Z1 - B)):
        quad("floor_dark", a0, a1, b0, b1)
    g = .06
    for (a0, a1, b0, b1) in ((X0 + B, X1 - B, Z0 + B, Z0 + B + g), (X0 + B, X1 - B, Z1 - B - g, Z1 - B),
                             (X0 + B, X0 + B + g, Z0 + B + g, Z1 - B - g), (X1 - B - g, X1 - B, Z0 + B + g, Z1 - B - g)):
        quad("gold", a0, a1, b0, b1)
    # the slabs inside: a grid from the middle line, each cell clipped to the field and to the dance floor's edges
    fx0, fx1, fz0, fz1 = X0 + B + g, X1 - B - g, Z0 + B + g, Z1 - B - g
    xs = sorted({fx0, fx1, dance[1]} | {x for x in (S * i for i in range(-14, 15)) if fx0 < x < fx1})
    zs = sorted({fz0, fz1, dance[2], dance[3]} | {z for z in (S * i for i in range(0, 31)) if fz0 < z < fz1})
    for x0, x1 in zip(xs, xs[1:]):
        for z0, z1 in zip(zs, zs[1:]):
            jl = J / 2 if abs(x0 / S - round(x0 / S)) < 1e-6 else 0
            jr = J / 2 if abs(x1 / S - round(x1 / S)) < 1e-6 else 0
            jb = J / 2 if abs(z0 / S - round(z0 / S)) < 1e-6 else 0
            jt = J / 2 if abs(z1 / S - round(z1 / S)) < 1e-6 else 0
            quad("marble", x0 + jl, x1 - jr, z0 + jb, z1 - jt)
            quad("floor_dark", x0, x0 + jl, z0, z1)
            quad("floor_dark", x1 - jr, x1, z0, z1)
            quad("floor_dark", x0 + jl, x1 - jr, z0, z0 + jb)
            quad("floor_dark", x0 + jl, x1 - jr, z1 - jt, z1)
    return m.finish("BH hall floor", col, bevel=0, merge=True)


def musicians(col):
    """The orchestra behind the curtain and the two bassists in the pit, at the builder's places and heights (its
    musicians 4.6 m to the head's top on the stage at 7.4, its bassists 4.4 m on the pit's floor): bh-path's
    figurines from the user's Tripo set (bh_figures: violinist - white pawn, bassist - black rook), facing the hall
    (+x). Pictures, as the builder's (no colliders)."""
    import bh_figures as F
    out = []
    spots = [("violinist", (-29.0, 33.05, 7.4), 4.6), ("violinist", (-29.0, 38.05, 7.4), 4.6),
             ("violinist", (-27.0, 43.05, 7.4), 4.6), ("violinist", (-29.0, 46.05, 7.4), 4.6),
             ("bassist", (-21.4, 19.0, 0.0), 4.4), ("bassist", (-21.4, 27.05, 0.0), 4.4)]
    tpl = {}
    for name, at, height in spots:
        if name not in tpl:
            ob = F.figure(name, col, obj_name="BH " + name)
            zs = [v.co.z for v in ob.data.vertices]
            k = height / (max(zs) - min(zs))
            ob.data.transform(Matrix.Diagonal((k, k, k, 1)))
            tpl[name] = ob
            ob.matrix_world = Matrix.Translation(at) @ Matrix.Rotation(math.pi / 2, 4, "Z")
            out.append(ob)
        else:
            c = tpl[name].copy()
            col.objects.link(c)
            c.matrix_world = Matrix.Translation(at) @ Matrix.Rotation(math.pi / 2, 4, "Z")
            out.append(c)
    return out


def alighting_post(col):
    """The alighting lamp's post at the stage's front (the builder's "BH alighting post": a 0.3 x 0.3 x 1.8 box, not to
    be climbed, its globe at 2.1 lights as a queen comes to stand - the globe stays the builder's), in its root's frame
    (feet at 0, z up). The stage front's style and the dais's boarding lamps' (bh_b3_dais_model.newel(lamp=True)):
    a walnut plinth with a gold band, a walnut shaft with gold-framed flutes on each face, a stepped gold capital, a
    turned gold neck with a knop and the tulip cup the globe sits in (its beaded rim at 1.85, the globe's foot at 1.8)."""
    m = h.Mesh()
    h.box(m, "walnut", -.15, .15, -.15, .15, 0, .22)
    h.prism_yz(m, "walnut", [(-.15, .22), (.15, .22), (.13, .27), (-.13, .27)], -.15, .15)
    h.box(m, "gold", -.14, .14, -.14, .14, .27, .32)
    h.box(m, "walnut", -.11, .11, -.11, .11, .32, 1.42)
    for z0, z1, r in ((.32, .36, .125), (1.36, 1.42, .125)):
        h.box(m, "gold", -r, r, -r, r, z0, z1)
    # each face: a sunk gold frame and two gold flutes inside it
    for ang in range(4):
        rot = Matrix.Rotation(math.radians(90 * ang), 4, "Z")
        k = len(m.bm.verts)
        for x in (-.035, .035):
            h.tube(m, "gold", [Vector((x, .112, .5)), Vector((x, .112, 1.25))], .014, sides=6)
        h.tube(m, "gold", [Vector((-.075, .113, .44)), Vector((.075, .113, .44)), Vector((.075, .113, 1.31)),
                           Vector((-.075, .113, 1.31)), Vector((-.075, .113, .44))], .01, sides=5)
        m.bm.verts.ensure_lookup_table()
        bmesh.ops.transform(m.bm, matrix=rot, verts=m.bm.verts[k:])
    # the capital: stepped out to the box's edge
    h.box(m, "gold", -.13, .13, -.13, .13, 1.42, 1.48)
    h.box(m, "gold", -.15, .15, -.15, .15, 1.48, 1.56)
    # a turned gold neck with a knop, and a tulip cup flaring round the globe's foot (its rim at 1.83)
    h.lathe(m, "gold", [(0.0, 1.56), (.09, 1.56), (.07, 1.59), (.05, 1.6), (.085, 1.63), (.09, 1.65), (.05, 1.68),
                        (.045, 1.7), (.07, 1.72), (.13, 1.75), (.19, 1.78), (.235, 1.81), (.25, 1.83), (.255, 1.845),
                        (.235, 1.85), (.2, 1.82), (.12, 1.79), (0.0, 1.785)], segs=32)
    for z, r in ((1.845, .255),):
        h.tube(m, "gold", [Vector((r * math.cos(2 * math.pi * t / 32), r * math.sin(2 * math.pi * t / 32), z)) for t in range(33)],
               .012, sides=6)
    return m.finish("BH alighting post", col, bevel=.008, merge=False)


# (the way's carpets and arrows are Unity decals - .Hall.cs BanquetWayDecals, user 11-25 - not a model)
EXTRAS = {"bh_hall_floor": ("Hall", lambda col: [hall_floor(col)]), "bh_musicians": ("Stage", musicians),
          "bh_alighting_post": ("Stage", lambda col: [alighting_post(col)])}


def export_extras(names):
    for model in names:
        zone, build = EXTRAS[model]
        tpl = bpy.data.collections.new("tpl " + model)
        bpy.context.scene.collection.children.link(tpl)
        col = bpy.data.collections.new("export " + model)
        bpy.context.scene.collection.children.link(col)
        out = []
        for i, o in enumerate(build(tpl)):
            out += lod_objects(o, "%s %03d" % (o.name.split(".")[0], i), col)
        write_fbx(out, zone, model)


def export_all(only=None):
    if only is None or only in EXTRAS:
        export_extras([only] if only else list(EXTRAS))
        if only:
            return
    hall = build_hall()
    groups = {}
    for o in hall.objects:
        g = GROUPS.get(o.get("bh_group"))
        if g is None or not exported(o):
            continue
        groups.setdefault(g, []).append(o)
    for (zone, model), objs in sorted(groups.items()):
        if only and model != only:
            continue
        col = bpy.data.collections.new("export " + model)
        bpy.context.scene.collection.children.link(col)
        out = []
        for i, o in enumerate(objs):
            base = "%s %03d" % (o.name.split(".i")[0], i)
            out += lod_objects(o, base, col)
        if model == "bh_hall_ceiling":
            # the coffers one by one (8 x 9, each 8 m): as one mesh the ceiling's LODGroup would be the hall's size
            # and never switch
            tpl = bpy.data.collections.new("tpl coffer")
            bpy.context.scene.collection.children.link(tpl)
            m = h.Mesh()
            h.coffer(m, full=False, at=(0, 0))
            cof = m.finish("BH hall ceiling coffer", tpl, bevel=.06, merge=False)
            n = 0
            for i in range(8):
                for j in range(9):
                    out += lod_objects(cof, "BH hall ceiling coffer %03d" % n, col,
                                       Matrix.Translation((a7.HX0 + 4 + 8 * i, 4 + 8 * j, a7.CEIL)))
                    n += 1
        write_fbx(out, zone, model, tripo=(model == "bh_hall_entry"))
    # the hanging chandeliers, in the look's frame (the pivot; the chandelier hangs down -z)
    for L, model in ((6.5, "bh_chandelier_65"), (8.0, "bh_chandelier_80")):
        if only and model != only:
            continue
        tpl = bpy.data.collections.new("tpl " + model)
        bpy.context.scene.collection.children.link(tpl)
        col = bpy.data.collections.new("export " + model)
        bpy.context.scene.collection.children.link(col)
        out = []
        for i, o in enumerate(x for x in cm.hanging(tpl, L) if exported(x)):
            out += lod_objects(o, "%s %03d" % (o.name, i), col)
        write_fbx(out, "Chandeliers", model)
    # a curtain pleat in its root's frame: the root at (x -24.5, y 0, its z); the model's foot at PleatBottom 7.7
    if not only or only == "bh_curtain_pleat":
        tpl = bpy.data.collections.new("tpl pleat")
        bpy.context.scene.collection.children.link(tpl)
        m = h.Mesh()
        st.pleat(m)
        o = m.finish("BH curtain pleat", tpl, bevel=0, merge=False)
        col = bpy.data.collections.new("export pleat")
        bpy.context.scene.collection.children.link(col)
        write_fbx(lod_objects(o, "BH curtain pleat 000", col, b5.facing((1, 0), (0, 0, b5.PLEAT_BOTTOM))),
                  "Stage", "bh_curtain_pleat")
    pivots, great = a7.rope_pivots()
    print("BH_EXPORT rope pivots z", [round(z, 3) for z in pivots], "great chandelier z", round(great, 3))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if unity_in_play():
        raise SystemExit("BH_EXPORT Unity is in Play - nothing written (Editor.log)")
    export_all(args[args.index("--only") + 1] if "--only" in args else None)
    print("BH_EXPORT done")
