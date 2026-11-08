# E Royal Library core (R507): the user's two Tripo sets, made from the GPT asset sheets
# (Docs/PawnRush/ConceptArt/R507_RoyalLibrary/AssetRefs), cut into parts and fitted to MgBells' numbers.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python e_library_v1.py -- [--render] [--export]
#
# Sources (Source/, each 1 m wide, z up, the parts standing on the ground plane):
#   R507_StackedWoodenBookcases: three stepped bookcase towers (front = Blender -y). Tripo made the lower storeys taller
#                                and the upper ones narrower than the game's, so the towers are not used whole: the three
#                                lower storeys and the medium tower's short top are cut out as bookcase-cabinet modules.
#   R507_AntiquePropCollection:  door arch, pivoting bookcase door, hand bell, reading lamp (unused: the user, 11-07,
#                                "책장 위에 램프는 두지 않을거야"), tower-top platform with a rail on three sides
#                                (unused: torn rail bars; the rail is built in brass instead, rail()).
# Every part keeps its set's own texture (PR_Mid_LibraryTowerKit / PR_Mid_LibraryPropKit).
#
# Outputs (Export/):
#   E_StationEnv.fbx  MgBells' game frame of one station (Blender x = v, y = u, z = up; symmetric in v, so it serves
#                     both teams): the six towers built from cabinet modules on the game's storey boxes, a walnut lid
#                     and core per storey, a brass rail round every top. A tower storey is 2.4 m wide, 3 m tall (the 7.5 m tower's last one 1.5 m),
#                     flush at the back (u + 1.2) and 0.8 m deeper to the south for every storey above it. A 2.4 m deep
#                     storey is one cabinet; a deeper one is two cabinets, one at the back and one at the front (1 cm
#                     wider so their side faces never share a plane), so no cabinet is stretched in depth.
#   E_Bell.fbx        the hand bell on its stand, 1.25 m tall, origin at the stand's bottom centre. Code hangs one on
#                     each top and gilds it when the game rings it.
#   E_ShelfDoor.fbx   the pivoting bookcase leaf, 3.8 x 4.6 x 0.45 m, books to Blender -y, hinge post on x = 0 and the
#                     leaf along +x, centred in height: the frame of E's DEC_ShelfDoorPivot (MiniGamePalaceArt).
#   E_DoorArch.fbx    the stone door frame round each team gate: opening 4.5 m wide and 5.8 m to the crown (the
#                     shell's "Team portal front"), piers 0.9 m, 1 m deep, origin at the opening's bottom centre.
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

# MgBells (game frame): (v, height, u); storeys of 3 m; top 2.4 m square; 0.8 m ledge per storey on the south side.
TOWERS = [(-5., 6., 5.), (5., 6., 5.), (-5., 7.5, 11.), (5., 7.5, 11.), (-5., 9., 17.), (5., 9., 17.)]
TOP, LEDGE, STOREY = 2.4, .8, 3.
BELL_BACK = .55          # the bell stands on the top's north half, out of the climbers' way
BELL_H = 1.25
BELL_LIFT = .025         # the bell's stand 0.5 cm over the top storey's lid (lid top = game top + 2 cm)
# The top rail (user, 11-07: "난간도 이어지지 않아"): Tripo's railed platform came out with torn, unjoined bars, so the
# rail is built here in matte brass, all round the top, open only in a 1.2 m entry in the middle of the south side.
RAIL_IN, RAIL_H, RAIL_GAP = .12, .72, .6
CABINET_TRIS = 6000      # 3000 and 4000 tore the books and the frames' thin edges (user, 11-07: "깨진 부분 고쳐",
                         # "아직 깨져 보이고"); the source modules are 4k-20k, the books stay whole at 6000
LID_T, LID_UP = .12, .02  # every storey's walnut lid: 2 cm over the game's top, 12 cm thick
CORE_IN = .45            # the dark walnut core inside each storey, this far in from every face (behind the books)

