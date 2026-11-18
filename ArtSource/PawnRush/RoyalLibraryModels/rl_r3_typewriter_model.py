"""R3 typewriter zone (seoungtest R687, 2026-11-18): PLAN.md R3, blockout collection 04 Typewriter (Unity z -60 .. -8).

    blender -b --factory-startup --python rl_r3_typewriter_model.py -- [still <name> ...] [export]

Always builds every model and saves rl_r3_typewriter.blend. `still body|keys|signs|tables|all` writes a look-dev
preview (rl_lookdev.py on a scratch FBX, nothing written under Assets) to stills/r3/; `export` writes the FBXs with
rl_kit.export to Assets/Resources/RoyalLibrary/Models/Typewriter/ (never while Unity is in Play).

Shapes are the E5 models 승규 approved (rl_e5_props_model.py build_typewriter / build_round_key, e5 hint sign, e5
course table kit), read only: their helpers are exec'd here, segment counts capped (CAPS) to keep the zone at ~100k
triangles, then re-laid on the blockout (prefab) sizes:
  RlTypewriter      body + inner passage, static (course root). E5 body re-fitted: the cheeks beside the keys stay
                    under 1.6 (their collider top 1.2), the key well floor is flat at the gap bed's height (-0.42, the
                    E5 key tiers are gone: a pawn in a gap stands on the bed), the plinth sits on the furniture base
                    (top -0.8), 17 type bars (blockout count) fan above a black nameplate behind the window (the game
                    prints "MEMORY TYPEWRITER" at `Carriage branding`, y -31.2 z 9), a parchment plate hangs in the
                    tunnel mouth under `Printed order text` (y -36.92 z 4.8), ribbon reels under the spool covers and a
                    crimson ribbon across the type guide, and the passage runs on as a lower rear case to y -10 (the
                    blockout's walls and roof reach there).
  RlTwKey           one key cap in `Key 0-0`'s frame, hung on all 23 `Key r-c` (they move down 0.14 when pressed): brass
                    cup round the blockout's ivory letter disk, which stays drawn (the game writes the letter on it and
                    flashes it red), lip top 0.19 under the letter (0.20).
  RlTwKeyStems      23 static stems: floor plate, rod into the cap, spring (rows 1-4) and collar under the pressed cap.
  RlTwClueSign      the E5 hint sign in `Memory clue 1`'s frame, hung on the 5 `Memory clue n` (glyph 0.1 in front of
                    the field); base narrowed to sit inside the 18 m tables' rims.
  RlTwTableFront / RlTwTableExit / RlTwFurnitureBase   the course table kit at 18 x 9, 16 x 19, 25 x 24 (top -0.8).
Blender = blockout frame (x, y, z = Unity x, z, y). Slots: rl_kit 8 (enamel -> ink, ivory/paper -> parchment).
"""
import math, os, subprocess, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
E5_MODEL = os.path.join(HERE, "rl_e5_props_model.py")

# E5 helpers (prism, lathe, tube, box, chaikin, offset_poly, rrect, M, begin_model ...) into this module
# (it also sets HERE / STILLS / BLEND for itself: ours are set after it)
exec(open(E5_MODEL, encoding="utf-8").read(), globals())

HERE = os.path.dirname(os.path.abspath(__file__))
E5_TABLES = os.path.join(HERE, "e5", "rl_e5_06_07_tables.py")
E5_SIGN = os.path.join(HERE, "e5", "rl_e5_08_09_sign_seal.py")
BLEND = os.path.join(HERE, "rl_r3_typewriter.blend")
STILLS = os.path.join(HERE, "stills", "r3")
SCRATCH = os.path.join(os.environ.get("TEMP", HERE), "rl_r3_preview")
BLENDER = bpy.app.binary_path
ZONE = "Typewriter"

SLOT_OF = {"E5_BlackEnamel": "ink", "E5_Rubber": "ink", "E5_DarkSteel": "ink", "E5_Brass": "brass",
           "E5_BrassDark": "brass", "E5_BrassSatin": "brass", "E5_Ivory": "parchment", "E5_Paper": "parchment",
           "E5_Walnut": "walnut", "E5_WalnutTop": "walnut", "E5_RedLeather": "leather", "E5_RedWax": "leather"}
PALETTE["R3_Ivory"] = dict(rgb=(0.79, 0.65, 0.44), rough=0.4)
SLOT_OF["R3_Ivory"] = "ivory"           # rl_kit's ivory slot (R6, 11-18): the key faces
PALETTE["E5_RedLeather"] =PALETTE.get("E5_RedLeather", dict(rgb=(0.105, 0.026, 0.012), rough=0.5))


# ------------------------------------------------------------------ segment caps (face budget)
def cap_helpers(ns, big=24, small=12, bev=2, tube_res=4, tube_bres=1):
    """Wrap a namespace's lathe / bevel / tube so the E5 builders make fewer segments (looked up at call time)."""
    lathe0, bevel0, tube0 = ns["lathe"], ns["bevel"], ns["tube"]

    def lathe(name, prof, mats, loc=(0, 0, 0), axis="Z", seg=48, smooth=30):
        return lathe0(name, prof, mats, loc, axis, big if seg > 30 else min(seg, small), smooth)

    def bevel(o, w, seg=3, angle=40, limit="ANGLE", profile=0.5):
        return bevel0(o, w, min(seg, bev), angle, limit, profile)

    def tube(name, pts, r, mats, closed=False, kind="POLY", res=10, radii=None, bres=4):
        return tube0(name, pts, r, mats, closed, kind, min(res, tube_res), radii, min(bres, tube_bres))
    ns["lathe"], ns["bevel"], ns["tube"] = lathe, bevel, tube


