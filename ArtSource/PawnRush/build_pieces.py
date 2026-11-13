"""Realistic chess pieces for the Pawn Rush mini-games (승규 10-22: "모든 기물 다 실제 기물같이, 고퀄리티로").

blender --background --factory-startup --python build_pieces.py -- <out_dir>
Staunton tournament pattern: every profile is a smooth spline (sharp only where a real turned piece has a
crisp edge), turned on a 64-sided lathe; the rook's six crenels and the bishop's mitre slit are cut; the
king's cross pattee is a rounded extrusion; the knight's head is sculpted from horse volumes (neck, chest,
jaw, muzzle, poll, ears, mane) fused into one carved surface with eyes, nostrils and mouth cut in.
Every piece: height 1, base on z=0, centred, one material slot "PieceBody", cylindrical UVs (u round,
v up: wood grain runs up the piece). The knight looks along Blender +X. PR_PieceRing: torus radius 1.
Unity: Resources/Pieces/<name>.fbx, read by MapLook.PieceMesh. BASE_R = each piece's foot radius.
"""
import math
import os
import sys

import bmesh
import numpy as np
import bpy
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
SEGS = 56
BASE_R = {"Pawn": 0.29, "Rook": 0.30, "Bishop": 0.23, "Queen": 0.23, "King": 0.225, "Knight": 0.26}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def body_mat():
    m = bpy.data.materials.get("PieceBody")
    if m is None:
        m = bpy.data.materials.new("PieceBody")
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (0.92, 0.86, 0.74, 1)
        b.inputs["Roughness"].default_value = 0.28
    return m


# ---------------------------------------------------------------- profile tools

def catmull(points, samples=3):
    """Smooth curve through `points` (uniform Catmull-Rom), endpoints included."""
    if len(points) < 3:
        return list(points)
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = (Vector(p) for p in pts[i - 1:i + 3])
        for s in range(samples):
            t = s / samples
            t2, t3 = t * t, t * t * t
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            out.append((max(0.0, q.x), q.y))
    out.append(points[-1])
    return out


def profile(*runs):
    """Smooth runs joined at crisp corners (each run's last point is the next run's first)."""
    out = []
    for run in runs:
        c = catmull(run)
        if out and abs(out[-1][0] - c[0][0]) < 1e-9 and abs(out[-1][1] - c[0][1]) < 1e-9:
            c = c[1:]
        out += c
    return out


def arc(cx, cz, r, deg0, deg1, n):
    return [(cx + r * math.cos(math.radians(deg0 + (deg1 - deg0) * i / n)),
             cz + r * math.sin(math.radians(deg0 + (deg1 - deg0) * i / n))) for i in range(n + 1)]


def foot(R):
    """The Staunton foot: a flat felt base, a full torus bead, a fillet, a smaller bead and a cove."""
    return [
        [(0.0, 0.0), (R - 0.006, 0.0)],
        [(R - 0.006, 0.0), (R, 0.006), (R, 0.016)],
        [(R, 0.016)] + arc(R - 0.021, 0.036, 0.021, -70, 115, 8)[1:],
        [(R - 0.03, 0.0555), (R - 0.034, 0.059), (R - 0.033, 0.064)],
        [(R - 0.033, 0.064)] + arc(R - 0.044, 0.073, 0.012, -40, 120, 5)[1:],
        [(R - 0.05, 0.0834), (R - 0.058, 0.087), (R - 0.07, 0.094), (R - 0.082, 0.104), (R - 0.09, 0.114)],
    ]


