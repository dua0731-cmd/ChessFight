"""The second Tripo batch (10-28): the rook statues on b2, g2, b7, g7 and the study's arched window.

blender -b --factory-startup --python clean_tripo_v2.py -- <raw_dir> <out_dir>
  tripo_raw/rook_pedestal.glb -> SF_Prop_Rook.fbx    a chess rook 1.45 m wide (the old foot) and 0.9 m tall (user 10-28:
                                                     "0.9m 으로 맞추고", "높이만 바꿔야지 너비감은 그대로"; "재질과
                                                     색상도 체스판과 맞춰"): wood UVs at the squares' scale, the game
                                                     draws it in the board's light oak (white's side) or walnut (black's)
  tripo_raw/window_frame.fbx  -> SF_Prop_Window.fbx  the carved arched frame (Tripo) as it is, 28 m wide (user 10-28:
                                                     "창틀에 너가 모델링 한 것을 빼주고"): no glass, no bars added;
                                                     empty Front on the side that faces the board
Like clean_tripo_v1.py: untextured and dense, so decimated, scaled, bottom-centre pivot, colour slots by name
(SF_rrggbb, _m metal).
Then both go into v1/SF_Arena_v1_atmosphere.blend in place of the concept's box window and the lathe pedestals.
(10-28 first pass: a 2.4 m rook with a 1.45 m foot, and glass and bars added to the window; both undone on request.)
"""
import bpy
import bmesh
import math
import os
import sys
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, OUT = args[0], args[1]

STONE, FRAME = "8d8478", "2a1c13"
ROOK_FOOT = 1.45          # the old pedestal's foot: as wide as before (user 10-28: "높이만 바꿔야지 너비감은 그대로")
ROOK_HEIGHT = 0.9         # about the old pedestal's height (0.79 m): still something to hop onto
WOOD_METRES = 6.0         # one texture repeat, as on the squares (build_arena_v1.SQUARE_UV_METRES)
WINDOW_WIDTH = 28.0       # between the bookcases (the room leaves -14..14 free)


def srgb(h):
    c = [int(h[k:k + 2], 16) / 255 for k in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def mat(code):
    name = "SF_" + code
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*srgb(code[:6]), 1)
    b.inputs["Metallic"].default_value = 0.8 if code.endswith("_m") else 0.0
    b.inputs["Roughness"].default_value = 0.75
    if code.endswith("_u"):
        b.inputs["Emission Color"].default_value = (*srgb(code[:6]), 1)
        b.inputs["Emission Strength"].default_value = 1.0
    return m


def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def load(path, budget, name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    imported = list(bpy.context.selected_objects)
    meshes = [o for o in imported if o.type == "MESH"]
    activate(meshes[0])
    for o in meshes:
        o.select_set(True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for junk in [x for x in imported if x.type != "MESH"]:
        bpy.data.objects.remove(junk)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > budget:
        mod = o.modifiers.new("decimate", "DECIMATE")
        mod.ratio = budget / tris
        activate(o)
        bpy.ops.object.modifier_apply(modifier="decimate")
    o.data.materials.clear()
    o.name = name
    return o


def bounds(o):
    vs = [v.co for v in o.data.vertices]
    return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
            Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))


def smooth(o, angle=40):
    activate(o)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    except Exception:
        pass


def export(o, extra=()):
    activate(o)
    for e in extra:
        e.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, o.name + ".fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    lo, hi = bounds(o)
    print(f"[v2] {o.name}: {tuple(round(d, 2) for d in hi - lo)} m, {sum(len(p.vertices) - 2 for p in o.data.polygons)} tris, "
          f"slots {[m.name for m in o.data.materials]}")


