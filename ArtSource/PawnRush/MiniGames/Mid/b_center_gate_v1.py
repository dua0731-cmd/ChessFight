# B centre gate (R503): the user's "Stone Archway Door" FBX as the decorative north door of the B East Wing.
# In the game this door never opens: the teams leave through their own team gates (x +-12, 4 x 4.6 m, rise up)
# and the rear corridor to the 12 m exit at z 36. The door dresses the closed central block
# (MiniGameSectionBuilder: block x -10..10, z 24..30; MiniGamePalaceArt.Shell: closed door 6.7 x 7.6 m at z 23.69;
# phase-2 wall 13 m). Concept: Docs/PawnRush/ConceptArt/R503_RepairWing/B_01_overview.png (centre back door).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_center_gate_v1.py -- [--render] [--export]
#
# Source: Source/R503_StoneArchwayDoor.fbx (1.0 x 0.27 x 0.62, 500k triangles, 4,932 loose pieces, own 4096 colour
# and 2048 normal maps). One uniform scale: the door opening is 0.34 of the width, so it becomes the game's 6.7 m and
# the whole gate 19.7 x 12.2 m (block 20 m, wall 13 m). Origin = door leaf face, floor level, centre; Blender -y faces
# the players. In the section the origin goes at (0, 0, 23.69).
# The bottom skirting of both side wainscots is broken in the source (torn chunks); a clean walnut plinth band covers
# it, with its own material slot "PR_Mid_WalnutFloor" (box UVs, 2 m tile) so Unity gives it the phase-2 floorboard.
# Gold door rings are toned down to matte brass (decorative metal rule).
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R503_StoneArchwayDoor.fbx")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# R503 (team arches): narrowed from 6.7 m so the gate ends at x +-8 and the team arch frames (x +-8.05..15.95) fit.
DOOR_W, S_DOOR_W = 5.44, .34
SCALE = DOOR_W / S_DOOR_W                  # 16.0 (was 19.7)
S_DOOR_Y = -.018                           # source door leaf face
TARGET = 26000                             # triangles after decimation
# broken skirting band in source units: x from the stone pilaster base outwards, z up to the panel bottom rail
SKIRT_X = (.262, .4885)
SKIRT_Z = (None, -.262)                    # bottom = source floor
SKIRT_Y = (-.099, .10)


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
    ob.name = ob.data.name = "B_CenterGate"
    return ob, lo


def decimate(ob):
    before = tris(ob)
    bpy.context.view_layer.objects.active = ob
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
    gold = np.clip((sat - .55) / .15, 0, 1) * np.clip((mx - .22) / .12, 0, 1) * np.clip(1 - np.abs(hue - 42) / 14, 0, 1)
    gold = gold[..., None]
    lum = (rgb * np.array([.2126, .7152, .0722], dtype=np.float32)).sum(-1, keepdims=True)
    brass = np.array([.30, .22, .11], dtype=np.float32) * (lum / .35) ** .8
    px[..., :3] = rgb * (1 - gold) + np.clip(brass, 0, 1) * gold
    print("GOLD texels toned down: %.2f%%" % (100 * float((gold > .5).mean())))
    out = bpy.data.images.new("PR_Mid_CenterGate_BaseColor", w, h)
    out.pixels.foreach_set(px.ravel())
    out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, "PR_Mid_CenterGate_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.copy(); nrm.name = "PR_Mid_CenterGate_Normal"; nrm.colorspace_settings.name = 'Non-Color'
    nrm.filepath_raw = os.path.join(TEX, "PR_Mid_CenterGate_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    imgs["base_color"].image = out; imgs["normal"].image = nrm
    mat.name = "PR_Mid_CenterGate"
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = .8
    bsdf.inputs["Metallic"].default_value = 0.0


def floor_material():
    """PR_Mid_WalnutFloor slot. Uses the baked swatch maps when present (Unity assigns its own .mat by name)."""
    m = bpy.data.materials.new("PR_Mid_WalnutFloor"); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    bc = os.path.join(TEX, "PR_Mid_WalnutFloor_BaseColor.png")
    if os.path.exists(bc):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(bc)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        nm = os.path.join(TEX, "PR_Mid_WalnutFloor_Normal.png")
        if os.path.exists(nm):
            tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(nm)
            tn.image.colorspace_settings.name = 'Non-Color'
            nmap = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(tn.outputs["Color"], nmap.inputs["Color"]); nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])
    else:
        bsdf.inputs["Base Color"].default_value = (.12, .05, .025, 1)
    bsdf.inputs["Roughness"].default_value = .62
    return m


