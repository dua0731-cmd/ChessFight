"""2026-11-26 (R850): the user's four picks, shaped toward the old clips ("옛 소리랑 다듬어").

  python Tools/Sfx/Regen2026/r850_polish.py OLD_DIR             write out/R850/polished/, print before / after
  python Tools/Sfx/Regen2026/r850_polish.py OLD_DIR --install   also copy them into the game (.meta untouched)

Run r850.py first (it builds out/R850/ and r850_picks.json). No audio of the old clip goes into the result:
only two measured curves are taken from it, the level per octave band and the loudness every 20 ms, and the new
take is equalised and gain-ridden toward them (each step limited to +-12 dB). OLD_DIR holds the old files
(git show 7bdedbdb:Assets/Resources/<path> | git lfs smudge), never committed.
"""
import json, os, shutil, subprocess, sys, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r850     # noqa: E402  (imports process, which chdirs for its own imports)
import similar  # noqa: E402

SR = 44100
OUT = os.path.join(r850.OUT, 'polished')
LIMIT = 12.0
FRAME = int(0.02 * SR)
CENTRES = np.sqrt(np.array(similar.EDGES[:-1], float) * np.array(similar.EDGES[1:], float))


def load(path, ch):
    raw = subprocess.run([r850.process.FF, '-v', 'error', '-i', path, '-ac', str(ch), '-ar', str(SR), '-f', 'f32le', '-'],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64).reshape(-1, ch)


def equalise(x, old_mono):
    diff = similar.bands(old_mono) - similar.bands(x.mean(axis=1))
    # a band both clips have at the -40 dB floor says nothing; the rest is centred so the overall level stays
    diff = np.clip(diff - np.median(diff), -LIMIT, LIMIT)
    n = len(x) * 2
    f = np.fft.rfftfreq(n, 1 / SR)
    gain = 10 ** (np.interp(np.log(np.maximum(f, 1)), np.log(CENTRES), diff) / 20)
    return np.fft.irfft(np.fft.rfft(x, n, axis=0) * gain[:, None], n, axis=0)[:len(x)]


def first_sound(mono):
    a = np.abs(mono)
    return int(np.argmax(a > a.max() * 0.03))


def frame_db(mono, frames):
    return np.array([10 * np.log10((mono[i * FRAME:(i + 1) * FRAME] ** 2).mean() + 1e-12)
                     if i * FRAME < len(mono) else -120.0 for i in range(frames)])


def shape(x, old_mono):
    start, old_start = first_sound(x.mean(axis=1)), first_sound(old_mono)
    body = x[start:]
    frames = int(np.ceil(len(body) / FRAME))
    old = frame_db(old_mono[old_start:], frames)
    new = frame_db(body.mean(axis=1), frames)
    old, new = np.maximum(old - old.max(), -50), np.maximum(new - new.max(), -50)
    gain = np.clip(old - new, -LIMIT, LIMIT)
    t = np.arange(len(body))
    g = 10 ** (np.interp(t, (np.arange(frames) + 0.5) * FRAME, gain) / 20)
    y = x.copy()
    y[start:] *= g[:, None]
    return y


def save(x, path):
    if path.endswith('.wav'):
        with wave.open(path, 'wb') as w:
            w.setnchannels(x.shape[1]); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes((x * 32767).astype('<i2').tobytes())
        return
    subprocess.run([r850.process.FF, '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', str(x.shape[1]),
                    '-i', '-', '-c:a', 'libmp3lame', '-b:a', '128k', path],
                   input=x.astype(np.float32).tobytes(), check=True)


def main(old_dir, install):
    os.makedirs(OUT, exist_ok=True)
    picks_path = os.path.join(HERE, 'r850_picks.json')
    with open(picks_path, encoding='utf-8') as f:
        data = json.load(f)
    for name, clip in data['clips'].items():
        asset = os.path.join(r850.ROOT, clip['asset'])
        ext = os.path.splitext(asset)[1]
        ch = 1 if ext == '.wav' else 2
        old_path = os.path.join(old_dir, name + ext)
        old = load(old_path, ch)
        old_mono = r850.load(old_path)
        src = os.path.join(r850.OUT, f"{name}_{clip['pick']}{ext}")
        x = load(src, ch)
        before = similar.distance(old_mono, x.mean(axis=1), name)
        for _ in range(2):
            x = shape(equalise(x, old_mono), old_mono)
        n = min(len(x), int(0.01 * SR))
        x[-n:] *= np.linspace(1, 0, n)[:, None]
        x *= min(np.abs(old).max(), 0.94) / (np.abs(x).max() + 1e-9)
        out = os.path.join(OUT, name + ext)
        save(x, out)
        made = r850.load(out)
        after = similar.distance(old_mono, made, name)
        ring = r850.ring_ms(out)
        clip['polished'] = dict(distance_before=round(before[0], 1), distance=round(after[0], 1),
                                bands=round(after[1], 1), envelope=round(after[2], 1),
                                len_s=round(len(made) / SR, 2), coin_ring_ms=ring)
        print(f'{name:11s} take {clip["pick"]}: distance {before[0]:5.1f} -> {after[0]:5.1f} '
              f'(bands {after[1]:4.1f}, env {after[2]:4.1f})  ring {ring} ms')
        if install:
            assert not ring and os.path.exists(asset + '.meta'), name
            shutil.copyfile(out, asset)
    with open(picks_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    main(os.path.abspath(sys.argv[1]), '--install' in sys.argv)
