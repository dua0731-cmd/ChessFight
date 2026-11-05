# 승규 11-03 "원형 홀 모델링 하려는데 시안 여러가지로 줘" -> "B랑 C 렌더 해서 블렌더 띄어": two looks for the round hall's
# wall (hub (0, 92), inner face r 31, open to the south phi 234..306 and the north phi 72..108, -4 .. 26), built over v20
# and rendered side by side. Render only for now: nothing exported, Unity untouched.
#   B  two-tier rotunda: the architectural wall (source/architectural_wall.glb, its glass left in: blind) bent round the
#      ring up to 14, a string course and a balustrade, then an open colonnade of 승규's stone column
#      (source/stone_column.glb) to 24 under an entablature, sky between the columns.
#   C  ribbed half dome: the same wall bent round the ring at full height with its windows open and the outer walls'
#      tracery (window_kit), stone ribs springing from its pilasters to a crown ring at 46, the hub's chandelier hung
#      from the crown, twice the size.
# The wall is laid out flat (x along the arc on the inner face, y the depth outwards, z up) and bent: phi = phi_end - x/31
# (no mirror, so the faces keep their winding), r = 31 + y. Long faces are sliced every ~0.6 m first so they follow the
# curve. The v9 smooth round wall stops drawing (moved to _Blockout originals), its collision stays as it was in Unity.
# Run: blender -b grand_stair_hall_v20.blend -P round_hall_v21.py -- B   (or C) -> grand_stair_hall_v21B/C.blend,
#      renders_v28/B_*.png / C_*.png
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import window_kit

VAR = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "C"
D = math.pi / 180
HY, R0, FLOOR, TOP = 92.0, 31.0, -4.0, 26.0
ARCS = ((108.0, 234.0), (306.0, 432.0))
DEPTH = 25.0                         # as the outer walls: the asset's 0.162 depth -> 4 m
W, H = 0.98, 0.59
CLAY = bpy.data.materials["Arch_Clay"]
sc = bpy.context.scene
col = bpy.data.collections.new("Round_hall_v21" + VAR); sc.collection.children.link(col)
originals = bpy.data.collections["_Blockout originals"]


def retire(name):
    o = bpy.data.objects.get(name)
    if o:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        originals.objects.link(o)


def polar(phi, r, z):
    return Vector((r * math.cos(phi * D), HY + r * math.sin(phi * D), z))


def bend(me, phi_end):
    for v in me.vertices:
        x, y, z = v.co
        p = phi_end * D - x / R0
        r = R0 + y
        v.co = (r * math.cos(p), HY + r * math.sin(p), z)
    me.update()


def link(name, me, smooth=30):
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if smooth:
        for s in bpy.context.selected_objects:
            s.select_set(False)
        bpy.context.view_layer.objects.active = o; o.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth))
    return o


def decimate(me, ratio):
    o = bpy.data.objects.new("_d", me); sc.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("d", "DECIMATE"); m.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(o)
    return me


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


def sweep(name, profile, a0, a1, steps_per_deg=.5):
    """A (r, z) profile swept round the hub from phi a0 to a1, both ends capped (a full turn is merged instead)."""
    bm = bmesh.new()
    vs = [bm.verts.new(polar(a0, r, z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[(i + 1) % len(vs)])) for i in range(len(vs))]
    full = abs(a1 - a0) >= 359.9
    r = bmesh.ops.spin(bm, geom=vs + es, cent=(0, HY, 0), axis=(0, 0, 1), angle=(a1 - a0) * D,
                       steps=max(2, int(abs(a1 - a0) * steps_per_deg)), use_duplicate=False)
    if full:
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    else:
        last = [v for v in r["geom_last"] if isinstance(v, bmesh.types.BMVert)]
        bm.faces.new(vs); bm.faces.new(last)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    return link(name, me, 0)


