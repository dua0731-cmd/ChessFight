# R502 step 3: one representative module, the canal-side wall section (concept sheet 2, items 5 and 6), to agree the
# shape language before the rest is built: round puffy stone blocks, chunky proportions, the tower's stone.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_canal_wall_v1.py -- [--render]
#
# Writes C_CanalWall_v1.blend and, with --render, render_canal_wall_L.png / _R.png (the right section is the left one
# mirrored, as in the sheet) and canal_wall_compare.png (sheet items 5 and 6 above, ours below).
# The size follows the game: walkway floor at z = 0 (the plaza), water surface at z = -0.35, the moat side of the
# section along y = 0 (water at y < 0); 1 m pawn for scale. Nothing here is exported to Unity yet (that is steps 4-5).
#
# Frame: Blender X along the wall, Y from the water (y < 0) to the wall (y > 0), Z up.
import os, sys, math, random, json
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
MAT_DIR = os.path.join(HERE, "Materials")
sys.path.insert(0, MAT_DIR)
import r502_tower_block as tb  # noqa: E402  (stone_material, light_rig, camera, reset)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEX = os.path.join(MAT_DIR, "Textures")
SHEET = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "Docs", "PawnRush", "ConceptArt",
                                      "R502_DrawbridgeSheets", "02_architecture_modules.webp"))
random.seed(5023)

WATER_Z, WALL_Y, LENGTH = -.35, 1.6, 7.0
GAP = .022                      # joint between procedural stones (the module's joints are about the same)


def lin(h):
    c = [int(h[i:i + 2], 16) / 255. for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1.,)


# ------------------------------------------------------------------ materials
def principled(name, col, rough=.5, metal=0., emit=None, strength=0.):
    m = bpy.data.materials.new(name)
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = lin(emit)
        b.inputs["Emission Strength"].default_value = strength
    return m


