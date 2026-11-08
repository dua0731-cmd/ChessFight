# R478 (승규 11-06, "레이아웃과 전체 구성은 유지하고, 사람이 손으로 만든 것처럼 보이도록 후처리만"): a pass over a finished
# garden sample, run after its sample script and before export_concept.py (the scripts rebuild the .blend from scratch,
# so hand edits there would be lost; this pass is repeatable - fixed seeds per file and object).
#
#   blender -b --python handmade_pass.py -- <Sample.blend> --step 2
#
# Step 2 - placement: the dressing meshes are many parts merged per kind (no linked duplicates), so the pass works on
# parts. Small decor parts (gold balls and poles, round stone balusters and urns, leaves and topiaries, banners and
# pennants, cushions, lanterns) are gathered into assemblies (parts that touch), and each assembly gets its own small
# turn, lean, size and shift, so mirrored sides, rows and repeats no longer match:
#   upright decor   turn about its own upright +-15 deg, lean +-1.5 deg, size +-6 % (seated on its foot), shift +-1.5 cm
#   balusters       turn +-12 deg, lean +-1 deg, girth +-5 %, height +0 .. 3 % (never short of their rail), shift +-1 cm
#   topiaries       freestanding ones (a pot and a cone) shift up to +-0.3 m: rows turn into loose groups and gaps
#   banners         hung things tilt +-2 deg in their own plane, size +-3 % from where they hang, shift +-3 cm
# On the pieces that ride movers in Unity (boats, platforms, spin bars, sluice gates, robots) a third as much. Masonry,
# paving, walls, rails, water and lawn are not moved (their colliders are the graybox's; steps 3 and 4 work on them).
#
# Step 3 - shape (R479): no piece keeps its ruler-straight, machine-cut shape:
#   long slender pieces  handrails, copings, beams, poles (over 2.5 m, under 0.7 m across): a sag of 0.4 .. 1.5 cm in
#                        their middle and a sideways bow of up to 0.4 cm (upright ones bow 0.4 .. 1 cm), as a lattice
#   stone blocks         each cut by hand: tilted up to 0.35 deg, its sides 1 % and its height 0.8 % off true, about
#                        its own middle (bevels and roundings move with it)
#   wear                 7 % of the blocks have a knocked corner (a top corner pushed in 2 .. 4.5 cm, falling off over
#                        8 .. 18 cm, as proportional editing); none on the pieces that ride movers
# The baked bevels stay as the sample scripts made them (rounder on the gate and the walls, finer on the kit's runs).
#
# Step 4 - weathering (R480): Blender's node tricks do not reach Unity (it draws the GC_ materials by name), so the
# weathering is baked into a vertex colour attribute "HM" that the FBX carries and PR_GardenWeathered.shader reads on
# the stone, tile and joint materials: R each piece's own tint and roughness, G grime (low on faces near the floor or
# ledge under them, under ledges, a little on floors), B moss (low and damp, at the water levels), A water streaks (down
# a face from the ledge or top over it, fading over 3 m). The shader breaks them up with noise, streaks them downward,
# and draws the stone's picture twice (turned and rescaled) mixed by a broad noise, so its tiling no longer shows.
import bpy, bmesh, sys, math, random, hashlib
from collections import defaultdict
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
BLEND = args[0]
STEP = int(args[args.index("--step") + 1]) if "--step" in args else 2
bpy.ops.wm.open_mainfile(filepath=BLEND)

SKIP_MATS = ("water", "fall", "lawn", "grout", "joint", "floor tile", "paving", "shade", "tier", "basin", "mosaic", "soil")
MOVERS = ("Boat ", "Bobbing platform", "Spin bar", "Sluice gate", "Robot ", "Shortcut post", "Shortcut lotus")
GOLD = ("gold", "bronze", "brass")
LEAF = ("leaf", "bush", "topiary", "moss band")
STONE = ("stone", "marble")
CLOTH = ("blue", "red", "velvet", "pennant", "crown", "banner")


def seed(*parts):
    return int(hashlib.md5("|".join(parts).encode()).hexdigest()[:8], 16)


