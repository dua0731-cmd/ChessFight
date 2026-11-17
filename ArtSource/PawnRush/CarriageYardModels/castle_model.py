# Royal Carriage Yard models (R678): the AI-3D castle kit fitted over the code's gates (승규 11-15, through the peer
# session: "마차뜰 블록아웃 채팅에서 그 구간 모델링 작업을 할 때 이거 쓰라고 하면 돼", "일단 에셋을 늘려서 써도 되고 이상하면
# 추후에 바꿈"; and "색이 없는 에셋은 에셋들과 색을 맞춘 다음 적용 시켜").
#
#   exec(open(r".../rcy_kit.py").read()); exec(open(r".../castle_model.py").read())
#   then exec(open(r".../export_rcy.py").read()) and castle_export()
#
# Sources, source_ai3d/ (each one textured mesh about 1 m wide, front -y, up +z, base centre at the origin once
# imported): stone_castle_gate (the outer gatehouse), beige_castle_gate (the final gatehouse), beige_castle_wall (a tall
# and a low wall segment), stone_castle_tower (three rook towers: short, slim, wide), castle_archway_stairs (the section
# end; it came with a blank base colour, painted here to the kit's: cream stone, the white marble balustrade, dark brass
# - castle_archway_stairs_base.png). The textures stay theirs: these are not clay.
#
# Builds, in the MAP frame (rcy_kit.py), each stretched so that what a pawn touches is where the code's collider is
# (the collider rule, 승규 "전해": box colliders only, the model fitted to them where pawns touch, boxes added where it
# cannot be, gameplay widths never changed):
#   "RCY Castle outer gate"   the outer gatehouse, its passage the code's 6 m opening (x +-3, 5.6 high, the court's
#                             face at z 0), two tall wall segments each side out to x +-23 on the gate wall, and the slim
#                             rook tower where the scenery's four round corner towers stand (the yard's, the canal's)
#   "RCY Castle final gate"   the final gatehouse, its passage the 8 m opening (x +-4, 7.3 high, the platform's face at
#                             z 155), two wall segments each side out to x +-26, the slim towers behind its corners
#   "RCY Castle section end"  the section end (asSection): the gateway's two piers on the 12 m arch's line (their inner
#                             faces at x +-6), its beam and brass crown, and the marble balustrade along the runout's and
#                             the entrance band's railings (inner face at x +-6). The asset's flight (three deep, low
#                             steps) cannot be stretched into four 0.25 m steps in 2 m (the section boundary standard):
#                             one of its steps (STEP: a tread and the riser up from it) is cut out and laid on each of the
#                             code's four (R679), whose blocks stop being drawn.
# Also writes guides/rcy_castle_boxes.json: the boxes the models need past the blockout's colliders (the gatehouses are
# deeper than the 2 m gate walls; their towers are round and stand out of them), which RoyalCarriageYard.Castle.cs carries.

import bpy, bmesh, hashlib, json, math, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

CASTLE_ROOT = globals().get("RCY_ROOT") or os.environ.get("RCY_ROOT") or r"C:/Work/ChessFight-SeoungTest"   # as rcy_kit.py
CASTLE_SRC = os.path.join(RCY_HERE, "source_ai3d")
CASTLE_SOURCES = {   # object in "RCY Castle source": (FBX in source_ai3d, the slot and texture name in Unity)
    "AI outer gatehouse": ("stone_castle_gate.fbx", "RcyCastle_OuterGate"),
    "AI final gatehouse": ("beige_castle_gate.fbx", "RcyCastle_FinalGate"),
    "AI walls": ("beige_castle_wall.fbx", "RcyCastle_Walls"),
    "AI towers": ("stone_castle_tower.fbx", "RcyCastle_Towers"),
    "AI section end": ("castle_archway_stairs.fbx", "RcyCastle_SectionEnd"),
}
CASTLE_PAINTED = {"AI section end": "castle_archway_stairs_base.png"}

# The fits (measured on the sources by ray casts, R678): scale (x, depth, up) and where the base centre stands.
OUTER = dict(scale=(23.26, 21.0, 18.8), at=(0.0, 1.79, 0.0))      # passage +-.129 -> 3 m, .298 high -> 5.6 m, the jambs'
                                                                  # face -.0854 -> z 0; the towers' yard side at 5.2
                                                                  # (the carriages keep past 5.3)
FINAL = dict(scale=(27.4, 24.0, 16.0), at=(0.0, 156.27, 8.0))     # passage +-.146 -> 4 m, .457 high -> 7.3 m, the face
                                                                  # -.053 -> z 155 (the push bar 13 cm in front of it)
GATEWAY = dict(scale=(30.32, 20.0, 22.0), at=(-.133, 180.48, 4.304))  # turned 180: the landing .168 -> y 8, the piers
                                                                   # on z 171.3..175.4 (the code's arch line 174.6..
                                                                   # 175.4). The opening is not in the asset's middle
                                                                   # (rays R679: its shafts' innermost stones at +6.23
                                                                   # and -5.96 at 30.8 wide), so it is moved .133 to -x
                                                                   # and narrowed to put them on the boxes' 6 m faces
PIER_CUT = .37   # the piers are taken from here up (world y 12.4, above where the balustrade ran into them); below
#                  it a course of them (PIER_COURSE high) is stacked to the ground
PIER_COURSE = .12
# The asset's second step (R679, measured by rays): its tread y -.245 .. -.068 at z .0985 between the balustrades
# (x +-.265), and the riser behind it up to the third's at .139. Laid on the code's steps (Sections.cs SectionEnd: from
# the terrace's edge at z 175, 0.5 m deep, each 0.25 m lower, the fourth level with the entrance band at y 7), turned to
# go down northwards: each tread on its step's top, its riser up to the one before at the step's south edge, 1 cm north
# of it so the first does not fight the terrace's drawn face.
STEP = dict(keep=lambda c: abs(c.x) < .265 and ((-.25 < c.y < -.07 and c.z > .085)
                                                 or (-.08 < c.y < -.06 and .09 < c.z < .142)),
            origin=(0.0, -.068, .0985), width=.53, depth=.177, rise=.0405)