def iron_material():
    m = principled("iron", "#3B3836", .48, .75)
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = 18.
    n.inputs["Detail"].default_value = 6.
    nt.links.new(tc.outputs["Object"], n.inputs["Vector"])
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.color_ramp.elements[0].color = lin("#2E2B2A")
    r.color_ramp.elements[1].color = lin("#5A5652")
    nt.links.new(n.outputs["Fac"], r.inputs["Fac"])
    nt.links.new(r.outputs["Color"], b.inputs["Base Color"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .25
    nt.links.new(n.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


def wood_material():
    """The R502 swatch's baked plank maps, box-projected (2 m tile)."""
    m = bpy.data.materials.new("wood")
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (.5, .5, .5)
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])

    def tex(suffix, cs):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_WoodPlank_" + suffix + ".png"), check_existing=True)
        t.image.colorspace_settings.name = cs
        t.projection = "BOX"
        t.projection_blend = .2
        nt.links.new(mp.outputs[0], t.inputs["Vector"])
        return t.outputs["Color"]
    nt.links.new(tex("BaseColor", "sRGB"), b.inputs["Base Color"])
    nt.links.new(tex("Roughness", "Non-Color"), b.inputs["Roughness"])
    return m


def water_material():
    m = bpy.data.materials.new("water")
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (.6, .6, .6)
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    cs = nt.nodes.new("ShaderNodeTexImage")
    cs.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_Water_Caustic.png"))
    cs.image.colorspace_settings.name = "Non-Color"
    nt.links.new(mp.outputs[0], cs.inputs["Vector"])
    nm = nt.nodes.new("ShaderNodeTexImage")
    nm.image = bpy.data.images.load(os.path.join(TEX, "PR_Early_Water_Normal.png"))
    nm.image.colorspace_settings.name = "Non-Color"
    nt.links.new(mp.outputs[0], nm.inputs["Vector"])
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nmap.inputs["Strength"].default_value = .7
    nt.links.new(nm.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs[0], b.inputs["Normal"])
    r = nt.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    els[0].color = lin("#0784AE")
    els[1].position = 1.
    els[1].color = lin("#E8FFFF")
    e = els.new(.35)
    e.color = lin("#18B8D6")
    nt.links.new(cs.outputs["Color"], r.inputs["Fac"])
    nt.links.new(r.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(r.outputs["Color"], b.inputs["Emission Color"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = .3
    nt.links.new(cs.outputs["Color"], mul.inputs[0])
    nt.links.new(mul.outputs[0], b.inputs["Emission Strength"])
    b.inputs["Roughness"].default_value = .05
    return m


# ------------------------------------------------------------------ geometry helpers
def link(ob, coll=None):
    (coll or bpy.context.scene.collection).objects.link(ob)
    return ob


def mesh_object(name, bm, mat, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.materials.append(mat)
    return link(bpy.data.objects.new(name, me))


def evaluated(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data
    ob.modifiers.clear()
    ob.data = me
    bpy.data.meshes.remove(old)
    return ob


def stone(name, mat, lo, hi, puff=.018, chip=.02, seg=3, bevel=.05):
    """A round, puffy, chipped stone block filling the box lo..hi (minus half a joint on each side)."""
    lo, hi = Vector(lo), Vector(hi)
    size = hi - lo - Vector((GAP, GAP, GAP))
    c = (lo + hi) / 2
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    cuts = max(3, min(9, int(max(size) / .12)))
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    ob = mesh_object(name, bm, mat)
    bv = ob.modifiers.new("bevel", "BEVEL")
    bv.width = min(bevel, min(size) * .3)
    bv.segments = seg
    bv.limit_method = "ANGLE"
    sub = ob.modifiers.new("sub", "SUBSURF")
    sub.levels = sub.render_levels = 1
    evaluated(ob)
    h = size / 2
    seed = Vector((random.uniform(0, 99), random.uniform(0, 99), random.uniform(0, 99)))
    me = ob.data
    for v in me.vertices:
        p, n = v.co, v.normal
        rx, ry, rz = [min(abs(p[i]) / max(h[i], 1e-4), 1.) for i in range(3)]
        d = Vector()
        # every face bulges a little (pillowy), most of all the visible ones
        for axis, r_a, r_b in ((0, ry, rz), (1, rx, rz), (2, rx, ry)):
            if abs(n[axis]) > .5:
                d += n * puff * (math.cos(r_a * math.pi / 2) ** .5) * (math.cos(r_b * math.pi / 2) ** .5)
        q = p * 3.2 + seed
        d += n * (noise.noise(q) * .007 + noise.noise(q * 3.1) * .0025)
        edge = max(0., (sorted((rx, ry, rz))[1] - .72) / .28)
        d += n * min(0., noise.noise(p * 8. + seed * 2) + .12) * chip * edge
        v.co = p + d
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    ob.location = c
    ob.rotation_euler = (random.uniform(-.006, .006), random.uniform(-.006, .006), random.uniform(-.01, .01))
    return ob


def load_module():
    path = os.path.join(MAT_DIR, "StoneBlock_Module.blend")
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = ["PR_Early_StoneBlock"]
    return dst.objects[0].data


def module(me, at, rot=(0, 0, 0), flip=False):
    """The R502 stone block module: front face at y = at.y facing -y, body +y, bottom at at.z, centred on at.x."""
    ob = link(bpy.data.objects.new("module", me))
    ob.location = Vector(at) + (Vector((0, .74, 0)) if flip else Vector())
    ob.rotation_euler = (rot[0], rot[1], rot[2] + (math.pi if flip else 0))
    return ob


# ------------------------------------------------------------------ parts of the section
def pillar(mat, x0, x1, y0, y1, height, name, base=0., plinth=True):
    """Courses of two stones, the split turning each course; a plinth below and a bevelled cap on top."""
    z = base
    if plinth:
        stone(name + " plinth", mat, (x0 - .08, y0 - .08, z), (x1 + .08, y1 + .08, z + .3), puff=.012)
        z += .3
    course = 0
    top = base + height
    while z < top - .12:
        hgt = min(.3 + random.uniform(-.03, .03), top - z)
        if course % 2 == 0:
            xm = (x0 + x1) / 2 + random.uniform(-.08, .08)
            stone(name, mat, (x0, y0, z), (xm, y1, z + hgt))
            stone(name, mat, (xm, y0, z), (x1, y1, z + hgt))
        else:
            ym = (y0 + y1) / 2 + random.uniform(-.08, .08)
            stone(name, mat, (x0, y0, z), (x1, ym, z + hgt))
            stone(name, mat, (x0, ym, z), (x1, y1, z + hgt))
        z += hgt
        course += 1
    stone(name + " cap", mat, (x0 - .07, y0 - .07, z), (x1 + .07, y1 + .07, z + .16), puff=.01, bevel=.04)
    stone(name + " cap top", mat, (x0 + .02, y0 + .02, z + .16), (x1 - .02, y1 - .02, z + .26), puff=.02, bevel=.05)
    return z + .26


def chain(mat, a, b, sag, name):
    """Oval iron links along a catenary-like curve, each turned 90 degrees from the last."""
    a, b = Vector(a), Vector(b)
    pts = []
    n = 200
    for i in range(n + 1):
        t = i / n
        p = a.lerp(b, t)
        p.z -= sag * 4 * t * (1 - t)
        pts.append(p)
    lengths = [0.]
    for i in range(1, len(pts)):
        lengths.append(lengths[-1] + (pts[i] - pts[i - 1]).length)
    step = .085
    s, k = step / 2, 0
    objs = []
    while s < lengths[-1] - step / 2:
        i = next(j for j in range(len(lengths)) if lengths[j] >= s)
        p = pts[i]
        tan = (pts[min(i + 1, n)] - pts[max(i - 1, 0)]).normalized()
        bm = bmesh.new()
        # an oval link: a torus stretched along the curve
        maj, mnr = .042, .014
        rings, segs = 16, 8
        verts = []
        for r in range(rings):
            u = r / rings * 2 * math.pi
            for sg in range(segs):
                v = sg / segs * 2 * math.pi
                x = (maj + mnr * math.cos(v)) * math.cos(u) * 1.45
                y = (maj + mnr * math.cos(v)) * math.sin(u)
                zz = mnr * math.sin(v)
                verts.append(bm.verts.new((x, y, zz)))
        for r in range(rings):
            for sg in range(segs):
                a0 = verts[r * segs + sg]
                a1 = verts[((r + 1) % rings) * segs + sg]
                a2 = verts[((r + 1) % rings) * segs + (sg + 1) % segs]
                a3 = verts[r * segs + (sg + 1) % segs]
                bm.faces.new((a0, a1, a2, a3))
        ob = mesh_object(name, bm, mat)
        rot = tan.to_track_quat("X", "Z").to_matrix().to_4x4()
        spin = Matrix.Rotation(math.pi / 2 if k % 2 else 0, 4, "X")
        ob.matrix_world = Matrix.Translation(p) @ rot @ spin
        objs.append(ob)
        s += step
        k += 1
    return objs


def bracket_plate(mat, at, normal_y=-1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=(.12, .03, .14), verts=bm.verts)
    ob = mesh_object("chain plate", bm, mat)
    bv = ob.modifiers.new("bv", "BEVEL")
    bv.width, bv.segments = .008, 2
    ob.location = at
    return ob


def lantern(iron, glass, at, out=Vector((0, -1, 0))):
    """A wall lantern: back plate, an arm out from the stone, and a hanging iron lantern with lit panes."""
    at = Vector(at)
    parts = []
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=(.11, .03, .2), verts=bm.verts)
    parts.append(mesh_object("lantern plate", bm, iron))
    parts[-1].location = at
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=(.035, .3, .035), verts=bm.verts)
    parts.append(mesh_object("lantern arm", bm, iron))
    parts[-1].location = at + out * .15 + Vector((0, 0, .06))
    hang = at + out * .27
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=.012, radius2=.012, depth=.07)
    parts.append(mesh_object("lantern hook", bm, iron))
    parts[-1].location = hang + Vector((0, 0, .02))
    top = hang + Vector((0, 0, -.02))
    w, hgt = .17, .26
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=w * .82, radius2=.03, depth=.09)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 4, 3, "Z"))
    parts.append(mesh_object("lantern roof", bm, iron, smooth=False))
    parts[-1].location = top + Vector((0, 0, -.045))
    body_top = top.z - .09
    for sx in (-1, 1):
        for sy in (-1, 1):
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.)
            bmesh.ops.scale(bm, vec=(.022, .022, hgt), verts=bm.verts)
            parts.append(mesh_object("lantern post", bm, iron, smooth=False))
            parts[-1].location = (top.x + sx * w / 2, top.y + sy * w / 2, body_top - hgt / 2)
    for zz, sz in ((body_top, .03), (body_top - hgt, .04)):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.)
        bmesh.ops.scale(bm, vec=(w + .05, w + .05, sz), verts=bm.verts)
        parts.append(mesh_object("lantern band", bm, iron, smooth=False))
        parts[-1].location = (top.x, top.y, zz)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=(w - .01, w - .01, hgt - .03), verts=bm.verts)
    parts.append(mesh_object("lantern glass", bm, glass, smooth=False))
    parts[-1].location = (top.x, top.y, body_top - hgt / 2)
    # light so the stone around it glows a little, as in the sheet
    lt = bpy.data.lights.new("lantern light", "POINT")
    lt.energy = 25
    lt.color = (1., .72, .38)
    lt.shadow_soft_size = .08
    lo = link(bpy.data.objects.new("lantern light", lt))
    lo.location = (top.x, top.y + out.y * .05, body_top - hgt / 2)
    return parts


