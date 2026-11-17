# Royal Carriage Yard models (R678, seoungtest): the shared kit every zone script runs on.
#
#   In Blender (BlenderMCP or the text editor): exec(open(r".../rcy_kit.py").read())
#
# Frames. Unity's map frame (the RoyalCarriageYard root, +z = the way north, y up) goes to Blender as
#   Blender x, y, z = Unity x, z, y
# which is the garden's convention (R412): the FBX is written -Z forward / Y up and Unity hangs it under a pivot turned
# 180 degrees about y (RoyalCarriageYard.Dressing.cs). A moving piece is modelled in ITS obstacle's own frame the same way
# (its pivot - an axle, a hinge - at the Blender origin).
#
# Guides. guides/rcy_built_play.json is every node the map built in Play (Unity MCP dump, R678): world pose, the primitive
# drawn, the colliders. guide(prefix) draws a prefix's colliders as red wire boxes (what must not move) and its drawn
# primitives as grey ghosts, in the "RCY guides" collection, which is never exported.
#
# Clay. Every material is grey clay (승규: modelling without textures is one grey clay material unless he asks for
# colour). The slot NAMES say what each surface will be ("RCY | walnut", "RCY | marble" ...) so a later colour pass can
# map them; Unity draws them all in the hall's clay for now.

import bpy, bmesh, json, math, os
from mathutils import Vector, Quaternion, Matrix

# The project root: set RCY_ROOT in the exec namespace or the environment on another PC (the default is the PC the
# yard was first modelled on).
RCY_ROOT = globals().get("RCY_ROOT") or os.environ.get("RCY_ROOT") or r"C:/Work/ChessFight-SeoungTest"
RCY_HERE = os.path.join(RCY_ROOT, "ArtSource/PawnRush/CarriageYardModels")
RCY_DUMP = os.path.join(RCY_HERE, "guides", "rcy_built_play.json")
CLAY = (0.62, 0.62, 0.62, 1.0)

_dump = None


def dump():
    global _dump
    if _dump is None:
        with open(RCY_DUMP, encoding="utf-8") as f:
            _dump = json.load(f)["nodes"]
    return _dump


def nodes(prefix):
    return [n for n in dump() if n["path"].startswith(prefix)]


def node(path):
    for n in dump():
        if n["path"] == path:
            return n
    raise KeyError(path)


def u2b(v):
    """A Unity point or direction to Blender (x, z, y)."""
    return Vector((v[0], v[2], v[1]))


def uq2b(q):
    """A Unity rotation (x, y, z, w) to Blender: the y/z swap is a mirror, so the angle flips."""
    return Quaternion((q[3], -q[0], -q[2], -q[1]))


def uscale2b(s):
    return Vector((s[0], s[2], s[1]))


def collection(name, parent=None, hide_render=False):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(col)
    col.hide_render = hide_render
    return col


def clear(col):
    for ob in list(col.objects):
        bpy.data.objects.remove(ob, do_unlink=True)


def material(name, color=CLAY, alpha=1.0):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = color
    p.inputs["Roughness"].default_value = 0.8
    p.inputs["Alpha"].default_value = alpha
    m.diffuse_color = (color[0], color[1], color[2], alpha)
    if alpha < 1.0:
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            pass
    return m


def mesh_object(name, bm, col, mat_names=("RCY | clay",)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for mn in mat_names:
        me.materials.append(material(mn))
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


# ------------------------------------------------------------------ guides


def _box_bm(center, size, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    # R680 QA (hard-surface): a box given lo > hi on an axis had a negative size and came out mirrored, its faces
    # pointing inward (12 shells on the giant waterwheel). A centred box is the same with the size's magnitude.
    m = Matrix.Diagonal(Vector((abs(size[0]), abs(size[1]), abs(size[2]), 1.0)))
    if rot is not None:
        m = rot.to_matrix().to_4x4() @ m
    m = Matrix.Translation(center) @ m
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def guide(prefix, name=None, ghosts=True, colliders=True, skip=()):
    """Draws a prefix's colliders (red wire) and drawn primitives (grey ghost) into 'RCY guides/<name>'."""
    root = collection("RCY guides", hide_render=True)
    col = collection("Guide " + (name or prefix.strip("/")), root, hide_render=True)
    clear(col)
    red = material("RCY guide | collider", (1.0, 0.05, 0.05, 1.0))
    ghost = material("RCY guide | drawn", (0.75, 0.8, 0.9, 1.0), alpha=0.25)
    made = 0
    for n in nodes(prefix):
        if any(s in n["path"] for s in skip):
            continue
        pos, rot, scl = u2b(n["pos"]), uq2b(n["rot"]), uscale2b(n["scale"])
        leaf = n["path"].split("/")[-1]
        if colliders:
            for c in n.get("cols", []):
                if c["trigger"]:
                    continue
                if c["type"] == "BoxCollider":
                    centre = pos + rot @ Vector((scl.x * c["center"][0], scl.y * c["center"][2], scl.z * c["center"][1]))
                    size = Vector((abs(scl.x * c["size"][0]), abs(scl.y * c["size"][2]), abs(scl.z * c["size"][1])))
                    bm = _box_bm(centre, size, rot)
                else:
                    lo, hi = u2b(c["min"]), u2b(c["max"])
                    bm = _box_bm((lo + hi) / 2, Vector([abs(a - b) for a, b in zip(hi, lo)]))
                ob = bpy.data.objects.new("COL " + leaf, bpy.data.meshes.new("COL " + leaf))
                bm.to_mesh(ob.data)
                bm.free()
                ob.data.materials.append(red)
                ob.display_type = "WIRE"
                ob.show_in_front = True
                col.objects.link(ob)
                made += 1
        if ghosts and n.get("draw") == 1 and n["mesh"] in ("Cube", "Cylinder", "Sphere"):
            bm = bmesh.new()
            if n["mesh"] == "Cube":
                bmesh.ops.create_cube(bm, size=1.0)
            elif n["mesh"] == "Cylinder":   # Unity's cylinder: 2 high along its y, 1 across
                bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=.5, radius2=.5, depth=2.0)
                bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
            else:
                bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=.5)
            # The local frame swapped too (Unity local y = Blender local z).
            m = Matrix.Translation(pos) @ rot.to_matrix().to_4x4() @ Matrix.Diagonal(Vector((scl.x, scl.y, scl.z, 1.0)))
            bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
            ob = bpy.data.objects.new("DRAWN " + leaf, bpy.data.meshes.new("DRAWN " + leaf))
            bm.to_mesh(ob.data)
            bm.free()
            ob.data.materials.append(ghost)
            ob.display_type = "SOLID"
            col.objects.link(ob)
            made += 1
    return made


