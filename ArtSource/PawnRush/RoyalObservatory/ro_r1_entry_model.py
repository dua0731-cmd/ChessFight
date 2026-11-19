"""R1 part 5 (seoungtest R725, 2026-11-19): the entry tunnel's floor, its threshold and its balustrades (RO_1 / RO_2
bottom left: a cream marble walk with a violet runner of gold-inlaid panels between gold balustrades with pearl lamps;
RO_A1's balustrade piece).

    blender -b --factory-startup --python ro_r1_entry_model.py -- [--stills] [--export]

Graybox (ro_blockout.json, "0 Entry tunnel"): "Entry floor" x -6 .. 6, z 0 .. 14 (Unity), its top y 0; "Entry threshold"
x -5.5 .. 1.5, z 14 .. 16.2, its top y -0.01 (under the first star platform's rim); rails 0.3 thick and 1.1 high:
"Entry rail W" x -6.3 .. -6, "Entry rail E" x 6 .. 6.3 (z 0 .. 14), "Entry rail N w" x -6.3 .. -5.5 and "Entry rail N e"
x 1.5 .. 6.3 (z 14 .. 14.3). All flush with those tops and footprints (their colliders stay):
  floor: a 6 m runner down the middle in panels about 3 x 2.8 m, each its own square motif (ro_kit.pick_cell: no two
         neighbours alike), framed by gold bands; cream marble slabs either side (1.5 m, a gold seam between), a gold
         strip at the threshold;
  threshold: marble with a gold edge and a gold star let into it;
  rails: ro_parts.rail on each graybox rail's line, its posts' finials and, on the long rails' middle posts, pearl lamps
         (light: RoyalObservatoryDressing).
-> ro_r1_entry.blend, stills/r1/, --export Assets/Resources/RoyalObservatory/Models/Dipper/RoEntryWalk.fbx (+ _lamps.txt),
in the section's frame; RoyalObservatoryDressing.Dipper.cs covers "Entry floor", "Entry threshold" and "Entry rail *".
"""
import math, os, sys
import bmesh, bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as P    # noqa: E402
from ro_parts import GOLD, MARBLE, MOTIF, VIOLET, _merge, _transform, star_stud  # noqa: E402

MODEL, ZONE = "RoEntryWalk", "Dipper"
X0, X1, Z0, Z1 = -6.0, 6.0, 0.0, 14.0
RUN = 3.0                 # the runner's half width
# The walk's drawn top 1 cm under the walking face (its collider's top, y 0): the first star platform's gold rim reaches
# 0.2 m over the walk's north end at y 0 too, and the two flickered in Unity (11-19, the user: "깜빡깜빡 거림")
TOP = -.03   # 3 cm (was 1): under the star's gold lip, whose outer slope passes -0.011 at 4.1 m (11-19, still flickering)
BAND = .18                # its gold frame


def quad(bm, x0, x1, y0, y1, z, mat):
    vs = [bm.verts.new((x0, y0, z)), bm.verts.new((x1, y0, z)), bm.verts.new((x1, y1, z)), bm.verts.new((x0, y1, z))]
    f = bm.faces.new(vs)
    f.material_index = mat
    return f


