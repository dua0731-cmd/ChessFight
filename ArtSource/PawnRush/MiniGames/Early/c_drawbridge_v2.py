# C · 해자 도개교 v2 (R502): the section laid out as the R311 concept (ConceptArt/R311/01_early_C_moat.png) with the
# user's own assets as the real models (user 11-07: "실제로 써야지 ... 구조물도", "사진이랑 배치랑 완전히 똑같이"):
#   gate     Source/R502_RoyalCastleGate_Open.blend  (the user's gatehouse, both side passages cut through, c_gate_open.py)
#   towers   Source/R502_CastleTower_Tripo.glb       (as is, with its own texture, gold finial included)
#   winches  Source/R502_WoodenGearWinch.fbx         (as is)
#   stone    Materials/StoneBlock_Module.blend       (the user's stone block in the tower's stone: floors, banks, walls)
# Gold follows the assets' own gold (user: "에셋 금색 색으로 맞춰"), sampled from the gate's texture.
# Parts the user gave no model for (bridge decks, pylons, chains, lanterns, rope, banners, planters, benches, props)
# are built here in the same materials.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_drawbridge_v2.py -- [--render]
#
# Writes C_Drawbridge_v2.blend; --render writes render_v2_main.png (the concept's view), render_v2_axo.png (its
# inset's view) and v2_compare.png (concept above, ours below).
#
# Game numbers kept (MiniGameSectionBuilder, MgDrawbridge): 44 x 36 m, entrance (0,0,0), exit (0,0,36); stations at
# x -12 (white) / +12 (black) from y 4; their floor 0.4 m over the plaza; moat y 16..20; deck 4 x 4.1 m hinged at
# y 19.95; team passages x 10..14 through the facade (y 24..30), 4 m wide, 4.6 m high; rope lane at x +-16,
# knots from y 14.2. Changed with the user's leave (step 5 moves the game to match): the winch stands at the front,
# beside the lane's south end (y 5.4, x +-14.1; the lane at x +-11, just inside the bridge as drawn), as in the concept, instead of by the moat; the pylons are 3.3 m.
# Frame: Blender X = section x, Y = section z (north), Z = up; plaza at 0.
import os, sys, math, random, json
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "Source")
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "Materials"))
import r502_tower_block as tb  # noqa: E402
import c_canal_wall_v1 as cw  # noqa: E402  (parts: chain, lantern, bench, planter, materials)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
CONCEPT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R311", "01_early_C_moat.png"))
random.seed(5025)

DAIS, WATER, MOAT_BOTTOM = .4, -.5, -1.2
MOAT0, MOAT1, HINGE = 15.5, 20.5, 20.45      # R502: the moat widened to 5 m (user 11-07, MgDrawbridge.PalaceMoat0/1)
DECK_LEN, DECK_W = 5.1, 4.
FACADE = 24.
REAR = 30.          # the game's rear corridor y 30..36 behind the gatehouse, the exit at x -6..6 (MiniGameSectionBuilder)
LANE_X, LANE_Y0, LANE_Y1 = 11., 4.2, 14.9
WINCH_X, WINCH_Y = 18.4, 5.4      # outside the bridge (x 10..14) and the knot line: MgDrawbridge 12 + PalaceWinchOut 6.4
ANCHOR_Y = MOAT0 - .5             # the pulley post's south face (MgDrawbridge.PalaceAnchorBack)
PULL_ASIDE = 2.4                  # the returning line, this far toward the bridge, clear of the winch (MgDrawbridge.PalacePulleyAside)
KNOTS = [14.2, 13.2, 12.2, 11.2]
PYLON_H = 3.3
MW, MD, MH = 1.0, .74, 1.093       # the stone block module


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def normalise(ob):
    """Applies the import's transforms and puts the model's bottom centre at the origin."""
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    if ob.parent:
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    vs = [v.co for v in ob.data.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)])
    hi = Vector([max(v[i] for v in vs) for i in range(3)])
    ob.data.transform(Matrix.Translation(Vector(((lo.x + hi.x) / -2, (lo.y + hi.y) / -2, -lo.z))))
    return hi - lo


def place(src, at, scale, yaw=0.):
    ob = link(bpy.data.objects.new(src.name, src.data))
    ob.location = at
    ob.scale = scale
    ob.rotation_euler = (0, 0, math.radians(yaw))
    return ob


# ------------------------------------------------------------------ the user's assets
def load_gate():
    with bpy.data.libraries.load(os.path.join(SRC, "R502_RoyalCastleGate_Open.blend")) as (src, dst):
        dst.objects = ["PR_Early_RoyalGate"]
    g = dst.objects[0]
    link(g)
    size = normalise(g)
    g.hide_render = True
    g.hide_set(True)
    return g, size


def load_glb(path, name):
    ob = tb.import_glb(path)
    ob.name = name
    size = normalise(ob)
    ob.hide_render = True
    ob.hide_set(True)
    return ob, size


WINCH_K = None   # the winch's scale: its drum's axis comes to the game's drum height (MgDrawbridge.DrumHeight 1.3 m)


def load_winch():
    """The user's winch split into its frame and its drum (c_winch_split.py); the game turns the drum."""
    global WINCH_K
    with bpy.data.libraries.load(os.path.join(SRC, "R502_Winch_Split.blend")) as (src, dst):
        dst.objects = ["WINCH_Frame", "WINCH_Drum"]
    frame, drum = dst.objects
    for ob in (frame, drum):
        link(ob)
        ob.hide_render = True
        ob.hide_set(True)
    frame.name, drum.name = "winch", "winch drum"
    WINCH_K = 1.3 / drum["axis"][2]
    print("WINCH scale %.3f (drum radius %.3f m)" % (WINCH_K, drum["radius"] * WINCH_K))
    return frame, drum


