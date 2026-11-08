# 승규 11-03 "원 반경 규격 다시 맞추고 C로 가 ... 튀어나오는 구조물이 없는지 수치상 검토하고 갈비뼈 반돔 더 멋있는 디자인 샘플 3개":
# C (v21) again with the wall fitted to the course, and three domes to choose from, each in its own collection.
#   Fit: v21 put the wall's frontmost point (the pilaster bases and capitals) on r 31, so at the curving stair's height
#        (8 .. 12) the pilaster shafts stood at r 31.57 and the stair (out to r 31, Unity's HallRadius) stopped 0.57 m short
#        of them (round_hall_audit.py). Now the frontmost point between z 6 and 12 is on r 31: the shafts meet the stair's
#        edge and the collision face; bases (z < 4) and capitals (z > 20) stand 0.57 m into the hall, under and over
#        the course.
#   Dome_1  lamella star vault: from every pilaster two ribs twisting 48 degrees either way to the crown, crossing in
#           diamonds; on the crown a lantern of eight columns, a cap and a king's cross.
#   Dome_2  king's crown: the ribs gather to a wide crown ring at 40 set with pearls and fleurons, four arches over it to
#           an orb and a cross at 60.
#   Dome_3  armillary cage: the ribs held by three hoops, fourteen small chandeliers hanging off the lowest hoop, a glass
#           oculus in the crown ring.
# The hub chandelier (v10's at (0, 92)) twice the size, its hook at 33, a chain up to each dome's crown.
# Render only: nothing exported, Unity untouched.
# Run: blender -b grand_stair_hall_v20.blend -P round_hall_v22.py -> grand_stair_hall_v22C.blend, renders_v29/D<n>_*.png
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import window_kit

D = math.pi / 180
HY, R0, FLOOR, TOP = 92.0, 31.0, -4.0, 26.0
ARCS = ((108.0, 234.0), (306.0, 432.0))
FIT_Z = (6.0, 12.0)                  # the band whose frontmost point is put on r 31 (the curving stair is 8 .. 10)
DEPTH, W, H = 25.0, 0.98, 0.59
CLAY = bpy.data.materials["Arch_Clay"]
GLASS = bpy.data.materials.get("Glass_TeamDivider") or CLAY
sc = bpy.context.scene
originals = bpy.data.collections["_Blockout originals"]


def new_col(name):
    c = bpy.data.collections.new(name); sc.collection.children.link(c)
    return c


wall_col = new_col("Round_hall_v22C")


def retire(name):
    o = bpy.data.objects.get(name)
    if o:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        originals.objects.link(o)


def polar(phi, r, z):
    return Vector((r * math.cos(phi * D), HY + r * math.sin(phi * D), z))


def link(col, name, me, smooth=30):
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if smooth:
        for s in bpy.context.selected_objects:
            s.select_set(False)
        bpy.context.view_layer.objects.active = o; o.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth))
    return o


def mesh_from(bm, name, mat=CLAY):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    return me


# ------------------------------------------------------------------ the wall (v21 C), fitted
def bend(me, phi_end, face):
    for v in me.vertices:
        x, y, z = v.co
        p = phi_end * D - x / R0
        r = R0 + (y - face)
        v.co = (r * math.cos(p), HY + r * math.sin(p), z)
    me.update()


def load(fname):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "source", fname))
    new = [o for o in bpy.data.objects if o not in before]
    src = [o for o in new if o.type == "MESH"][0]
    me = src.data.copy(); me.transform(src.matrix_world)
    for o in new:
        bpy.data.objects.remove(o)
    me.materials.clear(); me.materials.append(CLAY)
    return me


def slice_x(me, x0, x1, n):
    bm = bmesh.new(); bm.from_mesh(me)
    for i in range(1, n):
        x = x0 + (x1 - x0) * i / n
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0), plane_no=(1, 0, 0))
    bm.to_mesh(me); bm.free()
    return me


retire("Hall/Hall_wall_(round) (smooth)")
retire("Hall/Hall_wall_(round) (smooth 2)")
raw = load("architectural_wall.glb")
lo = Vector([min(v.co[i] for v in raw.vertices) for i in range(3)])
hi = Vector([max(v.co[i] for v in raw.vertices) for i in range(3)])
module = window_kit.prepare(raw, .35)
for p in module.polygons:
    p.use_smooth = False
