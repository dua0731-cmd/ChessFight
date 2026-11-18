"""E5 props 06 (course table) and 07 (rest table with the round seal holder), shape only.

Concept sheet: Docs/PawnRush/ConceptArt/R687_GiantChessLibrary/GC_E5_front_course_assets.png,
bottom row, first two. Flat-colour palette materials only; every detail is geometry.

Run in the background (never in the main session's live Blender):
    blender -b --factory-startup --python rl_e5_06_07_tables.py -- 06      (render 06 only)
    blender -b --factory-startup --python rl_e5_06_07_tables.py -- 07      (render 07 only)
    blender -b --factory-startup --python rl_e5_06_07_tables.py -- none    (build + save, no render)
Both models are always built and saved to e5/rl_e5_06_07.blend.

Sizes come from RoyalLibrary_blockout.blend (game metres, Z up, course runs +Y, front = -Y):
  every course table: top slab z -1.2 .. 0 (walkable top z 0), brass edge band around it,
  aprons z -1 .. -3, four 1.8 m legs z -1 .. -21 set 1.2 m in from each corner (centres at
  x +-(W/2 - 1.2), y +-(D/2 - 1.2)), 2 m ferrule z -20 .. -21. Widths x depths in the course:
  18 x 17 (Approach -146), 18 x 27 (Dispatch), 18 x 16 (two folio desks, top at -0.85),
  18 x 8 (Fixed seal island = 07), 18 x 9 (Approach -60), 16 x 19 (Approach -27).
  06 is modelled at 18 x 16; 07 at 18 x 8 with the 5 m round seal rest in the middle.

Stretching to another size = call build_table(W, D, ...) again; nothing is scaled:
  CT top            walnut slab, outline rrect(W, D, r 0.6), z -1.2 .. 0
  CT rim            brass frame swept round the slab outline (RIM_PROFILE), lip rises to z +0.135
  CT corner cap *   brass caps at the 4 corners (CAP 1.55 m), top z +0.24, rivet on top + sides
  CT rim rivet *    dome rivets on the rim's outer face, spaced ~RIVET_STEP m between the caps
  CT apron *        walnut boards between the leg blocks on all four sides, brass bead at the foot
  CT leg *          per leg: square walnut block (z -1.25 .. -3.3), brass collar, turned walnut
                    leg (LEG_PROFILE, fixed length z -3.55 .. -20), brass neck ring, brass ferrule,
                    brass foot (z -20 .. -21). Legs keep their length; only their x/y move.
"""
import bpy, bmesh, math, os, sys

exec(open(r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\RoyalLibraryModels\rl_e5_props_model.py",
          encoding="utf-8").read(), globals())

# Table-only varnished walnut (slight gloss, user request 11-18); exports to `RL | varnished`
# (RL_VarnishedWalnut). The shared E5_Walnut stays as it is (used by the stamp press).
# The top keeps a lower specular level and a softened coat: a sharp coat on an 18 m flat
# mirrors the whole studio light and reads grey.
PALETTE["E5_TableWalnut"] = dict(rgb=(0.085, 0.028, 0.011), rough=0.28, coat=0.35)
PALETTE["E5_WalnutTop"] = dict(rgb=(0.085, 0.028, 0.011), rough=0.55, coat=0.08)


def _varnish(name, spec, coat_rough):
    m = M(name)
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    _sock(b, "Specular IOR Level").default_value = spec
    s = _sock(b, "Coat Roughness")
    if s:
        s.default_value = coat_rough
    return m


def top_material():
    return _varnish("E5_WalnutTop", 0.2, 0.1)


def table_walnut():
    return _varnish("E5_TableWalnut", 0.5, 0.12)

E5_DIR = os.path.join(HERE, "e5")
MY_BLEND = os.path.join(E5_DIR, "rl_e5_06_07.blend")

LEG_IN = 1.2          # leg centre distance in from the top's edges (blockout)
BLOCK = 2.2           # square leg block width
LEG_R = 1.1           # radial scale of LEG_PROFILE
CORNER_R = 0.6        # top outline corner radius
RIVET_STEP = 2.4
CAP = 1.55

