# Piece calibration driver (Blender's python, no bpy): builds the atlas with the given style values, renders the piece's
# front view, measures it with measure_piece_eyes.py and prints it under the design's numbers.
# python piece_calib.py <rook|bishop> <white|black> <name> '<json: style_over, eye, blush, ...>'
import json, os, subprocess, sys
SP = os.path.dirname(os.path.abspath(__file__))
RW = "C:/Users/trews/OneDrive/문서/GitHub/ChessFight/RigWork"
B = r"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
DES = "C:/Users/trews/.codex/visualizations/2026/11/09/01a1bf32-96d3-7940-bc68-578364f54aa6/ChessFight/FinalMultiview/WhiteBlack_WithFace_BodyOnly_v1"
piece, team, name = sys.argv[1:4]
over = json.loads(sys.argv[4]) if len(sys.argv) > 4 else {}
style = {"rook": "rect", "bishop": "diamond"}[piece]
out = RW + "/eyes/work/%s_%s_%s" % (piece, team, name)
os.makedirs(out, exist_ok=True)
so = over.pop("style_over", {})
r = subprocess.run([B, "-b", "--factory-startup", "-P", os.path.join(SP, "make_eye_atlas.py"), "--", out, style, team, "eyes_" + style,
                    "--style-json", json.dumps(so)], capture_output=True, text=True)
assert "ATLAS" in r.stdout, r.stdout[-2000:] + r.stderr[-2000:]
cfg = {
    "body": "C:/Users/trews/Downloads/%s.fbx" % piece, "out": out, "team": team, "style": style,
    "atlas": out + "/eyes_%s_%s.png" % (style, team), "height": out + "/eyes_%s_%s_height.png" % (style, team),
    "methods": ["lensbevel"], "bump": 0.0015, "samples": 32,
    "eye_spec": 0.25 if team == "white" else 0.2, "eye_rough": 0.18 if team == "white" else 0.4,
    "style_over": so, "lathe_sides": 20,
    "body_blend": RW + "/pieces/models/%s_LowPoly.blend" % piece.capitalize(), "body_object": piece.capitalize(), "smooth": "mesh",
}
if team == "black":
    cfg.update(eye_albedo=0.2, glow=0.9)
cfg.update(over)
z_cam = cfg["eye"]["z"]
cfg.setdefault("views", [{"name": "front", "turn": 0, "el": 0, "target": [0, 0, z_cam], "dist": 4.2, "lens": 100, "res": 1254}])
cp = os.path.join(out, "config.json")
json.dump(cfg, open(cp, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
r = subprocess.run([B, "-b", "--factory-startup", "-P", os.path.join(SP, "piece_face.py"), "--", cp], capture_output=True, text=True)
if "DONE" not in r.stdout:
    print(r.stdout[-3000:], r.stderr[-3000:]); sys.exit(1)
print([l for l in r.stdout.splitlines() if l.startswith("PROFILE")][0])
img = out + "/front_lensbevel.png"
dimg = DES + "/%s_%s_%s_front.png" % (team, {"rook": "09", "bishop": "13"}[piece], piece)
for lab, p in (("design", dimg), ("ours", img)):
    r = subprocess.run([B, "-b", "--factory-startup", "-P", os.path.join(SP, "measure_piece_eyes.py"), "--", team, p], capture_output=True, text=True)
    print("==", lab)
    for l in r.stdout.splitlines():
        if l.startswith(("IMG", "  eye", "    glint", "    outline", "  blush")):
            print(l)
