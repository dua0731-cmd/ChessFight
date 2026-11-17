# blender -b --factory-startup --python qa/texture_lookdev.py -- <texture folder> <out folder> [cams] [engine]
# R686 (seoungtest, the texture pass's sample zone): zone 1 (the start court, the wagon yard, its sheds and loads) rebuilt
# from source with its metre UVs (rcy_kit.surface_uvs), every RCY slot in its baked tile (rcy_textures.py), the castle
# kit's outer gatehouse in its own textures, the parked coach and the five roaming teams where the R678 Play dump has
# them (coach, wheels and horses in their slots), the merlons and lamp posts (RcyScenery), and the canal and its wheel
# in clay behind (not yet textured). Lit like RoyalCarriageYard.Lighting.cs: a warm white sun 58 degrees up from the
# south-east, a blue sky. Renders the cameras asked for (a = 승규's wagon-yard painting's view, b = a pawn's camera in
# the yard, c = a shed bay close, d = the start court) to <out>/rcy_tex_<cam>.png.
import json, math, os, sys
import bpy, bmesh
from mathutils import Vector, Quaternion, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
HERE = os.path.join(ROOT, "ArtSource/PawnRush/CarriageYardModels")
RES = os.path.join(ROOT, "Assets/Resources/PawnRushCarriageYard")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEX = argv[0] if len(argv) > 0 and argv[0] else os.path.join(HERE, "textures", "library")
OUT = argv[1] if len(argv) > 1 else os.path.join(HERE, "stills")
CAMS = argv[2].split(",") if len(argv) > 2 and argv[2] else ["a", "b", "c", "d"]
ENGINE = argv[3] if len(argv) > 3 else "eevee"
os.makedirs(OUT, exist_ok=True)


def run(g, name):
    with open(os.path.join(HERE, name), encoding="utf-8") as f:
        exec(compile(f.read(), name, "exec"), g)


bpy.ops.wm.read_factory_settings(use_empty=True)
g = {"__name__": "rcy", "RCY_ROOT": ROOT, "RCY_BUILD": True}
run(g, "rcy_kit.py")
run(g, "export_rcy.py")
run(g, "entrance_model.py")   # builds "RCY Entrance" (cells, with UVs)
u2b, uq2b, dump = g["u2b"], g["uq2b"], g["dump"]
print("LOOKDEV entrance tris", g["tris"](bpy.data.collections["RCY Entrance"]))

with open(os.path.join(TEX, "rcy_textures.json"), encoding="utf-8") as f:
    SPECS = json.load(f)
BY_SLOT = {v["slot"]: (k, v) for k, v in SPECS.items()}


def image(path, colour):
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = "sRGB" if colour else "Non-Color"
    img.alpha_mode = "CHANNEL_PACKED"
    return img


def split_albedo(path):
    """The albedo PNG as two images, its colour (alpha 1) and its smoothness (the alpha, grey): Blender mixes a low
    alpha into the colour however the image's alpha mode is set (the cloth came out near white), Unity's Lit does not."""
    import numpy as np
    src = bpy.data.images.load(path, check_existing=False)
    w, h = src.size
    px = np.empty(w * h * 4, np.float32)
    src.pixels.foreach_get(px)
    bpy.data.images.remove(src)
    px = px.reshape(-1, 4)
    out = []
    for suffix, rgba, cs in (("rgb", np.concatenate([px[:, :3], np.ones((len(px), 1), np.float32)], 1), "sRGB"),
                             ("smooth", np.repeat(px[:, 3:4], 4, 1), "Non-Color")):
        if suffix == "smooth":
            rgba[:, 3] = 1
        img = bpy.data.images.new(os.path.basename(path) + " " + suffix, w, h, alpha=False)
        img.colorspace_settings.name = cs
        img.pixels.foreach_set(rgba.ravel())
        img.pack()
        out.append(img)
    return out


