# J Palace Laundry cloth chess dolls (R509), modelled here in Blender (user, 11-08: "Blender로 직접"), after the GPT sheet
# Docs/PawnRush/ConceptArt/R509_PalaceLaundry/AssetRefs/J_assets_02_dolls.png: stuffed linen pawn, bishop, knight, rook,
# queen, king with puffy padded collars and bases and stitched seams. E's modelling helpers (Mid/e_props_v1.py) are
# imported, not edited.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python j_dolls_v1.py -- [--render] [--export]
#
# Game size: MgKnightPads' pieces are 1.1 m (PieceHeight, capsule radius 0.3), base at the square's top. The dolls keep
# 1.1 m to the shoulders; the queen's and king's crowns stand a little over (1.17 / 1.24 m).
# Two materials, both tinted per station in Unity (MiniGameModels.Laundry): PR_Mid_J_Linen = the body (the other side's
# piece colour, charcoal or cream), PR_Mid_J_DollTrim = collars, base band, seams, crowns' balls, mane (the other side's
# team colour, orange or blue). Origin at the base centre, front -y; the knight's head looks to -x (its profile shows from the front).
# Output Export/J_Doll_<Kind>.fbx.
import bpy, math, os, random, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
MID = os.path.dirname(HERE)
sys.path.insert(0, MID)
import e_props_v1 as P   # noqa: E402
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def LIN(): return P.mat("PR_Mid_J_Linen", tex="PR_Mid_DollLinen_BaseColor.png", rough=.9)
def TRIM(): return P.mat("PR_Mid_J_DollTrim", (.88, .86, .82), rough=.85)


