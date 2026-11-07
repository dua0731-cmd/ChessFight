# R502: the user's drawbridge kit (Source/R502_DrawbridgeWalls.fbx, five pieces in one 500k-triangle mesh: the lowered
# bridge with pillars and chains, the raised bridge on its hinge drums, a rook pylon with a chain, the bridge-approach
# floor with iron tracks, the central parapet with the gold crest). The user: "조금 깨진 곳 있어 고쳐".
# What was broken (renders of each piece alone, 11-07):
#   track floor  smears in its stone: wood-coloured streaks and dark grey scribbles in the middle of the floor
#   parapet      grey/white smears on the pillar caps and the panel's top; dark gaps where the panel meets the pillars
#   the rest     only on undersides the game never shows (left as they are)
# The fix:
#   1. split the mesh into its five pieces (stray chain links go with their piece);
#   2. texture: on the stone faces of the floor and the parapet, texels that are not stone (wood-orange, or grey /
#      white with no warmth) are inpainted from the stone around them, inside the same UV island, so the brick lines
#      stay; iron and gold faces are not touched;
#   3. geometry: each dark gap beside the parapet panel is found by rays from the front and filled with a stone block
#      that takes a clean stone patch of the kit's own texture.
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_kit_fix.py
# Writes Source/R502_DrawbridgeKit_Fixed.blend (objects KIT_LoweredBridge, KIT_RaisedBridge, KIT_TrackFloor,
# KIT_Parapet, KIT_Pylon; the fixed texture packed) and kit_fix_before_after.png.
import os, sys, math
import bpy, bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source", "R502_DrawbridgeWalls.fbx")
OUT = os.path.join(HERE, "Source", "R502_DrawbridgeKit_Fixed.blend")
NAMES = ["KIT_LoweredBridge", "KIT_RaisedBridge", "KIT_TrackFloor", "KIT_Parapet", "KIT_Pylon"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC)
ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.remove_doubles(threshold=1e-5)
bpy.ops.mesh.separate(type="LOOSE")
bpy.ops.object.mode_set(mode="OBJECT")
obs = sorted([o for o in bpy.context.scene.objects if o.type == "MESH"], key=lambda o: -len(o.data.vertices))
pieces = obs[:5]
for o, n in zip(pieces, NAMES):
    o.name = n


def centre(o):
    return sum((o.matrix_world @ Vector(b) for b in o.bound_box), Vector()) / 8


for o in obs[5:]:
    best = min(pieces, key=lambda p: (centre(p) - centre(o)).length)
    for x in bpy.context.selected_objects:
        x.select_set(False)
    o.select_set(True)
    best.select_set(True)
    bpy.context.view_layer.objects.active = best
    bpy.ops.object.join()
    print("joined a stray link to", best.name)
P = {o.name: o for o in pieces}

mat = P["KIT_TrackFloor"].data.materials[0]
img = [n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and "base" in n.image.name][0]
W, H = img.size
tex = np.empty(W * H * 4, np.float32)
img.pixels.foreach_get(tex)
tex = tex.reshape(H, W, 4)
before = tex.copy()


def hsv(a):
    mx, mn = a.max(-1), a.min(-1)
    s = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0)
    return s, mx


def classify_texels(px):
    """Per texel: 0 stone, 1 wood, 2 grey, 3 gold (the same rules as before, on arrays)."""
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    mx, mn = px.max(-1), px.min(-1)
    s = (mx - mn) / np.maximum(mx, 1e-4)
    out = np.zeros(r.shape, np.int8)
    out[s < .15] = 2
    out[(s > .42) & (r > g + .12)] = 1
    out[(s > .55) & (r > .5) & (g > .35) & (b < .3) & (g > r * .62)] = 3
    return out