def place_winch(frame, drum, at, yaw):
    """The frame placed so the drum's axis is at `at` (the game's Drum), turned by yaw; the drum on its axis."""
    ax = Vector(drum["axis"]) * WINCH_K
    rot = Matrix.Rotation(math.radians(yaw), 3, "Z")
    place(frame, Vector(at) - rot @ ax, (WINCH_K, WINCH_K, WINCH_K), yaw)
    d = place(drum, at, (WINCH_K, WINCH_K, WINCH_K), 0.)
    d.name = "winch drum ride"
    return d


def load_fbx(path, name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    ob = [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]
    ob.name = name
    size = normalise(ob)
    ob.hide_render = True
    ob.hide_set(True)
    return ob, size


def asset_gold(gate):
    """The gate texture's own gold: the median of its saturated yellow texels."""
    img = [n.image for n in gate.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE" and n.image and "base" in n.image.name][0]
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    a = a.reshape(-1, 4)[::7, :3]
    m = (a[:, 0] > .55) & (a[:, 1] > .38) & (a[:, 2] < .32) & (a[:, 0] - a[:, 2] > .35)
    c = np.median(a[m], 0)
    print("GOLD (asset, sRGB) %.3f %.3f %.3f from %d texels" % (c[0], c[1], c[2], m.sum()))
    lin = [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]
    mat = bpy.data.materials.new("gold (asset)")
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*lin, 1.)
    b.inputs["Metallic"].default_value = .85
    b.inputs["Roughness"].default_value = .32
    return mat


# ------------------------------------------------------------------ stone block module placements
def mod(me, at, yaw=0., up=False, scale=(1, 1, 1)):
    """A module standing (front face at `at` facing -y turned by yaw, body behind) or laid face up (face at at.z,
    1.093 m along +y from at.y, body below)."""
    ob = link(bpy.data.objects.new("module", me))
    ob.location = at
    ob.scale = scale
    ob.rotation_euler = (-math.pi / 2, 0, 0) if up else (0, 0, math.radians(yaw))
    return ob


PAVER = 2.5     # the floors use the module this many times over: its bricks become the concept's big flagstones
WALL_K = 2.0    # banks and walls: bricks about the gatehouse's own
# The module's outer bricks are rounded and chipped, so modules butted edge to edge leave slits one can see through
# (user, Unity 11-07: "이런식으로 떨어져 있는 부분 최대한 고쳐"). Every module is drawn a little larger than its slot,
# so neighbours overlap: 6 % along a wall, 3 % up a course, 4 % on a floor.
OVER_W, OVER_H, OVER_F = 1.06, 1.03, 1.04


FLOOR_ME = {}     # the module mesh with the floor's own material (user 11-07: the floor in the screenshot's colour)


def floor_mesh(me):
    if me.name not in FLOOR_ME:
        f = me.copy()
        f.name = me.name + "_floor"
        m = me.materials[0].copy()
        m.name = "PR_Early_C_Floor"
        f.materials[0] = m
        FLOOR_ME[me.name] = f
    return FLOOR_ME[me.name]


def floor(me, x0, x1, y0, y1, z, k=PAVER):
    """Face-up modules filling the rectangle exactly (each stretched a little so whole ones fit)."""
    me = floor_mesh(me)
    nx = max(1, round((x1 - x0) / (MW * k)))
    ny = max(1, round((y1 - y0) / (MH * k)))
    sx, sy = (x1 - x0) / nx / MW, (y1 - y0) / ny / MH
    for j in range(ny):
        for i in range(nx):
            mod(me, (x0 + (i + .5) * sx * MW, y0 + j * sy * MH - sy * MH * (OVER_F - 1) / 2, z), up=True, scale=(sx * OVER_F, 1.0, sy * OVER_F))


def wall(me, x0, x1, y, z, yaw, courses=1, k=WALL_K):
    """Standing modules along x (yaw 0: face toward -y at y; yaw 180: face toward +y), k times the module."""
    n = max(1, round((x1 - x0) / (MW * k)))
    s = (x1 - x0) / n / MW
    for c in range(courses):
        for i in range(n):
            flip = (i + c) % 3 == 1
            d = MD * k
            yy = y + (d if (flip and yaw == 0) else 0) - (d if (flip and yaw == 180) else 0)
            mod(me, (x0 + (i + .5) * s * MW, yy, z + c * MH * k), yaw=yaw + (180 if flip else 0), scale=(s * OVER_W, k, k * OVER_H))


def side_wall(me, x, y0, y1, z, inward, courses=1, k=1.5):
    """Standing modules along y at x, face toward the section's middle (inward = +1 faces +x)."""
    yaw = 90 if inward > 0 else -90
    n = max(1, round((y1 - y0) / (MW * k)))
    s = (y1 - y0) / n / MW
    for c in range(courses):
        for i in range(n):
            mod(me, (x, y0 + (i + .5) * s * MW, z + c * MH * k), yaw=yaw, scale=(s * OVER_W, k, k * OVER_H))


def square_tower(me, cx, cy, w, z, courses, k=2.):
    """A crenellated square tower of modules (the sheet's left/right tower block), w metres a side."""
    h = w / 2
    for yaw, at in ((0, (cx, cy - h)), (180, (cx, cy + h)), (90, (cx - h, cy)), (-90, (cx + h, cy))):
        n = max(1, round(w / (MW * k)))
        s = w / n / MW
        for c in range(courses):
            for i in range(n):
                off = (i + .5) * s * MW - h
                if yaw in (0, 180):
                    p = (at[0] + off, at[1], z + c * MH * k)
                else:
                    p = (at[0], at[1] + off, z + c * MH * k)
                mod(me, p, yaw=yaw, scale=(s * OVER_W, min(k, w / 2 / MD), k * OVER_H))
    top = z + courses * MH * k
    mod(me, (cx, cy - h - .15, top), up=True, scale=((w + .3) / MW, .5, (w + .3) / MH))
    m = .7
    for i in range(4):
        for side in range(4):
            t = -h + .45 + i * (w - .9) / 3
            pos = [(cx + t, cy - h + .2), (cx + t, cy + h - .2 - m * MD), (cx - h + .2, cy + t), (cx + h - .2 - m, cy + t)][side]
            if i % 3 == 0 or side < 2:
                mod(me, (pos[0], pos[1], top + .1), scale=(m, m, m))
    return top


RAIL_K = 3.43     # the garden wall at 3.43 m a segment: 2.1 m to the planter's top, the 1 m pawn reaches the chains
RAIL_JOIN = .17   # neighbouring segments share their end pillar (about 17 % of the length)


def railing_run(rail, rsize, s, y0, y1, z):
    """Segments of the user's garden wall along the side wall at x = s * 22, front toward the middle. Neighbours share
    their end pillar; n segments are stretched a little along the run so they fill it exactly. No mirrored copies:
    a negative scale renders broken in Unity (user 11-07, "양 옆 사이드 난간 쪽은 깨졌어")."""
    L = rsize.y * RAIL_K
    depth = rsize.x * RAIL_K
    run = y1 - y0
    n = max(1, round((run - L * RAIL_JOIN) / (L * (1 - RAIL_JOIN))))
    stretch = run / (n * L * (1 - RAIL_JOIN) + L * RAIL_JOIN)
    Ls = L * stretch
    for i in range(n):
        c = y0 + Ls / 2 + i * Ls * (1 - RAIL_JOIN)
        place(rail, (s * (22. - depth / 2), c, z), (RAIL_K, RAIL_K * stretch, RAIL_K), 0 if s < 0 else 180)


# ------------------------------------------------------------------ parts
def box(name, lo, hi, mat, bevel=0.):
    lo, hi = Vector(lo), Vector(hi)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=hi - lo, verts=bm.verts)
    ob = cw.mesh_object(name, bm, mat, smooth=False)
    ob.location = (lo + hi) / 2
    if bevel:
        b = ob.modifiers.new("bv", "BEVEL")
        b.width, b.segments = bevel, 2
    return ob