STEPS = dict(edge=175.0, depth=.5, rise=.25, top=8.0, count=4, half=6.0, across=1)
# The balustrade at half the asset (R680 QA tried a quarter for 3.56k -> 1.8k a copy: the balusters went lumpy and the
# handrail thin at pawn height, so it stays a half).
RAIL_RATIO = .5
# The gatehouses' towers (R679), measured on the fitted models: a bronze band round the foot (studs on it), then stone
# that narrows as it rises, the outer towers' some 12 cm a metre, the final's after a ledge and a steep batter. Their
# boxes come in tiers above the floor, (from, to, leaning): the leaning ones along the stone's line between the heights.
# The final's stop under the ring course near its top (above any pawn's reach); the boxes' tops before were the walls'.
OUTER_TOWER = dict(band=(.2, 1.3), studs=(.3, 1.2), tiers=((0.0, 1.45, False), (1.45, 3.0, True), (3.0, 7.0, True)))
FINAL_TOWER = dict(band=(.3, 1.15), studs=(.4, 1.1), tiers=((0.0, 1.35, False), (1.35, 1.95, False), (1.95, 2.55, True),
                                                           (2.55, 9.4, True)))
FINAL_STRETCH = (15.3, 1.7, 1.5)   # the final gatehouse above its lintel (world y) stretched 1.7 up, easing in over 1.5 m
TOWER_SPOTS = [  # (FBX, Blender foot, diameter, height): the scenery's corner towers (RoyalCarriageYard.Scenery.cs)
    ("outer", (-24.0, 0.0, 0.0), 4.0, 12.0), ("outer", (24.0, 0.0, 0.0), 4.0, 12.0),
    ("outer", (-28.0, 82.0, 0.0), 4.0, 12.0), ("outer", (28.0, 82.0, 0.0), 4.0, 12.0),
    ("final", (-28.0, 159.0, 0.0), 4.4, 19.0), ("final", (28.0, 159.0, 0.0), 4.4, 19.0),
]


# ------------------------------------------------------------------ sources


def castle_source(name):
    """The source object (imported once from source_ai3d: transforms baked, base centre at the origin)."""
    ob = bpy.data.objects.get(name)
    if ob is not None:
        return ob
    fbx, slot = CASTLE_SOURCES[name]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(CASTLE_SRC, fbx))
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    ob = join(collection("RCY Castle source", hide_render=True), name, new)
    for o in [o for o in bpy.data.objects if o not in before and o != ob]:
        bpy.data.objects.remove(o, do_unlink=True)
    lo = Vector([min(v.co[k] for v in ob.data.vertices) for k in range(3)])
    hi = Vector([max(v.co[k] for v in ob.data.vertices) for k in range(3)])
    ob.data.transform(Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    paint = CASTLE_PAINTED.get(name)
    if paint:
        tree = ob.data.materials[0].node_tree
        bsdf = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].links[0].from_node.image = bpy.data.images.load(os.path.join(CASTLE_SRC, paint), check_existing=True)
    return ob


def castle_materials():
    """Each source's one material named as Unity's slot and texture (RcyCastle_...)."""
    for name, (fbx, slot) in CASTLE_SOURCES.items():
        castle_source(name).data.materials[0].name = slot


def part(src, name, keep=None, shear=None, origin=None, ratio=1.0, cuts=(), close=False, crumbs=0.0):
    """A copy of the faces of `src` whose centre passes `keep`, sheared, moved by -origin and decimated to `ratio`,
    then cut straight by `cuts` ((point, normal) planes, after the move: what lies on the normal's side goes).
    close: every opening left after the cuts is capped (R680 QA: where two cuts meet at a corner the per-plane caps
    failed and the piece stayed open; the balustrade's far end showed it toward the section exit)."""
    bm = bmesh.new()
    bm.from_mesh(src.data)
    # The sources come split along their UV seams (every one closes at 1e-5 welded, R680): decimated split, each side of
    # a seam went its own way and hairline cracks opened along the stones' joints, the piers' edges and the copings.
    # Welded first (the seams' UVs stay on the corners), the decimation keeps them shut.
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    if keep is not None:
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f.calc_center_median())], context="FACES")
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    if shear is not None:
        for v in bm.verts:
            v.co = shear(v.co)
    if origin is not None:
        o = origin(bm) if callable(origin) else Vector(origin)
        for v in bm.verts:
            v.co -= o
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(src.data.materials[0])
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    collection("RCY Castle parts (not exported)", hide_render=True).objects.link(ob)
    if ratio < 1.0:
        mod = ob.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = ratio
        bpy.context.view_layer.update()
        low = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        ob.modifiers.clear()
        ob.data = low
        bpy.data.meshes.remove(me)
        low.name = name
    if cuts or close:   # R682: close works without cuts too
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        for co, no in cuts:
            co, no = Vector(co), Vector(no)
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no,
                                   clear_outer=True)
            # Capped, facing the cut-away side: where the next piece is narrower the opening showed the sky. The
            # source's faces meet at its corners without sharing vertices, so the rim is welded first to close.
            def on_plane(e):
                return e.is_boundary and all(abs((v.co - co).dot(no)) < 1e-5 for v in e.verts)
            bmesh.ops.remove_doubles(bm, verts=list({v for e in bm.edges if on_plane(e) for v in e.verts}), dist=5e-4)
            rim = [e for e in bm.edges if on_plane(e)]
            for f in bmesh.ops.holes_fill(bm, edges=rim, sides=0)["faces"]:
                f.smooth = False    # R682: shaded flat, so the smooth faces round the rim keep their own normals
                f.normal_update()
                if f.normal.dot(Vector(no)) < 0:
                    f.normal_flip()
        if close:
            rim = [e for e in bm.edges if e.is_boundary]
            bmesh.ops.remove_doubles(bm, verts=list({v for e in rim for v in e.verts}), dist=5e-4)
            caps = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)["faces"]
            # Concave caps (22-82 sides) ear-clipped here rather than left to Unity's importer.
            tri = bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")["faces"]
            # R682: flat, as the cuts' caps: shaded smooth they pulled the step's nosing normals down (dark streaks)
            for f in tri:
                f.smooth = False
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(ob.data)
        bm.free()
    if crumbs > 0:
        drop_crumbs(ob, crumbs)
    return ob


