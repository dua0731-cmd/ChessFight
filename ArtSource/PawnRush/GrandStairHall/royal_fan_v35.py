# 승규 11-04 (an asset preview: a square stone fan housing with gold corner caps bearing crowns, gold studs, a thick gold
# ring round a six-bladed fan, wind streaming out of it): "1번째 사진 같이 모델링 해주고 회전 애니메이션 넣은 다음 택스쳐
# 입히고 적용해" + "블렌더에서 아주 세심하게". The hall H's two wind vents on the windy way (VentModel of 15_AirVent:
# housing 2.8 x 2.6 x 0.5 m, its front face the vent's front, the wind out of it) become this royal fan.
#   RoyalFan_Frame  the housing, still: a bevelled stone box with a round throat, a raised border round its front with
#                   a gold bead inside it, recessed panels on its sides and top, gold studs along the border and in the
#                   panels, four gold corner caps with a crown on a plaque on their front and outer faces, a gold flange
#                   and a thick gold ring round the throat with twelve clamp bands and bolts, and inside the throat a
#                   dark back plate, a steel grille, three struts and the bearing
#   RoyalFan_Rotor  what turns: six swept, twisted blades (cambered, thin at their edges, round at their tips) on a
#                   dark hub with a gold dome, a gold band and bolts; its origin the hub's centre, it turns about the
#                   housing's front axis (Unity RoyalFanSpin, faster as the vent warns and blows); here a looping
#                   keyframed turn for the preview
# Materials (Unity GrandStairHallModel.FanMaterial dresses them by name):
#   RoyalFan_Frame  weathered grey-beige stone (RoyalFan_Frame.png + _N), boxed UVs 1.4 m a tile
#   RoyalFan_Gold   hammered, brushed gold (RoyalFan_Gold.png + _N), 0.6 m a tile
#   RoyalFan_Blade  dark gunmetal brushed along the blade, worn lighter edges, gold pinstripes, two rivets at the root
#                   (RoyalFan_Blade.png + _N), one blade over the whole texture
#   RoyalFan_Steel  dark steel (the throat, grille, struts, bearing)     RoyalFan_Dark  the back plate, near black
# Units metres. Blender -Y is the front (arrives as Unity +z, like the falling knight), +Z up, origin at the middle of
# the housing's front face's bottom edge (the vent's origin: floor level, its front plane).
# Run: blender -b -P royal_fan_v35.py -- [render]
#   -> Assets/Resources/PawnRushGrandStairHall/RoyalFan.fbx and RoyalFan_*.png, royal_fan_v35.blend, renders_fan_v35/
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Matrix, Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
RENDER = "render" in sys.argv
OUT = os.path.join(HERE, "renders_fan_v35")

W, H, D = 2.8, 2.6, 0.5          # the housing (x, z, y)
CZ = H / 2                       # the throat's axis height
R_HOLE = .96                     # the throat
BAND = .14                       # the raised border's width
CAP = .46                        # a corner cap's side (x, z); it stands .02 proud of the housing all round
ROTOR_Y = .17                    # the rotor's middle, behind the front face
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
rng = np.random.default_rng(35)


