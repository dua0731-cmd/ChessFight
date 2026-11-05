# 승규 11-03 "1번 별 볼트에 + 유리 오큘러스 넣어": v22C's Dome_1 (lamella star vault and lantern) kept, Dome_2 and Dome_3
# dropped, and Dome_3's glass oculus set into the star vault's crown ring (inside r 5, under the lantern): a glass disc
# led in an eight-point star (eight bars, two rings) so it reads with the vault's diamonds from below.
# Render only: nothing exported, Unity untouched.
# Run: blender -b grand_stair_hall_v22C.blend -P round_hall_v23.py -> grand_stair_hall_v23C.blend, renders_v30/*.png
import bpy, bmesh, math, os
from mathutils import Matrix, Vector
HERE = os.path.dirname(os.path.abspath(__file__))
D = math.pi / 180
HY = 92.0
CLAY = bpy.data.materials["Arch_Clay"]
GLASS = bpy.data.materials.get("Glass_TeamDivider") or CLAY
sc = bpy.context.scene

for c in [c for c in bpy.data.collections if c.name.startswith(("Dome_2", "Dome_3"))]:
    for o in list(c.objects):
        bpy.data.objects.remove(o)
    bpy.data.collections.remove(c)
dome = next(c for c in bpy.data.collections if c.name.startswith("Dome_1"))
dome.name = "Dome_1 star vault + glass oculus"
bpy.context.view_layer.layer_collection.children[dome.name].exclude = False


def polar(phi, r, z):
    return Vector((r * math.cos(phi * D), HY + r * math.sin(phi * D), z))


def add(name, bm, mat=CLAY, smooth=False):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me); dome.objects.link(o)
    return o


def ring(name, r0, r1, z0, z1, mat=CLAY, steps=96):
    """A flat annulus (r0 = 0: a disc) round the hub, z0 .. z1."""
    bm = bmesh.new()
    prof = [(r0, z0), (r1, z0), (r1, z1), (r0, z1)] if r0 > 0 else [(0, z0), (r1, z0), (r1, z1), (0, z1)]
    vs = [bm.verts.new(polar(0, r, z)) for r, z in prof]
    es = [bm.edges.new((vs[i], vs[(i + 1) % 4])) for i in range(4)] if r0 > 0 else \
         [bm.edges.new((vs[i], vs[i + 1])) for i in range(3)]
    bmesh.ops.spin(bm, geom=vs + es, cent=(0, HY, 0), axis=(0, 0, 1), angle=2 * math.pi, steps=steps, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    return add(name, bm, mat, smooth=True)


def bar(name, a, length, z, w=.2, h=.22, off=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation(polar(a, off + length / 2, z)) @ Matrix.Rotation(a * D, 4, "Z")
                 @ Matrix.Diagonal((length, w, h, 1)))
    return add(name, bm)


# The crown ring of Dome_1 is open inside r 5.0 (z 44.4 .. 47.6); the glass sits in it at 46.4 .. 46.7. The leading is
# under the glass, where it is seen from the hall (Workbench draws the glass opaque). v22's X of spokes and its boss are
# gone: the chandelier hangs from the oculus' own boss in the glass' middle.
for o in [o for o in dome.objects if o.name.startswith(("Crown_spoke", "Crown_boss", "Chandelier_6/Chain_"))]:
    bpy.data.objects.remove(o)
Z0, Z1, RG = 46.4, 46.7, 5.05
LZ = Z0 - .11                                              # the leading's middle, just under the glass
ring("Oculus_glass", 0, RG, Z0, Z1, GLASS)
ring("Oculus_frame", 4.75, 5.1, Z0 - .25, Z1 + .2)
for r in (1.7, 3.4):
    ring("Oculus_lead_ring_%.1f" % r, r - .13, r + .13, Z0 - .22, Z0)
for j in range(8):                                         # an eight-point star: long and short bars in turn
    a = j * 45
    bar("Oculus_lead_%d" % j, a, 4.8 if j % 2 == 0 else 3.4, LZ, off=0.0)
    # the star's points: from each short bar's tip out to the neighbouring long bars at the outer ring
    if j % 2 == 1:
        for s in (-1, 1):
            p0, p1 = polar(a, 3.4, LZ), polar(a + s * 45, 4.75, LZ)
            mid, v = (p0 + p1) / 2, p1 - p0
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.0)
            bm.transform(Matrix.Translation(mid) @ Matrix.Rotation(math.atan2(v.y, v.x), 4, "Z")
                         @ Matrix.Diagonal((v.length, .18, .2, 1)))
            add("Oculus_star_%d_%s" % (j, "a" if s < 0 else "b"), bm)
ball = bmesh.new()
bmesh.ops.create_uvsphere(ball, u_segments=16, v_segments=8, radius=.6, matrix=Matrix.Translation((0, HY, Z0 - .35)))
add("Oculus_boss", ball, smooth=True)
# the hub chandelier's chain from the boss down to its hook (33), links as v22's (ChainSegment x 1.6)
cme = bpy.data.meshes["ChainSegment"]
zs = [v.co.z for v in cme.vertices]
pitch = (max(zs) - min(zs)) * .9 * 1.6
z, i = Z0 - .8, 0
while z - pitch * .5 > 33.0:
    o = bpy.data.objects.new("Chandelier_6/Chain_%02d" % i, cme); dome.objects.link(o)
    o.location = (0, HY, z); o.rotation_euler = (0, 0, math.pi / 2 * (i % 2)); o.scale = (1.6,) * 3
    z -= pitch; i += 1
print("CHAIN", i, "links from", Z0 - .8)

# renders (v22C's cameras and settings)
out = os.path.join(HERE, "renders_v30"); os.makedirs(out, exist_ok=True)
for name in ("Cam_overview", "Cam_low", "Cam_from_balcony", "Cam_dome_up"):
    sc.camera = bpy.data.objects[name]
    sc.render.filepath = os.path.join(out, "%s.png" % name[4:])
    bpy.ops.render.render(write_still=True)
sc.camera = bpy.data.objects["Cam_overview"]
print("DONE", dome.name, len(dome.objects), "objects", sum(len(o.data.polygons) for o in dome.objects if o.type == "MESH"), "faces")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v23C.blend"))
