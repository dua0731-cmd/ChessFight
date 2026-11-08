# Royal water garden - concept-art dressing, sample 1: the fountain plaza (beat 1) of the v31 layout (R400+).
#
# 승규 11-04: "배치는 지금 대로 하고 컨셉 아트만 저렇게 입힐 거야" -> the v31 graybox (RoyalWaterGardenBuilder) keeps every
# position and collider; this file only builds what is drawn over it, in the concept art's look: pink-beige herringbone
# paving, cream limestone, gold and royal blue, flower planters, wooden spin bars with gold caps.
#
# Frame: Blender X = section x (east), Blender Y = section z (north), Z = up; 1 unit = 1 m; a floor's top is z 0.
# The blockout numbers come from RoyalWaterGardenBuilder.Beats.cs (FountainPlaza, SpinBar).
#
# Run:  blender -b --python plaza_sample_v1.py
# Out:  PlazaSample_v1.blend, textures/Herringbone*.png, render_plaza_v1_*.png (next to this file)

import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "textures")
os.makedirs(TEX, exist_ok=True)
random.seed(11)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

# ------------------------------------------------------------------ the v31 numbers (section frame)
PLAZA = (0.0, -35.0)
R_PLAZA = 17.0
KERB_IN, KERB_OUT, KERB_H = 16.4, 17.0, 1.1
DEEP, WATER = -6.0, -1.7
# x, z(north), reach, deg/s, phase, tall
BARS = [(-6.5, -43.5, 4.5, 80, 0, 0), (6.5, -43.5, 4.5, -80, 0, 0), (0, -35, 5, 51.4, 0, 1),
        (-8, -27.5, 4.5, -72, .25, 0), (8, -27.5, 4.5, 72, .25, 0)]
# Where the kerb is open (degrees round the plaza centre): the start yard (south) and the apron (north).
OPEN = [(90, 24), (270, 24)]
# Where the plaza wall is buried under the start yard / the apron.
BURIED = [(90, 25.5), (270, 22.5)]


def srgb(c):
    return tuple((v / 12.92) if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


# ------------------------------------------------------------------ collections and materials
def coll(name):
    c = bpy.data.collections.new(name)
    sc.collection.children.link(c)
    return c


CTX = coll("Context | v31 blockout (not exported)")
DRESS = coll("Plaza dressing")
SCALE = coll("Scale pawns (not exported)")


def material(name, color, rough=.6, metal=0., emit=0.):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*srgb(color), 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit:
        p.inputs["Emission Color"].default_value = (*srgb(color), 1)
        p.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*srgb(color), 1)
    return m


M = {
    "stone": material("GC | cream stone", (.92, .82, .64), .72),
    "stone2": material("GC | cream stone warm", (.88, .77, .59), .75),
    "stone3": material("GC | cream stone pale", (.95, .88, .73), .7),
    "gold": material("GC | gold", (1.0, .74, .28), .28, 1.0),
    "wood": material("GC | bar wood", (.46, .26, .12), .55),
    "soil": material("GC | planter soil", (.30, .19, .10), .9),
    "leaf": material("GC | leaf", (.20, .50, .13), .7),
    "leaf2": material("GC | leaf light", (.32, .62, .18), .7),
    "leaf3": material("GC | leaf dark", (.11, .36, .12), .7),
    "pink": material("GC | flower pink", (1.0, .48, .68), .6),
    "yellow": material("GC | flower yellow", (1.0, .86, .22), .6),
    "white": material("GC | flower white", (.98, .97, .93), .6),
    "coral": material("GC | flower coral", (1.0, .55, .40), .6),
    "purple": material("GC | flower purple", (.70, .50, .92), .6),
    "blue": material("GC | royal blue", (.10, .24, .66), .6),
    "jet": material("GC | fountain jet", (.80, .95, 1.0), .1, 0, .6),
    "lily": material("GC | lily pad", (.30, .62, .22), .55),
    "clay": material("Context | clay", (.55, .55, .55), .8),
    "pawn": material("Context | pawn white", (.95, .95, .95), .5),
    "pawn_dark": material("Context | pawn black", (.06, .06, .07), .5),
}
STONES = [M["stone"], M["stone2"], M["stone3"]]
FLOWERS = [M["pink"], M["yellow"], M["white"], M["coral"], M["purple"]]
LEAVES = [M["leaf"], M["leaf2"], M["leaf3"]]