def skirting(ob, s_lo):
    """Clean walnut plinth band over the broken skirting, both sides, joined into the gate with its own slot."""
    mat = floor_material()
    ob.data.materials.append(mat)
    slot = len(ob.data.materials) - 1
    bm = bmesh.new(); bm.from_mesh(ob.data)
    uv = bm.loops.layers.uv.verify()
    z0, z1 = 0., (SKIRT_Z[1] - s_lo) * SCALE
    y0, y1 = (SKIRT_Y[0] - S_DOOR_Y) * SCALE, (SKIRT_Y[1] - S_DOOR_Y) * SCALE
    for side in (-1, 1):
        xa, xb = sorted((side * SKIRT_X[0] * SCALE, side * SKIRT_X[1] * SCALE))
        res = bmesh.ops.create_cube(bm, size=1.)
        for v in res["verts"]:
            v.co = Vector((xa if v.co.x < 0 else xb, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
        faces = {f for v in res["verts"] for f in v.link_faces}
        # One floorboard row (0.2 of the 2 m tile) stretched over the band height, so it reads as one wide board.
        kv = .2 / max(z1, 1e-3)
        for f in faces:
            f.material_index = slot
            n = f.normal
            for l in f.loops:
                p = l.vert.co
                if abs(n.z) > .5: l[uv].uv = (p.x / 2, .4 + p.y * kv)
                elif abs(n.x) > .5: l[uv].uv = (p.y / 2, .4 + p.z * kv)
                else: l[uv].uv = (p.x / 2, .4 + p.z * kv)
    bm.to_mesh(ob.data); bm.free()
    print("SKIRT height %.2f m, x %.2f..%.2f m" % (z1, SKIRT_X[0] * SCALE, SKIRT_X[1] * SCALE))


def proxies():
    """Game stand-ins in gate coordinates (section z - 23.69 = Blender y): floor, aisle, team gates, side blocks, pawns."""
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
    box("floor", (-22, -24, -.05), (22, 13, 0), mat("proxy slate", (.13, .17, .22, 1)))
    box("aisle", (-2, -20, 0), (2, 0, .01), mat("proxy aisle", (.2, .22, .3, 1)))
    team = {-1: mat("team white", (.25, .55, .85, 1)), 1: mat("team black", (.94, .54, .17, 1))}
    block = mat("proxy block", (.75, .72, .66, 1))
    for side in (-1, 1):
        x0, x1 = sorted((side * 10, side * 14))
        box("team gate", (x0, 23 - z0, 0), (x1, 23.4 - z0, 4.6), team[side])
        xa, xb = sorted((side * 14, side * 22))
        box("north block", (xa, 24 - z0, 0), (xb, 30 - z0, 13), block)
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
    w.node_tree.nodes["Background"].inputs[0].default_value = (.9, .8, .68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .55
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(55), 0, math.radians(-25)))
    bpy.context.object.data.energy = 3.2; bpy.context.object.data.color = (1, .9, .78)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((0, -34, 6), (0, 0, 6), 35), "q34": ((-20, -30, 12), (0, 0, 5), 32),
             "close": ((-9, -9, 1.6), (-7, 0, 1.2), 28), "player": ((0, -16, 1.4), (0, 0, 5), 24)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_b_gate_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def main():
    ob, s_lo = load()
    print("DECIMATE", decimate(ob))
    retexture(ob.data.materials[0])
    skirting(ob, s_lo)
    lo = [min(v.co[i] for v in ob.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    print("FIT tris", tris(ob), "lo", [round(x, 2) for x in lo], "hi", [round(x, 2) for x in hi])
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, "B_CenterGate.fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    proxies()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_CenterGate_v1.blend"))
    if "--render" in ARGS: render()


if __name__ == "__main__":
    main()
