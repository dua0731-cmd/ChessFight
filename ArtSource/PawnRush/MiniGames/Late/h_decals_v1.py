# H King's Chess Hall writing (R510): the asset photo had no text (sheet rule), so the notation tablet's chess symbols
# and move list and the chess clock's numerals are thin alpha-clipped plates just in front of the models' faces,
# with textures rendered here from Windows fonts. Positions were measured on h_props_v1.py's exports by casting rays
# from the front (tablet inner panel at y -0.124, x -0.54..0.56, z 0.34..1.56; dials centred x +-0.2, z 1.085, face
# around y -0.18..-0.20). Each FBX shares its model's origin, so it hangs under the same holder in Unity.
#   H_NotationText  tablet plate 0.84 x 0.86 m at y -0.128: king, knight, rook, pawn symbols, a rule, three moves
#   H_ClockDials    two 0.3 m plates at y -0.208: numerals 1-12 inside the tick marks
#   PR_Late_H_KingBanner  the royal banner texture (purple, cream border, king, V hem) for the code cloth
# Textures: Materials/Textures/PR_Late_H_Notation_BaseColor.png, PR_Late_H_ClockDial_BaseColor.png (RGBA).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python h_decals_v1.py -- [--export]
import bpy, bmesh, math, os, sys
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Export", "H")
TEX = os.path.join(HERE, "Materials", "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SYMBOL = "C:/Windows/Fonts/seguisym.ttf"
SERIF = "C:/Windows/Fonts/georgia.ttf"

TABLET = dict(cx=0.01, cz=.88, w=.84, h=.86, y=-.128)
DIALS = [(-.198, 1.085), (.202, 1.085)]
DIAL_SIZE, DIAL_Y = .3, -.208


def lin(h):
    c = [int(h[i:i + 2], 16) / 255. for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1.,)


def ink(name, colour):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = lin(colour)
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(e.outputs[0], o.inputs["Surface"])
    return m


def text(body, font, size, x, y, m, align="CENTER"):
    cu = bpy.data.curves.new("t", "FONT")
    cu.body = body
    cu.font = bpy.data.fonts.load(font, check_existing=True)
    cu.size = size
    cu.align_x, cu.align_y = align, "CENTER"
    ob = bpy.data.objects.new("t", cu)
    ob.location = (x, y, 0)
    ob.data.materials.append(m)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def rule(x0, x1, y, t, m):
    bpy.ops.mesh.primitive_plane_add(size=1, location=((x0 + x1) / 2, y, 0))
    p = bpy.context.object
    p.scale = (x1 - x0, t, 1)
    p.data.materials.append(m)


def shoot(path, w, h, px):
    sc = bpy.context.scene
    cam = bpy.data.cameras.new("cam"); cam.type = "ORTHO"; cam.ortho_scale = max(w, h)
    co = bpy.data.objects.new("cam", cam); co.location = (0, 0, 5); sc.collection.objects.link(co); sc.camera = co
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.film_transparent = True
    sc.render.resolution_x = px if w >= h else int(px * w / h)
    sc.render.resolution_y = px if h >= w else int(px * h / w)
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("wrote", path)


def tablet_texture():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    w, h = TABLET["w"], TABLET["h"]
    carve = ink("carve", "#6A5440")
    # four piece symbols across the top (king, knight, rook, pawn), a rule, then the opening's first three moves
    for i, s in enumerate("\u2654\u2658\u2656\u2659"):
        text(s, SYMBOL, .3, (i - 1.5) * .19, .28, carve)
    rule(-.33, .33, .165, .012, carve)
    for i, (n, a, b) in enumerate((("1.", "e4", "e5"), ("2.", "Nf3", "Nc6"), ("3.", "Bb5", "a6"))):
        y = .05 - i * .13
        text(n, SERIF, .095, -.3, y, carve, "LEFT")
        text(a, SERIF, .095, -.13, y, carve, "LEFT")
        text(b, SERIF, .095, .12, y, carve, "LEFT")
    text("\u2766", SYMBOL, .09, 0, -.36, carve)       # a small floral end mark (laurel on the concept)
    shoot(os.path.join(TEX, "PR_Late_H_Notation_BaseColor.png"), w, h, 1024)


def dial_texture():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    dark = ink("dark", "#2E241C")
    r = .094
    for n in range(1, 13):
        a = math.radians(90 - n * 30)
        text(str(n), SERIF, .042 if n < 10 else .036, r * math.cos(a), r * math.sin(a), dark)
    shoot(os.path.join(TEX, "PR_Late_H_ClockDial_BaseColor.png"), DIAL_SIZE, DIAL_SIZE, 512)


BANNER_W, BANNER_H = 1.9, 5.2     # the code cloth's size on H's panel bays (MiniGameModels.ChessHall)


def banner_texture():
    """H's royal banner (concept H_01 / H_03 #8): purple velvet, a cream border, a big cream king, a V hem (alpha)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    w, h, notch = BANNER_W, BANNER_H, .55
    purple, cream = ink("purple", "#4E2A6A"), ink("cream", "#EFE4CC")

    def shape(m, inset, z):
        bm = bmesh.new()
        hw, hh = w / 2 - inset, h / 2 - inset
        vs = [bm.verts.new(p) for p in ((-hw, hh, z), (-hw, -hh + notch, z), (0, -hh, z), (hw, -hh + notch, z), (hw, hh, z))]
        bm.faces.new(vs)
        me = bpy.data.meshes.new("s"); bm.to_mesh(me); bm.free()
        me.materials.append(m)
        bpy.context.scene.collection.objects.link(bpy.data.objects.new("s", me))
    shape(cream, 0., 0.)               # the border shows round the purple
    shape(purple, .09, .001)
    shape(cream, .16, .002)            # a thin inner line
    shape(purple, .19, .003)
    # the king: the silhouette of G's king model (Export/G/G_King.fbx) in flat cream, 2.3 m high, standing up the banner
    bpy.ops.import_scene.fbx(filepath=os.path.join(HERE, "Export", "G", "G_King.fbx"))
    king = [o for o in bpy.context.selected_objects if o.type == "MESH"][0]
    king.data.transform(king.matrix_world); king.matrix_world.identity()
    zs = [v.co.z for v in king.data.vertices]
    s = 2.3 / (max(zs) - min(zs))
    king.data.transform(Matrix.Translation((0, .3 - 1.15, .6)) @ Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Scale(s, 4) @ Matrix.Translation((0, 0, -min(zs))))
    for i in range(len(king.data.materials)): king.data.materials[i] = cream
    shoot(os.path.join(TEX, "PR_Late_H_KingBanner_BaseColor.png"), w, h, 1400)


def quad(bm, uv, cx, cz, w, h, y):
    """A plate facing -y (front), UV 0..1 left-to-right, bottom-to-top as seen from the front."""
    vs = [bm.verts.new((cx - w / 2, y, cz - h / 2)), bm.verts.new((cx + w / 2, y, cz - h / 2)),
          bm.verts.new((cx + w / 2, y, cz + h / 2)), bm.verts.new((cx - w / 2, y, cz + h / 2))]
    f = bm.faces.new(vs)
    f.normal_update()
    if f.normal.y > 0: f.normal_flip()
    for l, (u, v) in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        l[uv].uv = (u, v) if f.normal.y < 0 else (1 - u, v)


def plate(name, material, quads):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for q in quads: quad(bm, uv, *q)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(bpy.data.materials.get(material) or bpy.data.materials.new(material))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def export(ob):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


tablet_texture()
dial_texture()
banner_texture()
if "--export" in ARGS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    t = TABLET
    export(plate("H_NotationText", "PR_Late_H_Notation", [(t["cx"], t["cz"], t["w"], t["h"], t["y"])]))
    export(plate("H_ClockDials", "PR_Late_H_ClockDial", [(x, z, DIAL_SIZE, DIAL_SIZE, DIAL_Y) for x, z in DIALS]))
