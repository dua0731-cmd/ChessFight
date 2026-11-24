# The promotion pad (StationPromotion), v2: Codex design 2 "pressure plate" (11-22, user "2번으로 해서 정밀하게 재질 똑같이
# 블렌더에서 작업해"; concept Docs/PawnRush/ConceptArt/PromotionPad/pad_design_02.png). Replaces v1 (the crown medallion).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python promotion_pad_v2.py -- --bake --render --export --save
#
#   --bake    bakes the three surface textures (brushed gold, dark veined stone, cream marble) from procedural nodes to
#             Assets/Resources/PawnRushMiniGames/Common/T_PromoPad_*.png (+ .meta the first time)
#   --render  render_promotion_pad_v2.png (the concept's view) and _top.png, with the lamps half lit as in the concept
#   --export  Assets/Resources/PawnRushMiniGames/Common/PR_PromotionPad.fbx (+ .meta the first time)
#   --save    PromotionPad_v2.blend
#
# Origin = the pad's middle on the dais top; Blender +y = Unity +z (north), z up. UVs are top-down over the whole pad
# (u = (x + 1.15) / 2.3, v likewise), so each texture covers the pad once with no repeat. Parts by name (the game sets
# their materials, StationPromotion.DressModel):
#   PR_PromoPad_Base        the low slab under everything; its edge shows as the thin outer band (team colour, pale)
#   PR_PromoPad_Frame       four dark veined stone bars
#   PR_PromoPad_Corners     four cream marble corner blocks
#   PR_PromoPad_Rivets      the gold domes on the corner blocks
#   PR_PromoPad_Recess      the dark seam round the plate
#   PR_PromoPad_Plate       the brushed gold plate (dims when the pad is spent)
#   PR_PromoPad_Lamp_00~23  square gold studs on the frame, six a side, clockwise from the north-west; lit one by one
#                           over the 2 s hold (the concept's glowing studs)
# No collider (the dais carries the pawns); the plate is 5.6 cm, the rivet tops 7.4 cm.
import bpy, bmesh, math, os, sys, hashlib
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
OUT_REL = "Assets/Resources/PawnRushMiniGames/Common"
FBX_REL = OUT_REL + "/PR_PromotionPad.fbx"
FBX_META = os.path.join(ROOT, "Assets/Resources/PawnRushGarden/RoyalWaterGarden.fbx.meta")
TEX_META = os.path.join(ROOT, "Assets/Art/PawnRushMap/Endgame/Textures/T_EG_BannerNavy_BaseColor.png.meta")
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
TEX_SIZE = 1024
UV_HALF = 1.15

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# ---- sizes (metres)
BASE_HALF, BASE_TOP = 1.13, .024          # the slab under all; 4 cm of it shows outside the frame
FRAME_OUT, FRAME_IN, FRAME_TOP = 1.08, .82, .052
CORNER, CORNER_TOP, CORNER_CHAMFER = .27, .060, .06
PLATE_HALF, PLATE_R, PLATE_TOP = .775, .12, .056
RECESS_TOP = .012
STUD, STUD_H = .078, .014
STUD_AT = (-.60, -.36, -.12, .12, .36, .60)
FRAME_MID = (FRAME_OUT + FRAME_IN) / 2
FLOOR = .002


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(v / 12.92) if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in c] + [1.0]


def guid(rel):
    return hashlib.md5(("chessfight:" + rel).encode()).hexdigest()


def write(rel, text):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def ensure_folder():
    if not os.path.exists(os.path.join(ROOT, OUT_REL + ".meta")):
        os.makedirs(os.path.join(ROOT, OUT_REL), exist_ok=True)
        write(OUT_REL + ".meta", "fileFormatVersion: 2\nguid: %s\n%s" % (guid(OUT_REL), FOLDER))


# ------------------------------------------------------------------ textures (procedural, baked)

def node(tree, kind, **inputs):
    n = tree.nodes.new(kind)
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def ramp(tree, stops):
    r = tree.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    els[0].position, els[0].color = stops[0]
    for pos, col in stops[1:]:
        e = els.new(pos)
        e.color = col
    return r