# ------------------------------------------------------------------ the herringbone paving texture
def herringbone(name, size=2048, tile=4.0, w=.25, mortar=.022):
    """A seamless tile (tile metres square) of 2:1 bricks (w x 2w) laid in herringbone, with a matching height map."""
    n = size
    t = (np.arange(n) + .5) / n * tile / w  # brick-width units
    X, Y = np.meshgrid(t, t)
    found = np.zeros(X.shape, bool)
    dist = np.zeros(X.shape)
    ident = np.zeros(X.shape)
    kind = np.zeros(X.shape)
    b0 = np.floor((X - Y) / 4.0)
    period = int(round(tile / w)) // 2  # lattice steps that wrap the tile
    for db in (-1, 0, 1):
        b = b0 + db
        qx, qy = X - 2 * b, Y + 2 * b
        k0 = np.floor(qy)
        for dk in (-3, -2, -1, 0, 1):
            k = k0 + dk
            rx, ry = qx - k, qy - k
            inH = (rx >= 0) & (rx < 2) & (ry >= 0) & (ry < 1)
            dH = np.minimum(np.minimum(rx, 2 - rx), np.minimum(ry, 1 - ry))
            inV = (rx >= 0) & (rx < 1) & (ry >= 1) & (ry < 3)
            dV = np.minimum(np.minimum(rx, 1 - rx), np.minimum(ry - 1, 3 - ry))
            for inside, d, kd in ((inH, dH, 0), (inV, dV, 1)):
                m = inside & ~found
                # Seamless: the brick's id only from what the tile's own wrap keeps.
                a = np.mod(k - 2 * b, 2 * period)
                c = np.mod(k + 2 * b, 2 * period)
                ident[m] = (a * 97 + c * 31 + kd * 7)[m]
                dist[m] = d[m]
                kind[m] = kd
                found |= m
    rnd = np.mod(np.sin(ident * 12.9898 + 4.1) * 43758.5453, 1.0)
    rnd2 = np.mod(np.sin(ident * 78.233 + 1.7) * 24634.6345, 1.0)
    base = np.array([.90, .68, .53])
    warm = np.array([.95, .76, .58])
    deep = np.array([.82, .58, .45])
    col = base[None, None, :] * (1 - rnd[..., None]) + warm[None, None, :] * rnd[..., None]
    col = col * (1 - .45 * rnd2[..., None]) + deep[None, None, :] * (.45 * rnd2[..., None])
    edge = dist * w  # metres to the brick's edge
    mortar_mask = edge < mortar / 2
    bevel = np.clip(edge / .03, 0, 1)
    col = col * (.86 + .14 * bevel[..., None])
    grain = np.random.default_rng(3).normal(0, .018, X.shape)
    col = col + grain[..., None]
    col[mortar_mask] = np.array([.96, .89, .79])
    col = np.clip(col, 0, 1)
    height = np.where(mortar_mask, 0.0, .55 + .45 * np.sqrt(bevel))

    def save(fname, rgb):
        img = bpy.data.images.new(fname, n, n, alpha=False)
        rgba = np.concatenate([rgb, np.ones(rgb.shape[:2] + (1,))], axis=2).astype(np.float32)
        img.pixels.foreach_set(rgba.ravel())
        img.filepath_raw = os.path.join(TEX, fname + ".png")
        img.file_format = "PNG"
        img.save()
        return img

    color = save(name, col)
    h = save(name + "_height", np.repeat(height[..., None], 3, axis=2))
    h.colorspace_settings.name = "Non-Color"
    return color, h


hb_color, hb_height = herringbone("Herringbone_PinkBeige")
TILE = 4.0


def paving_material():
    m = bpy.data.materials.new("GC | herringbone paving")
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = .78
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = hb_color
    th = nt.nodes.new("ShaderNodeTexImage"); th.image = hb_height
    bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = .35
    nt.links.new(tc.outputs["Color"], p.inputs["Base Color"])
    nt.links.new(th.outputs["Color"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], p.inputs["Normal"])
    m.diffuse_color = (*srgb((.88, .67, .58)), 1)
    return m


