# R483 (승규 11-06, "fantasy castle arch 3d model.glb" and its painted picture: "깃발 제거 해주고 성문 쪽 에셋 바꾸고 똑같이
# 텍스쳐 입혀", then "새 성문 깃발 + 배너 전부"): the garden's exit gate becomes 승규's Tripo gate - a round arch with a
# crowned shield, two square towers with blue pyramid roofs, wing walls with vines and planters, urn pillars of flowers
# and low end blocks. The GLB comes untextured (one grey material, no UVs, ~183k faces, front -y with the banners and
# the shield), so this script cleans it and gives it the garden gate's own materials by where each face is, as the
# picture paints it:
#   flags         the two blue pennants beside the poles: deleted (the poles and gold balls stay)
#   banners       the two crowned banners, their boards and rods on the towers' fronts: deleted, the towers' backs
#                 (plain block courses) copied over their place (the mesh has no wall behind them)
#   gold          the spires, poles and balls; the roofs' eave slabs and their diamonds; the shield's frame and crown
#                 and the crest over the arch; the studs on the arch's rose band; a band over the moss footings of
#                 the towers and the urn pillars
#   blue          the roofs (GC | gate blue)
#   rose          the arch's middle band between its cream inner ring and outer cap (GC | rose stone)
#   olive         the shield's field (GC | shield olive)
#   moss          the bottom course everywhere but the planter boxes (GC | gate moss)
#   plants        vines, planter mounds and the urns' heaps (rough surfaces in their places): leaves with clumps of
#                 pink, white and yellow flowers
#   stone         the rest, the gate's own stone (GC | gate stone, Stone_Gate.png, the picture's)
# Colour edges that run level (the footings, the bands, the eaves) are cut into the mesh first, so they are straight.
# Then it recomputes the normals (some faced in), decimates to about FACES and saves; gate_sample_v2.py scales and places
# it (the asset's base at z 0, its opening's middle at x 0, its middle plane y 0).
#
#   blender -b --python gate_arch_asset.py [-- <castle arch .glb>]      (default: source/castle_arch.glb)
# Out: GateArchAsset_v1.blend (object "Gate arch asset"), render_gate_arch_asset_front/quarter/back.png
import bpy, bmesh, sys, os, math, random
from collections import defaultdict
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GLB = ARGS[0] if ARGS else os.path.join(HERE, "source", "castle_arch.glb")
FACES = 120000      # R483 v2: less decimated (fewer slivers)

# Measured on the GLB (asset units; x across, y depth with the front at -y, z up; it is symmetric in x to ~1 mm).
POLE_X, POLE_R = .2259, .0045          # the flag poles (both towers; each flag flies toward +x)
FLAG_Z = (.497, .5765)                 # the flags' height (the ball is over 0.578, the spire's collar under 0.53)
TOWER_X = (.168, .2775)                # a tower, |x| (its roof and spire)
SHAFT_FACE, SHAFT_MID = -.048, .001    # a tower shaft's front face (its back at 0.050) and middle plane
BOARD_X = (.1795, .2765)                # the banner's board, |x| (the shaft 0.18 .. 0.278)
BANNER_Z = (.0835, .373)               # from the board's foot to over its rod
PLINTH_TOP, CAP_BOTTOM = .0835, .377   # the board stands on the shaft's plinth (its top 0.080 .. 0.084), under the cap
KNOB_X, ROD_Z = (.165, .295), (.331, .362)   # the rod with its knobs
PANEL_X, PANEL_Y = (.1793, .2778), (-.0712, -.0485)   # the stone panel shutting the board's place (its front -0.0712)
PANEL_Z = (PLINTH_TOP - .003, CAP_BOTTOM + .003)        # ... into the plinth and the cap
PLUG_X, PLUG_BACK = (.1806, .2772), -.030   # R490: the plug behind the panel, just inside the shaft (0.18 .. 0.278)
PATCH_X, PATCH_Z = (.1815, .2755), (.0855, .3745)      # the back's courses copied onto the panel
PATCH_PROUD = .0006                    # ... this far out of its front (3 cm at the gate's size)
ARCH_C, ARCH_R = .20, .1405            # the arch: a half circle about (0, ARCH_C), its opening's radius
BAND = (.2005, .2295)                  # its rose band, recessed between the inner ring and the outer cap, radius
STUDS = (.205, .2255)                  # where its studs stand
BAND_FACE = .0385                      # the band's face |y| 0.034 .. 0.0365, the studs to 0.043, the rings at 0.048 .. 0.053
SHIELD_FIELD = (-.0714, -.0668)        # the shield's recessed field (its frame and crown at -0.073 .. -0.077)
MOSS_Z, GOLD_Z = .05, .066             # the footing course and the gold band over it
EAVE_Z, ROOF_Z, SPIRE_Z = .427, .4445, .497
DIAMOND = (.456, .0135, .017)          # the roof faces' diamonds: middle height, half width, half height
URN_X = .40                             # the urns' axis |x| (y 0); a bowl's radius: urn_r(z)


