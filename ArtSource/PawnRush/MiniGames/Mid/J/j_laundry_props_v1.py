# J Palace Laundry props (R509), modelled here in Blender (user, 11-08: "소품을 Blender로 직접"), after the GPT sheet
# Docs/PawnRush/ConceptArt/R509_PalaceLaundry/AssetRefs/J_assets_05_props.png: wicker laundry basket with folded linen,
# stack of folded sheets, washboard, soap crate, drying rack with towels, wooden bucket, washhouse bench.
# The modelling helpers are E's (Mid/e_props_v1.py: bevelled boxes, cylinders, turned parts, rings, grain UVs), imported
# and not edited. Stylised like the Tripo sets: chunky rounded bevels.
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python j_laundry_props_v1.py -- [--render] [--export]
#
# Materials: wood = R509 PR_Mid_OakStaves (1 m tile, grain along u); linen = R509 PR_Mid_DollLinen (as PR_Mid_J_Linen in
# Unity); iron = E's PR_Mid_PropIron; the rest plain colours PR_Mid_J_Prop* (j_install_unity.py makes them).
# Output Export/J_PropsEnv.fbx: all props placed in the section frame (Blender x = section x, y = section z, z = up),
# off the pawns' ways: the entry band's outer corners (|x| > 13, z 0.6..3) and the rear corridor's outer ends.
import bpy, math, os, random, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
MID = os.path.dirname(HERE)
sys.path.insert(0, MID)
import e_props_v1 as P   # noqa: E402
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

COLORS = {"PR_Mid_J_PropWicker": (.42, .24, .09), "PR_Mid_J_PropZinc": (.45, .47, .50),
          "PR_Mid_J_PropSoap": (.80, .72, .52), "PR_Mid_J_PropBristle": (.55, .40, .22)}


def OAK(): return P.mat("PR_Mid_OakStaves", tex="PR_Mid_OakStaves_BaseColor.png", rough=.6)
def LIN(): return P.mat("PR_Mid_J_Linen", tex="PR_Mid_DollLinen_BaseColor.png", rough=.9)
def IRON(): return P.C("PR_Mid_PropIron")
def PC(name): return P.mat(name, COLORS[name], rough=.35 if "Zinc" in name else .8, metal=.6 if "Zinc" in name else 0.)


def hoop(R, z, r, m, segs=40):
    """A horizontal ring (axis z) of radius R at height z."""
    t = P.torus(R, r, m, segs=segs, rsegs=8)
    t.data.transform(Matrix.Translation((0, 0, z)) @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    return t


def folded(lo, hi, rng, soft=.035):
    """A folded linen piece: a soft rounded slab with a slight random turn."""
    o = P.box(lo, hi, LIN(), bev=soft, seg=3)
    c = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, 0))
    o.data.transform(Matrix.Translation(c) @ Matrix.Rotation(math.radians(rng.uniform(-6, 6)), 4, 'Z') @ Matrix.Translation(-c))
    return o


# ------------------------------------------------------------------ props (origin floor centre, front -y)
def basket():
    rng = random.Random(1)
    W, H, R = PC("PR_Mid_J_PropWicker"), .40, .36
    p = [P.lathe([(0, .0), (R * .86, .0), (R * .93, .03), (R, H), (R * .92, H), (R * .86, .05), (0, .05)], W, segs=28)]
    for i in range(8):                                    # woven bands
        z = .05 + i * (H - .07) / 7
        p.append(hoop(R * (.87 + .13 * z / H) + .004, z, .016, W))
    p.append(hoop(R + .01, H, .03, W))                    # rolled rim
    for s in (-1, 1):                                     # two handles over the rim at the ends
        h = P.torus(.11, .02, W, segs=16, rsegs=6, arc=math.pi)
        h.data.transform(Matrix.Translation((s * (R + .02), 0, H - .02)) @ Matrix.Rotation(math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.pi, 4, 'Y'))
        p.append(h)
    z = H - .1
    for i in range(3):                                    # folded linen heaped in it
        w, d = .5 - i * .05, .36 - i * .03
        p.append(folded((-w / 2, -d / 2, z), (w / 2, d / 2, z + .07), rng))
        z += .065
    P.xf(p, Matrix.Diagonal((1.25, 1, 1, 1)))             # oval
    return p


def sheets():
    rng = random.Random(2)
    p, z = [], 0.
    for i in range(5):
        w, d, t = .62 - i * .015, .44, .075
        dx, dy = rng.uniform(-.025, .025), rng.uniform(-.02, .02)
        p.append(folded((-w / 2 + dx, -d / 2 + dy, z), (w / 2 + dx, d / 2 + dy, z + t), rng, .03))
        z += t - .006
    return p