def deck(wood, iron, gold, sx, lowered):
    """The 4 x 4.1 m bridge: planks across, iron side rails and end bands, rivets; hinged at the north bank."""
    parts = []
    hw = DECK_W / 2
    for i in range(8):
        y0 = -DECK_LEN + i * DECK_LEN / 8
        p = box("deck plank", (-hw + .02, y0 + .012, -.25), (hw - .02, y0 + DECK_LEN / 8 - .012, 0.), wood, .02)
        parts.append(p)
    for e in (-1, 1):
        parts.append(box("deck rail", (e * hw - (.16 if e > 0 else 0), -DECK_LEN, -.27), (e * hw + (0 if e > 0 else .16), 0, .03), iron, .015))
        for k in range(9):
            y = -DECK_LEN + .2 + k * (DECK_LEN - .4) / 8
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=.03)
            r = cw.mesh_object("rivet", bm, gold)
            r.location = (e * (hw - .08), y, .04)
            parts.append(r)
    for y in (-DECK_LEN + .05, -.15):
        parts.append(box("deck band", (-hw, y - .06, -.27), (hw, y + .06, .02), iron, .01))
    # hinge drums at the north end
    for e in (-1, 1):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=.2, radius2=.2, depth=.5)
        h = cw.mesh_object("hinge drum", bm, iron)
        h.rotation_euler = (0, math.pi / 2, 0)
        h.location = (e * (hw - .35), -.05, -.12)
        parts.append(h)
    root = link(bpy.data.objects.new("deck " + ("white" if sx < 0 else "black"), None))
    for p in parts:
        p.parent = root
    root.location = (12 * sx, HINGE, DAIS)
    root.rotation_euler = (0 if lowered else math.radians(-90), 0, 0)
    return root


KIT = {}       # the user's drawbridge kit (c_kit_fix.py): KIT_Pylon, KIT_Parapet, normalised (bottom centre at 0)