def lathe(name, profile, steps=16):
    """A (radius, z) profile turned about its own z axis (a solid of revolution at the origin)."""
    bm = bmesh.new()
    vs = [bm.verts.new((r, 0, z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[i + 1])) for i in range(len(vs) - 1)]
    bmesh.ops.spin(bm, geom=vs + es, cent=(0, 0, 0), axis=(0, 0, 1), angle=2 * math.pi, steps=steps, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(CLAY)
    for p in me.polygons:
        p.use_smooth = True
    return me


# ------------------------------------------------------------------ the wall, bent round the ring
retire("Hall/Hall_wall_(round) (smooth)")
retire("Hall/Hall_wall_(round) (smooth 2)")
raw = load("architectural_wall.glb")
lo = Vector([min(v.co[i] for v in raw.vertices) for i in range(3)])
hi = Vector([max(v.co[i] for v in raw.vertices) for i in range(3)])
if VAR == "C":
    module = window_kit.prepare(raw, .35)
    wall_top = TOP
else:
    module = decimate(raw, .35)
    wall_top = 14.0
for p in module.polygons:
    p.use_smooth = False
slice_x(module, lo.x, hi.x, 56)
local = Matrix.Translation(-lo)
PIERS = (0.0, .348, .632, .98)                  # the asset's pilasters (its two ends and the big ones), local x
piers = []                                      # (phi) of every pilaster, for the ribs
for ai, (a0, a1) in enumerate(ARCS):
    L = R0 * (a1 - a0) * D
    Lm = L / 2
    sx = Lm / W
    for k in range(2):
        M = (Matrix.Translation((k * Lm, 0, FLOOR)) @ Matrix.Diagonal((sx, DEPTH, (wall_top - FLOOR) / H, 1)) @ local)
        me = module.copy(); me.transform(M); bend(me, a1)
        link("Round_wall_%d_%d" % (ai, k), me)
        if VAR == "C":
            t = window_kit.tracery("Round_wall_%d_%d tracery" % (ai, k), M, window_kit.CENTRES, col, CLAY)
            t.data.transform(M); t.matrix_world = Matrix.Identity(4); bend(t.data, a1)
            for p in t.data.polygons:
                p.use_smooth = True
        for u in PIERS:
            phi = a1 - (k * Lm + u * sx) / R0 / D
            if all(abs(phi - q) > .5 for q in piers):
                piers.append(phi)
print("WALL", VAR, "piers", len(piers), sorted(round(p, 1) for p in piers))


if VAR == "B":
    # The string course over the lower wall, its moulding stepping out into the hall.
    course = [(31.0, 13.2), (30.6, 13.6), (30.3, 14.0), (30.3, 14.6), (30.0, 14.9), (30.0, 15.3), (35.4, 15.3), (35.4, 13.2)]
    # The entablature over the columns: architrave, frieze and a cornice reaching into the hall.
    entab = [(31.6, 24.0), (34.4, 24.0), (34.4, 25.0), (35.4, 25.0), (35.4, 26.6), (30.4, 26.6), (30.4, 26.1),
             (30.9, 25.8), (31.3, 25.6), (31.3, 25.0), (31.6, 24.9)]
    rail_lo = [(30.2, 15.3), (30.9, 15.3), (30.9, 15.55), (30.2, 15.55)]
    rail_hi = [(30.05, 16.45), (31.05, 16.45), (31.05, 16.6), (30.9, 16.75), (30.2, 16.75), (30.05, 16.6)]
    for ai, (a0, a1) in enumerate(ARCS):
        sweep("String_course_%d" % ai, course, a0, a1)
        sweep("Entablature_%d" % ai, entab, a0, a1)
        sweep("Balustrade_rail_low_%d" % ai, rail_lo, a0, a1)
        sweep("Balustrade_rail_top_%d" % ai, rail_hi, a0, a1)
    baluster = lathe("Baluster", [(0, 0), (.13, 0), (.13, .07), (.07, .13), (.07, .2), (.14, .42), (.07, .66),
                                  (.1, .74), (.1, .9), (0, .9)], 12)
    # 승규's stone column, decimated, 8.7 m high, its deeper side radial.
    cm = load("stone_column.glb")
    cm = decimate(cm, 6000 / max(1, len(cm.polygons)))
    clo = Vector([min(v.co[i] for v in cm.vertices) for i in range(3)])
    chi = Vector([max(v.co[i] for v in cm.vertices) for i in range(3)])
    s = 8.7 / (chi.z - clo.z)
    cm.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(clo.x + chi.x) / 2, -(clo.y + chi.y) / 2, -clo.z)))
    for p in cm.polygons:
        p.use_smooth = True
    n_col, n_bal = 0, 0
    for ai, (a0, a1) in enumerate(ARCS):
        n = 12
        for i in range(n):
            phi = a0 + (a1 - a0) * (i + .5) / n
            o = bpy.data.objects.new("Column_%d_%02d" % (ai, i), cm); col.objects.link(o)
            o.location = polar(phi, 33.0, 15.3)
            o.rotation_euler = (0, 0, phi * D - math.pi / 2)    # local y (the deeper side) along the radius
            n_col += 1
        L = 30.55 * (a1 - a0) * D
        m = int(L / .62)
        for i in range(m):
            phi = a0 + (a1 - a0) * (i + .5) / m
            o = bpy.data.objects.new("Baluster_%d_%03d" % (ai, i), baluster); col.objects.link(o)
            o.location = polar(phi, 30.55, 15.55)
            n_bal += 1
    print("COLONNADE", n_col, "columns", round(8.7, 1), "m,", n_bal, "balusters, column", len(cm.polygons), "faces")


