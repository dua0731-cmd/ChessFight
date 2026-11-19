"""R0 test piece (seoungtest R725, 2026-11-19): the Big Dipper's first star platform, end to end into Unity, to judge the
kit, the materials, the motif atlas and the light before the zones run (PLAN.md R0 "시험 한 점").

    blender -b --factory-startup --python ro_r0_star_model.py -- [--stills] [--export]

Graybox (RoyalObservatoryBuilder): Dipper[0] = Star(-2, 0, 18, r 4) - the deck "Dipper star 1" (a cylinder 8 m across,
0.6 deep, its top y 0), "Dipper star 1 brass rim", the five-ray "Dipper star 1 mark", "Dipper star 1 pillar" from the
hall floor (y -18). Its neighbours: the entry's threshold tongue to the south (x -5.5 .. 1.5, z 14 .. 16.2) and the line
to star 2 at (-10, 0.8, 26). Lamp posts flank both ways on, outside the rim.
Builds with ro_parts.star_platform -> ro_r0_star.blend, stills/r0/ (beside the concept), and with --export
Assets/Resources/RoyalObservatory/Models/Dipper/RoDipperStar1.fbx (+ _lamps.txt); RoyalObservatoryDressing.Dipper.cs
hangs it. Never export while Unity is in Play.
"""
import math, os, sys
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ro_kit as k      # noqa: E402
import ro_parts as parts   # noqa: E402

STAR = (-2.0, 0.0, 18.0)   # Unity
RADIUS = 4.0
HALL_FLOOR = -18.0
TO_STAR2 = (-10.0, .8, 26.0)
MODEL, ZONE = "RoDipperStar1", "Dipper"
CONCEPT = os.path.join(HERE, "..", "..", "..", "Docs", "PawnRush", "ConceptArt", "R725_RoyalObservatoryConcept")


def heading_to(a, b):
    """Blender heading (degrees, 0 = +x, counter-clockwise) from Unity point a to b."""
    return math.degrees(math.atan2(b[2] - a[2], b[0] - a[0]))


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    k.motif_reset()
    col = bpy.data.collections.new("RO R0 star")
    bpy.context.scene.collection.children.link(col)
    to2 = heading_to(STAR, TO_STAR2)
    # the line in is 3 m wide (28 deg each side at r 4), the entry's tongue 7 m (61 deg): posts just outside each
    lamps = [to2 - 30, to2 + 30, 270 - 66, 270 + 66]
    objs, lamp_at = parts.star_platform(col, "Dipper star 1", k.at(STAR), RADIUS, HALL_FLOOR, lamps=lamps)
    print("[r0] star platform:", len(objs), "objects,", k.tris(objs), "tris; lamps", len(lamp_at))
    for o in objs:
        print("   ", o.name, k.tris([o]))
    return col, objs, lamp_at


