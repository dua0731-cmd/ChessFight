"""소드 파이트 스킬 프리비즈 — 페이지를 띄우고 브라우저가 보낸 프레임을 mp4로 묶는다.

python -I server.py <port> <frames_dir> <video_dir>
  GET  /...                 이 폴더의 파일(index.html, previz.js ...)
  POST /frame/<name>/<i>    JPEG 한 장을 <frames_dir>/<name>/<i>.jpg 로 저장
  POST /batch/<name>/<i>    JPEG 여러 장(X-Sizes 헤더 = 각 길이)을 i번부터 저장
  POST /encode/<name>       저장된 프레임을 <video_dir>/<name>.mp4 (H.264, 30fps)로 묶음
  POST /concat/<name>       본문의 영상 이름들을 순서대로 이어 <video_dir>/<name>.mp4
"""
import http.server
import os
import subprocess
import sys

import imageio_ffmpeg

ROOT = os.path.dirname(os.path.abspath(__file__))
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
FRAMES = os.path.abspath(sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "frames"))
VIDEOS = os.path.abspath(sys.argv[3] if len(sys.argv) > 3 else os.path.join(ROOT, "out"))


def safe(name):
    return "".join(c for c in name if c.isalnum() or c in "-_")


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k):
        super().__init__(*a, directory=ROOT, **k)

    def log_message(self, fmt, *args):
        pass

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def reply(self, code, text):
        body = text.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        parts = self.path.strip("/").split("/")
        data = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        if len(parts) == 3 and parts[0] == "frame":
            folder = os.path.join(FRAMES, safe(parts[1]))
            os.makedirs(folder, exist_ok=True)
            with open(os.path.join(folder, "%05d.jpg" % int(parts[2])), "wb") as f:
                f.write(data)
            return self.reply(200, "ok")
        if len(parts) == 3 and parts[0] == "batch":   # 여러 장을 한 번에: X-Sizes = 각 JPEG 길이
            folder = os.path.join(FRAMES, safe(parts[1]))
            os.makedirs(folder, exist_ok=True)
            i, at = int(parts[2]), 0
            for n in map(int, self.headers["X-Sizes"].split(",")):
                with open(os.path.join(folder, "%05d.jpg" % i), "wb") as f:
                    f.write(data[at:at + n])
                i, at = i + 1, at + n
            return self.reply(200, "ok")
        if len(parts) == 2 and parts[0] == "concat":   # 본문 = 이어 붙일 영상 이름들(줄마다)
            os.makedirs(VIDEOS, exist_ok=True)
            listing = os.path.join(FRAMES, safe(parts[1]) + "_list.txt")
            with open(listing, "w", encoding="utf-8") as f:
                for name in data.decode("utf-8").split():
                    f.write("file '%s'\n" % os.path.join(VIDEOS, safe(name) + ".mp4").replace("\\", "/"))
            out = os.path.join(VIDEOS, safe(parts[1]) + ".mp4")
            cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error", "-f", "concat", "-safe", "0",
                   "-i", listing, "-c", "copy", "-movflags", "+faststart", out]
            r = subprocess.run(cmd, capture_output=True, text=True)
            return self.reply(200 if r.returncode == 0 else 500, out if r.returncode == 0 else r.stderr)
        if len(parts) == 2 and parts[0] == "encode":
            name = safe(parts[1])
            os.makedirs(VIDEOS, exist_ok=True)
            out = os.path.join(VIDEOS, name + ".mp4")
            cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error",
                   "-framerate", "30", "-i", os.path.join(FRAMES, name, "%05d.jpg"),
                   "-c:v", "libx264", "-preset", "slow", "-crf", "18",
                   "-pix_fmt", "yuv420p", "-movflags", "+faststart", out]
            r = subprocess.run(cmd, capture_output=True, text=True)
            return self.reply(200 if r.returncode == 0 else 500, out if r.returncode == 0 else r.stderr)
        self.reply(404, "no")


if __name__ == "__main__":
    print("previz on http://localhost:%d  frames=%s  videos=%s" % (PORT, FRAMES, VIDEOS), flush=True)
    http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
