"""The war room kit: modular static pieces, hall dressing and the device skins.

Blender coordinates (x east, y north, z up), 1 unit = 1 m. Each builder makes one object whose origin is its pivot
(bottom centre for static pieces, the motion axis for device skins, see the table in ENDGAME_LD_HARD_v1.md) and
returns it; scene code places linked duplicates. Walked and touched faces match the Unity collider sizes within 2 cm.
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

import eg_bl_util as B

M = {}          # material name -> material, filled by init()
KIT = None      # the kit collection


def init(materials, kit_coll):
    global KIT
    M.update(materials)
    KIT = kit_coll


def mats(*names):
    return [M[n] for n in names]


def finish(o, bevel_normals=True):
    if bevel_normals:
        B.weighted_normals(o)
    o["eg_kit"] = True
    return o


# ---------------------------------------------------------------------------------------------- static course kit
def rail(length):
    """Teal rail block 0.5 wide, 1.2 high, `length` long along y, with a gold cap and raised panel frames."""
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, -0.25, 0.25, -h, h, 0.0, 1.2, bevel=0.03, mat=0)
    B.box_bm(bm, -0.25, 0.25, -h, h, 1.2, 1.32, bevel=0.025, mat=1)
    B.box_bm(bm, -0.25, 0.25, -h, h, 0.0, 0.12, bevel=0.02, mat=1)
    n = max(1, int(round(length / 2.0)))
    pitch = length / n
    for i in range(n):
        y0 = -h + i * pitch + 0.18
        y1 = -h + (i + 1) * pitch - 0.18
        for side in (-1, 1):
            x = side * 0.25
            # recessed panel frames (inside the face, nothing proud of it)
            xi = x - side * 0.03
            B.box_bm(bm, min(x, xi), max(x, xi), y0, y1, 0.3, 1.0, bevel=0.0, mat=1)
            B.box_bm(bm, min(x, x - side * 0.02), max(x, x - side * 0.02), y0 + 0.08, y1 - 0.08, 0.38, 0.92, mat=0)
    return finish(B.mesh_object(f"PR_EG_Rail_{str(length).replace('.', 'p')}m", bm, mats("EG_Teal", "EG_Gold"), KIT))


def rail_post():
    """A post on the rail line: flush with the rail faces across (0.5 m), 0.72 m along it."""
    bm = bmesh.new()
    B.box_bm(bm, -0.25, 0.25, -0.36, 0.36, 0.0, 1.38, bevel=0.04, mat=0)
    B.box_bm(bm, -0.25, 0.25, -0.42, 0.42, 1.38, 1.48, bevel=0.03, mat=1)
    B.cyl_bm(bm, (0, 0, 1.6), 0.16, 0.24, segments=16, mat=1)
    B.sphere_bm(bm, (0, 0, 1.78), 0.12, seg=12, rings=8, mat=1)
    return finish(B.mesh_object("PR_EG_RailPost", bm, mats("EG_Teal", "EG_Gold"), KIT))


def map_table(length=6.0):
    """The signal table (S1, R6): walnut body 2 m wide, top 2.3 m over its foot (1.3 over the floor), a map top."""
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, -1.0, 1.0, -h, h, 1.95, 2.25, bevel=0.04, mat=0)          # top slab
    B.box_bm(bm, -0.92, 0.92, -h + 0.08, h - 0.08, 2.25, 2.3, bevel=0.0, mat=1)  # map
    B.box_bm(bm, -1.04, 1.04, -h - 0.04, h + 0.04, 2.18, 2.26, bevel=0.015, mat=2)  # gold lip
    B.box_bm(bm, -0.85, 0.85, -h + 0.2, h - 0.2, 0.0, 1.95, bevel=0.05, mat=0)    # body
    for sx in (-1, 1):
        for sy in (-1, 1):
            B.box_bm(bm, sx * 0.95 - 0.12, sx * 0.95 + 0.12, sy * (h - 0.15) - 0.12, sy * (h - 0.15) + 0.12, 0.0, 1.95,
                     bevel=0.03, mat=0)
    for z in (0.6, 1.3):
        B.box_bm(bm, -0.88, 0.88, -h + 0.18, h - 0.18, z, z + 0.05, bevel=0.0, mat=2)
    return finish(B.mesh_object("PR_EG_MapTable", bm, mats("EG_Walnut", "EG_MapPaper", "EG_Gold"), KIT))


def rook_statue():
    bm = bmesh.new()
    B.box_bm(bm, -1.3, 1.3, -1.3, 1.3, 0.0, 0.6, bevel=0.05, mat=1)
    prof = [(0.0, 0.6), (1.05, 0.6), (1.05, 0.85), (0.85, 1.0), (0.78, 1.3), (0.7, 3.2), (0.82, 3.35), (1.05, 3.55),
            (1.05, 4.25), (0.0, 4.25)]
    B.lathe_bm(bm, prof, segments=40, mat=0)
    for i in range(6):
        a = i * math.tau / 6
        cx, cy = 0.78 * math.cos(a), 0.78 * math.sin(a)
        v = B.box_bm(bm, -0.22, 0.22, -0.2, 0.2, 4.25, 4.85, bevel=0.03, mat=0)
        bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, "Z"))
        bmesh.ops.translate(bm, verts=v, vec=(cx, cy, 0))
    B.cyl_bm(bm, (0, 0, 3.0), 0.76, 0.12, segments=40, mat=2)
    return finish(B.mesh_object("PR_EG_RookStatue", bm, mats("EG_Marble_Cream", "EG_Stone", "EG_Gold"), KIT))


def compass_inlay():
    """S3 compass rose, 9 m across, 2 cm proud of the floor (looks only)."""
    bm = bmesh.new()
    ring_o, ring_i = 4.5, 4.15
    seg = 96
    outer = [bm.verts.new((ring_o * math.cos(i * math.tau / seg), ring_o * math.sin(i * math.tau / seg), 0.02)) for i in range(seg)]
    inner = [bm.verts.new((ring_i * math.cos(i * math.tau / seg), ring_i * math.sin(i * math.tau / seg), 0.02)) for i in range(seg)]
    for i in range(seg):
        f = bm.faces.new((outer[i], outer[(i + 1) % seg], inner[(i + 1) % seg], inner[i]))
        f.material_index = 1
    face = [bm.verts.new((4.1 * math.cos(i * math.tau / seg), 4.1 * math.sin(i * math.tau / seg), 0.012)) for i in range(seg)]
    f = bm.faces.new(face)
    f.material_index = 0
    for k in range(8):
        a = k * math.tau / 8
        length = 4.0 if k % 2 == 0 else 2.6
        w = 0.55 if k % 2 == 0 else 0.35
        tip = Vector((math.cos(a), math.sin(a), 0)) * length
        side = Vector((-math.sin(a), math.cos(a), 0)) * w
        for s, mi in ((1, 1), (-1, 2)):
            v = [bm.verts.new((0, 0, 0.03)), bm.verts.new(tuple(side * s + Vector((0, 0, 0.03)))),
                 bm.verts.new(tuple(tip + Vector((0, 0, 0.03))))]
            ff = bm.faces.new(v if s > 0 else list(reversed(v)))
            ff.material_index = mi
            ff.normal_update()
            if ff.normal.z < 0:
                ff.normal_flip()
    o = B.mesh_object("PR_EG_CompassInlay", bm, mats("EG_MapPaper", "EG_Gold", "EG_Teal"), KIT, smooth_angle=None)
    return finish(o, False)


def arrow_sign():
    """A gold double chevron on a teal board (S1 entry, beat starts), 1.6 x 0.9 m, standing on a post."""
    bm = bmesh.new()
    B.box_bm(bm, -0.8, 0.8, -0.06, 0.06, 1.6, 2.5, bevel=0.03, mat=0)
    B.box_bm(bm, -0.86, 0.86, -0.08, 0.08, 1.55, 1.6, bevel=0.01, mat=1)
    B.box_bm(bm, -0.86, 0.86, -0.08, 0.08, 2.5, 2.55, bevel=0.01, mat=1)
    B.box_bm(bm, -0.07, 0.07, -0.07, 0.07, 0.0, 1.6, bevel=0.02, mat=1)
    for dx in (-0.28, 0.12):
        pts = [(dx - 0.15, 1.75), (dx + 0.05, 1.75), (dx + 0.3, 2.05), (dx + 0.05, 2.35), (dx - 0.15, 2.35), (dx + 0.1, 2.05)]
        outline = [(x, z) for x, z in pts]
        v = B.prism_bm(bm, [(x, z) for x, z in outline], -0.1, 0.1, mat=1)
        bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    return finish(B.mesh_object("PR_EG_ArrowSign", bm, mats("EG_Teal", "EG_Gold"), KIT))


# ---------------------------------------------------------------------------------------------- hall dressing
def hall_pillar(height=48.0):
    bm = bmesh.new()
    prof = [(0.0, 0.0), (1.5, 0.0), (1.5, 0.6), (1.25, 0.9), (1.05, 1.2), (1.0, height - 2.4), (1.15, height - 2.0),
            (1.4, height - 1.4), (1.6, height - 0.6), (1.6, height), (0.0, height)]
    B.lathe_bm(bm, prof, segments=32, mat=0)
    for z in (height * 0.35, height * 0.6):
        B.cyl_bm(bm, (0, 0, z), 1.12, 0.4, segments=32, mat=1)
    # flutes as shallow ribs
    for i in range(12):
        a = i * math.tau / 12
        v = B.box_bm(bm, 0.98, 1.06, -0.06, 0.06, 1.3, height - 2.5, mat=0)
        bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(a, 3, "Z"))
    return finish(B.mesh_object("PR_EG_HallPillar", bm, mats("EG_Stone", "EG_Gold"), KIT))


def lamp():
    """A brass lantern on a wall bracket; the glass glows (EG_Lamp_Emissive)."""
    bm = bmesh.new()
    B.box_bm(bm, -0.05, 0.05, -0.05, 0.85, -0.05, 0.05, bevel=0.01, mat=0)       # bracket arm (along +y)
    B.cyl_bm(bm, (0, 0.85, -0.05), 0.22, 0.08, segments=16, mat=0)
    B.cyl_bm(bm, (0, 0.85, -0.42), 0.16, 0.62, segments=16, mat=1)
    B.cyl_bm(bm, (0, 0.85, -0.78), 0.24, 0.1, segments=16, mat=0)
    prof = [(0.24, 0.0), (0.06, 0.28), (0.0, 0.32)]
    B.lathe_bm(bm, prof, segments=16, mat=0, center=(0, 0.85, 0.0))
    for k in range(4):
        a = k * math.tau / 4 + math.pi / 4
        x, y = 0.17 * math.cos(a), 0.85 + 0.17 * math.sin(a)
        B.box_bm(bm, x - 0.025, x + 0.025, y - 0.025, y + 0.025, -0.75, -0.08, mat=0)
    return finish(B.mesh_object("PR_EG_Lamp", bm, mats("EG_Gold", "EG_Lamp_Emissive"), KIT))


BOOK_COLORS = ["#6B4B2E", "#8A6B3E", "#3E5A55", "#5A6B4A", "#4A4F63", "#7D5A50", "#B59A6A", "#584235", "#6C7A6E",
               "#8C7B5A", "#3F4A48", "#A0835B"]


def bookshelf_generator_group():
    """Geometry Nodes book filler: books along each shelf with random height, thickness, colour and lean.
    Inputs: width, shelf count, shelf pitch, seed. Output: realized books with a 'Col' colour attribute."""
    ng = bpy.data.node_groups.get("EG Bookshelf Books")
    if ng:
        return ng
    ng = bpy.data.node_groups.new("EG Bookshelf Books", "GeometryNodeTree")
    io = ng.interface
    io.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    for name, kind, val in (("Width", "NodeSocketFloat", 2.2), ("Shelves", "NodeSocketInt", 5), ("Pitch", "NodeSocketFloat", 0.6),
                            ("Seed", "NodeSocketInt", 0), ("Count", "NodeSocketInt", 16)):
        s = io.new_socket(name, in_out="INPUT", socket_type=kind)
        s.default_value = val
    io.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    n, L = ng.nodes, ng.links
    gi, go = n.new("NodeGroupInput"), n.new("NodeGroupOutput")
    # a grid of points: Count books per shelf x Shelves
    grid = n.new("GeometryNodeMeshGrid")
    L.new(gi.outputs["Count"], grid.inputs["Vertices X"])
    L.new(gi.outputs["Shelves"], grid.inputs["Vertices Y"])
    L.new(gi.outputs["Width"], grid.inputs["Size X"])
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    sub = n.new("ShaderNodeMath")
    sub.operation = "SUBTRACT"
    sub.inputs[1].default_value = 1.0
    L.new(gi.outputs["Shelves"], sub.inputs[0])
    L.new(sub.outputs[0], mul.inputs[0])
    L.new(gi.outputs["Pitch"], mul.inputs[1])
    L.new(mul.outputs[0], grid.inputs["Size Y"])
    pts0 = n.new("GeometryNodeMeshToPoints")
    L.new(grid.outputs["Mesh"], pts0.inputs["Mesh"])
    # stand the grid up: (x, y) -> (x, 0, y + half span + shelf board), so each row sits on a shelf
    pos = n.new("GeometryNodeInputPosition")
    sep = n.new("ShaderNodeSeparateXYZ")
    L.new(pos.outputs[0], sep.inputs[0])
    half = n.new("ShaderNodeMath")
    half.operation = "MULTIPLY_ADD"
    half.inputs[1].default_value = 0.5
    half.inputs[2].default_value = 0.05
    L.new(mul.outputs[0], half.inputs[0])
    zz = n.new("ShaderNodeMath")
    zz.operation = "ADD"
    L.new(sep.outputs["Y"], zz.inputs[0])
    L.new(half.outputs[0], zz.inputs[1])
    up = n.new("ShaderNodeCombineXYZ")
    L.new(sep.outputs["X"], up.inputs["X"])
    L.new(zz.outputs[0], up.inputs["Z"])
    pts = n.new("GeometryNodeSetPosition")
    L.new(pts0.outputs["Points"], pts.inputs["Geometry"])
    L.new(up.outputs[0], pts.inputs["Position"])
    # jitter x a little, random size per book
    cube = n.new("GeometryNodeMeshCube")
    cube.inputs["Size"].default_value = (1.0, 1.0, 1.0)
    tr = n.new("GeometryNodeTransform")
    tr.inputs["Translation"].default_value = (0.0, 0.0, 0.5)
    L.new(cube.outputs["Mesh"], tr.inputs["Geometry"])
    rnd_h = n.new("FunctionNodeRandomValue")
    rnd_h.data_type = "FLOAT"
    rnd_h.inputs["Min"].default_value = 0.32
    rnd_h.inputs["Max"].default_value = 0.52
    rnd_t = n.new("FunctionNodeRandomValue")
    rnd_t.data_type = "FLOAT"
    rnd_t.inputs["Min"].default_value = 0.07
    rnd_t.inputs["Max"].default_value = 0.13
    rnd_d = n.new("FunctionNodeRandomValue")
    rnd_d.data_type = "FLOAT"
    rnd_d.inputs["Min"].default_value = 0.26
    rnd_d.inputs["Max"].default_value = 0.36
    for k, r in enumerate((rnd_h, rnd_t, rnd_d)):
        add = n.new("ShaderNodeMath")
        add.operation = "ADD"
        add.inputs[1].default_value = 17 * (k + 1)
        L.new(gi.outputs["Seed"], add.inputs[0])
        L.new(add.outputs[0], r.inputs["Seed"])
    comb = n.new("ShaderNodeCombineXYZ")
    L.new(rnd_t.outputs["Value"], comb.inputs["X"])
    L.new(rnd_d.outputs["Value"], comb.inputs["Y"])
    L.new(rnd_h.outputs["Value"], comb.inputs["Z"])
    rnd_lean = n.new("FunctionNodeRandomValue")
    rnd_lean.data_type = "FLOAT"
    rnd_lean.inputs["Min"].default_value = -0.12
    rnd_lean.inputs["Max"].default_value = 0.12
    L.new(gi.outputs["Seed"], rnd_lean.inputs["Seed"])
    lean_c = n.new("ShaderNodeCombineXYZ")
    L.new(rnd_lean.outputs["Value"], lean_c.inputs["Y"])
    inst = n.new("GeometryNodeInstanceOnPoints")
    L.new(pts.outputs["Geometry"], inst.inputs["Points"])
    L.new(tr.outputs["Geometry"], inst.inputs["Instance"])
    L.new(comb.outputs["Vector"], inst.inputs["Scale"])
    L.new(lean_c.outputs["Vector"], inst.inputs["Rotation"])
    # colour index -> palette
    rnd_c = n.new("FunctionNodeRandomValue")
    rnd_c.data_type = "INT"
    rnd_c.inputs["Min"].default_value = 0
    rnd_c.inputs["Max"].default_value = len(BOOK_COLORS) - 1
    addc = n.new("ShaderNodeMath")
    addc.operation = "ADD"
    addc.inputs[1].default_value = 91
    L.new(gi.outputs["Seed"], addc.inputs[0])
    L.new(addc.outputs[0], rnd_c.inputs["Seed"])
    idx = n.new("GeometryNodeIndexSwitch")
    idx.data_type = "RGBA"
    while len(idx.index_switch_items) < len(BOOK_COLORS):
        idx.index_switch_items.new()
    from eg_bl_materials import srgb
    for k, hexc in enumerate(BOOK_COLORS):
        idx.inputs[k + 1].default_value = srgb(hexc)
    L.new(rnd_c.outputs["Value"], idx.inputs["Index"])
    store = n.new("GeometryNodeStoreNamedAttribute")
    store.data_type = "FLOAT_COLOR"
    store.domain = "INSTANCE"
    store.inputs["Name"].default_value = "Col"
    L.new(inst.outputs["Instances"], store.inputs["Geometry"])
    L.new(idx.outputs[0], store.inputs["Value"])
    real = n.new("GeometryNodeRealizeInstances")
    L.new(store.outputs["Geometry"], real.inputs["Geometry"])
    setm = n.new("GeometryNodeSetMaterial")
    setm.inputs["Material"].default_value = M["EG_Books"]
    L.new(real.outputs["Geometry"], setm.inputs["Geometry"])
    join = n.new("GeometryNodeJoinGeometry")
    L.new(setm.outputs["Geometry"], join.inputs[0])
    L.new(gi.outputs["Geometry"], join.inputs[0])
    L.new(join.outputs[0], go.inputs[0])
    return ng


def bookshelf_unit(seed=0, width=2.4, shelves=5, pitch=0.6):
    """One 2.4 m bookshelf bay (2.4 x 0.6 x 3.0 m): walnut carcass, gold shelf lips, books from the GN generator
    (realized and applied so the FBX carries plain geometry)."""
    bm = bmesh.new()
    hw = width / 2
    height = shelves * pitch
    B.box_bm(bm, -hw, hw, 0.25, 0.3, 0.0, height, mat=0)                          # back
    for sx in (-1, 1):
        B.box_bm(bm, sx * hw - 0.05, sx * hw + 0.05, -0.3, 0.3, 0.0, height + 0.08, bevel=0.015, mat=0)
    for k in range(shelves + 1):
        z = k * pitch
        B.box_bm(bm, -hw, hw, -0.3, 0.3, z, z + 0.05, bevel=0.01, segments=1, mat=0)
        B.box_bm(bm, -hw, hw, -0.32, -0.3, z + 0.01, z + 0.04, mat=1)
    o = B.mesh_object(f"PR_EG_Bookshelf_{seed}", bm, mats("EG_Walnut", "EG_Gold"), KIT)
    o.data.materials.append(M["EG_Books"])
    ng = bookshelf_generator_group()
    # the generator runs once per unit and is applied at once, so its inputs are set as the group's defaults
    for name, val in (("Width", width - 0.2), ("Shelves", shelves), ("Pitch", pitch), ("Seed", seed),
                      ("Count", max(6, int(width * 7.5)))):
        ng.interface.items_tree[name].default_value = val
    mod = o.modifiers.new("Books", "NODES")
    mod.node_group = ng
    # the generator's grid is centred on the origin: lift it so the books stand on the shelves
    bpy.context.view_layer.objects.active = o
    with bpy.context.temp_override(object=o, active_object=o):
        for m in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    B.set_smooth(o, 30)
    return finish(o)


def banner(cloth=True):
    """A navy banner, 3 x 6.5 m, hanging from a gold rod, a gold rook on it. Folds from a short cloth simulation,
    applied to a static mesh (no cloth in the game)."""
    w, h = 3.0, 6.5
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=18, y_subdivisions=36, size=1.0)
    o = bpy.context.active_object
    o.name = "PR_EG_Banner"
    for c in o.users_collection:
        c.objects.unlink(o)
    KIT.objects.link(o)
    me = o.data
    for v in me.vertices:
        x, y = v.co.x, v.co.y
        v.co = Vector((x * w, 0.0, -(y + 0.5) * h))
    # swallowtail bottom
    for v in me.vertices:
        if v.co.z < -h + 0.9:
            k = (v.co.z + h) / 0.9
            if abs(v.co.x) < 0.5 * (1 - k) * w * 0.6:
                v.co.z += (1 - k) * 0.6
    vg = o.vertex_groups.new(name="pin")
    vg.add([v.index for v in me.vertices if v.co.z > -0.01], 1.0, "REPLACE")
    if cloth:
        try:
            mod = o.modifiers.new("Cloth", "CLOTH")
            mod.settings.vertex_group_mass = "pin"
            mod.settings.quality = 6
            mod.settings.mass = 0.4
            mod.settings.tension_stiffness = 20
            sc = bpy.context.scene
            mod.point_cache.frame_start = 1
            mod.point_cache.frame_end = 40
            sc.frame_start, sc.frame_end = 1, 40
            bpy.ops.object.effector_add(type="WIND", location=(0, -3, -3))
            fld = bpy.context.active_object
            fld.field.strength = 60
            fld.rotation_euler = (math.radians(90), 0, math.radians(20))
            bpy.context.view_layer.objects.active = o
            for f in range(1, 41):
                sc.frame_set(f)
            with bpy.context.temp_override(object=o, active_object=o):
                bpy.ops.object.modifier_apply(modifier="Cloth")
            bpy.data.objects.remove(fld)
            sc.frame_set(1)
        except Exception as ex:
            print("[kit] cloth failed, flat banner:", ex)
    me.materials.append(M["EG_BannerNavy"])
    me.materials.append(M["EG_Gold"])
    sol = o.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.04
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier="Solidify")
    # the rod and the rook emblem (a flat gold silhouette just in front)
    bm = bmesh.new()
    bm.from_mesh(me)
    B.cyl_bm(bm, (0, 0, 0.05), 0.08, w + 0.6, segments=12, mat=1, axis="X")
    for sx in (-1, 1):
        B.sphere_bm(bm, (sx * (w / 2 + 0.35), 0, 0.05), 0.14, seg=12, rings=8, mat=1)
    rook = [(-0.55, -4.6), (0.55, -4.6), (0.55, -4.35), (0.38, -4.2), (0.3, -3.1), (0.5, -2.95), (0.5, -2.45),
            (0.3, -2.45), (0.3, -2.65), (0.1, -2.65), (0.1, -2.45), (-0.1, -2.45), (-0.1, -2.65), (-0.3, -2.65),
            (-0.3, -2.45), (-0.5, -2.45), (-0.5, -2.95), (-0.3, -3.1), (-0.38, -4.2), (-0.55, -4.35)]
    ys = -0.12
    verts = [bm.verts.new((x, ys, z)) for x, z in rook]
    f = bm.faces.new(verts)
    f.material_index = 1
    f.normal_update()
    if f.normal.y > 0:
        f.normal_flip()
    bm.to_mesh(me)
    bm.free()
    o.vertex_groups.clear()
    B.set_smooth(o, 60)
    return finish(o, False)


def arch_window(width=5.0, height=10.0):
    """A tall arched window frame (gold mullions, walnut frame) with glowing glass, for the hall walls."""
    bm = bmesh.new()
    hw = width / 2
    seg = 24
    outline = [(-hw, 0.0), (hw, 0.0), (hw, height - hw)]
    for i in range(1, seg):
        a = math.pi * i / seg
        outline.append((hw * math.cos(a), height - hw + hw * math.sin(a)))
    outline.append((-hw, height - hw))
    v = B.prism_bm(bm, outline, -0.05, 0.05, mat=2)
    bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    # frame
    B.box_bm(bm, -hw - 0.35, -hw, -0.25, 0.25, 0, height - hw, bevel=0.03, mat=0)
    B.box_bm(bm, hw, hw + 0.35, -0.25, 0.25, 0, height - hw, bevel=0.03, mat=0)
    B.box_bm(bm, -hw - 0.5, hw + 0.5, -0.35, 0.35, -0.4, 0.0, bevel=0.03, mat=0)
    for i in range(seg + 1):
        a = math.pi * i / seg
        c = Vector((0, 0, height - hw)) + Vector((math.cos(a), 0, math.sin(a))) * (hw + 0.17)
        vv = B.box_bm(bm, -0.2, 0.2, -0.25, 0.25, -0.2, 0.2, mat=0)
        bmesh.ops.rotate(bm, verts=vv, cent=(0, 0, 0), matrix=Matrix.Rotation(-a, 3, "Y"))
        bmesh.ops.translate(bm, verts=vv, vec=c)
    for x in (-hw / 3, hw / 3):
        B.box_bm(bm, x - 0.06, x + 0.06, -0.08, 0.08, 0, height - 0.6, mat=1)
    for z in (height * 0.3, height * 0.55):
        B.box_bm(bm, -hw, hw, -0.08, 0.08, z - 0.05, z + 0.05, mat=1)
    return finish(B.mesh_object("PR_EG_ArchWindow", bm, mats("EG_Walnut", "EG_Gold", "EG_Window_Emissive"), KIT))


def ladder(height=12.0):
    bm = bmesh.new()
    for sx in (-0.3, 0.3):
        B.box_bm(bm, sx - 0.04, sx + 0.04, -0.05, 0.05, 0, height, bevel=0.01, mat=0)
    k = 0.4
    z = k
    while z < height - 0.2:
        B.cyl_bm(bm, (0, 0, z), 0.025, 0.6, segments=8, mat=1, axis="X")
        z += k
    B.cyl_bm(bm, (0, 0, height - 0.05), 0.05, 0.9, segments=10, mat=1, axis="X")
    return finish(B.mesh_object("PR_EG_Ladder", bm, mats("EG_Walnut", "EG_Gold"), KIT))


def balcony(length=12.0):
    """A walnut gallery along the hall wall: deck 2 m deep, gold-capped balusters every 0.5 m."""
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, 0.0, 2.0, -h, h, -0.35, 0.0, bevel=0.03, mat=0)
    B.box_bm(bm, 1.85, 2.05, -h, h, 0.9, 1.0, bevel=0.02, mat=1)
    y = -h + 0.25
    while y < h:
        B.box_bm(bm, 1.9, 2.0, y - 0.04, y + 0.04, 0.0, 0.9, mat=0)
        y += 0.5
    for k in range(int(length / 3) + 1):
        yy = -h + k * 3.0
        prof = [(0.0, -2.4), (0.18, -2.4), (0.18, -0.35), (0.0, -0.35)]
        B.box_bm(bm, 0.2, 1.8, yy - 0.12, yy + 0.12, -1.6, -0.35, mat=0)
    return finish(B.mesh_object("PR_EG_Balcony", bm, mats("EG_Walnut", "EG_Gold"), KIT))


def chandelier():
    bm = bmesh.new()
    seg = 32
    for r, z, mi in ((2.2, 0.0, 0), (1.4, 0.6, 0)):
        for i in range(seg):
            a0, a1 = i * math.tau / seg, (i + 1) * math.tau / seg
            p0 = Vector((r * math.cos(a0), r * math.sin(a0), z))
            p1 = Vector((r * math.cos(a1), r * math.sin(a1), z))
            mid = (p0 + p1) / 2
            vv = B.box_bm(bm, -0.06, 0.06, -(p1 - p0).length / 2, (p1 - p0).length / 2, -0.06, 0.06, mat=mi)
            ang = math.atan2(p1.y - p0.y, p1.x - p0.x) - math.pi / 2
            bmesh.ops.rotate(bm, verts=vv, cent=(0, 0, 0), matrix=Matrix.Rotation(ang, 3, "Z"))
            bmesh.ops.translate(bm, verts=vv, vec=mid)
    for i in range(12):
        a = i * math.tau / 12
        p = Vector((2.2 * math.cos(a), 2.2 * math.sin(a), 0.06))
        B.cyl_bm(bm, p + Vector((0, 0, 0.12)), 0.05, 0.24, segments=8, mat=1)
        B.sphere_bm(bm, p + Vector((0, 0, 0.33)), 0.07, seg=8, rings=6, mat=2)
    B.cyl_bm(bm, (0, 0, 6.0), 0.04, 12.0, segments=8, mat=0)
    return finish(B.mesh_object("PR_EG_Chandelier", bm, mats("EG_Gold", "EG_Marble_Cream", "EG_Lamp_Emissive"), KIT))


def hall_floor_tile(size=12.0):
    """The hall floor far below: cream and walnut squares of 3 m (a giant chessboard), one 12 x 12 m tile."""
    bm = bmesh.new()
    n = int(size / 3)
    for i in range(n):
        for j in range(n):
            x0, y0 = -size / 2 + i * 3, -size / 2 + j * 3
            B.box_bm(bm, x0 + 0.02, x0 + 2.98, y0 + 0.02, y0 + 2.98, -0.3, 0.0, bevel=0.02, segments=1,
                     mat=(i + j) % 2)
    return finish(B.mesh_object("PR_EG_HallFloorTile", bm, mats("EG_Marble_Cream", "EG_Walnut"), KIT, uv_scale=4.0))


def ceiling_beam(length=60.0):
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, -h, h, -0.6, 0.6, -1.2, 0.0, bevel=0.05, mat=0)
    B.box_bm(bm, -h, h, -0.66, 0.66, -1.28, -1.18, bevel=0.02, mat=1)
    B.box_bm(bm, -h, h, -0.66, 0.66, -0.12, -0.02, bevel=0.02, mat=1)
    return finish(B.mesh_object("PR_EG_CeilingBeam", bm, mats("EG_Teal", "EG_Gold"), KIT))


def hall_wall_panel(length=12.0, height=48.0):
    """The hall wall between pillars: stone blocks with joints (background, never walked)."""
    bm = bmesh.new()
    h = length / 2
    course = 1.0
    z = 0.0
    row = 0
    while z < height - 0.01:
        off = 0.0 if row % 2 == 0 else 1.0
        x = -h - off
        while x < h:
            x0, x1 = max(-h, x + 0.02), min(h, x + 2.0 - 0.02)
            if x1 - x0 > 0.1:
                B.box_bm(bm, x0, x1, 0.0, 0.5, z + 0.02, z + course - 0.02, mat=0)
            x += 2.0
        z += course
        row += 1
    B.box_bm(bm, -h, h, 0.45, 0.6, 0.0, height, mat=0)
    o = B.mesh_object("PR_EG_HallWallPanel", bm, mats("EG_Stone"), KIT, smooth_angle=None)
    return finish(o, False)


# ---------------------------------------------------------------------------------------------- device skins
def skin_war_table():
    """R1 board: 16 m map table turning about its axis; pivot = top centre (the board's top is y 0)."""
    bm = bmesh.new()
    B.cyl_bm(bm, (0, 0, -0.25), 8.0, 0.5, segments=128, mat=0, bevel=0.03)
    B.cyl_bm(bm, (0, 0, -0.005), 7.4, 0.012, segments=128, mat=1)
    seg = 128
    for r0, r1, z0, z1, mi in ((7.4, 7.98, -0.02, 0.0, 2),):
        for i in range(seg):
            a0, a1 = i * math.tau / seg, (i + 1) * math.tau / seg
            pts = [(r0 * math.cos(a0), r0 * math.sin(a0)), (r1 * math.cos(a0), r1 * math.sin(a0)),
                   (r1 * math.cos(a1), r1 * math.sin(a1)), (r0 * math.cos(a1), r0 * math.sin(a1))]
            top = [bm.verts.new((x, y, z1)) for x, y in pts]
            f = bm.faces.new(top)
            f.material_index = mi
    for k in range(16):
        a = k * math.tau / 16
        p = Vector((7.7 * math.cos(a), 7.7 * math.sin(a), 0.0))
        v = B.box_bm(bm, -0.18, 0.18, -0.18, 0.18, -0.005, 0.012, mat=2)
        bmesh.ops.translate(bm, verts=v, vec=p)
    o = B.mesh_object("PR_EG_WarTable16", bm, mats("EG_Walnut", "EG_MapPaper", "EG_Gold"), KIT, uv_scale=16.0)
    return finish(o)


def skin_sweep_bar(length=16.4):
    """R1 sweep bar: a gold-banded teal staff, 0.2..0.52 m over the board; pivot on the axis 0.65 m up."""
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, -h, h, -0.225, 0.225, -0.45, -0.13, bevel=0.04, mat=0)
    for x in (-h + 0.3, -h / 2, h / 2, h - 0.3):
        B.box_bm(bm, x - 0.12, x + 0.12, -0.24, 0.24, -0.46, -0.12, bevel=0.02, mat=1)
    B.cyl_bm(bm, (0, 0, -0.2), 0.425, 0.9, segments=32, mat=1, bevel=0.03)
    B.sphere_bm(bm, (0, 0, 0.3), 0.3, seg=20, rings=10, mat=1)
    return finish(B.mesh_object("PR_EG_SweepBar", bm, mats("EG_Teal", "EG_Gold"), KIT))


