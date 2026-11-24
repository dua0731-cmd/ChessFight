# R742 (user 11-21: "천문대 사운드 필요한 곳 확인해" -> "네, 만들어"): the Royal Observatory's sounds.
# The takes are ElevenLabs sound effects (flow "R739 왕실 천문대 소리" (named R739 before the number was moved to R742; other sessions had taken R739 - R741) 3SsTjKFY5rvOHg2svvFA, eleven_text_to_sound_v2,
# prompt_influence 0.45, up to 4 takes a prompt - a few came back 2 or 3 when the queue was full; prompts.md) in
# raw/<kind>_<n>.mp3. This script measures every take and picks one per kind by the numbers (the AI cannot listen), or
# the one picks.json names, and writes Assets/Resources/RoyalObservatorySfx/<Name>.wav (RoyalObservatorySound.cs plays
# them) with a .meta beside each new clip (mono, PCM, preloaded, no import normalise - the carriage yard's settings).
# Every take, trimmed the same way, goes to takes/ for the listening page (page.py).
#   uv run --no-project --python "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" --with miniaudio --with numpy python -I bake.py <this folder> <project root>
import os, sys, json, wave, hashlib, numpy as np, miniaudio

S, ROOT = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(ROOT, 'ArtSource/Loading/SwordPick/Sound'))
from coin2 import rings

SR = 44100
OUT_REL = 'Assets/Resources/RoyalObservatorySfx'
OUT = os.path.join(ROOT, OUT_REL)
TAKES = os.path.join(S, 'takes')

# kind: (file name, loop, longest it may ring on after its loudest point (s; loops: None), starts with a build-up,
#        a ringing tone is what it is meant to have)
KINDS = {
    'meteor_warn':   ('RoMeteorWarn',   False, .7,  False, True),
    'meteor_fall':   ('RoMeteorFall',   False, .25, True,  False),
    'meteor_impact': ('RoMeteorImpact', False, 1.0, False, True),
    'flare_charge':  ('RoFlareCharge',  False, .2,  True,  False),
    'flare_burst':   ('RoFlareBurst',   False, 1.5, False, False),
    'flare_hit_me':  ('RoFlareHitMe',   False, .5,  False, False),
    'beam_hum':      ('RoBeamHum',      True,  None, False, True),
    'beam_strike':   ('RoBeamStrike',   False, .5,  False, False),
    'beam_hit_me':   ('RoBeamHitMe',    False, .45, False, False),
    'iris_warn':     ('RoIrisWarn',     False, .7,  False, True),
    'iris_scrape':   ('RoIrisScrape',   False, .4,  True,  False),
    'iris_clash':    ('RoIrisClash',    False, .8,  False, True),
    'petal_warn':    ('RoPetalWarn',    False, .7,  False, False),
    'petal_fold':    ('RoPetalFold',    False, .4,  False, False),
    'petal_rise':    ('RoPetalRise',    False, .5,  True,  False),
    'orbit_loop':    ('RoOrbitLoop',    True,  None, False, False),
    'gear_loop':     ('RoGearLoop',     True,  None, False, False),
    'planet_roll':   ('RoPlanetRoll',   True,  None, False, False),
    'planet_dock':   ('RoPlanetDock',   False, .5,  False, False),
    'globe_creak':   ('RoGlobeCreak',   True,  None, False, False),
    'hall_ambience': ('RoHallAmbience', True,  None, False, False),
    'balcony_wind':  ('RoBalconyWind',  True,  None, False, False),
}
# Every one-shot is peak-normalised to this and every loop's RMS to LoopRms (its peak no higher than OneShotPeak);
# how loud each is heard is the code's (RoyalObservatorySound's volumes).
OneShotPeak, LoopRms = -1.0, -20.0
# One-shots kept whole (a build-up or a rattle that lasts its device's whole warning): only their edges are faded, and
# the numbers only look for a ringing tone and a faint take.
WHOLE = {'flare_charge', 'petal_warn', 'iris_warn'}
# Loops that must sound heavy (picked toward the darker take). The gears' first takes sounded like a toy (user 11-21);
# they are kept in raw/toy_gear_R742, and gear_loop_1 .. 8 are the heavy prompts (prompts.md).
HEAVY = {'gear_loop'}

