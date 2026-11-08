# 승규 11-03 "여기 부분도 자연스럽게 해": the foyer's side walls (blockout Foyer_wall_W/E, x +-20 .. +-21, y -1 .. 14,
# -5 .. 12) were the last checker walls seen from outside, between the entrance archway's wings (y 0) and the hall's
# south walls (y 14). Each is now one window bay of the v17 architectural wall (source/architectural_wall.glb, its
# window cut through as there): 16.75 m long (from the wing's back, y -2.75, to y 14), -5 .. 12 high, 1 m thick in the old wall's place (its face outwards, where
# it was seen; the plain back on the foyer side), its finished end on the archway's wing and its cut pilaster into the
# south wall's end. Unity keeps the blockout walls' colliders. No paint: Arch_Clay.
# Run: blender -b grand_stair_hall_v19.blend -P foyer_sides_v20.py   -> grand_stair_hall_v20.blend
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import window_kit   # the windows opened whole, their tracery in clean bars, as the outer walls


PANE = (0.0105, 0.25)        # the cutter: from just in front of the glass to behind the wall
CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Foyer_sides_v20"); sc.collection.children.link(col)
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


FLOOR, TOP = -5.0, 12.0
col.name = "Foyer_sides_v20"
me = load()
lo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)])
me = window_kit.prepare(me, .35)   # the windows cut on the full mesh, the rest decimated
for p in me.polygons:
    p.use_smooth = False
left, right = bay(me, True), bay(me, False)
W, H, D = 0.98, 0.59, 0.162
local = Matrix.Translation(-lo)
Y0 = -2.75                      # the archway wing's back face, so the corner closes
SB = (14.0 - Y0) / (W / 2 - .142)


def place(name, mesh, loc, yaw, centres):
    o = bpy.data.objects.new(name, mesh); col.objects.link(o)
    o.matrix_world = (Matrix.Translation(loc) @ Matrix.Rotation(math.radians(yaw), 4, "Z")
                      @ Matrix.Diagonal((SB, 1.0 / D, (TOP - FLOOR) / H, 1)) @ local)
    for s in bpy.context.selected_objects:
        s.select_set(False)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    window_kit.tracery(name + " tracery", o.matrix_world.copy(), centres, col, CLAY)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    print("WALL", name, [round(min(v[i] for v in vs), 2) for i in range(3)], [round(max(v[i] for v in vs), 2) for i in range(3)])


# East: face towards +x (yaw 90: local x -> +y, the face -> +x), the finished end (local 0) at the wing's back.
place("Foyer_side_E", left, (21, Y0, FLOOR), 90, (-.2806,))
# West: face towards -x (yaw -90: local x -> -y), the right bay's finished end (local 0.98) at the wing's back.
place("Foyer_side_W", right, (-21, Y0 + W * SB, FLOOR), -90, (.2806,))
# 승규 11-03 "입구쪽 모서리 벽이랑 벽 부드럽게 이어붙일 수 있도록 해", then "모서리 부분에 기둥을 넣었더니 깨졌어 기둥 빼주고
# 자연스럽게 붙여": no columns. Where two walls met only along a line (Tripo faces, uneven, so a slit opened through),
# the one that ends now runs on into the other: its last 0.8 m stretched evenly so its end lies inside the side wall:
#   - the archway's wings (end x +-19.5 .. 20) into the side wall (x +-20 .. 21): the end on to +-20.7
#   - the hall's south outer wall (v17 Outer_wall_S_west/east, x +-21.01 .. 26, y 10 .. 14) into the side wall: on to
#     +-20.3
def run_into(name, s, fixed, old_end, new_end):
    """`name`'s part between |x| `fixed` and its end `old_end` (on the side s) stretched evenly to end at `new_end`."""
    o = bpy.data.objects[name]
    if o.data.users > 1:
        o.data = o.data.copy()                   # the outer walls share their bay meshes
    mw = o.matrix_world; inv = mw.inverted()
    moved = 0
    for v in o.data.vertices:
        w = mw @ v.co
        t = (s * w.x - fixed) / (old_end - fixed)
        if t > 0:
            w.x = s * (fixed + t * (new_end - fixed))
            v.co = inv @ w
            moved += 1
    print("RUN INTO", name, s, moved, "vertices")


for s in (1, -1):
    run_into("Entrance_archway", s, 19.2, 19.99, 20.7)
    run_into("Outer_wall_S_east" if s > 0 else "Outer_wall_S_west", s, 21.8, 21.01, 19.7)   # +0.6 below: 20.3

# 승규 11-03 (the hall's outer corner, "큰 외곽 벽 기둥을 제거 하고 자연스럽게 붙여"): the v16 stone columns where the south
# walls meet the side walls go. The side wall (Outer_wall_E_1 / W_0, its finished end with its 4.4 m end pilaster at
# y 12.02) reaches south until that end is flush with the south wall's outer face (y 9.96): the pilaster turns the
# corner. The wall stretches 5 % along its length for it (its other end stays, overlapping the next side wall at y 51 by
# 2.4 m as before; moved whole, that overlap was 0.3 m and a slit opened). The south wall moves 0.6 m along
# whole so its cut end (x +-34.0) lies inside the side wall (x +-34.1 .. 37.9) and the foyer end stays at +-20.3.
def shift(name, d):
    for n in (name, name + " tracery"):
        o = bpy.data.objects.get(n)
        if o:
            o.matrix_world = Matrix.Translation(d) @ o.matrix_world


def ys(name):
    o = bpy.data.objects[name]
    return [(o.matrix_world @ v.co).y for v in o.data.vertices]


for s in (1, -1):
    c = bpy.data.objects.get("Corner_column_SE" if s > 0 else "Corner_column_SW")
    if c:
        bpy.data.objects.remove(c)
    side, south = ("Outer_wall_E_1", "Outer_wall_S_east") if s > 0 else ("Outer_wall_W_0", "Outer_wall_S_west")
    far, end, to = max(ys(side)), min(ys(side)), min(ys(south))
    k = (far - to) / (far - end)
    for n in (side, side + " tracery"):
        o = bpy.data.objects[n]
        o.matrix_world = (Matrix.Translation((0, far, 0)) @ Matrix.Diagonal((1, k, 1, 1)) @ Matrix.Translation((0, -far, 0))
                          @ o.matrix_world)
    d = to - end
    shift(south, (.6 * s, 0, 0))
    print("CORNER", side, "moved", round(d, 2), "end now", round(min(ys(side)), 2))


for name in ("B1_GrandStair/Foyer_wall_W", "B1_GrandStair/Foyer_wall_E"):
    retire(name)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v20.blend"))