def skin_ferry(frame_h=0.12):
    """S2 ferry: an 8 x 8 m wooden map deck, flush teal rim, gold brackets under it; pivot = deck top centre."""
    bm = bmesh.new()
    B.box_bm(bm, -4, 4, -4, 4, -0.5, 0.0, bevel=0.03, mat=0)
    B.box_bm(bm, -3.4, 3.4, -3.4, 3.4, 0.0, 0.02, mat=1)
    for s in (-1, 1):
        B.box_bm(bm, s * 4 - 0.3 if s > 0 else -4.0, 4.0 if s > 0 else -3.7, -4, 4, 0.0, frame_h, bevel=0.02, mat=2)
        B.box_bm(bm, -4, 4, s * 4 - 0.3 if s > 0 else -4.0, 4.0 if s > 0 else -3.7, 0.0, frame_h * 0.6, bevel=0.02, mat=2)
    for sx in (-1, 1):
        for sy in (-3.2, -1.1, 1.1, 3.2):
            B.box_bm(bm, sx * 3.6 - 0.25, sx * 3.6 + 0.25, sy - 0.25, sy + 0.25, -1.1, -0.5, bevel=0.03, mat=3)
    for y in (-2.0, 0.0, 2.0):
        B.box_bm(bm, -3.9, 3.9, y - 0.18, y + 0.18, -0.9, -0.5, bevel=0.03, mat=2)
    return finish(B.mesh_object("PR_EG_FerryDeck", bm, mats("EG_Walnut", "EG_MapPaper", "EG_Teal", "EG_Gold"), KIT,
                                uv_scale=8.0))