def urn_r(z):
    """The bowl's radius at height z (0.028 at its foot 0.275, 0.058 at its rim 0.311)."""
    return min(.058, .028 + (z - .275) * .83)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
src = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
src.name = "Gate arch asset"
src.data.transform(src.matrix_world)
src.matrix_world.identity()


def mat(name, rgb, rough, metal=0.):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*rgb, 1)             # (the solid view's colour too)
    return m


# Placeholders by the garden's names (gate_sample_v2.py swaps in its own materials of these names); preview colours.
KEYS = ("stone", "moss", "gold", "blue", "rose", "olive", "leaf", "pink", "white", "yellow")
M = {
    "stone": mat("GC | gate stone", (.98, .84, .72), .72),
    "moss": mat("GC | gate moss", (.55, .62, .36), .78),
    "gold": mat("GC | gate gold", (.98, .72, .20), .3, .75),
    "blue": mat("GC | gate blue", (.10, .25, .70), .55),
    "rose": mat("GC | rose stone", (.92, .58, .48), .7),
    "olive": mat("GC | shield olive", (.47, .54, .27), .6),
    "leaf": mat("GC | leaf", (.30, .62, .16), .7),
    "pink": mat("GC | flower pink", (1.0, .48, .68), .6),
    "white": mat("GC | flower white", (.98, .97, .93), .6),
    "yellow": mat("GC | flower yellow", (1.0, .86, .22), .6),
}
me = src.data
me.materials.clear()
for k in KEYS:
    me.materials.append(M[k])
IDX = {k: i for i, k in enumerate(KEYS)}

bm = bmesh.new()
bm.from_mesh(me)


def components(bm):
    comp, out = {}, []
    for v in bm.verts:
        if v in comp:
            continue
        st, part = [v], [v]
        comp[v] = len(out)
        while st:
            a = st.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if b not in comp:
                    comp[b] = len(out)
                    st.append(b)
                    part.append(b)
        out.append(part)
    return out


# 1. The specks Tripo left floating (37 pieces of 3 .. 5 vertices).
specks = [v for part in components(bm) if len(part) < 200 for v in part]
bmesh.ops.delete(bm, geom=specks, context="VERTS")

# 2. The flags: the faces off the poles at the flags' height, on the side each flag flies.
def is_flag(f):
    c = f.calc_center_median()
    if not (FLAG_Z[0] < c.z < FLAG_Z[1]):
        return False
    s = 1 if c.x > 0 else -1
    dx = c.x - s * POLE_X
    # by the pole only between the spire's collar and the ring under the ball; further out, all of it
    # (and the flag's edge on the pole: faces looking along y just off it)
    near = (.528 < c.z < .5625 and dx > POLE_R + .004) or (.528 < c.z < .574 and dx > POLE_R + .0006 and abs(f.normal.y) > .7)
    return near or dx > .018


flags = [f for f in bm.faces if is_flag(f)]
bmesh.ops.delete(bm, geom=flags, context="FACES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")

