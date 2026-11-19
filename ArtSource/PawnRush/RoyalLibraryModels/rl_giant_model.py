"""The giant grandmasters (R6, seoungtest R687, 2026-11-18, 승규 "난 움직이게 만들고 싶어" → "블렌더에서 해줘").

    blender -b --factory-startup --python rl_giant_model.py -- [A|B] [--export]   (--export: never while Unity is in Play)

One rigged giant built in Blender (no Tripo), to the front/side references Docs/PawnRush/ConceptArt/
R687_GiantChessLibrary/AssetRefs/GC_rig_A_front.png / _side.png (measured in pixels, F metres a pixel):
  - a smooth round head with dot eyes and soft pink cheeks; a gold stand-up collar on a wide sloping gold yoke
  - the robe: a soft bell-shaped torso (lofted rounded rings, wide at the lap), the lap, two wide trouser legs and both
    sleeves as ONE watertight mesh (voxel remesh of the union), cut level above the feet, a few cloth folds carved in
    (inner elbows, the waist where the top hangs over the lap, below the knees, between the knees); a rounded gold
    band round each leg's opening
  - sleeves bent at the elbow, sagging under the forearm, with a rolled gold cuff and a dark inside (a separate part
    riding the forearm); soft glove hands (palm, four fingers curling down, a thumb toward the body); bun feet
  - an armature (hips, spine, chest, neck, head, shoulder / upper arm / forearm / hand / fingers / thumb,
    thigh / shin / foot) with automatic skin weights, cleaned: no weight crosses to the other side, the hem rides the
    shins only, the sleeve's end the forearm only (so the rigid cuffs always fit), at most 4 bones a vertex.
Frame: seated, the chair seat's top at z = 0, facing -Y (Blender front), metres at game size: the head's top 49 m above
the seat (the chess room's ceiling is 58.5 m up and the prefab's chair seat 3 m), the feet hang 19 m below it.
Giant A charcoal, B cream. Saves rl_giant_<a|b>.blend and stills to stills/giant/: a sheet of three test poses
(rest, reach = leaning in with the right hand down on the board, raise = right arm up high and left arm out) from the
front, side and three-quarter, and a sheet next to the reference drawings at their scale.
"""
import math, os, sys
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
STILLS = os.path.join(HERE, "stills", "giant")
REFS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R687_GiantChessLibrary",
                                     "AssetRefs"))

ROBE = {"A": (.075, .058, .055, 1), "B": (.72, .54, .31, 1)}
SKIN = (.87, .62, .39, 1)          # linear of FACE_SKIN
GOLD = (.62, .38, .08, 1)
INK = (.035, .016, .010, 1)
BLUSH = (.93, .40, .34, 1)

# ------------------------------------------------------------------ the reference, measured
# GC_rig_A_front/side (1024 x 1536): seat line at row 1020, head top row 150, centre column 512 (front) / torso
# centre column 610 (side). Scale so the head's top is 49 m above the seat.
F = 49.0 / (1020 - 150)


def zr(row):        # metres above the seat
    return (1020 - row) * F


def xc(col):        # metres right of centre (front view, the figure's left side is the image's right)
    return (col - 512) * F


def ys(col):        # metres back from the torso's centre (side view, the figure faces left = -y)
    return (col - 610) * F


HEAD_C = Vector((0, ys(575), zr(300)))
HEAD_R = (455 - 145) * F / 2
SHOULDER = Vector((xc(668), ys(600), zr(585)))      # where the sleeve leaves the body (+x side)
ELBOW = Vector((xc(790), ys(520), zr(690)))
WRIST = Vector((xc(855), ys(395), zr(770)))         # the cuff's centre
HIP = Vector((xc(640), ys(640), zr(950)))
KNEE = Vector((xc(650), ys(290), zr(940)))
ANKLE = Vector((xc(650), ys(355), zr(1250)))
CUT_Z = zr(1195)                                    # the leg openings (the gold bands hang below to ~1272)
CUFF_H = (1272 - 1195) * F
FOOT = Vector((xc(640), ys(312), zr(1318)))

# torso rings: (row, half width px (front view), front col, back col (side view))
TORSO = [(492, 88, 525, 668), (512, 112, 505, 686), (532, 138, 482, 704), (565, 150, 470, 714), (650, 154, 460, 720), (740, 172, 450, 724), (795, 222, 440, 727),
         (835, 240, 452, 731), (900, 240, 472, 734), (980, 238, 482, 734), (1018, 224, 500, 726)]