def face_mask(o, keep, allow=("stone", "wood", "grey")):
    """Rasterises the UV triangles of o's faces that pass keep(centres, normals) (arrays) and whose texels vote for
    a class in `allow` into a texel mask (vectorised: the kit's pieces have 10^5 triangles)."""
    me = o.data
    me.calc_loop_triangles()
    T = len(me.loop_triangles)
    loops = np.empty(T * 3, np.int32)
    me.loop_triangles.foreach_get("loops", loops)
    pidx = np.empty(T, np.int32)
    me.loop_triangles.foreach_get("polygon_index", pidx)
    uvs = np.empty(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uvs)
    pts = uvs.reshape(-1, 2)[loops].reshape(T, 3, 2) * np.array([W, H], np.float32)
    cen = np.empty(len(me.polygons) * 3, np.float32)
    me.polygons.foreach_get("center", cen)
    nor = np.empty(len(me.polygons) * 3, np.float32)
    me.polygons.foreach_get("normal", nor)
    cen, nor = cen.reshape(-1, 3)[pidx], nor.reshape(-1, 3)[pidx]
    sel = keep(cen, nor)
    c = pts.mean(1)
    samples = [c] + [c + (pts[:, i] - c) * .6 for i in range(3)] + [(pts[:, i] + pts[:, (i + 1) % 3]) / 2 * .8 + c * .2 for i in range(3)]
    votes = np.zeros((T, 4), np.int32)
    for sp in samples:
        xi = np.clip(sp[:, 0].astype(int), 0, W - 1)
        yi = np.clip(sp[:, 1].astype(int), 0, H - 1)
        k = classify_texels(before[yi, xi, :3])
        votes[np.arange(T), k] += 1
    cls = votes.argmax(1)
    names = ["stone", "wood", "grey", "gold"]
    sel &= np.isin(cls, [names.index(a) for a in allow])
    m = np.zeros((H, W), bool)
    for tri in pts[sel]:
        x0, y0 = np.floor(tri.min(0)).astype(int)
        x1, y1 = np.ceil(tri.max(0)).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        (ax, ay), (bx, by), (cx, cy) = tri
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-9:
            continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
        m[y0:y1 + 1, x0:x1 + 1] |= (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
    # one texel in from the edge, so the iron or gold next to a stone face is never touched
    m &= np.roll(m, 1, 0) & np.roll(m, -1, 0) & np.roll(m, 1, 1) & np.roll(m, -1, 1)
    print("MASK %s: %d of %d triangles" % (o.name, int(sel.sum()), T))
    return m


def triangle_class(pts):
    votes = np.bincount(classify_texels(np.array([before[min(max(int(y), 0), H - 1), min(max(int(x), 0), W - 1), :3]
                                                  for x, y in [pts.mean(0)] + list(pts)])), minlength=4)
    return ["stone", "wood", "grey", "gold"][int(votes.argmax())]


def stone_ref(m):
    """The stone colour under a mask: the median of its warm, light texels."""
    px = tex[m][:, :3]
    s, v = hsv(px)
    warm = (px[:, 0] > px[:, 2] + .05) & (v > .55) & (s < .45)
    return np.median(px[warm], 0)


def repair(m, label, kinds=("wood", "grey"), dark_below=0., grey_s=.17, grey_dv=.1):
    """Repaints the texels of `kinds` inside mask m from the stone around them, tile by tile (only where needed)."""
    px = tex[..., :3]
    s, v = hsv(px)
    ref = stone_ref(m)
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    bad = np.zeros((H, W), bool)
    if "wood" in kinds:
        bad |= (s > .42) & (r > g + .12) & (g > b + .02) & (v > .35)                # orange-brown plank streaks
    if "grey" in kinds:
        bad |= (s < grey_s) & ((v < ref.max() - grey_dv) | (v > ref.max() + .05))   # cold grey scribbles, white smears
    if "dark" in kinds:
        bad |= (s < .25) & (v < dark_below)                                         # darker than the iron tracks
    bad &= m
    for _ in range(2):          # their soft edges too
        bad = bad | (m & (np.roll(bad, 1, 0) | np.roll(bad, -1, 0) | np.roll(bad, 1, 1) | np.roll(bad, -1, 1)))
    print("REPAIR %s (%s): %d texels in the faces, %d repainted" % (label, "+".join(kinds), int(m.sum()), int(bad.sum())))
    T, PAD = 128, 24
    rng = np.random.default_rng(1)
    for ty in range(0, H, T):
        for tx in range(0, W, T):
            if not bad[ty:ty + T, tx:tx + T].any():
                continue
            y0, y1, x0, x1 = max(ty - PAD, 0), min(ty + T + PAD, H), max(tx - PAD, 0), min(tx + T + PAD, W)
            sub = tex[y0:y1, x0:x1, :3].copy()
            hole = bad[y0:y1, x0:x1]
            known = (m[y0:y1, x0:x1] & ~hole).astype(np.float32)
            sub[hole] = ref
            reach = (known + hole).astype(np.float32)
            for it in range(150):
                acc = np.zeros_like(sub)
                wsum = np.zeros(sub.shape[:2], np.float32)
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    w = np.roll(np.roll(reach, dy, 0), dx, 1)
                    acc += np.roll(np.roll(sub, dy, 0), dx, 1) * w[..., None]
                    wsum += w
                new = acc / np.maximum(wsum, 1e-6)[..., None]
                sub[hole] = new[hole]
            grain = (rng.random(sub.shape[:2]) - .5) * .025
            sub[hole] += grain[hole][:, None]
            # write back only this tile's own texels (the padding was context)
            iy0, ix0 = ty - y0, tx - x0
            tile_hole = hole[iy0:iy0 + T, ix0:ix0 + T]
            dst = tex[ty:ty + T, tx:tx + T, :3]
            dst[tile_hole] = np.clip(sub[iy0:iy0 + T, ix0:ix0 + T][tile_hole], 0, 1)


# ---- 2. textures
floor = P["KIT_TrackFloor"]
fz = np.array([v.co for v in floor.data.vertices])
zlo, zhi = fz[:, 2].min(), fz[:, 2].max()
# the floor: only the wood-coloured smears (its grey is the iron tracks: never touched)
m_floor = face_mask(floor, lambda c, n: (n[:, 2] > .7) & (c[:, 2] < zlo + .85 * (zhi - zlo)), allow=("stone", "wood"))
repair(m_floor, "track floor", kinds=("wood",))
# then the grey scribbles, away from the two iron tracks: each track is a line fitted through its own gold studs
me_f = floor.data
me_f.calc_loop_triangles()
T = len(me_f.loop_triangles)
loops = np.empty(T * 3, np.int32)
me_f.loop_triangles.foreach_get("loops", loops)
pidx = np.empty(T, np.int32)
me_f.loop_triangles.foreach_get("polygon_index", pidx)
uvs = np.empty(len(me_f.loops) * 2, np.float32)
me_f.uv_layers.active.data.foreach_get("uv", uvs)
tri_uv = uvs.reshape(-1, 2)[loops].reshape(T, 3, 2).mean(1) * np.array([W, H], np.float32)
cen = np.empty(len(me_f.polygons) * 3, np.float32)
me_f.polygons.foreach_get("center", cen)
cen = cen.reshape(-1, 3)[pidx]
low = cen[:, 2] < zlo + .85 * (zhi - zlo)
gold = classify_texels(before[np.clip(tri_uv[:, 1].astype(int), 0, H - 1), np.clip(tri_uv[:, 0].astype(int), 0, W - 1), :3]) == 3
studs = cen[low & gold][:, :2]
# two tracks: k-means on the stud positions themselves, seeded with the two studs farthest apart across the floor
# (the floor is longer than the tracks are apart, so splitting on one axis can cut each track in two)
seeds = []
mean = studs.mean(0)
_, _, vt = np.linalg.svd(studs - mean, full_matrices=False)
along = vt[0]
# the tracks run along the floor's long side: take the across axis as the floor's own short side
fx = fz[:, :2]
_, _, vf = np.linalg.svd(fx - fx.mean(0), full_matrices=False)
across_f = vf[1]
q = (studs - mean) @ across_f
k = np.array([q.min(), q.max()])
for _ in range(20):
    lab = np.abs(q[:, None] - k[None]).argmin(1)
    k = np.array([q[lab == j].mean() for j in range(2)])
rails = []
for j in range(2):
    pts = studs[lab == j]
    c0 = pts.mean(0)
    _, _, v2 = np.linalg.svd(pts - c0, full_matrices=False)
    rails.append((c0, v2[1]))          # a point on the track and its across direction
    print("TRACK %d: %d stud faces, through (%.3f, %.3f)" % (j, len(pts), c0[0], c0[1]))
RAIL_HALF = .04


def off_tracks(c, n):
    d = np.min([np.abs((c[:, :2] - c0) @ nrm) for c0, nrm in rails], 0)
    return (n[:, 2] > .7) & (c[:, 2] < zlo + .85 * (zhi - zlo)) & (d > RAIL_HALF)


m_floor2 = face_mask(floor, off_tracks, allow=("stone", "grey"))
repair(m_floor2, "track floor off the tracks", kinds=("grey",))
m_floor3 = face_mask(floor, lambda c, n: (n[:, 2] > .7) & (c[:, 2] < zlo + .85 * (zhi - zlo)), allow=("stone",))
repair(m_floor3, "track floor, grey inside stone faces", kinds=("grey",))
# the iron's own brightness: grey texels on the tracks (within the guard band of the fitted lines)
m_rail = face_mask(floor, lambda c, n: (n[:, 2] > .7) & (np.min([np.abs((c[:, :2] - c0) @ nrm) for c0, nrm in rails], 0) < .012), allow=("grey",))
rp = tex[m_rail][:, :3]
rs, rv = hsv(rp)
iron_v = float(np.percentile(rv[rs < .2], 10)) if (rs < .2).any() else .3
print("IRON brightness: 10th percentile %.3f, median %.3f" % (iron_v, float(np.median(rv[rs < .2])) if (rs < .2).any() else -1))
m_floor4 = face_mask(floor, lambda c, n: (n[:, 2] > .7) & (c[:, 2] < zlo + .85 * (zhi - zlo)), allow=("stone", "grey", "wood"))
repair(m_floor4, "track floor, darker than iron", kinds=("dark",), dark_below=iron_v - .04)
# the parapet: grey and white smears on its stone tops (the gold frame and crest are other faces, never touched)
par = P["KIT_Parapet"]
m_par = face_mask(par, lambda c, n: n[:, 2] > .2, allow=("stone", "grey"))
repair(m_par, "parapet tops", kinds=("grey",), grey_s=.24, grey_dv=.06)

# the last two marks, found by casting the renders' pixels back to the texture (11-07): the Y-shaped grey scribble beside
# the floor's right track and a dark mark on the parapet's slanted top. Inside each texture rectangle only, texels
# greyer and darker than the stone are filled from the stone at the rectangle's edge.
SPOTS = {"floor Y scribble": (1580, 1820, 1520, 1680), "parapet top mark": (1722, 1805, 508, 584)}


def spot(label, x0, x1, y0, y1, pad=12):
    sub = tex[y0 - pad:y1 + pad, x0 - pad:x1 + pad, :3].copy()
    s, v = hsv(sub)
    inner = np.zeros(sub.shape[:2], bool)
    inner[pad:-pad, pad:-pad] = True
    stone = np.median(sub[~inner & (v > .75) & (s < .45)], 0)
    hole = inner & (((s < .3) & (v < stone.max() - .08)) | (v < stone.max() - .22))   # grey, or clearly dark of any hue
    for _ in range(2):
        hole = hole | (inner & (np.roll(hole, 1, 0) | np.roll(hole, -1, 0) | np.roll(hole, 1, 1) | np.roll(hole, -1, 1)))
    sub[hole] = stone
    for _ in range(300):
        acc = (np.roll(sub, 1, 0) + np.roll(sub, -1, 0) + np.roll(sub, 1, 1) + np.roll(sub, -1, 1)) / 4
        sub[hole] = acc[hole]
    rng = np.random.default_rng(2)
    sub[hole] += ((rng.random(sub.shape[:2]) - .5) * .025)[hole][:, None]
    tex[y0 - pad:y1 + pad, x0 - pad:x1 + pad, :3] = np.clip(sub, 0, 1)
    print("SPOT %s: %d texels" % (label, int(hole.sum())))


for label, rect in SPOTS.items():
    spot(label, *rect)

# What is still seen of the Y is found exactly: a render whose pixels are the floor's UV coordinates (no filtering),
# from the before/after view; every pixel inside the scribble's screen box (left of the right track) marks its texel.
def screen_texels(obj, az, el, box, res=(700, 500)):
    sc = bpy.context.scene
    saved = [(o, o.hide_render) for o in sc.objects]
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = o is not obj
    uvm = bpy.data.materials.new("uv pass")
    nt = uvm.node_tree
    nt.nodes.clear()
    tc = nt.nodes.new("ShaderNodeTexCoord")
    em = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(tc.outputs["UV"], em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    old = list(obj.data.materials)
    idx = [pp.material_index for pp in obj.data.polygons]
    obj.data.materials.clear()
    obj.data.materials.append(uvm)
    cam = bpy.data.cameras.new("uvcam")
    cam.lens = 50
    co = bpy.data.objects.new("uvcam", cam)
    sc.collection.objects.link(co)
    prev_cam = sc.camera
    sc.camera = co
    pts = [obj.matrix_world @ Vector(b) for b in obj.bound_box]
    l2 = Vector([min(q[i] for q in pts) for i in range(3)])
    h2 = Vector([max(q[i] for q in pts) for i in range(3)])
    c, r = (l2 + h2) / 2, (h2 - l2).length
    d = Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
    co.location = c + d * r * 1.25
    co.rotation_euler = (c - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 1
    sc.cycles.filter_width = .01
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "OPEN_EXR"
    path = os.path.join(bpy.app.tempdir, "uvpass.exr")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    a = np.empty(res[0] * res[1] * 4, np.float32)
    im.pixels.foreach_get(a)
    a = a.reshape(res[1], res[0], 4)[::-1]          # rows top-down, as the renders are looked at
    x0, x1, y0, y1 = box
    sel = a[y0:y1, x0:x1]
    sel = sel[sel[..., 3] > .5]
    tx = np.clip((sel[:, 0] * W).astype(int), 0, W - 1)
    ty = np.clip((sel[:, 1] * H).astype(int), 0, H - 1)
    obj.data.materials.clear()
    for m_ in old:
        obj.data.materials.append(m_)
    for pp, i in zip(obj.data.polygons, idx):
        pp.material_index = i
    sc.camera = prev_cam
    bpy.data.objects.remove(co)
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.cycles.filter_width = 1.5
    sc.view_settings.view_transform = "AgX"
    for o, h in saved:
        o.hide_render = h
    return tx, ty


tx, ty = screen_texels(floor, -60, 55, (388, 470, 105, 210))
mask_y = np.zeros((H, W), bool)
mask_y[ty, tx] = True
for _ in range(4):
    mask_y |= np.roll(mask_y, 1, 0) | np.roll(mask_y, -1, 0) | np.roll(mask_y, 1, 1) | np.roll(mask_y, -1, 1)
print("SCREEN Y: %d pixels -> %d texels (x %d..%d, y %d..%d)" % (len(tx), int(mask_y.sum()), tx.min(), tx.max(), ty.min(), ty.max()))
ys, xs = np.nonzero(mask_y)
Y0, Y1, X0, X1 = max(ys.min() - 16, 0), min(ys.max() + 17, H), max(xs.min() - 16, 0), min(xs.max() + 17, W)
sub = tex[Y0:Y1, X0:X1, :3].copy()
hole = mask_y[Y0:Y1, X0:X1]
sv, vv = hsv(sub)
stone_c = np.median(sub[~hole & (vv > .75) & (sv < .45)], 0)
# only texels in the box that are greyer or darker than the stone are the scribble; the rest of the box stays
hole = hole & ((sv < .3) & (vv < stone_c.max() - .05))
for _ in range(3):
    hole = hole | (mask_y[Y0:Y1, X0:X1] & (np.roll(hole, 1, 0) | np.roll(hole, -1, 0) | np.roll(hole, 1, 1) | np.roll(hole, -1, 1)))
sub[hole] = stone_c
for _ in range(300):
    acc = (np.roll(sub, 1, 0) + np.roll(sub, -1, 0) + np.roll(sub, 1, 1) + np.roll(sub, -1, 1)) / 4
    sub[hole] = acc[hole]
tex[Y0:Y1, X0:X1, :3] = np.clip(sub, 0, 1)
print("SCREEN Y repainted %d texels" % int(hole.sum()))
Y_HOLE = (Y0, Y1, X0, X1, hole)

# the Y is also carved into the normal map (it still shaded after its colour was gone): smooth the normals there too
nimg = [n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and n.image != img][0]
NW, NH = nimg.size
ntex = np.empty(NW * NH * 4, np.float32)
nimg.pixels.foreach_get(ntex)
ntex = ntex.reshape(NH, NW, 4)
# the exact Y texels, scaled to the normal map
Y0_, Y1_, X0_, X1_, hole_ = Y_HOLE
hy, hx = np.nonzero(hole_)
nmask = np.zeros((NH, NW), bool)
nmask[np.clip(((hy + Y0_) * NH / H).astype(int), 0, NH - 1), np.clip(((hx + X0_) * NW / W).astype(int), 0, NW - 1)] = True
for _ in range(2):
    nmask |= np.roll(nmask, 1, 0) | np.roll(nmask, -1, 0) | np.roll(nmask, 1, 1) | np.roll(nmask, -1, 1)
ny, nx_ = np.nonzero(nmask)
if len(ny):
    a0, a1, b0, b1 = max(ny.min() - 8, 0), min(ny.max() + 9, NH), max(nx_.min() - 8, 0), min(nx_.max() + 9, NW)
    sub = ntex[a0:a1, b0:b1, :3].copy()
    hm = nmask[a0:a1, b0:b1]
    for _ in range(300):
        acc = (np.roll(sub, 1, 0) + np.roll(sub, -1, 0) + np.roll(sub, 1, 1) + np.roll(sub, -1, 1)) / 4
        sub[hm] = acc[hm]
    ntex[a0:a1, b0:b1, :3] = sub
    print("NORMAL screen Y: smoothed %d texels" % int(hm.sum()))
for label in ("floor Y scribble",):
    x0, x1, y0, y1 = [int(round(c * NW / W)) for c in SPOTS[label][:2]] + [int(round(c * NH / H)) for c in SPOTS[label][2:]]
    pad = 6
    sub = ntex[y0 - pad:y1 + pad, x0 - pad:x1 + pad, :3].copy()
    inner = np.zeros(sub.shape[:2], bool)
    inner[pad:-pad, pad:-pad] = True
    for _ in range(300):
        acc = (np.roll(sub, 1, 0) + np.roll(sub, -1, 0) + np.roll(sub, 1, 1) + np.roll(sub, -1, 1)) / 4
        sub[inner] = acc[inner]
    ntex[y0 - pad:y1 + pad, x0 - pad:x1 + pad, :3] = sub
    print("NORMAL %s: smoothed %d x %d" % (label, x1 - x0, y1 - y0))
nimg.pixels.foreach_set(ntex.ravel())
nimg.update()
nimg.pack()

# ---- 3. the parapet's gaps: rays from the front (-y) find the deep slots beside the panel
dg = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(par, dg)
pv = [par.matrix_world @ v.co for v in par.data.vertices]
lo = Vector([min(p[i] for p in pv) for i in range(3)])
hi = Vector([max(p[i] for p in pv) for i in range(3)])
nx, nz = 240, 120
depth = np.full((nz, nx), np.nan)
for i in range(nx):
    for k in range(nz):
        x = lo.x + (i + .5) / nx * (hi.x - lo.x)
        z = lo.z + (k + .5) / nz * (hi.z - lo.z)
        loc, n, f, d = bvh.ray_cast(Vector((x, lo.y - 1, z)), Vector((0, 1, 0)))
        if loc is not None:
            depth[k, i] = loc.y - lo.y
D = hi.y - lo.y
med = np.nanmedian(depth)
slot = np.nan_to_num(depth, nan=D) > med + .2 * D           # much deeper than the face around: a gap or a hole
# only below the panel's top (above it is open air between the pillars): the panel's top is where the middle
# third's columns stop being hit (the lower quarter of them, so the crest in the middle does not count)
tops = []
for i in range(nx // 3, 2 * nx // 3):
    hit = np.nonzero(~np.isnan(depth[:, i]))[0]
    if len(hit):
        tops.append(hit.max())
panel_top = int(np.percentile(tops, 25)) if tops else nz
slot[panel_top - 1:, :] = False
slot[:panel_top - 40, :] = False      # the slots are in the panel's upper part; lower down the base course is set back
print("PANEL top row %d of %d, median depth %.4f of %.4f" % (panel_top, nz, med, D))
for row in range(panel_top - 12, panel_top, 3):
    print("DEPTH row %3d:" % row, " ".join("--" if np.isnan(d) else "%02d" % int(d / D * 99) for d in depth[row, ::4]))
fill_uv = None
fills = []
cols = np.nonzero(slot.any(0))[0]
if len(cols):
    # group the slot columns into runs (one per gap)
    runs, start = [], cols[0]
    for a, b in zip(cols, cols[1:]):
        if b != a + 1:
            runs.append((start, a))
            start = b
    runs.append((start, cols[-1]))
    for a, b in runs:
        rows = np.nonzero(slot[:, a:b + 1].any(1))[0]
        x0 = lo.x + a / nx * (hi.x - lo.x)
        x1 = lo.x + (b + 1) / nx * (hi.x - lo.x)
        z0 = lo.z + rows.min() / nz * (hi.z - lo.z)
        z1 = lo.z + (rows.max() + 1) / nz * (hi.z - lo.z)
        print("RUN x %.4f..%.4f z %.4f..%.4f (%d columns)" % (x0, x1, z0, z1, b - a + 1))
        if (x1 - x0) > .25 * (hi.x - lo.x) or a == 0 or b == nx - 1:
            continue                                          # not a gap: open space, or the piece's own ends
        dmax = float(np.nanmax(np.where(slot[:, a:b + 1], depth[:, a:b + 1], np.nan)))
        fills.append((x0, x1, z0, z1, dmax))
        print("GAP x %.4f..%.4f z %.4f..%.4f" % (x0, x1, z0, z1))
# a clean stone texel to give the filler its colour: the centre of the parapet's front stone
loc, n, fidx, d = bvh.ray_cast(Vector((lo.x + .08 * (hi.x - lo.x), lo.y - 1, lo.z + .3 * (hi.z - lo.z))), Vector((0, 1, 0)))
poly = par.data.polygons[fidx]
uvl = par.data.uv_layers.active.data
fill_uv = sum((Vector(uvl[l].uv) for l in poly.loop_indices), Vector((0, 0))) / len(poly.loop_indices)
for x0, x1, z0, z1, dmax in fills:
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    # from the panel's face back to the slot's bottom, no further (it must not show behind)
    yb = lo.y + dmax + .002
    yf = lo.y + med + .002
    bmesh.ops.scale(bm, vec=(x1 - x0 + .002, yb - yf, z1 - z0 + .002), verts=bm.verts)
    bmesh.ops.translate(bm, verts=bm.verts, vec=((x0 + x1) / 2, (yf + yb) / 2, (z0 + z1) / 2))
    uv = bm.loops.layers.uv.new(par.data.uv_layers.active.name)
    for f in bm.faces:
        for i, l in enumerate(f.loops):
            l[uv].uv = (fill_uv.x + (i % 2) * .002, fill_uv.y + (i // 2) * .002)
    me = bpy.data.meshes.new("gap fill")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    f = bpy.data.objects.new("gap fill", me)
    bpy.context.scene.collection.objects.link(f)
    for x in bpy.context.selected_objects:
        x.select_set(False)
    f.select_set(True)
    par.select_set(True)
    bpy.context.view_layer.objects.active = par
    bpy.ops.object.join()

# ---- write the texture back, pack, save
img.pixels.foreach_set(tex.ravel())
img.update()
img.pack()
fixed_png = os.path.join(HERE, "Source", "R502_DrawbridgeKit_base_color_fixed.png")
img.filepath_raw = fixed_png
img.file_format = "PNG"
img.save()
img.pack()
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("saved", OUT)

# ---- before/after: the floor and the parapet, close, with the old and the new texture
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 32
sc.cycles.use_denoising = True
w = bpy.data.worlds.new("w")
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (.6, .62, .66, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.3
sc.world = w
sun = bpy.data.lights.new("s", "SUN")
sun.energy = 2.5
so = bpy.data.objects.new("s", sun)
so.rotation_euler = (math.radians(45), 0, math.radians(35))
sc.collection.objects.link(so)
cam = bpy.data.cameras.new("c")
cam.lens = 50
co = bpy.data.objects.new("c", cam)
sc.collection.objects.link(co)
sc.camera = co
sc.render.resolution_x, sc.render.resolution_y = 700, 500
shots = []
for name, az, el in (("KIT_TrackFloor", -60, 55), ("KIT_Parapet", -90, 15), ("KIT_Parapet", -60, 60)):
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = o.name != name
    p = P[name] if name in P else sc.objects[name]
    pts = [p.matrix_world @ Vector(b) for b in p.bound_box]
    l2 = Vector([min(q[i] for q in pts) for i in range(3)])
    h2 = Vector([max(q[i] for q in pts) for i in range(3)])
    c, r = (l2 + h2) / 2, (h2 - l2).length
    d = Vector((math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
    co.location = c + d * r * 1.25
    co.rotation_euler = (c - co.location).to_track_quat("-Z", "Y").to_euler()
    row = []
    for label, data in (("before", before), ("after", tex)):
        img.pixels.foreach_set(data.ravel())
        img.update()
        f = os.path.join(bpy.app.tempdir, "kit_%s_%d.png" % (label, len(shots)))
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(f)
        a = np.empty(im.size[0] * im.size[1] * 4, np.float32)
        im.pixels.foreach_get(a)
        row.append(a.reshape(im.size[1], im.size[0], 4))
    shots.append(np.concatenate(row, 1))
sheet = np.concatenate(shots[::-1], 0)
o2 = bpy.data.images.new("ba", sheet.shape[1], sheet.shape[0], alpha=True)
o2.pixels.foreach_set(sheet.ravel())
o2.filepath_raw = os.path.join(HERE, "kit_fix_before_after.png")
o2.file_format = "PNG"
o2.save()
print("wrote kit_fix_before_after.png (left before, right after)")