def lathe(name, prof, segs=SEGS):
    """prof: (r, z) bottom to top; r == 0 makes a pole. Cylindrical UVs."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings = []
    for r, z in prof:
        if r <= 1e-6:
            rings.append([bm.verts.new((0.0, 0.0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                          for i in range(segs)])

    def face(vs, us):
        try:
            f = bm.faces.new(vs)
        except ValueError:
            return
        for loop, (u, v) in zip(f.loops, us):
            loop[uv].uv = (u, v)

    for k in range(len(rings) - 1):
        A, B = rings[k], rings[k + 1]
        za, zb = prof[k][1], prof[k + 1][1]
        if len(A) == 1 and len(B) == 1:
            continue
        for i in range(segs):
            u0, u1 = i / segs, (i + 1) / segs
            j = (i + 1) % segs
            if len(A) == 1:
                face((A[0], B[j], B[i]), ((u0, za), (u1, zb), (u0, zb)))
            elif len(B) == 1:
                face((A[i], A[j], B[0]), ((u0, za), (u1, za), (u0, zb)))
            else:
                face((A[i], A[j], B[j], B[i]), ((u0, za), (u1, za), (u1, zb), (u0, zb)))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(body_mat())
    return o


# ---------------------------------------------------------------- object tools

def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def box(size, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    c = bpy.context.active_object
    c.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return c


def blob(radii, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1.0, location=loc,
                                         rotation=tuple(math.radians(a) for a in rot))
    o = bpy.context.active_object
    o.scale = radii
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return o


def subtract(o, c):
    activate(o)
    m = o.modifiers.new("cut", "BOOLEAN")
    m.operation = "DIFFERENCE"
    m.solver = "EXACT"
    m.object = c
    bpy.ops.object.modifier_apply(modifier="cut")
    bpy.data.objects.remove(c)


def join(objs, name):
    activate(objs[0])
    for o in objs:
        o.select_set(True)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    return o


def extruded(points, half, bevel, plane="XZ"):
    """A filled 2D outline (x, z) extruded +-half across Y and rounded by `bevel`; returns a mesh object."""
    cu = bpy.data.curves.new("outline", "CURVE")
    cu.dimensions = "2D"
    cu.fill_mode = "BOTH"
    cu.extrude = half
    cu.bevel_depth = bevel
    cu.bevel_resolution = 4
    sp = cu.splines.new("POLY")
    sp.points.add(len(points) - 1)
    for pt, (x, z) in zip(sp.points, points):
        pt.co = (x, z, 0, 1)
    sp.use_cyclic_u = True
    o = bpy.data.objects.new("outline", cu)
    bpy.context.scene.collection.objects.link(o)
    o.rotation_euler = (math.radians(90), 0, 0)
    activate(o)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(rotation=True)
    o.data.materials.clear()
    o.data.materials.append(body_mat())
    return o


def cylinder_uv(o):
    """u round the vertical axis, v = height: the grain of a turned piece runs up it."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        us = []
        for loop in f.loops:
            co = loop.vert.co
            us.append(math.atan2(co.y, co.x) / (2 * math.pi) + 0.5)
        # keep a face from wrapping across the seam
        if max(us) - min(us) > 0.5:
            us = [u + 1.0 if u < 0.5 else u for u in us]
        for loop, u in zip(f.loops, us):
            loop[uv].uv = (u, loop.vert.co.z)
    bm.to_mesh(o.data)
    bm.free()


def finish(o, name):
    activate(o)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    zs = [v.co.z for v in o.data.vertices]
    lo, hi = min(zs), max(zs)
    s = 1.0 / (hi - lo)
    for v in o.data.vertices:
        v.co = Vector((v.co.x * s, v.co.y * s, (v.co.z - lo) * s))
    # light enough for a station full of them: about 11k triangles a piece
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > 11000:
        activate(o)
        m = o.modifiers.new("dec", "DECIMATE")
        m.ratio = 11000 / tris
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="dec")
    cylinder_uv(o)
    activate(o)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(34))
    o.name = name
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE")
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    rmax = max(math.hypot(v.co.x, v.co.y) for v in o.data.vertices if v.co.z < 0.02)
    print(f"PIECE {name}: tris={tris} footR={rmax:.3f}")
    return o


# ---------------------------------------------------------------- the pieces

def pawn():
    R = BASE_R["Pawn"]
    p = profile(*foot(R),
                [(R - 0.09, 0.114), (0.18, 0.14), (0.152, 0.175), (0.133, 0.23), (0.12, 0.3), (0.11, 0.38),
                 (0.104, 0.45), (0.1, 0.49)],
                [(0.1, 0.49), (0.135, 0.5), (0.175, 0.512), (0.198, 0.527), (0.205, 0.544), (0.198, 0.56),
                 (0.172, 0.572), (0.125, 0.581), (0.098, 0.592)],
                [(0.098, 0.592)] + arc(0, 0.79, 0.21, -62, 90, 26)[1:-1] + [(0.0, 1.0)])
    return finish(lathe("PR_Piece_Pawn", p), "PR_Piece_Pawn")


