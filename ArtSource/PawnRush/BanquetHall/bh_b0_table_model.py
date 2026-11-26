"""B0 test piece (seoungtest R837, 2026-11-25; PLAN.md B0 "시험 한 점"): one bay of the long table - its south end,
Unity z 18 - 24, about the first plate - every piece after its BH_A04 cell.

    blender -b --factory-startup --python bh_b0_table_model.py -- [stills] [export]
    -> bh_b0_table.blend; with "stills": stills/b0/parts/<part>_compare.png (the sheet's cell | the render) and
       stills/b0/bay.png; with "export": the FBX (bh_kit.export; never while Unity is in Play).

Numbers are GrandBanquetHallBuilder's (LongTable): the table x 14 - 26, top 4.4; the cloth (a picture that the
tablecloth trick pulls south and lays again, pivoted at its south end: Unity "BH tablecloth" at (20, 4.405, 18)) over
x 13.85 - 26.15, the runner x 17.5 - 22.5; the plate (a floor: collider radius 1.2, top 4.52) at (20, 29); the guests'
chairs' block x 11.6 - 14, 2.2 high, a guest every 4 m from z 21 (one at 29). Nothing is above a walking face but the
goblets and candelabra, which stand off the runner by the table's edges (pictures, no collider).
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\BanquetHall"
sys.path.insert(0, HERE)
import importlib
import bh_kit as k
import bh_parts as P
importlib.reload(k); importlib.reload(P)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TABLE_Y, X0, X1, RUNNER_X = 4.4, 14.0, 26.0, 20.0
Z0, Z1 = 18.0, 36.0            # the piece: the table's south end over three plates (23, 29, 35) - its length shows
PLATE_Z, PLATE_R = 23.0, 1.2
PLATES = (23.0, 29.0, 35.0)
GUESTS = (21.0, 25.0, 29.0, 33.0)
CLOTH_OUT, DROP_TO = .15, .2   # the cloth's overhang past the table's edge; its hem's height over the floor (the sheet: nearly down)
SHEET = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R836_BanquetHallAssets", "AssetRefs",
                     "BH_A04_long_table.png")
# the sheet's cells (x0, y0, x1, y1 in its pixels)
CELLS = {"table": (10, 330, 460, 700), "plate": (460, 440, 735, 700), "goblet": (760, 365, 925, 700),
         "candelabra": (945, 290, 1270, 700), "chair": (1262, 255, 1535, 730)}


def U(x, y, z):
    """Unity section point -> Blender."""
    return Vector((x, z, y))


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    for o in list(c.objects):
        bpy.data.objects.remove(o)
    return c


# ------------------------------------------------------------------ the table's body (static)

def table_body(col):
    """Walnut under the cloth (seen while the cloth is pulled): a top slab, an apron with a gilded ogee moulding under
    its edge, recessed panels framed in gold beads between walnut stiles, a plinth with a gold fillet."""
    bm = bmesh.new()
    y_top = TABLE_Y
    # the core, a hair inside the collider so the cloth sits on it
    P.merge(bm, P.box(U(X0 + .02, 0, Z0), U(X1 - .02, y_top - .02, Z1), P.WALNUT))
    P.merge(bm, P.box(U(X0, y_top - .18, Z0), U(X1, y_top, Z1), P.WALNUT))                 # the top's edge
    for side, x in ((-1, X0), (1, X1)):
        # apron moulding under the top's edge (ogee, gold), running along z
        prof = [(a * 1.0, b) for a, b in P.ogee(.07, .16)]
        pts = [U(x - side * .0, y_top - .36, z) for z in (Z0, Z1)]
        mo = P.sweep(pts, [(-side * a if side < 0 else -a, b) for a, b in prof], P.GOLD)
        P.merge(bm, mo)
        # plinth
        P.merge(bm, P.box(U(min(x, x + side * .06), 0, Z0), U(max(x, x + side * .06), .32, Z1), P.WALNUT))
        P.merge(bm, P.box(U(min(x, x + side * .075), .32, Z0), U(max(x, x + side * .075), .36, Z1), P.GOLD))
        # panels: 1.5 m wide between 0.25 m stiles, sunk 4 cm, a gold bead frame
        for i in range(int((Z1 - Z0 - .25) / 1.5)):
            za, zb = Z0 + .125 + i * 1.5 + .125, Z0 + .125 + (i + 1) * 1.5 - .125
            ya, yb = .6, y_top - .6
            sunk = side * -.04
            P.merge(bm, P.box(U(min(x, x + sunk), ya, za), U(max(x, x + sunk) + (.001 if side > 0 else 0), yb, zb), P.WALNUT))
            frame = [U(x + side * .012, ya, za), U(x + side * .012, ya, zb), U(x + side * .012, yb, zb), U(x + side * .012, yb, za)]
            P.merge(bm, P.tube(frame + [frame[0]], .018, 6))
    ob = P.obj(col, "BH0 table body", bm)
    return P.finish(ob, bevel=.012)


# ------------------------------------------------------------------ the cloth and the runner (they move: the trick)

def fold(s, t):
    """The skirt's still folds: how far out at s (metres along the hem's line) and t (0 at the table's edge .. 1 at
    the hem)."""
    a = .075 * t ** 1.3
    return a * (math.sin(2 * math.pi * s / 1.15) + .35 * math.sin(2 * math.pi * s / .53 + 1.3))


def perimeter(step=.12):
    """The skirt's line round the table's south end (Unity x, z): up the west side from the bay's north end, round the
    south-west corner, across the end, round the south-east corner, up the east side. [(x, z, nx, nz, s), ...] with
    the outward normal and the distance along it."""
    o = CLOTH_OUT
    pts = []
    def run(a, b):
        n = max(1, int((b - a).length / step))
        return [a + (b - a) * i / n for i in range(n)]
    seq = []
    seq += [(p, Vector((-1, 0))) for p in run(Vector((X0 - o, Z1)), Vector((X0 - o, Z0)))]
    for i in range(8):
        t = math.pi + math.pi / 2 * i / 8
        seq.append((Vector((X0, Z0)) + Vector((math.cos(t), math.sin(t))) * o, Vector((math.cos(t), math.sin(t)))))
    seq += [(p, Vector((0, -1))) for p in run(Vector((X0, Z0 - o)), Vector((X1, Z0 - o)))]
    for i in range(8):
        t = math.pi * 1.5 + math.pi / 2 * i / 8
        seq.append((Vector((X1, Z0)) + Vector((math.cos(t), math.sin(t))) * o, Vector((math.cos(t), math.sin(t)))))
    seq += [(p, Vector((1, 0))) for p in run(Vector((X1 + o, Z0)), Vector((X1 + o, Z1)))]
    seq.append((Vector((X1 + o, Z1)), Vector((1, 0))))
    s = 0.0
    for i, (p, n) in enumerate(seq):
        if i:
            s += (p - seq[i - 1][0]).length
        pts.append((p.x, p.y, n.x, n.y, s))
    return pts


ROLL = .2   # the cloth rolls over the table's edge in its first 0.2 m of drop (the sheet's soft edge)


def skirt_at(pp, t, out=0.0):
    x, z, nx, nz, s = pp
    y = TABLE_Y + .01 - (TABLE_Y + .01 - DROP_TO) * t
    d = TABLE_Y + .01 - y
    roll = .02 + (CLOTH_OUT - .02) * math.sin(min(1.0, d / ROLL) * math.pi / 2)
    f = fold(s, t) + out + roll - CLOTH_OUT
    return U(x + nx * f, y, z + nz * f)


def cloth(col):
    """The cloth (BH_A04): the top over the table and past its edges, the skirt falling in still folds to 0.55 m over
    the floor round the west side, the south end and the east side, a gold hem, a gold line over it and a border of
    fleurs-de-lis between C-scrolls above it."""
    objs = []
    per = perimeter()
    top = TABLE_Y + .01
    bm = bmesh.new()
    nt = 26
    nt = 22
    grid = [[bm.verts.new(skirt_at(pp, (j / nt) ** 1.7)) for j in range(nt + 1)] for pp in per]
    for i in range(len(per) - 1):
        for j in range(nt):
            f = bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
            f.material_index = P.LINEN
            f.smooth = True
    ring = [g[0] for g in grid]
    bm.faces.new(ring).material_index = P.LINEN   # the top, an n-gon over the skirt's top line (triangulated on export)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < -.5 or (abs(f.normal.z) > .5 and f.calc_center_median().z > top - .02 and f.normal.z < 0):
            f.normal_flip()
    sk = P.obj(col, "BH0 cloth", bm)
    objs.append(P.finish(sk))
    # the gold: on the folds, just proud of the cloth
    gb = bmesh.new()
    drop = top - DROP_TO
    def line(height_over_hem, r, out=.012):
        t = 1.0 - height_over_hem / drop
        return P.tube([skirt_at(pp, t, out) for pp in per], r, 6)
    P.merge(gb, line(.035, .04))
    P.merge(gb, line(.15, .014))
    total = per[-1][4]
    spacing = .86
    s = .5
    k_i = 0
    while s < total - .3:
        pp = min(per, key=lambda q: abs(q[4] - s))
        # not on the corners' bend
        corner = abs(pp[2]) > .1 and abs(pp[3]) > .1
        if not corner:
            t_foot = 1.0 - .17 / drop
            at = skirt_at(pp, t_foot, .02)
            ds = .05
            a = skirt_at(min(per, key=lambda q: abs(q[4] - (s + ds))), t_foot, .02)
            b = skirt_at(min(per, key=lambda q: abs(q[4] - (s - ds))), t_foot, .02)
            along = (a - b).normalized()
            up = Vector((0, 0, 1))
            out = along.cross(up)
            m = Matrix((along, up, out)).transposed().to_4x4()
            m.translation = at
            f = P.fleur(.8, .035)
            P.transform(f, m)
            P.merge(gb, f)
            # a pair of C-scrolls halfway to the next fleur
            mid = min(per, key=lambda q: abs(q[4] - (s + spacing / 2)))
            if abs(mid[2]) < .1 or abs(mid[3]) < .1:
                mt = skirt_at(mid, t_foot, .02)
                ma = skirt_at(min(per, key=lambda q: abs(q[4] - (s + spacing / 2 + ds))), t_foot, .02)
                mb = skirt_at(min(per, key=lambda q: abs(q[4] - (s + spacing / 2 - ds))), t_foot, .02)
                al = (ma - mb).normalized()
                mm = Matrix((al, up, al.cross(up))).transposed().to_4x4()
                mm.translation = mt
                for side in (-1, 1):
                    c = [Vector((side * .02, .0, 0)), Vector((side * .13, .04, 0)), Vector((side * .16, .17, 0)),
                         Vector((side * .09, .25, 0)), Vector((side * .04, .19, 0)), Vector((side * .08, .14, 0))]
                    sc = P.tube(P.curve(c, 6), .022, 6)
                    P.transform(sc, mm)
                    P.merge(gb, sc)
        s += spacing
        k_i += 1
    gold = P.obj(col, "BH0 cloth border", gb)
    objs.append(P.finish(gold))
    return objs


def runner(col):
    """The runner (BH_A04): olive velvet down the middle of the cloth in a gold border, hanging over the table's south
    end in a shallow point with a gold tassel."""
    hw, edge = 2.5, .16
    y0 = TABLE_Y + .012
    zs = Z0 - CLOTH_OUT - .1      # the hanging part's plane, proud of the skirt's folds
    hang = 1.9                    # how far down the end it hangs (to its sides), the point 0.3 m lower
    # the runner's outline in its own "unrolled" 2D: u across (-hw..hw), v along from its point (0) up the end and
    # along the top (v = hang + .1 is the edge, beyond is the top)
    def place(u, v, lift=0.0):
        edge_v = hang + .32
        if v <= edge_v:   # hanging
            return U(RUNNER_X + u, TABLE_Y + .012 - (edge_v - v), zs - lift)
        return U(RUNNER_X + u, y0 + lift, Z0 - CLOTH_OUT + (v - edge_v))
    vmax = hang + .32 + (Z1 - Z0 + CLOTH_OUT)
    point = lambda u: .3 * (1 - abs(u) / hw)   # the shallow point: v of the bottom edge at u
    bm = bmesh.new()
    nu = 20
    vs = [0.0] + [hang + .32 - .02, hang + .32, hang + .32 + .02] + [vmax]
    rows = []
    for i in range(nu + 1):
        u = -hw + 2 * hw * i / nu
        b = .3 - point(u)
        col_v = [b] + [v for v in vs[1:]]
        rows.append([bm.verts.new(place(u, v)) for v in col_v])
    for i in range(nu):
        for j in range(len(rows[0]) - 1):
            f = bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
            f.material_index = P.OLIVE
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # the gold border: a flat band 0.16 m inside the outline, a little proud
    outline = [(-hw + edge / 2, vmax)] + [(-hw + edge / 2, .3 - point(-hw + edge / 2) + edge / 2), (0.0, edge / 2),
                                          (hw - edge / 2, .3 - point(hw - edge / 2) + edge / 2)] + [(hw - edge / 2, vmax)]
    edge_v = hang + .32
    pts = []
    for (ua, va), (ub, vb) in zip(outline, outline[1:]):
        n = max(2, int(math.hypot(ub - ua, vb - va) / .1))
        for i in range(n):
            pts.append((ua + (ub - ua) * i / n, va + (vb - va) * i / n))
    pts.append(outline[-1])
    # a flat ribbon edge wide along the outline, lying on the runner (its normal: up on the top, south on the end)
    rb = bmesh.new()
    pairs = []
    for i, (u, v) in enumerate(pts):
        a2 = pts[min(i + 1, len(pts) - 1)]
        b2 = pts[max(i - 1, 0)]
        du, dv = a2[0] - b2[0], a2[1] - b2[1]
        ln = math.hypot(du, dv) or 1.0
        cu, cv = -dv / ln * edge / 2, du / ln * edge / 2
        pairs.append((rb.verts.new(place(u - cu, v - cv, .006)), rb.verts.new(place(u + cu, v + cv, .006))))
    for (a1, b1), (a3, b3) in zip(pairs, pairs[1:]):
        try:
            rb.faces.new((a1, b1, b3, a3)).material_index = P.GOLD
        except ValueError:
            pass
    bmesh.ops.recalc_face_normals(rb, faces=rb.faces)
    P.merge(bm, rb)
    # the tassel at the point
    tip = place(0.0, 0.0, .02)
    tas = k.lathe([(0.0, 0.0), (.05, 0.0), (.09, -.05), (.07, -.12), (.12, -.4), (.0, -.42)], 12, mat=P.GOLD)
    P.transform(tas, Matrix.Translation(tip))
    P.merge(bm, tas)
    ob = P.obj(col, "BH0 runner", bm)
    return P.finish(ob)


# ------------------------------------------------------------------ ware and chair

def ware(col):
    """Each plate (a floor) and, round it off the runner, a goblet and a candelabrum by each edge of the table (the
    same setting every plate)."""
    objs, wicks = [], []
    for i, z in enumerate(PLATES):
        tag = "" if i == 0 else " %d" % (i + 1)
        pl = P.obj(col, "BH0 plate" + tag, P.plate(PLATE_R, .12), Matrix.Translation(U(RUNNER_X, TABLE_Y, z)))
        objs.append(P.finish(pl))
        for name, x, dz in (("BH0 goblet W", 16.1, -3.6), ("BH0 goblet E", 23.9, -.4)):
            g = P.obj(col, name + tag, P.goblet(1.3), Matrix.Translation(U(x, TABLE_Y + .02, z + dz)))
            objs.append(P.finish(g))
        for name, x, dz in (("BH0 candelabra W", 15.25, -.6), ("BH0 candelabra E", 24.75, -3.4)):
            bm, w = P.candelabra(3.0)
            m = Matrix.Translation(U(x, TABLE_Y + .02, z + dz)) @ Matrix.Rotation(math.pi / 2, 4, "Z")
            c = P.obj(col, name + tag, bm, m)
            wicks += [m @ q for q in w]
            objs.append(P.finish(c))
    return objs, wicks


def guest_chair(col):
    """The chair of the guest at z 21 (the builder's first), facing the table; its seat on the chairs' block (2.2)."""
    out = []
    for i, z in enumerate(GUESTS):
        m = Matrix.Translation(U(12.8, 0, z))
        tag = "" if i == 0 else " %d" % (i + 1)
        hard, soft = P.chair(5.4, 2.6, 2.3, seat=2.2)
        out.append(P.finish(P.obj(col, "BH0 chair" + tag, hard, m), bevel=.01))
        pad = P.obj(col, "BH0 chair pads" + tag, soft, m)
        k.bevel(pad, .06, 4, 30.0, harden=False)
        k.apply_modifiers(pad)
        out.append(P.finish(pad))
    return out


# ------------------------------------------------------------------ stills

def setup_render(scene, res=900):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    try:
        scene.cycles.device = "GPU"
    except TypeError:
        pass
    scene.render.resolution_x = scene.render.resolution_y = res
    scene.render.film_transparent = False
    w = scene.world or bpy.data.worlds.new("W")
    scene.world = w
    w.use_nodes = True
    # a studio: the camera sees white (as the sheet's paper), reflections see a soft light top and a darker floor
    nt = w.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_WORLD":
            nt.nodes.remove(n)
    out = next(n for n in nt.nodes if n.type == "OUTPUT_WORLD")
    lp, mix, white = nt.nodes.new("ShaderNodeLightPath"), nt.nodes.new("ShaderNodeMixShader"), nt.nodes.new("ShaderNodeBackground")
    white.inputs[0].default_value, white.inputs[1].default_value = (1, 1, 1, 1), 1.6
    studio, tc, sep, ramp = (nt.nodes.new("ShaderNodeBackground"), nt.nodes.new("ShaderNodeTexCoord"),
                             nt.nodes.new("ShaderNodeSeparateXYZ"), nt.nodes.new("ShaderNodeValToRGB"))
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Z"], ramp.inputs[0])
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .42, (.30, .26, .22, 1)
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .62, (1.0, .97, .92, 1)
    nt.links.new(ramp.outputs["Color"], studio.inputs[0])
    studio.inputs[1].default_value = 2.0
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(studio.outputs[0], mix.inputs[1])
    nt.links.new(white.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    scene.view_settings.exposure = -.55   # the white china and linen keep their shading; the paper stays white (below)
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError as e:
        print("[bh_b0] view transform:", e)
    for o in [o for o in scene.objects if o.type in ("LIGHT", "CAMERA")]:
        bpy.data.objects.remove(o)
    for name, rot, energy, size in (("key", (math.radians(50), 0, math.radians(-35)), 4.0, 8.0),
                                    ("fill", (math.radians(60), 0, math.radians(120)), 1.5, 15.0)):
        ld = bpy.data.lights.new(name, "SUN")
        ld.energy = energy
        ld.angle = math.radians(size)
        ld.color = (1.0, .95, .88) if name == "key" else (.9, .94, 1.0)
        lo = bpy.data.objects.new(name, ld)
        lo.rotation_euler = rot
        scene.collection.objects.link(lo)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def frame(cam, objs, yaw, pitch, lens=50, pad=1.12):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    centre = (lo + hi) / 2
    radius = (hi - lo).length / 2 * pad
    cam.data.lens = lens
    fov = 2 * math.atan(18 / lens)
    dist = radius / math.sin(fov / 2)
    d = Vector((math.cos(pitch) * math.sin(yaw), -math.cos(pitch) * math.cos(yaw), math.sin(pitch)))
    cam.location = centre + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = dist * 4


def render_only(scene, objs, path):
    keep = set(objs)
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o not in keep
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def compare(cell, render_path, out):
    """The sheet's cell beside the render, same height, white between."""
    import numpy as np
    sys.path.insert(0, os.path.join(HERE, "..", "CarriageYardModels"))
    import rcy_textures as rt
    sheet = rt.load_png(SHEET)[..., :3]
    x0, y0, x1, y1 = CELLS[cell]
    a = sheet[y0:y1, x0:x1]
    b = rt.load_png(render_path)[..., :3]
    hgt = 700
    def fit(img):
        h, w = img.shape[:2]
        return rt.resample(img, 1) if False else _resize(img, hgt, int(w * hgt / h))
    a, b = fit(a), fit(b)
    gap = np.ones((hgt, 30, 3), np.float32)
    img = np.concatenate([a, gap, b], 1)
    img = np.concatenate([img, np.ones(img.shape[:2] + (1,), np.float32)], -1)
    rt.save_png(img.astype(np.float32), out)


def _resize(a, h, w):
    import numpy as np
    H, W = a.shape[:2]
    ys = np.clip(((np.arange(h) + .5) * H / h).astype(int), 0, H - 1)
    xs = np.clip(((np.arange(w) + .5) * W / w).astype(int), 0, W - 1)
    return a[ys][:, xs].astype(np.float32)


def main():
    scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o)
    body = table_body(collection("BH0 table body"))
    cloth_col = collection("BH0 cloth")
    cl = cloth(cloth_col)
    rn = runner(cloth_col)
    ware_objs, wicks = ware(collection("BH0 ware"))
    ch = guest_chair(collection("BH0 chair"))
    k.preview_materials()
    print("[bh_b0] tris", k.tris(scene.objects), "wicks", len(wicks))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b0_table.blend"))
    if "stills" in ARGS:
        out = os.path.join(HERE, "stills", "b0", "parts")
        os.makedirs(out, exist_ok=True)
        cam = setup_render(scene)
        by = {o.name: o for o in scene.objects}
        jobs = (("plate", [by["BH0 plate"]], math.radians(-20), math.radians(50), 70),
                ("goblet", [by["BH0 goblet W"]], math.radians(-20), math.radians(14), 70),
                ("candelabra", [by["BH0 candelabra W"]], math.radians(90), math.radians(8), 70),
                ("chair", [by["BH0 chair"], by["BH0 chair pads"]], math.radians(55), math.radians(14), 70),
                ("table", [body, rn] + cl, math.radians(30), math.radians(28), 40))
        for name, objs, yaw, pitch, lens in jobs:
            frame(cam, objs, yaw, pitch, lens)
            path = os.path.join(out, name + "_render.png")
            render_only(scene, objs, path)
            compare(name, path, os.path.join(out, name + "_compare.png"))
        # the whole bay
        allm = [o for o in scene.objects if o.type == "MESH"]
        frame(cam, allm, math.radians(-28), math.radians(30), 35, .92)
        scene.render.resolution_x, scene.render.resolution_y = 1200, 1500
        render_only(scene, allm, os.path.join(HERE, "stills", "b0", "bay.png"))
    if "export" in ARGS:
        export_full()