# ------------------------------------------------------------------ modelling helpers (Blender frame, metres)


def box(col, name, lo, hi, mat="RCY | clay", bevel=0.0, segments=2, bevel_face=None):
    lo, hi = Vector(lo), Vector(hi)
    bm = _box_bm((lo + hi) / 2, hi - lo)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        mod = bevel_mod(ob, bevel, segments)
        if bevel_face and mod is not None:
            face_edges(ob, bevel_face)
            mod.limit_method = "WEIGHT"
    return ob


def face_edges(ob, face):
    """R682 (asset-optimization): marks for the bevel only the four edges round one face of a box part ('+x' ... '-z': a
    wall block's coursed face, a slab's top), so its hidden sides and back stay plain - the same look from the front at
    about a third of the triangles of a bevel on all twelve edges."""
    me = ob.data
    i = "xyz".index(face[1])
    cos = [v.co[i] for v in me.vertices]
    plane = max(cos) if face[0] == "+" else min(cos)
    on = [abs(c - plane) < 1e-6 for c in cos]
    # FACE_BEVEL_CORNERS: the four edges running back from the face too, so its corners round off instead of mitring.
    pick = (lambda a, b: a or b) if FACE_BEVEL_CORNERS else (lambda a, b: a and b)
    att = me.attributes.get("bevel_weight_edge") or me.attributes.new("bevel_weight_edge", "FLOAT", "EDGE")
    att.data.foreach_set("value", [1.0 if pick(on[e.vertices[0]], on[e.vertices[1]]) else 0.0 for e in me.edges])


FACE_BEVEL_CORNERS = True


def bevel_mod(ob, width, segments=2):
    mod = ob.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(40)
    mod.harden_normals = False
    return mod


def cylinder(col, name, centre, axis, radius, length, mat="RCY | clay", segments=24, bevel=0.0, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=length)
    q = Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized())
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(centre)) @ q.to_matrix().to_4x4(), verts=bm.verts)
    # R680 QA (hard-surface): round sides shade smooth (they were faceted: tyres strobed as the wheels rolled); join()
    # sharp_creases keeps the caps' rims sharp. smooth=False for shapes meant to read faceted (hex bolt heads).
    for f in bm.faces:
        f.smooth = smooth
    sharp_creases(bm)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        bevel_mod(ob, bevel)
    return ob


def join(col, name, objs):
    """Applies each object's modifiers and joins them into one object named `name` (fewer draw calls in Unity)."""
    objs = [o for o in objs if o is not None]
    if not objs:
        return None
    bpy.context.view_layer.update()   # rotations set this run reach matrix_world
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    mats, slot = [], {}
    for o in objs:
        ev = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        me.transform(o.matrix_world)
        index = {}
        for i, m in enumerate(me.materials):
            if m.name not in slot:
                slot[m.name] = len(mats)
                mats.append(m)
            index[i] = slot[m.name]
        tmp = bmesh.new()
        tmp.from_mesh(me)
        for f in tmp.faces:
            f.material_index = index.get(f.material_index, 0)
        turn_outward(tmp)
        hide = o.get("rcy_hide")
        if hide:
            cull_hidden_sides(tmp, set(hide.lstrip("=").split(",")), flat_only=hide.startswith("="))
        tmp.to_mesh(me)
        tmp.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    # Cleanup (R680 QA): bevels on boxes thinner than twice their width left zero-area faces; dissolve them (1e-5 m).
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
    turn_outward(bm)   # shells the cleanup closed (thin bevelled boards) are checked too
    if RCY_UNFIGHT:
        moved = {}   # part -> the directions it moved: once along each axis (a beam's top and its end)
        for _ in range(int(os.environ.get("RCY_UNFIGHT_PASSES", UNFIGHT_PASSES))):   # a part flush on two sides moves once a pass
            if not unfight(bm, moved):
                break
    if RCY_UVS:
        surface_uvs(bm, kinds={i: TINT_SLOTS[m.name.split(".")[0]] for i, m in enumerate(mats)
                               if m.name.split(".")[0] in TINT_SLOTS})
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


# ------------------------------------------------------------------ surface UVs (R686, the texture pass)

# R686 (seoungtest, 11-17 승규 "재질 텍스쳐 컨셉 아트대로 … 구워", uv-workflow / texture-workflow): every joined model gets
# one UV map in METRES, laid out part by part, so the tiling surface textures (rcy_textures.py, one tile a material,
# Unity's tiling = 1 / the tile's metres) keep one texel density everywhere. A part is a connected piece (one block, one
# slab, one board, one tile course): its own frame (the normal of its biggest face, the biggest face across that, and
# their cross), each face projected on the frame plane it faces most, U along the part's longest side (so wood grain
# runs down a board and a beam, not across it), plus a random offset from the part's position (each block and board
# shows another patch of the tile, so neighbours differ in tone and the tile's repeat does not line up). split_cells
# carries the UVs through its cuts.
RCY_UVS = True
UV_SPREAD = 16.0   # metres: the range of the random offset


# R686 (승규 11-17 "깜빡깜빡 거리는 곳 고쳐", qa/zfight_check.py): two parts with faces looking the same way within a
# few mm (a deck's boards flush with the hull's top, a kerb with the flags, an iron band on a chest's face, a beam's
# face with its posts', corner blocks of two walls) z-fight - the depth test takes one, then the other, frame by frame.
# join() pushes the smaller part of each such pair UNFIGHT_PUSH along that face's normal, so it sits just proud (its
# biggest overlap's, a pass; two passes). Faces looking down, and faces with something right in front of them (a back
# face inside the model), are left: they are never seen, and pushing them made new contacts.
RCY_UNFIGHT = True
UNFIGHT_GAP = .02      # faces this close fight (6 mm still flickered in Unity: 승규 11-17, the basin landing's kerb)
UNFIGHT_PUSH = .02     # and the smaller part moves this far out (clear of the depth test and the shadow bias)
UNFIGHT_PASSES = 3


