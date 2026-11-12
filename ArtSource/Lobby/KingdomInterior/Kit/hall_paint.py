# R576 (승규 "레퍼런스 사진 과 같이 컬러 텍스쳐 굽기"): paints the kingdom hall in the reference pictures' colours
# (reference.webp, reference_stairs.webp, reference_gate_end.webp, reference_side_windows.webp) and bakes them into
# textures for Unity. export_hall_kit.py runs it with "-- out.fbx --paint":
#   1. before the join, paint(objs) gives each object its material (cream stone, polished checker marble floor,
#      dark wood gate leaves, red velvet carpets and curtains, gold lanterns) and the faces the builders tagged
#      (MB.mat: gold rings, studs, stripes, the gate frame and crest; red: the crest's field) theirs. The crown
#      pedestals come from the kit untagged: their crown and diamonds are found as small or high mesh islands.
#   2. after the join, bake(objs, folder) unwraps each mesh (Smart UV Project), bakes the colour (an emission
#      bake: exact, no noise), the metal / smoothness and ambient occlusion, and writes
#      <name>.png (colour x occlusion) and <name>_Metal.png (R metallic, A smoothness: URP Lit's metallic map).
# The materials are emission-only on purpose: Cycles' emission bake gives the flat colour. Their colour node
# graphs stay small (noise for stone variation, a checker for the floor, waves for wood grain).
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector

PX_PER_M = 64                 # texture density aimed for (the size is clamped to 256 .. MAX_PX)
MAX_PX = 4096                 # 승규: 4096. One bake image and one pixel buffer at a time (memory)
AO_MIX = .5                   # how much of the baked occlusion darkens the colour
AO_SAMPLES = 24


def lin(h):
    c = [((h >> s) & 255) / 255 for s in (16, 8, 0)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1.0,)


# colour(s), metallic, smoothness (as the reference pictures: matte warm stone, polished floor, shiny gold)
KINDS = {
    "Stone":  dict(a=0xEADCC5, b=0xF4EBDC, dark=0xD9C7AA, metal=0, smooth=.25),
    "Marble": dict(a=0xEADBC4, b=0x7A4428, metal=0, smooth=.75),   # R578: the reference floor's cream and brown
    "Wood":   dict(a=0x7A4A2C, b=0x55301C, grain=0x3E2213, metal=0, smooth=.45),   # R577: the gate picture's diamonds
    "Red":    dict(a=0xA81E28, b=0x7E1219, metal=0, smooth=.15),
    "Gold":   dict(a=0xE0AC4A, b=0xB98128, metal=1, smooth=.78),
}


def _nodes(m):
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    pos = nt.nodes.new("ShaderNodeNewGeometry").outputs["Position"]
    return nt, em, pos


def _mix(nt, f, a, b):
    mx = nt.nodes.new("ShaderNodeMix")
    mx.data_type = "RGBA"
    nt.links.new(f, mx.inputs[0])
    for i, c in ((6, a), (7, b)):
        if isinstance(c, tuple):
            mx.inputs[i].default_value = c
        else:
            nt.links.new(c, mx.inputs[i])
    return mx.outputs[2]


def _noise(nt, pos, scale, detail=3.0, rough=.55):
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    nt.links.new(pos, n.inputs["Vector"])
    return n.outputs["Fac"]


def _ramp(nt, f, lo, hi):
    """Remap a 0..1 factor so lo..hi spans it."""
    r = nt.nodes.new("ShaderNodeMapRange")
    r.inputs[1].default_value, r.inputs[2].default_value = lo, hi
    nt.links.new(f, r.inputs[0])
    return r.outputs[0]