def cut_pylon_chain(ob, vs):
    """The kit's pylon has a short heavy chain hanging off its anchor plate (user 11-07: "두 번째 사진 쇠사슬을 지워주고",
    replaced by the moving chain over the pylon's top). Faces outside the body's footprint - the merlons' outline plus
    the plate's depth - are deleted; the plate stays as the anchor."""
    top = vs[vs[:, 2] > .75 * vs[:, 2].max()]
    lo, hi = top[:, :2].min(0), top[:, :2].max(0)
    pad = .09 * (hi - lo).max()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    out = [f for f in bm.faces if not (lo[0] - pad <= f.calc_center_median().x <= hi[0] + pad and
                                       lo[1] - pad <= f.calc_center_median().y <= hi[1] + pad)]
    bmesh.ops.delete(bm, geom=out, context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    print("KIT_Pylon: chain cut off (%d faces)" % len(out))


def strip_pylon_plate(ob):
    """User 11-07 ("이거 지워줘", photo of the pylon's iron anchor plate with the cut chain's stubs): the plate and what
    is left of the kit's chain go. The kit's pylon stands turned about 36.5 degrees in its file, so first it is turned
    square (its side faces' mean direction, mod 90); then the half on the plate's side (the grey iron's direction from
    the crown's middle) is cut off on the plane through the middle and rebuilt as the clean half's mirror, its faces
    turned outward (user 11-07: mirrored about the unturned axis it came out crooked, "기둥 쪽 삐뚤어졌어")."""
    me = ob.data
    zmax = max(v.co.z for v in me.vertices)
    # 1. square it: the side faces' mean direction (4x angle, so the four sides agree)
    sx = sy = 0.
    for p in me.polygons:
        if abs(p.normal.z) < .3 and .15 * zmax < p.center.z < .7 * zmax:
            a4 = 4 * math.atan2(p.normal.y, p.normal.x)
            sx += math.cos(a4) * p.area
            sy += math.sin(a4) * p.area
    turn = math.atan2(sy, sx) / 4
    vs = np.array([v.co[:] for v in me.vertices])
    crown = vs[:, 2] > .8 * zmax
    cx, cy = (vs[crown, 0].min() + vs[crown, 0].max()) / 2, (vs[crown, 1].min() + vs[crown, 1].max()) / 2
    # 2. the plate's side: where the grey (iron) faces under the crown lie from the middle
    img = next(n.image for n in me.materials[0].node_tree.nodes
               if n.type == "TEX_IMAGE" and any(l.to_socket.name == "Base Color" for o in n.outputs for l in o.links))
    W, H = img.size
    px = np.array(img.pixels[:], np.float32).reshape(H, W, 4)
    uvd = me.uv_layers.active.data
    gx = gy = 0.
    for p in me.polygons:
        if p.center.z > .72 * zmax:
            continue
        u = np.mean([uvd[li].uv[:] for li in p.loop_indices], 0)
        c = px[int((u[1] % 1) * (H - 1)), int((u[0] % 1) * (W - 1)), :3]
        if c.max() - c.min() < .06:
            gx += (p.center.x - cx) * p.area
            gy += (p.center.y - cy) * p.area
    plate = math.atan2(gy, gx) - turn                     # its direction once squared
    k = round(plate / (math.pi / 2)) % 4                  # 0 +x, 1 +y, 2 -x, 3 -y
    # 3. turn it square about the middle and so the plate's side is -x (one rotation does both)
    spin = -turn + (math.pi - k * math.pi / 2)
    bm = bmesh.new()
    bm.from_mesh(me)
    rot = Matrix.Rotation(spin, 3, "Z")
    for v in bm.verts:
        v.co = rot @ (v.co - Vector((cx, cy, 0.)))
    # 4. cut the -x half off on the middle plane and mirror the +x half into its place, faces outward
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), dist=1e-7,
                           plane_co=(0., 0., 0.), plane_no=(-1., 0., 0.), clear_outer=True)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    kept = set(bm.faces)
    bmesh.ops.mirror(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), axis="X", merge_dist=.0004 * zmax)
    bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f not in kept], flip_multires=False)
    for v in bm.verts:
        v.co += Vector((cx, cy, 0.))
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    print("KIT_Pylon: squared (turned %.1f deg), anchor plate side cut and mirrored" % math.degrees(spin))


def load_kit():
    with bpy.data.libraries.load(os.path.join(SRC, "R502_DrawbridgeKit_Fixed.blend")) as (src, dst):
        dst.objects = ["KIT_Pylon", "KIT_Parapet"]
    for ob in dst.objects:
        link(ob)
        size = normalise(ob)
        vs = np.array([v.co[:] for v in ob.data.vertices])
        # the pylon's chain hangs off one face: the side where the bounding box reaches farthest past the median
        med = np.median(vs[:, :2], 0)
        if ob.name == "KIT_Pylon":
            cut_pylon_chain(ob, vs)
            strip_pylon_plate(ob)
        KIT[ob.name] = (ob, size, med)
        ob.hide_render = True
        ob.hide_set(True)
        print("KIT", ob.name, tuple(round(v, 3) for v in size), "median xy", tuple(round(float(v), 3) for v in med))


def pylon(me, iron, glass, gold, x, lowered, sx):
    """The user's rook pylon (kit) by the hinge, 3.3 m, its chain anchor turned toward the bridge; a lantern on its
    south face; a chain from its top to the deck's far corner."""
    y = HINGE + .25
    ob, size, med = KIT["KIT_Pylon"]
    k = 3.3 / size.z
    # square to the section, the crown's middle on (x, y): the anchor plate is gone (strip_pylon_plate), and the game's
    # chain hangs from an eye in the middle of the crown (MgDrawbridge.ChainEnds, PalaceCrownFloor)
    vs = [v.co for v in ob.data.vertices if v.co.z > .8 * size.z]
    cx = .5 * (min(v.x for v in vs) + max(v.x for v in vs))
    cy = .5 * (min(v.y for v in vs) + max(v.y for v in vs))
    place(ob, (x - cx * k, y - cy * k, DAIS), (k, k, k), 0.)
    top = DAIS + 3.3
    cw.lantern(iron, glass, (x, y - .72, DAIS + 2.1))
    end_x = 12 * sx + (-1.6 if x < 12 * sx else 1.6)
    if lowered:
        end = (end_x, HINGE - DECK_LEN + .1, DAIS + .05)
    else:
        end = (end_x, HINGE + .05, DAIS + DECK_LEN - .1)
    cw.chain(iron, (x, y - .4, top - .15), end, .25 if lowered else .05, "deck chain")


def crown(gold, at, size):
    x, y, z = at
    parts = [box("crown band", (x - .5 * size, y - .04, z), (x + .5 * size, y + .04, z + .16 * size), gold, .01)]
    for k in range(-2, 3):
        h = (.6 if k == 0 else .34 if abs(k) == 1 else .46) * size
        parts.append(box("crown point", (x + k * .22 * size - .055 * size, y - .035, z + .14 * size), (x + k * .22 * size + .055 * size, y + .035, z + h), gold, .01))
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=.07 * size)
        s = cw.mesh_object("crown ball", bm, gold)
        s.location = (x + k * .22 * size, y, z + h + .05 * size)
    return parts


