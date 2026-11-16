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


def box(col, name, lo, hi, mat="RCY | clay", bevel=0.0, segments=2):
    lo, hi = Vector(lo), Vector(hi)
    bm = _box_bm((lo + hi) / 2, hi - lo)
    ob = mesh_object(name, bm, col, (mat,))
    if bevel > 0:
        bevel_mod(ob, bevel, segments)
    return ob


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
            parts.append(hide_sides(box(col, name, blo, bhi, mat, bevel=.035, segments=1), sides, flat_only=True))
    return join(col, name, parts)


def flagstones(col, name, x0, x1, y0, y1, top=0.0, size=1.0, mat=STONE, rng=None):
    """A paved top: beveled slabs in running bond, their tops at `top`, on a base slab 6 cm down. Only what can be
    seen is kept: a slab's bottom and the joints between slabs are dropped, the edge of the paving stays."""
    rng = rng or rnd
    parts = [box(col, name + " base", (x0, y0, top - 1.0), (x1, y1, top - .06), mat)]
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
            parts.append(hide_sides(box(col, name, (a, y, top - .06), (b, y + d, top), mat, bevel=.025, segments=1), sides))
    return join(col, name, parts)


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

