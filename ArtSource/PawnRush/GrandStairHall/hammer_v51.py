# 승규 11-04 (a shot of a swinging hammer - a steel mount with gold edges and a gold bolt, a heavy gold chain, a polished
# warm-metal ball: "해머 부분 이렇게 해주고 해머 받치는 기둥 부분도 깔끔하게 교체 해"): the grand stair hall's two
# swinging hammers over the curving stair (kit 11 pendulums on black blockout posts and beams) and their stands, in
# place of hammer_v42.py's cream fluted columns, ionic heads, scrolled braces and crowned iron balls. Brushed steel
# (Hammer_Steel) with gold (Palace_Gold, the walls') on every cut corner, and a polished ball (Hammer_Ball), as parts
# Unity puts on the blockout's places (Course01v4Builder.GrandStairHallHammer.cs). The blockout keeps its colliders; the
# parts are pictures only.
#   Hammer_Swing      under the kit pendulum's Pivot, which swings it about Unity +z: a gold pin through the mount's
#                     clevis, a chain of 13 heavy gold links down to a gold eye on the ball, the ball (1.1 m, its middle
#                     6 m under the pivot, as the kit's collider) polished, under a small gold cap with a beaded rim and a boss
#   Hammer_Hanger     the mount the pin turns in, as the shot's: a broad gold plate under the beam (1.06 m), a flat
#                     steel block narrowing down from it to a gold collar (its cut corners gold), four gold bolts under the
#                     plate's corners and two gold clevis cheeks; origin the pivot, Unity x along the beam
#   Hammer_Beam       the beam in the blockout beam's own unit cube (scaled by it to 0.4 x 0.4 x its length): steel, its
#                     four long corners cut and gold
#   Hammer_BeamEnd    the beam carried 0.36 m back past the post's middle line and closed with a gold plate and boss;
#                     origin the beam's start (the post's middle line, the beam's middle height), Unity +z along the beam
#   Hammer_ColBase    the post's foot on the hall floor: a steel plinth, a gold band, a steel step and a gold collar
#   Hammer_ColShaft   the square steel shaft (0.56 m, its corners cut and gold), 1 m tall, stretched by Unity between the
#                     foot and the head
#   Hammer_ColCapital the post's head: a gold collar, a steel block and a gold plate; its top under the beam
# No knee brace: the beam runs on into the hall wall, so it reads as carried at both ends.
# v51 (승규 11-04 "사슬에 애니메이션을 넣어서 스윙하는 듯한 궤적을 만들어라"): the same model, but the 13 chain links are
# parts of their own, Hammer_Link_01 (top, on the pin) .. Hammer_Link_13 (on the ball's eye), still in the pivot's frame
# (origin the pivot), so Unity can bend the chain as it swings (HammerChainSway); Hammer_Swing keeps the pin, its nuts,
# the eye and the ball.
# Frames: modelled with Blender X = Unity x, Y = Unity z, Z = Unity y, then turned half round about Z before export, as
# pads_v28.py, since this export brings Blender (X, Y, Z) in as Unity (-x, y, -z).
# Run: blender -b -P hammer_v51.py -- [render] [preview]   -> Assets/Resources/PawnRushGrandStairHall/PendulumHammer.fbx
# (with preview: into renders_v51 instead), pictures in renders_v51
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Assets", "Resources", "PawnRushGrandStairHall"))
OUT = os.path.join(HERE, "renders_v51")
RENDER = "render" in sys.argv
FBX = dict(use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
           axis_forward="-Z", axis_up="Y", bake_anim=False, use_mesh_modifiers=False, mesh_smooth_type="FACE",
           add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
MATS = ("Hammer_Steel", "Palace_Gold", "Hammer_Ball")
STEEL, GOLD, BALL = 0, 1, 2

# The kit pendulum and the hall's blockout (GrandStairHallBuilder.Beats.cs Chandelier, 11_PendulumBall.prefab).
BALL_R, BALL_Z = 1.1, -6.0          # the ball's collider: 2.2 m, 6 m under the pivot
BEAM_BOTTOM = .4                    # the beam's underside over the pivot (its middle 0.6 over it, 0.4 thick)
PIN_R = .07                         # the gold pin through the clevis, on the pivot
LINK_W, LINK_A = .05, .11           # the chain's wire radius and its links' half width (middle line)
EYE_R = .12                         # the ball's gold eye (middle line)
BOSS_TOP = BALL_Z + BALL_R + .1     # the gold boss on the ball's cap
SHAFT_W, SHAFT_C = .56, .06         # the post's square shaft and its cut corners
BASE_TOP, CAPITAL_H = .48, .32      # the post's foot up to the shaft, its head under the beam


def M(x=0., y=0., z=0.):
    return Matrix.Translation((x, y, z))


class Part:
    def __init__(self, name):
        self.name, self.bm = name, bmesh.new()

    def faces(self, verts, mat):
        fs = {f for v in verts for f in v.link_faces}
        for f in fs:
            f.material_index = mat
        return fs

    def cone(self, r1, r2, depth, m, mat, segs=32):
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2,
                                  depth=depth, matrix=m)
        self.faces(g["verts"], mat)

    def sphere(self, r, m, mat, u=24, v=12):
        g = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=r, matrix=m)
        self.faces(g["verts"], mat)

    def grid(self, rings, mat, closed_u=True, closed_v=False, caps=False):
        """Quads between rings of points (each ring closed round), optionally the rings closed into a loop and the
        first and last rings capped."""
        bm = self.bm
        vs = [[bm.verts.new(p) for p in ring] for ring in rings]
        n, k = len(vs), len(vs[0])
        made = []
        for i in range(n if closed_v else n - 1):
            a, b = vs[i], vs[(i + 1) % n]
            for j in range(k if closed_u else k - 1):
                made.append(bm.faces.new((a[j], a[(j + 1) % k], b[(j + 1) % k], b[j])))
        if caps and not closed_v:
            made.append(bm.faces.new(vs[0]))
            made.append(bm.faces.new(list(reversed(vs[-1]))))
        for f in made:
            f.material_index = mat

    def revolve(self, profile, m, mat, segs=48, closed=False):
        """A profile of (radius, z) turned round Z: ends on the axis close to a point; closed=True joins its last point
        back to its first (a ring of section)."""
        bm = self.bm
        rings = []
        for r, z in profile:
            if r < 1e-6:
                rings.append([bm.verts.new(m @ Vector((0., 0., z)))])
            else:
                rings.append([bm.verts.new(m @ Vector((r * math.cos(2 * math.pi * j / segs), r * math.sin(2 * math.pi * j / segs), z)))
                              for j in range(segs)])
        made = []
        n = len(rings)
        for i in range(n if closed else n - 1):
            a, b = rings[i], rings[(i + 1) % n]
            if len(a) == 1 and len(b) == 1:
                continue
            for j in range(segs):
                if len(a) == 1:
                    made.append(bm.faces.new((a[0], b[(j + 1) % segs], b[j])))
                elif len(b) == 1:
                    made.append(bm.faces.new((a[j], a[(j + 1) % segs], b[0])))
                else:
                    made.append(bm.faces.new((a[j], a[(j + 1) % segs], b[(j + 1) % segs], b[j])))
        for f in made:
            f.material_index = mat

    def torus(self, R, r, m, mat, segs=48, ring=12):
        self.revolve([(R + r * math.cos(2 * math.pi * i / ring), r * math.sin(2 * math.pi * i / ring)) for i in range(ring)],
                     m, mat, segs, closed=True)

    def sweep(self, path, profile, mat, closed=False, normal=None):
        """A section (points (u, v)) carried along a path with parallel-transported frames: u along the frame's normal
        (first `normal`, or any square to the path), v along tangent x normal."""
        n = len(path)
        tangents = []
        for i in range(n):
            if closed:
                t = path[(i + 1) % n] - path[i - 1]
            else:
                t = path[min(i + 1, n - 1)] - path[max(i - 1, 0)]
            tangents.append(t.normalized())
        nrm = Vector(normal) if normal is not None else tangents[0].orthogonal()
        nrm = (nrm - tangents[0] * nrm.dot(tangents[0])).normalized()
        rings = []
        for i in range(n):
            t = tangents[i]
            nrm = (nrm - t * nrm.dot(t)).normalized()
            b = t.cross(nrm)
            rings.append([path[i] + nrm * u + b * v for u, v in profile])
        self.grid(rings, mat, closed_u=True, closed_v=closed, caps=not closed)

    def prism(self, outline, y0, y1, mat):
        """An outline of (x, z) points drawn out along Y from y0 to y1."""
        bm = self.bm
        a = [bm.verts.new((x, y0, z)) for x, z in outline]
        b = [bm.verts.new((x, y1, z)) for x, z in outline]
        k = len(outline)
        made = [bm.faces.new(a), bm.faces.new(list(reversed(b)))]
        for j in range(k):
            made.append(bm.faces.new((a[j], a[(j + 1) % k], b[(j + 1) % k], b[j])))
        for f in made:
            f.material_index = mat

    def loft(self, low, high, z0, z1, mats, cap=None, m=None):
        """Side faces between the outline `low` (x, y points) at z0 and `high` at z1, face j in mats[j % len(mats)], the
        ends capped in `cap` (mats[0] when None); m places it."""
        m = Matrix.Identity(4) if m is None else m
        bm = self.bm
        a = [bm.verts.new(m @ Vector((x, y, z0))) for x, y in low]
        b = [bm.verts.new(m @ Vector((x, y, z1))) for x, y in high]
        k = len(low)
        for f in (bm.faces.new(list(reversed(a))), bm.faces.new(b)):
            f.material_index = mats[0] if cap is None else cap
        for j in range(k):
            f = bm.faces.new((a[j], a[(j + 1) % k], b[(j + 1) % k], b[j]))
            f.material_index = mats[j % len(mats)]

    def block(self, w, d, c, z0, z1, mats, cap=None, m=None):
        """A w (X) by d (Y) block from z0 to z1 with its four upright corners cut c; with mats (face, corner) the cut
        corners take the second."""
        self.loft(octagon(w, d, c), octagon(w, d, c), z0, z1, mats, cap, m)

    def finish(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for name in MATS:
            mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            me.materials.append(mat)
        o = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(o)
        for s in bpy.context.scene.objects:
            s.select_set(False)
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
        print("PART", self.name, len(me.polygons), "faces")
        return o


def octagon(w, d, c):
    """A w (X) by d (Y) rectangle with its corners cut c, round from the +X side: its sides alternate a face and a cut
    corner."""
    hw, hd = w / 2, d / 2
    return [(hw, -hd + c), (hw, hd - c), (hw - c, hd), (-hw + c, hd), (-hw, hd - c), (-hw, -hd + c), (-hw + c, -hd), (hw - c, -hd)]


def circle(r, k=10):
    return [(r * math.cos(2 * math.pi * i / k), r * math.sin(2 * math.pi * i / k)) for i in range(k)]


def zone(p, lat0, lat1, r_out, r_in, mat, segs=72):
    """A band of the ball's surface between two latitudes, r_out over it and r_in under it (hidden in the ball)."""
    k = 10
    outer = [(r_out * math.cos(math.radians(a)), BALL_Z + r_out * math.sin(math.radians(a)))
             for a in [lat0 + (lat1 - lat0) * i / k for i in range(k + 1)]]
    inner = [(r_in * math.cos(math.radians(a)), BALL_Z + r_in * math.sin(math.radians(a)))
             for a in [lat1 + (lat0 - lat1) * i / k for i in range(k + 1)]]
    p.revolve(outer + inner, Matrix.Identity(4), mat, segs, closed=True)


ALONG_Y = Matrix.Rotation(math.pi / 2, 4, "X")         # a cone's axis (Z) turned onto -Y
LENGTHWAYS = Matrix.Rotation(-math.pi / 2, 4, "X")     # a block's Z turned onto +Y, its Y onto -Z


# ------------------------------------------------------------------ the swing
def swing():
    p = Part("Hammer_Swing")
    # The pin through the mount's clevis (it turns with the chain, about its own axis), a hex nut outside each cheek.
    p.cone(PIN_R, PIN_R, .44, ALONG_Y, GOLD, 24)
    for y in (-.205, .205):
        p.cone(.1, .1, .05, M(0, y, 0) @ ALONG_Y, GOLD, 6)

    # The chain: interlocking stadium links, each turned a quarter from the last, from the pin to the ball's eye. The
    # first link's top wire rests on the pin, each link's on the one above's foot and the eye's on the last link's
    # foot, so their middle lines are two wire radii apart there; the links' length is what fits that exactly.
    w, a = LINK_W, LINK_A
    top = PIN_R + w                                      # the first link's middle line at its top
    eye_z = BOSS_TOP + w + EYE_R - .03                   # the eye ring's middle, its foot sunk in the boss
    links = 13
    half = (top - eye_z - EYE_R + 2 * w * links) / (2 * links)   # half a link's length along its middle line
    h = half - a                                         # half its straight sides
    pitch = 2 * half - 2 * w
    z0 = top - half
    print("CHAIN", links, "links, length %.3f, width %.3f, pitch %.3f" % (2 * half + 2 * w, 2 * (a + w), pitch))
    for i in range(links):
        zc = z0 - i * pitch
        path = []
        for j in range(14):                               # top semicircle, then bottom
            t = math.pi * j / 13
            path.append(Vector((a * math.cos(t), 0., zc + h + a * math.sin(t))))
        for j in range(14):
            t = math.pi + math.pi * j / 13
            path.append(Vector((a * math.cos(t), 0., zc - h + a * math.sin(t))))
        if i % 2:
            rot = Matrix.Rotation(math.pi / 2, 3, "Z")
            path = [Vector((0, 0, zc)) + rot @ (q - Vector((0, 0, zc))) for q in path]
        link = Part("Hammer_Link_%02d" % (i + 1))           # v51: each link its own part, Unity bends the chain
        link.sweep(path, circle(w, 12), GOLD, closed=True)
        LINKS.append(link.finish())
    # The eye, square to the last link (a link in XZ when even, YZ when odd), on the boss.
    p.torus(EYE_R, w, M(0, 0, eye_z) @ Matrix.Rotation(math.pi / 2, 4, "X" if (links - 1) % 2 else "Y"), GOLD, 32, 12)

    # The ball, polished, under a small gold cap: a shell over its top 18 degrees, a bead round its rim, the boss.
    p.sphere(BALL_R, M(0, 0, BALL_Z), BALL, 72, 36)
    rim = 72.
    zone(p, rim, 89.5, BALL_R + .025, BALL_R - .03, GOLD)
    rr = BALL_R + .03
    p.torus(rr * math.cos(math.radians(rim)), .03, M(0, 0, BALL_Z + rr * math.sin(math.radians(rim))), GOLD, 72, 10)
    p.revolve([(0, BOSS_TOP), (.12, BOSS_TOP), (.15, BOSS_TOP - .03), (.15, BALL_Z + BALL_R - .02), (0, BALL_Z + BALL_R - .02)],
              Matrix.Identity(4), GOLD, 32)
    return p.finish()


# ------------------------------------------------------------------ the stand
def hanger():
    """Origin the pivot, X along the beam, the pin along Y."""
    p = Part("Hammer_Hanger")
    p.block(1.06, 1.06, .07, BEAM_BOTTOM - .05, BEAM_BOTTOM, [GOLD])
    p.loft(octagon(.44, .44, .04), octagon(.96, .96, .09), .14, BEAM_BOTTOM - .05, [STEEL, GOLD])
    p.block(.5, .5, .045, .1, .14, [GOLD])
    for x in (-.45, .45):
        for y in (-.45, .45):
            p.cone(.04, .04, .03, M(x, y, BEAM_BOTTOM - .065), GOLD, 6)
    # The clevis: two gold cheeks the pin goes through, the first link hanging between them.
    cheek = [(-.13, .12), (.13, .12)] + [(.13 * math.cos(-math.pi * i / 16), .13 * math.sin(-math.pi * i / 16)) for i in range(17)]
    for y0 in (-.17, .13):
        p.prism(cheek, y0, y0 + .04, GOLD)
    return p.finish()


def beam():
    """In the blockout beam's unit cube: X its width, Y its length, Z its height."""
    p = Part("Hammer_Beam")
    p.block(1., 1., .1, -.5, .5, [STEEL, GOLD], m=LENGTHWAYS)
    return p.finish()


def beam_end():
    """Origin the beam's start (the post's middle line, the beam's middle height), Y along the beam."""
    p = Part("Hammer_BeamEnd")
    p.block(.4, .4, .04, -.36, 0., [STEEL, GOLD], m=LENGTHWAYS)
    p.block(.46, .46, .04, -.4, -.36, [GOLD], m=LENGTHWAYS)
    p.cone(.09, .07, .05, M(0, -.425, 0) @ ALONG_Y, GOLD, 32)
    return p.finish()


def col_base():
    p = Part("Hammer_ColBase")
    p.block(1.04, 1.04, .07, 0., .24, [STEEL, GOLD])
    p.block(1.08, 1.08, .05, .24, .28, [GOLD])
    p.block(.78, .78, .06, .28, .44, [STEEL, GOLD])
    p.block(.66, .66, .05, .44, BASE_TOP, [GOLD])
    return p.finish()


def col_shaft():
    """1 m tall from Z 0; Unity stretches it."""
    p = Part("Hammer_ColShaft")
    p.block(SHAFT_W, SHAFT_W, SHAFT_C, 0., 1., [STEEL, GOLD])
    return p.finish()


def col_capital():
    p = Part("Hammer_ColCapital")
    p.block(.66, .66, .05, 0., .04, [GOLD])
    p.block(.76, .76, .06, .04, .28, [STEEL, GOLD])
    p.block(.86, .86, .05, .28, CAPITAL_H, [GOLD])
    return p.finish()


# ------------------------------------------------------------------ build, export, render
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)
LINKS = []
parts = [swing(), hanger(), beam(), beam_end(), col_base(), col_shaft(), col_capital()] + LINKS

