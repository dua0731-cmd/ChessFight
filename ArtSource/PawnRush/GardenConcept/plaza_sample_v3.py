# Royal water garden - concept-art dressing, sample 1 v3: the fountain plaza (beat 1) of the v31 layout (R408, R409, R415).
# v3 (승규 11-05, two reference renders: "회전과 돌이랑 꽃을 더 디테일하게 저 텍스쳐로 해줘 완전 똑같이"):
#   spin bars as the reference - an octagonal cream stone hub (12-sided on the three-arm bar) on a two-tier gold plinth,
#   a gold sprig-of-leaves emblem on its free faces, a gold roof and knob; wood-grained logs with riveted gold bands, a
#   riveted gold collar where they enter the hub and a flat gold end cap;
#   stone as the reference - the planters built of separate pillowy blocks in staggered courses under a coping, panelled
#   pillars with a stepped cap, a gold dome or a gold urn; all stone in a mottled cream texture;
#   flowers as the reference - five-petal flowers with a yellow heart on bushes of pointed leaves, leafy topiary balls.
# v2 (승규 "모양 더 다듬기"): floor inlay bands and a star round the middle hub, tall entrance pillars with banners and
# big urns, thicker logs with ball-ended caps (the tall bar's pair joined by gold end plates), a plinth and a moulding on
# the planters, thin see-through jets that reach the water and splash.
#
# 승규 11-04: "배치는 지금 대로 하고 컨셉 아트만 저렇게 입힐 거야" -> the v31 graybox (RoyalWaterGardenBuilder) keeps every
# position and collider; this file only builds what is drawn over it, in the concept art's look: pink-beige herringbone
# paving, cream limestone, gold and royal blue, flower planters, wooden spin bars with gold caps.
#
# Frame: Blender X = section x (east), Blender Y = section z (north), Z = up; 1 unit = 1 m; a floor's top is z 0.
# The blockout numbers come from RoyalWaterGardenBuilder.Beats.cs (FountainPlaza, SpinBar).
#
# Run:  blender -b --python plaza_sample_v3.py
# Out:  PlazaSample_v3.blend, textures/Herringbone*.png, render_plaza_v3_*.png (next to this file)

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