# door frame and leaf (MiniGameSectionBuilder / MiniGamePalaceArt numbers)
ARCH_OPEN_W, ARCH_OPEN_H, ARCH_PIER, ARCH_DEPTH = 4.5, 5.8, .9, 1.
DOOR = (3.8, .45, 4.6)


# ------------------------------------------------------------------ helpers (as b_core_scene_v1.py)
def load(name):
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"))
    ob = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    ob.data.transform(ob.matrix_world); ob.matrix_world.identity()
    ob.name = name
    return ob


def split(ob, rule):
    """Separate mesh islands into named objects. rule(centroid) -> part name or None (dropped)."""
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
        print("SPLIT", ob.name, key, tris(o), "tris")
    bm.free()
    bpy.data.objects.remove(ob)
    return out


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
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


def set_coords(o, p):
    o.data.vertices.foreach_set("co", p.astype(np.float32).ravel()); o.data.update()


def xform(o, M):
    o.data.transform(M); o.data.update()


def fit_box(o, lo_to, hi_to):
    """Non-uniform: the object's bounding box onto the box lo_to..hi_to."""
    p = coords(o); lo, hi = p.min(0), p.max(0)
    q = (p - lo) / (hi - lo) * (np.array(hi_to) - np.array(lo_to)) + np.array(lo_to)
    set_coords(o, q)


def remap(values, src, dst):
    """Piecewise-linear map of one axis through matching breakpoints."""
    return np.interp(values, src, dst, left=None, right=None) + \
        np.where(values < src[0], (values - src[0]) * (dst[1] - dst[0]) / (src[1] - src[0]), 0) + \
        np.where(values > src[-1], (values - src[-1]) * (dst[-1] - dst[-2]) / (src[-1] - src[-2]), 0)


def ground_align(o):
    """Turn about z so the long ground axis is x (PCA of the footprint), centre it, ground at z 0."""
    p = coords(o)
    xy = p[:, :2] - p[:, :2].mean(0)
    w, v = np.linalg.eigh(np.cov(xy.T))
    ax = v[:, np.argmax(w)]
    xform(o, Matrix.Rotation(-math.atan2(ax[1], ax[0]), 4, 'Z'))
    p = coords(o); lo, hi = p.min(0), p.max(0)
    xform(o, Matrix.Translation(Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))))


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


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = name
    return o