def central_parapet(me, gold, stone_m):
    """The user's central parapet with the gold crest (kit), 4 m wide where the aisle meets the moat."""
    ob, size, med = KIT["KIT_Parapet"]
    k = 4. / size.x
    place(ob, (0., MOAT0 - .3 - size.y * k / 2, 0.), (k, k, k), 0.)


def banner(name, x, top, y, w, h, cloth, trim, rod, emblem=None, laurel=None):
    """A hanging banner with a swallow-tailed hem, a gold trim and a gold rod with knobs (the concept's banners)."""
    bm = bmesh.new()
    cols, rows = 8, 12
    grid = []
    for r in range(rows + 1):
        row = []
        for c in range(cols + 1):
            u = c / cols
            v = r / rows
            zz = top - v * h
            if r == rows:
                zz += h * .18 * (1 - abs(u - .5) * 2)      # the notch
            yy = y - .03 * math.sin(u * math.pi * 2 + v * 3)
            row.append(bm.verts.new((x - w / 2 + u * w, yy, zz)))
        grid.append(row)
    for r in range(rows):
        for c in range(cols):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    ob = cw.mesh_object(name, bm, cloth)
    ob.modifiers.new("solid", "SOLIDIFY").thickness = .02
    # trim: thin gold strips along the sides and the hem
    for e in (-1, 1):
        box(name + " trim", (x + e * w / 2 - .04, y - .05, top - h * .97), (x + e * w / 2 + .04, y + .01, top), trim)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=.04, radius2=.04, depth=w + .4)
    r = cw.mesh_object(name + " rod", bm, rod)
    r.rotation_euler = (0, math.pi / 2, 0)
    r.location = (x, y - .02, top + .03)
    for e in (-1, 1):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=.08)
        k = cw.mesh_object(name + " knob", bm, rod)
        k.location = (x + e * (w / 2 + .22), y - .02, top + .03)
    cz = top - h * .45
    if emblem:     # a white rook
        for lo, hi in (((-.22, -.36), (.22, -.24)), ((-.15, -.24), (.15, .14)), ((-.24, .14), (.24, .26)),
                       ((-.24, .26), (-.12, .38)), ((-.06, .26), (.06, .38)), ((.12, .26), (.24, .38))):
            box(name + " rook", (x + lo[0] * w, y - .06, cz + lo[1] * w), (x + hi[0] * w, y - .04, cz + hi[1] * w), emblem)
    if laurel:     # two gold sprays
        for e in (-1, 1):
            for k in range(7):
                a = math.radians(-70 + k * 22)
                px = x + e * (math.sin(a) * .0 + .17 * w * math.cos(a * .9) * 1.0) * (1 if e > 0 else 1)
                pz = cz + .25 * w * math.sin(a)
                bm = bmesh.new()
                bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=.05 * w)
                bmesh.ops.scale(bm, vec=(.5, .3, 1.), verts=bm.verts)
                lf = cw.mesh_object(name + " laurel", bm, laurel)
                lf.location = (px + e * .03 * k * w * .2, y - .06, pz)
                lf.rotation_euler = (0, e * (a + .6), 0)


def rope(mat, pts, r=.06):
    """A rope along a polyline."""
    curve = bpy.data.curves.new("rope", "CURVE")
    curve.dimensions = "3D"
    sp = curve.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for p, q in zip(sp.points, pts):
        p.co = (*q, 1.)
    curve.bevel_depth = r
    curve.bevel_resolution = 4
    ob = link(bpy.data.objects.new("rope", curve))
    ob.data.materials.append(mat)
    return ob


def knot(gold_glow, at):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=.15)
    for v in bm.verts:
        v.co *= 1 + .12 * math.sin(v.co.x * 60) * math.sin(v.co.z * 60)
    ob = cw.mesh_object("knot", bm, gold_glow)
    ob.scale = (1, .55, 1)
    ob.location = at


def cypress(leaf, at, h):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=.75, radius2=.05, depth=h)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3)
    for v in bm.verts:
        v.co += Vector((noise.noise(v.co * 2.), noise.noise(v.co * 2. + Vector((5, 5, 5))), 0)) * .12
    ob = cw.mesh_object("cypress", bm, leaf)
    ob.location = (at[0], at[1], at[2] + h / 2)
    trunk = box("trunk", (at[0] - .08, at[1] - .08, at[2]), (at[0] + .08, at[1] + .08, at[2] + .4), cw.principled("trunk", "#5A4130", .9))


def lifebuoy(at, yaw):
    bm = bmesh.new()
    segs, tube = 32, 12
    R, r = .32, .09
    verts = []
    for i in range(segs):
        u = i / segs * 2 * math.pi
        for j in range(tube):
            v = j / tube * 2 * math.pi
            verts.append(bm.verts.new(((R + r * math.cos(v)) * math.cos(u), r * math.sin(v), (R + r * math.cos(v)) * math.sin(u))))
    for i in range(segs):
        for j in range(tube):
            a = verts[i * tube + j]
            b = verts[((i + 1) % segs) * tube + j]
            c = verts[((i + 1) % segs) * tube + (j + 1) % tube]
            d = verts[i * tube + (j + 1) % tube]
            bm.faces.new((a, b, c, d))
    ob = cw.mesh_object("lifebuoy", bm, cw.principled("buoy", "#F1ECE2", .6))
    ob.location = at
    ob.rotation_euler = (0, 0, math.radians(yaw))


