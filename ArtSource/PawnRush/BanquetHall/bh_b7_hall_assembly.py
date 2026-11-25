"""B7 the hall's shell put together (bh-hall session, seoungtest R838, 2026-11-25): the parts the user passed one by
one (bh_b7_hall_model.py --part ...) placed round the grand banquet hall at the builder's numbers.

    blender -b --factory-startup --python bh_b7_hall_assembly.py

Saves bh_b7_hall.blend and stills/b7/assembly/*.png (the hall from five places; hall_vs_concept.png beside R787 and
R833). Blender frame: x = Unity x (east), y = Unity z (north), z = Unity y (up); the hall x -32 .. 32, y 0 .. 72,
the ceiling's underside at 34 (GrandBanquetHallBuilder.Shell).

The layout (decided here; the user to pass it):
  - The walls are two storeys: the plain-panel bays 0 .. 16, the window bays 16 .. 32, a walnut frieze to the
    ceiling at 34. Walnut pilasters between the bays, both storeys: on the east and west walls eight, the end ones
    against the corners (bays of 7.54 m, the sheet's 7.46); on the south and north walls either side of the doors.
  - The south doorway: the user's Tripo arch at the 12 m opening (26 m tall; the loft crosses it - the user's call),
    its transom at 7 m and the sunburst over it. The north exit: the scripted arch from the balcony (16) to the
    builder's lintel (24), slimmer rings so it stays under the ceiling's beams.
  - The ceiling: 8 x 9 coffers of 8 m. Roses where the builder hangs its chandeliers (the five rope pivots on each
    line, x +-2.6, and the great chandelier's middle) - the rods themselves are B6's.
  - Hearths (the user, 11-25): the east wall's three southern lower bays are fireplace bays (z 6.2, 16.1, 26.1;
    the third's north half is inside the pantry); their fire and smoke are previews here, Unity's in .Hall.cs.
  - Sconces: one on each lower pilaster's sunk panel (R787 hangs them on the posts). Banners: one on each upper pilaster, bishop and
    king in turn.
  - The stills only: a plain floor, sky behind the windows, lights at the sconces and a warm glow in the middle.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h   # noqa: E402

ROOT = h.ROOT
OUT = os.path.join(h.STILLS, "assembly")
HX0, HX1, HZ1, CEIL = -32.0, 32.0, 72.0, 34.0
ST = h.STOREY
F, D = 2.4, 1.2                 # the pilaster's face and depth (bh_b7_hall_model.pilaster's defaults)

WALL_ROT = {"S": 0.0, "N": math.pi, "W": -math.pi / 2, "E": math.pi / 2}


def wall_matrix(wall, c, z0=0.0, out=0.0):
    """Part frame -> hall: the part's x along the wall centred at c (x for S / N, y for W / E), its y out of the
    wall into the hall (+out more), z up from z0."""
    pos = {"S": (c, 0), "N": (c, HZ1), "W": (HX0, c), "E": (HX1, c)}[wall]
    m = Matrix.Translation((pos[0], pos[1], z0)) @ Matrix.Rotation(WALL_ROT[wall], 4, "Z")
    return m @ Matrix.Translation((0, out, 0))


class Kit:
    """Each part built once into a hidden templates collection; placed as linked copies."""

    def __init__(self, hall):
        self.hall = hall
        self.tpl = bpy.data.collections.new("templates")
        bpy.context.scene.collection.children.link(self.tpl)
        self.cache = {}

    def get(self, key, builder):
        if key not in self.cache:
            objs = builder(self.tpl)
            for o in objs:
                o.hide_render = True
            bpy.context.view_layer.update()        # the templates' matrices (the Tripo arch's scale) settled
            for o in objs:
                o.hide_set(True)
            self.cache[key] = objs
        return self.cache[key]

    def place(self, key, builder, m, name=None):
        out = []
        for o in self.get(key, builder):
            c = o.copy()                       # linked: the same mesh
            c.hide_render = False
            c.name = (name or o.name) + ".i"
            self.hall.objects.link(c)
            c.matrix_world = m @ o.matrix_world
            c.hide_set(False)
            out.append(c)
        return out


def pilaster_centres_ew():
    n = 7
    pitch = (HZ1 - F) / n
    return [F / 2 + i * pitch for i in range(n + 1)], pitch - F


def ns_layout(door_half):
    """Pilaster centres and bay centres on a south / north wall's half beside a door door_half wide each side."""
    p0, p2 = door_half + F / 2, HX1 - F / 2
    pil = [p0, (p0 + p2) / 2, p2]
    bays = [((pil[i] + pil[i + 1]) / 2, pil[i + 1] - pil[i] - F) for i in range(2)]
    return pil, bays


def rope_pivots():
    """GrandBanquetHallBuilder.Along / RopeEnd: the chandeliers' pivots along the hall (Unity z) and the great one's."""
    L = [6.5, 6.5, 6.5, 8.0, 8.0]
    end = [l * math.sin(math.radians(40)) for l in L]
    chord = math.sqrt(4.2 ** 2 - 2.6 ** 2)
    gap = [.8, .6, 0, .8]
    a = [0.0] * 5
    a[0] = .35 + end[0]
    for k in range(1, 5):
        if k == 3:
            gz = 6 + a[2] + end[2] + .3 + chord
            a[3] = gz + chord + .3 - 6 + end[3]
        else:
            a[k] = a[k - 1] + end[k - 1] + gap[k - 1] + end[k]
    great = 6 + a[2] + end[2] + .3 + chord
    return [6 + v for v in a], great