M["paving"] = paving_material()


# ------------------------------------------------------------------ mesh helpers
class Mesh:
    """One object built from many pieces, each with its own material."""

    def __init__(self, name, collection=DRESS):
        self.name, self.coll = name, collection
        self.bm = bmesh.new()
        self.mats = []
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def tag(self, verts, mat, smooth=False, bevel_edges=None, offset=0.):
        """Gives the piece made of `verts` its material, bevels `bevel_edges` and returns the piece's vertices."""
        i = self.mi(mat)
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = i
            f.smooth = smooth
        if offset > 0 and bevel_edges:
            r = bmesh.ops.bevel(self.bm, geom=list(bevel_edges), offset=offset, segments=2, affect="EDGES",
                                profile=.5, clamp_overlap=True)
            for f in r["faces"]:
                f.material_index = i
                f.smooth = smooth
            faces = {f for f in faces if f.is_valid} | set(r["faces"])
        return list({v for f in faces for v in f.verts})

    def box(self, lo, hi, mat, bevel=0.):
        lo, hi = Vector(lo), Vector(hi)
        size, mid = hi - lo, (lo + hi) / 2
        mtx = Matrix.Translation(mid) @ Matrix.Diagonal((*size, 1)).to_4x4()
        r = bmesh.ops.create_cube(self.bm, size=1, matrix=mtx)
        edges = {e for v in r["verts"] for e in v.link_edges}
        return self.tag(r["verts"], mat, False, edges, bevel)

    def lathe(self, profile, mtx, mat, segs=24, smooth=True, loop=False):
        """profile: (radius, z) from bottom to top; radius 0 at an end closes it."""
        rings = []
        for r, z in profile:
            if r <= 1e-6:
                rings.append([self.bm.verts.new(mtx @ Vector((0, 0, z)))])
            else:
                rings.append([self.bm.verts.new(mtx @ Vector((r * math.cos(2 * math.pi * i / segs),
                                                               r * math.sin(2 * math.pi * i / segs), z)))
                              for i in range(segs)])
        made = [v for ring in rings for v in ring]
        for a, b in zip(rings, rings[1:]):
            if len(a) == 1 and len(b) == 1:
                continue
            for i in range(segs):
                j = (i + 1) % segs
                if len(a) == 1:
                    self.bm.faces.new((a[0], b[i], b[j]))
                elif len(b) == 1:
                    self.bm.faces.new((a[i], b[0], a[j]))
                else:
                    self.bm.faces.new((a[i], b[i], b[j], a[j]))
        if loop:
            a, b = rings[-1], rings[0]
            for i in range(segs):
                j = (i + 1) % segs
                self.bm.faces.new((a[i], b[i], b[j], a[j]))
        elif len(rings[0]) > 1:
            self.bm.faces.new(list(reversed(rings[0])))
        if len(rings[-1]) > 1 and not loop:
            self.bm.faces.new(rings[-1])
        self.tag(made, mat, smooth)
        return made

    def sphere(self, at, radius, mat, scale=(1, 1, 1), subdiv=2):
        mtx = Matrix.Translation(Vector(at)) @ Matrix.Diagonal((*scale, 1)).to_4x4()
        r = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius, matrix=mtx)
        self.tag(r["verts"], mat, True)
        return r["verts"]

    def arc(self, r0, r1, a0, a1, z0, z1, mat, center=PLAZA, bevel=0., segs=None):
        """A ring piece between radii r0..r1, angles a0..a1 (degrees), heights z0..z1."""
        if segs is None:
            segs = max(2, int(abs(a1 - a0) / 2.5))
        cx, cy = center
        rings = []
        for i in range(segs + 1):
            a = math.radians(a0 + (a1 - a0) * i / segs)
            c, s = math.cos(a), math.sin(a)
            rings.append([self.bm.verts.new((cx + r * c, cy + r * s, z)) for r, z in ((r0, z0), (r1, z0), (r1, z1), (r0, z1))])
        for a, b in zip(rings, rings[1:]):
            for j in range(4):
                k = (j + 1) % 4
                self.bm.faces.new((a[j], b[j], b[k], a[k]))
        self.bm.faces.new(list(reversed(rings[0])))
        self.bm.faces.new(rings[-1])
        made = [v for r in rings for v in r]
        # The four long edges and the two end outlines, not the cross-sections between segments.
        pick = set()
        if bevel:
            for a, b in zip(rings, rings[1:]):
                for j in range(4):
                    e = self.bm.edges.get((a[j], b[j]))
                    if e: pick.add(e)
            for ring in (rings[0], rings[-1]):
                for j in range(4):
                    e = self.bm.edges.get((ring[j], ring[(j + 1) % 4]))
                    if e: pick.add(e)
        return self.tag(made, mat, False, pick, bevel)

    def done(self, planar_uv=None):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        for f in bm.faces:
            for loop in f.loops:
                co = loop.vert.co
                if planar_uv:
                    loop[self.uv].uv = planar_uv(co)
                else:
                    # Box mapping in metres (for stone and wood).
                    n = f.normal
                    if abs(n.z) > .7:
                        loop[self.uv].uv = (co.x, co.y)
                    elif abs(n.x) > abs(n.y):
                        loop[self.uv].uv = (co.y, co.z)
                    else:
                        loop[self.uv].uv = (co.x, co.z)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        self.coll.objects.link(ob)
        return ob