if VAR == "C":
    RS, ZS, RC, ZC = 31.9, 25.4, 7.4, 45.6      # rib springing (in the wall's top) and its foot on the crown ring
    WID, DEP, CH = 1.3, 1.6, .28                # rib width (along the wall), depth, corner chamfer

    def rib(name, phi):
        bm = bmesh.new()
        er = Vector((math.cos(phi * D), math.sin(phi * D), 0)); ep = Vector((-math.sin(phi * D), math.cos(phi * D), 0))
        ez = Vector((0, 0, 1))
        sec = [(-WID / 2 + CH, -DEP / 2), (WID / 2 - CH, -DEP / 2), (WID / 2, -DEP / 2 + CH), (WID / 2, DEP / 2 - CH),
               (WID / 2 - CH, DEP / 2), (-WID / 2 + CH, DEP / 2), (-WID / 2, DEP / 2 - CH), (-WID / 2, -DEP / 2 + CH)]
        rings, N = [], 40
        for i in range(N + 1):
            t = (math.pi / 2) * i / N
            r = RC + (RS - RC) * math.cos(t); z = ZS + (ZC - ZS) * math.sin(t)
            dr, dz = -(RS - RC) * math.sin(t), (ZC - ZS) * math.cos(t)
            ln = math.hypot(dr, dz); nr, nz = -dz / ln, dr / ln       # in the r-z plane, square to the curve (inwards-up)
            c = polar(phi, r, z); nv = er * nr + ez * nz
            # deeper at the springing, thinner at the crown
            k = 1.0 - .25 * (i / N)
            rings.append([bm.verts.new(c + ep * a * k + nv * b * k) for a, b in sec])
        for a, b in zip(rings, rings[1:]):
            for j in range(len(sec)):
                bm.faces.new((a[j], a[(j + 1) % len(sec)], b[(j + 1) % len(sec)], b[j]))
        bm.faces.new(rings[0]); bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
        me.materials.append(CLAY)
        return link(name, me, 40)

    for i, phi in enumerate(sorted(piers)):
        rib("Rib_%02d" % i, phi)
        # a corbel under each rib's springing, on the pilaster's top
        sweep("Rib_corbel_%02d" % i, [(30.2, 24.2), (31.2, 24.2), (31.2, 26.0), (29.9, 26.0), (29.9, 25.4), (30.2, 25.1)],
              phi - 1.6, phi + 1.6, 2)
    crown = [(5.0, 44.3), (8.6, 44.3), (8.6, 45.0), (9.3, 45.4), (9.3, 46.2), (8.5, 46.6), (8.5, 47.7), (5.0, 47.7)]
    sweep("Crown_ring", crown, 0, 360, .8)
    sweep("Crown_ring_lip", [(4.5, 47.7), (9.0, 47.7), (9.0, 48.2), (4.5, 48.2)], 0, 360, .8)
    # four spokes to a boss in the ring's middle, where the chandelier hangs
    for i in range(4):
        a = 45 + 90 * i
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bm.transform(Matrix.Translation((0, 0, 0)) @ Matrix.Diagonal((5.2, .7, 1.0, 1)))
        bm.transform(Matrix.Translation((3.0, 0, 0)))
        bm.transform(Matrix.Rotation(a * D, 4, "Z"))
        bm.transform(Matrix.Translation((0, HY, 46.0)))
        me = bpy.data.meshes.new("Crown_spoke_%d" % i); bm.to_mesh(me); bm.free(); me.materials.append(CLAY)
        link(me.name, me, 0)
    boss = lathe("Crown_boss", [(0, 43.4), (.6, 43.4), (1.2, 44.0), (1.3, 45.0), (1.1, 47.2), (0, 47.2)], 24)
    link("Crown_boss", boss, 0).location = (0, HY, 0)

    # The hub's chandelier (v10's at (0, 92)): twice the size, its hook at 33, a chain up to the boss.
    hub = [o for o in bpy.data.objects if o.name.startswith("Chandelier_") and "/Part_" in o.name
           and abs(o.location.x) < 1.5 and abs(o.location.y - HY) < 1.5]
    if hub:
        rig = hub[0].name.split("/")[0]
        parts = [o for o in bpy.data.objects if o.name.startswith(rig + "/Part_")]
        chains = [o for o in bpy.data.objects if o.name.startswith(rig + "/Chain_")]
        hook = max(o.location.z for o in parts) + .3
        S, NEW_HOOK = 2.0, 33.0
        for o in parts:
            o.location = Vector((0, HY, NEW_HOOK)) + (o.location - Vector((0, HY, hook))) * S
            o.scale = o.scale * S
        cme = chains[0].data if chains else None
        for o in chains:
            bpy.data.objects.remove(o)
        if cme:
            zs = [v.co.z for v in cme.vertices]
            pitch = (max(zs) - min(zs)) * .9 * 1.6
            z, i = 43.5, 0
            while z - pitch * .5 > NEW_HOOK:
                o = bpy.data.objects.new("%s/Chain_%02d" % (rig, i), cme); col.objects.link(o)
                o.location = (0, HY, z); o.rotation_euler = (0, 0, math.pi / 2 * (i % 2)); o.scale = (1.6, 1.6, 1.6)
                z -= pitch; i += 1
        print("CHANDELIER", rig, len(parts), "parts x", S, "hook", NEW_HOOK, "chain links", i)