# ------------------------------------------------------------------ v3: the stone and wood textures
def save_image(fname, rgb):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(fname, w, h, alpha=False)
    rgba = np.concatenate([rgb, np.ones((h, w, 1))], axis=2).astype(np.float32)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(TEX, fname + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def periodic_noise(h, w, seed, falloff=1.2, lo=2.0, hi=None, aniso=(1.0, 1.0)):
    """Seamless noise (zero mean, unit spread) from a random spectrum."""
    rng = np.random.default_rng(seed)
    fy = np.fft.fftfreq(h)[:, None] * h * aniso[1]
    fx = np.fft.fftfreq(w)[None, :] * w * aniso[0]
    f = np.sqrt(fx ** 2 + fy ** 2)
    amp = np.where(f >= lo, 1.0 / np.maximum(f, 1) ** falloff, 0.0)
    if hi:
        amp = np.where(f <= hi, amp, 0.0)
    n = np.real(np.fft.ifft2(amp * np.exp(2j * np.pi * rng.random((h, w)))))
    return (n - n.mean()) / (n.std() + 1e-9)


def stone_texture():
    """2 m of soft mottled sandstone, near white: the material's tint gives it its cream."""
    n = 1024
    v = .95 + .022 * periodic_noise(n, n, 5, 1.6, 2) + .012 * periodic_noise(n, n, 6, .5, 60)
    pits = np.random.default_rng(7).random((n, n))
    v = np.where(pits < .002, v - .05, v)
    v = np.where(pits > .9985, v + .04, v)
    v = np.clip(v, 0, 1)
    return save_image("Stone_Cream", np.stack([v, v * .992, v * .975], axis=2))


def wood_texture():
    """1.5 m of a log (v along it, u once round it): warm brown grain running along it."""
    h, w = 1024, 256
    streaks = periodic_noise(h, w, 9, 1.0, 2, None, (1.0, 6.0))      # long along v, fine across u
    knots = periodic_noise(h, w, 10, 1.8, 1)
    fine = periodic_noise(h, w, 11, .3, 30, None, (1.0, 8.0))
    t = np.clip(.52 + .2 * streaks + .08 * knots + .07 * fine, 0, 1)
    light, dark = np.array([.70, .43, .22]), np.array([.40, .21, .10])
    rgb = dark * (1 - t[..., None]) + light * t[..., None]
    lines = np.abs(periodic_noise(h, w, 12, .8, 8, None, (1.0, 10.0))) < .06   # thin dark grain lines
    rgb[lines] *= .78
    return save_image("Wood_Grain", np.clip(rgb, 0, 1))


STONE_TEX = stone_texture()
WOOD_TEX = wood_texture()


def textured(name, image, tint, rough, tiling=1.0, metal=0.):
    """A material that multiplies `image` (repeated every 1/tiling UV units) by `tint`; the export reads the gc_ keys."""
    m = material(name, tint, rough, metal)
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (tiling, tiling, 1)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    mul = nt.nodes.new("ShaderNodeVectorMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = srgb(tint)
    nt.links.new(coord.outputs["UV"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], mul.inputs[0])
    nt.links.new(mul.outputs["Vector"], p.inputs["Base Color"])
    m["gc_texture"] = image.name + ".png"
    m["gc_tint"] = list(tint)
    m["gc_tiling"] = tiling
    return m


M = {
    # Stone: box-mapped UVs in metres, the texture every 2 m.
    "stone": textured("GC | cream stone", STONE_TEX, (.95, .83, .67), .72, .5),
    "stone2": textured("GC | cream stone warm", STONE_TEX, (.92, .78, .62), .75, .5),
    "stone3": textured("GC | cream stone pale", STONE_TEX, (.97, .89, .76), .7, .5),
    "gold": material("GC | gold", (1.0, .70, .16), .26, .8),
    # Wood: the logs' own UVs (once round, 1.5 m along).
    "wood": textured("GC | bar wood", WOOD_TEX, (1.0, 1.0, 1.0), .55),
    "emblem": material("GC | emblem leaf", (.36, .55, .14), .45),
    "heart": material("GC | flower heart", (1.0, .74, .12), .55),
    "soil": material("GC | planter soil", (.30, .19, .10), .9),
    "leaf": material("GC | leaf", (.30, .62, .16), .7),
    "leaf2": material("GC | leaf light", (.45, .74, .22), .7),
    "leaf3": material("GC | leaf dark", (.18, .46, .12), .7),
    "pink": material("GC | flower pink", (1.0, .48, .68), .6),
    "yellow": material("GC | flower yellow", (1.0, .86, .22), .6),
    "white": material("GC | flower white", (.98, .97, .93), .6),
    "coral": material("GC | flower coral", (1.0, .55, .40), .6),
    "purple": material("GC | flower purple", (.70, .50, .92), .6),
    "blue": material("GC | royal blue", (.10, .24, .66), .6),
    "jet": material("GC | fountain jet", (.82, .95, 1.0), .05, 0, .35),
    "foam": material("GC | foam", (.97, .99, 1.0), .4, 0, .15),
    "lily": material("GC | lily pad", (.30, .62, .22), .55),
    "clay": material("Context | clay", (.55, .55, .55), .8),
    "pawn": material("Context | pawn white", (.95, .95, .95), .5),
    "pawn_dark": material("Context | pawn black", (.06, .06, .07), .5),
}
STONES = [M["stone"], M["stone2"], M["stone3"]]
GOLD = M["gold"]
# The jets are see-through.
_jet = M["jet"].node_tree.nodes["Principled BSDF"]
_jet.inputs["Alpha"].default_value = .55
for attr, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
    try:
        setattr(M["jet"], attr, value)
    except Exception:
        pass
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
    m["gc_texture"] = "Herringbone_PinkBeige.png"
    m["gc_tint"] = [1.0, 1.0, 1.0]
    m["gc_tiling"] = 1.0
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
        self.fixed = {}  # face -> {vert: uv}, for faces with their own UVs (the logs)
        self.no_recalc = set()  # open one-sided faces (leaves, petals) keep the side they were made facing

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

    def lathe(self, profile, mtx, mat, segs=24, smooth=True, loop=False, uv_len=None):
        """profile: (radius, z) from bottom to top; radius 0 at an end closes it. uv_len: the side gets UVs u = once
        round, v = z / uv_len (a log's wood grain)."""
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
                    f = self.bm.faces.new((a[i], b[i], b[j], a[j]))
                    if uv_len:
                        za, zb = profile[rings.index(a)][1], profile[rings.index(b)][1]
                        self.fixed[f] = {a[i]: (i / segs, za / uv_len), b[i]: (i / segs, zb / uv_len),
                                         b[j]: ((i + 1) / segs, zb / uv_len), a[j]: ((i + 1) / segs, za / uv_len)}
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
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in self.no_recalc])
        for f in bm.faces:
            own = self.fixed.get(f)
            for loop in f.loops:
                co = loop.vert.co
                if own and loop.vert in own:
                    loop[self.uv].uv = own[loop.vert]
                elif planar_uv:
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


    # ---------------------------------------------------------------- v3 pieces
    def slab(self, outline, frame, z0, z1, mat):
        """A flat solid: the 2D `outline` (x, y) between local heights z0..z1, placed by `frame` (closed, so the
        normals come out right)."""
        lo = [self.bm.verts.new(frame @ Vector((x, y, z0))) for x, y in outline]
        hi = [self.bm.verts.new(frame @ Vector((x, y, z1))) for x, y in outline]
        self.bm.faces.new(list(reversed(lo)))
        self.bm.faces.new(hi)
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        return self.tag(lo + hi, mat)

    def leaf(self, base, direction, length, width, mat, up=Vector((0, 0, 1))):
        """A pointed leaf from `base` along `direction`, folded along its midrib, its face toward `up`."""
        d = Vector(direction).normalized()
        side = d.cross(Vector(up))
        if side.length < 1e-4:
            side = d.orthogonal()
        side.normalize()
        normal = side.cross(d).normalized()
        b = Vector(base)
        mid = b + d * length * .45 + normal * width * .22
        tip = b + d * length
        left = b + d * length * .4 - side * width * .5 - normal * width * .05
        right = b + d * length * .4 + side * width * .5 - normal * width * .05
        vs = [self.bm.verts.new(p) for p in (b, left, mid, tip, right)]
        bb, ll, mm, tt, rr = vs
        for f in ((bb, ll, mm), (mm, ll, tt), (bb, mm, rr), (mm, tt, rr)):
            face = self.bm.faces.new(f)
            face.normal_update()
            if face.normal.dot(normal) < 0:
                face.normal_flip()
        i = self.mi(mat)
        for f in {f for v in vs for f in v.link_faces}:
            f.material_index = i
            f.smooth = True
        self.no_recalc.update({f for v in vs for f in v.link_faces})
        return vs

    def flower(self, centre, size, mat, tilt=Vector((0, 0, 1)), heart=None):
        """Five rounded petals round a yellow heart, facing `tilt`."""
        z = Vector(tilt).normalized()
        frame = Matrix.Translation(Vector(centre)) @ z.to_track_quat("Z", "Y").to_matrix().to_4x4() \
            @ Matrix.Rotation(random.uniform(0, 2 * math.pi), 4, "Z")
        for k in range(5):
            a = 2 * math.pi * k / 5
            pts = []
            for t, w in ((0, 0), (.35, .42), (.75, .48), (.95, .3), (1.0, 0), (.95, -.3), (.75, -.48), (.35, -.42)):
                pts.append((size * t, size * w * .9))
            rot = Matrix.Rotation(a, 4, "Z")
            vs = [self.bm.verts.new(frame @ rot @ Vector((x, y, .12 * size * (x / size) ** 2))) for x, y in pts]
            face = self.bm.faces.new(vs)
            face.normal_update()
            if face.normal.dot(frame.to_3x3() @ Vector((0, 0, 1))) < 0:
                face.normal_flip()
            self.no_recalc.add(face)
            i = self.mi(mat)
            face.material_index = i
            face.smooth = True
        self.lathe([(0, 0), (size * .3, 0), (size * .26, size * .12), (size * .12, size * .2), (0, size * .22)],
                   frame, heart or M["heart"], 8)

    def bush(self, centre, radius, leaves, mats, squash=1.0, core_subdiv=1):
        """A soft round mound (the reference's bushes): a smooth green core with broad leaves lying over it."""
        c = Vector(centre)
        self.sphere(c, radius * .86, mats[0], (1, 1, squash), core_subdiv)
        golden = math.pi * (3 - math.sqrt(5))
        for k in range(leaves):
            zf = 1 - (k + .5) / leaves * 1.4  # mostly the upper part
            r = math.sqrt(max(0.0, 1 - zf * zf))
            th = golden * k
            out = Vector((r * math.cos(th), r * math.sin(th), zf * squash)).normalized()
            base = c + Vector((out.x * radius * .72, out.y * radius * .72, out.z * radius * .72 * squash))
            # Leaning over the surface, as overlapping scales, a little outward.
            side = Vector((0, 0, 1)).cross(out)
            if side.length < 1e-3:
                side = Vector((1, 0, 0))
            down = side.cross(out).normalized()
            direction = (out * .55 + down * random.uniform(-.6, .9) + side.normalized() * random.uniform(-.5, .5)).normalized()
            length = radius * random.uniform(.42, .52)
            self.leaf(base, direction, length, length * random.uniform(.72, .85), random.choice(mats), up=out)


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
# v2: two cream inlay bands across the herringbone and an eight-point star round the middle hub, all flush (6 mm up).
for r0, r1 in ((7.55, 8.0), (11.55, 12.0)):
    for i in range(48):
        border.arc(r0, r1, i * 7.5 + .06, (i + 1) * 7.5 - .06, -.02, .006, random.choice(STONES[:2]), segs=3)