def mat(name, rgba, rough=.6, metal=0.0, sheen=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = rgba
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    try:
        p.inputs["Sheen Weight"].default_value = sheen
    except KeyError:
        pass
    m.diffuse_color = rgba
    return m


def link(ob, col):
    for c in ob.users_collection:
        c.objects.unlink(ob)
    col.objects.link(ob)


def apply_all(ob):
    bpy.context.view_layer.objects.active = ob
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def smooth_shade(ob):
    for p in ob.data.polygons:
        p.use_smooth = True


def bm_object(name, bm, col, mats):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    smooth_shade(ob)
    return ob


def join(parts, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    parts[0].name = name
    return parts[0]


def remesh(ob, voxel, smooth_iter, smooth_fac=.8, ratio=None):
    rm = ob.modifiers.new("remesh", "REMESH"); rm.mode = "VOXEL"; rm.voxel_size = voxel
    sm = ob.modifiers.new("smooth", "SMOOTH"); sm.iterations = smooth_iter; sm.factor = smooth_fac
    apply_all(ob)
    if ratio:
        dec = ob.modifiers.new("dec", "DECIMATE"); dec.ratio = ratio
        apply_all(ob)


# ------------------------------------------------------------------ shapes

def rounded_box(col, x0, x1, y0, y1, z0, z1, r, flare=None):
    """A box with every edge rounded (radius r); flare = (z_top, z_bottom, extra) widens x toward the bottom."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    b = bpy.context.object
    b.scale = (x1 - x0, y1 - y0, z1 - z0)
    bpy.ops.object.transform_apply(scale=True)
    bv = b.modifiers.new("bevel", "BEVEL"); bv.width = r; bv.segments = 6; bv.limit_method = "NONE"
    apply_all(b)
    if flare:
        zt, zb, extra = flare
        cx = b.location.x
        for v in b.data.vertices:
            z = v.co.z + b.location.z
            t = min(max((zt - z) / (zt - zb), 0), 1)
            v.co.x *= 1 + extra * t * t
    link(b, col)
    return b


def loft(col, rings, name):
    """A closed mesh through rings of points (each ring the same count), capped at both ends."""
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(rings[0])
    for r0, r1 in zip(vr, vr[1:]):
        for k in range(n):
            bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    bm.faces.new(vr[0][::-1]); bm.faces.new(vr[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm_object(name, bm, col, [])


def superellipse(cx, cy, z, ax, ay, n=2.6, segs=48):
    pts = []
    for k in range(segs):
        a = 2 * math.pi * k / segs
        c, s = math.cos(a), math.sin(a)
        pts.append(Vector((cx + ax * math.copysign(abs(c) ** (2 / n), c), cy + ay * math.copysign(abs(s) ** (2 / n), s), z)))
    return pts


def capsule_mesh(col, a, b, r, segs=24):
    d = b - a
    parts = []
    bpy.ops.mesh.primitive_cylinder_add(vertices=segs, radius=r, depth=max(d.length, .01), location=(a + b) / 2)
    cyl = bpy.context.object
    cyl.rotation_euler = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_euler()
    parts.append(cyl)
    for p in (a, b):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=segs // 2, radius=r, location=p)
        parts.append(bpy.context.object)
    for o in parts:
        link(o, col)
    return parts


def bezier(a, b, c, n):
    return [a * (1 - t) ** 2 + b * 2 * t * (1 - t) + c * t * t for t in (i / (n - 1) for i in range(n))]


def arm(side):
    m = Vector((side, 1, 1))
    return SHOULDER * m, ELBOW * m, WRIST * m


SLEEVE_N = 18
YOKE_R = (668 - 356) * F / 2 * 1.08


def sleeve_frame(side):
    """The sleeve's centre line, radius and sag per ring, and each ring's (u, v) axes. The last ring sits SLEEVE_BACK
    metres before the wrist (the cuff covers the rest)."""
    sh, el, wr = arm(side)
    start = sh + Vector((-side * 3.2, 0, -1.2))
    path = bezier(start, el, wr, SLEEVE_N)
    out = []
    for i, p in enumerate(path):
        t = (path[min(i + 1, SLEEVE_N - 1)] - path[max(i - 1, 0)]).normalized()
        u = t.cross(Vector((0, 0, 1))).normalized()       # horizontal, across the arm
        v = u.cross(t).normalized()                        # up across the arm
        f = i / (SLEEVE_N - 1)
        r = 3.9 + 1.5 * f ** 1.3
        sag = 1.6 * f ** 2                                 # the cloth hangs under the forearm
        out.append((p, t, u, v, r, sag))
    return out


def sleeve_tube(col, side):
    bm = bmesh.new()
    segs = 32
    rings = []
    frame = sleeve_frame(side)
    for p, t, u, v, r, sag in frame:
        ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs
            c, s = math.cos(a), math.sin(a)
            q = p + (u * c + v * s) * r
            if s < 0:
                q += v * (s * sag)                     # deepen the lower half
            ring.append(bm.verts.new(q))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segs):
            bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    bm.faces.new(rings[-1]); bm.faces.new(rings[0][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm_object("sleeve", bm, col, [])


def body_mesh(col, robe_mat):
    parts = []
    rings = []
    for row, hw, front, back in TORSO:
        cy = (ys(front) + ys(back)) / 2
        rings.append(superellipse(0, cy, zr(row), hw * F, (back - front) * F / 2))
    parts.append(loft(col, rings, "torso"))
    for s in (-1, 1):
        # thigh/lap: a soft block, the two meet at the middle (a crease is carved later)
        x_in, x_out = .15, abs(xc(262))
        x0, x1 = (x_in, x_out) if s > 0 else (-x_out, -x_in)
        parts.append(rounded_box(col, x0, x1, ys(222), ys(722), zr(1022), zr(858), 4.3))
        # the trouser leg: straight front, a little wider toward the hem
        x_in, x_out = .5, abs(xc(250))
        x0, x1 = (x_in, x_out) if s > 0 else (-x_out, -x_in)
        parts.append(rounded_box(col, x0, x1, ys(214), ys(492), zr(1240), zr(880), 3.9, (zr(950), zr(1240), .24)))
    for s in (1, -1):
        parts.append(sleeve_tube(col, s))
    body = join(parts, "GiantRobe")
    remesh(body, .32, 26, .8)
    folds(body)
    bm = bmesh.new(); bm.from_mesh(body.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, CUT_Z),
                           plane_no=(0, 0, 1), clear_inner=True)
    bm.to_mesh(body.data); bm.free()
    dec = body.modifiers.new("dec", "DECIMATE"); dec.ratio = .075
    apply_all(body)
    body.data.materials.append(robe_mat)
    smooth_shade(body)
    return body


def folds(ob):
    """Carve soft cloth folds: each a groove (depth d, half-width w) along a plane, faded out away from a centre."""
    me = ob.data
    me.update()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    no = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("normal", no); no = no.reshape(-1, 3)
    push = np.zeros(len(co))

    def groove(centre, normal, depth, w, reach, mask=None):
        c = np.array(centre); n = np.array(Vector(normal).normalized())
        d = (co - c) @ n
        r = np.linalg.norm((co - c) - np.outer(d, n), axis=1)
        g = depth * np.exp(-(d / w) ** 2) * np.clip(1 - (r / reach) ** 2, 0, 1)
        if mask is not None:
            g *= mask
        np.subtract(push, g, out=push)

    def bulge(centre, normal, height, w, reach, mask=None):
        groove(centre, normal, -height, w, reach, mask)

    front = co[:, 1] < ys(560)
    # the waist: the top hangs over the lap (a roll above, a groove under it), front and sides
    waist = (co[:, 1] < ys(700)).astype(float)
    groove((0, ys(440), zr(805)), (0, .25, 1), .55, .55, 16, waist)
    bulge((0, ys(440), zr(792)), (0, .25, 1), .35, .8, 15, waist)
    # between the knees, down the lap's front and top
    groove((0, ys(300), zr(870)), (1, 0, 0), .9, .7, 14, (co[:, 2] < zr(835)).astype(float))
    for s in (-1, 1):
        k = KNEE * Vector((s, 1, 1))
        # two slanting folds on the shin below the knee, one on the thigh top behind the knee
        groove(k + Vector((-s * 1.0, -1.5, -6.5)), (s * .25, 0, 1), .3, .8, 5.0, front.astype(float))
        groove(k + Vector((s * 1.5, -1.5, -10.5)), (-s * .2, 0, 1), .22, .8, 4.0, front.astype(float))
        groove(k + Vector((-s * 1.2, 7.0, 4.2)), (s * .2, 1, .15), .35, .55, 4.0)
        # the inner elbow: three short folds across the bend
        sh, el, wr = arm(s)
        up_dir = (el - sh).normalized(); fo_dir = (wr - el).normalized()
        axis = (up_dir + fo_dir).normalized()
        inner = (fo_dir - up_dir).normalized()           # points into the bend
        to_el = co - np.array(el)
        side_mask = np.clip((to_el @ np.array(inner)) / 2.0, 0, 1)
        for off in (-1.6, 0, 1.6):
            groove(el + axis * off, axis + inner * .2, .45, .45, 5.5, side_mask)
        # the shoulder: one soft fold where the sleeve leaves the body, on top
        groove(SHOULDER * Vector((s, 1, 1)) + Vector((-s * 1.6, 0, 2.0)), (s, 0, .25), .3, .7, 4.5,
               (co[:, 2] > zr(600)).astype(float))
    co += no * push[:, None]
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def boundary_loops(ob):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    adj = {}
    for e in bm.edges:
        if e.is_boundary:
            a, b = e.verts
            adj.setdefault(a.index, []).append(b.index); adj.setdefault(b.index, []).append(a.index)
    co = {v.index: v.co.copy() for v in bm.verts}
    bm.free()
    loops, used = [], set()
    for v0 in adj:
        if v0 in used:
            continue
        loop, prev, cur = [v0], None, v0
        used.add(v0)
        while True:
            nxt = [n for n in adj[cur] if n != prev and n not in used]
            if not nxt:
                break
            prev, cur = cur, nxt[0]; used.add(cur); loop.append(cur)
        if len(loop) > 8:
            loops.append([co[i] for i in loop])
    return loops


def leg_cuffs(col, body, gold_mat):
    """A rounded gold band round each leg's opening: a closed profile swept round the cut, from the cut down CUFF_H."""
    out = bmesh.new()
    prof = [(-.6, .7), (.5, .45), (.95, -.2), (1.0, -CUFF_H + .7), (.6, -CUFF_H), (-.5, -CUFF_H + .15), (-.9, -CUFF_H + .8),
            (-.9, .2)]   # (outward, down) round the band's section, outside first then the inside wall
    for pts in boundary_loops(body):
        c = sum(pts, Vector()) / len(pts)
        step = max(1, len(pts) // 32)
        rings = []
        for p in pts[::step]:
            o = p - c; o.z = 0; o = o.normalized()
            rings.append([out.verts.new(Vector((p.x, p.y, CUT_Z)) + o * a + Vector((0, 0, b))) for a, b in prof])
        n, k = len(rings), len(prof)
        for i in range(n):
            r0, r1 = rings[i], rings[(i + 1) % n]
            for j in range(k):
                out.faces.new((r0[j], r1[j], r1[(j + 1) % k], r0[(j + 1) % k]))
    bmesh.ops.recalc_face_normals(out, faces=out.faces)
    ob = bm_object("GiantLegCuffs", out, col, [gold_mat])
    sub = ob.modifiers.new("sub", "SUBSURF"); sub.levels = 1
    apply_all(ob)
    return ob


def sleeve_cuff(col, side, gold_mat, inner_mat):
    """The rolled gold cuff at the sleeve's end and the dark inside, riding the forearm. Axial 0 = the wrist; the robe's
    sleeve tube ends there (rounded by the remesh), the cuff starts buried inside it."""
    p, t, u, v, r, sag = sleeve_frame(side)[-1]
    segs = 28
    prof = [(.86, -3.2, 1), (1.04, -1.6, 1), (1.13, -.6, 1), (1.15, .6, 1), (1.08, 1.5, 1), (.97, 1.8, 1),
            (.88, 1.35, 2), (.85, .4, 2)]   # (radius scale, axial, material)
    bm = bmesh.new()
    rings = []
    for sc, ax, _ in prof:
        ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs
            c, s = math.cos(a), math.sin(a)
            rr = r * sc
            q = p + t * ax + (u * c + v * s) * rr
            if s < 0:
                q += v * (s * sag * sc)
            ring.append(bm.verts.new(q))
        rings.append(ring)
    for j in range(len(rings) - 1):
        for k in range(segs):
            f = bm.faces.new((rings[j][k], rings[j][(k + 1) % segs], rings[j + 1][(k + 1) % segs], rings[j + 1][k]))
            f.material_index = 0 if prof[j + 1][2] == 1 else 1
    bm.faces.new(rings[-1]).material_index = 1
    bm.faces.new(rings[0][::-1]).material_index = 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = bm_object("GiantCuff_" + ("L" if side > 0 else "R"), bm, col, [gold_mat, inner_mat])
    sub = ob.modifiers.new("sub", "SUBSURF"); sub.levels = 1
    apply_all(ob)
    return ob


def hand_frame(side):
    sh, el, wr = arm(side)
    fwd = ((wr - el).normalized() + Vector((0, 0, -.2))).normalized()
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd).normalized()
    inward = right * side                       # toward the body's middle
    return wr, fwd, right, up, inward


def hand(col, side, skin_mat):
    """A soft glove hand, palm down: a palm, four fingers curling down over the front, a thumb on the body's side."""
    wr, fwd, right, up, inward = hand_frame(side)
    basis = Matrix((right, fwd, up)).transposed()
    parts = []
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1)
    palm = bpy.context.object
    palm.matrix_world = Matrix.Translation(wr + fwd * 2.7 - up * .1) @ basis.to_4x4() @ Matrix.Diagonal((4.0, 3.3, 1.55, 1))
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    link(palm, col); parts.append(palm)
    # four fingers, index (inner) to little (outer); each two segments, bending down
    for i, (off, length, rad) in enumerate(((-2.6, 1.0, .95), (-.87, 1.12, 1.0), (.87, 1.06, .97), (2.55, .86, .88))):
        base = wr + fwd * 4.6 - inward * off - up * .25      # off < 0: toward the thumb
        d1 = (fwd * math.cos(math.radians(7)) - up * math.sin(math.radians(7))).normalized()
        d2 = (fwd * math.cos(math.radians(30)) - up * math.sin(math.radians(30))).normalized()
        k = base + d1 * 2.8 * length
        tip = k + d2 * 2.3 * length
        parts += capsule_mesh(col, base, k, rad)
        parts += capsule_mesh(col, k, tip, rad * .93)
    tb = wr + fwd * 2.0 + inward * 2.9 - up * .45
    tk = tb + (fwd * .8 + inward * .5 - up * .3).normalized() * 2.6
    tt = tk + (fwd * .8 + inward * .1 - up * .55).normalized() * 2.0
    parts += capsule_mesh(col, tb, tk, 1.05)
    parts += capsule_mesh(col, tk, tt, .95)
    h = join(parts, "GiantHand_" + ("L" if side > 0 else "R"))
    h.data.materials.clear(); h.data.materials.append(skin_mat)
    remesh(h, .16, 6, .6, .08)
    smooth_shade(h)
    return h


# the face, to GC_rig_A/B_front.png: eyes at +-.355 of the head's radius across, a touch above the middle, 1.24 m wide
# and 1.67 m tall; soft blush on the cheeks (+-.64 across, .28 down, about 2.8 m wide) painted into the face texture
EYE_DIRS = [Vector((s * .355, -1, .03)).normalized() for s in (-1, 1)]
CHEEK_DIRS = [Vector((s * .64, -.715, -.28)).normalized() for s in (-1, 1)]
FACE_SKIN = (.94, .81, .66)        # sRGB, the same skin as the hands (GiantChessCourse)
FACE_BLUSH = (.96, .60, .48)
FACE_PNG = os.path.join(HERE, "textures", "giant_face.png")


def face_uvs(ob):
    """Longitude / latitude UVs from the head's centre, the seam at the back of the head."""
    me = ob.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    c = ob.matrix_world.inverted() @ HEAD_C
    for poly in me.polygons:
        us = []
        for li in poly.loop_indices:
            n = (me.vertices[me.loops[li].vertex_index].co - c).normalized()
            us.append([math.atan2(n.x, -n.y) / (2 * math.pi) + .5, math.asin(max(-1, min(1, n.z))) / math.pi + .5])
        if max(u for u, _ in us) - min(u for u, _ in us) > .5:      # faces across the seam: keep them on one side
            for q in us:
                if q[0] < .5:
                    q[0] += 1
        for li, q in zip(poly.loop_indices, us):
            uv.data[li].uv = q


def face_texture(w=512, h=256):
    """The skin with two soft blush spots (sRGB PNG), in the head's longitude / latitude UVs."""
    u = (np.arange(w) + .5) / w; v = (np.arange(h) + .5) / h
    lon = (u - .5) * 2 * math.pi; lat = (v - .5) * math.pi
    LON, LAT = np.meshgrid(lon, lat)
    n = np.stack([np.sin(LON) * np.cos(LAT), -np.cos(LON) * np.cos(LAT), np.sin(LAT)], -1)
    k = np.zeros(LON.shape)
    for d in CHEEK_DIRS:
        ang = np.arccos(np.clip(n @ np.array(d), -1, 1))
        k = np.maximum(k, np.exp(-(ang / .2) ** 5) * .9)
    rgb = np.array(FACE_SKIN) * (1 - k[..., None]) + np.array(FACE_BLUSH) * k[..., None]
    a = np.concatenate([rgb, np.ones(k.shape + (1,))], -1).astype(np.float32)
    os.makedirs(os.path.dirname(FACE_PNG), exist_ok=True)
    save_px(a, FACE_PNG)
    return FACE_PNG


def face_material():
    m = bpy.data.materials.get("GM_Face") or bpy.data.materials.new("GM_Face")
    m.use_nodes = True
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(face_texture(), check_existing=False)
    tex.interpolation = "Linear"
    nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = .55
    m.diffuse_color = FACE_SKIN + (1,)
    return m


def head(col, skin_mat, ink_mat, blush_mat, gold_mat):
    parts = []
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=20, radius=HEAD_R, location=HEAD_C)
    h = bpy.context.object; h.name = "GiantHead"; h.data.materials.append(face_material()); parts.append(h)
    for d in EYE_DIRS:
        # a flat oval dot sitting on the skin, its thin axis along the head's surface normal
        bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, radius=1, location=HEAD_C + d * (HEAD_R - .08))
        e = bpy.context.object; e.name = "GiantEye"; e.data.materials.append(ink_mat); parts.append(e)
        e.scale = (.86, .34, 1.0)
        e.rotation_euler = Vector((0, -1, 0)).rotation_difference(d).to_euler()
    for o in parts:
        link(o, col)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        smooth_shade(o)
    face_uvs(h)
    return parts + collar(col, gold_mat)


