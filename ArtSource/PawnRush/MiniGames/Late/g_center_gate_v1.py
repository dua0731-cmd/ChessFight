# G centre gate (R504): the user's "Royal Stone Gateway" FBX as the decorative north door of the G Royal Procession.
# The user: "출구 게이트는 따로 줄게 저건 장식용 가운데에" - it dresses the closed central door; the teams leave through
# their own team gates (x +-12, the user's exit gates come separately). Same pipeline as the B and D centre gates
# (Mid/b_center_gate_v1.py, R503/R505). Concept: Docs/PawnRush/ConceptArt/R504_RoyalProcession/G_01_overview.png.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python g_center_gate_v1.py -- [--render] [--export]
#
# Source: Source/R504_RoyalStoneGateway.fbx (1.0 x 0.44 x 0.65, 500k triangles, 2,515 loose pieces, own 4096 colour and
# 2048 normal maps). One uniform scale of 16 (the user's choice): door opening 0.316 -> 5.06 m, the whole gate
# 16 x 10.4 m, so it stays clear of the team portals (inner edge x +-9.75) and under the 17 m phase-3 wall.
# Origin = door leaf face (source y -0.095), floor level, centre; Blender -y faces the players. In the section the
# origin goes at (0, 0, 23.69), the closed central door's face (MiniGamePalaceArt.Shell).
# Colour (phase-3 rules, PAWN_RUSH_ART_PHASES): the peach sandstone becomes cream marble #DDD7C9 (luminance kept, so
# the carved shading stays), the bright gold crown / rings / column bands become matte brass (decorative metal).
# The navy door leaves and the dark iron straps stay as they are.
import bpy, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R504_RoyalStoneGateway.fbx")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

SCALE = 16.
S_DOOR_Y = -.095                           # source door leaf face
TARGET = 30000                             # triangles after decimation (one big building: 10k-30k, the poly budget the user set via the D session 11-07)
CREAM = np.array([.867, .843, .788], np.float32)      # #DDD7C9
BRASS = np.array([.30, .23, .12], np.float32)         # matte brass, as the B gate


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SRC)
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    lo = min(v.co.z for v in ob.data.vertices)
    for v in ob.data.vertices:
        v.co = Vector((v.co.x * SCALE, (v.co.y - S_DOOR_Y) * SCALE, (v.co.z - lo) * SCALE))
    ob.name = ob.data.name = "G_CenterGate"
    return ob


def decimate(ob):
    before = tris(ob)
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new("weld", 'WELD'); mod.merge_threshold = .004
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = TARGET / tris(ob); mod.use_collapse_triangulate = True
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Tripo meshes come flat-shaded (crumpled-looking panels): smooth, sharp only above 35 degrees
    ob.data.polygons.foreach_set("use_smooth", [True] * len(ob.data.polygons))
    ob.data.set_sharp_from_angle(angle=math.radians(35.))
    return before, tris(ob)


def hsv(rgb):
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = np.maximum(mx - mn, 1e-5)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return hue, d / np.maximum(mx, 1e-5), mx