# ================================================================== textures (tileable, drawn with numpy)
def vnoise(n, cx, cy):
    """Smooth value noise on an n x n tile, cx x cy cells, wrapping."""
    g = rng.random((cy, cx))

    def axis(c):
        x = np.arange(n) * c / n
        i0 = np.floor(x).astype(int)
        f = x - i0
        return i0 % c, (i0 + 1) % c, f * f * (3 - 2 * f)
    y0, y1, fy = axis(cy)
    x0, x1, fx = axis(cx)
    a, b = g[np.ix_(y0, x0)], g[np.ix_(y0, x1)]
    c, d = g[np.ix_(y1, x0)], g[np.ix_(y1, x1)]
    fx, fy = fx[None, :], fy[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(n, cells, octaves, gain=.5, stretch=1.0):
    """Fractal noise; stretch > 1 draws it out along v (rows), < 1 along u."""
    s, amp, tot = np.zeros((n, n)), 1.0, 0.0
    for o in range(octaves):
        c = min(n, cells * 2 ** o)
        s += amp * vnoise(n, max(1, min(n, int(round(c * max(1, 1 / stretch))))), max(1, min(n, int(round(c / max(1, stretch))))))
        tot += amp
        amp *= gain
    return s / tot


def scratches(n, count, lmin, lmax, angle=None, spread=.2):
    m = np.zeros((n, n))
    for _ in range(count):
        x, y = rng.random(2) * n
        a = rng.random() * math.pi if angle is None else angle + rng.normal() * spread
        length = rng.uniform(lmin, lmax) * n
        t = np.linspace(0, 1, int(length) * 2 + 2)
        xs = (x + math.cos(a) * length * t).astype(int) % n
        ys = (y + math.sin(a) * length * t).astype(int) % n
        np.maximum.at(m, (ys, xs), np.sqrt(np.sin(math.pi * t)) * rng.uniform(.35, 1))
    return m


def blur(a, k=1):
    for _ in range(k):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5
    return a


def normal_map(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, -dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * .5 + .5


def mix(c0, c1, t):
    t = np.clip(t, 0, 1)[..., None]
    return np.array(c0) * (1 - t) + np.array(c1) * t


def save_png(name, rgb, color=True):
    n = rgb.shape[0]
    img = bpy.data.images.get(name) or bpy.data.images.new(name, n, n, alpha=False)
    img.colorspace_settings.name = "sRGB" if color else "Non-Color"
    px = np.ones((n, n, 4), np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(RES, name + ".png")
    img.file_format = "PNG"
    img.save()
    print("TEXTURE", name, n)
    return img


def frame_texture(n=1024):
    """Weathered grey-beige stone: mottled, finely speckled, a few chips and scratches, grime run down (v up)."""
    # v2 (first renders: too white, blotchy chips): soft mottling, few small dark pits.
    # v3 (승규 11-04, Unity screenshot "수정해": in the hall's shade the v2 grey read as black slate with stripes): a light
    # warm grey-beige like the reference and the hall's cream walls, the grime runs only faint.
    big = fbm(n, 3, 3)
    mid = fbm(n, 8, 5)
    fine = vnoise(n, 256, 256) * .5 + vnoise(n, 512, 512) * .5
    streak = fbm(n, 24, 3, stretch=10)                    # drawn out up and down: rain-run grime
    scr = blur(scratches(n, 140, .015, .06), 1)
    pits = blur((fbm(n, 64, 2) > .82).astype(float), 1)
    tone = np.clip((.55 * mid + .45 * big - .5) * 1.4 + .5, 0, 1)
    rgb = mix((.66, .63, .57), (.82, .79, .72), tone)
    rgb *= (.97 + .06 * fine)[..., None]
    rgb *= (1 - .07 * np.clip((streak - .5) * 3, 0, 1))[..., None]
    rgb = mix(rgb, (.88, .86, .81), scr * .3)
    rgb = mix(rgb, (.45, .42, .38), pits * .45)
    rgb *= mix((1.0, .985, .95), (.975, .98, .99), big)         # warm and cool patches
    height = .4 * mid + .2 * fine - .4 * scr - .6 * pits
    return rgb, normal_map(height, 1.4)


def gold_texture(n=512):
    """Hammered gold, brushed along u, deeper and redder in its hollows, a few dark specks of age."""
    # v2 (first renders: crumpled like foil): polished, the hammering only a soft ripple.
    ham = fbm(n, 8, 3, gain=.45)
    brush = vnoise(n, 3, 512) * .6 + vnoise(n, 6, 256) * .4      # lines along u
    broad = fbm(n, 3, 3)
    specks = np.clip((fbm(n, 64, 2) - .76) * 6, 0, 1)
    rgb = mix((.74, .53, .2), (.9, .7, .32), .4 * ham + .6 * broad)
    rgb *= (.96 + .07 * brush)[..., None]
    rgb = mix(rgb, (.4, .27, .1), specks * .5)
    height = .6 * ham + .1 * brush
    return rgb, normal_map(height, .9)


def blade_texture(n=512):
    """One blade over the whole tile (u across its chord, leading edge at 0; v root to tip): dark gunmetal brushed
    along the blade, lighter worn edges and tip, a gold pinstripe inside each edge, two rivets at the root."""
    u = (np.arange(n) + .5) / n
    U, V = np.meshgrid(u, u)
    brush = vnoise(n, 384, 4) * .6 + vnoise(n, 160, 8) * .4      # lines along v
    mott = fbm(n, 6, 4)
    rgb = mix((.13, .14, .16), (.25, .26, .29), .6 * mott + .4 * brush)
    edge = np.minimum(U, 1 - U)
    wear = np.clip((.07 - edge) / .07, 0, 1) * (.55 + .45 * fbm(n, 30, 2))
    wear = np.maximum(wear, np.clip((V - .93) / .07, 0, 1) * .8)
    rgb = mix(rgb, (.50, .50, .52), wear)
    stripe = np.exp(-((edge - .11) / .006) ** 2) * (V > .08) * (V < .96)
    rgb = mix(rgb, (.85, .64, .26), stripe * .9)
    height = .3 * brush + .2 * mott
    for cu in (.33, .67):
        r = np.sqrt(((U - cu) * 1.0) ** 2 + ((V - .05) * 1.0) ** 2)
        dome = np.clip(1 - (r / .028) ** 2, 0, 1)
        rgb = mix(rgb, (.62, .60, .58), np.sqrt(dome))
        rgb = mix(rgb, (.06, .06, .07), np.exp(-((r - .03) / .004) ** 2) * .8)
        height += 1.5 * np.sqrt(dome)
    return rgb, normal_map(height, 2.0)


images = {}
for name, fn in (("RoyalFan_Frame", frame_texture), ("RoyalFan_Gold", gold_texture), ("RoyalFan_Blade", blade_texture)):
    rgb, nrm = fn()
    images[name] = save_png(name, rgb)
    images[name + "_N"] = save_png(name + "_N", nrm, color=False)


# ================================================================== materials (Blender's for the preview; Unity redoes them)
def material(name, base, metal, rough, tex=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*base, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    if tex:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = images[tex]
        nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = images[tex + "_N"]
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    m.diffuse_color = (*base, 1)
    return m


MAT = {
    "frame": material("RoyalFan_Frame", (.62, .6, .56), .1, .62, "RoyalFan_Frame"),
    "gold": material("RoyalFan_Gold", (.86, .64, .28), 1.0, .3, "RoyalFan_Gold"),
    "blade": material("RoyalFan_Blade", (.2, .21, .23), .8, .4, "RoyalFan_Blade"),
    "steel": material("RoyalFan_Steel", (.2, .2, .22), .85, .45),
    "dark": material("RoyalFan_Dark", (.03, .03, .035), .3, .7),
}
TILE = {"RoyalFan_Frame": 1.4, "RoyalFan_Gold": .6, "RoyalFan_Steel": 1.0, "RoyalFan_Dark": 1.0}


# ================================================================== geometry helpers
def finish(ob, mat, smooth=False):
    ob.data.materials.clear()
    ob.data.materials.append(MAT[mat])
    for p in ob.data.polygons:
        p.use_smooth = smooth
    return ob


def cube(name, mat, center, size, rot=(0, 0, 0)):
    """A box of `size` metres; its size is in the mesh (not the object's scale) so a bevel is even all round."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rot)
    ob = bpy.context.active_object; ob.name = name
    ob.data.transform(Matrix.Diagonal((*size, 1)))
    return finish(ob, mat)


def cyl(name, mat, center, r, depth, rot=(math.pi / 2, 0, 0), verts=64, smooth=True):
    """A cylinder, along y by default."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=center, rotation=rot)
    ob = bpy.context.active_object; ob.name = name
    return finish(ob, mat, smooth)


def sphere(name, mat, center, r, scale=(1, 1, 1), seg=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r, location=center, scale=scale)
    ob = bpy.context.active_object; ob.name = name
    return finish(ob, mat, True)


def torus(name, mat, center, major, minor, rot=(math.pi / 2, 0, 0), seg=128, mseg=20):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=mseg, major_radius=major, minor_radius=minor,
                                     location=center, rotation=rot)
    ob = bpy.context.active_object; ob.name = name
    return finish(ob, mat, True)


def bevel(ob, width, segments=2, angle=30):
    m = ob.modifiers.new("Bevel", "BEVEL")
    m.width, m.segments, m.limit_method, m.angle_limit = width, segments, "ANGLE", math.radians(angle)
    return ob


def cut(ob, cutter):
    m = ob.modifiers.new("Cut " + cutter.name, "BOOLEAN")
    m.operation, m.solver, m.object = "DIFFERENCE", "EXACT", cutter
    cutter.hide_render = True
    return ob


def bake(obs):
    """Modifiers and transforms into the meshes."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def remove(obs):
    for o in obs:
        bpy.data.objects.remove(o, do_unlink=True)


def join(name, obs):
    bake(obs)
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = ob.data.name = name
    return ob


def plate_and_crown(prefix, center, right, up, out):
    """A raised square plaque with a five-point crown on it, three pearls on its points and three jewels on its band.
    right / up / out: the face's axes, right x up = out (out = its normal)."""
    right, up, out = Vector(right), Vector(up), Vector(out)
    basis = Matrix((right, up, out)).transposed().to_4x4()
    parts = []
    pl = cube(prefix + " plaque", "gold", (0, 0, 0), (.32, .32, .014))
    bevel(pl, .005, 2)
    pl.matrix_world = Matrix.Translation(Vector(center) + out * .007) @ basis
    parts.append(pl)
    # the crown's outline (x across, y up), 0.25 m wide, extruded 0.022 off the plaque
    outline = [(-.105, -.085), (.105, -.085), (.105, -.03), (.125, .085), (.058, .005), (0, .1), (-.058, .005),
               (-.125, .085), (-.105, -.03)]
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, 0)) for x, y in outline]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]:
        v.co.z += .022
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=[g for g in bm.faces if len(g.verts) > 4], ngon_method="EAR_CLIP")
    me = bpy.data.meshes.new(prefix + " crown"); bm.to_mesh(me); bm.free()
    cr = bpy.data.objects.new(prefix + " crown", me); sc.collection.objects.link(cr)
    finish(cr, "gold")
    bevel(cr, .004, 1, 40)
    cr.matrix_world = Matrix.Translation(Vector(center) + out * .014) @ basis
    parts.append(cr)
    base = Vector(center) + out * .036
    for x, y in ((-.125, .085), (0, .1), (.125, .085)):
        parts.append(sphere(prefix + " pearl", "gold", base + right * x + up * (y + .014), .021, seg=14, rings=8))
    for x in (-.055, 0, .055):
        parts.append(sphere(prefix + " jewel", "gold", base + right * x + up * -.058, .016, seg=12, rings=8))
    return parts