def kind_of(mats):
    names = " ".join(mats).lower()
    if any(s in names for s in SKIP_MATS):
        return None
    if any(s in names for s in GOLD):
        return "gold"
    if any(s in names for s in LEAF):
        return "leaf"
    if any(s in names for s in CLOTH):
        return "cloth"
    if any(s in names for s in STONE):
        return "stone"
    return "other"


def components(bm):
    comp, cid = {}, 0
    for v in bm.verts:
        if v.index in comp:
            continue
        stack = [v]
        comp[v.index] = cid
        while stack:
            a = stack.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in comp:
                    comp[b.index] = cid
                    stack.append(b)
        cid += 1
    return comp, cid


def plan_axes(vs):
    """The plan's main axis through the points (a banner on a slanting wall is thin across the wall, not across x or
    y): its centre, the axis along, the one across, and the widths along and across."""
    mx = sum(v.co.x for v in vs) / len(vs)
    my = sum(v.co.y for v in vs) / len(vs)
    sxx = sum((v.co.x - mx) ** 2 for v in vs)
    syy = sum((v.co.y - my) ** 2 for v in vs)
    sxy = sum((v.co.x - mx) * (v.co.y - my) for v in vs)
    ang = .5 * math.atan2(2 * sxy, sxx - syy)
    along = Vector((math.cos(ang), math.sin(ang), 0))
    thin = Vector((-along.y, along.x, 0))
    da = [v.co.x * along.x + v.co.y * along.y for v in vs]
    dt = [v.co.x * thin.x + v.co.y * thin.y for v in vs]
    return mx, my, along, thin, max(da) - min(da), max(dt) - min(dt)


def plan_widths(vs):
    return plan_axes(vs)[4:]