turn = Matrix.Rotation(math.pi, 4, "Z")
for o in parts:
    o.data.transform(turn)
for s in bpy.context.scene.objects:
    s.select_set(s in parts)
bpy.context.view_layer.objects.active = parts[0]
if "preview" in sys.argv:                               # -- render preview: the FBX beside the pictures, not into Unity
    os.makedirs(OUT, exist_ok=True)
path = os.path.join(OUT if "preview" in sys.argv else RES, "PendulumHammer.fbx")
bpy.ops.export_scene.fbx(filepath=path, **FBX)
print("EXPORT", path, sum(len(o.data.polygons) for o in parts), "faces")
for o in parts:
    o.data.transform(turn)                              # back to the modelling frame for the pictures

if RENDER:
    os.makedirs(OUT, exist_ok=True)
    look = {"Hammer_Steel": ((.56, .57, .6, 1), .9, .38), "Palace_Gold": ((.86, .64, .28, 1), 1., .24),
            "Hammer_Ball": ((.82, .72, .64, 1), 1., .1)}
    for name, (col, metal, rough) in look.items():
        mat = bpy.data.materials[name]
        try:
            mat.use_nodes = True
        except AttributeError:
            pass
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
    # The stand as the hall puts it: the post 6.2 m in from the pivot, the pivot 7.3 m over a 9 m walkway.
    by = {o.name: o for o in parts}
    pivot_z, post_y = 16., -6.2
    floor = -4.
    capital_top = pivot_z + BEAM_BOTTOM
    shaft_h = capital_top - CAPITAL_H - (floor + BASE_TOP)

    def place(o, m):
        o.matrix_world = m

    place(by["Hammer_ColBase"], M(0, post_y, floor))
    place(by["Hammer_ColShaft"], M(0, post_y, floor + BASE_TOP) @ Matrix.Diagonal((1, 1, shaft_h, 1)))
    place(by["Hammer_ColCapital"], M(0, post_y, capital_top - CAPITAL_H))
    place(by["Hammer_BeamEnd"], M(0, post_y, pivot_z + .6))
    length = 9.
    place(by["Hammer_Beam"], M(0, post_y + length / 2, pivot_z + .6) @ Matrix.Diagonal((.4, length, .4, 1)))
    # The hanger's X is the pendulum's x, along the beam (Y here).
    along = Matrix.Rotation(math.pi / 2, 4, "Z")
    place(by["Hammer_Hanger"], M(0, 0, pivot_z) @ along)
    for o in parts:
        if o.name == "Hammer_Swing" or o.name.startswith("Hammer_Link_"):
            place(o, M(0, 0, pivot_z) @ along @ Matrix.Rotation(math.radians(28), 4, "Y"))
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, floor))
    ground = bpy.context.active_object
    # The walkway under it, along the way (the axle, X here), its top 7.45 m under the pivot.
    bpy.ops.mesh.primitive_cube_add(location=(0, .6, pivot_z - 7.45 - .6))
    walk = bpy.context.active_object
    walk.scale = (8, 2.4, .6)
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = 1100, 900
    world = bpy.data.worlds.new("w") if sc.world is None else sc.world
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (.32, .3, .28, 1)
    bg.inputs["Strength"].default_value = 1.2
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), math.radians(10), math.radians(40)))
    sun = bpy.context.active_object
    sun.data.energy = 4
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(name, eye, at, lens):
        cam.location = eye
        d = Vector(at) - Vector(eye)
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam_data.lens = lens
        sc.render.filepath = os.path.join(OUT, name)
        bpy.ops.render.render(write_still=True)
        print("RENDER", sc.render.filepath)

    shot("hammer_all.png", (17, -15, 13), (0, -2.6, 11), 22)
    shot("hammer_head.png", (2.6, -2.4, 16.4), (0, -5.4, 15.4), 32)
    shot("hammer_foot.png", (2.4, -3.4, -2.2), (0, -6.2, -3.4), 30)

    # As the shot it follows: a dark warm room, the swing hanging still, a warm key light and a cool rim.
    for o in parts:
        if o.name == "Hammer_Swing" or o.name.startswith("Hammer_Link_"):
            place(o, M(0, 0, pivot_z) @ along)
    bg.inputs["Color"].default_value = (.035, .03, .028, 1)
    bg.inputs["Strength"].default_value = 1.
    sun.data.energy = 1.5
    ground.hide_render = walk.hide_render = True
    for loc, power, col in (((4, -3, 15), 4000, (1., .78, .55)), ((-4, 3, 13), 1500, (.75, .85, 1.)),
                            ((2, 4, 8), 2500, (1., .7, .45))):
        bpy.ops.object.light_add(type="AREA", location=loc)
        lamp = bpy.context.active_object
        lamp.data.energy, lamp.data.color, lamp.data.size = power, col, 3
        d = Vector((0, 0, 12)) - Vector(loc)
        lamp.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    shot("hammer_look.png", (7.5, -5.2, 14.4), (0, 0, 12.6), 32)
    shot("hammer_mount.png", (2.2, -1.6, 15.1), (0, 0, 15.9), 40)
    shot("hammer_ball.png", (3.2, -2.2, 11.4), (0, 0, 10.4), 45)