def rope_coil(rope_m, at):
    for k in range(4):
        bm = bmesh.new()
        segs, tube = 40, 8
        R, r = .34 - k * .02, .06
        verts = []
        for i in range(segs):
            u = i / segs * 2 * math.pi
            for j in range(tube):
                v = j / tube * 2 * math.pi
                verts.append(bm.verts.new(((R + r * math.cos(v)) * math.cos(u), (R + r * math.cos(v)) * math.sin(u), r * math.sin(v))))
        for i in range(segs):
            for j in range(tube):
                bm.faces.new((verts[i * tube + j], verts[((i + 1) % segs) * tube + j],
                              verts[((i + 1) % segs) * tube + (j + 1) % tube], verts[i * tube + (j + 1) % tube]))
        ob = cw.mesh_object("rope coil", bm, rope_m)
        ob.location = (at[0], at[1], at[2] + .06 + k * .11)


def gear(iron, at):
    bm = bmesh.new()
    teeth, r0, r1 = 12, .26, .34
    pts = []
    for i in range(teeth * 4):
        a = i / (teeth * 4) * 2 * math.pi
        rr = r1 if (i % 4) in (1, 2) else r0
        pts.append((rr * math.cos(a), rr * math.sin(a)))
    fr = [bm.verts.new((x, -.04, z)) for x, z in pts]
    bk = [bm.verts.new((x, .04, z)) for x, z in pts]
    bm.faces.new(fr[::-1])
    bm.faces.new(bk)
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        bm.faces.new((fr[i], fr[j], bk[j], bk[i]))
    ob = cw.mesh_object("gear", bm, iron, smooth=False)
    ob.location = at
    ob.rotation_euler = (math.radians(-20), 0, math.radians(15))