def export(o, name):
    os.makedirs(OUT_FBX, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


# ------------------------------------------------------------------ splitting rules (measured from the sources)
def tower_rule(c):
    # towers by x: short -0.50..-0.26, medium -0.14..0.14, tall 0.23..0.50; storeys by height (front = -y)
    if c.x < -.2:
        return "cab_short" if c.z < -.072 else None
    if c.x < .2:
        if c.z < -.068: return "cab_medium"
        if c.z > .126: return "cab_half"           # the medium tower's 1.5 m top (not the ledge caps under it)
        return None
    return "cab_tall" if c.z < -.062 else None


def prop_rule(c):
    if c.x < -.25 and c.y > .07: return "arch"
    if c.x < -.001: return "door"
    if c.x < .122 and c.y < .012: return "bell"
    return None    # the reading lamp, a stray sliver, the railed platform (torn bars; the rail is built, see rail())


# ------------------------------------------------------------------ parts
def cabinet(o):
    """A module on the unit storey: x -1.2..1.2, y 0..2.4 (front at 0), z 0..1 (scaled per storey)."""
    print("DEC", o.name, decimate(o, CABINET_TRIS))
    fit_box(o, (-TOP / 2, 0., 0.), (TOP / 2, TOP, 1.))


def bell(o):
    print("DEC bell", decimate(o, 3000))
    p = coords(o); lo, hi = p.min(0), p.max(0); c = (lo + hi) / 2
    set_coords(o, (p - np.array([c[0], c[1], lo[2]])) * (BELL_H / (hi[2] - lo[2])))


def door(o):
    """Long axis along x, books to -y, hinge post (the thicker brass column) at x = 0, leaf along +x."""
    print("DEC door", decimate(o, 7000))
    ground_align(o)
    p = coords(o)
    # the books' side: the face with more geometry in front of the frame's middle plane
    mid = (p[:, 1].min() + p[:, 1].max()) / 2
    if (p[:, 1] < mid).sum() < (p[:, 1] > mid).sum():
        xform(o, Matrix.Rotation(math.pi, 4, 'Z')); p = coords(o)
    # hinge: the end whose outermost 8 % holds more vertices (the big column)
    lo, hi = p[:, 0].min(), p[:, 0].max(); w = hi - lo
    if (p[:, 0] > hi - .08 * w).sum() > (p[:, 0] < lo + .08 * w).sum():
        xform(o, Matrix.Scale(-1, 4, Vector((1, 0, 0))))
        o.data.flip_normals(); p = coords(o)
    fit_box(o, (0., -DOOR[1] / 2, -DOOR[2] / 2), (DOOR[0], DOOR[1] / 2, DOOR[2] / 2))


def arch(o):
    print("DEC arch", decimate(o, 2500))
    p = coords(o)
    # measured: piers -0.498..-0.442 and -0.306..-0.240, opening between, crown under z 0.118, ground -0.192
    xs = remap(p[:, 0], [-.498, -.442, -.306, -.240],
               [-ARCH_OPEN_W / 2 - ARCH_PIER, -ARCH_OPEN_W / 2, ARCH_OPEN_W / 2, ARCH_OPEN_W / 2 + ARCH_PIER])
    zs = (p[:, 2] - (-.192)) * (ARCH_OPEN_H / (.118 + .192))
    y0, y1 = p[:, 1].min(), p[:, 1].max()
    ys = (p[:, 1] - (y0 + y1) / 2) * (ARCH_DEPTH / (y1 - y0))
    set_coords(o, np.stack([xs, ys, zs], 1))


# ------------------------------------------------------------------ towers
def storeys(h):
    n = math.ceil(h / STOREY - .01)
    for s in range(n):
        y0 = s * STOREY
        yield s, n, y0, min(h, y0 + STOREY)


def walnut_material():
    m = bpy.data.materials.new("PR_Mid_WalnutFloor"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, "PR_Mid_WalnutFloor_BaseColor.png"))
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = .7
    return m


