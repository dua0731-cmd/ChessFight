// Records the previz clips frame by frame: a small static server, headless Edge over the DevTools protocol,
// and Krita's ffmpeg (this PC has no other; it has no libx264, so libopenh264 at a fixed bit rate).
//   node record.mjs --clips pawnA,pawnB --out out/pawn.mp4
//   node record.mjs --sheets pawnA,pawnB      (a 3x3 contact sheet per clip in out/sheets)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const EDGE = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
const FFMPEG = 'C:/Program Files/Krita (x64)/bin/ffmpeg.exe';
const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : d; };
const FPS = Number(arg('fps', 30)), RATE = arg('rate', '5000k'), OUT = path.resolve(ROOT, arg('out', 'out/previz.mp4'));
const PROFILE = arg('profile', path.join(os.tmpdir(), 'pr-end-edge'));
fs.mkdirSync(path.dirname(OUT), { recursive: true });

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8', '.json': 'application/json', '.css': 'text/css' };
const server = http.createServer((req, res) => {
  const p = path.join(ROOT, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(ROOT)) { res.writeHead(403); res.end(); return; }
  fs.readFile(p, (err, data) => { if (err) { res.writeHead(404); res.end(); return; } res.writeHead(200, { 'Content-Type': MIME[path.extname(p)] ?? 'application/octet-stream' }); res.end(data); });
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const PORT = server.address().port, DPORT = 9300 + Math.floor(Math.random() * 500), PROF = `${PROFILE}-${DPORT}`;

const edge = spawn(EDGE, ['--headless=new', `--remote-debugging-port=${DPORT}`, `--user-data-dir=${PROF}`, '--window-size=1280,720', '--hide-scrollbars',
  '--mute-audio', '--no-first-run', '--no-default-browser-check', '--disable-extensions', '--force-device-scale-factor=1', 'about:blank'], { stdio: 'ignore' });
let cdpNow = null;
const stop = code => { try { cdpNow?.ws.close(); } catch {} const k = spawn('taskkill', ['/PID', String(edge.pid), '/T', '/F'], { stdio: 'ignore' }); server.close(); k.on('exit', () => process.exit(code)); setTimeout(() => process.exit(code), 3000); };

class CDP {
  constructor(url) {
    this.ws = new WebSocket(url); this.id = 0; this.wait = new Map();
    this.ws.onmessage = e => {
      const m = JSON.parse(e.data);
      if (m.id && this.wait.has(m.id)) { const [ok, ko] = this.wait.get(m.id); this.wait.delete(m.id); m.error ? ko(new Error(m.error.message)) : ok(m.result); }
      else if (m.method === 'Runtime.consoleAPICalled') console.log('[page]', ...m.params.args.map(a => a.value ?? a.description));
      else if (m.method === 'Runtime.exceptionThrown') console.log('[page error]', m.params.exceptionDetails?.exception?.description ?? m.params.exceptionDetails?.text);
    };
  }
  open() { return new Promise((ok, ko) => { this.ws.onopen = ok; this.ws.onerror = ko; }); }
  send(method, params = {}) { const id = ++this.id; this.ws.send(JSON.stringify({ id, method, params })); return new Promise((ok, ko) => this.wait.set(id, [ok, ko])); }
  async eval(expr) {
    const r = await this.send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
    return r.result.value;
  }
}

try {
  let target = null;
  for (let i = 0; i < 100 && !target; i++) {
    try { const list = await (await fetch(`http://127.0.0.1:${DPORT}/json/list`)).json(); target = list.find(t => t.type === 'page'); } catch {}
    if (!target) await new Promise(r => setTimeout(r, 200));
  }
  if (!target) throw new Error('headless Edge did not open a page');
  const cdp = cdpNow = new CDP(target.webSocketDebuggerUrl); await cdp.open();
  await cdp.send('Runtime.enable'); await cdp.send('Page.enable');
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
  await cdp.send('Page.navigate', { url: `http://127.0.0.1:${PORT}/index.html` });
  let ready = false;
  for (let i = 0; i < 300 && !ready; i++) { try { ready = await cdp.eval('window.PV ? window.PV.ready : false'); } catch {} if (!ready) await new Promise(r => setTimeout(r, 200)); }
  if (!ready) throw new Error('the page did not get ready (three.js from the CDN?)');
  const shot = async () => Buffer.from((await cdp.send('Page.captureScreenshot', { format: 'jpeg', quality: 92, optimizeForSpeed: true })).data, 'base64');

  // --sheets a,b: a 3x3 contact sheet per clip, at the nine moments the clip names (PV.sheetTimes)
  const sheets = arg('sheets');
  if (sheets) {
    const dir = path.join(ROOT, 'out', 'stills'); fs.mkdirSync(dir, { recursive: true });
    for (const id of sheets.split(',')) {
      let dur; try { dur = await cdp.eval(`PV.load(${JSON.stringify(id)})`); } catch (e) { console.log(`${id}: FAILED ${String(e.message).slice(0, 400)}`); continue; }
      const times = await cdp.eval('window.PV.sheetTimes()');
      for (let i = 0; i < times.length; i++) { await cdp.eval(`PV.render(${times[i]})`); fs.writeFileSync(path.join(dir, `s_${String(i).padStart(2, '0')}.jpg`), await shot()); }
      const out = path.join(ROOT, 'out', 'sheets', `${id}.jpg`); fs.mkdirSync(path.dirname(out), { recursive: true });
      await new Promise((ok, ko) => { const f = spawn(FFMPEG, ['-y', '-loglevel', 'error', '-i', path.join(dir, 's_%02d.jpg'), '-vf', 'scale=640:360,tile=3x3', '-frames:v', '1', '-fps_mode', 'passthrough', out], { stdio: 'inherit' }); f.on('exit', c => (c ? ko(new Error('ffmpeg ' + c)) : ok())); });
      console.log(`${id}: ${dur.toFixed(2)} s -> ${out} (${times.map(t => t.toFixed(1)).join(' ')})`);
    }
    for (const f of fs.readdirSync(dir)) fs.unlinkSync(path.join(dir, f));
  } else {
    const ids = arg('clips').split(',');
    const ff = spawn(FFMPEG, ['-y', '-loglevel', 'error', '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'mjpeg', '-i', '-', '-c:v', 'libopenh264', '-b:v', RATE, '-profile:v', 'high', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', OUT], { stdio: ['pipe', 'inherit', 'inherit'] });
    const done = new Promise((ok, ko) => ff.on('exit', c => (c ? ko(new Error('ffmpeg ' + c)) : ok())));
    const t0 = Date.now(); let frames = 0;
    for (const id of ids) {
      const dur = await cdp.eval(`PV.load(${JSON.stringify(id)})`), n = Math.round(dur * FPS);
      for (let f = 0; f < n; f++) {
        await cdp.eval(`PV.render(${f / FPS})`);
        if (!ff.stdin.write(await shot())) await new Promise(r => ff.stdin.once('drain', r));
        frames++;
      }
      console.log(`${id}: ${dur.toFixed(2)} s, ${n} frames (${((Date.now() - t0) / 1000).toFixed(0)} s so far)`);
    }
    ff.stdin.end(); await done;
    console.log(`video ${OUT}: ${frames} frames, ${(fs.statSync(OUT).size / 1048576).toFixed(1)} MiB`);
  }
  stop(0);
} catch (e) { console.error(e); stop(1); }
