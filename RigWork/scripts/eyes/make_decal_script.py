# Builds render_design_decal.py from render_design_match.py: eyes and blush as stickers on the head (each drawn in
# its own plane tangent to the head at its middle and projected along that plane's normal), side views at 72 degrees.
import os
here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, "render_design_match.py"), encoding="utf-8").read()

draw_start = src.index("# ---- the drawing: the design sheet's front view")
mat_start = src.index("# ---- materials and the three drawn versions")
stage_start = src.index("# ---- studio stage")
views_start = src.index('VIEWS = [("v17", 80)')
head = src[:draw_start]
drawing_old = src[draw_start:mat_start]
keep_fit = drawing_old[:drawing_old.index("ZT = EZ")]
keep_limbs = drawing_old[drawing_old.index("# the piece alone, as on the design sheet"):]
stage = src[stage_start:views_start]

frames = r'''
# ---- decals: each eye and each blush drawn in its own plane, tangent to the head at its middle, projected onto the
# head along that plane's normal, as a sticker sits (the design's side views show the eyes that way). Their middles
# stay where the front view put them; their sizes are the front-view sizes undone for the turn away from the front.
from mathutils.bvhtree import BVHTree
HC = Vector((0, 0, ZF))

def surface_at(x, z):
    hit = head_tree.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
    return hit[0]

def frame_at(x, z, w_front, h_front):
    p = surface_at(x, z)
    n = (p - HC).normalized()
    eu = Vector((-n.y, n.x, 0)).normalized()
    ev = n.cross(eu).normalized()
    turn = math.atan2(abs(n.x), -n.y)
    tilt = math.asin(max(-1.0, min(1.0, n.z)))
    return dict(p=p, n=n, eu=eu, ev=ev, w=w_front / math.cos(turn), h=h_front / math.cos(tilt), turn=turn, tilt=tilt)
'''