def open_front(tree, f, n, reach=.5):
    """A face that can be seen: from its middle or a point halfway to any corner, the space in front is open for
    `reach` (a face pressed against another part or inside a wall has something right in front of it)."""
    c = f.calc_center_median()
    for p in [c] + [(c + v.co) / 2 for v in f.verts]:
        if tree.ray_cast(p + n * .01, n, reach)[0] is None:
            return True
    return False


def unfight(bm, moved=None):
    from mathutils.bvhtree import BVHTree
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
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
    if k < 2:
        return 0
    area = [0.0] * k
    for f in bm.faces:
        area[lab[f.index]] += f.calc_area()
    tree = BVHTree.FromBMesh(bm)
    pairs = []   # (part to move, the part it fights, overlap area, normal)
    for f in bm.faces:
        a = f.calc_area()
        if a < 2e-4 or f.normal.z < -.5:
            continue
        c, n = f.calc_center_median(), f.normal
        # Every face passing within UNFIGHT_GAP of this face's middle or of a point halfway to each corner (a ray from
        # the middle found the face itself first when two faces are exactly level, and a flag only partly under the
        # basin's kerb has nothing under its middle: both were missed, 11-17).
        near = []
        for p in [c] + [(c + v.co) / 2 for v in f.verts]:
            near += [hit[2] for hit in tree.find_nearest_range(p, UNFIGHT_GAP)]
        if len(near) <= 5 or not open_front(tree, f, n):
            continue   # nothing but itself near, or inside the model (a back face against another part): never seen
        for gi in dict.fromkeys(near):
            g = bm.faces[gi]
            if g.index == f.index or lab[g.index] == lab[f.index] or g.normal.dot(n) < .97:
                continue
            if abs((g.verts[0].co - c).dot(n)) > UNFIGHT_GAP:
                continue   # near those points only through its edge, not lying in this face's plane
            i, j = lab[f.index], lab[g.index]
            mover = i if (area[i], i) < (area[j], j) else j
            if moved is not None and any(abs(u.dot(n)) > .5 for u in moved.get(mover, ())):
                continue   # moved along this axis already (its first push stands; moving the bigger one cascaded)
            other = j if mover == i else i
            pairs.append((mover, other, a, n.copy()))
            break
    # A part that wraps the other (an iron band round a post or a beam, a tyre on a rim) is flush on opposite sides:
    # pushed one way, its far side would land on the post (rcy-tex-canal-sorting, 11-17: the timber cranes' jib bands
    # flickered after the first push), and swelling it set off the parts round it in turn. Such a part is left: the
    # model makes it stand more than UNFIGHT_GAP proud (sorting_machines_model PROUD = 3 cm).
    # Wrapping is per partner: a slab flush with its neighbours on both sides still moves off a kerb laid over it.
    seen = {}
    for m, o, a, n in pairs:
        seen.setdefault((m, o), []).append(n)
    wraps = {k for k, ns in seen.items() if any(a.dot(b) < -.9 for a in ns for b in ns)}
    best = {}   # part -> (overlap area, normal)
    for m, o, a, n in pairs:
        if (m, o) not in wraps and (m not in best or best[m][0] < a):
            best[m] = (a, n)
    if not best:
        return 0
    verts = {}
    for f in bm.faces:
        if lab[f.index] in best:
            for v in f.verts:
                verts[v] = lab[f.index]
    for v, i in verts.items():
        v.co += best[i][1] * UNFIGHT_PUSH
    if moved is not None:
        for i, (_a, n) in best.items():
            moved.setdefault(i, []).append(n)
    return len(best)


# R686 (승규 11-17 "판석마다 짙은 돌 섞기"): a stone part's tone as a vertex colour ("Color", linear, per part from the
# same position hash as its UV offset), which the yard's tinted Lit (Assets/Art/PawnRushCarriageYard/Shaders/RcyTintLit,
# on RCY_Flag / RCY_Stone / RCY_Shade) multiplies into the albedo: most slabs near 1, about one in six a darker stone,
# a few warmer; wall blocks a gentler spread. Every other part's colour is white (1, 1, 1).
TINT_SLOTS = {"RCY | flag": "flag", "RCY | stone": "wall", "RCY | shade": "wall"}


def part_tint(kind, r):
    u, a, b = r.random(), r.random(), r.random()
    if kind == "flag":
        if u < .17:                                   # a darker stone
            v = .70 + .14 * a
            return (v, v * .965, v * .93)
        if u < .25:                                   # a warmer one
            v = .96 + .05 * a
            return (v, v * .955, v * .89)
        v = .93 + .09 * a
        return (v, v * (1 - .02 * b), v * (1 - .035 * b))
    if kind == "wall":
        if u < .1:
            v = .84 + .08 * a
            return (v, v * .975, v * .95)
        v = .94 + .07 * a
        return (v, v * (1 - .015 * b), v * (1 - .03 * b))
    return (1.0, 1.0, 1.0)


def surface_uvs(bm, salt=0, kinds=None):
    """kinds: material index -> TINT_SLOTS kind ('flag', 'wall'); the parts in those materials get a tone (part_tint)."""
    import zlib
    uv = bm.loops.layers.uv.verify()
    tint = bm.loops.layers.float_color.get("Color") or bm.loops.layers.float_color.new("Color")
    kinds = kinds or {}
    bm.faces.index_update()
    seen = bytearray(len(bm.faces))
    parts = 0
    for f0 in bm.faces:
        if seen[f0.index]:
            continue
        seen[f0.index] = 1
        stack, faces = [f0], []
        while stack:
            f = stack.pop()
            faces.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if not seen[g.index]:
                        seen[g.index] = 1
                        stack.append(g)
        r = _part_uvs(faces, uv, salt, zlib)
        tone = {}
        for f in faces:
            kind = kinds.get(f.material_index)
            if kind not in tone:
                tone[kind] = part_tint(kind, random.Random(r * 7919 + len(tone))) if kind else (1.0, 1.0, 1.0)
            c = tone[kind] + (1.0,)
            for loop in f.loops:
                loop[tint] = c
        parts += 1
    return parts