def rook():
    R = BASE_R["Rook"]
    p = profile(*foot(R),
                [(R - 0.09, 0.114), (0.19, 0.145), (0.2, 0.18), (0.197, 0.26), (0.193, 0.38), (0.189, 0.5),
                 (0.186, 0.58)],
                [(0.186, 0.58), (0.204, 0.59), (0.214, 0.603), (0.211, 0.616), (0.197, 0.625)],
                [(0.197, 0.625), (0.193, 0.635), (0.2, 0.645), (0.196, 0.655)],
                [(0.196, 0.655), (0.214, 0.675), (0.238, 0.7), (0.258, 0.73), (0.27, 0.765), (0.274, 0.79)],
                [(0.274, 0.79), (0.274, 0.994)],
                [(0.274, 0.994), (0.268, 1.0)],
                [(0.268, 1.0), (0.205, 1.0)],
                [(0.205, 1.0), (0.2, 0.994), (0.2, 0.925)],
                [(0.2, 0.925), (0.0, 0.925)])
    o = lathe("PR_Piece_Rook", p)
    for a in (0, 60, 120):   # six crenels, as wide as half a merlon pair
        subtract(o, box((0.7, 0.1, 0.15), (0, 0, 1.0), (0, 0, math.radians(a + 30))))
    return finish(o, "PR_Piece_Rook")


def bishop():
    R = BASE_R["Bishop"]
    mitre = [(0.07, 0.655)] + [(0.142 * math.cos(math.radians(t)), 0.775 + 0.13 * math.sin(math.radians(t)))
                               for t in range(-58, 75, 7)] + [(0.034, 0.912)]
    p = profile(*foot(R),
                [(R - 0.09, 0.114), (0.122, 0.14), (0.105, 0.18), (0.093, 0.25), (0.084, 0.34), (0.077, 0.44),
                 (0.073, 0.52), (0.072, 0.555)],
                [(0.072, 0.555), (0.11, 0.565), (0.142, 0.578), (0.152, 0.59), (0.148, 0.602), (0.128, 0.61),
                 (0.1, 0.616)],
                [(0.1, 0.616), (0.112, 0.623), (0.108, 0.632), (0.08, 0.64), (0.066, 0.648)],
                mitre,
                [(0.034, 0.912), (0.024, 0.92)] + arc(0, 0.958, 0.036, -48, 90, 12)[1:-1] + [(0.0, 0.994)])
    o = lathe("PR_Piece_Bishop", p)
    # the slit: a plane cut diagonally into the front of the mitre, to about its axis
    subtract(o, box((0.16, 0.03, 0.36), (0.11, 0, 0.8), (math.radians(42), 0, 0)))
    return finish(o, "PR_Piece_Bishop")


def queen():
    R = BASE_R["Queen"]
    p = profile(*foot(R),
                [(R - 0.09, 0.114), (0.125, 0.14), (0.108, 0.18), (0.095, 0.25), (0.085, 0.35), (0.078, 0.46),
                 (0.074, 0.56), (0.073, 0.62)],
                [(0.073, 0.62), (0.105, 0.632), (0.14, 0.645), (0.153, 0.658), (0.15, 0.671), (0.134, 0.68),
                 (0.105, 0.686), (0.078, 0.693)],
                [(0.078, 0.693), (0.075, 0.71), (0.082, 0.74), (0.098, 0.77), (0.12, 0.8), (0.145, 0.83),
                 (0.166, 0.858), (0.178, 0.878), (0.182, 0.892)],
                [(0.182, 0.892), (0.178, 0.9), (0.166, 0.902)],
                [(0.166, 0.902), (0.14, 0.896), (0.11, 0.896), (0.088, 0.903), (0.07, 0.915), (0.056, 0.93),
                 (0.044, 0.942), (0.03, 0.948)],
                [(0.03, 0.948)] + arc(0, 0.97, 0.03, -45, 90, 12)[1:-1] + [(0.0, 1.0)])
    parts = [lathe("PR_Piece_Queen", p)]
    for i in range(8):   # the coronet's eight points
        a = 2 * math.pi * (i + 0.5) / 8
        parts.append(blob((0.024, 0.024, 0.03), (0.17 * math.cos(a), 0.17 * math.sin(a), 0.905)))
    for o in parts[1:]:
        o.data.materials.append(body_mat())
    return finish(join(parts, "PR_Piece_Queen"), "PR_Piece_Queen")


CROSS = [(-0.022, 1.0), (0.022, 1.0), (0.009, 0.962), (0.05, 0.973), (0.05, 0.927), (0.009, 0.938), (0.022, 0.9),
         (-0.022, 0.9), (-0.009, 0.938), (-0.05, 0.927), (-0.05, 0.973), (-0.009, 0.962)]