cap_helpers(globals())

# =================================================================== body
# Local frame = E5's: origin at world (0, -38, 0) (keys row 0 at local y -11.6), front -Y.
BODY_Y = -38.0
WELL = -0.42                 # key well floor = top of the gap bed collider (-0.40)
CHEEK_TOP = 1.6              # cheek collider top 1.2 + 0.4
REAR_END = 28.0              # passage walls / roof run to world y -10
TB_LOW, TB_HIGH = 9.75, 11.15

SIDE = chaikin([(-14.6, WELL), (-14.6, 0.80), (-14.35, 1.16), (-13.7, 1.34), (-1.4, 1.42), (-0.6, 1.55),
                (-0.25, 2.4), (0.15, 4.4), (0.5, 7.0), (0.9, 10.0), (1.3, 11.9), (2.2, 12.9), (3.8, 13.3),
                (12.4, 13.4), (13.9, 13.1), (14.3, 12.0), (14.3, WELL)], 2)


def build_body():
    begin_model("R3 body")
    EN, BR, BD = M("E5_BlackEnamel"), M("E5_Brass"), M("E5_BrassDark")
    IV, PA, RU = M("E5_Ivory"), M("E5_Paper"), M("E5_Rubber")
    LE = M("E5_RedLeather")

    # --- plinth on the furniture base (top -0.8), brass band; its top is the key well floor
    prism("TW plinth", rrect(-12.0, 12.0, -14.4, 14.3, 1.4, 4), "XY", -0.80, WELL, EN, (0.12, 2))
    tube("TW plinth band", [(x, y, WELL - 0.05) for x, y in rrect(-12.03, 12.03, -14.43, 14.33, 1.4, 4)],
         0.07, BR, closed=True)
    # front plate of the well under the approach table's brass lip
    box("TW well front plate", -8.0, 8.0, -12.84, -12.7, WELL, -0.06, BR, (0.03, 1))
    # thin brass row lines on the well floor between the key rows (flat, 2 cm)
    for r in range(4):
        y = -11.6 + 2.8 * r + 1.4
        box(f"TW well line {r}", -7.8, 7.8, y - 0.04, y + 0.04, WELL, WELL + 0.02, BD)

    # --- side housings: low cheeks beside the keys, tall body behind
    for sx in (-1, 1):
        a0, a1 = (8.0, 11.6) if sx > 0 else (-11.6, -8.0)
        prism(f"TW side housing {sx:+d}", SIDE, "YZ", a0, a1, EN, (0.32, 2, 40))
        face = 11.6 * sx
        panel = offset_poly(SIDE, 0.55)
        prism(f"TW side panel {sx:+d}", panel, "YZ", face - 0.05 * sx, face + 0.10 * sx, EN, (0.05, 1, 40))
        tube(f"TW side panel brass line {sx:+d}", [(face + 0.02 * sx, y, z) for y, z in offset_poly(SIDE, 0.75)],
             0.07, BR, closed=True)
        rail = sorted((y, z) for y, z in SIDE if -14.35 < y < -0.75 and z > 1.0)
        tube(f"TW cheek rail {sx:+d}", [(8.12 * sx, y, z - 0.08) for y, z in rail], 0.15, BR)
        lathe(f"TW cheek boss {sx:+d}", [(0, 0), (0.5, 0), (0.56, 0.06), (0.56, 0.15), (0.45, 0.2), (0, 0.22)],
              BR, (9.8 * sx, -14.6, 0.42), "Y-")

    # --- sill (top 1.6, walking), brass nosing, descent to the exit table
    box("TW inner sill", -8.0, 8.0, 1.0, 5.0, WELL, 1.6, EN, (0.06, 1))
    tube("TW sill nosing", [(-7.95, 1.04, 1.55), (7.95, 1.04, 1.55)], 0.06, BR)
    prism("TW interior descent", [(5.0, WELL), (5.0, 1.6), (11.0, -0.15), (11.0, WELL)], "YZ", -8.0, 8.0, EN)

    # --- front fascia with the arch and the window (E5)
    half = tw_arch_half()
    arch = half + [(-x, z) for x, z in reversed(half[:-1])]
    FT = 12.35
    fascia = [(-8.05, FT), (-8.05, 4.7)] + list(reversed(arch)) + [(8.05, 4.7), (8.05, FT)]
    fa = prism("TW front fascia", fascia, "XZ", 1.5, 2.9, EN)
    cut = prism("TW window cutter", TW_WINDOW, "XZ", 1.0, 3.4, EN, smooth=0)
    cut.display_type = "WIRE"
    cut.hide_render = True
    cut["cutter"] = True
    b = fa.modifiers.new("Window", "BOOLEAN")
    b.operation, b.object, b.solver = "DIFFERENCE", cut, "EXACT"
    bevel(fa, 0.14, 2, 40)
    wnormal(fa)
    tube("TW arch brass trim", [(x, 1.47, z - 0.02) for x, z in arch], 0.12, BR)
    for sx in (-1, 1):
        tube(f"TW arch jamb trim {sx:+d}", [(7.92 * sx, 1.47, 4.7), (7.92 * sx, 1.47, 1.65)], 0.12, BR)
    tube("TW window brass trim", [(x, 1.44, z) for x, z in offset_poly(TW_WINDOW, -0.1)], 0.13, BR, closed=True)
    tube("TW fascia top trim", [(-8.0, 1.55, FT - 0.08), (8.0, 1.55, FT - 0.08)], 0.09, BR)

    # --- instruction plate in the tunnel mouth (game text at world y -36.92 z 4.8 = local 1.08)
    box("TW order plate", -5.0, 5.0, 1.15, 1.3, 4.05, 5.55, PA, (0.03, 1))
    tube("TW order plate frame", [(-5.05, 1.13, 4.0), (5.05, 1.13, 4.0), (5.05, 1.13, 5.6), (-5.05, 1.13, 5.6)],
         0.05, BR, closed=True)
    for sx in (-1, 1):
        tube(f"TW order plate hanger {sx:+d}", [(4.0 * sx, 1.22, 5.55), (4.0 * sx, 1.22, 6.86), (4.0 * sx, 1.47, 6.86)],
             0.06, BR)

    # --- hood: tunnel roof, back block, top deck
    box("TW tunnel roof", -8.05, 8.05, 2.2, 9.6, 6.8, 7.9, EN, (0.08, 1))
    box("TW hood back block", -8.05, 8.05, 9.6, 14.3, 6.8, 12.9, EN, (0.3, 2))
    box("TW hood top deck", -8.05, 8.05, 2.2, 9.7, 11.55, 12.9, EN, (0.3, 2))

    # --- nameplate behind the window (game prints white text at local y 6.8, z 9.0)
    box("TW nameplate", -5.0, 5.0, 6.85, 7.0, 8.4, 9.6, EN, (0.03, 1))
    tube("TW nameplate frame", [(-5.06, 6.84, 8.34), (5.06, 6.84, 8.34), (5.06, 6.84, 9.66), (-5.06, 6.84, 9.66)],
         0.06, BR, closed=True)
    for sx in (-1, 1):
        lathe(f"TW nameplate post {sx:+d}", [(0, 7.9), (0.2, 7.9), (0.2, 7.98), (0.11, 8.04), (0.11, 8.4), (0, 8.4)],
              BR, (4.2 * sx, 6.95, 0), seg=12)

    # --- type basket: 17 bars fanning above the nameplate, rest ring, segment, guide, ribbon
    bars = bmesh.new()
    C = Vector((0, 8.6))
    N = 17
    for i in range(N):
        a = math.radians(-76 + 152 * i / (N - 1))
        d = Vector((math.sin(a), -math.cos(a)))
        bb, tt = C + d * 5.6, C + d * 1.7
        B, T = Vector((bb.x, bb.y, TB_LOW)), Vector((tt.x, tt.y, TB_HIGH))
        ax = T - B
        zax = ax.normalized()
        xax = Vector((-d.y, d.x, 0))
        yax = zax.cross(xax).normalized()
        mat = Matrix((xax, yax, zax)).transposed().to_4x4()
        mat.translation = (B + T) * 0.5
        ret = bmesh.ops.create_cube(bars, size=1.0)
        bmesh.ops.scale(bars, vec=(0.26, 0.12, ax.length), verts=ret["verts"])
        bmesh.ops.transform(bars, matrix=mat, verts=ret["verts"])
        ret = bmesh.ops.create_cube(bars, size=1.0)
        bmesh.ops.scale(bars, vec=(0.34, 0.22, 0.32), verts=ret["verts"])
        m2 = mat.copy()
        m2.translation = T - zax * 0.12
        bmesh.ops.transform(bars, matrix=m2, verts=ret["verts"])
    o = mesh_obj("TW type bars", bars, BR, smooth=30)
    bevel(o, 0.03, 1, 60)
    tube("TW type bar rest", [(C.x + 5.6 * math.sin(math.radians(a)), C.y - 5.6 * math.cos(math.radians(a)), TB_LOW - 0.08)
                              for a in range(-80, 81, 8)], 0.12, BR)
    tube("TW type segment", [(C.x + 1.75 * math.sin(math.radians(a)), C.y - 1.75 * math.cos(math.radians(a)), TB_HIGH + 0.1)
                             for a in range(-90, 91, 10)], 0.2, BR)
    box("TW type guide", -0.45, 0.45, 6.6, 6.9, 10.7, 11.5, BR, (0.04, 1))
    box("TW ribbon vibrator", -1.2, 1.2, 6.9, 7.05, 10.5, 11.5, EN, (0.03, 1))
    box("TW ribbon", -2.4, 2.4, 6.86, 6.9, 10.9, 11.15, LE)

    # --- ribbon reels on the deck: brass hub, crimson ribbon coil, brass cover
    for sx in (-1, 1):
        x = 5.4 * sx
        lathe(f"TW spool hub {sx:+d}", [(0, 12.9), (1.5, 12.9), (1.52, 12.94), (1.5, 12.98), (0, 12.98)], BR, (x, 4.6, 0))
        lathe(f"TW spool ribbon {sx:+d}", [(0, 12.98), (1.32, 12.98), (1.36, 13.03), (1.36, 13.23), (1.32, 13.28),
                                           (0, 13.28)], LE, (x, 4.6, 0))
        lathe(f"TW spool cover {sx:+d}", [(0, 13.28), (1.45, 13.28), (1.5, 13.38), (1.42, 13.5), (1.1, 13.6),
                                          (0.45, 13.66), (0.42, 13.78), (0.28, 13.86), (0, 13.86)], BR, (x, 4.6, 0))

    # --- carriage (E5, on the hood), lifted by TW_CARRIAGE_DZ
    before = set(Ctx.col.objects)
    CY, CZ, CR = 9.0, 9.85, 1.22
    for sx in (-1, 1):
        a0, a1 = (10.15, 10.6) if sx > 0 else (-10.6, -10.15)
        prism(f"TW carriage end plate {sx:+d}", rrect(6.7, 11.6, 8.55, 11.3, 0.9, 4), "YZ", a0, a1, EN, (0.08, 1))
        lathe(f"TW platen bearing {sx:+d}", [(0, 0), (0.75, 0), (0.8, 0.05), (0.8, 0.2), (0.6, 0.28), (0, 0.3)],
              BR, (10.6 * sx, CY, CZ), "X+" if sx > 0 else "X-")
        knob = [(0, 0.0), (0.42, 0.0), (0.42, 0.32), (1.05, 0.36), (1.28, 0.46), (1.32, 0.56),
                (1.62, 0.62), (1.70, 0.72), (1.70, 0.80, 0.025), (1.70, 1.36, 0.025), (1.70, 1.42),
                (1.58, 1.50), (1.40, 1.54), (1.46, 1.60), (1.50, 1.70), (1.50, 1.74, 0.02),
                (1.50, 1.96, 0.02), (1.44, 2.02), (1.20, 2.08), (0.55, 2.12), (0.30, 2.22), (0, 2.24)]
        lathe(f"TW platen knob {sx:+d}", knob, BR, (10.85 * sx, CY, CZ), "X+" if sx > 0 else "X-", seg=40)
    lathe("TW platen roller", [(0, -10.15), (CR - 0.08, -10.15), (CR, -10.07), (CR, 10.07),
                               (CR - 0.08, 10.15), (0, 10.15)], RU, (0, CY, CZ), "X+", seg=40)
    for sx in (-1, 1):
        lathe(f"TW platen end flange {sx:+d}", [(0, 0), (CR + 0.12, 0), (CR + 0.15, 0.06), (CR + 0.12, 0.2), (0, 0.2)],
              BR, (9.95 * sx, CY, CZ), "X+" if sx > 0 else "X-")
    tube("TW carriage rail front", [(-11.2, 6.55, 8.88), (11.2, 6.55, 8.88)], 0.2, BR)
    tube("TW carriage rail back", [(-11.2, 11.95, 8.95), (11.2, 11.95, 8.95)], 0.24, BR)
    tube("TW paper support rod", [(-10.2, 10.75, 11.55), (10.2, 10.75, 11.55)], 0.13, BR)
    tube("TW paper bail", [(-10.1, 7.55, 10.8), (10.1, 7.55, 10.8)], 0.11, BR)
    for x in (-3.2, 3.2):
        lathe(f"TW bail roller {x:+.0f}", [(0, -0.35), (0.24, -0.35), (0.26, -0.3), (0.26, 0.3), (0.24, 0.35), (0, 0.35)],
              RU, (x, 7.55, 10.8), "X+", seg=12)
    box("TW card holder", -7.6, 7.6, 8.2, 8.42, 11.05, 11.45, BR, (0.05, 1))
    paper = [(CY + (CR + 0.03) * -math.sin(math.radians(a)), CZ + (CR + 0.03) * math.cos(math.radians(a)))
             for a in (120, 90, 60, 35, 15)]
    paper += [(8.62, 11.6), (8.6, 12.4), (8.7, 13.3), (8.95, 14.2), (9.3, 15.0), (9.8, 15.6),
              (10.4, 16.0), (10.9, 16.12)]
    surface_strip("TW paper", paper, -6.8, 6.8, PA, nx=8, thick=0.07)
    lev = [(-10.45, 8.0, 11.0), (-10.9, 6.8, 11.55), (-11.8, 4.9, 11.45), (-12.9, 3.0, 10.6),
           (-13.55, 1.7, 9.4), (-13.75, 1.2, 8.6)]
    tube("TW return lever", lev, 0.24, BR, kind="BEZIER", res=6, radii=[1.1, 1.0, 0.95, 0.9, 0.85, 0.8])
    lathe("TW return lever ball", [(0, -0.5)] + [(0.5 * math.sin(math.radians(a)), -0.5 * math.cos(math.radians(a)))
                                                 for a in range(30, 180, 30)] + [(0, 0.5)], BR, (-13.8, 1.05, 8.3), seg=16)
    lathe("TW return lever pivot", [(0, 0), (0.5, 0), (0.55, 0.06), (0.55, 0.2), (0.4, 0.28), (0, 0.3)], BR,
          (-10.6, 8.0, 11.0), "X-", seg=16)
    lathe("TW line space wheel", [(0, 0), (0.75, 0), (0.75, 0.25, 0.04), (0.6, 0.32), (0, 0.34)], BR,
          (10.6, 7.3, 10.6), "X+", seg=24)
    for o in Ctx.col.objects:
        if o not in before:
            o.location.z += TW_CARRIAGE_DZ

    # --- rear case: the passage runs on over the exit table to world y -10 (blockout walls x 8 .. 8.8, roof 6.8)
    for sx in (-1, 1):
        a0, a1 = sorted((8.0 * sx, 9.5 * sx))
        prism(f"TW rear wall {sx:+d}", [(14.0, -1.4), (14.0, 7.9), (REAR_END, 7.9), (REAR_END, -1.4)], "YZ", a0, a1, EN,
              (0.18, 2, 40))
        tube(f"TW rear wall brass line {sx:+d}", [(9.52 * sx, y, z) for y, z in
                                                  rrect(14.9, REAR_END - 0.9, -0.6, 7.1, 0.5, 3)], 0.06, BR, closed=True)
        for y in (16.5, 21.15, 25.8):
            lathe(f"TW rear wall boss {sx:+d} {y:.0f}", [(0, 0), (0.3, 0), (0.34, 0.05), (0.3, 0.1), (0, 0.12)],
                  BR, (9.5 * sx, y, 3.2), "X+" if sx > 0 else "X-", seg=12)
    box("TW rear roof", -9.5, 9.5, 14.0, REAR_END, 6.8, 7.9, EN, (0.25, 2))
    tube("TW rear roof brass strip", [(0, 14.3, 7.93), (0, REAR_END - 0.4, 7.93)], 0.1, BR)
    mouth = [(-7.95, REAR_END + 0.05, -0.05), (-7.95, REAR_END + 0.05, 6.75), (7.95, REAR_END + 0.05, 6.75),
             (7.95, REAR_END + 0.05, -0.05)]
    tube("TW rear mouth trim", mouth, 0.12, BR)

    body_details(EN, BR, BD, LE, PA, M("E5_DarkSteel"))
    end_model((0, BODY_Y, 0))
    return Ctx.root