def lathe(col, name, prof, centre, sy, segs, mats):
    """A ring lathed from a closed (radius, z) profile round a vertical axis, squashed front to back by sy."""
    bm = bmesh.new()
    rings = [[bm.verts.new(Vector((centre.x + r * math.cos(a), centre.y + r * sy * math.sin(a), centre.z + z)))
              for r, z in prof] for a in (2 * math.pi * k / segs for k in range(segs))]
    n = len(prof)
    for i in range(segs):
        r0, r1 = rings[i], rings[(i + 1) % segs]
        for j in range(n):
            bm.faces.new((r0[j], r1[j], r1[(j + 1) % n], r0[(j + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = bm_object(name, bm, col, mats)
    sub = ob.modifiers.new("sub", "SUBSURF"); sub.levels = 1
    apply_all(ob)
    return ob


def collar(col, gold_mat):
    """The stand-up band (the head sits in it, a little flared at its foot, a rolled top edge) standing on the wide
    gold yoke: a shallow ring over the shoulders with a rounded, slightly turned-down rim, its front a little lower."""
    yb = zr(474)                                  # the yoke's top where the band stands
    rb, rt = (612 - 410) * F / 2, (600 - 422) * F / 2
    h = zr(420) - yb
    band = lathe(col, "GiantCollar", [(rb - 1.6, -1.2), (rb + .45, -1.2), (rb + .3, -.4), (rb, h * .35), (rt + .45, h - .5),
                                      (rt + .35, h - .1), (rt - .2, h + .1), (rt - .9, h - .25), (rb - 1.5, h * .3)],
                 Vector((0, ys(588), yb)), .93, 28, [gold_mat])
    ro = (668 - 356) * F / 2 * 1.08
    yoke = lathe(col, "GiantCollarFlange", [(rb - 1.0, .1), (rb + .7, .2), (ro - 1.4, -1.0), (ro - .1, -1.45),
                                            (ro - .2, -2.35), (ro - 1.4, -2.4), (rb + 1.2, -1.15), (rb - 1.0, -.9)],
                 Vector((0, ys(590), yb)), (708 - 462) / (668 - 356) * 1.0, 34, [gold_mat])
    for v in yoke.data.vertices:          # the front edge droops a little lower than the back
        v.co.z -= max(0.0, -(v.co.y - ys(590))) * .1
    return [band, yoke]


def feet(col, skin_mat):
    out = []
    for s in (-1, 1):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=14, radius=1, location=FOOT * Vector((s, 1, 1)))
        f = bpy.context.object; f.scale = (4.7, 6.6, 3.5)
        f.name = "GiantFoot_" + ("L" if s > 0 else "R"); f.data.materials.append(skin_mat)
        link(f, col)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        for v in f.data.vertices:          # a flat sole, the front a little fuller than the heel
            if v.co.z < -2.0:
                v.co.z = -2.0 - (v.co.z + 2.0) * .15
            if v.co.y < 0:
                v.co.z += .25 * (-v.co.y / 6.6)
        smooth_shade(f)
        out.append(f)
    return out


# ------------------------------------------------------------------ armature / skin

def armature(col):
    arm_data = bpy.data.armatures.new("GiantRig")
    ob = bpy.data.objects.new("GiantRig", arm_data)
    col.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def bone(name, head, tail, parent=None, connect=False, roll_up=None):
        b = eb.new(name); b.head = head; b.tail = tail
        if roll_up is not None:
            b.align_roll(roll_up)
        if parent:
            b.parent = eb[parent]; b.use_connect = connect

    y0 = ys(612)
    fwd = Vector((0, -1, 0))
    bone("hips", Vector((0, y0, 1)), Vector((0, y0, zr(850))), roll_up=fwd)
    bone("spine", Vector((0, y0, zr(850))), Vector((0, y0, zr(720))), "hips", True, fwd)
    bone("chest", Vector((0, y0, zr(720))), Vector((0, ys(600), zr(540))), "spine", True, fwd)
    bone("neck", Vector((0, ys(600), zr(540))), Vector((0, ys(590), zr(440))), "chest", True, fwd)
    bone("head", Vector((0, ys(590), zr(440))), Vector((0, ys(575), zr(150))), "neck", True, fwd)
    for s, tag in ((1, "L"), (-1, "R")):
        sh, el, wr = arm(s)
        m = Vector((s, 1, 1))
        up = Vector((0, 0, 1))
        bone("shoulder." + tag, Vector((s * 2.5, ys(600), zr(560))), sh, "chest", roll_up=up)
        bone("upperarm." + tag, sh, el, "shoulder." + tag, True, up)
        bone("forearm." + tag, el, wr, "upperarm." + tag, True, up)
        w, hf, right, hu, inward = hand_frame(s)
        bone("hand." + tag, wr, wr + hf * 4.6, "forearm." + tag, True, hu)
        bone("fingers." + tag, wr + hf * 4.6, wr + hf * 9.4, "hand." + tag, True, hu)
        bone("thumb." + tag, wr + hf * 2.2 + inward * 3.0, wr + hf * 2.2 + inward * 3.0 +
             (hf * .8 + inward * .45 - hu * .45).normalized() * 4.0, "hand." + tag, False, hu)
        bone("thigh." + tag, HIP * m, KNEE * m, "hips", roll_up=up)
        bone("shin." + tag, KNEE * m, ANKLE * m, "thigh." + tag, True, fwd)
        bone("foot." + tag, ANKLE * m, FOOT * m + Vector((0, -5, 0)), "shin." + tag, True, up)
    bpy.ops.object.mode_set(mode="OBJECT")
    return ob


def side_of(name):
    return 1 if name.endswith(".L") else -1 if name.endswith(".R") else 0


def remap_weights(ob, remap, keep=None, extra=None):
    """Rebuild ob's skin weights: groups renamed through remap (merged), the other side's bones dropped by the vertex's
    x, extra(i, co, w) -> dict override, normalised to the 4 heaviest bones."""
    names = {g.index: g.name for g in ob.vertex_groups}
    new = []
    for v in ob.data.vertices:
        w = {}
        for g in v.groups:
            n = remap.get(names[g.group], names[g.group])
            if keep is not None and n not in keep:
                continue
            w[n] = w.get(n, 0) + g.weight
        x = v.co.x
        if abs(x) > .6:
            w = {n: a for n, a in w.items() if side_of(n) == 0 or side_of(n) == (1 if x > 0 else -1)}
        if extra:
            w = extra(v.co, w) or w
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
        tot = sum(a for _, a in top)
        new.append({n: a / tot for n, a in top} if tot > 1e-6 else {})
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    groups = {}
    for i, w in enumerate(new):
        for n, a in w.items():
            if n not in groups:
                groups[n] = ob.vertex_groups.new(name=n)
            groups[n].add([i], a, "REPLACE")
    return [i for i, w in enumerate(new) if not w]


def skin(rig, robe, cuffs, sleeve_cuffs, hands, rigid):
    bpy.ops.object.select_all(action="DESELECT")
    for m in [robe] + hands:
        m.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    wrists = {s: arm(s)[2] for s in (1, -1)}

    def robe_fix(co, w):
        tag = "L" if co.x > 0 else "R"
        s0 = 1 if co.x > 0 else -1
        sh_, el_, wr_ = arm(s0)
        if min(seg_dist(co, sh_, el_), seg_dist(co, el_, wr_)) > 7.5:     # the torso: no arm weight
            w = {n: a for n, a in w.items() if not n.split('.')[0] in ('shoulder', 'upperarm', 'forearm')}
            if not w:
                return {'chest' if co.z > zr(720) else 'spine': 1.0}
        # round the shoulder: a long, even blend chest -> shoulder -> upper arm along the arm (auto weights pinch
        # the back of the armpit into a crease when the arm swings forward or up)
        adir = (el_ - sh_).normalized()
        t = (co - sh_).dot(adir)
        dist = seg_dist(co, sh_ - adir * 7, el_)
        if t > -10 and dist < 9.0 and co.z > zr(820):
            wu = min(max((t + 5) / 9, 0), 1); wu = wu * wu * (3 - 2 * wu)
            even = {"upperarm." + tag: wu, "shoulder." + tag: (1 - wu) * .55, "chest": (1 - wu) * .45}
            # fade into the automatic weights at every edge of the region (no seams)
            sm = lambda x: (lambda c: c * c * (3 - 2 * c))(min(max(x, 0), 1))
            f = sm((9.0 - dist) / 3.0) * sm((t + 10) / 4.0) * sm((co.z - zr(820)) / 4.0) * (1 - sm((t - 3) / 4.0))
            out = {n: a * (1 - f) for n, a in w.items()}
            for n, a in even.items():
                out[n] = out.get(n, 0) + a * f
            return out
        if co.z < zr(1065):                      # the trouser legs below the seat: the shin only
            return {"shin." + tag: 1.0}
        s = 1 if co.x > 0 else -1
        d = (co - wrists[s]).length
        if d < 4.5:                              # the sleeve's end: the forearm only, so the rigid cuff fits it
            return {"forearm." + tag: 1.0}
        if d < 7.5:
            k = (d - 4.5) / 3.0
            out = {n: a * k for n, a in w.items()}
            out["forearm." + tag] = out.get("forearm." + tag, 0) + (1 - k)
            return out
        if not w:
            w = {"chest": 1.0}
        # round the neck, under the yoke: ride the chest with it (no crumpling under the gold), blending out past
        # the yoke's rim
        rn = math.hypot(co.x, (co.y - ys(590)) / .79)
        if co.z > zr(600) and rn < YOKE_R + 2.5:
            k = min(max((rn - (YOKE_R - 1.5)) / 4.0, 0), 1)
            out = {n: a * k for n, a in w.items()}
            out["chest"] = out.get("chest", 0) + (1 - k)
            return out
        return w

    remap = {}
    for t in ("L", "R"):
        remap.update({"hand." + t: "forearm." + t, "fingers." + t: "forearm." + t, "thumb." + t: "forearm." + t,
                      "foot." + t: "shin." + t})
    remap.update({"head": "chest", "neck": "chest"})
    empty = remap_weights(robe, remap, extra=robe_fix)
    smooth_weights(robe, 4)
    remap_weights(robe, {}, extra=robe_fix)
    print("[rl_giant] robe vertices without weight:", len(empty))
    for h, tag in zip(hands, ("L", "R")):
        keep = {"hand." + tag, "fingers." + tag, "thumb." + tag}

        def hand_fix(co, w, tag=tag):
            return w if w else {"hand." + tag: 1.0}
        remap_weights(h, {"forearm." + tag: "hand." + tag}, keep | {"forearm." + tag}, hand_fix)
    for ob in [cuffs] + sleeve_cuffs + [o for o, _ in rigid]:
        ob.parent = rig
        ob.matrix_parent_inverse = rig.matrix_world.inverted()
        ob.modifiers.new("Armature", "ARMATURE").object = rig
    for v in cuffs.data.vertices:
        tag = "L" if v.co.x > 0 else "R"
        g = cuffs.vertex_groups.get("shin." + tag) or cuffs.vertex_groups.new(name="shin." + tag)
        g.add([v.index], 1.0, "REPLACE")
    for ob, tag in zip(sleeve_cuffs, ("L", "R")):
        ob.vertex_groups.new(name="forearm." + tag).add(range(len(ob.data.vertices)), 1.0, "REPLACE")
    for ob, bone_name in rigid:
        ob.vertex_groups.new(name=bone_name).add(range(len(ob.data.vertices)), 1.0, "REPLACE")


def seg_dist(p, a, b):
    ab = b - a
    t = min(max((p - a).dot(ab) / ab.length_squared, 0), 1)
    return (p - (a + ab * t)).length


def smooth_weights(ob, repeat):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=.5, repeat=repeat)
    bpy.ops.object.mode_set(mode="OBJECT")


def build(which):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    col = bpy.data.collections.new("Giant " + which)
    bpy.context.scene.collection.children.link(col)
    robe = mat("GM_Robe", ROBE[which], .9, 0, .5)
    inner = mat("GM_RobeInside", tuple(c * .4 for c in ROBE[which][:3]) + (1,), .95)
    gold = mat("GM_Gold", GOLD, .32, .95)
    skin_m = mat("GM_Skin", SKIN, .5)
    ink = mat("GM_Ink", INK, .12)
    blush = mat("GM_Blush", BLUSH, .7)
    body = body_mesh(col, robe)
    cuffs = leg_cuffs(col, body, gold)
    sleeve_cuffs = [sleeve_cuff(col, s, gold, inner) for s in (1, -1)]
    hands = [hand(col, s, skin_m) for s in (1, -1)]
    head_parts = head(col, skin_m, ink, blush, gold)
    fts = feet(col, skin_m)
    rig = armature(col)
    rigid = [(fts[0], "foot.L"), (fts[1], "foot.R")]
    rigid += [(p, "chest" if p.name.startswith("GiantCollarFlange") else "neck" if p.name.startswith("GiantCollar") else "head")
              for p in head_parts]
    skin(rig, body, cuffs, sleeve_cuffs, hands, rigid)
    tris = 0
    for o in col.objects:
        if o.type == "MESH":
            n = sum(len(p.vertices) - 2 for p in o.data.polygons)
            tris += n
            print("[rl_giant] %-22s %6d tris" % (o.name, n))
    print("[rl_giant] total", tris, "tris")
    return rig


# ------------------------------------------------------------------ poses / stills

def _world(rig, pb):
    return rig.matrix_world @ pb.matrix


def turn(rig, bone, axis, deg):
    """Rotate a bone (and its children) about a world axis through its head."""
    bpy.context.view_layer.update()
    pb = rig.pose.bones[bone]
    M = _world(rig, pb)
    q = Quaternion(Vector(axis).normalized(), math.radians(deg))
    T = Matrix.Translation(M.translation)
    pb.matrix = rig.matrix_world.inverted() @ (T @ q.to_matrix().to_4x4() @ T.inverted() @ M)
    bpy.context.view_layer.update()


def aim(rig, bone, direction):
    """Swing a bone so it points along a world direction."""
    bpy.context.view_layer.update()
    pb = rig.pose.bones[bone]
    M = _world(rig, pb)
    cur = (M.to_3x3() @ Vector((0, 1, 0))).normalized()
    q = cur.rotation_difference(Vector(direction).normalized())
    T = Matrix.Translation(M.translation)
    pb.matrix = rig.matrix_world.inverted() @ (T @ q.to_matrix().to_4x4() @ T.inverted() @ M)
    bpy.context.view_layer.update()


CLAVICLE_SHARE = .3      # the shoulder bone takes this much of the upper arm's swing (GiantGrandmaster.cs the same)


def arm_aim(rig, tag, direction):
    """Swing the upper arm to a direction, the shoulder (clavicle) bone taking CLAVICLE_SHARE of the turn."""
    bpy.context.view_layer.update()
    M = _world(rig, rig.pose.bones["upperarm." + tag])
    cur = (M.to_3x3() @ Vector((0, 1, 0))).normalized()
    q = Quaternion().slerp(cur.rotation_difference(Vector(direction).normalized()), CLAVICLE_SHARE)
    axis, ang = q.to_axis_angle()
    if ang > 1e-4:
        turn(rig, "shoulder." + tag, axis, math.degrees(ang))
    aim(rig, "upperarm." + tag, direction)


def pose(rig, name):
    for b in rig.pose.bones:
        b.rotation_mode = "QUATERNION"; b.rotation_quaternion = (1, 0, 0, 0); b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    if name == "reach":         # lean in, the right hand down on the board in front
        turn(rig, "spine", (1, 0, 0), 12); turn(rig, "chest", (1, 0, 0), 12); turn(rig, "chest", (0, 0, 1), -10)
        turn(rig, "head", (1, 0, 0), 14)
        arm_aim(rig, "R", (-.25, -.8, -.25)); aim(rig, "forearm.R", (-.05, -.75, -.6))
        aim(rig, "hand.R", (0, -.45, -.9)); turn(rig, "fingers.R", (1, 0, 0), 25)
    elif name == "raise":       # the right arm up high (lifting a piece), the left out to the side
        turn(rig, "chest", (1, 0, 0), -5); turn(rig, "head", (1, 0, 0), -12)
        arm_aim(rig, "R", (-.35, -.35, .87)); aim(rig, "forearm.R", (-.1, -.45, .89))
        aim(rig, "hand.R", (0, -.8, .5)); turn(rig, "fingers.R", (1, 0, 0), 35)
        arm_aim(rig, "L", (1, -.1, .05)); aim(rig, "forearm.L", (.9, -.45, -.05))


def setup_render(w, h):
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.film_transparent = False
    wd = bpy.data.worlds.get("w") or bpy.data.worlds.new("w"); sc.world = wd
    wd.use_nodes = True
    bg = next(n for n in wd.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (1, 1, 1, 1); bg.inputs["Strength"].default_value = .55
    if "key" not in bpy.data.objects:
        k = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN")); sc.collection.objects.link(k)
        k.data.energy = 2.6; k.rotation_euler = (math.radians(40), 0, math.radians(-28))
        k.data.angle = math.radians(8)
    cam = bpy.data.objects.get("cam")
    if cam is None:
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.clip_end = 2000
    return sc, cam


VIEWS = (("front", Vector((0, -1, 0))), ("side", Vector((1, 0, 0))), ("threequarter", Vector((-.75, -1, .3))))


def bounds_in_view(d):
    """The posed meshes' extent across a view direction: centre and (width, height)."""
    dg = bpy.context.evaluated_depsgraph_get()
    d = d.normalized()
    right = d.cross(Vector((0, 0, 1))).normalized() * -1
    up = right.cross(d).normalized() * -1
    xs, zs, pts = [], [], []
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        me = o.evaluated_get(dg).to_mesh()
        for v in list(me.vertices)[::7]:
            p = o.matrix_world @ v.co
            pts.append(p); xs.append(p.dot(right)); zs.append(p.dot(up))
        o.evaluated_get(dg).to_mesh_clear()
    c = sum(pts, Vector()) / len(pts)
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    centre = c - right * (c.dot(right) - cx) - up * (c.dot(up) - cz)
    return centre, max(xs) - min(xs), max(zs) - min(zs)


def render_views(tag, w=520, h=680, fixed=None):
    sc, cam = setup_render(w, h)
    out = []
    for view, d in VIEWS:
        if fixed:
            centre, scale = fixed[view]
        else:
            centre, bw, bh = bounds_in_view(d)
            scale = max(bw * 1.12 * (h / w if h > w else 1), bh * 1.08)
        cam.data.ortho_scale = scale
        cam.location = centre + d.normalized() * 400
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(STILLS, "%s_%s.png" % (tag, view))
        bpy.ops.render.render(write_still=True)
        out.append(sc.render.filepath)
    return out


def load_px(path, w=None, h=None):
    img = bpy.data.images.load(path, check_existing=False)
    if w and (img.size[0] != w or img.size[1] != h):
        img.scale(w, h)
    a = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
    bpy.data.images.remove(img)
    return a


def save_px(a, path):
    h, w = a.shape[:2]
    img = bpy.data.images.new("sheet", w, h, alpha=True)
    img.pixels.foreach_set(a.ravel())
    img.filepath_raw = path; img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return path


def sheet(rows, path, gap=8):
    """rows: list of lists of image paths (top row first) -> one PNG."""
    tiles = [[load_px(p) for p in r] for r in rows]
    w = max(sum(t.shape[1] for t in r) + gap * (len(r) - 1) for r in tiles)
    lines = []
    for r in tiles:
        hh = max(t.shape[0] for t in r)
        line = np.ones((hh, w, 4), np.float32)
        x = 0
        for t in r:
            line[hh - t.shape[0]:, x:x + t.shape[1]] = t
            x += t.shape[1] + gap
        lines.append(line)
    out = []
    for i, l in enumerate(lines[::-1]):     # bottom-up rows
        if i:
            out.append(np.ones((gap, w, 4), np.float32))
        out.append(l)
    return save_px(np.concatenate(out, 0), path)


def compare_sheet(which):
    """The model in its rest pose next to the reference drawings, at the drawings' own scale and framing."""
    s = which.upper()
    w, h = 512, 768
    pose(bpy.data.objects["GiantRig"], "rest")
    scale = 1536 * F
    fixed = {"front": (Vector((0, 0, zr(768))), scale), "side": (Vector((0, ys(512), zr(768))), scale),
             "threequarter": (Vector((0, 0, zr(768))), scale)}
    paths = render_views("cmp_%s" % which.lower(), w, h, fixed)
    tiles = [os.path.join(REFS, "GC_rig_%s_front.png" % s), paths[0], os.path.join(REFS, "GC_rig_%s_side.png" % s), paths[1]]
    arrs = [load_px(p, w, h) for p in tiles]
    gap = np.ones((h, 8, 4), np.float32)
    row = np.concatenate([arrs[0], gap, arrs[1], gap, arrs[2], gap, arrs[3]], 1)
    out = save_px(row, os.path.join(STILLS, "giant_%s_vs_reference.png" % which.lower()))
    for p in paths:
        os.remove(p)
    return out


# ------------------------------------------------------------------ Unity

ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT_REL = "Assets/Resources/RoyalLibrary/Models/ChessRoom"


def metre_uvs(ob):
    """Box-projected UVs in metres (the library's cloth and brass are seamless tiles sized in metres)."""
    me = ob.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co[a], co[b])


def unity_meta(rel):
    """A ModelImporter .meta: Generic rig without an avatar (code drives the bones), no animation, file scale 1."""
    import hashlib
    with open(os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta"), encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines[1] = "guid: " + hashlib.md5(("chessfight:" + rel).encode()).hexdigest()
    text = "\n".join(lines).replace("  animationType: 0", "  animationType: 2")
    with open(os.path.join(ROOT, rel + ".meta"), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


# Unity draw cost (승규 11-18 "면 줄이고 합치는데 조금만 적용 시키고 거인에 lod 적용 해"): the parts are joined by
# material into four skinned meshes (the eyes stay apart: GiantGrandmaster blinks them by turning their renderers off),
# each written twice, <name>_LOD0 as built and <name>_LOD1 decimated to LOD1_RATIO; Unity's importer makes the LODGroup
# from the names, GiantGrandmaster sets where it switches.
MERGE = [("GiantRobe", ["GiantRobe"]), ("GiantGold", ["GiantCollar", "GiantCollarFlange", "GiantLegCuffs"]),
         ("GiantCuffs", ["GiantCuff_L", "GiantCuff_R"]),
         ("GiantSkin", ["GiantHead", "GiantHand_L", "GiantHand_R", "GiantFoot_L", "GiantFoot_R"])]
LOD1_RATIO = .5


def merge_and_lod(col):
    out = []
    for name, parts in MERGE:
        ob = join([bpy.data.objects[n] for n in parts], name + "_LOD0")
        lod = ob.copy(); lod.data = ob.data.copy(); lod.name = name + "_LOD1"
        col.objects.link(lod)
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = lod; lod.select_set(True)
        dec = lod.modifiers.new("dec", "DECIMATE"); dec.ratio = LOD1_RATIO
        bpy.ops.object.modifier_move_to_index(modifier="dec", index=0)
        bpy.ops.object.modifier_apply(modifier="dec")
        out += [ob, lod]
    for o in out:
        print("[rl_giant] %-18s %6d tris" % (o.name, sum(len(p.vertices) - 2 for p in o.data.polygons)))
    return out


def export(which):
    """RlGiant_<A|B>.fbx: the armature and every skinned part, rest pose, metres, Blender -Y front."""
    rig = bpy.data.objects["GiantRig"]
    pose(rig, "rest")
    meshes = [o for o in bpy.data.collections["Giant " + which].objects if o.type == "MESH"]
    for o in meshes:
        if o.name != "GiantHead":
            metre_uvs(o)
    merge_and_lod(bpy.data.collections["Giant " + which])
    meshes = [o for o in bpy.data.collections["Giant " + which].objects if o.type == "MESH"]
    folder = os.path.join(ROOT, OUT_REL)
    os.makedirs(folder, exist_ok=True)
    if not os.path.exists(folder + ".meta"):
        import hashlib
        with open(folder + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                    % hashlib.md5(("chessfight:" + OUT_REL).encode()).hexdigest())
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes + [rig]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rel = "%s/RlGiant_%s.fbx" % (OUT_REL, which)
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, rel), use_selection=True, object_types={"MESH", "ARMATURE"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
        unity_meta(rel)
    import shutil      # the face texture (skin + blush), drawn by GiantChessCourse on the GM_Face slot
    shutil.copyfile(FACE_PNG, os.path.join(folder, "RlGiantFace.png"))
    print("[rl_giant] exported", rel)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = (argv[0] if argv else "A").upper()
    os.makedirs(STILLS, exist_ok=True)
    rig = build(which)
    rows = []
    for p in ("rest", "reach", "raise"):
        pose(rig, p)
        rows.append(render_views("giant_%s_%s" % (which.lower(), p)))
    print("[rl_giant] sheet", sheet(rows, os.path.join(STILLS, "giant_%s_poses.png" % which.lower())))
    print("[rl_giant] compare", compare_sheet(which))
    pose(rig, "rest")
    if "--export" in argv:
        export(which)
    out = os.path.join(HERE, "rl_giant_%s.blend" % which.lower())
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print("[rl_giant] saved", out)
