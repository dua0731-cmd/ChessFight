# The entrance screen's backdrop (10-17, 승규 님: background B "성문 앞 광장", after the PAWN RUSH key art).
# v2 (승규 님: "좀 더 디테일 하게"): a grand cream-stone chess castle seen from the foot of its stair, now with
# real masonry detail: carved arch stones, quoined jambs, corbel tables under every parapet, battered plinths,
# string courses, buttresses, arrow slits in stone surrounds and arched windows; round towers with brick
# laid round them, some crenellated, some under slate cones with gold finials, all flying flags; a gate
# with a raised iron portcullis, a balcony, the royal crown and two heraldic shields, wall lanterns; a
# balustraded stair with king newels, braziers and two giant knight statues (ivory left, ebony right, the
# two teams); a keep with a rose window and the giant gold crown (beaded rims, jewels, arches, orb and cross);
# chess-piece spires (king and bishop) behind the walls, towers lost in the haze far behind, birds.
# v4 (승규 님: broken stone steps like the key art, the hillsides cracked and broken stone paving): worn block
# steps with chips, splits, gaps and rubble (no carpet); broken slab paving on the plaza, the landings and the
# hillsides over dark earth, rubble everywhere.
# v3 (승규 님: the gate bigger, the roof like the key art, the stair longer, the structures round it realistic):
# a great gate (10 m wide, 11.5 m high), every tower crenellated like a rook (no cones), the keep roofed by
# a giant gold crown (jewels in claw settings round its band, eight points with balls, scallops, a velvet
# dome under gold arches, orb and cross); two long flights with a landing between them, crenellated stepped
# walls, gate piers with king finials, turrets and braziers on the landing, banner poles, lantern posts,
# a giant ivory rook and ebony bishop at the foot, the knights on the gate terrace; the whole on a rocky
# hill with boulders, seen low from the plaza at the stair's foot.
# Built from the lobby castle's helpers and materials (build_castle.py above its layout, build_hall.py) and
# the Tripo parts' helpers (tripo4.py).
# Usage: blender -b --factory-startup --python build_gate_castle.py -- <out_dir> [samples]   (env PCT = size %)
import bpy, bmesh, math, os, sys, random
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
_args = sys.argv[sys.argv.index("--") + 1:]
OUT_DIR = _args[0]; _SAMPLES = _args[1] if len(_args) > 1 else "48"
sys.argv = sys.argv[:sys.argv.index("--") + 1] + [OUT_DIR, _SAMPLES]
LAYOUT_MARK = "# ================= " + "layout (Blender Y"
_castle_src = open(os.path.join(HERE, "build_castle.py"), encoding="utf-8").read()
exec(_castle_src.split(LAYOUT_MARK)[0])
# the knight statues' marble with its gold-banded pedestal, as in the lobby
exec(_castle_src[_castle_src.index("def knight_material"):_castle_src.index("KNIGHT_SRC")])
# zone_material, cmp, mul, absn, dist3 (Tripo crown and shield paint)
_t4 = open(os.path.join(HERE, "tripo4.py"), encoding="utf-8").read()
exec(_t4[_t4.index("def zone_material"):_t4.index("# ---- hide the code-made arch pieces")])
# the title screen's pennants (deep red / ebony cloth, inset gold lines, notched tail, gold chess glyph)
exec(_castle_src[_castle_src.index("# pennants: the title screen's own"):_castle_src.index("RED_CLOTH, EBONY_CLOTH")])
_HANG = [0]
def hang(x, y, z_top, w, h):
    """A pennant in the title screen's look; red and ebony and the four glyphs in turn."""
    i = _HANG[0]; _HANG[0] += 1
    pennant(x, y, z_top, w, h, ("7A1F24", "1C1612")[i % 2], "♕♘♖♗"[i % 4], sway=i * 1.3)
SAMPLES = int(_SAMPLES)
C = "Castle"
RNG = random.Random(1017)
DL = r"C:\Users\kerri\Downloads"
TAU = 2 * math.pi

def bx(name, center, size, mat, bevel=.04, coll_name=C):
    return box(name, center, size, mat, coll_name, bevel=bevel)

# ================= materials =================
def masonry(name, mode="XZ", c1="efe3c6", c2="e2d2ae", mortar="b5a383", bw=.9, bh=.42, rough=.7, bump=.8, mortar_size=.028):
    """Laid stone: two-tone blocks, each a little its own shade, mottled, darker near the ground, faint rain
    streaks, recessed mortar. mode: XZ / YZ / XY planes or CYL (laid round a vertical axis)."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; L = nt.links.new
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); b.inputs['Roughness'].default_value = rough
    tc = nt.nodes.new('ShaderNodeTexCoord'); obj_v = tc.outputs['Object']
    sep = nt.nodes.new('ShaderNodeSeparateXYZ'); L(obj_v, sep.inputs[0])
    cmb = nt.nodes.new('ShaderNodeCombineXYZ')
    if mode == "CYL":
        at = nt.nodes.new('ShaderNodeMath'); at.operation = 'ARCTAN2'; L(sep.outputs['Y'], at.inputs[0]); L(sep.outputs['X'], at.inputs[1])
        xy = nt.nodes.new('ShaderNodeCombineXYZ'); L(sep.outputs['X'], xy.inputs[0]); L(sep.outputs['Y'], xy.inputs[1])
        ln = nt.nodes.new('ShaderNodeVectorMath'); ln.operation = 'LENGTH'; L(xy.outputs[0], ln.inputs[0])
        u = nt.nodes.new('ShaderNodeMath'); u.operation = 'MULTIPLY'; L(at.outputs[0], u.inputs[0]); L(ln.outputs['Value'], u.inputs[1])
        L(u.outputs[0], cmb.inputs[0]); L(sep.outputs['Z'], cmb.inputs[1])
    elif mode == "XZ": L(sep.outputs['X'], cmb.inputs[0]); L(sep.outputs['Z'], cmb.inputs[1])
    elif mode == "YZ": L(sep.outputs['Y'], cmb.inputs[0]); L(sep.outputs['Z'], cmb.inputs[1])
    else: L(sep.outputs['X'], cmb.inputs[0]); L(sep.outputs['Y'], cmb.inputs[1])
    v = cmb.outputs[0]
    br = nt.nodes.new('ShaderNodeTexBrick'); L(v, br.inputs['Vector'])
    br.inputs['Scale'].default_value = 1.0; br.inputs['Brick Width'].default_value = bw; br.inputs['Row Height'].default_value = bh
    br.inputs['Mortar Size'].default_value = mortar_size; br.inputs['Mortar Smooth'].default_value = .35
    br.inputs['Color1'].default_value = srgb(c1); br.inputs['Color2'].default_value = srgb(c2); br.inputs['Mortar'].default_value = srgb(mortar)
    br.offset = .5; br.squash = 1.0
    br2 = nt.nodes.new('ShaderNodeTexBrick'); L(v, br2.inputs['Vector'])
    for k in ('Scale', 'Brick Width', 'Row Height', 'Mortar Size'): br2.inputs[k].default_value = br.inputs[k].default_value
    br2.inputs['Color1'].default_value = (1, 1, 1, 1); br2.inputs['Color2'].default_value = (.8, .76, .7, 1); br2.inputs['Mortar'].default_value = (1, 1, 1, 1)
    br2.offset = .5
    tint = nt.nodes.new('ShaderNodeMix'); tint.data_type = 'RGBA'; tint.blend_type = 'MULTIPLY'; tint.inputs[0].default_value = 1
    L(br.outputs['Color'], tint.inputs[6]); L(br2.outputs['Color'], tint.inputs[7])
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 2.5; nz.inputs['Detail'].default_value = 8; L(obj_v, nz.inputs['Vector'])
    mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.blend_type = 'MULTIPLY'; mx.inputs[0].default_value = .28
    L(tint.outputs[2], mx.inputs[6]); L(nz.outputs['Color'], mx.inputs[7])
    # each object a touch its own shade
    info = nt.nodes.new('ShaderNodeObjectInfo')
    rv = nt.nodes.new('ShaderNodeMapRange'); L(info.outputs['Random'], rv.inputs['Value']); rv.inputs['To Min'].default_value = .93; rv.inputs['To Max'].default_value = 1.03
    gw = nt.nodes.new('ShaderNodeMapRange'); gw.clamp = True; L(sep.outputs['Z'], gw.inputs['Value'])
    gw.inputs['From Min'].default_value = 0; gw.inputs['From Max'].default_value = 2.2; gw.inputs['To Min'].default_value = .74; gw.inputs['To Max'].default_value = 1.0
    st = nt.nodes.new('ShaderNodeTexWave'); st.wave_type = 'BANDS'; st.bands_direction = 'X'; st.inputs['Scale'].default_value = 1.6; st.inputs['Distortion'].default_value = 8
    L(obj_v, st.inputs['Vector'])
    stm = nt.nodes.new('ShaderNodeMapRange'); L(st.outputs['Fac'], stm.inputs['Value']); stm.inputs['To Min'].default_value = .92; stm.inputs['To Max'].default_value = 1.0
    k1 = nt.nodes.new('ShaderNodeMath'); k1.operation = 'MULTIPLY'; L(gw.outputs['Result'], k1.inputs[0]); L(stm.outputs['Result'], k1.inputs[1])
    k2 = nt.nodes.new('ShaderNodeMath'); k2.operation = 'MULTIPLY'; L(k1.outputs[0], k2.inputs[0]); L(rv.outputs['Result'], k2.inputs[1])
    fin = nt.nodes.new('ShaderNodeMix'); fin.data_type = 'RGBA'; fin.blend_type = 'MULTIPLY'; fin.inputs[0].default_value = 1
    L(mx.outputs[2], fin.inputs[6]); L(k2.outputs[0], fin.inputs[7]); L(fin.outputs[2], b.inputs['Base Color'])
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1; L(br.outputs['Fac'], inv.inputs[1])
    rn = nt.nodes.new('ShaderNodeTexNoise'); rn.inputs['Scale'].default_value = 14; rn.inputs['Detail'].default_value = 10; L(obj_v, rn.inputs['Vector'])
    hm = nt.nodes.new('ShaderNodeMath'); hm.operation = 'MULTIPLY_ADD'; L(rn.outputs['Fac'], hm.inputs[0]); hm.inputs[1].default_value = .18; L(inv.outputs[0], hm.inputs[2])
    bp = nt.nodes.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = bump; bp.inputs['Distance'].default_value = .035
    L(hm.outputs[0], bp.inputs['Height']); L(bp.outputs['Normal'], b.inputs['Normal'])
    MATS[name] = m; return m

def dressed(name, c1, c2, rough=.55, bump=.3):
    """Dressed stone for carved parts: each piece its own shade (object random), fine grain and tooling."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; L = nt.links.new
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); b.inputs['Roughness'].default_value = rough
    info = nt.nodes.new('ShaderNodeObjectInfo')
    r = nt.nodes.new('ShaderNodeValToRGB'); e = r.color_ramp.elements
    e[0].position, e[0].color = 0, srgb(c2); e[1].position, e[1].color = 1, srgb(c1)
    L(info.outputs['Random'], r.inputs['Fac'])
    tc = nt.nodes.new('ShaderNodeTexCoord'); nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 7; nz.inputs['Detail'].default_value = 10
    L(tc.outputs['Object'], nz.inputs['Vector'])
    mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.blend_type = 'MULTIPLY'; mx.inputs[0].default_value = .2
    L(r.outputs['Color'], mx.inputs[6]); L(nz.outputs['Color'], mx.inputs[7]); L(mx.outputs[2], b.inputs['Base Color'])
    n2 = nt.nodes.new('ShaderNodeTexNoise'); n2.inputs['Scale'].default_value = 40; n2.inputs['Detail'].default_value = 6; L(tc.outputs['Object'], n2.inputs['Vector'])
    bp = nt.nodes.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = bump; bp.inputs['Distance'].default_value = .01
    L(n2.outputs['Fac'], bp.inputs['Height']); L(bp.outputs['Normal'], b.inputs['Normal'])
    MATS[name] = m; return m