def drop_crumbs(ob, size):
    """R682: every loose island of `ob` smaller than `size` across (its box's diagonal) deleted - the balustrade part
    kept a 40-face chip of the stair's stringer by its foot (picked by its centre, 20 cm a copy once scaled, lying on
    the floor beside the first baluster on all ten railings)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    seen, drop = set(), []
    for f0 in bm.faces:
        if f0 in seen:
            continue
        comp, stack = [], [f0]
        seen.add(f0)
        while stack:
            f = stack.pop()
            comp.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g not in seen:
                        seen.add(g)
                        stack.append(g)
        vs = {v for f in comp for v in f.verts}
        lo = Vector([min(v.co[k] for v in vs) for k in range(3)])
        hi = Vector([max(v.co[k] for v in vs) for k in range(3)])
        if (hi - lo).length < size:
            drop += comp
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    return len(drop)


def base_centre(bm):
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    return Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(v.co.z for v in bm.verts)))


def place(col, piece, name, at, scale, turn=0.0):
    """A copy of `piece` standing at `at`, scaled (x, depth, up) and turned `turn` degrees about z."""
    ob = bpy.data.objects.new(name, piece.data.copy())
    col.objects.link(ob)
    ob.matrix_world = (Matrix.Translation(Vector(at)) @ Matrix.Rotation(math.radians(turn), 4, "Z")
                       @ Matrix.Diagonal(Vector(tuple(scale) + (1.0,))))
    return ob


# ------------------------------------------------------------------ the boxes past the blockout's colliders


def box_rec(name, a, b):
    """An axis-aligned box in Unity's map frame from two Blender corners: centre, size, yaw 0."""
    lo = Vector([min(a[k], b[k]) for k in range(3)])
    hi = Vector([max(a[k], b[k]) for k in range(3)])
    c, s = (lo + hi) / 2, hi - lo
    return {"name": name, "center": [round(c.x, 3), round(c.z, 3), round(c.y, 3)],
            "size": [round(s.x, 3), round(s.z, 3), round(s.y, 3)], "yaw": 0.0, "tilt": 0.0}


def box_face(name, p, q, floor, top, depth=1.0):
    """A box `depth` deep behind the face from p to q (Blender x, y; its front to the right of p -> q), floor .. top."""
    p, q = Vector(p), Vector(q)
    e = q - p
    n = Vector((e.y, -e.x)).normalized()
    mid = (p + q) / 2 - n * (depth / 2)
    return {"name": name, "center": [round(mid.x, 3), round((floor + top) / 2, 3), round(mid.y, 3)],
            "size": [round(e.length, 3), round(top - floor, 3), depth],
            "yaw": round(math.degrees(math.atan2(-e.y, e.x)), 2), "tilt": 0.0}


def wing_boxes(name, s, x0, x1, back, face, piers, floor, top, plinth=None):
    """A wall segment run (side s) whose face is set back between piers, as it was measured in Unity (R678): its body
    from `back` to `face[0]` over x0 .. x1, and each pier's shaft (x a .. b) out to `face[1]`. With `plinth` (height,
    wider by, front), as measured in R679, each pier stands on a plinth up to floor + height, that much wider each side
    (not past x1) and out to `front`; the shaft's box starts on it."""
    boxes = [box_rec(name, (s * x0, back, floor), (s * x1, face[0], top))]
    for a, b in piers:
        z0 = floor
        if plinth:
            high, wider, front = plinth
            boxes.append(box_rec(name + " pier plinth", (s * (a - wider), face[0], floor),
                                 (s * min(b + wider, x1), front, floor + high)))
            z0 = floor + high
        boxes.append(box_rec(name + " pier", (s * a, face[0], z0), (s * b, face[1], top)))
    return boxes


def straighten_jamb(ob, half, y0, y1, lintel, reach=1.2, heights=(9.0, 12.0)):
    """The passage's jambs of `ob` (map frame) put on x = +-half from y0 to y1 (Blender y). The final gatehouse's
    passage splays (4.7 at its face to 4.0 at its back once fitted): beside the push gate's 8 m door that left a slot
    to see through, and the door's frame (the collider) short of the stone. Each vertex near a jamb moves in by how
    far that jamb stands out there, fading out `reach` into the wall, in front of the face and above the lintel;
    inside the opening x scales, so the middle stays where it is."""
    bpy.context.view_layer.update()
    M = ob.matrix_world.copy()
    Mi = M.inverted()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.transform(M)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    step = .05
    ys = [y0 + i * step for i in range(int(round((y1 - y0) / step)) + 1)]

    def measured(y, side):
        rs = [abs(h.x) for h in (tree.ray_cast(Vector((0.0, y, z)), Vector((side, 0.0, 0.0)), 8.0)[0] for z in heights) if h is not None]
        return min(max(min(rs), half), half + .8) if rs else half

    table = {side: [measured(y, side) for y in ys] for side in (-1, 1)}

    def jamb(y, side):
        f = min(max((y - y0) / step, 0.0), len(ys) - 1.0)
        i = min(int(f), len(ys) - 2)
        t = f - i
        return table[side][i] * (1 - t) + table[side][i + 1] * t

    moved = 0
    for v in ob.data.vertices:
        w = M @ v.co
        if not (y0 - .3 <= w.y <= y1 + .05) or w.z > lintel + .7:
            continue
        side = 1 if w.x >= 0 else -1
        j = jamb(w.y, side)
        dx = (j - half) * min(1.0, (w.y - (y0 - .3)) / .3) * (1.0 if w.z <= lintel else 1 - (w.z - lintel) / .7)
        if dx <= 1e-4:
            continue
        ax = abs(w.x)
        if ax <= j:
            ax *= (j - dx) / j
        elif ax - j < reach:
            ax -= dx * (1 - (ax - j) / reach)
        else:
            continue
        w.x = side * ax
        v.co = Mi @ w
        moved += 1
    ob.data.update()
    return moved, round(max(table[-1] + table[1]) - half, 3)


def stretch_above(ob, z0, k, ramp):
    """Everything of `ob` (map frame) above world height z0 stretched `k` times up, easing in over `ramp` m (R679).
    The final gatehouse was fitted at 16/27.4 of its width high to bring its passage to 7.3 m; above the lintel that
    squashed its towers' shafts and crowns and the crest. The factor rises 1 .. k by smoothstep, so the stone courses
    do not jump in height at z0."""
    bpy.context.view_layer.update()
    M = ob.matrix_world.copy()
    Mi = M.inverted()

    def lift(z):
        t = z - z0
        if t <= 0:
            return z
        if t >= ramp:
            return z0 + t + (k - 1) * (t - ramp / 2)
        u = t / ramp
        return z0 + t + (k - 1) * ramp * (u ** 3 - u ** 4 / 2)   # the integral of smoothstep

    top = 0.0
    for v in ob.data.vertices:
        w = M @ v.co
        w.z = lift(w.z)
        top = max(top, w.z)
        v.co = Mi @ w
    ob.data.update()
    return round(top, 2)


