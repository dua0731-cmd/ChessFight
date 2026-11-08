# H King's Chess Hall board kit (R510): the simple parts made straight in Blender (no Tripo, REUSE.md), sized to the
# game (MgCheckmate). Origin = bottom centre, -y = front. Materials are the phase-3 swatch set by name (tiling,
# box UVs in metres / 2, the swatches are 2 m tiles; velvet 1 m, its .mat repeats twice):
#   H_BoardPillar  the pillar on c6 / f3 / f7: 1.6 x 1.6 x 2.2 m (the game's collider box), cream marble shaft on a
#                  stepped base, matte brass band under a marble capital. The base steps out only 5 cm past the collider.
#   H_FencePost    the low fence post: 0.15 m walnut shaft, 0.55 m high, matte brass cap and knob.
#   H_KingTray     the captured kings' tray: 0.7 x 8 x 0.3 m walnut (the game's solid box; kings lie on its top at 0.3),
#                  purple velvet top with a 4 cm walnut lip round it and brass corner caps.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python h_board_kit_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "H")
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def part(bm, slots, material, lo, hi, bevel=.0, ball=False):
    """A box (or a ball) from lo to hi, bevelled, box UVs in metres / 2, faces on the material's slot."""
    tmp = bmesh.new()
    if ball:
        bmesh.ops.create_uvsphere(tmp, u_segments=16, v_segments=10, radius=.5)
    else:
        bmesh.ops.create_cube(tmp, size=1.)
    for v in tmp.verts:
        v.co.x = (lo[0] + hi[0]) / 2 + v.co.x * (hi[0] - lo[0])
        v.co.y = (lo[1] + hi[1]) / 2 + v.co.y * (hi[1] - lo[1])
        v.co.z = (lo[2] + hi[2]) / 2 + v.co.z * (hi[2] - lo[2])
    if bevel and not ball:
        bmesh.ops.bevel(tmp, geom=tmp.edges[:], offset=bevel, segments=2, affect='EDGES', profile=.5)
    if material not in slots: slots.append(material)
    idx = slots.index(material)
    uv = tmp.loops.layers.uv.new("UVMap")
    for f in tmp.faces:
        f.material_index = idx
        f.smooth = ball
        a = max(range(3), key=lambda i: abs(f.normal[i]))
        u, w = [(1, 2), (0, 2), (0, 1)][a]
        for l in f.loops:
            l[uv].uv = (l.vert.co[u] / 2., l.vert.co[w] / 2.)
    me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); tmp.free()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def finish(name, bm, slots):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for s in slots: me.materials.append(mat(s))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    print("OBJ %-14s tris %5d" % (name, sum(len(p.vertices) - 2 for p in me.polygons)))
    return ob


def pillar():
    bm, s = bmesh.new(), []
    M, B = "PR_Late_MarbleCream", "PR_Late_Brass"
    part(bm, s, M, (-.85, -.85, 0.), (.85, .85, .12), .02)          # plinth
    part(bm, s, M, (-.82, -.82, .12), (.82, .82, .22), .02)         # step
    part(bm, s, M, (-.8, -.8, .22), (.8, .8, 1.78), .03)            # shaft = the collider's 1.6 m
    for z0, z1 in ((.34, .38), (1.62, 1.66)):                        # thin recessed lines (inset panels' edge)
        part(bm, s, M, (-.805, -.805, z0), (.805, .805, z1), .005)
    part(bm, s, B, (-.82, -.82, 1.78), (.82, .82, 1.9), .015)       # matte brass band
    part(bm, s, M, (-.84, -.84, 1.9), (.84, .84, 2.05), .03)        # capital
    part(bm, s, M, (-.8, -.8, 2.05), (.8, .8, 2.2), .04)            # cap
    return finish("H_BoardPillar", bm, s)


def fence_post():
    bm, s = bmesh.new(), []
    W, B = "PR_Late_Walnut", "PR_Late_Brass"
    part(bm, s, W, (-.095, -.095, 0.), (.095, .095, .05), .01)
    part(bm, s, W, (-.075, -.075, .05), (.075, .075, .48), .012)
    part(bm, s, B, (-.095, -.095, .48), (.095, .095, .53), .01)
    part(bm, s, B, (-.045, -.045, .52), (.045, .045, .61), ball=True)
    return finish("H_FencePost", bm, s)


