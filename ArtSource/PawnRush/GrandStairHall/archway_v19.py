# 승규 11-03: the entrance (the porch, x +-6, y -12 .. 0, into the foyer's south door) in 승규's castle archway
# (source/castle_archway.glb, 0.98 x 0.41 x 0.285): an arch, two passage walls running from it and a wing either side at
# their other end, each wing with three blind arched windows.
#   - the windows cut through ("창문쪽을 뚫어"): an arched cutter per window through the wing
#   - turned round ("반대로 배치해야지 기존 있던 블록아웃 치우고"): the arch is the way in at the porch's outer end
#     (its front at y -12.6), the wings are the foyer's south wall (their windowed face on the foyer's edge, y 0, out to
#     the foyer's side walls at x +-20), the passage walls along the porch
#   - 40.8 x 30.7 x 40.8 times: the arch's opening x +-6.5 over the porch walkway (+-6), the passage walls' faces at
#     +-6.9, 11.6 m high (the foyer walls 12)
#   - the blockout it stands in for (the foyer's south wall either side of the door, the porch walls) draws nothing and
#     keeps its colliders (Unity), the v9 "Entrance portal" goes
# No paint: Arch_Clay.
# Run: blender -b grand_stair_hall_v18.blend -P archway_v19.py   -> grand_stair_hall_v19.blend
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
originals = bpy.data.collections["_Blockout originals"]
col = bpy.data.collections.new("Entrance_archway_v19"); sc.collection.children.link(col)

HALF_W, FRONT_Y, BACK_Y = .49, -.205, .205   # the asset's half width, its wings' face and its arch's face (asset y)
SX = 20.0 / HALF_W              # the wings reach the foyer's side walls (x +-20)
SY = 12.6 / (BACK_Y - FRONT_Y)   # wings' face at y 0, the arch's face at y -12.6
# The windows (asset units): three a wing at x +-0.2565, 0.3285, 0.4005, 0.047 wide, sill z 0.05, round top to z 0.157,
# through the wing (y -0.205 .. -0.117).
WINDOWS = [s * c for c in (.2565, .3285, .4005) for s in (1, -1)]
HALF, SILL, CROWN, Y0, Y1 = .0235, .05, .157, -.22, -.10


def load():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "castle_archway.glb"))
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == "MESH"][0]
    me = src.data.copy(); me.transform(src.matrix_world)
    for o in new:
        bpy.data.objects.remove(o)
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.materials.clear(); me.materials.append(CLAY)
    return me


def apply(me, kind, **kw):
    o = bpy.data.objects.new("_w", me); sc.collection.objects.link(o)
    extra = kw.pop("cutter", None)
    if extra:
        co = bpy.data.objects.new("_c", extra); sc.collection.objects.link(co)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("m", kind)
    for k, v in kw.items():
        setattr(m, k, v)
    if extra:
        m.object = co
    bpy.ops.object.modifier_apply(modifier=m.name)
    if extra:
        bpy.data.objects.remove(co)
    bpy.data.objects.remove(o)
    return me


def cutter():
    """One arched prism per window, front to back through the wing."""
    bm = bmesh.new()
    spring = CROWN - HALF
    for cx in WINDOWS:
        prof = [(cx - HALF, SILL), (cx + HALF, SILL)]
        for i in range(17):
            a = math.pi * i / 16
            prof.append((cx + HALF * math.cos(a), spring + HALF * math.sin(a)))
        front = [bm.verts.new((x, Y0, z)) for x, z in prof]
        back = [bm.verts.new((x, Y1, z)) for x, z in prof]
        n = len(prof)
        bm.faces.new(front); bm.faces.new(list(reversed(back)))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    cm = bpy.data.meshes.new("_window_cutter"); bm.to_mesh(cm); bm.free()
    return cm


me = load()
me = apply(me, "DECIMATE", ratio=.15)
me = apply(me, "BOOLEAN", operation="DIFFERENCE", solver="EXACT", use_hole_tolerant=True, cutter=cutter())
for p in me.polygons:
    p.use_smooth = False
me.name = "Entrance_archway"
o = bpy.data.objects.new("Entrance_archway", me); col.objects.link(o)
# Half round: asset y -> -y, so the wings' face (y -0.205) lands on y 0 and the arch on the porch's outer end.
o.matrix_world = (Matrix.Translation((0, FRONT_Y * SY, 0)) @ Matrix.Rotation(math.pi, 4, "Z")
                  @ Matrix.Diagonal((SX, SY, SX, 1)))
for s in bpy.context.selected_objects:
    s.select_set(False)
bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
vs = [o.matrix_world @ v.co for v in me.vertices]
print("ARCHWAY", len(me.polygons), "faces", [round(min(v[i] for v in vs), 2) for i in range(3)],
      [round(max(v[i] for v in vs), 2) for i in range(3)], "passage inner x", round(.169 * SX, 2))

for name in ("B1_GrandStair/Porch_wall_W", "B1_GrandStair/Porch_wall_E", "Entrance portal",
             "B1_GrandStair/Foyer_wall_S_(west_of_the_door)", "B1_GrandStair/Foyer_wall_S_(east_of_the_door)"):
    p = bpy.data.objects.get(name)
    if p:
        for c in list(p.users_collection):
            c.objects.unlink(p)
        originals.objects.link(p)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v19.blend"))


# 승규 "받침 깔기": the wings and passage walls stand outside the floors (the porch floor is x +-6, the foyer's starts at
# y 0). A stone base under them either side, down to the hall's deep -5 like the porch floor, its top 2 cm under their
# foot: under the wing (x +-6 .. +-20.5, y -2.9 .. 0) and under the passage wall and the arch's post (x +-6 .. +-9.6,
# y -12.8 .. -2.9). Out of reach, so it draws only.
# 승규 11-03 "입구 하단도 모델링 해줘" (the plain boxes showed from outside): now built as masonry. A core 6 cm inside the
# outline; on each face seen from outside, courses of dressed stone blocks (0.6 m high, 1.5 m long, half a block
# staggered course to course, 4 cm joints, every edge chamfered 5 cm) standing 6 cm proud of the core; over them a band
# and a projecting cornice slab (out 0.12 and 0.25 m, chamfered) under the walls' foot.
COURSE, BLOCK, JOINT, PROUD, CHAMFER = .607, 1.5, .04, .06, .05
TOP, BAND, BOTTOM = -.02, -.75, -5.0


def _cube(bm, lo, hi, bevel, segments=1):
    new = bmesh.ops.create_cube(bm, size=1)["verts"]
    for v in new:
        v.co = Vector([(lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)])
    edges = list({e for v in new for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=segments, affect="EDGES")


def _grow(box, d):
    x0, x1, y0, y1 = box
    return (x0 - d, x1 + d, y0 - d, y1 + d)


def base(name, boxes, faces):
    """boxes: (x0, x1, y0, y1) of the outline; faces: (axis, at, u0, u1, out) for each face seen from outside, `axis`
    the one it is square to ('x' or 'y'), `out` +-1 its outward side."""
    bm = bmesh.new()
    for b in boxes:
        x0, x1, y0, y1 = _grow(b, -PROUD)
        _cube(bm, (x0, y0, BOTTOM), (x1, y1, BAND), .02)
        x0, x1, y0, y1 = _grow(b, .12)
        _cube(bm, (x0, y0, BAND), (x1, y1, BAND + .3), .05)
        x0, x1, y0, y1 = _grow(b, .25)
        _cube(bm, (x0, y0, BAND + .3), (x1, y1, TOP), .1, 2)
    blocks = 0
    for axis, at, u0, u1, out in faces:
        for k in range(int(round((BAND - BOTTOM) / COURSE))):
            z0 = BOTTOM + k * COURSE
            z1 = min(z0 + COURSE, BAND) - JOINT
            u = u0 - (k % 2) * BLOCK / 2
            while u < u1:
                a, b = max(u, u0), min(u + BLOCK, u1) - JOINT
                if b - a > .2:
                    d0, d1 = (at - .3, at) if out > 0 else (at, at + .3)
                    if axis == "x":
                        _cube(bm, (d0, a, z0), (d1, b, z1), CHAMFER)
                    else:
                        _cube(bm, (a, d0, z0), (b, d1, z1), CHAMFER)
                    blocks += 1
                u += BLOCK
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    print("BASE", name, blocks, "blocks", len(me.polygons), "faces")
    return ob


for s in (1, -1):
    wa, wb = sorted((6.0 * s, 20.5 * s)); pa, pb = sorted((6.0 * s, 9.6 * s))
    faces = [("y", -2.9, min(9.6 * s, 20.5 * s), max(9.6 * s, 20.5 * s), -1),   # the wing's front, beside the passage
             ("x", 20.5 * s, -2.9, 0.0, s),                                     # the wing's end
             ("x", 9.6 * s, -12.8, -2.9, s),                                    # the passage's outer side
             ("y", -12.8, pa, pb, -1),                                          # the arch's front
             ("x", 6.0 * s, -12.8, 0.0, -s)]                                    # the porch side, under the porch floor's edge
    base("Entrance_base_" + ("E" if s > 0 else "W"), [(wa, wb, -2.9, 0.0), (pa, pb, -12.8, -2.9)], faces)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v19.blend"))
print("BASES done")