def ang_ok(a, zones):
    return all(abs((a - c + 180) % 360 - 180) > half for c, half in zones)


def plaza_pt(a, r, z=0.):
    t = math.radians(a)
    return (PLAZA[0] + r * math.cos(t), PLAZA[1] + r * math.sin(t), z)


def herring_uv(co):
    # 45 degrees to the section's axes, 4 m a tile.
    s = 1 / (TILE * math.sqrt(2))
    return ((co.x + co.y) * s, (co.y - co.x) * s)


# ------------------------------------------------------------------ the context (v31 blockout, grey)
def context():
    m = Mesh("Context blockout", CTX)
    m.lathe([(0, DEEP), (R_PLAZA, DEEP), (R_PLAZA, -.02), (0, -.02)], Matrix.Translation((PLAZA[0], PLAZA[1], 0)), M["clay"], 96, False)
    m.box((-6, -61, DEEP), (6, -50, -.02), M["clay"])
    m.box((-7, -21, DEEP), (7, -18.5, -.02), M["clay"])
    m.box((-35, -18.5, DEEP), (7, -12.5, 0), M["clay"])
    m.box((-35, -12.5, DEEP), (-27, -1, 1.8), M["clay"])
    m.box((-48.5, -63, DEEP), (-46.5, 0, 7), M["clay"])
    m.box((46.5, -63, DEEP), (48.5, 0, 7), M["clay"])
    m.box((-46.5, -63, DEEP), (-6.3, -61.5, 7), M["clay"])
    m.box((6.3, -63, DEEP), (46.5, -61.5, 7), M["clay"])
    m.box((-6, -70, DEEP), (6, -61, 0), M["clay"])  # the section before
    m.done()
    # The water: turquoise, glossy, small waves.
    w = bpy.data.materials.new("Context | water")
    w.use_nodes = True
    nt = w.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*srgb((.02, .58, .82)), 1)
    p.inputs["Roughness"].default_value = .06
    noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 1.6
    bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = .25
    nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], p.inputs["Normal"])
    wm = Mesh("Context water", CTX)
    wm.box((-46.5, -61.5, WATER - .05), (46.5, 60.5, WATER), w)
    wm.done()


context()

# ------------------------------------------------------------------ the floor: herringbone, a cream border, medallions
floor = Mesh("Plaza paving")
r = bmesh.ops.create_circle(floor.bm, cap_ends=True, segments=128, radius=14.8,
                            matrix=Matrix.Translation((PLAZA[0], PLAZA[1], 0)))
floor.tag(r["verts"], M["paving"])
floor.box((-6, -61, -.05), (6, -51, 0), M["paving"])
floor.done(herring_uv)

