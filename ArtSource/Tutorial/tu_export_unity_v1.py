# Tutorial map -> Unity (R834): TU_Map_v1.blend's "Map" collection as two FBX files and the textures they use.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b TU_Map_v1.blend --python tu_export_unity_v1.py
#
# Assets/Art/Tutorial/Models/TU_Map.fbx      three objects, each one joined mesh (one draw per material):
#                                            TU_Course (floors, trims, moat walls, every dressed obstacle),
#                                            TU_Castle (rim, rails, castle walls, towers, gate, statues, greens),
#                                            TU_Grounds (the grass outside the walls)
# Assets/Art/Tutorial/Models/TU_Sweeper.fbx  the spinning bar's look, origin at the hub's foot (child of Spinning Bar)
# Assets/Art/Tutorial/Textures/*.png         TU_* maps and the three Tripo kits' BaseColor / Normal
#
# Both files land in Unity at the game's own coordinates with no extra rotation: Blender (x, y, z) is Unity (x, z, y),
# and the FBX axis change (forward -Z, up Y) plus Unity's x flip turn Blender (x, y, z) into Unity (-x, z, -y), so the
# meshes are turned 180 degrees about z first. Materials keep their Blender names; the Unity builder
# (RagdollLabBuilder.TutorialDress) makes the .mat files and remaps them. No colliders: the game's boxes stay.
import bpy, os, shutil, math
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(bpy.data.filepath)) if bpy.data.filepath else os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "Art", "Tutorial", "Models")
TEXTURES = os.path.join(ROOT, "Assets", "Art", "Tutorial", "Textures")
TEX_SRC = os.path.join(HERE, "Materials", "Textures")

COURSE = ("TU_Floor", "TU_FloorTrim", "TU_MoatWalls", "TU_TrainingWall", "TU_Hurdle", "TU_LimboStation", "TU_Tunnel_at",
          "TU_BarPadInlay", "TU_Beam_at", "TU_FinishArch_at", "TU_Climb", "TU_Platform", "TU_Slope")
TURN = Matrix.Rotation(math.pi, 4, 'Z')


def baked_copy(o):
    me = o.data.copy()
    me.transform(o.matrix_world)
    if o.matrix_world.determinant() < 0: me.flip_normals()
    while len(me.uv_layers) > 1: me.uv_layers.remove(me.uv_layers[-1])
    if me.uv_layers: me.uv_layers[0].name = "UVMap"
    c = bpy.data.objects.new(o.name + "_bake", me)
    bpy.context.scene.collection.objects.link(c)
    return c


def joined(objs, name):
    copies = [baked_copy(o) for o in objs]
    bpy.ops.object.select_all(action='DESELECT')
    for c in copies: c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    with bpy.context.temp_override(active_object=copies[0], selected_editable_objects=copies, selected_objects=copies):
        bpy.ops.object.join()
    o = copies[0]; o.name = o.data.name = name
    o.data.transform(TURN)
    return o


def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
                             mesh_smooth_type='OFF', use_mesh_modifiers=True, path_mode='STRIP', add_leaf_bones=False)
    print("EXPORT", path, [(o.name, len(o.data.polygons)) for o in objs])


def main():
    os.makedirs(MODELS, exist_ok=True); os.makedirs(TEXTURES, exist_ok=True)
    m = bpy.data.collections["Map"]
    objs = [o for o in m.objects if o.type == 'MESH']
    sweep = [o for o in objs if o.name.startswith("TU_Sweeper")]
    trees = [o for o in objs if o.name.startswith(("TU_TreeA_at", "TU_TreeB_at", "TU_TreeC_at", "TU_Bush_at"))]
    objs = [o for o in objs if o not in trees]
    grass = [o for o in objs if o.name.startswith(("TU_Grass", "TU_Hills", "TU_Road", "TU_WallFoundation", "TU_Rocks"))]
    course = [o for o in objs if o not in sweep and o not in grass and o.name.startswith(COURSE)]
    castle = [o for o in objs if o not in sweep and o not in grass and o not in course]
    print("GROUPS course", len(course), "castle", len(castle), "sweeper", len(sweep))
    a = joined(course, "TU_Course"); b = joined(castle, "TU_Castle"); g = joined(grass, "TU_Grounds")
    export([a, b, g], os.path.join(MODELS, "TU_Map.fbx"))
    s = baked_copy(sweep[0])
    loc = sweep[0].matrix_world.translation.copy()
    s.data.transform(Matrix.Translation(-loc)); s.data.transform(TURN); s.name = s.data.name = "TU_SweeperLook"
    export([s], os.path.join(MODELS, "TU_Sweeper.fbx"))
    # grown trees: one FBX per kind, its _LOD0 and _LOD1 as they are (Unity makes the LOD group from the names; the
    # builder turns each instance 180 degrees more for the axis change); their places as a CSV
    for v in ("TreeA", "TreeB", "TreeC", "Bush"):
        lods = [bpy.data.objects["TU_%s_LOD%d" % (v, k)] for k in (0, 1)]
        for lo in lods: lo.location = (0, 0, 0); lo.hide_set(False)
        export(lods, os.path.join(MODELS, "TU_%s.fbx" % v))
    src = os.path.join(HERE, "tu_tree_placements.json")
    if os.path.exists(src):
        import json
        rows = json.load(open(src))["trees"]
        NL = chr(10)
        with open(os.path.join(MODELS, "TU_TreePlacements.txt"), "w", newline=NL) as fh:
            fh.write("# variant,x,y(up),z,yaw(Blender, degrees),scale - Unity coordinates (Blender x, z, y)" + NL)
            for v, x, y, z, yaw, sc in rows:
                fh.write("%s,%.3f,%.3f,%.3f,%.1f,%.3f" % (v, x, z, y, yaw, sc) + NL)
        print("TREES", len(rows))
    for extra in ("TU_GrassBlades_BaseColor.png", "TU_Grass_Normal.png", "TU_MeadowMacro.png", "TU_SkyClouds.exr"):   # Unity-only maps
        shutil.copy2(os.path.join(TEX_SRC, extra), os.path.join(TEXTURES, extra))
    tf = os.path.join(HERE, "tu_grass_tufts.txt")
    if os.path.exists(tf):
        shutil.copy2(tf, os.path.join(MODELS, "TU_GrassTufts.txt")); print("TUFTS copied")
    # textures every exported material uses
    used = set()
    tree_objs = [bpy.data.objects["TU_%s_LOD0" % v] for v in ("TreeA", "TreeB", "TreeC", "Bush")]
    for o in [a, b, g, s] + tree_objs:
        for mat in o.data.materials:
            if mat and mat.use_nodes:
                for n in mat.node_tree.nodes:
                    if n.type == "TEX_IMAGE" and n.image and "_ORM" not in n.image.filepath: used.add(os.path.basename(n.image.filepath))
    for fn in sorted(used):
        src = os.path.join(TEX_SRC, fn)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(TEXTURES, fn)); print("TEX", fn)
        else:
            print("TEX-ELSEWHERE", fn)
    print("MATERIALS", sorted({mt.name for o in (a, b, g, s) for mt in o.data.materials if mt}))


main()