def texture_graph(name, tree, uv):
    """The colour output socket of one surface's procedural graph."""
    L = tree.links
    if name == "Stone":
        # dark warm grey, cloudy, with pale thin veins (the concept's frame)
        cloud = node(tree, "ShaderNodeTexNoise", Scale=4.0, Detail=10.0, Roughness=.62)
        L.new(uv, cloud.inputs["Vector"])
        base = ramp(tree, [(0.0, srgb("#3d3936")), (.5, srgb("#4b4743")), (1.0, srgb("#5a5550"))])
        L.new(cloud.outputs["Fac"], base.inputs["Fac"])
        warp = node(tree, "ShaderNodeTexNoise", Scale=2.2, Detail=6.0)
        L.new(uv, warp.inputs["Vector"])
        mix_uv = node(tree, "ShaderNodeVectorMath")
        mix_uv.operation = "ADD"
        L.new(uv, mix_uv.inputs[0])
        scale = node(tree, "ShaderNodeVectorMath")
        scale.operation = "SCALE"
        L.new(warp.outputs["Color"], scale.inputs[0])
        scale.inputs["Scale"].default_value = .35
        L.new(scale.outputs[0], mix_uv.inputs[1])
        wave = node(tree, "ShaderNodeTexWave", Scale=2.6, Distortion=6.0, Detail=4.0)
        wave.wave_profile = "SIN"
        L.new(mix_uv.outputs[0], wave.inputs["Vector"])
        vein = ramp(tree, [(0.0, (0, 0, 0, 1)), (.975, (0, 0, 0, 1)), (.992, (.75, .75, .75, 1)), (1.0, (.75, .75, .75, 1))])
        L.new(wave.outputs["Fac"], vein.inputs["Fac"])
        mix = tree.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MIX"
        L.new(vein.outputs["Color"], mix.inputs["Factor"])
        L.new(base.outputs["Color"], mix.inputs["A"])
        mix.inputs["B"].default_value = srgb("#77716a")
        return mix.outputs["Result"]
    if name == "Marble":
        cloud = node(tree, "ShaderNodeTexNoise", Scale=3.0, Detail=8.0, Roughness=.55)
        L.new(uv, cloud.inputs["Vector"])
        base = ramp(tree, [(0.0, srgb("#e9dcc7")), (.5, srgb("#f2e8d8")), (1.0, srgb("#f8f1e6"))])
        L.new(cloud.outputs["Fac"], base.inputs["Fac"])
        wave = node(tree, "ShaderNodeTexWave", Scale=3.4, Distortion=9.0, Detail=5.0)
        L.new(uv, wave.inputs["Vector"])
        vein = ramp(tree, [(0.0, (0, 0, 0, 1)), (.965, (0, 0, 0, 1)), (.99, (.7, .7, .7, 1)), (1.0, (.7, .7, .7, 1))])
        L.new(wave.outputs["Fac"], vein.inputs["Fac"])
        mix = tree.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        L.new(vein.outputs["Color"], mix.inputs["Factor"])
        L.new(base.outputs["Color"], mix.inputs["A"])
        mix.inputs["B"].default_value = srgb("#d9c4a6")
        return mix.outputs["Result"]
    # Gold: brushed along x - fine streaks, a soft broad sheen
    stretch = node(tree, "ShaderNodeMapping")
    stretch.inputs["Scale"].default_value = (1.0, 90.0, 1.0)
    L.new(uv, stretch.inputs["Vector"])
    streak = node(tree, "ShaderNodeTexNoise", Scale=6.0, Detail=12.0, Roughness=.7)
    L.new(stretch.outputs["Vector"], streak.inputs["Vector"])
    broad = node(tree, "ShaderNodeTexNoise", Scale=1.4, Detail=2.0)
    L.new(uv, broad.inputs["Vector"])
    add = node(tree, "ShaderNodeMath")
    add.operation = "MULTIPLY_ADD"
    L.new(streak.outputs["Fac"], add.inputs[0])
    add.inputs[1].default_value = .7
    L.new(broad.outputs["Fac"], add.inputs[2])
    norm = node(tree, "ShaderNodeMapRange")
    norm.inputs["From Min"].default_value = .55
    norm.inputs["From Max"].default_value = 1.25
    L.new(add.outputs[0], norm.inputs["Value"])
    col = ramp(tree, [(0.0, srgb("#e0a72e")), (.5, srgb("#f2c041")), (1.0, srgb("#fde28a"))])
    L.new(norm.outputs["Result"], col.inputs["Fac"])
    return col.outputs["Color"]