def skin_hurdle(x0=-5.6, x1=5.6, y0=0.2, y1=0.52, thick=0.45):
    """S2 fixed hurdle: a gold command baton across the chasm, held by teal clamps; pivot = bar centre."""
    bm = bmesh.new()
    zc = (y0 + y1) / 2
    r = (y1 - y0) / 2
    B.cyl_bm(bm, (0, 0, zc), r, x1 - x0, segments=24, mat=0, axis="X")
    for x in (x0 + 0.25, x1 - 0.25):
        B.box_bm(bm, x - 0.25, x + 0.25, -thick / 2, thick / 2, y0 - 0.02, y1 + 0.02, bevel=0.03, mat=1)
    for x in (-2.8, 0.0, 2.8):
        B.cyl_bm(bm, (x, 0, zc), r + 0.02, 0.18, segments=24, mat=1, axis="X")
    o = B.mesh_object("PR_EG_HurdleBaton", bm, mats("EG_Gold", "EG_Teal"), KIT)
    for v in o.data.vertices:
        v.co.z -= zc
    return finish(o)


def skin_shelf_wall(width=6.0):
    """R2 sliding wall (06 stretched): a 6 x 3 x 3 m bookcase block, books on both long faces; pivot = centre
    (1.5 m up). Everything stays inside the collider (touched faces within 2 cm)."""
    bm = bmesh.new()
    hw = width / 2
    B.box_bm(bm, -hw, hw, -0.9, 0.9, -1.5, 1.5, bevel=0.04, mat=0)
    # the pushing ends wear a teal and gold bumper so the moving edge reads from afar
    for sx in (-1, 1):
        x0, x1 = (hw - 0.25, hw) if sx > 0 else (-hw, -hw + 0.25)
        B.box_bm(bm, x0, x1, -1.5, 1.5, -1.5, 1.5, bevel=0.03, mat=2)
        B.box_bm(bm, x0 + 0.05, x1 - 0.05, -1.49, 1.49, 1.3, 1.45, mat=1)
    B.box_bm(bm, -hw + 0.25, hw - 0.25, -1.5, 1.5, 1.38, 1.5, bevel=0.02, mat=1)
    B.box_bm(bm, -hw + 0.25, hw - 0.25, -1.5, 1.5, -1.5, -1.38, bevel=0.02, mat=1)
    o = B.mesh_object("PR_EG_ShelfWall6", bm, mats("EG_Walnut", "EG_Gold", "EG_Teal"), KIT)
    for sy in (-1, 1):
        books = bookshelf_unit(seed=7 + sy, width=width - 0.6, shelves=4, pitch=0.7)
        for v in books.data.vertices:
            v.co = Vector((v.co.x * sy, v.co.y * sy, v.co.z - 1.4))
        join(o, books, offset=Vector((0, sy * 1.2, 0)))
    return finish(o)


