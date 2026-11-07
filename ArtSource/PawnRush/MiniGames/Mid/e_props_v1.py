# E Royal Library props (R507), modelled here in Blender (user, 11-07: "이번 건 너가 블렌더에서 디테일하게 만들어"),
# after the GPT sheet Docs/PawnRush/ConceptArt/R507_RoyalLibrary/AssetRefs/E_assets_05_props.png: book trolley,
# rolling ladder, book stack, globe on a stand, reading desk with an open book, reading table with two chairs.
# Stylised like the Tripo sets: chunky rounded bevels, turned legs, brass fittings, books with bands.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python e_props_v1.py -- [--render] [--export]
#
# Materials: wood = a seamless walnut grain made here (PR_Mid_PropWalnut, 1 m along x 0.5 m across), every part's UVs laid
# along its own grain (B's floor swatch showed board joints across legs); brass = B's PR_Mid_MatteBrass (1 m tile); the
# rest plain colours (PR_Mid_Prop*, e_install_unity.py makes them).
#
# Outputs (Export/):
#   E_Prop_<Name>.fbx  each prop alone, origin at its floor centre, front (-y) to the viewer (the preview renders)
#   E_PropsEnv.fbx     all props placed in the section frame (Blender x = section x, y = section z, z = up), clear of
#                      the tower lanes and the aisle: the entry band corners (z 0..4), the side walls, the rear corridor
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "Materials", "Textures")
OUT_FBX = os.path.join(HERE, "Export")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
UVN = "UVMap"

# placement (section frame): (prop, x, z, yaw degrees, floor y). Station floors (x +-2..22, z 4..24) are 0.4 m up.
# Props have no colliders, so they keep off the pawns' ways: the entry (x +-6) to the station ramps (x +-9.8..14.2,
# z 2.4..4), the aisle, the tower lanes, and the rear corridor from the team tunnels (x +-12, z 30) to the exit
# (x +-6, z 36). They stand by the walls: the entry band's outer corners (|x| > 15), a ladder against a wall
# bookcase, the rear corridor's outer ends.
PLACES = []
for s in (-1, 1):
    PLACES += [("Table", s * 17.3, 2.0, 0, 0.), ("Trolley", s * 20.6, 1.4, s * 90, 0.), ("Globe", s * 15.2, 1.0, s * 25, 0.),
               ("Stack", s * 21.1, 3.3, s * 15, 0.), ("Ladder", s * 20.95, 7.5, -s * 90, .4),
               ("Desk", s * 18.0, 33.6, 0, 0.), ("Stack", s * 16.0, 34.8, -s * 30, 0.), ("Trolley", s * 20.6, 31.4, s * 90, 0.)]


# ------------------------------------------------------------------ materials
MATS = {}


def mat(name, color=None, tex=None, rough=.6, metal=0.):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    if tex:
        t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(os.path.join(TEX, tex))
        m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    else:
        b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    MATS[name] = m
    return m


def walnut_texture():
    """A seamless walnut grain for furniture (B's floor swatch has board joints that showed as lines across legs):
    1024 x 512, u = along the grain (1 m), v = across (0.5 m). Every term is periodic in u and v, so it tiles."""
    path = os.path.join(TEX, "PR_Mid_PropWalnut_BaseColor.png")
    H, Wd = 512, 1024
    u = np.linspace(0, 1, Wd, endpoint=False)[None, :]; v = np.linspace(0, 1, H, endpoint=False)[:, None]
    rng = np.random.default_rng(11)
    warp = sum(a * np.sin(2 * np.pi * (fu * u + fv * v) + rng.uniform(0, 6.3))
               for a, fu, fv in ((.018, 1, 1), (.01, 2, 3), (.006, 3, 1), (.004, 5, 7)))
    rings = .5 + .5 * np.sin(2 * np.pi * (23 * (v + warp)))
    fine = .5 + .5 * np.sin(2 * np.pi * (97 * (v + .4 * warp) + 3 * np.sin(2 * np.pi * u)))
    fleck = sum(np.sin(2 * np.pi * (fu * u + fv * v) + rng.uniform(0, 6.3)) for fu, fv in ((41, 13), (57, 29), (83, 7))) / 3
    t = np.clip(.32 * rings ** 3 + .2 * fine + .05 * fleck + .1, 0, 1)[..., None]
    light, dark = np.array([.66, .42, .26]), np.array([.42, .24, .14])       # sRGB warm walnut, as the concept sheets
    rgb = light * (1 - t) + dark * t
    img = bpy.data.images.new("PR_Mid_PropWalnut_BaseColor", Wd, H, alpha=False)
    px = np.ones((H, Wd, 4), np.float32); px[..., :3] = rgb ** 2.2       # linear in Blender, saved as sRGB
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    return "PR_Mid_PropWalnut_BaseColor.png"


