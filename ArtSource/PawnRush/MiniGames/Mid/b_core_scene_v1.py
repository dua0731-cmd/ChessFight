# B East Wing core scene (R503): the user's three Tripo sets, made from the GPT asset sheets
# (Docs/PawnRush/ConceptArt/R503_RepairWing/AssetRefs), split into single assets and fitted to MgPlanks' numbers.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_core_scene_v1.py -- [--render] [--export]
#
# Sources (Source/, each 1 m wide, z up, the assets scattered on the ground plane):
#   R503_RusticBuildingComponents: basement slab (unused: came out brown), scaffold bay, joist beam, floor-edge piece (unused)
#   R503_StylizedWoodProps:        laid plank (unused: ragged ends), standing plank, plank stack (unused), work lamp
#   R503_CarpentrySupplies:        sawhorse with saw, tool chest, paint buckets, rolled dust sheet, sawdust pile
# Every asset keeps its set's own texture (one material per set: PR_Mid_RusticKit / WoodKit / CarpentryKit).
#
# Outputs (Export/), all in MgPlanks' game frame: Blender x = v (across), y = u (along the station), z = up.
#   B_Plank.fbx       one plank fitted to a unit cube (centre origin). MiniGameModels hangs it inside every plank box
#                     the game makes (laid 2.5 x 0.3 x 2, pile and carried 2.5 x 0.12 x 2), so the box's scale sizes it.
#                     Five walnut boards run along x (the 2.5 m side), gold nail heads.
#   B_StationEnv.fbx  the chasm's underworld and the station's work props, game frame of one station (symmetric in v,
#                     so it serves both teams): basement 4 m down (floor and four walls in PR_Mid_BasementStone,
#                     2 m tile), seven joists along u at v -10..10 (top 0.32 under the floor, just under the laid planks),
#                     eight scaffold bays from the basement floor up to the joists, two lit work lamps in the basement,
#                     props in the corners away from the pile-bridge-gate path. No colliders: the game's stay.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# MgPlanks (game frame)
CHASM_U = (6., 16.)
HALF_V = 10.                 # MissionStation.Half
BASE_Y = -4.                 # basement floor
JOIST_TOP = -.32
JOIST_V = [-10., -6.25, -2.5, 0., 2.5, 6.25, 10.]   # under both plank columns, then across the gap
SCAFFOLD_AT = [(v, u) for v in (-7.6, 7.6) for u in (8.6, 13.4)] + [(-4.4, 11.), (4.4, 11.)]
LAMP_AT = [(-6., 11.), (6., 11.)]
# props: (asset, v, u, yaw degrees, size along its longest ground axis in metres)
PROPS = [("sawhorse", -8.6, .9, 15, 1.3), ("chest", 8.4, 18.6, -10, 1.0), ("buckets", 6.9, 18.9, 0, .95),
         ("roll", -8.0, 18.7, 70, 1.5), ("sawdust", -6.3, 19.0, 0, 1.0)]


# ------------------------------------------------------------------ loading and splitting
def load(name):
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"))
    ob = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    ob.name = name
    return ob


