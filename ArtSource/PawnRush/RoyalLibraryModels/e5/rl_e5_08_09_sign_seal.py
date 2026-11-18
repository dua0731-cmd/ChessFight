"""E5 props 08 (brass hint sign on a post) and 09 (red wax seal impression), shape only.

Concept: Docs/PawnRush/ConceptArt/R687_GiantChessLibrary/GC_E5_front_course_assets.png, bottom row right.
Flat-colour materials only; every detail is geometry. Uses the shared helpers of
rl_e5_props_model.py read-only (never edits or saves that file's blend).

Run in the background:
    blender -b --factory-startup --python rl_e5_08_09_sign_seal.py -- 08      (sign only)
    blender -b --factory-startup --python rl_e5_08_09_sign_seal.py -- 09      (seal only)
    blender -b --factory-startup --python rl_e5_08_09_sign_seal.py -- 08 09   (both)
Builds, renders stills/e5/<nn>_<name>.png and saves e5/rl_e5_08_09.blend (both models).
Add `norender` to skip the stills.

Axes: game metres, Z up, the course runs +Y, so a prop's front faces -Y.
"""
import sys

exec(open(r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\RoyalLibraryModels\rl_e5_props_model.py",
          encoding="utf-8").read(), globals())

OWN_BLEND = os.path.join(HERE, "e5", "rl_e5_08_09.blend")

# satin brass for the plaque field (lighter, less mirror-like than the rim, like the sheet)
PALETTE["E5_BrassSatin"] = dict(rgb=(0.66, 0.43, 0.15), metal=1.0, rough=0.42)


def ring_prism(name, outer, inner, plane, a0, a1, mats, bev=None, smooth=35):
    """Extrude the band between two closed outlines with the same vertex count."""
    def P(u, v, a):
        if plane == "YZ":
            return (a, u, v)
        if plane == "XZ":
            return (u, a, v)
        return (u, v, a)
    bm = bmesh.new()
    n = len(outer)
    ol = [bm.verts.new(P(u, v, a0)) for u, v in outer]
    oh = [bm.verts.new(P(u, v, a1)) for u, v in outer]
    il = [bm.verts.new(P(u, v, a0)) for u, v in inner]
    ih = [bm.verts.new(P(u, v, a1)) for u, v in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ol[i], ol[j], oh[j], oh[i]))   # outer wall
        bm.faces.new((il[j], il[i], ih[i], ih[j]))   # inner wall
        bm.faces.new((oh[i], oh[j], ih[j], ih[i]))   # cap a1
        bm.faces.new((ol[j], ol[i], il[i], il[j]))   # cap a0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = mesh_obj(name, bm, mats, smooth=smooth)
    if bev:
        bevel(o, *bev) if isinstance(bev, tuple) else bevel(o, bev)
        wnormal(o)
    return o


# =================================================================== 08 BRASS HINT SIGN ON A POST
# Blockout `Memory clue` n: `Brass clue frame` 3.2 x 3.2 (z 2.2..5.4, centre z 3.8, 0.3 thick in y)
# with `Clue paper` 2.9 square in front of it (y -0.20..-0.16 from the frame centre) and the
# `Random clue glyph` empty on the paper. The plaque replaces the frame: its field (inside the
# inner border) is HS_W - 2*HS_IN wide and HS_H - 2*HS_IN high, so the 3.2 m clue area fits in the
# middle and the corner rivets sit beside it. Field face at local y HS_FACE; the clue paper goes
# just in front of it. Origin = centre of the base underside, standing on the table top (z 0);
# the clue centre is at (0, 0, HS_CZ).
HS_CZ = 3.8          # clue centre height (blockout)
HS_W, HS_H = 5.6, 4.3
HS_NOTCH = 0.48      # concave corner notch radius
HS_IN = 0.5          # inner border inset -> field 4.6 x 3.3
HS_FACE = -0.12      # field face (front of the back plate)


def notched_rect(w, h, r, cx=0.0, cz=0.0, d=0.0, n=8):
    """Rectangle w x h whose corners are cut by concave quarter circles of radius r (sheet plaque),
    offset inward by d: straight edges move in by d, the corner arcs keep their centres and grow to
    r + d, so every inset has the same vertex count and an even band width."""
    x0, x1, z0, z1 = cx - w / 2, cx + w / 2, cz - h / 2, cz + h / 2
    rr = r + d
    t0 = math.degrees(math.asin(d / rr)) if d > 0 else 0.0
    pts = []
    # CCW: bottom-right, top-right, top-left, bottom-left corner arcs (each centred on the corner)
    for (kx, kz), a_from in (((x1, z0), 180), ((x1, z1), 270), ((x0, z1), 360), ((x0, z0), 90)):
        a0, a1 = a_from - t0, a_from - 90 + t0
        for k in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * k / n)
            pts.append((kx + rr * math.cos(a), kz + rr * math.sin(a)))
    return pts