# How loud each is heard, close to it, at the settings' default effects volume (PAWN_RUSH_AUDIO.md section 0: a usual
# one-off -21 dB over its loudest 400 ms, a big dangerous hit -18 dB, a standing loop -30 dB RMS); bake.py prints the
# AudioSource volume that gives it (`vol`), which RoyalObservatorySound's table carries.
EFFECTS = .8
HEARD = dict(meteor_warn=-24, meteor_fall=-24, meteor_impact=-18, flare_charge=-18, flare_burst=-15, flare_hit_me=-20,
             beam_hum=-30, beam_strike=-21, beam_hit_me=-20, iris_warn=-24, iris_scrape=-22, iris_clash=-18,
             petal_warn=-25, petal_fold=-24, petal_rise=-23, orbit_loop=-32, gear_loop=-31, planet_roll=-30,
             planet_dock=-21, globe_creak=-34, hall_ambience=-34, balcony_wind=-32)


def loudness(y, loop):
    if loop: return db(np.sqrt((y * y).mean()))
    w = int(SR * .4)
    if len(y) <= w: return db(np.sqrt((y * y).mean()))
    c = np.cumsum(np.concatenate([[0.0], y * y]))
    return db(np.sqrt(((c[w:] - c[:-w]) / w).max()))


META = """fileFormatVersion: 2
guid: {g}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def load_mp3(p):
    d = miniaudio.decode_file(p, output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    return np.frombuffer(d.samples, dtype=np.float32).astype(np.float64)


def env(x, win=0.005):
    n = max(1, int(SR * win))
    return np.sqrt(np.convolve(x * x, np.ones(n) / n, mode='same') + 1e-12)


def db(v):
    return 20 * np.log10(max(v, 1e-9))


def measure(x, kind):
    _, loop, _, _, _ = KINDS[kind]
    e = env(x)
    top = e.max()
    m = dict(peak=db(np.abs(x).max()), rms=db(np.sqrt((x * x).mean())), length=len(x) / SR, rings=len(rings(x)))
    if loop:
        # How steady it is (the spread of its half-second loudness, dB) and how well its end meets its start.
        w = int(SR * .5)
        parts = [db(np.sqrt((x[i:i + w] ** 2).mean())) for i in range(0, len(x) - w, w)]
        m['spread'] = float(np.std(parts)) if parts else 0.0
        k = int(SR * .02)
        m['seam'] = abs(db(np.sqrt((x[-k:] ** 2).mean())) - db(np.sqrt((x[:k] ** 2).mean())))
        m['jump'] = float(abs(x[-1] - x[0]) / max(np.abs(x).max(), 1e-9))
        spec = np.abs(np.fft.rfft(x * np.hanning(len(x))))
        f = np.fft.rfftfreq(len(x), 1 / SR)
        m['centroid'] = float((spec * f).sum() / max(spec.sum(), 1e-9))
        return m
    onset = int(np.where(e > top * 0.1)[0][0])
    hit = int(np.argmax(e))
    after = np.where(e[hit:] < top * 10 ** (-40 / 20))[0]
    m.update(onset=onset / SR, hit=hit / SR, tail=(after[0] if len(after) else len(x) - hit) / SR)
    return m


def score(m, kind):
    # lower is better
    _, loop, tail, swell, ringing = KINDS[kind]
    s = 0.0
    if m['peak'] < (-45 if loop else -30): s += 100  # near silent (an ambience is quiet by nature)
    if not ringing: s += 6 * m['rings']              # a ringing tone (a beep) where none belongs
    s += .05 * max(0.0, -12 - m['peak'])             # a faint take brings its noise up when normalised
    if loop:
        s += 2 * m['spread'] + .5 * m['seam'] + 20 * m['jump']
        # user 11-21 "카시오페이 톱니 장난감 스러운 사운드야 묵직한 걸로해": the gears must sound heavy - the brighter, the worse
        if kind in HEAVY: s += m['centroid'] / 400
        return s
    if kind in WHOLE:
        # a charge should be loudest at its end, where the burst follows
        if kind == 'flare_charge': s += .5 * (m['length'] - m['hit'])
        return s + 2 * max(0.0, m['onset'] - .05)
    late = m['onset'] if swell else m['hit']
    s += 40 * max(0.0, late - (.15 if swell else .04))
    s += 20 * max(0.0, m['tail'] - tail)
    s += 2 * m['hit'] if not swell else 0.0
    return s


def trim(x, m, kind):
    _, loop, tail, _, _ = KINDS[kind]
    if loop:
        y = x.copy()
        y *= 10 ** (LoopRms / 20) / max(np.sqrt((y * y).mean()), 1e-9)
        top = np.abs(y).max()
        if top > 10 ** (OneShotPeak / 20): y *= 10 ** (OneShotPeak / 20) / top
        return y
    start = max(0, int(m['onset'] * SR) - int(.003 * SR))
    if kind in WHOLE:
        y = x[start:].copy()
        fin, fout = min(len(y), int(.002 * SR)), min(len(y), int(.03 * SR))
        y[:fin] *= np.linspace(0, 1, fin)
        y[-fout:] *= np.cos(np.linspace(0, np.pi / 2, fout))
        return y * (10 ** (OneShotPeak / 20) / max(np.abs(y).max(), 1e-9))
    e = env(x)
    top = e.max()
    hit = int(np.argmax(e))
    below = np.where(e[hit:] < top * 10 ** (-48 / 20))[0]
    end = min(len(x), hit + (below[0] if len(below) else len(x) - hit) + int(.02 * SR))
    end = min(end, hit + int((tail + .25) * SR))
    y = x[start:end].copy()
    fin = min(len(y), int(.002 * SR))
    y[:fin] *= np.linspace(0, 1, fin)
    fout = min(len(y), int(.05 * SR))
    y[-fout:] *= np.cos(np.linspace(0, np.pi / 2, fout))
    return y * (10 ** (OneShotPeak / 20) / max(np.abs(y).max(), 1e-9))


def write_wav(p, y, sr=SR):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    if sr != SR:
        t = np.arange(0, len(y) * sr / SR) * SR / sr
        y = np.interp(t, np.arange(len(y)), y)
    with wave.open(p, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())


def meta(name):
    rel = f'{OUT_REL}/{name}.wav'
    path = os.path.join(ROOT, rel) + '.meta'
    if os.path.exists(path): return 'kept'
    g = hashlib.md5(('chessfight:' + rel).encode()).hexdigest()
    with open(path, 'w', newline='\n') as f: f.write(META.format(g=g))
    return 'new'


def folder_meta():
    path = OUT + '.meta'
    if os.path.exists(path): return
    g = hashlib.md5(('chessfight:' + OUT_REL).encode()).hexdigest()
    with open(path, 'w', newline='\n') as f:
        f.write(f'fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


os.makedirs(OUT, exist_ok=True)
folder_meta()
picks_path = os.path.join(S, 'picks.json')
# picks.json: kind -> a take of its own ("3") or of another kind ("iris_warn_2"); a kind left out is picked by the numbers.
chosen = json.load(open(picks_path)) if os.path.exists(picks_path) else {}
measured = {}
for kind in KINDS:
    for n in range(1, 9):
        p = os.path.join(S, 'raw', f'{kind}_{n}.mp3')
        if not os.path.exists(p): continue
        x = load_mp3(p)
        m = measure(x, kind)
        m['take'] = n
        m['score'] = round(score(m, kind), 3)
        measured[f'{kind}_{n}'] = (m, x)
        write_wav(os.path.join(TAKES, f'{kind}_{n}.wav'), trim(x, m, kind), 22050)
report = {}
for kind, (name, loop, _, _, _) in KINDS.items():
    rows = [measured[k] for k in measured if k.rsplit('_', 1)[0] == kind]
    best = min(rows, key=lambda r: r[0]['score'])
    want = str(chosen.get(kind, best[0]['take']))
    source = want if '_' in want else f'{kind}_{want}'
    m, x = measured[source]
    y = trim(x, measure(x, source.rsplit('_', 1)[0]), source.rsplit('_', 1)[0])
    write_wav(os.path.join(OUT, name + '.wav'), y)
    loud = loudness(y, loop)
    vol = min(1.0, 10 ** ((HEARD[kind] - loud) / 20) / EFFECTS)
    report[kind] = dict(file=name, picked=source, by='picks.json' if kind in chosen else 'numbers', loop=loop,
                        length=round(len(y) / SR, 3), loudness=round(loud, 1), heard=HEARD[kind], vol=round(vol, 3),
                        takes=[{k: (round(v, 3) if isinstance(v, float) else v) for k, v in r[0].items()} for r in rows])
    print(f"{kind:14s} {source:16s} {report[kind]['by']:7s} {len(y) / SR:6.2f} s  vol {vol:.3f}  meta {meta(name)}  " +
          '  '.join(f"{r[0]['take']}:{r[0]['score']:.2f}" for r in rows))
json.dump(report, open(os.path.join(S, 'report.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