def split(ob, rule):
    """Separate mesh islands into named objects. rule(centroid) -> asset name or None (dropped)."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    seen = np.zeros(len(bm.faces), bool)
    groups = {}
    for f in bm.faces:
        if seen[f.index]: continue
        stack = [f]; seen[f.index] = True; isl = []
        while stack:
            g = stack.pop(); isl.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if not seen[h.index]:
                        seen[h.index] = True; stack.append(h)
        c = Vector()
        for i in isl[:64]:
            c += bm.faces[i].calc_center_median()
        c /= min(len(isl), 64)
        key = rule(c)
        if key: groups.setdefault(key, []).extend(isl)
    out = {}
    for key, idx in groups.items():
        keep = set(idx)
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new(key); b2.to_mesh(me); b2.free()
        for m in ob.data.materials: me.materials.append(m)
        o = bpy.data.objects.new(key, me); bpy.context.scene.collection.objects.link(o)
        out[key] = o
        print("SPLIT", ob.name, key, len(me.polygons), "faces")
    bm.free()
    bpy.data.objects.remove(ob)
    return out


def rustic_rule(c):
    if c.x < -.24: return None                                       # basement slab (brown)
    if c.x < .045 and c.y > .07: return "scaffold"
    if (c.x > .25 and c.y < -.01) or c.x > .30: return None          # floor-edge piece
    return "beam"


def wood_rule(c):
    if c.x < -.03: return None                                       # laid plank (ragged)
    if c.x < .1 and c.y < .06: return "plank"
    if c.x > .38 and c.y > .27: return "lamp"
    return None                                                      # stack on sawhorses


def carpentry_rule(c):
    if c.x < -.28: return "sawhorse"
    if c.x < -.069: return "chest"
    if c.x > .355 or (c.x > .289 and c.y < -.01): return "sawdust"
    if c.x > .15 or (c.x > .121 and c.y < -.033): return "roll"
    return "buckets"


# ------------------------------------------------------------------ fitting
def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("weld", 'WELD'); m.merge_threshold = .0004
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier=m.name)
    if tris(o) > target:
        m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = target / tris(o); m.use_collapse_triangulate = True
        with bpy.context.temp_override(object=o, active_object=o):
            bpy.ops.object.modifier_apply(modifier=m.name)
    return tris(o)


def coords(o):
    a = np.empty(len(o.data.vertices) * 3, np.float32); o.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def xform(o, M):
    o.data.transform(M); o.data.update()


def ground_align(o):
    """Turn the asset about z so its long ground axis is x (PCA of the footprint), centre it, ground at z 0."""
    p = coords(o)
    xy = p[:, :2] - p[:, :2].mean(0)
    w, v = np.linalg.eigh(np.cov(xy.T))
    ax = v[:, np.argmax(w)]
    ang = math.atan2(ax[1], ax[0])
    xform(o, Matrix.Rotation(-ang, 4, 'Z'))
    p = coords(o)
    lo, hi = p.min(0), p.max(0)
    xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))
    return hi - lo


def fit_box(o, size, centre_z=False):
    """Non-uniform scale to an exact box size (x, y, z), centred in x/y, ground or centre in z."""
    p = coords(o); lo, hi = p.min(0), p.max(0); d = hi - lo
    S = Matrix.Diagonal((size[0] / d[0], size[1] / d[1], size[2] / d[2], 1.))
    xform(o, Matrix.Translation(-Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2 if centre_z else lo[2]))))
    xform(o, S)


def uniform(o, longest):
    p = coords(o); d = p.max(0) - p.min(0)
    s = longest / max(d[0], d[1]); xform(o, Matrix.Scale(s, 4))


def place_copy(src, name, M):
    me = src.data.copy(); me.transform(M)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def kit_material(o, name):
    """The set's Tripo material renamed to the kit name, its maps saved as 2048 PNGs (once per kit)."""
    mat = o.data.materials[0]
    if mat.name == name: return mat
    imgs = {n.image.name.split(".")[0]: n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
    for key, suffix, cs in (("base_color", "BaseColor", 'sRGB'), ("normal", "Normal", 'Non-Color')):
        n = imgs.get(key)
        if n is None: continue
        img = n.image.copy(); img.name = "%s_%s" % (name, suffix); img.colorspace_settings.name = cs
        if img.size[0] > 2048: img.scale(2048, 2048)
        img.filepath_raw = os.path.join(TEX, "%s_%s.png" % (name, suffix)); img.file_format = 'PNG'; img.save()
        n.image = img
    mat.name = name
    return mat


def stone_material():
    m = bpy.data.materials.new("PR_Mid_BasementStone"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, "PR_Mid_BasementStone_BaseColor.png"))
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(os.path.join(TEX, "PR_Mid_BasementStone_Normal.png"))
    tn.image.colorspace_settings.name = 'Non-Color'
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = .9
    return m


