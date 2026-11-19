"""E5 front-course props, shape-only models (R687, concept sheet GC_E5_front_course_assets.png).

No image or procedural textures: flat-colour materials only. Every detail is geometry
(bevels, lathes, booleans, curves, arrays, solidify, weighted normals).

Run inside Blender (MCP or Text editor):
    exec(open(r"<repo>/ArtSource/PawnRush/RoyalLibraryModels/rl_e5_props_model.py").read())
    build_typewriter(); render_still("01_typewriter")

Axes and units follow the blockout (RoyalLibrary_blockout.blend): game metres, Z up,
the course runs +Y, so a prop's front faces -Y. Each model lives in its own collection
(E5_<nn>_<Name>) under a root empty at the origin; re-running a build replaces it.
"""
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector, Matrix

REPO = r"C:\Users\Makebot2\Documents\GitHub\ChessFight"
HERE = os.path.join(REPO, "ArtSource", "PawnRush", "RoyalLibraryModels")
STILLS = os.path.join(HERE, "stills", "e5")
BLEND = os.path.join(HERE, "rl_e5_props.blend")

# ---------------------------------------------------------------- materials
# Linear RGB picked from the concept sheet (sRGB -> linear).
PALETTE = {
    "E5_BlackEnamel": dict(rgb=(0.030, 0.020, 0.015), metal=0.0, rough=0.22, coat=0.0),
    "E5_Brass":       dict(rgb=(0.62, 0.38, 0.11), metal=1.0, rough=0.28),
    "E5_BrassDark":   dict(rgb=(0.40, 0.24, 0.07), metal=1.0, rough=0.38),
    "E5_Ivory":       dict(rgb=(0.80, 0.66, 0.45), rough=0.55),
    "E5_Paper":       dict(rgb=(0.78, 0.58, 0.30), rough=0.75),
    "E5_Rubber":      dict(rgb=(0.016, 0.014, 0.012), rough=0.45),
    "E5_DarkSteel":   dict(rgb=(0.060, 0.050, 0.042), metal=0.85, rough=0.35),
    "E5_Walnut":      dict(rgb=(0.085, 0.028, 0.011), rough=0.42, coat=0.2),
    "E5_RedLeather":  dict(rgb=(0.105, 0.026, 0.012), rough=0.5),
    "E5_RedWax":      dict(rgb=(0.42, 0.020, 0.018), rough=0.35),
}


def _sock(node, ident):
    for s in node.inputs:
        if s.identifier == ident:
            return s
    return None


def M(name):
    """Get or create one of the flat palette materials."""
    p = PALETTE[name]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    _sock(b, "Base Color").default_value = (*p["rgb"], 1.0)
    _sock(b, "Metallic").default_value = p.get("metal", 0.0)
    _sock(b, "Roughness").default_value = p.get("rough", 0.5)
    s = _sock(b, "Coat Weight")
    if s:
        s.default_value = p.get("coat", 0.0)
    m.diffuse_color = (*p["rgb"], 1.0)
    m.metallic = p.get("metal", 0.0)
    m.roughness = p.get("rough", 0.5)
    return m


# ---------------------------------------------------------------- scene helpers
class Ctx:
    col = None
    root = None


def begin_model(col_name, offset=(0, 0, 0)):
    """Fresh collection + root empty; everything made after this is parented to the root."""
    col = bpy.data.collections.get(col_name)
    if col is None:
        col = bpy.data.collections.new(col_name)
        bpy.context.scene.collection.children.link(col)
    for o in list(col.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)
    root = bpy.data.objects.new(col_name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 2.0
    col.objects.link(root)
    Ctx.col, Ctx.root = col, root
    return root


def end_model(offset=(0, 0, 0)):
    Ctx.root.location = offset


def _link(o, loc=(0, 0, 0), rot=(0, 0, 0)):
    Ctx.col.objects.link(o)
    o.parent = Ctx.root
    o.location = loc
    o.rotation_euler = rot
    return o


def mesh_obj(name, bm, mats, loc=(0, 0, 0), rot=(0, 0, 0), smooth=35, mesh=None):
    if mesh is None:
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        for m in mats if isinstance(mats, (list, tuple)) else [mats]:
            mesh.materials.append(m)
        if smooth:
            mesh.shade_smooth()
            mesh.set_sharp_from_angle(angle=math.radians(smooth))
    return _link(bpy.data.objects.new(name, mesh), loc, rot)


def bevel(o, w, seg=3, angle=40, limit="ANGLE", profile=0.5):
    m = o.modifiers.new("Bevel", "BEVEL")
    m.width = w
    m.segments = seg
    m.limit_method = limit
    m.angle_limit = math.radians(angle)
    m.profile = profile
    m.use_clamp_overlap = True
    return m


def wnormal(o):
    m = o.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    m.keep_sharp = True
    m.weight = 50
    return m


def subsurf(o, lv=2):
    m = o.modifiers.new("Subdivision", "SUBSURF")
    m.levels = lv
    m.render_levels = lv
    return m


# ---------------------------------------------------------------- 2D helpers
def chaikin(pts, it=3, closed=True):
    for _ in range(it):
        out = []
        n = len(pts)
        rng = range(n) if closed else range(n - 1)
        if not closed:
            out.append(pts[0])
        for i in rng:
            a, b = pts[i], pts[(i + 1) % n]
            out.append((0.75 * a[0] + 0.25 * b[0], 0.75 * a[1] + 0.25 * b[1]))
            out.append((0.25 * a[0] + 0.75 * b[0], 0.25 * a[1] + 0.75 * b[1]))
        if not closed:
            out.append(pts[-1])
        pts = out
    return pts


def area2(pts):
    return sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
               for i in range(len(pts))) * 0.5


def offset_poly(pts, d):
    """Offset a closed polygon inward by d (outward if d < 0)."""
    s = 1.0 if area2(pts) > 0 else -1.0
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2 = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[(i + 1) % n])
        e1 = (p1 - p0).normalized()
        e2 = (p2 - p1).normalized()
        n1 = Vector((-e1.y, e1.x)) * s
        n2 = Vector((-e2.y, e2.x)) * s
        nn = (n1 + n2)
        if nn.length < 1e-6:
            nn = n1
        nn.normalize()
        k = max(0.4, nn.dot(n1))
        q = p1 + nn * (d / k)
        out.append((q.x, q.y))
    return out


def rrect(x0, x1, y0, y1, r, n=6):
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