# 3. The banners and their rods: each hangs on a raised board covering the tower's front (the board at -0.0645, the
#    cloth to -0.078; the shaft's own face is at -0.048 and the mesh has no wall behind them, nor plinth top or cap
#    underside where the board met them). Everything standing out of the shaft's face there goes (with the rods'
#    knobs past its sides), and in its place a closed stone panel (PANEL: from the shaft's sides into the plinth and
#    the cap, its front where the board's was) shuts every opening that leaves; on it, the tower's back over the same
#    window - plain block courses with a band at the rod's height, as the picture's back - mirrored through the
#    shaft's middle plane, so its joints read (R483, 승규's Unity pictures: slits, flaps and a hole on the plinth).
def centre_in(f, x0, x1, z0, z1):
    c = f.calc_center_median()
    return x0 < abs(c.x) < x1 and z0 < c.z < z1


patch = bm.faces.layers.int.new("patch")                 # 1 / -1 on the copied courses of the east / west tower
bm.faces.ensure_lookup_table()
bm.normal_update()
board = [f for f in bm.faces if centre_in(f, *BOARD_X, *BANNER_Z) and any(v.co.y < SHAFT_FACE - .0012 for v in f.verts)
         and not (f.normal.z > .5 and f.calc_center_median().z < PLINTH_TOP + .006)]   # the plinth's top stays
# the knobs: past the shaft's outer side nothing else stands there; by its inner side, clear of the arch's cap ring
knobs = [f for f in bm.faces if (centre_in(f, TOWER_X[1] - .001, KNOB_X[1], *ROD_Z) and any(v.co.y < -.040 for v in f.verts))
         or (centre_in(f, KNOB_X[0], BOARD_X[0] + .001, *ROD_Z) and any(v.co.y < -.053 for v in f.verts))
         # and the board's ragged inner edge left beside the panel, under and over the impost
         or ((centre_in(f, .166, BOARD_X[0] + .001, BANNER_Z[0], .205) or centre_in(f, .166, BOARD_X[0] + .001, .25, ROD_Z[0]))
             and any(v.co.y < -.0525 for v in f.verts))]
back = [f for f in bm.faces if centre_in(f, *PATCH_X, *PATCH_Z)
        and all(v.co.y > 2 * SHAFT_MID - SHAFT_FACE - .008 for v in f.verts)]   # with its joints
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])          # the GLB's own (some faced in), before the copies
copy = bmesh.ops.duplicate(bm, geom=back)
for g in copy["geom"]:
    if isinstance(g, bmesh.types.BMFace):
        g[patch] = 1 if g.calc_center_median().x > 0 else -1
    elif isinstance(g, bmesh.types.BMVert):
        g.co.y = 2 * SHAFT_MID - g.co.y - (SHAFT_FACE - PANEL_Y[0] + PATCH_PROUD)
bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f[patch]])
bmesh.ops.delete(bm, geom=list(set(board) | set(knobs)), context="FACES")
# cut to the panel: the back's long triangles at the window's edge would hang past it, so each tower's copy is
# bisected clean along the panel's four edges and what lies past them dropped
for s in (-1, 1):
    for co, no, inner in (((s * (PANEL_X[0] + .0004), 0, 0), (s, 0, 0), True), ((s * (PANEL_X[1] - .0004), 0, 0), (s, 0, 0), False),
                          ((0, 0, PANEL_Z[0] + .004), (0, 0, 1), True), ((0, 0, PANEL_Z[1] - .004), (0, 0, 1), False)):
        fs = [f for f in bm.faces if f[patch] == s]
        geom = fs + list({e for f in fs for e in f.edges}) + list({v for f in fs for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, dist=1e-6, clear_inner=inner, clear_outer=not inner)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
for s in (-1, 1):
    x0, x1 = sorted((s * PANEL_X[0], s * PANEL_X[1]))
    lo, hi = Vector((x0, PANEL_Y[0], PANEL_Z[0])), Vector((x1, PANEL_Y[1], PANEL_Z[1]))
    r = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation((lo + hi) / 2) @ Matrix.Diagonal((*(hi - lo), 1)))
    bmesh.ops.bevel(bm, geom=list({e for v in r["verts"] for e in v.link_edges}), offset=.0009, segments=2,
                    affect="EDGES", profile=.5, clamp_overlap=True)
    # R490 (승규 Unity picture: a slit of sky beside the tower's front): the panel's back (-0.0485) only met the shaft's
    # sides' front edges (-0.048), and its rounded back corners left a slit there (~7 cm at the gate's size) into the
    # hollow shaft, whose inside Unity does not draw - so the sky showed through. A plug from inside the panel into
    # the shaft (to -0.030), just within the shaft's sides, shuts it: through the slit one sees its stone.
    px0, px1 = sorted((s * PLUG_X[0], s * PLUG_X[1]))
    lo = Vector((px0, PANEL_Y[1] - .0015, PANEL_Z[0]))
    hi = Vector((px1, PLUG_BACK, PANEL_Z[1]))
    bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation((lo + hi) / 2) @ Matrix.Diagonal((*(hi - lo), 1)))