def bench(wood, x0, x1, y0, y1):
    seat_h, th = .46, .08
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    bmesh.ops.scale(bm, vec=(x1 - x0, y1 - y0, th), verts=bm.verts)
    seat = mesh_object("bench seat", bm, wood)
    bv = seat.modifiers.new("bv", "BEVEL")
    bv.width, bv.segments = .018, 3
    seat.location = ((x0 + x1) / 2, (y0 + y1) / 2, seat_h - th / 2)
    for x in (x0 + .16, x1 - .16):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.)
        bmesh.ops.scale(bm, vec=(.14, (y1 - y0) - .06, seat_h - th), verts=bm.verts)
        leg = mesh_object("bench leg", bm, wood)
        bv = leg.modifiers.new("bv", "BEVEL")
        bv.width, bv.segments = .015, 3
        leg.location = (x, (y0 + y1) / 2, (seat_h - th) / 2)


def planter(mat, leaf, bloom, at):
    """A round stone bowl on a foot, heaped with leaves and cream hydrangea heads."""
    at = Vector(at)
    prof = [(0., 0.), (.17, 0.), (.19, .03), (.19, .07), (.13, .1), (.12, .14), (.16, .17), (.27, .26), (.33, .34),
            (.36, .4), (.36, .44), (.32, .46), (.3, .43), (0., .43)]
    bm = bmesh.new()
    ring = 40
    rows = []
    for r, z in prof:
        rows.append([bm.verts.new((r * math.cos(2 * math.pi * i / ring), r * math.sin(2 * math.pi * i / ring), z))
                     for i in range(ring)])
    for a, b in zip(rows, rows[1:]):
        for i in range(ring):
            bm.faces.new((a[i], a[(i + 1) % ring], b[(i + 1) % ring], b[i]))
    bowl = mesh_object("planter", bm, mat)
    sub = bowl.modifiers.new("sub", "SUBSURF")
    sub.levels = sub.render_levels = 1
    bowl.location = at
    rng = random.Random(7)
    for i in range(70):
        a = rng.uniform(0, 2 * math.pi)
        r = math.sqrt(rng.random()) * .34
        z = .43 + (.26 - r * .55) + rng.uniform(-.03, .03)
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=rng.uniform(.05, .08))
        bmesh.ops.scale(bm, vec=(1.4, .8, .45), verts=bm.verts)
        ob = mesh_object("leaf", bm, leaf)
        ob.location = at + Vector((r * math.cos(a), r * math.sin(a), z))
        ob.rotation_euler = (rng.uniform(-.6, .6), rng.uniform(-.6, .6), a)
    for i in range(9):
        a = i / 9 * 2 * math.pi + rng.uniform(-.2, .2)
        r = .16 + rng.uniform(-.05, .08) if i else 0.
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=3, radius=.1)
        for v in bm.verts:
            v.co *= 1 + noise.noise(v.co * 40 + Vector((i, i, i))) * .18
        ob = mesh_object("bloom", bm, bloom)
        ob.location = at + Vector((r * math.cos(a), r * math.sin(a), .66 - r * .4))


