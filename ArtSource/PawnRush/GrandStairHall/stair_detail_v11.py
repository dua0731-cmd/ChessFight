# The grand stair's two flights (lower: foyer -> middle landing, upper: middle landing -> central landing) as
# dressed stone steps like the reference photo: each tread is a row of stone slabs with a rounded nose that
# overhangs a recessed riser, thin joints between slabs (staggered from row to row), small chamfers and a
# little unevenness from slab to slab. The walked top stays where the blockout's was (slab tops are the
# step tops to within 3 mm), so nothing about the play changes. No paint: v9's grey Arch_Clay.
# Run: blender -b grand_stair_hall_v10.blend -P stair_detail_v11.py   -> grand_stair_hall_v11.blend
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TREAD = 0.10          # slab thickness
NOSE = 0.04           # how far the nose overhangs the riser
NOSE_R = 0.045        # rounded nose
JOINT = 0.008         # gap between slabs
random.seed(11)

CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Stairs_v11"); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]


def retire(ob):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    originals.objects.link(ob)


def corners(ob):
    vs = [ob.matrix_world @ v.co for v in ob.data.vertices]
    return Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)])


def cube(bm, lo, hi):
    r = bmesh.ops.create_cube(bm, size=1)
    vs = r["verts"]
    for v in vs:
        v.co = Vector([lo[i] if v.co[i] < 0 else hi[i] for i in range(3)])
    return vs


def bevel(bm, edges, off, segs):
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=off, offset_type="OFFSET", segments=segs, profile=0.5,
                        affect="EDGES", clamp_overlap=True)


def slab(x0, x1, y0, y1, ztop, front):
    """One stone tread slab: front = -Y (the nose), rounded front-top edge, chamfered everywhere else."""
    bm = bmesh.new()
    cube(bm, (x0, y0, ztop - TREAD), (x1, y1, ztop))
    nose = [e for e in bm.edges if all(abs(v.co.y - y0) < 1e-6 and abs(v.co.z - ztop) < 1e-6 for v in e.verts)]
    bevel(bm, nose, NOSE_R, 5)
    under = [e for e in bm.edges if all(abs(v.co.y - y0) < 1e-6 and abs(v.co.z - (ztop - TREAD)) < 1e-6 for v in e.verts)]
    bevel(bm, under, 0.012, 2)
    sharp = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(60)
             and not all(abs(v.co.y - y1) < 1e-6 for v in e.verts)]        # the back is under the next riser
    bevel(bm, sharp, 0.005, 1)
    # A little unevenness: each slab a hair high or low and tipped a fraction of a degree.
    c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, ztop))
    tilt = Matrix.Rotation(math.radians(random.uniform(-.12, .12)), 4, "X") @ Matrix.Rotation(
        math.radians(random.uniform(-.06, .06)), 4, "Y")
    bm.transform(Matrix.Translation(c + Vector((0, 0, random.uniform(-.003, .001)))) @ tilt @ Matrix.Translation(-c))
    return bm


def joints(x0, x1, offset):
    """Slab ends across the width: 1.6 ~ 2.4 m pieces, each row started at its own offset."""
    cuts, x = [x0], x0 + offset
    while x < x1 - 0.6:
        if x > x0 + 0.6:
            cuts.append(x)
        x += random.uniform(1.6, 2.4)
    cuts.append(x1)
    return cuts


def add(bm, into):
    me = bpy.data.meshes.new("_t"); bm.to_mesh(me); bm.free()
    into.from_mesh(me); bpy.data.meshes.remove(me)


def flight(prefix):
    steps = sorted([o for o in bpy.data.objects if o.name.startswith(prefix + "_step_")],
                   key=lambda o: int(o.name.rsplit("_", 1)[1]))
    treads, bodies = bmesh.new(), bmesh.new()
    for k, o in enumerate(steps):
        lo, hi = corners(o)
        y0, y1, top = lo.y, hi.y, hi.z
        # Riser set back by the nose; body and slab reach under the next step's nose so no slot opens.
        b = bmesh.new(); cube(b, (lo.x, y0 + NOSE, lo.z), (hi.x, y1 + NOSE, top - TREAD))
        riser = [e for e in b.edges if all(abs(v.co.y - (y0 + NOSE)) < 1e-6 and abs(v.co.z - (top - TREAD)) < 1e-6 for v in e.verts)]
        bevel(b, riser, 0.01, 1)
        add(b, bodies)
        cuts = joints(lo.x, hi.x, random.uniform(0.3, 1.9) if k % 2 else random.uniform(0.9, 1.3))
        for a, c in zip(cuts, cuts[1:]):
            add(slab(a + (JOINT / 2 if a > lo.x else 0), c - (JOINT / 2 if c < hi.x else 0), y0, y1 + NOSE, top, True), treads)
        retire(o)
    out = []
    for bm, name, ang in ((treads, prefix + "_treads", 30), (bodies, prefix + "_risers", 30)):
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        me.materials.append(CLAY)
        ob = bpy.data.objects.new(name.split("/")[-1].replace("B1_GrandStair", "") or name, me)
        ob.name = name
        col.objects.link(ob)
        bpy.context.view_layer.objects.active = ob
        for s in bpy.context.selected_objects:
            s.select_set(False)
        ob.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(ang))
        out.append(ob)
    print("FLIGHT", prefix, len(steps), "steps,", sum(len(o.data.polygons) for o in out), "faces")
    return out


for p in ("B1_GrandStair/Lower_flight", "B1_GrandStair/Upper_flight"):
    flight(p)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v11.blend"))