WALL = masonry("Castle Masonry")
TOWER = masonry("Tower Masonry", "CYL")
PLINTH = masonry("Plinth Masonry", "XZ", c1="e6d6b4", c2="d6c39c", mortar="a99472", bw=1.3, bh=.6)
SLATE = masonry("Slate Roof", "CYL", c1="5a6894", c2="4a577f", mortar="2d3552", bw=.42, bh=.26, rough=.42, bump=.6, mortar_size=.04)
CARVED = dressed("Carved Stone", "f4ead3", "dccaa2")
CARVED_DARK = dressed("Carved Stone Shade", "cdbb98", "b8a47e")
IRON = principled("Iron", "2a2826", rough=.45, metal=.75)
GLASS = principled("Window Glass", "ffd59a", rough=.15, emit="ffc070", emit_k=2.2)
SHADOW_STONE = principled("Deep Shadow", "3a2a1e", rough=.9)
HAZE_STONE = principled("Haze Stone", "d8d6dc", rough=.8)
HAZE_ROOF = principled("Haze Roof", "a9b4cc", rough=.6)
BIRD = principled("Bird", "3a3e48", rough=.7)
FLAG_RED = principled("Flag Red", "b3202a", rough=.7, sheen=.6)

# ================= detail helpers =================
def corbel_line(x0, x1, yf, z, step=.9):
    """A corbel table along a wall face (looking -Y) at height z: three stepped blocks every `step`."""
    n = max(1, int((x1 - x0) / step))
    for i in range(n):
        x = x0 + (i + .5) * (x1 - x0) / n
        for dz, d, w, h in ((0, .5, .34, .24), (-.24, .34, .27, .22), (-.46, .18, .2, .2)):
            bx("Corbel", (x, yf - d / 2, z + dz), (w, d, h), CARVED, bevel=.025)

def corbel_ring(x, y, r, z, n):
    for i in range(n):
        a = TAU * (i + .5) / n
        for dz, d, w, h in ((0, .5, .34, .24), (-.24, .34, .27, .22), (-.46, .18, .2, .2)):
            o = bx("Corbel", (x + math.cos(a) * (r + d / 2), y + math.sin(a) * (r + d / 2), z + dz), (d, w, h), CARVED, bevel=.025)
            o.rotation_euler = (0, 0, a)

def slit(x, y, z, a, r=0.0, h=1.1):
    """An arrow slit in a stone surround on a wall (a = angle of the face's outward normal)."""
    cx, cy = x + math.cos(a) * r, y + math.sin(a) * r
    f = bx("Slit Surround", (cx + math.cos(a) * .05, cy + math.sin(a) * .05, z), (.12, .5, h + .45), CARVED, bevel=.02); f.rotation_euler = (0, 0, a)
    s = bx("Slit", (cx + math.cos(a) * .1, cy + math.sin(a) * .1, z), (.06, .13, h), SHADOW_STONE, bevel=0); s.rotation_euler = (0, 0, a)
    c = bx("Slit Cross", (cx + math.cos(a) * .1, cy + math.sin(a) * .1, z + h * .15), (.06, .4, .1), SHADOW_STONE, bevel=0); c.rotation_euler = (0, 0, a)

def arched_window(x, yf, z, w, h, gold=True):
    """A tall arched window on a face looking -Y: carved surround, warm glass, a gold bar and sill."""
    r = w / 2
    half_arch("Window Head", x, yf, z + h - r, r, r + .28, .14, CARVED)
    for s in (-1, 1): bx("Window Jamb", (x + s * (r + .14), yf - .07, z + (h - r) / 2), (.28, .14, h - r), CARVED, bevel=.02)
    bx("Window Sill", (x, yf - .14, z - .08), (w + .7, .3, .16), CARVED, bevel=.02)
    half_arch("Window Glass", x, yf + .05, z + h - r, 0, r, .04, GLASS)
    bx("Window Glass", (x, yf + .03, z + (h - r) / 2), (w, .04, h - r), GLASS, bevel=0)
    if gold:
        bx("Window Bar", (x, yf - .02, z + h / 2), (.06, .05, h), GOLD, bevel=0)
        bx("Window Bar", (x, yf - .02, z + (h - r) * .55), (w, .05, .06), GOLD, bevel=0)

def flag(x, y, z, w=2.2, h=1.3, pole=3.0, side=1, phase=0.0):
    """A pole with a gold ball and a waving swallow-tailed red flag flying to `side`."""
    lathe("Flag Pole", [(.07, 0), (.05, pole), (0, pole)], IRON, segs=10, coll_name=C, loc=(x, y, z))
    lathe("Flag Ball", [(0, 0), (.12, .04), (.15, .14), (.1, .25), (0, .28)], GOLD, segs=16, coll_name=C, loc=(x, y, z + pole))
    bm = bmesh.new(); nx, nz = 16, 6; rows = []
    for j in range(nz + 1):
        row = []
        for i in range(nx + 1):
            u, v = i / nx, j / nz
            notch = (1 - abs(v * 2 - 1)) * .2 * max(0, (u - .8) / .2)
            px = side * w * (u - notch)
            wav = math.sin(u * 7 + phase) * .22 * u + math.sin(u * 13 + phase * 2 + v * 3) * .05 * u
            row.append(bm.verts.new((x + px, y + wav, z + pole - .15 - v * h - math.sin(u * 3) * .12 * u)))
        rows.append(row)
    for j in range(nz):
        for i in range(nx):
            bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    me = bpy.data.meshes.new("Flag"); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    obj("Flag", me, FLAG_RED, C)

def round_tower(name, x, y, r, h, roof=False, flags=True, banner_on=True, windows=True, phase=0.0):
    """A round rook tower in coursed masonry: battered foot, string courses, arrow slits and a window on the
    side facing the camera, a gold band, a corbelled parapet with capped merlons, or a slate cone with a
    gold finial; a flag on top and a banner down the front."""
    lathe(name, [(r * 1.14, 0), (r * 1.14, .4), (r * 1.02, 2.2), (r, 2.4), (r, h), (0, h)], TOWER, segs=64, coll_name=C, loc=(x, y, 0))
    for zz in (2.4, h * .42, h * .72):
        lathe(name + " Course", [(r - .01, zz - .1), (r + .1, zz - .1), (r + .12, zz), (r + .1, zz + .1), (r - .01, zz + .1)], CARVED, segs=64, coll_name=C, loc=(x, y, 0))
    lathe(name + " Band", [(r + .03, h - 1.9), (r + .06, h - 1.9), (r + .06, h - 1.72), (r + .03, h - 1.72)], GOLD, segs=64, coll_name=C, loc=(x, y, 0))
    front = -math.pi / 2
    for k, a in enumerate((front - .55, front + .55)):
        slit(x, y, h * .3 + k * .6, a, r)
        slit(x, y, h * .57 - k * .5, a, r)
    # a window on the front, unless a pennant hangs there (its frame poked through the cloth)
    if windows and not banner_on:
        arched_window(x, y - r - .02, h * .74, .9, 1.9)
    elif not banner_on:
        slit(x, y, h * .45, front, r)
    corbel_ring(x, y, r, h - .45, int(TAU * r / .85))
    lathe(name + " Parapet", [(r - .2, h - .2), (r + .55, h - .2), (r + .55, h + .95), (r + .3, h + .95), (r + .3, h + .25), (0, h + .25)],
          TOWER, segs=64, coll_name=C, loc=(x, y, 0))
    lathe(name + " Parapet Cap", [(r + .26, h + .95), (r + .62, h + .95), (r + .62, h + 1.07), (r + .26, h + 1.07)], CARVED, segs=64, coll_name=C, loc=(x, y, 0))
    if roof:
        rh = r * 3.0
        lathe(name + " Roof", [(r + .5, h + 1.0), (r + .55, h + 1.12), (r * .35, h + 1.0 + rh * .7), (.12, h + 1.0 + rh), (0, h + 1.0 + rh)],
              SLATE, segs=64, coll_name=C, loc=(x, y, 0))
        lathe(name + " Roof Rim", [(r + .5, h + 1.02), (r + .62, h + 1.02), (r + .62, h + 1.14), (r + .5, h + 1.14)], GOLD, segs=64, coll_name=C, loc=(x, y, 0))
        lathe(name + " Finial", [(0, 0), (.22, .1), (.28, .35), (.12, .6), (.1, 1.0), (.2, 1.15), (0, 1.4)], GOLD, segs=24, coll_name=C, loc=(x, y, h + .9 + rh))
        if flags: flag(x, y, h + 2.2 + rh, w=r * .8 + 1.2, h=1.0, pole=2.4, side=1, phase=phase)
    else:
        n = 12
        for i in range(n):
            a = TAU * (i + .5) / n
            o = bx("Tower Merlon", (x + math.cos(a) * (r + .42), y + math.sin(a) * (r + .42), h + 1.55), (.5, r * TAU / n * .52, 1.0), TOWER, bevel=.04)
            o.rotation_euler = (0, 0, a)
            o = bx("Merlon Cap", (x + math.cos(a) * (r + .42), y + math.sin(a) * (r + .42), h + 2.1), (.6, r * TAU / n * .6, .1), CARVED, bevel=.02)
            o.rotation_euler = (0, 0, a)
        if flags: flag(x, y, h + .25, w=2.6, h=1.5, pole=4.6, side=1 if x >= 0 else -1, phase=phase)
    if banner_on:
        hang(x, y - r - .14, h - 2.3, r * .85, h * .36)