def kind_material(kind):
    """The material whose emission is the kind's colour. A custom property holds metallic / smoothness."""
    name = "Paint " + kind
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    k = KINDS[kind]
    nt, em, pos = _nodes(m)
    if kind == "Stone":
        # large soft blotches between two creams, a little fine grain
        c = _mix(nt, _ramp(nt, _noise(nt, pos, .45), .35, .65), lin(k["a"]), lin(k["b"]))
        c = _mix(nt, _ramp(nt, _noise(nt, pos, 6.0, 5, .6), .55, .85), c, lin(k["dark"]))
    elif kind == "Marble":
        # 1.6 m squares as the reference's floor (cream and walnut). R578 (승규 "바닥도 색이랑 좀 달라 레퍼런스 사진에는
        # 문양이 없어"): plain polished squares, no veins
        sc = nt.nodes.new("ShaderNodeVectorMath")
        sc.operation = "SCALE"
        sc.inputs[3].default_value = 1 / 1.6
        nt.links.new(pos, sc.inputs[0])
        ch = nt.nodes.new("ShaderNodeTexChecker")
        ch.inputs["Scale"].default_value = 1.0
        ch.inputs["Color1"].default_value = lin(k["a"])
        ch.inputs["Color2"].default_value = lin(k["b"])
        nt.links.new(sc.outputs[0], ch.inputs["Vector"])
        c = ch.outputs["Color"]
    elif kind == "Wood":
        # R577 (the gate picture): dark brown boards laid as a diamond checker (0.45 m squares turned 45 degrees
        # in the leaf's plane, x-z), vertical grain through both tones
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(pos, sep.inputs[0])
        def lin_comb(sx, sz):
            m1 = nt.nodes.new("ShaderNodeMath"); m1.operation = "MULTIPLY"; m1.inputs[1].default_value = sx
            nt.links.new(sep.outputs[0], m1.inputs[0])
            m2 = nt.nodes.new("ShaderNodeMath"); m2.operation = "MULTIPLY_ADD"
            nt.links.new(sep.outputs[2], m2.inputs[0]); m2.inputs[1].default_value = sz
            nt.links.new(m1.outputs[0], m2.inputs[2])
            return m2.outputs[0]
        kd = 1 / (.45 * math.sqrt(2))
        comb = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(lin_comb(kd, kd), comb.inputs[0])
        nt.links.new(lin_comb(kd, -kd), comb.inputs[1])
        ch = nt.nodes.new("ShaderNodeTexChecker")
        ch.inputs["Scale"].default_value = 1.0
        ch.inputs["Color1"].default_value = lin(KINDS["Wood"]["a"])
        ch.inputs["Color2"].default_value = lin(KINDS["Wood"]["b"])
        nt.links.new(comb.outputs[0], ch.inputs["Vector"])
        wave = nt.nodes.new("ShaderNodeTexWave")
        wave.wave_type = "BANDS"
        wave.bands_direction = "X"
        wave.inputs["Scale"].default_value = 6
        wave.inputs["Distortion"].default_value = 5
        wave.inputs["Detail"].default_value = 3
        nt.links.new(pos, wave.inputs["Vector"])
        c = _mix(nt, _ramp(nt, wave.outputs["Fac"], .2, 1.6), ch.outputs["Color"], lin(KINDS["Wood"]["grain"]))
    else:
        c = _mix(nt, _ramp(nt, _noise(nt, pos, 3.0), .3, .7), lin(k["a"]), lin(k["b"]))
    nt.links.new(c, em.inputs["Color"])
    m["metal"], m["smooth"], m["kind"] = float(k["metal"]), float(k["smooth"]), kind
    return m


def object_kind(o):
    n = o.name
    if n.startswith(("Curtains", "Carpet")) or any(c.name == "Carpets" for c in o.users_collection):
        return "Red"
    if "Lantern" in n and "Glass" not in n:
        return "Gold"
    if n == "Floor":
        return "Marble"
    if o.parent is not None and o.parent.name.startswith("GateLeaf_"):
        return "Wood"
    return "Stone"


SKIP = ("Rose Glass", "Lantern Glass", "Gate Passage End", "Window Glass", "Vault Glass")   # R582 / R588: glass Unity colours