def _tree(ob):
    """A BVH of `ob` in the map frame."""
    bpy.context.view_layer.update()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.transform(ob.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def _radius(tree, centre, d, z):
    """How far from `centre` (Blender x, y) a ray from 7 m out along bearing `d` at height z meets the stone."""
    hit = tree.ray_cast(Vector((centre.x, centre.y, z)) + d * 7.0, -d, 7.0)[0]
    return None if hit is None else (Vector((hit.x, hit.y)) - centre).length


def _bearing(deg):
    a = math.radians(deg)
    return Vector((math.cos(a), math.sin(a), 0.0))


def tower_circle(gatehouse, near, out, floor, band, arc=80):
    """The band round a tower's foot as a circle, (centre, radius) in Blender x, y: rays at the band's two heights
    (`band` above floor, clear of the studs) towards `near` every 10 degrees within `arc` of the bearing `out` (away
    from the gatehouse), the least-squares circle through the hits. The towers' axes are not where the old boxes
    took them (R678 took .355 and .335 of the width): the outer's band is centred 17 cm nearer the passage, the
    final's 49 cm farther out."""
    import numpy
    tree = _tree(gatehouse)
    near = Vector(near)
    pts = []
    for k in range(-arc, arc + 1, 10):
        d = _bearing(out + k)
        for h in band:
            hit = tree.ray_cast(Vector((near.x, near.y, floor + h)) + d * 7.0, -d, 7.0)[0]
            if hit is not None:
                pts.append((hit.x, hit.y))
    A = numpy.array([[x, y, 1.0] for x, y in pts])
    b = numpy.array([-(x * x + y * y) for x, y in pts])
    D, E, F = numpy.linalg.lstsq(A, b, rcond=None)[0]
    c = Vector((-D / 2, -E / 2))
    return c, math.sqrt(c.x * c.x + c.y * c.y - F)


def flatten_studs(gatehouse, centre, out, floor, band, studs, slack=.05, cap=.45, arc=165):
    """The bronze studs on a tower's band pressed in to `slack` proud of it (R679): they stood 20 to 38 cm out of the
    band, the boxes' face, so a pawn at the tower went into them. Each vertex between the `studs` heights (above
    floor) within `arc` degrees of `out`, more than `slack` but less than `cap` out of the band on its bearing (the
    farther of the rays at the `band` heights, above and below the studs), moves in along the bearing."""
    tree = _tree(gatehouse)
    M = gatehouse.matrix_world.copy()
    Mi = M.inverted()
    c = Vector(centre)
    bands = {}
    moved = 0
    for v in gatehouse.data.vertices:
        w = M @ v.co
        if not (floor + studs[0] <= w.z <= floor + studs[1]):
            continue
        q = Vector((w.x - c.x, w.y - c.y))
        deg = math.degrees(math.atan2(q.y, q.x))
        if abs((deg - out + 180.0) % 360.0 - 180.0) > arc:
            continue
        key = round(deg * 2) / 2
        if key not in bands:
            rs = [r for r in (_radius(tree, c, _bearing(key), floor + h) for h in band) if r is not None]
            bands[key] = max(rs) if rs else None
        r0 = bands[key]
        if r0 is None or not (slack < q.length - r0 < cap):
            continue
        q *= (r0 + slack) / q.length
        w.x, w.y = c.x + q.x, c.y + q.y
        v.co = Mi @ w
        moved += 1
    gatehouse.data.update()
    return moved


def tower_boxes(name, gatehouse, centre, radius, floor, top, tiers, sides=32, depth=1.0, band=(.2, 1.3), faces=()):
    """A tower of `gatehouse` as boxes round `centre` (its band's axis, Blender x, y; `radius` the band's), `sides` to
    the ring. Where the tower stands free (the rays at both `band` heights find the band, and the stone just above it
    steps in from it), each edge has a box per tier of `tiers` ((from, to, leaning) above floor): at the outermost of the stone's radii
    between the tier's heights on the edge's two bearings, the leaning ones along the line through them (R679: the
    stone narrows as it rises; one upright ring at the band had stood 15 to 30 cm off it at head height, 80 cm at
    7 m). Edges into the gatehouse's block get none when `faces` ((p, q, open side), Blender x, y) gives the block's
    faces the tower meets, but a box per tier from the tower to where the face ends; without them one upright box
    from floor to top at the middle of the radii at six heights to 2 m, as R678's ring was. Each box: centre and
    size in Unity's map frame, yaw about y, and tilt (towards the tower) about the box's x."""
    tree = _tree(gatehouse)
    c = Vector(centre)
    dirs = [_bearing(360.0 * i / sides) for i in range(sides)]
    free = []
    above = floor + tiers[1][0] + .3
    for d in dirs:
        rs = [_radius(tree, c, d, floor + h) for h in band]
        stone = _radius(tree, c, d, above)
        # The band, and the stone above it stepped in from it: the tower (it is no circle: it swells towards the
        # passage, 3.5 to 3.9 m on the outer, where it meets the jambs, which stand straight)
        free.append(all(r is not None for r in rs) and stone is not None and .08 < max(rs) - stone < 1.0
                    and max(rs) < radius + 1.0)

    def middle(d):
        rs = sorted(r for r in (_radius(tree, c, d, floor + .1 + 1.9 * k / 5) for k in range(6)) if r is not None)
        return (rs[(len(rs) - 1) // 2] + rs[len(rs) // 2]) / 2 if rs else 0.0

    def line(d, z0, z1, lean):
        hs, rs = [], []
        h = z0 + .08
        while h <= z1 - .05 + 1e-6:
            r = _radius(tree, c, d, floor + h)
            if r is not None:
                hs.append(h)
                rs.append(r)
            h += .05
        b = 0.0
        if lean and len(hs) > 1:
            mh, mr = sum(hs) / len(hs), sum(rs) / len(rs)
            b = sum((x - mh) * (y - mr) for x, y in zip(hs, rs)) / sum((x - mh) ** 2 for x in hs)
        a = max(y - b * x for x, y in zip(hs, rs))      # out to the outermost
        return a + b * z0, a + b * z1

    def box(p0, p1, q0, q1):
        # p: the bottom points (x, y, z) on the edge's bearings, q: the top ones (anticlockwise)
        e = p1 - p0
        length = max(e.length, (q1 - q0).length)
        e.normalize()
        m0, m1 = (p0 + p1) / 2, (q0 + q1) / 2
        u = m1 - m0
        u -= e * u.dot(e)
        high = u.length
        u.normalize()
        n = e.cross(u)
        if n.dot(Vector((m0.x - c.x, m0.y - c.y, 0.0))) < 0:
            n = -n                                        # outward
        mid = m0 + u * (high / 2) - n * (depth / 2)
        w = Vector((-e.y, e.x, 0.0))                      # inward (the box's z in Unity): the ring runs anticlockwise
        return {"name": name, "center": [round(mid.x, 3), round(mid.z, 3), round(mid.y, 3)],
                "size": [round(length + .04, 3), round(high, 3), depth],
                "yaw": round(math.degrees(math.atan2(-e.y, e.x)), 2),
                "tilt": round(math.degrees(math.atan2(u.dot(w), u.z)), 2)}

    def at(d, r, z):
        return Vector((c.x + d.x * r, c.y + d.y * r, z))

    def junction(face, z):
        # The last point along `face` (p -> q, from the passage into the tower; its open side `o`; optionally the
        # height it stops at, the outer recesses' the passage's top) still on it at height z: past it the tower's
        # flank stands in front of it (the joints between its courses, behind it, do not stop the search).
        p, q, o = Vector(face[0]), Vector(face[1]), Vector(face[2]).normalized()
        if len(face) > 3:
            z = min(z, face[3])
        e = (q - p).normalized()
        last, t = p, 0.0
        while t <= (q - p).length:
            pt = p + e * t
            hit = tree.ray_cast(Vector((pt.x + o.x * .6, pt.y + o.y * .6, z)), Vector((-o.x, -o.y, 0.0)), 1.2)[0]
            if hit is not None and (Vector((hit.x, hit.y)) - pt).dot(o) > .06:
                break
            last, t = pt, t + .02
        return last

    lines = [[line(d, z0, z1, lean) for z0, z1, lean in tiers] if free[i] else None for i, d in enumerate(dirs)]
    olds = {}
    boxes = []
    for i in range(sides):
        j = (i + 1) % sides
        if lines[i] and lines[j]:
            for k, (z0, z1, lean) in enumerate(tiers):
                (a0, a1), (b0, b1) = lines[i][k], lines[j][k]
                boxes.append(box(at(dirs[i], a0, floor + z0), at(dirs[j], b0, floor + z0),
                                 at(dirs[i], a1, floor + z1), at(dirs[j], b1, floor + z1)))
        elif faces:
            # Where the tower meets a face of the gatehouse's block (`faces`, whose own boxes run into the stone to
            # meet it), each tier's box from the last free bearing to where that face ends at its heights (R679:
            # the upright box from the bearing into the block cut the corner, 18 to 41 cm in front of the stone).
            f = i if lines[i] else j if lines[j] else None
            if f is None:
                continue
            mid = floor + (tiers[0][0] + tiers[-1][1]) / 2
            ra = lines[f][len(tiers) // 2][0]
            face = min(faces, key=lambda fc: (junction(fc, mid) - at(dirs[f], ra, mid).to_2d()).length)
            for k, (z0, z1, lean) in enumerate(tiers):
                pf0, pf1 = at(dirs[f], lines[f][k][0], floor + z0), at(dirs[f], lines[f][k][1], floor + z1)
                j0, j1 = junction(face, floor + z0 + .08), junction(face, floor + z1 - .05)
                j0, j1 = Vector((j0.x, j0.y, floor + z0)), Vector((j1.x, j1.y, floor + z1))
                boxes.append(box(pf0, j0, pf1, j1) if f == i else box(j0, pf0, j1, pf1))
        else:
            for k in (i, j):
                if k not in olds:
                    olds[k] = middle(dirs[k])
            boxes.append(box(at(dirs[i], olds[i], floor), at(dirs[j], olds[j], floor),
                             at(dirs[i], olds[i], top), at(dirs[j], olds[j], top)))
    return boxes, sum(free)


# ------------------------------------------------------------------ build


def close_wall_ends(ob, inner=.095, end=.1275, past=.0003):
    """The wall part ends in half piers, so two segments end to end make one pier; but its shaft and its parapet end
    at x +-.1285 .. .1324, short of the +-.1409 its plinth and cornice reach (whose own end faces are open and ragged):
    where two segments met that left a 0.4 m slit through the pier, sky showing through from 1 to 5.4 m (R679). Each
    end's last stretch, from `inner` (inside the half pier's front, its side is at .088) to `end`, is drawn out at
    every height to a hair (`past`) beyond the plinth's end, and everything past `end` put flat there, so the two
    segments overlap inside the pier instead of leaving a seam to see through."""
    vs = ob.data.vertices
    for side in (-1, 1):
        full = max(side * v.co.x for v in vs) + past
        for v in vs:
            d = side * v.co.x
            if d >= end:
                v.co.x = side * full
            elif d > inner:
                v.co.x = side * (inner + (d - inner) * (full - inner) / (end - inner))
    ob.data.update()
    return full


def straighten_end_normals(ob, inner=.095, cap=.9):
    """The half piers' faces at the part's ends kept the asset's normals bending round its rounded corners, so two
    segments end to end showed a crease down the pier between them (R680): past `inner` every corner's normal loses
    its pull along x, but on the ends' own faces (more than `cap` along x)."""
    me = ob.data
    out = []
    for p in me.polygons:
        for i in p.loop_indices:
            n = me.corner_normals[i].vector.copy()
            if abs(me.vertices[me.loops[i].vertex_index].co.x) > inner and abs(n.x) < cap:
                n.x = 0.0
                n.normalize()
            out.append(n)
    me.normals_split_custom_set(out)
    me.update()


def mirror_part(ob, name):
    """`ob` mirrored across x into a new part: its faces turned back out and its corner normals mirrored with it."""
    src = ob.data
    normals = {(p.index, src.loops[i].vertex_index): src.corner_normals[i].vector.copy()
               for p in src.polygons for i in p.loop_indices}
    me = src.copy()
    me.name = name
    for v in me.vertices:
        v.co.x = -v.co.x
    me.flip_normals()
    me.normals_split_custom_set([(-n.x, n.y, n.z) for n in
                                 (normals[(p.index, me.loops[i].vertex_index)] for p in me.polygons for i in p.loop_indices)])
    me.update()
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    m = bpy.data.objects.new(name, me)
    collection("RCY Castle parts (not exported)", hide_render=True).objects.link(m)
    return m


def build_castle():
    castle_materials()
    gh_outer = part(castle_source("AI outer gatehouse"), "Outer gatehouse part", ratio=.5)
    gh_final = part(castle_source("AI final gatehouse"), "Final gatehouse part", ratio=.5)
    wall = part(castle_source("AI walls"), "Tall wall part", keep=lambda c: c.x < -.1, origin=base_centre, ratio=.3)
    close_wall_ends(wall)
    straighten_end_normals(wall)
    # R680: the part's two ends are not alike (their blocks at other heights, their normals bent their own ways), so
    # where two copies met the pier between them showed a seam down its middle, one half lit and the other dark. The
    # outer segment of each side is the part mirrored: every joint is then one end against its own mirror image.
    wall_m = mirror_part(wall, "Tall wall part (mirrored)")

    def wall_for(s, inner):
        return wall if (s > 0) == inner else wall_m
    tower = part(castle_source("AI towers"), "Slim tower part", keep=lambda c: abs(c.x) < .2, origin=base_centre, ratio=.45)
    arch = castle_source("AI section end")

    def pier(c):
        return abs(c.x) > .17 and c.y > .19

    def gateway_face(c):
        # The piers from above the balustrade's end (where it ran into them their north faces are open, and their
        # brass feet stood on the landing), the beam and its crown.
        return (pier(c) and c.z > PIER_CUT - .02) or c.z > .5

    def balustrade_face(c):
        return c.x > .262 and c.y < .19 and c.z > .09 + .08 * (c.y + .5) and not pier(c)

    # Both cut straight at PIER_CUT (R679: picking faces by their centres left torn edges with sky through them).
    gateway = part(arch, "Gateway part", keep=gateway_face, ratio=.5, cuts=(((0, 0, PIER_CUT), (0, 0, -1)),))
    # A plain course of both piers (z PIER_CUT .. +.12, their block shafts), stacked edge to edge under them down to
    # the ground.
    pier_course = part(arch, "Pier course part", keep=lambda c: pier(c) and PIER_CUT - .02 < c.z < PIER_CUT + .14,
                       origin=(0.0, 0.0, PIER_CUT), ratio=.5,
                       cuts=(((0, 0, 0), (0, 0, -1)), ((0, 0, PIER_COURSE + .0005), (0, 0, 1))))
    # The right-hand balustrade, its slope (handrail and stringer, ~.06 a unit) taken out, its inner face (x .265),
    # front end (y -.5) and foot at the origin: a straight run with its newel at the start. Its foot (torn up to .04
    # where the stair's stringer was picked off) and its far end (torn at .69, where it met the pier) cut straight.
    rail = part(arch, "Balustrade part", keep=balustrade_face, shear=lambda v: Vector((v.x, v.y, v.z - .06 * (v.y + .5))),
                origin=lambda bm: Vector((.265, -.5, min(v.co.z for v in bm.verts) + .04)), ratio=RAIL_RATIO,
                cuts=(((0, 0, 0), (0, 0, -1)), ((0, .68, 0), (0, 1, 0))), close=True, crumbs=.08)
    rail_len = max(v.co.y for v in rail.data.vertices)
    rail_high = max(v.co.z for v in rail.data.vertices)
    # Left open (R682 tried cutting and closing it: the cap of its one open rim - the tread's nose and the riser's top
    # in a single loop - lay over the tread as a lid 13 - 18 cm above the code's steps, where a pawn's feet sank into
    # it; the open plates' ends are inside the balustrades and seen from nowhere).
    stair = part(arch, "Step part", keep=STEP["keep"], origin=STEP["origin"], ratio=.5)
    boxes = []

    # --- the outer gate
    col = collection("RCY Castle outer gate")
    clear(col)
    pieces = [place(col, gh_outer, "Outer gatehouse", OUTER["at"], OUTER["scale"])]
    for s in (-1, 1):
        for x0, x1 in ((9.0, 16.0), (16.0, 23.0)):
            # 2.76 deep about z 1.2: the segment's body (its faces set back between piers) from z .4 to 2.0, where the
            # yard's floor starts (there is none under the blockout's 2 m wall past the court's x +-12); its piers out
            # to 2.54 on the yard, -.18 behind the court's wall.
            pieces.append(place(col, wall_for(s, x0 < 10), "Gate wall", (s * (x0 + x1) / 2, 1.2, 0.0),
                                ((x1 - x0) / .282, 2.76 / .118, 7 / .304)))
    bpy.context.view_layer.update()
    cx = .355 * OUTER["scale"][0]
    for s in (-1, 1):
        out = 0.0 if s > 0 else 180.0
        centre, radius = tower_circle(pieces[0], (s * cx, OUTER["at"][1]), out, 0.0, OUTER_TOWER["band"])
        studs = flatten_studs(pieces[0], centre, out, 0.0, OUTER_TOWER["band"], OUTER_TOWER["studs"])
        ring, free = tower_boxes("Outer gate tower", pieces[0], centre, radius, 0.0, 7.0, OUTER_TOWER["tiers"],
                                  band=OUTER_TOWER["band"], faces=(((s * 3.3, 3.07), (s * 5.8, 3.07), (0.0, 1.0), 5.5),
                                                                   ((s * 3.3, .485), (s * 5.8, .485), (0.0, -1.0), 5.5)))
        boxes += ring
        print("outer tower", s, "axis", tuple(round(v, 3) for v in centre), "band", round(radius, 3), "studs moved",
              studs, "free bearings", free, "boxes", len(ring))
        # The passage's jamb where it is 6 m (z .5 .. 3.05): at both its mouths the stone steps back to x +-4.4 for
        # .5 m (the frame's recesses, wider as the towers narrow above), which stay open: the box runs on into the
        # stone to x +-5.2 to meet the towers' boxes where their flanks meet the recesses' back faces (4.4 .. 5.1 up).
        boxes.append(box_rec("Outer gate passage", (s * 3.0, .5, 0.0), (s * 5.2, 3.05, 7.0)))
        # The gate wall's segments out to x +-23 (the blockout's wall stops colliding): their yard face at z 2.0 between
        # piers that come out to 2.54 (round x 16, where two segments meet, and at the end); the court sees only
        # x 11.5 .. 12 of it (its face there at z .4). From x 10.4: above the band the wall comes out of the narrowing
        # tower from there.
        # The piers' shafts (R679, measured in Blender once their segments met; R680 again with the outer segment
        # mirrored): x 14.68 .. 17.32 out to 2.40, on plinths to .95 m (below their tops' bevel), 14.47 .. 17.53 out
        # to 2.57.
        boxes += wing_boxes("Outer gate wall", s, 10.4, 23.0, .4, (2.0, 2.40), ((14.68, 17.32), (21.70, 23.0)), 0.0, 7.0,
                            plinth=(1.02, .21, 2.57))
    # --- the final gate
    fcol = collection("RCY Castle final gate")
    clear(fcol)
    fpieces = [place(fcol, gh_final, "Final gatehouse", FINAL["at"], FINAL["scale"])]
    moved, splay = straighten_jamb(fpieces[0], 4.0, 155.05, 159.25, 15.3)
    print("final jambs straightened", moved, "vertices, splay was", splay)
    fx = .335 * FINAL["scale"][0]
    towers = []
    for s in (-1, 1):
        out = 0.0 if s > 0 else 180.0
        centre, radius = tower_circle(fpieces[0], (s * fx, FINAL["at"][1]), out, 8.0, FINAL_TOWER["band"])
        studs = flatten_studs(fpieces[0], centre, out, 8.0, FINAL_TOWER["band"], FINAL_TOWER["studs"])
        towers.append((s, centre, radius, studs))
    print("final gatehouse top now", stretch_above(fpieces[0], *FINAL_STRETCH))
    for s in (-1, 1):
        for x0, x1 in ((10.0, 18.0), (18.0, 26.0)):
            fpieces.append(place(fcol, wall_for(s, x0 < 11), "Gate wall", (s * (x0 + x1) / 2, 156.0, 8.0),
                                 ((x1 - x0) / .282, 2 / .118, 11 / .304)))
    bpy.context.view_layer.update()
    for s, centre, radius, studs in towers:
        ring, free = tower_boxes("Final gate tower", fpieces[0], centre, radius, 8.0, 19.0, FINAL_TOWER["tiers"],
                                  band=FINAL_TOWER["band"], faces=(((s * 4.4, 155.02), (s * 7.0, 155.02), (0.0, -1.0)),
                                                                   ((s * 6.0, 159.18), (s * 7.22, 158.59), (s * .435, .9))))
        boxes += ring
        print("final tower", s, "axis", tuple(round(v, 3) for v in centre), "band", round(radius, 3), "studs moved",
              studs, "free bearings", free, "boxes", len(ring))
    for s in (-1, 1):
        # The passage's jamb (straightened onto x +-4) between the chamfers at both its mouths (R679: its box had
        # stood 25 cm out of the front ones): the front chamfer (4.0, 155.27) .. (4.3, 155.04), then the gatehouse's
        # face at 155.02; the back one (4.0, 159.22) .. (4.27, 159.48), then the far face, which runs from 159.49 at
        # x 4.35 to 159.18 at 6.0 and then bends into the tower's back (below). The chamfers' and the far face's boxes
        # keep their turned ends out of the passage.
        boxes.append(box_rec("Final gate passage", (s * 4.0, 155.27, 8.0), (s * 6.0, 159.22, 19.0)))
        boxes.append(box_rec("Final gate passage (face)", (s * 4.3, 155.03, 8.0), (s * 7.0, 155.6, 19.0)))
        for p, q in (((4.0, 155.27), (4.3, 155.04)), ((4.27, 159.48), (4.0, 159.22))):
            p, q = (s * p[0], p[1]), (s * q[0], q[1])
            boxes.append(box_face("Final gate passage (chamfer)", *((p, q) if s > 0 else (q, p)), 8.0, 19.0, depth=.5))
        # The far face bends at x 6.0 into the corner with the tower (measured R679: from 159.18 there to 158.6 at
        # 7.2, where the tower's flank stands from 10.1 m up; below that the tower's foot stands in front of it).
        for p, q in (((s * 4.35, 159.49), (s * 6.0, 159.18)), ((s * 6.0, 159.18), (s * 7.22, 158.59))):
            boxes.append(box_face("Final gate passage (far face)", *((p, q) if s < 0 else (q, p)), 8.0, 19.0))
        # The gate wall's segments out to x +-26, as the outer gate's: their platform face set back to z 155.4
        # between piers (round x 18 and at the end); behind them only the finish court's walls. From x 12: above the
        # batter the wall comes out of the narrowing tower from there. The piers' shafts (R679, R680): x 16.48 .. 19.52 out
        # to 155.12, on plinths to 9.5 (below their tops' bevel), 16.25 .. 19.73 out to 155.01.
        boxes += wing_boxes("Final gate wall", s, 12.0, 26.0, 157.0, (155.4, 155.12), ((16.48, 19.52), (24.54, 26.0)), 8.0,
                            19.0, plinth=(1.5, .24, 155.01))
    # --- the corner towers (the scenery's: pictures, nothing to touch)
    for which, at, d, h in TOWER_SPOTS:
        c, ps = (col, pieces) if which == "outer" else (fcol, fpieces)
        ps.append(place(c, tower, "Corner tower", at, (d / .115, d / .115, h / .258)))
    # --- the section end
    scol = collection("RCY Castle section end")
    clear(scol)
    spieces = [place(scol, gateway, "Gateway", GATEWAY["at"], GATEWAY["scale"], turn=180.0)]
    sx, sy, sz = GATEWAY["scale"]
    cut = GATEWAY["at"][2] + sz * PIER_CUT
    for i in range(1, 7):
        spieces.append(place(scol, pier_course, "Pier course", (GATEWAY["at"][0], GATEWAY["at"][1], cut - i * sz * PIER_COURSE), GATEWAY["scale"], turn=180.0))
    k = 1.15 / rail_high            # 1.15 m high (its boxes 1.1)
    kx = 1.15 / (rail_high + .04)   # as thick as before its foot was cut (R679), the balusters not fattened
    # The runout's railing from the gatehouse to the piers' south faces, the band's from their north faces (z 175.4).
    for y0, y1, foot, n in ((159.5, GATEWAY["at"][1] - .459 * sy, 8.0, 3), (GATEWAY["at"][1] - .254 * sy, 181.0, 7.0, 2)):
        step = (y1 - y0) / n
        for i in range(n):
            a = y0 + i * step
            spieces.append(place(scol, rail, "Balustrade", (6.0, a, foot), (kx, step / rail_len, k)))
            spieces.append(place(scol, rail, "Balustrade", (-6.0, a + step, foot), (kx, step / rail_len, k), turn=180.0))
    st = STEPS
    # Each step in `across` copies side by side. One copy stretched over the 12 m smears its texture 8:1 across the
    # treads (R680 QA, uv-workflow), but three side by side showed dark cracks at their joints (the asset's step ends are
    # not straight), so it stays one.
    span = 2 * st["half"] / st["across"]
    fit = (span / STEP["width"], st["depth"] / STEP["depth"], st["rise"] / STEP["rise"])
    for i in range(st["count"]):
        for j in range(st["across"]):
            at = (-st["half"] + span * (j + .5), st["edge"] + i * st["depth"] + .01, st["top"] - (i + 1) * st["rise"])
            spieces.append(place(scol, stair, "Step", at, fit, turn=180.0))
    for s in (-1, 1):   # the piers above the railings (a pawn jumping there would go into them)
        boxes.append(box_rec("Section end pier", (s * 6.0, GATEWAY["at"][1] - .459 * sy, 7.0),
                             (s * .41 * sx, GATEWAY["at"][1] - .254 * sy, 20.0)))

    outer = join(col, "Castle outer gate", pieces)
    final = join(fcol, "Castle final gate", fpieces)
    section = join(scol, "Castle section end", spieces)
    with open(os.path.join(RCY_HERE, "guides", "rcy_castle_boxes.json"), "w", encoding="utf-8") as f:
        json.dump({"boxes": boxes}, f, indent=1)
    print("castle tris", tris(col), tris(fcol), tris(scol), "boxes", len(boxes), "rail", round(rail_len, 3), round(rail_high, 3))
    return outer, final, section


# ------------------------------------------------------------------ textures (after export_rcy.py)

CASTLE_ALBEDO_META = os.path.join(CASTLE_ROOT, "Assets/Resources/PawnRushGrandStairHall/FloatingPad_Side.png.meta")


def _castle_png(image, size, rel, normal):
    """`image` resized to `size` written to `rel` as PNG with a .meta (an albedo, or a normal map: sRGB off, type 1)."""
    c = image.copy()
    c.scale(size, size)
    px = [0.0] * (size * size * 4)
    c.pixels.foreach_get(px)
    out = bpy.data.images.new(os.path.basename(rel), size, size, alpha=True)
    out.colorspace_settings.name = image.colorspace_settings.name
    out.pixels.foreach_set(px)
    out.filepath_raw = os.path.join(CASTLE_ROOT, rel)
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(c)
    bpy.data.images.remove(out)
    with open(CASTLE_ALBEDO_META, encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines[1] = "guid: " + hashlib.md5(("chessfight:" + rel).encode()).hexdigest()
    # Compressed (the template's are not): ten 2048/1024 textures would hold some 100 MB uncompressed.
    text = "\n".join(lines).replace("    textureCompression: 0\n", "    textureCompression: 1\n")
    if not normal:
        # R680 QA (texture-workflow): the base colours are opaque (alpha 1 everywhere), so no alpha in the compressed
        # format: BC1 instead of BC3, half the memory.
        text = text.replace("  alphaUsage: 1\n", "  alphaUsage: 0\n")
    if normal:
        text = text.replace("sRGBTexture: 1", "sRGBTexture: 0").replace("  textureType: 0", "  textureType: 1")
    with open(os.path.join(CASTLE_ROOT, rel + ".meta"), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def castle_export(models=("RcyCastleOuterGate", "RcyCastleFinalGate", "RcyCastleSectionEnd"), textures=True):
    for col, name in (("RCY Castle outer gate", "RcyCastleOuterGate"), ("RCY Castle final gate", "RcyCastleFinalGate"),
                      ("RCY Castle section end", "RcyCastleSectionEnd")):
        if name not in models:
            continue
        rel = export(col, name)
        # Unity names the imported materials by their own names, not their textures' (every AI source's base colour
        # is a "base_color.png"): RoyalCarriageYard.Castle.cs finds the textures by the slot names.
        meta = os.path.join(CASTLE_ROOT, rel + ".meta")
        with open(meta, encoding="utf-8") as f:
            text = f.read()
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(text.replace("    materialName: 0\n", "    materialName: 1\n"))
    for name, (fbx, slot) in CASTLE_SOURCES.items() if textures else ():
        tree = castle_source(name).data.materials[0].node_tree
        bsdf = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
        base = bsdf.inputs["Base Color"].links[0].from_node.image
        nmap = bsdf.inputs["Normal"].links[0].from_node.inputs["Color"].links[0].from_node.image
        _castle_png(base, 2048, RCY_OUT_REL + "/" + slot + ".png", False)
        _castle_png(nmap, 1024, RCY_OUT_REL + "/" + slot + "_N.png", True)
        print("textures", slot)
    castle_sync_cs()


CASTLE_CS = os.path.join(CASTLE_ROOT, "Assets/Scripts/PawnRush/RoyalCarriageYard.Castle.cs")


def castle_sync_cs():
    """guides/rcy_castle_boxes.json written into RoyalCarriageYard.Castle.cs between its "// <castle boxes>" lines."""
    with open(os.path.join(RCY_HERE, "guides", "rcy_castle_boxes.json"), encoding="utf-8") as f:
        boxes = json.load(f)["boxes"]

    def num(v):
        s = ("%.3f" % v).rstrip("0").rstrip(".")
        return ("0" if s in ("-0", "") else s) + "f"

    out = ["        static class CastleBoxData", "        {"]
    for field, prefix in (("Outer", "Outer"), ("Final", "Final"), ("Section", "Section")):
        out += ["            public static readonly float[] " + field + " =", "            {"]
        last = None
        for b in boxes:
            if not b["name"].startswith(prefix):
                continue
            if b["name"] != last:
                out.append("                // " + b["name"])
                last = b["name"]
            out.append("                " + ", ".join(num(v) for v in b["center"] + b["size"] + [b["yaw"], b.get("tilt", 0.0)]) + ",")
        out += ["            };", ""]
    out[-1] = "        }"
    with open(CASTLE_CS, "rb") as f:
        crlf = b"\r\n" in f.read()
    with open(CASTLE_CS, encoding="utf-8") as f:
        text = f.read()
    head, rest = text.split("        // <castle boxes>\n", 1)
    _, tail = rest.split("        // </castle boxes>\n", 1)
    with open(CASTLE_CS, "w", encoding="utf-8", newline="\r\n" if crlf else "\n") as f:
        f.write(head + "        // <castle boxes>\n" + "\n".join(out) + "\n        // </castle boxes>\n" + tail)
    print("castle boxes into", os.path.basename(CASTLE_CS), len(boxes))


if globals().get("RCY_BUILD", True):
    castle = build_castle()
