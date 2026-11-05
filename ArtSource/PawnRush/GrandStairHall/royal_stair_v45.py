# 승규 11-04 (an asset preview: a cream marble grand stair with a red carpet runner held by gold rods, ornate gold
# balustrades on marble curbs, marble newel posts topped by glowing crown lanterns, cream side walls with gold trim and
# medallions): "계단 부분도 고퀄리티로 모델링 해줘 텍스쳐 입혀주고". The hall H's grand stair (beat 1: the lower flight
# z 14 .. 30, y 0 -> 4, the middle landing z 29 .. 36, y 4, the upper flight z 36 .. 52, y 4 -> 8, 16 m wide, 16 steps
# of 0.25 x 1.0 each) modelled over the blockout, which keeps every collider and the rolling pins' lanes:
#   steps       cream-white marble (RoyalStair_Marble.png + _N, boxed UVs 2 m a tile), each nosing a bullnose with an
#               undercut, a thin gold inlay behind it on the marble either side of the carpet
#   carpet      a 9 m crimson runner (the hall's carpet texture, 2 m a tile along its length) draped over every nosing,
#               gold edge bands, a gold rod with ball finials and brackets in every step's inner corner, an end bar
#   side walls  cream palace stone (Palace_Stone: Unity's world-space PR_PalaceStone) x 8 .. 8.42 from the hall floor
#               (-4) up the rake: skirting with a gold bead, pilasters with gold bands, gold-beaded panels each with a
#               medallion (crimson velvet, gold ring, gold crown), a raking cornice with a gold bead under it
#   balustrade  a raking stone curb, a gold foot rail, turned gold balusters (two patterns in turn) every 0.28 m, a gold
#               handrail; eight stone newel posts (foot, both ends of the landing, top) with gold bands, gold-beaded
#               panels with crowns, a cornice cap and a crown lantern: a glowing amber globe in a gold cup under six
#               gold ribs, a crown with points and pearls, an orb and cross; a light marker in each globe
#   drain       the stair foot's drain (z 12 .. 14) as an iron grille in a gold frame with rosettes
# Frame: Blender (x, y, z) = Unity section (x, z, y), as the hall FBX: the FBX goes under the hall's 180-degree pivot.
# Everything stays inside x +-8.8 (the statues' plinths reach in to x +-10.4) and clear of what the rolling pins use.
# Run: blender -b -P royal_stair_v45.py -- [render]
#   -> Assets/Resources/PawnRushGrandStairHall/RoyalStair.fbx, RoyalStair_Marble.png + _N, royal_stair_v45.blend,
#      renders_stair_v45/
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
RENDER = "render" in sys.argv
OUT = os.path.join(HERE, "renders_stair_v45")
PI = math.pi
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
rng = np.random.default_rng(45)


def B(x, h, z):
    """Section (x, height, along) -> Blender."""
    return Vector((x, z, h))


# ================================================================== the stair's numbers (GrandStairHallBuilder.GrandStair)
HALF = 8.0                    # the flights' half width
FLOOR = -4.0                  # the hall floor beside the stair
RISERS = [(14.0 + k, .25 * k, .25 * (k + 1)) for k in range(16)] + [(36.0 + k, 4 + .25 * k, 4 + .25 * (k + 1)) for k in range(16)]
TOP_Z = 51.0                  # the top riser; the central landing beyond
NOSE_R, NOSE_C = .02, .02     # the bullnose: radius, its centre this far before the riser
CARPET = 4.5                  # the runner's half width
CT = .012                     # its thickness
WALL_IN, WALL_OUT = 8.0, 8.42
CURB = .22                    # the curb's height under the rake line
RAIL_UP = .9                  # the handrail's middle over the rake line
NEWELS = [(13.8, 0.0), (29.5, 4.0), (35.5, 4.0), (51.5, 8.0)]   # (z, the floor it stands on)
C_RAKE = .25


def rake_lower(z):
    return .25 * (z - 12.96) + C_RAKE        # through the lower flight's nosings, + C


def rake_upper(z):
    return 4 + .25 * (z - 34.96) + C_RAKE


def rake_landing(z):
    return 4 + C_RAKE


# the three runs between the newels: (z from, z to, rake)
RUNS = [(13.8, 29.5, rake_lower), (29.5, 35.5, rake_landing), (35.5, 51.5, rake_upper)]


