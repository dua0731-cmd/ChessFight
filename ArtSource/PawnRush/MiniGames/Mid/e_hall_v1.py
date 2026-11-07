# E Royal Library room (R507): the code-built R312 shell is removed for E (as B, user 11-07: walls must run on
# without gaps, the old structure may go). The user's two Tripo sets from the GPT asset sheets
# (Docs/PawnRush/ConceptArt/R507_RoyalLibrary/AssetRefs/E_assets_03_walls.png, E_assets_04_gate.png) make the walls;
# a plain cream ashlar fill closes every remaining gap up to the 13 m ceiling (B's PR_Mid_AshlarFill), a walnut-board
# slab (PR_Mid_WalnutFloor) closes the top. Colliders stay the game's (MiniGameSectionBuilder).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python e_hall_v1.py -- [--render] [--export]
#
# Sources (Source/, 1 m wide, z up, parts on the ground plane):
#   R507_ModularArchitecturalElements: ground window wall, low grey-book wall bookcase, upper window wall, gallery
#                                      balcony, plain wall (by depth in the top view)
#   R507_ModularFantasyKit:            centre facade with the closed double door, open arch, ceiling beam, cornice
#
# Output Export/E_HallEnv.fbx in the section frame (Blender x = section x, y = section z, z = up). The section is
# MiniGameSectionBuilder's: walls at x +-22..23, entry opening x +-6 at z 0, the north block x +-22, z 24..30 with the
# two team-gate tunnels (x +-(9.78..14.22)), the rear corridor z 30..36 and the exit opening x +-6 at z 36.
# Every module stands in front of the fill; front faces never share a plane with it (1 cm and more).
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import e_library_v1 as E  # noqa: E402  (load, split, decimate, fit_box, place_copy, kit_material, join, export)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TOP = 13.                          # ceiling (phase-2 hall, as B)
FILL_IN, FILL_OUT = 22.6, 23.0     # side-wall fill slab |x|
FACE = 22.0                        # the side modules' front face |x| (the game's wall collider starts at 22)
MOD_W, MOD_D = 5.0, .6             # ground / upper module pitch along the wall and depth
GROUND_TOP, DECK_TOP, UPPER_TOP, CORNICE_TOP = 5.0, 5.3, 11.7, 12.6
SIDE_Z = [2.5, 7.5, 12.5, 17.5, 22.5]                         # main room, z 0..25
REAR_Z, REAR_W = 33.0, 6.0                                      # rear corridor, z 30..36
NORTH = 25.0                       # north wall plane (room face of the north block's fill)
TUNNEL = (9.78, 14.22, 5.8)        # team-gate tunnel |x| span and its ceiling (E_DoorArch crown 5.8)
GATE_W, GATE_D, GATE_FRONT = 16., 1.3, 23.69                  # centre facade (within the team door arches' piers)
OPEN_HALF = 6.2                    # entry / exit opening half width (the game's 12 m + margin, as B)
ARCH_APEX = 8.5                    # the entry / exit arch's inner crown
BEAM_Z = [5., 10., 15., 20.]       # ceiling beams across the room (section z), over the module joints
BEAM_H = .5
SHELF = (3.6, .5, 1.7)             # low wall bookcase in front of each ground side module's wainscot


# ------------------------------------------------------------------ splitting rules (measured, kit_groups)
def wall_rule(c):
    if c.y > .25: return "ground"
    if c.y > .06: return "shelf"
    if c.y > -.01: return "upper"
    if c.y > -.2: return "balcony"
    return "plain"


def gate_rule(c):
    """By each face's centre: Tripo joined the arch's right pier (x -0.224..-0.028) and the beam's left post
    (-0.028..0.219) into one mesh, so the two are cut face by face at x -0.028."""
    if c.x < -.23: return "facade"
    if c.x > .235: return "cornice"
    if c.x < -.028: return "arch"
    return "beam"