def stills(objs, out, shots=None, centre=None, floor_at=None, fill_offset=(6, -8, 7)):
    """Preview renders (also used by the other R0/R1 part scripts with their own shots, centre and floor)."""
    os.makedirs(out, exist_ok=True)
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1400, 900
    sc.view_settings.view_transform = "Standard"   # as Unity's sRGB output (AgX greyed the enamel)
    # preview materials: the library albedo/normal on the slots (the atlas on motif), metallic as in Unity
    lib = os.path.join(HERE, "textures", "library")
    for key, (unity, *_rest) in k.SLOTS.items():
        m = bpy.data.materials.get(key)
        if m is None or unity == "RO_Glass":
            continue
        nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        alb = os.path.join(lib, unity + "_Albedo.png")
        if not os.path.exists(alb):
            continue
        img = bpy.data.images.load(alb, check_existing=True)
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
        if unity != "RO_Motif":
            mp = nt.nodes.new("ShaderNodeMapping")
            tile = {"RO_Marble": 3, "RO_Gold": .6, "RO_Brass": 1, "RO_NavyEnamel": 1.5, "RO_VioletEnamel": 1.5,
                    "RO_Pearl": 2, "RO_HallStone": 3, "RO_Velvet": 1}.get(unity, 1)
            mp.inputs["Scale"].default_value = (1 / tile, 1 / tile, 1)
            uv = nt.nodes.new("ShaderNodeTexCoord")
            nt.links.new(uv.outputs["UV"], mp.inputs["Vector"]); nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
            vec = mp.outputs["Vector"]
        else:
            vec = None
        nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
        nrm_path = os.path.join(lib, unity + "_Normal.png")
        if os.path.exists(nrm_path):
            ni = bpy.data.images.load(nrm_path, check_existing=True); ni.colorspace_settings.name = "Non-Color"
            nt_ = nt.nodes.new("ShaderNodeTexImage"); nt_.image = ni
            if vec is not None:
                nt.links.new(vec, nt_.inputs["Vector"])
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nt_.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
        if unity == "RO_Motif":
            ms = bpy.data.images.load(os.path.join(lib, "RO_Motif_MetallicSmoothness.png"), check_existing=True)
            ms.colorspace_settings.name = "Non-Color"
            mt = nt.nodes.new("ShaderNodeTexImage"); mt.image = ms
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(mt.outputs["Color"], sep.inputs["Color"]); nt.links.new(sep.outputs[0], p.inputs["Metallic"])
            inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
            nt.links.new(mt.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], p.inputs["Roughness"])
    # planets: their surfaces from the library, UVs as made (0..1 round the sphere)
    for m in [m for m in bpy.data.materials if m.name.startswith(k.PLANET_PREFIX)]:
        alb = os.path.join(lib, k.unity_material(m.name) + "_Albedo.png")
        if not os.path.exists(alb):
            continue
        nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(alb, check_existing=True)
        nt.links.new(tex.outputs["Color"], p.inputs["Base Color"])
    # a dark hall: navy world, a cool moonlight from high north-west, warm points at the lamp globes
    w = bpy.data.worlds.new("RO hall"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    # a sky gradient for the metal to reflect (Unity has its reflection probe): dark navy above, warm lamp-lit haze low
    grad = w.node_tree.nodes.new("ShaderNodeTexGradient"); grad.gradient_type = "SPHERICAL"
    tc = w.node_tree.nodes.new("ShaderNodeTexCoord"); sep = w.node_tree.nodes.new("ShaderNodeSeparateXYZ")
    ramp = w.node_tree.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = .45; ramp.color_ramp.elements[0].color = (.35, .22, .10, 1)
    ramp.color_ramp.elements[1].position = .62; ramp.color_ramp.elements[1].color = (.015, .02, .06, 1)
    w.node_tree.links.new(tc.outputs["Generated"], sep.inputs[0])
    mz = w.node_tree.nodes.new("ShaderNodeMapRange"); mz.inputs[1].default_value = -1; mz.inputs[2].default_value = 1
    w.node_tree.links.new(tc.outputs["Normal"], sep.inputs[0]); w.node_tree.links.new(sep.outputs[2], mz.inputs[0])
    w.node_tree.links.new(mz.outputs[0], ramp.inputs[0]); w.node_tree.links.new(ramp.outputs[0], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.0
    sun = bpy.data.objects.new("moon", bpy.data.lights.new("moon", "SUN"))
    sun.data.energy = 3.0; sun.data.color = (.75, .82, 1.0); sun.data.angle = math.radians(2)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-140))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new("warm", bpy.data.lights.new("warm", "AREA"))
    fill.data.energy = 1400; fill.data.size = 8; fill.data.color = (1.0, .78, .5)
    centre = Vector(centre) if centre is not None else k.at(STAR)
    fill.location = centre + Vector(fill_offset)
    fill.rotation_euler = (centre - fill.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(fill)
    for i, at in enumerate(LAMPS):
        L = bpy.data.objects.new("lamp %d" % i, bpy.data.lights.new("lamp %d" % i, "POINT"))
        L.data.energy = 60; L.data.color = (1.0, .82, .55); L.data.shadow_soft_size = .1
        L.location = at; sc.collection.objects.link(L)
    # a hall floor plane, dark stone, so the pillar stands on something
    bpy.ops.mesh.primitive_plane_add(size=60, location=floor_at or (-2, 18, HALL_FLOOR))
    fl = bpy.context.active_object; fl.data.materials.append(k.slot("hallstone"))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    shots = shots or {
        "r0_star_three_quarter": (Vector((9.5, -8.5, 5.5)), Vector((0, 0, -1.2)), 35),
        "r0_star_pawn_eye": (Vector((-3.0, -6.0, 1.3)), Vector((0, 0, -.2)), 35),
        "r0_star_rim_close": (Vector((5.0, -1.2, .3)), Vector((3.2, .6, -.45)), 40),
        "r0_star_whole": (Vector((20, -24, -2)), Vector((0, 0, -8)), 32),
    }
    for name, (eye, look, lens) in shots.items():
        cam.location = centre + eye
        d = (centre + look) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        cam.data.clip_end = 300
        sc.render.filepath = os.path.join(out, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("[r0] still", sc.render.filepath)


LAMPS = []

if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    col, objs, LAMPS[:] = build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "ro_r0_star.blend"))
    if "--export" in argv:
        k.export(col.name, MODEL, ZONE)
        k.write_lamps(ZONE, MODEL, LAMPS)
    if "--stills" in argv:
        stills(objs, os.path.join(HERE, "stills", "r0"))
