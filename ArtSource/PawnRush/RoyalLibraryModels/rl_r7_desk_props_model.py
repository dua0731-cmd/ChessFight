"""R7 giant's-desk props (seoungtest R687, 2026-11-18, PLAN.md R7): the far props of concept D "the giant's desk" -
glass ink bottle + white quill, open gold pocket watch + chain, magnifying glass, brass desk lamp (off), teacup on a
saucer, cliff-like leather book stacks, parchment sheets + fountain pen (GC_D6 top two rows; GC_D, D1 ~ D4, E1).

    blender -b --factory-startup --python rl_r7_desk_props_model.py -- layout [--still <dir>]
    blender -b --factory-startup --python rl_r7_desk_props_model.py -- <prop ...|all> [--still <dir>] [--export]

layout: the blockout (RoyalLibrary_blockout.blend, read only) with a proxy of every prop at its PLACES spot, saved as
rl_r7.blend, and stills of the plan and the players' views (stills/r7/layout_*.png) - where the props go is decided on
these before any modelling.

The props stand on the far floors (y -23), round the course and never on or over a walking face: nothing here has a
collider, the prefab's colliders stay. Faces in rl_kit slots only, metre UVs (rl_kit.surface_uvs), FBX by rl_kit.export
to Assets/Resources/RoyalLibrary/Models/Desk/. Never export while Unity is in Play.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

BLOCKOUT = os.path.join(HERE, "RoyalLibrary_blockout.blend")
BLEND = os.path.join(HERE, "rl_r7.blend")
STILLS = os.path.join(HERE, "stills", "r7")
ZONE = "Desk"
FLOOR = -23.0   # Unity y of both far floors (gallery "Distant operations room floor", chess room floor)

# ------------------------------------------------------------------ where the props stand
# The far floors are 23 m under the course, so a prop on the floor hides under the tables (layout try 1, 11-18). Each
# prop stands on a cliff of giant leather books (D6 row 2, E1) whose top is a little under the walking faces, so the
# prop sits beside the course at its level, as on the giant's desk of the concept.
# name: (prop, Unity x, Unity z, turn about y in degrees, prop scale, book cliff (w, d, top y) or None)
PLACES = {
    # chess room (floor y -23, oak table top y -0.8, table x +-41, z -10 .. 102; giants' chairs x +-48 .. 75, z 22 .. 58)
    "C1": ("ink", -62, 6, 20, 1.0, (30, 26, -3)),          # left front, before the black giant's chair (D, D1, D4)
    "C2": ("watch", -68, 82, -25, 1.0, (32, 26, -3)),      # left back, behind the chair (D, D1)
    "C3": ("cup", 62, 6, -15, 1.0, (30, 26, -3)),          # right front (D, D1)
    "C4": ("lamp", 68, 92, 200, 1.0, (30, 28, -3)),        # right back, by the big window, shade towards the board (D)
    "C5": ("magnifier", 28, 128, 30, 1.0, (30, 34, -3)),   # right of the exit walkway (D, D4)
    "C6": ("paper_pen", -28, 124, -10, 1.0, (30, 34, -3)),  # left of the exit walkway (D, D4)
    # front course (floor y -23, ceiling y 30, gallery walls x +-38 with bookcases to x +-33, tables x +-9 .. 12.5)
    "F1": ("ink", -22, -126, 15, .55, (18, 24, -3)),       # left of the start table (E1 left foreground)
    "F2": ("watch", -22, -88, 10, .55, (18, 24, -3)),      # left of the books (E1: watch and chain by the stamp)
    "F3": ("cup_magnifier", 22, -70, -10, .55, (18, 24, -3)),  # right, between the books and the typewriter (E1 right)
    "F4": ("paper_pen", 22, -38, 20, .55, (18, 24, -3)),   # right of the typewriter (E1 right foreground)
}

# rough size of each prop at scale 1 (w, d, h) - the layout proxies; the models replace them
SIZE = {
    "ink": (16, 16, 20),          # bottle; the quill rises to about h 45
    "watch": (22, 10, 26),        # case 22 across, propped up on its open lid, chain hanging over the books
    "cup": (26, 26, 12),          # saucer 26 across, cup 17 across, 11 high
    "cup_magnifier": (26, 26, 12),
    "lamp": (18, 18, 45),         # base 18 across, arm up to h 45, shade 18 across
    "magnifier": (24, 50, 5),     # lens 22 across, handle 26, lying tilted
    "paper_pen": (26, 32, 2),     # three loose sheets, the pen 26 long across them
}


def U(x, y, z):
    """Unity (x, y, z) -> Blender (x, z, y)."""
    return Vector((x, z, y))


# ------------------------------------------------------------------ layout

COLOURS = {"ink": (.05, .07, .12, 1), "watch": (.85, .6, .1, 1), "cup": (.95, .92, .82, 1),
           "cup_magnifier": (.95, .92, .82, 1), "lamp": (.75, .55, .2, 1), "magnifier": (.6, .8, .9, 1),
           "paper_pen": (.95, .85, .6, 1), "books": (.45, .1, .06, 1)}


def flat(name, rgba, glow=.35):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = rgba
    p.inputs["Emission Color"].default_value = rgba
    p.inputs["Emission Strength"].default_value = glow
    m.diffuse_color = rgba
    return m


def proxy(col, key):
    prop, x, z, turn, s, cliff = PLACES[key]
    w, d, h = (v * s for v in SIZE[prop])
    parts = []

    def part(name, kind, size, at, mat=None):
        bm = bmesh.new()
        if kind == "cyl":
            bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=.5, radius2=.5, depth=1)
        else:
            bmesh.ops.create_cube(bm, size=1)
        bmesh.ops.scale(bm, vec=size, verts=bm.verts)
        bmesh.ops.translate(bm, vec=at, verts=bm.verts)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me); bm.free()
        me.materials.append(mat or flat("R7 proxy " + prop, COLOURS[prop]))
        ob = bpy.data.objects.new(name, me)
        col.objects.link(ob)
        parts.append(ob)
        return ob

    n = "%s %s" % (key, prop)
    base = 0.0   # prop's foot, above the root (the cliff's top)
    root = bpy.data.objects.new("R7 " + n, None)
    col.objects.link(root)
    if cliff:
        cw, cd, top = cliff
        hgt = top - FLOOR
        books = max(3, round(hgt / 4.5))
        bt = hgt / books
        for i in range(books):   # the cliff: giant books, a little askew, under the root
            jitter = ((i * 37) % 7 - 3) * .5
            ob = part("%s book %d" % (n, i), "box", (cw * (1 - .03 * (i % 3)), cd * (1 - .04 * ((i + 1) % 3)), bt * .96),
                      (jitter * .4, -jitter * .3, -hgt + bt * (i + .5)), flat("R7 proxy books", COLOURS["books"]))
            ob.rotation_euler = (0, 0, math.radians(jitter * 1.5))
    if prop == "ink":
        part(n + " bottle", "cyl", (w, d, h * .8), (0, 0, h * .4))
        part(n + " neck", "cyl", (w * .45, d * .45, h * .2), (0, 0, h * .9))
        q = part(n + " quill", "box", (w * .1, w * .8, h * 1.3), (0, 0, 0), flat("R7 proxy quill", (1, 1, 1, 1)))
        q.rotation_euler = (math.radians(-18), 0, 0); q.location = (0, h * .2, h * 1.5)
    elif prop == "watch":
        c = part(n + " case", "cyl", (w, w, d * .4), (0, 0, 0))
        c.rotation_euler = (math.radians(75), 0, 0); c.location = (0, 0, w * .5)
        l = part(n + " lid", "cyl", (w, w, d * .15), (0, 0, 0))
        l.rotation_euler = (math.radians(-50), 0, 0); l.location = (0, -w * .4, w * .45)
        part(n + " chain", "box", (w * .15, 1, w * .9), (w * .45, cliff[1] * s * 0 + 8, -w * .3))
    elif prop in ("cup", "cup_magnifier"):
        part(n + " saucer", "cyl", (w, w, 1.2 * s), (0, 0, .6 * s))
        part(n + " cup", "cyl", (w * .65, w * .65, 11 * s), (0, 0, 6.7 * s))
        if prop == "cup_magnifier":
            m = part(n + " lens", "cyl", (24 * s, 24 * s, 2 * s), (0, 0, 0), flat("R7 proxy magnifier", COLOURS["magnifier"]))
            m.rotation_euler = (math.radians(15), 0, 0); m.location = (0, 22 * s, 4 * s)
            part(n + " handle", "box", (4 * s, 26 * s, 4 * s), (0, 44 * s, 2 * s), flat("R7 proxy magnifier", COLOURS["magnifier"]))
    elif prop == "lamp":
        part(n + " base", "cyl", (w, w, 3 * s), (0, 0, 1.5 * s))
        part(n + " arm1", "box", (2 * s, 2 * s, h * .75), (0, 0, h * .4))
        part(n + " arm2", "box", (2 * s, w * 1.4, 2 * s), (0, w * .65, h * .78))
        part(n + " shade", "cyl", (w, w, 10 * s), (0, w * 1.3, h * .68))
    elif prop == "magnifier":
        m = part(n + " lens", "cyl", (w * .92, w * .92, 2 * s), (0, 0, 0))
        m.rotation_euler = (math.radians(15), 0, 0); m.location = (0, -d * .2, w * .15)
        part(n + " handle", "box", (4 * s, d * .5, 4 * s), (0, d * .25, 2 * s))
    elif prop == "paper_pen":
        for i in range(3):
            part("%s sheet %d" % (n, i), "box", (w * .85, d * .85, .3), (i * 1.5 - 1.5, i * 1.2, .15 + i * .3))
        pen = part(n + " pen", "cyl", (2.4 * s, 2.4 * s, 26 * s), (0, 0, 0), flat("R7 proxy pen", (.05, .04, .04, 1)))
        pen.rotation_euler = (math.radians(90), 0, math.radians(35)); pen.location = (0, 0, 2.5 * s)
    for ob in parts:
        ob.parent = root
    top = cliff[2] if cliff else FLOOR
    root.location = U(x, top, z)
    root.rotation_euler = (0, 0, math.radians(-turn))
    cu = bpy.data.curves.new(key, "FONT"); cu.body = key; cu.size = 9; cu.align_x = "CENTER"
    t = bpy.data.objects.new(key + " label", cu); col.objects.link(t)
    cu.materials.append(flat("R7 label", (1, .1, .1, 1), 3))
    t.location = U(x, top + h + 14 * s + 6, z)
    return root, t


def layout(still_dir, fbx_dir=None):
    """fbx_dir: instead of the proxies, the exported spot FBX (RlDesk*.fbx) in the library materials (a check of the
    models in place; stills named check_*)."""
    bpy.ops.wm.open_mainfile(filepath=BLOCKOUT)
    sc = bpy.context.scene
    for c in bpy.data.collections:
        if c.name.startswith("RL 10") or "16 m" in c.name:
            c.hide_render = True
    for o in bpy.data.objects:
        if "ceiling" in o.name.lower() or "ceiling beam" in o.name.lower() or "Ceiling" in o.name:
            o.hide_render = True
    col = bpy.data.collections.new("R7 desk props (layout)")
    sc.collection.children.link(col)
    labels = []
    if fbx_dir:
        L = look_materials()
        cache = {}
        for key in PLACES:
            f = os.path.join(fbx_dir, "RlDesk%s.fbx" % key)
            if not os.path.exists(f):
                continue
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=f)
            for o in set(bpy.data.objects) - before:
                for c in list(o.users_collection):
                    c.objects.unlink(o)
                col.objects.link(o)
                for sl in o.material_slots:
                    n = sl.material.name if sl.material else ""
                    k2 = n[2:].lstrip(" _|").strip().lower().split(".")[0] if n.startswith("RL") else ""
                    if k2 == "glass":
                        sl.material = unity_glass()
                    elif k2 in L["SLOT"]:
                        cache.setdefault(k2, L["library_material"](L["SLOT"][k2]))
                        sl.material = cache[k2]
        prefix = "check"
    else:
        for key in PLACES:
            _, t = proxy(col, key)
            labels.append(t)
        prefix = "layout"
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if not still_dir:
        return
    os.makedirs(still_dir, exist_ok=True)
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng; break
        except TypeError:
            pass
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("r7 world"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.9, .82, .7, 1); bg.inputs["Strength"].default_value = .9
    sun = bpy.data.objects.new("r7 sun", bpy.data.lights.new("r7 sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.0; sun.data.color = (1, .9, .75)
    sun.rotation_euler = (math.radians(40), math.radians(-25), math.radians(-60))
    cam = bpy.data.objects.new("r7 cam", bpy.data.cameras.new("r7 cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.clip_end = 2000

    def shot(name, eye, target, lens=None, ortho=None, res=(1600, 1000), label_size=9):
        if ortho:
            cam.data.type = "ORTHO"; cam.data.ortho_scale = ortho
        else:
            cam.data.type = "PERSP"; cam.data.lens = lens or 20
        cam.location = eye
        cam.rotation_euler = (target - eye).to_track_quat("-Z", "Y").to_euler()
        for t in labels:   # labels face the camera
            t.data.size = label_size
            t.rotation_euler = (eye - t.location).to_track_quat("Z", "Y").to_euler()
        sc.render.resolution_x, sc.render.resolution_y = res
        sc.render.filepath = os.path.join(still_dir, "%s_%s.png" % (prefix, name))
        bpy.ops.render.render(write_still=True)
        print("[r7] wrote", sc.render.filepath)

    # plan: Unity z up the picture
    for t in labels:
        t.location.z = 80
    shot("plan", U(0, 400, 10), U(0, 0, 10.01), ortho=340, res=(1300, 1400), label_size=12)
    for key, t in zip(PLACES, labels):
        prop, x, z, _, s, cliff = PLACES[key]
        t.location = U(x, (cliff[2] if cliff else FLOOR) + SIZE[prop][2] * s + 14 * s + 6, z)
    for o in bpy.data.objects:   # the chess room's front wall would hide the overview
        if o.name.startswith(("HQ front facade", "HQ doorway header", "Square perimeter entrance")):
            o.hide_render = True
    shot("chess_overview", U(0, 95, -75), U(0, -10, 55), lens=22, label_size=7)
    for o in bpy.data.objects:
        if o.name.startswith(("HQ front facade", "HQ doorway header", "Square perimeter entrance")):
            o.hide_render = False
    shot("chess_from_door", U(0, 6, -6), U(0, -2, 60), lens=14, label_size=5)
    shot("chess_board_right", U(-10, 6, 40), U(80, -8, 60), lens=16, label_size=5)
    shot("chess_board_left", U(10, 6, 40), U(-80, -8, 60), lens=16, label_size=5)
    shot("exit_walkway", U(0, 6, 104), U(0, -12, 150), lens=14, label_size=5)
    shot("front_from_start", U(0, 6, -131), U(0, -6, -70), lens=14, label_size=4)
    shot("front_overview", U(0, 27, -138), U(0, -14, -60), lens=14, label_size=4)


# ------------------------------------------------------------------ modelling helpers
# Every prop is built at its own origin (foot centre on z = 0, its front towards -y), in metres at scale 1; the spots
# (PLACES) place, turn and scale it at export.

def _k():
    import rl_kit
    return rl_kit


def mesh_obj(col, name, bm, slot, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    me.materials.append(_k().slot(slot))
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def lathe(col, name, profile, slot, segs=32, smooth=True, at=(0, 0, 0), phase=0.0, cap=True, mod=None):
    """Turned piece about +z: profile [(radius, height)] bottom to top; radius 0 closes an end. segs 8 = an octagon
    (flat facets with smooth False). cap closes an open first / last ring with an n-gon."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, h))])
        else:
            ring = []
            for i in range(segs):
                th = phase + 2 * math.pi * i / segs
                rr = r * mod(th, h) if mod else r   # mod(angle, height): a radius factor (flutes, scallops)
                ring.append(bm.verts.new((rr * math.cos(th), rr * math.sin(th), h)))
            rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            for i in range(segs):
                bm.faces.new((a[0], b[(i + 1) % segs], b[i]))
        elif len(b) == 1:
            for i in range(segs):
                bm.faces.new((a[i], a[(i + 1) % segs], b[0]))
        else:
            for i in range(segs):
                bm.faces.new((a[i], a[(i + 1) % segs], b[(i + 1) % segs], b[i]))
    if cap:
        if len(rings[0]) > 1:
            bm.faces.new(list(reversed(rings[0])))
        if len(rings[-1]) > 1:
            bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
    return mesh_obj(col, name, bm, slot, smooth)