def split_faces(ob, rule):
    """As e_library_v1.split, but face by face (parts Tripo welded together)."""
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    groups = {}
    for f in bm.faces:
        key = rule(f.calc_center_median())
        if key: groups.setdefault(key, []).append(f.index)
    out = {}
    for key, idx in groups.items():
        keep = set(idx); b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f.index not in keep], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new(key); b2.to_mesh(me); b2.free()
        for m in ob.data.materials: me.materials.append(m)
        o = bpy.data.objects.new(key, me); bpy.context.scene.collection.objects.link(o); out[key] = o
    bm.free(); bpy.data.objects.remove(ob)
    return out


# ------------------------------------------------------------------ fitting
def fit(o, w, d, h):
    """Bounding box onto x -w/2..w/2, y -d/2..d/2 (front face -y), z 0..h."""
    E.fit_box(o, (-w / 2, -d / 2, 0.), (w / 2, d / 2, h))


def opening(o, z_lo, z_hi):
    """The window / arch opening seen from the front: x span at mid height and the top of the hole above it, found
    by casting rays through the part along +y."""
    bvh = BVHTree.FromObject(o, bpy.context.evaluated_depsgraph_get())
    p = E.coords(o); lo, hi = p.min(0), p.max(0)
    xs = np.linspace(lo[0], hi[0], 240)
    def hole_x(z):
        free = [x for x in xs if bvh.ray_cast(Vector((x, lo[1] - 1, z)), Vector((0, 1, 0)))[0] is None]
        if not free: return None
        free = np.array(free); c = (lo[0] + hi[0]) / 2
        mid = free[np.abs(free - c) < (hi[0] - lo[0]) * .45]
        return (mid.min(), mid.max()) if len(mid) else None
    zm = z_lo + .4 * (z_hi - z_lo)
    span = hole_x(zm)
    if span is None: return None
    zc = zm
    for z in np.linspace(zm, hi[2], 120):
        if bvh.ray_cast(Vector(((span[0] + span[1]) / 2, lo[1] - 1, z)), Vector((0, 1, 0)))[0] is not None:
            zc = z; break
    zb = zm
    for z in np.linspace(zm, lo[2], 120):
        if bvh.ray_cast(Vector(((span[0] + span[1]) / 2, lo[1] - 1, z)), Vector((0, 1, 0)))[0] is not None:
            zb = z; break
    return span[0], span[1], zb, zc


def deck_top(o):
    """Height of the largest up-facing area (the balcony's floor)."""
    zs, ws = [], []
    for f in o.data.polygons:
        if f.normal.z > .9: zs.append(f.center.z); ws.append(f.area)
    h, edges = np.histogram(zs, bins=60, weights=ws)
    k = int(np.argmax(h)); return float((edges[k] + edges[k + 1]) / 2)


def build_arch(uv_name):
    """A round stone arch, origin at the opening's bottom centre, 1.2 m deep (y -0.6..0.6), front -y:
    walnut plinths 1 m, cream marble piers (|x| 6.2..7.4), an impost band, fifteen voussoirs on a semicircle
    (inner radius 6.2 = the opening's half width, crown at ARCH_APEX) and a proud keystone. Joints are drawn by
    every other block standing 1 cm proud, so no two faces share a plane."""
    stone = material("PR_Mid_CreamFloor", os.path.join(E.TEX, "PR_Mid_CreamFloor_BaseColor.png"))
    wood = material("PR_Mid_WalnutFloor", os.path.join(E.TEX, "PR_Mid_WalnutFloor_BaseColor.png"))
    r0, r1, d = OPEN_HALF, OPEN_HALF + 1.2, .6
    spring = ARCH_APEX - r0
    parts = []
    for s in (-1, 1):
        a, b = sorted((s * (r0 - .1), s * (r1 + .1)))
        parts.append(boxes([(a, b, -d - .05, d + .05, 0., 1.)], wood, "plinth", uv_name))
        a, b = sorted((s * r0, s * r1))
        parts.append(boxes([(a, b, -d, d, 1., spring)], stone, "pier", uv_name))
        a, b = sorted((s * (r0 - .12), s * (r1 + .12)))
        parts.append(boxes([(a, b, -d - .08, d + .08, spring, spring + .3)], stone, "impost", uv_name))
    bm = bmesh.new(); uv = bm.loops.layers.uv.new(uv_name)
    n = 15
    for k in range(n):
        a0, a1 = math.pi * k / n, math.pi * (k + 1) / n
        key = k == n // 2
        out = r1 + (.35 if key else 0.)
        dd = d + (.1 if key else .01 * (k % 2))
        zb = spring + .3
        ring = [(r * math.cos(a), r * math.sin(a)) for a in (a0, a1) for r in (r0, out)]
        vs = [bm.verts.new((x, y, zb + z)) for y in (-dd, dd) for (x, z) in (ring[0], ring[1], ring[3], ring[2])]
        for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            face = bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for face in bm.faces:
        nrm = face.normal; dom = max(range(3), key=lambda i: abs(nrm[i])); u_, v_ = [i for i in range(3) if i != dom]
        for l in face.loops: l[uv].uv = (l.vert.co[u_] / 2, l.vert.co[v_] / 2)
    me = bpy.data.meshes.new("voussoirs"); bm.to_mesh(me); bm.free(); me.materials.append(stone)
    o = bpy.data.objects.new("voussoirs", me); bpy.context.scene.collection.objects.link(o)
    parts.append(o)
    return E.join(parts, "arch")