tail = r'''
head_tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
EYES = [frame_at(sx * EX, EZ, EW, EH) for sx in (-1, 1)]
BLUSH = [frame_at(sx * BX, BZ, BW, BH) for sx in (-1, 1)]
for f in EYES + BLUSH:
    print("decal at", [round(c, 4) for c in f["p"]], "turn %.1f tilt %.1f size %.4f x %.4f" % (
        math.degrees(f["turn"]), math.degrees(f["tilt"]), f["w"], f["h"]))

def decal_texture(name, w, h, half, glint, colour, size=1024):
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    U = ((xx + 0.5) / size * 2 - 1) * half
    V = ((yy + 0.5) / size * 2 - 1) * half
    ppm = size / (2 * half)
    d = (np.sqrt((U / (w / 2)) ** 2 + (V / (h / 2)) ** 2) - 1) * min(w, h) / 2 * ppm
    al = np.clip(0.5 - d / 1.2, 0, 1)
    rgb = np.zeros((size, size, 3), dtype=np.float32) + np.array(colour, dtype=np.float32)
    if glint:
        gd = (np.sqrt((U - 0.125 * w) ** 2 + (V - (h / 2 - 0.265 * h)) ** 2) - 0.19 * w) * ppm
        g = np.clip(0.5 - gd, 0, 1) * al
        rgb = rgb * (1 - g[..., None]) + g[..., None]
    img = bpy.data.images.new(name, size, size, alpha=True)
    img.pixels = np.dstack([rgb, al]).ravel()
    img.filepath_raw = os.path.join(OUT, name + ".png")
    img.file_format = "PNG"
    img.save()
    return os.path.join(OUT, name + ".png")

EHALF = 0.75 * max(EYES[0]["h"], EYES[0]["w"])
BHALF = 0.75 * max(BLUSH[0]["h"], BLUSH[0]["w"])
EYE_TEX = decal_texture("eye_decal", EYES[0]["w"], EYES[0]["h"], EHALF, True, (0.0, 0.0, 0.0))
BLUSH_TEX = decal_texture("blush_decal", BLUSH[0]["w"], BLUSH[0]["h"], BHALF, False, (0.96, 0.60, 0.54), 512)

def decal_uv(p, f, half):
    q = p - f["p"]
    return (q.dot(f["eu"]) / half + 1) / 2, (q.dot(f["ev"]) / half + 1) / 2

body_mat = bpy.data.materials.new("Body")
body_mat.use_nodes = True
_b = body_mat.node_tree.nodes["Principled BSDF"]
_v = body_mat.node_tree.nodes.new("ShaderNodeVertexColor")
_v.layer_name = "Col"
body_mat.node_tree.links.new(_v.outputs["Color"], _b.inputs["Base Color"])
_b.inputs["Roughness"].default_value = 0.5
me.materials.clear()
me.materials.append(body_mat)

def tex_material(name, path, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    tc = nt.nodes.new("ShaderNodeTexCoord")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(path)
    img.extension = "CLIP"
    img.interpolation = "Cubic"
    nt.links.new(tc.outputs["UV"], img.inputs["Vector"])
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(img.outputs["Alpha"], bsdf.inputs["Alpha"])
    return m

from mathutils.kdtree import KDTree
_hv = [v.co.copy() for v in me.vertices if v.co.z > 0.64 and v.co.y < 0.05]
_kd = KDTree(len(_hv))
for i, p in enumerate(_hv):
    _kd.insert((p - HC).normalized(), i)
_kd.balance()

def smooth_radius(d):
    hits = _kd.find_n(d, 6)
    ws = [1.0 / max(dist, 1e-4) for _, _, dist in hits]
    return sum(w * (_hv[i] - HC).length for w, (_, i, _) in zip(ws, hits)) / sum(ws)

def lens(f, half, name, n=36):
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    grid = []
    for j in range(n + 1):
        row = []
        for i in range(n + 1):
            u, v = (i / n * 2 - 1) * half, (j / n * 2 - 1) * half
            q = f["p"] + f["eu"] * u + f["ev"] * v
            d = (q - HC).normalized()
            dn = (u / (f["w"] * 0.62)) ** 2 + (v / (f["h"] * 0.62)) ** 2
            vert = bm.verts.new(HC + d * (smooth_radius(d) + 0.0004 + 0.0025 * max(0.0, 1 - dn)))
            row.append((vert, ((u / half + 1) / 2, (v / half + 1) / 2)))
        grid.append(row)
    for j in range(n):
        for i in range(n):
            cs = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
            fc = bm.faces.new([c[0] for c in cs])
            for l, c in zip(fc.loops, cs):
                l[uvl].uv = c[1]
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    for pl in m.polygons:
        pl.use_smooth = True
    mid = m.polygons[len(m.polygons) // 2]
    if (mid.center - HC).dot(mid.normal) < 0:
        m.flip_normals()
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

def patch(f, half, name):
    bm = bmesh.new()
    bm.from_mesh(me)
    # every facet that reaches into the sticker's square (facets are bigger than a blush); the drawing is clear
    # outside the sticker, so the extra facets show nothing
    def near_(fc):
        c = fc.calc_center_median()
        q = c - f["p"]
        return abs(q.dot(f["eu"])) < half + 0.04 and abs(q.dot(f["ev"])) < half + 0.04 and (c - HC).normalized().dot(f["n"]) > 0.5
    keep = [fc for fc in bm.faces if near_(fc)]
    nb = bmesh.new()
    vm = {}
    for fc in keep:
        vs = []
        for v in fc.verts:
            if v not in vm:
                vm[v] = nb.verts.new(v.co + (v.co - HC).normalized() * 0.00025)
            vs.append(vm[v])
        nb.faces.new(vs)
    bm.free()
    uvl = nb.loops.layers.uv.new("UVMap")
    for fc in nb.faces:
        for l in fc.loops:
            l[uvl].uv = decal_uv(l.vert.co, f, half)
    m = bpy.data.meshes.new(name)
    nb.to_mesh(m)
    nb.free()
    for pl in m.polygons:
        pl.use_smooth = False
    o = bpy.data.objects.new(name, m)
    bpy.context.scene.collection.objects.link(o)
    return o

eye_mat = tex_material("EyeDecal", EYE_TEX, 0.28)
blush_mat = tex_material("BlushDecal", BLUSH_TEX, 0.5)
for k, f in enumerate(EYES):
    lens(f, EHALF, "EyeLens%d" % k).data.materials.append(eye_mat)
for k, f in enumerate(BLUSH):
    patch(f, BHALF, "Blush%d" % k).data.materials.append(blush_mat)
old_eyes.hide_render = True

'''

views = r'''VIEWS = [("v17", 72), ("v18", 0), ("v19", -72), ("v20", 38)]
cam.data.lens = 100
sc.render.resolution_x = sc.render.resolution_y = 627
for vname, deg in VIEWS:
    az, el = math.radians(deg), math.radians(6)
    target = Vector((0, 0, 0.56))
    cam.location = target + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * 3.3
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, "design_%s.png" % vname)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED", vname)
print("DONE")
'''

# the old eye geometry is copied out (and hidden) in the head part; the materials section of the old script made
# old_mat etc. - not needed. The stage needs `cam`, created inside the stage section.
out = head + keep_fit + frames + keep_limbs + tail + stage + views
open(os.path.join(here, "render_design_decal.py"), "w", encoding="utf-8").write(out)
print("written", len(out))
