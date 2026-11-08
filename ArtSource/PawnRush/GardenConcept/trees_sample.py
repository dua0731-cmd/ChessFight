# Royal water garden - trees (R491, 승규 11-06: "나무를 배치 할 건데 길 쪽에 배치 하지 말고 길이 아닌 곳에 배치를 할거야
# 배치 라인은 너무 일렬로 배치 하지 마", the places chosen: the reservoir terraces and the waterside banks).
#
# tree_asset.py paints 승규's two Tripo trees (TreeAsset_v1.blend: a cone of a crown and a round one, trunk, mossy
# roots, leaves, flowers). Here copies of them are planted off the course:
#   terraces   the two 3.8 terraces beside the reservoir and by the gate (x +-26..44 at z 33..48.6, x +-14.3..44 at
#              48.6..59), their crowns clear of the gate yard (x +-14.3, the course), the boat tunnels' roofs (x +-26, 7.3
#              high), the gate's strip (z 59), the castle wall and the terraces' lake face (the dyke and lock below)
#   banks      the two banks (0.6) between the water's edge (x +-45.8) and the castle wall, clear of the wall's inner
#              face and of the towers with their bushes; their crowns may lean out over the water
# Not in rows: on the terraces each tree is thrown at random into the free ground left (Poisson-disc darts, its own size, a gap
# to its neighbours of 0.75 .. 1.25 of their crowns), so they cluster and part; the banks are too narrow for that (darts stood
# in a row), so there 3 or 4 groves a side of 1 .. 3 trees at any depth, open bank between; each tree turned at random,
# a little leaning, 8 .. 10.5 m high on the terraces, 6 .. 9 on the banks (mostly cones).
# The castle (WallsSample_v2.blend) is linked in for the renders only (not stored here, not exported).
#
# Run:  blender -b --python trees_sample.py           (after tree_asset.py)
# Out:  TreesSample_v1.blend ("Trees dressing": "Garden tree E1" ..), render_trees_v1_*.png
# Then: blender -b --python export_concept.py -- TreesSample_v1.blend "Trees dressing" GardenTrees

import os
import sys

os.environ["GC_DRESS"] = "Trees dressing"
os.environ["GC_SEED"] = "489"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403

HERE = os.path.dirname(os.path.abspath(__file__))
M["bark"] = material("GC | bark", (.58, .36, .18), .85)
M["moss"] = textured("GC | moss band", STONE_TEX, (.42, .62, .30), .75, .5)    # as the lock's and the gate's

UPPER, BANK, WATER_EDGE = 3.8, .6, 45.8
RND = random.Random(489)

# ------------------------------------------------------------------ the castle ring (walls_sample_v2.py's plan)
RING_A, RING_B, RING_CY, RING_N = 54.0, 66.81, 4.21, 2.4
WALL_HALF = 1.2                       # the wall's half thickness: its inner face 1.2 m in from its line
TOWER_CLEAR = 3.6                     # a tower (r 2.3 .. 2.6) and the bushes at its foot


def ring_pt(t):
    c, s = math.cos(t), math.sin(t)
    e = 2 / RING_N
    return Vector((math.copysign(abs(c) ** e, c) * RING_A, RING_CY + math.copysign(abs(s) ** e, s) * RING_B, 0))


def ring_towers(count=6):
    t_w = math.acos((24.8 / RING_A) ** (RING_N / 2))
    t_e = -math.acos((9.5 / RING_A) ** (RING_N / 2))
    ts = [t_w + (t_e - t_w) * i / 4000 for i in range(4001)]
    pts = [ring_pt(t) for t in ts]
    acc = [0.]
    for a, b in zip(pts, pts[1:]):
        acc.append(acc[-1] + (b - a).length)
    out, j = [], 0
    for i in range(1, count + 1):
        s = acc[-1] * i / count
        while j < len(acc) - 2 and acc[j + 1] < s:
            j += 1
        f = (s - acc[j]) / max(1e-9, acc[j + 1] - acc[j])
        out.append(pts[j].lerp(pts[j + 1], f))
    return out


