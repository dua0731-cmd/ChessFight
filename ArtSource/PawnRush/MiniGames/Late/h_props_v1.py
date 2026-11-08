# H King's Chess Hall props (R510): cut the user's Tripo row (Source/R510_H_A1_Props.fbx, from the asset photo
# Docs/PawnRush/ConceptArt/R510_KingsChessHall/AssetRefs/H_assets_01_props.png) into four objects, stand them straight,
# size them to the game, decimate and export one FBX each to Export/H. Reuses g_props_v1.py (load, labels, cut, place,
# decimate, export): the same method as G's props.
#
#   H_ChessClock      1.5 m high. The photo's marble pedestal came out lower than asked (half the clock instead of twice):
#                     its plain middle band is stretched so the pedestal is ~0.9 m, and the pedestal wears the hall's tiling
#                     cream marble (slot PR_Late_MarbleCream, box UVs in metres / 2) so the stretch does not smear veins.
#   H_PieceChest      1.2 m wide, lid open, eight spare pieces in velvet slots.
#   H_NotationTablet  1.6 m high, plain face (no writing: asset-photo rule; notation can be a code texture later).
#   H_HallBell        1.6 m high bell on its wall bracket. Origin: the wall plate's back face (Blender y = 0), centred on the
#                     plate in x, bottom = 0. The arm and bell reach out to -y (into the room).
# Origin = bottom centre and -y = front for the others (MiniGameModels.Hang turns the holder's -z to face the room).
# Every object keeps the Tripo atlas (one material PR_Late_H_Props: 2048 colour + normal in Materials/Textures).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python h_props_v1.py -- [--export] [--render]
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_props_v1 as gp

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
gp.OUT = os.path.join(HERE, "Export", "H")
TEX = gp.TEX
SOURCE = "R510_H_A1_Props.fbx"
# (row index, name, size rule, size, target triangles, straighten)
PICKS = [(0, "H_ChessClock", "h", 1.0, 3000, True),       # height before the pedestal stretch (see CLOCK_HEIGHT)
         (1, "H_PieceChest", "w", 1.2, 4000, True),
         (2, "H_NotationTablet", "h", 1.6, 1000, True),
         (3, "H_HallBell", "h", 1.6, 4000, False)]
CLOCK_HEIGHT = 1.5


def coords(me):
    co = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def straighten(ob):
    """Tripo turned the clock and chest a few degrees: turn the footprint's long side back onto x (about the centre)."""
    co = coords(ob.data)
    lo, hi = co.min(0), co.max(0)
    foot = co[co[:, 2] < lo[2] + .1 * (hi[2] - lo[2])][:, :2]
    best, angle = 1e9, 0.
    for deg in np.arange(-15., 15.01, .25):                     # smallest footprint box = square to the axes
        a = math.radians(deg)
        r = foot @ np.array([[math.cos(a), math.sin(a)], [-math.sin(a), math.cos(a)]], np.float32)
        area = np.ptp(r[:, 0]) * np.ptp(r[:, 1])
        if area < best: best, angle = area, a      # r = foot turned by +a; the mesh is turned by the same +a below
    c = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, 0.))
    ob.data.transform(Matrix.Translation(c) @ Matrix.Rotation(angle, 4, 'Z') @ Matrix.Translation(-c))
    ob.data.update()
    print("STRAIGHTEN %-16s %.2f deg" % (ob.name, math.degrees(angle)))


def pedestal_top(me):
    """Height of the pedestal's top: the highest slice still (nearly) as wide as the whole object."""
    co = coords(me)
    H = co[:, 2].max()
    W = np.ptp(co[:, 0])
    top = 0.
    for z in np.linspace(0., .8 * H, 161):
        s = co[np.abs(co[:, 2] - z) < .004 * H]
        if len(s) and np.ptp(s[:, 0]) > .94 * W: top = z
    return top


