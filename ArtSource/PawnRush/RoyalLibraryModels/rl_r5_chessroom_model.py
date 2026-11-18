"""R5 chess room architecture (seoungtest R687, 2026-11-18, PLAN.md R5): the giant oak table, the commanders' chairs, the
room's walls / ceiling / doors, bookcases, reading nook and the final exit walkway, to concept D (GC_D1 ~ GC_D5).

    blender -b --factory-startup --python rl_r5_chessroom_model.py -- <model ...|all> [--preview <dir>] [--no-export]

Every model is built in the blockout's frame (Blender x, y, z = Unity x, z, y, metres) at the prefab pieces' places and
sizes (WarRoomComplete, read by rl_blockout), faces in the rl_kit slots only, joined into one mesh a model, metre UVs
(rl_kit.surface_uvs) last, FBX by rl_kit.export to Assets/Resources/RoyalLibrary/Models/ChessRoom/RlRoom<Model>.fbx.
--preview <dir> writes the FBX under <dir> instead of Assets (for a look while Unity is in Play); --no-export skips it.
Saves rl_r5_chessroom.blend with every model built in this run. Never export into Assets while Unity is in Play.

Rules kept (PLAN.md 3): walking tops, colliders and moving axes are the prefab's. The oak table's top stays at y -0.8
(the live board's squares lie on it in Play), nothing rises more than 0.4 m above a walking face. The play-time board,
warning tiles, entry / exit platforms, the exit arch, pieces and giants are GiantChessCourse.cs's (not here). The four
conservatory plants wait for Tripo and are not covered.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rl_kit as k   # noqa: E402

ZONE = "ChessRoom"
LATHE_SEGS = 24


# ------------------------------------------------------------------ mesh helpers

def _obj(col, name, bm, mat, smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    me.materials.append(k.slot(mat))
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def box(col, name, lo, hi, mat, bevel=0.0, segs=1):
    """Axis box from corner lo to corner hi (Blender metres), optional bevel on every edge."""
    lo, hi = Vector(lo), Vector(hi)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    size = hi - lo
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(lo + hi) / 2, verts=bm.verts)
    if bevel > 0:
        b = min(bevel, min(size) * .45)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, segments=segs, profile=.5, affect="EDGES", clamp_overlap=True)
    return _obj(col, name, bm, mat)


def lathe(col, name, profile, centre, mat, segs=LATHE_SEGS, axis="Z"):
    """Turned piece: profile [(radius, height)] from bottom to top about a vertical axis through centre (x, y);
    a radius 0 at an end closes it. axis "X" / "Y" lays it along that axis instead (height = coordinate on it)."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, h))])
            continue
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), h))
                      for i in range(segs)])
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            for i in range(segs):
                bm.faces.new((a[0], b[i], b[(i + 1) % segs]))
        elif len(b) == 1:
            for i in range(segs):
                bm.faces.new((a[i], b[0], a[(i + 1) % segs]))
        else:
            for i in range(segs):
                bm.faces.new((a[i], a[(i + 1) % segs], b[(i + 1) % segs], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if axis == "X":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"))
    elif axis == "Y":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, "X"))
    bmesh.ops.translate(bm, vec=Vector(centre), verts=bm.verts)
    return _obj(col, name, bm, mat, smooth=True)


def rivet(col, name, loc, normal, mat, r=.35):
    """A low dome rivet on a face pointing along `normal` ("+x", "-y", ...)."""
    ob = lathe(col, name, [(r, 0), (r * .85, r * .35), (r * .45, r * .62), (0, r * .7)], (0, 0, 0), mat, segs=8)
    ax, sign = normal[1], (1 if normal[0] == "+" else -1)
    rot = {"z": Matrix.Identity(4), "x": Matrix.Rotation(math.pi / 2, 4, "Y"), "y": Matrix.Rotation(-math.pi / 2, 4, "X")}[ax]
    if sign < 0:
        rot = Matrix.Rotation(math.pi, 4, "X" if ax != "x" else "Z") @ rot
    ob.matrix_world = Matrix.Translation(loc) @ rot
    return ob


def dome(col, name, M, r, mat, segs=8):
    """A dome stud of radius r, its base at the origin of matrix M, rising along M's local +z."""
    ob = lathe(col, name, [(r, 0), (r * .7, r * .6), (0, r * .85)], (0, 0, 0), mat, segs=segs)
    ob.matrix_world = M
    return ob