_UV_AXES = (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))


def _part_uvs(faces, uv, salt, zlib):
    areas = [f.calc_area() for f in faces]
    k = max(range(len(faces)), key=areas.__getitem__)
    a = faces[k].normal.copy()
    if a.length < .5:
        a = Vector((0, 0, 1))
    b, best = None, 0.0
    for f, ar in zip(faces, areas):
        if ar > best and abs(f.normal.dot(a)) < .2:
            b, best = f.normal, ar
    if b is None:
        b = min(_UV_AXES, key=lambda w: abs(w.dot(a)))
    b = (b - a * b.dot(a)).normalized()
    axes = (a, b, a.cross(b))
    verts = {v for f in faces for v in f.verts}
    lo, ext = [], []
    for x in axes:
        ds = [v.co.dot(x) for v in verts]
        lo.append(min(ds))
        ext.append(max(ds) - min(ds))
    long = max(range(3), key=ext.__getitem__)
    c = sum((v.co for v in verts), Vector()) / len(verts)
    key = zlib.crc32(("%d %d %d %d" % (round(c.x * 50), round(c.y * 50), round(c.z * 50), salt)).encode())
    r = random.Random(key)
    ou, ov = r.random() * UV_SPREAD, r.random() * UV_SPREAD
    for f in faces:
        n = f.normal
        k = max(range(3), key=lambda i: abs(n.dot(axes[i])))
        plane = [i for i in range(3) if i != k]
        if long in plane:
            iu = long
            iv = plane[0] if plane[1] == long else plane[1]
        else:
            iu, iv = sorted(plane, key=lambda i: -ext[i])
        au, av, lu, lv = axes[iu], axes[iv], lo[iu], lo[iv]
        for loop in f.loops:
            p = loop.vert.co
            loop[uv].uv = (p.dot(au) - lu + ou, p.dot(av) - lv + ov)
    return key   # the part's hash: surface_uvs draws its tone from it too


CELL = 10.0   # R681: the length of a zone model's cells along Blender y (Unity z), split_cells


def split_cells(ob, size=CELL):
    """R681 (seoungtest, lod-pipeline / scene-assembly): cuts a joined zone model into one object per `size` m cell
    along Blender y (= Unity z, the way north), so Unity can cull the cells off screen (and out of the shadows' reach)
    instead of drawing a whole zone's 100-200k triangles whenever any of it shows.

    The model's length is shared into equal cells as near `size` as fit (no sliver at either end). A part (a connected
    piece) no longer than a cell goes whole to the cell its middle is in; a longer one (a shed roof's tile course, a
    wall's coping, a base slab) is cut at the cell lines. Positions are the joined model's, and so are the normals: the
    corner normals are carried through the cut as custom normals, so no seam shades where a smooth part is cut. Each
    cell keeps only the material slots it uses. Returns the cell objects, south to north, named
    '<name> z<cell start in whole m, three digits>' (Unity sorts the FBX's children by name)."""
    me, name = ob.data, ob.name
    col = ob.users_collection[0]
    mats = list(me.materials)
    ys = [v.co.y for v in me.vertices]
    lo_y = min(ys)
    count = max(1, round((max(ys) - lo_y) / size))
    size = (max(ys) - lo_y) / count

    def at(y):
        return min(count - 1, max(0, math.floor((y - lo_y) / size)))

    normals = [0.0] * (len(me.loops) * 3)
    me.corner_normals.foreach_get("vector", normals)
    att = me.attributes.new("rcy_n", "FLOAT_VECTOR", "CORNER")
    att.data.foreach_set("vector", normals)
    bm = bmesh.new()
    bm.from_mesh(me)
    me.attributes.remove(me.attributes["rcy_n"])
    cell = bm.faces.layers.int.new("rcy_cell")
    bm.faces.index_update()
    seen = set()
    islands = []
    for f0 in bm.faces:
        if f0.index in seen:
            continue
        stack, faces = [f0], []
        seen.add(f0.index)
        while stack:
            f = stack.pop()
            faces.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        islands.append(faces)
    for faces in islands:
        verts = {v for f in faces for v in f.verts}
        y0, y1 = min(v.co.y for v in verts), max(v.co.y for v in verts)
        if y1 - y0 <= size:
            k = at((y0 + y1) / 2)
            for f in faces:
                f[cell] = k
            continue
        geom = list(verts) + list({e for f in faces for e in f.edges}) + faces
        for k in range(1, count):
            line = lo_y + k * size
            if y0 + 1e-4 < line < y1 - 1e-4:
                geom = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-5, plane_co=(0, line, 0), plane_no=(0, 1, 0))["geom"]
        for f in geom:
            if isinstance(f, bmesh.types.BMFace):
                f[cell] = at(f.calc_center_median().y)
    keys = sorted({f[cell] for f in bm.faces})
    out = []
    for k in keys:
        part = bm.copy()
        layer = part.faces.layers.int["rcy_cell"]
        bmesh.ops.delete(part, geom=[f for f in part.faces if f[layer] != k], context="FACES")
        used = sorted({f.material_index for f in part.faces})
        remap = {m: i for i, m in enumerate(used)}
        for f in part.faces:
            f.material_index = remap[f.material_index]
        part.faces.layers.int.remove(layer)
        cme = bpy.data.meshes.new("%s z%03d" % (name, math.floor(lo_y + k * size + 1e-6)))
        part.to_mesh(cme)
        part.free()
        for m in used:
            cme.materials.append(mats[m])
        vals = [0.0] * (len(cme.loops) * 3)
        cme.attributes["rcy_n"].data.foreach_get("vector", vals)
        cme.attributes.remove(cme.attributes["rcy_n"])
        cme.normals_split_custom_set([Vector(vals[i:i + 3]).normalized() for i in range(0, len(vals), 3)])
        cob = bpy.data.objects.new(cme.name, cme)
        col.objects.link(cob)
        out.append(cob)
    bm.free()
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    return out


