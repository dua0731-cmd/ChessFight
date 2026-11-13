import numpy as np
SR = 44100; NFFT, HOP = 4096, 512
def rings(x):
    x = np.concatenate([x, np.zeros(max(0, NFFT + HOP - len(x)))])
    win = np.hanning(NFFT)
    frames = [x[i:i + NFFT] * win for i in range(0, max(1, len(x) - NFFT), HOP)]
    S = 20 * np.log10(np.abs(np.fft.rfft(np.array(frames), axis=1)) + 1e-9)
    f = np.fft.rfftfreq(NFFT, 1 / SR); band = (f >= 1800) & (f <= 12000); fb = f[band]; top = S.max()
    runs, cur = [], None
    for i, row in enumerate(S[:, band]):
        k = int(np.argmax(row)); stand = row[k] - np.median(row)
        ok = stand >= 20 and row[k] >= top - 26
        if ok and cur and abs(fb[k] - cur["f"]) / cur["f"] < 0.03:
            cur["n"] += 1
        else:
            if cur and cur["n"] * HOP / SR >= 0.09: runs.append(cur)
            cur = dict(f=fb[k], n=1, at=i * HOP / SR) if ok else None
    if cur and cur["n"] * HOP / SR >= 0.09: runs.append(cur)
    return runs