# The holes the GLB came with (the urns' open tops): closed.
filled = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary and abs(e.verts[0].co.x) > .3], sides=0)
bmesh.ops.triangulate(bm, faces=filled["faces"])
print("CLEANED specks %d, flag faces %d, banner faces %d (+ knobs %d) for %d from the back, %d urn holes filled" %
      (len(specks), len(flags), len(board), len(knobs), len(back), len(filled["faces"])))
bm.faces.layers.int.remove(patch)

bm.to_mesh(me)
bm.free()
me.update()

# 4. Decimate to the budget before painting (so the colour edges below are cut into the final mesh).
dec = src.modifiers.new("Decimate", "DECIMATE")
dec.ratio = min(1., FACES / max(1, len(me.polygons)))
bpy.context.view_layer.objects.active = src
bpy.ops.object.modifier_apply(modifier=dec.name)

bm = bmesh.new()
bm.from_mesh(me)
# Level colour edges, cut where they run (whole width: a cut through plain stone changes nothing).
for z in (MOSS_Z, GOLD_Z, EAVE_Z, ROOF_Z, SPIRE_Z):
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1), dist=1e-5)
bm.faces.ensure_lookup_table()
bm.normal_update()

# Roughness: how much the normals turn round each vertex, smoothed over its neighbours (leaves turn a lot, stone is
# flat between sharp edges).
rough = {}
for v in bm.verts:
    fs = v.link_faces
    rough[v] = sum(1 - max(-1., min(1., f.normal.dot(v.normal))) for f in fs) / len(fs) if fs else 0.
for _ in range(3):
    nxt = {}
    for v in bm.verts:
        nb = [e.other_vert(v) for e in v.link_edges]
        nxt[v] = (rough[v] + sum(rough[u] for u in nb)) / (1 + len(nb)) if nb else rough[v]
    rough = nxt

rnd = random.Random(483)
clump = {}


def flower(c, palette):
    key = (round(c.x / .011), round(c.y / .011), round(c.z / .011))
    if key not in clump:
        clump[key] = rnd.choice(palette)
    return clump[key]


URN_HEAP = ("pink", "pink", "white", "yellow", "leaf", "leaf", "leaf")
PLANTER = ("pink", "white", "white", "yellow", "leaf", "leaf", "leaf")
VINE = ("pink", "yellow", "leaf", "leaf", "leaf", "leaf", "leaf")
ROUGH = .035