SHARP_ANGLE = math.radians(65)


def sharp_creases(bm, angle=SHARP_ANGLE):
    """R680 QA (hard-surface): on a smooth-shaded round part, the creases sharper than `angle` (a cylinder's caps, a
    drum's 90-degree grooves) stay sharp while its segments shade round. Only for parts that ask: soft shapes (bales,
    sacks, hides) are smoothed across their corners on purpose."""
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > angle:
            e.smooth = False


def hide_sides(ob, sides, flat_only=False):
    """Marks sides of a box part ('+x', '-x', '+y', '-y', '+z', '-z') that touch a neighbour or the ground, so join()
    drops the faces that only look that way (R680 QA, asset-optimization: unseen faces of paving slabs and wall blocks).
    flat_only: only the flat faces, never a chamfer or corner between two sides."""
    if sides:
        ob["rcy_hide"] = ("=" if flat_only else "") + ",".join(sorted(sides))
    return ob


def cull_hidden_sides(bm, hidden, flat_only=False):
    """Deletes the faces whose every outward direction (a normal component over .3) is a hidden side: a slab's bottom,
    the joint faces between slabs and the chamfers between two hidden sides. A face that also looks up or out to a free
    side stays (the top, its chamfers, the edge of the paving)."""
    dead = []
    for f in bm.faces:
        n = f.normal
        dirs = {("+" if n[i] > 0 else "-") + "xyz"[i] for i in range(3) if abs(n[i]) > .3}
        if dirs and dirs <= hidden and (len(dirs) == 1 or not flat_only):
            dead.append(f)
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")


def turn_outward(bm):
    """R680 QA (hard-surface / validation): every closed shell of a part whose faces point inward (negative signed
    volume: a mirrored placement, an outline drawn the other way round) is turned outward. Open sheets are left alone."""
    bm.faces.index_update()
    seen = set()
    for f0 in bm.faces:
        if f0.index in seen:
            continue
        stack, shell = [f0], []
        seen.add(f0.index)
        while stack:
            f = stack.pop()
            shell.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        if any(len(e.link_faces) != 2 for f in shell for e in f.edges):
            continue
        vol = 0.0
        for f in shell:
            vs = [v.co for v in f.verts]
            for i in range(1, len(vs) - 1):
                vol += vs[0].dot(vs[i].cross(vs[i + 1]))
        if vol < 0:
            bmesh.ops.reverse_faces(bm, faces=shell)


def _covers(spans, a, b, eps=1e-4):
    """True if the touching intervals `spans` cover a..b."""
    at = a
    for s0, s1 in sorted(spans):
        if s0 > at + eps:
            break
        at = max(at, s1)
        if at >= b - eps:
            return True
    return at >= b - eps


def _joint_sides(spans, k, axis):
    """Which ends of span k (along `axis`) butt on a neighbour span in the same row."""
    a, b = spans[k]
    out = set()
    if any(abs(s1 - a) < 1e-6 for j, (s0, s1) in enumerate(spans) if j != k):
        out.add("-" + axis)
    if any(abs(s0 - b) < 1e-6 for j, (s0, s1) in enumerate(spans) if j != k):
        out.add("+" + axis)
    return out


def tris(col):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in col.all_objects if o.type == "MESH")


# ------------------------------------------------------------------ shared stone and timber (R678; were canal_model.py's)

import random

rnd = random.Random(678)
SHADE, TIMBER, SOIL = "RCY | shade", "RCY | timber", "RCY | soil"   # R686: plinths/footings/stone under water, raw sawn timber, earth (textures/PLAN.md)
FLAG = "RCY | flag"   # R686: paving, its own slot (the concept's flagstone swatch, warmer and worn smoother than the walls')
STONE, MARBLE, WALNUT, OAK, IRON, BRASS = ("RCY | stone", "RCY | marble", "RCY | walnut", "RCY | oak", "RCY | iron",
                                           "RCY | brass")


def col_box(path, index=0):
    """A blockout collider's box in Blender (lo, hi) from the dump (map frame)."""
    hits = [n for n in dump() if n["path"] == path]
    c = hits[index]["cols"][0]
    return u2b(c["min"]), u2b(c["max"])


def by_path(path):
    return [n for n in dump() if n["path"] == path]


# ------------------------------------------------------------------ stone


# Stone edges (graphics pass R682, 11-16, lookdev): the concept's "big soft-edged blocks" - a wall block's bevel (width m,
# segments), a paving slab's and the slab's depth. Round 7 cm in two steps on wall blocks, a 4.5 cm chamfer on a 10 cm
# slab (were 3.5 cm and 2.5 cm in one step on a 6 cm slab; a side-by-side render chose these). Only the edges round the
# seen face are bevelled (box bevel_face): the old values keep the old all-edge bevel, so a build that sets them back
# comes out as before.
STONE_BEVEL = (.07, 2)
FLAG_BEVEL = (.045, 1)
FLAG_DEPTH = .1


def ashlar(col, name, lo, hi, face, course=.55, length=1.15, mat=STONE, below=None):
    """A wall of beveled stone blocks filling lo..hi, coursed on its `face` ('+x', '-x', '+y', '-y'): the blocks are as
    deep as the wall; below `below` (Blender z, e.g. the water) one plain core instead."""
    lo, hi = Vector(lo), Vector(hi)
    parts = []
    z0 = lo.z
    if below is not None and below > lo.z:
        parts.append(box(col, name + " core", lo, Vector((hi.x, hi.y, below)), mat))
        z0 = below
    along = "y" if face in ("+x", "-x") else "x"
    a0, a1 = getattr(lo, along), getattr(hi, along)
    rows = []   # (z, h, [(b0, b1) ...]); the random lengths are drawn in the same order as before R680 QA
    row = 0
    z = z0
    while z < hi.z - 1e-3:
        h = min(course, hi.z - z)
        a = a0 - (length * .5 if row % 2 else 0)
        spans = []
        while a < a1 - 1e-3:
            l = length * rnd.uniform(.8, 1.2)
            b0, b1 = max(a, a0), min(a + l, a1)
            if b1 - b0 > .05:
                spans.append((b0, b1))
            a += l
        rows.append((z, h, spans))
        z += h
        row += 1
    for r, (z, h, spans) in enumerate(rows):
        for k, (b0, b1) in enumerate(spans):
            if along == "y":
                blo, bhi = Vector((lo.x, b0, z)), Vector((hi.x, b1, z + h))
            else:
                blo, bhi = Vector((b0, lo.y, z)), Vector((b1, hi.y, z + h))
            # Unseen: the flat joint faces between blocks of a course. Not the chamfers, tops or bottoms: a joint
            # over a block runs through the wall in a channel those close (dropping them let the sky through at
            # every joint corner).
            sides = _joint_sides(spans, k, along)
            parts.append(hide_sides(box(col, name, blo, bhi, mat, bevel=min(STONE_BEVEL[0], max(.035, h / 3)), segments=STONE_BEVEL[1],
                                        bevel_face=face if STONE_BEVEL[1] > 1 else None),
                                    sides, flat_only=True))
    return join(col, name, parts)


