# The tutorial's royal eagle (R839, user 11-25: "시작 초기에 독수리가 날 때 폰이 독수리 위에 올라가서 맵 안에 우스광 스럽게
# 착지 하는 연출 ... 독수리를 정밀 모델링"). Concept: Docs/Player/ConceptArt/R839_TutorialEagle/TU_Eagle_0N_*.png - a carved
# and painted wooden toy eagle: walnut body and wings, ivory head and tail, gold beak, talons, collar and medallion.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_eagle_v1.py -- --render --save --export
#
# Built in its flying pose: head toward -Y, wings along +-X, z up, metres (wingspan about 6.5 m; Unity scales it).
# Every feather is its own carved shape (a domed leaf with a blunt edge), laid in overlapping rows.
# Parts are separate objects with their origins at the joints, so Unity can move them without a rig:
#   TU_Eagle (empty)
#     TU_Eagle_Body                      torso, collar and medallion
#     TU_Eagle_Head                      origin at the neck
#     TU_Eagle_Tail                      origin at the tail's root
#     TU_Eagle_Wing_L / _R               inner wing, origin at the shoulder
#       TU_Eagle_WingTip_L / _R          outer wing, origin at the wrist
#     TU_Eagle_Leg_L / _R                origin at the hip
# Materials: TU_EagleBrown, TU_EagleChest, TU_EagleCream (all three: TU_EagleFeather_BaseColor times a colour),
#            TU_Gold (the map's), TU_EagleEye, TU_EagleDark.
import bpy, os, sys, math
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEX_DIR = os.path.join(HERE, "Materials", "Textures")
RENDERS = os.path.join(HERE, "Renders")
PLAIN = (0.5, 0.62)          # a flat mid tone of the feather texture, for the parts that are not feathers
BROWN, CHEST, CREAM, GOLD, EYE, DARK = range(6)
MAT_NAMES = ["TU_EagleBrown", "TU_EagleChest", "TU_EagleCream", "TU_Gold", "TU_EagleEye", "TU_EagleDark"]
V = Vector
PI = math.pi


# ---------------------------------------------------------------------------------------------- geometry builder

class Geo:
    """Vertices, faces, face-corner uvs, face materials, and for each vertex the bones that carry it ({name: weight},
    whatever self.bone is when it is added: a whole feather moves as one piece, shared between two bones near a joint)."""
    def __init__(self):
        self.v, self.f, self.uv, self.m, self.w = [], [], [], [], []
        self.bone = {"Body": 1.0}

    def add(self, prim, mat, M=None):
        verts, faces, vuv = prim
        o = len(self.v)
        for p in verts:
            p = V(p)
            self.v.append(M @ p if M is not None else p)
            self.w.append(self.bone)
        for f in faces:
            self.f.append(tuple(o + i for i in f))
            self.uv.append([vuv[i] for i in f])
            self.m.append(mat)

    def mirrored(self):
        def other(name):
            return name[:-2] + "_R" if name.endswith("_L") else name
        g = Geo()
        g.v = [V((-p.x, p.y, p.z)) for p in self.v]
        g.f = [tuple(reversed(f)) for f in self.f]
        g.uv = [list(reversed(u)) for u in self.uv]
        g.m = list(self.m)
        g.w = [{other(k): x for k, x in w.items()} for w in self.w]
        return g

    def merge(self, other, at=None):
        o = len(self.v)
        self.v += [p + at for p in other.v] if at is not None else list(other.v)
        self.f += [tuple(o + i for i in f) for f in other.f]
        self.uv += other.uv
        self.m += other.m
        self.w += other.w

    def obj(self, name, mats, coll):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(p) for p in self.v], [], self.f)
        uv = me.uv_layers.new(name="UVMap")
        k = 0
        for corners in self.uv:
            for c in corners:
                uv.data[k].uv = c
                k += 1
        for m in mats: me.materials.append(m)
        me.polygons.foreach_set("material_index", self.m)
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
        me.update()
        o = bpy.data.objects.new(name, me)
        coll.objects.link(o)
        batches = {}
        for i, w in enumerate(self.w):
            for bone, x in w.items():
                if x > 1e-4: batches.setdefault((bone, round(x, 4)), []).append(i)
        groups = {}
        for (bone, x), idx in batches.items():
            if bone not in groups: groups[bone] = o.vertex_groups.new(name=bone)
            groups[bone].add(idx, x, 'REPLACE')
        return o

    def tris(self):
        return sum(len(f) - 2 for f in self.f)


def place(root, d, n):
    """A frame at root: local +Y along d, local +Z along n (made square to d)."""
    d = d.normalized()
    x = d.cross(n).normalized()
    z = x.cross(d)
    M = Matrix(((x.x, d.x, z.x, root.x), (x.y, d.y, z.y, root.y), (x.z, d.z, z.z, root.z), (0, 0, 0, 1)))
    return M


def basis(x, y, z, o):
    return Matrix(((x.x, y.x, z.x, o.x), (x.y, y.y, z.y, o.y), (x.z, y.z, z.z, o.z), (0, 0, 0, 1)))


