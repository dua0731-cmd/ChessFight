# H King's Chess Hall team gate crest (R510): H reuses G's team portal frame, whose crest is G's rook medallion. H is
# the king's hall, so its crest carries a king instead: the same seat as G_TeamGateCrest (2.2 m across, origin at its
# back centre, -y = front, centred in z; MiniGameModels hangs it at the same spot), a matte brass rim and front ring,
# a bronze field (PR_Late_Bronze, R510 swatch) and G's king model (Export/G/G_King.fbx) pressed into a 1.5 m brass
# relief (depth x 0.3).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python h_crest_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "H")
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def box_uv(me):
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        a = max(range(3), key=lambda i: abs(f.normal[i]))
        u, w = [(1, 2), (0, 2), (0, 1)][a]
        for l in f.loops: l[uv].uv = (l.vert.co[u] / 2., l.vert.co[w] / 2.)
    bm.to_mesh(me); bm.free()


def disc(name, r, y0, y1, material):
    """A round plate facing -y from y0 (front) to y1 (back)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=64, radius1=r, radius2=r, depth=y1 - y0)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.transform(Matrix.Translation((0, (y0 + y1) / 2, 0)) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    box_uv(me)
    me.materials.append(mat(material))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = [disc("rim", 1.1, -.14, 0., "PR_Late_Brass"),
             disc("field", .96, -.17, -.1, "PR_Late_Bronze")]
    bpy.ops.mesh.primitive_torus_add(major_radius=1.02, minor_radius=.075, major_segments=64, minor_segments=10,
                                     location=(0, -.17, 0), rotation=(math.radians(90), 0, 0))
    ring = bpy.context.object
    ring.data.transform(ring.matrix_world); ring.matrix_world.identity()
    box_uv(ring.data); ring.data.materials.append(mat("PR_Late_Brass"))
    for p in ring.data.polygons: p.use_smooth = True
    parts.append(ring)
    bpy.ops.import_scene.fbx(filepath=os.path.join(HERE, "Export", "G", "G_King.fbx"))
    king = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    king.data.transform(king.matrix_world); king.matrix_world.identity()
    co = [v.co for v in king.data.vertices]
    h = max(c.z for c in co) - min(c.z for c in co)
    ymax = max(c.y for c in co)
    k = 1.5 / h
    # scale to 1.5 m, centre it, press its depth to 0.3 and seat its back half-way into the field
    king.data.transform(Matrix.Translation((0, -.17, -.75)) @ Matrix.Diagonal((k, k * .3, k, 1)) @ Matrix.Translation((0, -ymax, 0)))
    for i in range(len(king.data.materials)): king.data.materials[i] = mat("PR_Late_Brass")
    parts.append(king)
    for o in bpy.context.scene.objects: o.select_set(o in parts)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = "H_TeamGateCrest"
    import numpy as np
    c = np.array([v.co[:] for v in ob.data.vertices])
    print("CREST size %s  y %.3f..%.3f  tris %d" % (np.ptp(c, 0).round(3), c[:, 1].min(), c[:, 1].max(),
          sum(len(p.vertices) - 2 for p in ob.data.polygons)))
    return ob


def export(ob):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


def render(ob):
    sc = bpy.context.scene
    for m in bpy.data.materials:
        m.use_nodes = True
        p = os.path.join(TEX, m.name + "_BaseColor.png")
        b = m.node_tree.nodes.get("Principled BSDF")
        if b is None: continue
        b.inputs["Metallic"].default_value = .8; b.inputs["Roughness"].default_value = .4
        if os.path.exists(p):
            t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(p)
            m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.85, .82, .78, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(60), 0, math.radians(-30))); bpy.context.object.data.energy = 3
    cd = bpy.data.cameras.new("cam"); cd.lens = 50
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (2.2, -6.5, -1.); cam.rotation_euler = (math.radians(98), 0, math.radians(19))
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = 900
    sc.view_settings.view_transform = 'AgX'
    sc.render.filepath = os.path.join(HERE, "render_h_crest.png")
    bpy.ops.render.render(write_still=True)


ob = build()
if "--export" in ARGS: export(ob)
if "--render" in ARGS: render(ob)