def bake_textures():
    ensure_folder()
    scene = bpy.context.scene
    try:
        scene.render.engine = "CYCLES"
    except TypeError:
        pass
    scene.cycles.samples = 1
    scene.cycles.device = "CPU"
    bpy.ops.mesh.primitive_plane_add(size=2)
    plane = bpy.context.active_object
    paths = {}
    for name in ("Gold", "Stone", "Marble"):
        mat = bpy.data.materials.new("bake_" + name)
        tree = mat.node_tree
        for n in list(tree.nodes):
            tree.nodes.remove(n)
        out = tree.nodes.new("ShaderNodeOutputMaterial")
        emit = tree.nodes.new("ShaderNodeEmission")
        coord = tree.nodes.new("ShaderNodeTexCoord")
        uv = coord.outputs["UV"]
        tree.links.new(texture_graph(name, tree, uv), emit.inputs["Color"])
        tree.links.new(emit.outputs[0], out.inputs["Surface"])
        img = bpy.data.images.new("T_PromoPad_" + name, TEX_SIZE, TEX_SIZE)
        target = tree.nodes.new("ShaderNodeTexImage")
        target.image = img
        tree.nodes.active = target
        plane.data.materials.clear()
        plane.data.materials.append(mat)
        bpy.ops.object.select_all(action="DESELECT")
        plane.select_set(True)
        bpy.context.view_layer.objects.active = plane
        bpy.ops.object.bake(type="EMIT")
        rel = OUT_REL + "/T_PromoPad_%s.png" % name
        img.filepath_raw = os.path.join(ROOT, rel)
        img.file_format = "PNG"
        img.save()
        if not os.path.exists(os.path.join(ROOT, rel + ".meta")):
            with open(TEX_META, encoding="utf-8") as f:
                lines = f.read().split("\n")
            lines[1] = "guid: " + guid(rel)
            write(rel + ".meta", "\n".join(lines))
        paths[name] = os.path.join(ROOT, rel)
        print("BAKED", rel)
    bpy.data.objects.remove(plane)
    return paths


# ------------------------------------------------------------------ geometry

def area2(o, a, b, c):
    (x0, y0), (x1, y1), (x2, y2) = o[a], o[b], o[c]
    return (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)


def prism(name, outline, z0, z1, mat, bevel=0.0, segments=3):
    bm = bmesh.new()
    low = [bm.verts.new((x, y, z0)) for x, y in outline]
    high = [bm.verts.new((x, y, z1)) for x, y in outline]
    for a, b, c in tessellate_polygon([[Vector((x, y, 0.0)) for x, y in outline]]):
        tri = [a, b, c] if area2(outline, a, b, c) > 0 else [a, c, b]
        bm.faces.new([high[k] for k in tri])
        bm.faces.new([low[k] for k in reversed(tri)])
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((low[i], low[j], high[j], high[i]))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1), verts=bm.verts, edges=bm.edges, delimit=set())
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    if bevel > 0:
        mod = ob.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(35)
        mod.harden_normals = False
    return ob


def pyramid(name, cx, cy, size, z0, h, mat):
    """A low square stud: a flat-topped pyramid."""
    s, t = size / 2, size * .22
    bm = bmesh.new()
    low = [bm.verts.new((cx + x, cy + y, z0)) for x, y in ((-s, -s), (s, -s), (s, s), (-s, s))]
    high = [bm.verts.new((cx + x, cy + y, z0 + h)) for x, y in ((-t, -t), (t, -t), (t, t), (-t, t))]
    bm.faces.new(list(reversed(low)))
    bm.faces.new(high)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((low[i], low[j], high[j], high[i]))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    mod = ob.modifiers.new("Bevel", "BEVEL")
    mod.width, mod.segments = .004, 2
    return ob


def dome(name, cx, cy, r, z0, h, mat):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=1.0)
    for v in list(bm.verts):
        if v.co.z < -1e-4:
            bm.verts.remove(v)
    for v in bm.verts:
        v.co = Vector((cx + v.co.x * r, cy + v.co.y * r, z0 + max(v.co.z, 0) * h))
    bmesh.ops.contextual_create(bm, geom=[e for e in bm.edges if e.is_boundary])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    return ob


