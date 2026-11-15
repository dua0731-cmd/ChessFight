# Eyes as meshes (2026-11-10 decision: geometry, not textures). For each piece: the design-matched eye outline
# (eye_draw.py shapes, in eye widths) becomes a ring mesh that sits on the head's own low-poly faces (each vertex is
# carried along the projection direction onto the faceted surface), lifted along the smooth surface normal by a
# rounded rim and a low dome, so it reads like the old pawn's slightly raised eyes. The glint is a smaller raised
# piece on top of the eye; the knight adds a brow bar; the blush is a very thin disc. Normals are custom: the smooth
# head's normal tilted by the relief's slope, so the eye shades smooth over the facets.
# The eye objects are written, alone, to <out>/<Piece>_Eyes.blend in the rigged file's world coordinates
# (identity transforms, no parent); the rig session attaches them to the Head bone.
# blender -b --factory-startup -P eye_mesh.py -- <config.json>
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import mathutils.geometry as mg

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eye_draw as ed

CFG = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
OUT = CFG["out"]
os.makedirs(OUT, exist_ok=True)
PIECE = CFG["piece"]                                   # object name in the rigged file: Pawn, Rook, Knight, Bishop
STYLE = CFG["style"]                                   # eye_draw style: oval, rect, diamond, knight
TEAM_SHAPE = CFG.get("shape_team", "white")            # whose drawing values (TEAM_STYLE) the mesh follows
ed.STYLES[STYLE].update(ed.TEAM_STYLE.get((TEAM_SHAPE, STYLE), {}))
ed.STYLES[STYLE].update({k: (tuple(v) if isinstance(v, list) else v) for k, v in CFG.get("style_over", {}).items()})
S = ed.STYLES[STYLE]

bpy.ops.wm.open_mainfile(filepath=CFG["blend"])
body = bpy.data.objects[PIECE]
MW = body.matrix_world.copy()
me = body.data