def curtain(x0, x1, yf, h, thick=3.0, windows=0, slits=2, banners=1):
    """A straight curtain wall facing -Y: battered plinth, string course, buttresses, corbel table, parapet,
    capped merlons, windows / arrow slits between the buttresses, banners."""
    xc, L = (x0 + x1) / 2, x1 - x0
    bx("Curtain Wall", (xc, yf + thick / 2, h / 2), (L, thick, h), WALL, bevel=0)
    bx("Curtain Plinth", (xc, yf - .2, .8), (L, .5, 1.6), PLINTH, bevel=.03)
    bx("Curtain Plinth Cap", (xc, yf - .3, 1.66), (L, .7, .16), CARVED, bevel=.03)
    bx("Curtain Course", (xc, yf - .1, h * .55), (L, .3, .2), CARVED, bevel=.03)
    nb = max(2, int(L / 5.5))
    for i in range(nb + 1):
        bxx = x0 + i * L / nb
        bx("Buttress", (bxx, yf - .45, h * .3), (1.0, .9, h * .6), WALL, bevel=.03)
        bx("Buttress Cap", (bxx, yf - .55, h * .6 + .12), (1.1, 1.1, .25), CARVED, bevel=.04)
    for i in range(nb):
        cx = x0 + (i + .5) * L / nb
        if i < windows: arched_window(cx, yf - .01, h * .62, 1.0, 2.0)
        elif slits: slit(cx, yf, h * .38, -math.pi / 2)
    corbel_line(x0, x1, yf, h - .55)
    bx("Parapet", (xc, yf - .25, h + .4), (L, .7, 1.0), WALL, bevel=.02)
    bx("Parapet Cap", (xc, yf - .3, h + .95), (L + .1, .8, .12), CARVED, bevel=.02)
    merlons((x0, yf - .25, 0), (x1, yf - .25, 0), h + 1.0, WALL, w=1.0, gap=.6, h=1.0, depth=.6)
    n = int((L + .6) // 1.6)
    off = (L - (n * 1.0 + (n - 1) * .6)) / 2
    for i in range(n):
        bx("Merlon Cap", (x0 + off + .5 + i * 1.6, yf - .25, h + 2.05), (1.12, .72, .1), CARVED, bevel=.02)
    for i in range(banners):
        bc = x0 + (i + .5) * L / banners
        hang(bc, yf - .55, h - 1.0, 1.6, h * .38)

def voussoirs(cx, yf, zc, r_in, r_out, depth, n, mat):
    """The arch ring as separate wedge stones with fine joints; the keystone longer."""
    for i in range(n):
        a0 = math.pi * i / n + .006; a1 = math.pi * (i + 1) / n - .006
        ro = r_out + (.45 if i == n // 2 else 0)
        bm = bmesh.new(); segs = 4; front, back = [], []
        for k in range(segs + 1):
            a = a0 + (a1 - a0) * k / segs; c, s = math.cos(a), math.sin(a)
            for yy, lst in ((yf - depth, front), (yf, back)):
                lst.append((bm.verts.new((cx + c * ro, yy, zc + s * ro)), bm.verts.new((cx + c * r_in, yy, zc + s * r_in))))
        for k in range(segs):
            (o0, i0), (o1, i1) = front[k], front[k + 1]; (bo0, bi0), (bo1, bi1) = back[k], back[k + 1]
            bm.faces.new((o0, o1, i1, i0)); bm.faces.new((o0, bo0, bo1, o1)); bm.faces.new((i0, i1, bi1, bi0))
            bm.faces.new((bo1, bo0, bi0, bi1))
        (o0, i0), (bo0, bi0) = front[0], back[0]; bm.faces.new((o0, i0, bi0, bo0))
        (o1, i1), (bo1, bi1) = front[-1], back[-1]; bm.faces.new((o1, bo1, bi1, i1))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=.025, segments=2, affect='EDGES', profile=.5)
        me = bpy.data.meshes.new("Voussoir"); bm.to_mesh(me); bm.free()
        for p in me.polygons: p.use_smooth = True
        obj("Voussoir", me, mat, C)

def quoins(x, yf, z0, z1, depth, mat, w_long=1.2, w_short=.8, course=.6, side=1):
    """Long and short dressed stones up a jamb or a corner, alternating (side: which way they reach)."""
    k = 0; z = z0
    while z < z1 - .05:
        hh = min(course, z1 - z); w = w_long if k % 2 == 0 else w_short
        bx("Quoin", (x + side * (w / 2 - .4), yf - depth / 2, z + hh / 2), (w, depth, hh - .03), mat, bevel=.03)
        z += course; k += 1

def import_glb(fname, coll_name):
    before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=os.path.join(DL, fname))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        for c in o.users_collection: c.objects.unlink(o)
        coll(coll_name).objects.link(o)
    meshes = [o for o in new if o.type == 'MESH']
    for o in new:
        if o.type != 'MESH': o.hide_render = True
    for o in meshes:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
        for p in o.data.polygons: p.use_smooth = True
    return meshes

def grand_crown(x, y, z, s):
    """The crown on the keep: the PAWN RUSH crown (band, points, jewels, cushion) with beaded rims, a row of
    jewels, four arches over the cushion and an orb with a cross on top."""
    big_crown(x, y, z, s)
    for zz, rr in ((.0, .745), (.42, .79)):
        n = 48
        for i in range(n):
            a = TAU * i / n
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=.035 * s, location=(x + math.cos(a) * rr * s, y + math.sin(a) * rr * s, z + zz * s))
            p = bpy.context.active_object; p.data.materials.append(GOLD)
            for f in p.data.polygons: f.use_smooth = True
    for i in range(10):
        a = TAU * (i + .25) / 10
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=.06 * s, location=(x + math.cos(a) * .79 * s, y + math.sin(a) * .79 * s, z + .24 * s))
        p = bpy.context.active_object; p.data.materials.append(JEWEL_R if i % 2 else JEWEL_B); p.scale = (1, 1, .8)
        for f in p.data.polygons: f.use_smooth = True
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        pts = []
        for k in range(17):
            t = k / 16
            pts.append(Vector((x + math.cos(a) * .74 * s * (1 - t), y + math.sin(a) * .74 * s * (1 - t), z + .42 * s + math.sin(t * math.pi / 2) * .55 * s)))
        for p0, p1 in zip(pts, pts[1:]):
            beam_between("Crown Arch", p0, p1, (.07 * s, .07 * s), GOLD, "Crown")
    lathe("Crown Orb", [(0, 0), (.12 * s, .02 * s), (.17 * s, .12 * s), (.12 * s, .22 * s), (0, .24 * s)], GOLD, segs=32, coll_name="Crown", loc=(x, y, z + .95 * s))
    bx("Crown Cross", (x, y, z + 1.32 * s), (.07 * s, .07 * s, .3 * s), GOLD, bevel=.01, coll_name="Crown")
    bx("Crown Cross", (x, y, z + 1.36 * s), (.24 * s, .07 * s, .07 * s), GOLD, bevel=.01, coll_name="Crown")

# ================= v3 helpers =================
from mathutils import noise as mnoise

def lifted(dz, fn, *a, **k):
    """Run a builder made for ground level 0 and stand everything it made on ground at height dz."""
    before = set(bpy.data.objects)
    r = fn(*a, **k)
    for o in bpy.data.objects:
        if o not in before: o.location.z += dz
    return r

def scaled(f, centre, fn, *a, **k):
    """Run a builder, then grow everything it made by f about `centre`."""
    before = set(bpy.data.objects)
    r = fn(*a, **k)
    for o in bpy.data.objects:
        if o not in before:
            o.location = centre + (o.location - centre) * f
            o.scale = (o.scale[0] * f, o.scale[1] * f, o.scale[2] * f)
    return r

def smooth01(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

ROCK = bpy.data.materials.new("Rock"); ROCK.use_nodes = True
_nt = ROCK.node_tree; _L = _nt.links.new
_b = next(n for n in _nt.nodes if n.type == 'BSDF_PRINCIPLED'); _b.inputs['Roughness'].default_value = .88
_tc = _nt.nodes.new('ShaderNodeTexCoord')
_n1 = _nt.nodes.new('ShaderNodeTexNoise'); _n1.inputs['Scale'].default_value = .35; _n1.inputs['Detail'].default_value = 12; _n1.inputs['Roughness'].default_value = .62
_L(_tc.outputs['Object'], _n1.inputs['Vector'])
_r1 = _nt.nodes.new('ShaderNodeValToRGB'); _e = _r1.color_ramp.elements
_e[0].position, _e[0].color = .3, srgb("8a8073"); _e[1].position, _e[1].color = .72, srgb("cdbfa6")
_e2 = _e.new(.5); _e2.color = srgb("ad9f8a")
_L(_n1.outputs['Fac'], _r1.inputs['Fac'])
_vo = _nt.nodes.new('ShaderNodeTexVoronoi'); _vo.inputs['Scale'].default_value = .5; _vo.feature = 'DISTANCE_TO_EDGE'
_L(_tc.outputs['Object'], _vo.inputs['Vector'])
_cr = _nt.nodes.new('ShaderNodeMapRange'); _L(_vo.outputs['Distance'], _cr.inputs['Value'])
_cr.inputs['From Min'].default_value = 0; _cr.inputs['From Max'].default_value = .05; _cr.inputs['To Min'].default_value = .88; _cr.inputs['To Max'].default_value = 1.0
_mx = _nt.nodes.new('ShaderNodeMix'); _mx.data_type = 'RGBA'; _mx.blend_type = 'MULTIPLY'; _mx.inputs[0].default_value = 1
_L(_r1.outputs['Color'], _mx.inputs[6]); _L(_cr.outputs['Result'], _mx.inputs[7]); _L(_mx.outputs[2], _b.inputs['Base Color'])
_n2 = _nt.nodes.new('ShaderNodeTexNoise'); _n2.inputs['Scale'].default_value = 6; _n2.inputs['Detail'].default_value = 12; _L(_tc.outputs['Object'], _n2.inputs['Vector'])
_hh = _nt.nodes.new('ShaderNodeMath'); _hh.operation = 'MULTIPLY_ADD'; _L(_n2.outputs['Fac'], _hh.inputs[0]); _hh.inputs[1].default_value = .5; _L(_cr.outputs['Result'], _hh.inputs[2])
_bp = _nt.nodes.new('ShaderNodeBump'); _bp.inputs['Strength'].default_value = .9; _bp.inputs['Distance'].default_value = .12
_L(_hh.outputs[0], _bp.inputs['Height']); _L(_bp.outputs['Normal'], _b.inputs['Normal'])

def boulder(x, y, z, r, seed, squash=.7):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=r)
    for v in bm.verts:
        p = v.co.copy()
        d = mnoise.fractal(p * (1.3 / r) + Vector((seed, seed * .7, seed * .3)), .55, 2.0, 4) * .28 * r
        v.co = p + p.normalized() * d
        v.co.z *= squash
    me = bpy.data.meshes.new("Boulder"); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = obj("Boulder", me, ROCK, "Terrain", (x, y, z)); o.rotation_euler = (0, 0, seed); return o

