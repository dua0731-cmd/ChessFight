# R502: the user's Royal Castle Gate (Source/R502_RoyalCastleGate.fbx, a 500k-triangle gatehouse with a crowned
# central door and an arched passage each side) has its two side passages only as shallow dark recesses.
# The user: "이 에셋 양 통로가 막혀 있어 뚫어줘" - cut both through, front to back, so the teams can walk through.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_gate_open.py
#
# 1. finds each side arch by casting rays at the front face (the recess is where the hits are deep),
# 2. cuts an arch-shaped tunnel (a box up to the springing line plus a half cylinder) through the whole building,
# 3. the tunnel's new walls, vault and floor get their own material slot "PR_Early_TowerStone" with box-projected UVs
#    on the same UV map, so in Unity they take the tower stone tile while the rest keeps the asset's own texture.
# Writes Source/R502_RoyalCastleGate_Open.blend and gate_open_front.png / gate_open_back.png / gate_open_through.png.
# Frame as imported: X across the front, front face toward -Y, Z up, about 1 x 1 x 0.53 (the game's scale is set where
# the gate is placed, not here).
import os, sys, math
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R502_RoyalCastleGate.fbx")
OUT = os.path.join(HERE, "Source", "R502_RoyalCastleGate_Open.blend")
sys.path.insert(0, os.path.join(HERE, "Materials"))
import r502_tower_block as tb  # noqa: E402
import json  # noqa: E402

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
gate = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
gate.name = "PR_Early_RoyalGate"
bpy.context.view_layer.objects.active = gate
gate.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = gate.data
vs = [v.co for v in me.vertices]
lo = Vector([min(v[i] for v in vs) for i in range(3)])
hi = Vector([max(v[i] for v in vs) for i in range(3)])
print("GATE bounds", tuple(round(x, 4) for x in lo), tuple(round(x, 4) for x in hi), "tris", sum(len(p.vertices) - 2 for p in me.polygons))

# ---- 1. measure the two side recesses
dg = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(gate, dg)


def depth(x, z):
    loc, n, i, d = bvh.ray_cast(Vector((x, lo.y - 1, z)), Vector((0, 1, 0)))
    return None if loc is None else loc.y - lo.y


face = sorted(d for d in (depth(x, z) for x in [lo.x + .1 + .02 * k for k in range(40)] for z in [lo.z + .05 + .01 * k for k in range(30)]) if d is not None)
front = face[len(face) // 4]
print("front face depth (25th pct) %.4f" % front)
arches = []
for side in (-1, 1):
    xs = [side * (.2 + .0025 * k) for k in range(100)]           # 0.2 .. 0.45 out from the centre
    deep = []
    for x in xs:
        for k in range(160):
            z = lo.z + .005 + k * .002
            d = depth(x, z)
            if d is not None and d > front + .04:
                deep.append((x, z))
    x0, x1 = min(p[0] for p in deep), max(p[0] for p in deep)
    zb, zt = min(p[1] for p in deep), max(p[1] for p in deep)
    arches.append((x0, x1, zb, zt))
    print("ARCH side %d: x %.4f..%.4f  z %.4f..%.4f  (points %d)" % (side, x0, x1, zb, zt, len(deep)))

# ---- 2. the cutters: an arch through the whole depth
info = json.load(open(os.path.join(HERE, "Materials", "Textures", "PR_Early_TowerStone.json")))
liner = tb.stone_material(info)
liner.name = "PR_Early_TowerStone"
uv_name = me.uv_layers[0].name
cutters = []
for x0, x1, zb, zt in arches:
    inset = .004                       # stay just inside the arch's stone ring
    x0, x1, zt = x0 + inset, x1 - inset, zt - inset
    r = (x1 - x0) / 2
    cx = (x0 + x1) / 2
    spring = zt - r
    prof = [(x0, zb - .002), (x1, zb - .002)] + \
           [(cx + r * math.cos(math.radians(a)), spring + r * math.sin(math.radians(a))) for a in range(0, 181, 6)]
    prof = prof[:2] + prof[2:]
    bm = bmesh.new()
    y0, y1 = lo.y - .05, hi.y + .05
    fr = [bm.verts.new((px, y0, pz)) for px, pz in prof]
    bk = [bm.verts.new((px, y1, pz)) for px, pz in prof]
    n = len(prof)
    bm.faces.new(fr[::-1])
    bm.faces.new(bk)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((fr[i], fr[j], bk[j], bk[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    uv = bm.loops.layers.uv.new(uv_name)
    for f in bm.faces:
        ax = max(range(3), key=lambda i: abs(f.normal[i]))
        for l in f.loops:
            c = l.vert.co
            u, v = [(c.y, c.z), (c.x, c.z), (c.x, c.y)][ax]
            l[uv].uv = (u * 4., v * 4.)        # about the tower stone tile's density at the game scale
    cm = bpy.data.meshes.new("cutter")
    bm.to_mesh(cm)
    bm.free()
    cm.materials.append(liner)
    co = bpy.data.objects.new("cutter", cm)
    bpy.context.scene.collection.objects.link(co)
    co.hide_render = True
    cutters.append(co)

# ---- 3. cut
me.materials.append(liner)
for co in cutters:
    mod = gate.modifiers.new("open", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = co
    mod.solver = "EXACT"
    mod.material_mode = "TRANSFER"
    bpy.context.view_layer.objects.active = gate
    bpy.ops.object.modifier_apply(modifier=mod.name)
    co.hide_set(True)
print("CUT tris", sum(len(p.vertices) - 2 for p in gate.data.polygons))
bpy.ops.wm.save_as_mainfile(filepath=OUT)

# ---- check renders: front, back, and a low view straight through a passage
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 48
sc.cycles.use_denoising = True
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (.55, .75, .95, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.
sc.world = w
sun = bpy.data.lights.new("s", "SUN")
sun.energy = 3.
so = bpy.data.objects.new("s", sun)
so.rotation_euler = (math.radians(50), 0, math.radians(25))
sc.collection.objects.link(so)
cam = bpy.data.cameras.new("c")
cobj = bpy.data.objects.new("c", cam)
sc.collection.objects.link(cobj)
sc.camera = cobj
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
for name, loc, target, ortho in (("front", (0, -3, (lo.z + hi.z) / 2), (0, 0, (lo.z + hi.z) / 2), True),
                                 ("back", (0, 3, (lo.z + hi.z) / 2), (0, 0, (lo.z + hi.z) / 2), True),
                                 ("through", ((arches[0][0] + arches[0][1]) / 2, lo.y - .5, arches[0][2] + .05),
                                  ((arches[0][0] + arches[0][1]) / 2, hi.y, arches[0][2] + .05), False)):
    cam.type = "ORTHO" if ortho else "PERSP"
    cam.ortho_scale = 1.1
    cam.lens = 35
    cobj.location = loc
    cobj.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(HERE, "gate_open_" + name + ".png")
    bpy.ops.render.render(write_still=True)
