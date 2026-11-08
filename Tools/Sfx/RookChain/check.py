import glob, os, subprocess, sys
import numpy as np
import imageio_ffmpeg
sys.path.insert(0, "C:/Work/ChessFight-SkillNet/Tools/Sfx/PawnRushSkills")
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 48000


def load(path):
    raw = subprocess.run([FF, "-v", "quiet", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def rings(x):
    NFFT, HOP = 4096, 512
    x = np.concatenate([x, np.zeros(max(0, NFFT + HOP - len(x)))])
    win = np.hanning(NFFT)
    frames = np.array([x[i:i + NFFT] * win for i in range(0, max(1, len(x) - NFFT), HOP)])
    S = 20 * np.log10(np.abs(np.fft.rfft(frames, axis=1)) + 1e-9)
    f = np.fft.rfftfreq(NFFT, 1 / SR)
    band = (f >= 1800) & (f <= 12000)
    fb = f[band]
    top = S.max()
    best, cur = 0, None
    for row in S[:, band]:
        k = int(np.argmax(row))
        ok = row[k] - np.median(row) >= 20 and row[k] >= top - 26
        if ok and cur is not None and abs(fb[k] - cur[0]) / cur[0] < 0.03:
            cur[1] += 1
        elif ok:
            cur = [fb[k], 1]
        else:
            cur = None
        if cur:
            best = max(best, cur[1])
    return best * HOP / SR * 1000  # longest ring, ms


def centroid(x):
    sp = np.abs(np.fft.rfft(x * np.hanning(len(x)))) if len(x) > 16 else np.zeros(2)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return float((sp * f).sum() / max(1e-9, sp.sum()))


for path in sorted(glob.glob(sys.argv[1] if len(sys.argv) > 1 else "C:/tmp_sfx/*.mp3")):
    x = load(path)
    peak = 20 * np.log10(np.abs(x).max() + 1e-9)
    rms = 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-9)
    print(f"{os.path.basename(path):24s} {len(x) / SR:4.2f}s peak {peak:6.1f} rms {rms:6.1f} centroid {centroid(x):6.0f} Hz ring {rings(x):5.0f} ms")
