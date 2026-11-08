# R481 (승규 11-06, the castle tower picture + "castle tower 3d model.glb": "기둥 쪽은 이렇게 만들고 에셋 교체 해서 자연스럽게
# 택스쳐 입혀 빨간색 깃발은 제거해"): the garden's castle towers become 승규's Tripo tower - a round tower with a corbelled
# crown, a slate cone with dormers and a gold finial, a square pillar with a gold urn of flowers on one side, a low wall
# stub on the other, vines and cone bushes at its foot. The GLB comes untextured (one grey material, no UVs), so this
# script cleans it and gives it the garden's own materials by where each face is:
#   flag          the red pennant beside the pole (z 0.885 .. 0.965, off the pole): deleted
#   gold          the finial and pole over the apex, the dormers standing out of the cone, the urn on the pillar
#   roof          the cone over the eave (z 0.615): the gate's blue
#   greenery      the vines, the bushes and the pillar's flowers: where the surface is rough (its normals turn a lot
#                 between neighbours, as leaves do; stone is flat between sharp edges)
#   flowers       the rough top of the urn (pink, white, yellow at random clumps)
#   stone         the rest: the castle walls' stone (GC | castle stone, the gate's picture)
# then recomputes its normals (the GLB had some inward), decimates it to about 24k faces and moves it so its tower's
# axis is at x = y = 0 (its base stays at z 0, the asset's units; walls_sample_v2.py scales and places it).
#
#   blender -b --python tower_asset.py -- "<castle tower 3d model.glb>"
# Out: TowerAsset_v1.blend (object "Tower asset"), render_tower_asset_*.png
import bpy, bmesh, sys, os, math, random
from collections import defaultdict
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
GLB = sys.argv[sys.argv.index("--") + 1]
AXIS_Y = .026                      # the tower's axis in the GLB (the cone's centre)
EAVE, APEX = .615, .885
FACES = 18000

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
src = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
src.name = "Tower asset"


def mat(name, rgb, rough, metal=0.):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    return m


M = {
    "stone": mat("GC | castle stone", (.88, .74, .62), .74),
    "roof": mat("GC | gate blue", (.03, .12, .55), .55),
    "gold": mat("GC | gate gold", (.95, .64, .08), .3, .75),
    "leaf": mat("GC | leaf", (.10, .32, .06), .7),
    "pink": mat("GC | flower pink", (.95, .35, .55), .6),
    "white": mat("GC | flower white", (.95, .93, .88), .6),
    "yellow": mat("GC | flower yellow", (.98, .78, .15), .6),
}
me = src.data
me.materials.clear()
for k in ("stone", "roof", "gold", "leaf", "pink", "white", "yellow"):
    me.materials.append(M[k])
IDX = {k: i for i, k in enumerate(("stone", "roof", "gold", "leaf", "pink", "white", "yellow"))}

bm = bmesh.new()
bm.from_mesh(me)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()

# The flag: off the pole near the top.
def off_pole(f):
    c = f.calc_center_median()
    return .896 <= c.z <= .958 and math.hypot(c.x, c.y - AXIS_Y) > .0105 and c.y > AXIS_Y   # the pole is r 0.008


flag = [f for f in bm.faces if off_pole(f)]
bmesh.ops.delete(bm, geom=flag, context="FACES")
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context="VERTS")
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()

# Roughness: how much the normals turn round each vertex, smoothed over its neighbours twice.
rough = {}
for v in bm.verts:
    if v.link_faces:
        rough[v] = sum(1 - max(-1., min(1., f.normal.dot(v.normal))) for f in v.link_faces) / len(v.link_faces)
    else:
        rough[v] = 0.
for _ in range(3):
    nxt = {}
    for v in bm.verts:
        nb = [e.other_vert(v) for e in v.link_edges]
        nxt[v] = (rough[v] + sum(rough[u] for u in nb)) / (1 + len(nb)) if nb else rough[v]
    rough = nxt

# The urn on the pillar is its own piece (the GLB's second part): find it.
comp, cid = {}, 0
for v in bm.verts:
    if v in comp:
        continue
    st = [v]
    comp[v] = cid
    while st:
        a = st.pop()
        for e in a.link_edges:
            b = e.other_vert(a)
            if b not in comp:
                comp[b] = cid
                st.append(b)
    cid += 1
size = defaultdict(int)
for v in comp.values():
    size[v] += 1
