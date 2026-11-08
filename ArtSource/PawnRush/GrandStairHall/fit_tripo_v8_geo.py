# v8 geometry (from v7): the lamps gone and the way cleared, ready for the material bake (bake_tripo_v8.py).
#   1. The 32 wall lamps (picked by their bright bulbs, 승규 님 "모든 램프 지워줘") are deleted: their loose parts
#      in a cylinder round each bulb and the faces standing clear of the wall behind it. Where a lamp grew out of
#      the wall the hole is filled, and the fill and the wall right behind the lamp are marked plain wall (cls 1),
#      so the bake paints them as the wall round them.
#   2. The way is cleared of the model (it has no colliders; the blockout's stay as they were):
#      - the round wall along the curving stair (phi 300..375) moves out radially until nothing stands inside
#        r 31.4 over the stair (its end pier at phi 307..316 stood 6 m into the stair);
#      - the two walls flanking the north opening are pulled back north, their south ends from y 113 to 116.6,
#        clear of the north landing and the last floating stone;
#      - the stage on the royal balcony (3..5 m over the balcony's floor y 16) is cut away above y 16.15, so the
#        glass stones run onto the balcony (the blockout balcony shows there) and on to the gate;
#      - the porch's low side walls move out to |x| 6, the blockout porch's width.
#   3. Normals recomputed (hard above 35 degrees). Saves grand_stair_hall_v8_geo.blend.
# Run: blender -b grand_stair_hall_v7.blend -P fit_tripo_v8_geo.py -- <scratchpad with v7_faces.npz, cands.npy>
import bpy, bmesh, math, os, sys
import numpy as np

HERE = os.path.dirname(bpy.data.filepath)
DATA = sys.argv[sys.argv.index("--") + 1]
LAMP_IDS = [2, 4, 5, 7, 10, 12, 19, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 40, 41, 43, 44, 45, 46, 51, 52, 54, 55, 56, 63, 64]
EXTRA_LAMPS = [(27.6, 86.0, 14.2)]
R, DOWN, UP = 1.0, 3.8, .8

ob = bpy.data.objects["Tripo royal circular hall"]; me = ob.data
me.transform(ob.matrix_world); ob.matrix_world.identity()          # world = local from here on
if "custom_normal" in me.attributes: me.attributes.remove(me.attributes["custom_normal"])

def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def face_arrays():
    nf = len(me.polygons)
    fc = np.zeros(nf * 3); me.polygons.foreach_get("center", fc); fc = fc.reshape(-1, 3)
    fn = np.zeros(nf * 3); me.polygons.foreach_get("normal", fn); fn = fn.reshape(-1, 3)
    fa = np.zeros(nf); me.polygons.foreach_get("area", fa)
    return fc, fn, fa