def join(dst, src, offset=Vector((0, 0, 0))):
    """Merge src's mesh into dst (materials matched by name), then delete src."""
    bm = bmesh.new()
    bm.from_mesh(dst.data)
    me2 = src.data
    remap = {}
    for i, m in enumerate(me2.materials):
        if m is None:
            remap[i] = 0
            continue
        if m.name not in [x.name for x in dst.data.materials if x]:
            dst.data.materials.append(m)
        remap[i] = [x.name if x else "" for x in dst.data.materials].index(m.name)
    tmp = bmesh.new()
    tmp.from_mesh(me2)
    for f in tmp.faces:
        f.material_index = remap.get(f.material_index, 0)
    bmesh.ops.translate(tmp, verts=tmp.verts, vec=offset)
    me_tmp = bpy.data.meshes.new("tmp")
    tmp.to_mesh(me_tmp)
    tmp.free()
    bm.from_mesh(me_tmp)
    bm.to_mesh(dst.data)
    bm.free()
    bpy.data.meshes.remove(me_tmp)
    bpy.data.objects.remove(src)


def skin_shelf_housing(depth=6.6):
    """The cabinet a sliding wall rests in, outside the way (x 6..12.6 on its side); pivot = mouth centre, floor.
    The wall (3 x 3 m section) slides inside with 2 cm to spare at the sides and 5 cm under the roof."""
    bm = bmesh.new()
    B.box_bm(bm, 0.0, depth, -1.72, -1.52, 0.0, 3.45, bevel=0.02, mat=0)
    B.box_bm(bm, 0.0, depth, 1.52, 1.72, 0.0, 3.45, bevel=0.02, mat=0)
    B.box_bm(bm, 0.0, depth, -1.72, 1.72, 3.05, 3.45, bevel=0.03, mat=0)
    B.box_bm(bm, depth - 0.3, depth, -1.72, 1.72, 0.0, 3.45, bevel=0.03, mat=0)
    B.box_bm(bm, -0.05, depth, -1.77, 1.77, 3.45, 3.6, bevel=0.02, mat=1)
    B.box_bm(bm, 0.0, depth, -1.72, 1.72, -1.0, -0.02, bevel=0.02, mat=0)
    return finish(B.mesh_object("PR_EG_ShelfHousing", bm, mats("EG_Walnut", "EG_Gold"), KIT))


