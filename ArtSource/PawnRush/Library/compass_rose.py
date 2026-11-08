"""Pawn Rush library (작전 회의실): the start disc as an inlaid bronze compass rose — preview only (R359).

    blender --background --factory-startup --python compass_rose.py -- --preview D:/check   (renders, writes nothing)

승규 님: "출발 입구 쪽을 2번째 사진처럼 블렌더에서 문양 똑같이 해서 텍스쳐 입혀서 아직 유니티에 적용 시키지 말고
사진으로 보여줘". The photo: a floor compass rose — a bronze outer band, a dark verdigris field with thin gold rings
and a band of tick marks, a 16-point star (four long cardinal points, four intercardinal, eight small ones), every
point split along its ridge into a light gold and a darker bronze half.

The start disc in the environment FBX is START_spawn_zone (stone cylinder, radius 1.1, top z 0.2) with a flat
star (Upper20_Compass_star) and ring (Upper20_Start_compass_ring) lying on it. This script hides those two,
keeps the cylinder, and lays the compass rose on its top: ridged star points (two materials a point), raised
rings and ticks, a north mark toward +y (the course), procedural metal and patina. Nothing is saved or exported.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

FBX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "Assets",
                                    "Resources", "PawnRushLibrary", "PawnRushLibraryEnvironment.fbx"))
CENTRE = Vector((0.0, -16.5, 0.2))   # top centre of START_spawn_zone
DISC_R = 1.1
HIDE = ("Upper20_Compass_star", "Upper20_Start_compass_ring")

FIELD_Z = .002      # verdigris field above the stone top
BAND_IN, BAND_Z = .955, .008   # bronze outer band from BAND_IN to DISC_R
RIM_W, RIM_H = .012, .004       # thin raised gold rings
# (tip radius, base radius, half angle at the base, ridge height at the centre, z order)
POINTS = (
    (.45, .10, math.radians(11.25), .016, 0),   # 8 small points between
    (.64, .13, math.radians(22.5), .024, 1),     # 4 intercardinal
    (.93, .17, math.radians(45.0), .034, 2),     # 4 cardinal
)


# ------------------------------------------------------------------ materials

def node_mat(name, build):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    build(nt, bsdf)
    return m


def noise_ramp(nt, scale, detail, stops, coords=None):
    tex = nt.nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = scale
    tex.inputs["Detail"].default_value = detail
    if coords is not None:
        nt.links.new(coords, tex.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = stops[0]
    els[1].position, els[1].color = stops[1]
    for pos, col in stops[2:]:
        e = els.new(pos)
        e.color = col
    nt.links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
    return tex, ramp


def metal(name, light, dark, rough):
    def build(nt, bsdf):
        co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
        _, ramp = noise_ramp(nt, 9.0, 5.0, ((.25, (*dark, 1)), (.7, (*light, 1))), co)
        nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = .85
        _, rr = noise_ramp(nt, 40.0, 4.0, ((.35, (rough - .1,) * 3 + (1,)), (.7, (rough + .12,) * 3 + (1,))), co)
        nt.links.new(rr.outputs["Color"], bsdf.inputs["Roughness"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = .08
        n2 = nt.nodes.new("ShaderNodeTexNoise")
        n2.inputs["Scale"].default_value = 120.0
        nt.links.new(co, n2.inputs["Vector"])
        nt.links.new(n2.outputs["Fac"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return node_mat(name, build)


def patina(name):
    """Dark green-teal verdigris, mottled, a little lighter in blotches, satin."""
    def build(nt, bsdf):
        co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
        _, ramp = noise_ramp(nt, 6.0, 8.0, ((.35, (.025, .085, .06, 1)), (.62, (.07, .2, .15, 1)),
                                            (.8, (.14, .29, .21, 1))), co)
        vor = nt.nodes.new("ShaderNodeTexVoronoi")
        vor.inputs["Scale"].default_value = 55.0
        nt.links.new(co, vor.inputs["Vector"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = .35
        nt.links.new(ramp.outputs["Color"], mix.inputs["A"])
        nt.links.new(vor.outputs["Distance"], mix.inputs["B"])
        inv = nt.nodes.new("ShaderNodeMix")   # keep it from going black: lerp back toward the ramp
        inv.data_type = "RGBA"
        inv.inputs["Factor"].default_value = .55
        nt.links.new(mix.outputs["Result"], inv.inputs["A"])
        nt.links.new(ramp.outputs["Color"], inv.inputs["B"])
        nt.links.new(inv.outputs["Result"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = .25
        bsdf.inputs["Roughness"].default_value = .48
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = .15
        nt.links.new(vor.outputs["Distance"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return node_mat(name, build)


# ------------------------------------------------------------------ geometry (disc space: centre 0, top of stone z 0)

def polar(r, a, z):
    return Vector((r * math.sin(a), r * math.cos(a), z))   # angle 0 = +y (north, the course)


def annulus(bm, r0, r1, z, seg=160, mat=0):
    """A raised flat ring r0..r1 with its two walls down to 0."""
    ring = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring.append([bm.verts.new(polar(r, a, h)) for r, h in ((r0, 0), (r0, z), (r1, z), (r1, 0))])
    for i in range(seg):
        p, q = ring[i], ring[(i + 1) % seg]
        for k in range(3):
            f = bm.faces.new((p[k], q[k], q[k + 1], p[k + 1]))
            f.material_index = mat
            f.smooth = False


def disc(bm, r, z, seg=160, mat=0):
    c = bm.verts.new((0, 0, z))
    ring = [bm.verts.new(polar(r, 2 * math.pi * i / seg, z)) for i in range(seg)]
    for i in range(seg):
        bm.faces.new((c, ring[(i + 1) % seg], ring[i])).material_index = mat


def box_radial(bm, a, r0, r1, w, h, z0, mat):
    """A small raised bar along the radius at angle a (tick marks, the north mark's stem)."""
    d = Vector((math.sin(a), math.cos(a), 0))
    s = Vector((d.y, -d.x, 0)) * (w / 2)
    pts = [d * r0 - s, d * r1 - s, d * r1 + s, d * r0 + s]
    lo = [bm.verts.new(p + Vector((0, 0, z0))) for p in pts]
    hi = [bm.verts.new(p + Vector((0, 0, z0 + h))) for p in pts]
    bm.faces.new(hi).material_index = mat
    for i in range(4):
        bm.faces.new((lo[i], lo[(i + 1) % 4], hi[(i + 1) % 4], hi[i])).material_index = mat


def star_point(bm, a, tip, base_r, half, ridge, z0, light, dark):
    """One compass point: a ridge from the centre to the tip, the left half light gold, the right half bronze,
    its outer edges walled down to the field so no gap shows."""
    o = bm.verts.new((0, 0, z0 + ridge))
    t = bm.verts.new(polar(tip, a, z0 + .004))
    for side, mat in ((-1, light), (1, dark)):
        b = bm.verts.new(polar(base_r, a + side * half, z0 + .003))
        tri = (o, b, t) if side < 0 else (o, t, b)
        bm.faces.new(tri).material_index = mat
        t0 = bm.verts.new(polar(tip, a, FIELD_Z))
        b0 = bm.verts.new(polar(base_r, a + side * half, FIELD_Z))
        wall = (t, b, b0, t0) if side < 0 else (b, t, t0, b0)
        bm.faces.new(wall).material_index = mat


def build_rose(mats):
    """Returns the compass rose object (material slots: field, band, gold, bronze)."""
    FIELD, BAND, GOLD, BRONZE = range(4)
    bm = bmesh.new()
    disc(bm, BAND_IN + .002, FIELD_Z, mat=FIELD)
    annulus(bm, BAND_IN, DISC_R, BAND_Z, mat=BAND)
    # thin gold rings: inside the band, the tick band's two edges, the mid ring through the star, the hub
    for r in (.935, .895, .815, .53, .14):
        annulus(bm, r - RIM_W / 2, r + RIM_W / 2, FIELD_Z + RIM_H, mat=GOLD)
    # tick band: 64 ticks between .815 and .895, every fourth long
    for i in range(64):
        a = 2 * math.pi * i / 64
        long = i % 4 == 0
        box_radial(bm, a, .815 if long else .845, .895, .009 if long else .005, RIM_H * .8, FIELD_Z, GOLD)
    # north mark: a gold arrowhead on the bronze band toward +y
    n0, n1, w = .962, 1.07, .07
    tri = [bm.verts.new(p) for p in (polar(n1, 0, BAND_Z + .003), polar(n0, -w, BAND_Z + .003),
                                     polar(n0, w, BAND_Z + .003))]
    bm.faces.new(tri).material_index = GOLD
    # the star: small points first, cardinal points last and highest
    for tip, base_r, half, ridge, order in POINTS:
        z0 = FIELD_Z + .001 * order
        if order == 0:
            angles = [math.radians(22.5 + 45 * i) for i in range(8)]
        elif order == 1:
            angles = [math.radians(45 + 90 * i) for i in range(4)]
        else:
            angles = [math.radians(90 * i) for i in range(4)]
        for a in angles:
            star_point(bm, a, tip, base_r, half, ridge, z0, GOLD, BRONZE)
    # centre boss
    hub = bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=12, radius=.045)
    for v in hub["verts"]:
        v.co.z = v.co.z * .5 + .036
    for f in {f for v in hub["verts"] for f in v.link_faces}:
        f.material_index = GOLD
        f.smooth = True
    me = bpy.data.meshes.new("Start compass rose")
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new("Start_compass_rose", me)
    ob.location = CENTRE
    bpy.context.scene.collection.objects.link(ob)
    return ob


