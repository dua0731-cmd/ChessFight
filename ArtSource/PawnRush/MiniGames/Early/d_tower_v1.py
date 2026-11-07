# D corner tower (R505): the user's Tripo "castle tower" (from AssetRefs/D_assets_05_tower.png) fitted to the back
# corners of the court, where the 3.25 m side walls meet the shell's 11 m north wall (MiniGamePalaceArt.Facade).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python d_tower_v1.py -- [--render] [--export]
#
# Source: Source/R505_CornerTower_CastleTower.fbx (+ .fbm textures; 0.29 x 0.29 x 0.98, 94k triangles, 4 pieces).
# One uniform scale makes it HEIGHT tall (above the north wall); the arched window is turned to Blender -y (the
# holder's front), so MiniGameModels aims it at the room. Origin: the base's centre on the floor.
import bpy, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mg_holes

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R505_CornerTower_CastleTower.fbx")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

HEIGHT = 13.5            # m; the north wall is 11 m
TARGET = 14000           # triangles after decimation (two copies, large on screen)


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    vs = np.array([v.co[:] for v in ob.data.vertices])
    lo, hi = vs.min(0), vs.max(0)
    k = HEIGHT / (hi[2] - lo[2]); c = (lo + hi) / 2
    ob.data.transform(Matrix.Diagonal((k, k, k, 1)) @ Matrix.Translation((-c[0], -c[1], -lo[2])))
    ob.name = ob.data.name = "D_CornerTower"
    return ob


def decimate(ob):
    before = tris(ob)
    for kind, setup in (('WELD', lambda m: setattr(m, "merge_threshold", .004)),
                        ('DECIMATE', lambda m: (setattr(m, "ratio", TARGET / tris(ob)), setattr(m, "use_collapse_triangulate", True)))):
        mod = ob.modifiers.new(kind.lower(), kind); setup(mod)
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    return before, tris(ob)


def base_pixels(mat):
    img = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and n.image.name.endswith("_0"))
    w, h = img.size
    return img, np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)


def face_window(ob):
    """Turns the model so its arched oak window (the only brown, saturated wood on it) looks toward Blender -y."""
    img, px = base_pixels(ob.data.materials[0])
    h, w = px.shape[:2]
    uvl = ob.data.uv_layers.active.data
    pts = []
    for p in ob.data.polygons:
        if p.center.z < HEIGHT * .5: continue
        u, v = sum((uvl[i].uv for i in p.loop_indices), Vector((0, 0))) / p.loop_total
        r, g, b = px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w))), :3]
        if r > g * 1.25 > b * 1.25 and r < .45:      # dark warm wood, not the cream stone
            pts.append(p.center.xy.copy())
    if not pts:
        print("WINDOW not found"); return
    c = sum(pts, Vector((0, 0))) / len(pts)
    a = math.atan2(c.y, c.x)
    turn = -math.pi / 2 - a                           # bring that azimuth to -y
    ob.data.transform(Matrix.Rotation(turn, 4, 'Z'))
    print("WINDOW %d wood faces at azimuth %.0f deg, turned %.0f deg" % (len(pts), math.degrees(a), math.degrees(turn)))


def retexture(mat):
    os.makedirs(TEX, exist_ok=True)
    nodes = mat.node_tree.nodes
    img, px = base_pixels(mat)
    out = bpy.data.images.new("PR_Early_CornerTower_BaseColor", *img.size)
    out.pixels.foreach_set(px.ravel()); out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Early_CornerTower_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nimg = next(n.image for n in nodes if n.type == 'TEX_IMAGE' and n.image and n.image.name.endswith("_3"))
    nrm = nimg.copy(); nrm.name = "PR_Early_CornerTower_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.scale(2048, 2048)
    nrm.filepath_raw = os.path.join(TEX, "PR_Early_CornerTower_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    for n in list(nodes):
        if n.type == 'TEX_IMAGE' and n.image is img: n.image = out
        elif n.type == 'TEX_IMAGE' and n.image is nimg: n.image = nrm
        elif n.type == 'TEX_IMAGE': nodes.remove(n)              # roughness / metallic maps: plain values instead
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .88; bsdf.inputs["Metallic"].default_value = 0.0
    mat.name = "PR_Early_CornerTower"


def render(ob):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1200, 1300
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.55, .7, .9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.5; bpy.context.object.data.angle = math.radians(6)
    # the D wall module beside it (its face looks -y like the tower's window), for colour and size
    wall = os.path.join(OUT_FBX, "D_Wall.fbx")
    if os.path.exists(wall):
        bpy.ops.import_scene.fbx(filepath=wall)
        for o in bpy.context.selected_objects:
            o.scale = (1.3, 1.3, 1.3); o.location = (-4.6, 0, 0)
    m = bpy.data.materials.new("pawn"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.95, .93, .9, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(2.6, -2.2, .31)); bpy.context.object.data.materials.append(m)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(2.6, -2.2, .84)); bpy.context.object.data.materials.append(m)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((-1.5, -26, 6.5), (-1.5, 0, 6.2), 35), "q34": ((14, -20, 8), (0, 0, 6.5), 35),
             "top": ((3, -8, 16.5), (0, 0, 12.4), 30), "base": ((3.5, -7, 1.6), (0, 0, 1.6), 30)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_d_tower_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def main():
    ob = load()
    face_window(ob)
    print("DECIMATE", decimate(ob))
    mg_holes.close_small_holes(ob, 2.0)
    retexture(ob.data.materials[0])
    vs = np.array([v.co[:] for v in ob.data.vertices])
    print("FIT tris", tris(ob), "lo", vs.min(0).round(2), "hi", vs.max(0).round(2))
    # radius by height (for the placement next to the walls and the station)
    for z in (0.3, 1.5, 4, 8, 11, 12.5):
        sl = vs[np.abs(vs[:, 2] - z) < .25]
        if len(sl): print("RADIUS at %.1f m: %.2f" % (z, np.hypot(sl[:, 0], sl[:, 1]).max()))
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, "D_CornerTower.fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "D_CornerTower_v1.blend"))
    if "--render" in ARGS: render(ob)


main()