# Rim cross-section, (d, z): d = distance outward from the slab edge. Closed loop; the inner
# part (d < 0 below z 0) sits inside the slab and is never seen.
RIM_PROFILE = [
    (-0.40, -0.02), (-0.40, 0.03), (-0.36, 0.075), (-0.28, 0.11), (-0.15, 0.135), (0.00, 0.13),
    (0.10, 0.105), (0.15, 0.05), (0.16, -0.04),
    (0.16, -0.30), (0.19, -0.34), (0.19, -0.40), (0.16, -0.44),     # upper bead
    (0.16, -0.92), (0.20, -0.98), (0.23, -1.05), (0.23, -1.17), (0.20, -1.23), (0.14, -1.28),
    (0.05, -1.31), (-0.05, -1.31), (-0.05, -0.02),
]

# Turned leg, (r, f): f = 0 at the top (just under the collar) .. 1 at the foot.
LEG_TOP, LEG_BOT = -3.55, -20.0
LEG_PROFILE = [
    (0.00, 0.000), (0.80, 0.000), (0.84, 0.012), (0.84, 0.030), (0.76, 0.042), (0.70, 0.050),
    (0.78, 0.058), (0.86, 0.066), (0.86, 0.074), (0.78, 0.082), (0.72, 0.090),           # bead + cove
    (0.80, 0.110), (0.94, 0.140), (1.05, 0.175), (1.12, 0.215), (1.13, 0.235),           # vase bulb
    (1.09, 0.270), (0.98, 0.300), (0.82, 0.330), (0.67, 0.360), (0.58, 0.385),
    (0.56, 0.405), (0.56, 0.445), (0.59, 0.470),                                        # neck (brass ring)
    (0.64, 0.520), (0.67, 0.580), (0.67, 0.640), (0.64, 0.700), (0.58, 0.760),           # long shaft
    (0.53, 0.795), (0.52, 0.805), (0.58, 0.812), (0.64, 0.822), (0.64, 0.832),           # lower bead
    (0.58, 0.842), (0.52, 0.850), (0.54, 0.865), (0.64, 0.890), (0.72, 0.912),           # lower knob
    (0.74, 0.925), (0.70, 0.940), (0.62, 0.952), (0.58, 0.960), (0.58, 1.000), (0.00, 1.000),
]


def leg_z(f):
    return LEG_TOP + (LEG_BOT - LEG_TOP) * f


def sweep_ring(name, path, profile, mats, smooth=40):
    """Loft a closed (d, z) cross-section round a closed XY path (d = outward offset)."""
    bm = bmesh.new()
    rings = []
    for d, z in profile:
        rings.append([bm.verts.new((x, y, z)) for x, y in offset_poly(path, -d)])
    n, m = len(path), len(rings)
    for k in range(m):
        a, b = rings[k], rings[(k + 1) % m]
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_obj(name, bm, mats, smooth=smooth)


RIVET = [(0, 0), (0.17, 0), (0.17, 0.025), (0.15, 0.065), (0.10, 0.098), (0.05, 0.113), (0, 0.117)]


def rivet(name, loc, axis, mat, s=1.0):
    return lathe(name, [(r * s, z * s) for r, z in RIVET], mat, loc, axis, seg=20)


def spaced(a, b, step):
    n = max(1, int(round((b - a) / step)))
    return [a + (b - a) * (i + 0.5) / n for i in range(n)]