def rounded_square(half, r, seg=8):
    pts = []
    for cx, cy, a0 in ((half - r, -half + r, -90), (half - r, half - r, 0), (-half + r, half - r, 90), (-half + r, -half + r, 180)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def join(objects, name):
    for ob in objects:
        bpy.context.view_layer.objects.active = ob
        for m in list(ob.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    ob = objects[0]
    ob.name = ob.data.name = name
    return ob


def top_down_uvs(ob):
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            n = poly.normal
            if abs(n.z) > .5:
                u, v = co.x, co.y
            elif abs(n.x) > abs(n.y):
                u, v = co.y, co.z * 4 + co.x
            else:
                u, v = co.x, co.z * 4 + co.y
            uv[li].uv = ((u + UV_HALF) / (2 * UV_HALF), (v + UV_HALF) / (2 * UV_HALF))


def surface(name, tex_path, fallback, metal, rough, emission=None):
    m = bpy.data.materials.new(name)
    tree = m.node_tree
    p = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
    if tex_path and os.path.exists(tex_path):
        t = tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(tex_path, check_existing=True)
        tree.links.new(t.outputs["Color"], p.inputs["Base Color"])
    else:
        p.inputs["Base Color"].default_value = srgb(fallback)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if emission:
        p.inputs["Emission Color"].default_value = srgb(emission)
        p.inputs["Emission Strength"].default_value = 3.0
    m.diffuse_color = srgb(fallback)
    return m


def build(tex):
    gold = surface("Gold", tex.get("Gold"), "#e2ad3a", 1.0, .28)
    stone = surface("Stone", tex.get("Stone"), "#4a4541", 0, .55)
    marble = surface("Marble", tex.get("Marble"), "#f2e8d8", 0, .35)
    band = surface("Team", None, "#9ccbe0", 0, .45)
    dark = surface("Recess", None, "#1d1b1a", .3, .6)
    stud = surface("Stud", None, "#d9a537", 1.0, .3)
    lit = surface("StudLit", None, "#ffc94a", .3, .3, emission="#ffb733")

    parts = []
    sq = lambda h: [(-h, -h), (h, -h), (h, h), (-h, h)]
    parts.append(prism("PR_PromoPad_Base", sq(BASE_HALF), FLOOR, BASE_TOP, band, .008))

    # frame: north and south bars run between the corner blocks, east and west likewise (no overlaps)
    bars = []
    e = FRAME_OUT - CORNER
    bars.append(prism("bar_s", [(-e, -FRAME_OUT), (e, -FRAME_OUT), (e, -FRAME_IN), (-e, -FRAME_IN)], BASE_TOP - .004, FRAME_TOP, stone, .008))
    bars.append(prism("bar_n", [(-e, FRAME_IN), (e, FRAME_IN), (e, FRAME_OUT), (-e, FRAME_OUT)], BASE_TOP - .004, FRAME_TOP, stone, .008))
    bars.append(prism("bar_w", [(-FRAME_OUT, -e), (-FRAME_IN, -e), (-FRAME_IN, e), (-FRAME_OUT, e)], BASE_TOP - .004, FRAME_TOP, stone, .008))
    bars.append(prism("bar_e", [(FRAME_IN, -e), (FRAME_OUT, -e), (FRAME_OUT, e), (FRAME_IN, e)], BASE_TOP - .004, FRAME_TOP, stone, .008))
    parts.append(join(bars, "PR_PromoPad_Frame"))

    # corner blocks: square, the inner corner cut off (as in the concept), each with a gold dome
    corners, rivets = [], []
    c, k = CORNER, CORNER_CHAMFER
    for sx in (-1, 1):
        for sy in (-1, 1):
            ox, oy = sx * FRAME_OUT, sy * FRAME_OUT           # outer corner
            ix, iy = sx * (FRAME_OUT - c), sy * (FRAME_OUT - c)  # inner corner
            pts = [(ox, oy), (ix, oy), (ix, iy + sy * k), (ix + sx * k, iy), (ox, iy)]
            if sum((pts[i][0] * pts[(i + 1) % 5][1] - pts[(i + 1) % 5][0] * pts[i][1]) for i in range(5)) < 0:
                pts = list(reversed(pts))
            corners.append(prism("corner", pts, BASE_TOP - .004, CORNER_TOP, marble, .010))
            mx, my = sx * (FRAME_OUT - c * .48), sy * (FRAME_OUT - c * .48)
            rivets.append(dome("rivet", mx, my, .05, CORNER_TOP - .002, .016, stud))
    parts.append(join(corners, "PR_PromoPad_Corners"))
    parts.append(join(rivets, "PR_PromoPad_Rivets"))

    # the dark seam the plate sinks into, then the plate: a thick pillow-edged rounded square
    parts.append(prism("PR_PromoPad_Recess", sq(FRAME_IN + .002), BASE_TOP - .004, BASE_TOP + RECESS_TOP - .008, dark, 0))
    parts.append(prism("PR_PromoPad_Plate", rounded_square(PLATE_HALF, PLATE_R, 10), BASE_TOP, PLATE_TOP, gold, .016, 5))

    # the lamp studs: six a side, clockwise from the north-west, all the same mesh; the game lights them in order
    spots = []
    for x in STUD_AT:
        spots.append((x, FRAME_MID))           # north, west to east
    for y in reversed(STUD_AT):
        spots.append((FRAME_MID, y))           # east, north to south
    for x in reversed(STUD_AT):
        spots.append((x, -FRAME_MID))          # south, east to west
    for y in STUD_AT:
        spots.append((-FRAME_MID, y))          # west, south to north
    for i, (x, y) in enumerate(spots):
        ob = pyramid("PR_PromoPad_Lamp_%02d" % i, x, y, STUD, FRAME_TOP - .002, STUD_H, stud)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier="Bevel")
        parts.append(ob)

    for ob in parts:
        bpy.context.view_layer.objects.active = ob
        for m in list(ob.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
        top_down_uvs(ob)
    return parts, lit, stud


def render(parts, lit, path):
    scene = bpy.context.scene
    # the concept's charge: the six studs along one side lit (here the west side, the concept's lower left)
    for ob in parts:
        if ob.name.startswith("PR_PromoPad_Lamp_") and int(ob.name[-2:]) >= 18:
            ob.data.materials[0] = lit
    bpy.ops.mesh.primitive_plane_add(size=5.2, location=(0, 0, 0))
    floor = bpy.context.active_object
    slate = bpy.data.materials.new("preview_floor")
    p = next(n for n in slate.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = srgb("#4b4d52")
    p.inputs["Roughness"].default_value = .8
    floor.data.materials.append(slate)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 3.3
    # the concept: looking at the pad's south-west corner, about 38 degrees down
    d = 6.0
    elev, az = math.radians(34), math.radians(-135)
    cam.location = (d * math.cos(elev) * math.cos(az), d * math.cos(elev) * math.sin(az), d * math.sin(elev))
    cam.rotation_euler = (Vector((0, 0, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 4.0
    sun.data.angle = math.radians(12)
    sun.rotation_euler = (math.radians(38), 0, math.radians(-60))
    scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("w")
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.75, .75, .77, 1)
    bg.inputs["Strength"].default_value = 1.3
    scene.world = world
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        if e in engines:
            try:
                scene.render.engine = e
                break
            except TypeError:
                pass
    scene.render.resolution_x = scene.render.resolution_y = 1254
    try:
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.exposure = -.55
    except TypeError:
        pass
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    cam.location = (0, 0, 6)
    cam.rotation_euler = (0, 0, 0)
    cam.data.ortho_scale = 2.6
    scene.render.filepath = path.replace(".png", "_top.png")
    bpy.ops.render.render(write_still=True)
    keep = {p.name for p in parts}
    for ob in list(scene.objects):
        if ob.name not in keep:
            bpy.data.objects.remove(ob)
    stud = bpy.data.materials["Stud"]
    for ob in parts:
        if ob.name.startswith("PR_PromoPad_Lamp_"):
            ob.data.materials[0] = stud


def export(parts):
    ensure_folder()
    bpy.ops.object.select_all(action="DESELECT")
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, FBX_REL), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, colors_type="LINEAR")
    if not os.path.exists(os.path.join(ROOT, FBX_REL + ".meta")):
        with open(FBX_META, encoding="utf-8") as f:
            lines = f.read().split("\n")
        lines[1] = "guid: " + guid(FBX_REL)
        write(FBX_REL + ".meta", "\n".join(lines))
    tris, top = 0, 0.0
    for ob in parts:
        tris += sum(len(p.vertices) - 2 for p in ob.data.polygons)
        top = max(top, max(v.co.z for v in ob.data.vertices))
    print("EXPORT %s  %d objects  %d tris  top %.3f m" % (FBX_REL, len(parts), tris, top))


bpy.ops.wm.read_factory_settings(use_empty=True)
tex = {}
if "--bake" in ARGS:
    tex = bake_textures()
else:
    for name in ("Gold", "Stone", "Marble"):
        p = os.path.join(ROOT, OUT_REL, "T_PromoPad_%s.png" % name)
        if os.path.exists(p):
            tex[name] = p
parts, lit, stud = build(tex)
if "--render" in ARGS:
    render(parts, lit, os.path.join(HERE, "render_promotion_pad_v2.png"))
if "--export" in ARGS:
    export(parts)
if "--save" in ARGS:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "PromotionPad_v2.blend"))