def tube(col, name, pts, radii, slot, segs=12, cap=True):
    """A tube along the points pts (Vectors) with a radius per point (0 closes an end to a point)."""
    bm = bmesh.new()
    rings = []
    n = len(pts)
    a = None
    for i, (p, r) in enumerate(zip(pts, radii)):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        # parallel transport: carry the previous ring's frame along, so the rings do not twist or flip
        a = t.orthogonal().normalized() if a is None else (a - t * a.dot(t)).normalized()
        b = t.cross(a)
        if r <= 1e-6:
            rings.append([bm.verts.new(p)])
        else:
            rings.append([bm.verts.new(p + r * (math.cos(2 * math.pi * j / segs) * a + math.sin(2 * math.pi * j / segs) * b))
                          for j in range(segs)])
    for ra, rb in zip(rings, rings[1:]):
        if len(ra) == 1:
            for j in range(segs):
                bm.faces.new((ra[0], rb[j], rb[(j + 1) % segs]))
        elif len(rb) == 1:
            for j in range(segs):
                bm.faces.new((ra[j], ra[(j + 1) % segs], rb[0]))
        else:
            for j in range(segs):
                bm.faces.new((ra[j], ra[(j + 1) % segs], rb[(j + 1) % segs], rb[j]))
    if cap:
        for ring in (rings[0], rings[-1]):
            if len(ring) > 1:
                bm.faces.new(ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_obj(col, name, bm, slot)


def bez(p0, p1, p2, t):
    return (1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2


# ------------------------------------------------------------------ 1. ink bottle + quill (D6 row 1, first)
# A squat octagonal glass bottle (straight facets, wide sloping shoulders), a brass screw collar of two bands and a
# wide brass lip; black ink filling it to a little over half; a white quill standing in it, leaning back and to the
# right, its vane fuller on the outer side and parted by a few splits near the top (as on the sheet).

INK_W, INK_H = 16.0, 15.5   # bottle across the flats, height to the top of the brass lip (squat, D6: h ~ .95 w)


def build_ink(col):
    R = INK_W / 2 / math.cos(math.pi / 8)   # octagon corner radius for INK_W across the flats
    ph = math.pi / 8                         # a flat facing -y (the viewer)
    # glass: ONE closed outer shell (transparent: no inner faces to sort), a slightly thicker foot ring
    glass = [(0, 0), (R * .93, 0), (R, .5), (R, 7.6), (R * .96, 8.3), (R * .58, 10.4), (R * .42, 11.0),
             (R * .42, 11.6), (0, 11.6)]
    lathe(col, "ink bottle glass", glass, "glass", segs=8, smooth=False, phase=ph)
    # ink inside: the same octagon a little smaller, to 6/10 of the body, flat top
    ink = [(0, .9), (R * .88, .9), (R * .9, 1.2), (R * .9, 5.2), (0, 5.2)]
    lathe(col, "ink bottle ink", ink, "ink", segs=8, smooth=False, phase=ph)
    # brass collar (two bands with a groove) and the wide rolled lip; open mouth showing ink
    r0 = R * .42 * math.cos(math.pi / 8) + .15
    collar = [(r0, 11.2), (r0 + .55, 11.35), (r0 + .6, 12.1), (r0 + .35, 12.3), (r0 + .35, 12.6), (r0 + .6, 12.8),
              (r0 + .62, 13.7), (r0 + 1.25, 13.9), (r0 + 1.45, 14.6), (r0 + 1.4, 15.2), (r0 + 1.0, 15.5),
              (r0 - .2, 15.5), (r0 - .3, 14.9), (0, 14.9)]
    lathe(col, "ink bottle collar", collar, "brass", segs=32)
    lathe(col, "ink bottle mouth ink", [(0, 14.8), (r0 - .25, 14.8), (r0 - .25, 14.95), (0, 14.95)], "ink", segs=24)
    # quill: shaft from inside the bottle (tip on the ink) up and back, the vane on the upper two thirds
    base = Vector((.6, .8, 3.0))      # D6: a short broad feather, about 2x the bottle above the lip, bending right
    top = Vector((9.0, 6.0, 40.0))
    mid = Vector((1.5, 2.5, 26.0))
    pts = [bez(base, mid, top, i / 24) for i in range(25)]
    radii = [.38 + .12 * (1 - i / 24) if i < 23 else (.2 if i == 23 else 0) for i in range(25)]
    tube(col, "quill shaft", pts, radii, "cloth", segs=10)
    build_vane(col, pts)


def build_vane(col, pts):
    """The feather's two vanes along the shaft points: width rises from nothing at 0.33 of the length to its widest
    at 0.7 and closes round at the tip; the outer vane (towards +x) is 1.6x the inner one; a slight cup and twist;
    four V splits cut into the outer vane and two into the inner one."""
    n = len(pts)
    bm = bmesh.new()
    across = 5
    splits_out = {15: .35, 19: .3}   # shaft index -> depth of the split (fraction of the width): two soft notches
    splits_in = {18: .25}
    for side, scale, splits in ((1, 1.6, splits_out), (-1, 1.0, splits_in)):
        grid = []
        for i in range(n):
            s = i / (n - 1)
            p = pts[i]
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
            # sideways direction: across the shaft, mostly along +x (the feather faces the viewer at -y)
            side_dir = t.cross(Vector((0, 1, 0))).normalized() * side
            up = side_dir.cross(t).normalized()
            if s < .4:
                wdt = 0.0
            else:
                u = (s - .4) / .6
                wdt = math.sin(min(1.0, u / .55) * math.pi / 2) * (1 - max(0.0, (u - .62) / .38) ** 2.2)
            wdt *= 4.2 * scale   # outer vane ~6.7 m, inner ~4.2 m: about a third of the feather's length across
            row = []
            for j in range(across + 1):
                f = j / across
                cut = splits.get(i, 0.0)
                ww = wdt * (1 - cut * f if cut else 1)
                # vane edge swept back towards the tip (barbs lean up the feather)
                q = p + side_dir * ww * f + t * (-.9 * ww * f * f * -1) + up * (-.35 * ww * f * f)
                row.append(bm.verts.new(q))
            grid.append(row)
        for i in range(n - 1):
            for j in range(across):
                a, b, c, d = grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]
                if (a.co - b.co).length < 1e-4 and (d.co - c.co).length < 1e-4:
                    continue
                try:
                    bm.faces.new((a, b, c, d) if side > 0 else (d, c, b, a))
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    ob = mesh_obj(col, "quill vane", bm, "cloth")
    sol = ob.modifiers.new("thickness", "SOLIDIFY")   # a little body so it is not a paper sheet from the side
    sol.thickness = .18; sol.offset = 0
    return ob


# ------------------------------------------------------------------ 2. pocket watch + chain (D6 row 1, second)
# An open gold hunter: a domed case standing on its edge, leaning back 20 degrees, the cream dial with twelve hour
# marks and two black hands (ten past ten); the domed lid hinged at ten o'clock and swung open behind; at one o'clock
# the pendant, a knurled crown and the bow ring, from which the chain falls down the right side and curls on the desk.

WATCH_R = 11.0   # case radius (22 across, PLACES / SIZE)


def torus_link(bm, centre, t, n, a=.95, b=.55, r=.22, seg=8, tseg=4):
    """One oval chain link into bm: long axis along t, its ring in the plane of t and n."""
    t, n = t.normalized(), n.normalized()
    w = t.cross(n).normalized()
    rings = []
    for i in range(seg):
        ang = 2 * math.pi * i / seg
        c = centre + t * a * math.cos(ang) + n * b * math.sin(ang)
        tang = (-t * a * math.sin(ang) + n * b * math.cos(ang)).normalized()
        out = tang.cross(w).normalized()
        rings.append([bm.verts.new(c + r * (math.cos(2 * math.pi * j / tseg) * out + math.sin(2 * math.pi * j / tseg) * w))
                      for j in range(tseg)])
    for i in range(seg):
        ra, rb = rings[i], rings[(i + 1) % seg]
        for j in range(tseg):
            bm.faces.new((ra[j], ra[(j + 1) % tseg], rb[(j + 1) % tseg], rb[j]))


def catmull(pts, step):
    """Points every `step` metres along a Catmull-Rom curve through pts."""
    dense = []
    P = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(20):
            s = k / 20
            dense.append(.5 * ((2 * p1) + (-p0 + p2) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s
                               + (-p0 + 3 * p1 - 3 * p2 + p3) * s ** 3))
    dense.append(pts[-1])
    out, acc = [dense[0]], 0.0
    for a, b in zip(dense, dense[1:]):
        acc += (b - a).length
        if acc >= step:
            out.append(b); acc = 0.0
    return out


def build_watch(col):
    R = WATCH_R
    parts = []
    # --- built lying flat, dial up (+z), twelve o'clock towards +y; stood up at the end
    case = [(0, -2.4), (5, -2.25), (8.6, -1.6), (10.5, -.75), (R, 0), (R * .99, .55), (R * .96, .95), (R * .88, 1.05),
            (R * .86, .75), (R * .85, .6), (0, .6)]
    parts.append(lathe(col, "watch case", case, "brass", segs=48))
    parts.append(lathe(col, "watch dial", [(0, .55), (R * .85, .55), (R * .85, .72), (0, .72)], "ivory", segs=48))
    ring = [(R * .78, .72), (R * .79, .76), (R * .8, .76), (R * .81, .72)]   # a fine chapter ring
    parts.append(lathe(col, "watch chapter ring", ring + [(R * .78, .72)], "ink", segs=48, cap=False))
    bm = bmesh.new()
    for h in range(12):   # hour marks: longer at 12, 3, 6, 9
        ang = math.pi / 2 - 2 * math.pi * h / 12
        long_ = h % 3 == 0
        L, W = (1.5, .42) if long_ else (1.0, .26)
        rc = R * .68
        d = Vector((math.cos(ang), math.sin(ang), 0)); s = Vector((-d.y, d.x, 0))
        c = d * rc + Vector((0, 0, .76))
        vs = [bm.verts.new(c + d * L / 2 * a + s * W / 2 * b + Vector((0, 0, z))) for z in (0, .06)
              for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        for f in ((0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            bm.faces.new([vs[i] for i in f])
    parts.append(mesh_obj(col, "watch hour marks", bm, "ink", smooth=False))

    def hand(name, clock, length, width, z):
        ang = math.pi / 2 - 2 * math.pi * clock / 12
        d = Vector((math.cos(ang), math.sin(ang), 0)); s = Vector((-d.y, d.x, 0))
        # a slim spade: tail, waist, a diamond near the tip, point
        outline = [(-1.2, .35), (0, .5), (length * .62, .28), (length * .78, width), (length * .9, .22), (length, 0),
                   (length * .9, -.22), (length * .78, -width), (length * .62, -.28), (0, -.5), (-1.2, -.35)]
        bm = bmesh.new()
        top = [bm.verts.new(d * u + s * v + Vector((0, 0, z + .08))) for u, v in outline]
        bot = [bm.verts.new(d * u + s * v + Vector((0, 0, z))) for u, v in outline]
        bm.faces.new(top); bm.faces.new(list(reversed(bot)))
        n = len(outline)
        for i in range(n):
            bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return mesh_obj(col, name, bm, "ink", smooth=False)
    parts.append(hand("watch hour hand", 10 + 10 / 60, R * .5, .75, .8))
    parts.append(hand("watch minute hand", 2.0, R * .72, .6, .9))
    parts.append(lathe(col, "watch centre", [(0, .8), (.55, .8), (.5, 1.05), (0, 1.12)], "brass", segs=16))
    # pendant, crown, bow at one o'clock
    pa = math.pi / 2 - 2 * math.pi / 12
    pd = Vector((math.cos(pa), math.sin(pa), 0))
    pend = lathe(col, "watch pendant", [(0, 0), (1.0, 0), (.85, .9), (.8, 1.6), (0, 1.6)], "brass", segs=16)
    crown_prof = [(0, 1.5)]
    for i in range(7):   # knurled crown: ridges as alternate radii
        crown_prof += [(1.45, 1.6 + i * .28), (1.6, 1.74 + i * .28)]
    crown_prof += [(1.3, 3.7), (.7, 3.95), (0, 4.0)]
    crown = lathe(col, "watch crown", crown_prof, "brass", segs=20)
    for o in (pend, crown):
        o.matrix_world = Matrix.Translation(pd * (R - .3) + Vector((0, 0, -.2))) @ \
            Vector((0, 0, 1)).rotation_difference(pd).to_matrix().to_4x4()
    parts += [pend, crown]
    bow_c = pd * (R - .3 + 4.0 + 2.3) + Vector((0, 0, -.2))
    bm = bmesh.new()
    torus_link(bm, bow_c, Vector((-pd.y, pd.x, 0)), pd, a=2.5, b=2.5, r=.42, seg=24, tseg=8)
    parts.append(mesh_obj(col, "watch bow", bm, "brass"))
    # lid: dome over the dial when shut, hinged at ten o'clock, swung open 125 degrees
    lid_prof = [(0, 2.6), (R * .93, 1.1), (R, .95), (R * .985, 1.7), (R * .87, 2.55), (5.5, 3.2), (0, 3.35)]
    lid = lathe(col, "watch lid", lid_prof, "brass", segs=48)
    lid_in = lathe(col, "watch lid inside", [(0, 2.55), (R * .9, 1.12), (R * .9, 1.0), (0, 2.42)], "brass", segs=40)
    ha = math.pi / 2 + 2 * math.pi * 2 / 12
    hd = Vector((math.cos(ha), math.sin(ha), 0))
    hinge_p = hd * (R + .2) + Vector((0, 0, .95))
    axis = Vector((-hd.y, hd.x, 0))
    M = Matrix.Translation(hinge_p) @ Matrix.Rotation(math.radians(-125), 4, axis) @ Matrix.Translation(-hinge_p)
    for o in (lid, lid_in):
        o.matrix_world = M
    parts += [lid, lid_in]
    knuckle = tube(col, "watch hinge", [hinge_p - axis * 2.2, hinge_p + axis * 2.2], [.6, .6], "brass", segs=12)
    parts.append(knuckle)
    # --- stand it up: lean back 20 degrees (dial towards -y and a little up), lowest point on z = 0
    stand = Matrix.Rotation(math.radians(70), 4, "X")
    for o in parts:
        o.matrix_world = stand @ o.matrix_world
    bpy.context.view_layer.update()
    low = min((o.matrix_world @ v.co).z for o in parts for v in o.data.vertices)
    lift = Matrix.Translation((0, 0, -low))
    for o in parts:
        o.matrix_world = lift @ o.matrix_world
    bpy.context.view_layer.update()
    # chain from the bow down the right side, curling on the desk in front-right
    bw = lift @ stand @ bow_c
    ring_side = (lift @ stand).to_3x3() @ Vector((pd.y, -pd.x, 0)).normalized()   # the bow ring's right-hand side
    start = bw + ring_side * 2.5
    hang = bw + Vector((2.0, -1.5, -2.6))
    pts = [start, hang, hang + Vector((3.5, -1.5, -5)), Vector((hang.x + 5, -6, 4)), Vector((hang.x + 6.5, -10, .5)),
           Vector((hang.x + 3, -15, .45)), Vector((hang.x - 3, -14.5, .45)), Vector((hang.x - 4, -10, .45)),
           Vector((hang.x - .5, -8.5, .45)), Vector((hang.x + 2, -11, .45))]
    path = catmull(pts, 1.4)
    bm = bmesh.new()
    for i in range(1, len(path)):
        a, b = path[i - 1], path[i]
        t = b - a
        up = Vector((0, 0, 1)) if abs(t.normalized().z) < .9 else Vector((1, 0, 0))
        n = t.cross(up).normalized()
        if i % 2:
            n = t.cross(n).normalized()   # every other link turned a quarter about the chain
        torus_link(bm, (a + b) / 2, t, n)
    mesh_obj(col, "watch chain", bm, "brass")
    # the first link hooked through the bow ring
    for o in parts:   # apply the object transforms (rl_kit.export keeps world matrices; the still wants clean data)
        o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)


# ------------------------------------------------------------------ 3. magnifying glass (D6 row 1, third)
# A thick round brass rim (rounded section) round a lens a little domed on both faces, a brass ferrule with two collars,
# a turned, glossy walnut handle swelling in the middle, a brass end cap. It lies flat on the desk, on the rim and the
# handle's belly.

MAG_R = 11.0   # lens radius (rim 24.4 across)


def build_magnifier(col):
    R = MAG_R
    parts = []
    parts.append(lathe(col, "magnifier lens", [(0, -.7), (R * .6, -.45), (R + .3, -.08), (R + .3, .08), (R * .6, .45),
                                               (0, .7)], "glass", segs=48))
    rim = [(R - .25, -1.0), (R + .4, -1.45), (R + 1.3, -1.45), (R + 1.85, -.9), (R + 1.95, 0), (R + 1.85, .9),
           (R + 1.3, 1.45), (R + .4, 1.45), (R - .25, 1.0), (R - .4, 0), (R - .25, -1.0)]
    parts.append(lathe(col, "magnifier rim", rim, "brass", segs=64, cap=False))
    ferrule = [(0, -.6), (1.05, -.6), (1.1, .4), (1.45, .7), (1.45, 1.6), (1.15, 1.9), (1.2, 2.6), (1.7, 2.9),
               (1.75, 3.6), (1.35, 3.9), (0, 3.9)]
    wood = [(0, 3.8), (1.45, 3.8), (1.75, 4.8), (2.1, 7.5), (2.35, 11.5), (2.35, 15), (2.1, 18.5), (1.75, 20.8),
            (1.85, 21.6), (0, 21.6)]
    cap = [(0, 21.5), (1.95, 21.5), (2.05, 22.0), (1.85, 22.9), (1.2, 23.5), (.5, 23.8), (0, 23.85)]
    to_y = Matrix.Translation((0, R + 1.5, 0)) @ Matrix.Rotation(math.radians(-90), 4, "X")   # +z of the lathe -> +y
    for nm, prof, slot, segs in (("magnifier ferrule", ferrule, "brass", 24), ("magnifier handle", wood, "varnished", 24),
                                 ("magnifier end cap", cap, "brass", 24)):
        o = lathe(col, nm, prof, slot, segs=segs)
        o.matrix_world = to_y
        parts.append(o)
    # lie down: flat on the rim's far side and the handle's belly (which is thicker than the rim), lowest point z = 0
    tilt = Matrix.Rotation(math.radians(2.2), 4, "X")
    for o in parts:
        o.matrix_world = tilt @ o.matrix_world
    bpy.context.view_layer.update()
    low = min((o.matrix_world @ v.co).z for o in parts for v in o.data.vertices)
    for o in parts:
        o.matrix_world = Matrix.Translation((0, 0, -low)) @ o.matrix_world
        o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)


# ------------------------------------------------------------------ 4. brass desk lamp, switched off (D6 row 1, fourth)
# A stepped round brass base, a short post on a ball, the lower arm leaning back to a ball elbow with a side knob, the
# upper arm reaching forward and up to the shade's knuckle, a bell shade tipped 50 degrees to look down in front, an
# unlit frosted bulb inside (its light, if any, is Unity's: no glow in the model). Shade towards -y (the spot turns it).

def ball_knob(col, name, c, r):
    lathe(col, name + " ball", [(0, -r)] + [(r * math.sin(math.pi * i / 8), -r * math.cos(math.pi * i / 8))
                                           for i in range(1, 8)] + [(0, r)], "brass", segs=16, at=c)
    k = lathe(col, name + " knob", [(0, -r * 1.25), (r * .85, -r * 1.25), (r * .9, -r * .95), (r * .55, -r * .7),
                                   (r * .55, r * .7), (r * .9, r * .95), (r * .85, r * 1.25), (0, r * 1.25)], "brass", segs=16)
    k.matrix_world = Matrix.Translation(c) @ Matrix.Rotation(math.pi / 2, 4, "Y")   # the knob sticks out along x


def build_lamp(col):
    base = [(0, 0), (8.8, 0), (9.1, .35), (9.0, 1.1), (8.4, 1.4), (7.3, 1.6), (7.1, 2.1), (5.6, 2.5), (5.3, 3.1),
            (3.0, 3.4), (2.0, 3.7), (1.5, 4.4), (1.25, 5.6), (0, 5.6)]
    lathe(col, "lamp base", base, "brass", segs=40)
    S = Vector((0, .3, 6.6)); E = Vector((0, 7.5, 27.0)); H = Vector((0, -8.5, 38.0))
    ball_knob(col, "lamp shoulder", S, 1.5)
    ball_knob(col, "lamp elbow", E, 1.6)
    for nm, a, b in (("lamp lower arm", S, E), ("lamp upper arm", E, H)):
        d = (b - a).normalized()
        pts = [a + d * 1.2 + (b - a - d * 2.4) * (i / 6) for i in range(7)]
        tube(col, nm, pts, [.75] * 7, "brass", segs=14)
        for t in (.12, .88):   # a fine collar near each end
            c = a.lerp(b, t)
            tube(col, nm + " collar %d" % (t > .5), [c - d * .35, c + d * .35], [1.0, 1.0], "brass", segs=14)
    ball_knob(col, "lamp shade knuckle", H, 1.35)
    # the shade: lathe about its own axis, top at the origin, mouth along -z, then tipped and hung below the knuckle
    shade = [(0, .9), (1.6, .8), (2.6, .5), (4.4, -.3), (6.2, -1.6), (7.6, -3.4), (8.5, -5.6), (8.95, -8.0),
             (9.1, -9.6), (9.5, -10.2), (9.4, -10.6), (8.8, -10.45), (8.4, -8.6), (7.9, -6.2), (6.9, -4.0),
             (5.3, -2.3), (3.2, -1.2), (1.4, -.85), (0, -.8)]   # a round dome (D6), a rolled lip
    sh = lathe(col, "lamp shade", shade, "brass", segs=40)
    sock = lathe(col, "lamp socket", [(0, -.6), (1.3, -.6), (1.3, -2.6), (1.1, -2.8), (0, -2.8)], "brass", segs=16)
    bulb = lathe(col, "lamp bulb", [(0, -2.6), (1.0, -2.7), (1.6, -3.3), (2.6, -4.6), (2.75, -5.6), (2.3, -6.6),
                                    (1.3, -7.2), (0, -7.35)], "ivory", segs=20)
    fin = lathe(col, "lamp shade finial", [(0, .8), (.9, .8), (.7, 1.5), (.95, 1.9), (0, 2.6)], "brass", segs=12)
    M = Matrix.Translation(H + Vector((0, -2.2, -1.2))) @ Matrix.Rotation(math.radians(-50), 4, "X")
    for o in (sh, sock, bulb, fin):
        o.matrix_world = M
    # a short bracket from the knuckle to the shade's crown
    crown = M @ Vector((0, 0, .8))
    tube(col, "lamp shade bracket", [H, H.lerp(crown, .5), crown], [.6, .6, .6], "brass", segs=12)
    for o in col.objects:
        o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)


# ------------------------------------------------------------------ 5. teacup on its saucer (D6 row 1, last)
# Cream porcelain (ivory): a cup swelling out to the rim with twelve soft flutes up its body, a gold band at the rim
# and a gold ring at the foot, a gold ear handle with a thumb rest, tea in it; a wide saucer with a gold rim and a
# gold ring round the cup's well. Handle towards +x.

def build_cup(col):
    # saucer
    saucer = [(0, 0), (4.7, 0), (5.0, .45), (8.8, .85), (11.4, 1.5), (12.7, 2.15), (13.05, 2.5), (12.9, 2.68),
              (12.45, 2.58), (10.6, 1.95), (7.2, 1.42), (5.7, 1.32), (0, 1.3)]
    lathe(col, "saucer", saucer, "ivory", segs=64)
    lathe(col, "saucer gold rim", [(12.62, 2.3), (13.12, 2.42), (13.12, 2.6), (12.95, 2.74), (12.5, 2.66),
                                   (12.62, 2.3)], "brass", segs=64, cap=False)
    lathe(col, "saucer gold ring", [(5.55, 1.31), (5.75, 1.36), (5.95, 1.31), (5.75, 1.29), (5.55, 1.31)], "brass",
          segs=48, cap=False)
    base = 1.3   # the cup stands in the saucer's well

    def flutes(th, h):
        k = h - base
        f = 0.0 if k < 1.6 or k > 9.4 else math.sin(math.pi * (k - 1.6) / 7.8) ** .7
        return 1 + .075 * f * (abs(math.cos(6 * th)) ** .45 - .6)   # twelve rounded lobes, grooves between
    cup = [(0, 0), (3.9, 0), (4.3, .3), (4.4, .95), (4.7, 1.2), (6.2, 2.6), (7.4, 4.6), (8.1, 6.8), (8.45, 8.8),
           (8.6, 10.2), (8.65, 10.6), (8.45, 10.8), (8.2, 10.6), (8.0, 9.2), (7.6, 7.6), (6.8, 5.6), (0, 5.6)]
    lathe(col, "cup", [(r, h + base) for r, h in cup], "ivory", segs=96, mod=flutes)
    # tea (RL_Tea, glossy amber brown): flat in the middle, a meniscus creeping up the wall that catches the light
    # in a bright ring at the rim, as on D / D6
    # (inside the inner wall's flute grooves, r 7.45 at h 8.45: the meniscus must not poke through them)
    lathe(col, "cup tea", [(0, 8.0 + base), (7.4, 8.0 + base), (7.45, 8.45 + base), (7.25, 8.36 + base),
                           (6.8, 8.31 + base), (5.2, 8.3 + base), (0, 8.3 + base)], "tea", segs=64)
    lathe(col, "cup gold rim", [(8.58, 9.95 + base), (8.72, 10.0 + base), (8.74, 10.62 + base), (8.5, 10.85 + base),
                                (8.3, 10.7 + base), (8.58, 9.95 + base)], "brass", segs=72, cap=False)
    lathe(col, "cup gold foot", [(4.35, .25 + base), (4.5, .3 + base), (4.52, 1.0 + base), (4.38, 1.05 + base),
                                 (4.35, .25 + base)], "brass", segs=48, cap=False)
    # ear handle: from under the rim, out, round and down, back into the lower body; a thumb rest on top
    P = [Vector(v) for v in ((8.0, 0, 9.4), (10.2, 0, 10.1), (12.4, 0, 9.4), (13.3, 0, 7.6), (12.6, 0, 5.6),
                             (10.6, 0, 4.4), (8.2, 0, 4.2))]
    pts = [p + Vector((0, 0, base)) for p in catmull(P, .45)]
    n = len(pts)
    radii = [.62 + .18 * math.sin(math.pi * i / (n - 1)) for i in range(n)]
    tube(col, "cup handle", pts, radii, "brass", segs=12)
    tube(col, "cup handle thumb rest", [Vector((10.0, 0, 10.6 + base)), Vector((11.0, 0, 11.3 + base)),
                                        Vector((11.9, 0, 11.15 + base))], [.5, .45, 0], "brass", segs=10)


# ------------------------------------------------------------------ 6. parchment sheets + fountain pen (D6 row 2, second)
# Three loose parchment sheets, a little offset and turned, their edges rough and the corners lifting; a black
# lacquered fountain pen (ink) lying across them: a gold nib, a gold ring at the grip, the barrel, a gold cap band,
# the cap with a gold clip and a gold end ring.

def sheet(col, name, w, d, z, turn, off, seed):
    import random
    rnd = random.Random(seed)
    nx, ny = 14, 17
    bm = bmesh.new()
    grid = []
    edge_noise = {}
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            u, v = i / nx - .5, j / ny - .5
            x, y = u * w, v * d
            on_edge = i in (0, nx) or j in (0, ny)
            if on_edge:   # rough, slightly torn edge: pull the border in by a little noise
                k = .35 + .45 * rnd.random()
                x -= math.copysign(k, u) if i in (0, nx) else 0
                y -= math.copysign(k, v) if j in (0, ny) else 0
            # gentle sag in the middle, the corners and long edges lifting
            lift = .7 * (abs(2 * u) ** 3) + .45 * (abs(2 * v) ** 4) + .5 * (abs(2 * u) * abs(2 * v)) ** 6
            row.append(bm.verts.new((x, y, lift)))
        grid.append(row)
    for j in range(ny):
        for i in range(nx):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    ob = mesh_obj(col, name, bm, "parchment")
    sol = ob.modifiers.new("thickness", "SOLIDIFY"); sol.thickness = .14; sol.offset = -1
    ob.matrix_world = Matrix.Translation((off[0], off[1], z)) @ Matrix.Rotation(math.radians(turn), 4, "Z")
    return ob


def build_paper_pen(col):
    W, D = 22.0, 28.0
    sheet(col, "paper sheet 0", W, D, .14, -6, (0, 0), 1)
    sheet(col, "paper sheet 1", W, D, .32, 5, (1.6, 1.2), 2)
    sheet(col, "paper sheet 2", W, D, .5, -1, (-.6, 2.4), 3)
    # pen along +x from the nib point, then laid across the sheets at 35 degrees
    parts = []
    nib = lathe(col, "pen nib", [(0, 0), (.25, .4), (.75, 2.2), (.95, 3.6), (0, 3.6)], "brass", segs=16)
    nib.data.transform(Matrix.Diagonal((1, .45, 1, 1)))   # a flat nib
    parts.append(nib)
    body = [(.0, 3.3), (.85, 3.3), (.95, 4.0), (1.0, 5.9), (0, 5.9)]
    parts.append(lathe(col, "pen grip", body, "ink", segs=24))
    parts.append(lathe(col, "pen grip ring", [(0, 5.8), (1.12, 5.8), (1.18, 6.0), (1.18, 6.35), (1.1, 6.5), (0, 6.5)],
                       "brass", segs=24))
    parts.append(lathe(col, "pen barrel", [(0, 6.4), (1.12, 6.4), (1.2, 8), (1.22, 14.6), (0, 14.6)], "ink", segs=24))
    parts.append(lathe(col, "pen cap band", [(0, 14.5), (1.36, 14.5), (1.4, 14.7), (1.4, 15.7), (1.34, 15.9),
                                             (0, 15.9)], "brass", segs=24))
    parts.append(lathe(col, "pen cap", [(0, 15.8), (1.3, 15.8), (1.32, 22), (1.2, 24.0), (0, 24.0)], "ink", segs=24))
    parts.append(lathe(col, "pen end", [(0, 23.9), (1.22, 23.9), (1.25, 24.4), (1.0, 25.4), (.5, 25.8), (0, 25.85)],
                       "brass", segs=24))
    clip = tube(col, "pen clip", [Vector((1.45, 0, 23.6)), Vector((1.55, 0, 21)), Vector((1.6, 0, 18.5)),
                                  Vector((1.55, 0, 16.6))], [.35, .32, .3, .42], "brass", segs=8)
    parts.append(clip)
    ball = lathe(col, "pen clip ball", [(0, -.5)] + [(.5 * math.sin(math.pi * i / 6), -.5 * math.cos(math.pi * i / 6))
                                             for i in range(1, 6)] + [(0, .5)], "brass", segs=10, at=(1.6, 0, 16.5))
    parts.append(ball)
    PEN = 1.25   # the pen ~3/4 of a sheet's diagonal (D6)
    d = Vector((math.cos(math.radians(35)), math.sin(math.radians(35)), 0))
    start = Vector((-.4, 1.4, 0)) - d * 25.85 * PEN / 2
    start.z = .5 + 1.22 * PEN   # resting on the top sheet's face (z .5 in its flat middle)
    M = Matrix.Translation(start) @ Matrix.Rotation(math.radians(35), 4, "Z") @         Matrix.Rotation(math.radians(90), 4, "Y") @ Matrix.Rotation(math.radians(180), 4, "Z") @         Matrix.Scale(PEN, 4)
    for o in parts:   # lathe +z -> world +x (nib at the start), clip on top
        o.matrix_world = M @ o.matrix_world
    for o in col.objects:
        if o.modifiers:
            continue
        o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)


