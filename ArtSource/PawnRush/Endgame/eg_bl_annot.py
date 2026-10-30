"""Plan sheets: one top-down orthographic render per beat at 1 px = 2 cm, dimensions drawn with Grease Pencil,
labels as text objects (a Hangul font when one is installed)."""
import math
import os

import bpy

import eg_bl_materials as MAT
import eg_bl_render as R
import eg_bl_util as B
import eg_layout as E

FONTS = ["C:/Windows/Fonts/malgunbd.ttf", "C:/Windows/Fonts/malgun.ttf",
         "/usr/share/fonts/truetype/nanum/NanumSquareB.ttf", "/usr/share/fonts/truetype/nanum/NanumGothicBold.ttf",
         "/System/Library/Fonts/AppleSDGothicNeo.ttc"]
_font = None
LABEL_Y = 14.0     # labels float over everything walked or moving


def font():
    global _font
    if _font is None:
        for p in FONTS:
            if os.path.exists(p):
                _font = bpy.data.fonts.load(p)
                break
    return _font


def _gp_material(name, hexstr):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        bpy.data.materials.create_gpencil_data(m)
        m.grease_pencil.color = MAT.srgb(hexstr)
    return m


def gp_object(coll, name="EG_Dims"):
    gp = bpy.data.grease_pencils.new(name)
    o = bpy.data.objects.new(name, gp)
    coll.objects.link(o)
    gp.materials.append(_gp_material("GP_Dim_Gold", "#FFD36A"))
    gp.materials.append(_gp_material("GP_Dim_White", "#F4F0E6"))
    gp.materials.append(_gp_material("GP_Dim_Hazard", "#FF8A5B"))
    layer = gp.layers.new("dims")
    frame = layer.frames.new(bpy.context.scene.frame_current)
    return o, frame.drawing


def stroke(drawing, pts, radius=0.05, mat=0):
    """A stroke through Unity points (x, z) drawn at LABEL_Y."""
    drawing.add_strokes([len(pts)])
    st = drawing.strokes[len(drawing.strokes) - 1]
    for i, (x, z) in enumerate(pts):
        st.points[i].position = B.U(x, LABEL_Y, z)
        st.points[i].radius = radius
    st.material_index = mat
    return st


def dim_z(drawing, x, z0, z1, tick=0.6, mat=0):
    stroke(drawing, [(x, z0), (x, z1)], mat=mat)
    for z in (z0, z1):
        stroke(drawing, [(x - tick, z), (x + tick, z)], mat=mat)


def dim_x(drawing, z, x0, x1, tick=0.6, mat=0):
    stroke(drawing, [(x0, z), (x1, z)], mat=mat)
    for x in (x0, x1):
        stroke(drawing, [(x, z - tick), (x, z + tick)], mat=mat)


def label(coll, text, x, z, size=0.9, hexstr="#F4F0E6", align="CENTER", name=None):
    cu = bpy.data.curves.new(name or f"Label {text[:12]}", "FONT")
    cu.body = text
    f = font()
    if f:
        cu.font = f
    cu.size = size
    cu.align_x = align
    cu.align_y = "CENTER"
    o = bpy.data.objects.new(cu.name, cu)
    coll.objects.link(o)
    o.location = B.U(x, LABEL_Y + 0.05, z)
    m = bpy.data.materials.get(f"Label {hexstr}") or MAT.emissive(f"Label {hexstr}", hexstr, 3.0)
    cu.materials.append(m)
    return o


DEVICE_NOTE = {
    "board": lambda d: f"회전판 지름 {d['diameter']:.0f} m · {d['timing'].dps:+.0f}°/s",
    "spinbar": lambda d: f"스윕 막대 {d['length']} m · {d['timing'].dps:+.0f}°/s · 높이 {d['y0']}–{d['y1']}",
    "ferry": lambda d: f"Ferry 8×8 · ±{d['travel']:.0f} m · {d['timing'].period:.0f} s",
    "hurdle": lambda d: f"걸림 막대 높이 {d['y0']}–{d['y1']} m",
    "sliding": lambda d: "06 책장 벽 6 m × 2 · 4.5 s",
    "clockgates": lambda d: "09 시계 문 4 m × 2 · 5 s",
    "rookscreen": lambda d: f"룩 차폐판 6 m · ±3 m · {d['timing'].period:g} s",
    "pendulum": lambda d: f"11 진자 ±55° · 4.4 s · 위상 {d['timing'].phase:g} s",
    "lifttiles": lambda d: f"05 타일 ±3 m · 6 s · 위상 {d.get('phase_s', 0):g} s",
    "vent": lambda d: f"15 옆바람 · 6.8 s · 위상 {d['timing'].phase:g} s",
    "conveyor": lambda d: f"08 벨트 6×{d['length']:.0f} · 북쪽 {d['speed']} m/s",
    "fallingpiece": lambda d: f"10 낙하 말 r 5 · 4.55 s · 위상 {d['timing'].phase:g} s",
    "crate": lambda d: f"사슬 상자 6×3 · ±{d['timing'].distance} m · {d['timing'].period} s",
    "disc": lambda d: "01 원판 지름 8",
    "foldingbridge": lambda d: f"12 접이식 다리 · 13.5 s · 위상 {d['timing'].phase:g} s",
}


