# The royal water garden's concept-art kit (R419, R423): everything the beat samples share so they draw with the same
# materials (one Unity material per name, GC_*), textures and tools. A beat's script sets GC_DRESS (the collection it
# exports) before `from gc_kit import *`.
#
#   textures   Stone_Cream (mottled, times the tint), Wood_Grain, Tile_Peach (the kit's 0.6 m floor tiles)
#   materials  M[...] - the colours measured off 승규's kit picture (R419)
#   Mesh       one object of many pieces: box, lathe, sphere, arc, slab, leaf, flower, bush; per-face UVs (fixed)
#   render     render_setup(), shoot(prefix, name, at, look, lens)
#
# Frame: Blender X = section x (east), Blender Y = section z (north), Z = up; a floor's top is z 0.
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
TEX = os.path.join(HERE, "textures")
os.makedirs(TEX, exist_ok=True)
random.seed(int(os.environ.get("GC_SEED", "11")))

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
DRESS = coll(os.environ.get("GC_DRESS", "Plaza dressing"))
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
    light, dark = np.array([.72, .48, .27]), np.array([.48, .29, .15])
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
    "stone": textured("GC | cream stone", STONE_TEX, (.97, .80, .68), .72, .5),
    "stone2": textured("GC | cream stone warm", STONE_TEX, (.94, .76, .64), .75, .5),
    "stone3": textured("GC | cream stone pale", STONE_TEX, (.99, .85, .74), .7, .5),
    "stone_dark": textured("GC | stone band", STONE_TEX, (.80, .60, .45), .75, .5),
    "gold": material("GC | gold", (1.0, .72, .26), .28, .8),
    # Wood: the logs' own UVs (once round, 1.5 m along).
    "wood": textured("GC | bar wood", WOOD_TEX, (1.0, 1.0, 1.0), .55),
    "emblem": material("GC | emblem leaf", (.96, .62, .16), .35, .8),
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

# ------------------------------------------------------------------ v4: the floor tile (the kit's "바닥 타일" / "바닥 곡선 타일")
# 승규 11-05: "분홍빛 나오게 하니까 이상하고 진짜 저 레퍼런스 사진이랑 똑같이 해 바닥 컬러랑" - the herringbone's pink is
# gone; the floor is the kit's square tiles in its warm peach (measured off the kit picture: 246, 196, 166), light grout.
TILE_SIZE = .6  # one tile, metres


def tile_texture():
    """4 x 4 softly rounded square tiles in near final colours (the material's tint is white)."""
    n, cells = 1024, 4
    t = (np.arange(n) + .5) / n * cells
    X, Y = np.meshgrid(t, t)
    fx, fy = X - np.floor(X), Y - np.floor(Y)
    d = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy)) * TILE_SIZE  # metres to the tile's edge
    ident = np.floor(X) * 7 + np.floor(Y) * 13
    rnd = np.mod(np.sin(ident * 12.9898 + 3.1) * 43758.5453, 1.0)
    col = np.array(FLOOR_PEACH) * (.97 + .05 * rnd)[..., None]
    col = col * (1 + .018 * periodic_noise(n, n, 21, 1.4, 3))[..., None]
    grout = .011
    bevel = np.clip((d - grout) / .04, 0, 1)
    col = col * (.9 + .1 * np.sqrt(bevel))[..., None]
    col[d < grout] = np.array(GROUT)
    return save_image("Tile_Peach", np.clip(col, 0, 1))


FLOOR_PEACH = (.965, .77, .68)
GROUT = (.99, .88, .78)
TILE_TEX = tile_texture()
M["paving"] = textured("GC | floor tile", TILE_TEX, (1.0, 1.0, 1.0), .7)
M["grout"] = material("GC | grout", GROUT, .8)  # R428: under curved tile rings, so their seams read as grout

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