def wood_uv(o, metres):
    """UVs projected per face along its main axis, in metres / <metres> (the wood textures' scale on the squares)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    uv = bm.loops.layers.uv.verify()
    bm.normal_update()
    for f in bm.faces:
        ax = max(range(3), key=lambda k: abs(f.normal[k]))
        for loop in f.loops:
            p = loop.vert.co
            u, v = (p.x, p.y) if ax == 2 else (p.y, p.z) if ax == 0 else (p.x, p.z)
            loop[uv].uv = (u / metres + 0.5, v / metres + 0.5)
    bm.to_mesh(o.data)
    bm.free()


def rook():
    o = load(os.path.join(RAW, "rook_pedestal.glb"), 5000, "SF_Prop_Rook")
    lo, hi = bounds(o)
    # As wide as the old pedestal, only its height brought down: a squat rook.
    sw, sh = ROOK_FOOT / max(hi.x - lo.x, hi.y - lo.y), ROOK_HEIGHT / (hi.z - lo.z)
    o.data.transform(Matrix.Diagonal((sw, sw, sh, 1)) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    o.data.update()
    wood_uv(o, WOOD_METRES)
    o.data.materials.append(mat(STONE))   # the game puts the board's oak or walnut in its place
    smooth(o)
    export(o)
    return o


def window():
    """The Tripo frame as it is: scaled to 28 m wide, bottom-centre pivot, one dark wood slot; Front toward the board."""
    o = load(os.path.join(RAW, "window_frame.fbx"), 9000, "SF_Prop_Window")
    lo, hi = bounds(o)
    s = WINDOW_WIDTH / (hi.x - lo.x)
    o.data.transform(Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    lo, hi = bounds(o)
    o.data.materials.append(mat(FRAME))
    smooth(o, 30)
    front = bpy.data.objects.new("Front", None)
    bpy.context.scene.collection.objects.link(front)
    front.parent = o
    front.location = (0.0, lo.y - 2.0, (hi.z - lo.z) / 2)
    export(o, [front])
    return o


rook()
window()

# ---------------------------------------------------------------- into the study
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT, "SF_Arena_v1_atmosphere.blend"))
for ob in list(bpy.data.objects):
    if ob.name.startswith(("Window frame", "Pane", "SF_RookPedestal")) and ob.type == "MESH" and ob.users_collection and \
            ob.users_collection[0].name in ("Atmosphere (concept)", "Arena (preview)"):
        bpy.data.objects.remove(ob)


def bring(name, at, yaw=0.0):
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"))
    root = [x for x in bpy.context.selected_objects if x.parent is None][0]
    root.location = at
    root.rotation_euler.z = yaw
    return root


for x, y in ((-5, -5), (5, -5), (-5, 5), (5, 5)):
    r = bring("SF_Prop_Rook", (x, y, 0.0))
    wood = bpy.data.materials.get("Square_Light" if y < 0 else "Square_Dark")
    if wood:
        r.data.materials[0] = wood
bring("SF_Prop_Window", (0.0, 50.0, -3.0))   # front (-Y) toward the board
# The night behind it (user 10-28: "창틀에 아까처럼 배경을 해줘"): one pale moonlit sheet behind the frame's
# openings, not part of the frame model (the game draws the same sheet: SwordFightArena.Room).
BACKDROP = (0.0, 52.5, -3.0 + 0.5 * 29.0), (0.78 * 28.0, 0.9 * 29.0)
bpy.ops.mesh.primitive_plane_add(size=1.0, location=BACKDROP[0], rotation=(math.pi / 2, 0, 0))
sheet = bpy.context.active_object
sheet.name = "Window night"
sheet.scale = (BACKDROP[1][0], BACKDROP[1][1], 1.0)
# The moonlit night painted on it (make_night_texture.py; user 10-28 "달 배경 만들어"), glowing its own colour.
night = bpy.data.materials.new("Window night")
night.use_nodes = True
nt = night.node_tree
for n in list(nt.nodes):
    nt.nodes.remove(n)
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(os.path.join(os.path.dirname(OUT), "textures", "T_SF_Night.png"))
em = nt.nodes.new("ShaderNodeEmission")
out = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(tex.outputs["Color"], em.inputs["Color"])
nt.links.new(em.outputs[0], out.inputs["Surface"])
sheet.data.materials.append(night)
sc = bpy.context.scene
cam = sc.camera
for name, loc, target, lens in (("SF_Arena_v1_v2", (22, -40, 12), (0, 6, 2), 22),
                                ("SF_Arena_v1_v2_low", (9, -24, 1.5), (0, 4, 3), 20),
                                ("SF_Arena_v1_v2_rook", (-1, -14, 2.2), (-5, -5, 1.0), 30)):
    cam.location = loc
    cam.data.lens = lens
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[v2] rendered {name}.png")
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "SF_Arena_v1_v2.blend"))
print("[v2] done")