def globe_texture():
    """1024 x 512 equirectangular globe: parchment seas, warm brown continents from smooth 3D noise (no real map, no
    lettering), a darker coast line and a faint 30-degree graticule."""
    name = "PR_Mid_PropGlobe_BaseColor.png"
    H, Wd = 512, 1024
    lon = (np.arange(Wd) + .5) / Wd * 2 * np.pi; lat = np.pi / 2 - (np.arange(H) + .5) / H * np.pi
    LON, LAT = np.meshgrid(lon, lat)
    X, Y, Z = np.cos(LAT) * np.cos(LON), np.cos(LAT) * np.sin(LON), np.sin(LAT)
    f = np.empty((H, Wd), np.float32)
    for i in range(H):
        for j in range(Wd):
            n = Vector((X[i, j], Y[i, j], Z[i, j]))
            f[i, j] = noise.noise(n * 1.6 + Vector((3.1, 1.7, .4))) + .5 * noise.noise(n * 3.4) + .2 * noise.noise(n * 8.)
    land = np.clip((f - .15) / .04, 0, 1)
    coast = np.clip(1 - np.abs(f - .17) / .03, 0, 1)
    grid = np.maximum(np.clip(1 - np.abs(((np.degrees(LON) + 15) % 30) - 15) / .6, 0, 1),
                      np.clip(1 - np.abs(((np.degrees(LAT) + 15) % 30) - 15) / .6, 0, 1)) * (1 - land)
    sea, ground, line = np.array([.90, .82, .63]), np.array([.66, .48, .28]), np.array([.40, .27, .15])
    rgb = sea * (1 - land[..., None]) + ground * land[..., None]
    rgb = rgb * (1 - .8 * coast[..., None]) + line * .8 * coast[..., None]
    rgb = rgb * (1 - .35 * grid[..., None]) + line * .35 * grid[..., None]
    img = bpy.data.images.new("PR_Mid_PropGlobe_BaseColor", Wd, H, alpha=False)
    px = np.ones((H, Wd, 4), np.float32); px[..., :3] = rgb[::-1] ** 2.2     # image rows run bottom-up
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(TEX, name); img.file_format = 'PNG'; img.save()
    return name


def W():
    if "PR_Mid_PropWalnut" not in MATS: walnut_texture()
    return mat("PR_Mid_PropWalnut", tex="PR_Mid_PropWalnut_BaseColor.png", rough=.6)
def BR(): return mat("PR_Mid_MatteBrass", tex="PR_Mid_MatteBrass_BaseColor.png", rough=.45, metal=.8)


# linear colours (Blender); e_install_unity.py writes the same as sRGB
COLORS = {
    "PR_Mid_PropLeatherRed": (.36, .10, .06), "PR_Mid_PropLeatherTan": (.50, .27, .11),
    "PR_Mid_PropLeatherBrown": (.22, .11, .06), "PR_Mid_PropClothGrey": (.27, .28, .29),
    "PR_Mid_PropClothSlate": (.16, .19, .23), "PR_Mid_PropPage": (.86, .78, .60),
    "PR_Mid_PropParchment": (.82, .70, .48), "PR_Mid_PropLand": (.30, .19, .09),
    "PR_Mid_PropIron": (.06, .06, .07), "PR_Mid_PropSeat": (.40, .12, .07),
}
BOOKS = ["PR_Mid_PropLeatherRed", "PR_Mid_PropLeatherTan", "PR_Mid_PropLeatherBrown", "PR_Mid_PropClothGrey",
         "PR_Mid_PropClothSlate", "PR_Mid_PropLeatherRed", "PR_Mid_PropLeatherTan"]


def C(name): return mat(name, COLORS[name], rough=.85 if "Cloth" in name or "Page" in name else .6, metal=.6 if "Iron" in name else 0.)


# ------------------------------------------------------------------ geometry
def to_obj(bm, m, name="part"):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(m)
    if UVN not in me.uv_layers: me.uv_layers.new(name=UVN)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def bevel_edges(bm, edges, off, seg=2):
    if off > 0 and edges:
        bmesh.ops.bevel(bm, geom=list(edges), offset=off, offset_type='OFFSET', segments=seg, profile=.5,
                        affect='EDGES', clamp_overlap=True)


