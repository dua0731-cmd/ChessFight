# D centre gate (R505): the user's "Medieval Castle Gate" FBX fitted to the closed central block of the mini-game
# section (MiniGameSectionBuilder: block x -10..10, z 24..30; MiniGamePalaceArt.Shell: closed door 6.7 x 7.6 m at
# z 23.69, team gates at x +-12 outside the block, phase-1 wall 11 m).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python d_center_gate_v1.py -- [--render] [--export]
#
# Source: Source/R505_CenterGate_MedievalCastle.fbx (1.0 x 0.35 x 0.62, 500k triangles, 5,585 loose pieces).
# One uniform scale fits: the door opening is 0.3375 of the width, so x SCALE makes it the game's 6.7 m and the whole
# gate 19.9 m wide (block 20 m). Origin = door leaf face, floor level, centre; Blender -y faces the players
# (the entrance). In the section the origin goes at (0, 0, 23.69): the back then lands at z ~30.1.
# Bright gold ornaments (shield crown, spire caps, roof finials) are toned down to matte brass in the base colour
# (phase-1 rule: decorative metal is matte brass, only functional markers glow gold).
import bpy, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mg_holes   # closes Tripo's small face holes (R505)
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R505_CenterGate_MedievalCastle.fbx")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

DOOR_W, S_DOOR_W = 6.7, .3375
SCALE = DOOR_W / S_DOOR_W                  # ~19.85
S_BOTTOM, S_DOOR_Y = -.3075, -.148         # source floor and door leaf face
TARGET = 26000                             # triangles after decimation


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    lo = min(v.co.z for v in ob.data.vertices)
    for v in ob.data.vertices:
        v.co = Vector((v.co.x * SCALE, (v.co.y - S_DOOR_Y) * SCALE, (v.co.z - lo) * SCALE))
    ob.name = ob.data.name = "D_CenterGate"
    return ob


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate(ob):
    before = tris(ob)
    bpy.context.view_layer.objects.active = ob
    # Weld the near-duplicate seams of the AI mesh first so the collapse does not tear pieces apart.
    mod = ob.modifiers.new("weld", 'WELD'); mod.merge_threshold = .004
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = TARGET / tris(ob); mod.use_collapse_triangulate = True
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return before, tris(ob)


def retexture(mat):
    os.makedirs(TEX, exist_ok=True)
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    w, h = base.size
    px = np.array(base.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = np.maximum(mx - mn, 1e-5)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    sat = d / np.maximum(mx, 1e-5)
    # Gold: yellow hue, saturated and bright (linear values). Wood corbels are redder and darker, slate grey.
    gold = np.clip((sat - .55) / .15, 0, 1) * np.clip((mx - .22) / .12, 0, 1) \
        * np.clip(1 - np.abs(hue - 42) / 14, 0, 1)
    gold = gold[..., None]
    lum = (rgb * np.array([.2126, .7152, .0722], dtype=np.float32)).sum(-1, keepdims=True)
    brass = np.array([.30, .22, .11], dtype=np.float32) * (lum / .35) ** .8   # ~#9A8259 matte brass in linear
    px[..., :3] = rgb * (1 - gold) + np.clip(brass, 0, 1) * gold
    print("GOLD texels toned down: %.2f%%" % (100 * float((gold > .5).mean())))
    out = bpy.data.images.new("PR_Early_CenterGate_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel())
    out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Early_CenterGate_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = "PR_Early_CenterGate_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, "PR_Early_CenterGate_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = "PR_Early_CenterGate"
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .85
    bsdf.inputs["Metallic"].default_value = 0.0


def proxies():
    """Game stand-ins in gate coordinates (section z - 23.69 = Blender y): floor, team gates, side blocks, pawns."""
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)
    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
        return m
    def box(name, lo, hi, m):
        c = [(a + b) / 2 for a, b in zip(lo, hi)]; s = [(b - a) / 2 for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(location=c, scale=s)
        ob = bpy.context.object; ob.name = name; ob.data.materials.append(m)
        for u in ob.users_collection: u.objects.unlink(ob)
        col.objects.link(ob); return ob
    z0 = 23.69
    floor = mat("proxy paving", (.78, .72, .62, 1))
    box("floor", (-22, -24, -.05), (22, 13, 0), floor)
    team = {-1: mat("team white", (.25, .75, .85, 1)), 1: mat("team black", (.94, .54, .17, 1))}
    block = mat("proxy block", (.55, .55, .58, 1))
    for side in (-1, 1):
        x0, x1 = sorted((side * 10, side * 14))
        box("team gate", (x0, 23 - z0, 0), (x1, 23.4 - z0, 4.6), team[side])
        xa, xb = sorted((side * 14, side * 22))
        box("north block", (xa, 24 - z0, 0), (xb, 30 - z0, 11), block)
    pawn = mat("proxy pawn", (.95, .93, .9, 1))
    for x, y in ((-1.5, -4), (2.2, -6), (-12, -3)):
        bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(x, y, .31)); o = bpy.context.object
        o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(x, y, .84)); o = bpy.context.object
        o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)