def beam_split(o):
    """Height of the bar's underside: the lowest z of the up-to-down faces spanning most of the beam's length."""
    p = E.coords(o); lo, hi = p.min(0), p.max(0)
    mid = p[np.abs(p[:, 0] - (lo[0] + hi[0]) / 2) < .15 * (hi[0] - lo[0])]      # the middle, where only the bar is
    return float(mid[:, 2].min())


def cut_beam(o, z_cut):
    """Split the (placed) beam into the bar (faces above z_cut) and its two ends (below, x < 0 and x > 0); the bar
    is then centred on x 0."""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    parts = {}
    half = max(abs(v.co.x) for v in bm.verts)
    # the bar: only its middle 60 %, clear of the brackets' torn joints (white scraps once cut); its open ends meet
    # the next length or sink into the cornice at the walls
    for key, keep in (("bar", lambda c: c.z >= z_cut - .01 and abs(c.x) < .6 * half),
                      ("endW", lambda c: c.z < z_cut - .01 and c.x < 0),
                      ("endE", lambda c: c.z < z_cut - .01 and c.x >= 0)):
        b2 = bm.copy(); b2.faces.ensure_lookup_table()
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if not keep(f.calc_center_median())], context='FACES')
        bmesh.ops.delete(b2, geom=[v for v in b2.verts if not v.link_faces], context='VERTS')
        me = bpy.data.meshes.new(key); b2.to_mesh(me); b2.free()
        for m in o.data.materials: me.materials.append(m)
        ob = bpy.data.objects.new(key, me); bpy.context.scene.collection.objects.link(ob); parts[key] = ob
    bm.free()
    # the ends keep a short stub of the bar so they meet it; the bar ends where the ends' brackets begin
    p = E.coords(parts["bar"]); E.set_coords(parts["bar"], p - np.array([(p[:, 0].min() + p[:, 0].max()) / 2, 0, 0]))
    return parts["bar"], (parts["endW"], parts["endE"])


def side_m(s, z, y0, sx=1.):
    """A part whose front face is -y turned to face the room on the side wall s (x = s * ...), its back on FILL_IN."""
    return Matrix.Translation((s * (FILL_IN - MOD_D / 2 - .01), z, y0)) @ Matrix.Rotation(-s * math.pi / 2, 4, 'Z') @ Matrix.Diagonal((sx, 1, 1, 1))


def facing(x, zc, y0, yaw, sx=1.):
    return Matrix.Translation((x, zc, y0)) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Diagonal((sx, 1, 1, 1))


# ------------------------------------------------------------------ fill (as b_shell_v1.py)
def material(name, tex):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = .85
    return m


