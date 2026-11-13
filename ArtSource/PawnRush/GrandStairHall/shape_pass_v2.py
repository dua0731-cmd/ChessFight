# Shape pass on the grand stair hall blockout (grand_stair_hall.blend -> grand_stair_hall_v2.blend).
# Nothing moves and nothing changes size; the walked tops keep their heights:
#   - the round hall wall and the spiral's chunked ways (curving stair, north landing, dive landings, windy way,
#     last stair) are rebuilt as smooth curved strips through the same chunk edges (about 1 degree a slice);
#     the chunks they replace go to the hidden collection "_Blockout originals"
#   - every built piece (not the imported obstacles) gets a small bevel and smooth-by-angle shading
# Run: blender -b grand_stair_hall.blend -P shape_pass_v2.py
import bpy, bmesh, math, os, re
from collections import defaultdict
from mathutils import Vector

HUB = Vector((0.0, 92.0))          # Unity (0, y, 92): the round hall's centre, Blender X / Y
SLICE = math.radians(1.0)
CHAINS = {"B3_Spiral": ["Curving_stair", "North_landing", "Dive_landing_1", "Dive_landing_2", "Dive_landing_3",
                        "Windy_way", "Last_stair"],
          "Hall": ["Hall_wall_(round)"]}

HERE = os.path.dirname(bpy.data.filepath)
root = bpy.data.collections["Mid_2-1_GrandStairHall"]
originals = bpy.data.collections.new("_Blockout originals")
root.children.link(originals)


def piece_name(ob):
    return re.sub(r"#\d+$", "", ob.name).split("/")[-1]


def polar(p):
    d = Vector((p.x, p.y)) - HUB
    return math.atan2(d.y, d.x), d.length


def unwrap(a, ref):
    while a - ref > math.pi: a -= 2 * math.pi
    while a - ref < -math.pi: a += 2 * math.pi
    return a


def chunk_sections(ob):
    """The chunk's start and end edges on its top face: (phi, r_in, r_out, z_in, z_out), and its thickness."""
    # Unity cubes carry 24 vertices (one set per face): the 8 corners once each.
    vs = list({tuple(round(x, 3) for x in (ob.matrix_world @ v.co)): ob.matrix_world @ v.co
               for v in ob.data.vertices}.values())
    c = sum(vs, Vector()) / len(vs)
    ref, _ = polar(c)
    pts = [(unwrap(polar(v)[0], ref), polar(v)[1], v.z) for v in vs]
    top = sorted(pts, key=lambda p: -p[2])[:4]
    top.sort(key=lambda p: p[0])
    ends = []
    for pair in (top[:2], top[2:]):
        a, b = sorted(pair, key=lambda p: p[1])
        ends.append(((a[0] + b[0]) / 2, a[1], b[1], a[2], b[2]))
    thick = min(p[2] for p in top) - min(p[2] for p in pts)
    return ends[0], ends[1], thick, ref