def step2(ob):
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    comp, n = components(bm)
    verts = defaultdict(list)
    for v in bm.verts:
        verts[comp[v.index]].append(v)
    mats = defaultdict(set)
    for f in bm.faces:
        if me.materials:
            m = me.materials[f.material_index]
            mats[comp[f.verts[0].index]].add(m.name if m else "-")
    box, kind = {}, {}
    for c, vs in verts.items():
        lo = Vector((min(v.co.x for v in vs), min(v.co.y for v in vs), min(v.co.z for v in vs)))
        hi = Vector((max(v.co.x for v in vs), max(v.co.y for v in vs), max(v.co.z for v in vs)))
        box[c] = (lo, hi)
        size = hi - lo
        k = kind_of(mats[c])
        big = max(size)
        if k is None or big > 4.5:
            continue
        if k == "stone":
            # round stone pieces only (balusters, urns, pots): a lathe, about as wide as deep, not a block
            if len(vs) < 60 or big > 1.4 or abs(size.x - size.y) > .15 * max(size.x, size.y, 1e-6):
                continue
        w_along, w_thin = plan_widths(vs)
        flat = w_thin < .25 * max(w_along, 1e-6)
        if flat:
            if big > 6.0:
                continue                              # a flat thing that big is a wall's or a gate's
        elif (k in ("gold", "other") and big > 2.4) or (k == "cloth" and big > 1.4):
            continue                                  # cushions, lanterns may go; hulls and roofs stay
        kind[c] = k
    # assemblies: decor parts whose boxes touch (1 cm), by a coarse grid
    parent = {c: c for c in kind}

    def find(c):
        while parent[c] != c:
            parent[c] = parent[parent[c]]
            c = parent[c]
        return c
    cell = defaultdict(list)
    for c in kind:
        lo, hi = box[c]
        for gx in range(int(math.floor(lo.x)), int(math.floor(hi.x)) + 1):
            for gy in range(int(math.floor(lo.y)), int(math.floor(hi.y)) + 1):
                cell[(gx, gy)].append(c)
    for members in cell.values():
        for i, a in enumerate(members):
            la, ha = box[a]
            for b in members[i + 1:]:
                lb, hb = box[b]
                if all(la[k] <= hb[k] + .01 and lb[k] <= ha[k] + .01 for k in range(3)):
                    ra, rb = find(a), find(b)
                    if ra != rb:
                        parent[ra] = rb
    groups = defaultdict(list)
    for c in kind:
        groups[find(c)].append(c)
    amp = .33 if ob.name.startswith(MOVERS) else 1.
    rnd = random.Random(seed(bpy.path.basename(BLEND), ob.name, "step2"))
    moved = defaultdict(int)
    for g in sorted(groups.values(), key=lambda g: min(g)):
        lo = Vector((min(box[c][0].x for c in g), min(box[c][0].y for c in g), min(box[c][0].z for c in g)))
        hi = Vector((max(box[c][1].x for c in g), max(box[c][1].y for c in g), max(box[c][1].z for c in g)))
        size = hi - lo
        kinds = {kind[c] for c in g}
        vs = [v for c in g for v in verts[c]]
        if len(vs) < 12 or max(size) < .1:
            continue                                  # petals, buds: already strewn at random
        mx, my, along, thin, w_along, w_thin = plan_axes(vs)
        flat = w_thin < .25 * w_along and (size.z > .8 or "cloth" in kinds)
        if flat:
            # a hung or flat thing (banner, pennant, badge): tilt in its own plane about where it hangs, a little size
            # and shift along it, never turned toward its wall
            hang = Vector((mx, my, hi.z))
            m = (Matrix.Translation(hang + along * rnd.uniform(-.03, .03) * amp + Vector((0, 0, rnd.uniform(-.03, .02) * amp)))
                 @ Matrix.Rotation(math.radians(rnd.uniform(-2, 2) * amp), 4, thin)
                 @ Matrix.Scale(1 + rnd.uniform(-.03, .03) * amp, 4)
                 @ Matrix.Translation(-hang))
            moved["banner"] += 1
        else:
            foot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
            baluster = kinds == {"stone"} and size.z > 1.8 * max(size.x, size.y)
            topiary = "leaf" in kinds and ("stone" in kinds or "gold" in kinds) and size.z > 1.5
            turn = math.radians(rnd.uniform(-15, 15) * amp)
            tilt_axis = Vector((math.cos(rnd.uniform(0, 6.283)), math.sin(rnd.uniform(0, 6.283)), 0)).normalized()
            if baluster:
                lean, girth, height, shift = 1., 1 + rnd.uniform(-.05, .05) * amp, 1 + rnd.uniform(0, .03) * amp, .01
                turn = math.radians(rnd.uniform(-12, 12) * amp)
                moved["baluster"] += 1
            else:
                lean = 1.5
                s = 1 + rnd.uniform(-.06, .06) * amp
                girth, height, shift = s, s, .015
                moved["topiary" if topiary else "decor"] += 1
            if topiary:
                shift = .3
            off = Vector((rnd.uniform(-shift, shift), rnd.uniform(-shift, shift), 0)) * amp
            m = (Matrix.Translation(foot + off)
                 @ Matrix.Rotation(math.radians(rnd.uniform(-lean, lean) * amp), 4, tilt_axis)
                 @ Matrix.Rotation(turn, 4, "Z")
                 @ Matrix.Diagonal((girth, girth, height, 1))
                 @ Matrix.Translation(-foot))
        for v in vs:
            v.co = m @ v.co
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.update()
    return moved


