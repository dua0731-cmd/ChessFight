"""Timing previews (brief §9.11): the devices move by their real clock functions. Sine devices get drivers whose
simple expressions are the Unity formulas (frame / fps on the shared clock); the piecewise ones (pendulum pauses,
folding bridge states, falling piece) are keyed every frame from eg_layout. Workbench renders, one MP4."""
import math
import os

import bpy

import eg_bl_render as R
import eg_bl_scene as S
import eg_bl_util as B
import eg_layout as E

FPS = 12          # the clock runs frame / FPS; 12 fps keeps the software renderer under half an hour


def _driver(obj, path, index, expr):
    fc = obj.driver_add(path, index)
    fc.driver.type = "SCRIPTED"
    fc.driver.expression = expr
    for m in list(fc.modifiers):
        fc.modifiers.remove(m)
    return fc


def drive(rigs):
    """Drivers for the sine motions: t = frame / 24 on the shared clock."""
    t = f"(frame/{FPS})"
    for r in rigs:
        d, k = r.d, r.d["type"]
        tm = d.get("timing")
        if k in ("ferry", "crate"):
            _driver(r.parts[0], "location", 1, f"{tm.distance}*sin(2*pi*({t}/{tm.period}+{tm.phase:.6f}))")
        elif k == "rookscreen":
            _driver(r.parts[0], "location", 0, f"{tm.distance}*sin(2*pi*({t}/{tm.period}+{tm.phase:.6f}))")
        elif k in ("board", "spinbar"):
            _driver(r.parts[0], "rotation_euler", 2, f"-radians({t}*{tm.dps}+{tm.phase * 360:.4f})")
        elif k == "lifttiles":
            for p in r.parts[:4]:
                ix, iz = p.name.split()[-1].split("-")
                ph = 0.0 if ix == iz else 0.5
                m = E.Motion("translate", distance=3.0, period=6.0, phase=ph).shift(d.get("phase_s", 0.0))
                _driver(p, "location", 2, f"-0.2+3*sin(2*pi*({t}/6+{m.phase:.6f}))")
        elif k == "sliding":
            base = E.Motion("gate", distance=d["travel"], period=4.5).shift(d.get("phase_s", 0.0))
            for i, side in enumerate((-1, 1)):
                ph = E.repeat(base.phase + (0.0 if side < 0 else 0.5), 1.0)
                _driver(r.parts[2 * i], "location", 0,
                        f"{side}*({d['rest']}-{d['travel']}*smoothstep(-0.35,0.35,sin(2*pi*({t}/4.5+{ph:.6f}))))")
        elif k == "clockgates":
            for i, ph in enumerate((0.0, 0.5)):
                m = E.Motion("gate", distance=4.1, period=5.0, phase=ph).shift(d.get("phase_s", 0.0))
                _driver(r.parts[1 + i], "location", 2, f"1.6+4.1*smoothstep(-0.35,0.35,sin(2*pi*({t}/5+{m.phase:.6f})))")


def key_piecewise(rigs, f0, f1):
    """Pendulums, folding bridges and falling pieces keyed every frame from the clock functions."""
    sc = bpy.context.scene
    for f in range(f0, f1 + 1):
        t = f / FPS
        for r in rigs:
            k, tm = r.d["type"], r.d.get("timing")
            if k == "pendulum":
                r.parts[1].rotation_euler.y = math.radians(-tm.angle(t))
                r.parts[1].keyframe_insert("rotation_euler", index=1, frame=f)
            elif k == "foldingbridge":
                _, ang = tm.state(t)
                for p in r.parts[2:4]:
                    p.rotation_euler.y = math.radians(-ang)
                    p.keyframe_insert("rotation_euler", index=1, frame=f)
            elif k == "fallingpiece":
                r.parts[1].location.z = tm.piece_height(t)
                r.parts[1].keyframe_insert("location", index=2, frame=f)
    for a in bpy.data.actions:
        for fc in getattr(a, "fcurves", []):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"


SEGMENTS = [
    # (title, seconds, camera Unity pos, target, overlay function name)
    ("S2 첫 작전교: 탑승 창 2.55 s / 12 s, 걸림 막대", 12.0, (-11.0, 7.0, 36.0), (0.0, 0.0, 51.0), "s2"),
    ("R3 깃발 다리: 진자 2개, 폭 6 m", 8.8, (-12.0, 8.0, 112.0), (0.0, 1.5, 122.0), "r3"),
    ("R5 벨트 · 낙하 말: 같은 쪽이 친 직후 올라타기", 9.1, (-10.0, 10.0, 147.0), (0.0, 3.0, 161.0), "r5"),
    ("R5b 사슬 상자: 뛰기 창 2.25–2.45 s", 8.8, (-12.0, 9.0, 171.0), (0.0, 3.0, 181.0), "r5b"),
    ("S4 마지막 작전교: 다리 + 차폐판 2.5 s", 13.5, (-12.0, 10.0, 199.0), (0.0, 3.0, 213.0), "s4"),
]


