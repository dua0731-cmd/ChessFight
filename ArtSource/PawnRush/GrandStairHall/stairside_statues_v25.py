# 승규 11-03 (a Unity shot of the queen on the red floor beside the lower flight): "동상의 위치를 양 옆에 2개씩 배치할건데
# 동상 발판 조절해서 계단 양 사이드에 사진 같이 배치 해". v13's four foyer statues move into the hall beside the grand stair,
# two a side, on the red floor (-4), each on a new octagonal stone plinth that lifts its own pedestal to the stair's
# height where it stands, so it guards the steps rather than sinking below them:
#   lower flight (y 14 .. 30, z 0 .. 4), at y 22 (stair z 2):  queen west (as the shot), king east
#   upper flight (y 36 .. 52, z 4 .. 8), at y 44 (stair z 6):  bishop west, knight east
# Centres x +-15 (the stair's flank walls are at +-8.3, the hall's outer walls at +-34), each turned to the stair and
# 30 degrees down it, towards the way in. The foyer keeps no statues.
# Visual only, as before (Unity draws them, nothing collides). No paint: v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v24C.blend -P stairside_statues_v25.py -> grand_stair_hall_v25C.blend, renders_v32/*.png
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
FLOOR, SIDE_X, TURN = -4.0, 15.0, 30.0
CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections["Foyer_statues_v13"]
col.name = "Stair_statues_v25"


def stair_z(y):
    if y <= 30:
        return (y - 14) / 16 * 4
    if y <= 36:
        return 4.0
    return 4 + (y - 36) / 16 * 4


# name, side (-1 west, +1 east), y. v13 baked each statue facing the aisle across the foyer: the west ones +x, the
# east ones -x - the stair here, so only the turn down the stair is added.
PLACES = [("Queen", -1, 22.0), ("King", 1, 22.0), ("Bishop", -1, 44.0), ("Knight", 1, 44.0)]


def octagon_plinth(name, c, rad, top, yaw):
    """An octagonal plinth from the hall floor to `top`: a base step, the shaft, a moulded cap under the pedestal."""
    tiers = [(FLOOR, FLOOR + .35, rad + .45), (FLOOR + .35, FLOOR + .55, rad + .3), (FLOOR + .55, top - .55, rad),
             (top - .55, top - .35, rad + .18), (top - .35, top, rad + .32)]
    bm = bmesh.new()
    for z0, z1, r in tiers:
        rr = r / math.cos(math.pi / 8)          # the octagon's flats at r
        lo = [bm.verts.new((c.x + rr * math.cos(yaw + math.pi / 8 + k * math.pi / 4),
                            c.y + rr * math.sin(yaw + math.pi / 8 + k * math.pi / 4), z0)) for k in range(8)]
        hi = [bm.verts.new((v.co.x, v.co.y, z1)) for v in lo]
        bm.faces.new(list(reversed(lo))); bm.faces.new(hi)
        for k in range(8):
            bm.faces.new((lo[k], lo[(k + 1) % 8], hi[(k + 1) % 8], hi[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    return o


for name, side, y in PLACES:
    ob = bpy.data.objects["Foyer_statue_" + name]
    me = ob.data
    # The statue's own pedestal: its footprint (verts within 0.3 m of the bottom), in the mesh's frame.
    foot = [v.co for v in me.vertices if v.co.z < .3]
    fc = sum(foot, Vector()) / len(foot)
    rad = max(math.hypot(v.x - fc.x, v.y - fc.y) for v in foot)
    turn = math.radians(-TURN if side < 0 else TURN)      # west: from +x towards -y; east: from -x towards -y
    top = stair_z(y)
    target = Vector((side * SIDE_X, y, top))
    rot = Matrix.Rotation(turn, 4, "Z")
    ob.matrix_world = Matrix.Translation(target - rot @ Vector((fc.x, fc.y, 0))) @ rot
    ob.name = "Stair_statue_" + name
    octagon_plinth("Stair_statue_%s plinth" % name, target, rad * .92, top, turn)
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    print("STATUE", name, "pedestal r %.2f" % rad, "on a plinth to z %.2f" % top,
          [round(min(v[i] for v in bb), 2) for i in range(3)], [round(max(v[i] for v in bb), 2) for i in range(3)])

# Renders: down the stair from the porch, as 승규's shot, and from the side.
def camera(name, loc, at, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    o = bpy.data.objects.new(name, cd); col.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


cams = [camera("Cam_stair_up", (0, 6, 6.5), (0, 40, 5), 20),
        camera("Cam_stair_down", (0, 58, 14), (0, 18, 2), 20),
        camera("Cam_stair_side", (30, 4, 22), (0, 33, 2), 24)]
out = os.path.join(HERE, "renders_v32"); os.makedirs(out, exist_ok=True)
for c in cams:
    sc.camera = c
    sc.render.filepath = os.path.join(out, "%s.png" % c.name[4:])
    bpy.ops.render.render(write_still=True)
sc.camera = cams[0]
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v25C.blend"))
