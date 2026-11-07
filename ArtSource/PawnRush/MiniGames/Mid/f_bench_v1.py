# F Clockwork Ballroom capture bench (R508), modelled in Blender: the long velvet bench by the aisle where the taken
# dancers lie (MgEnPassant's "Tray": 1.4 m wide, u 3..19 = 16 m long, top 0.3 m, gold rims each side).
# A walnut base with a recessed plinth, a tufted plum-velvet cushion (PR_Mid_PlumVelvet, 0.6 m tile) whose top is the
# game's tray top, matte brass rails on short posts along both long sides (they stand where the game's gold rims were)
# and round bolsters at the ends. The game's tray collider stays; the dancers still land at its top.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python f_bench_v1.py -- [--render] [--export]
#
# Output Export/F_Bench.fbx: origin at the tray's floor centre, length along Blender y (the game's u), width along x.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")
OUT = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
L, W, TOP = 16., 1.4, .3


def mat(name, tex, rough=.6, metal=0.):
    if name in bpy.data.materials: return bpy.data.materials[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, tex + "_BaseColor.png"))
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m


def box(lo, hi, m, tile, bev=.02, seg=2):
    """Box with world-metre UVs divided by the material's tile."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co = Vector([lo[i] if v.co[i] < 0 else hi[i] for i in range(3)])
    if bev > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bev, offset_type='OFFSET', segments=seg, profile=.5, affect='EDGES', clamp_overlap=True)
    return finish(bm, m, tile)


def finish(bm, m, tile, name="part"):
    uv = bm.loops.layers.uv.new("UVMap")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        n = f.normal; dom = max(range(3), key=lambda i: abs(n[i])); a, b = [i for i in range(3) if i != dom]
        for l in f.loops: l[uv].uv = (l.vert.co[a] / tile, l.vert.co[b] / tile)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(m)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def cyl(a, b, r, m, tile, segs=12):
    a, b = Vector(a), Vector(b); d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=d.length)
    bm.transform(Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4())
    return finish(bm, m, tile)


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    wood = mat("PR_Mid_WalnutFloor", "PR_Mid_WalnutFloor", .55)
    velvet = mat("PR_Mid_PlumVelvet", "PR_Mid_PlumVelvet", .85)
    brass = mat("PR_Mid_MatteBrass", "PR_Mid_MatteBrass", .45, .8)
    hl, hw = L / 2, W / 2
    parts = [box((-hw + .06, -hl + .06, 0.), (hw - .06, hl - .06, .06), wood, 2., .015),       # recessed plinth
             box((-hw, -hl, .06), (hw, hl, .2), wood, 2., .025)]                                # base
    # cushion: top at the tray's top, slightly domed across its width
    bm = bmesh.new()
    nx, ny = 6, 40
    grid = [[bm.verts.new((-hw + .04 + (W - .08) * i / nx, -hl + .25 + (L - .5) * j / ny,
                           TOP - .005 - .025 * ((2 * i / nx - 1) ** 2))) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=-.1)
    parts.append(finish(bm, velvet, .6, "cushion"))
    # round bolsters at the two ends
    for s in (-1, 1):
        parts.append(cyl((-hw + .04, s * (hl - .14), .25), (hw - .04, s * (hl - .14), .25), .1, velvet, .6, 16))
        for x in (-hw + .03, hw - .03):
            parts.append(cyl((x - .02, s * (hl - .14), .25), (x + .02, s * (hl - .14), .25), .11, brass, 1., 16))
    # brass rails on posts along both long sides (where the game's gold rims stood)
    for x in (-hw + .02, hw - .02):
        parts.append(cyl((x, -hl + .1, .34), (x, hl - .1, .34), .022, brass, 1.))
        for k in range(9):
            y = -hl + .1 + (L - .2) * k / 8
            parts.append(cyl((x, y, .2), (x, y, .34), .018, brass, 1., 8))
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = "F_Bench"
    for p in o.data.polygons: p.use_smooth = True
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    print("PART F_Bench", sum(len(p.vertices) - 2 for p in o.data.polygons), "tris")
    return o


def main():
    o = build()
    if "--export" in ARGS:
        os.makedirs(OUT, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "F_Bench.fbx"), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='OFF',
                                 path_mode='STRIP', bake_space_transform=True)
    if "--render" in ARGS:
        sc = bpy.context.scene
        sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1400, 700
        sc.view_settings.view_transform = 'AgX'
        w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.95, .9, .82, 1)
        sc.world = w
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30))); bpy.context.object.data.energy = 3.
        cd = bpy.data.cameras.new("cam"); cd.lens = 40; cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
        tgt = Vector((0, 5.5, .2)); cam.location = tgt + Vector((-3.2, -4.5, 2.2))
        cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_f_bench.png"); bpy.ops.render.render(write_still=True)


main()