slice_x(module, lo.x, hi.x, 56)
local = Matrix.Translation(-lo)
PIERS = (0.0, .348, .632, .98)
piers, face = [], None
for ai, (a0, a1) in enumerate(ARCS):
    Lm = R0 * (a1 - a0) * D / 2
    sx = Lm / W
    for k in range(2):
        M = Matrix.Translation((k * Lm, 0, FLOOR)) @ Matrix.Diagonal((sx, DEPTH, (TOP - FLOOR) / H, 1)) @ local
        me = module.copy(); me.transform(M)
        if face is None:
            face = min(v.co.y for v in me.vertices if FIT_Z[0] <= v.co.z <= FIT_Z[1])
            front = min(v.co.y for v in me.vertices)
            print("FIT face at z %.0f..%.0f is %.3f m behind the frontmost point: the wall comes %.3f m in"
                  % (FIT_Z[0], FIT_Z[1], face - front, face - front))
        bend(me, a1, face)
        link(wall_col, "Round_wall_%d_%d" % (ai, k), me)
        t = window_kit.tracery("Round_wall_%d_%d tracery" % (ai, k), M, window_kit.CENTRES, wall_col, CLAY)
        t.data.transform(M); t.matrix_world = Matrix.Identity(4); bend(t.data, a1, face)
        for p in t.data.polygons:
            p.use_smooth = True
        for u in PIERS:
            phi = a1 - (k * Lm + u * sx) / R0 / D
            if all(abs(phi - q) > .5 for q in piers):
                piers.append(phi)
piers.sort()
print("WALL piers", len(piers), [round(p, 1) for p in piers])


# ------------------------------------------------------------------ shared shapes
def sweep(col, name, profile, a0, a1, steps_per_deg=.5, mat=CLAY, smooth=0):
    bm = bmesh.new()
    vs = [bm.verts.new(polar(a0, r, z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[(i + 1) % len(vs)])) for i in range(len(vs))]
    full = abs(a1 - a0) >= 359.9
    res = bmesh.ops.spin(bm, geom=vs + es, cent=(0, HY, 0), axis=(0, 0, 1), angle=(a1 - a0) * D,
                         steps=max(2, int(abs(a1 - a0) * steps_per_deg)), use_duplicate=False)
    if full:
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    else:
        bm.faces.new(vs); bm.faces.new([v for v in res["geom_last"] if isinstance(v, bmesh.types.BMVert)])
    return link(col, name, mesh_from(bm, name, mat), smooth)


def tube(col, name, path, wid, dep, ref, taper=1.0, ch=.22, mat=CLAY):
    """A chamfered bar along `path`; its depth faces `ref(p)` (a unit vector at each point, e.g. the dome's normal)."""
    sec = [(-wid / 2 + ch, -dep / 2), (wid / 2 - ch, -dep / 2), (wid / 2, -dep / 2 + ch), (wid / 2, dep / 2 - ch),
           (wid / 2 - ch, dep / 2), (-wid / 2 + ch, dep / 2), (-wid / 2, dep / 2 - ch), (-wid / 2, -dep / 2 + ch)]
    bm = bmesh.new(); rings = []
    n = len(path)
    for i, p in enumerate(path):
        t = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        nv = ref(p)
        a = t.cross(nv).normalized(); b = a.cross(t).normalized()
        k = 1 + (taper - 1) * i / (n - 1)
        rings.append([bm.verts.new(p + a * u * k + b * v * k) for u, v in sec])
    for r1, r2 in zip(rings, rings[1:]):
        for j in range(8):
            bm.faces.new((r1[j], r1[(j + 1) % 8], r2[(j + 1) % 8], r2[j]))
    bm.faces.new(rings[0]); bm.faces.new(rings[-1])
    return link(col, name, mesh_from(bm, name, mat), 40)


def ball(col, name, c, r, mat=CLAY, seg=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=r, matrix=Matrix.Translation(c))
    me = mesh_from(bm, name, mat)
    for p in me.polygons:
        p.use_smooth = True
    return link(col, name, me, 0)


def lathe(col, name, profile, at, steps=24, mat=CLAY):
    """A (radius, z) profile turned about the vertical through `at` (its z added)."""
    bm = bmesh.new()
    vs = [bm.verts.new((at.x + r, at.y, at.z + z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[i + 1])) for i in range(len(vs) - 1)]
    bmesh.ops.spin(bm, geom=vs + es, cent=at, axis=(0, 0, 1), angle=2 * math.pi, steps=steps, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    me = mesh_from(bm, name, mat)
    for p in me.polygons:
        p.use_smooth = True
    return link(col, name, me, 0)


def box(col, name, c, size, rot_z=0.0, mat=CLAY):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation(c) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Diagonal((*size, 1)))
    return link(col, name, mesh_from(bm, name, mat), 0)


