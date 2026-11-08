# R502: one chain link for the drawbridge's chains (user 11-07, with Source/R502_ChunkyChain.fbx: "쇠사슬을 굵게 해서
# 이거 작동 시켜"). The user's chain is a wall plate and four links welded into one lumpy mesh, so no single link can be
# cut out of it cleanly (tried: the cut link came out in pieces). This makes a link in its look instead: a chunky
# stadium-shaped ring with a fat, slightly squared bar and a lumpy forged surface, 1 unit long (Unity sets the size).
# Unity lays these from each pylon's top to the deck's far corner every frame (MiniGameDrawbridgeChain).
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_chain_link.py
# Writes Export/C_ChainLink.fbx (long axis along Blender y, ring in the x-y plane; material PR_Early_DarkIron) and
# chain_link_check.png (the link next to the user's chain).
import os, math
import bpy, bmesh
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R502_ChunkyChain.fbx")
OUT = os.path.join(HERE, "Export", "C_ChainLink.fbx")

LENGTH, WIDTH, BAR = 1.0, .62, .14      # outer length and width, bar radius (the user's links: about 1 : 0.6 : 0.15)
RING, TUBE = 48, 10

bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()
straight = (LENGTH - WIDTH) / 2          # half the straight side
r_mid = WIDTH / 2 - BAR                  # centre line radius of the round ends
perim = 4 * straight + 2 * math.pi * r_mid


def centre_line(s):
    """Point and tangent on the stadium centre line at arc length s (0..perim), ring in x-y, long along y."""
    s %= perim
    segs = [2 * straight, math.pi * r_mid, 2 * straight, math.pi * r_mid]
    if s < segs[0]:
        return Vector((r_mid, -straight + s, 0)), Vector((0, 1, 0))
    s -= segs[0]
    if s < segs[1]:
        a = s / r_mid
        return Vector((r_mid * math.cos(a), straight + r_mid * math.sin(a), 0)), Vector((-math.sin(a), math.cos(a), 0))
    s -= segs[1]
    if s < segs[2]:
        return Vector((-r_mid, straight - s, 0)), Vector((0, -1, 0))
    s -= segs[2]
    a = math.pi + s / r_mid
    return Vector((r_mid * math.cos(a), -straight + r_mid * math.sin(a), 0)), Vector((-math.sin(a), math.cos(a), 0))


rows = []
for i in range(RING):
    p, t = centre_line(perim * i / RING)
    out = Vector((t.y, -t.x, 0))                 # in-plane normal (outward)
    up = Vector((0, 0, 1))
    row = []
    for j in range(TUBE):
        a = 2 * math.pi * j / TUBE
        # a squarish bar section (superellipse), as the user's forged links
        ca, sa = math.cos(a), math.sin(a)
        k = 2.6
        rr = BAR / ((abs(ca) ** k + abs(sa) ** k) ** (1 / k))
        q = p + out * (ca * rr) + up * (sa * rr * .92)
        q += (q * 3.1 + Vector((3, 1, 7))).normalized() * 0 + Vector((noise.noise(q * 6.0), noise.noise(q * 6.0 + Vector((5, 5, 5))), noise.noise(q * 6.0 + Vector((9, 2, 4))))) * .018
        row.append(bm.verts.new(q))
    rows.append(row)
for i in range(RING):
    for j in range(TUBE):
        a, b = rows[i][j], rows[(i + 1) % RING][j]
        c, d = rows[(i + 1) % RING][(j + 1) % TUBE], rows[i][(j + 1) % TUBE]
        bm.faces.new((a, b, c, d))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
me = bpy.data.meshes.new("C_ChainLink")
bm.to_mesh(me)
bm.free()
me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
iron = bpy.data.materials.new("PR_Early_DarkIron")
iron.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.12, .11, .10, 1)
iron.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = .6
me.materials.append(iron)
link = bpy.data.objects.new("C_ChainLink", me)
bpy.context.scene.collection.objects.link(link)
sub = link.modifiers.new("sub", "SUBSURF")
sub.levels = sub.render_levels = 1
me.calc_loop_triangles()
print("LINK triangles (before subdivision)", len(me.loop_triangles))
for o in bpy.context.scene.objects:
    o.select_set(o is link)
bpy.context.view_layer.objects.active = link
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                         use_mesh_modifiers=True, mesh_smooth_type="OFF", add_leaf_bones=False, path_mode="STRIP",
                         embed_textures=False)
print("wrote", OUT)

# check: four links in a chain (every other one turned 90 degrees) beside the user's chain
pitch = LENGTH - 2 * BAR * 1.05
for i in range(1, 4):
    o = link.copy()
    bpy.context.scene.collection.objects.link(o)
    o.location = (0, i * pitch, 0)
    o.rotation_euler = (0, math.pi / 2 if i % 2 else 0, 0)
bpy.ops.import_scene.fbx(filepath=SRC)
user = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name.startswith("tripo")][0]
user.location = (1.6, 0, 0)
user.scale = (2, 2, 2)
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.2
sc.world = w
sun = bpy.data.lights.new("s", "SUN")
sun.energy = 3
so = bpy.data.objects.new("s", sun)
so.rotation_euler = (math.radians(50), 0, math.radians(30))
sc.collection.objects.link(so)
cam = bpy.data.cameras.new("c")
co = bpy.data.objects.new("c", cam)
sc.collection.objects.link(co)
sc.camera = co
co.location = (3.2, -3.0, 3.0)
co.rotation_euler = (Vector((1.0, 1.2, 0)) - co.location).to_track_quat("-Z", "Y").to_euler()
sc.render.resolution_x, sc.render.resolution_y = 900, 600
sc.render.filepath = os.path.join(HERE, "chain_link_check.png")
bpy.ops.render.render(write_still=True)