def render_setup():
    """The kit's light (R419): a warm neutral sky light, the sky blue only to the camera, exposure -0.45."""
    world = bpy.data.worlds.new("Sky")
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb((.62, .80, 1.0)), 1)
    bg.inputs["Strength"].default_value = .75
    light = nt.nodes.new("ShaderNodeBackground")
    light.inputs["Color"].default_value = (*srgb((1.0, .96, .9)), 1)
    light.inputs["Strength"].default_value = .75
    path = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(light.outputs["Background"], mix.inputs[1])
    nt.links.new(bg.outputs["Background"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], nt.nodes["World Output"].inputs["Surface"])
    sc.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 2.6
    sun.data.color = (1.0, .95, .86)
    sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(42), 0, math.radians(-35))
    sc.collection.objects.link(sun)
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    try:
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "None"
        sc.view_settings.exposure = -.45
    except Exception:
        pass
    try:
        sc.eevee.taa_render_samples = 48
    except Exception:
        pass
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    sc.collection.objects.link(cam)
    sc.camera = cam


def shoot(prefix, name, at, look, lens):
    cam = sc.camera
    cam.location = Vector(at)
    cam.rotation_euler = (Vector(look) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    cam.data.clip_end = 400
    sc.render.filepath = os.path.join(HERE, "render_%s_%s.png" % (prefix, name))
    bpy.ops.render.render(write_still=True)


def pawn(m, x, y, z=0.):
    """A white scale pawn, about 0.75 m."""
    m.lathe([(0, 0), (.24, 0), (.24, .08), (.16, .12), (.1, .36), (.15, .42), (.08, .46), (0, .46)],
            Matrix.Translation((x, y, z)), M["pawn"], 24)
    m.sphere((x, y, z + .58), .15, M["pawn"], subdiv=3)


# ------------------------------------------------------------------ R428: pieces the beats share (from the plaza and canal samples)
def tile_disc(m, cx, cy, z, r_out, centre_r=.9, backing=True):
    """Rings of curved 0.6 m tiles round (cx, cy) out to r_out, one quad per tile (the plaza's floor, R419)."""
    if backing:
        # The rings' chords differ ring to ring (each has its own tile count): a grout-coloured disc under them fills
        # the hairline slivers between rings.
        m.lathe([(0, z - .008), (r_out, z - .008), (r_out, z - .005), (0, z - .005)], Matrix.Translation((cx, cy, 0)), M["grout"], 48, False)
    rc = min(centre_r, r_out)
    centre = [m.bm.verts.new((cx + rc * math.cos(2 * math.pi * i / 24), cy + rc * math.sin(2 * math.pi * i / 24), z))
              for i in range(24)]
    f = m.bm.faces.new(centre)
    m.tag(centre, M["paving"])
    m.fixed[f] = {v: ((v.co.x - cx) / (TILE_SIZE * 4) + .5, (v.co.y - cy) / (TILE_SIZE * 4) + .5) for v in centre}
    m.no_recalc.add(f)
    ring, r0 = 0, rc
    while r0 < r_out - 1e-3:
        r1 = min(r0 + TILE_SIZE, r_out)
        n = max(6, round(2 * math.pi * (r0 + r1) / 2 / TILE_SIZE))
        off = random.random()
        inner = [m.bm.verts.new((cx + r0 * math.cos(2 * math.pi * (j + off) / n), cy + r0 * math.sin(2 * math.pi * (j + off) / n), z))
                 for j in range(n)]
        outer = [m.bm.verts.new((cx + r1 * math.cos(2 * math.pi * (j + off) / n), cy + r1 * math.sin(2 * math.pi * (j + off) / n), z))
                 for j in range(n)]
        v0 = (ring % 4) / 4
        v1 = v0 + (r1 - r0) / TILE_SIZE / 4
        for j in range(n):
            k = (j + 1) % n
            q = m.bm.faces.new((inner[j], outer[j], outer[k], inner[k]))
            u0 = (j % 4) / 4
            m.fixed[q] = {inner[j]: (u0, v0), outer[j]: (u0, v1), outer[k]: (u0 + .25, v1), inner[k]: (u0 + .25, v0)}
            m.no_recalc.add(q)
        m.tag(inner + outer, M["paving"])
        r0, ring = r1, ring + 1


def ring_blocks(m, cx, cy, r0, r1, z0, z1, length, mats, bevel, stagger=0.):
    """A full ring of separate rounded blocks round (cx, cy), each about `length` long, joints offset by `stagger`."""
    rm = (r0 + r1) / 2
    n = max(3, round(2 * math.pi * rm / length))
    step = 360.0 / n
    gap = math.degrees(.014 / rm) / 2
    for k in range(n):
        a0 = step * (k + stagger)
        m.arc(r0, r1, a0 + gap, a0 + step - gap, z0, z1, random.choice(mats), center=(cx, cy), bevel=bevel,
              segs=max(1, int(step / 2.5)))


def beam(m, a, b, half_w, z_lo, z_hi, mat, bevel=.03):
    """A straight piece from a to b (x, y, base z), half_w either side, z_lo..z_hi above the base line."""
    a, b = Vector(a), Vector(b)
    d = Vector((b.x - a.x, b.y - a.y, 0)).normalized()
    s = Vector((-d.y, d.x, 0)) * half_w
    pts = [a - s, b - s, b + s, a + s]
    lo = [m.bm.verts.new((p.x, p.y, p.z + z_lo)) for p in pts]
    hi = [m.bm.verts.new((p.x, p.y, p.z + z_hi)) for p in pts]
    m.bm.faces.new(list(reversed(lo)))
    m.bm.faces.new(hi)
    for i in range(4):
        j = (i + 1) % 4
        m.bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    return m.tag(lo + hi, mat, False, {e for v in lo + hi for e in v.link_edges}, bevel)


def kit_urn(m, flowers, at):
    """A small gold urn of flowers on a post."""
    m.lathe([(0, 0), (.09, 0), (.1, .03), (.06, .06), (.06, .1), (.13, .14), (.22, .22), (.24, .27), (.21, .29), (0, .29)],
            Matrix.Translation(at), GOLD, 24)
    c = Vector(at) + Vector((0, 0, .34))
    flowers.bush(c, .24, 18, [M["leaf2"], M["leaf"]], .7)
    for _ in range(7):
        t = random.uniform(0, 2 * math.pi)
        up = random.uniform(.45, 1.0)
        out = Vector((math.cos(t) * math.sqrt(1 - up * up), math.sin(t) * math.sqrt(1 - up * up), up))
        p = c + Vector((out.x * .23, out.y * .23, out.z * .23 * .7)) + out * .02
        flowers.flower(p, random.uniform(.07, .09), random.choice(FLOWERS[:4]), out + Vector((0, 0, .4)))


def kit_balustrade(rail, flowers, a, b, height_at, urns=True, skip_first=False):
    """The canal's balustrade (R423): posts every ~3.3 m with a gold ball or, every other one, a gold urn of flowers;
    turned balusters every 0.3 m, a plinth and a handrail - all following height_at(point) along a -> b."""
    a, b = Vector(a), Vector(b)
    length = (b - a).length
    n_posts = max(1, round(length / 3.3))
    pts = [a + (b - a) * (k / n_posts) for k in range(n_posts + 1)]
    for p in pts:
        p.z = height_at(p)
    for p, q in zip(pts, pts[1:]):
        beam(rail, p, q, .2, 0, .2, M["stone2"])
        beam(rail, p, q, .2, .86, .98, M["stone"])
        beam(rail, p, q, .23, .98, 1.06, M["stone3"])
        seg = (q - p).length
        n = max(1, int((seg - .5) / .3))
        for i in range(n):
            c = p + (q - p) * ((.25 + (seg - .5) * (i + .5) / n) / seg)
            rail.lathe([(.07, .2), (.08, .24), (.05, .3), (.06, .38), (.11, .52), (.09, .66), (.05, .74), (.06, .8), (.09, .86)],
                       Matrix.Translation((c.x, c.y, c.z)), M["stone"], 12)
    for k, p in enumerate(pts):
        if skip_first and k == 0:
            continue
        rail.box((p.x - .3, p.y - .3, p.z), (p.x + .3, p.y + .3, p.z + .22), M["stone2"], .04)
        rail.box((p.x - .24, p.y - .24, p.z + .22), (p.x + .24, p.y + .24, p.z + 1.2), M["stone"], .04)
        rail.box((p.x - .3, p.y - .3, p.z + 1.2), (p.x + .3, p.y + .3, p.z + 1.3), M["stone3"], .04)
        if urns and k % 2 == 1:
            kit_urn(rail, flowers, (p.x, p.y, p.z + 1.3))
        else:
            rail.lathe([(0, 1.3), (.09, 1.3), (.05, 1.37), (0, 1.37)], Matrix.Translation((p.x, p.y, p.z)), GOLD, 16)
            rail.sphere((p.x, p.y, p.z + 1.52), .16, GOLD, subdiv=3)


# The plaza's spin bars (R419): the round hub with its gold sprig, wood-grained logs with riveted gold bands.
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
    """v4, as the kit's "중앙 회전 축": a wide round gold foot, a darker stone band, a round stone drum with a gold
    sprig on its sides, a gold rim and a low gold dome with a knob. Returns the drum's radius (where the arms enter)."""
    base = Matrix.Translation(at)
    k = R / .5
    segs = 40
    bar.lathe([(0, 0), (R + .2 * k, 0), (R + .2 * k, .045), (R + .17 * k, .075), (0, .075)], base, GOLD, segs, False)
    bar.lathe([(0, .075), (R + .11 * k, .075), (R + .11 * k, body0 - .02), (R + .07 * k, body0), (0, body0)], base, GOLD, segs, False)
    band = body0 + .1 * k
    bar.lathe([(0, body0), (R + .008, body0), (R + .012, body0 + .01), (R + .012, band - .01), (R + .008, band), (0, band)],
              base, M["stone_dark"], segs, False)
    bar.lathe([(0, band), (R - .012, band), (R, band + .012), (R, body1 - .012), (R - .012, body1), (0, body1)], base,
              M["stone2"], segs, True)
    bar.lathe([(0, body1), (R + .04 * k, body1), (R + .045 * k, body1 + .02), (R + .045 * k, body1 + .06 * k),
               (R + .03 * k, body1 + .075 * k), (0, body1 + .075 * k)], base, GOLD, segs, False)
    top = body1 + .075 * k
    bar.lathe([(0, top), (R * .86, top), (R * .78, top + .07 * k), (R * .5, top + .13 * k), (R * .2, top + .155 * k),
               (0, top + .16 * k)], base, GOLD, segs)
    bar.lathe([(0, top + .14 * k), (.06 * k, top + .14 * k), (.045 * k, top + .21 * k), (0, top + .21 * k)], base, GOLD, 16, False)
    bar.sphere(Vector(at) + Vector((0, 0, top + .28 * k)), .075 * k, GOLD, subdiv=2)
    for deg in emblem_faces:
        frame = Matrix.Translation(at) @ Matrix.Rotation(math.radians(deg), 4, "Z") @ Matrix.Translation((R - .004, 0, emblem_z)) \
            @ Matrix(((0, 0, 1), (1, 0, 0), (0, 1, 0))).to_4x4()
        emblem(bar, frame, k)
    return R


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




# ------------------------------------------------------------------ R432: the crown badge (the tubs' crown, R429, for any face)
CROWN = [(-.8, -.45), (.8, -.45), (.86, .45), (.42, .06), (0, .62), (-.42, .06), (-.86, .45)]   # x across, y up, metres
CROWN_BALLS = [(-.86, .55), (0, .74), (.86, .55)]


def offset_polygon(pts, d):
    """Each corner pushed out by d along its bisector (mitred), for an outline round a polygon."""
    out = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2 = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[(i + 1) % n])
        e0, e1 = (p1 - p0).normalized(), (p2 - p1).normalized()
        n0, n1 = Vector((e0.y, -e0.x)), Vector((e1.y, -e1.x))   # outward for a counter-clockwise polygon
        b = (n0 + n1)
        if b.length < 1e-6:
            b = n0
        b.normalize()
        cos_half = max(.35, b.dot(n0))
        q = p1 + b * (d / cos_half)
        out.append((q.x, q.y))
    return out