def lantern_post(x, y, z, h=2.4):
    lathe("Lantern Post", [(.12, 0), (.1, .2), (.07, .3), (.06, h), (0, h)], IRON, segs=12, coll_name=C, loc=(x, y, z))
    bx("Lantern Cap", (x, y, z + h + .62), (.42, .42, .08), IRON, bevel=.02)
    lathe("Lantern Roof", [(.26, 0), (0, .3)], IRON, segs=4, coll_name=C, loc=(x, y, z + h + .66)).rotation_euler = (0, 0, math.pi / 4)
    bx("Lantern Glass", (x, y, z + h + .32), (.3, .3, .52), GLASS, bevel=0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            bx("Lantern Frame", (x + sx * .16, y + sy * .16, z + h + .32), (.04, .04, .6), IRON, bevel=0)
    bx("Lantern Base", (x, y, z + h + .04), (.38, .38, .08), IRON, bevel=.02)
    l = bpy.data.lights.new("Lantern Light", 'POINT'); l.energy = 60; l.color = srgb("ffb45e")[:3]; l.shadow_soft_size = .15
    lo = bpy.data.objects.new("Lantern Light", l); coll("Lights").objects.link(lo); lo.location = (x, y, z + h + .32)

def banner_pole(x, y, z, h=8.5):
    lathe("Banner Pole", [(.14, 0), (.12, .4), (.09, .5), (.08, h), (0, h)], IRON, segs=12, coll_name=C, loc=(x, y, z))
    lathe("Banner Pole Base", [(.5, 0), (.5, .35), (.3, .5), (0, .5)], CARVED, segs=24, coll_name=C, loc=(x, y, z))
    lathe("Banner Pole Finial", [(0, 0), (.16, .06), (.2, .22), (.1, .4), (.06, .7), (0, .9)], GOLD, segs=16, coll_name=C, loc=(x, y, z + h))
    hang(x, y - .1, z + h - .3, 1.5, 4.2)

def stepped_parapet(xs, y0, run, rise, steps, z0, thick=1.0, wall=1.05):
    """The crenellated walls beside a flight: a block per two steps, stepping up with the stair, a cap, and a
    merlon on every block; each block in coursed stone."""
    for k in range(0, steps, 2):
        n = min(2, steps - k)
        top = z0 + rise * (k + n)
        yc = y0 + run * (k + n / 2)
        bx("Stair Parapet", (xs, yc, (top + wall) / 2 + z0 / 2 - z0 / 2), (thick, run * n + .02, top + wall), PLINTH, bevel=.02)
        bx("Stair Parapet Cap", (xs, yc, top + wall + .07), (thick + .16, run * n + .06, .14), CARVED, bevel=.03)
        bx("Stair Merlon", (xs, yc, top + wall + .55), (thick - .1, run * n * .5, .85), PLINTH, bevel=.03)
        bx("Stair Merlon Cap", (xs, yc, top + wall + 1.02), (thick + .02, run * n * .5 + .1, .1), CARVED, bevel=.02)

def big_statue(kind, x, y, z, h, mat):
    """A giant chess piece on a broad octagonal plinth with a gold band (statue() is sized for small ones)."""
    pr = .5 * h
    lathe("Statue Plinth", [(pr * 1.1, 0), (pr * 1.1, .1 * h), (pr, .13 * h), (pr, .3 * h), (pr * 1.07, .32 * h), (pr * 1.07, .36 * h), (0, .36 * h)],
          CARVED, segs=8, coll_name="Statues", loc=(x, y, z)).rotation_euler = (0, 0, math.pi / 8)
    lathe("Plinth Gold", [(pr + .02, .26 * h), (pr + .06, .26 * h), (pr + .06, .28 * h), (pr + .02, .28 * h), (pr + .02, .26 * h)],
          GOLD, segs=8, coll_name="Statues", loc=(x, y, z), caps=False).rotation_euler = (0, 0, math.pi / 8)
    top = z + .36 * h
    prof = {"king": KING, "bishop": BISHOP, "rook": ROOK}[kind]
    lathe("Statue " + kind, prof, mat, segs=96, coll_name="Statues", loc=(x, y, top), scale=(h, h, h))
    crest = top + h * prof[-1][1]
    if kind == "rook":
        for i in range(6):
            a = TAU * i / 6
            o = bx("Rook Merlon", (x + math.cos(a) * .26 * h, y + math.sin(a) * .26 * h, crest + .05 * h), (.09 * h, .09 * h, .1 * h), mat, bevel=.02, coll_name="Statues")
            o.rotation_euler = (0, 0, a)
    cz = (.58 if kind != "rook" else .72) * h
    lathe("Statue Collar", [(.24 * h + .01, cz), (.26 * h, cz), (.26 * h, cz + .02 * h), (.24 * h + .01, cz + .02 * h), (.24 * h + .01, cz)], GOLD, segs=96,
          coll_name="Statues", loc=(x, y, top), caps=False)

def crown_roof(x, y, z, R):
    """The keep's roof: a giant gold crown. A band set with big jewels in gold settings between beaded rims,
    eight tall points with balls, scallops between them, a red velvet dome under four gold arches, an orb
    and a cross."""
    H = 4.8
    lathe("Crown Band", [(R - .5, 0), (R, 0), (R + .2, .35), (R + .2, H - .35), (R, H), (R - .5, H), (R - .5, 0)], GOLD, segs=128, coll_name="Crown", loc=(x, y, z), caps=False)
    for zz, rr, n, br in ((.18, R + .22, 72, .2), (H - .18, R + .22, 72, .2), (H * .5 - 1.35, R + .2, 0, 0)):
        for i in range(n):
            a = TAU * i / n
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=br, location=(x + math.cos(a) * rr, y + math.sin(a) * rr, z + zz))
            p = bpy.context.active_object; p.data.materials.append(GOLD)
            for f in p.data.polygons: f.use_smooth = True
    gems = (JEWEL_B, JEWEL_R, principled("Amethyst", "7a3cc8", rough=.08) if "Amethyst" not in MATS else MATS["Amethyst"])
    for i in range(16):
        a = TAU * (i + .5) / 16
        cx, cy = x + math.cos(a) * (R + .2), y + math.sin(a) * (R + .2)
        ring = lathe("Jewel Setting", [(.62, -.12), (.82, -.12), (.82, .12), (.62, .12), (.62, -.12)], GOLD, segs=32, coll_name="Crown",
                     loc=(cx, cy, z + H * .5), caps=False)
        ring.rotation_euler = (math.pi / 2, 0, a + math.pi / 2)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=.62, location=(cx + math.cos(a) * .12, cy + math.sin(a) * .12, z + H * .5))
        g = bpy.context.active_object; g.data.materials.append(gems[i % 3]); g.scale = (1, 1, 1.15)
        for f in g.data.polygons: f.use_smooth = True
        for k in range(4):   # four little claws holding the stone
            ca = k * math.pi / 2 + math.pi / 4
            off = Vector((-math.sin(a) * math.cos(ca), math.cos(a) * math.cos(ca), math.sin(ca))) * .7
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=.12, location=(cx + off.x + math.cos(a) * .25, cy + off.y + math.sin(a) * .25, z + H * .5 + off.z))
            c_ = bpy.context.active_object; c_.data.materials.append(GOLD)
    # the points and the scallops between them
    tips = []
    for i in range(8):
        a = TAU * i / 8
        px, py = x + math.cos(a) * (R - .1), y + math.sin(a) * (R - .1)
        t = lathe("Crown Point", [(1.6, 0), (1.4, .6), (.95, 2.4), (.55, 4.4), (.32, 5.3), (0, 5.5)], GOLD, segs=32, coll_name="Crown", loc=(px, py, z + H - .1))
        t.rotation_euler = (math.cos(a + math.pi / 2) * -.12, math.sin(a + math.pi / 2) * -.12, 0)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=.95, location=(px + math.cos(a) * .55, py + math.sin(a) * .55, z + H + 6.0))
        p = bpy.context.active_object; p.data.materials.append(GOLD)
        for f in p.data.polygons: f.use_smooth = True
        tips.append(a)
    for i in range(8):
        a0, a1 = TAU * i / 8, TAU * (i + 1) / 8
        pts = []
        for k in range(13):
            t = k / 12; a = a0 + (a1 - a0) * t
            pts.append(Vector((x + math.cos(a) * (R + .05), y + math.sin(a) * (R + .05), z + H + .2 + math.sin(t * math.pi) * 1.6)))
        for p0, p1 in zip(pts, pts[1:]): beam_between("Crown Scallop", p0, p1, (.24, .24), GOLD, "Crown")
        mid = TAU * (i + .5) / 8
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=.32, location=(x + math.cos(mid) * (R + .05), y + math.sin(mid) * (R + .05), z + H + 2.1))
        q = bpy.context.active_object; q.data.materials.append(GOLD)
    # the velvet dome inside, gold arches over it, orb and cross
    lathe("Crown Velvet", [(R - .5, .4), (R - .6, 2.5), (R * .8, 5.6), (R * .5, 7.8), (R * .2, 8.8), (0, 9.0)], CUSHION, segs=96, coll_name="Crown", loc=(x, y, z))
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        pts = []
        for k in range(21):
            t = k / 20; ang = t * math.pi / 2
            rr = (R - .3) * math.cos(ang); zz = 5.0 + 4.6 * math.sin(ang)
            pts.append(Vector((x + math.cos(a) * rr, y + math.sin(a) * rr, z + zz)))
        for p0, p1 in zip(pts, pts[1:]): beam_between("Crown Arch", p0, p1, (.45, .45), GOLD, "Crown")
    lathe("Crown Orb", [(0, 0), (.9, .1), (1.25, .9), (.9, 1.7), (0, 1.85)], GOLD, segs=48, coll_name="Crown", loc=(x, y, z + 9.4))
    bx("Crown Cross", (x, y, z + 12.2), (.5, .5, 2.4), GOLD, bevel=.05, coll_name="Crown")
    bx("Crown Cross", (x, y, z + 12.5), (1.8, .5, .5), GOLD, bevel=.05, coll_name="Crown")