def _islands(o, gold_slot):
    """Kit crown pedestals: the crown (islands above 70% of the height) and the diamonds (small islands) are gold."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table()
    zs = [v.co.z for v in bm.verts]
    z0, z1 = min(zs), max(zs)
    seen = set()
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, isl = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        co = [v.co for g in isl for v in g.verts]
        lo = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
        hi = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
        size = max(hi - lo)
        if lo.z > z0 + .7 * (z1 - z0) or size < .12 * (z1 - z0):
            for g in isl:
                g.material_index = gold_slot
    bm.to_mesh(o.data)
    bm.free()


# R586 (승규 "다음 텍스쳐", reference.webp): the long runners have a thin gold stripe a little in from each edge, and
# the carpet runs on over the two front steps (stone at their ends). Blockout runners and steps: x -1.5 .. 1.5 and
# -1.7 .. 1.7 (Blender world x across the hall).
RUNNER_HALF = 1.5
CARPET_STRIPE = (1.30, 1.37)    # |x| of the gold stripes (picture: about 4% of the width in, 2% wide)


def _slot(me, m):
    names = [x.name if x else None for x in me.materials]
    if m.name not in names:
        me.materials.append(m)
        names.append(m.name)
    return names.index(m.name)


def _carpet_stripes(o, mats):
    """Cut the runner / step at the carpet's edges and stripes (world x planes) and colour the strips."""
    me = o.data
    red, gold, stone = (_slot(me, mats[k]) for k in ("Red", "Gold", "Stone"))
    mw = o.matrix_world
    inv, nrm = mw.inverted(), (mw.to_3x3().transposed() @ Vector((1, 0, 0))).normalized()
    bm = bmesh.new()
    bm.from_mesh(me)
    for x in (RUNNER_HALF,) + CARPET_STRIPE:
        for s in (-1, 1):
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:],
                                   plane_co=inv @ Vector((s * x, 0, 0)), plane_no=nrm)
    for f in bm.faces:
        x = abs((mw @ f.calc_center_median()).x)
        f.material_index = stone if x > RUNNER_HALF + 1e-4 else gold if CARPET_STRIPE[0] < x < CARPET_STRIPE[1] else red
    bm.to_mesh(me)
    bm.free()


def paint(objs):
    mats = {k: kind_material(k) for k in KINDS}
    for o in objs:
        if o.type != "MESH" or o.name.startswith(SKIP):
            continue
        if o.data.users > 1:
            o.data = o.data.copy()
        me = o.data
        if not me.materials:
            me.materials.append(mats["Stone"])
        me.materials[0] = mats[object_kind(o)]
        for i, m in enumerate(me.materials):
            if m is not None and m.name.startswith("Paint Gold"):
                me.materials[i] = mats["Gold"]
            elif m is not None and m.name.startswith("Paint Red"):
                me.materials[i] = mats["Red"]
        if o.name.startswith(("Runner", "Front Step")):
            _carpet_stripes(o, mats)
        if "Pedestal" in o.name:
            if mats["Gold"].name not in [m.name for m in me.materials if m]:
                me.materials.append(mats["Gold"])
            _islands(o, [m.name for m in me.materials].index(mats["Gold"].name))
    print(flush=True)
    print("PAINT", {k: sum(1 for o in objs if o.type == "MESH" and o.data.materials and o.data.materials[0] == m)
                    for k, m in mats.items()})


# ---------------------------------------------------------------- baking
def _area(o):
    return sum(p.area for p in o.data.polygons) * max(o.matrix_world.to_scale()) ** 2


def _size(o):
    s = math.sqrt(max(_area(o), 1e-4)) * PX_PER_M
    lo = 2048 if o.name.startswith("GateLeaf_") else 256    # R577: the leaves' diamonds and gold up close
    if o.name == "Hall_Carpets":
        lo = 4096    # R586: the 19 m runner is one UV island, so its 7 cm stripes need the pixels
    elif o.name == "Hall_Front_Steps":
        lo = 1024
    return int(min(MAX_PX, max(lo, 2 ** round(math.log2(s)))))