def box(lo, hi, m, bev=.015, grain=None, seed=0, seg=2):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co = Vector([lo[i] if v.co[i] < 0 else hi[i] for i in range(3)])
    d = min(hi[i] - lo[i] for i in range(3))
    bevel_edges(bm, bm.edges, min(bev, .45 * d), seg)
    o = to_obj(bm, m, "box")
    uv(o, grain, seed)
    return o


def cyl(a, b, r, m, segs=16, bev=.008, r2=None, grain=True, seed=0):
    a, b = Vector(a), Vector(b); d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r if r2 is None else r2, depth=d.length)
    h = d.length / 2
    bevel_edges(bm, [e for e in bm.edges if all(abs(abs(v.co.z) - h) < 1e-5 for v in e.verts)], min(bev, .4 * r))
    o = to_obj(bm, m, "cyl")
    uv(o, 2 if grain else None, seed)            # in its own frame (grain along the axis), then placed
    o.data.transform(Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4())
    return o


def lathe(profile, m, segs=20, at=(0, 0, 0), seed=0):
    """Turned part about z: profile [(r, z)], r 0 at the ends closes it."""
    bm = bmesh.new()
    vs = [bm.verts.new((r, 0, z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[i + 1])) for i in range(len(vs) - 1)]
    bmesh.ops.spin(bm, geom=vs + es, cent=(0, 0, 0), axis=(0, 0, 1), angle=2 * math.pi, steps=segs, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.transform(Matrix.Translation(at))
    o = to_obj(bm, m, "lathe")
    uv(o, 2, seed)
    return o


def torus(R, r, m, segs=40, rsegs=10, arc=2 * math.pi):
    """Ring in the xz plane (axis y) about the origin; an arc from the bottom when arc < 2 pi."""
    bm = bmesh.new()
    rings = []
    n = segs + (0 if arc >= 2 * math.pi - 1e-6 else 1)
    for i in range(n):
        a = -math.pi / 2 + arc * i / segs
        c = Vector((R * math.cos(a), 0, R * math.sin(a))); out = Vector((math.cos(a), 0, math.sin(a)))
        rings.append([bm.verts.new(c + r * (math.cos(t) * out + math.sin(t) * Vector((0, 1, 0))))
                      for t in [2 * math.pi * k / rsegs for k in range(rsegs)]])
    for i in range(len(rings) - (1 if n == segs + 1 else 0)):
        A, B = rings[i], rings[(i + 1) % len(rings)]
        for k in range(rsegs):
            bm.faces.new((A[k], A[(k + 1) % rsegs], B[(k + 1) % rsegs], B[k]))
    return to_obj(bm, m, "torus")


def uv(o, grain, seed=0, local_axis=None):
    """Wood: u along the grain axis (2 m tile), v across inside one board of the swatch (boards 0.2 of v).
    Others: box projection on a 1 m tile."""
    me = o.data; L = me.uv_layers[UVN]
    p = np.array([v.co[:] for v in me.vertices]); lo, hi = p.min(0), p.max(0); ext = hi - lo + 1e-6
    rng = random.Random(seed * 7919 + len(p))
    board, off = rng.randrange(5) * .2, rng.random()
    if grain is None:
        for poly in me.polygons:
            dom = int(np.argmax(np.abs(poly.normal))); a, b = [i for i in range(3) if i != dom]
            for li in poly.loop_indices:
                q = me.vertices[me.loops[li].vertex_index].co; L.data[li].uv = (q[a], q[b])
        return
    axis = int(np.argmax(ext)) if grain == "long" else grain
    others = [i for i in range(3) if i != axis]
    voff = rng.random()
    for poly in me.polygons:
        dom = int(np.argmax(np.abs(poly.normal)))
        acr = [i for i in others if i != dom][0] if dom in others else (others[0] if ext[others[0]] >= ext[others[1]] else others[1])
        for li in poly.loop_indices:
            q = me.vertices[me.loops[li].vertex_index].co
            L.data[li].uv = (off + q[axis], voff + q[acr] / .5)    # PR_Mid_PropWalnut: 1 m along, 0.5 m across


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = o.data.name = name
    return o


def xf(objs, M):
    for o in objs: o.data.transform(M)
    return objs


# ------------------------------------------------------------------ books
def book(lo_hi, cover, rng, lying=False, bands=True):
    """A hardback: two covers and a spine in cloth/leather, a cream page block inset on the three open sides,
    brass bands across the spine. Standing: spine faces -y, height z. Lying: spine faces -y, thickness z."""
    (x0, y0, z0), (x1, y1, z1) = lo_hi
    t = .012 if not lying else .01
    m = C(cover); pg = C("PR_Mid_PropPage"); parts = []
    if not lying:
        parts += [box((x0, y0, z0), (x0 + t, y1, z1), m, .004, seg=1), box((x1 - t, y0, z0), (x1, y1, z1), m, .004, seg=1),
                  box((x0, y0, z0), (x1, y0 + t * 1.6, z1), m, .006, seg=1)]
        parts.append(box((x0 + t, y0 + t, z0 + .008), (x1 - t, y1 - .006, z1 - .008), pg, 0))
        if bands and x1 - x0 > .035:
            for zz in (z0 + .18 * (z1 - z0), z1 - .18 * (z1 - z0)):
                parts.append(box((x0 + .002, y0 - .003, zz - .006), (x1 - .002, y0 + .01, zz + .006), BR(), 0))
    else:
        parts += [box((x0, y0, z0), (x1, y1, z0 + t), m, .004, seg=1), box((x0, y0, z1 - t), (x1, y1, z1), m, .004, seg=1),
                  box((x0, y0, z0), (x1, y0 + t * 1.6, z1), m, .006, seg=1)]
        parts.append(box((x0 + .006, y0 + t, z0 + t), (x1 - .006, y1 - .006, z1 - t), pg, 0))
        if bands:
            for xx in (x0 + .2 * (x1 - x0), x1 - .2 * (x1 - x0)):
                parts.append(box((xx - .008, y0 - .003, z0 + .002), (xx + .008, y0 + .01, z1 - .002), BR(), 0))
    return parts


def shelf_books(x0, x1, y_front, depth, z0, rng, facing=-1):
    """A row of standing books from x0 to x1 on a shelf at z0, spines to `facing` (-1 = -y)."""
    parts, x = [], x0
    while x < x1 - .05:
        w = rng.uniform(.045, .085); h = rng.uniform(.2, .3); d = depth * rng.uniform(.85, 1.)
        if x + w > x1: break
        b = book(((x, 0, z0), (x + w, d, z0 + h)), rng.choice(BOOKS), rng)
        if facing > 0: xf(b, Matrix.Translation((2 * x + w, 0, 0)) @ Matrix.Diagonal((-1, -1, 1, 1)) @ Matrix.Translation((0, -d, 0)))
        xf(b, Matrix.Translation((0, y_front if facing < 0 else y_front - d, 0)))
        if rng.random() < .15:   # a leaning one
            c = Vector((x + w / 2, 0, z0))
            xf(b, Matrix.Translation(c) @ Matrix.Rotation(math.radians(rng.choice((-8, 8))), 4, 'Y') @ Matrix.Translation(-c))
        parts += b; x += w + rng.uniform(.002, .01)
    return parts


# ------------------------------------------------------------------ props (local: origin floor centre, front -y)
def trolley():
    rng = random.Random(1); w = W(); p = []
    L, D, H = 1.2, .56, 1.02
    for sx in (-1, 1):
        x = sx * (L / 2 - .045)
        for sy in (-1, 1):
            y0, y1 = (-D / 2, -D / 2 + .07) if sy < 0 else (D / 2 - .07, D / 2)
            p.append(box((x - .035, y0, .12), (x + .035, y1, H - .1), w, .015, 2, rng.random() * 99))
        # rounded end top: an arch of short boxes
        for k in range(9):
            a0, a1 = math.pi * k / 9, math.pi * (k + 1) / 9
            yy = -(D / 2 - .035) * math.cos((a0 + a1) / 2); zz = H - .1 + .09 * math.sin((a0 + a1) / 2)
            seg = (D - .07) * math.pi / 2 / 9 + .012
            b = box((x - .035, -seg / 2, -.035), (x + .035, seg / 2, .035), w, .012, 1)
            b.data.transform(Matrix.Translation((0, yy, zz)) @ Matrix.Rotation(-((a0 + a1) / 2 - math.pi / 2), 4, 'X'))
            p.append(b)
        p.append(box((x - .03, -D / 2 + .05, .5), (x + .03, D / 2 - .05, .62), w, .012, 1))   # end panel rail
    for z in (.2, .58):
        p.append(box((-L / 2 + .02, -D / 2 + .02, z - .035), (L / 2 - .02, D / 2 - .02, z), w, .012, 0, rng.random() * 99))
        for sy in (-1, 1):   # shelf lips
            p.append(box((-L / 2 + .08, sy * D / 2 - .03 if sy > 0 else -D / 2, z), (L / 2 - .08, D / 2 if sy > 0 else -D / 2 + .03, z + .05), w, .01, 0))
        for sx in (-1, 1):
            for sy in (-1, 1):   # brass corner caps
                cx, cy = sx * (L / 2 - .045), sy * (D / 2 - .035)
                p.append(box((cx - .05, cy - .05, z - .06), (cx + .05, cy + .05, z + .02), BR(), .01))
        for sy in (-1, 1):   # brass rails along both sides
            p.append(cyl((-L / 2 + .08, sy * (D / 2 + .005), z + .2), (L / 2 - .08, sy * (D / 2 + .005), z + .2), .016, BR(), 12, grain=False))
            for sx in (-1, 1):
                p.append(cyl((sx * (L / 2 - .08), sy * (D / 2 - .03), z + .2), (sx * (L / 2 - .08), sy * (D / 2 + .02), z + .2), .022, BR(), 12, grain=False))
        p += shelf_books(-L / 2 + .12, L / 2 - .12, -D / 2 + .035, .2, z, rng, -1)
        p += shelf_books(-L / 2 + .12, L / 2 - .12, D / 2 - .035, .2, z, rng, 1)
    for sx in (-1, 1):   # castors
        for sy in (-1, 1):
            cx, cy = sx * (L / 2 - .045), sy * (D / 2 - .035)
            p.append(box((cx - .04, cy - .04, .1), (cx + .04, cy + .04, .13), BR(), .008))
            p.append(box((cx - .035, cy - .03, .05), (cx - .022, cy + .03, .11), BR(), .004))
            p.append(box((cx + .022, cy - .03, .05), (cx + .035, cy + .03, .11), BR(), .004))
            p.append(cyl((cx - .02, cy, .055), (cx + .02, cy, .055), .055, C("PR_Mid_PropIron"), 16, .01, grain=False))
            p.append(cyl((cx - .028, cy, .055), (cx + .028, cy, .055), .014, BR(), 10, grain=False))
    return p


def ladder():
    w = W(); p = []; H = 3.1; half = .27
    for sx in (-1, 1):
        p.append(box((sx * half - .035, -.03, .09), (sx * half + .035, .03, H), w, .015, 2, sx + 5))
        p.append(box((sx * half - .045, -.04, H - .16), (sx * half + .045, .05, H - .04), BR(), .01))   # top fitting
        p.append(cyl((sx * half, .02, H - .1), (sx * half, .16, H - .1), .02, BR(), 10, grain=False))  # hook arm
        p.append(box((sx * half - .03, .12, H - .2), (sx * half + .03, .18, H - .06), BR(), .01))       # hook
        p.append(box((sx * half - .045, -.045, .06), (sx * half + .045, .045, .16), BR(), .01))         # wheel fork
        p.append(cyl((sx * half - .02, 0, .06), (sx * half + .02, 0, .06), .06, C("PR_Mid_PropIron"), 16, .01, grain=False))
    p.append(cyl((-half - .06, .1, H - .1), (half + .06, .1, H - .1), .022, BR(), 12, grain=False))   # hook rail
    for sx in (-1, 1):
        p.append(lathe([(0, 0), (.035, 0), (.035, .02), (.025, .03), (.03, .05), (0, .07)], BR(), 12, (sx * (half + .06), .1, H - .135)))
    z = .4
    while z < H - .2:
        p.append(cyl((-half, 0, z), (half, 0, z), .022, w, 12, .006, seed=int(z * 10)))
        z += .3
    # leaning toward +y (the wall), feet on the floor
    xf(p, Matrix.Rotation(math.radians(-14), 4, 'X'))
    zmin = min(min(v.co.z for v in o.data.vertices) for o in p)
    xf(p, Matrix.Translation((0, 0, -zmin)))
    return p


def stack():
    rng = random.Random(3); p = []; z = 0.
    for i in range(6):
        lx, ly, th = rng.uniform(.34, .46) - i * .02, rng.uniform(.26, .32) - i * .012, rng.uniform(.05, .085)
        b = book(((-lx / 2, -ly / 2, z), (lx / 2, ly / 2, z + th)), BOOKS[(i * 3) % len(BOOKS)], rng, lying=True)
        a = math.radians(rng.uniform(-12, 12))
        p += xf(b, Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((rng.uniform(-.02, .02), rng.uniform(-.02, .02), 0)))
        z += th + .002
    return p


def globe():
    w = W(); p = []
    p.append(lathe([(0, .06), (.13, .06), (.15, .1), (.08, .14), (.06, .2), (.075, .3), (.05, .42), (.045, .55),
                    (.07, .6), (.09, .64), (0, .66)], w, 20))
    for k in range(3):   # three curved legs
        a = 2 * math.pi * k / 3 + math.pi / 2
        for i in range(5):
            r0, r1 = .05 + .06 * i, .05 + .06 * (i + 1)
            z0, z1 = .16 - .028 * i ** 1.25, .16 - .028 * (i + 1) ** 1.25
            mid = ((r0 + r1) / 2, (max(z0, .025) + max(z1, .025)) / 2)
            seg = box((-.035, -.034, -.03), (.035, .034, .03), w, .012, 0, k * 10 + i)
            ang = math.atan2(max(z1, .025) - max(z0, .025), r1 - r0)
            seg.data.transform(Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((mid[0], 0, mid[1])) @ Matrix.Rotation(-ang, 4, 'Y') @ Matrix.Diagonal((1.6, 1, 1, 1)))
            p.append(seg)
        p.append(lathe([(0, 0), (.04, 0), (.045, .03), (0, .045)], BR(), 12, (.34 * math.cos(a), .34 * math.sin(a), 0)))
    c = Vector((0, 0, 1.0)); R = .3
    # the globe: its own UV sphere mapping and a baked equirectangular map (smooth continents, a faint graticule)
    bm = bmesh.new(); bm.loops.layers.uv.new(UVN)
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=28, radius=R, calc_uvs=True)
    sphere = to_obj(bm, mat("PR_Mid_PropGlobe", tex=globe_texture(), rough=.7), "globe")
    tilt = Matrix.Translation(c) @ Matrix.Rotation(math.radians(23), 4, 'Y')
    sphere.data.transform(tilt); p.append(sphere)
    ring = torus(R + .045, .016, BR(), 48, 8); ring.data.transform(tilt @ Matrix.Rotation(math.pi / 2, 4, 'Z')); uv(ring, None); p.append(ring)
    pin = cyl((0, 0, -R - .07), (0, 0, R + .07), .012, BR(), 8, grain=False); pin.data.transform(tilt); p.append(pin)
    for zz in (R + .07, -R - .07):
        knob = lathe([(0, -.02), (.025, -.02), (.03, 0), (.02, .02), (0, .025)], BR(), 12, (0, 0, zz)); knob.data.transform(tilt); p.append(knob)
    p.append(lathe([(0, 0), (.06, 0), (.045, .05), (.02, .09), (0, .1)], BR(), 14, (0, 0, .64)))   # cradle cup
    p.append(cyl((0, 0, .7), (0, 0, 1.0 - R - .02), .02, BR(), 10, grain=False))
    return p