# ================= the ground: a plaza, a hill rising to the castle =================
W1, S1, W2, S2, RISE, RUN = 14.0, 16, 12.0, 16, .3, 1.0
L1Z = S1 * RISE; L1Y0 = S1 * RUN; L1Y1 = L1Y0 + 7.0
LZ = L1Z + S2 * RISE; LY0 = L1Y1 + S2 * RUN; LY1 = LY0 + 8.0
PZ = LZ                      # the castle's ground
def stair_top(y):
    if y < 0: return 0.0
    if y < L1Y0: return RISE * min(S1, int(y / RUN) + 1)
    if y < L1Y1: return L1Z
    if y < LY0: return L1Z + RISE * min(S2, int((y - L1Y1) / RUN) + 1)
    return LZ
def ground(x, y):
    t = smooth01(-4, LY0 + 2, y)
    base = t * PZ
    ax = abs(x)
    bank = smooth01(9, 26, ax) * (2.5 + 2.0 * mnoise.noise(Vector((x * .05, y * .05, 1.7)))) * (1 - smooth01(LY0, LY1 + 6, y))
    h = base + bank + mnoise.fractal(Vector((x * .06, y * .06, .3)), .6, 2.0, 5) * 1.6 * smooth01(4, 18, ax)
    if ax < W1 / 2 + 2.6 and y < LY1: h = min(h, stair_top(y) - .35)
    if y < -1: h = min(h, -.2)
    if y > LY1 - 1: h = max(h, PZ - .4) if ax < 60 else h
    return h
# ---------- v4 (승규 님: "부서진 돌 계단으로 … 바위 비탈도 더 갈라지고 부서진 돌 바닥으로"): the steps are laid
# from separate worn blocks (some chipped, some split, a few gone, rubble beside them), no carpet; the plaza,
# the landings and the hillsides are paved with irregular slabs, many cracked into pieces, some tilted or
# missing, dark earth and rubble in the gaps.
from mathutils import Matrix