def arc(cz, r, a0, a1, n=8):
    """Profile points round a circle centre (0, cz), angles in degrees from the horizontal."""
    return [(max(0., r * math.cos(math.radians(a0 + (a1 - a0) * i / n))), cz + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


BASE = [(0., 0.), (.27, 0.), (.31, .015), (.335, .05), (.335, .11), (.31, .15), (.27, .175)]


def body(profile, segs=32):
    o = P.lathe(profile, LIN(), segs=segs)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def ring(R, z, r, segs=36):
    """A puffy trim band round the doll (horizontal ring)."""
    t = P.torus(R, r, TRIM(), segs=segs, rsegs=8)
    t.data.transform(Matrix.Translation((0, 0, z)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    for p in t.data.polygons:
        p.use_smooth = True
    return t


def stitches(profile_r, z0, z1, n_lines=4, per=7, phase=0.):
    """Dashed seam stitches running up the body: small flattened trim dashes on n meridians."""
    out = []
    for k in range(n_lines):
        a = phase + 2 * math.pi * k / n_lines
        for i in range(per):
            z = z0 + (z1 - z0) * (i + .5) / per
            r = profile_r(z) + .004
            d = P.box((-.008, -.006, -.022), (.008, .006, .022), TRIM(), bev=.005, seg=1)
            d.data.transform(Matrix.Translation((r * math.cos(a), r * math.sin(a), z)) @ Matrix.Rotation(a, 4, 'Z'))
            out.append(d)
    return out


def radius_of(profile):
    """r(z) along a lathe profile (outer side, rising part)."""
    pts = [p for p in profile if p[0] > 0]
    def f(z):
        for (r0, z0), (r1, z1) in zip(pts, pts[1:]):
            if z0 <= z <= z1 and z1 > z0:
                return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
        return pts[-1][0]
    return f


def collar(z, R=.215):
    return ring(R - .03, z, .04)


def foot():
    return [ring(.325, .085, .03)]


# ------------------------------------------------------------------ the six
def pawn():
    pr = BASE + [(.22, .28), (.155, .47), (.13, .57), (.11, .64)] + arc(.86, .18, -62, 90, 10)
    p = [body(pr), collar(.6)] + foot()
    p += stitches(radius_of(pr), .2, .55)
    return p


def bishop():
    pr = BASE + [(.22, .3), (.15, .55), (.13, .62), (.12, .68), (.15, .74), (.17, .82), (.165, .9), (.13, .98), (.07, 1.05), (.025, 1.085), (0., 1.095)]
    p = [body(pr), collar(.64, .205)] + foot()
    p += stitches(radius_of(pr), .2, .58)
    # the mitre's sewn slit, slanting across its front
    s = P.box((-.012, -.02, -.12), (.012, .02, .12), TRIM(), bev=.008, seg=2)
    s.data.transform(Matrix.Translation((0, -.155, .9)) @ Matrix.Rotation(math.radians(28), 4, 'Y'))
    p.append(s)
    p.append(P.lathe([(0, 1.08), (.03, 1.08), (.032, 1.1), (0, 1.12)], TRIM(), segs=12))   # button on top
    return p


def rook():
    pr = BASE + [(.25, .27), (.215, .45), (.21, .74), (.25, .78), (.275, .82), (.275, .9), (.2, .9), (0, .9)]
    p = [body(pr), collar(.36, .25), ring(.255, .775, .03)] + foot()
    p += stitches(radius_of(pr), .45, .72, 4, 5, math.pi / 4)
    for k in range(4):                                     # four soft crenellations on the rim
        a = math.pi / 4 + k * math.pi / 2
        c = P.box((-.07, -.06, 0), (.07, .06, .14), LIN(), bev=.045, seg=3)
        for q in c.data.polygons:
            q.use_smooth = True
        c.data.transform(Matrix.Translation((.2 * math.cos(a), .2 * math.sin(a), .885)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
        p.append(c)
    return p


def queen():
    pr = BASE + [(.22, .3), (.15, .6), (.13, .66), (.125, .72), (.16, .8), (.205, .9), (.215, .95), (.15, .955), (.06, .99), (0., 1.0)]
    p = [body(pr), collar(.69, .2), ring(.205, .93, .03)] + foot()
    p += stitches(radius_of(pr), .2, .62)
    for k in range(8):                                     # stuffed crown balls
        a = 2 * math.pi * k / 8
        b = P.lathe(arc(0, .048, -90, 90, 6), TRIM(), segs=10)
        b.data.transform(Matrix.Translation((.19 * math.cos(a), .19 * math.sin(a), 1.0)))
        p.append(b)
    top = P.lathe(arc(0, .065, -90, 90, 8), TRIM(), segs=14)
    top.data.transform(Matrix.Translation((0, 0, 1.1)))
    p.append(top)
    return p


def king():
    pr = BASE + [(.23, .3), (.155, .6), (.135, .66), (.13, .72), (.17, .8), (.205, .88), (.21, .97), (.15, .975), (.08, 1.0), (0., 1.01)]
    p = [body(pr), collar(.69, .21), ring(.21, .95, .032)] + foot()
    p += stitches(radius_of(pr), .2, .62)
    for lo, hi in (((-.04, -.04, 1.0), (.04, .04, 1.24)), ((-.11, -.04, 1.1), (.11, .04, 1.18))):   # padded cross
        c = P.box(lo, hi, TRIM(), bev=.03, seg=3)
        for q in c.data.polygons:
            q.use_smooth = True
        p.append(c)
    return p


def knight():
    pr = BASE + [(.23, .29), (.18, .44), (.16, .5), (0, .5)]
    p = [body(pr), collar(.47, .215)] + foot()
    p += stitches(radius_of(pr), .2, .44, 4, 4)
    soft = []
    n0 = len(p)
    # neck rising and leaning back, then the head reaching forward (-y), all stuffed rounded blocks
    neck = P.box((-.12, -.11, 0), (.12, .13, .44), LIN(), bev=.09, seg=4)
    neck.data.transform(Matrix.Translation((0, .02, .46)) @ Matrix.Rotation(math.radians(12), 4, 'X'))
    head = P.box((-.105, -.34, -.09), (.105, .07, .1), LIN(), bev=.085, seg=4)
    head.data.transform(Matrix.Translation((0, .0, .9)) @ Matrix.Rotation(math.radians(-18), 4, 'X'))
    soft += [neck, head]
    for s in (-1, 1):                                       # ears
        e = P.lathe([(0, 0), (.04, 0), (.03, .07), (0, .12)], LIN(), segs=10)
        e.data.transform(Matrix.Translation((s * .065, .04, .99)) @ Matrix.Rotation(math.radians(-12), 4, 'X'))
        soft.append(e)
        eye = P.box((-.005, -.03, -.008), (.005, .03, .008), TRIM(), bev=.004, seg=1)      # sewn eye seam
        eye.data.transform(Matrix.Translation((s * .107, -.12, .95)) @ Matrix.Rotation(math.radians(-18), 4, 'X'))
        p.append(eye)
    for o in soft:
        for q in o.data.polygons:
            q.use_smooth = True
    p += soft
    for i in range(9):                                      # the mane: stuffed tufts down the back of the neck
        t = i / 8
        y, z = .14 + .05 * t - .02, 1.02 - .5 * t
        m = P.lathe(arc(0, .045, -90, 90, 5), TRIM(), segs=10)
        m.data.transform(Matrix.Translation((0, y + .02 * math.sin(t * 3), z)) @ Matrix.Diagonal((.8, 1., 1.2, 1.)))
        p.append(m)
    # the head looks to the side (-x), as a knight in a chess diagram: from the game camera (behind, looking north)
    # its horse profile reads; a head facing the camera was a blob (render check)
    P.xf(p[n0:], Matrix.Rotation(-math.pi / 2, 4, 'Z'))
    return p


DOLLS = {"Pawn": pawn, "Bishop": bishop, "Knight": knight, "Rook": rook, "Queen": queen, "King": king}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = {}
    for name, make in DOLLS.items():
        o = P.join(make(), "J_Doll_" + name)
        built[name] = o
        zs = [v.co.z for v in o.data.vertices]
        print("DOLL", name, P.tris(o), "tris", "height %.2f" % max(zs), sorted({m.name for m in o.data.materials}))
        if "--export" in ARGS:
            P.export(o, "J_Doll_" + name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(MID, "J_Dolls_v1.blend"))
    if "--render" in ARGS:
        render(built)


def render(built):
    """The six in a row, 3/4 from the front, tinted as the blue station sees them (charcoal + orange) and one cream set."""
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1800, 700
    sc.view_settings.view_transform = 'AgX'
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (.95, .9, .82, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = .6
    sc.world = w
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(45), 0, math.radians(-35)))
    bpy.context.object.data.energy = 3.5
    tint = {"PR_Mid_J_Linen": (.09, .075, .07, 1), "PR_Mid_J_DollTrim": (.87, .25, .03, 1)}
    for m in bpy.data.materials:
        if m.name in tint and m.use_nodes:
            b = m.node_tree.nodes["Principled BSDF"]
            img = next((n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE'), None)
            if img is not None:
                mix = m.node_tree.nodes.new("ShaderNodeMix")
                mix.data_type = 'RGBA'
                mix.blend_type = 'MULTIPLY'
                mix.inputs["Factor"].default_value = 1.
                m.node_tree.links.new(img.outputs["Color"], mix.inputs[6])
                mix.inputs[7].default_value = tint[m.name]
                m.node_tree.links.new(mix.outputs[2], b.inputs["Base Color"])
            else:
                b.inputs["Base Color"].default_value = tint[m.name]
    x = 0.
    for name, o in built.items():
        o.location = (x, 0, 0)
        x += .85
    cd = bpy.data.cameras.new("cam")
    cd.lens = 50
    cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    c = Vector((x / 2 - .42, 0, .6))
    cam.location = c + Vector((-1.3, -7.2, 1.9))
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(HERE, "render_j_dolls.png")
    bpy.ops.render.render(write_still=True)


main()