def king_tray():
    bm, s = bmesh.new(), []
    W, V, B = "PR_Late_Walnut", "PR_Late_Velvet", "PR_Late_Brass"
    hx, hy = .35, 4.
    part(bm, s, W, (-hx, -hy, 0.), (hx, hy, .06), .01)               # foot
    part(bm, s, W, (-hx + .03, -hy + .03, .06), (hx - .03, hy - .03, .27), .015)
    part(bm, s, V, (-hx + .04, -hy + .04, .27), (hx - .04, hy - .04, .3))   # velvet top at 0.3 (the game's tray top)
    for side in (-1, 1):                                             # walnut lip round the velvet
        part(bm, s, W, (side * hx - .04 if side > 0 else -hx, -hy, .25), (hx if side > 0 else -hx + .04, hy, .34), .01)
        part(bm, s, W, (-hx, side * hy - .04 if side > 0 else -hy, .25), (hx, hy if side > 0 else -hy + .04, .34), .01)
    for sx in (-1, 1):
        for sy in (-1, 1):
            part(bm, s, B, (sx * hx - .07 if sx > 0 else -hx - .005, sy * hy - .07 if sy > 0 else -hy - .005, 0.),
                 (hx + .005 if sx > 0 else -hx + .07, hy + .005 if sy > 0 else -hy + .07, .35), .008)
    return finish("H_KingTray", bm, s)


# Skylight well (user 11-08, concept H_01: a flat ceiling with framed skylight openings and pane grids, not G's open
# barrel vaults). One well over a 6 x 4.4 m opening; origin = the opening's centre on the ceiling's underside (z 0), the
# well rises 1.3 m to a matte brass pane grid (no glass: open to the sky, user 11-07). Under the ceiling a stepped
# cream marble coffer frame with a brass band hangs 0.34 m down round the opening.
SKY_W, SKY_D, SKY_H = 6., 4.4, 1.3


def ring(bm, s, material, w, d, width, z0, z1, bevel=.015):
    """Four boxes round a w x d opening (inner edge on the opening), `width` wide, from z0 to z1."""
    hx, hy = w / 2, d / 2
    part(bm, s, material, (-hx - width, -hy - width, z0), (hx + width, -hy, z1), bevel)
    part(bm, s, material, (-hx - width, hy, z0), (hx + width, hy + width, z1), bevel)
    part(bm, s, material, (-hx - width, -hy, z0), (-hx, hy, z1), bevel)
    part(bm, s, material, (hx, -hy, z0), (hx + width, hy, z1), bevel)


def skylight_well():
    bm, s = bmesh.new(), []
    M, B = "PR_Late_MarbleCream", "PR_Late_Brass"
    ring(bm, s, M, SKY_W, SKY_D, .5, -.3, 0.)                         # inner coffer step
    ring(bm, s, M, SKY_W + 1., SKY_D + 1., .3, -.16, 0.)              # outer step
    ring(bm, s, B, SKY_W + .04, SKY_D + .04, .06, -.34, -.24, .01)    # brass band round the opening's lower edge
    ring(bm, s, M, SKY_W, SKY_D, .3, 0., SKY_H)                       # well walls
    ring(bm, s, M, SKY_W - .1, SKY_D - .1, .45, SKY_H, SKY_H + .14)   # top lip
    # pane grid: perimeter frame + 3 bars across x, 2 across y (4 x 3 panes), matte brass
    hx, hy, z0, z1, b = SKY_W / 2, SKY_D / 2, SKY_H - .02, SKY_H + .1, .06
    ring(bm, s, B, SKY_W - .2, SKY_D - .2, .1, z0, z1, .01)
    for i in (1, 2, 3):
        x = -hx + i * SKY_W / 4
        part(bm, s, B, (x - b, -hy, z0), (x + b, hy, z1), .01)
    for j in (1, 2):
        y = -hy + j * SKY_D / 3
        part(bm, s, B, (-hx, y - b, z0), (hx, y + b, z1), .01)
    return finish("H_SkylightWell", bm, s)


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
        if os.path.exists(p):
            t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(p)
            m.node_tree.links.new(t.outputs["Color"], m.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    xs = {"H_BoardPillar": 0., "H_FencePost": 1.6, "H_KingTray": 3., "H_SkylightWell": 0.}
    for ob in obs:
        ob.location.x = xs[ob.name]
        if ob.name == "H_KingTray": ob.rotation_euler.z = math.radians(90)
    bpy.ops.mesh.primitive_cylinder_add(radius=.28, depth=.6, location=(-1.6, 0, .3))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.25, location=(-1.6, 0, .8))
    bpy.ops.mesh.primitive_plane_add(size=200)
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.8, .8, .85, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-25))); bpy.context.object.data.energy = 3
    cd = bpy.data.cameras.new("cam"); cd.lens = 35
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (3.5, -10, 4.5); cam.rotation_euler = (math.radians(68), 0, math.radians(14))
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1800, 1000
    sc.view_settings.view_transform = 'AgX'
    sc.render.filepath = os.path.join(HERE, "render_h_board_kit.png")
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
obs = [pillar(), fence_post(), king_tray(), skylight_well()]
if "--export" in ARGS:
    for ob in obs: export(ob)
if "--render" in ARGS:
    render(obs)