def chunk_order(chunks):
    def num(ob):
        m = re.search(r"_(\d+)$", piece_name(ob))
        return int(m.group(1)) if m and not piece_name(ob).startswith("Hall_wall") else None
    if all(num(o) is not None for o in chunks):
        return [sorted(chunks, key=num)]
    # The round wall: by angle, split where a gap opens (its south and north doors).
    data = sorted(((polar(sum((o.matrix_world @ v.co for v in o.data.vertices), Vector()) / len(o.data.vertices))[0], o)
                   for o in chunks), key=lambda t: t[0])
    steps = [(data[(i + 1) % len(data)][0] - data[i][0]) % (2 * math.pi) for i in range(len(data))]
    typical = sorted(steps)[len(steps) // 2]
    cut = max(range(len(steps)), key=lambda i: steps[i])
    data = data[cut + 1:] + data[:cut + 1]
    runs, run = [], [data[0][1]]
    for (a0, _), (a1, o1) in zip(data, data[1:]):
        if (a1 - a0) % (2 * math.pi) > typical * 1.6:
            runs.append(run); run = []
        run.append(o1)
    runs.append(run)
    return runs


def smooth_strip(name, chunks, collection):
    secs, thicks = [], []
    prev = None
    for ob in chunks:
        s0, s1, t, ref = chunk_sections(ob)
        if prev is not None:
            # keep the angles continuous along the chain
            shift = round((prev - s0[0]) / (2 * math.pi)) * 2 * math.pi
            s0 = (s0[0] + shift,) + s0[1:]
            s1 = (s1[0] + shift,) + s1[1:]
        if secs:
            p = secs[-1]
            secs[-1] = tuple((x + y) / 2 for x, y in zip(p, s0))   # the overlap between two chunks: meet halfway
            thicks[-1] = (thicks[-1] + t) / 2
        else:
            secs.append(s0); thicks.append(t)
        secs.append(s1); thicks.append(t)
        prev = s1[0]
    if secs[-1][0] < secs[0][0]:
        secs.reverse(); thicks.reverse()
    rings = []
    for (a, ta), (b, tb) in zip(zip(secs, thicks), zip(secs[1:], thicks[1:])):
        n = max(1, int(abs(b[0] - a[0]) / SLICE))
        for k in range(n):
            f = k / n
            rings.append((tuple(x + (y - x) * f for x, y in zip(a, b)), ta + (tb - ta) * f))
    rings.append((secs[-1], thicks[-1]))

    bm = bmesh.new()
    loops = []
    for (phi, ri, ro, zi, zo), t in rings:
        d = Vector((math.cos(phi), math.sin(phi)))
        pi, po = HUB + d * ri, HUB + d * ro
        bottom = min(zi, zo) - t
        loops.append([bm.verts.new((pi.x, pi.y, zi)), bm.verts.new((po.x, po.y, zo)),
                      bm.verts.new((po.x, po.y, bottom)), bm.verts.new((pi.x, pi.y, bottom))])
    for a, b in zip(loops, loops[1:]):
        for i in range(4):
            bm.faces.new((a[i], a[(i + 1) % 4], b[(i + 1) % 4], b[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in chunks[0].data.materials:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    ob["replaces"] = ", ".join(o.name for o in chunks)
    for o in chunks:
        for c in list(o.users_collection): c.objects.unlink(o)
        originals.objects.link(o)
    return ob, rings


made = []
for group, prefixes in CHAINS.items():
    col = bpy.data.collections[group]
    by = defaultdict(list)
    for ob in col.objects:
        if ob.type == "MESH" and len(ob.name.split("/")) == 2:
            p = re.sub(r"_\d+$", "", piece_name(ob))
            if p in prefixes: by[p].append(ob)
    for p in prefixes:
        for i, run in enumerate(chunk_order(by[p])):
            name = f"{group}/{p} (smooth{'' if i == 0 else ' ' + str(i + 1)})"
            made.append((smooth_strip(name, run, col)[0], run))

# Check: the new top under each original chunk's top-face centre.
worst = 0.0
for ob, run in made:
    for c in run:
        vs = [c.matrix_world @ v.co for v in c.data.vertices]
        top = sorted(vs, key=lambda v: -v.z)[:4]
        p = sum(top, Vector()) / 4
        res = ob.ray_cast(ob.matrix_world.inverted() @ Vector((p.x, p.y, p.z + 3)), Vector((0, 0, -1)), distance=6)
        if res[0]:
            worst = max(worst, abs((ob.matrix_world @ res[1]).z - p.z))
        else:
            print("NOHIT", ob.name, c.name)
print("SMOOTH strips", len(made), "max top difference %.3f m" % worst)

# Bevel + smooth shading on the built pieces (group/piece), not the imported obstacles' parts.
bevelled = 0
for ob in bpy.data.objects:
    if ob.type != "MESH" or ob.name.startswith("H ") or "_Blockout originals" in [c.name for c in ob.users_collection]:
        continue
    parts = re.sub(r"#\d+$", "", ob.name).split("/")
    if len(parts) != 2 or parts[1][:1].isdigit() or parts[1].startswith(("Glass_stones",)):
        continue
    dims = sorted(ob.dimensions)
    if dims[0] < 0.06:
        continue                     # painted lines and thin plates
    mod = ob.modifiers.new("Bevel", "BEVEL")
    mod.width = min(0.05, dims[0] * 0.2)
    mod.segments = 2
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(35)
    mod.harden_normals = True
    mod.use_clamp_overlap = True
    bevelled += 1
bpy.ops.object.select_all(action="DESELECT")
for ob in bpy.data.objects:
    if ob.type == "MESH":
        ob.select_set(True)
bpy.context.view_layer.objects.active = next(o for o in bpy.data.objects if o.type == "MESH")
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
bpy.ops.object.select_all(action="DESELECT")
print("BEVELLED", bevelled)

lc = bpy.context.view_layer.layer_collection.children[root.name].children[originals.name]
lc.hide_viewport = True
originals.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "grand_stair_hall_v2.blend"))
print("SAVED")