star = []
for i in range(16):
    a = math.radians(i * 22.5 + 11.25)
    rr = 4.4 if i % 2 == 0 else 2.75
    star.append(border.bm.verts.new((PLAZA[0] + rr * math.cos(a), PLAZA[1] + rr * math.sin(a), .006)))
centre = border.bm.verts.new((PLAZA[0], PLAZA[1], .006))
for i in range(16):
    border.bm.faces.new((centre, star[i], star[(i + 1) % 16]))
border.tag(star + [centre], M["stone3"])
for i in range(16):
    a, b = star[i].co, star[(i + 1) % 16].co
    mid, d = (a + b) / 2, (b - a)
    v = border.box((-d.length / 2, -.05, -.02), (d.length / 2, .05, .009), M["gold"])
    rot = Matrix.Translation(mid) @ Matrix.Rotation(math.atan2(d.y, d.x), 4, "Z")
    for vv in v:
        vv.co = rot @ Vector((vv.co.x, vv.co.y, vv.co.z))
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
        wall.arc(R_PLAZA - .05, R_PLAZA + .13, a0 + .12, a1 - .12, z0 + .03, z1 - .03, random.choice(STONES), bevel=.06, segs=4)
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


BLOCK_GAP = .014


def block_course(r0, r1, a0, a1, z0, z1, length, mats, bevel, stagger=0.):
    """A course of separate pillowy blocks along the arc a0..a1, each about `length` long, joints offset by `stagger`."""
    rm = (r0 + r1) / 2
    span = math.radians(a1 - a0) * rm
    n = max(1, round(span / length))
    step = (a1 - a0) / n
    gap = math.degrees(BLOCK_GAP / rm) / 2
    edges = [a0] + [a0 + step * (k + stagger) for k in range(n + 1) if a0 < a0 + step * (k + stagger) < a1] + [a1]
    edges = sorted(set(round(e, 6) for e in edges))
    for s, e in zip(edges, edges[1:]):
        if e - s < step * .2:
            continue
        planter.arc(r0, r1, s + gap, e - gap, z0, z1, random.choice(mats), bevel=bevel, segs=max(1, int((e - s) / 2.5)))


