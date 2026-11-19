"""E5 prop 05: the closed book (concept sheet GC_E5_front_course_assets.png, middle row right).

Shape only: flat palette materials from rl_e5_props_model.py, every detail is geometry.
Run in the background (never in the main session's live Blender):
    blender.exe -b --factory-startup --python rl_e5_05_closed_book.py [-- --quick]

Size: the blockout has no closed-book object, so it uses the `Closing royal folio` footprint
(18 x 16 m) and the sheet's proportions for the thickness: 0.7 m boards + 2.0 m page block
+ 0.7 m boards = 3.4 m. Origin = centre of the footprint on the floor, Z up, front faces -Y,
spine along Y on the -X side (same side as book 4).
"""
import sys

exec(open(r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\RoyalLibraryModels\rl_e5_props_model.py",
          encoding="utf-8").read(), globals())

OUT_BLEND = os.path.join(HERE, "e5", "rl_e5_05.blend")

CB_BOARD = 0.70          # board (cover) thickness
CB_PAGES = 2.00          # page block height
CB_H = CB_BOARD * 2 + CB_PAGES
CB_X0, CB_X1 = -8.50, 9.45   # boards: spine-side edge (meets the spine arms) .. fore-edge
CB_Y = 8.45                  # boards run y -8.45 .. 8.45
CB_SX = -8.90                # spine C centre x
CB_ZC = CB_H / 2


def _ellipse_arc(cx, cz, a, b, a0, a1, n):
    return [(cx + a * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             cz + b * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def spine_c(a_out, b_out, a_in, b_in, tail, n=28):
    """Closed (x, z) outline of a C-shaped spine wrapping the page block on the -X side.
    The arms run tail metres into +X so they tuck under/over the boards' spine edge."""
    outer = _ellipse_arc(CB_SX, CB_ZC, a_out, b_out, 90, 270, n)
    inner = _ellipse_arc(CB_SX, CB_ZC, a_in, b_in, 270, 90, n)
    pts = [(CB_SX + tail, CB_ZC + b_out)] + outer + [(CB_SX + tail, CB_ZC - b_out),
                                                     (CB_SX + tail, CB_ZC - b_in)] + inner + [(CB_SX + tail, CB_ZC + b_in)]
    return pts


def stud(name, x, y, z, r=0.30, mat=None):
    """Domed brass stud sitting on a face at height z."""
    prof = [(0, 0), (r * 1.12, 0), (r * 1.12, 0.06)]
    prof += [(r * math.cos(math.radians(a)), 0.06 + 0.62 * r * math.sin(math.radians(a))) for a in range(0, 91, 10)]
    return lathe(name, prof, mat, (x, y, z), seg=32)


def build_closed_book(offset=(130, 0, 0)):
    begin_model("E5_05_ClosedBook")
    BR, LE, PA = M("E5_Brass"), M("E5_RedLeather"), M("E5_Paper")
    B, P, H = CB_BOARD, CB_PAGES, CB_H

    # --- boards: thick leather covers with soft, rounded edges
    board = rrect(CB_X0, CB_X1, -CB_Y, CB_Y, 0.38, 6)
    prism("CB back board", board, "XY", 0.0, B, LE, (0.24, 4, 40))
    prism("CB front board", board, "XY", B + P, H, LE, (0.24, 4, 40))

    # --- page block: Array of page sheets with a slightly concave fore-edge (layered cream edges)
    fore = [(9.0 - 0.30 * math.sin(math.pi * t / 12), -8.05 + 16.1 * t / 12) for t in range(13)]
    outline = [(-9.25, -8.05)] + fore + [(-9.25, 8.05)]
    sheet = prism("CB page sheets", outline, "XY", B, B + 0.098, PA, (0.04, 2, 40))
    arr = sheet.modifiers.new("Pages", "ARRAY")
    arr.count = int(round(P / 0.1))
    arr.use_relative_offset = False
    arr.use_constant_offset = True
    arr.constant_offset_displace = (0, 0, 0.1)
    sheet.modifiers.move(len(sheet.modifiers) - 1, 0)

    # --- spine: C-shaped leather wrap around the page block, flush with the boards;
    # its arms meet the boards in a soft hinge groove
    A_OUT, B_OUT = 1.58, H / 2
    A_IN, B_IN = 0.92, H / 2 - B + 0.02
    prism("CB spine", spine_c(A_OUT, B_OUT, A_IN, B_IN, 0.45), "XZ", -CB_Y + 0.08, CB_Y - 0.08, LE,
          (0.16, 4, 40))
    # two brass bands wrapped round the spine (sheet: one near the head, one past the middle)
    for y in (-4.6, 4.6):
        band = spine_c(A_OUT + 0.12, B_OUT + 0.12, A_OUT - 0.25, B_OUT - 0.25, 0.32)
        prism(f"CB spine band {y:+.0f}", band, "XZ", y - 0.5, y + 0.5, BR, (0.07, 3, 40))

    # --- front board face: brass border lines, corner guards on the fore-edge, studs at the spine
    book_cover_details("CB front board", CB_X0 + 0.2, CB_X1, -CB_Y, CB_Y, B + P + B / 2, B, BR, 1)
    for cy, sy in ((-CB_Y, -1), (CB_Y, 1)):
        for z0, z1, tag in ((B + P - 0.05, H + 0.05, "front"), (-0.0, B + 0.05, "back")):
            brass_corner(f"CB {tag} corner {sy:+d}", CB_X1 + 0.06, cy + 0.06 * sy, 1, sy, z0, z1, leg=2.5, w=0.85,
                         mat=BR)
        # rivet on each top guard plate
        stud(f"CB front corner stud {sy:+d}", CB_X1 - 0.62, cy - sy * 0.62, H + 0.05, 0.30, BR)
        # round studs at the border's spine-side corners
        stud(f"CB spine stud {sy:+d}", CB_X0 + 1.25, cy - sy * 1.05, H - 0.02, 0.42, BR)

    end_model(offset)
    return Ctx.root


def _clear_default_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        if not c.name.startswith("E5_"):
            bpy.data.collections.remove(c)
    bpy.data.orphans_purge(do_recursive=True)


if __name__ == "__main__":
    quick = "--quick" in sys.argv
    _clear_default_scene()
    build_closed_book()
    if quick:
        # quick checks go to a temp folder, not the repo stills
        STILLS = os.environ.get("CB_QUICK_DIR", bpy.app.tempdir)
        p = render_still("05_closed_book_quick", "E5_05_ClosedBook", res=(800, 600), samples=16)
    else:
        p = render_still("05_closed_book", "E5_05_ClosedBook")
        bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("CB_DONE", p)
