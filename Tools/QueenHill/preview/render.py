#!/usr/bin/env python3
# Renders the Queen of the Hill layout to PNG views without Unity (Tools/QueenHill/README.md).
#   cd Tools/QueenHill/preview && npm install three@0.170.0     (once)
#   python3 Tools/QueenHill/preview/render.py [out_dir] [--time 0]
# Needs Chromium (or Chrome) with WebGL; set CHROME=/path/to/chrome if it is not found.
import functools, http.server, json, os, shutil, socketserver, subprocess, sys, threading

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
LAYOUT = os.path.join(ROOT, "Assets", "Resources", "QueenHill", "QueenHillLayout.json")

# (name, eye, look at, field of view, optional height band to show)
VIEWS = [
    ("01_overview_south_east", (175, 140, -235), (0, 105, 0), 45, None),
    ("02_from_white_bridge", (-4, 15, -80), (4, 32, -40), 64, None),
    ("03_floor1", (70, 95, -130), (0, 24, -40), 50, (8, 44)),
    ("04_floor2", (-70, 121, -130), (0, 50, -40), 50, (40, 70)),
    ("05_floor3", (70, 147, -130), (0, 76, -40), 50, (66, 96)),
    ("06_floor4", (62, 150, -78), (0, 104, 0), 55, (90, 124)),
    ("07_floor5", (-70, 199, -130), (0, 128, -40), 50, (118, 148)),
    ("08_floor6", (70, 225, -130), (0, 154, -40), 50, (144, 174)),
    ("09_floor7_summit", (48, 225, -62), (0, 188, 0), 55, (168, 230)),
    ("10_overview_north_west", (-175, 140, 235), (0, 105, 0), 45, None),
    ("11_white_hub", (34, 64, -92), (0, 42, -42), 55, None),
    ("12_floor1_play_view", (30, 22, -8), (40, 26, -30), 70, None),
]


def chrome():
    for c in (os.environ.get("CHROME"), "/opt/pw-browsers/chromium_headless_shell-1194/chrome-linux/headless_shell",
              "/opt/pw-browsers/chromium-1194/chrome-linux/chrome",
              shutil.which("chromium"), shutil.which("google-chrome"), shutil.which("chrome")):
        if c and os.path.exists(c):
            return c
    sys.exit("Chromium not found: set CHROME=/path/to/chrome")


def main():
    out = next((a for a in sys.argv[1:] if not a.startswith("--")), os.path.join(HERE, "out"))
    t = float(sys.argv[sys.argv.index("--time") + 1]) if "--time" in sys.argv else 0.0
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(HERE, "layout.js"), "w", encoding="utf-8") as f:
        f.write("window.LAYOUT = " + open(LAYOUT, encoding="utf-8").read() + ";\n")
    class Quiet(http.server.SimpleHTTPRequestHandler):
        def log_message(self, *args):
            pass

    handler = functools.partial(Quiet, directory=HERE)
    with socketserver.TCPServer(("127.0.0.1", 0), handler) as httpd:
        port = httpd.server_address[1]
        threading.Thread(target=httpd.serve_forever, daemon=True).start()
        for name, eye, at, fov, band in VIEWS:
            url = "http://127.0.0.1:%d/index.html#eye=%s&at=%s&fov=%s&t=%s&w=1280&h=720" % (
                port, ",".join(map(str, eye)), ",".join(map(str, at)), fov, t)
            if band:
                url += "&clip=%s,%s" % band
            png = os.path.join(out, name + ".png")
            exe = chrome()
            head = [] if exe.endswith("headless_shell") else ["--headless"]
            subprocess.run([exe] + head + ["--no-sandbox", "--use-angle=swiftshader", "--enable-unsafe-swiftshader",
                            "--hide-scrollbars", "--window-size=1280,720", "--virtual-time-budget=20000",
                            "--screenshot=" + png, url], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=240)
            print("wrote", png)
        httpd.shutdown()


if __name__ == "__main__":
    main()