def boxes(specs, mat, name, uv_name):
    """Axis boxes (x0, x1, y0, y1, z0, z1) in the section frame, box UVs on a 2 m tile."""
    bm = bmesh.new(); uv = bm.loops.layers.uv.new(uv_name)
    for x0, x1, y0, y1, z0, z1 in specs:
        if x1 - x0 < .01 or y1 - y0 < .01 or z1 - z0 < .01: continue
        r = bmesh.ops.create_cube(bm, size=1.)
        for v in r["verts"]:
            v.co = Vector((x0 if v.co.x < 0 else x1, y0 if v.co.y < 0 else y1, z0 if v.co.z < 0 else z1))
        for f in {f for v in r["verts"] for f in v.link_faces}:
            n = f.normal
            for l in f.loops:
                p = l.vert.co
                l[uv].uv = (p.x / 2, p.y / 2) if abs(n.z) > .5 else ((p.y / 2, p.z / 2) if abs(n.x) > .5 else (p.x / 2, p.z / 2))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def cut(lo, hi, holes):
    """Split the span lo..hi around holes [(a, b)] (sorted, inside)."""
    out, at = [], lo
    for a, b in holes:
        if a > at: out.append((at, a))
        at = max(at, b)
    if hi > at: out.append((at, hi))
    return out


