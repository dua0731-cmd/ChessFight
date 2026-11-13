"""Sword Fight arena kit v1: the board's parts at the sizes the game uses (ArtSource/SwordFight/README.md).

Run headless (from anywhere):
  blender --background --factory-startup --python build_arena_v1.py -- <out_dir>
Units: 1 Blender unit = 1 m. Pivot: bottom centre of each part. Long parts run along Blender Y.

Squares (user 10-28): dark = the walnut texture the user sent (textures/source/walnut_a.webp),
light = light oak #b09a7c, made from the other walnut texture's grain (walnut_b.webp) recoloured.
Both are written as PNGs next to the sources (Unity does not import WebP).
Each part is exported as FBX; the whole board is then laid out as the game lays it out
(SwordFightArena.Build) and rendered in the demo's dark study light: an overview, a close-up,
and the skill effects' colours on both squares. The book stacks, the chess clock and the lamp
come later (Tripo); the lamp's light is here.
"""
import bpy
import bmesh
import math
import os
import sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(HERE, "v1")
os.makedirs(OUT, exist_ok=True)
TEX = os.path.join(HERE, "textures")

CELL = 2.0          # SwordFightMatch.cellSize
SIZE = 8            # SwordBoard.Size
DESK_Y = -6.0       # SwordFightArena.DeskY
UV_METRES = 4.0     # one texture repeat covers 4 m (rails, frame, tray)
SQUARE_UV_METRES = 6.0   # on the squares 6 m: a 2 m square shows a third of it, about 3.5 planks (10-28, wider planks)
OAK = "#b09a7c"