def build_hint_sign(offset=(220, 0, 0)):
    begin_model("E5_08_HintSign")
    BR, BD, BS = M("E5_Brass"), M("E5_BrassDark"), M("E5_BrassSatin")
    W, H, R, CZ = HS_W, HS_H, HS_NOTCH, HS_CZ
    pb = CZ - H / 2      # plaque bottom
    pt = CZ + H / 2      # plaque top

    def outline(d):
        return notched_rect(W, H, R, 0.0, CZ, d)

    # --- plaque: satin back plate (its front is the field), raised outer rim, inner border bead
    prism("HS back plate", outline(0.02), "XZ", HS_FACE, 0.13, BS, (0.035, 2, 40))
    ring_prism("HS outer rim", outline(0.0), outline(0.30), "XZ", -0.36, 0.05, BR, (0.09, 4, 40, "ANGLE", 0.6))
    ring_prism("HS rim back lip", outline(0.0), outline(0.16), "XZ", 0.04, 0.17, BR, (0.04, 2, 40))
    ring_prism("HS inner border", outline(HS_IN - 0.11), outline(HS_IN), "XZ", -0.215, HS_FACE + 0.01, BR,
               (0.04, 3, 40, "ANGLE", 0.6))
    # rivets: domed heads in the four field corners, beside the clue area
    head = [(0, 0), (0.19, 0), (0.19, 0.025), (0.17, 0.07), (0.12, 0.12), (0.06, 0.145), (0, 0.15)]
    rx, rz = W / 2 - HS_IN - 0.47, H / 2 - HS_IN - 0.42
    for sx in (-1, 1):
        for sz in (-1, 1):
            lathe(f"HS rivet {sx:+d}{sz:+d}", head, BR, (sx * rx, HS_FACE + 0.005, CZ + sz * rz), "Y-", seg=24)

    # --- turned post from the base up into the plaque; bulb collar just under the plaque
    lathe("HS base", [(0, 0), (1.30, 0), (1.36, 0.03), (1.38, 0.09), (1.38, 0.16), (1.33, 0.21),
                      (1.20, 0.25), (1.02, 0.29), (0.90, 0.33), (0.86, 0.36), (0.80, 0.37),
                      (0.80, 0.42), (0.83, 0.45), (0.83, 0.52), (0.78, 0.57), (0.62, 0.60),
                      (0, 0.60)], BR, seg=72)
    lathe("HS lower collar", [(0, 0.58), (0.50, 0.58), (0.53, 0.62), (0.53, 0.70), (0.47, 0.74),
                              (0.44, 0.76), (0.47, 0.79), (0.47, 0.85), (0.40, 0.90), (0.31, 0.93),
                              (0, 0.93)], BR, seg=56)
    lathe("HS post", [(0, 0.9), (0.29, 0.9), (0.27, 1.05), (0.26, pb - 0.5), (0.27, pb - 0.38), (0, pb - 0.38)],
          BR, seg=48)
    lathe("HS bulb collar", [(0, pb - 0.44), (0.30, pb - 0.44), (0.38, pb - 0.38), (0.42, pb - 0.28),
                             (0.42, pb - 0.16), (0.38, pb - 0.08), (0.32, pb - 0.03), (0.30, pb + 0.1),
                             (0, pb + 0.1)], BR, seg=48)
    # --- finial: short neck on the top edge and a ball
    fr = 0.40
    lathe("HS finial neck", [(0, pt - 0.1), (0.24, pt - 0.1), (0.24, pt + 0.02), (0.30, pt + 0.05),
                             (0.30, pt + 0.11), (0.20, pt + 0.15), (0.17, pt + 0.2), (0, pt + 0.2)], BR, seg=40)
    bz = pt + 0.12 + fr
    ball = [(0, bz - fr)] + [(fr * math.sin(math.pi * k / 16), bz - fr * math.cos(math.pi * k / 16))
                             for k in range(1, 16)] + [(0, bz + fr)]
    lathe("HS finial ball", ball, BR, seg=48)

    # marker for Unity: where the clue paper / glyph centre goes
    clue = bpy.data.objects.new("Memory clue centre", None)
    clue.empty_display_type = "PLAIN_AXES"
    clue.empty_display_size = 1.6
    _link(clue, (0, HS_FACE - 0.04, CZ))

    end_model(offset)
    return Ctx.root