def planter_run(a0, a1):
    # v3, as the reference: two staggered courses of rounded blocks and a coping of long flat ones, both walls.
    for r0, r1 in ((KERB_IN, KERB_IN + .2), (BED_OUT - .2, BED_OUT)):
        block_course(r0, r1, a0, a1, 0, .36, .55, STONES[:2], .045, 0)
        block_course(r0, r1, a0, a1, .36, .72, .55, STONES[:2], .045, .5)
        block_course(r0 - .035, r1 + .035, a0, a1, .72, .86, .75, [M["stone3"]], .05, .25)
    planter.arc(KERB_IN + .2, BED_OUT - .2, a0, a1, 0, .68, M["soil"])
    # Bushes of pointed leaves, flowers on them, a leafy topiary ball every ~4 m.
    length = math.radians(a1 - a0) * 16.88
    count = int(length / .3)
    tops = max(1, int(length / 4.2))
    top_at = [a0 + (a1 - a0) * (j + .5) / tops for j in range(tops)]
    for i in range(count):
        a = a0 + (a1 - a0) * (i + .5) / count
        if any(abs(a - t) < math.degrees(.55 / 16.9) for t in top_at):
            continue
        row = 16.88 + (.09 if i % 2 else -.09) + random.uniform(-.03, .03)
        x, y, _ = plaza_pt(a, row)
        rad = random.uniform(.2, .26)
        c = Vector((x, y, .72 + rad * .4))
        flowers.bush(c, rad, 16, [M["leaf2"], M["leaf"], M["leaf2"]], .8)
        bloom(c, rad, .8, random.randint(5, 6), .1, .13)
    for t in top_at:
        x, y, _ = plaza_pt(t, 16.88)
        flowers.bush(Vector((x, y, 1.14)), .44, 150, [M["leaf"], M["leaf2"], M["leaf3"]], 1.0, 2)