def open_book(rng, at, slope):
    """An open book on a slanted top: cover boards and two page blocks whose tops arch up to the spine."""
    p = []; L, D = .5, .36
    cv = C("PR_Mid_PropLeatherRed"); pg = C("PR_Mid_PropPage")
    p.append(box((-L / 2 - .015, -D / 2 - .015, 0), (L / 2 + .015, D / 2 + .015, .012), cv, .005))
    for sx in (-1, 1):
        bm = bmesh.new(); nx, ny = 10, 4
        grid = [[None] * (ny + 1) for _ in range(nx + 1)]
        for i in range(nx + 1):
            t = i / nx; x = sx * (L / 2) * t
            # the gutter dips at the spine, the pages swell in the middle and fall to a thick fore-edge
            top = .012 + (.03 + .025 * math.sin(math.pi * t)) * min(1., .35 + t * 4.3)
            for j in range(ny + 1):
                y = -D / 2 + D * j / ny
                grid[i][j] = (bm.verts.new((x, y, top)), bm.verts.new((x, y, .012)))
        for i in range(nx):
            for j in range(ny):
                a, b, c_, d = grid[i][j][0], grid[i + 1][j][0], grid[i + 1][j + 1][0], grid[i][j + 1][0]
                bm.faces.new((a, b, c_, d) if sx > 0 else (a, d, c_, b))
        for i in range(nx):   # page edges front and back, and the outer edge
            for j in (0, ny):
                bm.faces.new((grid[i][j][0], grid[i + 1][j][0], grid[i + 1][j][1], grid[i][j][1]))
        for j in range(ny):
            bm.faces.new((grid[nx][j][0], grid[nx][j + 1][0], grid[nx][j + 1][1], grid[nx][j][1]))
        p.append(to_obj(bm, pg, "pages")); uv(p[-1], None)
    p.append(cyl((0, -D / 2, .05), (0, D / 2, .05), .006, C("PR_Mid_PropLeatherRed"), 8, grain=False))   # ribbon line
    return xf(p, Matrix.Translation(at) @ Matrix.Rotation(math.radians(slope), 4, 'X'))