def basement(mat):
    """Floor and four walls of the chasm's basement, box-projected UVs on a 2 m tile."""
    bm = bmesh.new(); uv = bm.loops.layers.uv.new("map1")   # same name as the Tripo assets, so the join keeps one UV map
    # 1 cm into the chasm: the game's floor slabs end in these planes and two faces in one plane flicker (R498)
    u0, u1 = CHASM_U[0] + .01, CHASM_U[1] - .01; v0, v1 = -HALF_V + .01, HALF_V - .01; y0, y1 = BASE_Y, -.02
    def quad(ps, axes):
        vs = [bm.verts.new(p) for p in ps]; f = bm.faces.new(vs)
        for l in f.loops:
            p = l.vert.co; l[uv].uv = (p[axes[0]] / 2, p[axes[1]] / 2)
    quad([(v0, u0, y0), (v1, u0, y0), (v1, u1, y0), (v0, u1, y0)], (0, 1))           # floor, facing up
    quad([(v0, u0, y0), (v0, u0, y1), (v1, u0, y1), (v1, u0, y0)], (0, 2))           # near wall, facing +u
    quad([(v1, u1, y0), (v1, u1, y1), (v0, u1, y1), (v0, u1, y0)], (0, 2))           # far wall, facing -u
    quad([(v0, u1, y0), (v0, u1, y1), (v0, u0, y1), (v0, u0, y0)], (1, 2))           # -v wall, facing +v
    quad([(v1, u0, y0), (v1, u0, y1), (v1, u1, y1), (v1, u1, y0)], (1, 2))           # +v wall, facing -v
    me = bpy.data.meshes.new("basement"); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new("basement", me); bpy.context.scene.collection.objects.link(o)
    return o


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = name
    return o


LAMP_LANTERN = Vector()


def lantern_centre(o, top=.62):
    """Centre of the part above `top` of the height (a lantern's cage), in the object's coordinates."""
    p = coords(o); z0, z1 = p[:, 2].min(), p[:, 2].max()
    q = p[p[:, 2] > z0 + top * (z1 - z0)]
    lo, hi = q.min(0), q.max(0)
    return Vector(((lo + hi) / 2).tolist())


def socket(parent, name, at):
    """An empty child (exported with the FBX) where code adds a flame and a light."""
    e = bpy.data.objects.new(name, None); e.empty_display_size = .1
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent; e.location = at
    return e