# ------------------------------------------------------------------ build
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    walls = E.split(E.load("R507_ModularArchitecturalElements"), wall_rule)
    gate = split_faces(E.load("R507_ModularFantasyKit"), gate_rule)
    for o in walls.values(): E.kit_material(o, "PR_Mid_LibraryWallKit")
    for o in gate.values(): E.kit_material(o, "PR_Mid_LibraryGateKit")
    uv_name = walls["ground"].data.uv_layers[0].name
    for k, o in list(walls.items()) + list(gate.items()):
        p = E.coords(o); print("PART", k, E.tris(o), p.min(0).round(3), p.max(0).round(3))
    targets = {"ground": 3000, "upper": 3000, "plain": 2500, "shelf": 3000, "balcony": 3000,
               "facade": 20000, "arch": 3000, "beam": 3000, "cornice": 1500}
    for k, o in list(walls.items()) + list(gate.items()):
        print("DEC", k, E.decimate(o, targets[k]))

    g, up, pl, sh, bal = walls["ground"], walls["upper"], walls["plain"], walls["shelf"], walls["balcony"]
    fit(g, MOD_W, MOD_D, GROUND_TOP); fit(pl, MOD_W, MOD_D, GROUND_TOP)
    fit(up, MOD_W, MOD_D, UPPER_TOP - DECK_TOP)
    fit(sh, *SHELF)
    # balcony: room side is the source's +y (the balustrade); turn it so the balustrade faces -y like the walls
    E.xform(bal, Matrix.Rotation(math.pi, 4, 'Z'))
    p = E.coords(bal); lo, hi = p.min(0), p.max(0)
    s = MOD_W / (hi[0] - lo[0])
    E.set_coords(bal, (p - np.array([(lo[0] + hi[0]) / 2, hi[1], 0.])) * s)      # back edge (wall side) at y 0
    dz = deck_top(bal); E.xform(bal, Matrix.Translation((0, 0, -dz)))             # deck top at z 0
    ga, ar, bm_, co = gate["facade"], gate["arch"], gate["beam"], gate["cornice"]
    p = E.coords(ga); gh = GATE_W * (np.ptp(p[:, 2]) / np.ptp(p[:, 0]))
    fit(ga, GATE_W, GATE_D, min(gh, 11.8))
    fit(co, MOD_W, .8, CORNICE_TOP - UPPER_TOP)
    # the entry / exit arch: the kit's arch was a 5 cm thin relief; stretched to the game's 1.2 m depth its piers
    # smeared and went lumpy (user, 11-07: "여기 깨져 있어"). Built here instead, in clean stone (build_arch).
    bpy.data.objects.remove(ar)
    ar = gate["arch"] = build_arch(uv_name)
    arch_apex = ARCH_APEX
    # the beam: the source is a bar with a knee bracket and post under each end. The bar is cut off (everything above
    # the brackets' top) and repeated across the room; the two ends (bracket + post) stand only at the side walls.
    p = E.coords(bm_); lo, hi = p.min(0), p.max(0)
    bar_bottom = beam_split(bm_)
    s = BEAM_H / (hi[2] - bar_bottom)                                  # bar height -> BEAM_H, uniform for the ends
    E.set_coords(bm_, (p - np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, hi[2]])) * s)
    bar, ends = cut_beam(bm_, (bar_bottom - hi[2]) * s)
    p = E.coords(bar); bar_len = np.ptp(p[:, 0])

    gw = opening(g, 0., GROUND_TOP); uw = opening(up, 0., UPPER_TOP - DECK_TOP)
    print("WINDOWS ground", gw, "upper", uw)

    env = []
    pc = E.place_copy
    for sd in (-1, 1):
        # side walls of the main room: ground windows, a grey-book bookcase in front, the gallery, upper windows, cornice
        for i, z in enumerate(SIDE_Z):
            env.append(pc(g, "ground", side_m(sd, z, 0., 1.03)))
            sm = Matrix.Translation((sd * (FACE - SHELF[1] / 2 - .02), z, 0.)) @ Matrix.Rotation(-sd * math.pi / 2, 4, 'Z')
            env.append(pc(sh, "shelf", sm))
            bm = Matrix.Translation((sd * (FACE - .01 * (i % 2)), z, DECK_TOP)) @ Matrix.Rotation(-sd * math.pi / 2, 4, 'Z') @ Matrix.Diagonal((1.03, 1, 1, 1))
            env.append(pc(bal, "balcony", bm))
            env.append(pc(up, "upper", side_m(sd, z, DECK_TOP, 1.03)))
            cm = Matrix.Translation((sd * (FILL_IN - .4 - .01 * (i % 2)), z, UPPER_TOP)) @ Matrix.Rotation(-sd * math.pi / 2, 4, 'Z') @ Matrix.Diagonal((1.03, 1, 1, 1))
            env.append(pc(co, "cornice", cm))
        # rear corridor side wall: one plain module
        env.append(pc(pl, "rear plain", side_m(sd, REAR_Z, 0., REAR_W / MOD_W * 1.02)))
        # north wall outer span (|x| 14.22..22.6): two plain modules and the cornice
        span = (TUNNEL[1], FILL_IN); w = (span[1] - span[0]) / 2
        for k in range(2):
            x = sd * (span[0] + w * (k + .5))
            env.append(pc(pl, "north plain", facing(x, NORTH - MOD_D / 2 - .01 * (k + 1), 0., 0., w / MOD_W * 1.03)))
        # entry wall wings (|x| 7.6..22.6): three plain modules, room face +y (turned 180)
        span = (OPEN_HALF + 1.4, FILL_IN); w = (span[1] - span[0]) / 3
        for k in range(3):
            x = sd * (span[0] + w * (k + .5))
            env.append(pc(pl, "entry plain", facing(x, -.05 + MOD_D / 2 + .01 * (k % 2 + 1), 0., math.pi, w / MOD_W * 1.03)))
    # cornice round the north and entry walls (full width, under the ceiling)
    for k in range(9):
        x = -22.6 + (k + .5) * 45.2 / 9
        env.append(pc(co, "north cornice", facing(x, NORTH - .4 - .01 * (k % 2), UPPER_TOP, 0., 45.2 / 9 / MOD_W * 1.03)))
        env.append(pc(co, "entry cornice", facing(x, -.05 + .4 + .01 * (k % 2), UPPER_TOP, math.pi, 45.2 / 9 / MOD_W * 1.03)))
    # the centre facade on the north block, front face at the old closed door's face
    env.append(pc(ga, "centre gate", Matrix.Translation((0, GATE_FRONT + GATE_D / 2, 0.))))
    # entry and exit arches (room face toward the room; the exit's toward the rear corridor)
    env.append(pc(ar, "entry arch", facing(0., -.05 + .6 + .02, 0., math.pi)))
    env.append(pc(ar, "exit arch", Matrix.Translation((0., 36.0 - .6 - .02, 0.))))
    # ceiling beams across the main room, under the ceiling: the bar in six stretched lengths from wall face to wall
    # face (each 1 % longer, every other one 1 cm lower: no shared end planes), the bracket ends against both walls
    n = 6; seg = 2 * FACE / n
    for z in BEAM_Z:
        for k in range(n):
            x = -FACE + (k + .5) * seg
            env.append(pc(bar, "beam", Matrix.Translation((x, z, TOP - .01 - .01 * (k % 2))) @ Matrix.Diagonal((seg / bar_len * 1.01, 1, 1, 1))))
        for sd, end in ((-1, ends[0]), (1, ends[1])):
            p = E.coords(end); ex = p[:, 0].min() if sd < 0 else p[:, 0].max()
            env.append(pc(end, "beam end", Matrix.Translation((sd * FACE - ex, z, TOP - .01))))

    # ---- fill
    fill = material("PR_Mid_AshlarFill", os.path.join(HERE, "..", "Early", "Materials", "Textures", "PR_Early_StoneBrick_BaseColor.png"))
    t0, t1, tc = TUNNEL
    specs = []
    # north facade plane (z 25.0..25.5): band over the tunnels, below it the centre and the outer spans
    nf = (NORTH, NORTH + .5)
    specs += [(-FILL_IN, FILL_IN, *nf, tc, TOP)]
    for sd in (-1, 1):
        a, b = sorted((0., sd * t0)); specs += [(a, b, *nf, 0., tc)]
        a, b = sorted((sd * t1, sd * FILL_IN)); specs += [(a, b, *nf, 0., tc)]
        # the tunnel through the block: side walls and a ceiling
        for x0, x1 in ((t0 - .25, t0), (t1, t1 + .25)):
            a, b = sorted((sd * x0, sd * x1)); specs += [(a, b, 24.0, 30.0, 0., tc)]
        a, b = sorted((sd * t0, sd * t1)); specs += [(a, b, 24.0, 30.0, tc, tc + .35)]
    # north block back (room face z 29.95 toward the rear corridor), tunnel rears open
    nb = (29.4, 29.95)
    specs += [(-FILL_IN, FILL_IN, *nb, tc, TOP), (-t0, t0, *nb, 0., tc)]
    for sd in (-1, 1):
        a, b = sorted((sd * t1, sd * FILL_IN)); specs += [(a, b, *nb, 0., tc)]
    # entry (room face z -0.05) and exit (room face z 35.95): wings full height, above the arch opening
    for zf, zb in ((-.6, -.05), (35.95, 36.6)):
        for sd in (-1, 1):
            a, b = sorted((sd * OPEN_HALF, sd * FILL_IN)); specs += [(a, b, zf, zb, 0., TOP)]
        specs += [(-OPEN_HALF, OPEN_HALF, zf, zb, arch_apex - .2, TOP)]
    # side walls behind every module, full height, with a hole behind each window (the windows look outside)
    gx = ((gw[0] + gw[1]) / 2 * 1.03, (gw[1] - gw[0]) / 2 * 1.03, gw[2], gw[3]) if gw else None
    ux = ((uw[0] + uw[1]) / 2 * 1.03, (uw[1] - uw[0]) / 2 * 1.03, DECK_TOP + uw[2], DECK_TOP + uw[3]) if uw else None
    bands = [(0., TOP, None)]
    if gx and ux:
        bands = [(0., gx[2], None), (gx[2], gx[3], gx), (gx[3], ux[2], None), (ux[2], ux[3], ux), (ux[3], TOP, None)]
    for sd in (-1, 1):
        a, b = sorted((sd * FILL_IN, sd * FILL_OUT))
        for y0, y1, win in bands:
            # the module's own x runs along -sd * section z after the turn, so its window centre offset flips
            holes = sorted((z - sd * win[0] - win[1], z - sd * win[0] + win[1]) for z in SIDE_Z) if win else []
            for z0, z1 in cut(-.6, 36.6, holes):
                specs += [(a, b, z0, z1, y0, y1)]
    stone = boxes(specs, fill, "fill", uv_name)
    wood = material("PR_Mid_WalnutFloor", os.path.join(E.TEX, "PR_Mid_WalnutFloor_BaseColor.png"))
    ceil = boxes([(-FILL_OUT, FILL_OUT, -.6, 36.6, TOP, TOP + .4)], wood, "ceiling", uv_name)
    # Floor strips where the game's floors stop short of the rebuilt walls (user, 11-07: "바닥에 틈 안생기게 해"):
    #   the stations' slate (top 0.4 m, the dais) ends at z 24, the north wall stands at z 25 -> slate z 24..25 beside
    #   the team-gate passages (their own floor, at 0, runs x +-10..14), the centre gate covers the middle;
    #   the entry band starts at z 0, the entry wall's face is z -0.05 -> chequer z -0.06..0;
    #   the north block's back face is z 29.95, the rear corridor starts at z 30 -> cream z 29.9..30.
    # Tops 2 to 5 mm under the game's floors where they meet (no shared plane).
    def floor_mat(name):
        return material(name, os.path.join(E.TEX, name + "_BaseColor.png"))
    strips = []
    slate = []
    for sd in (-1, 1):
        for x0, x1 in ((2., t0 + .2), (t1 - .2, FILL_IN)):
            a, b = sorted((sd * x0, sd * x1)); slate.append((a, b, 23.99, NORTH + .01, -.6, .395))
    strips.append(boxes(slate, floor_mat("PR_Mid_SlateFloor"), "north floor strip", uv_name))
    strips.append(boxes([(-FILL_IN, FILL_IN, -.07, 0., -.6, -.002)], floor_mat("PR_Mid_CheckerMarble"), "entry floor strip", uv_name))
    strips.append(boxes([(-FILL_IN, FILL_IN, 29.9, 30., -.6, -.002)], floor_mat("PR_Mid_CreamFloor"), "rear floor strip", uv_name))
    env += [stone, ceil] + strips
    for o in list(walls.values()) + list(gate.values()):
        bpy.data.objects.remove(o)
    hall = E.join(env, "E_HallEnv")
    print("HALL tris", E.tris(hall), "fill boxes", len(specs))
    if "--export" in ARGS: E.export(hall, "E_HallEnv")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "E_Hall_v1.blend"))
    if "--render" in ARGS: render(hall)