# ---------------------------------------------------------------- 3D primitives
def prism(name, pts, plane, a0, a1, mats, bev=None, loc=(0, 0, 0), rot=(0, 0, 0), smooth=35):
    """Extrude a closed 2D outline. plane 'YZ' extrudes along X, 'XZ' along Y, 'XY' along Z."""
    def P(u, v, a):
        if plane == "YZ":
            return (a, u, v)
        if plane == "XZ":
            return (u, a, v)
        return (u, v, a)
    bm = bmesh.new()
    lo = [bm.verts.new(P(u, v, a0)) for u, v in pts]
    hi = [bm.verts.new(P(u, v, a1)) for u, v in pts]
    n = len(pts)
    if n > 4:
        # concave outlines: scanfill each cap from its boundary edges (n-gon caps fill wrongly)
        nrm = Vector(P(0, 0, 1)) - Vector(P(0, 0, 0))
        for ring in (lo, hi):
            edges = [bm.edges.new((ring[i], ring[(i + 1) % n])) for i in range(n)]
            bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges, normal=nrm)
    else:
        bm.faces.new(lo)
        bm.faces.new(hi)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = mesh_obj(name, bm, mats, loc, rot, smooth)
    if bev:
        bevel(o, *bev) if isinstance(bev, tuple) else bevel(o, bev)
        wnormal(o)
    return o