# ------------------------------------------------------------------ the section
def build():
    sc = tb.reset()
    info = json.load(open(os.path.join(tb.TEX, "PR_Early_TowerStone.json")))
    stone_m = tb.stone_material(info)
    stone_m.name = "PR_Early_TowerStone"
    iron = cw.iron_material()
    wood = cw.wood_material()
    glass = cw.principled("lantern glass", "#FFD58A", .2, 0., "#FFB24A", 9.)
    leaf = cw.principled("leaf", "#4F8A36", .6)
    dark_leaf = cw.principled("cypress", "#3C6A34", .8)
    bloom = cw.principled("bloom", "#F3EEDD", .7)
    water = cw.water_material()
    rope_m = cw.principled("rope", "#C9A57A", .9)
    tick = cw.principled("tick (functional gold)", "#E2B23C", .45, .3, "#E2B23C", .5)
    me = cw.load_module()

    gate, gsize = load_gate()
    gold = asset_gold(gate)
    tower, tsize = load_glb(os.path.join(SRC, "R502_CastleTower_Tripo.glb"), "tower")
    winch, wdrum = load_winch()
    wsize = winch.dimensions
    rail, rsize = load_fbx(os.path.join(SRC, "R502_GardenStoneWall.fbx"), "railing")
    load_kit()
    print("SIZES gate", tuple(round(v, 3) for v in gsize), "tower", tuple(round(v, 3) for v in tsize), "winch", tuple(round(v, 3) for v in wsize))

    # ---- the facade: the gatehouse with its side passages on the team gates (x +-12), front face at y 24
    arch_cx = .34                                   # c_gate_open.py: the passages' centres, from the middle
    sx = 12. / arch_cx
    sz = 24.
    sy = 6. / gsize.y
    place(gate, (0, FACADE + 3., 0.), (sx, sy, sz))
    print("GATE scale", round(sx, 2), round(sy, 2), round(sz, 2), "-> width %.1f height %.1f" % (gsize.x * sx, gsize.z * sz))
    # ---- the corner towers (the user's tower, as is) either side of the gatehouse
    # the user's roofed towers rise behind the gatehouse at both ends, an upper wall between them, and
    # crenellated square towers of stone block stand at the front corners (the concept's back and front towers)
    k = 19.
    for s in (-1, 1):
        # behind the rear corridor (y 30..36): at y 31.6 they filled the team passages' far end and the corridor
        # (user 11-07, "양 쪽 사이드 문이 이렇게 막혀 있으면 통로가 안돼")
        place(tower, (s * 15.0, REAR + 11.3, 0.), (k, k, k), 0)
        square_tower(me, s * 19.8, FACADE - 1.7, 3.6, DAIS, 3)   # clear of the 5 m moat (ends at y 20.5)
    # the upper arcade: the gatehouse again, turned so its three-arched side faces the plaza, raised behind it
    place(gate, (0, FACADE + 9.5, 9.0), (7. / gsize.x, 22. / gsize.y, 15.), -90)

    # ---- floors (the stone block face up): entry band, the two stations (0.4 m up), the central aisle
    floor(me, -22, 22, 0., 4., 0.)
    for s in (-1, 1):
        x0, x1 = (2., 22.) if s > 0 else (-22., -2.)
        floor(me, x0, x1, 4., MOAT0, DAIS)
        # the station floors run on under the gatehouse's front (no sky between floor and wall, user 11-07), except in
        # the team passage (x 10..14), which steps down to the passage floor as the game's (Gate passage, top 0)
        lo_x, hi_x = sorted((s * 2., s * 10.))
        floor(me, lo_x, hi_x, MOAT1, FACADE + .8, DAIS)
        lo_x, hi_x = sorted((s * 14., s * 22.))
        floor(me, lo_x, hi_x, MOAT1, FACADE + .8, DAIS)
        lo_x, hi_x = sorted((s * 10., s * 14.))
        floor(me, lo_x, hi_x, MOAT1, FACADE, DAIS)
        floor(me, lo_x, hi_x, FACADE, REAR, 0.)                   # the team passage through the gatehouse
    # the central aisle stays low, its curbs to the stations kept (user 11-08, "역과 통로 사이 옆 턱은 유지")
    floor(me, -2., 2., 4., MOAT0, 0.)
    floor(me, -2., 2., MOAT1, FACADE + .8, 0.)
    # two stone steps up from the entry band to each station, the station's whole width (x 2..22, y 2.4..4); the game
    # walks a hidden 14 degree slope under them (MiniGameModels.Drawbridge.CurbRamps, as the stations' own ramp)
    for s in (-1, 1):
        lo_x, hi_x = sorted((s * 2., s * 22.))
        floor(me, lo_x, hi_x, 2.4, 3.2, DAIS * .5)
        floor(me, lo_x, hi_x, 3.2, 4., DAIS)
    # ---- the rear corridor (the game's, y 30..36) to the exit (x -6..6 at y 36): floor, back walls, side walls
    floor(me, -22., 22., REAR, REAR + 6., 0.)
    for lo_x, hi_x in ((-22., -14.), (-10., 10.), (14., 22.)):      # under the gatehouse's rear edge: no sky below it
        floor(me, lo_x, hi_x, REAR - 1.2, REAR, 0.)
    for s in (-1, 1):
        lo_x, hi_x = sorted((s * 6., s * 22.))
        wall(me, lo_x, hi_x, REAR + 6., 0., 0, courses=3)
        side_wall(me, s * 22., REAR - .4, REAR + 6.4, 0., -s, courses=3)
    # ---- the moat: banks of standing modules facing the water, a module bottom, the water
    wall(me, -22, 22, MOAT0, -1.8, 180)
    wall(me, -22, 22, MOAT1, -1.8, 0)
    floor(me, -22, 22, MOAT0, MOAT1, MOAT_BOTTOM)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.)
    w = cw.mesh_object("water", bm, water, smooth=False)
    w.scale = (22., 2., 1.)
    w.location = (0, (MOAT0 + MOAT1) / 2, WATER)
    # ---- side walls: one course on the station floor, a module coping, cypresses behind
    for s in (-1, 1):
        # the user's garden stone wall (pillars, chains, bench, planter) joined end to end along both sides
        railing_run(rail, rsize, s, 0., 4., 0.)
        # one unbroken run from the stations to the facade (user: walls must read as continuous): over the moat it
        # stands on a stone block wall rising from the moat's bottom
        railing_run(rail, rsize, s, 4., FACADE - .2, DAIS)
        side_wall(me, s * (22. - 1.2), MOAT0 - .3, MOAT1 + .3, -1.8, -s, k=2.)
        for y in (6., 9.5, 13., 22.):
            cypress(dark_leaf, (s * 23.4, y, 0.), random.uniform(5.5, 7.))

    # ---- bridges, pylons and chains: white's lowered, black's standing (as the concept)
    for s, lowered in ((-1, True), (1, False)):
        deck(wood, iron, gold, s, lowered)
        for e in (-1, 1):
            pylon(me, iron, glass, gold, 12 * s + e * 3.4, lowered, s)   # just outside the deck (MgDrawbridge.PalacePylonX)
    central_parapet(me, gold, stone_m)

    # ---- the winches at the front, the rope lanes, the ropes and the gold knots
    for s in (-1, 1):
        wx = s * WINCH_X
        place_winch(winch, wdrum, (wx, WINCH_Y, DAIS + 1.3), 90 if s < 0 else -90)
        # R502 (user 11-07, "컨셉 아트를 보고 그거대로", then "l l 이런식으로", the pulley): the rope runs from the drum's
        # top north along a track of two dark iron plates (gold studs) to a pulley on a stone post by the moat, across
        # the post to a second pulley and back south beside the track; the pawns pull the knots on the returning line
        # south, toward the winch (MgDrawbridge.PalacePulleyAside 1.3, PalaceKnotU 8.3, gap 0.8).
        bx = wx - s * PULL_ASIDE                     # the returning line (the bridge's side, user 11-07 swap)
        py = ANCHOR_Y - .08                          # the pulleys' rope line (MgDrawbridge.PalacePulleyU)
        for e in (-1, 1):
            x0, x1 = wx + e * .12, wx + e * .62
            box("lane band", (min(x0, x1), WINCH_Y + .9, DAIS + .05), (max(x0, x1), ANCHOR_Y - .3, DAIS + .1), iron, .02)
            y = WINCH_Y + 1.2
            while y < ANCHOR_Y - .5:
                bm = bmesh.new()
                bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=.035)
                st = cw.mesh_object("lane stud", bm, gold)
                st.location = (wx + e * .55, y, DAIS + .11)
                y += .6
        # the post across both lines, a gold plate on its south face and a gold pulley on an iron bracket for each line
        cx = .5 * (wx + bx)
        box("rope anchor post", (cx - PULL_ASIDE / 2 - .45, ANCHOR_Y, DAIS), (cx + PULL_ASIDE / 2 + .45, MOAT0 - .04, DAIS + 1.0), stone_m, .05)
        box("rope anchor plate", (cx - PULL_ASIDE / 2 - .3, ANCHOR_Y - .04, DAIS + .3), (cx + PULL_ASIDE / 2 + .3, ANCHOR_Y, DAIS + .9), gold, .02)
        # the pulleys: the rope wraps each one's rim (MgDrawbridge.Wrap, groove 0.15 m), so a wheel's centre sits inside
        # the turn - a hub the rope rides on, a flange above and below it, an axle on two iron brackets from the post
        dx = 1. if bx > wx else -1.                  # the way the rope turns at the first pulley
        for qx in (wx + dx * .15, bx - dx * .15):
            qy = py - .15
            for z0, z1 in ((.62, .66), (.94, .98)):
                box("pulley bracket", (qx - .05, qy - .05, DAIS + z0), (qx + .05, ANCHOR_Y - .03, DAIS + z1), iron, .01)
            for r, depth, z in ((.085, .15, .8), (.2, .025, .8 - .085), (.2, .025, .8 + .085), (.03, .3, .8)):
                bm = bmesh.new()
                bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=r, radius2=r, depth=depth)
                pw = cw.mesh_object("pulley wheel", bm, gold)
                pw.location = (qx, qy, DAIS + z)
        # the ticks under the returning line, every metre the lead knot goes (u 8.3 down to 1.3)
        for i in range(8):
            ty = 4. + 8.3 - i
            box("lane tick", (bx - .5, ty - .025, DAIS + .003), (bx + .5, ty + .025, DAIS + .009), tick)
        drum = Vector((wx, WINCH_Y, DAIS + 1.3))
        rope(rope_m, [(wx, WINCH_Y + .1, drum.z + .48), (wx, py, DAIS + .8)])
        rope(rope_m, [(wx, py, DAIS + .8), (bx, py, DAIS + .8), (bx, 4. + 8.3 - .8, DAIS + .8)])
        for k in range(4):
            knot(tick, (bx, 4. + 8.3 + k * .8, DAIS + .8))

    # ---- facade dressing: team banners over the passages, cream laurel banners by the central door, lanterns
    blue = cw.principled("banner white team", "#3FC1D9", .75)
    orange = cw.principled("banner black team", "#F08A2C", .75)
    cream = cw.principled("banner cream", "#EEE6D2", .8)
    white = cw.principled("emblem", "#FAFAF7", .7)
    front = FACADE - .25
    for s, cloth in ((-1, blue), (1, orange)):
        banner("team banner", s * 12., 10.9, front, 2.6, 3.8, cloth, gold, gold, emblem=white)
        banner("laurel banner", s * 6.3, 10.2, front - .3, 2.0, 3.4, cream, gold, gold, laurel=gold)
        banner("outer banner", s * 20.8, 6.6, 1.0, 1.5, 2.8, cream, gold, gold, laurel=gold)
        bpost = box("banner post", (s * 20.8 - .06, .94, 0), (s * 20.8 + .06, 1.06, 6.5), iron)
        for e in (-1, 1):
            cw.lantern(iron, glass, (s * 12 + e * 2.6, front - .05, 3.2))

    # ---- props, as the concept (the side walls' planters and benches are the garden wall's own): rope coil,
    # crate, buoy, gear
    # between the returning rope line (x -18.4, pawns to -18.8) and the railing (-21.4): props have no collider
    rope_coil(rope_m, (-20.3, 9.8, DAIS))
    box("crate", (-21.2, 8.2, DAIS), (-20.5, 8.9, DAIS + .65), wood, .03)
    lifebuoy((19.4, 15.4, DAIS + .35), 90)
    gear(iron, (18.7, 15.6, DAIS + .34))
    return sc