def stud(name, at, out, r=.03):
    """A gold dome on a face (a squashed half-buried sphere)."""
    out = Vector(out)
    s = sphere(name, "gold", Vector(at), r, scale=(1, 1, 1), seg=16, rings=8)
    s.rotation_euler = out.to_track_quat("Z", "Y").to_euler()
    s.scale = (1, 1, .55)
    return s


# ================================================================== the housing (RoyalFan_Frame)
frame = []
# Body: the stone box behind the front face (y 0 .. D), the throat through it.
body = cube("Body", "frame", (0, D / 2, CZ), (W, D, H))
# v2 (first renders: from behind, a hole with a dark disc in it): the throat stops 4 cm short of the back, its floor the
# dark back plate (the back stays solid stone, with a recessed panel like the sides).
THROAT_END = D - .04
throat = cyl("Throat cutter", "frame", (0, (THROAT_END - .2) / 2, CZ), R_HOLE, THROAT_END + .2, verts=96)
cut(body, throat)
# recessed panels on the sides and top, 15 mm deep, between the caps
side_cuts = []
for sx in (-1, 1):
    side_cuts.append(cube("Side panel cutter", "frame", (sx * W / 2, D / 2, CZ), (.03, D - .14, H - 2 * CAP - .04)))
side_cuts.append(cube("Top panel cutter", "frame", (0, D / 2, H), (W - 2 * CAP - .04, D - .14, .03)))
side_cuts.append(cube("Back panel cutter", "frame", (0, D, CZ), (W - 2 * CAP - .04, .03, H - 2 * CAP - .04)))
for c in side_cuts:
    cut(body, c)
