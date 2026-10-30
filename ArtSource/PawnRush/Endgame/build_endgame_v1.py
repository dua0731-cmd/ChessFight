"""Pawn Rush endgame (the war room), LD v1: build the kit, place the course, check it, render, export.

    blender --background --factory-startup --python build_endgame_v1.py -- <out dir> [options]
    python build_endgame_v1.py -- <out dir> [options]          (with the bpy module, Blender 5.2)

Options:
    --renders none|quick|cuts|sheets|skins|turntable|mp4|full
                                review renders (quick: layout + 6 cuts at 1280 x 720; sheets: per-beat plans at
                                1 px = 2 cm; skins: every device skin alone; turntable: every skin from four sides;
                                mp4: timing preview; full: all of them)
    --size <w>x<h>, --samples <n>
                                cut size and EEVEE samples (default 1280x720 and 24, full: 1920x1080 and 48)
    --check                     interference, path, safe windows and budget reports
    --export                    FBX per part (LOD0-2, UV1), part .blend files, PR_EG_manifest.json
    --parts <dir>               where the part folders go (default <out dir>/parts; the repo keeps them here, next to
                                this script: ArtSource/PawnRush/Endgame/<Part>/)
    --time <s>                  shared-clock time the devices are posed at for the cuts (default per shot)

Headless renders need an OpenGL context: on a machine without a GPU set EGL_PLATFORM=surfaceless and install Mesa
(llvmpipe); EEVEE then renders on the CPU.
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

import eg_bl_kit as K  # noqa: E402
import eg_bl_materials as MAT  # noqa: E402
import eg_bl_render as R  # noqa: E402
import eg_bl_scene as S  # noqa: E402
import eg_bl_util as B  # noqa: E402
import eg_layout as E  # noqa: E402


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv and not argv[0].startswith("--") else os.path.join(HERE, "out")
    opt = dict(out=os.path.abspath(out), renders="quick", check="--check" in argv, export="--export" in argv, time=None,
               parts=None)
    if "--parts" in argv:
        opt["parts"] = os.path.abspath(argv[argv.index("--parts") + 1])
    if "--renders" in argv:
        opt["renders"] = argv[argv.index("--renders") + 1]
    if "--time" in argv:
        opt["time"] = float(argv[argv.index("--time") + 1])
    if "--size" in argv:
        opt["size"] = tuple(int(v) for v in argv[argv.index("--size") + 1].lower().split("x"))
    if "--samples" in argv:
        opt["samples"] = int(argv[argv.index("--samples") + 1])
    return opt


def log(*a):
    print("[endgame]", *a, flush=True)


# ---------------------------------------------------------------------------------------------- kit
def build_kit(kit_coll, L):
    k = {}
    k["rail4"], k["rail2"], k["rail1"], k["rail05"] = K.rail(4.0), K.rail(2.0), K.rail(1.0), K.rail(0.5)
    k["post"] = K.rail_post()
    k["map_table"] = K.map_table()
    k["rook"] = K.rook_statue()
    k["compass"] = K.compass_inlay()
    k["arrow"] = K.arrow_sign()
    k["pillar"] = K.hall_pillar()
    k["lamp"] = K.lamp()
    k["shelves"] = [K.bookshelf_unit(seed=s) for s in range(4)]
    k["banner"] = K.banner()
    k["window"] = K.arch_window()
    k["ladder"] = K.ladder()
    k["balcony"] = K.balcony()
    k["chandelier"] = K.chandelier()
    k["floor_tile"] = K.hall_floor_tile()
    k["beam"] = K.ceiling_beam()
    k["wall_panel"] = K.hall_wall_panel()
    k["war_table"] = K.skin_war_table()
    k["sweep_bar"] = K.skin_sweep_bar(E.V1["bar_length"])
    k["ferry"] = K.skin_ferry(E.V1["ferry_frame_h"])
    hb, ht, hd = E.V1["hurdle"]
    k["hurdle"] = K.skin_hurdle(-5.6, 5.6, hb, ht, hd)
    k["shelf_wall"] = K.skin_shelf_wall()
    k["shelf_housing"] = K.skin_shelf_housing()
    k["gate_frame"] = K.skin_gate_frame()
    k["gate_door"] = K.skin_gate_door()
    k["rook_screen"] = K.skin_rook_screen()
    k["pendulum"] = K.skin_pendulum()
    k["gantry"] = K.skin_pendulum_gantry()
    k["chest_tile"] = K.skin_map_chest_tile()
    k["lift_shaft"] = S.lift_shaft_kit()
    k["vent"] = K.skin_wind_vent()
    k["belt"] = K.skin_map_belt(10.0)
    k["pawn"] = K.skin_giant_pawn()
    k["crown_circle"] = K.skin_crown_circle()
    k["crate"] = K.skin_hanging_crate()
    k["crane_rail"] = K.skin_crane_rail(getattr(L, "crane_len", 16.0))
    k["disc"] = K.skin_compass_disc()
    k["bridge_panel"] = K.skin_bridge_panel()
    k["bridge_landing"] = K.skin_bridge_landing()
    k["plinth"] = S.plinth_kit()
    k["arch_crest"] = S.arch_crest_kit()
    for o in kit_coll.objects:
        o.hide_render = True
    return k


# ---------------------------------------------------------------------------------------------- gameplay reference
def gameplay_ref(coll):
    """The gray box as it is in Unity today (Course01v4Builder.WarRoom.cs): wire boxes, locked, not rendered."""
    import bmesh
    L = E.graybox()
    bm = bmesh.new()
    for f in L.floors:
        B.box_bm(bm, f["x0"], f["x1"], f["z0"], f["z1"], f["bottom"], f["top"])
    for w in L.walls:
        B.box_bm(bm, w["x0"], w["x1"], w["z0"], w["z1"], w["y0"], w["y1"])
    for r in L.rails:
        B.box_bm(bm, r["x0"], r["x1"], r["z0"], r["z1"], r["floor"], r["floor"] + r["h"])
    for d in L.devices:
        x, y, z = d["pos"]
        t = d["type"]
        if t in ("board", "disc"):
            B.cyl_bm(bm, (x, z, y - 0.25 if t == "board" else y + 0.2), d["diameter"] / 2, 0.5 if t == "board" else 0.4, segments=48)
        elif t == "ferry":
            B.box_bm(bm, x - 4, x + 4, z - 4 - 6, z - 4 + 2, y - 0.5, y)
        elif t == "rookscreen":
            B.box_bm(bm, x - 3, x + 3, z - 1, z + 1, y, y + 3.2)
        elif t == "pendulum":
            B.sphere_bm(bm, (x - 4.91, z, y + 7.3 - 6 * math.cos(math.radians(55))), 1.1, seg=16, rings=8)
        elif t == "lifttiles":
            B.box_bm(bm, x - 3.35, x + 3.35, z - 3.35, z + 3.35, y - 0.4, y)
        elif t == "conveyor":
            B.box_bm(bm, x - 3, x + 3, z - 4, z + 4, y, y + 0.4)
        elif t == "foldingbridge":
            B.box_bm(bm, x - 3, x + 3, z - 7.05, z + 7.05, y - 0.3, y)
        elif t == "fallingpiece":
            B.cyl_bm(bm, (x, z, y + 0.02), 5.0, 0.04, segments=48)
        elif t in ("sliding", "clockgates"):
            B.box_bm(bm, x - 7.4, x + 7.4, z - 3.3, z + 3.3, y, y + 3.2)
    o = B.mesh_object("EG_GameplayRef_graybox", bm, [], coll, uv_world=False, smooth_angle=None)
    o.display_type = "WIRE"
    o.hide_render = True
    o.hide_select = True
    o.color = (1.0, 0.35, 0.2, 1.0)
    return o


# ---------------------------------------------------------------------------------------------- lights and shots
def lights(L, coll):
    R.world(0.28, "#33403F")
    R.light("Sun through the east windows", "SUN", (0, 0, 60), 2.6, "#FFE0B0",
            rot=(math.radians(58), math.radians(-18), math.radians(-115)), size=4, coll=coll)
    R.light("Cool fill", "SUN", (0, 0, 60), 0.35, "#A9C6D6", rot=(math.radians(35), 0, math.radians(70)), size=20, coll=coll)
    end = max(b["z1"] for b in L.beats)
    z = -6.0
    while z < end + 20:
        for sx in (-1, 1):
            R.light(f"Lamp light {z:.0f}{sx}", "POINT", B.U(sx * 24.6, 6.4, z), 900, "#FFC27A", size=0.4, coll=coll)
        R.light(f"Course glow {z:.0f}", "AREA", B.U(0, 16, z + 6), 1800, "#FFE3B8", size=14, coll=coll)
        z += 12.0


def shot_list(L):
    """Six cuts like the concept frames: (camera Unity pos, target Unity pos, lens, clock time)."""
    gate = L.device_by_id("R2_gates")["pos"][2]
    zc = [d for d in L.devices if d["type"] == "fallingpiece"][0]["pos"][2]
    zb = [d for d in L.devices if d["type"] == "foldingbridge"][0]["pos"][2]
    return {
        "S1": ((-4.6, 5.6, -3.0), (1.0, 0.3, 17.0), 24, 1.0),
        "R1": ((-13.0, 9.0, 13.5), (0.5, 0.0, 25.0), 24, 2.0),
        "S2": ((-10.5, 6.5, 35.0), (0.0, 0.0, 51.0), 24, 3.0),
        "S3": ((-4.5, 5.0, gate + 2.5), (0.5, 0.8, gate + 21.0), 22, 1.2),
        "R5": ((-9.0, 9.5, zc - 14.0), (0.0, 3.0, zc + 1.0), 24, 2.3),
        "S4": ((-10.0, 9.5, zb - 12.0), (0.0, 3.0, zb + 2.0), 24, 9.5),
    }


def shots(L, rigs, coll, outdir, width, height, samples, only=None):
    out = []
    R.setup("EEVEE", width, height, samples)
    for name, (cp, tp, lens, t) in shot_list(L).items():
        if only and name not in only:
            continue
        S.pose(rigs, t)
        cam = R.camera(f"Cam {name}", B.U(*cp), B.U(*tp), lens=lens, coll=coll)
        t0 = time.time()
        path = R.render(cam, os.path.join(outdir, f"cut_{name}.png"))
        log("cut", name, round(time.time() - t0, 1), "s")
        out.append(path)
    return out


def layout_overview(L, coll, outdir, px_per_m=10, samples=8):
    """Top-down orthographic plan of the whole course, the way north turned to the right."""
    z0 = -6.0
    z1 = max(b["z1"] for b in L.beats) + 6.0
    length, width = z1 - z0, 64.0
    cam = R.camera("Cam plan", B.U(0, 120, (z0 + z1) / 2), B.U(0, 0, (z0 + z1) / 2), coll=coll, ortho=length)
    cam.rotation_euler = (0, 0, math.radians(90))      # north (Blender +Y) to the right of the picture
    overhead = [o for o in bpy.data.objects if o.name.startswith(("Hall beam", "Hall chandelier", "R5b crane rail"))]
    for o in overhead:
        o.hide_render = True
    R.setup("EEVEE", int(length * px_per_m), int(width * px_per_m), samples, raytrace=False)
    t0 = time.time()
    path = R.render(cam, os.path.join(outdir, "plan_overview.png"))
    for o in overhead:
        o.hide_render = False
    log("plan", round(time.time() - t0, 1), "s")
    return path


# ---------------------------------------------------------------------------------------------- main
def main():
    opt = args()
    os.makedirs(opt["out"], exist_ok=True)
    t_all = time.time()
    B.reset_scene()
    mats = MAT.build_all()
    kit_coll = B.collection("EG_Kit", hide_render=True)
    K.init(mats, kit_coll)
    L = E.ld_v1()
    kit = build_kit(kit_coll, L)
    log("kit", len(kit_coll.objects), "pieces", round(time.time() - t_all, 1), "s")

    colls = {b["id"]: B.collection(f"EG_{b['id']}") for b in L.beats}
    colls["EG_Devices"] = B.collection("EG_Devices")
    signals_coll = B.collection("EG_Signals")
    hall_coll = B.collection("EG_Hall")
    for b in L.beats:
        S.beat_floor_mesh(L, b["id"], colls[b["id"]])
        S.beat_wall_mesh(L, b["id"], colls[b["id"]])
    S.rails(L, kit, B.collection("EG_Rails"))
    rigs = S.build_devices(L, kit, colls, t=opt["time"] or 0.0)
    crane = B.instance(kit["crane_rail"], "R5b crane rail", colls["R5b"], B.U(0, E.UPPER, getattr(L, "crane_z", 180.0)))
    S.record(crane, kit["crane_rail"].name, "static", pivot="floor-level centre of the span")
    S.signals(L, kit, signals_coll)
    S.hall(L, kit, hall_coll)
    ref = B.collection("EG_GameplayRef")
    gameplay_ref(ref)
    review = B.collection("EG_Review")
    lights(L, review)
    log("course placed", len(bpy.data.objects), "objects", round(time.time() - t_all, 1), "s")

    if opt["check"]:
        run_checks(L, rigs, kit_coll, review, opt["out"])

    if opt["renders"] in ("quick", "full", "cuts"):
        w, h, smp = (1280, 720, 24) if opt["renders"] != "full" else (1920, 1080, 48)
        w, h = opt.get("size", (w, h))
        smp = opt.get("samples", smp)
        if opt["renders"] != "cuts":
            layout_overview(L, review, opt["out"], px_per_m=8 if opt["renders"] == "quick" else 20)
        shots(L, rigs, review, opt["out"], w, h, smp)
    if opt["renders"] in ("full", "sheets"):
        import eg_bl_annot as A
        notes = B.collection("EG_Notes")
        A.annotate(L, notes)
        A.beat_sheets(L, review, opt["out"])
        bpy.data.collections["EG_Notes"].hide_render = True
    if opt["renders"] in ("full", "skins", "turntable"):
        import eg_bl_export as X
        skins = [o for o in kit_coll.objects if o.type == "MESH" and o.name in X.DESCRIBE]
        names = {k: v[0] for k, v in X.DESCRIBE.items()}
        if opt["renders"] != "turntable":
            R.skin_tiles(skins, opt["out"], names=names)
        if opt["renders"] != "skins":
            R.skin_tiles(skins, opt["out"], size=360, angles=(35.0, 125.0, 215.0, 305.0), names=names,
                         sheet="turntable_sheet.png", cols=8)
    if opt["renders"] in ("full", "mp4"):
        import eg_bl_anim as AN
        AN.timing_mp4(L, rigs, review, os.path.join(opt["out"], "timing_preview.mp4"))

    S.pose(rigs, 0.0)
    blend = os.path.join(opt["out"], "Endgame_work.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend, compress=True)
    if opt["export"]:
        run_export(L, rigs, kit_coll, opt["out"], opt["parts"])
    log("saved", blend, "total", round(time.time() - t_all, 1), "s")


def run_checks(L, rigs, kit_coll, review, outdir):
    import eg_bl_check as C
    t0 = time.time()
    static = [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render and kit_coll not in o.users_collection
              and any(c.name.startswith(("EG_S", "EG_R", "EG_EX", "EG_Rails", "EG_Signals")) for c in o.users_collection)]
    moving_names = {r.parts[i].name for r in rigs for i in C.MOVING.get(r.d["type"], [])}
    static = [o for o in static if o.name not in moving_names]
    inter = C.interference(rigs, static)
    log("interference", round(time.time() - t0, 1), "s")
    path, timing = C.path_check(L)
    cams = [R.camera(f"Budget cam {n}", B.U(*cp), B.U(*tp), lens=lens, coll=review) for n, (cp, tp, lens, _) in shot_list(L).items()]
    bud = C.budget(kit_coll, cams)
    for c in cams:
        bpy.data.objects.remove(c)
    report = dict(interference=inter, path=path, timing=timing, budget=bud)
    with open(os.path.join(outdir, "validation_v1.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=1, default=str)
    log("checks", round(time.time() - t0, 1), "s")
    return report


def run_export(L, rigs, kit_coll, outdir, parts_dir=None):
    import eg_bl_export as X
    rest(rigs)
    parts = [o for o in kit_coll.objects if o.type == "MESH" and o.name.startswith("PR_EG_")]
    parts += [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith(("PR_EG_Floor_", "PR_EG_Walls_"))]
    root = parts_dir or os.path.join(outdir, "parts")
    rows = X.export_all(parts, root)
    X.manifest(S.PLACED, rigs, root, os.path.join(root, "PR_EG_manifest.json"))
    with open(os.path.join(outdir, "export_report.json"), "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    S.pose(rigs, 0.0)


def rest(rigs):
    """Every moving part at zero motion: where Unity's prefab holds it before the clock moves it."""
    import math as _m
    for r in rigs:
        k = r.d["type"]
        if k in ("board", "spinbar"):
            r.parts[0].rotation_euler.z = 0.0
        elif k in ("ferry", "crate"):
            r.parts[0].location.y = 0.0
        elif k == "rookscreen":
            r.parts[0].location.x = 0.0
        elif k == "sliding":
            for i, side in enumerate((-1, 1)):
                r.parts[2 * i].location.x = side * r.d["rest"]
        elif k == "clockgates":
            for i in (1, 2):
                r.parts[i].location.z = 1.6
        elif k == "pendulum":
            r.parts[1].rotation_euler.y = 0.0
        elif k == "lifttiles":
            for p in r.parts[:4]:
                p.location.z = -0.2
        elif k == "fallingpiece":
            r.parts[1].location.z = 9.0
        elif k == "foldingbridge":
            r.parts[2].rotation_euler.y = 0.0
            r.parts[3].rotation_euler.y = 0.0
    bpy.context.view_layer.update()


if __name__ == "__main__":
    main()