def walnut_box(name, lo, hi, uv_name, mat):
    """A closed box in the walnut swatch, box-projected UVs on its 2 m tile (the boards run along x)."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    bm = bmesh.new(); uv = bm.loops.layers.uv.new(uv_name)
    def quad(ps, axes):
        vs = [bm.verts.new(p) for p in ps]; f = bm.faces.new(vs)
        for l in f.loops:
            p = l.vert.co; l[uv].uv = (p[axes[0]] / 2, p[axes[1]] / 2)
    quad([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 1))    # top
    quad([(x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0)], (0, 1))    # bottom
    quad([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, 2))    # south
    quad([(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)], (0, 2))    # north
    quad([(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)], (1, 2))    # west
    quad([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], (1, 2))    # east
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def brass_material():
    m = bpy.data.materials.new("PR_Mid_MatteBrass"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, "PR_Mid_MatteBrass_BaseColor.png"))
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Metallic"].default_value = .8; b.inputs["Roughness"].default_value = .45
    return m


def rod(a, b, r, uv_name, mat, verts=12):
    """A closed cylinder from point a to point b."""
    a, b = Vector(a), Vector(b); d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length)
    o = bpy.context.object
    finish(o, Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4(), uv_name, mat)
    return o


def ball(c, r, uv_name, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=r)
    o = bpy.context.object; finish(o, Matrix.Translation(c), uv_name, mat)
    return o


def finish(o, M, uv_name, mat):
    """Place the primitive (made at the origin) by M, one UV map named like the rest, the brass material."""
    o.data.transform(M); o.matrix_world.identity()
    if o.data.uv_layers: o.data.uv_layers[0].name = uv_name
    o.data.materials.clear(); o.data.materials.append(mat)


def rail(v, h, u, uv_name, brass):
    """Matte brass rail round a tower top, standing on the top storey's lid: turned posts at the corners, mid-sides
    and either side of the south entry, a hand rail and a foot rail joined post to post, balusters between."""
    z = h + LID_UP + .005
    x0, x1 = v - TOP / 2 + RAIL_IN, v + TOP / 2 - RAIL_IN
    y0, y1 = u - TOP / 2 + RAIL_IN, u + TOP / 2 - RAIL_IN
    # runs of posts, each run joined by rails: west+north+east as one, the south in two halves round the entry
    runs = [[(v - RAIL_GAP, y0), (x0, y0), (x0, u), (x0, y1), (v, y1), (x1, y1), (x1, u), (x1, y0), (v + RAIL_GAP, y0)]]
    out = []
    posts = set()
    for run in runs:
        for (ax, ay), (bx, by) in zip(run, run[1:]):
            for zz, r in ((z + RAIL_H, .038), (z + .14, .026)):
                out.append(rod((ax, ay, zz), (bx, by, zz), r, uv_name, brass))
            n = max(1, round(math.hypot(bx - ax, by - ay) / .26))
            for k in range(1, n):
                t = k / n; px, py = ax + (bx - ax) * t, ay + (by - ay) * t
                out.append(rod((px, py, z + .14), (px, py, z + RAIL_H), .02, uv_name, brass, 8))
        posts.update(run)
    for px, py in posts:
        out.append(rod((px, py, z), (px, py, z + .07), .085, uv_name, brass))           # foot
        out.append(rod((px, py, z), (px, py, z + RAIL_H + .05), .055, uv_name, brass))  # shaft
        out.append(rod((px, py, z + RAIL_H + .03), (px, py, z + RAIL_H + .08), .07, uv_name, brass))  # collar
        out.append(ball((px, py, z + RAIL_H + .14), .065, uv_name, brass))            # knob
    return out


def tower(mods, v, h, u, uv_name, walnut):
    """Cabinets on the game's storey boxes of one tower. Tripo modelled each storey's top as the crumpled seat of the
    storey above, with torn scraps along the rim, and the cabinets are open frames you can see into (user, 11-07:
    "깨진 부분 고쳐"). So every storey gets one walnut lid over its whole top (the ledge where pawns stand, and under
    the storey above), 2 cm over the game's top, 1 cm in from the box, and a walnut core behind its books."""
    out = []
    back = u + TOP / 2
    for s, n, z0, z1 in storeys(h):
        front = back - TOP - (n - 1 - s) * LEDGE
        out.append(walnut_box("lid", (v - TOP / 2 + .01, front + .01, z1 + LID_UP - LID_T),
                              (v + TOP / 2 - .01, back - .01, z1 + LID_UP), uv_name, walnut))
        out.append(walnut_box("core", (v - TOP / 2 + CORE_IN, front + CORE_IN, z0 + .15),
                              (v + TOP / 2 - CORE_IN, back - CORE_IN, z1 - LID_T), uv_name, walnut))
        depth = TOP + (n - 1 - s) * LEDGE
        front = back - depth
        if z1 - z0 < STOREY - .01: m = mods["cab_half"]
        elif s == n - 1: m = mods["cab_short"]
        elif s == 0: m = mods["cab_tall"]
        else: m = mods["cab_medium"]
        hz = z1 - z0
        if depth <= TOP + .01:
            out.append(place_copy(m, "cabinet", Matrix.Translation((v, front, z0)) @ Matrix.Diagonal((1, 1, hz, 1))))
        else:
            out.append(place_copy(m, "cabinet back", Matrix.Translation((v, back - TOP, z0)) @ Matrix.Diagonal((1, 1, hz, 1))))
            out.append(place_copy(m, "cabinet front", Matrix.Translation((v, front, z0)) @ Matrix.Diagonal((1.0085, 1, hz, 1))))
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mods = split(load("R507_StackedWoodenBookcases"), tower_rule)
    props = split(load("R507_AntiquePropCollection"), prop_rule)
    for o in mods.values(): kit_material(o, "PR_Mid_LibraryTowerKit")
    for o in props.values(): kit_material(o, "PR_Mid_LibraryPropKit")
    for o in mods.values():
        p = coords(o); print("MODULE", o.name, p.min(0).round(3), p.max(0).round(3))
        cabinet(o)
    bell(props["bell"]); door(props["door"]); arch(props["arch"])

    env = []
    walnut = walnut_material(); brass = brass_material()
    uv_name = mods["cab_short"].data.uv_layers[0].name   # one UV map name, so the join keeps one map
    for v, h, u in TOWERS:
        env += tower(mods, v, h, u, uv_name, walnut)
        # no rail on the tops (user, 11-07: "제일 위에 난간 그냥 지우자"); rail() is kept for reference
    for o in list(mods.values()):
        bpy.data.objects.remove(o)
    station = join(env, "E_StationEnv")
    print("ENV tris", tris(station))
    for k in ("bell", "door", "arch"):
        print("PART", k, tris(props[k]), coords(props[k]).min(0).round(2), coords(props[k]).max(0).round(2))
    if "--export" in ARGS:
        export(station, "E_StationEnv")
        export(props["bell"], "E_Bell")
        export(props["door"], "E_ShelfDoor")
        export(props["arch"], "E_DoorArch")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "E_Library_v1.blend"))
    if "--render" in ARGS:
        render(station, props)