def king():
    R = BASE_R["King"]
    p = profile(*foot(R),
                [(R - 0.09, 0.114), (0.12, 0.14), (0.104, 0.18), (0.092, 0.25), (0.083, 0.35), (0.076, 0.46),
                 (0.072, 0.56), (0.071, 0.61)],
                [(0.071, 0.61), (0.1, 0.622), (0.135, 0.636), (0.148, 0.649), (0.145, 0.662), (0.13, 0.671),
                 (0.1, 0.677), (0.074, 0.684)],
                [(0.074, 0.684), (0.071, 0.7), (0.078, 0.73), (0.093, 0.76), (0.112, 0.79), (0.132, 0.815),
                 (0.149, 0.835), (0.157, 0.848)],
                [(0.157, 0.848), (0.155, 0.857), (0.145, 0.861)],
                [(0.145, 0.861), (0.115, 0.868), (0.085, 0.874), (0.058, 0.88), (0.042, 0.885)],
                [(0.042, 0.885), (0.036, 0.889), (0.036, 0.902)],
                [(0.036, 0.902), (0.0, 0.902)])
    cross = extruded(CROSS, 0.012, 0.009)
    return finish(join([lathe("PR_Piece_King", p), cross], "PR_Piece_King"), "PR_Piece_King")


def knight():
    R = BASE_R["Knight"]
    p = profile(*foot(R),
                [(R - 0.09, 0.114), (0.152, 0.14), (0.148, 0.17), (0.152, 0.2), (0.165, 0.222), (0.17, 0.238),
                 (0.163, 0.254), (0.14, 0.264)],
                [(0.14, 0.264), (0.0, 0.268)])
    foot_obj = lathe("PR_Piece_Knight", p)
    head, outline = carved_head()
    # the two ears, apart
    subtract(head, box((0.16, 0.024, 0.12), (0.005, 0, 1.0)))
    # eyes: a sunken socket under the brow with the eyeball in it
    ye = surf_y(head, 0.095, 0.8)
    for s in (-1, 1):
        subtract(head, blob((0.031, 0.022, 0.025), (0.095, s * (ye + 0.011), 0.8)))
        union(head, blob((0.019, 0.013, 0.016), (0.097, s * (ye - 0.004), 0.798)))
    # flared nostrils and the line of the mouth
    yn = surf_y(head, 0.282, 0.642)
    for s in (-1, 1):
        subtract(head, blob((0.02, 0.014, 0.024), (0.284, s * (yn + 0.003), 0.642), (0, 30, 0)))
    subtract(head, box((0.08, 0.4, 0.0075), (0.268, 0, 0.594), (0, math.radians(-6), 0)))
    # the mane: strands carved across the crest...
    back = [q for q in outline if q[0] < -0.03 and 0.34 < q[1] < 0.93]
    for i in range(1, 12):
        k = int(i * (len(back) - 1) / 12)
        (x0, z0), (x1, z1) = back[max(k - 1, 0)], back[min(k + 1, len(back) - 1)]
        tx, tz = x1 - x0, z1 - z0
        ln = math.hypot(tx, tz)
        nx, nz = tz / ln, -tx / ln            # inward (the outline runs clockwise)
        # hair flows down: each strand slants 28 degrees below the inward normal; lengths alternate
        a = math.atan2(nz, nx) - math.radians(28)
        nx, nz = math.cos(a), math.sin(a)
        x, z = back[k]
        length = 0.085 if i % 2 else 0.06
        subtract(head, box((length, 0.5, 0.0075), (x + nx * (length / 2 - 0.012), 0, z + nz * (length / 2 - 0.012)),
                           (0, math.atan2(-nz, nx), 0)))
    # ...a groove along the mane's edge, and the line of the round cheek, on both sides
    edge = []
    for i in range(len(back)):
        (x0, z0), (x1, z1) = back[max(i - 1, 0)], back[min(i + 1, len(back) - 1)]
        tx, tz = x1 - x0, z1 - z0
        ln = math.hypot(tx, tz) or 1.0
        edge.append((back[i][0] + tz / ln * 0.066, back[i][1] - tx / ln * 0.066))
    cheek = arc(0.045, 0.69, 0.098, 140, 320, 24)
    for path in (edge[2:-2], cheek):
        for s in (-1, 1):
            subtract(head, tube([(x, s * (surf_y(head, x, z) - 0.001), z) for x, z in path], 0.0065))
    activate(head)
    tris = sum(len(f.vertices) - 2 for f in head.data.polygons)
    m = head.modifiers.new("dec", "DECIMATE")
    m.ratio = min(1.0, 9000 / tris)
    bpy.ops.object.modifier_apply(modifier="dec")
    head.data.materials.clear()
    head.data.materials.append(body_mat())
    return finish(join([foot_obj, head], "PR_Piece_Knight"), "PR_Piece_Knight")