def _unwrap(o):
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    while o.data.uv_layers:
        o.data.uv_layers.remove(o.data.uv_layers[0])
    o.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    size = _size(o)
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=3.0 / size, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return size


def _bake(o, img, kind):
    for m in o.data.materials:
        nt = m.node_tree
        node = nt.nodes.get("Bake Target") or nt.nodes.new("ShaderNodeTexImage")
        node.name = "Bake Target"
        node.image = img
        nt.nodes.active = node
    bpy.ops.object.bake(type=kind, margin=4, use_clear=True)


def _pixels(img, buf):
    img.pixels.foreach_get(buf)
    return buf.reshape(img.size[1], img.size[0], 4)


def _save(buf, path, size, srgb):
    img = bpy.data.images.new(os.path.basename(path), size, size, alpha=True, float_buffer=False)
    img.colorspace_settings.name = "sRGB" if srgb else "Non-Color"
    img.pixels.foreach_set(buf)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def bake(objs, folder):
    # Memory: a 4096 float image is 268 MB and this PC is short of it, so a single float bake image and a
    # single float pixel buffer are reused, the occlusion is kept as one float16 channel, and each map is
    # written the moment it is made.
    import gc
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.render.bake.use_selected_to_active = False
    os.makedirs(folder, exist_ok=True)
    for o in objs:
        if o.type != "MESH" or o.name.replace("_", " ").startswith(SKIP) or not o.data.polygons:
            continue
        mats = list(o.data.materials)
        if not mats or any(m is None or "kind" not in m for m in mats):
            continue
        size = _unwrap(o)
        buf = np.empty(size * size * 4, dtype=np.float32)
        img = bpy.data.images.new(o.name + " bake", size, size, alpha=True, float_buffer=True)
        img.colorspace_settings.name = "Linear Rec.709"
        # 1. ambient occlusion, kept small
        sc.cycles.samples = AO_SAMPLES
        _bake(o, img, "AO")
        ao = _pixels(img, buf)[:, :, 0].astype(np.float16)
        # 2. metallic / smoothness: swap each material's emission for (metal, smooth, 0) -> R metal, A smooth
        keep = {}
        for m in mats:
            em = next(n for n in m.node_tree.nodes if n.type == "EMISSION")
            link = em.inputs["Color"].links[0]
            keep[m.name] = link.from_socket
            m.node_tree.links.remove(link)
            em.inputs["Color"].default_value = (m["metal"], m["smooth"], 0, 1)
        sc.cycles.samples = 1
        _bake(o, img, "EMIT")
        px = _pixels(img, buf)
        px[:, :, 3] = px[:, :, 1]
        px[:, :, 1:3] = 0
        _save(buf, os.path.join(folder, o.name + "_Metal.png"), size, False)
        for m in mats:
            em = next(n for n in m.node_tree.nodes if n.type == "EMISSION")
            m.node_tree.links.new(keep[m.name], em.inputs["Color"])
        # 3. colour (emission bake = the flat colour) x occlusion, to sRGB, in place
        _bake(o, img, "EMIT")
        px = _pixels(img, buf)
        bpy.data.images.remove(img)
        rgb = px[:, :, :3]
        rgb *= (1 - AO_MIX + AO_MIX * ao.astype(np.float32))[:, :, None]
        np.clip(rgb, 0, 1, out=rgb)
        low = rgb <= .0031308
        hi = 1.055 * np.power(rgb, 1 / 2.4) - .055
        rgb *= 12.92
        np.copyto(rgb, hi, where=~low)
        del hi, low, ao
        px[:, :, 3] = 1
        _save(buf, os.path.join(folder, o.name + ".png"), size, True)
        del buf, px, rgb
        gc.collect()
        print("BAKED %-24s %5d px" % (o.name, size), flush=True)
        # the export carries one plain material per mesh; Unity puts the baked maps on it
        plain = bpy.data.materials.get("Baked") or bpy.data.materials.new("Baked")
        o.data.materials.clear()
        o.data.materials.append(plain)