bevel(body, .022, 2, 30)
bake([body])
remove([throat] + side_cuts)
body.data.materials.append(MAT["steel"])
body.data.materials.append(MAT["dark"])
for p in body.data.polygons:
    c = p.center
    rr = math.hypot(c.x, c.z - CZ)
    if abs(rr - R_HOLE) < .035 and abs(p.normal.y) < .6:
        p.material_index = 1          # the throat's wall in steel
        p.use_smooth = True
    elif rr < R_HOLE - .01 and abs(c.y - THROAT_END) < .01 and p.normal.y < -.5:
        p.material_index = 2          # the throat's floor: the dark back plate
frame.append(body)

# The raised border round the front (y -0.05 .. 0), its gold bead inside.
border = cube("Border", "frame", (0, -.025, CZ), (W, .05, H))
hole = cube("Border cutter", "frame", (0, -.025, CZ), (W - 2 * BAND, .3, H - 2 * BAND))
cut(border, hole); bevel(border, .012, 2, 30); bake([border]); remove([hole])
frame.append(border)
bead = cube("Border bead", "gold", (0, -.016, CZ), (W - 2 * BAND + .045, .032, H - 2 * BAND + .045))
bead_hole = cube("Bead cutter", "gold", (0, -.016, CZ), (W - 2 * BAND, .3, H - 2 * BAND))
cut(bead, bead_hole); bevel(bead, .008, 2, 30); bake([bead]); remove([bead_hole])
frame.append(bead)