# ---------------------------------------------------------------- 1. lamps
fc, fn, fa = face_arrays()
nf = len(fc)
lum = np.load(os.path.join(DATA, "v7_faces.npz"))["lum"]
assert len(lum) == nf
# components (lamp parts are mostly loose)
nv = len(me.vertices)
ev = np.zeros(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get("vertices", ev); ev = ev.reshape(-1, 2)
parent = np.arange(nv)
while True:
    pu, pv = parent[ev[:, 0]], parent[ev[:, 1]]
    if (pu == pv).all(): break
    lo, hi = np.minimum(pu, pv), np.maximum(pu, pv)
    np.minimum.at(parent, hi, lo)
    while True:
        pp = parent[parent]
        if (pp == parent).all(): break
        parent = pp
_, comp = np.unique(parent, return_inverse=True)
ls = np.zeros(nf, dtype=np.int64); me.polygons.foreach_get("loop_start", ls)
lv = np.zeros(len(me.loops), dtype=np.int64); me.loops.foreach_get("vertex_index", lv)
fcomp = comp[lv[ls]]
ctotal = np.bincount(fcomp)
cands = np.load(os.path.join(DATA, "cands.npy"))
lamps = [(cands[i, :3], np.array([cands[i, 3], cands[i, 4], 0.0])) for i in LAMP_IDS]
for p in EXTRA_LAMPS:
    p = np.array(p); h = np.hypot(fc[:, 0] - p[0], fc[:, 1] - p[1])
    ring = (fc[:, 2] > p[2] - 3) & (fc[:, 2] < p[2] + .5) & (h > 1) & (h < 2)
    o = (fn[ring] * fa[ring, None]).sum(0); o[2] = 0; o /= max(np.linalg.norm(o), 1e-9)
    lamps.append((p, o))
dead = np.zeros(nf, bool); plain = np.zeros(nf, bool)
for p, o in lamps:
    h = np.hypot(fc[:, 0] - p[0], fc[:, 1] - p[1]); z = fc[:, 2]
    zr = (z > p[2] - DOWN) & (z < p[2] + UP)
    inside = zr & (h < R)
    cin = np.bincount(fcomp[inside], minlength=len(ctotal))
    loose = (cin / np.maximum(ctotal, 1)) > .6
    dd = (fc - p) @ o
    ring = zr & (h > R) & (h < 1.9) & (dd < -.3)
    wall = np.percentile(dd[ring], 85) if ring.sum() > 10 else -.9
    sel = inside & (loose[fcomp] | ((dd > wall + .1) & (dd > -.95)))
    dead |= sel
    plain |= zr & (h < R + .3) & ~sel & (dd > wall - .35)
print("LAMPS", len(lamps), "faces", int(dead.sum()), "plain", int(plain.sum()))

bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table()
cls = bm.faces.layers.int.new("cls")
for i in np.where(plain)[0]: bm.faces[int(i)][cls] = 1
dead_faces = [bm.faces[int(i)] for i in np.where(dead)[0]]
dead_set = set(dead_faces)
border = [e for f in dead_faces for e in f.edges if any(g not in dead_set for g in e.link_faces)]
border = list(set(border))
bmesh.ops.delete(bm, geom=dead_faces, context="FACES_ONLY")
bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context="EDGES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_edges], context="VERTS")
border = [e for e in border if e.is_valid and e.is_boundary]
filled = bmesh.ops.holes_fill(bm, edges=border, sides=0)["faces"]
tri = bmesh.ops.triangulate(bm, faces=filled, quad_method="BEAUTY", ngon_method="BEAUTY")["faces"] if filled else []
for f in tri: f[cls] = 1
print("BORDER EDGES", len(border), "FILLED", len(filled), "->", len(tri), "tris")
# The holes the fill could not close (the lamp grew out of a wall that has no surface behind it): a flat plate in
# the wall's plane right behind each hole, a little deeper than the hole's rim, so the rim covers the plate's edge.
open_verts = {v for e in border if e.is_valid and e.is_boundary for v in e.verts}
ov = np.array([v.co[:] for v in open_verts]) if open_verts else np.zeros((0, 3))
plates = 0
for p, o in lamps:
    if not len(ov): break
    h = np.hypot(ov[:, 0] - p[0], ov[:, 1] - p[1])
    s = (h < R + .6) & (ov[:, 2] > p[2] - DOWN - 1.5) & (ov[:, 2] < p[2] + UP + 1.5)
    if s.sum() < 4: continue
    t = np.array([-o[1], o[0], 0.0])
    q = ov[s] - p
    tu, zu, du = q @ t, q[:, 2], q @ o
    hring = np.hypot(fc[:, 0] - p[0], fc[:, 1] - p[1])
    ring = (fc[:, 2] > p[2] - DOWN) & (fc[:, 2] < p[2] + UP) & (hring > R) & (hring < 1.9) & ((fc - p) @ o < -.3)
    wall = np.percentile((fc[ring] - p) @ o, 85) if ring.sum() > 10 else -.9
    # the plate fits the rim (a flat wall where the lamp was); a larger one deeper behind it catches any sliver
    depth = float(np.clip(np.percentile(du, 10) - .03, wall - .5, wall - .03))
    for (t0, t1, z0, z1), dep in (((tu.min() - .12, tu.max() + .12, zu.min() - .12, zu.max() + .12), depth),
                                  ((min(tu.min(), -R) - .12, max(tu.max(), R) + .12, min(zu.min(), -DOWN) - .12,
                                    max(zu.max(), UP + .7) + .12), min(depth, wall) - .45)):
        corners = [p + t * a + np.array([0, 0, b]) + o * dep for a, b in ((t0, z0), (t1, z0), (t1, z1), (t0, z1))]
        f = bm.faces.new([bm.verts.new(c) for c in corners])
        if f.normal.dot(o) < 0: f.normal_flip()
        f[cls] = 1
    plates += 1
print("PLATES", plates, "behind", len(ov), "open rim verts")

# ---------------------------------------------------------------- 2c. the balcony stage, cut above the floor
bm.faces.ensure_lookup_table()
cut = []
for f in bm.faces:
    c = f.calc_center_median()
    if abs(c.x) < 5.3 and 120.8 < c.y < 128.3 and 16.15 < c.z < 21.6: cut.append(f)
bmesh.ops.delete(bm, geom=cut, context="FACES_ONLY")
bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context="EDGES")
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_edges], context="VERTS")
print("BALCONY STAGE CUT", len(cut))
bm.to_mesh(me); bm.free(); me.update()