border = Mesh("Plaza border stones")
n_slab = 120
for i in range(n_slab):
    a0, a1 = i * 360 / n_slab, (i + 1) * 360 / n_slab
    border.arc(14.8, 17.0, a0 + .08, a1 - .08, -.06, .012, random.choice(STONES[:2]), bevel=.018, segs=2)
# A cream medallion with a gold ring under each hub.
for x, z, reach, _, _, tall in BARS:
    ro = 2.4 if tall else 1.0
    border.lathe([(0, -.02), (ro, -.02), (ro, .012), (0, .012)], Matrix.Translation((x, z, 0)), M["stone"], 48, False)
    border.lathe([(ro, -.02), (ro + .14, -.02), (ro + .14, .016), (ro, .016)], Matrix.Translation((x, z, 0)), M["gold"], 48, False, True)
border.done()

# ------------------------------------------------------------------ the plaza's side: block courses and a coping
wall = Mesh("Plaza wall stones")
courses = [(-2.25, -1.55), (-1.55, -.86), (-.86, -.17)]
step = math.degrees(1.5 / R_PLAZA)
for ci, (z0, z1) in enumerate(courses):
    a = (step / 2) * (ci % 2)
    while a < 360 + step:
        a0, a1 = a, a + step
        a += step
        mid = (a0 + a1) / 2
        if not ang_ok(mid, BURIED):
            continue
        wall.arc(R_PLAZA - .05, R_PLAZA + .13, a0 + .12, a1 - .12, z0 + .03, z1 - .03, random.choice(STONES), bevel=.035, segs=4)
a = 0
while a < 360:
    if ang_ok(a + 2.5, BURIED):
        wall.arc(R_PLAZA - .1, R_PLAZA + .26, a + .05, a + 5 - .05, -.17, .012, M["stone3"], bevel=.05, segs=3)
    a += 5
# The start yard's sides (x +-6..6.35, from the section join to the plaza wall).
for side in (-1, 1):
    for ci, (z0, z1) in enumerate(courses):
        y = -61 + .75 * (ci % 2)
        while y < -50.8:
            y1 = min(y + 1.5, -50.8)
            x0, x1 = (6.0, 6.36) if side > 0 else (-6.36, -6.0)
            wall.box((x0, y + .06, z0 + .03), (x1, y1 - .06, z1 - .03), random.choice(STONES), .035)
            y = y1
    x0, x1 = (5.95, 6.5) if side > 0 else (-6.5, -5.95)
    wall.box((x0, -61, -.17), (x1, -50.9, .0), M["stone3"], .05)
wall.done()

# ------------------------------------------------------------------ the kerb as flower planters, posts between them
POSTS = [66, 22, -22, -66, 114, 158, 202, 246]
ENTRY_POSTS = {66, -66, 114, 246}
planter = Mesh("Plaza planters")
flowers = Mesh("Plaza planter flowers")


# The bed reaches 0.35 m past the kerb's collider over the plaza wall's coping (outwards only: the plaza side keeps the
# collider's face at r 16.4), so it reads as the concept's broad flower beds.
BED_OUT = KERB_OUT + .35


def planter_run(a0, a1):
    planter.arc(KERB_IN, KERB_IN + .14, a0, a1, 0, .8, M["stone"], bevel=.03)
    planter.arc(BED_OUT - .14, BED_OUT, a0, a1, -.17, .8, M["stone"], bevel=.03)
    planter.arc(KERB_IN - .03, KERB_IN + .18, a0, a1, .8, .9, M["stone3"], bevel=.035)
    planter.arc(BED_OUT - .18, BED_OUT + .04, a0, a1, .8, .9, M["stone3"], bevel=.035)
    planter.arc(KERB_IN + .14, BED_OUT - .14, a0, a1, 0, .78, M["soil"])
    # Two rows of leafy mounds full of flowers, every ~0.4 m; a round topiary every ~4 m.
    length = math.radians(a1 - a0) * 16.9
    count = int(length / .4)
    tops = max(1, int(length / 4.2))
    top_at = [a0 + (a1 - a0) * (j + .5) / tops for j in range(tops)]
    for row in (16.78, 17.08):
        for i in range(count):
            a = a0 + (a1 - a0) * (i + .5 + (.5 if row > 17 else 0)) / count
            if a > a1 or any(abs(a - t) < math.degrees(.5 / 16.9) for t in top_at):
                continue
            x, y, _ = plaza_pt(a, row + random.uniform(-.04, .04))
            h = random.uniform(.88, 1.0)
            flowers.sphere((x, y, h), random.uniform(.24, .3), random.choice(LEAVES), (1, 1, .72), 1)
            for _ in range(random.randint(5, 8)):
                t = random.uniform(0, 2 * math.pi)
                d = random.uniform(.04, .24)
                flowers.sphere((x + d * math.cos(t), y + d * math.sin(t), h + .2 - d * .35 + random.uniform(-.02, .03)),
                               random.uniform(.06, .09), random.choice(FLOWERS), (1, 1, .7), 1)
    for t in top_at:
        x, y, _ = plaza_pt(t, 16.92)
        flowers.sphere((x, y, 1.2), .46, random.choice(LEAVES[:2]), (1, 1, .95), 2)