def skin_gate_frame():
    """R2 09 frame: three pillars and the arch with two clock faces; pivot = base centre."""
    bm = bmesh.new()
    for x in (-4.15, 0.0, 4.15):
        B.box_bm(bm, x - 0.15, x + 0.15, -0.5, 0.5, 0.0, 5.6, bevel=0.03, mat=0)
        B.box_bm(bm, x - 0.22, x + 0.22, -0.56, 0.56, 0.0, 0.25, bevel=0.02, mat=1)
    B.box_bm(bm, -4.3, 4.3, -0.6, 0.6, 4.6, 5.8, bevel=0.04, mat=0)
    B.box_bm(bm, -4.36, 4.36, -0.66, 0.66, 5.8, 5.95, bevel=0.02, mat=1)
    for x in (-2.08, 2.08):
        B.cyl_bm(bm, (x, -0.68, 5.4), 0.85, 0.1, segments=48, mat=1, axis="Y")
        B.cyl_bm(bm, (x, -0.74, 5.4), 0.72, 0.04, segments=48, mat=2, axis="Y")
        for k in range(12):
            a = k * math.tau / 12
            p = Vector((x + 0.62 * math.cos(a), -0.77, 5.4 + 0.62 * math.sin(a)))
            v = B.box_bm(bm, -0.03, 0.03, -0.01, 0.01, -0.06, 0.06, mat=1)
            bmesh.ops.translate(bm, verts=v, vec=p)
    # crown crest
    crest = [(-1.2, 5.95), (1.2, 5.95), (1.2, 6.5), (0.8, 6.3), (0.4, 6.75), (0.0, 6.35), (-0.4, 6.75), (-0.8, 6.3), (-1.2, 6.5)]
    v = B.prism_bm(bm, crest, -0.12, 0.12, mat=1)
    bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    return finish(B.mesh_object("PR_EG_GateFrame", bm, mats("EG_Teal", "EG_Gold", "EG_Marble_Cream"), KIT))