def render(hall):
    """The hall with the station towers (E_StationEnv at both stations, x +-12, z 4) and a few pawns."""
    bpy.ops.import_scene.fbx(filepath=os.path.join(E.OUT_FBX, "E_StationEnv.fbx"))
    st = [o for o in bpy.context.selected_objects if o.type == 'MESH'][0]
    st.data.transform(st.matrix_world); st.matrix_world.identity()
    for x in (-12., 12.):
        E.place_copy(st, "station", Matrix.Translation((x, 4., .4)))
    st.hide_render = True
    m = bpy.data.materials.new("floor"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.2, .23, .28, 1)
    bpy.ops.mesh.primitive_cube_add(location=(0, 18, -.5), scale=(23, 19, .5)); bpy.context.object.data.materials.append(m)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w"); w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.75, .85, 1., 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .8; sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
    bpy.context.object.data.energy = 3.0; bpy.context.object.data.color = (1, .9, .78)
    for x, y in ((0, 12), (-12, 12), (12, 12), (0, 22)):
        bpy.ops.object.light_add(type='POINT', location=(x, y, 10)); bpy.context.object.data.energy = 6000
        bpy.context.object.data.color = (1, .8, .55)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    views = {"entry": ((0, 1.5, 4.5), (0, 20, 5), 16), "north": ((-6, 8, 6), (0, 24, 5), 20),
             "side": ((-8, 6, 3), (-22, 14, 6), 18), "back": ((4, 22, 7), (0, 0, 5), 16),
             "corner": ((14, 18, 3), (22, 24, 5), 18), "rear": ((-14, 31, 2.5), (0, 33, 3), 18)}
    for name, (loc, tgt, lens) in views.items():
        cam.location = loc; cd.lens = lens
        cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(HERE, "render_e_hall_%s.png" % name)
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
