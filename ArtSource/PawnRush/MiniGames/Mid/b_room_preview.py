# B East Wing whole-room preview (R503): every exported model placed as MiniGameModels hangs it, with the game's
# floors as stand-ins, rendered to render_b_room_*.png - to check that walls and railings run on without gaps.
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python b_room_preview.py
import bpy, math, os, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
EXPORT = os.path.join(HERE, "Export")
TEX = os.path.join(HERE, "Materials", "Textures")
EARLY = os.path.join(HERE, "..", "Early", "Materials", "Textures")


def textured(mat):
    """Re-link the material to its PNG (the FBX strips paths)."""
    name = mat.name.split(".")[0]
    path = os.path.join(TEX, name + "_BaseColor.png")
    if name == "PR_Mid_AshlarFill": path = os.path.join(EARLY, "PR_Early_StoneBrick_BaseColor.png")
    mat.use_nodes = True; nt = mat.node_tree
    b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is None: return
    if name == "PR_Mid_WindowGlass":
        b.inputs["Base Color"].default_value = (1, .86, .62, 1)
        b.inputs["Emission Color"].default_value = (1, .8, .5, 1); b.inputs["Emission Strength"].default_value = 1.2
        return
    if not os.path.exists(path): return
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(path, check_existing=True)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])


def load(name, M):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(EXPORT, name + ".fbx"))
    for o in set(bpy.data.objects) - before:
        if o.parent is None: o.matrix_world = M @ o.matrix_world
        if o.type == 'MESH':
            for m in o.data.materials:
                if m and not m.get("done"): textured(m); m["done"] = 1


bpy.ops.wm.read_factory_settings(use_empty=True)
I = Matrix.Identity(4)
for n in ("B_ShellEnv", "B_UpperEnv", "B_HallEnv"):
    load(n, I)
load("B_CenterGate", Matrix.Translation((0, 23.69, 0)))
for x in (-12., 12.):
    load("B_TeamArch", Matrix.Translation((x, 23.8, 0)))
for side in (-1., 1.):
    for i, z in enumerate((2., 8., 14., 20., 33.)):
        load("B_WallArch", Matrix.Translation((side * (22.2 + .01 * (i % 2)), z, 0)) @ Matrix.Rotation(math.radians(90. if side < 0 else -90.), 4, 'Z') @ Matrix.Diagonal((1.03, 1, 1, 1)))
for x in (-12., 12.):
    load("B_StationEnv", Matrix.Translation((x, 4., 0)))
# game floors (stand-ins): entry band, rear corridor, aisle, station floors either side of the chasms
slate = bpy.data.materials.new("slate"); slate.use_nodes = True
slate.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.13, .17, .22, 1)
for x0, x1, z0, z1 in ((-22, 22, 0, 4), (-22, 22, 30, 36), (-2, 2, 4, 24), (-22, -2, 4, 10), (2, 22, 4, 10), (-22, -2, 20, 24), (2, 22, 20, 24)):
    bpy.ops.mesh.primitive_cube_add(location=((x0 + x1) / 2, (z0 + z1) / 2, -.5), scale=((x1 - x0) / 2, (z1 - z0) / 2, .5))
    bpy.context.object.data.materials.append(slate)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
sc.view_settings.view_transform = 'AgX'
w = bpy.data.worlds.new("w"); w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (.35, .3, .26, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .5
sc.world = w
for x, z in ((-12, 2), (12, 2), (-12, 21), (12, 21), (0, 12)):
    bpy.ops.object.light_add(type='POINT', location=(x, z, 9.5)); bpy.context.object.data.energy = 9000
    bpy.context.object.data.color = (1, .8, .6)
cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
views = {"entry": ((0, 1.5, 2.2), (0, 24, 6), 18), "side": ((-8, 4, 3), (22, 14, 6), 20),
         "north": ((0, 12, 3), (0, 24, 6), 18), "rear": ((-10, 31, 2), (10, 35, 4), 18), "back": ((0, 34, 2.5), (0, -1, 6), 18),
         "railtop": ((-19.5, 10, 9.5), (-21, 14, 5.3), 22), "railside": ((-14, 6, 6.2), (-20.8, 14, 5.8), 26),
         "aisle": ((0, 9, 9), (0, 14.01, 0), 24), "roof": ((30, -25, 40), (0, 18, 6), 28)}
ONLY = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for name, (loc, tgt, lens) in views.items():
    if ONLY and name not in ONLY: continue
    cam.location = loc; cd.lens = lens
    cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(HERE, "render_b_room_%s.png" % name)
    bpy.ops.render.render(write_still=True)
