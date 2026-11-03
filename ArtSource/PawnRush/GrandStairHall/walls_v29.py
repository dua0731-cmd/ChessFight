# 승규 11-03 (a reference palace wall: cream ashlar, gold trim at the cap, the base and the pilasters' edges, a crimson
# banner with a gold crown): "이런 텍스쳐로 벽 텍스쳐 입혀". The hall's walls - the outer walls (v17), the foyer's sides
# (v20), the round wall (v22), the entrance archway and its bases (v19), the corner columns (v16) - get two materials
# by face, which Unity dresses (Course01v4Builder.GrandStairHallModel.cs):
#   Palace_Stone  the faces looking out of the wall, and everything not trim: a cream limestone drawn in world space by
#                 PR_PalaceStone.shader (the walls have no UVs; the asset's own blocks give the courses)
#   Palace_Gold   the trim: the lowest 3.5 % and the top 6 % of each wall, every level face (sills, ledges, the
#                 cornice, the capitals' tops), the faces looking along the wall (the pilasters' and frames' sides,
#                 the window reveals); the window bars (tracery) whole
# The library's bookshelf walls beside the stair keep their clay. A crimson banner with a gold crown
# (banner_texture_v29.py) hangs from a gold rod in each blind-arch bay: the outer walls' four (y 31.6 / 71.4, in front
# of the pilasters at x +-34, z 6.5 .. 16.5) and the round wall's four (phi 40.5, 139.5, 202.5, 337.5, r 30.6, z 13.5
# .. 22: over the curving stair's head room - it passes under the one at 337.5 at y 8 .. 10 + a 2.4 m jump).
# Run: blender -b grand_stair_hall_v28C.blend -P walls_v29.py -> grand_stair_hall_v29C.blend, renders_v36/*.png
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
HY, NOTCH = 92.0, .12
sc = bpy.context.scene