# ------------------------------------------------------------------ 7. the book cliffs under the props (D6 row 2, first)
# Giant leather books lying flat, a little askew: two covers overhanging a cream page block (pages showing on three
# sides), a rounded spine on one long side with four raised gold bands and a gold rule near each cover edge. Leathers
# alternate red, brown, dark and tan. A cliff's top face (the top book's cover) is flat at its height.

LEATHERS = ("leather", "leather_brown", "leather_dark", "leather_tan", "leather_brown", "leather")


def box(col, name, lo, hi, slot, bevel=0.0):
    lo, hi = Vector(lo), Vector(hi)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    bmesh.ops.scale(bm, vec=hi - lo, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(lo + hi) / 2, verts=bm.verts)
    if bevel:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(hi - lo) * .45), segments=2, profile=.5,
                        affect="EDGES", clamp_overlap=True)
    return mesh_obj(col, name, bm, slot, smooth=True)


def book(col, name, w, d, t, leather, M):
    """One book lying flat, its spine along y on the -x side, bottom at z 0; placed by M."""
    c = min(.42, t * .1)            # cover board
    ov = .45                        # cover overhang over the pages
    rs = t * .5                     # spine radius (round back)
    parts = [box(col, name + " bottom cover", (-w / 2, -d / 2, 0), (w / 2, d / 2, c), leather, .12),
             box(col, name + " top cover", (-w / 2, -d / 2, t - c), (w / 2, d / 2, t), leather, .12),
             box(col, name + " pages", (-w / 2 + rs * .5, -d / 2 + ov, c), (w / 2 - ov, d / 2 - ov, t - c), "parchment")]
    # a slight hollow in the fore edge (pages pushed in at the middle) - one extra loop is enough at this size
    sp = lathe(col, name + " spine", [(0, -d / 2), (rs, -d / 2), (rs, d / 2), (0, d / 2)], leather, segs=16)
    sp.matrix_world = Matrix.Translation((-w / 2 + rs * .45, 0, t / 2)) @ Matrix.Rotation(math.radians(-90), 4, "X") @         Matrix.Diagonal((.55, 1, 1, 1))
    parts.append(sp)
    for k in range(4):   # raised gold bands across the spine
        y = -d / 2 + d * (k + .5) / 4 * .82 + d * .09
        # inside the covers' height (not through the top cover), standing out of the spine sideways
        b = lathe(col, name + " band %d" % k, [(0, -.32), (rs * .9, -.32), (rs * .95, 0), (rs * .9, .32), (0, .32)],
                  "brass", segs=16)
        b.matrix_world = Matrix.Translation((-w / 2 + rs * .45, y, t / 2)) @ Matrix.Rotation(math.radians(-90), 4, "X") @             Matrix.Diagonal((.66, 1, 1, 1))
        parts.append(b)
    for z in (c * .5, t - c * .5):   # gold rule round the spine end of each cover
        parts.append(box(col, name + " rule", (-w / 2 + rs * .9, -d / 2 - .03, z - .1), (-w / 2 + rs * .9 + .3, d / 2 + .03,
                                                                                         z + .1), "brass"))
    for o in parts:
        o.matrix_world = M @ o.matrix_world
    return parts