# The gold flange round the throat and the thick ring on it, its twelve clamp bands and their bolts.
flange = cyl("Flange", "gold", (0, -.016, CZ), 1.12, .032, verts=128)
flange_hole = cyl("Flange cutter", "gold", (0, -.016, CZ), R_HOLE - .005, .3, verts=128)
cut(flange, flange_hole); bevel(flange, .006, 2, 30); bake([flange]); remove([flange_hole])
for p in flange.data.polygons:
    p.use_smooth = abs(p.normal.y) < .6
frame.append(flange)
RING_R, RING_T, RING_Y = 1.035, .068, -.045
frame.append(torus("Ring", "gold", (0, RING_Y, CZ), RING_R, RING_T))
frame.append(torus("Throat bead", "gold", (0, -.004, CZ), R_HOLE + .006, .018, mseg=12))
for k in range(12):
    a = math.radians(k * 30 + 15)
    at = Vector((RING_R * math.cos(a), RING_Y, CZ + RING_R * math.sin(a)))
    tangent = Vector((-math.sin(a), 0, math.cos(a)))
    band = cyl("Clamp band", "gold", at, RING_T + .016, .07, rot=(0, 0, 0), verts=24)
    band.rotation_euler = tangent.to_track_quat("Z", "Y").to_euler()
    bevel(band, .006, 2, 30)
    frame.append(band)
    frame.append(stud("Clamp bolt", at + Vector((0, -(RING_T + .016), 0)), (0, -1, 0), r=.026))