# ---------------------------------------------------------------- 2a/2b/2d. moves (vertex fields, shapes kept)
nv = len(me.vertices)
co = np.zeros(nv * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
x, y, z = co.T.copy()
fc, fn, fa = face_arrays()

# 2a. the round wall along the curving stair: its inner radius over the stair, per 1 degree
fphi = np.degrees(np.arctan2(fc[:, 1] - 92, fc[:, 0])) % 360; fphi = np.where(fphi < 200, fphi + 360, fphi)
fr = np.hypot(fc[:, 0], fc[:, 1] - 92)
PH = np.arange(280, 400)
rmin = np.full(len(PH), np.nan)
for k, p in enumerate(PH):
    ys = 8 + 2 * np.clip((p - 297) / 75, 0, 1)
    s = (fphi >= p - .5) & (fphi < p + .5) & (fc[:, 2] > ys + .15) & (fc[:, 2] < ys + 3.4) & (fr > 22) & (fr < 38)
    if s.any(): rmin[k] = fr[s].min()
wallr = np.array([np.percentile(fr[(fphi >= p - 1.5) & (fphi < p + 1.5) & (fr > 22) & (fr < 45)], 10) for p in PH])
need = np.where(np.isnan(rmin), 0, np.clip(31.4 - rmin, 0, 8))
need[(PH < 299) | (PH > 376)] = 0
need = np.array([need[max(0, k - 6):k + 7].max() for k in range(len(PH))])            # widen
need = np.convolve(np.pad(need, 6, mode="edge"), np.ones(13) / 13, mode="valid")        # smooth
need *= smooth(292, 299, PH) * (1 - smooth(376, 386, PH))
rcut = np.fmin(np.where(np.isnan(rmin), 99, rmin), wallr) - .3
rcut = np.convolve(np.pad(rcut, 3, mode="edge"), np.ones(7) / 7, mode="valid")
print("PUSH max %.2f m at phi %d" % (need.max(), PH[need.argmax()]))
vphi = np.degrees(np.arctan2(y - 92, x)) % 360; vphi = np.where(vphi < 200, vphi + 360, vphi)
vr = np.hypot(x, y - 92)
d_v = np.interp(vphi, PH, need, left=0, right=0)
c_v = np.interp(vphi, PH, rcut)
w = smooth(c_v - 1.2, c_v, vr) * (1 - smooth(c_v + 3.5, c_v + 14.5, vr))
nr = vr + d_v * w
moved = (d_v * w) > .01
ux, uy = (x - 0) / np.maximum(vr, 1e-6), (y - 92) / np.maximum(vr, 1e-6)
x = np.where(moved, ux * nr, x); y = np.where(moved, 92 + uy * nr, y)
print("ROUND WALL moved verts", int(moved.sum()))

# 2b. the north walls pulled back: south ends y 113 -> 116.6
ax = np.abs(x)
wx = smooth(3.9, 4.4, ax) * (1 - smooth(9.0, 9.6, ax)) * smooth(1.5, 2.5, z) * (1 - smooth(26.5, 27.5, z))
wy = smooth(110.8, 112.4, y) * (1 - smooth(113.4, 120.0, y))
dy = 3.6 * wx * wy
y = y + dy
print("NORTH WALLS moved verts", int((dy > .01).sum()))

# 2d. the porch's low side walls out to |x| 6
side = (y > -12.6) & (y < 1.2) & (z > -.5) & (z < 6) & (ax > 3.5) & (ax < 7.5)
inner = np.min(ax[side & (z > .3) & (z < 3)]) if (side & (z > .3) & (z < 3)).any() else 9
push = max(0.0, 6.05 - inner)
wp = smooth(3.4, 4.0, ax) * (1 - smooth(7.0, 8.5, ax)) * smooth(-13.2, -12.4, y) * (1 - smooth(.8, 1.8, y)) * (1 - smooth(5.0, 6.5, z))
x = x + np.sign(x) * push * wp
print("PORCH inner |x| %.2f push %.2f" % (inner, push))

me.vertices.foreach_set("co", np.c_[x, y, z].ravel()); me.update()
me.set_sharp_from_angle(angle=math.radians(35))
me.update()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v8_geo.blend"))
print("SAVED faces", len(me.polygons))