def floor(col):
    """The walk's top (Blender: x, y = Unity z, z 0): marble slabs, the runner's gold frame and its motif panels."""
    bm = bmesh.new()
    # marble either side of the runner, slabs 1.5 m with gold seams 4 cm
    for s in (-1, 1):
        xa, xb = sorted((s * (RUN + BAND), s * X1))
        rows = int(round((Z1 - Z0) / 1.5))
        for j in range(rows):
            y0, y1 = Z0 + (Z1 - Z0) * j / rows, Z0 + (Z1 - Z0) * (j + 1) / rows
            cols = 2
            for i in range(cols):
                x0, x1 = xa + (xb - xa) * i / cols, xa + (xb - xa) * (i + 1) / cols
                quad(bm, x0 + .02, x1 - .02, y0 + .02, y1 - .02, TOP, MARBLE)
        # seams: the gaps as thin gold quads flush
        for j in range(rows + 1):
            y = Z0 + (Z1 - Z0) * j / rows
            quad(bm, xa, xb, max(Z0, y - .02), min(Z1, y + .02), TOP, GOLD)
        for i in range(3):
            x = xa + (xb - xa) * i / 2
            quad(bm, x - .02 if i else x, x + .02 if i < 2 else x, Z0, Z1, TOP, GOLD)
    # the runner's frame
    for s in (-1, 1):
        xa, xb = sorted((s * RUN, s * (RUN + BAND)))
        quad(bm, xa, xb, Z0, Z1, TOP, GOLD)
    quad(bm, -RUN, RUN, Z1 - BAND, Z1, TOP, GOLD)
    # the walk's body under its top (the graybox box is 1 m deep; only its upper part shows at the tunnel's mouth)
    # its sides and bottom only, the sides up to the drawn top's edge: a cap 4 mm under the top flickered with it at a
    # glancing view (11-19, the user: "바닥 옆 쪽이 깜빡 거려")
    body = k.prism([(X0, Z0), (X1, Z0), (X1, Z1), (X0, Z1)], -.6, TOP, MARBLE, cap=False)
    bot = [v for v in body.verts if v.co.z < -.5]
    bot.sort(key=lambda v: math.atan2(v.co.y - (Z0 + Z1) / 2, v.co.x))
    body.faces.new(list(reversed(bot))).material_index = MARBLE
    _merge(bm, body)
    ob = k.new_object("Entry floor top", bm, col, P.SLOT_ORDER)
    k.surface_uvs(ob)
    # the runner's panels: two across, five along, each its own motif (a quad each, mapped whole to its cell)
    bm = bmesh.new()
    across, along = 2, 5
    pw = 2 * RUN / across
    ph = (Z1 - BAND - Z0) / along
    panels = []
    for j in range(along):
        for i in range(across):
            x0, y0 = -RUN + pw * i, Z0 + ph * j
            quad(bm, x0 + .03, x0 + pw - .03, y0 + .03, y0 + ph - .03, TOP, MOTIF)
            panels.append((x0 + pw * .5, y0 + ph * .5))
        # a gold line between the rows and the two columns
        quad(bm, -RUN, RUN, y0 + ph - .03, y0 + ph + .03, TOP, GOLD) if j < along - 1 else None
    quad(bm, -.03, .03, Z0, Z1 - BAND, TOP, GOLD)
    run = k.new_object("Entry runner", bm, col, P.SLOT_ORDER)
    k.surface_uvs(run)
    run.data.update()
    for n, (cx, cy) in enumerate(panels):
        key = "Entry floor#%d" % n
        cell = k.pick_cell(key, "square", Vector((cx, cy, 0)), reach=3.2)
        idx = [p.index for p in run.data.polygons if p.material_index == MOTIF
               and abs(p.center.x - cx) < pw * .4 and abs(p.center.y - cy) < ph * .4]
        k.motif_planar(run, idx, cell, Vector((cx, cy, TOP)), (1, 0, 0), (0, 1, 0), pw - .06, ph - .06,
                       k.pick_turn(key, cell, "square"))
    return [ob, run]


