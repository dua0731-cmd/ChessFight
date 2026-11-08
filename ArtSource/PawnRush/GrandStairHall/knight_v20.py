# 승규 11-03: the "10 Falling piece" obstacles (the orange-topped kit pawn) drawn as 승규's chess knight
# (source/chess_knight.glb, 534k faces, a knight bust on an octagonal plinth on a round base, head toward asset +x).
# Decimated to about 20k faces, its round base 1.8 m across (the kit pawn's foot), so about 2.4 m high, the origin at
# the base's bottom centre and the head turned to the piece's +z. It falls (Unity moves it), so it goes out alone and
# Unity hangs it on the piece's "Falling pawn"; the kit pawn's colliders stay. No paint: Arch_Clay.
# Run: blender -b grand_stair_hall_v20.blend -P knight_v20.py   -> FallingKnight.fbx (the .blend is not saved)
import bpy, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TARGET = r"C:\Work\ChessFight-SeoungTest\Assets\Resources\PawnRushGrandStairHall"
CLAY = bpy.data.materials["Arch_Clay"]
BASE = 1.8
sc = bpy.context.scene
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)

before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", "chess_knight.glb"))
new = [o for o in bpy.data.objects if o not in before]
src = [o for o in new if o.type == "MESH"][0]
me = src.data.copy(); me.transform(src.matrix_world)
for o in new:
    bpy.data.objects.remove(o)
while me.uv_layers:
    me.uv_layers.remove(me.uv_layers[0])
me.materials.clear(); me.materials.append(CLAY)
o = bpy.data.objects.new("Falling_knight", me); sc.collection.objects.link(o)
bpy.context.view_layer.objects.active = o
m = o.modifiers.new("d", "DECIMATE"); m.ratio = .04
bpy.ops.object.modifier_apply(modifier=m.name)

vs = [v.co for v in me.vertices]
lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
k = BASE / max(hi.x - lo.x, hi.y - lo.y)
# Head +x -> +Y (Blender), then half round as the other loose models (pads_v18.export): Blender +Y arrives as Unity +z.
me.transform(Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "Z") @ Matrix.Diagonal((k, k, k, 1))
             @ Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z)))
for p in me.polygons:
    p.use_smooth = False
for s in sc.objects:
    s.select_set(False)
o.select_set(True); bpy.context.view_layer.objects.active = o
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
bpy.ops.export_scene.fbx(filepath=os.path.join(TARGET, "FallingKnight.fbx"), **FBX)
print("KNIGHT", len(me.polygons), "faces, height", round((hi.z - lo.z) * k, 2), "m")