def render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.55, .7, .9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.5; bpy.context.object.data.angle = math.radians(6)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((0, -30, 5), (0, 0, 5), 35), "q34": ((-22, -30, 14), (0, 0, 4), 32),
             "close": ((5, -11, 2.2), (0, 0, 4), 30), "player": ((0, -14, 1.2), (0, 0, 4.5), 24)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_d_gate_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def back_face(ob):
    """Tripo made only the gatehouse's face; from the rear corridor its back showed holes (user, 11-07: "안채워 졌어").
    A copy turned front to back about the gate's middle depth goes inside it, a hair thinner (x0.97 in depth) and
    narrower (x0.998) so no face shares a plane with the original's (no flicker): from behind, the gate's face shows."""
    import bmesh
    from mathutils import Matrix
    ys = [v.co.y for v in ob.data.vertices]
    mid = (min(ys) + max(ys)) / 2
    me = ob.data.copy()
    me.transform(Matrix.Translation((0, mid, 0)) @ Matrix.Diagonal((.998, -.97, 1.0, 1.0)) @ Matrix.Translation((0, -mid, 0)))
    me.flip_normals()
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.from_mesh(me); bm.to_mesh(ob.data); bm.free()
    bpy.data.meshes.remove(me)
    print("GATE back: mirrored copy about y %.2f, %d triangles" % (mid, tris(ob)))


def threshold(ob):
    """The door leaf ends 0.4 m above the floor and the closed block behind the gate is hidden in the game, so the
    sky showed under the door (user, 11-07: "공중에 떠보이지 않게 바닥 매꿔"). Rays through the gate's foot find the
    open span; a stone threshold slab fills it from the floor to just over the door's bottom, through the whole gate
    (front step 10 cm before the door face, out to the mirrored back), in the courtyard's own paving (PR_Early_Paving,
    box-mapped in metres, 2 m a tile)."""
    import bmesh
    from mathutils.bvhtree import BVHTree
    bm0 = bmesh.new(); bm0.from_mesh(ob.data); tree = BVHTree.FromBMesh(bm0); bm0.free()
    vs = np.array([v.co[:] for v in ob.data.vertices]); lo, hi = vs.min(0), vs.max(0)
    xs = np.arange(lo[0] + .05, hi[0], .05)
    def through(x, z):
        loc, *_ = tree.ray_cast(Vector((x, lo[1] - 1, z)), Vector((0, 1, 0)))
        return loc is None
    gap = [x for x in xs if through(x, .2) and abs(x) < 5]
    if not gap: print("THRESHOLD none"); return
    top = .2
    while top < 1.5 and any(through(x, top) for x in gap): top += .02
    x0, x1 = min(gap) - .35, max(gap) + .35                     # into the jambs on both sides
    y0, y1 = -.10, (lo[1] + hi[1]) + .10                        # the door face (y 0) mirrored about the middle depth
    z1 = top + .04
    m = bpy.data.materials.get("PR_Early_Paving") or bpy.data.materials.new("PR_Early_Paving")
    m.use_nodes = True
    t = m.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_Paving_BaseColor.png"))
    m.node_tree.links.new(t.outputs[0], m.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    ob.data.materials.append(m); slot = len(ob.data.materials) - 1
    bm = bmesh.new(); bm.from_mesh(ob.data); uvl = bm.loops.layers.uv.active
    before = set(bm.faces)
    g = bmesh.ops.create_cube(bm, size=1.0)
    for v in g["verts"]:
        v.co = Vector(((x0 + x1) / 2 + v.co.x * (x1 - x0), (y0 + y1) / 2 + v.co.y * (y1 - y0), z1 / 2 + v.co.z * z1))
    edges = list({e for v in g["verts"] for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=edges, offset=.02, segments=2, affect='EDGES', profile=.5)
    for f in set(bm.faces) - before:
        f.material_index = slot; f.normal_update()
        a, b, c = (abs(q) for q in f.normal)
        for l in f.loops:
            co = l.vert.co
            u, w = (co.x, co.y) if c >= max(a, b) else ((co.y, co.z) if a >= b else (co.x, co.z))
            l[uvl].uv = (u / 2.0, w / 2.0)
    bm.to_mesh(ob.data); bm.free()
    print("THRESHOLD x %.2f..%.2f, y %.2f..%.2f, top %.2f (open to %.2f)" % (x0, x1, y0, y1, z1, top))


def main():
    ob = load()
    print("DECIMATE", decimate(ob))
    retexture(ob.data.materials[0])
    mg_holes.close_small_holes(ob, 6.0)
    mg_holes.plug_gaps(ob, mg_holes.stone_brick("PR_Early_WallCore", TEX, (.81, .78, .73)))
    mg_holes.mirror_back(ob)       # replaces back_face (kept below for the record)
    threshold(ob)
    lo = [min(v.co[i] for v in ob.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    print("FIT tris", tris(ob), "lo", [round(x, 2) for x in lo], "hi", [round(x, 2) for x in hi])
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, "D_CenterGate.fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    proxies()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "D_CenterGate_v1.blend"))
    if "--render" in ARGS: render()


main()