runs = [(-66, -22), (-22, 22), (22, 66), (114, 158), (158, 202), (202, 246)]
for a0, a1 in runs:
    off = math.degrees(.38 / 16.7)
    planter_run(a0 + off, a1 - off)


def post(a, entry):
    x, y, _ = plaza_pt(a, 16.7)
    t = math.radians(a)
    mtx = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(t, 4, "Z")

    def b(lo, hi, mat, bev):
        v = planter.box(lo, hi, mat, bev)
        for vv in v:
            vv.co = mtx @ vv.co

    b((-.42, -.42, 0), (.42, .42, .2), M["stone2"], .04)
    b((-.33, -.33, .2), (.33, .33, 1.22), M["stone"], .04)
    b((-.4, -.4, 1.22), (.4, .4, 1.34), M["stone3"], .04)
    b((-.35, -.35, 1.34), (.35, .35, 1.38), M["gold"], .01)
    top = mtx @ Vector((0, 0, 1.38))
    if entry:
        # A gold urn of flowers.
        planter.lathe([(0, 0), (.13, 0), (.16, .06), (.11, .14), (.12, .2), (.26, .38), (.3, .48), (.27, .52), (0, .52)],
                      Matrix.Translation(top), M["gold"], 24)
        c = top + Vector((0, 0, .55))
        flowers.sphere(c, .3, M["leaf"], (1, 1, .7), 2)
        for _ in range(10):
            t2 = random.uniform(0, 2 * math.pi)
            d = random.uniform(.05, .26)
            flowers.sphere(c + Vector((d * math.cos(t2), d * math.sin(t2), .12 + random.uniform(0, .1) - d * .3)),
                           random.uniform(.05, .075), random.choice(FLOWERS), (1, 1, .7), 1)
    else:
        planter.lathe([(0, 0), (.1, 0), (.06, .06), (0, .06)], Matrix.Translation(top), M["gold"], 16)
        planter.sphere(top + Vector((0, 0, .24)), .19, M["gold"], subdiv=3)


for a in POSTS:
    post(a, a in ENTRY_POSTS)
planter.done()
flowers.done()