# =================================================================== 09 RED WAX SEAL IMPRESSION
# Blockout `Royal seal rest island` (5 x 5 m disc pad, top z 0.08 in the `Fixed seal island` table,
# centre y -80). The seal is a low disc of the same 5 m footprint: scalloped blobby rim, a groove,
# a thin raised inner ring and a flat recessed centre left blank (the game draws on it).
# Origin = centre of the underside; it sits on the pad top. Centre face at local z WS_FACE,
# blank radius WS_FIELD_R.
WS_R = 2.5
WS_FACE = 0.14
WS_FIELD_R = 1.62
WS_LOBES = 9


def build_wax_seal(offset=(250, 0, 0)):
    begin_model("E5_09_WaxSeal")
    WX = M("E5_RedWax")
    F = WS_FACE
    # radial profile (r, z), outside in over the top; smoothed so the wax reads soft and poured
    top = [(2.48, 0.05), (2.54, 0.15), (2.52, 0.26), (2.44, 0.34), (2.30, 0.38), (2.16, 0.375),
           (2.04, 0.34), (1.96, 0.27), (1.91, 0.205), (1.87, 0.19), (1.83, 0.215), (1.795, 0.25),
           (1.75, 0.262), (1.71, 0.25), (1.68, 0.205), (1.655, F + 0.01), (WS_FIELD_R, F)]
    top = chaikin(top, 2, closed=False)
    prof = [(0.0, 0.0), (2.30, 0.0), (2.43, 0.012)] + top + [(1.2, F), (0.6, F), (0.0, F)]
    seg = 216
    bm = lathe_bm(prof, seg)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    # scalloped, slightly irregular outline; the wave fades out before the inner ring so the
    # ring and the blank centre stay perfectly round and flat
    for v in bm.verts:
        r = math.hypot(v.co.x, v.co.y)
        if r < 1e-6:
            continue
        s = min(1.0, max(0.0, (r - 1.80) / 0.45))
        s = s * s * (3 - 2 * s)
        th = math.atan2(v.co.y, v.co.x)
        wave = (0.055 * math.sin(WS_LOBES * th) + 0.018 * math.sin(4 * th + 1.3)
                + 0.012 * math.sin(13 * th + 0.4))
        k = 1.0 + wave * s
        v.co.x *= k
        v.co.y *= k
        if v.co.z > 0.03:   # lumpy rim height, never on the underside
            v.co.z += s * (0.018 * math.sin(WS_LOBES * th + 0.9) + 0.012 * math.sin(5 * th + 2.1))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = mesh_obj("WS wax seal", bm, WX, smooth=75)
    wnormal(o)

    mark = bpy.data.objects.new("Seal face centre", None)
    mark.empty_display_type = "CIRCLE"
    mark.empty_display_size = WS_FIELD_R
    _link(mark, (0, 0, F + 0.005))

    end_model(offset)
    return Ctx.root


# =================================================================== main
def _clear_default_scene():
    for o in list(bpy.data.objects):
        if o.users_collection and all(c == bpy.context.scene.collection for c in o.users_collection):
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)


JOBS = {
    "08": (build_hint_sign, "08_hint_sign", "E5_08_HintSign"),
    "09": (build_wax_seal, "09_wax_seal", "E5_09_WaxSeal"),
}

if __name__ == "__main__" or bpy.app.background:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["08"]
    _clear_default_scene()
    for key, (fn, still, col) in JOBS.items():
        fn()
    for key in argv:
        if key in JOBS and "norender" not in argv:
            # the broad flat seal face catches the whole key light; expose it down so the red
            # reads like the sheet (still only, materials untouched)
            bpy.context.scene.view_settings.exposure = -0.9 if key == "09" else 0.0
            print("RENDER", key, render_still(JOBS[key][1], JOBS[key][2]))
            bpy.context.scene.view_settings.exposure = 0.0
    os.makedirs(os.path.dirname(OWN_BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OWN_BLEND)
    print("SAVED", OWN_BLEND)