def rivet_head(name, loc, axis, mat, r=0.16):
    return lathe(name, [(0, 0), (r, 0), (r, 0.02), (r * 0.8, 0.07), (r * 0.4, 0.1), (0, 0.11)], mat, loc, axis, seg=8)


def body_details(EN, BR, BD, LE, PA, ST):
    """Second pass (승규 11-18 "좀 더 디테일 하게"): what a pawn sees walking in and the faces round the machine."""
    # --- tunnel ceiling: key linkage (13 flat brass levers) on two cross shafts, under the roof (z >= 6.3)
    for i in range(13):
        x = -6.0 + i
        box(f"TW link lever {i}", x - 0.06, x + 0.06, 2.6, 13.6, 6.5, 6.62, BR)
        for y in (3.4, 8.6, 12.8):
            box(f"TW link hanger {i} {y:.0f}", x - 0.05, x + 0.05, y - 0.05, y + 0.05, 6.62, 6.8, BD)
    for y in (3.4, 8.6, 12.8):
        tube(f"TW link shaft {y:.0f}", [(-7.95, y, 6.42), (7.95, y, 6.42)], 0.08, ST)
    # --- tunnel walls inside: brass panel lines and rivets (housings and rear case, inner faces x +-8)
    for sx in (-1, 1):
        x = 7.96 * sx
        for y0, y1, z0 in ((1.6, 13.8, 2.2), (14.6, REAR_END - 0.6, 0.6)):
            tube(f"TW inner panel {sx:+d} {y0:.0f}", [(x, y, z) for y, z in rrect(y0, y1, z0, 6.1, 0.4, 2)],
                 0.045, BR, closed=True)
            n = max(2, int((y1 - y0) / 3.0))
            for k in range(n + 1):
                y = y0 + 0.4 + (y1 - y0 - 0.8) * k / n
                rivet_head(f"TW inner rivet {sx:+d} {y:.1f}", (x, y, 6.35), "X-" if sx > 0 else "X+", BR, 0.12)
    # rear case ceiling ribs
    for y in (16.5, 19.6, 22.7, 25.8):
        box(f"TW rear rib {y:.0f}", -7.95, 7.95, y - 0.12, y + 0.12, 6.6, 6.8, BR, (0.03, 1))
    # --- outside of the housings: oval brass medallion, rivets along the panel line
    for sx in (-1, 1):
        x = 11.68 * sx
        med = lathe(f"TW side medallion {sx:+d}", [(0, 0), (1.25, 0), (1.32, 0.05), (1.3, 0.12), (1.18, 0.15),
                                                   (1.1, 0.12), (0.95, 0.12), (0.9, 0.16), (0.3, 0.18), (0, 0.18)],
                    BR, (x, 6.5, 7.2), "X+" if sx > 0 else "X-", seg=28)
        med.scale = (1.35, 1.0, 1.0)                       # local x points down/up after the X turn -> tall oval
        for y, z in ((2.2, 3.0), (7.0, 3.0), (11.8, 3.0), (11.8, 11.0), (7.0, 12.3), (3.2, 11.4)):
            rivet_head(f"TW side rivet {sx:+d} {y:.0f} {z:.0f}", (11.7 * sx, y, z), "X+" if sx > 0 else "X-", BR)
        # brass toe plate on the cheek nose
        box(f"TW cheek toe {sx:+d}", *sorted((8.05 * sx, 11.55 * sx)), -14.75, -14.55, WELL + 0.05, 0.75, BR, (0.04, 1))
    # --- fascia: crest above the window, corner rivets
    lathe("TW fascia crest", [(0, 0), (0.42, 0), (0.46, 0.05), (0.42, 0.1), (0.3, 0.13), (0, 0.14)], BR,
          (0, 1.48, 11.85), "Y-", seg=20)
    for x in (-7.4, 7.4):
        for z in (5.6, 11.8):
            rivet_head(f"TW fascia rivet {x:+.0f} {z:.0f}", (x, 1.48, z), "Y-", BR)
    # --- hood: brass edge band round the deck, ribbon run to a centre guide
    tube("TW deck band", [(-8.0, 2.22, 12.84), (8.0, 2.22, 12.84)], 0.07, BR)
    for sx in (-1, 1):
        box(f"TW deck ribbon {sx:+d}", *sorted((4.05 * sx, 0.55 * sx)), 4.45, 4.75, 12.9, 12.93, LE)
    box("TW deck ribbon guide", -0.55, 0.55, 4.25, 4.95, 12.9, 13.25, BR, (0.05, 1))
    # felt pad under the type bar rest
    C = Vector((0, 8.6))
    tube("TW type bar felt", [(C.x + 5.45 * math.sin(math.radians(a)), C.y - 5.45 * math.cos(math.radians(a)),
                               TB_LOW - 0.3) for a in range(-80, 81, 10)], 0.16, LE)
    # --- carriage: brass scale with ticks in front of the paper, margin stops, ink lines on the paper
    dz = TW_CARRIAGE_DZ
    box("TW paper scale", -7.2, 7.2, 8.0, 8.12, 11.5 + dz, 11.7 + dz, BR, (0.02, 1))
    for i in range(29):
        x = -7.0 + 0.5 * i
        box(f"TW scale tick {i}", x - 0.025, x + 0.025, 7.97, 8.0, 11.55 + dz, (11.66 if i % 2 else 11.69) + dz, EN)
    for x in (-8.5, 8.0):
        box(f"TW margin stop {x:+.0f}", x - 0.25, x + 0.25, 11.8, 12.3, 9.1 + dz, 9.5 + dz, BR, (0.05, 1))
    paper_y = {12.0: 8.61, 12.45: 8.61, 12.9: 8.645, 13.35: 8.71, 13.8: 8.83}   # paper mid-surface (E5 strip)
    for i, (z, y) in enumerate(paper_y.items()):
        w = (5.6, 5.0, 5.4, 4.2, 5.2)[i]
        f = y - 0.035                                                            # front face (0.07 solidify)
        box(f"TW paper line {i}", -w, w - 0.6 * (i % 2), f - 0.015, f + 0.01, z + dz, z + 0.07 + dz, EN)