def cross(col, name, base, s=1.0):
    box(col, name + " upright", base + Vector((0, 0, 1.6 * s)), (.45 * s, .45 * s, 3.2 * s))
    box(col, name + " bar", base + Vector((0, 0, 2.2 * s)), (2.0 * s, .45 * s, .45 * s))
    box(col, name + " bar 2", base + Vector((0, 0, 2.2 * s)), (.45 * s, 2.0 * s, .45 * s))


def corbels(col):
    for i, phi in enumerate(piers):
        sweep(col, "Rib_corbel_%02d" % i, [(29.6, 24.2), (30.9, 24.2), (30.9, 26.0), (29.3, 26.0), (29.3, 25.4), (29.6, 25.1)],
              phi - 1.6, phi + 1.6, 2)


# a dome surface: meridian t 0 .. 90 from the springing (RS, ZS) to the crown (rc, zc)
RS, ZS = 31.4, 25.4


def meridian(phi_of_t, rc, zc, n=48):
    out = []
    for i in range(n + 1):
        t = (math.pi / 2) * i / n
        out.append(polar(phi_of_t(i / n), rc + (RS - rc) * math.cos(t), ZS + (zc - ZS) * math.sin(t)))
    return out


def dome_normal(zc):
    c = Vector((0, HY, ZS))
    return lambda p: (p - c).normalized()


def hang_chain(col, rig, top, hook, scale=1.6):
    cme = bpy.data.meshes.get("ChainSegment")
    zs = [v.co.z for v in cme.vertices]
    pitch = (max(zs) - min(zs)) * .9 * scale
    z, i = top, 0
    while z - pitch * .5 > hook:
        o = bpy.data.objects.new("%s/Chain_%02d" % (rig, i), cme); col.objects.link(o)
        o.location = (0, HY, z); o.rotation_euler = (0, 0, math.pi / 2 * (i % 2)); o.scale = (scale,) * 3
        z -= pitch; i += 1
    return i


# The hub chandelier: twice the size, hook at 33 (bottom about 25, 9 m over the dais).
hub = [o for o in bpy.data.objects if o.name.startswith("Chandelier_") and "/Part_" in o.name
       and abs(o.location.x) < 1.5 and abs(o.location.y - HY) < 1.5]
RIG = hub[0].name.split("/")[0]
HOOK = 33.0
parts = [o for o in bpy.data.objects if o.name.startswith(RIG + "/Part_")]
old_hook = max(o.location.z for o in parts) + .3
for o in parts:
    o.location = Vector((0, HY, HOOK)) + (o.location - Vector((0, HY, old_hook))) * 2.0
    o.scale = o.scale * 2.0
for o in [o for o in bpy.data.objects if o.name.startswith(RIG + "/Chain_")]:
    bpy.data.objects.remove(o)
print("HUB chandelier", RIG, "bottom", round(min((o.matrix_world @ Vector(c)).z for o in parts for c in o.bound_box), 2))


# ------------------------------------------------------------------ Dome 1: lamella star vault and lantern
def dome1():
    col = new_col("Dome_1 lamella star vault")
    corbels(col)
    RC, ZC, TW = 6.2, 45.4, 48.0
    nrm = dome_normal(ZC)
    for i, phi in enumerate(piers):
        for s in (-1, 1):
            path = meridian(lambda u, phi=phi, s=s: phi + s * TW * (1 - (1 - u) ** 1.6), RC, ZC)
            tube(col, "Lamella_%02d_%s" % (i, "L" if s < 0 else "R"), path, 1.0, 1.25, nrm, .8)
    sweep(col, "Crown_ring", [(5.0, 44.4), (7.8, 44.4), (8.4, 45.0), (8.4, 46.2), (7.6, 46.6), (7.6, 47.6), (5.0, 47.6)], 0, 360, .8)
    # the lantern: eight columns, an entablature ring, a ribbed cap, a ball and a cross
    for j in range(8):
        a = j * 45 + 22.5
        lathe(col, "Lantern_column_%d" % j, [(0, 0), (.55, 0), (.55, .25), (.38, .4), (.32, 3.6), (.5, 3.8), (.5, 4.1), (0, 4.1)],
              polar(a, 6.2, 47.6), 12)
    sweep(col, "Lantern_ring", [(5.4, 51.7), (7.2, 51.7), (7.2, 52.6), (5.4, 52.6)], 0, 360, .8)
    lathe(col, "Lantern_cap", [(0, 0), (6.9, 0), (6.6, .8), (5.6, 2.2), (3.8, 3.4), (1.6, 4.1), (0, 4.2)], Vector((0, HY, 52.6)), 32)
    ball(col, "Lantern_ball", Vector((0, HY, 57.6)), 1.1)
    cross(col, "Lantern_cross", Vector((0, HY, 58.4)), 1.0)
    # the chandelier hangs from a boss under the lantern
    lathe(col, "Crown_boss", [(0, 0), (.7, 0), (1.3, .6), (1.3, 1.4), (0, 1.4)], Vector((0, HY, 43.2)), 16)
    for j in range(4):
        box(col, "Crown_spoke_%d" % j, polar(45 + 90 * j, 3.4, 44.0), (5.6, .6, .8), (45 + 90 * j) * D)
    hang_chain(col, RIG, 43.2, HOOK)
    return col