TOWERS_E = ring_towers()
LINE_E = [Vector((24.8, 66.1, 0))] + TOWERS_E          # the wall's line from the gate wing's end, tower to tower


def seg_dist(p, a, b):
    ab = b - a
    t = max(0., min(1., (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def wall_clear(x, y):
    """How far (x, y) is inside the wall's inner face (negative: in or past the wall) and from the nearest tower."""
    p = Vector((abs(x), y, 0))
    line = min(seg_dist(p, a, b) for a, b in zip(LINE_E, LINE_E[1:]))
    tower = min((p - t).length for t in TOWERS_E)
    # inside the ring? (the ring's centre side of its line)
    c = Vector((0, 2, 0))
    a, b = min(zip(LINE_E, LINE_E[1:]), key=lambda ab: seg_dist(p, *ab))
    n = Vector((-(b - a).y, (b - a).x, 0)).normalized()
    inside = (c - a).dot(n) * (p - a).dot(n) > 0
    return (line - WALL_HALF) if inside else -1., tower


# ------------------------------------------------------------------ where a tree may stand
def terrace_ok(x, y, r):
    """A trunk at (x, y) with a crown of radius r on a 3.8 terrace."""
    ax = abs(x)
    in_res = 26 <= ax <= 44 and 33 <= y <= 48.6
    in_gate = 14.3 <= ax <= 44 and 48.6 <= y <= 59
    if not (in_res or in_gate):
        return False
    if y < 33 + r + .3 or y > 58.0:      # its crown not out over the lake face (the dyke and lock below), off the strip
        return False
    if ax < 14.3 + r + 1.2:                                    # the gate yard (the course)
        return False
    if y < 48.6 + r * .9 and ax < 26 + r + .4:                 # the boat tunnels' roofs
        return False
    face, tower = wall_clear(x, y)
    return face > r * .9 + .4 and tower > TOWER_CLEAR + r * .8


def bank_ok(x, y, r):
    ax = abs(x)
    if ax < WATER_EDGE + 1.1 or not (-58 <= y <= 57):
        return False
    face, tower = wall_clear(x, y)
    return face > r * .55 + .2 and tower > TOWER_CLEAR + r * .6          # (a crown may brush the wall)


# ------------------------------------------------------------------ the trees
TREE_ASSET = os.path.join(HERE, "TreeAsset_v1.blend")
with bpy.data.libraries.load(TREE_ASSET, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n.startswith("Tree ")]
MESHES = {}
for tob in dst.objects:
    me = tob.data
    for i, m in enumerate(me.materials):
        base = m.name.rsplit(".", 1)[0] if m.name[-4:-3] == "." else m.name
        if base != m.name and base in bpy.data.materials:
            me.materials[i] = bpy.data.materials[base]
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    crown = max(max(xs) - min(xs), max(ys) - min(ys)) / 2 / (max(zs) - min(zs))   # crown radius per height
    MESHES["cone" if "cone" in tob.name else "round"] = (me, crown)
print("TREE MESHES", {k: (len(v[0].polygons), round(v[1], 3)) for k, v in MESHES.items()})


def plant(region, s, count, heights, cone_share, ok, ground):
    """Darts into the region on side s until `count` trees stand (or the ground is full)."""
    placed = []
    tries = 0
    while len(placed) < count and tries < 6000:
        tries += 1
        kind = "cone" if RND.random() < cone_share else "round"
        h = RND.uniform(*heights)
        r = MESHES[kind][1] * h
        x0, x1, y0, y1 = region
        x, y = s * RND.uniform(x0, x1), RND.uniform(y0, y1)
        if not ok(x, y, r):
            continue
        gap = RND.uniform(.75, 1.25)
        if any(math.hypot(x - px, y - py) < (r + pr) * gap * .8 for px, py, pr, _, _ in placed):
            continue
        placed.append((x, y, r, kind, h))
    return [(x, y, ground, r, kind, h) for x, y, r, kind, h in placed]


def plant_bank(s, ground):
    """The bank is a strip 2 .. 5 m deep where a tree fits (y -25 .. 32): darts thrown along it stand in a row (승규 "너무 일렬로 배치 하지 마").
    Instead, groves: 3 or 4 a side at random along it, open stretches of bank between, each of 1 .. 3 trees
    thrown round its middle, at any depth - by the water (a crown out over it) or close in under the wall."""
    placed = []
    ys = sorted(RND.uniform(-21, 28) for _ in range(RND.choice((3, 4))))   # (the bank is wide enough at -25 .. 32)
    groves = []
    for y in ys:                                                    # at least 14 m between the groves' middles
        if all(abs(y - g) > 12 for g in groves):
            groves.append(y)
    for gy in groves:
        want, tries, got = RND.choice((1, 2, 2, 3)), 0, 0
        while got < want and tries < 800:
            tries += 1
            kind = "cone" if RND.random() < .6 else "round"
            h = RND.uniform(6.5, 9.0) if kind == "cone" else RND.uniform(6.0, 7.5)
            r = MESHES[kind][1] * h
            x, y = s * RND.uniform(46.9, 53.0), gy + RND.uniform(-5.5, 5.5)
            if not bank_ok(x, y, r):
                continue
            if any(math.hypot(x - px, y - py) < (r + pr) * RND.uniform(.6, .85) for px, py, pr, _, _ in placed):
                continue
            placed.append((x, y, r, kind, h))
            got += 1
    return [(x, y, ground, r, kind, h) for x, y, r, kind, h in placed]


TREES = []
for s in (1, -1):
    TREES += plant((15, 44, 34, 58.5), s, 7, (8.0, 10.5), .45, terrace_ok, UPPER)
    TREES += plant_bank(s, BANK)
print("TREES", len(TREES))

counter = {1: 0, -1: 0}
tris = 0
for x, y, gz, r, kind, h in sorted(TREES, key=lambda t: (t[0] < 0, -t[1])):
    s = 1 if x > 0 else -1
    counter[s] += 1
    me0, _ = MESHES[kind]
    me = me0.copy()
    lean = Matrix.Rotation(math.radians(RND.uniform(-2.5, 2.5)), 4,
                           Vector((math.cos(RND.uniform(0, 6.3)), math.sin(RND.uniform(0, 6.3)), 0)).normalized())
    m = (Matrix.Translation((x, y, gz - .08)) @ lean @ Matrix.Rotation(RND.uniform(0, 2 * math.pi), 4, "Z")
         @ Matrix.Scale(h / .98, 4))
    me.transform(m)
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(50))
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        n = p.normal
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co.x, co.y) if abs(n.z) > .7 else ((co.y, co.z) if abs(n.x) > abs(n.y) else (co.x, co.z))
    name = "Garden tree %s%d" % ("E" if s > 0 else "W", counter[s])
    me.name = name
    ob = bpy.data.objects.new(name, me)
    DRESS.objects.link(ob)
    tris += sum(len(p.vertices) - 2 for p in me.polygons)
    print("TREE %-16s %-5s h %.1f r %.1f at (%.1f, %.1f, %.1f)" % (name, kind, h, r, x, y, gz))
print("TREES TRIS", tris)

# ------------------------------------------------------------------ the castle for the renders (linked, not exported)
WALLS = os.path.join(HERE, "WallsSample_v2.blend")
with bpy.data.libraries.load(WALLS, link=True) as (src, dst):
    dst.objects = [n for n in src.objects]
ctx = coll("Context | castle (linked, not exported)")
for ob in dst.objects:
    if ob is not None and ob.type == "MESH":
        ctx.objects.link(ob)

render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "TreesSample_v1.blend"))
shoot("trees_v1", "overview", (0, -20, 95), (0, 10, 0), 22)
shoot("trees_v1", "east_bank", (40, -40, 12), (50, 0, 3), 26)
shoot("trees_v1", "east_terrace", (14, 26, 14), (34, 46, 6), 24)
shoot("trees_v1", "west_terrace", (-14, 26, 14), (-34, 46, 6), 24)
shoot("trees_v1", "west_bank", (-40, 30, 12), (-50, -5, 3), 26)
print("TREES SAMPLE DONE")