# ------------------------------------------------------------------ preview

def look_at(cam, target):
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def preview(out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=FBX)
    sc = bpy.context.scene
    for name in HIDE:
        bpy.data.objects[name].hide_render = True
    # the environment's single clay material in a warm stone like the Unity screen
    stone = bpy.data.materials.get("CLAY | uniform neutral gray")
    stone.use_nodes = True
    b = stone.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (.70, .58, .47, 1)
    b.inputs["Roughness"].default_value = .62
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.hide_render = True

    mats = (patina("Compass verdigris"),
            metal("Compass bronze band", (.70, .46, .18), (.56, .35, .12), .34),
            metal("Compass gold", (1.0, .72, .28), (.86, .56, .18), .24),
            metal("Compass bronze", (.52, .29, .09), (.38, .20, .06), .32))
    build_rose(mats)

    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sc.collection.objects.link(sun)
    sun.data.energy = 4.0
    sun.data.color = (1.0, .82, .64)
    sun.data.angle = math.radians(1.5)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-128))
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.42, .48, .58, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    sc.world = world

    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.use_raytracing = True
    sc.eevee.taa_render_samples = 64
    sc.view_settings.view_transform = "AgX"
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(out, exist_ok=True)
    shots = (
        ("compass_game.png", (2.6, -11.2, 3.0), (0, -16.3, .2), 40),       # like the player's camera
        ("compass_close.png", (1.6, -14.0, 2.1), (0, -16.5, .2), 45),
        ("compass_top.png", (0, -16.5, 4.2), (0, -16.5, .2), 35),
    )
    for name, at, target, lens in shots:
        cam.location = Vector(at)
        cam.data.lens = lens
        look_at(cam, target)
        if name == "compass_top.png":
            cam.rotation_euler = (0, 0, 0)
            sc.render.resolution_x = sc.render.resolution_y = 1200
        sc.render.filepath = os.path.join(out, name)
        bpy.ops.render.render(write_still=True)
        print("WROTE", sc.render.filepath)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" not in argv:
        sys.exit("Preview only for now (not applied to Unity): -- --preview <folder>")
    preview(argv[argv.index("--preview") + 1])