def build_cliff(col, w, d, height, seed=0, prefix="cliff"):
    """Books from z 0 to `height`, roughly w x d, the top cover flat and level."""
    import random
    rnd = random.Random(seed)
    n = max(3, round(height / 4.2))
    ts = [rnd.uniform(.85, 1.15) for _ in range(n)]
    ts = [height * t / sum(ts) for t in ts]
    z = 0.0
    for i, t in enumerate(ts):
        last = i == n - 1
        bw = w * rnd.uniform(.9, 1.0); bd = d * rnd.uniform(.88, 1.0)
        turn = 0 if last else rnd.uniform(-5, 5)
        flip = rnd.random() < .5   # spine on either side
        M = Matrix.Translation((rnd.uniform(-.8, .8), rnd.uniform(-.8, .8), z)) @             Matrix.Rotation(math.radians(turn + (180 if flip else 0)), 4, "Z")
        book(col, "%s book %d" % (prefix, i), bw, bd, t, LEATHERS[(i + seed) % len(LEATHERS)], M)
        z += t
    for o in col.objects:
        if o.name.startswith(prefix):
            o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)


def build_books(col):
    build_cliff(col, 30, 26, 20, seed=1)


PROPS = {"ink": build_ink, "watch": build_watch, "magnifier": build_magnifier, "lamp": build_lamp, "cup": build_cup,
         "paper_pen": build_paper_pen, "books": build_books}