# ================================================================== textures
def vnoise(n, cx, cy):
    g = rng.random((cy, cx))

    def axis(c):
        x = np.arange(n) * c / n
        i0 = np.floor(x).astype(int)
        f = x - i0
        return i0 % c, (i0 + 1) % c, f * f * (3 - 2 * f)
    y0, y1, fy = axis(cy)
    x0, x1, fx = axis(cx)
    a, b = g[np.ix_(y0, x0)], g[np.ix_(y0, x1)]
    c, d = g[np.ix_(y1, x0)], g[np.ix_(y1, x1)]
    fx, fy = fx[None, :], fy[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(n, cells, octaves, gain=.5):
    s, amp, tot = np.zeros((n, n)), 1.0, 0.0
    for o in range(octaves):
        c = min(n, cells * 2 ** o)
        s += amp * vnoise(n, c, c)
        tot += amp
        amp *= gain
    return s / tot


def normal_map(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, -dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * .5 + .5


def mix(c0, c1, t):
    t = np.clip(t, 0, 1)[..., None]
    return np.array(c0) * (1 - t) + np.array(c1) * t


def save_png(name, rgb, color=True):
    n = rgb.shape[0]
    img = bpy.data.images.new(name, n, n, alpha=False)
    img.colorspace_settings.name = "sRGB" if color else "Non-Color"
    px = np.ones((n, n, 4), np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(RES, name + ".png")
    img.file_format = "PNG"
    img.save()
    print("TEXTURE", name, n)
    return img


def marble_texture(n=1024):
    """Cream-white marble, one tile = 2 x 2 m: soft clouds, a few long grey veins wandering across, fine warm veins."""
    # v2 (first renders: sine veins drew closed loops like a contour map): ridged noise - long, branching, crack-like veins.
    main = fbm(n, 3, 6, gain=.55)
    vein = (1 - np.abs(2 * main - 1)) ** 22                          # thin where the noise crosses its middle
    vein *= .55 + .45 * fbm(n, 5, 3)                                 # fading in and out along their length
    fine = (1 - np.abs(2 * fbm(n, 6, 5, gain=.55) - 1)) ** 40 * .8
    cloud = fbm(n, 4, 5)
    rgb = mix((.9, .885, .86), (.975, .965, .945), cloud)
    rgb = mix(rgb, (.58, .565, .55), np.clip(vein * 1.3, 0, 1) * .6)
    rgb = mix(rgb, (.78, .7, .54), np.clip(fine, 0, 1) * .45)
    height = .15 * cloud - .5 * vein - .3 * fine
    return rgb, normal_map(height, .22)                              # polished: the veins barely felt (.7 looked embossed)


images = {}
rgb, nrm = marble_texture()
images["RoyalStair_Marble"] = save_png("RoyalStair_Marble", rgb)
images["RoyalStair_Marble_N"] = save_png("RoyalStair_Marble_N", nrm, color=False)
for name in ("HallCarpet", "RoyalFan_Gold", "RoyalFan_Gold_N"):        # the hall's carpet and the fan's gold, for the preview
    img = bpy.data.images.load(os.path.join(RES, name + ".png"))
    if name.endswith("_N"):
        img.colorspace_settings.name = "Non-Color"
    images[name] = img


# ================================================================== materials (Blender's for the preview; Unity matches by name)
def material(name, base, metal=0., rough=.5, tex=None, normal=None, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*base, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if tex:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = images[tex]
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    if normal:
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = images[normal]
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit[0], 1)
        bsdf.inputs["Emission Strength"].default_value = emit[1]
    m.diffuse_color = (*base, 1)
    return m


MAT = {
    "marble": material("Stair_Marble", (.94, .93, .9), 0, .25, "RoyalStair_Marble", "RoyalStair_Marble_N"),
    "carpet": material("Stair_Carpet", (.5, .05, .06), 0, .85, "HallCarpet"),
    "gold": material("Stair_Gold", (.86, .64, .28), 1.0, .28, "RoyalFan_Gold", "RoyalFan_Gold_N"),
    "stone": material("Palace_Stone", (.86, .81, .72), 0, .55),
    "velvet": material("Stair_Velvet", (.42, .03, .06), 0, .6),
    "glass": material("Stair_Glass", (1, .7, .35), 0, .2, emit=((1, .62, .28), 6.0)),
    "iron": material("Stair_Iron", (.07, .07, .075), .8, .5),
}
# boxed UVs: metres per tile (the carpet and the blades of nothing else keep their own)
TILE = {"Stair_Marble": 2.0, "Stair_Gold": .6, "Palace_Stone": 2.0, "Stair_Velvet": 1.0, "Stair_Glass": 1.0, "Stair_Iron": 1.0}


# ================================================================== mesh helpers (bmesh, collected into parts)
class Part:
    """One object: pieces added with their material and shading; boxed UVs and sharp edges by angle at the end."""

    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []
        self.bm.loops.layers.uv.verify()

    def add(self, bm, mat, smooth=False):
        m = MAT[mat]
        if m not in self.mats:
            self.mats.append(m)
        mi = self.mats.index(m)
        bm.loops.layers.uv.verify()
        for f in bm.faces:
            f.material_index = mi
            f.smooth = smooth
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me); bm.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def done(self, keep_uv=("Stair_Carpet",), angle=35):
        bm = self.bm
        layer = bm.loops.layers.uv.verify()
        names = [m.name for m in self.mats]
        for f in bm.faces:
            name = names[f.material_index]
            if name in keep_uv:
                continue
            tile = TILE.get(name, 1.0)
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                p = l.vert.co
                if ax == 0:
                    u, v = (p.y if n.x > 0 else -p.y), p.z
                elif ax == 1:
                    u, v = (-p.x if n.y > 0 else p.x), p.z
                else:
                    u, v = p.x, (p.y if n.z > 0 else -p.y)
                l[layer].uv = (u / tile, v / tile)
        limit = math.radians(angle)
        for e in bm.edges:
            if len(e.link_faces) == 2:
                a, b = e.link_faces
                e.smooth = a.smooth and b.smooth and a.normal.angle(b.normal, 0) < limit
            else:
                e.smooth = False
        for f in bm.faces:
            f.smooth = True
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me); bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        sc.collection.objects.link(ob)
        print("PART", self.name, len(me.polygons), "faces")
        return ob


def finish(bm):
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, ngon_method="EAR_CLIP")
    return bm