def desk():
    rng = random.Random(5); w = W(); p = []; L, D = 1.3, .72
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (L / 2 - .06), sy * (D / 2 - .06)
            p.append(box((x - .045, y - .045, 0), (x + .045, y + .045, .78), w, .015, 2, sx * 3 + sy))
            p.append(box((x - .06, y - .06, 0), (x + .06, y + .06, .06), w, .015, 0))   # foot block
    p.append(box((-L / 2 + .04, -D / 2 + .04, .14), (L / 2 - .04, -D / 2 + .08, .2), w, .01, 0))       # stretcher
    p.append(box((-L / 2 + .04, D / 2 - .08, .14), (L / 2 - .04, D / 2 - .04, .2), w, .01, 0))
    p.append(box((-L / 2 + .02, -D / 2 + .02, .56), (L / 2 - .02, D / 2 - .02, .78), w, .015, 0, 7))   # drawer body
    for sx in (-1, 1):   # drawer fronts and brass cup pulls
        x0, x1 = (-L / 2 + .1, -.02) if sx < 0 else (.02, L / 2 - .1)
        p.append(box((x0, -D / 2 - .002, .585), (x1, -D / 2 + .03, .755), w, .012, 0, 8 + sx))
        cx = (x0 + x1) / 2
        p.append(box((cx - .07, -D / 2 - .03, .655), (cx + .07, -D / 2 + .005, .685), BR(), .01))
        cup = torus(.05, .012, BR(), 16, 6, math.pi); cup.data.transform(Matrix.Translation((cx, -D / 2 - .02, .68)) @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Rotation(math.pi, 4, 'Y')); uv(cup, None)
        p.append(cup)
    # slanted top, 18 degrees, low edge to the front with a book ledge
    top = box((-L / 2 - .03, -D / 2 - .03, 0), (L / 2 + .03, D / 2 + .03, .05), w, .015, 0, 11)
    top.data.transform(Matrix.Translation((0, 0, .9)) @ Matrix.Rotation(math.radians(18), 4, 'X')); p.append(top)
    for sx in (-1, 1):   # side cheeks under the slant
        bm = bmesh.new()
        v = [bm.verts.new((sx * (L / 2 - .02) + dx, y, z)) for dx in (-.02, .02) for y, z in ((-D / 2, .78), (D / 2, .78), (D / 2, .9 + .12), (-D / 2, .9 - .12))]
        for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            bm.faces.new([v[i] for i in f])
        p.append(to_obj(bm, w, "cheek")); uv(p[-1], 1, sx)
    ledge = box((-L / 2 + .05, -.03, 0), (L / 2 - .05, .0, .05), w, .01, 0)
    ledge.data.transform(Matrix.Translation((0, 0, .9)) @ Matrix.Rotation(math.radians(18), 4, 'X') @ Matrix.Translation((0, -D / 2 + .06, .05))); p.append(ledge)
    p += open_book(rng, (0, -.02, .9 + .05 + .02), 18)
    return p


