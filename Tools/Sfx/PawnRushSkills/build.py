# Lays the generated cues (gen/<dir>_<skill>_<cue>.<ext>) on each skill's timeline and writes the page's audio.
# SPEC=spec_v2 builds the second round (R107: per-piece sound families, gen2/ -> out2/); default spec (R101).
#   out/audio/<skill>_<dir>.mp3   the whole skill in time with its moments (the demo)
#   out/audio/cue_<skill>_<cue>_<dir>.mp3   each cue alone, trimmed and levelled
#   out/meta.json                 what the page draws (moments, frames, parts, file names)
#   out/check/<skill>_<dir>.png   waveform + spectrogram with the moments, for the AI to look at
import glob, importlib, json, os, subprocess, sys
import numpy as np, cv2

SPEC = importlib.import_module(os.environ.get("SPEC", "spec"))
SKILLS = SPEC.SKILLS
PER_PIECE = hasattr(SPEC, "FAMILIES")  # R107: every piece has its own five families
BEDS = getattr(SPEC, "BEDS", set())     # held cues: faded in, and out where they are cut

SR = 48000
HERE = os.path.dirname(os.path.abspath(__file__))
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
OUT = os.path.join(HERE, getattr(SPEC, "OUT", "out"))
GEN = getattr(SPEC, "GEN", "gen")


def letters(skill):
    return list(SPEC.FAMILIES[skill]) if PER_PIECE else list(SPEC.DIRECTIONS)


def prompt(skill, d, cue):
    return SPEC.P[skill][d][cue] if PER_PIECE else SPEC.P[(skill, cue)][d]
for d in ("audio", "check"):
    os.makedirs(os.path.join(OUT, d), exist_ok=True)