def threshold(col):
    """The tongue under the first star's rim: marble 1 cm under the walk's top, gold edges along its sides - only where
    the star platform does not cover it. (11-19, the user's screen recording: the tongue's marble, 1 cm under the star's
    enamel top, flickered through it. The part under the star - its rim reaches 4.2 m from (-2, 18) - is cut away, so
    nothing lies under the star's top; a 0.1 m grid keeps the cut's edge under the star's gold lip.)"""
    x0, x1, y0, y1, top = -5.5, 1.5, 14.0, 16.2, TOP
    star, cover = Vector((-2.0, 18.0)), 4.22   # past the star's whole rim (lip and torus end at 4.2 m)
    bm = bmesh.new()
    nx, ny = int((x1 - x0) / .1), int((y1 - y0) / .1)
    for i in range(nx):
        for j in range(ny):
            a0, a1 = x0 + (x1 - x0) * i / nx, x0 + (x1 - x0) * (i + 1) / nx
            b0, b1 = y0 + (y1 - y0) * j / ny, y0 + (y1 - y0) * (j + 1) / ny
            if (Vector(((a0 + a1) * .5, (b0 + b1) * .5)) - star).length < cover - .12:
                continue
            gold = a0 < x0 + .1 - 1e-6 or a1 > x1 - .1 + 1e-6
            f = bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=.5)
            for v in f["verts"]:
                v.co = Vector((a0 if v.co.x < 0 else a1, b0 if v.co.y < 0 else b1, top + (.003 if gold else 0)))
            for face in {fa for v in f["verts"] for fa in v.link_faces}:
                face.material_index = GOLD if gold else MARBLE
    # the cut: every vertex inside the circle moved out onto it, so the edge is a smooth arc round the star's rim
    for v in bm.verts:
        d = Vector((v.co.x, v.co.y)) - star
        if d.length < cover:
            d = d.normalized() * cover
            v.co.x, v.co.y = star.x + d.x, star.y + d.y
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-4)
    # the south face, down from the top, where the walk's edge meets it
    # (no face of its own along its south edge: the walk's north side face meets it there; two faces 2 mm apart flickered)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = k.new_object("Entry threshold", bm, col, P.SLOT_ORDER)
    k.surface_uvs(ob)
    return [ob]


def rails(col):
    objs, lamps = [], []
    # the long rails: on the graybox rail's middle line; posts every ~4.7 m, lamps on the middle two
    for name, x in (("Entry rail W", -6.15), ("Entry rail E", 6.15)):
        mids = (Z0 + (Z1 - Z0) / 3, Z0 + 2 * (Z1 - Z0) / 3)
        objs += P.rail(col, name, (x, Z0), (x, Z1), TOP, 1.1 - TOP, lamp_posts=[y - Z0 for y in mids])
        for y in mids:
            got, at = P.lamp_post(col, "%s lamp %.0f" % (name, y), (x, y, 1.1 + .12 + .02), height=1.0, globe=.17)
            objs += got
            lamps.append(at)
    objs += P.rail(col, "Entry rail N w", (-6.3, 14.15), (-5.5, 14.15), TOP, 1.1 - TOP, end_post=False)   # standing on the drawn floor, its top the graybox rail's
    objs += P.rail(col, "Entry rail N e", (1.5, 14.15), (6.3, 14.15), TOP, 1.1 - TOP, start_post=False)
    return objs, lamps


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    k.motif_reset()
    col = bpy.data.collections.new("RO R1 entry")
    bpy.context.scene.collection.children.link(col)
    objs = floor(col) + threshold(col)
    r, lamps = rails(col)
    objs += r
    print("[r1 entry]", len(objs), "objects,", k.tris(objs), "tris; floor", k.tris(objs[:3]), "rails", k.tris(r))
    return col, objs, lamps


def stills(out, lamps):
    import ro_r0_star_model as r0
    r0.LAMPS[:] = lamps
    os.makedirs(out, exist_ok=True)
    shots = {
        "r1_entry_walk": (Vector((0, -6.0, 3.2)), Vector((0, 8.0, 0)), 26),
        "r1_entry_over": (Vector((9.0, -4.0, 9.0)), Vector((0, 7.0, 0)), 30),
        "r1_entry_rail_close": (Vector((3.6, 3.2, 1.6)), Vector((6.15, 6.0, .7)), 35),
    }
    r0.stills([], out, shots=shots, centre=Vector((0, 0, 0)), floor_at=(0, 7, -18), fill_offset=(4, 2, 8))


LAMPS = []

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    col, objs, LAMPS[:] = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r1_entry.blend"))
    if "--export" in argv:
        k.export(col.name, MODEL, ZONE)
        k.write_lamps(ZONE, MODEL, LAMPS)
    if "--stills" in argv:
        stills(os.path.join(HERE, "stills", "r1"), LAMPS)