# =================================================================== keys
ROWS = (5, 4, 5, 4, 5)


def key_spots():
    for r, n in enumerate(ROWS):
        xs = [-6, -3, 0, 3, 6] if n == 5 else [-4.5, -1.5, 1.5, 4.5]
        for c, x in enumerate(xs):
            yield r, c, x, -49.6 + 2.8 * r, 0.02 + 0.32 * r       # zb: rim bottom; Key r-c origin = zb + 0.15


def build_key():
    """One cap in Key 0-0's place (exported in its frame). Origin (key) z = zb + 0.15; ivory disk 0.15 .. 0.18."""
    begin_model("R3 key")
    BR = M("E5_Brass")
    cup = [(0, -0.36), (0.66, -0.36), (0.74, -0.33), (1.05, -0.26), (1.30, -0.15), (1.38, -0.06),
           (1.40, 0.04), (1.40, 0.13), (1.37, 0.18), (1.31, 0.19), (1.25, 0.175), (1.225, 0.15), (1.22, 0.14), (0, 0.14)]
    lathe("Key cap", cup, BR, (0, 0, 0), seg=48)    # capped to 24
    _, _, x, y, zb = next(key_spots())
    end_model((x, y, zb + 0.15))
    return Ctx.root


DISK_Z = 0.165     # `Ivory letter disk` centre above its key (prefab: scale 2.42 x 0.012 x 2.42 -> r 1.21, 0.153 .. 0.177)