def raise_pedestal(ob):
    me = ob.data
    top = pedestal_top(me)
    H = coords(me)[:, 2].max()
    z0, z1 = .3 * top, .7 * top                               # the plain marble band of the pedestal body
    grow = CLOCK_HEIGHT - H
    s = 1. + grow / (z1 - z0)
    for v in me.vertices:
        z = v.co.z
        if z > z1: v.co.z = z + grow
        elif z > z0: v.co.z = z0 + (z - z0) * s
    me.update()
    print("PEDESTAL top %.3f -> %.3f (band %.3f..%.3f x %.2f), clock %.2f m" % (top, top + grow, z0, z1, s, CLOCK_HEIGHT))
    return top + grow


def marble_slot(ob, top):
    """Faces of the pedestal (centre below its top) take the tiling cream marble with box UVs in metres / 2."""
    me = ob.data
    cream = bpy.data.materials.get("PR_Late_MarbleCream") or bpy.data.materials.new("PR_Late_MarbleCream")
    me.materials.append(cream)
    slot = len(me.materials) - 1
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    n = 0
    for f in bm.faces:
        if f.calc_center_median().z > top + .004: continue
        f.material_index = slot
        a = int(np.argmax(np.abs(np.array(f.normal[:]))))
        u, w = [(1, 2), (0, 2), (0, 1)][a]
        for l in f.loops:
            l[uv].uv = (l.vert.co[u] / 2., l.vert.co[w] / 2.)
        n += 1
    bm.to_mesh(me); bm.free(); me.update()
    print("MARBLE pedestal faces: %d" % n)


def bell_origin(ob):
    co = coords(ob.data)
    ymax = co[:, 1].max()
    plate = co[co[:, 1] > ymax - .05 * np.ptp(co[:, 1])]
    cx = (plate[:, 0].min() + plate[:, 0].max()) / 2
    ob.data.transform(Matrix.Translation((-cx, -ymax, 0.)))
    ob.data.update()
    co = coords(ob.data)
    print("BELL size %s, plate x %.2f..%.2f z %.2f..%.2f" % ([round(float(v), 2) for v in np.ptp(co, 0)],
          plate[:, 0].min() - cx, plate[:, 0].max() - cx, plate[:, 2].min(), plate[:, 2].max()))


BELL_HINGE = 1.33        # just under the bracket's arm (h_props_v1 probe: arm at 1.35..1.55, bell collar below)


def split_bell(ob):
    """The bell swings on its own (MiniGameHallBell): cut the bell and its collar (faces under the arm, near the bell's
    axis) into H_HallBellBody with its origin on the hinge; the plate and arm stay H_HallBellBracket (origin unchanged).
    Prints the hinge in the bracket's frame for MiniGameModels.ChessHall.cs (Blender x, y, z = Unity holder x, z, y)."""
    me = ob.data
    co = coords(me)
    low = co[co[:, 2] < co[:, 2].min() + .05]
    ax, ay = low[:, 0].mean(), low[:, 1].mean()                # the clapper hangs on the bell's axis
    bm = bmesh.new(); bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    body = set()
    for f in bm.faces:
        c = f.calc_center_median()
        if c.z < BELL_HINGE and math.hypot(c.x - ax, c.y - ay) < .75: body.add(f.index)
    bm2 = bm.copy()                                                              # same face order
    bm2.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index in body], context='FACES')        # bracket
    bmesh.ops.delete(bm2, geom=[f for f in bm2.faces if f.index not in body], context='FACES')  # bell
    bm.to_mesh(me); bm.free(); me.update()
    ob.name = me.name = "H_HallBellBracket"
    me2 = bpy.data.meshes.new("H_HallBellBody"); bm2.to_mesh(me2); bm2.free()
    for m in me.materials: me2.materials.append(m)
    me2.transform(Matrix.Translation((-ax, -ay, -BELL_HINGE))); me2.update()
    ob2 = bpy.data.objects.new("H_HallBellBody", me2); bpy.context.scene.collection.objects.link(ob2)
    ob2["hinge"] = (float(ax), float(ay), BELL_HINGE)
    gp.smooth_by_angle(me2)
    print("BELL HINGE blender (%.3f, %.3f, %.3f) -> unity holder (%.3f, %.3f, %.3f); body %d tris, bracket %d tris"
          % (ax, ay, BELL_HINGE, ax, BELL_HINGE, ay, gp.tris(ob2), gp.tris(ob)))
    return ob2