def feather(L, W, T, nu=4, nv=6, closed=True, finger=0.0, curl=0.0, edge=0.3, point=0.0, notch=0):
    """One carved feather lying along +Y from its root at the origin, its top toward +Z: a leaf outline with a round
    tip (point 1: a pointed lock), a domed top with a raised shaft, a blunt edge (edge * T), flat underneath (left
    open on the body). notch: that many V splits cut into its right edge, as a flight feather's vane parts."""
    def wprof(v):
        if v < 0.25: w = 0.55 + 0.45 * math.sin(PI / 2 * v / 0.25)
        elif v < 0.6: w = 1.0
        else: w = math.sqrt(max(0.0, 1.0 - ((v - 0.6) / 0.42) ** 2))
        if point > 0 and v > 0.4:
            w = w * (1 - point) + point * min(w, max(0.0, 1.0 - ((v - 0.4) / 0.6) ** 1.6))
        return w * (1.0 - finger * 0.35 * v)

    splits = [round(nv * f) for f in ((0.62,), (0.5, 0.74), (0.42, 0.62, 0.8))[notch - 1]] if notch else []
    verts, vuv, faces = [], [], []
    for j in range(nv + 1):
        v = j / nv
        w = W * wprof(v)
        vp = math.sin(PI * min(1.0, v * 0.9 + 0.1)) ** 0.5 if v < 1 else 0.3
        for i in range(nu + 1):
            u = i / nu
            dome = 1.0 - (2 * u - 1) ** 2
            ridge = 0.24 * max(0.0, 1.0 - abs(2 * u - 1) * 4.0)
            z = T * (edge + (1 - edge) * (dome + ridge) * vp) - curl * L * v * v
            x = (u - 0.5) * w
            if j in splits and i == nu: x *= 0.62          # the split's bottom
            verts.append((x, v * L, z))
            vuv.append((0.04 + 0.92 * u, 0.03 + 0.94 * v))
    top = lambda i, j: j * (nu + 1) + i
    for j in range(nv):
        for i in range(nu):
            faces.append((top(i, j), top(i + 1, j), top(i + 1, j + 1), top(i, j + 1)))
    # the outline, counter-clockwise seen from above
    rim = [(i, 0) for i in range(nu)] + [(nu, j) for j in range(nv)] + [(i, nv) for i in range(nu, 0, -1)] + [(0, j) for j in range(nv, 0, -1)]
    under = {}
    if closed:
        for j in range(nv + 1):
            for i in range(nu + 1):
                x, y, _ = verts[top(i, j)]
                under[(i, j)] = len(verts)
                verts.append((x, y, -curl * L * (j / nv) ** 2))
                vuv.append(vuv[top(i, j)])
        for j in range(nv):
            for i in range(nu):
                faces.append((under[(i, j)], under[(i, j + 1)], under[(i + 1, j + 1)], under[(i + 1, j)]))
    else:
        for (i, j) in rim:
            x, y, _ = verts[top(i, j)]
            under[(i, j)] = len(verts)
            verts.append((x, y, -curl * L * (j / nv) ** 2 - 0.3 * T))
            vuv.append(vuv[top(i, j)])
    for k in range(len(rim)):
        p, q = rim[k], rim[(k + 1) % len(rim)]
        faces.append((under[p], under[q], top(*q), top(*p)))
    return verts, faces, vuv


def ellipsoid(nu=20, nv=12, uv=PLAIN):
    """A unit sphere (scale it with a matrix)."""
    verts, faces = [(0, 0, 1)], []
    for j in range(1, nv):
        ph = PI * j / nv
        for i in range(nu):
            th = 2 * PI * i / nu
            verts.append((math.sin(ph) * math.cos(th), math.sin(ph) * math.sin(th), math.cos(ph)))
    verts.append((0, 0, -1))
    last = len(verts) - 1
    at = lambda i, j: 1 + (j - 1) * nu + (i % nu)
    for i in range(nu):
        faces.append((0, at(i, 1), at(i + 1, 1)))
        faces.append((last, at(i + 1, nv - 1), at(i, nv - 1)))
    for j in range(1, nv - 1):
        for i in range(nu):
            faces.append((at(i, j), at(i, j + 1), at(i + 1, j + 1), at(i + 1, j)))
    return verts, faces, [uv] * len(verts)


def blob(center, radii, x=None, y=None):
    """The matrix of an ellipsoid at center with half sizes radii along x, y and x cross y (world axes by default)."""
    x = (x or V((1, 0, 0))).normalized()
    y = (y or V((0, 1, 0)))
    z = x.cross(y).normalized()
    y = z.cross(x)
    return basis(x * radii[0], y * radii[1], z * radii[2], V(center))


def sweep(points, radii, up, seg=12, caps=True, uv=PLAIN, side=None):
    """A tube through points with an elliptical section: radii[i] = (across, along up)."""
    verts, faces = [], []
    n = len(points)
    for k, p in enumerate(points):
        p = V(p)
        t = (V(points[min(n - 1, k + 1)]) - V(points[max(0, k - 1)])).normalized()
        sd = side.normalized() if side is not None else up.cross(t).normalized()
        upv = t.cross(sd).normalized()
        ra, rb = radii[k]
        for i in range(seg):
            a = 2 * PI * i / seg
            verts.append(tuple(p + sd * (ra * math.cos(a)) + upv * (rb * math.sin(a))))
    for k in range(n - 1):
        for i in range(seg):
            a, b = k * seg + i, k * seg + (i + 1) % seg
            faces.append((a, b, b + seg, a + seg))
    if caps:
        verts.append(tuple(V(points[0]))); c0 = len(verts) - 1
        verts.append(tuple(V(points[-1]))); c1 = len(verts) - 1
        for i in range(seg):
            faces.append((c0, (i + 1) % seg, i))
            faces.append((c1, (n - 1) * seg + i, (n - 1) * seg + (i + 1) % seg))
    return verts, faces, [uv] * len(verts)


