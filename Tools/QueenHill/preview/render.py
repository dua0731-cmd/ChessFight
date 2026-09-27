#!/usr/bin/env python3
# Renders the Queen of the Hill layout to PNG views without Unity (Tools/QueenHill/README.md).
#   cd Tools/QueenHill/preview && npm install three@0.170.0     (once)
#   python3 Tools/QueenHill/preview/render.py [out_dir] [--time 0]
# Needs Chromium (or Chrome) with WebGL; set CHROME=/path/to/chrome if it is not found.
import functools, http.server, json, os, shutil, socketserver, subprocess, sys, threading

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
LAYOUT = os.path.join(ROOT, "Assets", "Resources", "QueenHill", "QueenHillLayout.json")

VIEWS = [
    ("01_overview_south_east", (120, 95, -190), (0, 82, 0), 45),
    ("02_from_white_bridge", (-4, 14, -52), (6, 34, -12), 64),
    ("03_floor1", (40, 36, -62), (4, 25, -22), 52),
    ("04_floor2", (-12, 62, -72), (0, 46, -24), 52),
    ("05_floor3", (36, 82, -64), (4, 66, -22), 52),
    ("06_floor4", (56, 104, -58), (0, 84, 0), 52),
    ("07_floor5", (34, 120, -72), (2, 106, -24), 52),
    ("08_floor6", (-32, 142, -72), (0, 126, -26), 52),
    ("09_floor7_summit", (40, 172, -52), (0, 148, 0), 50),
    ("10_overview_north_west", (-120, 95, 190), (0, 82, 0), 45),
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
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=HERE)
    handler.log_message = lambda *a: None
    with socketserver.TCPServer(("127.0.0.1", 0), handler) as httpd:
        port = httpd.server_address[1]
        threading.Thread(target=httpd.serve_forever, daemon=True).start()
        for name, eye, at, fov in VIEWS:
            url = "http://127.0.0.1:%d/index.html#eye=%s&at=%s&fov=%s&t=%s&w=1280&h=720" % (
                port, ",".join(map(str, eye)), ",".join(map(str, at)), fov, t)
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
