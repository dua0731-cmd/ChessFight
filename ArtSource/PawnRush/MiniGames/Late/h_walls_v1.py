# H King's Chess Hall walls (R510), H's own architecture from its concept (Docs/PawnRush/ConceptArt/R510_KingsChessHall
# H_01 overview, H_02 #7 "Side Wall Section: fluted column, arched window and purple curtain"), not G's wall modules
# (user 11-08, relayed by the I bedchamber chat: "행차 회랑 체스 홀 침실 컨셉 아트에 있는 벽 구조물이 다른데 왜 구조물이
# 다 같아?"). Built straight in Blender from boxes, booleans and swept arches; phase-3 swatch materials by name, box UVs in
# metres / 2. The hall is 12 m high (the H ceiling sits on it).
#   H_WallWindowBay  6.2 m wide, 12 m high, 0.6 m deep cream marble wall: plinth, a panelled dado, a tall round-arched
#                    window (2.6 m wide, sill 2.2 m, crown 9.5 m) with a stepped arch surround, keystone, sill and a
#                    cream mullion grid (no glass, the sky shows: user 11-07), a recessed panel above and a cornice.
#   H_WallPanelBay   the same wall without the window: one tall recessed panel (a banner hangs on it, code cloth).
#   H_WallColumn     a 12 m fluted column (origin on its axis), 1.5 m shaft (24 flutes), square plinth and capital, brass rings above
#                    the base and under the capital (concept: brass rings).
# Origin = bottom centre of the wall's back face (Blender y = 0), the face toward the room is -y. Bays are stretched along
# x in Unity to the column spacing (as G's), so their window is drawn for the 6.2 m side-wall spacing.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python h_walls_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "H")
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
M, B = "PR_Late_MarbleCream", "PR_Late_Brass"

BAY_W, HALL, WALL_D = 6.2, 12., .6
WIN_R, SILL, SPRING = 1.3, 2.2, 8.2          # window half width (= arch radius), sill, arch springing height


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def box(lo, hi, material=M, name="box"):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co = Vector(((lo[0] + hi[0]) / 2 + v.co.x * (hi[0] - lo[0]), (lo[1] + hi[1]) / 2 + v.co.y * (hi[1] - lo[1]),
                       (lo[2] + hi[2]) / 2 + v.co.z * (hi[2] - lo[2])))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat(material))
    return link(bpy.data.objects.new(name, me))


