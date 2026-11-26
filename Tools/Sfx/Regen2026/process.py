"""2026-11-26 (R845): turn the regenerated takes into the clips the game plays, under the old names.

  python3 Tools/Sfx/Regen2026/process.py            check every take, pick one per clip, write picks.json + out/
  python3 Tools/Sfx/Regen2026/process.py --install  also copy out/ over Assets/Resources/... (the .meta files stay)

Each clip had two takes (ElevenLabs Sound Effects v2 on the paid plan, flow ULhjnpqP98wVoPRP9vI3, sessions.json).
Post-processing is the one the old clip got (plan.json "kind"):
  chain         Tools/Sfx/RookChain/process.py: low-pass twice, high-pass 40 Hz, pitched down by resampling,
                the silence in front cut, RMS set, peak under 0.89, mono 44.1 kHz mp3 128k (ChainRun: a seamless loop)
  export_unity  Tools/Sfx/PawnRushSkills/export_unity.py cue(): dc block, trim, the first hit only for the
                one-hit moments (tick, turn), level -16, soft limit -1 dBFS, mono mp3 160k
  rope          the same chain as export_unity (not recorded for the 10-26 rope clips; their files match it)
  raw           the take as downloaded (the Sword Fight clips were never processed)
The pick between the two takes: no coin-like ring (Tools/Sfx/PawnRushSkills/coin.py, measured on the processed
clip - 2026-11-26 user "동전 소리는 나게 하지마"), no boing (Tools/Sfx/SwordFightEdgeSkills/boing.py rule, Sword
Fight), not near-silent, then the closer length and main-hit time to the old clip. A clip whose both takes fail
is listed under "retake" and not installed.
"""
import glob, json, os, shutil, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools/Sfx/PawnRushSkills'))
os.chdir(os.path.join(ROOT, 'Tools/Sfx/PawnRushSkills'))   # build.py imports its spec module from here
from build import FF, SR, load, dc_block, trim, first_event, level, limit, env_db  # noqa: E402
from coin import rings  # noqa: E402

TAKES = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/takes')
OUT = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/out')
plan = json.load(open(os.path.join(HERE, 'plan.json'), encoding='utf-8'))

# Tools/Sfx/RookChain/process.py PICK: (target rms dB, low-pass Hz, pitch factor, loop)
CHAIN = {
    'ChainThrow': (-19, 2600, 0.84, False), 'ChainRun': (-24, 2200, 0.80, True), 'ChainBite': (-16, 2600, 0.84, False),
    'ChainPull': (-18, 2600, 0.87, False), 'ChainOffer': (-24, 1300, 0.84, False), 'ChainGrab': (-18, 2600, 0.84, False),
    'ChainLand': (-18, 1700, 0.89, False), 'ChainRewind': (-21, 1900, 0.84, False), 'ChainBreak': (-18, 1600, 0.87, False),
    'SkillFail': (-20, 3000, 1.0, False),
}
SINGLE = {'RookTick', 'KnightTurn'}