def wrap(text, width):
    """Break a note into lines of about `width` characters at spaces or commas."""
    words = text.replace(", ", ",, ").split(" ")
    lines, cur = [], ""
    for w in words:
        w = w.replace(",,", ",")
        if cur and len(cur) + 1 + len(w) > width:
            lines.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}".strip()
    if cur:
        lines.append(cur)
    return "\n".join(lines)


def annotate(L, coll):
    """Dimension lines, beat names, device notes and checkpoints for the whole layout."""
    gpo, dr = gp_object(coll)
    for b in L.beats:
        dim_z(dr, -10.5, b["z0"], b["z1"])
        label(coll, f"{b['z1'] - b['z0']:.1f} m", -11.4, (b["z0"] + b["z1"]) / 2, size=0.75, align="RIGHT")
        label(coll, f"{b['id']}  {b['title']}", -10.0, b["z0"] + 0.9, size=0.85, hexstr="#FFD36A", align="LEFT")
        fl = [f for f in L.floors if f["beat"] == b["id"]]
        if fl:
            widest = max(fl, key=lambda f: f["x1"] - f["x0"])
            dim_x(dr, b["z0"] + 0.4, widest["x0"], widest["x1"], mat=1)
            label(coll, f"폭 {widest['x1'] - widest['x0']:.0f} m · y {widest['top']:g}", 0.0, b["z0"] + 1.1, size=0.6)
    used = {}
    for d in L.devices:
        note = DEVICE_NOTE.get(d["type"])
        if note:
            x, _, z = d["pos"]
            side = 7.0
            key = round(z / 2.5)
            k = used.get(key, 0)
            used[key] = k + 1
            lz = z - k * 1.5
            label(coll, wrap(note(d), 15), side, lz, size=0.5, hexstr="#FF9E6B", align="LEFT")
            stroke(dr, [(x + (1.0 if x >= 0 else -1.0), z), (side - 0.2, lz)], radius=0.025, mat=2)
    for idx, x, y, z in L.checkpoints:
        label(coll, f"CP{idx}", x, z, size=1.1, hexstr="#7CF2C8")
        dim_x(dr, z, -5.8, 5.8, tick=0.3, mat=0)
    return gpo


def beat_sheets(L, coll, outdir, px_per_m=50, samples=8, beats=None):
    """One sheet per beat, north up, 1 px = 1 / px_per_m m (50 = 2 cm)."""
    paths = []
    # the hall is left out: the course reads best on the dark of the drop
    hall = bpy.data.collections.get("EG_Hall")
    overhead = list(hall.objects) if hall else []
    overhead += [o for o in bpy.data.objects if o.name.startswith("R5b crane rail")]
    for o in overhead:
        o.hide_render = True
    for b in L.beats:
        if beats and b["id"] not in beats:
            continue
        z0, z1 = b["z0"] - 2.0, b["z1"] + 2.0
        w, h = 30.0, z1 - z0
        cam = R.camera(f"Cam sheet {b['id']}", B.U(0, 60, (z0 + z1) / 2), B.U(0, 0, (z0 + z1) / 2), coll=coll,
                       ortho=max(w, h))
        cam.rotation_euler = (0, 0, 0)
        R.setup("EEVEE", int(w * px_per_m), int(h * px_per_m), samples, raytrace=False)
        if h > w:
            cam.data.sensor_fit = "VERTICAL"
        else:
            cam.data.sensor_fit = "HORIZONTAL"
        paths.append(R.render(cam, os.path.join(outdir, f"sheet_{b['id']}.png")))
    for o in overhead:
        o.hide_render = False
    return paths
