"""A look at the study's atmosphere before it goes into the game (10-28, user "추천 1~3 진행"):
  1  the lamp's beam: a faint cone of light from under the shade toward the board, fading out, dust drifting in it
  2  the room in the fog: dark bookcase walls far off, a tall window behind the board with cool moonlight
(3, the lamp swaying on big hits, moves: the game only.) Opens v1/SF_Arena_v1_study.blend (clean_tripo_v1.py).

blender -b --factory-startup --python concept_atmosphere_v1.py -- <v1_dir>
Blender x / y = Unity x / z here. The same numbers go into SwordFightArena (BeamLength, BeamAngle, ...).
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector

V1 = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.open_mainfile(filepath=os.path.join(V1, "SF_Arena_v1_study.blend"))
sc = bpy.context.scene
coll = bpy.data.collections.new("Atmosphere (concept)")
sc.collection.children.link(coll)
rng = random.Random(3)

BEAM_LENGTH, BEAM_ANGLE, BEAM_ALPHA = 15.0, 24.0, 0.10   # m, half angle (deg), strength at the shade
DUST = 140


def srgb(h):
    c = [int(h[k:k + 2], 16) / 255 for k in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def link(o):
    coll.objects.link(o)
    return o


def emissive(name, hexs, strength, alpha=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0, 0, 0, 1)
    b.inputs["Emission Color"].default_value = (*srgb(hexs), 1)
    b.inputs["Emission Strength"].default_value = strength
    if alpha is not None:
        b.inputs["Alpha"].default_value = alpha
    return m


def lit(name, hexs, rough=0.8):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*srgb(hexs), 1)
    b.inputs["Roughness"].default_value = rough
    return m


def box(name, size, at, material):
    bm = bmesh.new()
    for v in bmesh.ops.create_cube(bm, size=1.0)["verts"]:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    o = bpy.data.objects.new(name, me)
    o.location = at
    return link(o)


# ---------------------------------------------------------------- 1 the beam and its dust

# The bulb inside the shade, measured on the shade's own faces (as the game does, SwordFightArena.Shade), and the
# light coming out of its mouth: straight down, leaning 12 degrees toward the board's middle (user 10-28: "실제
# 램프에서 조명 효과가 나타나게 해").
LAMP_LEAN, LAMP_SPREAD = 12.0, 80.0
lampo = [o for o in bpy.data.objects if o.type == "MESH" and any(m and "1f4a35" in m.name for m in o.data.materials)][0]
slot = [i for i, m in enumerate(lampo.data.materials) if m and "1f4a35" in m.name][0]
pts = [lampo.matrix_world @ lampo.data.vertices[v].co for p in lampo.data.polygons if p.material_index == slot for v in p.vertices]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
apex = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z + 0.35 * (hi.z - lo.z)))
inward = Vector((-apex.x, -apex.y, 0)).normalized()
axis = (Vector((0, 0, -1)) + inward * math.tan(math.radians(LAMP_LEAN))).normalized()
BEAM_LENGTH = min(BEAM_LENGTH, max(3.0, (apex.z - 3.0) / -axis.z))
radius = BEAM_LENGTH * math.tan(math.radians(BEAM_ANGLE))
spot = bpy.data.objects.get("Lamp light")
if spot:
    spot.location = apex
    spot.rotation_euler = axis.to_track_quat("-Z", "Y").to_euler()
    spot.data.spot_size = math.radians(LAMP_SPREAD)
    spot.data.spot_blend = 0.45
print(f"[atmo] bulb {tuple(round(x, 2) for x in apex)}, beam {BEAM_LENGTH:.1f} m")

# The cone: apex at the bulb, open toward the board; its alpha fades with the distance (object-space -Z).
bm = bmesh.new()
seg = 40
tip = bm.verts.new((0, 0, 0))
ring = [bm.verts.new((radius * math.cos(a), radius * math.sin(a), -BEAM_LENGTH)) for a in (s / seg * math.tau for s in range(seg))]
for s in range(seg):
    bm.faces.new((tip, ring[s], ring[(s + 1) % seg]))
me = bpy.data.meshes.new("Lamp beam")
bm.to_mesh(me)
bm.free()
beam_mat = bpy.data.materials.new("Lamp beam")
beam_mat.use_nodes = True
nt = beam_mat.node_tree
for n in list(nt.nodes):
    nt.nodes.remove(n)
out = nt.nodes.new("ShaderNodeOutputMaterial")
em = nt.nodes.new("ShaderNodeEmission")
em.inputs["Color"].default_value = (*srgb("ffcf8a"), 1)
em.inputs["Strength"].default_value = 1.0
tr = nt.nodes.new("ShaderNodeBsdfTransparent")
mix = nt.nodes.new("ShaderNodeMixShader")
coord = nt.nodes.new("ShaderNodeTexCoord")
sep = nt.nodes.new("ShaderNodeSeparateXYZ")
fade = nt.nodes.new("ShaderNodeMapRange")
fade.inputs["From Min"].default_value = 0.0
fade.inputs["From Max"].default_value = -BEAM_LENGTH
fade.inputs["To Min"].default_value = BEAM_ALPHA
fade.inputs["To Max"].default_value = 0.0
nt.links.new(coord.outputs["Object"], sep.inputs[0])
nt.links.new(sep.outputs["Z"], fade.inputs["Value"])
nt.links.new(fade.outputs["Result"], mix.inputs[0])
nt.links.new(tr.outputs[0], mix.inputs[1])
nt.links.new(em.outputs[0], mix.inputs[2])
nt.links.new(mix.outputs[0], out.inputs["Surface"])
beam_mat.use_backface_culling = False
try:
    beam_mat.surface_render_method = "BLENDED"   # smooth see-through, not dithered
except AttributeError:
    beam_mat.blend_method = "BLEND"
me.materials.append(beam_mat)
beam = link(bpy.data.objects.new("Lamp beam", me))
beam.location = apex
beam.rotation_euler = (-axis).to_track_quat("Z", "Y").to_euler()   # object +Z back toward the lamp

dust_mat = emissive("Dust", "fff0d0", 2.0)
for k in range(DUST):
    d = rng.uniform(0.05, 0.85) ** 0.7 * BEAM_LENGTH
    r = rng.uniform(0, 1) ** 0.5 * d * math.tan(math.radians(BEAM_ANGLE)) * 0.9
    a = rng.uniform(0, math.tau)
    side = axis.orthogonal().normalized()
    up = axis.cross(side)
    p = apex + axis * d + side * math.cos(a) * r + up * math.sin(a) * r
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=rng.uniform(0.02, 0.05), location=p)
    o = bpy.context.active_object
    o.data.materials.append(dust_mat)
    for c in o.users_collection:
        c.objects.unlink(o)
    coll.objects.link(o)

# ---------------------------------------------------------------- 2 the room: bookcases and a moonlit window

shelf_wood = lit("Bookcase", "1c130d")
spines = [lit(f"Spine {h}", h, 0.9) for h in ("3a1a16", "1d2a22", "1c2236", "3a3018", "2a1e2c")]
for wall_y, x0, x1, face in ((48.0, -60.0, 60.0, -1), ):
    x = x0
    while x < x1:
        if -14.0 < x < 14.0:     # the window's place
            x = 14.0
            continue
        box("Bookcase", (8.0, 3.0, 40.0), (x + 4.0, wall_y, -6.0 + 20.0), shelf_wood)
        for sh in range(9):
            z = -6.0 + 2.0 + sh * 4.2
            box("Shelf books", (7.2, 0.4, 3.0), (x + 4.0, wall_y - 1.3, z + 1.5), spines[(sh + int(x)) % 5])
        x += 8.4
for side in (-1, 1):   # side walls of shelves, far left and right, turned toward the board
    for k in range(5):
        box("Bookcase", (3.0, 8.0, 40.0), (side * 55.0, 40.0 - k * 8.4, 14.0), shelf_wood)
# The window: a tall dark frame with pale panes, behind the board.
frame = lit("Window frame", "120c08")
box("Window frame", (26.0, 1.0, 34.0), (0.0, 50.0, 14.0), frame)
pane = emissive("Moonlit pane", "9fb4dc", 0.55)
for col in range(3):
    for row in range(4):
        box("Pane", (7.4, 0.3, 7.2), (-8.2 + col * 8.2, 49.3, 0.5 + row * 8.0 + 2.0), pane)
# The moonlight: cool, from the window, no shadows (the lamp keeps the only shadow in the game).
moon = bpy.data.objects.new("Moonlight", bpy.data.lights.new("Moonlight", "SUN"))
moon.data.energy = 0.9
moon.data.color = srgb("8fa8d8")
moon.data.use_shadow = False
moon.rotation_euler = Vector((0.2, -1.0, -0.55)).to_track_quat("-Z", "Y").to_euler()
link(moon)

# ---------------------------------------------------------------- renders

cam = sc.camera
for name, loc, target, lens in (("SF_Arena_v1_atmosphere", (22, -40, 12), (0, 6, 2), 22),
                                ("SF_Arena_v1_atmosphere_low", (9, -24, 1.5), (0, 4, 3), 20)):
    cam.location = loc
    cam.data.lens = lens
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = 300
    sc.render.filepath = os.path.join(V1, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[atmo] rendered {name}.png")
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(V1, "SF_Arena_v1_atmosphere.blend"))
print("[atmo] done")