def cylinder_y(cx, cz, r, y0, y1, material=M, seg=48):
    """A disc/cylinder with its axis along y (for the arch cutter)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=y1 - y0)
    me = bpy.data.meshes.new("cyl"); bm.to_mesh(me); bm.free()
    me.transform(Matrix.Translation((cx, (y0 + y1) / 2, cz)) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    me.materials.append(mat(material))
    return link(bpy.data.objects.new("cyl", me))


def cut(target, cutters, op='DIFFERENCE'):
    for c in cutters:
        m = target.modifiers.new("b", 'BOOLEAN'); m.operation = op; m.object = c; m.solver = 'EXACT'
        with bpy.context.temp_override(object=target, active_object=target):
            bpy.ops.object.modifier_apply(modifier=m.name)
        bpy.data.objects.remove(c)


def arch_ring(r0, r1, cz, y0, y1, material=M, seg=32):
    """A half ring (the arch surround) from radius r0 to r1 round (0, cz), from angle 0 to 180 degrees, y0..y1."""
    bm = bmesh.new()
    rows = []
    for i in range(seg + 1):
        a = math.pi * i / seg
        c, s = math.cos(a), math.sin(a)
        rows.append([bm.verts.new((r * c, y, cz + r * s)) for r in (r0, r1) for y in (y0, y1)])
    for i in range(seg):
        a, b = rows[i], rows[i + 1]
        # vertex order per row: (r0,y0) (r0,y1) (r1,y0) (r1,y1)
        bm.faces.new((a[0], b[0], b[2], a[2]))   # front (y0)
        bm.faces.new((a[1], a[3], b[3], b[1]))   # back
        bm.faces.new((a[0], a[1], b[1], b[0]))   # inner
        bm.faces.new((a[2], b[2], b[3], a[3]))   # outer
    for row, flip in ((rows[0], False), (rows[-1], True)):
        f = (row[0], row[2], row[3], row[1])
        bm.faces.new(f[::-1] if flip else f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("arch"); bm.to_mesh(me); bm.free()
    me.materials.append(mat(material))
    return link(bpy.data.objects.new("arch", me))


def panel_frame(x0, x1, z0, z1, y, w=.12, d=.05, material=M):
    """A raised moulding round a rectangle on the wall face (y = the face, the moulding stands d in front)."""
    return [box((x0, y - d, z0), (x1, y, z0 + w), material), box((x0, y - d, z1 - w), (x1, y, z1), material),
            box((x0, y - d, z0), (x0 + w, y, z1), material), box((x1 - w, y - d, z0), (x1, y, z1), material)]


def common(parts, hw, face):
    parts += [box((-hw, face - .14, 0.), (hw, face, .45)),                    # plinth
              box((-hw, face - .06, .45), (hw, face, .55)),
              box((-hw, face - .1, 1.75), (hw, face, 1.9)),                   # dado rail
              box((-hw, face - .2, HALL - .7), (hw, face, HALL - .45)),       # cornice, two steps
              box((-hw, face - .38, HALL - .45), (hw, face, HALL))]
    parts += panel_frame(-hw + .35, hw - .35, .75, 1.55, face)               # dado panel


def window_bay():
    hw, face = BAY_W / 2, -WALL_D
    wall = box((-hw, face, 0.), (hw, 0., HALL), name="H_WallWindowBay")
    cut(wall, [box((-WIN_R, face - .5, SILL), (WIN_R, .5, SPRING)), cylinder_y(0., SPRING, WIN_R, face - .5, .5)])
    parts = [wall]
    common(parts, hw, face)
    # arch surround: jambs and a stepped arch ring in front of the wall, keystone, sill
    parts += [box((-WIN_R - .32, face - .12, SILL), (-WIN_R, face, SPRING)), box((WIN_R, face - .12, SILL), (WIN_R + .32, face, SPRING)),
              arch_ring(WIN_R, WIN_R + .32, SPRING, face - .12, face + .01),
              box((-WIN_R - .45, face - .06, SILL), (-WIN_R - .32, face, SPRING)), box((WIN_R + .32, face - .06, SILL), (WIN_R + .45, face, SPRING)),
              arch_ring(WIN_R + .32, WIN_R + .45, SPRING, face - .06, face + .01),
              box((-.22, face - .2, SPRING + WIN_R - .05), (.22, face, SPRING + WIN_R + .45)),       # keystone
              box((-WIN_R - .5, face - .25, SILL - .18), (WIN_R + .5, face + .01, SILL))]          # sill
    parts += [box((-WIN_R - .4, face - .1, SPRING - .12), (-WIN_R, face, SPRING)), box((WIN_R, face - .1, SPRING - .12), (WIN_R + .4, face, SPRING))]  # imposts
    # mullion grid in the opening's middle (y -0.3), cream: two uprights, crossbars every 1.5 m, a ring and a centre bar in the arch
    y0, y1, b = -.36, -.24, .055
    for x in (-WIN_R / 3, WIN_R / 3):
        top = SPRING + math.sqrt(WIN_R ** 2 - x * x)
        parts.append(box((x - b, y0, SILL), (x + b, y1, top)))
    for z in (SILL + 1.5, SILL + 3., SILL + 4.5, SPRING):
        parts.append(box((-WIN_R, y0, z - b), (WIN_R, y1, z + b)))
    parts.append(arch_ring(WIN_R * .55, WIN_R * .55 + .11, SPRING, y0, y1))
    parts.append(box((-b, y0, SPRING), (b, y1, SPRING + WIN_R)))
    parts += panel_frame(-hw + .5, hw - .5, SPRING + WIN_R + .7, HALL - .95, face)              # panel above the arch
    return join("H_WallWindowBay", parts)


def panel_bay():
    hw, face = BAY_W / 2, -WALL_D
    parts = [box((-hw, face, 0.), (hw, 0., HALL), name="H_WallPanelBay")]
    common(parts, hw, face)
    parts += panel_frame(-hw + .5, hw - .5, 2.2, HALL - 1.1, face, .16, .07)                   # one tall panel
    parts += panel_frame(-hw + .8, hw - .8, 2.5, HALL - 1.4, face, .06, .03)                   # its inner line
    return join("H_WallPanelBay", parts)


def column():
    R, flutes = .75, 24
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=flutes * 4, radius1=R, radius2=R * .96, depth=HALL - 2.6)
    for v in bm.verts:                                                         # concave flutes
        a = math.atan2(v.co.y, v.co.x)
        r = math.hypot(v.co.x, v.co.y)
        if r > .1:
            k = 1. - .045 * max(0., math.cos(a * flutes)) ** 2
            v.co.x *= k; v.co.y *= k
    me = bpy.data.meshes.new("shaft"); bm.to_mesh(me); bm.free()
    me.transform(Matrix.Translation((0, 0, 1.2 + (HALL - 2.6) / 2)))
    me.materials.append(mat(M))
    shaft = link(bpy.data.objects.new("H_WallColumn", me))
    for p in me.polygons: p.use_smooth = True
    def ring(z0, z1, r, material):
        return cylinder_z(r, z0, z1, material)
    parts = [shaft,
             box((-1.0, -1.0, 0.), (1.0, 1.0, .5)), box((-.92, -.92, .5), (.92, .92, .75)),          # square plinth, step
             ring(.75, 1.0, .86, M), ring(1.0, 1.2, .82, B),                                         # torus-like base, brass ring
             ring(HALL - 1.4, HALL - 1.25, .8, B), ring(HALL - 1.25, HALL - .9, .9, M),             # brass ring, echinus
             box((-1.0, -1.0, HALL - .9), (1.0, 1.0, HALL - .55)), box((-1.08, -1.08, HALL - .55), (1.08, 1.08, HALL))]
    ob = join("H_WallColumn", parts)
    return ob


def cylinder_z(r, z0, z1, material, seg=48):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=z1 - z0)
    me = bpy.data.meshes.new("cz"); bm.to_mesh(me); bm.free()
    me.transform(Matrix.Translation((0, 0, (z0 + z1) / 2)))
    me.materials.append(mat(material))
    for p in me.polygons: p.use_smooth = True
    return link(bpy.data.objects.new("cz", me))


def join(name, parts):
    for o in bpy.context.scene.objects: o.select_set(o in parts)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        a = max(range(3), key=lambda i: abs(f.normal[i]))
        u, w = [(1, 2), (0, 2), (0, 1)][a]
        for l in f.loops: l[uv].uv = (l.vert.co[u] / 2., l.vert.co[w] / 2.)
    bm.to_mesh(me); bm.free()
    print("OBJ %-16s tris %6d" % (name, sum(len(p.vertices) - 2 for p in me.polygons)))
    return ob


def export(ob):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


def render(obs):
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_nodes = True
        p = os.path.join(TEX, m.name + "_BaseColor.png")
        b = m.node_tree.nodes.get("Principled BSDF")
        if b is None or not os.path.exists(p): continue
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(p)
        uvn = m.node_tree.nodes.new("ShaderNodeUVMap")
        m.node_tree.links.new(uvn.outputs[0], t.inputs[0])
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
        if m.name == B: b.inputs["Metallic"].default_value = .8
    win, pan, col = obs
    # a wall run as in the hall: column, window bay, column, panel bay, column
    col.location.x = -BAY_W
    win.location.x = -BAY_W / 2
    c2 = col.copy(); c2.location.x = 0; sc.collection.objects.link(c2)
    pan.location.x = BAY_W / 2
    c3 = col.copy(); c3.location.x = BAY_W; sc.collection.objects.link(c3)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, -6, 0))
    bpy.ops.mesh.primitive_cylinder_add(radius=.3, depth=.6, location=(-2, -3, .3))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.25, location=(-2, -3, .8))
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.6, .78, 1, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(55), 0, math.radians(-35))); bpy.context.object.data.energy = 3.5
    cd = bpy.data.cameras.new("cam"); cd.lens = 24
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (3, -19, 5.5); cam.rotation_euler = (math.radians(84), 0, math.radians(8))
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    sc.render.filepath = os.path.join(HERE, "render_h_walls.png")
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
obs = [window_bay(), panel_bay(), column()]
if "--export" in ARGS:
    for ob in obs: export(ob)
if "--render" in ARGS:
    render(obs)