# The Staunton knight's side profile (looking along +x), smoothed by closed_catmull.
KNIGHT_OUTLINE = [
    (-0.15, 0.255), (-0.175, 0.32), (-0.19, 0.4), (-0.196, 0.48), (-0.19, 0.56), (-0.178, 0.64), (-0.158, 0.72),
    (-0.13, 0.79), (-0.095, 0.855), (-0.06, 0.9), (-0.038, 0.935), (-0.026, 0.97), (-0.006, 1.0), (0.018, 0.988),
    (0.034, 0.952), (0.06, 0.93), (0.1, 0.905), (0.145, 0.87), (0.19, 0.825), (0.235, 0.77), (0.27, 0.715),
    (0.293, 0.665), (0.304, 0.632), (0.3, 0.602), (0.285, 0.586), (0.25, 0.576), (0.205, 0.576), (0.165, 0.586),
    (0.135, 0.601), (0.11, 0.614), (0.09, 0.61), (0.076, 0.585), (0.075, 0.55), (0.085, 0.5), (0.105, 0.44),
    (0.13, 0.38), (0.15, 0.32), (0.155, 0.27), (0.15, 0.255)]


def closed_catmull(points, samples=4):
    n = len(points)
    out = []
    for i in range(n):
        p0, p1, p2, p3 = (Vector(points[(i + k) % n]) for k in (-1, 0, 1, 2))
        for s in range(samples):
            t = s / samples
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                       + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)
            out.append((q.x, q.y))
    return out


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def knight_half(x, z):
    """Half thickness of the carved head at side-profile point (x, z)."""
    t = 0.125
    t -= 0.048 * smoothstep(0.06, 0.3, x)                                     # the face narrows to the muzzle
    t -= 0.032 * smoothstep(0.87, 0.99, z) * smoothstep(-0.09, -0.01, x)      # ears
    t += 0.02 * math.exp(-(((x - 0.04) / 0.075) ** 2 + ((z - 0.685) / 0.065) ** 2))   # the round cheek
    t -= 0.028 * smoothstep(-0.12, -0.195, x)                                 # the mane's crest is narrower
    t += 0.012 * smoothstep(0.42, 0.28, z)                                    # the neck widens into the foot
    return max(0.035, t)


def carved_head():
    """The side profile as a solid whose sides are carved round: flat faces, rounded edges (radius RAD),
    thickness from knight_half. Returns the mesh object and the smoothed outline."""
    outline = closed_catmull(KNIGHT_OUTLINE, 4)
    slab = extruded(outline, 0.14, 0.0)
    activate(slab)
    m = slab.modifiers.new("remesh", "REMESH")
    m.mode = "VOXEL"
    m.voxel_size = 0.0055
    bpy.ops.object.modifier_apply(modifier="remesh")
    me = slab.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    a = np.array(outline)
    ab = np.roll(a, -1, axis=0) - a
    ab2 = (ab * ab).sum(1)
    d = np.empty(n)
    for s in range(0, n, 3000):
        P = co[s:s + 3000][:, [0, 2]]
        ap = P[:, None, :] - a[None]
        t = np.clip((ap * ab[None]).sum(-1) / ab2[None], 0, 1)
        q = a[None] + t[..., None] * ab[None]
        d[s:s + 3000] = np.sqrt(((P[:, None, :] - q) ** 2).sum(-1)).min(1)
    RAD = 0.055
    g = np.sqrt(1 - (1 - np.clip(d / RAD, 0, 1)) ** 2)
    T = np.array([knight_half(x, z) for x, z in co[:, [0, 2]]])
    co[:, 1] = co[:, 1] / np.abs(co[:, 1]).max() * T * g
    me.vertices.foreach_set("co", co.ravel())
    me.update()
    m = slab.modifiers.new("remesh", "REMESH")
    m.mode = "VOXEL"
    m.voxel_size = 0.0042
    bpy.ops.object.modifier_apply(modifier="remesh")
    m = slab.modifiers.new("smooth", "CORRECTIVE_SMOOTH")
    m.iterations = 3
    m.use_only_smooth = True
    bpy.ops.object.modifier_apply(modifier="smooth")
    return slab, outline


