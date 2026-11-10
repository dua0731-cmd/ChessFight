# Calibration driver (Blender's python, no bpy): writes a config, renders the four design views, measures them and
# prints ours against the design's numbers. python calib.py <name> '<json overrides>'
import json, os, subprocess, sys
SP = os.path.dirname(os.path.abspath(__file__))
RW = "C:/Users/trews/OneDrive/문서/GitHub/ChessFight/RigWork"
B = r"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
name = sys.argv[1]
over = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
CAMZ = float(os.environ.get("CAMZ", "0.797"))
TEAM = os.environ.get("TEAM", "white")
cam = dict(el=0, target=[0, 0, CAMZ], dist=4.2, lens=118, res=1254)
cfg = {
    "blend": RW + "/pawn_lowpoly/Pawn_Rigged.blend",
    "out": RW + "/eyes/work/" + name,
    "team": TEAM,
    "atlas": RW + "/eyes/atlas/eyes_oval_%s.png" % TEAM,
    "height": RW + "/eyes/atlas/eyes_oval_%s_height.png" % TEAM,
    "eye": {"az": 25.0, "el": -9.0, "w": 0.0705},
    "blush": {"az": 37.0, "el": -27.0, "d": 0.063, "aspect": 0.95, "colour": [0.96, 0.60, 0.54], "alpha": 1.0},
    "methods": ["lens"],
    "samples": 24,
    "views": [dict(name="front", turn=0, **cam), dict(name="left", turn=77, **cam),
              dict(name="right", turn=-77, **cam), dict(name="q34", turn=33, **cam)],
}
for k, v in over.items():
    if isinstance(v, dict) and isinstance(cfg.get(k), dict):
        cfg[k].update(v)
    else:
        cfg[k] = v
os.makedirs(cfg["out"], exist_ok=True)
cp = os.path.join(cfg["out"], "config.json")
json.dump(cfg, open(cp, "w"), indent=1)
r = subprocess.run([B, "-b", "--factory-startup", "-P", os.path.join(SP, "pawn_face.py"), "--", cp], capture_output=True, text=True)
if "DONE" not in r.stdout:
    print(r.stdout[-3000:], r.stderr[-3000:]); sys.exit(1)
meth = cfg["methods"][-1]
imgs = [os.path.join(cfg["out"], "%s_%s.png" % (v["name"], meth)) for v in cfg["views"]]
mj = os.path.join(cfg["out"], "measure.json")
r = subprocess.run([B, "-b", "--factory-startup", "-P", os.path.join(SP, "face_measure.py"), "--", mj, "--team", TEAM] + imgs, capture_output=True, text=True)
ours = json.load(open(mj))
design = json.load(open(RW + "/eyes/work/design_%s_pawn.json" % TEAM))
def avg_front(m):
    e = m["eyes"]; b = m["blush"]
    out = dict(ex=(abs(e[0]["centre"][0]) + abs(e[1]["centre"][0])) / 2, ey=(e[0]["centre"][1] + e[1]["centre"][1]) / 2,
               ew=(e[0]["box"][0] + e[1]["box"][0]) / 2, eh=(e[0]["box"][1] + e[1]["box"][1]) / 2,
               tilt=(abs(e[0]["tilt"]) + abs(e[1]["tilt"])) / 2,
               gx=(e[0]["glint"]["at_box"][0] + e[1]["glint"]["at_box"][0]) / 2 if "glint" in e[0] and "glint" in e[1] else -1,
               gy=(e[0]["glint"]["at_box"][1] + e[1]["glint"]["at_box"][1]) / 2 if "glint" in e[0] and "glint" in e[1] else -1,
               gd=(e[0]["glint"]["diam_eq"] / e[0]["box"][0] + e[1]["glint"]["diam_eq"] / e[1]["box"][0]) / 2 if "glint" in e[0] and "glint" in e[1] else -1,
               bx=(abs(b[0]["centre"][0]) + abs(b[1]["centre"][0])) / 2, by=(b[0]["centre"][1] + b[1]["centre"][1]) / 2,
               bw=(b[0]["box"][0] + b[1]["box"][0]) / 2, bh=(b[0]["box"][1] + b[1]["box"][1]) / 2)
    return out
def side(m):
    e = m["eyes"][0]; b = [bb for bb in m["blush"]]
    b = min(b, key=lambda bb: abs(bb["centre"][1] - 0.55)) if b else None
    g = e.get("glint")
    return dict(ex=abs(e["centre"][0]), ey=e["centre"][1], ew=e["box"][0], eh=e["box"][1], tilt=abs(e["tilt"]),
                gx=(g["at_box"][0] if e["centre"][0] < 0 else 1 - g["at_box"][0]) if g else -1, gy=g["at_box"][1] if g else -1,
                gd=g["diam_eq"] / e["box"][0] if g else -1,
                bx=abs(b["centre"][0]) if b else -1, by=b["centre"][1] if b else -1, bw=b["box"][0] if b else -1, bh=b["box"][1] if b else -1)
rows = [("front", avg_front(ours[0]), avg_front(design[0])),
        ("left", side(ours[1]), side(design[1])), ("right", side(ours[2]), side(design[2]))]
keys = ["ex", "ey", "ew", "eh", "tilt", "gx", "gy", "gd", "bx", "by", "bw", "bh"]
print("%-6s %s" % ("", " ".join("%7s" % k for k in keys)))
for nm, o, d in rows:
    print("%-6s %s" % (nm + "  us", " ".join("%7.3f" % o[k] for k in keys)))
    print("%-6s %s" % ("design", " ".join("%7.3f" % d[k] for k in keys)))
q = ours[3]
print("q34 us    eyes", [(round(e["centre"][0], 3), round(e["centre"][1], 3), round(e["box"][0], 3), round(e["box"][1], 3)) for e in q["eyes"]],
      "blush", [(round(b["centre"][0], 3), round(b["centre"][1], 3)) for b in q["blush"]])
q = design[3]
print("q34 design eyes", [(round(e["centre"][0], 3), round(e["centre"][1], 3), round(e["box"][0], 3), round(e["box"][1], 3)) for e in q["eyes"]],
      "blush", [(round(b["centre"][0], 3), round(b["centre"][1], 3)) for b in q["blush"]])
