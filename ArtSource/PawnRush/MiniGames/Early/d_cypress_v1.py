# D cypress (R505): the concept sheet's tall Italian cypress in a cream stone planter, built in Blender (user, 11-07:
# "나무는 너가 블렌더해서 디테일 하게 해"). No Tripo source.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python d_cypress_v1.py -- [--render] [--export]
#
# Shape: a flame-shaped column (widest ~28 % up, rounded at the bottom, a pointed top) made of a dark closed core and
# ~150 leaf tufts on a golden-angle spiral, each tuft a teardrop leaning up and out (the stylised scale look of the
# concept). Colour is a painted ramp (Materials/Textures/PR_Early_Cypress_BaseColor.png): every vertex's u is how lit it
# is (up- and outward-facing, higher, the tuft's own top) and v is one of seven hue rows, so tufts read as soft clumps
# with dark gaps between them. The bottom row of the ramp is the bark. The planter wears the tinted R502 stone brick
# (PR_Early_WallCore, box-mapped in metres) and flat soil (PR_Early_Soil).
# Origin: the planter's floor centre; Blender -y faces the players. About 1.24 m wide, 4.6 m high.
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mg_holes

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
rng = random.Random(505)

# Planter (metres): plinth, body, a rim frame round the soil.
PLINTH, BODY, RIM, RIM_IN = .60, .55, .62, .45
PLINTH_H, BODY_Z, RIM_Z = .10, (.08, .66), (.62, .74)
SOIL_Z = .70
# Tree.
FOL_Z0, FOL_TOP, RMAX, WIDEST = 1.0, 4.55, .53, .26
TUFTS = 150
ROWS = 8                       # ramp rows: 0..6 leaf hues, 7 bark
RAMP = [(0.00, (.07, .15, .09)), (.30, (.13, .27, .13)), (.55, (.22, .40, .17)),
        (.78, (.36, .54, .22)), (.92, (.52, .65, .28)), (1.0, (.66, .74, .36))]
BARK = [(0.0, (.17, .11, .07)), (1.0, (.46, .33, .21))]


def radius(t):
    """Foliage radius at height fraction t (0 bottom, 1 tip)."""
    if t <= 0 or t >= 1: return 0.0
    if t < WIDEST: return RMAX * math.sqrt(max(0.0, 1 - ((WIDEST - t) / WIDEST) ** 2))
    return RMAX * (1 - (t - WIDEST) / (1 - WIDEST)) ** .9


