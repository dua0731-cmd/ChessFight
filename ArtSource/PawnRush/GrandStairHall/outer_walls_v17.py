# The hall's outer walls in 승규's architectural wall (source/architectural_wall.glb, 11-03): three bays (window, blind
# arch, window) between pilasters on a panelled base, 0.98 x 0.16 x 0.59. It replaces the v9 castle module walls
# (Hall_wall_E/W/S (arch)) and v16's plinth (the asset has its own base down to the floor); v16's corner columns stay.
#   - the two windows are cut through (the glass between the tracery bars, front to back) so daylight comes in
#   - one row, the wall's full height (-4 .. 26), as the v9 walls (승규: no stacking):
#     east / west (y 12 .. 92): two whole walls each; the short south walls (13 m): one window bay, its open cut end
#     inside the corner column
#   - its most forward face (the pilasters) on the old inner face; the walls grow outwards, where nobody walks
# No paint: v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v16.blend -P outer_walls_v17.py   -> grand_stair_hall_v17.blend
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import window_kit   # 11-03: the windows opened whole and their tracery drawn again in clean bars
FLOOR, TOP = -4.0, 26.0
DEPTH = 25.0                 # depth scale (0.162 -> 4 m): the relief half as deep as the height scale, not 8 m thick
PANE = (0.0105, 0.25)        # the cutter: from just in front of the glass to behind the wall
CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Outer_walls_v17"); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]


def retire(name):
    o = bpy.data.objects.get(name)
    if o:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        originals.objects.link(o)


def load():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "architectural_wall.glb"))
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == "MESH"][0]
    me = src.data.copy(); me.transform(src.matrix_world)
    for o in new:
        bpy.data.objects.remove(o)
    me.materials.clear(); me.materials.append(CLAY)
    return me


def cutter(me):
    """The window glass (front-facing faces on the glass plane inside the two windows), pushed through the wall."""
    bm = bmesh.new(); bm.from_mesh(me)
    keep = set()
    for f in bm.faces:
        c = f.calc_center_median()
        if f.normal.y < -.9 and .012 <= c.y < .02 and .23 < abs(c.x) < .33 and .17 < c.z < .45:
            keep.add(f)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keep], context="FACES")
    ext = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    moved = {v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)}
    for v in bm.verts:
        v.co.y = PANE[1] if v in moved else PANE[0]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    cm = bpy.data.meshes.new("_window_cutter"); bm.to_mesh(cm); bm.free()
    print("CUTTER", len(keep), "glass faces")
    return cm


def decimate(me, ratio):
    o = bpy.data.objects.new("_d", me); sc.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("d", "DECIMATE"); m.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(o)
    return me


def cut(me, cm):
    o = bpy.data.objects.new("_w", me); sc.collection.objects.link(o)
    co = bpy.data.objects.new("_c", cm); sc.collection.objects.link(co)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("cut", "BOOLEAN"); m.operation = "DIFFERENCE"; m.object = co
    m.solver = "EXACT"; m.use_hole_tolerant = True
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(co); bpy.data.objects.remove(o)
    return me


def bay(me, keep_left):
    """One window bay: cut at the big pilaster's middle (x -+0.142), the far side thrown away."""
    m = me.copy()
    bm = bmesh.new(); bm.from_mesh(m)
    x = -.142 if keep_left else .142
    r = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0),
                               plane_no=(1, 0, 0), clear_outer=keep_left, clear_inner=not keep_left)
    # 승규 11-03 ("남쪽 부분 깨진다니까"): the cut left the wall open, and Unity draws no back faces, so you saw through it.
    # The cut section is closed with flat faces on the cut plane.
    # Only edges lying on the plane, and only faces lying on it: a fill reaching off the plane drew long slivers across
    # the wall's face (the first try).
    on = lambda v: abs(v.co.x - x) < 1e-5
    cut = [e for e in r["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and on(e.verts[0]) and on(e.verts[1])]
    capped = [f for f in bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=cut, normal=(1, 0, 0))["geom"]
              if isinstance(f, bmesh.types.BMFace)]
    stray = [f for f in capped if not all(on(v) for v in f.verts)]
    bmesh.ops.delete(bm, geom=stray, context="FACES_ONLY")
    capped = [f for f in capped if f not in stray]
    print("CAP stray faces dropped", len(stray))
    want = 1 if keep_left else -1
    for f in capped:
        if f.normal.x * want < 0:
            f.normal_flip()
    print("CAP", "left" if keep_left else "right", len(capped), "faces")
    bm.to_mesh(m); bm.free()
    return m


me = load()
lo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)])
me = window_kit.prepare(me, .35)   # the windows cut on the full mesh, the rest decimated
me.name = "Outer_wall_module"
for p in me.polygons:
    p.use_smooth = False
print("MODULE", len(me.polygons), "faces after the cut")
left, right = bay(me, True), bay(me, False)
W, H, D = 0.98, 0.59, 0.162
local = Matrix.Translation(-lo)             # x 0..0.98, y 0 (front) .. 0.162, z 0..0.59


def place(name, mesh, loc, yaw, sx, centres=window_kit.CENTRES):
    o = bpy.data.objects.new(name, mesh); col.objects.link(o)
    o.matrix_world = (Matrix.Translation(loc) @ Matrix.Rotation(math.radians(yaw), 4, "Z")
                      @ Matrix.Diagonal((sx, DEPTH, (TOP - FLOOR) / H, 1)) @ local)
    for s in bpy.context.selected_objects:
        s.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    window_kit.tracery(name + " tracery", o.matrix_world.copy(), centres, col, CLAY)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
    return o


# East: front towards -x (yaw -90: local x -> -y); west: front towards +x (yaw 90: local x -> +y). 41.2 m each,
# overlapping 2.4 m at y 52 so the end pilasters close the joint.
SX = 41.2 / W
for k in range(2):
    place("Outer_wall_E_%d" % k, me, (34, 92 - 38.8 * k, FLOOR), -90, SX)
    place("Outer_wall_W_%d" % k, me, (-34, 12 + 38.8 * k, FLOOR), 90, SX)
# South (front towards +y, yaw 180: local x -> -x): its finished end at x +-21, the cut at x +-34 in the column.
SB = 13.0 / (W / 2 - .142)
place("Outer_wall_S_east", right, (21 + SB * W, 14, FLOOR), 180, SB, (.2806,))      # local x 0.98 (finished) at x 21
place("Outer_wall_S_west", left, (-21, 14, FLOOR), 180, SB, (-.2806,))              # local x 0 (finished) at x -21

for name in ("Hall_wall_E (arch)", "Hall_wall_W (arch)", "Hall_wall_S_(east) (arch)", "Hall_wall_S_(west) (arch)",
             "Outer_wall_plinth"):
    retire(name)
for o in col.objects:
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    print("WALL", o.name, [round(min(v[i] for v in vs), 2) for i in range(3)], [round(max(v[i] for v in vs), 2) for i in range(3)])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v17.blend"))
