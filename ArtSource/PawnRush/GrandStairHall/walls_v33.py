# 승규 11-03: the trim map (white gold, black stone) is the design, but painting whole faces by it (walls_v32.py) left
# sawteeth wherever a big flat triangle half lay on a line. So the walls built from the architectural wall - the outer
# walls, the south walls, the foyer's sides and the round wall - get UVs straight onto the asset's front (u over its
# x -0.49 .. 0.49, v over its z 0 .. 0.59: every vertex where the module had it) and one material, Palace_Wall, whose
# shader (PR_PalaceWall.shader) reads PalaceTrim.png per pixel: the gold's edge is the map's. The round wall's
# vertices go back through the bend (round_hall_v22.py) to the asset's x and z. The window bars stay Palace_Gold, the
# archway, its bases and the corner columns Palace_Stone; no banners. From grand_stair_hall_v31C.blend (R335, no
# banners); the renders show the map's colours (textures/PalaceTrimPreview.png).
# Run: blender -b grand_stair_hall_v31C.blend -P walls_v33.py -> grand_stair_hall_v33C.blend, renders_v39/*.png
import bpy, math, os
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
HY, R0, FLOOR, TOP, W, H = 92.0, 31.0, -4.0, 26.0, 0.98, 0.59
ARCS = ((108.0, 234.0), (306.0, 432.0))
sc = bpy.context.scene
lo_m, hi_m = np.load(os.path.join(HERE, "textures", "WallModule_bounds.npy"))
STONE = bpy.data.materials["Palace_Stone"]

wall = bpy.data.materials.get("Palace_Wall") or bpy.data.materials.new("Palace_Wall")
wall.use_nodes = True
nt = wall.node_tree
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(os.path.join(HERE, "textures", "PalaceTrimPreview.png"), check_existing=True)
nt.links.new(tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
nt.nodes.active = tex
wall.diffuse_color = (.93, .87, .76, 1)


def uv_onto(me, asset_xz):
    """One material and UVs from asset_xz(vertex position) -> (asset x, asset z)."""
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    me.materials.clear(); me.materials.append(wall)
    per_vertex = [asset_xz(v.co) for v in me.vertices]
    for p in me.polygons:
        p.material_index = 0
    for loop, d in zip(me.loops, uv.data):
        ax, az = per_vertex[loop.vertex_index]
        d.uv = ((ax - lo_m[0]) / (hi_m[0] - lo_m[0]), (az - lo_m[2]) / (hi_m[2] - lo_m[2]))


done = set()
for name in ("Outer_wall_E_0", "Outer_wall_S_east", "Outer_wall_S_west", "Foyer_side_E", "Foyer_side_W"):
    me = bpy.data.objects[name].data
    if me.name in done:
        continue
    done.add(me.name)
    uv_onto(me, lambda co: (co.x, co.z))
    print("UV", name, "(%s, %d users)" % (me.name, me.users), len(me.polygons))

for ai, (a0, a1) in enumerate(ARCS):
    Lm = R0 * math.radians(a1 - a0) / 2
    sx = Lm / W
    for k in range(2):
        me = bpy.data.objects["Round_wall_%d_%d" % (ai, k)].data

        def back(co, a1=a1, k=k, Lm=Lm, sx=sx):
            phi = math.degrees(math.atan2(co.y - HY, co.x)) % 360
            while phi < a1 - 180:
                phi += 360
            s = math.radians(a1 - phi) * R0
            return lo_m[0] + (s - k * Lm) / sx, lo_m[2] + (co.z - FLOOR) * H / (TOP - FLOOR)

        uv_onto(me, back)
        us = [d.uv[0] for d in me.uv_layers[0].data]
        print("UV Round_wall_%d_%d" % (ai, k), len(me.polygons), "u %.3f .. %.3f" % (min(us), max(us)))

for name in ("Entrance_archway", "Entrance_base_E", "Entrance_base_W", "Corner_column_NE", "Corner_column_NW"):
    me = bpy.data.objects[name].data
    me.materials.clear(); me.materials.append(STONE)
    for p in me.polygons:
        p.material_index = 0

# ---- previews (textures as the map's colours)
sh = sc.display.shading
sh.color_type = "TEXTURE"
out = os.path.join(HERE, "renders_v39"); os.makedirs(out, exist_ok=True)
for name, loc, at, lens in (("wall_east", (8, 58, 12), (34, 74, 9), 26), ("wall_base", (24, 66, -1), (34, 78, 2), 24),
                            ("round_wall", (0, 100, 17), (-12, 120, 16), 22), ("foyer_out", (34, 6, 2), (21, 6, 3), 24)):
    cd = bpy.data.cameras.new(name); cd.lens = lens
    o = bpy.data.objects.new("Cam33_" + name, cd); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = o
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
sh.color_type = "MATERIAL"
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v33C.blend"))
