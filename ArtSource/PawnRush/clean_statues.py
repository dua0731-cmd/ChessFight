"""Giant chess statues for the towers by the Pawn Rush start bridge (승규 10-22, Tripo GLBs: king, queen, bishop,
knight; ~300k triangles each, no UVs, no textures).

blender --background --factory-startup --python clean_statues.py -- <raw_dir> <out_dir>
Each: decimated to ~45k triangles, smooth by angle, origin at the centre of the pedestal's foot, scaled so the
pedestal's footprint is FOOT metres (it covers a kit tower's 4.3 m top and buries its corner merlons), smart-UV
unwrapped, the crown (and the queen's crossed sceptres) on a "Gold" material, the rest "Statue_Stone".
Front looks along -Y (along +Z in Unity after import). Writes PR_Statue_<Piece>.fbx and a preview sheet.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)
FOOT = 4.0
TARGET_TRIS = 45000
# gold: everything above this height (fraction of the statue's height), plus the queen's sceptres
# 10-22 (승규 "돌 동상같이, 금색 빼고"): all stone now; a fraction above 1 puts nothing in gold
GOLD_ABOVE = {"King": 9.0, "Queen": 9.0, "Bishop": 9.0, "Knight": 9.0}


def material(name, color, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = (*color, 1)
    bs.inputs["Metallic"].default_value = metal
    bs.inputs["Roughness"].default_value = rough
    return m


def clean(piece, src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    o = [x for x in bpy.data.objects if x.type == "MESH"][0]
    for x in list(bpy.data.objects):
        if x != o:
            bpy.data.objects.remove(x)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    dec = o.modifiers.new("dec", "DECIMATE")
    dec.ratio = min(1.0, TARGET_TRIS / tris)
    bpy.ops.object.modifier_apply(modifier="dec")

    # the pedestal's foot: the vertices in the lowest 2 % of the height
    vs = [v.co.copy() for v in o.data.vertices]
    zmin = min(v.z for v in vs)
    zmax = max(v.z for v in vs)
    foot = [v for v in vs if v.z < zmin + 0.02 * (zmax - zmin)]
    fx0, fx1 = min(v.x for v in foot), max(v.x for v in foot)
    fy0, fy1 = min(v.y for v in foot), max(v.y for v in foot)
    centre = Vector(((fx0 + fx1) / 2, (fy0 + fy1) / 2, zmin))
    s = FOOT / max(fx1 - fx0, fy1 - fy0)
    for v in o.data.vertices:
        v.co = (v.co - centre) * s
    height = (zmax - zmin) * s

    # 10-22 (승규 "받침대 없애줘"): cut the pedestal off. Its top is where the model's width, slice by slice
    # (1 % of the height each), first falls under 80 % of the foot's; everything below goes, the piece's
    # open underside sits flat on the tower, and the origin moves to the centre of the piece's own foot.
    def width(z0, z1):
        sl = [v.co for v in o.data.vertices if z0 <= v.co.z < z1]
        if not sl:
            return 0.0, None
        x0, x1 = min(v.x for v in sl), max(v.x for v in sl)
        y0, y1 = min(v.y for v in sl), max(v.y for v in sl)
        return max(x1 - x0, y1 - y0), Vector(((x0 + x1) / 2, (y0 + y1) / 2, 0))
    step = 0.01 * height
    cut = None
    z = 0.02 * height
    while z < 0.5 * height:
        w, _ = width(z, z + step)
        if 0 < w < (0.6 if piece == "Knight" else 0.8) * FOOT:   # the knight's pedestal steps in once more
            cut = z + step * 0.5
            break
        z += step
    if cut is not None:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        doomed = [f for f in bm.faces if f.calc_center_median().z < cut]
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bm.to_mesh(o.data)
        bm.free()
        zlow = min(v.co.z for v in o.data.vertices)
        _, c = width(zlow, zlow + 0.04 * height)
        for v in o.data.vertices:
            v.co -= Vector((c.x, c.y, zlow))
        height -= zlow
        bw, _ = width(0, 0.04 * height)
        print(f"CUT {piece} pedestal {cut:.2f} m, piece foot {bw:.2f} m")

    # materials
    stone = material("Statue_Stone", (0.9, 0.85, 0.74), 0.0, 0.6)
    gold = material("Gold", (0.93, 0.7, 0.2), 1.0, 0.28)
    o.data.materials.clear()
    o.data.materials.append(stone)
    o.data.materials.append(gold)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    above = GOLD_ABOVE[piece] * height
    n_gold = 0
    for f in bm.faces:
        c = f.calc_center_median()
        g = c.z > above
        if False and piece == "Queen" and abs(c.x) > 0.21 * FOOT and c.z > 0.25 * height:
            g = True          # the crossed sceptres reach out past the body
        f.material_index = 1 if g else 0
        n_gold += g
    bm.to_mesh(o.data)
    bm.free()

    # UVs for the stone grain
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    o.name = o.data.name = "PR_Statue_" + piece
    out = os.path.join(OUT, o.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE")
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    print(f"STATUE {piece} tris={tris} height={height:.2f} foot={(fx1 - fx0) * s:.2f}x{(fy1 - fy0) * s:.2f} gold_faces={n_gold}")


def sheet():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    for i, p in enumerate(("King", "Queen", "Bishop", "Knight")):
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, "PR_Statue_" + p + ".fbx"))
        o = bpy.context.selected_objects[0]
        o.location.x = i * 7.0
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1800, 700
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.68, 0.95, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    sc.world = w
    sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 30
    cam.location = (10.5, -30, 6)
    cam.rotation_euler = (math.radians(88), 0, 0)
    sc.render.filepath = os.path.join(OUT, "statues_sheet.png")
    bpy.ops.render.render(write_still=True)


for piece in ("King", "Queen", "Bishop", "Knight"):
    clean(piece, os.path.join(RAW, piece.lower() + ".glb"))
sheet()
print("DONE")