def chair(rng, seed):
    w = W(); p = []; S = .46; h = .46
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (S / 2 - .035), sy * (S / 2 - .035)
            top = 1.0 if sy > 0 else h
            p.append(box((x - .028, y - .028, 0), (x + .028, y + .028, top), w, .012, 2, seed + sx * 3 + sy))
    p.append(box((-S / 2, -S / 2, h - .05), (S / 2, S / 2, h), w, .012, 0, seed + 9))                     # seat frame
    p.append(box((-S / 2 + .03, -S / 2 + .03, h), (S / 2 - .03, S / 2 - .03, h + .07), C("PR_Mid_PropSeat"), .035))   # cushion
    for zz in (.15,):
        for sx in (-1, 1):
            p.append(box((sx * (S / 2 - .035) - .015, -S / 2 + .04, zz), (sx * (S / 2 - .035) + .015, S / 2 - .04, zz + .03), w, .006, 1))
    p.append(box((-S / 2 + .02, S / 2 - .05, .9), (S / 2 - .02, S / 2 - .01, 1.02), w, .015, 0, seed + 11))   # top rail
    p.append(box((-S / 2 + .02, S / 2 - .05, .66), (S / 2 - .02, S / 2 - .02, .74), w, .012, 0, seed + 12))   # mid rail
    for x in (-.08, .08):
        p.append(box((x - .02, S / 2 - .045, .74), (x + .02, S / 2 - .025, .9), w, .006, 2))
    return p


