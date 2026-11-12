"""Waveform + spectrogram contact sheet and numbers for every take in raw/ (or the folder given)."""
import glob, os, sys
import numpy as np
import miniaudio
from PIL import Image, ImageDraw

here = os.path.dirname(os.path.abspath(__file__))
src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "raw")
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(here, "sheet_raw.png")


def load(path):
    if path.endswith(".wav"):
        d = miniaudio.wav_read_file_f32(path)
    else:
        d = miniaudio.mp3_read_file_f32(path)
    a = np.frombuffer(d.samples, dtype=np.float32).reshape(-1, d.nchannels).mean(axis=1)
    return a, d.sample_rate


def db(x):
    return 20 * np.log10(max(x, 1e-9))


def coin(a, sr):
    # narrow tonal peak 1.8-12 kHz lasting: compare band peak to band median per 46 ms frame
    n = 2048
    hits = 0
    for i in range(0, len(a) - n, n // 2):
        f = np.abs(np.fft.rfft(a[i:i + n] * np.hanning(n)))
        fr = np.fft.rfftfreq(n, 1 / sr)
        band = (fr > 1800) & (fr < 12000)
        b = f[band]
        if b.max() > 1e-4 and db(b.max()) - db(np.median(b) + 1e-9) > 20:
            hits += 1
    return hits * (n / 2) / sr


files = sorted(glob.glob(os.path.join(src, "*.mp3")) + glob.glob(os.path.join(src, "*.wav")))
W, H = 560, 150
sheet = Image.new("RGB", (W * 3 + 40, (H + 34) * ((len(files) + 2) // 3) + 10), (18, 16, 14))
dr = ImageDraw.Draw(sheet)
for i, path in enumerate(files):
    a, sr = load(path)
    peak = np.abs(a).max()
    rms = np.sqrt(np.mean(a ** 2))
    env = np.abs(a)
    onset = int(np.argmax(env > peak * .2)) / sr
    ipeak = int(np.argmax(env)) / sr
    ring = coin(a, sr)
    # spectrogram
    n = 1024
    hop = max(1, len(a) // W)
    cols = []
    for x in range(W):
        s = x * hop
        seg = a[s:s + n]
        if len(seg) < n:
            seg = np.pad(seg, (0, n - len(seg)))
        cols.append(np.abs(np.fft.rfft(seg * np.hanning(n)))[:n // 2])
    spec = 20 * np.log10(np.array(cols).T + 1e-6)
    # log-frequency rows
    fr = np.fft.rfftfreq(n, 1 / sr)[:n // 2]
    rows = np.geomspace(40, sr / 2 - 1, H)
    idx = np.clip(np.searchsorted(fr, rows), 0, len(fr) - 1)
    img = spec[idx][::-1]
    img = np.clip((img + 30) / 60, 0, 1)
    rgb = (np.stack([img, img ** 1.8, img ** 4], -1) * 255).astype(np.uint8)
    x0 = 10 + (i % 3) * (W + 10)
    y0 = 10 + (i // 3) * (H + 34)
    sheet.paste(Image.fromarray(rgb), (x0, y0 + 20))
    # envelope overlay
    pts = []
    for x in range(W):
        s = x * len(a) // W
        e = env[s:s + max(1, len(a) // W)].max() if s < len(a) else 0
        pts.append((x0 + x, y0 + 20 + H - int(e / max(peak, 1e-9) * (H - 4))))
    dr.line(pts, fill=(120, 220, 255), width=1)
    name = os.path.basename(path).rsplit(".", 1)[0]
    dr.text((x0, y0 + 3), f"{name}  {len(a)/sr:.2f}s pk {db(peak):.0f} rms {db(rms):.0f} on {onset*1000:.0f}ms max {ipeak*1000:.0f}ms ring {ring:.2f}s", fill=(240, 220, 170))
    print(f"{name:14s} len {len(a)/sr:.2f} peak {db(peak):6.1f} rms {db(rms):6.1f} onset {onset*1000:5.0f}ms peakAt {ipeak*1000:5.0f}ms ring {ring:.2f}s")
sheet.save(out)
print("sheet", out)