def poly_x(pts_zh, x0, x1):
    """A polygon in the (along, height) plane, a solid across x from x0 to x1."""
    bm = bmesh.new()
    a = [bm.verts.new(B(x0, h, z)) for z, h in pts_zh]
    b = [bm.verts.new(B(x1, h, z)) for z, h in pts_zh]
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    n = len(pts_zh)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    return finish(bm)


def box(lo, hi):
    """An axis box from section corners lo = (x, h, z) to hi."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    c = B(*[(l + h) / 2 for l, h in zip(lo, hi)])
    s = B(*[abs(h - l) for l, h in zip(lo, hi)])
    bmesh.ops.scale(bm, vec=s, verts=bm.verts)
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    return bm


def bbox(lo, hi, w=.012):
    """A box with its long edges bevelled."""
    bm = box(lo, hi)
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=w, segments=2, affect="EDGES", profile=.5)
    return finish(bm)


def lathe(prof, centre, axis=(0, 0, 1), segs=16, closed=False):
    """Profile (radius, along axis) turned about an axis through centre (Blender coords)."""
    axis = Vector(axis).normalized()
    e1 = Vector((1, 0, 0)) if abs(axis.x) < .9 else Vector((0, 1, 0))
    e1 = (e1 - axis * e1.dot(axis)).normalized()
    e2 = axis.cross(e1)
    bm = bmesh.new()
    rings = []
    for i in range(segs):
        a = 2 * PI * i / segs
        d = e1 * math.cos(a) + e2 * math.sin(a)
        rings.append([bm.verts.new(Vector(centre) + axis * h + d * r) for r, h in prof])
    m = len(prof)
    for i in range(segs):
        r0, r1 = rings[i], rings[(i + 1) % segs]
        for j in range(m if closed else m - 1):
            k = (j + 1) % m
            bm.faces.new((r0[j], r1[j], r1[k], r0[k]))
    if not closed:
        if prof[0][0] > 1e-5:
            bm.faces.new([r[0] for r in rings])
        if prof[-1][0] > 1e-5:
            bm.faces.new([r[-1] for r in rings])
    return finish(bm)


def circle(cr, ch, r, n=10):
    return [(cr + r * math.cos(2 * PI * i / n), ch + r * math.sin(2 * PI * i / n)) for i in range(n)]


def sweep_x(path_zh, x, prof, closed_path=False, cap=True):
    """A profile swept along a path in the (along, height) plane at x: its a across x, its b square to the path in the
    plane (up for a path going +along). Corners mitred."""
    pts = [Vector(p) for p in path_zh]
    n = len(pts)
    bm = bmesh.new()
    rings = []
    for i in range(n):
        if closed_path or 0 < i < n - 1:
            t0 = (pts[i] - pts[i - 1]).normalized()
            t1 = (pts[(i + 1) % n] - pts[i]).normalized()
            t = (t0 + t1).normalized()
            k = 1 / max(.35, t.dot(t1))
        else:
            t = (pts[1] - pts[0]).normalized() if i == 0 else (pts[-1] - pts[-2]).normalized()
            k = 1.0
        nrm = Vector((-t.y, t.x))
        rings.append([bm.verts.new(B(x + a, pts[i].y + nrm.y * b * k, pts[i].x + nrm.x * b * k)) for a, b in prof])
    m = len(prof)
    for i in range(n if closed_path else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for j in range(m):
            k = (j + 1) % m
            bm.faces.new((r0[j], r1[j], r1[k], r0[k]))
    if cap and not closed_path:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1][::-1])
    return finish(bm)


def crown(centre, right, up, out, w=.25, depth=.022):
    """A five-point crown relief on a face, with pearls on its points and jewels on its band; w = its width."""
    s = w / .25
    right, up, out = Vector(right), Vector(up), Vector(out)
    outline = [(-.105, -.085), (.105, -.085), (.105, -.03), (.125, .085), (.058, .005), (0, .1), (-.058, .005),
               (-.125, .085), (-.105, -.03)]
    bm = bmesh.new()
    a = [bm.verts.new(Vector(centre) + (right * x + up * y) * s) for x, y in outline]
    b = [bm.verts.new(Vector(centre) + (right * x + up * y) * s + out * depth) for x, y in outline]
    bm.faces.new(a); bm.faces.new(b[::-1])
    for i in range(len(a)):
        j = (i + 1) % len(a)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bms = [finish(bm)]
    tip = Vector(centre) + out * depth
    for x, y in ((-.125, .085), (0, .1), (.125, .085)):
        bms.append(sphere(tip + (right * x + up * (y + .014)) * s, .021 * s, 12, 8))
    for x in (-.055, 0, .055):
        bms.append(sphere(tip + (right * x + up * -.058) * s, .016 * s, 10, 6))
    return bms


def sphere(c, r, seg=16, rings=10, squash=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    bmesh.ops.scale(bm, vec=Vector(squash), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    return bm


# ================================================================== the steps (marble) and their inlays
def nosing(z, h1, off=0.0, n=8):
    """The bullnose round (offset outwards by off), from its underside, round the front, to its top."""
    cz, ch, r = z - NOSE_C, h1 - NOSE_R, NOSE_R + off
    return [(cz + r * math.cos(math.radians(-90 - 180 * i / n)), ch + r * math.sin(math.radians(-90 - 180 * i / n)))
            for i in range(n + 1)]


def stair_top():
    pts = [(14.0, 0.0)]
    for z, h0, h1 in RISERS:
        if pts[-1] != (z, h0):
            pts.append((z, h0))
        pts.append((z, h1 - .055))                      # up the riser to the undercut
        pts += nosing(z, h1)                            # the bullnose; its top (z - .02, h1)
    pts.append((TOP_Z, 8.0))                            # the top nosing meets the central landing's edge
    return pts


steps = Part("RoyalStair_Steps")
outline = stair_top() + [(TOP_Z, FLOOR - .3), (14.0, FLOOR - .3)]
steps.add(poly_x(outline, -HALF, HALF), "marble", True)
for z, h0, h1 in RISERS:                                # a gold inlay behind each nosing, either side of the carpet
    if z >= TOP_Z:
        continue                                        # past the top nosing is the central landing's floor
    for s in (-1, 1):
        lo, hi = sorted((s * (CARPET + .12), s * (HALF - .08)))
        steps.add(box((lo, h1 - .01, z + .085), (hi, h1 + .0015, z + .11)), "gold")
steps_ob = steps.done()


# ================================================================== the carpet runner, its gold bands, rods and end bars
def carpet_path(t):
    pts = [(14.0 - t, t)]
    for i, (z, h0, h1) in enumerate(RISERS):
        if i > 0:
            pts.append((z - t, h0 + t))
        pts.append((z - t, h1 - .06))
        pts += nosing(z, h1, t)
    pts.append((TOP_Z + .05, 8.0 + t))
    return pts


def ribbon(x0, x1, t_out, t_in, uv_tile=None):
    """A strip following the carpet path between offsets t_in and t_out, across x0 .. x1; its top UV-mapped along it."""
    po, pi = carpet_path(t_out), carpet_path(t_in)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    n = len(po)
    s = [0.0]
    for i in range(1, n):
        s.append(s[-1] + (Vector(po[i]) - Vector(po[i - 1])).length)
    ol = [bm.verts.new(B(x0, h, z)) for z, h in po]
    orr = [bm.verts.new(B(x1, h, z)) for z, h in po]
    il = [bm.verts.new(B(x0, h, z)) for z, h in pi]
    ir = [bm.verts.new(B(x1, h, z)) for z, h in pi]
    top = []
    for i in range(n - 1):
        top.append((bm.faces.new((ol[i], ol[i + 1], orr[i + 1], orr[i])), i))
        bm.faces.new((il[i], ir[i], ir[i + 1], il[i + 1]))
        bm.faces.new((ol[i], il[i], il[i + 1], ol[i + 1]))
        bm.faces.new((orr[i], orr[i + 1], ir[i + 1], ir[i]))
    bm.faces.new((ol[0], orr[0], ir[0], il[0]))
    bm.faces.new((ol[-1], il[-1], ir[-1], orr[-1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if uv_tile:
        for f, i in top:
            for l in f.loops:
                v = l.vert
                k = i if v in (ol[i], orr[i]) else i + 1
                l[uv].uv = (v.co.x / uv_tile, s[k] / uv_tile)
    return bm


carpet = Part("RoyalStair_Carpet")
carpet.add(ribbon(-CARPET, CARPET, CT, .0015, uv_tile=2.0), "carpet", True)
for sx in (-1, 1):
    a, b = sorted((sx * (CARPET - .16), sx * (CARPET - .05)))
    carpet.add(ribbon(a, b, CT + .0025, CT - .004), "gold", True)
for z, h0, h1 in RISERS:                                 # a rod in each step's inner corner (the first on the floor)
    c = B(0, h0 + CT + .017, z - CT - .02)
    carpet.add(lathe([(.016, -CARPET - .14), (.016, CARPET + .14)], c, (1, 0, 0), 12), "gold", True)
    for sx in (-1, 1):
        carpet.add(sphere(c + Vector((sx * (CARPET + .17), 0, 0)), .028, 14, 8), "gold", True)
        carpet.add(bbox((sx * (CARPET + .1) - .02, h0, z - .055), (sx * (CARPET + .1) + .02, h0 + .05, z - .001), .006), "gold")
top_bar = B(0, 8.0 + CT + .012, TOP_Z + .035)
carpet.add(lathe([(.014, -CARPET - .1), (.014, CARPET + .1)], top_bar, (1, 0, 0), 12), "gold", True)
carpet_ob = carpet.done(keep_uv=("Stair_Carpet",))


# ================================================================== side walls, curbs, balustrades and newels (both sides)
def wall_top(z_from, z_to, rake, down):
    return [(z_from, rake(z_from) - down), (z_to, rake(z_to) - down)]


walls = Part("RoyalStair_Walls")
rails = Part("RoyalStair_Balustrade")
newels = Part("RoyalStair_Newels")
lights = []

# R352 (승규 11-04, the side walls' crowned medallion: "로고 지워줘"): the panels keep only their gold bead.
MEDALLIONS = False
PILASTERS = [18.0, 22.0, 26.0, 29.5, 32.5, 35.5, 40.0, 44.0, 48.0]   # 29.5 / 35.5: under the landing newels, where the top jogs


def wall_rake(z):
    """The wall's top (under the curb), by run."""
    for z0, z1, rake in RUNS:
        if z <= z1 + 1e-6:
            return rake(z) - CURB
    return rake_upper(z) - CURB