def assemble():
    hall = bpy.data.collections.new("BH B7 hall")
    bpy.context.scene.collection.children.link(hall)
    kit = Kit(hall)
    cols_ew, w_ew = pilaster_centres_ew()
    south_pil, south_bays = ns_layout(11.2)
    north_pil, north_bays = ns_layout(9.3)
    lamps = []

    def bays_on(wall, centres_w, fires=()):
        for i, (c, w) in enumerate(centres_w):
            kw = round(w, 3)
            if i in fires:     # the user (11-25): hearths cut into the east wall's lower bays
                kit.place(("fireplace", kw), lambda col, w=w: h.fireplace_bay(col, W=w), wall_matrix(wall, c, 0))
            else:
                kit.place(("panel", kw), lambda col, w=w: h.panel_bay(col, W=w), wall_matrix(wall, c, 0))
            kit.place(("window", kw), lambda col, w=w: h.window_bay(col, W=w), wall_matrix(wall, c, ST))

    def pilasters_on(wall, centres, banners=True):
        for i, c in enumerate(centres):
            for z0 in (0, ST):
                kit.place("pilaster", h.pilaster, wall_matrix(wall, c, z0))
            # a sconce on the lower pilaster's sunk panel (R787 hangs them on the posts)
            kit.place("sconce", h.sconce, wall_matrix(wall, c, 6.2, out=D - .1))
            lamps.append(wall_matrix(wall, c, 6.2, out=D - .1) @ Vector((0, .2, 2.0)))
            if banners:
                piece = "bishop" if i % 2 == 0 else "king"
                kit.place(("banner", piece), lambda col, p=piece: h.banner(col, W=3.2, H=7.6, piece=p),
                          wall_matrix(wall, c, ST + 12.3 - 7.6 - .12, out=D))

    # east and west walls
    for wall in ("W", "E"):
        bays_on(wall, [((cols_ew[i] + cols_ew[i + 1]) / 2, w_ew) for i in range(len(cols_ew) - 1)],
                fires=(0, 1, 2) if wall == "E" else ())
        pilasters_on(wall, cols_ew)
    # south and north walls, either side of their doors (the wall's x mirrored for the north: its part x runs west)
    for wall, pil, bays in (("S", south_pil, south_bays), ("N", north_pil, north_bays)):
        sgn_w = 1 if wall == "S" else -1
        for side in (-1, 1):
            bays_on(wall, [(sgn_w * side * c, w) for c, w in bays])
            pilasters_on(wall, [sgn_w * side * c for c in pil])
    # the south doorway: the Tripo arch, the stone behind it with the opening, above it to the frieze
    kit.place("arch_tripo", h.arch_tripo, wall_matrix("S", 0, 0))
    m = h.Mesh()
    h.field_with_hole(m, "stone", -11.2, 11.2, 0, 2 * ST, [(-6, 0), (6, 0), (6, 7), (-6, 7)], y=-.3, reveal=.7)
    kit.place("south back", lambda col: [m.finish("BH hall south door back", col, bevel=0)], wall_matrix("S", 0, 0))
    # the north exit: the scripted arch from the balcony, the wall below it (behind the dais) and around it
    kit.place("arch_north", lambda col: h.arch_doorway(col, W=12, sill=ST, lintel=24, spring=24.9,
                                                       name="BH hall north exit", rw=.9, rs=1.1),
              wall_matrix("N", 0, 0))
    kit.place(("panel", 18.6), lambda col: h.panel_bay(col, W=18.6), wall_matrix("N", 0, 0))
    m2 = h.Mesh()
    h.field_with_hole(m2, "stone", -9.3, 9.3, ST, 2 * ST, [(-6, ST), (6, ST), (6, 24), (-6, 24)], y=-.3, reveal=.7)
    kit.place("north back", lambda col: [m2.finish("BH hall north exit back", col, bevel=0)], wall_matrix("N", 0, 0))
    # the frieze 32 .. 34 all round: a gold band, walnut with a top moulding into the ceiling
    for wall, c, length in (("S", 0, 64), ("N", 0, 64), ("W", 36, 72), ("E", 36, 72)):
        fm = h.Mesh()
        h.prism_yz(fm, "gold", [(0, 0), (.22, 0), (.26, .08), (.22, .2), (0, .2)], -length / 2, length / 2)
        h.prism_yz(fm, "walnut", [(0, .2), (.3, .2), (.3, 1.5), (.5, 1.62), (.55, 2.0), (0, 2.0)], -length / 2, length / 2)
        kit.place(("frieze", wall), lambda col, fm=fm, wall=wall: [fm.finish("BH hall frieze " + wall, col, bevel=.02)],
                  wall_matrix(wall, c, 2 * ST))
    # the ceiling: 8 x 9 coffers, then the roses
    cm = h.Mesh()
    for i in range(8):
        for j in range(9):
            h.coffer(cm, full=False, at=(HX0 + 4 + 8 * i, 4 + 8 * j))
    kit.place("ceiling", lambda col: [cm.finish("BH hall ceiling", col, bevel=.06, merge=False)],
              Matrix.Translation((0, 0, CEIL)))
    pivots, great = rope_pivots()
    roses = [(x, z) for z in pivots for x in (-2.6, 2.6)] + [(0.0, great)]
    for x, y in roses:
        below_beam = abs(((y + 4) % 8) - 4) < 1.4 + h.ROSE_R or abs(x) < 1.1
        drop = 1.37 if below_beam or abs(x) < 1.1 else .73
        kit.place("rose", lambda col: [_rose(col)], Matrix.Translation((x, y, CEIL - drop)))
    return hall, lamps