def build_key_face():
    """The ivory letter face (승규 via the R0 session 11-18: concept E4/E5 key tops, cream ivory dished inside the brass
    lip). It replaces the blockout disk's mesh on its own renderer (Hang.reskin), so TypewriterStairs' red flash on a
    wrong key (MaterialPropertyBlock on keyVisuals = these disks) still shows. Built in Key 0-0's disk frame (origin =
    disk centre): rim top +0.012 (key 0.177), dish centre -0.016, bottom -0.025 on the cap's floor (key 0.14); the
    game's letter floats at +0.035 (key 0.20), clear above the face."""
    begin_model("R3 key face")
    IV = M("R3_Ivory")
    lathe("Key face", [(0, -0.025), (1.21, -0.025), (1.215, 0.0), (1.21, 0.010), (1.19, 0.013), (1.15, 0.012),
                       (1.08, 0.006), (0.9, -0.004), (0.6, -0.011), (0.3, -0.015), (0, -0.016)], IV, seg=48)
    _, _, x, y, zb = next(key_spots())
    end_model((x, y, zb + 0.15 + DISK_Z))
    return Ctx.root


def spring(name, z0, z1, mat, coil=0.36, wire=0.055, pitch=0.2, steps_per_turn=10):
    turns = max(0.5, (z1 - z0 - 2 * wire) / pitch)
    bm = bmesh.new()
    vs = [bm.verts.new((coil + wire * math.cos(t), 0, wire * math.sin(t))) for t in (2 * math.pi * k / 5 for k in range(5))]
    for k in range(5):
        bm.edges.new((vs[k], vs[(k + 1) % 5]))
    o = mesh_obj(name, bm, mat, loc=(0, 0, z0 + wire), smooth=0)
    s = o.modifiers.new("Screw", "SCREW")
    s.axis, s.angle, s.iterations = "Z", 2 * math.pi * turns, 1
    s.steps = s.render_steps = max(6, int(round(steps_per_turn * turns)))
    s.screw_offset = pitch * turns
    s.use_smooth_shade = s.use_normal_calculate = True
    return o