for s in (-1, 1):
    xi, xo = s * WALL_IN, s * WALL_OUT
    x0, x1 = sorted((xi, xo))
    # the wall: hall floor up to under the curb, its top jogging inside the landing newels
    top = [(14.0, rake_lower(14.0) - CURB), (29.5, rake_lower(29.5) - CURB), (29.5, rake_landing(29.5) - CURB),
           (35.5, rake_landing(35.5) - CURB), (35.5, rake_upper(35.5) - CURB), (TOP_Z, rake_upper(TOP_Z) - CURB)]
    walls.add(poly_x([(14.0, FLOOR), (TOP_Z, FLOOR)] + top[::-1], x0, x1), "stone")
    # skirting and its gold bead
    a, b = sorted((xo, s * (WALL_OUT + .1)))
    walls.add(bbox((a, FLOOR, 14.0), (b, FLOOR + .35, TOP_Z), .015), "stone")
    walls.add(lathe([(.025, 14.0), (.025, TOP_Z)], B(s * (WALL_OUT + .1), FLOOR + .35, 0), (0, 1, 0), 10), "gold", True)
    # the raking cornice under the curb (a stone moulding standing 0.1 proud) and a gold bead under it, run by run
    cornice = [(0, -.16), (.04 * s, -.16), (.06 * s, -.12), (.1 * s, -.08), (.1 * s, 0), (0, 0)]
    if s < 0:
        cornice = cornice[::-1]
    for z0, z1, rake in RUNS:
        za, zb = max(z0, 14.0), min(z1, TOP_Z)
        path = [(za, rake(za) - CURB), (zb, rake(zb) - CURB)]
        walls.add(sweep_x(path, xo, cornice), "stone")
        walls.add(sweep_x([(za, rake(za) - CURB - .19), (zb, rake(zb) - CURB - .19)], xo, circle(0, 0, .022, 10)), "gold", True)
    # pilasters with gold bands at their foot and head
    for zp in PILASTERS:
        head = wall_rake(zp) - .2
        a, b = sorted((xo, s * (WALL_OUT + .05)))
        walls.add(bbox((a, FLOOR + .35, zp - .23), (b, head, zp + .23), .01), "stone")
        a2, b2 = sorted((xo, s * (WALL_OUT + .07)))
        walls.add(bbox((a2, head - .05, zp - .26), (b2, head, zp + .26), .006), "gold")
        walls.add(bbox((a2, FLOOR + .35, zp - .26), (b2, FLOOR + .4, zp + .26), .006), "gold")
    # panels between the pilasters: a gold bead outline and a medallion (velvet, ring, crown) in each
    edges = [14.1] + [p for zp in PILASTERS for p in (zp - .23, zp + .23)] + [TOP_Z]
    for i in range(0, len(edges), 2):
        za, zb = edges[i] + .22, edges[i + 1] - .22
        bot = FLOOR + .75
        poly = [(za, bot), (zb, bot), (zb, wall_rake(zb) - .45), (za, wall_rake(za) - .45)]
        if poly[3][1] - bot < 1.2:
            continue
        walls.add(sweep_x(poly, xo, circle(0, 0, .018, 8), closed_path=True), "gold", True)
        if not MEDALLIONS:
            continue
        zc = (za + zb) / 2
        hc = (bot + min(wall_rake(za), wall_rake(zb)) - .45) / 2
        out = Vector((s, 0, 0))
        face = B(xo, hc, zc)
        # each layer clear of the one behind it (R346): velvet 0 .. 0.02 out, the ring round its edge, the crown on it
        walls.add(lathe([(0, -.005), (.42, -.005), (.42, .02), (0, .02)], face, out, 48), "velvet", True)
        walls.add(lathe(circle(.44, .02, .032, 10), face, out, 64, closed=True), "gold", True)
        for bmc in crown(face + out * .02, Vector((0, s, 0)), Vector((0, 0, 1)), out, w=.4, depth=.03):
            walls.add(bmc, "gold", True)

    # ---- the balustrade, run by run: curb, gold foot rail, balusters, handrail
    xc = s * 8.2
    curb = [(-.26, -CURB), (.26, -CURB), (.26, -.03), (.23, 0), (-.23, 0), (-.26, -.03)]
    hand = [(-.065, -.03), (-.05, -.05), (.05, -.05), (.065, -.03), (.06, 0), (.04, .03), (0, .04), (-.04, .03), (-.06, 0)]
    for z0, z1, rake in RUNS:
        za, zb = z0 + .32, z1 - .32
        rails.add(sweep_x([(z0, rake(z0)), (z1, rake(z1))], xc, curb), "stone")
        rails.add(sweep_x([(za, rake(za) + .02), (zb, rake(zb) + .02)], xc, circle(0, 0, .03, 10)), "gold", True)
        rails.add(sweep_x([(za - .05, rake(za) + RAIL_UP), (zb + .05, rake(zb) + RAIL_UP)], xc, hand), "gold", True)
        n = max(1, int((zb - za) / .28))
        for i in range(n + 1):
            z = za + (zb - za) * i / n
            if i in (0, n):
                continue
            h0, h1 = rake(z) + .045, rake(z) + RAIL_UP - .05
            L = h1 - h0
            if i % 2:
                prof = [(.05, 0), (.05, .06), (.035, .08), (.03, .14), (.05, .3), (.065, .42), (.05, .52), (.028, .62),
                        (.04, .66), (.028, .7), (.03, .9), (.045, .94), (.045, 1)]
            else:
                prof = [(.05, 0), (.05, .06), (.03, .1), (.04, .2), (.028, .3), (.045, .45), (.06, .5), (.045, .55),
                        (.028, .7), (.04, .8), (.028, .9), (.045, .94), (.045, 1)]
            rails.add(lathe([(r, h * L) for r, h in prof], B(xc, h0, z), (0, 0, 1), 12), "gold", True)

    # ---- the newels and their crown lanterns
    for zn, base in NEWELS:
        c = lambda dx, h, dz: B(xc + dx, base + h, zn + dz)
        newels.add(bbox((xc - .39, base, zn - .39), (xc + .39, base + .22, zn + .39), .015), "stone")
        newels.add(bbox((xc - .37, base + .22, zn - .37), (xc + .37, base + .26, zn + .37), .006), "gold")
        newels.add(bbox((xc - .32, base + .26, zn - .32), (xc + .32, base + 1.32, zn + .32), .015), "stone")
        # a gold-beaded panel with a crown on each face
        for out, right in (((1, 0, 0), (0, 1, 0)), ((-1, 0, 0), (0, -1, 0)), ((0, 1, 0), (-1, 0, 0)), ((0, -1, 0), (1, 0, 0))):
            out, right = Vector(out), Vector(right)
            face = c(0, .79, 0) + out * .32
            corners = [face + right * a + Vector((0, 0, b)) for a, b in ((-.2, -.38), (.2, -.38), (.2, .38), (-.2, .38))]
            bmf = bmesh.new()
            for p0, p1 in zip(corners, corners[1:] + corners[:1]):
                d = p1 - p0
                bmr = lathe([(.014, 0), (.014, d.length)], p0, d, 8)
                bmr.to_mesh(tmp := bpy.data.meshes.new("t")); bmr.free(); bmf.from_mesh(tmp); bpy.data.meshes.remove(tmp)
            newels.add(bmf, "gold", True)
            for bmc in crown(face + Vector((0, 0, .02)), right, Vector((0, 0, 1)), out, w=.24, depth=.025):
                newels.add(bmc, "gold", True)
        newels.add(bbox((xc - .35, base + 1.32, zn - .35), (xc + .35, base + 1.37, zn + .35), .006), "gold")
        newels.add(bbox((xc - .4, base + 1.37, zn - .4), (xc + .4, base + 1.5, zn + .4), .02), "stone")
        newels.add(bbox((xc - .35, base + 1.5, zn - .35), (xc + .35, base + 1.56, zn + .35), .01), "stone")
        # the lantern: socket, cup, globe, six ribs, crown with points and pearls, orb and cross
        newels.add(lathe([(.0, 0), (.13, 0), (.13, .03), (.1, .06), (.1, .1), (.0, .1)], c(0, 1.56, 0), (0, 0, 1), 24), "gold", True)
        newels.add(lathe([(.0, 0), (.08, 0), (.12, .03), (.2, .1), (.21, .14), (.17, .15), (.0, .15)], c(0, 1.64, 0), (0, 0, 1), 32), "gold", True)
        g = c(0, 1.94, 0)
        newels.add(sphere(g, .18, 32, 16), "glass", True)
        for k in range(6):
            a = 2 * PI * k / 6
            d = Vector((math.cos(a), math.sin(a), 0))
            arc = [g + d * (.205 * math.sin(t)) + Vector((0, 0, -.2 * math.cos(t))) for t in np.linspace(.6, PI - .35, 10)]
            bmr = bmesh.new()
            for p0, p1 in zip(arc, arc[1:]):
                seg = lathe([(.012, 0), (.012, (p1 - p0).length)], p0, p1 - p0, 6)
                seg.to_mesh(tmp := bpy.data.meshes.new("t")); seg.free(); bmr.from_mesh(tmp); bpy.data.meshes.remove(tmp)
            newels.add(bmr, "gold", True)
        crown_c = c(0, 2.1, 0)
        newels.add(lathe(circle(.12, 0, .022, 8), crown_c, (0, 0, 1), 32, closed=True), "gold", True)
        for k in range(6):
            a = 2 * PI * k / 6 + PI / 6
            d = Vector((math.cos(a), math.sin(a), 0))
            newels.add(lathe([(.03, 0), (.0, .1)], crown_c + d * .12, (0, 0, 1), 8), "gold", True)
            newels.add(sphere(crown_c + d * .12 + Vector((0, 0, .115)), .02, 10, 6), "gold", True)
        newels.add(sphere(crown_c + Vector((0, 0, .07)), .05, 16, 10), "gold", True)
        newels.add(bbox((xc - .008, base + 2.22, zn - .008), (xc + .008, base + 2.34, zn + .008), .003), "gold")
        newels.add(bbox((xc - .04, base + 2.29, zn - .008), (xc + .04, base + 2.31, zn + .008), .003), "gold")
        lights.append(g)