# The four corner caps (.02 proud of the housing all round) with a plaque and crown on the front and on the outside.
for sx in (-1, 1):
    for sz in (-1, 1):
        cx = sx * (W / 2 - CAP / 2 + .02)
        cz = CZ + sz * (H / 2 - CAP / 2 + .02)
        capo = cube("Corner cap", "gold", (cx, D / 2 - .01, cz), (CAP, D + .12, CAP))
        bevel(capo, .022, 3, 30)
        frame.append(capo)
        frame += plate_and_crown("Cap front", (cx, -.07, cz), (1, 0, 0), (0, 0, 1), (0, -1, 0))
        frame += plate_and_crown("Cap side", (sx * (W / 2 + .02), D / 2 - .01, cz), (0, sx, 0), (0, 0, 1), (sx, 0, 0))

# Studs: along the border (top, bottom, both sides) and down the side and top panels.
for x in np.linspace(-.84, .84, 7):
    for z in (BAND / 2, H - BAND / 2):
        frame.append(stud("Border stud", (x, -.05, z), (0, -1, 0)))
for z in np.linspace(.66, H - .66, 6):
    for sx in (-1, 1):
        frame.append(stud("Border stud", (sx * (W / 2 - BAND / 2), -.05, z), (0, -1, 0)))
        frame.append(stud("Panel stud", (sx * (W / 2 - .015), D / 2, z), (sx, 0, 0), r=.034))
for x in np.linspace(-.7, .7, 5):
    frame.append(stud("Panel stud", (x, D / 2, H - .015), (0, 0, 1), r=.034))

# Inside the throat: the dark back plate, a steel grille in front of it, three struts and the bearing.
for x in np.linspace(-.7, .7, 5):
    for z in (CAP + .14, H - CAP - .14):
        frame.append(stud("Back stud", (x, D - .015, z), (0, 1, 0), r=.034))
for r in (.32, .58, .84):
    frame.append(torus("Grille ring", "steel", (0, .44, CZ), r, .012, seg=96, mseg=8))
for k in range(8):
    a = k * math.pi / 8
    frame.append(cube("Grille bar", "steel", (0, .44, CZ), (2 * R_HOLE - .02, .018, .018), rot=(0, -a, 0)))
for k in range(3):
    a = math.radians(90 + 120 * k)
    mid = Vector((.55 * math.cos(a), .38, CZ + .55 * math.sin(a)))
    s = cube("Strut", "steel", mid, (.84, .05, .07), rot=(0, -a, 0))
    bevel(s, .01, 1)
    frame.append(s)
frame.append(cyl("Bearing", "steel", (0, .385, CZ), .11, .11, verts=32))
frame.append(cyl("Bearing cap", "steel", (0, .32, CZ), .085, .02, verts=32))

frame_ob = join("RoyalFan_Frame", frame)


# ================================================================== the rotor (RoyalFan_Rotor)
rotor = []
O = Vector((0, ROTOR_Y, CZ))
rotor.append(cyl("Hub", "steel", O, .19, .18, verts=48))
rotor.append(cyl("Hub back", "steel", O + Vector((0, .115, 0)), .15, .05, verts=48))
rotor.append(sphere("Hub dome", "gold", O + Vector((0, -.09, 0)), .19, scale=(1, .5, 1), seg=48, rings=16))
rotor.append(torus("Hub band", "gold", O + Vector((0, -.085, 0)), .19, .016, seg=64, mseg=10))
rotor.append(sphere("Hub finial", "gold", O + Vector((0, -.19, 0)), .045, seg=16, rings=10))
for k in range(6):
    a = math.radians(k * 60 + 30)
    rr = .12
    y = -.09 - .095 * math.sqrt(1 - (rr / .19) ** 2)
    rotor.append(stud("Hub bolt", O + Vector((rr * math.cos(a), y + .004, rr * math.sin(a))), (0, -1, 0), r=.018))