def table():
    rng = random.Random(7); w = W(); p = []; L, D, H = 2.0, .9, .78
    p.append(box((-L / 2, -D / 2, H - .07), (L / 2, D / 2, H), w, .02, 0, 21))
    p.append(box((-L / 2 + .07, -D / 2 + .07, H - .17), (L / 2 - .07, D / 2 - .07, H - .07), w, .012, 0, 22))   # apron
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (L / 2 - .12), sy * (D / 2 - .12)
            p.append(lathe([(0, 0), (.06, 0), (.06, .08), (.045, .1), (.05, .16), (.07, .28), (.05, .42), (.04, .5),
                            (.055, .56), (.055, H - .17), (0, H - .17)], w, 16, (x, y, 0), seed=sx + sy * 2))
    for sx in (-1, 1):
        p.append(box((sx * (L / 2 - .12) - .025, -D / 2 + .14, .12), (sx * (L / 2 - .12) + .025, D / 2 - .14, .17), w, .01, 1))
    p.append(box((-L / 2 + .16, -.025, .12), (L / 2 - .16, .025, .17), w, .01, 0))
    # books on the top
    z = H
    for i, (lx, ly, th) in enumerate(((.36, .27, .07), (.32, .24, .06))):
        p += xf(book(((-lx / 2, -ly / 2, z), (lx / 2, ly / 2, z + th)), BOOKS[i + 2], rng, lying=True), Matrix.Translation((-.55, .1, 0)) @ Matrix.Rotation(math.radians(8 * i - 6), 4, 'Z'))
        z += th + .002
    p += xf(book(((-.17, -.12, H), (.17, .12, H + .06)), BOOKS[0], rng, lying=True), Matrix.Translation((.5, .05, 0)) @ Matrix.Rotation(math.radians(20), 4, 'Z'))
    for x in (-.45, .45):   # two chairs pushed in on the front side, backs to the viewer's side? no: seats under
        c = chair(rng, int(x * 10))
        p += xf(c, Matrix.Translation((x, -D / 2 + .02, 0)) @ Matrix.Rotation(math.pi, 4, 'Z'))
    return p