def washboard():
    oak, zinc = OAK(), PC("PR_Mid_J_PropZinc")
    p = []
    for s in (-1, 1):
        p.append(P.box((s * .27 - .035, -.03, 0), (s * .27 + .035, .03, .95), oak, bev=.01, grain=2, seed=s + 3))
    p.append(P.box((-.27, -.035, .82), (.27, .035, .92), oak, bev=.012, grain=0, seed=5))       # top rail
    p.append(P.box((-.24, -.03, .1), (.24, .03, .17), oak, bev=.01, grain=0, seed=6))            # bottom rail
    p.append(P.box((-.235, -.008, .17), (.235, .008, .8), zinc, bev=.004))                      # scrub panel
    for i in range(14):                                                                          # ribs
        z = .2 + i * .043
        p.append(P.cyl((-.225, -.012, z), (.225, -.012, z), .012, zinc, segs=8, grain=False))
    P.xf(p, Matrix.Rotation(math.radians(-14), 4, 'X') @ Matrix.Translation((0, .0, 0)))         # leans back to a wall
    return p


def soap_crate():
    rng = random.Random(4)
    oak, iron, soap = OAK(), IRON(), PC("PR_Mid_J_PropSoap")
    X, Y, H = .3, .22, .3
    p = [P.box((-X, -Y, 0), (X, Y, .03), oak, bev=.006, grain=0, seed=1)]
    def span(sign, half, inner):
        # from the face at sign * half, `inner` metres into the crate
        return (half - inner, half) if sign > 0 else (-half, -half + inner)
    for sx in (-1, 1):
        for sy in (-1, 1):
            (x0, x1), (y0, y1) = span(sx, X, .03), span(sy, Y, .03)
            p.append(P.box((x0, y0, 0), (x1, y1, H), oak, bev=.006, grain=2))           # corner post
            # an iron corner plate on the front / back face, a little proud of the slats
            px0, px1 = span(sx, X + .006, .086)
            py0, py1 = (Y + .022, Y + .028) if sy > 0 else (-Y - .028, -Y - .022)
            p.append(P.box((px0, py0, .02), (px1, py1, .28), iron, bev=.002))
    for z0 in (.05, .17):                                                  # side slats with gaps
        for sy in (-1, 1):
            p.append(P.box((-X, sy * Y - (.022 if sy > 0 else 0), z0), (X, sy * Y + (0 if sy > 0 else .022), z0 + .09), oak, bev=.006, grain=0, seed=int(z0 * 100) + sy))
        for sx in (-1, 1):
            p.append(P.box((sx * X - (.022 if sx > 0 else 0), -Y, z0), (sx * X + (0 if sx > 0 else .022), Y, z0 + .09), oak, bev=.006, grain=1, seed=int(z0 * 100) + sx + 7))
    for i in range(3):                                                     # soap bars, two layers
        for j in range(2):
            for k in range(2 if i < 2 else 1):
                x, y = -.18 + i * .18, -.09 + j * .18
                z = .03 + k * .085
                p.append(P.box((x - .075, y - .075, z), (x + .075, y + .075, z + .08), soap, bev=.02, seg=2))
    return p


def drying_rack():
    rng = random.Random(5)
    oak = OAK()
    L, H, D = 1.3, 1.25, .7
    p = []
    for sx in (-1, 1):                                    # an A-frame at each end
        x = sx * L / 2
        for sy in (-1, 1):
            p.append(P.cyl((x, sy * D / 2, 0), (x, sy * .06, H), .03, oak, segs=10))
        p.append(P.cyl((x, -D * .36, .35), (x, D * .36, .35), .022, oak, segs=8))
    bars = [(0, H - .02), (-D * .28, .75), (D * .28, .75)]
    for y, z in bars:
        p.append(P.cyl((-L / 2 - .05, y, z), (L / 2 + .05, y, z), .022, oak, segs=10))
    for i, (y, z) in enumerate(bars):                     # towels folded tight over each bar (they do not sway)
        w, drop = L - .25 - i * .1, .42 - i * .08
        t = .012
        p.append(P.box((-w / 2, y - .03, z - .005), (w / 2, y + .03, z + .03), LIN(), bev=.012))
        for y0, y1 in ((y + .03, y + .03 + t), (y - .03 - t, y - .03)):      # the two hanging halves
            p.append(P.box((-w / 2, y0, z - drop), (w / 2, y1, z + .01), LIN(), bev=.006))
    return p


