"""Procedural Principled masters for the war room (art direction §7). Names are what Unity swaps for URP Lit.

Base colours carry no light: shading comes from URP. The procedural detail here (marble veins, wood grain, edge
wear, the map print) is what the bake step writes into the trim sheet, the marble tile and the decal atlas.
"""
import bpy

PALETTE = dict(
    marble="#EFE6D4", gold="#D2A63E", inlay="#C9A44E", teal="#2F6B70", walnut="#7A5432", map="#6F8E80",
    stone="#E3D7C0", navy="#2C4F86", lamp="#FFC978", iron="#2E2C2A", parchment="#D8C9A3", window="#CFE0E6",
)

NAMES = ["EG_Marble_Cream", "EG_Gold", "EG_Teal", "EG_Walnut", "EG_MapPaper", "EG_BannerNavy", "EG_Stone",
         "EG_Lamp_Emissive", "EG_Iron", "EG_Books", "EG_Window_Emissive", "EG_Trim"]


def srgb(hexstr, a=1.0):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*lin, a)


def _new(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (300, 0)
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    return m, nt, bsdf


def _n(nt, kind, loc, **props):
    node = nt.nodes.new(kind)
    node.location = loc
    for k, v in props.items():
        setattr(node, k, v)
    return node


def _ramp(nt, loc, stops):
    r = _n(nt, "ShaderNodeValToRGB", loc)
    els = r.color_ramp.elements
    while len(els) > 2:
        els.remove(els[-1])
    for i, (pos, col) in enumerate(stops):
        e = els[i] if i < len(els) else els.new(pos)
        e.position = pos
        e.color = col
    return r


def _coord(nt, loc, scale=1.0):
    tc = _n(nt, "ShaderNodeTexCoord", loc)
    mp = _n(nt, "ShaderNodeMapping", (loc[0] + 180, loc[1]))
    mp.inputs["Scale"].default_value = (scale, scale, scale)
    nt.links.new(tc.outputs["Object"], mp.inputs[0])
    return mp


def marble():
    m, nt, b = _new("EG_Marble_Cream")
    co = _coord(nt, (-1200, 0), 0.6)
    noise = _n(nt, "ShaderNodeTexNoise", (-850, 120))
    noise.inputs["Scale"].default_value = 1.6
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Distortion"].default_value = 0.6
    wave = _n(nt, "ShaderNodeTexWave", (-600, 120))
    wave.wave_type = "BANDS"
    wave.bands_direction = "DIAGONAL"
    wave.inputs["Scale"].default_value = 0.9
    wave.inputs["Distortion"].default_value = 9.0
    wave.inputs["Detail"].default_value = 4.0
    nt.links.new(co.outputs[0], noise.inputs["Vector"])
    nt.links.new(noise.outputs["Color"], wave.inputs["Vector"])
    vein = _ramp(nt, (-380, 120), [(0.0, (0, 0, 0, 1)), (0.06, (1, 1, 1, 1))])
    nt.links.new(wave.outputs["Fac"], vein.inputs[0])
    cloud = _n(nt, "ShaderNodeTexNoise", (-600, -160))
    cloud.inputs["Scale"].default_value = 0.7
    nt.links.new(co.outputs[0], cloud.inputs["Vector"])
    base = _ramp(nt, (-380, -160), [(0.3, srgb("#E8DCC6")), (0.7, srgb(PALETTE["marble"]))])
    nt.links.new(cloud.outputs["Fac"], base.inputs[0])
    mix = _n(nt, "ShaderNodeMix", (-120, 60))
    mix.data_type = "RGBA"
    mix.inputs["B"].default_value = srgb("#B9AC92")
    nt.links.new(vein.outputs[0], mix.inputs["Factor"])
    inv = _n(nt, "ShaderNodeMath", (-250, 260), operation="SUBTRACT")
    inv.inputs[0].default_value = 1.0
    nt.links.new(vein.outputs[0], inv.inputs[1])
    nt.links.new(inv.outputs[0], mix.inputs["Factor"])
    nt.links.new(base.outputs[0], mix.inputs["A"])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.32
    b.inputs["Specular IOR Level"].default_value = 0.5
    bump = _n(nt, "ShaderNodeBump", (0, -260))
    bump.inputs["Strength"].default_value = 0.04
    nt.links.new(cloud.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs[0], b.inputs["Normal"])
    return m


def gold():
    m, nt, b = _new("EG_Gold")
    b.inputs["Base Color"].default_value = srgb(PALETTE["gold"])
    b.inputs["Metallic"].default_value = 1.0
    co = _coord(nt, (-900, 0), 3.0)
    noise = _n(nt, "ShaderNodeTexNoise", (-600, 0))
    noise.inputs["Scale"].default_value = 6.0
    nt.links.new(co.outputs[0], noise.inputs["Vector"])
    r = _ramp(nt, (-350, 0), [(0.35, (0.18, 0.18, 0.18, 1)), (0.75, (0.34, 0.34, 0.34, 1))])
    nt.links.new(noise.outputs["Fac"], r.inputs[0])
    nt.links.new(r.outputs[0], b.inputs["Roughness"])
    # worn edges: brighter, smoother where ambient occlusion is lowest (exposed edges)
    ao = _n(nt, "ShaderNodeAmbientOcclusion", (-600, 250))
    ao.inputs["Distance"].default_value = 0.05
    ao.only_local = True
    edge = _ramp(nt, (-350, 250), [(0.85, srgb("#F2D27A")), (1.0, srgb(PALETTE["gold"]))])
    nt.links.new(ao.outputs["AO"], edge.inputs[0])
    nt.links.new(edge.outputs[0], b.inputs["Base Color"])
    return m


def painted(name, hexstr, rough, wear_hex):
    m, nt, b = _new(name)
    co = _coord(nt, (-900, 0), 2.0)
    noise = _n(nt, "ShaderNodeTexNoise", (-650, 0))
    noise.inputs["Scale"].default_value = 3.0
    noise.inputs["Detail"].default_value = 6.0
    nt.links.new(co.outputs[0], noise.inputs["Vector"])
    tone = _ramp(nt, (-400, 0), [(0.3, srgb(hexstr)), (0.8, srgb(hexstr))])
    tone.color_ramp.elements[0].color = tuple(c * 0.86 for c in srgb(hexstr)[:3]) + (1,)
    nt.links.new(noise.outputs["Fac"], tone.inputs[0])
    ao = _n(nt, "ShaderNodeAmbientOcclusion", (-650, 260))
    ao.inputs["Distance"].default_value = 0.04
    ao.only_local = True
    wear = _ramp(nt, (-400, 260), [(0.9, (1, 1, 1, 1)), (1.0, (0, 0, 0, 1))])
    nt.links.new(ao.outputs["AO"], wear.inputs[0])
    mix = _n(nt, "ShaderNodeMix", (-120, 100))
    mix.data_type = "RGBA"
    mix.inputs["B"].default_value = srgb(wear_hex)
    nt.links.new(wear.outputs[0], mix.inputs["Factor"])
    nt.links.new(tone.outputs[0], mix.inputs["A"])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough
    return m


def walnut():
    m, nt, b = _new("EG_Walnut")
    co = _coord(nt, (-1100, 0), 1.0)
    sep = _n(nt, "ShaderNodeMapping", (-850, 0))
    sep.inputs["Scale"].default_value = (3.0, 0.35, 3.0)
    nt.links.new(co.outputs[0], sep.inputs[0])
    wave = _n(nt, "ShaderNodeTexWave", (-600, 0))
    wave.wave_type = "RINGS"
    wave.inputs["Scale"].default_value = 6.0
    wave.inputs["Distortion"].default_value = 4.0
    wave.inputs["Detail"].default_value = 3.0
    nt.links.new(sep.outputs[0], wave.inputs["Vector"])
    r = _ramp(nt, (-350, 0), [(0.25, srgb("#6A4729")), (0.75, srgb(PALETTE["walnut"]))])
    nt.links.new(wave.outputs["Fac"], r.inputs[0])
    nt.links.new(r.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.5
    bump = _n(nt, "ShaderNodeBump", (0, -220))
    bump.inputs["Strength"].default_value = 0.08
    nt.links.new(wave.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs[0], b.inputs["Normal"])
    return m


def map_paper():
    """A hand-drawn campaign map: parchment, grey-green land, thin gold grid lines."""
    m, nt, b = _new("EG_MapPaper")
    co = _coord(nt, (-1300, 0), 0.25)
    land = _n(nt, "ShaderNodeTexNoise", (-1000, 120))
    land.inputs["Scale"].default_value = 2.2
    land.inputs["Detail"].default_value = 10.0
    land.inputs["Roughness"].default_value = 0.62
    nt.links.new(co.outputs[0], land.inputs["Vector"])
    coast = _ramp(nt, (-760, 120), [(0.53, srgb(PALETTE["parchment"])), (0.545, srgb("#A9B9A2"))])
    nt.links.new(land.outputs["Fac"], coast.inputs[0])
    edge = _ramp(nt, (-760, 330), [(0.525, (0, 0, 0, 1)), (0.535, (1, 1, 1, 1)), (0.545, (0, 0, 0, 1))])
    nt.links.new(land.outputs["Fac"], edge.inputs[0])
    ink = _n(nt, "ShaderNodeMix", (-500, 200))
    ink.data_type = "RGBA"
    ink.inputs["B"].default_value = srgb(PALETTE["map"])
    nt.links.new(edge.outputs[0], ink.inputs["Factor"])
    nt.links.new(coast.outputs[0], ink.inputs["A"])
    # grid every 2 m
    grid_co = _coord(nt, (-1300, -300), 0.5)
    sx = _n(nt, "ShaderNodeSeparateXYZ", (-1000, -300))
    nt.links.new(grid_co.outputs[0], sx.inputs[0])
    lines = []
    for i, axis in enumerate(("X", "Y")):
        fr = _n(nt, "ShaderNodeMath", (-800, -250 - 120 * i), operation="PINGPONG")
        fr.inputs[1].default_value = 0.5
        nt.links.new(sx.outputs[axis], fr.inputs[0])
        th = _n(nt, "ShaderNodeMath", (-620, -250 - 120 * i), operation="LESS_THAN")
        th.inputs[1].default_value = 0.012
        nt.links.new(fr.outputs[0], th.inputs[0])
        lines.append(th)
    mx = _n(nt, "ShaderNodeMath", (-450, -300), operation="MAXIMUM")
    nt.links.new(lines[0].outputs[0], mx.inputs[0])
    nt.links.new(lines[1].outputs[0], mx.inputs[1])
    gridmix = _n(nt, "ShaderNodeMix", (-250, 60))
    gridmix.data_type = "RGBA"
    gridmix.inputs["B"].default_value = srgb(PALETTE["inlay"])
    nt.links.new(mx.outputs[0], gridmix.inputs["Factor"])
    nt.links.new(ink.outputs[2], gridmix.inputs["A"])
    nt.links.new(gridmix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.75
    return m


def banner():
    m, nt, b = _new("EG_BannerNavy")
    b.inputs["Base Color"].default_value = srgb("#34507E")
    b.inputs["Roughness"].default_value = 0.85
    b.inputs["Sheen Weight"].default_value = 0.4
    return m


def stone():
    m, nt, b = _new("EG_Stone")
    co = _coord(nt, (-900, 0), 0.8)
    noise = _n(nt, "ShaderNodeTexNoise", (-650, 0))
    noise.inputs["Scale"].default_value = 2.0
    noise.inputs["Detail"].default_value = 6.0
    nt.links.new(co.outputs[0], noise.inputs["Vector"])
    r = _ramp(nt, (-400, 0), [(0.35, srgb("#D4C7AE")), (0.7, srgb(PALETTE["stone"]))])
    nt.links.new(noise.outputs["Fac"], r.inputs[0])
    nt.links.new(r.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.6
    return m


def emissive(name, hexstr, strength):
    m, nt, b = _new(name)
    b.inputs["Base Color"].default_value = srgb(hexstr)
    b.inputs["Emission Color"].default_value = srgb(hexstr)
    b.inputs["Emission Strength"].default_value = strength
    b.inputs["Roughness"].default_value = 0.4
    return m


def iron():
    m, nt, b = _new("EG_Iron")
    b.inputs["Base Color"].default_value = srgb(PALETTE["iron"])
    b.inputs["Metallic"].default_value = 0.85
    b.inputs["Roughness"].default_value = 0.45
    return m


def books():
    """Book spines: colour from the 'Col' attribute written by the bookshelf generator."""
    m, nt, b = _new("EG_Books")
    attr = _n(nt, "ShaderNodeAttribute", (-400, 0))
    attr.attribute_name = "Col"
    nt.links.new(attr.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.7
    return m


def trim():
    """The trim sheet (Textures/T_EG_Trim_*): exported mouldings use it; renders show it when the texture exists."""
    import os
    m, nt, b = _new("EG_Trim")
    b.inputs["Base Color"].default_value = srgb(PALETTE["gold"])
    b.inputs["Metallic"].default_value = 0.6
    b.inputs["Roughness"].default_value = 0.35
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Textures", "T_EG_Trim_BaseColor.png")
    if os.path.exists(path):
        img = bpy.data.images.load(path, check_existing=True)
        tex = _n(nt, "ShaderNodeTexImage", (-300, 0))
        tex.image = img
        nt.links.new(tex.outputs["Color"], b.inputs["Base Color"])
    return m


def build_all():
    mats = {
        "EG_Trim": trim(),
        "EG_Marble_Cream": marble(),
        "EG_Gold": gold(),
        "EG_Teal": painted("EG_Teal", PALETTE["teal"], 0.4, "#6E9A98"),
        "EG_Walnut": walnut(),
        "EG_MapPaper": map_paper(),
        "EG_BannerNavy": banner(),
        "EG_Stone": stone(),
        "EG_Lamp_Emissive": emissive("EG_Lamp_Emissive", PALETTE["lamp"], 8.0),
        "EG_Iron": iron(),
        "EG_Books": books(),
        "EG_Window_Emissive": emissive("EG_Window_Emissive", PALETTE["window"], 2.5),
    }
    viewport = {"EG_Marble_Cream": PALETTE["marble"], "EG_Gold": PALETTE["gold"], "EG_Teal": PALETTE["teal"],
                "EG_Walnut": PALETTE["walnut"], "EG_MapPaper": "#B9C2A6", "EG_BannerNavy": "#34507E", "EG_Stone": PALETTE["stone"],
                "EG_Lamp_Emissive": PALETTE["lamp"], "EG_Iron": PALETTE["iron"], "EG_Books": "#7A5F45",
                "EG_Window_Emissive": PALETTE["window"]}
    for name, hx in viewport.items():
        mats[name].diffuse_color = srgb(hx)          # what Workbench and the viewport's solid mode show
        if name == "EG_Gold":
            mats[name].metallic = 0.8
    ref = bpy.data.materials.new("EG_Ref_Gray")
    ref.diffuse_color = (0.6, 0.6, 0.6, 1)
    mats["EG_Ref_Gray"] = ref
    return mats
