# R308: the v15 hall's new pieces to the seoungtest Unity (zone H), over the blockout the code still builds.
#   GrandStairHallV15.fbx         every visible piece the blockout does not have (v2 smooth bands, v9 arch pass, v10
#                                 hanging chandeliers at rest, v11 stairs, v12 marble floors and the lowered floors under
#                                 them, v13 statues, v15 library walls), world frame, joined per object group
#   GrandStairHallV15_Hidden.txt  the Unity paths (under Content) of the blockout pieces they replace: Unity stops
#                                 drawing those and keeps their colliders
#   FallingChandelier.fbx         one v10 chandelier's 129 parts (origin = its bottom centre) and its chain (origin =
#                                 its foot) for ChandelierRain
# Axes as the garden's export: Unity puts the model under a 180-degree pivot. No UVs, no textures: the clay is Unity's.
# Run: blender -b grand_stair_hall_v20.blend -P export_unity_v15.py   (v16: corner columns; v17: the outer walls in the architectural wall, windows open; v18: wider red floor, pads out on their own - pads_v18.py; v19: the entrance in the castle archway, its windows open - archway_v19.py; v20: the foyer side walls in the architectural wall - foyer_sides_v20.py)
import bpy, bmesh, os, re
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TARGET = r"C:\Work\ChessFight-SeoungTest\Assets\Resources\PawnRushGrandStairHall"
os.makedirs(TARGET, exist_ok=True)
sc = bpy.context.scene
sc.frame_set(sc.frame_start)            # the chandeliers hanging still
dg = bpy.context.evaluated_depsgraph_get()
retired = set(bpy.data.collections["_Blockout originals"].all_objects)
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)


def world_mesh(o, offset=Vector()):
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
    me.transform(Matrix.Translation(-offset) @ o.matrix_world)
    if o.matrix_world.determinant() < 0:
        me.flip_normals()
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    return me


def joined(name, objs, offset=Vector()):
    """One object from several, their materials merged by name."""
    mats, bm = [], bmesh.new()
    for o in objs:
        me = world_mesh(o, offset)
        remap = []
        for m in me.materials:
            if m not in mats:
                mats.append(m)
            remap.append(mats.index(m))
        for p in me.polygons:
            p.material_index = remap[p.material_index] if remap else 0
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    sc.collection.objects.link(ob)
    return ob


def clean(name):
    return re.sub(r"[^A-Za-z0-9_()\- ]", "_", name.replace("/", "__"))


# ------------------------------------------------------------------ the hall
new = [o for o in sc.objects if o.type == "MESH" and o not in retired and o.visible_get()
       and (not o.get("unity_path") or o.name.endswith("(under marble)"))]
groups = {}
for o in new:
    m = re.match(r"(Chandelier_\d+)/", o.name)
    key = m.group(1) if m else o.name
    groups.setdefault(key, []).append(o)
out = [joined("HallV15_" + clean(k), objs) for k, objs in sorted(groups.items())]
hidden = sorted({o["unity_path"] for o in retired if o.get("unity_path")})
for o in sc.objects:
    o.select_set(False)
for o in out:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(TARGET, "GrandStairHallV15.fbx"), **FBX)
with open(os.path.join(TARGET, "GrandStairHallV15_Hidden.txt"), "w", encoding="utf8") as f:
    f.write("\n".join(hidden) + "\n")
print("HALL", len(out), "nodes,", sum(len(o.data.polygons) for o in out), "faces,", len(hidden), "blockout pieces hidden")
for o in out:
    bpy.data.objects.remove(o)

# ------------------------------------------------------------------ one falling chandelier
parts = sorted([o for o in sc.objects if o.name.startswith("Chandelier_1/Part_")], key=lambda o: o.name)
chain = [o for o in sc.objects if o.name.startswith("Chandelier_1/Chain_")]
def bounds(objs):
    vs = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)])
lo, hi = bounds(parts)
base = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
pieces = [joined("Part_%03d" % i, [o], base) for i, o in enumerate(parts)]
for p in pieces:
    # Each part's origin at its own centre, so it tumbles about itself and flies out from where it was.
    vs = [v.co for v in p.data.vertices]
    mid = sum(vs, Vector()) / max(len(vs), 1)
    p.data.transform(Matrix.Translation(-mid))
    p.location = mid
clo, chi = bounds(chain)
links = joined("Chain", chain, Vector((base.x, base.y, clo.z)))
for o in sc.objects:
    o.select_set(False)
for o in pieces + [links]:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(TARGET, "FallingChandelier.fbx"), **FBX)
print("CHANDELIER", len(pieces), "parts, size", [round(v, 2) for v in (hi - lo)], "chain", round(chi.z - clo.z, 2),
      "gap to chain", round(clo.z - hi.z, 2))