def skin_gate_door():
    """R2 09 door, 3.8 x 3.2 x 0.6 m, gold-framed walnut; pivot = centre (1.6 m up when shut)."""
    bm = bmesh.new()
    B.box_bm(bm, -1.9, 1.9, -0.26, 0.26, -1.6, 1.6, bevel=0.03, mat=0)
    for y in (-0.26, 0.26):
        s = 1 if y > 0 else -1
        for x0, x1, z0, z1 in ((-1.9, 1.9, 1.45, 1.6), (-1.9, 1.9, -1.6, -1.45), (-1.9, -1.75, -1.6, 1.6), (1.75, 1.9, -1.6, 1.6),
                               (-0.07, 0.07, -1.45, 1.45), (-1.75, 1.75, -0.07, 0.07)):
            B.box_bm(bm, x0, x1, min(y, y + s * 0.04), max(y, y + s * 0.04), z0, z1, mat=1)
    return finish(B.mesh_object("PR_EG_GateDoor", bm, mats("EG_Walnut", "EG_Gold"), KIT))


def skin_rook_screen(length=6.0):
    """S3 / S4 rook screen: 6 x 3.2 x 2 m teal wall, gold battlements and edges; pivot = centre (1.6 m up)."""
    bm = bmesh.new()
    hl = length / 2
    B.box_bm(bm, -hl, hl, -0.92, 0.92, -1.6, 1.6, bevel=0.05, mat=0)
    n = max(2, round(length / 2.2))
    for i in range(n):
        x = -hl + 0.5 + i * (length - 1.0) / (n - 1)
        B.box_bm(bm, x - 0.5, x + 0.5, -1.0, 1.0, 1.6, 2.1, bevel=0.04, mat=1)
    for sx in (-1, 1):
        B.box_bm(bm, sx * hl - 0.12 if sx > 0 else -hl, hl if sx > 0 else -hl + 0.12, -1.0, 1.0, -1.6, 1.6, bevel=0.02, mat=1)
    for y in (-0.92, 0.92):
        s = 1 if y > 0 else -1
        B.box_bm(bm, -hl + 0.3, hl - 0.3, min(y, y + s * 0.04), max(y, y + s * 0.04), -1.2, 1.2, mat=1)
        B.box_bm(bm, -hl + 0.45, hl - 0.45, min(y, y + s * 0.06), max(y, y + s * 0.06), -1.05, 1.05, mat=0)
        # rook emblem
        B.box_bm(bm, -0.35, 0.35, min(y, y + s * 0.08), max(y, y + s * 0.08), -0.6, 0.5, mat=1)
        for x in (-0.35, 0.0, 0.35):
            B.box_bm(bm, x - 0.09, x + 0.09, min(y, y + s * 0.08), max(y, y + s * 0.08), 0.5, 0.75, mat=1)
    return finish(B.mesh_object("PR_EG_RookScreen6", bm, mats("EG_Teal", "EG_Gold"), KIT))


def skin_pendulum():
    """R3 pendulum: a gold-bound weight (2.2 m ball) on a chain with a rolled war map; pivot = the axis 7.3 m up."""
    bm = bmesh.new()
    B.sphere_bm(bm, (0, 0, -6.0), 1.1, seg=32, rings=16, mat=0)
    for ax in ("X", "Y"):
        B.cyl_bm(bm, (0, 0, -6.0), 1.12, 0.22, segments=48, mat=1, axis=ax)
    B.cyl_bm(bm, (0, 0, -4.75), 0.32, 0.4, segments=24, mat=1)
    z = -0.95
    k = 0
    while z > -4.6:
        v = []
        ring_r, ring_t = 0.18, 0.05
        seg = 12
        rot = 0 if k % 2 == 0 else math.pi / 2
        for i in range(seg):
            a = i * math.tau / seg
            p = Vector((ring_r * math.cos(a), 0, ring_r * 1.4 * math.sin(a)))
            vv = B.box_bm(bm, -ring_t, ring_t, -ring_t, ring_t, -0.06, 0.06, mat=2)
            bmesh.ops.rotate(bm, verts=vv, cent=(0, 0, 0), matrix=Matrix.Rotation(-a, 3, "Y"))
            bmesh.ops.translate(bm, verts=vv, vec=p)
            v += vv
        bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(rot, 3, "Z"))
        bmesh.ops.translate(bm, verts=v, vec=(0, 0, z))
        z -= 0.42
        k += 1
    # the rolled map hanging from the collar, a cloth band with a gold rod
    B.cyl_bm(bm, (0, 0.0, -4.9), 0.12, 1.6, segments=16, mat=1, axis="X")
    B.box_bm(bm, -0.75, 0.75, -0.02, 0.02, -6.0, -4.9, mat=3)
    B.box_bm(bm, -0.22, 0.22, -0.22, 0.22, -0.75, -0.35, bevel=0.03, mat=1)
    return finish(B.mesh_object("PR_EG_PendulumWeight", bm, mats("EG_Iron", "EG_Gold", "EG_Iron", "EG_BannerNavy"), KIT))