# ------------------------------------------------------------------ Dome 2: the king's crown
def dome2():
    col = new_col("Dome_2 king's crown")
    corbels(col)
    RC, ZC = 12.5, 40.0
    nrm = dome_normal(ZC)
    for i, phi in enumerate(piers):
        tube(col, "Rib_%02d" % i, meridian(lambda u, phi=phi: phi, RC, ZC), 1.3, 1.6, nrm, .85)
    band = [(11.2, 39.3), (14.6, 39.3), (14.6, 40.0), (15.2, 40.4), (15.2, 42.6), (14.6, 43.0), (14.6, 43.6), (11.2, 43.6)]
    sweep(col, "Crown_band", band, 0, 360, .8)
    for j in range(28):                                    # pearls round the band
        ball(col, "Crown_pearl_%02d" % j, polar(j * 360 / 28 + 6.4, 15.25, 41.5), .42, seg=12)
    for j in range(14):                                    # fleurons and points round the rim, between the ribs
        a = j * 360 / 14
        lathe(col, "Crown_fleuron_%02d" % j, [(0, 0), (1.0, 0), (1.15, .6), (.7, 1.6), (.95, 2.4), (.4, 3.4), (0, 4.0)],
              polar(a, 13.0, 43.6), 8)
        ball(col, "Crown_point_pearl_%02d" % j, polar(a + 360 / 28, 13.0, 45.2), .6, seg=12)
        box(col, "Crown_point_%02d" % j, polar(a + 360 / 28, 13.0, 44.4), (.5, .5, 1.6), (a + 360 / 28) * D)
    # four arches from the band over to the orb, dipping a little in the middle as a king's crown does
    nrm2 = lambda p: (p - Vector((0, HY, 40.0))).normalized()
    for j in range(4):
        a = 45 + 90 * j
        path = []
        for i in range(41):
            t = (math.pi / 2) * i / 40
            path.append(polar(a, 13.0 * math.cos(t), 43.4 + 13.6 * math.sin(t) - 1.2 * math.sin(2 * t) ** 4))
        tube(col, "Crown_arch_%d" % j, path, 1.4, 1.1, nrm2, .7)
        for i in range(1, 9):                              # pearls along each arch
            t = (math.pi / 2) * i / 9.5
            ball(col, "Crown_arch_pearl_%d_%d" % (j, i), polar(a, 13.0 * math.cos(t), 44.4 + 13.6 * math.sin(t)
                                                                     - 1.2 * math.sin(2 * t) ** 4), .38, seg=10)
    ball(col, "Crown_orb", Vector((0, HY, 59.0)), 2.0, seg=24)
    sweep(col, "Crown_orb_band", [(1.95, 58.8), (2.15, 58.8), (2.15, 59.2), (1.95, 59.2)], 0, 360, .5)
    cross(col, "Crown_cross", Vector((0, HY, 60.6)), 1.4)
    hang_chain(col, RIG, 56.8, HOOK)
    return col


