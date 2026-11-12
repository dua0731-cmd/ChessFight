# R589 (승규 "체스 판을 레퍼런스 사진처럼"): the gold crown on the front of the lobby stage's frame, over the carpet,
# as in reference.webp / reference_stairs.webp. A band with a rolled rim, eight points round it (tall and short in
# turn) with a ball on each, a ruby under each point. Flattened front to back so it stands close to the frame.
# Origin at the middle of its foot; Unity scales it to the stage (LobbyStage.Hall.Board.cs HallCrown).
#   blender -b --factory-startup --python stage_crown.py [-- out.fbx]
import bpy, bmesh, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "Lobby", "KingdomHall", "StageCrown.fbx"))
SEG, POINTS, DEPTH = 64, 8, .55          # segments round, points, front-to-back squash
BAND = [(0, 1.0), (.04, 1.05), (.07, 1.05), (.09, 1.02), (.25, 1.02), (.27, 1.05), (.31, 1.05), (.34, 1.0),
        (.34, .93), (0, .93)]           # (z, r) round the band's section, a closed loop
TALL, SHORT, BALL, RUBY = .62, .4, .085, .075

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)


def mat(name, rgb, metal, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = rgb + (1,)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    return m


bm = bmesh.new()
gold_i, ruby_i = 0, 1
P = lambda r, a, z: (r * math.cos(a), DEPTH * r * math.sin(a), z)

# the band, lathed
rings = [[bm.verts.new(P(r, 2 * math.pi * k / SEG, z)) for (z, r) in BAND] for k in range(SEG)]
n = len(BAND)
for k in range(SEG):
    a, b = rings[k], rings[(k + 1) % SEG]
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])).material_index = gold_i

# the points: a wedge from the band's top to a tip, outer and inner faces, a ball on the tip
top = BAND[7][0]
for p in range(POINTS):
    a = 2 * math.pi * (p + .5) / POINTS
    h = TALL if p % 2 == 0 else SHORT
    da = math.pi / POINTS * .92
    ro, ri = 1.0, .93
    v = [bm.verts.new(P(ro, a - da, top)), bm.verts.new(P(ro, a + da, top)), bm.verts.new(P(ri, a + da, top)),
         bm.verts.new(P(ri, a - da, top))]
    tip_o, tip_i = bm.verts.new(P(ro * .985, a, top + h)), bm.verts.new(P(ri * .985, a, top + h))
    for f in ((v[0], v[1], tip_o), (v[2], v[3], tip_i), (v[1], v[2], tip_i, tip_o), (v[3], v[0], tip_o, tip_i)):
        bm.faces.new(f).material_index = gold_i
    ball = bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=BALL)
    x, y, z = P((ro + ri) / 2, a, top + h + BALL * .7)
    bmesh.ops.translate(bm, verts=ball["verts"], vec=(x, y, z))
    for f in {f for v_ in ball["verts"] for f in v_.link_faces}:
        f.material_index = gold_i
    gem = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=RUBY)
    bmesh.ops.scale(bm, vec=(1, 1, 1.3), verts=gem["verts"])
    x, y, z = P(1.06, a, .17)
    bmesh.ops.translate(bm, verts=gem["verts"], vec=(x, y, z))
    for f in {f for v_ in gem["verts"] for f in v_.link_faces}:
        f.material_index = ruby_i

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("Stage Crown")
bm.to_mesh(me)
bm.free()
me.materials.append(mat("Gold", (.86, .62, .22), 1.0, .25))
me.materials.append(mat("Ruby", (.55, .03, .08), 0.0, .1))
for f in me.polygons:
    f.use_smooth = True
o = bpy.data.objects.new("Stage_Crown", me)
bpy.context.scene.collection.objects.link(o)
tris = sum(len(f.vertices) - 2 for f in me.polygons)
print("CROWN TRIS", tris)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=False, object_types={"MESH"}, apply_scale_options="FBX_SCALE_UNITS",
                         mesh_smooth_type="FACE", add_leaf_bones=False)
print("WROTE", OUT)