def frame_of(origin, u, v):
    """Matrix whose local x = u, y = v, z = u x v, at origin (Blender vectors)."""
    u, v = Vector(u).normalized(), Vector(v).normalized()
    n = u.cross(v)
    m = Matrix((u, v, n)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def stud_row(col, name, M, a, b, step, r, mat):
    """Brass nail heads from local point a to b (on M's local z = 0 face, pointing +z), about `step` apart."""
    a, b = Vector((tuple(a) + (0,))[:3]), Vector((tuple(b) + (0,))[:3])
    n = max(1, round((b - a).length / step))
    for i in range(n + 1):
        p = a.lerp(b, i / n)
        dome(col, name, M @ Matrix.Translation(p), r, mat, segs=5)


def pad(col, name, M, U, V, T, cells=(4, 4), dimple=.45, roll=None, mat="leather", buttons="brass", button_r=.32,
        res=3, top=None, bulge=0.0):
    """Tufted leather pad (GC_assets_03): local u across U, v across V, the puffed face at z = 0 and the flat back at
    z = -T, placed by matrix M. Edges roll down by `roll`; buttons sit on a diamond lattice (cells nu x nv) in pits,
    with the pleats running diagonally between them (a cos x cos pillow field); `bulge` domes the whole face.
    top(u) -> local v of the top edge gives an arched top (the rows follow it)."""
    nu, nv = cells
    gu, gv = nu * res, nv * res
    R = roll if roll is not None else min(T * .7, U / 4, V / 4)
    cu, cv = U / nu, V / nv
    vt_of = top or (lambda u: V / 2)

    def edge(u, v):
        return min(U / 2 - abs(u), v + V / 2, vt_of(u) - v)

    def height(u, v):
        e = edge(u, v)
        h = bulge * (1 - (2 * u / U) ** 2) * max(0.0, 1 - ((v) / (V / 2 + 1e-6)) ** 2)
        if e < R:
            h -= R - math.sqrt(max(0.0, R * R - (R - max(e, 0.0)) ** 2))
        g = math.cos(math.pi * (u + U / 2) / cu) * math.cos(math.pi * (v + V / 2) / cv)
        w = min(1.0, max(0.0, (e - .25 * min(cu, cv)) / (.5 * min(cu, cv))))
        w = w * w * (3 - 2 * w)
        return h - dimple * w * ((1 + g) / 2) ** 3
    pts = []
    for j in range(nv + 1):
        for i in range(nu + 1):
            u, v = -U / 2 + i * cu, -V / 2 + j * cv
            if (i + j) % 2 == 0 and edge(u, v) > .7 * min(cu, cv):
                pts.append(Vector((u, v)))
    bm = bmesh.new()
    grid = []
    for j in range(gv + 1):
        row = []
        for i in range(gu + 1):
            u = -U / 2 + U * i / gu
            v = -V / 2 + (vt_of(u) + V / 2) * j / gv
            row.append(bm.verts.new((u, v, height(u, v))))
        grid.append(row)
    for j in range(gv):
        for i in range(gu):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    ring = [grid[0][i] for i in range(gu)] + [grid[j][gu] for j in range(gv)] +            [grid[gv][i] for i in range(gu, 0, -1)] + [grid[j][0] for j in range(gv, 0, -1)]
    low = [bm.verts.new((vt.co.x, vt.co.y, -T)) for vt in ring]
    n = len(ring)
    for i in range(n):
        bm.faces.new((ring[(i + 1) % n], ring[i], low[i], low[(i + 1) % n]))
    bm.faces.new(list(reversed(low)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.transform(M)
    ob = _obj(col, name, bm, mat, smooth=True)
    if buttons:
        for p in bm_pts(pts, height):
            dome(col, name + " button", M @ Matrix.Translation(p), button_r, buttons, segs=6)
    return ob


def bm_pts(pts, height):
    return [Vector((p.x, p.y, height(p.x, p.y) - .05)) for p in pts]


def scroll(col, name, M, r, w, mat, turns=1.6, segs=40):
    """A carved scroll end (GC_assets_03 chair arms, chaise): a flat spiral band of width w (local z) in the local
    xy plane, outer radius r, winding inward."""
    bm = bmesh.new()
    th = r * .26
    prev = None
    for i in range(segs + 1):
        a = 2 * math.pi * turns * i / segs
        rr = r * (1 - .62 * i / segs)
        t = th * (1 - .5 * i / segs)
        c, s = math.cos(a), math.sin(a)
        q = [bm.verts.new((rr * c, rr * s, -w / 2)), bm.verts.new(((rr - t) * c, (rr - t) * s, -w / 2)),
             bm.verts.new(((rr - t) * c, (rr - t) * s, w / 2)), bm.verts.new((rr * c, rr * s, w / 2))]
        if prev:
            for kq in range(4):
                bm.faces.new((prev[kq], prev[(kq + 1) % 4], q[(kq + 1) % 4], q[kq]))
        else:
            bm.faces.new(q)
        prev = q
    bm.faces.new(list(reversed(prev)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # the inner disc the band winds round (the scroll's eye), and a backing disc so it reads solid
    bm.transform(M)
    ob = _obj(col, name, bm, mat, smooth=True)
    lathe_m(col, name + " eye", M, [(0, -w / 2), (r * .42, -w / 2), (r * .42, w / 2 + .1), (0, w / 2 + .25)], mat)
    return ob


def lathe_m(col, name, M, profile, mat, segs=LATHE_SEGS):
    """lathe() about M's local z axis."""
    ob = lathe(col, name, profile, (0, 0, 0), mat, segs=segs)
    ob.matrix_world = M
    return ob


def collection(name):
    old = bpy.data.collections.get(name)
    if old:
        for o in list(old.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


# The room's shell models are seen from inside only (11-18, 승규 "코스 외관 필요 없는 부분 지워"): every face that faces
# out of the room from within the shell's skin is dropped - the backs of every wall piece and of what hangs on it
# (window glass and bars, panels, frames: they face the wall, so no camera in the room can see them), the tops at
# the ceiling, the ceiling's top and its beams' tops, the floor's underside. With the prefab walls hidden these were
# what showed from outside. Near a room corner only the wall's own axis counts (a side wall's pieces facing the front
# wall stay).
OUTSIDE_STRIP = {"RlRoomWalls": (3.0, 3.0, .02, .02), "RlRoomCeiling": (.02, .02, 3.6, .02),
                 "RlRoomFloor": (.02, .02, .02, 1.2)}      # skin depth: x walls, y walls, ceiling (+z), floor (-z)
ROOM_BOUNDS = ((0, -123.0, 123.0), (1, -9.0, 169.0), (2, -22.0, 58.0))   # Blender axis, low, high


def strip_outside(ob, skin):
    dx, dy, dtop, dbot = skin
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    gone = []

    def near(c, ax, d):
        lo, hi = ROOM_BOUNDS[ax][1:]
        return c[ax] >= hi - d or c[ax] <= lo + d
    for f in bm.faces:
        n, c = f.normal, f.calc_center_median()
        out = False
        for ax, lo, hi in ROOM_BOUNDS:
            d = (dx, dy)[ax] if ax < 2 else 0
            if ax < 2:
                other = 1 - ax
                corner = d > .1 and near(c, other, (dx, dy)[other])     # in the other wall's skin too: leave it
                if not corner and ((n[ax] > .9 and c[ax] >= hi - d) or (n[ax] < -.9 and c[ax] <= lo + d)):
                    out = True
                elif (n[ax] > .9 and c[ax] >= hi - .02) or (n[ax] < -.9 and c[ax] <= lo + .02):
                    out = True
            else:
                if (n[2] > .9 and c[2] >= hi - dtop) or (n[2] < -.9 and c[2] <= lo + dbot):
                    out = True
            if out:
                break
        if out:
            gone.append(f)
    bmesh.ops.delete(bm, geom=gone, context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    return len(gone)


PAINTING_ASPECT = 4 / 5     # RL_Portrait's picture (1024 x 1280)


def painting_uvs(ob):
    """Faces in the "RL | painting" slot get the whole picture once across them, seen from the room (-y side):
    u 0 at the left (-x) to 1 at the right, v 0 at the bottom to 1 at the top. The canvas is a little narrower than
    4:5, so the picture keeps its proportions and loses a strip at each side."""
    idx = [i for i, m in enumerate(ob.data.materials) if m and m.name == "RL | painting"]
    if not idx:
        return
    me = ob.data
    faces = [p for p in me.polygons if p.material_index in idx]
    xs = [me.vertices[v].co.x for p in faces for v in p.vertices]
    zs = [me.vertices[v].co.z for p in faces for v in p.vertices]
    x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
    w = (x1 - x0) / ((z1 - z0) * PAINTING_ASPECT)          # the canvas's share of the picture's width (<= 1)
    uv = me.uv_layers[0].data
    for p in faces:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = ((1 - w) / 2 + w * (co.x - x0) / (x1 - x0), (co.z - z0) / (z1 - z0))


def finish(col, model):
    """Apply transforms, join the collection into one mesh named after the model, one slot per material, metre UVs."""
    obs = [o for o in col.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
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
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.to_mesh(ob.data)
    bm.free()
    if model in OUTSIDE_STRIP:
        before = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        n = strip_outside(ob, OUTSIDE_STRIP[model])
        print("[r5] %s: %d faces facing out of the room dropped (%d -> %d tris)"
              % (model, n, before, sum(len(p.vertices) - 2 for p in ob.data.polygons)))
    k.surface_uvs(ob)
    painting_uvs(ob)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print("[r5] %s: %d tris, slots %s" % (model, tris, [m.name for m in ob.data.materials]))
    return ob, tris


# ------------------------------------------------------------------ the giant oak table (D2, D3, D5 table corner)
# Prefab: "Giant oak table" x -41..41, Unity z -10..102, y -4.8..-0.8 (top; the live board lies on it in Play);
# four "Table leg" 7 x 7 m, y -22..-2, centres x +-35, z -4 / 96, "Brass leg collar" y -20..-18. Floor top y -22.

TABLE = dict(x=41.0, y0=-10.0, y1=102.0, top=-0.8, bottom=-4.8)


def build_table(col):
    t = TABLE
    X, Y0, Y1, TOP, BOT = t["x"], t["y0"], t["y1"], t["top"], t["bottom"]
    box(col, "top slab", (-X, Y0, BOT), (X, Y1, TOP), "varnished", bevel=.25, segs=2)
    # brass bands round the slab: one flush with the top, a bead along the bottom
    for z0, z1, out in ((TOP - .9, TOP, .18), (BOT, BOT + .45, .14)):
        # beside the slab (never over its top: no shared faces to flicker), the long sides round the corners
        box(col, "rim x-", (-X - out, Y0 - out, z0), (-X + .01, Y1 + out, z1 - .01), "brass", bevel=.06)
        box(col, "rim x+", (X - .01, Y0 - out, z0), (X + out, Y1 + out, z1 - .01), "brass", bevel=.06)
        box(col, "rim y-", (-X + .01, Y0 - out, z0 + .01), (X - .01, Y0 + .01, z1 - .02), "brass", bevel=.06)
        box(col, "rim y+", (-X + .01, Y1 - .01, z0 + .01), (X - .01, Y1 + out, z1 - .02), "brass", bevel=.06)
    # brass corner caps (D5): an L of plates down the corner, a thin plate on the top (0.15 above the top: never a step)
    C, T = 6.5, .4
    for sx in (-1, 1):
        for sy, yc in ((-1, Y0), (1, Y1)):
            xo, yo = sx * (X + T), yc + sy * T
            box(col, "cap side x", (min(sx * (X - .2), xo), min(yc, yc - sy * C), BOT - .3),
                (max(sx * (X - .2), xo), max(yc, yc - sy * C), TOP + .02), "brass", bevel=.12, segs=2)
            box(col, "cap side y", (min(sx * X, sx * (X - C)), min(yc - sy * .2, yo), BOT - .3),
                (max(sx * X, sx * (X - C)), max(yc - sy * .2, yo), TOP + .02), "brass", bevel=.12, segs=2)
            box(col, "cap top", (min(sx * (X + T), sx * (X - C)), min(yo, yc - sy * C), TOP - .05),
                (max(sx * (X + T), sx * (X - C)), max(yo, yc - sy * C), TOP + .15), "brass", bevel=.1)
            for i, f in enumerate((.3, .62)):
                zz = BOT + (TOP - BOT) * (.28 + .44 * i)
                for j, d in enumerate((1.4, C - 1.4)):
                    rivet(col, "cap rivet", Vector((xo, yc - sy * d, zz)), ("+" if sx > 0 else "-") + "x", "brass", .42)
                    rivet(col, "cap rivet", Vector((sx * (X - d), yo, zz)), ("+" if sy > 0 else "-") + "y", "brass", .42)
    # aprons between the leg blocks and their brass bead
    AX, AY0, AY1, AZ = 38.6, -7.6, 99.6, -8.4
    for sx in (-1, 1):
        box(col, "apron x", (sx * AX - .6, AY0, AZ), (sx * AX + .6, AY1, BOT), "walnut", bevel=.15)
        box(col, "apron bead x", (sx * AX - .8, AY0, AZ - .35), (sx * AX + .8, AY1, AZ + .1), "brass", bevel=.08)
    for yy in (AY0, AY1):
        box(col, "apron y", (-AX, yy - .6, AZ), (AX, yy + .6, BOT), "walnut", bevel=.15)
        box(col, "apron bead y", (-AX, yy - .8, AZ - .37), (AX, yy + .8, AZ + .08), "brass", bevel=.08)
    # turned legs (D3, D5): square block, brass collar, bulb, brass foot on the floor
    for lx in (-35, 35):
        for ly in (-4, 96):
            box(col, "leg block", (lx - 4, ly - 4, -9.2), (lx + 4, ly + 4, BOT), "walnut", bevel=.35, segs=2)
            box(col, "leg block band", (lx - 4.15, ly - 4.15, -9.6), (lx + 4.15, ly + 4.15, -9.0), "brass", bevel=.1)
            lathe(col, "leg neck", [(0, -10.6), (3.0, -10.6), (3.4, -10.1), (3.6, -9.6), (0, -9.6)], (lx, ly, 0), "walnut")
            lathe(col, "leg collar", [(0, -11.6), (3.2, -11.6), (3.8, -11.3), (3.8, -10.8), (3.2, -10.5), (0, -10.5)],
                  (lx, ly, 0), "brass")
            lathe(col, "leg", [(0, -20.4), (3.3, -20.4), (3.5, -19.9), (4.3, -18.5), (4.8, -16.6), (4.7, -15.2),
                               (4.1, -13.9), (3.2, -12.8), (2.6, -12.0), (2.7, -11.6), (0, -11.6)], (lx, ly, 0), "walnut")
            lathe(col, "leg foot", [(0, -22), (4.4, -22), (4.4, -21.3), (4.0, -20.8), (3.4, -20.4), (0, -20.4)],
                  (lx, ly, 0), "brass")


def plate_x(col, name, poly, x0, x1, mat, bevel=0.0):
    """A flat piece cut from the outline poly [(y, z)] (Blender), from x0 to x1 thick."""
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new((x0, y, z)) for y, z in poly])
    res = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(x1 - x0, 0, 0), verts=[e for e in res["geom"] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, profile=.5, affect="EDGES", clamp_overlap=True)
    return _obj(col, name, bm, mat)


HALF = False   # --half: the left twin only (look stills)


def mirror_x(col):
    """Every mesh of the collection copied to the other side of x = 0 (the right-hand twin)."""
    if HALF:
        return
    S = Matrix.Scale(-1, 4, (1, 0, 0))
    for o in list(col.objects):
        c = o.copy()
        c.data = o.data.copy()
        c.matrix_world = S @ o.matrix_world
        col.objects.link(c)


# ------------------------------------------------------------------ the commanders' chairs (GC_assets_03 1, D3, D5)
# Sized to the seated giant (rl-giant, 11-18; side-session note r5-chair-giant-fit.md), Unity, left chair (the right one
# is its x mirror): seat x -71 (back) .. -48, z 23.5 .. 56.5, top y 3; arms outside it (z 21.5 .. 23.5 / 56.5 .. 58.5
# and outward), top y 7.8 (the resting hands' undersides are at y 8: they lie on the rolls); back's front face x -71, 34 m tall. The prefab's chair pieces
# (colliders) stay as they are; the giant sits at x +-64.

CH = dict(back=-71.0, front=-48.0, y0=23.5, y1=56.5, seat=3.0, arm=7.8, top=37.0)


def chair_back_top(y):
    """World z of the back cushion's arched top at world y (centre hump, dipping shoulders: GC_assets_03)."""
    ym, half = (CH["y0"] + CH["y1"]) / 2, (CH["y1"] - CH["y0"]) / 2
    d = (y - ym) / half
    return 31.0 + 3.6 * math.cos(min(1.0, abs(d)) * math.pi / 2) ** 2 + 1.4 * math.exp(-((abs(d) - .95) / .12) ** 2)


def build_chair(col):
    c = CH
    B, F, Y0, Y1, S, A = c["back"], c["front"], c["y0"], c["y1"], c["seat"], c["arm"]
    xm, ym = (B + F) / 2, (Y0 + Y1) / 2

    def face(x):            # a +x facing plane at x (local u = world y, v = world z)
        return frame_of((x, 0, 0), (0, 1, 0), (0, 0, 1))

    # --- seat: thick tufted cushion with a puffed front edge, its top at y 3
    pad(col, "seat cushion", Matrix.Translation((xm + .3, ym, S)), F - B + 1.4, Y1 - Y0, 4.4, cells=(4, 6), roll=2.0, res=4,
        dimple=2.0, button_r=.45, bulge=.6)
    # --- base: a tufted leather front band with nail heads above and below it, on a deep walnut rail with a carved,
    #     scalloped foot (the sheet's chair base), turned bun legs in brass cups
    box(col, "base core", (B - 1.2, Y0 - 3.2, -13.9), (F + .2, Y1 + 3.2, -.4), "walnut", bevel=.5, segs=2)
    band = face(F + 1.4) @ Matrix.Translation((ym, -3.7, 0))
    pad(col, "front band", band, Y1 - Y0 + 4.0, 6.6, 1.6, cells=(8, 1), roll=.9, dimple=.7, button_r=.36)
    stud_row(col, "base stud", face(F + 1.45), (Y0 - 1.6, -.55), (Y1 + 1.6, -.55), 1.5, .34, "brass")
    stud_row(col, "base stud", face(F + 1.45), (Y0 - 1.6, -6.85), (Y1 + 1.6, -6.85), 1.5, .34, "brass")

    def rail_bottom(y):
        d = abs(y - ym) / ((Y1 - Y0) / 2 + 3)
        return -14.0 + 1.6 * math.exp(-(d / .28) ** 2) - .5 * math.exp(-((d - .55) / .1) ** 2)
    ys = [Y0 - 3.2 + (Y1 - Y0 + 6.4) * i / 40 for i in range(41)]
    plate_x(col, "base rail", [(y, rail_bottom(y)) for y in ys] + [(Y1 + 3.2, -7.2), (Y0 - 3.2, -7.2)],
            F - .2, F + 1.6, "walnut", bevel=.3)
    box(col, "rail bead", (F + .3, Y0 - 3.0, -7.6), (F + 1.75, Y1 + 3.0, -7.1), "brass", bevel=.12)
    for yy, out in ((Y0 - 3.2, -1), (Y1 + 3.2, 1)):
        side = frame_of((0, yy, 0), *(((1, 0, 0), (0, 0, 1)) if out < 0 else ((0, 0, 1), (1, 0, 0))))
        a, b = ((B, -7.0), (F, -7.0)) if out < 0 else ((-7.0, B), (-7.0, F))
        stud_row(col, "base stud", side, a, b, 1.5, .34, "brass")
    for lx in (B + .4, F - .2):
        for ly in (Y0 - 1.6, Y1 + 1.6):
            lathe(col, "chair leg", [(0, -14.0), (3.0, -14.0), (3.3, -14.6), (2.6, -15.4), (3.0, -16.2), (3.9, -17.6),
                                     (4.0, -18.8), (3.5, -20.0), (2.6, -20.8), (0, -20.8)], (lx, ly, 0), "walnut")
            lathe(col, "chair leg ring", [(0, -15.6), (2.9, -15.6), (3.05, -15.1), (2.9, -14.7), (0, -14.7)],
                  (lx, ly, 0), "brass", segs=16)
            lathe(col, "chair foot cup", [(0, -22), (3.1, -22), (3.2, -21.6), (2.9, -21.1), (2.5, -20.6), (0, -20.6)],
                  (lx, ly, 0), "brass")

    # --- arms: a fat leather roll along the arm (top at y 7.8: the resting hands lie on it), a leather side panel
    #     under it, a walnut front: a round head with a scroll, S-curving down to the leg
    R = 3.7
    for out in (-1, 1):
        yc = (Y0 if out < 0 else Y1) + out * (R - .6)
        zc = A - R
        lathe(col, "arm roll", [(0, B - .8), (R * .9, B - .8), (R, B - .2), (R, F - 1.0), (R * .92, F - .4), (0, F - .4)],
              (0, yc, zc), "leather", axis="X")
        for i in range(1, 8):
            dome(col, "arm button", Matrix.Translation((B + (F - B) * i / 8, yc, A - .05)), .4, "brass", segs=6)
        box(col, "arm side", (B - .4, yc - R + .2, -.6), (F - 1.0, yc + R - .2, zc), "leather", bevel=.6, segs=2)
        sf = frame_of((0, yc + out * (R - .2), 0), *(((0, 0, 1), (1, 0, 0)) if out > 0 else ((1, 0, 0), (0, 0, 1))))
        a, b = ((.1, B + .3), (.1, F - 1.6)) if out > 0 else ((B + .3, .1), (F - 1.6, .1))
        stud_row(col, "arm stud", sf, a, b, 1.5, .32, "brass")
        # walnut front, one winding: bottom, up the +y side, over the head, down the -y side
        arc = [(yc + (R + .6) * math.cos(math.radians(t)), zc + (R + .6) * math.sin(math.radians(t)))
               for t in range(-60, 241, 12)]
        right = [(yc + 2.6, -11.0), (yc + 1.5, -6.0), (yc + 2.3, -1.5)]
        left = [(yc - 2.3, -1.5), (yc - 1.5, -6.0), (yc - 2.6, -11.0)]
        plate_x(col, "arm front", [(yc - 2.8, -14.0), (yc + 2.8, -14.0)] + right + arc + left, F - 1.4, F + .5,
                "walnut", bevel=.25)
        scroll(col, "arm scroll", frame_of((F + 1.15, yc, zc), (0, -out, 0), (0, 0, 1)), R + .2, 1.3, "walnut")
        for t in range(-50, 231, 20):
            p = (yc + (R + .25) * math.cos(math.radians(t)), zc + (R + .25) * math.sin(math.radians(t)), 0)
            dome(col, "arm head stud", face(F + .5) @ Matrix.Translation(p), .3, "brass", segs=5)
        stud_row(col, "arm front stud", face(F + .5), (yc, -3.0), (yc, -12.0), 1.5, .3, "brass")

    # --- back: deep-tufted cushion with an arched top, the walnut crest following it with nail heads, flared wing
    #     posts with scroll ears and brass finials, the brass crown medallion on the crest
    zb0 = S - .2
    zmid = (zb0 + 31.0) / 2
    pad(col, "back cushion", frame_of((B + .2, ym, zmid), (0, 1, 0), (0, 0, 1)), Y1 - Y0, 31.0 - zb0, 2.8,
        cells=(7, 8), roll=2.2, dimple=2.6, button_r=.45, bulge=.8, res=4, top=lambda u: chair_back_top(ym + u) - zmid)
    box(col, "back shell", (B - 4.0, Y0 - 1.5, -1.0), (B - 1.8, Y1 + 1.5, 33.0), "walnut", bevel=.6, segs=2)
    ys = [Y0 - 1.6 + (Y1 - Y0 + 3.2) * i / 48 for i in range(49)]
    inner = [(y, chair_back_top(min(max(y, Y0), Y1)) - .4) for y in ys]
    outer = [(y, chair_back_top(min(max(y, Y0), Y1)) + 2.6) for y in ys]
    plate_x(col, "crest rail", inner + outer[::-1], B - 4.2, B + 1.0, "walnut", bevel=.3)
    for y in ys[3:-3:2]:
        dome(col, "crest stud", face(B + 1.0) @ Matrix.Translation((y, chair_back_top(y) + .7, 0)), .32, "brass",
             segs=5)
    for out in (-1, 1):
        ye = Y0 if out < 0 else Y1
        prof = [(ye - out * .6, -.6), (ye + out * 3.0, -.6), (ye + out * 3.4, 8.0), (ye + out * 4.6, 20.0),
                (ye + out * 5.4, 29.0), (ye + out * 5.6, 33.5), (ye + out * 4.8, 36.2), (ye + out * 3.0, 37.2),
                (ye + out * 1.2, 36.4), (ye - out * .6, chair_back_top(ye) + .4)]
        plate_x(col, "wing post", prof if out > 0 else prof[::-1], B - 4.6, B + 1.0, "walnut", bevel=.35)
        scroll(col, "wing ear", frame_of((B + 1.6, ye + out * 3.6, 33.6), (0, out, 0), (0, 0, 1)), 2.3, 1.1, "walnut")
        stud_row(col, "wing stud", face(B + 1.0), (ye - out * .1, 3.0), (ye - out * .1, chair_back_top(ye) - .8), 1.5,
                 .32, "brass")
        stud_row(col, "wing stud", face(B + 1.0), (ye + out * 2.6, 2.0), (ye + out * 4.4, 27.0), 1.6, .3, "brass")
        fx, fy, fz = B - 1.8, ye + out * 3.0, 37.0
        lathe(col, "finial base", [(0, fz - .6), (2.0, fz - .6), (2.0, fz + .2), (1.4, fz + .6), (0, fz + .6)],
              (fx, fy, 0), "brass", segs=16)
        lathe(col, "finial", [(0, fz + .5), (1.0, fz + .5), (1.7, fz + 1.4), (2.0, fz + 2.6), (1.6, fz + 3.8),
                              (.7, fz + 4.4), (.6, fz + 4.9), (.9, fz + 5.3), (.4, fz + 6.0), (0, fz + 6.5)],
              (fx, fy, 0), "brass")
    crown = frame_of((B + 1.0, ym, chair_back_top(ym) + 1.2), (0, 1, 0), (0, 0, 1))
    lathe_m(col, "crown medallion", crown, [(0, 0), (1.8, 0), (1.8, .3), (1.4, .6), (0, .7)], "brass", segs=20)
    mirror_x(col)


def plate_y(col, name, poly, y0, y1, mat, bevel=0.0):
    """A flat piece cut from the outline poly [(x, z)] (Blender), from y0 to y1 thick."""
    ob = plate_x(col, name, poly, -y1, -y0, mat, bevel)     # built as (t, x, z), turned so t -> -y
    ob.matrix_world = Matrix.Rotation(-math.pi / 2, 4, "Z")
    return ob


# ------------------------------------------------------------------ the giants' footstools (GC_assets_03 2)
# Where the seated giant's feet rest (rl-giant 11-18): Unity x -56 .. -41, z 26 .. 54, top y -16 (the soles at -15.8),
# on the room floor (y -22 top); the right one is the x mirror. One square stool under each foot (the sheet's shape;
# the feet are at z 28 .. 37 and 43 .. 52), both inside that box. Decoration only (no collider in the prefab).

FS = dict(x0=-55.5, x1=-41.0, top=-16.0, floor=-22.0, spans=((26.5, 39.5), (40.5, 53.5)))


def build_footstool(col):
    for Y0, Y1 in FS["spans"]:
        one_footstool(col, FS["x0"], FS["x1"], Y0, Y1, FS["top"], FS["floor"])
    mirror_x(col)


def one_footstool(col, X0, X1, Y0, Y1, TOP, FL):
    xm, ym = (X0 + X1) / 2, (Y0 + Y1) / 2
    cz0, fz0, fz1 = TOP - 1.9, FL + 1.8, TOP - 1.8       # cushion bottom, frame bottom / top
    # tufted cushion, a nail-head row round its foot
    pad(col, "stool cushion", Matrix.Translation((xm, ym, TOP)), X1 - X0 + .3, Y1 - Y0 + .3, TOP - cz0, cells=(4, 4),
        roll=1.3, dimple=1.0, button_r=.55, bulge=.6, res=4)
    box(col, "stool frame top", (X0 - .1, Y0 - .1, fz1 - .6), (X1 + .1, Y1 + .1, fz1 + .1), "walnut", bevel=.25)
    zs = fz1 - .3
    for a, b, face_of in (((Y0 + 2.2, zs), (Y1 - 2.2, zs), frame_of((X1 + .1, 0, 0), (0, 1, 0), (0, 0, 1))),
                          ((-Y1 + 2.2, zs), (-Y0 - 2.2, zs), frame_of((X0 - .1, 0, 0), (0, -1, 0), (0, 0, 1))),
                          ((X0 + 2.2, zs), (X1 - 2.2, zs), frame_of((0, Y0 - .1, 0), (1, 0, 0), (0, 0, 1))),
                          ((-X1 + 2.2, zs), (-X0 - 2.2, zs), frame_of((0, Y1 + .1, 0), (-1, 0, 0), (0, 0, 1)))):
        stud_row(col, "stool stud", face_of, a, b, 1.1, .3, "brass")
    # aprons with an arched cut-out underneath (the sheet), set in between square corner posts
    P = 1.8                                         # corner post size

    def arched(a0, a1):
        mid, half = (a0 + a1) / 2, (a1 - a0) / 2
        pts = [(a0, fz1 - .4)]
        for i in range(25):
            t = -1 + 2 * i / 24
            pts.append((mid + t * half, fz0 + .2 + .8 * max(0.0, 1 - abs(t) ** 4) ** .5))
        return pts + [(a1, fz1 - .4)]
    for x0, x1 in ((X0 + .1, X0 + .9), (X1 - .9, X1 - .1)):
        poly = arched(Y0 + P, Y1 - P)
        plate_x(col, "stool apron", poly[::-1], x0, x1, "walnut", bevel=.15)
    for y0, y1 in ((Y0 + .1, Y0 + .9), (Y1 - .9, Y1 - .1)):
        poly = arched(X0 + P, X1 - P)
        plate_y(col, "stool apron", poly, y0, y1, "walnut", bevel=.15)
    for sx, cx in ((-1, X0 - .05), (1, X1 + .05)):
        for sy, cy in ((-1, Y0 - .05), (1, Y1 + .05)):
            lo = (min(cx, cx - sx * P), min(cy, cy - sy * P), fz0 - .26)
            hi = (max(cx, cx - sx * P), max(cy, cy - sy * P), fz1 + .1)
            box(col, "stool post", lo, hi, "walnut", bevel=.25, segs=2)
            # brass corner plate: an L over the post's two outer faces with three studs a face
            box(col, "stool corner x", (min(cx, cx + sx * .25), lo[1] - (.25 if sy < 0 else 0), fz1 - 1.5),
                (max(cx, cx + sx * .25), hi[1] + (.25 if sy > 0 else 0), fz1), "brass", bevel=.08)
            box(col, "stool corner y", (lo[0] - (.25 if sx < 0 else 0), min(cy, cy + sy * .25), fz1 - 1.5),
                (hi[0] + (.25 if sx > 0 else 0), max(cy, cy + sy * .25), fz1), "brass", bevel=.08)
            dome(col, "corner stud", frame_of((cx + sx * .25, cy - sy * P / 2, fz1 - .75), (0, sx, 0), (0, 0, 1)),
                 .36, "brass", segs=6)
            dome(col, "corner stud", frame_of((cx - sx * P / 2, cy + sy * .25, fz1 - .75), (-sy, 0, 0), (0, 0, 1)),
                 .36, "brass", segs=6)
            # turned bun leg in a brass cup
            lx, ly = cx - sx * P / 2, cy - sy * P / 2
            lathe(col, "stool leg", [(0, fz0 - .2), (1.0, fz0 - .2), (1.1, fz0 - .6), (.8, fz0 - .9), (1.3, fz0 - 1.3),
                                     (1.45, FL + .9), (1.1, FL + .55), (0, FL + .55)], (lx, ly, 0), "walnut")
            lathe(col, "stool foot", [(0, FL), (1.25, FL), (1.3, FL + .3), (1.1, FL + .65), (0, FL + .65)], (lx, ly, 0),
                  "brass", segs=16)


# ------------------------------------------------------------------ the reading chaise (GC_assets_03 3)
# Prefab "Reading chaise" Unity x 102 .. 114, z 53 .. 77, y -16.5 .. -11.5 (seat top -11.5), "Chaise back" x 113.5 ..
# 116.5, z 53 .. 77, y -15 .. -1. It stands on the alcove rug (top y -21.72) in the room's +x alcove, the room's middle
# on its -x side. The head (scroll arm, high back) is at z 53, the back sweeps down toward z 74. No collider is used.

CL = dict(x0=102.0, x1=114.0, y0=53.0, y1=77.0, seat=-11.5, floor=-21.7, back=113.6)


def chaise_back_top(y):
    """World z of the chaise back's top at world y: high at the head, sweeping down the length (the sheet)."""
    c = CL
    t = min(1.0, max(0.0, (y - c["y0"]) / 21.0))
    s = 1 - t * t * (3 - 2 * t)
    return c["seat"] + 1.6 + 9.4 * s ** 1.3 + .8 * math.exp(-((t - .12) / .08) ** 2)


def build_chaise(col):
    c = CL
    X0, X1, Y0, Y1, S, FL, BK = c["x0"], c["x1"], c["y0"], c["y1"], c["seat"], c["floor"], c["back"]
    ym = (Y0 + Y1) / 2
    HEAD = Y0 + 4.2                       # where the seat cushion starts (the head roll before it)

    def face_mx(x):                       # a -x facing plane (local u = -y, v = z)
        return frame_of((x, 0, 0), (0, -1, 0), (0, 0, 1))

    # base: a leather band (studs above and below) over a walnut rail with a shallow scalloped foot
    box(col, "chaise core", (X0 + .4, Y0 + .4, -19.4), (X1, Y1 - .4, S - 2.6), "walnut", bevel=.4)
    pad(col, "chaise band", face_mx(X0 + .1) @ Matrix.Translation((-ym, -15.6, 0)), Y1 - Y0 - .6, 2.6, 1.0,
        cells=(10, 1), roll=.6, dimple=.5, button_r=.3)
    for zz in (-14.15, -17.05):
        stud_row(col, "chaise stud", face_mx(X0 + .12), (-(Y0 + .6), zz), (-(Y1 - .6), zz), 1.0, .26, "brass")

    def rail_bottom(y):
        d = abs(y - ym) / ((Y1 - Y0) / 2)
        return -19.6 + .8 * math.exp(-(d / .3) ** 2)
    ys = [Y0 + (Y1 - Y0) * i / 40 for i in range(41)]
    plate_x(col, "chaise rail", [(y, rail_bottom(y)) for y in ys] + [(Y1, -17.0), (Y0, -17.0)], X0 - .3, X0 + .6,
            "walnut", bevel=.2)
    box(col, "chaise rail bead", (X0 - .4, Y0 + .2, -17.25), (X0 + .5, Y1 - .2, -16.9), "brass", bevel=.08)
    for yy, out in ((Y0 + .4, -1), (Y1 - .4, 1)):
        plate_y(col, "chaise end rail", [(X0 - .25, -19.55), (X1, -19.55), (X1, -14.0), (X0 - .25, -14.0)],
                *((yy - .5, yy + .2) if out < 0 else (yy - .2, yy + .5)), "walnut", bevel=.2)
    # the long tufted seat
    pad(col, "chaise seat", Matrix.Translation(((X0 + BK) / 2 - .1, (HEAD + Y1) / 2, S)), BK - X0 + .4, Y1 - HEAD,
        2.9, cells=(3, 8), roll=1.4, dimple=1.3, button_r=.34, bulge=.4, res=4)
    # the head: a fat leather roll across the width, its walnut end with a scroll toward the room
    R = 2.6
    zc = S + 3.6
    lathe(col, "head roll", [(0, X0 + .3), (R * .9, X0 + .3), (R, X0 + .8), (R, BK), (0, BK)], (0, Y0 + R - .2, zc),
          "leather", axis="X")
    box(col, "head body", (X0 + .6, Y0 + .4, S - 2.8), (BK, Y0 + 2 * R - .4, zc), "leather", bevel=.5)
    for i in range(1, 6):
        dome(col, "head button", Matrix.Translation((X0 + (BK - X0) * i / 6, Y0 + R - .2, zc + R - .05)), .32, "brass")
    yc = Y0 + R - .2
    arc = [(yc + (R + .5) * math.cos(math.radians(t)), zc + (R + .5) * math.sin(math.radians(t)))
           for t in range(-60, 241, 12)]
    right = [(yc + 2.0, -16.8), (yc + 1.2, -14.6), (yc + 1.9, S - 1.0)]
    left = [(yc - 1.9, S - 1.0), (yc - 1.2, -14.6), (yc - 2.0, -16.8)]
    plate_x(col, "head front", [(yc - 2.2, -17.2), (yc + 2.2, -17.2)] + right + arc + left, X0 - .6, X0 + .7,
            "walnut", bevel=.2)
    scroll(col, "head scroll", frame_of((X0 - 1.15, yc, zc), (0, -1, 0), (0, 0, 1)), R + .1, 1.1, "walnut")
    for t in range(-50, 231, 22):
        p = (-(yc + (R + .2) * math.cos(math.radians(t))), zc + (R + .2) * math.sin(math.radians(t)), 0)
        dome(col, "head stud", face_mx(X0 - .6) @ Matrix.Translation(p), .26, "brass", segs=5)
    # the back: a tufted pad sweeping down from the head, a walnut rail on its top edge with nail heads, a scroll
    # where it rises out of the head roll
    top_end = Y1 - 3.0
    zb0 = S - .2
    zmid = zb0 + 3.0
    yb0, yb1 = Y0 + .6, top_end
    pad(col, "chaise back", frame_of((BK, (yb0 + yb1) / 2, zmid), (0, -1, 0), (0, 0, 1)), yb1 - yb0, 6.0, 2.0,
        cells=(9, 3), roll=1.3, dimple=1.2, button_r=.32, bulge=.3, res=3,
        top=lambda u: chaise_back_top((yb0 + yb1) / 2 - u) - .4 - zmid)
    ys = [Y0 + .2 + (Y1 - .2 - Y0) * i / 48 for i in range(49)]
    shell = [(y, min(chaise_back_top(y), chaise_back_top(top_end)) + .2) for y in ys]
    plate_x(col, "back shell", [(Y1 - .2, -17.0), (Y0 + .2, -17.0)] + shell, BK - .2, X1 + 2.4, "walnut", bevel=.25)
    band_in = [(y, chaise_back_top(y) - .5) for y in ys if y <= top_end]
    band_out = [(y, chaise_back_top(y) + .9) for y in ys if y <= top_end]
    plate_x(col, "back rail", band_in + band_out[::-1], BK - .9, X1 + 2.6, "walnut", bevel=.25)
    for y in [y for y in ys if y <= top_end][2::2]:
        dome(col, "back stud", face_mx(BK - .9) @ Matrix.Translation((-y, chaise_back_top(y) + .2, 0)), .26, "brass",
             segs=5)
    scroll(col, "back scroll", frame_of((BK - 1.3, Y0 + 1.6, chaise_back_top(Y0) - .9), (0, -1, 0), (0, 0, 1)), 1.9, 1.0,
           "walnut")
    # bun legs in brass cups, two along the middle as well
    for lx in (X0 + 1.4, X1 - .6):
        for ly in (Y0 + 1.6, ym, Y1 - 1.6):
            lathe(col, "chaise leg", [(0, -19.4), (1.4, -19.4), (1.5, -19.8), (1.1, -20.1), (1.6, -20.5), (1.65, -20.9),
                                      (1.2, -21.15), (0, -21.15)], (lx, ly, 0), "walnut")
            lathe(col, "chaise foot", [(0, FL), (1.4, FL), (1.45, FL + .25), (1.2, FL + .6), (0, FL + .6)], (lx, ly, 0),
                  "brass", segs=16)


# ------------------------------------------------------------------ the round side table and its stack of books
# (GC_assets_03 4) Prefab "Reading side table" Unity x 94 .. 104, z 44 .. 52, top y -9.5, on the alcove rug (top y
# -21.72); "Oversized stacked volume 0 .. 3" on it (a stack growing to y -6.25). Here: a round pedestal table (top
# radius 5 at the prefab's centre, brass rim with nail heads, turned column, four carved feet in brass shoes) and four
# thick books (red, brown, cream, red: covers, a parchment page block, a rounded spine with brass bands), spines to the
# room (-x). Decoration only.

ST = dict(x=99.0, y=48.0, top=-9.5, floor=-21.7, r=5.0)


def book(col, name, centre, W, D, H, cover, rot=0.0):
    """A closed book lying flat: W along x (the spine at -x), D along y, H thick, bottom at centre z."""
    cx, cy, z0 = centre
    obs = []
    b = .14                                   # board thickness
    inset = .3
    obs.append(box(col, name + " lower board", (-W / 2, -D / 2, 0), (W / 2, D / 2, b), cover, bevel=.05))
    obs.append(box(col, name + " upper board", (-W / 2, -D / 2, H - b), (W / 2, D / 2, H), cover, bevel=.05))
    obs.append(box(col, name + " pages", (-W / 2 + .2, -D / 2 + inset, b - .02), (W / 2 - inset, D / 2 - inset, H - b + .02),
                   "parchment", bevel=.04))
    # rounded spine: a half round of radius H/2 along y at the -x edge, two brass bands round it
    obs.append(lathe(col, name + " spine", [(0, -D / 2), (H / 2, -D / 2), (H / 2, D / 2), (0, D / 2)],
                     (-W / 2 + .05, 0, H / 2), cover, segs=16, axis="Y"))
    for f in (.22, .78):
        y = -D / 2 + D * f
        obs.append(lathe(col, name + " band", [(0, y - .22), (H / 2 + .06, y - .22), (H / 2 + .06, y + .22),
                                               (0, y + .22)], (-W / 2 + .05, 0, H / 2), "brass", segs=16, axis="Y"))
        obs.append(box(col, name + " band top", (-W / 2, y - .22, H - .02), (-W / 2 + 1.2, y + .22, H + .04), "brass"))
        obs.append(box(col, name + " band low", (-W / 2, y - .22, -.04), (-W / 2 + 1.2, y + .22, .02), "brass"))
    M = Matrix.Translation((cx, cy, z0)) @ Matrix.Rotation(rot, 4, "Z")
    for o in obs:
        o.matrix_world = M @ o.matrix_world


def build_side_table(col):
    t = ST
    X, Y, TOP, FL, R = t["x"], t["y"], t["top"], t["floor"], t["r"]
    # top: a varnished disc with a moulded edge, a brass band round it set with nail heads
    lathe(col, "table top", [(0, TOP - .9), (R - .3, TOP - .9), (R, TOP - .6), (R, TOP - .15), (R - .15, TOP), (0, TOP)],
          (X, Y, 0), "varnished", segs=40)
    lathe(col, "top band", [(0, TOP - .78), (R + .12, TOP - .78), (R + .12, TOP - .22), (0, TOP - .22)], (X, Y, 0),
          "brass", segs=40)
    for i in range(16):
        a = 2 * math.pi * i / 16
        n = Vector((math.cos(a), math.sin(a), 0))
        dome(col, "top stud", frame_of(Vector((X, Y, TOP - .5)) + n * (R + .12), Vector((0, 0, 1)).cross(n),
                                       (0, 0, 1)), .2, "brass", segs=6)
    lathe(col, "apron", [(0, TOP - 1.9), (R - .9, TOP - 1.9), (R - .7, TOP - 1.5), (R - .7, TOP - .9), (0, TOP - .9)],
          (X, Y, 0), "walnut", segs=32)
    # the turned column: collar, a long vase, a ring, a bulb at the foot
    lathe(col, "column", [(0, FL + 2.6), (1.5, FL + 2.6), (1.7, FL + 3.2), (1.25, FL + 3.8), (1.9, FL + 5.0),
                          (2.1, FL + 6.0), (1.7, FL + 7.0), (1.05, FL + 8.0), (.95, FL + 9.1), (1.25, FL + 9.6),
                          (1.5, TOP - 1.9), (0, TOP - 1.9)], (X, Y, 0), "walnut", segs=24)
    lathe(col, "column ring", [(0, FL + 8.25), (1.25, FL + 8.25), (1.3, FL + 8.6), (1.2, FL + 8.9), (0, FL + 8.9)],
          (X, Y, 0), "brass", segs=24)
    lathe(col, "column foot ring", [(0, FL + 2.3), (1.85, FL + 2.3), (1.85, FL + 2.75), (0, FL + 2.75)], (X, Y, 0),
          "brass", segs=24)
    # four feet: a curved walnut leg out from the column, a brass shoe under each toe
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        u = Vector((math.cos(a), math.sin(a), 0))
        prof = [(0, FL), (1.2, FL), (3.0, FL + .25), (4.2, FL + .2), (4.5, FL + .55), (4.3, FL + 1.0),
                (3.0, FL + 1.1), (1.8, FL + 2.0), (1.0, FL + 3.0), (0, FL + 3.0)]
        ob = plate_x(col, "foot", [(p, z) for p, z in prof], -.65, .65, "walnut", bevel=.12)
        ob.matrix_world = Matrix.Translation((X, Y, 0)) @ Matrix.Rotation(a - math.pi / 2, 4, "Z")
        lathe(col, "foot shoe", [(0, FL), (.75, FL), (.75, FL + .45), (.55, FL + .6), (0, FL + .6)],
              tuple(Vector((X, Y, 0)) + u * 4.05), "brass", segs=12)
    # the four volumes (bottom up: red, brown, cream, red), each a little turned, spines to the room
    z = TOP
    for i, (W, D, H, cover, rot) in enumerate(((8.4, 6.6, 1.15, "leather", .03), (8.2, 6.4, 1.05, "walnut", -.05),
                                               (8.0, 6.2, 1.15, "cloth", .04), (8.3, 6.5, 1.1, "leather", -.02))):
        book(col, "volume %d" % i, (X - .6 + .25 * (i % 2), Y + .15 * (i - 1.5), z), W, D, H, cover, rot)
        z += H


def sweep_rect(col, name, M, U, V, profile, mat):
    """A picture-frame moulding: the profile [(inset from the outer edge, depth)] swept round a U x V rectangle with
    mitred corners, outer edge first, closed across the back (depth 0); placed by M (local z = depth, toward the
    viewer)."""
    bm = bmesh.new()
    rings = []
    for w, d in profile:
        hu, hv = U / 2 - w, V / 2 - w
        rings.append([bm.verts.new(p) for p in ((-hu, -hv, d), (hu, -hv, d), (hu, hv, d), (-hu, hv, d))])
    w0, w1 = profile[0][0], profile[-1][0]
    back_out = [bm.verts.new((x * (U / 2 - w0), y * (V / 2 - w0), 0)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    back_in = [bm.verts.new((x * (U / 2 - w1), y * (V / 2 - w1), 0)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    seq = [back_out] + rings + [back_in]
    for a, b in zip(seq, seq[1:]):
        for i in range(4):
            try:
                bm.faces.new((a[i], a[(i + 1) % 4], b[(i + 1) % 4], b[i]))
            except ValueError:
                pass
    for i in range(4):
        bm.faces.new((back_in[i], back_in[(i + 1) % 4], back_out[(i + 1) % 4], back_out[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.transform(M)
    return _obj(col, name, bm, mat)


def placed_plate(col, name, M, poly, d0, d1, mat, bevel=0.0):
    """plate_x's outline [(u, v)] laid in M's local xy plane, from depth d0 to d1 along M's local z."""
    ob = plate_x(col, name, poly, d0, d1, mat, bevel)        # built as (t, u, v)
    swap = Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))   # (t, u, v) -> (u, v, t)
    ob.matrix_world = M @ swap
    return ob


# ------------------------------------------------------------------ the rear-wall portrait (GC_assets_03 5)
# Prefab "Royal library rear portrait" Unity x 20 .. 44, y 7 .. 37 on the rear wall (z 167.8 .. 168.2; the wall's room
# face is at z 169, the frame's back at 168.9), "Portrait velvet" the canvas. Here: a carved walnut
# frame (stepped moulding, carved crest and foot cartouches, side scrolls), brass corner fans, an inner brass bead set
# with nail heads, and a flat canvas board (one parchment face, to be painted later: the ivory king still life).

PT = dict(x0=20.0, x1=44.0, z0=7.0, z1=37.0, wall=168.9)


def build_portrait(col):
    p = PT
    U, V = p["x1"] - p["x0"], p["z1"] - p["z0"]
    M = frame_of(((p["x0"] + p["x1"]) / 2, p["wall"], (p["z0"] + p["z1"]) / 2), (1, 0, 0), (0, 0, 1))  # local z = -y
    # stepped walnut moulding, 2.6 m wide, 1.4 m deep
    sweep_rect(col, "frame moulding", M, U, V, [(0, .7), (.15, 1.0), (.5, 1.15), (.8, 1.0), (1.1, 1.0), (1.3, 1.4),
                                                  (1.7, 1.45), (1.95, 1.2), (2.2, .95), (2.45, .8), (2.6, .55)],
               "walnut")
    sweep_rect(col, "brass bead", M, U - 5.2, V - 5.2, [(0, .6), (.1, .8), (.3, .85), (.4, .5)], "brass")
    # the painting (rl_kit slot "painting" = RL_Portrait, the ivory king still life, 4:5): one picture over the canvas
    # face, UVs set by painting_uvs after the metre UVs
    box(col, "canvas", (0, 0, 0), (1, 1, 1), "painting").matrix_world = M @ Matrix.Translation((0, 0, .2)) @ \
        Matrix.Diagonal((U - 5.8, V - 5.8, .4, 1)) @ Matrix.Translation((-.5, -.5, -.5))
    # nail heads round the bead
    iu, iv = U / 2 - 2.95, V / 2 - 2.95
    for a, b in (((-iu, -iv), (iu, -iv)), ((iu, -iv), (iu, iv)), ((iu, iv), (-iu, iv)), ((-iu, iv), (-iu, -iv))):
        stud_row(col, "bead stud", M @ Matrix.Translation((0, 0, .8)), a, b, 1.05, .2, "brass")
    # brass corner fans: a quarter round with lobed rim over each corner, a boss in the middle
    for sx in (-1, 1):
        for sy in (-1, 1):
            cu, cv = sx * (U / 2 - .2), sy * (V / 2 - .2)
            pts = [(cu, cv)]
            for i in range(31):
                t = math.pi / 2 * i / 30
                r = 3.3 + .55 * abs(math.sin(3 * t)) ** .6 - .25 * math.exp(-((t - math.pi / 4) / .12) ** 2)
                pts.append((cu - sx * r * math.cos(t), cv - sy * r * math.sin(t)))
            placed_plate(col, "corner fan", M, pts if sx * sy > 0 else pts[::-1], 1.35, 1.65, "brass", bevel=.08)
            dome(col, "fan boss", M @ Matrix.Translation((cu - sx * 1.1, cv - sy * 1.1, 1.6)), .5, "brass", segs=10)
    # carved crest (top) and foot (bottom) cartouches: C-scroll lobes round a brass shell
    for sv, h in ((1, 4.2), (-1, 2.6)):
        v0 = sv * (V / 2 - .4)
        pts = []
        for i in range(61):
            t = -1 + 2 * i / 60
            pts.append((t * 7.0, v0 + sv * (h * .55 * max(0.0, 1 - t * t) ** .5 + h * .45 * math.exp(-(t / .2) ** 2)
                                             + 1.0 * math.exp(-((abs(t) - .6) / .14) ** 2)
                                             - .35 * math.exp(-((abs(t) - .85) / .06) ** 2))))
        placed_plate(col, "cartouche", M, pts if sv > 0 else pts[::-1], .2, 1.3, "walnut", bevel=.2)
        for su in (-1, 1):
            scroll(col, "cartouche scroll", M @ Matrix.Translation((su * 4.3, v0 + sv * 1.6, 1.45)) @
                   Matrix.Diagonal((-su, 1, 1, 1)), 1.4, .5, "walnut")
        shell = [(0, v0 + sv * .2)] + [(1.7 * math.cos(math.radians(a)), v0 + sv * (.9 + 1.7 * math.sin(math.radians(a))))
                                       for a in range(0, 181, 15)]
        placed_plate(col, "cartouche shell", M, shell if sv > 0 else shell[::-1], 1.25, 1.75, "brass", bevel=.1)
        for k in range(5):
            a = math.radians(30 + 30 * k)
            box(col, "shell rib", (0, 0, 0), (1, 1, 1), "brass").matrix_world = M @ Matrix.Translation(
                (0, v0 + sv * .9, 1.72 + .012 * k)) @ Matrix.Rotation(sv * a, 4, "Z") @ Matrix.Translation((.2, -.07, -.05)) @ \
                Matrix.Diagonal((1.5, .14, .14, 1))
    # side scrolls at mid height, a brass leaf on each
    for su in (-1, 1):
        u0 = su * (U / 2 - .3)
        pts = [(u0 + su * (2.3 * math.cos(math.radians(a)) ** .8 + .5 * math.exp(-((a - 50) / 12) ** 2)
                           + .5 * math.exp(-((a + 50) / 12) ** 2)), 3.4 * math.sin(math.radians(a)))
               for a in range(-90, 91, 6)]
        placed_plate(col, "side lobe", M, pts if su < 0 else pts[::-1], .2, 1.2, "walnut", bevel=.15)
        scroll(col, "side scroll", M @ Matrix.Translation((u0 + su * 1.2, 0, 1.3)) @ Matrix.Diagonal((su, 1, 1, 1)),
               1.0, .45, "walnut")
        leaf = [(u0 - su * .2, -1.4), (u0 + su * .9, -.6), (u0 + su * 1.1, 0), (u0 + su * .9, .6), (u0 - su * .2, 1.4)]
        placed_plate(col, "side leaf", M, leaf if su < 0 else leaf[::-1], 1.35, 1.6, "brass", bevel=.06)


# ------------------------------------------------------------------ the captured-piece tray (GC_assets_03 6)
# A prop by each commander's chair (not in the prefab): a brass-cornered walnut tray of taken pieces (a white king, a
# black bishop, a black rook, a white pawn lying in it) on a turned walnut stand beside the chair's outer arm, the
# tray at about seat height (Unity x -60, z 74, tray floor y 1; the right one mirrored). The play-time pieces are the
# code's (taken ones are destroyed); these are carved props only. White = cloth (ivory), black = ink.

TR = dict(x=-60.0, y=74.0, top=.4, floor=-22.0, W=22.0, D=12.0, H=2.4)

PIECES = {   # profiles (radius, height from the base) at piece scale, about 7 .. 9 m tall like the board's pieces
    "pawn": [(0, 0), (2.0, 0), (2.0, .5), (1.6, .9), (1.0, 1.5), (.75, 3.0), (1.2, 3.3), (1.2, 3.6), (.6, 3.9),
             (1.15, 4.4), (1.3, 5.0), (1.1, 5.6), (.6, 6.0), (0, 6.15)],
    "rook": [(0, 0), (2.2, 0), (2.2, .6), (1.8, 1.0), (1.4, 1.6), (1.15, 4.2), (1.5, 4.6), (1.75, 4.9), (1.75, 6.2),
             (1.35, 6.2), (1.35, 5.8), (0, 5.8)],
    "bishop": [(0, 0), (2.1, 0), (2.1, .55), (1.7, .95), (1.1, 1.8), (.8, 4.0), (1.3, 4.3), (1.3, 4.6), (.75, 4.85),
               (1.15, 5.4), (1.3, 6.2), (1.0, 7.0), (.4, 7.5), (.45, 7.8), (.3, 8.1), (0, 8.15)],
    "king": [(0, 0), (2.3, 0), (2.3, .6), (1.9, 1.05), (1.25, 2.0), (.95, 5.0), (1.55, 5.4), (1.55, 5.75), (1.0, 6.0),
             (1.5, 6.9), (1.75, 7.6), (1.3, 7.9), (0, 8.0)],
}


def lying_piece(col, name, kind, base, yaw, mat, roll=0.0):
    """A turned chess piece lying on its side on the plane z = base.z, its foot at `base`, pointing along yaw."""
    prof = PIECES[kind]
    rmax = max(r for r, _ in prof)
    ob = lathe(col, name, prof, (0, 0, 0), mat, segs=20)
    parts = [ob]
    if kind == "king":   # the cross on top
        parts.append(box(col, name + " cross", (-.35, -.35, 8.0), (.35, .35, 10.2), mat, bevel=.08))
        parts.append(box(col, name + " cross arm", (-1.0, -.35, 8.9), (1.0, .35, 9.6), mat, bevel=.08))
    if kind == "rook":   # the crenels: four notches cut as dark gaps would need booleans; four raised merlons
        for i in range(4):
            a = i * math.pi / 2 + math.pi / 4
            parts.append(box(col, name + " merlon", (-.45, -.3, 0), (.45, .3, .6), mat, bevel=.05))
            parts[-1].matrix_world = Matrix.Rotation(a, 4, "Z") @ Matrix.Translation((1.5, 0, 6.2)) @ \
                Matrix.Rotation(math.pi / 2, 4, "Z") @ parts[-1].matrix_world
    if kind == "bishop":
        parts.append(lathe(col, name + " ball", [(0, 8.0), (.45, 8.15), (.5, 8.5), (.35, 8.85), (0, 8.95)],
                           (0, 0, 0), mat, segs=10))
    # lay it down: local +z (its axis) -> horizontal along yaw, lifted so the widest ring touches the plane
    M = Matrix.Translation((base[0], base[1], base[2] + rmax)) @ Matrix.Rotation(yaw, 4, "Z") @ \
        Matrix.Rotation(math.pi / 2, 4, "Y") @ Matrix.Rotation(roll, 4, "Z")
    for o in parts:
        o.matrix_world = M @ o.matrix_world


def build_tray(col):
    t = TR
    X, Y, TOP, FL, W, D, H = t["x"], t["y"], t["top"], t["floor"], t["W"], t["D"], t["H"]
    # the stand: a square top with a brass edge, an apron, a turned column, four carved feet in brass shoes
    box(col, "stand top", (X - 9.0, Y - 5.0, TOP - .8), (X + 9.0, Y + 5.0, TOP), "varnished", bevel=.25, segs=2)
    box(col, "stand edge", (X - 9.15, Y - 5.15, TOP - .7), (X + 9.15, Y + 5.15, TOP - .25), "brass", bevel=.08)
    box(col, "stand apron", (X - 7.5, Y - 3.8, TOP - 2.4), (X + 7.5, Y + 3.8, TOP - .8), "walnut", bevel=.2)
    lathe(col, "stand column", [(0, FL + 2.6), (1.6, FL + 2.6), (1.8, FL + 3.3), (1.3, FL + 4.2), (2.2, FL + 7.0),
                                (2.4, FL + 9.5), (1.9, FL + 12.0), (1.1, FL + 14.5), (1.0, FL + 17.0), (1.4, FL + 18.0),
                                (1.4, TOP - 3.0), (2.4, TOP - 2.6), (2.4, TOP - 2.4), (0, TOP - 2.4)], (X, Y, 0),
          "walnut", segs=24)
    for z0, r in ((FL + 2.3, 1.95), (FL + 17.3, 1.45)):
        lathe(col, "stand ring", [(0, z0), (r, z0), (r, z0 + .5), (0, z0 + .5)], (X, Y, 0), "brass", segs=24)
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        u = Vector((math.cos(a), math.sin(a), 0))
        prof = [(0, FL), (1.2, FL), (3.4, FL + .25), (4.8, FL + .2), (5.1, FL + .6), (4.9, FL + 1.1), (3.4, FL + 1.2),
                (2.0, FL + 2.2), (1.1, FL + 3.3), (0, FL + 3.3)]
        ob = plate_x(col, "stand foot", prof, -.7, .7, "walnut", bevel=.12)
        ob.matrix_world = Matrix.Translation((X, Y, 0)) @ Matrix.Rotation(a - math.pi / 2, 4, "Z")
        lathe(col, "stand shoe", [(0, FL), (.8, FL), (.8, FL + .45), (.6, FL + .6), (0, FL + .6)],
              tuple(Vector((X, Y, 0)) + u * 4.6), "brass", segs=12)
    # the tray: walnut floor and walls, a brass foot band, brass L caps on the corners with rivets
    z0 = TOP
    T = .6
    box(col, "tray floor", (X - W / 2, Y - D / 2, z0), (X + W / 2, Y + D / 2, z0 + T), "walnut", bevel=.1)
    for sx in (-1, 1):
        box(col, "tray end", (X + sx * W / 2 - (T if sx > 0 else 0), Y - D / 2 + T - .02, z0 + .03),
            (X + sx * W / 2 + (0 if sx > 0 else T), Y + D / 2 - T + .02, z0 + H - .02), "walnut", bevel=.12)
    for sy in (-1, 1):
        box(col, "tray side", (X - W / 2, Y + sy * D / 2 - (T if sy > 0 else 0), z0 + .03),
            (X + W / 2, Y + sy * D / 2 + (0 if sy > 0 else T), z0 + H), "walnut", bevel=.12)
    box(col, "tray band", (X - W / 2 - .08, Y - D / 2 - .08, z0 + .015), (X + W / 2 + .08, Y + D / 2 + .08, z0 + .37), "brass",
        bevel=.05)
    C = 2.2
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = X + sx * W / 2, Y + sy * D / 2
            box(col, "cap x", (min(cx, cx + sx * .15), min(cy, cy - sy * C), z0 - .05),
                (max(cx, cx + sx * .15), max(cy, cy - sy * C), z0 + H + .05), "brass", bevel=.06)
            box(col, "cap y", (min(cx, cx - sx * C), min(cy, cy + sy * .15), z0 - .05),
                (max(cx, cx - sx * C), max(cy, cy + sy * .15), z0 + H + .05), "brass", bevel=.06)
            box(col, "cap top", (min(cx + sx * .15, cx - sx * C), min(cy + sy * .15, cy - sy * C), z0 + H - .02),
                (max(cx + sx * .15, cx - sx * C), max(cy + sy * .15, cy - sy * C), z0 + H + .12), "brass", bevel=.05)
            for f in (.35, .75):
                dome(col, "cap rivet", frame_of((cx + sx * .15, cy - sy * C * .5, z0 + H * f), (0, sx, 0), (0, 0, 1)),
                     .22, "brass", segs=6)
                dome(col, "cap rivet", frame_of((cx - sx * C * .5, cy + sy * .15, z0 + H * f), (-sy, 0, 0), (0, 0, 1)),
                     .22, "brass", segs=6)
    # the taken pieces
    fl = z0 + T
    lying_piece(col, "white king", "king", (X - 9.6, Y - 3.0, fl), math.radians(2), "cloth", roll=.3)
    lying_piece(col, "black bishop", "bishop", (X + 9.6, Y + 3.0, fl), math.radians(178), "ink")
    lying_piece(col, "black rook", "rook", (X + 2.4, Y - 3.2, fl), math.radians(12), "ink", roll=.4)
    lying_piece(col, "white pawn", "pawn", (X - 8.6, Y + 3.2, fl), math.radians(-9), "cloth")
    mirror_x(col)


def torus(col, name, M, R, r, mat, segs=20, rsegs=8):
    """A ring of radius R, tube r, in M's local xy plane (axis = local z)."""
    bm = bmesh.new()
    rings = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        c, s = math.cos(a), math.sin(a)
        rings.append([bm.verts.new(((R + r * math.cos(b)) * c, (R + r * math.cos(b)) * s, r * math.sin(b)))
                      for b in (2 * math.pi * j / rsegs for j in range(rsegs))])
    for i in range(segs):
        a, b = rings[i], rings[(i + 1) % segs]
        for j in range(rsegs):
            bm.faces.new((a[j], b[j], b[(j + 1) % rsegs], a[(j + 1) % rsegs]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.transform(M)
    return _obj(col, name, bm, mat, smooth=True)


def face_y(y, f):
    """A plane at y facing +y (f = 1) or -y (f = -1), its local (u, v) = world (x, z) either way."""
    M = frame_of((0, y, 0), (1, 0, 0), (0, 0, 1))          # normal -y
    return M if f < 0 else M @ Matrix.Diagonal((-1, 1, -1, 1))   # turned to +y, u still = x after the flip


def pyramid(col, name, M, r, h, mat):
    """A four-sided brass diamond stud, base r, height h along M's local z."""
    ob = lathe(col, name, [(r, 0), (0, h)], (0, 0, 0), mat, segs=4)
    ob.matrix_world = M @ Matrix.Rotation(math.pi / 4, 4, "Z")
    return ob


# ------------------------------------------------------------------ the chess room's double doors (GC_assets_04 left)
# Prefab: the front facade (Unity z -11 .. -9) leaves a doorway x -8 .. 8, y 0 .. 6.8; "HQ door carved jamb" x +-8 ..
# 9.4, "HQ door lintel" y 6.8 .. 7.8, "HQ royal crest", the "Royal standard" banners. "Headquarters privacy doors -
# proximity reveal" holds two "Solid command door leaf" (x -4 and 4, 8 x 6.8 x 0.6) that HeadquartersPrivacyDoor slides
# sideways into the wall. Here:
#   RlRoomDoorway (static): walnut door posts with panels and brass diamonds, walnut pilasters on plinths (the
#     wall's colours: walnut with brass inlay lines, as the gallery wainscot round it - 승규 11-18 "벽 색이랑 맞춰"),
#     a walnut lintel with brass bands and nail heads, a walnut cornice with brass corner caps and the crest,
#     through the wall's thickness (both faces). The banners give way to it.
#   RlRoomDoorLeafL / R (each in its own leaf's frame, so it rides the slide): a panelled walnut leaf, raised panels,
#     nail heads down the stiles, a brass ring knocker, strap hinges on the outer edge - both faces. Kept within
#     +-0.6 m of the leaf's middle so it slides into the wall unseen.

DOOR = dict(w=8.0, h=6.8, y=-10.0, wall0=-11.0, wall1=-9.0)


def inlay_rect(col, y, f, x0, z0, x1, z1, w=.09, d=.025):
    """A thin brass inlay outline (the gallery wainscot's lines) on a plane at y facing f (+1 = +y), x0..x1 by z0..z1."""
    ya, yb = sorted((y - f * .01, y + f * d))
    for lo, hi in (((x0, ya, z0), (x1, yb, z0 + w)), ((x0, ya, z1 - w), (x1, yb, z1)),
                   ((x0, ya, z0), (x0 + w, yb, z1)), ((x1 - w, ya, z0), (x1, yb, z1))):
        box(col, "inlay", lo, hi, "brass")


def build_doorway(col):
    d = DOOR
    W, H, Y = d["w"], d["h"], d["y"]
    yo0, yo1 = d["wall0"] - 1.4, d["wall1"] + 1.4          # the posts stand 1.4 m proud of each face
    # The prefab facade stays drawn (its other face is the gallery's far wall): its ends at x +-8 and the header's
    # underside at y 6.8 would share faces with the posts and the lintel, so these stand 3 cm proud of them.
    for s in (-1, 1):
        x0, x1 = sorted((s * (W - .03), s * (W + 2.4)))
        box(col, "door post", (x0, yo0, -.6), (x1, yo1, H), "walnut", bevel=.2, segs=2)
        for yy, f in ((yo0, -1), (yo1, 1)):
            box(col, "post panel", (x0 + .45, min(yy, yy + f * .12), .7), (x1 - .45, max(yy, yy + f * .12), H - .7),
                "walnut", bevel=.08)
            inlay_rect(col, yy + f * .12, f, x0 + .75, .95, x1 - .75, H - .95)
            for zz in (1.6, 3.4, 5.2):
                sgn = 1 if f < 0 else -1         # face_y's local u = x on the -y face, -x on the +y face
                pyramid(col, "post diamond", face_y(yy + f * .12, f) @ Matrix.Translation((sgn * (x0 + x1) / 2, zz, 0)),
                        .32, .3, "brass")
        for z0 in (-.65, H - .5):
            box(col, "post band", (x0 - .08, yo0 - .08, z0), (x1 + .08, yo1 + .08, z0 + .5), "brass", bevel=.06)
        # walnut pilaster on a plinth, panelled with brass inlay lines like the gallery's wainscot (R4 walls)
        px0 = s * (W + 2.4)
        qa, qb = sorted((px0, px0 + s * 3.4))
        box(col, "pilaster plinth", (min(px0 - s * .2, px0 + s * 3.6), yo0 - .5, -1.4),
            (max(px0 - s * .2, px0 + s * 3.6), yo1 + .5, .4), "walnut", bevel=.2, segs=2)
        box(col, "pilaster", (qa, yo0 - .15, .4), (qb, yo1 + .15, H), "walnut", bevel=.15, segs=2)
        box(col, "pilaster plinth band", (min(px0 - s * .25, px0 + s * 3.65), yo0 - .55, .25),
            (max(px0 - s * .25, px0 + s * 3.65), yo1 + .55, .45), "brass", bevel=.05)
        for yy, f in ((yo0 - .15, -1), (yo1 + .15, 1)):
            inlay_rect(col, yy, f, qa + .5, 1.0, qb - .5, H - .6)
    # lintel and cornice
    LX = W + 2.4 + 3.5
    box(col, "lintel", (-LX, yo0 - .2, H - .03), (LX, yo1 + .2, H + 2.0), "walnut", bevel=.2, segs=2)
    for zz in (H + .05, H + 1.65):
        box(col, "lintel band", (-LX - .05, yo0 - .3, zz), (LX + .05, yo1 + .3, zz + .3), "brass", bevel=.06)
    for yy, f in ((yo0 - .2, -1), (yo1 + .2, 1)):
        stud_row(col, "lintel stud", face_y(yy, f), (-(LX - .8), H + 1.0), (LX - .8, H + 1.0), 1.4, .22, "brass")
    CZ = H + 2.0
    box(col, "cornice", (-LX - .7, yo0 - .8, CZ), (LX + .7, yo1 + .8, CZ + 1.6), "walnut", bevel=.3, segs=2)
    box(col, "cornice band", (-LX - .6, yo0 - .7, CZ + .05), (LX + .6, yo1 + .7, CZ + .35), "brass", bevel=.05)
    C = 1.8
    for sx in (-1, 1):
        for yy, sy in ((yo0 - .8, -1), (yo1 + .8, 1)):
            cx = sx * (LX + .7)
            box(col, "cornice cap x", (min(cx, cx + sx * .15), min(yy, yy - sy * C), CZ - .05),
                (max(cx, cx + sx * .15), max(yy, yy - sy * C), CZ + 1.65), "brass", bevel=.05)
            box(col, "cornice cap y", (min(cx, cx - sx * C), min(yy, yy + sy * .15), CZ - .05),
                (max(cx, cx - sx * C), max(yy, yy + sy * .15), CZ + 1.65), "brass", bevel=.05)
            box(col, "cornice cap top", (min(cx + sx * .15, cx - sx * C), min(yy + sy * .15, yy - sy * C), CZ + 1.58),
                (max(cx + sx * .15, cx - sx * C), max(yy + sy * .15, yy - sy * C), CZ + 1.75), "brass", bevel=.05)
            dome(col, "cap rivet", frame_of((cx - sx * C * .45, yy + sy * .15, CZ + .8), (-sy, 0, 0), (0, 0, 1)), .3,
                 "brass", segs=8)
            dome(col, "cap rivet", frame_of((cx + sx * .15, yy - sy * C * .45, CZ + .8), (0, sx, 0), (0, 0, 1)), .3,
                 "brass", segs=8)
    # the royal crest on the cornice, both faces: a medallion with a crown
    for yy, f in ((yo0 - .8, -1), (yo1 + .8, 1)):
        M = face_y(yy, f) @ Matrix.Translation((0, CZ + .8, 0))
        lathe_m(col, "crest medallion", M, [(0, 0), (1.1, 0), (1.1, .25), (.85, .45), (0, .5)], "brass", segs=20)
        for i, dx in enumerate((-.45, 0, .45)):
            box(col, "crest point", (0, 0, 0), (1, 1, 1), "brass").matrix_world = M @ Matrix.Translation(
                (dx - .1, .55, .3)) @ Matrix.Diagonal((.2, .5 + .25 * (i == 1), .2, 1))


def build_door_leaf(col, s):
    """One leaf, built where it stands (s = -1: the left leaf x -8 .. 0, s = 1: the right one 0 .. 8)."""
    d = DOOR
    W, H, Y = d["w"], d["h"], d["y"]
    x_in, x_out = s * .005, s * (W - .05)
    xa, xb = sorted((x_in, x_out))
    box(col, "leaf slab", (xa, Y - .22, .05), (xb, Y + .22, H - .05), "walnut", bevel=.05)
    st, rl, mr = .9, .9, (3.15, 3.85)
    cols = [(xa + st, (xa + xb) / 2 - .3), ((xa + xb) / 2 + .3, xb - st)]
    rows = [(rl, mr[0]), (mr[1], H - rl)]
    for f in (-1, 1):
        y0, y1 = sorted((Y + f * .2, Y + f * .32))
        # stiles, rails and the muntin raised on the face, raised panels in between
        box(col, "stile", (xa, y0, .05), (xa + st, y1, H - .05), "walnut", bevel=.05)
        box(col, "stile", (xb - st, y0, .05), (xb, y1, H - .05), "walnut", bevel=.05)
        for z0, z1 in ((.05, rl), (mr[0], mr[1]), (H - rl, H - .05)):
            box(col, "rail", (xa + st, y0, z0), (xb - st, y1, z1), "walnut", bevel=.05)
        for z0, z1 in ((rl, mr[0]), (mr[1], H - rl)):
            box(col, "muntin", ((xa + xb) / 2 - .3, y0, z0), ((xa + xb) / 2 + .3, y1, z1), "walnut", bevel=.05)
        for c0, c1 in cols:
            for r0, r1 in rows:
                py0, py1 = sorted((Y + f * .2, Y + f * .3))
                box(col, "raised panel", (c0 + .3, py0, r0 + .3), (c1 - .3, py1, r1 - .3), "walnut", bevel=.12,
                    segs=2)
        F = face_y(Y + f * .32, f)
        sgn = 1 if f < 0 else -1                 # face_y's local u = x on the -y face, -x on the +y face
        for xx in (xa + st / 2, xb - st / 2):
            stud_row(col, "leaf stud", F, (sgn * xx, .5), (sgn * xx, H - .5), .75, .12, "brass")
        for zz in (rl / 2, (mr[0] + mr[1]) / 2, H - rl / 2):
            stud_row(col, "leaf stud", F, (sgn * (xa + st + .3), zz), (sgn * (xb - st - .3), zz), .75, .12, "brass")
        # ring knocker in the middle of the leaf, on the mid rail
        kx = (xa + xb) / 2
        boss = F @ Matrix.Translation((sgn * kx, mr[1] + .15, 0))
        lathe_m(col, "knocker boss", boss, [(0, 0), (.42, 0), (.42, .1), (.3, .2), (0, .24)], "brass", segs=16)
        ring_c = Y + f * (.32 + .16)
        torus(col, "knocker ring", Matrix.Translation((kx, ring_c, mr[1] - .55)) @ Matrix.Rotation(math.pi / 2, 4, "X"),
              .65, .12, "brass")
        # strap hinges on the outer edge, a knuckle at the edge
        for zc in (1.1, H - 1.1):
            e1 = x_out - s * 3.2
            pts = [(x_out, zc - .3), (x_out - s * 2.2, zc - .2), (x_out - s * 2.6, zc - .45), (e1, zc),
                   (x_out - s * 2.6, zc + .45), (x_out - s * 2.2, zc + .2), (x_out, zc + .3)]
            pts = [(sgn * x, z) for x, z in pts]
            placed_plate(col, "strap hinge", F, pts, 0, .07, "brass", bevel=.02)
            for xx in (x_out - s * .7, x_out - s * 1.6, x_out - s * 2.6):
                dome(col, "hinge rivet", F @ Matrix.Translation((sgn * xx, zc, .07)), .11, "brass", segs=6)
    for zc in (1.1, H - 1.1):
        lathe(col, "hinge knuckle", [(0, zc - .38), (.2, zc - .38), (.2, zc + .38), (0, zc + .38)],
              (x_out - s * .05, Y, 0), "brass", segs=10)


def build_door_leaf_l(col):
    build_door_leaf(col, -1)


def build_door_leaf_r(col):
    build_door_leaf(col, 1)


def build_door_preview(col):
    """Look stills only (never exported to Assets): the doorway with both leaves shut, in the course frame."""
    build_doorway(col)
    build_door_leaf(col, -1)
    build_door_leaf(col, 1)


# ------------------------------------------------------------------ the room: walls, ceiling, floor, exit portal
# Prefab "Headquarters - enclosed command chamber": side walls' room faces x = +-123, rear wall's z = 169 (exit hole x
# +-7, y -10 .. 10), front facade's z = -9 (doorway x +-8, y -10 .. 6.8; RlRoomDoorway dresses |x| < 14.6 up to
# y 10.4), walls y -10 .. 58 standing on nothing (the room floor below is at y -22), coffered ceiling underside y 58,
# "Carved ceiling beam" x 0, +-48, +-96 and "Ceiling cross beam" z 0, 40 .. 160 (y 56.8 .. 58), "Room floor below fall
# reset" top y -22. The prefab keeps every collider; these only draw.
# Look = the gallery's (R4 rl_r4_gallery_model.py, GC_D5 / E1) at this room's scale: walnut wainscot with raised,
# brass-inlaid panels to y 14 (the doorway's and the gallery's height), a dado rail, cream stone above, walnut
# pilasters every ~20 m, a stepped walnut cornice and frieze to the ceiling; the right wall (+x) a row of arched
# windows (dark glass, shape only: R7 lights them), the left and rear walls framed chess boards (D3, D4), the portrait
# bay on the rear wall kept low; the exit through the rear wall in a stone arch of chess-piece plaques (D4).

RM = dict(x=123.0, y0=-9.0, y1=169.0, floor=-22.0, dado0=14.0, dado1=15.6, cornice=52.0, ceil=58.0)


def at(x, y, z, deg):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(deg), 4, "Z")


def wbox(col, name, M, u0, u1, y0, y1, z0, z1, mat, bevel=0.0, segs=1):
    """A box in a wall's frame M (local x = along the wall, local y = 0 the wall's room face, the room at -y)."""
    ob = box(col, name, (u0, y0, z0), (u1, y1, z1), mat, bevel, 1)   # one bevel step: the walls are seen from afar
    ob.matrix_world = M
    return ob


def winlay(col, M, u0, u1, z0, z1, y, w=.16, t=.05):
    for lo, hi in (((u0, y - t, z0), (u1, y, z0 + w)), ((u0, y - t, z1 - w), (u1, y, z1)),
                   ((u0, y - t, z0), (u0 + w, y, z1)), ((u1 - w, y - t, z0), (u1, y, z1))):
        box(col, "inlay", lo, hi, "brass").matrix_world = M


def wall_frames():
    r = RM
    return {"left": at(-r["x"], 0, 0, 90),        # local u = world y
            "right": at(r["x"], 0, 0, -90),       # local u = -world y
            "rear": at(0, r["y1"], 0, 0),         # local u = world x
            "front": at(0, r["y0"], 0, 180)}      # local u = -world x


def room_pilaster(col, M, u):
    r = RM
    F, C = r["floor"], r["cornice"]
    # deeper than the dado (1.2) and the cornice's first step (1.0), so their ends die into it
    wbox(col, "pilaster", M, u - 1.8, u + 1.8, -1.35, 0, F, C, "walnut", .15, 2)
    wbox(col, "pilaster base", M, u - 2.2, u + 2.2, -1.7, 0, F, F + 3.2, "walnut", .18, 2)
    wbox(col, "pilaster band", M, u - 2.23, u + 2.23, -1.73, -.1, F + 3.2, F + 3.5, "brass")
    wbox(col, "pilaster band", M, u - 2.23, u + 2.23, -1.77, -.1, C - 2.8, C - 2.5, "brass")
    wbox(col, "pilaster capital", M, u - 2.2, u + 2.2, -1.75, 0, C - 2.5, C, "walnut", .18, 2)
    for du in (-.65, .65):
        wbox(col, "pilaster inlay", M, u + du - .09, u + du + .09, -1.43, -1.31, F + 4.4, C - 3.4, "brass")


def wainscot(col, M, a0, a1, top=None):
    r = RM
    F, D0 = r["floor"], (top if top is not None else r["dado0"])
    wbox(col, "wainscot", M, a0, a1, -.18, 0, F, D0, "walnut")
    n = max(1, int(round((a1 - a0) / 8.5)))
    step = (a1 - a0) / n
    rows = ((F + 3.4, -3.0), (-1.6, D0 - 1.4)) if D0 > 5 else ((F + 3.4, D0 - 1.4),)
    for i in range(n):
        x0, x1 = a0 + i * step + .6, a0 + (i + 1) * step - .6
        for z0, z1 in rows:
            wbox(col, "wainscot panel", M, x0, x1, -.5, -.15, z0, z1, "walnut", .16, 2)
            winlay(col, M, x0 + .7, x1 - .7, z0 + .7, z1 - .7, -.5)


def dado(col, M, a0, a1):
    r = RM
    D0, D1 = r["dado0"], r["dado1"]
    wbox(col, "dado", M, a0, a1, -.9, 0, D0, D0 + .8, "walnut", .18, 2)
    wbox(col, "dado cap", M, a0, a1, -1.2, 0, D0 + .8, D1, "walnut", .2, 2)
    wbox(col, "dado brass", M, a0, a1, -1.25, -1.13, D0 + 1.0, D0 + 1.18, "brass")


def mouldings(col, M, a0, a1, trim=(False, False)):
    """Skirting, cornice and frieze along a0 .. a1. trim: an end at a room corner stops each piece at the side wall's
    same piece (its depth short of the corner), so the two walls' mouldings meet face to face and never overlap."""
    r = RM
    F, C, CE = r["floor"], r["cornice"], r["ceil"]
    for name, y0, y1, z0, z1, mat, bev in (("skirting", -.7, 0, F, F + 1.8, "walnut", .12),
                                           ("skirting brass", -.76, -.56, F + 1.8, F + 2.0, "brass", 0),
                                           ("cornice", -1.0, 0, C, C + .7, "walnut", .18),
                                           ("cornice", -1.7, 0, C + .7, C + 1.6, "walnut", .25),
                                           ("cornice brass", -1.08, -.96, C + .15, C + .35, "brass", 0),
                                           ("frieze", -.4, 0, C + 1.6, CE, "walnut", 0),
                                           ("frieze crown", -1.4, 0, CE - 1.0, CE, "walnut", .2)):
        d = -y0
        wbox(col, name, M, a0 + (d if trim[0] else 0), a1 - (d if trim[1] else 0), y0, y1, z0, z1, mat, bev)


def arch_points(cx, w, z0, zs, n=16, grow=0.0):
    """Round-headed outline: up the left side, over the half circle, down the right (u, z)."""
    w = w + grow
    pts = [(cx - w, z0 - grow)]
    pts += [(cx - w * math.cos(math.pi * i / n), zs + w * math.sin(math.pi * i / n)) for i in range(n + 1)]
    pts += [(cx + w, z0 - grow)]
    return pts


def strip_ring(col, name, M, outer, inner, y0, y1, mat):
    """A band between two outlines of the same count (open at the ends), y0 .. y1 thick, in wall frame M."""
    bm = bmesh.new()
    def ring(pts, y):
        return [bm.verts.new((u, y, z)) for u, z in pts]
    o0, o1, i0, i1 = ring(outer, y0), ring(outer, y1), ring(inner, y0), ring(inner, y1)
    for a in range(len(outer) - 1):
        b = a + 1
        bm.faces.new((o0[a], o0[b], i0[b], i0[a]))
        bm.faces.new((o1[a], i1[a], i1[b], o1[b]))
        bm.faces.new((o0[a], o1[a], o1[b], o0[b]))
        bm.faces.new((i0[a], i0[b], i1[b], i1[a]))
    for e in (0, len(outer) - 1):
        bm.faces.new((o0[e], i0[e], i1[e], o1[e]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _obj(col, name, bm, mat)
    ob.matrix_world = M
    return ob


def room_window(col, M, cx):
    """Tall round-headed window in the stone (GC_D5): stone surround and sill, keystone, walnut sash and glazing
    bars over a dark glass panel set slightly into the wall face."""
    w, z0, zs = 6.0, 20.0, 40.0
    strip_ring(col, "window surround", M, arch_points(cx, w, z0, zs, 18, 1.6), arch_points(cx, w, z0, zs, 18, 0),
               -.9, 0, "stone")
    plate = arch_points(cx, w, z0, zs, 18, .05)
    placed_plate(col, "window glass", M @ Matrix.Rotation(math.pi / 2, 4, "X"), [(u, z) for u, z in plate], .32, .4,
                 "ink")
    strip_ring(col, "window sash", M, arch_points(cx, w, z0, zs, 18, 0), arch_points(cx, w, z0, zs, 18, -.5),
               -.6, -.05, "walnut")
    for du in (-w / 3, 0.0, w / 3):
        top = zs + math.sqrt(max(w * w - du * du, 0)) - .3
        wbox(col, "glazing bar", M, cx + du - .2, cx + du + .2, -.5, -.08, z0, top, "walnut")
    for z in (z0 + 5, z0 + 10, z0 + 15, zs):
        wbox(col, "glazing bar", M, cx - w + .2, cx + w - .2, -.5, -.08, z - .2, z + .2, "walnut")
    wbox(col, "window sill", M, cx - w - 2.0, cx + w + 2.0, -1.6, 0, z0 - 1.6, z0 - .6, "stone", .15, 2)
    wbox(col, "window sill", M, cx - w - 1.4, cx + w + 1.4, -1.1, 0, z0 - 2.4, z0 - 1.6, "stone", .12, 2)
    crown = zs + w + 1.6
    placed_plate(col, "keystone", M @ Matrix.Rotation(math.pi / 2, 4, "X"),
                 [(cx - .9, crown - 1.7), (cx + .9, crown - 1.7), (cx + 1.3, crown + .7), (cx - 1.3, crown + .7)],
                 0, 1.3, "stone", bevel=.1)


def board_frame(col, M, cx, cz, size=12.0, seed=0):
    """A framed chess diagram on the wall (D3 / D4): walnut moulding, brass bead, a parchment field with walnut
    squares and a few pieces standing on it."""
    Mb = M @ Matrix.Translation((cx, 0, cz)) @ Matrix.Rotation(math.pi / 2, 4, "X")
    # Mb: local x = wall u, local y = z (up), local z = -wall y (out into the room)
    sweep_rect(col, "board frame", Mb, size, size, [(0, .5), (.2, .8), (.5, .9), (.8, .7), (1.0, .6), (1.15, .35)],
               "walnut")
    sweep_rect(col, "board bead", Mb, size - 2.3, size - 2.3, [(0, .4), (.08, .55), (.2, .55), (.28, .3)], "brass")
    inner = size - 2.86
    box(col, "board field", (0, 0, 0), (1, 1, 1), "parchment").matrix_world = Mb @ Matrix.Translation(
        (-inner / 2, -inner / 2, .05)) @ Matrix.Diagonal((inner, inner, .25, 1))
    sq = inner / 8
    for i in range(8):
        for j in range(8):
            if (i + j) % 2:
                box(col, "board square", (0, 0, 0), (1, 1, 1), "walnut").matrix_world = Mb @ Matrix.Translation(
                    (-inner / 2 + i * sq, -inner / 2 + j * sq, .3)) @ Matrix.Diagonal((sq, sq, .04, 1))
    rng = __import__("random").Random(seed)
    for _ in range(5):
        i, j = rng.randrange(8), rng.randrange(8)
        mat = "ink" if rng.random() < .5 else "cloth"
        ob = lathe(col, "board piece", [(0, 0), (sq * .32, 0), (sq * .22, sq * .2), (sq * .14, sq * .55),
                                        (sq * .22, sq * .7), (0, sq * .8)], (0, 0, 0), mat, segs=10)
        ob.matrix_world = Mb @ Matrix.Translation((-inner / 2 + (i + .5) * sq, -inner / 2 + (j + .5) * sq, .34))


def stone_field(col, M, a0, a1, z0=None, z1=None):
    r = RM
    wbox(col, "string course", M, a0, a1, -.35, 0, r["dado1"], r["dado1"] + .5, "stone", .1, 2)
    wbox(col, "stone", M, a0, a1, -.2, 0, z0 if z0 is not None else r["dado1"], z1 if z1 is not None else r["cornice"],
         "stone")


def wall_run(col, M, posts, upper, low_bays=(), corner=(False, False)):
    """Pilasters on `posts` (wall u), bays between: wainscot + dado + stone, then `upper(col, M, u0, u1, i)`."""
    for u in posts:
        room_pilaster(col, M, u)
    for i, (u0, u1) in enumerate(zip(posts, posts[1:])):
        a0, a1 = u0 + 1.75, u1 - 1.75        # 5 cm into the pilasters: no shared end faces
        if i in low_bays:            # kept low behind a big picture: a short wainscot, stone up from it
            wainscot(col, M, a0, a1, top=2.0)
            wbox(col, "stone", M, a0, a1, -.2, 0, 2.05, RM["cornice"], "stone")
            wbox(col, "low rail", M, a0, a1, -.8, 0, 2.0, 2.8, "walnut", .15, 2)
        else:
            wainscot(col, M, a0, a1)
            dado(col, M, a0, a1)
            stone_field(col, M, a0, a1)
            if upper:
                upper(col, M, u0, u1, i)
    # ends clear of the end pilasters' side faces; at a room corner (front / rear walls) trimmed to meet the side wall
    mouldings(col, M, posts[0] - (1.8 if corner[0] else 1.85), posts[-1] + (1.8 if corner[1] else 1.85), corner)


def build_walls(col):
    r = RM
    W = wall_frames()
    side = [-7.2, 10, 30, 50, 70, 90, 110, 130, 150, 167.2]
    wall_run(col, W["left"], side, lambda c, M, u0, u1, i: board_frame(c, M, (u0 + u1) / 2, 33.0, 13.0, i))
    wall_run(col, W["right"], [-u for u in reversed(side)], lambda c, M, u0, u1, i: room_window(c, M, (u0 + u1) / 2))
    # rear wall: the exit (|x| < 7, y -10 .. 10) in its arch, |x| < 12.5 kept for it; the portrait bay (x 20 .. 44) low
    rear_l = [-121.2, -97.4, -73.6, -50.0, -12.5]
    rear_r = [12.5, 50.0, 73.6, 97.4, 121.2]           # bay 0 (x 12.5 .. 50) holds the portrait
    boards = lambda c, M, u0, u1, i: board_frame(c, M, (u0 + u1) / 2, 33.0, 13.0, 20 + i)   # noqa: E731
    wall_run(col, W["rear"], rear_l, boards, corner=(True, False))
    wall_run(col, W["rear"], rear_r, boards, low_bays=(0,), corner=(False, True))
    exit_portal(col, W["rear"])
    mouldings(col, W["rear"], -10.65, 10.65)     # the cornice and frieze run on over the exit (end to end)
    # front wall (u = -x): the doorway (RlRoomDoorway, |x| < 14.6 up to y 10.4) in plain stone above
    front = [-121.2, -100, -78, -56, -34, -16]
    for posts in (front, [-u for u in reversed(front)]):
        wall_run(col, W["front"], posts, lambda c, M, u0, u1, i: room_window(c, M, (u0 + u1) / 2) if i % 2 else None,
                 corner=(posts[0] < 0, posts[0] > 0))
    Mf = W["front"]
    wbox(col, "door wall", Mf, -14.2, 14.2, -.2, 0, 10.4, r["cornice"], "stone")
    wbox(col, "door wall low", Mf, -14.2, 14.2, -.2, 0, r["floor"], -10.0, "walnut")
    for a, b in ((-14.2, -8.0), (8.0, 14.2)):     # beside the doorway's hole, under its posts
        wbox(col, "door wall side", Mf, a, b, -.2, 0, -10.0, -1.4, "walnut")
    wbox(col, "door string", Mf, -14.2, 14.2, -.35, 0, r["dado1"], r["dado1"] + .5, "stone", .1, 2)
    mouldings(col, Mf, -14.15, 14.15)
    # the walls' foot below the prefab walls (y -10) is the wainscot above; corners: nothing else needed


def exit_portal(col, M):
    """The exit through the rear wall (D4): walnut jamb pilasters with brass, a round stone arch of voussoirs carrying
    dark plaques with ivory chess pieces, a keystone crown, a walnut tympanum; stone filling the rest of |x| < 12.5."""
    r = RM
    w, zs = 7.0, 10.0
    for s in (-1, 1):
        a, b = sorted((s * 7.0, s * 10.0))
        wbox(col, "exit jamb", M, a, b, -1.6, 0, r["floor"], zs, "walnut", .2, 2)
        winlay(col, M, a + .5, b - .5, -6.0, zs - 1.0, -1.6)
        wbox(col, "exit jamb band", M, a - .05, b + .05, -1.68, -.1, zs - .6, zs - .03, "brass")
        c0, c1 = sorted((s * 10.0, s * 12.5))
        wbox(col, "exit side stone", M, c0, c1, -.2, 0, r["floor"], r["cornice"], "stone")
    wbox(col, "exit wall", M, -10, 10, -.2, 0, zs + .05, r["cornice"], "stone")
    wbox(col, "exit sill wall", M, -7, 7, -.2, 0, r["floor"], -10.0, "walnut")
    # tympanum (half disc) and the arch ring split into nine voussoirs
    placed_plate(col, "exit tympanum", M @ Matrix.Rotation(math.pi / 2, 4, "X"),
                 [(w * math.cos(math.pi * i / 24), zs + w * math.sin(math.pi * i / 24)) for i in range(25)], -.4, .3,
                 "walnut", bevel=.1)
    n = 9
    for i in range(n):
        a0, a1 = math.pi * (i + .04) / n, math.pi * (i + .96) / n
        ts = [a0 + (a1 - a0) * k / 4 for k in range(5)]
        outer = [(-(w + 2.8) * math.cos(t), zs + (w + 2.8) * math.sin(t)) for t in ts]
        inner = [(-w * math.cos(t), zs + w * math.sin(t)) for t in ts]
        strip_ring(col, "voussoir", M, outer, inner, -1.4, 0, "stone")
        tm = (a0 + a1) / 2
        pu, pz = -(w + 1.4) * math.cos(tm), zs + (w + 1.4) * math.sin(tm)
        Mp = M @ Matrix.Translation((pu, -1.4, pz)) @ Matrix.Rotation(math.pi / 2, 4, "X") @ \
            Matrix.Rotation(-(tm - math.pi / 2), 4, "Z")
        box(col, "plaque", (0, 0, 0), (1, 1, 1), "ink").matrix_world = Mp @ Matrix.Translation((-.85, -.85, 0)) @ \
            Matrix.Diagonal((1.7, 1.7, .25, 1))
        piece = lathe(col, "plaque piece", [(0, 0), (.45, 0), (.3, .3), (.18, .8), (.32, 1.05), (0, 1.2)], (0, 0, 0),
                      "cloth", segs=8)
        piece.matrix_world = Mp @ Matrix.Translation((0, -.6, .25)) @ Matrix.Rotation(-math.pi / 2, 4, "X")
    crown = zs + w + 2.8
    placed_plate(col, "exit keystone", M @ Matrix.Rotation(math.pi / 2, 4, "X"),
                 [(-1.1, crown - 2.9), (1.1, crown - 2.9), (1.6, crown + .9), (-1.6, crown + .9)], 0, 1.8, "stone",
                 bevel=.12)
    for s in (-1, 1):
        lathe(col, "exit finial", [(0, zs), (1.0, zs), (1.0, zs + .4), (.5, zs + .7), (.45, zs + 1.0), (1.1, zs + 1.9),
                                   (.9, zs + 2.7), (.3, zs + 3.1), (0, zs + 3.2)], (0, 0, 0), "brass", segs=16
              ).matrix_world = M @ Matrix.Translation((s * 11.2, -1.0, 0))


def build_ceiling(col):
    """Coffered ceiling: cream field at the prefab's underside (y 58), walnut beams dressing the prefab's beam lines
    with brass bands, a moulded walnut frame in every coffer and a brass rosette."""
    r = RM
    X, Y0, Y1, CE = r["x"], r["y0"], r["y1"], r["ceil"]
    box(col, "ceiling field", (-X, Y0, CE), (X, Y1, CE + 1.0), "stone")
    xs = [-X, -96, -48, 0, 48, 96, X]
    ys = [Y0, 0, 40, 80, 120, 160, Y1]
    for x in xs[1:-1]:
        box(col, "long beam", (x - 1.6, Y0, CE - 2.2), (x + 1.6, Y1, CE), "walnut", .25, 2)
        box(col, "long beam band", (x - 1.0, Y0, CE - 2.3), (x + 1.0, Y1, CE - 2.2), "brass")
    for y in ys[1:-1]:
        box(col, "cross beam", (-X, y - 1.6, CE - 2.6), (X, y + 1.6, CE), "walnut", .25, 2)
        box(col, "cross beam band", (-X, y - 1.0, CE - 2.7), (X, y + 1.0, CE - 2.6), "brass")
        for x in xs[1:-1]:
            box(col, "beam boss", (x - 2.0, y - 2.0, CE - 3.0), (x + 2.0, y + 2.0, CE - 2.4), "walnut", .3, 2)
            dome(col, "beam boss stud", Matrix.Translation((x, y, CE - 3.0)) @ Matrix.Rotation(math.pi, 4, "X"), .8,
                 "brass", segs=12)
    rosette = [(0, -.7), (.8, -.7), (1.5, -.55), (2.1, -.3), (2.4, 0), (0, 0)]
    for xa, xb in zip(xs, xs[1:]):
        for ya, yb in zip(ys, ys[1:]):
            cu, cv = (xa + xb) / 2, (ya + yb) / 2
            U, V = (xb - xa) - 4.0, (yb - ya) - 4.0
            down = Matrix.Translation((cu, cv, CE)) @ Matrix.Rotation(math.pi, 4, "X")
            sweep_rect(col, "coffer frame", down, U, V, [(0, .9), (.3, 1.1), (.8, .8), (1.2, .5)], "walnut")
            lathe(col, "rosette", rosette, (cu, cv, CE), "brass", segs=20)


def build_floor(col):
    """The room floor (prefab top y -22): walnut boards along the room with staggered joints in a cream stone
    border, and the alcove rug under the reading corner (prefab "Reading alcove rug" x 90 .. 120, z 44 .. 80)."""
    r = RM
    X, Y0, Y1, F = r["x"], r["y0"], r["y1"], r["floor"]
    border = 4.0
    box(col, "floor bed", (-X, Y0, F - 1.0), (X, Y1, F - .2), "stone")
    for lo, hi in (((-X, Y0, F - .6), (X, Y0 + border, F)), ((-X, Y1 - border, F - .6), (X, Y1, F)),
                   ((-X, Y0 + border, F - .6), (-X + border, Y1 - border, F)),
                   ((X - border, Y0 + border, F - .6), (X, Y1 - border, F))):
        box(col, "floor border", lo, hi, "stone", .08)
    rng = __import__("random").Random(11)
    inner = X - border
    n = int(round(2 * inner / 3.0))
    wd = 2 * inner / n
    for i in range(n):
        x0 = -inner + i * wd
        y = Y0 + border + rng.uniform(0, 20)
        start = Y0 + border
        cuts = [start]
        while y < Y1 - border - 6:
            cuts.append(y)
            y += rng.uniform(22, 34)
        cuts.append(Y1 - border)
        for a, b in zip(cuts, cuts[1:]):
            box(col, "floor board", (x0 + .05, a + .05, F - .4), (x0 + wd - .05, b - .05, F), "walnut")
    # rug: cream cloth field, red leather border, a brass-thread line
    box(col, "rug field", (90.0, 44.0, F), (120.0, 80.0, F + .22), "cloth", .1)
    for lo, hi in (((90.0, 44.0, F + .2), (120.0, 46.2, F + .3)), ((90.0, 77.8, F + .2), (120.0, 80.0, F + .3)),
                   ((90.0, 46.2, F + .2), (92.2, 77.8, F + .3)), ((117.8, 46.2, F + .2), (120.0, 77.8, F + .3))):
        box(col, "rug border", lo, hi, "leather", .05)
    for lo, hi in (((93.0, 47.0, F + .22), (117.0, 47.3, F + .3)), ((93.0, 76.7, F + .22), (117.0, 77.0, F + .3)),
                   ((93.0, 47.3, F + .22), (93.3, 76.7, F + .3)), ((116.7, 47.3, F + .22), (117.0, 76.7, F + .3))):
        box(col, "rug line", lo, hi, "brass")


# ------------------------------------------------------------------ the final exit walkway (D4)
# Prefab "Final exit walkway" x -6 .. 6, Unity z 100 .. 170, y -1.2 .. 0 (the walking top y 0, its collider), a brass
# edge, aprons x +-5.3 .. 5.8, four carved legs (x +-4.8, z 101.2 / 168.8, y -21 .. -1) with ferrules. Here: walnut
# planks laid across with their top flush at y 0, a brass edge band, a low walnut curb on each side (0.35 m: under
# the 0.4 m rule) with a brass cap and brass bosses, aprons with brass beads, turned legs on brass feet down to the
# room floor (y -22), cross braces between them.

WK = dict(x=6.0, y0=100.0, y1=170.0, top=0.0, floor=-22.0, legs=(101.2, 168.8))


def build_walkway(col):
    w = WK
    X, Y0, Y1, T, FL = w["x"], w["y0"], w["y1"], w["top"], w["floor"]
    box(col, "walk bed", (-X + .1, Y0 + .05, T - 1.2), (X - .1, Y1 - .05, T - .35), "walnut")
    n = int(round((Y1 - Y0) / 1.6))
    step = (Y1 - Y0) / n
    for i in range(n):
        y0 = Y0 + i * step
        box(col, "walk plank", (-X + .7, y0 + .06, T - .4), (X - .7, y0 + step - .06, T), "varnished" if i % 5 == 2
            else "walnut", .05)
    for s in (-1, 1):
        a, b = sorted((s * (X - .7), s * X))
        box(col, "walk curb", (a, Y0, T - 1.25), (b, Y1, T + .28), "walnut", .1, 2)
        box(col, "walk curb cap", (a - .04, Y0 + .02, T + .28), (b + .04, Y1 - .02, T + .35), "brass", .03)
        for i in range(12):
            y = Y0 + 3 + i * (Y1 - Y0 - 6) / 11
            dome(col, "curb boss", frame_of((s * X, y, T - .45), (0, s, 0), (0, 0, 1)), .3, "brass", segs=10)
        c0, c1 = sorted((s * (X + .1), s * (X - .05)))
        box(col, "walk edge band", (c0, Y0 - .1, T - 1.33), (c1, Y1 + .1, T - 1.0), "brass", .03)
        d0, d1 = sorted((s * 5.3, s * 5.8))
        box(col, "walk apron", (d0, Y0 + .5, T - 3.0), (d1, Y1 - .5, T - 1.2), "walnut", .1)
        box(col, "walk apron bead", (d0 - .05, Y0 + .5, T - 3.2), (d1 + .05, Y1 - .5, T - 2.9), "brass", .04)
    for yy in (Y0 + .1, Y1 - .1):
        box(col, "walk end band", (-X - .08, yy - .1, T - 1.31), (X + .08, yy + .1, T - 1.02), "brass", .03)
    for lx in (-4.8, 4.8):
        for ly in w["legs"]:
            box(col, "walk leg block", (lx - .95, ly - .95, T - 3.4), (lx + .95, ly + .95, T - 1.22), "walnut", .15, 2)
            lathe(col, "walk leg", [(0, FL + 1.0), (.75, FL + 1.0), (.8, FL + 1.6), (1.15, FL + 4.5), (1.25, FL + 7.0),
                                    (1.0, FL + 9.5), (.7, FL + 12.0), (.65, FL + 15.5), (.85, FL + 16.5),
                                    (.7, FL + 17.2), (.75, T - 3.4), (0, T - 3.4)], (lx, ly, 0), "walnut", segs=16)
            lathe(col, "walk leg ring", [(0, FL + 16.0), (.95, FL + 16.0), (.95, FL + 16.5), (0, FL + 16.5)],
                  (lx, ly, 0), "brass", segs=16)
            lathe(col, "walk foot", [(0, FL), (1.0, FL), (1.0, FL + .5), (.8, FL + 1.1), (0, FL + 1.1)], (lx, ly, 0),
                  "brass", segs=16)
    # cross braces: across under each pair of legs and along each side
    for ly in w["legs"]:
        box(col, "walk brace", (-4.4, ly - .35, FL + 9.0), (4.4, ly + .35, FL + 9.8), "walnut", .1)
    for lx in (-4.8, 4.8):
        box(col, "walk brace", (lx - .35, w["legs"][0] + .8, FL + 9.0), (lx + .35, w["legs"][1] - .8, FL + 9.8),
            "walnut", .1)


# ------------------------------------------------------------------ z-fighting check
def zfight_report(ob, tol=.004, min_area=.02):
    """Pairs of faces from different connected parts that lie in the same plane, face the same way and overlap
    (they flicker in Unity), leaving out faces buried in front: a point just in front of the face inside another part's
    box, or against the room's shell (walls x +-123, z -9 / 169, ceiling y 58, floor y -22, facing into them).
    Returns [(area, centre, normal, part a, part b)], biggest first."""
    import bmesh as _bm
    from collections import defaultdict
    bm = _bm.new()
    bm.from_mesh(ob.data)
    bm.transform(ob.matrix_world)
    bm.faces.ensure_lookup_table()
    part = {}
    pid = 0
    for f in bm.faces:
        if f.index in part:
            continue
        stack = [f]
        part[f.index] = pid
        while stack:
            g = stack.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in part:
                        part[h.index] = pid
                        stack.append(h)
        pid += 1
    lo, hi = {}, {}
    for f in bm.faces:
        k_ = part[f.index]
        for p in f.verts:
            c = p.co
            if k_ not in lo:
                lo[k_], hi[k_] = c.copy(), c.copy()
            else:
                lo[k_] = Vector(map(min, lo[k_], c)); hi[k_] = Vector(map(max, hi[k_], c))
    shell = ((0, 1, 123.0), (0, -1, -123.0), (1, 1, 169.0), (1, -1, -9.0), (2, 1, 58.0), (2, -1, -22.0))

    def buried(f, skip):
        n, c = f.normal, f.calc_center_median()
        for ax, sg, val in shell:
            if n[ax] * sg > .9 and abs(c[ax] - val) < .05:
                return True
        q = c + n * .05
        for k_, a in lo.items():
            if k_ in skip:
                continue
            b = hi[k_]
            if a.x < q.x < b.x and a.y < q.y < b.y and a.z < q.z < b.z:
                return True
        return False
    planes = defaultdict(list)
    for f in bm.faces:
        if f.calc_area() < min_area:
            continue
        n = f.normal
        key = (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(n.dot(f.calc_center_median()) / tol))
        planes[key].append(f)
    hits = []
    for key, fs in planes.items():
        if len(fs) < 2:
            continue
        n = fs[0].normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        u, v = [i for i in range(3) if i != ax]
        boxes = [(f, min(p.co[u] for p in f.verts), max(p.co[u] for p in f.verts),
                  min(p.co[v] for p in f.verts), max(p.co[v] for p in f.verts)) for f in fs]
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                fa, a0, a1, b0, b1 = boxes[i]
                fb, c0, c1, d0, d1 = boxes[j]
                if part[fa.index] == part[fb.index]:
                    continue
                ou, ov = min(a1, c1) - max(a0, c0), min(b1, d1) - max(b0, d0)
                if ou > .02 and ov > .02 and not buried(fa, (part[fa.index], part[fb.index])):
                    hits.append((ou * ov, tuple(round(c, 2) for c in fa.calc_center_median()),
                                 tuple(round(c, 2) for c in n), part[fa.index], part[fb.index]))
    bm.free()
    hits.sort(reverse=True)
    return hits

# ------------------------------------------------------------------ look stills from inside the room
def room_still(fbxs, out, cam, target, lens=18.0, res=(1600, 1000)):
    """Renders the given FBX files together, in the library materials (rl_lookdev's), from a perspective camera
    inside the room (cam, target in Blender metres), lit by a soft warm area light under the ceiling and a dim world."""
    src = open(os.path.join(HERE, "rl_lookdev.py"), encoding="utf-8").read()
    g = {"__name__": "rl_lookdev_lib", "__file__": os.path.join(HERE, "rl_lookdev.py")}
    exec(src[:src.rindex("\nmain()")], g)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for f in fbxs:
        bpy.ops.import_scene.fbx(filepath=f)
    cache = {}
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        for s in o.material_slots:
            key = s.material.name.split("|")[-1].split("_", 1)[-1].strip().lower() if s.material else ""
            key = s.material.name.split("|")[-1].strip().lower() if s.material and "|" in s.material.name else key
            if key in g["SLOT"]:
                cache.setdefault(key, g["library_material"](g["SLOT"][key]))
                s.material = cache[key]
    for m in bpy.data.materials:            # as Unity draws them: front faces only
        m.use_backface_culling = True
        try:
            m.use_backface_culling_shadow = True
        except AttributeError:
            pass
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "AgX"
    sc.render.resolution_x, sc.render.resolution_y = res
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.95, .82, .66, 1); bg.inputs["Strength"].default_value = .35
    for loc, energy in (((0, 80, 48), 1.2e6), ((60, 40, 30), 3.0e5), ((-60, 120, 30), 3.0e5)):
        L = bpy.data.objects.new("lamp", bpy.data.lights.new("lamp", "AREA")); sc.collection.objects.link(L)
        L.data.shape = "RECTANGLE"; L.data.size, L.data.size_y = 160, 120
        L.data.energy = energy; L.data.color = (1.0, .86, .68); L.location = loc
    camo = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(camo); sc.camera = camo
    camo.data.lens = lens; camo.data.clip_end = 2000
    camo.location = Vector(cam)
    camo.rotation_euler = (Vector(target) - Vector(cam)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("[r5] still", out)


MODELS = {
    "table": ("RlRoomOakTable", build_table, None),
    "chairs": ("RlRoomCommanderChairs", build_chair, None),
    "footstools": ("RlRoomFootstools", build_footstool, None),
    "chaise": ("RlRoomReadingChaise", build_chaise, None),
    "sidetable": ("RlRoomSideTable", build_side_table, None),
    "portrait": ("RlRoomPortrait", build_portrait, None),
    "tray": ("RlRoomPieceTrays", build_tray, None),
    "doorway": ("RlRoomDoorway", build_doorway, None),
    "leafl": ("RlRoomDoorLeafL", build_door_leaf_l, ("Solid command door leaf", 0)),
    "leafr": ("RlRoomDoorLeafR", build_door_leaf_r, ("Solid command door leaf", 1)),
    "walls": ("RlRoomWalls", build_walls, None),
    "ceiling": ("RlRoomCeiling", build_ceiling, None),
    "floor": ("RlRoomFloor", build_floor, None),
    "walkway": ("RlRoomExitWalkway", build_walkway, None),
    "_doors": ("RlRoomDoorPreview", build_door_preview, None),   # '_': look stills only, not in "all"
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if argv and argv[0] == "--still":   # --still <out.png> <cam x,y,z> <target x,y,z> <lens> <fbx> ...
        room_still(argv[5:], argv[1], tuple(map(float, argv[2].split(","))), tuple(map(float, argv[3].split(","))),
                   float(argv[4]))
        return
    preview = None
    if "--preview" in argv:
        preview = argv[argv.index("--preview") + 1]
        argv.remove(preview)
        argv.remove("--preview")
    no_export = "--no-export" in argv
    global HALF
    HALF = "--half" in argv
    names = [a for a in argv if not a.startswith("--")] or ["all"]
    if "all" in names:
        names = [n for n in MODELS if not n.startswith("_")]
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    if preview:
        k.ROOT = os.path.abspath(preview)
    for key in names:
        model, build, fr = MODELS[key]
        col = collection("R5 " + model)
        build(col)
        ob, _ = finish(col, model)
        if "--zcheck" in argv:
            hits = zfight_report(ob)
            print("[r5] zcheck %s: %d overlapping coplanar face pairs" % (model, len(hits)))
            for h in hits[:12]:
                print("[r5]    area %.2f at %s normal %s" % h[:3])
        if not no_export:
            k.export(col.name, model, ZONE, *(fr or ()))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "rl_r5_chessroom.blend"))


if __name__ == "__main__":
    main()