def bucket():
    oak, iron = OAK(), IRON()
    R0, R1, H = .2, .25, .42
    p = [P.lathe([(0, 0), (R0, 0), (R1, H), (R1 - .025, H), (R0 - .02, .04), (0, .04)], oak, segs=24)]
    for z, r in ((.08, R0 + (R1 - R0) * .08 / H), (.33, R0 + (R1 - R0) * .33 / H)):
        p.append(hoop(r + .006, z, .014, iron, segs=32))
    h = P.torus(R1 + .02, .012, iron, segs=24, rsegs=6, arc=math.pi)        # bail handle over the top
    h.data.transform(Matrix.Translation((0, 0, H)) @ Matrix.Rotation(math.pi, 4, 'Y'))
    p.append(h)
    p.append(P.cyl((-.09, 0, H + R1 + .02), (.09, 0, H + R1 + .02), .025, oak, segs=10))   # wooden grip
    return p


def bench():
    rng = random.Random(7)
    oak, bristle = OAK(), PC("PR_Mid_J_PropBristle")
    L, D, Hs = 1.6, .4, .45
    p = [P.box((-L / 2, -D / 2, Hs - .07), (L / 2, D / 2, Hs), oak, bev=.018, grain=0, seed=1)]
    for s in (-1, 1):
        p.append(P.box((s * (L / 2 - .2) - .045, -D / 2 + .04, 0), (s * (L / 2 - .2) + .045, D / 2 - .04, Hs - .07), oak, bev=.012, grain=2, seed=s + 2))
    p.append(P.box((-L / 2 + .25, -.04, .12), (L / 2 - .25, .04, .2), oak, bev=.01, grain=0, seed=4))   # stretcher
    p.append(folded((-.62, -.15, Hs), (-.12, .15, Hs + .07), rng))                             # a folded towel
    p.append(P.box((.25, -.05, Hs + .02), (.5, .05, Hs + .065), oak, bev=.01, grain=0, seed=6))  # scrubbing brush
    p.append(P.box((.27, -.04, Hs), (.48, .04, Hs + .02), bristle, bev=.004))                      # its bristles
    return p


PROPS = {"Basket": basket, "Sheets": sheets, "Washboard": washboard, "SoapCrate": soap_crate,
         "DryingRack": drying_rack, "Bucket": bucket, "Bench": bench}

# placement (section frame): (prop, x, z, yaw degrees). Front (-y) turned to the room: yaw 180 against the entry wall
# (room face z ~0.63 with the tile dado), -s*90 against a side wall (face |x| ~21.95). Pawns come in at x +-6 and walk to
# the station ramps (x +-9.8..14.2, z 2.4..4) and leave the rear corridor through x +-12 -> x 0 at z 33.
PLACES = []
for s in (-1, 1):
    PLACES += [("Bench", s * 18.2, .9, 180), ("Basket", s * 15.6, 1.45, s * 15), ("Sheets", s * 21.55, 1.5, -s * 90),
               ("SoapCrate", s * 20.1, 2.9, s * 8), ("Bucket", s * 17.0, 2.7, 0), ("Washboard", s * 21.7, 2.15, -s * 90),
               ("DryingRack", s * 21.2, 32.8, -s * 90), ("Basket", s * 20.7, 35.0, s * 30),
               ("Bench", s * 18.0, 30.4, 180), ("Bucket", s * 16.2, 30.6, 0), ("Sheets", s * 19.4, 35.4, s * 5)]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    built = {}
    for name, make in PROPS.items():
        built[name] = P.join(make(), "J_Prop_" + name)
        print("PROP", name, P.tris(built[name]), "tris", sorted({m.name for m in built[name].data.materials}))
    env = []
    for name, x, z, yaw in PLACES:
        me = built[name].data.copy()
        me.transform(Matrix.Translation((x, z, 0.)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
        o = bpy.data.objects.new("placed", me)
        bpy.context.scene.collection.objects.link(o)
        env.append(o)
    props_env = P.join(env, "J_PropsEnv")
    # one slot per material (joined props repeat them)
    print("ENV", P.tris(props_env), "tris")
    if "--export" in ARGS:
        P.export(props_env, "J_PropsEnv")
    props_env.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(MID, "J_Props_Laundry_v1.blend"))
    if "--render" in ARGS:
        P.HERE = HERE                     # E's render writes render_e_prop_<name>.png into HERE
        P.render(built)
        for name in PROPS:
            src = os.path.join(HERE, "render_e_prop_%s.png" % name.lower())
            if os.path.exists(src):
                os.replace(src, os.path.join(HERE, "render_j_prop_%s.png" % name.lower()))


main()