def crown_badge(m, frame, scale, fill, outline=None, depth=.012):
    """A crown standing proud of a face: `frame` puts local x across, y up, z out of the face; a gold outline round
    a `fill` crown, gold balls on its points and a gold band across its foot."""
    gold = outline or GOLD
    pts = [(x * scale, y * scale) for x, y in CROWN]
    m.slab(offset_polygon(pts, .07 * max(scale, .5)), frame, 0, depth, gold)
    m.slab(pts, frame, depth, depth * 1.6, fill)
    for bx, by in CROWN_BALLS:
        c = frame @ Vector((bx * scale, by * scale, depth))
        m.sphere(c, .13 * scale, gold, subdiv=2)
    m.slab([(-.8 * scale, -.33 * scale), (.8 * scale, -.33 * scale), (.8 * scale, -.22 * scale), (-.8 * scale, -.22 * scale)],
           frame, depth * 1.6, depth * 2.2, gold)


def face_frame(at, out, up=(0, 0, 1)):
    """A frame at `at` whose z is `out` (a face's normal), y is `up`, x = y cross z (across, to the right as seen)."""
    z = Vector(out).normalized()
    y = Vector(up).normalized()
    x = y.cross(z)
    return Matrix.Translation(Vector(at)) @ Matrix((x, y, z)).transposed().to_4x4()