def bloom(c, rad, squash, count, small, big):
    """Flowers sitting on a mound's upper half, facing out of it, a little above its leaves."""
    for _ in range(count):
        t = random.uniform(0, 2 * math.pi)
        up = random.uniform(.45, 1.0)
        out = Vector((math.cos(t) * math.sqrt(1 - up * up), math.sin(t) * math.sqrt(1 - up * up), up))
        p = c + Vector((out.x * rad * .95, out.y * rad * .95, out.z * rad * .95 * squash)) + out * .02
        flowers.flower(p, random.uniform(small, big), random.choice(FLOWERS[:4]), out + Vector((0, 0, .4)))


runs = [(-66, -22), (-22, 22), (22, 66), (114, 158), (158, 202), (202, 246)]
for a0, a1 in runs:
    off = math.degrees(.38 / 16.7)
    planter_run(a0 + off, a1 - off)


def banner_at(m, bx, by, top, f):
    """A royal blue swallowtail banner 1.43 m long hanging from `top`, its gold crown on the side facing f (-1 south, +1 north)."""
    z0 = top - 1.43

    def piece(x0, x1, d0, d1, za, zb, mat, bev=0.):
        y0, y1 = sorted((by + f * d0, by + f * d1))
        m.box((bx + x0, y0, za), (bx + x1, y1, zb), mat, bev)

    piece(-.34, .34, -.02, .02, z0, top, M["blue"])
    piece(-.34, -.04, -.02, .02, z0 - .3, z0, M["blue"])
    piece(.04, .34, -.02, .02, z0 - .3, z0, M["blue"])
    piece(-.38, .38, -.05, .05, top - .06, top + .02, M["gold"], .01)
    piece(-.34, .34, .02, .035, top - .2, top - .15, M["gold"])
    piece(-.34, .34, .02, .035, z0 + .03, z0 + .08, M["gold"])
    piece(-.2, .2, .02, .05, z0 + .55, z0 + .65, M["gold"])
    for k in (-1, 0, 1):
        tip = .06 if k == 0 else 0
        piece(k * .15 - .05, k * .15 + .05, .02, .05, z0 + .65, z0 + .87 + tip, M["gold"])
        m.sphere((bx + k * .15, by + f * .04, z0 + .91 + tip), .045, M["gold"], subdiv=1)