def retexture(mat):
    os.makedirs(TEX, exist_ok=True)
    nodes = mat.node_tree.nodes
    imgs = {n.image.name: n for n in nodes if n.type == 'TEX_IMAGE' and n.image}
    base, normal = imgs["base_color"].image, imgs["normal"].image
    w, h = base.size
    px = np.array(base.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    hue, sat, val = hsv(rgb)
    lum = (rgb * np.array([.2126, .7152, .0722], dtype=np.float32)).sum(-1, keepdims=True)
    # gold: saturated, bright, yellow-orange (the B gate's mask)
    gold = np.clip((sat - .55) / .15, 0, 1) * np.clip((val - .22) / .12, 0, 1) * np.clip(1 - np.abs(hue - 42) / 14, 0, 1)
    # sandstone: warm hue (red-orange to yellow), some saturation, not gold
    warm = np.clip(1 - np.abs(hue - 25) / 22, 0, 1) * np.clip((sat - .08) / .1, 0, 1) * (1 - gold)
    gold, warm = gold[..., None], warm[..., None]
    ref = float(np.median(lum[warm[..., 0] > .5])) if (warm > .5).any() else .55
    stone = np.clip(CREAM * (lum / ref) ** .85, 0, 1)      # the median stone texel becomes exactly #DDD7C9
    brass = np.clip(BRASS * (lum / .35) ** .8, 0, 1)
    rgb = rgb * (1 - warm) + stone * warm
    rgb = rgb * (1 - gold) + brass * gold
    px[..., :3] = rgb
    print("RECOLOUR stone %.1f%%  gold %.1f%%  (stone median lum %.3f -> cream)" %
          (100 * float((warm > .5).mean()), 100 * float((gold > .5).mean()), ref))
    out = bpy.data.images.new("PR_Late_CenterGate_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel())
    out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Late_CenterGate_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = "PR_Late_CenterGate_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, "PR_Late_CenterGate_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = "PR_Late_CenterGate"
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .45      # polished marble, phase 3
    bsdf.inputs["Metallic"].default_value = 0.0


def proxies():
    """Game stand-ins in gate coordinates (section z - 23.69 = Blender y): floor, aisle, team portals, wall, balcony, pawns."""
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)

    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
        return m

    def box(name, lo, hi, m):
        c = [(a + b) / 2 for a, b in zip(lo, hi)]; s = [(b - a) / 2 for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(location=c, scale=s)
        o = bpy.context.object; o.name = name; o.data.materials.append(m)
        for u in o.users_collection: u.objects.unlink(o)
        col.objects.link(o); return o
    z0 = 23.69
    box("floor", (-22, -24, -.05), (22, 13, 0), mat("proxy navy dais", (.03, .04, .07, 1)))
    box("runner", (-1.2, -24, 0), (1.2, 0, .03), mat("proxy runner", (.12, .05, .2, 1)))
    wall = mat("proxy wall", (.7, .67, .6, 1))
    box("north wall above gate", (-9.65, 24.05 - z0, 0), (9.65, 25.95 - z0, 17), wall)
    team = {-1: mat("team blue", (.25, .55, .85, 1)), 1: mat("team orange", (.94, .54, .17, 1))}
    for side in (-1, 1):
        x0, x1 = sorted((side * 9.75, side * 14.25))
        box("team portal", (x0, 23.8 - z0, 0), (x1, 24.2 - z0, 5.8), team[side])
        xa, xb = sorted((side * 14.35, side * 22))
        box("north wall side", (xa, 24.05 - z0, 0), (xb, 25.95 - z0, 17), wall)
    box("balcony slab", (-21.5, 26.8 - 1.5 - z0, 10.475), (21.5, 26.8 + 1.5 - z0, 10.925), wall)
    pawn = mat("proxy pawn", (.95, .93, .9, 1))
    for x, y in ((-1.2, -4), (1.8, -7), (-12, -3)):
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
    w.node_tree.nodes["Background"].inputs[0].default_value = (.75, .78, .9, 1)     # cool skylight
    w.node_tree.nodes["Background"].inputs[1].default_value = .6
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(35), 0, math.radians(-20)))
    bpy.context.object.data.energy = 3.0; bpy.context.object.data.color = (1, .96, .9)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((0, -34, 7), (0, 0, 7), 35), "q34": ((-20, -30, 12), (0, 0, 5), 32),
             "close": ((-5, -8, 1.6), (-2, 0, 2.5), 28), "player": ((0, -16, 1.4), (0, 0, 6), 24)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_g_gate_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def main():
    ob = load()
    print("DECIMATE", decimate(ob))
    retexture(ob.data.materials[0])
    lo = [min(v.co[i] for v in ob.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    print("FIT tris", tris(ob), "lo", [round(x, 2) for x in lo], "hi", [round(x, 2) for x in hi])
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, "G_CenterGate.fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    proxies()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "G_CenterGate_v1.blend"))
    if "--render" in ARGS: render()


if __name__ == "__main__":
    main()