def build_stems():
    begin_model("R3 key stems")
    BR, BD, ST = M("E5_Brass"), M("E5_BrassDark"), M("E5_DarkSteel")
    for r, c, x, y, zb in key_spots():
        tag = f"{r}-{c}"
        p = lathe(f"Stem plate {tag}", [(0, WELL), (0.55, WELL), (0.58, WELL + 0.03), (0.5, WELL + 0.07), (0.24, WELL + 0.09),
                                         (0, WELL + 0.09)], BR, seg=12)
        p.location = (x, y, 0)
        rod = lathe(f"Stem rod {tag}", [(0, WELL + 0.05), (0.14, WELL + 0.05), (0.14, zb - 0.05), (0, zb - 0.05)], ST, seg=8)
        rod.location = (x, y, 0)
        if r > 0:
            top = zb - 0.37                       # under the pressed cap (rest bottom zb - 0.21, travel 0.14)
            s = spring(f"Stem spring {tag}", WELL + 0.12, top - 0.04, BD)
            s.location = (x, y, s.location.z)
            col = lathe(f"Stem collar {tag}", [(0, top - 0.05), (0.42, top - 0.05), (0.44, top - 0.02), (0.42, top),
                                                (0, top)], BR, seg=12)
            col.location = (x, y, 0)
    end_model()
    return Ctx.root


