"""R600 / R601: bakes the picked course reveal music into
Assets/Resources/Loading/CourseReveal/Sound/RevealMusic.wav for LoadingScreenView.RevealSound.cs: R600 "D" (승규 님 "D로"),
then R601 "G" (승규 님 "G로 해서 넣어 봐"), then R605 "K" with a loop that goes on until the match starts
(승규 님 "K로 해주고 실제 인게임이 시작 될 때 까지 사운드가 유지 되야 해"). A letter is looked up in picks.json, picks_scene.json, picks_game.json (R602) and picks_show.json (R604); "take@seconds"
loops on that period, as in mix.py.

Same treatment as the listening page (mix.py): the take looped on its own beat where it fades out early, a 0.4 s fade in
with the dark, and cut dead on the 1 (the clip is exactly ONE seconds long, so it ends there). The clip is peak normalised
to -1 dBFS; MUSIC_GAIN (printed) is the cue gain that puts it back at the page's level, RMS -25 dBFS."""
import os, wave, json, sys
import numpy as np, miniaudio
from scipy import signal

here = os.path.dirname(os.path.abspath(__file__))
dest = os.path.join(here, '../../../../../Assets/Resources/Loading/CourseReveal/Sound/RevealMusic.wav')
SR, ONE, PICK = 44100, 9.8, sys.argv[1] if len(sys.argv) > 1 else 'D'


def rms_db(x): return 20 * np.log10(np.sqrt((x ** 2).mean()) + 1e-12)


def load(name):
    d = miniaudio.decode_file(os.path.join(here, 'raw', name + '.mp3'), miniaudio.SampleFormat.FLOAT32, 2, SR)
    return np.frombuffer(d.samples, np.float32).reshape(-1, 2).astype(float).T


def fill(a, need, force=None, cap=None):   # as mix.py; cap: loop from there at the latest
    mono = a.mean(0)
    sec = [rms_db(mono[i:i + SR // 4]) for i in range(0, len(mono) - SR // 4, SR // 4)]
    med = np.median(sec[:28])
    end = (max(k for k in range(len(sec)) if sec[k] >= med - 6) + 1) * SR // 4
    if cap is not None: end = min(end, int(cap * SR))
    if end >= need * SR: return a[:, :int(need * SR)], None
    b, aa = signal.butter(2, 150 / (SR / 2), 'high')
    env = np.convolve(np.abs(signal.lfilter(b, aa, mono[:end])), np.ones(441) / 441, 'same')[::441]
    e = env - env.mean(); ac = np.correlate(e, e, 'full')[len(e) - 1:]
    xf = int(.12 * SR)
    lo, hi = int(1.2 * 100), min(int(4 * 100), len(ac) - 1, (end - xf) // 441 - 1)
    P = (lo + int(np.argmax(ac[lo:hi]))) * 441
    if force: P = int(round(force * 100)) * 441
    seg = a[:, end - P - xf:end]
    out = a[:, :end].copy()
    while out.shape[1] < need * SR:
        ramp = np.linspace(0, 1, xf)
        tail = out[:, -xf:] * (1 - ramp) + seg[:, :xf] * ramp
        out = np.concatenate([out[:, :-xf], tail, seg[:, xf:]], axis=1)
    return out[:, :int(need * SR)], P / SR


picks = {}
for f in ('picks_show.json', 'picks_game.json', 'picks_scene.json', 'picks.json'): picks.update(json.load(open(os.path.join(here, f))))
take, _, force = picks[PICK].partition('@')
# R605: the music goes on past the 1 until the match starts, so the take always loops on its beat from the 1 at the
# latest: RevealMusic.wav is the first ONE seconds (in with the dark, no cut at the end) and RevealMusicLoop.wav one
# period that follows it seamlessly (the filled take repeats exactly every period from its last loop point on).
a = load(take)
probe, period = fill(a, ONE + 1, float(force) if force else None, cap=ONE)
m, _ = fill(a, ONE + 3 * period, float(force) if force else None, cap=ONE)
P = int(round(period * SR))
level = 10 ** (-25 / 20) / (np.sqrt((m[:, :int(ONE * SR)] ** 2).mean()) + 1e-12)
m = m * level
k = int(.4 * SR); m[:, :k] *= np.linspace(0, 1, k) ** 2
s = int(ONE * SR)
intro, loop = m[:, :s], m[:, s + P:s + 2 * P]
assert np.abs(m[:, s:s + P] - loop).max() < 1e-6, 'not periodic from the 1'
peak = max(np.abs(intro).max(), np.abs(loop).max()); scale = 10 ** (-1 / 20) / peak
def write(path, x):
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x.T * scale, -1, 1) * 32767).astype('<i2').tobytes())
write(dest, intro)
write(dest.replace('RevealMusic.wav', 'RevealMusicLoop.wav'), loop)
print(json.dumps({'take': take, 'loop_period_s': round(P / SR, 4), 'intro_s': intro.shape[1] / SR, 'loop_s': loop.shape[1] / SR,
                  'MUSIC_GAIN': round(1 / scale, 4)}))