def dome(m, at, out, r, mat=None):
    """A little dome (a rivet, a stud) on a face, facing `out`."""
    m.lathe([(0, 0), (r, 0), (r * .8, r * .55), (0, r * .75)], face_frame(at, out, (0, 0, 1) if abs(Vector(out).z) < .9 else (0, 1, 0)),
            mat or GOLD, 10)


# ------------------------------------------------------------------ R436: floors and block walls (from lock_dyke_sample_v1, R434 joints)
def kit_paving(m, x0, x1, y0, y1, z):
    """A flat run of the kit's floor tiles (Tile_Peach, 4 tiles a texture)."""
    vs = [m.bm.verts.new(p) for p in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    f = m.bm.faces.new(vs)
    m.tag(vs, M["paving"])
    m.fixed[f] = {v: (v.co.x / (TILE_SIZE * 4), v.co.y / (TILE_SIZE * 4)) for v in vs}
    m.no_recalc.add(f)


def kit_wall_run(m, axis, fixed, a0, a1, d0, d1, top, bottom=-2.25, coping=True, course=.68, block=1.5):
    """Courses of rounded blocks from `bottom` up to `top` (a coping on it), along x or y; d0..d1 across. Joints 4 cm
    and 3 cm over a mortar core, so nothing behind shows through (R434)."""
    span = top - (.17 if coping else 0) - bottom
    n = max(1, round(span / course))
    h = span / n
    # R438: the core stops 3 cm short of the run's ends (flush, its end faces fought the coping's and the blocks').
    lo = (a0 + .03, fixed + d0 + .05, bottom) if axis == "x" else (fixed + d0 + .05, a0 + .03, bottom)
    hi = (a1 - .03, fixed + d1 - .05, top - .1) if axis == "x" else (fixed + d1 - .05, a1 - .03, top - .1)
    m.box(lo, hi, M["grout"])
    for ci in range(n):
        zb, zt = bottom + ci * h, bottom + (ci + 1) * h
        a = a0 - (block / 2 if ci % 2 else 0)
        while a < a1:
            s, e = max(a, a0), min(a + block, a1)
            a += block
            if e - s < .3:
                continue
            lo = (s + .02, fixed + d0, zb + .015) if axis == "x" else (fixed + d0, s + .02, zb + .015)
            hi = (e - .02, fixed + d1, zt - .015) if axis == "x" else (fixed + d1, e - .02, zt - .015)
            m.box(lo, hi, random.choice(STONES), .035)
    if coping:
        lo = (a0, fixed + d0 - .05, top - .17) if axis == "x" else (fixed + d0 - .05, a0, top - .17)
        hi = (a1, fixed + d1 + .05, top + .012) if axis == "x" else (fixed + d1 + .05, a1, top + .012)
        m.box(lo, hi, M["stone3"], .04)