def blade(k):
    """A swept, twisted, cambered blade with UVs (u across its chord from its leading edge, v root to tip)."""
    M, S = 11, 26
    r0, r1 = .15, .905
    a0 = math.radians(k * 60)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings, uvs = [], {}
    for j in range(S + 1):
        t = j / S
        r = r0 + (r1 - r0) * t
        w = .27 + .25 * min(1, t / .6) ** .8
        if t > .86:
            w *= max(.25, math.sqrt(1 - ((t - .86) / .14) ** 2))
        beta = math.radians(40 - 22 * t)                 # pitch: steep at the root, flatter at the tip
        theta = a0 + math.radians(16 * t ** 1.5)         # swept back
        er = Vector((math.cos(theta), 0, math.sin(theta)))
        et = Vector((-math.sin(theta), 0, math.cos(theta)))
        ea = Vector((0, -1, 0))
        chord = et * math.cos(beta) + ea * math.sin(beta)
        normal = -et * math.sin(beta) + ea * math.cos(beta)
        thick = .034 * (1 - .65 * t)
        camber = .028 * w
        ring = []
        sides = [(i, 1) for i in range(M)] + [(i, -1) for i in range(M - 2, 0, -1)]
        for i, side in sides:
            s = -w / 2 + w * i / (M - 1)
            q = 1 - (2 * s / w) ** 2
            h = side * thick / 2 * max(q, 0) ** .6 + camber * q
            p = O + er * r + chord * s + normal * h
            v = bm.verts.new(p)
            ring.append(v)
            uvs[v] = (i / (M - 1), t)
        rings.append(ring)
    L = len(rings[0])
    for j in range(S):
        for i in range(L):
            a, b = rings[j][i], rings[j][(i + 1) % L]
            c, d = rings[j + 1][(i + 1) % L], rings[j + 1][i]
            bm.faces.new((a, b, c, d))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        for l in f.loops:
            l[uv].uv = uvs[l.vert]
    me = bpy.data.meshes.new("Blade"); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("Blade %d" % (k + 1), me); sc.collection.objects.link(ob)
    return finish(ob, "blade", True)


for k in range(6):
    rotor.append(blade(k))
rotor_ob = join("RoyalFan_Rotor", rotor)


# ================================================================== UVs, normals, origins
def box_uv(ob, skip=()):
    """Boxed UVs from the object's own space, each material at its tile size (the blades keep theirs)."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    layer = bm.loops.layers.uv.verify()
    names = [m.name for m in ob.data.materials]
    for f in bm.faces:
        name = names[f.material_index]
        if name in skip:
            continue
        tile = TILE.get(name, 1.0)
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            if ax == 0:
                u, v = (p.y if n.x > 0 else -p.y), p.z
            elif ax == 1:
                u, v = (-p.x if n.y > 0 else p.x), p.z
            else:
                u, v = p.x, (p.y if n.z > 0 else -p.y)
            l[layer].uv = (u / tile, v / tile)
    bm.to_mesh(ob.data); bm.free()


def smooth_by_angle(ob, degrees=40):
    """Sharp edges where faces meet at more than `degrees`, smooth elsewhere (flat parts stay flat)."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    limit = math.radians(degrees)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            e.smooth = e.link_faces[0].normal.angle(e.link_faces[1].normal, 0) < limit
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(ob.data); bm.free()


box_uv(frame_ob)
box_uv(rotor_ob, skip=("RoyalFan_Blade",))
smooth_by_angle(frame_ob)
smooth_by_angle(rotor_ob)

# The rotor's origin at the hub's centre (it turns about the housing's y axis).
rotor_ob.data.transform(Matrix.Translation(-O))
rotor_ob.location = O