# ------------------------------------------------------------------ the start yard's balustrades and banners
rail = Mesh("Start yard balustrade")
banner = Mesh("Start yard banners")
for side in (-1, 1):
    xc = 6.15 * side
    ys = [-61.0, -57.73, -54.47, -51.2]
    rail.box((xc - .2, -61.0, 0), (xc + .2, -51.2, .2), M["stone2"], .03)
    rail.box((xc - .2, -61.0, .86), (xc + .2, -51.2, .98), M["stone"], .03)
    rail.box((xc - .23, -61.0, .98), (xc + .23, -51.2, 1.06), M["stone3"], .03)
    for ya, yb in zip(ys, ys[1:]):
        n = int((yb - ya - .5) / .3)
        for i in range(n):
            y = ya + .25 + (yb - ya - .5) * (i + .5) / n
            rail.lathe([(.07, .2), (.08, .24), (.05, .3), (.06, .38), (.11, .52), (.09, .66), (.05, .74), (.06, .8), (.09, .86)],
                       Matrix.Translation((xc, y, 0)), M["stone"], 12)
    for y in ys:
        rail.box((xc - .3, y - .3, 0), (xc + .3, y + .3, .22), M["stone2"], .04)
        rail.box((xc - .24, y - .24, .22), (xc + .24, y + .24, 1.2), M["stone"], .04)
        rail.box((xc - .3, y - .3, 1.2), (xc + .3, y + .3, 1.3), M["stone3"], .04)
        rail.lathe([(0, 1.3), (.09, 1.3), (.05, 1.37), (0, 1.37)], Matrix.Translation((xc, y, 0)), M["gold"], 16)
        rail.sphere((xc, y, 1.52), .16, M["gold"], subdiv=3)
    # A royal blue banner with a gold crown on the post at the section join, facing south.
    bx, by = xc, -61.32
    banner.box((bx - .34, by - .02, -.25), (bx + .34, by + .02, 1.18), M["blue"])
    # Swallowtail: two gold-edged points.
    for s in (-1, 1):
        banner.box((bx + (.04 if s > 0 else -.34), by - .02, -.55), (bx + (.34 if s > 0 else -.04), by + .02, -.25), M["blue"])
    banner.box((bx - .38, by - .05, 1.12), (bx + .38, by + .05, 1.2), M["gold"], .01)
    banner.box((bx - .34, by - .035, .98), (bx + .34, by - .02, 1.03), M["gold"])
    banner.box((bx - .34, by - .035, -.22), (bx + .34, by - .02, -.17), M["gold"])
    # The crown: a band and three points with balls.
    banner.box((bx - .2, by - .05, .3), (bx + .2, by - .02, .4), M["gold"])
    for k in (-1, 0, 1):
        banner.box((bx + k * .15 - .05, by - .05, .4), (bx + k * .15 + .05, by - .02, .62 + (.06 if k == 0 else 0)), M["gold"])
        banner.sphere((bx + k * .15, by - .04, .66 + (.06 if k == 0 else 0)), .045, M["gold"], subdiv=1)
rail.done()
banner.done()

# ------------------------------------------------------------------ the spin bars: wooden logs, gold caps and bands, stone hubs
for bi, (x, z, reach, dps, phase, tall) in enumerate(BARS):
    bar = Mesh("Spin bar %d" % (bi + 1))
    hub_r = .7 if tall else .5
    hub_h = 1.55 if tall else .9
    base = Matrix.Translation((x, z, 0))
    bar.lathe([(0, 0), (hub_r + .12, 0), (hub_r + .12, .14), (hub_r, .18), (hub_r, hub_h - .12), (hub_r + .05, hub_h - .1),
               (hub_r + .05, hub_h), (0, hub_h)], base, M["stone"], 32, False)
    for zb in (.2, hub_h * .55):
        bar.lathe([(hub_r + .02, zb), (hub_r + .035, zb + .02), (hub_r + .035, zb + .07), (hub_r + .02, zb + .09)], base, M["gold"], 32, True, True)
    bar.lathe([(hub_r - .02, hub_h), (hub_r * .9, hub_h + .12), (hub_r * .55, hub_h + .26), (hub_r * .25, hub_h + .32), (0, hub_h + .33)],
              base, M["gold"], 32)
    bar.sphere((x, z, hub_h + .45), .13 if not tall else .2, M["gold"], subdiv=3)
    arms = 3 if tall else 2
    logs = [(.36, .25), (.94, .25)] if tall else [(.40, .155)]
    turn = random.uniform(0, 360)
    for k in range(arms):
        t = math.radians(turn + k * 360 / arms)
        rot = base @ Matrix.Rotation(t, 4, "Z")
        for lz, lr in logs:
            along = Matrix.Rotation(math.pi / 2, 4, "Y")  # lathe axis -> +x
            start = .3
            m = rot @ Matrix.Translation((0, 0, lz)) @ along
            # Lathe along x: z of the profile is the distance from the hub.
            bar.lathe([(0, start), (lr, start), (lr, reach - .14), (lr * .92, reach - .12), (0, reach - .12)], m, M["wood"], 16)
            bar.lathe([(lr + .03, reach - .16), (lr + .045, reach - .14), (lr + .045, reach - .02), (lr * .7, reach + .02),
                       (0, reach + .03)], m, M["gold"], 16)
            for f in (.42, .72):
                d = start + (reach - start) * f
                bar.lathe([(lr + .012, d), (lr + .022, d + .015), (lr + .022, d + .06), (lr + .012, d + .075)], m, M["gold"], 16, True, True)
        if tall:
            for f in (.25, .6, .9):
                d = .3 + (reach - .3) * f
                v = bar.box((d - .05, -.27, .1), (d + .05, .27, 1.2), M["gold"], .015)
                for vv in v:
                    vv.co = rot @ vv.co
    bar.done()