# ------------------------------------------------------------------ Dome 3: armillary cage, small chandeliers, oculus
def dome3():
    col = new_col("Dome_3 armillary cage")
    corbels(col)
    RC, ZC = 6.6, 45.4
    nrm = dome_normal(ZC)
    for i, phi in enumerate(piers):
        tube(col, "Rib_%02d" % i, meridian(lambda u, phi=phi: phi, RC, ZC), 1.1, 1.5, nrm, .8)
    hoops = []
    for j, deg in enumerate((28, 55, 76)):
        t = deg * D
        r, z = RC + (RS - RC) * math.cos(t), ZS + (ZC - ZS) * math.sin(t)
        hoops.append((r, z))
        w = .55 - .1 * j
        sweep(col, "Hoop_%d" % j, [(r - w, z - .45), (r + w, z - .45), (r + w + .25, z), (r + w, z + .45), (r - w, z + .45),
                                   (r - w - .25, z)], 0, 360, .8, smooth=40)
    sweep(col, "Crown_ring", [(5.2, 44.4), (8.0, 44.4), (8.6, 45.0), (8.6, 46.2), (7.8, 46.6), (7.8, 47.4), (5.2, 47.4)], 0, 360, .8)
    sweep(col, "Oculus_glass", [(0, 46.4), (5.3, 46.4), (5.3, 46.7), (0, 46.7)], 0, 360, .8, mat=GLASS)
    for j in range(6):                                    # leading over the glass
        box(col, "Oculus_lead_%d" % j, polar(j * 30, 0, 46.8), (10.4, .18, .18), j * 30 * D)
    sweep(col, "Oculus_lead_ring", [(2.4, 46.7), (2.65, 46.7), (2.65, 46.9), (2.4, 46.9)], 0, 360, .8)
    # fourteen small chandeliers off the lowest hoop, halfway between the ribs (v10's asset, its own size)
    r, z = hoops[0]
    small = [(o.data, o.location - Vector((0, HY, HOOK))) for o in parts]
    for i in range(len(piers)):
        a = (piers[i] + piers[(i + 1) % len(piers)]) / 2 + (180 if piers[(i + 1) % len(piers)] < piers[i] else 0)
        if not any(a0 <= a <= a1 or a0 <= a + 360 <= a1 for a0, a1 in ARCS):
            continue
        hook = polar(a, r, z - 4.5)
        for j, (me, off) in enumerate(small):
            o = bpy.data.objects.new("Hoop_chandelier_%02d/Part_%03d" % (i, j), me); col.objects.link(o)
            o.location = hook + off * .5
            o.scale = (2.0 * .5,) * 3 if parts[0].scale.x > 1.5 else (1,) * 3
        cme = bpy.data.meshes.get("ChainSegment")
        zs = [v.co.z for v in cme.vertices]; pitch = (max(zs) - min(zs)) * .9
        zz, n = z - .4, 0
        while zz - pitch * .5 > z - 4.5:
            o = bpy.data.objects.new("Hoop_chandelier_%02d/Chain_%02d" % (i, n), cme); col.objects.link(o)
            o.location = hook + Vector((0, 0, zz - (z - 4.5))); o.rotation_euler = (0, 0, math.pi / 2 * (n % 2))
            zz -= pitch; n += 1
    hang_chain(col, RIG, 46.3, HOOK)
    return col


domes = [dome1(), dome2(), dome3()]


# ------------------------------------------------------------------ cameras and renders, one dome at a time
def camera(name, loc, at, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    o = bpy.data.objects.new(name, cd); wall_col.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


cams = [camera("Cam_overview", (62, 24, 84), (0, 96, 18), 26),
        camera("Cam_low", (-22, 82, 1), (16, 106, 34), 18),
        camera("Cam_from_balcony", (0, 136, 21), (0, 88, 26), 18),
        camera("Cam_dome_up", (0, 85.5, 17.8), (0, 100, 44), 14)]
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 1600, 900, 100
sh = sc.display.shading
sh.light = "STUDIO"; sh.color_type = "MATERIAL"; sh.show_cavity = True; sh.cavity_type = "BOTH"
sh.show_shadows = False; sh.show_backface_culling = True
sh.background_type = "VIEWPORT"; sh.background_color = (.72, .80, .88)
if sc.world:
    sc.world.color = (.62, .72, .84)
sc.display.render_aa = "16"
out = os.path.join(HERE, "renders_v29"); os.makedirs(out, exist_ok=True)
vl = bpy.context.view_layer.layer_collection
for n, dc in enumerate(domes, 1):
    for m, other in enumerate(domes, 1):
        vl.children[other.name].exclude = (m != n)
    for c in cams:
        sc.camera = c
        sc.render.filepath = os.path.join(out, "D%d_%s.png" % (n, c.name[4:]))
        bpy.ops.render.render(write_still=True)
    faces = sum(len(o.data.polygons) for o in dc.objects if o.type == "MESH")
    print("DOME", n, dc.name, len(dc.objects), "objects", faces, "faces")
for m, other in enumerate(domes, 1):
    vl.children[other.name].exclude = (m != 1)
sc.camera = cams[0]
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v22C.blend"))
