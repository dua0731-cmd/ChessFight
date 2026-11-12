# Local preview server: wraps index.html in the publish skeleton (doctype + charset) as /dev.html.
import http.server, os, sys
ROOT = sys.argv[1]
class H(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k): super().__init__(*a, directory=ROOT, **k)
    def do_GET(self):
        if self.path.split('?')[0] == '/dev.html':
            body = open(os.path.join(ROOT, 'index.html'), encoding='utf-8').read()
            html = '<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"></head><body>' + body + '</body></html>'
            b = html.encode('utf-8')
            self.send_response(200); self.send_header('Content-Type', 'text/html; charset=utf-8'); self.send_header('Cache-Control', 'no-store'); self.send_header('Content-Length', str(len(b))); self.end_headers(); self.wfile.write(b); return
        return super().do_GET()
    def do_POST(self):
        # Saves a captured frame: POST /shot?name=xyz with a data: URL body.
        import base64, urllib.parse
        q = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
        name = ''.join(ch for ch in q.get('name', ['shot'])[0] if ch.isalnum() or ch in '-_.')
        data = self.rfile.read(int(self.headers['Content-Length'])).decode()
        os.makedirs(os.path.join(ROOT, '..', 'shots'), exist_ok=True)
        open(os.path.join(ROOT, '..', 'shots', name + '.png'), 'wb').write(base64.b64decode(data.split(',', 1)[1]))
        self.send_response(204); self.end_headers()
    def end_headers(self):
        self.send_header('Cache-Control', 'no-store'); super().end_headers()
    def guess_type(self, p):
        t = super().guess_type(p)
        return 'text/javascript; charset=utf-8' if p.endswith('.js') else t
http.server.ThreadingHTTPServer(('127.0.0.1', 8765), H).serve_forever()