def paint(f):
    c, n = f.calc_center_median(), f.normal
    ax, ay, z = abs(c.x), abs(c.y), c.z
    rv = sum(rough[v] for v in f.verts) / len(f.verts)
    tower = TOWER_X[0] - .03 < ax < TOWER_X[1] + .02
    # the towers' tops: spire, pole and ball; the roof with gold eaves and diamonds
    if tower and z > SPIRE_Z:
        return "gold"
    if tower and z > ROOF_Z - .0005:
        dx = c.x - math.copysign(POLE_X, c.x)
        dy = c.y + .0155
        u = dx if abs(n.y) >= abs(n.x) else dy
        if abs(u) / DIAMOND[1] + abs(z - DIAMOND[0]) / DIAMOND[2] < 1 and abs(n.z) < .95:
            return "gold"
        return "blue"
    if tower and z > EAVE_Z and ax > .17:
        return "gold"
    # the crest over the arch and the shield on its front
    if ax < .045 and z > .44 and (c.y < -.035 or z > .468):   # (not the arch's own top behind it)
        return "gold"
    if ax < .068 and .326 < z < .452 and c.y < -.0545:
        if SHIELD_FIELD[0] < c.y < SHIELD_FIELD[1] and n.y < -.7 and ax < .055:
            return "olive"
        return "gold"
    # the arch's rose band and its studs
    rho = math.hypot(c.x, z - ARCH_C)
    if z > .235 and ax < .19 and BAND[0] < rho < BAND[1] and .02 < ay < BAND_FACE:
        return "rose"
    if z > .235 and ax < .19 and STUDS[0] < rho < STUDS[1] and BAND_FACE <= ay < .0455:
        return "gold"
    # the plants (R483 v2, 승규 "나뭇잎 택스쳐 덜 입힌 부분": the leaves on the wing walls' tops, the urn heaps' edges and
    # the vines' smoother leaves had stayed stone): over an urn's bowl everything is its heap; over a wing wall's
    # coping, its cushion of leaves; in front of (or behind) a wing wall below its coping, its vine; the coping itself
    # and the bowls are told from leaves by their smoothness
    d_urn = math.hypot(ax - URN_X, c.y)
    rim = .300 < z < .326 and .045 < d_urn < .068 and rv < ROUGH * 2     # the bowl's rounded lip
    if .30 < ax < .49 and z > .30 and not rim and (rv > ROUGH or z > .322) and (d_urn > .062 or z > .316):
        return flower(c, URN_HEAP)                   # (and the leaves hanging out over the bowl's rim)
    clear_of_urn = z < .262 or d_urn > .064          # (the urn's pedestal and bowl stand over its pillar's cap)
    if .284 < ax < .370 and .233 < z < .30 and clear_of_urn and (z > .242 or rv > ROUGH * 1.2):
        return flower(c, VINE)
    if .290 < ax < .370 and .085 < z < .195 and ay > .039:
        return flower(c, VINE)
    if .286 < ax < .372 and .085 < z < .275 and ay > .039 and rv > ROUGH and clear_of_urn:
        return flower(c, VINE)
    if .27 < ax < .39 and .036 < z < .12 and ay > .05 and rv > ROUGH * .8:
        return flower(c, PLANTER)
    # footings: moss, the gold band over the towers' and the urn pillars'
    planter_box = .28 < ax < .385 and ay > .062 and z < .045
    if z < MOSS_Z and not planter_box:
        return "moss"
    if z < GOLD_Z and (.13 < ax < .29 or .37 < ax < .448) and not planter_box:
        return "gold"
    return "stone"


counts = defaultdict(int)
for f in bm.faces:
    k = paint(f)
    f.material_index = IDX[k]
    counts[k] += 1
print("SEGMENTS", dict(counts))

bm.to_mesh(me)
bm.free()
me.update()
for p in me.polygons:
    p.use_smooth = True
me.set_sharp_from_angle(angle=math.radians(40))
print("FACES", len(me.polygons))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "GateArchAsset_v1.blend"))

# Previews in the materials' colours (front as 승규's picture, three-quarter, back).
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 1400, 900
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (.06, .06, .07, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
sc.world = w
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
sc.collection.objects.link(sun)
fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
fill.data.energy = 1.2
fill.rotation_euler = (math.radians(70), 0, math.radians(150))
sc.collection.objects.link(fill)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
for name, at in (("front", (.55, -2.3, .55)), ("quarter", (1.5, -1.6, .75)), ("back", (-.6, 2.3, .55))):
    cam.location = Vector(at)
    cam.rotation_euler = (Vector((0, 0, .29)) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    sc.render.filepath = os.path.join(HERE, "render_gate_arch_asset_%s.png" % name)
    bpy.ops.render.render(write_still=True)
print("GATE ARCH ASSET DONE")
