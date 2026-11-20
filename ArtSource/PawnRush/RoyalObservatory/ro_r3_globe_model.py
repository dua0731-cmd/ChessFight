"""R3 the great globe (ro-hall session, R725, 2026-11-19): the glass globe and everything in and round it.

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python ro_r3_globe_model.py -- [--stills]

Shape only for now: ro_kit.py (R0, the ro-path session) does not exist yet, so this script makes its own preview
materials under the shared slot names ("RO | marble", "RO | gold" ... PLAN.md 2) and does not export. When ro_kit
exists, the parts below are exported with it, each in the frame of the Unity object it hangs on (MODELS).

Every number at the top is RoyalObservatoryBuilder.cs's (GreatGlobe), so a change there is a change here and a
re-run. Walking faces and colliders stay the builder's: the tilted floor's top is y 0 of Device_SolarFlare and nothing
of the model stands above it by more than 3 mm; the moons stay inside their 0.4 m discs.

Axes: Unity (x, y up, z forward) -> Blender (x, z up, y forward), metres (as rl_blockout.py).
"""
import math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)            # ro_kit (R0, ro-path's: read only here)
STILLS = os.path.join(HERE, "stills", "r3")
BLEND = os.path.join(HERE, "ro_r3_globe.blend")

# ---------------------------------------------------------------- the builder's numbers (Unity space)
GLOBE = (0.0, 10.0, 64.0)
WEST_GATE, EAST_GATE = (-12.65, 4.0, 64.0), (13.86, 12.0, 64.0)
GLOBE_R, ECLIPTIC_R, MOON_R, SUN_H = 14.0, 15.6, 3.4, 8.0
HALL_FLOOR = -18.0
ORBITS = [(6.0, 3.4, 1.1, 3), (3.5, 3.9, .95, 2), (1.45, 4.5, .75, 2)]   # radius, height, body, count
PLANETS = [["jupiter", "saturn", "neptune"], ["earth", "mars"], ["venus", "mercury"]]
BASE_R, BASE_TOP = [13.0, 10.5, 7.0, 4.5], [-14.0, -10.0, -6.5, GLOBE[1] - GLOBE_R]
MERIDIAN_R, HORIZON_R = 16.5, 18.0
GATE_FRAME_R = 3.8
GRID = "black"
# The light pass (the user via the planning session 11-19, "경량화 진행"): round things lose segments where the silhouette
# allows (SEG_SCALE on revolves and spheres over 24 segments), the glass's engraving becomes its texture.
SEG_SCALE = .55
GLASS_STARS = False     # the user 11-19 (light pass): "별 다 지워도 돼" - the glass keeps its black grid only


def seg(n):
    return n if n <= 24 else max(24, int(round(n * SEG_SCALE)))          # the glass's latitude and longitude lines (user 11-19: black)


def U(x, y, z):
    """Unity point -> Blender vector."""
    return Vector((x, z, y))


TILT = math.atan2(EAST_GATE[1] - WEST_GATE[1], EAST_GATE[0] - WEST_GATE[0])           # 16.79 deg, west low
NORMAL_U = (-math.sin(TILT), math.cos(TILT), 0.0)
DEPTH = (GLOBE[0] - WEST_GATE[0]) * NORMAL_U[0] + (GLOBE[1] - WEST_GATE[1]) * NORMAL_U[1]
MIDDLE_U = tuple(GLOBE[i] - NORMAL_U[i] * DEPTH for i in range(3))
FLOOR_R = math.sqrt(GLOBE_R ** 2 - DEPTH ** 2) + .1
# Device_SolarFlare's frame in Blender: local x east-up the slope, local z the floor's normal, local y north.
FLOOR_M = Matrix.Translation(U(*MIDDLE_U)) @ Matrix.Rotation(-TILT, 4, "Y")
TRACKS = [o[0] * SUN_H / (SUN_H - o[1]) for o in ORBITS]                                 # 10.43, 6.83, 3.31

# ---------------------------------------------------------------- materials (preview only: the slots are what counts)
PREVIEW = {
    "marble": ((.86, .72, .62), 0, .3), "gold": ((1.0, .46, .07), 1, .22), "brass": ((.70, .47, .17), 1, .36),
    "black": ((.008, .008, .01), 0, .3), "navy": ((.008, .012, .07), 0, .38), "violet": ((.20, .05, .36), 0, .25), "pearl": ((.95, .92, .84), 0, .08),
    "glass": ((.85, .92, 1.0), 0, .02), "hallstone": ((.05, .06, .12), 0, .4), "velvet": ((.16, .03, .28), 0, .8),
    "motif": ((.05, .06, .2), 0, .3),
    "planet_jupiter": ((.72, .52, .34), 0, .5), "planet_saturn": ((.82, .70, .48), 0, .5),
    "planet_saturn_ring": ((.78, .66, .46), 0, .4), "planet_neptune": ((.16, .30, .72), 0, .4),
    "planet_earth": ((.18, .36, .62), 0, .45), "planet_mars": ((.66, .26, .12), 0, .6),
    "planet_venus": ((.86, .74, .50), 0, .5), "planet_mercury": ((.55, .52, .50), 0, .7),
    "planet_moon": ((.80, .78, .74), 0, .7), "planet_globe_glass": ((.86, .92, 1.0), 0, .05), "planet_sun": ((1, .6, .15), 0, .5),
}
_mats = {}
PLANET_SRC = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R725_RoyalObservatoryConcept",
                          "AssetRefs", "Planets")
PLANET_FILES = {"jupiter": "RO_P1_jupiter", "saturn": "RO_P2_saturn", "neptune": "RO_P3_neptune", "earth": "RO_P4_earth",
                "mars": "RO_P5_mars", "venus": "RO_P6_venus", "mercury": "RO_P7_mercury",
                "saturn_ring": "RO_P8_saturn_ring", "moon": "RO_P9_moon_disc"}
PLANET_OUT = os.path.join(HERE, "textures", "r3")


def moon_relief():
    """The moon face's height field from RO_P9 itself, so the dents sit where its craters and seas are drawn: a crater
    is a dark ring and spot against its surroundings, so height = how much lighter a point is than the stone round
    it (fine detail less a blur), plus the seas a little lower. Returns a sampler (x, y metres on the face) -> metres."""
    import numpy as np
    src = bpy.data.images.load(os.path.join(PLANET_SRC, PLANET_FILES["moon"] + ".png"), check_existing=True)
    w, h = src.size

    def lum_at(size):
        im = src.copy()
        im.scale(size, size)
        a_ = np.array(im.pixels[:], dtype=np.float32).reshape(size, size, 4)[..., :3].mean(axis=2)
        bpy.data.images.remove(im)
        return a_

    fine, mid, broad = lum_at(192), lum_at(48), lum_at(12)
    span = max(1e-3, float(fine.max() - fine.min()))

    def sample(arr, u, v):
        n = arr.shape[0]
        x, y = min(n - 1.001, max(0, u * n - .5)), min(n - 1.001, max(0, v * n - .5))
        i, j = int(y), int(x)
        fy, fx = y - i, x - j
        return (arr[i, j] * (1 - fx) * (1 - fy) + arr[i, j + 1] * fx * (1 - fy) + arr[i + 1, j] * (1 - fx) * fy
                + arr[i + 1, j + 1] * fx * fy)

    def relief(x, y):
        u, v = .5 + .47 * x / MOON_R, .5 + .47 * y / MOON_R
        crater = (sample(fine, u, v) - sample(mid, u, v)) / span          # a crater's dark floor < 0, its bright rim > 0
        sea = (sample(broad, u, v) - float(broad.mean())) / span           # the dark seas lower
        return max(-.1, min(.026, .32 * crater + .06 * sea))

    return relief