def mat(name, rgb, image=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    if image:
        m.use_nodes = True
        nt = m.node_tree
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(RES, image), check_existing=True)
        nt.links.new(tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
        nt.nodes.active = tex
    return m


STONE, GOLD = mat("Palace_Stone", (.93, .87, .76)), mat("Palace_Gold", (.86, .64, .27))
BANNER = mat("Palace_Banner", (.62, .11, .16), "PalaceBanner.png")

MODULES = ["Outer_wall_E_0", "Outer_wall_S_east", "Outer_wall_S_west", "Foyer_side_E", "Foyer_side_W"]
ROUND = ["Round_wall_0_0", "Round_wall_0_1", "Round_wall_1_0", "Round_wall_1_1"]
BANDED = ["Entrance_archway", "Entrance_base_E", "Entrance_base_W", "Corner_column_NE", "Corner_column_NW"]


def classify(me, front_of, low=.035, high=.94):
    """Slot 0 stone, 1 gold, by the face's height in the mesh and its normal against the wall's front there."""
    zs = [v.co.z for v in me.vertices]
    z0, z1 = min(zs), max(zs)
    me.materials.clear(); me.materials.append(STONE); me.materials.append(GOLD)
    gold = 0
    for p in me.polygons:
        t = (p.center.z - z0) / max(z1 - z0, 1e-6)
        n = p.normal
        front = front_of(p.center)
        if front is None:
            trim = t < low or t > high or abs(n.z) > .75
        else:
            along = Vector((-front.y, front.x, 0))
            trim = t < low or t > high or abs(n.z) > .75 or (n.dot(front) < .55 and abs(n.dot(along)) > .8)
        p.material_index = 1 if trim else 0
        gold += trim
    return gold, len(me.polygons)


done = set()
for name in MODULES:            # the architectural wall: its front is the asset's -y, in the mesh's own frame
    o = bpy.data.objects[name]
    if o.data.name in done:
        continue
    done.add(o.data.name)
    g, n = classify(o.data, lambda c: Vector((0, -1, 0)))
    print("WALL", name, "(mesh %s, %d users)" % (o.data.name, o.data.users), "gold %d / %d faces" % (g, n))
for name in ROUND:              # baked round the ring: its front looks at the hub
    o = bpy.data.objects[name]
    g, n = classify(o.data, lambda c: -Vector((c.x, c.y - HY, 0)).normalized())
    print("WALL", name, "gold %d / %d faces" % (g, n))
for name in BANDED:             # no single front: bands and level faces only
    o = bpy.data.objects[name]
    g, n = classify(o.data, lambda c: None, .05, .9)
    print("WALL", name, "gold %d / %d faces" % (g, n))
tracery = [o for o in bpy.data.objects if o.type == "MESH" and o.name.endswith(" tracery")
           and o.name.split(" ")[0] in [m.replace("Outer_wall_E_0", "Outer_wall") for m in []] + []]
for o in bpy.data.objects:
    if o.type == "MESH" and o.name.endswith(" tracery") and o.users_collection[0].name in (
            "Outer_walls_v17", "Foyer_sides_v20", "Round_hall_v22C"):
        o.data.materials.clear(); o.data.materials.append(GOLD)
        for p in o.data.polygons:
            p.material_index = 0
print("TRACERY gold")

# ---------------------------------------------------------------- the banners
col = bpy.data.collections.new("Palace_banners_v29"); sc.collection.children.link(col)


def banner(name, centre, facing, width, z0, z1):
    """A swallowtail banner hanging in front of a wall, a gentle ripple across it, its UVs straight on; a rod above."""
    right = Vector((facing.y, -facing.x, 0)).normalized()     # across the banner, seen from the hall
    bm = bmesh.new(); uv = bm.loops.layers.uv.new("UVMap")
    nu, nv = 12, 24
    grid = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            u, v = i / nu, j / nv
            lo = NOTCH * (1 - abs(2 * u - 1))                      # the swallowtail: the middle cut up
            vv = lo + (1 - lo) * v
            ripple = .10 * math.sin(u * math.pi * 3) * (1 - .6 * vv)
            p = centre + right * (u - .5) * width + Vector((0, 0, z0 + (z1 - z0) * vv)) + facing * (ripple + .05)
            row.append((bm.verts.new(p), (u, vv)))
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            q = [grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]]   # wound to face the hall
            f = bm.faces.new([a for a, _ in q])
            for loop, (_, t) in zip(f.loops, q):
                loop[uv].uv = t
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(BANNER)
    col.objects.link(bpy.data.objects.new(name, me))
    # the rod and its two finials
    bm = bmesh.new()
    rod = Matrix.Translation(centre + Vector((0, 0, z1 + .12)) + facing * .08) @ Vector((1, 0, 0)).rotation_difference(right).to_matrix().to_4x4()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=.09, radius2=.09, depth=width + .7,
                          matrix=rod @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    for s in (-1, 1):
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=.2,
                                  matrix=Matrix.Translation(centre + Vector((0, 0, z1 + .12)) + facing * .08 + right * s * (width / 2 + .4)))
    me = bpy.data.meshes.new(name + " rod"); bm.to_mesh(me); bm.free()
    me.materials.append(GOLD)
    col.objects.link(bpy.data.objects.new(name + " rod", me))


for side, x in ((1, 34.0), (-1, -34.0)):
    for y in (31.6, 71.4):
        banner("Palace_banner_%s_%d" % ("E" if side > 0 else "W", y), Vector((x - side * .25, y, 0)), Vector((-side, 0, 0)), 4.2, 6.5, 16.5)
for phi in (40.5, 139.5, 202.5, 337.5):
    a = math.radians(phi)
    out = Vector((math.cos(a), math.sin(a), 0))
    banner("Palace_banner_ring_%d" % round(phi), Vector((0, HY, 0)) + out * 30.6, -out, 3.6, 13.5, 22.0)
print("BANNERS", len([o for o in col.objects if not o.name.endswith(" rod")]))

# ---------------------------------------------------------------- previews
def camera(name, loc, at, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens
    o = bpy.data.objects.new(name, cd); col.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


out = os.path.join(HERE, "renders_v36"); os.makedirs(out, exist_ok=True)
sh = sc.display.shading
sh.color_type = "TEXTURE"
for c in (camera("Cam_wall_east", (8, 58, 12), (34, 74, 9), 26), camera("Cam_round_wall", (0, 100, 17), (-12, 120, 16), 22),
          camera("Cam_stair_up2", (0, 6, 6.5), (0, 40, 5), 20)):
    sc.camera = c
    sc.render.filepath = os.path.join(out, c.name[4:] + ".png")
    bpy.ops.render.render(write_still=True)
sh.color_type = "MATERIAL"
sc.camera = bpy.data.objects.get("Cam_overview") or sc.camera
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v29C.blend"))