# =================================================================== clue sign
def build_sign():
    ns = dict(globals())
    src = open(E5_SIGN, encoding="utf-8").read()
    src = src[src.index("OWN_BLEND ="):src.index("JOBS = {")]      # helpers + builders only (no main, no save)
    exec(src, ns)
    cap_helpers(ns, big=16, small=8)
    ns["build_hint_sign"](offset=(-8.0, -138.0, 0.0))                   # Memory clue 1: (-8, -138, 3.8)
    col = bpy.data.collections["E5_08_HintSign"]
    col.name = "R3 clue sign"
    for o in list(col.objects):
        if o.name.startswith(("HS base", "HS lower collar")):
            o.scale = (0.65, 0.65, 1.0)                                  # sits inside the 18 m tables' rims
        if o.type == "EMPTY" and o.parent is not None:
            bpy.data.objects.remove(o, do_unlink=True)
    return col


# =================================================================== tables
TABLES = {  # model: (W, D, centre y, top z)
    "RlTwTableFront": (18.0, 9.0, -55.5, 0.0),
    "RlTwTableExit": (16.0, 19.0, -17.5, 0.0),
    "RlTwFurnitureBase": (25.0, 24.0, -39.0, -0.8),
}


def book_side_caps(col):
    """Approach -60 meets the second book's desk at y -60 (R2 note 11-18: the kit's corner caps stand 0.27 m past the
    edge and 0.25 high, into the book's page corner and under its closing cover). On that side (-Y) the caps move in
    flush with the edge and down to top 0.12; their top plates and rivets go."""
    for o in list(col.objects):
        n = o.name
        if not n.endswith("-1"):
            continue
        if n.startswith(("CT corner plate", "CT corner rivet")):
            bpy.data.objects.remove(o, do_unlink=True)
        elif n.startswith("CT corner cap"):
            o.location.y += 0.27
            o.location.z -= 0.05


def build_tables():
    ns = {"__name__": "rl_r3_tables", "__file__": E5_TABLES}
    exec(open(E5_TABLES, encoding="utf-8").read(), ns)
    cap_helpers(ns, big=12, small=6, bev=1, tube_bres=0)
    out = {}
    for model, (W, D, cy, top) in TABLES.items():
        ns["begin_model"]("R3 " + model)
        ns["build_table"]("CT", W, D, ns["M"]("E5_Brass"), ns["M"]("E5_Walnut"), ns["top_material"]())
        if model == "RlTwTableFront":
            book_side_caps(ns["Ctx"].col)
        ns["end_model"]((0, cy, top))
        out[model] = ns["Ctx"].col
    return out


# =================================================================== bake to slots, join, UV
# 승규 11-18 "탁자를 살짝 광택이 나게": the tables' walnut is drawn in the library's varnished walnut
GLOSS = {"walnut": "varnished"}