def _rose(col):
    m = h.Mesh()
    h.rosette(m)
    return m.finish("BH hall ceiling rose", col, bevel=.01, recalc=True)


def stage(lamps):
    """The stills' stage (not part of the model): a floor, sky behind the windows, the lights."""
    sc = bpy.context.scene
    fm = h.Mesh()
    h.box(fm, "floor_light", HX0 - 1, HX1 + 1, -1, HZ1 + 1, -.2, 0)
    floor = fm.finish("preview floor (not exported)", sc.collection, bevel=0)
    # the sky outside: a big box round the hall, emitting a soft afternoon sky
    sky = bpy.data.objects.new("preview sky (not exported)", bpy.data.meshes.new("sky"))
    sbm = bmesh.new()
    bmesh.ops.create_cube(sbm, size=1)
    sbm.to_mesh(sky.data)
    sbm.free()
    sky.scale = (110, 120, 70)
    sky.location = (0, 36, 20)
    sm = bpy.data.materials.new("preview sky")
    sm.use_nodes = True
    nt = sm.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (.75, .82, .95, 1)
    em.inputs["Strength"].default_value = 1.4
    nt.links.new(em.outputs[0], next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL").inputs[0])
    sky.data.materials.append(sm)
    sc.collection.objects.link(sky)
    # the afternoon sun through the west windows, the sconces, the chandeliers' glow (one warm light per line)
    sun = h._light("sun", "SUN", (0, 0, 0), 2.2, (1, .86, .66), 2)
    sun.rotation_euler = (Vector((-.7, -.25, -.45)).to_track_quat("-Z", "Y")).to_euler()
    for i, p in enumerate(lamps):
        h._light("sconce %d" % i, "POINT", p, 260, (1, .64, .32), .15)
    pivots, great = rope_pivots()
    for i, z in enumerate(pivots + [great]):
        h._light("chandelier %d" % i, "POINT", (0, z, 18), 9000, (1, .7, .4), 2.0)
    w = bpy.data.worlds.new("hall")
    sc.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (.04, .03, .025, 1)
    bg.inputs["Strength"].default_value = 1.0
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.eevee.taa_render_samples = 64
        sc.eevee.use_raytracing = True
        sc.eevee.use_shadows = True
    except Exception:
        pass
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy" if "AgX - Punchy" in [i.identifier for i in
                                                                 bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items] else "None"


SHOTS = {
    # name: (camera, look at, lens)
    "overview": ((26, 3, 30), (-10, 46, 8), 20),
    "north": ((0, 16, 5), (0, 72, 20), 22),
    "south": ((0, 52, 9), (0, 0, 15), 22),
    "west": ((22, 30, 9), (-32, 40, 15), 24),
    "ceiling": ((0, 30, 2), (4, 42, 34), 18),
    "east": ((4, 2, 6), (32, 18, 3), 24),
}


def stills():
    import numpy as np
    os.makedirs(OUT, exist_ok=True)
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    files = {}
    for name, (loc, look, lens) in SHOTS.items():
        cam = h.camera(name, loc, look, lens)
        sc.camera = cam
        f = os.path.join(OUT, "hall_%s.png" % name)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        files[name] = f
        print("STILL", f)
    H = 700
    row = [h._fit(h._pixels(h.R787), H),
           h._fit(h._pixels(os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R833_BanquetHallConcept",
                                         "banquet_concept_graybox.png")), H),
           h._fit(h._pixels(files["overview"]), H)]
    gap = np.ones((H, 14, 4), np.float32)
    img = np.concatenate(sum([[r, gap] for r in row], [])[:-1], axis=1)
    o = bpy.data.images.new("cmp", img.shape[1], H, alpha=True)
    o.pixels[:] = img.ravel()
    o.filepath_raw = os.path.join(OUT, "hall_vs_concept.png")
    o.file_format = "PNG"
    o.save()
    print("STILL", o.filepath_raw)


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hall, lamps = assemble()
    dg = bpy.context.evaluated_depsgraph_get()
    tris = sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons) for o in hall.objects if o.type == "MESH")
    print("HALL objects", len(hall.objects), "tris", tris)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b7_hall.blend"))
    stage(lamps)
    stills()