def surf_y(o, x, z, r=0.012):
    """How far the head's side surface stands out from the middle plane at (x, z)."""
    best = 0.0
    for v in o.data.vertices:
        if abs(v.co.x - x) < r and abs(v.co.z - z) < r:
            best = max(best, abs(v.co.y))
    return best


def union(o, c):
    activate(o)
    m = o.modifiers.new("add", "BOOLEAN")
    m.operation = "UNION"
    m.solver = "EXACT"
    m.object = c
    bpy.ops.object.modifier_apply(modifier="add")
    bpy.data.objects.remove(c)


def tube(points, radius):
    """A round groove cutter along 3D points."""
    cu = bpy.data.curves.new("tube", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 2
    cu.use_fill_caps = True
    sp = cu.splines.new("POLY")
    sp.points.add(len(points) - 1)
    for pt, (x, y, z) in zip(sp.points, points):
        pt.co = (x, y, z, 1)
    o = bpy.data.objects.new("tube", cu)
    bpy.context.scene.collection.objects.link(o)
    activate(o)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.active_object


def ring():
    bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.08, major_segments=96, minor_segments=16)
    o = bpy.context.active_object
    o.name = "PR_PieceRing"
    o.data.materials.append(bpy.data.materials.new("PieceRing"))
    activate(o)
    bpy.ops.object.shade_smooth()
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "PR_PieceRing.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE")


# ---------------------------------------------------------------- previews

def stage(res=(1800, 760)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.5, 0.62, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
    sc.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.4
    sun.rotation_euler = (math.radians(52), 0, math.radians(-38))
    sc.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("floor")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.35, 0.3, 0.25, 1)
    floor.data.materials.append(fm)


def place(name, x, yaw, ebony, scale=1.0):
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"))
    o = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.location = (x, 0, 0)
    o.rotation_euler = (0, 0, math.radians(yaw))
    o.scale = (scale, scale, scale)
    mat = bpy.data.materials.new("ebony" if ebony else "boxwood")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.05, 0.04, 0.035, 1) if ebony else (0.86, 0.74, 0.52, 1)
    b.inputs["Roughness"].default_value = 0.22
    o.data.materials.clear()
    o.data.materials.append(mat)


def sheet():
    reset()
    stage()
    order = [("PR_Piece_Pawn", 0.62), ("PR_Piece_Rook", 0.7), ("PR_Piece_Knight", 0.8), ("PR_Piece_Bishop", 0.9),
             ("PR_Piece_Queen", 1.0), ("PR_Piece_King", 1.08)]
    for i, (n, s) in enumerate(order):
        place(n, i * 0.62 - 1.55, 0 if "Knight" not in n else 25, False, s)
        place(n, i * 0.62 - 1.55 + 0.25, 0 if "Knight" not in n else -25, True, s * 0.95)
        bpy.context.selected_objects[0].location.y = 0.55
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = 46
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (0.1, -4.6, 1.55)
    cam.rotation_euler = (Vector((0.1, 0.25, 0.45)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam
    bpy.context.scene.render.filepath = os.path.join(OUT, "pieces_sheet.png")
    bpy.ops.render.render(write_still=True)


def knight_sheet():
    reset()
    stage((1800, 620))
    for i, yaw in enumerate((-90, -40, 0, 50, 180)):
        place("PR_Piece_Knight", i * 0.62 - 1.24, yaw, i % 2 == 1)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = 50
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (0, -4.3, 0.95)
    cam.rotation_euler = (Vector((0, 0, 0.55)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam
    bpy.context.scene.render.filepath = os.path.join(OUT, "knight_sheet.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    only = sys.argv[sys.argv.index("--") + 2] if len(sys.argv) > sys.argv.index("--") + 2 else "all"
    builds = {"pawn": pawn, "rook": rook, "bishop": bishop, "queen": queen, "king": king, "knight": knight}
    for key, build in builds.items():
        if only in ("all", key):
            reset()
            build()
    if only == "all":
        reset()
        ring()
    sheet()
    knight_sheet()
    print("DONE")