def step3(ob):
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    comp, n = components(bm)
    verts = defaultdict(list)
    for v in bm.verts:
        verts[comp[v.index]].append(v)
    mats = defaultdict(set)
    for f in bm.faces:
        if me.materials:
            m = me.materials[f.material_index]
            mats[comp[f.verts[0].index]].add(m.name if m else "-")
    mover = ob.name.startswith(MOVERS)
    amp = .33 if mover else 1.
    rnd = random.Random(seed(bpy.path.basename(BLEND), ob.name, "step3"))
    done_ = defaultdict(int)
    for c in sorted(verts):
        vs = verts[c]
        k = kind_of(mats[c])
        if k is None or len(vs) < 8:
            continue
        lo = Vector((min(v.co.x for v in vs), min(v.co.y for v in vs), min(v.co.z for v in vs)))
        hi = Vector((max(v.co.x for v in vs), max(v.co.y for v in vs), max(v.co.z for v in vs)))
        size = hi - lo
        order = sorted(range(3), key=lambda i: size[i])
        big, mid = size[order[2]], size[order[1]]
        if big > 2.5 and mid < .7 and k in ("stone", "gold", "other"):
            ax = order[2]
            if ax == 2:
                side = Vector((math.cos(rnd.uniform(0, 6.283)), math.sin(rnd.uniform(0, 6.283)), 0)).normalized()
                bow, sag = rnd.uniform(.004, .01) * amp, 0.
            else:
                other = 1 - ax
                side = Vector((0, 0, 0))
                side[other] = 1.
                bow = rnd.uniform(-.004, .004) * amp
                sag = rnd.uniform(.004, .015) * amp * min(1., big / 8.)
            for v in vs:
                t = (v.co[ax] - lo[ax]) / size[ax]
                f = 4 * t * (1 - t)
                v.co += side * bow * f
                v.co.z -= sag * f
            done_["bent"] += 1
            continue
        if k != "stone" or big > 3.5 or big < .2:
            continue
        if len(vs) >= 60 and abs(size.x - size.y) <= .15 * max(size.x, size.y):
            continue                                  # round turned stone (balusters, urns): step 2's
        mid_pt = (lo + hi) / 2
        axis = Vector((math.cos(rnd.uniform(0, 6.283)), math.sin(rnd.uniform(0, 6.283)), 0)).normalized()
        m = (Matrix.Translation(mid_pt)
             @ Matrix.Rotation(math.radians(rnd.uniform(-.35, .35) * amp), 4, axis)
             @ Matrix.Diagonal((1 + rnd.uniform(-.01, .01) * amp, 1 + rnd.uniform(-.01, .01) * amp,
                                1 + rnd.uniform(-.008, .008) * amp, 1))
             @ Matrix.Translation(-mid_pt))
        for v in vs:
            v.co = m @ v.co
        done_["block"] += 1
        if not mover and rnd.random() < .07:
            corner = Vector((rnd.choice((lo.x, hi.x)), rnd.choice((lo.y, hi.y)), hi.z))
            centre = sum((v.co for v in vs), Vector()) / len(vs)
            r = rnd.uniform(.08, .18) * min(1., big)
            d = rnd.uniform(.02, .045) * min(1., big / .6)
            corner = min(vs, key=lambda v: (v.co - corner).length).co.copy()
            for v in vs:
                dist = (v.co - corner).length
                if dist < r:
                    v.co += (centre - v.co).normalized() * d * (1 - dist / r) ** 2
            done_["knocked"] += 1
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.update()
    return done_


WATER_LEVELS = (-1.9, -.55, 3.0)        # the moat, the lake, the reservoir (water_tiers.py)
CELL = .5


def water_here(x, y):
    """The water a face stands in where nothing is under it: the reservoir, else the lake north of the weirs' line,
    else the moat (RoyalWaterGardenBuilder.Water)."""
    if abs(x) < 26 and 33 < y < 48.6:
        return 3.0
    return -.55 if y > -15.5 else -1.9


def up_surfaces(objects):
    """Where the open upward faces are (floors, ledges, copings, tops) on a 0.5 m plan grid: cell -> sorted heights.
    A block's top with the next block's bed 2 .. 10 cm over it (a joint in a wall) is not open."""
    ups, downs = defaultdict(list), defaultdict(list)
    for ob in objects:
        me = ob.data
        mw = ob.matrix_world
        for p in me.polygons:
            n = (mw.to_3x3() @ p.normal).normalized()
            if abs(n.z) < .7:
                continue
            pts = [mw @ me.vertices[i].co for i in p.vertices]
            z = sum(q.z for q in pts) / len(pts)
            x0, x1 = min(q.x for q in pts), max(q.x for q in pts)
            y0, y1 = min(q.y for q in pts), max(q.y for q in pts)
            to = ups if n.z > 0 else downs
            for gx in range(int(math.floor(x0 / CELL)), int(math.floor(x1 / CELL)) + 1):
                for gy in range(int(math.floor(y0 / CELL)), int(math.floor(y1 / CELL)) + 1):
                    to[(gx, gy)].append(z)
    out = {}
    for k, v in ups.items():
        beds = downs.get(k, [])
        out[k] = sorted({round(z, 3) for z in v if not any(-.005 < b - z < .1 for b in beds)})
    return out