def build_table(prefix, W, D, BR, WA, WT):
    hw, hd = W / 2, D / 2
    path = rrect(-hw, hw, -hd, hd, CORNER_R, 6)

    # --- top: walnut slab + brass rim frame
    prism(f"{prefix} top", path, "XY", -1.2, -0.12, WA, (0.03, 2))
    # walnut planks along X (the sheet's grain direction); their V-grooves break the big flat
    n = max(2, int(round(D / 2.7)))
    for i in range(n):
        y0, y1 = -hd + 0.05 + (D - 0.1) * i / n, -hd + 0.05 + (D - 0.1) * (i + 1) / n
        box(f"{prefix} top plank {i}", -hw + 0.1, hw - 0.1, y0, y1, -0.13, 0.0, WT, (0.035, 2, 30))
    sweep_ring(f"{prefix} rim", path, RIM_PROFILE, BR)

    # --- corner caps: square brass block wrapping the corner, raised plate, rivets
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * hw, sy * hd
            o = 0.27
            xs = sorted((cx + sx * o, cx - sx * (CAP - o)))
            ys = sorted((cy + sy * o, cy - sy * (CAP - o)))
            prism(f"{prefix} corner cap {sx:+d}{sy:+d}", rrect(xs[0], xs[1], ys[0], ys[1], 0.22, 4), "XY",
                  -1.36, 0.17, BR, (0.06, 3))
            xi = sorted((xs[0] + 0.16, xs[1] - 0.16))
            yi = sorted((ys[0] + 0.16, ys[1] - 0.16))
            prism(f"{prefix} corner plate {sx:+d}{sy:+d}", rrect(xi[0], xi[1], yi[0], yi[1], 0.14, 4), "XY",
                  0.17, 0.25, BR, (0.035, 2))
            mx, my = (xs[0] + xs[1]) / 2, (ys[0] + ys[1]) / 2
            rivet(f"{prefix} corner rivet top {sx:+d}{sy:+d}", (mx, my, 0.25), "Z", BR, 1.3)
            rivet(f"{prefix} corner rivet x {sx:+d}{sy:+d}", (cx + sx * o, my, -0.6), "X+" if sx > 0 else "X-", BR, 1.4)
            rivet(f"{prefix} corner rivet y {sx:+d}{sy:+d}", (mx, cy + sy * o, -0.6), "Y+" if sy > 0 else "Y-", BR, 1.4)

    # --- rim rivets on the outer face between the caps
    RZ, RD = -0.66, 0.16
    for sy in (-1, 1):
        for x in spaced(-hw + CAP - 0.27, hw - CAP + 0.27, RIVET_STEP):
            rivet(f"{prefix} rim rivet y{sy:+d} {x:+.1f}", (x, sy * (hd + RD), RZ), "Y+" if sy > 0 else "Y-", BR, 1.4)
    for sx in (-1, 1):
        for y in spaced(-hd + CAP - 0.27, hd - CAP + 0.27, RIVET_STEP):
            rivet(f"{prefix} rim rivet x{sx:+d} {y:+.1f}", (sx * (hw + RD), y, RZ), "X+" if sx > 0 else "X-", BR, 1.4)

    # --- legs at the blockout positions
    lx, ly = hw - LEG_IN, hd - LEG_IN
    bh = BLOCK / 2
    leg_prof = [(r * LEG_R, leg_z(f)) for r, f in LEG_PROFILE]
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * lx, sy * ly
            tag = f"{sx:+d}{sy:+d}"
            box(f"{prefix} leg block {tag}", x - bh, x + bh, y - bh, y + bh, -3.3, -1.25, WA, (0.09, 3))
            # brass collar (two rings) under the block
            lathe(f"{prefix} leg collar {tag}",
                  [(0, -3.25), (1.00, -3.25), (1.06, -3.29), (1.06, -3.40), (1.00, -3.44), (1.03, -3.48),
                   (1.03, -3.58), (0.98, -3.62), (0, -3.62)], BR, (x, y, 0), seg=48)
            lathe(f"{prefix} leg {tag}", leg_prof, WA, (x, y, 0), seg=48)
            z0, z1 = leg_z(0.405), leg_z(0.445)
            lathe(f"{prefix} leg neck ring {tag}",
                  [(0, z0), (0.62 * LEG_R, z0), (0.68 * LEG_R, z0 - 0.06), (0.70 * LEG_R, z0 - 0.16),
                   (0.70 * LEG_R, z1 + 0.16), (0.68 * LEG_R, z1 + 0.06), (0.62 * LEG_R, z1), (0, z1)], BR, (x, y, 0), seg=40)
            z0, z1 = leg_z(0.955), leg_z(1.0)
            lathe(f"{prefix} leg ferrule {tag}",
                  [(0, z0)] + [(r * LEG_R, z) for r, z in ((0.62, z0), (0.66, z0 - 0.05), (0.66, z0 - 0.22),
                   (0.63, z0 - 0.27), (0.66, z0 - 0.32), (0.66, z1 + 0.05), (0.64, z1))] + [(0, z1)],
                  BR, (x, y, 0), seg=40)
            lathe(f"{prefix} leg foot {tag}",
                  [(r * LEG_R, z) for r, z in [(0, -19.98), (0.70, -19.98), (0.80, -20.05), (0.84, -20.16), (0.80, -20.28),
                   (0.84, -20.42), (0.94, -20.62), (1.00, -20.80), (1.00, -20.92), (0.95, -21.0), (0, -21.0)]],
                  BR, (x, y, 0), seg=48)

    # --- aprons between the leg blocks (all four sides), brass bead along the foot
    AZ0, AZ1, AO, AT = -2.95, -1.25, 0.32, 0.30
    for sy in (-1, 1):
        y0, y1 = sorted((sy * (hd - AO), sy * (hd - AO - AT)))
        x0, x1 = -lx + bh, lx - bh
        box(f"{prefix} apron y{sy:+d}", x0, x1, y0, y1, AZ0, AZ1, WA, (0.05, 2))
        tube(f"{prefix} apron bead y{sy:+d}", [(x0, sy * (hd - AO), AZ0 + 0.06), (x1, sy * (hd - AO), AZ0 + 0.06)],
             0.07, BR)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * (hw - AO), sx * (hw - AO - AT)))
        y0, y1 = -ly + bh, ly - bh
        box(f"{prefix} apron x{sx:+d}", x0, x1, y0, y1, AZ0, AZ1, WA, (0.05, 2))
        tube(f"{prefix} apron bead x{sx:+d}", [(sx * (hw - AO), y0, AZ0 + 0.06), (sx * (hw - AO), y1, AZ0 + 0.06)],
             0.07, BR)