def load(path):
    raw = subprocess.run([FF, "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def save_mp3(x, path, kbps=128):
    x = np.clip(x, -1, 1).astype(np.float32)
    subprocess.run([FF, "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libmp3lame", "-b:a", f"{kbps}k", path], input=x.tobytes(), check=True)


def moving_mean(v, n):
    """Centered running mean by cumulative sums (np.convolve with long windows is far too slow)."""
    c = np.concatenate([[0.0], np.cumsum(v)])
    half = n // 2
    i = np.arange(len(v))
    lo = np.clip(i - half, 0, len(v))
    hi = np.clip(i - half + n, 0, len(v))
    return (c[hi] - c[lo]) / n


def env_db(x, win=0.01):
    n = max(1, int(win * SR))
    return 10 * np.log10(moving_mean(x * x, n) + 1e-12)


def dc_block(x, r=0.995):
    """One-pole high-pass near 38 Hz: some takes carry a slow offset or sub-bass drift under the hit."""
    y = np.empty_like(x)
    prev_x = prev_y = 0.0
    for i, v in enumerate(x.tolist()):
        prev_y = v - prev_x + r * prev_y
        prev_x = v
        y[i] = prev_y
    return y


def trim(x, floor_db=-50):
    """Cut the silence before the first sound and after the last, with tiny fades."""
    e = env_db(x, 0.005)
    loud = np.where(e > e.max() + floor_db)[0]
    if len(loud) == 0:
        return x
    a = max(0, loud[0] - int(0.004 * SR))
    b = min(len(x), loud[-1] + int(0.03 * SR))
    y = x[a:b].copy()
    fi, fo = int(0.002 * SR), int(0.02 * SR)
    y[:fi] *= np.linspace(0, 1, fi)
    y[-fo:] *= np.linspace(1, 0, fo)
    return y


# Cues that stand for one hit: the model sometimes gives a row of them (three zips for one step), keep the first.
SINGLE = {"step", "tick", "hit", "push", "turn"}


def first_event(x):
    e = env_db(x, 0.004)
    above = (e > e.max() - 24).astype(int)
    starts = np.where(np.diff(above) == 1)[0]
    if len(starts) == 0:
        return x
    first = starts[0]
    for j in starts[1:]:
        if j - first > int(0.07 * SR) and e[j:j + int(0.03 * SR)].max() > e.max() - 12:
            y = x[:max(int(0.02 * SR), j - int(0.005 * SR))].copy()
            fo = min(int(0.015 * SR), len(y))
            y[-fo:] *= np.linspace(1, 0, fo)
            return y
    return x


def loudness(x):
    """Short-term loudness stand-in: the loudest 400 ms of a K-weighting-like tilt (highs count a bit more)."""
    N = 1 << int(np.ceil(np.log2(len(x) + SR // 2)))
    spec = np.fft.rfft(x, n=N)
    f = np.fft.rfftfreq(N, 1 / SR)
    w = np.where(f < 60, (f / 60) ** 2, 1.0) * np.where(f > 1500, 1.58, 1.0)
    y = np.fft.irfft(spec * w, n=N)[:len(x)]
    n = int(0.4 * SR)
    p = moving_mean(np.concatenate([y * y, np.zeros(n)]), n)
    return 10 * np.log10(p.max() + 1e-12)


def lowpass(x, fc):
    """Second-order-like roll-off above fc (magnitude 1 / sqrt(1 + (f/fc)^4)), done in one FFT."""
    N = 1 << int(np.ceil(np.log2(len(x) + 1)))
    f = np.fft.rfftfreq(N, 1 / SR)
    return np.fft.irfft(np.fft.rfft(x, n=N) / np.sqrt(1 + (f / fc) ** 4), n=N)[:len(x)]


def level(x, target=-16.0):
    return x * 10 ** ((target - loudness(x)) / 20)


def pitch(x, semis):
    if semis == 0:
        return x
    r = 2 ** (semis / 12)
    idx = np.arange(0, len(x) - 1, r)
    return np.interp(idx, np.arange(len(x)), x)


def peak_at(x):
    """Index of the cue's main hit: the loudest 10 ms."""
    return int(np.argmax(env_db(x, 0.01)))


def limit(x, ceil_db=-1.0):
    c = 10 ** (ceil_db / 20)
    pk = np.abs(x).max()
    if pk > c:  # soft knee: gentle tanh above 70 % of the ceiling, then scale in
        k = 0.7 * c
        y = np.where(np.abs(x) > k, np.sign(x) * (k + (c - k) * np.tanh((np.abs(x) - k) / (c - k))), x)
        return y
    return x


def gen_path(d, skill, cue):
    src = getattr(SPEC, "REUSE", {}).get((skill, d, cue))  # an R101 take kept as it is
    folder, d = ("gen", src) if src else (GEN, d)
    hits = glob.glob(os.path.join(HERE, folder, f"{d}_{skill}_{cue}.*"))
    return hits[0] if hits else None


def build(only=None):
    meta = {"skills": {}}
    if PER_PIECE:
        meta["families"] = {k: {L: dict(name=n, line=l) for L, (n, l) in v.items()} for k, v in SPEC.FAMILIES.items()}
    else:
        meta["directions"] = SPEC.DIRECTIONS
    for skill, s in SKILLS.items():
        if only and skill not in only:
            continue
        sm = dict(name=s["name"], key=s["key"], length=s["length"], moments=s["moments"],
                  frames=[(f"{skill}_{n}.jpg", t, label) for n, t, label in s["frames"]],
                  parts=s["parts"], cues=list(s["cues"]), tracks={})
        for d in letters(skill):
            cues = {}
            for cue in s["cues"]:
                cut = getattr(SPEC, "DERIVE", {}).get((skill, d, cue))
                p = gen_path(d, skill, cut[0] if cut else cue)
                if p is None:
                    break
                x = trim(dc_block(load(p)))
                fc = getattr(SPEC, "LOWPASS", {}).get((skill, d, cue))
                if fc:
                    x = lowpass(x, fc)
                if cut:  # the start of a longer take of the same family, re-pitched
                    x = x[:int(cut[2] * SR)].copy()
                    fo = min(int(0.03 * SR), len(x))
                    x[-fo:] *= np.linspace(1, 0, fo)
                    x = pitch(x, cut[3])
                if cue in SINGLE:
                    x = trim(first_event(x))
                cues[cue] = level(x)
            if len(cues) != len(s["cues"]):
                print(f"skip {skill} {d}: missing cues")
                continue
            n = int((s["length"] + 0.5) * SR)
            mix = np.zeros(n)
            clips = dict((c, list(v)) for c, v in s.get("clip", {}).items())
            for cue, t, align, semis, gain in s["timeline"]:
                x = pitch(cues[cue], semis)
                if cue in clips and clips[cue]:
                    t0, keep = clips[cue].pop(0)
                    k = int(keep * SR)
                    if len(x) > k:
                        x = x[:k].copy()
                        fo = min(int((0.12 if cue in BEDS else 0.05) * SR), k)
                        x[-fo:] *= np.linspace(1, 0, fo)
                if cue in BEDS:
                    x = x.copy()
                    fi = min(int(0.06 * SR), len(x))
                    x[:fi] *= np.linspace(0, 1, fi)
                if align == "end" and cue == "windup":  # the windup is 0.35 s: keep its last part only
                    k = int(0.38 * SR)
                    if len(x) > k:
                        x = x[-k:].copy()
                        fi = int(0.03 * SR)
                        x[:fi] *= np.linspace(0, 1, fi)
                if align == "start":
                    at = int(t * SR)
                elif align == "peak":
                    at = int(t * SR) - peak_at(x)
                else:
                    at = int(t * SR) - len(x)
                a, b = max(0, at), min(n, at + len(x))
                if b > a:
                    mix[a:b] += x[a - at:b - at] * 10 ** (gain / 20)
            last = np.where(np.abs(mix) > 1e-4)[0]
            mix = mix[: (last[-1] + int(0.05 * SR)) if len(last) else n]
            mix = limit(level(mix, -14.0))
            name = f"{skill}_{d}.mp3"
            save_mp3(mix, os.path.join(OUT, "audio", name))
            for cue, x in cues.items():
                save_mp3(limit(level(x, -16.0)), os.path.join(OUT, "audio", f"cue_{skill}_{cue}_{d}.mp3"), 96)
            sm["tracks"][d] = dict(file=name, length=len(mix) / SR,
                                   cues={c: f"cue_{skill}_{c}_{d}.mp3" for c in cues},
                                   prompts={c: prompt(skill, d, c) for c in s["cues"]})
            draw_check(mix, s, os.path.join(OUT, "check", f"{skill}_{d}.png"), f"{skill} {d}")
            print(f"{skill} {d}: {len(mix) / SR:.2f}s peak {20 * np.log10(np.abs(mix).max() + 1e-9):.1f} dBFS")
        meta["skills"][skill] = sm
    with open(os.path.join(OUT, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=1)


def draw_check(x, s, path, title):
    W, H = 1400, 420
    img = np.full((H, W, 3), 255, np.uint8)
    L = max(s["length"], len(x) / SR)
    # spectrogram (bottom 260 px), log frequency 40 Hz .. 16 kHz
    hop, nfft = 256, 2048
    frames = [x[i:i + nfft] * np.hanning(nfft) for i in range(0, max(1, len(x) - nfft), hop)]
    if frames:
        S = 20 * np.log10(np.abs(np.fft.rfft(np.array(frames), axis=1)) + 1e-9)
        f = np.fft.rfftfreq(nfft, 1 / SR)
        rows = np.geomspace(40, 16000, 260)[::-1]
        idx = np.searchsorted(f, rows)
        S = S[:, np.clip(idx, 0, len(f) - 1)].T
        S = np.clip((S - (S.max() - 80)) / 80, 0, 1)
        Simg = cv2.resize((S * 255).astype(np.uint8), (int(W * (len(x) / SR) / L), 260))
        col = cv2.applyColorMap(Simg, cv2.COLORMAP_MAGMA)
        img[160:420, :col.shape[1]] = col
    # waveform (top 150 px)
    xs = np.linspace(0, len(x) - 1, int(W * (len(x) / SR) / L)).astype(int)
    for i in range(len(xs) - 1):
        seg = x[xs[i]:xs[i + 1] + 1]
        if len(seg):
            y0, y1 = int(80 - seg.max() * 70), int(80 - seg.min() * 70)
            cv2.line(img, (i, y0), (i, y1), (60, 60, 60), 1)
    for t, label in s["moments"]:
        px = int(W * t / L)
        cv2.line(img, (px, 0), (px, H), (0, 160, 0), 1)
    cv2.putText(img, title, (8, 18), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 200), 2)
    cv2.imwrite(path, img)


if __name__ == "__main__":
    build(set(sys.argv[1:]) or None)
