# coin.py: finds "coin" rings — a bright, narrow, sustained tone above 1.8 kHz (a jingle, chime, bell, ding).
# 승규 님 rejected exactly that in Queen of the Hill (R104: the knockdown take, a jingle whose strongest ring sits near 7.7 kHz for 150 ms).
#   python coin.py "gen/*.mp3"        prints every file with a ring, loudest first
# A ring = one spectral peak 1.8-12 kHz that stands >= 20 dB above its band's median, stays on the same pitch
# (within 3 %) for >= 90 ms, and is no more than 26 dB under the file's loudest moment.
import glob, sys
import numpy as np
from build import load, trim, dc_block, SR

NFFT, HOP = 4096, 512


def rings(x):
    x = np.concatenate([x, np.zeros(max(0, NFFT + HOP - len(x)))])  # very short cues
    win = np.hanning(NFFT)
    frames = [x[i:i + NFFT] * win for i in range(0, max(1, len(x) - NFFT), HOP)]
    if not frames:
        return []
    S = 20 * np.log10(np.abs(np.fft.rfft(np.array(frames), axis=1)) + 1e-9)
    f = np.fft.rfftfreq(NFFT, 1 / SR)
    band = (f >= 1800) & (f <= 12000)
    fb = f[band]
    top = S.max()
    runs, cur = [], None
    for i, row in enumerate(S[:, band]):
        k = int(np.argmax(row))
        stand = row[k] - np.median(row)
        ok = stand >= 20 and row[k] >= top - 26
        if ok and cur and abs(fb[k] - cur["f"]) / cur["f"] < 0.03:
            cur["n"] += 1
            cur["stand"] = max(cur["stand"], stand)
            cur["level"] = max(cur["level"], row[k] - top)
        else:
            if cur and cur["n"] * HOP / SR >= 0.09:
                runs.append(cur)
            cur = dict(f=fb[k], n=1, stand=stand, level=row[k] - top, at=i * HOP / SR) if ok else None
    if cur and cur["n"] * HOP / SR >= 0.09:
        runs.append(cur)
    return runs


if __name__ == "__main__":
    hits = []
    for pat in sys.argv[1:]:
        for p in sorted(glob.glob(pat)):
            r = rings(trim(dc_block(load(p))))
            if r:
                worst = max(r, key=lambda c: c["n"])
                hits.append((worst["n"] * HOP / SR, p, worst))
    for dur, p, w in sorted(hits, reverse=True):
        print(f"{p}: ring {w['f']:.0f} Hz for {dur * 1000:.0f} ms at {w['at']:.2f}s, "
              f"{w['stand']:.0f} dB over band, {w['level']:.0f} dB under top")
    print(len(hits), "files ring")