PROPS = {"Trolley": trolley, "Ladder": ladder, "Stack": stack, "Globe": globe, "Desk": desk, "Table": table}


def export(o, name):
    os.makedirs(OUT_FBX, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_FBX, name + ".fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)


def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = {}
    for name, make in PROPS.items():
        built[name] = join(make(), "E_Prop_" + name)
        print("PROP", name, tris(built[name]), "tris", [m.name for m in built[name].data.materials])
    env = []
    for name, x, z, yaw, y in PLACES:
        me = built[name].data.copy()
        me.transform(Matrix.Translation((x, z, y)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
        o = bpy.data.objects.new("placed", me); bpy.context.scene.collection.objects.link(o); env.append(o)
    props_env = join(env, "E_PropsEnv")
    print("ENV", tris(props_env), "tris")
    if "--export" in ARGS:
        export(props_env, "E_PropsEnv")
    props_env.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "E_Props_v1.blend"))
    if "--render" in ARGS: render(built)


def render(built):
    """Each prop alone on a cream ground with a pawn for scale, 3/4 from the front, and the six in a row."""
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1100, 1100
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.95, .9, .82, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .6
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(45), 0, math.radians(-35))); bpy.context.object.data.energy = 3.5
    pawn_m = bpy.data.materials.new("pawn"); pawn_m.use_nodes = True
    pawn_m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.95, .93, .9, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=.22, depth=.62, location=(0, 0, .31)); pb = bpy.context.object; pb.data.materials.append(pawn_m)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.21, location=(0, 0, .84)); ph = bpy.context.object; ph.data.materials.append(pawn_m)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    for o in built.values(): o.hide_render = True
    for name, o in built.items():
        o.hide_render = False
        p = np.array([v.co[:] for v in o.data.vertices]); lo, hi = p.min(0), p.max(0); size = float(max(hi - lo))
        for e in (pb, ph): e.location.x = hi[0] + .45; e.location.y = 0
        ph.location.z = .84
        tgt = Vector(((lo[0] + hi[0]) / 2 + .2, 0, (lo[2] + hi[2]) / 2))
        cam.location = tgt + Vector((-.55, -1.25, .55)) * (size * 1.25 + .9); cd.lens = 50
        cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_e_prop_%s.png" % name.lower()); bpy.ops.render.render(write_still=True)
        o.hide_render = True


if __name__ == "__main__":
    main()