def flagstones(col, name, x0, x1, y0, y1, top=0.0, size=1.0, mat=FLAG, rng=None):
    """A paved top: beveled slabs in running bond, their tops at `top`, on a base slab 6 cm down. Only what can be
    seen is kept: a slab's bottom and the joints between slabs are dropped, the edge of the paving stays."""
    rng = rng or rnd
    parts = [box(col, name + " base", (x0, y0, top - 1.0), (x1, y1, top - FLAG_DEPTH), mat)]
    rows = []   # (y, d, [(a, b) ...]); the random widths are drawn in the same order as before R680 QA
    row, y = 0, y0
    while y < y1 - 1e-3:
        d = min(size * .8, y1 - y)
        x = x0 - (size * .5 if row % 2 else 0)
        spans = []
        while x < x1 - 1e-3:
            w = size * rng.uniform(.85, 1.15)
            a, b = max(x, x0), min(x + w, x1)
            if b - a > .1:
                spans.append((a, b))
            x += w
        rows.append((y, d, spans))
        y += d
        row += 1
    for r, (y, d, spans) in enumerate(rows):
        for k, (a, b) in enumerate(spans):
            sides = _joint_sides(spans, k, "x") | {"-z"}
            if r > 0 and _covers(rows[r - 1][2], a, b):
                sides.add("-y")
            if r + 1 < len(rows) and _covers(rows[r + 1][2], a, b):
                sides.add("+y")
            parts.append(hide_sides(box(col, name, (a, y, top - FLAG_DEPTH), (b, y + d, top), mat, bevel=FLAG_BEVEL[0],
                                        segments=FLAG_BEVEL[1], bevel_face="+z" if FLAG_DEPTH > .06 else None), sides))
    return join(col, name, parts)


def burn_flagstones(x0, x1, y0, y1, size=1.0, rng=None):
    """R682: draws the numbers flagstones() would for this patch and lays nothing. A floor re-cut into a field and kerb
    bands with their own numbers spends the shared sequence as the one patch did, so every later layout (wall blocks,
    goods) stays as it was."""
    rng = rng or rnd
    y = y0
    row = 0
    while y < y1 - 1e-3:
        d = min(size * .8, y1 - y)
        x = x0 - (size * .5 if row % 2 else 0)
        while x < x1 - 1e-3:
            x += size * rng.uniform(.85, 1.15)
        y += d
        row += 1


def setts_line(col, name, a, b, y0, y1, top, rng):
    """One line of small square setts a..b wide along y (a flagstones() band this narrow would leave its running bond's
    slivers out), on a base like the flags', with the same edges and hidden faces."""
    w = b - a
    parts = [box(col, name + " base", (a, y0, top - 1.0), (b, y1, top - FLAG_DEPTH), FLAG)]
    y = y0
    while y < y1 - 1e-3:
        d = min(w * rng.uniform(.9, 1.15), y1 - y)
        sides = {"-z"} | ({"-y"} if y > y0 + 1e-6 else set()) | ({"+y"} if y + d < y1 - 1e-3 else set())
        parts.append(hide_sides(box(col, name, (a, y, top - FLAG_DEPTH), (b, y + d, top), FLAG, bevel=min(FLAG_BEVEL[0], w / 5),
                                    segments=FLAG_BEVEL[1], bevel_face="+z" if FLAG_DEPTH > .06 else None), sides))
        y += d
    return join(col, name, parts)


def kerbed_floor(col, name, x0, x1, y0, y1, top, kerb_x, size=2.0, kerb=.8, setts=.24, seed=682):
    """R682 (environment-artist, graphics pass): a floor x0..x1 whose long sides get a border that frames the field
    between them (the yards' wide flag fields read as one flat sheet): at +-kerb_x a kerb band of long slabs (`kerb`
    wide, 2.4 m long so their joints never line up with the field's rows) and inside it one line of small square setts.
    Beyond the bands (x0..-kerb_x, kerb_x..x1) plain flags again. Tops all at `top`."""
    burn_flagstones(x0, x1, y0, y1, size)
    inner = kerb_x - kerb - setts
    parts = [flagstones(col, name, -inner, inner, y0, y1, top, size=size, rng=random.Random(seed))]
    for s in (-1, 1):
        a, b = sorted((s * inner, s * (inner + setts)))
        parts.append(setts_line(col, name + " setts", a, b, y0, y1, top, random.Random(seed + 8 + s)))
        a, b = sorted((s * (kerb_x - kerb), s * kerb_x))
        parts.append(flagstones(col, name + " kerb", a, b, y0, y1, top, size=3.0, rng=random.Random(seed + 2 + s)))
        out = sorted((s * kerb_x, x1 if s > 0 else x0))
        if out[1] - out[0] > .05:
            parts.append(flagstones(col, name, out[0], out[1], y0, y1, top, size=size, rng=random.Random(seed + 5 + s)))
    return parts


# ------------------------------------------------------------------ chess pieces as icons (R682)


def _pawn_icon():
    pts = [(.2, 0), (.8, 0), (.8, .1), (.68, .15), (.6, .5), (.66, .56), (.57, .6)]
    for k in range(1, 18):     # the head, round from the right side of the neck over the top to the left
        a = math.radians(-60 + 300 * k / 18)
        pts.append((.5 + .17 * math.cos(a), .77 + .17 * math.sin(a)))
    return pts + [(.43, .6), (.34, .56), (.4, .5), (.32, .15), (.2, .1)]