def pawn(at):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=.27, radius2=.15, depth=.68)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, .34))
    s = bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=.17)
    bmesh.ops.translate(bm, verts=s["verts"], vec=(0, 0, .83))
    ob = mesh_object("pawn 1 m", bm, principled("pawn", "#ECECEE", .45))
    ob.location = at


# ------------------------------------------------------------------ the section
def build():
    sc = tb.reset()
    info = json.load(open(os.path.join(TEX, "PR_Early_TowerStone.json")))
    stone_m = tb.stone_material(info)
    stone_m.name = "PR_Early_TowerStone"
    iron = iron_material()
    wood = wood_material()
    glass = principled("lantern glass", "#FFD58A", .2, 0., "#FFB24A", 9.)
    leaf = principled("leaf", "#4F8A36", .6)
    bloom = principled("bloom", "#F3EEDD", .7)
    water = water_material()
    mod = load_module()

    # water and the moat floor under it
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.)
    w = mesh_object("water", bm, water, smooth=False)
    w.scale = (7., 2.6, 1.)
    w.location = (LENGTH / 2, -2.6, WATER_Z)
    for i in range(8):
        for j in range(4):
            module(mod, (-.5 + i, -(j + 1) * 1.093 - .02, -1.25), rot=(-math.pi / 2, 0, 0))

    # the water edge: tall coping stones from below the water up to just over the walkway
    x = -.6
    while x < LENGTH + .6:
        L = random.uniform(.6, .9)
        stone("edge", stone_m, (x, -.02, -.75), (x + L, .5, .04), puff=.02)
        x += L
    # walkway: the module laid face up, then the back wall of standing modules with a coping course on top
    for i in range(7):
        module(mod, (i + .5, .5, 0.), rot=(-math.pi / 2, 0, 0))
    for i in range(7):
        module(mod, (i + .5, WALL_Y, 0.), flip=(i % 3 == 1))
    x = 0.
    while x < LENGTH:
        L = min(random.uniform(.45, .7), LENGTH - x)
        stone("wall coping", stone_m, (x, WALL_Y - .06, 1.093), (x + L, WALL_Y + .8, 1.31), puff=.016)
        x += L

    # pillars: the big one with the planter (left), a slim middle one, a tall one at the far end
    # the big pillar stands on a raised step that runs out to the water, as in the sheet
    stone("step", stone_m, (-.15, -.05, -.32), (1.15, .55, -.02), puff=.015)
    stone("pillar base", stone_m, (-.12, .45, 0.), (1.12, 2.45, .32), puff=.015)
    top_a = pillar(stone_m, 0., 1., 1.0, 2.3, 1.55, "pillar A", base=.32, plinth=False)
    top_b = pillar(stone_m, 3.0, 3.55, 1.38, 2.3, 1.45, "pillar B", plinth=True)
    top_c = pillar(stone_m, 6.1, 6.95, 1.2, 2.35, 1.95, "pillar C", plinth=True)
    planter(stone_m, leaf, bloom, (.5, 1.65, top_a))

    # chains hang in a swag across the wall face between the pillars
    face = WALL_Y - .07
    for xa, xb in ((1.0, 3.0), (3.55, 6.1)):
        bracket_plate(iron, (xa + .07, face + .015, 1.2))
        bracket_plate(iron, (xb - .07, face + .015, 1.2))
        chain(iron, (xa + .07, face, 1.16), (xb - .07, face, 1.16), .42, "chain")

    # lanterns on the pillars' faces toward the walkway
    lantern(iron, glass, (.82, .985, 1.15))
    lantern(iron, glass, (6.35, 1.185, 1.35))
    bench(wood, 4.1, 5.5, 1.08, 1.52)
    pawn((2.2, 1.0, 0.))
    return sc


