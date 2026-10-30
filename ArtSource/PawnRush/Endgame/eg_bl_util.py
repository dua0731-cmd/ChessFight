"""Blender helpers for the endgame build: scene, collections, bmesh shapes, UVs, normals, placing in Unity units."""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import eg_layout as E


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.unit_settings.length_unit = "METERS"
    ts = sc.tool_settings
    ts.use_snap = True
    ts.snap_elements = {"INCREMENT"}
    ts.use_snap_grid_absolute = True
    sc.render.fps = 24
    return sc


def collection(name, parent=None, hide_render=False, exclude=False):
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(coll)
    coll.hide_render = hide_render
    if exclude:
        layer = _find_layer_coll(bpy.context.view_layer.layer_collection, name)
        if layer:
            layer.exclude = True
    return coll


def _find_layer_coll(lc, name):
    if lc.collection.name == name:
        return lc
    for c in lc.children:
        r = _find_layer_coll(c, name)
        if r:
            return r
    return None


def hide_in_viewlayer(name, hide=True):
    lc = _find_layer_coll(bpy.context.view_layer.layer_collection, name)
    if lc:
        lc.hide_viewport = hide


# ---------------------------------------------------------------------------------------------- unity <-> blender
def U(x, y, z):
    """Unity position -> Blender Vector."""
    return Vector((x, z, y))


def yaw_to_rz(yaw_deg):
    """Unity yaw (clockwise seen from above) -> Blender rotation about Z."""
    return math.radians(-yaw_deg)


def unity_pos(v):
    """Blender position -> Unity [x, y, z]."""
    return [round(v.x, 4), round(v.z, 4), round(v.y, 4)]


# ---------------------------------------------------------------------------------------------- bmesh shapes
def box_bm(bm, x0, x1, y0, y1, z0, z1, bevel=0.0, segments=2, mat=0):
    """Axis-aligned box in Blender coords (x, y = north, z = up), optional bevelled edges. Returns its verts."""
    n0 = len(bm.verts)
    geom = bmesh.ops.create_cube(bm, size=1.0)
    verts = geom["verts"]
    sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=verts)
    bmesh.ops.translate(bm, vec=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), verts=verts)
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        f.material_index = mat
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        b = min(bevel, 0.45 * min(sx, sy, sz))
        res = bmesh.ops.bevel(bm, geom=edges, offset=b, segments=segments, profile=0.5, affect="EDGES",
                              harden_normals=False, clamp_overlap=True)
        for f in res.get("faces", []):
            f.material_index = mat
        bm.verts.ensure_lookup_table()
        return [bm.verts[i] for i in range(n0, len(bm.verts))]
    return verts


def cyl_bm(bm, center, radius, depth, segments=48, mat=0, axis="Z", cap=True, bevel=0.0):
    geom = bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments, radius1=radius,
                                 radius2=radius, depth=depth)
    verts = geom["verts"]
    if axis == "X":
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
    elif axis == "Y":
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    bmesh.ops.translate(bm, vec=center, verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat
    if bevel > 0:
        rim = [e for e in {e for v in verts for e in v.link_edges} if len(e.link_faces) == 2 and
               abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, segments=2, profile=0.5, affect="EDGES", clamp_overlap=True)
    return verts


def lathe_bm(bm, profile, segments=32, mat=0, center=(0, 0, 0)):
    """Revolve a profile [(r, z), ...] (bottom to top) about Z."""
    rings = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        ring = [bm.verts.new((center[0] + r * math.cos(a), center[1] + r * math.sin(a), center[2] + z)) for r, z in profile]
        rings.append(ring)
    for i in range(segments):
        r0, r1 = rings[i], rings[(i + 1) % segments]
        for k in range(len(profile) - 1):
            f = bm.faces.new((r0[k], r1[k], r1[k + 1], r0[k + 1]))
            f.material_index = mat
    if profile[0][0] > 1e-4:
        f = bm.faces.new([rings[i][0] for i in reversed(range(segments))])
        f.material_index = mat
    if profile[-1][0] > 1e-4:
        f = bm.faces.new([rings[i][-1] for i in range(segments)])
        f.material_index = mat
    return rings


def sphere_bm(bm, center, radius, seg=24, rings=12, mat=0):
    geom = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=radius)
    bmesh.ops.translate(bm, vec=center, verts=geom["verts"])
    for f in {f for v in geom["verts"] for f in v.link_faces}:
        f.material_index = mat
    return geom["verts"]