def post(a, entry):
    x, y, _ = plaza_pt(a, 16.7)
    t = math.radians(a)
    mtx = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(t, 4, "Z")

    def b(lo, hi, mat, bev):
        v = planter.box(lo, hi, mat, bev)
        for vv in v:
            vv.co = mtx @ vv.co

    # v3, as the reference: a plinth, a shaft with a raised panel frame on each face, a stepped cap, then a gold
    # plate and a gold dome - or, at the openings (tall, with a banner), a gold urn of flowers.
    h = 1.95 if entry else 1.22
    b((-.43, -.43, 0), (.43, .43, .24), M["stone2"], .05)
    b((-.34, -.34, .24), (.34, .34, h), M["stone"], .04)
    top_panel = h - .16
    for ang in range(4):
        rot = Matrix.Rotation(math.radians(90 * ang), 4, "Z")
        for lo, hi in (((.34, -.24, .36), (.37, .24, .41)), ((.34, -.24, top_panel - .05), (.37, .24, top_panel)),
                       ((.34, -.24, .41), (.37, -.19, top_panel - .05)), ((.34, .19, .41), (.37, .24, top_panel - .05))):
            v = planter.box(lo, hi, M["stone3"], .012)
            for vv in v:
                vv.co = mtx @ rot @ vv.co
    b((-.4, -.4, h), (.4, .4, h + .08), M["stone3"], .03)
    b((-.45, -.45, h + .08), (.45, .45, h + .17), M["stone3"], .04)
    b((-.36, -.36, h + .17), (.36, .36, h + .21), GOLD, .012)
    top = mtx @ Vector((0, 0, h + .21))
    if entry:
        s = 1.3
        planter.lathe([(0, 0), (.12 * s, 0), (.13 * s, .03 * s), (.07 * s, .07 * s), (.07 * s, .12 * s), (.16 * s, .16 * s),
                       (.3 * s, .26 * s), (.36 * s, .34 * s), (.38 * s, .38 * s), (.34 * s, .4 * s), (0, .4 * s)],
                      Matrix.Translation(top), GOLD, 32)
        c = top + Vector((0, 0, .44 * s))
        flowers.bush(c, .36, 30, [M["leaf2"], M["leaf"]], .7)
        bloom(c, .36, .7, 12, .1, .13)
        x, y, _ = plaza_pt(a, 16.7)
        f = -1 if y < PLAZA[1] else 1
        banner_at(planter, x, y + f * .5, h - .02, f)
    else:
        planter.lathe([(0, 0), (.24, 0), (.24, .02), (0, .02)], Matrix.Translation(top), GOLD, 24, False)
        planter.lathe([(0, .02), (.21, .02), (.207, .08), (.18, .17), (.126, .24), (.056, .27), (0, .273)],
                      Matrix.Translation(top), GOLD, 24)
        planter.sphere(top + Vector((0, 0, .31)), .045, GOLD, subdiv=2)


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
    banner_at(banner, xc, -61.32, 1.18, -1)
rail.done()
banner.done()

# ------------------------------------------------------------------ v3: the spin bars as the reference render
GOLD = M["gold"]


def rivets(bar, frame, radius, along, count=8, size=.02):
    """Little gold domes round a log (the log frame's z is along it) at `along`."""
    for k in range(count):
        t = 2 * math.pi * (k + .5) / count
        out = Vector((math.cos(t), math.sin(t), 0))
        at = frame @ Matrix.Translation(out * radius + Vector((0, 0, along))) @ out.to_track_quat("Z", "Y").to_matrix().to_4x4()
        bar.lathe([(0, 0), (size, 0), (size * .8, size * .5), (0, size * .75)], at, GOLD, 6)


def emblem(bar, frame, scale):
    """The reference's sprig: a gold stem and three green leaves edged in gold (frame: z out of the face, y up)."""
    def outline(length, width):
        pts = [(0, 0)]
        for k in range(1, 8):
            t = k / 8
            pts.append((-width * math.sin(math.pi * t) ** .8, length * t))
        pts.append((0, length))
        for k in range(7, 0, -1):
            t = k / 8
            pts.append((width * math.sin(math.pi * t) ** .8, length * t))
        return pts

    s = scale
    bar.slab([(-.008 * s, -.07 * s), (.008 * s, -.07 * s), (.008 * s, .1 * s), (-.008 * s, .1 * s)], frame, 0, .012 * s, GOLD)
    for ang, size, y in ((0, 1.0, .02), (42, .8, -.05), (-42, .8, -.05)):
        place = frame @ Matrix.Translation((0, y * s, 0)) @ Matrix.Rotation(math.radians(ang), 4, "Z")
        bar.slab(outline(.17 * s * size, .06 * s * size), place, 0, .014 * s, GOLD)
        inner = place @ Matrix.Translation((0, .012 * s * size, 0))
        bar.slab(outline(.14 * s * size, .045 * s * size), inner, .014 * s, .02 * s, M["emblem"])