def torus(R, r, nu=40, nv=10):
    """A ring in the local XY plane."""
    verts, faces = [], []
    for i in range(nu):
        a = 2 * PI * i / nu
        for j in range(nv):
            b = 2 * PI * j / nv
            verts.append(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
    for i in range(nu):
        for j in range(nv):
            a, b = i * nv + j, i * nv + (j + 1) % nv
            c, d = ((i + 1) % nu) * nv + (j + 1) % nv, ((i + 1) % nu) * nv + j
            faces.append((a, d, c, b))
    return verts, faces, [PLAIN] * len(verts)


def bezier(p0, p1, p2, n):
    p0, p1, p2 = V(p0), V(p1), V(p2)
    return [(1 - t) ** 2 * p0 + 2 * t * (1 - t) * p1 + t * t * p2 for t in (k / n for k in range(n + 1))]


def ring(g, center, axis, rx, rz, n, L, W, T, mat, flare=0.0, lift=0.12, phase=0.0, arc=None, up=None, sink=0.012, **fk):
    """A row of feathers rooted on an ellipse round axis (rx to the side, rz along up), pointing along the axis and
    turned outward by flare, their tips lifted off the surface by lift (so each row lies on the next)."""
    a = axis.normalized()
    up = up or V((0, 0, 1))
    s = a.cross(up).normalized()
    u = s.cross(a).normalized()
    fm = feather(L, W, T, **fk)
    for k in range(n):
        th = 2 * PI * (k + phase) / n if arc is None else arc[0] + (arc[1] - arc[0]) * (k + 0.5) / n
        c, sn = math.cos(th), math.sin(th)
        pos = V(center) + s * (rx * c) + u * (rz * sn)
        rn = (s * (c * rz) + u * (sn * rx)).normalized()
        d = a * math.cos(flare) + rn * math.sin(flare)
        nn = rn * math.cos(flare) - a * math.sin(flare)
        d2 = d * math.cos(lift) + nn * math.sin(lift)
        n2 = nn * math.cos(lift) - d * math.sin(lift)
        g.add(fm, mat, place(pos - d * (0.12 * L) - nn * sink, d2, n2))


# ---------------------------------------------------------------------------------------------- the parts

AX, AY, AZ = 0.44, 0.82, 0.46          # the torso's half sizes
HEAD = V((0.0, -0.84, 0.42))
FWD = V((0.0, -1.0, -0.16)).normalized()
SHOULDER = V((0.30, -0.14, 0.24))
WRIST = V((0.92, -0.18, 0.0))           # from the shoulder
HIP = V((0.21, 0.20, -0.26))
TAIL = V((0.0, 0.70, -0.02))


def body_row(phi):
    k = 1.0 + 0.16 * math.cos(phi)
    return AX * math.sin(phi) * k, AZ * math.sin(phi) * k, -AY * math.cos(phi)


def part_body():
    g = Geo()
    # the torso under the feathers: the same egg, a little smaller
    verts, faces, vuv = [], [], []
    nu, nv = 28, 18
    for j in range(nv + 1):
        phi = PI * j / nv
        rx, rz, y = body_row(phi)
        for i in range(nu):
            th = 2 * PI * i / nu
            verts.append((0.96 * rx * math.cos(th), y * 0.985, 0.96 * rz * math.sin(th)))
            vuv.append(PLAIN)
    for j in range(nv):
        for i in range(nu):
            a, b = j * nu + i, j * nu + (i + 1) % nu
            faces.append((a, a + nu, b + nu, b))
    g.add((verts, faces, vuv), BROWN)
    # feather rows from the chest back to the rump
    rows = 10
    for r in range(rows):
        phi = 0.86 + r * 0.195
        rx, rz, y = body_row(phi)
        rx2, rz2, y2 = body_row(phi + 0.01)
        flare = math.atan2(((rx2 + rz2) - (rx + rz)) * 0.5, y2 - y)
        size = max(0.6, math.sin(phi) ** 0.6)
        W, L = 0.26 * size, 0.36 * size
        n = max(7, round(2 * PI * (rx + rz) * 0.5 / (W * 0.74)))
        ring(g, (0, y, 0), V((0, 1, 0)), rx, rz, n, L, W, 0.05 * size, CHEST if phi < 1.3 else BROWN,
             flare=flare, lift=0.13, phase=0.5 * (r % 2), closed=False, curl=0.05)
    # the gold collar, tilted so its front hangs lower, and the plain medallion under the chest
    cc = V((0, -0.33, 0.0))
    cx, cy, cz = V((1, 0, 0)), V((0, 0, 1)), V((0, -1, 0))
    g.add(torus(1.0, 0.052, 48, 10), GOLD, basis(cx * 0.487, cy * 0.507, cz * 0.5, cc))
    low = cc - cy * 0.507
    g.add(torus(0.04, 0.011, 16, 8), GOLD, basis(V((0, 1, 0)), V((0, 0, 1)), V((1, 0, 0)), low + V((0, 0, -0.05))))
    disc = sweep([low + V((0, -0.016, -0.18)), low + V((0, 0.016, -0.18))], [(0.095, 0.095)] * 2, V((0, 0, 1)), seg=28, side=V((1, 0, 0)))
    g.add(disc, GOLD)
    g.add(torus(0.082, 0.014, 28, 8), GOLD, basis(V((1, 0, 0)), V((0, 0, 1)), V((0, -1, 0)), low + V((0, 0, -0.18))))
    return g


def part_head():
    """Built in body coordinates and moved to the neck by the caller. The face follows the concept sheet: a deep
    hooked gold bill with a cere and nostrils, big amber eyes with a dark rim under a heavy brow lock, and the head
    feathers as large pointed locks swept back in layers, a crest at the back of the crown, a ragged ruff."""
    g = Geo()
    g.bone = {"Head": 1.0}
    up, side = V((0, 0, 1)), V((1, 0, 0))
    g.add(ellipsoid(32, 20), CREAM, blob(HEAD, (0.25, 0.30, 0.26)))
    g.bone = {"Neck1": 0.5, "Neck2": 0.5}
    g.add(sweep([HEAD + V((0, 0.05, -0.08)), V((0, -0.52, 0.16))], [(0.21, 0.21), (0.3, 0.3)], up, seg=20, caps=False), CREAM)
    g.bone = {"Head": 1.0}
    R = 0.262
    back = -FWD
    lock = dict(closed=True, point=1.0, nu=4, nv=8)
    # the crown and cheeks: pointed locks, each row longer and lifted further than the one before
    for r, (phi, L, W, lift, n) in enumerate(((0.95, 0.24, 0.17, 0.2, 9), (1.3, 0.3, 0.2, 0.24, 9), (1.68, 0.36, 0.22, 0.28, 9),
                                              (2.06, 0.4, 0.23, 0.3, 8), (2.44, 0.42, 0.23, 0.3, 7))):
        ring(g, HEAD + FWD * (R * math.cos(phi)), back, R * math.sin(phi) * 0.97, R * math.sin(phi), n, L, W, 0.06, CREAM,
             flare=PI / 2 - phi, lift=lift, phase=0.5 * (r % 2), curl=0.1, **lock)
    # the ruff over the chest: two rows of long locks
    down = V((0, 0.82, -0.57)).normalized()
    g.bone = {"Neck2": 1.0}
    ring(g, V((0, -0.70, 0.30)), down, 0.28, 0.28, 10, 0.4, 0.23, 0.06, CREAM, flare=0.45, lift=0.16, curl=0.09, **lock)
    g.bone = {"Neck1": 1.0}
    ring(g, V((0, -0.62, 0.22)), down, 0.36, 0.36, 11, 0.42, 0.25, 0.06, CREAM, flare=0.6, lift=0.14, phase=0.5, curl=0.09, **lock)
    g.bone = {"Head": 1.0}
    # the crest: five spiky locks standing off the back of the crown
    for sx, sy, lift, L in ((0.0, 0.0, 0.62, 0.36), (-0.1, 0.03, 0.5, 0.32), (0.1, 0.03, 0.5, 0.32), (-0.18, 0.08, 0.38, 0.28), (0.18, 0.08, 0.38, 0.28)):
        root = HEAD + V((sx, 0.0 + sy, 0.2 - abs(sx) * 0.25))
        d = (V((sx * 1.2, 0.85, 0.0)).normalized() * math.cos(lift) + up * math.sin(lift)).normalized()
        g.add(feather(L, 0.15, 0.06, curl=-0.12, **lock), CREAM, place(root, d, V((sx, -0.5, 1)).normalized()))

    # the bill: a deep hooked upper bill, the cere over its base, a shorter lower bill, dark nostrils
    path = bezier(HEAD + V((0, -0.17, 0.05)), HEAD + V((0, -0.57, 0.13)), HEAD + V((0, -0.5, -0.27)), 16)
    radii = [(0.122 * (1 - t) ** 0.7 + 0.004, 0.158 * (1 - t) ** 0.62 + 0.004) for t in (k / 16 for k in range(17))]
    g.add(sweep(path, radii, up, seg=22, side=side), GOLD)
    g.add(ellipsoid(20, 12), GOLD, blob(HEAD + V((0, -0.245, 0.11)), (0.118, 0.1, 0.085)))        # the cere
    g.add(ellipsoid(20, 12), GOLD, blob(HEAD + V((0, -0.3, -0.125)), (0.092, 0.15, 0.058)))       # the lower bill
    g.add(ellipsoid(14, 8), DARK, blob(HEAD + V((0, -0.27, -0.078)), (0.07, 0.1, 0.007)))        # the mouth's line
    for sx in (-1, 1):
        g.add(ellipsoid(10, 6), DARK, blob(HEAD + V((sx * 0.082, -0.31, 0.135)), (0.016, 0.034, 0.016), V((sx * 0.3, -1, 0.2)).cross(up)))
        # the eye: a big amber iris, a dark pupil and rim, a white glint
        e = (FWD * 0.74 + side * (sx * 0.64) + up * 0.18).normalized()
        ex = e.cross(up).normalized()
        g.add(ellipsoid(20, 12), EYE, blob(HEAD + e * 0.205, (0.098, 0.098, 0.098)))
        g.add(ellipsoid(14, 10), DARK, blob(HEAD + e * 0.268, (0.044, 0.044, 0.044)))
        g.add(torus(0.092, 0.013, 28, 8), DARK, basis(ex, e.cross(ex), e, HEAD + e * 0.24))
        g.add(ellipsoid(8, 6, uv=(0.5, 0.95)), CREAM, blob(HEAD + e * 0.305 + up * 0.024 + FWD * 0.02, (0.015, 0.015, 0.015)))
        # the brow: a thick lock from above the cere back over the eye, low at the front - the stern look
        root = HEAD + e * 0.25 + up * 0.105 + FWD * 0.13
        d = (back * 0.9 + side * (sx * 0.3) + up * 0.16).normalized()
        g.add(feather(0.34, 0.105, 0.075, closed=True, point=1.0, nu=4, nv=8, curl=0.03), CREAM, place(root, d, (e + up * 0.15).normalized()))
        # two cheek locks under the eye, swept back and down
        for k, (dz, L) in enumerate(((-0.1, 0.3), (-0.18, 0.26))):
            root = HEAD + e * 0.2 + up * dz + FWD * (0.03 - 0.04 * k)
            d = (back * 0.8 + side * (sx * 0.35) - up * (0.35 + 0.2 * k)).normalized()
            g.add(feather(L, 0.15, 0.055, closed=True, point=1.0, nu=4, nv=8, curl=0.08), CREAM, place(root, d, (side * sx + up * 0.1 - back * 0.3).normalized()))
    return g


def part_tail():
    """Local origin at the tail's root."""
    g = Geo()
    g.bone = {"Tail": 1.0}
    n = 7
    for i in range(n):
        a = math.radians(-44 + 88 * i / (n - 1))
        mid = 1.0 - abs(i - (n - 1) / 2) / ((n - 1) / 2)
        d = V((math.sin(a), math.cos(a), -0.06)).normalized()
        root = V((math.sin(a) * 0.05, -0.1, -0.02 + 0.014 * mid * 3))
        g.add(feather(0.84 + 0.12 * mid, 0.3, 0.05, nu=4, nv=12, closed=True, curl=0.03, notch=1 + i % 2), CREAM, place(root, d, V((0, 0.06, 1))))
    # two rows of brown coverts over the roots
    ring(g, V((0, -0.2, 0.0)), V((0, 1, 0)), 0.2, 0.12, 9, 0.3, 0.17, 0.04, BROWN, flare=0.05, lift=0.1, closed=True)
    return g


def part_leg():
    """The left leg, local origin at the hip: the feathered thigh, the gold shank, the foot."""
    g = Geo()
    g.bone = {"Thigh_L": 1.0}
    down = V((0, 0, -1))
    g.add(ellipsoid(18, 12), BROWN, blob((0, 0, -0.12), (0.15, 0.17, 0.22)))
    ring(g, V((0, 0, -0.02)), down, 0.15, 0.17, 9, 0.27, 0.15, 0.04, BROWN, flare=0.08, lift=0.1, up=V((0, 1, 0)), closed=True)
    ring(g, V((0, 0, -0.17)), down, 0.12, 0.13, 8, 0.22, 0.13, 0.035, BROWN, flare=-0.05, lift=0.1, phase=0.5, up=V((0, 1, 0)), closed=True)
    g.bone = {"Shank_L": 1.0}
    g.add(sweep([(0, -0.01, -0.3), (0, -0.04, -0.4), (0, -0.07, -0.5)], [(0.055, 0.055), (0.048, 0.048), (0.055, 0.055)], V((0, 1, 0)), seg=14), GOLD)
    for k in range(3):   # the carved rings of the shank
        g.add(torus(0.052, 0.012, 16, 6), GOLD, blob((0, -0.02 - 0.02 * k, -0.34 - 0.055 * k), (1, 1, 1)))
    g.bone = {"Foot_L": 1.0}
    hub = V((0, -0.075, -0.53))
    g.add(ellipsoid(16, 10), GOLD, blob(hub, (0.075, 0.08, 0.06)))
    toes = [(-30, 3), (0, 3), (30, 3), (180, 2)]
    for ang, beads in toes:
        a = math.radians(ang)
        d = V((math.sin(a), -math.cos(a), 0))
        reach = 0.0
        for b in range(beads):
            r = 0.05 - 0.005 * b
            c = hub + d * (0.085 + 0.088 * b) + V((0, 0, -0.012 * b * b))
            g.add(ellipsoid(10, 6), GOLD, blob(c, (0.056, r, r * 0.95), d, V((0, 0, 1))))
            reach = 0.085 + 0.088 * b
        p0 = hub + d * (reach + 0.035) + V((0, 0, -0.012 * (beads - 1) ** 2 + 0.005))
        claw = bezier(p0, p0 + d * 0.13 + V((0, 0, 0.02)), p0 + d * 0.11 + V((0, 0, -0.17)), 8)
        g.add(sweep(claw, [(0.036 * (1 - t) ** 0.8 + 0.003,) * 2 for t in (k / 8 for k in range(9))], V((0, 0, 1)), seg=10, side=d.cross(V((0, 0, 1)))), GOLD)
    return g


ELBOW = 0.42          # the elbow's place along the inner wing (shoulder 0, wrist 1)


def inner_bones(u):
    """The bones of a feather rooted at u along the inner wing: the upper arm near the body, the forearm beyond the
    elbow, shared across the elbow and a little with the hand at the wrist - so the feathers fan as the wing folds."""
    t = min(1.0, max(0.0, (u - 0.2) / 0.42))
    fore = t * t * (3 - 2 * t)
    hand = min(0.5, max(0.0, (u - 0.8) / 0.4))
    return {"Humerus_L": 1.0 - fore, "Forearm_L": fore * (1.0 - hand), "Hand_L": fore * hand}


def outer_bones(u):
    """A feather rooted at u along the hand: the hand, shared with the forearm next to the wrist."""
    fore = 0.5 * min(1.0, max(0.0, 1.0 - u / 0.32))
    return {"Hand_L": 1.0 - fore, "Forearm_L": fore}


WB = inner_bones


def wing_feather(g, root, ang, L, W, mat, z=0.0, T=0.046, finger=0.0, flip=False, notch=0):
    d = V((math.sin(ang), math.cos(ang), 0.0))
    n = V((0, 0, -1 if flip else 1))
    g.add(feather(L, W, T, nu=4, nv=12 if notch else 7, closed=True, finger=finger, curl=0.02, notch=notch), mat, place(V(root) + V((0, 0, z)), d, n))


def part_wing_inner():
    """The left wing from the shoulder to the wrist, local origin at the shoulder; +X outward, +Y to the trailing edge."""
    g = Geo()
    W0, W1 = V((0, 0, 0)), WRIST
    global WB
    WB = inner_bones
    E = W0 + (W1 - W0) * ELBOW
    g.bone = {"Humerus_L": 1.0}
    g.add(sweep([W0, (W0 + E) * 0.5, E], [(0.2, 0.075), (0.19, 0.072), (0.18, 0.07)], V((0, 0, 1)), seg=14), BROWN)
    g.add(ellipsoid(16, 10), BROWN, blob(W0 + V((0.02, 0.02, 0)), (0.24, 0.22, 0.14)))
    g.bone = {"Humerus_L": 0.5, "Forearm_L": 0.5}
    g.add(ellipsoid(14, 8), BROWN, blob(E, (0.19, 0.19, 0.075)))
    g.bone = {"Forearm_L": 1.0}
    g.add(sweep([E, (E + W1) * 0.5, W1], [(0.18, 0.07), (0.16, 0.065), (0.14, 0.06)], V((0, 0, 1)), seg=14), BROWN)
    n = 7
    for i in range(n):
        u = (i + 0.5) / n
        p = W0 + (W1 - W0) * u + V((0, 0.07, 0))
        ang = math.radians(1 + 13 * u)
        g.bone = WB(u); wing_feather(g, p, ang, 0.74 + 0.08 * u, 0.26, BROWN, notch=1 + i % 2, z=-0.02 + 0.004 * (i % 2))
    for i in range(n + 1):   # greater coverts
        u = i / n
        p = W0 + (W1 - W0) * u + V((0, 0.03, 0))
        g.bone = WB(u); wing_feather(g, p, math.radians(1 + 12 * u), 0.48, 0.24, BROWN, z=0.03 + 0.004 * (i % 2))
    for row, (off, L, cnt, z, mat) in enumerate(((0.0, 0.34, 8, 0.065, CHEST), (-0.08, 0.28, 9, 0.095, CHEST), (-0.17, 0.24, 9, 0.1, BROWN), (-0.25, 0.2, 9, 0.07, BROWN))):
        for i in range(cnt):
            u = (i + 0.5 * (row % 2)) / cnt
            if u > 1: continue
            p = W0 + (W1 - W0) * u + V((0, off, 0))
            g.bone = WB(u); wing_feather(g, p, math.radians(2 + 10 * u), L, 0.21, mat, z=z, T=0.04)
    # under the wing: one row of coverts over the roots
    for i in range(n):
        u = (i + 0.5) / n
        p = W0 + (W1 - W0) * u + V((0, -0.06, 0))
        g.bone = WB(u); wing_feather(g, p, math.radians(2 + 10 * u), 0.46, 0.26, CHEST, z=-0.07, flip=True)
    return g


def part_wing_outer():
    """The left wing from the wrist out, local origin at the wrist."""
    g = Geo()
    H1 = V((0.72, 0.14, 0))
    hand = [H1 * t for t in (0, 0.5, 1)]
    global WB
    WB = outer_bones
    g.bone = {"Forearm_L": 0.5, "Hand_L": 0.5}
    g.add(ellipsoid(14, 8), BROWN, blob((0, 0, 0), (0.15, 0.15, 0.065)))
    g.bone = {"Hand_L": 1.0}
    g.add(sweep(hand, [(0.14, 0.06), (0.11, 0.05), (0.07, 0.035)], V((0, 0, 1)), seg=12), BROWN)
    n = 8
    for i in range(n):
        u = (i + 0.5) / n
        p = H1 * u + V((0, 0.06, 0))
        ang = math.radians(16 + 66 * u ** 1.15)
        L = 0.8 + 0.2 * math.sin(PI * min(1.0, u * 1.15)) - 0.1 * u
        g.bone = WB(u); wing_feather(g, p, ang, L, 0.24 - 0.02 * u, BROWN, z=-0.02 + 0.004 * (i % 2) - 0.012 * u, finger=0.25 + 0.3 * u, notch=1 + (i + 1) % 3)
    for i in range(n):   # greater coverts
        u = (i + 0.2) / n
        p = H1 * u + V((0, 0.02, 0))
        g.bone = WB(u); wing_feather(g, p, math.radians(14 + 56 * u), 0.5 - 0.08 * u, 0.22, BROWN, z=0.03 + 0.004 * (i % 2))
    for row, (off, L, cnt, z, mat) in enumerate(((-0.01, 0.32, 7, 0.06, CHEST), (-0.08, 0.26, 7, 0.085, CHEST), (-0.16, 0.2, 7, 0.07, BROWN))):
        for i in range(cnt):
            u = (i + 0.5 * (row % 2)) / cnt
            p = H1 * u + V((0, off, 0))
            g.bone = WB(u); wing_feather(g, p, math.radians(12 + 50 * u), L - 0.06 * u, 0.19, mat, z=z, T=0.038)
    for i in range(8):
        u = (i + 0.5) / 8
        p = H1 * u + V((0, -0.04, 0))
        g.bone = WB(u); wing_feather(g, p, math.radians(14 + 50 * u), 0.42, 0.22, CHEST, z=-0.065, flip=True)
    return g


# ---------------------------------------------------------------------------------------------- texture and materials

def feather_texture():
    """One feather (the materials multiply their colour over it): u across, v root to tip. Base colour: darker under
    the row above (the root), barbs running out from the shaft, a pale shaft, a lighter worn rim. Normal map: the
    shaft as a ridge and the barbs as fine grooves, so each feather reads as a feather and not as a smooth scale."""
    os.makedirs(TEX_DIR, exist_ok=True)
    w, h = 512, 1024
    rng = np.random.default_rng(7)
    u = np.linspace(0, 1, w)[None, :] * np.ones((h, 1))
    v = np.linspace(0, 1, h)[:, None] * np.ones((1, w))
    side = np.abs(u - 0.5)
    barbs = np.sin((side * 58 - v * 34) * 2 * np.pi)                           # chevrons pointing to the tip
    fine = np.sin((side * 131 - v * 77) * 2 * np.pi + rng.normal(0, 0.4, (h, 1)))
    shaft = np.exp(-((u - 0.5) / 0.014) ** 2)
    wear = rng.normal(0, 1, (h // 8, w // 8)).repeat(8, axis=0).repeat(8, axis=1)
    val = 0.64 + 0.36 * np.clip(v / 0.45, 0, 1) ** 0.8                          # the root sits in the shade
    val = val + 0.035 * barbs + 0.015 * fine + 0.1 * shaft + 0.012 * wear
    val = val + 0.1 * np.clip((side - 0.4) / 0.1, 0, 1) + 0.08 * np.clip((v - 0.86) / 0.14, 0, 1)
    val = val - 0.06 * np.exp(-((side - 0.03) / 0.02) ** 2)                     # the groove beside the shaft
    val = np.clip(val, 0, 1)
    rgb = np.stack([val, val * 0.985, val * 0.96, np.ones_like(val)], axis=-1)
    img = bpy.data.images.new("TU_EagleFeather_BaseColor", w, h, alpha=False)
    img.pixels.foreach_set(rgb.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX_DIR, "TU_EagleFeather_BaseColor.png")
    img.file_format = 'PNG'
    img.save()
    # height: the shaft ridge and the barbs, fading out at the rim; its slope as a tangent space normal
    height = 1.0 * shaft + 0.16 * barbs * (1 - shaft) + 0.05 * fine
    height = height * np.clip((0.5 - side) / 0.06, 0, 1)
    du = np.gradient(height, axis=1) * w / 512.0
    dv = np.gradient(height, axis=0) * h / 1024.0
    k = 3.0
    nx, ny, nz = -du * k, -dv * k, np.ones_like(height)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    nrm = np.stack([nx / ln * 0.5 + 0.5, ny / ln * 0.5 + 0.5, nz / ln * 0.5 + 0.5, np.ones_like(height)], axis=-1)
    nimg = bpy.data.images.new("TU_EagleFeather_Normal", w, h, alpha=False, is_data=True)
    nimg.pixels.foreach_set(nrm.astype(np.float32).ravel())
    nimg.filepath_raw = os.path.join(TEX_DIR, "TU_EagleFeather_Normal.png")
    nimg.file_format = 'PNG'
    nimg.save()
    return img, nimg


def srgb(hexa):
    c = [int(hexa[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]


def material(name, hexa, tex=None, metallic=0.0, rough=0.5, normal=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    col = srgb(hexa)
    m.diffuse_color = col
    if tex is None:
        bsdf.inputs["Base Color"].default_value = col
        return m
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = tex
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    mix.inputs[0].default_value = 1.0
    ins = [s for s in mix.inputs if s.type == 'RGBA']
    out = next(s for s in mix.outputs if s.type == 'RGBA')
    nt.links.new(t.outputs["Color"], ins[0])
    ins[1].default_value = col
    nt.links.new(out, bsdf.inputs["Base Color"])
    if normal is not None:
        nimg = nt.nodes.new("ShaderNodeTexImage")
        nimg.image = normal
        nimg.image.colorspace_settings.name = 'Non-Color'
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nmap.inputs["Strength"].default_value = 1.0
        nt.links.new(nimg.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def materials():
    tex, nrm = feather_texture()
    return [material("TU_EagleBrown", "#8A3A1C", tex, rough=0.42, normal=nrm),
            material("TU_EagleChest", "#B05A2C", tex, rough=0.42, normal=nrm),
            material("TU_EagleCream", "#F4E2B8", tex, rough=0.45, normal=nrm),
            material("TU_Gold", "#F2B21E", None, metallic=0.9, rough=0.24),
            material("TU_EagleEye", "#C7780F", None, rough=0.15),
            material("TU_EagleDark", "#24120A", None, rough=0.2)]


# ---------------------------------------------------------------------------------------------- assembly

# The skeleton (user 11-25: "독수리 뼈대 넣어서 더 사실적으로"): name, where its joint is, parent. Only the joints matter:
# Unity turns the bones about the model's own axes from this rest pose (TutorialIntro), not about the bones' own.
def bones():
    out = [("Body", V((0, 0.1, 0)), None),
           ("Neck1", V((0, -0.50, 0.13)), "Body"), ("Neck2", V((0, -0.63, 0.25)), "Neck1"), ("Head", V((0, -0.74, 0.35)), "Neck2"),
           ("Tail", V(TAIL), "Body")]
    for sx, tag in ((1, "L"), (-1, "R")):
        m = lambda v: V((sx * v.x, v.y, v.z))
        out += [("Humerus_" + tag, m(SHOULDER), "Body"),
                ("Forearm_" + tag, m(SHOULDER + WRIST * ELBOW), "Humerus_" + tag),
                ("Hand_" + tag, m(SHOULDER + WRIST), "Forearm_" + tag),
                ("Thigh_" + tag, m(HIP), "Body"),
                ("Shank_" + tag, m(HIP + V((0, 0, -0.28))), "Thigh_" + tag),
                ("Foot_" + tag, m(HIP + V((0, -0.06, -0.5))), "Shank_" + tag)]
    return out


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = bpy.data.collections.new("Eagle")
    bpy.context.scene.collection.children.link(coll)
    mats = materials()
    report = {}

    # one mesh, in the body's coordinates
    g = Geo()
    def take(name, part, at=None):
        report[name] = part.tris()
        g.merge(part, at)
    take("body", part_body())
    take("head", part_head())
    take("tail", part_tail(), V(TAIL))
    for sx, tag in ((1, "L"), (-1, "R")):
        inner, outer, leg = part_wing_inner(), part_wing_outer(), part_leg()
        if sx < 0: inner, outer, leg = inner.mirrored(), outer.mirrored(), leg.mirrored()
        m = lambda v: V((sx * v.x, v.y, v.z))
        take("wing_" + tag, inner, m(SHOULDER))
        take("wingtip_" + tag, outer, m(SHOULDER + WRIST))
        take("leg_" + tag, leg, m(HIP))

    arm = bpy.data.armatures.new("TU_Eagle_Rig")
    rig = bpy.data.objects.new("TU_Eagle_Rig", arm)
    coll.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name, joint, parent in bones():
        eb = arm.edit_bones.new(name)
        eb.head = joint
        eb.tail = joint + V((0, 0.12, 0))
        if parent: eb.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')

    mesh = g.obj("TU_Eagle_Mesh", mats, coll)
    mesh.parent = rig
    mod = mesh.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig
    print("EAGLE TRIS", g.tris(), report, "verts", len(g.v), "bones", len(arm.bones))
    return rig, coll


AX_FWD, AX_UP, AX_LEFT = V((0, -1, 0)), V((0, 0, 1)), V((1, 0, 0))


def turn(a, b, degrees):
    """A turn that carries direction a toward direction b."""
    from mathutils import Quaternion
    return Quaternion(a.cross(b).normalized(), math.radians(degrees))


def pose(rig, turns):
    """Forward kinematics as Unity does it: each bone's turn is given about the model's axes in the rest pose, and a
    child's adds to its parent's."""
    from mathutils import Quaternion
    total = {}
    for bone in rig.data.bones:            # parents come first
        pb = rig.pose.bones[bone.name]
        before = total[bone.parent.name] if bone.parent else Quaternion()
        q = before @ turns.get(bone.name, Quaternion())
        total[bone.name] = q
        rest = bone.matrix_local
        at = rest.translation
        if bone.parent:
            at = (rig.pose.bones[bone.parent.name].matrix @ bone.parent.matrix_local.inverted() @ rest).translation
        pb.matrix = Matrix.Translation(at) @ q.to_matrix().to_4x4() @ rest.to_3x3().to_4x4()
        bpy.context.view_layer.update()


def wing_turn(sx, raise_, sweep, twist=0.0):
    out = AX_LEFT * sx
    return turn(out, AX_UP, raise_) @ turn(out, -AX_FWD, sweep) @ turn(AX_FWD, -AX_UP, twist)


def pose_named(rig, name):
    """glide: wings a little raised, feet reaching. fold: standing with the wings folded and the knees bent - the
    pose that shows whether the skinning holds."""
    t = {}
    for sx, tag in ((1, "L"), (-1, "R")):
        if name == "fold":
            t["Humerus_" + tag] = wing_turn(sx, -22, 58)
            t["Forearm_" + tag] = wing_turn(sx, 0, -112)
            t["Hand_" + tag] = wing_turn(sx, -6, 128)
            t["Thigh_" + tag] = turn(-AX_UP, AX_FWD, 42)
            t["Shank_" + tag] = turn(-AX_UP, AX_FWD, -68)
            t["Foot_" + tag] = turn(-AX_UP, AX_FWD, 26)
        else:
            t["Humerus_" + tag] = wing_turn(sx, 10, 4)
            t["Forearm_" + tag] = wing_turn(sx, 2, -6)
            t["Hand_" + tag] = wing_turn(sx, 6, 8)
            t["Thigh_" + tag] = turn(-AX_UP, AX_FWD, 30)
            t["Shank_" + tag] = turn(-AX_UP, AX_FWD, -20)
    if name == "fold":
        t["Neck1"] = turn(AX_FWD, AX_UP, 18)
        t["Head"] = turn(AX_FWD, AX_UP, -22)
        t["Tail"] = turn(-AX_FWD, AX_UP, -18)
    pose(rig, t)


def rest_pose(rig):
    pose(rig, {})


def render(views, samples):
    os.makedirs(RENDERS, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    try:
        sc.cycles.use_denoising = True
    except Exception:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new("White")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (1, 1, 1, 1)
    bg.inputs[1].default_value = 1.0
    sc.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 2.6
    sun.data.angle = math.radians(12)
    sun.rotation_euler = (math.radians(48), 0, math.radians(-32))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    shots = {
        "front": ((3.6, -8.2, 3.4), (0, -0.1, -0.05), 50),
        "head": ((1.25, -2.7, 0.95), (0, -0.78, 0.3), 55),
        "top": ((0.01, 1.2, 10.5), (0, 0.1, 0), 50),
        "below": ((2.2, -5.5, -5.2), (0, 0, 0), 50),
        "side": ((7.5, -1.2, 0.9), (0, 0, 0.05), 85),
        "back": ((-3.2, 7.4, 3.6), (0, 0, 0), 50),
        "feet": ((1.3, -2.2, -1.2), (0, 0.05, -0.6), 60),
        "fold": ((3.2, -4.6, 1.6), (0, -0.05, -0.2), 50),
        "foldback": ((-2.6, 4.8, 2.6), (0, 0, -0.1), 50),
        "foldtop": ((0.01, 0.6, 7.5), (0, 0, 0), 50),
    }
    for name in views:
        loc, aim, lens = shots[name]
        pose_named(bpy.data.objects["TU_Eagle_Rig"], "fold" if name.startswith("fold") else "glide")
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (V(aim) - V(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(RENDERS, "eagle_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)
    for o in (sun, cam): bpy.data.objects.remove(o)


def export():
    import shutil
    models = os.path.join(ROOT, "Assets", "Art", "Tutorial", "Models")
    textures = os.path.join(ROOT, "Assets", "Art", "Tutorial", "Textures")
    bpy.ops.object.select_all(action='DESELECT')
    objs = [o for o in bpy.data.objects if o.name.startswith("TU_Eagle")]
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["TU_Eagle_Rig"]
    path = os.path.join(models, "TU_Eagle.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=False,
                             object_types={'ARMATURE', 'MESH'}, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_armature_deform_only=True,
                             path_mode='STRIP', add_leaf_bones=False, bake_anim=False)
    for t in ("TU_EagleFeather_BaseColor.png", "TU_EagleFeather_Normal.png"):
        shutil.copy2(os.path.join(TEX_DIR, t), os.path.join(textures, t))
    print("EXPORT", path)


if __name__ == "__main__":
    rig, _ = build()
    if "--render" in ARGS:
        views = next((a.split("=")[1].split(",") for a in ARGS if a.startswith("--views=")), ["front", "head", "top", "below", "side", "back", "feet", "fold", "foldback", "foldtop"])
        samples = int(next((a.split("=")[1] for a in ARGS if a.startswith("--samples=")), 48))
        render(views, samples)
        rest_pose(rig)
    if "--save" in ARGS:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "TU_Eagle_v1.blend"))
    if "--export" in ARGS:
        export()