def build_course_table(offset=(160, 0, 0), W=18.0, D=16.0):
    begin_model("E5_06_CourseTable")
    build_table("CT", W, D, M("E5_Brass"), table_walnut(), top_material())
    end_model(offset)
    return Ctx.root


def build_rest_table(offset=(190, 0, 0), W=18.0, D=8.0):
    begin_model("E5_07_RestTable")
    BR = M("E5_Brass")
    build_table("RT", W, D, BR, table_walnut(), top_material())
    # Round seal holder (blockout `Royal seal rest island`: 5 m disc centred on the top). The sheet's
    # pedestal is a low stepped brass drum; kept to 0.5 m so it stays a small step up (typewriter
    # keys rise 0.32 per row). Flange r 2.5 sits on the planks, drum r 2.0, dished top r 1.7.
    lathe("RT seal flange", [(0, -0.02), (2.50, -0.02), (2.56, 0.02), (2.58, 0.08), (2.54, 0.13), (2.44, 0.16),
                             (2.30, 0.17), (2.24, 0.19), (2.24, 0.22), (2.0, 0.23), (0, 0.23)], BR, seg=96)
    lathe("RT seal drum", [(0, 0.2), (2.02, 0.2), (2.06, 0.24), (2.06, 0.30), (2.01, 0.33), (1.98, 0.36),
                           (1.98, 0.40), (2.04, 0.43), (2.06, 0.47), (2.02, 0.51), (1.92, 0.53),
                           (1.80, 0.53), (1.74, 0.50), (1.70, 0.47), (1.2, 0.455), (0, 0.45)], BR, seg=96)
    for i in range(12):
        a = 2 * math.pi * (i + 0.5) / 12
        rivet(f"RT seal flange rivet {i}", (2.38 * math.cos(a), 2.38 * math.sin(a), 0.16), "Z", BR, 0.8)
    end_model(offset)
    return Ctx.root


def _clear_startup():
    for o in list(bpy.data.objects):
        if not o.name.startswith("E5_"):
            bpy.data.objects.remove(o, do_unlink=True)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["06"]
    _clear_startup()
    build_course_table()
    build_rest_table()
    bpy.context.view_layer.update()
    if "06" in argv:
        print("STILL", render_still("06_course_table", "E5_06_CourseTable"))
    if "07" in argv:
        print("STILL", render_still("07_rest_table", "E5_07_RestTable"))
    for c in bpy.context.scene.collection.children:
        c.hide_render = c.hide_viewport = False
    bpy.ops.wm.save_as_mainfile(filepath=MY_BLEND)
    print("SAVED", MY_BLEND)
