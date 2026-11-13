# Finds "boing"/twang/whistle: one spectral peak 120-1800 Hz standing >= 18 dB over its band's median for >= 120 ms,
# its pitch allowed to slide (<= 8 % a hop). Run on the old rook takes too, to see it catches what 승규 hated.
import sys, glob, subprocess, numpy as np, imageio_ffmpeg
ff = imageio_ffmpeg.get_ffmpeg_exe(); SR = 22050; NFFT, HOP = 2048, 256
def load(p):
    raw = subprocess.run([ff, '-v', 'quiet', '-i', p, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)
for pat in sys.argv[1:]:
    for p in sorted(glob.glob(pat)):
        x = load(p); x = np.concatenate([x, np.zeros(NFFT)])
        w = np.hanning(NFFT)
        S_ = 20 * np.log10(np.abs(np.fft.rfft(np.array([x[i:i + NFFT] * w for i in range(0, len(x) - NFFT, HOP)]), axis=1)) + 1e-9)
        f = np.fft.rfftfreq(NFFT, 1 / SR); band = (f >= 120) & (f <= 1800); fb = f[band]; top = S_.max()
        best, cur, last = 0, 0, None
        for row in S_[:, band]:
            k = int(np.argmax(row)); ok = row[k] - np.median(row) >= 18 and row[k] >= top - 20
            if ok and last is not None and abs(fb[k] - last) / last <= 0.08: cur += 1
            else: cur = 1 if ok else 0
            last = fb[k] if ok else None
            best = max(best, cur)
        ms = best * HOP / SR * 1000
        print(f"{p.split('/')[-1].split(chr(92))[-1]:28s} longest tone {ms:5.0f} ms {'BOING?' if ms >= 120 else ''}")