def ffmpeg_load(path, af=None):
    cmd = [FF, '-v', 'error', '-i', path, '-ac', '1', '-ar', str(SR)]
    if af:
        cmd += ['-af', af]
    raw = subprocess.run(cmd + ['-f', 'f32le', '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def save(x, path, kbps):
    x = np.clip(x, -1, 1).astype(np.float32)
    subprocess.run([FF, '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-ar', '44100',
                    '-ac', '1', '-c:a', 'libmp3lame', '-b:a', f'{kbps}k', path], input=x.tobytes(), check=True)


def chain(path, name):
    rms_db, lp, pf, loop = CHAIN[name]
    x = ffmpeg_load(path, f'lowpass=f={lp},lowpass=f={lp},highpass=f=40,asetrate={int(SR * pf)},aresample={SR}')
    if loop:
        n = int(0.25 * SR)
        ramp = np.linspace(0, 1, n)
        x = np.concatenate([x[n:-n], x[-n:] * (1 - ramp) + x[:n] * ramp])
    else:
        env = np.abs(x)
        start = max(0, int(np.argmax(env > env.max() * 10 ** (-22 / 20))) - int(0.012 * SR))
        x = x[start:].copy()
        n = min(len(x), int(0.06 * SR))
        x[-n:] *= np.linspace(1, 0, n)
    x *= 10 ** (rms_db / 20) / (np.sqrt((x ** 2).mean()) + 1e-9)
    pk = np.abs(x).max()
    return x * (0.89 / pk) if pk > 0.89 else x


def export_unity(path, name):
    x = trim(dc_block(load(path)))
    if name in SINGLE:
        x = trim(first_event(x))
    return limit(level(x, -16.0))


def boing_ms(x):
    """Tools/Sfx/SwordFightEdgeSkills/boing.py at 48 kHz: longest held 120-1800 Hz tone (>= 120 ms = boing)."""
    nfft, hop = 4096, 512
    x = np.concatenate([x, np.zeros(nfft)])
    w = np.hanning(nfft)
    S = 20 * np.log10(np.abs(np.fft.rfft(np.array([x[i:i + nfft] * w for i in range(0, len(x) - nfft, hop)]), axis=1)) + 1e-9)
    f = np.fft.rfftfreq(nfft, 1 / SR)
    band = (f >= 120) & (f <= 1800)
    fb, top = f[band], S.max()
    best = cur = 0
    last = None
    for row in S[:, band]:
        k = int(np.argmax(row))
        ok = row[k] - np.median(row) >= 18 and row[k] >= top - 20
        cur = cur + 1 if ok and last is not None and abs(fb[k] - last) / last <= 0.08 else (1 if ok else 0)
        last = fb[k] if ok else None
        best = max(best, cur)
    return best * hop / SR * 1000


def main(install):
    os.makedirs(OUT, exist_ok=True)
    picks, retake = {}, []
    for p in plan:
        folder, name, kind = p['folder'], p['name'], p['kind']
        cands = []
        for take in sorted(glob.glob(os.path.join(TAKES, folder, f'{name}_*.mp3'))):
            if kind == 'chain':
                x = chain(take, name)
            elif kind in ('export_unity', 'rope'):
                x = export_unity(take, name)
            else:
                x = load(take)
            t = trim(x, -50) if kind == 'raw' else x
            peak_db = 20 * np.log10(np.abs(x).max() + 1e-9)
            r = rings(trim(dc_block(t)))   # exactly coin.py's own path
            ring = max((c['n'] * 512 / SR for c in r), default=0.0)
            boing = boing_ms(t) if folder == 'SwordFightSkillSfx' else 0.0
            length = len(x) / SR
            hit = int(np.argmax(env_db(x, 0.02))) / SR
            bad = []
            if ring:
                bad.append(f'coin ring {ring * 1000:.0f} ms')
            if boing >= 120:
                bad.append(f'boing {boing:.0f} ms')
            if peak_db < -30:
                bad.append(f'near silent ({peak_db:.0f} dBFS)')
            score = abs(length - p['old_len_s']) + 2 * abs(hit - p['old_peak_s'])
            cands.append(dict(take=os.path.relpath(take, ROOT), len_s=round(length, 3), hit_s=round(hit, 3),
                              peak_db=round(peak_db, 1), bad=bad, score=round(score, 3), x=x))
        ok = [c for c in cands if not c['bad']]
        if not ok:
            retake.append(dict(folder=folder, name=name, takes=[{k: v for k, v in c.items() if k != 'x'} for c in cands]))
            continue
        best = min(ok, key=lambda c: c['score'])
        ext = '.mp3'
        out = os.path.join(OUT, folder, name + ext)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        if kind == 'raw':
            shutil.copyfile(os.path.join(ROOT, best['take']), out)
        else:
            save(best['x'], out, 128 if kind == 'chain' else 160)
        picks[f'{folder}/{name}'] = dict(take=best['take'], kind=kind, old_len_s=p['old_len_s'], new_len_s=best['len_s'],
                                         old_hit_s=p['old_peak_s'], new_hit_s=best['hit_s'], others=[
                                             {k: v for k, v in c.items() if k != 'x'} for c in cands if c is not best])
        if install:
            dest = os.path.join(ROOT, 'Assets/Resources', folder, name + ext)
            assert os.path.exists(dest + '.meta'), dest
            shutil.copyfile(out, dest)
    json.dump(dict(generated='2026-11-26', plan='ElevenLabs paid plan (upgraded 2026-10-29 22:42 KST)',
                   model='eleven_text_to_sound_v2', flow='ULhjnpqP98wVoPRP9vI3', picks=picks, retake=retake),
              open(os.path.join(HERE, 'picks.json'), 'w'), indent=1, ensure_ascii=False)
    print(len(picks), 'picked,', len(retake), 'need a retake:', ', '.join(r['name'] for r in retake))
    for k, v in picks.items():
        print(f"{k:40s} len {v['old_len_s']:.2f}->{v['new_len_s']:.2f}  hit {v['old_hit_s']:.2f}->{v['new_hit_s']:.2f}")


if __name__ == '__main__':
    main('--install' in sys.argv)