# Six outlines in a unit box (u right, v up, foot on v 0), for the sorting stalls' plaques (the concept: "a matte brass
# piece-icon plaque" on each stall bay; pictograms, never numbers or letters).
PIECE_ICONS = {
    "pawn": _pawn_icon(),
    "rook": [(.2, 0), (.8, 0), (.8, .1), (.7, .14), (.66, .62), (.76, .68), (.76, .95), (.66, .95), (.66, .85), (.56, .85),
             (.56, .95), (.44, .95), (.44, .85), (.34, .85), (.34, .95), (.24, .95), (.24, .68), (.34, .62), (.3, .14), (.2, .1)],
    "knight": None,   # KNIGHT_HEAD (carving, below)
    "bishop": [(.22, 0), (.78, 0), (.78, .1), (.64, .16), (.58, .42), (.66, .48), (.6, .52), (.66, .62), (.66, .72),
               (.6, .82), (.52, .9), (.5, .92), (.48, .9), (.4, .82), (.34, .72), (.34, .62), (.4, .52), (.34, .48),
               (.42, .42), (.36, .16), (.22, .1)],
    "queen": [(.2, 0), (.8, 0), (.8, .1), (.68, .14), (.62, .55), (.72, .6), (.8, .9), (.68, .72), (.6, .95), (.54, .74),
              (.5, 1.0), (.46, .74), (.4, .95), (.32, .72), (.2, .9), (.28, .6), (.38, .55), (.32, .14), (.2, .1)],
    "king": [(.2, 0), (.8, 0), (.8, .1), (.68, .14), (.62, .55), (.7, .6), (.66, .72), (.54, .72), (.54, .8), (.62, .8),
             (.62, .88), (.54, .88), (.54, .98), (.46, .98), (.46, .88), (.38, .88), (.38, .8), (.46, .8), (.46, .72),
             (.34, .72), (.3, .6), (.38, .55), (.32, .14), (.2, .1)],
}
PIECE_ORDER = ("pawn", "rook", "knight", "bishop", "queen", "king")


def piece_icon(col, name, kind, at, facing, height, depth=.02, mat=WALNUT):
    """A piece's outline raised `depth` off a plaque whose face is the plane x = at.x, looking along Blender x `facing`
    (+1 / -1); `at` is the icon's foot centre, `height` tall. Eased edges so it reads in clay."""
    at = Vector(at)
    out = PIECE_ICONS[kind] or KNIGHT_HEAD
    bm = bmesh.new()
    lo, hi = [], []
    for u, v in out:
        y = at.y + (u - .5) * height * facing     # seen from the front, u runs to the viewer's right
        lo.append(bm.verts.new((at.x, y, at.z + v * height)))
        hi.append(bm.verts.new((at.x + facing * depth, y, at.z + v * height)))
    n = len(out)
    caps = [bm.faces.new(lo), bm.faces.new(list(reversed(hi)))]
    for k in range(n):
        bm.faces.new((lo[k], hi[k], hi[(k + 1) % n], lo[(k + 1) % n]))
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object(name, bm, col, (mat,))
    bevel_mod(ob, min(.006, depth / 3), 1)
    return ob


def crenel_fascia(col, name, x, y0, y1, top, high=.34, low=.18, merlon=.5, gap=.32, thick=.05, mat=OAK):
    """R682 (the concept's granary bay: "a crenel-cut oak fascia and a matte brass edge"): an eave board along y at x,
    its foot cut like a battlement - tabs `high` deep, `merlon` long, between notches `low` deep, `gap` long."""
    n = max(1, int((y1 - y0 + gap) / (merlon + gap)))
    pad = (y1 - y0 - (n * merlon + (n - 1) * gap)) / 2
    pts = [(y0, top), (y1, top), (y1, top - low)]
    ends = []
    for k in reversed(range(n)):
        a = y0 + pad + k * (merlon + gap)
        ends += [(a + merlon, top - low), (a + merlon, top - high), (a, top - high), (a, top - low)]
    pts += ends + [(y0, top - low)]
    bm = bmesh.new()
    lo = [bm.verts.new((x - thick / 2, a, b)) for a, b in pts]
    hi = [bm.verts.new((x + thick / 2, a, b)) for a, b in pts]
    m = len(pts)
    caps = [bm.faces.new(lo), bm.faces.new(list(reversed(hi)))]
    for k in range(m):
        bm.faces.new((lo[k], hi[k], hi[(k + 1) % m], lo[(k + 1) % m]))
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object(name, bm, col, (mat,))
    bevel_mod(ob, .012, 2)
    return ob


# ------------------------------------------------------------------ marble