# ------------------------------------------------------------------ cameras and the renders
def camera(name, loc, at, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_end = 2000
    o = bpy.data.objects.new(name, cd); col.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


cams = [camera("Cam_overview", (58, 28, 72), (0, 96, 10), 26),
        camera("Cam_inside", (8, 70, 12), (-4, 112, 30), 16),
        camera("Cam_low", (-22, 82, 1), (16, 106, 32), 18),
        camera("Cam_from_balcony", (0, 136, 21), (0, 88, 20), 18)]
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 1600, 900, 100
sh = sc.display.shading
sh.light = "STUDIO"; sh.color_type = "MATERIAL"; sh.show_cavity = True; sh.cavity_type = "BOTH"
sh.show_shadows = False; sh.show_backface_culling = True
sh.background_type = "VIEWPORT"; sh.background_color = (.72, .80, .88)
sc.display.render_aa = "16"
out = os.path.join(HERE, "renders_v28"); os.makedirs(out, exist_ok=True)
for c in cams:
    sc.camera = c
    sc.render.filepath = os.path.join(out, "%s_%s.png" % (VAR, c.name[4:]))
    bpy.ops.render.render(write_still=True)
sc.camera = cams[0]
faces = sum(len(o.data.polygons) for o in col.objects if o.type == "MESH")
print("DONE", VAR, len(col.objects), "objects", faces, "faces (instances counted each)")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v21%s.blend" % VAR))