def render_views(sc):
    tb.light_rig(sc, (0, 0, 0), 0)
    sky = sc.world.node_tree.nodes["Background"]
    sky.inputs["Color"].default_value = (.33, .58, 1.0, 1)
    sky.inputs["Strength"].default_value = 1.1
    sun = sc.objects["sun"]
    sun.rotation_euler = (math.radians(52), 0, math.radians(-28))
    sun.data.energy = 4.2
    sc.cycles.samples = 128
    sc.render.resolution_x, sc.render.resolution_y = 1536, 1024
    # the concept's view: from over the entrance, looking at the gate
    tb.camera(sc, (0, -13., 17.5), (0, 17.5, 3.6), 33)
    sc.render.filepath = os.path.join(HERE, "render_v2_main.png")
    bpy.ops.render.render(write_still=True)
    tb.camera(sc, (30, -18, 30), (0, 14, 0), 30)
    sc.render.filepath = os.path.join(HERE, "render_v2_axo.png")
    bpy.ops.render.render(write_still=True)


def compare():
    def load(p):
        img = bpy.data.images.load(p, check_existing=False)
        a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(img.size[1], img.size[0], 4)

    def resized(arr, w):
        img = bpy.data.images.new("t", arr.shape[1], arr.shape[0], alpha=True)
        img.pixels.foreach_set(arr.ravel())
        img.scale(w, int(arr.shape[0] * w / arr.shape[1]))
        a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(img.size[1], img.size[0], 4)
    C = load(CONCEPT)
    h, w = C.shape[:2]
    main = C[:h - int(h * 56 / 1024), :int(w * 1120 / 1536)]          # the big view, no title, no insets
    W = 1100
    top = resized(main, W)
    bot = resized(load(os.path.join(HERE, "render_v2_main.png")), W)
    bar = np.ones((8, W, 4), np.float32) * np.array([.55, .45, .35, 1.], np.float32)
    sheet = np.concatenate([bot, bar, top], 0)
    img = bpy.data.images.new("cmp", W, sheet.shape[0], alpha=True)
    img.pixels.foreach_set(sheet.ravel())
    img.filepath_raw = os.path.join(HERE, "v2_compare.png")
    img.file_format = "PNG"
    img.save()
    print("wrote v2_compare.png")


if __name__ == "__main__":     # c_drawbridge_v2_export.py imports build() from here
    sc = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "C_Drawbridge_v2.blend"))
    if "--render" in ARGS:
        render_views(sc)
        compare()