def step4(ob, ups):
    """Weathering baked into a float colour attribute "HM" (exported as the FBX's vertex colours) that
    PR_GardenWeathered reads in Unity: R the piece's own tint and roughness (0.5 as made), G grime (low on a face, near
    the floor or ledge under it, in corners), B moss (low and damp: near the floor and at the water's levels), A water
    streaks (down a face from the ledge or top over it)."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    comp, n = components(bm)
    rnd = random.Random(seed(bpy.path.basename(BLEND), ob.name, "step4"))
    tint = [rnd.random() for _ in range(n)]
    bm.normal_update()
    mw = ob.matrix_world
    nm = mw.to_3x3()
    values = []
    for v in bm.verts:
        p = mw @ v.co
        nrm = (nm @ v.normal).normalized()
        hs = ups.get((int(math.floor(p.x / CELL)), int(math.floor(p.y / CELL))), [])
        below = [z for z in hs if z <= p.z + .03]
        above = [z for z in hs if z > p.z + .05]
        ground = below[-1] if below else None
        ledge = above[0] if above else None
        h = (p.z - ground) if ground is not None else 9.
        upward = nrm.z > .7
        side = abs(nrm.z) < .6
        grime = (.12 + .1 * rnd.random()) if upward else max(0., 1 - h / 1.4) ** 1.5
        if nrm.z < -.6:
            grime = max(grime, .35)                   # undersides of ledges
        # damp only where water is: the water of that place (water_here), under a face standing in it or level with
        # the floor under it (a pool's sheet, a step at the waterline) - not any ledge that happens to be near a level
        here = water_here(p.x, p.y)
        wet = [here] if ground is None or abs(ground - here) < .3 else []
        damp = max((max(0., 1 - max(w - p.z, 0) / .3 - max(p.z - w, 0) / .45) for w in wet), default=0.)
        moss = min(1., .3 * max(0., 1 - h / .7) ** 2 + .9 * damp) if not upward else .15 * damp
        streak = 0.
        if side and ledge is not None:
            streak = max(0., 1 - (ledge - p.z) / 3.0) ** 1.3
        values.append((tint[comp[v.index]], min(1., grime), moss, streak))
    bm.free()
    attr = me.color_attributes.get("HM") or me.color_attributes.new("HM", "FLOAT_COLOR", "POINT")
    for i, c in enumerate(values):
        attr.data[i].color = c
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = me.color_attributes.find("HM")
    return {"verts": len(values)}


done = bpy.context.scene.get("handmade_steps", "")
if str(STEP) in done.split(","):
    print("HANDMADE STEP", STEP, "already applied to", bpy.path.basename(BLEND))
    sys.exit(0)
total = defaultdict(int)
dressing = [ob for col in bpy.data.collections if "dressing" in col.name.lower() and not col.name.startswith("Context")
            for ob in col.all_objects if ob.type == "MESH"]
if STEP == 4:
    # every mesh gets the colours (a stone part drawn weathered in Unity must have them; white would read as all dirt)
    ups = up_surfaces(dressing)
    for ob in dressing:
        if ob.name.startswith(("Water ", "Fall ")):
            continue                                  # their vertex colours are their own shaders' data (R440)
        for k, v in step4(ob, ups).items():
            total[k] += v
else:
    for ob in dressing:
        if ob.name.startswith(("Water ", "Fall ", "Castle lawn", "Canal geysers")):   # geysers: game spots
            continue
        for k, v in ({2: step2, 3: step3}[STEP])(ob).items():
            total[k] += v
bpy.context.scene["handmade_steps"] = ",".join(s for s in (done, str(STEP)) if s)   # run once per step
bpy.ops.wm.save_mainfile(filepath=BLEND)
print("HANDMADE STEP", STEP, bpy.path.basename(BLEND), dict(total))