def overlay_text(L, which, t):
    if which == "s2":
        f = L.device_by_id("S2_ferry")["timing"]
        off = f.value(t)
        gap_s = (50.2 + off - 4.0) - 40.0
        gap_n = 60.4 - (50.2 + off + 4.0)
        state = "남쪽 탑승 가능" if gap_s <= 1.5 else "북쪽 하선 가능" if gap_n <= 1.5 else "건너는 중"
        return f"갑판 {off:+.2f} m · 남 틈 {gap_s:.2f} · 북 틈 {gap_n:.2f} · {state}"
    if which == "r3":
        a = [d["timing"].angle(t) for d in L.devices if d["type"] == "pendulum"]
        return "진자 각도 " + " / ".join(f"{x:+.0f}°" for x in a)
    if which == "r5":
        p = [d for d in L.devices if d["type"] == "fallingpiece"]
        return " · ".join(f"{d['id'][-1]} {'충격파' if d['timing'].shocking(t) else '경고' if d['timing'].warned(t) else '쉼'}" for d in p)
    if which == "r5b":
        c = [d for d in L.devices if d["type"] == "crate"]
        return "상자 " + " / ".join(f"{d['timing'].value(t):+.2f}" for d in c)
    if which == "s4":
        b = [d for d in L.devices if d["type"] == "foldingbridge"]
        names = dict(open="열림", warning="경고", folding="접힘", closed="접힌 채", unfolding="펴짐")
        sc = L.device_by_id("S4_screen")["timing"].value(t)
        return " · ".join(f"{d['id'][-1]} {names[d['timing'].state(t)[0]]}" for d in b) + f" · 차폐판 {sc:+.1f} m"
    return ""


def timing_mp4(L, rigs, coll, path, width=854, height=480):
    sc = bpy.context.scene
    try:
        bpy.context.preferences.filepaths.use_scripts_auto_execute = True
    except Exception:
        pass
    sc.render.fps = FPS
    drive(rigs)
    frame = 1
    plan = []
    for title, secs, cp, tp, which in SEGMENTS:
        n = int(round(secs * FPS))
        cam = R.camera(f"Cam timing {which}", B.U(*cp), B.U(*tp), lens=24, coll=coll)
        m = sc.timeline_markers.new(which, frame=frame)
        m.camera = cam
        plan.append((frame, frame + n - 1, title, which, cam))
        frame += n
    sc.frame_start, sc.frame_end = 1, frame - 1
    key_piecewise(rigs, 1, frame - 1)
    # overlay: a text object in front of each camera, rewritten every frame
    import eg_bl_annot as A
    txt = bpy.data.curves.new("Timing overlay", "FONT")
    if A.font():
        txt.font = A.font()
    txt.size = 0.017
    txt.align_x = "LEFT"
    to = bpy.data.objects.new("Timing overlay", txt)
    coll.objects.link(to)
    mat = bpy.data.materials.get("Label #F4F0E6") or __import__("eg_bl_materials").emissive("Label #F4F0E6", "#F4F0E6", 3.0)
    txt.materials.append(mat)

    def update(scene, depsgraph=None):
        f = scene.frame_current
        for f0, f1, title, which, cam in plan:
            if f0 <= f <= f1:
                t = (f - f0) / FPS
                to.parent = cam
                to.location = (-0.36, 0.185, -0.5)
                to.rotation_euler = (0, 0, 0)
                txt.body = f"{title}\nt = {t:5.2f} s   {overlay_text(L, which, f / FPS)}"
                break

    bpy.app.handlers.frame_change_pre.clear()
    bpy.app.handlers.frame_change_pre.append(update)
    # the hall is looks only: hidden here so the timing preview renders in minutes, not hours
    hall = bpy.data.collections.get("EG_Hall")
    if hall:
        hall.hide_render = True
    R.setup("WORKBENCH", width, height)
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sc.display.shading.show_shadows = True
    if hasattr(sc.render.image_settings, "media_type"):      # Blender 5: video is its own media type
        sc.render.image_settings.media_type = "VIDEO"
    sc.render.image_settings.file_format = "FFMPEG"
    sc.render.ffmpeg.format = "MPEG4"
    sc.render.ffmpeg.codec = "H264"
    sc.render.ffmpeg.constant_rate_factor = "MEDIUM"
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.camera = plan[0][4]
    if os.environ.get("EG_MP4_FRAMES"):
        sc.frame_end = sc.frame_start + int(os.environ["EG_MP4_FRAMES"]) - 1
    bpy.ops.render.render(animation=True)
    if hasattr(sc.render.image_settings, "media_type"):
        sc.render.image_settings.media_type = "IMAGE"
    sc.render.image_settings.file_format = "PNG"
    return path
