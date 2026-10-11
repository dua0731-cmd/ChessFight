# sheet.py <glob> <out.png>: a contact sheet of waveform + log spectrogram per sound, labelled with name,
# length and peak, so the AI can see which takes are empty, clipped or the wrong kind of sound.
import glob, os, subprocess, sys
import numpy as np, cv2

def _ffmpeg():
    """FFMPEG env var, else the binary bundled with imageio-ffmpeg, else ffmpeg on PATH."""
    if os.environ.get("FFMPEG"):
        return os.environ["FFMPEG"]
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        return "ffmpeg"


FF = _ffmpeg()
SR = 48000


def load(p):
    raw = subprocess.run([FF, "-v", "error", "-i", p, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"],
                         capture_output=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def tile(x, name, W=420, H=200, span=2.0):
    img = np.full((H, W, 3), 250, np.uint8)
    n = len(x)
    px = int(W * min(1.0, n / SR / span))
    # spectrogram (bottom 130 px), 40 Hz .. 16 kHz, 70 dB range, absolute (not normalised) so quiet takes look dark
    nfft, hop = 1024, 128
    if n > nfft and px > 2:
        fr = np.array([x[i:i + nfft] * np.hanning(nfft) for i in range(0, n - nfft, hop)])
        S = 20 * np.log10(np.abs(np.fft.rfft(fr, axis=1)) + 1e-9)
        f = np.fft.rfftfreq(nfft, 1 / SR)
        rows = np.geomspace(40, 16000, 130)[::-1]
        S = S[:, np.clip(np.searchsorted(f, rows), 0, len(f) - 1)].T
        S = np.clip((S + 30) / 70, 0, 1)  # -30 dB .. +40 dB re. FFT units
        col = cv2.applyColorMap(cv2.resize((S * 255).astype(np.uint8), (px, 130)), cv2.COLORMAP_INFERNO)
        img[70:200, :px] = col
    # waveform (top 70 px), fixed scale so loudness differences show
    xs = np.linspace(0, n - 1, max(px, 2)).astype(int)
    for i in range(len(xs) - 1):
        seg = x[xs[i]:xs[i + 1] + 1]
        if len(seg):
            cv2.line(img, (i, int(38 - seg.max() * 30)), (i, int(38 - seg.min() * 30)), (40, 40, 40), 1)
    pk = 20 * np.log10(np.abs(x).max() + 1e-9)
    cv2.putText(img, f"{name} {n / SR:.2f}s {pk:+.0f}dB", (4, 14), cv2.FONT_HERSHEY_SIMPLEX, 0.42,
                (0, 0, 220) if pk < -20 else (20, 20, 20), 1)
    cv2.rectangle(img, (0, 0), (W - 1, H - 1), (180, 180, 180), 1)
    return img


files = sorted(glob.glob(sys.argv[1]))
tiles = [tile(load(p), os.path.basename(p)[:-4]) for p in files]
cols = 4
while len(tiles) % cols:
    tiles.append(np.full_like(tiles[0], 255))
rows = [np.hstack(tiles[i:i + cols]) for i in range(0, len(tiles), cols)]
cv2.imwrite(sys.argv[2], np.vstack(rows))
print(len(files), "sounds ->", sys.argv[2])