def hub(bar, at, R, sides, body0, body1, emblem_faces, emblem_z):
    """A gold two-tier plinth, a stone drum of `sides` faces (one facing +x), a stone ledge, a gold roof and knob.
    R is the drum's corner radius; returns the radius to its faces."""
    base = Matrix.Translation(at) @ Matrix.Rotation(math.pi / sides, 4, "Z")
    k = R / .5
    bar.lathe([(0, 0), (R + .17 * k, 0), (R + .17 * k, .045), (R + .14 * k, .075), (0, .075)], base, GOLD, sides, False)
    bar.lathe([(0, .075), (R + .1 * k, .075), (R + .1 * k, body0 - .02), (R + .06 * k, body0), (0, body0)], base, GOLD, sides, False)
    bar.lathe([(0, body0), (R - .02, body0), (R, body0 + .025), (R, body1 - .02), (R - .02, body1), (0, body1)], base,
              M["stone2"], sides, False)
    bar.lathe([(0, body1), (R + .05 * k, body1), (R + .06 * k, body1 + .03), (R + .06 * k, body1 + .07),
               (R + .03 * k, body1 + .09), (0, body1 + .09)], base, M["stone3"], sides, False)
    top = body1 + .09
    bar.lathe([(0, top), (R + .03 * k, top), (R + .03 * k, top + .05 * k), (R * .6, top + .17 * k), (R * .32, top + .22 * k),
               (0, top + .22 * k)], base, GOLD, sides, False)
    bar.lathe([(0, top + .2 * k), (.075 * k, top + .2 * k), (.06 * k, top + .27 * k), (0, top + .27 * k)],
              Matrix.Translation(at), GOLD, 12, False)
    bar.sphere(Vector(at) + Vector((0, 0, top + .35 * k)), .09 * k, GOLD, subdiv=2)
    face_r = R * math.cos(math.pi / sides)
    for deg in emblem_faces:
        frame = Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, "Z") @ Matrix.Translation((face_r, 0, emblem_z)) \
            @ Matrix(((0, 0, 1), (1, 0, 0), (0, 1, 0))).to_4x4()
        emblem(bar, frame, k)
    return face_r


def log(bar, frame, lr, start, end, face_r):
    """A wood-grained log from `start` to `end` along the frame's z: a riveted gold collar at the hub, two riveted
    bands, a flat gold end cap."""
    bar.lathe([(0, start), (lr, start), (lr, end), (0, end)], frame, M["wood"], 16, True, False, 1.5)
    c0 = face_r - .02
    # Wide bands as the reference's (about half the log's width), each with a row of rivets.
    w = lr * 1.0
    bar.lathe([(lr + .05, c0), (lr + .07, c0 + .02), (lr + .07, c0 + w - .02), (lr + .05, c0 + w)], frame, GOLD, 20, True, True)
    rivets(bar, frame, lr + .07, c0 + w / 2, 8, .026)
    for f in (.36, .68):
        d = start + (end - start) * f
        bar.lathe([(lr + .02, d), (lr + .04, d + .018), (lr + .04, d + w - .018), (lr + .02, d + w)], frame, GOLD, 20, True, True)
        rivets(bar, frame, lr + .04, d + w / 2, 8, .024)
    # A flat cap, wider than the log, with a raised rim.
    cap = lr * .8
    bar.lathe([(0, end - cap), (lr + .02, end - cap), (lr + .075, end - cap + .03), (lr + .075, end - .025), (lr + .05, end),
               (lr * .8, end), (lr * .72, end - .018), (0, end - .018)], frame, GOLD, 24, True)