# ---- the body without arms, hands and feet, in world space; its smooth (vertex) normals
gidx = {g.index for g in body.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Foot"))}
limb = {v.index for v in me.vertices if sum(g.weight for g in v.groups if g.group in gidx) >= 0.5}
old_face = set()
if CFG.get("remove_old_face") and "Col" in me.color_attributes:   # the pawn file still carries the old transplanted face
    attr = me.color_attributes["Col"]
    EYE, GLINT = (0.118, 0.098, 0.078), (0.93, 0.93, 0.93)
    SKIN, HAND, FOOT = (0.945, 0.808, 0.702), (0.275, 0.231, 0.208), (0.157, 0.133, 0.118)
    near = lambda c, r, t=0.04: max(abs(c[i] - r[i]) for i in range(3)) < t
    for poly in me.polygons:
        c = attr.data[poly.loop_start].color_srgb[:3]
        if poly.center.z > 0.6 and (near(c, EYE, 0.03) or near(c, GLINT, 0.03) or
                                    not (near(c, SKIN) or near(c, HAND) or near(c, FOOT))):
            old_face.add(poly.index)
bm = bmesh.new()
bm.from_mesh(me)
bm.transform(MW)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
dead = [f for f in bm.faces if f.index in old_face or any(v.index in limb for v in f.verts)]
bmesh.ops.delete(bm, geom=dead, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="FIXED", ngon_method="BEAUTY")   # quads split 0-2, as Blender and an FBX fan do
bm.normal_update()
TRIS = [[(v.co.copy(), v.normal.copy()) for v in f.verts] for f in bm.faces]
TREE = BVHTree.FromBMesh(bm)
bm.free()
print("BODY", PIECE, "faces", len(TRIS), "limb verts", len(limb), "old face polys", len(old_face))


def smooth_normal(p):
    loc, nrm, idx, dist = TREE.find_nearest(p)
    (a, na), (b, nb), (c, nc) = TRIS[idx]
    w = mg.barycentric_transform(loc, a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
    n = na * w.x + nb * w.y + nc * w.z
    return n.normalized() if n.length > 1e-8 else nrm


# ---- placement: the pawn by azimuth / elevation around its head's centre, the others by azimuth / height
def frame(az, side_sign, el=None, z=None, roll=0.0, front=0.0, centre=None):
    a = math.radians(az) * side_sign
    if el is not None:
        e = math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        origin = Vector(centre)
    else:
        d = Vector((math.sin(a), -math.cos(a), 0))
        origin = Vector((0, 0, z))
    hit = TREE.ray_cast(origin + d * 2.0, -d)
    p = hit[0]
    n = CFG.get("frame_normal") == "ellipsoid" and ellipsoid_normal(p) or smooth_normal(p)
    if front:
        n = (n * (1 - front) + Vector((0, -1, 0)) * front).normalized()
    eu = Vector((-n.y, n.x, 0)).normalized()
    ev = n.cross(eu).normalized()
    if roll:
        r = math.radians(roll) * (1 if side_sign < 0 else -1)
        eu, ev = eu * math.cos(r) - ev * math.sin(r), eu * math.sin(r) + ev * math.cos(r)
    return dict(p=p, n=n, eu=eu, ev=ev)


ELL = CFG.get("ellipsoid")                             # the pawn's head: centre z and semi-axes (as pawn_face.py)
def ellipsoid_normal(p):
    zc, ea, eb, ec = ELL
    q = p - Vector((0, 0, zc))
    return Vector((q.x / ea ** 2, q.y / eb ** 2, q.z / ec ** 2)).normalized()


def onto_body(q, n):
    """q (on the frame's tangent plane) carried along n onto the faceted body."""
    hit = TREE.ray_cast(q + n * 0.08, -n, 0.3)
    if hit[0] is None:
        hit = TREE.find_nearest(q)
    return hit[0]


# ---- shapes: signed distance in eye widths (eye_draw), outline by bisection along rays from an inside point
class Pts:
    def __init__(self, x, y):
        self.X = np.asarray(x, dtype=np.float32); self.Y = np.asarray(y, dtype=np.float32); self.ppw = 1.0


def sdf_eye(side):
    def f(u, v):
        if STYLE == "diamond" and S.get("kite"):
            # the bishop's sheet (11-12) draws the diamond as a kite: its side corners sit below the middle
            # ("kite": their height in eye widths, negative = lower)
            h, kv = S["aspect"], S["kite"]
            return ed.rounded_polygon(Pts(u, v), [(0, h / 2), (0.5, kv), (0, -h / 2), (-0.5, kv)], S["corner"])
        return ed.base_shape(Pts(u, v), STYLE)
    return f


def sdf_polygon(pts, rad=0.0):
    def f(u, v):
        return ed.rounded_polygon(Pts(u, v), pts, rad) if rad > 0 else ed.polygon(Pts(u, v), pts)
    return f


def star_points(cx, cy, st):
    """An eight-point star (the queen's glint): long points up, down and sideways, shorter ones between."""
    pts = []
    for i in range(16):
        ang = math.pi / 2 - i * math.pi / 8
        if i % 2:
            r = st["ri"]
        elif i % 8 == 0:
            r = st.get("ro_v", st["ro"])
        elif i % 4 == 0:
            r = st.get("ro_h", st["ro"])
        else:
            r = st.get("ro_d", st["ro"])
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return pts


def sdf_ellipse(cx, cy, w, h):
    def f(u, v):
        return ed.ellipse(Pts(u, v), cx, cy, w, h)
    return f


def sdf_box(cx, cy, w, h, rad, ang):
    ca, sa = math.cos(ang), math.sin(ang)
    def f(u, v):
        u = np.asarray(u, dtype=np.float32) - cx; v = np.asarray(v, dtype=np.float32) - cy
        return ed.box(Pts(u * ca + v * sa, -u * sa + v * ca), 0, 0, w, h, rad)
    return f


def outline(f, centre, n_rays=720, rmax=3.0):
    """Points of f's zero contour, star-shaped around centre, resampled evenly by arc length later."""
    angs = np.linspace(0, 2 * math.pi, n_rays, endpoint=False)
    lo = np.zeros(n_rays); hi = np.full(n_rays, rmax)
    for _ in range(40):
        mid = (lo + hi) / 2
        d = f(centre[0] + mid * np.cos(angs), centre[1] + mid * np.sin(angs))
        inside = d < 0
        lo = np.where(inside, mid, lo); hi = np.where(inside, hi, mid)
    r = (lo + hi) / 2
    pts = np.stack([centre[0] + r * np.cos(angs), centre[1] + r * np.sin(angs)], axis=1)
    seg = np.linalg.norm(np.diff(np.vstack([pts, pts[:1]]), axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    return pts, s


def resample(pts, s, n):
    t = np.linspace(0, s[-1], n, endpoint=False)
    closed = np.vstack([pts, pts[:1]])
    return np.stack([np.interp(t, s, closed[:, 0]), np.interp(t, s, closed[:, 1])], axis=1)


# ---- the relief: rim lift + rounded bevel (quarter circle) + low dome, all in metres
def relief(dist_in, u, v, P):
    """Height above the surface at a point dist_in (m) inside the outline; u, v in metres from the dome's centre."""
    t = np.clip(dist_in / P["bevel_w"], 0, 1)
    h = P["rim"] + P["bevel_h"] * np.sqrt(1 - (1 - t) ** 2)
    if P.get("dome", 0):
        dn = (u / P["dome_a"]) ** 2 + (v / P["dome_b"]) ** 2
        h = h + P["dome"] * np.clip(1 - dn, 0, 1)
    return h


RING_SCALES = [1.0, 0.993, 0.982, 0.966, 0.946, 0.92, 0.886, 0.84, 0.78, 0.70, 0.60, 0.48, 0.34, 0.18, 0.0]


def build_piece(name, f_sdf, centre, W, fr, P, n_seg=96, base_height=None, lift_extra=0.0, normal_height=None):
    """A ring mesh of the outline of f_sdf (eye-width units, centred at `centre`), W metres per unit, on the body
    under frame fr. base_height(u_m, v_m) adds the height of what it sits on (the glint on the eye)."""
    pts, s = outline(f_sdf, centre)
    ring0 = resample(pts, s, n_seg)
    bm_ = bmesh.new()
    verts = []
    uv_all = []
    for k, sc in enumerate(RING_SCALES):
        if sc == 0.0:
            uv = np.array([centre])
        else:
            uv = centre + (ring0 - centre) * sc
        uv_all.append(uv)
    # heights from the true distance to the edge (so the bevel is the same width all round)
    flat = np.vstack(uv_all)
    d_in = -f_sdf(flat[:, 0], flat[:, 1]) * W                       # metres inside the edge (>= 0)
    d_in = np.maximum(d_in, 0)
    uc, vc = (flat[:, 0] - centre[0]) * W, (flat[:, 1] - centre[1]) * W
    h = relief(d_in, uc, vc, P) + lift_extra
    if base_height is not None:
        h = h + base_height(flat[:, 0] * W, flat[:, 1] * W)
    # positions on the body, lifted along the smooth normal
    pos, nrm_s = [], []
    for (u, v), hh in zip(flat, h):
        q = fr["p"] + fr["eu"] * (u * W) + fr["ev"] * (v * W)
        sfc = onto_body(q, fr["n"])
        ns = smooth_normal(sfc)
        pos.append(sfc + ns * hh)
        nrm_s.append(ns)
    # in a crease the lifted point can still be under the neighbouring facet: push it out along the smooth normal
    # until nothing of the body lies just above it (a ray along the normal finds no face within 3 mm)
    clear = P.get("clear", 0.00015)
    pushed = 0
    for i in range(len(pos)):
        for _ in range(4):
            hit = TREE.ray_cast(pos[i], nrm_s[i], 0.003)
            if hit[0] is None:
                break
            pos[i] = hit[0] + nrm_s[i] * clear
            pushed += 1
    if pushed:
        print("PUSHED", name, pushed, "vertex moves out of creases")
    k0 = 0
    rings = []
    for uv in uv_all:
        rings.append([bm_.verts.new(pos[k0 + i]) for i in range(len(uv))])
        k0 += len(uv)
    for a_, b_ in zip(rings[:-1], rings[1:]):
        n_ = len(a_)
        if len(b_) == 1:
            for i in range(n_):
                bm_.faces.new((a_[i], a_[(i + 1) % n_], b_[0]))
        else:
            for i in range(n_):
                bm_.faces.new((a_[i], a_[(i + 1) % n_], b_[(i + 1) % n_], b_[i]))
    m = bpy.data.meshes.new(name)
    bm_.to_mesh(m)
    bm_.free()
    # outward faces
    if m.polygons[0].normal.dot(nrm_s[0]) < 0:
        m.flip_normals()
    for pl in m.polygons:
        pl.use_smooth = True
    # custom normals: the smooth surface normal tilted by the relief's slope (numeric gradient of the height field)
    eps = 0.0004
    def hfun(uu, vv):
        uu = np.atleast_1d(uu); vv = np.atleast_1d(vv)
        d = np.maximum(-f_sdf(uu / W, vv / W) * W, 0)
        hh = relief(d, uu - centre[0] * W, vv - centre[1] * W, P)
        if base_height is not None:
            hh = hh + base_height(uu, vv)
        return hh
    flatm = flat * W
    nh = normal_height or hfun                         # normal_height: shade like another surface (the glint)
    gu = (nh(flatm[:, 0] + eps, flatm[:, 1]) - nh(flatm[:, 0] - eps, flatm[:, 1])) / (2 * eps)
    gv = (nh(flatm[:, 0], flatm[:, 1] + eps) - nh(flatm[:, 0], flatm[:, 1] - eps)) / (2 * eps)
    vn = [(ns - fr["eu"] * a_ - fr["ev"] * b_).normalized() for ns, a_, b_ in zip(nrm_s, gu, gv)]
    m.normals_split_custom_set([vn[l.vertex_index] for l in m.loops])
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o, hfun


# ---- materials (white team on the objects; the black team's kept in the file with a fake user)
def mat(name, rgb, rough=0.4, spec=0.5, glow=0.0, albedo=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    lin = [c ** 2.2 for c in rgb]
    b.inputs["Base Color"].default_value = (*[c * albedo for c in lin], 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Specular IOR Level"].default_value = spec
    if glow:
        b.inputs["Emission Color"].default_value = (*lin, 1)
        b.inputs["Emission Strength"].default_value = glow
    m.diffuse_color = (*lin, 1)
    return m


MATS = {
    "eye_white": mat("Eye_WhiteTeam_Black", (0.045, 0.042, 0.038), rough=0.18, spec=0.25),
    "glint_white": mat("Glint_WhiteTeam", (0.97, 0.97, 0.97), rough=0.3, glow=0.5),
    "eye_black": mat("Eye_BlackTeam_Ivory", CFG.get("ivory", (0.97, 0.90, 0.76)), rough=0.4, spec=0.2, glow=0.9, albedo=0.2),
    "blush_white": mat("Blush_WhiteTeam", CFG.get("blush_white", (0.98, 0.62, 0.58)), rough=0.5, glow=0.15),
    "blush_black": mat("Blush_BlackTeam", CFG.get("blush_black", (0.86, 0.49, 0.48)), rough=0.5, glow=0.3),
}
for m in MATS.values():
    m.use_fake_user = True

# ---- build
EYE = CFG["eye"]
W = EYE["w"]
H = W * S["aspect"]
P_EYE = dict(rim=0.0003, bevel_w=CFG.get("bevel_w", 0.07) * W, bevel_h=CFG.get("bevel_h", 0.0012),
             dome=CFG.get("dome", 0.0013), dome_a=0.5 * W, dome_b=0.5 * H)
made = []
FRAMES = []
for side, sgn in ((0, -1), (1, 1)):                      # 0 = viewer's left (the piece's right eye)
    fr = frame(EYE["az"], sgn, el=EYE.get("el"), z=EYE.get("z"), roll=EYE.get("roll", 0), front=EYE.get("front", 0),
               centre=CFG.get("centre"))
    tag = "R" if side == 0 else "L"                      # the piece's own right / left
    inner = 1 if side == 0 else -1
    if STYLE == "knight":
        f_eye = ed.knight_eye_sdf(inner)
        ctr = ed.KNIGHT["eye_centre"](inner)
    else:
        f_eye = sdf_eye(side)
        ctr = (0.0, 0.0)
    eye, h_eye = build_piece("Eye_%s" % tag, f_eye, ctr, W, fr, P_EYE)
    eye.data.materials.append(MATS["eye_white"])
    made.append(eye)
    if S.get("glint"):
        gu, gv, gd = S["glint"]
        gx = gu + inner * S.get("glint_inner", 0.0)
        if STYLE == "knight":
            gx, gv = ed.KNIGHT["glint_pos"](inner)
        star = CFG.get("glint_star")
        if star:
            f_gl = sdf_polygon(star_points(gx, gv, star))
        else:
            f_gl = sdf_ellipse(gx, gv, gd, gd * S.get("glint_aspect", 1.0))
        P_GL = dict(rim=0.0002, bevel_w=(0.08 if star else 0.3) * gd / 2 * W, bevel_h=0.0003, dome=0.0002, dome_a=gd / 2 * W,
                    dome_b=gd / 2 * W * S.get("glint_aspect", 1.0))
        # glint_normals "eye" (default): the glint shades like the eye under it. In one lit game material the black
        # team paints the glint ivory like the eye, and the glint's own bevel / dome normals would show as a ring.
        own = CFG.get("glint_normals", "eye") == "own"
        gl, _ = build_piece("Glint_%s" % tag, f_gl, (gx, gv), W, fr, P_GL, n_seg=160 if star else 48,
                            base_height=lambda uu, vv, h_eye=h_eye: h_eye(uu, vv),
                            normal_height=None if own else (lambda uu, vv, h_eye=h_eye: h_eye(uu, vv)))
        gl.data.materials.append(MATS["glint_white"])
        made.append(gl)
    FRAMES.append((fr, inner))
    # lashes (the queen): small tapered strokes in the eye's colour, each a polygon in eye widths with x counted
    # outwards (away from the nose) from the eye's centre. Named Brow_* so the game export colours them like a brow.
    for i, la in enumerate(CFG.get("lashes", [])):
        pts = [(-inner * u_, v_) for u_, v_ in la["pts"]]
        if inner < 0:
            pts = pts[::-1]
        cx_ = sum(p[0] for p in pts) / len(pts)
        cy_ = sum(p[1] for p in pts) / len(pts)
        P_LA = dict(rim=0.0003, bevel_w=0.03 * W, bevel_h=CFG.get("lash_h", 0.0012), dome=0.0)
        lo, _ = build_piece("Brow_%s_%d" % (tag, i), sdf_polygon(pts, la.get("rad", 0.0)), (cx_, cy_), W, fr, P_LA, n_seg=48)
        lo.data.materials.append(MATS["eye_white"])
        made.append(lo)
    if STYLE == "knight":
        bx, by, bw, bh, brad, bang = ed.KNIGHT["brow"](inner)
        f_br = sdf_box(bx, by, bw, bh, brad, bang)
        P_BR = dict(rim=0.0003, bevel_w=CFG.get("brow_bevel_w", 0.04) * W, bevel_h=CFG.get("brow_h", 0.0018), dome=0.0)
        br, _ = build_piece("Brow_%s" % tag, f_br, (bx, by), W, fr, P_BR, n_seg=96)
        br.data.materials.append(MATS["eye_white"])
        made.append(br)
BL = CFG.get("blush")
if BL:
    for side, sgn in ((0, -1), (1, 1)):
        if "rel_eye" in BL:
            # placed from the eye's own frame: (towards the back of the head, up) in eye widths
            efr, inner = FRAMES[side]
            du, dv = BL["rel_eye"]
            q = efr["p"] + efr["eu"] * (du * -inner * W) + efr["ev"] * (dv * W)
            sp = onto_body(q, efr["n"])
            n_ = smooth_normal(sp)
            eu_ = Vector((-n_.y, n_.x, 0)).normalized()
            fr = dict(p=sp, n=n_, eu=eu_, ev=n_.cross(eu_).normalized())
        else:
            fr = frame(BL["az"], sgn, el=BL.get("el"), z=BL.get("z"), front=BL.get("front", 0), centre=CFG.get("centre"))
        d = BL["d"]
        asp = BL.get("aspect", 1.0)
        f_b = sdf_ellipse(0, 0, 1.0, asp)
        P_B = dict(rim=0.00015, bevel_w=0.12 * d, bevel_h=0.0003, dome=0.0)
        bl, _ = build_piece("Blush_%s" % ("R" if side == 0 else "L"), f_b, (0.0, 0.0), d, fr, P_B, n_seg=64)
        bl.data.materials.append(MATS["blush_white"])
        made.append(bl)
for o in made:
    print("MADE", o.name, len(o.data.vertices), "verts")

# ---- preview renders (body + eyes) if asked, then the eye-only file
if CFG.get("views"):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import eye_mesh_render
    eye_mesh_render.render(CFG, body, made, MATS, limb_free=CFG.get("no_limbs", True), old_face=old_face)

keep = set(made)
for o in list(bpy.data.objects):
    if o not in keep:
        bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.actions, bpy.data.images, bpy.data.armatures, bpy.data.cameras, bpy.data.lights):
    for d in list(coll):
        coll.remove(d)
for w in list(bpy.data.worlds):
    w.use_fake_user = False
bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
path = os.path.join(OUT, "%s_Eyes.blend" % PIECE)
bpy.context.preferences.filepaths.save_version = 0       # no .blend1 next to it
bpy.ops.wm.save_as_mainfile(filepath=path, copy=True)
print("SAVED", path)