main = max(size, key=size.get)


def cone_r(z):
    return max(.012, .141 - .565 * (z - .64))


# R488 (승규's Unity picture of a bush half cream: "이런 부분 덜 칠해 졌어"): the two cone bushes at the tower's foot were
# painted inside a box (y -0.125 .. 0.17), which cut each through, so their outer halves stayed stone. They are cones,
# measured on the GLB (centre x, y, base radius, apex height): painted as those, a little fuller, off the shaft.
BUSHES = ((.0910, -.0990, .048, .127), (.0947, .1398, .060, .177))


def in_bush(c, r, rv):
    if r < .106 or c.x < .048:                       # the shaft's and the pillar's own faces stay stone
        return False
    for bx, by, rb, h in BUSHES:
        d = math.hypot(c.x - bx, c.y - by)
        # the cone with its leaves' tips (they stand out a third), and the tufts round its foot
        # (the flat stone of the plinth and wall foot under them stays: leaves are never that smooth)
        if (c.z < h + .012 and d < 1.35 * rb * max(0., 1 - c.z / (h + .01)) + .012 and rv > .008
                or c.z < .035 and d < 1.12 * rb + .006           # (its foot, whatever its shape)
                or c.z < .025 and d < 1.8 * rb and rv > .02):
            return True
    return False


rnd = random.Random(481)
clump = {}
counts = defaultdict(int)
for f in bm.faces:
    c = f.calc_center_median()
    r = math.hypot(c.x, c.y - AXIS_Y)
    rv = sum(rough[v] for v in f.verts) / len(f.verts)
    part = comp[f.verts[0]]
    dy = c.y - AXIS_Y
    shaft_front = math.sqrt(max(0., .104 ** 2 - dy * dy))
    if part != main:
        # the urn (gold) and the flowers heaped in it (in clumps of pink, white, yellow among leaves)
        if c.z >= .452:
            key = (round(c.x / .012), round(c.y / .012), round(c.z / .012))
            clump.setdefault(key, rnd.choice(("pink", "pink", "white", "yellow", "leaf", "leaf")))
            k = clump[key]
        else:
            k = "gold"
    elif c.z > APEX - .025:
        k = "gold"                                   # finial, pole
    elif c.z > EAVE and c.z < .725 and r > cone_r(c.z) + .008:
        k = "gold"                                   # dormers
    elif c.z > EAVE or (c.z > EAVE - .012 and r > .145):
        k = "roof"
    elif in_bush(c, r, rv):
        k = "leaf"                                   # the two cone bushes at the tower's foot (R488: as cones)
    elif .1 < c.z < .45 and -.23 < c.y < -.095 and (c.x > .053 or c.x > .0495 and rv > .025):
        k = "leaf"                                   # the vine down the pillar's front (its face at x 0.046), past its corner
    elif c.z < .02 and rv > .05:
        k = "leaf"                                   # grass tufts round the foot
    else:
        k = "stone"
    f.material_index = IDX[k]
    counts[k] += 1
print("SEGMENTS", dict(counts))

bm.to_mesh(me)
bm.free()
me.update()
# Decimate to the budget (materials kept), then centre the axis.
dec = src.modifiers.new("Decimate", "DECIMATE")
dec.ratio = min(1., FACES / max(1, len(me.polygons)))
bpy.context.view_layer.objects.active = src
bpy.ops.object.modifier_apply(modifier=dec.name)
for v in src.data.vertices:
    v.co.y -= AXIS_Y
src.data.update()
for p in src.data.polygons:
    p.use_smooth = False
print("FACES", len(src.data.polygons))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "TowerAsset_v1.blend"))

# Previews (front and three-quarter), in the materials' colours.
sc = bpy.context.scene
sc.render.resolution_x, sc.render.resolution_y = 700, 900
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (.5, .6, .75, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = .8
sc.world = w
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 4
sun.rotation_euler = (math.radians(45), 0, math.radians(35))
sc.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
for name, at in (("front", (2.2, 0, .55)), ("quarter", (1.6, 1.6, .7)), ("back", (-1.8, -1.0, .6))):
    cam.location = Vector(at)
    cam.rotation_euler = (Vector((0, 0, .47)) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    sc.render.filepath = os.path.join(HERE, "render_tower_asset_%s.png" % name)
    bpy.ops.render.render(write_still=True)
print("TOWER ASSET DONE")