def retexture(mat):
    nodes = mat.node_tree.nodes
    imgs = [n for n in nodes if n.type == 'TEX_IMAGE' and n.image]
    base = next(n for n in imgs if "basecolor" in n.image.name.lower())
    normal = next(n for n in imgs if "normal" in n.image.name.lower())
    name = "PR_Late_H_Props"
    out = base.image.copy(); out.name = name + "_BaseColor"; out.scale(2048, 2048)
    out.filepath_raw = os.path.join(TEX, name + "_BaseColor.png"); out.file_format = 'PNG'; out.save()
    nrm = normal.image.copy(); nrm.name = name + "_Normal"; nrm.colorspace_settings.name = 'Non-Color'; nrm.scale(2048, 2048)
    nrm.filepath_raw = os.path.join(TEX, name + "_Normal.png"); nrm.file_format = 'PNG'; nrm.save()
    base.image, normal.image = out, nrm
    mat.name = name


def build():
    src = gp.load(SOURCE)
    co, lab, *_ = gp.labels(src.data, 0)
    print("SOURCE: %d objects" % len(set(lab.tolist())))
    made = []
    for idx, name, rule, size, target, upright in PICKS:
        ob = gp.cut(src, lab == idx, name)
        if upright: straighten(ob)
        k = gp.place(ob, 0., rule, size)
        gp.decimate(ob, target)
        if name == "H_ChessClock": marble_slot(ob, raise_pedestal(ob))
        if name == "H_HallBell":
            bell_origin(ob)
            made.append(split_bell(ob))
        lo = coords(ob.data).min(0); hi = coords(ob.data).max(0)
        print("OBJ %-16s scale %6.2f  tris %6d  size %s" % (ob.name, k, gp.tris(ob), [round(float(b - a), 2) for a, b in zip(lo, hi)]))
        made.append(ob)
    bpy.data.objects.remove(src)
    retexture(made[0].data.materials[0])
    if "--export" in ARGS:
        for ob in made: gp.export(ob)
    return made


def render(made):
    """One preview of the four, the clock's marble slot shown in the swatch colour, a 1 m pawn for scale."""
    sc = bpy.context.scene
    cream = bpy.data.materials["PR_Late_MarbleCream"]
    cream.use_nodes = True
    t = cream.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(TEX, "PR_Late_MarbleCream_BaseColor.png"))
    cream.node_tree.links.new(t.outputs["Color"], cream.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    x = 0.
    for ob in made:
        if ob.name == "H_HallBellBody": continue          # hung on the bracket below, tilted as if swinging
        bb = coords(ob.data)
        w = np.ptp(bb[:, 0])
        ob.location.x = x - bb[:, 0].min()
        if ob.name == "H_HallBellBracket": ob.location.z = .3
        x += w + .7
    bracket = next((o for o in made if o.name == "H_HallBellBracket"), None)
    body = next((o for o in made if o.name == "H_HallBellBody"), None)
    if bracket and body:
        body.location = bracket.location + Vector(body["hinge"])
        body.rotation_euler.x = math.radians(12)
    bpy.ops.mesh.primitive_cylinder_add(radius=.28, depth=.6, location=(x + .3, 0, .3))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.25, location=(x + .3, 0, .8))
    bpy.ops.mesh.primitive_plane_add(size=200, location=(x / 2, 0, 0))
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.8, .8, .85, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(40), 0, math.radians(-25))); bpy.context.object.data.energy = 3
    cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = x + 1.5
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (x / 2 + .3, -40, 1. + 40 * math.tan(math.radians(14))); cam.rotation_euler = (math.radians(76), 0, math.radians(0))
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 2400, 900
    sc.view_settings.view_transform = 'AgX'
    sc.render.filepath = os.path.join(HERE, "render_h_props.png")
    bpy.ops.render.render(write_still=True)


made = build()
if "--render" in ARGS:
    render(made)