# ------------------------------------------------------------------ fountain jets from the planters and lily pads
jets = Mesh("Plaza fountain jets")
for a in (0, 44, -44, 136, 180, 224):
    p0 = Vector(plaza_pt(a, KERB_OUT + .02, .45))
    out = Vector(plaza_pt(a, 1.0)) - Vector(plaza_pt(a, 0.0))
    jets.lathe([(0, 0), (.16, 0), (.13, .12), (0, .12)],
               Matrix.Translation(p0) @ Matrix.Rotation(math.radians(a), 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "Y"),
               M["gold"], 16)
    pts = []
    for i in range(16):
        s = i / 15
        pts.append(p0 + out * (2.4 * s) + Vector((0, 0, 1.1 * s - 2.25 * s * s)))
    for p, q in zip(pts, pts[1:]):
        d = q - p
        mtx = Matrix.Translation(p) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        jets.lathe([(.07, 0), (.075, d.length)], mtx, M["jet"], 10)
jets.done()

pads = Mesh("Plaza lily pads")
for i in range(34):
    a = random.uniform(0, 360)
    if not ang_ok(a, [(90, 30), (270, 28)]):
        continue
    rr = random.uniform(18.4, 25)
    x, y, _ = plaza_pt(a, rr)
    s = random.uniform(.45, .85)
    notch = random.uniform(0, 360)
    prof = [(0, WATER + .01), (s, WATER + .01), (s, WATER + .035), (0, WATER + .035)]
    v = pads.lathe(prof, Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(notch), 4, "Z"), M["lily"], 18, False)
    if random.random() < .35:
        pads.sphere((x, y, WATER + .1), .11, M["pink"], (1, 1, .7), 1)
pads.done()

# ------------------------------------------------------------------ scale pawns (about 0.75 m) and the camera
pawns = Mesh("Scale pawns", SCALE)
for x, y in ((-2.5, -46), (3, -38), (-4.5, -31), (0, -55), (5.5, -27)):
    pawns.lathe([(0, 0), (.24, 0), (.24, .08), (.16, .12), (.1, .36), (.15, .42), (.08, .46), (0, .46)],
                Matrix.Translation((x, y, 0)), M["pawn"], 24)
    pawns.sphere((x, y, .58), .15, M["pawn"], subdiv=3)
pawns.done()

world = bpy.data.worlds.new("Sky")
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (*srgb((.62, .80, 1.0)), 1)
bg.inputs["Strength"].default_value = 1.1
sc.world = world
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.6
sun.data.color = (1.0, .95, .86)
sun.data.angle = math.radians(3)
sun.rotation_euler = (math.radians(42), 0, math.radians(-35))
sc.collection.objects.link(sun)

sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1600, 900
try:
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
except Exception:
    pass
try:
    sc.eevee.taa_render_samples = 48
except Exception:
    pass

cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
sc.collection.objects.link(cam)
sc.camera = cam


def shoot(name, at, look, lens):
    cam.location = Vector(at)
    cam.rotation_euler = (Vector(look) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    cam.data.clip_end = 400
    sc.render.filepath = os.path.join(HERE, "render_plaza_v1_%s.png" % name)
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "PlazaSample_v1.blend"))
shoot("overview", (0, -71, 19), (0, -31, -2), 28)
shoot("player", (0, -60, 2.4), (0, -35, .6), 26)
shoot("bars", (-14, -50, 5), (-3, -38, 0), 30)
print("PLAZA SAMPLE DONE")