def worn_stone(name, c1, c2, c3, crack_scale=1.6, rough=.82):
    """Weathered stone for many pieces in one mesh: each piece (island) its own shade, mottled, hairline cracks,
    darker in the crevices (ambient occlusion), a gritty bump."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; L = nt.links.new
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'); b.inputs['Roughness'].default_value = rough
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    isl = nt.nodes.new('ShaderNodeObjectInfo')
    rnd = nt.nodes.new('ShaderNodeValToRGB'); e = rnd.color_ramp.elements
    e[0].position, e[0].color = 0, srgb(c3); e[1].position, e[1].color = 1, srgb(c1)
    em = e.new(.5); em.color = srgb(c2)
    try: L(geo.outputs['Random Per Island'], rnd.inputs['Fac'])
    except Exception: L(isl.outputs['Random'], rnd.inputs['Fac'])
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 1.8; nz.inputs['Detail'].default_value = 10; L(tc.outputs['Object'], nz.inputs['Vector'])
    mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.blend_type = 'MULTIPLY'; mx.inputs[0].default_value = .35
    L(rnd.outputs['Color'], mx.inputs[6]); L(nz.outputs['Color'], mx.inputs[7])
    # hairline cracks: thin cell edges of a distorted voronoi
    dn = nt.nodes.new('ShaderNodeTexNoise'); dn.inputs['Scale'].default_value = 3; dn.inputs['Detail'].default_value = 4; L(tc.outputs['Object'], dn.inputs['Vector'])
    dm = nt.nodes.new('ShaderNodeMix'); dm.data_type = 'VECTOR'; dm.inputs[0].default_value = .18
    L(tc.outputs['Object'], dm.inputs[4]); L(dn.outputs['Color'], dm.inputs[5])
    vo = nt.nodes.new('ShaderNodeTexVoronoi'); vo.feature = 'DISTANCE_TO_EDGE'; vo.inputs['Scale'].default_value = crack_scale
    L(dm.outputs[1], vo.inputs['Vector'])
    cr0 = nt.nodes.new('ShaderNodeMapRange'); L(vo.outputs['Distance'], cr0.inputs['Value'])
    cr0.inputs['From Min'].default_value = .0; cr0.inputs['From Max'].default_value = .018; cr0.inputs['To Min'].default_value = .5; cr0.inputs['To Max'].default_value = 1.0
    # cracks only in patches: a broad noise fades them out over most of the stone
    cm_ = nt.nodes.new('ShaderNodeTexNoise'); cm_.inputs['Scale'].default_value = .9; cm_.inputs['Detail'].default_value = 3; L(tc.outputs['Object'], cm_.inputs['Vector'])
    cmr = nt.nodes.new('ShaderNodeMapRange'); L(cm_.outputs['Fac'], cmr.inputs['Value']); cmr.inputs['From Min'].default_value = .52; cmr.inputs['From Max'].default_value = .62
    cr = nt.nodes.new('ShaderNodeMix'); cr.data_type = 'FLOAT'; L(cmr.outputs['Result'], cr.inputs[0]); cr.inputs[2].default_value = 1.0; L(cr0.outputs['Result'], cr.inputs[3])
    ao = nt.nodes.new('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = .35; ao.samples = 8
    aor = nt.nodes.new('ShaderNodeMapRange'); L(ao.outputs['AO'], aor.inputs['Value']); aor.inputs['To Min'].default_value = .45; aor.inputs['To Max'].default_value = 1.0
    k = nt.nodes.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; L(cr.outputs['Result'], k.inputs[0]); L(aor.outputs['Result'], k.inputs[1])
    fin = nt.nodes.new('ShaderNodeMix'); fin.data_type = 'RGBA'; fin.blend_type = 'MULTIPLY'; fin.inputs[0].default_value = 1
    L(mx.outputs[2], fin.inputs[6]); L(k.outputs[0], fin.inputs[7]); L(fin.outputs[2], b.inputs['Base Color'])
    gr = nt.nodes.new('ShaderNodeTexNoise'); gr.inputs['Scale'].default_value = 28; gr.inputs['Detail'].default_value = 8; L(tc.outputs['Object'], gr.inputs['Vector'])
    hh = nt.nodes.new('ShaderNodeMath'); hh.operation = 'MULTIPLY_ADD'; L(gr.outputs['Fac'], hh.inputs[0]); hh.inputs[1].default_value = .35; L(cr.outputs['Result'], hh.inputs[2])
    bp = nt.nodes.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = .7; bp.inputs['Distance'].default_value = .03
    L(hh.outputs[0], bp.inputs['Height']); L(bp.outputs['Normal'], b.inputs['Normal'])
    MATS[name] = m; return m

WORN = worn_stone("Worn Stone", "eddcbd", "d9c6a2", "bba98a", crack_scale=.8)
WORN_DARK = worn_stone("Worn Paving", "dccaa8", "c0ad8c", "978873", crack_scale=.6)
GROUT = principled("Grout Earth", "4a3f34", rough=.95)
EARTH = bpy.data.materials.new("Earth"); EARTH.use_nodes = True
_nt = EARTH.node_tree; _L = _nt.links.new
_b = next(n for n in _nt.nodes if n.type == 'BSDF_PRINCIPLED'); _b.inputs['Roughness'].default_value = .97
_tc = _nt.nodes.new('ShaderNodeTexCoord')
_n = _nt.nodes.new('ShaderNodeTexNoise'); _n.inputs['Scale'].default_value = 9; _n.inputs['Detail'].default_value = 12; _L(_tc.outputs['Object'], _n.inputs['Vector'])
_r = _nt.nodes.new('ShaderNodeValToRGB'); _e = _r.color_ramp.elements
_e[0].position, _e[0].color = .35, srgb("3d342b"); _e[1].position, _e[1].color = .7, srgb("7a6d5c")
_L(_n.outputs['Fac'], _r.inputs['Fac']); _L(_r.outputs['Color'], _b.inputs['Base Color'])
_v = _nt.nodes.new('ShaderNodeTexVoronoi'); _v.inputs['Scale'].default_value = 22; _L(_tc.outputs['Object'], _v.inputs['Vector'])
_bp = _nt.nodes.new('ShaderNodeBump'); _bp.inputs['Strength'].default_value = .8; _bp.inputs['Distance'].default_value = .05
_L(_v.outputs['Distance'], _bp.inputs['Height']); _L(_bp.outputs['Normal'], _b.inputs['Normal'])

def split_poly(poly, rng):
    """Break a convex polygon in two along a line between points on two different edges."""
    n = len(poly)
    ia = rng.randrange(n); ib = (ia + rng.randint(1, n - 1)) % n
    if ia == ib: return [poly]
    def on(i, t): (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % n]; return (x0 + (x1 - x0) * t, y0 + (y1 - y0) * t)
    pa, pb = on(ia, rng.uniform(.25, .75)), on(ib, rng.uniform(.25, .75))
    a, b_ = [pa], [pb]
    i = (ia + 1) % n
    while True:
        a.append(poly[i])
        if i == ib: break
        i = (i + 1) % n
    a.append(pb)
    i = (ib + 1) % n
    while True:
        b_.append(poly[i])
        if i == ia: break
        i = (i + 1) % n
    b_.append(pa)
    return [a, b_]

RUBBLE_BM = bmesh.new()
def rubble(x, y, z, r, rng):
    res = bmesh.ops.create_icosphere(RUBBLE_BM, subdivisions=1, radius=r, matrix=Matrix.Translation((x, y, z)))
    for v in res['verts']:
        d = v.co - Vector((x, y, z))
        v.co = Vector((x, y, z)) + Vector((d.x * rng.uniform(.7, 1.3), d.y * rng.uniform(.7, 1.3), d.z * rng.uniform(.45, .9)))

def slab_field(name, x0, x1, y0, y1, cell, zfunc, mat, seed, thick=.24, gap=(.03, .08), broken=.4, missing=.05, tilt=.035, sink=.08, mask=None):
    rng = random.Random(seed)
    nx, ny = max(1, int((x1 - x0) / cell)), max(1, int((y1 - y0) / cell))
    cx_, cy_ = (x1 - x0) / nx, (y1 - y0) / ny
    P = [[(x0 + i * cx_ + (rng.uniform(-.3, .3) * cx_ if 0 < i < nx else 0), y0 + j * cy_ + (rng.uniform(-.3, .3) * cy_ if 0 < j < ny else 0))
          for i in range(nx + 1)] for j in range(ny + 1)]
    bm = bmesh.new()
    for j in range(ny):
        for i in range(nx):
            quad = [P[j][i], P[j][i + 1], P[j + 1][i + 1], P[j + 1][i]]
            qx = sum(p[0] for p in quad) / 4; qy = sum(p[1] for p in quad) / 4
            if mask and not mask(qx, qy): continue
            if rng.random() < missing:
                for _ in range(rng.randint(1, 3)):
                    rx, ry = qx + rng.uniform(-.4, .4) * cx_, qy + rng.uniform(-.4, .4) * cy_
                    rubble(rx, ry, zfunc(rx, ry) + .02, rng.uniform(.08, .22), rng)
                continue
            pieces = [quad]
            if rng.random() < broken:
                pieces = split_poly(quad, rng)
                if rng.random() < .4: pieces = split_poly(pieces[0], rng) + pieces[1:]
            for poly in pieces:
                px = sum(p[0] for p in poly) / len(poly); py = sum(p[1] for p in poly) / len(poly)
                g = rng.uniform(*gap)
                pts = []
                for (x, y) in poly:
                    dx, dy = x - px, y - py; dl = math.hypot(dx, dy) or 1
                    k = max(0.0, (dl - g) / dl)
                    pts.append((px + dx * k, py + dy * k))
                dz = rng.uniform(-.035, .05); tx, ty = rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt)
                top = [bm.verts.new((x, y, zfunc(x, y) + sink + dz + tx * (x - px) + ty * (y - py))) for (x, y) in pts]
                bot = [bm.verts.new((v.co.x, v.co.y, v.co.z - thick)) for v in top]
                try:
                    bm.faces.new(top); bm.faces.new(list(reversed(bot)))
                    for k_ in range(len(top)):
                        bm.faces.new((top[k_], bot[k_], bot[(k_ + 1) % len(top)], top[(k_ + 1) % len(top)]))
                except ValueError:
                    pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.bevel(bm, geom=[e for e in bm.edges], offset=.018, segments=1, affect='EDGES', clamp_overlap=True)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    return obj(name, me, mat, "Ground")

def broken_flight(y0, z0, width, steps, rise, run, seed):
    """A flight of worn block steps: each row of blocks of random widths with fine joints; some front corners
    chipped, some blocks split in two, a few gone (the dark core shows), rubble at the edges."""
    rng = random.Random(seed)
    bm = bmesh.new()
    for i in range(steps):
        top = z0 + rise * (i + 1); ya = y0 + run * i; yb = ya + run
        bx("Stair Core", (0, ya + run / 2, (z0 + top - rise) / 2 + .0), (width, run, max(.02, top - rise - z0 + .02)), GROUT, bevel=0, coll_name="Ground") if i > 0 else None
        x = -width / 2
        while x < width / 2 - .05:
            w = rng.uniform(.7, 2.1)
            if width / 2 - (x + w) < .45: w = width / 2 - x
            x0b, x1b = x + .02, x + w - .02
            x += w
            if rng.random() < .07:
                for _ in range(rng.randint(2, 5)):
                    rubble(rng.uniform(x0b, x1b), rng.uniform(ya, yb), top - rise + .08, rng.uniform(.07, .18), rng)
                bx("Stair Hole Core", ((x0b + x1b) / 2, ya + run / 2, top - rise - .02), (x1b - x0b, run, .06), GROUT, bevel=0, coll_name="Ground")
                continue
            parts = [(x0b, x1b)]
            if rng.random() < .3 and x1b - x0b > .9:
                cut = rng.uniform(x0b + .35, x1b - .35); parts = [(x0b, cut - .025), (cut + .025, x1b)]
            for (a, b_) in parts:
                dz = rng.uniform(-.04, .015)
                zt, zb = top + dz, top - rise - .03
                ov = rng.uniform(.0, .07)
                v = [bm.verts.new(c) for c in ((a, ya - ov, zb), (b_, ya - ov, zb), (b_, yb, zb), (a, yb, zb),
                                               (a, ya - ov, zt), (b_, ya - ov, zt), (b_, yb, zt), (a, yb, zt))]
                # a chipped front corner, now and then a worn-down front edge
                if rng.random() < .55:
                    c = rng.choice((4, 5)); v[c].co.z -= rng.uniform(.08, .22); v[c].co.y += rng.uniform(.05, .18)
                    v[c].co.x += (.12 if c == 4 else -.12) * rng.random()
                if rng.random() < .4:
                    for c in (4, 5): v[c].co.z -= rng.uniform(.02, .06)
                if rng.random() < .3:   # sunk at one end
                    c2 = rng.choice(((4, 7), (5, 6)))
                    dd = rng.uniform(.03, .08)
                    for c in c2: v[c].co.z -= dd
                if rng.random() < .25:  # shoved a little out of line
                    dy = rng.uniform(-.06, .06)
                    for vv in v: vv.co.y += dy
                for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
                    bm.faces.new([v[k] for k in f])
                if rng.random() < .25:
                    rubble(rng.uniform(a, b_), ya - .25, top - rise + .06, rng.uniform(.06, .16), rng)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.bevel(bm, geom=[e for e in bm.edges], offset=.012, segments=1, affect='EDGES', clamp_overlap=True)
    me = bpy.data.meshes.new("Worn Steps"); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    return obj("Worn Steps", me, WORN, "Ground")

# ================= the ground =================
bm = bmesh.new(); NX, NY = 150, 120; X0, X1, Y0g, Y1g = -80.0, 80.0, -2.0, 95.0
grid = [[bm.verts.new((X0 + (X1 - X0) * i / NX, Y0g + (Y1g - Y0g) * j / NY, 0)) for i in range(NX + 1)] for j in range(NY + 1)]
for row in grid:
    for v in row: v.co.z = ground(v.co.x, v.co.y)
for j in range(NY):
    for i in range(NX):
        bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
me = bpy.data.meshes.new("Hill"); bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
obj("Hill", me, EARTH, "Terrain")
# the hillsides paved in broken slabs (clear of the stair and its walls)
slab_field("Hillside Paving", -60, 60, -.3, LY1 + 1.5, 1.7, ground, WORN_DARK, 21, thick=.32, broken=.5, missing=.08, tilt=.05, sink=.06,
           mask=lambda x, y: abs(x) > W1 / 2 + 2.0 and not (abs(x) < W1 / 2 + 6.6 and L1Y0 - .6 < y < L1Y1 + .6) and not (abs(x) < 28.6 and y > LY0 - .6))
# the plaza at the foot, the middle landing and the gate terrace in broken paving too
slab_field("Plaza Paving", -42, 42, -26, -.4, 2.0, lambda x, y: -.08, WORN_DARK, 22, broken=.35, missing=.03, tilt=.025, sink=.0)
bx("Plaza Kerb", (0, -.1, .1), (68, .5, .2), CARVED, bevel=.04)
broken_flight(0, 0, W1, S1, RISE, RUN, 31)
bx("Mid Landing", (0, (L1Y0 + L1Y1) / 2, (L1Z - .16) / 2), (W1 + 12, L1Y1 - L1Y0, L1Z - .16), PLINTH, bevel=.05)
slab_field("Landing Paving", -(W1 / 2 + 6), W1 / 2 + 6, L1Y0 + .02, L1Y1, 1.5, lambda x, y: L1Z - .2, WORN, 23, broken=.4, missing=.03, tilt=.02, sink=.0, thick=.22)
broken_flight(L1Y1, L1Z, W2, S2, RISE, RUN, 32)
bx("Gate Terrace", (0, (LY0 + LY1) / 2, (LZ - .16) / 2), (56, LY1 - LY0, LZ - .16), PLINTH, bevel=.05)
slab_field("Terrace Paving", -28, 28, LY0 + .02, LY1 + 2.5, 1.6, lambda x, y: LZ - .2, WORN, 24, broken=.4, missing=.03, tilt=.02, sink=.0, thick=.22)
# boulders: a few big outcrops on the outer slopes
for i in range(14):
    side = -1 if i % 2 else 1
    x = side * RNG.uniform(16, 40); y = RNG.uniform(2, LY1)
    r = RNG.uniform(1.2, 3.0)
    boulder(x, y, ground(x, y) + r * .1, r, RNG.uniform(0, 50))
# rubble along the stair walls and scattered on the slopes
_rr = random.Random(77)
for i in range(260):
    side = -1 if i % 2 else 1
    if i < 80:
        y = _rr.uniform(0, LY0); x = side * _rr.uniform(W1 / 2 + 1.1, W1 / 2 + 2.4)
    else:
        y = _rr.uniform(-1, LY1); x = side * _rr.uniform(W1 / 2 + 2.4, 50)
    rubble(x, y, ground(x, y) + .1, _rr.uniform(.06, .3), _rr)
me = bpy.data.meshes.new("Rubble"); RUBBLE_BM.to_mesh(me); RUBBLE_BM.free()
for p in me.polygons: p.use_smooth = True
obj("Rubble", me, WORN_DARK, "Ground")

for s in (-1, 1):
    # crenellated stepped walls up both flights, lanterns on them
    stepped_parapet(s * (W1 / 2 + .5), 0, RUN, RISE, S1, 0)
    stepped_parapet(s * (W2 / 2 + .5), L1Y1, RUN, RISE, S2, L1Z)
    for k in (3, 9, 15):
        lantern_post(s * (W1 / 2 + 1.8), RUN * k, ground(s * (W1 / 2 + 1.8), RUN * k), h=3.0)
    for k in (5, 11):
        lantern_post(s * (W2 / 2 + 1.8), L1Y1 + RUN * k, ground(s * (W2 / 2 + 1.8), L1Y1 + RUN * k), h=3.0)
    # the landing's parapet round its outer edges
    bx("Landing Parapet", (s * (W1 / 2 + 5.6), (L1Y0 + L1Y1) / 2, L1Z + .55), (.9, L1Y1 - L1Y0, 1.1), PLINTH, bevel=.02)
    merlons((s * (W1 / 2 + 5.6), L1Y0 + .4, 0), (s * (W1 / 2 + 5.6), L1Y1 - .4, 0), L1Z + 1.1, PLINTH, w=.9, gap=.6, h=.8, depth=.8)
    # gate piers at the foot: coursed stone, a cap, a king finial, a lantern
    bx("Stair Pier", (s * (W1 / 2 + 1.2), -.6, 2.4), (2.0, 2.0, 4.8), PLINTH, bevel=.04)
    bx("Stair Pier Cap", (s * (W1 / 2 + 1.2), -.6, 4.88), (2.4, 2.4, .2), CARVED, bevel=.04)
    bx("Stair Pier Band", (s * (W1 / 2 + 1.2), -.6, 4.2), (2.06, 2.06, .12), GOLD, bevel=0)
    tripo("King Newel", "KingPost", (s * (W1 / 2 + 1.2), -.6, 4.98), 0, 2.4)
    # turrets either side of the middle landing, braziers, banner poles up the upper flight
    lifted(L1Z - .2, round_tower, "Landing Turret", s * (W1 / 2 + 7.6), (L1Y0 + L1Y1) / 2, 2.8, 11.0, roof=False, phase=s * .9)
    tripo("Brazier", "Brazier", (s * (W1 / 2 + 3.2), L1Y0 + 1.6, L1Z), 0, 2.0)
    # banner poles framing the flights: outside the foot piers and at the landing's front corners
    banner_pole(s * (W1 / 2 + 3.6), -1.4, 0)
    banner_pole(s * (W1 / 2 + 4.9), L1Y0 + .9, L1Z)
    # the teams' giant pieces at the foot: an ivory rook on the left, an ebony bishop on the right
    big_statue("rook" if s < 0 else "bishop", s * 17.5, 8.0, ground(s * 17.5, 8.0) - .2, 4.6 if s < 0 else 5.4, PIECE_W if s < 0 else PIECE_B)

# ================= the gatehouse: a great gate =================
GY = LY1 - .5
GW, GH = 22.0, 22.0
GT = LZ + GH + 3.0      # the gatehouse alone 3 m taller (승규 님: its battlements up where the rose window was)
gh = bx("Gatehouse", (0, GY + 3.5, GT / 2), (GW, 7, GT), WALL, bevel=0)
OW, SPRING = 10.0, 6.5
R = OW / 2
cut1 = bx("Cut", (0, GY + 3.5, LZ + SPRING / 2), (OW, 7.4, SPRING + .02), PIECE_B, bevel=0)
cut2 = lathe("Cut Head", [(R, -3.7), (R, 3.7)], PIECE_B, segs=96, coll_name=C)
cut2.location = (0, GY + 3.5, LZ + SPRING); cut2.rotation_euler = (math.pi / 2, 0, 0)
for c in (cut1, cut2):
    m = gh.modifiers.new("Door Cut", 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = c; m.solver = 'EXACT'
    bpy.context.view_layer.objects.active = gh
    with bpy.context.temp_override(object=gh, active_object=gh): bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(c, do_unlink=True)
voussoirs(0, GY - .02, LZ + SPRING, R, R + 1.5, 1.0, 21, CARVED)
half_arch("Gate Arch Gold", 0, GY - 1.03, LZ + SPRING, R + .03, R + .2, .04, GOLD)
half_arch("Gate Arch Outer", 0, GY - .02, LZ + SPRING, R + 1.5, R + 1.75, .5, CARVED_DARK)
for s in (-1, 1):
    quoins(s * (R + .5), GY, LZ, LZ + SPRING, 1.0, CARVED, w_long=1.5, w_short=1.0, course=.75, side=s)
    bx("Jamb Base", (s * (R + .7), GY - .6, LZ + .45), (1.8, 1.2, .9), CARVED, bevel=.05)
door = bx("Door", (0, GY + 1.2, LZ + (SPRING + R) / 2), (OW, .35, SPRING + R), DOOR_WOOD, bevel=.02)
dc = lathe("Door Head", [(R + .01, -1), (R + .01, 1)], PIECE_B, segs=96, coll_name=C); dc.location = (0, GY + 1.2, LZ + SPRING); dc.rotation_euler = (math.pi / 2, 0, 0)
shape = bx("Door Shape", (0, GY + 1.2, LZ + SPRING / 2), (OW, 1, SPRING), PIECE_B, bevel=0)
un = shape.modifiers.new("U", 'BOOLEAN'); un.operation = 'UNION'; un.object = dc; un.solver = 'EXACT'
bpy.context.view_layer.objects.active = shape
with bpy.context.temp_override(object=shape, active_object=shape): bpy.ops.object.modifier_apply(modifier=un.name)
bpy.data.objects.remove(dc, do_unlink=True)
m = door.modifiers.new("Shape", 'BOOLEAN'); m.operation = 'INTERSECT'; m.object = shape; m.solver = 'EXACT'
bpy.context.view_layer.objects.active = door
with bpy.context.temp_override(object=door, active_object=door): bpy.ops.object.modifier_apply(modifier=m.name)
bpy.data.objects.remove(shape, do_unlink=True)
for i in range(-3, 4):
    bx("Door Seam", (i * 1.43, GY + 1.0, LZ + SPRING / 2), (.05, .04, SPRING), PIECE_B, bevel=0)
bx("Door Seam", (0, GY + .98, LZ + (SPRING + R) / 2), (.08, .05, SPRING + R), IRON, bevel=0)
for zz in (1.2, 3.4, 5.6):
    bx("Door Strap", (0, GY + .99, LZ + zz), (OW - .3, .05, .2), IRON, bevel=.01)
    for i in range(-8, 9):
        lathe("Door Stud", [(0, 0), (.08, .02), (.06, .08), (0, .1)], GOLD, segs=10, coll_name=C,
              loc=(i * .58, GY + .96, LZ + zz)).rotation_euler = (math.radians(-90), 0, 0)
for s in (-1, 1):
    r_ = lathe("Door Ring", [(.32, -.04), (.4, -.04), (.4, .04), (.32, .04), (.32, -.04)], GOLD, segs=24, coll_name=C, loc=(s * .8, GY + .9, LZ + 2.6), caps=False)
    r_.rotation_euler = (math.pi / 2, 0, 0)
for i in range(-11, 12):
    x = i * .45
    top = LZ + SPRING + math.sqrt(max(0, R * R - x * x)) - .05
    bot = LZ + SPRING + 1.6
    if top - bot > .2:
        bx("Portcullis Bar", (x, GY + .5, (top + bot) / 2), (.1, .1, top - bot), IRON, bevel=.01)
        lathe("Portcullis Spike", [(.08, 0), (0, -.36)], IRON, segs=8, coll_name=C, loc=(x, GY + .5, bot))
for zz in (1.9, 2.8, 3.7, 4.5):
    w = 2 * math.sqrt(max(0, R * R - zz * zz))
    if w > .5: bx("Portcullis Rail", (0, GY + .42, LZ + SPRING + zz), (w - .1, .08, .1), IRON, bevel=.01)
crown_rules = [
    (lambda nt, tc, x, y, z: cmp(nt, dist3(nt, tc, 0, 0, .3), 'LESS_THAN', .36), "9e1420", .65, 0.0),
    (lambda nt, tc, x, y, z: cmp(nt, z, 'LESS_THAN', .2), "d9ab4a", .25, 1.0),
    (lambda nt, tc, x, y, z: cmp(nt, z, 'GREATER_THAN', .74), "d9ab4a", .25, 1.0),
]
cm = zone_material("Royal Crown", crown_rules, "d9ab4a", .25, 1.0)
KEY_TOP = LZ + SPRING + R + 1.5 + .45
for o in import_glb("royal crown 3d model.glb", C):
    o.data.materials.clear(); o.data.materials.append(cm)
    o.scale = (2.6, 2.6, 2.6); o.location = (0, GY - .7, KEY_TOP - .1)
sm = bpy.data.materials.new("Heraldic Shield"); sm.use_nodes = True; nt = sm.node_tree; L = nt.links.new
b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
tc = nt.nodes.new('ShaderNodeTexCoord'); sp = nt.nodes.new('ShaderNodeSeparateXYZ'); L(tc.outputs['Object'], sp.inputs[0])
face = mul(nt, mul(nt, cmp(nt, absn(nt, sp.outputs[0]), 'LESS_THAN', .235), cmp(nt, sp.outputs[2], 'LESS_THAN', .63)), cmp(nt, sp.outputs[2], 'GREATER_THAN', .2))
ck = nt.nodes.new('ShaderNodeTexChecker'); ck.inputs['Scale'].default_value = 1
mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (9, 9, 9); mp.inputs['Rotation'].default_value = (math.radians(90), 0, 0)
L(tc.outputs['Object'], mp.inputs['Vector']); L(mp.outputs['Vector'], ck.inputs['Vector'])
ck.inputs['Color1'].default_value = srgb("efe6d2"); ck.inputs['Color2'].default_value = srgb("1e1a18")
mxs = nt.nodes.new('ShaderNodeMix'); mxs.data_type = 'RGBA'; L(face, mxs.inputs[0]); mxs.inputs[6].default_value = srgb("d9ab4a"); L(ck.outputs['Color'], mxs.inputs[7])
L(mxs.outputs[2], b.inputs['Base Color'])
inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1; L(face, inv.inputs[1]); L(inv.outputs[0], b.inputs['Metallic'])
rr = nt.nodes.new('ShaderNodeMapRange'); L(face, rr.inputs['Value']); rr.inputs['To Min'].default_value = .25; rr.inputs['To Max'].default_value = .3; L(rr.outputs['Result'], b.inputs['Roughness'])
for s in (-1, 1):
    for o in import_glb("heraldic shield 3d model.glb", C):
        o.data.materials.clear(); o.data.materials.append(sm)
        o.scale = (3.0, 3.0, 3.0); o.location = (s * (R + 3.4), GY - .5, LZ + SPRING + .6)
    tripo("Wall Lantern", "WallLantern", (s * (R + 2.3), GY - .02, LZ + 3.6), -math.pi / 2, 1.8)
    l = bpy.data.lights.new("Gate Lantern", 'POINT'); l.energy = 220; l.color = srgb("ffb45e")[:3]; l.shadow_soft_size = .2
    lo = bpy.data.objects.new("Gate Lantern", l); coll("Lights").objects.link(lo); lo.location = (s * (R + 2.3), GY - .7, LZ + 3.6)
BZ = KEY_TOP + 2.0
bx("Balcony", (0, GY - .9, BZ), (8.4, 1.8, .32), CARVED, bevel=.04)
bx("Balcony Gilt", (0, GY - 1.8, BZ - .06), (8.4, .03, .06), GOLD, bevel=0)
for i in range(8):
    x = -3.85 + i * 1.1
    for dz, d, w, h in ((-.32, 1.6, .36, .32), (-.64, 1.15, .3, .32), (-.94, .7, .24, .28)):
        bx("Balcony Corbel", (x, GY - d / 2, BZ + dz), (w, d, h), CARVED, bevel=.025)
balustrade((-4.0, GY - 1.6, 0), (4.0, GY - 1.6, 0), BZ + .16, BZ + .16, spacing=.36, coll_name=C)
arched_window(0, GY - .01, BZ + .55, 2.2, 4.2)
for s in (-1, 1):
    arched_window(s * 5.6, GY - .01, BZ + .55, 1.4, 3.2)
bx("Gate Course", (0, GY - .12, LZ + SPRING + R + 1.2), (GW, .3, .24), CARVED, bevel=.03)
bx("Gate Course", (0, GY - .12, GT - 2.8), (GW, .3, .24), CARVED, bevel=.03)
corbel_line(-GW / 2, GW / 2, GY, GT - .55)
bx("Gate Parapet", (0, GY - .25, GT + .4), (GW, .7, 1.0), WALL, bevel=.02)
bx("Gate Parapet Cap", (0, GY - .3, GT + .95), (GW + .1, .8, .12), CARVED, bevel=.02)
merlons((-GW / 2, GY - .25, 0), (GW / 2, GY - .25, 0), GT + 1.0, WALL, w=1.1, gap=.7, h=1.1, depth=.7)
for s in (-1, 1):
    hang(s * 8.3, GY - .1, GT - 3.4, 2.2, 7.5)
# the knights on the terrace either side of the gate
KNIGHT_SRC = os.path.join(DL, "3d white marble chess knight statue.glb")
for k, (piece_hex, vein_hex) in ((-1, ("efe6d4", "b9a487")), (1, ("1e1a18", "5c4e40"))):
    before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=KNIGHT_SRC)
    for o in [o for o in bpy.data.objects if o not in before]:
        for c in o.users_collection: c.objects.unlink(o)
        coll("Statues").objects.link(o)
        if o.type != 'MESH': continue
        o.data.materials.clear(); o.data.materials.append(knight_material("Knight " + piece_hex, piece_hex, vein_hex))
        for p in o.data.polygons: p.use_smooth = True
        o.name = "Knight Statue"; o.location = (k * 11.0, LY0 + 2.6, LZ); o.scale = (5.6, 5.6, 5.6)
        o.rotation_euler = (0, 0, math.radians(180 - 25 * k))

# ================= towers and walls (on the castle ground) =================
for s in (-1, 1):
    lifted(PZ, round_tower, "Gate Tower", s * (GW / 2 + 3.4), GY + 2.2, 4.3, GH + 4, roof=False, phase=s * 1.3)
    lifted(PZ, curtain, min(s * (GW / 2 + 7.4), s * 30.5), max(s * (GW / 2 + 7.4), s * 30.5), GY + 5, GH - 2, windows=1, banners=1)
    lifted(PZ, round_tower, "Wall Tower", s * 34.0, GY + 8, 4.6, GH + 2, roof=False, phase=s * 2.1)
    lifted(PZ, curtain, min(s * 38.4, s * 46.0), max(s * 38.4, s * 46.0), GY + 11, GH - 4, windows=0, slits=1, banners=1)
    lifted(PZ, round_tower, "Corner Tower", s * 49.5, GY + 14, 4.0, GH, roof=False, phase=s * .7)

# ================= the keep, crowned =================
KY = GY + 29
KW, KD, KH = 30.0, 28.0, 38.0
KT = PZ + KH
KF = KY - KD / 2           # its front face
bx("Keep", (0, KY, KT / 2), (KW, KD, KT), WALL, bevel=0)
for zz in (KT - 14.5, KT - 2.8):
    bx("Keep Course", (0, KF - .1, zz), (KW, .3, .24), CARVED, bevel=.03)
for s in (-1, 1):
    quoins(s * (KW / 2 - .1), KF, KT - 22, KT, .6, CARVED, w_long=1.6, w_short=1.0, course=.8, side=-s)
for k in range(5):
    arched_window((k - 2) * 4.4, KF, KT - 13.6, 2.0, 4.0)
for s in (-1, 1):
    hang(s * 11.0, KF - .1, KT - 3.6, 2.0, 8.0)
# (no rose window: 승규 님 asked for it gone, the gatehouse's battlements now rise into its place)
corbel_line(-KW / 2, KW / 2, KF, KT - .55)
bx("Keep Parapet", (0, KF - .25, KT + .4), (KW, .7, 1.0), WALL, bevel=.02)
merlons((-KW / 2, KF - .25, 0), (KW / 2, KF - .25, 0), KT + 1.0, WALL, w=1.2, gap=.8, h=1.2, depth=.7)
for s in (-1, 1):
    lifted(PZ, round_tower, "Keep Turret", s * (KW / 2 + .6), KF + 1.2, 3.4, KH + 4, roof=False, banner_on=False, windows=True, phase=s * 1.7)
# the crown stands on a drum so its whole band rises clear of the keep's battlements
lathe("Crown Drum", [(13.4, 0), (13.4, 2.6), (13.9, 2.8), (13.9, 3.1), (13.2, 3.2), (13.2, 3.5), (0, 3.5)], TOWER, segs=96, coll_name=C, loc=(0, KY, KT + .2))
lathe("Crown Drum Gilt", [(13.42, 2.3), (13.5, 2.3), (13.5, 2.45), (13.42, 2.45), (13.42, 2.3)], GOLD, segs=96, coll_name=C, loc=(0, KY, KT + .2), caps=False)
scaled(1.3, Vector((0, KY, KT + 3.7)), crown_roof, 0, KY, KT + 3.7, 10.4)

# ================= towers far away in the haze =================
# (the king and bishop spires are gone: hidden behind the towers, only the king's cross showed, floating)
for x, y, r, h in ((-62, 120, 4.0, 36), (-44, 140, 3.5, 40), (-20, 150, 3.0, 32), (18, 148, 3.2, 36), (46, 128, 4.0, 40), (66, 112, 3.5, 30)):
    lathe("Far Tower", [(r, 0), (r, h), (r * 1.15, h + .5), (r * 1.15, h + 1.4), (0, h + 1.4)], HAZE_STONE, segs=32, coll_name="Far", loc=(x, y, 0))
    for i in range(10):
        a = TAU * (i + .5) / 10
        o = bx("Far Merlon", (x + math.cos(a) * r * 1.1, y + math.sin(a) * r * 1.1, h + 1.9), (.5, .9, 1.0), HAZE_STONE, bevel=0, coll_name="Far")
        o.rotation_euler = (0, 0, a)
    bx("Far Wall", (x + (7 if x < 0 else -7), y + 2, h * .35), (14, 2, h * .7), HAZE_STONE, bevel=0, coll_name="Far")

# ================= birds =================
for (x, y, z, s, a) in ((-30, 90, 52, 1.6, 10), (-24, 92, 56, 1.2, -8), (-35, 95, 58, 1.1, 20), (22, 100, 60, 1.5, -15),
                        (28, 102, 57, 1.1, 5), (33, 98, 63, 1.0, 25), (-8, 115, 70, 1.0, 0)):
    for side in (-1, 1):
        wng = bx("Bird Wing", (x + side * .45 * s, y, z + .12 * s), (.9 * s, .08 * s, .05 * s), BIRD, bevel=.01, coll_name="Birds")
        wng.rotation_euler = (0, math.radians(-side * 24), math.radians(a))
    bx("Bird Body", (x, y, z), (.3 * s, .1 * s, .1 * s), BIRD, bevel=.02, coll_name="Birds")

# ================= sky, air and sun (10-22, 승규 님: "AI 같은 거 말고 실제 같이") =================
# A physically based sky instead of a painted gradient and puffy volume clouds, a faint haze through the air
# so distance pales the far towers, one sun with a soft cool fill from the sky, less plastic gold.
w = bpy.data.worlds.new("Sky"); sc.world = w; w.use_nodes = True; nt = w.node_tree
bgn = nt.nodes['Background']
sky = nt.nodes.new('ShaderNodeTexSky')
for kind in ('MULTIPLE_SCATTERING', 'NISHITA', 'HOSEK_WILKIE'):
    try:
        sky.sky_type = kind; break
    except Exception: pass
for attr, val in (('sun_disc', False), ('sun_elevation', math.radians(38)), ('sun_rotation', math.radians(205)), ('altitude', 0.0), ('air_density', 1.0), ('aerosol_density', 1.0)):
    try: setattr(sky, attr, val)
    except Exception as e: print("SKY", attr, e)
nt.links.new(sky.outputs['Color'], bgn.inputs['Color']); bgn.inputs['Strength'].default_value = .22
print("SKY TYPE", sky.sky_type, flush=True)
haze = bpy.data.materials.new("Haze"); haze.use_nodes = True; hn = haze.node_tree
for n in list(hn.nodes):
    if n.type != 'OUTPUT_MATERIAL': hn.nodes.remove(n)
hv = hn.nodes.new('ShaderNodeVolumePrincipled'); hv.inputs['Color'].default_value = srgb("dfe8f2"); hv.inputs['Density'].default_value = .00035; hv.inputs['Anisotropy'].default_value = .35
hn.links.new(hv.outputs['Volume'], next(n for n in hn.nodes if n.type == 'OUTPUT_MATERIAL').inputs['Volume'])
bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 60, 40)); hz = bpy.context.active_object; hz.name = "Air Haze"
hz.scale = (220, 200, 60); hz.data.materials.append(haze)
for p_ in GOLD.node_tree.nodes:
    if p_.type == 'BSDF_PRINCIPLED':
        p_.inputs['Roughness'].default_value = .36; p_.inputs['Base Color'].default_value = srgb("c99a46")

# ================= light =================
sun = bpy.data.lights.new("Sun", 'SUN'); sun.energy = 5.2; sun.angle = math.radians(.6); sun.color = srgb("ffeacc")[:3]
so = bpy.data.objects.new("Sun", sun); coll("Lights").objects.link(so); so.rotation_euler = (math.radians(55), 0, math.radians(-70))   # from the left and a little in front: side light, real shadows
fl = bpy.data.lights.new("Sky Fill", 'SUN'); fl.energy = .45; fl.color = srgb("c6d8f0")[:3]; fl.angle = math.radians(40)
fo = bpy.data.objects.new("Sky Fill", fl); coll("Lights").objects.link(fo); fo.rotation_euler = (math.radians(35), 0, math.radians(160))
for s in (-1, 1):
    l = bpy.data.lights.new("Brazier Fire", 'POINT'); l.energy = 600; l.color = srgb("ff8a3a")[:3]; l.shadow_soft_size = .5
    lo = bpy.data.objects.new("Brazier Fire", l); coll("Lights").objects.link(lo); lo.location = (s * (W1 / 2 + 3.2), L1Y0 + 1.6, L1Z + 2.4)

# ================= camera + render =================
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.cycles.volume_bounces = 1; sc.cycles.volume_step_rate = 4.0
sc.render.resolution_x = 1920; sc.render.resolution_y = 1080; sc.render.resolution_percentage = int(os.environ.get("PCT", "100"))
sc.view_settings.view_transform = "AgX"
sc.view_settings.exposure = -.35
try: sc.view_settings.look = "AgX - Medium High Contrast"
except Exception as e: print("LOOK", e)
cam = bpy.data.cameras.new("Cam"); cam.sensor_fit = 'HORIZONTAL'; cam.angle = math.radians(72)
co = bpy.data.objects.new("Cam Entrance", cam); sc.collection.objects.link(co)
co.location = (0, -18, 5.0)
co.rotation_euler = (Vector((0, GY, 23.0)) - co.location).to_track_quat('-Z', 'Y').to_euler(); sc.camera = co
for o in list(bpy.data.objects):
    if o.location.z < -50: o.hide_render = True; o.hide_set(True)
os.makedirs(r"C:\Work\ChessFight\ArtSource\Lobby\GateCastle", exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=r"C:\Work\ChessFight\ArtSource\Lobby\GateCastle\GateCastle.blend", compress=True)
sc.render.filepath = os.path.join(OUT_DIR, "gate_castle.png"); bpy.ops.render.render(write_still=True)
print("GATE CASTLE DONE", flush=True)
