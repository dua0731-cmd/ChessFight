# 승규 11-03 (Tripo's preview of the knight: an ivory knight with gold crowns and bridle on a dark plinth with gold crown
# emblems and frames, on a cream round base with a gold rim): "직접 칠해줘". The download carries no UVs and no images
# (source/chess_knight.glb, POSITION and NORMAL only), so the knight is painted here by its own shape, at full detail
# (534k faces: 승규 "폴리곤 수 그대로", only scaled), in four materials Unity dresses (GrandStairHallModel.cs):
#   Knight_Ivory  the knight's head and neck                 Knight_Cream  the round base's two tiers
#   Knight_Dark   the plinth drum and the box behind it      Knight_Gold   relief and rims:
#     relief  where the surface stands proud of itself - each vertex against its own surface smoothed (Laplacian),
#             along its normal: fine (30 passes, over RELIEF) for the studs on the base and the bridle on the head's
#             front half; coarse (300 passes, over COARSE) for whole raised shapes on the plinth and the box - the
#             crown emblems, spirals and frames filled, not only outlined
#     rims    measured bands (승규 "테두리 부분 깔끔하게"): each base tier's top outer edge, the plinth drum's foot and head
#             mouldings whole, the box's frame along its edges
#     crowns  the two crowns, found in plan (one mesh with the head): the mane's crown round (-0.07, 0.05), the
#             head's between the ears round (0.16, 0.01), each above its foot - not the ears beside it
# Sized as R317's: the round base 1.8 m across, origin at its bottom centre, the head turned to the piece's +z.
# Run: blender -b -P knight_v34.py -- [render]   -> Assets/Resources/PawnRushGrandStairHall/FallingKnight.fbx, renders_v40
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
RENDER = "render" in sys.argv
BASE = 1.8
RELIEF, ITER = .0025, 30
COARSE, ITER_COARSE = .004, 300
# the crowns in plan (asset x, y), radius, foot z
CROWNS = ((-.07, .05, .078, .835), (.16, .01, .066, .87))
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
sc = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "chess_knight.glb"))
src = [o for o in bpy.data.objects if o.type == "MESH"][0]
me = src.data
me.transform(src.matrix_world); src.matrix_world = Matrix.Identity(4)

# ---- relief: each vertex against its smoothed self
n_v = len(me.vertices)
P = np.empty(n_v * 3); me.vertices.foreach_get("co", P); P = P.reshape(-1, 3)
N = np.empty(n_v * 3); me.vertices.foreach_get("normal", N); N = N.reshape(-1, 3)
E = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get("vertices", E); E = E.reshape(-1, 2)
deg = np.bincount(E.ravel(), minlength=n_v).astype(float)
S = P.copy()
for _ in range(ITER):
    acc = np.zeros_like(S)
    np.add.at(acc, E[:, 0], S[E[:, 1]]); np.add.at(acc, E[:, 1], S[E[:, 0]])
    S = .5 * S + .5 * acc / np.maximum(deg, 1)[:, None]
relief = np.einsum("ij,ij->i", P - S, N)
for _ in range(ITER_COARSE - ITER):
    acc = np.zeros_like(S)
    np.add.at(acc, E[:, 0], S[E[:, 1]]); np.add.at(acc, E[:, 1], S[E[:, 0]])
    S = .5 * S + .5 * acc / np.maximum(deg, 1)[:, None]
coarse = np.einsum("ij,ij->i", P - S, N)
print("RELIEF pct", np.percentile(relief, [5, 25, 50, 75, 90, 95, 98]).round(4))
print("COARSE pct", np.percentile(coarse, [5, 25, 50, 75, 90, 95, 98]).round(4))

# ---- faces into materials
mats = []
for name, rgb in (("Knight_Ivory", (.94, .9, .82)), ("Knight_Cream", (.88, .82, .7)), ("Knight_Dark", (.16, .15, .19)),
                  ("Knight_Gold", (.86, .64, .27))):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    mats.append(m)
IVORY, CREAM, DARK, GOLD = range(4)
me.materials.clear()
for m in mats:
    me.materials.append(m)