def box(name, x0, x1, y0, y1, z0, z1, mats, bev=None, r=0.0):
    pts = rrect(x0, x1, y0, y1, r) if r > 0 else [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    return prism(name, pts, "XY", z0, z1, mats, bev)


def lathe_bm(prof, seg=48, bm=None, matrix=None):
    """Revolve a (r, z[, knurl]) profile around local Z. knurl > 0 makes ridged rings."""
    bm = bm or bmesh.new()
    rings = []
    for p in prof:
        r, z = p[0], p[1]
        k = p[2] if len(p) > 2 else 0.0
        if r < 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
            continue
        ring = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            rr = r * (1 + (k if i % 2 == 0 else -k))
            ring.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            for i in range(seg):
                bm.faces.new((a[0], b[i], b[(i + 1) % seg]))
        elif len(b) == 1:
            for i in range(seg):
                bm.faces.new((a[(i + 1) % seg], a[i], b[0]))
        else:
            for i in range(seg):
                bm.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
    if matrix is not None:
        verts = [v for r in rings for v in r]
        bmesh.ops.transform(bm, matrix=matrix, verts=verts)
    return bm


def lathe(name, prof, mats, loc=(0, 0, 0), axis="Z", seg=48, smooth=30):
    bm = lathe_bm(prof, seg)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rot = {"Z": (0, 0, 0), "X+": (0, math.pi / 2, 0), "X-": (0, -math.pi / 2, 0),
           "Y+": (-math.pi / 2, 0, 0), "Y-": (math.pi / 2, 0, 0)}[axis]
    o = mesh_obj(name, bm, mats, loc, rot, smooth)
    wnormal(o)
    return o


def tube(name, pts, r, mats, closed=False, kind="POLY", res=10, radii=None, bres=4):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = r
    cu.bevel_resolution = bres
    cu.use_fill_caps = True
    cu.resolution_u = res
    if kind == "BEZIER":
        sp = cu.splines.new("BEZIER")
        sp.bezier_points.add(len(pts) - 1)
        for i, (bp, p) in enumerate(zip(sp.bezier_points, pts)):
            bp.co = p
            bp.handle_left_type = bp.handle_right_type = "AUTO"
            if radii:
                bp.radius = radii[i]
    else:
        sp = cu.splines.new("POLY")
        sp.points.add(len(pts) - 1)
        for i, (pt, p) in enumerate(zip(sp.points, pts)):
            pt.co = (*p, 1.0)
            if radii:
                pt.radius = radii[i]
    sp.use_cyclic_u = closed
    sp.use_smooth = True
    for m in mats if isinstance(mats, (list, tuple)) else [mats]:
        cu.materials.append(m)
    return _link(bpy.data.objects.new(name, cu))


def surface_strip(name, prof_yz, x0, x1, mats, nx=12, thick=0.06, loc=(0, 0, 0)):
    """Sheet swept along X from a (y, z) polyline (paper, pages)."""
    bm = bmesh.new()
    grid = []
    for i in range(nx + 1):
        x = x0 + (x1 - x0) * i / nx
        grid.append([bm.verts.new((x, y, z)) for y, z in prof_yz])
    for i in range(nx):
        for j in range(len(prof_yz) - 1):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    o = mesh_obj(name, bm, mats, loc, smooth=60)
    s = o.modifiers.new("Solidify", "SOLIDIFY")
    s.thickness = thick
    s.offset = 0
    return o


# =================================================================== 01 TYPEWRITER
# Fits `Typewriter stairs - printed command` in the blockout, shifted so the body centre
# (world y -38) is the origin: front y -14.6, back y +14.3. Gameplay-fixed parts kept exact:
#   keys 23 = rows 5-4-5-4-5, x step 3 m, row step 2.8 m (local y -11.6 + 2.8 r), key rim
#   bottom z 0.02 + 0.32 r, cap diameter 2.8, inner passage 16 wide, sill top z 1.6, roof z 6.8.
TW_ROWS = (5, 4, 5, 4, 5)


def tw_key_y(r):
    return -11.6 + 2.8 * r


def tw_key_zb(r):
    return 0.02 + 0.32 * r


TW_SIDE = chaikin([(-14.6, 0.0), (-14.6, 0.95), (-14.4, 1.5), (-13.7, 1.78), (-1.4, 2.75),
                   (-0.2, 3.2), (0.4, 4.4), (0.6, 7.0), (0.9, 10.0), (1.3, 11.9), (2.2, 12.9),
                   (3.8, 13.3), (12.4, 13.4), (13.9, 13.1), (14.3, 12.0), (14.3, 0.0),
                   (13.9, -0.45), (-14.2, -0.45)], 3)
TW_PANEL = chaikin([(-13.1, 0.30), (-13.1, 0.95), (-12.6, 1.18), (-1.8, 2.05), (-0.9, 2.5),
                    (-0.4, 3.8), (-0.15, 7.0), (0.2, 10.0), (0.7, 11.6), (1.8, 12.35),
                    (12.2, 12.45), (13.2, 12.0), (13.25, 0.30)], 3)
TW_WINDOW = chaikin([(-7.0, 7.95), (7.0, 7.95), (7.0, 10.1), (6.3, 10.85), (4.2, 11.2), (0.0, 11.4),
                     (-4.2, 11.2), (-6.3, 10.85), (-7.0, 10.1)], 3)
TW_CARRIAGE_DZ = 4.4  # carriage sits on the raised hood (top z 12.9)


def tw_arch_half():
    """Right half of the tunnel opening's top edge, x from 8 to 0 (x, z)."""
    return chaikin([(8.0, 4.7), (8.0, 5.35), (7.55, 5.95), (6.6, 6.12), (5.6, 6.22),
                    (5.0, 6.62), (4.1, 6.92), (0.0, 6.98)], 3, closed=False)


def build_typewriter(offset=(0, 0, 0)):
    begin_model("E5_01_Typewriter")
    EN, BR, BD = M("E5_BlackEnamel"), M("E5_Brass"), M("E5_BrassDark")
    IV, PA, RU, ST = M("E5_Ivory"), M("E5_Paper"), M("E5_Rubber"), M("E5_DarkSteel")

    # --- base plinth, brass band, feet
    prism("TW base plinth", rrect(-12.0, 12.0, -15.0, 14.8, 1.6, 8), "XY", -0.95, -0.40, EN, (0.14, 3))
    tube("TW base brass band", [(x, y, -0.43) for x, y in rrect(-12.02, 12.02, -15.02, 14.82, 1.6, 8)],
         0.09, BR, closed=True)
    foot = [(0, -1.25), (0.95, -1.25), (1.12, -1.20), (1.2, -1.10), (1.2, -1.02), (1.1, -0.98),
            (0.9, -0.96), (0.82, -0.92), (0.85, -0.88), (0, -0.88)]
    for sx in (-1, 1):
        for y in (-12.8, 12.6):
            lathe(f"TW foot {sx:+d} {y:+.0f}", foot, BR, (sx * 9.8, y, 0))

    # --- side housings (cast enamel), raised side panel, brass panel line
    for sx in (-1, 1):
        a0, a1 = (8.0, 11.6) if sx > 0 else (-11.6, -8.0)
        prism(f"TW side housing {sx:+d}", TW_SIDE, "YZ", a0, a1, EN, (0.42, 4, 40))
        face = 11.6 * sx
        inner = offset_poly(TW_PANEL, 0.32)
        prism(f"TW side panel {sx:+d}", inner, "YZ", face - 0.05 * sx, face + 0.10 * sx, EN, (0.05, 2, 40))
        tube(f"TW side panel brass line {sx:+d}", [(face + 0.02 * sx, y, z) for y, z in TW_PANEL],
             0.075, BR, closed=True)
        # polished brass rail along the cheek top, beside the keys
        rail = [(y, z) for y, z in TW_SIDE if -14.35 < y < -0.6 and z > 1.0]
        rail.sort()
        tube(f"TW cheek rail {sx:+d}", [(8.12 * sx, y, z - 0.10) for y, z in rail], 0.16, BR)
        # front scroll boss on each cheek nose
        lathe(f"TW cheek boss {sx:+d}", [(0, 0), (0.55, 0), (0.62, 0.06), (0.62, 0.16), (0.5, 0.22), (0, 0.24)],
              BR, (9.8 * sx, -14.6, 0.75), "Y-")

    # --- keyboard tiers (stairs under the key rows) + front brass rim
    for r in range(5):
        y0 = tw_key_y(r) - 1.55
        prism(f"TW key tier {r}", rrect(-8.0, 8.0, y0, 1.2, 0.15, 2), "XY", -0.45, tw_key_zb(r) - 0.26, EN,
              (0.07, 2, 40))
        tube(f"TW key tier lip {r}", [(-7.9, y0 - 0.02, tw_key_zb(r) - 0.30), (7.9, y0 - 0.02, tw_key_zb(r) - 0.30)],
             0.05, BD)
    box("TW front brass rim", -8.0, 8.0, -13.32, -13.1, -0.42, -0.24, BR, (0.04, 2))

    # --- keys: brass cup on a stem with collar, ivory face left blank (letters come from the game)
    zs = -0.33
    key_prof = [(0, zs), (0.66, zs), (0.68, zs + 0.04), (0.64, zs + 0.08), (0.42, zs + 0.11),
                (0.36, zs + 0.16), (0.36, -0.10), (0.46, -0.06), (0.58, -0.02), (1.20, 0.0),
                (1.34, 0.04), (1.40, 0.11), (1.40, 0.24), (1.37, 0.30), (1.30, 0.32), (1.24, 0.30),
                (1.20, 0.25), (1.18, 0.23), (0, 0.23)]
    face_prof = [(0, 0.23), (1.18, 0.23), (1.18, 0.265), (1.15, 0.285), (1.08, 0.293), (0, 0.293)]
    key_bm = lathe_bm(key_prof, 56)
    bmesh.ops.remove_doubles(key_bm, verts=key_bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(key_bm, faces=key_bm.faces)
    face_bm = lathe_bm(face_prof, 56)
    bmesh.ops.remove_doubles(face_bm, verts=face_bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(face_bm, faces=face_bm.faces)
    key_me = bpy.data.meshes.new("TW key brass")
    key_bm.to_mesh(key_me); key_bm.free()
    key_me.materials.append(BR)
    face_me = bpy.data.meshes.new("TW key ivory face")
    face_bm.to_mesh(face_me)
    face_bm.free()
    face_me.materials.append(IV)
    for me in (key_me, face_me):
        me.shade_smooth()
        me.set_sharp_from_angle(angle=math.radians(30))
    # stems reach down to each row's tier: one stem mesh per row height
    count = 0
    for r, n in enumerate(TW_ROWS):
        xs = [-6, -3, 0, 3, 6] if n == 5 else [-4.5, -1.5, 1.5, 4.5]
        for c, x in enumerate(xs):
            loc = (x, tw_key_y(r), tw_key_zb(r))
            k = mesh_obj(f"Key {r}-{c} brass", None, None, loc, mesh=key_me)
            wnormal(k)
            mesh_obj(f"Key {r}-{c} face", None, None, loc, mesh=face_me)
            count += 1
    assert count == 23

    # --- tunnel: sill, descent floor (gameplay), front fascia with arch, hood
    box("TW inner sill", -8.0, 8.0, 1.0, 5.0, -0.4, 1.6, EN, (0.08, 2))
    prism("TW interior descent", [(5.0, -0.4), (5.0, 1.6), (11.0, -0.15), (11.0, -0.4)], "YZ", -8.0, 8.0, EN)
    half = tw_arch_half()
    arch = half + [(-x, z) for x, z in reversed(half[:-1])]
    # one loop: left top, left bottom, along the arch from left to right, right bottom, right top
    FT = 12.35  # fascia top
    fascia = [(-8.05, FT), (-8.05, 4.7)] + list(reversed(arch)) + [(8.05, 4.7), (8.05, FT)]
    fa = prism("TW front fascia", fascia, "XZ", 1.5, 2.9, EN)
    # type-basket window cut through the fascia (boolean), cutter kept hidden for edits
    cut = prism("TW window cutter", TW_WINDOW, "XZ", 1.0, 3.4, EN, smooth=0)
    cut.display_type = "WIRE"
    cut.hide_render = True
    cut.hide_set(True)
    b = fa.modifiers.new("Window", "BOOLEAN")
    b.operation = "DIFFERENCE"
    b.object = cut
    b.solver = "EXACT"
    bevel(fa, 0.16, 3, 40)
    wnormal(fa)
    tube("TW arch brass trim", [(x, 1.47, z - 0.02) for x, z in arch], 0.12, BR)
    for sx in (-1, 1):
        tube(f"TW arch jamb trim {sx:+d}", [(7.92 * sx, 1.47, 4.7), (7.92 * sx, 1.47, 1.65)], 0.12, BR)
    tube("TW window brass trim", [(x, 1.44, z) for x, z in offset_poly(TW_WINDOW, -0.1)], 0.13, BR, closed=True)
    tube("TW fascia top trim", [(-8.0, 1.55, FT - 0.08), (8.0, 1.55, FT - 0.08)], 0.09, BR)
    # hood: tunnel roof slab, cavity back wall, top deck (cavity behind the window is open)
    box("TW tunnel roof", -8.05, 8.05, 2.2, 9.6, 6.8, 7.9, EN, (0.08, 2))
    box("TW hood back block", -8.05, 8.05, 9.6, 14.3, 6.8, 12.9, EN, (0.35, 4))
    box("TW hood top deck", -8.05, 8.05, 2.2, 9.7, 11.55, 12.9, EN, (0.35, 4))

    # --- type basket: fan of type bars on a cone, brass segment ring and rest ring
    bars = bmesh.new()
    C = Vector((0, 8.6))
    N = 27
    for i in range(N):
        a = math.radians(-76 + 152 * i / (N - 1))
        d = Vector((math.sin(a), -math.cos(a)))
        b = C + d * 5.6
        t = C + d * 1.7
        B = Vector((b.x, b.y, 8.05))
        T = Vector((t.x, t.y, 10.95))
        ax = (T - B)
        L = ax.length
        zax = ax.normalized()
        xax = Vector((-d.y, d.x, 0))  # tangent of the fan
        yax = zax.cross(xax).normalized()
        mat = Matrix((xax, yax, zax)).transposed().to_4x4()
        mat.translation = (B + T) * 0.5
        ret = bmesh.ops.create_cube(bars, size=1.0)
        bmesh.ops.scale(bars, vec=(0.26, 0.12, L), verts=ret["verts"])
        bmesh.ops.transform(bars, matrix=mat, verts=ret["verts"])
        # type slug at the top end
        ret = bmesh.ops.create_cube(bars, size=1.0)
        bmesh.ops.scale(bars, vec=(0.34, 0.22, 0.32), verts=ret["verts"])
        m2 = mat.copy()
        m2.translation = T - zax * 0.12
        bmesh.ops.transform(bars, matrix=m2, verts=ret["verts"])
    o = mesh_obj("TW type bars", bars, ST, smooth=30)
    bevel(o, 0.035, 2, 60)
    wnormal(o)
    ring = [(C.x + 5.6 * math.sin(math.radians(a)), C.y - 5.6 * math.cos(math.radians(a)), 8.0)
            for a in range(-80, 81, 4)]
    tube("TW type bar rest", ring, 0.13, BR)
    seg = [(C.x + 1.75 * math.sin(math.radians(a)), C.y - 1.75 * math.cos(math.radians(a)), 11.05)
           for a in range(-90, 91, 6)]
    tube("TW type segment", seg, 0.22, BR)
    box("TW type guide", -0.45, 0.45, 6.6, 6.9, 10.7, 11.5, BR, (0.04, 2))
    # dark ribbon vibrator plate behind the type guide
    box("TW ribbon vibrator", -1.2, 1.2, 6.9, 7.05, 10.5, 11.5, ST, (0.03, 2))

    # --- ribbon spool covers on the hood
    for sx in (-1, 1):
        lathe(f"TW spool cover {sx:+d}", [(0, 8.4), (1.45, 8.4), (1.5, 8.5), (1.42, 8.62), (1.1, 8.72),
                                           (0.45, 8.78), (0.42, 8.9), (0.28, 8.98), (0, 8.98)],
              BR, (5.4 * sx, 4.6, 4.5))

    # --- carriage: end plates, rails, platen, knobs, bail, card bar, paper, return lever
    # (laid out at the blockout platen height, then lifted by TW_CARRIAGE_DZ onto the hood)
    before = set(Ctx.col.objects)
    CY, CZ, CR = 9.0, 9.85, 1.22
    for sx in (-1, 1):
        a0, a1 = (10.15, 10.6) if sx > 0 else (-10.6, -10.15)
        prism(f"TW carriage end plate {sx:+d}", rrect(6.7, 11.6, 8.55, 11.3, 0.9, 6), "YZ", a0, a1, EN, (0.08, 2))
        lathe(f"TW platen bearing {sx:+d}", [(0, 0), (0.75, 0), (0.8, 0.05), (0.8, 0.2), (0.6, 0.28), (0, 0.3)],
              BR, (10.6 * sx, CY, CZ), "X+" if sx > 0 else "X-")
        # knob: shaft, flange, knurled grip, cap
        knob = [(0, 0.0), (0.42, 0.0), (0.42, 0.32), (1.05, 0.36), (1.28, 0.46), (1.32, 0.56),
                (1.62, 0.62), (1.70, 0.72), (1.70, 0.80, 0.025), (1.70, 1.36, 0.025), (1.70, 1.42),
                (1.58, 1.50), (1.40, 1.54), (1.46, 1.60), (1.50, 1.70), (1.50, 1.74, 0.02),
                (1.50, 1.96, 0.02), (1.44, 2.02), (1.20, 2.08), (0.55, 2.12), (0.30, 2.22), (0, 2.24)]
        lathe(f"TW platen knob {sx:+d}", knob, BR, (10.85 * sx, CY, CZ), "X+" if sx > 0 else "X-", seg=72)
    lathe("TW platen roller", [(0, -10.15), (CR - 0.08, -10.15), (CR, -10.07), (CR, 10.07),
                               (CR - 0.08, 10.15), (0, 10.15)], RU, (0, CY, CZ), "X+", seg=64)
    for sx in (-1, 1):
        lathe(f"TW platen end flange {sx:+d}", [(0, 0), (CR + 0.12, 0), (CR + 0.15, 0.06), (CR + 0.12, 0.2), (0, 0.2)],
              BR, (9.95 * sx, CY, CZ), "X+" if sx > 0 else "X-")
    tube("TW carriage rail front", [(-11.2, 6.55, 8.88), (11.2, 6.55, 8.88)], 0.2, BR)
    tube("TW carriage rail back", [(-11.2, 11.95, 8.95), (11.2, 11.95, 8.95)], 0.24, BR)
    tube("TW paper support rod", [(-10.2, 10.75, 11.55), (10.2, 10.75, 11.55)], 0.13, BR)
    # paper bail with two little rubber rollers
    tube("TW paper bail", [(-10.1, 7.55, 10.8), (10.1, 7.55, 10.8)], 0.11, BR)
    for x in (-3.2, 3.2):
        lathe(f"TW bail roller {x:+.0f}", [(0, -0.35), (0.24, -0.35), (0.26, -0.3), (0.26, 0.3), (0.24, 0.35), (0, 0.35)],
              RU, (x, 7.55, 10.8), "X+", seg=24)
    # card holder bar in front of the paper with screw heads
    box("TW card holder", -7.6, 7.6, 8.2, 8.42, 11.05, 11.45, BR, (0.05, 2))
    for x in (-6.8, 6.8):
        lathe(f"TW card holder screw {x:+.0f}", [(0, 0), (0.18, 0), (0.18, 0.05), (0.1, 0.09), (0, 0.1)], BD,
              (x, 8.2, 11.25), "Y-", seg=16)
    # paper: wraps the platen then rises, curling back at the top
    paper = [(CY + (CR + 0.03) * -math.sin(math.radians(a)), CZ + (CR + 0.03) * math.cos(math.radians(a)))
             for a in (120, 90, 60, 35, 15)]
    paper += [(8.62, 11.6), (8.6, 12.4), (8.7, 13.3), (8.95, 14.2), (9.3, 15.0), (9.8, 15.6),
              (10.4, 16.0), (10.9, 16.12)]
    surface_strip("TW paper", paper, -6.8, 6.8, PA, nx=16, thick=0.07)
    # carriage return lever (left): sweeps forward and down, ball end
    lev = [(-10.45, 8.0, 11.0), (-10.9, 6.8, 11.55), (-11.8, 4.9, 11.45), (-12.9, 3.0, 10.6),
           (-13.55, 1.7, 9.4), (-13.75, 1.2, 8.6)]
    tube("TW return lever", lev, 0.24, BR, kind="BEZIER", res=12, radii=[1.1, 1.0, 0.95, 0.9, 0.85, 0.8])
    lathe("TW return lever ball", [(0, -0.5)] + [(0.5 * math.sin(math.radians(a)), -0.5 * math.cos(math.radians(a)))
                                                 for a in range(15, 180, 15)] + [(0, 0.5)], BR, (-13.8, 1.05, 8.3))
    lathe("TW return lever pivot", [(0, 0), (0.5, 0), (0.55, 0.06), (0.55, 0.2), (0.4, 0.28), (0, 0.3)], BR,
          (-10.6, 8.0, 11.0), "X-")
    # margin stops / line-space wheel on the right plate
    lathe("TW line space wheel", [(0, 0), (0.75, 0), (0.75, 0.25, 0.04), (0.6, 0.32), (0, 0.34)], BR,
          (10.6, 7.3, 10.6), "X+", seg=40)
    for o in Ctx.col.objects:
        if o not in before:
            o.location.z += TW_CARRIAGE_DZ

    end_model(offset)
    return Ctx.root


# =================================================================== 02 ROUND KEY ON A SPRING
# Stand-alone key from the sheet: cap diameter 2.8 m like the typewriter keys (blockout key rim),
# total height ~3.7 m (sheet ratio 1.32 x diameter). Origin = centre of the base underside.
def build_round_key(offset=(45, 0, 0)):
    begin_model("E5_02_RoundKey")
    BR, BD, IV, ST = M("E5_Brass"), M("E5_BrassDark"), M("E5_Ivory"), M("E5_DarkSteel")

    # base: flat plate with a raised dome, rim lip and four screw heads
    lathe("RK base", [(0, 0), (1.20, 0), (1.25, 0.03), (1.27, 0.09), (1.27, 0.15), (1.23, 0.20),
                      (1.12, 0.22), (0.98, 0.23), (0.90, 0.26), (0.82, 0.33), (0.72, 0.40),
                      (0.64, 0.44), (0.62, 0.48), (0, 0.48)], BR, seg=72)
    head = [(0, 0), (0.10, 0), (0.10, 0.025), (0.085, 0.05), (0.05, 0.068), (0, 0.072)]
    for i in range(4):
        a = math.radians(45 + 90 * i)
        lathe(f"RK base screw {i}", head, BD, (1.06 * math.cos(a), 1.06 * math.sin(a), 0.218), seg=16)
    # lower collar the spring seats in
    lathe("RK lower collar", [(0, 0.46), (0.60, 0.46), (0.64, 0.50), (0.64, 0.58), (0.58, 0.62),
                              (0.58, 0.66), (0.58, 0.72), (0.52, 0.76), (0, 0.76)], BR, seg=56)
    # guide rod inside the spring
    lathe("RK guide rod", [(0, 0.7), (0.22, 0.7), (0.22, 2.30), (0, 2.30)], ST, seg=32)

    # spring: Screw modifier sweeping a round wire section around Z
    turns, pitch, coil_r, wire_r = 6, 0.215, 0.42, 0.088
    bm = bmesh.new()
    vs = [bm.verts.new((coil_r + wire_r * math.cos(t), 0, wire_r * math.sin(t)))
          for t in (2 * math.pi * k / 12 for k in range(12))]
    for k in range(12):
        bm.edges.new((vs[k], vs[(k + 1) % 12]))
    sp = mesh_obj("RK spring", bm, BD, loc=(0, 0, 0.76 + wire_r), smooth=0)
    scr = sp.modifiers.new("Screw", "SCREW")
    scr.axis = "Z"
    scr.angle = 2 * math.pi
    scr.steps = 36
    scr.render_steps = 36
    scr.screw_offset = pitch
    scr.iterations = turns
    scr.use_smooth_shade = True
    scr.use_normal_calculate = True
    wnormal(sp)

    # upper collar and neck flaring into the cap
    lathe("RK upper collar", [(0, 2.12), (0.54, 2.12), (0.60, 2.16), (0.60, 2.26), (0.52, 2.30),
                              (0.55, 2.34), (0.55, 2.40), (0.44, 2.46), (0.38, 2.56), (0.38, 2.64),
                              (0.55, 2.72), (0, 2.72)], BR, seg=56)
    # cap: thick rounded brass cup, scribed line, inner lip; ivory face slightly dished, blank
    lathe("RK cap", [(0, 2.70), (0.55, 2.70), (0.95, 2.74), (1.22, 2.83), (1.36, 2.95),
                     (1.42, 3.08), (1.43, 3.18), (1.415, 3.20), (1.43, 3.22), (1.44, 3.50),
                     (1.41, 3.62), (1.34, 3.68), (1.26, 3.67), (1.21, 3.62), (1.18, 3.54),
                     (1.16, 3.50), (0, 3.50)], BR, seg=72)
    lathe("RK ivory face", [(0, 3.50), (1.16, 3.50), (1.16, 3.57), (1.13, 3.60), (1.0, 3.605),
                            (0.6, 3.592), (0, 3.588)], IV, seg=72)

    end_model(offset)
    return Ctx.root


# =================================================================== 03 STAMP PRESS
# Blockout `Command stamp press - warning then strike`: square brass head 5.8 x 5.8, 1.6 high,
# striking a 6 x 6 warning area; the moving part is the `Stamp head - edit timing and impulse`
# empty. Here the head is round (sheet) with the same 5.8 m footprint and sits under an empty of
# that name, raised SP_LIFT above its strike position for the still. Origin = head centre on the
# table top. The column stands to the +X side; the stamp at x -3 would use a mirrored copy.
SP_BASE_TOP = 0.62
SP_LIFT = 1.4


def sp_section(name, w, h, r):
    """Rounded-rectangle 2D curve used as a bevel object (arm cross-section)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "2D"
    sp = cu.splines.new("POLY")
    pts = rrect(-w / 2, w / 2, -h / 2, h / 2, r, 4)
    sp.points.add(len(pts) - 1)
    for pt, (x, y) in zip(sp.points, pts):
        pt.co = (x, y, 0, 1)
    sp.use_cyclic_u = True
    o = _link(bpy.data.objects.new(name, cu))
    o.hide_render = True
    o.hide_set(True)
    return o


def build_stamp_press(offset=(70, 0, 0)):
    begin_model("E5_03_StampPress")
    BR, BD, WA, WX, ST = M("E5_Brass"), M("E5_BrassDark"), M("E5_Walnut"), M("E5_RedWax"), M("E5_DarkSteel")
    BT = SP_BASE_TOP

    # --- wooden base: two-tier plinth, brass band, corner caps, front bosses
    prism("SP base lower", rrect(-3.4, 9.2, -3.4, 3.4, 0.45, 6), "XY", 0.0, 0.42, WA, (0.07, 2))
    prism("SP base upper", rrect(-3.1, 8.9, -3.1, 3.1, 0.30, 6), "XY", 0.40, BT, WA, (0.12, 4, 40, "ANGLE", 0.7))
    tube("SP base brass band", [(x, y, 0.035) for x, y in rrect(-3.42, 9.22, -3.42, 3.42, 0.45, 6)], 0.05, BR,
         closed=True)
    tube("SP base top trim", [(x, y, 0.42) for x, y in rrect(-3.36, 9.16, -3.36, 3.36, 0.43, 6)], 0.04, BR,
         closed=True)
    boss = [(0, 0), (0.24, 0), (0.24, 0.03), (0.20, 0.07), (0.12, 0.10), (0, 0.11)]
    for x in (-2.3, 3.0, 8.2):
        lathe(f"SP base boss {x:+.0f}", boss, BR, (x, -3.40, 0.22), "Y-", seg=24)
    for cx, cy in ((-3.4, -3.4), (9.2, -3.4), (-3.4, 3.4), (9.2, 3.4)):
        sx, sy = (1 if cx > 0 else -1), (1 if cy > 0 else -1)
        # L-shaped brass corner guard hugging each base corner
        pts = [(cx - sx * 0.0, cy), (cx - sx * 0.9, cy), (cx - sx * 0.9, cy - sy * 0.12),
               (cx - sx * 0.12, cy - sy * 0.12), (cx - sx * 0.12, cy - sy * 0.9), (cx, cy - sy * 0.9)]
        pts = [(x + sx * 0.04, y + sy * 0.04) for x, y in pts]
        prism(f"SP base corner {cx:+.0f} {cy:+.0f}", pts, "XY", 0.0, 0.44, BR, (0.025, 2))

    # --- column: walnut core between brass side bars, foot bracket with bolts, hinge knuckle
    box("SP column core", 5.95, 7.25, -0.85, 0.85, BT, 6.9, WA, (0.06, 2))
    for x0, x1 in ((5.6, 5.98), (7.22, 7.6)):
        box(f"SP column bar {x0:+.1f}", x0, x1, -1.0, 1.0, BT, 6.95, BR, (0.06, 2))
    box("SP column band", 5.55, 7.65, -1.05, 1.05, 2.0, 2.3, BR, (0.05, 2))
    prism("SP foot bracket", [(5.2, BT), (5.2, BT + 0.35), (5.6, BT + 1.05), (7.6, BT + 1.05),
                              (8.0, BT + 0.35), (8.0, BT)], "XZ", -1.25, 1.25, BR, (0.06, 2))
    bolt = [(0, 0), (0.16, 0), (0.16, 0.04), (0.12, 0.09), (0, 0.11)]
    for x in (5.55, 7.65):
        for y in (-0.8, 0.8):
            lathe(f"SP foot bolt {x:+.1f} {y:+.1f}", bolt, BD, (x, y, BT + 0.35), seg=16)
    for x in (5.9, 7.3):
        lathe(f"SP foot face bolt {x:+.1f}", bolt, BD, (x, -1.25, BT + 0.55), "Y-", seg=16)
    lathe("SP hinge knuckle", [(0, -1.1), (0.78, -1.1), (0.85, -1.03), (0.85, 1.03), (0.78, 1.1), (0, 1.1)],
          BR, (6.6, 0, 6.9), "Y+", seg=48)
    for sy in (-1, 1):
        lathe(f"SP hinge pin cap {sy:+d}", [(0, 0), (0.52, 0), (0.55, 0.05), (0.52, 0.16), (0.38, 0.24), (0, 0.27)],
              BR, (6.6, 1.1 * sy, 6.9), "Y+" if sy > 0 else "Y-", seg=40)

    # --- arm: brass beam (rounded-rectangle bevel object) from the hinge over to the guide collar
    head_z = BT + SP_LIFT
    sec = sp_section("SP arm section", 1.35, 1.15, 0.38)
    arm_pts = [(6.6, 0, 6.9), (6.0, 0, 8.2), (4.6, 0, 8.75), (3.2, 0, 8.25), (2.2, 0, head_z + 3.0),
               (1.0, 0, head_z + 2.75)]
    cu = bpy.data.curves.new("SP arm", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_mode = "OBJECT"
    cu.bevel_object = sec
    cu.use_fill_caps = True
    cu.resolution_u = 16
    spl = cu.splines.new("BEZIER")
    spl.bezier_points.add(len(arm_pts) - 1)
    for bp, p in zip(spl.bezier_points, arm_pts):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    cu.materials.append(BR)
    _link(bpy.data.objects.new("SP arm", cu))
    # guide collar the plunger slides through (fixed to the arm)
    gz = head_z + 1.85
    lathe("SP guide collar", [(0.6, gz), (1.32, gz), (1.38, gz + 0.06), (1.38, gz + 0.26), (1.22, gz + 0.32),
                              (1.18, gz + 0.36), (1.18, gz + 1.45), (1.22, gz + 1.5), (1.38, gz + 1.56),
                              (1.38, gz + 1.76), (1.32, gz + 1.82), (0.6, gz + 1.82)], BR, seg=64)

    # --- moving head (strikes down by SP_LIFT): red rubber pad, brass head, plunger, walnut knob
    head = bpy.data.objects.new("Stamp head - edit timing and impulse", None)
    head.empty_display_type = "SINGLE_ARROW"
    head.empty_display_size = 3
    _link(head, (0, 0, head_z))
    moving = []
    moving.append(lathe("SP rubber pad", [(0, 0), (2.62, 0), (2.68, 0.04), (2.70, 0.30), (2.6, 0.36), (0, 0.36)],
                        WX, seg=72))
    moving.append(lathe("SP brass head", [(0, 0.34), (2.78, 0.34), (2.88, 0.40), (2.92, 0.50), (2.92, 0.60),
                                          (2.86, 0.64), (2.90, 0.70), (2.90, 1.20), (2.86, 1.26),
                                          (2.92, 1.32), (2.92, 1.42), (2.80, 1.52), (2.40, 1.58),
                                          (1.6, 1.62), (1.25, 1.70), (1.2, 1.80), (0, 1.80)], BR, seg=96))
    moving.append(lathe("SP plunger", [(0, 1.7), (0.58, 1.7), (0.58, 5.6), (0, 5.6)], BR, seg=32))
    moving.append(lathe("SP knob ferrule", [(0, 5.4), (0.95, 5.4), (1.0, 5.45), (1.0, 5.75), (0.92, 5.8),
                                            (0.96, 5.85), (0.96, 6.0), (0.85, 6.05), (0, 6.05)], BR, seg=48))
    moving.append(lathe("SP walnut knob", [(0, 6.0), (0.62, 6.0), (0.58, 6.25), (0.62, 6.5), (0.85, 6.8),
                                           (1.18, 7.2), (1.36, 7.7), (1.40, 8.1), (1.34, 8.5), (1.16, 8.85),
                                           (0.85, 9.08), (0.45, 9.2), (0, 9.23)], WA, seg=64))
    for o in moving:
        o.parent = head

    end_model(offset)
    return Ctx.root


# =================================================================== 04 OPEN BOOK (LIFTED COVER)
# Blockout `Closing royal folio -100` (local = world + (0, 100, 0)): walkable page block
# x -9..9, y 0..16, top z 0; back cover under it (z -0.8..-0.5); spine at x -9; the moving cover
# hangs under the empty `Horizontal closing cover` at (-9, 8, 0.35) and lies open flat over
# x -27..-9 (rotation 0). It closes by turning about Y. The still shows it at BK_POSE degrees,
# leaning over the pages like the sheet.
BK_POSE = 112.0
BK_COVER = 0.7  # leather board thickness (was 0.32; user: thicker covers)


def brass_corner(name, cx, cy, sx, sy, z0, z1, leg=2.3, w=0.5, mat=None):
    """Triangular-ish brass corner guard (L with a mitred inner edge) wrapping a board corner."""
    p = [(cx, cy), (cx - sx * leg, cy), (cx - sx * leg, cy - sy * w), (cx - sx * w * 1.6, cy - sy * w * 1.6),
         (cx - sx * w, cy - sy * leg), (cx, cy - sy * leg)]
    o = prism(name, p, "XY", z0, z1, mat, (0.05, 3, 40))
    return o


def book_cover_details(prefix, x0, x1, y0, y1, zc, th, BR, rivet_face):
    """Brass border line on the outer face plus rivets; zc = board centre z, th = thickness."""
    face = zc + rivet_face * (th / 2 + 0.01)
    tube(f"{prefix} border", [(x, y, face) for x, y in rrect(x0 + 1.1, x1 - 1.1, y0 + 1.1, y1 - 1.1, 0.5, 6)],
         0.07, BR, closed=True)
    tube(f"{prefix} border inner", [(x, y, face) for x, y in rrect(x0 + 1.45, x1 - 1.45, y0 + 1.45, y1 - 1.45, 0.3, 6)],
         0.035, BR, closed=True)


def build_open_book(offset=(100, 0, 0)):
    begin_model("E5_04_OpenBook")
    BR, LE, PA, IV = M("E5_Brass"), M("E5_RedLeather"), M("E5_Paper"), M("E5_Ivory")

    # --- back cover board, page block (Array of page sheets), spine roll
    # The sheet's book is chunky: page block 1.3 m (blockout 0.5) grows downward so the walkable
    # top stays at z 0; the folio desk top has to drop to z -1.62 in Unity.
    PB = 1.3
    CT = BK_COVER  # thick leather boards (user asked for thicker covers)
    prism("BK back cover", rrect(-9.2, 9.45, -0.45, 16.45, 0.45, 4), "XY", -PB - CT, -PB, LE, (0.22, 4))
    book_cover_details("BK back cover", -9.2, 9.45, -0.45, 16.45, -PB - CT / 2, CT, BR, -1)
    for cy, sy in ((-0.45, -1), (16.45, 1)):
        brass_corner(f"BK back corner {sy:+d}", 9.5, cy + 0.05 * sy, 1, sy, -PB - CT - 0.04, -PB + 0.04, mat=BR)
    # page outline with a slightly concave fore-edge (x = +9), like a real text block
    fore = [(9.0 - 0.28 * math.sin(math.pi * t / 12), 0.05 + 15.9 * t / 12) for t in range(13)]
    outline = [(-8.95, 0.05)] + fore + [(-8.95, 15.95)]
    sheet = prism("BK page sheets", outline, "XY", -PB, -PB + 0.098, PA, (0.04, 2, 40))
    arr = sheet.modifiers.new("Pages", "ARRAY")
    arr.count = int(round(PB / 0.1)) - 1
    arr.use_relative_offset = False
    arr.use_constant_offset = True
    arr.constant_offset_displace = (0, 0, 0.1)
    sheet.modifiers.move(len(sheet.modifiers) - 1, 0)
    # top page: single clean sheet that players stand on (top exactly z 0)
    prism("BK top page", outline, "XY", -0.1, 0.0, PA, (0.04, 2, 40))
    # spine: round leather roll with raised bands and brass end caps
    SR, SZ = (PB + CT + 0.25) / 2, (0.25 - PB - CT) / 2
    lathe("BK spine roll", [(0, -0.35), (SR - 0.04, -0.35), (SR, -0.3), (SR, 16.3), (SR - 0.04, 16.35), (0, 16.35)],
          LE, (-9.3, 0, SZ), "Y+", seg=48)
    for y in (2.2, 5.6, 10.4, 13.8):
        lathe(f"BK spine band {y:.0f}", [(0, -0.2), (SR + 0.04, -0.2), (SR + 0.09, -0.1), (SR + 0.09, 0.1),
                                          (SR + 0.04, 0.2), (0, 0.2)], LE, (-9.3, y, SZ), "Y+", seg=48)
    for y, ax in ((-0.35, "Y-"), (16.35, "Y+")):
        lathe(f"BK spine cap {y:+.0f}", [(0, 0), (SR + 0.04, 0), (SR + 0.08, 0.06), (SR + 0.06, 0.22),
                                         (SR - 0.15, 0.3), (0, 0.32)], BR, (-9.3, y, SZ), ax, seg=48)

    # --- moving cover (pivot = blockout `Horizontal closing cover`)
    piv = bpy.data.objects.new("Horizontal closing cover", None)
    piv.empty_display_type = "ARROWS"
    piv.empty_display_size = 3
    _link(piv, (-9.0, 8.0, 0.35))
    made_before = set(Ctx.col.objects)
    # The board thickens outward (away from the pages) so the inner face stays at +0.16 and the
    # closed cover still lands on the walkable pages (closed underside z = 0.35 - 0.16 - 0.18 > 0).
    TH, IN = CT, 0.16
    prism("BK moving cover board", rrect(-18.45, -0.3, -8.45, 8.45, 0.45, 4), "XY", IN - TH, IN, LE, (0.22, 4))
    book_cover_details("BK moving cover", -18.45, -0.3, -8.45, 8.45, IN - TH / 2, TH, BR, -1)
    for cy, sy in ((-8.45, -1), (8.45, 1)):
        brass_corner(f"BK moving corner {sy:+d}", -18.5, cy + 0.05 * sy, -1, sy, IN - TH - 0.04, IN + 0.04, mat=BR)
    # thin page stack on the inside of the cover (top when open flat)
    msh = prism("BK moving page sheets", rrect(-17.95, -0.7, -7.95, 7.95, 0.12, 2), "XY", IN, IN + 0.042,
                PA, (0.016, 2))
    marr = msh.modifiers.new("Pages", "ARRAY")
    marr.count = 4
    marr.use_relative_offset = False
    marr.use_constant_offset = True
    marr.constant_offset_displace = (0, 0, 0.045)
    msh.modifiers.move(len(msh.modifiers) - 1, 0)
    box("BK moving flyleaf", -17.95, -0.7, -7.95, 7.95, IN + 0.18, IN + 0.2, IV, (0.012, 2))
    # hinge: brass knuckles along the cover's spine edge, alternating with the spine roll bands
    for y in (-7.6, -3.0, 3.0, 7.6):
        lathe(f"BK hinge knuckle {y:+.0f}", [(0, -0.9), (0.5, -0.9), (0.55, -0.84), (0.55, 0.84), (0.5, 0.9), (0, 0.9)],
              BR, (0.0, y, 0.0), "Y+", seg=32)
        box(f"BK hinge leaf {y:+.0f}", -1.8, -0.05, y - 0.8, y + 0.8, IN - TH - 0.05, IN - TH + 0.02, BR, (0.03, 2))
    tube("BK hinge pin", [(0, -8.2, 0), (0, 8.2, 0)], 0.2, BR)
    for o in Ctx.col.objects:
        if o not in made_before and o.parent == Ctx.root:
            o.parent = piv
    piv.rotation_euler = (0, math.radians(BK_POSE), 0)

    end_model(offset)
    return Ctx.root


# ---------------------------------------------------------------- studio + still render
def _model_bounds(col):
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in col.all_objects:
        if o.type not in {"MESH", "CURVE"} or o.hide_render:
            continue
        oe = o.evaluated_get(dg)
        me = oe.to_mesh()
        mw = oe.matrix_world
        pts += [mw @ v.co for i, v in enumerate(me.vertices) if i % 7 == 0]
        oe.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi, pts


def _studio():
    col = bpy.data.collections.get("E5_Studio")
    if col is None:
        col = bpy.data.collections.new("E5_Studio")
        bpy.context.scene.collection.children.link(col)
    def get(name, make):
        o = bpy.data.objects.get(name)
        if o is None:
            o = make()
            col.objects.link(o)
        return o
    cam = get("E5_Cam", lambda: bpy.data.objects.new("E5_Cam", bpy.data.cameras.new("E5_Cam")))
    key = get("E5_Key", lambda: bpy.data.objects.new("E5_Key", bpy.data.lights.new("E5_Key", "AREA")))
    fill = get("E5_Fill", lambda: bpy.data.objects.new("E5_Fill", bpy.data.lights.new("E5_Fill", "AREA")))
    rim = get("E5_Rim", lambda: bpy.data.objects.new("E5_Rim", bpy.data.lights.new("E5_Rim", "AREA")))
    def mk_ground():
        me = bpy.data.meshes.new("E5_Ground")
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.0)
        bm.to_mesh(me); bm.free()
        return bpy.data.objects.new("E5_Ground", me)
    ground = get("E5_Ground", mk_ground)
    ground.is_shadow_catcher = True
    return cam, key, fill, rim, ground


def _look_at(o, target):
    d = (target - o.location)
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def render_still(name, model_col=None, view=(-0.62, -1.0, 0.62), res=(1600, 1200), samples=128, lens=70):
    """3/4 view like the sheet, white background with a soft contact shadow -> stills/e5/<name>.png."""
    sc = bpy.context.scene
    col = bpy.data.collections.get(model_col) if model_col else Ctx.col
    for c in bpy.context.scene.collection.children:
        if c.name.startswith("E5_") and c.name != "E5_Studio":
            c.hide_render = (c != col)
            c.hide_viewport = (c != col)
    cam, key, fill, rim, ground = _studio()
    lo, hi, pts = _model_bounds(col)
    c = (lo + hi) * 0.5
    size = (hi - lo).length

    cam.data.lens = lens
    cam.data.sensor_width = 36
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    d = Vector(view).normalized()
    fwd = -d
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd)
    th = 18 / lens * 0.86
    tv = th * res[1] / res[0]
    dist = 0.0
    for p in pts:
        q = p - c
        f = q.dot(fwd)
        dist = max(dist, -f + abs(q.dot(right)) / th, -f + abs(q.dot(up)) / tv)
    cam.location = c + d * dist
    _look_at(cam, c)

    ground.location = (c.x, c.y, lo.z)
    ground.scale = (size * 4, size * 4, 1)
    R = size
    key.location = c + Vector((-0.9, -0.55, 1.25)).normalized() * R * 1.6
    key.data.size = R * 0.6
    key.data.energy = 70 * R * R
    _look_at(key, c)
    fill.location = c + Vector((1.0, -0.8, 0.45)).normalized() * R * 1.8
    fill.data.size = R * 1.2
    fill.data.energy = 35 * R * R
    _look_at(fill, c)
    rim.location = c + Vector((0.5, 1.0, 0.8)).normalized() * R * 1.6
    rim.data.size = R * 0.8
    rim.data.energy = 70 * R * R
    _look_at(rim, c)

    w = sc.world or bpy.data.worlds.new("E5_World")
    sc.world = w
    try:
        w.use_nodes = True
    except Exception:
        pass
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.85, 0.78, 0.68, 1.0)
    bg.inputs[1].default_value = 0.15

    try:
        sc.render.engine = "CYCLES"
    except TypeError:
        pass
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.film_transparent = True
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    _enable_gpu(sc)

    os.makedirs(STILLS, exist_ok=True)
    tmp = os.path.join(bpy.app.tempdir, f"e5_{name}_rgba.png")
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True)

    img = bpy.data.images.load(tmp, check_existing=False)
    w_, h_ = img.size
    px = np.empty(w_ * h_ * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(-1, 4)
    a = px[:, 3:4]
    px[:, :3] = px[:, :3] * a + (1.0 - a)
    px[:, 3] = 1.0
    out = bpy.data.images.new(f"e5_{name}", w_, h_, alpha=False)
    out.pixels.foreach_set(px.ravel())
    path = os.path.join(STILLS, f"{name}.png")
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(img)
    bpy.data.images.remove(out)
    return path


def _enable_gpu(sc):
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for t in ("OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"):
            try:
                prefs.compute_device_type = t
            except TypeError:
                continue
            prefs.get_devices()
            devs = [d for d in prefs.devices if d.type == t]
            if devs:
                for d in prefs.devices:
                    d.use = d.type in (t, "CPU") and d.type == t
                sc.cycles.device = "GPU"
                return t
    except Exception:
        pass
    sc.cycles.device = "CPU"
    return "CPU"


def save_blend():
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)


BUILDERS = {
    "01_typewriter": build_typewriter,
    "02_round_key": build_round_key,
    "03_stamp_press": build_stamp_press,
    "04_open_book": build_open_book,
}
