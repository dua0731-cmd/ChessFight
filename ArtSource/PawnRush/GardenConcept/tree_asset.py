# R491 (승규 11-06, two Tripo trees - "stylized tree 3d model.glb" (a cone of a crown) and "(1)" (a round crown) - and
# their painted picture: brown trunks with moss on the roots, bright green leaf crowns, pink and white flowers with
# yellow hearts; "나무를 배치 할 건데 길 쪽에 배치 하지 말고 길이 아닌 곳에 ... 너무 일렬로 배치 하지 마", the places:
# the reservoir terraces and the waterside banks). The GLBs come untextured (one grey material, no UVs, ~250k faces
# each), so this script cleans each, decimates it and paints it by where each face is:
#   bark        the trunk and the branches under and into the crown (inside a radius that widens up the fork)
#   moss        the roots' tops at the foot (GC | moss band, as the lock's)
#   leaves      the crown: GC | leaf, its sunlit tops GC | leaf light
#   flowers     the GLB's own little flowers are too small to find, so flowers are made on the crown's outside (rays
#               from round it onto the leaves): five petals, pink or white, a yellow heart, as the picture's
# Each tree is centred on its trunk's foot (x = y = 0, z 0), in the GLB's units (about 1 high); trees_sample.py
# scales, turns and places copies.
#
#   blender -b --python tree_asset.py            (source/tree_cone.glb, source/tree_round.glb)
# Out: TreeAsset_v1.blend (objects "Tree cone asset", "Tree round asset"), render_tree_asset_*.png
import bpy, bmesh, os, math, random
from collections import defaultdict
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
# name, GLB, faces, trunk top, fork (top, radius at the trunk top, widening), moss height, crown centre z, flowers,
# flower diameter
TREES = (
    ("Tree cone asset", "tree_cone.glb", 14000, .19, (.28, .055, 3.4), .035, .55, 11, .066),
    ("Tree round asset", "tree_round.glb", 16000, .30, (.40, .085, .6), .05, .62, 15, .078),
)

bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name, rgb, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1)
    return m


# Placeholders by the garden's names (trees_sample.py swaps in its own materials of these names); preview colours.
KEYS = ("bark", "moss", "leaf", "leaf2", "pink", "white", "heart")
M = {
    "bark": mat("GC | bark", (.36, .17, .06), .8),
    "moss": mat("GC | moss band", (.25, .40, .10), .75),
    "leaf": mat("GC | leaf", (.07, .34, .02), .7),
    "leaf2": mat("GC | leaf light", (.18, .50, .04), .7),
    "pink": mat("GC | flower pink", (1.0, .19, .41), .6),
    "white": mat("GC | flower white", (.96, .94, .86), .6),
    "heart": mat("GC | flower heart", (1.0, .50, .01), .55),
}
IDX = {k: i for i, k in enumerate(KEYS)}


def flower(bm, at, n, size, petal_mat, rnd):
    """Five round petals cupped a little round a heart, facing n, `size` across."""
    n = n.normalized()
    u = n.orthogonal().normalized()
    u.rotate(Matrix.Rotation(rnd.uniform(0, 2 * math.pi), 3, n))
    v = n.cross(u)
    centre = at + n * size * .08
    L, W = size * .46, size * .40
    for k in range(5):
        a = 2 * math.pi * k / 5
        d = u * math.cos(a) + v * math.sin(a)
        s = d.cross(n)
        ring = []
        for j in range(10):
            t = 2 * math.pi * j / 10
            along, across = (1 - math.cos(t)) * .5 * L, math.sin(t) * W * .5
            lift = (along / L) ** 2 * size * .12          # cupped: the tips rise
            ring.append(bm.verts.new(centre + d * (along + size * .04) + s * across + n * lift))
        f = bm.faces.new(ring)
        f.material_index = IDX[petal_mat]
        f.smooth = True
    heart = [bm.verts.new(centre + n * size * .05 + (u * math.cos(2 * math.pi * j / 8) + v * math.sin(2 * math.pi * j / 8))
                          * size * .13) for j in range(8)]
    top = bm.verts.new(centre + n * size * .1)
    for j in range(8):
        f = bm.faces.new((heart[j], heart[(j + 1) % 8], top))
        f.material_index = IDX["heart"]
        f.smooth = True