def skin_pendulum_gantry(span=15.6, height=8.3):
    """R3 static frame: teal posts outside the way (x +-7..7.8), a gold beam over the pivot; pivot = base centre."""
    bm = bmesh.new()
    for sx in (-1, 1):
        x0, x1 = (-7.8, -7.0) if sx < 0 else (7.0, 7.8)
        B.box_bm(bm, x0, x1, -0.4, 0.4, -3.0, height, bevel=0.04, mat=0)
        B.box_bm(bm, x0 - 0.1, x1 + 0.1, -0.5, 0.5, -3.0, -2.6, bevel=0.03, mat=1)
    B.box_bm(bm, -7.8, 7.8, -0.4, 0.4, 7.8, height, bevel=0.04, mat=1)
    B.box_bm(bm, -0.9, 0.9, -0.75, 0.75, 7.3, 7.8, bevel=0.04, mat=1)
    for sx in (-1, 1):
        v = B.box_bm(bm, -1.2, 1.2, -0.3, 0.3, -0.15, 0.15, mat=1)
        bmesh.ops.rotate(bm, verts=v, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(45 * sx), 3, "Y"))
        bmesh.ops.translate(bm, verts=v, vec=(sx * 6.6, 0, 7.2))
    return finish(B.mesh_object("PR_EG_PendulumGantry", bm, mats("EG_Teal", "EG_Gold"), KIT))


def skin_map_chest_tile():
    """R4 05 tile: a 3 x 0.4 x 3 m map chest lid; pivot = tile centre (0.2 m under its top)."""
    bm = bmesh.new()
    B.box_bm(bm, -1.5, 1.5, -1.5, 1.5, -0.2, 0.2, bevel=0.03, mat=0)
    B.box_bm(bm, -1.3, 1.3, -1.3, 1.3, 0.2, 0.215, mat=1)
    for s in (-1, 1):
        B.box_bm(bm, -1.52, 1.52, s * 1.5 - 0.1 if s > 0 else -1.52, s * 1.52 if s > 0 else -1.4, -0.2, 0.22, bevel=0.01, mat=2)
        B.box_bm(bm, s * 1.5 - 0.1 if s > 0 else -1.52, s * 1.52 if s > 0 else -1.4, -1.52, 1.52, -0.2, 0.22, bevel=0.01, mat=2)
    # a gold arrow pointing north (the way off the tile)
    pts = [(-0.18, -0.6), (0.18, -0.6), (0.18, 0.1), (0.45, 0.1), (0.0, 0.7), (-0.45, 0.1), (-0.18, 0.1)]
    B.prism_bm(bm, pts, 0.215, 0.23, mat=2)
    return finish(B.mesh_object("PR_EG_MapChestTile", bm, mats("EG_Walnut", "EG_MapPaper", "EG_Gold"), KIT))


def skin_wind_vent():
    """R4 15 vent: a bronze grille housing 2.8 x 2.6 x 0.5 m; pivot = base centre, blowing along +y (local +z in Unity)."""
    bm = bmesh.new()
    B.box_bm(bm, -1.4, 1.4, -0.5, 0.0, 0.0, 2.6, bevel=0.05, mat=0)
    for k in range(7):
        z = 0.35 + k * 0.32
        B.box_bm(bm, -1.15, 1.15, 0.0, 0.1, z, z + 0.1, bevel=0.01, mat=1)
    B.box_bm(bm, -1.3, 1.3, -0.02, 0.05, 2.55, 2.75, bevel=0.01, mat=2)
    B.cyl_bm(bm, (0, -0.5, 1.3), 1.0, 0.4, segments=32, mat=0, axis="Y")
    return finish(B.mesh_object("PR_EG_WindVent", bm, mats("EG_Teal", "EG_Gold", "EG_Lamp_Emissive"), KIT))


def skin_map_belt(length=10.0):
    """R5 08 belt: 6 m wide, `length` long, a printed map running on it; pivot = root (bottom centre)."""
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, -3.0, 3.0, -h, h, 0.0, 0.39, bevel=0.03, mat=0)
    B.box_bm(bm, -2.85, 2.85, -h + 0.05, h - 0.05, 0.39, 0.4, mat=1)
    for s in (-1, 1):
        B.box_bm(bm, s * 3.0 - 0.15 if s > 0 else -3.0, 3.0 if s > 0 else -2.85, -h, h, 0.0, 0.405, bevel=0.02, mat=2)
        B.cyl_bm(bm, (0, s * (h - 0.2), 0.22), 0.2, 5.8, segments=24, mat=3, axis="X")
    # direction chevrons printed on the belt (gold), pointing the way the surface runs (local -y = Unity local -z,
    # the prefab's conveyor direction)
    y = h - 1.0
    while y > -h + 0.8:
        pts = [(-0.9, y), (-0.9, y - 0.25), (0.0, y - 0.85), (0.9, y - 0.25), (0.9, y), (0.0, y - 0.6)]
        B.prism_bm(bm, pts, 0.4, 0.404, mat=2)
        y -= 2.0
    return finish(B.mesh_object("PR_EG_MapBelt", bm, mats("EG_Walnut", "EG_MapPaper", "EG_Gold", "EG_Iron"), KIT,
                                uv_scale=6.0))


def skin_giant_pawn():
    """R5 10 piece: a giant marble pawn with a gold collar (2.4 m tall, foot 1.8 m); pivot = base centre."""
    bm = bmesh.new()
    prof = [(0.0, 0.0), (0.9, 0.0), (0.9, 0.18), (0.75, 0.32), (0.55, 0.45), (0.42, 0.9), (0.36, 1.25),
            (0.55, 1.35), (0.55, 1.45), (0.32, 1.52), (0.5, 1.75), (0.53, 1.95), (0.45, 2.15), (0.28, 2.3), (0.0, 2.36)]
    B.lathe_bm(bm, prof, segments=40, mat=0)
    B.cyl_bm(bm, (0, 0, 1.4), 0.58, 0.12, segments=40, mat=1)
    B.cyl_bm(bm, (0, 0, 0.16), 0.93, 0.08, segments=40, mat=1)
    return finish(B.mesh_object("PR_EG_GiantPawn", bm, mats("EG_Marble_Cream", "EG_Gold"), KIT))