def export(o, name):
    os.makedirs(OUT_FBX, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    for c in o.children: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


# ------------------------------------------------------------------ build
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rustic = split(load("R503_RusticBuildingComponents"), rustic_rule)
    wood = split(load("R503_StylizedWoodProps"), wood_rule)
    carp = split(load("R503_CarpentrySupplies"), carpentry_rule)
    for o in rustic.values(): kit_material(o, "PR_Mid_RusticKit")
    for o in wood.values(): kit_material(o, "PR_Mid_WoodKit")
    for o in carp.values(): kit_material(o, "PR_Mid_CarpentryKit")

    # plank: the standing plank's thickness is source x, its five boards run along source z.
    plank = wood["plank"]
    print("DEC plank", decimate(plank, 1600))
    xform(plank, Matrix.Rotation(math.radians(90), 4, 'Y'))       # boards (z) -> x, thickness (x) -> z
    fit_box(plank, (1., 1., 1.), centre_z=True)                    # unit cube, centre origin
    p = coords(plank); print("PLANK box", p.min(0).round(3), p.max(0).round(3))

    beam = rustic["beam"]; print("DEC beam", decimate(beam, 1400)); ground_align(beam)
    fit_box(beam, (2.5, .4, .45))                                  # one joist segment, long axis x
    scaffold = rustic["scaffold"]; print("DEC scaffold", decimate(scaffold, 5000)); ground_align(scaffold)
    fit_box(scaffold, (2.3, 2.3, (JOIST_TOP - .45) - BASE_Y))      # basement floor up to the joists' underside
    lamp = wood["lamp"]; print("DEC lamp", decimate(lamp, 2500)); ground_align(lamp); uniform(lamp, .7)
    p = coords(lamp); s = 1.7 / (p[:, 2].max() - p[:, 2].min()); xform(lamp, Matrix.Scale(s, 4))
    global LAMP_LANTERN
    LAMP_LANTERN = lantern_centre(lamp)

    env = [basement(stone_material())]
    for v in JOIST_V:
        for k in range(4):
            u = CHASM_U[0] + 1.25 + 2.5 * k
            M = Matrix.Translation((v, u, JOIST_TOP - .45)) @ Matrix.Rotation(math.radians(90), 4, 'Z')
            env.append(place_copy(beam, "joist", M))
    for v, u in SCAFFOLD_AT:
        env.append(place_copy(scaffold, "scaffold", Matrix.Translation((v, u, BASE_Y))))
    for v, u in LAMP_AT:
        env.append(place_copy(lamp, "lamp", Matrix.Translation((v, u, BASE_Y))))
    for key, v, u, yaw, size in PROPS:
        o = carp[key]; print("DEC", key, decimate(o, 3500)); ground_align(o); uniform(o, size)
        env.append(place_copy(o, key, Matrix.Translation((v, u, 0.)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')))
    for o in [beam, scaffold, lamp] + list(carp.values()):
        bpy.data.objects.remove(o)
    for key in ("scaffold", "beam"):
        rustic.pop(key, None)
    station = join(env, "B_StationEnv")
    print("ENV tris", tris(station), "materials", [m.name for m in station.data.materials])
    # The work lamps' lanterns get a real light and a flame from code (MiniGameModels.LightSockets): an empty at
    # each lantern's centre, named SOCKET_Lamp. The model itself stays unlit (no painted glow is relied on).
    for v, u in LAMP_AT:
        socket(station, "SOCKET_Lamp", Vector((v, u, BASE_Y)) + LAMP_LANTERN)
    if "--export" in ARGS:
        export(plank, "B_Plank")
        export(station, "B_StationEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "B_CoreScene_v1.blend"))
    if "--render" in ARGS:
        render(plank, station)


def render(plank, station):
    """Preview in the game frame: floors either side of the chasm, a few laid planks, piles, pawns."""
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)
    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba; return m
    slate = mat("slate", (.13, .17, .22, 1)); gold = mat("gold", (.9, .65, .15, 1)); pawn = mat("pawn", (.95, .93, .9, 1))
    def box(lo, hi, m):
        c = [(a + b) / 2 for a, b in zip(lo, hi)]; s = [(b - a) / 2 for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(location=c, scale=s); o = bpy.context.object; o.data.materials.append(m)
        [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o); return o
    box((-10, 0, -1), (10, 6, 0), slate); box((-10, 16, -1), (10, 20, 0), slate)
    def plank_at(lo, hi):
        c = Vector([(a + b) / 2 for a, b in zip(lo, hi)]); s = [(b - a) for a, b in zip(lo, hi)]
        place_copy(plank, "laid", Matrix.Translation(c) @ Matrix.Diagonal((s[0], s[1], s[2], 1.)))
    for r in range(3):
        for c0 in (-2.5, 0.):
            if r == 2 and c0 == 0.: continue
            plank_at((c0, 6 + 2 * r, -.3), (c0 + 2.5, 8 + 2 * r, 0.))
    for px in (-8., 8.):
        for i in range(5): plank_at((px - 1.25, 2, i * .14), (px + 1.25, 4, i * .14 + .12))
    for r in range(5):
        for c0 in (-2.5, 0.):
            if r < 3: continue
            box((c0 + .05, 6 + 2 * r + .05, -.05), (c0 + 2.45, 8 + 2 * r - .05, -.02), gold)
    for x, y in ((-1.2, 4.5), (1.5, 11.5), (-7.5, 3.3)):
        bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(x, y, .31 if y < 6 or y > 16 or abs(x) <= 2.5 and y < 12 else .31))
        o = bpy.context.object; o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(x, y, .84)); o = bpy.context.object
        o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.9, .8, .68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .5; sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.0; bpy.context.object.data.color = (1, .9, .78)
    for v, u in LAMP_AT:
        bpy.ops.object.light_add(type='POINT', location=(v, u, BASE_Y + 1.6)); bpy.context.object.data.energy = 300
        bpy.context.object.data.color = (1, .75, .45)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    views = {"overview": ((0, -9, 12), (0, 10, -1.5), 26), "chasm": ((6, 4.5, 2.2), (-1, 12, -2.5), 24),
             "pile": ((-3, -2, 2.5), (-8, 3, .2), 28), "props": ((0, 13, 3), (-6, 19, 0), 28)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_b_core_%s.png" % name)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