def render(sc, path, mirror):
    root = bpy.data.objects.new("mirror", None)
    link(root)
    for ob in list(sc.objects):
        if ob.parent is None and ob is not root and ob.type in ("MESH", "LIGHT"):
            ob.parent = root
    root.scale.x = -1 if mirror else 1
    root.location.x = LENGTH if mirror else 0
    if "sun" not in sc.objects:
        tb.light_rig(sc, (0, 0, 0), 0)
    sc.cycles.samples = 160
    # the sheet's view: from the water side, a little to the left (mirrored for the right section)
    sx = 1 if not mirror else -1
    cx = LENGTH / 2
    tb.camera(sc, (cx - sx * 3.6, -7.2, 3.6), (cx - sx * .2, 1.1, .55), 42)
    sc.render.resolution_x, sc.render.resolution_y = 1400, 1000
    sc.render.film_transparent = False
    w = sc.world.node_tree.nodes["Background"]
    w.inputs["Color"].default_value = (.93, .88, .80, 1)
    w.inputs["Strength"].default_value = .75
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    root.scale.x = 1
    root.location.x = 0


def compare(out):
    def load(p):
        img = bpy.data.images.load(p, check_existing=False)
        a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(img.size[1], img.size[0], 4), img

    def resized(arr, w):
        img = bpy.data.images.new("t", arr.shape[1], arr.shape[0], alpha=True)
        img.pixels.foreach_set(arr.ravel())
        img.scale(w, int(arr.shape[0] * w / arr.shape[1]))
        a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(img.size[1], img.size[0], 4)
    S, _ = load(SHEET)
    h, w = S.shape[:2]
    crops = []
    for x0, x1 in ((.02, .27), (.75, .99)):
        crops.append(S[h - int(.97 * h):h - int(.69 * h), int(x0 * w):int(x1 * w)])
    W = 700
    tops = [resized(c, W) for c in crops]
    th = max(t.shape[0] for t in tops)
    tops = [np.concatenate([t, np.ones((th - t.shape[0], W, 4), np.float32)], 0) for t in tops]
    L, _ = load(os.path.join(HERE, "render_canal_wall_L.png"))
    R, _ = load(os.path.join(HERE, "render_canal_wall_R.png"))
    bots = [resized(L, W), resized(R, W)]
    gap = np.ones((th, 12, 4), np.float32)
    top = np.concatenate([tops[0], gap, tops[1]], 1)
    bot = np.concatenate([bots[0], np.ones((bots[0].shape[0], 12, 4), np.float32), bots[1]], 1)
    bar = np.ones((8, top.shape[1], 4), np.float32) * np.array([.55, .45, .35, 1.], np.float32)
    sheet = np.concatenate([bot, bar, top], 0)
    img = bpy.data.images.new("cmp", sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels.foreach_set(sheet.ravel())
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print("wrote", out)


if __name__ == "__main__":     # also a parts library for c_drawbridge_v2.py
    sc = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "C_CanalWall_v1.blend"))
    if "--render" in ARGS:
        render(sc, os.path.join(HERE, "render_canal_wall_L.png"), False)
        render(sc, os.path.join(HERE, "render_canal_wall_R.png"), True)
        compare(os.path.join(HERE, "canal_wall_compare.png"))