for bi, (x, z, reach, dps, phase, tall) in enumerate(BARS):
    bar = Mesh("Spin bar %d" % (bi + 1))
    at = Vector((x, z, 0))
    arms = 3 if tall else 2
    # Arm 0 along +x, as the blockout's at rest: the export hangs each bar on its mover before it turns.
    arm_deg = [k * 360 / arms for k in range(arms)]
    if tall:
        # Twelve faces so each of the three arms meets one; the sprigs on the faces between them.
        face_r = hub(bar, at, .66, 12, .13, 1.5, [60, 180, 300], .65)
        logs = [(.36, .25), (.94, .25)]
    else:
        face_r = hub(bar, at, .44, 8, .13, .92, [45, 90, 135, 225, 270, 315], .56)
        # 2 cm fatter than its 0.35 x 0.3 collider, as the reference's chunky log.
        logs = [(.40, .175)]
    end = reach - .02  # the drawn end stays inside the collider's reach
    for deg in arm_deg:
        rot = Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, "Z")
        for lz, lr in logs:
            frame = rot @ Matrix.Translation((0, 0, lz)) @ Matrix.Rotation(math.pi / 2, 4, "Y")
            log(bar, frame, lr, .2, end, face_r)
        if tall:
            # A small gold spacer between the pair at each band (v3: no straps round them).
            for f in (.36, .7):
                d = .3 + (end - .3) * f
                v = bar.box((d + .02, -.09, .56), (d + .14, .09, .74), GOLD, .02)
                for vv in v:
                    vv.co = rot @ vv.co
    bar.done()

# ------------------------------------------------------------------ fountain jets from the planters and lily pads
jets = Mesh("Plaza fountain jets")
for a in (0, 44, -44, 136, 180, 224):
    # v2: from the bed's outer wall, thin and see-through, down to the water with a ring of foam where it lands.
    p0 = Vector(plaza_pt(a, BED_OUT + .02, .45))
    out = Vector(plaza_pt(a, 1.0)) - Vector(plaza_pt(a, 0.0))
    jets.lathe([(0, 0), (.13, 0), (.1, .14), (.06, .16), (0, .16)],
               Matrix.Translation(p0) @ Matrix.Rotation(math.radians(a), 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "Y"),
               M["gold"], 16)
    drop = p0.z - WATER
    pts = []
    for i in range(21):
        s = i / 20
        pts.append(p0 + out * (.14 + 2.2 * s) + Vector((0, 0, 1.0 * s - (1.0 + drop) * s * s)))
    for i, (p, q) in enumerate(zip(pts, pts[1:])):
        d = q - p
        mtx = Matrix.Translation(p) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        w = .04 + .025 * i / 20
        jets.lathe([(w, 0), (w + .0012, d.length)], mtx, M["jet"], 10)
    land = pts[-1]
    jets.lathe([(.16, WATER + .005), (.42, WATER + .005), (.38, WATER + .04), (.2, WATER + .05)],
               Matrix.Translation((land.x, land.y, 0)), M["foam"], 24, True, True)
    for k in range(6):
        t = math.radians(k * 60 + random.uniform(-15, 15))
        jets.sphere((land.x + .2 * math.cos(t), land.y + .2 * math.sin(t), WATER + .08), random.uniform(.06, .1), M["foam"],
                    (1, 1, 1.3), 1)
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
    sc.render.filepath = os.path.join(HERE, "render_plaza_v3_%s.png" % name)
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "PlazaSample_v3.blend"))
shoot("overview", (0, -71, 19), (0, -31, -2), 28)
shoot("player", (0, -60, 2.4), (0, -35, .6), 26)
shoot("bars", (-14, -50, 5), (-3, -38, 0), 30)
shoot("entrance", (3.5, -66, 6.5), (0, -46, 1), 30)
# v3: close-ups as the two reference renders.
shoot("bar_close", (-4.2, -46.4, 1.9), (-6.5, -43.5, .45), 34)
shoot("tall_close", (2.6, -39.6, 2.6), (0, -35, .8), 34)
shoot("planter_close", (12.6, -38.6, 2.0), (16.6, -35.3, .75), 34)
print("PLAZA SAMPLE DONE")