def tile_material(slot):
    """A slot's look as Unity's URP Lit will draw it: the albedo (alpha = smoothness) and normal tiles at 1 / tile."""
    name, spec = BY_SLOT[slot]
    m = bpy.data.materials.new("LD " + slot)
    m.use_nodes = True
    nt = m.node_tree
    p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    uv = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    s = 1.0 / spec["tile"]
    mp.inputs["Scale"].default_value = (s, s, 1)
    nt.links.new(uv.outputs["UV"], mp.inputs["Vector"])
    rgb, smooth = split_albedo(os.path.join(TEX, name + "_Albedo.png"))
    a = nt.nodes.new("ShaderNodeTexImage")
    a.image = rgb
    sm = nt.nodes.new("ShaderNodeTexImage")
    sm.image = smooth
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = image(os.path.join(TEX, name + "_Normal.png"), False)
    for t in (a, sm, n):
        nt.links.new(mp.outputs["Vector"], t.inputs["Vector"])
    base_out = a.outputs["Color"]
    if slot in ("RCY | flag", "RCY | stone", "RCY | shade"):   # RcyTintLit: the part's tone (vertex colour "Color")
        at = nt.nodes.new("ShaderNodeAttribute")
        at.attribute_name = "Color"
        mt = nt.nodes.new("ShaderNodeMix")
        mt.data_type = "RGBA"
        mt.blend_type = "MULTIPLY"
        mt.inputs["Factor"].default_value = 1.0
        nt.links.new(a.outputs["Color"], mt.inputs[6])
        nt.links.new(at.outputs["Color"], mt.inputs[7])
        base_out = mt.outputs[2]
    tint = {"RCY | coat": "#8B4A22", "RCY | hair": "#3B2216"}.get(slot)   # Dressing.cs: the neutral coat times a bay's colour
    if tint:
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        c = [min(1.0, int(tint[i:i + 2], 16) / 255 * 1.25) for i in (1, 3, 5)]
        c = [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]
        mul.inputs[7].default_value = c + [1]
        nt.links.new(base_out, mul.inputs[6])
        nt.links.new(mul.outputs[2], p.inputs["Base Color"])
    else:
        nt.links.new(base_out, p.inputs["Base Color"])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(sm.outputs["Color"], inv.inputs[1])
    nt.links.new(inv.outputs["Value"], p.inputs["Roughness"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(n.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
    p.inputs["Metallic"].default_value = spec["metallic"]
    return m


def flat(name, hexc, rough=.6, metal=0.0, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    c = tuple(int(hexc[i:i + 2], 16) / 255 for i in (1, 3, 5))
    lin = tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1,)
    p.inputs["Base Color"].default_value = lin
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit:
        p.inputs["Emission Color"].default_value = lin
        p.inputs["Emission Strength"].default_value = emit
    return m


CLAY = flat("LD clay", "#9E9E9E", .85)
# The horse's slots as RoyalCarriageYard.Dressing.cs HorsePaint draws a bay (no tile yet: next phase).
HORSE = {"coat": flat("LD coat", "#8B4A22", .65), "hair": flat("LD hair", "#3B2216", .7), "hoof": flat("LD hoof", "#2B221D", .5),
         "eye": flat("LD eye", "#0E0B0A", .15), "plume": flat("LD plume", "#F3ECDB", .8)}
mats = {}


def slot_material(slot_name):
    base = slot_name.split(".")[0]
    if base in mats:
        return mats[base]
    if base == os.environ.get("LD_DEBUG_SLOT"):   # one slot in flat red, to find where it is drawn
        m = flat("LD debug", "#FF0000", .5)
    elif base.startswith("RcyCastle_"):
        m = None   # the castle kit keeps the FBX's own (textured below)
    elif base in BY_SLOT:
        m = tile_material(base)
    elif base == "RCY | clay":
        m = tile_material("RCY | stone")
    else:
        key = next((k for k in HORSE if k in base.lower()), None)
        m = HORSE[key] if key else CLAY
    mats[base] = m
    return m


def paint(objs):
    for o in objs:
        if o.type != "MESH":
            continue
        for i, s in enumerate(o.material_slots):
            if s.material is None:
                continue
            m = slot_material(s.material.name)
            if m is not None:
                s.material = m


def import_fbx(name, col_name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(RES, name + ".fbx"), axis_forward="-Z", axis_up="Y", use_custom_normals=True)
    new = [o for o in bpy.data.objects if o not in before]
    col = bpy.data.collections.get(col_name) or bpy.data.collections.new(col_name)
    if col.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(col)
    for o in new:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        col.objects.link(o)
    return new


def uv_imported(objs):
    for o in objs:
        if o.type == "MESH" and "Color" not in o.data.color_attributes:   # an older FBX: no tone, white
            ca = o.data.color_attributes.new("Color", "FLOAT_COLOR", "CORNER")
            ca.data.foreach_set("color", [1.0] * (4 * len(o.data.loops)))
        if o.type != "MESH" or o.data.uv_layers:
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(o.matrix_world)            # world frame, so a part's offset matches the built zones'
        g["surface_uvs"](bm)
        bm.transform(o.matrix_world.inverted())
        bm.to_mesh(o.data)
        bm.free()


def place(objs, pos, rot, local=Vector()):
    """Imported FBX roots onto a Unity pose (pos, rot (x, y, z, w)) the way Dressing.cs hangs them."""
    m = Matrix.Translation(u2b(pos)) @ uq2b(rot).to_matrix().to_4x4() @ Matrix.Translation(local)
    for o in objs:
        if o.parent is None:
            o.matrix_world = m @ o.matrix_world


# ------------------------------------------------------------------ the scene

paint(bpy.data.collections["RCY Entrance"].all_objects)

# The castle kit's outer gatehouse in its own textures (RoyalCarriageYard.Castle.cs CastlePaint).
gate = import_fbx("RcyCastleOuterGate", "LD castle")
for o in gate:
    if o.type != "MESH":
        continue
    for s in o.material_slots:
        if s.material is None:
            continue
        base = s.material.name.split(".")[0]
        png = os.path.join(RES, base + ".png")
        if not os.path.exists(png):
            continue
        m = bpy.data.materials.get("LD " + base)
        if m is None:
            m = bpy.data.materials.new("LD " + base)
            m.use_nodes = True
            nt = m.node_tree
            p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = image(png, True)
            nt.links.new(t.outputs["Color"], p.inputs["Base Color"])
            p.inputs["Roughness"].default_value = .82
            npng = os.path.join(RES, base + "_N.png")
            if os.path.exists(npng):
                tn = nt.nodes.new("ShaderNodeTexImage")
                tn.image = image(npng, False)
                nm = nt.nodes.new("ShaderNodeNormalMap")
                nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
                nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
        s.material = m

# The merlons on the yard's walls and the lamp posts (scenery_model.py), UVs the kit's way.
scen = import_fbx("RcyScenery", "LD scenery")
uv_imported(scen)
paint(scen)

# The canal and its wheel behind, in clay (later zones, not textured yet).
canal = import_fbx("RcyCanal", "LD later zones")
for o in canal:
    if o.type == "MESH":
        for s in o.material_slots:
            s.material = CLAY
wheel_node = next(n for n in dump() if n["path"] == "2 Canal/Giant waterwheel")
wheel = import_fbx("RcyGiantWaterwheel", "LD later zones")
place(wheel, wheel_node["pos"], wheel_node["rot"])
for o in wheel:
    if o.type == "MESH":
        for s in o.material_slots:
            s.material = CLAY

# The coaches (the parked one and the five teams as the dump caught them), their wheels and horses.
coach_parts = {}
for n in dump():
    p = n["path"]
    leaf = p.split("/")[-1]
    if not p.startswith("1 Entrance/"):
        continue
    if leaf == "Carriage":
        objs = import_fbx("RcyCoach", "LD coaches")
        uv_imported(objs)
        paint(objs)
        place(objs, n["pos"], n["rot"])
    elif leaf == "Wheel" and "/Carriage/" in p:
        objs = import_fbx("RcyCoachWheel", "LD coaches")
        uv_imported(objs)
        paint(objs)
        place(objs, n["pos"], n["rot"])
    elif leaf == "Body" and "/Horse " in p:
        objs = import_fbx("RcyHorse", "LD horses")
        for o in objs:
            if o.type == "MESH" and (o.name.startswith("Saddle") or o.name.endswith("_LOD1") or ".LOD1" in o.name or "LOD1" in o.name):
                o.hide_render = True
        uv_imported([o for o in objs if not o.hide_render])
        paint(objs)
        place(objs, n["pos"], n["rot"], u2b((0, -1.05, .75)))

# Backdrop: grass round the walls, and a sky.
bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=300)
me = bpy.data.meshes.new("LD ground")
bm.to_mesh(me)
bm.free()
ground = bpy.data.objects.new("LD ground", me)
ground.location = (0, 40, -.16)   # under the paving bases (top - .1), so no grass in the joints
ground.data.materials.append(flat("LD grass", "#7F9A55", .9))
bpy.context.scene.collection.objects.link(ground)

scene = bpy.context.scene
world = bpy.data.worlds.new("LD sky")
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
# RoyalCarriageYard.Lighting.cs's numbers (URP's light intensity ~ Blender's sun strength on a white diffuse face):
# the ambient's sky colour, the sun 1.85 warm white 58 degrees up from the south-east, a cool sky fill from the side
# away from the sun (.22) and a warm bounce from the ground (.12), no shadows from either; then GameLook's ACES and
# ZoneGrade's +.6 exposure.
bg.inputs["Color"].default_value = (.5, .6, .78, 1)
bg.inputs["Strength"].default_value = float(os.environ.get("LD_SKY", "1.0"))
scene.world = world


def sun_light(name, energy, colour, el, az, shadow=True, angle=2.5):
    d = bpy.data.lights.new(name, "SUN")
    d.energy = energy
    d.color = colour
    d.angle = math.radians(angle)
    d.use_shadow = shadow
    o = bpy.data.objects.new(name, d)
    el, az = math.radians(el), math.radians(az)
    to_light = Vector((math.cos(el) * math.sin(az), math.cos(el) * math.cos(az), math.sin(el)))
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = Vector((0, 0, -1)).rotation_difference(-to_light)
    scene.collection.objects.link(o)
    return o


sun = sun_light("LD sun", float(os.environ.get("LD_SUN", "1.85")), (1, .955, .88), 58, 135)
sun_light("LD sky fill", .22, (.62, .74, 1), 35, 315, shadow=False, angle=40)
sun_light("LD bounce", .12, (1, .86, .68), -40, 315, shadow=False, angle=40)

# Render.
if ENGINE == "cycles":
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
else:
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    for attr, val in (("taa_render_samples", 48), ("use_shadows", True), ("shadow_ray_count", 2), ("shadow_step_count", 8),
                      ("use_gtao", True), ("gtao_distance", 1.2), ("use_raytracing", True), ("fast_gi_distance", 2.0)):
        try:
            setattr(scene.eevee, attr, val)
        except Exception:
            pass
try:
    scene.view_settings.view_transform = os.environ.get("LD_VIEW", "ACES 1.3")
except TypeError:
    scene.view_settings.view_transform = "AgX"
scene.view_settings.exposure = float(os.environ.get("LD_EXP", ".6"))


CAMS_AT = {   # name: (location, target, lens mm, width, height)
    "a": ((0.0, -34.0, 38.0), (0.0, 24.0, -2.0), 28, 1200, 1600),
    "b": ((-2.5, 3.0, 3.1), (5.0, 24.0, 1.0), 20, 1600, 900),
    "c": ((11.0, 17.5, 1.9), (19.5, 23.0, 1.9), 26, 1600, 900),
    "d": ((-4.0, -14.5, 2.6), (2.0, 4.0, 3.0), 20, 1600, 900),
}
for cam in CAMS:
    loc, target, lens, w, h = CAMS_AT[cam]
    cd = bpy.data.cameras.new("LD cam " + cam)
    cd.lens = lens
    cd.clip_end = 600
    co = bpy.data.objects.new("LD cam " + cam, cd)
    co.location = loc
    co.rotation_mode = "QUATERNION"
    co.rotation_quaternion = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y")
    scene.collection.objects.link(co)
    scene.camera = co
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.resolution_percentage = 100
    scene.render.filepath = os.path.join(OUT, "rcy_tex_%s.png" % cam)
    bpy.ops.render.render(write_still=True)
    print("LOOKDEV rendered", scene.render.filepath)
if "--save" in sys.argv:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "rcy_tex_lookdev.blend"))
print("LOOKDEV_DONE")