def prism_bm(bm, outline, z0, z1, mat=0):
    """Extrude a 2D outline [(x, y), ...] (counter-clockwise) from z0 to z1."""
    bot = [bm.verts.new((x, y, z0)) for x, y in outline]
    top = [bm.verts.new((x, y, z1)) for x, y in outline]
    n = len(outline)
    faces = [bm.faces.new(list(reversed(bot))), bm.faces.new(top)]
    for i in range(n):
        faces.append(bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i])))
    for f in faces:
        f.material_index = mat
    return bot + top


# ---------------------------------------------------------------------------------------------- objects
def mesh_object(name, bm, materials, coll, uv_scale=1.0, uv_world=True, smooth_angle=35.0, origin=(0, 0, 0)):
    """Make an object from a bmesh: UV0 box-mapped in metres (world-aligned), materials by index."""
    me = bpy.data.meshes.new(name)
    if uv_world:
        box_uv(bm, uv_scale)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for m in materials:
        me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    coll.objects.link(o)
    o.location = origin
    if smooth_angle is not None:
        set_smooth(o, smooth_angle)
    return o


def box_uv(bm, metres=1.0):
    """Box-project UV0 in metres: 1 UV unit = `metres` m (512 px/m on a 2048 sheet with metres = 4)."""
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            co = loop.vert.co
            if ax == 0:
                u, v = co.y, co.z
            elif ax == 1:
                u, v = co.x, co.z
            else:
                u, v = co.x, co.y
            loop[uv].uv = (u / metres, v / metres)


def set_smooth(o, angle=35.0):
    """Smooth by Angle, written into the mesh (sharp edges marked), so the FBX carries it."""
    me = o.data
    me.shade_smooth()
    me.set_sharp_from_angle(angle=math.radians(angle))


_SBA = None


def smooth_by_angle_group():
    """A small node group that does what Blender's Smooth by Angle asset does (no asset library in background)."""
    global _SBA
    if _SBA is not None:
        return _SBA
    ng = bpy.data.node_groups.new("EG Smooth by Angle", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Angle", in_out="INPUT", socket_type="NodeSocketFloat")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    n = ng.nodes
    gi, go = n.new("NodeGroupInput"), n.new("NodeGroupOutput")
    ea = n.new("GeometryNodeInputMeshEdgeAngle")
    cmp = n.new("FunctionNodeCompare")
    cmp.data_type = "FLOAT"
    cmp.operation = "LESS_EQUAL"
    sss = n.new("GeometryNodeSetShadeSmooth")
    sss.domain = "EDGE"
    ng.links.new(ea.outputs["Unsigned Angle"], cmp.inputs[0])
    ng.links.new(gi.outputs[1], cmp.inputs[1])
    ng.links.new(gi.outputs[0], sss.inputs["Geometry"])
    ng.links.new(cmp.outputs[0], sss.inputs["Shade Smooth"])
    sss2 = n.new("GeometryNodeSetShadeSmooth")
    sss2.domain = "FACE"
    ng.links.new(sss.outputs[0], sss2.inputs["Geometry"])
    ng.links.new(sss2.outputs[0], go.inputs[0])
    _SBA = ng
    return ng


def weighted_normals(o):
    mod = o.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    mod.keep_sharp = True
    mod.weight = 50
    return mod


def instance(src, name, coll, loc, rz=0.0, scale=(1, 1, 1)):
    """A linked duplicate (shares the mesh)."""
    o = bpy.data.objects.new(name, src.data)
    coll.objects.link(o)
    o.location = loc
    o.rotation_euler = (0, 0, rz)
    o.scale = scale
    for m in src.modifiers:
        if m.type == "NODES":
            nm = o.modifiers.new(m.name, "NODES")
            nm.node_group = m.node_group
            for k in m.keys():
                try:
                    nm[k] = m[k]
                except Exception:
                    pass
        elif m.type == "WEIGHTED_NORMAL":
            weighted_normals(o)
    o["eg_source"] = src.name
    return o


def empty(name, coll, loc=(0, 0, 0), size=0.5, kind="PLAIN_AXES"):
    o = bpy.data.objects.new(name, None)
    o.empty_display_type = kind
    o.empty_display_size = size
    o.location = loc
    coll.objects.link(o)
    return o


def tri_count(o):
    me = o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    n = sum(len(p.vertices) - 2 for p in me.polygons)
    o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    return n