# ------------------------------------------------------------------ build / still

_LOOK = None


def look_materials():
    """rl_lookdev's library materials (its file without the trailing main() call), and RL_Glass drawn the way Unity
    does it (alpha blend .2 with the gloss kept), not as Cycles-style transmission."""
    global _LOOK
    if _LOOK is None:
        src = open(os.path.join(HERE, "rl_lookdev.py"), encoding="utf-8").read()
        src = src[:src.rstrip().rfind("main()")]
        ns = {"__file__": os.path.join(HERE, "rl_lookdev.py"), "__name__": "rl_lookdev_lib"}
        exec(compile(src, "rl_lookdev.py", "exec"), ns)
        _LOOK = ns
    return _LOOK


def unity_glass():
    m = bpy.data.materials.get("R7 still glass")
    if m:
        return m
    m = bpy.data.materials.new("R7 still glass"); m.use_nodes = True
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (1.0, .97, .90, 1); p.inputs["Roughness"].default_value = .05
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader"); mix.inputs["Fac"].default_value = .2   # Unity alpha .2
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(p.outputs[0], mix.inputs[2])
    gl = nt.nodes.new("ShaderNodeBsdfGlossy"); gl.inputs["Roughness"].default_value = .05
    lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = .2
    rim = nt.nodes.new("ShaderNodeMixShader")   # glossy at grazing angles: URP keeps specular on alpha glass
    nt.links.new(lw.outputs["Fresnel"], rim.inputs["Fac"])
    nt.links.new(mix.outputs[0], rim.inputs[1]); nt.links.new(gl.outputs[0], rim.inputs[2])
    nt.links.new(rim.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        m.blend_method = "BLEND"
    m.use_backface_culling = True
    return m


def still(col, name, out_dir, views):
    """Renders the collection alone in the library materials, warm afternoon light (rl_lookdev's look) on a plain
    floor. views: [(file suffix, direction from the centre to the camera, neutral?)]."""
    L = look_materials()
    sc = bpy.context.scene
    for c in sc.collection.children:
        c.hide_render = c != col
    col.hide_render = False; col.hide_viewport = False
    cache = {}
    saved = {}
    for o in col.all_objects:
        if o.type != "MESH":
            continue
        saved[o] = [s.material for s in o.material_slots]
        for s in o.material_slots:
            key = s.material.name[5:] if s.material and s.material.name.startswith("RL | ") else ""
            if key == "glass":
                s.material = unity_glass()
            elif key in L["SLOT"]:
                cache.setdefault(key, L["library_material"](L["SLOT"][key]))
                s.material = cache[key]
    objs = [o for o in col.all_objects if o.type == "MESH"]
    lo = Vector((min(min((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in objs) for i in range(3)))
    hi = Vector((max(max((o.matrix_world @ Vector(c))[i] for c in o.bound_box) for o in objs) for i in range(3)))
    centre, size = (lo + hi) / 2, (hi - lo).length
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng; break
        except TypeError:
            pass
    sc.render.resolution_x, sc.render.resolution_y = 1100, 1100
    tmp = bpy.data.collections.new("R7 still rig"); sc.collection.children.link(tmp)
    floor_me = bpy.data.meshes.new("still floor")
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=size * 3); bm.to_mesh(floor_me); bm.free()
    floor = bpy.data.objects.new("still floor", floor_me); tmp.objects.link(floor)
    floor.location = (centre.x, centre.y, lo.z - .02)
    fm = bpy.data.materials.new("still floor"); fm.use_nodes = True
    fp = next(n for n in fm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    fp.inputs["Base Color"].default_value = (.35, .2, .11, 1); fp.inputs["Roughness"].default_value = .6
    floor_me.materials.append(fm)
    sun = bpy.data.objects.new("still sun", bpy.data.lights.new("still sun", "SUN")); tmp.objects.link(sun)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-40)); sun.data.angle = math.radians(3)
    cam = bpy.data.objects.new("still cam", bpy.data.cameras.new("still cam")); tmp.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = size * 1.0; cam.data.clip_end = size * 20
    w = bpy.data.worlds.new("still world"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    os.makedirs(out_dir, exist_ok=True)
    for suffix, view, neutral in views:
        sc.view_settings.view_transform = "Standard" if neutral else "AgX"
        bg.inputs["Color"].default_value = (1, 1, 1, 1) if neutral else (.95, .82, .66, 1)
        bg.inputs["Strength"].default_value = .8 if neutral else .7
        sun.data.energy = 3.5 if neutral else 4.5
        sun.data.color = (1, 1, 1) if neutral else (1.0, .86, .68)
        v = Vector(view).normalized()
        cam.location = centre + v * size * 3
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out_dir, "%s_%s.png" % (name, suffix))
        bpy.ops.render.render(write_still=True)
        print("[r7] still", sc.render.filepath)
    for o, mats in saved.items():
        for s, m in zip(o.material_slots, mats):
            s.material = m
    for o in list(tmp.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.collections.remove(tmp)


STILL_VIEWS = [("front", (0, -1, .28), True), ("34", (-.8, -.75, .5), False), ("side", (1, -.15, .25), False)]
# flat props: looked at from above as well (the course looks down on the cliff tops)
FLAT_VIEWS = [("top", (.15, -.35, 1), True), ("34", (-.8, -.75, .6), False), ("low", (.6, -1, .3), False)]
VIEWS_FOR = {"magnifier": FLAT_VIEWS, "paper_pen": FLAT_VIEWS}
STILL_NO = {"ink": "01", "watch": "02", "magnifier": "03", "lamp": "04", "cup": "05", "paper_pen": "06", "books": "07"}


def build(props, still_dir):
    if os.path.exists(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    k = _k()
    for name in props:
        cname = "R7 prop " + name
        old = bpy.data.collections.get(cname)
        if old:
            for o in list(old.all_objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(old)
        col = bpy.data.collections.new(cname)
        bpy.context.scene.collection.children.link(col)
        PROPS[name](col)
        for o in col.objects:   # modifiers applied, then metre UVs
            bpy.context.view_layer.objects.active = o
            for m in list(o.modifiers):
                bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
                bpy.ops.object.modifier_apply(modifier=m.name)
            k.surface_uvs(o)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in col.objects if o.type == "MESH")
        print("[r7] built %s: %d objects, %d tris" % (name, len(col.objects), tris))
        if still_dir:
            still(col, "%s_%s" % (STILL_NO.get(name, "xx"), name), still_dir, VIEWS_FOR.get(name, STILL_VIEWS))
        col.hide_render = True
        col.hide_viewport = True
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


# ------------------------------------------------------------------ the spots: cliff + props, one FBX each
# What stands on each kind of spot's cliff: (builder, x, y on the cliff top before the spot's scale, turn, scale)
SPOT_CONTENT = {
    "ink": [("ink", 0, 0, 0, 1)],
    "watch": [("watch", 0, 3, 0, 1)],            # the chain spills to the front (-y)
    "cup": [("cup", 0, 0, 0, 1)],
    "lamp": [("lamp", 0, 4, 0, 1)],              # the shade leans out to the front
    "magnifier": [("magnifier", 0, -4, 25, .8)],
    "paper_pen": [("paper_pen", 0, 0, 0, 1)],
    "cup_magnifier": [("cup", -4, 5, 0, 1), ("magnifier", 6, -7, 40, .75)],
}


def bake(col, M):
    """Modifiers applied, every mesh moved by M into its data (identity objects), metre UVs."""
    k = _k()
    bpy.context.view_layer.update()
    for o in list(col.objects):
        bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers):
            bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
            bpy.ops.object.modifier_apply(modifier=m.name)
        o.data.transform(M @ o.matrix_world)
        o.matrix_world = Matrix.Identity(4)
        o.data.update()


def build_spot(key):
    """Collection "R7 spot <key>": the book cliff from the floor and the props on top, in the blockout frame."""
    prop, x, z, turn, s, cliff = PLACES[key]
    cname = "R7 spot " + key
    old = bpy.data.collections.get(cname)
    if old:
        for o in list(old.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(cname)
    bpy.context.scene.collection.children.link(col)
    spot = Matrix.Translation(U(x, FLOOR, z)) @ Matrix.Rotation(math.radians(-turn), 4, "Z")
    cw, cd, top = cliff
    height = top - FLOOR
    tmp = bpy.data.collections.new("R7 tmp"); bpy.context.scene.collection.children.link(tmp)
    build_cliff(tmp, cw, cd, height, seed=list(PLACES).index(key) + 1, prefix=key + " cliff")
    bake(tmp, spot)
    for o in list(tmp.objects):
        tmp.objects.unlink(o); col.objects.link(o)
    for name, dx, dy, t2, s2 in SPOT_CONTENT[prop]:
        PROPS[name](tmp)
        local = Matrix.Translation((dx * s, dy * s, height)) @ Matrix.Rotation(math.radians(-t2), 4, "Z") @ \
            Matrix.Scale(s * s2, 4)
        bake(tmp, spot @ local)
        for o in list(tmp.objects):
            o.name = "%s %s" % (key, o.name)
            tmp.objects.unlink(o); col.objects.link(o)
    bpy.data.collections.remove(tmp)
    k = _k()
    for o in col.objects:
        k.surface_uvs(o)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in col.objects)
    print("[r7] spot %s (%s): %d objects, %d tris" % (key, prop, len(col.objects), tris))
    merge_by_slot(col, key)
    tris2 = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in col.objects)
    print("[r7] spot %s merged: %d objects (%s), %d tris" % (key, len(col.objects),
                                                            ", ".join(sorted(o.name for o in col.objects)), tris2))
    if tris2 != tris:
        raise RuntimeError("merge %s changed the triangle count %d -> %d" % (key, tris, tris2))
    return col


def merge_by_slot(col, key):
    """One mesh per material slot (PERF 3.1, 11-18): every part joined, then split by material, so a spot draws
    once per slot instead of once per part. Shapes, places, UVs (made per part before this) and triangles stay;
    the transparent glass is its own lump like every other slot. Objects are named "<key> <slot>"."""
    objs = [o for o in col.objects if o.type == "MESH"]
    if len(objs) < 2:
        return
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    whole = bpy.context.view_layer.objects.active
    bpy.ops.object.select_all(action="DESELECT")
    whole.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in [o for o in col.objects if o.type == "MESH"]:
        used = {p.material_index for p in o.data.polygons}
        mats = [m for i, m in enumerate(o.data.materials) if i in used]
        remap = {i: mats.index(m) for i, m in enumerate(o.data.materials) if i in used}
        idx = [remap[p.material_index] for p in o.data.polygons]
        o.data.materials.clear()
        for m in mats:
            o.data.materials.append(m)
        o.data.polygons.foreach_set("material_index", idx)
        o.data.update()
        slot = mats[0].name.split("|")[-1].strip() if mats else "none"
        o.name = o.data.name = "%s %s" % (key, slot)


def export_spots(keys, preview_dir):
    if os.path.exists(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    for c in bpy.data.collections:   # earlier layout / prop collections stay in the blend, hidden
        if c.name.startswith(("R7 prop", "R7 desk props")):
            c.hide_viewport = True
    for key in keys:
        col = build_spot(key)
        model = "RlDesk" + key
        if preview_dir:   # outside Assets (Unity in Play): the same FBX settings as rl_kit.export
            os.makedirs(preview_dir, exist_ok=True)
            bpy.ops.object.select_all(action="DESELECT")
            for o in col.objects:
                o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(preview_dir, model + ".fbx"), use_selection=True,
                                     object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
                                     axis_forward="-Z", axis_up="Y", bake_anim=False, mesh_smooth_type="FACE",
                                     colors_type="LINEAR")
            print("[r7] preview", model)
        else:
            _k().export(col.name, model, ZONE)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    still = None
    if "--still" in argv:
        i = argv.index("--still")
        still = argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith("--") else STILLS
    if "layout" in argv:
        layout(still)
        return
    if "check" in argv:   # -- check <fbx dir> : the exported spots in the blockout, stills/r7/check_*.png
        layout(STILLS, argv[argv.index("check") + 1])
        return
    if "export" in argv:   # -- export [C1 F2 ...] [--preview <dir>]
        keys = [a for a in argv if a in PLACES] or list(PLACES)
        prev = None
        if "--preview" in argv:
            i = argv.index("--preview")
            prev = argv[i + 1] if i + 1 < len(argv) else os.path.join(STILLS, "_fbx")
        export_spots(keys, prev)
        return
    props = [a for a in argv if a in PROPS] or (list(PROPS) if "all" in argv else [])
    if not props:
        raise SystemExit("name a prop (%s), all, or layout" % ", ".join(PROPS))
    build(props, still)


main()
