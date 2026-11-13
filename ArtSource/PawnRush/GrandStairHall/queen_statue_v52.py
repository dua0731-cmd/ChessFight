# 승규 11-05 (a shot of the central landing's grey king statue and a Tripo model, "chess queen statue 3d model.glb":
# "교체 해주고 발판 지우고 텍스쳐 입혀"): the queen in place of the landing's king statue. The model (source/
# queen_statue_tripo.glb, 0.98 high, no UVs or textures) is cut above its square pedestal (z 0.185: the plinth with the
# crown cartouche goes, the queen's own round foot stays), its bottom closed, decimated to about a quarter, stood with
# its foot at 0 and scaled to QUEEN_H, and split for Unity's materials by shape: Queen_Gold for the crown and orb (faces
# over CROWN_Z of the cut model's height) and the two crossed maces (their own pieces, wide in x), Queen_Marble for the
# rest. Its front (the old cartouche side, Blender -Y) comes into Unity as -z.
# Run: blender -b --factory-startup -P queen_statue_v52.py [-- render]
#   -> Assets/Resources/PawnRushGrandStairHall/QueenStatue.fbx (+ renders_v52/queen_front.png with render)
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source", "queen_statue_tripo.glb")
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
OUT = os.path.join(HERE, "renders_v52")
CUT_Z = .185              # the pedestal's top (measured off a front render with 5 cm ticks)
QUEEN_H = 6.0             # the king statue it replaces: 6.1 m with its cross
CROWN_Z = .80             # share of the cut height where the crown's band starts
DECIMATE = .25

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
o = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.context.view_layer.objects.active = o
o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = o.data

bm = bmesh.new()
bm.from_mesh(me)
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, CUT_Z), plane_no=(0, 0, 1), clear_inner=True)
cut_edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0)
# Stray bits of the pedestal left over the cut (loose pieces whose top is under the queen's foot ring): gone.
bm.verts.ensure_lookup_table()
seen, pieces = set(), []
for v in bm.verts:
    if v in seen: continue
    stack, ids = [v], []
    while stack:
        x = stack.pop()
        if x in seen: continue
        seen.add(x); ids.append(x)
        stack.extend(e.other_vert(x) for e in x.link_edges)
    pieces.append(ids)
gone = [p for p in pieces if max(v.co.z for v in p) < CUT_Z + .03]
bmesh.ops.delete(bm, geom=[v for p in gone for v in p], context="VERTS")
print("PIECES", len(pieces), "dropped", len(gone))
bm.to_mesh(me); bm.free()

mod = o.modifiers.new("dec", "DECIMATE"); mod.ratio = DECIMATE
bpy.ops.object.modifier_apply(modifier=mod.name)

# Foot at 0, centred on its own axis, QUEEN_H high.
vs = [v.co for v in me.vertices]
lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
s = QUEEN_H / (hi.z - lo.z)
for v in me.vertices:
    v.co = Vector(((v.co.x) * s, (v.co.y) * s, (v.co.z - lo.z) * s))

# Materials by shape.
for name in ("Queen_Marble", "Queen_Gold"):
    me.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
while len(me.materials) > 2: me.materials.pop(index=0)
bm = bmesh.new(); bm.from_mesh(me)
seen, gold_faces = set(), set()
for f in bm.faces:
    if f in seen: continue
    stack, part = [f], []
    while stack:
        x = stack.pop()
        if x in seen: continue
        seen.add(x); part.append(x)
        for e in x.edges:
            stack.extend(g for g in e.link_faces if g not in seen)
    xs = [v.co.x for g in part for v in g.verts]
    if max(xs) - min(xs) > .35 * QUEEN_H and len(part) < len(bm.faces) * .4:   # a mace: a long piece across
        gold_faces.update(part)
for f in bm.faces:
    f.material_index = 1 if (f in gold_faces or f.calc_center_median().z > CROWN_Z * QUEEN_H) else 0
print("FACES", len(bm.faces), "gold", sum(1 for f in bm.faces if f.material_index == 1))
bm.to_mesh(me); bm.free()
me.uv_layers.clear() if hasattr(me.uv_layers, "clear") else None
o.name = me.name = "QueenStatue"
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))

bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "QueenStatue.fbx"), use_selection=True, object_types={"MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                         bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False,
                         path_mode="STRIP", embed_textures=False)
print("EXPORT", os.path.join(RES, "QueenStatue.fbx"), len(me.polygons), "faces", "size", (hi - lo) * s)

if "render" in sys.argv:
    os.makedirs(OUT, exist_ok=True)
    m0, m1 = me.materials
    m0.diffuse_color = (.95, .94, .92, 1); m1.diffuse_color = (.86, .64, .28, 1)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"; sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_cavity = True
    sc.render.resolution_x, sc.render.resolution_y = 700, 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (4.5, -11, 4.2); d = Vector((0, 0, 3)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler(); cam.data.lens = 50
    sc.render.filepath = os.path.join(OUT, "queen_front.png")
    bpy.ops.render.render(write_still=True)