def ramp_image():
    """256 x 256 painted ramp: u = light, rows of 32 px; leaf rows differ a little in hue, the last row is bark."""
    os.makedirs(TEX, exist_ok=True)
    n = 256; u = np.linspace(0, 1, n)
    def interp(stops):
        xs = [s[0] for s in stops]
        return np.stack([np.interp(u, xs, [s[1][c] for s in stops]) for c in range(3)], -1)
    leaf, bark = interp(RAMP), interp(BARK)
    img = np.zeros((n, n, 3), np.float32)
    shift = [(1, 1, 1), (.95, 1.0, .92), (1.04, 1.02, .9), (.92, .98, 1.06), (1.0, .96, .9), (1.06, 1.04, .96), (.96, 1.02, 1.0)]
    r = np.random.default_rng(5051)
    for row in range(ROWS):
        base = bark if row == ROWS - 1 else leaf * np.array(shift[row])
        band = np.repeat(base[None], n // ROWS, 0)
        band = band * (1 + .025 * r.uniform(-1, 1, band.shape[:2]))[..., None]   # a faint painted grain
        img[row * n // ROWS:(row + 1) * n // ROWS] = band
    img = np.clip(img, 0, 1)
    def save(name, rgb, linear):
        im = bpy.data.images.new(name, n, n)
        im.colorspace_settings.name = 'Non-Color' if linear else 'sRGB'
        im.pixels.foreach_set(np.dstack([rgb, np.ones((n, n))]).astype(np.float32)[::-1].ravel())
        im.filepath_raw = os.path.join(TEX, name + ".png"); im.file_format = 'PNG'; im.save()
        return im
    base = save("PR_Early_Cypress_BaseColor", img, False)
    save("PR_Early_Cypress_Normal", np.dstack([np.full((n, n), .5), np.full((n, n), .5), np.ones((n, n))]), True)
    return base


def row_v(row):
    """v of a ramp row's middle (image rows run top to bottom; Blender v runs bottom to top)."""
    return 1 - (row + .5) / ROWS


def materials(img):
    leaf = bpy.data.materials.new("PR_Early_Cypress"); leaf.use_nodes = True
    nt = leaf.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.interpolation = 'Linear'
    nt.links.new(t.outputs[0], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = .88
    stone = mg_holes.stone_brick("PR_Early_WallCore", TEX, (.81, .78, .73))
    soil = bpy.data.materials.new("PR_Early_Soil"); soil.use_nodes = True
    soil.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.10, .06, .04, 1)
    return leaf, stone, soil


def box(bm, lo, hi, slot, uvl, bevel=.012):
    before = set(bm.faces)
    g = bmesh.ops.create_cube(bm, size=1.0)
    vs = g["verts"]
    for v in vs:
        v.co = Vector([(lo[i] + hi[i]) / 2 + v.co[i] * (hi[i] - lo[i]) for i in range(3)])
    edges = list({e for v in vs for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=2, affect='EDGES', profile=.5)
    faces = set(bm.faces) - before
    for f in faces:
        f.material_index = slot; f.normal_update()
        a, b, c = (abs(x) for x in f.normal)
        for l in f.loops:
            co = l.vert.co
            u, w = (co.x, co.y) if c >= max(a, b) else ((co.y, co.z) if a >= b else (co.x, co.z))
            l[uvl].uv = (u / 2.0 + .3, w / 2.0 + .1)      # the brick tile is 2 m; offset off a joint


def planter(bm, uvl, s_stone, s_soil):
    box(bm, (-PLINTH, -PLINTH, 0), (PLINTH, PLINTH, PLINTH_H), s_stone, uvl, .015)
    box(bm, (-BODY, -BODY, BODY_Z[0]), (BODY, BODY, BODY_Z[1]), s_stone, uvl, .01)
    # rim frame: front/back full width, sides between them (their touching ends are inside the frame)
    for sy in (-1, 1):
        y0, y1 = sorted((sy * RIM_IN, sy * RIM))
        box(bm, (-RIM, y0, RIM_Z[0]), (RIM, y1, RIM_Z[1]), s_stone, uvl, .012)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * RIM_IN, sx * RIM))
        box(bm, (x0, -RIM_IN + .001, RIM_Z[0] + .002), (x1, RIM_IN - .001, RIM_Z[1] - .002), s_stone, uvl, .012)
    # soil: a low dome inside the rim, above the body's top
    n = 8; grid = []
    for i in range(n + 1):
        row = []
        for j in range(n + 1):
            x = -RIM_IN + .005 + (2 * RIM_IN - .01) * i / n; y = -RIM_IN + .005 + (2 * RIM_IN - .01) * j / n
            d = max(abs(x), abs(y)) / RIM_IN
            row.append(bm.verts.new((x, y, SOIL_Z + .035 * (1 - d * d) + .006 * noise.noise(Vector((x * 9, y * 9, 0))))))
        grid.append(row)
    for i in range(n):
        for j in range(n):
            f = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
            f.material_index = s_soil
            for l in f.loops: l[uvl].uv = (.5, .5)


def light(n, out, h, local_up, rand):
    """How lit a tuft vertex is (ramp u): faces up or out, sits high, is the tuft's top; dark underneath."""
    L = .30 + .36 * n.z + .22 * n.dot(out) + .12 * (h - .5) + .34 * local_up + rand
    return min(.97, max(.03, L))


def tree(bm, uvl, slot):
    # trunk from the soil into the foliage
    seg = 9; z0, z1 = SOIL_Z - .02, FOL_Z0 + .55
    rings = []
    for k, z in enumerate((z0, (z0 + z1) / 2, z1)):
        r = (.105, .085, .07)[k]
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            f = bm.faces.new((a[i], a[j], b[j], b[i])); f.material_index = slot; f.smooth = True
            f.normal_update()
            for l in f.loops: l[uvl].uv = (.25 + .5 * max(0, l.vert.normal.dot(Vector((.5, -.7, .5)).normalized())), row_v(ROWS - 1))
    # dark closed core: tufts sit on it, so the gaps between them show deep green, never the sky
    H = FOL_TOP - FOL_Z0
    ts = np.linspace(0, 1, 30); seg = 18
    rings = []
    for t in ts[1:-1]:
        r = .80 * radius(t)
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg), r * math.sin(2 * math.pi * i / seg), FOL_Z0 + t * H)) for i in range(seg)])
    bot = bm.verts.new((0, 0, FOL_Z0)); top = bm.verts.new((0, 0, FOL_TOP - .1))
    core = []
    for a, b in zip(rings, rings[1:]):
        for i in range(seg):
            j = (i + 1) % seg; core.append(bm.faces.new((a[i], a[j], b[j], b[i])))
    for i in range(seg):
        j = (i + 1) % seg
        core.append(bm.faces.new((bot, rings[0][j], rings[0][i])))
        core.append(bm.faces.new((rings[-1][i], rings[-1][j], top)))
    for f in core:
        f.material_index = slot; f.smooth = True
        for l in f.loops: l[uvl].uv = (.06 + .1 * (l.vert.co.z - FOL_Z0) / H, row_v(0))
    # tufts: area-even heights (inverse of the cumulative side area), golden-angle turns
    fine = np.linspace(0, 1, 2001)
    area = np.cumsum([radius(t) for t in fine]); area /= area[-1]
    golden = math.pi * (3 - math.sqrt(5))
    spots = [(float(np.interp((i + .5) / TUFTS, area, fine)), i * golden + rng.uniform(-.25, .25)) for i in range(TUFTS)]
    spots.append((.975, 0.0))                                   # the pointed top
    for i, (t, ang) in enumerate(spots):
        tip = i == len(spots) - 1
        R = radius(t)
        s = .15 if tip else (.11 + .13 * math.sqrt(R / RMAX)) * rng.uniform(.85, 1.15)
        out = Vector((math.cos(ang), math.sin(ang), 0))
        centre = out * (.86 * R) + Vector((0, 0, FOL_Z0 + t * H))
        lean = 0.0 if tip else math.radians(rng.uniform(18, 32))
        up = (Vector((0, 0, 1)) * math.cos(lean) + out * math.sin(lean)).normalized()
        side = up.cross(out).normalized() if not tip else Vector((1, 0, 0))
        radial = side.cross(up).normalized()
        stretch, flat = (1.9, 1.0) if tip else (1.3, .8)
        g = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
        vs = g["verts"]
        seed = Vector((i * 1.7, i * .3, 0))
        for v in vs:
            p = v.co.copy()
            pu = p.z
            taper = 1 - .38 * max(0.0, pu) ** 1.5                   # a soft teardrop: rounder, a gentle point
            q = Vector((p.x * taper, p.y * taper * flat, pu * stretch))
            q *= 1 + .16 * noise.noise(p * 2.1 + seed) + .05 * noise.noise(p * 6.0 + seed)
            v.co = centre + (side * q.x + radial * q.y + up * q.z) * s
        faces = list({f for v in vs for f in v.link_faces})
        rand = rng.uniform(-.08, .08); row = rng.randrange(ROWS - 1)
        for f in faces:
            f.material_index = slot; f.smooth = True; f.normal_update()
        for v in vs: v.normal_update()
        for f in faces:
            for l in f.loops:
                v = l.vert
                local_up = (v.co - centre).dot(up) / (s * stretch)
                l[uvl].uv = (light(v.normal, out, t, local_up, rand), row_v(row))


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    img = ramp_image()
    leaf, stone, soil = materials(img)
    me = bpy.data.meshes.new("D_Cypress")
    for m in (leaf, stone, soil): me.materials.append(m)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("map1")
    planter(bm, uvl, 1, 2)
    tree(bm, uvl, 0)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("D_Cypress", me); bpy.context.scene.collection.objects.link(ob)
    return ob


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1000, 1300
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.55, .7, .9, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.5; bpy.context.object.data.angle = math.radians(6)
    def proxy(name, loc, size, rgba):
        m = bpy.data.materials.new(name); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = rgba
        bpy.ops.mesh.primitive_cube_add(location=loc, scale=size); bpy.context.object.data.materials.append(m)
    proxy("floor", (0, 0, -.05), (6, 6, .05), (.62, .56, .47, 1))
    proxy("wall", (0, 1.6, 1.625), (5, .3, 1.625), (.80, .74, .63, 1))
    m = bpy.data.materials.new("pawn"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.95, .93, .9, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(1.4, -.8, .31)); bpy.context.object.data.materials.append(m)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(1.4, -.8, .84)); bpy.context.object.data.materials.append(m)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    views = {"front": ((0, -9.5, 2.4), (0, 0, 2.3), 35), "q34": ((-5, -7, 4.5), (0, 0, 2.2), 35),
             "close": ((1.2, -2.6, 2.6), (0, 0, 2.4), 30), "base": ((.9, -2.4, 1.0), (0, 0, .7), 30)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_d_cypress_%s.png" % name)
        bpy.ops.render.render(write_still=True)


def main():
    ob = build()
    lo = [min(v.co[i] for v in ob.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for v in ob.data.vertices) for i in range(3)]
    print("CYPRESS tris", tris(ob), "lo", [round(x, 2) for x in lo], "hi", [round(x, 2) for x in hi])
    if "--export" in ARGS:
        os.makedirs(OUT_FBX, exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, "D_Cypress.fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                                 mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "D_Cypress_v1.blend"))
    if "--render" in ARGS: render()


main()