def bake(col, model):
    """Every mesh / curve of `col` -> one mesh object `model` (modifiers applied, rl_kit slots, metre UVs)."""
    import rl_kit as k
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    parts = []
    for o in list(col.all_objects):
        if o.type not in {"MESH", "CURVE"} or o.get("cutter"):
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=False, depsgraph=dg)
        for i, m in enumerate(me.materials):
            name = m.name if m else ""
            if name not in SLOT_OF:
                raise RuntimeError("no slot for %s on %s" % (name, o.name))
            me.materials[i] = k.slot(GLOSS.get(SLOT_OF[name], SLOT_OF[name]) if model in TABLES else SLOT_OF[name])
        ob = bpy.data.objects.new(o.name + " baked", me)
        ob.matrix_world = o.matrix_world.copy()
        parts.append(ob)
    for o in list(col.all_objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for ob in parts:
        col.objects.link(ob)
    for ob in parts:
        k.surface_uvs(ob)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = model
    mats = ob.data.materials
    first = {}
    for i, m in enumerate(mats):
        first.setdefault(m.name, i)
    for p in ob.data.polygons:
        p.material_index = first[mats[p.material_index].name]
    for i in reversed(range(len(mats))):
        if first[mats[i].name] != i:
            mats.pop(index=i)
    col.name = model
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print("[r3] %-18s %7d tris  slots %s" % (model, tris, [m.name for m in mats]))
    return ob, tris


# =================================================================== previews
def preview_fbx(models, path):
    """World-frame FBX of some baked models (scratch, for rl_lookdev only)."""
    bpy.ops.object.select_all(action="DESELECT")
    for m in models:
        bpy.data.objects[m].select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y", bake_anim=False,
                             mesh_smooth_type="FACE", add_leaf_bones=False)


def place_keys_for_preview():
    """Copies of the key cap at all 23 keys (preview only, not exported)."""
    import rl_kit as k
    src = bpy.data.objects["RlTwKey"]
    _, _, x0, y0, z0 = next(key_spots())
    names = []
    face = bpy.data.objects["RlTwKeyFace"]          # the reskinned ivory letter disk, centre 0.165 above the key
    for r, c, x, y, zb in key_spots():
        o = src.copy()
        o.name = f"preview key {r}-{c}"
        o.location = (x, y, zb + 0.15)
        d = face.copy()
        d.name = f"preview face {r}-{c}"
        d.location = (x, y, zb + 0.15 + DISK_Z)
        for ob in (o, d):
            bpy.context.scene.collection.objects.link(ob)
            names.append(ob.name)
    floor = bpy.data.meshes.new("preview well")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1)
    bm.to_mesh(floor); bm.free()
    floor.materials.append(k.slot("ink"))
    f = bpy.data.objects.new("preview well", floor)
    f.location, f.scale = (0, -44.3, WELL), (8.0, 7.3, 1)
    bpy.context.scene.collection.objects.link(f)
    names.append(f.name)
    return names


def still(name, models, view, scale=0.0):
    fbx = os.path.join(SCRATCH, name + ".fbx")
    preview_fbx(models, fbx)
    os.makedirs(STILLS, exist_ok=True)
    out = os.path.join(STILLS, name + ".png")
    args = [BLENDER, "-b", "--factory-startup", "--python", os.path.join(HERE, "rl_lookdev.py"), "--", fbx, out,
            ",".join(str(v) for v in view)] + ([str(scale)] if scale else [])
    subprocess.run(args, check=True, stdout=subprocess.DEVNULL)
    print("[r3] still", out)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    built = {"RlTypewriter": build_body(), "RlTwKey": build_key(), "RlTwKeyStems": build_stems(),
             "RlTwKeyFace": build_key_face()}
    cols = {"RlTypewriter": bpy.data.collections["R3 body"], "RlTwKey": bpy.data.collections["R3 key"],
            "RlTwKeyStems": bpy.data.collections["R3 key stems"], "RlTwKeyFace": bpy.data.collections["R3 key face"],
            "RlTwClueSign": build_sign()}
    cols.update(build_tables())
    total = 0
    counts = {}
    for model, col in cols.items():
        _, t = bake(col, model)
        counts[model] = t
    for model, t in counts.items():
        total += t * (23 if model == "RlTwKey" else 5 if model == "RlTwClueSign" else 1)
    print("[r3] zone total (23 keys, 5 signs placed): %d tris" % total)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)

    if "still" in argv:
        want = set(argv[argv.index("still") + 1:]) - {"export"}
        if want & {"body", "all"}:
            still("body", ["RlTypewriter"], (-0.7, -1, 0.55))
            still("body_back", ["RlTypewriter"], (0.8, 1, 0.6))
            still("body_front", ["RlTypewriter"], (0.12, -1, 0.2), 30)
        if want & {"keys", "all"}:
            keys = place_keys_for_preview()
            still("keys", ["RlTwKeyStems"] + keys, (-0.45, -1, 0.75), 20)
            still("keys_low", ["RlTwKeyStems"] + keys, (-0.3, -1, 0.18), 20)
            still("key_single", ["RlTwKey"], (-0.6, -1, 0.5), 4)
            for n in keys:
                bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
        if want & {"signs", "all"}:
            still("clue_sign", ["RlTwClueSign"], (-0.5, -1, 0.3), 12)
        if want & {"tables", "all"}:
            still("tables", ["RlTwTableFront", "RlTwTableExit", "RlTwFurnitureBase"], (-0.8, -1, 0.6))
    if "export" in argv:
        import rl_kit as k
        frames = {"RlTwKey": "Key 0-0", "RlTwClueSign": "Memory clue 1", "RlTwKeyFace": "Ivory letter disk"}
        only = set(argv[argv.index("export") + 1:]) & set(cols)      # `export RlTwKeyFace` = just that one
        for model in cols:
            if not only or model in only:
                k.export(model, model, ZONE, frame_name=frames.get(model))


main()