made = []
for name, glb, faces, trunk_top, (fork_top, fork_r, widen), moss_z, crown_z, n_flowers, fsize in TREES:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", glb))
    ob = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
    ob.name = name
    me = ob.data
    me.transform(ob.matrix_world)
    ob.matrix_world.identity()
    me.materials.clear()
    for k in KEYS:
        me.materials.append(M[k])
    bm = bmesh.new()
    bm.from_mesh(me)
    # the foot's middle: the trunk's centre near the ground
    foot = [v.co for v in bm.verts if v.co.z < .06]
    fx = sorted(c.x for c in foot)[len(foot) // 2]
    fy = sorted(c.y for c in foot)[len(foot) // 2]
    bmesh.ops.translate(bm, verts=bm.verts[:], vec=(-fx, -fy, 0))
    # specks (a few 3-vertex crumbs) out, normals out
    seen, crumbs = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        part, st = [v], [v]
        seen.add(v)
        while st:
            a = st.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if b not in seen:
                    seen.add(b)
                    st.append(b)
                    part.append(b)
        if len(part) < 50:
            crumbs += part
    bmesh.ops.delete(bm, geom=crumbs, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    dec = ob.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = min(1., faces / max(1, len(me.polygons)))
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=dec.name)

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    counts = defaultdict(int)
    for f in bm.faces:
        c, n = f.calc_center_median(), f.normal
        r = math.hypot(c.x, c.y)
        if c.z < trunk_top or (c.z < fork_top and r < fork_r + (c.z - trunk_top) * widen and n.z > -.6):
            k = "moss" if c.z < moss_z and n.z > .25 else "bark"
        else:
            k = "leaf2" if n.z > .45 else "leaf"
        f.material_index = IDX[k]
        f.smooth = True
        counts[k] += 1
    # flowers on the crown's outside
    bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    rnd = random.Random(489 + len(made))
    spots = []
    tries = 0
    while len(spots) < n_flowers and tries < 2000:
        tries += 1
        az, el = rnd.uniform(0, 2 * math.pi), math.radians(rnd.uniform(-15, 70))
        d = Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
        origin = Vector((0, 0, crown_z)) + d * 1.5
        hit, nrm, fi, _ = tree.ray_cast(origin, -d)
        if hit is None or bm.faces[fi].material_index not in (IDX["leaf"], IDX["leaf2"]):
            continue
        if any((hit - p).length < fsize * 1.8 for p, _ in spots):
            continue
        spots.append((hit, nrm))
    for i, (p, n) in enumerate(spots):
        flower(bm, p, (n + (p - Vector((0, 0, crown_z))).normalized()) * .5, fsize * rnd.uniform(.85, 1.15),
               "pink" if rnd.random() < .6 else "white", rnd)
    counts["flowers"] = len(spots)
    bm.to_mesh(me)
    bm.free()
    me.update()
    me.set_sharp_from_angle(angle=math.radians(50))
    print("TREE", name, len(me.polygons), "faces", dict(counts))
    made.append(ob)

for i, ob in enumerate(made):
    ob.location.x = i * 1.2            # side by side in the file (trees_sample.py uses the meshes, not these places)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "TreeAsset_v1.blend"))

# Previews in the materials' colours.
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 1400, 800
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (.85, .82, .8, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = .9
sc.world = w
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(45), 0, math.radians(-35))
sc.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(cam)
sc.camera = cam
for view, at in (("front", (.6, -3.0, .6)), ("high", (2.2, -2.2, 1.6))):
    cam.location = Vector(at)
    cam.rotation_euler = (Vector((.6, 0, .45)) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    sc.render.filepath = os.path.join(HERE, "render_tree_asset_%s.png" % view)
    bpy.ops.render.render(write_still=True)
print("TREE ASSET DONE")