F = len(me.polygons)
C = np.empty(F * 3); me.polygons.foreach_get("center", C); C = C.reshape(-1, 3)
Nf = np.empty(F * 3); me.polygons.foreach_get("normal", Nf); Nf = Nf.reshape(-1, 3)
loop_start = np.empty(F, dtype=np.int64); me.polygons.foreach_get("loop_start", loop_start)
loop_total = np.empty(F, dtype=np.int64); me.polygons.foreach_get("loop_total", loop_total)
lv = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get("vertex_index", lv)
face_relief = np.add.reduceat(relief[lv], loop_start) / loop_total
face_coarse = np.add.reduceat(coarse[lv], loop_start) / loop_total

x, y, z = C[:, 0], C[:, 1], C[:, 2]
r = np.hypot(x, y)
# 승규 "테두리 부분 깔끔하게": the rims are measured bands now, not faces picked by their slope (which streaked).
# Measured on the asset (radius by height): the lower disc to z 0.115 (r 0.355, its plates out to 0.372), the upper
# tier to 0.205 (r 0.26), the plinth drum to about 0.45 (r 0.22) with a moulding at its foot (to 0.248) and its head
# (from 0.398) and its top at 0.452; the box behind it x -0.303 .. -0.18, y -0.166 .. 0.055, z 0.205 .. 0.56.
base = z < .205
box = (x < -.18) & (y > -.17) & (y < .06) & (z > .2) & (z < .565)
drum = (~base) & (~box) & (z < .452) & (r < .3)          # its top at 0.452: the neck above stays ivory
knight = ~(base | box | drum)
crown = np.zeros(F, bool)
for cx, cy, cr, cz in CROWNS:
    crown |= knight & (np.hypot(x - cx, y - cy) < cr) & (z > cz)
bridle = knight & (x > .02) & (z < .86) & (face_relief > RELIEF * .8)
base_rim = base & (((z > .098) & (r > .335)) | ((z > .188) & (z > .12) & (r > .25)))
base_plates = base & (((z < .098) & (r > .358)) | ((z > .12) & (z < .188) & (r > .263))) & (face_relief > RELIEF * .6)
drum_bands = drum & ((z < .248) | (z > .398))
W = .016
nx = ((x + .303) < W) | ((-.18 - x) < W)
ny = ((y + .166) < W) | ((.055 - y) < W)
box_frame = box & ((z > .51) | (z < .25) | (nx & ny))   # its cap, its foot and its four corners
emblems = (box | (drum & ~drum_bands)) & (face_coarse > COARSE)
idx = np.where(base, CREAM, np.where(box | drum, DARK, IVORY))
gold = base_rim | base_plates | drum_bands | box_frame | emblems | crown | bridle
idx = np.where(gold, GOLD, idx)
me.polygons.foreach_set("material_index", idx.astype(np.int32))
me.update()
for k, nm in enumerate(("ivory", "cream", "dark", "gold")):
    print("FACES", nm, int((idx == k).sum()))

# ---- size and turn as R317 (head +x -> +Y, then half round: Blender +Y arrives as Unity +z), origin at the base
lo, hi = P.min(0), P.max(0)
k = BASE / max(hi[0] - lo[0], hi[1] - lo[1])
me.transform(Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "Z") @ Matrix.Diagonal((k, k, k, 1))
             @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
for p in me.polygons:
    p.use_smooth = True
src.name = "Falling_knight"
print("KNIGHT", F, "faces (all kept), height %.2f m" % ((hi[2] - lo[2]) * k))

for o in sc.objects:
    o.select_set(False)
src.select_set(True); bpy.context.view_layer.objects.active = src
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "FallingKnight.fbx"), **FBX)
print("EXPORT FallingKnight.fbx")

if RENDER:
    sc.render.engine = "BLENDER_WORKBENCH"; sc.render.resolution_x = sc.render.resolution_y = 900
    sh = sc.display.shading; sh.light = "STUDIO"; sh.color_type = "MATERIAL"; sh.show_cavity = True
    out = os.path.join(HERE, "renders_v40"); os.makedirs(out, exist_ok=True)
    for n, loc in (("front", (0, 5.2, 1.6)), ("side", (5.2, 0, 1.6)), ("back", (-3.8, -3.8, 2.2))):
        cd = bpy.data.cameras.new(n); cd.lens = 50
        c = bpy.data.objects.new(n, cd); sc.collection.objects.link(c)
        c.location = loc
        c.rotation_euler = (Vector((0, 0, 1.15)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.camera = c
        sc.render.filepath = os.path.join(out, "knight_" + n + ".png")
        bpy.ops.render.render(write_still=True)