walls_ob = walls.done()
rails_ob = rails.done()
newels_ob = newels.done()

# ================================================================== the drain at the stair's foot (z 12 .. 14, on the foyer floor)
drain = Part("RoyalStair_Drain")
drain.add(box((-7.86, 0.0, 12.14), (7.86, .006, 13.86)), "iron")
for x0, x1, z0, z1 in ((-7.98, 7.98, 12.02, 12.14), (-7.98, 7.98, 13.86, 13.98), (-7.98, -7.86, 12.14, 13.86), (7.86, 7.98, 12.14, 13.86)):
    drain.add(bbox((x0, 0.0, z0), (x1, .035, z1), .006), "gold")
for k in range(63):
    x = -7.75 + k * .25
    drain.add(box((x - .018, .006, 12.14), (x + .018, .03, 13.86)), "iron")
for z in (12.57, 13.0, 13.43):
    drain.add(box((-7.86, .006, z - .02), (7.86, .028, z + .02)), "iron")
for x, z in ((-7.92, 12.08), (7.92, 12.08), (-7.92, 13.92), (7.92, 13.92), (0, 12.08), (0, 13.92)):
    drain.add(lathe([(0, 0), (.05, 0), (.05, .01), (.035, .02), (.02, .03), (0, .036)], B(x, .035, z), (0, 0, 1), 16), "gold", True)