def render(station, props):
    """Preview in one station's game frame: slate floor, the bells on the tops, the door leaf in its frame, pawns."""
    col = bpy.data.collections.new("GAME_PROXY"); bpy.context.scene.collection.children.link(col)
    for o in props.values():
        o.hide_render = True                    # the parts themselves sit at the origin; copies are placed below

    def mat(name, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba; return m
    slate = mat("slate", (.13, .17, .22, 1)); pawn = mat("pawn", (.95, .93, .9, 1))

    def box(lo, hi, m):
        c = [(a + b) / 2 for a, b in zip(lo, hi)]; s = [(b - a) / 2 for a, b in zip(lo, hi)]
        bpy.ops.mesh.primitive_cube_add(location=c, scale=s); o = bpy.context.object; o.data.materials.append(m)
        [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o); return o
    box((-10, -1, -.4), (10, 24, 0), slate)
    for v, h, u in TOWERS:
        place_copy(props["bell"], "bell", Matrix.Translation((v, u + BELL_BACK, h + BELL_LIFT)))
    # the team gate frame at the station's north end (u 19.6 here: section z 23.8 - station z 4 - its 0.2 m lead)
    place_copy(props["arch"], "arch", Matrix.Translation((0, 19.8, 0)))
    place_copy(props["door"], "door", Matrix.Translation((-1.9, 19.2, 2.3)))
    for x, y, z in ((-1.2, 2.5, 0), (-5.6, 3.1, 3.0), (5.2, 15.8, 9.0)):
        bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(x, y, z + .31))
        o = bpy.context.object; o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(x, y, z + .84)); o = bpy.context.object
        o.data.materials.append(pawn); [u.objects.unlink(o) for u in o.users_collection]; col.objects.link(o)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.9, .8, .68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .5; sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.0; bpy.context.object.data.color = (1, .9, .78)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    views = {"overview": ((-14, -10, 14), (0, 10, 3), 24), "entry": ((0, -6, 2.2), (0, 10, 4), 22),
             "side": ((-16, 11, 5), (0, 11, 4), 26), "top": ((-3.5, 14, 11.5), (-5, 17.5, 9), 26),
             "door": ((3, 12, 3), (0, 19.5, 2.5), 26),
             "ledge": ((-3.4, 12.4, 3.9), (-5, 14.8, 2.9), 30), "rail": ((-4.1, 15.5, 9.9), (-5.2, 16.8, 9.1), 30),
             "underside": ((-2.6, 11.8, 1.6), (-4.6, 14.6, 3.4), 24)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_e_%s.png" % name)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