def srgb(hexstr):
    """'#rrggbb' -> linear RGB (Blender colour inputs are linear)."""
    h = hexstr.lstrip("#")
    out = []
    for k in range(3):
        c = int(h[2 * k:2 * k + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


# ---------------------------------------------------------------- textures


def load_pixels(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    return img, w, h, px.reshape(h, w, 4)


def save_png(name, w, h, px):
    img = bpy.data.images.new(name, w, h, alpha=False)
    img.pixels.foreach_set(px.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png")
    img.file_format = "PNG"
    img.save()
    img.reload()
    print(f"[arena] texture {name}.png {w}x{h}")
    return img


def make_textures():
    """T_SF_Walnut (the user's texture as it is) and T_SF_Oak (the other one's grain on #b09a7c)."""
    _, w, h, walnut = load_pixels(os.path.join(TEX, "source", "walnut_a.webp"))
    dark = save_png("T_SF_Walnut", w, h, walnut)
    _, w, h, grain = load_pixels(os.path.join(TEX, "source", "walnut_b.webp"))
    lum = grain[..., 0] * 0.2126 + grain[..., 1] * 0.7152 + grain[..., 2] * 0.0722
    rel = (lum - lum.mean()) / max(lum.mean(), 1e-4)
    h8 = OAK.lstrip("#")
    target = np.array([int(h8[k:k + 2], 16) / 255.0 for k in (0, 2, 4)], dtype=np.float32)   # sRGB, like the pixels
    oak = np.empty_like(grain)
    oak[..., :3] = np.clip(target[None, None, :] * (1.0 + rel[..., None] * 0.75), 0.0, 1.0)
    oak[..., 3] = 1.0
    light = save_png("T_SF_Oak", w, h, oak)
    return light, dark


# ---------------------------------------------------------------- materials

# name -> (colour, metallic, roughness); the squares, the rail and the desk also get a texture.
PALETTE = {
    "Square_Light": (OAK, 0.0, 0.55),
    "Square_Dark": ("#4e4139", 0.0, 0.5),
    "Trim_Bronze": ("#5a4a3a", 0.8, 0.45),
    "Rail_Wood": ("#5e4a3e", 0.0, 0.5),
    "Frame_Wood": ("#4e4139", 0.0, 0.4),
    "Stone": ("#8d8478", 0.0, 0.85),
    "Desk_Wood": ("#3a2a20", 0.0, 0.55),
    "Band_Dark": ("#2b2420", 0.0, 0.7),
    "Band_Light": ("#f3ecdd", 0.0, 0.7),
}
TEXTURED = {}   # material name -> (image, tint multiplier)


def mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    col, metal, rough = PALETTE[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    rgb = srgb(col)
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1.0)
    if name in TEXTURED:
        img, tint = TEXTURED[name]
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        if tint == 1.0:
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        else:
            mul = nt.nodes.new("ShaderNodeMix")
            mul.data_type = "RGBA"
            mul.blend_type = "MULTIPLY"
            # The colour sockets of a Mix node share their names with the float ones: by index (A 6, B 7, Result 2).
            mul.inputs[0].default_value = 1.0
            mul.inputs[7].default_value = (tint, tint, tint, 1.0)
            nt.links.new(tex.outputs["Color"], mul.inputs[6])
            nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
    return m


# ---------------------------------------------------------------- mesh helpers


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def link(o, coll=None):
    (coll or bpy.context.scene.collection).objects.link(o)
    return o


def box_uv(bm, metres=UV_METRES):
    """Project UVs per face along its main axis, in metres / <metres> (one texture repeat)."""
    uv = bm.loops.layers.uv.verify()
    bm.normal_update()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda k: abs(n[k]))
        for loop in f.loops:
            p = loop.vert.co
            if ax == 2:
                u, v = p.x, p.y
            elif ax == 0:
                u, v = p.y, p.z
            else:
                u, v = p.x, p.z
            loop[uv].uv = (u / metres + 0.5, v / metres + 0.5)


def mesh_object(name, bm, material, uv_metres=UV_METRES):
    """material: one name, or a list (faces carry the slot index, see tagged)."""
    box_uv(bm, uv_metres)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in material if isinstance(material, (list, tuple)) else [material]:
        me.materials.append(mat(m))
    return bpy.data.objects.new(name, me)


def tagged(bm, slot, build):
    """Run build(bm) and give the faces it made material slot <slot>."""
    before = set(bm.faces)
    build(bm)
    for f in bm.faces:
        if f not in before:
            f.material_index = slot


def extrude_profile(bm, profile, length):
    """A closed (x, z) profile swept along Y from -length/2 to length/2, both ends capped."""
    ends = [[bm.verts.new((x, y, z)) for x, z in profile] for y in (-length / 2, length / 2)]
    n = len(profile)
    for k in range(n):
        bm.faces.new((ends[0][k], ends[0][(k + 1) % n], ends[1][(k + 1) % n], ends[1][k]))
    bm.faces.new(list(reversed(ends[0])))
    bm.faces.new(ends[1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)


def box_bm(sx, sy, sz, z0=0.0, bevel=0.0, segments=2, top_only=False, bm=None, offset=(0.0, 0.0)):
    """A box sx * sy * sz standing on z0 (bottom centre at offset), its edges bevelled."""
    own = bm is None
    bm = bm or bmesh.new()
    new = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    for v in new:
        v.co = Vector((v.co.x * sx + offset[0], v.co.y * sy + offset[1], v.co.z * sz + sz / 2 + z0))
    if bevel > 0:
        top = z0 + sz - 1e-5
        newset = set(new)
        edges = [e for e in bm.edges if all(v in newset for v in e.verts)
                 and (not top_only or all(v.co.z >= top for v in e.verts))]
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=segments, profile=0.5, affect="EDGES")
    return bm if own else None


def smooth(o, angle=35):
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    except Exception:
        pass
    o.select_set(False)


# ---------------------------------------------------------------- parts


def square(name, material):
    """SF_Square_*: 1.96 x 0.6 x 1.96 (4 cm gap on a 2 m cell), every edge rounded 4.5 cm (user 10-28, the
    references' soft blocks): the gaps read as grooves."""
    return mesh_object(name, box_bm(CELL - 0.04, CELL - 0.04, 0.6, bevel=0.045, segments=3), material, SQUARE_UV_METRES)


# The frame's section (x outward from the board's edge line, z up; the board's top is z 0 in the game,
# the frame's bottom -0.62 like the old brass): a walnut band 17 cm wide whose outer top edge is
# rounded (radius 6 cm) and stands 4 cm above the squares, a thin bronze line set into its side.
FRAME_TOP, FRAME_BOTTOM, FRAME_IN, FRAME_OUT, FRAME_R = 0.04, -0.62, -0.02, 0.15, 0.06
BRONZE_LINE = (-0.165, -0.135, 0.007)   # z from, z to, how far it stands out


def frame_section():
    arc = [(FRAME_OUT - FRAME_R + FRAME_R * math.cos(a), FRAME_TOP - FRAME_R + FRAME_R * math.sin(a))
           for a in (k / 6 * math.pi / 2 for k in range(7))]
    return [(FRAME_IN, FRAME_BOTTOM), (FRAME_OUT, FRAME_BOTTOM)] + arc + [(FRAME_IN, FRAME_TOP)]


def frame():
    """SF_Frame_2m: one cell side of the board's frame, 2 m long (the rook's E hides it cell by cell, like the
    old brass SF_Trim_2m it replaces). Pivot: on the board's edge line at the frame's bottom; +X points out."""
    bm = bmesh.new()
    tagged(bm, 0, lambda b: extrude_profile(b, frame_section(), CELL))
    z0, z1, out = BRONZE_LINE
    tagged(bm, 1, lambda b: box_bm(out, CELL, z1 - z0, z0=z0, bm=b, offset=(FRAME_OUT + out / 2, 0)))
    # Sit the bottom at z 0 (pivot at the bottom, like every part): the section was drawn in game heights.
    for v in bm.verts:
        v.co.z -= FRAME_BOTTOM
    return mesh_object("SF_Frame_2m", bm, ["Frame_Wood", "Trim_Bronze"])


def frame_corner():
    """SF_Frame_Corner: a quarter turn of the frame's section round the board's corner (pivot there, bottom),
    filling the gap where two sides' frames meet. Placed on the corner square (+X, +Y quarter)."""
    sec = [(x - FRAME_IN, z - FRAME_BOTTOM) for x, z in frame_section()]
    bm = bmesh.new()
    tagged(bm, 0, lambda b: lathe_bm(sec + [(0.0, 0.0)], 10, b, quarter=True))
    z0, z1, out = BRONZE_LINE
    r0 = FRAME_OUT - FRAME_IN
    line = [(r0, z0 - FRAME_BOTTOM), (r0 + out, z0 - FRAME_BOTTOM), (r0 + out, z1 - FRAME_BOTTOM), (r0, z1 - FRAME_BOTTOM)]
    tagged(bm, 1, lambda b: lathe_bm(line, 10, b, quarter=True))
    for v in bm.verts:   # the turn is round (FRAME_IN, FRAME_IN): back to the board's corner
        v.co.x += FRAME_IN
        v.co.y += FRAME_IN
    return mesh_object("SF_Frame_Corner", bm, ["Frame_Wood", "Trim_Bronze"])


def rail():
    """SF_Rail_1p9: 0.14 x 1.9 x 0.42 overall, a low rail whose top is fully rounded, with a bronze cap
    (0.16 x 0.03 x 0.44) at each end: the rails of neighbouring squares read as posts and beams."""
    cap = 0.03
    bm = bmesh.new()
    tagged(bm, 0, lambda b: box_bm(0.14, 0.95 * CELL - 2 * cap, 0.42, bevel=0.066, segments=6, top_only=True, bm=b))
    for sy in (-1, 1):
        tagged(bm, 1, lambda b, sy=sy: box_bm(0.16, cap, 0.44, bevel=0.012, segments=2, bm=b,
                                              offset=(0.0, sy * (0.95 * CELL / 2 - cap / 2))))
    return mesh_object("SF_Rail_1p9", bm, ["Rail_Wood", "Trim_Bronze"])


# SwordFightShapes.Pedestal's profile (radius, height) times its scale (0.85, 0.5, 0.85).
PEDESTAL = [(r * 0.85, h * 0.5) for r, h in
            [(0, 0), (0.85, 0), (0.85, 0.15), (0.7, 0.25), (0.62, 0.95), (0.8, 1.05), (0.8, 1.35), (0, 1.35)]]


def lathe_bm(profile, segments, bm=None, quarter=False):
    """The (radius, height) profile turned round Z: a full turn, or (quarter) 0-90 degrees with open ends."""
    bm = bm or bmesh.new()
    rings = []
    count = segments + 1 if quarter else segments
    for s in range(count):
        a = s / segments * (math.pi / 2 if quarter else math.tau)
        rings.append([bm.verts.new((r * math.cos(a), r * math.sin(a), h)) for r, h in profile])
    for s in range(segments):
        ra, rb = rings[s], rings[(s + 1) % count]
        for k in range(len(profile) - 1):
            uniq = []
            for v in (ra[k], rb[k], rb[k + 1], ra[k + 1]):
                if all((v.co - u.co).length > 1e-6 for u in uniq):
                    uniq.append(v)
            if len(uniq) >= 3:
                bm.faces.new(uniq)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def pedestal():
    """SF_RookPedestal: the turned stone body and six battlements on top (0.79 m)."""
    bm = lathe_bm(PEDESTAL, 40)
    for k in range(6):
        a = k / 6 * math.tau
        box_bm(0.238, 0.238, 0.11, z0=0.675, bevel=0.01, segments=1, bm=bm,
               offset=(math.cos(a) * 0.527, math.sin(a) * 0.527))
    return mesh_object("SF_RookPedestal", bm, "Stone", 1.0)


def pedestal_collider():
    """SF_RookPedestal_Col: the body's convex hull, no battlements, well under Unity's 255 faces."""
    bm = bmesh.new()
    for s in range(16):
        a = s / 16 * math.tau
        for r, h in PEDESTAL:
            if r > 0:
                bm.verts.new((r * math.cos(a), r * math.sin(a), h))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    return mesh_object("SF_RookPedestal_Col", bm, "Stone", 1.0)


def desk():
    """SF_Desk: the study desk's top, 80 x 50 x 1 (the game puts it at -7, its top at -6)."""
    return mesh_object("SF_Desk", box_bm(80.0, 50.0, 1.0, bevel=0.05, segments=2, top_only=True), "Desk_Wood", 8.0)


def tray():
    """SF_Tray: a shallow tray 2.2 x 10 x 0.15 (floor 4 cm, walls 6 cm)."""
    bm = box_bm(2.2, 10.0, 0.04)
    for sx, sy, ox, oy in ((2.2, 0.06, 0, 4.97), (2.2, 0.06, 0, -4.97), (0.06, 10.0, 1.07, 0), (0.06, 10.0, -1.07, 0)):
        box_bm(sx, sy, 0.15, bevel=0.01, segments=1, bm=bm, offset=(ox, oy))
    return mesh_object("SF_Tray", bm, "Trim_Bronze")


def export(o):
    bpy.ops.object.select_all(action="DESELECT")
    link(o)
    smooth(o)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, o.name + ".fbx"),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
    )
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    print(f"[arena] {o.name}: {tuple(round(d, 3) for d in o.dimensions)} m, {tris} tris")
    return o


# ---------------------------------------------------------------- the board as the game lays it out


def centre(i, j):
    return ((i - SIZE / 2 + 0.5) * CELL, (j - SIZE / 2 + 0.5) * CELL)


def place(part, loc, rot_z=0.0, coll=None, name=None):
    o = bpy.data.objects.new(name or part.name, part.data)
    o.location = loc
    o.rotation_euler.z = math.radians(rot_z)
    return link(o, coll)


def assemble(parts):
    coll = bpy.data.collections.new("Arena (preview)")
    bpy.context.scene.collection.children.link(coll)
    half = SIZE * CELL / 2
    k = CELL / 2
    for j in range(SIZE):
        for i in range(SIZE):
            cx, cy = centre(i, j)
            light = (i + j) % 2 == 0          # a1 is a light square, as in the game
            sq = place(parts["SF_Square_Light" if light else "SF_Square_Dark"], (cx, cy, -0.6), coll=coll,
                       name=f"Square {'abcdefgh'[i]}{j + 1}")
            # Every other square of a colour turned a quarter: the planks do not all run the same way.
            if (i // 2 + j) % 2 == 1:
                sq.rotation_euler.z = math.radians(90)
            # The frame: +X of the part points away from the board.
            fz = FRAME_BOTTOM
            if i == 0:
                place(parts["SF_Frame_2m"], (-half, cy, fz), 180, coll=coll)
            if i == SIZE - 1:
                place(parts["SF_Frame_2m"], (half, cy, fz), 0, coll=coll)
            if j == 0:
                place(parts["SF_Frame_2m"], (cx, -half, fz), -90, coll=coll)
            if j == SIZE - 1:
                place(parts["SF_Frame_2m"], (cx, half, fz), 90, coll=coll)
            for (ci, cj), turn in (((SIZE - 1, SIZE - 1), 0), ((0, SIZE - 1), 90), ((0, 0), 180), ((SIZE - 1, 0), 270)):
                if (i, j) == (ci, cj):
                    place(parts["SF_Frame_Corner"], (half if i else -half, half if j else -half, fz), turn, coll=coll)
            if (i == 0 or i == SIZE - 1) and 2 <= j <= 5:
                place(parts["SF_Rail_1p9"], (cx + (-0.9 if i == 0 else 0.9) * k, cy, 0.0), coll=coll)
            if j == 0 or j == SIZE - 1:   # the home-rank bands (drawn by the game's code, here for the look)
                band = mesh_object("Home band", box_bm(1.8 * k, 0.12, 0.004), "Band_Dark" if j == 0 else "Band_Light")
                band.location = (cx, cy + (0.75 if j == 0 else -0.75) * k, 0.0)
                link(band, coll)
    for i, j in ((1, 1), (6, 1), (1, 6), (6, 6)):
        cx, cy = centre(i, j)
        place(parts["SF_RookPedestal"], (cx, cy, 0.0), coll=coll)
    place(parts["SF_Desk"], (0, 0, DESK_Y - 1.0), coll=coll)
    for sx in (-1, 1):
        place(parts["SF_Tray"], (sx * 12 * k, 0.5 * k, DESK_Y), coll=coll)
    return coll


# ---------------------------------------------------------------- skill-effect colour check


def emit(name, hexstr=None, strength=2.5, rgb=None, dark=None, alpha=1.0):
    """An emissive decal material; dark = a non-emissive colour that darkens (the queen's scorch).
    Strengths stay near the game's (VFX README section 6: rings x1.4-1.6) so the colours do not wash out to white."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    if dark is not None:
        bsdf.inputs["Base Color"].default_value = (*dark, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.9
    else:
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        bsdf.inputs["Emission Color"].default_value = (*(rgb or srgb(hexstr)), 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    bsdf.inputs["Alpha"].default_value = alpha
    return m


def decal(name, bm, material, loc, rot_z=0.0, coll=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler.z = math.radians(rot_z)
    return link(o, coll)


def ring_bm(r_out, r_in, segments=64):
    bm = bmesh.new()
    angles = [s / segments * math.tau for s in range(segments)]
    outer = [bm.verts.new((r_out * math.cos(a), r_out * math.sin(a), 0)) for a in angles]
    inner = [bm.verts.new((r_in * math.cos(a), r_in * math.sin(a), 0)) for a in angles]
    for s in range(segments):
        t = (s + 1) % segments
        bm.faces.new((outer[s], outer[t], inner[t], inner[s]))
    return bm


def strip_bm(length, width):
    bm = bmesh.new()
    vs = [bm.verts.new((x * length / 2, y * width / 2, 0)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    bm.faces.new(vs)
    return bm


def text(body, loc, coll):
    cu = bpy.data.curves.new("label", "FONT")
    cu.body = body
    cu.size = 0.32
    cu.align_x = "CENTER"
    o = bpy.data.objects.new("Label " + body, cu)
    o.location = loc
    cu.materials.append(emit("Label", "#ffffff", 1.2))
    return link(o, coll)


def vfx_check():
    """Each piece's effect colour laid on a light and a dark square of ranks 4-5 (files c-h)."""
    coll = bpy.data.collections.new("VFX colour check")
    bpy.context.scene.collection.children.link(coll)
    z = 0.006
    king = emit("King gold", "#C9952F", 1.3)
    rook = emit("Rook beige", "#D3B596", 1.0)
    silver = emit("Bishop silver", "#C2C9D3", 1.0)
    red = emit("Queen ember", rgb=(0.6, 0.07, 0.06), strength=1.6)
    scorch = emit("Queen scorch", dark=(0.02, 0.008, 0.008))
    warn = emit("Warning teal", "#3fd6c8", 0.5, alpha=0.55)
    # King and rook: rings 1.6 m across on c4 (dark) / d4 (light), e4 (dark) / f4 (light).
    for f, m, label in ((2, king, "KING"), (3, king, None), (4, rook, "ROOK"), (5, rook, None)):
        cx, cy = centre(f, 3)
        decal("ring", ring_bm(0.8, 0.66), m, (cx, cy, z), coll=coll)
        decal("ring", ring_bm(0.45, 0.4), m, (cx, cy, z), coll=coll)
        if label:
            text(label, (cx + CELL / 2, cy - 1.25, z), coll)
    # Bishop: the silver X on c5 (light) / d5 (dark).
    for f in (2, 3):
        cx, cy = centre(f, 4)
        for rz in (45, -45):
            decal("x", strip_bm(1.6, 0.1), silver, (cx, cy, z), rz, coll)
        decal("x ring", ring_bm(0.72, 0.66), silver, (cx, cy, z), coll=coll)
    text("BISHOP", (centre(2, 4)[0] + CELL / 2, centre(2, 4)[1] + 1.15, z), coll)
    # Queen: a scorched line with an ember rim across e5 (light) and f5 (dark).
    cx, cy = (centre(4, 4)[0] + centre(5, 4)[0]) / 2, centre(4, 4)[1]
    decal("scorch", strip_bm(3.8, 0.28), scorch, (cx, cy, z), coll=coll)
    for oy in (-0.15, 0.15):
        decal("ember", strip_bm(3.8, 0.035), red, (cx, cy + oy, z + 0.001), coll=coll)
    text("QUEEN", (cx, cy + 1.15, z), coll)
    # The square warning (teal proposed): g4 (dark) and g5 (light).
    for j in (3, 4):
        cx, cy = centre(6, j)
        decal("warning", strip_bm(1.9, 1.9), warn, (cx, cy, z), coll=coll)
    text("WARNING", (centre(6, 4)[0], centre(6, 4)[1] + 1.15, z), coll)
    return coll


# ---------------------------------------------------------------- the study light and the renders


def look(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_render():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    try:
        sc.eevee.taa_render_samples = 64
    except AttributeError:
        pass
    # The demo's dark room: the camera sees #140f0c, the light bounces a little cooler and brighter.
    world = bpy.data.worlds.new("study")
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb("#140f0c"), 1.0)
    fill = nt.nodes.new("ShaderNodeBackground")
    fill.inputs["Color"].default_value = (*srgb("#7d7a86"), 1.0)
    fill.inputs["Strength"].default_value = 0.35
    path = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(fill.outputs[0], mix.inputs[1])
    nt.links.new(bg.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], nt.nodes["World Output"].inputs["Surface"])
    sc.world = world
    # The warm key light (the game's sun: toward +x, down, +z in Unity = +x, +y, -z here).
    sun = link(bpy.data.objects.new("Key light", bpy.data.lights.new("Key light", "SUN")))
    sun.data.energy = 2.6
    sun.data.color = srgb("#fff1dc")
    sun.data.angle = math.radians(3)
    sun.rotation_euler = Vector((9, 8, -20)).to_track_quat("-Z", "Y").to_euler()
    # The banker's lamp's spot (Unity (-7, 15, 8) -> here (-7, 8, 15)), looking at the board's centre.
    spot = link(bpy.data.objects.new("Lamp light", bpy.data.lights.new("Lamp light", "SPOT")))
    spot.data.energy = 9000
    spot.data.color = srgb("#ffcf8a")
    spot.data.spot_size = math.radians(92)
    spot.data.spot_blend = 0.6
    spot.location = (-7, 8, 15)
    look(spot, (0, 0, 0))
    cam = link(bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera")))
    sc.camera = cam
    return cam


def render(cam, name, loc, target, lens):
    cam.location = loc
    cam.data.lens = lens
    look(cam, target)
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[arena] rendered {name}.png")


if __name__ == "__main__":
    reset()
    light, dark = make_textures()
    TEXTURED.update({"Square_Light": (light, 1.0), "Square_Dark": (dark, 1.0),
                     "Rail_Wood": (dark, 1.2), "Frame_Wood": (dark, 0.85), "Desk_Wood": (dark, 0.75)})
    stale = os.path.join(OUT, "SF_Trim_2m.fbx")   # the brass strip the frame replaced (10-28)
    if os.path.exists(stale):
        os.remove(stale)
    parts = {}
    for o in (square("SF_Square_Light", "Square_Light"), square("SF_Square_Dark", "Square_Dark"), frame(), frame_corner(), rail(),
              pedestal(), pedestal_collider(), desk(), tray()):
        parts[o.name] = export(o)
    # The parts stay in the file (at the origin, hidden) next to the laid-out board.
    for o in parts.values():
        o.hide_render = True
        o.hide_set(True)
    assemble(parts)
    cam = setup_render()
    render(cam, "SF_Arena_v1_overview", (17, -23, 15), (0, 0, -1.5), 30)
    render(cam, "SF_Arena_v1_closeup", (-11.5, -12.5, 3.2), (-6.2, -4.6, 0.2), 32)
    checks = vfx_check()
    render(cam, "SF_Arena_v1_vfx", (2.0, -8.5, 9.0), (2.0, 0.6, 0.0), 34)
    checks.hide_render = True
    # No .blend1 backups, and the textures by relative path (the file opens on any PC with this folder).
    bpy.context.preferences.filepaths.save_version = 0
    blend = os.path.join(OUT, "SF_Arena_v1.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    print("[arena] done")
