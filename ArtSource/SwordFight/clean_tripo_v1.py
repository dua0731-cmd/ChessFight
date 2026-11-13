"""Turn the Tripo GLBs (tripo_raw/) into the study's props (README section 3 and 10).

blender -b --factory-startup --python clean_tripo_v1.py -- <raw_dir> <out_dir>
Tripo gave untextured, triangulated meshes far over budget (10-28: 52k-154k triangles each), so each is
decimated, scaled to the game's size, given a bottom-centre pivot, and split into colour slots by region.
A slot's material is named SF_<rrggbb> (SF_<rrggbb>_m for metal): the game makes its colour from the name.
  SF_Prop_BookStack_A..D  four stacks of 7-8 books (three Tripo books, recoloured), 5.35 m tall
  SF_Prop_ChessClock      the clock; empties Front (in front of it), Dial_L / Dial_R (the dials' centres)
  SF_Prop_Lamp            the floor lamp, pivot on its pole's foot; empty Light_Anchor (under the shade)
Then the props are set round the arena of build_arena_v1.py (its .blend) and rendered.
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, OUT = args[0], args[1]
os.makedirs(OUT, exist_ok=True)

STACK_HEIGHT = 5.35            # desk top (-6) to just under the squares (-0.65)
BOOK_COLOURS = ["7a2b2b", "2f4f3a", "2c3b5e", "9a7a3a", "5b3a5e", "3d3d3d"]   # the demo's six
PAGES = "e3d6b8"
CLOCK_WOOD, CLOCK_DIAL, BRONZE, SHADE = "3a2618", "efe6d2", "5a4a3a_m", "1f4a35"
CLOCK_WIDTH = 4.2
LAMP_REACH = math.hypot(5.5, 2.0)   # pole (-12.5, 10) to shade (-7, 8), Unity x / z at 2 m squares


def mat(code):
    name = "SF_" + code
    m = bpy.data.materials.get(name)
    if m:
        return m
    hexs = code.split("_")[0]
    rgb = [int(hexs[k:k + 2], 16) / 255 for k in (0, 2, 4)]
    rgb = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in rgb]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Metallic"].default_value = 0.8 if code.endswith("_m") else 0.0
    bsdf.inputs["Roughness"].default_value = 0.45 if code.endswith("_m") else 0.7
    m.diffuse_color = (*rgb, 1)
    return m


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def load(name, budget):
    """The GLB as one mesh object, transforms applied, decimated to about <budget> triangles."""
    bpy.ops.import_scene.gltf(filepath=os.path.join(RAW, name + ".glb"))
    imported = list(bpy.context.selected_objects)
    meshes = [o for o in imported if o.type == "MESH"]
    activate(meshes[0])
    for o in meshes:
        o.select_set(True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for junk in [x for x in imported if x.type != "MESH"]:
        bpy.data.objects.remove(junk)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > budget:
        mod = o.modifiers.new("decimate", "DECIMATE")
        mod.ratio = budget / tris
        activate(o)
        bpy.ops.object.modifier_apply(modifier="decimate")
    o.data.materials.clear()
    o.name = name
    return o


def bounds(o):
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def tris_of(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def smooth(o):
    activate(o)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    except Exception:
        pass


def empty(name, parent, at):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.2
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = at
    return e


def export(o, extra=()):
    activate(o)
    for e in extra:
        e.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, o.name + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    lo, hi = bounds(o)
    print(f"[props] {o.name}: {tuple(round(d, 2) for d in hi - lo)} m, {tris_of(o)} tris, slots {[m.name for m in o.data.materials]}")


# ---------------------------------------------------------------- books


def book(name, budget=700):
    """One Tripo book, long side along X, bottom centre at the origin; faces tagged 0 cover, 1 pages.
    The pages: the faces in the middle height band that face one of the three sides the spine is not on
    (the spine side is the one whose middle band reaches as far out as the covers do)."""
    o = load(name, budget)
    lo, hi = bounds(o)
    size = hi - lo
    if size.y > size.x:   # long side along X
        o.data.transform(Matrix.Rotation(math.pi / 2, 4, "Z"))
        lo, hi = bounds(o)
        size = hi - lo
    o.data.transform(Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    lo, hi = bounds(o)
    h = hi.z
    band = [v.co for v in o.data.vertices if 0.2 * h < v.co.z < 0.8 * h]
    sides = {(1, 0): hi.x, (-1, 0): -lo.x, (0, 1): hi.y, (0, -1): -lo.y}
    gap = {s: lim - max(v.x * s[0] + v.y * s[1] for v in band) for s, lim in sides.items()}
    spine = min(gap, key=gap.get)
    for p in o.data.polygons:
        c, n = p.center, p.normal
        p.material_index = 0
        if 0.18 * h < c.z < 0.82 * h and abs(n.z) < 0.5:
            facing = max(sides, key=lambda s: n.x * s[0] + n.y * s[1])
            if facing != spine and n.x * facing[0] + n.y * facing[1] > 0.5:
                p.material_index = 1
    o.hide_render = True
    o.hide_set(True)
    return o


# kind -> (source book, footprint x, footprint y, height range)
KINDS = {
    "ledger": ("book_ledger", 3.4, 2.8, (0.8, 0.95)),
    "thick": ("book_thick", 3.1, 2.5, (0.72, 0.88)),
    "thin": ("book_thin", 2.9, 2.3, (0.45, 0.6)),
}
ORDER = ["ledger", "thick", "thin", "thick", "ledger", "thin", "thick", "thin", "thick"]


def stack(index, sources):
    """SF_Prop_BookStack_<A..D>: books from the bottom up till 5.35 m, every height scaled so the top is exactly there."""
    rng = random.Random(10 + index)
    picks, total = [], 0.0
    n = 0
    while total < STACK_HEIGHT - 0.3:
        kind = ORDER[(index * 3 + n) % len(ORDER)] if n else "ledger"
        h = rng.uniform(*KINDS[kind][3])
        picks.append((kind, h))
        total += h
        n += 1
    fit = STACK_HEIGHT / total
    parts, z = [], 0.0
    for n, (kind, h) in enumerate(picks):
        src_name, fx, fy, _ = KINDS[kind]
        src = sources[src_name]
        lo, hi = bounds(src)
        sx, sy, sz = fx * rng.uniform(0.92, 1.0) / (hi.x - lo.x), fy * rng.uniform(0.92, 1.0) / (hi.y - lo.y), h * fit / hi.z
        yaw = ((index + n) % 5 - 2) * 0.06 + rng.uniform(-0.03, 0.03)
        if rng.random() < 0.5:
            yaw += math.pi   # the spine on the other side
        me = src.data.copy()
        me.transform(Matrix.Translation((rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08), z))
                     @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Diagonal((sx, sy, sz, 1)))
        # (The source has no slots; Mesh.materials.clear() would also reset every face's slot to 0.)
        me.materials.append(mat(BOOK_COLOURS[(index + n) % 6]))
        me.materials.append(mat(PAGES))
        part = bpy.data.objects.new(f"book {n}", me)
        bpy.context.scene.collection.objects.link(part)
        parts.append(part)
        z += h * fit
    activate(parts[0])
    for p in parts:
        p.select_set(True)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = "SF_Prop_BookStack_" + "ABCD"[index]
    smooth(o)
    return o


# ---------------------------------------------------------------- the clock


def clock():
    """SF_Prop_ChessClock: 4.2 m wide, the dials on its front (-Y here, +Z in Unity). Slots: wood, dial, bronze.
    The dials are found as the front-most rings in the clock's middle height, left and right."""
    o = load("chess_clock", 6000)
    lo, hi = bounds(o)
    s = CLOCK_WIDTH / (hi.x - lo.x)
    o.data.transform(Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    lo, hi = bounds(o)
    h, depth = hi.z, hi.y - lo.y
    # The base moulding sticks out further than the dials: measure "front-most" within the dials' height only.
    mid = [v.co.copy() for v in o.data.vertices if 0.36 * h < v.co.z < 0.92 * h]
    face = min(v.y for v in mid)
    front = [v for v in mid if v.y < face + 0.06 * depth]
    dials = []
    for side in (-1, 1):
        ring = [v for v in front if v.x * side > 0.15]
        c = sum(ring, Vector()) / max(len(ring), 1)
        r = sum(math.hypot(v.x - c.x, v.z - c.z) for v in ring) / max(len(ring), 1)
        dials.append((c, r))
        print(f"[props] clock dial {side:+d}: centre {tuple(round(x, 2) for x in c)}, ring radius {r:.2f}")
    for p in o.data.polygons:
        c, n = p.center, p.normal
        p.material_index = 0
        if c.z > hi.z - 0.05 * h:
            p.material_index = 2   # the push buttons on top
        for dc, r in dials:
            d = math.hypot(c.x - dc.x, c.z - dc.z)
            if n.y < -0.5 and c.y < lo.y + 0.25 * depth:
                if d < r * 0.92:
                    p.material_index = 1
                elif d < r * 1.18:
                    p.material_index = 2   # the bezel
    for code in (CLOCK_WOOD, CLOCK_DIAL, BRONZE):
        o.data.materials.append(mat(code))
    o.name = "SF_Prop_ChessClock"
    smooth(o)
    marks = [empty("Front", o, (0, lo.y - 1.0, h / 2))]
    for name, (dc, r) in zip(("Dial_L", "Dial_R"), dials):
        marks.append(empty(name, o, (dc.x, lo.y - 0.02, dc.z)))
    return o, marks


# ---------------------------------------------------------------- the lamp


def lamp():
    """SF_Prop_Lamp: pivot on the pole's foot, scaled so the shade is 5.85 m out from the pole (where the game's lamp
    hangs it). Slots: bronze, shade. Light_Anchor under the shade's middle."""
    o = load("lamp", 8000)
    lo, hi = bounds(o)
    h = hi.z - lo.z
    pole = [v.co for v in o.data.vertices if lo.z + 0.3 * h < v.co.z < lo.z + 0.6 * h]
    axis = Vector((sum(v.x for v in pole) / len(pole), sum(v.y for v in pole) / len(pole), lo.z))
    o.data.transform(Matrix.Translation(-axis))
    lo, hi = bounds(o)
    h = hi.z
    # The shade: high, away from the pole, below the arm.
    def out(v):
        return math.hypot(v.x, v.y)
    top = [v.co for v in o.data.vertices if v.co.z > 0.72 * h]
    reach_max = max(out(v) for v in top)
    shade_vs = [v for v in top if out(v) > 0.35 * reach_max and v.z < 0.9 * h]
    sc = sum(shade_vs, Vector()) / len(shade_vs)
    bottom = min(v.z for v in shade_vs)
    s = LAMP_REACH / math.hypot(sc.x, sc.y)
    o.data.transform(Matrix.Diagonal((s, s, s, 1)))
    sc, bottom, h, reach_max = sc * s, bottom * s, h * s, reach_max * s
    for p in o.data.polygons:
        c = p.center
        p.material_index = 1 if (c.z > 0.72 * h and out(c) > 0.35 * reach_max and c.z < 0.9 * h) else 0
    for code in (BRONZE, SHADE):
        o.data.materials.append(mat(code))
    o.name = "SF_Prop_Lamp"
    smooth(o)
    anchor = empty("Light_Anchor", o, (sc.x, sc.y, bottom + 0.15))
    print(f"[props] lamp: {h:.1f} m tall, shade {tuple(round(x, 2) for x in sc)} (bottom {bottom:.1f} m), reach {LAMP_REACH:.2f}")
    return o, [anchor]


# ---------------------------------------------------------------- the arena with its props


def place_in_arena(names):
    """Open the arena's .blend and set the exported props where the game puts them (Blender x / y = Unity x / z)."""
    bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT, "SF_Arena_v1.blend"))
    coll = bpy.data.collections.new("Study props (preview)")
    bpy.context.scene.collection.children.link(coll)

    def bring(name, at, yaw):
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"))
        root = [o for o in bpy.context.selected_objects if o.parent is None][0]
        root.location = at
        root.rotation_euler.z = yaw
        for o in bpy.context.selected_objects:
            for c in o.users_collection:
                c.objects.unlink(o)
            coll.objects.link(o)
        return root

    for i, (x, y) in enumerate(((-6.2, -6.2), (6.2, -6.2), (-6.2, 6.2), (6.2, 6.2))):
        bring(names[i], (x, y, -6.0), ((i % 5) - 2) * 0.06)
    # The clock's front (-Y) turned toward the board.
    # Turning -Y by t gives (sin t, -cos t): toward the board's centre from (11, -11).
    bring("SF_Prop_ChessClock", (11.0, -11.0, -6.0), math.atan2(-11.0, 11.0))
    # The lamp's arm (pole -> anchor) toward the shade's spot.
    lampo = bring("SF_Prop_Lamp", (-12.5, 10.0, -6.0), 0.0)
    anchor = [c for c in lampo.children_recursive if c.name.startswith("Light_Anchor")][0]
    bpy.context.view_layer.update()
    a = anchor.matrix_world.translation - lampo.location
    lampo.rotation_euler.z += math.atan2(-2.0, 5.5) - math.atan2(a.y, a.x)
    bpy.context.view_layer.update()
    spot = bpy.data.objects.get("Lamp light")
    if spot:
        spot.location = anchor.matrix_world.translation
        spot.rotation_euler = (Vector((0, 0, 0)) - spot.location).to_track_quat("-Z", "Y").to_euler()
    sc = bpy.context.scene
    cam = sc.camera
    for name, loc, target, lens in (("SF_Arena_v1_study", (30, -34, 14), (0, 0, -3.5), 24),
                                    ("SF_Arena_v1_study_low", (14, -26, -2.5), (3, 0, -3.0), 22),
                                    ("SF_Arena_v1_study_lamp", (6, -40, 8), (-6, 4, 6), 20)):
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
        print(f"[props] rendered {name}.png")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "SF_Arena_v1_study.blend"))


if __name__ == "__main__":
    reset()
    sources = {name: book(name) for name in ("book_ledger", "book_thick", "book_thin")}
    stacks = [stack(i, sources) for i in range(4)]
    for s in stacks:
        export(s)
    o, marks = clock()
    export(o, marks)
    o, marks = lamp()
    export(o, marks)
    place_in_arena([s.name for s in stacks])
    print("[props] done")