def skin_crown_circle(radius=5.0, clip_x=3.0):
    """R5 warning: a gold crown ring of 5 m radius drawn on the floor (lit by the device during its warning).
    Cut at local x = clip_x (the floor edge when the piece stands 3 m in from it); turn it to face the edge."""
    bm = bmesh.new()
    seg = 128
    for r0, r1, z in ((radius - 0.22, radius, 0.012), (radius - 0.55, radius - 0.45, 0.012)):
        outer = [bm.verts.new((r1 * math.cos(i * math.tau / seg), r1 * math.sin(i * math.tau / seg), z)) for i in range(seg)]
        inner = [bm.verts.new((r0 * math.cos(i * math.tau / seg), r0 * math.sin(i * math.tau / seg), z)) for i in range(seg)]
        for i in range(seg):
            f = bm.faces.new((outer[i], outer[(i + 1) % seg], inner[(i + 1) % seg], inner[i]))
            f.material_index = 0
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
    for k in range(8):
        a = k * math.tau / 8
        c = Vector((math.cos(a), math.sin(a), 0)) * (radius - 1.0)
        side = Vector((-math.sin(a), math.cos(a), 0))
        out = Vector((math.cos(a), math.sin(a), 0))
        pts = [c - side * 0.45, c + side * 0.45, c + side * 0.45 + out * 0.55, c + side * 0.2 + out * 0.3,
               c + out * 0.7, c - side * 0.2 + out * 0.3, c - side * 0.45 + out * 0.55]
        vs = [bm.verts.new((p.x, p.y, 0.014)) for p in pts]
        f = bm.faces.new(vs)
        f.material_index = 0
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    if clip_x is not None:
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(clip_x, 0, 0), plane_no=(1, 0, 0), clear_outer=True)
    o = B.mesh_object("PR_EG_CrownCircle", bm, mats("EG_Gold"), KIT, smooth_angle=None)
    return finish(o, False)


def skin_hanging_crate():
    """R5b crate: a 6 x 3 m document crate, deck 0.6 thick, hung by four chains from a trolley 9 m up that rides an
    overhead rail; pivot = deck top centre."""
    bm = bmesh.new()
    B.box_bm(bm, -3.0, 3.0, -1.5, 1.5, -0.6, 0.0, bevel=0.03, mat=0)
    B.box_bm(bm, -2.8, 2.8, -1.3, 1.3, 0.0, 0.015, mat=1)
    for s in (-1, 1):
        B.box_bm(bm, -3.02, 3.02, s * 1.5 - 0.12 if s > 0 else -1.52, 1.52 if s > 0 else -1.38, -0.62, 0.1, bevel=0.02, mat=2)
    for sx in (-2.7, 2.7):
        for sy in (-1.2, 1.2):
            B.box_bm(bm, sx - 0.12, sx + 0.12, sy - 0.12, sy + 0.12, 0.0, 0.25, bevel=0.02, mat=2)
            z = 0.4
            k = 0
            while z < 8.3:
                v = B.box_bm(bm, -0.05, 0.05, -0.12, 0.12, -0.2, 0.2, mat=3) if k % 2 == 0 else \
                    B.box_bm(bm, -0.12, 0.12, -0.05, 0.05, -0.2, 0.2, mat=3)
                bmesh.ops.translate(bm, verts=v, vec=(sx, sy, z))
                z += 0.34
                k += 1
    B.box_bm(bm, -3.2, 3.2, -1.6, 1.6, 8.4, 9.0, bevel=0.05, mat=4)
    for sx in (-2.0, 2.0):
        B.cyl_bm(bm, (sx, 0, 9.3), 0.28, 0.3, segments=20, mat=2, axis="Y")
    return finish(B.mesh_object("PR_EG_HangingCrate", bm, mats("EG_Walnut", "EG_MapPaper", "EG_Gold", "EG_Iron", "EG_Teal"), KIT))


def skin_crane_rail(length=16.0):
    """R5b overhead rail the crate trolleys ride, 9.8 m over the floor, on teal posts outside the way."""
    bm = bmesh.new()
    h = length / 2
    B.box_bm(bm, -3.4, 3.4, -h, h, 9.8, 10.3, bevel=0.04, mat=0)
    B.box_bm(bm, -3.5, 3.5, -h, h, 9.7, 9.8, mat=1)
    for y in (-h + 0.4, h - 0.4):
        for sx in (-1, 1):
            B.box_bm(bm, sx * 7.4 - 0.4, sx * 7.4 + 0.4, y - 0.4, y + 0.4, -3.0, 10.3, bevel=0.04, mat=0)
        B.box_bm(bm, -7.8, 7.8, y - 0.4, y + 0.4, 10.3, 10.8, bevel=0.04, mat=1)
    return finish(B.mesh_object("PR_EG_CraneRail", bm, mats("EG_Teal", "EG_Gold"), KIT))


def skin_compass_disc():
    """R6 01 disc: 8 m, 0.4 m thick, a star-chart face; pivot = root (bottom centre, the top is 0.4 m up)."""
    bm = bmesh.new()
    B.cyl_bm(bm, (0, 0, 0.2), 4.0, 0.4, segments=96, mat=0, bevel=0.03)
    B.cyl_bm(bm, (0, 0, 0.405), 3.8, 0.012, segments=96, mat=1)
    for k in range(3):
        r = 1.25 + k * 1.2
        seg = 96
        outer = [bm.verts.new((r * math.cos(i * math.tau / seg), r * math.sin(i * math.tau / seg), 0.413)) for i in range(seg)]
        inner = [bm.verts.new(((r - 0.06) * math.cos(i * math.tau / seg), (r - 0.06) * math.sin(i * math.tau / seg), 0.413)) for i in range(seg)]
        for i in range(seg):
            f = bm.faces.new((outer[i], outer[(i + 1) % seg], inner[(i + 1) % seg], inner[i]))
            f.material_index = 2
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
    # spiral arms (the swirl that shows it spins)
    for arm in range(4):
        prev = None
        for i in range(24):
            t = i / 23
            a = arm * math.tau / 4 + t * 2.4
            r = 0.4 + t * 3.2
            p = Vector((r * math.cos(a), r * math.sin(a), 0.414))
            if prev is not None:
                d = (p - prev)
                side = Vector((-d.y, d.x, 0)).normalized() * (0.12 * (1 - t) + 0.03)
                vs = [bm.verts.new(prev - side), bm.verts.new(prev + side), bm.verts.new(p + side), bm.verts.new(p - side)]
                f = bm.faces.new(vs)
                f.material_index = 2
                f.normal_update()
                if f.normal.z < 0:
                    f.normal_flip()
            prev = p
    return finish(B.mesh_object("PR_EG_CompassDisc", bm, mats("EG_Teal", "EG_MapPaper", "EG_Gold"), KIT, uv_scale=8.0))


def skin_bridge_panel():
    """S4 12 panel: 3.89 x 0.3 x 6 m metal walkway; pivot = the hinge (its edge at the landing, top level)."""
    bm = bmesh.new()
    B.box_bm(bm, 0.05, 3.94, -3.0, 3.0, -0.3, 0.0, bevel=0.025, mat=0)
    for y in (-2.94, 2.94):
        B.box_bm(bm, 0.05, 3.94, y - 0.06, y + 0.06, 0.0, 0.025, mat=1)
    x = 0.4
    while x < 3.9:
        B.box_bm(bm, x - 0.03, x + 0.03, -2.85, 2.85, 0.0, 0.01, mat=2)
        x += 0.45
    B.cyl_bm(bm, (0.1, 0, -0.15), 0.09, 6.0, segments=16, mat=1, axis="Y")
    return finish(B.mesh_object("PR_EG_BridgePanel", bm, mats("EG_Teal", "EG_Gold", "EG_Iron"), KIT))


def skin_bridge_landing():
    """S4 12 landing: 3 x 0.3 x 6 m; pivot = centre of its top."""
    bm = bmesh.new()
    B.box_bm(bm, -1.5, 1.5, -3.0, 3.0, -0.3, 0.0, bevel=0.025, mat=0)
    B.box_bm(bm, -1.3, 1.3, -2.8, 2.8, 0.0, 0.012, mat=1)
    for s in (-1, 1):
        B.box_bm(bm, s * 1.5 - 0.15 if s > 0 else -1.5, 1.5 if s > 0 else -1.35, -3.0, 3.0, -0.3, 0.02, mat=2)
    return finish(B.mesh_object("PR_EG_BridgeLanding", bm, mats("EG_Teal", "EG_MapPaper", "EG_Gold"), KIT))