drain_ob = drain.done()

# ================================================================== light markers (Unity puts a warm light in each globe)
markers = []
for i, g in enumerate(lights):
    bm = bmesh.new()
    vs = [bm.verts.new(g + Vector(d)) for d in ((.01, 0, 0), (-.01, 0, 0), (0, .01, 0))]
    bm.faces.new(vs)
    me = bpy.data.meshes.new("RoyalStair_light_%02d" % i); bm.to_mesh(me); bm.free()
    me.materials.append(MAT["glass"])
    ob = bpy.data.objects.new("RoyalStair_light_%02d" % i, me); sc.collection.objects.link(ob)
    markers.append(ob)

parts = [steps_ob, carpet_ob, walls_ob, rails_ob, newels_ob, drain_ob]
print("STAIR", sum(len(o.data.polygons) for o in parts), "faces in", len(parts), "parts,", len(markers), "lights")

for o in sc.objects:
    o.select_set(False)
for o in parts + markers:
    o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "RoyalStair.fbx"), **FBX)
print("EXPORT RoyalStair.fbx")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "royal_stair_v45.blend"))
print("SAVED royal_stair_v45.blend")

if RENDER:
    # context for the preview only: the foyer floor, the central landing and the hall floor
    ctx = Part("Preview_context")
    ctx.add(box((-20, -1, 0), (20, 0, 14)), "stone")
    ctx.add(box((-14, 7, 51), (14, 8, 66)), "marble")
    ctx.add(box((-30, FLOOR - 1, 10), (30, FLOOR, 60)), "velvet")
    ctx.done()
    os.makedirs(OUT, exist_ok=True)
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 1280, 800
    world = bpy.data.worlds.new("World"); sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.03, .03, .035, 1)
    sun = bpy.data.lights.new("Sun", "SUN"); sun.energy = 3.0; sun.angle = .2
    so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), math.radians(-20), math.radians(150))
    for i, g in enumerate(lights):
        ld = bpy.data.lights.new("Lamp%d" % i, "POINT"); ld.energy = 120; ld.color = (1, .72, .42); ld.shadow_soft_size = .15
        lo = bpy.data.objects.new("Lamp%d" % i, ld); sc.collection.objects.link(lo); lo.location = g
    shots = (("approach", (0, 2.0, 3.2), (0, 30.0, 4.5), 26),
             ("side", (15.5, 16.0, 1.0), (8.4, 29.0, 1.5), 24),
             ("landing", (-5.5, 25.0, 6.0), (0, 33.0, 4.6), 26),
             ("newel", (5.6, 27.2, 5.2), (8.2, 29.5, 5.6), 35),
             ("carpet", (2.2, 40.5, 6.2), (0, 45.0, 6.3), 30),
             ("marble", (-6.4, 30.6, 5.6), (-6.0, 33.0, 4.0), 40))
    only = [a for a in sys.argv if a.startswith("only=")]
    if only:
        keep = only[0][5:].split(",")
        shots = [s for s in shots if s[0] in keep]
    for name, loc, at, lens in shots:
        cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = .05
        cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
        cam.location = loc                              # shots in Blender coordinates (x, along, height)
        cam.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        sc.render.filepath = os.path.join(OUT, "stair_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)