def sun_texture(out):
    """The sun lamp's surface (the user, 11-19: "태양 섬을 실제 태양 처럼 해"), 2048 x 1024 equirectangular: a burning
    orange-to-yellow photosphere - granulation at three scales, bright faculae, a few dark spots with orange
    penumbrae - made from noise filtered in the frequency domain, so it repeats round seamlessly."""
    import numpy as np
    rnd = np.random.default_rng(725)
    W, H = 2048, 1024

    def field(lo, hi):
        f = np.fft.fft2(rnd.standard_normal((H, W)))
        ky = np.fft.fftfreq(H)[:, None] * H
        kx = np.fft.fftfreq(W)[None, :] * W / 2              # pixels are square on the sphere's equator
        k = np.hypot(kx, ky)
        band = np.exp(-((np.log(k + 1e-6) - np.log((lo * hi) ** .5)) / np.log(hi / lo) * 2) ** 2)
        a = np.real(np.fft.ifft2(f * band))
        return (a - a.mean()) / a.std()

    gran = .55 * field(120, 260) + .3 * field(40, 90) + .15 * field(8, 20)
    heat = np.clip(.62 + .12 * gran, 0, 1)
    fac = np.clip(field(10, 24), 0, None) ** 2 * .12
    heat = np.clip(heat + fac, 0, 1)
    ys, xs = np.mgrid[0:H, 0:W]
    lat = (ys / H - .5) * np.pi
    for _ in range(0):                                        # (no sunspots: the concept's orb is clear) sunspots, kept to the low latitudes as the sun's are
        cy = rnd.uniform(.32, .68) * H
        cx = rnd.uniform(0, W)
        r = rnd.uniform(14, 34)
        dx = np.minimum(np.abs(xs - cx), W - np.abs(xs - cx)) * np.cos(lat)
        d = np.hypot(dx, ys - cy) / r
        heat = np.where(d < 1, heat * (.12 + .5 * d ** 2), np.where(d < 1.9, heat * (.65 + .35 * (d - 1) / .9), heat))
    stops = np.array([0, .35, .6, .8, 1.0])
    cols = np.array([(.98, .62, .22), (1.0, .76, .38), (1.0, .86, .55), (1.0, .93, .74), (1.0, .98, .9)])
    rgb = np.stack([np.interp(heat, stops, cols[:, c]) for c in range(3)], axis=-1)
    img = np.concatenate([rgb, np.ones((H, W, 1))], axis=-1).astype(np.float32)[::-1]
    o = bpy.data.images.new("sun", W, H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = out
    o.file_format = "PNG"
    o.save()
    return out


def planet_texture(name):
    """The planet's texture for this zone (textures/r3/RO_Planet<Name>.png), made once from the Codex sheet:
    planets 1536 x 1024 equirectangular -> 2048 x 1024 (2:1); Saturn's ring - one row across the sheet's stripes
    (inner left, outer right) -> 2048 x 128, its alpha from how far the colour is from the sheet's white; the moon's
    disc as it is (1024 x 1024)."""
    os.makedirs(PLANET_OUT, exist_ok=True)
    out = os.path.join(PLANET_OUT, "RO_Planet%s.png" % "".join(w.capitalize() for w in name.split("_")))
    if os.path.exists(out):
        return out
    if name == "sun":
        return sun_texture(out)
    if name == "globe_glass":
        return globe_glass_texture(out)
    if name == "moon":
        import shutil
        shutil.copyfile(os.path.join(PLANET_SRC, PLANET_FILES[name] + ".png"), out)    # as it is (the user, 11-19)
        return out
    if False:
        # (no longer used) RO_P9 lightened toward the concept's cream (it reads darker than RO_1 / RO_3 draw the moons), its grey
        # softened, the crater bowls shaded a little darker and their rims lighter, as the dents catch the light.
        import numpy as np
        src = bpy.data.images.load(os.path.join(PLANET_SRC, PLANET_FILES[name] + ".png"))
        w, h = src.size
        px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
        # the concept's moons: smooth cream stone with soft grey-violet patches - so RO_P9's maria only, blurred (its
        # fine crater detail is left to the dents and their shading)
        soft = src.copy()
        soft.scale(40, 40)
        soft.scale(w, h)
        sp = np.array(soft.pixels[:], dtype=np.float32).reshape(h, w, 4)
        lum = sp[..., :3].mean(axis=2)
        dark = np.clip(((lum.max() - lum) / max(1e-3, lum.max() - lum.min()) - .15) / .6, 0, 1) ** .9
        cream = np.array((.92, .87, .82), np.float32)
        patch = np.array((.5, .47, .5), np.float32)
        px[..., :3] = cream * (1 - dark[..., None] * .75) + patch * dark[..., None] * .75
        ys, xs = np.mgrid[0:h, 0:w]
        X = ((xs + .5) / w - .5) / .47 * MOON_R                     # the disc's metres (u = .5 + .47 x / R)
        Y = ((ys + .5) / h - .5) / .47 * MOON_R
        shade = np.ones((h, w), np.float32)
        for cx, cy, cr in moon_craters():
            q = np.hypot(X - cx, Y - cy) / cr
            shade -= np.where(q < .85, .22 * (1 - (q / .85) ** 2), 0)
            shade += np.where((q >= .85) & (q < 1.25), .06 * np.cos((q - 1.0) / .25 * np.pi / 2) ** 2, 0)
        px[..., :3] *= shade[..., None]
        np.clip(px, 0, 1, out=px)
        o = bpy.data.images.new("moon", w, h, alpha=True)
        o.pixels[:] = px.ravel()
        o.filepath_raw = out
        o.file_format = "PNG"
        o.save()
        return out
    src = bpy.data.images.load(os.path.join(PLANET_SRC, PLANET_FILES[name] + ".png"))
    if name == "saturn_ring":
        import numpy as np
        w, h = src.size
        px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
        row = px[h // 2 - 8:h // 2 + 8].mean(axis=0)                       # a band across the middle
        xs = (np.arange(2048) * w / 2048).astype(int)
        row = row[xs]
        white = np.clip((row[:, :3].min(axis=1) - .85) / .15, 0, 1)       # the sheet's white margin -> clear
        row[:, 3] = 1 - white
        img = np.repeat(row[None, :, :], 128, axis=0)
        o = bpy.data.images.new("ring", 2048, 128, alpha=True)
        o.pixels[:] = img.ravel()
    else:
        src.pixels[0]                                                       # load it
        o = src.copy()
        o.scale(2048, 1024)
    o.filepath_raw = out
    o.file_format = "PNG"
    o.save()
    return out


def mat(slot):
    if slot in _mats:
        return _mats[slot]
    name = "RO | " + slot
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    col, metal, rough = PREVIEW[slot]
    p.inputs["Base Color"].default_value = (*col, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if slot == "glass":
        p.inputs["Roughness"].default_value = .02
        lw = nt.nodes.new("ShaderNodeLayerWeight")
        lw.inputs["Blend"].default_value = .25
        mr = nt.nodes.new("ShaderNodeMapRange")
        mr.inputs["To Min"].default_value, mr.inputs["To Max"].default_value = .02, .16
        nt.links.new(lw.outputs["Facing"], mr.inputs["Value"])
        nt.links.new(mr.outputs["Result"], p.inputs["Alpha"])
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            m.blend_method = "BLEND"
        m.use_backface_culling = False
    if slot == "pearl":
        p.inputs["Emission Color"].default_value = (1, .86, .6, 1)
        p.inputs["Emission Strength"].default_value = 4.0      # preview glow (Unity: the lit pearl)
    if slot == "marble":
        # Preview veins: thin warm lines from a distorted noise.
        tc = nt.nodes.new("ShaderNodeTexCoord")
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = .35
        nz.inputs["Detail"].default_value = 6
        nz.inputs["Distortion"].default_value = 2.5
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[1].position = .47, .53
        ramp.color_ramp.elements[0].color = (*col, 1)
        ramp.color_ramp.elements[1].color = (*col, 1)
        e = ramp.color_ramp.elements.new(.5)
        e.color = (.62, .45, .30, 1)
        nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
        nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
        nt.links.new(ramp.outputs["Color"], p.inputs["Base Color"])
    if slot == "motif":
        # Preview: the atlas itself (ro-path's library), on the UVs the floor gives each panel.
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(HERE, "textures", "library", "RO_Motif_Albedo.png"), check_existing=True)
        nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
        p.inputs["Metallic"].default_value = .35
    if slot.startswith("planet_"):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(planet_texture(slot[len("planet_"):]), check_existing=True)
        nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
        if slot == "planet_sun":
            # glowing warm: the texture tinted toward amber, so it burns yellow-white at the core in the tone map
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            nt.links.new(tex.outputs["Color"], mix.inputs[6])
            mix.inputs[7].default_value = (1.0, .62, .22, 1)
            nt.links.new(mix.outputs[2], p.inputs["Emission Color"])
            p.inputs["Emission Strength"].default_value = 7.0
        if slot in ("planet_saturn_ring", "planet_globe_glass"):
            nt.links.new(tex.outputs["Alpha"], p.inputs["Alpha"])
            try:
                m.surface_render_method = "BLENDED"
            except Exception:
                m.blend_method = "BLEND"
    _mats[slot] = m
    return m


# ---------------------------------------------------------------- mesh helpers

class Mesh:
    """A bmesh with per-face material slots, made into one object."""

    def __init__(self):
        self.bm = bmesh.new()
        self.slots = []

    def si(self, slot):
        if slot not in self.slots:
            self.slots.append(slot)
        return self.slots.index(slot)

    def face(self, pts, slot, smooth=False):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = self.si(slot)
        f.smooth = smooth
        return f

    def grid(self, rows, slot, closed_u=False, closed_v=False, smooth=True, flip=False):
        """rows: list of rows of points; quads between them."""
        vv = [[self.bm.verts.new(p) for p in r] for r in rows]
        nr, nc = len(vv), len(vv[0])
        faces = []
        for i in range(nr - 1 + (1 if closed_v else 0)):
            for j in range(nc - 1 + (1 if closed_u else 0)):
                a, b = vv[i][j], vv[i][(j + 1) % nc]
                c, d = vv[(i + 1) % nr][(j + 1) % nc], vv[(i + 1) % nr][j]
                q = [a, d, c, b] if flip else [a, b, c, d]
                try:
                    f = self.bm.faces.new(q)
                except ValueError:
                    continue
                f.material_index = self.si(slot)
                f.smooth = smooth
                faces.append(f)
        return vv, faces

    def revolve(self, profile, slot, segs=64, m=Matrix(), closed=False, smooth=True, a0=0.0, a1=2 * math.pi):
        """profile: [(r, z)] revolved round local z; closed: the profile is a loop (a ring's section)."""
        segs = seg(segs)
        full = abs(a1 - a0 - 2 * math.pi) < 1e-6
        n = segs if full else segs + 1
        rows = []
        for (r, z) in profile:
            row = []
            for k in range(n):
                a = a0 + (a1 - a0) * k / segs
                row.append(m @ Vector((r * math.cos(a), r * math.sin(a), z)))
            rows.append(row)
        return self.grid(rows, slot, closed_u=full, closed_v=closed, smooth=smooth)

    def box(self, c, size, slot, m=Matrix()):
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        p = [m @ Vector((c[0] + sx * hx, c[1] + sy * hy, c[2] + sz * hz))
             for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        vs = [self.bm.verts.new(q) for q in p]
        for idx in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
            f = self.bm.faces.new([vs[i] for i in idx])
            f.material_index = self.si(slot)

    def star(self, m, r_out, r_in, points, thick, slot):
        """A raised star (local xy plane, local z up, from 0 to thick)."""
        ring = []
        for k in range(points * 2):
            a = math.pi / 2 + k * math.pi / points
            r = r_out if k % 2 == 0 else r_in
            ring.append((r * math.cos(a), r * math.sin(a)))
        top_c = self.bm.verts.new(m @ Vector((0, 0, thick)))
        top = [self.bm.verts.new(m @ Vector((x, y, thick * (1.0 if i % 2 else .55)))) for i, (x, y) in enumerate(ring)]
        bot = [self.bm.verts.new(m @ Vector((x, y, 0))) for x, y in ring]
        si = self.si(slot)
        n = len(ring)
        for i in range(n):
            j = (i + 1) % n
            f = self.bm.faces.new([top_c, top[i], top[j]])
            f.material_index = si
            f = self.bm.faces.new([bot[i], bot[j], top[j], top[i]])
            f.material_index = si

    def sphere(self, c, r, slot, segs=32, rings=16, m=Matrix(), uv=True):
        segs, rings = seg(segs), max(rings if segs <= 24 else 12, int(round(rings * (seg(segs) / segs))))
        rows = []
        for i in range(rings + 1):
            th = math.pi * i / rings
            row = []
            for k in range(segs + 1):
                ph = 2 * math.pi * k / segs
                row.append(m @ Vector((c[0] + r * math.sin(th) * math.cos(ph), c[1] + r * math.sin(th) * math.sin(ph),
                                       c[2] + r * math.cos(th))))
            rows.append(row)
        vv, faces = self.grid(rows, slot, smooth=True, flip=True)
        if uv:
            # equirectangular: u round the poles' axis (local z), v from the south pole up
            lay = self.bm.loops.layers.uv.verify()
            where = {}
            for i, row in enumerate(vv):
                for k_, v in enumerate(row):
                    where[v] = (k_ / segs, 1.0 - i / rings)
            for f in faces:
                for lp in f.loops:
                    lp[lay].uv = where[lp.vert]
        bmesh.ops.remove_doubles(self.bm, verts=[v for r in vv for v in r], dist=1e-5)
        return faces

    def revolve_uv(self, profile, slot, segs, m, tile_len, closed=True):
        """A full revolve with its own UVs in RO_Gold's metres (0.6 a tile, the material's tiling): one tile every
        tile_len along the ring (a whole number round it) and across the profile, so the gold's stars and scrolls
        sit on the ring the way they sit on a wall (the user 11-19, a screenshot of RO_Gold: "이거랑 같은 텍스처로").
        No SEG_SCALE: the big rings showed kinks on their outline at 106 segments (the user 11-19: "꺾임이 있어").
        The faces are marked keep_uv, so metre_uvs keeps these UVs."""
        TILE = .6
        lay = self.bm.loops.layers.uv.verify()
        keep = self.bm.faces.layers.int.get("keep_uv") or self.bm.faces.layers.int.new("keep_uv")
        R = sum(r for r, _ in profile) / len(profile)
        n_u = max(1, round(2 * math.pi * R / tile_len))
        pts = list(profile) + ([profile[0]] if closed else [])
        cum = [0.0]
        for a, b in zip(pts, pts[1:]):
            cum.append(cum[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        n_v = max(1, round(cum[-1] / tile_len)) if closed else cum[-1] / tile_len
        vv = [[self.bm.verts.new(m @ Vector((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)))
               for k in range(segs)] for r, z in profile]
        si = self.si(slot)
        npr = len(profile)
        faces = []
        for i in range(npr if closed else npr - 1):
            for k in range(segs):
                q = [(i, k), (i, k + 1), (i + 1, k + 1), (i + 1, k)]
                f = self.bm.faces.new([vv[a % npr][b % segs] for a, b in q])
                f.material_index = si
                f.smooth = True
                f[keep] = 1
                for lp, (a, b) in zip(f.loops, q):
                    lp[lay].uv = (b / segs * n_u * TILE, cum[a] / cum[-1] * n_v * TILE)
                faces.append(f)
        return faces

    def uv_radial(self, faces, centre, r0, r1, m=Matrix()):
        """UVs for a flat ring: u from r0 (0) to r1 (1), v the middle (a radial strip texture)."""
        lay = self.bm.loops.layers.uv.verify()
        inv = m.inverted()
        for f in faces:
            for lp in f.loops:
                q = inv @ lp.vert.co
                lp[lay].uv = (max(0, min(1, ((Vector((q.x, q.y)) - Vector(centre[:2])).length - r0) / (r1 - r0))), .5)

    def star3d(self, m, r_long, r_short, r_in, points, height, slot):
        """A faceted star (RO_A3's gold stars): `points` long points alternating with as many short ones (r_short = 0:
        no short points), ridged up to an apex `height` over the local xy plane."""
        tips = []
        n = points * (2 if r_short else 1)
        for k in range(n):
            a = math.pi / 2 + 2 * math.pi * k / n
            r = r_long if (k % 2 == 0 or not r_short) else r_short
            tips.append((r, a))
        ring = []
        for k, (r, a) in enumerate(tips):
            ring.append((r * math.cos(a), r * math.sin(a), 0.0))
            b = a + math.pi / n
            ring.append((r_in * math.cos(b), r_in * math.sin(b), height * .28))
        apex = self.bm.verts.new(m @ Vector((0, 0, height)))
        top = [self.bm.verts.new(m @ Vector(q)) for q in ring]
        si = self.si(slot)
        for i in range(len(top)):
            f = self.bm.faces.new([apex, top[i], top[(i + 1) % len(top)]])
            f.material_index = si
            f.smooth = False

    def tube_var(self, pts, radii, slot, sides=6):
        """A tube along a polyline whose radius changes point to point (scrolls)."""
        rows = []
        n = len(pts)
        for i in range(n):
            a = pts[max(i - 1, 0)]
            b = pts[min(i + 1, n - 1)]
            t = (b - a).normalized()
            up = Vector((0, 0, 1)) if abs(t.z) < .9 else Vector((1, 0, 0))
            x = t.cross(up).normalized()
            y = t.cross(x).normalized()
            rows.append([pts[i] + (x * math.cos(2 * math.pi * k / sides) + y * math.sin(2 * math.pi * k / sides)) * radii[i]
                         for k in range(sides)])
        self.grid(rows, slot, closed_u=True, smooth=True)

    def tube(self, pts, r, slot, sides=6, closed=False):
        """A round tube along a polyline."""
        n = len(pts)
        rows = []
        for i in range(n):
            p = pts[i]
            a = pts[(i - 1) % n] if (closed or i > 0) else p
            b = pts[(i + 1) % n] if (closed or i < n - 1) else p
            t = (b - a).normalized()
            up = Vector((0, 0, 1)) if abs(t.z) < .9 else Vector((1, 0, 0))
            x = t.cross(up).normalized()
            y = t.cross(x).normalized()
            rows.append([p + (x * math.cos(2 * math.pi * k / sides) + y * math.sin(2 * math.pi * k / sides)) * r
                         for k in range(sides)])
        self.grid(rows, slot, closed_u=True, closed_v=closed, smooth=True)

    def ribbon(self, pts, normals, w, slot, closed=False):
        """A flat strip on a surface (both faces), w wide."""
        n = len(pts)
        left, right = [], []
        for i in range(n):
            a = pts[(i - 1) % n] if (closed or i > 0) else pts[i]
            b = pts[(i + 1) % n] if (closed or i < n - 1) else pts[i]
            side = (b - a).cross(normals[i]).normalized() * (w / 2)
            left.append(pts[i] - side)
            right.append(pts[i] + side)
        self.grid([left, right], slot, closed_u=closed, smooth=False)

    def make(self, name, coll, parent=None, matrix_world=None, two_sided=False, up=None):
        """up: a direction (mesh space) - faces looking against it are turned (flat inlays on a floor)."""
        me = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        self.bm.normal_update()
        if up is not None:
            upv = Vector(up)
            flip = [f for f in self.bm.faces if f.normal.dot(upv) < -.5 and f.calc_center_median().z > -.01]
            if flip:
                bmesh.ops.reverse_faces(self.bm, faces=flip)
        if two_sided:
            src = self.bm.faces[:]
            dup = bmesh.ops.duplicate(self.bm, geom=src)
            back = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
            bmesh.ops.reverse_faces(self.bm, faces=back)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for s in self.slots:
            me.materials.append(mat(s))
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        if parent is not None:
            ob.parent = parent
        if matrix_world is not None:
            ob.matrix_world = matrix_world
        return ob


def ring_section(w, d, bevel=.04, bead=0.0):
    """A ring's section, (r offset, z) loop: w across (radially), d deep, chamfered, an optional bead outside."""
    b = bevel
    pts = [(-w / 2, -d / 2 + b), (-w / 2 + b, -d / 2), (w / 2 - b, -d / 2), (w / 2, -d / 2 + b)]
    if bead:
        pts += [(w / 2, -bead), (w / 2 + bead * .7, -bead * .5), (w / 2 + bead * .7, bead * .5), (w / 2, bead)]
    pts += [(w / 2, d / 2 - b), (w / 2 - b, d / 2), (-w / 2 + b, d / 2), (-w / 2, d / 2 - b)]
    return pts


def ring(mesh, radius, section, slot, segs, m=Matrix()):
    prof = [(radius + dr, z) for dr, z in section]
    mesh.revolve(prof, slot, segs, m, closed=True)


def empty(name, coll, matrix, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = .5
    coll.objects.link(e)
    if parent:
        e.parent = parent
    e.matrix_world = matrix
    return e


def rng(name):
    return random.Random(sum((i + 1) * ord(c) for i, c in enumerate(name)))


# ---------------------------------------------------------------- parts

def glass_gates():
    c = U(*GLOBE)
    gates = []
    for g_, side in ((WEST_GATE, -1), (EAST_GATE, 1)):
        fc = U(g_[0] + side * .5, g_[1] + 3.0, g_[2])
        gates.append(((fc - c).normalized(), math.asin(3.45 / GLOBE_R)))
    return gates


def glass_stars():
    """The engraved stars (seeded as before): (direction, outer radius m, inner radius m, points, turn)."""
    gates = glass_gates()
    r = rng("RoGlobe stars")
    out = []
    while len(out) < 420:
        d = Vector((r.gauss(0, 1), r.gauss(0, 1), r.gauss(0, 1))).normalized()
        if any(d.angle(g_) < a_ for g_, a_ in gates):
            continue
        turn = r.uniform(-.3, .3)
        q = r.random()
        if q < .07:
            out.append((d, r.uniform(1.0, 1.5), .13, 4, turn))
        elif q < .3:
            out.append((d, r.uniform(.45, .75), .08, 4, turn))
        elif q < .42:
            out.append((d, r.uniform(.3, .45), .13, 8, turn))
        else:
            out.append((d, r.uniform(.07, .12), .035, 4, turn))
    return out


def globe_glass_texture(out, W=4096):
    """The glass's engraving as a texture (the light pass): equirectangular W x W/2 over the sphere's directions (u round
    the vertical axis from +x, v from the south pole up) - clear glass (a faint tint, alpha .06), the black grid
    (latitudes every 10 deg, meridians every 12 stopping 8 deg short of the poles, two tilted great circles; 8 / 14 /
    10 cm as the ribbons were) and the gold stars (four- and eight-point, as the geometry was)."""
    import numpy as np
    H = W // 2
    R = GLOBE_R
    v = (np.arange(H) + .5) / H
    u = (np.arange(W) + .5) / W
    lat = (v - .5) * np.pi
    lon = u * 2 * np.pi
    LON, LAT = np.meshgrid(lon, lat)
    X, Y, Z = np.cos(LAT) * np.cos(LON), np.cos(LAT) * np.sin(LON), np.sin(LAT)
    px = 2 * np.pi * R / W                                       # metres a pixel along the equator

    def cover(dist_m, w):
        """Antialiased coverage of a line `w` wide at metre distance dist_m (a pixel's width scaled by cos lat)."""
        aa = px * .8
        return np.clip((w / 2 - dist_m) / aa + .5, 0, 1)

    line = np.zeros((H, W), np.float32)
    for L in range(-80, 81, 10):
        line = np.maximum(line, cover(np.abs(LAT - np.radians(L)) * R, .2 if L == 0 else .12))
    for lo in range(0, 180, 12):
        a = np.radians(lo)
        dist = np.abs(np.sin(LON - a)) * np.cos(LAT) * R
        m_ = cover(dist, .12) * (np.abs(LAT) < np.radians(82))
        line = np.maximum(line, m_)
    for tilt, spin in ((23.4, 0), (66.6, 90)):
        tm = Matrix.Rotation(math.radians(spin), 3, "Z") @ Matrix.Rotation(math.radians(tilt), 3, "X")
        n = tm @ Vector((0, 0, 1))
        line = np.maximum(line, cover(np.abs(X * n.x + Y * n.y + Z * n.z) * R, .14))
    star = np.zeros((H, W), np.float32)
    for d, ro, ri, pts, turn in (glass_stars() if GLASS_STARS else ()):
        x = d.cross(Vector((0, 0, 1)) if abs(d.z) < .95 else Vector((1, 0, 0))).normalized()
        y = d.cross(x)
        la, lo_ = math.asin(max(-1, min(1, d.z))), math.atan2(d.y, d.x) % (2 * math.pi)
        span = ro / R * 1.2
        i0, i1 = int((la - span) / math.pi * H + H / 2) - 1, int((la + span) / math.pi * H + H / 2) + 2
        i0, i1 = max(0, i0), min(H, i1)
        dl = span / max(.05, math.cos(la))
        j0, j1 = int((lo_ - dl) / (2 * math.pi) * W) - 1, int((lo_ + dl) / (2 * math.pi) * W) + 2
        js = np.arange(j0, j1) % W
        sx, sy, sz = X[i0:i1][:, js], Y[i0:i1][:, js], Z[i0:i1][:, js]
        lx = (sx * x.x + sy * x.y + sz * x.z) * R
        ly = (sx * y.x + sy * y.y + sz * y.z) * R
        front = (sx * d.x + sy * d.y + sz * d.z) > 0
        rr = np.hypot(lx, ly)
        ang = np.arctan2(ly, lx) - turn - math.pi / 2
        k = pts
        f = np.abs(((ang * k / (2 * np.pi)) % 1) - .5) * 2         # 1 at a point, 0 between
        edge = ri * ro / (ro * (1 - f) + ri * f)                 # straight-edged points, as the geometry was
        c_ = np.clip((edge - rr) / (px * .8) + .5, 0, 1) * front
        star[i0:i1][:, js] = np.maximum(star[i0:i1][:, js], c_)
    img = np.zeros((H, W, 4), np.float32)
    glass = np.array((.86, .92, 1.0), np.float32)
    gold = np.array((.78, .48, .12), np.float32)
    black = np.array((.012, .012, .015), np.float32)
    a_line, a_star = line[..., None], star[..., None]
    rgb = glass * (1 - a_line) + black * a_line
    rgb = rgb * (1 - a_star) + gold * a_star
    alpha = .06 + (1 - .06) * np.maximum(line, star)
    img[..., :3] = rgb
    img[..., 3] = alpha
    o = bpy.data.images.new("globe_glass", W, H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = out
    o.file_format = "PNG"
    o.save()
    return out


def glass_globe(coll):
    """The glass, open at the two gates, its engraving (black grid, gold stars) now its texture (the light pass) - one
    sphere, drawn from both sides by its material (RO_PlanetGlobeGlass: transparent, culling off) - and, as geometry,
    only what stands proud: the gold bosses at the poles and the gold lips round the two openings."""
    c = U(*GLOBE)
    gates = glass_gates()

    def open_at(d):
        return any(d.angle(g_) < a_ for g_, a_ in gates)

    m = Mesh()
    segs, rings = 72, 36
    rows = []
    for i in range(rings + 1):
        th = math.pi * i / rings
        rows.append([Vector((math.sin(th) * math.cos(2 * math.pi * k / segs), math.sin(th) * math.sin(2 * math.pi * k / segs),
                             math.cos(th))) for k in range(segs + 1)])
    vv, faces = m.grid([[c + d * GLOBE_R for d in r] for r in rows], "planet_globe_glass", flip=True)
    lay = m.bm.loops.layers.uv.verify()
    where = {}
    for i, row in enumerate(vv):
        for k_, v_ in enumerate(row):
            where[v_] = (k_ / segs, 1.0 - i / rings)
    for f in faces:
        for lp in f.loops:
            lp[lay].uv = where[lp.vert]
    kill = [f for f in faces if open_at((f.calc_center_median() - c).normalized())]
    bmesh.ops.delete(m.bm, geom=kill, context="FACES")
    glass = m.make("RoGlobe glass", coll)

    e = Mesh()
    R = GLOBE_R + .012
    for z in (-1, 1):
        e.revolve([(.001, R * z), (.9, R * z * math.cos(.065)), (.001, (R + .12) * z)], "gold", 24,
                  Matrix.Translation(c))
    for g_, ang in gates:
        x = g_.cross(Vector((0, 0, 1))).normalized()
        y = g_.cross(x)
        pts = [c + (g_ * math.cos(ang) + (x * math.cos(t) + y * math.sin(t)) * math.sin(ang)) * GLOBE_R
               for t in (2 * math.pi * j / 48 for j in range(48))]
        e.tube(pts, .07, "gold", 8, closed=True)
    e.make("RoGlobe engraving", coll)
    return glass


# ---------------------------------------------------------------- the armillary (the user 11-19: "사진에 보이는 고리를
# 로비처럼 하라고" - the lobby's armillary sphere, ArtSource/Lobby/KingdomInterior/armillary.py): round gold tubes in
# pairs, balls at every pin, the broad ecliptic band with its marks. The builder's GlobeArmillary: the fixed frame
# (meridian 16.5 standing north-south, horizon 18) and three gimbals pinned one in the other at the globe's middle -
# A (two meridians at +-30 deg, radius 16, pinned to the meridian top and bottom), B (the ecliptic band, radius 15.5,
# 1 m wide, leaning 30 deg west up, pinned to A north and south), C (two rings 15.15, upright and turned 60 deg about
# north, pinned to B north and south). Unity axes in this block, Blender = (x, z, y).

def UV(x, y, z):
    return Vector((x, z, y))


def axis_q(deg_x=0.0, deg_y=0.0, deg_z=0.0, base=(0, 1, 0)):
    """A Unity Quaternion.Euler(x, y, z) applied to a Unity vector (Z, then X, then Y, as Unity composes)."""
    from mathutils import Euler
    v = Vector(base)
    v = Matrix.Rotation(math.radians(deg_z), 3, "Z") @ v
    v = Matrix.Rotation(math.radians(deg_x), 3, "X") @ v
    v = Matrix.Rotation(math.radians(deg_y), 3, "Y") @ v
    return v


def align_b(n):
    """Blender rotation taking +Z onto n (a Blender vector)."""
    return Vector((0, 0, 1)).rotation_difference(n.normalized()).to_matrix().to_4x4()


def tube_ring(m, F, R, r, n_unity, offset=0.0, segs=224):
    """A round gold tube of radius r on the circle R round the Unity axis n, its plane `offset` along the axis."""
    n = UV(*n_unity)
    M = F @ align_b(n) @ Matrix.Translation((0, 0, offset))
    m.revolve_uv([(R + r * math.cos(t), r * math.sin(t)) for t in (2 * math.pi * j / 8 for j in range(8))], "gold", segs, M,
                 2 * math.pi * r)


def pin(m, F, u_unity, r0, r1, rad=.07):
    """An axle along the Unity direction u from radius r0 (the child) out to r1 (the parent), balls at both ends, both ways."""
    u = UV(*u_unity).normalized()
    for sg in (-1, 1):
        d = u * sg
        M = F @ align_b(d)
        m.revolve([(.001, r0 - .05), (rad, r0 - .05), (rad, r1 + .05), (.001, r1 + .05)], "gold", 16, M)
        m.sphere(tuple(d * (r0 + .12)), .17, "gold", 16, 8, F, uv=False)
        m.sphere(tuple(d * (r1 - .04)), .12, "gold", 16, 8, F, uv=False)


UPRIGHT = (1, 0, 0)          # Unity Quaternion.Euler(0, 0, -90) * up: a ring standing north-south, its axis east-west


def armillary_fixed(coll):
    """The fixed frame (static, the section's frame): the meridian (a pair of tubes 0.36 apart, ties between them
    every 15 deg) standing north-south at 16.5, the horizon (a pair, one over the other) at 18, gold arms with balls
    where the meridian meets the horizon's plane north and south, an orb and cross on the meridian's top."""
    F = Matrix.Translation(U(*GLOBE))
    m = Mesh()
    for off in (-.24, .24):
        tube_ring(m, F, 16.5, .16, UPRIGHT, off)
    for k in range(24):
        a_ = 2 * math.pi * (k + .5) / 24
        p_ = UV(0, 16.5 * math.cos(a_), 16.5 * math.sin(a_))
        m.revolve([(.001, -.26), (.07, -.26), (.07, .26), (.001, .26)], "gold", 8,
                  F @ Matrix.Translation(p_) @ align_b(UV(*UPRIGHT)))
        if k % 2 == 0:
            m.sphere(tuple(p_ + UV(.42, 0, 0)), .1, "gold", 8, 4, F, uv=False)
            m.sphere(tuple(p_ + UV(-.42, 0, 0)), .1, "gold", 8, 4, F, uv=False)
    meridian = m.make("RoGlobe meridian", coll)
    h = Mesh()
    for off in (-.32, .32):
        tube_ring(h, F, 18.0, .18, (0, 1, 0), off)
    for k in range(36):
        a_ = 2 * math.pi * k / 36
        p_ = UV(18 * math.cos(a_), 0, 18 * math.sin(a_))
        h.revolve([(.001, -.34), (.07, -.34), (.07, .34), (.001, .34)], "gold", 8, F @ Matrix.Translation(p_))
    for sg in (-1, 1):                        # north / south: arms from the meridian out to the horizon, balls
        d = UV(0, 0, sg)
        h.revolve([(.001, 16.4), (.09, 16.4), (.09, 18.1), (.001, 18.1)], "gold", 12, F @ align_b(d))
        h.sphere(tuple(d * 16.5), .2, "gold", 16, 8, F, uv=False)
        h.sphere(tuple(d * 18.0), .3, "gold", 16, 8, F, uv=False)
    top = 16.5 + .12
    tm = F @ Matrix.Translation(UV(0, top, 0))
    h.revolve([(.001, 0), (.2, 0), (.14, .16), (.09, .26)], "gold", 20, tm)
    h.sphere((0, 0, .58), .32, "gold", 20, 10, tm, uv=False)
    h.box((0, 0, 1.2), (.08, .08, .55), "gold", tm)
    h.box((0, 0, 1.28), (.34, .08, .08), "gold", tm)
    horizon = h.make("RoGlobe horizon", coll)
    return meridian, horizon


A_R, B_R, C_R = 16.0, 15.5, 15.15
B_AXIS = axis_q(deg_z=-30)                    # Unity Euler(0, 0, -30) * up
A_AXES = [axis_q(deg_y=30, base=UPRIGHT), axis_q(deg_y=-30, base=UPRIGHT)]
C_AXES = [UPRIGHT, tuple(axis_q(deg_z=60, base=UPRIGHT))]


def gimbal_a(coll):
    """Gimbal A: two meridians crossing at 60 deg (round tubes, 0.12), pinned to the fixed meridian top and bottom."""
    F = Matrix.Translation(U(*GLOBE))
    m = Mesh()
    for ax in A_AXES:
        tube_ring(m, F, A_R, .2, tuple(ax))
    pin(m, F, (0, 1, 0), A_R + .2, 16.5 - .16, rad=.09)
    return m.make("RoGlobe gimbal A", coll)


def gimbal_b(coll):
    """Gimbal B: the broad ecliptic band (radius 15.5, 1 m wide, 0.12 thick, beads along both edges), its marks on the
    outer face - a short one every 7.5 deg, a long one with a ball every 30 - a thin colure across it through the pins,
    pinned to A north and south."""
    F = Matrix.Translation(U(*GLOBE))
    m = Mesh()
    n = UV(*B_AXIS)
    M = F @ align_b(n)
    w, t = 1.0, .12
    sec = [(-t / 2, -w / 2), (t / 2, -w / 2)]
    sec += [(t / 2 + .05 * math.sin(math.pi * j / 6), -w / 2 + .06 - .06 * math.cos(math.pi * j / 6)) for j in range(1, 6)]
    sec += [(t / 2, -w / 2 + .12), (t / 2, w / 2 - .12)]
    sec += [(t / 2 + .05 * math.sin(math.pi * j / 6), w / 2 - .06 - .06 * math.cos(math.pi * j / 6)) for j in range(1, 6)]
    sec += [(t / 2, w / 2), (-t / 2, w / 2)]
    m.revolve_uv([(B_R + dr, h_) for dr, h_ in sec], "gold", 224, M, w)
    for k in range(12):
        km = M @ Matrix.Rotation(2 * math.pi * k / 12, 4, "Z")
        for z in (-1, 1):
            m.sphere((B_R + t / 2 + .05, 0, z * (w / 2 - .06)), .07, "gold", 10, 5, km, uv=False)
    tube_ring(m, F, B_R, .07, tuple(axis_q(deg_z=-30, base=UPRIGHT)))   # the builder's "B colure": square to the band, through the pins
    pin(m, F, (0, 0, 1), B_R + .1, A_R - .2, rad=.09)
    return m.make("RoGlobe gimbal B", coll)


def gimbal_c(coll):
    """Gimbal C: two narrow rings (round tubes, 0.09) crossing at 60 deg about north, pinned to B north and south."""
    F = Matrix.Translation(U(*GLOBE))
    m = Mesh()
    for ax in C_AXES:
        tube_ring(m, F, C_R, .16, tuple(ax))
    pin(m, F, (0, 0, 1), C_R + .16, B_R - .06, rad=.07)
    return m.make("RoGlobe gimbal C", coll)


def meridian_and_horizon(coll):
    c = U(*GLOBE)
    m = Mesh()
    # Meridian, after RO_A3's meridian ring: a broad flat gold band (15.9 .. 17.1 m, 0.45 deep - the builder's hoop is
    # 16.25 .. 16.75) standing north-south (Unity's x is its axis), raised lips at both edges, degree marks engraved
    # in dark enamel on both faces (long every 5 deg, short between), and at north and south, where the horizon ring
    # rests on it, a clamp across the band and a round pivot boss. Its foot goes down into the base's top.
    across = Matrix.Translation(c) @ Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # local z -> x
    R0, R1, D = 16.1, 16.9, .25                  # between the ecliptic (to 15.95) and the horizon (from 17.2)
    sec = [(R0, -D - .05), (R0 + .1, -D - .05), (R0 + .13, -D), (R1 - .13, -D), (R1 - .1, -D - .05), (R1, -D - .05),
           (R1 + .04, -D + .03), (R1 + .04, D - .03), (R1, D + .05), (R1 - .1, D + .05), (R1 - .13, D), (R0 + .13, D),
           (R0 + .1, D + .05), (R0, D + .05), (R0 - .04, D - .03), (R0 - .04, -D + .03)]
    m.revolve(sec, "gold", 192, across, closed=True, smooth=True)
    for k in range(72):
        ang = 2 * math.pi * k / 72
        long_ = k % 2 == 0
        L = .5 if long_ else .28
        for z in (-1, 1):
            pm = across @ Matrix.Rotation(ang, 4, "Z")
            m.box(((R0 + R1) / 2 + (0 if long_ else .1), 0, z * (D + .004)), (L, .1 if long_ else .07, .012), "navy", pm)
    for k in range(12):                                      # a gold star on both faces every 30 deg
        pm = across @ Matrix.Rotation(2 * math.pi * (k + .5) / 12, 4, "Z")
        for z in (-1, 1):
            sm = pm @ Matrix.Translation(((R0 + R1) / 2, 0, z * (D + .02))) @ Matrix.Rotation(0 if z > 0 else math.pi, 4, "X")
            m.star3d(sm, .3, .19, .08, 4, .05, "gold")
    meridian = m.make("RoGlobe meridian", coll)

    h = Mesh()
    # Horizon, after RO_A3's horizon ring: a gold band 17 .. 19 m (the builder's), 1.2 m high (the builder's hoop is
    # 0.3 - the sheet's would be about 4 m, which would meet the east threshold and the camera on the west one), a
    # flat top with rolled edges, the outer face set back between rolled rims and embossed with eight-point stars and
    # small diamonds between them (as far apart as the sheet's, for the face's height), a plain inner face.
    hc = Matrix.Translation(c)
    # 2.2 m high: as tall as it can be under the east threshold (its underside 1.5 m over the ring's middle) - the
    # sheet's proportion would be about 4 m.
    RI, RO_, H2 = HORIZON_R - .8, HORIZON_R + .8, .8
    roll = .14
    top = [(RI, H2 - .08), (RI + .08, H2), (RO_ - .2, H2)]
    top += [(RO_ - .2 + roll * math.sin(t), H2 - roll + roll * math.cos(t)) for t in (math.pi * j / 8 for j in range(1, 9))]
    top += [(RO_ - .2, H2 - 2 * roll), (RO_ - .12, H2 - 2 * roll - .05)]
    h.revolve(top, "gold", 192, hc)
    h.revolve([(RO_ - .12, H2 - 2 * roll - .05), (RO_ - .12, -H2 + 2 * roll + .05)], "brass", 192, hc)      # the face, set back
    bot = [(RO_ - .12, -H2 + 2 * roll + .05), (RO_ - .2, -H2 + 2 * roll)]
    bot += [(RO_ - .2 + roll * math.sin(t), -H2 + roll - roll * math.cos(t)) for t in (math.pi * (8 - j) / 8 for j in range(0, 8))]
    bot += [(RO_ - .2, -H2), (RI + .08, -H2), (RI, -H2 + .08), (RI, H2 - .08)]
    h.revolve(bot, "gold", 192, hc)
    n = 32
    for k in range(n * 2):
        ang = 2 * math.pi * k / (n * 2)
        pm = (hc @ Matrix.Rotation(ang, 4, "Z") @ Matrix.Translation((RO_ - .12, 0, 0)) @ Matrix.Rotation(math.pi / 2, 4, "Y")
              @ Matrix.Rotation(math.pi / 2, 4, "Z"))
        if k % 2 == 0:
            h.star3d(pm, .66, .5, .24, 4, .2, "gold")
        else:
            h.star3d(pm, .17, 0, .06, 4, .08, "gold")
    horizon = h.make("RoGlobe horizon", coll)
    return meridian, horizon


def base(coll):
    """The globe's base, after RO_A3's sheet picture (measured off it, 11-19 - the user: the first one was far from
    the sheet): a low stepped dome 27 m across and 14 m high, from the bottom
      a gold foot flange with big gold beads round it, a marble tier sloping in to a thick gold ring,
      a navy enamel band with big faceted eight-point stars and small four-point sparkles between gold rims,
      a gold step (a broad gold ring between ledges), a marble dome cushion with gold scrolls between stars,
      a thick gold rim round a navy top with small stars, and in its middle the gold collar the glass sits in.
    Heights are metres over the hall floor; the collar's lip meets the glass (radius 14 round GLOBE)."""
    c = U(*GLOBE)
    at = Matrix.Translation((c.x, c.y, HALL_FLOOR))
    m = Mesh()
    SEG = 128

    W = 1.12

    def rev(prof, slot, smooth=True, wide=True):
        m.revolve([(r * W if wide else r, z) for r, z in prof], slot, SEG, at, smooth=smooth)

    def arc(r0, z0, r1, z1, bulge, n=8):
        return [(r0 + (r1 - r0) * j / n + bulge * math.sin(math.pi * j / n), z0 + (z1 - z0) * j / n) for j in range(n + 1)]

    def band(rc, z0, z1, out):
        """A rounded band: a half round `out` proud of rc between z0 and z1."""
        return [(rc + out * math.sin(math.pi * j / 8), z0 + (z1 - z0) * j / 8) for j in range(9)]

    # The profile was measured off the sheet's silhouette (its width at each height, the sheet seen from about 22 deg
    # up): radius 13.8 at the foot, 11 at the navy band, 9 at the gold step, 6.7 at the top rim. The heights are the
    # sheet's scaled by S so the top's collar meets the glass. Gold bands are crisp, flat-topped rings with ledges
    # between them (the steps the sheet shows from above), not round tori.
    S = 1.0

    def Z(z):
        return z * S

    def crisp(r, z0, z1, out=.1, lip=.07):
        """A gold band standing `out` proud of r from z0 to z1, its edges chamfered."""
        return [(r, Z(z0)), (r + out, Z(z0) + lip), (r + out, Z(z1) - lip), (r, Z(z1))]

    # Radii fitted to the sheet's silhouette (its width at each height of the picture, compared with a render of
    # this from the same height): the tiers step in more than a smooth dome would - navy band 10.4, gold step 8.7,
    # dome 7.3 to 5.4, top rim 5.45 (times W).
    # 1 The foot: a gold flange, its top a ledge the beads sit on.
    rev([(.001, 0.0), (13.8, 0.0), (13.8, Z(.2)), (13.7, Z(.32)), (13.2, Z(.34))], "gold")   # closed underneath: the hall below is an abyss (ro-shell 11-19)
    # 2 Marble tier, sloping in, rounding over at its top.
    rev(arc(13.2, Z(.34), 12.3, Z(3.45), .12, 10) + [(12.0, Z(3.68)), (11.6, Z(3.72))], "marble")
    # 3 A gold ring on it, and a flat gold ledge in to the navy band's foot.
    rev([(11.6, Z(3.72))] + crisp(11.6, 3.72, 4.45, .32, .18) + [(10.1, Z(4.48))], "gold", smooth=False)
    # 4 The navy band between gold rims (10.4 at its foot to 10.0 at its top).
    rev([(10.1, Z(4.48))] + crisp(10.1, 4.5, 4.93, .12) + [(9.9, Z(4.95))], "gold", smooth=False)
    rev([(9.9, Z(4.95)), (9.55, Z(7.75))], "navy")
    rev([(9.55, Z(7.75)), (9.7, Z(7.75))] + crisp(9.7, 7.77, 8.45, .14, .1) + [(8.2, Z(8.48))], "gold", smooth=False)
    # 5 The gold step: a broad ring, a ledge, a thin ring, a ledge.
    rev([(8.2, Z(8.48))] + crisp(8.2, 8.5, 9.7, .08, .1) + [(7.65, Z(9.73))] + crisp(7.65, 9.75, 10.05, .07) +
        [(7.2, Z(10.1))], "gold", smooth=False)
    # 6 The marble dome cushion: steep at its foot, rounding over to the top rim (7.3 -> 5.4).
    D_R, D_A, D_Z, D_H = 5.4, 1.8, Z(10.1), Z(12.3) - Z(10.1)
    rev([(D_R + D_A * math.cos(t), D_Z + D_H * math.sin(t)) for t in (math.pi / 2 * j / 16 for j in range(17))], "marble")
    # 7 The thick gold top rim, the navy top a little inside and below it.
    rev([(D_R, Z(12.3))] + crisp(5.45, 12.32, 13.0, .2, .1) + [(5.05, Z(13.05)), (5.0, Z(12.9))], "gold", smooth=False)
    top = Z(12.9)
    rev([(5.0, top), (2.95 / W, top)], "navy", smooth=False)
    # 8 The collar: a thick gold tube the glass sits in.
    zc = c.z - HALL_FLOOR - math.sqrt(GLOBE_R ** 2 - 2.4 ** 2) - .02      # the glass's height 2.4 m off the axis
    rev([(2.95, top), (3.05, top + .1), (3.05, zc - .45), (2.95, zc - .2), (2.75, zc - .03), (2.45, zc),
         (2.1, zc - .05), (1.95, zc - .25), (1.95, top)], "gold", wide=False)

    # Big beads round the foot, on the flange against the marble.
    nb = 58
    for k in range(nb):
        a = 2 * math.pi * (k + .5) / nb
        m.sphere((13.45 * W * math.cos(a), 13.45 * W * math.sin(a), Z(.34) + .34), .38, "gold", 12, 6, at, uv=False)

    # The navy band: 13 big eight-point stars (long and short points) and 13 four-point sparkles between.
    nst = 13
    slope = math.atan2((9.9 - 9.55) * W, Z(7.75) - Z(4.95))
    zm = (Z(4.95) + Z(7.75)) / 2
    rr = (9.9 + 9.55) / 2 * W
    for k in range(nst * 2):
        a = 2 * math.pi * k / (nst * 2)
        sm = (at @ Matrix.Rotation(a, 4, "Z") @ Matrix.Translation((rr, 0, zm)) @ Matrix.Rotation(-slope, 4, "Y")
              @ Matrix.Rotation(math.pi / 2, 4, "Y") @ Matrix.Rotation(math.pi / 2, 4, "Z"))
        if k % 2 == 0:
            m.star3d(sm, 1.3, 1.0, .62, 4, .42, "gold")
        else:
            m.star3d(sm, .4, 0, .14, 4, .16, "gold")

    # The dome cushion: 8 eight-point stars and in each gap a gold scroll running from one star toward the next and
    # curling under at its end (alternately left and right, as on the sheet).
    def on_dome(a, t, off=0.0):
        r = (D_R + D_A * math.cos(t)) * W
        z = D_Z + D_H * math.sin(t)
        n = Matrix.Rotation(a, 3, "Z") @ Vector((D_H * math.cos(t), 0, D_A * W * math.sin(t))).normalized()
        return at @ (Vector((r * math.cos(a), r * math.sin(a), z)) + n * off), n

    nds = 6
    tm = .5
    for k in range(nds):
        a = 2 * math.pi * k / nds
        p, n = on_dome(a, tm)
        x = Vector((-math.sin(a), math.cos(a), 0))
        y = n.cross(x)
        sm = Matrix((x, y, n)).transposed().to_4x4()
        sm.translation = p
        m.star3d(sm, 1.05, .8, .5, 4, .36, "gold")
        # The scroll, drawn in the dome's surface: u along it (metres, away from the star), v up. A stem that rises and
        # dips, then a spiral curl hanging under its end; a leaf off the stem.
        sgn = 1 if k % 2 == 0 else -1
        zc_ = D_Z + D_H * math.sin(tm)
        r_c = (D_R + D_A * math.cos(tm)) * W

        def put(u, v, off):
            ang = a + sgn * (1.25 + u) / r_c
            t = math.asin(max(-1, min(1, (zc_ + v - D_Z) / D_H)))
            return on_dome(ang, t, off)[0]

        path = []
        L = 2.2
        for j in range(16):
            u = L * j / 15
            path.append((u, .42 * math.sin(math.pi * u / L * 1.1) + .05, .3 - .12 * j / 15))
        cx, cy = L + .05, -.2
        for j in range(1, 24):
            th = math.pi * .55 - j / 23 * math.pi * 1.85         # round to the right and under, inward
            rr_ = .55 * (1 - .58 * j / 23)
            path.append((cx + rr_ * math.cos(th), cy + rr_ * math.sin(th), .18 - .1 * j / 23))
        m.tube_var([put(u, v, r * .55) for u, v, r in path], [r for _, _, r in path], "gold", 8)
        m.sphere(tuple(put(cx - .05, cy - .02, .12)), .22, "gold", 8, 4, uv=False)
        leaf = [(.6 + .55 * q, .34 - .6 * q + .15 * math.sin(math.pi * q), .2 * (1 - .7 * q)) for q in (j / 8 for j in range(9))]
        m.tube_var([put(u, v, r * .5) for u, v, r in leaf], [r for _, _, r in leaf], "gold", 6)

    # The navy top: small stars and dots in a ring.
    for k in range(24):
        a = 2 * math.pi * (k + .25) / 24
        rad = 4.0 if k % 2 == 0 else 4.85
        sm = at @ Matrix.Translation((rad * math.cos(a), rad * math.sin(a), top)) @ Matrix.Rotation(a, 4, "Z")
        if k % 2 == 0:
            m.star3d(sm, .42, .26 if k % 4 == 0 else 0, .12, 4 if k % 4 == 0 else 5, .1, "gold")
        else:
            m.sphere((0, 0, 0), .07, "gold", 8, 4, sm, uv=False)
    return m.make("RoGlobe base", coll)


# The door frame: the user chose the wide, low size (C, 11-19) and then "시트 처럼 정밀하게" - every part of RO_A3's
# globe door, in its proportions to the big frame's radius, set on that size.
GATE_STYLE = "C"


def gate_frame(coll, gate, side, name):
    """A gate's frame after RO_A3's globe door (without its leaves - the way stays open), 9 m high, 12.4 m wide, the
    doorway 6 m wide over the 7 m threshold. Measured off the sheet, as parts of the big frame's radius R:
      the big frame - a thick rolled gold rim (0.065 R), a fine bead inside it, a navy band 0.18 R wide with big
        eight-point stars and two rows of small sparkles, a gold bead at its inner edge; a big eight-point star on top
      the inner arch - a stepped gold moulding 0.16 R wide (bead, flat, big roll), violet soffit, resting on
        the columns' capitals and touching the band at its crown
      the columns - square pedestal, two tori, a plain shaft, a necking ring, an echinus and a wide square abacus
      the side - a gold-edged navy band with a sparkle from each capital out across the big frame to a gold flange and
        ball; below it a navy panel with a gold S-scroll (curl out at the top, curl in at the foot)
      a stepped gold plinth under it all.
    It stands on the threshold 0.5 m outside the gate (where the builder's hoop is); nothing is collided with."""
    fc = U(gate[0] + side * .5, gate[1], gate[2])
    # Local frame: x across the threshold (Unity z), y up from the threshold's top, z through the gate (Unity x).
    F = Matrix.Translation(fc) @ Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    m = Mesh()
    R, CY, D = 6.2, 2.6, .35                    # the big frame's radius and centre height, half the frame's depth
    B1, B0 = R - .52, R - .52 - 1.06            # the navy band: 5.68 .. 4.62
    SY, AI, AO = 3.2, 2.85, 3.75                # the inner arch: springing, inner and outer radius
    CX, CR = 3.3, .32                           # the columns' centres and shaft radius
    PT = .42                                    # the plinth's top
    big = F @ Matrix.Translation((0, CY, 0))
    a0 = math.asin((PT - CY) / R)
    a1 = math.pi - a0

    def both(profile):
        """A front-face profile (r, z > 0) and its mirror on the back face."""
        return profile, [(r, -z) for r, z in profile][::-1]

    def rev2(profile, slot, mtx, lo, hi, segs=64, smooth=True):
        f, bk = both(profile)
        m.revolve(f, slot, segs, mtx, a0=lo, a1=hi, smooth=smooth)
        m.revolve(bk, slot, segs, mtx, a0=lo, a1=hi, smooth=smooth)

    # -- the big frame
    rim = [(B1 + .02, D + .03), (B1 + .06, D + .07), (B1 + .1, D + .03), (R - .4, D + .03), (R - .36, D + .1),
           (R - .24, D + .14), (R - .1, D + .1), (R - .02, D + .04), (R, 0)]
    rev2(rim, "gold", big, a0, a1)
    YB = SY - .38                               # the side bands' middle
    b_lo = math.asin((YB + .23 - CY) / B0)
    rev2([(B0, D), (B1 + .02, D)], "navy", big, b_lo, math.pi - b_lo, smooth=False)
    rev2([(B0 - .14, D - .02), (B0 - .1, D + .05), (B0 - .03, D + .06), (B0, D)], "gold", big, b_lo, math.pi - b_lo)
    m.revolve([(B0 - .14, D - .02), (B0 - .14, -D + .02)], "gold", 64, big, a0=b_lo, a1=math.pi - b_lo)     # inner edge
    # stars on the band: six big eight-point stars, sparkles in two rows between them (both faces)
    rm = (B0 + B1) / 2
    for k in range(7):
        t = math.radians(22 + 136 * k / 6)
        if abs(t - math.pi / 2) < .15:
            continue
        for zf in (1, -1):
            sm = big @ Matrix.Translation((rm * math.cos(t), rm * math.sin(t), zf * D)) @ Matrix.Rotation(0 if zf > 0 else math.pi, 4, "X")
            m.star3d(sm, .46, .33, .15, 4, .16, "gold")
        for t2, rr in ((t + math.radians(11), B0 + .28), (t + math.radians(11), B1 - .26)):
            if t2 > math.radians(160) or abs(t2 - math.pi / 2) < .12:
                continue
            for zf in (1, -1):
                sm = big @ Matrix.Translation((rr * math.cos(t2), rr * math.sin(t2), zf * D)) @ Matrix.Rotation(0 if zf > 0 else math.pi, 4, "X")
                m.star3d(sm, .12, 0, .045, 4, .06, "gold")
    # the big star on top, both faces, on a boss through the frame
    ks = big @ Matrix.Translation((0, R - .25, 0))
    m.revolve([(.001, -D - .12), (.55, -D - .12), (.62, -D), (.62, D), (.55, D + .12), (.001, D + .12)], "gold", 24, ks)
    for zf in (1, -1):
        m.star3d(ks @ Matrix.Rotation(0 if zf > 0 else math.pi, 4, "X") @ Matrix.Translation((0, 0, D + .12)), 1.3, .8, .4, 4, .42, "gold")

    # -- the inner arch: violet soffit, a stepped gold moulding on each face, a gold back
    arch = F @ Matrix.Translation((0, SY, 0))
    m.revolve([(AI, -D - .05), (AI, D + .05)], "violet", 48, arch, a0=0, a1=math.pi, smooth=False)
    mould = [(AI, D + .05), (AI + .05, D + .16), (AI + .14, D + .2), (AI + .22, D + .16), (AI + .26, D + .18),
             (AI + .5, D + .18), (AI + .54, D + .22), (AI + .64, D + .32), (AI + .76, D + .34), (AO - .06, D + .24),
             (AO - .02, D + .12), (AO, D + .02)]
    rev2(mould, "gold", arch, 0, math.pi, 48)
    m.revolve([(AO, D + .02), (AO, -D - .02)], "gold", 48, arch, a0=0, a1=math.pi)
    # navy behind it, out to the band (the spandrel), set back a little
    ring_c = Vector((0, CY))
    rows_a, rows_b = [], []
    for j in range(33):
        t = -.12 + (math.pi + .24) * j / 32
        A = Vector((AO * math.cos(t), SY + AO * math.sin(t)))
        Bp = ring_c + (A - ring_c).normalized() * (B0 - .1)
        for zf, ra, rb in ((1, rows_a, rows_b),):
            ra.append(F @ Vector((A.x, A.y, D - .04)))
            rb.append(F @ Vector((Bp.x, Bp.y, D - .04)))
    m.grid([rows_a, rows_b], "navy", smooth=False)
    m.grid([[q - F.to_3x3() @ Vector((0, 0, 2 * D - .08)) for q in rows_b], [q - F.to_3x3() @ Vector((0, 0, 2 * D - .08)) for q in rows_a]],
           "navy", smooth=False)

    for sx in (-1, 1):
        # -- the column
        cm = F @ Matrix.Translation((sx * CX, 0, 0)) @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
        m.box((sx * CX, PT + .26, 0), (1.15, .52, 2 * D + .3), "gold", F)                  # pedestal
        m.box((sx * CX, PT + .56, 0), (1.0, .08, 2 * D + .2), "gold", F)
        m.revolve([(.001, PT + .6), (.48, PT + .6), (.5, PT + .66), (.48, PT + .76), (.4, PT + .8), (.37, PT + .86),
                   (.43, PT + .92), (.43, PT + .98), (.38, PT + 1.02), (CR, PT + 1.08), (CR, SY - .72), (.36, SY - .68),
                   (.38, SY - .62), (.36, SY - .56), (CR + .02, SY - .52), (.4, SY - .44), (.5, SY - .34), (.55, SY - .3),
                   (.001, SY - .3)], "gold", 24, cm)
        m.box((sx * CX, SY - .2, 0), (1.3, .2, 2 * D + .36), "gold", F)                   # abacus
        m.box((sx * CX, SY - .05, 0), (1.42, .1, 2 * D + .44), "gold", F)
        # -- the side band from the capital out across the big frame, its flange and ball
        yb = SY - .38
        x_in = CX + .65
        x_out = math.sqrt(R ** 2 - (yb - CY) ** 2) - .05
        w = x_out - x_in
        xm = sx * (x_in + x_out) / 2
        m.box((xm, yb, 0), (w, .3, 2 * D + .14), "navy", F)
        m.box((xm, yb + .19, 0), (w, .08, 2 * D + .22), "gold", F)
        m.box((xm, yb - .19, 0), (w, .08, 2 * D + .22), "gold", F)
        for zf in (1, -1):
            m.star3d(F @ Matrix.Translation((xm, yb, zf * (D + .07))) @ Matrix.Rotation(0 if zf > 0 else math.pi, 4, "X"),
                     .13, 0, .05, 4, .06, "gold")
        km = F @ Matrix.Translation((sx * (R - .05), yb, 0)) @ Matrix.Rotation(sx * math.pi / 2, 4, "Y")
        m.revolve([(.001, 0), (.58, 0), (.6, .06), (.58, .16), (.4, .2), (.32, .28), (.001, .3)], "gold", 24, km)
        m.sphere((0, 0, .66), .44, "gold", 20, 10, km, uv=False)
        # -- the side panel below the band: navy from the column's pedestal to the outer rim (both faces)
        x_col = CX + .58
        y_top = yb - .23
        rows = []
        for j in range(13):
            y = PT + (y_top - PT) * j / 12
            rows.append((y, math.sqrt(max(0, (B1 + .03) ** 2 - (y - CY) ** 2))))
        for zf in (1, -1):
            pts_in = [F @ Vector((sx * x_col, y, zf * D)) for y, _ in rows]
            pts_out = [F @ Vector((sx * xr, y, zf * D)) for y, xr in rows]
            m.grid([pts_in, pts_out] if (sx * zf) > 0 else [pts_out, pts_in], "navy", smooth=False)
        # The scroll, as the sheet draws it (measured on a 7x crop of the sheet's side panel), in metres on a panel
        # 0.6 wide (x from its outer edge toward the column) and 2.1 high, set in the middle of this panel at its
        # height's scale (the panel is wider than the sheet's):
        #   a stem rising from a small hook at its foot, bending out at the top over a spiral that curls inward
        #   (counter-clockwise from the stem) by the big frame; from the stem's middle a branch rising toward the
        #   column and curling down into a small spiral; a short twig with a bud toward the frame; a sprout above it.
        # Traced off the sheet's side panel (a 7x crop, its pixels; the panel 220 px wide from the rim to the column,
        # 595 px high): each line a centre line from its root, with its half-thickness in pixels.
        PX = .6 / 220                                            # sheet pixels -> "sheet metres" (panel 0.6 wide)
        hgt = y_top - PT - .2
        k = hgt / (500 * PX)                                     # the scroll (y 227 .. 720) fills the panel's height
        x_mid = (x_col + rows[6][1]) / 2

        def P(xp, yp, z0):
            """Sheet crop pixel (x from the left = the rim side, y down) -> the frame's local point."""
            xm, ym = (xp - 210) * PX, (720 - yp) * PX           # x about the scroll's middle, y from below its foot
            return F @ Vector((sx * (x_mid - xm * k * 1.15), PT + .1 + ym * k, z0))      # widened: the panel is wider

        def smooth(path, n=4):
            """Catmull-Rom through the traced points (x, y, half-thickness), n steps a span."""
            out = []
            for i in range(len(path) - 1):
                p0, p1, p2, p3 = path[max(i - 1, 0)], path[i], path[i + 1], path[min(i + 2, len(path) - 1)]
                for j in range(n):
                    t = j / n
                    out.append(tuple(.5 * (2 * p1[c] + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t * t
                                           + (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t ** 3) for c in range(3)))
            out.append(path[-1])
            return out

        # the stem, from its foot (a little hook) up the S, over the top and into the curl's eye
        stem = [(272, 662, 5), (270, 685, 7), (250, 675, 9), (228, 650, 10), (210, 605, 11), (206, 560, 12),
                (214, 520, 12), (232, 485, 12), (252, 450, 12), (270, 410, 12), (283, 360, 13), (285, 310, 13),
                (272, 265, 13), (245, 238, 13), (205, 227, 13), (165, 232, 12), (135, 252, 11), (124, 282, 10),
                (132, 310, 9), (155, 325, 8), (178, 318, 7), (185, 300, 6), (175, 288, 5), (165, 295, 5)]
        branch = [(214, 520, 10), (238, 503, 10), (265, 497, 9), (287, 507, 8), (296, 528, 7), (288, 548, 6), (272, 550, 5),
                  (265, 538, 5)]
        twig = [(222, 476, 9), (200, 466, 9)]
        sprout = [(268, 396, 7), (252, 384, 6), (244, 374, 5)]
        for zf in (1, -1):
            z0 = zf * D
            for path in (stem, branch, twig, sprout):
                path = smooth(path, 2)
                rk = [r * 1.6 * PX * k for _, _, r in path]      # as bold as the sheet draws them
                m.tube_var([P(x, y, z0 + zf * r * .6) for (x, y, _), r in zip(path, rk)], rk, "gold", 6)
            m.sphere(tuple(P(180, 461, z0 + zf * 12 * PX * k * .6)), 22 * PX * k, "gold", 12, 6, uv=False)   # the bud
            m.sphere(tuple(P(167, 296, z0 + zf * 5 * PX * k * .6)), 7 * PX * k, "gold", 10, 5, uv=False)     # eyes
            m.sphere(tuple(P(267, 540, z0 + zf * 5 * PX * k * .6)), 6 * PX * k, "gold", 10, 5, uv=False)

    # -- the stepped plinth
    px = math.sqrt(R ** 2 - (PT - CY) ** 2) + .2
    m.box((0, .11, 0), (2 * px + .3, .22, 2 * D + .6), "gold", F)
    m.box((0, .32, 0), (2 * px, .2, 2 * D + .4), "gold", F)
    m.box((0, .325, 0), (2 * px + .04, .04, 2 * D + .44), "navy", F)
    return m.make(name, coll)


def tilted_floor(coll, frame):
    """The floor in Device_SolarFlare's frame (its top is the builder's walking face, y 0), as the concept draws it
    (RO_1, RO_3 - the user, 11-19: "컨셉 아트 처럼", in place of the motif atlas): a cream marble disc inlaid in gold
    with a star chart - a sixteen-point compass star in the middle (its rays two-toned gold / brass along their
    ridges, as faceted gold reads), thin concentric circles and radial lines, four great off-centre arcs crossing,
    small eight-point stars where lines meet, a gold band round the edge. The builder's three shadow tracks stay,
    broader gold circles. All inlay lies 2-3 mm proud at most; the brass rim and the ribs under it."""
    m = Mesh()
    R = FLOOR_R
    # the marble top (rings of quads, so the inlay has something to sit on in every light)
    m.revolve([(.001, 0)] + [(R * j / 14, 0) for j in range(1, 15)], "marble", 192, smooth=False)

    def line(pts, w, h, slot="gold"):
        """A flat inlay strip along a polyline at height h."""
        left, right = [], []
        for i in range(len(pts)):
            a_ = pts[max(i - 1, 0)]
            b_ = pts[min(i + 1, len(pts) - 1)]
            t = (b_ - a_).normalized()
            nrm = Vector((-t.y, t.x, 0)) * (w / 2)
            left.append(Vector((pts[i].x, pts[i].y, h)) + nrm)
            right.append(Vector((pts[i].x, pts[i].y, h)) - nrm)
        m.grid([right, left], slot, smooth=False)

    def circle(cx, cy, r, w, h, clip=R - .32):
        """A circle (or the part of it inside the floor), as strips."""
        run = []
        for j in range(181):
            t = 2 * math.pi * j / 180
            q = Vector((cx + r * math.cos(t), cy + r * math.sin(t), 0))
            if q.length <= clip:
                run.append(q)
            elif len(run) > 1:
                line(run, w, h)
                run = []
            else:
                run = []
        if len(run) > 1:
            line(run, w, h)

    # inlay heights (11-20, flicker in Unity at 2-3 mm, the user via ro-path "여기도 깜빡거려"): layers that cross
    # 8 mm apart - arcs and edge band 0.6 cm, circles 1.4, radials 2.2, tracks 3, rays 3.8, stars 4.2, boss 4.6
    # the builder's shadow tracks: broad gold circles (14 cm), the walk's guide
    for t in TRACKS:
        circle(0, 0, t, .17, .03)
    # thin concentric circles and radials
    for r in (1.7, 5.1, 8.6, 12.1):
        circle(0, 0, r, .08, .014)
    for j in range(16):
        t = 2 * math.pi * j / 16
        d = Vector((math.cos(t), math.sin(t), 0))
        line([d * (2.2 if j % 2 else 4.8), d * (R - .32)], .07, .022)
    # four great arcs, off centre, crossing (the chart's projection lines)
    for j in range(6):
        t = math.pi / 6 + j * math.pi / 3
        circle(11.0 * math.cos(t), 11.0 * math.sin(t), 11.6 if j % 2 else 13.0, .065, .006)
    # the gold band round the edge (flush) and a fine line inside it
    m.revolve([(R - .3, .006), (R, .006)], "gold", 192, smooth=False)
    circle(0, 0, R - .52, .04, .014, clip=R)

    # the compass star: eight long rays and eight short, each ray two triangles - gold one side of its ridge, brass
    # the other, as the concept's faceted gold reads from above
    def ray(angle, length, half_w, h):
        d = Vector((math.cos(angle), math.sin(angle), 0))
        nrm = Vector((-d.y, d.x, 0))
        tip = d * length
        c0 = Vector((0, 0, 0))
        for side, slot in ((1, "gold"), (-1, "brass")):
            base = nrm * half_w * side
            f = [Vector((q.x, q.y, h)) for q in ((c0, tip, base) if side > 0 else (c0, base, tip))]
            m.face(f, slot)
    # one fan, no ray over another (11-20: separate rays crossed near the middle - flicker in Unity)
    tips = []
    for j in range(16):
        long_ = j % 2 == 0
        tips.append((2 * math.pi * j / 16, (4.6 if j % 4 == 0 else 3.4) if long_ else 2.0))
    n_ = len(tips)
    for j, (a_, L) in enumerate(tips):
        v0 = Vector((.75 * math.sin(a_ - math.pi / n_), .75 * math.cos(a_ - math.pi / n_), .038))
        v1 = Vector((.75 * math.sin(a_ + math.pi / n_), .75 * math.cos(a_ + math.pi / n_), .038))
        tip = Vector((L * math.sin(a_), L * math.cos(a_), .038))
        c0 = Vector((0, 0, .038))
        m.face([c0, tip, v0], "gold")
        m.face([c0, v1, tip], "brass")
    m.revolve([(.001, .046), (.42, .046)], "gold", 24, smooth=False)
    circle(0, 0, .75, .07, .044, clip=R)

    # small eight-point stars where lines meet (seeded), flat, two-toned like the big one
    r_ = rng("RoGlobe floor stars")
    spots = []
    for rr in (5.1, 8.6, 12.1):
        for j in range(16):
            if r_.random() < .55:
                t = 2 * math.pi * j / 16
                spots.append((rr * math.cos(t), rr * math.sin(t), r_.uniform(.5, .85)))
    for (x, y, size) in spots:
        for j in range(8):
            t = 2 * math.pi * j / 8
            L = size if j % 2 == 0 else size * .55
            d = Vector((math.cos(t), math.sin(t), 0))
            nrm = Vector((-d.y, d.x, 0))
            c0 = Vector((x, y, 0))
            for side, slot in ((1, "gold"), (-1, "brass")):
                base = c0 + nrm * size * .16 * side
                f = [Vector((q.x, q.y, .042)) for q in ((c0, c0 + d * L, base) if side > 0 else (c0, base, c0 + d * L))]
                m.face(f, slot)

    # rim and underside
    m.revolve([(R, 0), (R + .04, -.04), (R + .04, -.12), (R, -.16), (R, -.42), (R + .06, -.46), (R, -.5)], "gold", 192)
    m.revolve([(R, -.5), (.001, -.5)], "marble", 192, smooth=False)
    for q in range(24):
        pm = Matrix.Rotation(2 * math.pi * q / 24, 4, "Z")
        prof = [(1.6 + (R - 1.9) * t, -.5 - .55 * math.sin(math.pi * min(1, t * 1.15)) ** .6) for t in (j / 12 for j in range(13))]
        m.grid([[pm @ Vector((x, sg * .06, z)) for x, z in prof] for sg in (-1, 1)], "brass", smooth=False)
        m.grid([[pm @ Vector((x, -.06, -.5)) for x, z in prof], [pm @ Vector((x, -.06, z)) for x, z in prof]], "brass", smooth=False)
        m.grid([[pm @ Vector((x, .06, z)) for x, z in prof], [pm @ Vector((x, .06, -.5)) for x, z in prof]], "brass", smooth=False)
    m.revolve([(1.7, -.5), (1.6, -.9), (1.0, -1.3), (.5, -1.8), (.25, -2.6), (.001, -2.9)], "gold", 32)
    ob = m.make("RoGlobe floor", coll, up=(0, 0, 1))
    ob.parent = frame
    ob.matrix_parent_inverse = Matrix()
    ob.matrix_basis = Matrix()
    return ob, 0


def sun_and_rods(coll, frame):
    """The sun lamp after RO_A3's sun lamp, hanging in the floor's frame 8 m over its middle:
      "RoGlobe sun glass" - the pearl globe (2.2 m), the lamp's light and glow go on it (pearl glows; a point light
        from the lamp list) - and "RoGlobe sun" - its gold cage (a ring round it facing the gates, the equator and
        the meridian across its face, gold knobs where they cross), eight spear spikes in the cage's plane (the
        sheet's: two pairs each side, none straight up or down), a collar and a ball on top. Both ride the builder's
        "Sun lamp", which swells in the flare; the cage swells with the glass.
      "RoGlobe rods" (still): the rod from the glass's top it hangs on, the sheet's foot under it (a stepped neck, a
        navy drum with gold stars, a stepped base) where the rod goes on down to the orbits' hubs, and the orbits'
        gold wires (the builder's hoops)."""
    # The user (11-19, a crop of RO_1 beside "이렇게 태양 처럼 해주라고"): no cage and no spikes - a radiant orb, two thin
    # glowing rings round it tilted like orbits, the rod through it with gold knobs above and below.
    s_ = Matrix.Translation((0, 0, SUN_H))
    g = Mesh()
    g.sphere((0, 0, 0), 2.2, "planet_sun", 64, 32, s_)
    glass = g.make("RoGlobe sun glass", coll, frame)
    glass.matrix_parent_inverse = Matrix()
    glass.matrix_basis = Matrix()

    # The halo rings: shown only in the flare (the user, 11-19: "고리는 vfx 효과 할 때 연출로서 해 평상시 때는 고리 없이") -
    # their own object, "RoGlobe halo", which RoyalObservatoryDressing.Globe.cs shows and turns in the warning
    # and the burn. Pearl (it glows), a fine gold edge each side.
    h = Mesh()
    for rr, tx, ty in ((3.0, 14, 0), (3.7, -9, 22)):
        hm = s_ @ Matrix.Rotation(math.radians(tx), 4, "X") @ Matrix.Rotation(math.radians(ty), 4, "Y")
        ring(h, rr, ring_section(.2, .07, .02), "pearl", 128, hm)
        for dz in (-.04, .04):
            ring(h, rr, [(dr, z + dz) for dr, z in ring_section(.04, .02, .005)], "gold", 128, hm)
    halo = h.make("RoGlobe halo", coll, frame)
    halo.data.transform(Matrix.Translation((0, 0, -SUN_H)))     # its origin the sun's centre: it spins about it
    halo.matrix_parent_inverse = Matrix()
    halo.matrix_basis = Matrix.Translation((0, 0, SUN_H))
    m = Mesh()
    # the knobs where the rod enters and leaves the orb: a collar, a ring and a ball above, the same below
    for sgn in (1, -1):
        prof = [(.001, 2.12), (.42, 2.14), (.46, 2.22), (.3, 2.3), (.2, 2.42), (.26, 2.5), (.26, 2.56), (.16, 2.62), (.16, 2.75)]
        m.revolve([(r_, sgn * z) for r_, z in prof] if sgn > 0 else [(r_, sgn * z) for r_, z in prof][::-1], "gold", 24, s_)
        m.sphere((0, 0, sgn * 3.0), .26, "gold", 16, 8, s_, uv=False)
        m.revolve([(.001, sgn * 3.2), (.1, sgn * 3.25), (.001, sgn * 3.5)][::sgn], "gold", 12, s_)
    sun = m.make("RoGlobe sun", coll, frame)
    sun.matrix_parent_inverse = Matrix()
    sun.matrix_basis = Matrix()

    r = Mesh()
    top = DEPTH + GLOBE_R
    r.tube([Vector((0, 0, SUN_H + 3.5)), Vector((0, 0, top - .2))], .14, "brass", 12)
    for z in (SUN_H + 4.0, (SUN_H + top) / 2 + 1, top - 1.2):
        r.revolve([(.14, z - .12), (.26, z - .07), (.26, z + .07), (.14, z + .12)], "gold", 16)
    r.revolve([(.14, top - .9), (1.4, top - .2), (1.4, top - .05), (.001, top - .05)], "gold", 48)
    # the sheet's foot, under the glass: stepped neck, navy drum with gold stars, stepped base
    z0 = SUN_H - 2.34
    foot = [(.001, z0), (.8, z0), (.8, z0 - .1), (.62, z0 - .16), (.6, z0 - .26), (.42, z0 - .32), (.36, z0 - .42),
            (.42, z0 - .5), (.66, z0 - .56), (.7, z0 - .64), (.62, z0 - .68)]
    r.revolve(foot, "gold", 32)
    zd = z0 - .68
    r.revolve([(.62, zd), (.62, zd - .55)], "navy", 32)
    r.revolve([(.62, zd - .55), (.78, zd - .58), (.8, zd - .66), (.98, zd - .7), (1.0, zd - .8), (.001, zd - .8)], "gold", 32)
    for q in range(6):
        t = 2 * math.pi * q / 6
        pm = (Matrix.Rotation(t, 4, "Z") @ Matrix.Translation((.62, 0, zd - .275)) @ Matrix.Rotation(math.pi / 2, 4, "Y")
              @ Matrix.Rotation(math.pi / 2, 4, "Z"))
        r.star3d(pm, .22, .16, .07, 4, .06, "gold")
    r.tube([Vector((0, 0, zd - .8)), Vector((0, 0, HUBS[0] - .3))], .1, "brass", 12)
    r.revolve([(.001, HUBS[0] - .3), (.16, HUBS[0] - .35), (.1, HUBS[0] - .55), (.001, HUBS[0] - .7)], "gold", 16)
    rods = r.make("RoGlobe rods", coll, frame)
    rods.matrix_parent_inverse = Matrix()
    rods.matrix_basis = Matrix()
    # Orbit lines: the builder's hoops, as thin gold wires (with the orbits, part 8).
    w = Mesh()
    for (rad, hgt, body, cnt) in ORBITS:
        ring(w, rad, ring_section(.07, .07, .015), "gold", 128, Matrix.Translation((0, 0, hgt)))
    wires = w.make("RoGlobe orbit wires", coll, frame)
    wires.matrix_parent_inverse = Matrix()
    wires.matrix_basis = Matrix()
    return sun, rods


# Each orbit's hub on the rod under the sun's foot (floor-frame heights): the outermost lowest, so every arm passes
# under the planets of the orbits inside it (orbit 1's arm is 2.5 m up where orbit 2's planets hang down to 2.95).
HUBS = (3.0, 3.25, 3.6)


def orbit(coll, frame, i):
    """Device_PlanetOrbit i after RO_A3's orrery: a gold collar on the rod, and for each planet a gold arm sweeping
    out from it (curving round in plan, as the sheet's arms do), a ball where it turns up, a short stalk and a gold
    cup the planet sits in, a gold ball on the planet's top; the planets wear the Codex surfaces (RO_P1 .. P7),
    Saturn its ring (RO_P8). All in the device's frame (it turns about the floor's normal through the rod)."""
    rad, hgt, body, cnt = ORBITS[i]
    dev = empty("Device_PlanetOrbit %d" % (i + 1), coll, FLOOR_M @ Matrix.Translation((0, 0, hgt)), frame)
    m = Mesh()
    hz = HUBS[i] - hgt                                        # the hub's height in the device's frame
    m.revolve([(.11, hz - .2), (.3, hz - .16), (.36, hz - .06), (.36, hz + .06), (.3, hz + .16), (.11, hz + .2)], "gold", 24,
              closed=True)
    for q in range(cnt):
        a = math.radians(q * 360 / cnt)
        d = Vector((math.sin(a), math.cos(a), 0))             # Toward(yaw): Unity (sin, 0, cos) -> Blender (sin, cos, 0)
        side = Vector((-d.y, d.x, 0))
        cup_z = -body - .22                                    # the cup under the planet
        stalk_foot = d * rad + Vector((0, 0, cup_z - .35))
        # the arm: leaves the collar sideways and sweeps round and out to under the planet (a quadratic curve)
        p0 = (d * .3 - side * .2) + Vector((0, 0, hz))
        p1 = d * (rad * .45) - side * (rad * .55) + Vector((0, 0, (hz + stalk_foot.z) / 2))
        p2 = stalk_foot
        pts = [p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t * t for t in (j / 16 for j in range(17))]
        m.tube(pts, .13 + .04 * body, "gold", 10)
        m.sphere(tuple(stalk_foot), .26 + .06 * body, "gold", 14, 7, uv=False)
        m.tube([stalk_foot, d * rad + Vector((0, 0, cup_z))], .12, "gold", 10)
        cm = Matrix.Translation(d * rad + Vector((0, 0, cup_z)))
        m.revolve([(.001, -.06), (.16, -.06), (.3, .02), (.42, .16), (.44, .24), (.38, .26)], "gold", 20, cm)
        name = PLANETS[i][q]
        m.sphere(tuple(d * rad), body, "planet_" + name, 48, 24)
        tm = Matrix.Translation(d * rad + Vector((0, 0, body - .03)))
        m.revolve([(.001, 0), (.14, 0), (.14, .06), (.06, .1), (.06, .14), (.001, .14)], "gold", 12, tm)
        m.sphere((0, 0, .26), .14, "gold", 12, 6, tm, uv=False)
        if name == "saturn":
            tilt = Matrix.Translation(d * rad) @ Matrix.Rotation(math.radians(24), 4, "X")
            _, f1 = m.revolve([(body * 1.3, 0), (body * 2.0, 0)], "planet_saturn_ring", 64, tilt, smooth=False)
            _, f2 = m.revolve([(body * 2.0, -.005), (body * 1.3, -.005)], "planet_saturn_ring", 64, tilt, smooth=False)
            m.uv_radial(f1 + f2, (0, 0), body * 1.3, body * 2.0, tilt)
    ob = m.make("RoGlobe orbit %d" % (i + 1), coll, dev)
    ob.matrix_parent_inverse = Matrix()
    ob.matrix_basis = Matrix()
    return ob


def ecliptic_and_moons(coll, frame):
    """The ecliptic ring (in the floor's plane, its top flush with it so it reads as an inlay where it crosses the
    thresholds) and Device_EclipseMoon's two moons, each inside its 0.4 m disc."""
    # The ecliptic as the concept draws it (RO_1, RO_3): a broad gold track round the glass in the floor's plane,
    # its top flush with that plane (where it crosses the thresholds it reads as an inlay), a groove down its middle
    # that the moons ride in (their 0.4 m discs pass through it), pearl studs along its outer rail (they glow, as the
    # concept's do - the lamp list gives them light), and twelve navy and gold medallions on its outer face.
    m = Mesh()
    E = 14.35          # (was ECLIPTIC_R 15.6: inside the gimbals now, just outside the glass)
    G0, G1 = E - .12, E + .12                    # the groove (decorative: the moons are gone)
    T0, T1 = E - .35, E + .35                    # the track (between the colure and the meridian)
    prof = [(G0, 0), (G0 - .02, -.03), (G0 - .02, -.36), (G1 + .02, -.36), (G1 + .02, -.03), (G1, 0), (T1 - .06, 0),
            (T1, -.04), (T1 + .06, -.12), (T1 + .06, -.2), (T1, -.26), (T1, -.42), (T1 + .04, -.48), (T1 - .04, -.56),
            (T0 + .04, -.56), (T0 - .04, -.48), (T0, -.42), (T0, -.26), (T0 - .06, -.2), (T0 - .06, -.12), (T0, -.04),
            (T0 + .06, 0)]
    m.revolve(prof, "gold", 256, closed=True)
    m.revolve([(G0 + .015, -.36), (G1 - .015, -.36)], "navy", 256, smooth=False)        # the groove's floor
    for k in range(72):
        t = 2 * math.pi * (k + .5) / 72
        if k % 6 == 3:
            continue                                                                    # the medallions' places
        pm = (Matrix.Rotation(t, 4, "Z") @ Matrix.Translation((T1 + .06, 0, -.16)) @ Matrix.Rotation(math.pi / 2, 4, "Y"))
        m.revolve([(.001, .0), (.1, .0), (.1, .02), (.06, .06), (.001, .075)], "pearl", 8, pm)   # a pearl bead
        m.revolve([(.1, -.01), (.13, .0), (.13, .03), (.1, .03)], "gold", 8, pm, closed=True)
    for k in range(12):
        pm = Matrix.Rotation(2 * math.pi * (6 * k + 3.5) / 72, 4, "Z") @ Matrix.Translation((T1, 0, -.34))
        pm = pm @ Matrix.Rotation(math.pi / 2, 4, "Y")
        m.revolve([(.001, 0), (.2, 0), (.2, .03), (.001, .03)], "navy", 16, pm)
        m.revolve([(.2, 0), (.24, .02), (.2, .05), (.17, .04)], "gold", 16, pm, closed=True)
        m.star(pm @ Matrix.Translation((0, 0, .03)), .15, .05, 8 if k % 2 else 5, .025, "gold")
    eco = m.make("RoGlobe ecliptic", coll, frame)
    eco.matrix_parent_inverse = Matrix()
    eco.matrix_basis = Matrix()

    return eco, []      # the moons are gone (the user 11-19: "달 지워주고")
    dev = empty("Device_EclipseMoon", coll, FLOOR_M, frame)
    out = []
    for side, nm in ((-1, "Moon A"), (1, "Moon B")):
        # The disc's frame: its axis along the ring's radius (local x), its middle 0.2 below the ring.
        dm = Matrix.Translation((side * ECLIPTIC_R, 0, -.2)) @ Matrix.Rotation(math.pi / 2, 4, "Y")
        mm = Mesh()
        relief = moon_relief()

        # As the concept draws them (RO_1, RO_3): thick cream moon-stone discs, rounded at the edge, the crater face
        # of RO_P9 on both sides (craters also pressed into the surface a little), no metal. Inside the builder's
        # disc (6.8 m across, 0.4 thick): the faces at +-0.19, the edge rounded off to the 3.4 m rim.
        RF = MOON_R - .2                                  # where the face turns into the rounded edge
        inv = dm.inverted()
        lay = mm.bm.loops.layers.uv.verify()

        def disc_uv(f, flip):
            for lp in f.loops:
                q = inv @ lp.vert.co
                u = .5 + .47 * q.x / MOON_R * (-1 if flip else 1)
                lp[lay].uv = (u, .5 + .47 * q.y / MOON_R)

        for face in (1, -1):
            rows = []
            for i in range(25):
                rr = RF * i / 24
                row = []
                for k in range(112):
                    t = 2 * math.pi * k / 112
                    x, y = rr * math.cos(t), rr * math.sin(t)
                    # the back face shows the texture mirrored (u flipped), so its craters are at -x
                    hgt = relief(x if face > 0 else -x, y)
                    row.append(dm @ Vector((x, y, face * (.17 + min(.028, hgt)))))
                rows.append(row)
            _, faces = mm.grid(rows, "planet_moon", closed_u=True, smooth=True, flip=face < 0)
            for f in faces:
                disc_uv(f, face < 0)
        # the rounded edge: a half round from one face to the other
        edge = [(RF + .2 * math.sin(t), .17 * math.cos(t)) for t in (math.pi * j / 10 for j in range(11))]
        mm.revolve(edge, "marble", 96, dm)              # the edge: plain cream stone, as the concept's
        ob = mm.make("RoGlobe " + nm.lower(), coll, dev)
        ob.matrix_parent_inverse = Matrix()
        ob.matrix_basis = Matrix()
        out.append(ob)
    return eco, out


# ---------------------------------------------------------------- the scene

def stage(coll):
    """Preview only: the hall's dark floor with gold circles, a night world and lights."""
    m = Mesh()
    c = U(*GLOBE)
    m.revolve([(.001, HALL_FLOOR), (48, HALL_FLOOR)], "hallstone", 96, Matrix.Translation((c.x, c.y, 0)), smooth=False)
    for rr in (16, 22, 30, 40):
        m.revolve([(rr - .06, HALL_FLOOR + .005), (rr + .06, HALL_FLOOR + .005)], "gold", 128,
                  Matrix.Translation((c.x, c.y, 0)), smooth=False)
    m.make("Preview hall floor", coll)
    # Thresholds (the builder's, Unity x0..x1, z0..z1, y) as plain slabs for scale.
    for x0, x1, y in ((-18.5, -12.4, WEST_GATE[1]), (13.6, 17.2, EAST_GATE[1] - .01)):
        t = Mesh()
        t.box(((x0 + x1) / 2, 64, y - .25), (x1 - x0, 7, .5), "marble")
        t.make("Preview threshold", coll)
    w = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.01, .014, .04, 1)
    bg.inputs["Strength"].default_value = 1.0
    for name, loc, energy, col, size in (("Key", (30, 30, 50), 60000, (1, .85, .65), 20),
                                         ("Fill", (-40, 90, 30), 25000, (.55, .6, 1), 25),
                                         ("Sun glow", (U(*MIDDLE_U) + Vector((0, 0, 7.6))), 9000, (1, .8, .45), 1)):
        ld = bpy.data.lights.new(name, "AREA" if name != "Sun glow" else "POINT")
        ld.energy = energy
        ld.color = col
        if name != "Sun glow":
            ld.size = size
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        if name != "Sun glow":
            lo.rotation_euler = (Vector((0, 64, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        coll.objects.link(lo)


# ---------------------------------------------------------------- the gate thresholds (the user 11-19, a Unity Play
# screenshot: "아직 블록아웃으로 된 부분 … 수정해줘"; asked how, "대천구 바닥처럼"): the builder's two 1 m slabs before the
# gates ("West gate threshold" x -18.5 .. -12.4 at y 4, "East gate threshold" x 13.6 .. 17.2 at y 11.99, both z 60.5 ..
# 67.5) dressed as the globe's floor - cream marble, a gold star chart inlaid 2-3 mm proud, a gold band round the top
# edge. The walking face and the box stay the builder's; nothing reaches past the slab but 3 mm of gold on its sides.

THRESHOLD_INLAY = False   # the star chart and gold bands below were the first try; the user chose plain marble
THRESHOLDS = [("west", -18.5, -12.4, 60.5, 67.5, 4.0), ("east", 13.6, 17.2, 60.5, 67.5, 11.99)]


def threshold(coll, side, x0, x1, z0, z1, top):
    W, D = x1 - x0, z1 - z0
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    m = Mesh()
    hw, hd = W / 2, D / 2               # Blender local: x east, y north, z up; the walking face at z 0
    # the marble slab, its top in 1 m squares (the inlay has something to sit on in every light)
    nx, ny = max(2, round(W)), max(2, round(D))
    m.grid([[Vector((-hw + W * i / nx, -hd + D * j / ny, 0)) for i in range(nx + 1)] for j in range(ny + 1)], "marble", smooth=False)
    ring = [Vector((-hw, -hd, 0)), Vector((hw, -hd, 0)), Vector((hw, hd, 0)), Vector((-hw, hd, 0))]
    for a, b in zip(ring, ring[1:] + ring[:1]):
        m.face([a + Vector((0, 0, -1)), b + Vector((0, 0, -1)), b, a], "marble")
    m.face([q + Vector((0, 0, -1)) for q in reversed(ring)], "marble")
    if not THRESHOLD_INLAY:              # the user 11-19 (via the planning session): "크림색 대리석만"
        return m.make("RoGlobe threshold " + side, coll, matrix_world=Matrix.Translation(U(cx, top, cz)), two_sided=True)
    # the gold band round the top edge and a thin one low down, 3 mm proud of the sides
    for z_lo, z_hi in ((-.16, -.02), (-.92, -.86)):
        for sx, sy, lx, ly in ((0, -1, W, .006), (0, 1, W, .006), (-1, 0, .006, D), (1, 0, .006, D)):
            m.box((sx * hw, sy * hd, (z_lo + z_hi) / 2), (lx + (.006 if ly > .1 else 0), ly + (.006 if lx > .1 else 0), z_hi - z_lo), "gold")

    def line(pts, w, h, slot="gold", closed=False):
        if closed:
            pts = pts + pts[:1]
        left, right = [], []
        for i in range(len(pts)):
            a_ = pts[max(i - 1, 0)] if not (closed and i == 0) else pts[-2]
            b_ = pts[min(i + 1, len(pts) - 1)] if not (closed and i == len(pts) - 1) else pts[1]
            t = (b_ - a_).normalized()
            nrm = Vector((-t.y, t.x, 0)) * (w / 2)
            left.append(Vector((pts[i].x, pts[i].y, h)) + nrm)
            right.append(Vector((pts[i].x, pts[i].y, h)) - nrm)
        m.grid([right, left], slot, smooth=False)

    def rect(inset, w, h):
        line([Vector((-hw + inset, -hd + inset, 0)), Vector((hw - inset, -hd + inset, 0)),
              Vector((hw - inset, hd - inset, 0)), Vector((-hw + inset, hd - inset, 0))], w, h, closed=True)

    def circle(r, w, h, lim_x, lim_y):
        run = []
        for j in range(121):
            t = 2 * math.pi * j / 120
            q = Vector((r * math.cos(t), r * math.sin(t), 0))
            if abs(q.x) <= lim_x and abs(q.y) <= lim_y:
                run.append(q)
            elif len(run) > 1:
                line(run, w, h); run = []
            else:
                run = []
        if len(run) > 1:
            line(run, w, h)

    def star8(c0, size, h):
        for j in range(8):
            t = math.pi / 2 + 2 * math.pi * j / 8
            L = size if j % 2 == 0 else size * .55
            d = Vector((math.cos(t), math.sin(t), 0))
            nrm = Vector((-d.y, d.x, 0))
            for sd, slot in ((1, "gold"), (-1, "brass")):
                base = c0 + nrm * size * .16 * sd
                f = [Vector((q.x, q.y, h)) for q in ((c0, c0 + d * L, base) if sd > 0 else (c0, base, c0 + d * L))]
                m.face(f, slot)

    rect(.12, .16, .002)                 # the band round the top, as the floor's edge band
    rect(.36, .04, .002)                 # a fine line inside it
    ix, iy = hw - .36, hd - .36
    small = min(hw, hd)
    for r in (small * .42, small * .7, small * 1.05, small * 1.45):
        circle(r, .06 if r < small else .05, .0021, ix, iy)
    for sx in (-1, 1):                   # radial lines to the corners
        for sy in (-1, 1):
            d = Vector((sx * ix, sy * iy, 0))
            line([d.normalized() * small * .5, d * .97], .05, .0022)
    for j in range(4):                   # and along the axes
        t = math.pi / 2 * j
        d = Vector((math.cos(t), math.sin(t), 0))
        lim = ix if j % 2 == 0 else iy
        line([d * small * .5, d * (lim - .02)], .05, .0022)
    # the compass star in the middle: sixteen rays, two-toned along their ridges, as the floor's
    L0 = small * .82
    for j in range(16):
        t = math.pi / 2 + 2 * math.pi * j / 16
        long_ = j % 2 == 0
        L = (L0 if j % 4 == 0 else L0 * .74) if long_ else L0 * .44
        hwid = (.11 if long_ else .07) * L0
        d = Vector((math.cos(t), math.sin(t), 0))
        nrm = Vector((-d.y, d.x, 0))
        h = .003 if long_ else .0028
        for sd, slot in ((1, "gold"), (-1, "brass")):
            base = nrm * hwid * sd
            f = [Vector((q.x, q.y, h)) for q in ((Vector(), d * L, base) if sd > 0 else (Vector(), base, d * L))]
            m.face(f, slot)
    m.revolve([(.001, .0032), (L0 * .09, .0032)], "gold", 24, smooth=False)
    circle(L0 * .17, .045, .0031, 99, 99)
    for sx in (-1, 1):                   # small stars in the corners
        for sy in (-1, 1):
            star8(Vector((sx * (ix - .55), sy * (iy - .55), 0)), .38, .0034)
    return m.make("RoGlobe threshold " + side, coll, matrix_world=Matrix.Translation(U(cx, top, cz)), up=(0, 0, 1))


def thresholds(coll):
    return [threshold(coll, *t) for t in THRESHOLDS]


def build():
    for c in list(bpy.data.collections):
        if c.name.startswith("RO R3") or c.name.startswith("RO preview"):
            for o in list(c.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(c)
    root = bpy.data.collections.new("RO R3 Great globe")
    bpy.context.scene.collection.children.link(root)
    prev = bpy.data.collections.new("RO preview")
    bpy.context.scene.collection.children.link(prev)

    flare = empty("Device_SolarFlare", root, FLOOR_M)
    glass_globe(root)
    armillary_fixed(root)
    gimbal_a(root)
    gimbal_b(root)
    gimbal_c(root)
    base(root)
    gate_frame(root, WEST_GATE, -1, "RoGlobe west gate")
    gate_frame(root, EAST_GATE, 1, "RoGlobe east gate")
    thresholds(root)
    floor, panels = tilted_floor(root, flare)
    sun_and_rods(root, flare)
    for i in range(3):
        orbit(root, flare, i)
    ecliptic_and_moons(root, flare)
    stage(prev)
    print("panels", panels)
    tris = 0
    for o in root.all_objects:
        if o.type == "MESH":
            n = sum(len(p.vertices) - 2 for p in o.data.polygons)
            tris += n
            print("%-28s %7d tris  slots %s" % (o.name, n, [m.name[5:] for m in o.data.materials]))
    print("TOTAL", tris)


def camera(name, loc, look, lens=35):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_end = 1000
    co = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(co)
    co.location = loc
    co.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return co


def stills():
    os.makedirs(STILLS, exist_ok=True)
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    try:
        sc.eevee.taa_render_samples = 48
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.view_settings.view_transform = "AgX"
    c = U(*GLOBE)
    shots = {
        "r3_overview": ((34, 22, 40), (0, 64, 2), 30),
        "r3_from_west_gate": ((-26, 61, 9), (4, 66, 7), 26),
        "r3_inside_floor": ((-6, 50, 14), (4, 68, 6), 22),
        "r3_base": ((24, 34, -6), (0, 64, -9), 28),
        "r3_moon_east_gate": ((26, 54, 18), (13, 64, 12), 30),
        "r3_sun_close": ((-7, 52, 17), (1.8, 64, 15.5), 40),
        "r3_west_gate_close": ((-24, 56, 7.5), (-13, 64, 6.5), 32),
    }
    only = [a for a in sys.argv[sys.argv.index("--") + 1:] if not a.startswith("--")] if "--" in sys.argv else []
    for name, (loc, look, lens) in shots.items():
        if only and name not in only:
            continue
        cam = camera(name, loc, look, lens)
        sc.camera = cam
        sc.render.filepath = os.path.join(STILLS, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("wrote", sc.render.filepath)


# ---------------------------------------------------------------- the assembled globe (all ten parts passed, 11-19)

LIBRARY = {"marble": "RO_Marble", "gold": "RO_Gold", "brass": "RO_Brass", "navy": "RO_NavyEnamel",
           "violet": "RO_VioletEnamel", "pearl": "RO_Pearl", "hallstone": "RO_HallStone", "velvet": "RO_Velvet"}


def library_materials():
    """The preview materials swapped for ro-path's library (textures/library: albedo with smoothness in its alpha,
    normal; tiled in metres as ro_textures.json says), box-projected - the look the globe will have in Unity."""
    import json
    lib = os.path.join(HERE, "textures", "library")
    info = json.load(open(os.path.join(lib, "ro_textures.json"), encoding="utf-8"))
    for slot, name in LIBRARY.items():
        m = bpy.data.materials.get("RO | " + slot)
        if m is None:
            continue
        nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tile = info.get(name, {}).get("tile", 1.0)
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1 / tile,) * 3
        nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
        al = nt.nodes.new("ShaderNodeTexImage")
        al.image = bpy.data.images.load(os.path.join(lib, name + "_Albedo.png"), check_existing=True)
        al.projection, al.projection_blend = "BOX", .25
        nt.links.new(mp.outputs["Vector"], al.inputs["Vector"])
        nt.links.new(al.outputs["Color"], p.inputs["Base Color"])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(al.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], p.inputs["Roughness"])
        nf = os.path.join(lib, name + "_Normal.png")
        if os.path.exists(nf):
            nm = nt.nodes.new("ShaderNodeTexImage")
            nm.image = bpy.data.images.load(nf, check_existing=True)
            nm.image.colorspace_settings.name = "Non-Color"
            nm.projection, nm.projection_blend = "BOX", .25
            nt.links.new(mp.outputs["Vector"], nm.inputs["Vector"])
            nn = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nm.outputs["Color"], nn.inputs["Color"])
            nt.links.new(nn.outputs["Normal"], p.inputs["Normal"])
        p.inputs["Metallic"].default_value = info.get(name, {}).get("metallic", 0.0) if isinstance(
            info.get(name, {}).get("metallic", 0.0), float) else 0.0
        if slot == "pearl":
            p.inputs["Emission Color"].default_value = (1, .85, .58, 1)
            p.inputs["Emission Strength"].default_value = 2.5
    # the sun glass lit (its light and flare are added in Unity; the preview shows it lit)
    sun = None   # the sun's own surface glows (planet_sun)
    if sun:
        m = bpy.data.materials.new("Preview sun glow")
        p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        p.inputs["Base Color"].default_value = (1, .9, .7, 1)
        p.inputs["Emission Color"].default_value = (1, .82, .5, 1)
        p.inputs["Emission Strength"].default_value = 12
        sun.data.materials[0] = m


def assembly_stills():
    import numpy as np
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1500, 1000
    try:
        sc.eevee.taa_render_samples = 64
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.view_settings.view_transform = "AgX"
    try:
        sc.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    library_materials()
    for m in bpy.data.materials:
        m.use_backface_culling = True                        # as Unity draws (front faces only)
    # the hall: dark, its light warm from the lamps and the sun, a cool moonlight from above
    w = sc.world
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    nt = w.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[1].position = .35, .62
    ramp.color_ramp.elements[0].color = (.55, .36, .16, 1)       # warm, the lit hall round and below
    ramp.color_ramp.elements[1].color = (.04, .06, .16, 1)       # the night dome above
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value, mr.inputs["From Max"].default_value = -1, 1
    nt.links.new(tc.outputs["Generated"], sep.inputs["Vector"])
    nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.6
    for o in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        bpy.data.objects.remove(o, do_unlink=True)

    def light(name, kind, loc, energy, colour, size=1.0, look=None):
        ld = bpy.data.lights.new(name, kind)
        ld.energy, ld.color = energy, colour
        if kind == "AREA":
            ld.size = size
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        if look is not None:
            lo.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)

    c = U(*GLOBE)
    light("Moonlight", "AREA", c + Vector((-30, 20, 60)), 90000, (.7, .78, 1), 30, c)
    light("Warm key", "AREA", c + Vector((35, -40, 25)), 70000, (1, .8, .55), 25, c)
    sun_at = FLOOR_M @ Vector((0, 0, SUN_H))
    light("Sun lamp", "POINT", sun_at, 60000, (1, .8, .5))
    for i, o in enumerate([o for o in bpy.data.objects if o.name.startswith("Preview hall floor")]):
        pass
    out = os.path.join(STILLS, "assembly")
    os.makedirs(out, exist_ok=True)
    shots = {
        "globe_whole": ((44, -2, 22), c + Vector((0, 0, -2)), 32, ("RO_1_whole_hall.png", (545, 290, 1010, 800))),
        "globe_west_gate": ((-27, 56, 8.5), c + Vector((4, 0, -2.5)), 24, ("RO_3_cassiopeia_telescope.png", (0, 310, 520, 1020))),
        "globe_inside_up": (FLOOR_M @ Vector((-6, -3, 1.6)), FLOOR_M @ Vector((2, 4, 12)), 20,
                            ("RO_3_cassiopeia_telescope.png", (0, 0, 520, 420))),
        "horizon_close": ((-24, 46, 12), (-12, 56, 9.5), 30, ("RO_1_whole_hall.png", (545, 290, 1010, 800))),
        "globe_inside": ((-8, 57, 11), (3, 67, 5.5), 22, ("RO_3_cassiopeia_telescope.png", (0, 560, 470, 900))),
    }
    H = 640
    for name, (loc, look, lens, (src, (x0, y0, x1, y1))) in shots.items():
        cam = camera(name, loc, look, lens)
        sc.camera = cam
        f = os.path.join(out, name + ".png")
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        cpx, cw, ch = _pixels(os.path.join(CONCEPT, src), H)
        row = [_fit(cpx[ch - y1:ch - y0, x0:x1], H), np.ones((H, 10, 4), np.float32), _fit(_pixels(f, H)[0], H)]
        img = np.concatenate(row, axis=1)
        img[..., 3] = 1
        o = bpy.data.images.new(name, img.shape[1], H, alpha=True)
        o.pixels[:] = img.ravel()
        o.filepath_raw = os.path.join(out, name + "_compare.png")
        o.file_format = "PNG"
        o.save()
        print("ASSEMBLY", o.filepath_raw)


# ---------------------------------------------------------------- export (the assembly passed, 11-19)
# One FBX for each thing the models ride, to Assets/Resources/RoyalObservatory/Models/Globe/ (ro_kit.export), each written
# in the frame of the Unity object it hangs under - RoyalObservatoryDressing.Globe.cs names them. The frames are the
# builder's rest poses, computed here (the dump's devices may be caught mid-turn).
EXPORTS = [
    # model, Blender objects (name starts), frame (Blender matrix)
    ("RoGlobeShell", ["RoGlobe glass", "RoGlobe engraving", "RoGlobe meridian", "RoGlobe horizon", "RoGlobe base",
                      "RoGlobe west gate", "RoGlobe east gate"], lambda: Matrix()),
    ("RoGlobeThresholds", ["RoGlobe threshold"], lambda: Matrix()),
    ("RoGlobeFloor", ["RoGlobe floor", "RoGlobe rods", "RoGlobe orbit wires", "RoGlobe ecliptic"], lambda: FLOOR_M),
    # the armillary's gimbals, each in its pivot's frame (the globe's middle; GlobeArmillary swings them)
    ("RoGlobeGimbalA", ["RoGlobe gimbal A"], lambda: Matrix.Translation(U(*GLOBE))),
    ("RoGlobeGimbalB", ["RoGlobe gimbal B"], lambda: Matrix.Translation(U(*GLOBE))),
    ("RoGlobeGimbalC", ["RoGlobe gimbal C"], lambda: Matrix.Translation(U(*GLOBE))),
    # (the halo rings, "RoGlobe halo", are kept in the scene but not exported: the user 11-19 "일단 고리 빼주고")
    ("RoGlobeSun", ["RoGlobe sun"], lambda: FLOOR_M @ Matrix.Translation((0, 0, SUN_H))),
    ("RoGlobeOrbit1", ["RoGlobe orbit 1"], lambda: FLOOR_M @ Matrix.Translation((0, 0, ORBITS[0][1]))),
    ("RoGlobeOrbit2", ["RoGlobe orbit 2"], lambda: FLOOR_M @ Matrix.Translation((0, 0, ORBITS[1][1]))),
    ("RoGlobeOrbit3", ["RoGlobe orbit 3"], lambda: FLOOR_M @ Matrix.Translation((0, 0, ORBITS[2][1]))),
]


def metre_uvs(ob):
    """ro_kit's metre UVs for the library slots; planet faces keep the UVs their textures need."""
    import ro_kit as k
    me = ob.data
    keep = {}
    if me.uv_layers:
        uv = me.uv_layers[0].data
        kat = me.attributes.get("keep_uv")
        for poly in me.polygons:
            mt = me.materials[poly.material_index]
            if (mt is not None and mt.name.startswith("RO | planet_")) or (kat is not None and kat.data[poly.index].value):
                keep[poly.index] = [tuple(uv[li].uv) for li in poly.loop_indices]
    k.surface_uvs(ob)
    if keep:
        uv = me.uv_layers[0].data
        for pi, uvs in keep.items():
            for li, u in zip(me.polygons[pi].loop_indices, uvs):
                uv[li].uv = u


def check_frames():
    """The computed rest frames against the graybox Unity dumped (ro_blockout.json), as a check."""
    import ro_kit as k
    for name, mtx in (("Device_SolarFlare", FLOOR_M), ("Sun lamp", FLOOR_M @ Matrix.Translation((0, 0, SUN_H))),
                      ("Device_EclipseMoon", FLOOR_M)):
        try:
            f = k.frame(name)
        except Exception as e:
            print("FRAME", name, "not in the dump:", e)
            continue
        d = (f.translation - mtx.translation).length
        r = f.to_quaternion().rotation_difference(mtx.to_quaternion()).angle
        print("FRAME %-20s off %.3f m  %.2f deg" % (name, d, math.degrees(r)))


def export_all():
    import ro_kit as k
    check_frames()
    for model, starts, frame_of in EXPORTS:
        coll = bpy.data.collections.new("RO export " + model)
        bpy.context.scene.collection.children.link(coll)
        F = frame_of()
        obs = [o for o in bpy.data.objects if o.type == "MESH" and any(o.name.startswith(s_) for s_ in starts)
               and not (model == "RoGlobeFloor" and o.name.startswith("RoGlobe orbit ") and o.name[-1].isdigit())]
        for o in obs:
            w = o.matrix_world.copy()
            o.parent = None
            o.matrix_world = F.inverted() @ w
            for c in list(o.users_collection):
                c.objects.unlink(o)
            coll.objects.link(o)
            metre_uvs(o)
        print("EXPORT", model, [o.name for o in obs])
        k.export(coll.name, model, "Globe", None)


# ---------------------------------------------------------------- one part at a time (user 11-19: "모델 하나하나 진행해서
# 나한테 검사 받아" - each part is shown alone beside its sheet picture; the globe is put together once all are passed)

PARTS = {
    "base": (["RoGlobe base"], (0, 0)),
    "glass": (["RoGlobe glass", "RoGlobe engraving"], [("RO_1_whole_hall.png", (545, 290, 1010, 800)),
                                                          ("RO_3_cassiopeia_telescope.png", (0, 310, 520, 1020))]),
    "meridian": (["RoGlobe meridian"], (1, 0)),     # the sheet's stand is the globe's base here
    "horizon": (["RoGlobe horizon"], (2, 0)),
    "gate": (["RoGlobe west gate"], (3, 0)),
    "floor": (["RoGlobe floor"], [("RO_3_cassiopeia_telescope.png", (0, 560, 470, 900))]),
    "sun": (["RoGlobe sun", "RoGlobe rods"], [("RO_1_whole_hall.png", (680, 330, 900, 560))]),
    "orbits": (["RoGlobe orbit"], (0, 2)),
    "ecliptic": (["RoGlobe ecliptic"], [("RO_3_cassiopeia_telescope.png", (0, 640, 520, 1000))]),
    "moon": (["RoGlobe moon a"], [("RO_3_cassiopeia_telescope.png", (130, 700, 450, 880))]),
}
DARK_PARTS = {"glass", "sun"}
# The west gate's south side panel (Blender y + 4.2 of the gate, 1.5 m up), seen from outside, beside the sheet's.
CLOSE = {"moon": {"moon_close": (FLOOR_M @ Vector((-26, -4.5, 1.2)), FLOOR_M @ Vector((-15.6, 0, -.2)), 40,
                                 (130, 700, 450, 880), "RO_3_cassiopeia_telescope.png")},
         "ecliptic": {"ecliptic_close": (FLOOR_M @ Vector((-21, -7, 3.2)), FLOOR_M @ Vector((-15.6, 1.5, -.3)), 35,
                                         (0, 640, 520, 1000), "RO_3_cassiopeia_telescope.png")},
         "gate": {"gate_scroll": ((WEST_GATE[0] - .5 - 5.5, WEST_GATE[2] + 4.15, WEST_GATE[1] + 1.9),
                                  (WEST_GATE[0] - .5, WEST_GATE[2] + 4.15, WEST_GATE[1] + 1.6), 40, (1205, 140, 1265, 260))}}
VIEWS = {"meridian": (Vector((1, -.55, .3)), Vector((1, .25, .9))),
         "sun": (Vector((-1, -.15, .1)), Vector((-1, -.9, .5))),
         "orbits": (Vector((-.3, -1, .35)), Vector((-1, -.5, .9))),
         "moon": (Vector((-1, -.25, .2)), Vector((-.4, -1, .5))),
         "floor": (Vector((.15, -.35, 1)), Vector((-1, -.6, .45))),
         "gate": (Vector((-1, 0, .12)), Vector((-1, -.7, .5)))}
CONCEPT = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R725_RoyalObservatoryConcept")
SHEET = os.path.join(CONCEPT, "AssetRefs", "RO_A3_hall_props.png")


def _pixels(path, H):
    import numpy as np
    im = bpy.data.images.load(path)
    w, h = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return px, w, h


def _fit(px, H):
    import numpy as np
    h, w = px.shape[:2]
    nw = max(1, int(w * H / h))
    return px[(np.arange(H) * h / H).astype(int)][:, (np.arange(nw) * w / nw).astype(int)]


def part_stills(part):
    import numpy as np
    keep, cell = PARTS[part]
    for o in list(bpy.data.objects):
        if o.type == "MESH" and not any(o.name.startswith(k) for k in keep):
            bpy.data.objects.remove(o, do_unlink=True)
    pts = [o.matrix_world @ Vector(c) for o in bpy.data.objects if o.type == "MESH" for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mid, size = (lo + hi) / 2, (hi - lo).length
    # A pawn for scale (1.2 m), beside the part on its lowest level.
    pw = Mesh()
    pw.revolve([(.001, 0), (.35, 0), (.35, .12), (.2, .25), (.14, .7), (.24, .78), (.12, .86)], "marble", 24,
               Matrix.Translation((hi.x + .8, mid.y, lo.z)))
    pw.sphere((0, 0, 1.0), .22, "marble", 16, 8, Matrix.Translation((hi.x + .8, mid.y, lo.z)), uv=False)
    pw.make("Scale pawn 1.2 m", bpy.context.scene.collection)
    if part == "gate":
        th = Mesh()
        th.box((WEST_GATE[0] - 3.05 + .25, WEST_GATE[2], WEST_GATE[1] - .25), (6.1, 7, .5), "marble")
        th.make("Preview threshold", bpy.context.scene.collection)
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1100, 1000
    kd = bpy.data.lights.new("Part key", "AREA")
    kd.energy, kd.size, kd.color = 9000 * (size / 20) ** 2, size * .6, (1, .9, .78)
    ko = bpy.data.objects.new("Part key", kd)
    v0 = VIEWS.get(part, (Vector((0, -1, .34)),))[0]
    turn = Matrix.Rotation(math.atan2(v0.x, -v0.y), 3, "Z")            # lights follow the first view round
    ko.location = mid + turn @ Vector((.4, -1, .9)).normalized() * size * 1.2
    ko.rotation_euler = (mid - ko.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(ko)
    sc.view_settings.view_transform = "AgX"
    out = os.path.join(STILLS, "parts")
    os.makedirs(out, exist_ok=True)
    shots = []
    w = sc.world
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.5, .44, .36, 1)      # something warm for the metal to reflect
    bg.inputs["Strength"].default_value = 1.3
    try:
        sc.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    sc.render.film_transparent = True                         # put on white (navy for glass) afterwards
    for nm, off, en, col in (("Part fill", Vector((-1, -.6, .4)), .45, (.75, .82, 1)), ("Part rim", Vector((.2, 1, .7)), .6, (1, .9, .75))):
        ld = bpy.data.lights.new(nm, "AREA")
        ld.energy, ld.size, ld.color = 11000 * (size / 20) ** 2 * en, size * .5, col
        lob = bpy.data.objects.new(nm, ld)
        lob.location = mid + turn @ off.normalized() * size * 1.2
        lob.rotation_euler = (mid - lob.location).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lob)
    for i, d in enumerate(VIEWS.get(part, (Vector((0, -1, .34)), Vector((.75, -1, .9))))):
        d.normalize()
        cam = camera("%s_%d" % (part, i), mid + d * size * 1.55, mid, 55)
        sc.camera = cam
        f = os.path.join(out, "%s_%d.png" % (part, i))
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        shots.append(f)
    H = 640
    row = []
    if isinstance(cell, tuple) and os.path.exists(SHEET):
        px, w, h = _pixels(SHEET, H)
        cw, ch = w // 4, h // 3
        x0, y0 = cell[0] * cw, h - (cell[1] + 1) * ch           # Blender pixels start at the bottom
        row.append(_fit(px[y0:y0 + ch, x0:x0 + cw], H))
    elif isinstance(cell, list):
        for name, (x0, y0, x1, y1) in cell:
            px, w, h = _pixels(os.path.join(CONCEPT, name), H)
            row.append(_fit(px[h - y1:h - y0, x0:x1], H))
    for f in shots:
        px = _pixels(f, H)[0]
        al = px[..., 3:4]
        under = np.array((.025, .035, .1, 1) if part in DARK_PARTS else (1, 1, 1, 1), np.float32)
        px = px * al + (1 - al) * under                     # over white, or the hall's navy for glass
        px[..., 3] = 1
        row.append(_fit(px, H))
    gap = np.ones((H, 10, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(out, "%s_compare.png" % (part if part != "gate" else "gate_" + GATE_STYLE))
    o.file_format = "PNG"
    o.save()
    tris = sum(len(p.vertices) - 2 for ob in bpy.data.objects if ob.type == "MESH" and not ob.name.startswith("Scale")
               for p in ob.data.polygons)
    # Close-ups beside the sheet's detail (part -> camera, target, lens, sheet box in pixels top-left).
    for nm, (loc, look, lens, box, *src) in CLOSE.get(part, {}).items():
        src = src[0] if src else None
        cam = camera(nm, loc, look, lens)
        sc.camera = cam
        f = os.path.join(out, "%s.png" % nm)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        spx, sw, sh = _pixels(os.path.join(CONCEPT, src) if src else SHEET, H)
        x0, y0, x1, y1 = box
        a_ = _fit(spx[sh - y1:sh - y0, x0:x1], H)
        px = _pixels(f, H)[0]
        al = px[..., 3:4]
        px = px * al + (1 - al) * np.ones_like(px)
        px[..., 3] = 1
        img2 = np.concatenate([a_, np.ones((H, 10, 4), np.float32), _fit(px, H)], axis=1)
        o2 = bpy.data.images.new(nm, img2.shape[1], H, alpha=True)
        o2.pixels[:] = img2.ravel()
        o2.filepath_raw = os.path.join(out, "%s_compare.png" % nm)
        o2.file_format = "PNG"
        o2.save()
        print("CLOSE", o2.filepath_raw)
    print("PART", part, "tris", tris, "size", tuple(round(v, 2) for v in (hi - lo)), "->", o.filepath_raw)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if "--stills" in args:
        stills()
    if "--export" in args:
        export_all()
    if "--assembly" in args:
        assembly_stills()
    if "--part" in args:
        for o in [o for o in bpy.data.objects if o.name.startswith("Preview")]:
            bpy.data.objects.remove(o, do_unlink=True)
        part_stills(args[args.index("--part") + 1])