tris = lambda ob: sum(len(p.vertices) - 2 for p in ob.data.polygons)
print("FAN frame %d tris, rotor %d tris" % (tris(frame_ob), tris(rotor_ob)))
lo = Vector([min(v.co[i] for v in frame_ob.data.vertices) for i in range(3)])
hi = Vector([max(v.co[i] for v in frame_ob.data.vertices) for i in range(3)])
print("FAN frame bounds", [round(x, 3) for x in lo], [round(x, 3) for x in hi])
rlo = Vector([min((rotor_ob.matrix_world @ v.co)[i] for v in rotor_ob.data.vertices) for i in range(3)])
rhi = Vector([max((rotor_ob.matrix_world @ v.co)[i] for v in rotor_ob.data.vertices) for i in range(3)])
print("FAN rotor bounds", [round(x, 3) for x in rlo], [round(x, 3) for x in rhi])

# ================================================================== export (still), then the preview's turn
for o in sc.objects:
    o.select_set(False)
frame_ob.select_set(True); rotor_ob.select_set(True)
bpy.context.view_layer.objects.active = frame_ob
bpy.ops.export_scene.fbx(filepath=os.path.join(RES, "RoyalFan.fbx"), **FBX)
print("EXPORT RoyalFan.fbx")

# One turn every second at 24 fps, looping (Unity turns it from the vent's state instead).
sc.frame_start, sc.frame_end = 1, 24
rotor_ob.rotation_mode = "XYZ"
rotor_ob.rotation_euler = (0, 0, 0); rotor_ob.keyframe_insert("rotation_euler", index=1, frame=1)
rotor_ob.rotation_euler = (0, -2 * math.pi, 0); rotor_ob.keyframe_insert("rotation_euler", index=1, frame=25)
action = rotor_ob.animation_data.action
curves = list(getattr(action, "fcurves", None) or [])
if not curves:   # Blender 5: layered actions
    curves = [c for layer in action.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]
for fc in curves:
    for kp in fc.keyframe_points:
        kp.interpolation = "LINEAR"
    fc.modifiers.new("CYCLES")
sc.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "royal_fan_v35.blend"))
print("SAVED royal_fan_v35.blend")

if RENDER:
    os.makedirs(OUT, exist_ok=True)
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 64
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = 1000
    world = bpy.data.worlds.new("World"); sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.012, .012, .014, 1)
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    floor = bpy.context.active_object
    fm = material("Floor", (.05, .05, .055), 0, .8); floor.data.materials.append(fm)
    # v3: a softer key, nearer the hall's light, so the preview no longer flatters the stone.
    for name, loc, energy, size, color in (("Key", (-3.5, -4.5, 5.5), 850, 3, (1, .93, .82)),
                                           ("Fill", (4.5, -3, 2.0), 300, 4, (.8, .87, 1)),
                                           ("Rim", (2.5, 4.5, 4.5), 900, 2, (1, .95, .9))):
        ld = bpy.data.lights.new(name, "AREA"); ld.energy = energy; ld.size = size; ld.color = color
        lo_ = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo_)
        lo_.location = loc
        lo_.rotation_euler = (Vector((0, .2, CZ)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    shots = (("hero", (3.1, -4.6, 2.9), 50, 1), ("front", (0, -6.2, CZ), 50, 1), ("back", (4.2, 4.0, 2.6), 50, 1),
             ("close", (.9, -2.0, 2.1), 50, 4))
    for name, loc, lens, frame_no in shots:
        cd = bpy.data.cameras.new(name); cd.lens = lens
        cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
        cam.location = loc
        target = Vector((0, .1, CZ)) if name != "close" else Vector((-.1, 0, CZ + .3))
        cam.rotation_euler = (target - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        sc.frame_set(frame_no)
        sc.render.filepath = os.path.join(OUT, "fan_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)
    sc.frame_set(1)