def baluster(col, centre, height, radius=.09, mat=MARBLE):
    """A turned baluster (a lathe profile) standing at centre (its foot), `height` tall."""
    prof = [(.0, .0), (1.0, .0), (1.0, .1), (.7, .14), (.6, .22), (.95, .42), (1.0, .5), (.8, .65), (.45, .78),
            (.55, .84), (.85, .9), (.85, 1.0), (.0, 1.0)]
    bm = bmesh.new()
    seg = 8
    rings = []
    for r, h in prof:
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            ring.append(bm.verts.new((centre[0] + r * radius * math.cos(a), centre[1] + r * radius * math.sin(a),
                                      centre[2] + h * height)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(seg):
            a, b = rings[i][k], rings[i][(k + 1) % seg]
            c, d = rings[i + 1][(k + 1) % seg], rings[i + 1][k]
            if len({a.co.to_tuple(5), b.co.to_tuple(5), c.co.to_tuple(5), d.co.to_tuple(5)}) >= 3:
                try:
                    bm.faces.new((a, b, c, d))
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    return mesh_object("Baluster", bm, col, (mat,))


def balustrade(col, name, lo, hi, mat=MARBLE):
    """A marble balustrade filling the collider lo..hi (its long side along x or y): plinth, balusters, rail, newels."""
    lo, hi = Vector(lo), Vector(hi)
    along = "x" if hi.x - lo.x >= hi.y - lo.y else "y"
    parts = []
    top = hi.z
    plinth, rail = .2, .14
    parts.append(box(col, name, (lo.x + .02, lo.y + .02, lo.z), (hi.x - .02, hi.y - .02, lo.z + plinth), mat, bevel=.03))
    parts.append(box(col, name, (lo.x - .02, lo.y - .02, top - rail), (hi.x + .02, hi.y + .02, top), mat, bevel=.035))
    a0, a1 = getattr(lo, along), getattr(hi, along)
    newel = .42
    for a in (a0, a1 - newel):
        if along == "x":
            nlo, nhi = (a, lo.y - .01, lo.z), (a + newel, hi.y + .01, top)
        else:
            nlo, nhi = (lo.x - .01, a, lo.z), (hi.x + .01, a + newel, top)
        parts.append(box(col, name + " newel", nlo, nhi, mat, bevel=.04))
    span = (a1 - newel) - (a0 + newel)
    count = max(1, int(span / .3))
    step = span / count
    mid = ((lo.y + hi.y) / 2) if along == "x" else ((lo.x + hi.x) / 2)
    for k in range(count):
        a = a0 + newel + step * (k + .5)
        foot = (a, mid, lo.z + plinth) if along == "x" else (mid, a, lo.z + plinth)
        parts.append(baluster(col, foot, top - rail - lo.z - plinth))
    return join(col, name, parts)


def parapet_wall(col, name, lo, hi, mat=MARBLE):
    """A solid marble parapet as tall as its collider: a base course, recessed panels, a coping."""
    lo, hi = Vector(lo), Vector(hi)
    along = "x" if hi.x - lo.x >= hi.y - lo.y else "y"
    parts = [box(col, name, (lo.x + .03, lo.y + .03, lo.z), (hi.x - .03, hi.y - .03, hi.z - .16), mat)]
    parts.append(box(col, name, (lo.x, lo.y, lo.z), (hi.x, hi.y, lo.z + .25), mat, bevel=.03))
    parts.append(box(col, name, (lo.x, lo.y, hi.z - .16), (hi.x, hi.y, hi.z), mat, bevel=.04))
    a0, a1 = getattr(lo, along), getattr(hi, along)
    n = max(1, int((a1 - a0) / 1.6))
    w = (a1 - a0) / n
    for k in range(n):
        b0, b1 = a0 + k * w + .18, a0 + (k + 1) * w - .18
        for sgn in (-1, 1):   # a raised frame on both faces, 1 cm proud
            if along == "y":
                x = hi.x - .02 if sgn > 0 else lo.x + .02
                parts.append(box(col, name + " panel", (min(x, x + sgn * .03), b0, lo.z + .4), (max(x, x + sgn * .03), b1, hi.z - .32), mat, bevel=.01, segments=1))
            else:
                y = hi.y - .02 if sgn > 0 else lo.y + .02
                parts.append(box(col, name + " panel", (b0, min(y, y + sgn * .03), lo.z + .4), (b1, max(y, y + sgn * .03), hi.z - .32), mat, bevel=.01, segments=1))
    return join(col, name, parts)


# ------------------------------------------------------------------ timber


def square_post(col, name, cx, cy, z0, z1, w=.26, mat=WALNUT, bands=()):
    parts = [box(col, name, (cx - w / 2, cy - w / 2, z0), (cx + w / 2, cy + w / 2, z1), mat, bevel=.03)]
    for z in bands:
        parts.append(box(col, name + " band", (cx - w / 2 - .02, cy - w / 2 - .02, z), (cx + w / 2 + .02, cy + w / 2 + .02, z + .1), IRON, bevel=.01, segments=1))
    return parts


def beam(col, name, a, b, w=.2, h=.2, mat=WALNUT, segments=2):
    """A squared timber from a to b (Blender points)."""
    a, b = Vector(a), Vector(b)
    length = (b - a).length
    bm = _box_bm(Vector((0, 0, 0)), Vector((w, h, length)))
    q = Vector((0, 0, 1)).rotation_difference((b - a).normalized())
    bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4(), verts=bm.verts)
    ob = mesh_object(name, bm, col, (mat,))
    bevel_mod(ob, .02, segments)
    return ob


# ------------------------------------------------------------------ carving (R682)

# A chess knight's head in profile facing -u, its neck's foot on v 0 (unit box; canal_model.py's medallion outline).
KNIGHT_HEAD = [(.18, 0), (.86, 0), (.86, .07), (.78, .1), (.8, .3), (.84, .48), (.82, .64), (.76, .78), (.66, .9),
               (.6, .97), (.55, 1.04), (.5, .94), (.42, .92), (.32, .86), (.22, .76), (.12, .64), (.06, .56), (.04, .5),
               (.07, .44), (.16, .43), (.2, .47), (.26, .44), (.36, .46), (.42, .42), (.4, .32), (.33, .22), (.28, .12),
               (.2, .1)]


def knight_finial(col, name, at, facing, height, thick, mat=WALNUT):
    """R682 (the concept: "carved knight heads appear only high up, on crane and hoist arm ends"): a knight's head carved
    from a beam's end. `at` is the end face's foot (Blender), the head looks along Blender x `facing` (+1 / -1), its
    back of the neck 2 cm into the beam; `height` tall, `thick` across (Blender y). Eased edges so it reads in clay."""
    at = Vector(at)
    bm = bmesh.new()
    lo, hi = [], []
    for u, v in KNIGHT_HEAD:
        x = at.x + facing * ((.86 - u) * height - .02)
        lo.append(bm.verts.new((x, at.y - thick / 2, at.z + v * height)))
        hi.append(bm.verts.new((x, at.y + thick / 2, at.z + v * height)))
    n = len(KNIGHT_HEAD)
    caps = [bm.faces.new(lo), bm.faces.new(list(reversed(hi)))]
    for k in range(n):
        bm.faces.new((lo[k], hi[k], hi[(k + 1) % n], lo[(k + 1) % n]))
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = mesh_object(name, bm, col, (mat,))
    bevel_mod(ob, min(.025, thick / 6), 2)
    return ob