def export_full():
    """The whole table for Unity (GrandBanquetHallDressing.Table.cs, zone B2): the body to the dais (z 56), the cloth to
    its north end (z 50, the trick's area) with the runner to 49.5, the builder's five plates and nine guests' chairs.
    The cloth goes out in the trick's picture's frame ("BH tablecloth"), the rest in the section's."""
    global Z1, PLATES, GUESTS
    scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o)
    PLATES = (23.0, 29.0, 35.0, 41.0, 47.0)
    GUESTS = tuple(21.0 + 4.0 * i for i in range(9))
    Z1 = 56.0
    table_body(collection("BH2 body"))
    Z1 = 50.0
    cloth_col = collection("BH2 cloth")
    cloth(cloth_col)
    Z1 = 49.5
    runner(cloth_col)
    _, wicks = ware(collection("BH2 ware"))
    guest_chair(collection("BH2 chairs"))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b2_table_export.blend"))
    if not os.path.exists(k.DUMP):
        # No Unity dump yet: the trick's picture's frame from the builder's numbers (LongTable: "BH tablecloth" at
        # (mid x, TableY + .005, TableZ0) under "BH tablecloth trick" at Content's origin, not turned).
        k.frame = lambda name, index=0: Matrix.Translation(U((X0 + X1) * .5, TABLE_Y + .005, 18.0))
    k.export("BH2 body", "bh_table_body", "B2")
    k.export("BH2 cloth", "bh_table_cloth", "B2", "BH tablecloth")
    k.export("BH2 ware", "bh_table_ware", "B2")
    k.write_lamps("B2", "bh_table_ware", wicks)
    k.export("BH2 chairs", "bh_table_chairs", "B2")


if "export" in ARGS and "stills" not in ARGS:
    export_full()
else:
    main()
